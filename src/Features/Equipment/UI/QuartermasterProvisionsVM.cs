using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Logistics;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Main ViewModel for the quartermaster provisions grid UI.
    ///
    /// Displays available food items in a visual grid layout matching the equipment interface.
    /// Tier-based behavior: T1-T6 (Enlisted/NCO) see ration info with supplement option,
    /// T7+ (Officers) see full provisions shop with all food items.
    /// </summary>
    public class QuartermasterProvisionsVm : ViewModel
    {
        // Row-based organization for ListPanel grid layout
        [DataSourceProperty]
        public MBBindingList<QuartermasterProvisionRowVm> ProvisionRows { get; }

        [DataSourceProperty]
        public string HeaderText { get; private set; }

        [DataSourceProperty]
        public string PlayerGoldText { get; private set; }

        [DataSourceProperty]
        public string RationInfoText { get; private set; }

        /// <summary>
        /// Whether to show the ration info panel (T1-T6 enlisted/NCO).
        /// </summary>
        [DataSourceProperty]
        public bool ShowRationInfo { get; private set; }

        /// <summary>
        /// Whether to show the full provisions shop (T7+ officers).
        /// </summary>
        [DataSourceProperty]
        public bool ShowProvisionsShop { get; private set; }

        /// <summary>
        /// Days until next muster (for restocking message).
        /// </summary>
        [DataSourceProperty]
        public string RestockInfoText { get; private set; }

        // Player tier determines UI mode
        private readonly bool _isOfficer;

        // Items per row in grid
        private const int ItemsPerRow = 4;

        /// <summary>
        /// Initialize provisions ViewModel.
        /// T7+ officers and anyone completely out of food can purchase.
        /// </summary>
        public QuartermasterProvisionsVm()
        {
            ProvisionRows = new MBBindingList<QuartermasterProvisionRowVm>();
            
            var playerTier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
            var hasNoFood = !HasAnyFood();
            
            // Allow purchase access if: officer rank (T7+) OR completely out of food (emergency access)
            _isOfficer = playerTier >= 7 || hasNoFood;
            
            // Simple header for all ranks
            HeaderText = "Company Provisions";
            
            ShowRationInfo = false;
            ShowProvisionsShop = true;
            
            BuildProvisionsGrid();
            
            var accessReason = playerTier >= 7 ? "officer" : (hasNoFood ? "emergency (no food)" : "enlisted");
            ModLogger.Info("QuartermasterUI", $"Provisions UI initialized for T{playerTier} {accessReason}");
        }

        /// <summary>
        /// Build the provisions grid from available food items.
        /// Uses QMInventoryState for stock tracking and supply-based pricing.
        /// </summary>
        private void BuildProvisionsGrid()
        {
            try
            {
                ProvisionRows.Clear();

                // Get available food items from game data
                var foodItems = GetAvailableFoodItems();

                ModLogger.Info("QuartermasterUI", $"Found {foodItems.Count} food items for provisions");

                if (foodItems.Count == 0)
                {
                    ModLogger.Error("QuartermasterUI", "No food items found! Cannot build provisions grid. This should never happen - check game data.");
                    HeaderText = "Error: No Food Items";
                    OnPropertyChanged(nameof(HeaderText));
                    return;
                }

                // Get inventory state for stock quantities
                var qmManager = QuartermasterManager.Instance;
                var inventoryState = qmManager?.GetInventoryState();
                
                // Ensure inventory has food items (may not be populated until first muster)
                if (inventoryState != null && inventoryState.CurrentStock.Count == 0)
                {
                    ModLogger.Warn("QuartermasterUI", "Inventory state is empty - initializing with food items");
                    var currentSupplyLevel = CompanySupplyManager.Instance?.TotalSupply ?? 50f;
                    inventoryState.RefreshInventory(currentSupplyLevel, foodItems);
                }

                // Get supply level for pricing
                var supplyLevel = CompanySupplyManager.Instance?.TotalSupply ?? 50f;

                // Create provision items with stock and pricing info
                var provisionItems = new List<QuartermasterProvisionItemVm>();

                foreach (var item in foodItems)
                {
                    // Get stock quantity from inventory state
                    var quantity = inventoryState?.GetAvailableQuantity(item.StringId) ?? 5;

                    // Calculate price with QM rep markup
                    var basePrice = item.Value;
                    var finalPrice = CalculateProvisionPrice(basePrice, supplyLevel);

                    var provisionItem = new QuartermasterProvisionItemVm(item, finalPrice, quantity, this, _isOfficer);
                    provisionItems.Add(provisionItem);
                }

                // Organize into rows of 4 items each
                var currentCards = new MBBindingList<QuartermasterProvisionItemVm>();

                foreach (var item in provisionItems)
                {
                    currentCards.Add(item);

                    if (currentCards.Count == ItemsPerRow)
                    {
                        ProvisionRows.Add(new QuartermasterProvisionRowVm(currentCards));
                        currentCards = new MBBindingList<QuartermasterProvisionItemVm>();
                    }
                }

                // Add remaining items as final row
                if (currentCards.Count > 0)
                {
                    ProvisionRows.Add(new QuartermasterProvisionRowVm(currentCards));
                }

                ModLogger.Info("QuartermasterUI", $"Built provisions grid with {provisionItems.Count} items in {ProvisionRows.Count} rows");
                
                // Debug: Log each row and its cards
                for (int r = 0; r < ProvisionRows.Count; r++)
                {
                    var row = ProvisionRows[r];
                    ModLogger.Debug("QuartermasterUI", $"Row {r}: {row.Cards.Count} cards");
                    for (int c = 0; c < row.Cards.Count; c++)
                    {
                        var card = row.Cards[c];
                        ModLogger.Debug("QuartermasterUI", $"  Card {c}: {card.ItemName ?? "NULL"} | Price: {card.PriceText ?? "NULL"} | Qty: {card.QuantityText ?? "NULL"}");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error building provisions grid", ex);
            }
        }

        /// <summary>
        /// Get available food items from game data.
        /// Filters to consumable food items only.
        /// </summary>
        private List<ItemObject> GetAvailableFoodItems()
        {
            var foodItems = new List<ItemObject>();

            try
            {
                // Get all food-related items from object manager
                var allItems = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
                ModLogger.Debug("QuartermasterUI", $"Scanning {allItems.Count} total items for food");

                foreach (var item in allItems)
                {
                    if (item == null || item.StringId == null)
                    {
                        continue;
                    }

                    // Check if item is food (consumable with food morale bonus)
                    if (IsFood(item))
                    {
                        foodItems.Add(item);
                        ModLogger.Info("QuartermasterUI", $"Found food: {item.StringId} | Name: {item.Name} | IsFood={item.IsFood}");
                    }
                }

                // Sort by value (cheapest first for better UX)
                foodItems = foodItems.OrderBy(i => i.Value).ToList();

                ModLogger.Info("QuartermasterUI", $"Found {foodItems.Count} food items for provisions from {allItems.Count} total items");
                
                // Log the final sorted list
                foreach (var item in foodItems)
                {
                    ModLogger.Info("QuartermasterUI", $"  -> {item.Name} ({item.StringId}) | Value: {item.Value}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error getting food items", ex);
            }

            return foodItems;
        }

        /// <summary>
        /// Check if the player currently has any food in their party inventory.
        /// Used to determine emergency provisions access for non-officers.
        /// </summary>
        private static bool HasAnyFood()
        {
            try
            {
                var party = MobileParty.MainParty;
                if (party?.ItemRoster == null)
                {
                    return false;
                }

                // Check if player has any food items in inventory
                foreach (var element in party.ItemRoster)
                {
                    var item = element.EquipmentElement.Item;
                    if (item != null && item.IsFood)
                    {
                        return true;
                    }
                }

                ModLogger.Info("QuartermasterUI", "Player has no food in inventory - granting emergency provisions access");
                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error checking player food inventory", ex);
                return false; // Fail safe - grant access on error
            }
        }

        /// <summary>
        /// Check if an item is a food item suitable for provisions.
        /// </summary>
        private static bool IsFood(ItemObject item)
        {
            if (item == null)
            {
                return false;
            }

            // Primary check: Bannerlord's IsFood property
            if (item.IsFood)
            {
                return true;
            }

            // Secondary check: Goods with food-related names
            if (item.ItemType == ItemObject.ItemTypeEnum.Goods)
            {
                var id = item.StringId?.ToLowerInvariant() ?? "";
                if (id.Contains("grain") || id.Contains("meat") || id.Contains("fish") ||
                    id.Contains("butter") || id.Contains("cheese") || id.Contains("bread") ||
                    id.Contains("date") || id.Contains("olives") || id.Contains("food") ||
                    id.Contains("apple") || id.Contains("grape"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculate provision price with QM reputation markup.
        /// Provisions use higher markup than equipment (1.5× to 2.2× base).
        /// </summary>
        private int CalculateProvisionPrice(int basePrice, float supplyLevel)
        {
            // Get QM reputation modifier for provisions
            // Provisions pricing from spec: 1.5× (Trusted) to 2.2× (Hostile)
            var repMultiplier = GetProvisionsReputationMultiplier();

            // Apply supply scarcity pricing (stacks with rep)
            var supplyMultiplier = GetSupplyPriceMultiplier(supplyLevel);

            var finalPrice = (int)Math.Ceiling(basePrice * repMultiplier * supplyMultiplier);

            // Ensure minimum price of 1
            return Math.Max(1, finalPrice);
        }

        /// <summary>
        /// Get provisions-specific reputation price multiplier.
        /// Provisions have higher markups than equipment per spec.
        /// </summary>
        private float GetProvisionsReputationMultiplier()
        {
            var qmManager = QuartermasterManager.Instance;
            if (qmManager == null)
            {
                return 2.5f; // Neutral default
            }

            var rep = qmManager.Reputation;

            // Provisions pricing: Always more expensive than town markets
            // QM is convenience for officers in the field, not a bargain shop
            // Trusted (65+): 2.0× market (best case - still double town prices)
            // Friendly (35-64): 2.3× market
            // Neutral (10-34): 2.5× market
            // Wary (-10 to 9): 2.8× market
            // Hostile (< -10): 3.2× market (price gouging)
            if (rep >= 65)
            {
                return 2.0f;
            }
            if (rep >= 35)
            {
                return 2.3f;
            }
            if (rep >= 10)
            {
                return 2.5f;
            }
            if (rep >= -10)
            {
                return 2.8f;
            }

            return 3.2f;
        }

        /// <summary>
        /// Get supply-based price multiplier.
        /// Low supply increases prices.
        /// </summary>
        private static float GetSupplyPriceMultiplier(float supplyLevel)
        {
            if (supplyLevel >= 60f)
            {
                return 1.0f; // Good/Excellent: no markup
            }
            if (supplyLevel >= 40f)
            {
                return 1.1f; // Fair: +10%
            }
            if (supplyLevel >= 30f)
            {
                return 1.25f; // Low: +25%
            }

            return 1.5f; // Critical: +50%
        }

        /// <summary>
        /// Refresh all display values when data changes.
        /// </summary>
        public override void RefreshValues()
        {
            base.RefreshValues();

            try
            {
                var hero = Hero.MainHero;
                // Simple header for all ranks
                HeaderText = "Company Provisions";

                PlayerGoldText = $"Your Gold: {hero.Gold} denars";

                // Build ration info for T1-T6
                if (!_isOfficer)
                {
                    BuildRationInfoText();
                }

                // Build restock info
                var inventoryState = QuartermasterManager.Instance?.GetInventoryState();
                var daysUntilRestock = inventoryState?.DaysUntilNextRefresh() ?? 0;
                RestockInfoText = daysUntilRestock > 0
                    ? new TextObject("{=qm_provisions_restock}Restocks in {DAYS} days")
                        .SetTextVariable("DAYS", daysUntilRestock).ToString()
                    : new TextObject("{=qm_provisions_fresh_stock}Fresh stock available").ToString();

                // Refresh all provision items
                foreach (var row in ProvisionRows)
                {
                    foreach (var item in row.Cards)
                    {
                        item.RefreshValues();
                    }
                }

                // Notify UI of changes
                OnPropertyChanged(nameof(HeaderText));
                OnPropertyChanged(nameof(PlayerGoldText));
                OnPropertyChanged(nameof(RationInfoText));
                OnPropertyChanged(nameof(RestockInfoText));
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error refreshing provisions values", ex);
                HeaderText = "Provisions";
                PlayerGoldText = "Gold unavailable";
            }
        }

        /// <summary>
        /// Build ration info text for T1-T6 enlisted/NCO.
        /// Shows current ration status and expected quality.
        /// </summary>
        private void BuildRationInfoText()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    RationInfoText = new TextObject("{=qm_provisions_not_enlisted}You are not currently enlisted.").ToString();
                    return;
                }

                var sb = new System.Text.StringBuilder();

                // Get food quality info (morale system removed - quality is now just for flavor)
                var (qualityName, daysRemaining) = enlistment.GetFoodQualityInfo();

                if (daysRemaining > 0)
                {
                    var statusText = new TextObject("{=qm_ration_status}Current: {QUALITY} - {DAYS} days remaining");
                    statusText.SetTextVariable("QUALITY", qualityName);
                    statusText.SetTextVariable("DAYS", (int)Math.Ceiling(daysRemaining));
                    sb.AppendLine(statusText.ToString());
                }
                else
                {
                    sb.AppendLine(new TextObject("{=qm_ration_standard}Current: Standard army rations (no bonus)").ToString());
                }

                // Add hint about purchasing supplements
                sb.AppendLine();
                sb.AppendLine(new TextObject("{=qm_ration_supplements_hint}You can purchase supplemental provisions below for morale bonuses.").ToString());

                RationInfoText = sb.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error building ration info text", ex);
                RationInfoText = "";
            }
        }

        /// <summary>
        /// Handle provision item purchase from child ViewModel.
        /// </summary>
        public void OnProvisionPurchased(QuartermasterProvisionItemVm item, int quantity)
        {
            try
            {
                if (item?.Item == null)
                {
                    ModLogger.Warn("QuartermasterUI", "OnProvisionPurchased called with null item");
                    return;
                }

                var totalCost = item.Price * quantity;
                var hero = Hero.MainHero;

                // Check affordability
                if (hero.Gold < totalCost)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_cannot_afford_provisions}You can't afford this.").ToString(),
                        Colors.Red));
                    return;
                }

                // Deduct gold
                hero.Gold -= totalCost;

                // Add food to party inventory
                var party = MobileParty.MainParty;
                if (party != null)
                {
                    party.ItemRoster.AddToCounts(item.Item, quantity);
                }

                // Decrement stock in inventory state
                var inventoryState = QuartermasterManager.Instance?.GetInventoryState();
                inventoryState?.TryPurchase(item.Item.StringId, quantity);

                // Log purchase
                var msg = new TextObject("{=qm_provisions_purchased}Purchased {QUANTITY}x {ITEM} for {COST} denars.");
                msg.SetTextVariable("QUANTITY", quantity);
                msg.SetTextVariable("ITEM", item.Item.Name);
                msg.SetTextVariable("COST", totalCost);
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));

                ModLogger.Info("QuartermasterUI", $"Purchased {quantity}x {item.Item.StringId} for {totalCost}g");

                // Refresh UI
                RefreshValues();
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error processing provision purchase", ex);
            }
        }

        /// <summary>
        /// Close the provisions UI.
        /// </summary>
        public void ExecuteClose()
        {
            try
            {
                QuartermasterProvisionsBehavior.CloseProvisionsScreen();
            }
            catch (Exception ex)
            {
                ModLogger.Error("QuartermasterUI", "Error closing provisions UI", ex);
            }
        }
    }

    /// <summary>
    /// Row ViewModel for provisions grid layout.
    /// Groups provision items into rows of 4 for display.
    /// </summary>
    public class QuartermasterProvisionRowVm : ViewModel
    {
        [DataSourceProperty]
        public MBBindingList<QuartermasterProvisionItemVm> Cards { get; }

        public QuartermasterProvisionRowVm(MBBindingList<QuartermasterProvisionItemVm> cards)
        {
            Cards = cards ?? new MBBindingList<QuartermasterProvisionItemVm>();
        }
    }
}

