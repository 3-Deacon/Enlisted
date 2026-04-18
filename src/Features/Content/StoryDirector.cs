using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Central broker for player-facing story content. Sources emit StoryCandidates
    /// via EmitCandidate; the Director filters by relevance, classifies by severity,
    /// and routes to Modal (via EventDeliveryManager) or Observational (via news feed).
    /// Phase 1: registered but Route is a no-op. Phase 2 (Task 12) fills in real routing.
    /// </summary>
    public sealed class StoryDirector : CampaignBehaviorBase
    {
        /// <summary>Singleton instance set on RegisterEvents.</summary>
        public static StoryDirector Instance { get; private set; }

        // Project convention: behavior fields use SyncData only, no [SaveableField].
        // See src/Features/Retinue/Core/CompanionAssignmentManager.cs for precedent.
        private int _lastModalDay;
        private long _lastModalUtcTicks;
        private Dictionary<string, int> _categoryCooldowns = new Dictionary<string, int>();
        private List<StoryCandidatePersistent> _deferredInteractive = new List<StoryCandidatePersistent>();

        /// <summary>Deferred interactive candidates blocked by the pacing floor, retried on DailyTick.</summary>
        public IReadOnlyList<StoryCandidatePersistent> DeferredInteractive => _deferredInteractive;

        /// <summary>Registers the singleton instance. No campaign events subscribed in Phase 1.</summary>
        public override void RegisterEvents()
        {
            Instance = this;
        }

        /// <summary>Persists pacing state across save/load cycles.</summary>
        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("storydir_lastModalDay", ref _lastModalDay);
            dataStore.SyncData("storydir_lastModalUtcTicks", ref _lastModalUtcTicks);
            dataStore.SyncData("storydir_categoryCooldowns", ref _categoryCooldowns);
            dataStore.SyncData("storydir_deferredInteractive", ref _deferredInteractive);
        }

        /// <summary>
        /// Emit a story candidate. Filters by relevance, classifies by severity, routes
        /// through Route. Exceptions are caught and logged as E-PACE-001 -- a failing
        /// emit never crashes the game, candidates drop silently.
        /// </summary>
        public void EmitCandidate(StoryCandidate candidate)
        {
            try
            {
                if (candidate == null || Hero.MainHero == null)
                {
                    return;
                }

                var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (lord == null)
                {
                    return;
                }

                if (!RelevanceFilter.Passes(candidate.Relevance, lord, MobileParty.MainParty))
                {
                    return;
                }

                // Recency check: RelevanceFilter already gated on settlement logic;
                // this is an approximation for the severity bonus -- good enough for Phase 1.
                bool recentlyVisited = candidate.Relevance.SubjectSettlement != null;

                var finalTier = SeverityClassifier.Classify(
                    candidate,
                    candidate.Relevance.TouchesEnlistedLord,
                    candidate.Relevance.TouchesPlayerKingdom,
                    recentlyVisited);

                Route(candidate, finalTier);
            }
            catch (Exception ex)
            {
                ModLogger.ErrorCode("Content", "E-PACE-001", "StoryDirector.EmitCandidate failed", ex);
            }
        }

        // Filled in by Task 12 (routing) -- skeleton no-op for Phase 1.
        private void Route(StoryCandidate c, StoryTier tier)
        {
            // Phase 1: no-op. Phase 2 implements Modal / Observational routing.
        }
    }
}
