using System;
using System.IO;
using EnlistedConfig = Enlisted.Mod.Core.Config.ConfigurationManager;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Mod.Core.Logging;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Conditions
{
    /// <summary>
    /// Manages the player condition system, including injuries, illnesses, and exhaustion.
    ///
    /// This system is feature-flagged and only active while the player is enlisted. 
    /// It uses safe persistence by storing only primitives and strings, and integrates 
    /// with the MedicalRisk escalation track where untreated conditions increase risk 
    /// and treatment resets it.
    /// </summary>
    public sealed class PlayerConditionBehavior : CampaignBehaviorBase
    {
        private const string LogCategory = "PlayerConditions";

        private readonly PlayerConditionState _state = new PlayerConditionState();
        private PlayerConditionDefinitionsJson _cachedDefs;
        private int _lastDailyTickDayNumber = -1;

        public static PlayerConditionBehavior Instance { get; private set; }

        public PlayerConditionBehavior()
        {
            Instance = this;
        }

        public PlayerConditionState State => _state;

        public bool IsEnabled()
        {
            return EnlistedConfig.LoadPlayerConditionsConfig()?.Enabled == true;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                // Injury
                var injSeverity = (int)_state.CurrentInjury;
                var injType = _state.InjuryType ?? string.Empty;
                var injDays = _state.InjuryDaysRemaining;

                // Illness
                var illSeverity = (int)_state.CurrentIllness;
                var illType = _state.IllnessType ?? string.Empty;
                var illDays = _state.IllnessDaysRemaining;

                // Exhaustion
                var exhaustion = (int)_state.Exhaustion;

                // Treatment
                var underCare = _state.UnderMedicalCare;
                var recoveryMult = _state.RecoveryRateModifier;

                dataStore.SyncData("pc_injSeverity", ref injSeverity);
                dataStore.SyncData("pc_injType", ref injType);
                dataStore.SyncData("pc_injDays", ref injDays);

                dataStore.SyncData("pc_illSeverity", ref illSeverity);
                dataStore.SyncData("pc_illType", ref illType);
                dataStore.SyncData("pc_illDays", ref illDays);

                dataStore.SyncData("pc_exhaust", ref exhaustion);
                dataStore.SyncData("pc_underCare", ref underCare);
                dataStore.SyncData("pc_recoveryMult", ref recoveryMult);

                if (dataStore.IsLoading)
                {
                    _state.CurrentInjury = (InjurySeverity)ClampEnum(injSeverity, 0, 4);
                    _state.InjuryType = injType ?? string.Empty;
                    _state.InjuryDaysRemaining = Math.Max(0, injDays);

                    _state.CurrentIllness = (IllnessSeverity)ClampEnum(illSeverity, 0, 4);
                    _state.IllnessType = illType ?? string.Empty;
                    _state.IllnessDaysRemaining = Math.Max(0, illDays);

                    _state.Exhaustion = (ExhaustionLevel)ClampEnum(exhaustion, 0, 4);

                    _state.UnderMedicalCare = underCare;
                    _state.RecoveryRateModifier = Math.Max(0.1f, recoveryMult);

                    // Detect stale condition data (severity without days) before normalization
                    if ((_state.CurrentInjury != InjurySeverity.None && _state.InjuryDaysRemaining <= 0) ||
                        (_state.CurrentIllness != IllnessSeverity.None && _state.IllnessDaysRemaining <= 0))
                    {
                        ModLogger.Warn(LogCategory, 
                            $"Detected stale condition data on load: Injury={_state.CurrentInjury}({_state.InjuryDaysRemaining}d), " +
                            $"Illness={_state.CurrentIllness}({_state.IllnessDaysRemaining}d). Normalizing...");
                    }

                    NormalizeState();
                    
                    // Log loaded state for diagnostics
                    if (_state.HasAnyCondition)
                    {
                        ModLogger.Info(LogCategory, 
                            $"Loaded active conditions: Injury={_state.CurrentInjury}({_state.InjuryDaysRemaining}d), " +
                            $"Illness={_state.CurrentIllness}({_state.IllnessDaysRemaining}d)");
                    }
                }
            });
        }

        /// <summary>
        /// Ensures condition state is consistent. Clears any severity values 
        /// when days remaining is 0 or negative, preventing stale data from 
        /// triggering decision requirements or displaying incorrect status.
        /// </summary>
        private void NormalizeState()
        {
            if (_state.InjuryDaysRemaining <= 0)
            {
                _state.CurrentInjury = InjurySeverity.None;
                _state.InjuryType = string.Empty;
                _state.InjuryDaysRemaining = 0; // Ensure no negative values
            }

            if (_state.IllnessDaysRemaining <= 0)
            {
                _state.CurrentIllness = IllnessSeverity.None;
                _state.IllnessType = string.Empty;
                _state.IllnessDaysRemaining = 0; // Ensure no negative values
            }

            if (!_state.HasAnyCondition)
            {
                _state.ClearTreatment();
            }
        }

        private void OnDailyTick()
        {
            try
            {
                if (!IsEnabled())
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                var dayNumber = (int)CampaignTime.Now.ToDays;
                if (dayNumber == _lastDailyTickDayNumber)
                {
                    return;
                }
                _lastDailyTickDayNumber = dayNumber;

                ApplyDailyRecoveryAndRisk();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PlayerConditions", "Daily tick failed", ex);
            }
        }

        private void ApplyDailyRecoveryAndRisk()
        {
            if (!_state.HasAnyCondition)
            {
                return;
            }

            var recoveryMult = Math.Max(0.1f, _state.RecoveryRateModifier);
            var recoveryTicks = _state.UnderMedicalCare ? Math.Max(1, (int)Math.Round(recoveryMult)) : 1;

            // Track if illness is about to heal
            var hadIllness = _state.HasIllness;
            var illnessSeverity = _state.CurrentIllness;

            if (_state.HasInjury)
            {
                _state.InjuryDaysRemaining = Math.Max(0, _state.InjuryDaysRemaining - recoveryTicks);
            }

            if (_state.HasIllness)
            {
                _state.IllnessDaysRemaining = Math.Max(0, _state.IllnessDaysRemaining - recoveryTicks);
            }

            // Medical risk rises slowly when conditions are untreated. Treatment resets it (see ApplyTreatment).
            if ((_state.HasInjury || _state.HasIllness) && !_state.UnderMedicalCare)
            {
                EscalationManager.Instance?.ModifyMedicalRisk(1, "condition_untreated");
            }

            var recoveredAny = false;
            var recoveredFromIllness = false;
            
            if (_state.InjuryDaysRemaining <= 0 && _state.CurrentInjury != InjurySeverity.None)
            {
                recoveredAny = true;
            }
            if (_state.IllnessDaysRemaining <= 0 && _state.CurrentIllness != IllnessSeverity.None)
            {
                recoveredAny = true;
                recoveredFromIllness = hadIllness;
            }

            NormalizeState();

            // Restore HP when illness heals (Phase 6G)
            if (recoveredFromIllness && !_state.HasIllness)
            {
                RestoreHpOnIllnessRecovery(illnessSeverity);
            }

            if (recoveredAny)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=cond_recovered}You've recovered.").ToString(),
                    Colors.Green));
            }
            else
            {
                // Daily illness status messaging (Phase 6G)
                DisplayDailyConditionStatus();
            }
        }

        /// <summary>
        /// Restores HP when the player recovers from illness.
        /// </summary>
        private void RestoreHpOnIllnessRecovery(IllnessSeverity previousSeverity)
        {
            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            var maxHp = hero.CharacterObject.MaxHitPoints();
            
            // Restore a portion of max HP based on previous severity
            var restoration = previousSeverity switch
            {
                IllnessSeverity.Mild => (int)(maxHp * 0.05f),
                IllnessSeverity.Moderate => (int)(maxHp * 0.10f),
                IllnessSeverity.Severe => (int)(maxHp * 0.15f),
                IllnessSeverity.Critical => (int)(maxHp * 0.20f),
                _ => 0
            };

            if (restoration > 0)
            {
                hero.HitPoints = Math.Min(maxHp, hero.HitPoints + restoration);
                ModLogger.Info(LogCategory, $"Illness recovery HP restored: +{restoration} HP, new HP: {hero.HitPoints}");
            }
        }

        /// <summary>
        /// Displays daily combat log messages about current condition status.
        /// </summary>
        private void DisplayDailyConditionStatus()
        {
            if (!_state.HasAnyCondition)
            {
                return;
            }

            // Display illness status
            if (_state.HasIllness)
            {
                var color = _state.CurrentIllness switch
                {
                    IllnessSeverity.Mild => Color.FromUint(0xFFCCCC88), // Pale yellow
                    IllnessSeverity.Moderate => Color.FromUint(0xFFCC9944), // Orange
                    IllnessSeverity.Severe => Color.FromUint(0xFFCC4444), // Red
                    IllnessSeverity.Critical => Color.FromUint(0xFFFF2222), // Bright red
                    _ => Colors.White
                };

                var severityText = GetIllnessSeverityLabel(_state.CurrentIllness).ToString();
                var daysText = _state.IllnessDaysRemaining == 1 ? "day" : "days";
                var msg = $"Illness ({severityText}): {_state.IllnessDaysRemaining} {daysText} remaining.";
                
                InformationManager.DisplayMessage(new InformationMessage(msg, color));
            }

            // Display injury status
            if (_state.HasInjury)
            {
                var color = _state.CurrentInjury switch
                {
                    InjurySeverity.Minor => Color.FromUint(0xFFCCCC88),
                    InjurySeverity.Moderate => Color.FromUint(0xFFCC9944),
                    InjurySeverity.Severe => Color.FromUint(0xFFCC4444),
                    InjurySeverity.Critical => Color.FromUint(0xFFFF2222),
                    _ => Colors.White
                };

                var severityText = GetInjurySeverityLabel(_state.CurrentInjury).ToString();
                var daysText = _state.InjuryDaysRemaining == 1 ? "day" : "days";
                var msg = $"Injury ({severityText}): {_state.InjuryDaysRemaining} {daysText} remaining.";
                
                InformationManager.DisplayMessage(new InformationMessage(msg, color));
            }
        }

        public bool CanTrain()
        {
            if (!IsEnabled())
            {
                return true;
            }

            if (_state.CurrentInjury >= InjurySeverity.Severe)
            {
                return false;
            }

            if (_state.CurrentIllness >= IllnessSeverity.Severe)
            {
                return false;
            }

            if (EnlistedConfig.LoadPlayerConditionsConfig()?.ExhaustionEnabled == true &&
                _state.Exhaustion >= ExhaustionLevel.Broken)
            {
                return false;
            }

            return true;
        }

        public void ApplyTreatment(float recoveryMultiplier, string reason)
        {
            if (!IsEnabled())
            {
                return;
            }

            if (!_state.HasAnyCondition)
            {
                return;
            }

            _state.UnderMedicalCare = true;
            _state.RecoveryRateModifier = Math.Max(1.0f, recoveryMultiplier);

            // Treatment resets medical risk pressure.
            EscalationManager.Instance?.ResetMedicalRisk(reason ?? "treatment");

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=cond_treatment_applied}Treatment applied.").ToString(),
                Colors.White));
        }

        public void TryApplyInjury(string injuryType, InjurySeverity severity, int days, string reason)
        {
            if (!IsEnabled())
            {
                return;
            }

            if (severity == InjurySeverity.None || days <= 0)
            {
                return;
            }

            // If already injured, upgrade severity or extend.
            if (_state.HasInjury)
            {
                if (severity > _state.CurrentInjury)
                {
                    _state.CurrentInjury = severity;
                    _state.InjuryType = injuryType ?? string.Empty;
                    _state.InjuryDaysRemaining = Math.Max(_state.InjuryDaysRemaining, days);
                }
                else
                {
                    _state.InjuryDaysRemaining = Math.Max(_state.InjuryDaysRemaining, days);
                }
            }
            else
            {
                _state.CurrentInjury = severity;
                _state.InjuryType = injuryType ?? string.Empty;
                _state.InjuryDaysRemaining = days;
            }

            _state.ClearTreatment(); // new injury cancels current treatment until reapplied

            var label = GetInjuryLabel(_state.InjuryType);
            var severityText = GetInjurySeverityLabel(severity);
            var msg = new TextObject("{=cond_injury_gained}You suffered a {SEVERITY} injury: {INJURY}.");
            msg.SetTextVariable("SEVERITY", severityText);
            msg.SetTextVariable("INJURY", label);

            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
            ModLogger.Info(LogCategory, $"Injury applied ({severity} {injuryType}, days={days}, why={reason})");
        }

        public void TryApplyIllness(string illnessType, IllnessSeverity severity, int days, string reason)
        {
            if (!IsEnabled())
            {
                return;
            }

            if (severity == IllnessSeverity.None || days <= 0)
            {
                return;
            }

            var wasIll = _state.HasIllness;
            var previousSeverity = _state.CurrentIllness;

            if (_state.HasIllness)
            {
                if (severity > _state.CurrentIllness)
                {
                    _state.CurrentIllness = severity;
                    _state.IllnessType = illnessType ?? string.Empty;
                    _state.IllnessDaysRemaining = Math.Max(_state.IllnessDaysRemaining, days);
                }
                else
                {
                    _state.IllnessDaysRemaining = Math.Max(_state.IllnessDaysRemaining, days);
                }
            }
            else
            {
                _state.CurrentIllness = severity;
                _state.IllnessType = illnessType ?? string.Empty;
                _state.IllnessDaysRemaining = days;
            }

            _state.ClearTreatment();

            // Apply HP reduction based on illness severity (Phase 6G)
            // HP floor is 30% of max HP - illness can weaken but not kill
            if (!wasIll || severity > previousSeverity)
            {
                ApplyIllnessHpReduction(severity);
            }

            var label = GetIllnessLabel(_state.IllnessType);
            var severityText = GetIllnessSeverityLabel(severity);
            var msg = new TextObject("{=cond_illness_gained}You fell ill ({SEVERITY}): {ILLNESS}.");
            msg.SetTextVariable("SEVERITY", severityText);
            msg.SetTextVariable("ILLNESS", label);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
            ModLogger.Info(LogCategory, $"Illness applied ({severity} {illnessType}, days={days}, why={reason})");
        }

        /// <summary>
        /// Applies HP reduction based on illness severity.
        /// HP floor is 30% of max HP - illness can weaken but not kill.
        /// </summary>
        private void ApplyIllnessHpReduction(IllnessSeverity severity)
        {
            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            var maxHp = hero.CharacterObject.MaxHitPoints();
            var hpFloor = (int)(maxHp * 0.30f);

            // HP reduction by severity
            var reduction = severity switch
            {
                IllnessSeverity.Mild => 0,
                IllnessSeverity.Moderate => (int)(maxHp * 0.10f),
                IllnessSeverity.Severe => (int)(maxHp * 0.25f),
                IllnessSeverity.Critical => (int)(maxHp * 0.40f),
                _ => 0
            };

            if (reduction <= 0)
            {
                return;
            }

            var newHp = hero.HitPoints - reduction;
            hero.HitPoints = Math.Max(hpFloor, newHp);
            
            ModLogger.Info(LogCategory, $"Illness HP reduction: -{reduction} HP (floor: {hpFloor}), new HP: {hero.HitPoints}");
        }

        internal PlayerConditionDefinitionsJson LoadDefinitions()
        {
            if (_cachedDefs != null)
            {
                return _cachedDefs;
            }

            try
            {
                var cfg = EnlistedConfig.LoadPlayerConditionsConfig() ?? new Mod.Core.Config.PlayerConditionsConfig();
                var rel = cfg.DefinitionsFile ?? "Conditions\\condition_defs.json";
                var path = Path.Combine(EnlistedConfig.GetModuleDataPathForConsumers(), rel);
                if (!File.Exists(path))
                {
                    ModLogger.Warn(LogCategory, $"Condition definitions missing at: {path}");
                    return null;
                }

                var json = File.ReadAllText(path);
                var parsed = JsonConvert.DeserializeObject<PlayerConditionDefinitionsJson>(json);
                if (parsed?.SchemaVersion != 1)
                {
                    ModLogger.Warn(LogCategory, "Condition definitions schema mismatch (expected 1)");
                    return null;
                }

                _cachedDefs = parsed;
                return _cachedDefs;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PlayerConditions", "Failed to load condition definitions", ex);
                return null;
            }
        }

        public int GetBaseRecoveryDaysForInjury(string injuryType, InjurySeverity severity)
        {
            var defs = LoadDefinitions();
            if (defs?.Injuries == null || string.IsNullOrWhiteSpace(injuryType) || severity == InjurySeverity.None)
            {
                return 0;
            }

            if (!defs.Injuries.TryGetValue(injuryType, out var def) || def?.BaseRecoveryDays == null)
            {
                return 0;
            }

            return severity switch
            {
                InjurySeverity.Minor => def.BaseRecoveryDays.Minor,
                InjurySeverity.Moderate => def.BaseRecoveryDays.Moderate,
                InjurySeverity.Severe => def.BaseRecoveryDays.Severe,
                InjurySeverity.Critical => Math.Max(def.BaseRecoveryDays.Critical, def.BaseRecoveryDays.Severe),
                _ => 0
            };
        }

        public int GetBaseRecoveryDaysForIllness(string illnessType, IllnessSeverity severity)
        {
            var defs = LoadDefinitions();
            if (defs?.Illnesses == null || string.IsNullOrWhiteSpace(illnessType) || severity == IllnessSeverity.None)
            {
                return 0;
            }

            if (!defs.Illnesses.TryGetValue(illnessType, out var def) || def?.BaseRecoveryDays == null)
            {
                return 0;
            }

            return severity switch
            {
                IllnessSeverity.Mild => def.BaseRecoveryDays.Mild,
                IllnessSeverity.Moderate => def.BaseRecoveryDays.Moderate,
                IllnessSeverity.Severe => def.BaseRecoveryDays.Severe,
                IllnessSeverity.Critical => Math.Max(def.BaseRecoveryDays.Critical, def.BaseRecoveryDays.Severe),
                _ => 0
            };
        }

        private TextObject GetInjuryLabel(string injuryType)
        {
            var defs = LoadDefinitions();
            if (defs?.Injuries == null || string.IsNullOrWhiteSpace(injuryType) ||
                !defs.Injuries.TryGetValue(injuryType, out var def) || def == null)
            {
                return new TextObject("{=cond_injury_generic}Injury");
            }

            var embeddedFallback = def.DisplayNameFallback ?? "Injury";
            return new TextObject("{=" + (def.DisplayNameId ?? string.Empty) + "}" + embeddedFallback);
        }

        private TextObject GetIllnessLabel(string illnessType)
        {
            var defs = LoadDefinitions();
            if (defs?.Illnesses == null || string.IsNullOrWhiteSpace(illnessType) ||
                !defs.Illnesses.TryGetValue(illnessType, out var def) || def == null)
            {
                return new TextObject("{=cond_illness_generic}Illness");
            }

            var embeddedFallback = def.DisplayNameFallback ?? "Illness";
            return new TextObject("{=" + (def.DisplayNameId ?? string.Empty) + "}" + embeddedFallback);
        }

        private static TextObject GetInjurySeverityLabel(InjurySeverity severity)
        {
            return severity switch
            {
                InjurySeverity.Minor => new TextObject("{=cond_sev_injury_minor}minor"),
                InjurySeverity.Moderate => new TextObject("{=cond_sev_injury_moderate}moderate"),
                InjurySeverity.Severe => new TextObject("{=cond_sev_injury_severe}severe"),
                InjurySeverity.Critical => new TextObject("{=cond_sev_injury_critical}critical"),
                _ => new TextObject(string.Empty)
            };
        }

        private static TextObject GetIllnessSeverityLabel(IllnessSeverity severity)
        {
            return severity switch
            {
                IllnessSeverity.Mild => new TextObject("{=cond_sev_ill_mild}mild"),
                IllnessSeverity.Moderate => new TextObject("{=cond_sev_ill_moderate}moderate"),
                IllnessSeverity.Severe => new TextObject("{=cond_sev_ill_severe}severe"),
                IllnessSeverity.Critical => new TextObject("{=cond_sev_ill_critical}critical"),
                _ => new TextObject(string.Empty)
            };
        }

        private static int ClampEnum(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            return value > max ? max : value;
        }
    }
}


