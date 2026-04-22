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

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Hourly-tick duty emitter. Gated by IsEnlisted. Reads the intelligence
    /// snapshot + current duty profile, invokes DutyOpportunityBuilder,
    /// throttle-claims via OrdersNewsFeedThrottle, picks one opportunity,
    /// emits via StoryDirector. Cooldown state persists in DutyCooldownStore
    /// via SaveableTypeDefiner offset 50.
    /// </summary>
    [UsedImplicitly("Registered in SubModule.OnGameStart via campaignStarter.AddBehavior.")]
    public sealed class EnlistedDutyEmitterBehavior : CampaignBehaviorBase
    {
        public static EnlistedDutyEmitterBehavior Instance { get; private set; }

        private DutyCooldownStore _cooldowns = new DutyCooldownStore();
        private readonly Queue<string> _recentEmittedIds = new Queue<string>();
        private int _lastHeartbeatHourTick = int.MinValue / 2;

        private const int HEARTBEAT_INTERVAL_HOURS = 12;
        private const int DEFAULT_COOLDOWN_HOURS = 36;
        private const int RECENT_HISTORY_SIZE = 3;
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
                _ = dataStore.SyncData("_dutyCooldowns", ref _cooldowns);
                _cooldowns = _cooldowns ?? new DutyCooldownStore();
                _cooldowns.EnsureInitialized();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTY", "SyncData failed", ex);
                _cooldowns = new DutyCooldownStore();
            }
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _cooldowns = _cooldowns ?? new DutyCooldownStore();
            _cooldowns.EnsureInitialized();
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

                var activity = OrderActivity.Instance;
                if (activity == null)
                {
                    return;
                }

                var snapshot = EnlistedCampaignIntelligenceBehavior.Instance?.Current;
                if (snapshot == null)
                {
                    return;
                }

                var candidates = EnlistedDutyOpportunityBuilder.Build(snapshot, activity.CurrentDutyProfile);
                if (candidates == null || candidates.Count == 0)
                {
                    return;
                }

                if (!OrdersNewsFeedThrottle.TryClaim())
                {
                    return;
                }

                foreach (var opp in candidates)
                {
                    var storylet = ResolveStoryletForOpportunity(opp);
                    if (storylet == null)
                    {
                        continue;
                    }

                    EmitOpportunity(opp, storylet);
                    return;
                }

                ModLogger.Expected("DUTY", "no_opportunity_storylet",
                    "no eligible storylet found for any candidate",
                    new Dictionary<string, object>
                    {
                        { "candidate_count", candidates.Count },
                        { "profile", activity.CurrentDutyProfile }
                    });
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTY", "OnHourlyTick threw", ex);
            }
        }

        private Storylet ResolveStoryletForOpportunity(DutyOpportunity opp)
        {
            if (opp == null)
            {
                return null;
            }

            if (opp.Shape == DutyOpportunityShape.ArcScale)
            {
                if (string.IsNullOrEmpty(opp.ArchetypeStoryletId))
                {
                    return null;
                }
                var direct = StoryletCatalog.GetById(opp.ArchetypeStoryletId);
                if (direct == null)
                {
                    return null;
                }
                return IsEligibleForEmit(direct) ? direct : null;
            }

            return PickEpisodicFromPool(opp.PoolPrefix);
        }

        private Storylet PickEpisodicFromPool(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return null;
            }

            var eligible = new List<(Storylet s, float weight)>();

            foreach (var s in StoryletCatalog.All)
            {
                if (s?.Id == null || !s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!IsEligibleForEmit(s))
                {
                    continue;
                }

                var w = s.WeightFor(null);
                foreach (var recent in _recentEmittedIds)
                {
                    if (string.Equals(recent, s.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        w *= RECENT_PENALTY_PER_HIT;
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
                    return e.s;
                }
            }

            return eligible[eligible.Count - 1].s;
        }

        private bool IsEligibleForEmit(Storylet storylet)
        {
            if (storylet == null || string.IsNullOrEmpty(storylet.Id))
            {
                return false;
            }

            var nowHours = (int)CampaignTime.Now.ToHours;
            if (_cooldowns.LastFiredAt.TryGetValue(storylet.Id, out var last))
            {
                var cooldownHours = storylet.CooldownDays > 0
                    ? storylet.CooldownDays * 24
                    : DEFAULT_COOLDOWN_HOURS;
                if (nowHours - (int)last.ToHours < cooldownHours)
                {
                    return false;
                }
            }

            if (!TriggerRegistry.Evaluate(storylet.Trigger, null))
            {
                return false;
            }

            return true;
        }

        private void EmitOpportunity(DutyOpportunity opp, Storylet storylet)
        {
            var tier = opp.Shape == DutyOpportunityShape.ArcScale
                ? StoryTier.Modal
                : StoryTier.Log;

            StoryDirector.Instance?.EmitCandidate(new StoryCandidate
            {
                SourceId = "duty." + opp.Shape.ToString().ToLowerInvariant(),
                CategoryId = string.IsNullOrEmpty(storylet.Category) ? "duty" : storylet.Category,
                ProposedTier = tier,
                EmittedAt = CampaignTime.Now,
                RenderedTitle = storylet.Title,
                RenderedBody = storylet.Setup,
                StoryKey = storylet.Id
            });

            _cooldowns.LastFiredAt[storylet.Id] = CampaignTime.Now;

            if (opp.Shape == DutyOpportunityShape.Episodic)
            {
                _recentEmittedIds.Enqueue(storylet.Id);
                while (_recentEmittedIds.Count > RECENT_HISTORY_SIZE)
                {
                    _recentEmittedIds.Dequeue();
                }
            }

            // Episodic Log-tier storylets don't render a modal, so their single-option
            // effects must auto-apply. Multi-option storylets rely on player interaction.
            if (opp.Shape == DutyOpportunityShape.Episodic && storylet.Options?.Count == 1)
            {
                var mainOpt = storylet.Options.FirstOrDefault(o => o.Id == "main");
                if (mainOpt?.Effects != null)
                {
                    EffectExecutor.Apply(mainOpt.Effects, null);
                }
            }

            ModLogger.Info("DUTY",
                $"emitted {opp.Shape} storylet={storylet.Id} reason={opp.TriggerReason}");
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
            var hasSnap = EnlistedCampaignIntelligenceBehavior.Instance?.Current != null;
            var tracked = _cooldowns?.LastFiredAt?.Count ?? 0;
            ModLogger.Info("DUTY",
                $"heartbeat: enlisted={isEnlisted} snapshot={hasSnap} tracked={tracked}");
        }
    }
}
