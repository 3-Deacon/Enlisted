using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Utils;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Enlisted.Features.Interface.ViewModels
{
    /// <summary>
    /// Main ViewModel for the enlisted combat log.
    /// Manages the scrollable list of combat messages, expiration, and visibility.
    /// </summary>
    public class EnlistedCombatLogVM : ViewModel
    {
        private const int MaxMessages = 50;
        private const float MessageLifetimeSeconds = 300f; // 5 minutes
        private const float FadeStartSeconds = 270f; // Start fading at 4.5 minutes
        private const float InactivityFadeDelay = 10f; // Fade after 10 seconds of inactivity
        private const float DimmedAlpha = 0.35f; // Dimmed opacity
        private const float FullAlpha = 1.0f; // Full opacity
        public const float CompactWidth = 500f;
        public const float CompactHeight = 340f;

        private bool _isVisible;
        private float _containerWidth;
        private float _containerHeight;
        private float _expandedWidth;
        private float _expandedHeight;
        private float _resizePreviewWidth;
        private float _resizePreviewHeight;
        private bool _showResizeFrame;
        private float _panelOffsetX;
        private float _panelOffsetY;
        private int _backgroundMode;
        private float _containerAlpha;
        private float _timeSinceLastActivity;
        private MBBindingList<CombatLogMessageVM> _messages;

        public EnlistedCombatLogVM()
        {
            Messages = new MBBindingList<CombatLogMessageVM>();
            _expandedWidth = CombatLogUiStateStore.DefaultExpandedWidth;
            _expandedHeight = CombatLogUiStateStore.DefaultExpandedHeight;
            UpdateVisibility();
            ApplyCurrentSize();
            ContainerAlpha = FullAlpha; // Start at full opacity
            _timeSinceLastActivity = 0f;
        }

        /// <summary>
        /// List of combat log messages displayed in the UI.
        /// </summary>
        [DataSourceProperty]
        public MBBindingList<CombatLogMessageVM> Messages
        {
            get => _messages;
            set
            {
                if (_messages != value)
                {
                    _messages = value;
                    OnPropertyChangedWithValue(value, nameof(Messages));
                }
            }
        }

        /// <summary>
        /// Controls visibility based on enlistment state.
        /// </summary>
        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsVisible));
                }
            }
        }

        [DataSourceProperty]
        public float ContainerWidth
        {
            get => _containerWidth;
            set
            {
                if (_containerWidth != value)
                {
                    _containerWidth = value;
                    OnPropertyChangedWithValue(value, nameof(ContainerWidth));
                }
            }
        }

        [DataSourceProperty]
        public float ContainerHeight
        {
            get => _containerHeight;
            set
            {
                if (_containerHeight != value)
                {
                    _containerHeight = value;
                    OnPropertyChangedWithValue(value, nameof(ContainerHeight));
                }
            }
        }

        [DataSourceProperty]
        public float ResizePreviewWidth
        {
            get => _resizePreviewWidth;
            set
            {
                if (_resizePreviewWidth != value)
                {
                    _resizePreviewWidth = value;
                    OnPropertyChangedWithValue(value, nameof(ResizePreviewWidth));
                }
            }
        }

        [DataSourceProperty]
        public float ResizePreviewHeight
        {
            get => _resizePreviewHeight;
            set
            {
                if (_resizePreviewHeight != value)
                {
                    _resizePreviewHeight = value;
                    OnPropertyChangedWithValue(value, nameof(ResizePreviewHeight));
                }
            }
        }

        [DataSourceProperty]
        public bool ShowResizeFrame
        {
            get => _showResizeFrame;
            set
            {
                if (_showResizeFrame != value)
                {
                    _showResizeFrame = value;
                    OnPropertyChangedWithValue(value, nameof(ShowResizeFrame));
                }
            }
        }

        [DataSourceProperty]
        public bool ShowResizeHandle => true;

        [DataSourceProperty]
        public float PanelOffsetX
        {
            get => _panelOffsetX;
            set
            {
                if (_panelOffsetX != value)
                {
                    _panelOffsetX = value;
                    OnPropertyChangedWithValue(value, nameof(PanelOffsetX));
                }
            }
        }

        [DataSourceProperty]
        public float PanelOffsetY
        {
            get => _panelOffsetY;
            set
            {
                if (_panelOffsetY != value)
                {
                    _panelOffsetY = value;
                    OnPropertyChangedWithValue(value, nameof(PanelOffsetY));
                }
            }
        }

        [DataSourceProperty]
        public bool ShowSolidBackground => _backgroundMode == 0;

        [DataSourceProperty]
        public bool ShowDimBackground => _backgroundMode == 1;

        [DataSourceProperty]
        public bool ShowTextOnlyBackground => _backgroundMode == 2;

        [DataSourceProperty]
        public string BackgroundButtonText => "BG";

        [DataSourceProperty]
        public string HeaderText => "Campaign Log";

        /// <summary>
        /// Overall opacity of the combat log container.
        /// Fades to dimmed after inactivity, returns to full on activity.
        /// </summary>
        [DataSourceProperty]
        public float ContainerAlpha
        {
            get => _containerAlpha;
            set
            {
                if (_containerAlpha != value)
                {
                    _containerAlpha = value;
                    OnPropertyChangedWithValue(value, nameof(ContainerAlpha));
                }
            }
        }

        /// <summary>
        /// Adds a new message to the combat log.
        /// Enforces message cap and updates visibility.
        /// Resets inactivity timer and restores full opacity.
        /// Messages are used as-is to preserve both mod colors and native rich text/links.
        /// </summary>
        public void AddMessage(InformationMessage message)
        {
            if (message == null)
            {
                return;
            }

            // Use message text as-is - no modifications
            // Mod messages: Already have proper colors via message.Color
            // Native messages: Already have rich text formatting and links built-in
            var messageVM = new CombatLogMessageVM(
                message.Information,
                message.Color
            );

            Messages.Add(messageVM);

            // Enforce message cap
            while (Messages.Count > MaxMessages)
            {
                Messages.RemoveAt(0);
            }

            // Reset activity timer and restore full opacity on new message
            _timeSinceLastActivity = 0f;
            ContainerAlpha = FullAlpha;
        }

        /// <summary>
        /// Called each frame to handle message expiration, fade effects, and inactivity dimming.
        /// </summary>
        public void Tick(float dt)
        {
            // Remove expired messages
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                var message = Messages[i];
                float age = message.GetAgeInSeconds();

                if (age >= MessageLifetimeSeconds)
                {
                    Messages.RemoveAt(i);
                }
                else if (age >= FadeStartSeconds)
                {
                    // Apply fade effect in last 30 seconds
                    float fadeProgress = (age - FadeStartSeconds) / (MessageLifetimeSeconds - FadeStartSeconds);
                    message.AlphaFactor = 1.0f - fadeProgress;
                }
            }

            // Handle inactivity fade
            _timeSinceLastActivity += dt;

            if (_timeSinceLastActivity >= InactivityFadeDelay)
            {
                // Fade to dimmed after inactivity
                ContainerAlpha = DimmedAlpha;
            }
            else
            {
                // Keep at full opacity during activity
                ContainerAlpha = FullAlpha;
            }

            // Update visibility based on enlistment state
            UpdateVisibility();
        }

        /// <summary>
        /// Updates visibility based on current enlistment state and mission state.
        /// Hides during missions (taverns, halls, etc.) - only visible on campaign map.
        /// Note: Conversations are handled by removing the entire layer, not just hiding the widget.
        /// </summary>
        public void UpdateVisibility()
        {
            bool isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted ?? false;
            bool isInMission = TaleWorlds.MountAndBlade.Mission.Current != null;

            // Only visible when enlisted AND on campaign map (not in any mission/scene)
            // Conversations are handled at the layer level by EnlistedCombatLogBehavior
            IsVisible = isEnlisted && !isInMission;
        }

        public void RestoreExpandedSize(float width, float height)
        {
            _expandedWidth = CombatLogUiStateStore.ClampWidth(width, Screen.RealScreenResolutionWidth);
            _expandedHeight = CombatLogUiStateStore.ClampHeight(height, Screen.RealScreenResolutionHeight);
            ApplyCurrentSize();
        }

        public void RestorePlacement(float offsetX, float offsetY)
        {
            PanelOffsetX = CombatLogUiStateStore.ClampOffsetX(offsetX, ContainerWidth, Screen.RealScreenResolutionWidth);
            PanelOffsetY = CombatLogUiStateStore.ClampOffsetY(offsetY, ContainerHeight, Screen.RealScreenResolutionHeight);
        }

        public void RestoreBackgroundMode(int backgroundMode)
        {
            _backgroundMode = CombatLogUiStateStore.ClampBackgroundMode(backgroundMode);
            OnPropertyChangedWithValue(ShowSolidBackground, nameof(ShowSolidBackground));
            OnPropertyChangedWithValue(ShowDimBackground, nameof(ShowDimBackground));
            OnPropertyChangedWithValue(ShowTextOnlyBackground, nameof(ShowTextOnlyBackground));
        }

        public int GetBackgroundMode()
        {
            return _backgroundMode;
        }

        public void ExecuteCycleBackgroundMode()
        {
            RestoreBackgroundMode((_backgroundMode + 1) % 3);
            CombatLogUiStateStore.Save(ContainerWidth, ContainerHeight, PanelOffsetX, PanelOffsetY, _backgroundMode);
            OnUserInteraction();
        }

        public void BeginResizePreview()
        {
            ShowResizeFrame = true;
            ResizePreviewWidth = ContainerWidth;
            ResizePreviewHeight = ContainerHeight;
        }

        public void UpdateResizePreview(float width, float height)
        {
            float clampedWidth = CombatLogUiStateStore.ClampWidth(width, Screen.RealScreenResolutionWidth);
            float clampedHeight = CombatLogUiStateStore.ClampHeight(height, Screen.RealScreenResolutionHeight);

            ResizePreviewWidth = clampedWidth;
            ResizePreviewHeight = clampedHeight;
            ShowResizeFrame = true;
        }

        public void CommitExpandedSize(float width, float height)
        {
            _expandedWidth = CombatLogUiStateStore.ClampWidth(width, Screen.RealScreenResolutionWidth);
            _expandedHeight = CombatLogUiStateStore.ClampHeight(height, Screen.RealScreenResolutionHeight);
            ShowResizeFrame = false;
            ApplyCurrentSize();
            PanelOffsetX = CombatLogUiStateStore.ClampOffsetX(PanelOffsetX, ContainerWidth, Screen.RealScreenResolutionWidth);
            PanelOffsetY = CombatLogUiStateStore.ClampOffsetY(PanelOffsetY, ContainerHeight, Screen.RealScreenResolutionHeight);
        }

        public void CancelResizePreview()
        {
            ShowResizeFrame = false;
            ApplyCurrentSize();
        }

        private void ApplyCurrentSize()
        {
            ContainerWidth = _expandedWidth;
            ContainerHeight = _expandedHeight;
        }

        /// <summary>
        /// Called when user interacts with the log (hover, scroll, clicks links).
        /// Resets inactivity timer and restores full opacity.
        /// </summary>
        public void OnUserInteraction()
        {
            _timeSinceLastActivity = 0f;
            ContainerAlpha = FullAlpha;
        }

        /// <summary>
        /// Clears all messages from the log.
        /// </summary>
        public void Clear()
        {
            Messages.Clear();
            ModLogger.Debug("Interface", "Combat log cleared");
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            Messages.Clear();
        }
    }
}
