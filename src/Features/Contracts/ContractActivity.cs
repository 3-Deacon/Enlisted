using System;
using System.Collections.Generic;
using Enlisted.Features.Activities;
using Enlisted.Features.Retinue.Core;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Contracts
{
    /// <summary>
    /// Notable-issued contract on the Activity backbone. Sibling to EndeavorActivity
    /// (player-issued) and OrderActivity (lord-issued). The issuing notable's MBGUID
    /// + the settlement where the contract was taken are captured at start so
    /// completion can route relation + reward back to the right hero. Phase
    /// advancement is driven by EndeavorRunner (the contract-archetype loader feeds
    /// it the same way endeavor templates do); the activity_contract.json type
    /// definition declares a single long-duration "running" phase to keep
    /// ActivityRuntime quiet. Phase index + total + score live in AccumulatedOutcomes
    /// under reserved __-prefixed keys; choice memory rides on FlagStore keys
    /// `contract_choice_&lt;contract_id&gt;_&lt;phase&gt;_&lt;option_id&gt;`.
    /// </summary>
    [Serializable]
    public sealed class ContractActivity : Activity
    {
        public const string TYPE_ID = "contract_activity";

        public const string PhaseIndexKey = "__phase_index__";
        public const string TotalPhasesKey = "__total_phases__";
        public const string ScoreKey = "__score__";

        public string ContractId { get; set; } = string.Empty;
        public MBGUID IssuingNotableId { get; set; }
        public string SettlementId { get; set; } = string.Empty;
        public List<MBGUID> AssignedCompanionIds { get; set; } = new List<MBGUID>();
        public CampaignTime StartedOn { get; set; } = CampaignTime.Zero;
        public CampaignTime DeadlineOn { get; set; } = CampaignTime.Zero;
        public Dictionary<string, int> AccumulatedOutcomes { get; set; } = new Dictionary<string, int>();

        public static ContractActivity Instance =>
            ActivityRuntime.Instance?.FindActive<ContractActivity>();

        public override void OnStart(ActivityContext ctx)
        {
            TypeId = TYPE_ID;
            EnsureInitialized();
            StartedOn = CampaignTime.Now;

            var manager = CompanionAssignmentManager.Instance;
            if (manager == null)
            {
                ModLogger.Expected("CONTRACT", "no_assignment_manager_at_start",
                    "CompanionAssignmentManager null at contract start; skipping companion lock");
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
                ModLogger.Expected("CONTRACT", "no_assignment_manager_at_finish",
                    "CompanionAssignmentManager null at contract finish; companion locks may persist");
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

        public Hero GetIssuingNotable()
        {
            return ResolveHero(IssuingNotableId);
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

        public bool IsPastDeadline()
        {
            return DeadlineOn != CampaignTime.Zero && CampaignTime.Now > DeadlineOn;
        }

        private static Hero ResolveHero(MBGUID guid)
        {
            try
            {
                return MBObjectManager.Instance?.GetObject(guid) as Hero;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CONTRACT", "hero resolve failed", ex);
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
