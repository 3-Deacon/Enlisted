using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Individual equipment item ViewModel for clickable selection.
    ///
    /// Represents a single equipment variant option with proper data binding.
    /// Uses TaleWorlds ViewModel APIs for data binding and property change notifications.
    /// </summary>
    public class QuartermasterEquipmentItemVm : ViewModel
    {
        // DataSourceProperty attributes enable data binding with the UI layer

        [DataSourceProperty]
        public string StatusText { get; private set; }

        [DataSourceProperty]
        public bool IsCurrentEquipment { get; private set; }

        [DataSourceProperty]
        public bool CanAfford { get; private set; }

        [DataSourceProperty]
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// Quantity available for purchase (Phase 7: Inventory & Pricing System).
        /// </summary>
        [DataSourceProperty]
        public int QuantityAvailable { get; private set; }

        /// <summary>
        /// Whether this item is available for purchase (has stock).
        /// </summary>
        [DataSourceProperty]
        public bool IsAvailable { get; private set; }

        /// <summary>
        /// Display text for quantity available (e.g., "Available: 3").
        /// </summary>
        [DataSourceProperty]
        public string QuantityText { get; private set; }

        /// <summary>
        /// Whether to show the quantity display (only show when quantity is limited).
        /// </summary>
        [DataSourceProperty]
        public bool ShowQuantity { get; private set; }

        /// <summary>
        /// Alpha factor for card (0.5 when unavailable, 1.0 when available).
        /// </summary>
        [DataSourceProperty]
        public float CardAlpha { get; private set; }

        [DataSourceProperty]
        public ItemImageIdentifierVM Image { get; private set; }

        [DataSourceProperty]
        public string ItemName { get; private set; }
        [DataSourceProperty] public string CostText { get; private set; }

        /// <summary>
        /// Primary stats line showing key stats for weapons/armor (displayed below image).
        /// </summary>
        [DataSourceProperty]
        public string PrimaryStats { get; private set; }

        /// <summary>
        /// Secondary stats line for additional details (tier, material type, etc).
        /// </summary>
        [DataSourceProperty]
        public string SecondaryStats { get; private set; }

        /// <summary>
        /// Slot type indicator (Head, Body, Weapon, etc).
        /// </summary>
        [DataSourceProperty]
        public string SlotTypeText { get; private set; }

        /// <summary>
        /// Quality tier display text (Poor, Worn, Standard, Fine, Masterwork, Legendary).
        /// Empty string if Common quality (no modifier).
        /// </summary>
        [DataSourceProperty]
        public string QualityText { get; private set; }

        /// <summary>
        /// Quality tier color code for UI styling.
        /// Empty string if Common quality (no modifier).
        /// </summary>
        [DataSourceProperty]
        public string QualityColor { get; private set; }

        /// <summary>
        /// Tooltip text showing base stats vs modified stats.
        /// Displays stat differences when quality modifier is applied.
        /// </summary>
        [DataSourceProperty]
        public string TooltipText { get; private set; }

        /// <summary>
        /// Whether this item can be upgraded (has modifier group and not at max quality).
        /// </summary>
        [DataSourceProperty]
        public bool IsUpgradeable { get; private set; }

        /// <summary>
        /// Display text for upgrade indicator (e.g., "UPGRADE AVAILABLE").
        /// Empty string if not upgradeable.
        /// </summary>
        [DataSourceProperty]
        public string UpgradeIndicatorText { get; private set; }

        // Legacy property for backwards compatibility
        [DataSourceProperty]
        public string WeaponDetails { get; private set; }

        // Internal data (readonly as set only in constructor)
        private readonly EquipmentVariantOption _variant;
        private readonly QuartermasterEquipmentSelectorVm _parentSelector;

        /// <summary>
        /// Get the underlying variant option for state updates.
        /// </summary>
        public EquipmentVariantOption GetVariant() => _variant;

        /// <summary>
        /// Initialize equipment item with variant data.
        /// Sets up the ViewModel with equipment variant information for display and selection.
        /// </summary>
        public QuartermasterEquipmentItemVm(EquipmentVariantOption variant, QuartermasterEquipmentSelectorVm parent)
        {
            _variant = variant; // Allow null for empty slot display
            _parentSelector = parent ?? throw new ArgumentNullException(nameof(parent));

            // Note: RefreshValues() should be called by the parent after construction
            // to avoid virtual member call in constructor (ReSharper warning)
        }

        /// <summary>
        /// Refresh display values when equipment variant data changes.
        /// </summary>
        public override void RefreshValues()
        {
            base.RefreshValues();

            try
            {
                if (_variant?.Item == null)
                {
                    SetEmptyValues();
                    return;
                }

                // Build item details for display, include price
                var item = _variant.Item;
                CostText = $"Price: {_variant.Cost} denars";

                // Phase 7: Set availability properties
                QuantityAvailable = _variant.QuantityAvailable;
                IsAvailable = _variant.QuantityAvailable > 0;

                // Show quantity if limited (< 999 means actual tracked quantity)
                ShowQuantity = QuantityAvailable < 999;
                if (ShowQuantity)
                {
                    QuantityText = QuantityAvailable > 0
                        ? $"Available: {QuantityAvailable}"
                        : "Out of Stock";
                }
                else
                {
                    QuantityText = "";
                }

                // Grey out unavailable items (Alpha = 0.5)
                CardAlpha = IsAvailable ? 1.0f : 0.5f;

                // Set item name with quality modifier if present
                var baseName = item.Name?.ToString() ?? "Unknown Item";

                // Apply quality modifier name if present
                if (!string.IsNullOrEmpty(_variant.ModifiedName))
                {
                    baseName = _variant.ModifiedName;
                }

                // Truncate very long names to prevent overlap (max 45 characters)
                if (baseName.Length > 45)
                {
                    baseName = baseName.Substring(0, 42) + "...";
                }

                // Add "NEW" indicator for recently unlocked items
                ItemName = _variant.IsNewlyUnlocked ? $"[NEW] {baseName}" : baseName;
                IsCurrentEquipment = _variant.IsCurrent;
                CanAfford = _variant.CanAfford;

                // Determine slot type for enable/disable behavior (weapons can be bought repeatedly).
                var isWeaponSlot = _variant.Slot is >= EquipmentIndex.Weapon0 and <= EquipmentIndex.Weapon3;

                // Set item image using ItemImageIdentifierVM for proper image display (1.3.4 API)
                Image = new ItemImageIdentifierVM(item);

                // Build equipment stats based on item type (weapon, armor, or accessory)
                BuildEquipmentStats(item);

                // Set quality display based on modifier
                SetQualityDisplay(_variant.Modifier);

                // Build tooltip with base vs modified stats comparison
                BuildTooltipText(item, _variant.Modifier);

                // Check if item is upgradeable
                SetUpgradeIndicator(item, _variant.Quality);

                // Legacy support - combine stats into WeaponDetails for backwards compatibility
                WeaponDetails = $"{PrimaryStats}\n{SecondaryStats}";

                // Purchase-based status (no issue limits / no accountability).
                // Phase 7: Show out of stock status
                if (QuantityAvailable <= 0)
                {
                    StatusText = new TextObject("{=qm_status_out_of_stock}(Out of Stock)").ToString();
                }
                else if (_variant.IsCurrent)
                {
                    StatusText = new TextObject("{=qm_status_equipped}Equipped").ToString();
                }
                else if (!_variant.CanAfford)
                {
                    StatusText = "Insufficient funds";
                }
                else
                {
                    StatusText = new TextObject("{=qm_status_available}Available").ToString();
                }

                // Disable buying the currently-equipped non-weapon item (no-op purchase).
                // Weapons can be purchased repeatedly (another copy goes to an empty weapon slot or inventory).
                // Phase 7: Also disable if out of stock
                IsEnabled = _variant.CanAfford && (isWeaponSlot || !_variant.IsCurrent) && IsAvailable;

                // Notify UI of property changes for data binding updates
                OnPropertyChanged(nameof(ItemName));
                OnPropertyChanged(nameof(CostText));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsCurrentEquipment));
                OnPropertyChanged(nameof(CanAfford));
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(QuantityAvailable));
                OnPropertyChanged(nameof(IsAvailable));
                OnPropertyChanged(nameof(QuantityText));
                OnPropertyChanged(nameof(ShowQuantity));
                OnPropertyChanged(nameof(CardAlpha));
                OnPropertyChanged(nameof(Image));
                OnPropertyChanged(nameof(PrimaryStats));
                OnPropertyChanged(nameof(SecondaryStats));
                OnPropertyChanged(nameof(SlotTypeText));
                OnPropertyChanged(nameof(QualityText));
                OnPropertyChanged(nameof(QualityColor));
                OnPropertyChanged(nameof(TooltipText));
                OnPropertyChanged(nameof(IsUpgradeable));
                OnPropertyChanged(nameof(UpgradeIndicatorText));
                OnPropertyChanged(nameof(WeaponDetails));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error refreshing equipment item values", ex);
                SetEmptyValues();
            }
        }

        /// <summary>
        /// Set safe fallback values for error cases or empty slots.
        /// </summary>
        private void SetEmptyValues()
        {
            if (_variant == null)
            {
                // Empty slot display
                ItemName = new TextObject("{=qm_ui_empty}Empty").ToString();
                CostText = "";
                StatusText = new TextObject("{=qm_ui_empty_slot_label}Empty Slot").ToString();
                PrimaryStats = "";
                SecondaryStats = "";
                SlotTypeText = "";
                QualityText = "";
                QualityColor = "";
                WeaponDetails = "";
                Image = new ItemImageIdentifierVM(null);
            }
            else
            {
                // Error case
                ItemName = new TextObject("{=qm_ui_error}Error").ToString();
                CostText = "";
                StatusText = new TextObject("{=qm_ui_error_loading_item}Error loading item").ToString();
                PrimaryStats = "";
                SecondaryStats = "";
                SlotTypeText = "";
                QualityText = "";
                QualityColor = "";
                TooltipText = "";
                IsUpgradeable = false;
                UpgradeIndicatorText = "";
                WeaponDetails = "";
                Image = new ItemImageIdentifierVM(null);
            }
            IsCurrentEquipment = false;
            CanAfford = false;
            IsEnabled = false;
        }

        /// <summary>
        /// Handle clicking on this equipment item (main functionality).
        /// Executes when the player clicks on an equipment item in the UI.
        /// </summary>
        [UsedImplicitly("Bound via Gauntlet XML: Command.Click")]
        public void ExecuteSelectItem()
        {
            try
            {
                if (_variant == null)
                {
                    // Empty slot - unequip current equipment
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_ui_unequip_not_supported}Equipment unequipping not yet supported.").ToString()));
                    return;
                }

                if (_variant.Item == null)
                {
                    ModLogger.Expected("QUARTERMASTERUI", "equip_item_null", "Cannot select item - variant item is null");
                    return;
                }

                // Block purchase when the player can't afford it.
                if (!_variant.CanAfford)
                {
                    var msg = new TextObject("{=qm_cannot_afford}You can’t afford this. Cost: {COST} denars.");
                    msg.SetTextVariable("COST", _variant.Cost);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
                    return;
                }

                // Apply selection through parent
                _parentSelector?.OnEquipmentItemSelected(_variant);

                ModLogger.Info("QUARTERMASTERUI", $"Equipment purchased: {_variant.Item.Name} ({_variant.Cost} denars)");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error selecting equipment item", ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=qm_error_selecting}Error selecting equipment. Please try again.").ToString()));
            }
        }

        /// <summary>
        /// Handle previewing this equipment item.
        /// </summary>
        [UsedImplicitly("Bound via Gauntlet XML: Command.Click")]
        public void ExecutePreviewItem()
        {
            try
            {
                if (_variant?.Item != null)
                {
                    var msg = new TextObject("{=qm_ui_preview}Preview: {ITEM_NAME} - {COST} denars");
                    msg.SetTextVariable("ITEM_NAME", _variant.Item.Name);
                    msg.SetTextVariable("COST", _variant.Cost);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
                }
                else
                {
                    // Empty slot or null item
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_ui_empty_equipment_slot}Empty equipment slot").ToString()));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error previewing equipment item", ex);
            }
        }

        /// <summary>
        /// Build equipment stats based on item type (weapon, armor, shield, etc).
        /// Sets PrimaryStats, SecondaryStats, and SlotTypeText properties.
        /// </summary>
        private void BuildEquipmentStats(ItemObject item)
        {
            try
            {
                if (item == null)
                {
                    PrimaryStats = "";
                    SecondaryStats = "";
                    SlotTypeText = "";
                    return;
                }

                // Determine slot type display name
                SlotTypeText = GetSlotTypeName(_variant.Slot);

                // Build stats based on item component type
                if (item.ArmorComponent != null)
                {
                    BuildArmorStats(item);
                }
                else if (item.WeaponComponent != null)
                {
                    BuildWeaponStats(item);
                }
                else
                {
                    // Generic item (banner, horse, etc)
                    var tierNumber = ((int)item.Tier) + 1; // Tier enum is 0-based
                    PrimaryStats = $"Tier {tierNumber}";
                    SecondaryStats = item.Culture?.Name?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error building equipment stats", ex);
                PrimaryStats = "";
                SecondaryStats = "Stats unavailable";
            }
        }

        /// <summary>
        /// Build armor-specific stats for display.
        /// Shows armor values and material type.
        /// </summary>
        private void BuildArmorStats(ItemObject item)
        {
            var armor = item.ArmorComponent;
            var statParts = new List<string>();

            // Show relevant armor values based on slot type
            switch (_variant.Slot)
            {
                case EquipmentIndex.Head:
                    if (armor.HeadArmor > 0)
                    {
                        statParts.Add($"Head: {armor.HeadArmor}");
                    }
                    break;

                case EquipmentIndex.Body:
                    if (armor.BodyArmor > 0)
                    {
                        statParts.Add($"Body: {armor.BodyArmor}");
                    }
                    if (armor.ArmArmor > 0)
                    {
                        statParts.Add($"Arm: {armor.ArmArmor}");
                    }
                    if (armor.LegArmor > 0)
                    {
                        statParts.Add($"Leg: {armor.LegArmor}");
                    }
                    break;

                case EquipmentIndex.Leg:
                    if (armor.LegArmor > 0)
                    {
                        statParts.Add($"Leg: {armor.LegArmor}");
                    }
                    break;

                case EquipmentIndex.Gloves:
                    if (armor.ArmArmor > 0)
                    {
                        statParts.Add($"Arm: {armor.ArmArmor}");
                    }
                    break;

                case EquipmentIndex.Cape:
                    // Capes typically have body armor bonus
                    if (armor.BodyArmor > 0)
                    {
                        statParts.Add($"Body: {armor.BodyArmor}");
                    }
                    break;

                default:
                    // Show all non-zero armor values
                    if (armor.HeadArmor > 0)
                    {
                        statParts.Add($"H:{armor.HeadArmor}");
                    }
                    if (armor.BodyArmor > 0)
                    {
                        statParts.Add($"B:{armor.BodyArmor}");
                    }
                    if (armor.ArmArmor > 0)
                    {
                        statParts.Add($"A:{armor.ArmArmor}");
                    }
                    if (armor.LegArmor > 0)
                    {
                        statParts.Add($"L:{armor.LegArmor}");
                    }
                    break;
            }

            // Set primary stats (armor values)
            PrimaryStats = statParts.Count > 0 ? string.Join(" | ", statParts) : "No armor bonus";

            // Set secondary stats (material and tier)
            // Note: item.Tier is an enum (Tier1, Tier2, etc.) - extract just the number
            var tierNumber = ((int)item.Tier) + 1; // Tier enum is 0-based
            var materialName = armor.MaterialType.ToString();
            SecondaryStats = $"Tier {tierNumber} • {materialName}";
        }

        /// <summary>
        /// Build weapon-specific stats for display.
        /// Shows damage, speed, and handling.
        /// </summary>
        private void BuildWeaponStats(ItemObject item)
        {
            var weapon = item.WeaponComponent.PrimaryWeapon;
            var statParts = new List<string>();

            // Damage stats - show most relevant based on weapon type
            if (weapon.IsShield)
            {
                // Shield stats
                statParts.Add($"HP: {weapon.MaxDataValue}");
                if (weapon.BodyArmor > 0)
                {
                    statParts.Add($"Armor: {weapon.BodyArmor}");
                }
            }
            else if (weapon.IsRangedWeapon && weapon.IsConsumable)
            {
                // Throwing weapons or arrows
                if (weapon.MissileDamage > 0)
                {
                    statParts.Add($"Dmg: {weapon.MissileDamage}");
                }
                statParts.Add($"Stack: {weapon.MaxDataValue}");
            }
            else if (weapon.IsRangedWeapon)
            {
                // Bows/crossbows
                if (weapon.MissileSpeed > 0)
                {
                    statParts.Add($"Speed: {weapon.MissileSpeed}");
                }
                if (weapon.ThrustDamage > 0)
                {
                    statParts.Add($"Dmg: {weapon.ThrustDamage}");
                }
            }
            else
            {
                // Melee weapons - show primary damage type
                if (weapon.SwingDamage > 0)
                {
                    statParts.Add($"Swing: {weapon.SwingDamage}");
                }
                if (weapon.ThrustDamage > 0)
                {
                    statParts.Add($"Thrust: {weapon.ThrustDamage}");
                }
            }

            // Add length for melee weapons
            if (weapon.WeaponLength > 0 && !weapon.IsRangedWeapon)
            {
                statParts.Add($"Reach: {weapon.WeaponLength}");
            }

            PrimaryStats = statParts.Count > 0 ? string.Join(" | ", statParts) : "No stats";

            // Secondary stats: handling and tier
            // Note: item.Tier is an enum (Tier1, Tier2, etc.) - extract just the number
            var tierNumber = ((int)item.Tier) + 1; // Tier enum is 0-based
            var handlingText = weapon.Handling > 0 ? $"Handling: {weapon.Handling}" : "";
            SecondaryStats = $"Tier {tierNumber}" + (handlingText.Length > 0 ? $" • {handlingText}" : "");
        }

        /// <summary>
        /// Sets quality display properties based on the item quality tier.
        /// </summary>
        private void SetQualityDisplay(ItemModifier modifier)
        {
            if (modifier == null || _variant.Quality == ItemQuality.Common)
            {
                // Common quality (no modifier) - no quality display
                QualityText = "";
                QualityColor = "";
                return;
            }

            var quality = _variant.Quality;

            // Set quality text using localized strings
            QualityText = quality switch
            {
                ItemQuality.Poor => new TextObject("{=qm_quality_poor}Poor").ToString(),
                ItemQuality.Inferior => new TextObject("{=qm_quality_inferior}Worn").ToString(),
                ItemQuality.Common => new TextObject("{=qm_quality_common}Standard").ToString(),
                ItemQuality.Fine => new TextObject("{=qm_quality_fine}Fine").ToString(),
                ItemQuality.Masterwork => new TextObject("{=qm_quality_masterwork}Masterwork").ToString(),
                ItemQuality.Legendary => new TextObject("{=qm_quality_legendary}Legendary").ToString(),
                _ => ""
            };

            // Set quality color (ARGB hex codes for Gauntlet UI - 8 digits with alpha channel)
            // Using softer colors for better readability and reduced eye strain
            QualityColor = quality switch
            {
                ItemQuality.Poor => "#909090FF",        // Light gray (more visible than dark gray)
                ItemQuality.Inferior => "#CD853FFF",    // Peru/tan (lighter brown, more readable)
                ItemQuality.Common => "#E8E8E8FF",      // Off-white (softer than pure white)
                ItemQuality.Fine => "#90EE90FF",        // Light green (softer than lime)
                ItemQuality.Masterwork => "#6495EDFF",  // Cornflower blue (lighter, more readable)
                ItemQuality.Legendary => "#FFD700FF",   // Gold (keeping as-is, already good)
                _ => "#E8E8E8FF"
            };
        }

        /// <summary>
        /// Get human-readable slot type name for display.
        /// </summary>
        private static string GetSlotTypeName(EquipmentIndex slot)
        {
            return slot switch
            {
                EquipmentIndex.Head => "Helmet",
                EquipmentIndex.Body => "Body Armor",
                EquipmentIndex.Leg => "Leg Armor",
                EquipmentIndex.Gloves => "Gloves",
                EquipmentIndex.Cape => "Cape",
                EquipmentIndex.Weapon0 => "Weapon 1",
                EquipmentIndex.Weapon1 => "Weapon 2",
                EquipmentIndex.Weapon2 => "Weapon 3",
                EquipmentIndex.Weapon3 => "Weapon 4",
                EquipmentIndex.Horse => "Mount",
                EquipmentIndex.HorseHarness => "Horse Armor",
                _ => "Equipment"
            };
        }

        /// <summary>
        /// Build tooltip text showing base stats vs modified stats.
        /// Displays stat differences when quality modifier is applied.
        /// </summary>
        private void BuildTooltipText(ItemObject item, ItemModifier modifier)
        {
            try
            {
                if (item == null)
                {
                    TooltipText = "";
                    return;
                }

                // If no modifier, just show basic item info
                if (modifier == null || _variant.Quality == ItemQuality.Common)
                {
                    TooltipText = $"{item.Name?.ToString() ?? "Unknown"}\n{item.ItemType}";
                    return;
                }

                var tooltipParts = new List<string>();

                // Add item name (with fallback if both ModifiedName and item.Name are null)
                var displayName = _variant.ModifiedName ?? item.Name?.ToString() ?? "Unknown Item";
                tooltipParts.Add(displayName);

                // Add quality tier (with fallback)
                if (!string.IsNullOrEmpty(QualityText))
                {
                    tooltipParts.Add($"Quality: {QualityText}");
                }

                // Add modifier effects based on item type
                if (item.WeaponComponent != null)
                {
                    BuildWeaponTooltip(item, modifier, tooltipParts);
                }
                else if (item.ArmorComponent != null)
                {
                    BuildArmorTooltip(item, modifier, tooltipParts);
                }

                // Add price multiplier info with clearer text for large reductions
                var priceMultiplier = modifier.PriceMultiplier;
                if (Math.Abs(priceMultiplier - 1.0f) > 0.01f)
                {
                    var percentChange = (int)((priceMultiplier - 1.0f) * 100);

                    // Use clearer wording for very low quality items
                    if (percentChange <= -75)
                    {
                        tooltipParts.Add($"Value: {(int)(priceMultiplier * 100)}% of normal");
                    }
                    else
                    {
                        tooltipParts.Add($"Value: {(percentChange > 0 ? "+" : "")}{percentChange}%");
                    }
                }

                TooltipText = string.Join("\n", tooltipParts);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error building tooltip text", ex);
                TooltipText = item?.Name?.ToString() ?? "";
            }
        }

        /// <summary>
        /// Build weapon-specific tooltip showing damage/speed modifiers.
        /// </summary>
        private void BuildWeaponTooltip(ItemObject item, ItemModifier modifier, List<string> tooltipParts)
        {
            if (item?.WeaponComponent == null || modifier == null || tooltipParts == null)
            {
                return;
            }

            var weapon = item.WeaponComponent.PrimaryWeapon;
            if (weapon == null)
            {
                return;
            }

            // Show damage modifier
            if (modifier.Damage != 0)
            {
                tooltipParts.Add($"Damage: {(modifier.Damage > 0 ? "+" : "")}{modifier.Damage}");
            }

            // Show speed modifier
            if (modifier.Speed != 0)
            {
                tooltipParts.Add($"Speed: {(modifier.Speed > 0 ? "+" : "")}{modifier.Speed}");
            }

            // Show missile speed modifier for ranged weapons
            if (weapon.IsRangedWeapon && modifier.MissileSpeed != 0)
            {
                tooltipParts.Add($"Missile Speed: {(modifier.MissileSpeed > 0 ? "+" : "")}{modifier.MissileSpeed}");
            }
        }

        /// <summary>
        /// Build armor-specific tooltip showing armor value modifiers.
        /// </summary>
        private void BuildArmorTooltip(ItemObject item, ItemModifier modifier, List<string> tooltipParts)
        {
            if (item?.ArmorComponent == null || modifier == null || tooltipParts == null)
            {
                return;
            }

            if (modifier.Armor != 0)
            {
                tooltipParts.Add($"Armor: {(modifier.Armor > 0 ? "+" : "")}{modifier.Armor}");
            }
        }

        /// <summary>
        /// Set upgrade indicator based on whether item can be upgraded.
        /// Items with modifier groups and not at max quality show upgrade indicator.
        /// </summary>
        private void SetUpgradeIndicator(ItemObject item, ItemQuality currentQuality)
        {
            try
            {
                if (item == null)
                {
                    IsUpgradeable = false;
                    UpgradeIndicatorText = "";
                    return;
                }

                // Check if item has modifier group (required for upgrades)
                var modGroup = item.ItemComponent?.ItemModifierGroup;
                if (modGroup == null)
                {
                    IsUpgradeable = false;
                    UpgradeIndicatorText = "";
                    return;
                }

                // Check if already at max quality
                if (currentQuality == ItemQuality.Legendary)
                {
                    IsUpgradeable = false;
                    UpgradeIndicatorText = "";
                    return;
                }

                // Check if any higher quality tiers exist
                var hasUpgrades = false;
                var qualityTiers = new[] { ItemQuality.Fine, ItemQuality.Masterwork, ItemQuality.Legendary };

                foreach (var tier in qualityTiers)
                {
                    if (tier > currentQuality)
                    {
                        var modifiers = modGroup.GetModifiersBasedOnQuality(tier);
                        if (modifiers != null && modifiers.Count > 0)
                        {
                            hasUpgrades = true;
                            break;
                        }
                    }
                }

                IsUpgradeable = hasUpgrades;
                UpgradeIndicatorText = hasUpgrades ? new TextObject("{=qm_ui_upgrade_available}UPGRADE").ToString() : "";
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error setting upgrade indicator", ex);
                IsUpgradeable = false;
                UpgradeIndicatorText = "";
            }
        }
    }
}
