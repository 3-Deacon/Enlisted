# Native Skill XP and Leveling

**Summary:** Research notes documenting how Bannerlord awards skill XP, converts XP into skill levels, and advances hero level (attributes/focus points). This reference helps design Enlisted training, decisions, and schedule rewards that integrate with native progression mechanics.

**Status:** Reference  
**Last Updated:** 2026-01-03 (expanded with native decompile details, attribute-skill hierarchy, content aliases)  
**Related Docs:** [Training System](../Features/Combat/training-system.md), [Content System Architecture](../Features/Content/content-system-architecture.md)

---

## Overview

Bannerlord has **two related but distinct progression tracks**:

- **Skill progression** (e.g., `OneHanded`, `Riding`, `Charm`):
  - XP is stored per skill and can increase the **skill value** when thresholds are crossed.
  - XP gain is affected by **learning rate** (focus + attributes + learning limit) and a small **global XP multiplier** model.

- **Hero "level" progression** (character level):
  - Driven by the hero's **total accumulated raw XP** (tracked in the hero developer).
  - Level-ups grant **focus points** (every level) and **attribute points** (every N levels).

For Enlisted, the actionable takeaway is:

- When we want native skill progression behavior, we should award XP via **`Hero.AddSkillXp(skill, xp)`**, and let native systems handle learning rates and level-ups.

---

## Bannerlord Character Progression System

Bannerlord uses a **two-tier progression system**:

### Tier 1: Attributes (6 total)

**Attributes** are the broad character capabilities. Heroes invest **attribute points** (gained every 4 levels) into these. Each attribute governs 3 skills.

| Attribute | StringId | Description (from native) |
|-----------|----------|---------------------------|
| **Vigor** | `vigor` | Ability to move with speed and force. Important for melee combat. |
| **Control** | `control` | Ability to use strength without sacrificing precision. Necessary for ranged weapons. |
| **Endurance** | `endurance` | Ability to perform taxing physical activity for a long time. |
| **Cunning** | `cunning` | Ability to predict what other people will do, and outwit their plans. |
| **Social** | `social` | Ability to understand people's motivations and sway them. |
| **Intelligence** | `intelligence` | Aptitude for reading and theoretical learning. |

**Native Source:** `TaleWorlds.Core/DefaultCharacterAttributes.cs`

### Tier 2: Skills (18 total, 3 per attribute)

**Skills** are specific competencies. Heroes invest **focus points** (gained every level) and earn **skill XP** through activities. Higher attribute + focus = faster skill learning.

| Attribute | Skills | Internal StringId | Display Name |
|-----------|--------|-------------------|--------------|
| **Vigor** | OneHanded, TwoHanded, Polearm | `OneHanded`, `TwoHanded`, `Polearm` | One Handed, Two Handed, Polearm |
| **Control** | Bow, Crossbow, Throwing | `Bow`, `Crossbow`, `Throwing` | Bow, Crossbow, Throwing |
| **Endurance** | Riding, Athletics, Crafting | `Riding`, `Athletics`, `Crafting` | Riding, Athletics, **Smithing*** |
| **Cunning** | Scouting, Tactics, Roguery | `Scouting`, `Tactics`, `Roguery` | Scouting, Tactics, Roguery |
| **Social** | Charm, Leadership, Trade | `Charm`, `Leadership`, `Trade` | Charm, Leadership, Trade |
| **Intelligence** | Steward, Medicine, Engineering | `Steward`, `Medicine`, `Engineering` | Steward, Medicine, Engineering |

*\*Note: The Crafting skill's internal StringId is `Crafting` but displays as "Smithing" in-game (`{=smithingskill}Smithing`)*

**Native Source:** `TaleWorlds.Core/DefaultSkills.cs`

### Skill → Attribute Relationship

From `DefaultSkills.InitializeAll()`, each skill explicitly declares its parent attribute:

```csharp
// Example from native decompile
this._skillScouting.Initialize(
    new TextObject("{=LJ6Krlbr}Scouting"),
    new TextObject("{=kmBxaJZd}Knowledge of how to scan the wilderness..."),
    new CharacterAttribute[1] { DefaultCharacterAttributes.Cunning }  // Parent attribute
);
```

The `SkillObject.Attributes` property is an array, but in vanilla Bannerlord each skill has exactly one parent attribute.

---

## Content Aliases (Thematic Skill Names)

