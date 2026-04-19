using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Camp;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Company;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.SaveSystem;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// A single scheduled opportunity, locked once generated for the day.
    /// Part of the Orchestrator's pre-scheduling system that prevents opportunities
    /// from disappearing when context changes mid-session.
    /// </summary>
    public class ScheduledOpportunity
    {
        /// <summary>Opportunity definition ID (e.g., "opp_card_game").</summary>
        public string OpportunityId { get; set; }

        /// <summary>Target decision to fire (e.g., "dec_gamble_cards").</summary>
        public string TargetDecisionId { get; set; }

        /// <summary>Phase when this opportunity is available.</summary>
        public DayPhase Phase { get; set; }

        /// <summary>Display name for menu.</summary>
        public string DisplayName { get; set; }

        /// <summary>Narrative hint for Daily Brief (e.g., "A card game is forming").</summary>
        public string NarrativeHint { get; set; }

        /// <summary>True if player has engaged with this opportunity (event fired).</summary>
        public bool Consumed { get; set; }

        /// <summary>
        /// True if player has committed to this opportunity (clicked to schedule).
        /// Committed opportunities are greyed out in menu and will fire when their phase arrives.
        /// </summary>
        public bool PlayerCommitted { get; set; }

        /// <summary>Fitness score when generated (for debugging).</summary>
        public float FitnessScore { get; set; }

        /// <summary>The underlying CampOpportunity for menu display conversion.</summary>
        public CampOpportunity SourceOpportunity { get; set; }

        /// <summary>
        /// Returns true if this opportunity is available to click (not consumed, not committed).
        /// </summary>
        public bool IsAvailableToCommit => !Consumed && !PlayerCommitted;

        /// <summary>
        /// Returns true if this opportunity is waiting to fire (committed but not consumed).
        /// </summary>
        public bool IsScheduledToFire => PlayerCommitted && !Consumed;
    }

    /// <summary>
    /// Central coordinator for content pacing in the Enlisted mod.
    /// Analyzes world state and provides activity levels to OrderProgressionBehavior.
    /// Generates forecasts for UI and updates camp opportunities for player decisions.
    /// Does NOT fire automatic events - content delivery happens through order events (during duty) 
    /// and player-initiated camp decisions (DECISIONS menu).
    /// </summary>
    public class ContentOrchestrator : CampaignBehaviorBase
    {
        private const string LogCategory = "ORCHESTRATOR";

        /// <summary>Singleton instance for global access.</summary>
        public static ContentOrchestrator Instance { get; private set; }

        // Track last day phase for phase change detection
        private DayPhase _lastPhase = DayPhase.Night;

        // Behavior tracking data for save/load
        private Dictionary<string, int> _behaviorCounts = new Dictionary<string, int>();
        private Dictionary<string, int> _contentEngagement = new Dictionary<string, int>();

        // Override system tracking
        private int _lastVarietyInjectionDay = -10;
        private int _varietyInjectionsThisWeek;
        private int _weekStartDay;
        private JObject _overrideConfig;
        private bool _overrideConfigLoaded;
        private OrchestratorOverride _currentOverride;
        private DayPhase _currentOverridePhase = DayPhase.Night;

        // Medical orchestration tracking (Phase 6H)
        private int _lastMedicalCheckDay = -1;
        private int _consecutiveHighMedicalPressureDays;
        private bool _medicalOpportunityQueuedToday;
        private bool _emergencyOpportunityForced;
        private int _lastIllnessOnsetDay = -10;

        #region Opportunity Scheduling (Orchestrator Unification)

        /// <summary>
        /// Pre-scheduled opportunities for each phase. Generated once per day.
        /// Locked until consumed, fired, or day ends. This prevents the jarring
        /// disappearance of opportunities when context changes (e.g., lord leaves settlement).
        /// </summary>
        private Dictionary<DayPhase, List<ScheduledOpportunity>> _scheduledOpportunities;

        /// <summary>
        /// Pre-scheduled opportunities for TOMORROW (cross-day forecasting).
        /// When it's late in the day (Dusk/Night), we also schedule tomorrow's early phases
        /// to maintain a rolling 24-hour visibility window per the design spec.
        /// </summary>
        private Dictionary<DayPhase, List<ScheduledOpportunity>> _scheduledTomorrowOpportunities;

        /// <summary>
        /// Day the current schedule was generated. Used to detect new day.
        /// </summary>
        private int _scheduledDay = -1;

        /// <summary>
        /// Maximum opportunities per phase during normal activity.
        /// </summary>
        private const int MaxOpportunitiesPerPhase = 3;

        #endregion

        public ContentOrchestrator()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            ModLogger.Info(LogCategory, "Content Orchestrator registered for daily and hourly ticks");
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                // Save/load behavior tracking data
                dataStore.SyncData("orchestrator_behaviorCounts", ref _behaviorCounts);
                dataStore.SyncData("orchestrator_contentEngagement", ref _contentEngagement);

                // Sync last phase as int
                int lastPhaseInt = (int)_lastPhase;
                dataStore.SyncData("orchestrator_lastPhase", ref lastPhaseInt);
                _lastPhase = (DayPhase)lastPhaseInt;

                // Medical orchestration tracking (Phase 6H)
                dataStore.SyncData("orchestrator_lastMedicalCheckDay", ref _lastMedicalCheckDay);
                dataStore.SyncData("orchestrator_consecutiveHighMedicalDays", ref _consecutiveHighMedicalPressureDays);
                dataStore.SyncData("orchestrator_medicalOpportunityQueued", ref _medicalOpportunityQueuedToday);
                dataStore.SyncData("orchestrator_emergencyForced", ref _emergencyOpportunityForced);
                dataStore.SyncData("orchestrator_lastIllnessDay", ref _lastIllnessOnsetDay);

                // After loading, restore PlayerBehaviorTracker state
                if (dataStore.IsLoading)
                {
                    PlayerBehaviorTracker.LoadState(_behaviorCounts, _contentEngagement);
                    ModLogger.Debug(LogCategory, "Restored behavior tracking state from save");

                    // Scheduled opportunities don't persist - regenerate on load (per spec)
                    ForceReschedule();
                }
            });
        }

        /// <summary>
        /// Called every in-game hour. Checks for day phase transitions.
        /// </summary>
        private void OnHourlyTick()
        {
            if (!IsActive())
            {
                return;
            }

            CheckPhaseTransition();
        }

        /// <summary>
        /// Checks if the day phase has changed and fires OnDayPhaseChanged if so.
        /// </summary>
        private void CheckPhaseTransition()
        {
            var currentHour = CampaignTime.Now.GetHourOfDay;
            var currentPhase = WorldStateAnalyzer.GetDayPhaseFromHour(currentHour);

            if (currentPhase != _lastPhase)
            {
                _lastPhase = currentPhase;
                OnDayPhaseChanged(currentPhase);
            }
        }

        /// <summary>
        /// Fires when military day phase changes (4x per day).
        /// Fires committed opportunities and cleans up missed ones.
        /// </summary>
        private void OnDayPhaseChanged(DayPhase newPhase)
        {
            ModLogger.Info(LogCategory, $"Day phase changed to {newPhase}");

            // Clean up uncommitted opportunities from the previous phase (they were missed)
            var previousPhase = GetPreviousPhase(newPhase);
            CleanupMissedOpportunities(previousPhase);

            // Fire committed opportunities for the new phase
            FireCommittedOpportunities(newPhase);

            // Notify camp life systems to refresh for new phase
            Camp.CampOpportunityGenerator.Instance?.OnPhaseChanged(newPhase);
        }

        /// <summary>
        /// Gets the phase that came before the given phase.
        /// </summary>
        private static DayPhase GetPreviousPhase(DayPhase phase)
        {
            return phase switch
            {
                DayPhase.Dawn => DayPhase.Night,
                DayPhase.Midday => DayPhase.Dawn,
                DayPhase.Dusk => DayPhase.Midday,
                DayPhase.Night => DayPhase.Dusk,
                _ => DayPhase.Night
            };
        }

        /// <summary>
        /// Fires all committed opportunities for the given phase.
        /// Called when a phase boundary is crossed.
        /// </summary>
        private void FireCommittedOpportunities(DayPhase phase)
        {
            var toFire = GetOpportunitiesToFireNow();

            foreach (var opp in toFire)
            {
                try
                {
                    ModLogger.Info(LogCategory, $"⚡ Auto-firing committed opportunity: {opp.OpportunityId}");

                    // Mark as consumed
                    opp.Consumed = true;

                    // Fire the decision event
                    if (!string.IsNullOrEmpty(opp.TargetDecisionId))
                    {
                        var decision = DecisionCatalog.GetDecision(opp.TargetDecisionId);
                        if (decision != null)
                        {
                            // Convert to event and queue for delivery
                            var eventDef = new EventDefinition
                            {
                                Id = decision.Id,
                                TitleId = decision.TitleId,
                                TitleFallback = decision.TitleFallback,
                                SetupId = decision.SetupId,
                                SetupFallback = decision.SetupFallback,
                                Category = decision.Category,
                                Requirements = decision.Requirements,
                                Timing = decision.Timing,
                                Options = decision.Options
                            };
                            var director = StoryDirector.Instance;
                            if (director != null)
                            {
                                director.EmitCandidate(new StoryCandidate
                                {
                                    SourceId = "content.committed_opportunity." + (opp.SourceOpportunity?.Type.ToString() ?? "unknown"),
                                    CategoryId = "opportunity." + (opp.SourceOpportunity?.Type.ToString() ?? "unknown"),
                                    ProposedTier = StoryTier.Modal,
                                    SeverityHint = 0.5f,
                                    Beats = { StoryBeat.OrderPhaseTransition },
                                    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                                    EmittedAt = CampaignTime.Now,
                                    InteractiveEvent = eventDef,
                                    RenderedTitle = eventDef.TitleFallback,
                                    RenderedBody = eventDef.SetupFallback,
                                    StoryKey = eventDef.Id
                                });
                            }
                            else
                            {
                                EventDeliveryManager.Instance?.QueueEvent(eventDef);
                            }
                            ModLogger.Info(LogCategory, $"  ✓ Queued decision event: {opp.TargetDecisionId}");
                        }
                        else
                        {
                            ModLogger.Warn(LogCategory, $"  Decision not found: {opp.TargetDecisionId}");
                        }
                    }
                    else
                    {
                        ModLogger.Warn(LogCategory, $"  No target decision for: {opp.OpportunityId}");
                    }

                    // Record engagement
                    if (opp.SourceOpportunity != null)
                    {
                        Camp.CampOpportunityGenerator.Instance?.RecordEngagement(
                            opp.OpportunityId, opp.SourceOpportunity.Type);
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("ORCHESTRATOR", $"Failed to fire opportunity {opp.OpportunityId}", ex);
                }
            }

            if (toFire.Count > 0)
            {
                ModLogger.Info(LogCategory, $"Phase {phase}: Fired {toFire.Count} committed opportunities");
            }
        }

        /// <summary>
        /// Main daily tick. Runs once per in-game day.
        /// Orchestrator provides activity levels to OrderProgressionBehavior and generates forecasts.
        /// Does NOT fire automatic events - content delivery happens through orders and player decisions.
        /// </summary>
        private void OnDailyTick()
        {
            if (!IsActive())
            {
                return;
            }

            // Analyze world situation
            var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
            var activityLevel = worldSituation.ExpectedActivity;

            ModLogger.Debug(LogCategory,
                $"World State: Lord={worldSituation.LordIs}, Phase={worldSituation.CurrentPhase}, Activity={activityLevel}");

            // Check if orchestrator is enabled
            var config = Mod.Core.Config.ConfigurationManager.LoadOrchestratorConfig();
            if (config?.Enabled == true)
            {
                // Orchestrator is active - manage world state and forecasts silently
                // Activity level is provided to OrderProgressionBehavior via GetCurrentWorldSituation()
                // OrderProgressionBehavior uses it to modify order event slot probabilities

                // Set quiet day based on world state
                SetQuietDayFromWorldState(worldSituation);

                // Generate forecasts for UI (Main Menu NOW and AHEAD sections)
                // This ensures forecast data is fresh for when player opens menu
                GenerateForecastData(worldSituation);

                // Schedule opportunities for the day (Orchestrator Unification)
                // This pre-schedules opportunities 24 hours ahead so they don't disappear mid-session
                ScheduleOpportunities();

                // Update camp opportunities availability (legacy - can be deprecated after Phase 3)
                RefreshCampOpportunities(worldSituation);

                // Update baggage simulation context (world-state-aware probabilities)
                RefreshBaggageSimulation(worldSituation);

                // Check medical pressure and trigger illness events / opportunities (Phase 6H)
                CheckMedicalPressure(worldSituation);

                // Debug logging (only appears when Debug level is enabled for this category)
                ModLogger.Debug(LogCategory, $"Orchestrator active: Activity={activityLevel}, Phase={worldSituation.CurrentPhase}");

                // TODO Phase 10: Add order planning with 24h/8h/2h warnings
                // PlanNext24Hours(worldSituation);
            }

            // Update behavior tracking data for next save
            _behaviorCounts = PlayerBehaviorTracker.GetBehaviorCountsForSave();
            _contentEngagement = PlayerBehaviorTracker.GetContentEngagementForSave();
        }

        /// <summary>
        /// Tests content selection with fitness scoring and logs comparisons.
        /// Logs what WOULD be selected without affecting the live system.
        /// Debug-level logging only - not shown to end users.
        /// </summary>
        private void TestContentSelection(WorldSituation worldSituation)
        {
            // All diagnostic logging is at Debug level to avoid spamming end-user logs
            if (!ModLogger.IsEnabled(LogCategory, LogLevel.Debug))
            {
                return; // Skip expensive selection tests if Debug logging is disabled
            }

            ModLogger.Debug(LogCategory, "=== Content Selection Test ===");

            // Select with OLD system (no world situation)
            var oldSelection = EventSelector.SelectEvent(null);
            if (oldSelection != null)
            {
                ModLogger.Debug(LogCategory, $"OLD system would select: {oldSelection.Id} (category: {oldSelection.Category})");
            }
            else
            {
                ModLogger.Debug(LogCategory, "OLD system: No eligible events");
            }

            // Select with NEW system (with world situation and fitness scoring)
            var newSelection = EventSelector.SelectEvent(worldSituation);
            if (newSelection != null)
            {
                ModLogger.Debug(LogCategory, $"NEW system would select: {newSelection.Id} (category: {newSelection.Category})");

                // Log fitness reasoning
                var playerPrefs = PlayerBehaviorTracker.GetPreferences();
                ModLogger.Debug(LogCategory,
                    $"Player preferences: Combat={playerPrefs.CombatVsSocial:F2}, Risky={playerPrefs.RiskyVsSafe:F2}, Loyal={playerPrefs.LoyalVsSelfServing:F2} (from {playerPrefs.TotalChoicesMade} choices)");
            }
            else
            {
                ModLogger.Debug(LogCategory, "NEW system: No eligible events");
            }

            // Compare selections
            if (oldSelection != null && newSelection != null)
            {
                if (oldSelection.Id == newSelection.Id)
                {
                    ModLogger.Debug(LogCategory, "✓ Both systems selected same event");
                }
                else
                {
                    ModLogger.Debug(LogCategory, $"✗ Systems differ: OLD={oldSelection.Id}, NEW={newSelection.Id}");
                    ModLogger.Debug(LogCategory, $"Difference reason: Fitness scoring adjusted weights based on activity={worldSituation.ExpectedActivity}");
                }
            }

            ModLogger.Debug(LogCategory, "=== End Selection Test ===");
        }

        /// <summary>
        /// Generates forecast data for UI display.
        /// Forecasts include player status (duty, health), upcoming events, and company state.
        /// ForecastGenerator.BuildPlayerStatus() is called by UI on-demand using this data.
        /// </summary>
        private void GenerateForecastData(WorldSituation worldSituation)
        {
            // Forecast data is cached in various systems for UI consumption:
            // - OrderManager tracks current/upcoming orders
            // - CompanySimulationBehavior tracks needs/pressure
            // - EscalationManager tracks tension levels
            // - CampOpportunityGenerator tracks commitments
            
            // The daily tick ensures all these systems have current data
            // UI calls ForecastGenerator.BuildPlayerStatus() which reads from these systems
            
            ModLogger.Debug(LogCategory, "Forecast data ready for UI");
        }

        /// <summary>
        /// Refreshes available camp opportunities based on world state.
        /// CampOpportunityGenerator filters opportunities by phase, context, and eligibility.
        /// </summary>
        private void RefreshCampOpportunities(WorldSituation worldSituation)
        {
            // Camp opportunities refresh automatically on phase changes via OnPhaseChanged()
            // This daily tick can trigger additional logic for opportunity planning
            
            var opportunityGen = Camp.CampOpportunityGenerator.Instance;
            if (opportunityGen != null)
            {
                // Opportunities are pre-scheduled by the Orchestrator at daily tick
                // CampOpportunityGenerator provides candidate generation logic
                ModLogger.Debug(LogCategory, "Camp opportunity system active");
            }
        }

        /// <summary>
        /// Updates baggage simulation with world-state-aware probabilities.
        /// Makes baggage events (delays, raids, arrivals) responsive to campaign situation.
        /// </summary>
        private void RefreshBaggageSimulation(WorldSituation worldSituation)
        {
            var baggageManager = Logistics.BaggageTrainManager.Instance;
            if (baggageManager == null)
            {
                return;
            }

            try
            {
                // Calculate current probabilities for diagnostics
                var probs = baggageManager.CalculateEventProbabilities(worldSituation);
                
                // The probabilities will be used automatically when BaggageTrainManager
                // checks for events - no need to pass them explicitly
                
                ModLogger.Debug(LogCategory, 
                    $"Baggage simulation updated: CatchUp={probs.CaughtUpChance}%, " +
                    $"Delay={probs.DelayChance}%, Raid={probs.RaidChance}% " +
                    $"(Activity={worldSituation.ExpectedActivity})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ORCHESTRATOR", "Error refreshing baggage simulation", ex);
            }
        }

        /// <summary>
        /// Determines realistic event frequency based on world situation and pressure.
        /// Used for diagnostic analysis when orchestrator is disabled.
        /// </summary>
        private float DetermineRealisticFrequency(WorldSituation situation, SimulationPressure pressure)
        {
            // Start with base frequency from world analysis
            var baseFrequency = situation.RealisticEventFrequency;

            // Apply pressure modifier
            var pressureModifier = pressure.GetFrequencyModifier();

            return baseFrequency * pressureModifier;
        }

        /// <summary>
        /// Checks if the orchestrator should be active.
        /// </summary>
        private bool IsActive()
        {
            return EnlistmentBehavior.Instance?.IsEnlisted == true;
        }

        /// <summary>
        /// Sets quiet day flag based on world state.
        /// Garrison = potentially quiet, Battle = always quiet, Campaign/Siege = never quiet.
        /// </summary>
        private void SetQuietDayFromWorldState(WorldSituation situation)
        {
            bool isQuiet = false;

            // Defeated/Captured = always quiet (recovery/imprisonment)
            if (situation.LordIs == LordSituation.Defeated || situation.LordIs == LordSituation.Captured)
            {
                isQuiet = true;
            }
            // Peacetime garrison = potentially quiet (low activity)
            else if (situation.LordIs == LordSituation.PeacetimeGarrison)
            {
                // Use a small chance for quiet days in garrison (10%)
                var roll = TaleWorlds.Core.MBRandom.RandomFloat;
                isQuiet = roll < 0.10f;
            }
            // Campaign/Siege/War = never quiet (high activity)
            else
            {
                isQuiet = false;
            }

            GlobalEventPacer.SetQuietDay(isQuiet);

            if (isQuiet)
            {
                ModLogger.Debug(LogCategory, $"Set quiet day: true (situation: {situation.LordIs})");
            }
        }

        /// <summary>
        /// Checks if OrderManager can issue a new order now based on world state timing.
        /// Coordinates order issuance frequency with activity level.
        /// </summary>
        public bool CanIssueOrderNow()
        {
            if (!IsActive())
            {
                return true; // Orchestrator disabled, allow normal order flow
            }

            var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
            var activityLevel = worldSituation.ExpectedActivity;

            // Determine minimum days between orders based on activity level
            float minDaysBetweenOrders = activityLevel switch
            {
                ActivityLevel.Quiet => 3.0f,      // Garrison: 3-5 days
                ActivityLevel.Routine => 2.0f,    // Peacetime: 2-3 days
                ActivityLevel.Active => 1.0f,     // Campaign: 1-2 days
                ActivityLevel.Intense => 0.5f,    // Siege: 0.5-1 day
                _ => 2.0f
            };

            // Check if enough time has passed (OrderManager tracks _lastOrderTime)
            // For now, always return true to allow OrderManager's existing timing logic
            // TODO: OrderManager will coordinate with this method when order integration is added
            ModLogger.Debug(LogCategory,
                $"Order issuance check: Activity={activityLevel}, MinDays={minDaysBetweenOrders}");

            return true;
        }

        /// <summary>
        /// Exposes current world situation for OrderProgressionBehavior event weighting.
        /// Used to modify slot event chances based on activity level.
        /// </summary>
        public WorldSituation GetCurrentWorldSituation()
        {
            return WorldStateAnalyzer.AnalyzeSituation();
        }

        /// <summary>
        /// Queues a crisis event for high-priority delivery.
        /// Called by CompanySimulationBehavior when pressure thresholds are exceeded.
        /// Crisis events bypass normal frequency limits and fire at the next opportunity.
        /// </summary>
        /// <param name="eventId">Event ID to queue (e.g., "evt_supply_crisis", "evt_morale_collapse").</param>
        public void QueueCrisisEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            ModLogger.Warn(LogCategory, $"Crisis event queued: {eventId}");

            // Load the event definition
            var eventDef = EventCatalog.GetEvent(eventId);
            if (eventDef == null)
            {
                ModLogger.Warn(LogCategory, $"Crisis event '{eventId}' not found in definitions - creating placeholder");

                // Crisis events should exist, but if not, log for content authors to add them
                // The simulation should still function even if specific crisis events aren't defined yet
                return;
            }

            // Route via StoryDirector; fall back to direct queue if director not yet registered
            var director = StoryDirector.Instance;
            if (director != null)
            {
                director.EmitCandidate(new StoryCandidate
                {
                    SourceId = "content.crisis." + eventId,
                    CategoryId = "company.crisis." + eventId,
                    ProposedTier = StoryTier.Modal,
                    SeverityHint = 0.80f,
                    Beats = { StoryBeat.CompanyCrisisThreshold },
                    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                    EmittedAt = CampaignTime.Now,
                    InteractiveEvent = eventDef,
                    RenderedTitle = eventDef.TitleFallback,
                    RenderedBody = eventDef.SetupFallback,
                    StoryKey = eventDef.Id
                });
                ModLogger.Info(LogCategory, $"Crisis event queued for delivery: {eventId}");
            }
            else
            {
                EventDeliveryManager.Instance?.QueueEvent(eventDef);
                ModLogger.Warn(LogCategory, "EventDeliveryManager not available for crisis event delivery");
            }
        }

        #region Medical Pressure System

        /// <summary>
        /// Checks medical pressure and triggers appropriate responses.
        /// Called from OnDailyTick when orchestrator is active.
        /// Triggers illness onset events based on medical risk levels and queues medical opportunities.
        /// </summary>
        private void CheckMedicalPressure(WorldSituation worldSituation)
        {
            var currentDay = (int)CampaignTime.Now.ToDays;

            // Only check once per day
            if (currentDay == _lastMedicalCheckDay)
            {
                return;
            }
            _lastMedicalCheckDay = currentDay;
            _medicalOpportunityQueuedToday = false;

            // Get medical pressure analysis
            var (medicalAnalysis, pressureLevel) = SimulationPressureCalculator.GetMedicalPressure();

            // Track consecutive high pressure days for escalation
            if (pressureLevel >= MedicalPressureLevel.High)
            {
                _consecutiveHighMedicalPressureDays++;
            }
            else
            {
                _consecutiveHighMedicalPressureDays = 0;
            }

            // Log current medical state for diagnostics
            ModLogger.Debug(LogCategory,
                $"Medical pressure check: Level={pressureLevel}, MedRisk={medicalAnalysis.MedicalRisk}, " +
                $"HasCondition={medicalAnalysis.HasCondition}, Untreated={medicalAnalysis.IsUntreated}, " +
                $"ConsecHighDays={_consecutiveHighMedicalPressureDays}");

            // Queue medical opportunities based on pressure level
            QueueMedicalOpportunities(medicalAnalysis, pressureLevel);

            // Check for illness onset event triggers
            CheckIllnessOnsetTriggers(medicalAnalysis, pressureLevel);
        }

        /// <summary>
        /// Queues appropriate medical opportunities based on current pressure.
        /// Higher pressure = higher priority opportunities appear in camp menu.
        /// </summary>
        private void QueueMedicalOpportunities(MedicalPressureAnalysis pressure, MedicalPressureLevel level)
        {
            var opportunityGen = Camp.CampOpportunityGenerator.Instance;
            if (opportunityGen == null)
            {
                return;
            }

            // Critical: Force emergency opportunity (once per critical episode)
            if (level == MedicalPressureLevel.Critical && !_emergencyOpportunityForced)
            {
                ModLogger.Info(LogCategory, "Critical medical pressure - emergency opportunity forced");
                opportunityGen.QueueMedicalOpportunity("opp_urgent_medical");
                _emergencyOpportunityForced = true;
                _medicalOpportunityQueuedToday = true;
                return;
            }

            // Reset flag when no longer critical
            if (level < MedicalPressureLevel.Critical)
            {
                _emergencyOpportunityForced = false;
            }

            // Don't queue multiple opportunities per day
            if (_medicalOpportunityQueuedToday)
            {
                return;
            }

            // Only queue opportunities if player has condition or high medical risk
            if (!pressure.HasCondition && pressure.MedicalRisk < 3)
            {
                return;
            }

            // Has untreated condition: queue standard medical care opportunity
            if (pressure.HasCondition && pressure.IsUntreated)
            {
                ModLogger.Debug(LogCategory, "Untreated condition - medical care opportunity queued");
                opportunityGen.QueueMedicalOpportunity("opp_seek_medical_care");
                _medicalOpportunityQueuedToday = true;
                return;
            }

            // High medical risk but no condition yet: queue preventive rest
            if (level >= MedicalPressureLevel.Moderate && !pressure.HasCondition)
            {
                ModLogger.Debug(LogCategory, "High medical risk - preventive rest opportunity queued");
                opportunityGen.QueueMedicalOpportunity("opp_preventive_rest");
                _medicalOpportunityQueuedToday = true;
            }
        }

        /// <summary>
        /// Checks if medical risk warrants triggering an illness onset event.
        /// Events are triggered through EventDeliveryManager for popup delivery.
        /// Includes 7-day cooldown and sophisticated probability calculation.
        /// </summary>
        private void CheckIllnessOnsetTriggers(MedicalPressureAnalysis pressure, MedicalPressureLevel level)
        {
            // Don't trigger illness onset if player already has condition
            if (pressure.HasCondition)
            {
                return;
            }

            // Only check if Medical Risk >= 3
            if (pressure.MedicalRisk < 3)
            {
                return;
            }

            // 7-day cooldown between illness triggers
            var currentDay = (int)CampaignTime.Now.ToDays;
            if (currentDay - _lastIllnessOnsetDay < 7)
            {
                return;
            }

            // Calculate probability with modifiers
            var baseChance = pressure.MedicalRisk * 0.05f; // 5% per risk level (15-25% base)
            
            // Season modifier - winter increases illness risk
            var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
            // TODO: Add season detection when WorldStateAnalyzer has GetSeason()
            // For now, skip season modifier
            
            // Siege modifier - cramped conditions increase illness
            if (worldSituation.LordIs == LordSituation.SiegeAttacking || 
                worldSituation.LordIs == LordSituation.SiegeDefending)
            {
                baseChance += 0.12f; // +12% during siege
            }

            // Consecutive high pressure day modifier
            baseChance += _consecutiveHighMedicalPressureDays * 0.05f; // +5% per consecutive day

            // Cap probability at 50%
            baseChance = Math.Min(baseChance, 0.50f);

            ModLogger.Debug(LogCategory, 
                $"Illness onset chance: {baseChance * 100:F1}% (Risk={pressure.MedicalRisk}, ConsecDays={_consecutiveHighMedicalPressureDays})");

            // Roll for illness onset
            if (TaleWorlds.Core.MBRandom.RandomFloat >= baseChance)
            {
                return; // No illness today
            }

            // Determine context for maritime vs land illness events
            var party = EnlistmentBehavior.Instance?.CurrentLord?.PartyBelongedTo;
            // BUGFIX: If party is in a settlement or besieging, they are on land regardless of IsCurrentlyAtSea
            var isAtSea = party != null && 
                          party.CurrentSettlement == null && 
                          party.BesiegedSettlement == null && 
                          party.IsCurrentlyAtSea;
            var contextSuffix = isAtSea ? "_sea" : "";

            // Select event based on medical risk severity and context
            string baseEventId;
            if (pressure.MedicalRisk >= 5)
            {
                baseEventId = "illness_onset_severe";
            }
            else if (pressure.MedicalRisk >= 4)
            {
                baseEventId = "illness_onset_moderate";
            }
            else
            {
                baseEventId = "illness_onset_minor";
            }

            // Try context-specific variant first, then fall back to base event
            var eventToQueue = baseEventId + contextSuffix;
            var eventDef = EventCatalog.GetEvent(eventToQueue);
            if (eventDef == null && !string.IsNullOrEmpty(contextSuffix))
            {
                // Fall back to base event if sea variant doesn't exist
                eventToQueue = baseEventId;
                eventDef = EventCatalog.GetEvent(eventToQueue);
                ModLogger.Debug(LogCategory, $"Sea variant not found, using base event: {eventToQueue}");
            }

            // Queue the illness onset event
            if (eventDef == null)
            {
                ModLogger.Warn(LogCategory, $"Illness onset event '{eventToQueue}' not found in catalog");
                return;
            }

            var director = StoryDirector.Instance;
            if (director != null)
            {
                director.EmitCandidate(new StoryCandidate
                {
                    SourceId = "content.illness_onset." + eventToQueue,
                    CategoryId = "company.illness",
                    ProposedTier = StoryTier.Modal,
                    SeverityHint = 0.70f,
                    Beats = { StoryBeat.EscalationThreshold },
                    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                    EmittedAt = CampaignTime.Now,
                    InteractiveEvent = eventDef,
                    RenderedTitle = eventDef.TitleFallback,
                    RenderedBody = eventDef.SetupFallback,
                    StoryKey = eventDef.Id
                });
                ModLogger.Info(LogCategory,
                    $"Illness onset event queued: {eventToQueue} (MedRisk={pressure.MedicalRisk}, chance={baseChance * 100:F1}%)");
            }
            else
            {
                EventDeliveryManager.Instance?.QueueEvent(eventDef);
            }
            _lastIllnessOnsetDay = currentDay;
        }

        #endregion

        #region Opportunity Scheduling Methods

        /// <summary>
        /// Called on daily tick. Schedules opportunities for next 24 hours.
        /// Opportunities are locked once scheduled and won't disappear when context changes.
        /// Implements cross-day scheduling: when late in day, also schedules tomorrow's early phases.
        /// </summary>
        private void ScheduleOpportunities()
        {
            // Edge case: Don't schedule if not enlisted
            if (EnlistmentBehavior.Instance?.IsEnlisted != true)
            {
                _scheduledOpportunities = null;
                _scheduledTomorrowOpportunities = null;
                ModLogger.Debug(LogCategory, "Opportunity scheduling skipped: not enlisted");
                return;
            }

            var currentDay = (int)CampaignTime.Now.ToDays;

            // Only schedule once per day
            if (_scheduledDay == currentDay)
            {
                return;
            }

            ModLogger.Info(LogCategory, "═══ Daily Opportunity Schedule ═══");

            // Day transition: move yesterday's "tomorrow" to today's schedule
            // This ensures opportunities pre-scheduled for tomorrow become available when tomorrow arrives
            if (_scheduledTomorrowOpportunities != null && _scheduledTomorrowOpportunities.Count > 0)
            {
                _scheduledOpportunities = _scheduledTomorrowOpportunities;
                ModLogger.Info(LogCategory, "  (Promoted tomorrow's pre-scheduled opportunities to today)");
            }
            else
            {
                _scheduledOpportunities = new Dictionary<DayPhase, List<ScheduledOpportunity>>();
            }
            
            _scheduledTomorrowOpportunities = new Dictionary<DayPhase, List<ScheduledOpportunity>>();
            _scheduledDay = currentDay;

            // Get current world situation for prediction
            var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
            ModLogger.Info(LogCategory, 
                $"Context: {worldSituation.LordIs}, Activity={worldSituation.ExpectedActivity}, Phase={worldSituation.CurrentDayPhase}");

            // Track ALL scheduled opportunities across phases to prevent duplicates
            // Each opportunity should only appear ONCE per day (in one phase)
            var alreadyScheduledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Include IDs from promoted tomorrow schedule in already-scheduled set
            foreach (var kvp in _scheduledOpportunities)
            {
                foreach (var opp in kvp.Value)
                {
                    alreadyScheduledIds.Add(opp.OpportunityId);
                }
            }

            // Get current phase to determine which phases have already passed
            // This prevents past-phase opportunities from appearing when schedule is generated mid-day
            var currentPhase = worldSituation.CurrentDayPhase;
            
            // Schedule for each phase (today)
            int totalScheduled = 0;
            int totalGuaranteed = 0;
            int skippedPastPhase = 0;
            foreach (DayPhase phase in Enum.GetValues(typeof(DayPhase)))
            {
                // Skip phases that were already promoted from yesterday's "tomorrow" schedule
                if (_scheduledOpportunities.ContainsKey(phase) && _scheduledOpportunities[phase].Count > 0)
                {
                    var promotedCount = _scheduledOpportunities[phase].Count(o => !o.Consumed);
                    if (promotedCount > 0)
                    {
                        totalScheduled += promotedCount;
                        var oppList = string.Join(", ", _scheduledOpportunities[phase].Where(o => !o.Consumed).Select(o => o.OpportunityId));
                        ModLogger.Info(LogCategory, $"  {phase}: {promotedCount} opportunities (promoted) → [{oppList}]");
                        continue;
                    }
                }
                
                var scheduled = SchedulePhaseOpportunities(phase, worldSituation, alreadyScheduledIds);
                
                // If this phase has already passed today, mark all opportunities as consumed immediately
                // This handles mid-day schedule regeneration (e.g., after save/load)
                // Phase order: Dawn(0) -> Midday(1) -> Dusk(2) -> Night(3)
                bool isPastPhase = (int)phase < (int)currentPhase;
                if (isPastPhase && scheduled.Count > 0)
                {
                    foreach (var opp in scheduled)
                    {
                        opp.Consumed = true;
                    }
                    skippedPastPhase += scheduled.Count;
                    ModLogger.Debug(LogCategory, 
                        $"  {phase}: {scheduled.Count} opportunities marked consumed (phase already passed)");
                }
                
                _scheduledOpportunities[phase] = scheduled;

                if (scheduled.Count > 0 && !isPastPhase)
                {
                    var guaranteed = scheduled.Count(s => s.SourceOpportunity?.Immediate == true);
                    totalScheduled += scheduled.Count;
                    totalGuaranteed += guaranteed;

                    var oppList = string.Join(", ", scheduled.Select(s => 
                        s.SourceOpportunity?.Immediate == true ? $"{s.OpportunityId}*" : s.OpportunityId));
                    
                    ModLogger.Info(LogCategory, 
                        $"  {phase}: {scheduled.Count} opportunities ({guaranteed} guaranteed) → [{oppList}]");
                }
                else if (!isPastPhase)
                {
                    ModLogger.Debug(LogCategory, $"  {phase}: 0 opportunities (budget=0 or no candidates)");
                }
            }
            
            if (skippedPastPhase > 0)
            {
                ModLogger.Info(LogCategory, 
                    $"  (Skipped {skippedPastPhase} past-phase opportunities - schedule generated at {currentPhase})");
            }

            // Cross-day scheduling: When late in the day (Dusk or Night), also schedule tomorrow's 
            // earlier phases to maintain a rolling 24-hour visibility window per design spec.
            int tomorrowScheduled = ScheduleTomorrowOpportunities(currentPhase, worldSituation, alreadyScheduledIds);

            ModLogger.Info(LogCategory, 
                $"Schedule complete: {totalScheduled} today + {tomorrowScheduled} tomorrow ({totalGuaranteed} guaranteed)");
            ModLogger.Info(LogCategory, "══════════════════════════════════");
        }

        /// <summary>
        /// Schedules tomorrow's early phases when it's late in the day.
        /// This maintains a rolling 24-hour visibility window as per design spec.
        /// </summary>
        /// <param name="currentPhase">Current day phase.</param>
        /// <param name="worldSituation">Current world state for budget calculation.</param>
        /// <param name="alreadyScheduledIds">IDs to exclude (prevents duplicates).</param>
        /// <returns>Number of opportunities scheduled for tomorrow.</returns>
        private int ScheduleTomorrowOpportunities(DayPhase currentPhase, WorldSituation worldSituation, HashSet<string> alreadyScheduledIds)
        {
            // Only schedule tomorrow if we're late in the day
            // Dusk (2): schedule tomorrow's Dawn, Midday
            // Night (3): schedule tomorrow's Dawn, Midday, Dusk
            if ((int)currentPhase < (int)DayPhase.Dusk)
            {
                ModLogger.Debug(LogCategory, $"  (Skipping tomorrow scheduling - too early in day, currentPhase={currentPhase})");
                return 0; // Early in day, no need for tomorrow yet
            }

            var phasesToSchedule = new List<DayPhase>();
            switch (currentPhase)
            {
                case DayPhase.Dusk:
                    phasesToSchedule.Add(DayPhase.Dawn);
                    phasesToSchedule.Add(DayPhase.Midday);
                    break;
                case DayPhase.Night:
                    phasesToSchedule.Add(DayPhase.Dawn);
                    phasesToSchedule.Add(DayPhase.Midday);
                    phasesToSchedule.Add(DayPhase.Dusk);
                    break;
            }

            if (phasesToSchedule.Count == 0)
            {
                return 0;
            }

            ModLogger.Info(LogCategory, $"  --- Tomorrow's Forecast ({string.Join(", ", phasesToSchedule)}) ---");

            int tomorrowScheduled = 0;
            foreach (var phase in phasesToSchedule)
            {
                var scheduled = SchedulePhaseOpportunities(phase, worldSituation, alreadyScheduledIds);
                _scheduledTomorrowOpportunities[phase] = scheduled;

                if (scheduled.Count > 0)
                {
                    tomorrowScheduled += scheduled.Count;
                    var oppList = string.Join(", ", scheduled.Select(o => $"{o.OpportunityId}"));
                    ModLogger.Info(LogCategory, $"  Tomorrow {phase}: {scheduled.Count} opportunities → [{oppList}]");
                }
                else
                {
                    ModLogger.Debug(LogCategory, $"  Tomorrow {phase}: 0 opportunities (budget=0 or no candidates)");
                }
            }

            if (tomorrowScheduled == 0)
            {
                ModLogger.Warn(LogCategory, 
                    $"[W-ORCH-005] Tomorrow scheduling returned 0 opportunities at {currentPhase} - check budget/candidates");
            }

            return tomorrowScheduled;
        }

        /// <summary>
        /// Schedules opportunities for a specific phase using the CampOpportunityGenerator.
        /// </summary>
        /// <param name="phase">The day phase to schedule for.</param>
        /// <param name="worldSituation">Current world state for budget calculation.</param>
        /// <param name="alreadyScheduledIds">IDs of opportunities already scheduled for other phases (to prevent duplicates).</param>
        private List<ScheduledOpportunity> SchedulePhaseOpportunities(
            DayPhase phase,
            WorldSituation worldSituation,
            HashSet<string> alreadyScheduledIds)
        {
            var generator = CampOpportunityGenerator.Instance;
            if (generator == null)
            {
                ModLogger.Expected(LogCategory, "orchestrator_no_generator", $"SchedulePhaseOpportunities({phase}): CampOpportunityGenerator not available - decisions won't appear");
                return new List<ScheduledOpportunity>();
            }

            // Generate candidates using existing generator logic
            var candidates = GenerateCandidatesForPhase(phase, generator);

            // Filter out any opportunities already scheduled for an earlier phase
            // This prevents the same opportunity (like "War Stories") from appearing in Dusk AND Night
            var availableCandidates = candidates
                .Where(c => !alreadyScheduledIds.Contains(c.Id))
                .ToList();

            ModLogger.Debug(LogCategory, 
                $"  {phase} candidates: {availableCandidates.Count} available ({candidates.Count - availableCandidates.Count} already scheduled)");

            // Get budget for this phase based on world situation
            int budget = DetermineOpportunityBudget(worldSituation, phase);

            // Select top N candidates by fitness score (within budget)
            var selected = new List<ScheduledOpportunity>();

            if (budget > 0 && availableCandidates.Count > 0)
            {
                var topCandidates = availableCandidates
                    .OrderByDescending(c => c.FitnessScore)
                    .Take(budget)
                    .ToList();

                ModLogger.Debug(LogCategory, 
                    $"  {phase} fitness selection (budget={budget}): " +
                    $"{string.Join(", ", topCandidates.Select(c => $"{c.Id}({c.FitnessScore:F0})"))}");

                foreach (var c in topCandidates)
                {
                    selected.Add(new ScheduledOpportunity
                    {
                        OpportunityId = c.Id,
                        TargetDecisionId = c.TargetDecisionId,
                        Phase = phase,
                        DisplayName = c.GetTitle(),
                        NarrativeHint = c.GetHint(),
                        FitnessScore = c.FitnessScore,
                        SourceOpportunity = c,
                        Consumed = false
                    });
                    
                    // Track this opportunity to prevent duplicates in later phases
                    alreadyScheduledIds.Add(c.Id);
                }
            }
            else if (budget == 0)
            {
                ModLogger.Debug(LogCategory, $"  {phase} budget=0 (no opportunities)");
            }

            return selected;
        }

        /// <summary>
        /// Generates candidate opportunities for a specific phase.
        /// Uses CampOpportunityGenerator.GenerateCandidatesForPhase() which properly filters for the target phase.
        /// </summary>
        private List<CampOpportunity> GenerateCandidatesForPhase(DayPhase phase, CampOpportunityGenerator generator)
        {
            // Use the generator's phase-specific method which generates candidates AS IF it were the target phase
            // This is critical: GenerateCampLife() uses the CURRENT phase, but we need candidates for ALL phases
            return generator.GenerateCandidatesForPhase(phase);
        }

        /// <summary>
        /// Checks if an opportunity is valid for a specific phase.
        /// </summary>
        private bool IsOpportunityValidForPhase(CampOpportunity opportunity, DayPhase phase)
        {
            if (opportunity.ValidPhases == null || opportunity.ValidPhases.Count == 0)
            {
                return true; // No phase restriction
            }

            return opportunity.ValidPhases.Contains(phase.ToString());
        }

        /// <summary>
        /// Determines opportunity budget for a phase based on world situation.
        /// Mirrors the logic in CampOpportunityGenerator.DetermineOpportunityBudget.
        /// </summary>
        private int DetermineOpportunityBudget(WorldSituation world, DayPhase phase)
        {
            int budget;

            // Base budget by lord situation and day phase
            budget = (world.LordIs, phase) switch
            {
                // Garrison: high activity, especially mornings and evenings
                (LordSituation.PeacetimeGarrison, DayPhase.Dawn) => 3,
                (LordSituation.PeacetimeGarrison, DayPhase.Midday) => 2,
                (LordSituation.PeacetimeGarrison, DayPhase.Dusk) => 3,
                (LordSituation.PeacetimeGarrison, DayPhase.Night) => 1,

                // Siege: very limited opportunities
                (LordSituation.SiegeAttacking, _) => 1,
                (LordSituation.SiegeDefending, _) => 0,

                // Campaign: always at least 1 opportunity per phase (lords rarely stop moving)
                (LordSituation.WarMarching, DayPhase.Dawn) => 1,
                (LordSituation.WarMarching, DayPhase.Midday) => 1,
                (LordSituation.WarMarching, DayPhase.Dusk) => 2,
                (LordSituation.WarMarching, DayPhase.Night) => 1,

                (LordSituation.WarActiveCampaign, DayPhase.Dusk) => 2,
                (LordSituation.WarActiveCampaign, _) => 1,

                // Defeated/Captured: recovery time
                (LordSituation.Defeated, _) => 1,
                (LordSituation.Captured, _) => 0,

                // Default
                _ => 2
            };

            // Apply modifiers (mirroring CampOpportunityGenerator logic)
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment != null)
            {
                // Probation reduces opportunities
                if (enlistment.IsOnProbation)
                {
                    budget = Math.Max(0, budget - 1);
                }

                // Low supplies reduce opportunities
                var supplies = enlistment.CompanyNeeds?.Supplies ?? 50;
                if (supplies < 30)
                {
                    budget = Math.Max(0, budget - 1);
                }
                if (supplies < 20)
                {
                    budget = 1; // Survival mode
                }
            }

            return Math.Min(budget, MaxOpportunitiesPerPhase);
        }

        /// <summary>
        /// Gets opportunities for the current phase. Returns locked list, no regeneration.
        /// This is the main method the menu should call to get stable opportunities.
        /// </summary>
        public IReadOnlyList<ScheduledOpportunity> GetCurrentPhaseOpportunities()
        {
            // Edge case: Block opportunities during active muster sequence
            // Muster menu takes over, opportunities would be confusing
            var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "";
            if (currentMenu.StartsWith("enlisted_muster_"))
            {
                ModLogger.Debug(LogCategory, "GetCurrentPhaseOpportunities: Blocked (muster sequence active)");
                return new List<ScheduledOpportunity>();
            }

            // Edge case: Block opportunities during new enlistment grace period (first 3 days)
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment != null && enlistment.DaysServed < 3)
            {
                ModLogger.Debug(LogCategory, 
                    $"GetCurrentPhaseOpportunities: Blocked (grace period, day {enlistment.DaysServed}/3)");
                return new List<ScheduledOpportunity>();
            }

            // Ensure opportunities are scheduled for today
            ScheduleOpportunities();

            var currentPhase = WorldStateAnalyzer.GetCurrentDayPhase();
            
            // Diagnostic: log what phases we have scheduled
            if (_scheduledOpportunities != null)
            {
                var phaseInfo = string.Join(", ", _scheduledOpportunities.Select(kvp => 
                    $"{kvp.Key}:{kvp.Value.Count(o => !o.Consumed)}"));
                ModLogger.Info(LogCategory, 
                    $"GetCurrentPhaseOpportunities: Looking for {currentPhase}, available phases: [{phaseInfo}]");
            }

            if (_scheduledOpportunities == null ||
                !_scheduledOpportunities.TryGetValue(currentPhase, out var opportunities))
            {
                ModLogger.Expected(LogCategory, "orchestrator_no_schedule",
                    $"GetCurrentPhaseOpportunities: No schedule exists for {currentPhase} " +
                    $"(_scheduledOpportunities={_scheduledOpportunities != null}, _scheduledDay={_scheduledDay})");
                return new List<ScheduledOpportunity>();
            }

            // Return only non-consumed opportunities
            var available = opportunities.Where(o => !o.Consumed).ToList();
            var consumed = opportunities.Count - available.Count;

            if (consumed > 0)
            {
                ModLogger.Debug(LogCategory, 
                    $"GetCurrentPhaseOpportunities: {available.Count} available for {currentPhase} ({consumed} consumed)");
            }
            else if (available.Count > 0)
            {
                ModLogger.Debug(LogCategory, 
                    $"GetCurrentPhaseOpportunities: {available.Count} available for {currentPhase}");
            }

            return available;
        }

        /// <summary>
        /// Gets all scheduled opportunities for a specific phase (including consumed ones).
        /// Useful for debugging and schedule display.
        /// </summary>
        public IReadOnlyList<ScheduledOpportunity> GetScheduledOpportunitiesForPhase(DayPhase phase)
        {
            if (_scheduledOpportunities == null ||
                !_scheduledOpportunities.TryGetValue(phase, out var opportunities))
            {
                return new List<ScheduledOpportunity>();
            }

            return opportunities;
        }

        /// <summary>
        /// Marks an opportunity as consumed (player interacted with it).
        /// Consumes ALL instances across all phases to ensure it disappears completely.
        /// </summary>
        public void ConsumeOpportunity(string opportunityId)
        {
            if (string.IsNullOrEmpty(opportunityId))
            {
                ModLogger.Expected(LogCategory, "orchestrator_consume_null_id", "ConsumeOpportunity called with null/empty ID - decision may not disappear from menu");
                return;
            }

            if (_scheduledOpportunities == null)
            {
                ModLogger.Debug(LogCategory, $"ConsumeOpportunity({opportunityId}): No schedule exists");
                return;
            }

            bool found = false;
            bool engagementRecorded = false;

            // Consume ALL instances across all phases (safety net in case duplicates exist)
            foreach (var phaseEntry in _scheduledOpportunities)
            {
                var opp = phaseEntry.Value.FirstOrDefault(o => o.OpportunityId == opportunityId);
                if (opp != null && !opp.Consumed)
                {
                    opp.Consumed = true;
                    found = true;
                    ModLogger.Info(LogCategory,
                        $"✓ Opportunity consumed: {opportunityId} (phase={phaseEntry.Key}, fitness={opp.FitnessScore:F1})");

                    // Record engagement only once (first instance)
                    if (!engagementRecorded)
                    {
                        var generator = CampOpportunityGenerator.Instance;
                        if (generator != null && opp.SourceOpportunity != null)
                        {
                            generator.RecordEngagement(opportunityId, opp.SourceOpportunity.Type);
                            ModLogger.Debug(LogCategory,
                                $"  Recorded engagement for learning system (type={opp.SourceOpportunity.Type})");
                            engagementRecorded = true;
                        }
                    }
                }
            }

            // Also check tomorrow's schedule (24-hour rolling window)
            if (_scheduledTomorrowOpportunities != null)
            {
                foreach (var phaseEntry in _scheduledTomorrowOpportunities)
                {
                    var opp = phaseEntry.Value.FirstOrDefault(o => o.OpportunityId == opportunityId);
                    if (opp != null && !opp.Consumed)
                    {
                        opp.Consumed = true;
                        found = true;
                        ModLogger.Info(LogCategory,
                            $"✓ Opportunity consumed: {opportunityId} (tomorrow {phaseEntry.Key}, fitness={opp.FitnessScore:F1})");
                    }
                }
            }

            if (!found)
            {
                ModLogger.Expected(LogCategory, "orchestrator_opportunity_not_in_schedule",
                    $"ConsumeOpportunity({opportunityId}): Not found in schedule - decision may reappear after phase change");
            }
        }

        /// <summary>
        /// Consumes an opportunity by its target decision ID (fallback when opportunity ID is unknown).
        /// Used when CampOpportunity is null but we still need to mark the scheduled opportunity as consumed.
        /// </summary>
        public void ConsumeOpportunityByDecisionId(string decisionId)
        {
            if (string.IsNullOrEmpty(decisionId))
            {
                return;
            }

            if (_scheduledOpportunities == null)
            {
                ModLogger.Debug(LogCategory, $"ConsumeOpportunityByDecisionId({decisionId}): No schedule exists");
                return;
            }

            // Search today's schedule
            foreach (var phaseEntry in _scheduledOpportunities)
            {
                var opp = phaseEntry.Value.FirstOrDefault(o => 
                    o.TargetDecisionId != null && 
                    o.TargetDecisionId.Equals(decisionId, StringComparison.OrdinalIgnoreCase));
                if (opp != null)
                {
                    opp.Consumed = true;
                    ModLogger.Info(LogCategory, 
                        $"✓ Opportunity consumed by decisionId: {decisionId} (opportunityId={opp.OpportunityId}, phase={phaseEntry.Key})");

                    // Also record in CampOpportunityGenerator for history tracking
                    var generator = CampOpportunityGenerator.Instance;
                    if (generator != null && opp.SourceOpportunity != null)
                    {
                        generator.RecordEngagement(opp.OpportunityId, opp.SourceOpportunity.Type);
                        ModLogger.Debug(LogCategory, 
                            $"  Recorded engagement for learning system (type={opp.SourceOpportunity.Type})");
                    }

                    return;
                }
            }

            // Search tomorrow's schedule (24-hour rolling window)
            if (_scheduledTomorrowOpportunities != null)
            {
                foreach (var phaseEntry in _scheduledTomorrowOpportunities)
                {
                    var opp = phaseEntry.Value.FirstOrDefault(o => 
                        o.TargetDecisionId != null && 
                        o.TargetDecisionId.Equals(decisionId, StringComparison.OrdinalIgnoreCase));
                    if (opp != null)
                    {
                        opp.Consumed = true;
                        ModLogger.Info(LogCategory, 
                            $"✓ Opportunity consumed by decisionId: {decisionId} (tomorrow, opportunityId={opp.OpportunityId}, phase={phaseEntry.Key})");
                        return;
                    }
                }
            }

            // Not found - this is expected for non-opportunity decisions (logistics, etc.)
            ModLogger.Debug(LogCategory, 
                $"ConsumeOpportunityByDecisionId({decisionId}): No matching opportunity in schedule (may be non-opportunity decision)");
        }

        /// <summary>
        /// Player commits to an opportunity (schedules it to fire at its designated phase).
        /// The opportunity will grey out in the menu and fire automatically when its phase arrives.
        /// </summary>
        /// <returns>True if successfully committed, false if not found or already committed/consumed.</returns>
        public bool CommitToOpportunity(string opportunityId)
        {
            if (string.IsNullOrEmpty(opportunityId))
            {
                ModLogger.Warn(LogCategory, "CommitToOpportunity called with null/empty ID");
                return false;
            }

            if (_scheduledOpportunities == null)
            {
                ModLogger.Warn(LogCategory, $"CommitToOpportunity({opportunityId}): No schedule exists");
                return false;
            }

            // Search today's schedule
            foreach (var phaseEntry in _scheduledOpportunities)
            {
                var opp = phaseEntry.Value.FirstOrDefault(o => o.OpportunityId == opportunityId);
                if (opp != null)
                {
                    if (opp.Consumed)
                    {
                        ModLogger.Debug(LogCategory, $"CommitToOpportunity({opportunityId}): Already consumed");
                        return false;
                    }

                    if (opp.PlayerCommitted)
                    {
                        ModLogger.Debug(LogCategory, $"CommitToOpportunity({opportunityId}): Already committed");
                        return false;
                    }

                    opp.PlayerCommitted = true;
                    ModLogger.Info(LogCategory,
                        $"✓ Player committed to: {opportunityId} (will fire at {phaseEntry.Key})");
                    return true;
                }
            }

            // Search tomorrow's schedule (24-hour rolling window)
            if (_scheduledTomorrowOpportunities != null)
            {
                foreach (var phaseEntry in _scheduledTomorrowOpportunities)
                {
                    var opp = phaseEntry.Value.FirstOrDefault(o => o.OpportunityId == opportunityId);
                    if (opp != null)
                    {
                        if (opp.Consumed)
                        {
                            ModLogger.Debug(LogCategory, $"CommitToOpportunity({opportunityId}): Already consumed (tomorrow)");
                            return false;
                        }

                        if (opp.PlayerCommitted)
                        {
                            ModLogger.Debug(LogCategory, $"CommitToOpportunity({opportunityId}): Already committed (tomorrow)");
                            return false;
                        }

                        opp.PlayerCommitted = true;
                        ModLogger.Info(LogCategory,
                            $"✓ Player committed to: {opportunityId} (will fire tomorrow at {phaseEntry.Key})");
                        return true;
                    }
                }
            }

            ModLogger.Warn(LogCategory, $"CommitToOpportunity({opportunityId}): Not found in schedule");
            return false;
        }

        /// <summary>
        /// Gets ALL opportunities scheduled for the next 24 hours (today + tomorrow's early phases).
        /// Used by menu to show upcoming opportunities with their availability state.
        /// Implements rolling 24-hour visibility per design spec.
        /// </summary>
        public List<ScheduledOpportunity> GetAllTodaysOpportunities()
        {
            var allOpps = new List<ScheduledOpportunity>();

            if (_scheduledOpportunities == null)
            {
                return allOpps;
            }

            var currentPhase = WorldStateAnalyzer.GetCurrentDayPhase();

            // Add today's remaining opportunities
            foreach (var phase in Enum.GetValues(typeof(DayPhase)).Cast<DayPhase>())
            {
                if (_scheduledOpportunities.TryGetValue(phase, out var opportunities))
                {
                    foreach (var opp in opportunities.Where(o => !o.Consumed))
                    {
                        allOpps.Add(opp);
                    }
                }
            }

            // Add tomorrow's pre-scheduled opportunities (24-hour rolling window)
            if (_scheduledTomorrowOpportunities != null)
            {
                foreach (var phase in Enum.GetValues(typeof(DayPhase)).Cast<DayPhase>())
                {
                    if (_scheduledTomorrowOpportunities.TryGetValue(phase, out var opportunities))
                    {
                        foreach (var opp in opportunities.Where(o => !o.Consumed))
                        {
                            allOpps.Add(opp);
                        }
                    }
                }
            }

            ModLogger.Debug(LogCategory,
                $"GetAllTodaysOpportunities: {allOpps.Count} opportunities (24h window) " +
                $"(committed={allOpps.Count(o => o.PlayerCommitted)}, available={allOpps.Count(o => o.IsAvailableToCommit)})");

            return allOpps;
        }

        /// <summary>
        /// Fires all committed opportunities for the current phase.
        /// Called when a phase boundary is crossed.
        /// Returns the opportunities that should fire now.
        /// </summary>
        public List<ScheduledOpportunity> GetOpportunitiesToFireNow()
        {
            var toFire = new List<ScheduledOpportunity>();

            if (_scheduledOpportunities == null)
            {
                return toFire;
            }

            var currentPhase = WorldStateAnalyzer.GetCurrentDayPhase();

            if (_scheduledOpportunities.TryGetValue(currentPhase, out var opportunities))
            {
                // Get opportunities that are committed but not yet consumed
                toFire = opportunities.Where(o => o.IsScheduledToFire).ToList();

                if (toFire.Count > 0)
                {
                    ModLogger.Info(LogCategory,
                        $"Phase {currentPhase}: {toFire.Count} committed opportunities ready to fire: " +
                        $"[{string.Join(", ", toFire.Select(o => o.OpportunityId))}]");
                }
            }

            return toFire;
        }

        /// <summary>
        /// Cleans up opportunities that were not committed when their phase passed.
        /// Called after phase transitions.
        /// </summary>
        public void CleanupMissedOpportunities(DayPhase pastPhase)
        {
            if (_scheduledOpportunities == null)
            {
                return;
            }

            if (_scheduledOpportunities.TryGetValue(pastPhase, out var opportunities))
            {
                var missed = opportunities.Where(o => o.IsAvailableToCommit).ToList();
                foreach (var opp in missed)
                {
                    opp.Consumed = true; // Mark as consumed so they don't show anymore
                    ModLogger.Debug(LogCategory, $"Opportunity missed (not committed): {opp.OpportunityId} for {pastPhase}");
                }

                if (missed.Count > 0)
                {
                    ModLogger.Info(LogCategory, $"Phase {pastPhase} passed: {missed.Count} uncommitted opportunities expired");
                }
            }
        }

        /// <summary>
        /// Gets narrative hints for upcoming opportunities (for Company Reports).
        /// Returns hints for current and next phases.
        /// </summary>
        public IEnumerable<string> GetUpcomingHints()
        {
            if (_scheduledOpportunities == null)
            {
                ModLogger.Debug(LogCategory, "GetUpcomingHints: No schedule, falling back to generator");
                
                // Fall back to generator's hints if scheduling hasn't run yet
                var generator = CampOpportunityGenerator.Instance;
                if (generator != null)
                {
                    var hints = generator.GetUpcomingHints().ToList();
                    if (hints.Count > 0)
                    {
                        ModLogger.Debug(LogCategory, $"GetUpcomingHints: {hints.Count} hints from generator fallback");
                    }
                    
                    foreach (var hint in hints)
                    {
                        yield return hint;
                    }
                }
                yield break;
            }

            var currentPhase = WorldStateAnalyzer.GetCurrentDayPhase();

            // Get hints for current and next phase
            var phasesToCheck = GetPhasesAhead(currentPhase, 2).ToList();
            int hintCount = 0;
            var hintsGenerated = new List<string>();

            foreach (var phase in phasesToCheck)
            {
                if (_scheduledOpportunities.TryGetValue(phase, out var opportunities))
                {
                    foreach (var opp in opportunities.Where(o => !o.Consumed))
                    {
                        if (!string.IsNullOrEmpty(opp.NarrativeHint))
                        {
                            hintsGenerated.Add($"{opp.OpportunityId} ({phase})");
                            yield return opp.NarrativeHint;
                            hintCount++;
                            if (hintCount >= 2) // Limit to 2 hints as per spec
                            {
                                ModLogger.Debug(LogCategory, 
                                    $"GetUpcomingHints: {hintCount} hints provided: [{string.Join(", ", hintsGenerated)}]");
                                yield break;
                            }
                        }
                    }
                }
            }

            if (hintCount > 0)
            {
                ModLogger.Debug(LogCategory, 
                    $"GetUpcomingHints: {hintCount} hints provided: [{string.Join(", ", hintsGenerated)}]");
            }
            else
            {
                ModLogger.Debug(LogCategory, 
                    $"GetUpcomingHints: No hints (checking phases: {string.Join(", ", phasesToCheck)})");
            }
        }

        /// <summary>
        /// Gets the next N phases starting from the given phase.
        /// </summary>
        private IEnumerable<DayPhase> GetPhasesAhead(DayPhase startPhase, int count)
        {
            var phases = new[] { DayPhase.Dawn, DayPhase.Midday, DayPhase.Dusk, DayPhase.Night };
            int startIndex = Array.IndexOf(phases, startPhase);

            for (int i = 0; i < count; i++)
            {
                yield return phases[(startIndex + i) % phases.Length];
            }
        }

        /// <summary>
        /// Forces a reschedule of opportunities. Called when major context changes occur
        /// that warrant a fresh schedule (e.g., after loading a save).
        /// </summary>
        public void ForceReschedule()
        {
            _scheduledDay = -1;
            _scheduledOpportunities = null;
            _scheduledTomorrowOpportunities = null;
            ModLogger.Info(LogCategory, "Opportunity schedule invalidated, will regenerate on next access");
        }

        #endregion

        #region Schedule Override System

        /// <summary>
        /// Checks if an orchestrator override should apply to the given phase.
        /// First checks for need-based overrides (critical supplies, exhaustion, etc.),
        /// then checks for variety injections if no need-based override applies.
        /// Returns null if normal schedule should be used.
        /// </summary>
        public OrchestratorOverride CheckForScheduleOverride(DayPhase phase)
        {
            EnsureOverrideConfigLoaded();

            // Check if we already have an override for this phase
            if (_currentOverride != null && _currentOverridePhase == phase)
            {
                return _currentOverride;
            }

            // Get company needs for need-based checks
            var companyNeeds = EnlistmentBehavior.Instance?.CompanyNeeds;
            if (companyNeeds == null)
            {
                return null;
            }

            // Check for need-based overrides first (highest priority)
            var needOverride = CheckNeedBasedOverrides(phase, companyNeeds);
            if (needOverride != null)
            {
                _currentOverride = needOverride;
                _currentOverridePhase = phase;
                ModLogger.Info(LogCategory, 
                    $"Need-based override activated: {needOverride.ActivityName} ({needOverride.Reason})");
                return needOverride;
            }

            // Check for variety injection
            if (ShouldInjectVariety())
            {
                var varietyOverride = SelectVarietyInjection(phase);
                if (varietyOverride != null)
                {
                    _currentOverride = varietyOverride;
                    _currentOverridePhase = phase;
                    _lastVarietyInjectionDay = (int)CampaignTime.Now.ToDays;
                    _varietyInjectionsThisWeek++;
                    ModLogger.Info(LogCategory, 
                        $"Variety injection activated: {varietyOverride.ActivityName}");
                    return varietyOverride;
                }
            }

            // No override - clear cached override if phase changed
            if (_currentOverridePhase != phase)
            {
                _currentOverride = null;
                _currentOverridePhase = phase;
            }

            return null;
        }

        /// <summary>
        /// Checks all need-based override triggers and returns the highest priority override
        /// if conditions are met.
        /// </summary>
        private OrchestratorOverride CheckNeedBasedOverrides(DayPhase phase, CompanyNeedsState needs)
        {
            if (_overrideConfig == null)
            {
                return null;
            }

            OrchestratorOverride highestPriorityOverride = null;
            int highestPriority = -1;

            try
            {
                var needOverrides = _overrideConfig["needBasedOverrides"] as JObject;
                if (needOverrides == null)
                {
                    return null;
                }

                foreach (var overrideDef in needOverrides.Properties())
                {
                    var config = overrideDef.Value;
                    var trigger = config["trigger"];
                    var overrideData = config["override"];

                    if (trigger == null || overrideData == null)
                    {
                        continue;
                    }

                    // Check if trigger condition is met
                    var needName = trigger["need"]?.Value<string>();
                    var threshold = trigger["threshold"]?.Value<int>() ?? 30;
                    var comparison = trigger["comparison"]?.Value<string>() ?? "lessThan";

                    if (string.IsNullOrEmpty(needName))
                    {
                        continue;
                    }

                    // Get the need value
                    int needValue = GetNeedValue(needs, needName);
                    
                    // Check comparison
                    bool triggered = comparison == "lessThan" 
                        ? needValue < threshold 
                        : needValue > threshold;

                    if (!triggered)
                    {
                        continue;
                    }

                    // Check if override applies to this phase
                    var affectedPhases = overrideData["affectedPhases"]?.ToObject<List<string>>();
                    if (affectedPhases != null && affectedPhases.Count > 0)
                    {
                        if (!affectedPhases.Contains(phase.ToString()))
                        {
                            continue;
                        }
                    }

                    // Check priority
                    var priority = overrideData["priority"]?.Value<int>() ?? 50;
                    if (priority <= highestPriority)
                    {
                        continue;
                    }

                    // Create override
                    highestPriorityOverride = new OrchestratorOverride
                    {
                        Type = OverrideType.NeedBased,
                        ActivityCategory = overrideData["category"]?.Value<string>() ?? "foraging",
                        ActivityName = overrideData["name"]?.Value<string>() ?? "Override Activity",
                        Description = overrideData["description"]?.Value<string>() ?? "",
                        Reason = overrideData["reason"]?.Value<string>() ?? "Need critical",
                        Priority = priority,
                        AddressesNeed = overrideData["addressesNeed"]?.Value<string>(),
                        ReplaceBothSlots = overrideData["replaceBothSlots"]?.Value<bool>() ?? true,
                        SkillAffected = overrideData["skill"]?.Value<string>(),
                        BaseXpMin = overrideData["baseXpMin"]?.Value<int>() ?? 5,
                        BaseXpMax = overrideData["baseXpMax"]?.Value<int>() ?? 15,
                        ActivationText = config["activationText"]?.Value<string>()
                    };
                    highestPriority = priority;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ORCHESTRATOR", "Error checking need-based overrides", ex);
            }

            return highestPriorityOverride;
        }

        /// <summary>
        /// Gets the value of a company need by name string.
        /// </summary>
        private int GetNeedValue(CompanyNeedsState needs, string needName)
        {
            return needName.ToLowerInvariant() switch
            {
                "supplies" => needs.Supplies,
                "readiness" => needs.Readiness,
                _ => 100
            };
        }

        /// <summary>
        /// Checks if it's time to inject a variety activity into the schedule.
        /// Based on days since last injection and weekly limits.
        /// </summary>
        public bool ShouldInjectVariety()
        {
            EnsureOverrideConfigLoaded();

            // Check world state - skip variety during intense activity or siege
            var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
            if (worldSituation.ExpectedActivity == ActivityLevel.Intense)
            {
                var settings = _overrideConfig?["varietySettings"];
                if (settings?["skipDuringIntense"]?.Value<bool>() == true)
                {
                    return false;
                }
            }

            if (worldSituation.LordIs == LordSituation.SiegeAttacking || 
                worldSituation.LordIs == LordSituation.SiegeDefending)
            {
                var settings = _overrideConfig?["varietySettings"];
                if (settings?["skipDuringSiege"]?.Value<bool>() == true)
                {
                    return false;
                }
            }

            var currentDay = (int)CampaignTime.Now.ToDays;

            // Reset weekly counter if new week
            if (currentDay - _weekStartDay >= 7)
            {
                _weekStartDay = currentDay;
                _varietyInjectionsThisWeek = 0;
            }

            // Check weekly limit
            var maxPerWeek = _overrideConfig?["varietySettings"]?["maxInjectionsPerWeek"]?.Value<int>() ?? 2;
            if (_varietyInjectionsThisWeek >= maxPerWeek)
            {
                return false;
            }

            // Check minimum days between injections
            var minDays = _overrideConfig?["varietySettings"]?["minDaysBetweenInjections"]?.Value<int>() ?? 3;
            var daysSinceLastInjection = currentDay - _lastVarietyInjectionDay;
            
            if (daysSinceLastInjection < minDays)
            {
                return false;
            }

            // Check if we should roll for injection
            var maxDays = _overrideConfig?["varietySettings"]?["maxDaysBetweenInjections"]?.Value<int>() ?? 5;
            
            // After max days, always inject
            if (daysSinceLastInjection >= maxDays)
            {
                return true;
            }

            // Between min and max, use probability
            var injectionChance = _overrideConfig?["varietySettings"]?["injectionChancePerDay"]?.Value<float>() ?? 0.35f;
            var roll = TaleWorlds.Core.MBRandom.RandomFloat;
            
            return roll < injectionChance;
        }

        /// <summary>
        /// Selects a variety injection appropriate for the given phase using weighted random selection.
        /// </summary>
        public OrchestratorOverride SelectVarietyInjection(DayPhase phase)
        {
            EnsureOverrideConfigLoaded();

            if (_overrideConfig == null)
            {
                return null;
            }

            try
            {
                var varieties = _overrideConfig["varietyInjections"] as JObject;
                if (varieties == null)
                {
                    return null;
                }

                // Build weighted pool of eligible varieties for this phase
                var eligible = new List<(string id, JToken config, int weight)>();
                int totalWeight = 0;

                foreach (var variety in varieties.Properties())
                {
                    var config = variety.Value;
                    var preferredPhases = config["preferredPhases"]?.ToObject<List<string>>();

                    // Check if this variety applies to current phase
                    if (preferredPhases != null && preferredPhases.Count > 0)
                    {
                        if (!preferredPhases.Contains(phase.ToString()))
                        {
                            continue;
                        }
                    }

                    var weight = config["weight"]?.Value<int>() ?? 10;
                    eligible.Add((variety.Name, config, weight));
                    totalWeight += weight;
                }

                if (eligible.Count == 0 || totalWeight == 0)
                {
                    return null;
                }

                // Weighted random selection
                var roll = TaleWorlds.Core.MBRandom.RandomInt(totalWeight);
                int cumulative = 0;
                
                foreach (var (id, config, weight) in eligible)
                {
                    cumulative += weight;
                    if (roll < cumulative)
                    {
                        return new OrchestratorOverride
                        {
                            Type = OverrideType.VarietyInjection,
                            ActivityCategory = config["category"]?.Value<string>() ?? "patrol",
                            ActivityName = config["name"]?.Value<string>() ?? "Special Assignment",
                            Description = config["description"]?.Value<string>() ?? "",
                            Reason = "Variety assignment",
                            Priority = 50,
                            ReplaceBothSlots = false,
                            AffectedPhases = new[] { phase },
                            SkillAffected = config["skill"]?.Value<string>(),
                            BaseXpMin = config["baseXpMin"]?.Value<int>() ?? 4,
                            BaseXpMax = config["baseXpMax"]?.Value<int>() ?? 10,
                            ActivationText = config["activationText"]?.Value<string>()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ORCHESTRATOR", "Error selecting variety injection", ex);
            }

            return null;
        }

        /// <summary>
        /// Gets the currently active override for the specified phase, if any.
        /// Returns null if no override is active.
        /// </summary>
        public OrchestratorOverride GetActiveOverride(DayPhase phase)
        {
            if (_currentOverride != null && _currentOverridePhase == phase)
            {
                return _currentOverride;
            }
            return null;
        }

        /// <summary>
        /// Clears the current override. Called when phase transitions or override is consumed.
        /// </summary>
        public void ClearCurrentOverride()
        {
            _currentOverride = null;
        }

        /// <summary>
        /// Ensures the override configuration is loaded from JSON.
        /// </summary>
        private void EnsureOverrideConfigLoaded()
        {
            if (_overrideConfigLoaded)
            {
                return;
            }

            try
            {
                var configPath = ModulePaths.GetConfigPath("orchestrator_overrides.json");

                if (!File.Exists(configPath))
                {
                    ModLogger.Warn(LogCategory, $"Override config not found at {configPath}");
                    _overrideConfigLoaded = true;
                    return;
                }

                var json = File.ReadAllText(configPath);
                _overrideConfig = JObject.Parse(json);
                _overrideConfigLoaded = true;
                ModLogger.Info(LogCategory, "Orchestrator override config loaded");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ORCHESTRATOR", "Failed to load orchestrator override config", ex);
                _overrideConfigLoaded = true;
            }
        }

        #endregion
    }
}
