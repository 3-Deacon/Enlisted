using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Suppresses the "gained 0 influence" message that appears after battles when enlisted.
    /// The native game shows this message to all party leaders in a battle, but as an enlisted
    /// soldier we shouldn't be receiving influence notifications at all.
    /// 
    /// The native code displays the message regardless of whether influence > 0, so we patch
    /// to skip the method entirely when the player is enlisted.
    /// </summary>
    [HarmonyPatch(typeof(GainKingdomInfluenceAction), nameof(GainKingdomInfluenceAction.ApplyForBattle))]
    public static class InfluenceMessageSuppressionPatch
    {
        /// <summary>
        /// Skip influence notification when player is enlisted as a soldier.
        /// Soldiers don't gain political influence from battles - that goes to their lord.
        /// Called by Harmony via reflection.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(Hero hero, float value)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                // Only intercept if this is about the player
                if (hero != Hero.MainHero)
                {
                    return true; // Continue normally for other heroes
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted == true)
                {
                    // Player is enlisted - skip influence gain/message entirely
                    // As a soldier, influence goes to the lord, not to us
                    ModLogger.Debug("Influence", $"Suppressed influence notification (value: {value}) - player is enlisted");
                    return false; // Skip original method
                }

                // Not enlisted - allow normal behavior
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Influence", "Error in influence suppression patch", ex);
                return true; // Fail open - allow normal behavior
            }
        }
    }
}

