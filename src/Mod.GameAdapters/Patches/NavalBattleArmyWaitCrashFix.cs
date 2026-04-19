using System;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

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
        private static bool _hasLoggedFix;

        /// <summary>
        /// Patches ArmyWaitMenuTick to add null safety for Army.LeaderParty access.
        /// This method is called continuously while the player is in the army_wait menu.
        /// The native code at the end accesses Army.LeaderParty.IsCurrentlyAtSea without null checks.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("ArmyWaitMenuTick")]
        private static bool ArmyWaitMenuTick_Prefix(MenuCallbackArgs args)
        {
            try
            {
                var mainParty = MobileParty.MainParty;
                if (mainParty == null)
                {
                    // No main party - let the menu exit gracefully
                    LogCrashPrevention("ArmyWaitMenuTick", "MainParty is null");
                    SafeExitArmyWaitMenu(args);
                    return false;
                }

                var army = mainParty.Army;
                if (army == null)
                {
                    // Player not in an army anymore - exit the army_wait menu safely
                    LogCrashPrevention("ArmyWaitMenuTick", "Army is null");
                    SafeExitArmyWaitMenu(args);
                    return false;
                }

                var leaderParty = army.LeaderParty;
                if (leaderParty == null)
                {
                    // Army has no leader party - this is the naval battle crash scenario
                    // The army may have been partially disbanded or the leader captured
                    LogCrashPrevention("ArmyWaitMenuTick", "Army.LeaderParty is null (likely post-naval battle state)");

                    // Clean up the invalid army reference to prevent repeated crashes
                    try
                    {
                        mainParty.Army = null;
                        ModLogger.Info("Naval", "Cleaned up invalid army reference to prevent crash loop");
                    }
                    catch (Exception cleanupEx)
                    {
                        ModLogger.Caught("Naval", "Failed to clean up army reference", cleanupEx);
                    }

                    SafeExitArmyWaitMenu(args);
                    return false;
                }

                // All null checks passed - allow native method to run
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Error in ArmyWaitMenuTick_Prefix", ex);
                // Fail safe - try to exit the menu to prevent crash loop
                SafeExitArmyWaitMenu(args);
                return false;
            }
        }

        /// <summary>
        /// Patches game_menu_army_wait_on_init to add null safety.
        /// This initialization handler also accesses Army.LeaderParty.IsCurrentlyAtSea.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("game_menu_army_wait_on_init")]
        private static bool ArmyWaitOnInit_Prefix(MenuCallbackArgs args)
        {
            try
            {
                var mainParty = MobileParty.MainParty;
                if (mainParty?.Army?.LeaderParty == null)
                {
                    LogCrashPrevention("game_menu_army_wait_on_init",
                        "Army or LeaderParty is null during menu init");

                    // Set a fallback background to prevent further issues
                    try
                    {
                        args.MenuContext?.SetBackgroundMeshName("wait_fallback");
                    }
                    catch
                    {
                        // Ignore background setting errors
                    }

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Error in ArmyWaitOnInit_Prefix", ex);
                return false;
            }
        }

        /// <summary>
        /// Patches wait_menu_army_wait_on_init to add null safety for text variable setting.
        /// Native code accesses Army.LeaderParty.LeaderHero.Name without null checks.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("wait_menu_army_wait_on_init")]
        private static bool WaitMenuArmyWaitOnInit_Prefix(MenuCallbackArgs args)
        {
            try
            {
                var mainParty = MobileParty.MainParty;
                if (mainParty == null)
                {
                    return false;
                }

                var army = mainParty.Army;
                if (army?.LeaderParty?.LeaderHero == null)
                {
                    LogCrashPrevention("wait_menu_army_wait_on_init",
                        "Army, LeaderParty, or LeaderHero is null");

                    // The native code will crash trying to set text variables
                    // Skip native and the menu will show with default/missing text
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Error in WaitMenuArmyWaitOnInit_Prefix", ex);
                return false;
            }
        }

        /// <summary>
        /// Safely exits the army_wait menu by checking for a valid menu to switch to
        /// or exiting to the campaign map.
        /// </summary>
        private static void SafeExitArmyWaitMenu(MenuCallbackArgs args)
        {
            try
            {
                // End the wait menu if it's active
                args?.MenuContext?.GameMenu?.EndWait();

                // Try to get the appropriate menu from the game's encounter model
                var genericStateMenu = Campaign.Current?.Models?.EncounterGameMenuModel?.GetGenericStateMenu();

                if (!string.IsNullOrEmpty(genericStateMenu) && genericStateMenu != "army_wait")
                {
                    GameMenu.SwitchToMenu(genericStateMenu);
                    ModLogger.Debug("Naval", $"Safely switched to menu: {genericStateMenu}");
                }
                else
                {
                    // No specific menu - exit to previous menu or campaign map
                    GameMenu.ExitToLast();
                    ModLogger.Debug("Naval", "Safely exited army_wait menu to previous state");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Naval", "Error in SafeExitArmyWaitMenu", ex);

                // Last resort - try to exit to campaign map
                try
                {
                    GameMenu.ExitToLast();
                }
                catch
                {
                    // Completely failed - the game may be in a bad state
                }
            }
        }

        /// <summary>
        /// Logs crash prevention details (only once per session to avoid log spam).
        /// </summary>
        private static void LogCrashPrevention(string method, string reason)
        {
            if (!_hasLoggedFix)
            {
                _hasLoggedFix = true;
                ModLogger.Info("Naval",
                    $"CRASH PREVENTED in {method}: {reason}. " +
                    "This typically occurs after a naval battle when army state is inconsistent. " +
                    "The fix safely exits the army_wait menu instead of crashing.");
            }
            else
            {
                ModLogger.Debug("Naval", $"Crash prevented in {method}: {reason}");
            }
        }

        /// <summary>
        /// Resets the logging flag when a new campaign starts.
        /// Called from SubModule or EnlistmentBehavior on campaign start.
        /// </summary>
        public static void ResetSessionLogging()
        {
            _hasLoggedFix = false;
        }
    }
}

