# Event System Schemas

**Summary:** Authoritative JSON schema definitions for events, decisions, orders, and camp routine configs. This document specifies the exact field names the parser expects. When in doubt, **this document is the source of truth**.

**Status:** ✅ Current  
**Last Updated:** 2026-01-03 (Bug fixes: EventSelector category filtering, Order context variant mutation)  
**Related Docs:** [Writing Style Guide](writing-style-guide.md), [Content System Architecture](content-system-architecture.md), [Camp Routine Schedule](../../Campaign/camp-routine-schedule-spec.md), [Content Index](content-index.md), [Quartermaster System](../Equipment/quartermaster-system.md)

---

## Critical Rules

1. **Root array must be named `"events"`** - Even for decisions, the parser expects the array to be called `"events"`, not `"decisions"`.

2. **Category determines content type and selection behavior:**
   - `"category": "decision"` → Camp Hub decisions (required for dec_* prefixed items), **excluded from EventSelector**
   - `"category": "onboarding"` → Explicit enlistment events (e.g., baggage stowage), **excluded from EventSelector**
   - Other categories → Automatic events (selected by EventSelector during event windows)
   - **Note:** "decision" and "onboarding" events are never randomly selected; they fire only when explicitly triggered by code

3. **ID prefixes determine delivery mechanism:**
   - `dec_*` → Player-initiated Camp Hub menu items
   - `player_*` → Player-initiated popup inquiries  
   - `decision_*` → Game-triggered popup inquiries
   - Other → Automatic events

4. **Rewards vs Effects:** 
   - `"rewards"` = Player gains for **sub-choices only** (reward_choices options)
   - `"effects"` = State changes and rewards for **main options** (reputation, escalation, hpChange, skillXp)
   - **⚠️ CRITICAL:** For main event options, use `effects.skillXp`. Only use `rewards.skillXp` in sub-choice options!

5. **Order Events Must Grant XP:**
   - All order event options **must** include `skillXp` in their `effects` object
   - Players expect XP for completing orders (rank progression depends on it)
   - Failed skill checks should still grant reduced XP (typically 50% of success)
   - Validation tool (`tools/events/validate_content.py`) warns if order events lack XP rewards

---

## File Structure

### Required Root Structure

```json
{
  "schemaVersion": 2,
  "category": "decision",
  "events": [
    { ... event/decision definitions ... }
  ]
}
```

### File Locations

| Content Type | Location | ID Prefix | Delivery |
|-------------|----------|-----------|----------|
| Camp Hub Decisions | `ModuleData/Enlisted/Decisions/decisions.json` | `dec_*` | Inline menu |
| Player Popup Events | `ModuleData/Enlisted/Events/events_player_decisions.json` | `player_*` | Popup inquiry |
| Game-Triggered Events | `ModuleData/Enlisted/Events/events_decisions.json` | `decision_*` | Popup inquiry |
| Automatic Events | `ModuleData/Enlisted/Events/events_*.json` | `evt_*` | Popup inquiry |
| Map Incidents | `ModuleData/Enlisted/Events/incidents_*.json` | `mi_*` | Popup inquiry |
| **Muster Menu Stages** | *See note below* | `evt_muster_*` | **GameMenu stage** |

**Note on Muster Events:** The `evt_muster_new_recruit` event is delivered as a GameMenu stage during the muster sequence rather than a popup inquiry. The JSON definition remains in `events_*.json` files, but `MusterMenuHandler` converts it to menu text instead of using `MultiSelectionInquiryData`. Effects still apply via `EventDeliveryManager.ApplyEffects()`. See [Muster Menu System](../Core/muster-menu-revamp.md).

---

## Event/Decision Definition

### Core Fields

```json
{
  "id": "dec_rest",
  "category": "decision",
  "severity": "normal",
  "titleId": "dec_rest_title",
  "title": "Rest",
  "setupId": "dec_rest_setup", 
  "setup": "Your legs ache from the march...",
  "requirements": { ... },
  "timing": { ... },
  "options": [ ... ]
}
```

| Field | JSON Name | Type | Required | Notes |
|-------|-----------|------|----------|-------|
| ID | `id` | string | ✅ | Must be unique. Prefix determines delivery. |
| Category | `category` | string | ✅ | See valid categories below. `"decision"` for Camp Hub. |
| Severity | `severity` | string | ❌ | News priority: `"normal"`, `"positive"`, `"attention"`, `"urgent"`, `"critical"` |
| Title ID | `titleId` | string | ✅* | XML localization key. *Either titleId OR title required (both recommended) |
| Title Fallback | `title` | string | ✅* | Fallback text if localization missing. *Either titleId OR title required |
| Setup ID | `setupId` | string | ✅* | XML localization key for description. *Either setupId OR setup required (both recommended) |
| Setup Fallback | `setup` | string | ✅* | Fallback text if localization missing. *Either setupId OR setup required |

### Valid Categories (enforced by validator)

**Verified from actual JSON files (26 total):**

| Category | Used In | Description |
|----------|---------|-------------|
| `camp_life` | Events | Daily camp activities and routines |
| `crisis` | Events | Urgent problems requiring immediate attention |
| `decision` | Decisions | Player choices with consequences |
| `discipline` | Orders | Behavioral issues and corrections |
| `discovery` | Events | Learning and skill development |
| `economic` | Events | Financial and resource management |
| `emergency_drill` | Orders | Combat readiness training |
| `extended_rest` | Orders | Recovery and recuperation |
| `foraging` | Orders | Resource gathering activities |
| `formation` | Orders | Formation selection and tactics |
| `light_duty` | Orders | Reduced work assignments |
| `map_incident` | Events | Random encounters during travel |
| `medical` | Events | Illness, injury, and treatment |
| `onboarding` | Events | New player introduction and tutorials |
| `order_event` | Events | Order-triggered events |
| `patrol` | Orders | Patrol and reconnaissance missions |
| `pay` | Events | Wage, payment, and compensation |
| `problems` | Events | General problems and complications |
| `promotion` | Events | Tier advancement and recognition |
| `recovery` | Orders | Healing and rehabilitation |
| `retinue` | Events | Companion and follower events |
| `social` | Events | Social interactions and relationships |
| `special` | Events | Special circumstances and unique events |
| `threshold` | Events | Escalation threshold triggers |
| `training` | Orders | Skill training and practice |
| `work` | Orders | Work assignments and labor |

**Invalid category = validation ERROR (blocks commit).**

### Valid Severities

**Verified from actual JSON files (5 total):**

| Severity | Used For |
|----------|----------|
| `normal` | Standard priority event |
| `attention` | Requires player attention |
| `critical` | High priority, serious consequences |
| `serious` | Major problem or situation |
| `moderate` | Medium severity |

**Invalid severity = validation ERROR (blocks commit).**

---

## Triggers Object

**CRITICAL:** The `triggers` object controls **when** an event can fire through flag checks and escalation thresholds. This is **separate from** the `requirements` object which controls player eligibility.

⚠️ **Common Bug:** Events with `triggers` but missing validation will fire incorrectly. The system now validates all trigger conditions before event selection.

```json
{
  "triggers": {
    "all": ["is_enlisted", "has_flag:mutiny_joined"],
    "any": [],
    "none": ["has_flag:mutiny_failed"],
    "time_of_day": ["night"],
    "escalation_requirements": {
      "pay_tension_min": 85,
      "scrutiny": 5
    }
  }
}
```

### Trigger Fields

| Field | JSON Name | Type | Purpose |
|-------|-----------|------|---------|
| All Conditions | `all` | array | ALL must be true for event to fire |
| Any Conditions | `any` | array | At least ONE must be true |
| None Conditions | `none` | array | NONE can be true (blocking conditions) |
| Time of Day | `time_of_day` | array | Time blocks when eligible |
| Escalation Requirements | `escalation_requirements` | object | Minimum escalation track values |

### Trigger Condition Strings

**Flag Checks:**
```json
"all": [
  "has_flag:mutiny_joined",        // Player has this flag
  "flag:completed_onboarding"      // Same as has_flag:
]
```

**Generic Conditions:**
```json
"all": [
  "is_enlisted",                   // Player is currently enlisted
  "LeavingBattle"                  // Map incident context (battle ended)
]
```

**Medical Conditions:**
```json
"all": [
  "has_any_condition",             // Player has active injury or illness
  "has_untreated_injury"           // Player has injury without treatment
]
```

**Time of Day:**
```json
"time_of_day": ["night", "evening"]  // Only fires during these times
```

### Escalation Requirements

Minimum values for escalation tracks. Event only eligible when threshold reached:

```json
"escalation_requirements": {
  "pay_tension_min": 85,           // Pay tension must be 85+
  "scrutiny": 5,                   // Scrutiny must be 5+
  "discipline": 3,                 // Discipline must be 3+
  "medical_risk": 2                // Medical risk must be 2+
}
```

**Supported Tracks:**
- `pay_tension_min` / `pay_tension` - Pay tension (0-100)
- `scrutiny` - Crime suspicion (0-10)
- `discipline` - Rule-breaking record (0-10)
- `medical_risk` / `medicalrisk` - Health status (0-5)
- `soldierreputation` / `soldier_reputation` - Soldier rep threshold
- `officerreputation` / `officer_reputation` - Officer rep threshold
- `lordreputation` / `lord_reputation` - Lord rep threshold

### Triggers vs Requirements

| Object | Purpose | Validated By | When Checked |
|--------|---------|--------------|--------------|
| **triggers** | Controls when event CAN fire (context, flags, escalation) | EventSelector, MapIncidentManager | Before event enters selection pool |
| **requirements** | Controls player eligibility (tier, role, skills) | EventRequirementChecker | During selection filtering |

**Example: Mutiny Event**
```json
{
  "triggers": {
    "all": ["has_flag:mutiny_joined"],        // Only fires if joined mutiny
    "escalation_requirements": {}
  },
  "requirements": {
    "tier": { "min": 1, "max": 9 }            // Any tier can participate
  }
}
```

### Common Trigger Patterns

**Flag-Gated Chain Events:**
```json
"triggers": {
  "all": ["is_enlisted", "has_flag:event_completed"]
}
```

**High Escalation Events:**
```json
"triggers": {
  "all": ["is_enlisted"],
  "escalation_requirements": {
    "pay_tension_min": 85
  }
}
```

**Time-Specific Events:**
```json
"triggers": {
  "all": ["is_enlisted"],
  "time_of_day": ["night", "evening"]
}
```

---

## Requirements Object

All fields are optional. If omitted, no restriction applies.

### JSON Field Names (Parser Accepts Both)

```json
{
  "requirements": {
    "tier": { "min": 1, "max": 9 },
    "context": "Any",
    "role": "Any",
    "hp_below": 80,
    "minSkills": { "Scouting": 50 },
    "minTraits": { "Honor": 3 },
    "minEscalation": { "Scrutiny": 5 }
  }
}
```

| Field | JSON Names | Type | Range | Notes |
|-------|------------|------|-------|-------|
| Tier Range | `tier.min`, `tier.max` or `minTier`, `maxTier` | int | 1-9 | Enlistment tier |
| Context | `context` | string or array | See list | Campaign context(s) |
| Role | `role` | string | See list | Player's primary role |
| HP Below | `hp_below` or `hpBelow` | int | 0-100 | % HP threshold |
| Min Skills | `minSkills` | dict | - | Skill name → level |
| Min Traits | `minTraits` | dict | - | Trait name → level |
| Min Escalation | `minEscalation` | dict | - | Track → threshold |
| Onboarding Stage | `onboarding_stage` | int | 1-3 | Onboarding events only |
| Onboarding Track | `onboarding_track` | string | - | green/seasoned/veteran |
| Not At Sea | `notAtSea` | bool | - | Only appears on land (not at sea) |
| At Sea | `atSea` | bool | - | Only appears at sea (not on land) |
| Has Condition | `hasAnyCondition` | bool | - | Requires active injury or illness |
| Has Severe Condition | `hasSevereCondition` | bool | - | Requires Severe/Critical condition |
| Max Illness | `maxIllness` | string | See list | Max illness severity allowed |

**Max Illness Values:**
- `"None"` - Only if completely healthy (no illness)
- `"Mild"` - Allows Mild only, blocks Moderate+
- `"Moderate"` - Allows Mild/Moderate, blocks Severe+
- `"Severe"` - Allows Mild/Moderate/Severe, blocks Critical
- Omitted - No restriction (always available)

**Example:**
```json
{
  "id": "dec_training_drill",
  "requirements": {
    "tier": { "min": 1 },
    "maxIllness": "Mild"
  }
}
```

### Valid Context Values

**General Event Contexts:**
`"Any"`, `"War"`, `"Peace"`, `"Siege"`, `"Battle"`, `"Town"`, `"Camp"`

**Map Incident Contexts:**
`"leaving_battle"`, `"during_siege"`, `"entering_town"`, `"entering_village"`, `"leaving_settlement"`, `"waiting_in_settlement"`

Map incident contexts are used by `MapIncidentManager` to filter incidents based on player actions (battle end, settlement entry/exit, hourly checks during garrison/siege).

**Context as Array:**
```json
{
  "requirements": {
    "context": ["Camp", "Town"]  // Eligible in EITHER camp or town
  }
}
```

---

### Maritime Context (At Sea / On Land)

Events and decisions can be restricted to maritime (sea travel) or land-based contexts using the `atSea` and `notAtSea` requirement fields. This enables context-appropriate content when the player's party is traveling by ship.

**Requirements:**
```json
{
  "requirements": {
    "atSea": true        // Only available during sea travel
  }
}
```

```json
{
  "requirements": {
    "notAtSea": true     // Only available on land (not during sea travel)
  }
}
```

**Trigger Conditions:**

