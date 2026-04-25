using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Patches HeroDeveloper.AddSkillXp to:
    /// 1. Suppress tactics and leadership skill XP gain during battles for enlisted soldiers
    ///    (regular soldiers don't make tactical decisions - that's the commander's role)
    /// 2. Award enlistment XP for combat skill XP gained during battles (for rank progression)
    ///    Native Bannerlord scales combat XP with enemy tier/power, so higher tier enemies = more XP
    /// </summary>
    // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch(typeof(HeroDeveloper), nameof(HeroDeveloper.AddSkillXp), typeof(SkillObject), typeof(float), typeof(bool), typeof(bool))]
    public class SkillSuppressionPatch
    {
        // Combat skills that indicate the XP came from fighting (not campaign-layer activities)
        private static readonly string[] CombatSkills =
        {
            "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing", "Athletics"
        };

        // Track cumulative combat XP during a mission to batch the enlistment XP award
        // This prevents spamming AddEnlistmentXP for every single hit
        private static float _accumulatedCombatXp;
        private static int _lastMissionHash;
        /// <summary>
        /// Prefix that runs before AddSkillXp. Returns false to prevent tactics and leadership skill XP when player is enlisted.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Parameters required to match Harmony patch signature")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance is a special injected parameter")]
        private static bool Prefix(HeroDeveloper __instance, SkillObject skill, float rawXp)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                // Skip during character creation - use safe guard to prevent crashes
                if (!CampaignSafetyGuard.IsCampaignReady)
                {
                    return true; // Allow normal skill assignment
                }

                // Only suppress for the main hero when enlisted
                var mainHero = CampaignSafetyGuard.SafeMainHero;
                if (__instance?.Hero != mainHero || mainHero == null)
                {
                    return true; // Not the player - allow normal behavior
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true; // Not enlisted - allow normal behavior
                }

                // Suppress tactics and leadership skill XP for enlisted soldiers during battles
                // These are command/leadership skills - regular soldiers don't make tactical decisions or lead troops
                // However, allow XP from duties/professions (awarded during campaign layer, not in missions)
                if ((skill == DefaultSkills.Tactics || skill == DefaultSkills.Leadership) && rawXp > 0f)
                {
                    // Check if we're in a battle mission - if so, suppress
                    // If Mission.Current is null, we're in campaign layer (duties, etc.) - allow XP
                    if (Mission.Current != null)
                    {
                        // Suppress command skill XP during battles - return false to skip original method
                        ModLogger.LogOnce("skill_suppression_active", "XP", $"Suppressing {skill.Name} XP during battles - enlisted soldiers don't gain command skills from combat");
                        return false;
                    }
                    // Not in a mission - this is likely from duties/professions, allow it
                }

                // Allow all other skills to gain XP normally
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SkillSuppression", "Error in skill suppression patch", ex);
                return true; // Fail open - allow normal behavior on error
            }
        }

        /// <summary>
        /// Postfix that tracks combat skill XP for enlistment progression.
        /// Native Bannerlord already scales combat XP based on enemy tier/power:
        ///   baseXP = 0.4 × (attackerPower + 0.5) × (victimPower + 0.5) × damage × multiplier
        /// Higher tier enemies have higher victimPower, so killing them grants more XP.
        /// We accumulate this XP during battle and award it as enlistment XP for rank progression.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Parameters required to match Harmony patch signature")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance is a special injected parameter")]
        private static void Postfix(HeroDeveloper __instance, SkillObject skill, float rawXp)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return;
                }

                // Must be in an active mission (battle) for this to be combat XP
                if (Mission.Current == null)
                {
                    return; // Campaign layer XP - already handled by orders/events/camp
                }

                // Skip during character creation
                if (!CampaignSafetyGuard.IsCampaignReady)
                {
                    return;
                }

                // Only track for the main hero
                var mainHero = CampaignSafetyGuard.SafeMainHero;
                if (__instance?.Hero != mainHero || mainHero == null)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                // Only track combat skills (weapon skills + athletics from unarmed)
                bool isCombatSkill = false;
                foreach (var combatSkill in CombatSkills)
                {
                    if (skill.StringId.Equals(combatSkill, StringComparison.OrdinalIgnoreCase))
                    {
                        isCombatSkill = true;
                        break;
                    }
                }

                if (!isCombatSkill || rawXp <= 0f)
                {
                    return;
                }

                // Track accumulated XP for this mission
                // Reset accumulator if this is a new mission
                int currentMissionHash = Mission.Current.GetHashCode();
                if (currentMissionHash != _lastMissionHash)
                {
                    // Flush any remaining XP from previous mission before resetting
                    if (_accumulatedCombatXp >= 1f)
                    {
                        int xpToAward = (int)_accumulatedCombatXp;
                        enlistment.AddEnlistmentXP(xpToAward, "Combat");
                        ModLogger.Debug("CombatXP", $"Flushed {xpToAward} accumulated combat XP from previous mission");
                    }
                    _accumulatedCombatXp = 0f;
                    _lastMissionHash = currentMissionHash;
                }

                // Accumulate the raw XP (native formula already scaled by enemy tier)
                _accumulatedCombatXp += rawXp;

                // Award in batches of 10+ to reduce log spam and performance overhead
                if (_accumulatedCombatXp >= 10f)
                {
                    int xpToAward = (int)_accumulatedCombatXp;
                    enlistment.AddEnlistmentXP(xpToAward, "Combat");
                    ModLogger.Debug("CombatXP", $"Awarded {xpToAward} enlistment XP from combat (native tier-scaled)");
                    _accumulatedCombatXp -= xpToAward; // Keep fractional remainder
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CombatXP", "Error tracking combat XP for enlistment", ex);
            }
        }

        /// <summary>
        /// Called when a battle ends to flush any remaining accumulated combat XP.
        /// Should be invoked by EnlistmentBehavior.OnPlayerBattleEnd().
        /// </summary>
        public static void FlushAccumulatedCombatXP()
        {
            try
            {
                if (_accumulatedCombatXp >= 1f)
                {
                    var enlistment = EnlistmentBehavior.Instance;
                    if (enlistment?.IsEnlisted == true)
                    {
                        int xpToAward = (int)_accumulatedCombatXp;
                        enlistment.AddEnlistmentXP(xpToAward, "Combat");
                        ModLogger.Debug("CombatXP", $"Flushed {xpToAward} remaining combat XP at battle end");
                    }
                }
                _accumulatedCombatXp = 0f;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CombatXP", "Error flushing accumulated combat XP", ex);
            }
        }
    }
}

