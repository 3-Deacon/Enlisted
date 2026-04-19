using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

// ReSharper disable UnusedType.Global - Harmony patches are applied via attributes, not direct code references
// ReSharper disable UnusedMember.Local - Harmony Prefix/Postfix methods are invoked via reflection
// ReSharper disable InconsistentNaming - __instance is a Harmony naming convention
namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Suppresses the "take prisoner" conversation that appears after defeating enemy lords when enlisted.
    ///     
    ///     As an enlisted soldier, the player has no authority to capture or negotiate with defeated lords.
    ///     Captured lords automatically go to the commanding lord's party, not the player's party.
    ///     This patch blocks the DoCaptureHeroes conversation from appearing.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch classes discovered via reflection")]
    [HarmonyPatch(typeof(PlayerEncounter), "DoCaptureHeroes")]
    public static class CapturedLordConversationSuppressionPatch
    {
        /// <summary>
        ///     Checks if the player is enlisted and should be blocked from lord capture conversations.
        /// </summary>
        private static bool ShouldBlockCaptureConversation()
        {
            // If the mod is not active (e.g., not enlisted playthrough), skip entirely
            if (!EnlistedActivation.IsActive)
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return false; // Not enlisted - allow normal conversations
            }

            // Allow capture actions when on leave or in grace period - player is operating independently
            if (enlistment.IsOnLeave || enlistment.IsInDesertionGracePeriod)
            {
                return false;
            }

            return true; // Actively serving - block capture conversations
        }

        /// <summary>
        ///     Intercepts DoCaptureHeroes to skip the conversation when enlisted.
        ///     Returns false to skip original method, preventing the capture conversation popup.
        ///     
        ///     When enlisted, defeated lords are not captured by the player - they go to the
        ///     commanding lord's party instead (handled by LootBlockPatch). This patch just
        ///     skips the conversation entirely and advances to the next encounter state.
        /// </summary>
        [HarmonyPrefix]
        private static bool Prefix(PlayerEncounter __instance)
        {
            try
            {
                if (!ShouldBlockCaptureConversation())
                {
                    return true; // Let original method run
                }

                // Log suppression (we don't need to check if there are captured heroes -
                // if we got here, the game thinks there are, so just skip the conversation)
                ModLogger.Debug("CaptureConversation", 
                    "Suppressed lord capture conversation - enlisted soldiers cannot negotiate with defeated lords");

                // Advance to FreeHeroes state (next state after CaptureHeroes)
                var stateProperty = AccessTools.Property(typeof(PlayerEncounter), "EncounterState");
                if (stateProperty != null)
                {
                    stateProperty.SetValue(__instance, PlayerEncounterState.FreeHeroes);
                }
                else
                {
                    ModLogger.Error("CaptureConversation", "Failed to find EncounterState property");
                }

                // Set _stateHandled to true to indicate we handled this state
                var stateHandledField = AccessTools.Field(typeof(PlayerEncounter), "_stateHandled");
                if (stateHandledField != null)
                {
                    stateHandledField.SetValue(__instance, true);
                }
                else
                {
                    ModLogger.Error("CaptureConversation", "Failed to find _stateHandled field");
                }

                return false; // Skip original method
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CaptureConversation", "Error in captured lord conversation suppression patch", ex);
                return true; // Fail open - allow normal behavior
            }
        }
    }
}