In the `triggers.all` or `triggers.any` arrays, you can also use string conditions:
```json
{
  "triggers": {
    "all": ["is_enlisted", "at_sea"]    // Fires only at sea
  }
}
```

```json
{
  "triggers": {
    "all": ["is_enlisted", "not_at_sea"] // Fires only on land
  }
}
```

**Illness-Type Conditions:**

For events that should match the illness type (not current location), use:
```json
{
  "triggers": {
    "all": ["has_untreated_condition", "has_maritime_illness"]  // Ship fever or scurvy
  }
}
```

```json
{
  "triggers": {
    "all": ["has_untreated_condition", "not_maritime_illness"]  // Land illness or injury
  }
}
```

| Condition | Description |
|-----------|-------------|
| `has_maritime_illness` | Player has ship_fever or scurvy |
| `has_land_illness` | Player has camp_fever or flux |
| `not_maritime_illness` | Player does NOT have ship_fever/scurvy (includes injuries, land illnesses, or healthy) |

This enables worsening events to show nautical flavor text when the player has ship fever, even if they've already landed.

**Maritime Event Naming Convention:**

Events with maritime variants use the `_sea` suffix:
- `illness_onset_minor` → `illness_onset_minor_sea`
- `dec_medical_surgeon` → `dec_medical_surgeon_sea`
- `untreated_condition_worsening` → `untreated_condition_worsening_sea`

The Content Orchestrator automatically selects context-appropriate variants when queuing events. If a sea variant doesn't exist, it falls back to the base event.

**Maritime Illness Types:**

When at sea, the medical system applies maritime-appropriate illnesses:
| Context | Mild/Moderate | Severe/Critical |
|---------|---------------|-----------------|
| At Sea | Ship Fever | Scurvy |
| On Land | Camp Fever | Flux |

**Example - Maritime Medical Decision:**
```json
{
  "id": "dec_medical_surgeon_sea",
  "category": "decision",
  "title": "Ship's Surgeon",
  "setup": "The ship's surgeon works in a cramped space near the waterline...",
  "requirements": {
    "tier": { "min": 1, "max": 999 },
    "hasAnyCondition": true,
    "atSea": true
  },
  "options": [
    {
      "id": "surgeon_treat_sea",
      "text": "\"I need your help, surgeon.\"",
      "effects_success": { "hpChange": 20, "medicalRisk": -5, "beginTreatment": true },
      "resultText": "He examines you by lantern-light, steady hands despite the ship's roll..."
    }
  ]
}
```

**Implementation:**
- `WorldStateAnalyzer.DetectTravelContext()` uses native `party.IsCurrentlyAtSea` property
- `EventRequirementChecker` validates `atSea`/`notAtSea` requirements
- `DecisionManager` filters decisions based on sea context
- `ContentOrchestrator.CheckIllnessOnsetTriggers()` selects `_sea` event variants when at sea
- `EventDeliveryManager.ApplyIllnessOnset()` applies context-appropriate illness types

---

### Context Mapping (Orchestrator Integration)

The Content Orchestrator uses internal world state keys for frequency calculation but maps them to event context strings for filtering. This ensures events fire with appropriate frequency AND appear in appropriate situations.

**Two-Tier System:**

1. **Internal World State** (Orchestrator frequency lookup)
   - Keys: `peacetime_garrison`, `war_marching`, `siege_attacking`, etc.
   - Used in: `enlisted_config.json` → `orchestrator.frequency` table
   
2. **Event Context** (Event filtering)
   - Values: `Camp`, `War`, `Siege`, `Battle`, `Town`, `Peace`, `Any`
   - Used in: Event JSON → `requirements.context`

**Mapping Table:**

| Orchestrator State | Event Context | Used For |
|--------------------|---------------|----------|
| `peacetime_garrison` | `Camp` | Garrison at settlement, no war |
| `peacetime_recruiting` | `Camp` | Garrison during peacetime patrol |
| `war_marching` | `War` | Marching to war with army |
| `war_active_campaign` | `War` | Active campaign operations |
| `siege_attacking` | `Siege` | Participating in siege assault |
| `siege_defending` | `Siege` | Defending settlement during siege |
| `lord_captured` | `Camp` | Recovery period after lord defeated |

**Implementation Example:**
```csharp
// In WorldStateAnalyzer.cs
public static string GetEventContext(WorldSituation situation)
{
    return situation.LordSituation switch
    {
        LordSituation.InGarrison => "Camp",
        LordSituation.InSiege => "Siege",
        LordSituation.InCampaign => "War",
        LordSituation.Defeated => "Camp",  // Recovery counts as garrison
        _ => "Any"
    };
}
```

**Usage in Event Selection:**
```csharp
var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
var eventContext = WorldStateAnalyzer.GetEventContext(worldSituation);

// EventRequirementChecker filters events by context
var eligible = events.Where(e => 
    e.Requirements.Context == "Any" || 
    e.Requirements.Context.Contains(eventContext)
);
```

**See Also:** [Content System Architecture](content-system-architecture.md) for world state analysis specification.

---

### Requirements: Context vs World State

**IMPORTANT:** There are TWO different requirement fields for situational filtering:

**1. `requirements.context`** - For narrative events (Camp Hub decisions, automatic events)
- **Uses:** `"Any"`, `"War"`, `"Peace"`, `"Siege"`, `"Camp"`, `"Battle"`, `"Town"`
- **Mapped from:** `WorldStateAnalyzer.GetEventContext()`
- **Purpose:** Simple context filtering for narrative content

**2. `requirements.world_state`** - For order events only (during duty execution)
- **Uses:** `"peacetime_garrison"`, `"war_marching"`, `"war_active_campaign"`, `"siege_attacking"`, `"siege_defending"`, etc.
- **Mapped from:** `WorldStateAnalyzer.GetOrderEventWorldState()`
- **Purpose:** Granular state weighting for in-order events

**Example - Narrative Event:**
```json
{
  "id": "dec_rest_garrison",
  "category": "decision",
  "requirements": {
    "context": ["Camp"]  // ← Simplified context for Camp Hub
  }
}
```

**Example - Order Event:**
```json
{
  "id": "scout_enemy_patrol",
  "order_type": "order_scout_route",
  "requirements": {
    "world_state": ["war_marching", "war_active_campaign", "siege_attacking"]  // ← Granular orchestrator state
  }
}
```

**Why Two Systems?**
- **Narrative events** need simple filtering (Camp/War/Siege)
- **Order events** need granular weighting (peacetime vs active war vs desperate siege)
- Both are driven by the same underlying `WorldSituation` analysis

**See:** [Order Progression System](../Core/order-progression-system.md) for execution flow, `ModuleData/Enlisted/Orders/order_events/*.json` for 84 event definitions.

---

### Valid Role Values  
`"Any"`, `"Soldier"`, `"Scout"`, `"Medic"`, `"Engineer"`, `"Officer"`, `"Operative"`, `"NCO"`

### Sea/Land Context Requirements

Events can be restricted to land or sea travel:

```json
{
  "requirements": {
    "notAtSea": true  // Only fires on land
  }
}
```

```json
{
  "requirements": {
    "atSea": true  // Only fires at sea
  }
}
```

