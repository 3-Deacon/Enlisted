using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Content
{
    /// <summary>Resolves named scripted effect ids to their primitive effect bodies. Loaded at boot.</summary>
    public static class ScriptedEffectRegistry
    {
        private static Dictionary<string, List<EffectDecl>> _registry =
            new Dictionary<string, List<EffectDecl>>(StringComparer.OrdinalIgnoreCase);

        public static void LoadAll()
        {
            _registry.Clear();
            var dir = ModulePaths.GetContentPath("Effects");
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("EFFECT", "no_effects_dir", "Scripted-effects directory not found: " + dir);
                return;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);
                    var effects = root["effects"] as JObject;
                    if (effects == null)
                    {
                        continue;
                    }
                    foreach (var kv in effects)
                    {
                        var id = kv.Key;
                        var arr = kv.Value as JArray;
                        if (arr == null)
                        {
                            continue;
                        }
                        var list = new List<EffectDecl>();
                        foreach (var item in arr)
                        {
                            var parsed = ParseEffect(item as JObject);
                            if (parsed != null)
                            {
                                list.Add(parsed);
                            }
                        }
                        _registry[id] = list;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("EFFECT", "Failed to load scripted-effects file", ex,
                        new Dictionary<string, object> { ["file"] = file });
                }
            }
        }

        public static IList<EffectDecl> Resolve(string id) =>
            !string.IsNullOrEmpty(id) && _registry.TryGetValue(id, out var list) ? list : null;

        public static IEnumerable<string> AllIds => _registry.Keys;

        private static EffectDecl ParseEffect(JObject obj)
        {
            if (obj == null)
            {
                return null;
            }
            var apply = (string)obj["apply"];
            if (string.IsNullOrEmpty(apply))
            {
                return null;
            }
            var eff = new EffectDecl { Apply = apply };
            foreach (var prop in obj.Properties())
            {
                if (prop.Name == "apply")
                {
                    continue;
                }
                eff.Params[prop.Name] = prop.Value.Type switch
                {
                    JTokenType.Integer => (object)(long)prop.Value,
                    JTokenType.Float => (object)(double)prop.Value,
                    JTokenType.Boolean => (object)(bool)prop.Value,
                    _ => (object)(string)prop.Value
                };
            }
            return eff;
        }
    }
}
