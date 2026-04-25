using System;
using System.Collections.Generic;
using Enlisted.Features.Activities;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Contracts
{
    /// <summary>
    /// Notable-issued contract running on the Activity backbone. Sibling to
    /// EndeavorActivity (player-issued) and OrderActivity (lord-issued). Source
    /// notable hero MBGUID is captured at start so completion can route relation
    /// + reward back to the issuer. Plan 5 populates phase logic + outcome routing;
    /// Plan 1 ships the registered shell.
    /// </summary>
    [Serializable]
    public sealed class ContractActivity : Activity
    {
        public const string TYPE_ID = "contract_activity";

        public string ContractId { get; set; } = string.Empty;
        public MBGUID IssuingNotableId { get; set; }
        public string SettlementId { get; set; } = string.Empty;
        public CampaignTime StartedOn { get; set; } = CampaignTime.Zero;
        public CampaignTime DeadlineOn { get; set; } = CampaignTime.Zero;
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
            if (AccumulatedOutcomes == null)
            {
                AccumulatedOutcomes = new Dictionary<string, int>();
            }
        }
    }
}
