using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Activities
{
    /// <summary>
    /// Loads ActivityTypeDefinition JSON from ModuleData/Enlisted/Activities/*.json
    /// and registers each with ActivityRuntime. Spec 0 leaves _types empty by design;
    /// surface specs (1-5) drop files here, and this loader wires them up at session launch.
    /// </summary>
    public static class ActivityTypeCatalog
    {
        public static void LoadAll()
        {
            var runtime = ActivityRuntime.Instance;
            if (runtime == null)
            {
                ModLogger.Expected("ACTIVITY", "catalog_no_runtime",
                    "ActivityRuntime.Instance null at LoadAll — order-of-boot issue");
                return;
            }

            var dir = ModulePaths.GetContentPath("Activities");
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("ACTIVITY", "no_activities_dir",
                    "Activities directory not found: " + dir);
                return;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);
                    var def = Parse(root);
                    if (def == null || string.IsNullOrEmpty(def.Id))
                    {
                        ModLogger.Expected("ACTIVITY", "bad_activity_file",
                            "Activity JSON missing id: " + file);
                        continue;
                    }
                    runtime.RegisterType(def);
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("ACTIVITY", "Failed to load activity type",
                        ex, new Dictionary<string, object> { ["file"] = file });
                }
            }
        }

        private static ActivityTypeDefinition Parse(JObject root)
        {
            if (root == null) { return null; }
            var def = new ActivityTypeDefinition
            {
                Id = (string)root["id"] ?? string.Empty,
                DisplayName = (string)root["display_name"] ?? string.Empty,
                ValidIntents = (root["valid_intents"] as JArray)?
                    .Select(t => (string)t)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList() ?? new List<string>(),
                Phases = new List<Phase>()
            };
            var phases = root["phases"] as JArray;
            if (phases == null) { return def; }
            foreach (var p in phases.OfType<JObject>())
            {
                def.Phases.Add(new Phase
                {
                    Id = (string)p["id"] ?? string.Empty,
                    DurationHours = (int?)p["duration_hours"] ?? 0,
                    FireIntervalHours = (int?)p["fire_interval_hours"] ?? 0,
                    Pool = (p["pool"] as JArray)?.Select(t => (string)t).Where(s => !string.IsNullOrEmpty(s)).ToList()
                           ?? new List<string>(),
                    IntentBias = ParseIntentBias(p["intent_bias"] as JObject),
                    Delivery = ParseDelivery((string)p["delivery"]),
                    PlayerChoiceCount = (int?)p["player_choice_count"] ?? 4
                });
            }
            return def;
        }

        private static Dictionary<string, float> ParseIntentBias(JObject obj)
        {
            var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (obj == null) { return dict; }
            foreach (var prop in obj.Properties())
            {
                dict[prop.Name] = (float?)prop.Value ?? 1.0f;
            }
            return dict;
        }

        private static PhaseDelivery ParseDelivery(string s)
        {
            if (string.IsNullOrEmpty(s)) { return PhaseDelivery.Auto; }
            return string.Equals(s, "player_choice", StringComparison.OrdinalIgnoreCase)
                ? PhaseDelivery.PlayerChoice
                : PhaseDelivery.Auto;
        }
    }
}
