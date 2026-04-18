using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// In-memory candidate a story-source emits to the StoryDirector.
    /// Carries EITHER an InteractiveEvent (full EventDefinition with options/effects)
    /// OR an observational render (title + body only). The Director branches on that:
    /// interactive candidates fire as modals via EventDeliveryManager; observational
    /// candidates land in the news feed as DispatchItem entries.
    /// Not persisted directly — see StoryCandidatePersistent for the save form.
    /// </summary>
    public sealed class StoryCandidate
    {
        public string SourceId { get; set; }
        public string CategoryId { get; set; }
        public StoryTier ProposedTier { get; set; } = StoryTier.Log;
        public float SeverityHint { get; set; }
        public HashSet<StoryBeat> Beats { get; set; } = new HashSet<StoryBeat>();
        public RelevanceKey Relevance { get; set; }
        public CampaignTime EmittedAt { get; set; }

        // Set one of:
        public EventDefinition InteractiveEvent { get; set; }
        public bool HasObservationalRender { get; set; }

        // Observational / deferred-interactive rendering:
        public string RenderedTitle { get; set; }
        public string RenderedBody { get; set; }

        // DispatchItem compatibility when routed through news feed:
        public string StoryKey { get; set; }
        public string DispatchCategory { get; set; }
        public int SeverityLevel { get; set; }
        public int MinDisplayDays { get; set; } = 1;
    }
}
