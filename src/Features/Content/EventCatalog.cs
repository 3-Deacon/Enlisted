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
    /// Catalog of narrative events loaded from JSON files in ModuleData/Enlisted/Events/ and ModuleData/Enlisted/Decisions/.
    /// Handles both simplified and verbose JSON schemas with automatic field mapping.
    /// </summary>
    public static class EventCatalog
    {
        private const string LogCategory = "EventCatalog";
        private static readonly Dictionary<string, EventDefinition> EventsById = new();
        private static readonly List<EventDefinition> AllEvents = [];
        private static bool _initialized;

        /// <summary>
        /// Total number of events loaded.
        /// </summary>
        public static int EventCount => AllEvents.Count;

        /// <summary>
        /// Initializes the event catalog by loading all JSON files from the Events and Decisions directories.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            EventsById.Clear();
            AllEvents.Clear();

            var filesLoaded = 0;
            var eventsLoaded = 0;
            var migrationWarnings = 0;

            // Load from Events directory
            var eventsPath = GetEventsBasePath();
            if (!string.IsNullOrEmpty(eventsPath) && Directory.Exists(eventsPath))
            {
                var (eventsFiles, eventsCount, eventsWarnings) = LoadFromDirectory(eventsPath, "Events");
                filesLoaded += eventsFiles;
                eventsLoaded += eventsCount;
                migrationWarnings += eventsWarnings;
            }
            else
            {
                ModLogger.Warn(LogCategory, $"Events directory not found: {eventsPath}");
            }

            // Load from Decisions directory
            var decisionsPath = GetDecisionsBasePath();
            if (!string.IsNullOrEmpty(decisionsPath) && Directory.Exists(decisionsPath))
            {
                var (decisionsFiles, decisionsCount, decisionsWarnings) = LoadFromDirectory(decisionsPath, "Decisions");
                filesLoaded += decisionsFiles;
                eventsLoaded += decisionsCount;
                migrationWarnings += decisionsWarnings;
            }
            else
            {
                ModLogger.Warn(LogCategory, $"Decisions directory not found: {decisionsPath}");
            }

            // Load from Order Events directory (for Order Progression System)
            var orderEventsPath = GetOrderEventsBasePath();
            if (!string.IsNullOrEmpty(orderEventsPath) && Directory.Exists(orderEventsPath))
            {
                var (orderFiles, orderCount, orderWarnings) = LoadFromDirectory(orderEventsPath, "OrderEvents");
                filesLoaded += orderFiles;
                eventsLoaded += orderCount;
                migrationWarnings += orderWarnings;
            }
            else
            {
                ModLogger.Warn(LogCategory, $"Order events directory not found: {orderEventsPath} - order phase events will not fire");
            }

            _initialized = true;

            var warningMsg = migrationWarnings > 0 ? $" ({migrationWarnings} migration warnings)" : "";
            ModLogger.Info(LogCategory, $"Loaded {eventsLoaded} events from {filesLoaded} files{warningMsg}");
        }

        /// <summary>
        /// Loads all JSON files from a directory and returns statistics.
        /// </summary>
        private static (int filesLoaded, int eventsLoaded, int migrationWarnings) LoadFromDirectory(string directoryPath, string directoryName)
        {
            var filesLoaded = 0;
            var eventsLoaded = 0;
            var migrationWarnings = 0;

            var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var (eventCount, warnings) = LoadEventsFromFile(filePath);
                    if (eventCount > 0)
                    {
                        filesLoaded++;
                        eventsLoaded += eventCount;
                        migrationWarnings += warnings;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error(LogCategory, $"Failed to load events from {directoryName}/{Path.GetFileName(filePath)}", ex);
                }
            }

            return (filesLoaded, eventsLoaded, migrationWarnings);
        }

        /// <summary>
        /// Gets an event by its unique ID.
        /// </summary>
        public static EventDefinition GetEvent(string eventId)
        {
            if (!_initialized)
            {
                Initialize();
            }

            EventsById.TryGetValue(eventId, out var eventDef);
            return eventDef;
        }

        /// <summary>
        /// Gets an event by its unique ID. Alias for GetEvent for clarity in proving event context.
        /// </summary>
        public static EventDefinition GetEventById(string eventId)
        {
            return GetEvent(eventId);
        }

        /// <summary>
        /// Gets all loaded events.
        /// </summary>
        public static IReadOnlyList<EventDefinition> GetAllEvents()
        {
            if (!_initialized)
            {
                Initialize();
            }

            return AllEvents;
        }

        /// <summary>
        /// Gets events matching the specified category.
        /// </summary>
        public static IEnumerable<EventDefinition> GetEventsByCategory(string category)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return AllEvents.Where(e =>
                e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets events matching the specified role requirement.
        /// </summary>
        public static IEnumerable<EventDefinition> GetEventsByRole(string role)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return AllEvents.Where(e =>
                e.Requirements.Role.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
                e.Requirements.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets events matching the specified context.
        /// Used by MapIncidentManager to filter events by map context (leaving_battle, entering_town, etc.).
        /// Returns events where context matches exactly or context is "Any".
        /// </summary>
        public static IEnumerable<EventDefinition> GetEventsForContext(string context)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return AllEvents.Where(e =>
                e.Requirements.Context.Equals(context, StringComparison.OrdinalIgnoreCase) ||
                e.Requirements.Context.Equals("Any", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets events matching the specified order type.
        /// Used by OrderProgressionBehavior to get events for a specific order (e.g., "order_guard_post").
        /// </summary>
        public static IEnumerable<EventDefinition> GetEventsByOrderType(string orderType)
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (string.IsNullOrEmpty(orderType))
            {
                return Enumerable.Empty<EventDefinition>();
            }

            return AllEvents.Where(e =>
                !string.IsNullOrEmpty(e.OrderType) &&
                e.OrderType.Equals(orderType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the base path for event JSON files.
        /// Uses ModulePaths utility for correct resolution with both manual and Workshop installs.
        /// </summary>
        private static string GetEventsBasePath() => ModulePaths.GetContentPath("Events");

        /// <summary>
        /// Gets the base path for decision JSON files.
        /// Uses ModulePaths utility for correct resolution with both manual and Workshop installs.
        /// </summary>
        private static string GetDecisionsBasePath() => ModulePaths.GetContentPath("Decisions");

        /// <summary>
        /// Gets the base path for order event JSON files.
        /// Uses ModulePaths utility for correct resolution with both manual and Workshop installs.
        /// </summary>
        private static string GetOrderEventsBasePath() => Path.Combine(ModulePaths.GetContentPath("Orders"), "order_events");

        /// <summary>
        /// Loads events from a single JSON file.
        /// Returns tuple of (eventsLoaded, migrationWarnings).
        /// </summary>
        private static (int eventsLoaded, int migrationWarnings) LoadEventsFromFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);

            // Skip schema version files
            if (fileName.Equals("schema_version.json", StringComparison.OrdinalIgnoreCase))
            {
                return (0, 0);
            }

            var json = File.ReadAllText(filePath);
            var root = JObject.Parse(json);

            // Check for events array (both new and old formats use this)
            var eventsArray = root["events"] as JArray;
            if (eventsArray == null || eventsArray.Count == 0)
            {
                ModLogger.Debug(LogCategory, $"No events array in {fileName}");
                return (0, 0);
            }

            var eventsLoaded = 0;
            var migrationWarnings = 0;

            foreach (var eventToken in eventsArray)
            {
                try
                {
                    var (eventDef, warnings) = ParseEvent(eventToken as JObject, fileName);
                    if (eventDef != null && !string.IsNullOrEmpty(eventDef.Id))
                    {
                        if (EventsById.ContainsKey(eventDef.Id))
                        {
                            ModLogger.Warn(LogCategory, $"Duplicate event ID '{eventDef.Id}' in {fileName}, skipping");
                            continue;
                        }

                        EventsById[eventDef.Id] = eventDef;
                        AllEvents.Add(eventDef);
                        eventsLoaded++;
                        migrationWarnings += warnings;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error(LogCategory, $"Failed to parse event in {fileName}", ex);
                }
            }

            return (eventsLoaded, migrationWarnings);
        }

        /// <summary>
        /// Parses a single event from JSON, handling both simplified and verbose schema formats.
        /// Returns tuple of (EventDefinition, fieldMappingWarningCount).
        /// </summary>
        private static (EventDefinition eventDef, int warnings) ParseEvent(JObject eventJson, string sourceFile)
        {
            if (eventJson == null)
            {
                return (null, 0);
            }

            var warnings = 0;
            var eventDef = new EventDefinition();

            // Core fields (same in both schemas)
            eventDef.Id = eventJson["id"]?.ToString() ?? string.Empty;
            eventDef.Category = eventJson["category"]?.ToString() ?? "general";
            eventDef.OrderType = eventJson["order_type"]?.ToString() ?? string.Empty;
            eventDef.Severity = eventJson["severity"]?.ToString() ?? "normal";

            // Parse title/setup IDs - check both new and old locations
            ParseTitleAndSetup(eventJson, eventDef);

            // Parse requirements with migration support
            warnings += ParseRequirements(eventJson, eventDef, sourceFile);

            // Parse timing
            ParseTiming(eventJson, eventDef);

            // Parse triggers
            ParseTriggers(eventJson, eventDef);

            // Parse options with migration support
            warnings += ParseOptions(eventJson, eventDef, sourceFile);

            return (eventDef, warnings);
        }

        /// <summary>
        /// Parses title and setup IDs from the event JSON.
        /// Checks both root-level and content-nested locations.
        /// Also extracts inline fallback text for when XML localization fails.
        /// </summary>
        private static void ParseTitleAndSetup(JObject eventJson, EventDefinition eventDef)
        {
            // Check root level first for IDs
            eventDef.TitleId = eventJson["titleId"]?.ToString() ?? string.Empty;
            eventDef.SetupId = eventJson["setupId"]?.ToString() ?? string.Empty;

            // Check root level for inline fallback text
            eventDef.TitleFallback = eventJson["title"]?.ToString() ?? string.Empty;
            eventDef.SetupFallback = eventJson["setup"]?.ToString() ?? string.Empty;

            // Also check nested content object
            var content = eventJson["content"] as JObject;
            if (content != null)
            {
                if (string.IsNullOrEmpty(eventDef.TitleId))
                {
                    eventDef.TitleId = content["titleId"]?.ToString() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(eventDef.SetupId))
                {
                    eventDef.SetupId = content["setupId"]?.ToString() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(eventDef.TitleFallback))
                {
                    eventDef.TitleFallback = content["title"]?.ToString() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(eventDef.SetupFallback))
                {
                    eventDef.SetupFallback = content["setup"]?.ToString() ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// Parses event requirements from JSON.
        /// Handles alternate field names (formation→role, tier.min→minTier, etc.).
        /// </summary>
        private static int ParseRequirements(JObject eventJson, EventDefinition eventDef, string sourceFile)
        {
            var warnings = 0;
            var reqs = new EventRequirements();

            var reqJson = eventJson["requirements"] as JObject;
            if (reqJson == null)
            {
                eventDef.Requirements = reqs;
                return 0;
            }

            // Parse tier - supports both minTier/maxTier and tier.min/tier.max formats
            var tierObj = reqJson["tier"] as JObject;
            if (tierObj != null)
            {
                reqs.MinTier = tierObj["min"]?.Value<int>();
                reqs.MaxTier = tierObj["max"]?.Value<int>();
            }
            else
            {
                reqs.MinTier = reqJson["minTier"]?.Value<int>();
                reqs.MaxTier = reqJson["maxTier"]?.Value<int>();
            }

            // Parse context
            reqs.Context = reqJson["context"]?.ToString() ?? "Any";

            // Parse role - migrate from "formation" if present
            reqs.Role = reqJson["role"]?.ToString();
            if (string.IsNullOrEmpty(reqs.Role))
            {
                var formation = reqJson["formation"]?.ToString();
                if (!string.IsNullOrEmpty(formation) &&
                    !formation.Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    reqs.Role = MigrateFormationToRole(formation);
                    ModLogger.Debug(LogCategory, $"[{sourceFile}] Migrated formation '{formation}' → role '{reqs.Role}'");
                    warnings++;
                }
            }

            reqs.Role ??= "Any";

            // Handle deprecated "duty" field
            var duty = reqJson["duty"]?.ToString();
            if (!string.IsNullOrEmpty(duty) && !duty.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                ModLogger.Debug(LogCategory, $"[{sourceFile}] Ignoring deprecated 'duty' field: {duty}");
                warnings++;
            }

            // Parse skill requirements
            ParseDictionaryField(reqJson, "minSkills", reqs.MinSkills);

            // Parse trait requirements
            ParseDictionaryField(reqJson, "minTraits", reqs.MinTraits);

            // Parse HP requirement (for decisions like Seek Treatment that require being wounded)
            reqs.HpBelow = reqJson["hp_below"]?.Value<int>() ?? reqJson["hpBelow"]?.Value<int>();

            // Parse baggage items requirement (for theft events that require items to exist)
            reqs.BaggageHasItems = reqJson["baggageHasItems"]?.Value<bool>() ?? reqJson["baggage_has_items"]?.Value<bool>();

            // Parse sea/land context requirements
            reqs.NotAtSea = reqJson["notAtSea"]?.Value<bool>() ?? reqJson["not_at_sea"]?.Value<bool>();
            reqs.AtSea = reqJson["atSea"]?.Value<bool>() ?? reqJson["at_sea"]?.Value<bool>();

            // Parse medical condition requirements
            reqs.HasAnyCondition = reqJson["hasAnyCondition"]?.Value<bool>() ?? reqJson["has_any_condition"]?.Value<bool>();
            reqs.HasSevereCondition = reqJson["hasSevereCondition"]?.Value<bool>() ?? reqJson["has_severe_condition"]?.Value<bool>();
            reqs.MaxIllness = reqJson["maxIllness"]?.ToString() ?? reqJson["max_illness"]?.ToString();

            // Parse world state requirements for order events
            // These are detailed context keys like "peacetime_garrison", "war_active_campaign", "siege_defending"
            var worldStateArray = reqJson["world_state"] as JArray ?? reqJson["worldState"] as JArray;
            if (worldStateArray != null)
            {
                foreach (var state in worldStateArray)
                {
                    var stateStr = state.Value<string>();
                    if (!string.IsNullOrEmpty(stateStr))
                    {
                        reqs.WorldState.Add(stateStr);
                    }
                }
            }

            // Parse escalation requirements (check both locations)
            var escalationJson = reqJson["minEscalation"] as JObject;
            if (escalationJson == null)
            {
                // Also check triggers.escalation_requirements location
                var triggers = eventJson["triggers"] as JObject;
                escalationJson = triggers?["escalation_requirements"] as JObject;
            }

            if (escalationJson != null)
            {
                foreach (var prop in escalationJson.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value > 0)
                    {
                        reqs.MinEscalation[prop.Name] = value;
                    }
                }
            }

            eventDef.Requirements = reqs;
            return warnings;
        }

        /// <summary>
        /// Parses timing constraints from JSON.
        /// </summary>
        private static void ParseTiming(JObject eventJson, EventDefinition eventDef)
        {
            var timing = new EventTiming();

            var timingJson = eventJson["timing"] as JObject;
            if (timingJson != null)
            {
                timing.CooldownDays = timingJson["cooldown_days"]?.Value<int>() ??
                                     timingJson["cooldownDays"]?.Value<int>() ?? 7;
                timing.Priority = timingJson["priority"]?.ToString() ?? "normal";
                timing.OneTime = timingJson["one_time"]?.Value<bool>() ??
                                timingJson["oneTime"]?.Value<bool>() ?? false;
            }

            eventDef.Timing = timing;
        }

        /// <summary>
        /// Parses trigger conditions from JSON.
        /// </summary>
        private static void ParseTriggers(JObject eventJson, EventDefinition eventDef)
        {
            var triggersJson = eventJson["triggers"] as JObject;
            if (triggersJson == null)
            {
                return;
            }

            // Parse "all" triggers (must all be true)
            eventDef.TriggersAll = ParseStringList(triggersJson["all"]);

            // Parse "any" triggers (at least one must be true)
            eventDef.TriggersAny = ParseStringList(triggersJson["any"]);

            // Parse "none" triggers (must all be false)
            eventDef.TriggersNone = ParseStringList(triggersJson["none"]);
        }

        /// <summary>
        /// Parses event options from JSON.
        /// Options can be at root level or nested inside a content object.
        /// </summary>
        private static int ParseOptions(JObject eventJson, EventDefinition eventDef, string sourceFile)
        {
            var warnings = 0;

            // Options can be at root level or inside content object
            var optionsArray = eventJson["options"] as JArray;
            if (optionsArray == null)
            {
                var content = eventJson["content"] as JObject;
                optionsArray = content?["options"] as JArray;
            }

            if (optionsArray == null)
            {
                return 0;
            }

            foreach (var optToken in optionsArray)
            {
                var optJson = optToken as JObject;
                if (optJson == null)
                {
                    continue;
                }

                var option = new EventOption
                {
                    Id = optJson["id"]?.ToString() ?? string.Empty,
                    TextId = optJson["textId"]?.ToString() ?? string.Empty,
                    TextFallback = optJson["text"]?.ToString() ?? string.Empty,
                    ResultTextId = optJson["resultTextId"]?.ToString() ?? string.Empty,
                    ResultTextFallback = optJson["resultText"]?.ToString() ?? string.Empty,
                    Tooltip = optJson["tooltip"]?.ToString() ?? string.Empty,
                    Risk = optJson["risk"]?.ToString() ?? "safe"
                };

                // Parse option requirements
                var optReqJson = optJson["requirements"] as JObject ?? optJson["condition"] as JObject;
                if (optReqJson != null)
                {
                    option.Requirements = new EventOptionRequirements();
                    ParseDictionaryField(optReqJson, "minSkills", option.Requirements.MinSkills);
                    ParseDictionaryField(optReqJson, "min_skill", option.Requirements.MinSkills);
                    ParseDictionaryField(optReqJson, "minTraits", option.Requirements.MinTraits);
                    ParseDictionaryField(optReqJson, "min_trait", option.Requirements.MinTraits);
                    option.Requirements.MinTier = optReqJson["minTier"]?.Value<int>() ??
                                                  optReqJson["min_tier"]?.Value<int>();
                    option.Requirements.Role = optReqJson["role"]?.ToString() ??
                                               optReqJson["has_role"]?.ToString();
                }

                // Parse effects with migration support
                warnings += ParseOptionEffects(optJson, option, sourceFile);

                // Parse success/failure effects for risky options
                var effectsSuccessJson = optJson["effects_success"] as JObject ?? optJson["effectsSuccess"] as JObject;
                if (effectsSuccessJson != null)
                {
                    option.EffectsSuccess = ParseEffectsObject(effectsSuccessJson);
                }

                var effectsFailureJson = optJson["effects_failure"] as JObject ?? optJson["effectsFailure"] as JObject;
                if (effectsFailureJson != null)
                {
                    option.EffectsFailure = ParseEffectsObject(effectsFailureJson);
                }

                // Parse risk chance for risky options
                option.RiskChance = optJson["risk_chance"]?.Value<int?>() ?? optJson["riskChance"]?.Value<int?>();

                // Parse dynamic skill check fields
                option.SkillCheck = optJson["skillCheck"]?.ToString() ?? optJson["skill_check"]?.ToString();
                option.SkillBase = optJson["skillBase"]?.Value<int>() ?? optJson["skill_base"]?.Value<int>() ?? 50;
                option.TooltipTemplate = optJson["tooltipTemplate"]?.ToString() ?? optJson["tooltip_template"]?.ToString();

                // Parse failure result text
                option.ResultTextFailureId = optJson["resultFailureTextId"]?.ToString() ??
                                             optJson["result_failure_text_id"]?.ToString() ?? string.Empty;
                option.ResultTextFailureFallback = optJson["outcome_failure"]?.ToString() ??
                                                   optJson["resultTextFailure"]?.ToString() ?? string.Empty;

                // Parse flag operations
                option.SetFlags = ParseStringList(optJson["set_flags"]);
                option.ClearFlags = ParseStringList(optJson["clear_flags"]);
                option.FlagDurationDays = optJson["flag_duration_days"]?.Value<int>() ?? 0;

                // Parse chain event fields
                option.ChainsTo = optJson["chains_to"]?.ToString() ?? string.Empty;
                option.ChainDelayHours = optJson["chain_delay_hours"]?.Value<int>() ?? 0;

                // Parse reward choices (sub-choice popup after main option)
                option.RewardChoices = ParseRewardChoices(optJson["reward_choices"]);

                // Parse enlistment abort flag
                option.AbortsEnlistment = optJson["aborts_enlistment"]?.Value<bool>() ??
                                         optJson["abortsEnlistment"]?.Value<bool>() ?? false;

                eventDef.Options.Add(option);
            }

            return warnings;
        }

        /// <summary>
        /// Parses effects for an event option.
        /// Handles alternate field names (trait_xp→traitXp, etc.).
        /// </summary>
        private static int ParseOptionEffects(JObject optJson, EventOption option, string sourceFile)
        {
            var warnings = 0;
            var effects = new EventEffects();

            var effectsJson = optJson["effects"] as JObject;
            var rewardsJson = optJson["rewards"] as JObject;

            // Parse skill XP from effects or rewards.xp
            if (effectsJson != null)
            {
                ParseDictionaryField(effectsJson, "skillXp", effects.SkillXp);
            }

            if (rewardsJson != null)
            {
                var xpObj = rewardsJson["xp"] as JObject;
                if (xpObj != null)
                {
                    foreach (var prop in xpObj.Properties())
                    {
                        var value = prop.Value?.Value<int>() ?? 0;
                        if (value != 0 && !effects.SkillXp.ContainsKey(prop.Name))
                        {
                            effects.SkillXp[prop.Name] = value;
                        }
                    }
                }
            }

            // Parse trait XP, mapping trait_xp.Leadership → traitXp.Commander if needed
            if (effectsJson != null)
            {
                ParseDictionaryField(effectsJson, "traitXp", effects.TraitXp);

                var oldTraitXp = effectsJson["trait_xp"] as JObject;
                if (oldTraitXp != null)
                {
                    foreach (var prop in oldTraitXp.Properties())
                    {
                        var newName = MigrateTraitName(prop.Name);
                        var value = prop.Value?.Value<int>() ?? 0;
                        if (value != 0 && !effects.TraitXp.ContainsKey(newName))
                        {
                            effects.TraitXp[newName] = value;
                            if (!newName.Equals(prop.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                ModLogger.Debug(LogCategory, $"[{sourceFile}] Migrated trait_xp.{prop.Name} → traitXp.{newName}");
                                warnings++;
                            }
                        }
                    }
                }
            }

            // Parse reputation
            if (effectsJson != null)
            {
                effects.LordRep = effectsJson["lordRep"]?.Value<int>() ??
                                  effectsJson["lord_reputation"]?.Value<int>();

                // Parse escalation changes
                effects.Scrutiny = effectsJson["scrutiny"]?.Value<int>();
                effects.MedicalRisk = effectsJson["medicalRisk"]?.Value<int>() ??
                                      effectsJson["medical_risk"]?.Value<int>();

                // Parse resource changes
                effects.Gold = effectsJson["gold"]?.Value<int>();
                effects.HpChange = effectsJson["hpChange"]?.Value<int>() ??
                                   effectsJson["hp_change"]?.Value<int>();
                effects.TroopLoss = effectsJson["troopLoss"]?.Value<int>() ??
                                    effectsJson["troop_loss"]?.Value<int>();
                effects.TroopWounded = effectsJson["troopWounded"]?.Value<int>() ??
                                       effectsJson["troop_wounded"]?.Value<int>();
                effects.FoodLoss = effectsJson["foodLoss"]?.Value<int>() ??
                                   effectsJson["food_loss"]?.Value<int>();
                effects.TroopXp = effectsJson["troopXp"]?.Value<int>() ??
                                  effectsJson["troop_xp"]?.Value<int>();
                effects.ApplyWound = effectsJson["applyWound"]?.ToString() ??
                                     effectsJson["apply_wound"]?.ToString();
                effects.ChainEventId = effectsJson["chainEventId"]?.ToString() ??
                                       effectsJson["triggers_event"]?.ToString();
                effects.Renown = effectsJson["renown"]?.Value<int>();
                effects.TriggersDischarge = effectsJson["triggers_discharge"]?.ToString() ??
                                            effectsJson["triggersDischarge"]?.ToString();
                effects.Promotes = ParsePromotesEffect(effectsJson["promotes"]);
                effects.RetinueGain = effectsJson["retinueGain"]?.Value<int>() ??
                                      effectsJson["retinue_gain"]?.Value<int>();
                effects.RetinueLoyalty = effectsJson["retinueLoyalty"]?.Value<int>() ??
                                         effectsJson["retinue_loyalty"]?.Value<int>();
                effects.RetinueLoss = effectsJson["retinueLoss"]?.Value<int>() ??
                                      effectsJson["retinue_loss"]?.Value<int>();
                effects.RetinueWounded = effectsJson["retinueWounded"]?.Value<int>() ??
                                         effectsJson["retinue_wounded"]?.Value<int>();

                // Parse baggage train effects
                effects.GrantTemporaryBaggageAccess = effectsJson["grantTemporaryBaggageAccess"]?.Value<int>() ??
                                                     effectsJson["grant_temporary_baggage_access"]?.Value<int>();
                effects.BaggageDelayDays = effectsJson["baggageDelayDays"]?.Value<int>() ??
                                          effectsJson["baggage_delay_days"]?.Value<int>();
                effects.RandomBaggageLoss = effectsJson["randomBaggageLoss"]?.Value<int>() ??
                                           effectsJson["random_baggage_loss"]?.Value<int>();

                // Parse bag check choice (first-enlistment gear handling)
                effects.BagCheckChoice = effectsJson["bagCheckChoice"]?.ToString() ??
                                        effectsJson["bag_check_choice"]?.ToString();

                // Parse company needs
                ParseDictionaryField(effectsJson, "companyNeeds", effects.CompanyNeeds);
            }

            // Parse gold from rewards if not in effects
            if (!effects.Gold.HasValue && rewardsJson != null)
            {
                effects.Gold = rewardsJson["gold"]?.Value<int>();
            }

            option.Effects = effects;
            return warnings;
        }

        /// <summary>
        /// Parses the "promotes" effect from JSON.
        /// Handles both boolean (true = promote to next tier) and integer (explicit target tier) values.
        /// Returns null if not specified, or the target tier if promotion should occur.
        /// </summary>
        private static int? ParsePromotesEffect(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            // Handle explicit integer (e.g., "promotes": 3 means promote to T3)
            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>();
            }

            // Handle boolean true (e.g., "promotes": true means promote to next tier)
            // We use -1 as a sentinel value meaning "promote to current tier + 1"
            // The EventDeliveryManager will resolve this at runtime
            if (token.Type == JTokenType.Boolean && token.Value<bool>())
            {
                return -1; // Sentinel: resolve to currentTier + 1 at runtime
            }

            return null;
        }

        /// <summary>
        /// Parses a dictionary field from JSON (handles snake_case and camelCase).
        /// </summary>
        private static void ParseDictionaryField(JObject json, string fieldName, Dictionary<string, int> target)
        {
            var obj = json[fieldName] as JObject;
            if (obj == null)
            {
                return;
            }

            foreach (var prop in obj.Properties())
            {
                var value = prop.Value.Value<int?>() ?? 0;
                if (value != 0)
                {
                    target[prop.Name] = value;
                }
            }
        }

        /// <summary>
        /// Parses a string array from JSON (e.g., set_flags, clear_flags).
        /// </summary>
        private static List<string> ParseStringList(JToken token)
        {
            if (token == null)
            {
                return [];
            }

            if (token is JArray array)
            {
                return array.Select(t => t.Value<string>()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            }

            return [];
        }

        /// <summary>
        /// Migrates old formation names to new role names.
        /// </summary>
        private static string MigrateFormationToRole(string formation)
        {
            return formation?.ToLowerInvariant() switch
            {
                "infantry" => "Soldier",
                "cavalry" => "Soldier",
                "ranged" => "Soldier",
                "skirmisher" => "Scout",
                "heavy_infantry" => "Soldier",
                "light_cavalry" => "Scout",
                "horse_archer" => "Scout",
                _ => "Any"
            };
        }

        /// <summary>
        /// Migrates old trait names to current native trait names.
        /// </summary>
        private static string MigrateTraitName(string oldName)
        {
            return oldName?.ToLowerInvariant() switch
            {
                "leadership" => "Commander",
                "martialskills" => "SergeantCommandSkills",
                "martial_skills" => "SergeantCommandSkills",
                "command" => "Commander",
                _ => oldName
            };
        }

        /// <summary>
        /// Parses a reward_choices block from JSON.
        /// Contains sub-options for branching rewards after main option selection.
        /// </summary>
        private static RewardChoices ParseRewardChoices(JToken rewardChoicesToken)
        {
            if (rewardChoicesToken == null)
            {
                return null;
            }

            var rc = new RewardChoices
            {
                Type = rewardChoicesToken["type"]?.Value<string>() ?? string.Empty,
                Prompt = rewardChoicesToken["prompt"]?.Value<string>() ?? string.Empty,
                Options = []
            };

            var optionsArray = rewardChoicesToken["options"] as JArray;
            if (optionsArray == null)
            {
                return rc;
            }

            foreach (var optToken in optionsArray)
            {
                var subOption = new RewardChoiceOption
                {
                    Id = optToken["id"]?.Value<string>() ?? string.Empty,
                    Text = optToken["text"]?.Value<string>() ?? string.Empty,
                    Tooltip = optToken["tooltip"]?.Value<string>() ?? string.Empty,
                    Condition = optToken["condition"]?.Value<string>(),
                    Rewards = ParseRewards(optToken["rewards"]),
                    Effects = ParseSubChoiceEffects(optToken["effects"]),
                    Costs = ParseCosts(optToken["costs"])
                };
                rc.Options.Add(subOption);
            }

            return rc;
        }

        /// <summary>
        /// Parses a rewards block from JSON.
        /// Contains gold, xp, and skill xp rewards.
        /// </summary>
        private static EventRewards ParseRewards(JToken rewardsToken)
        {
            if (rewardsToken == null)
            {
                return null;
            }

            var rewards = new EventRewards
            {
                Gold = rewardsToken["gold"]?.Value<int>()
            };

            // Parse general XP (e.g., {"enlisted": 20})
            var xpObj = rewardsToken["xp"] as JObject;
            if (xpObj != null)
            {
                foreach (var prop in xpObj.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value != 0)
                    {
                        rewards.Xp[prop.Name] = value;
                    }
                }
            }

            // Parse skill XP (e.g., {"OneHanded": 40})
            var skillXpObj = rewardsToken["skillXp"] as JObject ?? rewardsToken["skill_xp"] as JObject;
            if (skillXpObj != null)
            {
                foreach (var prop in skillXpObj.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value != 0)
                    {
                        rewards.SkillXp[prop.Name] = value;
                    }
                }
            }

            // Parse dynamic skill XP (e.g., {"equipped_weapon": 15, "weakest_combat": 12})
            var dynamicSkillXpObj = rewardsToken["dynamicSkillXp"] as JObject ?? rewardsToken["dynamic_skill_xp"] as JObject;
            if (dynamicSkillXpObj != null)
            {
                foreach (var prop in dynamicSkillXpObj.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value != 0)
                    {
                        rewards.DynamicSkillXp[prop.Name] = value;
                    }
                }
            }

            return rewards;
        }

        /// <summary>
        /// Parses a costs block from JSON.
        /// Contains gold and time costs.
        /// </summary>
        private static EventCosts ParseCosts(JToken costsToken)
        {
            if (costsToken == null)
            {
                return null;
            }

            return new EventCosts
            {
                Gold = costsToken["gold"]?.Value<int>(),
                TimeHours = costsToken["time_hours"]?.Value<int>() ??
                           costsToken["timeHours"]?.Value<int>()
            };
        }

        /// <summary>
        /// Parses an effects object from JSON into an EventEffects instance.
        /// Handles both snake_case and camelCase field names.
        /// </summary>
        private static EventEffects ParseEffectsObject(JObject effectsJson)
        {
            if (effectsJson == null)
            {
                return null;
            }

            var effects = new EventEffects();

            // Parse skill XP
            ParseDictionaryField(effectsJson, "skillXp", effects.SkillXp);
            ParseDictionaryField(effectsJson, "skill_xp", effects.SkillXp);

            // Parse dynamic skill XP
            ParseDictionaryField(effectsJson, "dynamicSkillXp", effects.DynamicSkillXp);
            ParseDictionaryField(effectsJson, "dynamic_skill_xp", effects.DynamicSkillXp);

            // Parse trait XP
            ParseDictionaryField(effectsJson, "traitXp", effects.TraitXp);
            ParseDictionaryField(effectsJson, "trait_xp", effects.TraitXp);

            // Parse reputation changes
            effects.LordRep = effectsJson["lordRep"]?.Value<int>() ?? effectsJson["lord_reputation"]?.Value<int>();

            // Parse escalation changes
            effects.Scrutiny = effectsJson["scrutiny"]?.Value<int>();
            effects.MedicalRisk = effectsJson["medicalRisk"]?.Value<int>() ?? effectsJson["medical_risk"]?.Value<int>();

            // Parse resource changes
            effects.Gold = effectsJson["gold"]?.Value<int>();
            effects.HpChange = effectsJson["hpChange"]?.Value<int>() ?? effectsJson["hp_change"]?.Value<int>();
            effects.TroopLoss = effectsJson["troopLoss"]?.Value<int>() ?? effectsJson["troop_loss"]?.Value<int>();
            effects.TroopWounded = effectsJson["troopWounded"]?.Value<int>() ?? effectsJson["troop_wounded"]?.Value<int>();
            effects.FoodLoss = effectsJson["foodLoss"]?.Value<int>() ?? effectsJson["food_loss"]?.Value<int>();
            effects.TroopXp = effectsJson["troopXp"]?.Value<int>() ?? effectsJson["troop_xp"]?.Value<int>();
            effects.ApplyWound = effectsJson["applyWound"]?.ToString() ?? effectsJson["apply_wound"]?.ToString();
            effects.ChainEventId = effectsJson["chainEventId"]?.ToString() ?? effectsJson["triggers_event"]?.ToString();
            effects.Renown = effectsJson["renown"]?.Value<int>();
            effects.TriggersDischarge = effectsJson["triggers_discharge"]?.ToString() ?? effectsJson["triggersDischarge"]?.ToString();
            effects.Promotes = ParsePromotesEffect(effectsJson["promotes"]);
            effects.RetinueGain = effectsJson["retinueGain"]?.Value<int>() ?? effectsJson["retinue_gain"]?.Value<int>();
            effects.RetinueLoyalty = effectsJson["retinueLoyalty"]?.Value<int>() ?? effectsJson["retinue_loyalty"]?.Value<int>();
            effects.RetinueLoss = effectsJson["retinueLoss"]?.Value<int>() ?? effectsJson["retinue_loss"]?.Value<int>();
            effects.RetinueWounded = effectsJson["retinueWounded"]?.Value<int>() ?? effectsJson["retinue_wounded"]?.Value<int>();

            // Parse baggage train effects
            effects.GrantTemporaryBaggageAccess = effectsJson["grantTemporaryBaggageAccess"]?.Value<int>() ??
                                                 effectsJson["grant_temporary_baggage_access"]?.Value<int>();
            effects.BaggageDelayDays = effectsJson["baggageDelayDays"]?.Value<int>() ??
                                      effectsJson["baggage_delay_days"]?.Value<int>();
            effects.RandomBaggageLoss = effectsJson["randomBaggageLoss"]?.Value<int>() ??
                                       effectsJson["random_baggage_loss"]?.Value<int>();

            // Parse bag check choice (first-enlistment gear handling)
            effects.BagCheckChoice = effectsJson["bagCheckChoice"]?.ToString() ??
                                    effectsJson["bag_check_choice"]?.ToString();

            // Parse medical effects (illness, injury, treatment, worsening)
            effects.IllnessOnset = effectsJson["illnessOnset"]?.ToString() ?? effectsJson["illness_onset"]?.ToString();
            effects.InjuryOnset = effectsJson["injuryOnset"]?.ToString() ?? effectsJson["injury_onset"]?.ToString();
            effects.BeginTreatment = effectsJson["beginTreatment"]?.Value<bool>() ?? effectsJson["begin_treatment"]?.Value<bool>();
            effects.WorsenCondition = effectsJson["worsenCondition"]?.Value<bool>() ?? effectsJson["worsen_condition"]?.Value<bool>();

            // Parse company needs
            ParseDictionaryField(effectsJson, "companyNeeds", effects.CompanyNeeds);
            ParseDictionaryField(effectsJson, "company_needs", effects.CompanyNeeds);

            return effects;
        }

        /// <summary>
        /// Parses effects for a sub-choice option.
        /// Simplified version that handles the common effect fields.
        /// </summary>
        private static EventEffects ParseSubChoiceEffects(JToken effectsToken)
        {
            if (effectsToken == null)
            {
                return null;
            }

            var effects = new EventEffects();

            // Parse common effects
            effects.LordRep = effectsToken["lordRep"]?.Value<int>() ??
                              effectsToken["lord_reputation"]?.Value<int>();
            effects.Scrutiny = effectsToken["scrutiny"]?.Value<int>();
            effects.MedicalRisk = effectsToken["medicalRisk"]?.Value<int>() ??
                                  effectsToken["medical_risk"]?.Value<int>();
            effects.Gold = effectsToken["gold"]?.Value<int>();
            effects.Renown = effectsToken["renown"]?.Value<int>();
            effects.TriggersDischarge = effectsToken["triggers_discharge"]?.ToString() ??
                                        effectsToken["triggersDischarge"]?.ToString();

            // Parse skill XP
            ParseDictionaryField(effectsToken as JObject, "skillXp", effects.SkillXp);
            ParseDictionaryField(effectsToken as JObject, "skill_xp", effects.SkillXp);

            // Parse trait XP
            ParseDictionaryField(effectsToken as JObject, "traitXp", effects.TraitXp);
            ParseDictionaryField(effectsToken as JObject, "trait_xp", effects.TraitXp);

            // Parse company needs
            ParseDictionaryField(effectsToken as JObject, "companyNeeds", effects.CompanyNeeds);
            ParseDictionaryField(effectsToken as JObject, "company_needs", effects.CompanyNeeds);

            return effects;
        }

        /// <summary>
        /// Clears the catalog and resets initialization state. Used for testing.
        /// </summary>
        public static void Reset()
        {
            EventsById.Clear();
            AllEvents.Clear();
            _initialized = false;
        }
    }
}

