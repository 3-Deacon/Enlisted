using System;
using System.Collections.Generic;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Localization;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Tracks when native personality traits cross level thresholds and pushes news.
    /// Called after applying trait effects to detect milestone changes.
    /// </summary>
    public static class TraitMilestoneTracker
    {
        private const string LogCategory = "TraitMilestone";

        // Cache of previous trait levels for comparison
        private static readonly Dictionary<TraitObject, int> PreviousLevels = new();

        // Traits we track for milestones (mapped from Enlisted reputation)
        private static readonly TraitObject[] TrackedTraits =
        {
            DefaultTraits.Valor,
            DefaultTraits.Calculating,
            DefaultTraits.Honor
        };

        /// <summary>
        /// Checks if any tracked traits have crossed a level threshold since last check.
        /// Pushes news notifications for any changes.
        /// </summary>
        /// <param name="hero">The hero to check traits for (usually MainHero).</param>
        public static void CheckForMilestones(Hero hero)
        {
            if (hero == null) return;

            try
            {
                foreach (var trait in TrackedTraits)
                {
                    var currentLevel = hero.GetTraitLevel(trait);

                    if (!PreviousLevels.TryGetValue(trait, out var previousLevel))
                    {
                        // First time seeing this trait - just record it
                        PreviousLevels[trait] = currentLevel;
                        continue;
                    }

                    if (currentLevel != previousLevel)
                    {
                        PushTraitMilestoneNews(trait, previousLevel, currentLevel);
                        PreviousLevels[trait] = currentLevel;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TraitMilestone", $"Failed to check trait milestones: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resets the tracker state. Called when enlistment ends or save loads.
        /// </summary>
        public static void Reset()
        {
            PreviousLevels.Clear();
            ModLogger.Debug(LogCategory, "Trait milestone tracker reset");
        }

        /// <summary>
        /// Initializes the tracker with current trait levels. 
        /// Called when enlistment begins to set baseline.
        /// </summary>
        /// <param name="hero">The hero to initialize from.</param>
        public static void Initialize(Hero hero)
        {
            if (hero == null) return;

            PreviousLevels.Clear();
            foreach (var trait in TrackedTraits)
            {
                PreviousLevels[trait] = hero.GetTraitLevel(trait);
            }

            ModLogger.Debug(LogCategory, "Trait milestone tracker initialized");
        }

        /// <summary>
        /// Pushes a news notification when a trait crosses a level threshold.
        /// </summary>
        private static void PushTraitMilestoneNews(TraitObject trait, int oldLevel, int newLevel)
        {
            var direction = newLevel > oldLevel ? "gained" : "lost";
            var message = GetTraitMilestoneMessage(trait, newLevel);
            var headlineKey = GetTraitMilestoneHeadlineKey(trait, newLevel);

            // Push to personal news feed
            var news = EnlistedNewsBehavior.Instance;
            if (news != null)
            {
                var placeholders = new Dictionary<string, string>
                {
                    { "TRAIT", trait.Name?.ToString() ?? "Unknown" },
                    { "LEVEL", newLevel.ToString() },
                    { "DIRECTION", direction }
                };

                // Use severity 1 (positive) for gains, 2 (attention) for losses
                var severity = newLevel > oldLevel ? 1 : 2;

                // Add to personal feed via headline key
                try
                {
                    // Use reflection or a public method if available
                    // For now, log and use InformationManager for immediate feedback
                    ModLogger.Info(LogCategory, $"{trait.Name}: {oldLevel} → {newLevel} ({message})");
                }
                catch (Exception ex)
                {
                    ModLogger.Warn(LogCategory, $"Failed to push trait news: {ex.Message}");
                }
            }

            // Also show immediate feedback via information message
            TaleWorlds.Library.InformationManager.DisplayMessage(
                new TaleWorlds.Library.InformationMessage(
                    message,
                    newLevel > oldLevel ? TaleWorlds.Library.Colors.Green : TaleWorlds.Library.Colors.Yellow
                )
            );

            ModLogger.Info(LogCategory, $"Trait milestone: {trait.Name} {oldLevel} → {newLevel}");
        }

        /// <summary>
        /// Gets the localization headline key for a trait milestone.
        /// </summary>
        private static string GetTraitMilestoneHeadlineKey(TraitObject trait, int level)
        {
            var traitKey = trait.StringId?.ToLowerInvariant() ?? "unknown";
            var levelDesc = level >= 0 ? "high" : "low";
            return $"trait_milestone_{traitKey}_{levelDesc}";
        }

        /// <summary>
        /// Gets flavor text based on trait and level for news display.
        /// </summary>
        private static string GetTraitMilestoneMessage(TraitObject trait, int level)
        {
            // Valor messages (mapped from Soldier reputation)
            if (trait == DefaultTraits.Valor)
            {
                return level switch
                {
                    >= 2 => new TextObject("{=trait_valor_2}Your bravery in battle has become legendary among the troops.").ToString(),
                    1 => new TextObject("{=trait_valor_1}Soldiers speak of your courage under fire.").ToString(),
                    0 => new TextObject("{=trait_valor_0}Your reputation for valor is unremarkable.").ToString(),
                    -1 => new TextObject("{=trait_valor_n1}Some whisper that you lack courage.").ToString(),
                    _ => new TextObject("{=trait_valor_n2}Your cowardice is well known.").ToString()
                };
            }

            // Calculating messages (mapped from Officer reputation)
            if (trait == DefaultTraits.Calculating)
            {
                return level switch
                {
                    >= 2 => new TextObject("{=trait_calc_2}Officers seek your counsel on tactical matters.").ToString(),
                    1 => new TextObject("{=trait_calc_1}Your tactical thinking has been noticed by command.").ToString(),
                    0 => new TextObject("{=trait_calc_0}Your judgment is considered sound.").ToString(),
                    -1 => new TextObject("{=trait_calc_n1}Some question your decision-making.").ToString(),
                    _ => new TextObject("{=trait_calc_n2}Your poor judgment is notorious.").ToString()
                };
            }

            // Honor messages (mapped from Lord reputation)
            if (trait == DefaultTraits.Honor)
            {
                return level switch
                {
                    >= 2 => new TextObject("{=trait_honor_2}Lords speak highly of your sense of duty and honor.").ToString(),
                    1 => new TextObject("{=trait_honor_1}Your word is trusted. You've earned respect.").ToString(),
                    0 => new TextObject("{=trait_honor_0}You're known as neither particularly honorable nor dishonorable.").ToString(),
                    -1 => new TextObject("{=trait_honor_n1}Some doubt whether you can be trusted.").ToString(),
                    _ => new TextObject("{=trait_honor_n2}Your reputation for dishonor precedes you.").ToString()
                };
            }

            // Fallback for any other traits
            var textObj = new TextObject("{=trait_generic}Your {TRAIT} has changed.");
            textObj.SetTextVariable("TRAIT", trait.Name ?? new TextObject("trait"));
            return textObj.ToString();
        }
    }
}
