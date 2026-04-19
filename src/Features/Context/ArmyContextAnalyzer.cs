using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Enlisted.Features.Context
{
    /// <summary>
    /// Analyzes army state to determine lord's current objective and priority.
    /// Used for context detection in orders and events. Enhanced with strategic context awareness
    /// using Bannerlord's AI strategic data (Objectives, Aggressiveness, Settlement threat).
    /// </summary>
    public static class ArmyContextAnalyzer
    {
        private const string LogCategory = "Context";
        
        private static JObject _strategicConfig;
        private static bool _configLoaded;

        /// <summary>
        /// Determine the lord's current objective based on army state.
        /// </summary>
        public static LordObjective GetLordObjective(MobileParty army)
        {
            if (army == null)
            {
                ModLogger.Warn(LogCategory, "GetLordObjective called with null army");
                return LordObjective.Unknown;
            }

            // Check if besieging
            if (army.BesiegerCamp != null)
            {
                return LordObjective.Besieging;
            }

            // Check if defending (in settlement)
            if (army.CurrentSettlement is { IsUnderSiege: true })
            {
                return LordObjective.Defending;
            }

            // Check if fleeing (low morale, being chased)
            if (army.Morale < 30 && army.IsActive && army.TargetParty != null)
            {
                // If target party is stronger and we're moving away, we're fleeing
                if (army.TargetParty.Party.NumberOfAllMembers > army.Party.NumberOfAllMembers * 1.5f)
                {
                    return LordObjective.Fleeing;
                }
            }

            // Check army AI behavior
            if (army.Army != null)
            {
                var armyLeader = army.Army.LeaderParty;
                if (armyLeader != null)
                {
                    // Check if raiding (in enemy territory with hostile intent)
                    if (armyLeader is { MapFaction: { } faction, CurrentSettlement.MapFaction: { } settlementFaction } &&
                        faction.IsAtWarWith(settlementFaction))
                    {
                        return LordObjective.Raiding;
                    }

                    // Check if patrolling (moving in own territory)
                    if (armyLeader.DefaultBehavior == AiBehavior.PatrolAroundPoint ||
                        armyLeader.DefaultBehavior == AiBehavior.EscortParty)
                    {
                        return LordObjective.Patrolling;
                    }
                }
            }

            // Check if resting (in friendly settlement, not under siege)
            if (army.CurrentSettlement is { IsUnderSiege: false } settlement &&
                settlement.MapFaction == army.MapFaction)
            {
                return LordObjective.Resting;
            }

            // Check if traveling (moving between locations)
            if (army.TargetSettlement != null || army.TargetParty != null)
            {
                return LordObjective.Traveling;
            }

            // Default to patrolling if no specific state detected
            return LordObjective.Patrolling;
        }

        /// <summary>
        /// Determine priority level for the lord's current objective.
        /// </summary>
        public static LordOrderPriority GetObjectivePriority(LordObjective objective, MobileParty army)
        {
            switch (objective)
            {
                case LordObjective.PreparingBattle:
                case LordObjective.Fleeing:
                    // Battle imminent or fleeing - critical priority
                    return LordOrderPriority.Critical;

                case LordObjective.Besieging:
                case LordObjective.Defending:
                    // Active siege operations - high priority
                    return LordOrderPriority.High;

                case LordObjective.Raiding:
                case LordObjective.Patrolling:
                    // Active operations - medium priority
                    return LordOrderPriority.Medium;

                case LordObjective.Traveling:
                case LordObjective.Resting:
                    // Peaceful activities - low priority
                    return LordOrderPriority.Low;

                default:
                    return LordOrderPriority.Medium;
            }
        }

        /// <summary>
        /// Get a human-readable description of the lord's orders.
        /// </summary>
        public static string GetObjectiveDescription(LordObjective objective)
        {
            return objective switch
            {
                LordObjective.Patrolling => "Patrol the territory and watch for threats",
                LordObjective.Besieging => "Maintain siege operations and secure the perimeter",
                LordObjective.PreparingBattle => "Prepare for imminent battle",
                LordObjective.Traveling => "March to destination and maintain formation",
                LordObjective.Resting => "Rest and recover, maintain camp discipline",
                LordObjective.Defending => "Defend the settlement from siege",
                LordObjective.Raiding => "Raid enemy territory and gather supplies",
                LordObjective.Fleeing => "Retreat and preserve the army",
                _ => "Maintain readiness and follow orders"
            };
        }

        #region Strategic Context Enhancement

        /// <summary>
        /// Loads the strategic context configuration from JSON.
        /// Called automatically on first use.
        /// </summary>
        private static void LoadStrategicContextConfig()
        {
            if (_configLoaded)
            {
                return;
            }

            try
            {
                var configPath = ModulePaths.GetConfigPath("strategic_context_config.json");
                
                if (!File.Exists(configPath))
                {
                    ModLogger.Surfaced("CONTEXT", "Strategic context config not found - army analysis unavailable", null);
                    _strategicConfig = new JObject();
                    _configLoaded = true;
                    return;
                }

                var json = File.ReadAllText(configPath);
                _strategicConfig = JObject.Parse(json);
                _configLoaded = true;
                
                ModLogger.Info(LogCategory, "Strategic context configuration loaded successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Context", "Failed to load strategic context config", ex);
                _strategicConfig = new JObject();
                _configLoaded = true;
            }
        }

        /// <summary>
        /// Determines the faction's war stance based on strategic position.
        /// Returns: "desperate", "defensive", "balanced", or "offensive"
        /// </summary>
        public static string GetWarStance(IFaction faction)
        {
            LoadStrategicContextConfig();

            if (faction == null || !(faction is Kingdom kingdom))
            {
                return "balanced";
            }

            try
            {
                // Calculate faction strength score (0.0 to 1.0+)
                var strengthScore = CalculateFactionStrength(kingdom);

                // Get thresholds from config
                var thresholds = _strategicConfig?["war_stance_thresholds"];
                var desperateThreshold = thresholds?["desperate"]?.Value<float>() ?? 0.3f;
                var defensiveThreshold = thresholds?["defensive"]?.Value<float>() ?? 0.5f;
                var balancedThreshold = thresholds?["balanced"]?.Value<float>() ?? 0.7f;

                // Determine stance based on thresholds
                if (strengthScore < desperateThreshold)
                {
                    return "desperate";
                }
                if (strengthScore < defensiveThreshold)
                {
                    return "defensive";
                }
                if (strengthScore < balancedThreshold)
                {
                    return "balanced";
                }
                
                return "offensive";
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Context", "Error calculating war stance", ex);
                return "balanced";
            }
        }

        /// <summary>
        /// Calculates faction strength as a normalized score (0.0 to 1.0+).
        /// Considers territory control, military strength, and economic situation.
        /// </summary>
        private static float CalculateFactionStrength(Kingdom kingdom)
        {
            try
            {
                var weights = _strategicConfig?["weights"];
                var territoryWeight = weights?["territory_control"]?.Value<float>() ?? 0.4f;
                var militaryWeight = weights?["military_strength"]?.Value<float>() ?? 0.4f;
                var economicWeight = weights?["economic_situation"]?.Value<float>() ?? 0.2f;

                // Territory control (settlements owned vs total)
                var ownedSettlements = kingdom.Settlements.Count(s => s.IsTown || s.IsCastle);
                var totalSettlements = Settlement.All.Count(s => s.IsTown || s.IsCastle);
                var territoryScore = totalSettlements > 0 ? (float)ownedSettlements / totalSettlements : 0.5f;

                // Military strength (active lords and total troops)
                var activeLords = kingdom.AliveLords.Count(h => h.IsAlive && !h.IsDisabled && h.PartyBelongedTo != null);
                var totalTroops = kingdom.Armies.Sum(a => a.TotalManCount) + 
                                  kingdom.AliveLords.Where(h => h.PartyBelongedTo != null).Sum(h => h.PartyBelongedTo.MemberRoster.TotalManCount);
                var militaryScore = MathF.Min(1.0f, (activeLords / 20f) * 0.5f + (totalTroops / 5000f) * 0.5f);

                // Economic situation (clan gold average)
                var avgGold = kingdom.Clans.Where(c => c.Leader != null).Average(c => (float)c.Leader.Gold);
                var economicScore = MathF.Min(1.0f, avgGold / 100000f);

                // Weighted combination
                var strengthScore = (territoryScore * territoryWeight) + 
                                      (militaryScore * militaryWeight) + 
                                      (economicScore * economicWeight);

                return MathF.Max(0f, strengthScore);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Context", "Error calculating faction strength", ex);
                return 0.5f;
            }
        }

        /// <summary>
        /// Detects the current strategic context for the lord's party.
        /// Returns one of 8 contexts: coordinated_offensive, desperate_defense, raid_operation,
        /// siege_operation, patrol_peacetime, garrison_duty, recruitment_drive, winter_camp
        /// </summary>
        public static string GetLordStrategicContext(MobileParty party)
        {
            LoadStrategicContextConfig();

            if (party == null || party.LeaderHero == null)
            {
                return "patrol_peacetime";
            }

            try
            {
                var faction = party.MapFaction;

                // Check siege operation first (highest priority)
                if (party.BesiegerCamp != null || party.SiegeEvent != null)
                {
                    return "siege_operation";
                }

                // Check garrison duty
                if (party is { CurrentSettlement: not null, IsActive: false })
                {
                    return "garrison_duty";
                }

                // Check winter camp (winter season + stationary)
                if (CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Winter && 
                    party.CurrentSettlement is not null)
                {
                    return "winter_camp";
                }

                // Check war stance for war-related contexts
                if (faction != null)
                {
                    var warStance = GetWarStance(faction);
                    var atWar = IsAtWar(faction);

                    // Desperate defense
                    if (atWar && warStance == "desperate" && IsInOwnTerritory(party))
                    {
                        return "desperate_defense";
                    }

                    // Coordinated offensive
                    if (atWar && IsPartOfCoordinatedOperation(party) && IsInEnemyTerritory(party))
                    {
                        return "coordinated_offensive";
                    }

                    // Raid operation
                    if (atWar && IsInEnemyTerritory(party) && party.Army == null && party.Party.NumberOfAllMembers < 200)
                    {
                        return "raid_operation";
                    }

                    // Recruitment drive
                    if (atWar && IsInOwnTerritory(party) && IsArmyBelowStrength(party))
                    {
                        return "recruitment_drive";
                    }

                    // Patrol peacetime (not at war)
                    if (!atWar && IsInOwnTerritory(party))
                    {
                        return "patrol_peacetime";
                    }
                }

                // Default to patrol peacetime
                return "patrol_peacetime";
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Context", "Error detecting strategic context", ex);
                return "patrol_peacetime";
            }
        }

        /// <summary>
        /// Calculates the strategic value of a settlement based on fortification, borders, and prosperity.
        /// Returns a normalized score (0.0 to 1.0+).
        /// </summary>
        public static float GetSettlementStrategicValue(Settlement settlement)
        {
            LoadStrategicContextConfig();

            if (settlement == null)
            {
                return 0f;
            }

            try
            {
                var valueParams = _strategicConfig?["settlement_strategic_value"];
                var fortificationBase = valueParams?["fortification_base"]?.Value<float>() ?? 0.3f;
                var borderBonus = valueParams?["border_bonus"]?.Value<float>() ?? 0.2f;
                var neighborMultiplier = valueParams?["neighbor_multiplier"]?.Value<float>() ?? 0.05f;
                var prosperityScale = valueParams?["prosperity_scale"]?.Value<float>() ?? 0.00001f;

                var value = 0f;

                // Base value from fortification level
                if (settlement.IsCastle)
                {
                    value += fortificationBase;
                }
                else if (settlement.IsTown)
                {
                    value += fortificationBase * 1.5f;
                }

                // Border settlement bonus (has neighbors from different factions)
                var isBorder = settlement.BoundVillages.Any(v => 
                    v.Settlement.OwnerClan?.MapFaction != settlement.OwnerClan?.MapFaction);
                if (isBorder)
                {
                    value += borderBonus;
                }

                // Neighbor count (more connections = more strategic)
                var neighborCount = settlement.BoundVillages.Count;
                value += neighborCount * neighborMultiplier;

                // Prosperity contribution
                if (settlement.Town != null)
                {
                    value += settlement.Town.Prosperity * prosperityScale;
                }

                return MathF.Max(0f, value);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Context", "Error calculating settlement strategic value", ex);
                return 0.5f;
            }
        }

        /// <summary>
        /// Detects if the party is part of a coordinated multi-army operation.
        /// </summary>
        public static bool IsPartOfCoordinatedOperation(MobileParty party)
        {
            LoadStrategicContextConfig();

            if (party == null || party.MapFaction == null)
            {
                return false;
            }

            try
            {
                var coordParams = _strategicConfig?["coordination_detection"];
                var minAlliedLords = coordParams?["min_allied_lords"]?.Value<int>() ?? 2;
                var maxDistanceKm = coordParams?["max_distance_km"]?.Value<float>() ?? 20.0f;

                // Check if in an army
                if (party.Army != null && party.Army.Parties.Count >= minAlliedLords + 1)
                {
                    return true;
                }

                // Check for nearby allied armies with same target
                var nearbyAlliedArmies = 0;
                var targetSettlement = party.TargetSettlement;

                foreach (var otherArmy in MobileParty.All)
                {
                    if (otherArmy == party || otherArmy.MapFaction != party.MapFaction)
                    {
                        continue;
                    }

                    var distance = otherArmy.Position.DistanceSquared(party.Position);
                    if (distance < maxDistanceKm)
                    {
                        // Check if targeting same settlement
                        if (targetSettlement != null && otherArmy.TargetSettlement == targetSettlement)
                        {
                            nearbyAlliedArmies++;
                        }
                        else if (otherArmy.Army != null)
                        {
                            nearbyAlliedArmies++;
                        }
                    }
                }

                return nearbyAlliedArmies >= minAlliedLords;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Context", "Error detecting coordinated operation", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the appropriate order tags for the current strategic context.
        /// </summary>
        public static List<string> GetContextOrderTags(string context)
        {
            LoadStrategicContextConfig();

            try
            {
                var contextData = _strategicConfig?["strategic_contexts"]?[context];
                if (contextData == null)
                {
                    return new List<string> { "routine" };
                }

                var orderTags = contextData["order_tags"]?.ToObject<List<string>>();
                return orderTags ?? new List<string> { "routine" };
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Context", $"Error getting order tags for context {context}", ex);
                return new List<string> { "routine" };
            }
        }

        /// <summary>
        /// Gets the inappropriate order tags for the current strategic context.
        /// </summary>
        public static List<string> GetContextInappropriateTags(string context)
        {
            LoadStrategicContextConfig();

            try
            {
                var contextData = _strategicConfig?["strategic_contexts"]?[context];
                if (contextData == null)
                {
                    return new List<string>();
                }

                var inappropriateTags = contextData["inappropriate_tags"]?.ToObject<List<string>>();
                return inappropriateTags ?? new List<string>();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Context", $"Error getting inappropriate tags for context {context}", ex);
                return new List<string>();
            }
        }

        #endregion

        #region Helper Methods

        private static bool IsAtWar(IFaction faction)
        {
            if (faction == null)
            {
                return false;
            }
            
            return Kingdom.All.Any(k => k != faction && FactionManager.IsAtWarAgainstFaction(faction, k));
        }

        private static bool IsInOwnTerritory(MobileParty party)
        {
            if (party == null)
            {
                return false;
            }

            if (party.CurrentSettlement is { MapFaction: var faction })
            {
                return faction == party.MapFaction;
            }

            // Check nearest settlement
            Settlement nearestSettlement = null;
            var minDistance = float.MaxValue;
            foreach (var settlement in Settlement.All)
            {
                if (settlement == null)
                {
                    continue;
                }

                var dist = settlement.GetPosition2D.Distance(party.Position.ToVec2());
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestSettlement = settlement;
                }
            }
            return nearestSettlement != null && nearestSettlement.MapFaction == party.MapFaction;
        }

        private static bool IsInEnemyTerritory(MobileParty party)
        {
            if (party == null)
            {
                return false;
            }

            if (party.CurrentSettlement is { MapFaction: { } settlementFaction } && party.MapFaction is { } partyFaction)
            {
                return FactionManager.IsAtWarAgainstFaction(partyFaction, settlementFaction);
            }

            // Check nearest settlement
            Settlement nearestSettlement = null;
            var minDistance = float.MaxValue;
            foreach (var settlement in Settlement.All)
            {
                if (settlement == null)
                {
                    continue;
                }

                var dist = settlement.GetPosition2D.Distance(party.Position.ToVec2());
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestSettlement = settlement;
                }
            }
            return nearestSettlement != null && nearestSettlement.MapFaction != null && 
                   party.MapFaction != null &&
                   FactionManager.IsAtWarAgainstFaction(party.MapFaction, nearestSettlement.MapFaction);
        }

        private static bool IsArmyBelowStrength(MobileParty party)
        {
            if (party?.LeaderHero == null)
            {
                return false;
            }

            // Check if party is below 60% of its limit
            var currentSize = party.MemberRoster.TotalManCount;
            var limitSize = party.Party.PartySizeLimit;

            return currentSize < (limitSize * 0.6f);
        }

        #endregion
    }
}

