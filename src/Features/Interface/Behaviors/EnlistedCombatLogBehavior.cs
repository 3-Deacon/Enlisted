using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.ViewModels;
using Enlisted.Features.Interface.Utils;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Interface.Behaviors
{
    /// <summary>
    /// Manages the enlisted combat log UI layer lifecycle and message routing.
    /// Creates the Gauntlet layer when campaign starts and handles visibility
    /// based on enlistment state. Provides singleton access for Harmony patch.
    /// </summary>
    public class EnlistedCombatLogBehavior : CampaignBehaviorBase
    {
        public static EnlistedCombatLogBehavior Instance { get; private set; }
        
        private GauntletLayer _layer;
        private EnlistedCombatLogVM _dataSource;
        private GauntletMovieIdentifier _movie;
        private ScrollablePanel _scrollablePanel;
        private ListPanel _messagesListPanel;
        private ScreenBase _ownerScreen; // The screen we added our layer to
        private bool _isInitialized;
        private bool _isLayerActive;
        private bool _wasInConversation;
        private bool _wasArmyManagementOpen;
        private bool _wasRepositionedUp;
        private float _lastScrollPosition;
        private float _timeSinceLastManualScroll;
        private bool _shouldAutoScroll = true;
        private const float AutoScrollResumeDelay = 6f;
        
        public EnlistedCombatLogBehavior()
        {
            Instance = this;
        }
        
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, OnGameOver);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }
        
        public override void SyncData(IDataStore dataStore)
        {
            // No save data needed - messages are transient
        }
        
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Delay initialization until we're sure MapScreen exists
            _isInitialized = false;
        }
        
        private void OnTick(float dt)
        {
            // Log once per session to confirm OnTick is being called
            ModLogger.LogOnce("combat_log_ontick_called", "Interface", "EnlistedCombatLogBehavior.OnTick is being called", LogLevel.Info);
            
            // Initialize UI on first tick when MapScreen is available
            if (!_isInitialized && Campaign.Current != null)
            {
                ModLogger.Info("Interface", "Combat log initializing...");
                InitializeUI();
                _isInitialized = true;
                ModLogger.Info("Interface", $"Combat log initialized: _dataSource={(_dataSource != null ? "OK" : "NULL")}, _layer={(_layer != null ? "OK" : "NULL")}");
            }
            
            // Manage layer visibility based on conversation state
            ManageLayerVisibility();
            
            // Update ViewModel tick
            if (_dataSource != null && _isLayerActive)
            {
                _dataSource.Tick(dt);
                _dataSource.UpdateVisibility();
            }
            
            // Detect if party screen, army management, or inventory is open and adjust positioning
            // Only reposition for Gauntlet UI screens that have panels at the bottom,
            // not for text-based GameMenus (like enlisted_status) which don't obstruct the log
            if (_dataSource != null)
            {
                // Check if party screen is open by checking active game state
                bool isPartyScreenOpen = Game.Current?.GameStateManager?.ActiveState is TaleWorlds.CampaignSystem.GameState.PartyState;
                
                // Check if party bar is extended
                bool isBarExtended = IsBarExtended();
                
                // Check if army overlay roster panel is expanded
                bool isArmyOverlayExtended = IsArmyOverlayExtended();
                
                // Combine both checks
                bool anyBarExtended = isBarExtended || isArmyOverlayExtended;
                
                // Info log when bar state changes
                if (anyBarExtended != _wasArmyManagementOpen)
                {
                    ModLogger.Info("Interface", $"Bottom bar extended state changed: {_wasArmyManagementOpen} -> {anyBarExtended} (party bar={isBarExtended}, armyOverlay={isArmyOverlayExtended})");
                    _wasArmyManagementOpen = anyBarExtended;
                }
                
                // Update positioning if either panel is open
                bool shouldRepositionUp = isPartyScreenOpen || anyBarExtended;
                if (shouldRepositionUp != _wasRepositionedUp)
                {
                    ModLogger.Info("Interface", $"Combat log repositioning: {_wasRepositionedUp} -> {shouldRepositionUp} (party={isPartyScreenOpen}, bars={anyBarExtended})");
                    _wasRepositionedUp = shouldRepositionUp;
                }
                UpdatePositioning(shouldRepositionUp);
            }
            else
            {
                ModLogger.LogOnce("combat_log_null_datasource", "Interface", "_dataSource is null - repositioning code not running", LogLevel.Debug);
            }
            
            // Handle manual scroll detection and auto-scroll behavior
            if (_scrollablePanel != null && _scrollablePanel.VerticalScrollbar != null)
            {
                var scrollbar = _scrollablePanel.VerticalScrollbar;
                float currentScrollPosition = scrollbar.ValueFloat;
                
                // Detect manual scroll (user interaction)
                if (MathF.Abs(currentScrollPosition - _lastScrollPosition) > 0.01f)
                {
                    // User scrolled manually - pause auto-scroll
                    _shouldAutoScroll = false;
                    _timeSinceLastManualScroll = 0f;
                    _lastScrollPosition = currentScrollPosition;
                }
                
                // Update timer if auto-scroll is paused
                if (!_shouldAutoScroll)
                {
                    _timeSinceLastManualScroll += dt;
                    
                    // Resume auto-scroll after delay
                    if (_timeSinceLastManualScroll >= AutoScrollResumeDelay)
                    {
                        _shouldAutoScroll = true;
                    }
                }
                
                // Auto-scroll to bottom if enabled
                if (_shouldAutoScroll)
                {
                    scrollbar.ValueFloat = scrollbar.MaxValue;
                    _lastScrollPosition = scrollbar.ValueFloat;
                }
            }
        }
        
        private void InitializeUI()
        {
            try
            {
                // Find the map screen
                var mapScreen = ScreenManager.TopScreen;
                if (mapScreen == null)
                {
                    ModLogger.Warn("Interface", "Cannot initialize combat log: MapScreen not found");
                    return;
                }
                
                // Create Gauntlet layer with priority 1000 (above map, below menus)
                _layer = new GauntletLayer("EnlistedCombatLog", 1000);
                
                // Configure input: allow mouse input but don't restrict other inputs
                _layer.InputRestrictions.SetInputRestrictions(false);
                
                // Create ViewModel
                _dataSource = new EnlistedCombatLogVM();
                
                // Load prefab
                _movie = _layer.LoadMovie("EnlistedCombatLog", _dataSource);
                
                // Get reference to ScrollablePanel widget for manual scroll control
                var rootWidget = _movie.Movie.RootWidget;
                _scrollablePanel = rootWidget.FindChild("ScrollablePanel", true) as ScrollablePanel;
                if (_scrollablePanel != null && _scrollablePanel.VerticalScrollbar != null)
                {
                    _lastScrollPosition = _scrollablePanel.VerticalScrollbar.ValueFloat;
                    
                    // Subscribe to scroll events for immediate detection
                    _scrollablePanel.OnScroll += OnUserScroll;
                }
                
                // Get reference to the messages ListPanel to subscribe to link clicks
                _messagesListPanel = rootWidget.FindChild("InnerPanel", true) as ListPanel;
                
                // Subscribe to ViewModel Messages collection changes to handle new message widgets
                _dataSource.Messages.ListChanged += OnMessagesListChanged;
                
                // Add layer to screen (initially - will be managed by ManageLayerVisibility thereafter)
                mapScreen.AddLayer(_layer);
                _ownerScreen = mapScreen; // Store reference for later removal
                _isLayerActive = true;
                
                ModLogger.Info("Interface", "Enlisted combat log initialized successfully");
            }
            catch (System.Exception ex)
            {
                ModLogger.Caught("Interface", $"Failed to initialize combat log: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Called when user scrolls the panel with mouse wheel.
        /// Immediately pauses auto-scroll and resets timer.
        /// </summary>
        private void OnUserScroll(float scrollAmount)
        {
            // User is actively scrolling - pause auto-scroll and reset timer
            _shouldAutoScroll = false;
            _timeSinceLastManualScroll = 0f;
            
            if (_scrollablePanel?.VerticalScrollbar != null)
            {
                _lastScrollPosition = _scrollablePanel.VerticalScrollbar.ValueFloat;
            }
        }
        
        /// <summary>
        /// Called when the Messages collection changes (items added/removed).
        /// Subscribes to RichTextWidget EventFire events for new messages.
        /// </summary>
        private void OnMessagesListChanged(object sender, TaleWorlds.Library.ListChangedEventArgs e)
        {
            // Wait one frame for the UI to create the widget, then subscribe
            if (_messagesListPanel != null && e.ListChangedType == TaleWorlds.Library.ListChangedType.ItemAdded)
            {
                // Schedule subscription for next frame when widget exists
                _messagesListPanel.EventManager.AddLateUpdateAction(_messagesListPanel, dt =>
                {
                    SubscribeToMessageWidget(e.NewIndex);
                }, 1);
            }
        }
        
        /// <summary>
        /// Subscribes to the RichTextWidget EventFire event for a specific message at the given index.
        /// </summary>
        private void SubscribeToMessageWidget(int messageIndex)
        {
            if (_messagesListPanel == null || messageIndex < 0 || messageIndex >= _messagesListPanel.ChildCount)
                return;
            
            var messageWidget = _messagesListPanel.GetChild(messageIndex);
            
            // ItemTemplate widgets may not preserve ID attribute, so search by type instead
            // The message widget should have exactly one RichTextWidget child
            RichTextWidget richTextWidget = null;
            if (messageWidget != null)
            {
                for (int i = 0; i < messageWidget.ChildCount; i++)
                {
                    var child = messageWidget.GetChild(i);
                    if (child is RichTextWidget rtw)
                    {
                        richTextWidget = rtw;
                        break;
                    }
                }
            }
            
            if (richTextWidget != null)
            {
                richTextWidget.EventFire += OnRichTextWidgetEvent;
            }
        }
        
        /// <summary>
        /// Handles EventFire events from RichTextWidget instances.
        /// Processes LinkClick events and opens encyclopedia pages.
        /// </summary>
        private void OnRichTextWidgetEvent(Widget widget, string eventName, object[] args)
        {
            if (eventName != "LinkClick") return;
            
            if (args == null || args.Length == 0) return;
            
            string linkHref = args[0] as string;
            
            if (string.IsNullOrEmpty(linkHref)) return;
            
            // Strip "event:" prefix if present - EncyclopediaManager.GoToLink expects format like "Hero:lord_1_1"
            // but the href in RichTextWidget links includes the "event:" prefix like "event:Hero:lord_1_1"
            string encyclopediaLink = linkHref.StartsWith("event:") 
                ? linkHref.Substring(6) 
                : linkHref;
            
            try
            {
                // Open the encyclopedia page
                Campaign.Current.EncyclopediaManager.GoToLink(encyclopediaLink);
                
                // Reset inactivity timer on interaction
                _dataSource?.OnUserInteraction();
                
                ModLogger.Debug("Interface", $"Opened encyclopedia link from combat log: {encyclopediaLink} (original href: {linkHref})");
            }
            catch (System.Exception ex)
            {
                ModLogger.Caught("Interface", $"Failed to open encyclopedia link: {encyclopediaLink}", ex);
            }
        }
        
        /// <summary>
        /// Called by Harmony patch to add messages to the log.
        /// Colorizes kingdom links with faction-specific colors, preserves all other formatting.
        /// </summary>
        public void AddMessage(InformationMessage message)
        {
            if (_dataSource == null || message == null)
            {
                return;
            }
            
            // Debug logging for link formatting (only appears when Debug level is enabled)
            if (message.Information.Contains("<a "))
            {
                ModLogger.Debug("Interface", $"Message with link: {message.Information}");
            }
            
            // Replace generic Link.Kingdom styles with faction-specific colors
            // This makes each kingdom appear in its native banner color
            var colorizedText = FactionLinkColorizer.ColorizeFactionLinks(message.Information);
            
            // Create new message with colorized text
            var colorizedMessage = new InformationMessage(
                colorizedText,
                message.Color,
                message.SoundEventPath
            );
            
            _dataSource.AddMessage(colorizedMessage);
            
            // Don't force auto-scroll here - let the timer-based logic in OnTick handle it
            // This respects the user's manual scroll and the 6-second pause delay
        }
        
        /// <summary>
        /// Updates positioning to avoid menu overlap (party screen, inventory, etc.).
        /// Automatically called each tick based on MapScreen.IsInMenu detection.
        /// </summary>
        public void UpdatePositioning(bool isMenuOpen)
        {
            _dataSource?.UpdatePositioning(isMenuOpen);
        }
        
        private void OnGameOver()
        {
            CleanUp();
        }
        
        private void OnConversationEnded(System.Collections.Generic.IEnumerable<CharacterObject> characters)
        {
            // Conversation ended - layer will be re-added on next tick if needed
            _wasInConversation = false;
        }
        
        /// <summary>
        /// Suspends the combat log layer (hides it during conversations).
        /// Called by Harmony patch when map conversations start.
        /// </summary>
        public void SuspendLayer()
        {
            if (_layer == null)
            {
                return;
            }
            
            _wasInConversation = true;
            
            if (!_isLayerActive)
            {
                return; // Already suspended
            }
            
            ScreenManager.SetSuspendLayer(_layer, true);
            _isLayerActive = false;
            ModLogger.Debug("Interface", "Combat log layer suspended");
        }
        
        /// <summary>
        /// Resumes the combat log layer (shows it after conversations end).
        /// Called by Harmony patch when map conversations end.
        /// </summary>
        public void ResumeLayer()
        {
            if (_layer == null)
            {
                return;
            }
            
            _wasInConversation = false;
            
            if (_isLayerActive)
            {
                return; // Already active
            }
            
            // Only resume if enlisted
            if (EnlistmentBehavior.Instance?.IsEnlisted != true)
            {
                return;
            }
            
            ScreenManager.SetSuspendLayer(_layer, false);
            _isLayerActive = true;
            _dataSource?.UpdateVisibility();
            ModLogger.Debug("Interface", "Combat log layer resumed");
        }
        
        /// <summary>
        /// Manages the combat log visibility based on enlistment state and current scene.
        /// Called every tick. Map conversations are also handled via Harmony patches.
        /// </summary>
        private void ManageLayerVisibility()
        {
            if (_layer == null)
            {
                return;
            }
            
            bool isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted ?? false;
            
            // Check if player is in a mission/scene (battles, taverns, halls, arenas, etc.)
            bool isInMission = TaleWorlds.MountAndBlade.Mission.Current != null;
            
            // Check if encyclopedia is open
            bool isEncyclopediaOpen = IsEncyclopediaOpen();
            
            // Should the layer be suspended?
            // Suspend if: not enlisted, in a mission, in a conversation (set by Harmony patch), or encyclopedia is open
            bool shouldSuspend = !isEnlisted || isInMission || _wasInConversation || isEncyclopediaOpen;
            
            // Suspend/resume layer as needed
            if (shouldSuspend && _isLayerActive)
            {
                ScreenManager.SetSuspendLayer(_layer, true);
                _isLayerActive = false;
                ModLogger.Debug("Interface", $"Combat log suspended (enlisted={isEnlisted}, mission={isInMission}, conversation={_wasInConversation}, encyclopedia={isEncyclopediaOpen})");
            }
            else if (!shouldSuspend && !_isLayerActive)
            {
                ScreenManager.SetSuspendLayer(_layer, false);
                _isLayerActive = true;
                _dataSource?.UpdateVisibility();
                ModLogger.Debug("Interface", $"Combat log resumed (enlisted={isEnlisted}, mission={isInMission}, conversation={_wasInConversation}, encyclopedia={isEncyclopediaOpen})");
            }
        }
        
        /// <summary>
        /// Checks if the encyclopedia screen is currently open.
        /// The combat log needs to be suspended when encyclopedia is open to avoid blocking its close button.
        /// </summary>
        private bool IsEncyclopediaOpen()
        {
            try
            {
                // Get the MapScreen
                var mapScreen = ScreenManager.TopScreen;
                if (mapScreen == null)
                {
                    return false;
                }
                
                // Use reflection to call GetMapView<MapEncyclopediaView>() since we don't have direct access to MapScreen type
                var getMapViewMethod = mapScreen.GetType().GetMethod("GetMapView");
                if (getMapViewMethod == null)
                {
                    return false;
                }
                
                // Make it generic for MapEncyclopediaView
                var mapEncyclopediaViewType = Type.GetType("SandBox.View.Map.MapEncyclopediaView, SandBox.View");
                if (mapEncyclopediaViewType == null)
                {
                    return false;
                }
                
                var genericMethod = getMapViewMethod.MakeGenericMethod(mapEncyclopediaViewType);
                var encyclopediaView = genericMethod.Invoke(mapScreen, null);
                
                if (encyclopediaView == null)
                {
                    return false;
                }
                
                // Get the IsEncyclopediaOpen property
                var isOpenProperty = encyclopediaView.GetType().GetProperty("IsEncyclopediaOpen");
                if (isOpenProperty == null)
                {
                    return false;
                }
                
                return (bool)isOpenProperty.GetValue(encyclopediaView);
            }
            catch (Exception ex)
            {
                ModLogger.Debug("Interface", $"Failed to check encyclopedia state: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Checks if the party bar (bottom-left/right panel) is currently extended.
        /// This tracks the expand/collapse state of bottom UI panels.
        /// </summary>
        private bool IsBarExtended()
        {
            try
            {
                var mapScreen = ScreenManager.TopScreen;
                if (mapScreen == null) return false;
                
                var prop = mapScreen.GetType().GetProperty("IsBarExtended");
                if (prop == null) return false;
                
                return (bool)prop.GetValue(mapScreen);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Interface", $"Failed to check bar extended state: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Checks if the army overlay panel is currently visible on screen.
        /// The overlay shows when player is in an army and displays party portraits,
        /// food, cohesion, and troop counts on the right side of the map screen.
        /// Returns true if the overlay exists and has a valid ViewModel.
        /// </summary>
        private bool IsArmyOverlayExtended()
        {
            try
            {
                var mapScreen = ScreenManager.TopScreen;
                if (mapScreen == null) return false;
                
                // Get the _armyOverlay MapView field - search up the inheritance chain
                FieldInfo armyOverlayField = null;
                var type = mapScreen.GetType();
                int searchDepth = 0;
                while (type != null && armyOverlayField == null && searchDepth < 10)
                {
                    armyOverlayField = type.GetField("_armyOverlay", BindingFlags.NonPublic | BindingFlags.Instance);
                    type = type.BaseType;
                    searchDepth++;
                }
                
                if (armyOverlayField == null) return false;
                
                var armyOverlay = armyOverlayField.GetValue(mapScreen);
                if (armyOverlay == null) return false; // Not in army
                
                // Check if the ViewModel exists (indicates overlay is active)
                var dataSourceField = armyOverlay.GetType().GetField("_overlayDataSource", BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataSourceField == null) return false;
                
                var viewModel = dataSourceField.GetValue(armyOverlay);
                
                // The army overlay is visible when both armyOverlay and viewModel exist
                return viewModel != null;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Interface", $"Failed to check army overlay state: {ex.Message}", ex);
                return false;
            }
        }
        
        private void CleanUp()
        {
            try
            {
                // Unsubscribe from scroll events
                if (_scrollablePanel != null)
                {
                    _scrollablePanel.OnScroll -= OnUserScroll;
                    _scrollablePanel = null;
                }
                
                // Unsubscribe from message list events
                if (_dataSource != null)
                {
                    _dataSource.Messages.ListChanged -= OnMessagesListChanged;
                }
                
                // Unsubscribe from all existing RichTextWidget EventFire events
                if (_messagesListPanel != null)
                {
                    for (int i = 0; i < _messagesListPanel.ChildCount; i++)
                    {
                        var messageWidget = _messagesListPanel.GetChild(i);
                        
                        // Find RichTextWidget by type (same as subscription logic)
                        RichTextWidget richTextWidget = null;
                        if (messageWidget != null)
                        {
                            for (int j = 0; j < messageWidget.ChildCount; j++)
                            {
                                var child = messageWidget.GetChild(j);
                                if (child is RichTextWidget rtw)
                                {
                                    richTextWidget = rtw;
                                    break;
                                }
                            }
                        }
                        
                        if (richTextWidget != null)
                        {
                            richTextWidget.EventFire -= OnRichTextWidgetEvent;
                        }
                    }
                    
                    _messagesListPanel = null;
                }
                
                if (_layer != null)
                {
                    // Release movie first
                    _layer.ReleaseMovie(_movie);
                    
                    // Remove layer from the screen we added it to
                    if (_ownerScreen != null)
                    {
                        _ownerScreen.RemoveLayer(_layer);
                    }
                    _layer = null;
                    _ownerScreen = null;
                    _isLayerActive = false;
                }
                
                // Clean up ViewModel
                if (_dataSource != null)
                {
                    _dataSource.OnFinalize();
                    _dataSource = null;
                }
                
                Instance = null;
                _isInitialized = false;
                
                ModLogger.Info("Interface", "Enlisted combat log cleaned up");
            }
            catch (System.Exception ex)
            {
                ModLogger.Caught("Interface", "Error during combat log cleanup", ex);
            }
        }
    }
}
