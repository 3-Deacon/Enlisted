using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Bridges JSON-defined EventEffects to native Bannerlord IncidentEffect objects.
    /// This provides automatic tooltip generation via GetHint() and standardized
    /// effect application patterns while preserving JSON content flexibility.
    /// </summary>
    public static class IncidentEffectTranslator
    {
        private const string LogCategory = "EffectTranslator";

        /// <summary>
        /// Converts EventEffects from JSON into a list of native IncidentEffect objects.
        /// These can be executed with Consequence() or queried with GetHint() for tooltips.
        /// </summary>
        /// <param name="effects">The JSON-defined effects to translate.</param>
        /// <returns>List of native IncidentEffect objects. Empty list if effects is null.</returns>
        public static List<IncidentEffect> TranslateEffects(EventEffects effects)
        {
            var result = new List<IncidentEffect>();
            if (effects == null) return result;

            try
            {
                // Gold changes - safe for any game state
                if (effects.Gold.HasValue && effects.Gold.Value != 0)
                {
                    var goldAmount = effects.Gold.Value;
                    result.Add(IncidentEffect.GoldChange(() => goldAmount));
                    ModLogger.Debug(LogCategory, $"Translated gold change: {goldAmount}");
                }

                // Skill XP awards
                if (effects.SkillXp != null && effects.SkillXp.Count > 0)
                {
                    foreach (var kvp in effects.SkillXp)
                    {
                        var skill = FindSkillByName(kvp.Key);
                        if (skill != null)
                        {
                            result.Add(IncidentEffect.SkillChange(skill, kvp.Value));
                            ModLogger.Debug(LogCategory, $"Translated skill XP: {kvp.Key} +{kvp.Value}");
                        }
                        else
                        {
                            ModLogger.Warn(LogCategory, $"Could not find skill: {kvp.Key}");
                        }
                    }
                }

                // Discipline effects no longer mapped to morale (morale system removed)
                // Native party morale (RecentEventsMorale) was previously used but is now skipped

                // Health/HP changes
                if (effects.HpChange.HasValue && effects.HpChange.Value != 0)
                {
                    result.Add(IncidentEffect.HealthChance(effects.HpChange.Value));
                    ModLogger.Debug(LogCategory, $"Translated HP change: {effects.HpChange.Value}");
                }

                // Renown changes
                if (effects.Renown.HasValue && effects.Renown.Value != 0)
                {
                    result.Add(IncidentEffect.RenownChange(effects.Renown.Value));
                    ModLogger.Debug(LogCategory, $"Translated renown: {effects.Renown.Value}");
                }

                // Troop effects - only if party exists
                if (MobileParty.MainParty != null)
                {
                    // Troop loss (kills)
                    if (effects.TroopLoss.HasValue && effects.TroopLoss.Value > 0)
                    {
                        // Native API uses percentage, but we have absolute count
                        // We'll create a custom effect for this instead
                        ModLogger.Debug(LogCategory, $"TroopLoss {effects.TroopLoss.Value} - using manual application (native uses percentage)");
                    }

                    // Troop wounded
                    if (effects.TroopWounded.HasValue && effects.TroopWounded.Value > 0)
                    {
                        result.Add(IncidentEffect.WoundTroopsRandomly(effects.TroopWounded.Value));
                        ModLogger.Debug(LogCategory, $"Translated troop wounded: {effects.TroopWounded.Value}");
                    }
                }

                // Native trait integration - map custom reputation to personality traits
                var config = ConfigurationManager.LoadNativeTraitMappingConfig();
                if (config.Enabled)
                {
                    result.AddRange(TranslateReputationToTraits(effects, config));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, $"Translation failed: {ex.Message}", ex);
                // Return partial results - don't lose effects that succeeded
            }

            ModLogger.Debug(LogCategory, $"Translated {result.Count} native effects");
            return result;
        }

        /// <summary>
        /// Maps custom Lord reputation to native personality traits.
        /// This provides free UI integration on the character sheet and affects
        /// native game reactions from lords and companions.
        /// </summary>
        private static List<IncidentEffect> TranslateReputationToTraits(EventEffects effects, NativeTraitMappingConfig config)
        {
            var result = new List<IncidentEffect>();

            // Lord rep → Honor (duty, keeping word)
            if (effects.LordRep.HasValue && effects.LordRep.Value != 0)
            {
                var honorAmount = effects.LordRep.Value / config.ScaleDivisor;
                if (Math.Abs(honorAmount) >= config.MinimumChange)
                {
                    result.Add(IncidentEffect.TraitChange(DefaultTraits.Honor, honorAmount));
                    ModLogger.Debug(LogCategory, $"Lord rep {effects.LordRep.Value} → Honor {honorAmount}");
                }
            }

            return result;
        }

        /// <summary>
        /// Generates tooltip hint text for an option's effects.
        /// Uses native IncidentEffect.GetHint() which includes probability display.
        /// </summary>
        /// <param name="effects">The effects to generate hints for.</param>
        /// <returns>List of hint text objects for tooltip display.</returns>
        public static List<TextObject> GenerateTooltipHints(EventEffects effects)
        {
            var hints = new List<TextObject>();
            if (effects == null) return hints;

            try
            {
                var incidentEffects = TranslateEffects(effects);

                foreach (var effect in incidentEffects)
                {
                    var effectHints = effect.GetHint();
                    if (effectHints != null)
                    {
                        hints.AddRange(effectHints);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn(LogCategory, $"Failed to generate tooltip hints: {ex.Message}");
            }

            return hints;
        }

        /// <summary>
        /// Generates a combined tooltip string from effects, including both native
        /// and Enlisted-specific effects.
        /// </summary>
        /// <param name="effects">The effects to describe.</param>
        /// <returns>Combined tooltip text with newlines between entries.</returns>
        public static string BuildCombinedTooltip(EventEffects effects)
        {
            if (effects == null) return string.Empty;

            var hints = new List<string>();

            // Get native effect hints
            var nativeHints = GenerateTooltipHints(effects);
            hints.AddRange(nativeHints.Select(h => h.ToString()));

            // Add Enlisted-specific effect hints (not in native system)
            if (effects.RetinueGain.HasValue && effects.RetinueGain.Value > 0)
            {
                hints.Add($"+{effects.RetinueGain.Value} Retinue Soldiers");
            }

            if (effects.RetinueLoss.HasValue && effects.RetinueLoss.Value > 0)
            {
                hints.Add($"-{effects.RetinueLoss.Value} Retinue Soldiers");
            }

            if (effects.RetinueLoyalty.HasValue && effects.RetinueLoyalty.Value != 0)
            {
                var sign = effects.RetinueLoyalty.Value > 0 ? "+" : "";
                hints.Add($"{sign}{effects.RetinueLoyalty.Value} Retinue Loyalty");
            }

            if (effects.Scrutiny.HasValue && effects.Scrutiny.Value != 0)
            {
                var sign = effects.Scrutiny.Value > 0 ? "+" : "";
                hints.Add($"{sign}{effects.Scrutiny.Value} Scrutiny");
            }

            // TroopLoss hint (manual since native uses percentage)
            if (effects.TroopLoss.HasValue && effects.TroopLoss.Value > 0)
            {
                hints.Add($"Lose {effects.TroopLoss.Value} troops");
            }

            if (effects.FoodLoss.HasValue && effects.FoodLoss.Value > 0)
            {
                hints.Add($"Lose {effects.FoodLoss.Value} food");
            }

            if (effects.GrantTemporaryBaggageAccess.HasValue && effects.GrantTemporaryBaggageAccess.Value > 0)
            {
                hints.Add($"Baggage access for {effects.GrantTemporaryBaggageAccess.Value} hours");
            }

            if (!string.IsNullOrEmpty(effects.TriggersDischarge))
            {
                hints.Add($"Triggers discharge: {effects.TriggersDischarge}");
            }

            if (effects.Promotes.HasValue && effects.Promotes.Value > 0)
            {
                hints.Add($"Promotes to Tier {effects.Promotes.Value}");
            }

            return string.Join("\n", hints);
        }

        /// <summary>
        /// Applies native effects and returns feedback messages.
        /// Should be called before manual Enlisted-specific effect application.
        /// </summary>
        /// <param name="effects">The effects to apply.</param>
        /// <returns>List of feedback messages from effect application.</returns>
        public static List<string> ApplyNativeEffects(EventEffects effects)
        {
            var feedback = new List<string>();
            if (effects == null) return feedback;

            var nativeEffects = TranslateEffects(effects);

            foreach (var effect in nativeEffects)
            {
                try
                {
                    // Check condition before applying
                    if (!effect.Condition())
                    {
                        ModLogger.Debug(LogCategory, "Effect condition not met, skipping");
                        continue;
                    }

                    var results = effect.Consequence();
                    if (results != null)
                    {
                        foreach (var result in results)
                        {
                            if (result != null)
                            {
                                feedback.Add(result.ToString());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Warn(LogCategory, $"Native effect failed: {ex.Message}");
                }
            }

            return feedback;
        }

        /// <summary>
        /// Finds a skill by name, checking both StringId and display Name.
        /// </summary>
        private static SkillObject FindSkillByName(string skillName)
        {
            if (string.IsNullOrWhiteSpace(skillName)) return null;

            // Try exact StringId match first
            var skill = Skills.All.FirstOrDefault(s =>
                s.StringId.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill != null) return skill;

            // Try display name match
            skill = Skills.All.FirstOrDefault(s =>
                s.Name?.ToString()?.Equals(skillName, StringComparison.OrdinalIgnoreCase) == true);

            return skill;
        }
    }
}
