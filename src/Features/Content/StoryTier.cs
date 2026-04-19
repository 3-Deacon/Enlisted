namespace Enlisted.Features.Content
{
    /// <summary>
    /// Tier of a story candidate — determines how it surfaces to the player.
    /// Log: scrollable news; Pertinent: accordion entry; Headline: [NEW]-badged accordion;
    /// Modal: blocking inquiry modal.
    /// </summary>
    public enum StoryTier
    {
        Log = 0,
        Pertinent = 1,
        Headline = 2,
        Modal = 3
    }
}
