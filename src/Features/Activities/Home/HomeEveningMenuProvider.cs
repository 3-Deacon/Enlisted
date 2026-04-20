using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.Localization;

namespace Enlisted.Features.Activities.Home
{
    /// <summary>
    /// Supplies the five Evening intent options to the preallocated menu slot-bank
    /// in EnlistedMenuBehavior. Slot visibility gated by per-intent pool eligibility.
    /// </summary>
    public static class HomeEveningMenuProvider
    {
        public static readonly string[] Intents = { "unwind", "train_hard", "brood", "commune", "scheme" };

        public static bool IsSlotActive(int slotIndex, out TextObject label, out TextObject tooltip)
        {
            label = TextObject.GetEmpty();
            tooltip = TextObject.GetEmpty();
            if (slotIndex < 0 || slotIndex >= Intents.Length) { return false; }

            var runtime = ActivityRuntime.Instance;
            var active = runtime?.GetActiveChoicePhase();
            if (active == null) { return false; }
            if (!(active.Value.activity is HomeActivity home)) { return false; }
            if (active.Value.phase.Id != "evening") { return false; }

            var intent = Intents[slotIndex];
            var eligible = AnyEligibleForIntent(home, intent);
            label = new TextObject("{=home_evening_" + intent + "_label}" + DefaultLabel(intent));
            tooltip = BuildTooltip(intent, eligible);
            return eligible;
        }

        public static void OnSlotSelected(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Intents.Length) { return; }
            var runtime = ActivityRuntime.Instance;
            var active = runtime?.GetActiveChoicePhase();
            if (active == null) { return; }
            if (!(active.Value.activity is HomeActivity home)) { return; }

            var intent = Intents[slotIndex];
            var ctx = new StoryletContext
            {
                ActivityTypeId = home.TypeId,
                PhaseId = "evening",
                CurrentContext = "settlement"
            };
            home.OnPlayerChoice(intent, ActivityContext.FromCurrent());

            var picked = PickStorylet(home, intent, ctx);
            if (picked == null)
            {
                ModLogger.Surfaced("HOME-EVENING", "No eligible storylet at fire",
                    new System.InvalidOperationException("intent=" + intent));
                runtime.ResolvePlayerChoice(home);
                return;
            }

            EffectExecutor.Apply(picked.Immediate, ctx);
            var evt = StoryletEventAdapter.BuildModal(picked, ctx, home);
            var candidate = picked.ToCandidate(ctx);
            if (candidate != null && evt != null)
            {
                candidate.InteractiveEvent = evt;
                candidate.ProposedTier = StoryTier.Modal;
                candidate.ChainContinuation = true;
                StoryDirector.Instance?.EmitCandidate(candidate);
            }
            runtime.ResolvePlayerChoice(home);
        }

        private static bool AnyEligibleForIntent(HomeActivity home, string intent)
        {
            if (home?.CurrentPhase?.Pool == null) { return false; }
            var ctx = new StoryletContext
            {
                ActivityTypeId = home.TypeId,
                PhaseId = "evening",
                CurrentContext = "settlement"
            };
            var prefix = "home_evening_" + intent + "_";
            foreach (var id in home.CurrentPhase.Pool)
            {
                if (id == null || !id.StartsWith(prefix)) { continue; }
                var s = StoryletCatalog.GetById(id);
                if (s != null && s.IsEligible(ctx)) { return true; }
            }
            return false;
        }

        private static Storylet PickStorylet(HomeActivity home, string intent, StoryletContext ctx)
        {
            var prefix = "home_evening_" + intent + "_";
            var eligible = new List<(Storylet s, float w)>();
            foreach (var id in home.CurrentPhase?.Pool ?? new List<string>())
            {
                if (id == null || !id.StartsWith(prefix)) { continue; }
                var s = StoryletCatalog.GetById(id);
                if (s == null || !s.IsEligible(ctx)) { continue; }
                var w = s.WeightFor(ctx);
                if (w > 0f) { eligible.Add((s, w)); }
            }
            if (eligible.Count == 0) { return null; }
            var total = eligible.Sum(e => e.w);
            var r = (float)(new System.Random().NextDouble() * total);
            var cursor = 0f;
            foreach (var (s, w) in eligible)
            {
                cursor += w;
                if (r <= cursor) { return s; }
            }
            return eligible[eligible.Count - 1].s;
        }

        private static TextObject BuildTooltip(string intent, bool eligible)
        {
            if (!eligible)
            {
                return new TextObject("{=home_evening_nothing}There's nothing here tonight.");
            }
            if (intent == "scheme"
                && EnlistmentBehavior.Instance?.EnlistedLord?.Clan?.IsUnderMercenaryService != true)
            {
                return new TextObject("{=home_scheme_tooltip_vassal}Risky under an honour-bound lord. Scrutiny may rise.");
            }
            return new TextObject("{=home_evening_" + intent + "_tooltip}…");
        }

        private static string DefaultLabel(string intent)
        {
            switch (intent)
            {
                case "unwind": return "Drink with the squad";
                case "train_hard": return "Drill until blisters";
                case "brood": return "Sit alone with your thoughts";
                case "commune": return "Seek out the lord";
                case "scheme": return "Slip away quietly";
                default: return intent;
            }
        }
    }
}
