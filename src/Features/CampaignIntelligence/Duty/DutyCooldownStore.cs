using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Persistent per-storylet last-fired timestamps for the duty emitter's
    /// cooldown logic. Cleared when enlistment ends.
    /// </summary>
    [Serializable]
    public sealed class DutyCooldownStore
    {
        public Dictionary<string, CampaignTime> LastFiredAt { get; set; }
            = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reseats null dictionaries after deserialization paths that skip the
        /// constructor (TaleWorlds IDataStore.SyncData).
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