Content authors can use **thematic terms** in JSON instead of Bannerlord's internal skill names. The code automatically maps them to the real skill.

### Why Use Aliases?

1. **Immersive content** - "Perception" sounds better than "Scouting" in narrative text
2. **Attribute tracking** - know which attribute your content benefits
3. **Future-proof** - if we change mappings, content doesn't need updating

### Current Alias Mappings

| Thematic Alias | Maps To Skill | Parent Attribute | Use In Content For |
|----------------|---------------|------------------|-------------------|
| `Perception` | Scouting | Cunning | Guard duty, sentry, spotting, observation |
| `Smithing` | Crafting | Endurance | Equipment repair, maintenance, forging |

### Using Aliases in JSON

Aliases work in **both** `skillCheck` and `skillXp` fields:

```json
{
  "skillCheck": {
    "skill": "Perception",
    "difficulty": 40
  },
  "effects": {
    "skillXp": {
      "Perception": 20
    }
  }
}
```

The code maps `"Perception"` → `Scouting` → awards XP to `DefaultSkills.Scouting` → benefits `Cunning` attribute.

### Code Implementation

Mappings are handled in:
- `SkillCheckHelper.GetSkillByName()` - for skill checks and XP effects
- `CampRoutineProcessor.GetSkillFromName()` - for routine outcomes

Both use the same switch expression pattern:

```csharp
var mappedSkill = normalizedName switch
{
    "perception" => DefaultSkills.Scouting,
    "smithing" => DefaultSkills.Crafting,
    _ => null
};
```

---

## Full Skill Reference

| Skill | Attribute | API | Enlisted Activities |
|-------|-----------|-----|---------------------|
| OneHanded | Vigor | `DefaultSkills.OneHanded` | Sword drills, sparring |
| TwoHanded | Vigor | `DefaultSkills.TwoHanded` | Heavy weapon training |
| Polearm | Vigor | `DefaultSkills.Polearm` | Pike drills, formation |
| Bow | Control | `DefaultSkills.Bow` | Archery practice |
| Crossbow | Control | `DefaultSkills.Crossbow` | Crossbow drills |
| Throwing | Control | `DefaultSkills.Throwing` | Javelin practice |
| Riding | Endurance | `DefaultSkills.Riding` | Mounted patrol, cavalry |
| Athletics | Endurance | `DefaultSkills.Athletics` | Formation, marching, labor |
| Crafting | Endurance | `DefaultSkills.Crafting` | Equipment repair (displays as "Smithing") |
| Scouting | Cunning | `DefaultSkills.Scouting` | Patrol, sentry, recon |
| Tactics | Cunning | `DefaultSkills.Tactics` | Battle planning, drills |
| Roguery | Cunning | `DefaultSkills.Roguery` | Stealth, night ops |
| Charm | Social | `DefaultSkills.Charm` | Social events, morale |
| Leadership | Social | `DefaultSkills.Leadership` | Command, NCO duties |
| Trade | Social | `DefaultSkills.Trade` | Quartermaster, logistics |
| Steward | Intelligence | `DefaultSkills.Steward` | Camp management |
| Medicine | Intelligence | `DefaultSkills.Medicine` | Treating wounded |
| Engineering | Intelligence | `DefaultSkills.Engineering` | Fortifications, siege |

### Native API

```csharp
// Get a skill's parent attribute (SkillObject.Attributes is an array)
var skill = DefaultSkills.Scouting;
var attribute = skill.Attributes[0];  // Returns DefaultCharacterAttributes.Cunning

// Get attribute for any skill name (including aliases)
public static CharacterAttribute GetAttributeForSkill(string skillName)
{
    var skill = SkillCheckHelper.GetSkillByName(skillName);
    return skill?.Attributes?.Length > 0 ? skill.Attributes[0] : null;
}

// Example: "Perception" → Scouting → Cunning
var attr = GetAttributeForSkill("Perception");
// attr.StringId == "cunning"
```

**Native Source:** `TaleWorlds.Core/SkillObject.cs`
- `SkillObject.Attributes` - array of parent attributes (always length 1 in vanilla)
- `SkillObject.HowToLearnSkillText` - localized hint text via `GameTexts.FindText("str_how_to_learn_skill", stringId)`

---

## Primary Native Reference Files (Local Decompile)

