using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using static TaleWorlds.Core.ViewModelCollection.CharacterViewModel;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Equipment selector main ViewModel for grid-based equipment selection.
    ///
    /// Provides individual clickable equipment items displayed in a grid layout.
    /// Uses TaleWorlds ViewModel patterns for data binding and UI updates.
    /// </summary>
    public class QuartermasterEquipmentSelectorVm : ViewModel
    {
        // Row-based organization for ListPanel with ItemTemplate in grid layout
        [DataSourceProperty]
        public MBBindingList<QuartermasterEquipmentRowVm> EquipmentRows { get; }

        [DataSourceProperty]
        public string HeaderText { get; private set; }

        [DataSourceProperty]
        public string CurrentEquipmentText { get; private set; }

        [DataSourceProperty]
        public string PlayerGoldText { get; private set; }

        // CharacterViewModel for displaying character preview with equipment (set only during construction)
        [DataSourceProperty]
        public CharacterViewModel UnitCharacter { get; }

        /// <summary>
        /// Initialize equipment selector with available variants.
        /// Sets up the ViewModel with equipment data and organizes items into rows for grid display.
        /// </summary>
        public QuartermasterEquipmentSelectorVm(List<EquipmentVariantOption> availableVariants, EquipmentIndex targetSlot, string equipmentType)
        {
            if (availableVariants == null)
            {
                throw new ArgumentNullException(nameof(availableVariants));
            }
            _ = targetSlot;

            HeaderText = GetHeaderText(equipmentType);
            CurrentEquipmentText = "Your Current Equipment";
            PlayerGoldText = GetPlayerGoldText(Hero.MainHero);

            // Organize equipment items into rows with 4 cards per row for grid display
            EquipmentRows = new MBBindingList<QuartermasterEquipmentRowVm>();
            var currentCards = new MBBindingList<QuartermasterEquipmentItemVm>();

            // Add equipment variants organized into rows of 4 cards each
            foreach (var variant in availableVariants.Take(15)) // Reasonable limit for 4 rows
            {
                currentCards.Add(new QuartermasterEquipmentItemVm(variant, this));

                // Create new row when we reach 4 cards in the current row
                if (currentCards.Count == 4)
                {
                    EquipmentRows.Add(new QuartermasterEquipmentRowVm(currentCards));
                    currentCards = new MBBindingList<QuartermasterEquipmentItemVm>();
                }
            }

            // Add remaining cards as final row if any remain
            if (currentCards.Count > 0)
            {
                EquipmentRows.Add(new QuartermasterEquipmentRowVm(currentCards));
            }

            // Set up CharacterViewModel for character preview display
            CharacterViewModel unitCharacter = null;
            try
            {
                unitCharacter = new CharacterViewModel(StanceTypes.None);
                unitCharacter.FillFrom(Hero.MainHero.CharacterObject); // Fill character model for preview, omit default seed param
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error setting up character view model", ex);
            }
            UnitCharacter = unitCharacter;

            // Note: RefreshValues() deferred until after construction to avoid virtual member call in constructor
        }

        /// <summary>
        /// Refresh all display values when equipment data changes.
        /// Recalculates IsAtLimit for all variants based on current player inventory.
        /// Also updates the player character model to show new equipment.
        /// </summary>
        public override void RefreshValues()
        {
            base.RefreshValues();

            try
            {
                var hero = Hero.MainHero;

                // Recalculate ALL variant states based on current gold/equipment.
                // This ensures buttons enable/disable correctly after purchases.
                RecalculateAllVariantStates(hero);
                PlayerGoldText = GetPlayerGoldText(hero);

                // Refresh the character model to show updated equipment
                RefreshCharacterModel(hero);

                // Refresh all equipment rows and their cards to update display
                foreach (var row in EquipmentRows)
                {
                    foreach (var card in row.Cards)
                    {
                        card.RefreshValues();
                    }
                }

                OnPropertyChanged(nameof(HeaderText));
                OnPropertyChanged(nameof(CurrentEquipmentText));
                OnPropertyChanged(nameof(PlayerGoldText));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error refreshing equipment selector values", ex);
            }
        }

        /// <summary>
        /// Refresh the character model to show updated equipment.
        /// Called after equipment changes to update the player preview in real-time.
        /// </summary>
        private void RefreshCharacterModel(Hero hero)
        {
            try
            {
                if (UnitCharacter != null && hero?.CharacterObject != null)
                {
                    // Re-fill the character model to reflect current equipment
                    UnitCharacter.FillFrom(hero.CharacterObject);
                    OnPropertyChanged(nameof(UnitCharacter));
                    ModLogger.Debug("QUARTERMASTERUI", "Player model refreshed to show updated equipment");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error refreshing character model", ex);
            }
        }

        /// <summary>
        /// Recalculate IsAtLimit and IsCurrent for ALL variants based on current player inventory.
        /// Called on every refresh to ensure buttons grey/ungrey correctly in real-time.
        /// </summary>
        private void RecalculateAllVariantStates(Hero hero)
        {
            try
            {
                var cardsUpdated = 0;

                foreach (var row in EquipmentRows)
                {
                    foreach (var card in row.Cards)
                    {
                        var variant = card.GetVariant();
                        if (variant?.Item == null)
                        {
                            continue;
                        }

                        // No issue limits / no accountability in purchase-based Quartermaster.
                        variant.IsAtLimit = false;

                        // Update IsCurrent based on what's actually equipped now (slot-based).
                        variant.IsCurrent = IsEquipped(hero, variant.Item, variant.Slot);

                        // Purchase affordability.
                        variant.CanAfford = hero.Gold >= variant.Cost;

                        cardsUpdated++;
                    }
                }

                ModLogger.Debug("QUARTERMASTERUI", $"Quartermaster refresh: {cardsUpdated} cards updated");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error recalculating variant states", ex);
            }
        }

        /// <summary>
        /// Handle equipment item selection from child ViewModels.
        /// Menu stays open so player can requisition multiple items without reopening.
        /// </summary>
        public void OnEquipmentItemSelected(EquipmentVariantOption selectedVariant)
        {
            try
            {
                if (selectedVariant?.Item == null)
                {
                    ModLogger.Expected("QUARTERMASTERUI", "equip_variant_null", "Cannot select equipment - variant or item is null");
                    return;
                }

                // Apply equipment through existing QuartermasterManager (priced purchase)
                var quartermasterManager = QuartermasterManager.Instance;
                if (quartermasterManager != null)
                {
                    quartermasterManager.RequestEquipmentVariant(selectedVariant);
                }

                // Refresh the UI to reflect the acquisition (update item counts, limits, etc.)
                // Menu stays OPEN so player can continue selecting multiple items
                RefreshValues();

                ModLogger.Info("QUARTERMASTERUI", $"Applied equipment variant: {selectedVariant.Item.Name}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error applying selected equipment", ex);
            }
        }

        private static string GetHeaderText(string equipmentType)
        {
            return equipmentType switch
            {
                "weapons" => "Quartermaster Weapons",
                "armor" => "Quartermaster Armor",
                "accessories" => "Quartermaster Accessories",
                "mounts" => "Quartermaster Mounts",
                "harness" => "Quartermaster Harness",
                _ => "Quartermaster Equipment"
            };
        }

        private static string GetPlayerGoldText(Hero hero)
        {
            return hero != null ? $"Your Gold: {hero.Gold} denars" : "Gold unavailable";
        }

        private static bool IsEquipped(Hero hero, ItemObject item, EquipmentIndex slot)
        {
            if (hero == null || item == null)
            {
                return false;
            }

            if (slot >= EquipmentIndex.Weapon0 && slot <= EquipmentIndex.Weapon3)
            {
                for (var weaponSlot = EquipmentIndex.Weapon0; weaponSlot <= EquipmentIndex.Weapon3; weaponSlot++)
                {
                    if (hero.BattleEquipment[weaponSlot].Item == item)
                    {
                        return true;
                    }
                }

                return false;
            }

            return hero.BattleEquipment[slot].Item == item;
        }

        /// <summary>
        /// Close the equipment selector when player cancels or completes selection.
        /// </summary>
        public void ExecuteClose()
        {
            try
            {
                QuartermasterEquipmentSelectorBehavior.CloseEquipmentSelector();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTERUI", "Error closing equipment selector", ex);
            }
        }

        // Note: GetSlotDisplayName method removed as it was unused.
        // If needed in future, add it to a shared utility class for slot name formatting.
    }
}
