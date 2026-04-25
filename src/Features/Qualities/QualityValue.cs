using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Qualities
{
    /// <summary>Per-(quality,owner) value + decay bookkeeping. Saveable.</summary>
    [Serializable]
    public sealed class QualityValue
    {
        public int Current { get; set; }
        public CampaignTime LastChanged { get; set; } = CampaignTime.Zero;
        public CampaignTime LastDecayed { get; set; } = CampaignTime.Zero;
    }
}
