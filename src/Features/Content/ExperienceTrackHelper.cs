using System;
using Enlisted.Features.Retinue.Data;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Determines the player's experience track based on their level and provides
    /// starting tier calculations for enlistment and training XP modifiers.
    /// </summary>
    public static class ExperienceTrackHelper
    {
        // Experience track thresholds based on player level
        private const int GreenMaxLevel = 9;
        private const int SeasonedMaxLevel = 20;

        /// <summary>
        /// Experience track identifiers. These determine starting tier and training XP modifiers.
        /// </summary>
        public static class Tracks
        {
            public const string Green = "green";
            public const string Seasoned = "seasoned";
            public const string Veteran = "veteran";
        }

        /// <summary>
        /// Returns the experience track for a hero based on their character level.
        /// 
        /// Track thresholds:
        /// - Green: Levels 1-9 (new to military life)
        /// - Seasoned: Levels 10-20 (knows the basics)
        /// - Veteran: Levels 21+ (proven fighter)
        /// </summary>
        /// <param name="hero">The hero to evaluate. Uses Hero.MainHero if null.</param>
        /// <returns>Track identifier: "green", "seasoned", or "veteran".</returns>
        public static string GetExperienceTrack(Hero hero)
        {
            hero ??= Hero.MainHero;
            if (hero == null)
            {
                return Tracks.Seasoned;
            }

            var level = hero.Level;

            if (level <= GreenMaxLevel)
            {
                return Tracks.Green;
            }

            if (level <= SeasonedMaxLevel)
            {
                return Tracks.Seasoned;
            }

            return Tracks.Veteran;
        }

        /// <summary>
        /// Returns the base starting tier for an experience track.
        /// 
        /// Track tiers:
        /// - Green: Tier 1 (recruit)
        /// - Seasoned: Tier 2 (soldier)
        /// - Veteran: Tier 3 (experienced fighter)
        /// </summary>
        /// <param name="track">The experience track identifier.</param>
        /// <returns>Base starting tier (1-3).</returns>
        public static int GetBaseTierForTrack(string track)
        {
            return track switch
            {
                Tracks.Green => 1,
                Tracks.Seasoned => 2,
                Tracks.Veteran => 3,
                _ => 1
            };
        }

        // Maximum starting tier from experience track system.
        // Reservist bonuses (veteran/honorable discharge) can exceed this cap.
        private const int MaxExperienceStartingTier = 3;

        /// <summary>
        /// Calculates the starting tier for enlistment, considering both the player's
        /// experience track and their prior service history with the faction.
        /// 
        /// Takes the higher of:
        /// - Base tier from experience track
        /// - Faction record tier (HighestTier - 2, minimum 1)
        /// 
        /// Result is capped at tier 3. Higher tiers require the reservist discharge
        /// bonus system (veteran/honorable discharge from the same faction).
        /// </summary>
        /// <param name="track">The player's experience track.</param>
        /// <param name="factionRecord">Optional faction service record for prior service bonus.</param>
        /// <returns>Starting tier for enlistment (1-3).</returns>
        public static int GetStartingTierForTrack(string track, FactionServiceRecord factionRecord = null)
        {
            var experienceTier = GetBaseTierForTrack(track);

            // Prior faction service can boost starting tier.
            // Veterans start 2 tiers below their highest achieved tier (minimum 1).
            var factionTier = 1;
            if (factionRecord != null && factionRecord.HighestTier > 0)
            {
                // Check if player had a bad discharge - if so, don't grant faction bonus
                var lastBand = factionRecord.LastDischargeBand?.ToLowerInvariant() ?? string.Empty;
                var wasBadDischarge = lastBand == "washout" || lastBand == "dishonorable" || lastBand == "deserter";

                if (!wasBadDischarge)
                {
                    factionTier = Math.Max(1, factionRecord.HighestTier - 2);
                }
            }

            // Return the higher of experience-based tier and faction-based tier, capped at T3.
            // Higher starting tiers require the reservist bonus system (veteran/honorable discharge).
            return Math.Min(MaxExperienceStartingTier, Math.Max(experienceTier, factionTier));
        }

        /// <summary>
        /// Returns the XP modifier for training events based on experience track.
        /// New soldiers benefit most from formal training, while veterans gain more
        /// from combat experience.
        /// 
        /// Modifiers:
        /// - Green: +20% (1.20) - Learning quickly
        /// - Seasoned: Normal (1.00) - Steady progression
        /// - Veteran: -10% (0.90) - Diminishing returns from drills
        /// </summary>
        /// <param name="hero">The hero to evaluate. Uses Hero.MainHero if null.</param>
        /// <returns>XP multiplier for training events.</returns>
        public static float GetTrainingXpModifier(Hero hero)
        {
            var track = GetExperienceTrack(hero);

            return track switch
            {
                Tracks.Green => 1.20f,
                Tracks.Seasoned => 1.00f,
                Tracks.Veteran => 0.90f,
                _ => 1.00f
            };
        }

        /// <summary>
        /// Returns a display-friendly name for the experience track.
        /// </summary>
        /// <param name="track">The track identifier.</param>
        /// <returns>Localized display name for the track.</returns>
        public static string GetTrackDisplayName(string track)
        {
            return track switch
            {
                Tracks.Green => "Green Recruit",
                Tracks.Seasoned => "Seasoned Soldier",
                Tracks.Veteran => "Veteran Fighter",
                _ => "Soldier"
            };
        }

        /// <summary>
        /// Returns a brief description of the experience track for player notification.
        /// </summary>
        /// <param name="track">The track identifier.</param>
        /// <returns>Description explaining the track's effects.</returns>
        public static string GetTrackDescription(string track)
        {
            return track switch
            {
                Tracks.Green => "You start as a raw recruit at Tier 1. Training events will be more effective.",
                Tracks.Seasoned => "Your experience earns you a starting rank of Tier 2.",
                Tracks.Veteran => "Your veteran status grants you Tier 3 and immediate respect.",
                _ => "You begin your service."
            };
        }
    }
}

