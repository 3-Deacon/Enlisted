using Enlisted.Mod.Core.Triggers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Decides whether a story candidate is relevant enough to the player to surface.
    /// Drops ~90% of raw world events at the source (distant wars, irrelevant battles,
    /// unknown heroes). A candidate passes if it touches the enlisted lord, the player's
    /// kingdom, a recently-visited settlement, a known hero, a nearby world position,
    /// or an active enemy of the lord. Recency for settlements uses
    /// CampaignTriggerTrackerBehavior.Instance — no flag persistence needed.
    /// </summary>
    public static class RelevanceFilter
    {
        private const float RecentSettlementDays = 60f;
        private const float TravelDaysThreshold = 7f;

        /// <summary>
        /// Returns true when the candidate described by <paramref name="key"/> is
        /// relevant enough to surface to the player. Returns false if either required
        /// context argument is null.
        /// </summary>
        public static bool Passes(RelevanceKey key, Hero enlistedLord, MobileParty playerParty)
        {
            if (enlistedLord == null || playerParty == null)
            {
                return false;
            }

            if (key.TouchesEnlistedLord || key.TouchesPlayerKingdom)
            {
                return true;
            }

            if (key.SubjectHero != null && key.SubjectHero == enlistedLord)
            {
                return true;
            }

            if (key.SubjectKingdom != null && Hero.MainHero?.Clan?.Kingdom != null
                && key.SubjectKingdom == Hero.MainHero.Clan.Kingdom)
            {
                return true;
            }

            if (key.SubjectSettlement != null && RecentlyVisited(key.SubjectSettlement))
            {
                return true;
            }

            if (key.SubjectHero != null && HeroKnownToPlayer(key.SubjectHero))
            {
                return true;
            }

            if (key.SubjectPosition.IsNonZero())
            {
                float distance = playerParty.GetPosition2D.Distance(key.SubjectPosition);
                float partySpeed = playerParty.Speed > 0f ? playerParty.Speed : 5f;
                float daysOfTravel = distance / partySpeed;
                if (daysOfTravel <= TravelDaysThreshold)
                {
                    return true;
                }
            }

            if (key.SubjectFaction != null && IsActiveEnemyOfLord(key.SubjectFaction, enlistedLord))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true when the player has entered the given settlement within the
        /// last <see cref="RecentSettlementDays"/> days, according to the tracker.
        /// </summary>
        private static bool RecentlyVisited(TaleWorlds.CampaignSystem.Settlements.Settlement s)
        {
            var tracker = CampaignTriggerTrackerBehavior.Instance;
            if (tracker == null || s?.StringId == null)
            {
                return false;
            }

            return tracker.LastSettlementEnteredId == s.StringId
                && tracker.IsWithinDays(tracker.LastSettlementEnteredTime, RecentSettlementDays);
        }

        /// <summary>
        /// Returns true when the hero is alive and has a positive relation with the player.
        /// </summary>
        private static bool HeroKnownToPlayer(Hero hero)
        {
            if (hero?.IsAlive != true)
            {
                return false;
            }

            return Hero.MainHero.GetRelation(hero) > 0;
        }

        /// <summary>
        /// Returns true when the given faction is currently at war with the enlisted
        /// lord's kingdom.
        /// </summary>
        private static bool IsActiveEnemyOfLord(IFaction faction, Hero enlistedLord)
        {
            var lordKingdom = enlistedLord?.Clan?.Kingdom;
            if (lordKingdom == null || faction == null)
            {
                return false;
            }

            return FactionManager.IsAtWarAgainstFaction(lordKingdom, faction);
        }
    }
}
