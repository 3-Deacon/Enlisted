using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Represents a single upgrade option card: one equipped item that can be upgraded to a specific quality.
    /// </summary>
    public class QuartermasterUpgradeCardVm : ViewModel
    {
        private readonly EquipmentIndex _slot;
        private readonly ItemObject _item;
        private readonly ItemModifier _currentModifier;
        private readonly ItemQuality _currentQuality;
        private readonly ItemQuality _targetQuality;
        private readonly QuartermasterUpgradeVm _parent;

        [DataSourceProperty]
        public string ItemName { get; private set; }

        [DataSourceProperty]
        public string SlotTypeText { get; private set; }

        [DataSourceProperty]
        public ItemImageIdentifierVM Image { get; private set; }

        [DataSourceProperty]
        public string CurrentQualityText { get; private set; }

        [DataSourceProperty]
        public string CurrentQualityColor { get; private set; }

        [DataSourceProperty]
        public string TargetQualityText { get; private set; }

        [DataSourceProperty]
        public string TargetQualityColor { get; private set; }

        [DataSourceProperty]
        public string StatsText { get; private set; }

        [DataSourceProperty]
        public string CostText { get; private set; }

        [DataSourceProperty]
        public string DiscountText { get; private set; }

        [DataSourceProperty]
        public bool HasDiscount { get; private set; }

        [DataSourceProperty]
        public string StatusText { get; private set; }

        [DataSourceProperty]
        public string TooltipText { get; private set; }

        [DataSourceProperty]
        public bool IsEnabled { get; private set; }

        [DataSourceProperty]
        public float CardAlpha { get; private set; }

        public QuartermasterUpgradeCardVm(
            EquipmentIndex slot,
            ItemObject item,
            ItemModifier currentModifier,
            ItemQuality currentQuality,
            ItemQuality targetQuality,
            QuartermasterUpgradeVm parent)
        {
            _slot = slot;
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _currentModifier = currentModifier;
            _currentQuality = currentQuality;
            _targetQuality = targetQuality;
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));

            Image = new ItemImageIdentifierVM(_item);
            RefreshValues();
        }

        public override void RefreshValues()
        {
            base.RefreshValues();

            try
            {
                ItemName = _item.Name?.ToString() ?? "Unknown";
                SlotTypeText = GetSlotTypeName(_slot);

                // Current quality
                CurrentQualityText = GetQualityName(_currentQuality);
                CurrentQualityColor = GetQualityColor(_currentQuality);

                // Target quality
                TargetQualityText = GetQualityName(_targetQuality);
                TargetQualityColor = GetQualityColor(_targetQuality);

                // Calculate actual upgrade cost using QuartermasterManager
                var qm = QuartermasterManager.Instance;
                var currentElement = new EquipmentElement(_item, _currentModifier);
                int cost = qm?.CalculateUpgradeCost(currentElement, _targetQuality) ?? 0;

                // No discounts displayed for now (cost already includes QM reputation markup)
                HasDiscount = false;
                DiscountText = "";
                CostText = $"{cost}⊕";

                var hero = Hero.MainHero;

                // Get actual stat improvements from modifiers
                StatsText = GetStatImprovements();

                // Check if player can afford
                bool canAfford = hero != null && hero.Gold >= cost;
                IsEnabled = canAfford;
                CardAlpha = canAfford ? 1.0f : 0.6f;

                StatusText = canAfford ? "" : "Insufficient gold";

                // Tooltip with actual improvements
                TooltipText = $"Upgrade {ItemName} from {CurrentQualityText} to {TargetQualityText}\n{StatsText}\nCost: {cost} denars";

                // Notify all properties
                OnPropertyChanged(nameof(ItemName));
                OnPropertyChanged(nameof(SlotTypeText));
                OnPropertyChanged(nameof(CurrentQualityText));
                OnPropertyChanged(nameof(CurrentQualityColor));
                OnPropertyChanged(nameof(TargetQualityText));
                OnPropertyChanged(nameof(TargetQualityColor));
                OnPropertyChanged(nameof(StatsText));
                OnPropertyChanged(nameof(CostText));
                OnPropertyChanged(nameof(DiscountText));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(TooltipText));
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(CardAlpha));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QuartermasterUI", $"Error refreshing upgrade card for {_item?.Name}", ex);
            }
        }

        /// <summary>
        /// Get the actual stat improvements from current to target quality modifiers.
        /// Shows armor, damage, speed improvements as they appear in game stats.
        /// </summary>
        private string GetStatImprovements()
        {
            try
            {
                var modGroup = _item.ItemComponent?.ItemModifierGroup;
                if (modGroup == null)
                {
                    return "";
                }

                // Get target modifier (first one in the quality tier)
                var targetModifiers = modGroup.GetModifiersBasedOnQuality(_targetQuality);
                if (targetModifiers == null || targetModifiers.Count == 0)
                {
                    return "";
                }

                var targetModifier = targetModifiers.FirstOrDefault();
                if (targetModifier == null)
                {
                    return "";
                }

                var improvements = new List<string>();

                // Armor improvements (for armor items)
                if (_item.ArmorComponent != null && targetModifier.Armor != 0)
                {
                    int armorDiff = targetModifier.Armor - (_currentModifier?.Armor ?? 0);
                    if (armorDiff != 0)
                    {
                        improvements.Add($"{(armorDiff > 0 ? "+" : "")}{armorDiff} Armor");
                    }
                }

                // Weapon improvements
                if (_item.WeaponComponent != null)
                {
                    // Damage improvements
                    if (targetModifier.Damage != 0)
                    {
                        int damageDiff = targetModifier.Damage - (_currentModifier?.Damage ?? 0);
                        if (damageDiff != 0)
                        {
                            improvements.Add($"{(damageDiff > 0 ? "+" : "")}{damageDiff} Damage");
                        }
                    }

                    // Speed improvements
                    if (targetModifier.Speed != 0)
                    {
                        int speedDiff = targetModifier.Speed - (_currentModifier?.Speed ?? 0);
                        if (speedDiff != 0)
                        {
                            improvements.Add($"{(speedDiff > 0 ? "+" : "")}{speedDiff} Speed");
                        }
                    }

                    // Missile speed improvements (for ranged weapons)
                    if (targetModifier.MissileSpeed != 0)
                    {
                        int missileSpeedDiff = targetModifier.MissileSpeed - (_currentModifier?.MissileSpeed ?? 0);
                        if (missileSpeedDiff != 0)
                        {
                            improvements.Add($"{(missileSpeedDiff > 0 ? "+" : "")}{missileSpeedDiff} Missile");
                        }
                    }
                }

                return improvements.Count > 0 ? string.Join(", ", improvements) : "Improved stats";
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QuartermasterUI", $"Error calculating stat improvements for {_item?.Name}", ex);
                return "Improved stats";
            }
        }

        public void ExecuteUpgrade()
        {
            if (!IsEnabled)
            {
                return;
            }

            _parent.OnUpgradePerformed(_slot, _targetQuality);
        }

        private static string GetSlotTypeName(EquipmentIndex slot)
        {
            return slot switch
            {
                EquipmentIndex.Weapon0 => "Main Weapon",
                EquipmentIndex.Weapon1 => "Secondary Weapon",
                EquipmentIndex.Weapon2 => "Tertiary Weapon",
                EquipmentIndex.Weapon3 => "Quaternary Weapon",
                EquipmentIndex.Head => "Helmet",
                EquipmentIndex.Body => "Armor",
                EquipmentIndex.Leg => "Boots",
                EquipmentIndex.Gloves => "Gloves",
                EquipmentIndex.Cape => "Cape",
                EquipmentIndex.Horse => "Mount",
                EquipmentIndex.HorseHarness => "Horse Armor",
                _ => slot.ToString()
            };
        }

        private static string GetQualityName(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Poor => "Poor",
                ItemQuality.Inferior => "Worn",
                ItemQuality.Common => "Standard",
                ItemQuality.Fine => "Fine",
                ItemQuality.Masterwork => "Masterwork",
                ItemQuality.Legendary => "Legendary",
                _ => quality.ToString()
            };
        }

        private static string GetQualityColor(ItemQuality quality)
        {
            return quality switch
            {
                ItemQuality.Poor => "#808080FF",        // Gray
                ItemQuality.Inferior => "#B0B0B0FF",    // Light Gray
                ItemQuality.Common => "#FFFFFFFF",      // White
                ItemQuality.Fine => "#90EE90FF",        // Light Green
                ItemQuality.Masterwork => "#4169E1FF",  // Royal Blue
                ItemQuality.Legendary => "#FFD700FF",   // Gold
                _ => "#FFFFFFFF"
            };
        }
    }
}
