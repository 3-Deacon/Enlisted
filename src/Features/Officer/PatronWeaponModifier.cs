using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Officer
{
    /// <summary>
    /// Applies the patron-named weapon modifier to the player's primary weapon at officer-tier
    /// transitions (T7-T9). Modifier StringIds are pre-registered at SubModule.OnGameStart by
    /// <see cref="OfficerGearRegistry"/>; this helper only does the lookup, the slot assignment,
    /// and the toast notification.
    /// </summary>
    public static class PatronWeaponModifier
    {
        /// <summary>
        /// Apply the tier-appropriate modifier to <see cref="Hero.MainHero"/>'s primary weapon.
        /// Replaces any existing modifier on the slot; safe to call repeatedly (idempotent at the
        /// modifier level — the same StringId always resolves to the same registered instance).
        /// </summary>
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

            var cultureId = lord.Culture?.StringId ?? "vlandia";
            var modifierStringId = OfficerGearRegistry.GetModifierStringId(cultureId, newTier);
            if (string.IsNullOrEmpty(modifierStringId))
            {
                return;
            }

            var modifier = MBObjectManager.Instance?.GetObject<ItemModifier>(modifierStringId);
            if (modifier == null)
            {
                ModLogger.Expected(
                    "OFFICER",
                    "patron_modifier_missing",
                    "patron weapon modifier not registered at promotion time",
                    LogCtx.Of("StringId", modifierStringId));
                return;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            // Set LORD_NAME globally so the modifier's Name TextObject ({LORD_NAME}'s Fine Steel)
            // resolves correctly when the inventory tooltip renders. EnlistedDialogManager refreshes
            // this on every conversation; we set it here so the toast that fires immediately below
            // also sees the right name.
            MBTextManager.SetTextVariable("LORD_NAME", lord.Name);

            try
            {
                var weapon = hero.BattleEquipment[EquipmentIndex.Weapon0];
                if (weapon.Item == null)
                {
                    ModLogger.Expected(
                        "OFFICER",
                        "patron_weapon_no_primary",
                        "main hero has no primary weapon at promotion");
                    return;
                }

                hero.BattleEquipment[EquipmentIndex.Weapon0] = new EquipmentElement(weapon.Item, modifier);

                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=officer_weapon_gifted}{LORD_NAME} has gifted you a finer blade.")
                        .SetTextVariable("LORD_NAME", lord.Name)
                        .ToString()));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "patron_weapon_apply_failed", ex);
            }
        }
    }
}
