using Enlisted.Features.Activities;
using Enlisted.Features.Content;
using Enlisted.Features.Endeavors;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.Core.Helpers
{
    /// <summary>
    /// Wraps the canonical Storylet → BuildModal → EmitCandidate pipeline so
    /// ceremony, endeavor-phase, decision-outcome, and patron-favor call sites
    /// don't reinvent the same 10 lines of boilerplate. Plans 3-6 of the CK3
    /// wanderer cluster all route their player-choice modals through these
    /// helpers; the architecture brief at
    /// docs/architecture/ck3-wanderer-architecture-brief.md §6 names this
    /// pipeline as load-bearing.
    ///
    /// Pacing rails (DensitySettings): ChainContinuation = true bypasses the
    /// 5-day in-game floor and 3-day per-category cooldown but still respects
    /// the 60s wall-clock guard. Use for ceremony-after-promotion and
    /// in-progress endeavor phases; never use for ambient triggers.
    /// </summary>
    public static class ModalEventBuilder
    {
        /// <summary>
        /// Looks up the storylet, builds the modal event, and emits it as a
        /// modal candidate through StoryDirector. Owner is null for free-floating
        /// ceremonies and decisions; pass a non-null Activity for endeavor phases
        /// (the adapter routes effect application back through the activity).
        /// </summary>
        public static void FireSimpleModal(string storyletId, StoryletContext ctx, bool chainContinuation)
        {
            FireInternal(storyletId, ctx, chainContinuation, owner: null);
        }

        /// <summary>
        /// Ceremony at a tier transition. ChainContinuation = true so the
        /// ceremony fires immediately after OnTierChanged regardless of the
        /// 5-day modal floor or category cooldown.
        /// </summary>
        public static void FireCeremony(string storyletId, StoryletContext ctx)
        {
            FireInternal(storyletId, ctx, chainContinuation: true, owner: null);
        }

        /// <summary>
        /// In-progress endeavor phase event. ChainContinuation = true so phase
        /// 2 / phase 3 fire on schedule even if other Modal candidates are
        /// pending. Owner activity carries phase / outcome state.
        /// </summary>
        public static void FireEndeavorPhase(string storyletId, StoryletContext ctx, EndeavorActivity owner)
        {
            FireInternal(storyletId, ctx, chainContinuation: true, owner: owner);
        }

        /// <summary>
        /// Outcome modal for a player decision (e.g. patron-favor result).
        /// ChainContinuation = false so the outcome respects normal pacing
        /// rails — outcomes aren't time-critical.
        /// </summary>
        public static void FireDecisionOutcome(string storyletId, StoryletContext ctx)
        {
            FireInternal(storyletId, ctx, chainContinuation: false, owner: null);
        }

        private static void FireInternal(string storyletId, StoryletContext ctx, bool chainContinuation, Activity owner)
        {
            if (string.IsNullOrEmpty(storyletId) || ctx == null)
            {
                ModLogger.Expected("MODAL", "missing_input", "storylet id or context missing for modal fire");
                return;
            }

            var storylet = StoryletCatalog.GetById(storyletId);
            if (storylet == null)
            {
                ModLogger.Expected("MODAL", "storylet_not_found", "storylet missing for modal fire");
                return;
            }

            var evt = StoryletEventAdapter.BuildModal(storylet, ctx, owner);
            if (evt == null)
            {
                ModLogger.Expected("MODAL", "build_modal_returned_null", "BuildModal returned null for storylet");
                return;
            }

            var candidate = storylet.ToCandidate(ctx);
            if (candidate == null)
            {
                ModLogger.Expected("MODAL", "to_candidate_returned_null", "Storylet.ToCandidate returned null");
                return;
            }

            candidate.InteractiveEvent = evt;
            candidate.ProposedTier = StoryTier.Modal;
            candidate.ChainContinuation = chainContinuation;

            StoryDirector.Instance?.EmitCandidate(candidate);
        }
    }
}
