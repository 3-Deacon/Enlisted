using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Conditions;
using Enlisted.Features.Content;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Ranks;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Camp
{
    /// <summary>
    /// Generates camp life opportunities based on world state, camp context, player state, and history.
    /// The heart of the living camp simulation. Opportunities are filtered, scored, and selected
    /// to create a realistic camp experience that adapts to the player.
    /// </summary>
    public sealed class CampOpportunityGenerator : CampaignBehaviorBase
    {
        private const string LogCategory = "CAMPLIFE";
        private const int FitnessThreshold = 40;
        private const int MaxOpportunitiesDefault = 3;

        /// <summary>Singleton instance for external access.</summary>
        public static CampOpportunityGenerator Instance { get; private set; }

        // Opportunity definitions loaded from JSON
        private List<CampOpportunity> _opportunityDefinitions = new List<CampOpportunity>();
        private bool _definitionsLoaded;

        // History tracking
        private OpportunityHistory _history = new OpportunityHistory();

        // Player commitments
        private PlayerCommitments _commitments = new PlayerCommitments();

        // Cached opportunities for current phase
        private List<CampOpportunity> _cachedOpportunities;
        private DayPhase _cachePhase = DayPhase.Night;

        // Promotion reputation pressure (cached per generation cycle for performance)
        private bool _cachedNeedsReputation = false;
        private int _cachedReputationGap = 0;
        private bool _hasShownPromotionRepNotice = false;

        // Track previous phase for routine processing
        private DayPhase _previousPhase = DayPhase.Night;
        private bool _hasProcessedFirstPhase;

        public CampOpportunityGenerator()
        {
            Instance = this;
        }

        /// <summary>Current player commitments.</summary>
        public PlayerCommitments Commitments => _commitments;

        /// <summary>Opportunity history for learning.</summary>
        public OpportunityHistory History => _history;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            ModLogger.Info(LogCategory, "CampOpportunityGenerator registered");
        }

        /// <summary>
        /// Hourly tick handler to check for scheduled commitments that should fire.
        /// Checks at phase boundaries: Dawn (6am), Midday (12pm), Dusk (6pm), Night (12am).
        /// </summary>
        private void OnHourlyTick()
        {
            try
            {
                var currentHour = CampaignTime.Now.GetHourOfDay;
                var currentDay = (int)CampaignTime.Now.ToDays;

                // Check at phase boundaries only
                if (currentHour != 6 && currentHour != 12 && currentHour != 18 && currentHour != 0)
                {
                    return;
                }

                var currentPhase = currentHour switch
                {
                    6 => "Dawn",
                    12 => "Midday",
                    18 => "Dusk",
                    0 or 24 => "Night",
                    _ => null
                };

                if (currentPhase == null)
                {
                    return;
                }

                FireScheduledCommitments(currentPhase, currentDay);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CAMPLIFE", "Error in OnHourlyTick checking commitments", ex);
            }
        }

        /// <summary>
        /// Fires all commitments scheduled for the given phase and day.
        /// </summary>
        private void FireScheduledCommitments(string phase, int day)
        {
            var toFire = _commitments.GetCommitmentsForPhase(phase, day);

            if (toFire.Count == 0)
            {
                return;
            }

            ModLogger.Info(LogCategory, $"Firing {toFire.Count} scheduled commitments for {phase} on day {day}");

            foreach (var commitment in toFire)
            {
                try
                {
                    FireCommitment(commitment);
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("CAMPLIFE", $"Error firing commitment {commitment.OpportunityId}", ex);
                }
                finally
                {
                    _ = _commitments.RemoveCommitment(commitment.OpportunityId);
                }
            }
        }

        /// <summary>
        /// Fires a single commitment by delivering the target decision as an event.
        /// </summary>
        private void FireCommitment(ScheduledCommitment commitment)
        {
            // Get the decision definition
            var decision = DecisionCatalog.GetDecision(commitment.TargetDecisionId);
            if (decision == null)
            {
                ModLogger.Warn(LogCategory, $"Commitment target decision not found: {commitment.TargetDecisionId}");
                InformationManager.DisplayMessage(new InformationMessage(
                    $"The scheduled activity ({commitment.Title}) is no longer available.",
                    Colors.Yellow));
                return;
            }

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
                    SourceId = "camp.scheduled_commitment." + (commitment.TargetDecisionId ?? "unknown"),
                    CategoryId = "commitment." + (decision.Id ?? "unknown"),
                    ProposedTier = StoryTier.Modal,
                    SeverityHint = 0.40f,
                    Beats = { StoryBeat.OrderPhaseTransition },
                    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                    EmittedAt = CampaignTime.Now,
                    InteractiveEvent = eventDef,
                    RenderedTitle = eventDef.TitleFallback,
                    RenderedBody = eventDef.SetupFallback,
                    StoryKey = eventDef.Id
                });
                ModLogger.Info(LogCategory, $"Routed scheduled commitment through Director: {commitment.OpportunityId} -> {commitment.TargetDecisionId}");

                // Show notification that scheduled activity is starting
                var phaseText = commitment.ScheduledPhase.ToLower();
                InformationManager.DisplayMessage(new InformationMessage(
                    $"It's {phaseText}. Time for {commitment.Title.ToLower()}.",
                    Colors.Cyan));
            }
            else
            {
                var deliveryManager = EventDeliveryManager.Instance;
                if (deliveryManager != null)
                {
                    deliveryManager.QueueEvent(eventDef);
                    ModLogger.Info(LogCategory, $"Fired scheduled commitment (Director unavailable): {commitment.OpportunityId} -> {commitment.TargetDecisionId}");

                    var phaseText = commitment.ScheduledPhase.ToLower();
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"It's {phaseText}. Time for {commitment.Title.ToLower()}.",
                        Colors.Cyan));
                }
                else
                {
                    ModLogger.Warn(LogCategory, "EventDeliveryManager not available for scheduled commitment");
                }
            }
        }

        /// <summary>
        /// Commits the player to an opportunity at its scheduled phase.
        /// The opportunity will grey out in the menu and fire automatically at the scheduled time.
        /// </summary>
        public void CommitToOpportunity(CampOpportunity opportunity)
        {
            if (opportunity == null)
            {
                return;
            }

            var scheduledPhase = opportunity.GetEffectiveScheduledPhase();
            var scheduledDay = PlayerCommitments.CalculateScheduledDay(scheduledPhase);
            var hoursUntil = _commitments.GetHoursUntilCommitment(new ScheduledCommitment
            {
                ScheduledPhase = scheduledPhase,
                ScheduledDay = scheduledDay
            });

            // Generate display text
            var phaseText = scheduledPhase.ToLower();
            var displayText = hoursUntil <= 6
                ? $"You've committed to {opportunity.TitleFallback?.ToLower() ?? opportunity.Id} this {phaseText}."
                : $"You've committed to {opportunity.TitleFallback?.ToLower() ?? opportunity.Id} tomorrow at {phaseText}.";

            _commitments.AddCommitment(
                opportunity.Id,
                opportunity.TargetDecisionId,
                opportunity.TitleFallback ?? opportunity.Id,
                scheduledPhase,
                scheduledDay,
                displayText
            );

            // Record in history as engaged
            RecordEngagement(opportunity.Id, opportunity.Type);

            // Clear cache to force menu refresh
            _cachedOpportunities = null;

            ModLogger.Info(LogCategory, $"Committed to {opportunity.Id} for {scheduledPhase} (day {scheduledDay}, ~{hoursUntil:F0}h)");

            // Show confirmation message
            var message = new TextObject("{=enlisted_commitment_scheduled}You've made plans for {ACTIVITY} at {PHASE}.");
            _ = message.SetTextVariable("ACTIVITY", opportunity.TitleFallback ?? opportunity.Id);
            _ = message.SetTextVariable("PHASE", phaseText);
            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Cyan));
        }

        /// <summary>
        /// Cancels a commitment.
        /// </summary>
        public void CancelCommitment(string opportunityId)
        {
            var commitment = _commitments.GetCommitment(opportunityId);
            if (commitment == null)
            {
                return;
            }

            _ = _commitments.RemoveCommitment(opportunityId);

            // Clear cache to force menu refresh
            _cachedOpportunities = null;

            ModLogger.Info(LogCategory, $"Cancelled commitment: {opportunityId}");

            InformationManager.DisplayMessage(new InformationMessage(
                "{=enlisted_commitment_cancelled}Commitment cancelled. You feel restless from changing plans.",
                Colors.Yellow));
        }

        /// <summary>
        /// Checks if the player is committed to a specific opportunity.
        /// </summary>
        public bool IsCommittedTo(string opportunityId)
        {
            return _commitments.IsCommittedTo(opportunityId);
        }

        /// <summary>
        /// Gets all active commitments for display in the Your Status section.
        /// </summary>
        public IReadOnlyList<ScheduledCommitment> GetActiveCommitments()
        {
            return _commitments.Commitments;
        }

        /// <summary>
        /// Gets the next commitment to fire (closest in time).
        /// </summary>
        public ScheduledCommitment GetNextCommitment()
        {
            return _commitments.GetNextCommitment();
        }

        /// <summary>
        /// Gets a specific commitment by opportunity ID.
        /// </summary>
        public ScheduledCommitment GetCommitment(string opportunityId)
        {
            return _commitments.GetCommitment(opportunityId);
        }

        /// <summary>
        /// Gets hours until a commitment fires.
        /// </summary>
        public float GetHoursUntilCommitment(ScheduledCommitment commitment)
        {
            return _commitments.GetHoursUntilCommitment(commitment);
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                // Serialize history as dictionaries - use local variables for ref passing
                var lastPresented = _history.LastPresentedHours;
                var timesSeen = _history.TimesSeen;
                var timesEngaged = _history.TimesEngaged;
                var timesIgnored = _history.TimesIgnored;

                _ = dataStore.SyncData("camp_lastPresented", ref lastPresented);
                _ = dataStore.SyncData("camp_timesSeen", ref timesSeen);
                _ = dataStore.SyncData("camp_timesEngaged", ref timesEngaged);
                _ = dataStore.SyncData("camp_timesIgnored", ref timesIgnored);

                // Serialize commitments (new multi-commitment format)
                var commitmentCount = dataStore.IsSaving ? _commitments.Commitments.Count : 0;
                _ = dataStore.SyncData("camp_commitmentCount", ref commitmentCount);

                // Serialize each commitment
                if (dataStore.IsSaving)
                {
                    for (var i = 0; i < _commitments.Commitments.Count; i++)
                    {
                        var c = _commitments.Commitments[i];
                        var oppId = c.OpportunityId ?? "";
                        var decId = c.TargetDecisionId ?? "";
                        var title = c.Title ?? "";
                        var phase = c.ScheduledPhase ?? "";
                        var day = c.ScheduledDay;
                        var commitTime = c.CommitTimeHours;
                        var display = c.DisplayText ?? "";

                        _ = dataStore.SyncData($"camp_commit_{i}_oppId", ref oppId);
                        _ = dataStore.SyncData($"camp_commit_{i}_decId", ref decId);
                        _ = dataStore.SyncData($"camp_commit_{i}_title", ref title);
                        _ = dataStore.SyncData($"camp_commit_{i}_phase", ref phase);
                        _ = dataStore.SyncData($"camp_commit_{i}_day", ref day);
                        _ = dataStore.SyncData($"camp_commit_{i}_time", ref commitTime);
                        _ = dataStore.SyncData($"camp_commit_{i}_display", ref display);
                    }
                }

                // Legacy compatibility: still sync old format for backwards compatibility
                var legacyId = "";
                var legacyTime = 0f;
                var legacyText = "";
                _ = dataStore.SyncData("camp_commitmentId", ref legacyId);
                _ = dataStore.SyncData("camp_commitmentTime", ref legacyTime);
                _ = dataStore.SyncData("camp_commitmentText", ref legacyText);

                // Serialize learning system data (opportunity engagement tracking)
                var oppPresented = dataStore.IsSaving
                    ? PlayerBehaviorTracker.GetOpportunityPresentedForSave()
                    : new Dictionary<string, int>();
                var oppEngaged = dataStore.IsSaving
                    ? PlayerBehaviorTracker.GetOpportunityEngagedForSave()
                    : new Dictionary<string, int>();

                _ = dataStore.SyncData("camp_oppPresented", ref oppPresented);
                _ = dataStore.SyncData("camp_oppEngaged", ref oppEngaged);

                if (dataStore.IsLoading)
                {
                    // Restore history from loaded values
                    _history = new OpportunityHistory
                    {
                        LastPresentedHours = lastPresented ?? new Dictionary<string, float>(),
                        TimesSeen = timesSeen ?? new Dictionary<string, int>(),
                        TimesEngaged = timesEngaged ?? new Dictionary<string, int>(),
                        TimesIgnored = timesIgnored ?? new Dictionary<string, int>()
                    };

                    // Restore commitments
                    _commitments = new PlayerCommitments();

                    // Try to load new format first
                    for (var i = 0; i < commitmentCount; i++)
                    {
                        var oppId = "";
                        var decId = "";
                        var title = "";
                        var phase = "";
                        var day = 0;
                        var commitTime = 0f;
                        var display = "";

                        _ = dataStore.SyncData($"camp_commit_{i}_oppId", ref oppId);
                        _ = dataStore.SyncData($"camp_commit_{i}_decId", ref decId);
                        _ = dataStore.SyncData($"camp_commit_{i}_title", ref title);
                        _ = dataStore.SyncData($"camp_commit_{i}_phase", ref phase);
                        _ = dataStore.SyncData($"camp_commit_{i}_day", ref day);
                        _ = dataStore.SyncData($"camp_commit_{i}_time", ref commitTime);
                        _ = dataStore.SyncData($"camp_commit_{i}_display", ref display);

                        if (!string.IsNullOrEmpty(oppId))
                        {
                            _commitments.Commitments.Add(new ScheduledCommitment
                            {
                                OpportunityId = oppId,
                                TargetDecisionId = decId,
                                Title = title,
                                ScheduledPhase = phase,
                                ScheduledDay = day,
                                CommitTimeHours = commitTime,
                                DisplayText = display
                            });
                        }
                    }

                    // Fall back to legacy format if no new commitments loaded
                    if (_commitments.Commitments.Count == 0 && !string.IsNullOrEmpty(legacyId))
                    {
#pragma warning disable CS0618 // Intentionally using obsolete for migration
                        _commitments.ScheduledActivityId = legacyId;
                        _commitments.ScheduledTimeHours = legacyTime > 0 ? legacyTime : null;
                        _commitments.CommitmentDisplayText = legacyText;
#pragma warning restore CS0618
                    }

                    // Restore learning system data
                    PlayerBehaviorTracker.LoadOpportunityState(oppPresented, oppEngaged);

                    ModLogger.Debug(LogCategory, $"Loaded opportunity history: {_history.TimesSeen.Count} types seen, {_commitments.Commitments.Count} commitments, {oppPresented?.Count ?? 0} learning entries");
                }
            });
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            LoadOpportunityDefinitions();
            InitializeScheduleManager();
        }

        /// <summary>
        /// Initializes the camp schedule manager for routine tracking.
        /// Creates a schedule manager instance if one doesn't exist.
        /// </summary>
        private void InitializeScheduleManager()
        {
            if (CampScheduleManager.Instance == null)
            {
                _ = new CampScheduleManager();
                ModLogger.Info(LogCategory, "CampScheduleManager initialized");
            }
        }

        /// <summary>
        /// Generates camp life opportunities for the current context.
        /// Returns 0-3 opportunities based on world state, budget, and player state.
        /// </summary>
        /// <remarks>
        /// INTERNAL USE ONLY: Called by ContentOrchestrator during daily scheduling.
        /// External code should use ContentOrchestrator.GetCurrentPhaseOpportunities()
        /// which provides stable, pre-scheduled opportunities that won't disappear
        /// when context changes mid-session.
        /// </remarks>
        internal List<CampOpportunity> GenerateCampLife()
        {
            // Lazy initialization: ensure definitions are loaded before first use
            // This handles the timing issue where Orchestrator may request opportunities
            // before OnSessionLaunched fires on this behavior
            if (!_definitionsLoaded)
            {
                ModLogger.Surfaced("CAMPLIFE", "Opportunity definitions not yet loaded - loading now (this is normal on first access after load)");
                LoadOpportunityDefinitions();

                // Diagnostic: warn if still no definitions after loading
                if (_opportunityDefinitions.Count == 0)
                {
                    ModLogger.Surfaced("CAMPLIFE",
                        "No opportunity definitions found — check camp_opportunities.json exists and has valid content. Decisions won't appear in the accordion menu.");
                }
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                ModLogger.Info(LogCategory, $"GenerateCampLife: Not enlisted (enlistment={enlistment != null}, IsEnlisted={enlistment?.IsEnlisted ?? false})");
                return new List<CampOpportunity>();
            }

            // Build context from all layers
            var campContext = AnalyzeCampContext();

            // Check for edge cases that block opportunities entirely
            if (ShouldBlockAllOpportunities(campContext, out string blockReason))
            {
                ModLogger.Info(LogCategory, $"Opportunities blocked: {blockReason}");
                return new List<CampOpportunity>();
            }

            // Check cache validity
            if (_cachedOpportunities != null && _cachePhase == campContext.DayPhase)
            {
                ModLogger.Debug(LogCategory, $"Returning cached {_cachedOpportunities.Count} opportunities");
                return _cachedOpportunities;
            }

            // Get world situation
            var worldSituation = ContentOrchestrator.Instance?.GetCurrentWorldSituation()
                ?? WorldStateAnalyzer.AnalyzeSituation();

            // Get player preferences
            var playerPrefs = PlayerBehaviorTracker.GetPreferences();

            // Determine opportunity budget
            int budget = DetermineOpportunityBudget(worldSituation, campContext);
            ModLogger.Info(LogCategory, $"Opportunity budget: {budget} (LordIs={worldSituation.LordIs}, phase={campContext.DayPhase}, activity={campContext.ActivityLevel})");

            if (budget <= 0)
            {
                ModLogger.Info(LogCategory, "Budget is 0 - no opportunities available");
                _cachedOpportunities = new List<CampOpportunity>();
                _cachePhase = campContext.DayPhase;
                return _cachedOpportunities;
            }

            // Generate candidates
            var candidates = GenerateCandidates(campContext);
            ModLogger.Info(LogCategory, $"Generated {candidates.Count} candidates from {_opportunityDefinitions.Count} total definitions");

            // Diagnostic: warn if no candidates despite having definitions and budget
            if (candidates.Count == 0 && _opportunityDefinitions.Count > 0 && budget > 0)
            {
                ModLogger.Surfaced("CAMPLIFE",
                    "No candidates passed filtering — check tier/context requirements in camp_opportunities.json match current state.");
            }

            // Cache promotion reputation need (calculated once, used for all fitness calculations)
            (_cachedNeedsReputation, _cachedReputationGap) = SimulationPressureCalculator.CheckPromotionReputationNeed();
            if (_cachedNeedsReputation && _cachedReputationGap > 0 && !_hasShownPromotionRepNotice)
            {
                _hasShownPromotionRepNotice = true;
                ModLogger.Info(LogCategory,
                    $"Promotion reputation pressure: boosting rep-granting opportunities (need {_cachedReputationGap} more soldier rep)");
            }

            // Score each candidate
            foreach (var candidate in candidates)
            {
                candidate.FitnessScore = CalculateFitness(candidate, worldSituation, campContext, playerPrefs, _history);
            }

            // Separate guaranteed opportunities (immediate=true, like baggage access) from normal candidates
            var guaranteed = candidates.Where(c => c.Immediate).ToList();
            var normalCandidates = candidates.Where(c => !c.Immediate).ToList();

            // Select best fitting normal opportunities (within budget)
            var selected = SelectTopN(normalCandidates, budget);

            // Add guaranteed opportunities (always show when available, don't count against budget)
            foreach (var guaranteedOpp in guaranteed)
            {
                if (!selected.Any(s => s.Id == guaranteedOpp.Id))
                {
                    selected.Add(guaranteedOpp);
                    ModLogger.Debug(LogCategory, $"Guaranteed opportunity added: {guaranteedOpp.Id}");
                }
            }

            // Record presentations for history and learning system
            foreach (var opp in selected)
            {
                _history.RecordPresented(opp.Id, opp.Type.ToString());
                PlayerBehaviorTracker.RecordOpportunityPresented(opp.Type.ToString());
            }

            // Cache results
            _cachedOpportunities = selected;
            _cachePhase = campContext.DayPhase;

            ModLogger.Info(LogCategory, $"Selected {selected.Count} opportunities ({guaranteed.Count} guaranteed): [{string.Join(", ", selected.Select(o => o.Id))}]");
            return selected;
        }

        /// <summary>
        /// Returns narrative hints for upcoming opportunities (for Company Reports).
        /// Hints are brief, immersive text that foreshadow available activities.
        /// Returns up to 2 hints from currently cached opportunities.
        /// </summary>
        /// <remarks>
        /// This method only uses cached opportunities - it does NOT regenerate them.
        /// The ContentOrchestrator owns the opportunity schedule and should be the
        /// primary source for hints via GetUpcomingHints().
        /// </remarks>
        public IEnumerable<string> GetUpcomingHints()
        {
            // Use cached opportunities only - do NOT regenerate
            // The Orchestrator owns the schedule; this is just a fallback for cached data
            if (_cachedOpportunities == null || _cachedOpportunities.Count == 0)
            {
                yield break;
            }

            // Return up to 2 hints from cached opportunities
            int hintCount = 0;
            foreach (var opp in _cachedOpportunities)
            {
                var hint = opp.GetHint();
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    yield return hint;
                    hintCount++;
                    if (hintCount >= 2)
                    {
                        yield break;
                    }
                }
            }
        }

        /// <summary>
        /// Called when day phase changes. Processes routine for the completed phase,
        /// invalidates the opportunity cache, and updates the schedule for the new phase.
        /// </summary>
        public void OnPhaseChanged(DayPhase newPhase)
        {
            // Process routine for the completed phase (the previous phase)
            if (_hasProcessedFirstPhase && _previousPhase != newPhase)
            {
                ProcessCompletedPhaseRoutine(_previousPhase);
            }

            // Track for next transition
            _previousPhase = newPhase;
            _hasProcessedFirstPhase = true;

            // Invalidate opportunity cache
            _cachedOpportunities = null;

            // Update the schedule manager for the new phase
            CampScheduleManager.Instance?.OnPhaseChanged(newPhase);

            ModLogger.Debug(LogCategory, $"Phase changed to {newPhase}, cache invalidated");
        }

        /// <summary>
        /// Invalidates the opportunity cache, forcing a fresh generation on next access.
        /// Called when major state changes occur (like muster completion) that affect opportunity availability.
        /// </summary>
        public void InvalidateCache()
        {
            _cachedOpportunities = null;

            // Reset promotion reputation pressure cache (will recalculate on next generation)
            _cachedNeedsReputation = false;
            _cachedReputationGap = 0;
            _hasShownPromotionRepNotice = false;

            ModLogger.Debug(LogCategory, "Opportunity cache manually invalidated");
        }

        /// <summary>
        /// Queues a medical opportunity for high-priority display.
        /// Stub implementation for Phase 6H medical orchestration.
        /// </summary>
        public void QueueMedicalOpportunity(string opportunityType)
        {
            // Phase 6H stub: Medical opportunities are prioritized through normal fitness scoring
            // with medical-related opportunities boosted when medical pressure is high
            ModLogger.Debug(LogCategory, $"Medical opportunity queued: {opportunityType} (stub - using fitness boost instead)");
            InvalidateCache(); // Force regeneration to pick up new medical context
        }

        /// <summary>
        /// Processes the automatic routine activities for a completed phase.
        /// Generates outcomes and applies them to player state.
        /// </summary>
        private void ProcessCompletedPhaseRoutine(DayPhase completedPhase)
        {
            // Only process if player is enlisted
            if (EnlistmentBehavior.Instance?.IsEnlisted != true)
            {
                return;
            }

            // Get the schedule that was active for the completed phase
            var schedule = CampScheduleManager.Instance?.GetScheduleForPhase(completedPhase);
            if (schedule == null)
            {
                ModLogger.Debug(LogCategory, $"No schedule for completed phase {completedPhase}");
                return;
            }

            // Process routine and get outcomes
            var outcomes = CampRoutineProcessor.ProcessPhaseTransition(completedPhase, schedule);

            if (outcomes.Count > 0)
            {
                ModLogger.Info(LogCategory,
                    $"Processed {outcomes.Count} routine activities for {completedPhase}");
            }
        }

        /// <summary>
        /// Analyzes current camp context from game state.
        /// </summary>
        public CampContext AnalyzeCampContext()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
            var simulation = CompanySimulationBehavior.Instance;

            var context = new CampContext
            {
                DayPhase = worldSituation.CurrentDayPhase,
                LordSituation = worldSituation.LordIs,
                ActivityLevel = worldSituation.ExpectedActivity
            };

            // Muster cycle position: calculate from LastMusterDay
            int currentDay = (int)CampaignTime.Now.ToDays;
            int lastMusterDay = enlistment?.LastMusterDay ?? 0;

            // If lastMusterDay is 0 (uninitialized), treat as if muster just happened
            if (lastMusterDay == 0)
            {
                lastMusterDay = currentDay;
            }

            context.DaysSinceLastMuster = currentDay - lastMusterDay;

            // IsMusterDay should only be true when the MUSTER MENU is actively being shown,
            // not just because it's been 12 days. The muster system handles its own timing.
            // Block opportunities only during active muster sequence (detected by menu state).
            var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "";
            context.IsMusterDay = currentMenu.StartsWith("enlisted_muster_");

            // Camp mood derived from recent events
            context.CurrentMood = DeriveCampMood(simulation);

            // Player state
            context.PlayerOnDuty = IsPlayerOnDuty();
            context.PlayerGold = Hero.MainHero.Gold;
            context.PlayerInjured = Hero.MainHero.HitPoints < Hero.MainHero.MaxHitPoints * 0.5f;

            // Company state
            var needs = enlistment?.CompanyNeeds;
            context.SupplyLevel = needs?.Supplies ?? 50;

            // Edge case flags
            // NOTE: New enlistment grace period removed - let content flow immediately
            context.IsNewEnlistmentGrace = false;
            context.IsOnProbation = enlistment?.IsOnProbation ?? false;
            context.InBaggageWindow = IsBaggageWindowActive(enlistment);

            return context;
        }

        /// <summary>
        /// Checks if opportunities should be completely blocked due to edge cases.
        /// </summary>
        private bool ShouldBlockAllOpportunities(CampContext context, out string reason)
        {
            // New enlistment grace period
            if (context.IsNewEnlistmentGrace)
            {
                reason = "New enlistment grace period (first 3 days)";
                return true;
            }

            // Active muster sequence (player is in muster menu)
            if (context.IsMusterDay)
            {
                reason = "Active muster sequence - structured menu takes over";
                return true;
            }

            // Player on duty (will show filtered opportunities instead)
            // Actually, we don't block - we filter. So this is not a full block.

            // Critical supply shortage
            if (context.SupplyLevel < 20)
            {
                // Budget reduced, not blocked
                reason = null;
                return false;
            }

            // Lord captured/grace period would be handled by EnlistmentBehavior

            reason = null;
            return false;
        }

        /// <summary>
        /// Determines how many opportunities to show based on context.
        /// </summary>
        private int DetermineOpportunityBudget(WorldSituation world, CampContext camp)
        {
            int budget;

            // Base budget by lord situation and day phase
            budget = (world.LordIs, camp.DayPhase) switch
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

                // Default
                _ => 2
            };

            // Modifiers
            if (camp.IsOnProbation)
            {
                budget = Math.Max(0, budget - 1);
            }

            if (camp.SupplyLevel < 30)
            {
                budget = Math.Max(0, budget - 1);
            }

            if (camp.SupplyLevel < 20)
            {
                budget = 1; // Survival mode
            }

            if (camp.PlayerOnDuty)
            {
                // On duty: reduced but not zero - risky opportunities still available
                budget = Math.Max(1, budget / 2);
            }

            return Math.Min(budget, MaxOpportunitiesDefault);
        }

        /// <summary>
        /// Generates candidates for a SPECIFIC target phase (used by ContentOrchestrator for daily scheduling).
        /// Unlike GenerateCampLife() which uses the current phase, this generates candidates AS IF it were the target phase.
        /// </summary>
        /// <param name="targetPhase">The day phase to generate candidates for.</param>
        /// <returns>Unselected candidates (not budget-limited) for the orchestrator to score and select.</returns>
        internal List<CampOpportunity> GenerateCandidatesForPhase(DayPhase targetPhase)
        {
            // Ensure definitions are loaded
            if (!_definitionsLoaded)
            {
                LoadOpportunityDefinitions();
            }

            if (_opportunityDefinitions.Count == 0)
            {
                ModLogger.Debug(LogCategory, $"GenerateCandidatesForPhase({targetPhase}): No definitions loaded");
                return new List<CampOpportunity>();
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return new List<CampOpportunity>();
            }

            // Build context but OVERRIDE the phase to the target phase
            var campContext = AnalyzeCampContext();
            var actualPhase = campContext.DayPhase;
            campContext.DayPhase = targetPhase; // Override to target phase

            // Generate candidates for the target phase (no budget limiting - orchestrator handles that)
            var candidates = GenerateCandidates(campContext);

            ModLogger.Debug(LogCategory,
                $"GenerateCandidatesForPhase({targetPhase}): {candidates.Count} candidates (actual phase was {actualPhase})");

            return candidates;
        }

        /// <summary>
        /// Generates candidate opportunities based on current context.
        /// </summary>
        private List<CampOpportunity> GenerateCandidates(CampContext camp)
        {
            var candidates = new List<CampOpportunity>();
            var tier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
            var filterCounts = new Dictionary<string, int>();

            foreach (var opp in _opportunityDefinitions)
            {
                // Tier check
                if (tier < opp.MinTier)
                {
                    IncrementFilterCount(filterCounts, "tier_too_low");
                    continue;
                }

                if (opp.MaxTier > 0 && tier > opp.MaxTier)
                {
                    IncrementFilterCount(filterCounts, "tier_too_high");
                    continue;
                }

                // Cooldown check
                if (_history.WasRecentlyShown(opp.Id, opp.CooldownHours))
                {
                    IncrementFilterCount(filterCounts, "cooldown");
                    continue;
                }

                // Day phase check
                if (opp.ValidPhases.Count > 0 && !opp.ValidPhases.Contains(camp.DayPhase.ToString()))
                {
                    IncrementFilterCount(filterCounts, "wrong_phase");
                    continue;
                }

                // Sea/land context check
                var enlistment = EnlistmentBehavior.Instance;
                var party = enlistment?.CurrentLord?.PartyBelongedTo;
                // BUGFIX: If party is in a settlement or besieging, they are on land regardless of IsCurrentlyAtSea
                var isAtSea = party != null &&
                              party.CurrentSettlement == null &&
                              party.BesiegedSettlement == null &&
                              party.IsCurrentlyAtSea;

                if (opp.NotAtSea && isAtSea)
                {
                    IncrementFilterCount(filterCounts, "at_sea");
                    continue; // Land-only opportunity but party is at sea
                }

                if (opp.AtSea && !isAtSea)
                {
                    IncrementFilterCount(filterCounts, "on_land");
                    continue; // Sea-only opportunity but party is on land
                }

                // Order compatibility check when on duty
                if (camp.PlayerOnDuty)
                {
                    var currentOrderType = GetCurrentOrderType();
                    var compat = opp.GetOrderCompatibility(currentOrderType);
                    if (compat == "blocked")
                    {
                        IncrementFilterCount(filterCounts, "order_blocked");
                        continue;
                    }
                }

                // Injury filter: no training if injured
                if (camp.PlayerInjured && opp.Type == OpportunityType.Training)
                {
                    IncrementFilterCount(filterCounts, "injured");
                    continue;
                }

                // Gold filter: no gambling if broke
                if (camp.PlayerGold < 20 && opp.Type == OpportunityType.Economic && opp.Id.Contains("gambl"))
                {
                    IncrementFilterCount(filterCounts, "no_gold");
                    continue;
                }

                // Condition state filter: check player condition requirements
                if (opp.ConditionStates != null && opp.ConditionStates.Count > 0)
                {
                    if (!MeetsConditionStateRequirements(opp.ConditionStates))
                    {
                        IncrementFilterCount(filterCounts, "condition_state");
                        continue;
                    }
                }

                // Medical pressure filter: check medical risk level requirements
                if (opp.MedicalPressure != null && opp.MedicalPressure.Count > 0)
                {
                    if (!MeetsMedicalPressureRequirements(opp.MedicalPressure))
                    {
                        IncrementFilterCount(filterCounts, "medical_pressure");
                        continue;
                    }
                }

                // Suppress when treated: hide medical opportunities if player is already under care
                if (opp.SuppressWhenTreated)
                {
                    var conditions = PlayerConditionBehavior.Instance;
                    if (conditions?.IsEnabled() == true && conditions.State?.UnderMedicalCare == true)
                    {
                        IncrementFilterCount(filterCounts, "under_treatment");
                        continue;
                    }
                }

                // BAGGAGE ACCESS FILTER: Only show baggage access if player actually has access
                // This prevents the frustrating UX of clicking "Baggage Access" and getting "access denied"
                if (opp.Id == "opp_baggage_access")
                {
                    var baggageManager = Logistics.BaggageTrainManager.Instance;
                    if (baggageManager != null)
                    {
                        var accessState = baggageManager.GetCurrentAccess();
                        if (accessState != Logistics.BaggageAccessState.FullAccess &&
                            accessState != Logistics.BaggageAccessState.TemporaryAccess)
                        {
                            IncrementFilterCount(filterCounts, "no_baggage_access");
                            continue;
                        }
                    }
                }

                // Create a copy with instance-specific data
                var candidate = CloneOpportunity(opp);
                candidates.Add(candidate);
            }

            // Log filter statistics
            if (filterCounts.Count > 0)
            {
                var filterSummary = string.Join(", ", filterCounts.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                ModLogger.Info(LogCategory, $"Candidate filters (tier={tier}, phase={camp.DayPhase}, onDuty={camp.PlayerOnDuty}): {filterSummary}");
            }

            // Log filter summary
            if (filterCounts.Count > 0)
            {
                var filterSummary = string.Join(", ", filterCounts.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                ModLogger.Debug(LogCategory, $"Opportunity filtering: {candidates.Count} passed, rejected: {filterSummary}");
            }
            else
            {
                ModLogger.Debug(LogCategory, $"Opportunity filtering: {candidates.Count} passed, none rejected");
            }

            return candidates;
        }

        private static void IncrementFilterCount(Dictionary<string, int> counts, string key)
        {
            if (counts.ContainsKey(key))
            {
                counts[key]++;
            }
            else
            {
                counts[key] = 1;
            }
        }

        /// <summary>
        /// Calculates fitness score for an opportunity using all 4 intelligence layers plus schedule awareness.
        /// </summary>
        private float CalculateFitness(CampOpportunity opp, WorldSituation world, CampContext camp,
            PlayerPreferences prefs, OpportunityHistory history)
        {
            float score = opp.BaseFitness;

            // LAYER 1: World State (Macro)
            score += CalculateWorldStateModifier(opp, world);

            // LAYER 2: Camp Context (Meso)
            score += CalculateCampContextModifier(opp, camp);

            // LAYER 3: Player State (Micro)
            score += CalculatePlayerStateModifier(opp, camp, prefs);

            // LAYER 4: History (Meta)
            score += CalculateHistoryModifier(opp, history);

            // LAYER 5: Schedule Awareness (Routine)
            score = ApplyScheduleBoost(opp, score, camp);

            return Math.Max(0f, Math.Min(100f, score));
        }

        /// <summary>
        /// Applies schedule boost to opportunities that match the current routine schedule.
        /// Opportunities matching scheduled activities get a fitness boost, making them more likely to appear.
        /// </summary>
        private float ApplyScheduleBoost(CampOpportunity opp, float currentScore, CampContext camp)
        {
            var scheduleManager = CampScheduleManager.Instance;
            if (scheduleManager == null)
            {
                return currentScore;
            }

            var currentSchedule = scheduleManager.GetScheduleForPhase(camp.DayPhase);
            if (currentSchedule == null)
            {
                return currentScore;
            }

            // Check if this opportunity matches a scheduled activity category
            if (scheduleManager.IsScheduledCategory(opp.Type, currentSchedule))
            {
                // Apply schedule boost multiplier
                float boostedScore = currentScore * scheduleManager.ScheduleBoostMultiplier;

                // Mark the opportunity as scheduled for UI display
                opp.IsScheduled = true;

                ModLogger.Debug(LogCategory,
                    $"Schedule boost applied to {opp.Id}: {currentScore:F1} -> {boostedScore:F1}");

                return boostedScore;
            }

            return currentScore;
        }

        private float CalculateWorldStateModifier(CampOpportunity opp, WorldSituation world)
        {
            float mod = 0f;

            // Training fits garrison
            if (opp.Type == OpportunityType.Training && world.LordIs == LordSituation.PeacetimeGarrison)
            {
                mod += 15f;
            }

            // Social is odd during siege
            if (opp.Type == OpportunityType.Social &&
                (world.LordIs == LordSituation.SiegeAttacking || world.LordIs == LordSituation.SiegeDefending))
            {
                mod -= 20f;
            }

            // Recovery valuable during intense periods
            if (opp.Type == OpportunityType.Recovery && world.ExpectedActivity == ActivityLevel.Intense)
            {
                mod += 25f;
            }

            return mod;
        }

        private float CalculateCampContextModifier(CampOpportunity opp, CampContext camp)
        {
            float mod = 0f;

            // Day phase preferences
            if (opp.Type == OpportunityType.Training && camp.DayPhase == DayPhase.Dawn)
            {
                mod += 10f;
            }

            if (opp.Type == OpportunityType.Social && camp.DayPhase == DayPhase.Dusk)
            {
                mod += 15f;
            }

            if (opp.Type == OpportunityType.Economic && camp.DayPhase == DayPhase.Night)
            {
                mod -= 30f;
            }

            // Camp mood effects
            if (camp.CurrentMood == CampMood.Celebration && opp.Type == OpportunityType.Social)
            {
                mod += 20f;
            }

            if (camp.CurrentMood == CampMood.Mourning && opp.Type == OpportunityType.Social)
            {
                mod -= 15f;
            }

            if (camp.CurrentMood == CampMood.Tense && opp.Type == OpportunityType.Recovery)
            {
                mod += 10f;
            }

            // Weekly rhythm: near muster = economic focus
            if (camp.DaysSinceLastMuster >= 9 && opp.Type == OpportunityType.Economic)
            {
                mod += 10f;
            }

            return mod;
        }

        private float CalculatePlayerStateModifier(CampOpportunity opp, CampContext camp, PlayerPreferences prefs)
        {
            float mod = 0f;

            // Recovery when injured
            if (opp.Type == OpportunityType.Recovery && camp.PlayerInjured)
            {
                mod += 30f;
            }

            // Economic when poor
            if (opp.Type == OpportunityType.Economic && camp.PlayerGold < 50)
            {
                mod += 20f;
            }

            // Player preference bonus (learned over time)
            if (opp.Type == OpportunityType.Training && prefs.CombatVsSocial > 0.6f)
            {
                mod += 10f;
            }

            if (opp.Type == OpportunityType.Social && prefs.CombatVsSocial < 0.4f)
            {
                mod += 10f;
            }

            // PROMOTION REPUTATION PRESSURE: Boost reputation-gaining opportunities when player needs reputation for promotion
            // Uses cached values calculated once per generation cycle (not per-opportunity)
            if (_cachedNeedsReputation && _cachedReputationGap > 0)
            {
                // Identify opportunities that grant soldier reputation
                bool grantsReputation = IsReputationGrantingOpportunity(opp.TargetDecisionId);

                if (grantsReputation)
                {
                    // Scale boost based on reputation gap
                    // Small gap (1-5): +15 fitness
                    // Medium gap (6-15): +25 fitness
                    // Large gap (16+): +35 fitness
                    float repBoost = 15f;
                    if (_cachedReputationGap >= 16)
                    {
                        repBoost = 35f;
                    }
                    else if (_cachedReputationGap >= 6)
                    {
                        repBoost = 25f;
                    }

                    // Apply PHASE-AWARE boosting: boost MORE for phase-appropriate activities
                    // This respects the existing schedule system (training at dawn, social at dusk)
                    repBoost = ApplyPromotionPhaseBoost(opp, camp.DayPhase, repBoost);

                    mod += repBoost;
                    ModLogger.Debug(LogCategory,
                        $"Promotion reputation boost: +{repBoost:F0} for {opp.Id} (target: {opp.TargetDecisionId}, gap: {_cachedReputationGap}, phase: {camp.DayPhase})");
                }
            }

            return mod;
        }

        /// <summary>
        /// Applies phase-aware boosting to promotion reputation fitness.
        /// Boosts opportunities that are phase-appropriate higher, respecting the camp schedule.
        /// </summary>
        private float ApplyPromotionPhaseBoost(CampOpportunity opp, DayPhase phase, float baseBoost)
        {
            // Phase-appropriate activities get a 40% bonus to the rep boost
            // This ensures we boost activities that fit the schedule (training at dawn, social at dusk)

            switch (phase)
            {
                case DayPhase.Dawn:
                    // Dawn favors training activities
                    if (opp.Type == OpportunityType.Training)
                    {
                        return baseBoost * 1.4f;
                    }
                    // Helping wounded/mentoring also fits dawn routine
                    if (opp.TargetDecisionId == "dec_help_wounded" ||
                        opp.TargetDecisionId == "dec_mentor_recruit")
                    {
                        return baseBoost * 1.3f;
                    }
                    break;

                case DayPhase.Midday:
                    // Midday favors training and work details
                    if (opp.Type == OpportunityType.Training)
                    {
                        return baseBoost * 1.3f;
                    }
                    if (opp.TargetDecisionId == "dec_help_wounded" ||
                        opp.TargetDecisionId == "dec_volunteer_extra")
                    {
                        return baseBoost * 1.2f;
                    }
                    break;

                case DayPhase.Dusk:
                    // Dusk is prime social time - boost social opportunities significantly
                    if (opp.Type == OpportunityType.Social)
                    {
                        return baseBoost * 1.5f;
                    }
                    // Economic/gambling also fits dusk schedule
                    if (opp.Type == OpportunityType.Economic)
                    {
                        return baseBoost * 1.2f;
                    }
                    break;

                case DayPhase.Night:
                    // Night has limited opportunities - modest boost for quiet social
                    if (opp.TargetDecisionId == "dec_social_stories" ||
                        opp.TargetDecisionId == "dec_social_storytelling")
                    {
                        return baseBoost * 1.2f;
                    }
                    // Cards/dice can happen at night around fires
                    if (opp.TargetDecisionId == "dec_gamble_cards" ||
                        opp.TargetDecisionId == "dec_gamble_dice")
                    {
                        return baseBoost * 1.1f;
                    }
                    break;
            }

            // Default: no phase bonus
            return baseBoost;
        }

        /// <summary>
        /// Checks if an opportunity grants lord reputation by examining its target decision.
        /// These opportunities help players gain reputation needed for promotions.
        /// Note: After Phase 3, all decisions grant lordReputation instead of separate officer/soldier rep.
        /// </summary>
        private bool IsReputationGrantingOpportunity(string targetDecisionId)
        {
            if (string.IsNullOrEmpty(targetDecisionId))
            {
                return false;
            }

            // Training decisions: grant lordReputation on success
            if (targetDecisionId.StartsWith("dec_training_"))
            {
                return true;
            }

            // Social decisions: grant lordReputation
            if (targetDecisionId == "dec_social_stories" ||
                targetDecisionId == "dec_social_storytelling" ||
                targetDecisionId == "dec_social_singing" ||
                targetDecisionId == "dec_tavern_drink" ||
                targetDecisionId == "dec_arm_wrestling" ||
                targetDecisionId == "dec_drinking_contest")
            {
                return true;
            }

            // Helping/mentoring decisions: grant lordReputation
            if (targetDecisionId == "dec_help_wounded" ||
                targetDecisionId == "dec_mentor_recruit")
            {
                return true;
            }

            // Special decisions
            if (targetDecisionId == "dec_gamble_cards" ||
                targetDecisionId == "dec_gamble_high")
            {
                return true;
            }

            return false;
        }

        private float CalculateHistoryModifier(CampOpportunity opp, OpportunityHistory history)
        {
            float mod = 0f;
            string typeKey = opp.Type.ToString();

            // Don't repeat too soon
            float hoursSince = history.HoursSincePresented(typeKey);
            if (hoursSince < 12f)
            {
                mod -= 40f;
            }
            else if (hoursSince < 24f)
            {
                mod -= 20f;
            }

            // Learning system: apply learned preference modifier with 70/30 split
            // 70% learned preference, 30% variety (built into GetLearningModifier)
            float learningMod = PlayerBehaviorTracker.GetLearningModifier(typeKey);
            mod += learningMod;

            if (Math.Abs(learningMod) > 0.1f)
            {
                ModLogger.Debug(LogCategory, $"Learning modifier for {typeKey}: {learningMod:+0.0;-0.0;0}");
            }

            // Novelty bonus for unseen types (maintains the 30% variety)
            if (!history.TimesSeen.ContainsKey(typeKey))
            {
                mod += 8f; // Slight bonus to encourage trying new types
            }

            // If a type has been ignored many times, occasionally still show it (variety rule)
            // This prevents the system from completely hiding ignored types
            float engagementRate = PlayerBehaviorTracker.GetOpportunityEngagementRate(typeKey);
            if (engagementRate < 0.3f)
            {
                // Apply random variety chance: 30% of the time, reduce the penalty
                float currentHour = (float)CampaignTime.Now.ToHours;
                int variationSeed = (int)(currentHour / 6) + opp.Id.GetHashCode();
                if (variationSeed % 10 < 3) // 30% variety window
                {
                    mod += 5f; // Reduce penalty slightly to maintain variety
                }
            }

            return mod;
        }

        /// <summary>
        /// Selects the top N opportunities by fitness score.
        /// </summary>
        private List<CampOpportunity> SelectTopN(List<CampOpportunity> candidates, int n)
        {
            return candidates
                .Where(c => c.FitnessScore >= FitnessThreshold)
                .OrderByDescending(c => c.FitnessScore)
                .Take(n)
                .ToList();
        }

        /// <summary>
        /// Records that the player engaged with an opportunity.
        /// Invalidates the cache so the opportunity immediately disappears from the menu.
        /// </summary>
        public void RecordEngagement(string opportunityId, OpportunityType type)
        {
            string typeKey = type.ToString();
            _history.RecordEngaged(opportunityId, typeKey);

            // Track for learning system
            PlayerBehaviorTracker.RecordOpportunityEngagement(typeKey, true);
            PlayerBehaviorTracker.RecordChoice(typeKey.ToLower());

            // Invalidate cache to immediately remove the completed opportunity from the menu
            InvalidateCache();

            ModLogger.Info(LogCategory, $"Recorded engagement: {opportunityId} (type={typeKey}), cache invalidated to remove from menu");
        }

        /// <summary>
        /// Records that the player ignored an opportunity (menu closed without engaging).
        /// </summary>
        public void RecordIgnored(string opportunityId, OpportunityType type)
        {
            string typeKey = type.ToString();
            _history.RecordIgnored(opportunityId, typeKey);

            // Track for learning system (presented but not engaged)
            PlayerBehaviorTracker.RecordOpportunityEngagement(typeKey, false);

            ModLogger.Debug(LogCategory, $"Recorded ignored: {opportunityId} ({typeKey})");
        }

        /// <summary>
        /// Performs detection check for risky opportunities while player is on duty.
        /// Returns true if player gets away with it, false if caught.
        /// Applies consequences automatically if caught.
        /// </summary>
        public bool AttemptRiskyOpportunity(CampOpportunity opportunity)
        {
            if (opportunity?.Detection == null)
            {
                return true; // No detection settings = always succeeds
            }

            var context = AnalyzeCampContext();
            if (!context.PlayerOnDuty)
            {
                return true; // Not on duty = no risk
            }

            // Calculate detection chance
            float chance = opportunity.Detection.BaseChance;

            // Night modifier (harder to detect at night)
            if (context.DayPhase == DayPhase.Night)
            {
                chance += opportunity.Detection.NightModifier;
            }

            // Lord reputation modifier (trusted soldiers get away with more - now uses native relation)
            var enlistment = EnlistmentBehavior.Instance;
            var reputation = enlistment?.EnlistedLord != null
                ? CharacterRelationManager.GetHeroRelation(Hero.MainHero, enlistment.EnlistedLord)
                : 0;
            if (reputation > 30)
            {
                chance += opportunity.Detection.HighRepModifier;
            }

            // Clamp to valid range
            chance = Math.Max(0f, Math.Min(1f, chance));

            ModLogger.Debug(LogCategory, $"Detection check for {opportunity.Id}: {chance * 100:F0}% chance");

            // Roll for detection
            bool caught = MBRandom.RandomFloat < chance;

            if (caught)
            {
                ApplyCaughtConsequences(opportunity);
                ModLogger.Info(LogCategory, $"Player caught doing {opportunity.Id} while on duty!");
                return false;
            }

            ModLogger.Debug(LogCategory, $"Player got away with {opportunity.Id}");
            return true;
        }

        /// <summary>
        /// Applies consequences when player is caught doing risky activity while on duty.
        /// </summary>
        private void ApplyCaughtConsequences(CampOpportunity opportunity)
        {
            var consequences = opportunity.CaughtConsequences;
            if (consequences == null)
            {
                return;
            }

            var escalation = EscalationManager.Instance;
            if (escalation != null)
            {
                // Apply lord reputation penalty (native system)
                if (consequences.OfficerRep != 0)
                {
                    escalation.ModifyLordReputation(consequences.OfficerRep, "Caught away from post");
                    ModLogger.Debug(LogCategory, $"Lord rep penalty: {consequences.OfficerRep}");
                }

                // Apply scrutiny increase (discipline merged into scrutiny)
                if (consequences.Discipline != 0)
                {
                    escalation.ModifyScrutiny(consequences.Discipline * 10, "Absent from duty"); // Scale to 0-100
                    ModLogger.Debug(LogCategory, $"Scrutiny increase: {consequences.Discipline * 10}");
                }
            }

            // Check for order failure - for now just add extra discipline penalty
            // Future: integrate with order progression system when order completion tracking is added
            if (consequences.OrderFailureRisk > 0)
            {
                bool orderImpacted = MBRandom.RandomFloat < consequences.OrderFailureRisk;
                if (orderImpacted && escalation != null)
                {
                    if (OrderDisplayHelper.IsOrderActive())
                    {
                        ModLogger.Info(LogCategory, "Active order impacted by absence - extra scrutiny penalty");
                        // Add extra scrutiny as consequence for potentially failing the order
                        escalation.ModifyScrutiny(10, "Order compromised by absence"); // Scale to 0-100
                    }
                }
            }

            // Show notification to player
            var title = new TextObject("{=opp_caught_title}Dereliction of Duty");
            var message = new TextObject("{=opp_caught_notification}Your absence was noticed. The {NCO_TITLE} will hear of this.");

            // Set NCO title based on culture
            var cultureId = EnlistmentBehavior.Instance?.EnlistedLord?.Culture?.StringId ?? "empire";
            var ncoTitle = RankHelper.GetNCOTitle(cultureId);
            _ = message.SetTextVariable("NCO_TITLE", ncoTitle);

            // Intentional bypass of StoryDirector — fires on detection check; self-paced by
            // the underlying detection probability and scrutiny state. Observational-modal
            // routing is a followup.
            InformationManager.ShowInquiry(
                new InquiryData(
                    title.ToString(),
                    message.ToString(),
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: false,
                    affirmativeText: new TextObject("{=str_understood}Understood").ToString(),
                    negativeText: null,
                    affirmativeAction: null,
                    negativeAction: null
                ), true);
        }

        /// <summary>
        /// Commits the player to a scheduled activity (legacy method).
        /// Use CommitToOpportunity instead for the new scheduling system.
        /// </summary>
        [Obsolete("Use CommitToOpportunity instead for the new scheduling system")]
        public void CommitToActivity(string activityId, float hoursFromNow, string displayText)
        {
#pragma warning disable CS0618 // Intentional: legacy compatibility method
            _commitments.CommitTo(activityId, hoursFromNow, displayText);
#pragma warning restore CS0618
            ModLogger.Info(LogCategory, $"Player committed to {activityId} in {hoursFromNow:F1} hours");
        }

        /// <summary>
        /// Gets contextually appropriate empty state message.
        /// </summary>
        public string GetEmptyStateMessage(CampContext context)
        {
            if (context.PlayerOnDuty)
            {
                return new TextObject("{=camp_empty_duty}You're on duty.").ToString();
            }

            if (context.LordSituation == LordSituation.WarMarching)
            {
                return new TextObject("{=camp_empty_march}The army is on the march. No time for leisure.").ToString();
            }

            if (context.LordSituation == LordSituation.SiegeAttacking || context.LordSituation == LordSituation.SiegeDefending)
            {
                return new TextObject("{=camp_empty_siege}The siege consumes all attention.").ToString();
            }

            if (context.IsNewEnlistmentGrace)
            {
                return new TextObject("{=camp_empty_new}You're still finding your place. The camp will open up once you've settled in.").ToString();
            }

            return new TextObject("{=camp_empty_quiet}A quiet moment in camp. Rest while you can.").ToString();
        }

        #region Helper Methods

        private CampMood DeriveCampMood(CompanySimulationBehavior simulation)
        {
            if (simulation == null)
            {
                return CampMood.Routine;
            }

            var needs = EnlistmentBehavior.Instance?.CompanyNeeds;

            // Recent deaths = mourning
            if (simulation.Pressure.RecentDesertions > 2 || simulation.Roster?.CasualtyRate > 0.15f)
            {
                return CampMood.Mourning;
            }

            // Low readiness or supplies = tense
            if (needs != null && (needs.Readiness < 30 || needs.Supplies < 30))
            {
                return CampMood.Tense;
            }

            // High readiness = celebration
            if (needs != null && needs.Readiness > 70)
            {
                return CampMood.Celebration;
            }

            return CampMood.Routine;
        }

        private bool IsPlayerOnDuty()
        {
            // TODO: Check OrderProgressionBehavior for active order
            // For now, return false - will be integrated with Order system
            return false;
        }

        private string GetCurrentOrderType()
        {
            // TODO: Get from OrderProgressionBehavior
            return "";
        }

        private bool IsBaggageWindowActive(EnlistmentBehavior enlistment)
        {
            if (enlistment == null)
            {
                return false;
            }

            // Baggage window is 6 hours after muster
            // TODO: Integrate with muster timing
            return false;
        }

        private CampOpportunity CloneOpportunity(CampOpportunity source)
        {
            return new CampOpportunity
            {
                Id = source.Id,
                Type = source.Type,
                TitleId = source.TitleId,
                TitleFallback = source.TitleFallback,
                DescriptionId = source.DescriptionId,
                DescriptionFallback = source.DescriptionFallback,
                ActionId = source.ActionId,
                ActionFallback = source.ActionFallback,
                TargetDecisionId = source.TargetDecisionId,
                MinTier = source.MinTier,
                MaxTier = source.MaxTier,
                CooldownHours = source.CooldownHours,
                BaseFitness = source.BaseFitness,
                ValidPhases = source.ValidPhases,
                OrderCompatibility = source.OrderCompatibility,
                Detection = source.Detection,
                CaughtConsequences = source.CaughtConsequences,
                TooltipRiskyId = source.TooltipRiskyId,
                TooltipRiskyFallback = source.TooltipRiskyFallback,
                RequiredFlags = source.RequiredFlags,
                BlockedByFlags = source.BlockedByFlags,
                ScheduledTime = source.ScheduledTime,
                ConditionStates = source.ConditionStates,
                MedicalPressure = source.MedicalPressure,
                SuppressWhenTreated = source.SuppressWhenTreated,
                ScheduledPhase = source.ScheduledPhase,
                Immediate = source.Immediate,
                NotAtSea = source.NotAtSea,
                AtSea = source.AtSea
            };
        }

        #endregion

        #region JSON Loading

        private void LoadOpportunityDefinitions()
        {
            // Prevent double-loading (OnSessionLaunched + lazy init may both call this)
            if (_definitionsLoaded)
            {
                return;
            }

            try
            {
                var configPath = Path.Combine(ModulePaths.GetContentPath("Decisions"), "camp_opportunities.json");
                if (!File.Exists(configPath))
                {
                    ModLogger.Surfaced("CAMPLIFE",
                        "camp_opportunities.json not found — decisions won't appear in menu. Verify mod installation is complete.");
                    _opportunityDefinitions = new List<CampOpportunity>();
                    _definitionsLoaded = true;
                    return;
                }

                var json = File.ReadAllText(configPath);
                var jObject = JObject.Parse(json);
                var opportunities = jObject["opportunities"] as JArray;

                if (opportunities == null)
                {
                    ModLogger.Surfaced("CAMPLIFE",
                        "No opportunities array found in camp_opportunities.json — file may be corrupt or invalid. Decisions won't appear in menu.");
                    _definitionsLoaded = true;
                    return;
                }

                _opportunityDefinitions = new List<CampOpportunity>();

                foreach (var item in opportunities)
                {
                    var opp = ParseOpportunity(item);
                    if (opp != null)
                    {
                        _opportunityDefinitions.Add(opp);
                    }
                }

                _definitionsLoaded = true;
                ModLogger.Info(LogCategory, $"Loaded {_opportunityDefinitions.Count} opportunity definitions");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMPLIFE",
                    "Failed to parse camp_opportunities.json — decisions won't appear in menu. Check JSON syntax.", ex);
                _opportunityDefinitions = new List<CampOpportunity>();
                _definitionsLoaded = true; // Mark as loaded even on error to prevent retry loops
            }
        }

        private CampOpportunity ParseOpportunity(JToken item)
        {
            try
            {
                var opp = new CampOpportunity
                {
                    Id = item["id"]?.Value<string>(),
                    Type = ParseOpportunityType(item["type"]?.Value<string>()),
                    TitleId = item["titleId"]?.Value<string>(),
                    TitleFallback = item["title"]?.Value<string>(),
                    DescriptionId = item["descriptionId"]?.Value<string>(),
                    DescriptionFallback = item["description"]?.Value<string>(),
                    HintId = item["hintId"]?.Value<string>(),
                    HintFallback = item["hint"]?.Value<string>(),
                    ActionId = item["actionId"]?.Value<string>(),
                    ActionFallback = item["action"]?.Value<string>(),
                    TargetDecisionId = item["targetDecision"]?.Value<string>(),
                    MinTier = item["minTier"]?.Value<int>() ?? 1,
                    MaxTier = item["maxTier"]?.Value<int>() ?? 0,
                    CooldownHours = item["cooldownHours"]?.Value<int>() ?? 12,
                    BaseFitness = item["baseFitness"]?.Value<int>() ?? 50,
                    ScheduledTime = item["scheduledTime"]?.Value<string>(),
                    ScheduledPhase = item["scheduledPhase"]?.Value<string>(),
                    Immediate = item["immediate"]?.Value<bool>() ?? false,
                    NotAtSea = item["notAtSea"]?.Value<bool>() ?? false,
                    AtSea = item["atSea"]?.Value<bool>() ?? false
                };

                // Valid phases
                var phases = item["validPhases"] as JArray;
                if (phases != null)
                {
                    opp.ValidPhases = phases.Select(p => p.Value<string>()).ToList();
                }

                // Order compatibility
                var orderCompat = item["orderCompatibility"] as JObject;
                if (orderCompat != null)
                {
                    opp.OrderCompatibility = new Dictionary<string, string>();
                    foreach (var prop in orderCompat.Properties())
                    {
                        opp.OrderCompatibility[prop.Name] = prop.Value.Value<string>();
                    }
                }

                // Detection settings
                var detection = item["detection"] as JObject;
                if (detection != null)
                {
                    opp.Detection = new DetectionSettings
                    {
                        BaseChance = detection["baseChance"]?.Value<float>() ?? 0.25f,
                        NightModifier = detection["nightModifier"]?.Value<float>() ?? -0.15f,
                        HighRepModifier = detection["highRepModifier"]?.Value<float>() ?? -0.10f
                    };
                }

                // Caught consequences (after Phase 3: uses lordReputation and scrutiny)
                var caught = item["caughtConsequences"] as JObject;
                if (caught != null)
                {
                    opp.CaughtConsequences = new CaughtConsequences
                    {
                        OfficerRep = caught["lordReputation"]?.Value<int>() ?? caught["officerRep"]?.Value<int>() ?? -15,
                        Discipline = caught["scrutiny"]?.Value<int>() ?? caught["discipline"]?.Value<int>() ?? 20, // Scrutiny 0-100 scale
                        OrderFailureRisk = caught["orderFailureRisk"]?.Value<float>() ?? 0.20f
                    };
                }

                opp.TooltipRiskyId = item["tooltipRiskyId"]?.Value<string>();
                opp.TooltipRiskyFallback = item["tooltipRisky"]?.Value<string>();

                // Required/blocked flags
                var requiredFlags = item["requiredFlags"] as JArray;
                if (requiredFlags != null)
                {
                    opp.RequiredFlags = requiredFlags.Select(f => f.Value<string>()).ToList();
                }

                var blockedFlags = item["blockedByFlags"] as JArray;
                if (blockedFlags != null)
                {
                    opp.BlockedByFlags = blockedFlags.Select(f => f.Value<string>()).ToList();
                }

                // Requirements (from requirements object)
                var requirements = item["requirements"] as JObject;
                if (requirements != null)
                {
                    var conditionStates = requirements["conditionStates"] as JArray;
                    if (conditionStates != null)
                    {
                        opp.ConditionStates = conditionStates.Select(c => c.Value<string>()).ToList();
                    }

                    var medicalPressure = requirements["medicalPressure"] as JArray;
                    if (medicalPressure != null)
                    {
                        opp.MedicalPressure = medicalPressure.Select(m => m.Value<string>()).ToList();
                    }
                }

                // Suppress when treated flag
                opp.SuppressWhenTreated = item["suppressWhenTreated"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(opp.Id))
                {
                    return null;
                }

                return opp;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CAMPLIFE", "Failed to parse opportunity", ex);
                return null;
            }
        }

        private OpportunityType ParseOpportunityType(string type)
        {
            return type?.ToLower() switch
            {
                "training" => OpportunityType.Training,
                "social" => OpportunityType.Social,
                "economic" => OpportunityType.Economic,
                "recovery" => OpportunityType.Recovery,
                "special" => OpportunityType.Special,
                _ => OpportunityType.Social
            };
        }

        /// <summary>
        /// Checks if the player meets all required condition states for an opportunity.
        /// Returns true if all requirements are met, false otherwise.
        /// </summary>
        private static bool MeetsConditionStateRequirements(List<string> conditionStates)
        {
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                // If condition system is disabled, treat as not meeting condition requirements
                return false;
            }

            var state = conditions.State;
            if (state == null)
            {
                return false;
            }

            foreach (var requirement in conditionStates)
            {
                switch (requirement)
                {
                    case "HasAnyCondition":
                    case "HasCondition":
                        // Both variants mean the same thing: player has any active condition
                        if (!state.HasAnyCondition)
                        {
                            return false;
                        }
                        break;

                    case "HasSevereCondition":
                        // Check for severe or critical injury/illness that is ACTIVE (days remaining > 0)
                        var hasSevereInjury = state.CurrentInjury >= InjurySeverity.Severe && state.InjuryDaysRemaining > 0;
                        var hasSevereIllness = state.CurrentIllness >= IllnessSeverity.Severe && state.IllnessDaysRemaining > 0;
                        if (!hasSevereInjury && !hasSevereIllness)
                        {
                            return false;
                        }
                        break;

                    case "HasInjury":
                        if (!state.HasInjury)
                        {
                            return false;
                        }
                        break;

                    case "HasIllness":
                        if (!state.HasIllness)
                        {
                            return false;
                        }
                        break;

                    default:
                        // Unknown requirement type - fail safe by rejecting
                        ModLogger.Warn(LogCategory, $"Unknown condition state requirement: {requirement}");
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the player's medical risk level meets the required pressure levels.
        /// Returns true if medical risk matches any of the specified pressure levels.
        /// </summary>
        private static bool MeetsMedicalPressureRequirements(List<string> pressureLevels)
        {
            var escalation = EscalationManager.Instance;
            if (escalation?.State == null)
            {
                return false;
            }

            var medicalRisk = escalation.State.MedicalRisk;

            // Map medical risk (0-5) to pressure levels
            // 0-1: None/Low, 2: Moderate, 3: High, 4-5: Critical
            foreach (var level in pressureLevels)
            {
                switch (level)
                {
                    case "Moderate":
                        if (medicalRisk >= 2)
                        {
                            return true;
                        }
                        break;

                    case "High":
                        if (medicalRisk >= 3)
                        {
                            return true;
                        }
                        break;

                    case "Critical":
                        if (medicalRisk >= 4)
                        {
                            return true;
                        }
                        break;

                    case "Low":
                        if (medicalRisk >= 1)
                        {
                            return true;
                        }
                        break;

                    default:
                        ModLogger.Warn(LogCategory, $"Unknown medical pressure level: {level}");
                        break;
                }
            }

            return false;
        }

        #endregion
    }
}
