using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Qualities
{
    /// <summary>Loads quality definitions from ModuleData/Enlisted/Qualities/*.json at boot.</summary>
    public static class QualityCatalog
    {
        public static List<QualityDefinition> LoadAll()
        {
            var result = new List<QualityDefinition>();
            var dir = ModulePaths.GetContentPath("Qualities");
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("QUALITY", "no_quality_dir", "Quality definitions directory not found", LogCtx.Of("dir", dir));
                return result;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);
                    var defs = root["qualities"] as JArray;
                    if (defs == null)
                    {
                        continue;
                    }

                    foreach (var item in defs)
                    {
                        try
                        {
                            var def = ParseDefinition(item as JObject);
                            if (def != null)
                            {
                                result.Add(def);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("QUALITY", "bad_quality_parse", ex, LogCtx.Of("file", file));
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("QUALITY", "Failed to load quality definition file", ex, LogCtx.Of("file", file));
                }
            }

            return result;
        }

        private static QualityDefinition ParseDefinition(JObject obj)
        {
            if (obj == null)
            {
                return null;
            }

            var def = new QualityDefinition
            {
                Id = (string)obj["id"] ?? string.Empty,
                DisplayName = (string)obj["displayName"] ?? string.Empty,
                Min = (int?)obj["min"] ?? 0,
                Max = (int?)obj["max"] ?? 100,
                Default = (int?)obj["default"] ?? 0,
                Scope = ParseEnum((string)obj["scope"], QualityScope.Global),
                Kind = ParseEnum((string)obj["kind"], QualityKind.Stored),
                Writable = (bool?)obj["writable"] ?? true,
            };

            var decayObj = obj["decay"] as JObject;
            if (decayObj != null)
            {
                def.Decay = new DecayRule
                {
                    Amount = (int?)decayObj["amount"] ?? -1,
                    IntervalDays = (int?)decayObj["intervalDays"] ?? 5,
                    RequireIdle = (bool?)decayObj["requireIdle"] ?? true,
                    Floor = (int?)decayObj["floor"] ?? def.Min,
                };
            }

            return string.IsNullOrEmpty(def.Id) ? null : def;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return !string.IsNullOrEmpty(value) && Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
                ? parsed
                : fallback;
        }
    }
}
