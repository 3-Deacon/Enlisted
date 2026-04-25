using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
// Removed: using Enlisted.Features.Camp.UI.Bulletin; (old Bulletin UI deleted)
using Enlisted.Debugging.Behaviors;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.Camp;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Companions;
using Enlisted.Features.Content;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Equipment.Managers;
using Enlisted.Features.Logistics;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Entry;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
// 1.3.4 API: ImageIdentifier moved here

namespace Enlisted.Features.Interface.Behaviors
{
    /// <summary>
    ///     Menu system for enlisted military service providing comprehensive status display,
    ///     interactive duty management, and professional military interface.
    ///     This system provides rich, real-time information about military service status including
    ///     detailed progression tracking, army information, duties management, and service records.
    ///     Handles menu creation, state management, and integration with the native game menu system.
    /// </summary>
    public sealed class EnlistedMenuBehavior : CampaignBehaviorBase
    {
        private const string CampHubMenuId = "enlisted_camp_hub";

        /// <summary>
        ///     Minimum time interval between menu updates, in seconds.
        ///     Updates are limited to once per second to provide real-time feel
        ///     without overwhelming the system with too-frequent refreshes.
        /// </summary>
        private readonly float _updateIntervalSeconds = 5.0f;

        /// <summary>
        ///     Currently active menu ID string.
        ///     Used to track which menu is currently open and determine when to refresh.
        /// </summary>
        private string _currentMenuId = "";

        /// <summary>
        ///     Last campaign time when the menu was updated.
        ///     Used to throttle menu updates to once per second.
        /// </summary>
        private CampaignTime _lastMenuUpdate = CampaignTime.Zero;

        /// <summary>
        ///     Whether the menu needs to be refreshed due to state changes.
        ///     Set to true when enlistment state, duties, or other menu-affecting data changes.
        /// </summary>
        private bool _menuNeedsRefresh;

        /// <summary>
        ///     Tracks if there's a pending return to the enlisted menu after settlement exit.
        ///     Used to defer menu activation until after settlement exit completes.
        /// </summary>
        private bool _pendingReturnToEnlistedMenu;

        /// <summary>
        ///     Campaign time when the player left a settlement.
        ///     Used to delay menu activation after settlement exit to prevent timing conflicts.
        /// </summary>
        private CampaignTime _settlementExitTime = CampaignTime.Zero;


        /// <summary>
        ///     Tracks whether we created a synthetic outside encounter for settlement access.
        ///     Used to clean up encounter state when leaving settlements.
        /// </summary>
        private bool _syntheticOutsideEncounter;

        /// <summary>
        ///     Public accessor for checking if the player has explicitly visited a settlement.
        ///     Used by GenericStateMenuPatch to prevent auto-opening settlement menus when just paused at a settlement.
        /// </summary>
        public static bool HasExplicitlyVisitedSettlement { get; private set; }

        // Orders accordion state for the main enlisted status menu.
        // We keep this simple: one active order can be expanded/collapsed via a header entry.
        // When a new order arrives, the section auto-expands.
        private bool _ordersCollapsed = true;
        private string _ordersLastSeenOrderId = string.Empty;

        // Headlines drilldown state — session-scoped; not persisted (DispatchItem is a struct).
        private readonly HashSet<string> _viewedHeadlineStoryKeys = new HashSet<string>(StringComparer.Ordinal);

        // Cached narrative text to prevent flickering from frequent rebuilds.
        // Only updates when the underlying data changes.
        private string _cachedMainMenuNarrative = string.Empty;
        private CampaignTime _narrativeLastBuiltAt = CampaignTime.Zero;
        private int _lastKnownSupplyLevel = -1;

        // State tracking for smooth past/present tense transitions (static since EnlistedMenuBehavior is singleton)
        private static Settlement _lastKnownSettlement = null;
        private static CampaignTime _lastSettlementChangeTime = CampaignTime.Zero;
        private static bool _lastKnownInArmy = false;
        private static CampaignTime _lastArmyChangeTime = CampaignTime.Zero;

        /// <summary>
        ///     Last time we logged an enlisted menu activation, used to avoid log spam
        ///     when activation is retried in quick succession.
        /// </summary>
        private static CampaignTime _lastEnlistedMenuActivationLogTime = CampaignTime.Zero;

        private const float EnlistedMenuActivationLogCooldownSeconds = 1.0f;

        public EnlistedMenuBehavior()
        {
            Instance = this;
        }

        public static EnlistedMenuBehavior Instance { get; private set; }

        /// <summary>
        ///     Helper method to check if a party is in battle or siege.
        ///     This prevents PlayerSiege assertion failures by ensuring we don't finish encounters during sieges.
        /// </summary>
        private static bool InBattleOrSiege(MobileParty party)
        {
            return party?.Party.MapEvent != null || party?.Party.SiegeEvent != null ||
                   party?.BesiegedSettlement != null;
        }

        // Note: ContainsParty helper removed - was unused utility method for battle side detection

