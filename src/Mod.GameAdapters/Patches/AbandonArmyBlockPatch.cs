using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Prevents enlisted soldiers from seeing the native "Abandon army" options
    /// in the army_wait, raiding_village, and siege menus. Applied during normal patching.
    /// 
    /// Note: EncounterGameMenuBehavior patches are in a separate deferred class
    /// because that class has static fields that cause TypeInitializationException
    /// if patched before the localization system is ready.
    /// </summary>
    [HarmonyPatch]
    internal static class AbandonArmyBlockPatch
    {
    }

    /// <summary>
    /// Deferred patch for join_encounter abandon option.
    /// Must be applied after campaign starts because EncounterGameMenuBehavior
    /// has static fields that call GameTexts.FindText() before localization is ready.
    /// </summary>
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_abandon_army_on_condition")]
    internal static class EncounterAbandonArmyBlockPatch
    {
        [HarmonyPrefix]
        [UsedImplicitly]
        public static bool Prefix(ref bool __result)
        {
            if (EnlistmentBehavior.Instance?.IsEnlisted == true)
            {
                __result = false;
                ModLogger.Debug("Encounter", "Hid join_encounter abandon army for enlisted player");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Deferred patch for encounter abandon option.
    /// Must be applied after campaign starts because EncounterGameMenuBehavior
    /// has static fields that call GameTexts.FindText() before localization is ready.
    /// </summary>
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_encounter_abandon_army_on_condition")]
    internal static class EncounterAbandonArmyBlockPatch2
    {
        [HarmonyPrefix]
        [UsedImplicitly]
        public static bool Prefix(ref bool __result)
        {
            if (EnlistmentBehavior.Instance?.IsEnlisted == true)
            {
                __result = false;
                ModLogger.Debug("Encounter", "Hid encounter abandon army for enlisted player");
                return false;
            }

            return true;
        }
    }
}

