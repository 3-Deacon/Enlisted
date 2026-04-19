# Traits & Identity System

**Summary:** The identity system allows player personality to emerge from in-game choices through native Bannerlord trait integration. Player identity is defined by personality traits (Mercy, Valor, Honor), specialist skills (Surgeon, Scout, Commander), rank progression, and relationship with their lord.

**Status:** ✅ Current  
**Last Updated:** 2026-01-14 (Updated to reflect Scrutiny/lord relation system)  
**Related Docs:** [Content System](../Content/content-system-architecture.md), [Event System Schemas](../Content/event-system-schemas.md), [Promotion System](../Core/promotion-system.md)

---

## Table of Contents

1. [Overview](#overview)
2. [Identity Components](#identity-components)
3. [Native Trait System](#native-trait-system)
4. [Escalation System](#escalation-system)
5. [NCO & Soldier Names](#nco--soldier-names)
6. [Event Integration](#event-integration)
7. [Implementation Reference](#implementation-reference)

---

## Overview

### Design Philosophy

**Choice-based identity** using native Bannerlord traits:
- Player identity emerges from event choices, not menu selections
- Native personality traits (Mercy, Valor, Honor, etc.) track moral/behavioral patterns
- Rank progression (T1-T9) gates access to content
- Lord relationship uses native `Hero.GetRelation()` system
- Scrutiny track (0-100) measures trouble/disciplinary issues
- High replayability through trait combinations affecting available content

### Key Systems

**Rank Progression (T1-T9)**
- Clear progression milestones
- Culture-specific rank names
- Equipment access tied to rank
- See [Promotion System](../Core/promotion-system.md)

**Trait System**
- Native Bannerlord personality traits (-2 to +2)
- Hidden specialist traits (0-20)
- Affects event availability and options

**Escalation System**
- **Scrutiny** (0-100): Trouble/suspicion level
- **Lord Relation**: Uses native `Hero.GetRelation()` (-100 to +100)
- **Medical Risk** (0-5): Injury/illness severity

---

## Identity Components

### Complete Matrix

| Component | Type | Range | Purpose |
|-----------|------|-------|---------|
| **Rank** | Progression | T1-T9 | Experience level, access gating |
| **Mercy** | Personality | -2 to +2 | Compassion vs. cruelty |
| **Valor** | Personality | -2 to +2 | Courage vs. caution |
| **Honor** | Personality | -2 to +2 | Integrity vs. pragmatism |
| **Generosity** | Personality | -2 to +2 | Loyalty vs. selfishness |
| **Calculating** | Personality | -2 to +2 | Strategic vs. impulsive |
| **Commander** | Hidden Skill | 0-20 | Leadership capability |
| **Surgeon** | Hidden Skill | 0-20 | Medical proficiency |
| **Sergeant** | Hidden Skill | 0-20 | NCO capability |
| **Engineer** | Hidden Skill | 0-20 | Siege/construction |
| **Rogue** | Hidden Skill | 0-20 | Criminal/stealth |
| **Scout** | Hidden Skill | 0-20 | Reconnaissance |
| **Trader** | Hidden Skill | 0-20 | Commerce |
| **Smuggler** | Hidden Skill | 0-20 | Black market specialty |
| **Thug** | Hidden Skill | 0-20 | Enforcement specialty |
| **Lord Relation** | Social | -100 to +100 | Trust/loyalty with lord (native) |
| **Scrutiny** | Escalation | 0-100 | Trouble/suspicion level |
| **Medical Risk** | Condition | 0-5 | Injury/illness severity |

---

## Native Trait System

### Personality Traits (Visible, -2 to +2)

```csharp
DefaultTraits.Mercy        // Aversion to suffering, helping strangers/enemies
DefaultTraits.Valor        // Risking life for glory/wealth/cause
DefaultTraits.Honor        // Keeping commitments, respecting law
DefaultTraits.Generosity   // Loyalty to kin/servants, gratitude
DefaultTraits.Calculating  // Controlling emotions for long-term gain
```

**XP Thresholds:**
```
Level -2: -1000 XP (Cruel, Cowardly, Devious, Closefisted, Impulsive)
Level -1: -500 XP
Level  0: 0 XP (default)
Level +1: +500 XP
Level +2: +1000 XP (Merciful, Valorous, Honorable, Generous, Calculating)
```

### Hidden Skill Traits (0-20)

```csharp
DefaultTraits.Commander             // Leadership capability
DefaultTraits.Surgeon               // Medical skill proficiency
DefaultTraits.SergeantCommandSkills // NCO capability
DefaultTraits.EngineerSkills        // Engineering/siege warfare
DefaultTraits.RogueSkills           // Criminal/stealth abilities
DefaultTraits.ScoutSkills           // Scouting/reconnaissance
DefaultTraits.Trader                // Trade/barter skill
DefaultTraits.Blacksmith            // Crafting skill
DefaultTraits.Thug                  // Gang enforcement specialty
DefaultTraits.Smuggler              // Black market specialty
```

**XP Progression:**
```
Each level requires ~100 XP
Level 5: 500 XP (noticeable)
Level 10: 1000 XP (significant)
Level 15: 1500 XP (major)
Level 20: 2000 XP (mastery)
```

### Core API Methods

**Reading Traits:**
```csharp
// Get trait level
int mercyLevel = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy);
// Returns: -2 (Cruel) to +2 (Merciful) for personality traits
// Returns: 0-20 for hidden traits

// Check if trait is positive
bool isMerciful = Hero.MainHero.GetTraitLevel(DefaultTraits.Mercy) > 0;
```

**Changing Traits:**
```csharp
// Recommended: Use native helper (includes logging)
TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Honor, 50);

// Shows notification banner in-game automatically when level changes
```

### Event Integration

**Applying Trait XP in Events:**
```csharp
// In EventDeliveryManager
private void ApplyTraitEffect(string traitId, int value)
{
    var trait = GetTraitFromId(traitId);
    if (trait != null)
    {
        TraitLevelingHelper.OnIncidentResolved(trait, value);
        ModLogger.Info("Events", $"Trait {traitId} changed by {value}");
    }
}
```

**JSON Schema:**
```json
{
  "effects": {
    "traitXp": {
      "mercy": 50,     // +50 XP toward Mercy
      "honor": -80,    // -80 XP (toward Devious)
      "valor": 100,    // +100 XP toward Valor
      "commander": 150 // +150 XP toward Commander
    }
  }
}
```

---

## Escalation System

### Current Tracks

**Scrutiny (0-100)**
- **Purpose**: Measures trouble/suspicion level with command
- **Gained From**: Order failures, insubordination, criminal activity, theft, suspicious behavior
- **Reduced By**: Passive decay over time with good behavior
- **Impact**: Blocks promotion when too high, triggers discharge at 100
- **Scale Note**: Merged from old Discipline (0-10) system - now 0-100 for finer granularity

**Lord Relation (-100 to +100)**
- **Purpose**: Your relationship with your enlisted lord
- **System**: Uses native Bannerlord `Hero.GetRelation()` - integrates with vanilla
- **Gained From**: Order success, loyalty, combat performance, helpful actions
- **Lost Through**: Order failures, disobedience, poor performance, criminal activity
- **Impact**: Required for promotions (T3+ requires 10+, T5+ requires 30+, etc.)

**Medical Risk (0-5)**
- **Purpose**: Injury/illness severity
- **Gained From**: Taking damage, poor treatment, ignoring injuries
- **Reduced By**: Rest, medical treatment
- **Impact**: Affects available decisions, can trigger medical events

### Implementation

**File:** `src/Features/Escalation/EscalationState.cs`

```csharp
[Serializable]
public sealed class EscalationState
{
    // Track values
    public int Scrutiny { get; set; }           // 0-100
    public int LordReputation { get; set; }     // 0-100 (pending full native migration)
    public int MedicalRisk { get; set; }        // 0-5
    
    // Constants
    public const int ScrutinyMin = 0;
    public const int ScrutinyMax = 100;
    public const int LordReputationMin = 0;
    public const int LordReputationMax = 100;
    public const int MedicalRiskMin = 0;
    public const int MedicalRiskMax = 5;
}
```

**Accessing in Code:**
```csharp
var escalation = EscalationManager.Instance;
int scrutiny = escalation.State?.Scrutiny ?? 0;
int lordRep = escalation.State?.LordReputation ?? 0;

// Or use native lord relation directly:
var relation = enlistment.EnlistedLord.GetRelationWithPlayer();
```

---

## NCO & Soldier Names

Personalized NPC names for event dialogue without simulation overhead.

### NCO (Non-Commissioned Officer)

Each enlistment generates a single persistent NCO:
- Name generated using Bannerlord's `NameGenerator` for lord's culture
- Rank pulled from `progression_config.json` (T5 tier for lord's culture)
- Persists in save data for duration of service
- Cleared on discharge, regenerated on re-enlistment

### Soldier Name Pool

Pool of 3 soldier names for personalized event dialogue:
- Generated using `NameGenerator` for lord's culture
- ~20% female names based on culture settings
- Used randomly when events reference comrades

### Text Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `{SERGEANT}` | NCO full name with rank | "Sergeant Aldric" |
| `{NCO_NAME}` | Same as SERGEANT | "Sergeant Aldric" |
| `{NCO_RANK}` | Just the rank title | "Sergeant" |
| `{COMRADE_NAME}` | Random soldier name | "Bjorn" |
| `{SOLDIER_NAME}` | Same as COMRADE_NAME | "Bjorn" |
| `{COMPANY_NAME}` | Lord's party name | "Derthert's Army" |
| `{PLAYER_NAME}` | Player's first name | "Aeldred" |
| `{PLAYER_RANK}` | Player's current rank | "Corporal" |
| `{LORD_NAME}` | Enlisted lord's name | "Derthert" |
| `{LORD_TITLE}` | "Lord" or "Lady" | "Lord" |

### Culture Rank Examples

| Culture | NCO Rank (T5) | Example |
|---------|---------------|---------|
| Empire | Evocatus | "Evocatus Marcus" |
| Vlandia | Sergeant | "Sergeant Aldric" |
| Sturgia | Huskarl | "Huskarl Bjorn" |
| Aserai | Muqaddam | "Muqaddam Farid" |
| Khuzait | Torguud | "Torguud Temur" |
| Battania | Fiann | "Fiann Brennan" |

---

## Event Integration

### Event Requirements

**Trait-Based Filtering:**
```json
{
  "requirements": {
    "tier": { "min": 3, "max": 6 },
    "minTraits": {
      "Mercy": 1,        // Requires Merciful (+1 or +2)
      "Commander": 5     // Requires Commander trait level 5+
    }
  }
}
```

**Lord Relation Requirements:**
```json
{
  "requirements": {
    "minEscalation": {
      "LordReputation": 30  // Requires lord relation 30+
    }
  }
}
```

**Scrutiny Requirements:**
```json
{
  "requirements": {
    "minEscalation": {
      "Scrutiny": 50  // Only fires when Scrutiny is 50+
    }
  }
}
```

### Event Effects

**Trait XP:**
```json
{
  "effects": {
    "traitXp": {
      "Honor": 100,     // +100 XP toward Honor
      "Commander": 50   // +50 XP toward Commander
    }
  }
}
```

**Lord Relation:**
```json
{
  "effects": {
    "lordRep": 20   // +20 to lord relation
  }
}
```

**Scrutiny:**
```json
{
  "effects": {
    "scrutiny": -10  // Reduce scrutiny by 10
  }
}
```

### Complete Event Example

```json
{
  "id": "moral_choice_prisoners",
  "category": "duty",
  "requirements": {
    "tier": { "min": 3 },
    "minTraits": {
      "Mercy": 1
    }
  },
  "content": {
    "title": "Wounded Prisoners",
    "setup": "After the battle, you find enemy wounded. Your men want to finish them.",
    "options": [
      {
        "id": "treat_them",
        "text": "Treat their wounds. They're defeated, not animals.",
        "effects": {
          "traitXp": {
            "Mercy": 80,
            "Surgeon": 50
          },
          "lordRep": -5,
          "scrutiny": -5
        }
      },
      {
        "id": "execute_them",
        "text": "They're a liability. End it quickly.",
        "effects": {
          "traitXp": {
            "Mercy": -80,
            "Calculating": 40
          },
          "lordRep": 5,
          "scrutiny": 10
        }
      }
    ]
  }
}
```

---

## Implementation Reference

### Key Files

```
src/Features/Escalation/
├── EscalationManager.cs       # Manages escalation tracks
├── EscalationState.cs         # State storage (Scrutiny, LordRep, MedicalRisk)
└── EscalationThresholds.cs   # Threshold definitions

src/Features/Content/
├── EventCatalog.cs            # Event loading and lookup
├── EventDeliveryManager.cs    # Event delivery and trait XP application
└── EventRequirementChecker.cs # Trait/escalation requirement validation

src/Features/Ranks/
├── PromotionBehavior.cs       # Checks Scrutiny and lord relation for promotions
└── RankHelper.cs              # Culture-specific rank titles

src/Features/Enlistment/Behaviors/
└── EnlistmentBehavior.cs      # NCO/soldier name generation
```

### Trait Helper

```csharp
// File: src/Features/Traits/SkillCheckHelper.cs
public static class SkillCheckHelper
{
    public static TraitObject GetTraitByName(string traitName)
    {
        return traitName?.ToLower() switch
        {
            "mercy" => DefaultTraits.Mercy,
            "valor" => DefaultTraits.Valor,
            "honor" => DefaultTraits.Honor,
            "generosity" => DefaultTraits.Generosity,
            "calculating" => DefaultTraits.Calculating,
            "commander" => DefaultTraits.Commander,
            "surgeon" => DefaultTraits.Surgeon,
            "sergeant" => DefaultTraits.SergeantCommandSkills,
            "engineer" => DefaultTraits.EngineerSkills,
            "rogue" => DefaultTraits.RogueSkills,
            "scout" => DefaultTraits.ScoutSkills,
            _ => null
        };
    }
    
    public static bool MeetsTraitThreshold(Hero hero, TraitObject trait, int requiredLevel)
    {
        return hero.GetTraitLevel(trait) >= requiredLevel;
    }
}
```

### Event Effect Application

```csharp
// In EventDeliveryManager.cs
private void ApplyTraitXp(Dictionary<string, int> traitXp)
{
    if (traitXp == null || traitXp.Count == 0)
        return;
    
    foreach (var kvp in traitXp)
    {
        var trait = SkillCheckHelper.GetTraitByName(kvp.Key);
        if (trait != null)
        {
            TraitLevelingHelper.OnIncidentResolved(trait, kvp.Value);
            ModLogger.Debug(LogCategory, $"Applied {kvp.Value} XP to {kvp.Key}");
        }
    }
}
```

### Promotion Requirements Check

```csharp
// In PromotionBehavior.cs (lines 232-250)
var scrutiny = escalation.State?.Scrutiny ?? 0;
if (scrutiny >= req.MaxScrutiny)
{
    reasons.Add($"Scrutiny too high: {scrutiny} (max: {req.MaxScrutiny - 1})");
}

var relation = enlistment.EnlistedLord.GetRelationWithPlayer();
if (relation < req.MinLeaderRelation)
{
    reasons.Add($"Leader relation: {relation}/{req.MinLeaderRelation}");
}
```

---

## Summary

The identity system uses:
- **Native Bannerlord traits** for personality (Mercy, Valor, Honor, etc.)
- **Native `Hero.GetRelation()`** for lord relationship
- **Scrutiny (0-100)** for trouble/disciplinary tracking
- **Rank progression (T1-T9)** for experience gating
- **Event choices** to shape character identity organically

This creates emergent player identities through choices rather than menu selections, with high replayability through different trait combinations unlocking different content paths.
