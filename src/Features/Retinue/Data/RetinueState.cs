using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Retinue.Data
{
    /// <summary>
    /// Defines loyalty threshold levels that trigger automatic events.
    /// Used to track which threshold was last crossed to prevent duplicate triggers.
    /// </summary>
    public enum LoyaltyThreshold
    {
        None = 0,      // No threshold crossed yet
        Low = 30,      // Loyalty below 30: warning event
        Critical = 20, // Loyalty below 20: desertion risk event
        Mutiny = 10,   // Loyalty below 10: crisis event
        High = 80      // Loyalty above 80: positive recognition event
    }

    /// <summary>
    /// Represents the outcome of a battle for more granular trickle rate handling (EC5).
    /// </summary>
    public enum BattleOutcome
    {
        Unknown,    // No battle tracked yet
        Victory,    // Player's side won decisively
        Defeat,     // Player's side lost decisively
        Withdrawal, // Battle ended without clear winner, casualties taken
        Draw        // Battle ended without clear winner, minimal/no casualties
    }

    /// <summary>
    /// Tracks the player's personal retinue state including soldier type, counts, and replenishment tracking.
    /// This is the single source of truth for all retinue data.
    /// </summary>
    [Serializable]
    public class RetinueState
    {
        /// <summary>
        /// The soldier type the player selected ("infantry", "archers", "cavalry", "horse_archers").
        /// Null or empty if no type selected yet.
        /// </summary>
        public string SelectedTypeId { get; set; }

        /// <summary>
        /// Tracks which specific CharacterObjects we added and how many.
        /// Key = CharacterObject.StringId, Value = count.
        /// This allows us to distinguish retinue soldiers from other troops in the party.
        /// </summary>
        public Dictionary<string, int> TroopCounts { get; set; }

        /// <summary>
        /// Days elapsed since last free trickle replenishment.
        /// Resets to 0 when a soldier is added via trickle.
        /// </summary>
        public int DaysSinceLastTrickle { get; set; }

        /// <summary>
        /// Campaign time when instant requisition cooldown ends.
        /// Player can requisition again when CampaignTime.Now >= this value.
        /// </summary>
        public CampaignTime RequisitionCooldownEnd { get; set; }

        /// <summary>
        /// Campaign time of last battle the player participated in.
        /// Used to determine post-battle trickle rate bonuses or penalties.
        /// </summary>
        public CampaignTime LastBattleTime { get; set; }

        /// <summary>
        /// Whether the player's side won the last battle.
        /// Victory grants bonus trickle rate, defeat blocks replenishment temporarily.
        /// Kept for backwards compatibility; use LastBattleOutcome for granular handling.
        /// </summary>
        public bool LastBattleWon { get; set; }

        /// <summary>
        /// Granular battle outcome for EC5 edge case handling.
        /// Withdrawal: no bonus, no penalty (neutral rate).
        /// Draw: slight bonus (1 per 3 days).
        /// </summary>
        public BattleOutcome LastBattleOutcome { get; set; } = BattleOutcome.Unknown;

        /// <summary>
        /// Campaign time when reinforcement request cooldown ends.
        /// Shorter cooldown (7 days) for high-relation lords, longer (14 days) for neutral.
        /// </summary>
        public CampaignTime ReinforcementRequestCooldownEnd { get; set; }

        /// <summary>
        /// Loyalty level of the retinue (0-100 scale).
        /// Represents the Commander's relationship with their personal soldiers.
        /// Starts at 50 (neutral). Low loyalty can trigger desertion events.
        /// High loyalty provides combat bonuses and morale stability.
        /// </summary>
        public int RetinueLoyalty { get; set; }

        /// <summary>
        /// The last loyalty threshold that was crossed and triggered an event.
        /// Prevents duplicate events when loyalty oscillates around a threshold.
        /// </summary>
        public LoyaltyThreshold LastLoyaltyThresholdCrossed { get; set; }

        /// <summary>
        /// Campaign time when the last threshold event was triggered.
        /// Used to enforce cooldown between threshold events (7 days minimum).
        /// </summary>
        public CampaignTime LastThresholdEventTime { get; set; }

        /// <summary>
        /// Named veterans who have emerged from the ranks. These soldiers have names, traits,
        /// and tracked history. Their deaths trigger memorial events. Maximum 5 veterans at a time.
        /// </summary>
        public List<NamedVeteran> NamedVeterans { get; set; }

        /// <summary>
        /// Maximum number of named veterans that can exist at once.
        /// Keeps the system manageable and each veteran feeling special.
        /// </summary>
        public const int MaxNamedVeterans = 5;

        /// <summary>
        /// Number of battles the retinue has participated in since formation.
        /// Used to track when anonymous soldiers become eligible to emerge as named veterans.
        /// </summary>
        public int BattlesParticipated { get; set; }

        /// <summary>
        /// Total number of soldiers currently in the retinue.
        /// Computed from TroopCounts dictionary.
        /// </summary>
        public int TotalSoldiers => TroopCounts?.Values.Sum() ?? 0;

        /// <summary>
        /// Returns true if the player has an active retinue (type selected and soldiers present).
        /// </summary>
        public bool HasRetinue => !string.IsNullOrEmpty(SelectedTypeId) && TotalSoldiers > 0;

        /// <summary>
        /// Returns true if a soldier type has been selected, even if no soldiers are present.
        /// Used to determine if trickle replenishment should occur.
        /// </summary>
        public bool HasTypeSelected => !string.IsNullOrEmpty(SelectedTypeId);

        public RetinueState()
        {
            TroopCounts = new Dictionary<string, int>();
            RequisitionCooldownEnd = CampaignTime.Zero;
            LastBattleTime = CampaignTime.Zero;
            LastBattleWon = false;
            ReinforcementRequestCooldownEnd = CampaignTime.Zero;
            RetinueLoyalty = 50; // Start neutral
            LastLoyaltyThresholdCrossed = LoyaltyThreshold.None;
            LastThresholdEventTime = CampaignTime.Zero;
            NamedVeterans = new List<NamedVeteran>();
            BattlesParticipated = 0;
        }

        /// <summary>
        /// Clears all retinue state. Called on capture, enlistment end, army defeat, or type change.
        /// Named veterans are lost when the retinue is disbanded.
        /// </summary>
        public void Clear()
        {
            TroopCounts?.Clear();
            SelectedTypeId = null;
            DaysSinceLastTrickle = 0;
            NamedVeterans?.Clear();
            BattlesParticipated = 0;
            // Note: RequisitionCooldownEnd is preserved across clears to prevent exploit
        }

        /// <summary>
        /// Updates troop count for a specific character. Removes entry if count reaches zero.
        /// </summary>
        /// <param name="characterId">The CharacterObject.StringId</param>
        /// <param name="delta">Amount to add (positive) or remove (negative)</param>
        /// <returns>New count for this character type</returns>
        public int UpdateTroopCount(string characterId, int delta)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                return 0;
            }

            TroopCounts ??= new Dictionary<string, int>();

            if (!TroopCounts.TryGetValue(characterId, out var current))
            {
                current = 0;
            }

            var newCount = Math.Max(0, current + delta);

            if (newCount > 0)
            {
                TroopCounts[characterId] = newCount;
            }
            else
            {
                _ = TroopCounts.Remove(characterId);
            }

            return newCount;
        }

        /// <summary>
        /// Gets the count of a specific troop type in the retinue.
        /// </summary>
        public int GetTroopCount(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || TroopCounts == null)
            {
                return 0;
            }

            return TroopCounts.TryGetValue(characterId, out var count) ? count : 0;
        }

        /// <summary>
        /// Checks if the requisition cooldown has elapsed.
        /// </summary>
        public bool IsRequisitionAvailable()
        {
            return RequisitionCooldownEnd.IsPast || RequisitionCooldownEnd == CampaignTime.Zero;
        }

        /// <summary>
        /// Gets remaining days until requisition is available.
        /// </summary>
        public int GetRequisitionCooldownDays()
        {
            if (IsRequisitionAvailable())
            {
                return 0;
            }

            var remaining = RequisitionCooldownEnd.RemainingDaysFromNow;
            return (int)Math.Ceiling(remaining);
        }

        /// <summary>
        /// Checks if the reinforcement request cooldown has elapsed.
        /// </summary>
        public bool IsReinforcementRequestAvailable()
        {
            return ReinforcementRequestCooldownEnd.IsPast || ReinforcementRequestCooldownEnd == CampaignTime.Zero;
        }

        /// <summary>
        /// Gets remaining days until reinforcement request is available.
        /// </summary>
        public int GetReinforcementRequestCooldownDays()
        {
            if (IsReinforcementRequestAvailable())
            {
                return 0;
            }

            var remaining = ReinforcementRequestCooldownEnd.RemainingDaysFromNow;
            return (int)Math.Ceiling(remaining);
        }

        /// <summary>
        /// Gets the number of days since the last battle.
        /// Returns -1 if no battle has been tracked.
        /// </summary>
        public double GetDaysSinceLastBattle()
        {
            if (LastBattleTime == CampaignTime.Zero)
            {
                return -1;
            }

            return (CampaignTime.Now - LastBattleTime).ToDays;
        }

        public override string ToString()
        {
            return $"Retinue[Type={SelectedTypeId ?? "none"}, Soldiers={TotalSoldiers}, " +
                   $"TrickleDays={DaysSinceLastTrickle}, ReqCooldown={GetRequisitionCooldownDays()}d, " +
                   $"Veterans={NamedVeterans?.Count ?? 0}]";
        }

        #region Named Veteran Management

        /// <summary>
        /// Returns true if there is room for another named veteran in the retinue.
        /// </summary>
        public bool CanAddNamedVeteran()
        {
            return (NamedVeterans?.Count ?? 0) < MaxNamedVeterans;
        }

        /// <summary>
        /// Adds a named veteran to the retinue. Returns false if at capacity.
        /// </summary>
        /// <param name="veteran">The veteran to add</param>
        /// <returns>True if added successfully, false if at capacity</returns>
        public bool AddNamedVeteran(NamedVeteran veteran)
        {
            if (veteran == null || !CanAddNamedVeteran())
            {
                return false;
            }

            NamedVeterans ??= new List<NamedVeteran>();
            NamedVeterans.Add(veteran);
            return true;
        }

        /// <summary>
        /// Removes a named veteran by their unique ID.
        /// </summary>
        /// <param name="veteranId">The veteran's unique ID</param>
        /// <returns>The removed veteran, or null if not found</returns>
        public NamedVeteran RemoveNamedVeteran(string veteranId)
        {
            if (string.IsNullOrEmpty(veteranId) || NamedVeterans == null)
            {
                return null;
            }

            var veteran = NamedVeterans.FirstOrDefault(v => v.Id == veteranId);
            if (veteran != null)
            {
                _ = NamedVeterans.Remove(veteran);
            }

            return veteran;
        }

        /// <summary>
        /// Finds a named veteran by their name (for death detection).
        /// </summary>
        /// <param name="name">The veteran's name</param>
        /// <returns>The veteran with that name, or null if not found</returns>
        public NamedVeteran GetVeteranByName(string name)
        {
            if (string.IsNullOrEmpty(name) || NamedVeterans == null)
            {
                return null;
            }

            return NamedVeterans.FirstOrDefault(v =>
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Marks all non-wounded veterans as having survived another battle.
        /// </summary>
        public void RecordBattleSurvivalForAllVeterans()
        {
            if (NamedVeterans == null)
            {
                return;
            }

            foreach (var veteran in NamedVeterans.Where(v => !v.IsWounded))
            {
                veteran.RecordBattleSurvival();
            }
        }

        #endregion
    }
}

