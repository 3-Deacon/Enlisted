using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Escalation
{
    /// <summary>
    /// Persisted escalation tracks.
    ///
    /// Ranges (per docs/Features/Core/core-gameplay.md):
    /// - Scrutiny: 0–100 (tracks rule-breaking, insubordination, crime suspicion)
    /// - Lord reputation: 0–100 (pending migration to native Hero.GetRelation)
    /// - Medical risk: 0–5 (illness/injury risk from conditions and poor care)
    ///
    /// This class is intentionally "dumb storage". All rules live in EscalationManager.
    /// </summary>
    [Serializable]
    public sealed class EscalationState
    {
        public const int ScrutinyMin = 0;
        public const int ScrutinyMax = 100;
        public const int LordReputationMin = 0;
        public const int LordReputationMax = 100;
        public const int MedicalRiskMin = 0;
        public const int MedicalRiskMax = 5;

        // Track values
        public int Scrutiny { get; set; }
        public int LordReputation { get; set; }
        public int MedicalRisk { get; set; }

        // Timestamps used for passive decay rules.
        // We store "last raised time" for tracks where decay requires a quiet period.
        public CampaignTime LastScrutinyRaisedTime { get; set; } = CampaignTime.Zero;
        public CampaignTime LastScrutinyDecayTime { get; set; } = CampaignTime.Zero;

        // Medical risk is special: it should reset when treated and decay only when resting.
        public CampaignTime LastMedicalRiskDecayTime { get; set; } = CampaignTime.Zero;

        // Global cooldown gate for threshold events (Phase 4+).
        public CampaignTime LastThresholdEventTime { get; set; } = CampaignTime.Zero;

        // Pending threshold event story id (consumed by LanceStoryBehavior).
        public string PendingThresholdStoryId { get; set; } = string.Empty;

        // Per-threshold cooldown tracking (key = threshold story id, value = last fired time).
        public Dictionary<string, CampaignTime> ThresholdStoryLastFired { get; set; } =
            new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        // Event system cooldown tracking (key = event id, value = last fired time).
        // Used by EventSelector to prevent the same event from firing within its cooldown period.
        public Dictionary<string, CampaignTime> EventLastFired { get; set; } =
            new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        // One-time events that have already fired (stored by event id).
        // These events cannot fire again for the rest of the playthrough.
        public HashSet<string> OneTimeEventsFired { get; set; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Global event pacing: tracks events across ALL automatic sources (EventPacingManager + MapIncidentManager).
        // These enforce the limits from decision_events.pacing config to prevent event spam.
        public CampaignTime LastAutoEventTime { get; set; } = CampaignTime.Zero;
        public int AutoEventsToday { get; set; }
        public int AutoEventDayNumber { get; set; } = -1;
        public int AutoEventsThisWeek { get; set; }
        public int AutoEventWeekNumber { get; set; } = -1;

        // Quiet day flag: if true, no automatic events fire today (rolled once per day).
        public bool IsQuietDay { get; set; }

        // Per-category cooldown tracking (key = category name, value = last fired time).
        // Used by GlobalEventPacer to enforce per_category_cooldown_days config.
        public Dictionary<string, CampaignTime> CategoryLastFired { get; set; } =
            new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        // Flag system: active flags and their expiration times.
        // Key: flag name, Value: expiration time (CampaignTime.Never for permanent).
        // Flags are temporary boolean states that gate access to decisions/events.
        public Dictionary<string, CampaignTime> ActiveFlags { get; set; } =
            new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        // Chain event system: pending chain events scheduled for future delivery.
        // Key: event ID, Value: scheduled delivery time.
        // Used for delayed follow-up events (e.g., friend repays loan after 7 days).
        public Dictionary<string, CampaignTime> PendingChainEvents { get; set; } =
            new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Checks if a flag is currently active (set and not expired).
        /// </summary>
        public bool HasFlag(string flagName)
        {
            if (string.IsNullOrEmpty(flagName))
            {
                return false;
            }

            ActiveFlags ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

            if (!ActiveFlags.TryGetValue(flagName, out var expiryTime))
            {
                return false;
            }

            // Check if expired
            if (expiryTime != CampaignTime.Never && CampaignTime.Now >= expiryTime)
            {
                ActiveFlags.Remove(flagName);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sets a flag with optional expiration.
        /// </summary>
        public void SetFlag(string flagName, int durationDays = 0)
        {
            if (string.IsNullOrEmpty(flagName))
            {
                return;
            }

            ActiveFlags ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

            var expiryTime = durationDays > 0
                ? CampaignTime.DaysFromNow(durationDays)
                : CampaignTime.Never;

            ActiveFlags[flagName] = expiryTime;
        }

        /// <summary>
        /// Clears a flag.
        /// </summary>
        public void ClearFlag(string flagName)
        {
            if (string.IsNullOrEmpty(flagName))
            {
                return;
            }

            ActiveFlags ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            ActiveFlags.Remove(flagName);
        }

        /// <summary>
        /// Schedules a chain event for future delivery.
        /// If an event with the same ID is already scheduled, it will be rescheduled with the new delay.
        /// </summary>
        public void ScheduleChainEvent(string eventId, int delayHours)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            if (delayHours <= 0)
            {
                return;
            }

            PendingChainEvents ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

            var deliveryTime = CampaignTime.HoursFromNow(delayHours);
            PendingChainEvents[eventId] = deliveryTime;
        }

        /// <summary>
        /// Gets and removes any chain events that are ready to fire.
        /// Returns a list of event IDs that should be delivered now.
        /// </summary>
        public List<string> PopReadyChainEvents()
        {
            PendingChainEvents ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

            var ready = new List<string>();
            var toRemove = new List<string>();

            foreach (var kvp in PendingChainEvents)
            {
                if (CampaignTime.Now >= kvp.Value)
                {
                    ready.Add(kvp.Key);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                PendingChainEvents.Remove(key);
            }

            return ready;
        }

        /// <summary>
        /// Checks if a specific event is on cooldown based on its last fired time and cooldown duration.
        /// Returns true if the event should not fire yet (still in cooldown).
        /// </summary>
        public bool IsEventOnCooldown(string eventId, int cooldownDays)
        {
            if (string.IsNullOrEmpty(eventId) || cooldownDays <= 0)
            {
                return false;
            }

            EventLastFired ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

            if (!EventLastFired.TryGetValue(eventId, out var lastFired))
            {
                return false; // Never fired before, not on cooldown
            }

            var daysSinceLastFired = (CampaignTime.Now - lastFired).ToDays;
            return daysSinceLastFired < cooldownDays;
        }

        /// <summary>
        /// Records that an event was just fired, updating its last-fired timestamp.
        /// </summary>
        public void RecordEventFired(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            EventLastFired ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            EventLastFired[eventId] = CampaignTime.Now;
        }

        /// <summary>
        /// Marks a one-time event as fired so it won't fire again this playthrough.
        /// </summary>
        public void RecordOneTimeEventFired(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            OneTimeEventsFired ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            OneTimeEventsFired.Add(eventId);
        }

        /// <summary>
        /// Checks if a one-time event has already fired.
        /// </summary>
        public bool HasOneTimeEventFired(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return false;
            }

            OneTimeEventsFired ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return OneTimeEventsFired.Contains(eventId);
        }

        /// <summary>
        /// Records that an event category was just fired, updating its last-fired timestamp.
        /// Used by GlobalEventPacer for per-category cooldown tracking.
        /// </summary>
        public void RecordCategoryFired(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return;
            }

            CategoryLastFired ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            CategoryLastFired[category] = CampaignTime.Now;
        }

        public void ClampAll()
        {
            Scrutiny = Clamp(Scrutiny, ScrutinyMin, ScrutinyMax);
            LordReputation = Clamp(LordReputation, LordReputationMin, LordReputationMax);
            MedicalRisk = Clamp(MedicalRisk, MedicalRiskMin, MedicalRiskMax);

            PendingThresholdStoryId ??= string.Empty;
            ThresholdStoryLastFired ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            EventLastFired ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            OneTimeEventsFired ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ActiveFlags ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            PendingChainEvents ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            CategoryLastFired ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            return value > max ? max : value;
        }
    }
}


