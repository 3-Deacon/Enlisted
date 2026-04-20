namespace Enlisted.Features.Activities
{
    /// <summary>
    /// Auto: ActivityRuntime picks one weighted storylet from the phase pool per tick.
    /// PlayerChoice: the phase renders N eligible storylets as menu options; player picks one.
    /// </summary>
    public enum PhaseDelivery
    {
        Auto,
        PlayerChoice
    }
}
