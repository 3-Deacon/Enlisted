using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Activities
{
    /// <summary>Passed into Activity hooks (OnStart/Tick/OnPhaseEnter/OnPhaseExit).</summary>
    public sealed class ActivityContext
    {
        public string TypeId { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public List<Hero> Attendees { get; set; } = new List<Hero>();
        public CampaignTime EnteredAt { get; set; } = CampaignTime.Now;
    }
}
