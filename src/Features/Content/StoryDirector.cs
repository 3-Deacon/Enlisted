using System;
using System.Collections.Generic;
using System.Linq;
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
    /// and routes accordingly: Modal candidates fire through EventDeliveryManager subject
    /// to pacing floors; floor-blocked interactives are deferred; everything else is
    /// written as a DispatchItem through EnlistedNewsBehavior.AddPersonalDispatch.
    /// The Director also subscribes to DailyTickEvent to surface a quiet-stretch fallback
    /// event after DensitySettings.QuietStretchDays in-game days without a Modal firing.
    /// </summary>
    public sealed class StoryDirector : CampaignBehaviorBase
    {
        /// <summary>Singleton instance set on RegisterEvents.</summary>
        public static StoryDirector Instance { get; private set; }

        private const string NoCategorySentinel = "_none";
        private const string DefaultDispatchCategory = "general";
        private const int DeferredInteractiveCap = 32;
        private const int MaxProbesPerTick = 4;

        // Project convention: behavior fields use SyncData only, no [SaveableField].
        // See src/Features/Retinue/Core/CompanionAssignmentManager.cs for precedent.
        private int _lastModalDay;
        private long _lastModalUtcTicks;
        private Dictionary<string, int> _categoryCooldowns = new Dictionary<string, int>();
        private List<StoryCandidatePersistent> _deferredInteractive = new List<StoryCandidatePersistent>();

        /// <summary>Deferred interactive candidates blocked by the pacing floor, retried on DailyTick.</summary>
        public IReadOnlyList<StoryCandidatePersistent> DeferredInteractive => _deferredInteractive;

        /// <summary>Registers the singleton instance and subscribes to DailyTickEvent for quiet-stretch fallback.</summary>
        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
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
        /// through Route. Exceptions are caught and logged via ModLogger.Caught -- a
        /// failing emit never crashes the game, candidates drop silently.
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

        private void OnDailyTick()
        {
            try
            {
                int today = (int)CampaignTime.Now.ToDays;

                // Goal 1: retry one deferred interactive candidate per day if floors now allow.
                // If the head's InteractiveEventId is no longer in the catalog, drop it and
                // continue to try the next one (up to one re-fire per tick).
                if (TryFlushDeferredInteractive(today))
                {
                    // Re-firing a deferred interactive counts as the day's modal activity —
                    // don't also fire a quiet-stretch fallback on the same tick.
                    return;
                }

                if (today - _lastModalDay < DensitySettings.QuietStretchDays)
                {
                    return;
                }

                var candidates = EventCatalog.GetEventsByCategory("quiet_stretch").ToList();
                if (candidates.Count == 0)
                {
                    return;
                }

                var rng = new Random();
                var evt = candidates[rng.Next(candidates.Count)];

                EmitCandidate(new StoryCandidate
                {
                    SourceId = "director.quiet_stretch",
                    CategoryId = "quiet_stretch",
                    ProposedTier = StoryTier.Modal,
                    SeverityHint = 0.70f,
                    Beats = { StoryBeat.QuietStretchTimeout },
                    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                    EmittedAt = CampaignTime.Now,
                    InteractiveEvent = evt,
                    RenderedTitle = evt.TitleFallback,
                    RenderedBody = evt.SetupFallback
                });
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CONTENT", "Quiet-stretch tick failed", ex);
            }
        }

        /// <summary>
        /// Attempts to flush up to one deferred interactive candidate from _deferredInteractive.
        /// Drops candidates whose event is no longer in the catalog (at most a few per tick
        /// to avoid unbounded work) and tries subsequent candidates; only one successful fire
        /// per tick. Returns true if a candidate was fired.
        /// </summary>
        private bool TryFlushDeferredInteractive(int today)
        {
            // Guard against O(n) scans in pathological cases: give up after a small probe.
            int probes = 0;

            while (_deferredInteractive.Count > 0 && probes < MaxProbesPerTick)
            {
                probes++;
                var next = _deferredInteractive[0];
                EventDefinition evt = null;
                if (!string.IsNullOrEmpty(next.InteractiveEventId))
                {
                    evt = EventCatalog.GetEventById(next.InteractiveEventId);
                }

                if (evt == null)
                {
                    // Event no longer in catalog — drop silently and probe next.
                    _deferredInteractive.RemoveAt(0);
                    continue;
                }

                var pseudo = new StoryCandidate
                {
                    SourceId = next.SourceId,
                    CategoryId = next.CategoryId,
                    InteractiveEvent = evt
                };

                if (!ModalFloorsAllow(pseudo, today))
                {
                    // Head still blocked — don't skip it (preserve FIFO semantics).
                    return false;
                }

                _deferredInteractive.RemoveAt(0);
                var delivery = EventDeliveryManager.Instance;
                if (delivery == null)
                {
                    ModLogger.Expected("CONTENT", "deferred_flush_no_delivery_manager",
                        "EventDeliveryManager.Instance null — deferred candidate dropped");
                    // The head was already removed above, so it's lost for this tick.
                    // Stop probing: if the delivery manager is unavailable, further probes
                    // on the same tick would compound the drop.
                    return false;
                }

                DownshiftSpeedIfNeeded();
                delivery.QueueEvent(evt);
                _lastModalDay = today;
                _lastModalUtcTicks = DateTime.UtcNow.Ticks;
                _categoryCooldowns[next.CategoryId ?? NoCategorySentinel] = today;
                return true;
            }

            return false;
        }

        private void Route(StoryCandidate c, StoryTier tier)
        {
            int today = (int)CampaignTime.Now.ToDays;

            if (tier == StoryTier.Modal && c.InteractiveEvent != null)
            {
                if (!ModalFloorsAllow(c, today))
                {
                    _deferredInteractive.Add(MakePersistent(c, tier, today));
                    if (_deferredInteractive.Count > DeferredInteractiveCap)
                    {
                        _deferredInteractive.RemoveAt(0);
                    }
                    return;
                }

                var delivery = EventDeliveryManager.Instance;
                if (delivery == null)
                {
                    ModLogger.Expected("CONTENT", "route_no_delivery_manager",
                        "EventDeliveryManager.Instance null — candidate dropped");
                    return;
                }

                DownshiftSpeedIfNeeded();
                delivery.QueueEvent(c.InteractiveEvent);
                _lastModalDay = today;
                _lastModalUtcTicks = DateTime.UtcNow.Ticks;
                _categoryCooldowns[CategoryKey(c)] = today;
                return;
            }

            WriteDispatchItem(c, tier);
        }

        private static string CategoryKey(StoryCandidate c) => c.CategoryId ?? NoCategorySentinel;

        private bool ModalFloorsAllow(StoryCandidate c, int today)
        {
            bool wallClockFloor;
            if (_lastModalUtcTicks == 0)
            {
                wallClockFloor = true; // first-ever fire — no wall-clock constraint.
            }
            else
            {
                double wallSeconds = (DateTime.UtcNow.Ticks - _lastModalUtcTicks) / (double)TimeSpan.TicksPerSecond;
                wallClockFloor = wallSeconds >= DensitySettings.ModalFloorWallClockSeconds;
            }

            // Chain continuations (promotions, bag checks, chain events) are follow-up
            // beats the player already opted into. They bypass the in-game floor and
            // the per-category cooldown but still honor the wall-clock guard so two
            // modals never fire in the same second.
            if (c != null && c.ChainContinuation)
            {
                return wallClockFloor;
            }

            bool inGameFloor = today - _lastModalDay >= DensitySettings.ModalFloorInGameDays;

            bool categoryOk = !_categoryCooldowns.TryGetValue(CategoryKey(c), out int lastDay)
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
                category: c.DispatchCategory ?? DefaultDispatchCategory,
                headlineKey: c.RenderedTitle,
                placeholderValues: null,
                storyKey: c.StoryKey,
                severity: severity,
                minDisplayDays: c.MinDisplayDays,
                tier: tier,
                beats: c.Beats != null ? new System.Collections.Generic.HashSet<StoryBeat>(c.Beats) : null,
                body: c.RenderedBody);
        }
    }
}
