using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Camp;
using Enlisted.Features.Camp.Models;
using Enlisted.Features.Company;
using Enlisted.Features.Conditions;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Identity;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Features.Logistics;
using Enlisted.Features.Ranks;
using Enlisted.Features.Ranks.Behaviors;
using Enlisted.Features.Retinue.Core;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Entry;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Delivers events to the player via UI popups using MultiSelectionInquiryData.
    /// Manages event queue, checks option requirements, and applies effects when options are selected.
    /// Supports tier, role, skill, and trait requirements for event options.
    /// </summary>
    public class EventDeliveryManager : CampaignBehaviorBase
    {
        private const string LogCategory = "EVENTDELIVERY";

        public static EventDeliveryManager Instance { get; private set; }

        private readonly Queue<EventDefinition> _pendingEvents = new();
        private bool _isShowingEvent;
        private EventDefinition _currentEvent;

        // Save-backed snapshot of _pendingEvents. We serialize event Ids only and resolve
        // back to EventDefinition instances on load via EventCatalog. Full EventDefinition
        // graphs are authored content — the catalog is authoritative. See spec §5.2 and
        // docs/superpowers/specs/2026-04-19-event-meaning-design.md.
        private List<string> _pendingEventIds = new List<string>();

        // Bounded cap. Matches StoryDirector._deferredInteractive cap (see StoryDirector.cs
        // DeferredInteractiveCap). Prevents a pathological pile-up from growing unbounded
        // in save files.
        private const int PendingQueueCap = 32;

        // Debug-only accessors used by DebugToolsBehavior for save/reload verification.
        // Do not consume these from gameplay code — use the normal event flow instead.
        internal int PendingQueueCountForDebug => _pendingEvents.Count;
        internal IEnumerable<string> PendingQueueIdsForDebug => _pendingEvents.Select(e => e?.Id ?? "(null)");

        public EventDeliveryManager()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            // No campaign events needed - events are queued programmatically
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                // Snapshot the current live queue into the serializable ID list.
                _pendingEventIds = _pendingEvents.Select(evt => evt?.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
                if (_pendingEventIds.Count > PendingQueueCap)
                {
                    _pendingEventIds = _pendingEventIds.Take(PendingQueueCap).ToList();
                }
            }

            dataStore.SyncData("evt_delivery_pendingIds", ref _pendingEventIds);

            // Re-hydration happens lazily on the next tick via HydrateFromPendingIdsIfNeeded — not
            // here, because EventCatalog load-order relative to SyncData is not guaranteed.
        }

        /// <summary>
        /// Queues an event for delivery to the player.
        /// Events are shown one at a time in FIFO order.
        /// </summary>
        public void QueueEvent(EventDefinition evt)
        {
            HydrateFromPendingIdsIfNeeded();

            if (evt == null)
            {
                ModLogger.Expected(LogCategory, "event_queue_null", "Attempted to queue null event - check event ID and catalog loading");
                return;
            }

            if (_pendingEvents.Count >= PendingQueueCap)
            {
                // Drop the oldest to keep the queue bounded. Preserves the "newest event is most
                // relevant to current gameplay" heuristic while preventing runaway growth in save.
                var dropped = _pendingEvents.Dequeue();
                ModLogger.Expected(LogCategory, "queue_cap_exceeded",
                    $"Queue at cap ({PendingQueueCap}); dropped oldest event {dropped?.Id ?? "unknown"} to make room for {evt.Id}");
            }
            _pendingEvents.Enqueue(evt);
            ModLogger.Info(LogCategory, $"Queued event: {evt.Id} (queue size: {_pendingEvents.Count})");

            // Try to deliver immediately if no event is currently showing
            if (!_isShowingEvent)
            {
                TryDeliverNextEvent();
            }
        }

        /// <summary>
        /// Attempts to deliver the next event in the queue.
        /// Only delivers if no event is currently showing and queue is not empty.
        /// </summary>
        private void TryDeliverNextEvent()
        {
            HydrateFromPendingIdsIfNeeded();

            if (_isShowingEvent)
            {
                ModLogger.Debug(LogCategory, $"Event delivery blocked: currently showing {_currentEvent?.Id ?? "unknown"}");
                return;
            }

            if (_pendingEvents.Count == 0)
            {
                ModLogger.Debug(LogCategory, "Event delivery: queue is empty");
                return;
            }

            // Check if player is enlisted and not in invalid states
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                ModLogger.Debug(LogCategory, "Skipping event delivery - not enlisted");
                _pendingEvents.Clear(); // Clear queue when not enlisted
                return;
            }

            _currentEvent = _pendingEvents.Dequeue();
            _isShowingEvent = true;

            var category = _currentEvent.Category ?? "event";
            ModLogger.Info(LogCategory, $"Delivering {category}: {_currentEvent.Id} (queue remaining: {_pendingEvents.Count})");

            ShowEventPopup(_currentEvent);
        }

        // True until the first call after load has drained _pendingEventIds into _pendingEvents.
        private bool _needsHydration = true;

        private void HydrateFromPendingIdsIfNeeded()
        {
            if (!_needsHydration)
            {
                return;
            }
            _needsHydration = false;

            if (_pendingEventIds == null || _pendingEventIds.Count == 0)
            {
                return;
            }

            int resolved = 0;
            int dropped = 0;
            foreach (var id in _pendingEventIds)
            {
                // EventCatalog.GetEvent is static and self-initializing (see EventCatalog.cs:127).
                var evt = EventCatalog.GetEvent(id);
                if (evt != null)
                {
                    _pendingEvents.Enqueue(evt);
                    resolved++;
                }
                else
                {
                    dropped++;
                }
            }

            if (dropped > 0)
            {
                ModLogger.Expected(LogCategory, "queue_hydrate_dropped",
                    $"Dropped {dropped} pending event id(s) on load — not found in catalog (removed/renamed in content?)");
            }

            ModLogger.Info(LogCategory, $"Hydrated {resolved} pending event(s) from save");
            _pendingEventIds.Clear();  // Live queue is authoritative from here.
        }

        /// <summary>
        /// Shows the event popup using MultiSelectionInquiryData.
        /// </summary>
        private void ShowEventPopup(EventDefinition evt)
        {
            var options = BuildOptions(evt);

            if (options.Count == 0)
            {
                ModLogger.Expected(LogCategory, "event_no_valid_options", $"Event {evt.Id} has no valid options - check requirements in JSON");
                OnEventClosed();
                return;
            }

            // Resolve localized text with inline fallback for when XML lookup fails
            var titleText = ResolveText(evt.TitleId, evt.TitleFallback);
            var descriptionText = ResolveText(evt.SetupId, evt.SetupFallback);

            var inquiry = new MultiSelectionInquiryData(
                titleText: titleText,
                descriptionText: descriptionText,
                inquiryElements: options,
                isExitShown: false, // Force player to choose (no escape)
                minSelectableOptionCount: 1,
                maxSelectableOptionCount: 1,
                affirmativeText: new TextObject("{=str_ok}Confirm").ToString(),
                negativeText: null,
                affirmativeAction: OnOptionSelected,
                negativeAction: null
            );

            MBInformationManager.ShowMultiSelectionInquiry(inquiry, true);
        }

        /// <summary>
        /// Builds the list of inquiry elements from event options.
        /// Checks requirements and disables options that don't meet them.
        /// </summary>
        private List<InquiryElement> BuildOptions(EventDefinition evt)
        {
            var options = new List<InquiryElement>();
            var hero = Hero.MainHero;

            foreach (var option in evt.Options)
            {
                var meetsRequirements = MeetsRequirements(option.Requirements, hero);
                var hint = GetOptionHint(option, hero);
                var optionText = ResolveText(option.TextId, option.TextFallback);

                options.Add(new InquiryElement(
                    identifier: option, // Store the EventOption object
                    title: optionText,
                    imageIdentifier: null,
                    isEnabled: meetsRequirements,
                    hint: hint
                ));
            }

            return options;
        }

        /// <summary>
        /// Checks if a hero meets the requirements for an event option.
        /// </summary>
        private bool MeetsRequirements(EventOptionRequirements requirements, Hero hero)
        {
            if (requirements == null)
            {
                return true; // No requirements = always available
            }

            // Check tier requirement
            if (requirements.MinTier.HasValue)
            {
                var playerTier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
                if (playerTier < requirements.MinTier.Value)
                {
                    return false;
                }
            }

            // Check role requirement against player's current specialization
            if (!string.IsNullOrEmpty(requirements.Role) &&
                !requirements.Role.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                var playerRole = EnlistmentBehavior.Instance?.CurrentSpecialization ?? "Soldier";
                if (!playerRole.Equals(requirements.Role, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Check skill requirements
            if (requirements.MinSkills != null)
            {
                foreach (var skillReq in requirements.MinSkills)
                {
                    var skill = SkillCheckHelper.GetSkillByName(skillReq.Key);
                    if (skill != null && !SkillCheckHelper.MeetsSkillThreshold(hero, skill, skillReq.Value))
                    {
                        return false;
                    }
                }
            }

            // Check trait requirements
            if (requirements.MinTraits != null)
            {
                foreach (var traitReq in requirements.MinTraits)
                {
                    var trait = SkillCheckHelper.GetTraitByName(traitReq.Key);
                    if (trait != null && !SkillCheckHelper.MeetsTraitThreshold(hero, trait, traitReq.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the tooltip hint for an option explaining requirements or consequences.
        /// </summary>
        private string GetOptionHint(EventOption option, Hero hero)
        {
            if (option.Requirements == null)
            {
                // No requirements - show tooltip if provided
                return option.Tooltip ?? string.Empty;
            }

            // Build requirements hint
            var hints = new List<string>();

            // Tier requirement
            if (option.Requirements.MinTier.HasValue)
            {
                var playerTier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
                var tierReq = option.Requirements.MinTier.Value;
                if (playerTier < tierReq)
                {
                    hints.Add($"Requires Rank {tierReq}+");
                }
            }

            // Role requirement - show hint if player doesn't have the required specialization
            if (!string.IsNullOrEmpty(option.Requirements.Role) &&
                !option.Requirements.Role.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                var playerRole = EnlistmentBehavior.Instance?.CurrentSpecialization ?? "Soldier";
                if (!playerRole.Equals(option.Requirements.Role, StringComparison.OrdinalIgnoreCase))
                {
                    hints.Add($"Requires {option.Requirements.Role} specialization");
                }
            }

            // Skill requirements
            if (option.Requirements.MinSkills != null)
            {
                foreach (var skillReq in option.Requirements.MinSkills)
                {
                    var skill = SkillCheckHelper.GetSkillByName(skillReq.Key);
                    if (skill != null)
                    {
                        var currentLevel = hero.GetSkillValue(skill);
                        if (currentLevel < skillReq.Value)
                        {
                            hints.Add($"Requires {skillReq.Key} {skillReq.Value}+ (You have {currentLevel})");
                        }
                    }
                }
            }

            // Trait requirements
            if (option.Requirements.MinTraits != null)
            {
                foreach (var traitReq in option.Requirements.MinTraits)
                {
                    var trait = SkillCheckHelper.GetTraitByName(traitReq.Key);
                    if (trait != null)
                    {
                        var currentLevel = hero.GetTraitLevel(trait);
                        if (currentLevel < traitReq.Value)
                        {
                            hints.Add($"Requires {traitReq.Key} trait level {traitReq.Value}+ (You have {currentLevel})");
                        }
                    }
                }
            }

            // If requirements are met, show tooltip
            if (hints.Count == 0 && !string.IsNullOrEmpty(option.Tooltip))
            {
                return option.Tooltip;
            }

            return hints.Count > 0 ? string.Join("\n", hints) : string.Empty;
        }

        /// <summary>
        /// Called when the player selects an option.
        /// Applies effects and shows result text.
        /// </summary>
        private void OnOptionSelected(List<InquiryElement> selected)
        {
            if (selected == null || selected.Count == 0)
            {
                ModLogger.Warn(LogCategory, "No option selected");
                OnEventClosed();
                return;
            }

            var selectedElement = selected[0];
            var option = selectedElement.Identifier as EventOption;

            if (option == null)
            {
                ModLogger.Expected(LogCategory, "event_option_wrong_type", "Selected option identifier is not an EventOption - internal error");
                OnEventClosed();
                return;
            }

            ModLogger.Info(LogCategory, $"Option selected: {option.Id}");

            // Record cooldown for player-initiated decisions ONLY if they commit to an action
            // (not if they select a cancel/decline option)
            if (_currentEvent != null && _currentEvent.Category != null &&
                _currentEvent.Category.Equals("decision", StringComparison.OrdinalIgnoreCase))
            {
                // Check if this is a cancel option (common patterns: cancel, nevermind, not_now, decline, skip, back)
                var isCancelOption = option.Id.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     option.Id.IndexOf("nevermind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     option.Id.IndexOf("not_now", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     option.Id.IndexOf("skip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     option.Id.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isCancelOption)
                {
                    ModLogger.Info(LogCategory, $"Non-cancel option selected: {option.Id} - recording cooldown");
                    
                    // Player committed to an action - record decision cooldown
                    DecisionManager.Instance?.RecordDecisionSelected(_currentEvent.Id);
                    ModLogger.Info(LogCategory, $"Decision cooldown recorded for: {_currentEvent.Id}");

                    // Also record opportunity engagement if this decision was triggered by a camp opportunity
                    // This ensures non-immediate opportunities get their cooldown tracked properly
                    if (!string.IsNullOrEmpty(_currentEvent.OriginatingOpportunityId) &&
                        !string.IsNullOrEmpty(_currentEvent.OriginatingOpportunityType))
                    {
                        ModLogger.Debug(LogCategory, $"Recording opportunity engagement: {_currentEvent.OriginatingOpportunityId}");
                        var generator = CampOpportunityGenerator.Instance;
                        if (generator != null)
                        {
                            if (System.Enum.TryParse<OpportunityType>(_currentEvent.OriginatingOpportunityType, out var oppType))
                            {
                                generator.RecordEngagement(_currentEvent.OriginatingOpportunityId, oppType);
                                ModLogger.Info(LogCategory, $"Opportunity engagement recorded for: {_currentEvent.OriginatingOpportunityId}");
                            }
                        }
                    }
                }
                else
                {
                    ModLogger.Info(LogCategory, $"Cancel option selected: {option.Id} - no cooldown recorded for {_currentEvent?.Id}");
                }
            }
            
            // Track declined promotions (player chose "not ready" or "decline" in proving event)
            if (_currentEvent != null && _currentEvent.Id.StartsWith("promotion_", StringComparison.OrdinalIgnoreCase))
            {
                if (option.Id.IndexOf("not_ready", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    option.Id.IndexOf("decline", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Extract target tier from event ID (e.g., "promotion_t6_t7_commanders_commission" -> tier 7)
                    var parts = _currentEvent.Id.Split('_');
                    if (parts.Length >= 3 && parts[2].StartsWith("t", StringComparison.OrdinalIgnoreCase))
                    {
                        var tierStr = parts[2].Substring(1);
                        if (int.TryParse(tierStr, out var targetTier))
                        {
                            EscalationManager.Instance?.RecordDeclinedPromotion(targetTier);
                            ModLogger.Info(LogCategory, $"Player declined promotion to tier {targetTier} - must request via dialog");
                        }
                    }
                }
            }

            // Determine if this is a risky option and resolve success/failure
            var isRisky = option.RiskChance.HasValue && option.RiskChance.Value > 0 && option.RiskChance.Value < 100;
            var success = true;

            if (isRisky)
            {
                // Calculate actual chance, including skill modifier if specified
                var actualChance = CalculateSkillModifiedChance(option);
                var roll = MBRandom.RandomInt(100);
                success = roll < actualChance;
                
                if (!string.IsNullOrEmpty(option.SkillCheck))
                {
                    ModLogger.Info(LogCategory, $"Skill-modified roll: {roll} vs {actualChance}% ({option.SkillCheck} check, base {option.RiskChance.Value}%) -> {(success ? "SUCCESS" : "FAILURE")}");
                }
                else
                {
                    ModLogger.Info(LogCategory, $"Risky option roll: {roll} vs {actualChance}% -> {(success ? "SUCCESS" : "FAILURE")}");
                }
            }
            else
            {
                ModLogger.Debug(LogCategory, $"Non-risky option selected: {option.Id}");
            }

            // Apply base effects first (always applied)
            ApplyEffects(option.Effects);

            // Apply success or failure effects based on outcome
            if (isRisky)
            {
                if (success && option.EffectsSuccess != null)
                {
                    ApplyEffects(option.EffectsSuccess);
                }
                else if (!success && option.EffectsFailure != null)
                {
                    ApplyEffects(option.EffectsFailure);
                }
            }

            // Apply flag changes
            ApplyFlagChanges(option);

            // Handle enlistment abort (used by bag check)
            if (option.AbortsEnlistment)
            {
                ApplyEnlistmentAbort();
                // Still show result text and continue normally
            }

            // Notify news system of event outcome (includes narrative for Recent Activities display)
            NotifyNewsOfEventOutcome(option, isRisky && !success);

            // Show result text in combat log for decisions (immediate feedback)
            if (_currentEvent != null && _currentEvent.Category != null &&
                _currentEvent.Category.Equals("decision", StringComparison.OrdinalIgnoreCase))
            {
                SendDecisionOutcomeToCombatLog(option, isRisky && !success);
            }

            // Notify news of pending chain event if applicable
            NotifyNewsOfPendingChainEvent(option);

            // Handle reward_choices if present (sub-choice popups for training focus, etc.)
            // We skip the outcome narrative popup but still need to show reward choices
            if (option.RewardChoices != null && option.RewardChoices.Options.Count > 0)
            {
                ShowSubChoicePopup(option.RewardChoices);
            }

            // Result narrative is now shown in Recent Activities feed instead of popup to reduce UI interruption
            // ShowResultText(option, isRisky && !success);

            // Handle immediate chain events (backwards compatibility)
            if (!string.IsNullOrEmpty(option.Effects?.ChainEventId))
            {
                var chainEvent = EventCatalog.GetEvent(option.Effects.ChainEventId);
                if (chainEvent != null)
                {
                    ModLogger.Info(LogCategory, $"Queuing immediate chain event: {option.Effects.ChainEventId}");
                    QueueEvent(chainEvent);
                }
                else
                {
                    ModLogger.Warn(LogCategory, $"Immediate chain event not found: {option.Effects.ChainEventId}");
                }
            }

            // Handle delayed chain events
            if (!string.IsNullOrEmpty(option.ChainsTo) && option.ChainDelayHours > 0)
            {
                var escalationState = EscalationManager.Instance?.State;
                if (escalationState != null)
                {
                    escalationState.ScheduleChainEvent(option.ChainsTo, option.ChainDelayHours);
                    ModLogger.Info(LogCategory,
                        $"Scheduled chain event: {option.ChainsTo} in {option.ChainDelayHours} hours ({option.ChainDelayHours / 24f:F1} days)");
                }
                else
                {
                    ModLogger.Warn(LogCategory, "Cannot schedule chain event - EscalationManager not available");
                }
            }

            OnEventClosed();
        }

        /// <summary>
        /// Applies the effects of a selected event option.
        /// </summary>
        private void ApplyEffects(EventEffects effects)
        {
            if (effects == null)
            {
                return;
            }

            var hero = Hero.MainHero;
            var enlistment = EnlistmentBehavior.Instance;
            var escalation = EscalationManager.Instance;

            // Track applied effects for player feedback
            var feedbackMessages = new List<string>();

            // Apply skill XP and track total for enlistment progression
            int totalSkillXpAwarded = 0;
            if (effects.SkillXp != null)
            {
                foreach (var skillXp in effects.SkillXp)
                {
                    var skill = SkillCheckHelper.GetSkillByName(skillXp.Key);
                    if (skill != null)
                    {
                        hero.AddSkillXp(skill, skillXp.Value);
                        totalSkillXpAwarded += skillXp.Value;
                        feedbackMessages.Add($"+{skillXp.Value} {skillXp.Key} XP");
                        ModLogger.Debug(LogCategory, $"Applied {skillXp.Value} XP to {skillXp.Key}");
                    }
                }
            }

            // Apply dynamic skill XP from effects
            if (effects.DynamicSkillXp != null && effects.DynamicSkillXp.Count > 0)
            {
                foreach (var dynamicXp in effects.DynamicSkillXp)
                {
                    if (dynamicXp.Value <= 0)
                    {
                        ModLogger.Warn(LogCategory, $"Invalid dynamic XP value for {dynamicXp.Key}: {dynamicXp.Value}");
                        continue;
                    }

                    SkillObject targetSkill;

                    switch (dynamicXp.Key.ToLowerInvariant())
                    {
                        case "equipped_weapon":
                            targetSkill = WeaponSkillHelper.GetEquippedWeaponSkill(hero);
                            break;
                        case "weakest_combat":
                            targetSkill = WeaponSkillHelper.GetWeakestCombatSkill(hero);
                            break;
                        default:
                            ModLogger.Warn(LogCategory, $"Unknown dynamic XP key in effects: {dynamicXp.Key}");
                            continue;
                    }

                    if (targetSkill != null)
                    {
                        hero.AddSkillXp(targetSkill, dynamicXp.Value);
                        totalSkillXpAwarded += dynamicXp.Value;
                        ModLogger.Debug(LogCategory,
                            $"Applied dynamic {dynamicXp.Key} XP from effects: +{dynamicXp.Value} {targetSkill.Name}");
                    }
                    else
                    {
                        ModLogger.Warn(LogCategory, $"Could not resolve dynamic skill for {dynamicXp.Key}");
                    }
                }
            }

            // Award enlistment XP based on skill XP earned (for rank progression in muster reports).
            // This is a critical XP source that players see in their muster period summary.
            if (totalSkillXpAwarded > 0 && enlistment is { IsEnlisted: true })
            {
                var eventTitle = _currentEvent?.TitleFallback ?? "Event";
                enlistment.AddEnlistmentXP(totalSkillXpAwarded, $"Event: {eventTitle}");
                ModLogger.Debug(LogCategory, $"Awarded {totalSkillXpAwarded} enlistment XP for event: {eventTitle}");
            }

            // Apply trait XP
            if (effects.TraitXp != null)
            {
                foreach (var traitXp in effects.TraitXp)
                {
                    var trait = SkillCheckHelper.GetTraitByName(traitXp.Key);
                    if (trait != null)
                    {
                        TraitHelper.AwardTraitXp(hero, trait, traitXp.Value);
                        ModLogger.Debug(LogCategory, $"Applied {traitXp.Value} trait XP to {traitXp.Key}");
                    }
                }
            }

            // Apply lord reputation changes (uses native Hero.GetRelation system)
            if (effects.LordRep.HasValue && escalation != null)
            {
                escalation.ModifyLordReputation(effects.LordRep.Value, "event");
                feedbackMessages.Add($"{(effects.LordRep.Value > 0 ? "+" : "")}{effects.LordRep.Value} Lord Reputation");
                ModLogger.Debug(LogCategory, $"Modified lord reputation by {effects.LordRep.Value}");
            }

            // Officer/Soldier reputation removed in Phase 3 - JSON files converted to use lordReputation

            // Apply escalation changes
            if (effects.Scrutiny.HasValue && escalation != null)
            {
                escalation.ModifyScrutiny(effects.Scrutiny.Value, "event");
                ModLogger.Debug(LogCategory, $"Modified scrutiny by {effects.Scrutiny.Value}");
            }

            // Discipline merged into Scrutiny in Phase 1 - JSON files converted to use scrutiny (0-100 scale)

            if (effects.MedicalRisk.HasValue && escalation != null)
            {
                escalation.ModifyMedicalRisk(effects.MedicalRisk.Value, "event");
                ModLogger.Debug(LogCategory, $"Modified medical risk by {effects.MedicalRisk.Value}");
            }

            // Apply gold
            if (effects.Gold.HasValue && effects.Gold.Value != 0)
            {
                if (effects.Gold.Value > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, hero, effects.Gold.Value);
                    feedbackMessages.Add($"+{effects.Gold.Value} gold");
                }
                else
                {
                    GiveGoldAction.ApplyBetweenCharacters(hero, null, -effects.Gold.Value);
                    feedbackMessages.Add($"{effects.Gold.Value} gold");
                }
                ModLogger.Debug(LogCategory, $"Applied gold change: {effects.Gold.Value}");
            }

            // Apply HP change
            if (effects.HpChange.HasValue && effects.HpChange.Value != 0)
            {
                hero.HitPoints += effects.HpChange.Value;
                // Clamp to valid range
                if (hero.HitPoints > hero.CharacterObject.MaxHitPoints())
                {
                    hero.HitPoints = hero.CharacterObject.MaxHitPoints();
                }
                if (hero.HitPoints < 1)
                {
                    hero.HitPoints = 1; // Prevent death
                }
                feedbackMessages.Add($"{(effects.HpChange.Value > 0 ? "+" : "")}{effects.HpChange.Value} HP");
                ModLogger.Debug(LogCategory, $"Applied HP change: {effects.HpChange.Value}");
            }

            // Apply troop loss
            if (effects.TroopLoss.HasValue && effects.TroopLoss.Value > 0)
            {
                var party = MobileParty.MainParty;
                if (party != null)
                {
                    ApplyTroopLoss(party, effects.TroopLoss.Value);
                    ModLogger.Debug(LogCategory, $"Applied troop loss: {effects.TroopLoss.Value}");
                }
            }

            // Apply troop wounded
            if (effects.TroopWounded.HasValue && effects.TroopWounded.Value > 0)
            {
                var party = MobileParty.MainParty;
                if (party != null)
                {
                    ApplyTroopWounded(party, effects.TroopWounded.Value);
                    ModLogger.Debug(LogCategory, $"Applied troop wounded: {effects.TroopWounded.Value}");
                }
            }

            // Apply food loss
            if (effects.FoodLoss.HasValue && effects.FoodLoss.Value > 0)
            {
                var party = MobileParty.MainParty;
                if (party != null)
                {
                    ApplyFoodLoss(party, effects.FoodLoss.Value);
                    ModLogger.Debug(LogCategory, $"Applied food loss: {effects.FoodLoss.Value}");
                }
            }

            // Apply troop XP
            if (effects.TroopXp.HasValue && effects.TroopXp.Value > 0)
            {
                ApplyTroopXp(effects.TroopXp.Value);
                feedbackMessages.Add($"+{effects.TroopXp.Value} Troop XP");
                ModLogger.Debug(LogCategory, $"Applied troop XP: {effects.TroopXp.Value}");
            }

            // Apply company needs changes
            if (effects.CompanyNeeds != null && enlistment?.CompanyNeeds != null)
            {
                foreach (var needChange in effects.CompanyNeeds)
                {
                    if (Enum.TryParse<CompanyNeed>(needChange.Key, true, out var need))
                    {
                        var oldValue = enlistment.CompanyNeeds.GetNeed(need);
                        var newValue = oldValue + needChange.Value;
                        enlistment.CompanyNeeds.SetNeed(need, newValue);
                        ModLogger.Debug(LogCategory, $"Modified {need}: {oldValue} -> {newValue}");
                    }
                }
            }

            // Apply renown
            if (effects.Renown.HasValue && effects.Renown.Value != 0)
            {
                hero.Clan.AddRenown(effects.Renown.Value);
                ModLogger.Debug(LogCategory, $"Applied renown change: {effects.Renown.Value}");
            }

            // Apply retinue gain - adds soldiers to Commander's retinue
            if (effects.RetinueGain.HasValue && effects.RetinueGain.Value > 0)
            {
                ApplyRetinueGain(effects.RetinueGain.Value);
            }

            // Apply retinue loss - removes soldiers from Commander's retinue
            if (effects.RetinueLoss.HasValue && effects.RetinueLoss.Value > 0)
            {
                ApplyRetinueLoss(effects.RetinueLoss.Value);
            }

            // Apply retinue wounded - wounds soldiers in Commander's retinue
            if (effects.RetinueWounded.HasValue && effects.RetinueWounded.Value > 0)
            {
                ApplyRetinueWounded(effects.RetinueWounded.Value);
            }

            // Apply retinue loyalty change
            if (effects.RetinueLoyalty.HasValue && effects.RetinueLoyalty.Value != 0)
            {
                ApplyRetinueLoyalty(effects.RetinueLoyalty.Value);
                feedbackMessages.Add($"{(effects.RetinueLoyalty.Value > 0 ? "+" : "")}{effects.RetinueLoyalty.Value} Retinue Loyalty");
            }

            // Apply baggage train effects
            if (effects.GrantTemporaryBaggageAccess.HasValue && effects.GrantTemporaryBaggageAccess.Value > 0)
            {
                ApplyBaggageAccessGrant(effects.GrantTemporaryBaggageAccess.Value);
            }

            if (effects.BaggageDelayDays.HasValue)
            {
                ApplyBaggageDelay(effects.BaggageDelayDays.Value);
            }

            if (effects.RandomBaggageLoss.HasValue && effects.RandomBaggageLoss.Value > 0)
            {
                ApplyRandomBaggageLoss(effects.RandomBaggageLoss.Value);
            }

            // Apply bag check choice (first-enlistment gear handling)
            if (!string.IsNullOrEmpty(effects.BagCheckChoice))
            {
                ApplyBagCheckChoice(effects.BagCheckChoice);
            }

            // Apply medical effects (illness, injury, treatment, worsening)
            if (!string.IsNullOrEmpty(effects.IllnessOnset))
            {
                ApplyIllnessOnset(effects.IllnessOnset);
            }

            if (!string.IsNullOrEmpty(effects.InjuryOnset))
            {
                ApplyInjuryOnset(effects.InjuryOnset);
            }

            if (effects.BeginTreatment == true)
            {
                ApplyBeginTreatment();
            }

            if (effects.WorsenCondition == true)
            {
                ApplyWorsenCondition();
            }

            // Apply discharge if specified. Ends the player's enlistment with the given band.
            if (!string.IsNullOrEmpty(effects.TriggersDischarge))
            {
                ApplyDischargeEffect(effects.TriggersDischarge);
                feedbackMessages.Add($"Discharged: {effects.TriggersDischarge}");
            }

            // Apply promotion if specified. Used by proving events to grant tier advancement.
            // Note: "promotes": true in JSON is parsed as -1 (sentinel for "promote to next tier").
            // We must check != 0, not > 0, to handle both explicit tiers (e.g., 3) and the -1 sentinel.
            if (effects.Promotes.HasValue && effects.Promotes.Value != 0)
            {
                ApplyPromotesEffect(effects.Promotes.Value);
                // Don't show raw tier number in feedback - ApplyPromotesEffect handles culture-specific notifications
            }

            // Display feedback to player if any effects were applied
            if (feedbackMessages.Count > 0)
            {
                var message = string.Join(", ", feedbackMessages);
                InformationManager.DisplayMessage(new InformationMessage(message, Colors.Green));
                ModLogger.Info(LogCategory, $"Effects applied: {message}");
            }

            // Check for trait milestones after applying effects (native trait integration)
            // This detects when personality traits cross level thresholds and pushes news
            TraitMilestoneTracker.CheckForMilestones(Hero.MainHero);
        }

        /// <summary>
        /// Adds soldiers to the player's retinue from event effects.
        /// Only works if player has Commander rank (T7+) and has selected a retinue type.
        /// </summary>
        private void ApplyRetinueGain(int count)
        {
            var manager = RetinueManager.Instance;
            if (manager?.State == null || !manager.State.HasTypeSelected)
            {
                ModLogger.Warn(LogCategory, $"ApplyRetinueGain: Cannot add {count} soldiers - no retinue type selected");
                return;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || enlistment.EnlistmentTier < RetinueManager.CommanderTier1)
            {
                ModLogger.Warn(LogCategory, $"ApplyRetinueGain: Cannot add {count} soldiers - not at Commander rank");
                return;
            }

            if (manager.TryAddSoldiers(count, manager.State.SelectedTypeId, out var added, out var message))
            {
                ModLogger.Info(LogCategory, $"ApplyRetinueGain: Added {added} soldiers via event");

                var notification = new TextObject("{=evt_ret_gain_notification}{COUNT} soldiers have joined your retinue.");
                notification.SetTextVariable("COUNT", added);
                InformationManager.DisplayMessage(new InformationMessage(notification.ToString(), Colors.Green));
            }
            else
            {
                ModLogger.Warn(LogCategory, $"ApplyRetinueGain failed: {message}");
            }
        }

        /// <summary>
        /// Removes soldiers from the player's retinue specifically.
        /// Only removes troops tracked in RetinueState (does not affect lord's main force).
        /// Used for dramatic events like ambush, betrayal, or desertion.
        /// </summary>
        private void ApplyRetinueLoss(int count)
        {
            var manager = RetinueManager.Instance;
            var state = manager?.State;
            if (state == null || !state.HasRetinue)
            {
                ModLogger.Warn(LogCategory, $"ApplyRetinueLoss: Cannot remove {count} soldiers - no active retinue");
                return;
            }

            var party = MobileParty.MainParty;
            if (party?.MemberRoster == null)
            {
                ModLogger.Warn(LogCategory, "ApplyRetinueLoss: No party roster available");
                return;
            }

            var removed = 0;
            var roster = party.MemberRoster;

            // Remove only troops tracked in RetinueState.TroopCounts
            foreach (var kvp in state.TroopCounts.ToList())
            {
                if (removed >= count)
                {
                    break;
                }

                var characterId = kvp.Key;
                var trackedCount = kvp.Value;

                var character = CharacterObject.Find(characterId);
                if (character == null)
                {
                    continue;
                }

                var rosterCount = roster.GetTroopCount(character);
                var toRemove = Math.Min(Math.Min(trackedCount, rosterCount), count - removed);

                if (toRemove > 0)
                {
                    roster.AddToCounts(character, -toRemove, removeDepleted: true);
                    state.UpdateTroopCount(characterId, -toRemove);
                    removed += toRemove;
                }
            }

            if (removed > 0)
            {
                ModLogger.Info(LogCategory, $"ApplyRetinueLoss: Removed {removed} retinue soldiers");

                var notification = new TextObject("{=evt_ret_loss_notification}{COUNT} soldiers have been lost from your retinue.");
                notification.SetTextVariable("COUNT", removed);
                InformationManager.DisplayMessage(new InformationMessage(notification.ToString(), Colors.Red));
            }
            else
            {
                ModLogger.Warn(LogCategory, $"ApplyRetinueLoss: No soldiers could be removed (requested {count})");
            }
        }

        /// <summary>
        /// Wounds soldiers in the player's retinue specifically.
        /// Only wounds troops tracked in RetinueState (moves healthy to wounded roster).
        /// Used for events involving skirmishes, accidents, or hardship.
        /// </summary>
        private void ApplyRetinueWounded(int count)
        {
            var manager = RetinueManager.Instance;
            var state = manager?.State;
            if (state == null || !state.HasRetinue)
            {
                ModLogger.Warn(LogCategory, $"ApplyRetinueWounded: Cannot wound {count} soldiers - no active retinue");
                return;
            }

            var party = MobileParty.MainParty;
            if (party?.MemberRoster == null)
            {
                ModLogger.Warn(LogCategory, "ApplyRetinueWounded: No party roster available");
                return;
            }

            var wounded = 0;
            var roster = party.MemberRoster;

            // Wound only troops tracked in RetinueState.TroopCounts
            foreach (var kvp in state.TroopCounts.ToList())
            {
                if (wounded >= count)
                {
                    break;
                }

                var characterId = kvp.Key;
                var character = CharacterObject.Find(characterId);
                if (character == null)
                {
                    continue;
                }

                // Get current roster state for this troop
                var rosterIndex = roster.FindIndexOfTroop(character);
                if (rosterIndex < 0)
                {
                    continue;
                }

                var element = roster.GetElementCopyAtIndex(rosterIndex);
                var healthyCount = element.Number - element.WoundedNumber;

                if (healthyCount <= 0)
                {
                    continue; // All already wounded
                }

                var toWound = Math.Min(healthyCount, count - wounded);
                if (toWound > 0)
                {
                    roster.AddToCounts(character, 0, woundedCount: toWound);
                    wounded += toWound;
                }
            }

            if (wounded > 0)
            {
                ModLogger.Info(LogCategory, $"ApplyRetinueWounded: Wounded {wounded} retinue soldiers");

                var notification = new TextObject("{=evt_ret_wounded_notification}{COUNT} of your soldiers have been wounded.");
                notification.SetTextVariable("COUNT", wounded);
                InformationManager.DisplayMessage(new InformationMessage(notification.ToString(), Colors.Yellow));
            }
            else
            {
                ModLogger.Warn(LogCategory, $"ApplyRetinueWounded: No soldiers could be wounded (requested {count})");
            }
        }

        /// <summary>
        /// Applies a loyalty change to the player's retinue.
        /// Loyalty affects morale, desertion risk, and combat effectiveness.
        /// Only applies if player has Commander rank (T7+) and has a retinue.
        /// Automatically checks for threshold crossings after applying the change.
        /// </summary>
        private void ApplyRetinueLoyalty(int delta)
        {
            var manager = RetinueManager.Instance;
            var state = manager?.State;
            if (state == null || !state.HasRetinue)
            {
                ModLogger.Debug(LogCategory, $"ApplyRetinueLoyalty: Skipping {delta:+#;-#;0} - no active retinue");
                return;
            }

            var oldLoyalty = state.RetinueLoyalty;
            var newValue = state.RetinueLoyalty + delta;
            state.RetinueLoyalty = newValue < 0 ? 0 : (newValue > 100 ? 100 : newValue);
            var newLoyalty = state.RetinueLoyalty;

            ModLogger.Info(LogCategory, $"ApplyRetinueLoyalty: {oldLoyalty} -> {newLoyalty} ({delta:+#;-#;0})");

            // Record loyalty change for news feed
            var dayNumber = (int)(CampaignTime.Now.ToDays);
            var message = delta > 0 ? "Improved morale from event" : "Morale impact from event";
            EnlistedNewsBehavior.Instance?.AddReputationChange("Retinue", delta, newLoyalty, message, dayNumber);

            // Show notification if loyalty changed significantly
            if (Math.Abs(delta) >= 5)
            {
                var loyaltyText = newLoyalty switch
                {
                    >= 80 => "high",
                    >= 60 => "good",
                    >= 40 => "fair",
                    >= 20 => "low",
                    _ => "critical"
                };

                var notification = new TextObject("{=evt_ret_loyalty_notification}Retinue loyalty {CHANGE}. Current: {LEVEL}");
                notification.SetTextVariable("CHANGE", delta > 0 ? "increased" : "decreased");
                notification.SetTextVariable("LEVEL", loyaltyText);

                var color = delta > 0 ? Colors.Green : Colors.Red;
                InformationManager.DisplayMessage(new InformationMessage(notification.ToString(), color));
            }

            // Check for threshold crossings after applying loyalty change
            manager.CheckLoyaltyThresholds();
        }

        /// <summary>
        /// Grants temporary baggage access for the specified number of hours.
        /// Used by "baggage caught up" events to give players a window to access their stash.
        /// </summary>
        private void ApplyBaggageAccessGrant(int hours)
        {
            var baggageManager = BaggageTrainManager.Instance;
            if (baggageManager == null)
            {
                ModLogger.Warn(LogCategory, $"ApplyBaggageAccessGrant: BaggageTrainManager not available");
                return;
            }

            baggageManager.GrantTemporaryAccess(hours);
            baggageManager.RecordBaggageArrival(); // Track arrival for Daily Brief display

            var notification = new TextObject("{=evt_baggage_access_granted}The baggage wagons have caught up. You have {HOURS} hours to access your belongings.");
            notification.SetTextVariable("HOURS", hours);
            InformationManager.DisplayMessage(new InformationMessage(notification.ToString(), Colors.Green));
        }

        /// <summary>
        /// Applies a delay to baggage availability.
        /// A value of 0 clears any existing delay, positive values add delay in days.
        /// </summary>
        private void ApplyBaggageDelay(int days)
        {
            var baggageManager = BaggageTrainManager.Instance;
            if (baggageManager == null)
            {
                ModLogger.Warn(LogCategory, "ApplyBaggageDelay: BaggageTrainManager not available");
                return;
            }

            if (days <= 0)
            {
                baggageManager.ClearBaggageDelay();
                ModLogger.Info(LogCategory, "Baggage delay cleared via event");
            }
            else
            {
                baggageManager.ApplyBaggageDelay(days);

                var notification = new TextObject("{=evt_baggage_delayed_msg}The baggage train is stuck. Access delayed by {DAYS} day(s).");
                notification.SetTextVariable("DAYS", days);
                InformationManager.DisplayMessage(new InformationMessage(notification.ToString(), Colors.Yellow));
            }
        }

        /// <summary>
        /// Removes a random number of items from the player's baggage stash.
        /// Gracefully handles empty stash (no crash, no message).
        /// Tracks raid events for Daily Brief display.
        /// </summary>
        private void ApplyRandomBaggageLoss(int count)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                ModLogger.Warn(LogCategory, "ApplyRandomBaggageLoss: EnlistmentBehavior not available");
                return;
            }

            var removedCount = enlistment.RemoveRandomBaggageItems(count);

            if (removedCount > 0)
            {
                var notification = new TextObject("{=evt_baggage_loss_msg}{COUNT} item(s) were lost from your baggage.");
                notification.SetTextVariable("COUNT", removedCount);
                InformationManager.DisplayMessage(new InformationMessage(notification.ToString(), Colors.Red));

                // Track raid for Daily Brief display (if this is from a raid event)
                var baggageManager = BaggageTrainManager.Instance;
                if (baggageManager != null)
                {
                    baggageManager.RecordBaggageRaid();
                }

                ModLogger.Info(LogCategory, $"ApplyRandomBaggageLoss: Removed {removedCount} items from baggage stash");
            }
            else
            {
                ModLogger.Debug(LogCategory, "ApplyRandomBaggageLoss: No items to remove (stash empty)");
            }
        }

        /// <summary>
        /// Handles the first-enlistment bag check choice.
        /// Delegates to EnlistmentBehavior.HandleBagCheckChoice() which processes stash/sell/smuggle actions.
        /// </summary>
        private void ApplyBagCheckChoice(string choice)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                ModLogger.Warn(LogCategory, "ApplyBagCheckChoice: EnlistmentBehavior not available");
                return;
            }

            ModLogger.Info(LogCategory, $"ApplyBagCheckChoice: Processing choice '{choice}'");
            enlistment.HandleBagCheckChoice(choice);
        }

        /// <summary>
        /// Aborts the enlistment process during bag check.
        /// Ends enlistment without normal discharge penalties and restores party to normal state.
        /// Lord reputation penalty should be applied via option effects.
        /// Records a 7-day cooldown with the lord so they won't accept the player immediately.
        /// </summary>
        private void ApplyEnlistmentAbort()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                ModLogger.Warn(LogCategory, "ApplyEnlistmentAbort: Not enlisted, nothing to abort");
                return;
            }

            var lord = enlistment.EnlistedLord;
            ModLogger.Info(LogCategory, $"ApplyEnlistmentAbort: Player aborting enlistment with {lord?.Name} during bag check");

            // Record the abort before ending enlistment (so lord reference is still available)
            if (lord != null)
            {
                enlistment.RecordBagCheckAbort(lord);
            }

            enlistment.StopEnlist("Aborted enlistment during bag check", isHonorableDischarge: false);
        }

        /// <summary>
        /// Applies an illness to the player based on the specified severity.
        /// Used by illness onset events and medical risk escalation.
        /// Context-aware: applies ship_fever/scurvy at sea, camp_fever/flux on land.
        /// </summary>
        private void ApplyIllnessOnset(string severityStr)
        {
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                ModLogger.Debug(LogCategory, "ApplyIllnessOnset: Condition system disabled");
                return;
            }

            // Parse severity (support both "mild" and "minor" for flexibility)
            var severity = severityStr.ToLowerInvariant() switch
            {
                "mild" or "minor" => IllnessSeverity.Mild,
                "moderate" => IllnessSeverity.Moderate,
                "severe" => IllnessSeverity.Severe,
                "critical" => IllnessSeverity.Critical,
                _ => IllnessSeverity.None
            };

            if (severity == IllnessSeverity.None)
            {
                ModLogger.Warn(LogCategory, $"ApplyIllnessOnset: Invalid severity '{severityStr}'");
                return;
            }

            // Select illness type based on travel context (maritime vs land)
            var party = EnlistmentBehavior.Instance?.CurrentLord?.PartyBelongedTo;
            // BUGFIX: If party is in a settlement or besieging, they are on land regardless of IsCurrentlyAtSea
            var isAtSea = party != null && 
                          party.CurrentSettlement == null && 
                          party.BesiegedSettlement == null && 
                          party.IsCurrentlyAtSea;
            string illnessType;
            
            if (isAtSea)
            {
                // Maritime illnesses: ship_fever (more common) or scurvy (prolonged voyages)
                // Scurvy appears at severe+ levels, ship_fever otherwise
                illnessType = severity >= IllnessSeverity.Severe ? "scurvy" : "ship_fever";
            }
            else
            {
                // Land illnesses: camp_fever (more common) or flux (camp conditions)
                // Flux appears at severe+ levels, camp_fever otherwise
                illnessType = severity >= IllnessSeverity.Severe ? "flux" : "camp_fever";
            }

            var baseDays = conditions.GetBaseRecoveryDaysForIllness(illnessType, severity);
            conditions.TryApplyIllness(illnessType, severity, baseDays, "event");
            
            var contextStr = isAtSea ? "at sea" : "on land";
            ModLogger.Info(LogCategory, $"ApplyIllnessOnset: Applied {severity} {illnessType} ({baseDays} days) [{contextStr}]");
        }

        /// <summary>
        /// Applies an injury to the player based on the specified severity.
        /// Used by injury events and combat outcomes.
        /// </summary>
        private void ApplyInjuryOnset(string severityStr)
        {
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                ModLogger.Debug(LogCategory, "ApplyInjuryOnset: Condition system disabled");
                return;
            }

            var severity = severityStr.ToLowerInvariant() switch
            {
                "minor" => InjurySeverity.Minor,
                "moderate" => InjurySeverity.Moderate,
                "severe" => InjurySeverity.Severe,
                "critical" => InjurySeverity.Critical,
                _ => InjurySeverity.None
            };

            if (severity == InjurySeverity.None)
            {
                ModLogger.Warn(LogCategory, $"ApplyInjuryOnset: Invalid severity '{severityStr}'");
                return;
            }

            // Default to blade_cut for now (can be made configurable later)
            var injuryType = "blade_cut";
            var baseDays = conditions.GetBaseRecoveryDaysForInjury(injuryType, severity);

            conditions.TryApplyInjury(injuryType, severity, baseDays, "event");
            ModLogger.Info(LogCategory, $"ApplyInjuryOnset: Applied {severity} {injuryType} ({baseDays} days)");
        }

        /// <summary>
        /// Begins medical treatment for the player's current condition.
        /// Marks player as under medical care and sets recovery rate modifier.
        /// </summary>
        private void ApplyBeginTreatment()
        {
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                ModLogger.Debug(LogCategory, "ApplyBeginTreatment: Condition system disabled");
                return;
            }

            if (!conditions.State.HasAnyCondition)
            {
                ModLogger.Debug(LogCategory, "ApplyBeginTreatment: No condition to treat");
                return;
            }

            // Use default treatment multiplier from config (1.5x recovery speed)
            var config = Enlisted.Mod.Core.Config.ConfigurationManager.LoadPlayerConditionsConfig();
            var multiplier = config?.BasicTreatmentMultiplier ?? 1.5f;

            conditions.ApplyTreatment(multiplier, "surgeon_treatment");
            ModLogger.Info(LogCategory, $"ApplyBeginTreatment: Started treatment (recovery {multiplier}x)");
        }

        /// <summary>
        /// Worsens the player's current condition by one severity level.
        /// Used by untreated condition events and failure outcomes.
        /// </summary>
        private void ApplyWorsenCondition()
        {
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                ModLogger.Debug(LogCategory, "ApplyWorsenCondition: Condition system disabled");
                return;
            }

            var state = conditions.State;
            if (!state.HasAnyCondition)
            {
                ModLogger.Debug(LogCategory, "ApplyWorsenCondition: No condition to worsen");
                return;
            }

            // Worsen illness if present
            if (state.HasIllness && state.CurrentIllness < IllnessSeverity.Critical)
            {
                var newSeverity = state.CurrentIllness + 1;
                var illnessType = state.IllnessType;
                var baseDays = conditions.GetBaseRecoveryDaysForIllness(illnessType, newSeverity);

                conditions.TryApplyIllness(illnessType, newSeverity, baseDays, "condition_worsened");
                ModLogger.Info(LogCategory, $"ApplyWorsenCondition: Illness worsened to {newSeverity}");
            }
            // Otherwise worsen injury if present
            else if (state.HasInjury && state.CurrentInjury < InjurySeverity.Critical)
            {
                var newSeverity = state.CurrentInjury + 1;
                var injuryType = state.InjuryType;
                var baseDays = conditions.GetBaseRecoveryDaysForInjury(injuryType, newSeverity);

                conditions.TryApplyInjury(injuryType, newSeverity, baseDays, "condition_worsened");
                ModLogger.Info(LogCategory, $"ApplyWorsenCondition: Injury worsened to {newSeverity}");
            }
            else
            {
                ModLogger.Debug(LogCategory, "ApplyWorsenCondition: Condition already at maximum severity");
            }
        }

        /// <summary>
        /// Applies a promotion effect, advancing the player to the specified tier.
        /// This is called when an event option with promotes is selected (proving events).
        /// Triggers retinue grant for T7/T8/T9 promotions via SetTier().
        /// A targetTier of -1 means "promote to next tier" (resolved at runtime).
        /// </summary>
        private void ApplyPromotesEffect(int targetTier)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                ModLogger.Warn(LogCategory, $"Cannot apply promotion (tier {targetTier}) - not enlisted");
                return;
            }

            var currentTier = enlistment.EnlistmentTier;

            // Handle sentinel value -1: promote to next tier
            // This allows JSON to use "promotes": true without specifying explicit tier
            if (targetTier == -1)
            {
                targetTier = currentTier + 1;
                ModLogger.Debug(LogCategory, $"Resolved 'promotes: true' to target tier {targetTier}");
            }

            if (targetTier <= currentTier)
            {
                ModLogger.Warn(LogCategory, $"Cannot promote to tier {targetTier} - already at tier {currentTier}");
                return;
            }

            ModLogger.Info(LogCategory, $"Applying promotion from event: T{currentTier} to T{targetTier}");

            // SetTier handles retinue grant for T7/T8/T9 promotions
            enlistment.SetTier(targetTier);
            QuartermasterManager.Instance?.UpdateNewlyUnlockedItems();

            // Clear pending promotion flag in PromotionBehavior
            PromotionBehavior.Instance?.ClearPendingPromotion();

            // Trigger promotion notification with culture-specific rank and immersive text
            PromotionBehavior.Instance?.TriggerPromotionNotificationPublic(targetTier);

            ModLogger.Info(LogCategory, $"Promotion complete: now tier {targetTier}");
        }

        /// <summary>
        /// Applies a discharge effect, ending the player's enlistment with the specified band.
        /// This is called when an event option with triggers_discharge is selected.
        /// </summary>
        private void ApplyDischargeEffect(string dischargeBand)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                ModLogger.Warn(LogCategory, $"Cannot apply discharge ({dischargeBand}) - not enlisted");
                return;
            }

            var bandLower = dischargeBand.ToLowerInvariant();
            var isHonorable = bandLower == "honorable" || bandLower == "veteran";

            ModLogger.Info(LogCategory, $"Applying discharge from event: band={dischargeBand}, honorable={isHonorable}");

            // Record the discharge in the service record.
            // ServiceRecordManager.RecordReservist handles setting LastDischargeBand and ReenlistmentBlockedUntil.
            var lord = enlistment.EnlistedLord;
            var daysServed = enlistment.EnlistmentDate != CampaignTime.Zero
                ? (int)(CampaignTime.Now - enlistment.EnlistmentDate).ToDays
                : 0;
            ServiceRecordManager.Instance?.RecordReservist(
                bandLower,
                daysServed,
                enlistment.EnlistmentTier,
                enlistment.EnlistmentXP,
                lord);

            enlistment.StopEnlist($"Discharged: {dischargeBand}", isHonorable);
        }

        /// <summary>
        /// Applies troop loss by removing troops from the party roster.
        /// </summary>
        private void ApplyTroopLoss(MobileParty party, int count)
        {
            var roster = party.MemberRoster;
            var troopsRemoved = 0;

            // Remove from regular troops first
            for (var i = roster.Count - 1; i >= 0 && troopsRemoved < count; i--)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.Character != null && !element.Character.IsHero)
                {
                    var toRemove = Math.Min(element.Number, count - troopsRemoved);
                    roster.AddToCounts(element.Character, -toRemove, removeDepleted: true);
                    troopsRemoved += toRemove;
                }
            }
        }

        /// <summary>
        /// Applies troop wounded by moving troops from healthy to wounded.
        /// </summary>
        private void ApplyTroopWounded(MobileParty party, int count)
        {
            var roster = party.MemberRoster;
            var troopsWounded = 0;

            // Wound regular troops
            for (var i = roster.Count - 1; i >= 0 && troopsWounded < count; i--)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.Character != null && !element.Character.IsHero && element.Number > element.WoundedNumber)
                {
                    var healthyCount = element.Number - element.WoundedNumber;
                    var toWound = Math.Min(healthyCount, count - troopsWounded);
                    roster.AddToCounts(element.Character, 0, woundedCount: toWound, removeDepleted: true);
                    troopsWounded += toWound;
                }
            }
        }

        /// <summary>
        /// Applies food loss by removing food items from the party inventory.
        /// </summary>
        private void ApplyFoodLoss(MobileParty party, int amount)
        {
            var itemRoster = party.ItemRoster;
            var foodRemaining = amount;

            // Remove food items
            for (var i = itemRoster.Count - 1; i >= 0 && foodRemaining > 0; i--)
            {
                var element = itemRoster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item.IsFood)
                {
                    var toRemove = Math.Min(element.Amount, foodRemaining);
                    itemRoster.AddToCounts(element.EquipmentElement.Item, -toRemove);
                    foodRemaining -= toRemove;
                }
            }
        }

        /// <summary>
        /// Applies XP to T1-T3 troops in the lord's party (NCO training).
        /// Validates troops can gain XP before awarding.
        /// Reports training results to the news system.
        /// </summary>
        private void ApplyTroopXp(int xpAmount)
        {
            if (xpAmount <= 0)
            {
                ModLogger.Warn(LogCategory, "Invalid XP amount for troop training");
                return;
            }

            // Get lord's party (player is enlisted, not leading their own party)
            var enlistment = EnlistmentBehavior.Instance;
            var lord = enlistment?.EnlistedLord;
            var lordParty = lord?.PartyBelongedTo;

            if (lordParty == null || !lordParty.IsActive)
            {
                ModLogger.Warn(LogCategory, "Cannot train troops: lord party not available");
                return;
            }

            // Don't train if lord is captured
            if (lord.IsPrisoner)
            {
                ModLogger.Info(LogCategory, "Skipping training: lord is prisoner");
                return;
            }

            var roster = lordParty.MemberRoster;
            if (roster == null || roster.TotalManCount == 0)
            {
                ModLogger.Info(LogCategory, "No troops to train");
                return;
            }

            // Snapshot roster to avoid concurrent modification
            var troopsToTrain = new List<(CharacterObject Character, int Count)>();

            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.Character == null || element.Character.IsHero)
                {
                    continue;
                }

                // Only train T1-T3 troops
                if (element.Character.Tier < 1 || element.Character.Tier > 3)
                {
                    continue;
                }

                if (element.Number <= 0)
                {
                    continue;
                }

                troopsToTrain.Add((element.Character, element.Number));
            }

            if (troopsToTrain.Count == 0)
            {
                ModLogger.Info(LogCategory, "No eligible troops for training (all max tier or heroes)");
                return;
            }

            // Limit to 10 troop types per training session
            troopsToTrain = troopsToTrain.Take(10).ToList();

            int trainedTypes = 0;
            int totalTrained = 0;
            int promotionReady = 0;

            foreach (var (character, count) in troopsToTrain)
            {
                try
                {
                    // Use native check: can this troop actually gain XP?
                    if (!MobilePartyHelper.CanTroopGainXp(lordParty.Party, character, out int maxGainableXp))
                    {
                        // Troop at max tier, can't gain XP
                        continue;
                    }

                    // Cap total XP to prevent over-leveling (integer division could round to 0, so ensure minimum of 1 XP per troop)
                    int totalXpForTroop = Math.Min(xpAmount * count, maxGainableXp);

                    // Skip if no XP can be awarded (very close to max level)
                    if (totalXpForTroop <= 0)
                    {
                        continue;
                    }

                    roster.AddXpToTroop(character, totalXpForTroop);

                    trainedTypes++;
                    totalTrained += count;

                    // Check if troops might be ready for promotion after training (approximate)
                    var upgradeTarget = character.UpgradeTargets?.FirstOrDefault();
                    if (upgradeTarget != null)
                    {
                        // Get current XP and calculate if close to promotion threshold
                        int indexOfTroop = roster.FindIndexOfTroop(character);
                        if (indexOfTroop >= 0)
                        {
                            int currentXp = roster.GetElementXp(indexOfTroop);
                            int xpNeeded = Campaign.Current.Models.PartyTroopUpgradeModel
                                .GetXpCostForUpgrade(lordParty.Party, character, upgradeTarget);
                            int totalXpNeeded = xpNeeded * count;

                            // Consider ready if within 90% of promotion after this training
                            if (xpNeeded > 0 && currentXp >= totalXpNeeded * 0.9f)
                            {
                                promotionReady += count;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("EVENTDELIVERY", $"Failed to train {character.Name}", ex);
                }
            }

            if (trainedTypes > 0)
            {
                ModLogger.Info(LogCategory,
                    $"Trained {trainedTypes} troop types ({totalTrained} soldiers, +{xpAmount} XP each)");

                // Report to news system
                ReportTrainingToNews(totalTrained, promotionReady, xpAmount);
            }
        }

        /// <summary>
        /// Reports training results to the camp news system.
        /// </summary>
        private void ReportTrainingToNews(int soldiersTrained, int promotionReady, int xpGiven)
        {
            if (EnlistedNewsBehavior.Instance == null)
            {
                return;
            }

            string summary;
            if (promotionReady > 0)
            {
                summary = $"{soldiersTrained} soldiers trained (+{xpGiven} XP), {promotionReady} ready for promotion";
            }
            else
            {
                summary = $"{soldiersTrained} soldiers trained (+{xpGiven} XP)";
            }

            // Use generic EventOutcome for training results
            var outcome = new EventOutcomeRecord
            {
                EventId = "training_session",
                EventTitle = "Training Session",
                OptionChosen = "NCO Training",
                OutcomeSummary = summary,
                DayNumber = (int)CampaignTime.Now.ToDays,
                EffectsApplied = new Dictionary<string, int>
                {
                    { "Soldiers Trained", soldiersTrained },
                    { "XP Awarded", xpGiven },
                    { "Ready for Promotion", promotionReady }
                }
            };

            EnlistedNewsBehavior.Instance.AddEventOutcome(outcome);
            ModLogger.Debug(LogCategory, $"Reported training to news: {summary}");
        }

        /// <summary>
        /// Calculates the actual success chance for an option, including skill modifiers.
        /// Formula: actualChance = baseChance + ((playerSkill - skillBase) / 5)
        /// Result is clamped to 5-95% to prevent guaranteed outcomes.
        /// </summary>
        private static int CalculateSkillModifiedChance(EventOption option)
        {
            if (!option.RiskChance.HasValue)
            {
                return 100; // Non-risky option always succeeds
            }

            var baseChance = option.RiskChance.Value;

            // Apply skill modifier if a skill check is specified
            if (!string.IsNullOrEmpty(option.SkillCheck))
            {
                var hero = Hero.MainHero;
                if (hero != null)
                {
                    var skill = SkillCheckHelper.GetSkillByName(option.SkillCheck);
                    if (skill != null)
                    {
                        var playerSkillValue = hero.GetSkillValue(skill);
                        var skillBase = option.SkillBase > 0 ? option.SkillBase : 50;
                        
                        // Each 5 points above/below skill base adds/subtracts 1% chance
                        var skillModifier = (playerSkillValue - skillBase) / 5;
                        baseChance += skillModifier;
                    }
                }
            }

            // Clamp to 5-95% to prevent guaranteed outcomes
            return Math.Max(5, Math.Min(95, baseChance));
        }

        /// <summary>
        /// Generates a dynamic tooltip for an option using its template.
        /// Replaces placeholders: {CHANCE}, {SKILL}, {SKILL_NAME}.
        /// </summary>
        public static string GenerateDynamicTooltip(EventOption option)
        {
            if (string.IsNullOrEmpty(option.TooltipTemplate))
            {
                return option.Tooltip ?? string.Empty;
            }

            var tooltip = option.TooltipTemplate;
            var chance = CalculateSkillModifiedChance(option);
            
            tooltip = tooltip.Replace("{CHANCE}", chance.ToString());

            if (!string.IsNullOrEmpty(option.SkillCheck))
            {
                var skill = SkillCheckHelper.GetSkillByName(option.SkillCheck);
                var skillValue = 0;
                var skillName = option.SkillCheck;
                
                if (skill != null && Hero.MainHero != null)
                {
                    skillValue = Hero.MainHero.GetSkillValue(skill);
                    skillName = skill.Name?.ToString() ?? option.SkillCheck;
                }
                
                tooltip = tooltip.Replace("{SKILL}", skillValue.ToString());
                tooltip = tooltip.Replace("{SKILL_NAME}", skillName);
            }

            return tooltip;
        }

        /// <summary>
        /// Applies flag changes from an event option.
        /// Sets or clears flags in the escalation state.
        /// </summary>
        private void ApplyFlagChanges(EventOption option)
        {
            var state = EscalationManager.Instance?.State;
            if (state == null)
            {
                return;
            }

            // Set flags
            if (option.SetFlags != null && option.SetFlags.Count > 0)
            {
                foreach (var flag in option.SetFlags)
                {
                    state.SetFlag(flag, option.FlagDurationDays);
                    ModLogger.Info(LogCategory, $"Set flag: {flag} (duration: {option.FlagDurationDays} days)");
                }
            }

            // Clear flags
            if (option.ClearFlags != null && option.ClearFlags.Count > 0)
            {
                foreach (var flag in option.ClearFlags)
                {
                    state.ClearFlag(flag);
                    ModLogger.Info(LogCategory, $"Cleared flag: {flag}");
                }
            }
        }

        /// <summary>
        /// Sends decision outcome text to the combat log for immediate feedback.
        /// Only called for decisions (not regular events).
        /// </summary>
        /// <param name="option">The selected option.</param>
        /// <param name="showFailure">True if this was a risky option that failed.</param>
        private void SendDecisionOutcomeToCombatLog(EventOption option, bool showFailure)
        {
            try
            {
                // Get the appropriate result narrative
                var resultNarrative = showFailure
                    ? ResolveText(option.ResultTextFailureId, option.ResultTextFailureFallback)
                    : ResolveText(option.ResultTextId, option.ResultTextFallback);

                if (string.IsNullOrWhiteSpace(resultNarrative))
                {
                    return;
                }

                // Choose color based on outcome
                var color = showFailure
                    ? Color.FromUint(0xFFCCCC44) // Yellow for failures
                    : Color.FromUint(0xFF88CC88); // Light green for successes

                // Send to combat log
                InformationManager.DisplayMessage(new InformationMessage(resultNarrative, color));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EVENTDELIVERY", "Failed to send decision outcome to combat log", ex);
            }
        }

        /// <summary>
        /// Notifies the news system of an event outcome after an option is selected.
        /// Adds the outcome to Personal Feed with the narrative result text for display in Recent Activities.
        /// This replaces the popup system to reduce UI interruption.
        /// </summary>
        /// <param name="option">The selected option.</param>
        /// <param name="showFailure">True if this was a risky option that failed.</param>
        private void NotifyNewsOfEventOutcome(EventOption option, bool showFailure = false)
        {
            if (EnlistedNewsBehavior.Instance == null)
            {
                return;
            }

            if (_currentEvent == null)
            {
                return;
            }

            try
            {
                // Get the appropriate result narrative (failure text if risky and failed, otherwise normal result)
                var resultNarrative = showFailure
                    ? ResolveText(option.ResultTextFailureId, option.ResultTextFailureFallback)
                    : ResolveText(option.ResultTextId, option.ResultTextFallback);

                var outcome = new EventOutcomeRecord
                {
                    EventId = _currentEvent.Id,
                    EventTitle = ResolveText(_currentEvent.TitleId, _currentEvent.TitleFallback),
                    OptionChosen = ResolveText(option.TextId, option.TextFallback),
                    OutcomeSummary = BuildOutcomeSummary(option.Effects),
                    ResultNarrative = resultNarrative ?? string.Empty,
                    DayNumber = (int)CampaignTime.Now.ToDays,
                    EffectsApplied = BuildEffectsDictionary(option.Effects),
                    Severity = ParseSeverityFromEvent(_currentEvent)
                };

                EnlistedNewsBehavior.Instance.AddEventOutcome(outcome);
                ModLogger.Debug(LogCategory, $"Notified news of event outcome: {_currentEvent.Id}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EVENTDELIVERY", "Failed to notify news of event outcome", ex);
            }
        }

        /// <summary>
        /// Parses the severity level from an event definition's severity string.
        /// Converts string values like "urgent" or "positive" to numeric severity codes.
        /// </summary>
        /// <param name="evt">The event definition.</param>
        /// <returns>Severity level (0=Normal, 1=Positive, 2=Attention, 3=Urgent, 4=Critical).</returns>
        private static int ParseSeverityFromEvent(EventDefinition evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.Severity))
            {
                return 0; // Normal
            }

            return evt.Severity.ToLowerInvariant() switch
            {
                "positive" => 1,
                "attention" => 2,
                "urgent" => 3,
                "critical" => 4,
                _ => 0 // Default to Normal for unknown values
            };
        }

        /// <summary>
        /// Builds a human-readable summary of event effects for display in headlines.
        /// </summary>
        private static string BuildOutcomeSummary(EventEffects effects)
        {
            if (effects == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            // Gold
            if (effects.Gold.HasValue && effects.Gold.Value != 0)
            {
                parts.Add(effects.Gold.Value > 0
                    ? $"+{effects.Gold.Value} gold"
                    : $"{effects.Gold.Value} gold");
            }

            // Skill XP - show top 3 skills by XP amount with truncated names
            if (effects.SkillXp != null)
            {
                var positiveSkillXp = effects.SkillXp
                    .Where(kv => kv.Value > 0)
                    .OrderByDescending(kv => kv.Value)
                    .ToList();

                var topThree = positiveSkillXp.Take(3);
                foreach (var skillXp in topThree)
                {
                    var skillName = skillXp.Key.Length > 12
                        ? skillXp.Key.Substring(0, 10) + ".."
                        : skillXp.Key;
                    parts.Add($"+{skillXp.Value} {skillName} XP");
                }

                // Show remaining count if more than 3 skills gained
                var remaining = positiveSkillXp.Count - 3;
                if (remaining > 0)
                {
                    parts.Add($"+{remaining} more skills");
                }
            }

            // Dynamic skill XP - resolve to actual skill names for display
            if (effects.DynamicSkillXp != null && effects.DynamicSkillXp.Count > 0)
            {
                foreach (var dynamicXp in effects.DynamicSkillXp.Where(kv => kv.Value > 0))
                {
                    SkillObject targetSkill = null;
                    switch (dynamicXp.Key.ToLowerInvariant())
                    {
                        case "equipped_weapon":
                            targetSkill = WeaponSkillHelper.GetEquippedWeaponSkill(Hero.MainHero);
                            break;
                        case "weakest_combat":
                            targetSkill = WeaponSkillHelper.GetWeakestCombatSkill(Hero.MainHero);
                            break;
                    }

                    if (targetSkill != null)
                    {
                        var skillName = targetSkill.Name.ToString();
                        if (skillName.Length > 12)
                        {
                            skillName = skillName.Substring(0, 10) + "..";
                        }
                        parts.Add($"+{dynamicXp.Value} {skillName} XP");
                    }
                }
            }

            // Reputations
            if (effects.LordRep.HasValue && effects.LordRep.Value != 0)
            {
                parts.Add($"{effects.LordRep.Value:+#;-#;0} Lord Rep");
            }

            // Escalation
            if (effects.Scrutiny.HasValue && effects.Scrutiny.Value != 0)
            {
                parts.Add($"{effects.Scrutiny.Value:+#;-#;0} Scrutiny");
            }

            // HP
            if (effects.HpChange.HasValue && effects.HpChange.Value != 0)
            {
                parts.Add(effects.HpChange.Value > 0
                    ? $"+{effects.HpChange.Value} HP"
                    : $"{effects.HpChange.Value} HP");
            }

            // Renown
            if (effects.Renown.HasValue && effects.Renown.Value != 0)
            {
                parts.Add($"{effects.Renown.Value:+#;-#;0} Renown");
            }

            return parts.Count > 0 ? $"({string.Join(", ", parts)})" : string.Empty;
        }

        /// <summary>
        /// Builds a dictionary of effect values for storage and lookup.
        /// </summary>
        private static Dictionary<string, int> BuildEffectsDictionary(EventEffects effects)
        {
            var dict = new Dictionary<string, int>();

            if (effects == null)
            {
                return dict;
            }

            if (effects.Gold.HasValue && effects.Gold.Value != 0)
            {
                dict["Gold"] = effects.Gold.Value;
            }

            if (effects.LordRep.HasValue && effects.LordRep.Value != 0)
            {
                dict["LordRep"] = effects.LordRep.Value;
            }

            if (effects.Scrutiny.HasValue && effects.Scrutiny.Value != 0)
            {
                dict["Scrutiny"] = effects.Scrutiny.Value;
            }

            if (effects.SkillXp != null)
            {
                foreach (var skillXp in effects.SkillXp)
                {
                    dict[$"{skillXp.Key}XP"] = skillXp.Value;
                }
            }

            if (effects.DynamicSkillXp != null)
            {
                foreach (var dynamicXp in effects.DynamicSkillXp)
                {
                    // Resolve dynamic skill to actual skill name for display
                    SkillObject targetSkill = null;
                    switch (dynamicXp.Key.ToLowerInvariant())
                    {
                        case "equipped_weapon":
                            targetSkill = WeaponSkillHelper.GetEquippedWeaponSkill(Hero.MainHero);
                            break;
                        case "weakest_combat":
                            targetSkill = WeaponSkillHelper.GetWeakestCombatSkill(Hero.MainHero);
                            break;
                    }

                    if (targetSkill != null)
                    {
                        dict[$"{targetSkill.Name}XP"] = dynamicXp.Value;
                    }
                }
            }

            return dict;
        }

        /// <summary>
        /// Notifies the news system of a pending chain event for context display.
        /// Shows hints like "A comrade owes you money" in the Daily Brief.
        /// </summary>
        private void NotifyNewsOfPendingChainEvent(EventOption option)
        {
            if (EnlistedNewsBehavior.Instance == null)
            {
                return;
            }

            if (_currentEvent == null)
            {
                return;
            }

            // Only notify for delayed chain events
            if (string.IsNullOrEmpty(option.ChainsTo) || option.ChainDelayHours <= 0)
            {
                return;
            }

            try
            {
                var hint = BuildChainEventContextHint(option.ChainsTo);
                var delayDays = Math.Max(1, option.ChainDelayHours / 24);

                EnlistedNewsBehavior.Instance.AddPendingEvent(
                    _currentEvent.Id,
                    option.ChainsTo,
                    hint,
                    delayDays);

                ModLogger.Debug(LogCategory, $"Notified news of pending chain event: {option.ChainsTo} in {delayDays} days");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EVENTDELIVERY", "Failed to notify news of pending chain event", ex);
            }
        }

        /// <summary>
        /// Builds a context hint for a pending chain event based on its ID.
        /// </summary>
        private static string BuildChainEventContextHint(string chainEventId)
        {
            var id = chainEventId?.ToLowerInvariant() ?? string.Empty;

            // Loan/debt repayment
            if (id.Contains("repay") || id.Contains("return") || id.Contains("debt"))
            {
                return "A comrade owes you money.";
            }

            // Gratitude/favor
            if (id.Contains("gratitude") || id.Contains("thank") || id.Contains("favor"))
            {
                return "Someone remembers your kindness.";
            }

            // Revenge/grudge
            if (id.Contains("revenge") || id.Contains("grudge") || id.Contains("payback"))
            {
                return "Someone holds a grudge.";
            }

            // Training follow-up
            if (id.Contains("training") || id.Contains("practice"))
            {
                return "Your training regimen continues.";
            }

            // Investigation/scrutiny follow-up
            if (id.Contains("investigation") || id.Contains("scrutiny"))
            {
                return "Officers are watching closely.";
            }

            // Default
            return "Something is brewing.";
        }


        /// <summary>
        /// Shows the sub-choice popup for branching rewards.
        /// Filters options based on conditions and presents available choices.
        /// </summary>
        private void ShowSubChoicePopup(RewardChoices choices)
        {
            var options = new List<InquiryElement>();

            foreach (var subOption in choices.Options)
            {
                // Check condition if present - hide option if condition fails
                if (!CheckSubChoiceCondition(subOption.Condition))
                {
                    ModLogger.Debug(LogCategory, $"Sub-option '{subOption.Id}' hidden - condition '{subOption.Condition}' not met");
                    continue;
                }

                options.Add(new InquiryElement(
                    identifier: subOption,
                    title: subOption.Text,
                    imageIdentifier: null,
                    isEnabled: true,
                    hint: subOption.Tooltip ?? string.Empty
                ));
            }

            if (options.Count == 0)
            {
                ModLogger.Warn(LogCategory, $"Sub-choice popup '{choices.Type}' has no valid options after condition filtering");
                return;
            }

            // Format the title from the type (capitalize first letter)
            var titleText = !string.IsNullOrEmpty(choices.Type)
                ? char.ToUpper(choices.Type[0]) + choices.Type.Substring(1).Replace('_', ' ')
                : "Choose";

            var inquiry = new MultiSelectionInquiryData(
                titleText: titleText,
                descriptionText: choices.Prompt,
                inquiryElements: options,
                isExitShown: false,
                minSelectableOptionCount: 1,
                maxSelectableOptionCount: 1,
                affirmativeText: new TextObject("{=str_ok}Confirm").ToString(),
                negativeText: null,
                affirmativeAction: OnSubChoiceSelected,
                negativeAction: null
            );

            MBInformationManager.ShowMultiSelectionInquiry(inquiry, true);
        }

        /// <summary>
        /// Called when a sub-choice is selected from the reward choices popup.
        /// Applies costs, rewards, and effects from the selected sub-option.
        /// </summary>
        private void OnSubChoiceSelected(List<InquiryElement> selected)
        {
            if (selected == null || selected.Count == 0)
            {
                ModLogger.Warn(LogCategory, "No sub-choice selected");
                return;
            }

            var subOption = selected[0].Identifier as RewardChoiceOption;
            if (subOption == null)
            {
                ModLogger.Caught("EVENTDELIVERY", "Selected sub-choice identifier is not a RewardChoiceOption", null);
                return;
            }

            ModLogger.Info(LogCategory, $"Sub-choice selected: {subOption.Id}");

            // Apply costs first
            ApplyCosts(subOption.Costs);

            // Apply rewards
            ApplyRewards(subOption.Rewards);

            // Apply effects (reuse existing method)
            ApplyEffects(subOption.Effects);
        }

        /// <summary>
        /// Checks if a sub-choice condition is met.
        /// Supports formation:X, flag:X, and role:X conditions.
        /// </summary>
        private bool CheckSubChoiceCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition))
            {
                return true; // No condition means always available
            }

            // Check for formation: prefix (e.g., "formation:ranged")
            if (condition.StartsWith("formation:", StringComparison.OrdinalIgnoreCase))
            {
                var formation = condition.Substring(10).Trim();
                var playerRole = EnlistmentBehavior.Instance?.CurrentSpecialization ?? "Soldier";

                // Map formations to roles that match
                return formation.ToLowerInvariant() switch
                {
                    "ranged" => playerRole.Equals("Scout", StringComparison.OrdinalIgnoreCase) ||
                                playerRole.Equals("Archer", StringComparison.OrdinalIgnoreCase),
                    "infantry" => playerRole.Equals("Soldier", StringComparison.OrdinalIgnoreCase),
                    "cavalry" => playerRole.Equals("Soldier", StringComparison.OrdinalIgnoreCase) ||
                                 playerRole.Equals("Scout", StringComparison.OrdinalIgnoreCase),
                    _ => true // Unknown formation - allow by default
                };
            }

            // Check for role: prefix (e.g., "role:Scout")
            if (condition.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
            {
                var requiredRole = condition.Substring(5).Trim();
                var playerRole = EnlistmentBehavior.Instance?.CurrentSpecialization ?? "Soldier";
                return playerRole.Equals(requiredRole, StringComparison.OrdinalIgnoreCase);
            }

            // Check for flag: prefix (e.g., "flag:has_bow")
            if (condition.StartsWith("flag:", StringComparison.OrdinalIgnoreCase))
            {
                var flagName = condition.Substring(5).Trim();
                return EscalationManager.Instance?.State?.HasFlag(flagName) ?? false;
            }

            // Check for has_flag: prefix
            if (condition.StartsWith("has_flag:", StringComparison.OrdinalIgnoreCase))
            {
                var flagName = condition.Substring(9).Trim();
                return EscalationManager.Instance?.State?.HasFlag(flagName) ?? false;
            }

            // Check for has_weapon_equipped condition
            if (condition.Equals("has_weapon_equipped", StringComparison.OrdinalIgnoreCase))
            {
                return WeaponSkillHelper.HasWeaponEquipped(Hero.MainHero);
            }

            // Unknown condition type - log and allow
            ModLogger.Debug(LogCategory, $"Unknown sub-choice condition type: {condition}");
            return true;
        }

        /// <summary>
        /// Applies costs from a sub-choice option.
        /// Deducts gold, time, etc.
        /// </summary>
        private void ApplyCosts(EventCosts costs)
        {
            if (costs == null)
            {
                return;
            }

            var hero = Hero.MainHero;

            // Apply gold cost
            if (costs.Gold.HasValue && costs.Gold.Value > 0)
            {
                GiveGoldAction.ApplyBetweenCharacters(hero, null, costs.Gold.Value);
                ModLogger.Debug(LogCategory, $"Applied gold cost: -{costs.Gold.Value}");
            }

            // Time costs could affect scheduling, but for now just log
            if (costs.TimeHours.HasValue && costs.TimeHours.Value > 0)
            {
                ModLogger.Debug(LogCategory, $"Time cost noted: {costs.TimeHours.Value} hours (not yet implemented)");
            }
        }

        /// <summary>
        /// Applies rewards from a sub-choice option.
        /// Grants gold, skill XP, etc.
        /// </summary>
        private void ApplyRewards(EventRewards rewards)
        {
            if (rewards == null)
            {
                return;
            }

            var hero = Hero.MainHero;
            var rewardMessages = new List<string>();

            // Apply gold reward
            if (rewards.Gold.HasValue && rewards.Gold.Value > 0)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, hero, rewards.Gold.Value);
                rewardMessages.Add($"+{rewards.Gold.Value} gold");
                ModLogger.Debug(LogCategory, $"Applied gold reward: +{rewards.Gold.Value}");
            }

            // Apply skill XP rewards with experience track modifier and track for enlistment XP
            int totalSkillXpAwarded = 0;
            if (rewards.SkillXp != null && rewards.SkillXp.Count > 0)
            {
                // Get training XP modifier based on hero's experience level
                // Green recruits get +20%, seasoned soldiers get normal XP, veterans get -10%
                float xpModifier = ExperienceTrackHelper.GetTrainingXpModifier(hero);

                foreach (var skillXp in rewards.SkillXp)
                {
                    var skill = SkillCheckHelper.GetSkillByName(skillXp.Key);
                    if (skill != null)
                    {
                        // Apply experience track modifier to training XP
                        int baseXp = skillXp.Value;
                        int modifiedXp = (int)Math.Round(baseXp * xpModifier);

                        // Ensure minimum 1 XP if base XP was positive to prevent rounding to zero
                        if (baseXp > 0 && modifiedXp < 1)
                        {
                            modifiedXp = 1;
                        }

                        hero.AddSkillXp(skill, modifiedXp);
                        totalSkillXpAwarded += modifiedXp;
                        rewardMessages.Add($"+{modifiedXp} {skillXp.Key} XP");

                        // Log with modifier details when modifier is not neutral
                        if (Math.Abs(xpModifier - 1.0f) > 0.001f)
                        {
                            ModLogger.Debug(LogCategory, $"Applied skill XP reward: +{modifiedXp} {skillXp.Key} (base {baseXp} × {xpModifier:F2})");
                        }
                        else
                        {
                            ModLogger.Debug(LogCategory, $"Applied skill XP reward: +{modifiedXp} {skillXp.Key}");
                        }
                    }
                    else
                    {
                        ModLogger.Warn(LogCategory, $"Unknown skill in reward: {skillXp.Key}");
                    }
                }
            }

            // Apply dynamic skill XP rewards
            if (rewards.DynamicSkillXp != null && rewards.DynamicSkillXp.Count > 0)
            {
                foreach (var dynamicXp in rewards.DynamicSkillXp)
                {
                    // Validate XP value
                    if (dynamicXp.Value <= 0)
                    {
                        ModLogger.Warn(LogCategory, $"Invalid dynamic XP value for {dynamicXp.Key}: {dynamicXp.Value}");
                        continue;
                    }

                    SkillObject targetSkill;

                    switch (dynamicXp.Key.ToLowerInvariant())
                    {
                        case "equipped_weapon":
                            targetSkill = WeaponSkillHelper.GetEquippedWeaponSkill(hero);
                            break;
                        case "weakest_combat":
                            targetSkill = WeaponSkillHelper.GetWeakestCombatSkill(hero);
                            break;
                        default:
                            ModLogger.Warn(LogCategory, $"Unknown dynamic XP key: {dynamicXp.Key}");
                            continue;
                    }

                    if (targetSkill != null)
                    {
                        hero.AddSkillXp(targetSkill, dynamicXp.Value);
                        totalSkillXpAwarded += dynamicXp.Value;
                        rewardMessages.Add($"+{dynamicXp.Value} {targetSkill.Name} XP");
                        ModLogger.Debug(LogCategory,
                            $"Applied dynamic {dynamicXp.Key} XP: +{dynamicXp.Value} {targetSkill.Name}");
                    }
                }
            }

            // Award enlistment XP based on skill XP earned (for rank progression in muster reports)
            if (totalSkillXpAwarded > 0)
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment is { IsEnlisted: true })
                {
                    enlistment.AddEnlistmentXP(totalSkillXpAwarded, "Reward");
                    ModLogger.Debug(LogCategory, $"Awarded {totalSkillXpAwarded} enlistment XP from rewards");
                }
            }

            // Log general XP (for future tracking systems)
            if (rewards.Xp != null && rewards.Xp.Count > 0)
            {
                foreach (var xp in rewards.Xp)
                {
                    rewardMessages.Add($"+{xp.Value} {xp.Key} XP");
                    ModLogger.Debug(LogCategory, $"General XP reward logged: +{xp.Value} {xp.Key} (tracking not yet implemented)");
                }
            }

            // Display rewards to player if any were applied
            if (rewardMessages.Count > 0)
            {
                var message = string.Join(", ", rewardMessages);
                InformationManager.DisplayMessage(new InformationMessage(message, Colors.Cyan));
            }
        }

        /// <summary>
        /// Called when an event is closed (after result shown or when no options available).
        /// </summary>
        private void OnEventClosed()
        {
            // Check if this was a decision event before clearing the current event
            var wasDecision = _currentEvent != null &&
                             _currentEvent.Category != null &&
                             _currentEvent.Category.Equals("decision", StringComparison.OrdinalIgnoreCase);

            _isShowingEvent = false;
            _currentEvent = null;

            // If this was a decision, clear the "currently showing" mark and refresh the menu
            if (wasDecision)
            {
                DecisionManager.Instance?.ClearCurrentlyShowingDecision();
                RequestDecisionsMenuRefresh();
            }

            // Try to deliver next event in queue
            if (_pendingEvents.Count > 0)
            {
                TryDeliverNextEvent();
            }
        }

        /// <summary>
        /// Requests that the decisions menu be refreshed to show updated cooldowns.
        /// This is called after a decision event completes.
        /// </summary>
        private void RequestDecisionsMenuRefresh()
        {
            try
            {
                // Refresh whichever menu the player is currently on
                // This forces the menu to rebuild, showing updated cooldowns and removing completed decisions
                var currentMenuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
                
                ModLogger.Info(LogCategory, $"Requesting decision menu refresh (current menu: {currentMenuId})");
                
                if (currentMenuId != null && currentMenuId.StartsWith("enlisted_"))
                {
                    // Force a refresh by switching to the same menu on next frame
                    // This rebuilds the cached entries and updates availability states
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        try
                        {
                            ModLogger.Info(LogCategory, $"Refreshing menu (next frame): {currentMenuId}");
                            GameMenu.SwitchToMenu(currentMenuId);
                            ModLogger.Debug(LogCategory, $"Menu refresh completed: {currentMenuId}");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Caught("EVENTDELIVERY", $"Error refreshing {currentMenuId} after decision", ex);
                        }
                    });
                }
                else
                {
                    ModLogger.Debug(LogCategory, $"Menu refresh skipped - not on enlisted menu (current: {currentMenuId})");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EVENTDELIVERY", "Error requesting decisions menu refresh", ex);
            }
        }

        /// <summary>
        /// Resolves a text ID to localized text from the XML strings file.
        /// Uses Bannerlord's TextObject system with the format {=stringId}Fallback.
        /// If the string is not found in XML, uses the provided inline fallback text.
        /// </summary>
        /// <param name="textId">The XML string ID to look up.</param>
        /// <param name="fallbackText">The inline fallback text from JSON if XML lookup fails.</param>
        private string ResolveText(string textId, string fallbackText = null)
        {
            // Determine the best fallback: use inline text if available, otherwise use the ID
            var effectiveFallback = !string.IsNullOrEmpty(fallbackText) ? fallbackText : textId;

            if (string.IsNullOrEmpty(textId))
            {
                // No XML ID provided, just return the fallback
                return effectiveFallback ?? string.Empty;
            }

            // Use TextObject to look up the string from XML
            // Format: "{=stringId}Fallback" - if string not found, uses the fallback text
            var textObject = new TextObject($"{{={textId}}}{effectiveFallback}");

            // Set NCO and soldier name text variables for personalized event dialogue
            SetEventTextVariables(textObject);

            var resolved = textObject.ToString();

            // If resolution returned the raw {=...} pattern, the lookup failed - use fallback with variable substitution
            if (resolved.StartsWith("{="))
            {
                ModLogger.Debug(LogCategory, $"XML lookup failed for '{textId}', using fallback");
                // Create new TextObject from fallback and substitute variables
                var fallbackTextObject = new TextObject(effectiveFallback ?? string.Empty);
                SetEventTextVariables(fallbackTextObject);
                return fallbackTextObject.ToString();
            }

            return resolved;
        }

        /// <summary>
        /// Sets text variables for event personalization (NCO names, soldier names, etc.).
        /// Called before resolving event text to populate placeholders like {SERGEANT}, {COMRADE_NAME}.
        /// </summary>
        private void SetEventTextVariables(TextObject textObject)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    return;
                }

                // NCO/Sergeant names
                var ncoFullName = enlistment.NcoFullName ?? "the Sergeant";
                var ncoRank = enlistment.NcoRank ?? "Sergeant";
                textObject.SetTextVariable("SERGEANT", ncoFullName);
                textObject.SetTextVariable("SERGEANT_NAME", ncoFullName);
                textObject.SetTextVariable("NCO_NAME", ncoFullName);
                textObject.SetTextVariable("NCO_RANK", ncoRank);

                // Soldier names for multi-character events
                var soldierName = enlistment.GetRandomSoldierName();
                var veteran1Name = enlistment.GetRandomSoldierName();
                var veteran2Name = enlistment.GetRandomSoldierName();
                var recruitName = enlistment.GetRandomSoldierName();
                textObject.SetTextVariable("COMRADE_NAME", soldierName);
                textObject.SetTextVariable("SOLDIER_NAME", soldierName);
                textObject.SetTextVariable("VETERAN_1_NAME", veteran1Name);
                textObject.SetTextVariable("VETERAN_2_NAME", veteran2Name);
                textObject.SetTextVariable("RECRUIT_NAME", recruitName);
                textObject.SetTextVariable("SECOND_SHORT", veteran1Name);

                // Officer names
                textObject.SetTextVariable("OFFICER_NAME", ncoFullName);
                textObject.SetTextVariable("CAPTAIN_NAME", "the Captain");

                // Naval crew names (for War Sails DLC events)
                textObject.SetTextVariable("BOATSWAIN_NAME", "the Boatswain");
                textObject.SetTextVariable("NAVIGATOR_NAME", "the Navigator");
                textObject.SetTextVariable("FIELD_MEDIC_NAME", "the Field Medic");

                // Company/Party info
                var companyName = enlistment.EnlistedLord?.PartyBelongedTo?.Name?.ToString() ?? "the company";
                textObject.SetTextVariable("COMPANY_NAME", companyName);

                // Player info
                if (Hero.MainHero != null)
                {
                    textObject.SetTextVariable("PLAYER_NAME", Hero.MainHero.FirstName?.ToString() ?? "Soldier");
                    textObject.SetTextVariable("PLAYER_RANK", RankHelper.GetCurrentRank(enlistment));
                }

                // Lord info
                if (enlistment.EnlistedLord != null)
                {
                    var lord = enlistment.EnlistedLord;
                    textObject.SetTextVariable("LORD_NAME", lord.Name?.ToString() ?? "the Lord");
                    textObject.SetTextVariable("LORD_TITLE", lord.IsFemale ? "Lady" : "Lord");

                    // Faction and kingdom names
                    if (lord.MapFaction != null)
                    {
                        textObject.SetTextVariable("FACTION_NAME", lord.MapFaction.Name?.ToString() ?? "the faction");

                        if (lord.MapFaction.IsKingdomFaction && lord.MapFaction is Kingdom kingdom)
                        {
                            textObject.SetTextVariable("KINGDOM_NAME", kingdom.Name?.ToString() ?? "the kingdom");
                        }
                        else
                        {
                            textObject.SetTextVariable("KINGDOM_NAME", lord.MapFaction.Name?.ToString() ?? "the realm");
                        }
                    }
                }

                // Rank progression variables
                var currentRank = RankHelper.GetCurrentRank(enlistment);
                textObject.SetTextVariable("NEXT_RANK", "the next rank");
                textObject.SetTextVariable("SECOND_RANK", currentRank);

                // Location variables
                var party = enlistment.EnlistedLord?.PartyBelongedTo;
                if (party?.CurrentSettlement != null)
                {
                    textObject.SetTextVariable("SETTLEMENT_NAME", party.CurrentSettlement.Name?.ToString() ?? "the settlement");
                }
                else
                {
                    textObject.SetTextVariable("SETTLEMENT_NAME", "the settlement");
                }

                // Previous lord (for transfer events)
                textObject.SetTextVariable("PREVIOUS_LORD", "your previous lord");
                textObject.SetTextVariable("ALLIED_LORD", "an allied lord");

                // Enemy faction info
                textObject.SetTextVariable("ENEMY_FACTION_ADJECTIVE", "enemy");

                // Medical event variables
                textObject.SetTextVariable("CONDITION_TYPE", "illness");
                textObject.SetTextVariable("CONDITION_LOCATION", "your arm");
                textObject.SetTextVariable("COMPLICATION_NAME", "infection");
                textObject.SetTextVariable("REMEDY_NAME", "medicine");

                // Naval event variables (for War Sails DLC)
                textObject.SetTextVariable("SHIP_NAME", companyName);
                textObject.SetTextVariable("DESTINATION_PORT", "port");
                textObject.SetTextVariable("DAYS_AT_SEA", "7");

                // Troop count for command events
                if (party != null)
                {
                    textObject.SetTextVariable("TROOP_COUNT", party.MemberRoster.TotalManCount.ToString());
                }
                else
                {
                    textObject.SetTextVariable("TROOP_COUNT", "20");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug(LogCategory, $"Failed to set event text variables: {ex.Message}");
            }
        }
    }
}

