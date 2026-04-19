using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Represents a narrative event that can be triggered based on context, role, and escalation state.
    /// Events are loaded from JSON files and presented to the player with multiple choice options.
    /// </summary>
    public class EventDefinition
    {
        /// <summary>
        /// Unique identifier for this event (e.g., "evt_scout_tracks").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// XML string ID for the event title (localized via enlisted_strings.xml).
        /// </summary>
        public string TitleId { get; set; } = string.Empty;

        /// <summary>
        /// Inline fallback title text if XML localization lookup fails.
        /// </summary>
        public string TitleFallback { get; set; } = string.Empty;

        /// <summary>
        /// XML string ID for the event setup text (localized via enlisted_strings.xml).
        /// </summary>
        public string SetupId { get; set; } = string.Empty;

        /// <summary>
        /// Inline fallback setup text if XML localization lookup fails.
        /// </summary>
        public string SetupFallback { get; set; } = string.Empty;

        /// <summary>
        /// Event category for filtering and selection weighting.
        /// Examples: "escalation", "role", "universal", "muster", "crisis", "order_event".
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Order type this event belongs to (for order events only).
        /// Examples: "order_guard_post", "order_sentry_duty", "order_camp_patrol".
        /// Empty for non-order events.
        /// </summary>
        public string OrderType { get; set; } = string.Empty;

        /// <summary>
        /// News severity for color coding and persistence when event is posted to news feed.
        /// Valid values: "normal", "positive", "attention", "urgent", "critical".
        /// Default is "normal" if not specified.
        /// </summary>
        public string Severity { get; set; } = "normal";

        /// <summary>
        /// Requirements that determine when this event can fire.
        /// </summary>
        public EventRequirements Requirements { get; set; } = new();

        /// <summary>
        /// Timing constraints for event firing (cooldowns, priority, one-time flags).
        /// </summary>
        public EventTiming Timing { get; set; } = new();

        /// <summary>
        /// Trigger conditions that must ALL be true for this event to fire.
        /// Examples: "is_enlisted", "flag:qm_owes_favor", "current_activity:rest"
        /// </summary>
        public List<string> TriggersAll { get; set; } = [];

        /// <summary>
        /// Trigger conditions where at least ONE must be true for this event to fire.
        /// </summary>
        public List<string> TriggersAny { get; set; } = [];

        /// <summary>
        /// Trigger conditions that must ALL be false for this event to fire.
        /// </summary>
        public List<string> TriggersNone { get; set; } = [];

        /// <summary>
        /// Available options the player can choose from when this event fires.
        /// </summary>
        public List<EventOption> Options { get; set; } = [];

        /// <summary>
        /// Opportunity ID that triggered this event, if any.
        /// Used to track engagement and cooldown for camp opportunities.
        /// </summary>
        public string OriginatingOpportunityId { get; set; }

        /// <summary>
        /// Opportunity type that triggered this event, if any.
        /// Used for engagement tracking and learning system integration.
        /// </summary>
        public string OriginatingOpportunityType { get; set; }
    }

    /// <summary>
    /// Defines when an event is eligible to fire based on player state and context.
    /// </summary>
    public class EventRequirements
    {
        /// <summary>
        /// Minimum enlistment tier required (1-9). Null means no minimum.
        /// </summary>
        public int? MinTier { get; set; }

        /// <summary>
        /// Maximum enlistment tier allowed (1-9). Null means no maximum.
        /// </summary>
        public int? MaxTier { get; set; }

        /// <summary>
        /// Campaign context requirement: "War", "Peace", "Siege", "Battle", "Town", "Any".
        /// </summary>
        public string Context { get; set; } = "Any";

        /// <summary>
        /// Player role requirement: "Scout", "Medic", "Engineer", "Officer", "Operative", "NCO", "Soldier", "Any".
        /// </summary>
        public string Role { get; set; } = "Any";

        /// <summary>
        /// Escalation thresholds required to trigger this event.
        /// Key: track name ("Scrutiny", "MedicalRisk"), Value: minimum level.
        /// </summary>
        public Dictionary<string, int> MinEscalation { get; set; } = [];

        /// <summary>
        /// Minimum skill levels required to see this event.
        /// Key: skill name (e.g., "Scouting"), Value: minimum level.
        /// </summary>
        public Dictionary<string, int> MinSkills { get; set; } = [];

        /// <summary>
        /// Minimum trait levels required to see this event.
        /// Key: trait name (e.g., "ScoutSkills"), Value: minimum level.
        /// </summary>
        public Dictionary<string, int> MinTraits { get; set; } = [];

        /// <summary>
        /// World state requirements for order events.
        /// Array of world state keys like "peacetime_garrison", "war_active_campaign", "siege_attacking".
        /// Event is eligible if current world state matches any of these values.
        /// </summary>
        public List<string> WorldState { get; set; } = [];

        /// <summary>
        /// Maximum HP percentage for this event to be available.
        /// Used for decisions like "Seek Treatment" which only appear when wounded.
        /// Value is 0-100. Null means no HP check.
        /// </summary>
        public int? HpBelow { get; set; }


        /// <summary>
        /// If true, requires the player's baggage stash to have at least one item.
        /// Used by theft events to avoid firing when there's nothing to steal.
        /// </summary>
        public bool? BaggageHasItems { get; set; }

        /// <summary>
        /// If true, requires the party to NOT be at sea (on land).
        /// Used by land-based events like baggage wagons that don't make sense during sea travel.
        /// </summary>
        public bool? NotAtSea { get; set; }

        /// <summary>
        /// If true, requires the party to BE at sea (sailing).
        /// Used by maritime events like ship's hold access that only make sense during sea travel.
        /// </summary>
        public bool? AtSea { get; set; }

        /// <summary>
        /// If true, requires the player to have any condition (injury, illness, or exhaustion).
        /// Used by medical decisions that only appear when the player needs treatment.
        /// </summary>
        public bool? HasAnyCondition { get; set; }

        /// <summary>
        /// If true, requires the player to have a severe or critical condition.
        /// Used by emergency medical decisions that only appear for urgent cases.
        /// </summary>
        public bool? HasSevereCondition { get; set; }

        /// <summary>
        /// Maximum illness severity allowed for this event to be available.
        /// Valid values: "None", "Mild", "Moderate", "Severe". 
        /// Used by training/labor decisions to block strenuous activity when too ill.
        /// "None" means player must have no illness. Null means no illness restriction.
        /// </summary>
        public string MaxIllness { get; set; }
    }

    /// <summary>
    /// Timing and pacing constraints for event firing.
    /// </summary>
    public class EventTiming
    {
        /// <summary>
        /// Minimum days between firing this specific event again.
        /// </summary>
        public int CooldownDays { get; set; } = 7;

        /// <summary>
        /// Event priority for selection: "low", "normal", "high", "critical".
        /// Higher priority events are more likely to be selected when eligible.
        /// </summary>
        public string Priority { get; set; } = "normal";

        /// <summary>
        /// If true, this event can only fire once per playthrough.
        /// </summary>
        public bool OneTime { get; set; }
    }

    /// <summary>
    /// Represents a single choice the player can make when an event fires.
    /// </summary>
    public class EventOption
    {
        /// <summary>
        /// Unique identifier for this option within the event.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// XML string ID for the option button text (localized).
        /// </summary>
        public string TextId { get; set; } = string.Empty;

        /// <summary>
        /// Inline fallback option text if XML localization lookup fails.
        /// </summary>
        public string TextFallback { get; set; } = string.Empty;

        /// <summary>
        /// XML string ID for the result text shown after choosing this option (localized).
        /// </summary>
        public string ResultTextId { get; set; } = string.Empty;

        /// <summary>
        /// Inline fallback result text if XML localization lookup fails.
        /// </summary>
        public string ResultTextFallback { get; set; } = string.Empty;

        /// <summary>
        /// Optional tooltip hint explaining consequences or requirements.
        /// </summary>
        public string Tooltip { get; set; } = string.Empty;

        /// <summary>
        /// Requirements to unlock this option. If not met, option is grayed out.
        /// </summary>
        public EventOptionRequirements Requirements { get; set; }

        /// <summary>
        /// Effects applied when the player chooses this option.
        /// For non-risky options, these are always applied.
        /// For risky options, these are applied before the success/failure roll.
        /// </summary>
        public EventEffects Effects { get; set; } = new();

        /// <summary>
        /// Effects applied when a risky option succeeds. Null if not a risky option.
        /// </summary>
        public EventEffects EffectsSuccess { get; set; }

        /// <summary>
        /// Effects applied when a risky option fails. Null if not a risky option.
        /// </summary>
        public EventEffects EffectsFailure { get; set; }

        /// <summary>
        /// Percentage chance of success for risky options (0-100). Null for safe options.
        /// This is the base chance, which may be modified by skillCheck if specified.
        /// </summary>
        public int? RiskChance { get; set; }

        /// <summary>
        /// Risk level for this option: "low", "medium", "high". Used for UI hints.
        /// </summary>
        public string Risk { get; set; }

        /// <summary>
        /// Skill name used to modify success chance (e.g., "Medicine", "Athletics", "Roguery").
        /// When specified, player's skill level adds to the base RiskChance percentage.
        /// Formula: actualChance = RiskChance + ((playerSkill - skillBase) / 5)
        /// </summary>
        public string SkillCheck { get; set; }

        /// <summary>
        /// Base skill level for skill checks (default 50).
        /// Player skill above this grants bonus success chance, below grants penalty.
        /// </summary>
        public int SkillBase { get; set; } = 50;

        /// <summary>
        /// Template for generating dynamic tooltips that show calculated success chance.
        /// Supports placeholders: {CHANCE}, {SKILL}, {SKILL_NAME}.
        /// Example: "{CHANCE}% success ({SKILL_NAME} {SKILL}). Treats illness. Costs 100 gold."
        /// </summary>
        public string TooltipTemplate { get; set; }

        /// <summary>
        /// Inline fallback result text for failure outcome. Used for risky options.
        /// </summary>
        public string ResultTextFailureFallback { get; set; } = string.Empty;

        /// <summary>
        /// XML string ID for the failure result text.
        /// </summary>
        public string ResultTextFailureId { get; set; } = string.Empty;

        /// <summary>
        /// Flags to set when this option is chosen.
        /// Flags are temporary boolean states that gate access to other decisions/events.
        /// </summary>
        public List<string> SetFlags { get; set; } = [];

        /// <summary>
        /// Flags to clear when this option is chosen.
        /// </summary>
        public List<string> ClearFlags { get; set; } = [];

        /// <summary>
        /// Duration in days for flags set by this option.
        /// After this time, flags auto-expire. 0 = permanent until cleared.
        /// </summary>
        public int FlagDurationDays { get; set; }

        /// <summary>
        /// ID of an event to trigger after this option is chosen, with a delay.
        /// Used for follow-up events (e.g., friend repays loan after 7 days).
        /// </summary>
        public string ChainsTo { get; set; }

        /// <summary>
        /// Hours to wait before triggering the chained event.
        /// Only used if ChainsTo is specified.
        /// </summary>
        public int ChainDelayHours { get; set; }

        /// <summary>
        /// Optional sub-choices presented after this option is selected.
        /// Used for branching rewards (training type, compensation method, etc.).
        /// </summary>
        public RewardChoices RewardChoices { get; set; }

        /// <summary>
        /// If true, aborts the enlistment process without normal discharge penalties.
        /// Used by the bag check "abort" option. Player party restored to normal state.
        /// Lord reputation penalty applied via effects.LordRep field.
        /// </summary>
        public bool AbortsEnlistment { get; set; }
    }

    /// <summary>
    /// Requirements that gate access to an event option.
    /// If requirements are not met, the option appears but is disabled.
    /// </summary>
    public class EventOptionRequirements
    {
        /// <summary>
        /// Minimum skill levels required to select this option.
        /// Key: skill name (e.g., "Scouting"), Value: minimum level.
        /// </summary>
        public Dictionary<string, int> MinSkills { get; set; } = [];

        /// <summary>
        /// Minimum trait levels required to select this option.
        /// Key: trait name (e.g., "ScoutSkills"), Value: minimum level.
        /// </summary>
        public Dictionary<string, int> MinTraits { get; set; } = [];

        /// <summary>
        /// Minimum enlistment tier required to select this option.
        /// </summary>
        public int? MinTier { get; set; }

        /// <summary>
        /// Required player role to select this option.
        /// </summary>
        public string Role { get; set; }
    }

    /// <summary>
    /// Represents a set of sub-choices presented after the main option is selected.
    /// Used for branching rewards like training weapon focus, hunt compensation, dice winnings, etc.
    /// </summary>
    public class RewardChoices
    {
        /// <summary>
        /// Type of choice: "compensation", "weapon_focus", "training_type", etc.
        /// Used for analytics and potential conditional logic.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Prompt text shown to the player (e.g., "What do you do with your winnings?").
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Available sub-choice options.
        /// </summary>
        public List<RewardChoiceOption> Options { get; set; } = [];
    }

    /// <summary>
    /// A single sub-choice option within a RewardChoices block.
    /// Each option can have its own rewards, effects, and costs.
    /// </summary>
    public class RewardChoiceOption
    {
        /// <summary>
        /// Unique identifier for this sub-choice within the parent.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display text for this option shown in the popup.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Tooltip explaining the consequences or requirements.
        /// </summary>
        public string Tooltip { get; set; } = string.Empty;

        /// <summary>
        /// Optional condition for when this option appears (e.g., "formation:ranged").
        /// If condition fails, the option is hidden from the popup.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Optional additional costs for this sub-choice.
        /// Deducted when the sub-choice is selected.
        /// </summary>
        public EventCosts Costs { get; set; }

        /// <summary>
        /// Rewards applied when this sub-choice is selected.
        /// Includes gold, XP, skill XP.
        /// </summary>
        public EventRewards Rewards { get; set; }

        /// <summary>
        /// Effects applied when this sub-choice is selected.
        /// Uses the same effect system as main event options.
        /// </summary>
        public EventEffects Effects { get; set; }
    }

    /// <summary>
    /// Reward values that can be applied from an option or sub-choice.
    /// Separate from effects to distinguish "rewards" (gains) from "effects" (changes).
    /// </summary>
    public class EventRewards
    {
        /// <summary>
        /// Gold/denar reward (positive value).
        /// </summary>
        public int? Gold { get; set; }

        /// <summary>
        /// General XP rewards (e.g., {"enlisted": 20} for enlisted-specific XP tracking).
        /// </summary>
        public Dictionary<string, int> Xp { get; set; } = [];

        /// <summary>
        /// Skill XP awards (e.g., {"OneHanded": 40, "Bow": 30}).
        /// </summary>
        public Dictionary<string, int> SkillXp { get; set; } = [];

        /// <summary>
        /// Dynamic skill XP keys that are resolved at runtime:
        /// - "equipped_weapon" - XP goes to the skill matching equipped weapon
        /// - "weakest_combat" - XP goes to hero's lowest combat skill
        /// </summary>
        public Dictionary<string, int> DynamicSkillXp { get; set; } = [];
    }

    /// <summary>
    /// Costs that must be paid to select an option or sub-choice.
    /// Deducted from player resources when the choice is made.
    /// </summary>
    public class EventCosts
    {
        /// <summary>
        /// Gold cost (deducted from player).
        /// </summary>
        public int? Gold { get; set; }

        /// <summary>
        /// Time cost in hours (advances campaign time or affects scheduling).
        /// </summary>
        public int? TimeHours { get; set; }
    }

    /// <summary>
    /// Effects applied when an event option is chosen.
    /// All effects are optional and can be positive or negative.
    /// </summary>
    public class EventEffects
    {
        /// <summary>
        /// Skill XP awards. Key: skill name, Value: XP amount (can be negative).
        /// </summary>
        public Dictionary<string, int> SkillXp { get; set; } = [];

        /// <summary>
        /// Dynamic skill XP keys that are resolved at runtime:
        /// - "equipped_weapon" - XP goes to the skill matching equipped weapon
        /// - "weakest_combat" - XP goes to hero's lowest combat skill
        /// </summary>
        public Dictionary<string, int> DynamicSkillXp { get; set; } = [];

        /// <summary>
        /// Trait XP awards. Key: trait name, Value: XP amount (can be negative).
        /// </summary>
        public Dictionary<string, int> TraitXp { get; set; } = [];

        /// <summary>
        /// Reputation change with the lord (-100 to +100 delta).
        /// </summary>
        public int? LordRep { get; set; }

        /// <summary>
        /// Scrutiny escalation change (-100 to +100 delta).
        /// </summary>
        public int? Scrutiny { get; set; }

        /// <summary>
        /// Medical risk escalation change (0-5 scale delta).
        /// </summary>
        public int? MedicalRisk { get; set; }

        /// <summary>
        /// Gold/denar change (positive = gain, negative = loss).
        /// </summary>
        public int? Gold { get; set; }

        /// <summary>
        /// Player HP change (positive = heal, negative = damage).
        /// </summary>
        public int? HpChange { get; set; }

        /// <summary>
        /// Applies a wound to the player (reduces HP and may trigger medical care).
        /// Valid values: "minor", "moderate", "severe".
        /// </summary>
        public string ApplyWound { get; set; }

        /// <summary>
        /// Number of troops lost from the party (rare, dramatic events).
        /// </summary>
        public int? TroopLoss { get; set; }

        /// <summary>
        /// Number of troops wounded in the party.
        /// </summary>
        public int? TroopWounded { get; set; }

        /// <summary>
        /// Food items lost from player inventory.
        /// </summary>
        public int? FoodLoss { get; set; }

        /// <summary>
        /// XP awarded to lord's T1-T3 troops (NCO training).
        /// </summary>
        public int? TroopXp { get; set; }

        /// <summary>
        /// ID of a follow-up event to trigger after this one completes.
        /// </summary>
        public string ChainEventId { get; set; }

        /// <summary>
        /// Company needs modifications.
        /// Key: need name ("Readiness", "Morale", "Supplies", "Equipment", "Rest").
        /// Value: delta to apply.
        /// </summary>
        public Dictionary<string, int> CompanyNeeds { get; set; } = [];

        /// <summary>
        /// Renown change for the player's clan.
        /// </summary>
        public int? Renown { get; set; }

        /// <summary>
        /// Discharge band to apply when this option is chosen. Ends the player's enlistment.
        /// Valid values: "dishonorable", "washout", "deserter".
        /// Null or empty means no discharge is triggered.
        /// </summary>
        public string TriggersDischarge { get; set; }

        /// <summary>
        /// Tier to promote player to when this option is chosen.
        /// Used by proving events to grant promotions after proving worthy.
        /// Null means no promotion is triggered.
        /// </summary>
        public int? Promotes { get; set; }

        /// <summary>
        /// Number of soldiers to add to the player's retinue.
        /// Used by post-battle volunteer events and similar.
        /// Only applies if player has Commander rank (T7+) and has a retinue type selected.
        /// </summary>
        public int? RetinueGain { get; set; }

        /// <summary>
        /// Modifier to retinue loyalty (affects Commander's relationship with their retinue).
        /// Positive values increase loyalty, negative decrease it. Range: -100 to +100.
        /// Only applies if player has Commander rank (T7+) and has a retinue.
        /// </summary>
        public int? RetinueLoyalty { get; set; }

        /// <summary>
        /// Number of soldiers to remove from the player's retinue.
        /// Removes only troops tracked in RetinueState (does not affect lord's main force).
        /// Used for dramatic events like ambush, betrayal, or desertion.
        /// Only applies if player has Commander rank (T7+) and has a retinue.
        /// </summary>
        public int? RetinueLoss { get; set; }

        /// <summary>
        /// Number of soldiers to wound in the player's retinue.
        /// Wounds only troops tracked in RetinueState (moves healthy to wounded roster).
        /// Used for events involving skirmishes, accidents, or hardship.
        /// Only applies if player has Commander rank (T7+) and has a retinue.
        /// </summary>
        public int? RetinueWounded { get; set; }

        /// <summary>
        /// Grants temporary baggage access for the specified number of hours.
        /// Used by "baggage caught up" events to give players a window to access their stash.
        /// </summary>
        public int? GrantTemporaryBaggageAccess { get; set; }

        /// <summary>
        /// Applies a delay to baggage availability in days.
        /// Value of 0 clears any existing delay, positive values add delay.
        /// Used by events where the baggage train gets stuck, delayed by weather, or raided.
        /// </summary>
        public int? BaggageDelayDays { get; set; }

        /// <summary>
        /// Removes a random number of items from the player's baggage stash.
        /// Used by theft, raid, and loss events. Gracefully handles empty stash (no crash, no message).
        /// </summary>
        public int? RandomBaggageLoss { get; set; }

        /// <summary>
        /// Bag check action to execute when this option is chosen.
        /// Valid values: "stash" (stow in baggage), "sell" (liquidate at 60%), "smuggle" (Roguery check).
        /// Used by the first-enlistment bag check event to handle the player's personal gear.
        /// </summary>
        public string BagCheckChoice { get; set; }

        /// <summary>
        /// Applies an illness to the player with specified severity.
        /// Valid values: "minor", "mild", "moderate", "severe", "critical".
        /// Used by illness onset events and worsening condition events.
        /// </summary>
        public string IllnessOnset { get; set; }

        /// <summary>
        /// Applies an injury to the player with specified severity.
        /// Valid values: "minor", "moderate", "severe", "critical".
        /// Used by injury events and combat outcomes.
        /// </summary>
        public string InjuryOnset { get; set; }

        /// <summary>
        /// Begins medical treatment for the player's current condition.
        /// When true, marks player as under medical care and applies recovery rate modifier.
        /// Used by treatment decisions and surgeon events.
        /// </summary>
        public bool? BeginTreatment { get; set; }

        /// <summary>
        /// Worsens the player's current condition by one severity level.
        /// Used by untreated condition events and failure outcomes.
        /// </summary>
        public bool? WorsenCondition { get; set; }
    }
}

