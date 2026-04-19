using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Enlisted.Mod.Core.Config
{
    /// <summary>
    /// Centralized configuration loader for various mod systems.
    /// Reads JSON config files from ModuleData/Enlisted/.
    /// </summary>
    public static class ConfigurationManager
    {
        private const string LogCategory = "Config";

        /// <summary>
        /// Gets the module data path using ModulePaths utility.
        /// This correctly resolves paths for both manual/Nexus installs and Steam Workshop.
        /// </summary>
        private static string ModuleDataPath => ModulePaths.ModuleDataPath;

        private static readonly JsonSerializerSettings SnakeCaseSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        private static T DeserializeSnakeCase<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<T>(json, SnakeCaseSettings);
        }

        /// <summary>
        /// Load retirement/discharge configuration.
        /// </summary>
        public static RetirementConfig LoadRetirementConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new RetirementConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.Retirement ?? new RetirementConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load retirement config", ex);
                return new RetirementConfig();
            }
        }

        /// <summary>
        /// Load escalation system configuration.
        /// </summary>
        public static EscalationConfig LoadEscalationConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    ModLogger.Warn(LogCategory, $"Escalation config not found: {path}");
                    return new EscalationConfig { Enabled = true };
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.Escalation ?? new EscalationConfig { Enabled = true };
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load escalation config", ex);
                return new EscalationConfig { Enabled = true };
            }
        }

        /// <summary>
        /// Load gameplay configuration.
        /// </summary>
        public static GameplayConfig LoadGameplayConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new GameplayConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.Gameplay ?? new GameplayConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load gameplay config", ex);
                return new GameplayConfig();
            }
        }

        /// <summary>
        /// Get module data path for config file consumers.
        /// Uses ModulePaths utility for correct resolution with both manual and Workshop installs.
        /// </summary>
        public static string GetModuleDataPathForConsumers()
        {
            return ModulePaths.ModuleDataPath;
        }

        public static PlayerConditionsConfig LoadPlayerConditionsConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new PlayerConditionsConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.PlayerConditions ?? new PlayerConditionsConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load player conditions config", ex);
                return new PlayerConditionsConfig();
            }
        }

        public static QuartermasterConfig LoadQuartermasterConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new QuartermasterConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.Quartermaster ?? new QuartermasterConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load quartermaster config", ex);
                return new QuartermasterConfig();
            }
        }

        public static FinanceConfig LoadFinanceConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new FinanceConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.Finance ?? new FinanceConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load finance config", ex);
                return new FinanceConfig();
            }
        }

        public static CampLifeConfig LoadCampLifeConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new CampLifeConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.CampLife ?? new CampLifeConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load camp life config", ex);
                return new CampLifeConfig();
            }
        }

        /// <summary>
        /// Load event pacing configuration from decision_events.pacing section.
        /// These settings control global limits across all automatic event sources.
        /// </summary>
        public static EventPacingConfig LoadEventPacingConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new EventPacingConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.DecisionEvents?.Pacing ?? new EventPacingConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load event pacing config", ex);
                return new EventPacingConfig();
            }
        }

        /// <summary>
        /// Load orchestrator configuration from orchestrator section.
        /// Controls world-state driven content delivery system.
        /// </summary>
        public static OrchestratorConfig LoadOrchestratorConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new OrchestratorConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.Orchestrator ?? new OrchestratorConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load orchestrator config", ex);
                return new OrchestratorConfig();
            }
        }

        /// <summary>
        /// Load native trait mapping configuration.
        /// Controls how Enlisted reputation maps to native personality traits.
        /// </summary>
        public static NativeTraitMappingConfig LoadNativeTraitMappingConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "enlisted_config.json");
                if (!File.Exists(path))
                {
                    return new NativeTraitMappingConfig();
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<EnlistedConfigRoot>(json);
                return config?.NativeTraitMapping ?? new NativeTraitMappingConfig();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load native trait mapping config", ex);
                return new NativeTraitMappingConfig();
            }
        }

        public static int GetDailyBaseXp()
        {
            return Math.Max(0, LoadProgressionConfig()?.XpSources?.DailyBase ?? 25);
        }

        public static int GetBattleParticipationXp()
        {
            return Math.Max(0, LoadProgressionConfig()?.XpSources?.BattleParticipation ?? 25);
        }

        public static int GetXpPerKill()
        {
            return Math.Max(0, LoadProgressionConfig()?.XpSources?.XpPerKill ?? 2);
        }

        public static int[] GetTierXpRequirements()
        {
            var cfg = LoadProgressionConfig();
            var reqs = cfg?.TierProgression?.Requirements ?? new List<ProgressionTierRequirement>();

            // Index i corresponds to tier i+1 (so index 1 is Tier 2 threshold, etc.).
            var maxTier = GetMaxTier();
            var arr = new int[Math.Max(2, maxTier + 1)];

            foreach (var r in reqs)
            {
                if (r == null || r.Tier <= 0)
                {
                    continue;
                }

                var idx = r.Tier - 1;
                if (idx >= 0 && idx < arr.Length)
                {
                    arr[idx] = Math.Max(0, r.XpRequired);
                }
            }

            return arr;
        }

        public static int GetMaxTier()
        {
            var cfg = LoadProgressionConfig();
            var reqs = cfg?.TierProgression?.Requirements;
            if (reqs == null || reqs.Count == 0)
            {
                return 9;
            }

            var max = 1;
            foreach (var r in reqs)
            {
                if (r != null && r.Tier > max)
                {
                    max = r.Tier;
                }
            }

            return Math.Max(1, max);
        }

        /// <summary>
        /// Load retinue system configuration.
        /// </summary>
        public static RetinueConfig LoadRetinueConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "retinue_config.json");
                if (!File.Exists(path))
                {
                    return new RetinueConfig();
                }

                var json = File.ReadAllText(path);
                return DeserializeSnakeCase<RetinueConfig>(json);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load retinue config", ex);
                return new RetinueConfig();
            }
        }

        /// <summary>
        /// Load equipment pricing configuration.
        /// </summary>
        public static EquipmentPricingConfig LoadEquipmentPricingConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "equipment_pricing.json");
                if (!File.Exists(path))
                {
                    return new EquipmentPricingConfig();
                }

                var json = File.ReadAllText(path);
                return DeserializeSnakeCase<EquipmentPricingConfig>(json);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load equipment pricing config", ex);
                return new EquipmentPricingConfig();
            }
        }

        /// <summary>
        /// Get culture-specific rank title from progression config.
        /// </summary>
        public static string GetCultureRankTitle(int tier, string cultureId)
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "progression_config.json");
                if (!File.Exists(path))
                {
                    return GetFallbackRankTitle(tier);
                }

                var json = File.ReadAllText(path);
                var config = DeserializeSnakeCase<ProgressionConfigFile>(json);

                if (config?.CultureRanks != null &&
                    !string.IsNullOrWhiteSpace(cultureId) &&
                    config.CultureRanks.TryGetValue(cultureId, out var culture) &&
                    culture?.Ranks != null &&
                    tier >= 1 &&
                    tier <= culture.Ranks.Count)
                {
                    return culture.Ranks[tier - 1];
                }

                return GetFallbackRankTitle(tier);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load rank title", ex);
                return GetFallbackRankTitle(tier);
            }
        }

        private static string GetFallbackRankTitle(int tier)
        {
            return tier switch
            {
                1 => "Recruit",
                2 => "Private",
                3 => "Corporal",
                4 => "Sergeant",
                5 => "Staff Sergeant",
                6 => "Lieutenant",
                7 => "Captain",
                8 => "Commander",
                9 => "Tribune",
                _ => "Soldier"
            };
        }

        private static ProgressionConfigFile LoadProgressionConfig()
        {
            try
            {
                var path = Path.Combine(ModuleDataPath, "Config", "progression_config.json");
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                return DeserializeSnakeCase<ProgressionConfigFile>(json);
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to load progression config", ex);
                return null;
            }
        }

        // Root config classes
        private class EnlistedConfigRoot
        {
            public GameplayConfig Gameplay { get; set; }
            public QuartermasterConfig Quartermaster { get; set; }
            public EscalationConfig Escalation { get; set; }
            public RetirementConfig Retirement { get; set; }
            public FinanceConfig Finance { get; set; }
            public CampLifeConfig CampLife { get; set; }
            public PlayerConditionsConfig PlayerConditions { get; set; }
            public DecisionEventsConfig DecisionEvents { get; set; }
            public OrchestratorConfig Orchestrator { get; set; }
            public NativeTraitMappingConfig NativeTraitMapping { get; set; }
        }

        private sealed class ProgressionConfigFile
        {
            public TierProgressionConfig TierProgression { get; set; } = new TierProgressionConfig();
            public Dictionary<string, CultureRanksConfig> CultureRanks { get; set; } = new Dictionary<string, CultureRanksConfig>(StringComparer.OrdinalIgnoreCase);
            public XpSourcesConfig XpSources { get; set; } = new XpSourcesConfig();
        }

        private sealed class TierProgressionConfig
        {
            public List<ProgressionTierRequirement> Requirements { get; set; } = new List<ProgressionTierRequirement>();
        }

        private sealed class ProgressionTierRequirement
        {
            public int Tier { get; set; }
            public int XpRequired { get; set; }
        }

        private sealed class CultureRanksConfig
        {
            public List<string> Ranks { get; set; } = new List<string>();
        }

        private sealed class XpSourcesConfig
        {
            public int DailyBase { get; set; } = 25;
            public int BattleParticipation { get; set; } = 25;
            public int XpPerKill { get; set; } = 2;
        }
    }

    /// <summary>
    /// Retirement/discharge configuration.
    /// </summary>
    public class RetirementConfig
    {
        public int FirstTermDays { get; set; } = 252;
        public int RenewalTermDays { get; set; } = 126;
        public int ProbationDays { get; set; } = 12;
        public int CommanderReentryFineGold { get; set; } = 5000;
        public int FirstTermReenlistBonus { get; set; } = 500;
        public int RenewalContinueBonus { get; set; } = 250;
        public int DesertionGracePeriodDays { get; set; } = 3;
        public float ProbationWageMultiplier { get; set; } = 0.5f;

        // Severance payouts used by camp menu flows.
        public int SeveranceVeteran { get; set; } = 500;
        public int SeveranceHonorable { get; set; } = 1000;

        // Pension system
        public int PensionHonorableDaily { get; set; } = 50;
        public int PensionVeteranDaily { get; set; } = 100;
        public int PensionRelationStopThreshold { get; set; } = 0;

        // Debug flags
        public bool DebugSkipGearStripping { get; set; } = false;

        // Dialog-related retirement properties
        public int CooldownDays { get; set; } = 7;
        public int FirstTermGold { get; set; } = 1000;
        public int RenewalDischargeGold { get; set; } = 500;
        public int LordRelationBonus { get; set; } = 10;
        public int FactionReputationBonus { get; set; } = 5;
        public int OtherLordsMinRelation { get; set; } = 0;
        public int OtherLordsRelationBonus { get; set; } = 2;
    }

    /// <summary>
    /// Escalation system configuration.
    /// </summary>
    public class EscalationConfig
    {
        public bool Enabled { get; set; } = true;
        public int ScrutinyDecayIntervalDays { get; set; } = 7;
        public int DisciplineDecayIntervalDays { get; set; } = 14;
        public int SoldierReputationDecayIntervalDays { get; set; } = 14;
        public int MedicalRiskDecayIntervalDays { get; set; } = 1;
        public int ThresholdEventCooldownDays { get; set; } = 3;
    }

    /// <summary>
    /// Player conditions configuration.
    /// </summary>
    public class PlayerConditionsConfig
    {
        public bool Enabled { get; set; } = true;
        public string DefinitionsFile { get; set; } = "Conditions\\condition_defs.json";
        public float BasicTreatmentMultiplier { get; set; } = 1.5f;
        public float ThoroughTreatmentMultiplier { get; set; } = 2.0f;
        public float HerbalTreatmentMultiplier { get; set; } = 1.75f;
        public bool ExhaustionEnabled { get; set; } = true;
    }

    public sealed class GameplayConfig
    {
        public int ReserveTroopThreshold { get; set; } = 100;
        public int DesertionGracePeriodDays { get; set; } = 14;
        public int LeaveMaxDays { get; set; } = 14;
    }

    public sealed class QuartermasterConfig
    {
        public float SoldierTax { get; set; } = 1.2f;
        public float BuybackRate { get; set; } = 0.5f;
        public float OfficerStockTax { get; set; } = 1.35f;
    }

    public sealed class FinanceConfig
    {
        public bool ShowInClanTooltip { get; set; } = true;
        public string TooltipLabel { get; set; } = "{=enlisted_wage_income}Enlistment Wages";
        public int PaydayIntervalDays { get; set; } = 12;
        public float PaydayJitterDays { get; set; } = 1f;
        public WageFormulaConfig WageFormula { get; set; } = new WageFormulaConfig();
    }

    public sealed class WageFormulaConfig
    {
        public int BaseWage { get; set; } = 10;
        public int LevelMultiplier { get; set; } = 1;
        public int TierMultiplier { get; set; } = 5;
        public int XpDivisor { get; set; } = 200;
        public float ArmyBonusMultiplier { get; set; } = 1.2f;
    }

    public sealed class CampLifeConfig
    {
        public bool Enabled { get; set; } = true;
        public float LogisticsHighThreshold { get; set; } = 70f;
        public float MoraleLowThreshold { get; set; } = 70f;
        public float PayTensionHighThreshold { get; set; } = 70f;
        public float ScrutinyHighThreshold { get; set; } = 70f;

        public float QuartermasterPurchaseFine { get; set; } = 0.98f;
        public float QuartermasterPurchaseTense { get; set; } = 1.0f;
        public float QuartermasterPurchaseSour { get; set; } = 1.07f;
        public float QuartermasterPurchasePredatory { get; set; } = 1.15f;

        public float QuartermasterBuybackFine { get; set; } = 1.0f;
        public float QuartermasterBuybackTense { get; set; } = 0.95f;
        public float QuartermasterBuybackSour { get; set; } = 0.85f;
        public float QuartermasterBuybackPredatory { get; set; } = 0.75f;
    }

    /// <summary>
    /// Decision events system configuration including pacing limits.
    /// Controls how often automatic events can fire to prevent spam.
    /// </summary>
    public sealed class DecisionEventsConfig
    {
        public bool Enabled { get; set; } = true;
        public string EventsFolder { get; set; } = "Events";
        public EventPacingConfig Pacing { get; set; } = new EventPacingConfig();
    }

    /// <summary>
    /// Global event pacing limits from enlisted_config.json.
    /// These limits apply across ALL automatic event sources (paced events + map incidents).
    /// </summary>
    public sealed class EventPacingConfig
    {
        // Global limits across all automatic events
        public int MaxPerDay { get; set; } = 2;
        public int MaxPerWeek { get; set; } = 8;
        public int MinHoursBetween { get; set; } = 6;

        // Per-event and per-category cooldowns
        public int PerEventCooldownDays { get; set; } = 7;
        public int PerCategoryCooldownDays { get; set; } = 1;
    }

    /// <summary>
    /// Retinue system configuration for capacity, trickle, and economics.
    /// </summary>
    public sealed class RetinueConfig
    {
        public bool Enabled { get; set; } = true;
        public Dictionary<int, RetinueCapacityTier> CapacityByTier { get; set; } = new Dictionary<int, RetinueCapacityTier>();
        public RetinueReplenishment Replenishment { get; set; } = new RetinueReplenishment();
        public RetinueEconomics Economics { get; set; } = new RetinueEconomics();
        public RetinueTierUnlock TierUnlock { get; set; } = new RetinueTierUnlock();
    }

    public sealed class RetinueCapacityTier
    {
        public string Name { get; set; } = string.Empty;
        public int MaxSoldiers { get; set; }
    }

    public sealed class RetinueReplenishment
    {
        public RetinueTrickle Trickle { get; set; } = new RetinueTrickle();
        public RetinueRequisition Requisition { get; set; } = new RetinueRequisition();
    }

    public sealed class RetinueTrickle
    {
        public bool Enabled { get; set; } = true;
        public int MinDays { get; set; } = 2;
        public int MaxDays { get; set; } = 3;
        public int SoldiersPerTick { get; set; } = 1;
    }

    public sealed class RetinueRequisition
    {
        public bool Enabled { get; set; } = true;
        public int CooldownDays { get; set; } = 14;
        public float CostMultiplier { get; set; } = 1.0f;
    }

    public sealed class RetinueEconomics
    {
        public int DailyUpkeepPerSoldier { get; set; } = 2;
        public bool DesertionEnabled { get; set; } = true;
    }

    public sealed class RetinueTierUnlock
    {
        public int LanceTier { get; set; } = 4;
        public int SquadTier { get; set; } = 5;
        public int RetinueTier { get; set; } = 6;
    }

    /// <summary>
    /// Equipment pricing configuration for quartermaster and retirement.
    /// </summary>
    public sealed class EquipmentPricingConfig
    {
        public bool Enabled { get; set; } = true;
        public PricingRules PricingRules { get; set; } = new PricingRules();
        public Dictionary<string, int> TroopOverrides { get; set; } = new Dictionary<string, int>();
        public RetirementRequirements RetirementRequirements { get; set; } = new RetirementRequirements();
        public MedicalTreatment MedicalTreatment { get; set; } = new MedicalTreatment();
    }

    public sealed class PricingRules
    {
        public int BaseCostPerTier { get; set; } = 75;
        public Dictionary<string, float> FormationMultipliers { get; set; } = new Dictionary<string, float>();
        public float EliteMultiplier { get; set; } = 1.5f;
        public Dictionary<string, float> CultureModifiers { get; set; } = new Dictionary<string, float>();
    }

    public sealed class RetirementRequirements
    {
        public int MinimumServiceDays { get; set; } = 365;
        public float HonorableDischargeBonusMultiplier { get; set; } = 2.0f;
        public int EquipmentRetentionTierRequirement { get; set; } = 5;
    }

    public sealed class MedicalTreatment
    {
        public int StandardCooldownDays { get; set; } = 5;
        public int FieldMedicCooldownDays { get; set; } = 2;
        public float BaseHealingPercentage { get; set; } = 0.8f;
        public float FieldMedicHealingPercentage { get; set; } = 1.0f;
        public int MinimumHealAmount { get; set; } = 20;
    }

    /// <summary>
    /// Content orchestrator configuration for world-state driven content pacing.
    /// Provides activity levels to OrderProgressionBehavior and manages order scheduling.
    /// </summary>
    public sealed class OrchestratorConfig
    {
        public bool Enabled { get; set; } = false;
        public bool LogDecisions { get; set; } = true;

        /// <summary>
        /// Activity level multipliers for order event slot probabilities.
        /// Keys: Quiet, Routine, Active, Intense
        /// Values: Multiplier applied to slot base chance (0.15 or 0.35)
        /// Example: Quiet=0.3 means 15% * 0.3 = 4.5% chance for normal slots
        /// </summary>
        public Dictionary<string, float> ActivityModifiers { get; set; } = new Dictionary<string, float>
        {
            { "Quiet", 0.3f },
            { "Routine", 0.6f },
            { "Active", 1.0f },
            { "Intense", 1.5f }
        };

        /// <summary>
        /// Order scheduling configuration for predictive warnings.
        /// </summary>
        public OrderSchedulingConfig OrderScheduling { get; set; } = new OrderSchedulingConfig();
    }

    /// <summary>
    /// Configuration for order scheduling with advance warnings.
    /// </summary>
    public sealed class OrderSchedulingConfig
    {
        /// <summary>Normal orders are forecast 24 hours in advance.</summary>
        public int NormalAdvanceHours { get; set; } = 24;

        /// <summary>Urgent orders are forecast 8 hours in advance.</summary>
        public int UrgentAdvanceHours { get; set; } = 8;

        /// <summary>Critical orders appear immediately (0-2 hours).</summary>
        public int CriticalAdvanceHours { get; set; } = 0;
    }

    /// <summary>
    /// Configuration for mapping Enlisted custom reputation to native personality traits.
    /// This provides character sheet integration and affects native lord/companion reactions.
    /// </summary>
    public sealed class NativeTraitMappingConfig
    {
        /// <summary>
        /// Whether native trait mapping is enabled.
        /// When disabled, reputation changes don't affect native personality traits.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Divisor for scaling reputation changes to trait XP.
        /// Higher values = slower trait progression. Recommended: 5 for 100-day careers.
        /// Example: +10 Soldier Rep / 5 = +2 Valor trait XP.
        /// </summary>
        public int ScaleDivisor { get; set; } = 5;

        /// <summary>
        /// Minimum trait change value to apply. Changes below this are skipped to avoid spam.
        /// Example: If ScaleDivisor=5 and MinimumChange=1, rep changes below 5 are ignored.
        /// </summary>
        public int MinimumChange { get; set; } = 1;
    }

}


