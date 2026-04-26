using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.Officer
{
    /// <summary>
    /// Registers the officer-tier weapon modifiers and their owning <see cref="ItemModifierGroup"/>
    /// at module-bootstrap time. StringIds are static so save-loaded <c>Hero.BattleEquipment</c>
    /// references resolve idempotently across reloads. Personalization (lord name) flows through
    /// the modifier <c>Name</c> <see cref="TextObject"/>'s {LORD_NAME} token, set globally via
    /// <see cref="TaleWorlds.Core.MBTextManager.SetTextVariable(string, TextObject)"/>.
    /// </summary>
    public static class OfficerGearRegistry
    {
        public const string GroupId = "mod_lord_gifted";

        private static readonly string[] Cultures =
        {
            "vlandia", "sturgia", "empire", "aserai", "battania", "khuzait",
        };

        /// <summary>
        /// Idempotent. Safe to call from <c>SubModule.OnGameStart</c> on every game start
        /// (new game and save load). Lookups before registration return null; lookups after
        /// return the same instance regardless of reload.
        /// </summary>
        public static void Initialize()
        {
            var group = ItemModifierFactory.GetOrCreateGroup(GroupId);
            if (group == null)
            {
                return;
            }

            foreach (var culture in Cultures)
            {
                RegisterTier(culture, 7, damage: 3, speed: 1, priceMultiplier: 1.05f, group);
                RegisterTier(culture, 8, damage: 5, speed: 2, priceMultiplier: 1.10f, group);
                RegisterTier(culture, 9, damage: 8, speed: 3, priceMultiplier: 1.20f, group);
            }
        }

        /// <summary>
        /// Returns the static StringId for an officer modifier, or null if culture or tier are out of range.
        /// Used by <c>PatronWeaponModifier</c> at T7 commission to look up the pre-registered modifier.
        /// </summary>
        public static string GetModifierStringId(string cultureStringId, int tier)
        {
            if (string.IsNullOrEmpty(cultureStringId) || tier < 7 || tier > 9)
            {
                return null;
            }

            return $"lord_gifted_{cultureStringId}_t{tier}";
        }

        private static void RegisterTier(
            string culture,
            int tier,
            int damage,
            int speed,
            float priceMultiplier,
            ItemModifierGroup group)
        {
            var stringId = $"lord_gifted_{culture}_t{tier}";
            var name = new TextObject("{=officer_lord_gifted_modifier}{LORD_NAME}'s Fine Steel");

            ItemModifierFactory.GetOrCreate(
                stringId,
                name,
                damage,
                speed,
                armor: 0,
                priceMultiplier: priceMultiplier,
                quality: ItemQuality.Fine,
                group: group);
        }
    }
}
