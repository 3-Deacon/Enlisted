using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Loads storylet JSON from ModuleData/Enlisted/Storylets/*.json. Spec 0 ships NO
    /// storylet content — surface specs author and load. This catalog is the contract.
    /// </summary>
    public static class StoryletCatalog
    {
        private static Dictionary<string, Storylet> _byId =
            new Dictionary<string, Storylet>(StringComparer.OrdinalIgnoreCase);

        public static void LoadAll()
        {
            _byId.Clear();
            var dir = ModulePaths.GetContentPath("Storylets");
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("STORYLET", "no_storylet_dir", "Storylets directory not found: " + dir);
                return;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);
                    var arr = root["storylets"] as JArray ?? (root as JContainer);
                    if (arr == null)
                    {
                        continue;
                    }
                    foreach (var item in arr.OfType<JObject>())
                    {
                        var s = Parse(item);
                        if (s == null || string.IsNullOrEmpty(s.Id))
                        {
                            continue;
                        }
                        _byId[s.Id] = s;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("STORYLET", "Failed to load storylet file", ex,
                        new Dictionary<string, object> { ["file"] = file });
                }
            }
        }

        public static Storylet GetById(string id) =>
            !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var s) ? s : null;

        public static IEnumerable<Storylet> All => _byId.Values;

        public static IEnumerable<string> AllIds => _byId.Keys;

        private static Storylet Parse(JObject obj)
        {
            if (obj == null)
            {
                return null;
            }
            var s = new Storylet
            {
                Id = (string)obj["id"] ?? string.Empty,
                Title = (string)obj["title"] ?? string.Empty,
                Setup = (string)obj["setup"] ?? string.Empty,
                Theme = (string)obj["theme"] ?? string.Empty,
                Category = (string)obj["category"] ?? string.Empty,
                Severity = (string)obj["severity"] ?? "normal",
                Observational = (bool?)obj["observational"] ?? false,
                CooldownDays = (int?)obj["cooldown_days"] ?? 0,
                OneTime = (bool?)obj["one_time"] ?? false,
                Scope = ParseScope(obj["scope"] as JObject),
                Weight = ParseWeight(obj["weight"] as JObject),
            };
            s.Trigger = ParseStringList(obj["trigger"] as JArray);
            s.Immediate = ParseEffectList(obj["immediate"] as JArray);
            s.Options = ParseChoices(obj["options"] as JArray);
            s.ProfileRequires     = ParseStringList(obj["profile_requires"] as JArray);
            s.ClassAffinity       = ParseFloatDict(obj["class_affinity"] as JObject);
            s.IntentAffinity      = ParseFloatDict(obj["intent_affinity"] as JObject);
            s.RequiresCombatClass = ParseStringList(obj["requires_combat_class"] as JArray);
            s.RequiresCulture     = ParseStringList(obj["requires_culture"] as JArray);
            s.ExcludesCulture     = ParseStringList(obj["excludes_culture"] as JArray);
            s.RequiresLordTrait   = ParseStringList(obj["requires_lord_trait"] as JArray);
            s.ExcludesLordTrait   = ParseStringList(obj["excludes_lord_trait"] as JArray);
            s.Arc = ParseArc(obj["arc"] as JObject);
            return s;
        }

        private static ScopeDecl ParseScope(JObject obj)
        {
            var scope = new ScopeDecl();
            if (obj == null)
            {
                return scope;
            }
            scope.Context = (string)obj["context"] ?? "any";
            var slots = obj["slots"] as JObject;
            if (slots == null)
            {
                return scope;
            }
            foreach (var prop in slots.Properties())
            {
                var decl = prop.Value as JObject;
                if (decl == null)
                {
                    continue;
                }
                scope.Slots[prop.Name] = new SlotDecl
                {
                    Role = (string)decl["role"] ?? string.Empty,
                    Tag = (string)decl["tag"] ?? string.Empty
                };
            }
            return scope;
        }

        private static WeightDecl ParseWeight(JObject obj)
        {
            var w = new WeightDecl();
            if (obj == null)
            {
                return w;
            }
            w.Base = (float?)obj["base"] ?? 1.0f;
            var mods = obj["modifiers"] as JArray;
            if (mods == null)
            {
                return w;
            }
            foreach (var m in mods.OfType<JObject>())
            {
                w.Modifiers.Add(new WeightModifier
                {
                    If = (string)m["if"] ?? string.Empty,
                    Factor = (float?)m["factor"] ?? 1.0f
                });
            }
            return w;
        }

        private static List<string> ParseStringList(JArray arr)
        {
            var list = new List<string>();
            if (arr == null)
            {
                return list;
            }
            foreach (var item in arr)
            {
                list.Add((string)item);
            }
            return list;
        }

        private static Dictionary<string, float> ParseFloatDict(JObject obj)
        {
            var dict = new Dictionary<string, float>();
            if (obj == null)
            {
                return dict;
            }
            foreach (var prop in obj.Properties())
            {
                dict[prop.Name] = (float?)prop.Value ?? 1.0f;
            }
            return dict;
        }

        private static List<EffectDecl> ParseEffectList(JArray arr)
        {
            var list = new List<EffectDecl>();
            if (arr == null)
            {
                return list;
            }
            foreach (var item in arr.OfType<JObject>())
            {
                var apply = (string)item["apply"];
                if (string.IsNullOrEmpty(apply))
                {
                    continue;
                }
                var eff = new EffectDecl { Apply = apply };
                foreach (var prop in item.Properties())
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
                list.Add(eff);
            }
            return list;
        }

        private static ArcSpec ParseArc(JObject arcObj)
        {
            if (arcObj == null)
            {
                return null;
            }
            var arc = new ArcSpec
            {
                DurationHours = (int?)arcObj["duration_hours"] ?? 0,
                OnTransitionInterruptStorylet = (string)arcObj["on_transition_interrupt_storylet"] ?? string.Empty,
            };
            var phasesArr = arcObj["phases"] as JArray;
            if (phasesArr != null)
            {
                foreach (JObject p in phasesArr.OfType<JObject>())
                {
                    arc.Phases.Add(new ArcPhaseSpec
                    {
                        Id = (string)p["id"] ?? string.Empty,
                        DurationHours = (int?)p["duration_hours"] ?? 0,
                        PoolPrefix = (string)p["pool_prefix"] ?? string.Empty,
                        FireIntervalHours = (int?)p["fire_interval_hours"] ?? 0,
                        Delivery = (string)p["delivery"] ?? "auto",
                    });
                }
            }
            return arc;
        }

        private static List<Choice> ParseChoices(JArray arr)
        {
            var list = new List<Choice>();
            if (arr == null)
            {
                return list;
            }
            foreach (var item in arr.OfType<JObject>())
            {
                var c = new Choice
                {
                    Id = (string)item["id"] ?? string.Empty,
                    Text = (string)item["text"] ?? string.Empty,
                    Tooltip = (string)item["tooltip"] ?? string.Empty,
                    Trigger = ParseStringList(item["trigger"] as JArray),
                    Effects = ParseEffectList(item["effects"] as JArray),
                    EffectsSuccess = ParseEffectList(item["effectsSuccess"] as JArray),
                    EffectsFailure = ParseEffectList(item["effectsFailure"] as JArray),
                };
                var chain = item["chain"] as JObject;
                if (chain != null)
                {
                    c.Chain = new ChainDecl
                    {
                        Id = (string)chain["id"] ?? string.Empty,
                        Days = (int?)chain["days"] ?? 0,
                        When = (string)chain["when"] ?? string.Empty
                    };
                }
                list.Add(c);
            }
            return list;
        }
    }
}
