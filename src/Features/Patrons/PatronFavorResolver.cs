using System;
using Enlisted.Features.Content;
using Enlisted.Mod.Core.Helpers;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// Centralized "can grant favor?" + "grant favor" gate. Dialog branches in
    /// EnlistedDialogManager call TryGrantFavor — when granted, the matching
    /// outcome storylet fires via ModalEventBuilder.FireDecisionOutcome and
    /// the per-favor cooldown is stamped on the entry. When refused, the caller
    /// surfaces refusalReason as inline text or via Inquiry.
    ///
    /// AnotherContract is the only kind that depends on Plan 5's runtime; the
    /// reflection-based runtime check avoids a hard compile-time dependency
    /// while Plan 5 lives on a sibling feature branch.
    /// </summary>
    public static class PatronFavorResolver
    {
        public static bool TryGrantFavor(PatronEntry entry, FavorKind kind, out string refusalReason)
        {
            refusalReason = string.Empty;
            if (entry == null || kind == FavorKind.None)
            {
                refusalReason = "{=patron_dead}This patron is no longer with us.";
                return false;
            }

            var hero = MBObjectManager.Instance?.GetObject(entry.HeroId) as Hero;
            if (hero == null || !hero.IsAlive)
            {
                refusalReason = "{=patron_dead}This patron is no longer with us.";
                return false;
            }

            if (!entry.IsFavorAvailable(kind, CampaignTime.Now))
            {
                refusalReason = "{=patron_cooldown}You asked too recently. Come back later.";
                return false;
            }

            int relation = (int)hero.GetRelationWithPlayer();
            if (!CheckKindConditions(hero, kind, relation, out refusalReason))
            {
                return false;
            }

            entry.SetCooldown(kind, CampaignTime.DaysFromNow(GetCooldownDays(kind)));
            entry.LastFavorRequestedOn = CampaignTime.Now;

            var ctx = new StoryletContext
            {
                CurrentContext = "any",
                SubjectHero = Hero.MainHero,
                EvaluatedAt = CampaignTime.Now,
            };
            ctx.ResolvedSlots["Patron"] = hero;

            ModalEventBuilder.FireDecisionOutcome(GetStoryletId(kind), ctx);

            ModLogger.Expected("PATRONS", "favor_granted",
                "patron favor granted",
                LogCtx.Of(
                    "lordId", hero.StringId,
                    "kind", kind.ToString(),
                    "relation", relation));

            return true;
        }

        public static bool IsKindAvailable(PatronEntry entry, FavorKind kind)
        {
            if (entry == null || kind == FavorKind.None)
            {
                return false;
            }
            if (kind == FavorKind.AnotherContract && !IsEndeavorRuntimeAvailable())
            {
                return false;
            }
            var hero = MBObjectManager.Instance?.GetObject(entry.HeroId) as Hero;
            if (hero == null || !hero.IsAlive)
            {
                return false;
            }
            if (!entry.IsFavorAvailable(kind, CampaignTime.Now))
            {
                return false;
            }
            return CheckKindConditions(hero, kind, (int)hero.GetRelationWithPlayer(), out _);
        }

        private static bool CheckKindConditions(Hero patron, FavorKind kind, int relation, out string refusalReason)
        {
            refusalReason = string.Empty;
            switch (kind)
            {
                case FavorKind.LetterOfIntroduction:
                    if (relation < 0)
                    {
                        refusalReason = "{=patron_relation_low}Your standing with my lord is too cold for that.";
                        return false;
                    }
                    return true;

                case FavorKind.GoldLoan:
                    if (relation < 10)
                    {
                        refusalReason = "{=patron_relation_low}Your standing with my lord is too cold for that.";
                        return false;
                    }
                    if (patron.Gold < 5000)
                    {
                        refusalReason = "{=patron_no_gold}My lord is short of coin himself.";
                        return false;
                    }
                    return true;

                case FavorKind.TroopLoan:
                    if (relation < 20)
                    {
                        refusalReason = "{=patron_relation_low}Your standing with my lord is too cold for that.";
                        return false;
                    }
                    if (!HasFreeHeroSlot(MobileParty.MainParty))
                    {
                        refusalReason = "{=patron_party_full}Your party has no room for the men he could send.";
                        return false;
                    }
                    return true;

                case FavorKind.AudienceArrangement:
                    if (relation < 5)
                    {
                        refusalReason = "{=patron_relation_low}Your standing with my lord is too cold for that.";
                        return false;
                    }
                    return true;

                case FavorKind.MarriageFacilitation:
                    if (relation < 30)
                    {
                        refusalReason = "{=patron_relation_low}Your standing with my lord is too cold for that.";
                        return false;
                    }
                    if (Hero.MainHero?.Spouse != null)
                    {
                        refusalReason = "{=patron_already_married}You already have a spouse.";
                        return false;
                    }
                    return true;

                case FavorKind.AnotherContract:
                    if (relation < 0)
                    {
                        refusalReason = "{=patron_relation_low}Your standing with my lord is too cold for that.";
                        return false;
                    }
                    if (!IsEndeavorRuntimeAvailable())
                    {
                        refusalReason = "{=patron_no_runtime}There are no contracts on offer.";
                        return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        private static int GetCooldownDays(FavorKind kind)
        {
            switch (kind)
            {
                case FavorKind.LetterOfIntroduction: return 30;
                case FavorKind.GoldLoan: return 60;
                case FavorKind.TroopLoan: return 90;
                case FavorKind.AudienceArrangement: return 45;
                case FavorKind.MarriageFacilitation: return 180;
                case FavorKind.AnotherContract: return 30;
                default: return 0;
            }
        }

        private static string GetStoryletId(FavorKind kind)
        {
            switch (kind)
            {
                case FavorKind.LetterOfIntroduction: return "patron_letter_of_introduction";
                case FavorKind.GoldLoan: return "patron_gold_loan";
                case FavorKind.TroopLoan: return "patron_troop_loan";
                case FavorKind.AudienceArrangement: return "patron_audience";
                case FavorKind.MarriageFacilitation: return "patron_marriage";
                case FavorKind.AnotherContract: return "patron_another_contract";
                default: return string.Empty;
            }
        }

        private static bool HasFreeHeroSlot(MobileParty party)
        {
            var partyBase = party?.Party;
            if (partyBase == null || party.MemberRoster == null)
            {
                return false;
            }
            return partyBase.PartySizeLimit - party.MemberRoster.TotalManCount >= 2;
        }

        private static bool IsEndeavorRuntimeAvailable()
        {
            return Type.GetType("Enlisted.Features.Endeavors.EndeavorRunner, Enlisted") != null;
        }
    }
}
