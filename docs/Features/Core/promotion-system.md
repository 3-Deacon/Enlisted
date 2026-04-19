# Promotion & Rank Progression System

**Summary:** Complete guide to military rank progression from T1 (Follower) to T9 (Marshal). Covers XP requirements, multi-factor promotion criteria, proving events, culture-specific rank titles, and the mechanics of advancement. This system rewards consistent service, combat performance, and maintaining good standing with your lord.

**Status:** ✅ Current  
**Last Updated:** 2026-01-14 (Updated to reflect Scrutiny 0-100 and native lord relation system)  
**Related Docs:** [Enlistment System](enlistment.md), [Training System](../Combat/training-system.md), [Pay System](pay-system.md), [Order Progression System](order-progression-system.md)

**SYSTEM CHANGES (2026-01-14):**
- **Scrutiny** is now 0-100 scale (merged from old Discipline 0-10)
- **Native lord reputation** via `Hero.GetRelation()` replaces custom reputation systems
- Soldier/Officer reputation removed - focus on lord trust and avoiding trouble

---

## Index

1. [Overview](#overview)
2. [Tier Progression Table](#tier-progression-table)
3. [Multi-Factor Promotion Requirements](#multi-factor-promotion-requirements)
4. [XP Sources](#xp-sources)
5. [The Promotion Process](#the-promotion-process)
6. [Proving Events](#proving-events)
7. [Culture-Specific Ranks](#culture-specific-ranks)
8. [Equipment & Benefits](#equipment--benefits)
9. [Configuration](#configuration)

---

## Overview

The promotion system transforms you from a raw recruit (T1) into a veteran commander (T9) through nine tiers of military rank. Promotion requires more than just experience—you must prove your worth through battle, maintain a good relationship with your lord, and avoid disciplinary problems.

**Three Career Tracks:**
- **Enlisted Track (T1-T4)**: Raw recruit to veteran soldier
- **Officer Track (T5-T6)**: Elite soldier to NCO leader
- **Commander Track (T7-T9)**: Captain to strategic commander with retinue

Each promotion unlocks better equipment from the Quartermaster, higher wages, and increased authority within the company.

---

## Tier Progression Table

| Tier | Generic Rank | XP Required | Typical Duration | Track | Notes |
|------|-------------|-------------|------------------|-------|-------|
| **T1** | Follower | 0 | Starting | Enlisted | Starting rank, basic equipment |
| **T2** | Recruit | 800 | 1-2 weeks | Enlisted | Formation selection event |
| **T3** | Free Sword | 3,000 | 1-2 months | Enlisted | Basic combat veteran |
| **T4** | Veteran | 6,000 | 2-3 months | Enlisted | Experienced fighter |
| **T5** | Blade | 11,000 | 3-4 months | Officer | Elite soldier status |
| **T6** | Chosen | 19,000 | 4-6 months | Officer | Senior NCO equivalent |
| **T7** | Captain | 30,000 | 6+ months | Commander | 20-soldier retinue granted |
| **T8** | Commander | 45,000 | Extended service | Commander | 30-soldier retinue (expanded) |
| **T9** | Marshal | 65,000 | Endgame | Commander | 40-soldier retinue (maximum) |

**Note:** XP requirements are cumulative totals, not per-tier amounts. T3 requires 3,000 total XP, not 3,000 additional XP beyond T2.

---

## Multi-Factor Promotion Requirements

Meeting the XP threshold is necessary but not sufficient for promotion. You must also satisfy four additional requirements:

### Complete Requirements Table

| Promotion | XP | Days in Rank | Battles | Min Lord Relation | Max Scrutiny |
|-----------|-----|--------------|---------|-------------------|--------------|
| **T1→T2** | 800 | 14 days | 2 | ≥0 | <80 |
| **T2→T3** | 3,000 | 35 days | 6 | ≥10 | <70 |
| **T3→T4** | 6,000 | 56 days | 12 | ≥20 | <60 |
| **T4→T5** | 11,000 | 56 days | 20 | ≥30 | <50 |
| **T5→T6** | 19,000 | 56 days | 30 | ≥15 | <40 |
| **T6→T7** | 30,000 | 70 days | 40 | ≥20 | <30 |
| **T7→T8** | 45,000 | 84 days | 50 | ≥25 | <20 |
| **T8→T9** | 65,000 | 112 days | 60 | ≥30 | <10 |

### Requirement Explanations

**1. XP Threshold**
- Cumulative enlistment XP earned from orders, events, and combat
- Tracked separately from skill XP
- See [XP Sources](#xp-sources) for details

**2. Days in Rank**
- Minimum time you must serve at your current rank
- Prevents rapid advancement without proving yourself
- Represents the time needed to learn your role

**3. Battles Survived**
- Combat experience requirement
- Only counts battles where you participated (not reserve duty)
- Incremented after each battle completion
- Shows you can survive under fire

**4. Lord Relation**
- Your personal relationship with your enlisted lord (native Bannerlord scale: -100 to +100)
- Gained through: order success, loyalty, combat performance, helpful actions
- Lost through: order failures, disobedience, poor performance, criminal activity
- Uses native `Hero.GetRelation()` system - integrates with vanilla Bannerlord
- Critical for officer and commander promotions - the lord must trust you before granting authority

**5. Maximum Scrutiny**
- Scrutiny is a "trouble/suspicion counter" (scale: 0-100, where 100 is discharge)
- Must be BELOW the threshold (e.g., <40 means scrutiny must be 39 or lower)
- Gained from: order failures, insubordination, criminal activity, suspicious behavior, theft
- Reduced slowly over time with good behavior (passive decay)
- Cannot promote with active disciplinary problems
- Replaces old Discipline system (0-10) - now on 0-100 scale for finer granularity

### Checking Your Status

When you're blocked from promotion but close (≥75% progress), the system logs the blocking factors:

```
Promotion to T3 blocked (82% progress): Days in rank: 28/35, Leader relation: 8/10
```

This tells you exactly what you need to improve to advance.

---

## XP Sources

**How XP Works:** The mod tracks two types of XP:
1. **Skill XP** - Individual character skills (Athletics, Tactics, etc.) via `Hero.AddSkillXp()`
2. **Enlistment XP** - Military rank progression via `EnlistmentBehavior.AddEnlistmentXP()`

Both are awarded simultaneously from all XP-granting activities. Enlistment XP is what advances your tier and appears in muster reports.

### Order Event XP

**Primary XP Source:** Events that occur during order execution  
**Amount:** 10-32 XP per event option (varies by skill and difficulty)  
**When:** Event resolution during order "slot" phases  
**Frequency:** 15-35% chance per slot (4 slots per day during active orders)

**Skills Awarded:**
- Guard/Sentry duty → Athletics, Tactics, Scouting
- Patrol → Scouting, Athletics, Roguery
- Medical → Medicine
- Equipment tasks → Crafting
- Leadership orders → Leadership, Tactics

**Example:**
```
Order Event: Drunk Soldier at Guard Post
Option: Turn him away
+12 Athletics XP (skill progression)
+12 Enlistment XP (rank progression)
```

All order events grant XP. Failed skill checks typically award 50% of success XP.

### Camp Activities XP

**Source:** Free time activities and camp opportunities  
**Amount:** 8-25 XP per activity  
**When:** Player-initiated camp decisions

**Activities:**
- Training sessions (weapon skills, athletics)
- Medical self-treatment
- Social activities (helping comrades, mentoring)
- Special decisions (quartermaster interactions, etc.)

### Combat & Battle XP

**Source:** Skill XP awarded during combat (native Bannerlord system)  
**Amount:** Varies by enemy tier, damage dealt, and killing blows  
**When:** During battle participation

The mod intercepts native skill XP awards during combat and converts accumulated combat skill XP to enlistment XP at battle end.

---

## The Promotion Process

### 1. Eligibility Check (Hourly)

The system checks promotion eligibility every in-game hour:
- XP threshold met?
- Days in rank sufficient?
- Battles survived requirement met?
- Lord relation high enough?
- Scrutiny low enough?

### 2. Proving Event Trigger

When all requirements are met, a **proving event** is triggered:
- Narrative event testing your character and choices
- Multiple options representing different approaches
- All options grant promotion (proving events test *how* you approach command, not *if* you deserve it)
- **T1→T2**: No proving event (direct promotion)
- **T2→T9**: Tier-specific proving events (see below)

### 3. Promotion Complete

After choosing your option:
- Tier advances immediately
- Culture-specific rank title applied
- Quartermaster equipment unlocked
- Wage increase applied
- Promotion notification shown
- Entry added to Personal Feed

---

## Proving Events

### T1→T2: Direct Promotion
No proving event - automatic promotion when requirements are met. This is the initial advancement from raw recruit to trained soldier.

### T2→T3: The Sergeant's Test
**Theme:** Earning the respect of NCOs  
**Situation:** The sergeant tests your competence under pressure with a complex order.

**What This Tests:** Your ability to handle responsibility and follow complex instructions. Shows the NCOs you're ready for veteran status.

### T3→T4: Crisis of Command
**Theme:** Battlefield leadership under pressure  
**Situation:** The sergeant falls wounded in battle. The line wavers. You must lead.

**Choices:**
1. **"I led the charge"** → Aggressive offense (Aggressive tag)
2. **"I held the line and protected the wounded"** → Defensive protection (Defender tag, +5 lord relation)
3. **"I ordered a tactical withdrawal"** → Calculated retreat (Tactician tag)

**What This Tests:** Your decision-making under fire and command instincts.

### T4→T5: The Veterans' Vote
**Theme:** Earning trust from those you'll lead  
**Situation:** The veteran squad gathers to decide if you're ready for officer rank.

**Choices:**
1. **"I'm nothing without this squad"** → Humble deference (+10 lord relation)
2. **"I've bled with you. I'm ready to lead you"** → Confident assertion (-10 scrutiny)
3. **"I swear no one goes hungry or forgotten"** → Protective promise (+5 lord relation, Protector tag)

**What This Tests:** How you relate to those who will serve under your command.

### T5→T6: Audience with the Lord
**Theme:** Loyalty and where your allegiance lies  
**Situation:** Your lord summons you. The rank of T6 is theirs to give, but first they must know where your loyalty lies.

**Choices:**
1. **"My sword belongs to you, my lord"** → Personal loyalty (Lord Sworn tag)
2. **"My loyalty is to [Kingdom] and its people"** → Loyalty to the realm (Realm Loyal tag)
3. **"My loyalty is to the soldiers beside me"** → Loyalty to comrades (+10 lord relation, Lance Bound tag)

**What This Tests:** Your fundamental allegiance - personal, political, or brotherly.

### T6→T7: Commander's Commission
**Theme:** Transition from soldier to commander  
**Situation:** Your lord offers command of twenty soldiers. Maps spread across the table. This is real command authority.

**Choices:**
1. **"I'm ready, my lord. Give me the command"** → Accept command (Leader tag, grants T7 + retinue)
2. **"I'm not ready to lead men yet"** → Decline (Cautious tag, NO promotion - must request later)

**What This Tests:** Whether you're ready for command responsibility. First promotion you can decline.

**Note:** This is the transition point to the Commander Track. From T7 onward, you command your own retinue of soldiers who fight under your banner.

### T7→T8 & T8→T9: Expanded Command
Both follow similar structures where the lord acknowledges your command performance and offers expanded responsibility:
- **T7→T8:** +10 soldiers (20→30 total)
- **T8→T9:** +10 soldiers (30→40 total, maximum)

---

## Culture-Specific Ranks

Generic rank names (Follower, Recruit, etc.) are replaced by culture-specific titles matching the faction you serve.

### Empire Ranks (Legion/Discipline)

| Tier | Rank | Translation |
|------|------|-------------|
| T1 | Tiro | Recruit |
| T2 | Miles | Soldier |
| T3 | Immunes | Specialist |
| T4 | Principalis | Junior Officer |
| T5 | Evocatus | Veteran |
| T6 | Centurion | Company Commander |
| T7 | Primus Pilus | First Spear (Elite Centurion) |
| T8 | Tribune | Senior Commander |
| T9 | Legate | Legion Commander |

### Vlandia Ranks (Feudal/Chivalry)

| Tier | Rank |
|------|------|
| T1 | Peasant |
| T2 | Levy |
| T3 | Footman |
| T4 | Man-at-Arms |
| T5 | Sergeant |
| T6 | Knight Bachelor |
| T7 | Cavalier |
| T8 | Banneret |
| T9 | Castellan |

### Sturgia Ranks (Tribal/Shield Wall)

| Tier | Rank |
|------|------|
| T1 | Thrall |
| T2 | Ceorl |
| T3 | Fyrdman |
| T4 | Drengr |
| T5 | Huskarl |
| T6 | Varangian |
| T7 | Champion |
| T8 | Thane |
| T9 | High Warlord |

*(Additional cultures: Khuzait, Battania, Aserai, Mercenary - see progression_config.json)*

---

## Equipment & Benefits

### Equipment Access

Each promotion unlocks higher-tier equipment from the Quartermaster:
- New items appear in the equipment grid
- You must purchase/request items (not automatic)
- Higher tiers = better armor, weapons, and horses
- Equipment quality gates represent supply chain limitations

**Officers' Armory (T7+ Only):**
At T7+ with Quartermaster Reputation 60+, you gain access to the Officers' Armory which provides T8-T9 tier equipment beyond your normal access.

### Wage Increases

Your daily wage increases with tier:

**Base Formula:**
```
daily_base = 10
tier_bonus = tier × 5
Wage = (10 + tier×5) × multipliers
```

**Example Progression:**
- T1: ~10-15 denars/day
- T3: ~25-30 denars/day
- T5: ~35-45 denars/day
- T7: ~50-65 denars/day (+ retinue management responsibility)

### Authority & Responsibilities

**T1-T2 (Enlisted):**
- Take orders from sergeants
- Basic camp activities
- No command authority

**T3-T4 (Veteran):**
- More complex orders
- Input on tactical decisions
- Veteran status recognition

**T5-T6 (Officer):**
- Officer-level orders
- Strategic decision participation
- NCO equivalent authority

**T7-T9 (Commander):**
- Command your own retinue (20-40 soldiers)
- Strategic orders from the lord
- Post-battle retinue management
- Reinforcement requests
- Named veteran soldiers under your command

---

## Configuration

### Tier Requirements

File: `ModuleData/Enlisted/progression_config.json`

```json
{
  "tier": 3,
  "xp_required": 3000,
  "name": "{=Enlisted_Rank_FreeSword}Free Sword",
  "duration": "1-2 months"
}
```

**Adjusting Thresholds:**
- Modify `xp_required` to change how much XP is needed
- Values are cumulative totals, not deltas
- Lower values = faster progression

### Multi-Factor Requirements

File: `src/Features/Ranks/Behaviors/PromotionBehavior.cs`

```csharp
2 => new PromotionRequirements { 
  DaysInRank = 14, 
  BattlesRequired = 2, 
  MinLeaderRelation = 0, 
  MaxScrutiny = 80 
}
```

**Adjusting Requirements:**
- Reduce `DaysInRank` for faster time progression
- Reduce `BattlesRequired` for less combat dependency
- Lower `MinLeaderRelation` for easier advancement
- Raise `MaxScrutiny` to be more forgiving of trouble (0-100 scale)

**Warning:** These are code-based values. Changes require recompiling the mod.

---

## Implementation Details

### Source Files

| File | Purpose |
|------|---------|
| `src/Features/Ranks/Behaviors/PromotionBehavior.cs` | Hourly eligibility checks, proving event queuing, requirements validation |
| `src/Features/Ranks/RankHelper.cs` | Culture-specific rank title lookup |
| `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` | XP tracking, tier storage, `SetTier()` method |
| `src/Features/Escalation/EscalationManager.cs` | Scrutiny tracking for promotion requirements |
| `src/Features/Content/EventDeliveryManager.cs` | Processes `"promotes": true` effect from event options |
| `src/Features/Equipment/Behaviors/QuartermasterManager.cs` | `UpdateNewlyUnlockedItems()` after promotions |
| `ModuleData/Enlisted/Events/events_promotion.json` | Proving event definitions (8 events) |
| `ModuleData/Enlisted/progression_config.json` | XP thresholds, culture ranks, XP sources |

### Key Methods

**Promotion Check (hourly):**
```csharp
// PromotionBehavior.CanPromote()
// Lines 186-260
- Checks XP threshold
- Checks days in rank
- Checks battles survived
- Checks scrutiny (escalation.State.Scrutiny < req.MaxScrutiny)
- Checks lord relation (lord.GetRelationWithPlayer() >= req.MinLeaderRelation)
```

**XP Award:**
```csharp
// EnlistmentBehavior.AddEnlistmentXP(amount, source)
_enlistmentXP += xp;
CheckPromotionNotification(previousXP, _enlistmentXP);
```

**Tier Advancement:**
```csharp
// EnlistmentBehavior.SetTier(newTier)
_enlistmentTier = newTier;
_daysInRank = 0;
QuartermasterManager.Instance?.UpdateNewlyUnlockedItems();
```

### Save/Load Compatibility

All promotion data persists through saves:
- `_enlistmentTier` — Current tier (1-9)
- `_enlistmentXP` — Total accumulated XP
- `_daysInRank` — Days at current rank
- `_battlesSurvived` — Total battles completed
- `_pendingPromotionTier` — Tracks queued proving event
