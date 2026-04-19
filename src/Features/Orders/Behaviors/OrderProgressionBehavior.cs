using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Orders.Models;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.SaveSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Enlisted.Features.Orders.Behaviors
{
    /// <summary>
    /// Handles multi-day order progression with phase-based event injection.
    /// Orders progress through 4 phases per day (Dawn, Midday, Dusk, Night).
    /// At "slot" phases, events may fire based on world state and activity level.
    /// </summary>
    public class OrderProgressionBehavior : CampaignBehaviorBase
    {
        private const string LogCategory = "OrderProgression";

        /// <summary>
        /// Singleton instance for global access.
        /// </summary>
        public static OrderProgressionBehavior Instance { get; private set; }

        // Phase timing constants
        private const int DawnHour = 6;
        private const int MiddayHour = 12;
        private const int DuskHour = 18;
        private const int NightHour = 0;

        // Event chance constants - tuned so ROUTINE phases are the majority
        // With 2 slot phases per day, routine activity gives ~12% daily event chance (1 event per 8 days)
        private const float SlotBaseChance = 0.08f;      // 8% base chance for slot phases
        private const float HighSlotBaseChance = 0.15f;  // 15% base chance for slot! phases

        // Activity level multipliers - routine is the baseline
        private static readonly Dictionary<ActivityLevel, float> ActivityMultipliers = new()
        {
            { ActivityLevel.Quiet, 0.25f },    // Quiet: ~3% daily (very rare events)
            { ActivityLevel.Routine, 0.5f },   // Routine: ~12% daily (1 event per 8 days)
            { ActivityLevel.Active, 1.0f },    // Active: ~23% daily (1 event per 4 days)
            { ActivityLevel.Intense, 2.0f }    // Intense: ~46% daily (1 event every 2 days)
        };

        // State tracking
        private int _lastProcessedHour = -1;
        private int _lastProcessedDay = -1;
        private readonly HashSet<string> _recentlyFiredEvents = new();
        private CampaignTime _lastEventCooldownClear = CampaignTime.Zero;

        // Phase recap tracking for duty log (keep last 16 phases = 4 days max)
        private List<PhaseRecap> _phaseRecaps = new List<PhaseRecap>();
        private const int MaxRecapsStored = 16;

        public OrderProgressionBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                dataStore.SyncData("orderProg_lastHour", ref _lastProcessedHour);
                dataStore.SyncData("orderProg_lastDay", ref _lastProcessedDay);
                
                // Serialize phase recaps list for duty log persistence
                dataStore.SyncData("orderProg_phaseRecaps", ref _phaseRecaps);
            });
        }

        /// <summary>
        /// Called every game hour. Checks if we're at a phase transition and processes it.
        /// </summary>
        private void OnHourlyTick()
        {
            // Only process if player is enlisted
            if (EnlistmentBehavior.Instance?.IsEnlisted != true)
            {
                return;
            }

            // Only process if there's an active (accepted) order
            var orderManager = OrderManager.Instance;
            if (orderManager == null || !orderManager.IsOrderActive())
            {
                return;
            }

            var currentHour = (int)CampaignTime.Now.CurrentHourInDay;
            var currentDay = (int)CampaignTime.Now.GetDayOfYear;

            // Check if we're at a phase hour and haven't processed it yet
            if (IsPhaseHour(currentHour) && !HasProcessedPhase(currentHour, currentDay))
            {
                ProcessPhase(currentHour);
                MarkPhaseProcessed(currentHour, currentDay);
            }

            // Clear event cooldowns periodically (every 7 days)
            ClearOldEventCooldowns();
        }

        /// <summary>
        /// Checks if the given hour is a phase transition hour.
        /// </summary>
        private static bool IsPhaseHour(int hour)
        {
            return hour == DawnHour || hour == MiddayHour || hour == DuskHour || hour == NightHour;
        }

        /// <summary>
        /// Checks if we've already processed this phase today.
        /// </summary>
        private bool HasProcessedPhase(int hour, int day)
        {
            return _lastProcessedHour == hour && _lastProcessedDay == day;
        }

        /// <summary>
        /// Marks the current phase as processed.
        /// </summary>
        private void MarkPhaseProcessed(int hour, int day)
        {
            _lastProcessedHour = hour;
            _lastProcessedDay = day;
        }

        /// <summary>
        /// Processes the current phase - may trigger an event based on slot type and world state.
        /// </summary>
        private void ProcessPhase(int hour)
        {
            var phaseName = GetPhaseName(hour);
            var orderManager = OrderManager.Instance;
            var currentOrder = orderManager?.GetCurrentOrder();

            if (currentOrder == null)
            {
                return;
            }

            ModLogger.Debug(LogCategory, $"Processing {phaseName} phase for order: {currentOrder.Id}");

            // Determine if this is a slot phase (for now, treat Midday and Dusk as slot phases)
            var isSlotPhase = hour == MiddayHour || hour == DuskHour;
            var isHighSlot = hour == DuskHour; // Dusk is higher chance

            bool eventFired = false;
            string eventTitle = null;

            if (isSlotPhase)
            {
                // Try to fire an event and capture if it succeeded
                var selectedEvent = TryGetOrderEvent(currentOrder, isHighSlot);
                if (selectedEvent != null)
                {
                    FireOrderEvent(selectedEvent);
                    eventFired = true;
                    eventTitle = selectedEvent.TitleFallback ?? selectedEvent.Id;
                }
            }

            // Record phase recap for duty log
            RecordPhaseRecap(hour, eventFired, eventTitle);
        }

        /// <summary>
        /// Attempts to select an order event based on world state and activity level.
        /// Returns the selected event if roll succeeds, null otherwise.
        /// </summary>
        private EventDefinition TryGetOrderEvent(Models.Order order, bool isHighSlot)
        {
            // Get world situation from orchestrator
            var worldSituation = ContentOrchestrator.Instance?.GetCurrentWorldSituation();
            var activityLevel = worldSituation?.ExpectedActivity ?? ActivityLevel.Routine;

            // Calculate event chance
            var baseChance = isHighSlot ? HighSlotBaseChance : SlotBaseChance;
            var multiplier = ActivityMultipliers.TryGetValue(activityLevel, out var mult) ? mult : 1.0f;
            var finalChance = baseChance * multiplier;

            // Roll for event
            var roll = MBRandom.RandomFloat;
            if (roll > finalChance)
            {
                ModLogger.Debug(LogCategory, $"Event roll failed: {roll:F2} > {finalChance:F2} (activity: {activityLevel})");
                return null;
            }

            ModLogger.Debug(LogCategory, $"Event roll succeeded: {roll:F2} <= {finalChance:F2}");

            // Select an event
            var selectedEvent = SelectOrderEvent(order.Id, worldSituation);
            return selectedEvent;
        }

        /// <summary>
        /// Selects an appropriate event for the given order based on world state.
        /// </summary>
        private EventDefinition SelectOrderEvent(string orderId, WorldSituation worldSituation)
        {
            // Get all events for this order type
            var orderEvents = EventCatalog.GetEventsByOrderType(orderId).ToList();

            if (orderEvents.Count == 0)
            {
                ModLogger.Debug(LogCategory, $"No events found for order type: {orderId}");
                return null;
            }

            // Filter by world state requirements
            var eligibleEvents = FilterByWorldState(orderEvents, worldSituation);

            // Filter by full requirements (includes NotAtSea, tier, role, etc.)
            eligibleEvents = eligibleEvents
                .Where(e => EventRequirementChecker.MeetsRequirements(e.Requirements))
                .ToList();

            // Exclude recently fired events
            eligibleEvents = eligibleEvents.Where(e => !_recentlyFiredEvents.Contains(e.Id)).ToList();

            if (eligibleEvents.Count == 0)
            {
                ModLogger.Debug(LogCategory, $"No eligible events after filtering for order: {orderId}");
                return null;
            }

            // Random selection from eligible events
            var selectedIndex = MBRandom.RandomInt(eligibleEvents.Count);
            var selected = eligibleEvents[selectedIndex];

            ModLogger.Info(LogCategory, $"Selected order event: {selected.Id} for order: {orderId}");
            return selected;
        }

        /// <summary>
        /// Filters events by world state requirements.
        /// Order events use requirements.WorldState array for context-specific selection.
        /// </summary>
        private static List<EventDefinition> FilterByWorldState(List<EventDefinition> events, WorldSituation worldSituation)
        {
            if (worldSituation == null)
            {
                return events;
            }

            var currentWorldState = GetWorldStateString(worldSituation);

            return events.Where(e =>
            {
                // If no world_state requirements, event is always eligible
                if (e.Requirements?.WorldState == null || e.Requirements.WorldState.Count == 0)
                {
                    return true;
                }

                // Check if current world state matches any of the event's requirements
                var requiredStates = e.Requirements.WorldState
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToList();

                return requiredStates.Contains(currentWorldState.ToLowerInvariant());
            }).ToList();
        }

        /// <summary>
        /// Converts WorldSituation to a world_state string for matching.
        /// </summary>
        private static string GetWorldStateString(WorldSituation situation)
        {
            return situation.LordIs switch
            {
                LordSituation.PeacetimeGarrison => "peacetime_garrison",
                LordSituation.PeacetimeRecruiting => "peacetime_recruiting",
                LordSituation.WarMarching => "war_marching",
                LordSituation.WarActiveCampaign => "war_active_campaign",
                LordSituation.SiegeAttacking => "siege_attacking",
                LordSituation.SiegeDefending => "siege_defending",
                LordSituation.Defeated => "defeated",
                LordSituation.Captured => "captured",
                _ => "any"
            };
        }

        /// <summary>
        /// Fires the selected order event, routing through StoryDirector when available.
        /// Phase events are incidental (no ChainContinuation) so the director may defer
        /// them behind the 5-day floor; the Task 30 retry queue will flush them later.
        /// Cooldown bookkeeping fires on both paths so phase-selection stays honest.
        /// </summary>
        private void FireOrderEvent(EventDefinition eventDef)
        {
            var director = StoryDirector.Instance;
            if (director != null)
            {
                director.EmitCandidate(new StoryCandidate
                {
                    SourceId = "order_progression.phase_event." + (eventDef.Id ?? "unknown"),
                    CategoryId = "order_phase." + (eventDef.Id ?? "unknown"),
                    ProposedTier = StoryTier.Modal,
                    SeverityHint = 0.45f,
                    Beats = { StoryBeat.OrderPhaseTransition },
                    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                    EmittedAt = CampaignTime.Now,
                    InteractiveEvent = eventDef,
                    RenderedTitle = eventDef.TitleFallback,
                    RenderedBody = eventDef.SetupFallback,
                    StoryKey = eventDef.Id
                });

                _recentlyFiredEvents.Add(eventDef.Id);
                GlobalEventPacer.RecordAutoEvent(eventDef.Id, eventDef.Category);
                ModLogger.Info(LogCategory, $"Routed order event through Director: {eventDef.Id}");
                return;
            }

            var deliveryManager = EventDeliveryManager.Instance;
            if (deliveryManager == null)
            {
                ModLogger.Warn(LogCategory, "Neither StoryDirector nor EventDeliveryManager available, cannot fire order event");
                return;
            }

            deliveryManager.QueueEvent(eventDef);
            _recentlyFiredEvents.Add(eventDef.Id);
            GlobalEventPacer.RecordAutoEvent(eventDef.Id, eventDef.Category);
            ModLogger.Info(LogCategory, $"Fired order event (Director unavailable): {eventDef.Id}");
        }

        /// <summary>
        /// Gets the phase name for logging.
        /// </summary>
        private static string GetPhaseName(int hour)
        {
            return hour switch
            {
                DawnHour => "Dawn",
                MiddayHour => "Midday",
                DuskHour => "Dusk",
                NightHour => "Night",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Clears old event cooldowns periodically.
        /// </summary>
        private void ClearOldEventCooldowns()
        {
            var daysSinceLastClear = (CampaignTime.Now - _lastEventCooldownClear).ToDays;
            if (daysSinceLastClear >= 7)
            {
                _recentlyFiredEvents.Clear();
                _lastEventCooldownClear = CampaignTime.Now;
                ModLogger.Debug(LogCategory, "Cleared order event cooldowns");
            }
        }

        #region Phase Recap System

        /// <summary>
        /// Creates a recap for the current phase and adds it to the tracking list.
        /// Called after processing each phase during an order.
        /// </summary>
        private void RecordPhaseRecap(int hour, bool eventFired, string eventTitle = null)
        {
            var orderManager = OrderManager.Instance;
            var currentOrder = orderManager?.GetCurrentOrder();
            if (currentOrder == null)
            {
                return;
            }

            var phase = GetDayPhaseFromHour(hour);
            var isSlotPhase = hour == MiddayHour || hour == DuskHour;
            var isHighSlot = hour == DuskHour;

            // Determine phase type for recap generation
            string phaseType = "routine";
            if (isSlotPhase)
            {
                phaseType = isHighSlot ? "slot!" : "slot";
            }

            // Generate recap text
            var recapText = GenerateRecapText(phase, phaseType, eventFired, eventTitle, currentOrder.Id);

            // Create recap entry
            var recap = new PhaseRecap
            {
                Phase = phase,
                PhaseTime = CampaignTime.Now,
                RecapText = recapText,
                EventFired = eventFired,
                OrderDay = CalculateOrderDay(currentOrder),
                PhaseNumber = CalculatePhaseNumber(currentOrder),
                OrderId = currentOrder.Id
            };

            // Add to list
            _phaseRecaps.Add(recap);

            // Trim to max storage (keep most recent)
            while (_phaseRecaps.Count > MaxRecapsStored)
            {
                _phaseRecaps.RemoveAt(0);
            }

            ModLogger.Debug(LogCategory, $"Phase recap recorded: {phase} - {recapText}");
        }

        /// <summary>
        /// Generates recap text based on phase type and whether an event fired.
        /// Uses RP-appropriate language for event outcomes.
        /// </summary>
        private string GenerateRecapText(DayPhase phase, string phaseType, bool eventFired, string eventTitle, string orderId)
        {
            if (eventFired && !string.IsNullOrEmpty(eventTitle))
            {
                // Event fired - use event title with RP-appropriate closing
                // Pick a random resolution phrase for variety
                var resolutions = new[]
                {
                    "Dealt with.",
                    "Resolved.",
                    "Matter settled.",
                    "Situation addressed.",
                    "Attended to."
                };
                var resolution = resolutions[MBRandom.RandomInt(resolutions.Length)];
                return $"{eventTitle}. {resolution}";
            }

            if (phaseType == "slot" || phaseType == "slot!")
            {
                // Slot phase but no event - use foreshadowing text
                return GetForeshadowingText(phase, orderId);
            }

            // Routine phase - simple status
            return GetRoutineText(phase);
        }

        /// <summary>
        /// Returns foreshadowing text for empty slot phases.
        /// Creates tension even when no event fires. 
        /// Naval-aware: uses ship-appropriate text when at sea.
        /// </summary>
        private string GetForeshadowingText(DayPhase phase, string orderId)
        {
            // Check if we're at sea (Warsails DLC)
            var worldSituation = ContentOrchestrator.Instance?.GetCurrentWorldSituation();
            var isAtSea = worldSituation?.TravelContext == Content.Models.TravelContext.Sea;

            // Different foreshadowing based on order type, phase, and travel context
            var foreshadowing = (orderId, isAtSea) switch
            {
                // Naval guard/watch duties
                ("order_guard_duty", true) or ("order_sentry", true) or ("order_deck_watch", true) => new[]
                {
                    "Stared into the fog. Saw naught but grey.",
                    "Thought I spied a sail on the horizon. Gone now.",
                    "The sea is restless tonight. No ships in sight.",
                    "Heard something scrape the hull. Just driftwood.",
                    "Kept watch over the waves. All clear.",
                    "Strange lights in the distance. Stars, most like."
                },

                // Land guard/sentry duties
                ("order_guard_duty", false) or ("order_sentry", false) => new[]
                {
                    "Heard movement in the trees. Naught but wind.",
                    "Thought I spied something. False alarm.",
                    "Kept sharp watch. Nothing stirred.",
                    "Spotted tracks near the perimeter. Origin unknown.",
                    "Strange shadows in the distance. Gone now.",
                    "Something rustled in the brush. A beast, most like."
                },

                // Patrol duties
                ("order_camp_patrol", _) => new[]
                {
                    "Walked the usual rounds. All in order.",
                    "Two men quarreling by the fire. They parted ways.",
                    "Found a dropped coin purse. Returned it to the captain.",
                    "Fresh boot prints in the mud. Nothing amiss.",
                    "Made extra rounds. The camp sleeps well.",
                    "Heard raised voices. The matter resolved itself."
                },

                // Manual labor
                ("order_firewood", _) or ("order_latrine", _) => new[]
                {
                    "Hard labor, but no trouble.",
                    "Other men worked nearby. We spoke little.",
                    "The work is done. Uneventful.",
                    "Kept my head down and toiled on.",
                    "Honest work. No complaints.",
                    "Finished the task without incident."
                },

                // Naval default
                (_, true) => new[]
                {
                    "The sea remains calm. Nothing to report.",
                    "Watched the horizon. No sails in sight.",
                    "The ship creaks and groans. All is well.",
                    "Salt spray and endless water. Quiet passage.",
                    "Stayed at my post. The voyage continues.",
                    "Nothing stirs but the waves."
                },

                // Land default
                _ => new[]
                {
                    "Carried on with duty. All quiet.",
                    "Nothing unusual to report.",
                    "The routine continued without issue.",
                    "Kept watchful. Nothing stirred.",
                    "Stayed vigilant. A quiet shift.",
                    "The hours passed without incident."
                }
            };

            // Pick a random foreshadowing text
            var index = MBRandom.RandomInt(foreshadowing.Length);
            return foreshadowing[index];
        }

        /// <summary>
        /// Returns routine text for non-slot phases.
        /// Naval-aware: uses ship-appropriate text when at sea.
        /// </summary>
        private string GetRoutineText(DayPhase phase)
        {
            // Check if we're at sea (Warsails DLC)
            var worldSituation = ContentOrchestrator.Instance?.GetCurrentWorldSituation();
            var isAtSea = worldSituation?.TravelContext == Content.Models.TravelContext.Sea;

            if (isAtSea)
            {
                return phase switch
                {
                    DayPhase.Dawn => "Morning watch. The sun breaks through the mist.",
                    DayPhase.Midday => "Midday duties. The ship holds course.",
                    DayPhase.Dusk => "Evening falls over calm waters.",
                    DayPhase.Night => "Night watch. Stars guide our way.",
                    _ => "The voyage continues."
                };
            }

            return phase switch
            {
                DayPhase.Dawn => "Morning muster complete. Nothing to report.",
                DayPhase.Midday => "Afternoon duties. All quiet.",
                DayPhase.Dusk => "Evening draws in. Camp settles.",
                DayPhase.Night => "Night watch. Silent and still.",
                _ => "Duty continues."
            };
        }

        /// <summary>
        /// Calculates which day of the order we're on (1-based).
        /// </summary>
        private int CalculateOrderDay(Models.Order order)
        {
            var hoursSinceStart = (CampaignTime.Now - order.IssuedTime).ToHours;
            return (int)(hoursSinceStart / 24) + 1;
        }

        /// <summary>
        /// Calculates which phase number within the order (1-based).
        /// </summary>
        private int CalculatePhaseNumber(Models.Order order)
        {
            var hoursSinceStart = (CampaignTime.Now - order.IssuedTime).ToHours;
            return ((int)hoursSinceStart / 6) + 1; // 4 phases per day (6 hours each)
        }

        /// <summary>
        /// Gets the day phase from hour of day.
        /// </summary>
        private static DayPhase GetDayPhaseFromHour(int hour)
        {
            return hour switch
            {
                >= 6 and <= 11 => DayPhase.Dawn,
                >= 12 and <= 17 => DayPhase.Midday,
                >= 18 and <= 21 => DayPhase.Dusk,
                _ => DayPhase.Night
            };
        }

        /// <summary>
        /// Gets the phase recaps for the current order (last 8 phases = 2 days).
        /// Returns in chronological order (oldest first).
        /// </summary>
        public List<PhaseRecap> GetCurrentOrderRecaps()
        {
            var orderManager = OrderManager.Instance;
            var currentOrder = orderManager?.GetCurrentOrder();
            if (currentOrder == null)
            {
                return new List<PhaseRecap>();
            }

            // Return recaps for current order only
            return _phaseRecaps
                .Where(r => r.OrderId == currentOrder.Id)
                .OrderBy(r => r.PhaseTime.ToHours)
                .ToList();
        }

        /// <summary>
        /// Clears all phase recaps. Called when an order completes or is cancelled.
        /// </summary>
        public void ClearPhaseRecaps()
        {
            _phaseRecaps.Clear();
            ModLogger.Debug(LogCategory, "Phase recaps cleared");
        }

        #endregion
    }
}
