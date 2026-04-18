using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Harmony patch that suppresses the "Return to Army" menu option in settlements.
    /// This option is redundant for enlisted players as the mod provides its own "Return to camp" option.
    /// </summary>
    // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), "game_menu_return_to_army_on_condition")]
    public static class ReturnToArmySuppressionPatch
    {
        /// <summary>
        /// Postfix that runs after the native condition check.
        /// Hides the "Return to Army" option if player is enlisted.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Harmony requires matching method signature")]
        private static void Postfix(ref bool __result, MenuCallbackArgs args)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return;
                }

                // Only modify if native would show the button
                if (!__result)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;

                // Hide if enlisted (regardless of leave state, as we want to use the mod's Return to Camp)
                // The user specifically said it's redundant because they have "Return to Camp".
                if (enlistment?.IsEnlisted == true)
                {
                    __result = false;
                    ModLogger.Debug("Interface", "Hiding native 'Return to Army' button - redundant for enlisted player");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Interface", "Error in ReturnToArmySuppressionPatch", ex);
                // Fail open - allow native behavior on error
            }
        }
    }
}
