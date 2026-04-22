using System;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// The change event the signal is projecting — mirrors Plan 1's
    /// RecentChangeFlags subset. None means the signal is ambient
    /// (not tied to a recent change).
    /// </summary>
    public enum SignalChangeType : byte
    {
        None,
        ObjectiveShift,
        NewThreat,
        SiegeTransition,
        DispersalRisk,
        ConfirmedScoutInfo,
        SupplyBreak,
        PrisonerUpdate
    }
}
