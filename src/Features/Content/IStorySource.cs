namespace Enlisted.Features.Content
{
    /// <summary>
    /// A story-source emits StoryCandidates into the StoryDirector.
    /// Currently requires only a stable SourceId; future versions may add
    /// payload rehydration hooks for deferred interactive candidates.
    /// </summary>
    public interface IStorySource
    {
        string SourceId { get; }
    }
}
