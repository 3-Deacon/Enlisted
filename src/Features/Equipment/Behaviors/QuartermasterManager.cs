using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enlisted.Features.Camp;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Managers;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Features.Retinue.Core;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Entry;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Equipment.Behaviors
{
    /// <summary>
    /// Quartermaster system providing equipment variant access for enlisted soldiers.
    ///
    /// This system replaces the weaponsmith feature with comprehensive equipment management
    /// based on runtime discovery of equipment variants from actual troop data. Players can
    /// request different weapons, armor, and equipment variants that their selected troop
    /// type can legally spawn with, creating authentic military supply management.
    /// </summary>
    public sealed class QuartermasterManager : CampaignBehaviorBase
    {
        public static QuartermasterManager Instance { get; private set; }

        private static readonly HashSet<string> NonReturnableQuestItemIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "dragon_banner" // Main quest banner item should never be returned
            };

        // Stock availability tracking - items in this set are out of stock until next muster
        private readonly HashSet<string> _outOfStockItems = new(StringComparer.OrdinalIgnoreCase);
        private int _lastStockRollSupplyLevel = -1;

        // Inventory state for Phase 7: Inventory & Pricing System
        // Tracks stock quantities and refreshes every 12 days at muster
        private QMInventoryState _inventoryState;

        /// <summary>
        /// Represents an item available for selling back to the quartermaster.
        /// Stores the full EquipmentElement to preserve modifier information for correct pricing.
        /// </summary>
        private sealed class ReturnOption
        {
            public EquipmentElement Element { get; set; }
            public int Count { get; set; }

            // Convenience accessors
            public ItemObject Item => Element.Item;
            public ItemModifier Modifier => Element.ItemModifier;

            /// <summary>
            /// Get a unique key for grouping items by both StringId and modifier.
            /// Items with different quality modifiers (Fine vs Rusty) are tracked separately.
            /// </summary>
            public static string GetGroupKey(EquipmentElement element)
            {
                var modifierId = element.ItemModifier?.StringId ?? "none";
                return $"{element.Item?.StringId}|{modifierId}";
            }
        }

        public QuartermasterManager()
        {
            Instance = this;
            _inventoryState = new QMInventoryState();
        }

        /// <summary>
        /// Gets the current QM reputation from the enlistment system.
        /// Higher reputation = better prices and access to premium items.
        /// </summary>
        public int Reputation
        {
            get
            {
                var enlistment = EnlistmentBehavior.Instance;
                return enlistment?.GetQMReputation() ?? 0;
            }
        }

        /// <summary>
        /// Get the inventory state for stock tracking.
        /// Used by provisions UI to show available quantities.
        /// </summary>
        public QMInventoryState GetInventoryState()
        {
            return _inventoryState;
        }

        #region Time-Preserving Menu Helpers

        // Captured time state for wait menu time restoration (allows spacebar to work)
        // Public so other menu behaviors can share the same captured state
        public static CampaignTimeControlMode? CapturedTimeMode { get; set; }

        /// <summary>
        /// Activate a game menu without changing the current time control mode.
        /// Vanilla ActivateGameMenu forcibly pauses time; this wrapper captures and restores the prior state.
        /// Also updates the captured time mode for the wait menu tick handler.
        /// </summary>
        private static void ActivateMenuPreserveTime(string menuId)
        {
            var previousMode = Campaign.Current?.TimeControlMode ?? CampaignTimeControlMode.Stop;
            CapturedTimeMode = previousMode; // Update for tick handler (shared across all Enlisted menus)
            GameMenu.ActivateGameMenu(menuId);
            if (Campaign.Current != null)
            {
                Campaign.Current.TimeControlMode = previousMode;
            }
        }

        /// <summary>
        /// Capture the current time control mode BEFORE activating a wait menu.
        /// Must be called from the calling code before ActivateGameMenu/SwitchToMenu,
        /// not from init handlers (which run after vanilla already sets Stop).
        /// Always captures the current state to respect player's time control changes
        /// while navigating between menus (e.g., if they pause in one menu and switch to another).
        /// </summary>
        public static void CaptureTimeStateBeforeMenuActivation()
        {
            // Always capture current time state to respect player changes during menu navigation.
            // If player changes speed in Camp menu then clicks Decisions, we want to preserve
            // their CURRENT speed, not the original speed from when they first opened Camp.
            var previousCaptured = CapturedTimeMode;
            CapturedTimeMode = Campaign.Current?.TimeControlMode;

            if (previousCaptured.HasValue && previousCaptured != CapturedTimeMode)
            {
                ModLogger.Debug("QUARTERMASTER", $"Time state changed: {previousCaptured} -> {CapturedTimeMode}");
            }
            else
            {
                ModLogger.Debug("QUARTERMASTER", $"Captured time state: {CapturedTimeMode}");
            }
        }

        /// <summary>
        /// Convert any unstoppable time modes to their stoppable equivalents while preserving Stop.
        /// Used to restore player-controlled speed after wait menus without auto-unpausing.
        /// </summary>
        public static CampaignTimeControlMode NormalizeToStoppable(CampaignTimeControlMode mode)
        {
            return mode switch
            {
                CampaignTimeControlMode.UnstoppablePlay => CampaignTimeControlMode.StoppablePlay,
                CampaignTimeControlMode.UnstoppableFastForward => CampaignTimeControlMode.StoppableFastForward,
                CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime => CampaignTimeControlMode.StoppableFastForward,
                _ => mode // Stop/StoppablePlay/StoppableFastForward stay unchanged
            };
        }

        /// <summary>
        /// Shared wait menu condition. Always returns true since we control exit via menu options.
        /// </summary>
        private static bool QuartermasterWaitCondition(MenuCallbackArgs args)
        {
            return true;
        }

        /// <summary>
        /// Shared wait menu consequence. Empty since we handle exit via menu options.
        /// </summary>
        private static void QuartermasterWaitConsequence(MenuCallbackArgs args)
        {
            // No consequence needed - we never let progress reach 100%
        }

        /// <summary>
        /// Wait tick handler for Quartermaster menus.
        /// NOTE: Time mode restoration is handled ONCE during menu init, not here.
        /// Previously this tick handler would restore CapturedTimeMode whenever it saw
        /// UnstoppableFastForward, but this fought with user input - when the user clicked
        /// fast forward, the next tick would immediately restore it. This caused x3 speed to pause.
        /// </summary>
        private static void QuartermasterWaitTick(MenuCallbackArgs args, CampaignTime dt)
        {
            // Intentionally empty - time mode is handled in menu init, not per-tick
            // The old code here fought with user speed input and caused pausing issues
        }

        /// <summary>
        /// Restart the quartermaster conversation after closing a GameMenu.
        /// Used by the rations menu back button to return to conversation flow.
        /// </summary>
        private static void RestartQuartermasterConversationFromMenu()
        {
            try
            {
                // Clear the captured time state since we're exiting the menu system
                CapturedTimeMode = null;

                // Close current menu first
                GameMenu.ExitToLast();

                // Get QM hero and restart conversation
                var enlistment = EnlistmentBehavior.Instance;
                var qmHero = enlistment?.GetOrCreateQuartermaster();

                if (qmHero != null && qmHero.IsAlive)
                {
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        try
                        {
                            var party = QuartermasterPartyResolver.GetConversationParty(qmHero);
                            if (party == null)
                            {
                                ModLogger.Expected("QUARTERMASTER", "qm_party_no_party_rations",
                                    "Both QM and enlisted lord have no party — cannot restart QM conversation from rations menu with correct scene");
                                InformationManager.DisplayMessage(new InformationMessage(
                                    new TextObject("{=qm_party_unavailable}The quartermaster cannot be reached right now.").ToString()));
                                return;
                            }

                            var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
                            var qmData = new ConversationCharacterData(qmHero.CharacterObject, party);

                            // Use sea conversation scene if at sea, otherwise use map conversation
                            if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
                            {
                                const string seaConversationScene = "conversation_scene_sea_multi_agent";
                                _ = CampaignMission.OpenConversationMission(playerData, qmData, seaConversationScene);
                            }
                            else
                            {
                                CampaignMapConversation.OpenConversation(playerData, qmData);
                            }
                            ModLogger.Debug("QUARTERMASTER", "Restarted QM conversation from rations menu");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("QUARTERMASTER", "Failed to restart QM conversation from menu", ex);
                            // Fallback: return to Camp Hub
                            EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                        }
                    });
                }
                else
                {
                    ModLogger.Warn("QUARTERMASTER", "QM hero unavailable, returning to Camp Hub");
                    EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error returning from rations menu", ex);
                EnlistedMenuBehavior.SafeActivateEnlistedMenu();
            }
        }

        #endregion

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Ensure inventory state exists
            _inventoryState ??= new QMInventoryState();

            // Manual serialization for QMInventoryState to avoid complex object serialization issues.
            // The Bannerlord save system can have issues with complex objects that have nested containers.

            // Sync primitive fields
            var lastRefreshDay = _inventoryState.LastRefreshDay;
            var lastRefreshSupply = _inventoryState.LastRefreshSupplyLevel;
            _ = dataStore.SyncData("qm_lastRefreshDay", ref lastRefreshDay);
            _ = dataStore.SyncData("qm_lastRefreshSupply", ref lastRefreshSupply);

            // Sync stock dictionary
            var stockCount = _inventoryState.CurrentStock?.Count ?? 0;
            _ = dataStore.SyncData("qm_stockCount", ref stockCount);

            if (dataStore.IsLoading)
            {
                _inventoryState.LastRefreshDay = lastRefreshDay;
                _inventoryState.LastRefreshSupplyLevel = lastRefreshSupply;
                _inventoryState.CurrentStock ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _inventoryState.CurrentStock.Clear();

                for (var i = 0; i < stockCount; i++)
                {
                    var itemId = string.Empty;
                    var qty = 0;
                    _ = dataStore.SyncData($"qm_stock_{i}_id", ref itemId);
                    _ = dataStore.SyncData($"qm_stock_{i}_qty", ref qty);

                    if (!string.IsNullOrEmpty(itemId) && qty > 0)
                    {
                        _inventoryState.CurrentStock[itemId] = qty;
                    }
                }

                ModLogger.Debug("QUARTERMASTER", $"Loaded inventory state: {stockCount} items, last refresh day {lastRefreshDay}");
            }
            else
            {
                // Saving - write each stock entry
                if (_inventoryState.CurrentStock != null)
                {
                    var idx = 0;
                    foreach (var kvp in _inventoryState.CurrentStock)
                    {
                        var itemId = kvp.Key;
                        var qty = kvp.Value;
                        _ = dataStore.SyncData($"qm_stock_{idx}_id", ref itemId);
                        _ = dataStore.SyncData($"qm_stock_{idx}_qty", ref qty);
                        idx++;
                    }
                }
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Set up global gold icon for inline currency display
            MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");

            AddQuartermasterMenus(starter);
            ModLogger.Info("QUARTERMASTER", "Quartermaster system initialized with modern UI styling");
        }


        /// <summary>
        /// Add quartermaster menu system.
        /// Equipment browsing and selling now use conversation-driven Gauntlet UI.
        /// Only the rations menu remains as a GameMenu (accessed from QM conversation).
        /// </summary>
        private void AddQuartermasterMenus(CampaignGameStarter starter)
        {
            // Rations purchase menu - still used via QM conversation
            starter.AddWaitGameMenu(
                "quartermaster_rations",
                "Provisions\n{RATIONS_TEXT}",
                OnQuartermasterRationsInit,
                QuartermasterWaitCondition,
                QuartermasterWaitConsequence,
                QuartermasterWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Add rations menu options
            AddRationsMenuOptions(starter);

            // Supply management menu for quartermaster officers (accessed from rations or directly)
            AddSupplyManagementMenu(starter);
        }

        // Track items that became available after the last promotion for "new item" indicators.
        private readonly HashSet<string> _previouslyAvailableItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _newlyUnlockedItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _lastPromotionTier;

        /// <summary>
        /// Get ALL equipment available for a given tier and culture, across ALL formations.
        /// Players should see infantry, archer, cavalry, and horse archer equipment for their tier.
        /// For example, a T3 player sees T1-T3 infantry weapons, T1-T3 archer bows, T1-T3 cavalry lances/horses, etc.
        /// </summary>
        /// <param name="tierCap">Maximum tier to include (player's current tier)</param>
        /// <param name="culture">Player's enlisted lord's culture</param>
        /// <returns>Dictionary of equipment slots to available items</returns>
        public Dictionary<EquipmentIndex, List<ItemObject>> GetAvailableEquipmentAllFormations(
            int tierCap,
            BasicCultureObject culture)
        {
            try
            {
                if (culture == null)
                {
                    return new Dictionary<EquipmentIndex, List<ItemObject>>();
                }

                var variants = new Dictionary<EquipmentIndex, List<ItemObject>>();
                var allTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

                // Scan ALL non-hero troops of the player's culture that are at or below tier
                // This includes infantry, archers, cavalry, and horse archers
                foreach (var troop in allTroops)
                {
                    if (troop.IsHero || troop.Culture != culture)
                    {
                        continue;
                    }

                    var troopTier = SafeGetTier(troop);
                    if (troopTier < 1 || troopTier > tierCap)
                    {
                        continue;
                    }

                    if (!troop.BattleEquipments.Any())
                    {
                        continue;
                    }

                    // Collect all equipment from this troop across all formations
                    foreach (var equipment in troop.BattleEquipments)
                    {
                        for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
                        {
                            var item = equipment[slot].Item;
                            if (item == null)
                            {
                                continue;
                            }

                            // Culture filter: if item has a culture, it must match
                            if (item.Culture != null && item.Culture != culture)
                            {
                                continue;
                            }

                            if (!variants.ContainsKey(slot))
                            {
                                variants[slot] = new List<ItemObject>();
                            }

                            if (!variants[slot].Contains(item))
                            {
                                variants[slot].Add(item);
                            }
                        }
                    }
                }

                var total = variants.Sum(kvp => kvp.Value.Count);
                ModLogger.Info("Quartermaster",
                    $"Cross-formation equipment scan: ALL formations T1-T{tierCap} {culture.Name} -> {total} items");

                return variants;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error in GetAvailableEquipmentAllFormations", ex);
                return new Dictionary<EquipmentIndex, List<ItemObject>>();
            }
        }

        /// <summary>
        /// Get mount variants for the UI browser, using cross-formation discovery.
        /// Returns EquipmentVariantOption list with proper pricing, quality, and stock status.
        /// This is the public API for mount browsing - use this instead of reflection.
        /// </summary>
        public List<EquipmentVariantOption> GetMountVariantsForBrowsing()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return new List<EquipmentVariantOption>();
                }

                var tier = enlistment.EnlistmentTier;
                var culture = enlistment.EnlistedLord?.Culture;
                if (culture == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                // Get all equipment across all formations
                var allEquipment = GetAvailableEquipmentAllFormations(tier, culture);

                var mountOptions = new List<EquipmentVariantOption>();

                // Get horse items
                if (allEquipment.TryGetValue(EquipmentIndex.Horse, out var horses))
                {
                    var currentHorse = hero.CharacterObject?.Equipment?[EquipmentIndex.Horse].Item;

                    foreach (var item in horses)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        var isCurrent = item == currentHorse;
                        var isAtLimit = GetPlayerItemCount(hero, item.StringId) >= 1;

                        // Roll quality based on reputation
                        var rolledQuality = RollItemQualityByReputation();
                        var (modifier, actualQuality) = GetRandomModifierForQuality(item, rolledQuality);

                        // Calculate price
                        var basePrice = item.Value;
                        if (modifier != null)
                        {
                            basePrice = (int)(basePrice * modifier.PriceMultiplier);
                        }
                        var price = CalculateQuartermasterPriceFromBase(basePrice);
                        var canAfford = hero.Gold >= price;
                        var isInStock = IsItemInStock(item);
                        var quantityAvailable = _inventoryState?.GetAvailableQuantity(item.StringId) ?? 999;
                        var isAvailable = quantityAvailable > 0;

                        string modifiedName = null;
                        if (modifier != null)
                        {
                            modifiedName = $"{modifier.Name} {item.Name}";
                        }

                        mountOptions.Add(new EquipmentVariantOption
                        {
                            Item = item,
                            Cost = price,
                            IsCurrent = isCurrent,
                            CanAfford = canAfford,
                            Slot = EquipmentIndex.Horse,
                            IsOfficerExclusive = false,
                            AllowsDuplicatePurchase = false,
                            IsAtLimit = isAtLimit,
                            IsNewlyUnlocked = IsNewlyUnlockedItem(item),
                            IsInStock = isInStock && isAvailable,
                            QuantityAvailable = quantityAvailable,
                            Modifier = modifier,
                            Quality = actualQuality,
                            ModifiedName = modifiedName
                        });
                    }
                }

                // Sort: current first, then by name
                return mountOptions.OrderBy(o => o.IsCurrent ? 0 : 1)
                                   .ThenBy(o => o.Item.Name.ToString()).ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error in GetMountVariantsForBrowsing", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        /// Get harness variants for the UI browser, using cross-formation discovery.
        /// Returns EquipmentVariantOption list with proper pricing, quality, and stock status.
        /// </summary>
        public List<EquipmentVariantOption> GetHarnessVariantsForBrowsing()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return new List<EquipmentVariantOption>();
                }

                var tier = enlistment.EnlistmentTier;
                var culture = enlistment.EnlistedLord?.Culture;
                if (culture == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                // Get all equipment across all formations
                var allEquipment = GetAvailableEquipmentAllFormations(tier, culture);

                var harnessOptions = new List<EquipmentVariantOption>();

                // Get harness items
                if (allEquipment.TryGetValue(EquipmentIndex.HorseHarness, out var harnesses))
                {
                    var currentHarness = hero.CharacterObject?.Equipment?[EquipmentIndex.HorseHarness].Item;

                    foreach (var item in harnesses)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        var isCurrent = item == currentHarness;
                        var isAtLimit = GetPlayerItemCount(hero, item.StringId) >= 1;

                        // Roll quality based on reputation
                        var rolledQuality = RollItemQualityByReputation();
                        var (modifier, actualQuality) = GetRandomModifierForQuality(item, rolledQuality);

                        // Calculate price
                        var basePrice = item.Value;
                        if (modifier != null)
                        {
                            basePrice = (int)(basePrice * modifier.PriceMultiplier);
                        }
                        var price = CalculateQuartermasterPriceFromBase(basePrice);
                        var canAfford = hero.Gold >= price;
                        var isInStock = IsItemInStock(item);
                        var quantityAvailable = _inventoryState?.GetAvailableQuantity(item.StringId) ?? 999;
                        var isAvailable = quantityAvailable > 0;

                        string modifiedName = null;
                        if (modifier != null)
                        {
                            modifiedName = $"{modifier.Name} {item.Name}";
                        }

                        harnessOptions.Add(new EquipmentVariantOption
                        {
                            Item = item,
                            Cost = price,
                            IsCurrent = isCurrent,
                            CanAfford = canAfford,
                            Slot = EquipmentIndex.HorseHarness,
                            IsOfficerExclusive = false,
                            AllowsDuplicatePurchase = false,
                            IsAtLimit = isAtLimit,
                            IsNewlyUnlocked = IsNewlyUnlockedItem(item),
                            IsInStock = isInStock && isAvailable,
                            QuantityAvailable = quantityAvailable,
                            Modifier = modifier,
                            Quality = actualQuality,
                            ModifiedName = modifiedName
                        });
                    }
                }

                // Sort: current first, then by name
                return harnessOptions.OrderBy(o => o.IsCurrent ? 0 : 1)
                                     .ThenBy(o => o.Item.Name.ToString()).ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error in GetHarnessVariantsForBrowsing", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        /// Get weapon variants for the UI browser, using cross-formation discovery.
        /// Shields are excluded because they are shown under accessories.
        /// </summary>
        public List<EquipmentVariantOption> GetWeaponVariantsForBrowsing()
        {
            try
            {
                var slots = new[]
                {
                    EquipmentIndex.Weapon0,
                    EquipmentIndex.Weapon1,
                    EquipmentIndex.Weapon2,
                    EquipmentIndex.Weapon3
                };

                return GetEquipmentVariantsForBrowsing(slots,
                    (slot, item) =>
                    {
                        _ = slot;
                        var weapon = item?.WeaponComponent?.PrimaryWeapon;
                        return weapon != null && !weapon.IsShield;
                    });
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error in GetWeaponVariantsForBrowsing", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        /// Get armor variants for the UI browser, combining helmet, body, gloves, and boots.
        /// Capes are excluded because they are shown under accessories.
        /// </summary>
        public List<EquipmentVariantOption> GetArmorVariantsForBrowsing()
        {
            try
            {
                var slots = new[]
                {
                    EquipmentIndex.Head,
                    EquipmentIndex.Body,
                    EquipmentIndex.Gloves,
                    EquipmentIndex.Leg
                };

                return GetEquipmentVariantsForBrowsing(slots,
                    (slot, item) =>
                    {
                        _ = slot;
                        return item?.ArmorComponent != null;
                    });
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error in GetArmorVariantsForBrowsing", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        /// Get accessory variants for the UI browser: capes, shields, and horse harness.
        /// </summary>
        public List<EquipmentVariantOption> GetAccessoryVariantsForBrowsing()
        {
            try
            {
                var slots = new[]
                {
                    EquipmentIndex.Weapon0,
                    EquipmentIndex.Weapon1,
                    EquipmentIndex.Weapon2,
                    EquipmentIndex.Weapon3,
                    EquipmentIndex.Cape,
                    EquipmentIndex.HorseHarness
                };

                return GetEquipmentVariantsForBrowsing(slots,
                    (slot, item) =>
                    {
                        if (item == null)
                        {
                            return false;
                        }

                        if (slot is EquipmentIndex.Cape or EquipmentIndex.HorseHarness)
                        {
                            return true;
                        }

                        return item.WeaponComponent?.PrimaryWeapon?.IsShield == true;
                    });
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error in GetAccessoryVariantsForBrowsing", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        /// Get armor variants for one specific armor slot.
        /// </summary>
        public List<EquipmentVariantOption> GetArmorVariantsForBrowsing(EquipmentIndex slot)
        {
            try
            {
                if (slot is not (EquipmentIndex.Head or EquipmentIndex.Body or EquipmentIndex.Gloves or EquipmentIndex.Leg or EquipmentIndex.Cape))
                {
                    return new List<EquipmentVariantOption>();
                }

                return GetEquipmentVariantsForBrowsing(new[] { slot },
                    (_, item) => item?.ArmorComponent != null);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error in GetArmorVariantsForBrowsing(slot)", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        private List<EquipmentVariantOption> GetEquipmentVariantsForBrowsing(
            IEnumerable<EquipmentIndex> slots,
            Func<EquipmentIndex, ItemObject, bool> includeItem)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return new List<EquipmentVariantOption>();
            }

            var culture = enlistment.EnlistedLord?.Culture;
            var hero = Hero.MainHero;
            if (culture == null || hero == null)
            {
                return new List<EquipmentVariantOption>();
            }

            var allEquipment = GetAvailableEquipmentAllFormations(enlistment.EnlistmentTier, culture);
            var options = new List<EquipmentVariantOption>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var slot in slots)
            {
                if (!allEquipment.TryGetValue(slot, out var items))
                {
                    continue;
                }

                foreach (var item in items)
                {
                    if (item == null || !includeItem(slot, item) || !seen.Add(item.StringId))
                    {
                        continue;
                    }

                    options.Add(CreateBrowsingOption(hero, item, slot));
                }
            }

            return options
                .OrderBy(o => o.IsCurrent ? 0 : 1)
                .ThenBy(o => o.Item?.Name?.ToString())
                .ToList();
        }

        private EquipmentVariantOption CreateBrowsingOption(Hero hero, ItemObject item, EquipmentIndex slot)
        {
            var rolledQuality = RollItemQualityByReputation();
            var (modifier, actualQuality) = GetRandomModifierForQuality(item, rolledQuality);

            var basePrice = item.Value;
            if (modifier != null)
            {
                basePrice = (int)(basePrice * modifier.PriceMultiplier);
            }

            var price = CalculateQuartermasterPriceFromBase(basePrice);
            var quantityAvailable = _inventoryState?.GetAvailableQuantity(item.StringId) ?? 999;
            var isInStock = IsItemInStock(item) && quantityAvailable > 0;
            var allowsDuplicate = IsWeaponSlot(slot) || IsConsumableItem(item);

            string modifiedName = null;
            if (modifier != null)
            {
                modifiedName = $"{modifier.Name} {item.Name}";
            }

            return new EquipmentVariantOption
            {
                Item = item,
                Cost = price,
                IsCurrent = IsEquipped(hero, item, slot),
                CanAfford = hero.Gold >= price,
                Slot = slot,
                IsOfficerExclusive = false,
                AllowsDuplicatePurchase = allowsDuplicate,
                IsAtLimit = false,
                IsNewlyUnlocked = IsNewlyUnlockedItem(item),
                IsInStock = isInStock,
                QuantityAvailable = quantityAvailable,
                Modifier = modifier,
                Quality = actualQuality,
                ModifiedName = modifiedName
            };
        }

        private static bool IsEquipped(Hero hero, ItemObject item, EquipmentIndex slot)
        {
            if (hero == null || item == null)
            {
                return false;
            }

            if (IsWeaponSlot(slot))
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
        /// Get equipment available for a specific formation+tier+culture combination.
        /// This is the formation-filtered version for cases where you need only one formation's equipment.
        /// 
        /// For Quartermaster inventory stock (where players see ALL formations), use GetAvailableEquipmentAllFormations().
        /// For "newly unlocked items" tracking after promotion, this formation-specific method is appropriate.
        /// </summary>
        /// <param name="formation">Player's formation (infantry, archer, cavalry, horsearcher)</param>
        /// <param name="tierCap">Maximum tier to include (player's current tier)</param>
        /// <param name="culture">Player's enlisted lord's culture</param>
        /// <returns>Dictionary of equipment slots to available items</returns>
        public Dictionary<EquipmentIndex, List<ItemObject>> GetAvailableEquipmentByFormation(
            string formation,
            int tierCap,
            BasicCultureObject culture)
        {
            try
            {
                if (culture == null || string.IsNullOrWhiteSpace(formation))
                {
                    return new Dictionary<EquipmentIndex, List<ItemObject>>();
                }

                var formationLower = formation.ToLowerInvariant();
                var variants = new Dictionary<EquipmentIndex, List<ItemObject>>();
                var allTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

                // Scan all non-hero troops of the player's culture that match formation and are at or below tier
                foreach (var troop in allTroops)
                {
                    if (troop.IsHero || troop.Culture != culture)
                    {
                        continue;
                    }

                    var troopTier = troop.GetBattleTier();
                    if (troopTier < 1 || troopTier > tierCap)
                    {
                        continue;
                    }

                    // Check if troop matches the formation
                    var troopFormation = DetectTroopFormation(troop).ToString().ToLowerInvariant();
                    if (troopFormation != formationLower)
                    {
                        continue;
                    }

                    if (!troop.BattleEquipments.Any())
                    {
                        continue;
                    }

                    // Collect all equipment from this troop
                    foreach (var equipment in troop.BattleEquipments)
                    {
                        for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
                        {
                            var item = equipment[slot].Item;
                            if (item == null)
                            {
                                continue;
                            }

                            // Culture filter: if item has a culture, it must match
                            if (item.Culture != null && item.Culture != culture)
                            {
                                continue;
                            }

                            if (!variants.ContainsKey(slot))
                            {
                                variants[slot] = new List<ItemObject>();
                            }

                            if (!variants[slot].Contains(item))
                            {
                                variants[slot].Add(item);
                            }
                        }
                    }
                }

                var total = variants.Sum(kvp => kvp.Value.Count);
                ModLogger.Info("Quartermaster",
                    $"Formation-based equipment scan: {formation} T1-T{tierCap} {culture.Name} -> {total} items");

                return variants;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error getting formation equipment", ex);
                return new Dictionary<EquipmentIndex, List<ItemObject>>();
            }
        }

        /// <summary>
        /// Update the "newly unlocked items" set after a promotion.
        /// Call this when the player's tier changes.
        /// </summary>
        public void UpdateNewlyUnlockedItems()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                var tier = enlistment.EnlistmentTier;
                var culture = enlistment.EnlistedLord?.Culture;
                // Detect player's actual formation from equipment for proper equipment filtering
                var formation = GetPlayerFormationString();

                // Only update if tier actually changed
                if (tier <= _lastPromotionTier)
                {
                    return;
                }

                // Get current available items
                var currentEquipment = GetAvailableEquipmentByFormation(formation, tier, culture);
                var currentItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var slot in currentEquipment.Values)
                {
                    foreach (var item in slot)
                    {
                        _ = currentItemIds.Add(item.StringId);
                    }
                }

                // Find newly unlocked items
                _newlyUnlockedItems.Clear();
                foreach (var itemId in currentItemIds)
                {
                    if (!_previouslyAvailableItems.Contains(itemId))
                    {
                        _ = _newlyUnlockedItems.Add(itemId);
                    }
                }

                // Update the previous set for next time
                _previouslyAvailableItems.Clear();
                foreach (var itemId in currentItemIds)
                {
                    _ = _previouslyAvailableItems.Add(itemId);
                }

                _lastPromotionTier = tier;

                if (_newlyUnlockedItems.Count > 0)
                {
                    ModLogger.Info("QUARTERMASTER", $"Promotion to T{tier}: {_newlyUnlockedItems.Count} new items unlocked");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error updating newly unlocked items", ex);
            }
        }

        /// <summary>
        /// Check if an item is newly unlocked (for "NEW" indicators in UI).
        /// </summary>
        public bool IsNewlyUnlockedItem(ItemObject item)
        {
            if (item == null)
            {
                return false;
            }
            return _newlyUnlockedItems.Contains(item.StringId);
        }

        /// <summary>
        /// Clear the newly unlocked items set (call when player visits QM or equips items).
        /// </summary>
        public void ClearNewlyUnlockedMarkers()
        {
            _newlyUnlockedItems.Clear();
        }

        /// <summary>
        /// Get the currently selected troop for the player.
        ///
        /// Note: This method now uses formation+tier+culture to find a representative troop.
        /// The TroopSelectionManager.LastSelectedTroopId is no longer the primary lookup method.
        /// GetAvailableEquipmentByFormation() should be preferred for equipment queries.
        /// </summary>
        public CharacterObject GetPlayerSelectedTroop()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;

                if (enlistment?.IsEnlisted != true)
                {
                    return null;
                }

                // Detect player's actual formation from equipment for proper troop matching
                var formation = GetPlayerFormationString();
                var culture = enlistment.EnlistedLord?.Culture;
                var tier = enlistment.EnlistmentTier;

                if (culture == null)
                {
                    return null;
                }

                // Find a representative troop for this culture/tier/formation combination
                var allTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();
                var matchingTroops = allTroops.Where(troop =>
                    troop.Culture == culture &&
                    troop.GetBattleTier() == tier &&
                    !troop.IsHero &&
                    troop.BattleEquipments.Any() &&
                    DetectTroopFormation(troop).ToString().ToLowerInvariant() == formation.ToLowerInvariant()).ToList();

                // Select first matching troop as representative
                var selectedTroop = matchingTroops.FirstOrDefault();
                if (selectedTroop != null)
                {
                    ModLogger.Debug("QUARTERMASTER", $"Representative troop for {formation} T{tier}: {selectedTroop.Name}");
                }

                return selectedTroop;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error identifying player troop", ex);
                return null;
            }
        }

        /// <summary>
        /// Detect formation type from troop properties (matches TroopSelectionManager logic).
        /// </summary>
        private FormationType DetectTroopFormation(CharacterObject troop)
        {
            try
            {
                if (troop.IsRanged && troop.IsMounted)
                {
                    return FormationType.HorseArcher;
                }
                else if (troop.IsMounted)
                {
                    return FormationType.Cavalry;
                }
                else if (troop.IsRanged)
                {
                    return FormationType.Archer;
                }
                else
                {
                    return FormationType.Infantry;
                }
            }
            catch
            {
                return FormationType.Infantry;
            }
        }

        /// <summary>
        /// Detect player's current formation based on their equipped items.
        /// Used to determine which equipment category (infantry/cavalry/archer) to show.
        /// </summary>
        public FormationType DetectPlayerFormation()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero?.CharacterObject == null)
                {
                    return FormationType.Infantry;
                }

                var characterObject = hero.CharacterObject;

                // Check if player has horse + ranged = horse archer
                if (characterObject.IsRanged && characterObject.IsMounted)
                {
                    return FormationType.HorseArcher;
                }

                // Check if player has a horse equipped = cavalry
                if (characterObject.IsMounted)
                {
                    return FormationType.Cavalry;
                }

                // Check if player has ranged weapon = archer
                if (characterObject.IsRanged)
                {
                    return FormationType.Archer;
                }

                // Default to infantry
                return FormationType.Infantry;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error detecting player formation", ex);
                return FormationType.Infantry;
            }
        }

        /// <summary>
        /// Check if cavalry is available at the player's current tier for their culture.
        /// Cavalry becomes available when the culture's troop tree includes mounted troops at or below the player's tier.
        /// </summary>
        public bool IsCavalryUnlockedForPlayer()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return false;
                }

                var tier = enlistment.EnlistmentTier;
                var culture = enlistment.EnlistedLord?.Culture;

                if (culture == null)
                {
                    return false;
                }

                // Scan all troops from this culture at or below the player's tier
                var allTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

                foreach (var troop in allTroops)
                {
                    // Skip heroes and other cultures
                    if (troop.IsHero || troop.Culture != culture)
                    {
                        continue;
                    }

                    var troopTier = troop.GetBattleTier();
                    if (troopTier < 1 || troopTier > tier)
                    {
                        continue;
                    }

                    // Check if this troop is cavalry or horse archer
                    if (troop.IsMounted)
                    {
                        ModLogger.Debug("Quartermaster",
                            $"Cavalry unlocked: {troop.Name} is mounted at tier {troopTier} (player tier: {tier})");
                        return true;
                    }
                }

                ModLogger.Debug("Quartermaster",
                    $"Cavalry NOT unlocked: No mounted troops in {culture.Name} at tier {tier} or below");
                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error checking cavalry unlock", ex);
                return false;
            }
        }

        /// <summary>
        /// Get the player's formation as a lowercase string for equipment filtering.
        /// </summary>
        public string GetPlayerFormationString()
        {
            return DetectPlayerFormation().ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Calculate cost for requesting a specific equipment variant.
        /// Purchase-based: cost is derived from the item's base value and quartermaster pricing rules.
        /// </summary>
        public int CalculateVariantCost(ItemObject requestedItem, ItemObject currentItem, EquipmentIndex slot)
        {
            try
            {
                _ = currentItem;
                _ = slot;

                if (requestedItem == null)
                {
                    return 0;
                }

                return CalculateQuartermasterPrice(requestedItem);
            }
            catch
            {
                return 25; // Safe fallback cost
            }
        }

        /// <summary>
        /// Calculate the quartermaster purchase price for an item.
        /// Applies the configured soldier tax, plus any applicable duty/officer discounts.
        /// </summary>
        private int CalculateQuartermasterPrice(ItemObject item)
        {
            try
            {
                if (item == null)
                {
                    return 0;
                }

                var basePrice = item.Value;
                return CalculateQuartermasterPriceFromBase(basePrice);
            }
            catch
            {
                return 25; // Safe fallback
            }
        }

        /// <summary>
        /// Calculate the quartermaster purchase price from a base price value.
        /// Use this when the base price already includes item modifier price multipliers.
        /// Applies supply scarcity, QM reputation, and camp mood multipliers.
        ///
        /// Combined pricing formula:
        /// finalPrice = basePrice × supplyModifier × repModifier × campModifier
        /// </summary>
        private int CalculateQuartermasterPriceFromBase(int basePrice)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;

                // Supply scarcity pricing - low supply increases prices
                // 80-100% (Excellent): 1.0× | 60-79% (Good): 1.0× | 40-59% (Fair): 1.1× (+10%)
                // 30-39% (Low): 1.25× (+25%) | 0-29% (Critical): 1.5× (+50%)
                var supplyMultiplier = GetSupplyPriceMultiplier();

                // QM reputation-based pricing multiplier (0.70 = 30% discount to 1.40 = 40% markup)
                var repMultiplier = enlistment?.IsEnlisted == true
                    ? enlistment.GetEquipmentPriceMultiplier()
                    : 1.0f;

                // Camp mood provides small day-to-day price variation (0.98-1.15)
                var campMultiplier = CampLifeBehavior.Instance?.GetQuartermasterPurchaseMultiplier() ?? 1.0f;

                var price = basePrice * supplyMultiplier * repMultiplier * campMultiplier;
                var roundedPrice = Convert.ToInt32(MathF.Round(price));
                var finalPrice = Math.Max(5, roundedPrice);

                return finalPrice;
            }
            catch
            {
                return 25; // Safe fallback
            }
        }

        /// <summary>
        /// Get the supply-based price multiplier for scarcity pricing.
        /// Low supply increases equipment prices.
        /// </summary>
        private float GetSupplyPriceMultiplier()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var supplyLevel = enlistment?.CompanyNeeds?.Supplies ?? 100;

            // Supply scarcity pricing per spec
            if (supplyLevel >= 60)
            {
                return 1.0f; // Excellent/Good: no markup
            }
            if (supplyLevel >= 40)
            {
                return 1.1f; // Fair: +10% markup
            }
            if (supplyLevel >= 30)
            {
                return 1.25f; // Low: +25% markup
            }
            // Critical (< 30%): +50% markup
            return 1.5f;
        }

        /// <summary>
        /// Get the QM reputation-based price multiplier for Phase 7 spec.
        /// Reputation provides discounts (trusted) or markups (hostile).
        /// Range: 0.85× (Trusted, -15%) to 1.25× (Hostile, +25%)
        /// </summary>
        private float GetReputationPriceMultiplier()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return 1.0f; // Neutral when not enlisted
            }

            var qmRep = enlistment.GetQMReputation();

            // Phase 7 spec: Reputation affects pricing
            // Trusted (65+): 0.85× (-15%)
            // Friendly (35-64): 0.92× (-8%)
            // Neutral (10-34): 1.0× (base)
            // Wary (-10 to 9): 1.1× (+10%)
            // Hostile (< -10): 1.25× (+25%)

            if (qmRep >= 65)
            {
                return 0.85f; // Trusted: 15% discount
            }
            if (qmRep >= 35)
            {
                return 0.92f; // Friendly: 8% discount
            }
            if (qmRep >= 10)
            {
                return 1.0f; // Neutral: no modifier
            }
            if (qmRep >= -10)
            {
                return 1.1f; // Wary: 10% markup
            }
            // Hostile: 25% markup
            return 1.25f;
        }

        /// <summary>
        /// Calculate final price with Phase 7 combined pricing formula.
        /// Formula: basePrice × supplyModifier × repModifier
        /// </summary>
        public int CalculateFinalPrice(int basePrice)
        {
            try
            {
                var supplyModifier = GetSupplyPriceMultiplier();
                var repModifier = GetReputationPriceMultiplier();

                var finalPrice = basePrice * supplyModifier * repModifier;
                var roundedPrice = (int)MathF.Ceiling(finalPrice);

                return Math.Max(1, roundedPrice); // Minimum 1 denar
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error calculating final price", ex);
                return basePrice; // Fallback to base price
            }
        }

        /// <summary>
        /// Calculate buyback price for an equipment element, accounting for quality modifiers.
        /// Fine/Masterwork items sell for more, Rusty/Battered items sell for less.
        /// </summary>
        private int CalculateQuartermasterBuybackPrice(EquipmentElement element)
        {
            try
            {
                if (element.IsEmpty || element.Item == null)
                {
                    return 0;
                }

                // Calculate modified value: base price × modifier's price multiplier
                var basePrice = element.Item.Value;
                var modifierMultiplier = element.ItemModifier?.PriceMultiplier ?? 1.0f;
                var modifiedValue = (int)(basePrice * modifierMultiplier);

                var enlistment = EnlistmentBehavior.Instance;

                // QM reputation-based buyback multiplier (0.30 hostile to 0.65 trusted)
                var repMultiplier = enlistment?.IsEnlisted == true
                    ? enlistment.GetBuybackMultiplier()
                    : 0.5f;

                // Camp mood affects buyback rates
                var campMultiplier = CampLifeBehavior.Instance?.GetQuartermasterBuybackMultiplier() ?? 1.0f;

                var priceFloat = MathF.Max(0f, modifiedValue * repMultiplier * campMultiplier);
                var finalPrice = (int)priceFloat;

                // Debug logging showing calculation details
                var modifierName = element.ItemModifier?.Name?.ToString() ?? "Common";
                ModLogger.Debug("Quartermaster",
                    $"Buyback calc: {element.Item.Name} ({modifierName}) base={basePrice} × mod={modifierMultiplier:F2} × repMult={repMultiplier:F2} × campMult={campMultiplier:F2} = {finalPrice} denars");

                return Math.Max(finalPrice, 0);
            }
            catch
            {
                return 0;
            }
        }

        private void ChargeGold(Hero hero, int amount, ItemObject item)
        {
            try
            {
                var before = hero.Gold;
                GiveGoldAction.ApplyBetweenCharacters(hero, null, amount);
                ModLogger.Info("QUARTERMASTER", $"Charged {amount} denars for {item?.Name?.ToString() ?? "item"} (was {before}, now {hero.Gold})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error charging gold", ex);
            }
        }

        /// <summary>
        /// Maximum return options shown at once in the return menu.
        /// </summary>
        private const int MaxReturnOptions = 6;

        /// <summary>
        /// Process equipment variant purchase and update player equipment.
        /// Purchases are priced; limited to 2 weapons/consumables, 1 armor/accessory.
        /// For weapons and consumables, finds the next available slot if the requested slot is occupied.
        /// </summary>
        public void RequestEquipmentVariant(EquipmentVariantOption variant)
        {
            try
            {
                if (variant?.Item == null)
                {
                    // This is a genuine bad state but can be hit repeatedly via UI retry flows.
                    // Keep it high-signal: log once per session with a stable code.
                    ModLogger.Expected("QUARTERMASTER", "qm_variant_null",
                        "Equipment request failed - variant or item is null");
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    ModLogger.Warn("QUARTERMASTER", "Equipment request blocked: player not enlisted");
                    return;
                }

                // SUPPLY GATE - purchases blocked when company supply is critically low (< 30%)
                const int criticalSupplyThreshold = 30;
                var supplyLevel = enlistment.CompanyNeeds?.Supplies ?? 100;
                if (supplyLevel < criticalSupplyThreshold)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_supply_blocked}We can't issue equipment right now. Supplies are critically low.").ToString(), Colors.Red));
                    ModLogger.Info("QUARTERMASTER", $"Purchase blocked: Supply at {supplyLevel}% (threshold: {criticalSupplyThreshold}%)");
                    return;
                }

                var hero = Hero.MainHero;
                var requestedItem = variant.Item;
                var slot = variant.Slot;

                // If the player selected their currently-equipped non-weapon item, treat as no-op (avoid charging / affordability warnings).
                // Weapons are handled differently because duplicates can be purchased into other weapon slots.
                if (variant.IsCurrent && !IsWeaponSlot(slot) && !IsConsumableItem(requestedItem))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_already_equipped}Already equipped.").ToString(), Colors.Yellow));
                    return;
                }

                // OUT OF STOCK CHECK - item availability is rolled at muster based on supply levels
                if (!variant.IsInStock)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_out_of_stock}This item is out of stock. Check back after the next muster.").ToString(), Colors.Red));
                    ModLogger.Debug("QUARTERMASTER", $"Purchase blocked: {requestedItem.Name} is out of stock");
                    return;
                }

                // PRICE CHECK
                var cost = variant.Cost > 0 ? variant.Cost : CalculateQuartermasterPrice(requestedItem);
                if (hero.Gold < cost)
                {
                    var msg = new TextObject("{=qm_cannot_afford}You can't afford this. Cost: {COST} denars.");
                    _ = msg.SetTextVariable("COST", cost);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Red));
                    return;
                }

                // For weapons and consumables, find the best slot to place the item
                // This prevents overwriting the same slot when player wants multiple items
                var targetSlot = slot;
                var addedToInventory = false;

                if (IsWeaponSlot(slot) || IsConsumableItem(requestedItem))
                {
                    targetSlot = FindBestWeaponSlot(hero, requestedItem, slot);

                    // If no empty slot available, add to inventory instead of blocking
                    if (targetSlot == EquipmentIndex.None)
                    {
                        // Add to party inventory since hands are full
                        var partyInventory = PartyBase.MainParty?.ItemRoster;
                        if (partyInventory != null)
                        {
                            // Create equipment element with modifier if present
                            var equipmentElement = variant.Modifier != null
                                ? new EquipmentElement(requestedItem, variant.Modifier)
                                : new EquipmentElement(requestedItem);
                            _ = partyInventory.AddToCounts(equipmentElement, 1);
                            addedToInventory = true;

                            var inventoryDisplayName = !string.IsNullOrEmpty(variant.ModifiedName) ? variant.ModifiedName : requestedItem.Name.ToString();
                            var inventoryMsg = new TextObject("{=qm_added_to_inventory}{ITEM_NAME} stowed in your pack. Hands full.");
                            _ = inventoryMsg.SetTextVariable("ITEM_NAME", inventoryDisplayName);
                            InformationManager.DisplayMessage(new InformationMessage(inventoryMsg.ToString(), Colors.Yellow));
                            ModLogger.Info("QUARTERMASTER", $"Weapon slots full - {requestedItem.Name} (quality: {variant.Quality}) added to inventory");
                        }
                        else
                        {
                            // Fallback if inventory unavailable (shouldn't happen)
                            var noSlotsMsg = new TextObject("{=qm_no_weapon_slots}Your hands are full, soldier. Sell something back to the quartermaster first.");
                            InformationManager.DisplayMessage(new InformationMessage(noSlotsMsg.ToString(), Colors.Yellow));
                            ModLogger.Info("QUARTERMASTER", $"No available weapon slot for {requestedItem.Name}");
                            return;
                        }
                    }
                }

                // If we added to inventory, skip equipment slot change
                if (addedToInventory)
                {
                    // Charge cost
                    ChargeGold(hero, cost, requestedItem);

                    // Phase 7: Decrement inventory stock
                    _ = (_inventoryState?.TryPurchase(requestedItem.StringId));

                    return;
                }

                var currentItem = hero.BattleEquipment[targetSlot].Item;
                var previousItemName = currentItem?.Name?.ToString() ?? "empty";

                // No-op: already equipped in that slot.
                if (currentItem != null && currentItem.StringId == requestedItem.StringId)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_already_equipped}Already equipped.").ToString(), Colors.Yellow));
                    return;
                }

                // Preserve the replaced item (purchase-based system: don't delete player property).
                // For weapons, FindBestWeaponSlot only returns empty slots, so this typically applies to armor/mount slots.
                var roster = PartyBase.MainParty?.ItemRoster;
                if (roster != null && !hero.BattleEquipment[targetSlot].IsEmpty && hero.BattleEquipment[targetSlot].Item != null)
                {
                    _ = roster.AddToCounts(hero.BattleEquipment[targetSlot], 1);
                }

                // Apply the equipment change to the target slot with modifier
                ApplyEquipmentSlotChange(hero, requestedItem, variant.Modifier, targetSlot);

                // Success notification with quality modifier name if present
                var itemDisplayName = !string.IsNullOrEmpty(variant.ModifiedName) ? variant.ModifiedName : requestedItem.Name.ToString();
                var successMessage = new TextObject("{=qm_equipment_issued_buy}Purchased {ITEM_NAME} for {COST} denars.");
                _ = successMessage.SetTextVariable("ITEM_NAME", itemDisplayName);
                _ = successMessage.SetTextVariable("COST", cost);
                InformationManager.DisplayMessage(new InformationMessage(successMessage.ToString()));

                // Charge cost
                ChargeGold(hero, cost, requestedItem);

                // Phase 7: Decrement inventory stock
                _ = (_inventoryState?.TryPurchase(requestedItem.StringId));

                ModLogger.Info("QUARTERMASTER", $"Purchased {requestedItem.Name} for {cost} denars to slot {targetSlot} (replaced {previousItemName})");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("QUARTERMASTER", "Error processing equipment variant request", ex);
            }
        }

        /// <summary>
        /// Find the best available weapon slot for an item.
        /// Only returns empty slots - does not replace existing items.
        /// When all slots are full, returns None so caller can add to inventory.
        /// </summary>
        private static EquipmentIndex FindBestWeaponSlot(Hero hero, ItemObject newItem, EquipmentIndex preferredSlot)
        {
            _ = newItem; // Item type not needed since we only look for empty slots

            // Weapon slots are Weapon0 through Weapon3
            var weaponSlots = new[] { EquipmentIndex.Weapon0, EquipmentIndex.Weapon1, EquipmentIndex.Weapon2, EquipmentIndex.Weapon3 };

            // Priority 1: Use the preferred slot if it's empty
            if (IsWeaponSlot(preferredSlot) && hero.BattleEquipment[preferredSlot].IsEmpty)
            {
                return preferredSlot;
            }

            // Priority 2: Find any empty weapon slot
            foreach (var weaponSlot in weaponSlots)
            {
                if (hero.BattleEquipment[weaponSlot].IsEmpty)
                {
                    return weaponSlot;
                }
            }

            // All weapon slots are occupied - return None so caller can add to inventory
            return EquipmentIndex.None;
        }

        /// <summary>
        /// Count how many of a specific item the player currently has in equipment and inventory.
        /// Used to enforce the 2-item-per-type limit to prevent abuse.
        /// </summary>
        private static int GetPlayerItemCount(Hero hero, string itemStringId)
        {
            if (hero == null || string.IsNullOrEmpty(itemStringId))
            {
                return 0;
            }

            var count = 0;

            // Check battle equipment (all slots including weapons, armor, horse)
            for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
            {
                if (hero.BattleEquipment[slot].Item?.StringId == itemStringId)
                {
                    count++;
                }
            }

            // Also check civilian equipment
            for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
            {
                if (hero.CivilianEquipment[slot].Item?.StringId == itemStringId)
                {
                    count++;
                }
            }

            // Check party inventory for the same item
            var partyInventory = PartyBase.MainParty?.ItemRoster;
            if (partyInventory != null)
            {
                // IMPORTANT: ItemRoster can contain multiple elements for the same ItemObject with different modifiers.
                // GetItemNumber(ItemObject) only returns the count of the first matching element, not the total across modifiers.
                // Sum all matching roster elements by StringId to get an accurate total.
                for (var i = 0; i < partyInventory.Count; i++)
                {
                    var element = partyInventory.GetElementCopyAtIndex(i);
                    if (element.Amount <= 0)
                    {
                        continue;
                    }

                    var rosterItem = element.EquipmentElement.Item;
                    if (rosterItem?.StringId == itemStringId)
                    {
                        count += element.Amount;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Build a list of returnable items from the player's equipped gear.
        /// Only includes items currently equipped (battle and civilian equipment).
        /// Party inventory (baggage train) is excluded to prevent selling army supplies or loot.
        /// Groups items by both StringId AND modifier, so "Fine Sword" and "Rusty Sword" are separate entries.
        /// </summary>
        private List<ReturnOption> BuildReturnOptions()
        {
            var hero = Hero.MainHero;
            if (hero == null)
            {
                return new List<ReturnOption>();
            }

            // Group by (StringId + Modifier) to separate quality variants
            var optionsByKey = new Dictionary<string, ReturnOption>(StringComparer.Ordinal);

            void TryAddElement(EquipmentElement element)
            {
                if (element.IsEmpty || !IsReturnableItem(element.Item))
                {
                    return;
                }

                var key = ReturnOption.GetGroupKey(element);
                if (optionsByKey.TryGetValue(key, out var existing))
                {
                    existing.Count++;
                }
                else
                {
                    optionsByKey[key] = new ReturnOption { Element = element, Count = 1 };
                }
            }

            // Battle equipment - only items currently equipped by the player
            for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
            {
                TryAddElement(hero.BattleEquipment[slot]);
            }

            // Civilian equipment - only items currently equipped by the player
            for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
            {
                TryAddElement(hero.CivilianEquipment[slot]);
            }

            // NOTE: Party inventory (baggage train) is intentionally excluded from selling.
            // The quartermaster only buys back equipment currently equipped by the soldier.
            // This prevents players from selling army supplies or loot from the baggage train.

            // OLD CODE (REMOVED - was allowing baggage train items to be sold):
            // var roster = PartyBase.MainParty?.ItemRoster;
            // if (roster != null)
            // {
            //     for (var i = 0; i < roster.Count; i++)
            //     {
            //         var rosterElement = roster.GetElementCopyAtIndex(i);
            //         if (rosterElement.Amount <= 0)
            //         {
            //             continue;
            //         }
            //
            //         var key = ReturnOption.GetGroupKey(rosterElement.EquipmentElement);
            //         if (optionsByKey.TryGetValue(key, out var existing))
            //         {
            //             existing.Count += rosterElement.Amount;
            //         }
            //         else
            //         {
            //             optionsByKey[key] = new ReturnOption
            //             {
            //                 Element = rosterElement.EquipmentElement,
            //                 Count = rosterElement.Amount
            //             };
            //         }
            //     }
            // }

            return optionsByKey.Values
                .OrderByDescending(o => o.Count)
                .ThenBy(o => o.Item?.Name?.ToString())
                .Take(MaxReturnOptions)
                .ToList();
        }

        /// <summary>
        /// Determine if an item is valid for return (equipment/weapon/mount/consumable).
        /// </summary>
        private static bool IsReturnableItem(ItemObject item)
        {
            if (item == null)
            {
                return false;
            }

            // Never allow quest-critical items (e.g., Dragon Banner) to be returned
            if (NonReturnableQuestItemIds.Contains(item.StringId))
            {
                return false;
            }

            return item.PrimaryWeapon != null ||
                   item.ArmorComponent != null ||
                   item.HorseComponent != null;
        }

        /// <summary>
        /// Attempt to return a single item from inventory or equipment.
        /// Matches by both StringId and modifier to remove the exact quality variant.
        /// </summary>
        private bool TryReturnSingleItem(EquipmentElement element)
        {
            if (element.IsEmpty || element.Item == null)
            {
                return false;
            }

            // Prefer removing from inventory first to avoid stripping equipped gear if possible
            if (TryRemoveFromInventory(element))
            {
                return true;
            }

            var hero = Hero.MainHero;
            if (hero != null && TryRemoveFromEquipment(hero, element))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove a specific equipment element (including modifier) from party inventory.
        /// </summary>
        private bool TryRemoveFromInventory(EquipmentElement targetElement)
        {
            var roster = PartyBase.MainParty?.ItemRoster;
            if (roster == null || targetElement.IsEmpty)
            {
                return false;
            }

            var targetKey = ReturnOption.GetGroupKey(targetElement);

            // Find an element matching both StringId and modifier
            for (var i = 0; i < roster.Count; i++)
            {
                var rosterEntry = roster.GetElementCopyAtIndex(i);
                if (rosterEntry.Amount <= 0)
                {
                    continue;
                }

                var ee = rosterEntry.EquipmentElement;
                if (ReturnOption.GetGroupKey(ee) == targetKey)
                {
                    _ = roster.AddToCounts(ee, -1);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove a specific equipment element (including modifier) from hero equipment slots.
        /// </summary>
        private bool TryRemoveFromEquipment(Hero hero, EquipmentElement targetElement)
        {
            var targetKey = ReturnOption.GetGroupKey(targetElement);

            // Battle equipment first
            var battleEquipment = hero.BattleEquipment.Clone();
            for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
            {
                if (ReturnOption.GetGroupKey(battleEquipment[slot]) == targetKey)
                {
                    battleEquipment[slot] = default;
                    EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, battleEquipment);
                    return true;
                }
            }

            // Civilian equipment
            var civilianEquipment = hero.CivilianEquipment.Clone();
            for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
            {
                if (ReturnOption.GetGroupKey(civilianEquipment[slot]) == targetKey)
                {
                    civilianEquipment[slot] = default;
                    hero.CivilianEquipment.FillFrom(civilianEquipment, false);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Show a popup inquiry for selling equipment.
        /// Replaces the old GameMenu-based sell system with a conversation-compatible popup.
        /// Called from the QM conversation when player selects "I want to sell something."
        /// </summary>
        public void ShowSellPopup()
        {
            try
            {
                var sellableItems = BuildReturnOptions();

                if (sellableItems.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_return_none}No equipment to sell.").ToString()));
                    RestartQuartermasterConversationFromPopup();
                    return;
                }

                // Build inquiry elements for each sellable item (including quality variants)
                var elements = new List<InquiryElement>();
                foreach (var option in sellableItems.Take(6)) // Max 6 items like the old menu
                {
                    var buybackPrice = CalculateQuartermasterBuybackPrice(option.Element);

                    // Build display name with quality prefix if modifier exists
                    var itemName = option.Item?.Name?.ToString() ?? "Unknown";
                    var modifierPrefix = option.Modifier?.Name?.ToString();
                    var displayName = string.IsNullOrEmpty(modifierPrefix)
                        ? itemName
                        : $"{modifierPrefix} {itemName}";

                    var displayText = $"{displayName} (x{option.Count}) - {buybackPrice} denars";

                    // Store the full EquipmentElement as identifier to preserve modifier info
                    elements.Add(new InquiryElement(option.Element, displayText, null, true,
                        new TextObject("{=qm_sell_tooltip}Sell one item to the quartermaster.").ToString()));
                }

                // Add "Done" option
                elements.Add(new InquiryElement("done",
                    new TextObject("{=qm_sell_done}Done selling").ToString(),
                    null, true,
                    new TextObject("{=qm_sell_done_tooltip}Return to the quartermaster.").ToString()));

                var inquiry = new MultiSelectionInquiryData(
                    new TextObject("{=qm_sell_title}Sell Equipment").ToString(),
                    new TextObject("{=qm_sell_description}Select an item to sell back to the quartermaster.").ToString(),
                    elements,
                    true, // isExitShown (X button at top)
                    1,    // minSelectableOptionCount
                    1,    // maxSelectableOptionCount
                    new TextObject("{=qm_sell_confirm}Sell").ToString(),
                    null, // No cancel button (X button handles closing)
                    OnSellPopupConfirm,
                    OnSellPopupCancel);

                MBInformationManager.ShowMultiSelectionInquiry(inquiry);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error showing sell popup", ex);
                RestartQuartermasterConversationFromMenu();
            }
        }

        /// <summary>
        /// Handle sell popup confirmation.
        /// </summary>
        private void OnSellPopupConfirm(List<InquiryElement> selectedElements)
        {
            try
            {
                if (selectedElements == null || selectedElements.Count == 0)
                {
                    RestartQuartermasterConversationFromPopup();
                    return;
                }

                var selected = selectedElements[0];

                // Check if "Done" was selected
                if (selected.Identifier is "done")
                {
                    RestartQuartermasterConversationFromPopup();
                    return;
                }

                // Process the sale - EquipmentElement is passed as identifier to preserve modifier info
                if (selected.Identifier is EquipmentElement { IsEmpty: false } element)
                {
                    var removed = TryReturnSingleItem(element);
                    var buyback = removed ? CalculateQuartermasterBuybackPrice(element) : 0;

                    // Build display name with modifier prefix
                    var itemName = element.Item?.Name?.ToString() ?? "Unknown";
                    var modifierPrefix = element.ItemModifier?.Name?.ToString();
                    var displayName = string.IsNullOrEmpty(modifierPrefix)
                        ? itemName
                        : $"{modifierPrefix} {itemName}";

                    if (removed && buyback > 0)
                    {
                        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, buyback);

                        var message = new TextObject("{=qm_return_success}Sold {ITEM_NAME} for {AMOUNT} denars.");
                        _ = message.SetTextVariable("ITEM_NAME", displayName);
                        _ = message.SetTextVariable("AMOUNT", buyback);
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Yellow));

                        ModLogger.Info("QUARTERMASTER", $"Sold {displayName} for {buyback} denars via popup");
                        ModLogger.IncrementSummary("quartermaster_buyback");
                    }
                    else
                    {
                        var message = new TextObject("{=qm_return_none_left}No {ITEM_NAME} remains to sell.");
                        _ = message.SetTextVariable("ITEM_NAME", displayName);
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Red));
                    }

                    // Show popup again for more sales
                    ShowSellPopup();
                }
                else
                {
                    RestartQuartermasterConversationFromPopup();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error processing sell popup selection", ex);
                RestartQuartermasterConversationFromPopup();
            }
        }

        /// <summary>
        /// Handle sell popup cancellation.
        /// </summary>
        private void OnSellPopupCancel(List<InquiryElement> selectedElements)
        {
            RestartQuartermasterConversationFromPopup();
        }

        /// <summary>
        /// Restart the quartermaster conversation from a popup context.
        /// Unlike RestartQuartermasterConversationFromMenu, this does NOT call GameMenu.ExitToLast()
        /// because popups are shown over conversations, not over GameMenus.
        /// </summary>
        private static void RestartQuartermasterConversationFromPopup()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                var qmHero = enlistment?.GetOrCreateQuartermaster();

                if (qmHero is { IsAlive: true })
                {
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        try
                        {
                            var party = QuartermasterPartyResolver.GetConversationParty(qmHero);
                            if (party == null)
                            {
                                ModLogger.Expected("QUARTERMASTER", "qm_party_no_party_popup",
                                    "Both QM and enlisted lord have no party — cannot restart QM conversation from popup with correct scene");
                                InformationManager.DisplayMessage(new InformationMessage(
                                    new TextObject("{=qm_party_unavailable}The quartermaster cannot be reached right now.").ToString()));
                                return;
                            }

                            var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
                            var qmData = new ConversationCharacterData(qmHero.CharacterObject, party);

                            // Use sea conversation scene if at sea, otherwise use map conversation
                            if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
                            {
                                const string seaConversationScene = "conversation_scene_sea_multi_agent";
                                _ = CampaignMission.OpenConversationMission(playerData, qmData, seaConversationScene);
                            }
                            else
                            {
                                CampaignMapConversation.OpenConversation(playerData, qmData);
                            }
                            ModLogger.Debug("QUARTERMASTER", "Restarted QM conversation from popup");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("QUARTERMASTER", "Failed to restart QM conversation from popup", ex);
                            EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                        }
                    });
                }
                else
                {
                    ModLogger.Warn("QUARTERMASTER", "QM hero unavailable after popup, returning to Camp Hub");
                    EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error returning from popup", ex);
                EnlistedMenuBehavior.SafeActivateEnlistedMenu();
            }
        }

        /// <summary>
        /// Apply equipment change to a specific slot while preserving other equipment.
        /// Uses safe cloning to avoid corrupting player equipment.
        /// Applies item modifier if provided (quality tier: Poor/Inferior/Fine/etc).
        /// </summary>
        private void ApplyEquipmentSlotChange(Hero hero, ItemObject newItem, ItemModifier modifier, EquipmentIndex slot)
        {
            try
            {
                // Clone current equipment to preserve other slots
                var newEquipment = hero.BattleEquipment.Clone(); // Default cloneWithoutWeapons=false is sufficient

                // Replace only the requested slot, applying modifier if present
                newEquipment[slot] = modifier != null
                    ? new EquipmentElement(newItem, modifier)
                    : new EquipmentElement(newItem);

                // Apply the updated equipment
                EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, newEquipment);

                // Equipment change is applied via EquipmentHelper which handles visual refresh
                // The hero's equipment is updated immediately and visible in the game world

                var modifierInfo = modifier != null ? $" with modifier '{modifier.Name}'" : "";
                ModLogger.Info("QUARTERMASTER", $"Equipment slot {slot} updated with {newItem.Name}{modifierInfo}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error applying equipment slot change", ex);
            }
        }

        /// <summary>
        /// Build equipment variant options with pricing and availability.
        /// Provides full equipment variant access for quartermaster officers.
        /// </summary>
        public void SetFilterToHorseAndTack()
        {
            // Compatibility no-op: browsing now resolves horse/tack variants directly from live data.
        }

        /// <summary>
        /// Sets filter to show officer-grade equipment (premium items for T7+ players).
        /// Enables access to culture-wide equipment variants beyond standard troop equipment.
        /// </summary>
        public void SetFilterToOfficerEquipment()
        {
            // Compatibility no-op: officer access is enforced when options are built, not by a cached flag.
        }

        /// <summary>
        /// Check if Officers' Armory is accessible to the player.
        /// Requirements: T7+ rank, QM Rep 60+
        /// </summary>
        public bool IsOfficersArmoryAccessible()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return false;
            }

            var playerTier = enlistment.EnlistmentTier;
            var qmRep = enlistment.GetQMReputation();

            return playerTier >= 7 && qmRep >= 60;
        }

        // ========================================================================
        // RATIONS/FOOD SYSTEM
        // ========================================================================

        /// <summary>
        /// Initialize rations purchase menu.
        /// </summary>
        private void OnQuartermasterRationsInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var enlistment = EnlistmentBehavior.Instance;
                var backgroundMesh = "encounter_looter";

                if (enlistment?.CurrentLord?.Clan?.Kingdom?.Culture?.EncounterBackgroundMesh != null)
                {
                    backgroundMesh = enlistment.CurrentLord.Clan.Kingdom.Culture.EncounterBackgroundMesh;
                }
                else if (enlistment?.CurrentLord?.Culture?.EncounterBackgroundMesh != null)
                {
                    backgroundMesh = enlistment.CurrentLord.Culture.EncounterBackgroundMesh;
                }

                args.MenuContext.SetBackgroundMeshName(backgroundMesh);

                var sb = new StringBuilder();
                _ = sb.AppendLine(new TextObject("{=qm_rations_intro}Purchase better rations from the quartermaster.").ToString());
                _ = sb.AppendLine(new TextObject("{=qm_rations_desc}Higher quality food provides morale bonuses.").ToString());
                _ = sb.AppendLine();

                // Show current status
                if (enlistment != null)
                {
                    var (qualityName, daysRemaining) = enlistment.GetFoodQualityInfo();  // Morale bonus removed

                    if (daysRemaining > 0)
                    {
                        var statusText = new TextObject("{=qm_rations_current}Current: {QUALITY} - {DAYS} days remaining");
                        _ = statusText.SetTextVariable("QUALITY", qualityName);
                        _ = statusText.SetTextVariable("DAYS", ((int)Math.Ceiling(daysRemaining)).ToString());
                        _ = sb.AppendLine(statusText.ToString());
                    }
                    else
                    {
                        _ = sb.AppendLine(new TextObject("{=qm_rations_standard}Current: Standard army rations (no bonus)").ToString());
                    }

                    _ = sb.AppendLine();
                    _ = sb.AppendLine(new TextObject("{=qm_rations_gold}Your gold: {GOLD_ICON} {GOLD}").ToString());
                    MBTextManager.SetTextVariable("GOLD", Hero.MainHero.Gold);

                    // Show retinue provisioning status.
                    if (enlistment.HasRetinueToProvision())
                    {
                        _ = sb.AppendLine();
                        _ = sb.AppendLine("â”€â”€â”€ Retinue Provisioning â”€â”€â”€");

                        var retinueManager = RetinueManager.Instance;
                        var soldierCount = retinueManager?.State?.TotalSoldiers ?? 0;

                        var (retinueName, retinueMorale, retinueDays) = enlistment.GetRetinueProvisioningInfo();

                        if (retinueDays > 0)
                        {
                            _ = sb.AppendLine($"Retinue ({soldierCount} soldiers): {retinueName} ({(retinueMorale >= 0 ? "+" : "")}{retinueMorale} morale)");
                            _ = sb.AppendLine($"Days remaining: {retinueDays:F1}");
                        }
                        else
                        {
                            _ = sb.AppendLine($"Retinue ({soldierCount} soldiers): NOT PROVISIONED!");
                            _ = sb.AppendLine("Your soldiers are starving! (-10 morale)");
                        }
                    }
                }

                MBTextManager.SetTextVariable("RATIONS_TEXT", sb.ToString());
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error initializing rations menu", ex);
                MBTextManager.SetTextVariable("RATIONS_TEXT",
                    new TextObject("{=qm_rations_error}Provisions unavailable.").ToString());
            }
        }

        /// <summary>
        /// Add rations purchase menu options.
        /// </summary>
        private void AddRationsMenuOptions(CampaignGameStarter starter)
        {
            // Supplemental Rations - 10g, +2 morale, 1 day
            starter.AddGameMenuOption("quartermaster_rations", "rations_supplemental",
                new TextObject("{=qm_rations_supplemental}Supplemental Rations (10{GOLD_ICON}) - +2 morale for 1 day").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    var enlistment = EnlistmentBehavior.Instance;
                    var cost = enlistment?.IsEnlisted == true ? enlistment.ApplyQuartermasterDiscount(10) : 10;
                    args.Text = new TextObject($"Supplemental Rations ({cost}{{GOLD_ICON}}) - +2 morale for 1 day");

                    var canAfford = Hero.MainHero.Gold >= cost;
                    if (!canAfford)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=qm_rations_no_gold}Not enough gold.");
                    }
                    return true;
                },
                _ => OnPurchaseRations(EnlistmentBehavior.FoodQualityTier.Supplemental, 10, 1),
                false, 1);

            // Officer's Fare - 30g, +4 morale, 2 days
            starter.AddGameMenuOption("quartermaster_rations", "rations_officer",
                new TextObject("{=qm_rations_officer}Officer's Fare (30{GOLD_ICON}) - +4 morale for 2 days").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    var enlistment = EnlistmentBehavior.Instance;
                    var cost = enlistment?.IsEnlisted == true ? enlistment.ApplyQuartermasterDiscount(30) : 30;
                    args.Text = new TextObject($"Officer's Fare ({cost}{{GOLD_ICON}}) - +4 morale for 2 days");

                    var canAfford = Hero.MainHero.Gold >= cost;
                    if (!canAfford)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=qm_rations_no_gold}Not enough gold.");
                    }
                    return true;
                },
                _ => OnPurchaseRations(EnlistmentBehavior.FoodQualityTier.Officer, 30, 2),
                false, 2);

            // Commander's Feast - 75g, +8 morale, 3 days
            starter.AddGameMenuOption("quartermaster_rations", "rations_commander",
                new TextObject("{=qm_rations_commander}Commander's Feast (75{GOLD_ICON}) - +8 morale for 3 days").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    var enlistment = EnlistmentBehavior.Instance;
                    var cost = enlistment?.IsEnlisted == true ? enlistment.ApplyQuartermasterDiscount(75) : 75;
                    args.Text = new TextObject($"Commander's Feast ({cost}{{GOLD_ICON}}) - +8 morale for 3 days");

                    var canAfford = Hero.MainHero.Gold >= cost;
                    var highEnoughTier = enlistment?.EnlistmentTier >= 4;

                    if (!canAfford)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=qm_rations_no_gold}Not enough gold.");
                    }
                    else if (!highEnoughTier)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=qm_rations_rank}Reserved for Tier 4+ soldiers.");
                    }
                    return true;
                },
                _ => OnPurchaseRations(EnlistmentBehavior.FoodQualityTier.Commander, 75, 3),
                false, 3);

            // ========================================
            // RETINUE PROVISIONING OPTIONS
            // Only shown for T7+ commanders with retinue
            // ========================================

            // Section header for retinue provisioning
            starter.AddGameMenuOption("quartermaster_rations", "retinue_header",
                new TextObject("{=qm_retinue_header}â€” RETINUE PROVISIONING â€”").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                    var enlistment = EnlistmentBehavior.Instance;
                    return enlistment?.HasRetinueToProvision() == true;
                },
                _ => { }, // No action for header
                false, 10);

            // Bare Minimum - lowest cost, morale penalty
            starter.AddGameMenuOption("quartermaster_rations", "retinue_bare",
                "{=qm_retinue_bare}Retinue: Bare Minimum (-5 morale)",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return SetupRetinueProvisioningOption(args, EnlistmentBehavior.RetinueProvisioningTier.BareMinimum);
                },
                _ => OnPurchaseRetinueProvisioning(EnlistmentBehavior.RetinueProvisioningTier.BareMinimum),
                false, 11);

            // Standard - default quality
            starter.AddGameMenuOption("quartermaster_rations", "retinue_standard",
                "{=qm_retinue_standard}Retinue: Standard Rations",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return SetupRetinueProvisioningOption(args, EnlistmentBehavior.RetinueProvisioningTier.Standard);
                },
                _ => OnPurchaseRetinueProvisioning(EnlistmentBehavior.RetinueProvisioningTier.Standard),
                false, 12);

            // Good Fare - morale bonus
            starter.AddGameMenuOption("quartermaster_rations", "retinue_good",
                "{=qm_retinue_good}Retinue: Good Fare (+5 morale)",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return SetupRetinueProvisioningOption(args, EnlistmentBehavior.RetinueProvisioningTier.GoodFare);
                },
                _ => OnPurchaseRetinueProvisioning(EnlistmentBehavior.RetinueProvisioningTier.GoodFare),
                false, 13);

            // Officer Quality - best morale bonus
            starter.AddGameMenuOption("quartermaster_rations", "retinue_officer",
                "{=qm_retinue_officer}Retinue: Officer Quality (+10 morale)",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return SetupRetinueProvisioningOption(args, EnlistmentBehavior.RetinueProvisioningTier.OfficerQuality);
                },
                _ => OnPurchaseRetinueProvisioning(EnlistmentBehavior.RetinueProvisioningTier.OfficerQuality),
                false, 14);

            // Return to quartermaster conversation
            starter.AddGameMenuOption("quartermaster_rations", "rations_back",
                new TextObject("{=qm_menu_supplies_back}Return to quartermaster").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => RestartQuartermasterConversationFromMenu(),
                false, 99);
        }

        /// <summary>
        /// Handle rations purchase.
        /// </summary>
        private void OnPurchaseRations(EnlistmentBehavior.FoodQualityTier tier, int cost, int durationDays)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_rations_not_enlisted}You must be enlisted to purchase provisions.").ToString()));
                    return;
                }

                // Cost shown to the player should reflect relationship discount (if any).
                var effectiveCost = enlistment.ApplyQuartermasterDiscount(cost);

                if (enlistment.PurchaseRations(tier, cost, durationDays))
                {
                    var tierName = tier switch
                    {
                        EnlistmentBehavior.FoodQualityTier.Supplemental => "Supplemental Rations",
                        EnlistmentBehavior.FoodQualityTier.Officer => "Officer's Fare",
                        EnlistmentBehavior.FoodQualityTier.Commander => "Commander's Feast",
                        _ => "Rations"
                    };

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Purchased {tierName} for {effectiveCost} gold ({durationDays} days).",
                        Colors.Green));

                    // Refresh menu to show updated status
                    ActivateMenuPreserveTime("quartermaster_rations");
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_rations_no_gold}Not enough gold.").ToString(),
                        Colors.Red));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error purchasing rations", ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=qm_rations_error}Failed to purchase provisions.").ToString(),
                    Colors.Red));
            }
        }

        // ========================================================================
        // RETINUE PROVISIONING METHODS
        // ========================================================================

        /// <summary>
        /// Sets up a retinue provisioning menu option with dynamic cost text.
        /// Returns true if the option should be visible.
        /// </summary>
        private bool SetupRetinueProvisioningOption(MenuCallbackArgs args, EnlistmentBehavior.RetinueProvisioningTier tier)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.HasRetinueToProvision())
            {
                return false;
            }

            var retinueManager = RetinueManager.Instance;
            var soldierCount = retinueManager?.State?.TotalSoldiers ?? 0;
            var baseCost = EnlistmentBehavior.GetRetinueProvisioningCost(tier, soldierCount);
            var cost = enlistment.ApplyQuartermasterDiscount(baseCost);

            // Set dynamic text with cost
            var tierName = tier switch
            {
                EnlistmentBehavior.RetinueProvisioningTier.BareMinimum => "Bare Minimum",
                EnlistmentBehavior.RetinueProvisioningTier.Standard => "Standard",
                EnlistmentBehavior.RetinueProvisioningTier.GoodFare => "Good Fare",
                EnlistmentBehavior.RetinueProvisioningTier.OfficerQuality => "Officer Quality",
                _ => "Provisions"
            };

            var moraleText = tier switch
            {
                EnlistmentBehavior.RetinueProvisioningTier.BareMinimum => "-5 morale",
                EnlistmentBehavior.RetinueProvisioningTier.Standard => "neutral",
                EnlistmentBehavior.RetinueProvisioningTier.GoodFare => "+5 morale",
                EnlistmentBehavior.RetinueProvisioningTier.OfficerQuality => "+10 morale",
                _ => ""
            };

            args.Text = new TextObject($"Retinue: {tierName} ({cost}{{GOLD_ICON}}) [{soldierCount} soldiers, {moraleText}]");

            var canAfford = Hero.MainHero.Gold >= cost;
            if (!canAfford)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=qm_retinue_no_gold}Not enough gold to provision your retinue.");
            }
            else
            {
                args.Tooltip = baseCost != cost
                    ? new TextObject($"Provision your {soldierCount} soldiers for 7 days at {tierName} quality (base {baseCost}, discounted {cost}).")
                    : new TextObject($"Provision your {soldierCount} soldiers for 7 days at {tierName} quality.");
            }

            return true;
        }

        /// <summary>
        /// Handle retinue provisioning purchase.
        /// </summary>
        private void OnPurchaseRetinueProvisioning(EnlistmentBehavior.RetinueProvisioningTier tier)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_retinue_not_enlisted}You must be enlisted to provision your retinue.").ToString()));
                    return;
                }

                var retinueManager = RetinueManager.Instance;
                var soldierCount = retinueManager?.State?.TotalSoldiers ?? 0;

                if (soldierCount <= 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_retinue_no_soldiers}You have no soldiers to provision.").ToString()));
                    return;
                }

                var baseCost = EnlistmentBehavior.GetRetinueProvisioningCost(tier, soldierCount);
                var cost = enlistment.ApplyQuartermasterDiscount(baseCost);

                if (enlistment.PurchaseRetinueProvisioning(tier, soldierCount))
                {
                    var tierName = tier switch
                    {
                        EnlistmentBehavior.RetinueProvisioningTier.BareMinimum => "Bare Minimum",
                        EnlistmentBehavior.RetinueProvisioningTier.Standard => "Standard",
                        EnlistmentBehavior.RetinueProvisioningTier.GoodFare => "Good Fare",
                        EnlistmentBehavior.RetinueProvisioningTier.OfficerQuality => "Officer Quality",
                        _ => "provisions"
                    };

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Provisioned {soldierCount} soldiers with {tierName} rations for {cost} gold (7 days).",
                        Colors.Green));

                    // Refresh menu to show updated status
                    ActivateMenuPreserveTime("quartermaster_rations");
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_retinue_no_gold}Not enough gold to provision your retinue.").ToString(),
                        Colors.Red));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error purchasing retinue provisioning", ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=qm_retinue_error}Failed to provision retinue.").ToString(),
                    Colors.Red));
            }
        }

        /// <summary>
        /// Check if quartermaster services are available based on duties.
        /// Integrates with existing provisioner duty system.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "May be called from menu system or other modules")]
        public bool IsQuartermasterServiceAvailable()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;

                if (enlistment?.IsEnlisted != true)
                {
                    return false;
                }

                // Phase 1: Duties system deleted, standard access for all enlisted soldiers
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region Menu Option Implementations

        private int SafeGetTier(CharacterObject troop)
        {
            try { return troop.GetBattleTier(); } catch { return 1; }
        }

        /// <summary>
        /// Add supply management menu for quartermaster officers.
        /// </summary>
        private void AddSupplyManagementMenu(CampaignGameStarter starter)
        {
            // Supply management menu (wait menu with hidden progress for spacebar support)
            starter.AddWaitGameMenu(
                "quartermaster_supplies",
                "Supply Management\n{SUPPLY_TEXT}",
                OnSupplyManagementInit,
                QuartermasterWaitCondition,
                QuartermasterWaitConsequence,
                QuartermasterWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Food optimization option (Manage icon)
            starter.AddGameMenuOption("quartermaster_supplies", "optimize_food",
                new TextObject("{=qm_menu_optimize_food}Optimize food supplies").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return IsFoodOptimizationAvailable(args);
                },
                OnFoodOptimizationSelected,
                false, 1);

            // Inventory management option (Manage icon)
            starter.AddGameMenuOption("quartermaster_supplies", "manage_inventory",
                new TextObject("{=qm_menu_reorganize_inventory}Reorganize party inventory").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return IsInventoryManagementAvailable(args);
                },
                OnInventoryManagementSelected,
                false, 2);

            // Supply purchase option (Trade icon)
            starter.AddGameMenuOption("quartermaster_supplies", "purchase_supplies",
                new TextObject("{=qm_menu_purchase_supplies}Purchase additional supplies").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return IsSupplyPurchaseAvailable(args);
                },
                OnSupplyPurchaseSelected,
                false, 3);

            // Return to quartermaster conversation
            starter.AddGameMenuOption("quartermaster_supplies", "supplies_back",
                new TextObject("{=qm_menu_supplies_back}Return to quartermaster").ToString(),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => RestartQuartermasterConversationFromMenu());
        }

        /// <summary>
        /// Initialize supply management menu display.
        /// </summary>
        private void OnSupplyManagementInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var sb = new StringBuilder();
                var party = MobileParty.MainParty;

                _ = sb.AppendLine("â€” Supply Status â€”");
                _ = sb.AppendLine();

                // Current supply status with cleaner formatting
                _ = sb.AppendLine($"Inventory: {party.TotalWeightCarried:F1} / {party.InventoryCapacity:F1} capacity");
                _ = sb.AppendLine($"Food Supplies: {party.Food:F1} (consumption: {party.FoodChange:F2}/day)");
                _ = sb.AppendLine($"Morale: {party.Morale:F1} / 100");
                _ = sb.AppendLine();

                // Phase 1: Duties system deleted, no officer benefits display
                // (Officer benefits will be reintegrated via Orders system in later phase)

                MBTextManager.SetTextVariable("SUPPLY_TEXT", sb.ToString());
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error initializing supply management menu", ex);
                MBTextManager.SetTextVariable("SUPPLY_TEXT", "Supply information unavailable.");
            }
        }

        private bool IsFoodOptimizationAvailable(MenuCallbackArgs args)
        {
            _ = args; // Required by API contract
            var party = MobileParty.MainParty;
            return party is { Food: > 0, FoodChange: < 0 }; // Has food but consuming it
        }

        private void OnFoodOptimizationSelected(MenuCallbackArgs args)
        {
            // Quartermaster food efficiency bonus implementation
            var party = MobileParty.MainParty;
            var foodBonus = (int)(party.Food * 0.05f); // 5% food efficiency bonus

            if (foodBonus > 0)
            {
                _ = party.ItemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("grain"), foodBonus);

                var message = new TextObject("{=qm_optimized_food}Optimized food supplies: +{AMOUNT} grain from efficient rationing.");
                _ = message.SetTextVariable("AMOUNT", foodBonus.ToString());
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                ModLogger.Info("QUARTERMASTER", $"Food optimization applied: +{foodBonus} grain");
            }
        }

        private bool IsInventoryManagementAvailable(MenuCallbackArgs args)
        {
            _ = args; // Required by API contract
            var party = MobileParty.MainParty;
            // 1.3.4 API: TotalWeight moved from ItemRoster to MobileParty.TotalWeightCarried
            return party != null && party.TotalWeightCarried > party.InventoryCapacity * 0.8f; // Over 80% capacity
        }

        private void OnInventoryManagementSelected(MenuCallbackArgs args)
        {
            // Quartermaster inventory optimization
            var party = MobileParty.MainParty;
            var capacityBonus = party.InventoryCapacity * 0.05f; // 5% temporary capacity bonus

            var message = new TextObject("{=qm_reorganized_inventory}Reorganized inventory: +{AMOUNT} temporary carry capacity.");
            _ = message.SetTextVariable("AMOUNT", capacityBonus.ToString("F0"));
            InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

            ModLogger.Info("QUARTERMASTER", $"Inventory optimization applied: +{capacityBonus:F0} capacity");
        }

        private bool IsSupplyPurchaseAvailable(MenuCallbackArgs args)
        {
            _ = args; // Required by API contract
            return Hero.MainHero.Gold >= 50; // Can afford basic supplies
        }

        private void OnSupplyPurchaseSelected(MenuCallbackArgs args)
        {
            // Basic supply purchase system
            var cost = 50;
            if (Hero.MainHero.Gold >= cost)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost); // Default disableNotification=false is sufficient

                // Add basic supplies
                var party = MobileParty.MainParty;
                _ = party.ItemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("grain"), 5);
                _ = party.ItemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("tools"), 2);

                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=qm_purchased_supplies}Purchased basic supplies: 5 grain, 2 tools.").ToString()));

                ModLogger.Info("QUARTERMASTER", "Supply purchase completed");
            }
        }

        /// <summary>
        /// Check if the given equipment slot is a weapon slot (Weapon0 through Weapon3).
        /// Soldiers can carry multiple weapons in different slots.
        /// </summary>
        private static bool IsWeaponSlot(EquipmentIndex slot)
        {
            return slot is >= EquipmentIndex.Weapon0 and <= EquipmentIndex.Weapon3;
        }

        /// <summary>
        /// Check if an item is consumable (arrows, bolts, throwing weapons, etc).
        /// Consumable items can be stacked and soldiers may want multiple stacks.
        /// </summary>
        private static bool IsConsumableItem(ItemObject item)
        {
            if (item?.WeaponComponent?.PrimaryWeapon == null)
            {
                return false;
            }

            return item.WeaponComponent.PrimaryWeapon.IsConsumable;
        }

        #endregion

        #region Stock Availability System

        /// <summary>
        /// Re-rolls stock availability for all items based on current company supply level.
        /// Called at muster to simulate supply chain fluctuations. Items marked out-of-stock
        /// remain unavailable until the next muster cycle.
        /// </summary>
        public void RollStockAvailability()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var supplyLevel = enlistment?.CompanyNeeds?.Supplies ?? 100;

            _outOfStockItems.Clear();
            _lastStockRollSupplyLevel = supplyLevel;

            // Phase 7: Check if inventory needs refresh (12-day cycle)
            _inventoryState ??= new QMInventoryState();

            if (_inventoryState.NeedsRefresh())
            {
                RefreshInventoryAtMuster(supplyLevel);
            }

            // Calculate out-of-stock probability based on supply level
            // >= 60%: All items in stock (0% out of stock chance)
            // 40-59%: 20% chance each item is out of stock
            // 15-39%: 50% chance each item is out of stock
            // < 15%: Menu is blocked entirely (handled in EnlistedMenuBehavior)
            if (supplyLevel >= 60)
            {
                ModLogger.Debug("QUARTERMASTER", $"Stock roll: Supplies at {supplyLevel}% - all items in stock");
                return;
            }

            var outOfStockChance = supplyLevel >= 40 ? 0.20f : 0.50f;

            // Roll for each item using cross-formation equipment discovery
            var itemsRolled = 0;
            var itemsOutOfStock = 0;

            // Get all available equipment across ALL formations (infantry, archer, cavalry, horse archer)
            if (enlistment?.IsEnlisted == true)
            {
                var tier = enlistment.EnlistmentTier;
                var culture = enlistment.EnlistedLord?.Culture;

                if (culture != null)
                {
                    var allEquipment = GetAvailableEquipmentAllFormations(tier, culture);
                    foreach (var slotEntry in allEquipment.Values)
                    {
                        foreach (var item in slotEntry)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            itemsRolled++;
                            if (MBRandom.RandomFloat < outOfStockChance)
                            {
                                _ = _outOfStockItems.Add(item.StringId);
                                itemsOutOfStock++;
                            }
                        }
                    }
                }
            }

            // Also roll for Officers' Armory items (higher tier equipment)
            // Officer items have HIGHER out-of-stock chance (premium items are scarcer)
            var officerOutOfStockChance = Math.Min(outOfStockChance * 1.5f, 0.75f);
            var officerItemsRolled = 0;
            var officerItemsOutOfStock = 0;

            if (enlistment is { IsEnlisted: true, EnlistmentTier: >= 7 })
            {
                var culture = enlistment.EnlistedLord?.Culture;
                var playerTier = enlistment.EnlistmentTier;

                if (culture != null)
                {
                    // Get officer-tier equipment (tiers above player's normal access)
                    var officerEquipment = GetOfficerTierEquipmentForStockRoll(culture, playerTier);

                    foreach (var item in officerEquipment)
                    {
                        if (item == null || _outOfStockItems.Contains(item.StringId))
                        {
                            continue;
                        }

                        officerItemsRolled++;
                        if (MBRandom.RandomFloat < officerOutOfStockChance)
                        {
                            _ = _outOfStockItems.Add(item.StringId);
                            officerItemsOutOfStock++;
                        }
                    }
                }
            }

            // Implement stock floor: ensure at least 1 item per major slot is in stock
            var slotsToCheck = new[]
            {
                EquipmentIndex.Weapon0,
                EquipmentIndex.Head,
                EquipmentIndex.Body,
                EquipmentIndex.Leg,
                EquipmentIndex.Gloves,
                EquipmentIndex.Cape,
                EquipmentIndex.Horse
            };

            // Stock floor: ensure at least 1 item per major slot using cross-formation equipment
            if (enlistment?.IsEnlisted == true)
            {
                var tier = enlistment.EnlistmentTier;
                var culture = enlistment.EnlistedLord?.Culture;

                if (culture != null)
                {
                    var allEquipment = GetAvailableEquipmentAllFormations(tier, culture);

                    foreach (var slot in slotsToCheck)
                    {
                        // Check if all items in this slot are out of stock
                        var slotHasAvailableItem = false;
                        if (allEquipment.TryGetValue(slot, out var items))
                        {
                            foreach (var item in items)
                            {
                                if (item != null && !_outOfStockItems.Contains(item.StringId))
                                {
                                    slotHasAvailableItem = true;
                                    break;
                                }
                            }
                        }

                        // If all items in this slot are out of stock, mark the first one as available
                        if (!slotHasAvailableItem && allEquipment.TryGetValue(slot, out var slotItems) && slotItems.Count > 0)
                        {
                            var firstItem = slotItems.FirstOrDefault(i => i != null);
                            if (firstItem != null)
                            {
                                _ = _outOfStockItems.Remove(firstItem.StringId);
                                itemsOutOfStock = Math.Max(0, itemsOutOfStock - 1);
                                ModLogger.Debug("Quartermaster",
                                    $"Stock floor: Guaranteed {firstItem.Name} available in slot {slot}");
                            }
                        }
                    }
                }
            }

            var totalRolled = itemsRolled + officerItemsRolled;
            var totalOutOfStock = itemsOutOfStock + officerItemsOutOfStock;

            ModLogger.Info("Quartermaster",
                $"Stock roll complete: Supplies at {supplyLevel}% ({outOfStockChance:P0} base out-of-stock chance), " +
                $"Regular: {itemsOutOfStock}/{itemsRolled}, Officer: {officerItemsOutOfStock}/{officerItemsRolled}, " +
                $"Total: {totalOutOfStock}/{totalRolled} items out of stock (after stock floor)");
        }

        /// <summary>
        /// Gets all equipment from officer tiers (above player tier) for stock rolling.
        /// Used to include Officers' Armory items in the muster stock roll.
        /// </summary>
        private List<ItemObject> GetOfficerTierEquipmentForStockRoll(CultureObject culture, int playerTier)
        {
            var items = new HashSet<ItemObject>();

            try
            {
                if (culture == null)
                {
                    return items.ToList();
                }

                // Calculate max tier access (same as GetCultureEquipmentVariants)
                var enlistment = EnlistmentBehavior.Instance;
                var qmRep = enlistment?.GetQMReputation() ?? 0;

                var tierBonus = qmRep >= 90 ? 2 : (qmRep >= 75 ? 2 : 1);
                var maxTroopTier = playerTier + tierBonus; // No artificial cap

                var allTroops = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();

                // Get troops from tiers above player's normal access
                var eliteTroops = allTroops.Where(troop =>
                    troop.Culture == culture &&
                    troop.GetBattleTier() > playerTier &&
                    troop.GetBattleTier() <= maxTroopTier &&
                    !troop.IsHero &&
                    troop.BattleEquipments.Any()).ToList();

                foreach (var troop in eliteTroops)
                {
                    foreach (var equipment in troop.BattleEquipments)
                    {
                        for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
                        {
                            var item = equipment[slot].Item;
                            if (item != null)
                            {
                                _ = items.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error getting officer tier equipment for stock roll", ex);
            }

            return items.ToList();
        }

        /// <summary>
        /// Get all food items for T7+ officer provisions shop.
        /// Returns all available food items from the game database.
        /// </summary>
        private List<ItemObject> GetFoodItemsForProvisionsShop()
        {
            var foodItems = new List<ItemObject>();

            try
            {
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item?.IsFood == true)
                    {
                        foodItems.Add(item);
                    }
                }

                ModLogger.Debug("QUARTERMASTER", $"Found {foodItems.Count} food items for provisions");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error getting food items for provisions shop", ex);
            }

            return foodItems;
        }

        /// <summary>
        /// Refresh inventory at muster based on supply level.
        /// Generates new stock quantities for all available items.
        /// </summary>
        private void RefreshInventoryAtMuster(float supplyLevel)
        {
            try
            {
                // Collect all available items across ALL formations (infantry, archer, cavalry, horse archer).
                // Players should see all equipment for their tier, not just their current formation's equipment.
                var availableItems = new List<ItemObject>();
                var enlistment = EnlistmentBehavior.Instance;

                if (enlistment?.IsEnlisted == true)
                {
                    var tier = enlistment.EnlistmentTier;
                    var culture = enlistment.EnlistedLord?.Culture;

                    if (culture != null)
                    {
                        // Get equipment across ALL formations for this tier
                        var allEquipment = GetAvailableEquipmentAllFormations(tier, culture);
                        foreach (var slotEntry in allEquipment.Values)
                        {
                            foreach (var item in slotEntry)
                            {
                                if (item != null && !availableItems.Contains(item))
                                {
                                    availableItems.Add(item);
                                }
                            }
                        }

                        ModLogger.Debug("Quartermaster",
                            $"Cross-formation inventory: ALL formations T1-T{tier} {culture.Name} -> {availableItems.Count} items");
                    }
                }

                // Also include Officers' Armory items if player is T7+
                if (enlistment is { IsEnlisted: true, EnlistmentTier: >= 7 })
                {
                    var officerCulture = enlistment.EnlistedLord?.Culture;
                    var playerTier = enlistment.EnlistmentTier;

                    if (officerCulture != null)
                    {
                        var officerItems = GetOfficerTierEquipmentForStockRoll(officerCulture, playerTier);
                        foreach (var item in officerItems)
                        {
                            if (item != null && !availableItems.Contains(item))
                            {
                                availableItems.Add(item);
                            }
                        }
                    }

                    // Add food items for T7+ officer provisions shop
                    var foodItems = GetFoodItemsForProvisionsShop();
                    foreach (var foodItem in foodItems)
                    {
                        if (foodItem != null && !availableItems.Contains(foodItem))
                        {
                            availableItems.Add(foodItem);
                        }
                    }
                    ModLogger.Debug("QUARTERMASTER", $"Added {foodItems.Count} food items to provisions inventory");
                }

                // Refresh inventory state with new stock quantities
                _inventoryState.RefreshInventory(supplyLevel, availableItems);

                ModLogger.Info("Quartermaster",
                    $"Inventory refreshed at muster: {availableItems.Count} items in pool, " +
                    $"{_inventoryState.CurrentStock.Count} items stocked, supply: {supplyLevel:F1}%");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUARTERMASTER", "Error refreshing inventory at muster", ex);
            }
        }

        /// <summary>
        /// Rolls a random item quality based on quartermaster reputation.
        /// Quality distribution improves with higher reputation.
        /// </summary>
        private ItemQuality RollItemQualityByReputation()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var qmReputation = enlistment?.GetQMReputation() ?? 0;

            var roll = MBRandom.RandomFloat;

            // Quality distribution based on reputation (from Phase 2 design):
            // < 0:     50% Poor, 40% Inferior, 10% Common
            // 0-30:    30% Poor, 50% Inferior, 20% Common
            // 31-60:   15% Poor, 45% Inferior, 35% Common, 5% Fine
            // 61+:     5% Poor, 30% Inferior, 50% Common, 15% Fine

            if (qmReputation < 0)
            {
                if (roll < 0.50f)
                {
                    return ItemQuality.Poor;
                }
                if (roll < 0.90f)
                {
                    return ItemQuality.Inferior;
                }
                return ItemQuality.Common;
            }
            else if (qmReputation <= 30)
            {
                if (roll < 0.30f)
                {
                    return ItemQuality.Poor;
                }
                if (roll < 0.80f)
                {
                    return ItemQuality.Inferior;
                }
                return ItemQuality.Common;
            }
            else if (qmReputation <= 60)
            {
                if (roll < 0.15f)
                {
                    return ItemQuality.Poor;
                }
                if (roll < 0.60f)
                {
                    return ItemQuality.Inferior;
                }
                if (roll < 0.95f)
                {
                    return ItemQuality.Common;
                }
                return ItemQuality.Fine;
            }
            else
            {
                if (roll < 0.05f)
                {
                    return ItemQuality.Poor;
                }
                if (roll < 0.35f)
                {
                    return ItemQuality.Inferior;
                }
                if (roll < 0.85f)
                {
                    return ItemQuality.Common;
                }
                return ItemQuality.Fine;
            }
        }

        /// <summary>
        /// Applies a quality modifier to an item, creating an EquipmentElement with the modifier.
        /// Returns null if the item has no modifier group or the quality is unavailable.
        /// Also returns the quality tier that was successfully applied.
        /// </summary>
        private (ItemModifier modifier, ItemQuality quality) GetRandomModifierForQuality(ItemObject item, ItemQuality quality)
        {
            if (item == null)
            {
                return (null, ItemQuality.Common);
            }

            var modifierGroup = item.ItemComponent?.ItemModifierGroup;
            if (modifierGroup == null)
            {
                // Item has no modifier group - will display as Common quality with no prefix
                return (null, ItemQuality.Common);
            }

            var modifiers = modifierGroup.GetModifiersBasedOnQuality(quality);
            if (modifiers == null || modifiers.Count == 0)
            {
                // Quality tier doesn't exist for this item - return null
                return (null, ItemQuality.Common);
            }

            // Pick a random modifier from the available options for this quality
            return (modifiers.GetRandomElement(), quality);
        }

        /// <summary>
        /// Checks if a specific item is currently in stock.
        /// </summary>
        public bool IsItemInStock(ItemObject item)
        {
            if (item == null)
            {
                return false;
            }
            return !_outOfStockItems.Contains(item.StringId);
        }

        /// <summary>
        /// Checks if a specific item is currently in stock by string ID.
        /// </summary>
        public bool IsItemInStock(string itemStringId)
        {
            if (string.IsNullOrEmpty(itemStringId))
            {
                return false;
            }
            return !_outOfStockItems.Contains(itemStringId);
        }

        /// <summary>
        /// Gets the supply level at which stock was last rolled.
        /// Returns -1 if stock has never been rolled.
        /// </summary>
        public int LastStockRollSupplyLevel => _lastStockRollSupplyLevel;

        /// <summary>
        /// Gets the count of items currently out of stock.
        /// </summary>
        public int OutOfStockCount => _outOfStockItems.Count;

        #endregion

        #region Phase 3: Equipment Upgrade System

        /// <summary>
        /// Determine the quality tier of an item modifier by checking which quality tier it belongs to.
        /// Returns Common if the modifier is null or cannot be determined.
        /// </summary>
        public static ItemQuality GetModifierQuality(ItemObject item, ItemModifier modifier)
        {
            if (modifier == null || item == null)
            {
                return ItemQuality.Common;
            }

            var modGroup = item.ItemComponent?.ItemModifierGroup;
            if (modGroup == null)
            {
                return ItemQuality.Common;
            }

            // Check each quality tier to find which one contains this modifier
            var qualitiesToCheck = new[] {
                ItemQuality.Legendary,
                ItemQuality.Masterwork,
                ItemQuality.Fine,
                ItemQuality.Common,
                ItemQuality.Inferior,
                ItemQuality.Poor
            };

            foreach (var quality in qualitiesToCheck)
            {
                var modifiers = modGroup.GetModifiersBasedOnQuality(quality);
                if (modifiers != null && modifiers.Contains(modifier))
                {
                    return quality;
                }
            }

            return ItemQuality.Common;
        }

        /// <summary>
        /// Get available upgrade quality tiers based on quartermaster reputation.
        /// Higher reputation unlocks higher quality tiers.
        /// </summary>
        public List<ItemQuality> GetAvailableUpgradeTiers()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var qmReputation = enlistment?.GetQMReputation() ?? 0;

            var availableTiers = new List<ItemQuality> { ItemQuality.Fine };

            // 30+ reputation: Masterwork available
            if (qmReputation >= 30)
            {
                availableTiers.Add(ItemQuality.Masterwork);
            }

            // 61+ reputation: Legendary available
            if (qmReputation >= 61)
            {
                availableTiers.Add(ItemQuality.Legendary);
            }

            return availableTiers;
        }

        /// <summary>
        /// Calculate upgrade cost for improving an equipped item to target quality.
        /// Formula: (TargetQualityPrice - CurrentQualityPrice) × ServiceMarkup
        /// Service markup varies by reputation (2.0× to 1.25×).
        /// Uses long arithmetic to prevent integer overflow with expensive items.
        /// </summary>
        public int CalculateUpgradeCost(EquipmentElement currentElement, ItemQuality targetQuality)
        {
            if (currentElement.IsEmpty || currentElement.Item == null)
            {
                return 0;
            }

            var item = currentElement.Item;
            var currentModifier = currentElement.ItemModifier;
            var baseValue = item.Value;

            // Get current quality price multiplier
            var currentPriceMultiplier = currentModifier?.PriceMultiplier ?? 1.0f;

            // Get target quality price (use first modifier in quality tier for price calculation)
            var modGroup = item.ItemComponent?.ItemModifierGroup;
            if (modGroup == null)
            {
                // No modifier group - cannot calculate cost
                return 0;
            }

            var targetModifiers = modGroup.GetModifiersBasedOnQuality(targetQuality);
            if (targetModifiers == null || targetModifiers.Count == 0)
            {
                // Quality tier doesn't exist for this item
                return 0;
            }

            // Use the first modifier's price multiplier for cost calculation
            var targetModifier = targetModifiers.FirstOrDefault();
            var targetPriceMultiplier = targetModifier?.PriceMultiplier ?? 1.0f;

            // Calculate service markup based on QM reputation
            var enlistment = EnlistmentBehavior.Instance;
            var qmReputation = enlistment?.GetQMReputation() ?? 0;

            var serviceMarkup = qmReputation switch
            {
                < 30 => 2.0f,      // High markup for low reputation
                < 61 => 1.5f,  // Moderate markup for mid reputation
                _ => 1.25f         // Low markup for high reputation (61+)
            };

            // Use long arithmetic to prevent overflow with expensive items
            // Calculate current and target prices as long values
            var currentPrice = (long)(baseValue * currentPriceMultiplier);
            var targetPrice = (long)(baseValue * targetPriceMultiplier);
            var priceDifference = targetPrice - currentPrice;

            // Apply service markup
            var upgradeCostLong = (long)(priceDifference * serviceMarkup);

            // Clamp to valid int range and ensure non-negative
            if (upgradeCostLong > int.MaxValue)
            {
                ModLogger.Warn("EQUIPMENT",
                    $"Upgrade cost overflow detected for {item.StringId}: clamping {upgradeCostLong} to {int.MaxValue}");
                return int.MaxValue;
            }

            return Math.Max(0, (int)upgradeCostLong);
        }

        /// <summary>
        /// Perform upgrade on an equipped item to target quality.
        /// Returns true if successful, false if failed (with error message).
        /// </summary>
        public bool PerformUpgrade(EquipmentIndex slot, ItemQuality targetQuality, out string errorMessage)
        {
            errorMessage = null;
            var hero = Hero.MainHero;

            if (hero == null)
            {
                errorMessage = "Hero not found.";
                ModLogger.Expected("EQUIPMENT", "upgrade_no_hero", "Upgrade failed: Hero.MainHero is null");
                return false;
            }

            // Check if player is still enlisted
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                errorMessage = "You are no longer enlisted. Service has ended.";
                ModLogger.Info("EQUIPMENT", "Upgrade blocked: Player no longer enlisted");
                return false;
            }

            var currentElement = hero.BattleEquipment[slot];

            if (currentElement.IsEmpty || currentElement.Item == null)
            {
                errorMessage = "No item in that slot.";
                ModLogger.Expected("EQUIPMENT", "upgrade_empty_slot", $"Upgrade failed: Empty slot {slot}");
                return false;
            }

            var item = currentElement.Item;
            var modGroup = item.ItemComponent?.ItemModifierGroup;

            if (modGroup == null)
            {
                errorMessage = new TextObject("{=qm_upgrade_no_modifier_group}This item cannot be improved.").ToString();
                ModLogger.Expected("EQUIPMENT", "upgrade_no_modifier_group", $"Upgrade failed: {item.StringId} has no modifier group");
                return false;
            }

            // Check if target quality exists for this item
            var targetModifiers = modGroup.GetModifiersBasedOnQuality(targetQuality);
            if (targetModifiers == null || targetModifiers.Count == 0)
            {
                errorMessage = $"No {targetQuality} variants exist for this item.";
                ModLogger.Expected("EQUIPMENT", "upgrade_no_modifiers", $"Upgrade failed: {item.StringId} has no {targetQuality} modifiers");
                return false;
            }

            // Check reputation requirement (validates against current reputation at transaction time)
            var availableTiers = GetAvailableUpgradeTiers();
            if (!availableTiers.Contains(targetQuality))
            {
                // Determine specific reason for gating
                if (targetQuality == ItemQuality.Masterwork)
                {
                    errorMessage = new TextObject("{=qm_upgrade_masterwork_locked}Masterwork upgrades require better standing with the quartermaster.").ToString();
                }
                else if (targetQuality == ItemQuality.Legendary)
                {
                    errorMessage = new TextObject("{=qm_upgrade_legendary_locked}Legendary work is reserved for trusted soldiers.").ToString();
                }
                else
                {
                    // Generic message if reputation changed during screen interaction
                    errorMessage = "The quartermaster will no longer perform this upgrade. Your standing may have changed.";
                }

                var qmRep = enlistment?.GetQMReputation() ?? 0;
                ModLogger.Info("EQUIPMENT",
                    $"Upgrade blocked: {targetQuality} not available at current QM reputation ({qmRep})");
                return false;
            }

            // Calculate cost
            var cost = CalculateUpgradeCost(currentElement, targetQuality);

            if (cost <= 0)
            {
                errorMessage = "Cannot calculate upgrade cost for this item.";
                ModLogger.Expected("EQUIPMENT", "upgrade_invalid_cost", $"Upgrade failed: Invalid cost calculation for {item.StringId} to {targetQuality}");
                return false;
            }

            // Check if player can afford
            if (hero.Gold < cost)
            {
                errorMessage = new TextObject("{=qm_upgrade_cannot_afford}You can't afford this upgrade.").ToString();
                ModLogger.Info("EQUIPMENT", $"Upgrade blocked: Player gold {hero.Gold} < cost {cost}");
                return false;
            }

            // Execute upgrade
            try
            {
                // Deduct gold using GiveGoldAction to update UI correctly
                GiveGoldAction.ApplyBetweenCharacters(hero, null, cost);

                // Apply new modifier to equipped item
                var newModifier = targetModifiers.GetRandomElement();
                hero.BattleEquipment[slot] = new EquipmentElement(item, newModifier);

                ModLogger.Info("EQUIPMENT",
                    $"Upgrade successful: {item.Name} upgraded to {targetQuality} quality for {cost} denars (slot: {slot})");

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Error performing upgrade. Please try again.";
                ModLogger.Caught("EQUIPMENT", $"Upgrade failed with exception: {item.StringId} to {targetQuality}", ex);
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// Equipment variant option for quartermaster menu display.
    /// </summary>
    public class EquipmentVariantOption
    {
        public ItemObject Item { get; set; }
        public int Cost { get; set; }
        public bool IsCurrent { get; set; }
        public bool CanAfford { get; set; }
        public EquipmentIndex Slot { get; set; }

        /// <summary>
        /// True if this item allows duplicate purchases (weapons and consumables like arrows/bolts).
        /// Soldiers can carry multiple weapons in different slots or multiple stacks of consumables.
        /// </summary>
        public bool AllowsDuplicatePurchase { get; set; }

        /// <summary>
        /// True if the player has reached the item limit (2 per type) for this item.
        /// Prevents abuse of the free equipment system.
        /// </summary>
        public bool IsAtLimit { get; set; }

        /// <summary>
        /// True if this item is restricted to officers (T7+).
        /// </summary>
        public bool IsOfficerExclusive { get; set; }

        /// <summary>
        /// True if this item became available after the player's last promotion.
        /// Used for "NEW" indicators in the UI.
        /// </summary>
        public bool IsNewlyUnlocked { get; set; }

        /// <summary>
        /// True if the item is currently in stock. When false, purchase is blocked.
        /// Stock availability is rolled at each muster based on company supply levels.
        /// </summary>
        public bool IsInStock { get; set; } = true;

        /// <summary>
        /// Quantity available for purchase (Phase 7: Inventory & Pricing System).
        /// 0 means out of stock. Decrements with each purchase until next muster refresh.
        /// </summary>
        public int QuantityAvailable { get; set; } = 999; // Default to unlimited for backwards compatibility

        /// <summary>
        /// Quality modifier applied to this item (Poor/Inferior/Common/Fine/Masterwork/Legendary).
        /// Null indicates Common quality (no modifier).
        /// </summary>
        public ItemModifier Modifier { get; set; }

        /// <summary>
        /// Quality tier of this item (Poor/Inferior/Common/Fine/Masterwork/Legendary).
        /// Used for display purposes since ItemModifier doesn't expose Quality property.
        /// </summary>
        public ItemQuality Quality { get; set; } = ItemQuality.Common;

        /// <summary>
        /// Display name with quality prefix applied (e.g., "Rusty Shortsword", "Fine Bastard Sword").
        /// Null indicates no modifier prefix.
        /// </summary>
        public string ModifiedName { get; set; }
    }
}

