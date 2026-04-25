using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.Localization;

namespace Enlisted.Features.Conversations.Data
{
    /// <summary>
    /// Catalog of quartermaster dialogue nodes loaded from JSON files.
    /// Handles context-conditional node selection and XML localization.
    /// </summary>
    public class QMDialogueCatalog
    {
        private const string LogCategory = "QMDialogueCatalog";
        private static QMDialogueCatalog _instance;
        private readonly Dictionary<string, List<QMDialogueNode>> _nodesByid;
        private bool _initialized;

        private QMDialogueCatalog()
        {
            _nodesByid = new Dictionary<string, List<QMDialogueNode>>();
        }

        public static QMDialogueCatalog Instance => _instance ?? (_instance = new QMDialogueCatalog());

        /// <summary>
        /// Total number of nodes loaded.
        /// </summary>
        public int NodeCount { get; private set; }

        /// <summary>
        /// Loads all dialogue JSON files from ModuleData/Enlisted/Dialogue/.
        /// </summary>
        public void LoadFromJson()
        {
            if (_initialized)
            {
                ModLogger.Debug(LogCategory, "Already initialized, skipping reload");
                return;
            }

            _nodesByid.Clear();
            NodeCount = 0;

            var dialoguePath = GetDialogueBasePath();
            if (string.IsNullOrEmpty(dialoguePath) || !Directory.Exists(dialoguePath))
            {
                ModLogger.Warn(LogCategory, $"Dialogue directory not found: {dialoguePath}");
                return;
            }

            // Filter to QM catalogs only — the Dialogue/ directory now also hosts
            // companion_*.json from the wanderer companion substrate (Plan 2),
            // which use dialogueType="companion" and would otherwise warn on
            // every launch when the QM-type check rejects them.
            var jsonFiles = Directory.GetFiles(dialoguePath, "qm_*.json", SearchOption.TopDirectoryOnly);
            var filesLoaded = 0;

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var nodesLoaded = LoadDialogueFile(filePath);
                    if (nodesLoaded > 0)
                    {
                        filesLoaded++;
                        NodeCount += nodesLoaded;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("QMDialogueCatalog", $"Failed to load dialogue from {Path.GetFileName(filePath)}", ex);
                }
            }

            _initialized = true;
            ModLogger.Info(LogCategory, $"Loaded {NodeCount} dialogue nodes from {filesLoaded} files");
        }