        /// <summary>
        ///     Hourly tick handler that runs once per in-game hour for battle detection.
        ///     Checks if the lord is in battle and exits custom menus to allow native battle menus to appear.
        ///     Battle detection is handled in hourly ticks rather than real-time ticks to avoid
        ///     overwhelming the system with constant checks and to prevent assertion failures.
        /// </summary>
        private void OnHourlyTick()
        {
            if (!EnlistedActivation.EnsureActive())
            {
                return;
            }

            // Skip all processing if the player is not currently enlisted
            // This avoids unnecessary computation when the system isn't active
            var enlistmentBehavior = EnlistmentBehavior.Instance;
            if (enlistmentBehavior?.IsEnlisted != true)
            {
                return;
            }

            // Check if the lord's party is in battle or siege
            var lord = enlistmentBehavior.CurrentLord;
            var lordParty = lord?.PartyBelongedTo;

            if (lordParty != null)
            {
                // Check battle state for both the lord individually and siege-related battles
                // The MapEvent and SiegeEvent properties exist on Party, not directly on MobileParty
                // This is the correct API structure for checking battle state
                var lordInBattle = lordParty.Party.MapEvent != null;
                var siegeRelatedBattle = IsSiegeRelatedBattle(MobileParty.MainParty, lordParty);

                // Only treat actual battles (MapEvent) as "battle" for menu switching.
                // During siege waiting (lordParty.Party.SiegeEvent != null but no MapEvent), switching into
                // menu_siege_strategies is disruptive for enlisted soldiers and can re-trigger unwanted
                // siege screens. Siege assault still creates a MapEvent and will be handled by lordInBattle.
                var lordInAnyBattle = lordInBattle || siegeRelatedBattle;

                if (lordInAnyBattle)
                {
                    // Exit custom menus to allow the native system to show appropriate battle menus
                    // The native system will show army_wait, menu_siege_strategies, or other battle menus
                    // CRITICAL: Only exit if we have a valid menu context and it's our custom menu
                    if (_currentMenuId.StartsWith("enlisted_") && Campaign.Current?.CurrentMenuContext != null)
                    {
                        try
                        {
                            // Try to let the native encounter system take over
                            var desiredMenu = Campaign.Current.Models?.EncounterGameMenuModel?.GetGenericStateMenu();
                            if (!string.IsNullOrEmpty(desiredMenu))
                            {
                                // Never force enlisted players into the commander siege strategy menu.
                                // This particular menu is prone to reopening loops after siege auto-resolve, and the native
                                // siege AI can continue without us manually pushing this menu via hourly ticks.
                                if (desiredMenu == "menu_siege_strategies" || desiredMenu == "encounter_interrupted_siege_preparations")
                                {
                                    ModLogger.Info("INTERFACE",
                                        $"Battle detected - NOT switching to '{desiredMenu}' (enlisted: siege strategy menu is native-only)");
                                    return;
                                }

                                ModLogger.Info("INTERFACE",
                                    $"Battle detected - switching to native menu '{desiredMenu}'");
                                GameMenu.SwitchToMenu(desiredMenu);
                            }
                            else
                            {
                                // No specific menu - just return and let native system push its menu
                                ModLogger.Info("INTERFACE",
                                    "Battle detected - letting native system handle menu (no specific menu)");
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("INTERFACE", "Error handling battle menu transition", ex);
                        }
                    }
                }
                // Don't automatically return to the enlisted menu after battles
                // The menu tick handler will check GetGenericStateMenu() and switch back when appropriate
            }
        }

        /// <summary>
        ///     Checks if it's safe to activate the enlisted status menu by verifying there are no
        ///     active battles, sieges, or encounters that would conflict with the menu display.
        ///     This prevents menu activation during critical game state transitions that could cause
        ///     assertion failures or menu conflicts.
        /// </summary>
        /// <returns>True if the menu can be safely activated, false if a conflict exists.</returns>
        public static bool SafeToActivateEnlistedMenu()
        {
            var enlist = EnlistmentBehavior.Instance;
            var main = MobileParty.MainParty;
            var lord = enlist?.CurrentLord?.PartyBelongedTo;

            // Check for conflicts that would prevent menu activation
            // The MapEvent and SiegeEvent properties exist on Party, not directly on MobileParty
            // This is the correct API structure for checking battle state
            var playerBattle = main?.Party.MapEvent != null;
            var playerEncounter = PlayerEncounter.Current != null;
            var lordSiegeEvent = lord?.Party.SiegeEvent != null;
            var siegeRelatedBattle = IsSiegeRelatedBattle(main, lord);

            // Settlement encounters are OK - they happen when armies enter towns/castles
            // We only want to block battle/siege encounters, not peaceful settlement visits
            var isSettlementEncounter = playerEncounter &&
                PlayerEncounter.EncounterSettlement != null &&
                !playerBattle;

            // If it's a settlement encounter (not a battle), allow menu activation
            if (isSettlementEncounter && !lordSiegeEvent && !siegeRelatedBattle)
            {
                ModLogger.Debug("INTERFACE", "Allowing menu activation - settlement encounter (not battle)");
                return true;
            }

            // If any conflict exists, prevent menu activation
            // This ensures menus don't interfere with battles, sieges, or encounters
            var conflict = playerBattle || playerEncounter || lordSiegeEvent || siegeRelatedBattle;

            if (conflict)
            {
                ModLogger.Debug("INTERFACE",
                    $"Menu activation blocked - battle: {playerBattle}, encounter: {playerEncounter}, siege: {lordSiegeEvent}");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Detects siege-related battles like sally-outs where formal siege state may be paused.
        /// </summary>
        private static bool IsSiegeRelatedBattle(MobileParty main, MobileParty lord)
        {
            try
            {
                // Check if current battle has siege-related types
                var mapEvent = main?.MapEvent ?? lord?.MapEvent;
                if (mapEvent != null)
                {
                    // Check for siege battle types: SiegeOutside, SiegeAssault, etc.
                    var battleType = mapEvent.EventType.ToString();
                    var mapEventString = mapEvent.ToString();

                    var isSiegeType = battleType.Contains("Siege") ||
                                      mapEventString.Contains("Siege") ||
                                      mapEventString.Contains("SiegeOutside");

                    if (isSiegeType)
                    {
                        ModLogger.Info("INTERFACE",
                            $"SIEGE BATTLE DETECTED: Type='{battleType}', Event='{mapEventString}'");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Error in siege battle detection", ex);
                return false;
            }
        }

        private void OnDebugToolsSelected(MenuCallbackArgs args)
        {
            try
            {
                var options = new List<InquiryElement>
                {
                    new InquiryElement(
                        "gold",
                        new TextObject("{=Enlisted_Debug_Gold}Give 1000 Gold").ToString(),
                        null),
                    new InquiryElement(
                        "xp",
                        new TextObject("{=Enlisted_Debug_XP}Give XP to Rank Up").ToString(),
                        null),
                    new InquiryElement(
                        "force_event",
                        new TextObject("{=Enlisted_Debug_ForceEvent}Force Event Selection").ToString(),
                        null),
                    new InquiryElement(
                        "list_events",
                        new TextObject("{=Enlisted_Debug_ListEvents}List Eligible Events").ToString(),
                        null),
                    new InquiryElement(
                        "clear_cooldowns",
                        new TextObject("{=Enlisted_Debug_ClearCooldowns}Clear Event Cooldowns").ToString(),
                        null),
                    new InquiryElement(
                        "trigger_muster",
                        new TextObject("{=enlisted_debug_muster}🔧 Trigger Muster").ToString(),
                        null),
                    new InquiryElement(
                        "test_provisions",
                        new TextObject("{=enlisted_debug_provisions}🍖 Test Provisions Shop").ToString(),
                        null)
                };

                var inquiry = new MultiSelectionInquiryData(
                    new TextObject("{=Enlisted_Debug_Title}Debug Tools").ToString(),
                    new TextObject("{=Enlisted_Debug_Body}Select a debug action:").ToString(),
                    options,
                    true,
                    1,
                    1,
                    new TextObject("{=str_ok}OK").ToString(),
                    new TextObject("{=str_cancel}Cancel").ToString(),
                    selectedElements =>
                    {
                        if (selectedElements != null && selectedElements.Count > 0)
                        {
                            var selected = selectedElements[0].Identifier as string;
                            switch (selected)
                            {
                                case "gold":
                                    DebugToolsBehavior.GiveGold();
                                    break;
                                case "xp":
                                    DebugToolsBehavior.GiveEnlistmentXp();
                                    break;
                                case "force_event":
                                    DebugToolsBehavior.ForceEventSelection();
                                    break;
                                case "list_events":
                                    DebugToolsBehavior.ListEligibleEvents();
                                    break;
                                case "clear_cooldowns":
                                    DebugToolsBehavior.ClearEventCooldowns();
                                    break;
                                case "trigger_muster":
                                    // Defer muster trigger to next frame to allow inquiry UI to fully close
                                    // Prevents graphics driver crash from menu transition during popup rendering
                                    NextFrameDispatcher.RunNextFrame(() => DebugToolsBehavior.TriggerMuster());
                                    break;
                                case "test_provisions":
                                    DebugToolsBehavior.TestProvisionsShop();
                                    break;
                            }
                        }
                    },
                    null);

                MBInformationManager.ShowMultiSelectionInquiry(inquiry);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error opening debug tools", ex);
            }
        }

        /// <summary>
        ///     Safely activates the enlisted status menu by checking for conflicts and respecting
        ///     the native menu system's state. Checks if battles, sieges, or encounters are active,
        ///     and verifies what menu the native system wants to show before activating.
        /// </summary>
        public static void SafeActivateEnlistedMenu()
        {
            // First check if we can activate (battle/encounter guards prevent activation during conflicts)
            if (!SafeToActivateEnlistedMenu())
            {
                return;
            }

            // Check if lord is at a siege - if so, let native siege menus show
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted == true)
            {
                var lordParty = enlistment.CurrentLord?.PartyBelongedTo;
                var lordInSiege = lordParty?.SiegeEvent != null ||
                                  lordParty?.BesiegerCamp != null ||
                                  lordParty?.BesiegedSettlement != null;

                // If lord is in an army, also check if the army leader is besieging
                var lordArmy = lordParty?.Army;
                if (!lordInSiege && lordArmy != null)
                {
                    var armyLeader = lordArmy.LeaderParty;
                    lordInSiege = armyLeader?.BesiegerCamp != null ||
                                  armyLeader?.BesiegedSettlement != null ||
                                  armyLeader?.SiegeEvent != null;
                }

                if (lordInSiege)
                {
                    ModLogger.Debug("MENU", "Skipping enlisted menu activation - lord/army at siege, letting native menu show");
                    return;
                }
            }

            // Check what menu the game system wants to show based on current campaign state
            // We override army_wait (we provide our own army controls), but respect battle/encounter menus
            try
            {
                var genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();

                // Menus we should NOT override - these are active battle/encounter states
                var isBattleMenu = !string.IsNullOrEmpty(genericStateMenu) &&
                                   (genericStateMenu.Contains("encounter") ||
                                    genericStateMenu.Contains("siege") ||
                                    genericStateMenu.Contains("battle") ||
                                    genericStateMenu.Contains("prisoner") ||
                                    genericStateMenu.Contains("captured"));

                if (isBattleMenu)
                {
                    // Native system wants a battle/encounter menu - respect it
                    ModLogger.Debug("MENU", $"Respecting battle menu '{genericStateMenu}'");
                    return;
                }

                // Override army_wait and other non-battle menus with our enlisted menu
                // This ensures enlisted players always see their status menu when not in combat
                if (!string.IsNullOrEmpty(genericStateMenu) && genericStateMenu != "enlisted_status")
                {
                    ModLogger.Debug("MENU", $"Overriding '{genericStateMenu}' with enlisted_status");
                }

                var now = CampaignTime.Now;
                if (now - _lastEnlistedMenuActivationLogTime >
                    CampaignTime.Seconds((long)EnlistedMenuActivationLogCooldownSeconds))
                {
                    ModLogger.Info("MENU", "Activating enlisted status menu");
                    _lastEnlistedMenuActivationLogTime = now;
                }

                // Capture time state BEFORE menu activation (vanilla sets Stop, then StartWait sets FastForward)
                QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                GameMenu.ActivateGameMenu("enlisted_status");
            }
            catch (Exception ex)
            {
                // Fallback to original behavior if GetGenericStateMenu() fails
                // This ensures the menu can still be activated even if the check fails
                ModLogger.Debug("INTERFACE", $"Error checking GetGenericStateMenu, using fallback: {ex.Message}");
                QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                GameMenu.ActivateGameMenu("enlisted_status");
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnMenuOpened);

            // Track lord settlement entry to adjust menu option visibility
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEnteredForButton);

            // Auto-return to enlisted menu when leaving towns/castles
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeftReturnToCamp);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Menu behavior has no persistent state - all data comes from other behaviors
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Set up global gold icon for inline currency display across all menus
            MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");

            AddEnlistedMenus(starter);
            ModLogger.Info("INTERFACE", "Enlisted menu system initialized with modern UI styling");
        }

        /// <summary>
        /// Menu background initialization for enlisted_status menu.
        /// Sets culture-appropriate background and ambient audio for modern feel.
        /// Leaves time control untouched so player retains current pause/speed.
        /// </summary>
        [GameMenuInitializationHandler("enlisted_status")]
        public static void OnEnlistedStatusBackgroundInit(MenuCallbackArgs args)
        {
            var enlistment = EnlistmentBehavior.Instance;
            var backgroundMesh = "encounter_looter";

            if (enlistment?.CurrentLord?.Clan?.Kingdom?.Culture?.EncounterBackgroundMesh != null)
            {
                backgroundMesh = enlistment.CurrentLord.Clan.Kingdom.Culture.EncounterBackgroundMesh;
            }
            else if (enlistment?.CurrentLord?.Culture?.EncounterBackgroundMesh != null)
            {
                backgroundMesh = enlistment.CurrentLord.Culture.EncounterBackgroundMesh;
            }

            args.MenuContext.SetBackgroundMeshName(backgroundMesh);
            args.MenuContext.SetAmbientSound("event:/map/ambient/node/settlements/2d/camp_army");
            args.MenuContext.SetPanelSound("event:/ui/panels/settlement_camp");
        }


        /// <summary>
        /// Menu background initialization for enlisted_desert_confirm menu.
        /// Sets ominous background for desertion confirmation.
        /// Leaves time control untouched so player retains current pause/speed.
        /// </summary>
        [GameMenuInitializationHandler("enlisted_desert_confirm")]
        public static void OnDesertConfirmBackgroundInit(MenuCallbackArgs args)
        {
            // Desertion confirmation should feel ominous/decisive, not "normal encounter".
            // Use a known base-game mesh intended for negative outcomes.
            args.MenuContext.SetBackgroundMeshName("encounter_lose");
            args.MenuContext.SetAmbientSound("event:/map/ambient/node/settlements/2d/camp_army");
            // Time control left untouched - respects player's current pause/speed setting
        }

        /// <summary>
        ///     Real-time tick handler that runs every game frame while the player is enlisted.
        ///     Handles menu state updates, menu transitions, and settlement access logic.
        ///     Includes time delta validation to prevent assertion failures, and defers
        ///     heavy processing to hourly ticks to avoid overwhelming the system.
        /// </summary>
        /// <param name="dt">Time elapsed since last frame, in seconds. Must be positive.</param>
        private void OnTick(float dt)
        {
            if (!EnlistedActivation.EnsureActive())
            {
                return;
            }

            // Skip all processing if the player is not currently enlisted
            // This avoids unnecessary computation when the system isn't active
            var enlistmentBehavior = EnlistmentBehavior.Instance;
            if (enlistmentBehavior?.IsEnlisted != true)
            {
                return;
            }

            // Validate time delta to prevent assertion failures
            // Zero-delta-time updates can cause assertion failures in the rendering system
            if (dt <= 0)
            {
                return;
            }

            // Real-time tick handles lightweight menu operations and state updates
            // Heavy processing like battle detection is moved to hourly ticks to avoid
            // overwhelming the system with constant checks

            // Real-time menu updates for dynamic information
            if (CampaignTime.Now - _lastMenuUpdate > CampaignTime.Seconds((long)_updateIntervalSeconds))
            {
                if (_currentMenuId.StartsWith("enlisted_") && _menuNeedsRefresh)
                {
                    RefreshCurrentMenu();
                    _lastMenuUpdate = CampaignTime.Now;
                    _menuNeedsRefresh = false;
                }
            }

            // Only activate menu after settlement exit if the native system allows it
            // Check GetGenericStateMenu() first to see if the native system wants a different menu
            // This prevents conflicts with battle menus that might be starting
            if (_pendingReturnToEnlistedMenu &&
                CampaignTime.Now - _settlementExitTime > CampaignTime.Milliseconds(500))
            {
                try
                {
                    var enlistment = EnlistmentBehavior.Instance;
                    var isEnlisted = enlistment?.IsEnlisted == true;

                    // Check what menu native system wants to show
                    var genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();

                    // Only activate our menu if native system says it's OK (null or our menu)
                    if (isEnlisted && (genericStateMenu == "enlisted_status" || string.IsNullOrEmpty(genericStateMenu)))
                    {
                        // Double-check no active battle or encounter
                        var hasEncounter = PlayerEncounter.Current != null;
                        var inSettlement = MobileParty.MainParty.CurrentSettlement != null;
                        var inBattle = MobileParty.MainParty?.Party.MapEvent != null;

                        if (!hasEncounter && !inSettlement && !inBattle)
                        {
                            ModLogger.Info("INTERFACE",
                                "Deferred menu activation: conditions met, activating enlisted menu");
                            SafeActivateEnlistedMenu();
                        }
                    }
                    else if (!string.IsNullOrEmpty(genericStateMenu))
                    {
                        ModLogger.Debug("INTERFACE",
                            $"Deferred menu activation skipped: native system wants '{genericStateMenu}'");
                    }

                    _pendingReturnToEnlistedMenu = false; // Clear flag regardless of outcome
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("INTERFACE", "Deferred enlisted menu activation error", ex);
                    _pendingReturnToEnlistedMenu = false; // Clear flag to prevent endless retries
                }
            }
        }

        private void OnMenuOpened(MenuCallbackArgs args)
        {
            var previousMenu = _currentMenuId;
            _currentMenuId = args.MenuContext.GameMenu.StringId;
            _menuNeedsRefresh = true;

            // Clear captured time state when exiting enlisted menu system entirely.
            // This ensures next time we enter an enlisted menu, we capture the player's fresh time state.
            var wasEnlistedMenu = previousMenu?.StartsWith("enlisted_") == true;
            var isEnlistedMenu = _currentMenuId?.StartsWith("enlisted_") == true;
            if (wasEnlistedMenu && !isEnlistedMenu)
            {
                QuartermasterManager.CapturedTimeMode = null;
                ModLogger.Debug("INTERFACE", "Cleared captured time state - exited enlisted menu system");
            }

            // Log menu state transition when enlisted for debugging menu transitions
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted == true)
            {
                // Log state transition with previous menu info
                ModLogger.StateChange("MENU",
                    string.IsNullOrEmpty(previousMenu) ? "None" : previousMenu,
                    _currentMenuId);

                // Check all siege/battle conditions to detect state conflicts
                var lord = enlistment.CurrentLord;
                var main = MobileParty.MainParty;
                var playerBattle = main?.Party.MapEvent != null;
                var playerEncounter = PlayerEncounter.Current != null;
                // Check for siege events using the SiegeEvent property on Party
                var lordSiegeEvent = lord?.PartyBelongedTo?.Party.SiegeEvent != null;

                // Check for siege-related battles like sally-outs
                var siegeRelatedBattle = IsSiegeRelatedBattle(main, lord?.PartyBelongedTo);

                ModLogger.Trace("MENU",
                    $"Context: battle={playerBattle}, encounter={playerEncounter}, siege={lordSiegeEvent}");

                if (lordSiegeEvent || siegeRelatedBattle)
                {
                    var battleInfo = siegeRelatedBattle ? " (sally-out)" : "";
                    ModLogger.Debug("SIEGE", $"Menu '{_currentMenuId}' opened during siege{battleInfo}");

                    if (_currentMenuId == "enlisted_status")
                    {
                        ModLogger.Warn("MENU", "Enlisted menu opened during siege - should have been blocked");
                    }
                }

                // FALLBACK: Override army_wait and army_wait_at_settlement menus if they somehow appear
                // NOTE: GenericStateMenuPatch now prevents these menus from appearing in the first place
                // when enlisted, but this serves as a defensive fallback in case something bypasses the patch.
                // These are native army menus that would appear when the lord is at settlements or during army operations.
                // Enlisted soldiers should see their custom menu instead, unless in combat/siege.

                if (_currentMenuId == "army_wait_at_settlement" || _currentMenuId == "army_wait" ||
                    _currentMenuId == "castle" || _currentMenuId == "castle_outside" ||
                    _currentMenuId == "town" || _currentMenuId == "town_outside")
                {
                    ModLogger.Info("MENU", $"OnMenuOpened detected {_currentMenuId} while enlisted - checking for override");

                    // Only override if not in siege or siege-related battle
                    if (!lordSiegeEvent && !siegeRelatedBattle)
                    {
                        // For army_wait, also check that player isn't in a battle/encounter
                        if (_currentMenuId == "army_wait" && (playerBattle || playerEncounter))
                        {
                            ModLogger.Debug("MENU", "Not overriding army_wait - player in battle/encounter");
                            return;
                        }

                        // For castle/town menus (both inside and outside variants), check if player explicitly visited
                        if ((_currentMenuId == "castle" || _currentMenuId == "castle_outside" ||
                             _currentMenuId == "town" || _currentMenuId == "town_outside") && HasExplicitlyVisitedSettlement)
                        {
                            ModLogger.Info("MENU", $"Not overriding {_currentMenuId} - player explicitly visited settlement");
                            return;
                        }

                        ModLogger.Warn("MENU", $"FALLBACK: Overriding {_currentMenuId} to enlisted menu (patch may have been bypassed)");
                        // Defer the override to next frame to avoid conflicts with the native menu system
                        NextFrameDispatcher.RunNextFrame(() =>
                        {
                            if (enlistment?.IsEnlisted == true)
                            {
                                SafeActivateEnlistedMenu();
                            }
                        });
                    }
                    else
                    {
                        ModLogger.Debug("MENU", $"Not overriding {_currentMenuId} - siege/battle active");
                    }
                }
            }
        }

        /// <summary>
        ///     Registers all enlisted menu options and submenus with the game starter.
        ///     Creates the main enlisted status menu, duty selection menu, and return to army options.
        ///     All functionality is consolidated into a single menu system for clarity and simplicity.
        /// </summary>
        private void AddEnlistedMenus(CampaignGameStarter starter)
        {
            AddMainEnlistedStatusMenu(starter);
            RegisterCampHubMenu(starter);
            // NOTE: Decisions, Reports, Status submenus removed.
            // - Decisions is now an accordion on the main menu
            // - Reports/Status info is integrated into Camp Hub

            // Siege interaction is handled by native Bannerlord menus (join_siege_event / siege assault).
            // We intentionally do not add an enlisted-menu fallback button here, to keep siege flow vanilla.

            AddReturnToCampOptions(starter);
        }

        /// <summary>
        ///     Adds "Return to camp" options to native town and castle menus.
        ///     These allow enlisted players to return to the enlisted status menu from settlements.
        ///     Covers: main menus, outside menus, guard menus, and bribe menus to ensure
        ///     enlisted players always have an exit option even when native Leave buttons are hidden.
        /// </summary>
        private void AddReturnToCampOptions(CampaignGameStarter starter)
        {
            try
            {
                // Town menu (inside town)
                starter.AddGameMenuOption("town", "enlisted_return_to_camp",
                    "{=Enlisted_Menu_ReturnToCamp}Return to camp",
                    IsReturnToCampAvailable,
                    OnReturnToCampSelected,
                    true, 100); // Leave type, high priority to show near bottom

                // Town outside menu
                starter.AddGameMenuOption("town_outside", "enlisted_return_to_camp",
                    "{=Enlisted_Menu_ReturnToCamp}Return to camp",
                    IsReturnToCampAvailable,
                    OnReturnToCampSelected,
                    true, 100);

                // Castle outside menu
                starter.AddGameMenuOption("castle_outside", "enlisted_return_to_camp",
                    "{=Enlisted_Menu_ReturnToCamp}Return to camp",
                    IsReturnToCampAvailable,
                    OnReturnToCampSelected,
                    true, 100);

                // Castle menu (inside castle)
                starter.AddGameMenuOption("castle", "enlisted_return_to_camp",
                    "{=Enlisted_Menu_ReturnToCamp}Return to camp",
                    IsReturnToCampAvailable,
                    OnReturnToCampSelected,
                    true, 100);

                // Castle guard menu (when approaching castle gates)
                // This menu's native "Back" button uses game_menu_leave_on_condition which we patch
                starter.AddGameMenuOption("castle_guard", "enlisted_return_to_camp",
                    "{=Enlisted_Menu_ReturnToCamp}Return to camp",
                    IsReturnToCampAvailable,
                    OnReturnToCampSelected,
                    true, 100);

                // Castle bribe menu (when guards require bribe to enter)
                // This menu's native "Leave" button uses game_menu_leave_on_condition which we patch
                starter.AddGameMenuOption("castle_enter_bribe", "enlisted_return_to_camp",
                    "{=Enlisted_Menu_ReturnToCamp}Return to camp",
                    IsReturnToCampAvailable,
                    OnReturnToCampSelected,
                    true, 100);

                // Town guard menu (when approaching town gates)
                starter.AddGameMenuOption("town_guard", "enlisted_return_to_camp",
                    "{=Enlisted_Menu_ReturnToCamp}Return to camp",
                    IsReturnToCampAvailable,
                    OnReturnToCampSelected,
                    true, 100);

                // Town keep bribe menu (when guards require bribe to enter keep)
                starter.AddGameMenuOption("town_keep_bribe", "enlisted_return_to_camp",
                    "{=Enlisted_Menu_ReturnToCamp}Return to camp",
                    IsReturnToCampAvailable,
                    OnReturnToCampSelected,
                    true, 100);

                ModLogger.Info("INTERFACE",
                    "Added 'Return to camp' options to town/castle menus (including guard and bribe menus)");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to add Return to camp options", ex);
            }
        }

        /// <summary>
        ///     Checks if the "Return to camp" option should be available.
        ///     Only shows when player is enlisted.
        /// </summary>
        private bool IsReturnToCampAvailable(MenuCallbackArgs args)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return false;
            }

            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        /// <summary>
        ///     Handles returning to the enlisted camp from a settlement.
        /// </summary>
        private void OnReturnToCampSelected(MenuCallbackArgs args)
        {
            try
            {
                // Leave the settlement encounter
                if (PlayerEncounter.Current != null)
                {
                    if (PlayerEncounter.InsideSettlement)
                    {
                        PlayerEncounter.LeaveSettlement();
                    }

                    PlayerEncounter.Finish();
                }

                // Return to enlisted status menu
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    SafeActivateEnlistedMenu();
                    ModLogger.Info("INTERFACE", "Returned to camp from settlement");
                });
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error returning to camp", ex,
                    ctx: LogCtx.PlayerState());
            }
        }

        // Note: SimpleArmyBattleCondition method removed - was unused utility condition

        // NOTE: Emergency siege battle option removed. Native siege flow handles this.

        /// <summary>
        ///     Registers the main enlisted status menu with comprehensive military service information.
        ///     This is a wait menu that displays real-time service status, progression, and army information.
        ///     Includes all menu options for managing military service, equipment, and duties.
        /// </summary>
        private void AddMainEnlistedStatusMenu(CampaignGameStarter starter)
        {
            // Create a wait menu with time controls but hides progress boxes
            // This provides the wait menu functionality (time controls) without showing progress bars
            // NOTE: Use MenuOverlayType.None to avoid showing the empty battle bar when not in combat
            // Create a wait menu with time controls but hides progress boxes
            // Using method group conversion instead of explicit delegate creation
            // Template uses single PARTY_TEXT variable for the full narrative paragraph
            starter.AddWaitGameMenu("enlisted_status",
                "{=Enlisted_Menu_Status_Title}{PARTY_TEXT}",
                OnEnlistedStatusInit,
                OnEnlistedStatusCondition,
                null, // No consequence for wait menu
                OnEnlistedStatusTick, // Tick handler for real-time updates
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption); // Wait menu template that hides progress boxes

            // Main menu options for enlisted status menu
            // IMPORTANT: Options appear in the order they are added to the menu. Index is secondary.
            // Desired order: Orders → Decisions → Camp → Reports → Status → Debug

            // 0. Headlines drilldown — visible only when there are unread Headline-tier (or legacy Severity>=2) dispatches.
            starter.AddGameMenuOption("enlisted_status", "enlisted_headlines_entry",
                "{HEADLINES_HEADER_TEXT}",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    var news = Campaign.Current?.GetCampaignBehavior<EnlistedNewsBehavior>();
                    int unread = CountUnreadHeadlines(news);
                    if (unread == 0)
                    {
                        return false;
                    }
                    var headerText = "<span style=\"Link\">HEADLINES</span>";
                    headerText += " <span style=\"Link\">[NEW]</span>";
                    MBTextManager.SetTextVariable("HEADLINES_HEADER_TEXT", headerText);
                    return true;
                },
                _ => GameMenu.SwitchToMenu("enlisted_headlines"),
                false, 0);

            // 1. Orders accordion header (always visible).
            // When a new order arrives, it auto-expands and shows a [NEW] marker for the day.
            starter.AddGameMenuOption("enlisted_status", "enlisted_orders_header",
                "{ORDERS_HEADER_TEXT}",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.WaitQuest;

                    var current = OrderDisplayHelper.GetCurrent();
                    var isNewOrder = false;

                    if (current == null)
                    {
                        _ordersCollapsed = true;
                        _ordersLastSeenOrderId = string.Empty;
                    }
                    else
                    {
                        var currentId = OrderActivity.Instance?.ActiveNamedOrder?.OrderStoryletId ?? string.Empty;
                        isNewOrder = string.IsNullOrEmpty(_ordersLastSeenOrderId) ||
                                     !string.Equals(_ordersLastSeenOrderId, currentId, StringComparison.OrdinalIgnoreCase);
                        if (isNewOrder)
                        {
                            _ordersLastSeenOrderId = currentId;
                            _ordersCollapsed = false;
                        }
                    }

                    var headerText = "<span style=\"Link\">ORDERS</span>";
                    if (current != null)
                    {
                        if (isNewOrder)
                        {
                            headerText += " <span style=\"Link\">[NEW]</span>";
                        }

                        args.Tooltip = new TextObject("{=enlisted_orders_tooltip_active}You have an active order: {TITLE} ({ELAPSED}/{TOTAL} hours).");
                        _ = args.Tooltip.SetTextVariable("TITLE", current.Title);
                        _ = args.Tooltip.SetTextVariable("ELAPSED", current.HoursElapsed.ToString());
                        _ = args.Tooltip.SetTextVariable("TOTAL", current.HoursTotal.ToString());
                    }
                    else
                    {
                        args.Tooltip = new TextObject("{=enlisted_orders_tooltip_none}No active orders at this time.");
                    }

                    MBTextManager.SetTextVariable("ORDERS_HEADER_TEXT", headerText);
                    return true;
                },
                ToggleOrdersAccordion,
                false, 1);

            // 1b. Active order row (visible only when an order is active and the accordion is expanded).
            // Header already shows [NEW] when the order is unseen; row shows only the order title.
            starter.AddGameMenuOption("enlisted_status", "enlisted_active_order",
                "{ACTIVE_ORDER_TEXT}",
                args =>
                {
                    var display = OrderDisplayHelper.GetCurrent();
                    if (display == null || _ordersCollapsed)
                    {
                        return false;
                    }

                    args.optionLeaveType = GameMenuOption.LeaveType.Mission;
                    args.IsEnabled = true;
                    args.Tooltip = new TextObject("{=enlisted_orders_tooltip_active_row}Click to view order details.");

                    var row = $"    {display.Title}";
                    MBTextManager.SetTextVariable("ACTIVE_ORDER_TEXT", row);

                    return true;
                },
                _ => ShowOrdersMenu(),
                false, 2);

            // Evening intent slots - visible only when HomeActivity is in the evening phase.
            // Indices 9..13 (one per slot) so Evening sits above camp_hub (index 14) during its
            // phase, matching the decisions-slot pattern at 4..8. GameMenu.AddOption uses
            // positional insertion, not priority sorting, so every slot needs a unique index —
            // registering all five at a shared index would splice them around later items.
            for (var i = 0; i < 5; i++)
            {
                var slotIndex = i;
                var textKey = "EVENING_SLOT_" + i + "_TEXT";
                starter.AddGameMenuOption("enlisted_status", $"enlisted_evening_intent_slot_{i}",
                    $"{{EVENING_SLOT_{i}_TEXT}}",
                    args =>
                    {
                        if (!Activities.Home.HomeEveningMenuProvider
                                .IsSlotActive(slotIndex, out var label, out var tooltip))
                        {
                            return false;
                        }
                        MBTextManager.SetTextVariable(textKey, label.ToString(), false);
                        args.Tooltip = tooltip;
                        args.IsEnabled = true;
                        return true;
                    },
                    _ =>
                    {
                        Activities.Home.HomeEveningMenuProvider.OnSlotSelected(slotIndex);
                    },
                    false, 9 + i);
            }

            // 3. Camp hub - index 14 to appear after decision slots (4-8) and evening slots (9-13).
            // GameMenu.AddOption is positional insertion, not priority sorting: if 5 evening
            // slots were to register at 9+i, Camp at 10 would splice in between them. We give
            // Camp a position past the entire evening block.
            starter.AddGameMenuOption("enlisted_status", "enlisted_camp_hub",
                "{=enlisted_camp}Camp",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    args.Tooltip = new TextObject("{=enlisted_camp_tooltip}Rest, train, manage equipment, and visit the medical tent.");
                    return true;
                },
                _ =>
                {
                    QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                    GameMenu.SwitchToMenu(CampHubMenuId);
                },
                false, 14);

            // Debug tools (QA only): grant gold/XP - at very bottom
            // DISABLED FOR PRODUCTION - change to true in settings.json to enable for testing
            starter.AddGameMenuOption("enlisted_status", "enlisted_debug_tools",
                "{=Enlisted_Menu_DebugTools}Debug Tools",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    args.Tooltip = new TextObject("{=menu_tooltip_debug}Grant gold or enlistment XP for testing.");
                    // Always hidden in production - comment out this line and uncomment below to enable
                    return false;
                    // return ModConfig.Settings?.EnableDebugTools == true;
                },
                OnDebugToolsSelected,
                false, 50);

            // Master at Arms is deprecated. Formation is chosen during the T1→T2 proving event, and equipment is
            // purchased from the Quartermaster based on formation, tier, and culture. We keep the code for save
            // compatibility, but the menu option stays hidden.
