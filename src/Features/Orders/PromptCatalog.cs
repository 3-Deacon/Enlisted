using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Orders.Models;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json;
using TaleWorlds.Core;

namespace Enlisted.Features.Orders
{
    /// <summary>
    /// Loads and manages Order Prompt templates and outcome pools.
    /// Reads from ModuleData/Enlisted/Prompts/order_prompts.json and prompt_outcomes.json
    /// </summary>
    public static class PromptCatalog
    {
        private const string LogCategory = "PromptCatalog";

        // Order type -> list of prompts
        private static Dictionary<string, List<OrderPrompt>> _promptsByOrderType;

        // Outcome type -> pool of possible outcomes
        private static Dictionary<PromptOutcome, List<PromptOutcomeData>> _outcomePools;

        // Configuration weights
        private static float _promptChance = 0.15f; // 15% per phase
        private static Dictionary<PromptOutcome, int> _outcomeWeights;

        /// <summary>
        /// Has the catalog been loaded successfully?
        /// </summary>
        public static bool IsLoaded { get; private set; }

        /// <summary>
        /// Load prompts and outcomes from JSON files.
        /// Called during mod initialization.
        /// </summary>
        public static void LoadPrompts()
        {
            try
            {
                LoadOrderPrompts();
                LoadOutcomePools();
                LoadConfiguration();

                IsLoaded = true;
                ModLogger.Info(LogCategory, $"Loaded {_promptsByOrderType?.Count ?? 0} order types with prompts, {_outcomePools?.Count ?? 0} outcome pools");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PromptCatalog", "E-PROMPT-002: Failed to load prompt catalog", ex);
                IsLoaded = false;
            }
        }

        /// <summary>
        /// Get all prompts valid for a specific order type and context.
        /// </summary>
        public static List<OrderPrompt> GetPromptsForOrderType(string orderType, bool isAtSea, string campaignContext = null)
        {
            if (!IsLoaded || _promptsByOrderType == null)
            {
                return new List<OrderPrompt>();
            }

            // Get prompts for this order type
            if (!_promptsByOrderType.TryGetValue(orderType, out var prompts))
            {
                // Try wildcard "any_order" fallback
                if (!_promptsByOrderType.TryGetValue("any_order", out prompts))
                {
                    return new List<OrderPrompt>();
                }
            }

            // Filter by sea/land context
            var contextFilter = isAtSea ? "sea" : "land";
            var filtered = prompts.Where(p =>
                p.Contexts.Contains("any") ||
                p.Contexts.Contains(contextFilter)
            ).ToList();

            // Filter by campaign context if specified
            if (!string.IsNullOrEmpty(campaignContext))
            {
                filtered = filtered.Where(p =>
                    p.ValidCampaignContexts == null ||
                    p.ValidCampaignContexts.Count == 0 ||
                    p.ValidCampaignContexts.Contains(campaignContext) ||
                    p.ValidCampaignContexts.Contains("any")
                ).ToList();
            }

            return filtered;
        }

        /// <summary>
        /// Roll whether a prompt should fire for this phase (15% base chance).
        /// </summary>
        public static bool ShouldFirePrompt(string campaignContext = null)
        {
            var baseChance = _promptChance;

            // Adjust by campaign context
            if (!string.IsNullOrEmpty(campaignContext))
            {
                baseChance = campaignContext switch
                {
                    "PostBattle" => 0.08f,      // 8% - rare in post-battle
                    "RecentEngagement" => 0.12f, // 12% - slightly reduced
                    "NormalCampaign" => 0.15f,   // 15% - standard
                    "Garrison" => 0.10f,         // 10% - reduced in garrison
                    _ => baseChance
                };
            }

            return MBRandom.RandomFloat < baseChance;
        }

        /// <summary>
        /// Pre-roll the outcome using weighted random selection.
        /// Outcome is determined BEFORE player sees the prompt (CK3 pattern).
        /// </summary>
        public static PromptOutcome RollOutcome()
        {
            if (_outcomeWeights == null)
            {
                // Default weights if config failed to load
                _outcomeWeights = new Dictionary<PromptOutcome, int>
                {
                    { PromptOutcome.Nothing, 50 },
                    { PromptOutcome.SmallReward, 30 },
                    { PromptOutcome.EventChain, 15 },
                    { PromptOutcome.Danger, 5 }
                };
            }

            var totalWeight = _outcomeWeights.Values.Sum();
            var roll = MBRandom.RandomInt(totalWeight);

            var cumulative = 0;
            foreach (var pair in _outcomeWeights.OrderBy(p => p.Key))
            {
                cumulative += pair.Value;
                if (roll < cumulative)
                {
                    return pair.Key;
                }
            }

            return PromptOutcome.Nothing; // Fallback
        }

        /// <summary>
        /// Get a random outcome data entry from the pool for this outcome type.
        /// </summary>
        public static PromptOutcomeData GetOutcomeData(PromptOutcome outcome)
        {
            if (_outcomePools == null || !_outcomePools.TryGetValue(outcome, out var pool))
            {
                return new PromptOutcomeData
                {
                    Text = "Nothing happens.",
                    Effects = new Dictionary<string, object>()
                };
            }

            if (pool.Count == 0)
            {
                return new PromptOutcomeData
                {
                    Text = "Nothing happens.",
                    Effects = new Dictionary<string, object>()
                };
            }

            return pool[MBRandom.RandomInt(pool.Count)];
        }

