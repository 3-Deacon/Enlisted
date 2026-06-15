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

        /// <summary>Last named-order completion seen by the producer-side gate.</summary>
        public CampaignTime LastNamedOrderCompletedAt { get; set; } = CampaignTime.Zero;

        /// <summary>Storylet id for the last completed named order.</summary>
        public string LastNamedOrderCompletedId { get; set; } = string.Empty;

        /// <summary>Accepted intent for the last completed named order.</summary>
        public string LastNamedOrderCompletedIntent { get; set; } = string.Empty;

        public void RecordNamedOrderCompletion(string orderId, string intent, CampaignTime completedAt)
        {
            LastNamedOrderCompletedAt = completedAt;
            LastNamedOrderCompletedId = orderId ?? string.Empty;
            LastNamedOrderCompletedIntent = intent ?? string.Empty;
        }

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
