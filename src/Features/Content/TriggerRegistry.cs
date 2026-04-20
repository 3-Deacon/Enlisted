using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Features.Qualities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Named predicate dispatch. Triggers come in three forms:
    ///   - Flat name: "is_enlisted"
    ///   - Name:arg: "flag:veteran_meeting_done"
    ///   - Name:arg:arg: "quality_gte:scrutiny:75"
    /// Unknown trigger ids are treated as false + Expected-logged once.
    /// </summary>
    public static class TriggerRegistry
    {
        private static readonly Dictionary<string, Func<string[], StoryletContext, bool>> _predicates =
            new Dictionary<string, Func<string[], StoryletContext, bool>>(StringComparer.OrdinalIgnoreCase);

        static TriggerRegistry() { RegisterSeedPredicates(); }

        public static void Register(string name, Func<string[], StoryletContext, bool> predicate)
        {
            if (string.IsNullOrEmpty(name) || predicate == null)
            {
                return;
            }

            _predicates[name] = predicate;
        }

        /// <summary>Returns all registered predicate names. Read-only snapshot of the
        /// keys collection — exposed for diagnostics (e.g. debug dump hotkey).</summary>
        public static IEnumerable<string> AllRegisteredNames() => _predicates.Keys;

        public static bool Evaluate(IList<string> triggers, StoryletContext ctx)
        {
            if (triggers == null || triggers.Count == 0)
            {
                return true;
            }

            foreach (var t in triggers)
            {
                if (!EvaluateOne(t, ctx))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool EvaluateOne(string trigger, StoryletContext ctx)
        {
            if (string.IsNullOrEmpty(trigger))
            {
                return true;
            }

            var negate = trigger.StartsWith("not:", StringComparison.OrdinalIgnoreCase);
            var body = negate ? trigger.Substring(4) : trigger;
            var parts = body.Split(':');
            var name = parts[0];
            var args = parts.Length > 1 ? new string[parts.Length - 1] : Array.Empty<string>();
            for (var i = 1; i < parts.Length; i++)
            {
                args[i - 1] = parts[i];
            }

            if (!_predicates.TryGetValue(name, out var predicate))
            {
                ModLogger.Expected("TRIGGER", "unknown_" + name, "Unknown trigger: " + name);
                return false;
            }
            try
            {
                var result = predicate(args, ctx);
                return negate ? !result : result;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TRIGGER", "Trigger predicate threw",
                    ex, new Dictionary<string, object> { { "name", name } });
                return false;
            }
        }

        // ---------- Seed predicates ----------

        private static void RegisterSeedPredicates()
        {
            Register("is_enlisted", (_, _2) => EnlistmentBehavior.Instance?.IsEnlisted == true);

            Register("is_at_sea", (_, _2) => MobileParty.MainParty?.IsCurrentlyAtSea == true);

            Register("in_settlement", (_, _2) => MobileParty.MainParty?.CurrentSettlement != null);

            Register("in_siege", (_, _2) =>
            {
                var p = MobileParty.MainParty;
                if (p == null)
                {
                    return false;
                }

                if (p.BesiegerCamp != null)
                {
                    return true;
                }

                return p.CurrentSettlement?.SiegeEvent != null;
            });

            Register("in_mapevent", (_, _2) => MobileParty.MainParty?.MapEvent != null);

            Register("context", (args, ctx) =>
                args.Length > 0 && string.Equals(ctx?.CurrentContext, args[0], StringComparison.OrdinalIgnoreCase));

            Register("flag", (args, _) =>
                args.Length > 0 && FlagStore.Instance?.Has(args[0]) == true);

            Register("quality_gte", (args, _) =>
            {
                if (args.Length < 2 || QualityStore.Instance == null)
                {
                    return false;
                }

                return int.TryParse(args[1], out var threshold)
                    && QualityStore.Instance.Get(args[0]) >= threshold;
            });

            Register("quality_lte", (args, _) =>
            {
                if (args.Length < 2 || QualityStore.Instance == null)
                {
                    return false;
                }

                return int.TryParse(args[1], out var threshold)
                    && QualityStore.Instance.Get(args[0]) <= threshold;
            });

            Register("rank_gte", (args, _) =>
            {
                if (args.Length < 1)
                {
                    return false;
                }

                return int.TryParse(args[0], out var threshold)
                    && (EnlistmentBehavior.Instance?.EnlistmentTier ?? 0) >= threshold;
            });

            Register("veteran_has_flag", (args, ctx) =>
            {
                if (args.Length < 1)
                {
                    return false;
                }

                if (ctx?.ResolvedSlots == null)
                {
                    return false;
                }

                if (!ctx.ResolvedSlots.TryGetValue("veteran", out var hero) || hero == null)
                {
                    return false;
                }

                return FlagStore.Instance?.HasForHero(hero, args[0]) == true;
            });

            Register("beat", (_, _2) =>
            {
                // Beat triggers are matched upstream by the source that emits the candidate.
                // At the storylet layer, treat as true — the source wouldn't have called us
                // if the beat didn't match. This is a marker, not a runtime check.
                return true;
            });
        }
    }
}
