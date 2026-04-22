namespace Enlisted.Features.Content
{
    /// <summary>
    /// How old the signal's underlying information is from the player's
    /// perspective. Rendering UI may use this to add "stale" / "late"
    /// qualifiers to the signal body.
    /// </summary>
    public enum SignalRecency : byte
    {
        Immediate,
        RecentHours,
        RecentDays,
        Stale
    }
}
