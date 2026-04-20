using System;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.Activities.Home
{
    /// <summary>
    /// Engine-state-reading trigger predicates for the enlisted-home storylets.
    /// Every predicate is null-safe and reads live Bannerlord state — never QualityStore
    /// mirrors of engine data. Registered by SubModule at boot before any behavior is added.
    /// </summary>
    public static class HomeTriggers
    {
        public static void Register()
        {
            // --- Party / food ---
            TriggerRegistry.Register("party_food_lte_3_days", (_, _2) => PartyFoodDays() <= 3);
            TriggerRegistry.Register("party_food_lte_5_days", (_, _2) => PartyFoodDays() <= 5);
            TriggerRegistry.Register("party_food_starving",   (_, _2) =>
            {
                var p = Party();
                return p != null && p.Food <= 0f;
            });

            // --- Lord morale ---
            TriggerRegistry.Register("lord_morale_lte_30", (_, _2) => LordMorale() <= 30f);
            TriggerRegistry.Register("lord_morale_lte_50", (_, _2) => LordMorale() <= 50f);
            TriggerRegistry.Register("lord_morale_lte_70", (_, _2) => LordMorale() <= 70f);
            TriggerRegistry.Register("lord_morale_gte_80", (_, _2) => LordMorale() >= 80f);

            // --- Lord condition ---
            TriggerRegistry.Register("lord_is_wounded",      (_, _2) => Lord()?.IsWounded == true);
            TriggerRegistry.Register("lord_is_disorganized", (_, _2) => Party()?.IsDisorganized == true);
            TriggerRegistry.Register("lord_at_home_settlement", (_, _2) =>
            {
                var lord = Lord();
                if (lord == null) { return false; }
                var current = Party()?.CurrentSettlement;
                return current != null && lord.HomeSettlement != null
                    && current.StringId == lord.HomeSettlement.StringId;
            });
            TriggerRegistry.Register("lord_has_spouse",   (_, _2) => Lord()?.Spouse != null);
            TriggerRegistry.Register("lord_has_children", (_, _2) =>
            {
                var children = Lord()?.Children;
                return children != null && children.Count > 0;
            });

            // --- Lord traits (mercy, valor, honor, calculating × positive/negative) ---
            RegisterTrait("mercy",      DefaultTraits.Mercy);
            RegisterTrait("valor",      DefaultTraits.Valor);
            RegisterTrait("honor",      DefaultTraits.Honor);
            RegisterTrait("calculating", DefaultTraits.Calculating);

            // --- Clan / kingdom ---
            TriggerRegistry.Register("enlisted_lord_is_mercenary", (_, _2) =>
                Lord()?.Clan?.IsUnderMercenaryService == true);

            TriggerRegistry.Register("kingdom_at_war", (_, _2) =>
            {
                var clan = Lord()?.Clan;
                if (clan == null) { return false; }
                var factions = clan.Kingdom?.FactionsAtWarWith ?? clan.FactionsAtWarWith;
                return factions != null && factions.Count > 0;
            });

            TriggerRegistry.Register("kingdom_multi_front_war", (_, _2) =>
            {
                var clan = Lord()?.Clan;
                if (clan == null) { return false; }
                var factions = clan.Kingdom?.FactionsAtWarWith ?? clan.FactionsAtWarWith;
                return factions != null && factions.Count > 1;
            });

            // --- Settlement type ---
            TriggerRegistry.Register("at_town",    (_, _2) => Settlement()?.IsTown    == true);
            TriggerRegistry.Register("at_castle",  (_, _2) => Settlement()?.IsCastle  == true);
            TriggerRegistry.Register("at_village", (_, _2) => Settlement()?.IsVillage == true);

            // --- Settlement state ---
            TriggerRegistry.Register("settlement_under_siege", (_, _2) =>
                Settlement()?.IsUnderSiege == true);

            TriggerRegistry.Register("settlement_recent_threat", (_, _2) =>
            {
                var s = Settlement();
                if (s == null) { return false; }
                return s.LastThreatTime.ElapsedDaysUntilNow <= 7.0;
            });

            TriggerRegistry.Register("settlement_low_loyalty", (_, _2) =>
            {
                var s = Settlement();
                if (s == null) { return false; }
                var town = s.Town;
                return town != null && town.Loyalty < 40f;
            });

            // --- Time ---
            TriggerRegistry.Register("is_night",   (_, _2) => CampaignTime.Now.IsNightTime);
            TriggerRegistry.Register("is_autumn",  (_, _2) =>
                CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Autumn);
            TriggerRegistry.Register("is_winter",  (_, _2) =>
                CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Winter);

            // --- Mod state ---
            TriggerRegistry.Register("days_enlisted_gte_30", (_, _2) =>
            {
                var eb = EnlistmentBehavior.Instance;
                return eb != null && (int)eb.DaysServed >= 30;
            });
            TriggerRegistry.Register("days_enlisted_gte_90", (_, _2) =>
            {
                var eb = EnlistmentBehavior.Instance;
                return eb != null && (int)eb.DaysServed >= 90;
            });
        }

        // Registers positive and negative trait-level predicates for the given trait name.
        private static void RegisterTrait(string name, TraitObject trait)
        {
            TriggerRegistry.Register($"lord_trait_{name}_positive",
                (_, _2) => (Lord()?.GetTraitLevel(trait) ?? 0) >= 1);
            TriggerRegistry.Register($"lord_trait_{name}_negative",
                (_, _2) => (Lord()?.GetTraitLevel(trait) ?? 0) <= -1);
        }

        // Returns the enlisted lord hero, or null if not enlisted.
        private static Hero Lord() => EnlistmentBehavior.Instance?.EnlistedLord;

        // Returns the party the enlisted lord belongs to, or null.
        private static MobileParty Party() => Lord()?.PartyBelongedTo;

        // Returns the settlement the lord's party is currently at, or null.
        private static Settlement Settlement() => Party()?.CurrentSettlement;

        // Returns the lord party's morale, or float.MinValue when the party is unavailable.
        private static float LordMorale()
        {
            var p = Party();
            return p != null ? p.Morale : float.MinValue;
        }

        // Returns the number of days of food remaining in the lord's party.
        // Returns int.MaxValue when the party is unavailable or the calculation throws.
        private static int PartyFoodDays()
        {
            var p = Party();
            if (p == null) { return int.MaxValue; }
            try
            {
                return p.GetNumDaysForFoodToLast();
            }
            catch
            {
                return int.MaxValue;
            }
        }
    }
}
