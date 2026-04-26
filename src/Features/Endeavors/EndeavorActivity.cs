using System;
using System.Collections.Generic;
using Enlisted.Features.Activities;
using Enlisted.Features.Retinue.Core;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Player-issued multi-phase endeavor on the Activity backbone. Sibling to
    /// OrderActivity (lord-issued) and ContractActivity (notable-issued). Phase
    /// advancement is driven by EndeavorRunner, NOT ActivityRuntime.TryAdvancePhase
    /// — the activity_endeavor.json type definition declares a single long-duration
    /// "running" phase so the runtime stays quiet, and EndeavorRunner reads the
    /// endeavor template's PhasePool to fire phase modals on its own schedule.
    /// Phase index + total phase count + score live in AccumulatedOutcomes under
    /// reserved __-prefixed keys; per-phase choice memory rides on FlagStore keys
    /// `endeavor_choice_&lt;endeavor_id&gt;_&lt;phase&gt;_&lt;option_id&gt;`.
    /// </summary>
    [Serializable]
    public sealed class EndeavorActivity : Activity
    {
        public const string TYPE_ID = "endeavor_activity";

        public const string PhaseIndexKey = "__phase_index__";
        public const string TotalPhasesKey = "__total_phases__";
        public const string ScoreKey = "__score__";

        public string EndeavorId { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public List<MBGUID> AssignedCompanionIds { get; set; } = new List<MBGUID>();
        public CampaignTime StartedOn { get; set; } = CampaignTime.Zero;
        public Dictionary<string, int> AccumulatedOutcomes { get; set; } = new Dictionary<string, int>();

        public static EndeavorActivity Instance =>
            ActivityRuntime.Instance?.FindActive<EndeavorActivity>();

        public override void OnStart(ActivityContext ctx)
        {
            TypeId = TYPE_ID;
            EnsureInitialized();
            StartedOn = CampaignTime.Now;

            var manager = CompanionAssignmentManager.Instance;
            if (manager == null)
            {
                ModLogger.Expected("ENDEAVOR", "no_assignment_manager_at_start",
                    "CompanionAssignmentManager null at endeavor start; skipping companion lock");
                return;
            }
            foreach (var guid in AssignedCompanionIds)
            {
                var hero = ResolveHero(guid);
                if (hero != null)
                {
                    manager.SetAssignedToEndeavor(hero, true);
                }
            }
        }

        public override void Tick(bool hourly)
        {
        }

        public override void OnPhaseEnter(Phase phase)
        {
        }

        public override void OnPhaseExit(Phase phase)
        {
        }

        public override void Finish(ActivityEndReason reason)
        {
            EnsureInitialized();
            var manager = CompanionAssignmentManager.Instance;
            if (manager == null)
            {
                ModLogger.Expected("ENDEAVOR", "no_assignment_manager_at_finish",
                    "CompanionAssignmentManager null at endeavor finish; companion locks may persist");
                return;
            }
            foreach (var guid in AssignedCompanionIds)
            {
                var hero = ResolveHero(guid);
                if (hero != null)
                {
                    manager.SetAssignedToEndeavor(hero, false);
                }
            }
        }

        public int GetPhaseIndex()
        {
            EnsureInitialized();
            return AccumulatedOutcomes.TryGetValue(PhaseIndexKey, out var v) ? v : 0;
        }

        public void SetPhaseIndex(int index)
        {
            EnsureInitialized();
            AccumulatedOutcomes[PhaseIndexKey] = index;
        }

        public int GetTotalPhases()
        {
            EnsureInitialized();
            return AccumulatedOutcomes.TryGetValue(TotalPhasesKey, out var v) ? v : 0;
        }

        public void SetTotalPhases(int count)
        {
            EnsureInitialized();
            AccumulatedOutcomes[TotalPhasesKey] = count;
        }

        public int GetScore()
        {
            EnsureInitialized();
            return AccumulatedOutcomes.TryGetValue(ScoreKey, out var v) ? v : 0;
        }

        public void AddScore(int delta)
        {
            EnsureInitialized();
            AccumulatedOutcomes.TryGetValue(ScoreKey, out var cur);
            AccumulatedOutcomes[ScoreKey] = cur + delta;
        }

        public IList<Hero> GetAssignedCompanions()
        {
            EnsureInitialized();
            var list = new List<Hero>(AssignedCompanionIds.Count);
            foreach (var guid in AssignedCompanionIds)
            {
                var hero = ResolveHero(guid);
                if (hero != null)
                {
                    list.Add(hero);
                }
            }
            return list;
        }

        private static Hero ResolveHero(MBGUID guid)
        {
            try
            {
                return MBObjectManager.Instance?.GetObject(guid) as Hero;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENDEAVOR", "hero resolve failed", ex);
                return null;
            }
        }

        private void EnsureInitialized()
        {
            if (AssignedCompanionIds == null)
            {
                AssignedCompanionIds = new List<MBGUID>();
            }
            if (AccumulatedOutcomes == null)
            {
                AccumulatedOutcomes = new Dictionary<string, int>();
            }
        }
    }
}
