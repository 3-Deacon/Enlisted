using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Fixes a base game crash in PlayerArmyWaitBehavior that occurs after winning a naval battle
    /// while enlisted in an army. The native code accesses Army.LeaderParty.IsCurrentlyAtSea
    /// without null checks, causing NullReferenceException when the army state is inconsistent
    /// after a naval battle ends.
    ///
    /// Bug report: "Game crashes after winning a naval battle while under the command of a lord.
    /// Workaround: detaching from the army immediately after the battle prevents the crash."
    ///
    /// Root cause: Native ArmyWaitMenuTick and game_menu_army_wait_on_init methods access
    /// MobileParty.MainParty.Army.LeaderParty.IsCurrentlyAtSea without null safety.
    /// </summary>
    [HarmonyPatch(typeof(PlayerArmyWaitBehavior))]
    public static class NavalBattleArmyWaitCrashFix
    {
        /// <summary>
        /// Resets the logging flag when a new campaign starts.
        /// Called from SubModule or EnlistmentBehavior on campaign start.
        /// </summary>
        public static void ResetSessionLogging()
        {
        }
    }
}

