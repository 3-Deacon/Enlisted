using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Companions.Data
{
    /// <summary>
    /// Loads and indexes companion archetype dialog catalogs from
    /// ModuleData/Enlisted/Dialogue/companion_*.json. Plan 2 ships the substrate;
    /// Plans 3-7 add live conversation rendering against the loaded nodes.
    ///
    /// Schema: every file has schemaVersion=1, dialogueType="companion".
    /// Nodes share QMDialogueCatalog's id/speaker/text/context/options shape but
    /// the context.archetype field is the load-bearing discriminator (one node id
    /// can have three variants — one per archetype — and the catalog picks the
    /// most specific match at runtime).
    /// </summary>
    public sealed class CompanionDialogueCatalog
    {
        private const string LogCategory = "COMPANION_DIALOG";
        private static CompanionDialogueCatalog _instance;

        private readonly Dictionary<string, List<CompanionDialogueNode>> _nodesById
            = new Dictionary<string, List<CompanionDialogueNode>>(StringComparer.Ordinal);

        private bool _initialized;

        public static CompanionDialogueCatalog Instance =>
            _instance ?? (_instance = new CompanionDialogueCatalog());

        public int NodeCount { get; private set; }
        public int FileCount { get; private set; }

        public void LoadFromJson()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            _nodesById.Clear();
            NodeCount = 0;
            FileCount = 0;

            try
            {
                var basePath = ModulePaths.GetContentPath("Dialogue");
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                {
                    ModLogger.Expected("COMPANION_DIALOG", "no_dialogue_dir",
                        "dialogue directory not found");
                    return;
                }

                var files = Directory.GetFiles(basePath, "companion_*.json",
                    SearchOption.TopDirectoryOnly);

                foreach (var path in files)
                {
                    try
                    {
                        var added = LoadFile(path);
                        if (added > 0)
                        {
                            FileCount++;
                            NodeCount += added;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("COMPANION_DIALOG",
                            "load_file_failed", ex,
                            LogCtx.Of("file", Path.GetFileName(path)));
                    }
                }

                ModLogger.Info(LogCategory,
                    $"Loaded {NodeCount} companion dialogue nodes from {FileCount} files");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION_DIALOG", "load_root_failed", ex);
            }
        }

        public CompanionDialogueNode GetNode(string nodeId, CompanionDialogueContext ctx)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            if (!_nodesById.TryGetValue(nodeId, out var variants)) return null;

            CompanionDialogueNode best = null;
            int bestSpec = -1;

            foreach (var node in variants)
            {
                if (node.Context != null && !node.Context.Matches(ctx))
                {
                    continue;
                }
                var spec = node.Context?.GetSpecificity() ?? 0;
                if (spec > bestSpec)
                {
                    bestSpec = spec;
                    best = node;
                }
            }

            return best;
        }

        public IReadOnlyList<CompanionDialogueNode> GetAllNodesById(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return Array.Empty<CompanionDialogueNode>();
            return _nodesById.TryGetValue(nodeId, out var list)
                ? (IReadOnlyList<CompanionDialogueNode>)list
                : Array.Empty<CompanionDialogueNode>();
        }

        public IReadOnlyCollection<string> AllNodeIds => _nodesById.Keys;

        // ---------- Internal load helpers ----------

        private int LoadFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var json = File.ReadAllText(filePath);
            var root = JObject.Parse(json);

            var schemaVersion = root.Value<int?>("schemaVersion") ?? 0;
            if (schemaVersion != 1)
            {
                ModLogger.Expected("COMPANION_DIALOG", "schema_version_mismatch",
                    $"{fileName}: unsupported schemaVersion {schemaVersion}");
                return 0;
            }

            var dialogueType = root.Value<string>("dialogueType");
            if (!string.Equals(dialogueType, "companion", StringComparison.OrdinalIgnoreCase))
            {
                // Not a companion catalog — silently skip (QM files share the directory).
                return 0;
            }

            var nodesArray = root.Value<JArray>("nodes");
            if (nodesArray == null) return 0;

            var added = 0;
            foreach (var token in nodesArray.OfType<JObject>())
            {
                var node = ParseNode(token);
                if (node == null || string.IsNullOrEmpty(node.Id)) continue;

                if (!_nodesById.TryGetValue(node.Id, out var list))
                {
                    list = new List<CompanionDialogueNode>();
                    _nodesById[node.Id] = list;
                }
                list.Add(node);
                added++;
            }
            return added;
        }

        private CompanionDialogueNode ParseNode(JObject obj)
        {
            var node = new CompanionDialogueNode
            {
                Id = obj.Value<string>("id") ?? string.Empty,
                Speaker = obj.Value<string>("speaker") ?? "companion",
                TextId = obj.Value<string>("textId") ?? string.Empty,
                Text = obj.Value<string>("text") ?? string.Empty
            };

            var ctxObj = obj.Value<JObject>("context");
            if (ctxObj != null)
            {
                node.Context = ParseContext(ctxObj);
            }

            var optionsArr = obj.Value<JArray>("options");
            if (optionsArr != null)
            {
                foreach (var optObj in optionsArr.OfType<JObject>())
                {
                    var opt = ParseOption(optObj);
                    if (opt != null) node.Options.Add(opt);
                }
            }

            return node;
        }

        private static CompanionDialogueContext ParseContext(JObject obj)
        {
            return new CompanionDialogueContext
            {
                CompanionType = obj.Value<string>("companion_type"),
                Archetype = obj.Value<string>("archetype"),
                IsIntroduced = obj.Value<bool?>("is_introduced"),
                RelationshipTier = obj.Value<string>("relationship_tier"),
                PlayerTier = obj.Value<int?>("player_tier"),
                TierMin = obj.Value<int?>("tier_min"),
                TierMax = obj.Value<int?>("tier_max"),
                HasRecentBattle = obj.Value<bool?>("has_recent_battle")
            };
        }

        private static CompanionDialogueOption ParseOption(JObject obj)
        {
            var opt = new CompanionDialogueOption
            {
                Id = obj.Value<string>("id") ?? string.Empty,
                TextId = obj.Value<string>("textId") ?? string.Empty,
                Text = obj.Value<string>("text") ?? string.Empty,
                Tooltip = obj.Value<string>("tooltip"),
                NextNode = obj.Value<string>("next_node"),
                Action = obj.Value<string>("action")
            };

            var actionData = obj.Value<JObject>("action_data");
            if (actionData != null)
            {
                opt.ActionData = new Dictionary<string, object>();
                foreach (var prop in actionData.Properties())
                {
                    opt.ActionData[prop.Name] = prop.Value.ToObject<object>();
                }
            }

            var requirements = obj.Value<JObject>("requirements");
            if (requirements != null)
            {
                opt.Requirements = ParseContext(requirements);
            }

            return opt;
        }
    }

    public class CompanionDialogueNode
    {
        public string Id { get; set; }
        public string Speaker { get; set; }
        public string TextId { get; set; }
        public string Text { get; set; }
        public CompanionDialogueContext Context { get; set; }
        public List<CompanionDialogueOption> Options { get; } = new List<CompanionDialogueOption>();
    }

    public class CompanionDialogueOption
    {
        public string Id { get; set; }
        public string TextId { get; set; }
        public string Text { get; set; }
        public string Tooltip { get; set; }
        public string NextNode { get; set; }
        public string Action { get; set; }
        public Dictionary<string, object> ActionData { get; set; }
        public CompanionDialogueContext Requirements { get; set; }
    }

    public class CompanionDialogueContext
    {
        public string CompanionType { get; set; }
        public string Archetype { get; set; }
        public bool? IsIntroduced { get; set; }
        public string RelationshipTier { get; set; }
        public int? PlayerTier { get; set; }
        public int? TierMin { get; set; }
        public int? TierMax { get; set; }
        public bool? HasRecentBattle { get; set; }

        public bool Matches(CompanionDialogueContext actual)
        {
            if (actual == null) return false;

            if (CompanionType != null
                && !string.Equals(CompanionType, actual.CompanionType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (Archetype != null
                && !string.Equals(Archetype, actual.Archetype, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (IsIntroduced.HasValue
                && IsIntroduced.Value != actual.IsIntroduced.GetValueOrDefault())
            {
                return false;
            }
            if (RelationshipTier != null
                && !string.Equals(RelationshipTier, actual.RelationshipTier, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            var actualTier = actual.PlayerTier ?? 1;
            if (TierMin.HasValue && actualTier < TierMin.Value) return false;
            if (TierMax.HasValue && actualTier > TierMax.Value) return false;
            if (HasRecentBattle.HasValue
                && HasRecentBattle.Value != actual.HasRecentBattle.GetValueOrDefault())
            {
                return false;
            }
            return true;
        }

        public int GetSpecificity()
        {
            int n = 0;
            if (CompanionType != null) n++;
            if (Archetype != null) n++;
            if (IsIntroduced.HasValue) n++;
            if (RelationshipTier != null) n++;
            if (PlayerTier.HasValue) n++;
            if (TierMin.HasValue) n++;
            if (TierMax.HasValue) n++;
            if (HasRecentBattle.HasValue) n++;
            return n;
        }
    }
}
