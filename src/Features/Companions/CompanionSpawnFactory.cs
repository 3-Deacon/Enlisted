using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Companions
{
    /// <summary>
    /// Spawns the six wanderer-mechanics companion archetypes by reading
    /// ModuleData/Enlisted/Companions/archetype_catalog.json and applying the
    /// HeroCreator + post-spawn customization recipe inherited from the
    /// quartermaster precedent at EnlistmentBehavior.GetOrCreateQuartermaster.
    /// </summary>
    public static class CompanionSpawnFactory
    {
        private const string LogCategory = "COMPANION";
        private const int ExpectedSchemaVersion = 1;

        private static readonly Dictionary<string, CompanionTypeEntry> Catalog =
            new Dictionary<string, CompanionTypeEntry>(StringComparer.Ordinal);

        private static bool _loaded;
        private static bool _loadFailed;

        public static IReadOnlyCollection<string> KnownTypeIds => Catalog.Keys;

        /// <summary>
        /// Loads and validates the archetype catalog. Idempotent: subsequent calls
        /// no-op once loaded. Returns false if the catalog file is missing or
        /// validation failed (callers should treat all spawn requests as failures).
        /// </summary>
        public static bool LoadCatalog()
        {
            if (_loaded)
            {
                return !_loadFailed;
            }

            _loaded = true;
            _loadFailed = false;

            try
            {
                var basePath = ModulePaths.GetContentPath("Companions");
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                {
                    ModLogger.Expected("COMPANION", "no_companions_dir",
                        "directory not found; companion spawns disabled");
                    _loadFailed = true;
                    return false;
                }

                var path = Path.Combine(basePath, "archetype_catalog.json");
                if (!File.Exists(path))
                {
                    ModLogger.Expected("COMPANION", "no_archetype_catalog",
                        "archetype_catalog.json not found; companion spawns disabled");
                    _loadFailed = true;
                    return false;
                }

                var json = JObject.Parse(File.ReadAllText(path));
                var schemaVersion = json.Value<int?>("schemaVersion") ?? -1;
                if (schemaVersion != ExpectedSchemaVersion)
                {
                    ModLogger.Surfaced("COMPANION", "schema_version_mismatch",
                        new InvalidDataException($"expected {ExpectedSchemaVersion}, found {schemaVersion}"));
                    _loadFailed = true;
                    return false;
                }

                var typesArray = json.Value<JArray>("companionTypes");
                if (typesArray == null || typesArray.Count == 0)
                {
                    ModLogger.Surfaced("COMPANION", "empty_catalog", null);
                    _loadFailed = true;
                    return false;
                }

                Catalog.Clear();
                foreach (var item in typesArray.OfType<JObject>())
                {
                    var entry = ParseCompanionType(item);
                    if (entry == null)
                    {
                        continue;
                    }
                    if (Catalog.ContainsKey(entry.Id))
                    {
                        ModLogger.Surfaced("COMPANION", "duplicate_companion_type", null,
                            LogCtx.Of("id", entry.Id));
                        continue;
                    }
                    Catalog[entry.Id] = entry;
                }

                ModLogger.Info(LogCategory,
                    $"Loaded {Catalog.Count} companion archetype entries from {path}");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("COMPANION", "catalog_load_failed", ex);
                _loadFailed = true;
                return false;
            }
        }

        /// <summary>
        /// Looks up the configured unlock tier for a companion type. Returns null
        /// if the type isn't in the catalog or the catalog isn't loaded.
        /// </summary>
        public static int? GetUnlockTier(string typeId)
        {
            if (!LoadCatalog() || string.IsNullOrEmpty(typeId)) return null;
            return Catalog.TryGetValue(typeId, out var entry) ? entry.UnlockTier : (int?)null;
        }

        /// <summary>
        /// Returns "per_player" or "per_lord" for a known companion type, else null.
        /// </summary>
        public static string GetLifecycle(string typeId)
        {
            if (!LoadCatalog() || string.IsNullOrEmpty(typeId)) return null;
            return Catalog.TryGetValue(typeId, out var entry) ? entry.Lifecycle : null;
        }

        /// <summary>
        /// Spawns a companion of the requested type into the owning clan. The hero
        /// is added to MobileParty.MainParty's MemberRoster. Returns null if the
        /// catalog isn't loaded, the type is unknown, or the underlying
        /// HeroCreator call fails.
        /// </summary>
        public static Hero SpawnCompanion(
            string typeId,
            Clan owningClan,
            Settlement bornSettlement,
            out string archetypeId)
        {
            archetypeId = null;

            if (!LoadCatalog())
            {
                return null;
            }

            if (string.IsNullOrEmpty(typeId) || !Catalog.TryGetValue(typeId, out var entry))
            {
                ModLogger.Surfaced("COMPANION", "unknown_companion_type", null,
                    LogCtx.Of("typeId", typeId));
                return null;
            }

            if (owningClan == null)
            {
                ModLogger.Surfaced("COMPANION", "spawn_no_clan", null, LogCtx.Of("typeId", typeId));
                return null;
            }

            try
            {
                var culture = bornSettlement?.Culture ?? owningClan.Culture;
                if (culture == null)
                {
                    ModLogger.Expected("COMPANION", "spawn_no_culture",
                        "culture missing for spawn", LogCtx.Of("typeId", typeId));
                    return null;
                }

                var rolledArchetype = PickRandom(entry.Archetypes);
                if (rolledArchetype == null)
                {
                    ModLogger.Surfaced("COMPANION", "no_archetype", null, LogCtx.Of("typeId", typeId));
                    return null;
                }
                archetypeId = rolledArchetype.Id;

                var rolledOption = entry.TroopTemplate?.Roll != null
                    ? PickRandom(entry.TroopTemplate.Roll.Options)
                    : null;

                var template = SelectTroopTemplate(entry.TroopTemplate, rolledOption, culture);
                if (template == null)
                {
                    ModLogger.Surfaced("COMPANION", "spawn_no_template", null,
                        LogCtx.Of("typeId", typeId, "culture", culture.StringId));
                    return null;
                }

                var settlement = bornSettlement
                    ?? owningClan.HomeSettlement
                    ?? Settlement.All.FirstOrDefault(s => s.Culture == culture && s.IsTown);

                var ageMin = entry.AgeMin;
                var ageMax = Math.Max(entry.AgeMin, entry.AgeMax);
                var hero = HeroCreator.CreateSpecialHero(
                    template,
                    settlement,
                    owningClan,
                    null,
                    MBRandom.RandomInt(ageMin, ageMax + 1));

                if (hero == null)
                {
                    ModLogger.Expected("COMPANION", "create_special_hero_null",
                        "HeroCreator.CreateSpecialHero returned null", LogCtx.Of("typeId", typeId));
                    return null;
                }

                hero.SetNewOccupation(Occupation.Soldier);
                hero.HiddenInEncyclopedia = true;
                hero.IsKnownToPlayer = true;

                ApplySkills(hero, entry.BaseSkills);
                if (rolledOption?.SkillOverride != null)
                {
                    ApplySkills(hero, rolledOption.SkillOverride);
                }
                ApplyAttributes(hero, entry.BaseAttributes);
                ApplyTraits(hero, entry.BaseTraits);
                ApplyTraits(hero, rolledArchetype.ExtraTraits);
                ApplyName(hero, rolledArchetype);

                AddCompanionAction.Apply(owningClan, hero);

                var mainParty = MobileParty.MainParty;
                if (mainParty?.MemberRoster != null)
                {
                    mainParty.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                }
                else
                {
                    ModLogger.Surfaced("COMPANION", "main_party_missing", null,
                        LogCtx.Of("typeId", typeId));
                }

                ModLogger.Info(LogCategory,
                    $"Spawned {entry.Id} '{hero.Name}' archetype={rolledArchetype.Id} clan={owningClan.StringId}");
                return hero;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION", "spawn_failed", ex, LogCtx.Of("typeId", typeId));
                return null;
            }
        }

        // ---------- Catalog parsing ----------

        private static CompanionTypeEntry ParseCompanionType(JObject obj)
        {
            try
            {
                var id = obj.Value<string>("id");
                if (string.IsNullOrEmpty(id))
                {
                    ModLogger.Surfaced("COMPANION", "companion_type_missing_id", null);
                    return null;
                }

                var entry = new CompanionTypeEntry
                {
                    Id = id,
                    DisplayName = obj.Value<string>("displayName") ?? id,
                    Lifecycle = obj.Value<string>("lifecycle") ?? "per_player",
                    UnlockTier = obj.Value<int?>("unlockTier") ?? 1,
                    CivilianStyle = obj.Value<string>("civilianStyle") ?? "soldier"
                };

                var ageRange = obj.Value<JArray>("ageRange");
                if (ageRange != null && ageRange.Count >= 2)
                {
                    entry.AgeMin = ageRange[0].Value<int>();
                    entry.AgeMax = ageRange[1].Value<int>();
                }
                else
                {
                    entry.AgeMin = 30;
                    entry.AgeMax = 50;
                }

                entry.BaseSkills = ParseIntDict(obj.Value<JObject>("baseSkills"));
                entry.BaseAttributes = ParseIntDict(obj.Value<JObject>("baseAttributes"));
                entry.BaseTraits = ParseIntDict(obj.Value<JObject>("baseTraits"));
                entry.TroopTemplate = ParseTroopTemplate(obj.Value<JObject>("troopTemplate"));

                var archetypes = obj.Value<JArray>("archetypes");
                if (archetypes == null || archetypes.Count == 0)
                {
                    ModLogger.Surfaced("COMPANION", "companion_type_no_archetypes", null,
                        LogCtx.Of("id", id));
                    return null;
                }

                entry.Archetypes = new List<ArchetypeEntry>();
                foreach (var aObj in archetypes.OfType<JObject>())
                {
                    var archetype = ParseArchetype(aObj);
                    if (archetype != null)
                    {
                        entry.Archetypes.Add(archetype);
                    }
                }

                if (entry.Archetypes.Count == 0)
                {
                    ModLogger.Surfaced("COMPANION", "companion_type_no_valid_archetypes",
                        null, LogCtx.Of("id", id));
                    return null;
                }

                return entry;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION", "parse_companion_type_failed", ex);
                return null;
            }
        }

        private static ArchetypeEntry ParseArchetype(JObject obj)
        {
            var id = obj.Value<string>("id");
            if (string.IsNullOrEmpty(id))
            {
                ModLogger.Surfaced("COMPANION", "archetype_missing_id", null);
                return null;
            }

            var namePool = obj.Value<JArray>("namePool")?
                .Select(t => t.Value<string>())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList() ?? new List<string>();

            return new ArchetypeEntry
            {
                Id = id,
                ExtraTraits = ParseIntDict(obj.Value<JObject>("extraTraits")),
                NamePool = namePool,
                DialogCatalogPrefix = obj.Value<string>("dialogCatalogPrefix") ?? string.Empty
            };
        }

        private static TroopTemplateSpec ParseTroopTemplate(JObject obj)
        {
            if (obj == null) return null;

            var spec = new TroopTemplateSpec
            {
                TierMin = obj.Value<int?>("tierMin") ?? 1,
                TierMax = obj.Value<int?>("tierMax") ?? 6,
                FormationClass = ParseFormationClass(obj.Value<string>("formationClass"))
            };

            var rollObj = obj.Value<JObject>("roll");
            if (rollObj != null)
            {
                var optionsArray = rollObj.Value<JArray>("options");
                if (optionsArray != null && optionsArray.Count > 0)
                {
                    var options = new List<RollOption>();
                    foreach (var optObj in optionsArray.OfType<JObject>())
                    {
                        options.Add(new RollOption
                        {
                            SkillOverride = ParseIntDict(optObj.Value<JObject>("skillOverride")),
                            FormationClass = ParseFormationClass(optObj.Value<string>("formationClass"))
                        });
                    }
                    if (options.Count > 0)
                    {
                        spec.Roll = new RollSpec { Options = options };
                    }
                }
            }

            return spec;
        }

        private static Dictionary<string, int> ParseIntDict(JObject obj)
        {
            var dict = new Dictionary<string, int>(StringComparer.Ordinal);
            if (obj == null) return dict;
            foreach (var prop in obj.Properties())
            {
                if (prop.Value.Type == JTokenType.Integer)
                {
                    dict[prop.Name] = prop.Value.Value<int>();
                }
            }
            return dict;
        }

        private static FormationClass? ParseFormationClass(string raw)
        {
            if (string.IsNullOrEmpty(raw) || string.Equals(raw, "Any", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (Enum.TryParse<FormationClass>(raw, ignoreCase: true, out var fc))
            {
                return fc;
            }
            ModLogger.Expected("COMPANION", "unknown_formation_class",
                "unknown formationClass; treating as Any", LogCtx.Of("raw", raw));
            return null;
        }

        // ---------- Spawn helpers ----------

        private static CharacterObject SelectTroopTemplate(
            TroopTemplateSpec spec,
            RollOption rolledOption,
            CultureObject culture)
        {
            if (spec == null) return culture?.BasicTroop;

            var formationFilter = rolledOption?.FormationClass ?? spec.FormationClass;
            var tierMin = spec.TierMin;
            var tierMax = Math.Max(tierMin, spec.TierMax);

            var primary = CharacterObject.All
                .Where(t => t != null
                    && !t.IsHero
                    && t.Culture == culture
                    && t.Tier >= tierMin && t.Tier <= tierMax
                    && t.Occupation == Occupation.Soldier
                    && (!formationFilter.HasValue || t.DefaultFormationClass == formationFilter.Value))
                .ToList();
            if (primary.Count > 0) return primary.GetRandomElement();

            var dropFormation = CharacterObject.All
                .Where(t => t != null
                    && !t.IsHero
                    && t.Culture == culture
                    && t.Tier >= tierMin && t.Tier <= tierMax
                    && t.Occupation == Occupation.Soldier)
                .ToList();
            if (dropFormation.Count > 0)
            {
                ModLogger.Expected("COMPANION", "template_fallback_formation",
                    "no troop matched formation; falling back to tier", LogCtx.Of("culture", culture.StringId));
                return dropFormation.GetRandomElement();
            }

            var dropTier = CharacterObject.All
                .Where(t => t != null
                    && !t.IsHero
                    && t.Culture == culture
                    && t.Occupation == Occupation.Soldier)
                .ToList();
            if (dropTier.Count > 0)
            {
                ModLogger.Expected("COMPANION", "template_fallback_tier",
                    "no troop matched tier; falling back to any soldier", LogCtx.Of("culture", culture.StringId));
                return dropTier.GetRandomElement();
            }

            ModLogger.Expected("COMPANION", "template_fallback_basic",
                "no soldier troop matched; falling back to BasicTroop",
                LogCtx.Of("culture", culture.StringId));
            return culture.BasicTroop;
        }

        private static void ApplySkills(Hero hero, Dictionary<string, int> skills)
        {
            if (hero?.HeroDeveloper == null || skills == null) return;
            foreach (var pair in skills)
            {
                var skill = ResolveSkill(pair.Key);
                if (skill == null) continue;
                hero.HeroDeveloper.SetInitialSkillLevel(skill, pair.Value);
            }
        }

        private static void ApplyAttributes(Hero hero, Dictionary<string, int> attrs)
        {
            if (hero?.HeroDeveloper == null || attrs == null) return;
            foreach (var pair in attrs)
            {
                var attr = ResolveAttribute(pair.Key);
                if (attr == null) continue;
                hero.HeroDeveloper.AddAttribute(attr, pair.Value, checkUnspentPoints: false);
            }
        }

        private static void ApplyTraits(Hero hero, Dictionary<string, int> traits)
        {
            if (hero == null || traits == null) return;
            foreach (var pair in traits)
            {
                var trait = ResolveTrait(pair.Key);
                if (trait == null) continue;
                hero.SetTraitLevel(trait, pair.Value);
            }
        }

        private static void ApplyName(Hero hero, ArchetypeEntry archetype)
        {
            if (hero == null || archetype == null) return;
            if (archetype.NamePool == null || archetype.NamePool.Count == 0) return;
            var picked = archetype.NamePool[MBRandom.RandomInt(archetype.NamePool.Count)];
            if (string.IsNullOrEmpty(picked)) return;
            var nameObj = new TextObject(picked);
            hero.SetName(nameObj, nameObj);
        }

        // ---------- Reference resolution ----------

        private static SkillObject ResolveSkill(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            switch (id)
            {
                case "OneHanded": return DefaultSkills.OneHanded;
                case "TwoHanded": return DefaultSkills.TwoHanded;
                case "Polearm": return DefaultSkills.Polearm;
                case "Bow": return DefaultSkills.Bow;
                case "Crossbow": return DefaultSkills.Crossbow;
                case "Throwing": return DefaultSkills.Throwing;
                case "Riding": return DefaultSkills.Riding;
                case "Athletics": return DefaultSkills.Athletics;
                case "Crafting": return DefaultSkills.Crafting;
                case "Smithing": return DefaultSkills.Crafting;
                case "Tactics": return DefaultSkills.Tactics;
                case "Scouting": return DefaultSkills.Scouting;
                case "Roguery": return DefaultSkills.Roguery;
                case "Charm": return DefaultSkills.Charm;
                case "Leadership": return DefaultSkills.Leadership;
                case "Trade": return DefaultSkills.Trade;
                case "Steward": return DefaultSkills.Steward;
                case "Medicine": return DefaultSkills.Medicine;
                case "Engineering": return DefaultSkills.Engineering;
                default:
                    ModLogger.Expected("COMPANION", "unknown_skill_key",
                        "unknown skill key in catalog", LogCtx.Of("id", id));
                    return null;
            }
        }

        private static CharacterAttribute ResolveAttribute(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            switch (id)
            {
                case "Vigor": return DefaultCharacterAttributes.Vigor;
                case "Control": return DefaultCharacterAttributes.Control;
                case "Endurance": return DefaultCharacterAttributes.Endurance;
                case "Cunning": return DefaultCharacterAttributes.Cunning;
                case "Social": return DefaultCharacterAttributes.Social;
                case "Intelligence": return DefaultCharacterAttributes.Intelligence;
                default:
                    ModLogger.Expected("COMPANION", "unknown_attribute_key",
                        "unknown attribute key in catalog", LogCtx.Of("id", id));
                    return null;
            }
        }

        private static TraitObject ResolveTrait(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            switch (id)
            {
                case "Valor": return DefaultTraits.Valor;
                case "Mercy": return DefaultTraits.Mercy;
                case "Honor": return DefaultTraits.Honor;
                case "Calculating": return DefaultTraits.Calculating;
                case "Generosity": return DefaultTraits.Generosity;
                default:
                    ModLogger.Expected("COMPANION", "unknown_trait_key",
                        "unknown trait key in catalog", LogCtx.Of("id", id));
                    return null;
            }
        }

        private static T PickRandom<T>(IList<T> list) where T : class
        {
            if (list == null || list.Count == 0) return null;
            return list[MBRandom.RandomInt(list.Count)];
        }

        // ---------- POCO catalog entries ----------

        private sealed class CompanionTypeEntry
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public string Lifecycle { get; set; }
            public int UnlockTier { get; set; }
            public int AgeMin { get; set; }
            public int AgeMax { get; set; }
            public Dictionary<string, int> BaseSkills { get; set; }
            public Dictionary<string, int> BaseAttributes { get; set; }
            public Dictionary<string, int> BaseTraits { get; set; }
            public TroopTemplateSpec TroopTemplate { get; set; }
            public string CivilianStyle { get; set; }
            public List<ArchetypeEntry> Archetypes { get; set; }
        }

        private sealed class ArchetypeEntry
        {
            public string Id { get; set; }
            public Dictionary<string, int> ExtraTraits { get; set; }
            public List<string> NamePool { get; set; }
            public string DialogCatalogPrefix { get; set; }
        }

        private sealed class TroopTemplateSpec
        {
            public int TierMin { get; set; }
            public int TierMax { get; set; }
            public FormationClass? FormationClass { get; set; }
            public RollSpec Roll { get; set; }
        }

        private sealed class RollSpec
        {
            public List<RollOption> Options { get; set; }
        }

        private sealed class RollOption
        {
            public Dictionary<string, int> SkillOverride { get; set; }
            public FormationClass? FormationClass { get; set; }
        }
    }
}
