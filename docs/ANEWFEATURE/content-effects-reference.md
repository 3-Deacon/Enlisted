# Content Effects Reference

**Summary:** Complete reference for all effect types available in Enlisted content (events, decisions, opportunities, routines, map incidents). Documents both Enlisted-specific effects and native Bannerlord integration points.

**Status:** Reference  
**Last Updated:** 2026-01-03  
**Related Docs:** [Native Skill XP](native-skill-xp.md), [Event System Schemas](../Features/Content/event-system-schemas.md), [Content Index](../Features/Content/content-index.md)

---

## Overview

Content in Enlisted can apply various effects to modify game state. These effects fall into categories:

| Category | Examples | Native Integration |
|----------|----------|-------------------|
| **Character Progression** | Skill XP, Trait XP | Native `Hero.AddSkillXp()` |
| **Resources** | Gold, HP, Food | Native `GiveGoldAction`, `Hero.HitPoints` |
| **Reputation** | Lord/Officer/Soldier rep | Enlisted-specific (EscalationManager) |
| **Escalation** | Scrutiny, Discipline, Medical Risk | Enlisted-specific |
| **Company Needs** | Readiness, Morale, Rest, Supplies | Enlisted-specific |
| **Party Effects** | Troop loss/wound, Retinue | Mixed native/Enlisted |
| **Narrative** | Chain events, Discharge, Promotion | Enlisted-specific |

---

## Character Progression Effects

### Skill XP (`skillXp`)

Awards XP to specific skills. Uses native `Hero.AddSkillXp()` which respects learning rates.

```json
"effects": {
  "skillXp": {
    "Scouting": 20,
    "Athletics": 12
  }
}
```

**Thematic Aliases:** Content can use thematic names that map to real skills:

| Alias | → Skill | → Attribute |
|-------|---------|-------------|
| `Perception` | Scouting | Cunning |
| `Smithing` | Crafting | Endurance |

See [Native Skill XP Reference](native-skill-xp.md) for the full skill→attribute mapping.

**Native Implementation:**
```csharp
// From EventDeliveryManager.cs
var skill = SkillCheckHelper.GetSkillByName(skillXp.Key);
hero.AddSkillXp(skill, skillXp.Value);
```

**Native Source:** `TaleWorlds.CampaignSystem/HeroDeveloper.cs` - `AddSkillXp()`

### Dynamic Skill XP (`dynamicSkillXp`)

XP awarded to skills determined at runtime based on player state.

```json
"effects": {
  "dynamicSkillXp": {
    "equipped_weapon": 15,
    "weakest_combat": 10
  }
}
```

| Key | Description |
|-----|-------------|
| `equipped_weapon` | XP to skill matching currently equipped weapon |
| `weakest_combat` | XP to hero's lowest combat skill |

### Trait XP (`traitXp`)

Modifies personality trait values (Mercy, Valor, Honor, Generosity, Calculating).

```json
"effects": {
  "traitXp": {
    "Valor": 100,
    "Mercy": -50
  }
}
```

**Trait Threshold System (from native):**
| XP Range | Trait Level |
|----------|-------------|
| ≤ -4000 | -2 |
| -4000 to -1000 | -1 |
| -1000 to +1000 | 0 |
| +1000 to +4000 | +1 |
| ≥ +4000 | +2 |

**Native Source:** `DefaultCharacterDevelopmentModel.GetTraitLevelForTraitXp()`

---

## Resource Effects

### Gold (`gold`)

Changes player gold. Uses native `GiveGoldAction` for proper tracking.

```json
"effects": { "gold": 50 }     // Player gains 50 gold
"effects": { "gold": -25 }    // Player loses 25 gold
```

**Native API:**
```csharp
// Gaining gold (from nowhere)
GiveGoldAction.ApplyBetweenCharacters(null, hero, amount);

// Losing gold (to nowhere)
GiveGoldAction.ApplyBetweenCharacters(hero, null, amount);
```

**Native Source:** `TaleWorlds.CampaignSystem/Actions/GiveGoldAction.cs`

**Important:** Don't use `Hero.ChangeHeroGold()` directly - it bypasses the party gold UI. Always use `GiveGoldAction`.

### HP Change (`hpChange`)

Modifies player HP directly.

```json
"effects": { "hpChange": -15 }   // Player takes 15 damage
"effects": { "hpChange": 10 }    // Player heals 10 HP
```