#pragma warning disable CS0618 // Intentionally using obsolete method for save compatibility
            starter.AddGameMenuOption("enlisted_status", "enlisted_master_at_arms",
                "{=Enlisted_Menu_MasterAtArms}Master at Arms",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.TroopSelection;
                    args.Tooltip = new TextObject("{=menu_tooltip_master}Select your troop type and equipment loadout based on your current tier.");
                    // Hidden: formation is now chosen via proving event.
                    return false;
                },
                OnMasterAtArmsSelected,
                false, 100);
#pragma warning restore CS0618

            // NOTE: My Lance and Camp Management have been moved to Camp Hub.
            // These are accessible via Camp → Camp Management.

            // Visit Settlement - towns and castles only (Submenu icon)
            // Hidden when not at a settlement to reduce clutter.
            starter.AddGameMenuOption("enlisted_status", "enlisted_visit_settlement",
                "{VISIT_SETTLEMENT_TEXT}",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    var enlistment = EnlistmentBehavior.Instance;
                    var lord = enlistment?.CurrentLord;
                    var settlement = lord?.CurrentSettlement;

                    // Hide entirely when not at a town or castle
                    if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
                    {
                        return false;
                    }

                    args.Tooltip = new TextObject("{=menu_tooltip_visit}Enter the settlement while your lord is present.");
                    var visitText = new TextObject("{=Enlisted_Menu_VisitSettlementNamed}Visit {SETTLEMENT}");
                    _ = visitText.SetTextVariable("SETTLEMENT", settlement.Name);
                    MBTextManager.SetTextVariable("VISIT_SETTLEMENT_TEXT", visitText);
                    return true;
                },
                OnVisitTownSelected,
                false, 15);

            // NOTE: Duties, Medical Attention, and Service Records are all accessed via Camp Hub.
            // Keeping the main menu lean - only essential/frequent actions here.

            // === LEAVE OPTIONS (grouped at bottom) ===

            // No "return to duties" option needed - player IS doing duties by being in this menu

            // Headlines drilldown menu — listed dispatches with Headline-tier (or legacy Severity>=2) from the last 7 days.
            starter.AddGameMenu(
                "enlisted_headlines",
                "{=enl_headlines_body}{HEADLINES_TEXT}",
                args =>
                {
                    try
                    {
                        var news = Campaign.Current?.GetCampaignBehavior<EnlistedNewsBehavior>();
                        var text = FormatHeadlines(news);
                        MBTextManager.SetTextVariable("HEADLINES_TEXT", text);
                        MarkHeadlinesViewed(news);
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("INTERFACE", "Error initializing Headlines menu", ex);
                        MBTextManager.SetTextVariable("HEADLINES_TEXT", string.Empty);
                    }
                });

            starter.AddGameMenuOption(
                "enlisted_headlines", "enlisted_headlines_back",
                "{=enl_back}Back",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => GameMenu.SwitchToMenu("enlisted_status"),
                true, -1, false);
        }

        /// <summary>
        ///     Handles settlement exit by scheduling a deferred return to the enlisted menu.
        ///     When the player leaves a town or castle, this method schedules menu activation
        ///     for the next frame to avoid timing conflicts with other game systems during state transitions.
        /// </summary>
        /// <param name="party">The party that left the settlement.</param>
        /// <param name="settlement">The settlement that was left.</param>
        private void OnSettlementLeftReturnToCamp(MobileParty party, Settlement settlement)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted == true)
                {
                    var lordParty = enlistment.CurrentLord?.PartyBelongedTo;
                    // Refresh menu when lord leaves the settlement (so Visit Town disappears)
                    if (party == lordParty && settlement != null &&
                        (settlement.IsTown || settlement.IsVillage || settlement.IsCastle))
                    {
                        // Force full menu re-render by switching to the same menu
                        var menuContext = Campaign.Current?.CurrentMenuContext;
                        var currentMenuId = menuContext?.GameMenu?.StringId;
                        if (currentMenuId == "enlisted_status")
                        {
                            QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                            GameMenu.SwitchToMenu("enlisted_status");
                        }
                    }
                }

                if (party != MobileParty.MainParty || enlistment?.IsEnlisted != true)
                {
                    return;
                }

                if (!(settlement?.IsTown == true || settlement?.IsCastle == true))
                {
                    return;
                }

                // CRITICAL: If lord is still in this settlement, don't schedule enlisted menu return
                // With escort AI, the player will be pulled right back in - let them stay in native town menu
                var lordPartyCheck = enlistment.CurrentLord?.PartyBelongedTo;
                if (lordPartyCheck?.CurrentSettlement == settlement)
                {
                    ModLogger.Debug("INTERFACE",
                        "Lord still in settlement - skipping enlisted menu return, using native town menu");
                    return;
                }

                ModLogger.Info("INTERFACE", $"Left {settlement.Name} - scheduling return to enlisted menu");

                // Only finish non-battle encounters when leaving settlements
                // Battle encounters should be preserved to allow battle participation
                if (PlayerEncounter.Current != null)
                {
                    var enlistedLord = EnlistmentBehavior.Instance?.CurrentLord;
                    var lordInBattle = enlistedLord?.PartyBelongedTo?.Party.MapEvent != null;

                    var lordParty = enlistedLord?.PartyBelongedTo;
                    if (!lordInBattle && !InBattleOrSiege(lordParty))
                    {
                        PlayerEncounter.Finish();
                        ModLogger.Debug("INTERFACE", "Finished non-battle encounter on settlement exit");
                    }
                    else
                    {
                        ModLogger.Debug("INTERFACE",
                            "Skipped finishing encounter - lord in battle, preserving vanilla battle menu");
                    }
                }

                // Restore "invisible escort" state if we created a synthetic outside encounter
                // This ensures the player party returns to following mode after leaving the settlement
                if (_syntheticOutsideEncounter)
                {
                    _syntheticOutsideEncounter = false;
                    HasExplicitlyVisitedSettlement = false;
                    // Defer IsActive change to next frame to prevent assertion failures
                    // Setting IsActive during settlement exit can interfere with the animation/skeleton update cycle
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        if (MobileParty.MainParty != null)
                        {
                            MobileParty.MainParty.IsActive = false;
                        }
                    });
                }

                // Schedule deferred menu activation to avoid timing conflicts
                // This ensures other systems finish processing before we activate our menu
                // A short delay prevents conflicts with settlement exit animations and state transitions
                _pendingReturnToEnlistedMenu = true;
                _settlementExitTime = CampaignTime.Now;

                ModLogger.Debug("INTERFACE", "Deferred enlisted menu return scheduled (0.5s delay)");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "OnSettlementLeftReturnToCamp error", ex);
                // Ensure we don't get stuck in pending state
                _pendingReturnToEnlistedMenu = false;
            }
        }


        // NOTE: RegisterDecisionsMenu, RegisterReportsMenu, RegisterStatusMenu removed.
        // - Decisions is now an accordion on the main menu (see enlisted_decisions_header)
        // - Reports/Status info is integrated into Camp Hub CAMP STATUS section

        private void RegisterCampHubMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(CampHubMenuId,
                "{=enlisted_camp_hub_title}{CAMP_HUB_TEXT}",
                OnCampHubInit,
                args =>
                {
                    _ = args;
                    return EnlistmentBehavior.Instance?.IsEnlisted == true;
                },
                null,
                OnCampHubTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // NOTE: Rest & Recover, Train Skills, and Morale Boost moved to Decisions menu.

            // Service Records
            starter.AddGameMenuOption(CampHubMenuId, "camp_hub_service_records",
                "{=enlisted_camp_records}Service Records",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    args.Tooltip = new TextObject("{=enlisted_camp_records_tooltip}Review your service records and history.");
                    return true;
                },
                _ =>
                {
                    QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                    GameMenu.SwitchToMenu("enlisted_service_records");
                },
                false, 1);

            // Manage Companions
            starter.AddGameMenuOption(CampHubMenuId, "camp_hub_companions",
                "{=enlisted_camp_companions}Manage Companions",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    args.Tooltip = new TextObject("{=enlisted_camp_companions_tooltip}Assign and manage your companions.");
                    return true;
                },
                _ =>
                {
                    QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                    GameMenu.SwitchToMenu("enlisted_companions");
                },
                false, 2);

            // Personal Retinue (T7+: Commander track with recruit training system)
            starter.AddGameMenuOption(CampHubMenuId, "camp_hub_retinue",
                "{=ct_option_retinue}Muster Personal Retinue",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.TroopSelection;
                    var enlistment = EnlistmentBehavior.Instance;

                    // T7+ for new Commander track retinue system (15/25/35 soldiers)
                    if ((enlistment?.EnlistmentTier ?? 1) < 7)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=ct_warn_retinue_tier_locked}You must reach Commander rank (Tier 7) to command your own retinue.");
                        return true;
                    }

                    args.Tooltip = new TextObject("{=ct_retinue_tooltip}Manage your personal retinue of soldiers. Recruits trickle in and must be trained through battle.");
                    return true;
                },
                _ =>
                {
                    QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                    GameMenu.SwitchToMenu("enlisted_retinue");
                },
                false, 3);

            // Visit Quartermaster - blocked when company supply is critically low (below 15%)
            starter.AddGameMenuOption(CampHubMenuId, "camp_hub_quartermaster",
                "{=Enlisted_Menu_VisitQuartermaster}Visit Quartermaster",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;

                    // Supply gate: block equipment access when supplies are critically low (below 15%)
                    const int criticalSupplyThreshold = 15;
                    var companyNeeds = EnlistmentBehavior.Instance?.CompanyNeeds;
                    if (companyNeeds != null && companyNeeds.Supplies < criticalSupplyThreshold)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=menu_qm_supply_blocked}Quartermaster unavailable. The company's supplies are critically low. Equipment requisitions are suspended until supply levels recover.");
                        ModLogger.Debug("INTERFACE", $"Quartermaster blocked: Supplies at {companyNeeds.Supplies}% (threshold: {criticalSupplyThreshold}%)");
                        return true;
                    }

                    args.Tooltip = new TextObject("{=menu_tooltip_quartermaster}Purchase equipment for your formation and rank. Newly unlocked items marked [NEW].");
                    return true;
                },
                OnQuartermasterSelected,
                false, 4);

            // Medical care migrated to decision system (Phase 6G)
            // Treatment decisions appear as orchestrated opportunities when player has conditions
            // See: dec_medical_surgeon, dec_medical_rest, dec_medical_herbal, dec_medical_emergency

            // Access Baggage Train moved to Decisions accordion - appears only when accessible

            // Talk to Companion - opens MultiSelectionInquiry over spawned wanderer-substrate companions
            starter.AddGameMenuOption(CampHubMenuId, "camp_hub_talk_to_companion",
                "{=enlisted_camp_talk_to_companion}Talk to...",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    var spawned = CompanionLifecycleHandler.Instance?.GetSpawnedCompanions();
                    if (spawned == null || spawned.Count == 0)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=enlisted_camp_talk_to_companion_none}No companions are with you yet.");
                        return true;
                    }
                    args.Tooltip = new TextObject("{=enlisted_camp_talk_to_companion_tooltip}Speak with a companion in your party.");
                    return true;
                },
                OnTalkToCompanionSelected,
                false, 6);

            // My Lord... - conversation with the current lord (Conversation icon)
            starter.AddGameMenuOption(CampHubMenuId, "camp_hub_talk_to_lord",
                "{=Enlisted_Menu_TalkToLord}My Lord...",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    var nearbyLords = GetNearbyLordsForConversation();
                    if (nearbyLords.Count == 0)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=menu_disabled_no_lords}No lords nearby for conversation.");
                        return true;
                    }
                    args.Tooltip = new TextObject("{=menu_tooltip_talk}Speak with nearby lords for quests, news, and relation building.");
                    return true;
                },
                OnTalkToSelected,
                false, 7);


            // Back
            starter.AddGameMenuOption(CampHubMenuId, "camp_hub_back",
                "{=enlisted_camp_back}Back",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ =>
                {
                    QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                    GameMenu.SwitchToMenu("enlisted_status");
                },
                false, 100);
        }

        private void OnCampHubInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                MBTextManager.SetTextVariable("CAMP_HUB_TEXT", BuildCampHubText());
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Error initializing Camp hub", ex);
                MBTextManager.SetTextVariable("CAMP_HUB_TEXT", "Camp hub unavailable.");
            }
        }

        private static void OnCampHubTick(MenuCallbackArgs args, CampaignTime dt)
        {
            _ = args;
            _ = dt;
            // Intentionally empty - hub is refreshed on init and re-entry.
        }

        private List<DispatchItem> GetUnreadHighSeverity(EnlistedNewsBehavior news)
        {
            if (news == null) { return new List<DispatchItem>(); }
            int today = (int)CampaignTime.Now.ToDays;
            var items = news.GetPersonalFeedSince(today - 7);
            var result = new List<DispatchItem>();
            foreach (var it in items)
            {
                // Prefer the typed predicate when the dispatch carries a Tier (i.e. was written
                // via the StoryDirector path post-PR-c). Fall back to the legacy Severity check
                // for dispatches created through older code paths that don't set Tier.
                bool isHeadline = it.Tier != StoryTier.Log
                    ? it.IsHeadline
                    : it.Severity >= 2;
                if (!isHeadline) { continue; }
                if (!string.IsNullOrEmpty(it.StoryKey) && _viewedHeadlineStoryKeys.Contains(it.StoryKey)) { continue; }
                result.Add(it);
            }
            return result;
        }

        private int CountUnreadHeadlines(EnlistedNewsBehavior news)
        {
            return GetUnreadHighSeverity(news).Count;
        }

        private string FormatHeadlines(EnlistedNewsBehavior news)
        {
            var items = GetUnreadHighSeverity(news);
            var sb = new StringBuilder();
            foreach (var it in items)
            {
                var line = EnlistedNewsBehavior.FormatDispatchForDisplay(it, includeColor: false);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _ = sb.AppendLine("• " + line);
                }
            }
            return sb.ToString();
        }

        private void MarkHeadlinesViewed(EnlistedNewsBehavior news)
        {
            var items = GetUnreadHighSeverity(news);
            foreach (var it in items)
            {
                if (!string.IsNullOrEmpty(it.StoryKey))
                {
                    _ = _viewedHeadlineStoryKeys.Add(it.StoryKey);
                }
            }
        }

        private static string BuildCampHubText()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return new TextObject("{=enl_camp_hub_not_enlisted}You are not currently enlisted.").ToString();
                }

                var lord = enlistment.CurrentLord;
                var lordParty = lord?.PartyBelongedTo;
                var sb = new StringBuilder();

                // === COMPANY STATUS (Flowing summary combining atmosphere, strength, needs) ===
                var companyStatus = BuildCompanyStatusSummary(enlistment, lord, lordParty);
                if (!string.IsNullOrWhiteSpace(companyStatus))
                {
                    _ = sb.AppendLine(companyStatus);
                    _ = sb.AppendLine();
                }

                // === PERIOD RECAP (Since last muster - orders, battles, training) ===
                var periodRecap = BuildPeriodRecapSection(enlistment);
                if (!string.IsNullOrWhiteSpace(periodRecap))
                {
                    var headerText = new TextObject("{=status_header_since_muster}SINCE LAST MUSTER").ToString();
                    _ = sb.AppendLine($"<span style=\"Header\">{headerText}</span>");
                    _ = sb.AppendLine(periodRecap);
                    _ = sb.AppendLine();
                }

                // === UPCOMING (Scheduled activities, expected orders) ===
                var upcoming = BuildUpcomingSection();
                if (!string.IsNullOrWhiteSpace(upcoming))
                {
                    var headerText = new TextObject("{=status_header_upcoming}UPCOMING").ToString();
                    _ = sb.AppendLine($"<span style=\"Header\">{headerText}</span>");
                    _ = sb.AppendLine(upcoming);
                    _ = sb.AppendLine();
                }

                // === RECENT ACTIVITY (What the player and company have been doing) ===
                var recentActivities = BuildRecentActivitiesNarrative(enlistment);
                if (!string.IsNullOrWhiteSpace(recentActivities))
                {
                    var headerText = new TextObject("{=status_header_recent_activity}RECENT ACTIVITY").ToString();
                    _ = sb.AppendLine($"<span style=\"Header\">{headerText}</span>");
                    _ = sb.AppendLine(recentActivities);
                    _ = sb.AppendLine();
                }

                // === STATUS (Player's personal condition - injuries, illness, duty status) ===
                var playerStatus = BuildPlayerPersonalStatus(enlistment);
                if (!string.IsNullOrWhiteSpace(playerStatus))
                {
                    var headerText = new TextObject("{=status_header_your_status}YOUR STATUS").ToString();
                    _ = sb.AppendLine($"<span style=\"Header\">{headerText}</span>");
                    _ = sb.AppendLine(playerStatus);
                }

                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                ModLogger.Debug("INTERFACE", $"Error building camp hub text: {ex.Message}");
                return new TextObject("{=enl_camp_hub_unavailable}Service Status unavailable.").ToString();
            }
        }

        /// <summary>
        /// Builds a flowing Company Status summary combining atmosphere, strength, and needs with color-coded text.
        /// </summary>
        private static string BuildCompanyStatusSummary(EnlistmentBehavior enlistment, Hero lord, MobileParty lordParty)
        {
            try
            {
                var parts = new List<string>();
                var companyNeeds = enlistment?.CompanyNeeds;

                // Atmospheric opening
                var atmosphere = BuildCampAtmosphereLine(lord);
                if (!string.IsNullOrWhiteSpace(atmosphere))
                {
                    parts.Add(atmosphere);
                }

                // Company strength and composition in flowing text
                if (lordParty?.MemberRoster != null)
                {
                    var roster = lordParty.MemberRoster;
                    var totalTroops = roster.TotalManCount;
                    var totalWounded = roster.TotalWounded;
                    var fitForDuty = totalTroops - totalWounded;

                    // Quality breakdown
                    var highTier = 0;
                    var midTier = 0;
                    var lowTier = 0;
                    foreach (var troop in roster.GetTroopRoster())
                    {
                        if (troop.Character == null || troop.Character.IsHero)
                        {
                            continue;
                        }
                        var tier = troop.Character.Tier;
                        var count = troop.Number;

                        if (tier >= 5)
                        {
                            highTier += count;
                        }
                        else if (tier >= 3)
                        {
                            midTier += count;
                        }
                        else
                        {
                            lowTier += count;
                        }
                    }

                    var strengthParts = new List<string>();
                    var soldiersText = new TextObject("{=status_soldiers}soldiers").ToString();
                    strengthParts.Add($"<span style=\"Default\">{totalTroops} {soldiersText}</span>");

                    if (totalWounded > 0)
                    {
                        var woundedStyle = totalWounded > (totalTroops * 0.3) ? "Alert" : "Warning";
                        var woundedText = new TextObject("{=status_wounded}wounded").ToString();
                        strengthParts.Add($"<span style=\"{woundedStyle}\">{totalWounded} {woundedText}</span>");
                    }

                    if (highTier > 0 || midTier > 0)
                    {
                        var eliteText = new TextObject("{=status_elite}elite").ToString();
                        var veteransText = new TextObject("{=status_veterans}veterans").ToString();
                        strengthParts.Add($"<span style=\"Success\">{highTier} {eliteText}</span>");
                        strengthParts.Add($"<span style=\"Default\">{midTier} {veteransText}</span>");
                    }

                    parts.Add(string.Join(", ", strengthParts) + ".");
                }

                // Company needs woven into narrative
                if (companyNeeds != null)
                {
                    var needsParts = new List<string>();

                    var supplyPhrase = GetSupplyPhrase(companyNeeds.Supplies);
                    var readinessPhrase = GetReadinessPhrase(companyNeeds.Readiness);

                    if (!string.IsNullOrEmpty(supplyPhrase))
                    {
                        needsParts.Add(supplyPhrase);
                    }
                    if (!string.IsNullOrEmpty(readinessPhrase))
                    {
                        needsParts.Add(readinessPhrase);
                    }

                    if (needsParts.Count > 0)
                    {
                        parts.Add(string.Join(", ", needsParts) + ".");
                    }
                }

                // Location context
                if (lordParty != null)
                {
                    if (lordParty.CurrentSettlement != null)
                    {
                        var encampedText = new TextObject("{=status_encamped_at}Encamped at {SETTLEMENT}")
                            .SetTextVariable("SETTLEMENT", lordParty.CurrentSettlement.Name)
                            .ToString();
                        parts.Add($"{encampedText}.");
                    }
                    else if (lordParty.TargetSettlement != null)
                    {
                        var marchText = new TextObject("{=status_on_march_toward}On the march toward {SETTLEMENT}")
                            .SetTextVariable("SETTLEMENT", lordParty.TargetSettlement.Name)
                            .ToString();
                        parts.Add($"{marchText}.");
                    }
                    else
                    {
                        var patrolText = new TextObject("{=status_on_patrol}On patrol in the field").ToString();
                        parts.Add($"{patrolText}.");
                    }

                    if (lordParty.Army != null && lordParty.Army.LeaderParty != lordParty)
                    {
                        var armyLeader = lordParty.Army.LeaderParty.LeaderHero;
                        if (armyLeader != null)
                        {
                            var armyText = new TextObject("{=status_part_of_army}Part of {LORD}'s army")
                                .SetTextVariable("LORD", armyLeader.Name)
                                .ToString();
                            parts.Add($"{armyText}.");
                        }
                    }
                }

                // Baggage train status when notable (raids, delays, arrivals, lockdowns)
                var baggageStatus = EnlistedNewsBehavior.BuildBaggageStatusLine();
                if (!string.IsNullOrWhiteSpace(baggageStatus))
                {
                    parts.Add(baggageStatus);
                }

                // Lord rumors and strategic gossip
                var rumors = BuildLordRumorsLine(lord, lordParty);
                if (!string.IsNullOrWhiteSpace(rumors))
                {
                    parts.Add(rumors);
                }

                return string.Join(" ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Generates lord rumors and strategic gossip based on comprehensive world state analysis.
        /// Integrates WorldStateAnalyzer, CampLifeBehavior pressures, and recent news for rich context.
        /// </summary>
        private static string BuildLordRumorsLine(Hero lord, MobileParty lordParty)
        {
            try
            {
                if (lord == null || lordParty == null)
                {
                    return string.Empty;
                }

                var kingdom = lord.Clan?.Kingdom;
                if (kingdom == null)
                {
                    return string.Empty;
                }

                // Get comprehensive world state analysis
                var worldState = WorldStateAnalyzer.AnalyzeSituation();
                var campLife = CampLifeBehavior.Instance;
                var newsSystem = EnlistedNewsBehavior.Instance;

                var rumors = new List<string>();

                // Build context-aware rumors based on lord situation from WorldStateAnalyzer
                var lordSituation = worldState.LordIs;
                var activityLevel = worldState.ExpectedActivity;
                var warStance = worldState.KingdomStance;

                // === SIEGE SITUATION === (Highest priority - WorldStateAnalyzer detected)
                if (lordSituation == LordSituation.SiegeAttacking ||
                    lordSituation == LordSituation.SiegeDefending)
                {
                    var siegeTarget = lordParty.CurrentSettlement ?? lordParty.TargetSettlement;
                    if (siegeTarget != null)
                    {
                        // Add pressure context to siege rumors
                        var logisticsLow = campLife?.LogisticsStrain > 60;
                        // MoraleShock removed - ReadinessStrain doesn't exist either, just use logistics
                        var readinessLow = logisticsLow;

                        if (lordSituation == LordSituation.SiegeAttacking)
                        {
                            if (logisticsLow && readinessLow)
                            {
                                rumors.Add($"<span style=\"Alert\">Siege of {siegeTarget.Name} grinds on. Whispers of withdrawal if supplies don't improve.</span>");
                            }
                            else if (logisticsLow)
                            {
                                rumors.Add($"<span style=\"Warning\">Camp talk: {lord.Name} won't lift the siege, but men wonder how long supplies will last.</span>");
                            }
                            else if (activityLevel == ActivityLevel.Intense)
                            {
                                rumors.Add($"<span style=\"Alert\">Assault on {siegeTarget.Name} expected soon. {lord.Name} pushes hard for a breach.</span>");
                            }
                            else
                            {
                                rumors.Add($"<span style=\"Warning\">Siege continues. Veterans say {lord.Name} means to starve them out.</span>");
                            }
                        }
                        else
                        {
                            rumors.Add($"<span style=\"Alert\">We hold {siegeTarget.Name} under siege. {lord.Name} vows no retreat.</span>");
                        }
                    }
                }
                // === ARMY CAMPAIGN === (WorldStateAnalyzer: WarActiveCampaign or in army)
                else if (lordSituation == LordSituation.WarActiveCampaign || lordParty.Army != null)
                {
                    var army = lordParty.Army;
                    var isLeader = army?.LeaderParty == lordParty;
                    var armyLeader = army?.LeaderParty?.LeaderHero;

                    if (isLeader && lordParty.TargetSettlement != null)
                    {
                        var target = lordParty.TargetSettlement;
                        var isEnemyFort = target.IsFortification && target.OwnerClan?.Kingdom != kingdom;

                        if (isEnemyFort)
                        {
                            // Check recent news for context
                            var recentVictory = newsSystem?.GetVisiblePersonalFeedItems(3)
                                ?.Any(n => n.Category == "participation" && n.HeadlineKey == "News_PlayerBattle") == true;

                            if (recentVictory)
                            {
                                rumors.Add($"<span style=\"Success\">After our victory, {lord.Name} marches on {target.Name}. Morale is high.</span>");
                            }
                            else
                            {
                                rumors.Add($"<span style=\"Warning\">Word is {lord.Name} means to take {target.Name}. Expect a fight.</span>");
                            }
                        }
                        else
                        {
                            rumors.Add($"<span style=\"Default\">Army moves toward {target.Name}. Purpose unclear, but {lord.Name} has a plan.</span>");
                        }
                    }
                    else if (armyLeader != null)
                    {
                        // Part of larger army - rumors about army commander
                        if (campLife?.TerritoryPressure > 60)
                        {
                            rumors.Add($"<span style=\"Warning\">Deep in enemy lands. {armyLeader.Name} pushes the campaign hard.</span>");
                        }
                        else
                        {
                            rumors.Add($"<span style=\"Default\">Part of {armyLeader.Name}'s host. {lord.Name} follows orders from above.</span>");
                        }
                    }
                    else
                    {
                        rumors.Add($"<span style=\"Default\">{lord.Name} keeps counsel close. The campaign's direction unclear.</span>");
                    }
                }
                // === WAR MARCHING === (WorldStateAnalyzer: moving during wartime, but not in army)
                else if (lordSituation == LordSituation.WarMarching)
                {
                    var target = lordParty.TargetSettlement;
                    if (target != null)
                    {
                        var isEnemyTerritory = target.OwnerClan?.Kingdom != null &&
                                              FactionManager.IsAtWarAgainstFaction(kingdom, target.OwnerClan.Kingdom);

                        if (isEnemyTerritory && target.IsFortification)
                        {
                            // Check if lord recently formed army (from news)
                            var recentArmyFormed = newsSystem?.GetVisiblePersonalFeedItems(3)
                                ?.Any(n => n.Category == "army" && n.HeadlineKey == "News_ArmyForming") == true;

                            if (recentArmyFormed)
                            {
                                rumors.Add($"<span style=\"Warning\">{lord.Name} gathers allies. {target.Name} is surely the target.</span>");
                            }
                            else
                            {
                                rumors.Add($"<span style=\"Warning\">Whispers in camp: {lord.Name} plans a strike on {target.Name}.</span>");
                            }
                        }
                        else if (isEnemyTerritory)
                        {
                            rumors.Add($"<span style=\"Warning\">Men say {lord.Name} intends to raid enemy lands near {target.Name}.</span>");
                        }
                        else if (target.IsFortification)
                        {
                            rumors.Add($"<span style=\"Success\">Orders came: we make for {target.Name}. Should be safe ground.</span>");
                        }
                        else
                        {
                            rumors.Add($"<span style=\"Default\">Rumor has it {lord.Name} seeks fresh recruits from {target.Name}.</span>");
                        }
                    }
                    else
                    {
                        // Wartime patrol with no clear destination
                        if (campLife?.TerritoryPressure > 60)
                        {
                            rumors.Add($"<span style=\"Alert\">Deep in hostile lands. {lord.Name} keeps us moving, watching for threats.</span>");
                        }
                        else
                        {
                            rumors.Add($"<span style=\"Warning\">On patrol in wartime. {lord.Name}'s orders: stay alert, stay mobile.</span>");
                        }
                    }
                }
                // === PEACETIME GARRISON === (WorldStateAnalyzer: PeacetimeGarrison or PeacetimeRecruiting)
                else if (lordSituation == LordSituation.PeacetimeGarrison ||
                        lordSituation == LordSituation.PeacetimeRecruiting)
                {
                    var settlement = lordParty.CurrentSettlement;

                    // Check for recent pay tension
                    var payProblems = campLife?.PayTension > 50;
                    // MoraleShock removed - ReadinessStrain doesn't exist, use payProblems as proxy
                    var readinessIssues = payProblems;

                    if (payProblems && readinessIssues)
                    {
                        rumors.Add($"<span style=\"Warning\">Men grumble about pay and boredom. {lord.Name} better have work for us soon.</span>");
                    }
                    else if (readinessIssues)
                    {
                        var dayNumber = (int)CampaignTime.Now.ToDays;
                        var phrases = new[]
                        {
                            $"Men grow restless in garrison. Some say {lord.Name} will seek a campaign soon.",
                            $"Camp morale sags with routine. Veterans mutter about needing action.",
                            $"Idle hands make trouble. {lord.Name} needs to keep the men busy."
                        };
                        rumors.Add($"<span style=\"Default\">{PickRandomStable(phrases, dayNumber)}</span>");
                    }
                    else if (settlement != null)
                    {
                        var dayNumber = (int)CampaignTime.Now.ToDays;
                        var phrases = new[]
                        {
                            $"Quiet days at {settlement.Name}. {lord.Name} keeps watch, but expects no trouble.",
                            $"Garrison duty means routine. {lord.Name} runs drills and keeps discipline.",
                            $"Veterans tell stories of old campaigns while {lord.Name} keeps the peace.",
                            $"Word is {lord.Name} meets with other lords, but nothing's been decided."
                        };
                        rumors.Add($"<span style=\"Default\">{PickRandomStable(phrases, dayNumber)}</span>");
                    }
                    else
                    {
                        rumors.Add($"<span style=\"Default\">Peacetime means recruiting and training. {lord.Name} rebuilds the company.</span>");
                    }
                }
                // === DEFEATED/CAPTURED === (WorldStateAnalyzer detected crisis)
                else if (lordSituation == LordSituation.Defeated)
                {
                    rumors.Add($"<span style=\"Alert\">After the defeat, {lord.Name} regroups. Men speak of revenge in hushed tones.</span>");
                }
                else if (lordSituation == LordSituation.Captured)
                {
                    rumors.Add($"<span style=\"Alert\">{lord.Name} is held captive. We await word of ransom or rescue.</span>");
                }
                // === FALLBACK === (Unknown situation - use basic patrol logic)
                else
                {
                    var dayNumber = (int)CampaignTime.Now.ToDays;
                    var phrases = new[]
                    {
                        $"Men don't know where {lord.Name} is leading us. Either scouting or lost.",
                        $"Officers say {lord.Name} follows intelligence reports, but won't share details.",
                        $"Camp gossip: {lord.Name} is looking for something specific.",
                        $"{lord.Name} keeps changing course. Testing our mobility, or just cautious."
                    };
                    rumors.Add($"<span style=\"Default\">{PickRandomStable(phrases, dayNumber)}</span>");
                }

                return rumors.Count > 0 ? string.Join(" ", rumors) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds Recent Activity narrative showing what the player and company have been doing.
        /// </summary>
        private static string BuildRecentActivitiesNarrative(EnlistmentBehavior enlistment)
        {
            try
            {
                var parts = new List<string>();
                var news = EnlistedNewsBehavior.Instance;

                // Recent personal feed events
                var personalItems = news?.GetVisiblePersonalFeedItems(3);
                if (personalItems != null && personalItems.Count > 0)
                {
                    foreach (var item in personalItems)
                    {
                        var eventLine = EnlistedNewsBehavior.FormatDispatchForDisplay(item, true);
                        if (!string.IsNullOrWhiteSpace(eventLine))
                        {
                            parts.Add(eventLine + ".");
                        }
                    }
                }

                // Muster/pay info
                if (enlistment?.IsPayMusterPending == true)
                {
                    parts.Add("<span style=\"Warning\">Muster awaiting your attention.</span>");
                }

                if (parts.Count == 0)
                {
                    return "All quiet. Nothing notable to report.";
                }

                return string.Join(" ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds player's personal status - flavor text about injuries, illness, duty status.
        /// </summary>
        private static string BuildPlayerPersonalStatus(EnlistmentBehavior enlistment)
        {
            try
            {
                var parts = new List<string>();
                var mainHero = Hero.MainHero;
                var orderState = OrderActivity.Instance?.ActiveNamedOrder;

                // Current duty
                if (orderState != null)
                {
                    var display = OrderDisplayHelper.GetCurrent();
                    var orderTitle = string.IsNullOrEmpty(display?.Title) ? "duty" : display.Title;
                    var hoursSinceIssued = OrderDisplayHelper.GetHoursSinceStarted();

                    if (hoursSinceIssued < 6)
                    {
                        var freshOrdersText = new TextObject("{=status_fresh_orders}Fresh orders").ToString();
                        parts.Add($"<span style=\"Link\">{freshOrdersText}:</span> {orderTitle}.");
                    }
                    else if (hoursSinceIssued < 24)
                    {
                        var onDutyText = new TextObject("{=status_on_duty}On duty").ToString();
                        var hourText = new TextObject("{=status_hour}hour").ToString();
                        parts.Add($"<span style=\"Link\">{onDutyText}:</span> {orderTitle}, {hourText} {(int)hoursSinceIssued}.");
                    }
                    else
                    {
                        var days = (int)(hoursSinceIssued / 24);
                        var stillOnDutyText = new TextObject("{=status_still_on_duty}Still on duty").ToString();
                        var dayText = new TextObject("{=status_day}day").ToString();
                        parts.Add($"<span style=\"Warning\">{stillOnDutyText}:</span> {orderTitle}, {dayText} {days}.");
                    }
                }
                else
                {
                    var offDutyText = new TextObject("{=status_off_duty}Off duty").ToString();
                    var noOrdersText = new TextObject("{=status_no_orders_present}No orders at present").ToString();
                    parts.Add($"<span style=\"Success\">{offDutyText}.</span> {noOrdersText}.");
                }

                // Physical condition
                if (mainHero != null)
                {
                    var conditionBehavior = Conditions.PlayerConditionBehavior.Instance;
                    var playerCondition = conditionBehavior?.State;

                    // Check for injuries (custom system)
                    if (playerCondition?.HasInjury == true)
                    {
                        var severity = playerCondition.CurrentInjury;
                        var daysRemaining = playerCondition.InjuryDaysRemaining;
                        var dayText = daysRemaining == 1 ? "day" : "days";

                        var severityLabel = severity switch
                        {
                            Conditions.InjurySeverity.Critical => "Critical injury",
                            Conditions.InjurySeverity.Severe => "Severe injury",
                            Conditions.InjurySeverity.Moderate => "Injured",
                            _ => "Injured"
                        };

                        var urgentNote = severity >= Conditions.InjurySeverity.Severe
                            ? " Urgent care recommended."
                            : " Training restricted.";

                        parts.Add($"<span style=\"Alert\">{severityLabel}.</span> {daysRemaining} {dayText} recovery.{urgentNote}");
                    }
                    // Check for illness (custom system)
                    else if (playerCondition?.HasIllness == true)
                    {
                        var severity = playerCondition.CurrentIllness;
                        var daysRemaining = playerCondition.IllnessDaysRemaining;
                        var dayText = daysRemaining == 1 ? "day" : "days";

                        var severityLabel = severity switch
                        {
                            Conditions.IllnessSeverity.Critical => "Critical illness",
                            Conditions.IllnessSeverity.Severe => "Severe illness",
                            Conditions.IllnessSeverity.Moderate => "Ill",
                            _ => "Ill"
                        };

                        var urgentNote = severity >= Conditions.IllnessSeverity.Severe
                            ? " Urgent care recommended."
                            : " Seek treatment if worsening.";

                        parts.Add($"<span style=\"Alert\">{severityLabel}.</span> {daysRemaining} {dayText} recovery.{urgentNote}");
                    }
                    // Check for native wounds
                    else if (mainHero.IsWounded)
                    {
                        parts.Add("<span style=\"Alert\">Your wounds slow you.</span> Rest and heal before the next call.");
                    }
                }

                // Service context
                var lord = enlistment?.CurrentLord;
                if (lord != null)
                {
                    var daysSinceEnlistment = (int)(CampaignTime.Now - enlistment.EnlistmentDate).ToDays;
                    var lordTitle = lord.Clan?.Kingdom != null
                        ? $"{lord.Name} of {lord.Clan.Kingdom.Name}"
                        : lord.Name.ToString();

                    parts.Add($"Serving under <span style=\"Link\">{lordTitle}</span> ({daysSinceEnlistment} days).");
                }

                // Player forecast (upcoming commitments, expected orders)
                var forecast = BuildBriefPlayerForecast();
                if (!string.IsNullOrWhiteSpace(forecast))
                {
                    parts.Add(forecast);
                }

                return string.Join(" ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds period recap section showing activity since last muster.
        /// Displays orders completed, battles survived, training done, and XP earned.
        /// Color-coded for easy scanning: green for positive, gold for neutral/partial, red for negative.
        /// </summary>
        private static string BuildPeriodRecapSection(EnlistmentBehavior enlistment)
        {
            try
            {
                var parts = new List<string>();
                var news = EnlistedNewsBehavior.Instance;

                // Calculate days since last muster
                var lastMusterDay = enlistment?.LastMusterDay ?? 0;
                var currentDay = (int)CampaignTime.Now.ToDays;
                var daysSinceMuster = lastMusterDay > 0 ? currentDay - lastMusterDay : 0;
                var musterDaysRemaining = Math.Max(0, 12 - daysSinceMuster);

                // Get period statistics from tracking systems
                var orderOutcomes = news?.GetRecentOrderOutcomes(12) ?? new List<OrderOutcomeRecord>();
                var eventOutcomes = news?.GetRecentEventOutcomes(12) ?? new List<EventOutcomeRecord>();
                var xpSources = enlistment?.GetXPSourcesThisPeriod() ?? new Dictionary<string, int>();
                var lastMuster = news?.GetLastMusterOutcome();

                // Count completed orders and their success rate
                var ordersCompleted = orderOutcomes.Count(o => o.Success);
                var ordersFailed = orderOutcomes.Count(o => !o.Success);

                // Orders summary with narrative from recent orders
                if (ordersCompleted > 0 || ordersFailed > 0)
                {
                    var orderParts = new List<string>();
                    if (ordersCompleted > 0)
                    {
                        orderParts.Add($"<span style=\"Success\">{ordersCompleted} completed</span>");
                    }
                    if (ordersFailed > 0)
                    {
                        orderParts.Add($"<span style=\"Alert\">{ordersFailed} failed</span>");
                    }

                    // Add narrative from most recent order
                    var recentOrder = orderOutcomes.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.BriefSummary));
                    if (recentOrder != null)
                    {
                        var narrative = recentOrder.BriefSummary;
                        var lastPart = recentOrder.Success
                            ? $"Last: {narrative}"
                            : $"Last: <span style=\"Alert\">{narrative}</span>";
                        parts.Add($"Orders: {string.Join(", ", orderParts)}. {lastPart}.");
                    }
                    else
                    {
                        parts.Add($"Orders: {string.Join(", ", orderParts)}.");
                    }
                }

                // Battles and casualties (from last muster record or news tracking)
                var lostCount = news?.LostSinceLastMuster ?? 0;
                var sickCount = news?.SickSinceLastMuster ?? 0;

                if (lostCount > 0 || sickCount > 0)
                {
                    var casualtyParts = new List<string>();
                    if (lostCount > 0)
                    {
                        casualtyParts.Add($"<span style=\"Alert\">{lostCount} lost</span>");
                    }
                    if (sickCount > 0)
                    {
                        casualtyParts.Add($"<span style=\"Warning\">{sickCount} sick</span>");
                    }
                    parts.Add("Company losses: " + string.Join(", ", casualtyParts) + ".");
                }

                // Event choices made
                var eventCount = eventOutcomes.Count;
                if (eventCount > 0)
                {
                    var recentEvent = eventOutcomes.FirstOrDefault();
                    if (recentEvent != null && !string.IsNullOrWhiteSpace(recentEvent.EventTitle))
                    {
                        parts.Add($"<span style=\"Link\">{eventCount} incidents</span> handled (last: {recentEvent.EventTitle}).");
                    }
                }

                // Days until next muster
                if (musterDaysRemaining > 0)
                {
                    var musterStyle = musterDaysRemaining <= 2 ? "Warning" : "Default";
                    parts.Add($"<span style=\"{musterStyle}\">{musterDaysRemaining} days</span> until next muster.");
                }
                else if (enlistment?.IsPayMusterPending == true)
                {
                    parts.Add("<span style=\"Warning\">Muster pending.</span> Report for pay.");
                }

                if (parts.Count == 0)
                {
                    return "New muster period. No activity recorded yet.";
                }

                return string.Join(" ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds upcoming section showing scheduled activities and expected orders.
        /// Displays player commitments, routine schedule, and order forecasts.
        /// Color-coded: blue for scheduled commitments, gold for imminent orders.
        /// </summary>
        private static string BuildUpcomingSection()
        {
            try
            {
                var parts = new List<string>();

                // Current order status and forecast
                var orderState = OrderActivity.Instance?.ActiveNamedOrder;
                if (orderState != null)
                {
                    var display = OrderDisplayHelper.GetCurrent();
                    var orderTitle = string.IsNullOrEmpty(display?.Title) ? "duty" : display.Title;
                    var hoursSinceIssued = OrderDisplayHelper.GetHoursSinceStarted();

                    // Estimate remaining time based on typical order duration (24-72h)
                    var hoursRemaining = Math.Max(0, 24 - (int)hoursSinceIssued);
                    var currentDutyText = new TextObject("{=status_current_duty}Current duty").ToString();
                    if (hoursRemaining > 0)
                    {
                        var completeStyle = hoursRemaining < 6 ? "Success" : "Default";
                        var remainingText = new TextObject("{=status_hours_remaining}~{HOURS}h remaining")
                            .SetTextVariable("HOURS", hoursRemaining)
                            .ToString();
                        parts.Add($"{currentDutyText}: <span style=\"{completeStyle}\">{orderTitle}</span> ({remainingText}).");
                    }
                    else
                    {
                        var completingSoonText = new TextObject("{=status_completing_soon}completing soon").ToString();
                        parts.Add($"{currentDutyText}: <span style=\"Link\">{orderTitle}</span> ({completingSoonText}).");
                    }
                }
                else
                {
                    // Check world state for activity level hints
                    var worldState = WorldStateAnalyzer.AnalyzeSituation();
                    if (worldState.ExpectedActivity == ActivityLevel.Intense)
                    {
                        var expectSoonText = new TextObject("{=status_expect_orders_soon}Expect orders soon. The situation is intense.").ToString();
                        parts.Add($"<span style=\"Warning\">{expectSoonText}</span>");
                    }
                    else if (worldState.ExpectedActivity == ActivityLevel.Active)
                    {
                        var likelyText = new TextObject("{=status_likely_receive_orders}Likely to receive orders within the day").ToString();
                        parts.Add($"{likelyText}.");
                    }
                    else
                    {
                        var noOrdersText = new TextObject("{=status_no_orders_expected}No orders expected. Routine duties apply").ToString();
                        parts.Add($"{noOrdersText}.");
                    }
                }

                if (parts.Count == 0)
                {
                    return "All quiet. Enjoy the respite while it lasts.";
                }

                return string.Join(" ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the next day phase in sequence.
        /// </summary>
        private static DayPhase GetNextPhase(DayPhase current)
        {
            return current switch
            {
                DayPhase.Dawn => DayPhase.Midday,
                DayPhase.Midday => DayPhase.Dusk,
                DayPhase.Dusk => DayPhase.Night,
                DayPhase.Night => DayPhase.Dawn,
                _ => DayPhase.Dawn
            };
        }

        /// <summary>
        /// Builds a rich 5-sentence camp status narrative integrating WorldStateAnalyzer,
        /// CampLifeBehavior pressures, and recent events for comprehensive context.
        /// </summary>
        private static string BuildCampNarrativeParagraph(EnlistmentBehavior enlistment, Hero lord, MobileParty lordParty)
        {
            try
            {
                var sentences = new List<string>();
                var companyNeeds = enlistment?.CompanyNeeds;
                var news = EnlistedNewsBehavior.Instance;

                // Get comprehensive systems state
                var worldState = WorldStateAnalyzer.AnalyzeSituation();
                var campLife = CampLifeBehavior.Instance;
                var lordSituation = worldState.LordIs;
                var activityLevel = worldState.ExpectedActivity;

                // Sentence 1: Atmosphere/setting with world state context
                var atmosphere = BuildCampAtmosphereLine(lord);
                if (!string.IsNullOrWhiteSpace(atmosphere))
                {
                    sentences.Add(atmosphere);
                }

                // NOTE: Removed personal feed from Company Reports - those belong in Player Status only
                // Personal routine activities (rest, training, etc.) should not appear in company-level reports

                // Sentence 3: Company status integrating CompanyNeeds + CampLifeBehavior pressures
                if (companyNeeds != null)
                {
                    var statusParts = new List<string>();

                    // Supplies with logistics strain context
                    // Only show "stretched thin" if logistics is strained AND supplies are actually concerning
                    var logisticsHigh = campLife?.LogisticsStrain > 60;
                    var suppliesLow = companyNeeds.Supplies < 50;

                    // Diagnostic: Log supply status computation for troubleshooting news vs QM discrepancies
                    var logisticsValue = campLife?.LogisticsStrain ?? 0f;
                    ModLogger.LogOnce(
                        $"SupplyStatus_Day{(int)CampaignTime.Now.ToDays}",
                        "Supply",
                        $"Company Reports: Supplies={companyNeeds.Supplies}%, Logistics={logisticsValue:F0}, " +
                        $"ShowStretchedThin={logisticsHigh && suppliesLow} (requires both logistics>60 AND supplies<50)");

                    if (logisticsHigh && suppliesLow)
                    {
                        // Both logistics strained and supplies actually low - show the combined warning
                        statusParts.Add("<span style=\"Alert\">Supply lines stretched thin</span>");
                    }
                    else
                    {
                        // Show actual supply level (handles critical, low, adequate, and good states)
                        var supplyPhrase = GetSupplyPhrase(companyNeeds.Supplies);
                        if (!string.IsNullOrEmpty(supplyPhrase))
                        {
                            statusParts.Add(supplyPhrase);
                        }
                    }

                    // Readiness check
                    var readinessPhrase = GetReadinessPhrase(companyNeeds.Readiness);
                    if (!string.IsNullOrEmpty(readinessPhrase))
                    {
                        statusParts.Add(readinessPhrase);
                    }

                    if (statusParts.Count > 0)
                    {
                        sentences.Add(string.Join(", ", statusParts) + ".");
                    }
                }

                // Sentence 4: Specific concerns (wounds, rest, equipment) with territory pressure
                var detailParts = new List<string>();
                var wounded = lordParty?.MemberRoster?.TotalWounded ?? 0;
                var inHostileTerritory = campLife?.TerritoryPressure > 60;

                if (wounded > 20)
                {
                    detailParts.Add($"<span style=\"Alert\">{wounded} wounded</span> strain the healers");
                }
                else if (wounded > 5)
                {
                    detailParts.Add($"<span style=\"Warning\">{wounded} recovering</span> from injuries");
                }


                // Add territory pressure warning if high
                if (inHostileTerritory && detailParts.Count < 2)
                {
                    detailParts.Add("<span style=\"Alert\">deep in enemy lands</span>");
                }

                if (detailParts.Count > 0)
                {
                    sentences.Add(string.Join(", ", detailParts) + ".");
                }

                // Baggage train status when notable (raids, delays, arrivals, lockdowns)
                var baggageStatus = EnlistedNewsBehavior.BuildBaggageStatusLine();
                if (!string.IsNullOrWhiteSpace(baggageStatus))
                {
                    sentences.Add(baggageStatus);
                }

                // Sentence 5: Context - location/army status with past/present tense blending
                var currentSettlement = lordParty?.CurrentSettlement;
                var inArmy = lordParty?.Army != null && lordParty.Army.LeaderParty != lordParty;

                // Track settlement changes for "recently arrived" phrasing
                if (currentSettlement != _lastKnownSettlement)
                {
                    _lastKnownSettlement = currentSettlement;
                    _lastSettlementChangeTime = CampaignTime.Now;
                }

                // Track army changes for smooth transitions
                if (inArmy != _lastKnownInArmy)
                {
                    _lastKnownInArmy = inArmy;
                    _lastArmyChangeTime = CampaignTime.Now;
                }

                var hoursSinceArrival = currentSettlement != null ?
                    (CampaignTime.Now - _lastSettlementChangeTime).ToHours : 999;
                var hoursSinceArmyChange = (CampaignTime.Now - _lastArmyChangeTime).ToHours;

                if (currentSettlement != null)
                {
                    // Use "Recently arrived" for first 24 hours, then "Encamped"
                    var prefix = hoursSinceArrival < 24 ? "Recently arrived at" : "Encamped at";
                    sentences.Add($"{prefix} <span style=\"Link\">{currentSettlement.Name}</span>.");
                }
                else if (inArmy)
                {
                    var armyLeader = lordParty.Army.LeaderParty.LeaderHero;
                    var armySize = lordParty.Army.Parties.Count();
                    if (armyLeader != null)
                    {
                        var armyVerb = hoursSinceArmyChange < 12 ? "Joined" : "Marching with";
                        sentences.Add($"{armyVerb} <span style=\"Link\">{armyLeader.Name}'s host</span>, {armySize} parties strong.");
                    }
                }
                else if (lordParty?.TargetSettlement != null)
                {
                    var targetName = lordParty.TargetSettlement.Name?.ToString();
                    if (!string.IsNullOrEmpty(targetName))
                    {
                        // Use maritime or land phrase depending on travel mode
                        if (lordParty.IsCurrentlyAtSea)
                        {
                            // Sailing toward a settlement means preparing to disembark
                            sentences.Add($"Disembarking at <span style=\"Link\">{targetName}</span>.");
                        }
                        else
                        {
                            sentences.Add($"The column makes for <span style=\"Link\">{targetName}</span>.");
                        }
                    }
                }
                else
                {
                    var activity = GetCampActivityPhrase();
                    if (!string.IsNullOrEmpty(activity))
                    {
                        sentences.Add(activity);
                    }
                }

                // Ensure we have content
                if (sentences.Count == 0)
                {
                    return "<span style=\"Default\">The camp is quiet. All is in order.</span>";
                }

                return string.Join(" ", sentences.Take(6));
            }
            catch
            {
                return "<span style=\"Default\">The camp awaits.</span>";
            }
        }

        /// <summary>
        /// Returns a color-coded, descriptive phrase about supply status.
        /// </summary>
        private static string GetSupplyPhrase(int supplies)
        {
            if (supplies >= 80)
            {
                return "<span style=\"Success\">Well-stocked with provisions</span>";
            }
            if (supplies >= 60)
            {
                return "<span style=\"Success\">Supplies holding steady</span>";
            }
            if (supplies >= 40)
            {
                return "Rations adequate for now";
            }
            if (supplies >= 25)
            {
                return "<span style=\"Warning\">Food stores running thin</span>";
            }
            if (supplies >= 15)
            {
                return "<span style=\"Alert\">Rations critically low</span>";
            }
            return "<span style=\"Alert\">Starvation threatens the company</span>";
        }

        /// <summary>
        /// Returns a color-coded, descriptive phrase about readiness status.
        /// </summary>
        private static string GetReadinessPhrase(int readiness)
        {
            if (readiness >= 80)
            {
                return "<span style=\"Success\">combat readiness high</span>";
            }
            if (readiness >= 60)
            {
                return "<span style=\"Success\">readiness solid</span>";
            }
            if (readiness >= 40)
            {
                return "readiness adequate";
            }
            if (readiness >= 25)
            {
                return "<span style=\"Warning\">training deficiencies noted</span>";
            }
            if (readiness >= 15)
            {
                return "<span style=\"Alert\">readiness poor</span>";
            }
            return "<span style=\"Alert\">unit combat ineffective</span>";
        }

        /// <summary>
        /// Returns a phrase describing current camp activity.
        /// Uses time-based logic for stable, non-flickering text.
        /// </summary>
        private static string GetCampActivityPhrase()
        {
            try
            {
                // Time-based activity that won't flicker
                var hour = CampaignTime.Now.GetHourOfDay;
                if (hour >= 22 || hour < 6)
                {
                    return "The camp sleeps.";
                }
                if (hour < 10)
                {
                    return "Morning routine underway.";
                }
                if (hour < 17)
                {
                    return "Soldiers go about their duties.";
                }
                return "Cook fires light up as evening settles.";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a compact one-sentence player recap for the main menu player status section.
        /// Shows narrative from recent activities in natural Bannerlord RP flavor with color coding.
        /// Integrates routine outcomes, orders, and events into a flowing status summary.
        /// </summary>
        private static string BuildBriefPlayerRecap(EnlistmentBehavior enlistment)
        {
            try
            {
                var news = EnlistedNewsBehavior.Instance;
                if (news == null)
                {
                    return string.Empty;
                }

                // Priority 1: Recent order outcomes (most important)
                var orderOutcomes = news.GetRecentOrderOutcomes(3);
                var recentOrder = orderOutcomes.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.BriefSummary));
                if (recentOrder != null)
                {
                    var narrative = recentOrder.BriefSummary;
                    var colorStyle = recentOrder.Success ? "Success" : "Alert";
                    return $"<span style=\"{colorStyle}\">{narrative}</span>";
                }

                // Priority 2: Check personal feed for routine outcomes and recent events
                var personalFeed = news.GetVisiblePersonalFeedItems(3);
                if (personalFeed.Count > 0)
                {
                    // Get most recent item
                    var recentItem = personalFeed[0];
                    if (!string.IsNullOrWhiteSpace(recentItem.HeadlineKey))
                    {
                        // Format the dispatch item properly to resolve headline key and placeholders
                        var text = EnlistedNewsBehavior.FormatDispatchForDisplay(recentItem, includeColor: false);

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            // Determine color based on severity
                            var colorStyle = recentItem.Severity switch
                            {
                                1 => "Success",   // Positive (excellent routine outcome, success)
                                2 => "Warning",   // Attention (mishap, failure)
                                3 => "Alert",     // Urgent (critical issues)
                                4 => "Critical",  // Critical danger
                                _ => "Default"    // Normal
                            };

                            // For routine activities, strip the activity name prefix if present
                            // to make it more natural ("Good progress" instead of "Combat Training: Good progress")
                            if (recentItem.Category == "routine_activity" && text.Contains(": "))
                            {
                                var parts = text.Split(new[] { ": " }, 2, StringSplitOptions.None);
                                if (parts.Length == 2)
                                {
                                    text = parts[1]; // Use the flavor text part only
                                }
                            }

                            // For event outcomes, also simplify by removing event title prefix
                            // Display just the narrative part for natural flow
                            if (text.Contains(": ") && !recentItem.Category.StartsWith("simulation_"))
                            {
                                var parts = text.Split(new[] { ": " }, 2, StringSplitOptions.None);
                                if (parts.Length == 2)
                                {
                                    text = parts[1];
                                }
                            }

                            return $"<span style=\"{colorStyle}\">{text}</span>";
                        }
                    }
                }

                // Fallback based on service time
                var daysServed = (int)(enlistment?.DaysServed ?? 0);
                if (daysServed < 2)
                {
                    return "Still settling in. The routine will come.";
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a compact player forecast sentence for the main menu player status section.
        /// Shows upcoming commitments, expected orders, or activity level hints in natural flowing text.
        /// </summary>
        private static string BuildBriefPlayerForecast()
        {
            try
            {
                // Activity-level forecast based on world state.
                var worldState = WorldStateAnalyzer.AnalyzeSituation();
                if (worldState.ExpectedActivity == ActivityLevel.Intense)
                {
                    var text = new TextObject("{=forecast_activity_intense}The captain's tent is busy. Expect orders soon.").ToString();
                    return $"<span style=\"Warning\">{text}</span>";
                }
                if (worldState.ExpectedActivity == ActivityLevel.Active)
                {
                    var text = new TextObject("{=forecast_activity_active}The company is on the move. Stay sharp for duty calls.").ToString();
                    return $"<span style=\"Link\">{text}</span>";
                }
                if (worldState.ExpectedActivity == ActivityLevel.Routine)
                {
                    var fallbackText = new TextObject("{=forecast_routine_fallback}Routine duty. Nothing unusual.").ToString();
                    return $"<span style=\"Default\">{fallbackText}</span>";
                }

                var quietText = new TextObject("{=forecast_activity_quiet}Garrison duty. Nothing pressing.").ToString();
                return $"<span style=\"Default\">{quietText}</span>";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds the camp atmosphere opening line. Uses day-seeded randomization
        /// to ensure the same text is shown for the entire in-game day (prevents flickering).
        /// </summary>
        private static string BuildCampAtmosphereLine(Hero lord)
        {
            try
            {
                var hour = CampaignTime.Now.GetHourOfDay;
                var party = lord?.PartyBelongedTo;
                var dayNumber = (int)CampaignTime.Now.ToDays;

                // Check for recent battle (within 24 hours)
                var news = EnlistedNewsBehavior.Instance;
                if (news != null)
                {
                    if (news.TryGetLastPlayerBattleSummary(out var lastBattleTime, out var playerWon) &&
                        lastBattleTime != CampaignTime.Zero)
                    {
                        var hoursSinceBattle = (CampaignTime.Now - lastBattleTime).ToHours;
                        if (hoursSinceBattle < 24)
                        {
                            return playerWon
                                ? PickRandomStable(BattleWonAtmoLines, dayNumber)
                                : PickRandomStable(BattleLostAtmoLines, dayNumber);
                        }
                    }
                }

                // Context-based atmosphere
                if (party?.Party?.SiegeEvent != null || party?.BesiegerCamp != null)
                {
                    return PickRandomStable(SiegeAtmoLines, dayNumber);
                }

                if (party?.Army != null)
                {
                    return PickRandomStable(ArmyAtmoLines, dayNumber);
                }

                if (party?.CurrentSettlement != null)
                {
                    return PickRandomStable(SettlementAtmoLines, dayNumber);
                }

                // Check if at sea - use maritime variants
                if (party?.IsCurrentlyAtSea == true)
                {
                    // Use disembarking lines if we have a target settlement (approaching land)
                    if (party.TargetSettlement != null)
                    {
                        return PickRandomStable(DisembarkingAtmoLines, dayNumber);
                    }

                    // Normal at-sea atmosphere
                    if (hour < 6 || hour >= 20)
                    {
                        return PickRandomStable(SeaNightAtmoLines, dayNumber);
                    }
                    if (hour < 10)
                    {
                        return PickRandomStable(SeaMorningAtmoLines, dayNumber);
                    }
                    if (hour >= 17)
                    {
                        return PickRandomStable(SeaEveningAtmoLines, dayNumber);
                    }

                    return PickRandomStable(SeaDayAtmoLines, dayNumber);
                }

                // Default: on the march (land), time-based
                if (hour < 6 || hour >= 20)
                {
                    return PickRandomStable(NightAtmoLines, dayNumber);
                }
                if (hour < 10)
                {
                    return PickRandomStable(MorningAtmoLines, dayNumber);
                }
                if (hour >= 17)
                {
                    return PickRandomStable(EveningAtmoLines, dayNumber);
                }

                return PickRandomStable(DayAtmoLines, dayNumber);
            }
            catch
            {
                return "The company goes about its duties.";
            }
        }

        /// <summary>
        /// Picks a random line from an array using a seed (day number).
        /// This ensures the same line is returned for the entire day, preventing flickering.
        /// </summary>
        private static string PickRandomStable(string[] lines, int seed)
        {
            if (lines == null || lines.Length == 0)
            {
                return string.Empty;
            }
            var rng = new Random(seed);
            return lines[rng.Next(lines.Length)];
        }

        // Atmosphere lines for different contexts
        private static readonly string[] BattleWonAtmoLines =
        {
            "Spirits are high after the victory. Men share stories around the fires.",
            "The camp buzzes with the energy of triumph. Wounds are tended, tales told.",
            "Victory songs drift through the camp. The company earned this rest."
        };

        private static readonly string[] BattleLostAtmoLines =
        {
            "A somber mood hangs over the camp. The cost was high.",
            "Quiet conversations and grim faces. The company licks its wounds.",
            "Few words are spoken. The men tend to their gear and their thoughts."
        };

        private static readonly string[] SiegeAtmoLines =
        {
            "The siege works stretch before the walls. Engineers shout orders.",
            "Smoke rises from siege preparations. The walls loom in the distance.",
            "The camp sprawls around the besieged fortification. Tension fills the air."
        };

        private static readonly string[] ArmyAtmoLines =
        {
            "The army camp stretches in every direction. Banners flutter in the wind.",
            "Thousands of soldiers go about their duties. The army is a city on the move.",
            "Lords and their retinues mingle. The gathered host is an impressive sight."
        };

        private static readonly string[] SettlementAtmoLines =
        {
            "The company has made camp near the settlement. Soldiers come and go.",
            "Market sounds drift from nearby. A welcome respite from the march.",
            "The settlement provides a backdrop to camp life. Rest comes easier here."
        };

        private static readonly string[] NightAtmoLines =
        {
            "The fires burn low as men settle in for the night.",
            "Sentries patrol the perimeter. The camp sleeps under the stars.",
            "Night has fallen. Quiet conversations drift from the watch fires."
        };

        private static readonly string[] MorningAtmoLines =
        {
            "Dawn breaks over the camp. Men stir and prepare for the day.",
            "The morning muster begins. Sergeants call out orders.",
            "Cook fires crackle to life. The smell of breakfast fills the air."
        };

        private static readonly string[] EveningAtmoLines =
        {
            "The day's march is done. Men gather around the evening fires.",
            "Dusk settles over the camp. The company prepares for night.",
            "Evening rations are distributed. Tired soldiers find their rest."
        };

        private static readonly string[] DayAtmoLines =
        {
            "The camp is alive with activity. Soldiers drill and maintain gear.",
            "Midday sun beats down on the tents. The company goes about its work.",
            "Another day on campaign. The rhythms of camp life continue."
        };

        // Sea travel atmosphere lines
        private static readonly string[] SeaMorningAtmoLines =
        {
            "Dawn breaks over the water. The crew begins the day's work.",
            "Morning light reflects off the waves. The ship rolls gently.",
            "The day begins at sea. Gulls circle overhead."
        };

        private static readonly string[] SeaDayAtmoLines =
        {
            "The ship cuts through the waves. Wind fills the sails.",
            "Midday sun beats down on the deck. The voyage continues.",
            "Another day at sea. The horizon stretches endlessly."
        };

        private static readonly string[] SeaEveningAtmoLines =
        {
            "The sun sets over the water. Men gather on deck.",
            "Dusk falls at sea. The watch changes as evening comes.",
            "Evening aboard ship. The crew settles in for the night."
        };

        private static readonly string[] SeaNightAtmoLines =
        {
            "Night falls over the water. Lanterns sway with the ship.",
            "The ship sails on through darkness. Stars reflect on black water.",
            "Night at sea. The crew sleeps below while the watch keeps vigil."
        };

        private static readonly string[] DisembarkingAtmoLines =
        {
            "The ships make port. Men prepare to disembark.",
            "Land ahead. The company readies to go ashore.",
            "The voyage ends. Ships are being unloaded at the docks."
        };

        /// <summary>
        ///     Initialize enlisted status menu with current service information.
        /// </summary>
        private void OnEnlistedStatusInit(MenuCallbackArgs args)
        {
            try
            {
                ModLogger.Debug("INTERFACE", "OnEnlistedStatusInit: Menu opened");

                // No cache to clear - Orchestrator owns the schedule, we query it directly

                // Time state is captured by calling code BEFORE menu activation
                // (not here - vanilla has already set Stop by the time init runs)

                // 1.3.4+: Set proper menu background to avoid assertion failure
                // Use the lord's kingdom culture background, or fallback to generic encounter mesh
                var enlistment = EnlistmentBehavior.Instance;
                var backgroundMesh = "encounter_looter"; // Safe fallback

                if (enlistment?.CurrentLord?.Clan?.Kingdom?.Culture != null)
                {
                    backgroundMesh = enlistment.CurrentLord.Clan.Kingdom.Culture.EncounterBackgroundMesh;
                }
                else if (enlistment?.CurrentLord?.Culture != null)
                {
                    backgroundMesh = enlistment.CurrentLord.Culture.EncounterBackgroundMesh;
                }

                args.MenuContext.SetBackgroundMeshName(backgroundMesh);

                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                // For first enlistment (no captured time), default to x1 speed instead of fast forward
                var captured = QuartermasterManager.CapturedTimeMode;
                if (captured.HasValue)
                {
                    var normalized = QuartermasterManager.NormalizeToStoppable(captured.Value);
                    Campaign.Current.TimeControlMode = normalized;
                }
                else
                {
                    // First enlistment - default to normal speed (x1) instead of fast forward
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
                    ModLogger.Debug("INTERFACE", "First enlistment - set time to x1 (StoppablePlay)");
                }

                RefreshEnlistedStatusDisplay(args);
                _menuNeedsRefresh = true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Error initializing enlisted status menu", ex);
            }
        }

        /// <summary>
        ///     Refreshes the enlisted status display with current military service information.
        ///     Updates all dynamic text variables used in the menu display, including party leader,
        ///     enlistment details, tier, formation, wages, and XP progression.
        ///     Formats information as "Label : Value" pairs displayed line by line in the menu.
        /// </summary>
        private void RefreshEnlistedStatusDisplay(MenuCallbackArgs args = null)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    MBTextManager.SetTextVariable("ENLISTED_STATUS_TEXT",
                        new TextObject("{=Enlisted_Status_NotEnlisted}You are not currently enlisted."));
                    return;
                }

                var lord = enlistment?.CurrentLord;
                if (lord == null)
                {
                    MBTextManager.SetTextVariable("ENLISTED_STATUS_TEXT",
                        new TextObject("{=Enlisted_Status_ErrorNoLord}Error: No enlisted lord found."));
                    return;
                }

                // Check if supply level changed significantly (invalidates cache to prevent stale data)
                var currentSupply = enlistment?.CompanyNeeds?.Supplies ?? 0;
                var supplyChanged = _lastKnownSupplyLevel >= 0 &&
                                   Math.Abs(currentSupply - _lastKnownSupplyLevel) >= 10;

                // Only rebuild narrative every 30 seconds to prevent rapid flickering,
                // unless supply changed significantly (10+ points).
                // This gives the player time to read before text changes while keeping supply reports accurate.
                var minTimeBetweenBuilds = CampaignTime.Seconds(30);
                if (string.IsNullOrEmpty(_cachedMainMenuNarrative) ||
                    CampaignTime.Now - _narrativeLastBuiltAt > minTimeBetweenBuilds ||
                    supplyChanged)
                {
                    _cachedMainMenuNarrative = BuildMainMenuNarrative(enlistment, lord);
                    _narrativeLastBuiltAt = CampaignTime.Now;
                    _lastKnownSupplyLevel = currentSupply;
                }

                // Get menu context and set text variable (only if actually changed)
                var menuContext = args?.MenuContext ?? Campaign.Current.CurrentMenuContext;
                if (menuContext != null)
                {
                    var text = menuContext.GameMenu.GetText();
                    _ = text.SetTextVariable("PARTY_TEXT", _cachedMainMenuNarrative);
                }
                else
                {
                    // Fallback for compatibility
                    MBTextManager.SetTextVariable("PARTY_TEXT", _cachedMainMenuNarrative);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Error refreshing enlisted status", ex);

                // Error fallback
                var menuContext = args?.MenuContext ?? Campaign.Current.CurrentMenuContext;
                if (menuContext != null)
                {
                    var text = menuContext.GameMenu.GetText();
                    _ = text.SetTextVariable("PARTY_LEADER", "Error");
                    _ = text.SetTextVariable("PARTY_TEXT", "Status information unavailable.");
                }
            }
        }

        /// <summary>
        /// Builds the main menu narrative with section headers for Kingdom Reports, Company Reports, and Player Status.
        /// Order: Kingdom (macro) → Camp (local) → Player (personal)
        /// </summary>
        private static string BuildMainMenuNarrative(EnlistmentBehavior enlistment, Hero lord)
        {
            try
            {
                var lordParty = lord?.PartyBelongedTo;
                var news = EnlistedNewsBehavior.Instance;
                var sb = new StringBuilder();

                // SECTION 1: Kingdom Reports (macro - what's happening in the realm)
                var kingdomParagraph = BuildKingdomNarrativeParagraph(enlistment, news);
                if (!string.IsNullOrWhiteSpace(kingdomParagraph))
                {
                    _ = sb.AppendLine("<span style=\"Header\">KINGDOM REPORTS</span>");
                    _ = sb.AppendLine(kingdomParagraph);
                    _ = sb.AppendLine();
                }

                // SECTION 2: Company Reports (local - color-coded keywords)
                var campParagraph = BuildCampNarrativeParagraph(enlistment, lord, lordParty);
                if (!string.IsNullOrWhiteSpace(campParagraph))
                {
                    _ = sb.AppendLine("<span style=\"Header\">COMPANY REPORTS</span>");
                    _ = sb.AppendLine(campParagraph);
                    _ = sb.AppendLine();
                }

                // SECTION 3: Player Status (personal - duty, health, notable conditions)
                var youParagraph = BuildPlayerNarrativeParagraph(enlistment);
                if (!string.IsNullOrWhiteSpace(youParagraph))
                {
                    _ = sb.AppendLine("<span style=\"Header\">PLAYER STATUS</span>");
                    _ = sb.AppendLine(youParagraph);
                }

                var result = sb.ToString().TrimEnd();
                return string.IsNullOrWhiteSpace(result) ? "The camp awaits your orders." : result;
            }
            catch
            {
                return "Status unavailable.";
            }
        }

        /// <summary>
        /// Builds a rich 5-sentence kingdom briefing integrating WorldStateAnalyzer, pressures, and recent events.
        /// </summary>
        private static string BuildKingdomNarrativeParagraph(EnlistmentBehavior enlistment, EnlistedNewsBehavior news)
        {
            try
            {
                var kingdom = enlistment?.CurrentLord?.Clan?.Kingdom;
                if (kingdom == null)
                {
                    return string.Empty;
                }

                // Get comprehensive world state
                var worldState = WorldStateAnalyzer.AnalyzeSituation();
                var campLife = CampLifeBehavior.Instance;
                var warStance = worldState.KingdomStance;

                var sentences = new List<string>();

                // Sentence 1-2: Recent kingdom news headlines (context-aware)
                var kingdomItems = news?.GetVisibleKingdomFeedItems(3);
                if (kingdomItems != null && kingdomItems.Count > 0)
                {
                    var mostRecent = kingdomItems[0];
                    var headline = EnlistedNewsBehavior.FormatDispatchForDisplay(mostRecent, true);
                    if (!string.IsNullOrWhiteSpace(headline))
                    {
                        sentences.Add(headline);
                    }

                    if (kingdomItems.Count > 1)
                    {
                        var secondary = kingdomItems[1];
                        var secondLine = EnlistedNewsBehavior.FormatDispatchForDisplay(secondary, false);
                        if (!string.IsNullOrWhiteSpace(secondLine))
                        {
                            sentences.Add(secondLine);
                        }
                    }
                }

                // Sentence 3: War status with WorldStateAnalyzer context
                var enemyKingdoms = Kingdom.All
                    .Where(k => k != kingdom && FactionManager.IsAtWarAgainstFaction(kingdom, k))
                    .ToList();

                if (warStance == WarStance.Desperate && enemyKingdoms.Count > 0)
                {
                    // Desperate war situation
                    var firstTwo = string.Join(" and ", enemyKingdoms.Take(2).Select(k => k.Name?.ToString() ?? ""));
                    var extra = enemyKingdoms.Count > 2 ? $", and {enemyKingdoms.Count - 2} others" : "";
                    sentences.Add($"<span style=\"Alert\">Desperate struggle:</span> {firstTwo}{extra} press from all sides.");
                }
                else if (enemyKingdoms.Count > 0)
                {
                    if (enemyKingdoms.Count == 1)
                    {
                        var enemyName = enemyKingdoms[0].Name?.ToString() ?? "the enemy";
                        var intensity = warStance == WarStance.Offensive ? "active war" : "conflict";
                        sentences.Add($"<span style=\"Warning\">{kingdom.Name} fights {intensity} with {enemyName}.</span>");
                    }
                    else
                    {
                        var firstTwo = string.Join(" and ", enemyKingdoms.Take(2).Select(k => k.Name?.ToString() ?? ""));
                        var extra = enemyKingdoms.Count > 2 ? $", plus {enemyKingdoms.Count - 2} others" : "";
                        sentences.Add($"<span style=\"Alert\">War on {enemyKingdoms.Count} fronts:</span> {firstTwo}{extra}.");
                    }
                }
                else if (sentences.Count == 0)
                {
                    sentences.Add($"<span style=\"Success\">The realm is at peace.</span>");
                }

                // Sentence 4: Siege situation
                var activeSieges = Settlement.All
                    .Count(s => s.IsUnderSiege &&
                               (s.MapFaction == kingdom ||
                                s.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction == kingdom));

                if (activeSieges > 0)
                {
                    var ourSieges = Settlement.All.Count(s => s.IsUnderSiege &&
                                                              s.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction == kingdom);
                    var enemySieges = activeSieges - ourSieges;

                    if (ourSieges > 0 && enemySieges > 0)
                    {
                        sentences.Add($"<span style=\"Warning\">{ourSieges} of our sieges press enemy holds, while {enemySieges} threaten our towns.</span>");
                    }
                    else if (ourSieges > 0)
                    {
                        sentences.Add($"<span style=\"Success\">{ourSieges} siege{(ourSieges > 1 ? "s" : "")} underway against enemy strongholds.</span>");
                    }
                    else
                    {
                        sentences.Add($"<span style=\"Alert\">{enemySieges} of our settlements under siege.</span>");
                    }
                }
                else if (enemyKingdoms.Count > 0)
                {
                    sentences.Add("No major sieges at present; raiding and skirmishing continues.");
                }
                else
                {
                    sentences.Add("Trade routes flourish and lords attend to their fiefs.");
                }

                // Sentence 5: Contextual color based on situation
                if (enemyKingdoms.Count >= 3)
                {
                    sentences.Add("The strain shows in every corner of the kingdom.");
                }
                else if (enemyKingdoms.Count > 0 && activeSieges > 2)
                {
                    sentences.Add("Lords rally their levies as the conflict intensifies.");
                }
                else if (enemyKingdoms.Count > 0)
                {
                    sentences.Add("Veterans speak of a long campaign ahead.");
                }
                else
                {
                    sentences.Add("The swords rest, for now.");
                }

                // Aim for 5 sentences, trim if we went over
                return string.Join(" ", sentences.Take(5));
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a flowing 4-5 sentence player status combining NOW and AHEAD seamlessly with RP flavor.
        /// Blends current state with future predictions using color-coded keywords and culture-aware ranks.
        /// </summary>
        private static string BuildPlayerNarrativeParagraph(EnlistmentBehavior enlistment)
        {
            try
            {
                var sentences = new List<string>();
                var mainHero = Hero.MainHero;
                var orderState = OrderActivity.Instance?.ActiveNamedOrder;
                var lord = enlistment?.CurrentLord;
                var lordParty = lord?.PartyBelongedTo;
                var companyNeeds = enlistment?.CompanyNeeds;

                // Get comprehensive systems state
                var worldState = WorldStateAnalyzer.AnalyzeSituation();
                var campLife = CampLifeBehavior.Instance;
                var lordSituation = worldState.LordIs;
                var activityLevel = worldState.ExpectedActivity;

                // Get culture-aware rank names for RP immersion
                var ncoTitle = GetNCOTitle(lord);
                var officerTitle = GetOfficerTitle(lord);

                // Build flowing NOW + AHEAD narrative combining present state with forecast hints

                // Opening: Current duty state with world-state-aware forecast
                if (orderState != null)
                {
                    var display = OrderDisplayHelper.GetCurrent();
                    var orderTitle = string.IsNullOrEmpty(display?.Title) ? "duty" : display.Title;

                    var hoursSinceIssued = OrderDisplayHelper.GetHoursSinceStarted();

                    if (hoursSinceIssued < 6)
                    {
                        sentences.Add($"<span style=\"Link\">Fresh orders:</span> {orderTitle}. The work lies ahead.");
                    }
                    else if (hoursSinceIssued < 24)
                    {
                        sentences.Add($"<span style=\"Link\">On duty:</span> {orderTitle}, {(int)hoursSinceIssued} hours in. The day wears on.");
                    }
                    else
                    {
                        var days = (int)(hoursSinceIssued / 24);
                        sentences.Add($"<span style=\"Warning\">Long days on duty:</span> {orderTitle}, day {days}. See it through to the end.");
                    }
                }
                else
                {
                    // Off duty - weave in WorldStateAnalyzer-based forecast hints
                    if (lordSituation == LordSituation.SiegeAttacking ||
                        lordSituation == LordSituation.SiegeDefending)
                    {
                        sentences.Add($"<span style=\"Success\">Off duty</span> between siege shifts. The {ncoTitle} watches for the next call to arms.");
                    }
                    else if (activityLevel == ActivityLevel.Intense)
                    {
                        sentences.Add($"<span style=\"Success\">Brief respite.</span> The {officerTitle} will have orders soon enough.");
                    }
                    else if (lordParty?.Army != null)
                    {
                        sentences.Add($"<span style=\"Success\">Off duty</span> while the {officerTitle} plans with other commanders.");
                    }
                    else if (lordSituation == LordSituation.WarMarching)
                    {
                        sentences.Add($"<span style=\"Success\">Off duty</span> but ready. Wartime brings orders without warning.");
                    }
                    else if (lordParty?.TargetSettlement != null)
                    {
                        sentences.Add($"<span style=\"Success\">Off duty</span> as the {ncoTitle} readies the company for the march ahead.");
                    }
                    else if (activityLevel == ActivityLevel.Quiet)
                    {
                        sentences.Add($"<span style=\"Success\">Off duty.</span> Garrison life means quiet hours between the routines.");
                    }
                    else
                    {
                        sentences.Add($"<span style=\"Success\">Off duty.</span> The {ncoTitle} keeps watch while you rest.");
                    }
                }

                // Physical state woven with implications for what's ahead
                if (mainHero != null)
                {
                    var conditionBehavior = Conditions.PlayerConditionBehavior.Instance;
                    var playerCondition = conditionBehavior?.State;

                    // Check for custom injuries first (twisted ankle, etc.)
                    if (playerCondition?.HasInjury == true)
                    {
                        var daysRemaining = playerCondition.InjuryDaysRemaining;
                        var recoveryPhrase = daysRemaining > 3 ? "The healing will take time" : "Recovery underway";
                        sentences.Add($"<span style=\"Alert\">Injured.</span> {recoveryPhrase} — {daysRemaining} days. Training restricted.");
                    }
                    // Check for custom illness
                    else if (playerCondition?.HasIllness == true)
                    {
                        var daysRemaining = playerCondition.IllnessDaysRemaining;
                        sentences.Add($"<span style=\"Alert\">Illness weakens you.</span> {daysRemaining} days to recovery. Seek treatment if it worsens.");
                    }
                    // Check for native wounds
                    else if (mainHero.IsWounded)
                    {
                        sentences.Add("<span style=\"Alert\">Your wounds slow you.</span> Rest now, heal, and ride again when ready.");
                    }
                }

                // Critical player-relevant warnings from company state (supplies impact player directly)
                // Skip shock-based warnings for fresh enlistment (< 1 day) - lord's prior battles shouldn't alarm new recruits
                var freshlyEnlisted = enlistment?.DaysServed < 1f;
                if (companyNeeds != null)
                {
                    var logisticsHigh = !freshlyEnlisted && campLife?.LogisticsStrain > 70;
                    var suppliesLow = companyNeeds.Supplies < 40;

                    // Only show hunger warnings when supplies are actually concerning
                    if (companyNeeds.Supplies < 20)
                    {
                        // Critical supplies - always warn
                        sentences.Add("<span style=\"Alert\">The men whisper of hunger.</span> Rations failing — resupply needed urgently.");
                        ModLogger.LogOnce(
                            $"PlayerStatus_Critical_Day{(int)CampaignTime.Now.ToDays}",
                            "Supply",
                            $"Player Status: Critical supplies ({companyNeeds.Supplies}%) - showing hunger warning");
                    }
                    else if (logisticsHigh && suppliesLow)
                    {
                        // Logistics strained AND supplies concerning - combined warning
                        sentences.Add("<span style=\"Alert\">Logistics collapsing</span> — resupply needed urgently.");
                        var logisticsValue = campLife?.LogisticsStrain ?? 0f;
                        ModLogger.LogOnce(
                            $"PlayerStatus_Logistics_Day{(int)CampaignTime.Now.ToDays}",
                            "Supply",
                            $"Player Status: Logistics warning (supplies={companyNeeds.Supplies}%, logistics={logisticsValue:F0})");
                    }
                }

                // Player recap: brief summary of their personal performance this period
                var playerRecap = BuildBriefPlayerRecap(enlistment);
                if (!string.IsNullOrWhiteSpace(playerRecap))
                {
                    sentences.Add(playerRecap);
                }

                // AHEAD: What's coming up for the player (scheduled activities, expected orders)
                var playerForecast = BuildBriefPlayerForecast();
                if (!string.IsNullOrWhiteSpace(playerForecast) && sentences.Count < 5)
                {
                    sentences.Add(playerForecast);
                }

                // Closing: AHEAD outlook based on WorldStateAnalyzer + present conditions
                if (sentences.Count < 5)
                {
                    var leadership = mainHero?.GetSkillValue(DefaultSkills.Leadership) ?? 0;

                    // Crisis situations take priority
                    if (lordSituation == LordSituation.Defeated)
                    {
                        sentences.Add("The defeat stings, but the company rebuilds. There will be another chance.");
                    }
                    else if (activityLevel == ActivityLevel.Intense && (companyNeeds?.Supplies < 30 || companyNeeds?.Readiness < 30))
                    {
                        sentences.Add("Hard campaigning tests every soldier. Hold the line.");
                    }
                    else if (lordSituation == LordSituation.SiegeAttacking)
                    {
                        sentences.Add("The siege grinds on. Patience and discipline will see it through.");
                    }
                    else if (activityLevel == ActivityLevel.Quiet && leadership >= 100)
                    {
                        sentences.Add("Garrison duty suits a measured leader. The men trust your judgment.");
                    }
                    else if (mainHero?.IsWounded != true)
                    {
                        sentences.Add("<span style=\"Success\">Ready for action.</span> Whatever comes, you'll face it.");
                    }
                }

                return string.Join(" ", sentences.Take(6));
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets culture-appropriate NCO rank name using localization.
        /// </summary>
        private static string GetNCOTitle(Hero lord)
        {
            try
            {
                var culture = lord?.Culture;
                if (culture == null)
                {
                    return "Sergeant";
                }

                var stringId = culture.StringId switch
                {
                    "empire" => "rank_nco_empire",
                    "sturgia" => "rank_nco_sturgia",
                    "aserai" => "rank_nco_aserai",
                    "khuzait" => "rank_nco_khuzait",
                    "battania" => "rank_nco_battania",
                    "vlandia" => "rank_nco_vlandia",
                    _ => "rank_nco_default"
                };

                return new TextObject($"{{={stringId}}}").ToString();
            }
            catch
            {
                return "Sergeant";
            }
        }

        /// <summary>
        /// Gets culture-appropriate officer rank name using localization.
        /// </summary>
        private static string GetOfficerTitle(Hero lord)
        {
            try
            {
                var culture = lord?.Culture;
                if (culture == null)
                {
                    return "Captain";
                }

                var stringId = culture.StringId switch
                {
                    "empire" => "rank_officer_empire",
                    "sturgia" => "rank_officer_sturgia",
                    "aserai" => "rank_officer_aserai",
                    "khuzait" => "rank_officer_khuzait",
                    "battania" => "rank_officer_battania",
                    "vlandia" => "rank_officer_vlandia",
                    _ => "rank_officer_default"
                };

                return new TextObject($"{{={stringId}}}").ToString();
            }
            catch
            {
                return "Captain";
            }
        }

        // Note: Removed legacy section builders (BuildMainMenuKingdomSection, BuildMainMenuCampSummary,
        // BuildMainMenuYouSection) - replaced by paragraph-based BuildMainMenuNarrative()

        // Note: Removed unused GetDynamicStatusMessages, CanPromote, and GetOfficerSkillName methods

        /// <summary>
        ///     Refresh current menu with updated information.
        /// </summary>
        private void RefreshCurrentMenu()
        {
            try
            {
                var menuContext = Campaign.Current?.CurrentMenuContext;
                if (menuContext?.GameMenu != null)
                {
                    // Force menu options to re-evaluate their conditions
                    Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
                    _menuNeedsRefresh = false;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Error refreshing menu", ex);
            }
        }

        #region Menu Condition and Action Methods

        private bool OnEnlistedStatusCondition(MenuCallbackArgs args)
        {
            var isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted == true;

            // Don't refresh in condition methods - they're called too frequently during menu rendering
            // Refresh is handled by the tick method with proper timing validation

            return isEnlisted;
        }

        // Menu Option Conditions and Actions

        /// <summary>
        /// Kept for save compatibility. This option is obsolete and always hidden because formation is chosen via
        /// proving events instead of the Master at Arms menu.
        /// </summary>
        /// <summary>
        /// Kept for save compatibility. This handler is obsolete and should not be reachable in normal play.
        /// </summary>
        [Obsolete("Formation is now chosen via proving events, not the Master at Arms menu.")]
        private void OnMasterAtArmsSelected(MenuCallbackArgs args)
        {
            try
            {
                // Capture current time state - user may have changed it with spacebar since menu opened
                QuartermasterManager.CaptureTimeStateBeforeMenuActivation();

                var manager = TroopSelectionManager.Instance;
                if (manager == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=menu_master_unavailable}Master at Arms system is temporarily unavailable.").ToString()));
                    return;
                }

                manager.ShowMasterAtArmsPopup();
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error opening Master at Arms", ex);
            }
        }

        // IsMyLanceAvailable removed - lance access now via Visit Camp > Lance location

        private void OnQuartermasterSelected(MenuCallbackArgs args)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=menu_must_be_enlisted_qm}You must be enlisted to access quartermaster services.").ToString()));
                    ModLogger.Warn("QUARTERMASTER", "Quartermaster open blocked: player not enlisted");
                    return;
                }

                // Try to open conversation with the Quartermaster hero.
                var qm = enlistment.GetOrCreateQuartermaster();

                if (qm != null && qm.IsAlive)
                {
                    var party = QuartermasterPartyResolver.GetConversationParty(qm);
                    if (party == null)
                    {
                        ModLogger.Surfaced("QUARTERMASTER", "Both QM and enlisted lord have no party — cannot open conversation with correct scene");
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=qm_party_unavailable}The quartermaster cannot be reached right now.").ToString()));
                        return;
                    }

                    ModLogger.Info("QUARTERMASTER",
                        $"Opening conversation with quartermaster '{qm.Name}' ({enlistment.QuartermasterArchetype})");

                    // Open conversation with quartermaster Hero
                    // The dialog tree is registered in EnlistedDialogManager
                    var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
                    var qmData = new ConversationCharacterData(qm.CharacterObject, party);

                    // Use sea conversation scene if at sea, otherwise use map conversation
                    // Mirrors lord conversation behavior for proper scene selection
                    if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
                    {
                        const string seaConversationScene = "conversation_scene_sea_multi_agent";
                        ModLogger.Info("INTERFACE", $"Opening sea conversation with QM using scene: {seaConversationScene}");
                        _ = CampaignMission.OpenConversationMission(playerData, qmData, seaConversationScene);
                    }
                    else
                    {
                        CampaignMapConversation.OpenConversation(playerData, qmData);
                    }
                }
                else
                {
                    // QM hero creation must succeed when enlisted. If we get here, hero spawn failed upstream.
                    ModLogger.Surfaced("QUARTERMASTER", "GetOrCreateQuartermaster returned null or dead hero while enlisted");
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=menu_qm_hero_unavailable}The quartermaster could not be reached. This is a bug — please report it.").ToString()));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error opening quartermaster conversation", ex,
                    ctx: LogCtx.PlayerState());
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=menu_qm_open_failed}Failed to open the quartermaster conversation. Check the log for details.").ToString()));
            }
        }

        /// <summary>
        /// Handles baggage train access menu selection. Routes to direct stash access or QM conversation
        /// based on current baggage access state.
        /// </summary>
        private void OnBaggageTrainSelected(MenuCallbackArgs args)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    // Not enlisted - direct access to personal stash
                    ModLogger.Debug("BAGGAGE", "Baggage access: Not enlisted, opening stash directly");
                    _ = args;
                    _ = enlistment?.TryOpenBaggageTrain();
                    return;
                }

                var baggageManager = BaggageTrainManager.Instance;
                if (baggageManager == null)
                {
                    ModLogger.Warn("BAGGAGE", "BaggageTrainManager not available, opening stash directly");
                    _ = enlistment.TryOpenBaggageTrain();
                    return;
                }

                var accessState = baggageManager.GetCurrentAccess();
                ModLogger.Info("BAGGAGE", $"Baggage access requested, state: {accessState}");

                switch (accessState)
                {
                    case BaggageAccessState.FullAccess:
                    case BaggageAccessState.TemporaryAccess:
                        // Direct access to baggage stash
                        _ = enlistment.TryOpenBaggageTrain();
                        break;

                    case BaggageAccessState.Locked:
                        // State changed since button was shown - inform player
                        ModLogger.Info("BAGGAGE", "Baggage state changed to Locked since button was cached");
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=baggage_now_locked}The baggage train is currently locked down (supply crisis).").ToString(),
                            Colors.Red));
                        break;

                    case BaggageAccessState.NoAccess:
                    default:
                        // State changed since button was shown - inform player
                        ModLogger.Info("BAGGAGE", "Baggage state changed to NoAccess since button was cached");
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=baggage_now_unavailable}The baggage train has fallen behind the column.").ToString(),
                            Colors.Red));
                        break;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error handling baggage train access", ex,
                    ctx: LogCtx.PlayerState());
            }
        }

        private void OnTalkToSelected(MenuCallbackArgs args)
        {
            try
            {
                // Capture current time state - user may have changed it with spacebar since menu opened
                QuartermasterManager.CaptureTimeStateBeforeMenuActivation();

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=menu_must_be_enlisted_lords}You must be enlisted to speak with lords.").ToString()));
                    return;
                }

                // Find nearby lords for conversation
                var nearbyLords = GetNearbyLordsForConversation();
                if (nearbyLords.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=menu_no_lords_available}No lords are available for conversation at this location.").ToString()));
                    return;
                }

                // Show lord selection inquiry
                ShowLordSelectionInquiry(nearbyLords);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error in Talk to My Lord", ex,
                    ctx: LogCtx.PlayerState());
            }
        }

        /// <summary>
        ///     Find nearby lords available for conversation using current TaleWorlds APIs.
        /// </summary>
        private List<Hero> GetNearbyLordsForConversation()
        {
            var nearbyLords = new List<Hero>();
            try
            {
                var mainParty = MobileParty.MainParty;
                if (mainParty == null)
                {
                    return nearbyLords;
                }

                // Check all mobile parties using verified API
                foreach (var party in MobileParty.All)
                {
                    if (party == null || party == mainParty || !party.IsActive)
                    {
                        continue;
                    }

                    // Check if party is close enough for conversation (same position or very close)
                    // 1.3.4 API: Position2D is now GetPosition2D property
                    var distance = mainParty.GetPosition2D.Distance(party.GetPosition2D);
                    if (distance > 2.0f) // Reasonable conversation distance
                    {
                        continue;
                    }

                    var lord = party.LeaderHero;
                    if (lord is { IsLord: true, IsAlive: true, IsPrisoner: false })
                    {
                        nearbyLords.Add(lord);
                    }
                }

                // Always include your enlisted lord if available
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.CurrentLord != null && !nearbyLords.Contains(enlistment.CurrentLord))
                {
                    nearbyLords.Insert(0, enlistment.CurrentLord); // Put enlisted lord first
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Error finding nearby lords", ex);
            }

            return nearbyLords;
        }

        /// <summary>
        ///     Show lord selection inquiry with portraits.
        /// </summary>
        private void ShowLordSelectionInquiry(List<Hero> lords)
        {
            try
            {
                var options = new List<InquiryElement>();
                var unknown = new TextObject("{=enl_ui_unknown}Unknown").ToString();
                foreach (var lord in lords)
                {
                    var name = lord.Name?.ToString() ?? unknown;
                    // 1.3.4 API: ImageIdentifier is now abstract, use CharacterImageIdentifier
                    var portrait = new CharacterImageIdentifier(CharacterCode.CreateFrom(lord.CharacterObject));
                    var description =
                        $"{lord.Clan?.Name?.ToString() ?? unknown}\n{lord.MapFaction?.Name?.ToString() ?? unknown}";

                    options.Add(new InquiryElement(lord, name, portrait, true, description));
                }

                var data = new MultiSelectionInquiryData(
                    titleText: new TextObject("{=enl_ui_select_lord_title}Select lord to speak with").ToString(),
                    descriptionText: string.Empty,
                    inquiryElements: options,
                    isExitShown: true,
                    minSelectableOptionCount: 1,
                    maxSelectableOptionCount: 1,
                    affirmativeText: new TextObject("{=enl_ui_talk}Talk").ToString(),
                    negativeText: new TextObject("{=enl_ui_cancel}Cancel").ToString(),
                    affirmativeAction: selected =>
                    {
                        try
                        {
                            // Use pattern matching for cleaner type check
                            if (selected?.FirstOrDefault()?.Identifier is Hero chosenLord)
                            {
                                StartConversationWithLord(chosenLord);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("INTERFACE", "Error starting lord conversation", ex);
                        }
                    },
                    negativeAction: _ =>
                    {
                        // Cancel action - just close popup, don't affect menu or time state
                        // The enlisted_status menu is already active underneath
                    },
                    soundEventPath: string.Empty);

                MBInformationManager.ShowMultiSelectionInquiry(data);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error showing lord selection", ex,
                    ctx: LogCtx.PlayerState());
            }
        }

        /// <summary>
        ///     Start conversation with selected lord using verified TaleWorlds APIs.
        ///     Uses different conversation systems for land vs sea to ensure proper scene selection.
        /// </summary>
        private void StartConversationWithLord(Hero lord)
        {
            try
            {
                if (lord?.PartyBelongedTo == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=menu_lord_not_available}Lord is not available for conversation.").ToString()));
                    return;
                }

                var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
                var lordData = new ConversationCharacterData(lord.CharacterObject, lord.PartyBelongedTo.Party);

                // At sea: use mission-based conversation with proper ship scene
                // On land: use map conversation (character portraits)
                // This mirrors PlayerEncounter behavior for proper scene selection
                if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
                {
                    // Use Naval DLC's sea conversation scene for proper ship deck visuals
                    const string seaConversationScene = "conversation_scene_sea_multi_agent";
                    ModLogger.Info("INTERFACE", $"Opening sea conversation with {lord.Name} using scene: {seaConversationScene}");
                    _ = CampaignMission.OpenConversationMission(playerData, lordData, seaConversationScene);
                }
                else
                {
                    // Standard land conversation using map conversation system
                    CampaignMapConversation.OpenConversation(playerData, lordData);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error opening conversation with lord", ex,
                    ctx: LogCtx.Of("Lord", lord?.Name?.ToString()));
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=menu_conversation_error}Unable to start conversation. Please try again.").ToString()));
            }
        }

        private void OnTalkToCompanionSelected(MenuCallbackArgs args)
        {
            try
            {
                QuartermasterManager.CaptureTimeStateBeforeMenuActivation();

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=enlisted_camp_talk_to_companion_not_enlisted}You must be enlisted to speak with companions.").ToString()));
                    return;
                }

                var spawned = CompanionLifecycleHandler.Instance?.GetSpawnedCompanions();
                if (spawned == null || spawned.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=enlisted_camp_talk_to_companion_none_available}No companions are available for conversation.").ToString()));
                    return;
                }

                ShowCompanionSelectionInquiry(spawned);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error in Talk to Companion", ex,
                    ctx: LogCtx.PlayerState());
            }
        }

        private void ShowCompanionSelectionInquiry(List<Hero> companions)
        {
            try
            {
                var elements = new List<InquiryElement>();
                var unknown = new TextObject("{=enl_ui_unknown}Unknown").ToString();
                foreach (var hero in companions)
                {
                    if (hero == null) continue;
                    var name = hero.Name?.ToString() ?? unknown;
                    var portrait = new CharacterImageIdentifier(CharacterCode.CreateFrom(hero.CharacterObject));
                    var typeId = EnlistmentBehavior.Instance?.GetCompanionTypeId(hero) ?? string.Empty;
                    var description = string.IsNullOrEmpty(typeId) ? string.Empty : typeId.Replace('_', ' ');

                    elements.Add(new InquiryElement(hero, name, portrait, true, description));
                }

                if (elements.Count == 0)
                {
                    return;
                }

                var data = new MultiSelectionInquiryData(
                    titleText: new TextObject("{=enlisted_camp_talk_to_companion_title}Speak with...").ToString(),
                    descriptionText: string.Empty,
                    inquiryElements: elements,
                    isExitShown: true,
                    minSelectableOptionCount: 1,
                    maxSelectableOptionCount: 1,
                    affirmativeText: new TextObject("{=enlisted_camp_talk_to_companion_speak}Speak").ToString(),
                    negativeText: new TextObject("{=enlisted_camp_talk_to_companion_back}Back").ToString(),
                    affirmativeAction: selected =>
                    {
                        try
                        {
                            if (selected?.FirstOrDefault()?.Identifier is Hero companion)
                            {
                                StartConversationWithCompanion(companion);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("INTERFACE", "Error starting companion conversation", ex);
                        }
                    },
                    negativeAction: null);

                MBInformationManager.ShowMultiSelectionInquiry(data, true);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error showing companion selection inquiry", ex);
            }
        }

        private void StartConversationWithCompanion(Hero companion)
        {
            try
            {
                if (companion == null || !companion.IsAlive)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=enlisted_camp_talk_to_companion_unavailable}Companion is not available for conversation.").ToString()));
                    return;
                }

                var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
                var mainParty = MobileParty.MainParty;
                var companionData = new ConversationCharacterData(companion.CharacterObject, mainParty?.Party);

                if (mainParty?.IsCurrentlyAtSea == true)
                {
                    const string seaConversationScene = "conversation_scene_sea_multi_agent";
                    ModLogger.Info("INTERFACE", $"Opening sea conversation with companion {companion.Name} using scene: {seaConversationScene}");
                    _ = CampaignMission.OpenConversationMission(playerData, companionData, seaConversationScene);
                }
                else
                {
                    CampaignMapConversation.OpenConversation(playerData, companionData);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error opening conversation with companion", ex,
                    ctx: LogCtx.Of("Companion", companion?.Name?.ToString()));
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=menu_conversation_error}Unable to start conversation. Please try again.").ToString()));
            }
        }

        /// <summary>
        ///     Tracks when the lord enters settlements to adjust menu option visibility.
        ///     Used to show/hide certain menu options based on settlement entry state.
        /// </summary>
        private void OnSettlementEnteredForButton(MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted == true &&
                    party == enlistment.CurrentLord?.PartyBelongedTo &&
                    (settlement.IsTown || settlement.IsCastle))
                {
                    ModLogger.Debug("INTERFACE", $"Lord entered {settlement.Name} - Visit option available");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Error in settlement entered tracking", ex);
            }
        }

        /// <summary>
        ///     Handles the player selecting "Visit Settlement" from the enlisted status menu.
        ///     Creates a synthetic outside encounter to allow settlement exploration for enlisted soldiers.
        ///     This enables players to visit towns and castles while maintaining their enlisted status.
        /// </summary>
        private void OnVisitTownSelected(MenuCallbackArgs args)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                var lord = enlistment.CurrentLord;
                var settlement = lord?.CurrentSettlement;
                if (settlement == null || (!settlement.IsTown && !settlement.IsCastle))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=menu_lord_not_in_settlement}Your lord is not in a town or castle.").ToString()));
                    return;
                }

                // If we're already on an outside menu, just reshow it.
                var activeId = Campaign.Current.CurrentMenuContext?.GameMenu?.StringId;
                if (activeId == "town_outside" || activeId == "castle_outside")
                {
                    // CRITICAL: Set flag so OnMenuOpened doesn't override back to enlisted_status
                    HasExplicitlyVisitedSettlement = true;
                    QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                    NextFrameDispatcher.RunNextFrame(() => GameMenu.SwitchToMenu(activeId));
                    return;
                }

                // If an encounter exists and it's already for this settlement, just switch.
                if (PlayerEncounter.Current != null &&
                    PlayerEncounter.EncounterSettlement == settlement)
                {
                    // CRITICAL: Set flag so OnMenuOpened doesn't override back to enlisted_status
                    HasExplicitlyVisitedSettlement = true;
                    QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                    NextFrameDispatcher.RunNextFrame(() =>
                        GameMenu.SwitchToMenu(settlement.IsTown ? "town_outside" : "castle_outside"));
                    ModLogger.Info("INTERFACE", $"Visit Settlement: Using existing encounter for {settlement.Name}");
                    return;
                }

                // Clear our own enlisted wait menu off the stack (deferred to next frame)
                if (Campaign.Current.CurrentMenuContext?.GameMenu?.StringId.StartsWith("enlisted_") == true)
                {
                    NextFrameDispatcher.RunNextFrame(() => GameMenu.ExitToLast());
                }

                // BATTLE-AWARE: Only finish non-battle encounters to prevent killing vanilla battle menus
                if (PlayerEncounter.Current != null)
                {
                    var enlistedLord3 = EnlistmentBehavior.Instance?.CurrentLord;
                    var lordInBattle3 = enlistedLord3?.PartyBelongedTo?.Party.MapEvent != null;

                    if (lordInBattle3)
                    {
                        ModLogger.Debug("INTERFACE",
                            "Skipped finishing encounter - lord in battle, preserving vanilla battle menu");
                        return; // Don't create settlement encounter during battles!
                    }

                    // Finish current encounter (if safe), then start a new one next frame
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        var lordParty3 = EnlistmentBehavior.Instance?.CurrentLord?.PartyBelongedTo;
                        if (!InBattleOrSiege(lordParty3))
                        {
                            PlayerEncounter.Finish();
                            ModLogger.Debug("INTERFACE", "Finished non-battle encounter before settlement access");
                        }
                        else
                        {
                            ModLogger.Debug("INTERFACE",
                                "SKIPPED finishing encounter - lord in battle/siege, preserving vanilla battle menu");
                        }
                    });
                }

                // TEMPORARILY activate the main party so the engine can attach an encounter.
                var needActivate = !MobileParty.MainParty.IsActive;

                // Set the flag BEFORE deferring to prevent race condition with menu system
                _syntheticOutsideEncounter = true;
                HasExplicitlyVisitedSettlement = true;

                // Start a clean outside encounter for the player at the lord's settlement (deferred)
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    if (needActivate)
                    {
                        MobileParty.MainParty.IsActive = true;
                    }

                    EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);
                });

                // The engine will have pushed the outside menu; show it explicitly for safety (deferred)
                QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    GameMenu.SwitchToMenu(settlement.IsTown ? "town_outside" : "castle_outside");
                    ModLogger.Info("INTERFACE", $"Started synthetic outside encounter for {settlement.Name}");
                });
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "VisitTown failed", ex,
                    ctx: LogCtx.PlayerState());
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=menu_town_interface_error}Couldn't open the town interface.").ToString()));
            }
        }

        #region Desertion Menu

        #endregion

        /// <summary>
        ///     Tick handler for real-time menu updates.
        ///     Called every frame while the enlisted status menu is active to update
        ///     dynamic information and handle menu transitions based on game state.
        ///     Includes time delta validation to prevent assertion failures.
        /// </summary>
        private void OnEnlistedStatusTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                // If no time state was captured yet (menu opened via native encounter system),
                // capture current time now so we have a baseline for restoration
                if (!QuartermasterManager.CapturedTimeMode.HasValue && Campaign.Current != null)
                {
                    QuartermasterManager.CapturedTimeMode = Campaign.Current.TimeControlMode;
                }

                // NOTE: Time mode restoration is handled ONCE in OnEnlistedStatusInit, not here.
                // Previously this tick handler would restore CapturedTimeMode whenever it saw
                // UnstoppableFastForward, but this fought with user input - when the user clicked
                // fast forward (which sets UnstoppableFastForward for army members), the next tick
                // would immediately restore it back to Stop. This caused x3 speed to pause.
                // The fix is to only handle time mode conversion once during menu init.

                // Validate time delta to prevent assertion failures
                // Zero-delta-time updates can cause assertion failures in the rendering system
                if (dt.ToSeconds <= 0)
                {
                    return;
                }

                // Skip all processing if the player is not currently enlisted
                if (!EnlistmentBehavior.Instance?.IsEnlisted == true)
                {
                    GameMenu.ExitToLast();
                    return;
                }

                // Check if native wants a different menu (e.g., siege menus when siege starts).
                // This mirrors native menu tick handlers like game_menu_siege_strategies_on_tick
                // which call GetGenericStateMenu() and switch away if needed.
                // Our GenericStateMenuPatch will return early for siege/battle menus when lordInSiege,
                // allowing native menus to take over when appropriate.
                var encounterModel = Campaign.Current?.Models?.EncounterGameMenuModel;
                if (encounterModel != null)
                {
                    var desiredMenu = encounterModel.GetGenericStateMenu();

                    // If native wants a different menu (not enlisted_status), switch to it
                    if (!string.IsNullOrEmpty(desiredMenu) &&
                        desiredMenu != "enlisted_status" &&
                        desiredMenu != "enlisted_battle_wait")
                    {
                        ModLogger.Info("MENU",
                            $"OnEnlistedStatusTick: Native wants '{desiredMenu}' - yielding to native menu system");

                        // Switch to the native menu (e.g., menu_siege_strategies, army_wait during siege)
                        GameMenu.SwitchToMenu(desiredMenu);
                        return;
                    }
                }

                // Don't do menu transitions during active encounters
                // This prevents assertion failures that can occur during encounter state transitions
                // When an encounter is active (like paying a bandit), let the native system handle menu transitions
                var hasActiveEncounter = PlayerEncounter.Current != null;

                // If an encounter is active, leave transitions to the native system.
                // We simply keep the menu data fresh so the player still sees updated info.
                if (hasActiveEncounter)
                {
                    if (CampaignTime.Now - _lastMenuUpdate > CampaignTime.Seconds((long)_updateIntervalSeconds))
                    {
                        RefreshEnlistedStatusDisplay(args);
                        _lastMenuUpdate = CampaignTime.Now;
                    }

                    return;
                }

                // No encounter in progress - just refresh the display periodically.
                if (CampaignTime.Now - _lastMenuUpdate > CampaignTime.Seconds((long)_updateIntervalSeconds))
                {
                    RefreshEnlistedStatusDisplay(args);
                    _lastMenuUpdate = CampaignTime.Now;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Error during enlisted status tick", ex);
            }
        }



        #region Orders Integration

        /// <summary>
        /// Refreshes the enlisted status menu UI to reflect current state changes.
        /// Public entry point so other behaviors can trigger a menu refresh when
        /// underlying state changes (e.g., a new order arrives and the accordion
        /// should auto-expand).
        /// </summary>
        public void RefreshEnlistedStatusMenuUi()
        {
            try
            {
                RefreshEnlistedStatusDisplay();

                var menuContext = Campaign.Current?.CurrentMenuContext;
                if (Campaign.Current != null && menuContext?.GameMenu != null)
                {
                    Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
                }
            }
            catch
            {
                // Best-effort only; never let a UI refresh throw from a button callback.
            }
        }

        private void ToggleOrdersAccordion(MenuCallbackArgs args)
        {
            try
            {
                if (!OrderDisplayHelper.IsOrderActive())
                {
                    _ordersCollapsed = true;
                    return;
                }

                _ordersCollapsed = !_ordersCollapsed;

                // Re-render header/report text and then force the menu to re-evaluate option visibility.
                RefreshEnlistedStatusDisplay(args);

                var menuContext = args?.MenuContext ?? Campaign.Current?.CurrentMenuContext;
                if (Campaign.Current != null && menuContext?.GameMenu != null)
                {
                    Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Failed to toggle orders accordion", ex);
            }
        }


        /// <summary>
        /// Shows a read-only details dialog for the currently active named order.
        /// The outer accordion row is gated on an active order, so this method is
        /// only reachable when a named order is in flight; the internal null guard
        /// is defensive against races between row render and click handler.
        /// Named orders run to completion via the storylet arc — there is no
        /// accept/decline surface in the new model.
        /// </summary>
        private void ShowOrdersMenu()
        {
            try
            {
                var display = OrderDisplayHelper.GetCurrent();
                if (display == null)
                {
                    return;
                }

                var sb = new StringBuilder();
                _ = sb.AppendLine(display.Title);

                if (!string.IsNullOrEmpty(display.Intent))
                {
                    _ = sb.AppendLine();
                    _ = sb.AppendLine(new TextObject("{=enlisted_orders_details_intent}Intent: {INTENT}")
                        .SetTextVariable("INTENT", display.Intent).ToString());
                }

                if (display.HoursTotal > 0)
                {
                    _ = sb.AppendLine();
                    _ = sb.AppendLine(new TextObject("{=enlisted_orders_details_progress}Progress: {ELAPSED} of {TOTAL} hours")
                        .SetTextVariable("ELAPSED", display.HoursElapsed.ToString())
                        .SetTextVariable("TOTAL", display.HoursTotal.ToString()).ToString());
                }

                var title = new TextObject("{=enlisted_orders_details_title}Order Details").ToString();
                var closeText = new TextObject("{=enlisted_orders_details_close}Close").ToString();

                InformationManager.ShowInquiry(new InquiryData(
                    titleText: title,
                    text: sb.ToString(),
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: false,
                    affirmativeText: closeText,
                    negativeText: null,
                    affirmativeAction: null,
                    negativeAction: null
                ), true);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("INTERFACE", "Error showing orders menu", ex);
            }
        }

        #endregion

        #region Localization Helpers

        /// <summary>
        /// Returns the localized name for a day phase.
        /// </summary>
        private static string GetLocalizedPhaseName(DayPhase phase)
        {
            return phase switch
            {
                DayPhase.Dawn => new TextObject("{=phase_dawn}dawn").ToString(),
                DayPhase.Midday => new TextObject("{=phase_midday}midday").ToString(),
                DayPhase.Dusk => new TextObject("{=phase_dusk}dusk").ToString(),
                DayPhase.Night => new TextObject("{=phase_night}night").ToString(),
                _ => phase.ToString().ToLower()
            };
        }

        #endregion

        // Note: Removed unused Military Styling Helper Methods region (GetFormationSymbol, GetProgressBar)
    }

    /// <summary>
    ///     Extension methods for string formatting.
    /// </summary>
    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }

        #endregion
    }
}
