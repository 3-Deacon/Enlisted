using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

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
    }
}
