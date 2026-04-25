using Enlisted.Features.Content;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Interface
{
    /// <summary>
    /// Caches the KINGDOM, CAMP, and YOU sections for the Main Menu to prevent flicker
    /// and optimize performance. Refreshes content intelligently based on game state changes.
    /// Phase 5: UI Integration - Content Orchestrator.
    /// </summary>
    public class MainMenuNewsCache : CampaignBehaviorBase
    {
        public static MainMenuNewsCache Instance { get; private set; }

        private const string LogCategory = "MainMenuCache";
        private const float KingdomRefreshIntervalHours = 24f;
        private const float CampRefreshIntervalHours = 6f; // Matches DayPhase changes
        private const float YouRefreshIntervalHours = 1f; // More frequent for player state

        private string _kingdomText = string.Empty;
        private CampaignTime _lastKingdomRefreshTime = CampaignTime.Zero;

        private string _campText = string.Empty;
        private DayPhase _lastCampRefreshPhase = DayPhase.Night; // Initialize to ensure first refresh
        private CampaignTime _lastCampRefreshTime = CampaignTime.Zero;

        private string _youNowText = string.Empty;
        private string _youAheadText = string.Empty;
        private CampaignTime _lastYouRefreshTime = CampaignTime.Zero;

        private CampaignTime _lastCheckTime = CampaignTime.Zero; // For fast travel detection

        private EnlistmentBehavior _enlistment;
        private EnlistedNewsBehavior _newsBehavior;
        private ForecastGenerator _forecastGenerator;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            // Future: Add specific triggers for major kingdom events, player state changes, etc.
        }

        public override void SyncData(IDataStore dataStore)
        {
            _ = dataStore.SyncData("mm_kingdomText", ref _kingdomText);
            _ = dataStore.SyncData("mm_lastKingdomRefreshTime", ref _lastKingdomRefreshTime);
            _ = dataStore.SyncData("mm_campText", ref _campText);
            _ = dataStore.SyncData("mm_lastCampRefreshPhase", ref _lastCampRefreshPhase);
            _ = dataStore.SyncData("mm_lastCampRefreshTime", ref _lastCampRefreshTime);
            _ = dataStore.SyncData("mm_youNowText", ref _youNowText);
            _ = dataStore.SyncData("mm_youAheadText", ref _youAheadText);
            _ = dataStore.SyncData("mm_lastYouRefreshTime", ref _lastYouRefreshTime);
            _ = dataStore.SyncData("mm_lastCheckTime", ref _lastCheckTime);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Instance = this;
            _enlistment = Campaign.Current.GetCampaignBehavior<EnlistmentBehavior>();
            _newsBehavior = Campaign.Current.GetCampaignBehavior<EnlistedNewsBehavior>();

            // Initialize ForecastGenerator
            var escalationManager = Campaign.Current.GetCampaignBehavior<EscalationManager>();

            if (_enlistment != null && escalationManager != null)
            {
                _forecastGenerator = new ForecastGenerator(_enlistment, escalationManager);
            }

            // Force initial refresh on launch
            ForceRefreshAll();
            ModLogger.Info(LogCategory, "MainMenuNewsCache initialized.");
        }

        private void OnHourlyTick()
        {
            RefreshIfNeeded();
        }

        private void OnDailyTick()
        {
            // Daily refresh for kingdom, ensures it updates at least once a day
            RefreshKingdom(true);
        }

        /// <summary>
        /// Refreshes sections intelligently based on time intervals and state changes.
        /// Detects fast travel (time jumps) and forces full refresh when needed.
        /// </summary>
        public void RefreshIfNeeded()
        {
            if (_enlistment?.IsEnlisted != true)
            {
                return;
            }

            var currentTime = CampaignTime.Now;
            var hoursSinceLastCheck = (currentTime - _lastCheckTime).ToHours;

            // Force refresh all if time jumped significantly (e.g., fast travel)
            if (hoursSinceLastCheck > 2f)
            {
                ModLogger.Debug(LogCategory, $"Time jump detected ({hoursSinceLastCheck:F1}h). Forcing full refresh.");
                ForceRefreshAll();
                _lastCheckTime = currentTime;
                return;
            }

            RefreshKingdom();
            RefreshCamp();
            RefreshYou();

            _lastCheckTime = currentTime;
        }

        /// <summary>
        /// Forces a full refresh of all sections (used on session launch or major state changes).
        /// </summary>
        public void ForceRefreshAll()
        {
            RefreshKingdom(true);
            RefreshCamp(true);
            RefreshYou(true);
        }

        private void RefreshKingdom(bool force = false)
        {
            if (_enlistment?.IsEnlisted != true)
            {
                return;
            }

            if (force || (CampaignTime.Now - _lastKingdomRefreshTime).ToHours >= KingdomRefreshIntervalHours)
            {
                _kingdomText = _newsBehavior?.BuildKingdomSummary() ?? string.Empty;
                _lastKingdomRefreshTime = CampaignTime.Now;
                ModLogger.Debug(LogCategory, "Kingdom section refreshed.");
            }
        }

        /// <summary>
        /// Called by ContentOrchestrator when DayPhase changes to trigger camp refresh.
        /// </summary>
        public void OnPhaseChanged(DayPhase newPhase)
        {
            ModLogger.Debug(LogCategory, $"Day phase changed to {newPhase}. Triggering Camp refresh.");
            RefreshCamp(true);
        }

        private void RefreshCamp(bool force = false)
        {
            if (_enlistment?.IsEnlisted != true)
            {
                return;
            }

            // Simplified: refresh based on time interval only (Phase tracking will be added later)
            if (force || (CampaignTime.Now - _lastCampRefreshTime).ToHours >= CampRefreshIntervalHours)
            {
                _campText = _newsBehavior?.BuildCampSummary() ?? string.Empty;
                _lastCampRefreshTime = CampaignTime.Now;
                ModLogger.Debug(LogCategory, "Camp section refreshed.");
            }
        }

        private void RefreshYou(bool force = false)
        {
            if (_enlistment?.IsEnlisted != true)
            {
                return;
            }

            // More frequent refresh for player-specific info or on explicit state changes
            if (force || (CampaignTime.Now - _lastYouRefreshTime).ToHours >= YouRefreshIntervalHours)
            {
                if (_forecastGenerator != null)
                {
                    (_youNowText, _youAheadText) = _forecastGenerator.BuildPlayerStatus();
                }
                else
                {
                    _youNowText = "Status unavailable.";
                    _youAheadText = "Forecast unavailable.";
                }

                _lastYouRefreshTime = CampaignTime.Now;
                ModLogger.Debug(LogCategory, "YOU section refreshed.");
            }
        }

        /// <summary>
        /// Gets the cached KINGDOM section text.
        /// </summary>
        public string GetKingdomSection()
        {
            return _kingdomText;
        }

        /// <summary>
        /// Gets the cached CAMP section text.
        /// </summary>
        public string GetCampSection()
        {
            return _campText;
        }

        /// <summary>
        /// Gets the cached YOU section text (NOW and AHEAD parts).
        /// </summary>
        public (string Now, string Ahead) GetYouSection()
        {
            return (_youNowText, _youAheadText);
        }
    }
}
