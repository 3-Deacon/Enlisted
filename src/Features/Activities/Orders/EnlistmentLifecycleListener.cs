using System;
using Enlisted.Features.Activities;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Captures prior-service flags when enlistment ends and notes kingdom changes affecting the enlisted lord's clan.</summary>
    public sealed class EnlistmentLifecycleListener : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunchedStartIfEnlisted);

            // Static events persist across Campaign teardown; guard against duplicate
            // subscription when RegisterEvents is re-invoked on save/load cycles.
            EnlistmentBehavior.OnEnlisted -= OnEnlistedStartActivity;
            EnlistmentBehavior.OnEnlisted += OnEnlistedStartActivity;
            EnlistmentBehavior.OnEnlistmentEnded -= OnEnlistmentEnded;
            EnlistmentBehavior.OnEnlistmentEnded += OnEnlistmentEnded;
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunchedStartIfEnlisted(CampaignGameStarter starter)
        {
            try
            {
                if (EnlistmentBehavior.Instance?.IsEnlisted == true)
                {
                    StartOrderActivityIfNeeded();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LORDSTATE", "OnSessionLaunchedStartIfEnlisted threw", ex);
            }
        }

        private static void OnEnlistedStartActivity(Hero lord)
        {
            try
            {
                StartOrderActivityIfNeeded();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LORDSTATE", "OnEnlistedStartActivity threw", ex);
            }
        }

        private static void StartOrderActivityIfNeeded()
        {
            if (OrderActivity.Instance != null)
            {
                return;
            }

            var activity = new OrderActivity();
            var ctx = ActivityContext.FromCurrent();
            ActivityRuntime.Instance?.Start(activity, ctx);
            ModLogger.Info("LORDSTATE", "OrderActivity started");
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            try
            {
                var enlistedLord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (enlistedLord == null || enlistedLord.Clan != clan)
                {
                    return;
                }
                ModLogger.Info("LORDSTATE", $"Lord's clan changed kingdom: {oldKingdom?.Name} -> {newKingdom?.Name}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LORDSTATE", "OnClanChangedKingdom threw", ex);
            }
        }

        private static void OnEnlistmentEnded(string reason, bool isHonorableDischarge)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                var lord = enlistment?.EnlistedLord;
                if (lord == null)
                {
                    return;
                }

                var cultureId = lord.Culture?.StringId ?? "neutral";
                var rank = enlistment.EnlistmentTier;
                FlagStore.Instance?.Set($"prior_service_culture_{cultureId}", CampaignTime.Never);
                FlagStore.Instance?.Set($"prior_service_rank_{rank}", CampaignTime.Never);

                var activity = OrderActivity.Instance;
                if (activity != null)
                {
                    ActivityRuntime.Instance?.Stop(activity, ActivityEndReason.Discharged);
                }

                ModLogger.Info("LORDSTATE", $"Prior service captured: culture={cultureId}, rank={rank}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LORDSTATE", "OnEnlistmentEnded threw", ex);
            }
        }
    }
}
