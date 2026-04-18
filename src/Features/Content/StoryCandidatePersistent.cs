namespace Enlisted.Features.Content
{
    /// <summary>
    /// Persistent form of a StoryCandidate. The Director stores deferred-interactive
    /// candidates (modals blocked by the pacing floor) by EventId so they can be
    /// rehydrated from EventCatalog on load. Interactive payload delegate is NOT
    /// persisted — only enough data to reconstruct the event.
    /// </summary>
    public sealed class StoryCandidatePersistent
    {
        public string SourceId { get; set; }
        public string CategoryId { get; set; }
        public StoryTier GrantedTier { get; set; }
        public float Severity { get; set; }
        public int EmittedDayNumber { get; set; }
        public int EmittedHour { get; set; }
        public string InteractiveEventId { get; set; }
        public string RenderedTitle { get; set; }
        public string RenderedBody { get; set; }
        public string StoryKey { get; set; }
    }
}
