using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace Enlisted.Features.Identity
{
    /// <summary>
    ///     Manages the enlisted soldier's role and status display based on traits and skills.
    ///     Determines primary role from trait levels using a priority hierarchy:
    ///     Commander > Scout > Medic > Engineer > Operative > NCO > Soldier.
    ///     Provides formatted descriptions combining trait levels and skill values for UI display.
    /// </summary>
    public class EnlistedStatusManager : CampaignBehaviorBase
    {
        public static EnlistedStatusManager Instance { get; private set; }

        public EnlistedStatusManager()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            // No events needed - this is a utility behavior for status queries
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent state to sync
        }

        /// <summary>
        ///     Determines the hero's primary role based on trait levels.
        ///     Uses a priority hierarchy where higher-authority roles take precedence.
        ///     Role thresholds: Commander 10+, Specialists 10+, NCO 8+, default Soldier.
        /// </summary>
        /// <returns>Role name: Officer, Scout, Medic, Engineer, Operative, NCO, or Soldier</returns>
        public string GetPrimaryRole()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return "Soldier";
                }

                // Priority order: Commander > Specialist > NCO > Default
                if (hero.GetTraitLevel(DefaultTraits.Commander) >= 10)
                {
                    return "Officer";
                }

                if (hero.GetTraitLevel(DefaultTraits.ScoutSkills) >= 10)
                {
                    return "Scout";
                }

                if (hero.GetTraitLevel(DefaultTraits.Surgery) >= 10)
                {
                    return "Medic";
                }

                if (hero.GetTraitLevel(DefaultTraits.Siegecraft) >= 10)
                {
                    return "Engineer";
                }

                if (hero.GetTraitLevel(DefaultTraits.RogueSkills) >= 10)
                {
                    return "Operative";
                }

                if (hero.GetTraitLevel(DefaultTraits.SergeantCommandSkills) >= 8)
                {
                    return "NCO";
                }

                return "Soldier"; // Default for unspecialized troops
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Identity", "Failed to determine primary role", ex);
                return "Soldier";
            }
        }

        /// <summary>
        ///     Generates a formatted description of the hero's role including trait level and skill value.
        ///     Format: "[Role] [TraitLevel], [SkillName] [SkillValue]"
        ///     Example: "Scout 12, Scouting 85"
        /// </summary>
        /// <returns>Formatted role description string</returns>
        public string GetRoleDescription()
        {
            try
            {
                var role = GetPrimaryRole();
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return "Enlisted Soldier";
                }

                return role switch
                {
                    "Officer" => $"Commander {hero.GetTraitLevel(DefaultTraits.Commander)}, Leadership {hero.GetSkillValue(DefaultSkills.Leadership)}",
                    "Scout" => $"Scout {hero.GetTraitLevel(DefaultTraits.ScoutSkills)}, Scouting {hero.GetSkillValue(DefaultSkills.Scouting)}",
                    "Medic" => $"Surgeon {hero.GetTraitLevel(DefaultTraits.Surgery)}, Medicine {hero.GetSkillValue(DefaultSkills.Medicine)}",
                    "Engineer" => $"Engineer {hero.GetTraitLevel(DefaultTraits.Siegecraft)}, Engineering {hero.GetSkillValue(DefaultSkills.Engineering)}",
                    "Operative" => $"Rogue {hero.GetTraitLevel(DefaultTraits.RogueSkills)}, Roguery {hero.GetSkillValue(DefaultSkills.Roguery)}",
                    "NCO" => $"Sergeant {hero.GetTraitLevel(DefaultTraits.SergeantCommandSkills)}, Leadership {hero.GetSkillValue(DefaultSkills.Leadership)}",
                    _ => "Enlisted Soldier"
                };
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Identity", "Failed to generate role description", ex);
                return "Enlisted Soldier";
            }
        }

        /// <summary>
        ///     Gets all specializations with their trait levels for detailed display.
        ///     Returns a formatted multi-line string showing each specialization and its current level.
        /// </summary>
        /// <returns>Formatted string with all specializations and levels</returns>
        public string GetAllSpecializations()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return "No specialization data available.";
                }

                var lines = new System.Text.StringBuilder();
                
                var commanderLevel = hero.GetTraitLevel(DefaultTraits.Commander);
                var scoutLevel = hero.GetTraitLevel(DefaultTraits.ScoutSkills);
                var surgeonLevel = hero.GetTraitLevel(DefaultTraits.Surgery);
                var engineerLevel = hero.GetTraitLevel(DefaultTraits.Siegecraft);
                var rogueLevel = hero.GetTraitLevel(DefaultTraits.RogueSkills);
                var sergeantLevel = hero.GetTraitLevel(DefaultTraits.SergeantCommandSkills);

                if (commanderLevel > 0)
                {
                    lines.AppendLine($"Commander: {commanderLevel} (Leadership {hero.GetSkillValue(DefaultSkills.Leadership)})");
                }

                if (scoutLevel > 0)
                {
                    lines.AppendLine($"Scout: {scoutLevel} (Scouting {hero.GetSkillValue(DefaultSkills.Scouting)})");
                }

                if (surgeonLevel > 0)
                {
                    lines.AppendLine($"Surgeon: {surgeonLevel} (Medicine {hero.GetSkillValue(DefaultSkills.Medicine)})");
                }

                if (engineerLevel > 0)
                {
                    lines.AppendLine($"Engineer: {engineerLevel} (Engineering {hero.GetSkillValue(DefaultSkills.Engineering)})");
                }

                if (rogueLevel > 0)
                {
                    lines.AppendLine($"Rogue: {rogueLevel} (Roguery {hero.GetSkillValue(DefaultSkills.Roguery)})");
                }

                if (sergeantLevel > 0)
                {
                    lines.AppendLine($"Sergeant: {sergeantLevel} (Leadership {hero.GetSkillValue(DefaultSkills.Leadership)})");
                }

                if (lines.Length == 0)
                {
                    return "No specializations developed yet.";
                }

                return lines.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Identity", "Failed to get all specializations", ex);
                return "Specialization data unavailable.";
            }
        }
    }
}

