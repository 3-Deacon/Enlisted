using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using static TaleWorlds.Core.ViewModelCollection.CharacterViewModel;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Main upgrade screen ViewModel with character preview and grid of upgrade options.
    /// </summary>
    public class QuartermasterUpgradeVm : ViewModel
    {
        private const int CardsPerRow = 4;

        [DataSourceProperty]
        public string HeaderText { get; private set; }

        [DataSourceProperty]
        public string PlayerGoldText { get; private set; }

        [DataSourceProperty]
        public string CurrentEquipmentText { get; private set; }

        [DataSourceProperty]
        public CharacterViewModel UnitCharacter { get; private set; }

        [DataSourceProperty]
        public MBBindingList<QuartermasterUpgradeRowVm> UpgradeRows { get; }

        public QuartermasterUpgradeVm()
        {
            UpgradeRows = new MBBindingList<QuartermasterUpgradeRowVm>();

            // Set up CharacterViewModel for character preview display
            try
            {
                var unitCharacter = new CharacterViewModel(StanceTypes.None);
                unitCharacter.FillFrom(Hero.MainHero.CharacterObject);
                UnitCharacter = unitCharacter;
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTERUI", "Error setting up character view model for upgrade screen", ex);
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();

            try
            {
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    SetEmptyValues();
                    return;
                }

                HeaderText = "Improve Equipment";
                PlayerGoldText = $"Your Gold: {hero.Gold} denars";
                CurrentEquipmentText = "Your Current Equipment";

                // Refresh character preview to show current gear
                if (UnitCharacter != null && hero.CharacterObject != null)
                {
                    UnitCharacter.FillFrom(hero.CharacterObject);
                    OnPropertyChanged(nameof(UnitCharacter));
                }

                // Build upgrade grid
                BuildUpgradeGrid(hero);

                OnPropertyChanged(nameof(HeaderText));
                OnPropertyChanged(nameof(PlayerGoldText));
                OnPropertyChanged(nameof(CurrentEquipmentText));
                OnPropertyChanged(nameof(UpgradeRows));
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTERUI", "Error refreshing upgrade screen values", ex);
                SetEmptyValues();
            }
        }

        /// <summary>
        /// Build a grid of upgrade cards (4 cards per row).
        /// Each card represents one equipped item showing its NEXT sequential upgrade.
        /// </summary>
        private void BuildUpgradeGrid(Hero hero)
        {
            try
            {
                UpgradeRows.Clear();
                var allCards = new List<QuartermasterUpgradeCardVm>();

                // Get available upgrade tiers for the player (sorted low to high)
                var availableTiers = QuartermasterManager.Instance?.GetAvailableUpgradeTiers()
                                   ?? new List<ItemQuality>();

                // Check all equipment slots
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var slot = (EquipmentIndex)i;
                    var element = hero.BattleEquipment[slot];

                    // Skip empty slots
                    if (element.IsEmpty || element.Item == null)
                    {
                        continue;
                    }

                    var item = element.Item;
                    var modGroup = item.ItemComponent?.ItemModifierGroup;

                    // Skip items without modifier groups (cannot be upgraded)
                    if (modGroup == null)
                    {
                        continue;
                    }

                    // Get current quality
                    var currentQuality = QuartermasterManager.GetModifierQuality(item, element.ItemModifier);

                    // Find the NEXT available upgrade tier (sequential upgrades only)
                    ItemQuality? nextQuality = null;
                    foreach (var tier in availableTiers)
                    {
                        if (tier > currentQuality)
                        {
                            // Check if this quality tier has modifiers for this item
                            var modifiers = modGroup.GetModifiersBasedOnQuality(tier);
                            if (modifiers != null && modifiers.Count > 0)
                            {
                                nextQuality = tier;
                                break; // Take the first higher quality available
                            }
                        }
                    }

                    // If there's a next quality available, create a card for it
                    if (nextQuality.HasValue)
                    {
                        var card = new QuartermasterUpgradeCardVm(
                            slot, item, element.ItemModifier, currentQuality, nextQuality.Value, this);
                        allCards.Add(card);
                    }
                }

                // Organize cards into rows of 4
                for (int i = 0; i < allCards.Count; i += CardsPerRow)
                {
                    var row = new QuartermasterUpgradeRowVm();
                    for (int j = 0; j < CardsPerRow && (i + j) < allCards.Count; j++)
                    {
                        row.Cards.Add(allCards[i + j]);
                    }
                    UpgradeRows.Add(row);
                }

                ModLogger.Info("QUARTERMASTERUI", $"Built upgrade grid with {allCards.Count} items in {UpgradeRows.Count} rows (sequential upgrades)");
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTERUI", "Error building upgrade grid", ex);
            }
        }

        /// <summary>
        /// Handle upgrade execution when player clicks Improve on a card.
        /// </summary>
        public void OnUpgradePerformed(EquipmentIndex slot, ItemQuality targetQuality)
        {
            try
            {
                var qm = QuartermasterManager.Instance;
                if (qm == null)
                {
                    ModLogger.Expected("QUARTERMASTERUI", "upgrade_no_qm_instance", "Upgrade failed: QuartermasterManager instance not found");
                    return;
                }

                // Perform the upgrade
                bool success = qm.PerformUpgrade(slot, targetQuality, out string errorMessage);

                if (success)
                {
                    // Show success message
                    var hero = Hero.MainHero;
                    var item = hero?.BattleEquipment[slot].Item;
                    var qualityName = GetQualityName(targetQuality);

                    var msg = $"Your {item?.Name?.ToString() ?? "equipment"} has been improved to {qualityName} quality.";
                    InformationManager.DisplayMessage(new InformationMessage(msg, Colors.Green));

                    // Refresh the screen to show updated equipment and gold
                    RefreshValues();
                }
                else
                {
                    // Show error message
                    InformationManager.DisplayMessage(new InformationMessage(errorMessage ?? "Upgrade failed.", Colors.Red));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTERUI", "Error performing upgrade", ex);
                InformationManager.DisplayMessage(new InformationMessage("Error performing upgrade. Please try again.", Colors.Red));
            }
        }

        public void ExecuteClose()
        {
            try
            {
                QuartermasterEquipmentSelectorBehavior.CloseUpgradeScreen();
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTERUI", "Error closing upgrade screen", ex);
            }
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

        private void SetEmptyValues()
        {
            HeaderText = "Improve Equipment";
            PlayerGoldText = "Gold information unavailable";
            CurrentEquipmentText = "Your Current Equipment";
            UpgradeRows.Clear();
        }
    }
}
