using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Activities;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Builds an EventDefinition (modal delivery unit) from a Storylet so that
    /// player-choice storylets can render as Modal events with buttons, effects, and chains.
    /// Spec 0 Storylet.ToCandidate only populates title/body — this adapter fills
    /// StoryCandidate.InteractiveEvent so StoryDirector.Route takes the Modal path
    /// (StoryDirector.cs:218 checks InteractiveEvent != null).
    ///
    /// Effect bridging: Spec 0 choices use List&lt;EffectDecl&gt;; the old EventDeliveryManager
    /// delivery layer uses EventEffects. Effects are stored in _pendingEffects keyed by
    /// (syntheticEventId, choiceId). Call DrainPendingEffects from EventDeliveryManager
    /// after ApplyEffects (Option.Effects will be empty for storylet-sourced events).
    /// </summary>
    public static class StoryletEventAdapter
    {
        private const string LogCategory = "STORYLET-ADAPT";

        // Out-of-band registry: (eventId, choiceId) -> (effects, ctx, chain, owner)
        // Populated in BuildModal; drained by EventDeliveryManager.OnOptionSelected via
        // DrainPendingEffects once it calls us.
        private static readonly Dictionary<string, PendingEffectEntry> _pendingEffects =
            new Dictionary<string, PendingEffectEntry>(StringComparer.Ordinal);

        /// <summary>
        /// Pending effect entry stored per (eventId + ":" + choiceId) key.
        /// </summary>
        private sealed class PendingEffectEntry
        {
            public List<EffectDecl> Effects { get; set; }
            public StoryletContext Ctx { get; set; }
            public ChainDecl Chain { get; set; }
            public Activity Owner { get; set; }
        }

        /// <summary>
        /// Builds a synthetic EventDefinition from a Storylet. The returned value is placed
        /// in StoryCandidate.InteractiveEvent so StoryDirector.Route queues it via
        /// EventDeliveryManager. Spec 0 effects are registered in _pendingEffects keyed by
        /// (evt.Id, choice.Id); call DrainPendingEffects from EventDeliveryManager after
        /// the player selects an option to execute them.
        /// Returns null if s is null.
        /// </summary>
        public static EventDefinition BuildModal(Storylet s, StoryletContext ctx, Activity owner)
        {
            if (s == null)
            {
                return null;
            }

            var eligibleChoices = (s.Options ?? new List<Choice>())
                .Where(c => c != null && TriggerRegistry.Evaluate(c.Trigger, ctx))
                .ToList();

            // Fallback: if no choice passes option-level trigger, offer a single continue.
            if (eligibleChoices.Count == 0)
            {
                eligibleChoices.Add(new Choice
                {
                    Id = "continue",
                    Text = "{=home_continue}Continue.",
                    Tooltip = string.Empty
                });
            }

            // Synthetic ID — unique per storylet invocation so the pending registry key is stable.
            var syntheticId = "storylet_" + s.Id;

            var options = eligibleChoices
                .Select(c => BuildOption(syntheticId, c))
                .ToList();

            var evt = new EventDefinition
            {
                Id = syntheticId,
                TitleFallback = s.Title ?? string.Empty,
                SetupFallback = s.Setup ?? string.Empty,
                Category = "storylet",
                Options = options
            };

            // Register Spec 0 effects in the pending store so they can be drained after
            // the player picks an option (EventDeliveryManager.OnOptionSelected hook).
            RegisterPendingEffects(syntheticId, eligibleChoices, ctx, owner);

            return evt;
        }

        /// <summary>
        /// Called by EventDeliveryManager.OnOptionSelected (via a hook to be added in Task 10
        /// or a follow-up) after the player picks an option from a storylet-sourced event.
        /// Applies Spec 0 effects (EffectDecl list) and parks any chain continuation.
        /// Safe to call with an eventId that was not sourced from this adapter (no-op).
        /// </summary>
        public static void DrainPendingEffects(string eventId, string optionId)
        {
            if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(optionId))
            {
                return;
            }

            var key = MakeKey(eventId, optionId);
            if (!_pendingEffects.TryGetValue(key, out var entry))
            {
                return; // Not a storylet-sourced event; no-op.
            }

            _pendingEffects.Remove(key);

            try
            {
                EffectExecutor.Apply(entry.Effects, entry.Ctx);
                ParkChainIfAny(entry.Chain, entry.Owner, entry.Ctx);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced(LogCategory, "DrainPendingEffects failed", ex);
            }
        }

        /// <summary>
        /// Removes all pending entries for the given event ID. Called if the event is
        /// cancelled or dismissed without a selection.
        /// </summary>
        public static void DiscardPendingEffects(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return;
            }

            var keysToRemove = _pendingEffects.Keys
                .Where(k => k.StartsWith(eventId + ":", StringComparison.Ordinal))
                .ToList();

            foreach (var k in keysToRemove)
            {
                _pendingEffects.Remove(k);
            }
        }

        // ---------- Helpers ----------

        private static EventOption BuildOption(string syntheticEventId, Choice c)
        {
            return new EventOption
            {
                Id = c.Id ?? string.Empty,
                TextFallback = c.Text ?? string.Empty,
                Tooltip = c.Tooltip ?? string.Empty,
                // Effects intentionally empty — Spec 0 EffectDecl list is applied via
                // DrainPendingEffects after the player selects (EventDeliveryManager hook).
                Effects = new EventEffects()
            };
        }

        private static void RegisterPendingEffects(
            string syntheticId,
            List<Choice> choices,
            StoryletContext ctx,
            Activity owner)
        {
            foreach (var c in choices)
            {
                var key = MakeKey(syntheticId, c.Id ?? string.Empty);
                _pendingEffects[key] = new PendingEffectEntry
                {
                    Effects = c.Effects ?? new List<EffectDecl>(),
                    Ctx = ctx,
                    Chain = c.Chain,
                    Owner = owner
                };
            }
        }

        private static void ParkChainIfAny(ChainDecl chain, Activity owner, StoryletContext ctx)
        {
            if (chain == null || string.IsNullOrEmpty(chain.Id))
            {
                return;
            }

            var (typeId, phaseId) = ParseWhen(
                chain.When,
                fallbackTypeId: owner?.TypeId ?? ctx?.ActivityTypeId ?? string.Empty,
                fallbackPhaseId: owner?.CurrentPhase?.Id ?? ctx?.PhaseId ?? string.Empty);

            if (string.IsNullOrEmpty(typeId) || string.IsNullOrEmpty(phaseId))
            {
                ModLogger.Expected(LogCategory, "chain_no_target",
                    "chain.when absent and owner had no type/phase; chain dropped");
                return;
            }

            ActivityRuntime.Instance?.ParkChain(typeId, phaseId, chain.Id);
        }

        private static (string typeId, string phaseId) ParseWhen(
            string when,
            string fallbackTypeId,
            string fallbackPhaseId)
        {
            if (string.IsNullOrEmpty(when))
            {
                return (fallbackTypeId, fallbackPhaseId);
            }

            var parts = when.Split('.');
            return parts.Length == 2 ? (parts[0], parts[1]) : (fallbackTypeId, fallbackPhaseId);
        }

        private static string MakeKey(string eventId, string choiceId)
        {
            return eventId + ":" + choiceId;
        }
    }
}
