# Injury & Illness System

**Summary:** The injury and illness system tracks player medical conditions through a unified condition system. Injuries (twisted knee, blade cut, arrow wound) result from orders, events, and combat, while illnesses (camp fever, flux, ship fever, scurvy) develop from medical risk escalation. Both use context-aware treatment (camp surgeon on land, ship's surgeon at sea), recovery tracking, and narrative descriptions. Maritime illnesses automatically apply when at sea (ship fever, scurvy) vs land-based illnesses (camp fever, flux).

**Status:** ✅ Implemented  
**Last Updated:** 2026-01-01  
**Implementation:** `src/Features/Conditions/PlayerConditionBehavior.cs`, `src/Features/Content/EventDeliveryManager.cs`, `ModuleData/Enlisted/Conditions/condition_defs.json`  
**Related Docs:** [Storylet Backbone](storylet-backbone.md)

---

## Index

1. [Overview](#overview)
2. [Unified Condition System](#unified-condition-system)
3. [Illness System](#illness-system)
4. [Maritime Context Awareness](#maritime-context-awareness)
5. [Medical Treatment](#medical-treatment)
6. [Design Philosophy](#design-philosophy)
7. [Injury Definitions](#injury-definitions)
8. [Illness Definitions](#illness-definitions)
9. [Severity Levels](#severity-levels)
10. [HP Calculation](#hp-calculation)
11. [Integration Points](#integration-points)
12. [Narrative System](#narrative-system)
13. [JSON Schema](#json-schema)
14. [API Reference](#api-reference)
15. [Implementation Notes](#implementation-notes)

---

## Overview

The medical condition system tracks both injuries and illnesses through a unified player condition tracking system. Injuries result from immediate events (combat, orders, accidents), while illnesses develop from medical risk escalation and environmental factors. Both share recovery mechanics, treatment systems, and context-aware narrative presentation.

### Core Principles

- **Unified tracking** - Single system handles injuries and illnesses
- **Context-aware** - Maritime vs land-based conditions and treatment
- **Narrative immersion** - Every condition has descriptive flavor text
- **Progressive worsening** - Untreated conditions escalate over time
- **Treatment system** - Camp surgeon (land) or ship's surgeon (sea)

### Key Features

- 3 injury types (twisted knee, blade cut, arrow wound)
- 4 illness types (camp fever, flux, ship fever, scurvy)
- Automatic context selection (maritime illnesses at sea, land illnesses on land)
- Treatment tracking (under medical care vs untreated)
- Condition worsening events (different narrative for ship vs camp)
- Recovery time calculation based on severity
- Medical risk escalation integration

---

## Unified Condition System

The `PlayerConditionBehavior` manages a single condition state that tracks:

**Current Conditions:**
- Injury type, severity, and days remaining
- Illness type, severity, and days remaining  
- Exhaustion level (separate from injury/illness)

**Treatment State:**
- Under medical care (applies recovery multiplier)
- Medical risk level (0-5, triggers illness onset)
- Days untreated (triggers worsening events)

**Player can have:**
- One injury AND one illness simultaneously
- Either injury OR illness (not required to have both)
- Exhaustion alongside any condition

**Example states:**
- Twisted Knee (Moderate) + Camp Fever (Mild) = Injured and ill
- Ship Fever (Severe) only = Ill but not injured
- Blade Cut (Minor) only = Injured but not ill
- Healthy but Exhausted = No injury/illness, just tired

---

## Illness System

Illnesses develop from medical risk escalation, not direct damage events. The ContentOrchestrator monitors medical risk and triggers illness onset events when risk reaches thresholds.

### Medical Risk Escalation

Medical risk is an escalation track (0-5) that accumulates from various sources and triggers illness onset when thresholds are reached.

**Risk Sources:**
- Injuries (each injury adds risk based on severity)
- Low HP (risk increases as HP drops)
- Exhaustion (low fatigue adds risk)
- Harsh conditions (siege, harsh terrain)
- Failed orders with injury risk
- Untreated existing conditions

**Risk Levels:**
| Risk Level | Status | Illness Trigger Chance |
|------------|--------|------------------------|
| 0 | Healthy | 0% |
| 1-2 | At Risk | 5-10% |
| 3 | Minor Illness Threshold | 15% |
| 4 | Moderate Illness Threshold | 20% |
| 5+ | Severe Illness Threshold | 25%+ |

**Risk Reduction:**
- Medical treatment (immediate -2 to -5 risk)
- Rest and recovery (gradual reduction)
- Successful rest decisions (-1 risk)
- Time passing while healthy

### Orchestrator Integration

**Daily Check:** ContentOrchestrator calls `CheckMedicalPressure()` at 6am each day during world-state tick.

**Probability Calculation:**
```
Base chance = 5% per Medical Risk level
+ Fatigue modifier (+10% if exhausted)
+ Context modifier (+12% during siege)
+ Consecutive high-pressure days (+5% each day)
```

**Cooldown:** 7-day cooldown between illness onset events to prevent illness spam.

**Illness Onset Triggers:**
- Medical Risk 3+ with consecutive high-pressure days
- Probability calculation as above
- Maritime vs land context determines illness type
- Blocked if player already has an illness

**Illness Progression:**
- Minor illness (3-5 day recovery)
- Moderate illness (7-10 day recovery)
- Severe illness (14-22 day recovery)
- Critical illness (21-25 day recovery)

**Context-Aware Illness Types:**
| Context | Mild/Moderate | Severe/Critical |
|---------|---------------|-----------------|
| At Sea | Ship Fever | Scurvy |
| On Land | Camp Fever | Flux |

**Example Flow:**
1. Player medical risk reaches 4 (from injuries, exhaustion, harsh conditions)
2. Daily orchestrator check: 20% illness chance (4 × 5% base)
3. Modifiers apply: +10% if exhausted, +12% during siege
4. Roll succeeds → Queue `illness_onset_moderate` or `illness_onset_moderate_sea`
5. Player chooses response (seek treatment, tough it out, herbal remedy)
6. Illness applied with context-appropriate type

---

## Maritime Context Awareness

The medical system automatically adapts content and treatment based on travel context.

**Detection:**
- Uses native `party.IsCurrentlyAtSea` property
- Checked when: queuing illness events, applying illness effects, filtering decisions
- No special DLC required (property exists in base game, always false if not at sea)

**Maritime Illness Events:**
All illness onset events have `_sea` variants with nautical flavor:
- `illness_onset_minor_sea` - Ship fever starting, fresh air on deck
- `illness_onset_moderate_sea` - Fever in cramped quarters, ship's surgeon
- `illness_onset_severe_sea` - Near-overboard, sick bay treatment
- `untreated_condition_worsening_sea` - Worsening during voyage

**Illness-Type Matching:**
Worsening events match illness type, not current location:
- Ship fever at sea → Shows sea worsening event ✅
- Ship fever on land → Shows sea worsening event ✅ (maintains consistency)
- Camp fever on land → Shows land worsening event ✅
- Injury only → Shows land worsening event (default)

**Trigger Conditions:**
Events use illness-type triggers, not location triggers:
```json
{
  "triggers": {
    "all": ["has_untreated_condition", "has_maritime_illness"]
  }
}
```

This ensures narrative consistency even if player catches ship fever then lands.

---

## Medical Treatment

Treatment system works identically on land and at sea, with context-appropriate flavor.

**Treatment Options:**
| Decision | Land Version | Sea Version | Cost | Effect |
|----------|-------------|-------------|------|--------|
| Surgeon | Camp surgeon's tent | Ship's surgeon belowdecks | 100g | Professional care, starts treatment |
| Rest | By supply wagons | Fresh air topside | Free | Passive recovery, reduces risk |
| Remedy | Old woman's herbs | Sailor's grog (rum+lime) | 15-30g | Cheaper alternative, 50% success |
| Emergency | Surgeon's tent | Sick bay belowdecks | 200g | Critical care, immediate relief |

**Treatment Effect:**
- Sets `UnderMedicalCare = true`
- Resets medical risk to 0
- Applies recovery multiplier to daily healing
- Prevents condition worsening events
- Shows treatment icon in status displays

**Without Treatment:**
- Condition worsens over time (3+ days untreated)
- Medical risk increases
- Worsening events fire (60% chance to escalate severity)
- Recovery takes longer (no multiplier)

---

## Design Philosophy

### Why Percentage-Based?

**Problem with flat HP loss:**
- Doesn't scale with player level/HP
- Sprained ankle shouldn't do same damage as gut wound
- No narrative context for what happened

**Solution with injury system:**
- Sprained ankle = 15% HP (minor, recoverable)
- Broken rib = 25% HP (moderate, painful)
- Gut wound = 50% HP (severe, life-threatening)
- Scales naturally with player HP growth

### Narrative First

Injuries are experienced, not just numbers:

```
❌ Before: "You took 25 HP damage from order outcome"
✅ After: "Steel finds your belly. Blood seeps between your fingers."
          [Player loses 50% HP based on gut_wound injury]
```

Players understand what happened and why they're hurt.

---

## Injury Definitions

### Complete Injury Catalog

All injuries defined in `ModuleData/Enlisted/Content/injuries.json`:

| Injury ID | Display Name | HP % | Recovery Days | Severity |
|-----------|--------------|------|---------------|----------|
| `sprained_ankle` | Sprained Ankle | 15% | 3 | Minor |
| `bruised_ribs` | Bruised Ribs | 18% | 4 | Minor |
| `minor_cut` | Minor Cut | 10% | 2 | Minor |
| `twisted_knee` | Twisted Knee | 20% | 5 | Moderate |
| `broken_finger` | Broken Finger | 12% | 7 | Moderate |
| `deep_gash` | Deep Gash | 30% | 8 | Moderate |
| `broken_rib` | Broken Rib | 25% | 10 | Serious |
| `concussion` | Concussion | 28% | 7 | Serious |
| `broken_arm` | Broken Arm | 35% | 14 | Serious |
| `puncture_wound` | Puncture Wound | 40% | 12 | Severe |
| `gut_wound` | Gut Wound | 50% | 10 | Severe |
| `broken_leg` | Broken Leg | 45% | 21 | Severe |
| `crushed_hand` | Crushed Hand | 38% | 18 | Severe |
| `head_trauma` | Head Trauma | 48% | 14 | Critical |
| `near_death` | Near Death | 55% | 30 | Critical |

---

## Illness Definitions

### Complete Illness Catalog

All illnesses defined in `ModuleData/Enlisted/Conditions/condition_defs.json`:

| Illness ID | Display Name | Context | Recovery Days (by severity) | Notes |
|------------|--------------|---------|---------------------------|-------|
| `camp_fever` | Camp Fever | Land | Mild: 3, Moderate: 7, Severe: 14, Critical: 21 | Common camp illness from cramped conditions |
| `flux` | Flux | Land | Mild: 4, Moderate: 8, Severe: 14, Critical: 21 | Digestive illness, more severe than camp fever |
| `ship_fever` | Ship Fever | Maritime | Mild: 4, Moderate: 8, Severe: 15, Critical: 22 | Common maritime illness in cramped quarters |
| `scurvy` | Scurvy | Maritime | Mild: 5, Moderate: 10, Severe: 18, Critical: 25 | Vitamin deficiency from prolonged voyages |

**Automatic Context Selection:**
```csharp
// In EventDeliveryManager.ApplyIllnessOnset()
var isAtSea = party.IsCurrentlyAtSea;
if (isAtSea)
{
    illnessType = severity >= Severe ? "scurvy" : "ship_fever";
}
else
{
    illnessType = severity >= Severe ? "flux" : "camp_fever";
}
```

**Recovery Time Comparison:**
- Maritime illnesses generally take 1-2 days longer to recover
- Scurvy has longest recovery time (up to 25 days for critical)
- Flux and severe camp fever both max at 21 days

**Illness Application:**
- Illnesses do NOT cause immediate HP damage (unlike injuries)
- HP loss comes from untreated condition daily tick
- Recovery begins immediately if under medical care
- Without treatment, condition can worsen to next severity level

---

## Severity Levels

Injuries are categorized by severity for contextual selection:

### Minor (10-20% HP)
**Use when:** Training accidents, minor mishaps, low-danger situations
- Sprained Ankle (15%)
- Bruised Ribs (18%)
- Minor Cut (10%)

**Example Usage:**
```json
"outcome": {
  "injury_type": "sprained_ankle",
  "reason": "training_accident"
}
```

### Moderate (20-30% HP)
**Use when:** Failed skill checks, moderate danger, physical labor
- Twisted Knee (20%)
- Deep Gash (30%)
- Broken Finger (12%)

### Serious (25-35% HP)
**Use when:** Combat-adjacent danger, falls, significant failures
- Broken Rib (25%)
- Concussion (28%)
- Broken Arm (35%)

### Severe (35-50% HP)
**Use when:** Direct combat, critical failures, life-threatening situations
- Puncture Wound (40%)
- Gut Wound (50%)
- Broken Leg (45%)
- Crushed Hand (38%)

### Critical (45-55% HP)
**Use when:** Near-death experiences, catastrophic failures
- Head Trauma (48%)
- Near Death (55%)

---

## HP Calculation

### Percentage-Based Formula

```csharp
// Load injury definition
var injuryDef = GetInjuryDefinition(injuryId);

// Calculate damage as percentage of max HP
var maxHp = Hero.MainHero.CharacterObject.MaxHitPoints();
var hpLoss = (int)(maxHp * injuryDef.HpPercentage);

// Apply damage (minimum 1 HP, won't kill player)
var currentHp = Hero.MainHero.HitPoints;
var newHp = Math.Max(1, currentHp - hpLoss);
Hero.MainHero.HitPoints = newHp;
```

### Example Calculation

**Player with 100 max HP:**
- Sprained Ankle (15%) → 15 HP loss
- Gut Wound (50%) → 50 HP loss

**Player with 200 max HP (higher level):**
- Sprained Ankle (15%) → 30 HP loss
- Gut Wound (50%) → 100 HP loss

The injury severity scales naturally with character progression.

---

## Integration Points

### Activity Outcomes

Activity and storylet outcomes route medical consequences through `EventDeliveryManager` and `PlayerConditionBehavior`.

**Code path:**
```csharp
EventDeliveryManager
  -> PlayerConditionBehavior.TryApplyInjury(...)
  -> Displays narrative + applies recovery state
```

### Event Outcomes

Events can trigger injuries through outcome effects:

```json:ModuleData/Enlisted/Events/illness_onset.json
{
  "id": "untreated_condition_worsening",
  "options": [
    {
      "id": "fight_back",
      "outcome": {
        "injury_type": "deep_gash",
        "reputation": { "soldier": 5 },
        "text": "You stand and fight. Steel clashes. You take a blade to the arm but hold your ground."
      }
    }
  ]
}
```

### Direct Code Application

Systems can apply injuries programmatically:

```csharp
// Apply specific injury
InjurySystem.ApplyInjury("broken_rib", "combat_incident");

// Apply random injury by severity
var injury = InjurySystem.GetRandomInjury(InjurySeverity.Moderate);
InjurySystem.ApplyInjury(injury.Id, "failed_skill_check");

// Get brief narrative for UI
var briefText = InjurySystem.GetBriefNarrative("sprained_ankle");
// Returns: "Sprained ankle"
```

---

## Narrative System

### Full Narratives

Each injury has 2-4 narrative variations for immersion:

**Sprained Ankle:**
- "Your ankle rolls on uneven ground. It swells before you can unlace your boot."
- "A misstep on the rocky path. Your ankle protests with a sharp pain."

**Gut Wound:**
- "Steel finds your belly. Blood seeps between your fingers."
- "A thrust to the gut. You gasp, the world blurring."

### Brief Narratives

Short forms for UI display and recap feeds:

| Injury | Brief Narrative |
|--------|-----------------|
| Sprained Ankle | "Sprained ankle" |
| Gut Wound | "Gut wound" |
| Broken Rib | "Broken rib" |
| Deep Gash | "Deep gash" |

Used in:
- Order recap feeds
- Recent Activities display
- Daily Brief summaries

---

## JSON Schema

### Injury Definition Structure

```json
{
  "id": "injury_identifier",
  "displayName": "Human-Readable Name",
  "hpPercentage": 0.15,
  "narratives": [
    "First narrative variation...",
    "Second narrative variation...",
    "Third narrative variation (optional)..."
  ],
  "briefNarrative": "Short form for UI",
  "recoveryDays": 7,
  "severity": "Minor | Moderate | Serious | Severe | Critical"
}
```

### Full Example

```json
{
  "id": "broken_rib",
  "displayName": "Broken Rib",
  "hpPercentage": 0.25,
  "narratives": [
    "Something cracks in your chest. Each breath is a knife.",
    "The impact drives air from your lungs. Breathing becomes agony.",
    "A rib snaps like kindling. You taste copper."
  ],
  "briefNarrative": "Broken rib",
  "recoveryDays": 10,
  "severity": "Serious"
}
```

### Severity Enum

```csharp
public enum InjurySeverity
{
    Minor,      // 10-20% HP
    Moderate,   // 20-30% HP
    Serious,    // 25-35% HP
    Severe,     // 35-50% HP
    Critical    // 45-55% HP
}
```

**Note:** Uses existing enum from `PlayerConditionModels.cs` for consistency.

---

## API Reference

### InjurySystem (Static Class)

#### Initialize()
Loads injury definitions from `injuries.json` at game start.

```csharp
// Called from SubModule.OnGameStart()
InjurySystem.Initialize();
```

#### ApplyInjury(string injuryId, string reason)
Applies a specific injury to the player with full narrative.

```csharp
InjurySystem.ApplyInjury("sprained_ankle", "training_accident");
// Displays: "Your ankle rolls on uneven ground..."
// Applies: 15% HP loss
// Logs: Player injured - Sprained Ankle (training_accident): 100 -> 85 HP
```

**Parameters:**
- `injuryId` - Injury identifier from `injuries.json`
- `reason` - Context for logging (e.g., "order_outcome", "combat_incident")

#### GetRandomInjury(InjurySeverity severity)
Returns a random injury definition of the specified severity.

```csharp
var injury = InjurySystem.GetRandomInjury(InjurySeverity.Moderate);
InjurySystem.ApplyInjury(injury.Id, "contextual_danger");
```

#### GetBriefNarrative(string injuryId)
Returns the short narrative form for UI display.

```csharp
var brief = InjurySystem.GetBriefNarrative("gut_wound");
// Returns: "Gut wound"
```

---

## Implementation Notes

### Initialization

Injury definitions are loaded once at game start:

```csharp
// src/Mod.Entry/SubModule.cs
protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
{
    if (game.GameType is Campaign)
    {
        // ... other initialization
        EventCatalog.Initialize();
        InjurySystem.Initialize(); // Load injuries.json
    }
}
```

### File Location

**Definition File:**
```
ModuleData/Enlisted/Content/injuries.json
```

**Implementation:**
```
src/Features/Content/InjurySystem.cs
```

### Error Handling

- Missing `injuries.json` → Warning logged, system disabled
- Invalid injury ID → Warning logged, no injury applied
- JSON parse errors → Error logged with details
- HP calculation always leaves minimum 1 HP (won't kill player)

### Save Compatibility

Injury system is stateless (no save data). Only immediate HP changes are persisted through Bannerlord's native save system.

**Future Enhancement:** Recovery tracking would require save definer registration.

### Performance

- Definitions loaded once at startup (not per-injury)
- Dictionary lookup for O(1) injury retrieval
- Minimal overhead (single calculation, one logging call)

---

## Usage Examples

### In Order Outcomes

```json
{
  "critical_failure": {
    "reputation": { "officer": -15 },
    "company_needs": { "Readiness": -15 },
    "injury_type": "broken_leg",
    "text": "The fall from the watchtower breaks your leg. You'll be off duty for weeks."
  }
}
```

### In Event Options

```json
{
  "id": "climb_wall",
  "text": "Scale the wall yourself",
  "skill_check": { "Athletics": 60 },
  "failure": {
    "injury_type": "broken_arm",
    "text": "Your grip fails. You fall hard. Your arm bends the wrong way."
  }
}
```

### In Code

```csharp
// Combat consequence
if (playerTookDangerousRisk)
{
    var injury = InjurySystem.GetRandomInjury(InjurySeverity.Severe);
    InjurySystem.ApplyInjury(injury.Id, "reckless_combat");
}

// Training accident
if (trainingFailed && MBRandom.RandomFloat < 0.1f)
{
    InjurySystem.ApplyInjury("sprained_ankle", "training_mishap");
}
```

---

## Acceptance Criteria

- [x] 15 injury types defined with appropriate HP percentages
- [x] Multiple narrative variations per injury
- [x] Severity levels (Minor, Moderate, Serious, Severe, Critical)
- [x] Integration with OrderManager (prioritizes injury_type over hp_loss)
- [x] Random injury selection by severity
- [x] Brief narratives for UI display
- [x] Proper logging with context
- [x] Initialization at game start
- [x] Error handling for missing/invalid data
- [x] HP calculation scales with character level
- [x] Bannerlord RP flavor throughout
- [x] Save compatibility (stateless design)
