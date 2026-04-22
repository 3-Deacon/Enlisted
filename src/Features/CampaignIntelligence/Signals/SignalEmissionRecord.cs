using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// Persistent per-storylet cooldown state for the signal emitter. Maps
    /// storylet id → last-fired CampaignTime. Cleared when enlistment ends
    /// (the emitter clears its instance on OnEnlistmentEnded).
    /// </summary>
    [Serializable]
    public sealed class SignalEmissionRecord
    {
        public Dictionary<string, CampaignTime> LastFiredAt { get; set; }
            = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reseats null dictionary after deserialization paths that skip ctors.
        /// See CLAUDE.md pitfall on [Serializable] + SyncData null-dict crashes.
        /// </summary>
        public void EnsureInitialized()
        {
            if (LastFiredAt == null)
            {
                LastFiredAt = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