**Use Cases:**
- Baggage wagon events: `"notAtSea": true` (wagons don't exist at sea)
- Ship-based events: `"atSea": true` (cargo hold access, sea sickness, etc.)
- Camp opportunities: Land activities vs maritime activities

**Detection:** Uses `MobileParty.IsCurrentlyAtSea` to determine if the enlisted lord's party is on water.

**Validation:** If both are true or both are false, no filtering occurs (event eligible in all locations).

---

## Content Variants Pattern

**Purpose:** Provide contextual variety by creating multiple versions of the same decision/event that fire in different situations.

### How Variants Work

The system automatically selects the best-fitting variant based on `requirements.context`:

1. Player enters situation (e.g., garrison, siege, campaign)
2. Orchestrator queries eligible content
3. EventRequirementChecker filters by context
4. Only matching variants pass filter
5. Orchestrator scores and selects best fit

**No code changes needed** - variants filter themselves via requirements.

### Variant Naming Convention

```
<base_id>                → Base version (any context)
<base_id>_<context>      → Context-specific variant
<base_id>_<intensity>    → Intensity variant (light/intense)
```

**Examples:**
- `dec_rest` → Base
- `dec_rest_garrison` → Garrison-specific
- `dec_rest_exhausted` → Siege-specific
- `dec_weapon_drill` → Base
- `dec_weapon_drill_light` → Low-intensity
- `dec_weapon_drill_intense` → High-intensity

### Basic Variant Example

```json
{
  "schemaVersion": 2,
  "category": "decision",
  "events": [
    {
      "id": "dec_rest",
      "category": "decision",
      "title": "Rest",
      "setup": "You need to rest.",
      "requirements": {
        "tier": { "min": 1 }
      },
      "options": [
        {
          "id": "rest",
          "text": "Find a spot and rest.",
          "costs": { "fatigue": 2 },
          "rewards": { "fatigueRelief": 5 }
        }
      ]
    },
    {
      "id": "dec_rest_garrison",
      "category": "decision",
      "title": "Rest (Garrison)",
      "setup": "You have time for proper rest.",
      "requirements": {
        "tier": { "min": 1 },
        "context": ["Camp"]  // ← Only in garrison
      },
      "options": [
        {
          "id": "rest_proper",
          "text": "Get proper rest in camp.",
          "costs": { "fatigue": 1 },  // ← Cheaper
          "rewards": { "fatigueRelief": 8 }  // ← More effective
        }
      ]
    },
    {
      "id": "dec_rest_exhausted",
      "category": "decision",
      "title": "Rest (Exhausted)",
      "setup": "You try to rest amid chaos.",
      "requirements": {
        "tier": { "min": 1 },
        "context": ["Siege", "Battle"]  // ← Only during crisis
      },
      "options": [
        {
          "id": "rest_crisis",
          "text": "Catch what rest you can.",
          "costs": { "fatigue": 2 },
          "rewards": { "fatigueRelief": 2 }  // ← Less effective
        }
      ]
    }
  ]
}
```

### Multi-Intensity Variant Example

```json
{
  "schemaVersion": 2,
  "category": "decision",
  "events": [
    {
      "id": "dec_weapon_drill",
      "category": "decision",
      "title": "Weapon Training",
      "requirements": { "tier": { "min": 1 } },
      "costs": { "fatigue": 2 },
      "effects": { "skillXp": { "OneHanded": 25 } }
    },
    {
      "id": "dec_weapon_drill_light",
      "category": "decision",
      "title": "Light Weapon Drill",
      "requirements": { 
        "tier": { "min": 1 },
        "context": ["Camp", "Town"]  // ← Garrison contexts
      },
      "costs": { "fatigue": 1 },  // ← Cheaper
      "effects": { "skillXp": { "OneHanded": 15 } }  // ← Less XP
    },
    {
      "id": "dec_weapon_drill_intense",
      "category": "decision",
      "title": "Intense Weapon Drill",
      "requirements": { 
        "tier": { "min": 3 },  // ← Higher tier required
        "context": ["War"]  // ← Campaign context
      },
      "costs": { "fatigue": 5 },  // ← More expensive
      "effects": { 
        "skillXp": { "OneHanded": 60 },  // ← Much more XP
        "hpChange": -5  // ← Risk of injury
      }
    }
  ]
}
```

### Tier + Context Variant Example

```json
{
  "schemaVersion": 2,
  "events": [
    {
      "id": "evt_patrol",
      "title": "Patrol Duty",
      "requirements": { "tier": { "min": 1, "max": 3 } },
      "setup": "Standard patrol assignment."
    },
    {
      "id": "evt_patrol_veteran",
      "title": "Veteran Patrol",
      "requirements": { 
        "tier": { "min": 4 },
        "context": ["War"]
      },
      "setup": "You lead a patrol in enemy territory."
    },
    {
      "id": "evt_patrol_garrison",
      "title": "Garrison Patrol",
      "requirements": { 
        "tier": { "min": 1, "max": 3 },
        "context": ["Camp", "Town"]
      },
      "setup": "Routine patrol around camp."
    }
  ]
}
```

### Role + Context Variant Example

```json
{
  "schemaVersion": 2,
  "events": [
    {
      "id": "evt_scout_mission",
      "title": "Scout Mission",
      "requirements": { 
        "role": "Scout",
        "tier": { "min": 2 }
      }
    },
    {
      "id": "evt_scout_mission_dangerous",
      "title": "Deep Reconnaissance",
      "requirements": { 
        "role": "Scout",
        "tier": { "min": 4 },
        "context": ["War"]  // ← Only during war
      }
    }
  ]
}
```

### Variant Selection Behavior

**Scenario 1: Garrison (Camp context)**
```
Eligible content:
  dec_rest           ✓ (context: Any)
  dec_rest_garrison  ✓ (context: ["Camp"])
  dec_rest_exhausted ✗ (context: ["Siege", "Battle"])

Orchestrator scores both eligible variants
Selects: dec_rest_garrison (better fit for garrison)
Player sees: Garrison-specific rest option
```

**Scenario 2: Siege Defense (Siege context)**
```
Eligible content:
  dec_rest           ✓ (context: Any)
  dec_rest_garrison  ✗ (context: ["Camp"])
  dec_rest_exhausted ✓ (context: ["Siege", "Battle"])

Orchestrator scores both eligible variants
Selects: dec_rest_exhausted (crisis-appropriate)
Player sees: Exhausted rest option
```

**Scenario 3: Campaign March (War context)**
```
Eligible content:
  dec_rest           ✓ (context: Any)
  dec_rest_garrison  ✗ (context: ["Camp"])
  dec_rest_exhausted ✗ (context: ["Siege", "Battle"])

Only base version eligible
Selects: dec_rest (fallback to base)
Player sees: Normal rest option
```

### Variant Best Practices

**DO:**
- Create base version with `"context": "Any"` as fallback
- Use specific contexts for variants (`["Camp"]`, `["Siege"]`)
- Vary costs/rewards to reflect context appropriately
- Keep variant count reasonable (2-3 per base event)
- Name variants descriptively (`_garrison`, `_exhausted`, `_intense`)

**DON'T:**
- Create variants without a base version (always have fallback)
- Overlap contexts too much (causes selection confusion)
- Make all events have variants (add incrementally)
- Change event logic between variants (keep structure similar)

### Implementation Timeline

**Phase 1-5 (Weeks 1-5):** Orchestrator built with current content (no variants)  
**Phase 6+ (Week 6+):** Add variants incrementally as JSON additions

**Priority for Variants:**
1. High-traffic decisions (training, rest, social)
2. Repetitive events (seen too often in playtesting)
3. Role-specific content (add depth)
4. Tier-specific content (progression feel)

**See:** [Content System Architecture](content-system-architecture.md) for variant strategy implementation.

---

## Timing Object

```json
{
  "timing": {
    "cooldown_days": 3,
    "priority": "normal",
    "one_time": false
  }
}
```

| Field | JSON Names | Type | Default | Notes |
|-------|------------|------|---------|-------|
| Cooldown | `cooldown_days` or `cooldownDays` | int | 7 | Days before re-available |
| Priority | `priority` | string | `"normal"` | low/normal/high/critical |
| One-Time | `one_time` or `oneTime` | bool | false | Fire once per playthrough |

---

## Global Event Pacing (enlisted_config.json)

**Location:** `enlisted_config.json` → `decision_events.pacing`

All automatic event timing is config-driven. These limits apply across ALL automatic event sources (EventPacingManager + MapIncidentManager) to ensure players aren't overwhelmed with events.

### Current Config Structure

```json
{
  "decision_events": {
    "pacing": {
      "max_per_day": 2,
      "max_per_week": 8,
      "min_hours_between": 6,
      "event_window_min_days": 3,
      "event_window_max_days": 5,
      "per_event_cooldown_days": 7,
      "per_category_cooldown_days": 1,
      "evaluation_hours": [8, 14, 20],
      "allow_quiet_days": true,
      "quiet_day_chance": 0.15
    }
  }
}
```

### Pacing Fields (Current)

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `max_per_day` | int | 2 | Maximum automatic events per day across ALL systems |
| `max_per_week` | int | 8 | Maximum automatic events per week |
| `min_hours_between` | int | 6 | Minimum hours between any automatic events |
| `event_window_min_days` | int | 3 | Minimum days between narrative event windows |
| `event_window_max_days` | int | 5 | Maximum days between narrative event windows |
| `per_event_cooldown_days` | int | 7 | Default cooldown before same event can fire again |
| `per_category_cooldown_days` | int | 1 | Cooldown between events of same category (narrative vs map_incident) |
| `evaluation_hours` | array | [8,14,20] | Campaign hours when narrative events can fire (map incidents skip this check) |
| `allow_quiet_days` | bool | true | Whether random quiet days can occur |
| `quiet_day_chance` | float | 0.15 | Chance of no automatic events on a given day (rolled once daily) |

### How Pacing Works (Current)

1. **EventPacingManager** checks if current time is within event window (every 3-5 days, config-driven)
2. **MapIncidentManager** fires context incidents on battles, settlements, sieges
3. **GlobalEventPacer** checks ALL limits before any automatic event fires:
   - `evaluation_hours`: Is current hour in allowed list?
   - `max_per_day` / `max_per_week`: Daily/weekly limits reached?
   - `min_hours_between`: Enough time since last event?
   - `per_category_cooldown_days`: Same category fired too recently?
   - `quiet_day_chance`: Is today a quiet day? (rolled once when first event is attempted)
4. Events blocked by any check wait until the condition clears

### Categories

| Category | Source | Evaluation Hours | Purpose |
|----------|--------|------------------|---------|
| `narrative` | EventPacingManager | ✅ Enforced | Paced narrative events (every 3-5 days) |
| `map_incident` | MapIncidentManager | ❌ Skipped | Context-triggered incidents fire immediately |

Map incidents skip `evaluation_hours` check because they're context-triggered (battle just ended, you entered a settlement). It would feel odd to delay the reaction.

Per-category cooldown ensures you don't get a narrative event and a map incident back-to-back.

**Player-selected decisions** (from Camp Hub menu) bypass all pacing since the player explicitly chose them.

---

## Future: Orchestrator Config Structure

**Status:** 📋 Specification - See [Content Orchestrator Plan](../../Features/Content/content-orchestrator-plan.md)

The orchestrator will replace schedule-driven config with world-state driven frequency tables:

### Future Config Structure

```json
{
  "decision_events": {
    "pacing": {
      "max_per_day": 2,
      "max_per_week": 8,
      "min_hours_between": 4,
      "per_event_cooldown_days": 7,
      "per_category_cooldown_days": 1
    },
    "orchestrator": {
      "enabled": true,
      "fitness_threshold": 40,
      "log_decisions": true,
      
      "frequency": {
        "peacetime_garrison": 0.14,      // 1 event per week
        "peacetime_recruiting": 0.35,    // 2.5 per week
        "war_marching": 0.5,             // 3.5 per week
        "war_active_campaign": 0.7,      // 5 per week
        "siege_attacking": 0.57,         // 4 per week
        "siege_defending": 1.0,          // 7 per week
        "lord_captured": 0.07            // 0.5 per week
      },
      
      "dampening": {
        "after_busy_week_multiplier": 0.7,
        "after_quiet_week_multiplier": 1.2,
        "after_battle_cooldown_days": 1.5
      },
      
      "pressure_modifiers": {
        "low_supplies": 0.1,
        "wounded_company": 0.1,
        "high_discipline": 0.15,
        "recent_victory": -0.2,
        "just_paid": -0.15
      }
    }
  }
}
```

### Changes from Current

**Removed Fields:**
- `event_window_min_days`, `event_window_max_days` - Schedule-driven pacing
- `evaluation_hours` - Artificial "event times"
- `allow_quiet_days`, `quiet_day_chance` - Random rolls replaced by world-state determination

**Kept Fields (Safety Limits):**
- `max_per_day`, `max_per_week` - Prevent spam
- `min_hours_between` - Minimum spacing
- `per_event_cooldown_days` - Prevent repetition
- `per_category_cooldown_days` - Prevent category spam

**Added Fields:**
- `orchestrator.frequency` - World situation → realistic event frequency mappings
- `orchestrator.dampening` - Activity-based frequency adjustments
- `orchestrator.pressure_modifiers` - Simulation pressure effects on frequency

### Frequency Table Rationale

| Situation | Events/Week | Daily Chance | Why |
|-----------|-------------|--------------|-----|
| Peacetime Garrison | 1.0 | 14% | Boring is realistic. Garrison duty is routine. |
| War Marching | 3.5 | 50% | Normal campaign tempo. |
| Active Campaign | 5.0 | 70% | High activity during active operations. |
| Siege Defense | 7.0 | 100% | Crisis situation. Something every day. |

**Philosophy:** The world determines realism. Garrison should feel quiet. Sieges should feel chaotic. The simulation doesn't manufacture pacing—it reflects reality.

**Grace Period:** 3-day grace period after enlistment (unchanged)

**See:** [Content Orchestrator Plan](content-orchestrator-plan.md) for complete specification and implementation timeline.

---

## Options Array

Each option represents a player choice.

```json
{
  "options": [
    {
      "id": "rest_short",
      "textId": "dec_rest_short",
      "text": "Find a shady spot and rest.",
      "costs": { ... },
      "rewards": { ... },
      "effects": { ... },
      "resultTextId": "dec_rest_short_result",
      "resultText": "You feel refreshed."
    }
  ]
}
```

| Field | JSON Name | Type | Required | Notes |
|-------|-----------|------|----------|-------|
| ID | `id` | string | ✅ | Unique within event |
| Text ID | `textId` | string | ✅* | XML localization key. *Either textId OR text required (both recommended) |
| Text Fallback | `text` | string | ✅* | Button text. *Either textId OR text required |
| Tooltip | `tooltip` | string | ✅* | Static tooltip. Required if no `tooltipTemplate` |
| Tooltip Template | `tooltipTemplate` | string | ✅* | Dynamic tooltip with `{CHANCE}`, `{SKILL}`, `{SKILL_NAME}` |
| Costs | `costs` | object | ❌ | Resources deducted |
| Rewards | `rewards` | object | ❌ | Resources gained |
| Effects | `effects` | object | ❌ | State changes |
| Result Text ID | `resultTextId` | string | ❌ | XML key for outcome narrative |
| Result Text | `resultText` | string or array | ❌ | Outcome narrative (string or array for random selection) |
| Risk | `risk` | string | ❌ | safe/moderate/risky/dangerous |
| Risk Chance | `risk_chance` or `riskChance` | int | ❌ | Base success % (0-100), modified by skill |
| Skill Check | `skillCheck` | string | ❌ | Skill that modifies riskChance (e.g., "Scouting") |
| Skill Base | `skillBase` | int | ❌ | Skill level where riskChance applies (default 50) |
| Effects Failure | `effectsFailure` | object | ❌ | Effects applied on failed skill check |
| Set Flags | `set_flags` or `setFlags` | array | ❌ | Flags to set |
| Clear Flags | `clear_flags` or `clearFlags` | array | ❌ | Flags to clear |
| Chains To | `chains_to` or `chainsTo` | string | ❌ | Follow-up event ID |
| Chain Delay | `chain_delay_hours` or `chainDelayHours` | int | ❌ | Hours before chain fires |

*Either `tooltip` OR `tooltipTemplate` is required (one must be present, cannot both be null).

---

## Tooltip Guidelines

**Tooltips cannot be null.** Every option must have either `tooltip` (static) or `tooltipTemplate` (dynamic).

### Dynamic Tooltips (Skill Checks)

For options with `skillCheck`, use `tooltipTemplate` with placeholders:

```json
{
  "riskChance": 50,
  "skillCheck": "Scouting",
  "skillBase": 50,
  "tooltipTemplate": "{CHANCE}% (Scouting {SKILL}). +15 Supplies. Fail: -5 Supplies."
}
```

**Placeholders:**
- `{CHANCE}` - Calculated success chance based on player skill
- `{SKILL}` - Player's current skill value
- `{SKILL_NAME}` - Name of the skill being checked

**Calculation:** `actualChance = riskChance + (playerSkill - skillBase) × 0.5`, clamped 15-95%

**Runtime Display (player has Scouting 72):**
```
"61% (Scouting 72). +15 Supplies. Fail: -5 Supplies."
```

### Static Tooltips (No Skill Check)

For guaranteed options or non-skill-based outcomes, use `tooltip`:

```json
// Simple actions
{"tooltip": "Trains equipped weapon. +3 One-Handed XP."}
{"tooltip": "Build stamina and footwork. +3 Athletics XP."}

// Guaranteed outcomes
{"tooltip": "Guaranteed. +25 Supplies. -3 Morale. +3 Scrutiny."}
{"tooltip": "No action taken."}

// Stat/reputation changes
{"tooltip": "Harsh welcome. +5 Officer rep. -3 Retinue Loyalty."}
{"tooltip": "Risky move. +10 Courage. -5 Discipline. Injury chance."}

// Consequences
{"tooltip": "Accept discharge. 90-day re-enlistment block applies."}
{"tooltip": "Desert immediately. Criminal record and faction hostility."}
```

**Format:** Action + effects + costs (under 100 characters)

---

## Result Text & Event Outcomes

**Purpose:** The `resultText` field provides narrative feedback after an event option is chosen.

**Display System:** Event outcomes are **NOT shown as popups** (the outcome popup system was removed as dead code). Instead, they appear in the **Recent Activities** section of the status menu using a queue system. **Decisions** (events with `category: "decision"`) also display their result text immediately in the combat log for instant feedback.

**What Still Uses Popups:**
- Initial event setup and option selection (unchanged)
- Multi-phase/chain events (each phase shows normally)
- `reward_choices` sub-popups (training focus, etc.) - These still work!
- Only the outcome narrative after choosing goes to Recent Activities (and combat log for decisions)

**Queue Behavior:**
- Only ONE event outcome displays at a time
- Each outcome shows for 1 day in Recent Activities
- Multiple events on the same day queue automatically (FIFO)
- Players check their status menu to see what happened

**Writing Guidelines:**
- Write immersive, present-tense narrative
- Show immediate consequences and atmosphere
- Keep it 1-3 sentences (under 200 characters)
- Use sensory details and character reactions
- Supports placeholder variables (e.g., `{PLAYER_NAME}`, `{SERGEANT}`)

**Examples:**

```json
{
  "resultText": "You wait on the bank with the others, watching the pioneers work. They know their business, but the current is strong and the footing treacherous. It will take time."
}

{
  "resultText": "You wade into the freezing water, gasping at the cold. Your boots fill instantly. Someone shoves a rope into your hands and you haul, muscles burning, until something finally gives."
}

{
  "resultText": "The dice tumble. You win the pot. {COMRADE_NAME} curses under his breath and walks away."
}
```

**Risky Options:** Use `failure_resultText` for the failure narrative when `risk` is set:

```json
{
  "resultText": "Your training pays off. The blow glances off harmlessly.",
  "failure_resultText": "You're too slow. Blood runs down your arm. {SERGEANT} calls for the medic."
}
```

**Display Format:**

*Combat Log (decisions only):*
```
You drive him back with a flurry of blows. He yields with a grudging nod. (green text)
```

*Recent Activities (all events):*
```
• Event Title: Result narrative text here
```

**Important:** The `resultText` field is required for all events and decisions. For regular events, it displays in Recent Activities. For decisions, it displays in both the combat log (immediate) and Recent Activities (persistent).

**See Also:** [News & Reporting System - Event Outcome Queue](../UI/news-reporting-system.md#event-outcome-queue-system)

### Result Text Variants (Array Format)

For replayability, `resultText` can be an array. The system picks randomly:

```json
{
  "resultText": [
    "A stray dog, hunting rats. You return to your post.",
    "Just the wind. You feel foolish but alert.",
    "A drunk soldier, relieving himself. He stumbles off.",
    "Nothing. Shadows and nerves. The night is long."
  ]
}
```

**Parser behavior:**
- If `resultText` is a string → Use directly
- If `resultText` is an array → Pick one randomly at display time

Same applies to `failure_resultText` for risky options:

```json
{
  "resultText": ["You made it.", "Close call, but you're through."],
  "failure_resultText": [
    "You trip on a root. {COMRADE_NAME} snorts. 'Graceful.'",
    "The 'shortcut' leads to a dead end. Everyone sighs.",
    "A chicken attacks you. The men will never let you forget this."
  ]
}
```

**Localization:** When using arrays, each variant should have its own `resultTextId` entry, or use a single ID with indexed variants in XML.

---

## Costs Object

Deducted when option is selected. Automatically displayed to player in yellow.

```json
{
  "costs": {
    "gold": 30,
    "fatigue": 2,
    "time_hours": 4
  }
}
// Player sees: "Cost: -30 gold, +2 fatigue, 4 hours"
```

| Field | JSON Names | Type | Notes |
|-------|------------|------|-------|
| Gold | `gold` | int | Deducted from player |
| Fatigue | `fatigue` | int | Added to fatigue |
| Time | `time_hours` or `timeHours` | int | Campaign hours |

---

## Rewards Object

**⚠️ ONLY USED IN SUB-CHOICE OPTIONS (reward_choices).** For main event options, use `effects` instead!

Gained when sub-choice option is selected. Used for positive gains in reward_choices blocks. Automatically displayed to player in cyan.

```json
{
  "reward_choices": {
    "type": "skill_focus",
    "prompt": "What do you focus on?",
    "options": [
      {
        "id": "focus_weapon",
        "text": "Weapon training",
        "rewards": {
          "skillXp": { "OneHanded": 50 }
        }
      }
    ]
  }
}
// Player sees: "+50 OneHanded XP"
```

| Field | JSON Names | Type | Notes |
|-------|------------|------|-------|
| Gold | `gold` | int | Added to player |
| Fatigue Relief | `fatigueRelief` or `fatigue_relief` | int | Reduces fatigue |
| Skill XP | `skillXp` | dict | Skill name → XP amount (SUB-CHOICES ONLY) |
| Dynamic Skill XP | `dynamicSkillXp` | dict | `"equipped_weapon"` or `"weakest_combat"` → XP (SUB-CHOICES ONLY) |

**⚠️ CRITICAL NOTES:** 
- Use `rewards` ONLY in sub-choice options inside `reward_choices` blocks
- For main event options, put `skillXp` in `effects` instead
- `fatigueRelief` always goes in `rewards` (or nowhere if not a sub-choice)

---

## Effects Object

State changes when option is selected. Can be positive or negative.

**⚠️ AUTOMATIC FEEDBACK:** All effects are automatically displayed to the player in the combat log with color coding. You don't need to add any special fields - just define the effects and the player will see them.

```json
{
  "effects": {
    "soldierRep": 5,
    "officerRep": -3,
    "lordRep": 2,
    "scrutiny": 3,
    "discipline": -2,
    "medicalRisk": -1,
    "hpChange": -10,
    "gold": -50,
    "troopXp": 20
  }
}
// Player sees: "+5 Soldier Reputation, -3 Officer Reputation, +2 Lord Reputation, -10 HP, -50 gold, +20 Troop XP"
```

| Field | JSON Names | Type | Range | Notes |
|-------|------------|------|-------|-------|
| Soldier Rep | `soldierRep` or `soldier_rep` | int | -50 to +50 | |
| Officer Rep | `officerRep` or `officer_rep` | int | -100 to +100 | |
| Lord Rep | `lordRep` or `lord_rep` | int | -100 to +100 | |
| Scrutiny | `scrutiny` | int | delta | Escalation track |
| Discipline | `discipline` | int | delta | Escalation track |
| Medical Risk | `medicalRisk` or `medical_risk` | int | delta | Escalation track |
| HP Change | `hpChange` or `hp_change` | int | delta | **NOT `hp`!** |
| Gold | `gold` | int | delta | Positive = gain |
| Troop Loss | `troopLoss` or `troop_loss` | int | count | Party troops killed |
| Troop Wounded | `troopWounded` or `troop_wounded` | int | count | Party troops wounded |
| Food Loss | `foodLoss` or `food_loss` | int | count | Food items removed |
| Troop XP | `troopXp` or `troop_xp` | int | amount | XP to lord's T1-T3 troops |
| Skill XP | `skillXp` | dict | - | Skill name → XP (USE THIS for main options!) |
| Dynamic Skill XP | `dynamicSkillXp` or `dynamic_skill_xp` | dict | - | `"equipped_weapon"` or `"weakest_combat"` → XP |
| Trait XP | `traitXp` | dict | - | Trait name → XP |
| Company Needs | `companyNeeds` | dict | - | Need name → delta |
| Apply Wound | `applyWound` or `apply_wound` | string | - | Minor/Serious/Permanent |
| Chain Event | `chainEventId` | string | - | Immediate follow-up |
| Discharge | `triggersDischarge` | string | - | dishonorable/washout/deserter |
| Renown | `renown` | int | delta | Clan renown |
| Illness Onset | `illnessOnset` or `illness_onset` | string | - | Apply illness: `"mild"`, `"moderate"`, `"severe"`, `"critical"` |
| Injury Onset | `injuryOnset` or `injury_onset` | string | - | Apply injury: `"minor"`, `"moderate"`, `"severe"`, `"critical"` |
| Begin Treatment | `beginTreatment` or `begin_treatment` | bool | - | Start medical care, apply recovery multiplier |
| Worsen Condition | `worsenCondition` or `worsen_condition` | bool | - | Increase severity by one level |
| **Promotion** | `promotes` | bool or int | - | **Grants tier promotion.** Use `true` for next tier, or specific tier number (e.g., `5` for tier 5). See promotion notes below. |
| Character Tag | `character_tag` | string | - | ⚠️ **Reserved for future use.** Apply personality tag (e.g., "disciplinarian", "merciful", "aggressive"). Currently in JSON but not implemented. |
| Loyalty Tag | `loyalty_tag` | string | - | ⚠️ **Reserved for future use.** Apply loyalty alignment (e.g., "lord_sworn", "realm_loyal", "lance_bound"). Currently in JSON but not implemented. |
| Retinue Gain | `retinueGain` or `retinue_gain` | int | - | Add soldiers to retinue (T7+ only) |
| Retinue Loss | `retinueLoss` or `retinue_loss` | int | - | Remove soldiers from retinue (T7+ only) |
| Retinue Wounded | `retinueWounded` or `retinue_wounded` | int | - | Wound soldiers in retinue (T7+ only) |
| Retinue Loyalty | `retinueLoyalty` or `retinue_loyalty` | int | - | Modify retinue loyalty (-100 to +100) |

**PROMOTION EFFECT (`promotes`):**
Used in proving events (`events_promotion.json`) to grant rank promotions:
- `"promotes": true` - Promotes player to next tier (T2→T3, T5→T6, etc.)
- `"promotes": 5` - Promotes player to specific tier (explicit tier 5)
- **Technical:** The value `true` is converted to `-1` internally (sentinel value) and resolved to `currentTier + 1` at runtime
- **Usage:** Typically all options in a proving event have `"promotes": true` to ensure promotion regardless of choice
- **Example:** See `events_promotion.json` for correct usage

**⚠️ COMMON MISTAKES:**
- Use `hpChange`, not `hp`
- Use `skillXp` in `effects` for main options, NOT in `rewards`
- Don't forget `"promotes": true` in all proving event options (or promotion won't happen!)

**✅ PLAYER FEEDBACK:**
- All effects, costs, and rewards are automatically shown to the player in combat log
- Effects = Green messages (e.g., `+5 Soldier Reputation, +25 XP`)
- Costs = Yellow messages (e.g., `Cost: -30 gold, +2 fatigue`)
- Rewards = Cyan messages (e.g., `+50 gold, -3 fatigue`)
- No special fields needed - just define the effects and they'll be displayed

---

## Company Needs Keys

For `effects.companyNeeds`:

```json
{
  "effects": {
    "companyNeeds": {
      "Readiness": -5,
      "Morale": 3,
      "Supplies": -10,
      "Equipment": 2,
      "Rest": 5
    }
  }
}
```

---

## Risky Options

Options with success/failure outcomes:

```json
{
  "id": "gamble_high",
  "text": "Bet big.",
  "risk": "risky",
  "risk_chance": 50,
  "effects": {
    "lordRep": 1
  },
  "effects_success": {
    "gold": 100
  },
  "effects_failure": {
    "gold": -100,
    "lordRep": -2
  },
  "resultText": "You win!",
  "failure_resultText": "You lose everything."
}
```

| Field | Notes |
|-------|-------|
| `effects` | Applied always (before roll) |
| `effects_success` | Applied on success |
| `effects_failure` | Applied on failure |
| `resultText` | Shown on success |
| `failure_resultText` | Shown on failure |

---

## Skill Names (Valid Values)

`OneHanded`, `TwoHanded`, `Polearm`, `Bow`, `Crossbow`, `Throwing`, `Riding`, `Athletics`, `Scouting`, `Tactics`, `Roguery`, `Charm`, `Leadership`, `Trade`, `Steward`, `Medicine`, `Engineering`

---

## Trait Names (Valid Values)

`Honor`, `Valor`, `Mercy`, `Generosity`, `Calculating`

---

## Escalation Track Names

For `requirements.minEscalation`, `triggers.escalation_requirements`, and `effects`:

**Active Tracks:**
- `scrutiny` - Trouble/suspicion level (0-100 scale, replaces old Discipline track)
- `medical_risk` / `medicalrisk` / `MedicalRisk` - Health status (0-5)
- `lordreputation` / `lord_reputation` / `LordReputation` - Lord rep (0-100, uses native Hero.GetRelation())

**Note:** Parser accepts multiple naming variants (camelCase, snake_case, PascalCase) for compatibility.

---

## Text Placeholder Variables

All text fields (`title`, `setup`, `text`, `resultText`, etc.) support placeholder variables that are replaced at runtime with actual game data.

### Common Placeholders

| Category | Variables | Notes |
|----------|-----------|-------|
| **Player** | `{PLAYER_NAME}`, `{PLAYER_RANK}` | Player's name and current rank |
| **NCO/Officers** | `{SERGEANT}`, `{SERGEANT_NAME}`, `{NCO_NAME}`, `{OFFICER_NAME}`, `{CAPTAIN_NAME}` | NCO and officer references |
| **Soldiers** | `{SOLDIER_NAME}`, `{COMRADE_NAME}`, `{VETERAN_1_NAME}`, `{VETERAN_2_NAME}`, `{RECRUIT_NAME}` | Fellow soldiers in your unit |
| **Lord/Faction** | `{LORD_NAME}`, `{LORD_TITLE}`, `{FACTION_NAME}`, `{KINGDOM_NAME}` | Your enlisted lord and faction |
| **Location** | `{SETTLEMENT_NAME}`, `{COMPANY_NAME}` | Current settlement and party name |
| **Rank** | `{NEXT_RANK}`, `{SECOND_RANK}` | Rank progression context |

**Example Usage:**

```json
{
  "setup": "{SERGEANT} pulls you aside. 'Listen, {PLAYER_NAME}, we need someone to scout ahead. {LORD_NAME} wants intel on {SETTLEMENT_NAME} before we march.'",
  "text": "Tell {SERGEANT_NAME} you'll do it.",
  "resultText": "You report back to {SERGEANT}. The information proves valuable."
}
```

All variables use fallback values if data is unavailable (e.g., "the Sergeant" if no NCO name is set). See [Writing Style Guide - Dynamic Tokens](writing-style-guide.md#dynamic-tokens) for the complete list.

---

## Complete Decision Example

```json
{
  "schemaVersion": 2,
  "category": "decision",
  "events": [
    {
      "id": "dec_spar",
      "category": "decision",
      "titleId": "dec_spar_title",
      "title": "Spar with a Comrade",
      "setupId": "dec_spar_setup",
      "setup": "A soldier offers to trade blows in the practice yard.",
      "requirements": {
        "tier": { "min": 1, "max": 999 }
      },
      "timing": {
        "cooldown_days": 1,
        "priority": "normal"
      },
      "options": [
        {
          "id": "spar_friendly",
          "textId": "dec_spar_friendly",
          "text": "Keep it friendly.",
          "costs": {
            "fatigue": 2
          },
  "effects": {
    "skillXp": { "OneHanded": 25, "Athletics": 10 },
    "lordRep": 2
  },
          "resultTextId": "dec_spar_friendly_result",
          "resultText": "A good practice session. You both learn something."
        },
        {
          "id": "spar_hard",
          "textId": "dec_spar_hard",
          "text": "Make it count.",
          "costs": {
            "fatigue": 4
          },
  "effects": {
    "skillXp": { "OneHanded": 40, "Athletics": 20 },
    "lordRep": 3,
    "hpChange": -8
  },
          "resultTextId": "dec_spar_hard_result",
          "resultText": "Blood is drawn. Word spreads about this bout."
        }
      ]
    }
  ]
}
```

---

## Validation Checklist

Before committing content:

- [ ] Root array is `"events"` (not `"decisions"`)
- [ ] All items have `"category": "decision"` for Camp Hub decisions
- [ ] ID prefixes match delivery mechanism (`dec_*` for Camp Hub)
- [ ] **`skillXp` is in `effects` for main options** (NOT in `rewards`)
- [ ] `rewards` only used in sub-choice options (reward_choices)
- [ ] HP changes use `hpChange`, not `hp`
- [ ] All string IDs have matching entries in `enlisted_strings.xml`
- [ ] Skill/trait names match valid values exactly (case-sensitive)
- [ ] Flag-gated events use `triggers.all` with `has_flag:` conditions
- [ ] Pay tension events use `triggers.escalation_requirements.pay_tension_min`
- [ ] Escalation track names match supported values (case variants accepted)

**Localization Status:** All existing events have complete XML localization as of Dec 2025. For new content, run `python tools/events/sync_event_strings.py` to automatically extract and add missing strings.

---

## Related Schemas

### Quartermaster Dialogue Schema

The Quartermaster system uses a dedicated dialogue JSON schema for conversation trees. This follows similar patterns to the event system but with dialogue-specific features:

- **Context-conditional nodes** - Multiple nodes with same ID, different contexts
- **Gate nodes** - RP responses when player doesn't meet requirements
- **Actions** - Trigger UI/system behaviors (open Gauntlet, set flags)
- **Dynamic text variables** - Nodes can use `{VARIABLE}` placeholders that are set at runtime

**Location:** `ModuleData/Enlisted/Dialogue/qm_*.json`

**Schema:** Defined in `QMDialogueCatalog.cs` - see [Quartermaster System](../Equipment/quartermaster-system.md) for documentation.

**Key Differences from Events:**
| Feature | Events | Dialogue |
|---------|--------|----------|
| Root Array | `"events"` | `"nodes"` |
| Delivery | Popup inquiry | Conversation flow |
| Branching | `chainsTo` | `next_node` |
| Conditions | `requirements` + `triggers` | `context` + `gate` |
| Actions | `effects` | `action` + `action_data` |
| Text Variables | Static or `{PLAYER_NAME}` | Dynamic runtime generation supported |

**Manual Registration Pattern:**
When a node uses dynamically-generated text (not just context selection), it must be manually registered in `EnlistedDialogManager.cs`:
- Manually register the NPC dialogue line with a condition that sets text variables
- Manually register all player response options (JSON loader won't process them)
- Example: Supply inquiry response uses `SetSupplyStatusText()` to generate `{SUPPLY_STATUS}` at runtime

**Example JSON (requires manual registration):**
```json
{
  "id": "qm_supply_response",
  "speaker": "quartermaster",
  "textId": "qm_supply_report",
  "text": "{SUPPLY_STATUS}",
  "context": {},
  "options": [
    {
      "id": "supply_understood",
      "textId": "qm_continue",
      "text": "[Continue]",
      "next_node": "qm_hub"
    }
  ]
}
```

---

## Severity Field (News Priority System)

**Added:** December 2025 (Baggage Train feature)

### Purpose

The optional `severity` field controls how events appear in the news feed:
1. **Color coding** - Visual importance in GameMenu displays
2. **Display duration** - How long the event stays visible
3. **Priority** - Whether it can be replaced by less important events

### Values

| Severity | Color | Duration | Use For |
|----------|-------|----------|---------|
| `"normal"` | Default (cream) | 6 hours | Routine updates, standard events |
| `"positive"` | Success (green) | 6 hours | Good news, opportunities, achievements |
| `"attention"` | Warning (gold) | 12 hours | Needs attention, warnings, delays |
| `"urgent"` | Alert (red) | 24 hours | Problems, losses, dangers, raids |
| `"critical"` | Alert (red) | 48 hours | Immediate threats, lockdowns, crises |

### Behavior

**Display Duration:**
- Events persist in personal feed for their duration
- Cannot be replaced by lower-severity events
- Automatically expire after duration elapses

**Color Rendering:**
- Maps to existing `EnlistedColors.xml` styles
- Displayed in Camp Hub RECENT ACTIONS section
- Also affects Daily Brief mentions

**Priority Replacement:**
```
Critical (48h) → Cannot be replaced by anything
Urgent (24h)   → Can only be replaced by Critical or another Urgent
Attention (12h)→ Can be replaced by Urgent or Critical
Normal/Positive (6h) → Can be replaced by anything higher
```

### Examples

**Baggage Train Events:**
```json
{
  "id": "evt_baggage_raided",
  "severity": "urgent",
  "title": "Raiders Hit the Baggage Train",
  ...
}
```

**Training Events:**
```json
{
  "id": "evt_training_success",
  "severity": "positive",
  "title": "Training Session Complete",
  ...
}
```

**Default (No Severity):**
```json
{
  "id": "evt_routine_patrol",
  "title": "Routine Patrol",
  // No severity field = defaults to "normal"
  ...
}
```

### Implementation Notes

- **Optional field** - Existing events without severity default to `"normal"`
- **Backward compatible** - No changes needed to existing event files
- **Case insensitive** - Parser accepts `"Normal"`, `"NORMAL"`, `"normal"`
- **Invalid values** - Unknown severities default to `"normal"` with warning log

---

## Camp Opportunities Schema (Phase 6)

**Added:** December 2025 (Content Orchestrator / Camp Life Simulation)  
**Updated:** December 2025 (Aligned with actual implementation)

Camp Opportunities are dynamically generated activities that appear in the Main Menu DECISIONS section. Unlike static decisions or scheduled events, these are orchestrator-curated based on world state, time, player condition, and history.

### File Location

| File | Purpose |
|------|---------|
| `ModuleData/Enlisted/camp_opportunities.json` | Opportunity definitions |

### Root Structure

```json
{
  "schemaVersion": 2,
  "opportunities": [
    { ... opportunity definitions ... }
  ]
}
```

### Opportunity Definition

```json
{
  "id": "opp_card_game",
  "type": "social",

  "titleId": "opp_card_game_title",
  "title": "Card Game",

  "descriptionId": "opp_card_game_desc",
  "description": "A card game is forming by the fire. Stakes look good.",
  
  "hintId": "opp_card_game_hint",
  "hint": "{SOLDIER_NAME} mentioned a card game tonight.",

  "actionId": "opp_card_game_action",
  "action": "Sit in",

  "targetDecision": "dec_gambling",
  
  "minTier": 1,
  "maxTier": 0,
  "cooldownHours": 24,
  "baseFitness": 50,
  
  "validPhases": ["Dusk", "Night"],
  
  "orderCompatibility": {
    "guard_duty": "risky",
    "patrol": "risky",
    "default": "available"
  },
  
  "detection": {
    "baseChance": 0.25,
    "nightModifier": -0.15,
    "highRepModifier": -0.10
  },
  
  "caughtConsequences": {
    "officerRep": -15,
    "discipline": 2,
    "orderFailureRisk": 0.20
  },
  
  "tooltipRiskyId": "opp_card_game_risk_tooltip",
  "tooltipRisky": "Gambling while on duty? If you're caught, the sergeant won't be pleased.",
  
  "scheduledTime": "tonight",
  
  "requiredFlags": [],
  "blockedByFlags": []
}
```

### Field Reference

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | string | ✅ | Unique ID, prefix `opp_` |
| `type` | string | ✅ | `training`, `social`, `economic`, `recovery`, `special` |
| `titleId` | string | ✅ | XML localization key |
| `title` | string | ❌ | Fallback if localization missing |
| `descriptionId` | string | ✅ | XML localization key for flavor text |
| `description` | string | ❌ | Fallback |
| `hintId` | string | ❌ | XML localization key for narrative hint (shown in Daily Brief before availability). Use dynamic placeholders like `{SOLDIER_NAME}`, `{COMRADE_NAME}`, `{SETTLEMENT_NAME}` for personalization. |
| `hint` | string | ❌ | Fallback hint text. Auto-categorized: camp rumors (social activities) appear in Company Reports with Rumor styling (lavender), personal hints (player needs) appear in Your Status with default styling. |
| `actionId` | string | ✅ | XML localization key for button text |
| `action` | string | ❌ | Fallback (e.g., "Join", "Sit in") |
| `targetDecision` | string | ❌ | Decision to trigger when engaged |
| `minTier` | int | ❌ | Minimum enlistment tier (default: 1) |
| `maxTier` | int | ❌ | Maximum tier (0 = no max) |
| `cooldownHours` | int | ❌ | Hours before can appear again (default: 12) |
| `baseFitness` | int | ❌ | Base fitness score 0-100 (default: 50). Modifiers applied in code. |
| `validPhases` | array | ❌ | Day phases when eligible: `["Dawn", "Midday", "Dusk", "Night"]` |
| `orderCompatibility` | object | ❌ | Order type → compatibility value |
| `detection` | object | ❌ | Risky activity detection chances |
| `caughtConsequences` | object | ❌ | Consequences if caught |
| `tooltipRiskyId` | string | ❌ | Localization key for risk tooltip |
| `tooltipRisky` | string | ❌ | Fallback risk tooltip |
| `scheduledTime` | string | ❌ | **DEPRECATED** - Use `scheduledPhase` instead |
| `scheduledPhase` | string | ❌ | When activity typically fires: `"Dawn"`, `"Midday"`, `"Dusk"`, `"Night"`. Used for fitness scoring hint, not strict scheduling. |
| `immediate` | bool | ❌ | **REMOVED 2026-01-04** - All opportunities now compete on fitness through orchestrator. This flag no longer has any effect. |
| `requiredFlags` | array | ❌ | Flags that must be set to appear |
| `blockedByFlags` | array | ❌ | Flags that prevent appearance |
| `requirements` | object | ❌ | Requirement conditions (see Requirements Object section below) |
| `notAtSea` | bool | ❌ | Only appears on land (not at sea) |
| `atSea` | bool | ❌ | Only appears at sea (not on land) |

### Requirements Object

Opportunities can have a `requirements` object that filters when they appear:

```json
{
  "id": "opp_urgent_medical",
  "type": "recovery",
  "requirements": {
    "conditionStates": ["HasSevereCondition"]
  }
}
```

**Supported Requirement Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `conditionStates` | array | Required player condition states (see below) |
| `medicalPressure` | array | Required medical pressure levels (see below) |

**Additional Medical Filtering:**

| Field | Type | Description |
|-------|------|-------------|
| `suppressWhenTreated` | bool | If true, hides opportunity while player is under medical care |

**Valid Condition States:**

| State | Description | When True |
|-------|-------------|-----------|
| `HasAnyCondition` | Player has any active condition | Any injury, illness, or exhaustion with days remaining > 0 |
| `HasCondition` | Same as `HasAnyCondition` | Alias for compatibility |
| `HasSevereCondition` | Player has severe/critical condition | Injury or illness severity >= Severe AND days remaining > 0 |
| `HasInjury` | Player has active injury | Injury with days remaining > 0 |
| `HasIllness` | Player has active illness | Illness with days remaining > 0 |

**Valid Medical Pressure Levels:**

| Level | Medical Risk Range | Description |
|-------|-------------------|-------------|
| `Low` | 1+ | Any medical risk present |
| `Moderate` | 2+ | Moderate medical risk (warning level) |
| `High` | 3+ | High medical risk (concerning) |
| `Critical` | 4-5 | Critical medical risk (emergency) |

**Example - Medical Opportunities:**

```json
{
  "id": "opp_seek_medical_care",
  "type": "recovery",
  "title": "Seek Medical Care",
  "requirements": {
    "conditionStates": ["HasCondition"]
  },
  "comment": "Only appears when player has any active condition"
}
```

```json
{
  "id": "opp_urgent_medical",
  "type": "recovery",
  "title": "Urgent Medical Care",
  "validPhases": ["Dawn", "Midday", "Dusk", "Night"],
  "requirements": {
    "conditionStates": ["HasSevereCondition"]
  },
  "comment": "Available in all phases when player has severe/critical injury or illness"
}
```

```json
{
  "id": "opp_preventive_rest",
  "type": "recovery",
  "title": "Preventive Rest",
  "requirements": {
    "medicalPressure": ["Moderate", "High"]
  },
  "comment": "Only appears when medical risk is 2+ but no condition yet"
}
```

**How It Works:**
- Requirements are checked during candidate generation (before fitness scoring)
- Opportunities that don't meet requirements are filtered out immediately
- Multiple condition states in the array work as AND logic (all must be true)
- Multiple medical pressure levels in the array work as OR logic (any can match)
- If condition system is disabled, condition-dependent opportunities won't appear
- Medical pressure checks escalation manager's MedicalRisk track (0-5 scale)

---

### Fitness Scoring

Fitness determines which opportunities appear. The `baseFitness` field sets the starting score (0-100). The `CampOpportunityGenerator` then applies 4 layers of modifiers in code:

| Layer | Source | Example Modifiers |
|-------|--------|-------------------|
| **World State (Macro)** | Lord situation, war status | Training +15 in garrison, Social -20 in siege |
| **Camp Context (Meso)** | Day phase, camp mood, weekly rhythm | Training +10 at dawn, Social +15 at dusk |
| **Player State (Micro)** | Fatigue, gold, injury | Training -25 if exhausted, Recovery +30 if injured |
| **History (Meta)** | Recent presentations, engagement rate | -40 if shown <12h ago, +15 if player engages often |

Opportunities with final score < 40 are filtered out. Top N (based on budget) are displayed.

**Why code-computed modifiers?** This approach keeps JSON simple while allowing nuanced, interconnected scoring that responds to game state. The modifiers are transparent in `CampOpportunityGenerator.cs`.

---

### Narrative Hints (Orchestrator Pre-Scheduling)

**Purpose:** Hints are short, immersive text snippets that foreshadow upcoming opportunities. They're part of the [Orchestrator Opportunity Unification](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md) system where opportunities are pre-scheduled 24 hours ahead.

**How It Works:**
1. At 6am daily tick, `ContentOrchestrator.ScheduleOpportunities()` generates opportunities for each phase (Dawn/Midday/Dusk/Night)
2. Scheduled opportunities are **locked** - they won't disappear due to context changes (lord leaving castle, phase transitions)
3. Hints from current and next phase are categorized and displayed in Daily Brief sections
4. Player sees hints before the opportunity becomes available
5. When the player opens the menu during that phase, the opportunity is available as promised

**Hint Categories & Display:**

Hints are automatically categorized by content:

| **Category** | **Display Section** | **Styling** | **Examples** |
|--------------|---------------------|-------------|--------------|
| **Camp Rumors** | Company Reports | `<span style="Rumor">` (muted lavender #B8A8D8) | "Torgan mentioned a card game tonight." |
| **Personal Hints** | Your Status | Default text | "Your condition needs attention." |

**Categorization Logic:**
- **Personal hints** start with "Your" or "You", or contain medical language (condition, wound, injury, worsening)
- **Camp rumors** are everything else (social activities, things others are doing)

**Hint Authoring Guidelines:**
- **Phase-appropriate:** "Veterans are gathering for morning drill" (Dawn), "A card game is forming this evening" (Dusk)
- **Immersive:** Write as camp gossip or observation, not UI text
- **Brief:** 1 sentence, max 10 words preferred
- **Use placeholders:** Include `{SOLDIER_NAME}`, `{COMRADE_NAME}`, `{SERGEANT}`, `{SETTLEMENT_NAME}` etc. for dynamic text
- **Optional:** If no hint provided, opportunity appears without foreshadowing (still works fine)

**Examples:**

```json
{
  "id": "opp_weapon_drill",
  "type": "training",
  "hintId": "opp_weapon_drill_hint",
  "hint": "{VETERAN_1_NAME} mentioned morning drill.",
  "targetDecision": "dec_training_weapon_drill"
}
```

```json
{
  "id": "opp_seek_medical_care",
  "type": "recovery",
  "hintId": "opp_medical_care_hint",
  "hint": "Your condition needs attention.",
  "targetDecision": "dec_medical_care"
}
```

**Display Behavior:**
- Camp rumors appear in **Company Reports** with lavender styling for visual distinction
- Personal hints appear in **Your Status** with default text color
- Max 2 hints per section (4 total)
- Only shows hints for non-consumed opportunities in current and next phases
- If opportunity is consumed (player engaged), hint disappears
- Placeholders like `{SOLDIER_NAME}` are resolved at runtime with actual game data

**Why This Matters:** Before this system, opportunities could vanish mid-session (e.g., lord leaving castle triggers context change → 0 budget). Now they're stable and telegraphed, creating immersive anticipation and player agency. The split display helps players scan quickly: lavender text = social activities (optional), plain text = personal needs (might want to address).

---

### Order Compatibility Values

| Value | Behavior |
|-------|----------|
| `"available"` | No risk. Appears normally. |
| `"risky"` | Appears with tooltip. Detection check on engage. |
| `"blocked"` | Filtered out by orchestrator. Doesn't appear. |

**Order type keys use snake_case:** `guard_duty`, `patrol`, `fatigue_duty`, `default`, etc.

### Opportunity Types

| Type | When Appropriate |
|------|------------------|
| `training` | Morning, garrison, rested player |
| `social` | Evening, good morale, off-duty |
| `economic` | Low gold, quartermaster needs help |
| `recovery` | Injured, exhausted, siege |
| `special` | Lord audience, settlement visit, baggage access |

### Valid Day Phases

| Phase | Time | Syncs With |
|-------|------|------------|
| `Dawn` | 6am-11am | Order Phase 1 |
| `Midday` | 12pm-5pm | Order Phase 2 |
| `Dusk` | 6pm-9pm | Order Phase 3 |
| `Night` | 10pm-5am | Order Phase 4 |

### Sea/Land Context Filtering

Opportunities can be restricted to land or sea travel using `notAtSea` and `atSea` fields:

**Land-Only Opportunities:**
```json
{
  "id": "opp_rest_tent",
  "title": "Rest in Tent",
  "description": "Your bedroll looks inviting...",
  "notAtSea": true  // Only appears on land
}
```

**Sea-Only Opportunities:**
```json
{
  "id": "opp_rest_hammock",
  "title": "Rest in Hammock",
  "description": "Your hammock sways with the ship's motion...",
  "atSea": true  // Only appears at sea
}
```

**Detection:** The generator checks `MobileParty.IsCurrentlyAtSea` to filter opportunities based on party location.

**Use Case:** Create contextually appropriate variants for different travel modes (wagons/tents on land, ship/crew at sea).

---

## Main Menu Info Sections Schema (Phase 5)

The Main Menu displays three cached info sections. These don't need JSON schemas - they're generated by code. But they need localization keys.

### Required Localization Keys

Add to `ModuleData/Languages/enlisted_strings.xml`:

```xml
<!-- ═══════════════════════════════════════════════════════════════ -->
<!-- MAIN MENU - Info Sections (Phase 5 Content Orchestrator)        -->
<!-- ═══════════════════════════════════════════════════════════════ -->

<!-- Section Headers -->
<string id="menu_section_kingdom" text="_____ KINGDOM _____" />
<string id="menu_section_camp" text="_____ CAMP _____" />
<string id="menu_section_you" text="_____ YOU _____" />

<!-- YOU Section - Natural flowing text about player status -->
<string id="menu_you_off_duty" text="You're off duty and well-rested." />
<string id="menu_you_off_duty_tired" text="You're off duty. Still tired from yesterday." />
<string id="menu_you_off_duty_scheduled" text="You're off duty. {ORDER_NAME} scheduled for tomorrow at dawn." />
<string id="menu_you_on_duty" text="On duty - {ORDER_NAME}, day {DAY} of {TOTAL}." />
<string id="menu_you_on_duty_progress" text="On duty - {ORDER_NAME}. {CURRENT_PHASE}. {NEXT_PHASE} in {HOURS} hours." />
<string id="menu_you_wounded" text="You're wounded - movement impaired. Off duty until you recover." />
<string id="menu_you_exhausted" text="Exhausted from today's combat. The {NCO_TITLE} gave everyone rest." />
<string id="menu_you_sick" text="You're feeling poorly. Rest ordered until you're fit for duty." />
<string id="menu_you_commitment" text="You've agreed to join the {ACTIVITY} tonight." />
<string id="menu_you_risk" text="You agreed to the {ACTIVITY} tonight. Risky - you'll need to slip away from your post." />
<string id="menu_you_default" text="The day stretches ahead." />

<!-- CAMP Section - Living world activities (what's happening around you) -->
<string id="menu_camp_morning" text="Morning bustle. Camp coming alive." />
<string id="menu_camp_midday" text="Midday heat. Most resting in shade." />
<string id="menu_camp_evening" text="Evening calm. Good spirits in camp." />
<string id="menu_camp_night" text="Night watch. Camp quiet." />
<string id="menu_camp_drilling" text="Veterans drilling by the wagons." />
<string id="menu_camp_cards" text="Card game forming tonight by the fire." />
<string id="menu_camp_dice" text="Dice game by the fire." />
<string id="menu_camp_roster" text="The {NCO_TITLE}'s making lists - duty roster tomorrow." />
<string id="menu_camp_qm_open" text="Quartermaster is open." />
<string id="menu_camp_good_morale" text="Good spirits in camp." />
<string id="menu_camp_tense" text="Camp is tense. Siege weighs on everyone." />
<string id="menu_camp_quiet" text="A quiet moment in camp. Rest while you can." />

<!-- KINGDOM Section Content -->
<string id="menu_kingdom_at_war" text="{KINGDOM_A} at war with {KINGDOM_B}." />
<string id="menu_kingdom_siege" text="Siege underway at {SETTLEMENT}." />
<string id="menu_kingdom_peace" text="The realm is at peace." />
<string id="menu_kingdom_quiet" text="The realm is quiet. No major news." />

<!-- Changed Content Indicator -->
<string id="menu_new_tag" text="[NEW]" />

<!-- Empty States -->
<string id="menu_orders_none" text="No active orders." />
<string id="menu_decisions_none" text="Nothing demands your attention right now." />
```

---

## Order State Display Schema (Phase 10)

**Updated:** 2025-12-31 - Simplified to imminent warning system

Orders progress through states: **IMMINENT** → PENDING → ACTIVE → COMPLETE.

**Time Speeds (from Campaign.cs decompile):**
- Play (1x): 1 game day = 80 real seconds | 1 hour = 3.3 real seconds
- FastForward (>>): 1 game day = 20 real seconds | 1 hour = 0.83 real seconds

**Imminent Warning System:**
- Orchestrator decides "order should fire"
- Creates order in IMMINENT state with 4-8 hour delay
- At FastForward: 4-8h = 3.3-6.6 real seconds warning
- Simple, realistic: "Sergeant will call for you soon" vs long-term forecasting

### Order State Enum (C# Model)

```csharp
public enum OrderState
{
    Imminent,  // 4-8h warning - "Sergeant will call for you soon"
    Pending,   // Issued - shows in Orders menu for accept/decline
    Active,    // Accepted - progressing through phases
    Complete   // Done - results in Recent Activity
}
```

### Order Model Fields (Phase 10)

```csharp
public class Order
{
    // ... existing fields ...
    public OrderState State { get; set; } = OrderState.Pending;
    public CampaignTime ImminentTime { get; set; }  // When imminent warning began
    public CampaignTime IssueTime { get; set; }     // When order will be issued (ImminentTime + 4-8h)
}
```

### Required Localization Keys (Phase 10)

```xml
<!-- ═══════════════════════════════════════════════════════════════ -->
<!-- ORDERS MENU - Order State Display (Phase 10)                    -->
<!-- ═══════════════════════════════════════════════════════════════ -->

<!-- Imminent Warning Display (in summaries) -->
<!-- Use <span style="Urgent"> for orange time-sensitive color -->
<string id="order_imminent_kingdom" text="Expect strategic orders from command soon." />
<string id="order_imminent_company" text="Sergeant looking for volunteers." />
<string id="order_imminent_player" text="&lt;span style='Urgent'&gt;{ORDER_NAME} in {HOURS} hours&lt;/span&gt;" />

<!-- Order State Headers -->
<string id="order_state_imminent" text="IMMINENT:" />
<string id="order_state_pending" text="PENDING:" />
<string id="order_state_active" text="ACTIVE:" />
<string id="order_state_complete" text="COMPLETE:" />

<!-- Pending Order Display -->
<string id="order_pending_prompt" text="Report to {LOCATION} for {ORDER_TYPE} duty." />
<string id="order_pending_accept" text="Accept Order" />
<string id="order_pending_decline" text="Decline" />

<!-- Active Order Display -->
<string id="order_active_progress" text="{ORDER_NAME} (Day {DAY}/{TOTAL})" />
<string id="order_active_status" text="{CURRENT_PHASE_TEXT}" />
<string id="order_active_next" text="Next: {NEXT_PHASE} in {HOURS} hours." />

<!-- Complete Order Display -->
<string id="order_complete_success" text="{ORDER_NAME} - Success" />
<string id="order_complete_adequate" text="{ORDER_NAME} - Adequate" />
<string id="order_complete_failed" text="{ORDER_NAME} - Failed" />
<string id="order_complete_rewards" text="Duty completed. {REWARDS}" />

<!-- Auto-Decline Messages -->
<string id="order_auto_accept" text="You were assigned {ORDER_NAME} while traveling." />
<string id="order_auto_decline" text="You missed an order assignment." />
<string id="order_pending_warning" text="Respond soon or miss the order." />
<string id="order_imminent_cancelled" text="World state changed. Order no longer needed." />
```

---

## Order-Decision Tension Schema (Phase 6)

When player is on duty, risky activities have detection chances and consequences.

### Required Localization Keys

```xml
<!-- ═══════════════════════════════════════════════════════════════ -->
<!-- ORDER-DECISION TENSION - Risk Tooltips (Phase 6)                -->
<!-- ═══════════════════════════════════════════════════════════════ -->

<!-- Generic Risk Tooltip (fallback) -->
<string id="opp_risk_tooltip_generic" text="Risk: You're on duty. If caught: {REP_PENALTY} Officer Rep. Detection: ~{DETECTION_CHANCE}%" />

<!-- Caught Notifications -->
<string id="opp_caught_notification" text="You were caught away from your post!" />
<string id="opp_caught_detail" text="The {OFFICER_TITLE} noticed your absence. This won't be forgotten." />
<string id="opp_caught_warning" text="You're on thin ice with command." />

<!-- Order-Specific Tooltips (optional overrides) -->
<string id="opp_risk_guard_post" text="Risk: Leaving guard post. If caught: Discipline review, -15 Officer Rep." />
<string id="opp_risk_patrol" text="Risk: Abandoning patrol. If caught: Order may fail." />
<string id="opp_risk_sentry" text="Risk: Sentry duty. Severe consequences if caught sleeping." />
```

---

## Culture-Aware Rank Tokens

The Main Menu uses culture-appropriate rank names via tokens. These resolve at runtime based on the enlisted lord's culture.

### Token Reference

| Token | Resolves To | Example (Vlandia) | Example (Battania) |
|-------|-------------|-------------------|-------------------|
| `{NCO_TITLE}` | Culture's NCO rank | "Sergeant" | "Warleader" |
| `{OFFICER_TITLE}` | Culture's officer rank | "Captain" | "Champion" |
| `{RANK_NAME}` | Player's current rank | "Footman" | "Warrior" |

### Implementation

```csharp
public static string ResolveToken(string text, CultureObject culture)
{
    return text
        .Replace("{NCO_TITLE}", GetNCOTitle(culture))
        .Replace("{OFFICER_TITLE}", GetOfficerTitle(culture))
        .Replace("{RANK_NAME}", GetPlayerRankName(culture));
}
```

**Fallback:** If culture lookup fails, use generic English: "the NCO", "the officer", "Soldier".

---

## Localization Checklist (Phase 5-6)

Before implementing Main Menu / Camp Life:

- [ ] All `menu_section_*` keys added to enlisted_strings.xml
- [ ] All `menu_you_*`, `menu_duty_*`, `menu_state_*` keys added
- [ ] All `menu_forecast_*` keys added
- [ ] All `menu_kingdom_*`, `menu_camp_*` keys added
- [ ] All `order_state_*`, `order_*_line` keys added
- [ ] All `opp_*` opportunity localization keys added
- [ ] Culture-aware rank fallbacks tested for all cultures
- [ ] `[NEW]` tag key added (`menu_new_tag`)
- [ ] Run `python tools/events/sync_event_strings.py` to verify

---

## Progression System Schema (Future Foundation)

**Status:** 📋 Schema Ready (Implementation Deferred)  
**Purpose:** Generic probabilistic progression pattern for escalation tracks

This schema defines how any escalation track (Medical Risk, Discipline, Pay Tension, etc.) can have organic, time-based progression instead of purely deterministic event outcomes.

### Core Concept

Instead of "choice = fixed outcome", progression tracks can:
- **Improve** naturally over time
- **Stay stable** (no change)
- **Worsen** (complications, decay)

Daily/hourly probability checks determine outcomes, modified by skills, context, and player actions.

### Progression Definition Schema

Add to any escalation track's config:

```json
{
  "progressionConfig": {
    "track": "medical_risk",
    "enabled": true,
    
    "tick": {
      "type": "daily",
      "hour": 6,
      "skipDuring": ["battle", "muster"]
    },
    
    "thresholdEvents": {
      "2": "evt_medical_onset",
      "3": "evt_medical_worsening",
      "4": "evt_medical_complication",
      "5": "evt_medical_emergency"
    },
    
    "baseChances": {
      "1": { "improve": 40, "stable": 50, "worsen": 10 },
      "2": { "improve": 25, "stable": 55, "worsen": 20 },
      "3": { "improve": 15, "stable": 50, "worsen": 35 },
      "4": { "improve": 10, "stable": 45, "worsen": 45 },
      "5": { "improve": 5, "stable": 40, "worsen": 55 }
    },
    
    "criticalRolls": {
      "luckyRange": [1, 5],
      "luckyEffect": -2,
      "complicationRange": [96, 100],
      "complicationEffect": +2
    },
    
    "skillModifier": {
      "skill": "Medicine",
      "divisor": 3,
      "maxBonus": 20
    },
    
    "contextModifiers": {
      "resting": { "improve": +10, "worsen": -10 },
      "marching": { "improve": -10, "worsen": +10 },
      "afterBattle": { "improve": -15, "worsen": +15 },
      "treated": { "improve": +25, "worsen": -25 }
    },
    
    "worldStateModifiers": {
      "garrison": { "improve": +5, "worsen": -5 },
      "campaign": { "improve": 0, "worsen": 0 },
      "siege": { "improve": -10, "worsen": +10 }
    },
    
    "clamps": {
      "improveMin": 5,
      "improveMax": 80,
      "worsenMin": 5,
      "worsenMax": 70
    }
  }
}
```

### Field Reference

| Field | Type | Purpose |
|-------|------|---------|
| `track` | string | Which escalation track this affects |
| `tick.type` | string | `"daily"`, `"hourly"`, or `"perPhase"` (for orders) |
| `tick.hour` | int | Hour of day to check (0-23) |
| `tick.skipDuring` | array | Contexts to skip (battle, muster) |
| `thresholdEvents` | object | Event IDs to fire when crossing thresholds |
| `baseChances` | object | Base probabilities per severity level |
| `criticalRolls` | object | Lucky/complication roll ranges and effects |
| `skillModifier` | object | Which skill affects this, formula |
| `contextModifiers` | object | Situational modifiers |
| `worldStateModifiers` | object | Orchestrator WorldSituation modifiers |
| `clamps` | object | Min/max probability limits |

### Applicable Tracks

This pattern can apply to any escalation track:

| Track | Tick Type | Skill | Notes |
|-------|-----------|-------|-------|
| `medical_risk` | Daily (6am) | Medicine | Health conditions |
| `discipline` | Daily (8pm) | Leadership | Behavior record |
| `pay_tension` | Daily | Trade | Financial stress |
| `scrutiny` | Per-order | Charm | Officer attention |
| `morale` | Daily | Leadership | Unit morale (future) |

### Orchestrator Integration

The ContentOrchestrator provides world state modifiers to progression systems:

```csharp
// Progression behavior asks orchestrator for modifiers
public interface IProgressionModifierProvider
{
    ProgressionModifiers GetProgressionModifiers(string trackName);
}

public class ProgressionModifiers
{
    public float ImproveBonus { get; set; }
    public float WorsenBonus { get; set; }
    public float TickMultiplier { get; set; } // 0.5 = half as often, 2.0 = twice as often
}

// Example: ContentOrchestrator implements this
public ProgressionModifiers GetProgressionModifiers(string trackName)
{
    var situation = WorldStateAnalyzer.Analyze();
    
    return trackName switch
    {
        "medical_risk" => new ProgressionModifiers
        {
            ImproveBonus = situation.LordSituation == LordSituation.InGarrison ? 5 : -5,
            WorsenBonus = situation.LordSituation == LordSituation.InSiege ? 10 : 0,
            TickMultiplier = situation.ActivityLevel == ActivityLevel.Intense ? 1.5f : 1.0f
        },
        _ => new ProgressionModifiers()
    };
}
```

### Threshold Event Delivery

When progression crosses a threshold, the event goes through standard delivery:

```csharp
// In ProgressionBehavior.OnTick()
if (crossedThreshold)
{
    var eventId = config.ThresholdEvents[newValue.ToString()];
    EventDeliveryManager.Instance.QueueEvent(eventId);
}
```

### Treatment/Action Integration

Player actions set flags that modify next day's roll:

```json
{
  "id": "dec_seek_treatment",
  "effects": {
    "setFlags": ["treated_today"],
    "medical_risk": -1
  }
}
```

The progression system checks for `treated_today` flag and applies the `treated` context modifier.

### Implementation Location

```
src/Features/Escalation/
├── ProgressionBehavior.cs         (base class)
├── MedicalProgressionBehavior.cs  (medical-specific)
├── DisciplineProgressionBehavior.cs
└── Models/
    ├── ProgressionConfig.cs
    └── ProgressionModifiers.cs

ModuleData/Enlisted/
└── progression_config.json        (all track configurations)
```

### Localization Keys

```xml
<!-- Progression Outcome Messages -->
<string id="prog_medical_improve" text="Feeling stronger today." />
<string id="prog_medical_stable" text="" /> <!-- No message for stable -->
<string id="prog_medical_worsen" text="Condition worsened overnight." />
<string id="prog_medical_lucky" text="The fever broke. Nearly back to normal." />
<string id="prog_medical_complication" text="Took a bad turn in the night." />

<string id="prog_discipline_improve" text="Your behavior has been noted positively." />
<string id="prog_discipline_worsen" text="Your conduct is drawing unwanted attention." />
```

### Future Expansion

Once the foundation is implemented, add:
1. **Specific Conditions** - Fever, Infection, Plague (each with own progression)
2. **Contagion** - Spread between party members
3. **Permanent Consequences** - Scars, stat penalties from critical conditions
4. **Treatment Quality** - Different lords have different surgeons
5. **Order Progression** - Orders could use this pattern for phase outcomes

---

## Order Event Schema

**Purpose:** Events that fire during order execution (at slot phases). Different from narrative events in that they use `order_type` instead of `category` and support world state requirements.

**⚠️ CRITICAL:** All order event options **must** include `skillXp` in their `effects` object. Players expect XP for completing orders. Validation tool will warn if missing.

**File Locations:**
```
ModuleData/Enlisted/Orders/order_events/
├── guard_events.json       (order_guard_post)
├── sentry_events.json      (order_sentry_duty)
├── patrol_events.json      (order_camp_patrol)
├── patrol_lead_events.json (order_lead_patrol)
├── scout_events.json       (order_scout_route)
├── escort_events.json      (order_escort_duty)
├── firewood_events.json    (order_firewood_detail)
├── latrine_events.json     (order_latrine_duty)
├── cleaning_events.json    (order_equipment_cleaning)
├── muster_events.json      (order_muster_inspection)
├── march_events.json       (order_march_formation)
├── medical_events.json     (order_treat_wounded)
├── repair_events.json      (order_repair_equipment)
├── forage_events.json      (order_forage_supplies)
├── training_events.json    (order_train_recruits)
└── defenses_events.json    (order_inspect_defenses)
```

### Order Event File Structure

```json
{
  "schemaVersion": 2,
  "order_type": "order_guard_post",
  "events": [
    { ... event definitions ... }
  ]
}
```

### Order Event Definition

```json
{
  "id": "guard_drunk_soldier",
  "order_type": "order_guard_post",
  
  "titleId": "order_evt_guard_drunk_title",
  "title": "Drunk Soldier",
  
  "setupId": "order_evt_guard_drunk_setup",
  "setup": "A soldier approaches your post, weaving slightly. He reeks of drink. 'Just let me through.'",
  
  "requirements": {
    "world_state": ["peacetime_garrison", "war_active_campaign"],
    "tier": { "min": 1, "max": 6 }
  },
  
  "options": [
    {
      "id": "let_pass",
      "textId": "order_evt_guard_drunk_opt_let",
      "text": "Step aside. Not worth the trouble.",
      "tooltip": "Avoid confrontation. -1 Officer Rep, +1 Soldier Rep.",
      "effects": {
        "officerRep": -1,
        "soldierRep": 1
      }
    },
    {
      "id": "turn_away",
      "textId": "order_evt_guard_drunk_opt_turn",
      "text": "You're not getting through like this.",
      "tooltip": "+1 Officer Rep, -1 Soldier Rep. +12 Athletics XP.",
      "effects": {
        "officerRep": 1,
        "soldierRep": -1,
        "skillXp": { "Athletics": 12 }
      }
    },
    {
      "id": "report",
      "textId": "order_evt_guard_drunk_opt_report",
      "text": "Call the sergeant.",
      "tooltip": "+2 Officer Rep, -2 Soldier Rep. +10 Tactics XP.",
      "effects": {
        "officerRep": 2,
        "soldierRep": -2,
        "skillXp": { "Tactics": 10 }
      }
    }
  ]
}
```

### Order Event Fields

| Field | JSON Name | Type | Required | Notes |
|-------|-----------|------|----------|-------|
| ID | `id` | string | ✅ | Unique. Convention: `{order_short}_{event_name}` |
| Order Type | `order_type` | string | ✅ | Which order can trigger this event |
| Title ID | `titleId` | string | ✅ | XML localization key |
| Title | `title` | string | ❌ | Fallback if loc missing |
| Setup ID | `setupId` | string | ✅ | XML key for event description |
| Setup | `setup` | string | ❌ | Fallback narrative |
| Requirements | `requirements` | object | ❌ | See below |
| Options | `options` | array | ✅ | Player choices (same as standard events) |
| Skill Check | `skill_check` | object | ❌ | Optional skill check |
| Chain Flags | `sets_flag` | string | ❌ | Flag set on completion |
| Requires Flag | `requires_flag` | string | ❌ | Flag required to appear |

### Order Event Requirements

Different from narrative events. Uses `world_state` instead of `context`:

```json
{
  "requirements": {
    "world_state": ["peacetime_garrison", "war_marching", "siege_defending"],
    "tier": { "min": 1, "max": 6 }
  }
}
```

**Valid world_state values:**
- `peacetime_garrison` - Camp at settlement, no war
- `peacetime_recruiting` - Patrolling during peace
- `war_marching` - Moving with army in war
- `war_active_campaign` - Active campaign operations
- `siege_attacking` - Attacking in siege
- `siege_defending` - Defending in siege

**Sea/Land Filtering:**

Order events can also use `notAtSea` or `atSea` to prevent land-specific events from firing at sea:

```json
{
  "id": "patrol_gambling_ring",
  "requirements": {
    "world_state": ["peacetime_garrison"],
    "notAtSea": true  // "tents" and "fire" don't exist at sea
  },
  "setup": "Behind the tents, a group of soldiers are rolling dice. Coins glint in the firelight."
}
```

**When to use `notAtSea: true` for order events:**
- Event mentions land-specific objects (tents, fires, walls, gates, forests, fields)
- Event involves land-specific activities (chopping wood, boar hunting, noble entry)
- Event describes garrison/fortification contexts (walls, perimeters, infiltrators)

**Generic events work at sea:**
- Equipment inspection/cleaning (works on ships)
- Interpersonal conflicts (soldiers fight anywhere)
- Relief schedules and exhaustion (universal)
- Officer inspections and tests (work on deck)

Orders with `context_variants.sea` (like Guard Duty → Deck Watch) can still fire events, but land-specific events within that order should use `notAtSea: true` to avoid immersion breaks.

### Order Event Skill Check

Optional skill check that modifies option outcomes:

```json
{
  "skill_check": {
    "skill": "Perception",
    "threshold": 40,
    "modifies_option": "challenge",
    "success_bonus": {
      "officerRep": 2,
      "skillXp": { "Perception": 10 }
    },
    "failure_penalty": {
      "officerRep": -1
    }
  }
}
```

### Order Context Variants (Sea/Land Awareness)

**Added:** 2025-12-31  
**Updated:** 2026-01-03 - Bug fix: Order.Title mutation causing wrong variant display on land

Orders support context-variant text to adapt flavor text for sea vs land travel. When at sea (Warsails DLC), orders can display nautical-themed titles and descriptions.

**Order Definition Schema:**
```json
{
  "id": "order_guard_duty",
  "title": "Guard Duty",
  "description": "Stand watch through the night. Keep your eyes sharp and your blade ready.",
  "context_variants": {
    "sea": {
      "title": "Deck Watch",
      "titleId": "order_guard_duty_sea_title",
      "description": "Stand watch on the foredeck. Keep your eyes on the horizon and your ears on the waves.",
      "descriptionId": "order_guard_duty_sea_desc"
    }
  },
  ...
}
```

**Dynamic Display Behavior:**
- Context variants are resolved **at display time**, not at order creation time
- `OrderCatalog.GetDisplayTitle(order)` - Returns context-appropriate title based on current travel state
- `OrderCatalog.GetDisplayDescription(order)` - Returns context-appropriate description based on current travel state
- If party goes to sea after an order was issued on land, all UI elements automatically show the sea variant
- Checks `WorldStateAnalyzer.DetectTravelContext()` which uses `MobileParty.IsCurrentlyAtSea` (Warsails DLC)
- Falls back to default Title/Description if no variant exists for current context
- **Important:** Order objects are **never mutated** - the base title/description remains unchanged, and variants are applied only during display rendering
- Works without Warsails DLC installed (defaults to "land" context)

**Applied Throughout UI:**
- Enlisted menu order display
- Daily Brief forecasts
- Order detail popups
- Order notifications (issued, started, completed, declined)
- Duty log and status summaries
- News system reports

**World State Integration:**
- `WorldSituation.TravelContext` enum: `Land`, `Sea`
- Populated automatically on every world state analysis
- Uses reflection to check Warsails API without hard dependency

**Use Cases:**
| Order | Land | Sea |
|-------|------|-----|
| Guard Duty | Stand watch at your post | Deck Watch - stand watch on the foredeck |
| Camp Patrol | Walk the perimeter | Hull Inspection - check below decks |
| Firewood Collection | Gather wood with an axe | Deck Scrubbing - scrub the deck |
| Equipment Inspection | Check gear with quartermaster | Rigging Check - inspect lines with the bosun |
| Sentry Post | Watch the road from the hilltop | Masthead Watch - watch from the masthead |
| Muster Inspection | Stand for inspection before lord | Deck Muster - assemble on deck for inspection |

### Order Event vs Narrative Event

| Aspect | Order Event | Narrative Event |
|--------|-------------|-----------------|
| Delivery | During order phases | MapIncident or EventPacer |
| Trigger | `order_type` | `category` |
| Context | `requirements.world_state` | `requirements.context` |
| File Location | `Orders/order_events/*.json` | `Events/*.json` |
| Frequency | Controlled by slot phase | Controlled by orchestrator |
| ID Convention | `{order}_{event}` | `dec_*`, `evt_*`, `mi_*` |

### Order Event Consequence Codes

For reference when reviewing event master document:

**Player Effects:**
- `HP` = Player health change (use `hpChange` in JSON)
- `MED` = Medical Risk (0-5 scale, use `medicalRisk` in JSON)
- `FAT` = Fatigue (use `fatigue` in costs, `fatigueRelief` in rewards)
- `G` = Gold (use `gold` in effects)
- `EQ` = Equipment loss/damage (use `equipmentLoss` in effects)

**Reputation:**
- `REP.S` = Soldier Rep (`soldierRep`, -50 to +50)
- `REP.O` = Officer Rep (`officerRep`, 0-100)
- `REP.L` = Lord Rep (`lordRep`, 0-100)

**Escalation (0-100):**
- `DISC` = Discipline (`discipline`)
- `SUSP` = Scrutiny (`scrutiny`)

**Company Needs (0-100):**
- `C.MOR` = Morale (`companyNeeds.Morale`)
- `C.RDY` = Readiness (`companyNeeds.Readiness`)
- `C.SUP` = Supplies (`companyNeeds.Supplies`)
- `C.MEN` = Troop Loss (`troopLoss`)
- `C.FOOD` = Food (`foodLoss` for loss, `companyNeeds.Supplies` for gain)

**XP:**
- `XP.{Skill}` = Skill XP (`skillXp.{Skill}`)
- **REQUIRED:** All order event options must include at least one skill XP reward in `effects`

**See Also:** [Orders Content](orders-content.md) for order definitions, event files at `ModuleData/Enlisted/Orders/order_events/`.

---

## Camp Routine Configs

**Added:** 2025-12-31  
**Related Docs:** [Camp Routine Schedule](../../Campaign/camp-routine-schedule-spec.md)

Three configuration files define the camp routine system:

### routine_outcomes.json

Defines outcome tables for automatic routine activities. Each activity has weighted outcome probabilities and effect ranges.

**Location:** `ModuleData/Enlisted/Config/routine_outcomes.json`

**Root Structure:**
```json
{
  "schemaVersion": 1,
  "description": "Outcome tables for routine activities",
  "outcomeWeights": { ... },
  "activities": { ... }
}
```

#### Outcome Weights

Weight sets determine probability distribution for outcome quality rolls:

```json
{
  "outcomeWeights": {
    "default": {
      "excellent": 10,
      "good": 25,
      "normal": 40,
      "poor": 18,
      "mishap": 7
    },
    "highSkill": {
      "excellent": 20,
      "good": 35,
      "normal": 30,
      "poor": 12,
      "mishap": 3
    },
    "fatigued": {
      "excellent": 5,
      "good": 15,
      "normal": 35,
      "poor": 30,
      "mishap": 15
    },
    "lowMorale": {
      "excellent": 5,
      "good": 20,
      "normal": 35,
      "poor": 28,
      "mishap": 12
    }
  }
}
```

**Weight Sets:**
- `default` - Normal conditions
- `highSkill` - Player skilled in activity (bonus to excellent/good)
- `fatigued` - Player exhausted (penalty, more mishaps)
- `lowMorale` - Company morale low (mediocre outcomes)

#### Activity Definitions

Each activity category has outcome ranges and flavor text:

```json
{
  "activities": {
    "training": {
      "name": "Combat Training",
      "skill": "OneHanded",
      "xpRanges": {
        "excellent": { "min": 18, "max": 28 },
        "good": { "min": 12, "max": 18 },
        "normal": { "min": 8, "max": 12 },
        "poor": { "min": 3, "max": 7 },
        "mishap": { "min": 0, "max": 3 }
      },
      "fatigueChange": 12,
      "mishapCondition": "minor_injury",
      "mishapChance": 0.4,
      "flavorText": {
        "excellent": [
          "Sharp focus today. Movements feel natural.",
          "Everything clicked. A veteran watched approvingly."
        ],
        "good": [
          "Solid session. The drills are paying off.",
          "Good practice. You're getting faster."
        ],
        "normal": [
          "Another day of practice.",
          "Standard drills completed."
        ],
        "poor": [
          "Distracted. Sergeant noticed.",
          "Sluggish today. The heat didn't help."
        ],
        "mishap": [
          "Twisted ankle during drill.",
          "Training partner's swing caught you wrong."
        ]
      }
    }
  }
}
```

**Activity Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | ✅ | Display name for activity |
| `skill` | string | ✅ | Skill receiving XP (OneHanded, Scouting, etc.) |
| `xpRanges` | object | ✅ | Min/max XP for each outcome type |
| `fatigueChange` | int | ❌ | Base fatigue cost (positive = tiring) |
| `goldChance` | object | ❌ | Probability of finding gold per outcome type |
| `goldRange` | object | ❌ | Min/max gold if found |
| `goldLossChance` | object | ❌ | Probability of losing gold (mishap) |
| `goldLossRange` | object | ❌ | Min/max gold loss |
| `supplyChange` | object | ❌ | Min/max supply gain/loss per outcome type |
| `moraleChange` | object | ❌ | Morale change per outcome type |
| `mishapCondition` | string | ❌ | Condition ID applied on mishap |
| `mishapChance` | float | ❌ | Probability condition applies (0.0-1.0) |
| `flavorText` | object | ✅ | Array of text variants per outcome type |

**Built-in Activities:**
- `formation` - Morning formations (Discipline XP)
- `training` - Combat training (OneHanded XP)
- `work` - Work details (Athletics XP, gold chance)
- `social` - Social time (Charm XP, morale boost)
- `economic` - Trading (Trade XP, gold chance/loss)
- `recovery` - Rest (Medicine XP, fatigue recovery)
- `foraging` - Foraging duty (Scouting XP, supply gain)
- `patrol` - Patrol duty (Scouting XP)
- `extended_rest` - Extended rest (Medicine XP, high recovery)
- `emergency_drill` - Emergency drill (Tactics XP, readiness boost)
- `light_duty` - Light duty (Steward XP, morale boost)

### orchestrator_overrides.json

Defines when the ContentOrchestrator can inject overrides into the schedule.

**Location:** `ModuleData/Enlisted/Config/orchestrator_overrides.json`

**Root Structure:**
```json
{
  "schemaVersion": 1,
  "description": "Orchestrator override definitions",
  "needBasedOverrides": { ... },
  "varietyInjections": { ... },
  "varietySettings": { ... },
  "priorityRules": { ... }
}
```

#### Need-Based Overrides

Triggered automatically when company needs cross thresholds:

```json
{
  "needBasedOverrides": {
    "low_supplies": {
      "trigger": {
        "need": "supplies",
        "threshold": 30,
        "comparison": "lessThan"
      },
      "override": {
        "category": "foraging",
        "name": "Foraging Duty",
        "description": "Company sent to gather supplies",
        "reason": "Supplies critical",
        "priority": 100,
        "addressesNeed": "supplies",
        "replaceBothSlots": true,
        "affectedPhases": ["Midday"],
        "skill": "Scouting",
        "baseXpMin": 8,
        "baseXpMax": 15
      },
      "activationText": "No training today. Entire company on foraging duty.",
      "recoveryThreshold": 50,
      "cooldownDays": 0
    }
  }
}
```

**Trigger Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `need` | string | Need to check: "supplies", "rest", "morale", "readiness", "equipment" |
| `threshold` | int | Value threshold (0-100) |
| `comparison` | string | "lessThan" or "greaterThan" |

**Override Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `category` | string | Activity category (foraging, patrol, extended_rest, etc.) |
| `name` | string | Display name |
| `description` | string | Description for schedule |
| `reason` | string | Reason shown in UI |
| `priority` | int | Priority for conflict resolution (higher wins) |
| `addressesNeed` | string | Need this override addresses |
| `replaceBothSlots` | bool | Replace both activity slots in phase |
| `affectedPhases` | array | Phases this applies to (empty = all phases) |
| `skill` | string | Skill receiving XP |
| `baseXpMin` | int | Minimum XP range |
| `baseXpMax` | int | Maximum XP range |

**Root Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `activationText` | string | Combat log message when override activates |
| `recoveryThreshold` | int | Need value to deactivate override |
| `cooldownDays` | int | Days before override can trigger again |

#### Variety Injections

Special assignments injected periodically to break monotony:

```json
{
  "varietyInjections": {
    "patrol_duty": {
      "category": "patrol",
      "name": "Patrol Duty",
      "description": "Assigned to patrol the perimeter",
      "preferredPhases": ["Dawn", "Midday"],
      "weight": 30,
      "skill": "Scouting",
      "baseXpMin": 5,
      "baseXpMax": 12,
      "activationText": "Assigned to patrol duty this morning."
    }
  }
}
```

**Variety Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `category` | string | Activity category |
| `name` | string | Display name |
| `description` | string | Description for schedule |
| `preferredPhases` | array | Phases this variety prefers |
| `weight` | int | Selection weight (higher = more likely) |
| `skill` | string | Skill receiving XP |
| `baseXpMin` | int | Minimum XP range |
| `baseXpMax` | int | Maximum XP range |
| `activationText` | string | Combat log message when variety triggers |

#### Variety Settings

Controls variety injection frequency:

```json
{
  "varietySettings": {
    "minDaysBetweenInjections": 3,
    "maxDaysBetweenInjections": 5,
    "injectionChancePerDay": 0.35,
    "maxInjectionsPerWeek": 2,
    "skipDuringIntense": true,
    "skipDuringSiege": true
  }
}
```

**Settings Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `minDaysBetweenInjections` | int | Minimum days between variety |
| `maxDaysBetweenInjections` | int | Maximum days (always inject after this) |
| `injectionChancePerDay` | float | Daily probability (0.0-1.0) |
| `maxInjectionsPerWeek` | int | Weekly cap on injections |
| `skipDuringIntense` | bool | Skip during Intense activity level |
| `skipDuringSiege` | bool | Skip during siege situations |

#### Priority Rules

Conflict resolution settings:

```json
{
  "priorityRules": {
    "needBasedAlwaysWins": true,
    "conflictResolution": "highestPriority",
    "maxSimultaneousOverrides": 1
  }
}
```

### camp_schedule.json

Defines baseline schedules per phase (see [Camp Routine Schedule](../../Campaign/camp-routine-schedule-spec.md) for full documentation).

**Location:** `ModuleData/Enlisted/Config/camp_schedule.json`

**Key Sections:**
- `phases` - Baseline schedule per day phase
- `categoryMappings` - Maps schedule categories to opportunity types
- `activityOverrides` - Activity level modifiers (Quiet/Routine/Active/Intense)
- `pressureOverrides` - Pressure-based deviations (low_morale, exhausted, siege, etc.)
- `lordSituationModifiers` - Lord situation impacts (garrison, marching, siege)

---

**End of Document**
