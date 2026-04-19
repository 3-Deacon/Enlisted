using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Retinue.Data
{
    /// <summary>
    /// Per-faction service history for the player. Each faction maintains separate records.
    /// </summary>
    [Serializable]
    public class FactionServiceRecord
    {
        /// <summary>Unique key: "kingdom_{StringId}", "minor_{StringId}", "merc_{StringId}", or "clan_{StringId}".</summary>
        public string FactionId { get; set; }

        /// <summary>Category: "kingdom", "minor", "merc", "clan", or "unknown".</summary>
        public string FactionType { get; set; }

        /// <summary>Display name for UI (e.g., "Kingdom of Vlandia").</summary>
        public string FactionDisplayName { get; set; }

        /// <summary>Full contract terms completed (not early discharge/desertion).</summary>
        public int TermsCompleted { get; set; }

        /// <summary>Total days served across all enlistments with this faction.</summary>
        public int TotalDaysServed { get; set; }

        /// <summary>Peak tier (1-6) achieved with this faction.</summary>
        public int HighestTier { get; set; }

        /// <summary>Battles fought while serving this faction.</summary>
        public int BattlesFought { get; set; }

        /// <summary>Unique lords served under in this faction.</summary>
        public int LordsServed { get; set; }

        /// <summary>Times enlisted with this faction.</summary>
        public int Enlistments { get; set; }

        /// <summary>Enemies killed while serving this faction.</summary>
        public int TotalKills { get; set; }

        // Re-enlistment control fields (used by discharge system)

        /// <summary>Campaign time when re-enlistment block expires. Zero means no block.</summary>
        public CampaignTime ReenlistmentBlockedUntil { get; set; } = CampaignTime.Zero;

        /// <summary>Band of the most recent discharge: "veteran", "honorable", "washout", "dishonorable", "deserter", "grace".</summary>
        public string LastDischargeBand { get; set; } = string.Empty;

        // Term tracking fields (migrated from FactionVeteranRecord)

        /// <summary>Whether the player has completed the initial full term with this faction.</summary>
        public bool FirstTermCompleted { get; set; }

        /// <summary>Preserved military tier from last service. Restored on re-enlistment after cooldown.</summary>
        public int PreservedTier { get; set; } = 1;

        /// <summary>Campaign time when the 6-month cooldown period ends after honorable discharge.</summary>
        public CampaignTime CooldownEnds { get; set; } = CampaignTime.Zero;

        /// <summary>Campaign time when the current service term ends.</summary>
        public CampaignTime CurrentTermEnd { get; set; } = CampaignTime.Zero;

        /// <summary>Whether the player is currently in a renewal term (post-first-term service).</summary>
        public bool IsInRenewalTerm { get; set; }

        /// <summary>Number of completed renewal terms after the first full term.</summary>
        public int RenewalTermsCompleted { get; set; }

        public FactionServiceRecord()
        {
            FactionId = string.Empty;
            FactionType = "unknown";
            FactionDisplayName = string.Empty;
        }

        public FactionServiceRecord(string factionId, string factionType, string displayName)
        {
            FactionId = factionId ?? string.Empty;
            FactionType = factionType ?? "unknown";
            FactionDisplayName = displayName ?? string.Empty;
        }

        /// <summary>Updates highest tier if <paramref name="currentTier"/> exceeds current record.</summary>
        public void UpdateHighestTier(int currentTier)
        {
            if (currentTier > HighestTier)
            {
                HighestTier = currentTier;
            }
        }

        public override string ToString()
        {
            return $"[{FactionId}] Terms:{TermsCompleted} Days:{TotalDaysServed} " +
                   $"MaxTier:{HighestTier} Battles:{BattlesFought} Kills:{TotalKills}";
        }
    }
}
