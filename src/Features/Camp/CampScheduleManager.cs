using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Company;
using Enlisted.Features.Content;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Camp
{
    /// <summary>
    /// Manages the daily camp routine schedule. Provides baseline schedules for each day phase
    /// and calculates deviations based on world state and company pressure.
    /// 
    /// The schedule creates a predictable military rhythm: dawn formations, midday work,
    /// dusk social time, night rest. World conditions cause deviations from this baseline,
    /// signaling to the player that something unusual is happening.
    /// </summary>
    public class CampScheduleManager
    {
        private const string LogCategory = "CampSchedule";
        private const string ConfigPath = "../../ModuleData/Enlisted/Config/camp_schedule.json";

        /// <summary>Singleton instance for external access.</summary>
        public static CampScheduleManager Instance { get; private set; }

        // Schedule configuration loaded from JSON
        private JObject _scheduleConfig;
        private bool _configLoaded;

        // Current phase schedule (cached for performance)
        private ScheduledPhase _currentSchedule;
        private DayPhase _currentCachedPhase = DayPhase.Night;

        // Active deviations from baseline
        private List<ScheduleDeviation> _activeDeviations = new List<ScheduleDeviation>();

        // Schedule boost multiplier for matching opportunities
        private float _scheduleBoostMultiplier = 1.3f;

        // Category to opportunity type mappings
        private Dictionary<string, List<string>> _categoryMappings = new Dictionary<string, List<string>>();

        public CampScheduleManager()
        {
            Instance = this;
            LoadConfig();
        }

        /// <summary>
        /// Gets the schedule boost multiplier for opportunities matching the current schedule.
        /// </summary>
        public float ScheduleBoostMultiplier => _scheduleBoostMultiplier;

        /// <summary>
        /// Gets the current schedule for this phase.
        /// </summary>
        public ScheduledPhase CurrentSchedule => _currentSchedule;

        /// <summary>
        /// True if the current schedule has deviated from baseline.
        /// </summary>
        public bool HasDeviation => _currentSchedule?.HasDeviation ?? false;

        /// <summary>
        /// Gets the reason for current deviation, if any.
        /// </summary>
        public string DeviationReason => _currentSchedule?.DeviationReason;

        /// <summary>
        /// Gets the baseline schedule for a day phase with all deviations applied.
        /// First checks if the orchestrator wants to inject an override activity.
        /// </summary>
        public ScheduledPhase GetScheduleForPhase(DayPhase phase)
        {
            var baseline = GetBaselineForPhase(phase);
            var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
            var companyNeeds = EnlistmentBehavior.Instance?.CompanyNeeds;

            // Check for orchestrator override first (foraging when low supplies, variety injections, etc.)
            var orchestratorOverride = ContentOrchestrator.Instance?.CheckForScheduleOverride(phase);
            if (orchestratorOverride != null)
            {
                ApplyOrchestratorOverride(baseline, orchestratorOverride);
                ApplyPlayerCommitments(baseline);
                return baseline;
            }

            ApplyActivityModifiers(baseline, worldSituation.ExpectedActivity);
            ApplyLordSituationModifiers(baseline, worldSituation.LordIs);
            ApplyPressureOverrides(baseline, companyNeeds, worldSituation);
            ApplyPlayerCommitments(baseline);

            return baseline;
        }

        /// <summary>
        /// Applies an orchestrator override to the schedule, replacing scheduled activities.
        /// </summary>
        private void ApplyOrchestratorOverride(ScheduledPhase schedule, OrchestratorOverride orchestratorOverride)
        {
            // Replace slot 1 with override activity
            schedule.Slot1Category = orchestratorOverride.ActivityCategory;
            schedule.Slot1Description = orchestratorOverride.ActivityName;
            schedule.Slot1Weight = 2.0f; // High weight since this is an override
            schedule.Slot1Skipped = false;

            // Optionally replace slot 2 as well
            if (orchestratorOverride.ReplaceBothSlots)
            {
                schedule.Slot2Category = orchestratorOverride.ActivityCategory;
                schedule.Slot2Description = orchestratorOverride.Description ?? orchestratorOverride.ActivityName;
                schedule.Slot2Weight = 1.5f;
                schedule.Slot2Skipped = false;
            }

            // Mark deviation with override reason
            schedule.DeviationReason = orchestratorOverride.Reason;
            schedule.FlavorText = orchestratorOverride.ActivationText ?? schedule.FlavorText;

            // Store override reference for processor
            schedule.OrchestratorOverride = orchestratorOverride;

            ModLogger.Debug(LogCategory, 
                $"Applied orchestrator override: {orchestratorOverride.ActivityName} ({orchestratorOverride.Reason})");
        }

        /// <summary>
        /// Called when day phase changes to refresh the current schedule.
        /// </summary>
        public void OnPhaseChanged(DayPhase newPhase)
        {
            if (newPhase == _currentCachedPhase && _currentSchedule != null)
            {
                return; // Already cached
            }

            _currentSchedule = GetScheduleForPhase(newPhase);
            _currentCachedPhase = newPhase;

            if (_currentSchedule.HasDeviation)
            {
                ModLogger.Info(LogCategory, 
                    $"Schedule deviation for {newPhase}: {_currentSchedule.DeviationReason}");
            }
            else
            {
                ModLogger.Debug(LogCategory, 
                    $"Normal schedule for {newPhase}: {_currentSchedule.Slot1Description}, {_currentSchedule.Slot2Description}");
            }
        }

        /// <summary>
        /// Gets forecast for all day phases, showing what's scheduled.
        /// </summary>
        public List<ScheduleForecast> GetDayForecast()
        {
            var forecasts = new List<ScheduleForecast>();
            var currentPhase = WorldStateAnalyzer.GetDayPhaseFromHour(CampaignTime.Now.GetHourOfDay);

            foreach (DayPhase phase in Enum.GetValues(typeof(DayPhase)))
            {
                var schedule = GetScheduleForPhase(phase);
                
                forecasts.Add(new ScheduleForecast
                {
                    Phase = phase,
                    PrimaryActivity = schedule.Slot1Skipped ? null : schedule.Slot1Description,
                    SecondaryActivity = schedule.Slot2Skipped ? null : schedule.Slot2Description,
                    IsDeviation = schedule.HasDeviation,
                    DeviationReason = schedule.DeviationReason,
                    PlayerCommitment = schedule.PlayerCommitmentTitle,
                    IsCurrent = phase == currentPhase,
                    HasPassed = phase < currentPhase,
                    FlavorText = schedule.FlavorText
                });
            }

            return forecasts;
        }

        /// <summary>
        /// Checks if an opportunity type matches the scheduled category for the current phase.
        /// </summary>
        public bool IsScheduledCategory(OpportunityType type, ScheduledPhase schedule)
        {
            if (schedule == null)
            {
                return false;
            }

            var typeStr = type.ToString().ToLowerInvariant();

            // Check slot 1
            if (!schedule.Slot1Skipped && MatchesCategory(typeStr, schedule.Slot1Category))
            {
                return true;
            }

            // Check slot 2
            if (!schedule.Slot2Skipped && MatchesCategory(typeStr, schedule.Slot2Category))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Invalidates the current schedule cache, forcing regeneration.
        /// Called when company state changes significantly.
        /// </summary>
        public void InvalidateCache()
        {
            _currentSchedule = null;
        }

        // Gets the raw baseline schedule for a phase from config
        private ScheduledPhase GetBaselineForPhase(DayPhase phase)
        {
            if (!_configLoaded || _scheduleConfig == null)
            {
                return CreateDefaultSchedule(phase);
            }

            try
            {
                var phaseName = phase.ToString();
                var phaseConfig = _scheduleConfig["phases"]?[phaseName];

                if (phaseConfig == null)
                {
                    ModLogger.Warn(LogCategory, $"No config found for phase {phaseName}, using defaults");
                    return CreateDefaultSchedule(phase);
                }

                return new ScheduledPhase
                {
                    Phase = phase,
                    Slot1Category = phaseConfig["slot1"]?["category"]?.Value<string>() ?? "training",
                    Slot1Description = phaseConfig["slot1"]?["description"]?.Value<string>() ?? "Training",
                    Slot1Weight = phaseConfig["slot1"]?["weight"]?.Value<float>() ?? 1.0f,
                    Slot2Category = phaseConfig["slot2"]?["category"]?.Value<string>() ?? "social",
                    Slot2Description = phaseConfig["slot2"]?["description"]?.Value<string>() ?? "Social time",
                    Slot2Weight = phaseConfig["slot2"]?["weight"]?.Value<float>() ?? 0.5f,
                    FlavorText = phaseConfig["flavor"]?.Value<string>() ?? ""
                };
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, $"Error reading schedule config for {phase}", ex);
                return CreateDefaultSchedule(phase);
            }
        }

        // Creates a default schedule when config is unavailable
        private ScheduledPhase CreateDefaultSchedule(DayPhase phase)
        {
            return phase switch
            {
                DayPhase.Dawn => new ScheduledPhase
                {
                    Phase = phase,
                    Slot1Category = "formation",
                    Slot1Description = "Morning formation",
                    Slot2Category = "training",
                    Slot2Description = "Early drill",
                    FlavorText = "The camp stirs to life."
                },
                DayPhase.Midday => new ScheduledPhase
                {
                    Phase = phase,
                    Slot1Category = "training",
                    Slot1Description = "Combat training",
                    Slot2Category = "work",
                    Slot2Description = "Work details",
                    FlavorText = "The sun climbs high."
                },
                DayPhase.Dusk => new ScheduledPhase
                {
                    Phase = phase,
                    Slot1Category = "social",
                    Slot1Description = "Evening leisure",
                    Slot2Category = "economic",
                    Slot2Description = "Trading and gambling",
                    FlavorText = "Work ends. Men gather."
                },
                DayPhase.Night => new ScheduledPhase
                {
                    Phase = phase,
                    Slot1Category = "recovery",
                    Slot1Description = "Rest and sleep",
                    Slot2Category = "special",
                    Slot2Description = "Night activities",
                    FlavorText = "The camp grows quiet."
                },
                _ => new ScheduledPhase { Phase = phase }
            };
        }

        // Applies activity level modifiers to category weights
        private void ApplyActivityModifiers(ScheduledPhase schedule, ActivityLevel level)
        {
            if (!_configLoaded || _scheduleConfig == null)
            {
                return;
            }

            try
            {
                var overrides = _scheduleConfig["activityOverrides"]?[level.ToString()];
                if (overrides == null)
                {
                    return;
                }

                var modifiers = overrides["modifiers"] as JObject;
                if (modifiers == null)
                {
                    return;
                }

                foreach (var mod in modifiers.Properties())
                {
                    var category = mod.Name;
                    var multiplier = mod.Value.Value<float>();

                    // A multiplier of 0 means skip
                    if (multiplier == 0)
                    {
                        if (schedule.Slot1Category == category)
                        {
                            schedule.Slot1Skipped = true;
                        }
                        if (schedule.Slot2Category == category)
                        {
                            schedule.Slot2Skipped = true;
                        }
                    }
                    else
                    {
                        if (schedule.Slot1Category == category)
                        {
                            schedule.Slot1Weight *= multiplier;
                        }
                        if (schedule.Slot2Category == category)
                        {
                            schedule.Slot2Weight *= multiplier;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, $"Error applying activity modifiers for {level}", ex);
            }
        }

        // Applies lord situation modifiers (marching skips midday, etc.)
        private void ApplyLordSituationModifiers(ScheduledPhase schedule, LordSituation situation)
        {
            if (!_configLoaded || _scheduleConfig == null)
            {
                return;
            }

            try
            {
                var situationConfig = _scheduleConfig["lordSituationModifiers"]?[situation.ToString()];
                if (situationConfig == null)
                {
                    return;
                }

                // Check if this phase should be skipped for this lord situation
                var skipPhases = situationConfig["skipPhases"]?.ToObject<List<string>>() ?? new List<string>();
                if (skipPhases.Contains(schedule.Phase.ToString()))
                {
                    schedule.Slot1Skipped = true;
                    schedule.Slot2Skipped = true;
                    schedule.DeviationReason = situationConfig["description"]?.Value<string>() 
                        ?? $"{situation} conditions";
                }

                // Apply boosts to specific categories
                var boostCategories = situationConfig["boostCategories"]?.ToObject<List<string>>() ?? new List<string>();
                foreach (var category in boostCategories)
                {
                    if (schedule.Slot1Category == category)
                    {
                        schedule.Slot1Weight *= 1.3f;
                    }
                    if (schedule.Slot2Category == category)
                    {
                        schedule.Slot2Weight *= 1.3f;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, $"Error applying lord situation modifiers for {situation}", ex);
            }
        }

        // Applies pressure overrides based on company needs
        private void ApplyPressureOverrides(ScheduledPhase schedule, CompanyNeedsState needs, WorldSituation world)
        {
            if (needs == null || !_configLoaded || _scheduleConfig == null)
            {
                return;
            }

            try
            {
                var pressureOverrides = _scheduleConfig["pressureOverrides"] as JObject;
                if (pressureOverrides == null)
                {
                    return;
                }

                // Morale removed - readiness is primary stress indicator now

                // Check low supplies
                var lowSupplies = pressureOverrides["low_supplies"];
                if (lowSupplies != null)
                {
                    var threshold = lowSupplies["threshold"]?.Value<int>() ?? 30;
                    if (needs.Supplies < threshold)
                    {
                        ApplyPressureEffect(schedule, lowSupplies);
                    }
                }

                // Check siege conditions
                if (world.LordIs == LordSituation.SiegeAttacking || 
                    world.LordIs == LordSituation.SiegeDefending)
                {
                    var siege = pressureOverrides["siege"];
                    if (siege != null)
                    {
                        ApplyPressureEffect(schedule, siege);
                    }
                }

                // Check marching conditions
                if (world.LordIs == LordSituation.WarMarching)
                {
                    var marching = pressureOverrides["marching"];
                    if (marching != null)
                    {
                        ApplyPressureEffect(schedule, marching);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error applying pressure overrides", ex);
            }
        }

        // Applies a single pressure effect to the schedule
        private void ApplyPressureEffect(ScheduledPhase schedule, JToken pressureConfig)
        {
            var effect = pressureConfig["effect"]?.Value<string>();
            var description = pressureConfig["description"]?.Value<string>() ?? "Conditions require schedule change";

            switch (effect)
            {
                case "skip_formations":
                    if (schedule.Slot1Category == "formation")
                    {
                        schedule.Slot1Skipped = true;
                        schedule.DeviationReason = description;
                    }
                    break;

                case "boost_foraging":
                    // Boost economic/work activities
                    if (schedule.Slot1Category == "work" || schedule.Slot1Category == "economic")
                    {
                        schedule.Slot1Weight *= 1.5f;
                    }
                    if (schedule.Slot2Category == "work" || schedule.Slot2Category == "economic")
                    {
                        schedule.Slot2Weight *= 1.5f;
                    }
                    break;

                case "restrict_leisure":
                    if (schedule.Slot1Category == "social" || schedule.Slot1Category == "economic")
                    {
                        schedule.Slot1Weight *= 0.5f;
                    }
                    if (schedule.Slot2Category == "social" || schedule.Slot2Category == "economic")
                    {
                        schedule.Slot2Weight *= 0.5f;
                    }
                    schedule.DeviationReason = description;
                    break;

                case "boost_recovery":
                    if (schedule.Slot1Category == "recovery")
                    {
                        schedule.Slot1Weight *= 1.5f;
                    }
                    if (schedule.Slot2Category == "recovery")
                    {
                        schedule.Slot2Weight *= 1.5f;
                    }
                    // Skip training when exhausted
                    if (schedule.Slot1Category == "training" || schedule.Slot1Category == "formation")
                    {
                        schedule.Slot1Skipped = true;
                        schedule.DeviationReason = description;
                    }
                    break;

                case "survival_mode":
                    // Only recovery activities, everything else cancelled
                    if (schedule.Slot1Category != "recovery")
                    {
                        schedule.Slot1Skipped = true;
                    }
                    if (schedule.Slot2Category != "recovery")
                    {
                        schedule.Slot2Skipped = true;
                    }
                    schedule.DeviationReason = description;
                    break;

                case "minimal_schedule":
                    // Skip midday activities when marching
                    if (schedule.Phase == DayPhase.Midday)
                    {
                        schedule.Slot1Skipped = true;
                        schedule.Slot2Skipped = true;
                        schedule.DeviationReason = description;
                    }
                    break;
            }
        }

        // Checks for player commitments and marks them on the schedule
        private void ApplyPlayerCommitments(ScheduledPhase schedule)
        {
            var commitments = CampOpportunityGenerator.Instance?.Commitments;
            if (commitments == null)
            {
                return;
            }

            var currentDay = (int)CampaignTime.Now.ToDays;
            var commitment = commitments.GetCommitmentsForPhase(schedule.Phase.ToString(), currentDay)
                .FirstOrDefault();

            if (commitment != null)
            {
                schedule.HasPlayerCommitment = true;
                schedule.PlayerCommitmentTitle = commitment.Title;
            }
        }

        // Checks if an opportunity type matches a schedule category
        private bool MatchesCategory(string opportunityType, string category)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(opportunityType))
            {
                return false;
            }

            // Use category mappings if loaded
            if (_categoryMappings.TryGetValue(category, out var mappedTypes))
            {
                return mappedTypes.Any(t => t.Equals(opportunityType, StringComparison.OrdinalIgnoreCase));
            }

            // Direct match fallback
            return category.Equals(opportunityType, StringComparison.OrdinalIgnoreCase);
        }

        // Loads configuration from JSON
        private void LoadConfig()
        {
            try
            {
                var configPath = ModulePaths.GetConfigPath("camp_schedule.json");

                if (!File.Exists(configPath))
                {
                    ModLogger.Warn(LogCategory, $"Schedule config not found at {configPath}, using defaults");
                    _configLoaded = false;
                    return;
                }

                var json = File.ReadAllText(configPath);
                _scheduleConfig = JObject.Parse(json);

                // Load schedule boost multiplier
                if (_scheduleConfig["scheduleBoostMultiplier"] != null)
                {
                    _scheduleBoostMultiplier = _scheduleConfig["scheduleBoostMultiplier"].Value<float>();
                }

                // Load category mappings
                var mappings = _scheduleConfig["categoryMappings"] as JObject;
                if (mappings != null)
                {
                    foreach (var mapping in mappings.Properties())
                    {
                        _categoryMappings[mapping.Name] = mapping.Value.ToObject<List<string>>() ?? new List<string>();
                    }
                }

                _configLoaded = true;
                ModLogger.Info(LogCategory, "Schedule config loaded successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load schedule config", ex);
                _configLoaded = false;
            }
        }

        /// <summary>
        /// Internal class to track active deviations for reversion checking.
        /// </summary>
        private class ScheduleDeviation
        {
            public string Type { get; set; }
            public string Description { get; set; }
            public int ActivatedDay { get; set; }
        }
    }
}
