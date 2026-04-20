using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Resolves scope.slots against world state. Returns a dictionary of slot-name -> Hero
    /// (or null for faction-style roles). Null entries for required slots mean the storylet
    /// is ineligible; callers drop.
    ///
    /// Also provides text flattening — {veteran.name} -> SetTextVariable("VETERAN_NAME", ...)
    /// because TaleWorlds TextObject doesn't parse dotted paths.
    /// </summary>
    public static class SlotFiller
    {
        private static readonly Regex DottedSlotPattern = new Regex(
            @"\{(\w+)\.(\w+)(?::([\w_]+))?\}",
            RegexOptions.Compiled);

        public static Dictionary<string, Hero> Resolve(ScopeDecl scope)
        {
            var resolved = new Dictionary<string, Hero>(StringComparer.OrdinalIgnoreCase);
            if (scope?.Slots == null || scope.Slots.Count == 0)
            {
                return resolved;
            }

            foreach (var kv in scope.Slots)
            {
                var slotName = kv.Key;
                var decl = kv.Value;
                var hero = ResolveOne(decl);
                resolved[slotName] = hero; // null if unresolvable — caller checks
            }

            return resolved;
        }

        public static bool AllRequiredFilled(Dictionary<string, Hero> resolved)
        {
            foreach (var kv in resolved)
            {
                if (kv.Value == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Render a "{=id}Fallback" template with dotted-slot substitution flattened to native SetTextVariable keys.</summary>
        public static string Render(string template, Dictionary<string, Hero> slots)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            var flat = DottedSlotPattern.Replace(template, match =>
            {
                var slot = match.Groups[1].Value;
                var prop = match.Groups[2].Value;
                var key = (slot + "_" + prop).ToUpperInvariant();
                return "{" + key + "}";
            });

            var textObj = new TextObject(flat);
            var effectiveSlots = slots ?? new Dictionary<string, Hero>();

            foreach (var kv in effectiveSlots)
            {
                if (kv.Value == null)
                {
                    continue;
                }

                var upper = kv.Key.ToUpperInvariant();
                _ = textObj.SetTextVariable(upper + "_NAME", kv.Value.Name);
                _ = textObj.SetTextVariable(upper + "_OCCUPATION", kv.Value.Occupation.ToString());
                _ = textObj.SetTextVariable(upper + "_CULTURE", kv.Value.Culture?.Name ?? new TextObject(string.Empty));
            }

            return textObj.ToString();
        }

        private static Hero ResolveOne(SlotDecl decl)
        {
            if (decl == null || string.IsNullOrEmpty(decl.Role))
            {
                return null;
            }

            switch (decl.Role.ToLowerInvariant())
            {
                case "enlisted_lord":
                    return EnlistmentBehavior.Instance?.EnlistedLord;

                case "squadmate":
                    return ResolveSquadmate(decl.Tag);

                case "named_veteran":
                    ModLogger.Expected("SLOT", "named_veteran_not_ready",
                        "named_veteran slot is reserved for a later surface spec that wires the query API.");
                    return null;

                case "chaplain":
                    return ResolveFlaggedHero("chaplain");

                case "quartermaster":
                    return EnlistmentBehavior.Instance?.QuartermasterHero;

                case "enemy_commander":
                    var mapEvent = MobileParty.MainParty?.MapEvent;
                    if (mapEvent == null)
                    {
                        return null;
                    }

                    return mapEvent.AttackerSide?.LeaderParty == MobileParty.MainParty?.Party
                        ? mapEvent.DefenderSide?.LeaderParty?.LeaderHero
                        : mapEvent.AttackerSide?.LeaderParty?.LeaderHero;

                case "settlement_notable":
                    var settlement = MobileParty.MainParty?.CurrentSettlement;
                    return settlement?.Notables?.FirstOrDefault();

                case "kingdom":
                    // Faction-style role; Hero-typed return is inappropriate. SlotFiller returns null
                    // and callers that need a faction read scope.slots meta directly for kingdom slots.
                    return null;

                default:
                    ModLogger.Expected("SLOT", "unknown_role_" + decl.Role,
                        "Unknown slot role: " + decl.Role);
                    return null;
            }
        }

        private static Hero ResolveSquadmate(string requiredTag)
        {
            if (!string.IsNullOrEmpty(requiredTag) && FlagStore.Instance == null)
            {
                return null;
            }

            var candidates = Hero.AllAliveHeroes
                .Where(h => h?.Occupation == Occupation.Soldier)
                .ToList();

            if (!string.IsNullOrEmpty(requiredTag))
            {
                candidates = candidates
                    .Where(h => FlagStore.Instance?.HasForHero(h, requiredTag) == true)
                    .ToList();
            }

            return candidates.FirstOrDefault();
        }

        private static Hero ResolveFlaggedHero(string flag)
        {
            if (FlagStore.Instance == null)
            {
                return null;
            }

            return Hero.AllAliveHeroes
                .Where(h => h?.Occupation == Occupation.Soldier)
                .FirstOrDefault(h => FlagStore.Instance.HasForHero(h, flag));
        }
    }
}
