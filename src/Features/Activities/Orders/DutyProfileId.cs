namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// The seven duty profiles OrderActivity can be in. Six are auto-resolved
    /// from party state by DutyProfileSelector; Imprisoned is only entered
    /// when an external signal (lord captured) forces it via DutyProfileBehavior.
    /// </summary>
    public enum DutyProfileId
    {
        Wandering  = 0,
        Marching   = 1,
        Garrisoned = 2,
        Escorting  = 3,
        Raiding    = 4,
        Besieging  = 5,
        Imprisoned = 6
    }

    /// <summary>
    /// String ids matching DutyProfileId values, used by storylet JSON
    /// profile_requires gates and authored content references.
    /// </summary>
    public static class DutyProfileIds
    {
        public const string Wandering  = "wandering";
        public const string Marching   = "marching";
        public const string Garrisoned = "garrisoned";
        public const string Escorting  = "escorting";
        public const string Raiding    = "raiding";
        public const string Besieging  = "besieging";
        public const string Imprisoned = "imprisoned";
    }
}
