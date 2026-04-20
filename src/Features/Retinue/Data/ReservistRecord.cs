using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Retinue.Data
{
    /// <summary>
    /// Snapshot of enlistment state when discharged, for re-entry offers.
    /// </summary>
    [Serializable]
    public class ReservistRecord
    {
        public string LastLordId { get; set; }
        public string LastFactionId { get; set; }
        public int DaysServed { get; set; }
        public int TierAtExit { get; set; }
        public int XpAtExit { get; set; }
        public string DischargeBand { get; set; }
        public float RelationAtExit { get; set; }
        public CampaignTime RecordedAt { get; set; }
        public bool Consumed { get; set; }

        // Internal flag indicating the record granted probation instead of a direct tier boost.
        public bool GrantedProbation { get; set; }
    }
}
