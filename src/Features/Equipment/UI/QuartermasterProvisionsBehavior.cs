using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using Enlisted.Features.Conversations.Behaviors;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Gauntlet UI controller for the provisions purchase screen.
    /// 
    /// Manages the provisions grid overlay lifecycle, input handling, and
    /// campaign event integration. Mirrors QuartermasterEquipmentSelectorBehavior patterns.
    /// </summary>
    public class QuartermasterProvisionsBehavior : CampaignBehaviorBase
    {
        public static QuartermasterProvisionsBehavior Instance { get; private set; }
        
        // Gauntlet UI components
        private static GauntletLayer _gauntletLayer;
        private static GauntletMovieIdentifier _gauntletMovie;
        private static QuartermasterProvisionsVm _provisionsViewModel;
        
        // Captured time state for restoring after provisions screen closes
        private static CampaignTimeControlMode? _capturedTimeMode;
        
        // Track whether to return to QM conversation after closing
        private static bool _returnToConversationOnClose = true;
        
        /// <summary>
        /// Returns true if the provisions screen is currently open.
        /// </summary>
        public static bool IsOpen => _gauntletLayer != null;
        
        public QuartermasterProvisionsBehavior()
        {
            Instance = this;
        }
        
        public override void RegisterEvents()
        {
            // Register for campaign events that should force-close the provisions screen
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnBattleEnd);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }
        
        public override void SyncData(IDataStore dataStore)
        {
            // No persistent data for UI behavior
            // Force-close on save/load to prevent stale UI
            if (dataStore.IsLoading && IsOpen)
            {
                ForceCloseOnInterruption("save/load");
            }
        }
        
        private void OnBattleEnd(MapEvent mapEvent)
        {
            if (IsOpen)
            {
                ForceCloseOnInterruption("battle end");
            }
        }
        
        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            if (party == MobileParty.MainParty && IsOpen)
            {
                ForceCloseOnInterruption("settlement left");
            }
        }
        
        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner == Hero.MainHero && IsOpen)
            {
                ForceCloseOnInterruption("player captured");
            }
        }
        
        private void OnMapEventEnded(MapEvent mapEvent)
        {
            // Log the event type to diagnose conversation vs combat events
            var eventType = mapEvent?.EventType.ToString() ?? "null";
            var isPlayerInMapEvent = MobileParty.MainParty?.MapEvent != null;
            var isPlayerInConversation = Campaign.Current?.ConversationManager?.IsConversationInProgress ?? false;
            
            ModLogger.Debug("QuartermasterUI", 
                $"MapEventEnded (Provisions): EventType={eventType}, IsOpen={IsOpen}, " +
                $"PlayerInMapEvent={isPlayerInMapEvent}, PlayerInConversation={isPlayerInConversation}");
            
            // Don't close UI when mapEvent is null (conversations don't have MapEvents)
            if (mapEvent == null)
            {
                ModLogger.Debug("QuartermasterUI", "MapEventEnded with null mapEvent - not closing provisions (likely conversation end)");
                return;
            }
            
            // Don't close UI if player is currently in a conversation (quartermaster dialogue)
            // This prevents old/unrelated MapEvents from closing the provisions screen
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
        }
        
        /// <summary>
        /// Force-close the provisions screen due to external interruption.
        /// Does not return to conversation.
        /// </summary>
        private static void ForceCloseOnInterruption(string reason)
        {
            ModLogger.Info("QuartermasterUI", $"Force-closing provisions screen due to: {reason}");
            CloseProvisionsScreen(false);
        }
        
        /// <summary>
        /// Show the provisions purchase screen.
        /// Uses Gauntlet layer overlay on top of existing screens.
        /// </summary>
        public static void ShowProvisionsScreen()
        {
            try
            {
                // Prevent double-open
                if (IsOpen)
                {
                    ModLogger.Debug("QuartermasterUI", "Closing existing provisions screen before opening new one");
                    CloseProvisionsScreen(false);
                }
                
                // Capture current time mode and pause time while purchasing provisions
                if (Campaign.Current != null)
                {
                    _capturedTimeMode = Campaign.Current.TimeControlMode;
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                    Campaign.Current.SetTimeControlModeLock(true);
                    ModLogger.Debug("QuartermasterUI", $"Time paused for provisions screen (captured: {_capturedTimeMode})");
                }
                
                // Create ViewModel
                _provisionsViewModel = new QuartermasterProvisionsVm();
                _provisionsViewModel.RefreshValues();
                
                // Create Gauntlet layer for provisions overlay
                _gauntletLayer = new GauntletLayer("QuartermasterProvisionsGrid", 4000);
                
                // Load provisions grid movie from GUI/Prefabs/Equipment/
                _gauntletMovie = _gauntletLayer.LoadMovie("QuartermasterProvisionsGrid", _provisionsViewModel);
                
                // Set up input handling
                _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _gauntletLayer.InputRestrictions.SetInputRestrictions();

                var topScreen = ScreenManager.TopScreen;
                if (topScreen == null)
                {
                    ModLogger.ErrorCode("QuartermasterUI", "E-QM-UI-005", "ScreenManager.TopScreen is null — cannot add provisions layer");
                    CloseProvisionsScreen(false);
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_ui_screen_unavailable}Unable to open the quartermaster screen right now. Try again in a moment.").ToString()));
                    return;
                }

                topScreen.AddLayer(_gauntletLayer);
                _gauntletLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_gauntletLayer);
                
                ModLogger.Info("QuartermasterUI", "Provisions screen opened successfully");
            }
            catch (Exception ex)
            {
                ModLogger.ErrorCode("QuartermasterUI", "E-QM-UI-004", "Failed to open provisions screen", ex);
                CloseProvisionsScreen(false);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=qm_ui_provisions_failed}Provisions screen failed to open. Check the log for details.").ToString()));
            }
        }
        
        /// <summary>
        /// Close the provisions screen and clean up resources.
        /// By default, returns to the quartermaster conversation hub.
        /// </summary>
        /// <param name="returnToConversation">If true, restarts QM conversation after closing.</param>
        public static void CloseProvisionsScreen(bool returnToConversation = true)
        {
            try
            {
                if (_gauntletLayer != null)
                {
                    // Reset input and focus
                    _gauntletLayer.InputRestrictions.ResetInputRestrictions();
                    _gauntletLayer.IsFocusLayer = false;
                    
                    // Release movie
                    if (_gauntletMovie != null)
                    {
                        _gauntletLayer.ReleaseMovie(_gauntletMovie);
                        _gauntletMovie = null;
                    }
                    
                    // Remove layer from screen
                    ScreenManager.TopScreen?.RemoveLayer(_gauntletLayer);
                    _gauntletLayer = null;
                    
                    ModLogger.Debug("QuartermasterUI", "Provisions screen layer removed");
                }
                
                // Clean up ViewModel
                if (_provisionsViewModel != null)
                {
                    _provisionsViewModel.OnFinalize();
                    _provisionsViewModel = null;
                }
                
                // Return to QM conversation if requested
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
                
                ModLogger.Info("QuartermasterUI", "Provisions screen closed");
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error closing provisions screen", ex);
            }
            finally
            {
                // Ensure cleanup even on error
                _gauntletLayer = null;
                _gauntletMovie = null;
                _provisionsViewModel = null;
                
                // Restore time control mode
                if (Campaign.Current != null)
                {
                    Campaign.Current.SetTimeControlModeLock(false);
                    
                    if (_capturedTimeMode.HasValue)
                    {
                        var normalized = QuartermasterManager.NormalizeToStoppable(_capturedTimeMode.Value);
                        Campaign.Current.TimeControlMode = normalized;
                        ModLogger.Debug("QuartermasterUI", $"Time restored from provisions screen: {normalized}");
                    }
                    
                    _capturedTimeMode = null;
                }
            }
        }
        
        /// <summary>
        /// Close without returning to conversation.
        /// Used when player explicitly exits or external interruption occurs.
        /// </summary>
        public static void CloseWithoutConversation()
        {
            _returnToConversationOnClose = false;
            CloseProvisionsScreen(false);
            _returnToConversationOnClose = true;
        }
        
        /// <summary>
        /// Refresh the provisions screen if open.
        /// Called after muster to update stock levels.
        /// </summary>
        public static void RefreshIfOpen()
        {
            if (IsOpen && _provisionsViewModel != null)
            {
                _provisionsViewModel.RefreshValues();
            }
        }
    }
}

