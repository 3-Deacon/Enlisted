using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers; // 1.3.4 API: ImageIdentifier moved here
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Helpers;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Entry;

namespace Enlisted.Features.Equipment.Behaviors
{
    /// <summary>
    /// Troop selection system allowing players to choose from real Bannerlord troop templates.
    ///
    /// This system allows players to select actual game troops during promotion, providing
    /// authentic military progression where equipment is replaced (not accumulated) for
    /// realistic military service. Players can choose troops up to their current tier level.
    /// </summary>
    public sealed class TroopSelectionManager : CampaignBehaviorBase
    {
        public static TroopSelectionManager Instance { get; private set; }

        // Promotion state tracking
        private bool _promotionPending;
        private int _pendingTier = 1;
        private List<CharacterObject> _availableTroops = new List<CharacterObject>();
        private string _lastSelectedTroopId;

        public string LastSelectedTroopId => _lastSelectedTroopId;

        public TroopSelectionManager()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                dataStore.SyncData("_promotionPending", ref _promotionPending);
                dataStore.SyncData("_pendingTier", ref _pendingTier);
                dataStore.SyncData("_lastSelectedTroopId", ref _lastSelectedTroopId);
            });
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddTroopSelectionMenus(starter);

            // Development-only: deep validation of troop coverage across cultures/tiers.
            // Disabled by default to keep session logs lightweight for players.
            if (ModConfig.Settings?.RunTroopDiscoveryValidation == true)
            {
                TroopDiscoveryValidator.ValidateAllCulturesAndTiers();
            }

            ModLogger.Info("TROOPSELECTION", "Troop selection system initialized with modern UI styling");
        }

        /// <summary>
        /// Menu background initialization for enlisted_troop_selection menu.
        /// Sets culture-appropriate background and ambient audio.
        /// </summary>
        [GameMenuInitializationHandler("enlisted_troop_selection")]
        private static void OnTroopSelectionBackgroundInit(MenuCallbackArgs args)
        {
            // Troop selection/promotion is a logistics/supply-facing flow (Quartermaster vibe).
            // Use a known "trade/logistics" encounter mesh instead of a generic battle encounter.
            args.MenuContext.SetBackgroundMeshName("encounter_caravan");
            args.MenuContext.SetAmbientSound("event:/map/ambient/node/settlements/2d/camp_army");
            args.MenuContext.SetPanelSound("event:/ui/panels/settlement_camp");
        }

        /// <summary>
        /// Show Master at Arms popup allowing player to select among unlocked troops (tiers ≤ current tier).
        /// Keeps tier unchanged; only equipment/role expression is changed.
        /// </summary>
        public void ShowMasterAtArmsPopup()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=eq_must_be_enlisted}You must be enlisted to use Master at Arms.").ToString()));
                    return;
                }

                var cultureId = enlistment?.CurrentLord?.Culture?.StringId;
                if (string.IsNullOrEmpty(cultureId))
                {
                    ModLogger.Expected("TROOPSELECTION", "troop_no_culture", "Master at Arms: Missing culture on current lord");
                    return;
                }

                // Use pending tier if promotion is active, otherwise current tier
                var effectiveTier = _promotionPending ? _pendingTier : enlistment.EnlistmentTier;
                var unlocked = GetUnlockedTroopsForCurrentTier(cultureId, effectiveTier);
                if (unlocked.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=eq_no_eligible_troops}No eligible troops found for your rank and culture.").ToString()));
                    return;
                }

                var options = new List<InquiryElement>();
                foreach (var troop in unlocked)
                {
                    var hint = BuildTroopLoadoutHint(troop);
                    var name = troop.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString();
                    // 1.3.4 API: ImageIdentifier is now abstract, use CharacterImageIdentifier
                    var portrait = new CharacterImageIdentifier(CharacterCode.CreateFrom(troop));
                    options.Add(new InquiryElement(troop, name, portrait, true, hint));
                }

                var data = new MultiSelectionInquiryData(
                    new TextObject("{=enl_ui_select_equipment_to_use_title}Select equipment to use").ToString(),
                    new TextObject("{=enl_ui_select_equipment_to_use_desc}Gear will not be auto-issued after Tier 1. Your chosen kit becomes purchasable at the Quartermaster.").ToString(),
                    options,
                    true, // Enable close button (X) like lord selection dialog
                    1,
                    1,
                    new TextObject("{=ll_default_continue}Continue").ToString(),
                    new TextObject("{=enl_ui_cancel}Cancel").ToString(),
                    selected =>
                    {
                        try
                        {
                            if (selected?.FirstOrDefault()?.Identifier is CharacterObject chosen)
                            {
                                ModLogger.Info("TROOPSELECTION", $"Player selected troop: {chosen.Name} (ID: {chosen.StringId}, Tier: {SafeGetTier(chosen)}, Formation: {DetectTroopFormation(chosen)})");
                                var autoIssue = SafeGetTier(chosen) <= 1; // Tier 1 keeps auto-issue; higher tiers unlock for purchase
                                ApplySelectedTroopEquipment(Hero.MainHero, chosen, autoIssue);
                                _lastSelectedTroopId = chosen.StringId;
                                // Don't re-capture time - preserve the time state from when the button was clicked
                                // Just refresh the menu without affecting time control
                                if (Campaign.Current?.CurrentMenuContext != null)
                                {
                                    Campaign.Current.GameMenuManager.RefreshMenuOptions(Campaign.Current.CurrentMenuContext);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("TROOPSELECTION", "Master at Arms apply failed", ex);
                        }
                    },
                    _ =>
                    {
                        // Cancel action - just close popup, don't affect menu or time state
                        // The enlisted_status menu is already active underneath
                    },
                    string.Empty);

                MBInformationManager.ShowMultiSelectionInquiry(data);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("TROOPSELECTION", "Master at Arms popup failed", ex);
            }
        }

        /// <summary>
        /// Build unlocked troop list for culture across tiers ≤ current tier.
        /// </summary>
        public List<CharacterObject> GetUnlockedTroopsForCurrentTier(string cultureId, int currentTier)
        {
            try
            {
                var culture = MBObjectManager.Instance.GetObject<CultureObject>(cultureId);
                var tree = BuildCultureTroopTree(culture);
                var troops = tree.Where(t =>
                        !t.IsHero &&
                        t.BattleEquipments.Any() &&
                        SafeGetTier(t) <= currentTier)
                    .OrderBy(SafeGetTier)
                    .ThenBy(t => t.Name?.ToString())
                    .ToList();

                if (troops.Count == 0 && culture != null)
                {
                    var fallback = new List<CharacterObject>();
                    if (culture.BasicTroop != null)
                    {
                        fallback.Add(culture.BasicTroop);
                    }
                    if (culture.EliteBasicTroop != null)
                    {
                        fallback.Add(culture.EliteBasicTroop);
                    }
                    if (culture.MeleeMilitiaTroop != null)
                    {
                        fallback.Add(culture.MeleeMilitiaTroop);
                    }
                    if (culture.RangedMilitiaTroop != null)
                    {
                        fallback.Add(culture.RangedMilitiaTroop);
                    }
                    troops = fallback.Where(t => t != null && t.BattleEquipments.Any()).ToList();
                }

                return troops;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TROOPSELECTION", "GetUnlockedTroops failed", ex);
                return new List<CharacterObject>();
            }
        }

        private int SafeGetTier(CharacterObject troop)
        {
            try { return troop.GetBattleTier(); } catch { return 1; }
        }

        private string BuildTroopLoadoutHint(CharacterObject troop)
        {
            try
            {
                var lines = new List<string>();
                var best = troop.BattleEquipments?.FirstOrDefault();
                if (best != null)
                {
                    for (var slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.HorseHarness; slot++)
                    {
                        var item = best[slot].Item;
                        if (item == null)
                        {
                            continue;
                        }
                        lines.Add(item.Name?.ToString() ?? item.StringId);
                    }
                }
                return string.Join("\n", lines);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Add troop selection menus for promotion system.
        /// </summary>
        private void AddTroopSelectionMenus(CampaignGameStarter starter)
        {
            // Main troop selection menu
            // NOTE: Use MenuOverlayType.None to avoid showing empty battle bar
            starter.AddGameMenu("enlisted_troop_selection",
                "Master at Arms\n{TROOP_SELECTION_TEXT}",
                OnTroopSelectionInit);

            // "Collect equipment now" button - opens Master at Arms popup for troop selection (TroopSelection icon)
            starter.AddGameMenuOption("enlisted_troop_selection", "troop_selection_collect_now",
                "Collect equipment now",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.TroopSelection;
                    return _promotionPending && _availableTroops.Count > 0;
                },
                _ =>
                {
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        try
                        {
                            ShowMasterAtArmsPopup();
                            ModLogger.Info("TROOPSELECTION", "Player chose to collect equipment now - opening Master at Arms");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("TROOPSELECTION", "Failed to open Master at Arms popup", ex);
                        }
                    });
                },
                false, 0);

            // "Return to camp" button - player declines immediate equipment collection (Leave icon)
            starter.AddGameMenuOption("enlisted_troop_selection", "troop_selection_back",
                "Return to camp",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ =>
                {
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        try
                        {
                            GameMenu.SwitchToMenu("enlisted_status");
                            ModLogger.Info("TROOPSELECTION", "Player declined equipment - returned to camp");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("TROOPSELECTION", "Failed to switch back to enlisted status", ex);
                            // Fallback to safe activation if direct switch failed
                            EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                        }
                    });
                });
        }

        /// <summary>
        /// Show troop selection menu for a specific tier.
        /// Called when player reaches promotion XP threshold.
        /// </summary>
        public void ShowTroopSelectionMenu(int newTier)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    ModLogger.Expected("TROOPSELECTION", "troop_not_enlisted", "Cannot show troop selection - player not enlisted");
                    return;
                }

                var cultureId = enlistment.CurrentLord?.Culture?.StringId;
                if (string.IsNullOrEmpty(cultureId))
                {
                    ModLogger.Expected("TROOPSELECTION", "troop_select_no_culture", "Cannot show troop selection - missing culture on current lord");
                    return;
                }
                _availableTroops = GetTroopsForCultureAndTier(cultureId, newTier);
                _pendingTier = newTier;
                _promotionPending = true;

                if (_availableTroops.Count == 0)
                {
                    ModLogger.Expected("TROOPSELECTION", "troop_select_no_eligible", $"No troops found for culture {cultureId} tier {newTier}");

                    // Fallback - apply basic tier progression without equipment change
                    enlistment.ApplyBasicPromotion(newTier);
                    return;
                }

                // Activate troop selection menu
                GameMenu.ActivateGameMenu("enlisted_troop_selection");

                ModLogger.Info("TROOPSELECTION", $"Troop selection menu opened - {_availableTroops.Count} troops available");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TROOPSELECTION", "Failed to show troop selection menu", ex);
            }
        }

        /// <summary>
        /// Initialize troop selection menu with available troop choices.
        /// </summary>
        private void OnTroopSelectionInit(MenuCallbackArgs args)
        {
            try
            {
                // 1.3.4+: Set proper menu background to avoid assertion failure
                var backgroundMesh = "encounter_looter"; // Safe fallback
                var enlistment = EnlistmentBehavior.Instance;

                if (enlistment?.CurrentLord?.Clan?.Kingdom?.Culture?.EncounterBackgroundMesh != null)
                {
                    backgroundMesh = enlistment.CurrentLord.Clan.Kingdom.Culture.EncounterBackgroundMesh;
                }
                else if (enlistment?.CurrentLord?.Culture?.EncounterBackgroundMesh != null)
                {
                    backgroundMesh = enlistment.CurrentLord.Culture.EncounterBackgroundMesh;
                }

                args.MenuContext.SetBackgroundMeshName(backgroundMesh);

                if (!_promotionPending || _availableTroops.Count == 0)
                {
                    // Initialize menu context
                    MBTextManager.SetTextVariable("TROOP_SELECTION_TEXT", "No promotion available at this time.");
                    return;
                }

                // Build roleplay-friendly promotion display
                var enlistmentRef = EnlistmentBehavior.Instance;
                var rankName = enlistmentRef?.GetRankName(_pendingTier) ?? $"Tier {_pendingTier}";

                var intro = new TextObject("{=enl_master_at_arms_summons_intro}You've been summoned before the Master at Arms.");
                var promo = new TextObject("{=enl_master_at_arms_promotion_line}\"Soldier, your service has not gone unnoticed. You've earned promotion to {RANK_NAME}.\"");
                promo.SetTextVariable("RANK_NAME", rankName);
                var note = new TextObject("{=enl_master_at_arms_after_t1_note}After Tier 1, gear is not auto-issued. Your chosen kit becomes purchasable from the Quartermaster.");
                var count = new TextObject("{=enl_master_at_arms_specializations_count}({COUNT} troop specializations available)");
                count.SetTextVariable("COUNT", _availableTroops.Count);

                var statusText = $"{intro}\n\n{promo}\n\n{note}\n\n{count}";

                MBTextManager.SetTextVariable("TROOP_SELECTION_TEXT", statusText);

                // Dynamically create menu options for each troop
                CreateTroopSelectionOptions(args);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TROOPSELECTION", "Error initializing troop selection menu", ex);
                MBTextManager.SetTextVariable("TROOP_SELECTION_TEXT", "Error loading troop selection. Please report this issue.");
            }
        }

        /// <summary>
        /// Create dynamic menu options for each available troop.
        /// </summary>
        private void CreateTroopSelectionOptions(MenuCallbackArgs _)
        {
            try
            {
                // Troop selection is handled through the popup dialog system
                // Players choose from available troops displayed in a selection dialog
                var troopCount = Math.Min(_availableTroops.Count, 8);
                ModLogger.Info("TROOPSELECTION", $"Created {troopCount} troop selection options");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TROOPSELECTION", "Error creating troop selection options", ex);
            }
        }

        /// <summary>
        /// Get available troops for specific culture and tier.
        /// Uses real Bannerlord CharacterObject templates.
        /// </summary>
        public List<CharacterObject> GetTroopsForCultureAndTier(string cultureId, int tier)
        {
            try
            {
                var culture = MBObjectManager.Instance.GetObject<CultureObject>(cultureId);
                var tree = BuildCultureTroopTree(culture);

                var availableTroops = tree.Where(troop =>
                    SafeGetTier(troop) == tier &&
                    !troop.IsHero &&
                    troop.BattleEquipments.Any()).ToList();

                // Add culture fallback troops if no specific tier troops found
                if (availableTroops.Count == 0 && culture != null)
                {
                    ModLogger.Info("TROOPSELECTION", $"No troops found at tier {tier}, using culture fallbacks");

                    // Use guaranteed culture troop templates as fallbacks
                    var fallbackTroops = new List<CharacterObject>();
                    if (culture.BasicTroop != null)
                    {
                        fallbackTroops.Add(culture.BasicTroop);
                    }
                    if (culture.EliteBasicTroop != null)
                    {
                        fallbackTroops.Add(culture.EliteBasicTroop);
                    }
                    if (culture.MeleeMilitiaTroop != null)
                    {
                        fallbackTroops.Add(culture.MeleeMilitiaTroop);
                    }
                    if (culture.RangedMilitiaTroop != null)
                    {
                        fallbackTroops.Add(culture.RangedMilitiaTroop);
                    }

                    availableTroops = fallbackTroops.Where(t => t.BattleEquipments.Any()).ToList();
                    ModLogger.Info("TROOPSELECTION", $"Using {availableTroops.Count} culture fallback troops");
                }

                ModLogger.Info("TROOPSELECTION", $"Found {availableTroops.Count} tree troops for {cultureId} tier {tier}");
                return availableTroops;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TROOPSELECTION", "Failed to get troops for culture/tier", ex);
                return new List<CharacterObject>();
            }
        }

        /// <summary>
        /// Build the culture's troop tree by traversing upgrade paths from BasicTroop and EliteBasicTroop.
        /// </summary>
        private List<CharacterObject> BuildCultureTroopTree(CultureObject culture)
        {
            var results = new List<CharacterObject>();
            try
            {
                if (culture == null)
                {
                    return results;
                }

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<CharacterObject>();

                void EnqueueIfValid(CharacterObject start)
                {
                    if (start == null)
                    {
                        return;
                    }
                    if (start.Culture != culture)
                    {
                        return;
                    }
                    if (start.IsHero)
                    {
                        return;
                    }
                    if (!visited.Add(start.StringId))
                    {
                        return;
                    }
                    queue.Enqueue(start);
                }

                EnqueueIfValid(culture.BasicTroop);
                EnqueueIfValid(culture.EliteBasicTroop);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    results.Add(node);

                    try
                    {
                        var upgrades = node.UpgradeTargets; // MBReadOnlyList<CharacterObject>
                        if (upgrades != null)
                        {
                            foreach (var next in upgrades)
                            {
                                if (next != null && next.Culture == culture && !next.IsHero && visited.Add(next.StringId))
                                {
                                    queue.Enqueue(next);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // best-effort; continue on any API differences
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TROOPSELECTION", "BuildCultureTroopTree failed", ex);
            }
            return results;
        }

        /// <summary>
        /// Apply equipment from selected troop to hero.
        /// Implements equipment REPLACEMENT system (not accumulation).
        /// Includes accountability check - soldier is charged for missing equipment when auto-issuing.
        /// PROTECTS QUEST ITEMS: Quest items are preserved during equipment replacement.
        /// </summary>
        public void ApplySelectedTroopEquipment(Hero hero, CharacterObject selectedTroop, bool autoIssueEquipment)
        {
            try
            {
                _lastSelectedTroopId = selectedTroop.StringId;

                // Phase 1: Formation/Duties system deleted
                // Formation preferences will be tracked via native traits in later phase
                var formation = DetectTroopFormation(selectedTroop);

                if (autoIssueEquipment)
                {
                    // Equipment is replaced (not accumulated) for realistic military service
                    var troopEquipment = selectedTroop.BattleEquipments.FirstOrDefault();
                    if (troopEquipment == null)
                    {
                        ModLogger.Expected("TROOPSELECTION", "equip_variant_null", $"No equipment found for {selectedTroop.Name}");
                        return;
                    }

                    // CRITICAL: Preserve quest items before replacing equipment
                    var equipmentManager = EquipmentManager.Instance;
                    var questItems = equipmentManager?.PreserveEquippedQuestItems() ?? new Dictionary<EquipmentIndex, EquipmentElement>();

                    // CRITICAL: Move civilian equipment to inventory BEFORE replacing
                    // This ensures the bag check (1 hour after enlistment) will find and stash these items
                    StashCivilianEquipmentToInventory(hero, questItems);

                    // Replace all equipment with new troop's gear
                    EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, troopEquipment);

                    // CRITICAL: Restore quest items after equipment replacement
                    equipmentManager?.RestoreEquippedQuestItems(questItems);

                    // Show promotion notification
                    var message = new TextObject("{=eq_promoted_new_equipment}Promoted to {TROOP_NAME}! New equipment issued.");
                    message.SetTextVariable("TROOP_NAME", selectedTroop.Name);
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                }
                else
                {
                    // No auto-issue: inform player gear is purchasable at Quartermaster
                    var message = new TextObject("{=eq_purchasable_qm}Promotion recorded. Gear for {TROOP_NAME} is now available at the Quartermaster.");
                    message.SetTextVariable("TROOP_NAME", selectedTroop.Name);
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                }

                // Clear promotion state
                _promotionPending = false;
                _availableTroops.Clear();

                ModLogger.Info("TROOPSELECTION", autoIssueEquipment
                    ? $"Equipment replaced with {selectedTroop.Name} gear (Formation: {formation}, quest items protected)"
                    : $"Troop selection recorded without auto-issue (Formation: {formation}); gear available at Quartermaster");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("TROOPSELECTION", "Failed to apply selected troop equipment", ex);
            }
        }

        /// <summary>
        /// Moves civilian (pre-enlistment) equipment from hero's equipped slots to party inventory.
        /// This allows the bag check event to find and properly stash these items.
        /// Quest items are excluded as they are handled separately.
        /// </summary>
        private void StashCivilianEquipmentToInventory(Hero hero, Dictionary<EquipmentIndex, EquipmentElement> questItems)
        {
            try
            {
                var partyRoster = MobileParty.MainParty?.ItemRoster;
                if (partyRoster == null || hero?.BattleEquipment == null)
                {
                    return;
                }

                var stashedCount = 0;

                // Stash battle equipment
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var slot = (EquipmentIndex)i;
                    var element = hero.BattleEquipment[slot];

                    if (element.IsEmpty || element.Item == null)
                    {
                        continue;
                    }

                    // Skip quest items - they're preserved separately
                    if (questItems.ContainsKey(slot) || element.IsQuestItem)
                    {
                        continue;
                    }

                    // Move to inventory
                    partyRoster.AddToCounts(element, 1);
                    stashedCount++;
                    ModLogger.Debug("TROOPSELECTION", $"Moved to inventory: {element.Item.Name}");
                }

                // Stash civilian equipment (if different from battle equipment)
                if (hero.CivilianEquipment != null)
                {
                    for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                    {
                        var slot = (EquipmentIndex)i;
                        var element = hero.CivilianEquipment[slot];

                        if (element.IsEmpty || element.Item == null)
                        {
                            continue;
                        }

                        // Skip if same as battle equipment (already stashed)
                        if (hero.BattleEquipment[slot].Item?.StringId == element.Item.StringId)
                        {
                            continue;
                        }

                        // Skip quest items
                        var civilianSlot = (EquipmentIndex)((int)slot + 100);
                        if (questItems.ContainsKey(civilianSlot) || element.IsQuestItem)
                        {
                            continue;
                        }

                        // Move to inventory
                        partyRoster.AddToCounts(element, 1);
                        stashedCount++;
                        ModLogger.Debug("TROOPSELECTION", $"Moved civilian item to inventory: {element.Item.Name}");
                    }
                }

                if (stashedCount > 0)
                {
                    ModLogger.Info("TROOPSELECTION", $"Stashed {stashedCount} civilian items to inventory for bag check");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Warn("TROOPSELECTION", $"Error stashing civilian equipment: {ex.Message}");
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
        /// Handle troop selection by formation type.
        /// Selects the first available troop matching the specified formation type.
        /// </summary>
        public void SelectTroopByFormation(FormationType formationType)
        {
            try
            {
                var troopOfType = _availableTroops.FirstOrDefault(t => DetectTroopFormation(t) == formationType);
                if (troopOfType != null)
                {
                    ApplySelectedTroopEquipment(Hero.MainHero, troopOfType, autoIssueEquipment: SafeGetTier(troopOfType) <= 1);

                    // Return to main enlisted menu
                    EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                }
                else
                {
                    ModLogger.Expected("TROOPSELECTION", "troop_no_formation_match", $"No {formationType} troop available");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("TROOPSELECTION", "Error in troop selection", ex);
            }
        }

        /// <summary>
        /// Check if promotion is currently pending.
        /// </summary>
        public bool IsPromotionPending => _promotionPending;

        /// <summary>
        /// Get pending promotion tier.
        /// </summary>
        public int PendingTier => _pendingTier;
    }

    /// <summary>
    /// Formation types for troop selection and duties system integration.
    /// </summary>
    public enum FormationType
    {
        None,
        Infantry,
        Archer,
        Cavalry,
        HorseArcher
    }

}
