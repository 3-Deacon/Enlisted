namespace Enlisted.Features.Qualities
{
    /// <summary>
    /// Stored = QualityStore owns the int; persists in save.
    /// ReadThrough = the definition carries Reader/Writer delegates that proxy to native state
    /// (e.g. EnlistmentBehavior.EnlistmentXP, Hero.GetRelationWithPlayer). Not persisted.
    /// </summary>
    public enum QualityKind
    {
        Stored,
        ReadThrough
    }
}
