using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Officer
{
    /// <summary>
    /// Tracks which officer-tier banner the player nominally carries via FlagStore keys
    /// (<c>officer_banner_t7</c> / <c>_t8</c> / <c>_t9</c>). v1 ships the banner as flag +
    /// toast + dialog-hook narrative — there is no banner ItemObject in vanilla we can reuse,
    /// and authoring new banner items with <c>BannerComponent</c> is deferred to Plan 7 polish
    /// if a real banner equip slot is wanted. Dialog branches reference the carried banner
    /// via the <c>BANNER_NAME</c> token populated by <see cref="GetCurrentBannerName"/>.
    /// </summary>
    public static class BannerProvision
    {
        public static void ApplyAtTierChange(int newTier)
        {
            if (newTier < 7 || newTier > 9)
            {
                return;
            }

            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            if (lord == null)
            {
                return;
            }

            var flags = FlagStore.Instance;
            if (flags == null)
            {
                return;
            }

            // Replace any prior banner flag with the new tier's flag — only one banner at a time.
            flags.Clear("officer_banner_t7");
            flags.Clear("officer_banner_t8");
            flags.Clear("officer_banner_t9");
            flags.Set($"officer_banner_t{newTier}", CampaignTime.Never);

            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=officer_banner_granted}{LORD_NAME} has granted you the {BANNER_NAME}.")
                        .SetTextVariable("LORD_NAME", lord.Name)
                        .SetTextVariable("BANNER_NAME", BannerNameForTier(newTier))
                        .ToString()));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "banner_grant_toast", ex);
            }
        }

        /// <summary>
        /// Returns the cosmetic display name of the banner the player is currently carrying,
        /// or null if no officer banner flag is set. Used by SetCommonDialogueVariables to
        /// populate the <c>BANNER_NAME</c> token for dialog branches.
        /// </summary>
        public static string GetCurrentBannerName()
        {
            var flags = FlagStore.Instance;
            if (flags == null)
            {
                return null;
            }

            for (int tier = 9; tier >= 7; tier--)
            {
                if (flags.Has($"officer_banner_t{tier}"))
                {
                    return BannerNameForTier(tier);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the active banner tier (7/8/9), or 0 if no officer banner flag is set.
        /// Used by the Inspect-tent menu to render a tier-aware trophy line.
        /// </summary>
        public static int GetCurrentBannerTier()
        {
            var flags = FlagStore.Instance;
            if (flags == null)
            {
                return 0;
            }

            for (int tier = 9; tier >= 7; tier--)
            {
                if (flags.Has($"officer_banner_t{tier}"))
                {
                    return tier;
                }
            }

            return 0;
        }

        private static string BannerNameForTier(int tier)
        {
            return tier switch
            {
                7 => new TextObject("{=officer_banner_t7_name}Cavalier's Standard").ToString(),
                8 => new TextObject("{=officer_banner_t8_name}Commander's Standard").ToString(),
                9 => new TextObject("{=officer_banner_t9_name}Marshal's Standard").ToString(),
                _ => null,
            };
        }
    }
}
