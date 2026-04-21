using System;
using Enlisted.Features.Activities;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Reacts to lord capture / release / death and kingdom changes while enlisted, and captures prior-service flags when enlistment ends.</summary>
    public sealed class LordStateListener : CampaignBehaviorBase
    {
        private DutyProfileBehavior _profileBehavior;

        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);

            // Static event persists across Campaign teardown; guard against duplicate
            // subscription when RegisterEvents is re-invoked on save/load cycles.
            EnlistmentBehavior.OnEnlistmentEnded -= OnEnlistmentEnded;
            EnlistmentBehavior.OnEnlistmentEnded += OnEnlistmentEnded;
        }

        public override void SyncData(IDataStore dataStore) { }

        private static bool IsTheLord(Hero hero)
        {
            return hero != null
                && EnlistmentBehavior.Instance?.IsEnlisted == true
                && hero == EnlistmentBehavior.Instance.EnlistedLord;
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (!IsTheLord(prisoner))
                {
                    return;
                }
                FlagStore.Instance?.Set("lord_imprisoned", CampaignTime.Never);
                ResolveProfileBehavior()?.ForceProfile(DutyProfileIds.Imprisoned);
                ModLogger.Info("LORDSTATE", "Lord captured; switched to imprisoned profile");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LORDSTATE", "OnHeroPrisonerTaken threw", ex);
            }
        }

        private void OnHeroPrisonerReleased(Hero hero, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification)
        {
            try
            {
                if (!IsTheLord(hero))
                {
                    return;
                }
                FlagStore.Instance?.Clear("lord_imprisoned");
                ResolveProfileBehavior()?.ForceProfile(DutyProfileIds.Wandering);
                ModLogger.Info("LORDSTATE", "Lord released; profile reset to wandering for re-sampling");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LORDSTATE", "OnHeroPrisonerReleased threw", ex);
            }
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            try
            {
                if (!IsTheLord(victim))
                {
                    return;
                }
                ModLogger.Surfaced("LORDSTATE", "lord killed; service ended", null);
                EnlistmentBehavior.Instance?.StopEnlist("lord_killed", isHonorableDischarge: true);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LORDSTATE", "OnHeroKilled threw", ex);
            }
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

        private DutyProfileBehavior ResolveProfileBehavior()
        {
            return _profileBehavior ??= Campaign.Current?.GetCampaignBehavior<DutyProfileBehavior>();
        }
    }
}
