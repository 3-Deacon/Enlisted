using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Persistent per-storylet last-fired timestamps for the duty emitter's
    /// cooldown logic. Cleared when enlistment ends.
    /// </summary>
    [Serializable]
    public sealed class DutyCooldownStore
    {
        [SaveableProperty(1)] public Dictionary<string, CampaignTime> LastFiredAt { get; set; }
            = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Last named-order completion seen by the producer-side gate.</summary>
        [SaveableProperty(2)] public CampaignTime LastNamedOrderCompletedAt { get; set; } = CampaignTime.Zero;

        /// <summary>Storylet id for the last completed named order.</summary>
        [SaveableProperty(3)] public string LastNamedOrderCompletedId { get; set; } = string.Empty;

        /// <summary>Accepted intent for the last completed named order.</summary>
        [SaveableProperty(4)] public string LastNamedOrderCompletedIntent { get; set; } = string.Empty;

        /// <summary>Last named-order accept modal emitted by the producer-side gate.</summary>
        [SaveableProperty(5)] public CampaignTime LastNamedOrderEmittedAt { get; set; } = CampaignTime.Zero;

        /// <summary>Storylet id for the last emitted named-order accept modal.</summary>
        [SaveableProperty(6)] public string LastNamedOrderEmittedId { get; set; } = string.Empty;

        /// <summary>Duty profile used for the last emitted named-order accept modal.</summary>
        [SaveableProperty(7)] public string LastNamedOrderEmittedProfile { get; set; } = string.Empty;

        public void RecordNamedOrderCompletion(string orderId, string intent, CampaignTime completedAt)
        {
            LastNamedOrderCompletedAt = completedAt;
            LastNamedOrderCompletedId = orderId ?? string.Empty;
            LastNamedOrderCompletedIntent = intent ?? string.Empty;
        }

        public void RecordNamedOrderEmission(string orderId, string profile, CampaignTime emittedAt)
        {
            LastNamedOrderEmittedAt = emittedAt;
            LastNamedOrderEmittedId = orderId ?? string.Empty;
            LastNamedOrderEmittedProfile = profile ?? string.Empty;
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
