using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Per-evaluation bag handed into IsEligible / WeightFor / ToCandidate. Populated by
    /// ActivityRuntime or beat handlers before invoking the storylet.
    /// </summary>
    public sealed class StoryletContext
    {
        public string CurrentContext { get; set; } = "any";       // runtime-derived: land | sea | siege | settlement
        public Hero SubjectHero { get; set; }
        public Dictionary<string, Hero> ResolvedSlots { get; set; } = new Dictionary<string, Hero>();
        public string ActivityTypeId { get; set; } = string.Empty;
        public string PhaseId { get; set; } = string.Empty;
        public CampaignTime EvaluatedAt { get; set; } = CampaignTime.Now;

        /// <summary>The storylet whose effects are being applied. Populated by the caller that iterates option effects (StoryletEventAdapter). Null for non-storylet effect applications.</summary>
        public Storylet SourceStorylet { get; set; }
    }
}
