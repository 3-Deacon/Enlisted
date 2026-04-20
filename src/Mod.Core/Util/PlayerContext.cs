using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Mod.Core.Util
{
    internal static class PlayerContext
    {
        public static bool IsPlayerContextActive()
        {
            if (Hero.OneToOneConversationHero != null)
            {
                return true;
            }

            if (MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement != null)
            {
                return true;
            }

            if (PlayerEncounter.Current != null)
            {
                return true;
            }

            return false;
        }
    }
}


