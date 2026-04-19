using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Escalation;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.Core;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Selects narrative events using a weighted random algorithm.
    /// Filters events by requirements and cooldowns, then weights by role and context match.
    /// Role-matching events get 2x weight, context-matching events get 1.5x weight.
    /// </summary>
    public static class EventSelector
    {
        private const string LogCategory = "EventSelector";

        // Weighting multipliers for event selection
        private const float RoleMatchMultiplier = 2.0f;
        private const float ContextMatchMultiplier = 1.5f;
        private const float HighPriorityMultiplier = 1.5f;
        private const float CriticalPriorityMultiplier = 2.0f;

        /// <summary>
        /// Selects an appropriate event based on current player state, context, and cooldowns.
        /// Returns null if no eligible events are available.
        /// </summary>
        /// <param name="worldSituation">Optional world situation for fitness scoring. If null, uses basic weighting only.</param>
        /// <returns>The selected EventDefinition, or null if none available.</returns>
        public static EventDefinition SelectEvent(WorldSituation worldSituation = null)
        {
            try
            {
                var escalationState = EscalationManager.Instance?.State;
                if (escalationState == null)
                {
                    ModLogger.Warn(LogCategory, "Cannot select event - EscalationManager not available");
                    return null;
                }

                // Get all loaded events
                var allEvents = EventCatalog.GetAllEvents();
                if (allEvents == null || allEvents.Count == 0)
                {
                    ModLogger.Debug(LogCategory, "No events loaded in catalog");
                    return null;
                }

                // Get current player state for filtering
                var playerRole = EventRequirementChecker.GetPlayerRole();
                var currentContext = EventRequirementChecker.GetCurrentContext();

                // Filter to eligible candidates
                var candidates = GetEligibleCandidates(allEvents, escalationState);

                if (candidates.Count == 0)
                {
                    ModLogger.Debug(LogCategory, "No eligible events after filtering");
                    return null;
                }

                // Apply weights and select
                var weightedCandidates = ApplyWeights(candidates, playerRole, currentContext, worldSituation);

                var selectedEvent = WeightedRandomSelect(weightedCandidates);

                if (selectedEvent != null)
                {
                    var selectedWeight = weightedCandidates.FirstOrDefault(w => w.Event == selectedEvent)?.Weight ?? 1f;
                    ModLogger.Info(LogCategory,
                        $"Selected {selectedEvent.Id} (weight: {selectedWeight:F1}, role: {selectedEvent.Requirements.Role}, context: {selectedEvent.Requirements.Context})");
                }

                return selectedEvent;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EventSelector", "Error selecting event", ex);
                return null;
            }
        }

        /// <summary>
        /// Filters events to those meeting all requirements and not on cooldown.
        /// Excludes special-purpose events that should never be randomly selected:
        /// - "decision" category: Handled by DecisionManager (Camp Hub menu)
        /// - "onboarding" category: Triggered explicitly during enlistment (e.g., baggage stowage)
        /// - Specific muster events: Fire as muster menu stages, not random events
        /// </summary>
        private static List<EventDefinition> GetEligibleCandidates(
            IReadOnlyList<EventDefinition> allEvents,
            EscalationState escalationState)
        {
            var candidates = new List<EventDefinition>();

            foreach (var evt in allEvents)
            {
                // Skip decision events - these are handled by DecisionManager, not the event pacing system
                if (!string.IsNullOrEmpty(evt.Category) &&
                    evt.Category.Equals("decision", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Skip onboarding events - these are triggered explicitly during enlistment, not via random selection
                // Example: evt_baggage_stowage_first_enlistment fires 1 hour after first enlistment only
                if (!string.IsNullOrEmpty(evt.Category) &&
                    evt.Category.Equals("onboarding", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Skip muster-specific events (recruit) - these fire as muster menu stages, not random camp events
                if (!string.IsNullOrEmpty(evt.Id) &&
                    evt.Id.Equals("evt_muster_new_recruit", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Skip one-time events that have already fired
                if (evt.Timing.OneTime && escalationState.HasOneTimeEventFired(evt.Id))
                {
                    continue;
                }

                // Skip events on cooldown
                if (escalationState.IsEventOnCooldown(evt.Id, evt.Timing.CooldownDays))
                {
                    continue;
                }

                // Check all requirements
                if (!EventRequirementChecker.MeetsRequirements(evt.Requirements))
                {
                    continue;
                }

                // Check trigger conditions (flags, contexts, etc.)
                if (!CheckTriggerConditions(evt))
                {
                    continue;
                }

                candidates.Add(evt);
            }

            ModLogger.Debug(LogCategory, $"Filtered to {candidates.Count} eligible candidates from {allEvents.Count} total events");
            return candidates;
        }

        /// <summary>
        /// Checks if all trigger conditions for an event are satisfied.
        /// Validates flag requirements, context requirements, and other trigger-based conditions.
        /// </summary>
        private static bool CheckTriggerConditions(EventDefinition evt)
        {
            if (evt.TriggersAll == null || evt.TriggersAll.Count == 0)
            {
                return true; // No trigger conditions = always eligible
            }

            // Check each trigger condition in the "all" array
            foreach (var trigger in evt.TriggersAll)
            {
                if (string.IsNullOrWhiteSpace(trigger))
                {
                    continue;
                }

                // Skip generic conditions that are handled by other systems
                if (trigger.Equals("is_enlisted", StringComparison.OrdinalIgnoreCase))
                {
                    // Already checked by event system context
                    continue;
                }

                // Check flag conditions and other trigger types
                if (!EventRequirementChecker.CheckTriggerCondition(trigger))
                {
                    return false; // At least one condition failed
                }
            }

            return true; // All conditions passed
        }

        /// <summary>
        /// Applies weight multipliers based on role match, context match, priority, and fitness scoring.
        /// </summary>
        private static List<WeightedEvent> ApplyWeights(
            List<EventDefinition> candidates,
            string playerRole,
            string currentContext,
            WorldSituation worldSituation)
        {
            var weighted = new List<WeightedEvent>();

            foreach (var evt in candidates)
            {
                var weight = 1.0f;

                // Role match bonus: events targeting player's current role get higher weight
                if (!string.IsNullOrEmpty(evt.Requirements.Role) &&
                    !evt.Requirements.Role.Equals("Any", StringComparison.OrdinalIgnoreCase) &&
                    evt.Requirements.Role.Equals(playerRole, StringComparison.OrdinalIgnoreCase))
                {
                    weight *= RoleMatchMultiplier;
                }

                // Context match bonus: events matching current campaign context get higher weight
                if (!string.IsNullOrEmpty(evt.Requirements.Context) &&
                    !evt.Requirements.Context.Equals("Any", StringComparison.OrdinalIgnoreCase) &&
                    evt.Requirements.Context.Equals(currentContext, StringComparison.OrdinalIgnoreCase))
                {
                    weight *= ContextMatchMultiplier;
                }

                // Priority bonus
                weight *= GetPriorityMultiplier(evt.Timing.Priority);

                // Fitness scoring (if world situation provided)
                if (worldSituation != null)
                {
                    var fitnessScore = CalculateFitnessScore(evt, worldSituation);
                    weight *= fitnessScore;
                }

                weighted.Add(new WeightedEvent { Event = evt, Weight = weight });
            }

            return weighted;
        }

        /// <summary>
        /// Calculates fitness score for an event based on world situation and player preferences.
        /// Returns a multiplier (0.5 - 2.0) indicating how well the event fits current context.
        /// </summary>
        private static float CalculateFitnessScore(EventDefinition evt, WorldSituation situation)
        {
            var fitness = 1.0f;

            // World state fitness: does this event make sense RIGHT NOW?
            var activityFitness = CalculateActivityFitness(evt, situation);
            fitness *= activityFitness;

            // Player preference fitness: does player engage with this type of content?
            var preferenceFitness = CalculatePreferenceFitness(evt);
            fitness *= preferenceFitness;

            return fitness;
        }

        /// <summary>
        /// Calculates how well event fits current activity level.
        /// Quiet garrison = prefer low-key events, intense siege = prefer high-stakes events.
        /// </summary>
        private static float CalculateActivityFitness(EventDefinition evt, WorldSituation situation)
        {
            // Events can have tags indicating their intensity/tone
            // For now, use priority as a proxy: high priority = high stakes
            var eventIntensity = evt.Timing.Priority?.ToLowerInvariant() switch
            {
                "critical" => 2.0f,  // High stakes event
                "high" => 1.5f,
                "low" => 0.5f,       // Low stakes event
                _ => 1.0f            // Normal
            };

            // Match event intensity to situation activity
            var activityMultiplier = situation.ExpectedActivity switch
            {
                ActivityLevel.Quiet => eventIntensity < 1.0f ? 1.5f : 0.7f,      // Prefer low-key in quiet times
                ActivityLevel.Routine => 1.0f,                                    // Neutral
                ActivityLevel.Active => eventIntensity > 1.0f ? 1.3f : 0.9f,     // Prefer higher stakes
                ActivityLevel.Intense => eventIntensity >= 1.5f ? 1.5f : 0.8f,   // Strongly prefer high stakes
                _ => 1.0f
            };

            return activityMultiplier;
        }

        /// <summary>
        /// Calculates how well event matches player's demonstrated preferences.
        /// Uses PlayerBehaviorTracker to learn what content player engages with.
        /// </summary>
        private static float CalculatePreferenceFitness(EventDefinition evt)
        {
            var preferences = PlayerBehaviorTracker.GetPreferences();

            // If no choices recorded yet, return neutral
            if (preferences.TotalChoicesMade < 5)
            {
                return 1.0f;
            }

            var fitness = 1.0f;

            // Use event ID and category to infer content type
            // Combat events typically have "combat", "battle", "fight" in ID
            // Social events have "social", "friend", "conversation" in ID
            var eventIdLower = evt.Id?.ToLowerInvariant() ?? "";
            var categoryLower = evt.Category?.ToLowerInvariant() ?? "";

            // Combat vs Social preference
            if (eventIdLower.Contains("combat") || eventIdLower.Contains("battle") || eventIdLower.Contains("fight"))
            {
                if (preferences.CombatVsSocial > 0.6f)
                    fitness *= 1.3f;  // Player likes combat content
            }
            else if (eventIdLower.Contains("social") || eventIdLower.Contains("friend") || eventIdLower.Contains("conversation"))
            {
                if (preferences.CombatVsSocial < 0.4f)
                    fitness *= 1.3f;  // Player likes social content
            }

            // Risky vs Safe preference (high priority = risky, low priority = safe)
            var priorityLower = evt.Timing.Priority?.ToLowerInvariant() ?? "normal";
            if (priorityLower == "critical" || priorityLower == "high")
            {
                if (preferences.RiskyVsSafe > 0.6f)
                    fitness *= 1.2f;  // Player likes risky content
            }
            else if (priorityLower == "low")
            {
                if (preferences.RiskyVsSafe < 0.4f)
                    fitness *= 1.2f;  // Player likes safe content
            }

            return fitness;
        }

        /// <summary>
        /// Gets the weight multiplier for an event's priority level.
        /// </summary>
        private static float GetPriorityMultiplier(string priority)
        {
            return priority?.ToLowerInvariant() switch
            {
                "high" => HighPriorityMultiplier,
                "critical" => CriticalPriorityMultiplier,
                "low" => 0.5f,
                _ => 1.0f // "normal" or unspecified
            };
        }

        /// <summary>
        /// Performs weighted random selection from the list of candidates.
        /// Events with higher weights have proportionally higher chance of being selected.
        /// </summary>
        private static EventDefinition WeightedRandomSelect(List<WeightedEvent> weightedEvents)
        {
            if (weightedEvents == null || weightedEvents.Count == 0)
            {
                return null;
            }

            // Calculate total weight
            var totalWeight = weightedEvents.Sum(w => w.Weight);
            if (totalWeight <= 0)
            {
                return weightedEvents[0].Event;
            }

            // Roll a random value in the weight range
            var roll = MBRandom.RandomFloat * totalWeight;

            // Walk through candidates until we find the selected one
            var cumulative = 0f;
            foreach (var weighted in weightedEvents)
            {
                cumulative += weighted.Weight;
                if (roll <= cumulative)
                {
                    return weighted.Event;
                }
            }

            // Fallback (should not reach here normally)
            return weightedEvents[weightedEvents.Count - 1].Event;
        }

        /// <summary>
        /// Gets the number of eligible events for diagnostic purposes.
        /// </summary>
        public static int GetEligibleEventCount()
        {
            var escalationState = EscalationManager.Instance?.State;
            if (escalationState == null)
            {
                return 0;
            }

            var allEvents = EventCatalog.GetAllEvents();
            return GetEligibleCandidates(allEvents, escalationState).Count;
        }

        /// <summary>
        /// Internal class for tracking event weights during selection.
        /// </summary>
        private class WeightedEvent
        {
            public EventDefinition Event { get; set; }
            public float Weight { get; set; }
        }
    }
}

