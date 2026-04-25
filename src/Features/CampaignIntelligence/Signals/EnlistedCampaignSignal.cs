using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// A projected signal produced by EnlistedCampaignSignalBuilder.
    /// Transient — not persisted. The emitter consumes it, picks a matching
    /// floor-storylet from the pool, and forwards rendering through
    /// StoryDirector.EmitCandidate at StoryTier.Log.
    /// </summary>
    public sealed class EnlistedCampaignSignal
    {
        public SignalType Type { get; set; }
        public SignalConfidence Confidence { get; set; } = SignalConfidence.Medium;
        public SignalChangeType ChangeType { get; set; } = SignalChangeType.None;

        /// <summary>
        /// Short string identifying which authored floor-storylet family this
        /// signal maps to. For most families this is `floor_&lt;familyName&gt;_`
        /// and the emitter greps StoryletCatalog by that prefix.
        /// </summary>
        public string PoolPrefix { get; set; } = string.Empty;

        /// <summary>
        /// CampaignTime of the change event that triggered the signal, or
        /// CampaignTime.Now for ambient signals with no specific triggering event.
        /// Used for recency classification.
        /// </summary>
        public CampaignTime AsOf { get; set; } = CampaignTime.Zero;

        /// <summary>
        /// Optional — if set, overrides the default per-family source perspective.
        /// </summary>
        public string SourcePerspectiveOverride { get; set; }

        /// <summary>
        /// Optional — if set, a short (≤80 char) inference hint authored on the
        /// signal itself rather than on the picked storylet.
        /// </summary>
        public string InferenceOverride { get; set; }
    }
}