**Native Properties:**
```csharp
Hero.HitPoints          // Current HP (get/set)
Hero.MaxHitPoints       // Maximum HP (derived from CharacterObject)
Hero.IsWounded          // True if HP ≤ WoundedHealthLimit
Hero.WoundedHealthLimit // Threshold for "wounded" state
```

**Native Source:** `TaleWorlds.CampaignSystem/Hero.cs`

### Apply Wound (`applyWound`)

Applies a wound severity level, which reduces HP and may trigger medical care.

```json
"effects": { "applyWound": "minor" }
"effects": { "applyWound": "moderate" }
"effects": { "applyWound": "severe" }
```

### Food Loss (`foodLoss`)

Removes food items from player inventory.

```json
"effects": { "foodLoss": 5 }
```

### Renown (`renown`)

Awards clan renown (affects clan tier).

```json
"effects": { "renown": 10 }
```

**Native API:**
```csharp
GainRenownAction.Apply(hero, renownValue);
// Internally calls: hero.Clan.AddRenown(gainedRenown)
```

**Native Source:** `TaleWorlds.CampaignSystem/Actions/GainRenownAction.cs`

---

## Reputation Effects (Enlisted-Specific)

These track the player's standing within the military hierarchy.

### Lord Reputation (`lordRep`)

Relationship with the enlisted lord. Affects assignments, trust, and discharge risk.

```json
"effects": { "lordRep": 3 }    // +3 lord reputation
"effects": { "lordRep": -5 }   // -5 lord reputation
```

### Officer Reputation (`officerRep`)

Relationship with officers (sergeants, captains). Affects daily treatment.

```json
"effects": { "officerRep": 2 }
```

### Soldier Reputation (`soldierRep`)

Relationship with fellow soldiers. Affects morale and peer support.

```json
"effects": { "soldierRep": -3 }
```

**Implementation:** Handled by `EscalationManager.ModifyXxxReputation()`

---

## Escalation Effects (Enlisted-Specific)

Escalation tracks build up over time and can trigger threshold events.

### Scrutiny (`scrutiny`)

How closely officers watch the player. High scrutiny = more inspections/checks.

```json
"effects": { "scrutiny": 5 }    // Officers watching more closely
"effects": { "scrutiny": -3 }   // Heat dies down
```

### Discipline (`discipline`)

Accumulated discipline problems. High discipline = risk of punishment/discharge.

```json
"effects": { "discipline": 10 }
```

### Medical Risk (`medicalRisk`)

Health risk level. High = increased chance of illness/injury events.

```json
"effects": { "medicalRisk": 2 }
```

**Scale:** 0-10 for escalation tracks (unlike 0-100 for company needs).

---

## Company Needs Effects (Enlisted-Specific)

Affect the lord's party state. Each need ranges 0-100.

### Company Needs (`companyNeeds`)

Modify party-wide resource levels.

```json
"effects": {
  "companyNeeds": {
    "Readiness": 10,
    "Morale": -5,
    "Rest": 15,
    "Supplies": -10
  }
}
```

| Need | Description | Affects |
|------|-------------|---------|
| `Readiness` | Combat readiness, training | Battle effectiveness |
| `Morale` | Unit cohesion, spirits | Event options, desertion |
| `Rest` | Fatigue level | Movement speed, performance |
| `Supplies` | Food, water, equipment | Starvation, attrition |

### Fatigue (`fatigue`)

Shorthand for personal fatigue (affects Rest indirectly).

```json
"effects": { "fatigue": 5 }     // Player more tired
"effects": { "fatigue": -10 }   // Player rested
```

---

## Party Effects

### Troop Loss (`troopLoss`)

Soldiers killed/lost from the lord's party.

```json
"effects": { "troopLoss": 3 }
```

### Troop Wounded (`troopWounded`)

Soldiers wounded in the lord's party.

```json
"effects": { "troopWounded": 5 }
```

### Troop XP (`troopXp`)

XP awarded to T1-T3 troops in the party (NCO training scenarios).

```json
"effects": { "troopXp": 50 }
```

**Native API:** `TroopRoster.AddXpToTroop(character, xpAmount)`

---

## Retinue Effects (T7+ Commanders Only)

For players at Commander rank with their own retinue.

### Retinue Gain (`retinueGain`)

Add soldiers to player's retinue.

```json
"effects": { "retinueGain": 2 }
```

### Retinue Loss (`retinueLoss`)

Remove soldiers from player's retinue.

```json
"effects": { "retinueLoss": 1 }
```

