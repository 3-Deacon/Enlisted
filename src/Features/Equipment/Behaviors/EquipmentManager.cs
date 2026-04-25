using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Equipment.Behaviors
{
    /// <summary>
    /// Equipment management system handling equipment replacement, pricing, and retirement.
    /// 
    /// This system manages the transition from personal equipment to military-issued gear
    /// and handles equipment choices during retirement. Implements realistic military
    /// equipment replacement (not accumulation) for authentic service experience.
    /// </summary>
    public sealed class EquipmentManager : CampaignBehaviorBase
    {
        public static EquipmentManager Instance { get; private set; }

        // Equipment pricing configuration
        private Dictionary<FormationType, float> _formationPriceMultipliers;
        private Dictionary<string, float> _culturePriceMultipliers;

        public EquipmentManager()
        {
            Instance = this;
            InitializePricingSystem();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No save data needed - we track QM-issued equipment via item modifiers
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            ModLogger.Info("Equipment", "Equipment management system initialized");
        }

        /// <summary>
        /// Initialize formation and culture-based pricing system.
        /// </summary>
        private void InitializePricingSystem()
        {
            // Formation-based pricing multipliers (Infantry cheapest → Horse Archer most expensive)
            _formationPriceMultipliers = new Dictionary<FormationType, float>
            {
                { FormationType.Infantry, 1.0f },      // Base cost
                { FormationType.Archer, 1.3f },        // +30% (ranged weapons)
                { FormationType.Cavalry, 2.0f },       // +100% (horse equipment)
                { FormationType.HorseArcher, 2.5f }    // +150% (horse + ranged premium)
            };

            // Culture-based economic modifiers
            _culturePriceMultipliers = new Dictionary<string, float>
            {
                { "khuzait", 0.8f },    // Cheapest (steppe culture)
                { "battania", 0.8f },   // Cheap (tribal culture)
                { "aserai", 0.9f },     // Below average
                { "sturgia", 0.9f },    // Below average
                { "empire", 1.0f },     // Standard
                { "vlandia", 1.2f }     // Most expensive (elite culture)
            };
        }

        /// <summary>
        /// Calculate equipment cost for a specific troop choice.
        /// </summary>
        public int CalculateEquipmentCost(CharacterObject troop, FormationType formation)
        {
            try
            {
                var baseCost = 75 + (troop.Tier * 75); // 75 base + 75 per tier

                var formationMultiplier = _formationPriceMultipliers.TryGetValue(formation, out var fMult) ? fMult : 1.0f;
                var cultureMultiplier = _culturePriceMultipliers.TryGetValue(troop.Culture.StringId, out var cMult) ? cMult : 1.0f;

                var finalCost = (int)(baseCost * formationMultiplier * cultureMultiplier);
                return Math.Max(finalCost, 25); // Minimum 25 gold
            }
            catch
            {
                return 100; // Safe fallback cost
            }
        }

        /// <summary>
        /// Backup player's personal equipment before military service.
        /// Called when enlisting to preserve personal gear.
        /// PROTECTS QUEST ITEMS: Quest items in equipment slots are preserved and not stowed.
        /// </summary>
        // Equipment backup system removed - we track QM-issued gear via item modifiers instead.
        // Discharge works by: reclaim QM gear + return baggage stash items.

        /// <summary>
        /// Preserve quest items from equipped slots before equipment replacement.
        /// Returns a dictionary mapping equipment slots to quest items that must be restored.
        /// </summary>
        public Dictionary<EquipmentIndex, EquipmentElement> PreserveEquippedQuestItems()
        {
            var questItems = new Dictionary<EquipmentIndex, EquipmentElement>();

            try
            {
                var hero = Hero.MainHero;

                // Check battle equipment for quest items
                for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
                {
                    var element = hero.BattleEquipment[slot];
                    if (element.Item != null && element.IsQuestItem)
                    {
                        questItems[slot] = element;
                        ModLogger.Info("Equipment", $"Preserving quest item '{element.Item.Name}' from slot {slot}");
                    }
                }

                // Check civilian equipment for quest items
                for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
                {
                    var element = hero.CivilianEquipment[slot];
                    if (element.Item != null && element.IsQuestItem)
                    {
                        // Use a civilian slot offset to distinguish from battle equipment
                        var civilianSlot = (EquipmentIndex)((int)slot + 100); // Offset by 100 to distinguish civilian slots
                        questItems[civilianSlot] = element;
                        ModLogger.Info("Equipment", $"Preserving quest item '{element.Item.Name}' from civilian slot {slot}");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Equipment", "Error preserving equipped quest items", ex);
            }

            return questItems;
        }

        /// <summary>
        /// Restore quest items back to their original equipment slots after equipment assignment.
        /// </summary>
        public void RestoreEquippedQuestItems(Dictionary<EquipmentIndex, EquipmentElement> questItems)
        {
            if (questItems == null || questItems.Count == 0)
            {
                return;
            }

            try
            {
                var hero = Hero.MainHero;
                var battleEquipment = hero.BattleEquipment.Clone();
                var civilianEquipment = hero.CivilianEquipment.Clone();

                foreach (var kvp in questItems)
                {
                    var slot = kvp.Key;
                    var element = kvp.Value;

                    // Check if this is a civilian slot (offset by 100)
                    if ((int)slot >= 100)
                    {
                        var actualSlot = (EquipmentIndex)((int)slot - 100);
                        civilianEquipment[actualSlot] = element;
                        ModLogger.Info("Equipment", $"Restored quest item '{element.Item.Name}' to civilian slot {actualSlot}");
                    }
                    else
                    {
                        battleEquipment[slot] = element;
                        ModLogger.Info("Equipment", $"Restored quest item '{element.Item.Name}' to battle slot {slot}");
                    }
                }

                // Apply the updated equipment back to hero
                EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, battleEquipment);
                hero.CivilianEquipment.FillFrom(civilianEquipment, false);

                ModLogger.Info("Equipment", $"Restored {questItems.Count} quest item(s) after equipment assignment");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Equipment", "Error restoring equipped quest items", ex);
            }
        }

        /// <summary>
        /// Get culture-appropriate equipment for a specific tier and formation.
        /// Used for equipment pricing and availability calculations.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "May be used for future equipment selection features")]
        public List<ItemObject> GetCultureAppropriateEquipment(CultureObject culture, int tier, FormationType formation)
        {
            try
            {
                var availableGear = new List<ItemObject>();

                // Get troops for this culture and tier
                var allCharacters = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>();
                var cultureTemplates = allCharacters.Where(c =>
                    c.Culture == culture &&
                    c.GetBattleTier() <= tier &&  // CORRECTED: Use GetBattleTier() method
                    !c.IsHero &&  // Exclude heroes, get regular troops only
                    DetectTroopFormation(c) == formation);

                // Extract equipment from matching troops
                foreach (var character in cultureTemplates)
                {
                    foreach (var equipment in character.BattleEquipments)
                    {
                        for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
                        {
                            var item = equipment[slot].Item;
                            if (item != null && !availableGear.Contains(item))
                            {
                                availableGear.Add(item);
                            }
                        }
                    }
                }

                return availableGear;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Equipment", "Error getting culture-appropriate equipment", ex);
                return new List<ItemObject>();
            }
        }

        /// <summary>
        /// Detect formation type from troop properties.
        /// Detects the player's military formation based on equipment.
        /// </summary>
        private FormationType DetectTroopFormation(CharacterObject troop)
        {
            try
            {
                // Detect formation based on equipment characteristics
                if (troop.IsRanged && troop.IsMounted)
                {
                    return FormationType.HorseArcher;   // Bow + Horse
                }
                else if (troop.IsMounted)
                {
                    return FormationType.Cavalry;       // Sword + Horse  
                }
                else if (troop.IsRanged)
                {
                    return FormationType.Archer;        // Bow + No Horse
                }
                else
                {
                    return FormationType.Infantry;      // Sword + No Horse (default)
                }
            }
            catch
            {
                return FormationType.Infantry; // Safe fallback
            }
        }

        /// <summary>
        /// Process equipment request from weaponsmith menu option.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "May be called from menu system")]
        public void ProcessEquipmentRequest(FormationType requestedFormation)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return;
                }

                var currentLord = enlistment.CurrentLord;
                if (currentLord == null)
                {
                    ModLogger.Warn("Equipment", "Cannot process equipment request - no current lord");
                    return;
                }

                var culture = currentLord.Culture;
                var currentTier = enlistment.EnlistmentTier;

                // Get troops of requested formation at current tier
                var troopSelectionManager = TroopSelectionManager.Instance;
                var availableTroops = troopSelectionManager?.GetTroopsForCultureAndTier(culture.StringId, currentTier)
                    .Where(t => DetectTroopFormation(t) == requestedFormation).ToList();

                if (availableTroops is { Count: > 0 })
                {
                    // For now, select first available troop
                    // Can be enhanced with choice menu later
                    var selectedTroop = availableTroops.FirstOrDefault();
                    if (selectedTroop == null)
                    {
                        return;
                    }
                    var cost = CalculateEquipmentCost(selectedTroop, requestedFormation);

                    if (Hero.MainHero.Gold >= cost)
                    {
                        var goldBefore = Hero.MainHero.Gold;
                        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost); // Default disableNotification=false is sufficient
                        troopSelectionManager?.ApplySelectedTroopEquipment(Hero.MainHero, selectedTroop, autoIssueEquipment: true);

                        // Log equipment purchase
                        ModLogger.Info("Gold", $"Equipment purchased: {selectedTroop.Name} for {cost} denars (had {goldBefore}, now {Hero.MainHero.Gold})");
                        ModLogger.IncrementSummary("equipment_purchases", 1, cost);

                        var message = new TextObject("{=eq_upgraded}Equipment upgraded to {TROOP_NAME} for {COST} denars.");
                        _ = message.SetTextVariable("TROOP_NAME", selectedTroop.Name);
                        _ = message.SetTextVariable("COST", cost.ToString());
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    }
                    else
                    {
                        ModLogger.Warn("Gold", $"Insufficient funds for equipment: need {cost} denars, have {Hero.MainHero.Gold}");
                        var message = new TextObject("{=eq_insufficient_upgrade}Insufficient funds. Need {COST} denars for equipment upgrade.");
                        _ = message.SetTextVariable("COST", cost.ToString());
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Equipment", "Error processing equipment request", ex);
            }
        }
    }
}

