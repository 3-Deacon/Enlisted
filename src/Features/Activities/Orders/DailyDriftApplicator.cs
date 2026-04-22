using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.CampaignIntelligence.Signals;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Awards profile-weighted skill XP silently each game day and emits one accumulator-summary news-feed entry per hourly tick when the wall-clock throttle allows.</summary>
    public sealed class DailyDriftApplicator : CampaignBehaviorBase
    {
        private static readonly Dictionary<string, string[]> ProfileHeavy = new Dictionary<string, string[]>
        {
            { DutyProfileIds.Garrisoned, new[] { "OneHanded", "Polearm", "Athletics", "Charm", "Steward" } },
            { DutyProfileIds.Marching,   new[] { "Athletics", "Riding", "Scouting", "Tactics" } },
            { DutyProfileIds.Besieging,  new[] { "Engineering", "Crossbow", "Bow", "Athletics" } },
            { DutyProfileIds.Raiding,    new[] { "Athletics", "Roguery", "OneHanded", "Riding" } },
            { DutyProfileIds.Escorting,  new[] { "Tactics", "Leadership", "Riding" } },
            { DutyProfileIds.Wandering,  new[] { "Scouting", "Trade", "Roguery" } },
            { DutyProfileIds.Imprisoned, new[] { "Roguery", "Charm" } }
        };

        private int _lastHourlyHeartbeatTick = int.MinValue / 2;
        private const int HOURLY_HEARTBEAT_INTERVAL = 12;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            try
            {
                var isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted == true;
                var activity = OrderActivity.Instance;
                var profile = activity?.CurrentDutyProfile ?? "none";

                if (!isEnlisted)
                {
                    ModLogger.Info("DRIFT", $"daily_tick: not_enlisted — skipping");
                    return;
                }
                if (activity == null)
                {
                    ModLogger.Info("DRIFT", $"daily_tick: no_activity — skipping");
                    return;
                }
                activity.EnsureInitialized();

                if (!ProfileHeavy.TryGetValue(activity.CurrentDutyProfile, out var heavy))
                {
                    ModLogger.Info("DRIFT", $"daily_tick: profile={profile} not in ProfileHeavy — skipping");
                    return;
                }

                var appliedSkills = new List<string>();
                var pickCount = activity.CurrentDutyProfile == DutyProfileIds.Imprisoned ? 1 : 2;
                for (int i = 0; i < pickCount; i++)
                {
                    var skillId = heavy[MBRandom.RandomInt(heavy.Length)];
                    var amount = MBRandom.RandomInt(5, 16);

                    var skill = MBObjectManager.Instance.GetObject<SkillObject>(skillId);
                    if (skill == null)
                    {
                        ModLogger.Expected("DRIFT", "unknown_skill_id", $"ProfileHeavy references unknown SkillObject: '{skillId}'");
                        continue;
                    }

                    if (!activity.DriftPendingXp.ContainsKey(skillId))
                    {
                        activity.DriftPendingXp[skillId] = 0;
                    }
                    activity.DriftPendingXp[skillId] += amount;
                    appliedSkills.Add($"{skillId}+{amount}");

                    Hero.MainHero.HeroDeveloper.AddSkillXp(skill, amount, shouldNotify: false);
                }

                ModLogger.Info("DRIFT", $"daily_tick: profile={profile} applied=[{string.Join(",", appliedSkills)}] pending_total={activity.DriftPendingXp.Count}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DRIFT", "OnDailyTick threw", ex);
            }
        }

        private void OnHourlyTick()
        {
            try
            {
                var nowHour = (int)CampaignTime.Now.ToHours;
                var activity = OrderActivity.Instance;
                var pendingCount = activity?.DriftPendingXp?.Count ?? 0;
                var speed = Campaign.Current?.SpeedUpMultiplier ?? 1f;

                if (nowHour - _lastHourlyHeartbeatTick >= HOURLY_HEARTBEAT_INTERVAL)
                {
                    _lastHourlyHeartbeatTick = nowHour;
                    ModLogger.Info("DRIFT", $"hourly_heartbeat: enlisted={EnlistmentBehavior.Instance?.IsEnlisted == true} pending_count={pendingCount} speed={speed:F1}x");
                }

                if (EnlistmentBehavior.Instance?.IsEnlisted != true)
                {
                    return;
                }
                if (activity?.DriftPendingXp == null || activity.DriftPendingXp.Count == 0)
                {
                    return;
                }

                FlushSummary(activity);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DRIFT", "OnHourlyTick flush threw", ex);
            }
        }

        private static void FlushSummary(OrderActivity activity)
        {
            var pieces = activity.DriftPendingXp
                .OrderByDescending(kv => kv.Value)
                .Take(4)
                .Select(kv =>
                {
                    var name = MBObjectManager.Instance.GetObject<SkillObject>(kv.Key)?.Name?.ToString();
                    return $"{name ?? kv.Key} +{kv.Value}";
                });
            var summary = "Past few days: " + string.Join(", ", pieces) + " XP.";

            // Route through SignalBuilder → EmitExternalSignal. Builder selects
            // ScoutReturn for movement profiles (marching / wandering) and
            // CampTalk for camp profiles. Emitter owns the throttle gate and
            // pool lookup; the summary text is passed separately so it lands
            // in RenderedBody instead of the signal's metadata.
            var signal = EnlistedCampaignSignalBuilder.BuildDriftSummarySignal(
                activity.CurrentDutyProfile ?? "wandering");

            var emitter = EnlistedSignalEmitterBehavior.Instance;
            bool emitted;
            if (emitter != null)
            {
                emitted = emitter.EmitExternalSignal(signal, summary);
            }
            else
            {
                // Boot-ordering fallback: emitter not yet registered. Direct
                // Log-tier emit preserves the pre-T14 delivery contract.
                StoryDirector.Instance?.EmitCandidate(new StoryCandidate
                {
                    SourceId = "orders.drift.summary",
                    CategoryId = "orders.drift",
                    ProposedTier = StoryTier.Log,
                    EmittedAt = CampaignTime.Now,
                    RenderedTitle = "Service notes",
                    RenderedBody = summary,
                    StoryKey = "orders_drift_summary"
                });
                emitted = true;
            }

            if (emitted)
            {
                activity.DriftPendingXp.Clear();
                ModLogger.Info("DRIFT", "flushed summary via signals: " + summary);
            }
        }
    }
}
