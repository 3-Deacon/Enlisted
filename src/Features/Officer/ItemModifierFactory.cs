using System;
using System.Reflection;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Officer
{
    /// <summary>
    /// Constructs and registers <see cref="ItemModifier"/> instances at runtime via property-setter reflection.
    /// Vanilla XML modifiers register at module load; this helper lets the mod register additional modifiers
    /// using static StringIds that survive save-load round-trips.
    /// </summary>
    public static class ItemModifierFactory
    {
        /// <summary>
        /// Idempotent: returns the existing modifier if a modifier with this StringId is already registered;
        /// otherwise creates, registers, and adds to the supplied group. Safe to call from a repeated session
        /// boot path (the registry persists for the module-load lifetime, not just one campaign session).
        /// </summary>
        public static ItemModifier GetOrCreate(
            string stringId,
            TextObject name,
            int damage,
            int speed,
            int armor,
            float priceMultiplier,
            ItemQuality quality,
            ItemModifierGroup group)
        {
            var manager = MBObjectManager.Instance;
            if (manager == null)
            {
                ModLogger.Surfaced(
                    "OFFICER",
                    "modifier_manager_unavailable",
                    new InvalidOperationException("MBObjectManager.Instance is null at modifier registration"));
                return null;
            }

            var existing = manager.GetObject<ItemModifier>(stringId);
            if (existing != null)
            {
                return existing;
            }

            var mod = new ItemModifier
            {
                StringId = stringId,
            };

            SetProperty(mod, nameof(ItemModifier.Name), name);
            SetProperty(mod, nameof(ItemModifier.Damage), damage);
            SetProperty(mod, nameof(ItemModifier.Speed), speed);
            SetProperty(mod, nameof(ItemModifier.Armor), armor);
            SetProperty(mod, nameof(ItemModifier.PriceMultiplier), priceMultiplier);
            SetProperty(mod, nameof(ItemModifier.ItemQuality), quality);

            try
            {
                manager.RegisterObject(mod);
                group?.AddItemModifier(mod);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("OFFICER", "modifier_register_failed", ex, LogCtx.Of("StringId", stringId));
                return null;
            }

            return mod;
        }

        /// <summary>
        /// Returns the existing <see cref="ItemModifierGroup"/> with this StringId, or creates and registers
        /// a new one. Idempotent across reloads.
        /// </summary>
        public static ItemModifierGroup GetOrCreateGroup(string stringId)
        {
            var manager = MBObjectManager.Instance;
            if (manager == null)
            {
                ModLogger.Surfaced(
                    "OFFICER",
                    "group_manager_unavailable",
                    new InvalidOperationException("MBObjectManager.Instance is null at group registration"));
                return null;
            }

            var existing = manager.GetObject<ItemModifierGroup>(stringId);
            if (existing != null)
            {
                return existing;
            }

            try
            {
                var group = new ItemModifierGroup(stringId);
                manager.RegisterObject(group);
                return group;
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("OFFICER", "group_register_failed", ex, LogCtx.Of("StringId", stringId));
                return null;
            }
        }

        private static void SetProperty(object instance, string propertyName, object value)
        {
            try
            {
                var prop = instance.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop == null)
                {
                    ModLogger.Surfaced(
                        "OFFICER",
                        "modifier_property_missing",
                        new InvalidOperationException($"Property {propertyName} not found on ItemModifier"));
                    return;
                }

                var setter = prop.GetSetMethod(nonPublic: true);
                if (setter == null)
                {
                    ModLogger.Surfaced(
                        "OFFICER",
                        "modifier_setter_missing",
                        new InvalidOperationException($"No accessible setter for {propertyName} on ItemModifier"));
                    return;
                }

                setter.Invoke(instance, new[] { value });
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "modifier_property_set_exception", ex, LogCtx.Of("Property", propertyName));
            }
        }
    }
}
