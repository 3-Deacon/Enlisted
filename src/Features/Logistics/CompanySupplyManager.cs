using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Enlisted.Features.Logistics
{
    /// <summary>
    /// Manages company supply calculation based on rations and equipment logistics.
    /// Tracks supply consumption and resupply for ammunition, repair materials, rations, and camp supplies.
    /// Player receives rations directly from their service rather than sharing from the lord's food supply.
    /// </summary>
    public class CompanySupplyManager
    {
        private const string LogCategory = "Supply";

        /// <summary>
        /// Singleton instance for global access. Set when EnlistmentBehavior initializes.
        /// </summary>
        public static CompanySupplyManager Instance { get; private set; }

        /// <summary>
        /// The total supply level (0-100%). Includes rations, ammunition, repair materials, medical supplies, and camp gear.
        /// </summary>
        private float _totalSupply = 100.0f;

        /// <summary>
        /// The enlisted lord whose party we track for context. Used for party size and activity calculations.
        /// </summary>
        private Hero _enlistedLord;

        /// <summary>
        /// Supply level when lord entered a settlement. Used to calculate gains for departure message.
        /// </summary>
        private float _supplyOnSettlementEntry;

        /// <summary>
        /// The settlement the lord last entered. Used for resupply departure message.
        /// </summary>
        private Settlement _currentResupplySettlement;

        /// <summary>
        /// Total supply percentage (0-100). Gates equipment access and reflects overall logistics state.
        /// </summary>
        public int TotalSupply
        {
            get
            {
                return (int)MathF.Clamp(_totalSupply, 0f, 100f);
            }
        }

        /// <summary>
        /// Current supply level (0-100). Exposed for save/load and debugging.
        /// </summary>
        public float NonFoodSupply => _totalSupply;

        /// <summary>
        /// Initializes the supply manager singleton and sets the enlisted lord.
        /// Called when player enlists with a lord.
        /// </summary>
        /// <param name="enlistedLord">The lord the player is enlisting with.</param>
        /// <param name="preserveSupply">If true, preserves existing supply level (for grace period re-enlistment).</param>
        public static void Initialize(Hero enlistedLord, bool preserveSupply = false)
        {
            float existingSupply = Instance?._totalSupply ?? 100.0f;

            Instance = new CompanySupplyManager
            {
                _enlistedLord = enlistedLord,
                _totalSupply = preserveSupply ? MathF.Clamp(existingSupply, 0f, 100f) : 100.0f
            };

            ModLogger.Info(LogCategory, preserveSupply
                ? $"CompanySupplyManager transferred to lord: {enlistedLord?.Name}, preserved supply: {Instance._totalSupply:F1}%"
                : $"CompanySupplyManager initialized for lord: {enlistedLord?.Name}");
        }

        /// <summary>
        /// Clears the singleton when player leaves service.
        /// </summary>
        public static void Shutdown()
        {
            if (Instance != null)
            {
                ModLogger.Info(LogCategory, "CompanySupplyManager shutdown");
                Instance = null;
            }
        }

        /// <summary>
        /// Restores supply manager state after a save load.
        /// </summary>
        public static void RestoreFromSave(Hero enlistedLord, float totalSupply)
        {
            Instance = new CompanySupplyManager
            {
                _enlistedLord = enlistedLord,
                _totalSupply = MathF.Clamp(totalSupply, 0f, 100f)
            };
            ModLogger.Info(LogCategory, $"CompanySupplyManager restored: Supply={totalSupply:F1}%, Lord={enlistedLord?.Name}");
        }


        /// <summary>
        /// Processes daily supply updates: consumption and resupply.
        /// Called from EnlistmentBehavior's daily tick while enlisted.
        /// </summary>
        public void DailyUpdate()
        {
            try
            {
                float oldSupply = _totalSupply;

                float consumption = CalculateSupplyConsumption();
                float resupply = CalculateSupplyResupply();

                _totalSupply = MathF.Clamp(_totalSupply - consumption + resupply, 0f, 100f);

                // Log significant changes
                float supplyChange = _totalSupply - oldSupply;
                int totalSupply = TotalSupply;

                // Always log when critically low to help diagnose stuck supply issues
                if (totalSupply < 15)
                {
                    ModLogger.Info(LogCategory,
                        $"Supply critically low: {oldSupply:F1}% -> {_totalSupply:F1}% (consumption={consumption:F2}, resupply={resupply:F2})");
                }
                else if (MathF.Abs(supplyChange) > 0.5f)
                {
                    ModLogger.Debug(LogCategory,
                        $"Daily supply update: {oldSupply:F1}% -> {_totalSupply:F1}% ({supplyChange:+0.0;-0.0}), Total={totalSupply}%");
                }

                // Log warnings at thresholds
                if (totalSupply < 30 && oldSupply >= 30)
                {
                    ModLogger.Warn(LogCategory, $"Supply CRITICAL: {totalSupply}% - equipment access blocked");
                }
                else if (totalSupply < 50 && oldSupply >= 50)
                {
                    ModLogger.Info(LogCategory, $"Supply LOW: {totalSupply}% - resupply recommended");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Supply", "Error in DailyUpdate", ex);
            }
        }

        /// <summary>
        /// Processes hourly supply resupply when in settlements.
        /// Called from EnlistmentBehavior's hourly tick while enlisted.
        /// This allows partial-day settlement visits to provide meaningful supply gains.
        /// Consumption remains daily (not hourly) to avoid excessive drain.
        /// </summary>
        public void HourlyUpdate()
        {
            try
            {
                float oldSupply = _totalSupply;

                // Only process resupply hourly; consumption stays daily to avoid over-draining
                float hourlyResupply = CalculateHourlyResupply();

                if (hourlyResupply > 0f)
                {
                    _totalSupply = MathF.Clamp(_totalSupply + hourlyResupply, 0f, 100f);

                    float supplyChange = _totalSupply - oldSupply;
                    int totalSupply = TotalSupply;

                    // Log hourly gains when they occur
                    if (supplyChange > 0.1f)
                    {
                        ModLogger.Debug(LogCategory,
                            $"Hourly supply gain: {oldSupply:F1}% -> {_totalSupply:F1}% (+{supplyChange:F2}%), Total={totalSupply}%");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Supply", "Error in HourlyUpdate", ex);
            }
        }

        /// <summary>
        /// Called when the lord enters a settlement. Tracks supply level for departure comparison.
        /// </summary>
        public void OnSettlementEntered(Settlement settlement)
        {
            if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
            {
                return;
            }

            _supplyOnSettlementEntry = _totalSupply;
            _currentResupplySettlement = settlement;

            ModLogger.Debug(LogCategory, $"Lord entered {settlement.Name} - tracking supply at {_totalSupply:F1}%");
        }

        /// <summary>
        /// Called when the lord leaves a settlement. Displays flavor message about resupply if meaningful gain occurred.
        /// </summary>
        public void OnSettlementLeft(Settlement settlement)
        {
            try
            {
                // Only show message for the settlement we were tracking
                if (settlement == null || _currentResupplySettlement != settlement)
                {
                    return;
                }

                float supplyGained = _totalSupply - _supplyOnSettlementEntry;

                // Only show message if meaningful supply was gained (at least 5%)
                if (supplyGained >= 5f)
                {
                    string message = GetResupplyDepartureMessage(settlement, supplyGained);
                    if (!string.IsNullOrEmpty(message))
                    {
                        InformationManager.DisplayMessage(new InformationMessage(message, Colors.Green));
                        ModLogger.Info(LogCategory, $"Resupply message: gained {supplyGained:F1}% in {settlement.Name}");
                    }
                }
                else
                {
                    ModLogger.Debug(LogCategory, $"Left {settlement.Name} - supply gain {supplyGained:F1}% too small for message");
                }

                // Clear tracking
                _currentResupplySettlement = null;
                _supplyOnSettlementEntry = 0f;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Supply", "Error in OnSettlementLeft", ex);
            }
        }

        /// <summary>
        /// Generates a flavor message for resupply based on how much was gained.
        /// Uses RP-appropriate language without percentages.
        /// </summary>
        private string GetResupplyDepartureMessage(Settlement settlement, float supplyGained)
        {
            string settlementName = settlement?.Name?.ToString() ?? "the settlement";

            // Choose message based on how much was gained
            if (supplyGained >= 30f)
            {
                // Major resupply
                return $"The company restocked well in {settlementName}. Stores are replenished.";
            }
            else if (supplyGained >= 15f)
            {
                // Good resupply
                return $"Supplies replenished in {settlementName}.";
            }
            else if (supplyGained >= 5f)
            {
                // Minor resupply
                return $"Took on some supplies in {settlementName}.";
            }

            return null;
        }

        /// <summary>
        /// Calculates daily supply consumption based on activity and conditions.
        /// Base rate is 1.5% per day, modified by company size, activity, and terrain.
        /// </summary>
        private float CalculateSupplyConsumption()
        {
            MobileParty lordParty = GetLordParty();
            if (lordParty == null)
            {
                return 0f;
            }

            // Base consumption rate: 1.5% per day for non-food supplies
            float baseRate = 1.5f;

            // Size multiplier: scales with party size relative to 100-troop baseline
            int partySize = lordParty.Party.NumberOfAllMembers;
            float sizeMultiplier = MathF.Max(0.3f, partySize / 100f);

            // Activity multiplier based on what the party is doing
            float activityMult = GetActivityMultiplier(lordParty);

            // Terrain multiplier based on current terrain type
            float terrainMult = GetTerrainMultiplier(lordParty);

            float consumption = baseRate * sizeMultiplier * activityMult * terrainMult;

            ModLogger.Debug(LogCategory,
                $"Consumption calc: base={baseRate}, size={sizeMultiplier:F2} (troops={partySize}), " +
                $"activity={activityMult:F1}, terrain={terrainMult:F1} => {consumption:F2}%");

            return consumption;
        }

        /// <summary>
        /// Determines activity multiplier for non-food consumption.
        /// Higher multipliers for intense activities (siege, raiding).
        /// </summary>
        private float GetActivityMultiplier(MobileParty party)
        {
            if (party == null)
            {
                return 1.0f;
            }

            // Check siege states first (before general settlement check)
            // Active siege = high consumption (siege equipment, repairs)
            if (party.BesiegerCamp != null)
            {
                return 2.5f;
            }

            // In settlement - check if being besieged first
            if (party.CurrentSettlement != null)
            {
                // Being besieged = elevated consumption (defending, rationing)
                if (party.CurrentSettlement.SiegeEvent != null)
                {
                    return 1.8f;
                }

                // Normal settlement rest = minimal consumption
                return 0.3f;
            }

            // In combat = extra consumption handled by battle events, not daily tick
            if (party.Party?.MapEvent != null)
            {
                return 1.5f;
            }

            // Patrolling or raiding behavior - check short-term AI
            var behavior = party.ShortTermBehavior;
            if (behavior == AiBehavior.RaidSettlement)
            {
                return 1.5f;
            }
            if (behavior == AiBehavior.PatrolAroundPoint)
            {
                return 1.2f;
            }

            // Default traveling
            return 1.0f;
        }

        /// <summary>
        /// Determines terrain multiplier for non-food consumption.
        /// Harsh terrain (desert, mountains, snow) increases wear on equipment.
        /// </summary>
        private float GetTerrainMultiplier(MobileParty party)
        {
            if (party == null || !party.IsActive)
            {
                return 1.0f;
            }

            try
            {
                // Safety check for campaign state
                if (Campaign.Current?.MapSceneWrapper == null)
                {
                    return 1.0f;
                }

                var navFace = party.CurrentNavigationFace;
                if (!navFace.IsValid())
                {
                    return 1.0f;
                }

                TerrainType terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(navFace);

                return terrain switch
                {
                    TerrainType.Desert => 1.2f,   // Sand damages equipment
                    TerrainType.Mountain => 1.3f, // Rough terrain wear
                    TerrainType.Snow => 1.4f,     // Cold damages gear, extra fuel needed
                    TerrainType.Forest => 1.0f,   // Standard
                    TerrainType.Plain => 1.0f,    // Standard
                    TerrainType.Steppe => 1.0f,   // Standard
                    TerrainType.Fording => 1.1f,  // Water damage
                    TerrainType.Lake => 1.1f,     // Water damage
                    TerrainType.Water => 1.1f,    // Naval/water damage
                    _ => 1.0f
                };
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error getting terrain type: {ex.Message}");
                return 1.0f;
            }
        }

        /// <summary>
        /// Calculates daily resupply when in a settlement.
        /// Base rate is +50% per day in towns/castles. Lords rarely stay a full day, so hourly ticks ensure partial visits are rewarded.
        /// Bonuses for prosperity and ownership provide additional recovery.
        /// </summary>
        private float CalculateSupplyResupply()
        {
            MobileParty lordParty = GetLordParty();
            if (lordParty?.CurrentSettlement == null)
            {
                return 0f;
            }

            Settlement settlement = lordParty.CurrentSettlement;

            // Only resupply in towns and castles (not villages)
            if (!settlement.IsTown && !settlement.IsCastle)
            {
                return 0f;
            }

            // Base resupply: +50% per day (lords rarely stay 24h, so hourly ticks ensure meaningful gains)
            float resupply = 50.0f;

            // Wealthy settlement bonus: +10% if prosperity > 5000
            float prosperity = settlement.Town?.Prosperity ?? 0f;
            if (prosperity > 5000)
            {
                resupply += 10.0f;
            }

            // Owned settlement bonus: +10% if lord owns the settlement
            if (settlement.OwnerClan == lordParty.LeaderHero?.Clan)
            {
                resupply += 10.0f;
            }

            ModLogger.Debug(LogCategory,
                $"Resupply in {settlement.Name}: base=50, prosperity={prosperity:F0} ({(prosperity > 5000 ? "+10" : "0")}), " +
                $"owned={settlement.OwnerClan == lordParty.LeaderHero?.Clan} => {resupply:F1}%");

            return resupply;
        }

        /// <summary>
        /// Calculates hourly resupply when in a settlement.
        /// Base rate is ~2.08% per hour (50% / 24 hours) in towns/castles.
        /// Bonuses for prosperity and ownership provide additional recovery.
        /// This allows partial-day settlement visits to provide meaningful supply gains since lords rarely stay 24 hours.
        /// </summary>
        private float CalculateHourlyResupply()
        {
            MobileParty lordParty = GetLordParty();
            if (lordParty?.CurrentSettlement == null)
            {
                return 0f;
            }

            Settlement settlement = lordParty.CurrentSettlement;

            // Only resupply in towns and castles (not villages)
            if (!settlement.IsTown && !settlement.IsCastle)
            {
                return 0f;
            }

            // Base resupply: +50% per day = ~2.08% per hour (50/24)
            float dailyResupply = 50.0f;

            // Wealthy settlement bonus: +10% per day = ~0.42% per hour (10/24)
            float prosperity = settlement.Town?.Prosperity ?? 0f;
            if (prosperity > 5000)
            {
                dailyResupply += 10.0f;
            }

            // Owned settlement bonus: +10% per day = ~0.42% per hour (10/24)
            if (settlement.OwnerClan == lordParty.LeaderHero?.Clan)
            {
                dailyResupply += 10.0f;
            }

            // Convert daily rate to hourly rate
            float hourlyResupply = dailyResupply / 24.0f;

            return hourlyResupply;
        }

        /// <summary>
        /// Applies supply changes after a battle based on casualties and outcome.
        /// Called from EnlistmentBehavior.OnPlayerBattleEnd or OnMapEventEnded.
        /// </summary>
        /// <param name="troopsLost">Number of friendly troops lost in battle.</param>
        /// <param name="enemiesKilled">Number of enemy troops killed.</param>
        /// <param name="playerWon">Whether the player's side won the battle.</param>
        /// <param name="wasSiege">Whether this was a siege assault.</param>
        public void ProcessBattleSupplyChanges(int troopsLost, int enemiesKilled, bool playerWon, bool wasSiege)
        {
            try
            {
                float oldSupply = _totalSupply;

                // Casualty loss: -1% per 10 troops lost (supplies abandoned/consumed treating wounded)
                float casualtyLoss = troopsLost / 10f;

                // Defeat penalty: -5% for lost battles (abandoned supplies during retreat)
                float defeatPenalty = playerWon ? 0f : 5f;

                // Siege assault penalty: -3% for siege equipment usage
                float siegePenalty = wasSiege ? 3f : 0f;

                // Total loss
                float totalLoss = casualtyLoss + defeatPenalty + siegePenalty;

                // Battle loot: +1% per 25 enemies killed (salvaged supplies from enemy)
                float lootGain = CalculateBattleLoot(enemiesKilled);

                // Apply changes
                _totalSupply = MathF.Clamp(_totalSupply - totalLoss + lootGain, 0f, 100f);

                float netChange = _totalSupply - oldSupply;

                ModLogger.Info(LogCategory,
                    $"Battle supply: casualties={troopsLost} (-{casualtyLoss:F1}%), " +
                    $"defeat={!playerWon} (-{defeatPenalty:F0}%), siege={wasSiege} (-{siegePenalty:F0}%), " +
                    $"loot={enemiesKilled} kills (+{lootGain:F1}%) => net {netChange:+0.0;-0.0}%, now {_totalSupply:F1}%");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Supply", "Error processing battle supply changes", ex);
            }
        }

        /// <summary>
        /// Calculates supply loot from defeated enemies.
        /// Base is +1% per 25 kills, modified by enemy faction quality.
        /// </summary>
        private float CalculateBattleLoot(int enemiesKilled)
        {
            if (enemiesKilled <= 0)
            {
                return 0f;
            }

            // Base loot: 1% per 25 enemies killed
            float baseLoot = enemiesKilled / 25f;

            // Faction multiplier could be added here based on MapEvent data
            // For now, use a standard 1.0x multiplier
            float factionMult = 1.0f;

            return MathF.Min(baseLoot * factionMult, 6f); // Cap at 6% per battle
        }

        /// <summary>
        /// Adds supplies from player actions (foraging duty, purchases, etc.).
        /// </summary>
        /// <param name="amount">Percentage to add (0-100 range clamped).</param>
        /// <param name="source">Description of the source for logging.</param>
        public void AddSupplies(float amount, string source)
        {
            if (amount <= 0)
            {
                return;
            }

            float oldSupply = _totalSupply;
            _totalSupply = MathF.Clamp(_totalSupply + amount, 0f, 100f);
            float actualGain = _totalSupply - oldSupply;

            if (actualGain > 0)
            {
                ModLogger.Info(LogCategory, $"Added {actualGain:F1}% supplies from {source}. Now: {_totalSupply:F1}%");
            }
        }

        /// <summary>
        /// Gets the lord's mobile party for observation.
        /// Returns null if lord has no party or is not available.
        /// Note: Uses EnlistedLord directly (not IsEnlisted) so we track supply even during leave.
        /// </summary>
        private MobileParty GetLordParty()
        {
            try
            {
                // First try to get from EnlistmentBehavior for most current state
                // Note: We check EnlistedLord directly, not IsEnlisted, because IsEnlisted
                // returns false during leave but we still want to observe the lord's party
                var behavior = EnlistmentBehavior.Instance;
                var lord = behavior?.EnlistedLord ?? _enlistedLord;

                if (lord == null)
                {
                    return null;
                }

                // Verify lord is still alive and has a party
                if (!lord.IsAlive)
                {
                    return null;
                }

                var party = lord.PartyBelongedTo;

                // Verify party is valid
                if (party == null || !party.IsActive)
                {
                    return null;
                }

                return party;
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Error getting lord party: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets the supply value directly. Used for save/load restoration.
        /// </summary>
        internal void SetTotalSupply(float value)
        {
            _totalSupply = MathF.Clamp(value, 0f, 100f);
        }

        /// <summary>
        /// Updates the enlisted lord reference. Called when lord changes.
        /// </summary>
        internal void SetEnlistedLord(Hero lord)
        {
            _enlistedLord = lord;
        }
    }
}

