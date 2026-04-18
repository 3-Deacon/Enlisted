using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameMenus;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

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
        [HarmonyPostfix]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming",
            Justification = "Harmony convention: __result is a special injected parameter")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local",
            Justification = "Harmony requires matching method signature")]
        private static void Postfix(MenuCallbackArgs args, ref bool __result)
        {
            try
            {
                if (!__result)
                {
                    return;
                }

                if (!EnlistedActivation.IsActive)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                // Allow normal choices when operating independently.
                if (enlistment.IsOnLeave || enlistment.IsInDesertionGracePeriod)
                {
                    return;
                }

                __result = false;
                ModLogger.Debug("Encounter",
                    "Suppressed 'Capture the enemy' option - enlisted soldiers do not decide surrender/capture outcomes");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Encounter", "Error in EncounterCaptureEnemySuppressionPatch", ex);
            }
        }
    }
}

