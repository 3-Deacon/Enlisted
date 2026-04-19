using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Features.Context;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Company
{
    /// <summary>
    /// Manages company need degradation and recovery mechanics for the enlisted lord's party.
    /// Needs degrade daily based on activity and conditions, recover when addressed.
    /// Enhanced with strategic context awareness for predicting upcoming needs.
    /// </summary>
    public static class CompanyNeedsManager
    {
        private const string LogCategory = "CompanyNeeds";
        
        private static JObject _strategicConfig;
        private static bool _configLoaded;

        /// <summary>
        /// Applies daily degradation to all company needs.
        /// Base degradation rates, accelerated by conditions.
        /// </summary>
        public static void ProcessDailyDegradation(CompanyNeedsState needs, MobileParty army = null)
        {
            if (needs == null)
            {
                ModLogger.Error(LogCategory, "Cannot process degradation: CompanyNeedsState is null");
                return;
            }

            ModLogger.Debug(LogCategory, "Processing daily degradation...");

            // Initial base degradation rates are hard-coded while the configuration system is being expanded.
            // NOTE: Supplies degradation is handled by CompanySupplyManager (unified logistics tracking).
            var readinessDegradation = 2;

            // Check for accelerated degradation conditions
            bool isInCombat = army?.MapEvent != null;
            bool isOnLongMarch = army is { IsMoving: true, CurrentSettlement: null };

            // Accelerated degradation from long marches
            if (isOnLongMarch)
            {
                readinessDegradation += 5;
                ModLogger.Debug(LogCategory, "Army on long march: accelerated Readiness degradation");
            }

            // Apply degradation (Supplies handled separately by CompanySupplyManager)
            var oldReadiness = needs.Readiness;

            needs.SetNeed(CompanyNeed.Readiness, needs.Readiness - readinessDegradation);

            // Log changes
            ModLogger.Debug(LogCategory, $"Readiness: {oldReadiness} -> {needs.Readiness} (-{readinessDegradation})");

            ModLogger.Info(LogCategory, "Daily degradation applied.");
        }

        // Need recovery is now managed through direct orders and camp activities.

        /// <summary>
        /// Checks if any company needs are critically low and returns warning messages.
        /// </summary>
        /// <returns>Dictionary of need -> warning message for critical needs</returns>
        public static Dictionary<CompanyNeed, string> CheckCriticalNeeds(CompanyNeedsState needs)
        {
            var warnings = new Dictionary<CompanyNeed, string>();

            if (needs == null)
            {
                ModLogger.Error(LogCategory, "Cannot check critical needs: CompanyNeedsState is null");
                return warnings;
            }

            // Critical thresholds are currently hard-coded while the configuration system is being expanded.
            var criticalThresholdHigh = 20; // 20%
            var criticalThresholdLow = 30;   // 30%

            // Check each need against thresholds
            CheckNeedThreshold(needs.Readiness, CompanyNeed.Readiness, criticalThresholdHigh, criticalThresholdLow, warnings);
            CheckNeedThreshold(needs.Supplies, CompanyNeed.Supplies, criticalThresholdHigh, criticalThresholdLow, warnings);

            if (warnings.Count > 0)
            {
                ModLogger.Info(LogCategory, $"Found {warnings.Count} critical needs");
            }

            return warnings;
        }

        /// <summary>
        /// Gets the status level text for a need value (Excellent, Good, Fair, Poor, Critical).
        /// </summary>
        public static string GetNeedStatusText(int needValue)
        {
            if (needValue >= 80)
            {
                return "Excellent";
            }
            if (needValue >= 60)
            {
                return "Good";
            }
            if (needValue >= 40)
            {
                return "Fair";
            }
            if (needValue >= 20)
            {
                return "Poor";
            }
            return "Critical";
        }

        /// <summary>
        /// Predicts upcoming company needs based on the current strategic context.
        /// Returns a dictionary of predicted need values (0-100) for each CompanyNeed.
        /// Used to forecast what needs will be important in upcoming operations.
        /// </summary>
        public static Dictionary<CompanyNeed, int> PredictUpcomingNeeds(MobileParty party)
        {
            LoadStrategicConfig();

            var predictions = new Dictionary<CompanyNeed, int>();

            if (party == null)
            {
                // Return default moderate predictions
                predictions[CompanyNeed.Readiness] = 60;
                predictions[CompanyNeed.Supplies] = 60;
                return predictions;
            }

            try
            {
                // Get current strategic context
                var context = ArmyContextAnalyzer.GetLordStrategicContext(party);

                // Load prediction template from config
                var contextData = _strategicConfig?["strategic_contexts"]?[context];
                var needsPrediction = contextData?["needs_prediction"];

                if (needsPrediction != null)
                {
                    predictions[CompanyNeed.Readiness] = needsPrediction["Readiness"]?.Value<int>() ?? 60;
                    predictions[CompanyNeed.Supplies] = needsPrediction["Supplies"]?.Value<int>() ?? 60;

                    ModLogger.Debug(LogCategory, $"Predicted needs for context '{context}': " +
                        $"Readiness={predictions[CompanyNeed.Readiness]}, " +
                        $"Supplies={predictions[CompanyNeed.Supplies]}");
                }
                else
                {
                    // Fallback to default predictions
                    predictions[CompanyNeed.Readiness] = 60;
                    predictions[CompanyNeed.Supplies] = 60;

                    ModLogger.Warn(LogCategory, $"No prediction template found for context '{context}', using defaults");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error predicting upcoming needs", ex);
                
                // Return default predictions on error
                predictions[CompanyNeed.Readiness] = 60;
                predictions[CompanyNeed.Supplies] = 60;
            }

            return predictions;
        }

        /// <summary>
        /// Loads the strategic context configuration from JSON.
        /// </summary>
        private static void LoadStrategicConfig()
        {
            if (_configLoaded)
            {
                return;
            }

            try
            {
                var configPath = ModulePaths.GetConfigPath("strategic_context_config.json");
                
                if (!File.Exists(configPath))
                {
                    ModLogger.Error(LogCategory, $"Strategic context config not found at: {configPath}");
                    _strategicConfig = new JObject();
                    _configLoaded = true;
                    return;
                }

                var json = File.ReadAllText(configPath);
                _strategicConfig = JObject.Parse(json);
                _configLoaded = true;
                
                ModLogger.Info(LogCategory, "Strategic context configuration loaded for needs prediction");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load strategic context config", ex);
                _strategicConfig = new JObject();
                _configLoaded = true;
            }
        }

        // Helper: Check individual need against thresholds and add warning if needed
        private static void CheckNeedThreshold(int needValue, CompanyNeed need, int criticalHigh, int criticalLow, 
            Dictionary<CompanyNeed, string> warnings)
        {
            if (needValue <= criticalHigh)
            {
                warnings[need] = $"{need} is CRITICAL ({needValue}%)! Immediate action required.";
                ModLogger.Warn(LogCategory, $"{need} is critical: {needValue}%");
            }
            else if (needValue <= criticalLow)
            {
                warnings[need] = $"{need} is low ({needValue}%). Address soon.";
                ModLogger.Debug(LogCategory, $"{need} is low: {needValue}%");
            }
        }
    }

    // Extension method for cleaner syntax
    public static class CompanyNeedsStateExtensions
    {
        public static int GetNeedValue(this CompanyNeedsState needs, CompanyNeed need)
        {
            return need switch
            {
                CompanyNeed.Readiness => needs.Readiness,
                CompanyNeed.Supplies => needs.Supplies,
                _ => 0
            };
        }
    }
}

