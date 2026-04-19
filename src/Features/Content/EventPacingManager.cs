using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Manages chain events (scheduled follow-ups from previous player choices).
    /// Previously controlled random narrative event pacing - now removed per Order Prompt Model.
    /// Chain events fire on daily tick. Random events removed - player engagement via Order Prompts.
    /// </summary>
    public class EventPacingManager : CampaignBehaviorBase
    {
        private const string LogCategory = "EventPacing";

        // Track the last day we ran the tick to avoid double-processing
        private int _lastTickDayNumber = -1;

        public static EventPacingManager Instance { get; private set; }

        public EventPacingManager()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            ModLogger.Info(LogCategory, "Event pacing manager registered for daily tick");
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No additional sync needed - EscalationManager handles all state persistence
            // Chain events are tracked in EscalationState.PendingChainEvents
        }

        /// <summary>
        /// Called once per in-game day. Chain events (scheduled follow-ups) fire here.
        /// Note: Random narrative event spam removed - events now fire via Order Prompt system.
        /// </summary>
        private void OnDailyTick()
        {
            try
            {
                // Only run when enlisted
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                // Prevent double-tick on same day
                var currentDay = (int)CampaignTime.Now.ToDays;
                if (currentDay == _lastTickDayNumber)
                {
                    return;
                }
                _lastTickDayNumber = currentDay;

                // Get escalation state for pacing timestamps
                var escalationState = EscalationManager.Instance?.State;
                if (escalationState == null)
                {
                    return;
                }

                // Check for pending chain events only
                // Random narrative events removed per Order Prompt Model (Phase 1)
                CheckPendingChainEvents(escalationState);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error in event pacing daily tick", ex);
            }
        }

        /// <summary>
        /// Checks for and fires any chain events that are ready to be delivered.
        /// Chain events have highest priority and fire before normal paced events.
        /// Also clears pending event hints from the news system when chain events fire.
        /// </summary>
        private void CheckPendingChainEvents(EscalationState escalationState)
        {
            var readyEvents = escalationState.PopReadyChainEvents();

            if (readyEvents.Count == 0)
            {
                return;
            }

            var deliveryManager = EventDeliveryManager.Instance;
            if (deliveryManager == null)
            {
                ModLogger.Warn(LogCategory, "EventDeliveryManager not available, cannot deliver chain events");
                return;
            }

            foreach (var eventId in readyEvents)
            {
                // Clear pending event context from news system when chain event fires
                EnlistedNewsBehavior.Instance?.ClearPendingEvent(eventId);

                var evt = EventCatalog.GetEvent(eventId);
                if (evt != null)
                {
                    ModLogger.Info(LogCategory, $"Firing scheduled chain event: {eventId}");
                    deliveryManager.QueueEvent(evt);
                }
                else
                {
                    ModLogger.Warn(LogCategory, $"Scheduled chain event not found: {eventId}");
                }
            }
        }

    }
}

