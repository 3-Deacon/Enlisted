using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace Enlisted.Features.Escalation
{
    /// <summary>
    /// Escalation manager - tracks player trouble with command and medical risk.
    ///
    /// Responsibilities:
    /// - Owns the persisted EscalationState (save/load via CampaignBehavior SyncData)
    /// - Provides track modification APIs (Scrutiny 0-100, MedicalRisk 0-5)
    /// - Manages lord reputation (0-100 scale, pending migration to native Hero.GetRelation)
    /// - Provides readable "state" descriptions for UI ("Watched", "Hot", "Trusted", etc.)
    /// - Handles passive decay via daily tick
    ///
    /// Important constraints:
    /// - No instant hard fails: this manager never forces game-over; it only tracks and exposes state.
    /// - Internal-only: does not touch vanilla crime/reputation systems.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "RedundantNameQualifier",
        Justification = "TaleWorlds.Library namespace conflicts with Enlisted.Mod.Core.Config (both contain ConfigurationManager). Adding 'using TaleWorlds.Library;' causes ambiguous reference errors.")]
    public sealed class EscalationManager : CampaignBehaviorBase
    {
        private const string LogCategory = "Escalation";
        private const int MinimumScrutinyRecoveryGraceDays = 14;
        private const int OrdinaryHighScrutinyCeiling = EscalationThresholds.ScrutinyExposed;

        public static EscalationManager Instance { get; private set; }

        private readonly EscalationState _state = new EscalationState();
        private int _lastDailyTickDayNumber = -1;
        private int _lastScrutinyDecayDayNumber = -1;
        private HashSet<int> _declinedPromotions = new HashSet<int>();

        public EscalationState State => _state;

        public EscalationManager()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnDailyTick()
        {
            try
            {
                if (!IsEnabled())
                {
                    return;
                }

                // Only active while enlisted
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                // DailyTickEvent should be daily, but keep a stable guard anyway.
                var dayNumber = (int)CampaignTime.Now.ToDays;
                if (dayNumber == _lastDailyTickDayNumber)
                {
                    return;
                }
                _lastDailyTickDayNumber = dayNumber;

                ApplyPassiveDecay();
                EvaluateThresholdsAndQueueIfNeeded();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Escalation", "Escalation daily tick failed", ex);
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                // Track values
                var scrutiny = _state.Scrutiny;
                var lordRep = _state.LordReputation;
                var medical = _state.MedicalRisk;
                var scrutinyRecoveryFloor = _state.ScrutinyRecoveryFloor;

                _ = dataStore.SyncData("esc_scrutiny", ref scrutiny);
                _ = dataStore.SyncData("esc_lordRep", ref lordRep);
                _ = dataStore.SyncData("esc_medical", ref medical);
                _ = dataStore.SyncData("esc_scrutinyRecoveryFloor", ref scrutinyRecoveryFloor);

                // Timestamps for decay logic
                var lastScrutinyRaised = _state.LastScrutinyRaisedTime;
                var lastScrutinyDecay = _state.LastScrutinyDecayTime;
                var lastOrdinaryHighScrutinyPressure = _state.LastOrdinaryHighScrutinyPressureTime;
                var lastMedicalDecay = _state.LastMedicalRiskDecayTime;
                var lastThresholdEvent = _state.LastThresholdEventTime;

                _ = dataStore.SyncData("esc_lastScrutinyRaised", ref lastScrutinyRaised);
                _ = dataStore.SyncData("esc_lastScrutinyDecay", ref lastScrutinyDecay);
                _ = dataStore.SyncData("esc_lastOrdinaryHighScrutinyPressure", ref lastOrdinaryHighScrutinyPressure);
                _ = dataStore.SyncData("esc_lastMedicalDecay", ref lastMedicalDecay);
                _ = dataStore.SyncData("esc_lastThresholdEvent", ref lastThresholdEvent);

                // Pending threshold event
                var pendingThreshold = _state.PendingThresholdStoryId ?? string.Empty;
                _ = dataStore.SyncData("esc_pendingThreshold", ref pendingThreshold);

                // Per-threshold cooldown map
                var thresholdKeys = (_state.ThresholdStoryLastFired ?? Enumerable.Empty<KeyValuePair<string, CampaignTime>>())
                    .Select(k => k.Key)
                    .ToList();
                var thresholdCount = thresholdKeys.Count;
                _ = dataStore.SyncData("esc_thresholdCount", ref thresholdCount);

                // Event cooldown map (same pattern as threshold cooldowns)
                var eventKeys = (_state.EventLastFired ?? Enumerable.Empty<KeyValuePair<string, CampaignTime>>())
                    .Select(k => k.Key)
                    .ToList();
                var eventCooldownCount = eventKeys.Count;
                _ = dataStore.SyncData("esc_eventCooldownCount", ref eventCooldownCount);

                // One-time events fired
                var oneTimeKeys = (_state.OneTimeEventsFired ?? Enumerable.Empty<string>()).ToList();
                var oneTimeCount = oneTimeKeys.Count;
                _ = dataStore.SyncData("esc_oneTimeCount", ref oneTimeCount);

                if (dataStore.IsLoading)
                {
                    _state.Scrutiny = scrutiny;
                    _state.LordReputation = lordRep;
                    _state.MedicalRisk = medical;
                    _state.ScrutinyRecoveryFloor = scrutinyRecoveryFloor;

                    _state.LastScrutinyRaisedTime = lastScrutinyRaised;
                    _state.LastScrutinyDecayTime = lastScrutinyDecay;
                    _state.LastOrdinaryHighScrutinyPressureTime = lastOrdinaryHighScrutinyPressure;
                    if (_state.LastScrutinyDecayTime != CampaignTime.Zero && _state.ScrutinyRecoveryFloor > _state.Scrutiny)
                    {
                        _state.ScrutinyRecoveryFloor = _state.Scrutiny;
                    }
                    _state.LastMedicalRiskDecayTime = lastMedicalDecay;
                    _state.LastThresholdEventTime = lastThresholdEvent;

                    _state.PendingThresholdStoryId = pendingThreshold;
                    _state.ThresholdStoryLastFired = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < thresholdCount; i++)
                    {
                        var key = string.Empty;
                        var time = CampaignTime.Zero;
                        _ = dataStore.SyncData($"esc_threshold_{i}_id", ref key);
                        _ = dataStore.SyncData($"esc_threshold_{i}_time", ref time);
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            _state.ThresholdStoryLastFired[key] = time;
                        }
                    }

                    // Load event cooldown map
                    _state.EventLastFired = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < eventCooldownCount; i++)
                    {
                        var key = string.Empty;
                        var time = CampaignTime.Zero;
                        _ = dataStore.SyncData($"esc_event_{i}_id", ref key);
                        _ = dataStore.SyncData($"esc_event_{i}_time", ref time);
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            _state.EventLastFired[key] = time;
                        }
                    }

                    // Load one-time events set
                    _state.OneTimeEventsFired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < oneTimeCount; i++)
                    {
                        var eventId = string.Empty;
                        _ = dataStore.SyncData($"esc_onetime_{i}", ref eventId);
                        if (!string.IsNullOrWhiteSpace(eventId))
                        {
                            _ = _state.OneTimeEventsFired.Add(eventId);
                        }
                    }

                    _state.ClampAll();
                    _lastScrutinyDecayDayNumber = _state.LastScrutinyDecayTime == CampaignTime.Zero
                        ? -1
                        : (int)_state.LastScrutinyDecayTime.ToDays;
                }
                else
                {
                    // Store in a stable order to reduce churn
                    thresholdKeys.Sort(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < thresholdKeys.Count; i++)
                    {
                        var key = thresholdKeys[i];
                        var time = _state.ThresholdStoryLastFired.TryGetValue(key, out var t) ? t : CampaignTime.Zero;
                        _ = dataStore.SyncData($"esc_threshold_{i}_id", ref key);
                        _ = dataStore.SyncData($"esc_threshold_{i}_time", ref time);
                    }

                    // Save event cooldown map
                    eventKeys.Sort(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < eventKeys.Count; i++)
                    {
                        var key = eventKeys[i];
                        var time = _state.EventLastFired.TryGetValue(key, out var t) ? t : CampaignTime.Zero;
                        _ = dataStore.SyncData($"esc_event_{i}_id", ref key);
                        _ = dataStore.SyncData($"esc_event_{i}_time", ref time);
                    }

                    // Save one-time events set
                    oneTimeKeys.Sort(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < oneTimeKeys.Count; i++)
                    {
                        var eventId = oneTimeKeys[i];
                        _ = dataStore.SyncData($"esc_onetime_{i}", ref eventId);
                    }
                }

                // Global event pacing state (prevents event spam across all automatic sources)
                var lastAutoEvent = _state.LastAutoEventTime;
                var autoEventsToday = _state.AutoEventsToday;
                var autoEventDayNum = _state.AutoEventDayNumber;
                var autoEventsWeek = _state.AutoEventsThisWeek;
                var autoEventWeekNum = _state.AutoEventWeekNumber;
                var isQuietDay = _state.IsQuietDay;

                _ = dataStore.SyncData("esc_lastAutoEvent", ref lastAutoEvent);
                _ = dataStore.SyncData("esc_autoEventsToday", ref autoEventsToday);
                _ = dataStore.SyncData("esc_autoEventDayNum", ref autoEventDayNum);
                _ = dataStore.SyncData("esc_autoEventsWeek", ref autoEventsWeek);
                _ = dataStore.SyncData("esc_autoEventWeekNum", ref autoEventWeekNum);
                _ = dataStore.SyncData("esc_isQuietDay", ref isQuietDay);

                // Category cooldown map (tracks last fired time per category)
                var categoryKeys = (_state.CategoryLastFired ?? Enumerable.Empty<KeyValuePair<string, CampaignTime>>())
                    .Select(k => k.Key)
                    .ToList();
                var categoryCount = categoryKeys.Count;
                _ = dataStore.SyncData("esc_categoryCooldownCount", ref categoryCount);

                if (dataStore.IsLoading)
                {
                    _state.LastAutoEventTime = lastAutoEvent;
                    _state.AutoEventsToday = autoEventsToday;
                    _state.AutoEventDayNumber = autoEventDayNum;
                    _state.AutoEventsThisWeek = autoEventsWeek;
                    _state.AutoEventWeekNumber = autoEventWeekNum;
                    _state.IsQuietDay = isQuietDay;

                    // Load category cooldown map
                    _state.CategoryLastFired = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < categoryCount; i++)
                    {
                        var key = string.Empty;
                        var time = CampaignTime.Zero;
                        _ = dataStore.SyncData($"esc_category_{i}_id", ref key);
                        _ = dataStore.SyncData($"esc_category_{i}_time", ref time);
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            _state.CategoryLastFired[key] = time;
                        }
                    }
                }
                else
                {
                    // Save category cooldown map
                    categoryKeys.Sort(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < categoryKeys.Count; i++)
                    {
                        var key = categoryKeys[i];
                        var time = _state.CategoryLastFired[key];
                        _ = dataStore.SyncData($"esc_category_{i}_id", ref key);
                        _ = dataStore.SyncData($"esc_category_{i}_time", ref time);
                    }
                }

                // Declined promotion tracking
                var declinedCount = _declinedPromotions.Count;
                _ = dataStore.SyncData("esc_declinedPromotionsCount", ref declinedCount);

                if (dataStore.IsLoading)
                {
                    _declinedPromotions = new HashSet<int>();
                    for (var i = 0; i < declinedCount; i++)
                    {
                        var tier = 0;
                        _ = dataStore.SyncData($"esc_declinedPromo_{i}", ref tier);
                        if (tier > 0)
                        {
                            _ = _declinedPromotions.Add(tier);
                        }
                    }
                }
                else
                {
                    var tiers = _declinedPromotions.ToList();
                    tiers.Sort();
                    for (var i = 0; i < tiers.Count; i++)
                    {
                        var tier = tiers[i];
                        _ = dataStore.SyncData($"esc_declinedPromo_{i}", ref tier);
                    }
                }
            });
        }

        public bool IsEnabled()
        {
            // Feature-flag requirement: can be disabled without removing code.
            return ConfigurationManager.LoadEscalationConfig()?.Enabled == true;
        }

        public string PendingThresholdStoryId => _state.PendingThresholdStoryId ?? string.Empty;

        public void ClearPendingThresholdStory()
        {
            _state.PendingThresholdStoryId = string.Empty;
        }

        public void MarkThresholdStoryFired(string storyId)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                _state.PendingThresholdStoryId = string.Empty;
                return;
            }

            _state.LastThresholdEventTime = CampaignTime.Now;
            _state.ThresholdStoryLastFired ??= new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            _state.ThresholdStoryLastFired[storyId] = CampaignTime.Now;
            _state.PendingThresholdStoryId = string.Empty;
        }

        public bool IsThresholdStoryOnCooldown(string storyId, int cooldownDays)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                return true;
            }

            if (cooldownDays <= 0)
            {
                return false;
            }

            if (_state.ThresholdStoryLastFired == null || !_state.ThresholdStoryLastFired.TryGetValue(storyId, out var last))
            {
                return false;
            }

            var next = last + CampaignTime.Days(cooldownDays);
            return next.IsFuture;
        }

        public void EvaluateThresholdsAndQueueIfNeeded()
        {
            if (!IsEnabled())
            {
                return;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                _state.PendingThresholdStoryId = string.Empty;
                return;
            }

            var cfg = ConfigurationManager.LoadEscalationConfig() ?? new EscalationConfig();
            var cooldownDays = Math.Max(0, cfg.ThresholdEventCooldownDays);

            // Only one threshold event per day.
            var lastDay = (int)_state.LastThresholdEventTime.ToDays;
            var today = (int)CampaignTime.Now.ToDays;
            if (_state.LastThresholdEventTime != CampaignTime.Zero && lastDay == today)
            {
                return;
            }

            var candidate = PickBestThresholdCandidateId(cooldownDays);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                _state.PendingThresholdStoryId = string.Empty;
                return;
            }

            // If the pending event no longer matches what we'd fire now, replace it.
            _state.PendingThresholdStoryId = candidate;
        }

        private string PickBestThresholdCandidateId(int cooldownDays)
        {
            // Priority is deterministic and "highest threshold wins" per track.
            // Order across tracks: Scrutiny, Medical.
            var scrutinyCandidates = new[]
            {
                (_state.Scrutiny >= EscalationThresholds.ScrutinyExposed, "scrutiny_exposed"),
                (_state.Scrutiny >= EscalationThresholds.ScrutinyAudit, "scrutiny_audit"),
                (_state.Scrutiny >= EscalationThresholds.ScrutinyShakedown, "scrutiny_shakedown"),
                (_state.Scrutiny >= EscalationThresholds.ScrutinyWarning, "scrutiny_warning")
            };
            var medicalCandidates = new[]
            {
                (_state.MedicalRisk >= EscalationThresholds.MedicalEmergency, "medical_emergency"),
                (_state.MedicalRisk >= EscalationThresholds.MedicalComplication, "medical_complication"),
                (_state.MedicalRisk >= EscalationThresholds.MedicalWorsening, "medical_worsening")
            };

            foreach (var (ok, id) in scrutinyCandidates)
            {
                if (ok && !IsThresholdStoryOnCooldown(id, cooldownDays))
                {
                    return id;
                }
            }
            foreach (var (ok, id) in medicalCandidates)
            {
                if (ok && !IsThresholdStoryOnCooldown(id, cooldownDays))
                {
                    return id;
                }
            }

            return string.Empty;
        }

        #region Track modification

        public void ModifyScrutiny(int delta, string reason = null)
        {
            if (!IsEnabled())
            {
                return;
            }

            var oldValue = _state.Scrutiny;
            var attempted = oldValue + delta;
            var next = Clamp(attempted, EscalationState.ScrutinyMin, EscalationState.ScrutinyMax);

            if (TryCapScrutinyIncreaseDuringRecovery(oldValue, attempted, delta, reason, out var cappedValue, out var graceAgeDays, out var graceDays, out var recoveryFloor))
            {
                next = cappedValue;
                ModLogger.Info(LogCategory,
                    $"Scrutiny increase suppressed during recovery grace: {oldValue} -> {next} " +
                    $"(attempted={Clamp(attempted, EscalationState.ScrutinyMin, EscalationState.ScrutinyMax)}, delta={delta}, " +
                    $"floor={recoveryFloor}, reason={reason ?? "unknown"}, graceAgeDays={graceAgeDays:F1}, graceDays={graceDays})");
            }
            else if (TryCapOrdinaryHighScrutinyPressure(oldValue, attempted, delta, reason, out cappedValue))
            {
                next = cappedValue;
                _state.LastOrdinaryHighScrutinyPressureTime = CampaignTime.Now;
                ModLogger.Info(LogCategory,
                    $"Ordinary scrutiny pressure capped at high-scrutiny ceiling: {oldValue} -> {next} " +
                    $"(attempted={Clamp(attempted, EscalationState.ScrutinyMin, EscalationState.ScrutinyMax)}, delta={delta}, " +
                    $"ceiling={OrdinaryHighScrutinyCeiling}, reason={reason ?? "unknown"})");
            }

            _state.Scrutiny = next;

            if (_state.Scrutiny > oldValue)
            {
                // Scrutiny decay requires a quiet period after an actual increase.
                // Do not reset the quiet timer for no-op positive writes at the cap, or a
                // maxed-out player can never decay back down while camp incidents keep firing.
                _state.LastScrutinyRaisedTime = CampaignTime.Now;
            }
            else if (delta > 0 && oldValue >= EscalationState.ScrutinyMax)
            {
                ModLogger.Debug(LogCategory,
                    $"Scrutiny raise ignored at cap ({oldValue}){FormatReason(reason)}");
            }

            LogTrackChange("Scrutiny", oldValue, _state.Scrutiny, reason);
            CheckThresholdCrossing("Scrutiny", oldValue, _state.Scrutiny, new[] {
                EscalationThresholds.ScrutinyWarning,
                EscalationThresholds.ScrutinyShakedown,
                EscalationThresholds.ScrutinyAudit,
                EscalationThresholds.ScrutinyExposed,
                EscalationThresholds.ScrutinyCritical
            });

            if (_state.Scrutiny != oldValue)
            {
                ShowScrutinyChangeMessage(delta, oldValue, _state.Scrutiny);
            }

            EvaluateThresholdsAndQueueIfNeeded();
        }

        private bool TryCapScrutinyIncreaseDuringRecovery(
            int oldValue,
            int attemptedValue,
            int delta,
            string reason,
            out int cappedValue,
            out float graceAgeDays,
            out int graceDays,
            out int recoveryFloor)
        {
            var clampedAttempt = Clamp(attemptedValue, EscalationState.ScrutinyMin, EscalationState.ScrutinyMax);
            cappedValue = clampedAttempt;
            graceAgeDays = 0f;
            graceDays = GetScrutinyRecoveryGraceDays();
            recoveryFloor = Clamp(_state.ScrutinyRecoveryFloor, EscalationState.ScrutinyMin, EscalationState.ScrutinyMax);

            if (delta <= 0 || clampedAttempt <= oldValue)
            {
                return false;
            }

            if (_state.LastScrutinyDecayTime == CampaignTime.Zero)
            {
                return false;
            }

            graceAgeDays = (float)(CampaignTime.Now.ToDays - _state.LastScrutinyDecayTime.ToDays);
            if (graceAgeDays < 0f || graceAgeDays > graceDays)
            {
                return false;
            }

            if (IsSevereScrutinyReason(reason))
            {
                return false;
            }

            // During recovery grace, ordinary camp/storylet pressure must not erase
            // the player's recovered value. Freeze the track at the current value
            // instead of allowing a bounce back to 99 under the hard cap.
            recoveryFloor = Math.Min(recoveryFloor, oldValue);
            cappedValue = oldValue;
            return cappedValue != clampedAttempt;
        }

        private bool TryCapOrdinaryHighScrutinyPressure(
            int oldValue,
            int attemptedValue,
            int delta,
            string reason,
            out int cappedValue)
        {
            var clampedAttempt = Clamp(attemptedValue, EscalationState.ScrutinyMin, EscalationState.ScrutinyMax);
            cappedValue = clampedAttempt;

            if (delta <= 0 || clampedAttempt <= oldValue)
            {
                return false;
            }

            if (IsSevereScrutinyReason(reason))
            {
                return false;
            }

            if (oldValue < OrdinaryHighScrutinyCeiling && clampedAttempt <= OrdinaryHighScrutinyCeiling)
            {
                return false;
            }

            cappedValue = oldValue >= OrdinaryHighScrutinyCeiling
                ? oldValue
                : OrdinaryHighScrutinyCeiling;
            return cappedValue != clampedAttempt;
        }

        private static bool IsSevereScrutinyReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            var severeMarkers = new[]
            {
                "severe",
                "disciplinary",
                "discipline",
                "desertion",
                "desert",
                "crime",
                "criminal",
                "arrest",
                "mutiny",
                "treason",
                "execution",
                "exposed",
                "critical",
                "audit"
            };

            return severeMarkers.Any(marker => reason.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int GetScrutinyRecoveryGraceDays()
        {
            var cfg = ConfigurationManager.LoadEscalationConfig() ?? new EscalationConfig();
            var intervalDays = Math.Max(1, cfg.ScrutinyDecayIntervalDays);
            return Math.Max(MinimumScrutinyRecoveryGraceDays, intervalDays * 2);
        }

        /// <summary>
        /// Gets the current native relation with the enlisted lord and mirrors it into the legacy
        /// LordReputation field so older UI/report code does not read stale save data.
        /// </summary>
        public int GetCurrentLordRelation(int fallback = 0)
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = enlistment?.EnlistedLord;
            if (enlistment?.IsEnlisted != true || lord == null || Hero.MainHero == null)
            {
                return fallback;
            }

            var relation = CharacterRelationManager.GetHeroRelation(Hero.MainHero, lord);
            _state.LordReputation = Clamp(relation, -100, 100);
            return relation;
        }

        /// <summary>
        /// Modifies the player's relation with their enlisted lord using native Bannerlord reputation system.
        /// Also mirrors the resulting value into the legacy LordReputation field for old consumers.
        /// </summary>
        public void ModifyLordReputation(int delta, string reason = null)
        {
            if (!IsEnabled() || delta == 0)
            {
                return;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true || enlistment.EnlistedLord == null || Hero.MainHero == null)
            {
                ModLogger.Expected(LogCategory, "lord_relation_target_missing",
                    $"Lord relation change skipped: no enlisted lord/player{FormatReason(reason)}");
                return;
            }

            var lord = enlistment.EnlistedLord;
            var oldValue = CharacterRelationManager.GetHeroRelation(Hero.MainHero, lord);
            ChangeRelationAction.ApplyPlayerRelation(lord, delta, affectRelatives: false, showQuickNotification: false);
            var newValue = CharacterRelationManager.GetHeroRelation(Hero.MainHero, lord);
            _state.LordReputation = Clamp(newValue, -100, 100);

            if (oldValue == newValue)
            {
                ModLogger.Info(LogCategory,
                    $"Lord relation unchanged: {lord.Name} {newValue} after delta {delta:+#;-#;0}{FormatReason(reason)}");
                return;
            }

            ModLogger.Info(LogCategory,
                $"Lord relation changed: {lord.Name} {oldValue} -> {newValue} (delta={newValue - oldValue:+#;-#;0}{FormatReason(reason)})");

            LogTrackChange("LordRelation", oldValue, newValue, reason);

            if (EnlistedNewsBehavior.Instance != null)
            {
                string message = GetReputationChangeMessage("Lord", delta, newValue);
                EnlistedNewsBehavior.Instance.AddReputationChange(
                    target: "Lord",
                    delta: newValue - oldValue,
                    newValue: newValue,
                    message: message,
                    dayNumber: (int)CampaignTime.Now.ToDays
                );
            }
        }

        public void ModifyMedicalRisk(int delta, string reason = null)
        {
            if (!IsEnabled())
            {
                return;
            }

            var oldValue = _state.MedicalRisk;
            var next = oldValue + delta;
            _state.MedicalRisk = Clamp(next, EscalationState.MedicalRiskMin, EscalationState.MedicalRiskMax);
            LogTrackChange("MedicalRisk", oldValue, _state.MedicalRisk, reason);
            CheckThresholdCrossing("MedicalRisk", oldValue, _state.MedicalRisk, new[] { 2, 3, 4, 5 });
            EvaluateThresholdsAndQueueIfNeeded();
        }

        public void ResetMedicalRisk(string reason = null)
        {
            if (!IsEnabled())
            {
                return;
            }

            var oldValue = _state.MedicalRisk;
            _state.MedicalRisk = 0;
            LogTrackChange("MedicalRisk", oldValue, _state.MedicalRisk, reason ?? "treated");
        }

        #endregion

        #region Passive decay (logic only; integration to daily tick is a later step)

        public void ApplyPassiveDecay()
        {
            if (!IsEnabled())
            {
                return;
            }

            var cfg = ConfigurationManager.LoadEscalationConfig() ?? new EscalationConfig();
            var now = CampaignTime.Now;

            // Scrutiny: -1 per configured quiet-service interval. Long campaign jumps
            // apply all elapsed intervals so bulk timeskip does not pin scrutiny at max.
            {
                var old = _state.Scrutiny;
                if (TryDecayDown(old, _state.LastScrutinyDecayTime, _state.LastScrutinyRaisedTime, cfg.ScrutinyDecayIntervalDays, 1,
                        EscalationState.ScrutinyMin, EscalationState.ScrutinyMax, now, out var updated, out var updatedTime,
                        out var decaySteps))
                {
                    _state.Scrutiny = updated;
                    _state.ScrutinyRecoveryFloor = updated;
                    _state.LastScrutinyDecayTime = updatedTime;
                    _lastScrutinyDecayDayNumber = (int)now.ToDays;
                    ModLogger.Info(LogCategory,
                        $"Scrutiny decayed: {old} -> {updated} (quiet service, intervals={decaySteps}, intervalDays={cfg.ScrutinyDecayIntervalDays})");
                    ShowScrutinyEasedMessage(old, updated);
                }
            }
        }

        public void ApplyMedicalRestDecay(bool isResting)
        {
            if (!IsEnabled())
            {
                return;
            }

            if (!isResting)
            {
                return;
            }

            var cfg = ConfigurationManager.LoadEscalationConfig() ?? new EscalationConfig();
            var now = CampaignTime.Now;

            // Medical risk: -1 per day of rest. (But "does not decay while condition persists untreated" is handled by caller.)
            var old = _state.MedicalRisk;
            if (TryDecayDownNoQuietRequirement(old, _state.LastMedicalRiskDecayTime, cfg.MedicalRiskDecayIntervalDays, 1,
                    EscalationState.MedicalRiskMin, EscalationState.MedicalRiskMax, now, out var updated, out var updatedTime))
            {
                _state.MedicalRisk = updated;
                _state.LastMedicalRiskDecayTime = updatedTime;
                ModLogger.Debug(LogCategory, $"Medical risk decayed: {old} -> {updated}");
            }
        }

        private static bool TryDecayDown(
            int value,
            CampaignTime lastDecayTime,
            CampaignTime lastRaisedTime,
            int intervalDays,
            int amount,
            int min,
            int max,
            CampaignTime now,
            out int updatedValue,
            out CampaignTime updatedLastDecayTime,
            out int decaySteps)
        {
            updatedValue = value;
            updatedLastDecayTime = lastDecayTime;
            decaySteps = 0;

            if (value <= min)
            {
                return false;
            }

            if (intervalDays <= 0 || amount <= 0)
            {
                return false;
            }

            // Require a quiet period since the last actual increase.
            if (lastRaisedTime != CampaignTime.Zero)
            {
                var quietDays = now.ToDays - lastRaisedTime.ToDays;
                if (quietDays < intervalDays)
                {
                    return false;
                }
            }

            var anchorTime = lastDecayTime;
            if (lastRaisedTime != CampaignTime.Zero &&
                (anchorTime == CampaignTime.Zero || lastRaisedTime.ToDays > anchorTime.ToDays))
            {
                anchorTime = lastRaisedTime;
            }

            if (anchorTime == CampaignTime.Zero)
            {
                decaySteps = 1;
            }
            else
            {
                var elapsedDays = now.ToDays - anchorTime.ToDays;
                if (elapsedDays < intervalDays)
                {
                    return false;
                }

                decaySteps = Math.Max(1, (int)Math.Floor(elapsedDays / intervalDays));
            }

            var totalDecay = decaySteps * amount;
            updatedValue = Clamp(value - totalDecay, min, max);
            updatedLastDecayTime = now;
            return updatedValue != value;
        }

        private static bool TryDecayDownNoQuietRequirement(
            int value,
            CampaignTime lastDecayTime,
            int intervalDays,
            int amount,
            int min,
            int max,
            CampaignTime now,
            out int updatedValue,
            out CampaignTime updatedLastDecayTime)
        {
            updatedValue = value;
            updatedLastDecayTime = lastDecayTime;

            if (value <= min)
            {
                return false;
            }

            if (intervalDays <= 0)
            {
                return false;
            }

            var sinceLastDecay = lastDecayTime == CampaignTime.Zero ? float.MaxValue : (now.ToDays - lastDecayTime.ToDays);
            if (sinceLastDecay < intervalDays)
            {
                return false;
            }

            updatedValue = Clamp(value - amount, min, max);
            updatedLastDecayTime = now;
            return updatedValue != value;
        }

        #endregion

        #region Readable status labels (for UI)

        public string GetScrutinyStatus()
        {
            var scrutiny = _state.Scrutiny;
            if (scrutiny <= 0)
            {
                return "Clean";
            }
            if (scrutiny <= 15)
            {
                return "Watched";
            }
            if (scrutiny <= 35)
            {
                return "Noticed";
            }
            if (scrutiny <= 55)
            {
                return "Hot";
            }
            if (scrutiny <= 75)
            {
                return "Burning";
            }
            return "Exposed";
        }

        /// <summary>
        /// Gets a readable status label for the player's relation with their enlisted lord.
        /// Now uses native Bannerlord reputation system (typically -100 to +100).
        /// </summary>
        public string GetLordReputationStatus()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true || enlistment.EnlistedLord == null)
            {
                return "Unknown";
            }

            var rep = CharacterRelationManager.GetHeroRelation(Hero.MainHero, enlistment.EnlistedLord);

            if (rep >= 80)
            {
                return "Celebrated";
            }
            if (rep >= 50)
            {
                return "Trusted";
            }
            if (rep >= 20)
            {
                return "Respected";
            }
            if (rep >= 5)
            {
                return "Promising";
            }
            if (rep >= -4)
            {
                return "Neutral";
            }
            if (rep >= -19)
            {
                return "Questionable";
            }
            if (rep >= -49)
            {
                return "Disliked";
            }
            return "Despised";
        }

        public string GetMedicalRiskStatus()
        {
            var risk = _state.MedicalRisk;
            if (risk <= 0)
            {
                return "None";
            }
            if (risk <= 2)
            {
                return "Mild";
            }
            if (risk == 3)
            {
                return "Concerning";
            }
            if (risk == 4)
            {
                return "Serious";
            }
            return "Critical";
        }

        #endregion

        #region Declined Promotion Tracking

        /// <summary>
        /// Records that the player declined a promotion to the specified tier.
        /// The player must then request promotion via dialog.
        /// </summary>
        public void RecordDeclinedPromotion(int tier)
        {
            _ = _declinedPromotions.Add(tier);
            ModLogger.Info(LogCategory, $"Recorded declined promotion to tier {tier}");
        }

        /// <summary>
        /// Checks if the player has previously declined promotion to the specified tier.
        /// </summary>
        public bool HasDeclinedPromotion(int tier)
        {
            return _declinedPromotions.Contains(tier);
        }

        /// <summary>
        /// Clears the declined promotion flag for the specified tier.
        /// Called when the player accepts the promotion via dialog.
        /// </summary>
        public void ClearDeclinedPromotion(int tier)
        {
            if (_declinedPromotions.Remove(tier))
            {
                ModLogger.Info(LogCategory, $"Cleared declined promotion flag for tier {tier}");
            }
        }

        /// <summary>
        /// Clears all declined promotion flags. Called when starting a new enlistment
        /// to give the player a fresh start with promotion eligibility.
        /// </summary>
        public void ClearAllDeclinedPromotions()
        {
            if (_declinedPromotions.Count > 0)
            {
                ModLogger.Info(LogCategory, $"Cleared {_declinedPromotions.Count} declined promotion flags for new enlistment");
                _declinedPromotions.Clear();
            }
        }

        #endregion

        private static string FormatReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? string.Empty : $" ({reason})";
        }

        private void ShowScrutinyChangeMessage(int delta, int oldValue, int newValue)
        {
            if (delta > 0 && newValue > oldValue)
            {
                var statusText = GetScrutinyStatus();
                var color = newValue >= EscalationThresholds.ScrutinyShakedown
                    ? TaleWorlds.Library.Colors.Red
                    : TaleWorlds.Library.Colors.Yellow;
                var msg = new TextObject("{=esc_scrutiny_changed}Scrutiny increased (+{DELTA}) - Status: {STATUS}");
                _ = msg.SetTextVariable("DELTA", newValue - oldValue);
                _ = msg.SetTextVariable("STATUS", statusText);
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage(msg.ToString(), color));
                return;
            }

            if (delta < 0 && newValue < oldValue)
            {
                ShowScrutinyEasedMessage(oldValue, newValue);
            }
        }

        private void ShowScrutinyEasedMessage(int oldValue, int newValue)
        {
            if (newValue >= oldValue)
            {
                return;
            }

            var msg = new TextObject("{=esc_scrutiny_eased}Scrutiny eased: {OLD} -> {NEW}");
            _ = msg.SetTextVariable("OLD", oldValue);
            _ = msg.SetTextVariable("NEW", newValue);
            TaleWorlds.Library.InformationManager.DisplayMessage(
                new TaleWorlds.Library.InformationMessage(msg.ToString(), TaleWorlds.Library.Colors.Green));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            return value > max ? max : value;
        }

        private static void LogTrackChange(string track, int oldValue, int newValue, string reason)
        {
            if (oldValue == newValue)
            {
                return;
            }

            var why = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" ({reason})";
            ModLogger.Info(LogCategory, $"{track}: {oldValue} -> {newValue}{why}");
        }

        /// <summary>
        ///     Checks if any thresholds were crossed and triggers threshold events when crossed upward.
        ///     Logs threshold crossings and queues appropriate events for delivery.
        /// </summary>
        private void CheckThresholdCrossing(string track, int oldValue, int newValue, int[] thresholds)
        {
            if (oldValue == newValue)
            {
                return;
            }

            foreach (var threshold in thresholds)
            {
                // Check if we crossed this threshold (either direction)
                bool crossedUp = oldValue < threshold && newValue >= threshold;
                bool crossedDown = oldValue >= threshold && newValue < threshold;

                if (crossedUp)
                {
                    ModLogger.Info(LogCategory, $"{track} crossed threshold {threshold} (increased from {oldValue} to {newValue})");
                    TryTriggerThresholdEvent(track, threshold);
                }
                else if (crossedDown)
                {
                    ModLogger.Info(LogCategory, $"{track} crossed threshold {threshold} (decreased from {oldValue} to {newValue})");
                }
            }
        }

        /// <summary>
        /// Attempts to trigger a threshold event when a track crosses a threshold upward.
        /// Maps track name and threshold to event ID and queues the event if it exists.
        /// Respects global event pacing limits to prevent event spam.
        /// </summary>
        private void TryTriggerThresholdEvent(string track, int threshold)
        {
            // Map track name to event ID pattern
            string eventId = track switch
            {
                "Scrutiny" => $"evt_scrutiny_{threshold}",
                "MedicalRisk" => $"evt_medical_{threshold}",
                _ => null
            };

            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            // Check global pacing limits before firing threshold event
            // Use "escalation" category for per-category cooldown tracking
            if (!Content.GlobalEventPacer.CanFireAutoEvent(eventId, "escalation", out var blockReason))
            {
                ModLogger.Debug(LogCategory, $"Threshold event {eventId} blocked by pacing: {blockReason}");
                return;
            }

            var evt = Content.EventCatalog.GetEvent(eventId);
            if (evt == null)
            {
                ModLogger.Debug(LogCategory, $"Threshold event not found: {eventId}");
                return;
            }

            string categoryKind = track switch
            {
                "Scrutiny" => "scrutiny",
                "MedicalRisk" => "medical",
                _ => track.ToLowerInvariant()
            };

            var director = Content.StoryDirector.Instance;
            if (director != null)
            {
                director.EmitCandidate(new Content.StoryCandidate
                {
                    SourceId = $"escalation.{categoryKind}.{threshold}",
                    CategoryId = $"escalation.{categoryKind}",
                    ProposedTier = Content.StoryTier.Modal,
                    SeverityHint = 0.80f,
                    Beats = { Content.StoryBeat.EscalationThreshold },
                    Relevance = new Content.RelevanceKey { TouchesEnlistedLord = true },
                    EmittedAt = CampaignTime.Now,
                    InteractiveEvent = evt,
                    RenderedTitle = evt.TitleFallback,
                    RenderedBody = evt.SetupFallback,
                    StoryKey = evt.Id
                });
                Content.GlobalEventPacer.RecordAutoEvent(eventId, "escalation");
                ModLogger.Info(LogCategory, $"Routed threshold event through Director: {eventId}");
            }
            else if (Content.EventDeliveryManager.Instance != null)
            {
                Content.EventDeliveryManager.Instance.QueueEvent(evt);
                Content.GlobalEventPacer.RecordAutoEvent(eventId, "escalation");
                ModLogger.Info(LogCategory, $"Queued threshold event (Director unavailable): {eventId}");
            }
            else
            {
                ModLogger.Debug(LogCategory, $"Threshold event cannot deliver: {eventId}");
            }
        }

        /// <summary>
        /// Generates a contextual message for lord reputation changes based on magnitude.
        /// </summary>
        private static string GetReputationChangeMessage(string ignoredTarget, int delta, int ignoredValue)
        {
            _ = ignoredTarget;
            _ = ignoredValue;
            if (delta >= 20)
            {
                return "Your lord took special notice of your recent performance";
            }
            else if (delta >= 10)
            {
                return "Your lord's confidence in you is growing";
            }
            else if (delta <= -20)
            {
                return "You've seriously disappointed your lord";
            }
            else // delta <= -10
            {
                return "Your lord's confidence in you has declined";
            }
        }
    }
}


