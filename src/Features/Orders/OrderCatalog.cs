using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Identity;
using Enlisted.Features.Orders.Models;
using Enlisted.Features.Ranks;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;

namespace Enlisted.Features.Orders
{
    /// <summary>
    /// Catalog of military orders available based on rank, role, and campaign context.
    /// Loads orders from ModuleData/Enlisted/Orders/*.json files.
    /// </summary>
    public static class OrderCatalog
    {
        private const string LogCategory = "OrderCatalog";
        private static readonly List<Order> _orders = [];
        private static bool _initialized;

        /// <summary>
        /// Initializes the order catalog by loading orders from JSON files.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _orders.Clear();

            var ordersPath = GetOrdersBasePath();
            if (string.IsNullOrEmpty(ordersPath) || !Directory.Exists(ordersPath))
            {
                ModLogger.Surfaced("ORDERCATALOG", "Orders directory not found - no orders will be available", null);
                _initialized = true;
                return;
            }

            var jsonFiles = Directory.GetFiles(ordersPath, "*.json", SearchOption.TopDirectoryOnly);
            var ordersLoaded = 0;

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var count = LoadOrdersFromFile(filePath);
                    ordersLoaded += count;
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("OrderCatalog", $"Failed to load orders from {Path.GetFileName(filePath)}", ex);
                }
            }

            _initialized = true;
            ModLogger.Info(LogCategory, $"Loaded {ordersLoaded} orders from {jsonFiles.Length} files");
        }

        /// <summary>
        /// Selects an appropriate order based on player rank, role, and campaign context.
        /// </summary>
        public static Order SelectOrder()
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (_orders.Count == 0)
            {
                ModLogger.Warn(LogCategory, "No orders available in catalog");
                return null;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return null;
            }

            var tier = enlistment.EnlistmentTier;
            var context = GetCampaignContext();
            var role = EnlistedStatusManager.Instance?.GetPrimaryRole() ?? "Soldier";

            // Filter by tier eligibility
            var eligibleOrders = _orders
                .Where(o => tier >= o.Requirements.TierMin && tier <= o.Requirements.TierMax)
                .ToList();

            if (eligibleOrders.Count == 0)
            {
                ModLogger.Debug(LogCategory, $"No orders for tier {tier}");
                return null;
            }

            // Filter by skill/trait requirements
            eligibleOrders = eligibleOrders
                .Where(o => MeetsSkillRequirements(o) && MeetsTraitRequirements(o))
                .ToList();

            if (eligibleOrders.Count == 0)
            {
                ModLogger.Debug(LogCategory, $"No orders matching skill/trait requirements for tier {tier}");
                return null;
            }

            // Filter by sea state - exclude land-only orders when at sea
            var party = enlistment.CurrentLord?.PartyBelongedTo;
            // BUGFIX: If party is in a settlement or besieging, they are on land regardless of IsCurrentlyAtSea
            var isAtSea = party != null &&
                          party.CurrentSettlement == null &&
                          party.BesiegedSettlement == null &&
                          party.IsCurrentlyAtSea;

            if (isAtSea)
            {
                eligibleOrders = eligibleOrders
                    .Where(o => !o.Requirements.NotAtSea)
                    .ToList();

                if (eligibleOrders.Count == 0)
                {
                    ModLogger.Debug(LogCategory, "No orders available at sea (all require land)");
                    return null;
                }
            }

            // Prefer orders matching context + role
            var contextOrders = eligibleOrders
                .Where(o => o.Tags.Any(t => t.Equals(context, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var roleOrders = eligibleOrders
                .Where(o => o.Tags.Any(t => t.Equals(role, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var roleContextOrders = contextOrders
                .Where(o => o.Tags.Any(t => t.Equals(role, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Priority: Role + Context > Context > Role > Any Eligible
            if (roleContextOrders.Count > 0)
            {
                return roleContextOrders.GetRandomElement();
            }

            if (contextOrders.Count > 0)
            {
                return contextOrders.GetRandomElement();
            }

            if (roleOrders.Count > 0)
            {
                var order = roleOrders.GetRandomElement();
                return ApplyContextVariant(order);
            }

            var selected = eligibleOrders.GetRandomElement();
            return ApplyContextVariant(selected);
        }

        /// <summary>
        /// Returns the order unchanged. Context-variant display (sea/land titles) is handled
        /// dynamically by GetDisplayTitle() and GetDisplayDescription() at render time.
        /// 
        /// Previously this method mutated order.Title/Description at selection time, which
        /// caused a bug: if an order was selected at sea, the title was permanently changed
        /// to the sea variant (e.g., "Deck Muster") even after returning to land. Now the
        /// Order object retains its base title, and variants are applied only at display time.
        /// </summary>
        private static Order ApplyContextVariant(Order order)
        {
            // No mutation - GetDisplayTitle()/GetDisplayDescription() handle variants dynamically
            return order;
        }

        /// <summary>
        /// Gets the context-appropriate display title for an order based on current travel context.
        /// Returns sea variant title if at sea and available, otherwise returns the base title.
        /// This should be used when displaying orders to ensure correct context is shown even if
        /// the party's travel state changed after the order was issued.
        /// </summary>
        public static string GetDisplayTitle(Order order)
        {
            if (order == null)
            {
                return string.Empty;
            }

            // If no context variants exist, return base title
            if (order.ContextVariants == null || order.ContextVariants.Count == 0)
            {
                return order.Title ?? string.Empty;
            }

            // Get current travel context key
            var contextKey = GetCurrentTravelContextKey(order);

            // Check if we have a variant for current context
            if (order.ContextVariants.TryGetValue(contextKey, out var variant) &&
                !string.IsNullOrEmpty(variant.Title))
            {
                return variant.Title;
            }

            // Fall back to base title
            return order.Title ?? string.Empty;
        }

        /// <summary>
        /// Gets the context-appropriate display description for an order based on current travel context.
        /// Returns sea variant description if at sea and available, otherwise returns the base description.
        /// </summary>
        public static string GetDisplayDescription(Order order)
        {
            if (order == null)
            {
                return string.Empty;
            }

            // If no context variants exist, return base description
            if (order.ContextVariants == null || order.ContextVariants.Count == 0)
            {
                return order.Description ?? string.Empty;
            }

            // Get current travel context key
            var contextKey = GetCurrentTravelContextKey(order);

            // Check if we have a variant for current context
            if (order.ContextVariants.TryGetValue(contextKey, out var variant) &&
                !string.IsNullOrEmpty(variant.Description))
            {
                return variant.Description;
            }

            // Fall back to base description
            return order.Description ?? string.Empty;
        }

        /// <summary>
        /// Gets the current travel context key ("sea" or "land") for order text variant selection.
        /// Directly checks the party's IsCurrentlyAtSea property for real-time accuracy.
        /// </summary>
        private static string GetCurrentTravelContextKey(Order order)
        {
            // Directly check party's current sea travel status for real-time accuracy
            var enlistment = EnlistmentBehavior.Instance;
            var party = enlistment?.CurrentLord?.PartyBelongedTo;

            if (party == null || !order.ContextVariants.ContainsKey("sea"))
            {
                return "land";
            }

            // BUGFIX: If party is in a settlement or besieging, they cannot be at sea
            // This prevents "Rigging Check" from appearing when on land
            if (party.CurrentSettlement != null || party.BesiegedSettlement != null)
            {
                ModLogger.Debug(LogCategory,
                    $"Party in settlement or siege - forcing land context (IsCurrentlyAtSea={party.IsCurrentlyAtSea})");
                return "land";
            }

            // Check IsCurrentlyAtSea (Warsails DLC property)
            if (party.IsCurrentlyAtSea)
            {
                ModLogger.Debug(LogCategory, $"Party at sea - using sea variant for order {order.Id}");
                return "sea";
            }

            return "land";
        }

        /// <summary>
        /// Determines who issues the order based on player rank and order type.
        /// Typically issuer is 2-3 tiers above player.
        /// </summary>
        public static string DetermineOrderIssuer(int playerTier, Order order)
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = enlistment?.CurrentLord;
            var culture = lord?.Culture?.StringId ?? "empire";

            int issuerTier;
            if (playerTier <= 2)
            {
                issuerTier = 4; // NCO
            }
            else if (playerTier <= 4)
            {
                issuerTier = 6; // Officer
            }
            else if (playerTier <= 6)
            {
                issuerTier = 8; // Commander
            }
            else
            {
                issuerTier = 9; // Lord-level
            }

            // Strategic orders from high ranks come directly from lord
            if (playerTier >= 7 && lord != null && order.Tags.Any(t => t.Equals("strategic", StringComparison.OrdinalIgnoreCase)))
            {
                return lord.Name?.ToString() ?? "Your Lord";
            }

            // Get culture-specific rank title for issuer
            var issuerRankTitle = RankHelper.GetRankTitle(issuerTier, culture);
            return issuerRankTitle;
        }

        /// <summary>
        /// Gets the current campaign context for order selection.
        /// </summary>
        private static string GetCampaignContext()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return "Peace";
            }

            var lord = enlistment.CurrentLord;
            if (lord?.PartyBelongedTo == null)
            {
                return "Peace";
            }

            var party = lord.PartyBelongedTo;

            if (party.BesiegerCamp != null || party.SiegeEvent != null)
            {
                return "Siege";
            }

            if (party.MapEvent != null)
            {
                return "Battle";
            }

            // Check for war state - check if at war with any faction
            if (lord.MapFaction != null)
            {
                foreach (var kingdom in Kingdom.All)
                {
                    if (kingdom != lord.MapFaction && FactionManager.IsAtWarAgainstFaction(lord.MapFaction, kingdom))
                    {
                        return "War";
                    }
                }
            }

            if (party.CurrentSettlement != null)
            {
                return "Town";
            }

            return "Peace";
        }

        /// <summary>
        /// Checks if the player meets the skill requirements for an order.
        /// </summary>
        private static bool MeetsSkillRequirements(Order order)
        {
            if (order.Requirements?.MinSkills == null || order.Requirements.MinSkills.Count == 0)
            {
                return true;
            }

            var hero = Hero.MainHero;
            foreach (var skillReq in order.Requirements.MinSkills)
            {
                var skill = GetSkillByName(skillReq.Key);
                if (skill == null)
                {
                    continue;
                }

                if (hero.GetSkillValue(skill) < skillReq.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the player meets the trait requirements for an order.
        /// </summary>
        private static bool MeetsTraitRequirements(Order order)
        {
            if (order.Requirements?.MinTraits == null || order.Requirements.MinTraits.Count == 0)
            {
                return true;
            }

            var hero = Hero.MainHero;
            foreach (var traitReq in order.Requirements.MinTraits)
            {
                var trait = GetTraitByName(traitReq.Key);
                if (trait == null)
                {
                    continue;
                }

                if (hero.GetTraitLevel(trait) < traitReq.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static SkillObject GetSkillByName(string skillName)
        {
            return Skills.All.FirstOrDefault(s =>
                s.StringId.Equals(skillName, StringComparison.OrdinalIgnoreCase) ||
                s.Name.ToString().Equals(skillName, StringComparison.OrdinalIgnoreCase));
        }

        private static TraitObject GetTraitByName(string traitName)
        {
            // Check common traits by string ID match
            if (traitName.Equals("Surgery", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultTraits.Surgery;
            }
            if (traitName.Equals("ScoutSkills", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultTraits.ScoutSkills;
            }
            if (traitName.Equals("RogueSkills", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultTraits.RogueSkills;
            }
            if (traitName.Equals("Siegecraft", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultTraits.Siegecraft;
            }
            if (traitName.Equals("Commander", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultTraits.Commander;
            }
            if (traitName.Equals("SergeantCommandSkills", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultTraits.SergeantCommandSkills;
            }

            // Try personality traits
            foreach (var trait in DefaultTraits.Personality)
            {
                if (trait.StringId.Equals(traitName, StringComparison.OrdinalIgnoreCase))
                {
                    return trait;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the base path for order JSON files.
        /// Uses ModulePaths utility for correct resolution with both manual and Workshop installs.
        /// </summary>
        private static string GetOrdersBasePath()
        {
            return ModulePaths.GetContentPath("Orders");
        }

        /// <summary>
        /// Loads orders from a single JSON file.
        /// Returns the number of orders loaded.
        /// </summary>
        private static int LoadOrdersFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var ordersArray = JArray.Parse(json);

            var ordersLoaded = 0;

            foreach (var orderToken in ordersArray)
            {
                try
                {
                    var order = ParseOrder(orderToken as JObject);
                    if (order != null && !string.IsNullOrEmpty(order.Id))
                    {
                        _orders.Add(order);
                        ordersLoaded++;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("OrderCatalog", $"Failed to parse order in {Path.GetFileName(filePath)}", ex);
                }
            }

            return ordersLoaded;
        }

        /// <summary>
        /// Parses a single order from JSON.
        /// </summary>
        private static Order ParseOrder(JObject orderJson)
        {
            if (orderJson == null)
            {
                return null;
            }

            var order = new Order
            {
                Id = orderJson["id"]?.ToString() ?? string.Empty,
                Title = orderJson["title"]?.ToString() ?? string.Empty,
                Description = orderJson["description"]?.ToString() ?? string.Empty,
                Issuer = orderJson["issuer"]?.ToString() ?? "auto",
                Tags = ParseStringList(orderJson["tags"]),
                Mandatory = orderJson["mandatory"]?.Value<bool>() ?? false,
                Requirements = ParseRequirements(orderJson["requirements"] as JObject),
                Consequences = ParseConsequences(orderJson["consequences"] as JObject),
                ContextVariants = ParseContextVariants(orderJson["context_variants"] as JObject)
            };

            return order;
        }

        /// <summary>
        /// Parses requirement fields from JSON.
        /// </summary>
        private static OrderRequirement ParseRequirements(JObject reqJson)
        {
            var req = new OrderRequirement();

            if (reqJson == null)
            {
                return req;
            }

            req.TierMin = reqJson["tier_min"]?.Value<int>() ?? 1;
            req.TierMax = reqJson["tier_max"]?.Value<int>() ?? 9;

            // Parse min skills
            var minSkillsJson = reqJson["min_skills"] as JObject;
            if (minSkillsJson != null)
            {
                foreach (var prop in minSkillsJson.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value > 0)
                    {
                        req.MinSkills[prop.Name] = value;
                    }
                }
            }

            // Parse min traits
            var minTraitsJson = reqJson["min_traits"] as JObject;
            if (minTraitsJson != null)
            {
                foreach (var prop in minTraitsJson.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value > 0)
                    {
                        req.MinTraits[prop.Name] = value;
                    }
                }
            }

            // Parse sea restriction (orders that can only be issued on land)
            req.NotAtSea = reqJson["not_at_sea"]?.Value<bool>() ?? false;

            return req;
        }

        /// <summary>
        /// Parses consequence fields from JSON.
        /// </summary>
        private static OrderConsequence ParseConsequences(JObject consJson)
        {
            var cons = new OrderConsequence
            {
                Success = ParseOutcome(consJson?["success"] as JObject),
                Failure = ParseOutcome(consJson?["failure"] as JObject),
                CriticalFailure = ParseOutcome(consJson?["critical_failure"] as JObject),
                Decline = ParseOutcome(consJson?["decline"] as JObject)
            };

            return cons;
        }

        /// <summary>
        /// Parses a single outcome (success/failure/decline) from JSON.
        /// </summary>
        private static OrderOutcome ParseOutcome(JObject outcomeJson)
        {
            if (outcomeJson == null)
            {
                return null;
            }

            var outcome = new OrderOutcome
            {
                Text = outcomeJson["text"]?.ToString() ?? string.Empty,
                Denars = outcomeJson["denars"]?.Value<int>(),
                Renown = outcomeJson["renown"]?.Value<int>(),
                HpLoss = outcomeJson["hp_loss"]?.Value<int>(),
                InjuryType = outcomeJson["injury_type"]?.ToString(),
                MedicalRisk = outcomeJson["medical_risk"]?.Value<int>()
            };

            // Parse reputation changes
            var repJson = outcomeJson["reputation"] as JObject;
            if (repJson != null)
            {
                foreach (var prop in repJson.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value != 0)
                    {
                        outcome.Reputation[prop.Name] = value;
                    }
                }
            }

            // Parse company needs changes
            var needsJson = outcomeJson["company_needs"] as JObject;
            if (needsJson != null)
            {
                foreach (var prop in needsJson.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value != 0)
                    {
                        outcome.CompanyNeeds[prop.Name] = value;
                    }
                }
            }

            // Parse skill XP
            var skillXpJson = outcomeJson["skill_xp"] as JObject;
            if (skillXpJson != null)
            {
                foreach (var prop in skillXpJson.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value != 0)
                    {
                        outcome.SkillXp[prop.Name] = value;
                    }
                }
            }

            // Parse trait XP
            var traitXpJson = outcomeJson["trait_xp"] as JObject;
            if (traitXpJson != null)
            {
                foreach (var prop in traitXpJson.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value != 0)
                    {
                        outcome.TraitXp[prop.Name] = value;
                    }
                }
            }

            // Parse escalation changes
            var escalationJson = outcomeJson["escalation"] as JObject;
            if (escalationJson != null)
            {
                foreach (var prop in escalationJson.Properties())
                {
                    var value = prop.Value.Value<int?>() ?? 0;
                    if (value != 0)
                    {
                        outcome.Escalation[prop.Name] = value;
                    }
                }
            }

            // Parse troop loss
            var troopLossJson = outcomeJson["troop_loss"] as JObject;
            if (troopLossJson != null)
            {
                outcome.TroopLossMin = troopLossJson["min"]?.Value<int>();
                outcome.TroopLossMax = troopLossJson["max"]?.Value<int>();
            }

            return outcome;
        }

        /// <summary>
        /// Parses context variants from JSON (sea/land title and description overrides).
        /// </summary>
        private static Dictionary<string, OrderTextVariant> ParseContextVariants(JObject variantsJson)
        {
            var variants = new Dictionary<string, OrderTextVariant>();

            if (variantsJson == null)
            {
                return variants;
            }

            // Parse each context key (e.g., "sea", "land")
            foreach (var contextKey in variantsJson.Properties())
            {
                var variantData = contextKey.Value as JObject;
                if (variantData == null)
                {
                    continue;
                }

                var variant = new OrderTextVariant
                {
                    Title = variantData["title"]?.ToString() ?? string.Empty,
                    TitleId = variantData["title_id"]?.ToString(),
                    Description = variantData["description"]?.ToString() ?? string.Empty,
                    DescriptionId = variantData["description_id"]?.ToString()
                };

                variants[contextKey.Name] = variant;
            }

            return variants;
        }

        /// <summary>
        /// Parses a string array from JSON token.
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
        /// Clears the catalog and resets initialization state. Used for testing.
        /// </summary>
        public static void Reset()
        {
            _orders.Clear();
            _initialized = false;
        }
    }
}
