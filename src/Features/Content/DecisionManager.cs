using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Triggers;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Manages decision availability, cooldowns, and gate checking.
    /// Provides filtered lists of available decisions for the menu system.
    /// </summary>
    public class DecisionManager : CampaignBehaviorBase
    {
        private const string LogCategory = "DecisionManager";

        public static DecisionManager Instance { get; private set; }

        /// <summary>
        /// Tracks the decision ID that is currently being shown to prevent spam-clicking.
        /// Cleared when the event popup closes.
        /// </summary>
        private string _currentlyShowingDecisionId;

        public override void RegisterEvents()
        {
            Instance = this;

            // Initialize the decision catalog (loads from JSON via EventCatalog)
            DecisionCatalog.Initialize();

            ModLogger.Info(LogCategory, $"DecisionManager registered with {DecisionCatalog.DecisionCount} decisions " +
                $"({DecisionCatalog.PlayerInitiatedCount} player-initiated, {DecisionCatalog.AutomaticCount} automatic)");
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Decision cooldowns are stored in EscalationState via EventLastFired
            // No additional persistence needed here
        }

        /// <summary>
        /// Gets all player-initiated decisions for a specific menu section that are currently available.
        /// Filters by tier, time of day, cooldown, and other gates.
        /// </summary>
        public IReadOnlyList<DecisionAvailability> GetAvailableDecisionsForSection(string section)
        {
            var decisions = DecisionCatalog.GetDecisionsBySection(section);
            var result = new List<DecisionAvailability>();

            foreach (var decision in decisions)
            {
                var availability = CheckAvailability(decision);
                // Include all decisions so menu can show disabled ones with reason
                result.Add(availability);
            }

            return result;
        }

        // NOTE: GetAvailableOpportunities() has been removed.
        // Use ContentOrchestrator.GetCurrentPhaseOpportunities() instead.
        // The Orchestrator pre-schedules opportunities 24 hours ahead to prevent them
        // from disappearing when context changes mid-session.

        /// <summary>
        /// Checks all availability gates for a decision and returns detailed status.
        /// </summary>
        public DecisionAvailability CheckAvailability(DecisionDefinition decision)
        {
            var result = new DecisionAvailability
            {
                Decision = decision,
                IsAvailable = true,
                IsVisible = true,
                UnavailableReason = null
            };

            if (decision == null)
            {
                result.IsAvailable = false;
                result.IsVisible = false;
                ModLogger.Debug(LogCategory, "CheckAvailability: Decision is null");
                return result;
            }

            ModLogger.Debug(LogCategory, $"CheckAvailability: {decision.Id}");

            var enlistment = EnlistmentBehavior.Instance;
            var escalation = EscalationManager.Instance;

            // Gate 0: Check if this decision is currently being shown (prevent spam-clicking)
            if (!string.IsNullOrEmpty(_currentlyShowingDecisionId) &&
                _currentlyShowingDecisionId.Equals(decision.Id, StringComparison.OrdinalIgnoreCase))
            {
                result.IsAvailable = false;
                result.UnavailableReason = "In progress...";
                return result;
            }

            // Gate 1: Must be enlisted
            if (enlistment?.IsEnlisted != true)
            {
                result.IsAvailable = false;
                result.IsVisible = false;
                result.UnavailableReason = "Not enlisted";
                return result;
            }

            // Gate 2: Tier requirement
            var playerTier = enlistment.EnlistmentTier;
            var minTier = decision.Requirements?.MinTier;
            var maxTier = decision.Requirements?.MaxTier;

            if (minTier.HasValue && playerTier < minTier.Value)
            {
                result.IsAvailable = false;
                result.UnavailableReason = $"Requires Tier {minTier.Value}+";
                return result;
            }

            if (maxTier.HasValue && playerTier > maxTier.Value)
            {
                result.IsAvailable = false;
                result.IsVisible = false; // Hide decisions above max tier
                result.UnavailableReason = $"Tier too high (max T{maxTier.Value})";
                return result;
            }

            // Gate 3: Time of day (if specified)
            if (decision.TimeOfDay != null && decision.TimeOfDay.Count > 0)
            {
                var currentTimeBlock = CampaignTriggerTrackerBehavior.Instance?.GetTimeBlock() ?? TimeBlock.Morning;
                var currentTimeStr = TimeBlockToString(currentTimeBlock);

                if (!decision.TimeOfDay.Any(t => t.Equals(currentTimeStr, StringComparison.OrdinalIgnoreCase)))
                {
                    result.IsAvailable = false;
                    result.UnavailableReason = $"Available during {string.Join("/", decision.TimeOfDay)}";
                    return result;
                }
            }

            // Gate 4: Cooldown
            var cooldownDays = decision.Timing?.CooldownDays ?? 0;
            if (cooldownDays > 0 && escalation?.State != null)
            {
                if (escalation.State.IsEventOnCooldown(decision.Id, cooldownDays))
                {
                    _ = escalation.State.EventLastFired.TryGetValue(decision.Id, out var lastFired);
                    var daysSince = (CampaignTime.Now - lastFired).ToDays;
                    var daysRemaining = (int)Math.Ceiling(cooldownDays - daysSince);
                    result.IsAvailable = false;
                    result.UnavailableReason = daysRemaining == 1
                        ? "Available tomorrow"
                        : $"Available in {daysRemaining} days";
                    return result;
                }
            }

            // Gate 5: One-time check
            if (decision.Timing?.OneTime == true && escalation?.State != null)
            {
                if (escalation.State.HasOneTimeEventFired(decision.Id))
                {
                    result.IsAvailable = false;
                    result.IsVisible = false;
                    result.UnavailableReason = "Already completed";
                    return result;
                }
            }

            // Gate 6: Required flags
            if (decision.RequiredFlags != null && decision.RequiredFlags.Count > 0 && escalation?.State != null)
            {
                foreach (var flagName in decision.RequiredFlags)
                {
                    if (!escalation.State.HasFlag(flagName))
                    {
                        result.IsAvailable = false;
                        result.IsVisible = false; // Hide decisions that require missing flags
                        result.UnavailableReason = "Prerequisite not met";
                        return result;
                    }
                }
            }

            // Gate 7: Blocking flags
            if (decision.BlockingFlags != null && decision.BlockingFlags.Count > 0 && escalation?.State != null)
            {
                foreach (var flagName in decision.BlockingFlags)
                {
                    if (escalation.State.HasFlag(flagName))
                    {
                        result.IsAvailable = false;
                        result.IsVisible = false; // Hide decisions blocked by flags
                        result.UnavailableReason = "No longer available";
                        return result;
                    }
                }
            }

            // Gate 8: Sea/land context check
            if (decision.Requirements != null)
            {
                var party = enlistment?.CurrentLord?.PartyBelongedTo;
                // BUGFIX: If party is in a settlement or besieging, they are on land regardless of IsCurrentlyAtSea
                var isAtSea = party != null &&
                              party.CurrentSettlement == null &&
                              party.BesiegedSettlement == null &&
                              party.IsCurrentlyAtSea;

                // Check if decision requires being NOT at sea (land-only)
                if (decision.Requirements.NotAtSea == true && isAtSea)
                {
                    result.IsAvailable = false;
                    result.UnavailableReason = "Not available at sea";
                    return result;
                }

                // Check if decision requires being at sea (maritime-only)
                if (decision.Requirements.AtSea == true && !isAtSea)
                {
                    result.IsAvailable = false;
                    result.UnavailableReason = "Only available at sea";
                    return result;
                }
            }

            // Gate 9: Baggage access decision - only show when baggage is accessible
            if (decision.Id.Equals("dec_baggage_access", StringComparison.OrdinalIgnoreCase))
            {
                // Gate 9a: Block baggage access if bag check is still pending (onboarding not complete)
                if (enlistment?.IsBagCheckPending == true)
                {
                    result.IsAvailable = false;
                    result.UnavailableReason = "Sort out storage arrangements with the quartermaster first";
                    ModLogger.Debug(LogCategory, "Baggage access blocked: bag check pending");
                    return result;
                }

                var baggageManager = Logistics.BaggageTrainManager.Instance;
                if (baggageManager != null)
                {
                    var accessState = baggageManager.GetCurrentAccess();

                    // Only show decision when baggage is actually accessible
                    // (FullAccess or TemporaryAccess, but not NoAccess or Locked)
                    if (accessState == Logistics.BaggageAccessState.NoAccess ||
                        accessState == Logistics.BaggageAccessState.Locked)
                    {
                        result.IsAvailable = false;
                        result.IsVisible = false;
                        result.UnavailableReason = "Baggage not currently accessible";
                        ModLogger.Debug(LogCategory, $"Baggage access blocked: state={accessState}");
                        return result;
                    }

                    ModLogger.Debug(LogCategory, $"Baggage access available: state={accessState}");
                }
            }

            ModLogger.Debug(LogCategory, $"CheckAvailability result: {decision.Id} - Available={result.IsAvailable}, Visible={result.IsVisible}");
            return result;
        }

        /// <summary>
        /// Marks a decision as currently being shown to the player.
        /// This prevents spam-clicking the same decision while its event popup is open.
        /// </summary>
        public void MarkDecisionAsShowing(string decisionId)
        {
            _currentlyShowingDecisionId = decisionId;
            ModLogger.Debug(LogCategory, $"Decision marked as showing: {decisionId}");
        }

        /// <summary>
        /// Clears the currently showing decision mark.
        /// Called when the event popup closes.
        /// </summary>
        public void ClearCurrentlyShowingDecision()
        {
            if (!string.IsNullOrEmpty(_currentlyShowingDecisionId))
            {
                ModLogger.Debug(LogCategory, $"Cleared currently showing decision: {_currentlyShowingDecisionId}");
                _currentlyShowingDecisionId = null;
            }
        }

        /// <summary>
        /// Records that a decision was selected, updating cooldown tracking.
        /// </summary>
        public void RecordDecisionSelected(string decisionId)
        {
            if (string.IsNullOrEmpty(decisionId))
            {
                return;
            }

            var escalation = EscalationManager.Instance;
            if (escalation?.State == null)
            {
                return;
            }

            escalation.State.RecordEventFired(decisionId);

            var decision = DecisionCatalog.GetDecision(decisionId);
            if (decision?.Timing?.OneTime == true)
            {
                escalation.State.RecordOneTimeEventFired(decisionId);
            }

            ModLogger.Info(LogCategory, $"Decision '{decisionId}' selected, cooldown recorded");
        }

        /// <summary>
        /// Gets a summary of available decisions per section for logging.
        /// </summary>
        public Dictionary<string, int> GetAvailableCountsBySection()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var section in new[] { "training", "social", "camp_life", "logistics", "intel" })
            {
                var available = GetAvailableDecisionsForSection(section)
                    .Count(d => d.IsAvailable);
                counts[section] = available;
            }

            // Use Orchestrator for opportunities count (Orchestrator owns the schedule)
            var orchestrator = ContentOrchestrator.Instance;
            counts["opportunities"] = orchestrator?.GetCurrentPhaseOpportunities().Count ?? 0;

            return counts;
        }

        private static string TimeBlockToString(TimeBlock block)
        {
            return block switch
            {
                TimeBlock.Morning => "morning",
                TimeBlock.Afternoon => "afternoon",
                TimeBlock.Dusk => "evening",  // Dusk maps to "evening" in JSON
                TimeBlock.Night => "night",
                _ => "any"
            };
        }
    }

    /// <summary>
    /// Availability status for a single decision.
    /// </summary>
    public class DecisionAvailability
    {
        public DecisionDefinition Decision { get; set; }

        /// <summary>
        /// True if the player can select this decision right now.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// True if this decision should appear in the menu (even if disabled).
        /// False to hide completely (e.g., one-time already done, tier too high).
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Human-readable reason why the decision is unavailable.
        /// Shown as tooltip when disabled.
        /// </summary>
        public string UnavailableReason { get; set; }

        /// <summary>
        /// True if this opportunity is risky (player on duty, could get caught).
        /// </summary>
        public bool IsRisky { get; set; }

        /// <summary>
        /// Tooltip explaining the risk when on duty.
        /// </summary>
        public string RiskyTooltip { get; set; }

        /// <summary>
        /// The source CampOpportunity if this came from the generator.
        /// </summary>
        public CampOpportunity CampOpportunity { get; set; }

        /// <summary>
        /// The scheduled opportunity from the Orchestrator.
        /// Used for the commitment model (scheduling future activities).
        /// </summary>
        public ScheduledOpportunity ScheduledOpportunity { get; set; }
    }
}

