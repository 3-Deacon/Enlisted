using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

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
    }
}

