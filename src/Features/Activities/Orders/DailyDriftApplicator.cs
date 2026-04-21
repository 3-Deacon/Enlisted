using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Awards profile-weighted skill XP silently each game day and emits one accumulator-summary news-feed entry per hourly tick when the wall-clock throttle allows.</summary>
    public sealed class DailyDriftApplicator : CampaignBehaviorBase
    {
        private static readonly Dictionary<string, string[]> ProfileHeavy = new Dictionary<string, string[]>
        {
            { DutyProfileIds.Garrisoned, new[] { "OneHanded", "Polearm", "Athletics", "Charm", "Steward" } },
            { DutyProfileIds.Marching,   new[] { "Athletics", "Riding", "Scouting", "Tactics" } },
            { DutyProfileIds.Besieging,  new[] { "Engineering", "Crossbow", "Bow", "Athletics" } },
            { DutyProfileIds.Raiding,    new[] { "Athletics", "Roguery", "OneHanded", "Riding" } },
            { DutyProfileIds.Escorting,  new[] { "Tactics", "Leadership", "Riding" } },
            { DutyProfileIds.Wandering,  new[] { "Scouting", "Trade", "Roguery" } },
            { DutyProfileIds.Imprisoned, new[] { "Roguery", "Charm" } }
        };

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            try
            {
                if (EnlistmentBehavior.Instance?.IsEnlisted != true)
                {
                    return;
                }
                var activity = OrderActivity.Instance;
                if (activity == null)
                {
                    return;
                }
                activity.EnsureInitialized();

                if (!ProfileHeavy.TryGetValue(activity.CurrentDutyProfile, out var heavy))
                {
                    return;
                }

                var pickCount = activity.CurrentDutyProfile == DutyProfileIds.Imprisoned ? 1 : 2;
                for (int i = 0; i < pickCount; i++)
                {
                    var skillId = heavy[MBRandom.RandomInt(heavy.Length)];
                    var amount = MBRandom.RandomInt(5, 16);
                    if (!activity.DriftPendingXp.ContainsKey(skillId))
                    {
                        activity.DriftPendingXp[skillId] = 0;
                    }
                    activity.DriftPendingXp[skillId] += amount;

                    var skill = MBObjectManager.Instance.GetObject<SkillObject>(skillId);
                    if (skill != null)
                    {
                        Hero.MainHero.HeroDeveloper.AddSkillXp(skill, amount, shouldNotify: false);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DRIFT", "OnDailyTick threw", ex);
            }
        }

        private void OnHourlyTick()
        {
            try
            {
                if (EnlistmentBehavior.Instance?.IsEnlisted != true)
                {
                    return;
                }
                var activity = OrderActivity.Instance;
                if (activity?.DriftPendingXp == null || activity.DriftPendingXp.Count == 0)
                {
                    return;
                }

                if (!OrdersNewsFeedThrottle.TryClaim())
                {
                    return;
                }

                FlushSummary(activity);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DRIFT", "OnHourlyTick flush threw", ex);
            }
        }

        private static void FlushSummary(OrderActivity activity)
        {
            var pieces = activity.DriftPendingXp
                .OrderByDescending(kv => kv.Value)
                .Take(4)
                .Select(kv => $"{kv.Key} +{kv.Value}");
            var summary = "Past few days: " + string.Join(", ", pieces) + " XP.";

            StoryDirector.Instance?.EmitCandidate(new StoryCandidate
            {
                SourceId = "orders.drift.summary",
                CategoryId = "orders.drift",
                ProposedTier = StoryTier.Log,
                EmittedAt = CampaignTime.Now,
                RenderedTitle = "Service notes",
                RenderedBody = summary,
                StoryKey = "orders_drift_summary"
            });

            activity.DriftPendingXp.Clear();
            ModLogger.Info("DRIFT", $"Flushed summary: {summary}");
        }
    }
}
