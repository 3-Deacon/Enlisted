using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Harmony patch that redirects enlisted players to the custom enlisted_status menu
    ///     when they click "Continue" on the native army_dispersed menu.
    ///     Without this patch, the native army_dispersed_continue_on_consequence method calls
    ///     GameMenu.ExitToLast() when not in a settlement, which returns to the campaign map
    ///     without showing the enlisted status menu. This patch intercepts that behavior and
    ///     activates the enlisted menu instead for enlisted players.
    /// </summary>
    [HarmonyPatch(typeof(PlayerArmyWaitBehavior), "army_dispersed_continue_on_consequence")]
    public static class ArmyDispersedMenuPatch
    {
        /// <summary>
        ///     Prefix that runs before the native consequence handler.
        ///     For enlisted players not in a settlement, activates the enlisted menu instead.
        ///     Called by Harmony via reflection.
        /// </summary>
        /// <returns>True to run native code, false to skip it.</returns>
        [HarmonyPrefix]
        public static bool Prefix()
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;

                // If not enlisted, let native handle it normally
                if (enlistment?.IsEnlisted != true)
                {
                    return true;
                }

                // If in a settlement, let native handle it (switches to town/castle/village menu)
                if (MobileParty.MainParty?.CurrentSettlement != null)
                {
                    ModLogger.Debug("Interface",
                        "Army dispersed in settlement - letting native handle menu transition");
                    return true;
                }

                // Enlisted and NOT in settlement - show our menu instead of ExitToLast()
                ModLogger.Info("Interface", "Army dispersed - activating enlisted status menu (via SafeActivate)");
                // Use SafeActivateEnlistedMenu to respect siege detection
                EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                return false; // Skip native ExitToLast() call
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Interface", "Error in ArmyDispersedMenuPatch", ex);
                // Fail open - allow native behavior on error
                return true;
            }
        }
    }
}
