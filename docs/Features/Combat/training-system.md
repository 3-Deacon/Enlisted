# Training & XP System

**Summary:** The training system allows players to improve combat skills through training events, troop instruction, and combat experience. The system uses weapon-aware training (adapts to equipped weapon), native Bannerlord XP mechanics, and experience track modifiers based on player level. All training actions provide news feed feedback showing skill progress.

**Status:** ✅ Current  
**Last Updated:** 2025-12-22  
**Related Docs:** [Enlistment System](../Core/enlistment.md), [Storylet Backbone](../Content/storylet-backbone.md)

---

## Index

1. [Overview](#overview)
2. [Weapon-Aware Training](#weapon-aware-training)
3. [Troop Training](#troop-training)
4. [Experience Track Modifiers](#experience-track-modifiers)
5. [XP Feedback System](#xp-feedback-system)
6. [Training Events](#training-events)
7. [Implementation Details](#implementation-details)

---

## Overview

The training system provides multiple pathways for skill development:

**Training Events:**
- Weapon-aware decisions that adapt to equipped weapons
- Targeted skill training (Athletics, Riding, Tactics, etc.)
- Troop instruction (Leadership, NCO skills)

**Combat XP:**
- Native Bannerlord combat XP (kills, damage dealt)
- Additional mod XP from duty completions
- Trait-based XP bonuses

**Experience Tracks:**
- Green soldiers (level 1-9): +20% XP from training
- Seasoned soldiers (level 10-20): Normal XP from training
- Veteran soldiers (level 21+): -10% XP from training (diminishing returns)

---

## Weapon-Aware Training

### Dynamic Weapon Detection

Training events can automatically detect the player's equipped weapon and award XP for the corresponding skill:

**Event Structure:**
```json
{
  "option_id": "train_weapon",
  "text": "Practice with your weapon",
  "tooltip": "Train with the weapon you carry into battle",
  "reward_choices": [
    {
      "equipped_weapon_skill": 20,
      "applies_training_modifier": true
    }
  ]
}
```

### Weapon-to-Skill Mapping

| Weapon Type | Skill Awarded |
|-------------|---------------|
| Sword, Axe, Mace | OneHanded |
| Two-Handed Sword, Poleaxe, Pike | TwoHanded |
| Spear (one-handed) | Polearm |
| Bow | Bow |
| Crossbow | Crossbow |
| Javelin, Throwing Axe | Throwing |
| Shield (equipped) | +5 bonus to melee skill |

**Fallback Behavior:**
- If no weapon equipped: Awards Athletics
- If invalid weapon type: Awards OneHanded (default melee)
- Shield bonus: Added to primary weapon XP when shield equipped

### Usage Example

**Event: "Morning Drills"**
```
The training sergeant calls for weapon practice.

Options:
  [1] Practice with your weapon (your equipped longsword)
      → Awards TwoHanded XP 20 (modified by experience track)
  
  [2] Focus on footwork
      → Awards Athletics XP 15
  
  [3] Skip training
      → No XP
```

**Result (Green Track, +20% modifier):**
```
News: "Training Progress"
You practiced weapon handling.
TwoHanded +24 XP (20 base × 1.2 modifier)
```

---

## Troop Training

### NCO Instruction

Players can train troops in their formation or party, granting them XP using native Bannerlord mechanics:

**Requirements:**
- Player must be NCO rank or higher (T3+)
- Troops must be in player's formation or party
- Uses `CharacterDevelopment.AddSkillXp()` (native API)

**Event Structure:**
```json
{
  "option_id": "instruct_troops",
  "text": "Train your soldiers",
  "tooltip": "Improve your squad's combat readiness",
  "effects": {
    "troop_training": {
      "skill": "OneHanded",
      "amount": 50,
      "target": "formation"
    }
  }
}
```

### Troop Training Rules

**XP Distribution:**
- Formation target: All troops in player's formation
- Party target: All troops in player's party (if commanding)
- Amount: Specified XP per troop (typically 30-100)
- Validation: Only applies to valid troops (not heroes, not companions)

**Player Benefits:**
- Leadership XP (25-50) for instructing troops
- Trait XP for NCO/officer traits
- Soldier reputation (+2-5) for helping comrades

**Validation:**
```csharp
// Troops must meet native requirements
bool canGainXp = MobilePartyHelper.CanTroopGainXp(
    party,
    characterObject,
    out TextObject explanation
);
```

---

## Experience Track Modifiers

### Track-Based XP Scaling

Player level determines their experience track, which modifies training XP gains:

| Track | Player Level | Training Modifier | Rationale |
|-------|--------------|-------------------|-----------|
| **Green** | 1-9 | **+20%** | New soldiers learn quickly from drills |
| **Seasoned** | 10-20 | **Normal** | Standard progression |
| **Veteran** | 21+ | **-10%** | Diminishing returns, learn more from combat |

**Implementation:**
```csharp
public static float GetTrainingXpModifier(string experienceTrack)
{
    return experienceTrack switch
    {
        "green" => 1.2f,      // +20%
        "seasoned" => 1.0f,   // Normal
        "veteran" => 0.9f,    // -10%
        _ => 1.0f
    };
}
```

### When Modifiers Apply

**Applied To:**
- Training event XP (weapon-aware, targeted skill training)
- Decision-based training XP
- Camp training activities

**NOT Applied To:**
- Combat XP (kills, damage)
- Quest rewards
- Fixed event bonuses (reputation, gold)

### Example Progression

**Level 5 Player (Green Track):**
```
Training Event: "Weapon Practice" (20 OneHanded XP)
Modifier: +20%
Result: 20 × 1.2 = 24 OneHanded XP
```

**Level 15 Player (Seasoned Track):**
```
Training Event: "Weapon Practice" (20 OneHanded XP)
Modifier: Normal
Result: 20 × 1.0 = 20 OneHanded XP
```

**Level 25 Player (Veteran Track):**
```
Training Event: "Weapon Practice" (20 OneHanded XP)
Modifier: -10%
Result: 20 × 0.9 = 18 OneHanded XP
```

---

## XP Feedback System

### News Feed Integration

All training XP awards display in the news feed system:

**Personal Dispatch Format:**
```
News Category: "training"
Title: "Training Progress"
Text: "You practiced [skill] and gained [amount] XP."
```

**Example Outputs:**

**Single Skill:**
```
Training Progress
You practiced weapon handling.
OneHanded +25 XP
```

**Multiple Skills:**
```
Training Progress
You completed combat drills.
OneHanded +20 XP
Athletics +15 XP
Tactics +10 XP
```

**Troop Training:**
```
Training Progress
You instructed your soldiers in weapon handling.
Your troops gained OneHanded +50 XP
You gained Leadership +30 XP
```

### News Feed Categories

| Category | Usage | Examples |
|----------|-------|----------|
| `"training"` | Personal skill training | Weapon practice, athletics drills |
| `"instruction"` | Teaching troops | NCO training, formation drills |
| `"combat"` | Combat XP (future) | Battle performance, kills |

---

## Training Events

### Event Types

**Weapon-Focused Training:**
- Morning drills (weapon practice)
- Sparring matches
- Combat form refinement

**Targeted Skill Training:**
- Athletics (running, endurance)
- Riding (horsemanship)
- Tactics (strategic thinking)
- Leadership (commanding presence)

**Troop Instruction:**
- Formation drills (NCO+)
- Weapon instruction (NCO+)
- Tactical training (Officer+)

### Event Structure Example

```json
{
  "event_id": "evt_training_weapon_drills",
  "title": "Weapon Drills",
  "setup": "The training sergeant calls for practice.",
  "options": [
    {
      "option_id": "equipped_weapon",
      "text": "Practice with your weapon",
      "tooltip": "Train with the weapon you carry",
      "reward_choices": [
        {
          "equipped_weapon_skill": 25,
          "applies_training_modifier": true
        }
      ]
    },
    {
      "option_id": "athletics",
      "text": "Focus on footwork",
      "tooltip": "Build stamina and agility",
      "reward_choices": [
        {
          "Athletics": 20,
          "applies_training_modifier": true
        }
      ]
    },
    {
      "option_id": "skip",
      "text": "Skip training today",
      "tooltip": "Rest instead of training",
      "effects": {
        "fatigue": -1
      }
    }
  ]
}
```

---

## Implementation Details

### Source Files

| File | Purpose |
|------|---------|
| `src/Features/Content/RewardChoices.cs` | Weapon-aware XP parsing and application |
| `src/Features/Content/EventDeliveryManager.cs` | Training event processing |
| `src/Features/Content/ExperienceTrackHelper.cs` | Experience track modifiers |
| `src/Features/News/PersonalDispatchManager.cs` | Training XP news display |
| `ModuleData/Enlisted/Events/events_training.json` | Training event definitions |

### Key Methods

**Weapon Detection:**
```csharp
public static SkillObject GetEquippedWeaponSkill(Hero hero)
{
    var weapon = GetPrimaryWeapon(hero);
    if (weapon == null) return DefaultSkills.Athletics;
    
    return weapon.PrimaryWeapon.ItemUsage switch
    {
        "long_bow" => DefaultSkills.Bow,
        "crossbow" => DefaultSkills.Crossbow,
        "javelin" => DefaultSkills.Throwing,
        "two_handed_sword" => DefaultSkills.TwoHanded,
        "one_handed_sword" => DefaultSkills.OneHanded,
        "polearm" => DefaultSkills.Polearm,
        _ => DefaultSkills.OneHanded
    };
}
```

**Training Modifier Application:**
```csharp
private int ApplyTrainingModifier(int baseXp, bool applyModifier)
{
    if (!applyModifier) return baseXp;
    
    string track = ExperienceTrackHelper.GetExperienceTrack(
        Hero.MainHero.Level);
    float modifier = ExperienceTrackHelper.GetTrainingXpModifier(track);
    
    return (int)(baseXp * modifier);
}
```

**Troop Training:**
```csharp
public void ApplyTroopTraining(TroopTraining training)
{
    var party = MobileParty.MainParty;
    int troopsAffected = 0;
    
    foreach (var element in party.MemberRoster.GetTroopRoster())
    {
        if (!element.Character.IsHero && 
            MobilePartyHelper.CanTroopGainXp(party, element.Character, out _))
        {
            element.Character.GetSkillValue(training.Skill)
                .AddXp(training.Amount);
            troopsAffected++;
        }
    }
    
    ModLogger.Info("Training", 
        $"Trained {troopsAffected} troops in {training.Skill.Name}");
}
```

### Configuration

Training XP values and modifiers can be adjusted in:
- Event JSON files (`events_training.json` for events, `Decisions/decisions.json` for decisions)
- Experience track configuration (code-based)
- News feed templates (`enlisted_strings.xml`)

---

**End of Document**

