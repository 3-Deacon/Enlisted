using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Harmony patch that guards against a vanilla null reference bug in CheckFortificationAttackableHonorably.
    /// 
    /// The vanilla method assumes PlayerEncounter.EncounterSettlement is always set when the town/castle
    /// menu is displayed, but our mod sometimes finishes PlayerEncounter on settlement entry to prevent
    /// InsideSettlement assertion failures. This creates a brief window where the menu refreshes but
    /// EncounterSettlement is null, causing a NullReferenceException when vanilla tries to access
    /// EncounterSettlement.MapFaction.
    /// 
    /// This patch safely disables the besiege option and skips the vanilla method when no settlement
    /// context exists, preventing the crash while maintaining correct menu behavior.
    /// </summary>
    [HarmonyPatch(typeof(EncounterGameMenuBehavior), "CheckFortificationAttackableHonorably")]
    public static class CheckFortificationAttackablePatch
    {
    }
}

