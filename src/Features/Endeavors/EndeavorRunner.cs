using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Activities;
using Enlisted.Features.Companions;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Qualities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Orchestrates the endeavor lifecycle: catalog load on session launch,
    /// hourly tick that fires phase modals on schedule, resolution dispatch when
    /// all phases have been fired and the duration has elapsed, and per-category
    /// cooldown bookkeeping. Time advances phases — player option picks
    /// contribute score via the endeavor_score_* scripted effects but do not
    /// directly advance phase index. If the player ignores a phase modal, its
    /// score for that phase is zero.
    /// </summary>
    public sealed class EndeavorRunner : CampaignBehaviorBase
    {
        private const int CategoryCooldownDays = 3;
        private const int ResolutionGraceHours = 1;

        public static EndeavorRunner Instance { get; private set; }

        // Per-category cooldown tracker. Key: category id, value: time the most
        // recent endeavor in that category resolved. New endeavors in that
        // category are unavailable until cooldown elapses.
        private Dictionary<string, CampaignTime> _categoryLastResolved =
            new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        public EndeavorRunner()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                _ = dataStore.SyncData("_categoryLastResolved", ref _categoryLastResolved);
                _categoryLastResolved ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            });
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Instance = this;
            EndeavorCatalog.LoadAll();
            ModLogger.Info("ENDEAVOR", $"EndeavorRunner ready ({EndeavorCatalog.Count} templates loaded)");
        }

        private void OnHourlyTick()
        {
            var activity = EndeavorActivity.Instance;
            if (activity == null)
            {
                return;
            }
            var template = EndeavorCatalog.GetById(activity.EndeavorId);
            if (template == null)
            {
                ModLogger.Expected("ENDEAVOR", "active_unknown_template",
                    "Active endeavor references a template no longer in the catalog");
                return;
            }

            var totalPhases = activity.GetTotalPhases();
            if (totalPhases <= 0)
            {
                totalPhases = template.PhasePool.Count;
                activity.SetTotalPhases(totalPhases);
            }

            var elapsedHours = (CampaignTime.Now - activity.StartedOn).ToHours;
            var phaseIndex = activity.GetPhaseIndex();
            var targetIndex = ComputeTargetPhaseIndex(template, elapsedHours, totalPhases);

            // Fire any phases that are due but haven't been fired yet. Multiple
            // phases can fire in one tick if the player paused for many in-game
            // hours between phases.
            while (phaseIndex < targetIndex && phaseIndex < totalPhases)
            {
                EndeavorPhaseProvider.FirePhase(activity);
                phaseIndex++;
                activity.SetPhaseIndex(phaseIndex);
            }

            // Resolution: all phases fired AND total duration elapsed (+ grace).
            var resolutionDueHours = template.DurationDays * 24f + ResolutionGraceHours;
            if (phaseIndex >= totalPhases && elapsedHours >= resolutionDueHours)
            {
                CompleteEndeavor(activity, template, ActivityEndReason.Completed);
            }
        }

        public bool CanStartCategory(string category)
        {
            if (!_categoryLastResolved.TryGetValue(category, out var lastResolved))
            {
                return true;
            }
            var sinceDays = (CampaignTime.Now - lastResolved).ToDays;
            return sinceDays >= CategoryCooldownDays;
        }

        public CampaignTime GetCategoryCooldownEnd(string category)
        {
            if (!_categoryLastResolved.TryGetValue(category, out var lastResolved))
            {
                return CampaignTime.Zero;
            }
            return lastResolved + CampaignTime.Days(CategoryCooldownDays);
        }

        public bool StartEndeavor(EndeavorTemplate template, IList<Hero> assignedCompanions)
        {
            if (template == null)
            {
                return false;
            }
            if (EndeavorActivity.Instance != null)
            {
                ModLogger.Expected("ENDEAVOR", "start_blocked_by_active",
                    "Cannot start endeavor while another is active");
                return false;
            }
            if (!CanStartCategory(template.Category))
            {
                ModLogger.Expected("ENDEAVOR", "start_blocked_by_category_cooldown",
                    "Category cooldown still active");
                return false;
            }

            var runtime = ActivityRuntime.Instance;
            if (runtime == null)
            {
                ModLogger.Surfaced("ENDEAVOR", "ActivityRuntime null at endeavor start", null);
                return false;
            }

            var activity = new EndeavorActivity
            {
                EndeavorId = template.Id,
                CategoryId = template.Category,
            };
            if (assignedCompanions != null)
            {
                foreach (var hero in assignedCompanions)
                {
                    if (hero != null)
                    {
                        activity.AssignedCompanionIds.Add(hero.Id);
                    }
                }
            }

            ResetActiveScore();
            activity.SetTotalPhases(template.PhasePool.Count);
            activity.SetPhaseIndex(0);

            runtime.Start(activity, ActivityContext.FromCurrent());
            ModLogger.Info("ENDEAVOR",
                $"Started endeavor '{template.Id}' (category={template.Category}, phases={template.PhasePool.Count}, companions={activity.AssignedCompanionIds.Count})");
            return true;
        }

        public void CancelEndeavor(string reason)
        {
            var activity = EndeavorActivity.Instance;
            if (activity == null)
            {
                return;
            }
            ModLogger.Info("ENDEAVOR", $"Cancelled endeavor '{activity.EndeavorId}' (reason={reason})");
            ResetActiveScore();
            ActivityRuntime.Instance?.Stop(activity, ActivityEndReason.PlayerCancelled);
            // No category cooldown stamp on cancel — Plan 5 §4.4 says cancelling
            // forfeits progress, not "lock the category for 3 days too".
        }

        private void CompleteEndeavor(EndeavorActivity activity, EndeavorTemplate template, ActivityEndReason reason)
        {
            EndeavorPhaseProvider.FireResolution(activity);
            _categoryLastResolved[template.Category] = CampaignTime.Now;
            ModLogger.Info("ENDEAVOR",
                $"Completed endeavor '{activity.EndeavorId}' (score={activity.GetScore()}, category-cooldown {CategoryCooldownDays}d)");
            ResetActiveScore();
            ActivityRuntime.Instance?.Stop(activity, reason);
        }

        private static int ComputeTargetPhaseIndex(EndeavorTemplate template, double elapsedHours, int totalPhases)
        {
            if (totalPhases <= 0)
            {
                return 0;
            }
            // Per-phase offset table. If the template provides explicit
            // phase_offsets_hours, use it directly. Otherwise compute even
            // spacing across the duration: phase k fires at duration*k/totalPhases.
            var offsets = template.PhaseOffsetsHours;
            if (offsets == null || offsets.Count != totalPhases)
            {
                var perPhaseHours = (template.DurationDays * 24f) / Math.Max(1, totalPhases);
                var idx = (int)Math.Floor(elapsedHours / Math.Max(1f, perPhaseHours));
                return Math.Min(idx, totalPhases);
            }
            var fired = 0;
            for (var i = 0; i < offsets.Count; i++)
            {
                if (elapsedHours >= offsets[i])
                {
                    fired = i + 1;
                }
                else
                {
                    break;
                }
            }
            return fired;
        }

        private static void ResetActiveScore()
        {
            QualityStore.Instance?.Set("endeavor_active_score", 0, "endeavor reset");
        }

        public IList<Hero> GetSpawnedCompanions()
        {
            return CompanionLifecycleHandler.Instance?.GetSpawnedCompanions() ?? new List<Hero>();
        }
    }
}