        /// <summary>
        /// Gets the best matching dialogue node for the given ID and context.
        /// Returns the node with the most specific matching context, or null if no match found.
        /// </summary>
        public QMDialogueNode GetNode(string nodeId, QMDialogueContext actualContext)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return null;
            }

            if (!_nodesByid.TryGetValue(nodeId, out var candidates))
            {
                ModLogger.Debug(LogCategory, $"No nodes found with ID: {nodeId}");
                return null;
            }

            // Filter to nodes where context matches
            var matchingNodes = candidates.Where(n => n.Context == null || n.Context.Matches(actualContext)).ToList();

            if (matchingNodes.Count == 0)
            {
                ModLogger.Debug(LogCategory, $"No matching nodes for ID '{nodeId}' with current context");
                return null;
            }

            // Sort by specificity (most specific first)
            var bestMatch = matchingNodes
                .OrderByDescending(n => n.Context?.GetSpecificity() ?? 0)
                .First();

            return bestMatch;
        }

        /// <summary>
        /// Gets all node variants for a given node ID, regardless of context.
        /// Used for registering multiple conditional dialogue lines.
        /// </summary>
        public List<QMDialogueNode> GetAllNodesById(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return new List<QMDialogueNode>();
            }

            if (!_nodesByid.TryGetValue(nodeId, out var candidates))
            {
                return new List<QMDialogueNode>();
            }

            return candidates;
        }

        /// <summary>
        /// Resolves localized text from XML using TextObject pattern.
        /// Returns fallback if XML lookup fails.
        /// </summary>
        public string ResolveText(string textId, string fallback)
        {
            if (string.IsNullOrEmpty(textId))
            {
                return fallback ?? string.Empty;
            }

            // Use Bannerlord's TextObject for XML localization
            // Pattern: {=stringId}Fallback text
            var textObject = new TextObject($"{{={textId}}}{fallback ?? string.Empty}");
            return textObject.ToString();
        }

        /// <summary>
        /// Gets the base path for dialogue JSON files.
        /// Uses ModulePaths utility for correct resolution with both manual and Workshop installs.
        /// </summary>
        private string GetDialogueBasePath()
        {
            var path = ModulePaths.GetContentPath("Dialogue");
            ModLogger.Debug(LogCategory, $"Using dialogue path: {path}");
            return path;
        }

        /// <summary>
        /// Loads dialogue nodes from a single JSON file.
        /// </summary>
        private int LoadDialogueFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var json = File.ReadAllText(filePath);
            var root = JObject.Parse(json);

            // Validate schema
            var schemaVersion = root["schemaVersion"]?.Value<int>() ?? 0;
            if (schemaVersion != 1)
            {
                ModLogger.Warn(LogCategory, $"{fileName}: Unsupported schema version {schemaVersion}");
                return 0;
            }

            var dialogueType = root["dialogueType"]?.ToString();
            if (!"quartermaster".Equals(dialogueType, StringComparison.OrdinalIgnoreCase))
            {
                ModLogger.Warn(LogCategory, $"{fileName}: Expected dialogueType 'quartermaster', got '{dialogueType}'");
                return 0;
            }

            // Parse nodes array
            var nodesArray = root["nodes"] as JArray;
            if (nodesArray == null || nodesArray.Count == 0)
            {
                ModLogger.Debug(LogCategory, $"{fileName}: No nodes found");
                return 0;
            }

            var nodesLoaded = 0;
            foreach (var nodeToken in nodesArray)
            {
                try
                {
                    var node = ParseNode(nodeToken as JObject);
                    if (node != null && !string.IsNullOrEmpty(node.Id))
                    {
                        if (!_nodesByid.ContainsKey(node.Id))
                        {
                            _nodesByid[node.Id] = new List<QMDialogueNode>();
                        }

                        _nodesByid[node.Id].Add(node);
                        nodesLoaded++;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("QMDialogueCatalog", $"Failed to parse node in {fileName}", ex);
                }
            }

            return nodesLoaded;
        }

        /// <summary>
        /// Parses a single dialogue node from JSON.
        /// </summary>
        private QMDialogueNode ParseNode(JObject nodeJson)
        {
            if (nodeJson == null)
            {
                return null;
            }

            var node = new QMDialogueNode
            {
                Id = nodeJson["id"]?.ToString() ?? string.Empty,
                Speaker = nodeJson["speaker"]?.ToString() ?? "quartermaster",
                TextId = nodeJson["textId"]?.ToString() ?? string.Empty,
                Text = nodeJson["text"]?.ToString() ?? string.Empty
            };

            // Parse context conditions
            var contextJson = nodeJson["context"] as JObject;
            if (contextJson != null)
            {
                node.Context = ParseContext(contextJson);
            }

            // Parse options
            var optionsArray = nodeJson["options"] as JArray;
            if (optionsArray != null)
            {
                foreach (var optToken in optionsArray)
                {
                    var option = ParseOption(optToken as JObject);
                    if (option != null)
                    {
                        node.Options.Add(option);
                    }
                }
            }

            return node;
        }

        /// <summary>
        /// Parses context conditions from JSON.
        /// </summary>
        private QMDialogueContext ParseContext(JObject contextJson)
        {
            if (contextJson == null)
            {
                return new QMDialogueContext();
            }

            var context = new QMDialogueContext
            {
                SupplyLevel = contextJson["supply_level"]?.ToString(),
                Archetype = contextJson["archetype"]?.ToString(),
                ReputationTier = contextJson["reputation_tier"]?.ToString(),
                PlayerStyle = contextJson["player_style"]?.ToString(),
                IsIntroduced = contextJson["is_introduced"]?.Value<bool?>(),
                PlayerTier = contextJson["player_tier"]?.Value<int?>(), // For actual context
                TierMin = contextJson["tier_min"]?.Value<int?>(), // For node requirements
                TierMax = contextJson["tier_max"]?.Value<int?>(), // For node requirements
                TierCategory = contextJson["tier_category"]?.ToString(),
                Formation = contextJson["formation"]?.ToString(),
                IsCavalry = contextJson["is_cavalry"]?.Value<bool?>(),
                IsOfficer = contextJson["is_officer"]?.Value<bool?>(),
                RecentEvent = contextJson["recent_event"]?.ToString(),
                HasRecentBattle = contextJson["has_recent_battle"]?.Value<bool?>(),
                HighCasualties = contextJson["high_casualties"]?.Value<bool?>(),
                HasFlag = contextJson["has_flag"]?.ToString(),
                DaysEnlisted = contextJson["days_enlisted"]?.Value<int?>(),
                RecentlyPromoted = contextJson["recently_promoted"]?.Value<bool?>(),
                LastPurchaseCategory = contextJson["last_purchase_category"]?.ToString(),
                BaggageRequestType = contextJson["baggage_request_type"]?.ToString(),
                BaggageAccess = contextJson["baggage_access"]?.ToString(),
                BaggageDelayed = contextJson["baggage_delayed"]?.Value<bool?>()
            };

            return context;
        }

        /// <summary>
        /// Parses a dialogue option from JSON.
        /// </summary>
        private QMDialogueOption ParseOption(JObject optionJson)
        {
            if (optionJson == null)
            {
                return null;
            }

            var option = new QMDialogueOption
            {
                Id = optionJson["id"]?.ToString() ?? string.Empty,
                TextId = optionJson["textId"]?.ToString() ?? string.Empty,
                Text = optionJson["text"]?.ToString() ?? string.Empty,
                Tooltip = optionJson["tooltip"]?.ToString() ?? string.Empty,
                NextNode = optionJson["next_node"]?.ToString() ?? string.Empty,
                Action = optionJson["action"]?.ToString() ?? "none"
            };

            // Parse action data
            var actionDataJson = optionJson["action_data"] as JObject;
            if (actionDataJson != null)
            {
                option.ActionData = new Dictionary<string, object>();
                foreach (var prop in actionDataJson.Properties())
                {
                    option.ActionData[prop.Name] = prop.Value.ToObject<object>();
                }
            }

            // Parse requirements
            var reqJson = optionJson["requirements"] as JObject;
            if (reqJson != null)
            {
                option.Requirements = ParseContext(reqJson);
            }

            // Parse gate condition
            var gateJson = optionJson["gate"] as JObject;
            if (gateJson != null)
            {
                option.Gate = new GateCondition
                {
                    Condition = gateJson["condition"]?.ToString() ?? string.Empty,
                    GateNode = gateJson["gate_node"]?.ToString() ?? string.Empty
                };
            }

            return option;
        }

        /// <summary>
        /// Resets the catalog. Used for testing.
        /// </summary>
        public void Reset()
        {
            _nodesByid.Clear();
            NodeCount = 0;
            _initialized = false;
        }
    }
}

