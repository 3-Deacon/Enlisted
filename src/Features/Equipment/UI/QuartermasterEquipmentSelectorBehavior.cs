using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using Enlisted.Features.Conversations.Behaviors;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.TimeControl;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Gauntlet equipment selector behavior providing individual clickable equipment selection.
    /// 
    /// Creates a custom UI overlay showing equipment variants as clickable buttons.
    /// Uses TaleWorlds Gauntlet UI system for custom overlay creation and management.
    /// After closing, the player returns to the quartermaster conversation hub for more shopping.
    /// Registers for campaign events to force-close on battle/capture interruptions.
    /// </summary>
    public class QuartermasterEquipmentSelectorBehavior : CampaignBehaviorBase
    {
        public static QuartermasterEquipmentSelectorBehavior Instance { get; private set; }
        
        // Gauntlet UI components for custom overlay display
        private static GauntletLayer _gauntletLayer;
        // 1.3.4 API: LoadMovie now returns GauntletMovieIdentifier instead of IGauntletMovie
        private static GauntletMovieIdentifier _gauntletMovie;
        private static QuartermasterEquipmentSelectorVm _selectorViewModel;
        
        // Track whether to return to conversation after closing
        private static bool _returnToConversationOnClose = true;
        
        // Time scope spanning the equipment selector's open lifetime.
        // Held as a field because open (ShowEquipmentSelector) and close
        // (CloseEquipmentSelector) are separate entry points — the scope
        // captures on open and disposes in CloseEquipmentSelector's finally.
        private static EnlistedTimeScope? _equipmentTimeScope;
        
        /// <summary>
        /// Returns true if the equipment selector UI is currently open.
        /// </summary>
        public static bool IsOpen => _gauntletLayer != null;
        
        public QuartermasterEquipmentSelectorBehavior()
        {
            Instance = this;
        }
        
        public override void RegisterEvents()
        {
            // Force-close Gauntlet on events that interrupt the QM interaction
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnBattleEnd);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }
        
        public override void SyncData(IDataStore dataStore)
        {
            // No persistent data for UI behavior
            // Force-close on save/load to prevent stale UI
            if (dataStore.IsLoading)
            {
                if (IsOpen)
                {
                    ForceCloseOnInterruption("save/load");
                }
                if (IsUpgradeScreenOpen)
                {
                    CloseUpgradeScreen(false);
                    ModLogger.Info("QuartermasterUI", "Force-closed upgrade screen due to save load");
                }
            }
        }
        
        private void OnBattleEnd(MapEvent mapEvent)
        {
            if (IsOpen)
            {
                ForceCloseOnInterruption("battle end");
            }
            if (IsUpgradeScreenOpen)
            {
                CloseUpgradeScreen(false);
                ModLogger.Info("QuartermasterUI", "Force-closed upgrade screen due to battle end");
            }
        }
        
        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            if (party == MobileParty.MainParty)
            {
                if (IsOpen)
                {
                    ForceCloseOnInterruption("settlement left");
                }
                if (IsUpgradeScreenOpen)
                {
                    CloseUpgradeScreen(false);
                    ModLogger.Info("QuartermasterUI", "Force-closed upgrade screen due to settlement departure");
                }
            }
        }
        
        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner == Hero.MainHero)
            {
                if (IsOpen)
                {
                    ForceCloseOnInterruption("player captured");
                }
                if (IsUpgradeScreenOpen)
                {
                    CloseUpgradeScreen(false);
                    ModLogger.Info("QuartermasterUI", "Force-closed upgrade screen due to player capture");
                }
            }
        }
        
        private void OnMapEventEnded(MapEvent mapEvent)
        {
            // Log the event type to diagnose conversation vs combat events
            var eventType = mapEvent?.EventType.ToString() ?? "null";
            var isPlayerInMapEvent = MobileParty.MainParty?.MapEvent != null;
            var isPlayerInConversation = Campaign.Current?.ConversationManager?.IsConversationInProgress ?? false;
            
            ModLogger.Debug("QuartermasterUI", 
                $"MapEventEnded: EventType={eventType}, IsOpen={IsOpen}, IsUpgradeScreenOpen={IsUpgradeScreenOpen}, " +
                $"PlayerInMapEvent={isPlayerInMapEvent}, PlayerInConversation={isPlayerInConversation}");
            
            // Don't close UI when mapEvent is null (conversations don't have MapEvents)
            if (mapEvent == null)
            {
                ModLogger.Debug("QuartermasterUI", "MapEventEnded with null mapEvent - not closing UI (likely conversation end)");
                return;
            }
            
            // Don't close UI if player is currently in a conversation (quartermaster dialogue)
            // This prevents old/unrelated MapEvents from closing the equipment selector
            if (isPlayerInConversation)
            {
                ModLogger.Debug("QuartermasterUI", "MapEventEnded during conversation - ignoring (QM dialogue in progress)");
                return;
            }
            
            // Don't close UI if the ended MapEvent is not the player's current MapEvent
            // This prevents unrelated map events from closing our UI
            if (MobileParty.MainParty?.MapEvent != mapEvent)
            {
                ModLogger.Debug("QuartermasterUI", "MapEventEnded for non-player MapEvent - ignoring");
                return;
            }
            
            bool isCombatEvent = mapEvent.EventType != MapEvent.BattleTypes.None;
            ModLogger.Debug("QuartermasterUI", $"isCombatEvent={isCombatEvent}");
            
            if (isCombatEvent && IsOpen)
            {
                ForceCloseOnInterruption("map event ended");
            }
            
            if (isCombatEvent && IsUpgradeScreenOpen)
            {
                CloseUpgradeScreen(false);
                ModLogger.Info("QuartermasterUI", "Force-closed upgrade screen due to map event end");
            }
        }
        
        /// <summary>
        /// Force-close the selector due to external interruption. Does not return to conversation.
        /// </summary>
        private static void ForceCloseOnInterruption(string reason)
        {
            ModLogger.Info("QuartermasterUI", $"Force-closing equipment selector due to: {reason}");
            ModLogger.Debug("QuartermasterUI", $"Stack trace: {Environment.StackTrace}");
            CloseEquipmentSelector(false);
        }
        
        /// <summary>
        /// Show equipment selector with proper grid UI using OFFICIAL module structure.
        /// Template now located in GUI/Prefabs/Equipment/ following Bannerlord standards.
        /// </summary>
        public static void ShowEquipmentSelector(List<EquipmentVariantOption> availableVariants, EquipmentIndex targetSlot, string equipmentType)
        {
            try
            {
                ModLogger.Info("QuartermasterUI", $"ShowEquipmentSelector called for {equipmentType}");
                ModLogger.Debug("QuartermasterUI", $"ScreenManager.TopScreen = {ScreenManager.TopScreen?.GetType().Name ?? "null"}");
                ModLogger.Debug("QuartermasterUI", $"IsOpen = {IsOpen}");
                
                // Prevent double-open - close existing first without returning to conversation
                if (IsOpen)
                {
                    ModLogger.Debug("QuartermasterUI", "Closing existing selector before opening new one");
                    CloseEquipmentSelector(false);
                }
                
                if (availableVariants == null || availableVariants.Count == 0)
                {
                    ModLogger.Warn("QuartermasterUI", $"No equipment variants available for {equipmentType} — grid not opened");
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_no_variants_for_type}The quartermaster has no {EQUIPMENT_TYPE} available for your rank and formation.")
                            .SetTextVariable("EQUIPMENT_TYPE", equipmentType ?? "equipment")
                            .ToString()));
                    return;
                }
                
                ModLogger.Debug("QuartermasterUI", $"Available variants: {availableVariants.Count}");
                
                // Capture via EnlistedTimeScope; disposed in CloseEquipmentSelector's finally.
                _equipmentTimeScope = EnlistedTimeScope.Capture();
                ModLogger.Debug("QuartermasterUI", "Time paused via EnlistedTimeScope for equipment selector");
                
                ModLogger.Debug("QuartermasterUI", "Creating Gauntlet layer...");
                
                // Create Gauntlet layer for custom UI overlay
                // 1.3.4 API: GauntletLayer constructor with name and localOrder (omit shouldClear as it defaults to false)
                _gauntletLayer = new GauntletLayer("QuartermasterEquipmentGrid", 1001);
                
                ModLogger.Debug("QuartermasterUI", "Creating ViewModel...");
                
                // Create ViewModel with equipment variant collection
                _selectorViewModel = new QuartermasterEquipmentSelectorVm(availableVariants, targetSlot, equipmentType);
                _selectorViewModel.RefreshValues();
                
                ModLogger.Debug("QuartermasterUI", "Loading movie...");
                
                // FIXED: Load template from official module structure GUI/Prefabs/Equipment/
                _gauntletMovie = _gauntletLayer.LoadMovie("QuartermasterEquipmentGrid", _selectorViewModel);
                
                ModLogger.Debug("QuartermasterUI", "Setting up input...");
                
                // Register hotkeys and set input restrictions for UI interaction
                _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                // Omit default parameter values for cleaner code
                _gauntletLayer.InputRestrictions.SetInputRestrictions();
                
                var topScreen = ScreenManager.TopScreen;
                if (topScreen == null)
                {
                    ModLogger.ErrorCode("QuartermasterUI", "E-QM-UI-001", "ScreenManager.TopScreen is null - cannot add layer");
                    CloseEquipmentSelector();
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_ui_screen_unavailable}Unable to open the quartermaster screen right now. Try again in a moment.").ToString()));
                    return;
                }
                
                ModLogger.Debug("QuartermasterUI", $"Adding layer to screen: {topScreen.GetType().Name}");
                
                topScreen.AddLayer(_gauntletLayer);
                _gauntletLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_gauntletLayer);
                
                ModLogger.Info("QuartermasterUI", $"Grid UI opened successfully with {availableVariants.Count} variants for {equipmentType}");
                ModLogger.Debug("QuartermasterUI", $"IsOpen after opening = {IsOpen}");
            }
            catch (Exception ex)
            {
                ModLogger.ErrorCode("QuartermasterUI", "E-QM-UI-002", "Equipment selector failed to open", ex);
                CloseEquipmentSelector();
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=qm_ui_open_failed}Quartermaster screen failed to open. Check the log for details.").ToString()));
            }
        }


        /// <summary>
        /// Close equipment selector and clean up UI resources.
        /// By default, returns to the quartermaster conversation hub for continued shopping.
        /// </summary>
        /// <param name="returnToConversation">If true, restarts the QM conversation after closing.</param>
        public static void CloseEquipmentSelector(bool returnToConversation = true)
        {
            try
            {
                ModLogger.Info("QuartermasterUI", $"CloseEquipmentSelector called (returnToConversation={returnToConversation})");
                ModLogger.Debug("QuartermasterUI", $"_gauntletLayer = {(_gauntletLayer != null ? "not null" : "null")}");
                
                if (_gauntletLayer != null)
                {
                    ModLogger.Debug("QuartermasterUI", "Resetting input restrictions...");
                    // Reset input restrictions and remove focus
                    _gauntletLayer.InputRestrictions.ResetInputRestrictions();
                    _gauntletLayer.IsFocusLayer = false;
                    
                    if (_gauntletMovie != null)
                    {
                        ModLogger.Debug("QuartermasterUI", "Releasing movie...");
                        _gauntletLayer.ReleaseMovie(_gauntletMovie);
                    }
                    
                    var topScreen = ScreenManager.TopScreen;
                    ModLogger.Debug("QuartermasterUI", $"TopScreen = {topScreen?.GetType().Name ?? "null"}");
                    if (topScreen != null)
                    {
                        ModLogger.Debug("QuartermasterUI", "Removing layer from screen...");
                        topScreen.RemoveLayer(_gauntletLayer);
                    }
                    
                    ModLogger.Info("QuartermasterUI", "Equipment selector closed");
                }
                else
                {
                    ModLogger.Debug("QuartermasterUI", "CloseEquipmentSelector called but layer was already null");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error closing equipment selector", ex);
            }
            finally
            {
                // Always cleanup references
                _gauntletLayer = null;
                _gauntletMovie = null;
                _selectorViewModel = null;
                
                // Dispose the scope to release lock and restore time mode.
                _equipmentTimeScope?.Dispose();
                _equipmentTimeScope = null;
                ModLogger.Debug("QuartermasterUI", "Equipment selector time scope disposed");
            }
            
            // Return to quartermaster conversation after closing (only if still enlisted with valid QM)
            if (returnToConversation && _returnToConversationOnClose)
            {
                var enlistment = EnlistmentBehavior.Instance;
                var qmHero = enlistment?.QuartermasterHero;
                
                if (enlistment?.IsEnlisted != true)
                {
                    ModLogger.Debug("QuartermasterUI", "Not returning to conversation - player no longer enlisted");
                }
                else if (qmHero == null || !qmHero.IsAlive)
                {
                    ModLogger.Debug("QuartermasterUI", "Not returning to conversation - QM hero unavailable");
                }
                else
                {
                    EnlistedDialogManager.RestartQuartermasterConversation();
                }
            }
        }
        
        /// <summary>
        /// Close the selector without returning to conversation.
        /// Used when the player explicitly exits via the "Done" button or external interruption.
        /// </summary>
        public static void CloseWithoutConversation()
        {
            _returnToConversationOnClose = false;
            CloseEquipmentSelector(false);
            _returnToConversationOnClose = true;
        }
        
        #region Upgrade Screen Support (Phase 3)
        
        // Upgrade screen UI components
        private static GauntletLayer _upgradeLayer;
        private static GauntletMovieIdentifier _upgradeMovie;
        private static QuartermasterUpgradeVm _upgradeViewModel;
        
        // Time scope spanning the upgrade screen's open lifetime.
        private static EnlistedTimeScope? _upgradeTimeScope;
        
        /// <summary>
        /// Check if upgrade screen is currently open.
        /// </summary>
        public static bool IsUpgradeScreenOpen => _upgradeLayer != null;
        
        /// <summary>
        /// Show upgrade screen with player's equipped items.
        /// </summary>
        public static void ShowUpgradeScreen()
        {
            try
            {
                ModLogger.Info("QuartermasterUI", "ShowUpgradeScreen called");
                
                // Prevent double-open
                if (IsUpgradeScreenOpen)
                {
                    ModLogger.Debug("QuartermasterUI", "Closing existing upgrade screen before opening new one");
                    CloseUpgradeScreen(false);
                }
                
                _upgradeTimeScope = EnlistedTimeScope.Capture();
                ModLogger.Debug("QuartermasterUI", "Time paused via EnlistedTimeScope for upgrade screen");
                
                ModLogger.Debug("QuartermasterUI", "Creating upgrade ViewModel");
                
                // Create ViewModel
                _upgradeViewModel = new QuartermasterUpgradeVm();
                _upgradeViewModel.RefreshValues();
                
                ModLogger.Debug("QuartermasterUI", $"ViewModel created, UpgradeRows={_upgradeViewModel.UpgradeRows.Count}");
                
                // Create Gauntlet layer for upgrade screen overlay
                _upgradeLayer = new GauntletLayer("QuartermasterUpgradeScreen", 4000);
                ModLogger.Debug("QuartermasterUI", "Gauntlet layer created");
                
                // Load upgrade screen movie from GUI/Prefabs/Equipment/
                ModLogger.Debug("QuartermasterUI", "Loading movie: QuartermasterUpgradeScreen");
                _upgradeMovie = _upgradeLayer.LoadMovie("QuartermasterUpgradeScreen", _upgradeViewModel);
                ModLogger.Debug("QuartermasterUI", "Movie loaded successfully");
                
                // Apply input restrictions and add layer to screen
                _upgradeLayer.InputRestrictions.SetInputRestrictions();

                var topScreen = ScreenManager.TopScreen;
                if (topScreen == null)
                {
                    ModLogger.ErrorCode("QuartermasterUI", "E-QM-UI-006", "ScreenManager.TopScreen is null — cannot add upgrade layer");
                    CloseUpgradeScreen();
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_ui_screen_unavailable}Unable to open the quartermaster screen right now. Try again in a moment.").ToString()));
                    return;
                }

                topScreen.AddLayer(_upgradeLayer);
                _upgradeLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_upgradeLayer);
                
                ModLogger.Debug("QuartermasterUI", "Layer added to screen and focused");
                
                // Handle ESC key to close upgrade screen
                _upgradeLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
                
                ModLogger.Info("QuartermasterUI", "Upgrade screen opened successfully");
            }
            catch (Exception ex)
            {
                ModLogger.ErrorCode("QuartermasterUI", "E-QM-UI-003", "Failed to open upgrade screen", ex);
                CloseUpgradeScreen();
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=qm_ui_upgrade_failed}Upgrade screen failed to open. Check the log for details.").ToString()));
            }
        }
        
        /// <summary>
        /// Close upgrade screen and clean up UI resources.
        /// By default, returns to the quartermaster conversation hub.
        /// </summary>
        /// <param name="returnToConversation">If true, restarts the QM conversation after closing.</param>
        public static void CloseUpgradeScreen(bool returnToConversation = true)
        {
            try
            {
                if (_upgradeLayer != null)
                {
                    // Reset input restrictions and remove focus
                    _upgradeLayer.InputRestrictions.ResetInputRestrictions();
                    _upgradeLayer.IsFocusLayer = false;
                    
                    if (_upgradeMovie != null)
                    {
                        _upgradeLayer.ReleaseMovie(_upgradeMovie);
                        _upgradeMovie = null;
                    }
                    
                    // Remove layer from screen
                    ScreenManager.TopScreen?.RemoveLayer(_upgradeLayer);
                    _upgradeLayer = null;
                    
                    ModLogger.Debug("QuartermasterUI", "Upgrade screen closed successfully");
                }
                
                // Clean up ViewModel (OnFinalize should cascade to child ViewModels in MBBindingList)
                if (_upgradeViewModel != null)
                {
                    _upgradeViewModel.OnFinalize();
                    _upgradeViewModel = null;
                }
                
                // Note: Child ViewModels (QuartermasterUpgradeItemVm and UpgradeOptionVm) in MBBindingList
                // should be automatically finalized by parent OnFinalize() call. Monitor for memory leaks
                // if upgrade screen is used frequently.
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error closing upgrade screen", ex);
            }
            finally
            {
                _upgradeTimeScope?.Dispose();
                _upgradeTimeScope = null;
                ModLogger.Debug("QuartermasterUI", "Upgrade screen time scope disposed");
            }
            
            // Return to quartermaster conversation if requested (outside try-finally to avoid interrupting time restore)
            if (returnToConversation && _returnToConversationOnClose)
            {
                EnlistedDialogManager.RestartQuartermasterConversation();
            }
        }
        
        #endregion
    }
}
