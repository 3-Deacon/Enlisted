using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Entry;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Combat.Behaviors
{
    /// <summary>
    ///     Handles encounter menu extensions and battle participation for enlisted soldiers.
    ///     Adds "Wait in reserve" option for large field battles (100+ troops).
    ///     Native system handles siege menus automatically.
    /// </summary>
    public sealed class EnlistedEncounterBehavior : CampaignBehaviorBase
    {
        public EnlistedEncounterBehavior()
        {
            Instance = this;
        }

        public static EnlistedEncounterBehavior Instance { get; private set; }

        /// <summary>
        ///     Tracks whether the player is currently waiting in reserve during a battle.
        ///     Used to prevent XP awards when entering reserve mode (OnPlayerBattleEnd fires but battle isn't actually over).
        /// </summary>
        public static bool IsWaitingInReserve { get; private set; }
        
        /// <summary>
        ///     Clears the reserve state flag. Called when the lord is captured while player is in reserve,
        ///     allowing the capture to proceed cleanly.
        /// </summary>
        public static void ClearReserveState()
        {
            IsWaitingInReserve = false;
        }


        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            // NOTE: Menu stability is now handled by GenericStateMenuPatch which overrides
            // GetGenericStateMenu() to return "enlisted_battle_wait" when in reserve mode.
            // This prevents native systems from even wanting to switch menus.
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent state needed
        }

        // NOTE: OnHeroPrisonerTaken handler removed - capture logic is handled by
        // EnlistmentBehavior.TryCapturePlayerAlongsideLord which properly integrates
        // with the grace period system. Adding a second handler here caused crashes
        // due to conflicting state management.

        private void OnSessionLaunched(CampaignGameStarter campaignStarter)
        {
            try
            {
                // Reset static state from previous session to prevent stale flag issues
                // If player quit while in reserve mode, flag would persist across save loads
                IsWaitingInReserve = false;
                _postBattleCleanupScheduled = false;
                _lastCleanupScheduledTime = CampaignTime.Never;

                AddEnlistedEncounterOptions(campaignStarter);
                ModLogger.LogOnce("encounter_behavior_init", "COMBAT",
                    "Encounter behavior initialized with modern UI styling - battle wait menu and reserve options ready");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMBAT", "Failed to initialize encounter behavior", ex);
            }
        }
        
        /// <summary>
        /// Menu background initialization for enlisted_battle_wait menu.
        /// Sets culture-appropriate background and ambient audio for battle wait.
        /// </summary>
        [GameMenuInitializationHandler("enlisted_battle_wait")]
        [SuppressMessage("ReSharper", "UnusedMember.Local", 
            Justification = "Called by Bannerlord engine via GameMenuInitializationHandler attribute")]
        private static void OnBattleWaitBackgroundInit(MenuCallbackArgs args)
        {
            var lord = EnlistmentBehavior.Instance?.CurrentLord;
            var backgroundMesh = "encounter_looter";
            
            if (lord?.Clan?.Kingdom?.Culture?.EncounterBackgroundMesh != null)
            {
                backgroundMesh = lord.Clan.Kingdom.Culture.EncounterBackgroundMesh;
            }
            else if (lord?.Culture?.EncounterBackgroundMesh != null)
            {
                backgroundMesh = lord.Culture.EncounterBackgroundMesh;
            }
            
            args.MenuContext.SetBackgroundMeshName(backgroundMesh);
            args.MenuContext.SetAmbientSound("event:/map/ambient/node/settlements/2d/camp_army");
        }

        /// <summary>
        ///     Registers military-specific menu options for battles.
        ///     Adds "Wait in reserve" option for large field battles (100+ troops).
        ///     This option appears in the encounter menu when the player is enlisted and their lord is in battle.
        ///     Native system handles siege menus automatically.
        /// </summary>
        private void AddEnlistedEncounterOptions(CampaignGameStarter starter)
        {
            // Add "Wait in reserve" option for large battles (100+ troops)
            // This allows players to stay out of the initial fighting in large battles
            var waitInReserveText = new TextObject("{=combat_wait_reserve}Wait in reserve");

            // Add to the encounter menu for field battles (Wait icon)
            starter.AddGameMenuOption("encounter", "enlisted_wait_reserve",
                waitInReserveText.ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                    return IsWaitInReserveAvailable(args);
                },
                OnWaitInReserveSelected,
                false, 1);

            // Also add to join_siege_event menu for siege battles
            // When player is injured or chooses to sit out, they can wait in reserve
            starter.AddGameMenuOption("join_siege_event", "enlisted_siege_wait_reserve",
                waitInReserveText.ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                    return IsWaitInReserveAvailable(args);
                },
                OnWaitInReserveSelected,
                false, 1);

            // Also add to menu_siege_strategies (native siege preparation menu)
            // For when the lord is commanding a siege and player wants to wait
            starter.AddGameMenuOption("menu_siege_strategies", "enlisted_siege_strategies_wait_reserve",
                waitInReserveText.ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                    return IsWaitInReserveAvailable(args);
                },
                OnWaitInReserveSelected,
                false, 1);

            // Add a custom "wait in reserve" menu for battles
            // This menu shows while the player is waiting in reserve and allows them to rejoin
            // Use shorter overload - MenuOverlayType.None is default
            starter.AddWaitGameMenu("enlisted_battle_wait",
                "Waiting in Reserve: {BATTLE_STATUS}",
                OnBattleWaitInit,
                OnBattleWaitCondition,
                null,
                OnBattleWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Return to battle option (Mission icon for combat)
            var rejoinBattleText = new TextObject("{=combat_rejoin_battle}Rejoin the battle");
            starter.AddGameMenuOption("enlisted_battle_wait", "enlisted_rejoin_battle",
                rejoinBattleText.ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Mission;
                    return true;
                },
                OnRejoinBattleSelected,
                false, 1);
        }

        /// <summary>
        ///     Checks if the "Wait in Reserve" option should be available in the encounter menu.
        ///     This option is only available for enlisted soldiers in large battles (100+ troops),
        ///     allowing players to stay out of the initial fighting when the army is large enough.
        /// </summary>
        /// <param name="args">Menu callback arguments containing menu state and context.</param>
        /// <returns>True if the option should be available, false otherwise.</returns>
        private bool IsWaitInReserveAvailable(MenuCallbackArgs args)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return false;
            }

            var lord = enlistment!.CurrentLord;
            if (lord == null)
            {
                return false;
            }

            var lordParty = lord.PartyBelongedTo;
            var mapEvent = lordParty?.Party.MapEvent;
            var siegeEvent = lordParty?.Party.SiegeEvent;
            
            // DIAGNOSTIC: Log why Wait in Reserve is being shown
            var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "unknown";
            var lordName = lord.Name?.ToString() ?? "null";
            var hasMapEvent = mapEvent != null;
            var hasSiegeEvent = siegeEvent != null;
            
            ModLogger.Info("ENCOUNTERGUARD", 
                $"WAIT_IN_RESERVE CHECK: Menu={currentMenu}, Lord={lordName}, MapEvent={hasMapEvent}, SiegeEvent={hasSiegeEvent}");

            // Two valid contexts for "Wait in Reserve":
            // 1. Field battle: MapEvent exists, not a siege
            // 2. Pre-assault siege menu: No MapEvent yet, but SiegeEvent exists (join_siege_event menu)
            var hasFieldBattle = mapEvent != null && !mapEvent.IsSiegeAssault && mapEvent.EventType != MapEvent.BattleTypes.Siege;
            var hasPreAssaultSiege = mapEvent == null && siegeEvent != null;
            
            // CRITICAL: Check if battle is already over (auto-resolved) - don't show Wait in Reserve, trigger cleanup
            if (mapEvent != null && (mapEvent.HasWinner || mapEvent.IsFinalized))
            {
                var winnerSide = mapEvent.WinningSide;
                ModLogger.Info("ENCOUNTERGUARD", 
                    $"WAIT_IN_RESERVE: Battle already ended (HasWinner={mapEvent.HasWinner}, IsFinalized={mapEvent.IsFinalized}, WinningSide={winnerSide}) - triggering auto-cleanup");
                
                // Trigger deferred cleanup of the stale encounter
                TriggerPostBattleCleanup();
                return false;
            }

            if (!hasFieldBattle && !hasPreAssaultSiege)
            {
                // Not in a valid context - either no battle/siege, or in active siege assault
                if (mapEvent != null && (mapEvent.IsSiegeAssault || mapEvent.EventType == MapEvent.BattleTypes.Siege))
                {
                    // Active siege assault - not allowed
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=combat_reserve_siege_disabled}Wait in reserve is not available during active siege assault battles");
                    ModLogger.Info("ENCOUNTERGUARD", "WAIT_IN_RESERVE: Disabled - active siege assault");
                }
                else
                {
                    ModLogger.Info("ENCOUNTERGUARD", "WAIT_IN_RESERVE: Not available - no valid battle or siege context");
                }
                return false;
            }

            // BUGFIX: Allow waiting in reserve if player morale is too low to fight, regardless of troop count
            if (MobileParty.MainParty.Morale <= 1f)
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                args.Tooltip = new TextObject("{=combat_too_demoralized}You are too demoralized to fight.");
                ModLogger.Info("ENCOUNTERGUARD", "WAIT_IN_RESERVE: Available (demoralized)");
                return true;
            }

            ModLogger.Info("ENCOUNTERGUARD", "WAIT_IN_RESERVE: Available (standard)");
            args.optionLeaveType = GameMenuOption.LeaveType.Wait;
            return true;
        }

        /// <summary>
        ///     Tracks whether a deferred cleanup is already scheduled to prevent duplicate cleanups.
        /// </summary>
        private static bool _postBattleCleanupScheduled;
        
        /// <summary>
        ///     Timestamp when cleanup was last scheduled (for race condition detection).
        /// </summary>
        private static CampaignTime _lastCleanupScheduledTime = CampaignTime.Never;
        
        /// <summary>
        ///     Triggers cleanup of a stale encounter after the battle has already ended.
        ///     This handles the case where auto-resolve completes but the encounter menu stays open.
        ///     Called when Wait in Reserve detects the battle is already won.
        ///     
        ///     CRITICAL: Cleanup is DEFERRED to the next frame because this method is called during
        ///     menu condition evaluation. Modifying encounter state synchronously during menu rendering
        ///     causes native code crashes in MenuHelper.CheckEnemyAttackableHonorably when it tries
        ///     to evaluate other menu options with corrupted state.
        ///     
        ///     DEBUGGING: If crash occurs in MenuHelper.CheckEnemyAttackableHonorably, look for:
        ///     1. "AUTO-CLEANUP: Scheduling deferred cleanup" log line (should appear)
        ///     2. "AUTO-CLEANUP: Executing deferred cleanup" with delay &lt; 0.001s (indicates NextFrameDispatcher failed to defer)
        ///     3. Multiple cleanup attempts with same mapEventId (indicates duplicate cleanup race)
        ///     4. OnMapEventEnded logs interleaved with AUTO-CLEANUP logs (indicates both paths running simultaneously)
        /// </summary>
        private void TriggerPostBattleCleanup()
        {
            // DIAGNOSTIC: Capture context for debugging race conditions
            var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none";
            var hasEncounter = PlayerEncounter.Current != null;
            var mapEventId = MobileParty.MainParty?.Party?.MapEvent?.GetHashCode().ToString() ?? "none";
            
            // Guard against duplicate cleanup scheduling
            if (_postBattleCleanupScheduled)
            {
                var timeSinceLastSchedule = CampaignTime.Now - _lastCleanupScheduledTime;
                ModLogger.Debug("ENCOUNTERGUARD", 
                    $"AUTO-CLEANUP: Already scheduled {timeSinceLastSchedule.ToSeconds:F2}s ago, skipping duplicate (menu={currentMenu}, mapEvent={mapEventId})");
                return;
            }
            
            _postBattleCleanupScheduled = true;
            _lastCleanupScheduledTime = CampaignTime.Now;
            
            // DIAGNOSTIC: Log that we're deferring cleanup during menu rendering to prevent crash
            ModLogger.Info("ENCOUNTERGUARD", 
                $"AUTO-CLEANUP: Scheduling deferred cleanup for next frame (currentMenu={currentMenu}, hasEncounter={hasEncounter}, mapEventId={mapEventId})");
            
            // CRITICAL: Defer to next frame to avoid modifying state during menu condition evaluation
            // The crash occurs because we're called from GetConditionsHold() during menu rendering,
            // and modifying encounter state corrupts the menu refresh loop.
            // 
            // If this log appears immediately before a crash, it indicates cleanup is somehow
            // executing synchronously instead of being deferred (NextFrameDispatcher bug).
            NextFrameDispatcher.RunNextFrame(() =>
            {
                _postBattleCleanupScheduled = false;
                ExecutePostBattleCleanup(currentMenu, mapEventId);
            });
        }
        
        /// <summary>
        ///     Executes the actual post-battle cleanup logic. Called from next frame dispatch.
        /// </summary>
        /// <param name="originalMenu">The menu ID when cleanup was triggered (for diagnostics).</param>
        /// <param name="originalMapEventId">The map event ID when cleanup was triggered (for race detection).</param>
        private void ExecutePostBattleCleanup(string originalMenu, string originalMapEventId)
        {
            try
            {
                // DIAGNOSTIC: Capture current state to detect if things changed during the defer
                var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none";
                var currentMapEventId = MobileParty.MainParty?.Party?.MapEvent?.GetHashCode().ToString() ?? "none";
                var hasEncounter = PlayerEncounter.Current != null;
                var timeSinceScheduled = CampaignTime.Now - _lastCleanupScheduledTime;
                
                ModLogger.Info("ENCOUNTERGUARD", 
                    $"AUTO-CLEANUP: Executing deferred cleanup (delay={timeSinceScheduled.ToSeconds:F3}s, originalMenu={originalMenu}, currentMenu={currentMenu}, hasEncounter={hasEncounter})");
                
                // DIAGNOSTIC: Detect if map event changed during defer (indicates OnMapEventEnded already ran)
                if (originalMapEventId != "none" && currentMapEventId != originalMapEventId)
                {
                    ModLogger.Info("ENCOUNTERGUARD", 
                        $"AUTO-CLEANUP: MapEvent changed during defer (was={originalMapEventId}, now={currentMapEventId}) - OnMapEventEnded likely already handled cleanup");
                }
                
                // Check if cleanup is still needed - OnMapEventEnded may have already handled it
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    ModLogger.Debug("ENCOUNTERGUARD", "AUTO-CLEANUP: No longer enlisted, skipping (OnMapEventEnded likely handled it)");
                    return;
                }
                
                // DIAGNOSTIC: Warn if encounter already cleaned up
                if (!hasEncounter)
                {
                    ModLogger.Debug("ENCOUNTERGUARD", "AUTO-CLEANUP: No PlayerEncounter exists (OnMapEventEnded likely already cleaned it up)");
                }
                
                // Clean up the encounter state
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.LeaveEncounter = true;
                    if (PlayerEncounter.InsideSettlement)
                    {
                        PlayerEncounter.LeaveSettlement();
                    }
                    PlayerEncounter.Finish();
                    ModLogger.Info("ENCOUNTERGUARD", "AUTO-CLEANUP: PlayerEncounter finished");
                }
                
                // Deactivate player party to prevent further stale encounters
                var mainParty = MobileParty.MainParty;
                if (mainParty != null && enlistment.IsEnlisted)
                {
                    mainParty.IsActive = false;
                    mainParty.IsVisible = false;
                    ModLogger.Info("ENCOUNTERGUARD", "AUTO-CLEANUP: Deactivated party");
                }
                
                // Clear the reserve state flag if set
                ClearReserveState();
                
                // Return to appropriate menu based on army status
                if (mainParty?.Army != null && mainParty.Army.LeaderParty != mainParty)
                {
                    GameMenu.SwitchToMenu("army_wait");
                    ModLogger.Info("ENCOUNTERGUARD", "AUTO-CLEANUP: Switched to army_wait menu");
                }
                else
                {
                    GameMenu.SwitchToMenu("enlisted_status");
                    ModLogger.Info("ENCOUNTERGUARD", "AUTO-CLEANUP: Switched to enlisted_status menu");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENCOUNTERGUARD", "AUTO-CLEANUP failed", ex);
                // Fallback - try to at least switch menu
                try
                {
                    GameMenu.SwitchToMenu("enlisted_status");
                }
                catch
                {
                    // Ignore fallback failures
                }
            }
        }

        /// <summary>
        ///     Handles the player selecting "Wait in Reserve" from the encounter menu.
        ///     Exits the current encounter and switches to the battle wait menu where
        ///     the player can monitor the battle and rejoin when ready.
        /// </summary>
        /// <param name="args">Menu callback arguments containing menu state and context.</param>
        private void OnWaitInReserveSelected(MenuCallbackArgs args)
        {
            try
            {
                // CRITICAL: Check if this is an ACTIVE siege ASSAULT - do NOT allow wait menu during active assaults
                // Active siege assault battles have their own menu system and custom menus cause loops/crashes
                // But DO allow on the pre-assault join_siege_event menu (before assault starts)
                var enlistmentBehavior = EnlistmentBehavior.Instance;
                var lordParty = enlistmentBehavior?.CurrentLord?.PartyBelongedTo;
                var mapEvent = lordParty?.Party.MapEvent;
                
                // Only block during ACTIVE siege assault (MapEvent exists and is siege type)
                // Allow on join_siege_event menu (no MapEvent yet, just choosing whether to participate)
                var inActiveSiegeAssault = mapEvent != null && 
                                           (mapEvent.IsSiegeAssault || mapEvent.EventType == MapEvent.BattleTypes.Siege);

                if (inActiveSiegeAssault)
                {
                    ModLogger.Info("BATTLE",
                        "Prevented wait in reserve during active siege assault - would cause menu loops");
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=combat_reserve_siege_disabled_full}Wait in reserve is not available during active siege assault battles.").ToString()));
                    return; // Don't switch menus during active siege assaults
                }

                // CRITICAL: Set waiting in reserve flag
                // This prevents XP awards and encounter loops while the player sits out battles
                IsWaitingInReserve = true;

                // CRITICAL: Remove the player from the MapEvent so GetGenericStateMenu() won't return "encounter"
                // This is the same technique used in DestroyClanAction to remove parties from battles
                // The battle continues with the lord fighting, but the player is no longer involved
                if (MobileParty.MainParty?.MapEventSide != null)
                {
                    MobileParty.MainParty.MapEventSide = null;
                    ModLogger.Debug("BATTLE", "Removed player from MapEvent for reserve mode");
                }

                // Tell the game the player is waiting (like native army_wait does)
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Current.IsPlayerWaiting = true;
                }

                // CRITICAL: Make player party inactive to prevent native system from creating new encounters
                // Without this, the game detects the player near the battle and forces them back to encounter menu
                var mainParty = MobileParty.MainParty;
                if (mainParty != null)
                {
                    mainParty.IsActive = false;
                    ModLogger.Debug("BATTLE", "Set player party inactive for reserve mode");
                }

                // Switch to the battle wait menu where the player can monitor the battle
                // NOTE: GenericStateMenuPatch will now return "enlisted_battle_wait" when GetGenericStateMenu()
                // is called, preventing native systems from switching away from our menu.
                GameMenu.ActivateGameMenu("enlisted_battle_wait");
                ModLogger.StateChange("BATTLE", "Fighting", "Reserve",
                    "Player chose to wait in reserve during large battle");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error entering reserve mode", ex);
            }
        }

        /// <summary>
        ///     Initialize battle wait menu.
        /// </summary>
        private void OnBattleWaitInit(MenuCallbackArgs args)
        {
            try
            {
                // 1.3.4+: Set proper menu background - MUST be set before anything else
                // to avoid "temp background" assertion failure
                var backgroundMesh = "encounter_looter"; // Safe fallback
                var lord = EnlistmentBehavior.Instance?.CurrentLord;

                if (lord?.Clan?.Kingdom?.Culture?.EncounterBackgroundMesh != null)
                {
                    backgroundMesh = lord.Clan.Kingdom.Culture.EncounterBackgroundMesh;
                }
                else if (lord?.Culture?.EncounterBackgroundMesh != null)
                {
                    backgroundMesh = lord.Culture.EncounterBackgroundMesh;
                }

                args.MenuContext.SetBackgroundMeshName(backgroundMesh);

                // StartWait() automatically enables time progression for WaitMenuHideProgressAndHoursOption menus
                // Time will continue at normal speed, and player can pause/unpause with spacebar
                args.MenuContext.GameMenu.StartWait();

                ModLogger.Info("BATTLE",
                    "Started wait in reserve - time will continue at normal speed (can pause with spacebar)");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error initializing battle wait menu", ex);
            }
        }

        /// <summary>
        ///     Condition for battle wait menu.
        /// </summary>
        private bool OnBattleWaitCondition(MenuCallbackArgs args)
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = enlistment?.CurrentLord;
            var lordParty = lord?.PartyBelongedTo;

            // Update battle status text
            // CORRECT API: Use Party.MapEvent (not direct on MobileParty)
            if (lordParty?.Party.MapEvent != null)
            {
                var battleStatus = "Your lord is engaged in battle";
                MBTextManager.SetTextVariable("BATTLE_STATUS", battleStatus);
            }
            else
            {
                MBTextManager.SetTextVariable("BATTLE_STATUS", "Battle has ended");
            }

            return enlistment?.IsEnlisted == true;
        }

        /// <summary>
        ///     Tick handler for the battle wait menu that runs while the player is waiting in reserve.
        ///     Monitors battle state, checks if the native system wants to show a different menu,
        ///     and automatically returns the player to battle if troop count drops below 100.
        ///     Includes time delta validation to prevent assertion failures.
        /// </summary>
        /// <param name="args">Menu callback arguments containing menu state and context.</param>
        /// <param name="dt">Time elapsed since last tick, in campaign time. Must be positive.</param>
        private void OnBattleWaitTick(MenuCallbackArgs args, CampaignTime dt)
        {
            try
            {
                // CRITICAL: If player clicked "Rejoin battle", stop all tick processing immediately
                // Without this check, the tick would reset mainParty.IsActive = false after rejoin
                // sets it to true, causing the encounter system to misbehave
                if (!IsWaitingInReserve)
                {
                    return;
                }
                
                // If no time state was captured yet (menu opened via native encounter system),
                // capture current time now so we have a baseline for restoration
                if (!QuartermasterManager.CapturedTimeMode.HasValue && Campaign.Current != null)
                {
                    QuartermasterManager.CapturedTimeMode = Campaign.Current.TimeControlMode;
                }
                
                // NOTE: Time mode restoration is handled ONCE in menu init, not here.
                // Previously this tick handler would restore CapturedTimeMode whenever it saw
                // UnstoppableFastForward, but this fought with user input - when the user clicked
                // fast forward, the next tick would immediately restore it. This caused x3 speed to pause.
                
                // Validate time delta to prevent assertion failures
                // Zero-delta-time updates can cause assertion failures in the rendering system
                if (dt.ToSeconds <= 0)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                var lord = enlistment?.CurrentLord;
                var lordParty = lord?.PartyBelongedTo;
                var mapEvent = lordParty?.Party.MapEvent;
                
                // Check if this is an actual siege ASSAULT (attacking the walls)
                // Do NOT exit reserve for:
                // - Sally out battles (defenders coming out to fight)
                // - General siege preparation (no MapEvent yet)
                // Only exit for actual assaults (IsSiegeAssault = true)
                var siegeAssaultStarted = mapEvent != null && mapEvent.IsSiegeAssault;

                // If the actual assault has begun (IsSiegeAssault), exit the reserve menu so the native encounter can start
                if (siegeAssaultStarted)
                {
                    // CRITICAL: Clear reserve state BEFORE exiting the menu!
                    // Otherwise GenericStateMenuPatch still sees IsWaitingInReserve=true and returns
                    // "enlisted_battle_wait", causing the menu to immediately re-open in an infinite loop
                    IsWaitingInReserve = false;
                    
                    // Restore player party to active state so they can participate in the siege
                    var mainParty = MobileParty.MainParty;
                    if (mainParty != null)
                    {
                        mainParty.IsActive = true;
                        mainParty.IsVisible = true;
                    }
                    
                    args.MenuContext.GameMenu.EndWait();
                    ModLogger.Info("BATTLE", "Siege assault started - cleared reserve state, exiting for native encounter");
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        if (Campaign.Current?.CurrentMenuContext != null)
                        {
                            GameMenu.ExitToLast();
                        }
                    });
                    return;
                }

                // During siege prep (no assault yet), allow waiting in reserve
                // Don't exit the reserve menu just because a SiegeEvent exists
                var hasSiegeEvent = lordParty?.Party.SiegeEvent != null;
                if (hasSiegeEvent && mapEvent == null)
                {
                    // Siege preparation with no active battle - stay in reserve menu
                    ModLogger.Debug("BATTLE", "Siege preparation detected - staying in reserve menu");
                    return;
                }

                // Check what menu the native game system wants to show based on current state
                // NOTE: GenericStateMenuPatch overrides this to return "enlisted_battle_wait" when in reserve,
                // so the result here will reflect our custom menu, not army_wait.
                var genericStateMenu = Campaign.Current?.Models?.EncounterGameMenuModel?.GetGenericStateMenu();

                // Check if lord is still in battle - if so, stay in reserve
                // NOTE: Menu stability is now handled by GenericStateMenuPatch which overrides
                // GetGenericStateMenu() to return "enlisted_battle_wait" when in reserve.
                // We no longer need to defensively switch menus here.
                var lordStillInBattle = lordParty?.Party.MapEvent != null;

                if (lordStillInBattle)
                {
                    // Lord is still in battle - stay in reserve
                    // Keep player party inactive and out of MapEvent while waiting
                    var mainParty = MobileParty.MainParty;
                    if (mainParty != null)
                    {
                        if (mainParty.IsActive)
                        {
                            mainParty.IsActive = false;
                        }
                        if (mainParty.MapEventSide != null)
                        {
                            mainParty.MapEventSide = null;
                        }
                    }
                    
                    return;
                }

                // Lord's current MapEvent is null, but check if we're in an army that's still fighting
                // Army battles can have multiple waves - don't exit reserve until the entire army sequence is done
                var armyStillInBattle = lordParty?.Army != null && 
                                        lordParty.Army.Parties.Any(p => p?.Party?.MapEvent != null);

                if (armyStillInBattle)
                {
                    // Army is still fighting (different party in battle) - stay in reserve
                    ModLogger.Debug("BATTLE", "Staying in reserve - army still fighting (other party in battle)");
                    return;
                }

                // CRITICAL: Don't switch to army_wait - it triggers new battle events and XP spam
                // Instead, check if the native system wants army_wait (meaning army battles ongoing)
                // and if so, STAY in our menu until battles are truly done
                if (!string.IsNullOrEmpty(genericStateMenu) && genericStateMenu == "army_wait")
                {
                    // Native system wants army_wait - this means the army battle series is ongoing
                    // Stay in our reserve menu to avoid triggering encounter loops
                    ModLogger.Debug("BATTLE", "Native wants army_wait - staying in reserve to avoid encounter loop");
                    return;
                }

                // Battle series has truly ended (not army_wait) - now we can exit reserve
                // NOTE: Since GenericStateMenuPatch returns "enlisted_battle_wait" while in reserve,
                // this will only trigger when battle truly ends and patch stops overriding
                if (!string.IsNullOrEmpty(genericStateMenu) &&
                    genericStateMenu != "enlisted_battle_wait")
                {
                    // Clear the waiting in reserve flag - battle series has ended
                    IsWaitingInReserve = false;
                    args.MenuContext.GameMenu.EndWait();
                    ModLogger.Info("BATTLE", $"Battle series ended - switching to native menu '{genericStateMenu}'");
                    GameMenu.SwitchToMenu(genericStateMenu);
                    return;
                }

                // Check if the battle has ended
                // Battle ends when: MapEvent is null, OR MapEvent.HasWinner is true
                // Note: lord, lordParty, and mapEvent are already declared above
                var battleEnded = mapEvent == null || mapEvent.HasWinner;
                
                // Also check if lord's party is gone (disbanded/captured)
                var lordPartyGone = lordParty == null || !lordParty.IsActive;
                
                if ((battleEnded || lordPartyGone) && string.IsNullOrEmpty(genericStateMenu))
                {
                    // Clear the waiting in reserve flag - battle has ended
                    IsWaitingInReserve = false;
                    args.MenuContext.GameMenu.EndWait();
                    
                    // CRITICAL: Fully clean up the encounter state so the player doesn't get stuck invisible
                    // When the battle ends while in reserve, we must call Finish() to immediately clear
                    // PlayerEncounter.Current. Setting LeaveEncounter=true alone is not enough because
                    // StopEnlist() checks PlayerEncounter.Current and will deactivate the party if it's still set.
                    if (PlayerEncounter.Current != null)
                    {
                        PlayerEncounter.Current.IsPlayerWaiting = false;
                        PlayerEncounter.LeaveEncounter = true;
                        try
                        {
                            // Must explicitly call LeaveSettlement() before Finish() when enlisted.
                            // The forcePlayerOutFromSettlement parameter in Finish() only works when
                            // MainParty.AttachedTo == null, but enlisted players are attached to their lord.
                            if (PlayerEncounter.InsideSettlement)
                            {
                                PlayerEncounter.LeaveSettlement();
                            }
                            
                            PlayerEncounter.Finish(); // Immediately clear PlayerEncounter.Current
                            ModLogger.Info("BATTLE", "Finished PlayerEncounter after battle end");
                        }
                        catch (Exception finishEx)
                        {
                            ModLogger.Caught("BATTLE", "Error finishing encounter", finishEx);
                        }
                    }
                    
                    // Restore player party to map - needed whether enlisted or not since we just exited reserve
                    var mainParty = MobileParty.MainParty;
                    if (mainParty != null && Hero.MainHero?.IsPrisoner != true)
                    {
                        mainParty.IsActive = true;
                        mainParty.IsVisible = true;
                        mainParty.SetMoveModeHold(); // Stop any phantom movement from battle
                        ModLogger.Info("BATTLE", "Restored player party to map after battle/reserve end");
                    }
                    
                    // Return to normal enlisted state or campaign map
                    // Note: 'enlistment' variable is already defined at the start of this method
                    // If lord's party is gone, go to campaign map (hourly tick will handle grace period)
                    if (enlistment?.IsEnlisted == true && !lordPartyGone)
                    {
                        // Use SafeActivateEnlistedMenu to respect siege detection
                        EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                        ModLogger.Info("BATTLE", "Battle ended - returning to enlisted status menu (via SafeActivate)");
                    }
                    else
                    {
                        GameMenu.ExitToLast();
                        ModLogger.Info("BATTLE", "Battle ended or lord party gone - exiting to campaign map");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error in battle wait tick", ex);
            }
        }

        /// <summary>
        ///     Handle rejoin battle selection.
        /// </summary>
        private void OnRejoinBattleSelected(MenuCallbackArgs args)
        {
            try
            {
                // Clear the waiting in reserve flag - player is rejoining battle
                IsWaitingInReserve = false;

                // Clear the player waiting flag so the encounter can proceed
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Current.IsPlayerWaiting = false;
                }

                // Restore player party to active state so they can participate in encounters
                var mainParty = MobileParty.MainParty;
                if (mainParty != null)
                {
                    mainParty.IsActive = true;
                }

                args.MenuContext.GameMenu.EndWait();

                NextFrameDispatcher.RunNextFrame(() =>
                {
                    try
                    {
                        if (Campaign.Current == null)
                        {
                            ModLogger.Warn("BATTLE", "Rejoin aborted - campaign not available");
                            return;
                        }
                        // Re-evaluate battle state next frame to avoid using stale data
                        var enlistment = EnlistmentBehavior.Instance;
                        var lordParty = enlistment?.CurrentLord?.PartyBelongedTo;
                        var lordMapEvent = lordParty?.Party.MapEvent;
                        var lordSide = lordParty?.Party.MapEventSide;

                        // CRITICAL: If lord is still in battle, re-add player to the MapEvent
                        // before activating encounter menu (we removed them when entering reserve)
                        if (lordMapEvent != null && lordSide != null)
                        {
                            var playerParty = MobileParty.MainParty?.Party;
                            if (playerParty != null)
                            {
                                playerParty.MapEventSide = lordSide;
                            }
                            
                            GameMenu.ActivateGameMenu("encounter");
                            ModLogger.Info("BATTLE", "Player rejoining battle from reserve");
                            return;
                        }
                        
                        // Lord not in battle - check what menu native system wants
                        var desiredMenu = Campaign.Current?.Models?.EncounterGameMenuModel?.GetGenericStateMenu();

                        if (!string.IsNullOrEmpty(desiredMenu) && desiredMenu != "enlisted_battle_wait")
                        {
                            GameMenu.ActivateGameMenu(desiredMenu);
                            ModLogger.Info("BATTLE", $"Exited reserve - battle ended, showing '{desiredMenu}'");
                        }
                        else if (PlayerEncounter.Current != null)
                        {
                            GameMenu.ActivateGameMenu("encounter");
                            ModLogger.Info("BATTLE", "Exited reserve to encounter menu");
                        }
                        else
                        {
                            GameMenu.ExitToLast();
                            ModLogger.Info("BATTLE", "Exited reserve - no active battle");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("BATTLE", "Error in rejoin menu switch", ex);
                        try
                        {
                            GameMenu.ExitToLast();
                        }
                        catch (Exception fallbackEx)
                        {
                            ModLogger.Caught("BATTLE", "Error during fallback exit", fallbackEx);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BATTLE", "Error rejoining battle", ex);
            }
        }


    }
}
