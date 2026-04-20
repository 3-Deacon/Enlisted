using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Enlisted.Features.Activities.Home
{
    /// <summary>
    /// Starts a HomeActivity when the enlisted lord's party enters a friendly town, castle,
    /// or village, and emits the "departure_imminent" beat when that party leaves. Party-identity
    /// gated: only the enlisted lord's own party triggers lifecycle changes; other parties
    /// entering/leaving settlements are silently ignored.
    /// </summary>
    public sealed class HomeActivityStarter : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero heroLeader)
        {
            try
            {
                var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (lord == null)
                {
                    return;
                }

                // Party-identity gate: only fire for the enlisted lord's own party.
                if (party != lord.PartyBelongedTo)
                {
                    return;
                }

                if (!ShouldStart(lord, settlement))
                {
                    return;
                }

                var activity = new HomeActivity();
                activity.Intent = PickInitialIntent(lord, settlement);
                var ctx = ActivityContext.FromCurrent();
                ActivityRuntime.Instance?.Start(activity, ctx);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("HOME-START", "Failed to start HomeActivity", ex);
            }
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            try
            {
                var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (lord == null)
                {
                    return;
                }

                // Party-identity gate: only fire for the enlisted lord's own party.
                if (party != lord.PartyBelongedTo)
                {
                    return;
                }

                var home = ActivityRuntime.Instance?.FindActive<HomeActivity>();
                if (home == null)
                {
                    return;
                }

                home.OnBeat("departure_imminent", ActivityContext.FromCurrent());
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("HOME-START", "Failed to emit departure_imminent beat", ex);
            }
        }

        /// <summary>
        /// Returns true when starting a HomeActivity is appropriate for this lord + settlement.
        /// Rejects hideouts, non-fief settlement types, hostile factions, and duplicate starts.
        /// </summary>
        private static bool ShouldStart(Hero lord, Settlement settlement)
        {
            if (settlement.IsHideout)
            {
                return false;
            }

            if (!settlement.IsTown && !settlement.IsCastle && !settlement.IsVillage)
            {
                return false;
            }

            if (settlement.MapFaction != lord.MapFaction)
            {
                ModLogger.Expected("HOME-START", "hostile_fief",
                    "Settlement belongs to a hostile faction; skipping HomeActivity start");
                return false;
            }

            if (ActivityRuntime.Instance?.FindActive<HomeActivity>() != null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Selects an initial intent for the HomeActivity using weighted random selection.
        /// Weights are adjusted by the lord's traits, service type, and current circumstances.
        /// Falls back to "unwind" if all weights sum to zero.
        /// </summary>
        private static string PickInitialIntent(Hero lord, Settlement settlement)
        {
            float unwindWeight = 1.0f;
            float trainHardWeight = 0f;
            float broodWeight = 0f;
            float communeWeight = 0f;
            float schemeWeight = 0f;

            // Brood: lord is wounded or player hero is at low HP.
            if (lord.IsWounded || (Hero.MainHero != null && Hero.MainHero.IsWounded))
            {
                broodWeight += 1.0f;
            }

            // Train hard: lord has the Valor trait.
            if (lord.GetTraitLevel(DefaultTraits.Valor) >= 1)
            {
                trainHardWeight += 1.0f;
            }

            // Scheme: lord is a mercenary and has the Calculating trait.
            if (lord.Clan?.IsUnderMercenaryService == true
                && lord.GetTraitLevel(DefaultTraits.Calculating) >= 1)
            {
                schemeWeight += 1.0f;
            }

            // Commune: arriving at the lord's home settlement while they have a spouse.
            if (lord.HomeSettlement == settlement && lord.Spouse != null)
            {
                communeWeight += 1.0f;
            }

            var total = unwindWeight + trainHardWeight + broodWeight + communeWeight + schemeWeight;
            if (total <= 0f)
            {
                return "unwind";
            }

            var r = MBRandom.RandomFloat * total;
            var cursor = 0f;

            cursor += unwindWeight;
            if (r < cursor)
            {
                return "unwind";
            }

            cursor += trainHardWeight;
            if (r < cursor)
            {
                return "train_hard";
            }

            cursor += broodWeight;
            if (r < cursor)
            {
                return "brood";
            }

            cursor += communeWeight;
            if (r < cursor)
            {
                return "commune";
            }

            cursor += schemeWeight;
            if (r < cursor)
            {
                return "scheme";
            }

            return "unwind";
        }
    }
}
