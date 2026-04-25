using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;

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
    }
}
