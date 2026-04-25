using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Enforces global event pacing limits across all automatic event sources.
    /// Both EventPacingManager and MapIncidentManager check this before firing events.
    /// Reads all limits from enlisted_config.json → decision_events.pacing section.
    ///
    /// Enforced limits:
    /// - max_per_day: Maximum automatic events per day
    /// - max_per_week: Maximum automatic events per week
    /// - min_hours_between: Minimum hours between any automatic events
    /// - per_category_cooldown_days: Days between events of same category
    ///
    /// Quiet days are computed on day-rollover from the enlisted lord's situation
    /// (defeated / captured / peacetime garrison with a 10% roll).
    /// </summary>
    public static class GlobalEventPacer
    {
        private const string LogCategory = "GlobalPacing";

        // Cached config to avoid repeated file reads
        private static EventPacingConfig _cachedConfig;
        private static bool _configLoaded;

        /// <summary>
        /// Gets the pacing config, loading it once and caching for performance.
        /// </summary>
        private static EventPacingConfig Config
        {
            get
            {
                if (!_configLoaded)
                {
                    _cachedConfig = ConfigurationManager.LoadEventPacingConfig();
                    _configLoaded = true;
                    ModLogger.Debug(LogCategory,
                        $"Loaded pacing config: max_per_day={_cachedConfig.MaxPerDay}, " +
                        $"max_per_week={_cachedConfig.MaxPerWeek}, min_hours_between={_cachedConfig.MinHoursBetween}");
                }
                return _cachedConfig;
            }
        }

        /// <summary>
        /// Checks if an automatic event is allowed to fire right now.
        /// Enforces config limits: max_per_day, max_per_week, min_hours_between, and per_category_cooldown.
        /// </summary>
        /// <param name="eventId">The event ID (for logging)</param>
        /// <param name="reason">Output: why the event was blocked, if any</param>
        /// <returns>True if the event can fire, false if blocked by pacing limits</returns>
        public static bool CanFireAutoEvent(string eventId, out string reason)
        {
            return CanFireAutoEvent(eventId, null, out reason);
        }

        /// <summary>
        /// Checks if an automatic event is allowed to fire right now with category check.
        /// </summary>
        /// <param name="eventId">The event ID (for logging)</param>
        /// <param name="category">Optional category for per-category cooldown</param>
        /// <param name="reason">Output: why the event was blocked, if any</param>
        /// <returns>True if the event can fire, false if blocked by pacing limits</returns>
        public static bool CanFireAutoEvent(string eventId, string category, out string reason)
        {
            reason = null;

            var state = EscalationManager.Instance?.State;
            if (state == null)
            {
                reason = "EscalationState not available";
                return false;
            }

            var config = Config;
            var now = CampaignTime.Now;
            var currentDay = (int)now.ToDays;
            var currentWeek = currentDay / 7;

            RolloverIfNewDay(state, currentDay, currentWeek);

            if (state.IsQuietDay)
            {
                reason = "Quiet day - no automatic events today";
                ModLogger.Debug(LogCategory, $"Blocking {eventId}: {reason}");
                return false;
            }

            // Check daily limit
            if (state.AutoEventsToday >= config.MaxPerDay)
            {
                reason = $"Daily limit reached ({state.AutoEventsToday}/{config.MaxPerDay})";
                ModLogger.Debug(LogCategory, $"Blocking {eventId}: {reason}");
                return false;
            }

            // Check weekly limit
            if (state.AutoEventsThisWeek >= config.MaxPerWeek)
            {
                reason = $"Weekly limit reached ({state.AutoEventsThisWeek}/{config.MaxPerWeek})";
                ModLogger.Debug(LogCategory, $"Blocking {eventId}: {reason}");
                return false;
            }

            // Check minimum hours between events
            if (state.LastAutoEventTime != CampaignTime.Zero)
            {
                var hoursSinceLastEvent = (now - state.LastAutoEventTime).ToHours;
                if (hoursSinceLastEvent < config.MinHoursBetween)
                {
                    reason = $"Too soon since last event ({hoursSinceLastEvent:F1}h elapsed, need {config.MinHoursBetween}h)";
                    ModLogger.Debug(LogCategory, $"Blocking {eventId}: {reason}");
                    return false;
                }
            }

            // Check per-category cooldown (if category provided and cooldown > 0)
            if (!string.IsNullOrEmpty(category) && config.PerCategoryCooldownDays > 0)
            {
                if (IsCategoryOnCooldown(state, category, config.PerCategoryCooldownDays))
                {
                    reason = $"Category '{category}' on cooldown ({config.PerCategoryCooldownDays} day limit)";
                    ModLogger.Debug(LogCategory, $"Blocking {eventId}: {reason}");
                    return false;
                }
            }

            ModLogger.Debug(LogCategory,
                $"Allowing {eventId}: today={state.AutoEventsToday}/{config.MaxPerDay}, " +
                $"week={state.AutoEventsThisWeek}/{config.MaxPerWeek}");
            return true;
        }

        /// <summary>
        /// Checks if a category is on cooldown based on last event time.
        /// </summary>
        private static bool IsCategoryOnCooldown(EscalationState state, string category, int cooldownDays)
        {
            if (state.CategoryLastFired == null)
            {
                return false;
            }

            if (!state.CategoryLastFired.TryGetValue(category, out var lastFired))
            {
                return false;
            }

            var daysSinceLastFired = (CampaignTime.Now - lastFired).ToDays;
            return daysSinceLastFired < cooldownDays;
        }

        /// <summary>
        /// Records that an automatic event was fired, updating counts and timestamps.
        /// Call this after successfully queuing an event.
        /// </summary>
        /// <param name="eventId">The event ID (for logging)</param>
        public static void RecordAutoEvent(string eventId)
        {
            RecordAutoEvent(eventId, null);
        }

        /// <summary>
        /// Records that an automatic event was fired with category tracking.
        /// </summary>
        /// <param name="eventId">The event ID (for logging)</param>
        /// <param name="category">Optional category for per-category cooldown tracking</param>
        public static void RecordAutoEvent(string eventId, string category)
        {
            var state = EscalationManager.Instance?.State;
            if (state == null)
            {
                ModLogger.Warn(LogCategory, "Cannot record event - EscalationState not available");
                return;
            }

            var now = CampaignTime.Now;
            var currentDay = (int)now.ToDays;
            var currentWeek = currentDay / 7;

            RolloverIfNewDay(state, currentDay, currentWeek);

            // Update tracking
            state.LastAutoEventTime = now;
            state.AutoEventsToday++;
            state.AutoEventsThisWeek++;

            // Track category if provided
            if (!string.IsNullOrEmpty(category))
            {
                state.RecordCategoryFired(category);
            }

            ModLogger.Info(LogCategory,
                $"Recorded auto event: {eventId} (today={state.AutoEventsToday}, week={state.AutoEventsThisWeek}" +
                (string.IsNullOrEmpty(category) ? ")" : $", category={category})"));
        }

        /// <summary>
        /// Gets diagnostic info about current pacing state for debug display.
        /// </summary>
        public static string GetPacingDiagnostics()
        {
            var state = EscalationManager.Instance?.State;
            if (state == null)
            {
                return "Pacing: No state available";
            }

            var config = Config;
            var now = CampaignTime.Now;
            var hoursSinceLast = state.LastAutoEventTime != CampaignTime.Zero
                ? (now - state.LastAutoEventTime).ToHours
                : -1;

            return $"Pacing: {state.AutoEventsToday}/{config.MaxPerDay} today, " +
                   $"{state.AutoEventsThisWeek}/{config.MaxPerWeek} this week, " +
                   $"{hoursSinceLast:F1}h since last (min {config.MinHoursBetween}h)";
        }

        /// <summary>
        /// Resets cached config (for hot-reload during development).
        /// </summary>
        public static void ReloadConfig()
        {
            _configLoaded = false;
            _cachedConfig = null;
            ModLogger.Info(LogCategory, "Pacing config cache cleared");
        }

        /// <summary>
        /// Rolls daily/weekly counters and recomputes the quiet-day flag on day boundaries.
        /// </summary>
        private static void RolloverIfNewDay(EscalationState state, int currentDay, int currentWeek)
        {
            if (state.AutoEventDayNumber != currentDay)
            {
                state.AutoEventDayNumber = currentDay;
                state.AutoEventsToday = 0;
                state.IsQuietDay = ComputeIsQuietDay();
            }
            if (state.AutoEventWeekNumber != currentWeek)
            {
                state.AutoEventWeekNumber = currentWeek;
                state.AutoEventsThisWeek = 0;
            }
        }

        /// <summary>
        /// Quiet-day predicate: defeated / captured / peacetime garrison with a 10% roll.
        /// Returns false outside enlistment so non-enlisted play keeps the default pacing.
        /// </summary>
        private static bool ComputeIsQuietDay()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return false;
            }

            var situation = WorldStateAnalyzer.AnalyzeSituation();
            var isQuiet = situation.LordIs == LordSituation.Defeated
                       || situation.LordIs == LordSituation.Captured
                       || (situation.LordIs == LordSituation.PeacetimeGarrison &&
                           MBRandom.RandomFloat < 0.10f);

            if (isQuiet)
            {
                ModLogger.Debug(LogCategory, $"Quiet day rolled (situation: {situation.LordIs})");
            }
            return isQuiet;
        }
    }
}

