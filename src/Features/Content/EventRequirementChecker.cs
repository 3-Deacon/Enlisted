using System;
using Enlisted.Features.Conditions;
using Enlisted.Features.Context;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Identity;
using Enlisted.Features.Retinue.Core;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Validates whether the player meets the requirements for an event to fire.
    /// Checks tier, role, context, skills, traits, and escalation thresholds.
    /// Used by EventSelector to filter eligible events before weighted selection.
    /// </summary>
    public static class EventRequirementChecker
    {
        private const string LogCategory = "EventRequirements";

        /// <summary>
        /// Checks if the player meets all requirements for an event.
        /// </summary>
        /// <param name="requirements">The event's requirements to check against.</param>
        /// <returns>True if all requirements are met, false otherwise.</returns>
        public static bool MeetsRequirements(EventRequirements requirements)
        {
            if (requirements == null)
            {
                return true; // No requirements means always eligible
            }

            try
            {
                // Check tier requirements
                if (!MeetsTierRequirement(requirements))
                {
                    return false;
                }

                // Check role requirement
                if (!MeetsRoleRequirement(requirements))
                {
                    return false;
                }

                // Check context requirement
                if (!MeetsContextRequirement(requirements))
                {
                    return false;
                }

                // Check skill requirements
                if (!MeetsSkillRequirements(requirements))
                {
                    return false;
                }

                // Check trait requirements
                if (!MeetsTraitRequirements(requirements))
                {
                    return false;
                }

                // Check escalation requirements
                if (!MeetsEscalationRequirements(requirements))
                {
                    return false;
                }
                
                // Check HP requirements (for decisions like Seek Treatment)
                if (!MeetsHpRequirement(requirements))
                {
                    return false;
                }
                
                // Check baggage has items (for theft events)
                if (!MeetsBaggageItemsRequirement(requirements))
                {
                    return false;
                }
                
                // Check not at sea (for land-based events like baggage wagons)
                if (!MeetsNotAtSeaRequirement(requirements))
                {
                    return false;
                }
                
                // Check at sea (for maritime events like ship's hold)
                if (!MeetsAtSeaRequirement(requirements))
                {
                    return false;
                }
                
                // Check hasAnyCondition (for medical decisions that require being injured/ill)
                if (!MeetsHasAnyConditionRequirement(requirements))
                {
                    return false;
                }
                
                // Check hasSevereCondition (for emergency medical decisions)
                if (!MeetsHasSevereConditionRequirement(requirements))
                {
                    return false;
                }
                
                // Check maxIllness (blocks training/labor when too ill)
                if (!MeetsMaxIllnessRequirement(requirements))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Error checking event requirements", ex);
                return false;
            }
        }

        /// <summary>
        /// Checks if player's tier is within the required range.
        /// </summary>
        private static bool MeetsTierRequirement(EventRequirements requirements)
        {
            var playerTier = GetPlayerTier();

            if (requirements.MinTier.HasValue && playerTier < requirements.MinTier.Value)
            {
                return false;
            }

            if (requirements.MaxTier.HasValue && playerTier > requirements.MaxTier.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if player's primary role matches the required role.
        /// "Any" means no restriction.
        /// </summary>
        private static bool MeetsRoleRequirement(EventRequirements requirements)
        {
            if (string.IsNullOrEmpty(requirements.Role) ||
                requirements.Role.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var playerRole = GetPlayerRole();
            return playerRole.Equals(requirements.Role, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if current campaign context matches the required context.
        /// "Any" means no restriction.
        /// </summary>
        private static bool MeetsContextRequirement(EventRequirements requirements)
        {
            if (string.IsNullOrEmpty(requirements.Context) ||
                requirements.Context.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var currentContext = GetCurrentContext();

            // Handle both simple context names and strategic context mapping
            return ContextMatches(currentContext, requirements.Context);
        }

        /// <summary>
        /// Checks if player meets all minimum skill requirements.
        /// </summary>
        private static bool MeetsSkillRequirements(EventRequirements requirements)
        {
            if (requirements.MinSkills == null || requirements.MinSkills.Count == 0)
            {
                return true;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return false;
            }

            foreach (var skillReq in requirements.MinSkills)
            {
                var skill = SkillCheckHelper.GetSkillByName(skillReq.Key);
                if (skill == null)
                {
                    continue; // Skip unknown skills
                }

                if (!SkillCheckHelper.MeetsSkillThreshold(hero, skill, skillReq.Value))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if player meets all minimum trait requirements.
        /// </summary>
        private static bool MeetsTraitRequirements(EventRequirements requirements)
        {
            if (requirements.MinTraits == null || requirements.MinTraits.Count == 0)
            {
                return true;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return false;
            }

            foreach (var traitReq in requirements.MinTraits)
            {
                var trait = SkillCheckHelper.GetTraitByName(traitReq.Key);
                if (trait == null)
                {
                    continue; // Skip unknown traits
                }

                if (!SkillCheckHelper.MeetsTraitThreshold(hero, trait, traitReq.Value))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if escalation tracks meet minimum thresholds.
        /// </summary>
        private static bool MeetsEscalationRequirements(EventRequirements requirements)
        {
            if (requirements.MinEscalation == null || requirements.MinEscalation.Count == 0)
            {
                return true;
            }

            var escalation = EscalationManager.Instance?.State;
            if (escalation == null)
            {
                return false;
            }

            foreach (var escReq in requirements.MinEscalation)
            {
                var trackValue = GetEscalationTrackValue(escalation, escReq.Key);
                if (trackValue < escReq.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the value of an escalation track by name.
        /// </summary>
        private static int GetEscalationTrackValue(EscalationState state, string trackName)
        {
            return trackName.ToLowerInvariant() switch
            {
                "scrutiny" => state.Scrutiny,
                "lordreputation" or "lord_reputation" or "lordrep" => state.LordReputation,
                "medicalrisk" or "medical_risk" => state.MedicalRisk,
                "pay_tension" or "paytension" or "pay_tension_min" => 0, // TODO: Add PayTension property to EscalationState if needed
                _ => 0
            };
        }
        

        /// <summary>
        /// Checks if the player's HP is below the required threshold.
        /// Used for decisions like Seek Treatment which only appear when wounded.
        /// </summary>
        private static bool MeetsHpRequirement(EventRequirements requirements)
        {
            if (!requirements.HpBelow.HasValue)
            {
                return true; // No HP requirement
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return false;
            }

            var maxHp = hero.CharacterObject.MaxHitPoints();
            if (maxHp <= 0)
            {
                return false;
            }

            var hpPercent = (hero.HitPoints * 100) / maxHp;
            return hpPercent < requirements.HpBelow.Value;
        }
        
        /// <summary>
        /// Checks if the player has at least one item in their baggage stash.
        /// Used for theft events that require items to exist.
        /// </summary>
        private static bool MeetsBaggageItemsRequirement(EventRequirements requirements)
        {
            if (requirements.BaggageHasItems != true)
            {
                return true; // No baggage requirement
            }
            
            var enlistment = EnlistmentBehavior.Instance;
            return enlistment != null && enlistment.HasBaggageItems();
        }

        /// <summary>
        /// Checks if the party is NOT at sea (on land).
        /// Used for land-based events like baggage wagons that don't make sense during sea travel.
        /// </summary>
        private static bool MeetsNotAtSeaRequirement(EventRequirements requirements)
        {
            if (requirements.NotAtSea != true)
            {
                return true; // No sea restriction
            }
            
            // Check if party is at sea - if so, fail the requirement
            return !CheckAtSea();
        }

        /// <summary>
        /// Checks if the party IS at sea (sailing).
        /// Used for maritime events like ship's hold access that only make sense during sea travel.
        /// </summary>
        private static bool MeetsAtSeaRequirement(EventRequirements requirements)
        {
            if (requirements.AtSea != true)
            {
                return true; // No maritime requirement
            }
            
            // Check if party is at sea - if not, fail the requirement
            return CheckAtSea();
        }

        /// <summary>
        /// Checks if the player has any condition (injury, illness, or exhaustion).
        /// Used by medical decisions that only appear when the player needs treatment.
        /// </summary>
        private static bool MeetsHasAnyConditionRequirement(EventRequirements requirements)
        {
            if (requirements.HasAnyCondition != true)
            {
                return true; // No condition requirement
            }
            
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                return false; // Condition system disabled
            }
            
            return conditions.State?.HasAnyCondition == true;
        }

        /// <summary>
        /// Checks if the player has a severe or critical condition.
        /// Used by emergency medical decisions that only appear for urgent cases.
        /// </summary>
        private static bool MeetsHasSevereConditionRequirement(EventRequirements requirements)
        {
            if (requirements.HasSevereCondition != true)
            {
                return true; // No severe condition requirement
            }
            
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                return false;
            }
            
            var state = conditions.State;
            if (state == null)
            {
                return false;
            }
            
            // Check for severe or critical injury/illness that is ACTIVE (days remaining > 0).
            // This matches the definition of HasInjury/HasIllness used in status display.
            var hasSevereInjury = state.CurrentInjury >= InjurySeverity.Severe && state.InjuryDaysRemaining > 0;
            var hasSevereIllness = state.CurrentIllness >= IllnessSeverity.Severe && state.IllnessDaysRemaining > 0;
            
            return hasSevereInjury || hasSevereIllness;
        }

        /// <summary>
        /// Checks if the player's illness severity is at or below the maximum allowed.
        /// Used by training/labor decisions to block strenuous activity when too ill.
        /// Valid maxIllness values: "None", "Mild", "Moderate", "Severe" (null = no restriction)
        /// </summary>
        private static bool MeetsMaxIllnessRequirement(EventRequirements requirements)
        {
            if (string.IsNullOrEmpty(requirements.MaxIllness))
            {
                return true; // No illness restriction
            }
            
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                return true; // Condition system disabled, allow the event
            }
            
            var currentIllness = conditions.State?.CurrentIllness ?? IllnessSeverity.None;
            
            // Parse the max allowed severity
            var maxAllowed = requirements.MaxIllness.ToLowerInvariant() switch
            {
                "none" => IllnessSeverity.None,
                "mild" => IllnessSeverity.Mild,
                "moderate" => IllnessSeverity.Moderate,
                "severe" => IllnessSeverity.Severe,
                _ => IllnessSeverity.Critical // Unknown value = allow all
            };
            
            return currentIllness <= maxAllowed;
        }

        /// <summary>
        /// Gets the player's current enlistment tier.
        /// </summary>
        public static int GetPlayerTier()
        {
            return EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
        }

        /// <summary>
        /// Gets the player's primary role based on trait levels.
        /// </summary>
        public static string GetPlayerRole()
        {
            return EnlistedStatusManager.Instance?.GetPrimaryRole() ?? "Soldier";
        }

        /// <summary>
        /// Gets the current strategic context for the lord's party.
        /// Returns a simplified context for event matching.
        /// </summary>
        public static string GetCurrentContext()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.CurrentLord?.PartyBelongedTo == null)
            {
                return "Peace";
            }

            var party = enlistment.CurrentLord.PartyBelongedTo;
            var strategicContext = ArmyContextAnalyzer.GetLordStrategicContext(party);

            // Map strategic context to simplified event context
            return MapStrategicToEventContext(strategicContext);
        }

        /// <summary>
        /// Maps detailed strategic context to simplified event context categories.
        /// </summary>
        private static string MapStrategicToEventContext(string strategicContext)
        {
            return strategicContext.ToLowerInvariant() switch
            {
                "siege_operation" => "Siege",
                "desperate_defense" => "War",
                "coordinated_offensive" => "War",
                "raid_operation" => "War",
                "recruitment_drive" => "War",
                "patrol_peacetime" => "Peace",
                "garrison_duty" => "Town",
                "winter_camp" => "Camp",
                _ => "Peace"
            };
        }

        /// <summary>
        /// Checks if the current context matches the required context.
        /// Handles hierarchical matching (War includes Battle, Siege includes War, etc.).
        /// </summary>
        private static bool ContextMatches(string current, string required)
        {
            if (string.IsNullOrEmpty(required))
            {
                return true;
            }

            var reqLower = required.ToLowerInvariant();
            var curLower = current.ToLowerInvariant();

            // Exact match
            if (reqLower == curLower)
            {
                return true;
            }

            // War context includes multiple sub-contexts
            if (reqLower == "war" && (curLower == "siege" || curLower == "battle"))
            {
                return true;
            }

            // Camp context matches peace
            if (reqLower == "camp" && curLower == "peace")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a trigger condition string is met.
        /// Supports flag: and has_flag: prefixes for flag checking.
        /// Also supports custom retinue-related conditions.
        /// </summary>
        public static bool CheckTriggerCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition))
            {
                return true;
            }

            // Check for flag: prefix (e.g., "flag:qm_owes_favor")
            if (condition.StartsWith("flag:", StringComparison.OrdinalIgnoreCase))
            {
                var flagName = condition.Substring(5).Trim();
                return EscalationManager.Instance?.State?.HasFlag(flagName) ?? false;
            }

            // Check for has_flag: prefix (e.g., "has_flag:mutiny_joined")
            if (condition.StartsWith("has_flag:", StringComparison.OrdinalIgnoreCase))
            {
                var flagName = condition.Substring(9).Trim();
                return EscalationManager.Instance?.State?.HasFlag(flagName) ?? false;
            }

            // Check custom conditions
            return CheckCustomCondition(condition);
        }

        /// <summary>
        /// Checks custom requirement conditions for events.
        /// Supports retinue-related conditions for post-battle events and loyalty-based events.
        /// Also supports escalation threshold conditions like scrutiny_3, discipline_5, etc.
        /// Also supports campaign state conditions like at_sea, ai_safe, etc.
        /// </summary>
        private static bool CheckCustomCondition(string condition)
        {
            var lowerCondition = condition.ToLowerInvariant();
            
            // Check escalation threshold conditions (scrutiny_3, discipline_5, soldier_rep_20, etc.)
            if (TryCheckEscalationThresholdCondition(lowerCondition, out var result))
            {
                return result;
            }
            
            return lowerCondition switch
            {
                // Campaign state conditions
                "at_sea" => CheckAtSea(),
                "not_at_sea" => !CheckAtSea(),
                "ai_safe" => CheckAiSafe(),
                "camp_established" => CheckCampEstablished(),
                
                // Medical conditions
                "has_untreated_condition" => CheckHasUntreatedCondition(),
                "has_maritime_illness" => CheckHasMaritimeIllness(),
                "has_land_illness" => CheckHasLandIllness(),
                "not_maritime_illness" => !CheckHasMaritimeIllness(),
                
                // Retinue conditions
                "retinue_below_capacity" => CheckRetinueBelowCapacity(),
                "last_battle_won" => CheckLastBattleWon(),
                "has_retinue" => CheckHasRetinue(),
                "retinue_loyalty_low" => CheckRetinueLoyaltyLow(),
                "retinue_loyalty_high" => CheckRetinueLoyaltyHigh(),
                "retinue_wounded" => CheckRetinueWounded(),
                
                // Unknown conditions fail by default to prevent unimplemented triggers from incorrectly firing
                _ => false
            };
        }
        
        /// <summary>
        /// Checks if the player's party is currently at sea.
        /// Used by naval events (Warsails DLC) to ensure they only trigger during sea travel.
        /// </summary>
        private static bool CheckAtSea()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.CurrentLord?.PartyBelongedTo == null)
            {
                return false;
            }

            var lordParty = enlistment.CurrentLord.PartyBelongedTo;
            
            // BUGFIX: If party is in a settlement or besieging, they cannot be at sea
            // This prevents sea events from appearing when on land in settlements
            if (lordParty.CurrentSettlement != null || lordParty.BesiegedSettlement != null)
            {
                return false;
            }

            return lordParty.IsCurrentlyAtSea;
        }

        /// <summary>
        /// Checks if the player has an untreated condition (injury or illness without medical care).
        /// Used by condition worsening events that trigger when player neglects treatment.
        /// </summary>
        private static bool CheckHasUntreatedCondition()
        {
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                return false;
            }

            var state = conditions.State;
            if (state == null || !state.HasAnyCondition)
            {
                return false;
            }

            // Has condition but is NOT under medical care = untreated
            return !state.UnderMedicalCare;
        }

        /// <summary>
        /// Checks if the player has a maritime illness (ship_fever or scurvy).
        /// Used to select nautical-themed worsening events regardless of current location.
        /// </summary>
        private static bool CheckHasMaritimeIllness()
        {
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                return false;
            }

            var state = conditions.State;
            if (state == null || !state.HasIllness)
            {
                return false;
            }

            var illnessType = state.IllnessType?.ToLowerInvariant() ?? string.Empty;
            return illnessType is "ship_fever" or "scurvy";
        }

        /// <summary>
        /// Checks if the player has a land-based illness (camp_fever or flux).
        /// Used to select camp-themed worsening events regardless of current location.
        /// </summary>
        private static bool CheckHasLandIllness()
        {
            var conditions = PlayerConditionBehavior.Instance;
            if (conditions?.IsEnabled() != true)
            {
                return false;
            }

            var state = conditions.State;
            if (state == null || !state.HasIllness)
            {
                return false;
            }

            var illnessType = state.IllnessType?.ToLowerInvariant() ?? string.Empty;
            return illnessType is "camp_fever" or "flux";
        }
        
        /// <summary>
        /// Checks if the AI is in a safe state for event delivery.
        /// Prevents events from firing during AI decision-making, transitions, or unstable states.
        /// </summary>
        private static bool CheckAiSafe()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.CurrentLord?.PartyBelongedTo == null)
            {
                return false;
            }
            
            var lordParty = enlistment.CurrentLord.PartyBelongedTo;
            
            // Not safe if lord is in battle
            if (lordParty.MapEvent != null)
            {
                return false;
            }
            
            // Not safe if lord is in a settlement (menu transitions)
            if (lordParty.CurrentSettlement != null)
            {
                return false;
            }
            
            // Not safe if lord's AI is currently making decisions (DefaultBehavior in transition)
            if (lordParty.Ai.IsDisabled)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Checks if the party is currently in a camp (waiting/resting on the campaign map).
        /// </summary>
        private static bool CheckCampEstablished()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.CurrentLord?.PartyBelongedTo == null)
            {
                return false;
            }
            
            var lordParty = enlistment.CurrentLord.PartyBelongedTo;
            
            // Camp is established when party is waiting on the map (not moving, not in settlement, not in battle)
            return !lordParty.IsMoving && 
                   lordParty.CurrentSettlement == null && 
                   lordParty.MapEvent == null;
        }
        
        /// <summary>
        /// Checks if a condition is an escalation threshold condition and evaluates it.
        /// Returns true if it's a valid threshold condition, with the result in the out parameter.
        /// Handles patterns like: scrutiny_3, discipline_5, soldier_rep_20, soldier_rep_-20, medical_4
        /// </summary>
        private static bool TryCheckEscalationThresholdCondition(string condition, out bool result)
        {
            result = false;
            
            var escalation = EscalationManager.Instance?.State;
            if (escalation == null)
            {
                return false;
            }
            
            // Check scrutiny thresholds (scrutiny_3, scrutiny_5, scrutiny_7, scrutiny_10)
            if (condition.StartsWith("scrutiny_"))
            {
                if (int.TryParse(condition.Substring(9), out var threshold))
                {
                    result = escalation.Scrutiny >= threshold;
                    return true;
                }
            }
            
            // Discipline merged into Scrutiny - no longer supported as separate condition
            // JSON files should use scrutiny_ conditions instead
            
            // Check medical risk thresholds (medical_3, medical_4, medical_5)
            if (condition.StartsWith("medical_"))
            {
                if (int.TryParse(condition.Substring(8), out var threshold))
                {
                    result = escalation.MedicalRisk >= threshold;
                    return true;
                }
            }
            
            // Soldier reputation thresholds removed in Phase 3
            // JSON files should use lord relation conditions instead
            
            return false;
        }

        /// <summary>
        /// Checks if player's retinue is below tier capacity.
        /// Used by post-battle reinforcement events.
        /// </summary>
        private static bool CheckRetinueBelowCapacity()
        {
            var manager = RetinueManager.Instance;
            var enlistment = EnlistmentBehavior.Instance;
            if (manager?.State == null || enlistment == null)
            {
                return false;
            }

            var capacity = RetinueManager.GetTierCapacity(enlistment.EnlistmentTier);
            return manager.State.TotalSoldiers < capacity;
        }

        /// <summary>
        /// Checks if player won their last battle.
        /// Only returns true if a battle was fought within the last day.
        /// </summary>
        private static bool CheckLastBattleWon()
        {
            var state = RetinueManager.Instance?.State;
            if (state == null)
            {
                return false;
            }

            // Must have fought recently (within 1 day) and won
            var daysSinceBattle = state.GetDaysSinceLastBattle();
            return daysSinceBattle is >= 0 and < 1 && state.LastBattleWon;
        }

        /// <summary>
        /// Checks if the player has an active retinue with soldiers.
        /// </summary>
        private static bool CheckHasRetinue()
        {
            return RetinueManager.Instance?.State?.HasRetinue == true;
        }

        /// <summary>
        /// Checks if retinue loyalty is low (below 30).
        /// Used to trigger warning events about morale problems.
        /// </summary>
        private static bool CheckRetinueLoyaltyLow()
        {
            var state = RetinueManager.Instance?.State;
            if (state == null || !state.HasRetinue)
            {
                return false;
            }

            return state.RetinueLoyalty < 30;
        }

        /// <summary>
        /// Checks if retinue loyalty is high (above 70).
        /// Used to trigger positive events about high morale and bonding.
        /// </summary>
        private static bool CheckRetinueLoyaltyHigh()
        {
            var state = RetinueManager.Instance?.State;
            if (state == null || !state.HasRetinue)
            {
                return false;
            }

            return state.RetinueLoyalty > 70;
        }

        /// <summary>
        /// Checks if the retinue has wounded soldiers.
        /// Used to trigger medical care or recovery events.
        /// </summary>
        private static bool CheckRetinueWounded()
        {
            var manager = RetinueManager.Instance;
            var state = manager?.State;
            if (state == null || !state.HasRetinue)
            {
                return false;
            }

            var party = MobileParty.MainParty;
            if (party?.MemberRoster == null)
            {
                return false;
            }

            var roster = party.MemberRoster;

            // Check if any retinue troops are wounded
            foreach (var kvp in state.TroopCounts)
            {
                var characterId = kvp.Key;
                var character = CharacterObject.Find(characterId);
                if (character == null)
                {
                    continue;
                }

                var rosterIndex = roster.FindIndexOfTroop(character);
                if (rosterIndex >= 0)
                {
                    var element = roster.GetElementCopyAtIndex(rosterIndex);
                    if (element.WoundedNumber > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

