using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TaleWorlds.CampaignSystem.Party;
using Enlisted.Mod.Core.Logging;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Fixes base game crash in PlayerIsAtSeaTag when player is prisoner in a settlement.
    /// Settlement garrison parties have MobileParty = null, causing NullReferenceException.
    /// </summary>
    [HarmonyPatch(typeof(PlayerIsAtSeaTag), nameof(PlayerIsAtSeaTag.IsApplicableTo))]
    public static class PlayerIsAtSeaTagCrashFix
    {
        private static bool Prefix(CharacterObject character, ref bool __result)
        {
            _ = __result; // consume incoming value to satisfy analyzers

            if (character?.HeroObject == null)
            {
                __result = false;
                return false;
            }

            try
            {
                var playerParty = GetMobileParty(Hero.MainHero);
                var partnerParty = GetMobileParty(character.HeroObject);

                // Null party means settlement prisoner - can't be at sea
                if (playerParty == null || partnerParty == null)
                {
                    __result = false;
                    return false;
                }

                __result = playerParty.IsCurrentlyAtSea && partnerParty != playerParty;
                return false;
            }
            catch (System.Exception ex)
            {
                ModLogger.Caught("ConversationFix", "PlayerIsAtSeaTag safety catch", ex);
                __result = false;
                return false;
            }
        }

        private static MobileParty GetMobileParty(Hero hero)
        {
            if (hero == null)
            {
                return null;
            }

            return hero.IsPrisoner
                ? hero.PartyBelongedToAsPrisoner?.MobileParty
                : hero.PartyBelongedTo;
        }
    }
}
