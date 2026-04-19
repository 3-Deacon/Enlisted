using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Individual provision item ViewModel for the provisions grid.
    ///
    /// Represents a single food item card with icon, name, price, quantity,
    /// and Buy 1 / Buy All purchase options.
    /// </summary>
    public class QuartermasterProvisionItemVm : ViewModel
    {
        /// <summary>
        /// The underlying food item.
        /// </summary>
        public ItemObject Item { get; }

        /// <summary>
        /// Price per unit after QM rep markup.
        /// </summary>
        public int Price { get; private set; }

        [DataSourceProperty]
        public string ItemName { get; private set; }

        [DataSourceProperty]
        public string PriceText { get; private set; }

        [DataSourceProperty]
        public string QuantityText { get; private set; }

        [DataSourceProperty]
        public int QuantityAvailable { get; private set; }

        [DataSourceProperty]
        public bool IsInStock { get; private set; }

        [DataSourceProperty]
        public bool CanAffordOne { get; private set; }

        [DataSourceProperty]
        public bool CanAffordAll { get; private set; }

        [DataSourceProperty]
        public bool BuyOneEnabled { get; private set; }

        [DataSourceProperty]
        public bool BuyAllEnabled { get; private set; }

        [DataSourceProperty]
        public string StatusText { get; private set; }

        /// <summary>
        /// Alpha for card (0.5 when out of stock, 1.0 when available).
        /// </summary>
        [DataSourceProperty]
        public float CardAlpha { get; private set; }

        [DataSourceProperty]
        public ItemImageIdentifierVM Image { get; private set; }

        [DataSourceProperty]
        public string TooltipText { get; private set; }

        /// <summary>
        /// Description of the food item (morale bonus, etc).
        /// </summary>
        [DataSourceProperty]
        public string DescriptionText { get; private set; }

        // Parent reference for purchase callbacks
        private readonly QuartermasterProvisionsVm _parent;
        
        // Whether player is an officer (T7+) and can purchase provisions
        private readonly bool _isOfficer;

        /// <summary>
        /// Initialize provision item with food data and pricing.
        /// </summary>
        /// <param name="item">The food item</param>
        /// <param name="price">Price per unit after markup</param>
        /// <param name="quantity">Available quantity</param>
        /// <param name="parent">Parent ViewModel for callbacks</param>
        /// <param name="isOfficer">Whether player has officer rank (T7+) to purchase</param>
        public QuartermasterProvisionItemVm(ItemObject item, int price, int quantity, QuartermasterProvisionsVm parent, bool isOfficer)
        {
            Item = item;
            Price = price;
            QuantityAvailable = quantity;
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _isOfficer = isOfficer;

            // Set up image
            if (item != null)
            {
                Image = new ItemImageIdentifierVM(item);
            }
            
            // Initialize display properties immediately
            RefreshValues();
        }

        /// <summary>
        /// Refresh display values when data changes.
        /// </summary>
        public override void RefreshValues()
        {
            base.RefreshValues();

            try
            {
                if (Item == null)
                {
                    SetEmptyValues();
                    return;
                }

                // Item name
                ItemName = Item.Name?.ToString() ?? "Unknown Food";

                // Truncate long names
                if (ItemName.Length > 30)
                {
                    ItemName = ItemName.Substring(0, 27) + "...";
                }

                // Price display
                PriceText = $"{Price} denars";

                // Quantity
                IsInStock = QuantityAvailable > 0;
                QuantityText = IsInStock
                    ? $"Available: {QuantityAvailable}"
                    : new TextObject("{=qm_provisions_out_of_stock}Out of Stock").ToString();

                // Card alpha for visual feedback
                CardAlpha = IsInStock ? 1.0f : 0.5f;

                // Status text
                if (!IsInStock)
                {
                    StatusText = new TextObject("{=qm_provisions_out_of_stock}Out of Stock").ToString();
                }
                else
                {
                    StatusText = new TextObject("{=qm_provisions_available}Available").ToString();
                }

                // Check affordability
                var playerGold = Hero.MainHero?.Gold ?? 0;
                CanAffordOne = playerGold >= Price;
                CanAffordAll = playerGold >= (Price * QuantityAvailable);

                // Button enable states (only officers can purchase)
                BuyOneEnabled = _isOfficer && IsInStock && CanAffordOne;
                BuyAllEnabled = _isOfficer && IsInStock && CanAffordAll && QuantityAvailable > 1;

                // Description (morale bonus info)
                BuildDescription();

                // Tooltip
                BuildTooltip(playerGold);

                // Notify UI
                NotifyPropertyChanges();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QuartermasterUI", "Error refreshing provision item", ex);
                SetEmptyValues();
            }
        }

        /// <summary>
        /// Build description text showing food quality/benefits.
        /// </summary>
        private void BuildDescription()
        {
            if (Item == null)
            {
                DescriptionText = "";
                return;
            }

            // Default description based on item tier/value
            var tierNum = (int)Item.Tier + 1;
            DescriptionText = $"Tier {tierNum} provisions";
        }

        /// <summary>
        /// Build tooltip with pricing and availability details.
        /// </summary>
        private void BuildTooltip(int playerGold)
        {
            if (Item == null)
            {
                TooltipText = "";
                return;
            }

            var parts = new System.Collections.Generic.List<string>();

            parts.Add(Item.Name?.ToString() ?? "Unknown");
            parts.Add($"Price: {Price} denars each");

            if (QuantityAvailable > 1)
            {
                parts.Add($"Buy all ({QuantityAvailable}): {Price * QuantityAvailable} denars");
            }

            if (!_isOfficer)
            {
                parts.Add("Officers Only (Rank T7+ or emergency: out of food)");
            }
            else if (!CanAffordOne)
            {
                parts.Add($"You need {Price - playerGold} more denars");
            }

            if (!IsInStock)
            {
                parts.Add("Restocks at next muster");
            }

            TooltipText = string.Join("\n", parts);
        }

        /// <summary>
        /// Set empty/error values.
        /// </summary>
        private void SetEmptyValues()
        {
            ItemName = new TextObject("{=qm_provisions_error}Error").ToString();
            PriceText = "";
            QuantityText = "";
            QuantityAvailable = 0;
            IsInStock = false;
            CanAffordOne = false;
            CanAffordAll = false;
            BuyOneEnabled = false;
            BuyAllEnabled = false;
            StatusText = "";
            CardAlpha = 0.5f;
            DescriptionText = "";
            TooltipText = "";
            Image = new ItemImageIdentifierVM(null);

            NotifyPropertyChanges();
        }

        /// <summary>
        /// Notify UI of all property changes.
        /// </summary>
        private void NotifyPropertyChanges()
        {
            OnPropertyChanged(nameof(ItemName));
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(QuantityText));
            OnPropertyChanged(nameof(QuantityAvailable));
            OnPropertyChanged(nameof(IsInStock));
            OnPropertyChanged(nameof(CanAffordOne));
            OnPropertyChanged(nameof(CanAffordAll));
            OnPropertyChanged(nameof(BuyOneEnabled));
            OnPropertyChanged(nameof(BuyAllEnabled));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CardAlpha));
            OnPropertyChanged(nameof(DescriptionText));
            OnPropertyChanged(nameof(TooltipText));
            OnPropertyChanged(nameof(Image));
        }

        /// <summary>
        /// Buy one unit of this provision.
        /// </summary>
        [UsedImplicitly("Bound via Gauntlet XML: Command.Click")]
        public void ExecuteBuyOne()
        {
            try
            {
                if (!BuyOneEnabled)
                {
                    if (!_isOfficer)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Provisions require officer rank (T7+) or emergency access (completely out of food).",
                            Colors.Red));
                    }
                    else if (!IsInStock)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=qm_provisions_out_of_stock_msg}This item is out of stock.").ToString()));
                    }
                    else if (!CanAffordOne)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=qm_cannot_afford_provisions}You can't afford this.").ToString(),
                            Colors.Red));
                    }
                    return;
                }

                // Process purchase through parent
                _parent?.OnProvisionPurchased(this, 1);

                // Update local quantity (will be fully refreshed by parent)
                QuantityAvailable = Math.Max(0, QuantityAvailable - 1);
                RefreshValues();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QuartermasterUI", "Error executing Buy One", ex);
            }
        }

        /// <summary>
        /// Buy all available units of this provision.
        /// </summary>
        [UsedImplicitly("Bound via Gauntlet XML: Command.Click")]
        public void ExecuteBuyAll()
        {
            try
            {
                if (!BuyAllEnabled)
                {
                    if (!_isOfficer)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Provisions require officer rank (T7+) or emergency access (completely out of food).",
                            Colors.Red));
                    }
                    else if (!IsInStock)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=qm_provisions_out_of_stock_msg}This item is out of stock.").ToString()));
                    }
                    else if (!CanAffordAll)
                    {
                        // Calculate how many we can afford
                        var playerGold = Hero.MainHero?.Gold ?? 0;
                        var canAffordCount = Price > 0 ? playerGold / Price : 0;

                        if (canAffordCount > 0 && _isOfficer)
                        {
                            // Buy what we can afford (officers only)
                            _parent?.OnProvisionPurchased(this, canAffordCount);
                            QuantityAvailable = Math.Max(0, QuantityAvailable - canAffordCount);
                            RefreshValues();
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                new TextObject("{=qm_cannot_afford_provisions}You can't afford this.").ToString(),
                                Colors.Red));
                        }
                    }
                    return;
                }

                // Buy all available
                var quantity = QuantityAvailable;
                _parent?.OnProvisionPurchased(this, quantity);

                QuantityAvailable = 0;
                RefreshValues();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QuartermasterUI", "Error executing Buy All", ex);
            }
        }
    }
}

