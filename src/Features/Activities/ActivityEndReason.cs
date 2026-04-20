namespace Enlisted.Features.Activities
{
    /// <summary>Why an Activity.Finish was called.</summary>
    public enum ActivityEndReason
    {
        Completed,
        PlayerCancelled,
        Interrupted,
        InvalidStateAfterLoad
    }
}
