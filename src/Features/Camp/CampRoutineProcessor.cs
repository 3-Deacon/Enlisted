using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Company;
using Enlisted.Features.Content;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Camp
{
    /// <summary>
    /// Processes routine activities at phase boundaries and generates outcomes.
    /// Handles XP gains, resource changes, conditions, and news/combat log output.
    /// Works with CampScheduleManager to determine what activities occur and applies
    /// dynamic outcome rolls (good day/bad day/mishap) for narrative variety.
    /// </summary>
    public static class CampRoutineProcessor
    {
        private const string LogCategory = "RoutineProcessor";
        
        private static JObject _outcomesConfig;
        private static bool _configLoaded;
        private static readonly Random _random = new Random();

        /// <summary>
        /// Processes the scheduled activities for a completed phase and generates outcomes.
        /// Called at phase boundaries by CampOpportunityGenerator.
        /// </summary>
        public static List<RoutineOutcome> ProcessPhaseTransition(DayPhase completedPhase, ScheduledPhase schedule)
        {
            var outcomes = new List<RoutineOutcome>();

            if (schedule == null)
            {
                ModLogger.Debug(LogCategory, $"No schedule for phase {completedPhase}, skipping routine processing");
                return outcomes;
            }

            // Skip if player has a commitment (they're doing something specific)
            if (schedule.HasPlayerCommitment)
            {
                ModLogger.Debug(LogCategory, 
                    $"Player has commitment '{schedule.PlayerCommitmentTitle}', skipping auto-routine");
                return outcomes;
            }

            EnsureConfigLoaded();

            // Check if this is an orchestrator override
            var isOverride = schedule.OrchestratorOverride != null;
            var overrideInfo = schedule.OrchestratorOverride;

            // Log the phase transition
            if (isOverride)
            {
                ModLogger.Info(LogCategory, 
                    $"Processing override routine for {completedPhase}: {overrideInfo.ActivityName}");
            }
            else
            {
                ModLogger.Debug(LogCategory, 
                    $"Processing routine for {completedPhase}: {schedule.Slot1Description}");
            }

            // Process slot 1 if not skipped
            if (!schedule.Slot1Skipped)
            {
                var outcome1 = ProcessActivity(
                    completedPhase,
                    schedule.Slot1Category,
                    schedule.Slot1Description,
                    isOverride,
                    overrideInfo);

                if (outcome1 != null)
                {
                    outcomes.Add(outcome1);
                    ApplyOutcome(outcome1);
                    SendCombatLogMessage(outcome1);
                    AddToNewsFeed(outcome1);
                }
            }

            // Process slot 2 if not skipped (and not replaced by override)
            if (!schedule.Slot2Skipped && !(isOverride && overrideInfo?.ReplaceBothSlots == true))
            {
                var outcome2 = ProcessActivity(
                    completedPhase,
                    schedule.Slot2Category,
                    schedule.Slot2Description,
                    false, // Slot 2 uses normal processing even during override
                    null);

                if (outcome2 != null)
                {
                    outcomes.Add(outcome2);
                    ApplyOutcome(outcome2);
                    SendCombatLogMessage(outcome2);
                    AddToNewsFeed(outcome2);
                }
            }

            // Clear the orchestrator's current override after processing
            if (isOverride)
            {
                ContentOrchestrator.Instance?.ClearCurrentOverride();
            }

            return outcomes;
        }

        /// <summary>
        /// Processes a single activity and generates an outcome with dynamic rolls.
        /// </summary>
        private static RoutineOutcome ProcessActivity(
            DayPhase phase,
            string category,
            string description,
            bool isOverride,
            OrchestratorOverride overrideInfo)
        {
            if (string.IsNullOrEmpty(category))
            {
                return null;
            }

            // Get activity config
            var activityConfig = GetActivityConfig(category);
            if (activityConfig == null)
            {
                ModLogger.Debug(LogCategory, $"No config for activity '{category}', using defaults");
                activityConfig = CreateDefaultActivityConfig(category);
            }

            // Roll for outcome quality
            var outcomeType = RollOutcomeType(category);

            // Calculate XP
            int xpGained = CalculateXp(activityConfig, outcomeType, overrideInfo);

        // Calculate other effects
        int goldChange = CalculateGoldChange(activityConfig, outcomeType);
        int supplyChange = CalculateSupplyChange(activityConfig, outcomeType);

        // Check for mishap condition
            string conditionApplied = null;
            if (outcomeType == OutcomeType.Mishap)
            {
                conditionApplied = CheckForMishapCondition(activityConfig);
            }

            // Get flavor text
            string flavorText = GetFlavorText(activityConfig, outcomeType);
            string skillName = activityConfig["skill"]?.Value<string>() ?? "Athletics";

            var outcome = new RoutineOutcome
            {
                Phase = phase,
                ActivityCategory = category,
                ActivityName = description ?? activityConfig["name"]?.Value<string>() ?? category,
                Outcome = outcomeType,
                XpGained = xpGained,
            SkillAffected = skillName,
            GoldChange = goldChange,
            SupplyChange = supplyChange,
            ConditionApplied = conditionApplied,
                FlavorText = flavorText,
                WasOverride = isOverride,
                OverrideReason = overrideInfo?.Reason
            };

            ModLogger.Debug(LogCategory, 
                $"Activity '{category}' outcome: {outcomeType} (+{xpGained} XP)");

            return outcome;
        }

        /// <summary>
        /// Rolls for outcome type using weighted random based on player state.
        /// </summary>
        private static OutcomeType RollOutcomeType(string category)
        {
            // Get weight set based on player condition
            var weightSet = DetermineWeightSet();
            var weights = GetOutcomeWeights(weightSet);

            // Roll weighted random
            int total = weights.excellent + weights.good + weights.normal + weights.poor + weights.mishap;
            int roll = _random.Next(total);

            int cumulative = 0;
            
            cumulative += weights.excellent;
            if (roll < cumulative) return OutcomeType.Excellent;
            
            cumulative += weights.good;
            if (roll < cumulative) return OutcomeType.Good;
            
            cumulative += weights.normal;
            if (roll < cumulative) return OutcomeType.Normal;
            
            cumulative += weights.poor;
            if (roll < cumulative) return OutcomeType.Poor;
            
            return OutcomeType.Mishap;
        }

        /// <summary>
        /// Determines which weight set to use based on player state.
        /// </summary>
        private static string DetermineWeightSet()
        {
            var needs = EnlistmentBehavior.Instance?.CompanyNeeds;
            if (needs == null)
            {
                return "default";
            }

        // Check for negative conditions
        // Note: Morale removed (system no longer exists)
        // For now, always use default weights

        // TODO: Check player skill level for highSkill set
            // For now, default to normal weights
            return "default";
        }

        /// <summary>
        /// Gets outcome weights from config.
        /// </summary>
        private static (int excellent, int good, int normal, int poor, int mishap) GetOutcomeWeights(string setName)
        {
            try
            {
                var weightsConfig = _outcomesConfig?["outcomeWeights"]?[setName];
                if (weightsConfig != null)
                {
                    return (
                        weightsConfig["excellent"]?.Value<int>() ?? 10,
                        weightsConfig["good"]?.Value<int>() ?? 25,
                        weightsConfig["normal"]?.Value<int>() ?? 40,
                        weightsConfig["poor"]?.Value<int>() ?? 18,
                        weightsConfig["mishap"]?.Value<int>() ?? 7
                    );
                }
            }
            catch
            {
                // Fall through to defaults
            }

            return (10, 25, 40, 18, 7);
        }

        /// <summary>
        /// Calculates XP gained based on outcome type and activity config.
        /// </summary>
        private static int CalculateXp(JToken activityConfig, OutcomeType outcome, OrchestratorOverride overrideInfo)
        {
            int min, max;

            // Use override XP range if available
            if (overrideInfo != null)
            {
                min = overrideInfo.BaseXpMin;
                max = overrideInfo.BaseXpMax;
            }
            else
            {
                var xpRanges = activityConfig["xpRanges"]?[outcome.ToString().ToLowerInvariant()];
                min = xpRanges?["min"]?.Value<int>() ?? 3;
                max = xpRanges?["max"]?.Value<int>() ?? 10;
            }

            // Apply outcome modifier
            float modifier = outcome switch
            {
                OutcomeType.Excellent => 1.5f,
                OutcomeType.Good => 1.2f,
                OutcomeType.Normal => 1.0f,
                OutcomeType.Poor => 0.5f,
                OutcomeType.Mishap => 0.2f,
                _ => 1.0f
            };

            int baseXp = _random.Next(min, max + 1);
            return (int)(baseXp * modifier);
        }


        /// <summary>
        /// Calculates gold change based on activity type and outcome.
        /// </summary>
        private static int CalculateGoldChange(JToken activityConfig, OutcomeType outcome)
        {
            var goldChance = activityConfig["goldChance"]?[outcome.ToString().ToLowerInvariant()]?.Value<float>() ?? 0;
            
            if (goldChance <= 0)
            {
                // Check for gold loss on mishap
                var lossChance = activityConfig["goldLossChance"]?["mishap"]?.Value<float>() ?? 0;
                if (outcome == OutcomeType.Mishap && _random.NextDouble() < lossChance)
                {
                    var lossRange = activityConfig["goldLossRange"];
                    int lossMin = lossRange?["min"]?.Value<int>() ?? 5;
                    int lossMax = lossRange?["max"]?.Value<int>() ?? 20;
                    return -_random.Next(lossMin, lossMax + 1);
                }
                return 0;
            }

            if (_random.NextDouble() >= goldChance)
            {
                return 0;
            }

            var range = activityConfig["goldRange"];
            int min = range?["min"]?.Value<int>() ?? 5;
            int max = range?["max"]?.Value<int>() ?? 25;
            
            return _random.Next(min, max + 1);
        }

        /// <summary>
        /// Calculates supply change from foraging and similar activities.
        /// </summary>
        private static int CalculateSupplyChange(JToken activityConfig, OutcomeType outcome)
        {
            var supplyChange = activityConfig["supplyChange"]?[outcome.ToString().ToLowerInvariant()];
            if (supplyChange == null)
            {
                return 0;
            }

            int min = supplyChange["min"]?.Value<int>() ?? 0;
            int max = supplyChange["max"]?.Value<int>() ?? 0;

            if (min == 0 && max == 0)
            {
                return 0;
            }

            return _random.Next(min, max + 1);
        }


        /// <summary>
        /// Checks if a mishap should apply a condition.
        /// </summary>
        private static string CheckForMishapCondition(JToken activityConfig)
        {
            var conditionId = activityConfig["mishapCondition"]?.Value<string>();
            if (string.IsNullOrEmpty(conditionId))
            {
                return null;
            }

            var chance = activityConfig["mishapChance"]?.Value<float>() ?? 0.5f;
            if (_random.NextDouble() < chance)
            {
                return conditionId;
            }

            return null;
        }

        /// <summary>
        /// Gets flavor text for the outcome from config.
        /// Checks for sea variants when party is at sea, falls back to land variants.
        /// </summary>
        private static string GetFlavorText(JToken activityConfig, OutcomeType outcome)
        {
            List<string> texts = null;
            
            // Check if we're at sea and have sea variants
            if (IsPartyAtSea())
            {
                texts = activityConfig["seaVariants"]?[outcome.ToString().ToLowerInvariant()]?.ToObject<List<string>>();
                if (texts != null && texts.Count > 0)
                {
                    ModLogger.Debug(LogCategory, "Using sea variant flavor text");
                    return texts[_random.Next(texts.Count)];
                }
            }
            
            // Fall back to standard land flavor text
            texts = activityConfig["flavorText"]?[outcome.ToString().ToLowerInvariant()]?.ToObject<List<string>>();
            if (texts == null || texts.Count == 0)
            {
                return GetDefaultFlavorText(outcome);
            }

            return texts[_random.Next(texts.Count)];
        }
        
        /// <summary>
        /// Checks if the party is currently at sea.
        /// Uses native IsCurrentlyAtSea property for Warsails DLC compatibility.
        /// </summary>
        private static bool IsPartyAtSea()
        {
            try
            {
                // Check the main party (player's party) for sea travel status
                // This is consistent with other parts of the codebase
                var mainParty = MobileParty.MainParty;
                if (mainParty != null)
                {
                    return mainParty.IsCurrentlyAtSea;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(LogCategory, $"Failed to check sea travel status: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// Gets default flavor text when config doesn't have any.
        /// </summary>
        private static string GetDefaultFlavorText(OutcomeType outcome)
        {
            return outcome switch
            {
                OutcomeType.Excellent => "Excellent performance today.",
                OutcomeType.Good => "Good work done.",
                OutcomeType.Normal => "Activity completed.",
                OutcomeType.Poor => "Struggled through the day.",
                OutcomeType.Mishap => "Something went wrong.",
                _ => "Done."
            };
        }

        /// <summary>
        /// Applies the outcome effects to player state.
        /// </summary>
        private static void ApplyOutcome(RoutineOutcome outcome)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            // Apply XP to the appropriate skill
            if (outcome.XpGained > 0 && !string.IsNullOrEmpty(outcome.SkillAffected))
            {
                ApplySkillXp(outcome.SkillAffected, outcome.XpGained);
            }

            // Apply gold change
            if (outcome.GoldChange != 0 && Hero.MainHero != null)
            {
                Hero.MainHero.ChangeHeroGold(outcome.GoldChange);
            }

            // Apply company need changes
            var needs = enlistment.CompanyNeeds;
            if (needs != null)
            {
            if (outcome.SupplyChange != 0)
            {
                needs.ModifyNeed(CompanyNeed.Supplies, outcome.SupplyChange);
            }
            // Note: Morale removed (system no longer exists)
        }

        // Apply condition if mishap caused one
            if (!string.IsNullOrEmpty(outcome.ConditionApplied))
            {
                ApplyCondition(outcome.ConditionApplied);
            }
        }

        /// <summary>
        /// Applies skill XP using the game's skill system and awards enlistment XP for rank progression.
        /// </summary>
        private static void ApplySkillXp(string skillName, int xp)
        {
            try
            {
                // Try to find the skill
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return;
                }

                // Map skill name to DefaultSkills
                var skill = GetSkillFromName(skillName);
                if (skill != null)
                {
                    hero.AddSkillXp(skill, xp);
                    ModLogger.Debug(LogCategory, $"Applied {xp} XP to {skillName}");

                    // Award enlistment XP for rank progression (what shows in muster reports)
                    EnlistmentBehavior.Instance?.AddEnlistmentXP(xp, $"Camp: {skillName}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, $"Failed to apply skill XP for {skillName}", ex);
            }
        }

        /// <summary>
        /// Maps skill name string to SkillObject.
        /// </summary>
        private static SkillObject GetSkillFromName(string skillName)
        {
            return skillName?.ToLowerInvariant() switch
            {
                "onehanded" => DefaultSkills.OneHanded,
                "twohanded" => DefaultSkills.TwoHanded,
                "polearm" => DefaultSkills.Polearm,
                "bow" => DefaultSkills.Bow,
                "crossbow" => DefaultSkills.Crossbow,
                "throwing" => DefaultSkills.Throwing,
                "riding" => DefaultSkills.Riding,
                "athletics" => DefaultSkills.Athletics,
                "crafting" or "smithing" => DefaultSkills.Crafting,
                "scouting" => DefaultSkills.Scouting,
                "tactics" => DefaultSkills.Tactics,
                "roguery" => DefaultSkills.Roguery,
                "charm" => DefaultSkills.Charm,
                "leadership" => DefaultSkills.Leadership,
                "trade" => DefaultSkills.Trade,
                "steward" => DefaultSkills.Steward,
                "medicine" => DefaultSkills.Medicine,
                "engineering" => DefaultSkills.Engineering,
                "perception" => DefaultSkills.Scouting, // Map perception checks to Scouting skill
                // Note: "Discipline" is NOT a skill - it's an escalation track. Use valid skill names in configs.
                _ => DefaultSkills.Athletics
            };
        }

        /// <summary>
        /// Applies a condition to the player character.
        /// </summary>
        private static void ApplyCondition(string conditionId)
        {
            // TODO: Integrate with ConditionManager when implemented
            ModLogger.Info(LogCategory, $"Condition applied: {conditionId}");
        }

        /// <summary>
        /// Sends a combat log message for the outcome.
        /// </summary>
        private static void SendCombatLogMessage(RoutineOutcome outcome)
        {
            var message = outcome.GetCombatLogText();
            var color = GetMessageColor(outcome);

            InformationManager.DisplayMessage(new InformationMessage(message, color));
        }

        /// <summary>
        /// Gets the appropriate color for the combat log message.
        /// </summary>
        private static Color GetMessageColor(RoutineOutcome outcome)
        {
            return outcome.Outcome switch
            {
                OutcomeType.Excellent => Color.FromUint(0xFF44AA44), // Green
                OutcomeType.Good => Color.FromUint(0xFF88CC88), // Light green
                OutcomeType.Normal => Color.FromUint(0xFFCCCCCC), // Light gray
                OutcomeType.Poor => Color.FromUint(0xFFCCCC44), // Yellow
                OutcomeType.Mishap => Color.FromUint(0xFFCC4444), // Red
                _ => Color.FromUint(0xFFCCCCCC)
            };
        }

        /// <summary>
        /// Adds the outcome to the news feed via EnlistedNewsBehavior.
        /// </summary>
        private static void AddToNewsFeed(RoutineOutcome outcome)
        {
            var newsBehavior = EnlistedNewsBehavior.Instance;
            if (newsBehavior == null)
            {
                return;
            }

            newsBehavior.AddRoutineOutcome(outcome);
        }

        /// <summary>
        /// Gets activity configuration from the loaded config.
        /// </summary>
        private static JToken GetActivityConfig(string category)
        {
            return _outcomesConfig?["activities"]?[category];
        }

        /// <summary>
        /// Creates a default activity config for unknown categories.
        /// </summary>
        private static JToken CreateDefaultActivityConfig(string category)
        {
            var defaultConfig = new JObject
            {
                ["name"] = category,
                ["skill"] = "Athletics",
                ["xpRanges"] = new JObject
                {
                    ["excellent"] = new JObject { ["min"] = 10, ["max"] = 15 },
                    ["good"] = new JObject { ["min"] = 6, ["max"] = 10 },
                    ["normal"] = new JObject { ["min"] = 3, ["max"] = 6 },
                    ["poor"] = new JObject { ["min"] = 1, ["max"] = 3 },
                    ["mishap"] = new JObject { ["min"] = 0, ["max"] = 1 }
                }
            };

            return defaultConfig;
        }

        /// <summary>
        /// Ensures the outcomes configuration is loaded.
        /// </summary>
        private static void EnsureConfigLoaded()
        {
            if (_configLoaded)
            {
                return;
            }

            try
            {
                var configPath = ModulePaths.GetConfigPath("routine_outcomes.json");

                if (!File.Exists(configPath))
                {
                    ModLogger.Warn(LogCategory, $"Routine outcomes config not found at {configPath}");
                    _configLoaded = true;
                    return;
                }

                var json = File.ReadAllText(configPath);
                _outcomesConfig = JObject.Parse(json);
                _configLoaded = true;
                ModLogger.Info(LogCategory, "Routine outcomes config loaded");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load routine outcomes config", ex);
                _configLoaded = true;
            }
        }
    }
}
