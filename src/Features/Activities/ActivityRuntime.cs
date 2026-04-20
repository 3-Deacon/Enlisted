using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Activities
{
    /// <summary>
    /// Singleton host for active Activities. Ticks on hourly + daily campaign events,
    /// picks phase-pool storylets per tick (for Auto phases), parks chain candidates
    /// targeted at named activity phases via "when: activity.phase" until the activity
    /// reaches that phase.
    /// </summary>
    public sealed class ActivityRuntime : CampaignBehaviorBase
    {
        public static ActivityRuntime Instance { get; private set; }

        private List<Activity> _active = new List<Activity>();
        private Dictionary<string, ActivityTypeDefinition> _types =
            new Dictionary<string, ActivityTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        // Parked chain continuations: key = "activityType.phaseId", value = list of storylet ids to fire on entry.
        private Dictionary<string, List<string>> _parkedChains =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<Activity> ActiveActivities => _active;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_active", ref _active);
                dataStore.SyncData("_parkedChains", ref _parkedChains);
                _active ??= new List<Activity>();
                _parkedChains ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ACTIVITY", "ActivityRuntime SyncData failed", ex);
                _active = new List<Activity>();
                _parkedChains = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Instance = this;
            ActivityTypeCatalog.LoadAll();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            Instance = this;
            foreach (var a in _active)
            {
                a.ResolvePhasesFromType(_types);
            }
        }

        public void RegisterType(ActivityTypeDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id))
            {
                return;
            }
            _types[def.Id] = def;
        }

        public void Start(Activity activity, ActivityContext ctx)
        {
            if (activity == null)
            {
                return;
            }
            activity.ResolvePhasesFromType(_types);
            activity.StartedAt = CampaignTime.Now;
            activity.OnStart(ctx);
            if (activity.CurrentPhase != null)
            {
                activity.OnPhaseEnter(activity.CurrentPhase);
            }
            _active.Add(activity);
            ConsumeParkedChains(activity);
        }

        public void ParkChain(string activityTypeId, string phaseId, string storyletId)
        {
            if (string.IsNullOrEmpty(activityTypeId) || string.IsNullOrEmpty(phaseId) || string.IsNullOrEmpty(storyletId))
            {
                return;
            }
            var key = activityTypeId + "." + phaseId;
            if (!_parkedChains.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _parkedChains[key] = list;
            }
            list.Add(storyletId);
        }

        private void ConsumeParkedChains(Activity activity)
        {
            if (activity?.CurrentPhase == null)
            {
                return;
            }
            var key = activity.TypeId + "." + activity.CurrentPhase.Id;
            if (!_parkedChains.TryGetValue(key, out var list) || list.Count == 0)
            {
                return;
            }
            foreach (var storyletId in list)
            {
                var storylet = StoryletCatalog.GetById(storyletId);
                if (storylet == null)
                {
                    continue;
                }
                var ctx = new StoryletContext
                {
                    ActivityTypeId = activity.TypeId,
                    PhaseId = activity.CurrentPhase.Id
                };
                var candidate = storylet.ToCandidate(ctx);
                if (candidate != null)
                {
                    candidate.ChainContinuation = true;
                    StoryDirector.Instance?.EmitCandidate(candidate);
                }
            }
            list.Clear();
        }

        private void OnHourlyTick()
        {
            foreach (var a in _active.ToList())
            {
                try
                {
                    a.Tick(hourly: true);
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("ACTIVITY", "Activity hourly tick failed", ex);
                }

                if (a.CurrentPhase == null)
                {
                    continue;
                }
                TryFireAutoPhaseStorylet(a);
                TryAdvancePhase(a);
            }
        }

        private void OnDailyTick()
        {
            foreach (var a in _active.ToList())
            {
                try
                {
                    a.Tick(hourly: false);
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("ACTIVITY", "Activity daily tick failed", ex);
                }
            }
        }

        private void TryFireAutoPhaseStorylet(Activity a)
        {
            var phase = a.CurrentPhase;
            if (phase == null || phase.Delivery != PhaseDelivery.Auto)
            {
                return;
            }
            if (phase.Pool == null || phase.Pool.Count == 0)
            {
                return;
            }

            var ctx = new StoryletContext
            {
                ActivityTypeId = a.TypeId,
                PhaseId = phase.Id,
                CurrentContext = DeriveContext()
            };

            // Weighted eligible selection.
            var eligible = new List<(Storylet storylet, float weight)>();
            foreach (var id in phase.Pool)
            {
                var s = StoryletCatalog.GetById(id);
                if (s == null)
                {
                    continue;
                }
                if (!s.IsEligible(ctx))
                {
                    continue;
                }
                var w = s.WeightFor(ctx);
                if (phase.IntentBias != null && phase.IntentBias.TryGetValue(a.Intent ?? string.Empty, out var bias))
                {
                    w *= bias;
                }
                if (w > 0f)
                {
                    eligible.Add((s, w));
                }
            }
            if (eligible.Count == 0)
            {
                return;
            }

            var total = eligible.Sum(e => e.weight);
            var r = (float)(new Random().NextDouble() * total);
            var cursor = 0f;
            foreach (var (s, w) in eligible)
            {
                cursor += w;
                if (r > cursor)
                {
                    continue;
                }
                var candidate = s.ToCandidate(ctx);
                if (candidate != null)
                {
                    StoryDirector.Instance?.EmitCandidate(candidate);
                }
                return;
            }
        }

        private void TryAdvancePhase(Activity a)
        {
            var phase = a.CurrentPhase;
            if (phase == null)
            {
                return;
            }
            var elapsedHours = (CampaignTime.Now - a.StartedAt).ToHours;
            var cumulative = 0;
            for (var i = 0; i <= a.CurrentPhaseIndex && i < a.Phases.Count; i++)
            {
                cumulative += a.Phases[i].DurationHours;
            }
            if (elapsedHours < cumulative)
            {
                return;
            }

            a.OnPhaseExit(phase);
            a.CurrentPhaseIndex++;
            if (a.CurrentPhaseIndex >= a.Phases.Count)
            {
                a.Finish(ActivityEndReason.Completed);
                _active.Remove(a);
                return;
            }
            a.OnPhaseEnter(a.CurrentPhase);
            ConsumeParkedChains(a);
        }

        private static string DeriveContext()
        {
            var p = MobileParty.MainParty;
            if (p == null)
            {
                return "any";
            }
            if (p.BesiegerCamp != null || p.CurrentSettlement?.SiegeEvent != null)
            {
                return "siege";
            }
            if (p.CurrentSettlement != null)
            {
                return "settlement";
            }
            if (p.IsCurrentlyAtSea)
            {
                return "sea";
            }
            return "land";
        }
    }
}
