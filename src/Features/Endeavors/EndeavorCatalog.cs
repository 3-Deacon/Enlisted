using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Loads and queries endeavor templates from
    /// ModuleData/Enlisted/Endeavors/endeavor_catalog.json. Schema at
    /// docs/Features/Endeavors/endeavor-catalog-schema.md. LoadAll runs from the
    /// EndeavorRunner host behavior's OnSessionLaunched handler — content
    /// catalogs initialize after OnGameStart, never during it (AGENTS.md
    /// pitfall #18).
    /// </summary>
    public static class EndeavorCatalog
    {
        private static readonly Dictionary<string, EndeavorTemplate> _byId =
            new Dictionary<string, EndeavorTemplate>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<EndeavorTemplate>> _byCategory =
            new Dictionary<string, List<EndeavorTemplate>>(StringComparer.OrdinalIgnoreCase);

        public static int Count => _byId.Count;

        public static IReadOnlyCollection<EndeavorTemplate> All => _byId.Values;

        public static EndeavorTemplate GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            return _byId.TryGetValue(id, out var t) ? t : null;
        }

        public static IReadOnlyList<EndeavorTemplate> GetByCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return Array.Empty<EndeavorTemplate>();
            }
            return _byCategory.TryGetValue(category, out var list)
                ? (IReadOnlyList<EndeavorTemplate>)list
                : Array.Empty<EndeavorTemplate>();
        }

        public static void LoadAll()
        {
            _byId.Clear();
            _byCategory.Clear();

            var dir = ModulePaths.GetContentPath("Endeavors");
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("ENDEAVOR", "no_endeavors_dir",
                    "Endeavors directory not found: " + dir);
                return;
            }

            var path = Path.Combine(dir, "endeavor_catalog.json");
            if (!File.Exists(path))
            {
                ModLogger.Expected("ENDEAVOR", "no_endeavor_catalog",
                    "endeavor_catalog.json not found at: " + path);
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var root = JObject.Parse(json);
                var entries = root["endeavors"] as JArray;
                if (entries == null)
                {
                    ModLogger.Surfaced("ENDEAVOR",
                        "endeavor_catalog.json missing top-level 'endeavors' array",
                        new InvalidDataException(path));
                    return;
                }

                foreach (var entry in entries.OfType<JObject>())
                {
                    var template = ParseTemplate(entry);
                    if (template == null || string.IsNullOrEmpty(template.Id))
                    {
                        continue;
                    }
                    _byId[template.Id] = template;
                    if (!_byCategory.TryGetValue(template.Category, out var list))
                    {
                        list = new List<EndeavorTemplate>();
                        _byCategory[template.Category] = list;
                    }
                    list.Add(template);
                }

                ModLogger.Info("ENDEAVOR", $"EndeavorCatalog loaded {_byId.Count} templates across {_byCategory.Count} categories");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENDEAVOR", "Failed to load endeavor_catalog.json", ex,
                    new Dictionary<string, object> { ["file"] = path });
            }
        }

        private static EndeavorTemplate ParseTemplate(JObject root)
        {
            var t = new EndeavorTemplate
            {
                Id = (string)root["id"] ?? string.Empty,
                Category = (string)root["category"] ?? string.Empty,
                TitleId = (string)root["title_id"] ?? string.Empty,
                Title = (string)root["title"] ?? string.Empty,
                DescriptionId = (string)root["description_id"] ?? string.Empty,
                Description = (string)root["description"] ?? string.Empty,
                DurationDays = (int?)root["duration_days"] ?? 0,
                PhaseOffsetsHours = (root["phase_offsets_hours"] as JArray)?
                    .Select(x => (int?)x ?? 0).ToList() ?? new List<int>(),
                PhasePool = (root["phase_pool"] as JArray)?
                    .Select(x => (string)x).Where(s => !string.IsNullOrEmpty(s)).ToList()
                    ?? new List<string>(),
                SkillAxis = (root["skill_axis"] as JArray)?
                    .Select(x => (string)x).Where(s => !string.IsNullOrEmpty(s)).ToList()
                    ?? new List<string>(),
                SelfGateSkillThreshold = (int?)root["self_gate_skill_threshold"] ?? 0,
                ScrutinyRiskPerPhase = (float?)root["scrutiny_risk_per_phase"] ?? 0f,
                TierMin = (int?)root["tier_min"] ?? 1,
                FlagGates = (root["flag_gates"] as JArray)?
                    .Select(x => (string)x).Where(s => !string.IsNullOrEmpty(s)).ToList()
                    ?? new List<string>(),
                FlagBlockers = (root["flag_blockers"] as JArray)?
                    .Select(x => (string)x).Where(s => !string.IsNullOrEmpty(s)).ToList()
                    ?? new List<string>(),
                Tags = (root["tags"] as JArray)?
                    .Select(x => (string)x).Where(s => !string.IsNullOrEmpty(s)).ToList()
                    ?? new List<string>(),
            };

            var rs = root["resolution_storylets"] as JObject;
            if (rs != null)
            {
                t.ResolutionSuccessId = (string)rs["success"] ?? string.Empty;
                t.ResolutionPartialId = (string)rs["partial"] ?? string.Empty;
                t.ResolutionFailureId = (string)rs["failure"] ?? string.Empty;
            }

            var rt = root["resolution_thresholds"] as JObject;
            if (rt != null)
            {
                t.ResolutionSuccessMinScore = (int?)rt["success_min_score"] ?? 2;
                t.ResolutionFailureMaxScore = (int?)rt["failure_max_score"] ?? -2;
            }

            var slots = root["companion_slots"] as JArray;
            if (slots != null)
            {
                foreach (var s in slots.OfType<JObject>())
                {
                    t.CompanionSlots.Add(new EndeavorCompanionSlot
                    {
                        Archetype = (string)s["archetype"] ?? string.Empty,
                        Role = (string)s["role"] ?? string.Empty,
                        Required = (bool?)s["required"] ?? false,
                        SkillBonusAxis = (string)s["skill_bonus_axis"] ?? string.Empty,
                        SkillBonusAmount = (int?)s["skill_bonus_amount"] ?? 0,
                    });
                }
            }

            return t;
        }
    }
}