        /// <summary>
        /// Get the prompt chance percentage for logging/debugging.
        /// </summary>
        public static float GetPromptChance()
        {
            return _promptChance;
        }

        private static void LoadOrderPrompts()
        {
            var path = ModulePaths.GetContentPath("Prompts");
            path = Path.Combine(path, "order_prompts.json");

            if (!File.Exists(path))
            {
                ModLogger.Warn(LogCategory, $"E-PROMPT-003: Order prompts file not found: {path}");
                _promptsByOrderType = new Dictionary<string, List<OrderPrompt>>();
                return;
            }

            var json = File.ReadAllText(path);
            var container = JsonConvert.DeserializeObject<OrderPromptContainer>(json);

            _promptsByOrderType = new Dictionary<string, List<OrderPrompt>>();

            foreach (var entry in container?.Prompts ?? new List<OrderPromptEntry>())
            {
                foreach (var orderType in entry.OrderTypes)
                {
                    if (!_promptsByOrderType.ContainsKey(orderType))
                    {
                        _promptsByOrderType[orderType] = new List<OrderPrompt>();
                    }
                    _promptsByOrderType[orderType].AddRange(entry.Prompts);
                }
            }

            ModLogger.Info(LogCategory, $"Loaded prompts for {_promptsByOrderType.Count} order types from {path}");
        }

        private static void LoadOutcomePools()
        {
            var path = ModulePaths.GetContentPath("Prompts");
            path = Path.Combine(path, "prompt_outcomes.json");

            if (!File.Exists(path))
            {
                ModLogger.Warn(LogCategory, $"E-PROMPT-004: Outcome pools file not found: {path}");
                _outcomePools = new Dictionary<PromptOutcome, List<PromptOutcomeData>>();
                return;
            }

            var json = File.ReadAllText(path);
            var container = JsonConvert.DeserializeObject<OutcomeContainer>(json);

            _outcomePools = new Dictionary<PromptOutcome, List<PromptOutcomeData>>
            {
                { PromptOutcome.Nothing, container?.Outcomes?.Nothing ?? new List<PromptOutcomeData>() },
                { PromptOutcome.SmallReward, container?.Outcomes?.SmallReward ?? new List<PromptOutcomeData>() },
                { PromptOutcome.EventChain, container?.Outcomes?.EventChain ?? new List<PromptOutcomeData>() },
                { PromptOutcome.Danger, container?.Outcomes?.Danger ?? new List<PromptOutcomeData>() }
            };

            ModLogger.Info(LogCategory, $"Loaded outcome pools: Nothing={_outcomePools[PromptOutcome.Nothing].Count}, " +
                $"SmallReward={_outcomePools[PromptOutcome.SmallReward].Count}, " +
                $"EventChain={_outcomePools[PromptOutcome.EventChain].Count}, " +
                $"Danger={_outcomePools[PromptOutcome.Danger].Count}");
        }

        private static void LoadConfiguration()
        {
            var path = ModulePaths.GetConfigPath("enlisted_config.json");

            if (!File.Exists(path))
            {
                ModLogger.Warn(LogCategory, "E-PROMPT-005: Config file not found, using defaults");
                return;
            }

            var json = File.ReadAllText(path);
            dynamic config = JsonConvert.DeserializeObject(json);

            if (config?.order_prompts != null)
            {
                _promptChance = config.order_prompts.prompt_chance ?? 0.15f;

                var weights = config.order_prompts.outcome_weights;
                if (weights != null)
                {
                    _outcomeWeights = new Dictionary<PromptOutcome, int>
                    {
                        { PromptOutcome.Nothing, weights.nothing ?? 50 },
                        { PromptOutcome.SmallReward, weights.small_reward ?? 30 },
                        { PromptOutcome.EventChain, weights.event_chain ?? 15 },
                        { PromptOutcome.Danger, weights.danger ?? 5 }
                    };
                }
            }
        }

        // JSON container classes
        private class OrderPromptContainer
        {
            [JsonProperty("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonProperty("prompts")]
            public List<OrderPromptEntry> Prompts { get; set; }
        }

        private class OrderPromptEntry
        {
            [JsonProperty("order_types")]
            public List<string> OrderTypes { get; set; }

            [JsonProperty("prompts")]
            public List<OrderPrompt> Prompts { get; set; }
        }

        private class OutcomeContainer
        {
            [JsonProperty("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonProperty("outcomes")]
            public OutcomePools Outcomes { get; set; }
        }

        private class OutcomePools
        {
            [JsonProperty("nothing")]
            public List<PromptOutcomeData> Nothing { get; set; }

            [JsonProperty("small_reward")]
            public List<PromptOutcomeData> SmallReward { get; set; }

            [JsonProperty("event_chain")]
            public List<PromptOutcomeData> EventChain { get; set; }

            [JsonProperty("danger")]
            public List<PromptOutcomeData> Danger { get; set; }
        }
    }
}
