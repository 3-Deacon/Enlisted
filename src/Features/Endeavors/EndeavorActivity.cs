using System;
using System.Collections.Generic;
using Enlisted.Features.Activities;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Player-issued multi-phase endeavor — soldier / rogue / medical / scouting / social.
    /// Sibling to OrderActivity (lord-issued) and ContractActivity (notable-issued); all
    /// three run on the shared Activity backbone. Plan 5 populates phase logic, agent
    /// assignment, and outcome routing; Plan 1 ships the registered shell.
    /// </summary>
    [Serializable]
    public sealed class EndeavorActivity : Activity
    {
        public const string TYPE_ID = "endeavor_activity";

        public string EndeavorId { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public List<MBGUID> AssignedCompanionIds { get; set; } = new List<MBGUID>();
        public CampaignTime StartedOn { get; set; } = CampaignTime.Zero;
        public Dictionary<string, int> AccumulatedOutcomes { get; set; } = new Dictionary<string, int>();

        public override void OnStart(ActivityContext ctx)
        {
            TypeId = TYPE_ID;
            EnsureInitialized();
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
