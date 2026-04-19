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

        public static EscalationManager Instance { get; private set; }

        private readonly EscalationState _state = new EscalationState();
        private int _lastDailyTickDayNumber = -1;
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
                ModLogger.Error(LogCategory, "Escalation daily tick failed", ex);
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

                dataStore.SyncData("esc_scrutiny", ref scrutiny);
                dataStore.SyncData("esc_lordRep", ref lordRep);
                dataStore.SyncData("esc_medical", ref medical);

                // Timestamps for decay logic
                var lastScrutinyRaised = _state.LastScrutinyRaisedTime;
                var lastScrutinyDecay = _state.LastScrutinyDecayTime;
                var lastMedicalDecay = _state.LastMedicalRiskDecayTime;
                var lastThresholdEvent = _state.LastThresholdEventTime;

                dataStore.SyncData("esc_lastScrutinyRaised", ref lastScrutinyRaised);
                dataStore.SyncData("esc_lastScrutinyDecay", ref lastScrutinyDecay);
                dataStore.SyncData("esc_lastMedicalDecay", ref lastMedicalDecay);
                dataStore.SyncData("esc_lastThresholdEvent", ref lastThresholdEvent);

                // Pending threshold event
                var pendingThreshold = _state.PendingThresholdStoryId ?? string.Empty;
                dataStore.SyncData("esc_pendingThreshold", ref pendingThreshold);

                // Per-threshold cooldown map
                var thresholdKeys = (_state.ThresholdStoryLastFired ?? Enumerable.Empty<System.Collections.Generic.KeyValuePair<string, CampaignTime>>())
                    .Select(k => k.Key)
                    .ToList();
                var thresholdCount = thresholdKeys.Count;
                dataStore.SyncData("esc_thresholdCount", ref thresholdCount);

                // Event cooldown map (same pattern as threshold cooldowns)
                var eventKeys = (_state.EventLastFired ?? Enumerable.Empty<System.Collections.Generic.KeyValuePair<string, CampaignTime>>())
                    .Select(k => k.Key)
                    .ToList();
                var eventCooldownCount = eventKeys.Count;
                dataStore.SyncData("esc_eventCooldownCount", ref eventCooldownCount);

                // One-time events fired
                var oneTimeKeys = (_state.OneTimeEventsFired ?? Enumerable.Empty<string>()).ToList();
                var oneTimeCount = oneTimeKeys.Count;
                dataStore.SyncData("esc_oneTimeCount", ref oneTimeCount);

                if (dataStore.IsLoading)
                {
                    _state.Scrutiny = scrutiny;
                    _state.LordReputation = lordRep;
                    _state.MedicalRisk = medical;

                    _state.LastScrutinyRaisedTime = lastScrutinyRaised;
                    _state.LastScrutinyDecayTime = lastScrutinyDecay;
                    _state.LastMedicalRiskDecayTime = lastMedicalDecay;
                    _state.LastThresholdEventTime = lastThresholdEvent;

                    _state.PendingThresholdStoryId = pendingThreshold;
                    _state.ThresholdStoryLastFired = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < thresholdCount; i++)
                    {
                        var key = string.Empty;
                        var time = CampaignTime.Zero;
                        dataStore.SyncData($"esc_threshold_{i}_id", ref key);
                        dataStore.SyncData($"esc_threshold_{i}_time", ref time);
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
                        dataStore.SyncData($"esc_event_{i}_id", ref key);
                        dataStore.SyncData($"esc_event_{i}_time", ref time);
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
                        dataStore.SyncData($"esc_onetime_{i}", ref eventId);
                        if (!string.IsNullOrWhiteSpace(eventId))
                        {
                            _state.OneTimeEventsFired.Add(eventId);
                        }
                    }

                    _state.ClampAll();
                }
                else
                {
                    // Store in a stable order to reduce churn
                    thresholdKeys.Sort(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < thresholdKeys.Count; i++)
                    {
                        var key = thresholdKeys[i];
                        var time = _state.ThresholdStoryLastFired.TryGetValue(key, out var t) ? t : CampaignTime.Zero;
                        dataStore.SyncData($"esc_threshold_{i}_id", ref key);
                        dataStore.SyncData($"esc_threshold_{i}_time", ref time);
                    }

                    // Save event cooldown map
                    eventKeys.Sort(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < eventKeys.Count; i++)
                    {
                        var key = eventKeys[i];
                        var time = _state.EventLastFired.TryGetValue(key, out var t) ? t : CampaignTime.Zero;
                        dataStore.SyncData($"esc_event_{i}_id", ref key);
                        dataStore.SyncData($"esc_event_{i}_time", ref time);
                    }

                    // Save one-time events set
                    oneTimeKeys.Sort(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < oneTimeKeys.Count; i++)
                    {
                        var eventId = oneTimeKeys[i];
                        dataStore.SyncData($"esc_onetime_{i}", ref eventId);
                    }
                }

                // Global event pacing state (prevents event spam across all automatic sources)
                var lastAutoEvent = _state.LastAutoEventTime;
                var autoEventsToday = _state.AutoEventsToday;
                var autoEventDayNum = _state.AutoEventDayNumber;
                var autoEventsWeek = _state.AutoEventsThisWeek;
                var autoEventWeekNum = _state.AutoEventWeekNumber;
                var isQuietDay = _state.IsQuietDay;

                dataStore.SyncData("esc_lastAutoEvent", ref lastAutoEvent);
                dataStore.SyncData("esc_autoEventsToday", ref autoEventsToday);
                dataStore.SyncData("esc_autoEventDayNum", ref autoEventDayNum);
                dataStore.SyncData("esc_autoEventsWeek", ref autoEventsWeek);
                dataStore.SyncData("esc_autoEventWeekNum", ref autoEventWeekNum);
                dataStore.SyncData("esc_isQuietDay", ref isQuietDay);

                // Category cooldown map (tracks last fired time per category)
                var categoryKeys = (_state.CategoryLastFired ?? Enumerable.Empty<System.Collections.Generic.KeyValuePair<string, CampaignTime>>())
                    .Select(k => k.Key)
                    .ToList();
                var categoryCount = categoryKeys.Count;
                dataStore.SyncData("esc_categoryCooldownCount", ref categoryCount);

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
                        dataStore.SyncData($"esc_category_{i}_id", ref key);
                        dataStore.SyncData($"esc_category_{i}_time", ref time);
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
                        dataStore.SyncData($"esc_category_{i}_id", ref key);
                        dataStore.SyncData($"esc_category_{i}_time", ref time);
                    }
                }

                // Declined promotion tracking
                var declinedCount = _declinedPromotions.Count;
                dataStore.SyncData("esc_declinedPromotionsCount", ref declinedCount);

                if (dataStore.IsLoading)
                {
                    _declinedPromotions = new HashSet<int>();
                    for (var i = 0; i < declinedCount; i++)
                    {
                        var tier = 0;
                        dataStore.SyncData($"esc_declinedPromo_{i}", ref tier);
                        if (tier > 0)
                        {
                            _declinedPromotions.Add(tier);
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
                        dataStore.SyncData($"esc_declinedPromo_{i}", ref tier);
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
            var next = oldValue + delta;
            _state.Scrutiny = Clamp(next, EscalationState.ScrutinyMin, EscalationState.ScrutinyMax);

            if (delta > 0)
            {
                // Scrutiny decay requires a "quiet period" (no corrupt choices) since the last raise.
                _state.LastScrutinyRaisedTime = CampaignTime.Now;
            }

            LogTrackChange("Scrutiny", oldValue, _state.Scrutiny, reason);
            CheckThresholdCrossing("Scrutiny", oldValue, _state.Scrutiny, new[] { 
                EscalationThresholds.ScrutinyWarning, 
                EscalationThresholds.ScrutinyShakedown, 
                EscalationThresholds.ScrutinyAudit, 
                EscalationThresholds.ScrutinyExposed,
                EscalationThresholds.ScrutinyCritical 
            });

            // Show UI notification for scrutiny changes (only when increasing - "attention" from authorities)
            if (_state.Scrutiny != oldValue && delta > 0)
            {
                var statusText = GetScrutinyStatus();
                var color = _state.Scrutiny >= EscalationThresholds.ScrutinyShakedown
                    ? TaleWorlds.Library.Colors.Red
                    : TaleWorlds.Library.Colors.Yellow;
                var msg = new TextObject("{=esc_scrutiny_changed}Scrutiny increased (+{DELTA}) - Status: {STATUS}");
                msg.SetTextVariable("DELTA", delta);
                msg.SetTextVariable("STATUS", statusText);
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage(msg.ToString(), color));
            }

            EvaluateThresholdsAndQueueIfNeeded();
        }

        /// <summary>
        /// Modifies the player's relation with their enlisted lord using native Bannerlord reputation system.
        /// This now directly modifies Hero.GetRelation() rather than a custom LordReputation track.
        /// </summary>
        public void ModifyLordReputation(int delta, string reason = null)
        {
            if (!IsEnabled())
            {
                return;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true || enlistment.EnlistedLord == null)
            {
                return;
            }

            var lord = enlistment.EnlistedLord;
            var oldValue = CharacterRelationManager.GetHeroRelation(Hero.MainHero, lord);
            ChangeRelationAction.ApplyPlayerRelation(lord, delta);
            var newValue = CharacterRelationManager.GetHeroRelation(Hero.MainHero, lord);

            LogTrackChange("LordReputation", oldValue, newValue, reason);

            // Report significant changes to news system
            if (Math.Abs(delta) >= 10 && EnlistedNewsBehavior.Instance != null)
            {
                string message = GetReputationChangeMessage("Lord", delta, newValue);
                EnlistedNewsBehavior.Instance.AddReputationChange(
                    target: "Lord",
                    delta: delta,
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

            // Scrutiny: -1 per 7 days with no corrupt choices (0-100 scale).
            // Note: Using 1 point decay to match config settings. Scale is 10x larger but keeps same decay rate.
            {
                var old = _state.Scrutiny;
                if (TryDecayDown(old, _state.LastScrutinyDecayTime, _state.LastScrutinyRaisedTime, cfg.ScrutinyDecayIntervalDays, 1,
                        EscalationState.ScrutinyMin, EscalationState.ScrutinyMax, now, out var updated, out var updatedTime))
                {
                    _state.Scrutiny = updated;
                    _state.LastScrutinyDecayTime = updatedTime;
                    ModLogger.Debug(LogCategory, $"Scrutiny decayed: {old} -> {updated}");
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

            // Require a quiet period since last raise.
            if (lastRaisedTime != CampaignTime.Zero)
            {
                var quietDays = now.ToDays - lastRaisedTime.ToDays;
                if (quietDays < intervalDays)
                {
                    return false;
                }
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

        private static bool TryDecayTowardZero(
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

            if (value == 0)
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

            if (value > 0)
            {
                updatedValue = Clamp(value - amount, min, max);
            }
            else
            {
                updatedValue = Clamp(value + amount, min, max);
            }

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
            _declinedPromotions.Add(tier);
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
            if (evt != null && Content.EventDeliveryManager.Instance != null)
            {
                Content.EventDeliveryManager.Instance.QueueEvent(evt);

                // Record in global pacer to track daily/weekly limits
                Content.GlobalEventPacer.RecordAutoEvent(eventId, "escalation");

                ModLogger.Info(LogCategory, $"Queued threshold event: {eventId}");
            }
            else
            {
                ModLogger.Debug(LogCategory, $"Threshold event not found: {eventId}");
            }
        }

        /// <summary>
        /// Generates a contextual message for lord reputation changes based on magnitude.
        /// </summary>
        private static string GetReputationChangeMessage(string target, int delta, int _)
        {
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


