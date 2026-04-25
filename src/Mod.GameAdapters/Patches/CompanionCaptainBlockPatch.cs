using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Retinue.Core;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Prevents player companions from being assigned as formation captains during enlistment.
    ///     
    ///     During enlistment, the player is a soldier in their lord's army - not a commander.
    ///     Their companions should fight alongside them (or stay back), but never command
    ///     formations. The vanilla game assigns heroes as captains based on power level,
    ///     which would make companions issue orders and break the enlisted soldier experience.
    ///     
    ///     This patch intercepts the Formation.Captain setter directly to catch ALL captain
    ///     assignments, regardless of which code path triggers them. This is more comprehensive
    ///     than patching GeneralsAndCaptainsAssignmentLogic.OnCaptainAssignedToFormation, which
    ///     may not catch all assignment paths.
    /// </summary>
    [HarmonyPatch(typeof(Formation), nameof(Formation.Captain), MethodType.Setter)]
    public class CompanionCaptainBlockPatch
    {
        private const string LogCategory = "CompanionCommand";

        /// <summary>
        ///     Prefix that blocks captain assignment for all player companions during enlistment.
        ///     If the value being assigned is a player companion, we set it to null instead.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(Formation __instance, ref Agent value)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                // Skip if value is null (clearing captain is always allowed)
                if (value == null)
                {
                    return true;
                }

                // Only check when enlisted at Commander tier (T7+) (when companions/retinue participate in battles)
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true || enlistment.EnlistmentTier < RetinueManager.CommanderTier1)
                {
                    return true; // Not enlisted or below tier - run vanilla
                }

                // Check if this agent is a hero
                if (value.Character is not CharacterObject charObj || !charObj.IsHero)
                {
                    return true; // Not a hero - run vanilla
                }

                var hero = charObj.HeroObject;
                if (hero == null)
                {
                    return true; // No hero object - run vanilla
                }

                // Block ALL player companions from becoming captains during enlistment
                // The player is a soldier in the lord's army - companions shouldn't command
                if (hero.IsPlayerCompanion)
                {
                    ModLogger.Debug(LogCategory,
                        $"Blocked captain: {hero.Name} -> {__instance?.FormationIndex} | Companions cannot command formations while enlisted");

                    // Set value to null to prevent companion from becoming captain
                    // The setter will then early-return if current captain is already null
                    value = null;
                    return true; // Continue with null value
                }

                return true; // Not a player companion - run vanilla (lords, other heroes can be captains)
            }
            catch (Exception ex)
            {
                // On error, fail open to vanilla behavior
                ModLogger.Caught("CompanionCommand", "Error checking captain assignment", ex);
                return true;
            }
        }
    }

    /// <summary>
    ///     Prevents player companions from being assigned as the Team's GeneralAgent during enlistment.
    ///     
    ///     The vanilla game picks the highest-power hero (excluding the main agent) as GeneralAgent.
    ///     If the player's companion has high power, they could be selected as the "general" and
    ///     announce tactical plans like "Our plan is to shower them with arrows..." which breaks
    ///     the enlisted soldier experience. The lord should be the general, not the companion.
    /// </summary>
    [HarmonyPatch(typeof(Team), nameof(Team.GeneralAgent), MethodType.Setter)]
    public class CompanionGeneralBlockPatch
    {
        private const string LogCategory = "CompanionCommand";

        /// <summary>
        ///     Prefix that blocks general assignment for all player companions during enlistment.
        ///     If the value being assigned is a player companion, we skip the original setter.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(Team __instance, ref Agent value)
        {
            try
            {
                // Skip if value is null (clearing general is always allowed)
                if (value == null)
                {
                    return true;
                }

                // Only check when enlisted at Commander tier (T7+) (when companions/retinue participate in battles)
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true || enlistment.EnlistmentTier < RetinueManager.CommanderTier1)
                {
                    return true; // Not enlisted or below tier - run vanilla
                }

                // Check if this agent is a hero
                if (value.Character is not CharacterObject charObj || !charObj.IsHero)
                {
                    return true; // Not a hero - run vanilla
                }

                var hero = charObj.HeroObject;
                if (hero == null)
                {
                    return true; // No hero object - run vanilla
                }

                // Block ALL player companions from becoming the general during enlistment
                // The lord should be the general, not the player's companion
                if (hero.IsPlayerCompanion)
                {
                    ModLogger.Debug(LogCategory,
                        $"Blocked general: {hero.Name} -> Team({__instance?.Side}) | Companions cannot be army general while enlisted");

                    // Set value to null to prevent companion from becoming general
                    value = null;
                    return true; // Continue with null value - another hero will be picked
                }

                return true; // Not a player companion - run vanilla (lords should be generals)
            }
            catch (Exception ex)
            {
                // On error, fail open to vanilla behavior
                ModLogger.Caught("CompanionCommand", "Error checking general assignment", ex);
                return true;
            }
        }
    }
}

