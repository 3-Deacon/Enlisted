using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Harmony patch that hides the native "Leave" button in town/castle menus when the player
    /// is actively enlisted. This prevents accidental departure from settlements.
    /// The button remains visible during grace periods or when on temporary leave.
    /// </summary>
    // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), "game_menu_town_town_leave_on_condition")]
    public static class TownLeaveButtonPatch
    {
        /// <summary>
        /// Postfix that runs after the native condition check.
        /// Hides the Leave button if player is actively enlisted (not on leave, not in grace period).
        /// This covers "town", "castle", and "village" main menus.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Harmony requires matching method signature")]
        private static void Postfix(ref bool __result, MenuCallbackArgs args)
        {
            try
            {
                // Only modify if native would show the button
                if (!__result)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                
                // Hide if actively enlisted (not on leave, not in grace period)
                if (enlistment?.IsEnlisted == true && 
                    !enlistment.IsOnLeave && 
                    !enlistment.IsInDesertionGracePeriod)
                {
                    __result = false;
                    ModLogger.Debug("TownLeave", "Hiding native Leave button (town/castle/village menu) - player is actively enlisted");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TownLeave", "Error in TownLeaveButtonPatch", ex);
                // Fail open - allow native behavior on error
            }
        }
    }
    
    /// <summary>
    /// Additional patch for the "outside" settlement menus (castle_outside, town_outside).
    /// These use a different condition method from EncounterGameMenuBehavior.
    /// </summary>
    // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_leave_on_condition")]
    public static class SettlementOutsideLeaveButtonPatch
    {
        /// <summary>
        /// Postfix that hides the Leave button in outside settlement menus when enlisted.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Harmony requires matching method signature")]
        private static void Postfix(ref bool __result, MenuCallbackArgs args)
        {
            try
            {
                // Only modify if native would show the button
                if (!__result)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                
                // Hide if actively enlisted (not on leave, not in grace period)
                if (enlistment?.IsEnlisted == true && 
                    !enlistment.IsOnLeave && 
                    !enlistment.IsInDesertionGracePeriod)
                {
                    __result = false;
                    ModLogger.Debug("TownLeave", "Hiding native Leave button (outside menu) - player is actively enlisted");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TownLeave", "Error in SettlementOutsideLeaveButtonPatch", ex);
                // Fail open - allow native behavior on error
            }
        }
    }
}

