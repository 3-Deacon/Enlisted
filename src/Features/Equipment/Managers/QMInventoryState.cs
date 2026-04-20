using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.Equipment.Managers
{
    /// <summary>
    /// Tracks quartermaster inventory stock between visits.
    /// Inventory refreshes every 12 days at muster with supply-level variation.
    /// </summary>
    public class QMInventoryState
    {
        /// <summary>
        /// Current stock quantities keyed by item StringId.
        /// Value represents how many of this item are available for purchase.
        /// </summary>
        [SaveableProperty(1)]
        public Dictionary<string, int> CurrentStock { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Campaign day when inventory was last refreshed.
        /// Used to determine if 12-day refresh cycle has elapsed.
        /// </summary>
        [SaveableProperty(2)]
        public int LastRefreshDay { get; set; } = -1;

        /// <summary>
        /// Supply level percentage used for last inventory refresh.
        /// Stored for reference and diagnostics.
        /// </summary>
        [SaveableProperty(3)]
        public float LastRefreshSupplyLevel { get; set; }

        private const int RefreshCycleDays = 12; // Muster cycle duration

        /// <summary>
        /// Check if inventory needs to be refreshed based on 12-day muster cycle.
        /// </summary>
        /// <returns>True if 12 or more days have passed since last refresh</returns>
        public bool NeedsRefresh()
        {
            if (LastRefreshDay < 0)
            {
                // Never refreshed - needs initial refresh
                return true;
            }

            var currentDay = (int)CampaignTime.Now.ToDays;
            var daysSinceRefresh = currentDay - LastRefreshDay;

            return daysSinceRefresh >= RefreshCycleDays;
        }

        /// <summary>
        /// Refresh inventory based on current supply level.
        /// Generates new stock quantities with supply-based variety and quantity scaling.
        /// </summary>
        /// <param name="supplyLevel">Current company supply percentage (0-100)</param>
        /// <param name="availableItems">Pool of items that could be stocked</param>
        public void RefreshInventory(float supplyLevel, List<ItemObject> availableItems)
        {
            if (availableItems == null || availableItems.Count == 0)
            {
                ModLogger.Warn("Inventory", "RefreshInventory called with empty item pool");
                CurrentStock.Clear();
                UpdateRefreshMetadata(supplyLevel);
                return;
            }

            try
            {
                ModLogger.Info("Inventory", $"Refreshing QM inventory at {supplyLevel:F1}% supply");

                CurrentStock.Clear();

                // Determine variety and quantity based on supply level
                var (varietyPercent, minQty, maxQty) = GetStockParameters(supplyLevel);

                // Calculate how many items to stock based on variety percentage
                var itemCount = Math.Max(1, (int)(availableItems.Count * varietyPercent));

                // Shuffle and select items to stock
                var itemsToStock = availableItems
                    .OrderBy(_ => MBRandom.RandomFloat) // Shuffle
                    .Take(itemCount)
                    .ToList();

                // Assign quantities to each selected item
                foreach (var item in itemsToStock)
                {
                    var quantity = MBRandom.RandomInt(minQty, maxQty + 1); // +1 because RandomInt is exclusive
                    CurrentStock[item.StringId] = quantity;
                }

                UpdateRefreshMetadata(supplyLevel);

                ModLogger.Info("Inventory", $"Stocked {itemsToStock.Count} items (variety: {varietyPercent:P0}, qty range: {minQty}-{maxQty})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Inventory", "Error refreshing inventory", ex);
                // Fail safe - ensure inventory is cleared on error
                CurrentStock.Clear();
                UpdateRefreshMetadata(supplyLevel);
            }
        }

        /// <summary>
        /// Get stock parameters (variety %, min qty, max qty) based on supply level.
        /// Supply affects both variety of items available and quantity of each item.
        /// </summary>
        /// <param name="supplyLevel">Supply percentage (0-100)</param>
        /// <returns>Tuple of (variety percent, min quantity, max quantity)</returns>
        private (float varietyPercent, int minQty, int maxQty) GetStockParameters(float supplyLevel)
        {
            // Clamp supply to valid range
            supplyLevel = MathF.Clamp(supplyLevel, 0f, 100f);

            // Define stock parameters by supply tier
            if (supplyLevel >= 80f) // Excellent: 80-100%
            {
                return (1.0f, 3, 5); // Full variety, 3-5 each
            }
            if (supplyLevel >= 60f) // Good: 60-79%
            {
                return (1.0f, 2, 3); // Full variety, 2-3 each
            }
            if (supplyLevel >= 40f) // Fair: 40-59%
            {
                return (0.75f, 2, 2); // 75% variety, 2 each
            }
            if (supplyLevel >= 30f) // Low: 30-39%
            {
                return (0.50f, 1, 2); // 50% variety, 1-2 each
            }
            // Critical: 0-29%
            return (0.25f, 1, 1); // 25% variety, 1 each
        }

        /// <summary>
        /// Try to purchase an item, decrementing stock if available.
        /// </summary>
        /// <param name="itemStringId">Item StringId to purchase</param>
        /// <param name="quantity">Number to purchase (default 1)</param>
        /// <returns>True if purchase succeeded (stock was available), false otherwise</returns>
        public bool TryPurchase(string itemStringId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemStringId))
            {
                ModLogger.Warn("Inventory", "TryPurchase called with null/empty itemStringId");
                return false;
            }

            if (quantity < 1)
            {
                ModLogger.Warn("Inventory", $"TryPurchase called with invalid quantity: {quantity}");
                return false;
            }

            // Check if item is in stock
            if (!CurrentStock.TryGetValue(itemStringId, out var availableQty))
            {
                ModLogger.Debug("Inventory", $"Item not in stock: {itemStringId}");
                return false;
            }

            // Check if enough quantity available
            if (availableQty < quantity)
            {
                ModLogger.Debug("Inventory", $"Insufficient stock for {itemStringId}: need {quantity}, have {availableQty}");
                return false;
            }

            // Decrement stock
            var newQty = availableQty - quantity;
            if (newQty <= 0)
            {
                // Remove from stock entirely
                _ = CurrentStock.Remove(itemStringId);
                ModLogger.Info("Inventory", $"Item sold out: {itemStringId}");
            }
            else
            {
                CurrentStock[itemStringId] = newQty;
                ModLogger.Debug("Inventory", $"Item purchased: {itemStringId} ({quantity}), remaining: {newQty}");
            }

            return true;
        }

        /// <summary>
        /// Get available quantity for an item.
        /// </summary>
        /// <param name="itemStringId">Item StringId to check</param>
        /// <returns>Available quantity (0 if not in stock)</returns>
        public int GetAvailableQuantity(string itemStringId)
        {
            if (string.IsNullOrEmpty(itemStringId))
            {
                return 0;
            }

            return CurrentStock.TryGetValue(itemStringId, out var qty) ? qty : 0;
        }

        /// <summary>
        /// Check if an item is currently in stock (quantity > 0).
        /// </summary>
        /// <param name="itemStringId">Item StringId to check</param>
        /// <returns>True if in stock, false otherwise</returns>
        public bool IsInStock(string itemStringId)
        {
            return GetAvailableQuantity(itemStringId) > 0;
        }

        /// <summary>
        /// Update refresh metadata after inventory generation.
        /// </summary>
        private void UpdateRefreshMetadata(float supplyLevel)
        {
            LastRefreshDay = (int)CampaignTime.Now.ToDays;
            LastRefreshSupplyLevel = supplyLevel;
        }

        /// <summary>
        /// Get number of days until next scheduled refresh.
        /// </summary>
        /// <returns>Days until next refresh (0 if already due)</returns>
        public int DaysUntilNextRefresh()
        {
            if (LastRefreshDay < 0)
            {
                return 0; // Never refreshed
            }

            var currentDay = (int)CampaignTime.Now.ToDays;
            var daysSinceRefresh = currentDay - LastRefreshDay;
            var daysRemaining = RefreshCycleDays - daysSinceRefresh;

            return Math.Max(0, daysRemaining);
        }

        /// <summary>
        /// Force an immediate inventory refresh (for debugging or special events).
        /// </summary>
        public void ForceRefresh(float supplyLevel, List<ItemObject> availableItems)
        {
            ModLogger.Info("Inventory", "Forcing immediate inventory refresh");
            RefreshInventory(supplyLevel, availableItems);
        }

        /// <summary>
        /// Clear all inventory (for testing or special scenarios).
        /// </summary>
        public void ClearInventory()
        {
            ModLogger.Info("Inventory", "Clearing all inventory");
            CurrentStock.Clear();
        }
    }
}

