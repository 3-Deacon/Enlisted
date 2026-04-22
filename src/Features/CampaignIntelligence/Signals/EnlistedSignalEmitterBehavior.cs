using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// Hourly-tick emitter for Plan 3 signal projection. Gated by
    /// EnlistmentBehavior.IsEnlisted. Reads the Plan 1 snapshot each tick,
    /// invokes SignalBuilder, throttle-claims via OrdersNewsFeedThrottle,
    /// picks a floor-storylet from the signal family's pool, emits via
    /// StoryDirector.EmitCandidate at StoryTier.Log. Cooldown state
    /// persists across save/load in SignalEmissionRecord.
    /// </summary>
    [UsedImplicitly("Registered in SubModule.OnGameStart via campaignStarter.AddBehavior.")]
    public sealed class EnlistedSignalEmitterBehavior : CampaignBehaviorBase
    {
        public static EnlistedSignalEmitterBehavior Instance { get; private set; }

        private SignalEmissionRecord _record = new SignalEmissionRecord();
        private int _lastHeartbeatHourTick = int.MinValue / 2;

        private readonly Dictionary<SignalType, Queue<string>> _recentByFamily = new Dictionary<SignalType, Queue<string>>();

        private const int HEARTBEAT_INTERVAL_HOURS = 12;
        private const int DEFAULT_COOLDOWN_HOURS = 36;
        private const int RECENT_HISTORY_PER_FAMILY = 3;
        private const float RECENT_PENALTY_PER_HIT = 0.7f;

        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                _ = dataStore.SyncData("_signalRecord", ref _record);
                _record = _record ?? new SignalEmissionRecord();
                _record.EnsureInitialized();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SIGNAL", "SyncData failed", ex);
                _record = new SignalEmissionRecord();
            }
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _record = _record ?? new SignalEmissionRecord();
            _record.EnsureInitialized();
        }

        private void OnHourlyTick()
        {
            try
            {
                LogHeartbeatIfDue();

                if (EnlistmentBehavior.Instance?.IsEnlisted != true)
                {
                    return;
                }

                var snapshot = EnlistedCampaignIntelligenceBehavior.Instance?.Current;
                if (snapshot == null)
                {
                    return;
                }

                var candidates = EnlistedCampaignSignalBuilder.Build(snapshot);
                if (candidates == null || candidates.Count == 0)
                {
                    return;
                }

                if (!OrdersNewsFeedThrottle.TryClaim())
                {
                    return;
                }

                var picked = candidates[0];

                var storyletId = PickStoryletFromPool(picked.PoolPrefix, picked.Type);
                if (storyletId == null)
                {
                    ModLogger.Expected("SIGNAL", "no_storylet_for_family",
                        "no eligible floor storylet for signal family",
                        new Dictionary<string, object>
                        {
                            { "signal_type", picked.Type.ToString() },
                            { "pool_prefix", picked.PoolPrefix }
                        });
                    return;
                }

                EmitPicked(picked, storyletId, overrideBody: null);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SIGNAL", "OnHourlyTick threw", ex);
            }
        }

        /// <summary>
        /// Selects a floor storylet from the pool matching <paramref name="prefix"/>,
        /// weighted by the storylet's base weight times its satisfied modifier factors,
        /// and biased against the last few emissions from the same family. Storylets
        /// on cooldown or whose triggers fail are skipped. When eligible storylets
        /// remain, one is sampled proportionally to its adjusted weight; otherwise
        /// null is returned.
        /// </summary>
        private string PickStoryletFromPool(string prefix, SignalType type)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return null;
            }

            var nowHours = (int)CampaignTime.Now.ToHours;
            var eligible = new List<(Storylet s, float weight)>();
            Queue<string> recent;
            _recentByFamily.TryGetValue(type, out recent);

            foreach (var s in StoryletCatalog.All)
            {
                if (s?.Id == null || !s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_record.LastFiredAt.TryGetValue(s.Id, out var last))
                {
                    var cooldownHours = s.CooldownDays > 0 ? s.CooldownDays * 24 : DEFAULT_COOLDOWN_HOURS;
                    if (nowHours - (int)last.ToHours < cooldownHours)
                    {
                        continue;
                    }
                }

                if (!TriggerRegistry.Evaluate(s.Trigger, null))
                {
                    continue;
                }

                var w = s.WeightFor(null);
                if (recent != null)
                {
                    foreach (var recentId in recent)
                    {
                        if (string.Equals(recentId, s.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            w *= RECENT_PENALTY_PER_HIT;
                        }
                    }
                }

                if (w > 0f)
                {
                    eligible.Add((s, w));
                }
            }

            if (eligible.Count == 0)
            {
                return null;
            }

            var total = 0f;
            foreach (var e in eligible)
            {
                total += e.weight;
            }

            var roll = MBRandom.RandomFloat * total;
            var acc = 0f;
            foreach (var e in eligible)
            {
                acc += e.weight;
                if (roll <= acc)
                {
                    return e.s.Id;
                }
            }

            return eligible[eligible.Count - 1].s.Id;
        }

        /// <summary>
        /// Emits the picked storylet as a signal-tagged StoryCandidate.
        /// When <paramref name="overrideBody"/> is non-null, it replaces
        /// <c>storylet.Setup</c> as the candidate's <c>RenderedBody</c> — for
        /// callers that supply their own body text while still wearing the
        /// floor storylet's title and signal metadata. The title and all
        /// signal fields always come from the storylet / signal pair; only
        /// the body string is swappable.
        /// </summary>
        private void EmitPicked(EnlistedCampaignSignal signal, string storyletId, string overrideBody)
        {
            var storylet = StoryletCatalog.GetById(storyletId);
            if (storylet == null)
            {
                return;
            }

            var perspective = signal.SourcePerspectiveOverride != null
                ? ParsePerspective(signal.SourcePerspectiveOverride)
                : DefaultPerspectiveFor(signal.Type);

            var director = StoryDirector.Instance;
            if (director == null)
            {
                ModLogger.Expected("SIGNAL", "no_director", "StoryDirector.Instance null at emit");
                return;
            }

            director.EmitCandidate(new StoryCandidate
            {
                SourceId = "signal." + signal.Type.ToString().ToLowerInvariant(),
                CategoryId = "signal." + signal.Type.ToString().ToLowerInvariant(),
                ProposedTier = StoryTier.Log,
                EmittedAt = CampaignTime.Now,
                RenderedTitle = storylet.Title,
                RenderedBody = overrideBody ?? storylet.Setup,
                StoryKey = storyletId,
                SourcePerspective = perspective,
                SignalConfidence = signal.Confidence,
                SignalRecency = ClassifyRecency(signal.AsOf),
                SignalChangeType = signal.ChangeType,
                SignalInference = signal.InferenceOverride
            });

            _record.LastFiredAt[storyletId] = CampaignTime.Now;

            var mainOpt = storylet.Options?.FirstOrDefault(o => o.Id == "main");
            if (mainOpt?.Effects != null)
            {
                EffectExecutor.Apply(mainOpt.Effects, null);
            }

            ModLogger.Info("SIGNAL", "emitted " + signal.Type + " storylet=" + storyletId + " confidence=" + signal.Confidence);

            if (!_recentByFamily.TryGetValue(signal.Type, out var historyQueue))
            {
                historyQueue = new Queue<string>();
                _recentByFamily[signal.Type] = historyQueue;
            }
            historyQueue.Enqueue(storyletId);
            while (historyQueue.Count > RECENT_HISTORY_PER_FAMILY)
            {
                historyQueue.Dequeue();
            }
        }

        /// <summary>
        /// Emits an externally-built signal through this emitter's throttle and
        /// storylet-pool lookup. Used by <see cref="DailyDriftApplicator"/>'s
        /// flush path so its drift summary text wears the same signal-pipeline
        /// metadata (family, perspective, cooldown) as state-driven emissions
        /// without re-running SignalBuilder. <paramref name="bodyText"/> is the
        /// caller's authoritative body: on a pool hit it's passed to
        /// <see cref="EmitPicked"/> as <c>overrideBody</c> so the storylet's
        /// atmospheric Setup is NOT substituted for the caller's content; on a
        /// pool miss it's written directly into a synthesized candidate's
        /// <c>RenderedBody</c>. Titles still come from the storylet (hit) or a
        /// short default (miss) — only the body is caller-owned.
        /// Returns true when an emit reached StoryDirector, false when the
        /// throttle rejected or the signal was null.
        /// </summary>
        public bool EmitExternalSignal(EnlistedCampaignSignal signal, string bodyText)
        {
            if (signal == null)
            {
                return false;
            }
            if (!OrdersNewsFeedThrottle.TryClaim())
            {
                return false;
            }

            var storyletId = PickStoryletFromPool(signal.PoolPrefix, signal.Type);
            if (storyletId == null)
            {
                // Pool empty for this family — synthesize a direct emit so the
                // caller's body still reaches the player. Pool-tracking state
                // (LastFiredAt, _recentByFamily) is intentionally skipped: no
                // storylet was selected, so there's nothing to record.
                var director = StoryDirector.Instance;
                if (director == null)
                {
                    ModLogger.Expected("SIGNAL", "no_director_external", "StoryDirector.Instance null at external emit");
                    return false;
                }

                director.EmitCandidate(new StoryCandidate
                {
                    SourceId = "signal." + signal.Type.ToString().ToLowerInvariant(),
                    CategoryId = "signal." + signal.Type.ToString().ToLowerInvariant(),
                    ProposedTier = StoryTier.Log,
                    EmittedAt = CampaignTime.Now,
                    RenderedTitle = "Service notes",
                    RenderedBody = bodyText,
                    StoryKey = "signal_drift_summary",
                    SourcePerspective = DefaultPerspectiveFor(signal.Type),
                    SignalConfidence = signal.Confidence,
                    SignalRecency = SignalRecency.Immediate,
                    SignalChangeType = signal.ChangeType,
                    SignalInference = signal.InferenceOverride
                });
                return true;
            }

            EmitPicked(signal, storyletId, overrideBody: bodyText);
            return true;
        }

        private SourcePerspective DefaultPerspectiveFor(SignalType type)
        {
            switch (type)
            {
                case SignalType.Rumor: return SourcePerspective.Rumor;
                case SignalType.CampTalk: return SourcePerspective.OwnObservation;
                case SignalType.Dispatch: return SourcePerspective.Courier;
                case SignalType.OrderShift: return SourcePerspective.Sergeant;
                case SignalType.QuartermasterWarning: return SourcePerspective.Quartermaster;
                case SignalType.ScoutReturn: return SourcePerspective.Narrator;
                case SignalType.PrisonerReport: return SourcePerspective.Narrator;
                case SignalType.AftermathNotice: return SourcePerspective.Narrator;
                case SignalType.TensionIncident: return SourcePerspective.OwnObservation;
                case SignalType.SilenceWithImplication: return SourcePerspective.Narrator;
                default: return SourcePerspective.Narrator;
            }
        }

        private SignalRecency ClassifyRecency(CampaignTime asOf)
        {
            var hours = (CampaignTime.Now - asOf).ToHours;
            if (hours < 2)
            {
                return SignalRecency.Immediate;
            }
            if (hours < 24)
            {
                return SignalRecency.RecentHours;
            }
            if (hours < 72)
            {
                return SignalRecency.RecentDays;
            }
            return SignalRecency.Stale;
        }

        private SourcePerspective ParsePerspective(string s)
        {
            return Enum.TryParse<SourcePerspective>(s, true, out var p) ? p : SourcePerspective.Narrator;
        }

        private void LogHeartbeatIfDue()
        {
            var nowHour = (int)CampaignTime.Now.ToHours;
            if (nowHour - _lastHeartbeatHourTick < HEARTBEAT_INTERVAL_HOURS)
            {
                return;
            }
            _lastHeartbeatHourTick = nowHour;

            var isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted == true;
            var snapPresent = EnlistedCampaignIntelligenceBehavior.Instance?.Current != null;
            var trackedStorylets = _record?.LastFiredAt?.Count ?? 0;
            ModLogger.Info("SIGNAL", "heartbeat: enlisted=" + isEnlisted + " snapshot_present=" + snapPresent + " tracked=" + trackedStorylets);
        }
    }
}