Use the local decompile as the authority (paths below are in this repo's decompile workspace):

- **Skills list and attributes**
  - `Decompile/TaleWorlds.Core/DefaultSkills.cs`
  - `Decompile/TaleWorlds.Core/SkillObject.cs`

- **Skill XP storage and application**
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/CharacterDevelopment/HeroDeveloper.cs`

- **Skill XP thresholds + learning rate + hero level thresholds**
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/GameComponents/DefaultCharacterDevelopmentModel.cs`
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/ComponentInterfaces/CharacterDevelopmentModel.cs`

- **Generic XP multiplier**
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/ComponentInterfaces/GenericXpModel.cs`
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/GameComponents/DefaultGenericXpModel.cs`

- **Example: combat skill XP awarding**
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/CharacterDevelopment/DefaultSkillLevelingManager.cs`

- **Combat XP calculation and awarding**
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/ComponentInterfaces/CombatXpModel.cs`
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/GameComponents/DefaultCombatXpModel.cs`
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/AgentOrigins/SimpleAgentOrigin.cs`
  - `Decompile/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/AgentOrigins/PartyAgentOrigin.cs`
  - `Decompile/TaleWorlds.MountAndBlade/TaleWorlds/MountAndBlade/CustomBattleAgentLogic.cs`
  - `Decompile/TaleWorlds.Core/IAgentOriginBase.cs`

---

## How Combat XP Works (Kill/Damage in Battle)

When a player or any hero deals damage in battle, the XP system follows this flow:

### 1. Combat Hit Detection

When an agent hits another agent, `CustomBattleAgentLogic.OnAgentHit()` is called. It determines:

- **Is the hit fatal?** `affectedAgent.Health - blow.InflictedDamage < 1.0`
- **Is it team kill?** `affectedAgent.Team.Side == affectorAgent.Team.Side`

Then calls: `affectorAgent.Origin.OnScoreHit(victim, captain, damage, isFatal, isTeamKill, weapon)`

Key parameters:
- `victim` - The character who was hit
- `captain` - The attacker's formation captain (can provide bonuses)
- `damage` - Actual damage dealt
- `isFatal` - Whether this hit killed the target
- `isTeamKill` - Whether this was friendly fire (no XP if true)
- `weapon` - Weapon component data (determines which skill to train)

### 2. Agent Origin XP Handling

**Critical distinction**: Only certain agent origins award XP, and only to heroes.

**`SimpleAgentOrigin.OnScoreHit()`** (AWARDS XP):
- Used for heroes in most singleplayer scenarios
- Checks: `if (!isTeamKill)` - no XP for friendly fire
- Calls `CombatXpModel.GetXpFromHit()` to calculate XP
- If troop is a hero and has a weapon: `troop.HeroObject.AddSkillXp(skill, xp)`

**`PartyAgentOrigin.OnScoreHit()`** (NO XP):
- Used for regular troops in parties
- This method is empty - regular troops don't gain XP from combat hits
- Troops gain XP through other systems (party training, battle recovery, etc.)

### 3. Combat XP Calculation (`DefaultCombatXpModel`)

The XP calculation formula (simplified):

```
baseXP = 0.4 * (attackerPower + 0.5) * (victimPower + 0.5) * (min(damage, victimMaxHP) + (isFatal ? victimMaxHP : 0)) * missionTypeMultiplier
```

**Components**:
- **Attacker power**: `MilitaryPowerModel.GetTroopPower(attackerTroop, side, context, leaderModifier)`
- **Victim power**: `MilitaryPowerModel.GetTroopPower(victimTroop, oppositeSide, context, leaderModifier)`
- **Damage component**: Both the damage dealt AND full HP if it's a killing blow
- **Mission type multipliers**:
  - Battle: 1.0
  - SimulationBattle: 0.9
  - Tournament: 0.33
  - PracticeFight: 0.0625 (1/16)
  - NoXp: 0.0

**Perk bonuses** (applied via `GetBattleXpBonusFromPerks`):
- Leadership.InspiringLeader (if captain has perk): +20% XP
- OneHanded.Trainer: bonus for melee troops
- TwoHanded.BaptisedInBlood: bonus for two-handed users
- Throwing.Resourceful: bonus for troops with throwing weapons
- OneHanded.CorpsACorps: bonus for infantry
- OneHanded.LeadByExample: general bonus
- Crossbow.MountedCrossbowman: bonus for mounted crossbowmen
- Bow.BullsEye: bonus for archers
- Roguery.NoRestForTheWicked: bonus when fighting bandits
- Various garrison perks if in settlement

### 4. Weapon Skill Mapping (`GetSkillForWeapon`)

The XP is awarded to the skill matching the weapon used:

- Siege engine hits -> `Engineering`
- Weapon-specific:
  - One-handed weapons -> `OneHanded`
  - Two-handed weapons -> `TwoHanded`
  - Polearms -> `Polearm`
  - Bows -> `Bow`
  - Crossbows -> `Crossbow`
  - Thrown weapons -> `Throwing`
  - No weapon/unarmed -> `Athletics`

**Source**: `weapon.RelevantSkill` property determines the skill.

### 5. Final XP Application

The calculated XP is passed to `Hero.AddSkillXp(skill, xp)`, which:
- Applies the generic XP multiplier
- Applies the learning rate (based on focus and attributes)
- Adds to skill XP pool
- Checks for skill level-up
- Adds to total hero XP for level progression

### Key Takeaways for Enlisted

1. **Only heroes gain XP from combat hits** - Regular troops use different progression systems
2. **XP scales with both attacker and victim power** - Killing stronger enemies gives more XP
3. **Killing blows give extra XP** - The victim's full HP is added to the calculation
4. **Formation captains matter** - Some perks grant bonuses if your captain has them
5. **Team kills give no XP** - Friendly fire is not rewarded
6. **The weapon determines the skill** - One-handed sword = OneHanded XP, bow = Bow XP, etc.
7. **Perk bonuses stack** - Multiple applicable perks can significantly boost XP gain

---

## How Troop (Non-Hero) XP Works

Regular troops in parties don't gain XP from combat hits directly. Instead, they gain XP through several other systems:

### 1. Party Training Behavior (`MobilePartyTrainingBehavior`)

Daily passive training for troops in mobile parties:
- Calculates effective daily XP based on party size, leader skills, and perks
- Calls `party.MemberRoster.AddXpToTroop(troop, xpAmount)` for each troop
- Training-related perks (e.g., Bow.Trainer) can grant additional XP

### 2. Battle Recovery (`CampaignBattleRecoveryBehavior`)

After battles, surviving wounded troops may gain XP:
- `party.MemberRoster.AddXpToTroop(troop, xp * count)`
- Represents experience gained from surviving combat

### 3. Garrison Training (`GarrisonRecruitmentCampaignBehavior`)

Troops in town garrisons receive daily training XP:
- `town.GarrisonParty.MemberRoster.AddXpToTroop(troop, dailyXpBonus * xpMultiplier * troopCount)`
- Scales with town prosperity and governor perks

### 4. Recruitment and Leadership Perks

When recruiting troops:
- `recruiter.PartyBelongedTo.MemberRoster.AddXpToTroop(troop, xp * count)` (Leadership.FamousCommander perk)
- Newly recruited troops can start with bonus XP

### 5. Prisoner Training

Prisoners can gain XP while held:
- `mobileParty.PrisonRoster.AddXpToTroop(troop, xpAmount)`

### 6. Siege Engineering Work

Troops working on siege engines gain Engineering-related XP:
- `siegeParty.MemberRoster.AddXpToTroop(troopCharacter, engineeringXp * troopCount)` (Engineering.Apprenticeship perk)

### Troop XP API: `TroopRoster.AddXpToTroop()`

The roster methods for troop XP:
- `AddXpToTroop(CharacterObject character, int xpAmount)` - Add XP to a specific troop type
- `AddXpToTroopAtIndex(int index, int xpAmount)` - Add XP to troops at a specific roster index
- `CanTroopGainXp(PartyBase party, CharacterObject character, out int maxXp)` - Check if troop can gain XP (not at max tier)

**Important**: Troops stop gaining XP when they reach their maximum upgrade tier (no further upgrades available).

### Enlisted Implications

For the Enlisted mod:
- **Hero XP**: Use `Hero.AddSkillXp()` for the player and companion progression
- **Troop XP**: If implementing troop training systems, use `MemberRoster.AddXpToTroop()` for party troops
- **Check upgrade availability**: Always verify troops have valid upgrades before awarding XP
- **Respect party context**: Garrison training is separate from field party training

---

## How Skill XP is Stored + Converted into Skill Levels

Native stores per-skill XP on `HeroDeveloper`:

- `HeroDeveloper._skillXps`: dictionary keyed by `SkillObject`
- `HeroDeveloper._totalXp`: "raw XP" used for hero level progression

When XP is awarded:

1. **Call path** (typical):
   - `Hero.AddSkillXp(skill, xp)` (Hero convenience)  
   - -> `HeroDeveloper.AddSkillXp(skill, rawXp, isAffectedByFocusFactor: true, shouldNotify: true)`

2. **Multipliers applied**
   - **Generic XP multiplier**: `Campaign.Current.Models.GenericXpModel.GetXpMultiplier(hero)`
     - Default: 1.0, except some companion/perk cases.
   - **Learning rate / focus factor**:
     - `HeroDeveloper.GetFocusFactor(skill)`
     - Uses `CharacterDevelopmentModel.CalculateLearningRate(...)`
     - Based on:
       - relevant attribute(s)
       - focus points in that skill
       - learning limit (attribute + focus)
       - skill value over the limit reduces the rate

3. **Threshold check and level-up**
   - `CharacterDevelopmentModel.GetSkillLevelChange(hero, skill, skillXpAfter)`
   - Skill levels increase immediately if thresholds are crossed.

### Skill XP thresholds
The XP required per skill level is generated once in `DefaultCharacterDevelopmentModel.InitializeXpRequiredForSkillLevel()` and stored in an internal table up to 1024.

**Important quirk:** If the campaign is in **AccelerationMode.Fast**, the XP requirements are scaled down (multiplied by ~0.3). This changes "how fast skills grow" in accelerated campaigns.

---

## How Hero Level Progression Works (Focus/Attribute Points)

Hero level is driven by `HeroDeveloper._totalXp`, which increments when skill XP is added with `isAffectedByFocusFactor == true` (the default path).

### Level thresholds
`DefaultCharacterDevelopmentModel.InitializeSkillsRequiredForLevel()` builds a cumulative threshold table (up to level 62). The model exposes:

- `CharacterDevelopmentModel.SkillsRequiredForLevel(level)`  

This is what `HeroDeveloper.CheckLevel(...)` uses to determine level-ups.

### Points Gained (from native constants)

From `DefaultCharacterDevelopmentModel` (verified in decompile):

```csharp
// Character Limits
private const int MaxCharacterLevels = 62;           // Max hero level
public override int MaxAttribute => 10;              // Max attribute value
public override int MaxFocusPerSkill => 5;           // Max focus per skill

// Starting Points
public override int AttributePointsAtStart => 15;    // Total at level 1
public override int FocusPointsAtStart => 5;         // Total at level 1

// Points Per Level
public override int FocusPointsPerLevel => 1;        // Every level
public override int LevelsPerAttributePoint => 4;    // 1 attribute point every 4 levels
```

**Summary:**
| Resource | Starting | Per Level | Maximum |
|----------|----------|-----------|---------|
| Focus Points | 5 | +1 every level | 5 per skill |
| Attribute Points | 15 | +1 every 4 levels | 10 per attribute |
| Hero Level | 1 | — | 62 |

**Native Source:** `TaleWorlds.CampaignSystem/GameComponents/DefaultCharacterDevelopmentModel.cs`

---

## Practical Guidance for Enlisted "Training / Decisions" Rewards

### Preferred API for granting skill progression
Use:

- `Hero.MainHero.AddSkillXp(DefaultSkills.<Skill>, xpAmount)`

This ensures:
- learning rates apply naturally (focus/attributes)
- skill thresholds apply naturally
- hero level progression tracks naturally

### When *not* to use raw skill XP
Avoid directly setting skill values or bypassing native learning unless you have a very specific reason (e.g., save migration, one-off debug).

### Tuning note for Enlisted
If Enlisted adds expensive "Free Time" training (4-6 fatigue), it should grant XP in a way that feels consistent with:
- combat hits (native `DefaultSkillLevelingManager` calls `hero.AddSkillXp(...)`)
- party/map actions (trade profits, scouting, etc. also route through skill XP)

So: prefer fewer, higher-cost actions that award XP to **the skills that match current equipment/formation**, rather than huge static lists.

---

## Acceptance Checklist (for future Enlisted implementations)

- We award skill XP via `Hero.AddSkillXp(...)` (not direct level edits) except for migrations/debug.
- We do not bypass native learning rate unless intentional and documented.
- We keep rewards stable under different campaign acceleration settings.
- We can trace any "skill gained" behavior back to one of the native model layers:
  - GenericXpModel multiplier
  - CharacterDevelopmentModel learning rate / limit
  - Skill XP threshold table
