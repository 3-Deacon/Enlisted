namespace Enlisted.Features.Logistics
{
    /// <summary>
    /// Represents the current accessibility state of the baggage train.
    /// The baggage train marches separately from the fighting column, creating
    /// natural access windows during camp, settlement stays, and muster events.
    /// </summary>
    public enum BaggageAccessState
    {
        /// <summary>
        /// Full, unrestricted access to baggage stash.
        /// Typical when in settlement, during muster, or army encamped.
        /// </summary>
        FullAccess,

        /// <summary>
        /// Brief access window as baggage train catches up to the column.
        /// Auto-expires after configured duration (typically 2-4 hours).
        /// </summary>
        TemporaryAccess,

        /// <summary>
        /// No access - baggage train is behind the column during march.
        /// Normal state when traveling on campaign map.
        /// </summary>
        NoAccess,

        /// <summary>
        /// Locked by quartermaster authority.
        /// Occurs during supply crisis (supply less than 20%).
        /// </summary>
        Locked
    }
}

