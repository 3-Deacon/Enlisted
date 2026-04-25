using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Hides the "Leave..." option from the encounter menu when the player is enlisted.
    /// Enlisted soldiers cannot abandon their lord's battles - they must fight or wait in reserve.
    /// </summary>
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_encounter_leave_on_condition")]
    public static class EncounterLeaveSuppressionPatch
    {
        /// <summary>
        /// Prefix that hides the "Leave..." option for enlisted players.
        /// Returns false to override native condition and hide the option.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted == true)
            {
                __result = false;
                ModLogger.Debug("Encounter", "Hid encounter leave option for enlisted player");
                return false;
            }

            // Not enlisted - let native condition determine visibility
            return true;
        }
    }
}

