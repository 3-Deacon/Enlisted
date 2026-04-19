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
                ModLogger.Caught("CONTENT", "StoryDirector.EmitCandidate failed", ex);
            }
        }

        private void Route(StoryCandidate c, StoryTier tier)
        {
            int today = (int)CampaignTime.Now.ToDays;

            if (tier == StoryTier.Modal && c.InteractiveEvent != null)
            {
                if (!ModalFloorsAllow(c, today))
                {
                    _deferredInteractive.Add(MakePersistent(c, tier, today));
                    return;
                }

                DownshiftSpeedIfNeeded();
                EventDeliveryManager.Instance?.QueueEvent(c.InteractiveEvent);
                _lastModalDay = today;
                _lastModalUtcTicks = DateTime.UtcNow.Ticks;
                _categoryCooldowns[c.CategoryId ?? "_none"] = today;
                return;
            }

            WriteDispatchItem(c, tier);
        }

        private bool ModalFloorsAllow(StoryCandidate c, int today)
        {
            double wallSeconds = (_lastModalUtcTicks == 0)
                ? double.MaxValue
                : (DateTime.UtcNow.Ticks - _lastModalUtcTicks) / (double)TimeSpan.TicksPerSecond;

            bool inGameFloor = today - _lastModalDay >= DensitySettings.ModalFloorInGameDays;
            bool wallClockFloor = _lastModalUtcTicks == 0 || wallSeconds >= DensitySettings.ModalFloorWallClockSeconds;
            bool categoryOk = !_categoryCooldowns.TryGetValue(c.CategoryId ?? "_none", out int lastDay)
                || (today - lastDay) >= DensitySettings.CategoryCooldownDays;

            return inGameFloor && wallClockFloor && categoryOk;
        }

        private static void DownshiftSpeedIfNeeded()
        {
            if (!DensitySettings.SpeedDownshiftOnModal) { return; }
            if (Campaign.Current == null) { return; }
            if (Campaign.Current.TimeControlMode == CampaignTimeControlMode.StoppableFastForward)
            {
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
            }
        }

        private static StoryCandidatePersistent MakePersistent(StoryCandidate c, StoryTier tier, int today)
        {
            return new StoryCandidatePersistent
            {
                SourceId = c.SourceId,
                CategoryId = c.CategoryId,
                GrantedTier = tier,
                Severity = c.SeverityHint,
                EmittedDayNumber = today,
                EmittedHour = CampaignTime.Now.GetHourOfDay,
                InteractiveEventId = c.InteractiveEvent?.Id,
                RenderedTitle = c.RenderedTitle,
                RenderedBody = c.RenderedBody,
                StoryKey = c.StoryKey
            };
        }

        private static void WriteDispatchItem(StoryCandidate c, StoryTier tier)
        {
            var news = Campaign.Current?.GetCampaignBehavior<Enlisted.Features.Interface.Behaviors.EnlistedNewsBehavior>();
            if (news == null) { return; }

            int severity = c.SeverityLevel;
            if (severity == 0)
            {
                severity = tier switch
                {
                    StoryTier.Headline => 2,
                    StoryTier.Pertinent => 1,
                    _ => 0
                };
            }

            news.AddPersonalDispatch(
                category: c.DispatchCategory ?? "general",
                headlineKey: c.RenderedTitle,
                placeholderValues: null,
                storyKey: c.StoryKey,
                severity: severity,
                minDisplayDays: c.MinDisplayDays);
        }
    }
}
