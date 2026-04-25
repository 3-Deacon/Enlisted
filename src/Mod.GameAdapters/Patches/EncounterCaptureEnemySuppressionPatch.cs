using HarmonyLib;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Suppresses the native "Capture the enemy" option in the encounter menu for enlisted soldiers.
    ///     
    ///     Native behavior: This option appears when the opposing side has no healthy troops remaining,
    ///     allowing the player to force the battle result immediately (MenuHelper.EncounterCaptureTheEnemyOnConsequence).
    ///     
    ///     In enlisted play, the player should not be making command decisions like accepting a surrender.
    ///     This also reduces confusing post-battle encounter screens if the engine re-opens "encounter"
    ///     during cleanup timing.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global",
        Justification = "Harmony patch classes discovered via reflection")]
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior),
        "game_menu_encounter_capture_the_enemy_on_condition")]
    public static class EncounterCaptureEnemySuppressionPatch
    {
    }
}