### Retinue Wounded (`retinueWounded`)

Wound soldiers in player's retinue.

```json
"effects": { "retinueWounded": 3 }
```

### Retinue Loyalty (`retinueLoyalty`)

Modify loyalty of player's retinue.

```json
"effects": { "retinueLoyalty": 10 }
"effects": { "retinueLoyalty": -15 }
```

---

## Narrative Effects

### Chain Event (`chainEventId`)

Triggers a follow-up event after this one completes.

```json
"effects": { "chainEventId": "follow_up_event_id" }
```

### Triggers Discharge (`triggersDischarge`)

Ends the player's enlistment with specified discharge type.

```json
"effects": { "triggersDischarge": "dishonorable" }
"effects": { "triggersDischarge": "washout" }
"effects": { "triggersDischarge": "deserter" }
```

### Promotes (`promotes`)

Promotes player to specified tier.

```json
"effects": { "promotes": 4 }   // Promote to Tier 4
```

---

## Baggage Effects

### Grant Temporary Baggage Access (`grantTemporaryBaggageAccess`)

Grants temporary access to baggage for specified days.

```json
"effects": { "grantTemporaryBaggageAccess": 3 }
```

### Baggage Delay Days (`baggageDelayDays`)

Delays baggage retrieval by specified days.

```json
"effects": { "baggageDelayDays": 2 }
```

### Random Baggage Loss (`randomBaggageLoss`)

Percentage chance of losing baggage items.

```json
"effects": { "randomBaggageLoss": 25 }
```

### Bag Check Choice (`bagCheckChoice`)

For first-enlistment gear handling.

```json
"effects": { "bagCheckChoice": "surrender" }
"effects": { "bagCheckChoice": "hide" }
"effects": { "bagCheckChoice": "bribe" }
```

---

## Complete Example

```json
{
  "id": "scout_enemy_camp",
  "options": [
    {
      "id": "get_closer",
      "text": "Get closer. Count their numbers.",
      "skillCheck": {
        "skill": "Perception",
        "difficulty": 55
      },
      "effects": {
        "lordRep": 5,
        "skillXp": {
          "Scouting": 20,
          "Athletics": 12
        },
        "gold": 25
      },
      "failEffects": {
        "hpChange": -18,
        "lordRep": 2,
        "skillXp": {
          "Scouting": 10,
          "Athletics": 6
        },
        "scrutiny": 3
      }
    }
  ]
}
```

---

## Native API Quick Reference

| Effect | Native API | Source File |
|--------|------------|-------------|
| Skill XP | `Hero.AddSkillXp(skill, xp)` | `HeroDeveloper.cs` |
| Gold | `GiveGoldAction.ApplyBetweenCharacters()` | `Actions/GiveGoldAction.cs` |
| HP | `Hero.HitPoints += change` | `Hero.cs` |
| Renown | `GainRenownAction.Apply(hero, value)` | `Actions/GainRenownAction.cs` |
| Troop XP | `TroopRoster.AddXpToTroop()` | `Roster/TroopRoster.cs` |

---

## Effect Processing Flow

1. **Player selects option** in event/decision/opportunity
2. **EventDeliveryManager.ApplyEffects()** processes the `EventEffects` object
3. **Each effect type** is applied through appropriate manager:
   - Skill XP → `Hero.AddSkillXp()` via `SkillCheckHelper`
   - Reputation → `EscalationManager.ModifyXxxReputation()`
   - Gold → `GiveGoldAction`
   - Company Needs → `CompanyNeedsState.ModifyNeed()`
4. **Feedback messages** generated for combat log
5. **EnlistmentXP** awarded for rank progression

---

## Content System Integration Points

| System | Uses Effects From | Primary Effect Types |
|--------|-------------------|---------------------|
| **Events** | `effects`, `failEffects` | All types |
| **Decisions** | `effects`, `effects_success`, `effects_failure` | Skill XP, rep, gold |
| **Opportunities** | `effects` | Skill XP, rep |
| **Order Events** | `effects`, `failEffects` | Skill XP, rep, HP |
| **Routines** | Config-driven | Skill XP, needs |
| **Map Incidents** | `effects` | All types |

---

## Validation

The content validator (`Tools/Validation/validate_content.py`) checks:

- All skill names are valid (including aliases)
- Effect values are within expected ranges
- Required effects are present (e.g., skillXp in order events)
- Chain event IDs reference existing events

Run validation before committing:
```powershell
python Tools/Validation/validate_content.py
```
