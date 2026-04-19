using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Retinue.Data;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Retinue.Core
{
    /// <summary>
    /// Manages the player's personal retinue of soldiers. Handles soldier addition/removal,
    /// tier capacity checks, faction availability, and party size safeguards.
    /// </summary>
    public sealed class RetinueManager
    {
        private const string LogCategory = "Retinue";

        // ========================================================================
        // RETINUE SYSTEM V2.0 - Commander's Retinue at T7-T9
        // Companions managed from T1 (via CompanionAssignmentManager)
        // Commander retinue: 20/30/40 soldiers at T7/T8/T9
        // ========================================================================

        // New tier unlock thresholds (Commander's Retinue)
        public const int CommanderTier1 = 7;  // First retinue tier
        public const int CommanderTier2 = 8;  // Expanded retinue
        public const int CommanderTier3 = 9;  // Elite retinue

        // New capacity by tier
        public const int CommanderCapacity1 = 20;  // T7: 20 soldiers
        public const int CommanderCapacity2 = 30;  // T8: 30 soldiers
        public const int CommanderCapacity3 = 40;  // T9: 40 soldiers

        // Factions that don't have horse archers
        private static readonly HashSet<string> NoHorseArcherFactions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vlandia", "battania", "sturgia"
        };

        // Formation class mapping for soldier types
        private static readonly Dictionary<string, FormationClass> TypeToFormation =
            new Dictionary<string, FormationClass>(StringComparer.OrdinalIgnoreCase)
            {
                { "infantry", FormationClass.Infantry },
                { "archers", FormationClass.Ranged },
                { "cavalry", FormationClass.Cavalry },
                { "horse_archers", FormationClass.HorseArcher }
            };

        public static RetinueManager Instance { get; private set; }

        private readonly RetinueState _state;

        public RetinueManager(RetinueState state)
        {
            _state = state ?? new RetinueState();
            Instance = this;
            ModLogger.Debug(LogCategory, "RetinueManager initialized");
        }

        /// <summary>
        /// Gets the current retinue state for serialization or UI access.
        /// </summary>
        public RetinueState State => _state;

        #region Tier Capacity

        /// <summary>
        /// Gets the maximum soldier capacity for a given tier.
        /// T1-T6: 0 (companions only)
        /// T7: 20 soldiers
        /// T8: 30 soldiers
        /// T9: 40 soldiers
        /// </summary>
        /// <param name="tier">Player's enlistment tier (1-9)</param>
        /// <returns>Max soldiers allowed: 0 for T1-T6, 20/30/40 for T7/T8/T9</returns>
        public static int GetTierCapacity(int tier)
        {
            return tier switch
            {
                >= CommanderTier3 => CommanderCapacity3,  // T9+: 40 soldiers
                CommanderTier2 => CommanderCapacity2,      // T8: 30 soldiers
                CommanderTier1 => CommanderCapacity1,      // T7: 20 soldiers
                _ => 0  // T1-T6: companions only, no soldiers
            };
        }

        #endregion

        #region Faction Availability

        /// <summary>
        /// Checks if a soldier type is available for the given culture.
        /// Horse archers are not available for Vlandia, Battania, and Sturgia.
        /// </summary>
        /// <param name="typeId">Soldier type ID (infantry, archers, cavalry, horse_archers)</param>
        /// <param name="culture">The faction's culture</param>
        /// <returns>True if the type is available for this culture</returns>
        public static bool IsSoldierTypeAvailable(string typeId, CultureObject culture)
        {
            if (string.IsNullOrEmpty(typeId) || culture == null)
            {
                return false;
            }

            // Horse archers are faction-restricted
            if (typeId.Equals("horse_archers", StringComparison.OrdinalIgnoreCase))
            {
                var cultureId = culture.StringId?.ToLowerInvariant() ?? "";
                return !NoHorseArcherFactions.Contains(cultureId);
            }

            // All other types available for all factions
            return true;
        }

        /// <summary>
        /// Gets the formation class for a soldier type ID.
        /// </summary>
        public static FormationClass GetFormationClass(string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                return FormationClass.Infantry;
            }

            return TypeToFormation.TryGetValue(typeId, out var formation)
                ? formation
                : FormationClass.Infantry;
        }

        #endregion

        #region Troop Retrieval

        /// <summary>
        /// Gets available troops of a specific type and tier range from a culture's troop tree.
        /// Uses CharacterHelper.GetTroopTree to traverse the faction's upgrade tree.
        /// </summary>
        /// <param name="typeId">Soldier type (infantry, archers, cavalry, horse_archers)</param>
        /// <param name="culture">The faction's culture</param>
        /// <param name="playerTier">Player's tier for determining troop quality range</param>
        /// <returns>List of CharacterObjects matching the criteria</returns>
        public static List<CharacterObject> GetAvailableTroops(string typeId, CultureObject culture, int playerTier)
        {
            if (culture?.BasicTroop == null)
            {
                ModLogger.Warn(LogCategory, "GetAvailableTroops: Invalid culture or no basic troop");
                return new List<CharacterObject>();
            }

            var targetFormation = GetFormationClass(typeId);

            // Determine tier range based on player tier
            // T7: Raw recruits (tier 1-2)
            // T8: Better quality (tier 2-3)
            // T9: Veteran troops (tier 3-4)
            int minTier, maxTier;
            if (playerTier >= CommanderTier3)
            {
                minTier = 3;
                maxTier = 4;
            }
            else if (playerTier >= CommanderTier2)
            {
                minTier = 2;
                maxTier = 3;
            }
            else
            {
                // T7 and below: raw recruits
                minTier = 1;
                maxTier = 2;
            }

            // Get all troops from the culture's troop tree within tier range
            // Materialize to list immediately to avoid multiple enumeration
            var allTroops = CharacterHelper.GetTroopTree(culture.BasicTroop, minTier, maxTier).ToList();

            // Filter by formation class
            var filtered = allTroops
                .Where(t => t != null && t.DefaultFormationClass == targetFormation)
                .ToList();

            ModLogger.Debug(LogCategory,
                $"GetAvailableTroops: type={typeId}, culture={culture.StringId}, " +
                $"tierRange={minTier}-{maxTier}, found={filtered.Count} troops");

            // If no troops found in the exact formation, try finding any troops in tier range
            // This handles edge cases where a faction might not have troops in a specific formation
            if (filtered.Count == 0)
            {
                ModLogger.Warn(LogCategory,
                    $"No {typeId} troops found for {culture.StringId} in tier {minTier}-{maxTier}, " +
                    "falling back to any available troop");

                filtered = allTroops.Take(1).ToList();
            }

            return filtered;
        }

        /// <summary>
        /// Gets a random troop of the specified type appropriate for the player's tier.
        /// </summary>
        public static CharacterObject GetRandomTroop(string typeId, CultureObject culture, int playerTier)
        {
            var troops = GetAvailableTroops(typeId, culture, playerTier);
            if (troops.Count == 0)
            {
                return null;
            }

            return troops.GetRandomElement();
        }

        #endregion

        #region Add/Remove Soldiers

        /// <summary>
        /// Attempts to add soldiers to the player's party with tier and party size safeguards.
        /// </summary>
        /// <param name="count">Requested number of soldiers</param>
        /// <param name="typeId">Soldier type ID</param>
        /// <param name="actuallyAdded">Out: actual number added</param>
        /// <param name="message">Out: result message for UI</param>
        /// <returns>True if any soldiers were added</returns>
        public bool TryAddSoldiers(int count, string typeId, out int actuallyAdded, out string message)
        {
            actuallyAdded = 0;
            message = string.Empty;

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                message = new TextObject("{=enl_retinue_msg_must_be_enlisted}You must be enlisted to command soldiers.").ToString();
                ModLogger.Warn(LogCategory, "AddSoldiers blocked: not enlisted");
                return false;
            }

            var party = MobileParty.MainParty;
            if (party == null)
            {
                message = new TextObject("{=enl_retinue_msg_no_party}No party available.").ToString();
                ModLogger.Warn(LogCategory, "AddSoldiers blocked: no party");
                return false;
            }

            var currentTier = enlistment.EnlistmentTier;
            var culture = enlistment.CurrentLord?.Culture;

            if (culture == null)
            {
                message = new TextObject("{=enl_retinue_msg_no_culture}Cannot determine faction culture.").ToString();
                ModLogger.Warn(LogCategory, "AddSoldiers blocked: no culture");
                return false;
            }

            // Calculate available space
            var tierCapacity = GetTierCapacity(currentTier);
            var currentSoldiers = _state.TotalSoldiers;
            var tierAvailable = tierCapacity - currentSoldiers;

            var partyLimit = PartyBase.MainParty.PartySizeLimit;
            var currentMembers = PartyBase.MainParty.NumberOfAllMembers;
            var partyAvailable = partyLimit - currentMembers;

            // Take the more restrictive limit
            var maxCanAdd = Math.Min(tierAvailable, partyAvailable);
            actuallyAdded = Math.Min(count, maxCanAdd);

            if (actuallyAdded <= 0)
            {
                if (partyAvailable <= 0)
                {
                    message = new TextObject("{=enl_retinue_msg_party_full}Party is full. Dismiss troops or increase party size.").ToString();
                }
                else if (tierAvailable <= 0)
                {
                    message = new TextObject("{=enl_retinue_msg_full_capacity}Retinue is at full capacity for your rank.").ToString();
                }
                else
                {
                    message = new TextObject("{=enl_retinue_msg_cannot_add}Cannot add soldiers.").ToString();
                }

                ModLogger.Warn(LogCategory, $"AddSoldiers blocked: {message}");
                return false;
            }

            // Set the type if not already set
            if (string.IsNullOrEmpty(_state.SelectedTypeId))
            {
                _state.SelectedTypeId = typeId;
            }

            // Add soldiers to roster
            var added = 0;
            for (var i = 0; i < actuallyAdded; i++)
            {
                var troop = GetRandomTroop(typeId, culture, currentTier);
                if (troop == null)
                {
                    ModLogger.Warn(LogCategory, $"Failed to get troop for type={typeId}");
                    continue;
                }

                party.MemberRoster.AddToCounts(troop, 1);
                _state.UpdateTroopCount(troop.StringId, 1);
                added++;

                ModLogger.Debug(LogCategory, $"Added {troop.Name} to retinue");
            }

            actuallyAdded = added;

            if (actuallyAdded < count)
            {
                var t = new TextObject("{=enl_retinue_msg_party_limit_reached}Party limit reached. Only {COUNT} soldiers assigned.");
                t.SetTextVariable("COUNT", actuallyAdded);
                message = t.ToString();
                ModLogger.Info(LogCategory, message);
            }
            else
            {
                message = $"{actuallyAdded} soldiers assigned to your retinue.";
            }

            ModLogger.Info(LogCategory, $"AddSoldiers: added {actuallyAdded}/{count}, total={_state.TotalSoldiers}");
            return actuallyAdded > 0;
        }

        /// <summary>
        /// Removes all retinue troops from the player's roster and clears state.
        /// Called on capture, enlistment end, army defeat, or type change.
        /// </summary>
        /// <param name="reason">Logging reason for debugging</param>
        public void ClearRetinueTroops(string reason)
        {
            var main = MobileParty.MainParty;
            if (main == null)
            {
                _state.Clear();
                ModLogger.Debug(LogCategory, $"ClearRetinueTroops({reason}): no party, state cleared");
                return;
            }

            var totalCleared = 0;

            foreach (var kvp in _state.TroopCounts.ToList())
            {
                var troopId = kvp.Key;
                var count = kvp.Value;

                if (count <= 0)
                {
                    continue;
                }

                var character = CharacterObject.Find(troopId);
                if (character != null)
                {
                    // Remove from roster (clamp to actual roster count to prevent negative)
                    var actualCount = main.MemberRoster.GetTroopCount(character);
                    var toRemove = Math.Min(count, actualCount);

                    if (toRemove > 0)
                    {
                        main.MemberRoster.AddToCounts(character, -toRemove);
                        totalCleared += toRemove;
                        ModLogger.Debug(LogCategory, $"Cleared {toRemove}x {character.Name}");
                    }
                }
            }

            _state.Clear();
            ModLogger.Info(LogCategory, $"Cleared {totalCleared} retinue troops (reason: {reason})");
        }

        #endregion

        #region Companion Tracking

        /// <summary>
        /// Gets the count of companions in the player's party.
        /// </summary>
        public static int GetCompanionCount()
        {
            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null)
            {
                return 0;
            }

            return roster.GetTroopRoster()
                .Count(e => e.Character.IsHero &&
                            e.Character.HeroObject?.IsPlayerCompanion == true);
        }

        #endregion

        #region Reinforcement Request System

        // Relation thresholds for reinforcement requests
        public const int MinRelationForRequest = 20;        // Minimum relation to request at all
        public const int HighRelationThreshold = 50;        // High relation for discount

        // Cooldown periods (in days)
        public const int HighRelationCooldownDays = 7;      // Shorter cooldown for good relation
        public const int StandardCooldownDays = 14;         // Standard cooldown

        // Cost multipliers
        public const float HighRelationCostMultiplier = 0.75f;  // 25% discount
        public const float StandardCostMultiplier = 1.0f;       // Full price

        /// <summary>
        /// Gets the player's relation with their current lord.
        /// Returns 0 if not enlisted or lord unavailable.
        /// </summary>
        public static int GetLordRelation()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = enlistment?.EnlistedLord;
            if (lord == null || Hero.MainHero == null)
            {
                return 0;
            }

            return Hero.MainHero.GetRelation(lord);
        }

        /// <summary>
        /// Gets the appropriate cost multiplier based on lord relation.
        /// High relation (50+) gets 25% discount.
        /// </summary>
        public static float GetRelationCostMultiplier()
        {
            var relation = GetLordRelation();
            return relation >= HighRelationThreshold ? HighRelationCostMultiplier : StandardCostMultiplier;
        }

        /// <summary>
        /// Gets the appropriate cooldown period based on lord relation.
        /// High relation (50+) gets shorter cooldown.
        /// </summary>
        public static int GetRelationCooldownDays()
        {
            var relation = GetLordRelation();
            return relation >= HighRelationThreshold ? HighRelationCooldownDays : StandardCooldownDays;
        }

        /// <summary>
        /// Checks if player can request reinforcements from their lord.
        /// Validates: relation, cooldown, capacity, and gold.
        /// </summary>
        /// <param name="reason">Out: reason if cannot request</param>
        /// <returns>True if request is allowed</returns>
        public bool CanRequestReinforcements(out string reason)
        {
            reason = null;

            // Must be enlisted at Commander tier (T7+)
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                reason = new TextObject("{=enl_retinue_reason_must_be_enlisted_request}You must be enlisted to request reinforcements.").ToString();
                return false;
            }

            if (enlistment.EnlistmentTier < CommanderTier1)
            {
                var t = new TextObject("{=enl_retinue_reason_requires_commander_request}Requires Commander rank (Tier {REQ}) or higher.");
                t.SetTextVariable("REQ", CommanderTier1);
                reason = t.ToString();
                return false;
            }

            // Must have a retinue type selected
            if (!_state.HasTypeSelected)
            {
                reason = new TextObject("{=enl_retinue_reason_select_type_request}You must select a soldier type first.").ToString();
                return false;
            }

            // Check lord relation minimum
            var relation = GetLordRelation();
            if (relation < MinRelationForRequest)
            {
                var t = new TextObject("{=enl_retinue_reason_relation_low}Your lord will not spare soldiers for you. Improve your standing first (requires {REQ}+ relation, have {HAVE}).");
                t.SetTextVariable("REQ", MinRelationForRequest);
                t.SetTextVariable("HAVE", relation);
                reason = t.ToString();
                return false;
            }

            // Check cooldown
            if (!_state.IsReinforcementRequestAvailable())
            {
                var daysRemaining = _state.GetReinforcementRequestCooldownDays();
                var t = new TextObject("{=enl_retinue_reason_request_cooldown}Reinforcement request on cooldown: {DAYS} days remaining.");
                t.SetTextVariable("DAYS", daysRemaining);
                reason = t.ToString();
                return false;
            }

            // Must have room for more soldiers
            var missing = GetMissingSoldierCount();
            if (missing <= 0)
            {
                reason = new TextObject("{=enl_retinue_reason_full_strength}Your retinue is at full strength.").ToString();
                return false;
            }

            // Must have enough gold
            var cost = CalculateReinforcementRequestCost();
            var playerGold = Hero.MainHero?.Gold ?? 0;
            if (playerGold < cost)
            {
                var t = new TextObject("{=enl_retinue_reason_not_enough_gold_request}Not enough gold. Need {NEED} denars, have {HAVE}.");
                t.SetTextVariable("NEED", cost);
                t.SetTextVariable("HAVE", playerGold);
                reason = t.ToString();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates the total gold cost for a reinforcement request.
        /// Uses base recruitment cost with relation-based multiplier.
        /// </summary>
        /// <returns>Total gold cost for reinforcement request</returns>
        public int CalculateReinforcementRequestCost()
        {
            var baseCost = CalculateRequisitionCost();
            var multiplier = GetRelationCostMultiplier();
            return (int)(baseCost * multiplier);
        }

        /// <summary>
        /// Attempts to request reinforcements from the lord.
        /// Applies relation-based cooldown on success.
        /// </summary>
        /// <param name="message">Out: result message for UI</param>
        /// <returns>True if request succeeded</returns>
        public bool TryRequestReinforcements(out string message)
        {
            const string reqCategory = "Reinforcements";

            if (!CanRequestReinforcements(out message))
            {
                ModLogger.Warn(reqCategory, $"Reinforcement request blocked: {message}");
                return false;
            }

            var cost = CalculateReinforcementRequestCost();
            var toAdd = GetMissingSoldierCount();
            var cooldownDays = GetRelationCooldownDays();

            if (toAdd <= 0)
            {
                message = "No soldiers needed.";
                ModLogger.Debug(reqCategory, message);
                return false;
            }

            // Deduct gold using GiveGoldAction
            if (cost > 0 && Hero.MainHero != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost);
            }

            // Add soldiers
            if (TryAddSoldiers(toAdd, _state.SelectedTypeId, out var actuallyAdded, out var addMessage))
            {
                // Start cooldown based on relation
                _state.ReinforcementRequestCooldownEnd = CampaignTime.Now + CampaignTime.Days(cooldownDays);

                var t = new TextObject("{=enl_retinue_msg_reinforcements_arrived}{COUNT} soldiers have been assigned to your command.");
                t.SetTextVariable("COUNT", actuallyAdded);
                message = t.ToString();

                ModLogger.ActionResult(reqCategory, "ReinforcementRequest", true,
                    $"Added {actuallyAdded} soldiers for {cost} gold, cooldown={cooldownDays}d");

                return true;
            }

            // Refund gold on failure
            if (cost > 0 && Hero.MainHero != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, cost);
            }
            message = addMessage;
            ModLogger.ActionResult(reqCategory, "ReinforcementRequest", false, addMessage);
            return false;
        }

        /// <summary>
        /// Gets remaining cooldown days for reinforcement request.
        /// </summary>
        public int GetReinforcementRequestCooldownDays()
        {
            return _state.GetReinforcementRequestCooldownDays();
        }

        /// <summary>
        /// Checks if reinforcement request is off cooldown.
        /// </summary>
        public bool IsReinforcementRequestAvailable()
        {
            return _state.IsReinforcementRequestAvailable();
        }

        #endregion

        #region Loyalty Threshold Checking

        // Loyalty thresholds that trigger automatic events
        private const int ThresholdLow = 30;      // Warning event
        private const int ThresholdCritical = 20; // Serious desertion risk
        private const int ThresholdMutiny = 10;   // Crisis event
        private const int ThresholdHigh = 80;     // Positive event

        // Cooldown between threshold events (in days)
        private const int ThresholdEventCooldownDays = 7;

        // Hysteresis buffer to prevent oscillation (loyalty must move 5 points away before retriggering)
        private const int ThresholdHysteresis = 5;

        /// <summary>
        /// Checks if loyalty has crossed any critical thresholds and queues appropriate warning events.
        /// Called after loyalty changes from events or other sources.
        /// Prevents spam with 7-day cooldown and tracks last threshold to avoid ping-pong.
        /// </summary>
        public void CheckLoyaltyThresholds()
        {
            const string thresholdCategory = "LoyaltyThreshold";

            if (!_state.HasRetinue)
            {
                ModLogger.Debug(thresholdCategory, "Skipping threshold check: no active retinue");
                return;
            }

            var currentLoyalty = _state.RetinueLoyalty;
            var lastThreshold = _state.LastLoyaltyThresholdCrossed;
            var lastEventTime = _state.LastThresholdEventTime;

            // Check cooldown (7 days minimum between threshold events)
            if (lastEventTime != CampaignTime.Zero && !lastEventTime.IsPast)
            {
                var cooldownEnd = lastEventTime + CampaignTime.Days(ThresholdEventCooldownDays);
                if (CampaignTime.Now < cooldownEnd)
                {
                    var remaining = (cooldownEnd - CampaignTime.Now).ToDays;
                    ModLogger.Debug(thresholdCategory,
                        $"Threshold event on cooldown: {remaining:F1} days remaining");
                    return;
                }
            }

            // Determine current threshold state
            LoyaltyThreshold currentThreshold;
            string eventId;

            if (currentLoyalty < ThresholdMutiny)
            {
                currentThreshold = LoyaltyThreshold.Mutiny;
                eventId = "evt_ret_loyalty_mutiny";
            }
            else if (currentLoyalty < ThresholdCritical)
            {
                currentThreshold = LoyaltyThreshold.Critical;
                eventId = "evt_ret_loyalty_critical";
            }
            else if (currentLoyalty < ThresholdLow)
            {
                currentThreshold = LoyaltyThreshold.Low;
                eventId = "evt_ret_loyalty_low";
            }
            else if (currentLoyalty >= ThresholdHigh)
            {
                currentThreshold = LoyaltyThreshold.High;
                eventId = "evt_ret_loyalty_high";
            }
            else
            {
                // Loyalty in safe zone (30-79), no event needed
                // Reset threshold tracking if loyalty recovered to safe zone
                if (lastThreshold != LoyaltyThreshold.None && lastThreshold != LoyaltyThreshold.High)
                {
                    ModLogger.Info(thresholdCategory,
                        $"Loyalty recovered to safe zone ({currentLoyalty}), resetting threshold tracking");
                    _state.LastLoyaltyThresholdCrossed = LoyaltyThreshold.None;
                }
                return;
            }

            // Check if we already triggered this threshold (prevent duplicate events)
            if (lastThreshold == currentThreshold)
            {
                ModLogger.Debug(thresholdCategory,
                    $"Threshold {currentThreshold} already triggered, skipping duplicate event");
                return;
            }

            // Apply hysteresis: if moving to a less severe threshold, require larger gap
            // This prevents oscillation when loyalty hovers around a boundary
            if (IsLessUrgentThreshold(currentThreshold, lastThreshold))
            {
                var thresholdValue = (int)currentThreshold;
                var gap = Math.Abs(currentLoyalty - thresholdValue);

                if (gap < ThresholdHysteresis)
                {
                    ModLogger.Debug(thresholdCategory,
                        $"Hysteresis: loyalty {currentLoyalty} too close to threshold {thresholdValue} (gap={gap}, need={ThresholdHysteresis})");
                    return;
                }
            }

            // Queue the threshold event
            var evt = Content.EventCatalog.GetEvent(eventId);
            if (evt == null)
            {
                ModLogger.Error(thresholdCategory, $"Threshold event not found: {eventId}");
                return;
            }

            ModLogger.Info(thresholdCategory,
                $"Loyalty threshold crossed: {lastThreshold} -> {currentThreshold} (loyalty={currentLoyalty}), queuing event {eventId}");

            string thresholdName = currentThreshold.ToString();

            var director = Content.StoryDirector.Instance;
            if (director != null)
            {
                director.EmitCandidate(new Content.StoryCandidate
                {
                    SourceId = "retinue.loyalty." + thresholdName,
                    CategoryId = "retinue.loyalty." + thresholdName,
                    ProposedTier = Content.StoryTier.Modal,
                    SeverityHint = 0.70f,
                    Beats = { Content.StoryBeat.EscalationThreshold },
                    Relevance = new Content.RelevanceKey { TouchesEnlistedLord = true },
                    EmittedAt = CampaignTime.Now,
                    InteractiveEvent = evt,
                    RenderedTitle = evt.TitleFallback,
                    RenderedBody = evt.SetupFallback,
                    StoryKey = evt.Id
                });
            }
            else
            {
                Content.EventDeliveryManager.Instance?.QueueEvent(evt);
            }

            // Update threshold tracking (fires on both director and fallback paths)
            _state.LastLoyaltyThresholdCrossed = currentThreshold;
            _state.LastThresholdEventTime = CampaignTime.Now;
        }

        /// <summary>
        /// Checks if a threshold represents a less urgent situation than another.
        /// Used for hysteresis to prevent oscillation between thresholds.
        /// </summary>
        private static bool IsLessUrgentThreshold(LoyaltyThreshold current, LoyaltyThreshold previous)
        {
            // High is special: it's positive, not urgent
            if (current == LoyaltyThreshold.High || previous == LoyaltyThreshold.High)
            {
                return false;
            }

            // Higher threshold values are LESS urgent (Mutiny=10 is worse than Low=30)
            return (int)current > (int)previous;
        }

        #endregion

        #region Requisition System (Legacy - kept for compatibility)

        // Requisition configuration constants
        public const int RequisitionCooldownDays = 14;

        /// <summary>
        /// Checks if the player can perform an instant requisition.
        /// Validates: enlisted status, tier, cooldown, capacity, and gold.
        /// </summary>
        /// <param name="reason">Out: reason if cannot requisition</param>
        /// <returns>True if requisition is allowed</returns>
        public bool CanRequisition(out string reason)
        {
            reason = null;

            // Must be enlisted at Commander tier (T7+)
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                reason = new TextObject("{=enl_retinue_reason_must_be_enlisted_requisition}You must be enlisted to requisition soldiers.").ToString();
                return false;
            }

            if (enlistment.EnlistmentTier < CommanderTier1)
            {
                var t = new TextObject("{=enl_retinue_reason_requires_commander_min}Requires Commander rank (Tier {REQ}) or higher.");
                t.SetTextVariable("REQ", CommanderTier1);
                reason = t.ToString();
                return false;
            }

            // Must have a retinue type selected
            if (!_state.HasTypeSelected)
            {
                reason = new TextObject("{=enl_retinue_reason_select_type}You must select a soldier type first.").ToString();
                return false;
            }

            // Check cooldown
            if (!_state.IsRequisitionAvailable())
            {
                var daysRemaining = _state.GetRequisitionCooldownDays();
                var t = new TextObject("{=enl_retinue_reason_requisition_cooldown}Requisition on cooldown: {DAYS} days remaining.");
                t.SetTextVariable("DAYS", daysRemaining);
                reason = t.ToString();
                return false;
            }

            // Must have room for more soldiers
            var missing = GetMissingSoldierCount();
            if (missing <= 0)
            {
                reason = new TextObject("{=enl_retinue_reason_full_strength}Your retinue is at full strength.").ToString();
                return false;
            }

            // Must have enough gold
            var cost = CalculateRequisitionCost();
            var playerGold = Hero.MainHero?.Gold ?? 0;
            if (playerGold < cost)
            {
                var t = new TextObject("{=enl_retinue_reason_not_enough_gold}Not enough gold. Need {NEED} denars, have {HAVE}.");
                t.SetTextVariable("NEED", cost);
                t.SetTextVariable("HAVE", playerGold);
                reason = t.ToString();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates the total gold cost to requisition all missing soldiers.
        /// Uses native GetTroopRecruitmentCost formula multiplied by missing count.
        /// </summary>
        /// <returns>Total gold cost for requisition</returns>
        public int CalculateRequisitionCost()
        {
            var missing = GetMissingSoldierCount();
            if (missing <= 0)
            {
                return 0;
            }

            var costPerSoldier = GetAverageRecruitmentCost();
            return costPerSoldier * missing;
        }

        /// <summary>
        /// Gets the number of soldiers that can be added (respects tier and party limits).
        /// Uses the shared capacity check: Math.Min(tierCapacity - current, partyLimit - partyMembers)
        /// </summary>
        /// <returns>Number of soldiers that can be added</returns>
        public int GetMissingSoldierCount()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return 0;
            }

            // Tier capacity check
            var tierCapacity = GetTierCapacity(enlistment.EnlistmentTier);
            var currentSoldiers = _state.TotalSoldiers;
            var tierAvailable = tierCapacity - currentSoldiers;

            // Party size check
            var party = PartyBase.MainParty;
            if (party == null)
            {
                return 0;
            }

            var partySpace = party.PartySizeLimit - party.NumberOfAllMembers;

            // Take the more restrictive limit
            return Math.Max(0, Math.Min(tierAvailable, partySpace));
        }

        /// <summary>
        /// Gets the average recruitment cost per soldier for the selected type.
        /// </summary>
        private int GetAverageRecruitmentCost()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var culture = enlistment?.CurrentLord?.Culture;
            var playerTier = enlistment?.EnlistmentTier ?? 4;

            if (culture == null || string.IsNullOrEmpty(_state.SelectedTypeId))
            {
                // Fallback cost based on type
                return _state.SelectedTypeId switch
                {
                    "cavalry" or "horse_archers" => 200,
                    _ => 100
                };
            }

            var troops = GetAvailableTroops(_state.SelectedTypeId, culture, playerTier);
            if (troops == null || troops.Count == 0)
            {
                return 100;
            }

            // Calculate average cost across available troops
            var totalCost = 0;
            var count = 0;

            foreach (var troop in troops)
            {
                var cost = Campaign.Current?.Models?.PartyWageModel?
                    .GetTroopRecruitmentCost(troop, Hero.MainHero)
                    .RoundedResultNumber ?? 100;
                totalCost += cost;
                count++;
            }

            return count > 0 ? totalCost / count : 100;
        }

        /// <summary>
        /// Attempts to instantly fill all missing soldier slots for gold.
        /// Applies a 14-day cooldown on success.
        /// </summary>
        /// <param name="message">Out: result message for UI</param>
        /// <returns>True if requisition succeeded</returns>
        public bool TryRequisition(out string message)
        {
            const string reqCategory = "Requisition";

            if (!CanRequisition(out message))
            {
                ModLogger.Warn(reqCategory, $"Requisition blocked: {message}");
                return false;
            }

            var cost = CalculateRequisitionCost();
            var toAdd = GetMissingSoldierCount();

            if (toAdd <= 0)
            {
                message = "No soldiers needed.";
                ModLogger.Debug(reqCategory, message);
                return false;
            }

            // Deduct gold using GiveGoldAction (properly affects party treasury and updates UI)
            if (cost > 0 && Hero.MainHero != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost);
            }

            // Add soldiers
            if (TryAddSoldiers(toAdd, _state.SelectedTypeId, out var actuallyAdded, out var addMessage))
            {
                // Start cooldown
                _state.RequisitionCooldownEnd = CampaignTime.Now + CampaignTime.Days(RequisitionCooldownDays);

                message = $"{actuallyAdded} soldiers have reported for duty.";
                ModLogger.ActionResult(reqCategory, "Requisition", true,
                    $"Added {actuallyAdded} soldiers for {cost} gold, cooldown={RequisitionCooldownDays}d");

                return true;
            }

            // Refund gold on failure
            if (cost > 0 && Hero.MainHero != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, cost);
            }
            message = addMessage;
            ModLogger.ActionResult(reqCategory, "Requisition", false, addMessage);
            return false;
        }

        /// <summary>
        /// Gets remaining cooldown days for requisition.
        /// </summary>
        public int GetRequisitionCooldownDays()
        {
            return _state.GetRequisitionCooldownDays();
        }

        /// <summary>
        /// Checks if requisition is off cooldown.
        /// </summary>
        public bool IsRequisitionAvailable()
        {
            return _state.IsRequisitionAvailable();
        }

        #endregion

        #region Leadership Notification

        /// <summary>
        /// Shows the Commander's Retinue unlock notification dialog.
        /// Called when player reaches Tier 7 (Commander rank) for the first time.
        /// </summary>
        public static void ShowLeadershipNotification()
        {
            var title = new TextObject("{=ct_leadership_title}Commander's Commission");
            var message = new TextObject("{=ct_leadership_message}Your long service has been recognized. " +
                "As a Commander, you've been granted authority over your own retinue of soldiers.\n\n" +
                "Twenty raw recruits have been assigned to your command. Train them well - their lives are in your hands.\n\n" +
                "Visit Camp to manage your forces.");

            // Intentional bypass of StoryDirector — fires exactly once when the player reaches
            // T7, immediately after the promotion ceremony (PromotionBehavior.cs TriggerPromotionNotification).
            // Same two-beat chain reasoning as the ceremony bypass: routing through the Director
            // would trip the 60s wall-clock guard and swallow this commission modal.
            // pauseGameActiveState = false so notifications don't freeze game time
            InformationManager.ShowInquiry(
                new InquiryData(
                    title.ToString(),
                    message.ToString(),
                    true,
                    false,
                    new TextObject("{=ct_leadership_acknowledge}I will lead them well").ToString(),
                    string.Empty,
                    null,
                    null));

            ModLogger.Info(LogCategory, "Showed Commander (Tier 7) leadership notification");
        }

        #endregion
    }
}

