# Order Progression System

**Summary:** Orders are multi-day duty assignments that progress through phases automatically. Events fire contextually during orders based on world state and order type. Players experience being a soldier through the rhythm of assigned duties, occasional events, and accumulated consequences like fatigue and injury. XP progression is now exclusively through orders (no passive daily/battle XP). Narrative recaps replace mechanical XP displays.

**Status:** ✅ **IMPLEMENTED** - OrderProgressionBehavior, 17 orders, 84 order events active  
**Last Updated:** 2026-01-04 (Bug fix: Order events now properly check notAtSea requirements)  
**Implementation:** `src/Features/Orders/Behaviors/OrderProgressionBehavior.cs`, `ModuleData/Enlisted/Orders/`  
**Related Docs:** [Orders Content](../Content/orders-content.md), [Event System Schemas](../Content/event-system-schemas.md), [Injury System](../Content/injury-system.md), [Content System Architecture](../Content/content-system-architecture.md)

**RECENT CHANGES (2026-01-04):**
- **Bug fix: notAtSea filtering**: `SelectOrderEvent()` now uses `EventRequirementChecker.MeetsRequirements()` to properly filter order events by all requirement fields (notAtSea, tier, role, etc.). Previously only checked `world_state`. Added `notAtSea: true` to 35 land-based events across 6 files (escort duty, forage, scout, patrol, march, inspect defenses, camp patrol).
- **Debugging note**: Investigation of debug logs (`Session-A_2026-01-04_04-20-21.log`) revealed the old build was running when "Shortcut" event fired at sea. No `OrderProgressionBehavior` logs were present - instead `EventSelector` (regular narrative system) was firing order events. **Game restart required** after build to load new DLL with fixes.

**PREVIOUS CHANGES (2026-01-01):**
- **Event frequency drastically reduced**: `SlotBaseChance` 15%→8%, `HighSlotBaseChance` 35%→15%, Routine multiplier 0.6→0.5
- **Result**: Routine duty now ~12% daily event chance (1 event every 8 days) instead of ~28% (1 every 3-4 days)
- **Order outcome fallback text**: Added RP-appropriate contextual text when JSON `text` is null ("Watch ended, all in order" vs generic "Order completed")
- **Sea/land context detection**: Now uses native `party.IsCurrentlyAtSea` property directly (removed reflection overhead)
- **Narrative recaps prioritized**: Order outcomes show "Routine watch" or "Spotted tracks" instead of "+15 XP"

---

## Index

1. [Overview](#overview)
2. [Design Philosophy](#design-philosophy)
3. [Phase System](#phase-system)
4. [Order Definitions (T1-T6)](#order-definitions-t1-t6)
5. [Event System](#event-system)
6. [Consequences](#consequences)
7. [XP Progression](#xp-progression)
8. [Orchestrator Integration](#orchestrator-integration)
9. [UI Integration](#ui-integration)
10. [Technical Specification](#technical-specification)
11. [Edge Cases](#edge-cases)
12. [Acceptance Criteria](#acceptance-criteria)

---

## Overview

The Order Progression System transforms orders from instant-resolution tasks into multi-day duty assignments that simulate military life. When a player accepts an order, it runs automatically through phases (4 per day), updating the Recent Activity feed with status text. The Content Orchestrator monitors world state and can inject contextual events during order execution.

### Core Loop

```
1. Order Issued
   - Mandatory orders (T1-T3 basic duties): Automatically assigned, "Duty Assigned" notification
   - Optional orders (T4+ advancement): Shows in Orders menu as [NEW] → Player clicks → Popup with Accept/Decline

2. Order auto-progresses through phases (4/day: Dawn, Midday, Dusk, Night)

3. Recent Activity updates with status text each phase

4. Orchestrator checks each "slot" phase → may inject event based on world state

5. IF event fires → Player gets popup, makes decision

6. Consequences accumulate (fatigue, XP, possible injury)

7. Order completes → Summary + rewards in Recent Activity

8. Player rests or accepts next order
```

### Mandatory vs Optional Orders

**Mandatory Orders (Automatically Assigned):**
- Basic T1-T3 duties that soldiers cannot decline
- Automatically accepted when issued
- Shows as `[ASSIGNED]` (greyed out, not clickable) in Orders menu
- Player status displays: "On duty: Guard Duty."
- Examples: Guard Duty, Camp Patrol, Firewood, Muster, Sentry
- Realistic: Low-rank soldiers don't choose their duties

**Optional Orders (Player Choice):**
- T4-T6 advancement and special duties
- Shows as `[NEW]` (clickable) in Orders menu
- Player clicks order row → popup shows order details with [Accept Order] / [Decline Order] buttons
- Declining has consequences (rep penalty, discharge risk)
- Examples: Lead Patrol, Scout Route, Train Recruits, Escort Duty

### Key Principles

- **Orders are the job** - Not quests or adventures. Assigned duties.
- **Mandatory duties are automatic** - T1-T3 soldiers don't choose their duties. They're assigned.
- **Events happen during duty** - Contextual to what you're doing, not random narrative.
- **Slow progression** - Routine duty gives tiny XP. Events and combat accelerate it.
- **Consequences matter** - Fatigue accumulates. Injury is possible. Skill checks have stakes.
- **Rank determines experience** - T1 gets menial work. T6 leads men. Different event pools.
- **Duty doesn't block gameplay** - On duty? Camp life continues, but some activities are risky/blocked.
- **World state affects frequency** - Siege duty is intense. Garrison duty is boring.
- **Naval awareness** - Orders adapt to sea/land travel (Warsails DLC aware). T1-T3 orders show nautical variants at sea (Guard Duty → Deck Watch). T4-T6 land-only orders (scouting, foraging, patrolling) are blocked at sea. Active land orders are cancelled when boarding a ship.

---

## Design Philosophy

### Why This Matters

**Problem with instant resolution:**
- Orders feel like button presses, not duties
- No sense of time passing
- No opportunity for things to happen during execution
- Player doesn't feel like a soldier doing a job

**Solution with progression:**
- Orders take time (1-3 days typically)
- Player sees status updates as duty progresses
- Events can fire during execution (contextually)
- Consequences accumulate realistically
- Player feels the rhythm of military life

### The Soldier Experience

A soldier's life is mostly routine. Guard posts. Patrols. Manual labor. Waiting. The same duties repeated. Occasionally something happens. Rarely is it exciting.

This system creates that experience:
- **Most phases are quiet** - Status text only
- **Events are the exception** - Meaningful when they occur
- **Combat is where growth happens** - Orders are maintenance, battles are advancement
- **Years of service** - Slow progression creates career arc

---

## Phase System

### Four Phases Per Day

Each day divides into 4 phases aligned with the day/night cycle:

| Phase | Game Hour | Character | Event Likelihood |
|-------|-----------|-----------|------------------|
| **Dawn** | ~6:00 | Fresh start, alert | Low |
| **Midday** | ~12:00 | Active, peak performance | Medium |
| **Dusk** | ~18:00 | Winding down, fatigue setting in | Medium |
| **Night** | ~0:00 | Dark, dangerous, tired | High |

### Phase Types

Each phase in an order sequence has a type:

| Type | Behavior | Player Sees |
|------|----------|-------------|
| **Routine** | Status text only, no event possible | Text in Recent Activity |
| **Slot** | Orchestrator may inject event (base 15% chance) | Text, possibly event popup |
| **Slot!** | High-chance event slot (base 35% chance) | Text, likely event popup |
| **Checkpoint** | Status + mini-reward (small XP) | Text + reward notification |
| **Resolve** | Order completion, final rewards | Completion summary |

### Order Duration

Orders have set durations determining total phases:

| Duration | Phases | Typical Orders |
|----------|--------|----------------|
| 4 phases (1 day) | 4 | Muster, equipment cleaning, latrine duty |
| 8 phases (2 days) | 8 | Guard post, patrol, sentry duty |
| 12 phases (3 days) | 12 | Scout route, lead patrol, escort duty |
| Variable | Varies | March formation (depends on travel) |

---

## Order Definitions (T1-T6)

### T1-T3: Basic Soldier Orders (Mandatory)

Menial, physical, and low-responsibility duties. **Automatically assigned - no player choice.** Short duration, low event chance, high fatigue.

**Mandatory Orders:**
- `order_guard_duty` - Guard Duty
- `order_camp_patrol` - Camp Patrol  
- `order_firewood` - Firewood Collection
- `order_equipment_check` - Equipment Inspection
- `order_muster` - Muster Inspection
- `order_sentry` - Sentry Post

These orders are flagged with `"mandatory": true` in JSON and automatically accepted when issued.

#### Guard Post
```
Duration: 2 days (8 phases)
Primary Skill: Perception
Fatigue: Low (+5/day)
Injury Risk: Very Low

Phases:
  Day 1: [routine] [routine] [slot] [slot!]
  Day 2: [slot] [routine] [checkpoint] [resolve]

Event Pool:
  - guard_drunk_soldier (Charm check)
  - guard_strange_noise (Perception check)
  - guard_officer_inspection (Discipline check)
  - guard_caught_sneaking (Perception check)
  - guard_relieved_early (Lucky - fatigue reduced)
  - guard_double_shift (Unlucky - extra fatigue)
```

#### Camp Patrol
```
Duration: 1-2 days (4-8 phases)
Primary Skill: Athletics
Fatigue: Medium (+10/day)
Injury Risk: Low

Phases (2-day):
  Day 1: [routine] [slot] [routine] [slot]
  Day 2: [slot] [routine] [routine] [resolve]

Event Pool:
  - patrol_lost_item (Perception check - find it)
  - patrol_soldier_argument (Leadership check - break it up)
  - patrol_suspicious_activity (Perception check - investigate)
  - patrol_officer_tags_along (Social pressure)
  - patrol_shortcut (Athletics check - save time)
  - patrol_twisted_ankle (Athletics check - avoid injury)
```

#### Firewood Detail
```
Duration: 1 day (4 phases)
Primary Skill: Athletics
Fatigue: High (+15/day)
Injury Risk: Medium

Phases:
  [routine] [slot] [slot] [resolve]

Event Pool:
  - firewood_axe_slip (Athletics check - avoid injury)
  - firewood_found_something (Perception check - what is it?)
  - firewood_wildlife (Scouting check - animal encounter)
  - firewood_work_song (Morale event - join in or not)
  - firewood_competition (Athletics check - impress others)
```

#### Latrine Duty
```
Duration: 1 day (4 phases)
Primary Skill: None
Fatigue: Medium (+10/day)
Injury Risk: Low

Phases:
  [routine] [routine] [routine] [resolve]

Event Pool:
  - latrine_overheard_rumor (Intelligence gained)
  - latrine_officer_complains (Discipline check)
  - latrine_soldier_gratitude (Rep event)

Notes: Low-event order. Punishment duty. Minimal rewards.
```

#### Equipment Cleaning
```
Duration: 1 day (4 phases)
Primary Skill: Crafting
Fatigue: Low (+5/day)
Injury Risk: Very Low

Phases:
  [routine] [slot] [routine] [resolve]

Event Pool:
  - cleaning_found_damage (Crafting check - report or fix)
  - cleaning_helped_comrade (Social event)
  - cleaning_officer_inspection (Discipline check)
```

#### Muster Inspection
```
Duration: 4 phases (same day)
Primary Skill: None
Fatigue: Low (+3)
Injury Risk: None

Phases:
  [routine] [slot] [routine] [resolve]

Event Pool:
  - muster_kit_problem (Decision - borrow or face consequences)
  - muster_praised (Rep bonus)
  - muster_singled_out (Scrutiny event)
```

#### Sentry Duty
```
Duration: 1 day (4 phases)
Primary Skill: Perception
Fatigue: Medium (+8/day)
Injury Risk: Low (night: Medium)

Phases:
  [routine] [slot!] [routine] [resolve]

Event Pool:
  - sentry_movement_spotted (Perception check)
  - sentry_fell_asleep (Failed fatigue check - consequences)
  - sentry_challenged_approach (Decision - challenge or let pass)
  - sentry_relief_late (Extra fatigue)
  - sentry_heard_something (Perception check)
```

#### March Formation
```
Duration: Variable (depends on travel time)
Primary Skill: Athletics
Fatigue: High (+12/day)
Injury Risk: Low

Phases:
  Per day: [routine] [slot] [slot] [routine]
  Final day: [routine] [routine] [checkpoint] [resolve]

Event Pool:
  - march_fell_behind (Athletics check)
  - march_helped_comrade (Decision - slow down to help)
  - march_equipment_problem (Crafting check - quick fix)
  - march_terrain_hazard (Athletics check - avoid injury)
  - march_foraging_opportunity (Scouting check - find food)
```

---

### T4-T6: Specialist/NCO Orders

Skilled work with higher stakes. Longer duration, more events, real consequences.

#### Scout the Route
```
Duration: 2-3 days (8-12 phases)
Primary Skill: Scouting
Fatigue: Medium (+10/day)
Injury Risk: High
Tier Requirement: T4+

Phases (3-day):
  Day 1: [routine] [slot] [slot] [slot!]
  Day 2: [slot] [slot!] [routine] [slot]
  Day 3: [routine] [slot] [checkpoint] [resolve]

Event Pool:
  - scout_tracks_found (Scouting check - identify threat level)
  - scout_enemy_patrol (Tactics check - evade, report, or observe)
  - scout_terrain_obstacle (Scouting check - find alternate route)
  - scout_injured_ankle (Athletics check - push through or slow)
  - scout_shortcut_found (Scouting check - faster completion)
  - scout_lost_trail (Scouting check - find it again)
  - scout_ambush (Combat trigger or flee)
  - scout_enemy_camp (Decision - observe, report, or withdraw)

Critical Failure: Ambush, HP loss, possible capture
```

#### Treat the Wounded
```
Duration: 1-2 days (4-8 phases)
Primary Skill: Medicine
Fatigue: Medium (+8/day)
Injury Risk: Low
Tier Requirement: T4+

Phases (2-day):
  Day 1: [routine] [slot] [slot] [routine]
  Day 2: [slot] [routine] [checkpoint] [resolve]

Event Pool:
  - treat_shortage (Medicine check - improvise)
  - treat_difficult_case (Medicine check - save or lose)
  - treat_infection_risk (Medicine check - prevent spread)
  - treat_grateful_soldier (Rep bonus event)
  - treat_officer_wounded (High-stakes case)
  - treat_contagion (Medical Risk to self)

Bonus: Successful treatment gives Soldier Rep
```

#### Lead Patrol
```
Duration: 2-3 days (8-12 phases)
Primary Skill: Tactics
Secondary: Leadership
Fatigue: High (+12/day)
Injury Risk: Medium
Tier Requirement: T5+

Phases (3-day):
  Day 1: [routine] [slot] [slot!] [slot]
  Day 2: [slot] [slot] [checkpoint] [slot!]
  Day 3: [slot] [routine] [routine] [resolve]

Event Pool:
  - patrol_lead_soldier_behind (Leadership check - wait or leave)
  - patrol_lead_route_dispute (Tactics check - who's right)
  - patrol_lead_enemy_contact (Tactics check - engage or evade)
  - patrol_lead_morale_drop (Leadership check - rally them)
  - patrol_lead_terrain_hazard (Scouting check - safe path)
  - patrol_lead_man_injured (Medicine check - field treatment)
  - patrol_lead_ambush (Combat trigger)
  - patrol_lead_good_find (Opportunity - supplies or intel)

Critical Failure: Men lost, reputation crash
Success Bonus: Lord Rep, command experience
```

#### Repair Equipment
```
Duration: 1-2 days (4-8 phases)
Primary Skill: Crafting
Fatigue: Low (+5/day)
Injury Risk: Low
Tier Requirement: T4+

Phases (2-day):
  Day 1: [routine] [slot] [routine] [routine]
  Day 2: [slot] [routine] [routine] [resolve]

Event Pool:
  - repair_missing_parts (Crafting check - improvise)
  - repair_discovered_sabotage (Decision - report or investigate)
  - repair_helped_smith (Social event)
  - repair_rush_job (Crafting check under pressure)
  - repair_quality_work (Bonus if high skill)

Bonus: Company Equipment stat improvement
```

#### Forage for Supplies
```
Duration: 1-2 days (4-8 phases)
Primary Skill: Scouting
Fatigue: High (+12/day)
Injury Risk: Medium
Tier Requirement: T4+

Phases (2-day):
  Day 1: [routine] [slot] [slot!] [slot]
  Day 2: [slot] [routine] [routine] [resolve]

Event Pool:
  - forage_rich_find (Scouting check - bonus supplies)
  - forage_wildlife (Combat or flee)
  - forage_farmer_encounter (Charm check - buy, steal, or leave)
  - forage_spoiled_cache (Bad luck - wasted effort)
  - forage_enemy_territory (Stealth check - avoid detection)
  - forage_injured (Athletics check - avoid injury)

Success: +Supplies to company
Critical Failure: Spoiled food, medical risk
```

#### Train Recruits
```
Duration: 2 days (8 phases)
Primary Skill: Leadership
Secondary: Athletics
Fatigue: Medium (+8/day)
Injury Risk: Low
Tier Requirement: T5+

Phases:
  Day 1: [routine] [slot] [slot] [routine]
  Day 2: [slot] [routine] [checkpoint] [resolve]

Event Pool:
  - train_difficult_recruit (Leadership check)
  - train_injury_during (Medicine check - handle it)
  - train_impressive_performance (Bonus rep)
  - train_officer_observes (Pressure event)
  - train_recruit_question (Decision - answer truthfully or lie)

Success: Soldier Rep, Leadership XP
Bonus: May unlock retinue candidate at T7+
```

#### Inspect Defenses
```
Duration: 1-2 days (4-8 phases)
Primary Skill: Engineering
Fatigue: Low (+5/day)
Injury Risk: Low
Tier Requirement: T4+

Phases (2-day):
  Day 1: [routine] [slot] [routine] [slot]
  Day 2: [routine] [routine] [routine] [resolve]

Event Pool:
  - inspect_found_weakness (Decision - report or fix quietly)
  - inspect_officer_dispute (Charm check - handle politics)
  - inspect_sabotage_discovered (Major event)
  - inspect_improvement_idea (Engineering check - implement)

Success: Readiness bonus, Officer Rep
Context: More events during siege
```

#### Escort Duty
```
Duration: 2-3 days (8-12 phases)
Primary Skill: Athletics
Secondary: Tactics
Fatigue: High (+12/day)
Injury Risk: Medium
Tier Requirement: T4+

Phases (3-day):
  Day 1: [routine] [slot] [slot] [slot!]
  Day 2: [slot!] [slot] [routine] [slot]
  Day 3: [routine] [slot] [routine] [resolve]

Event Pool:
  - escort_bandit_scouts (Perception check - spotted)
  - escort_ambush (Combat or evade)
  - escort_difficult_terrain (Athletics check)
  - escort_cargo_problem (Crafting check - quick fix)
  - escort_vip_demands (Charm check - handle noble)
  - escort_shortcut_option (Tactics check - risk vs reward)

High stakes: Cargo or VIP protection
Critical Failure: Lost cargo, major rep damage
```

---

## Event System

### Event Pool Selection

When a slot fires, the orchestrator selects from the order's event pool:

```
1. Filter pool by requirements (skill minimums, world state)
2. Weight by world state context (siege events weighted higher during siege)
3. Weight by player behavior (if player likes combat, weight combat events)
4. Exclude recently fired events (per-event cooldown)
5. Random weighted selection
```

### Event Structure

```json
{
  "id": "scout_enemy_patrol",
  "order_type": "order_scout_route",
  "title": "Enemy Patrol",
  "titleId": "order_evt_scout_patrol_title",
  
  "setup": "Movement ahead. Through the brush, you spot enemy soldiers. A patrol of five, maybe six. They haven't seen you yet.",
  "setupId": "order_evt_scout_patrol_setup",
  
  "requirements": {
    "world_state": ["war_marching", "war_active_campaign", "siege_attacking"]
  },
  
  "options": [
    {
      "id": "evade",
      "text": "Go to ground and let them pass.",
      "textId": "order_evt_scout_patrol_opt_evade",
      "tooltip": "Scouting check. Success: continue undetected. Failure: spotted, must flee.",
      "skill_check": { "Scouting": 50 },
      "success": {
        "order_progress": 10,
        "skill_xp": { "Scouting": 15 },
        "text": "You press into the undergrowth. They pass within twenty paces. Never see you."
      },
      "failure": {
        "order_progress": -20,
        "fatigue": 15,
        "text": "A twig snaps. Shouts. You run. Lost valuable time circling back."
      }
    },
    {
      "id": "observe",
      "text": "Follow them. Learn their route.",
      "textId": "order_evt_scout_patrol_opt_observe",
      "tooltip": "Scouting check (harder). Success: intel bonus, +Lord Rep. Failure: detected.",
      "skill_check": { "Scouting": 70 },
      "success": {
        "order_progress": 15,
        "reputation": { "lord": 5 },
        "skill_xp": { "Scouting": 25 },
        "text": "You shadow them for an hour. Their patrol route, their discipline, their numbers. Valuable."
      },
      "failure": {
        "order_progress": -30,
        "hp_loss": 15,
        "fatigue": 20,
        "text": "Too close. An arrow clips your arm as you flee. You'll have a scar from this."
      }
    },
    {
      "id": "report",
      "text": "Withdraw and report immediately.",
      "textId": "order_evt_scout_patrol_opt_report",
      "tooltip": "Safe choice. Order completes early with partial rewards.",
      "effects": {
        "order_complete_early": true,
        "order_outcome": "partial",
        "text": "You've seen enough. The captain needs to know. Order cut short but intel delivered."
      }
    }
  ]
}
```

### World State Event Weighting

Events are weighted based on current world state:

| World State | Event Types Weighted Higher |
|-------------|----------------------------|
| Peacetime Garrison | Social, discipline, boredom |
| War Marching | Terrain, fatigue, wildlife |
| War Active Campaign | Enemy contact, danger, opportunity |
| Siege Attacking | Opportunity, enemy weakness, morale |
| Siege Defending | Danger, fire, assault warning, sabotage |
| Low Morale | Discipline, arguments, desertion |
| High Scrutiny | Inspection, testing, pressure |

---

## Consequences

### Fatigue

Fatigue accumulates during orders and affects performance.

#### Accumulation

| Order Weight | Fatigue/Day | Night Phase Bonus |
|--------------|-------------|-------------------|
| Light | +5 | +3 |
| Medium | +10 | +5 |
| Heavy | +15 | +8 |
| Physical Labor | +20 | +10 |

Additional sources:
- Failed skill check: +5 fatigue
- Event went wrong: +10 fatigue
- Double shift event: +15 fatigue
- Combat during order: +20 fatigue

#### Effects

| Fatigue Level | Effects |
|---------------|---------|
| 0-30 | None |
| 31-50 | -5% skill check success |
| 51-70 | -10% skill check success, +5% injury risk |
| 71-90 | -20% skill check success, +10% injury risk |
| 91+ | -30% skill check, +20% injury risk, events more likely to go wrong |

#### Recovery

- Rest decision in Camp Hub: -20 fatigue per use
- No active order: -10 fatigue per day
- Good rations: Additional -5 fatigue per day
- Settlement rest: -25 fatigue per day

### Injury

Physical orders carry injury risk.

#### Base Risk Per Phase

| Order Type | Base Injury Chance |
|------------|-------------------|
| Guard/cleaning | 1% |
| Patrol | 3% |
| Physical labor | 5% |
| Scout/forage | 4% |
| Lead patrol | 3% |

#### Modifiers

| Condition | Modifier |
|-----------|----------|
| Failed skill check | +10% |
| Night phase | +5% |
| Fatigue > 50 | +5% |
| Fatigue > 75 | +10% |
| Low Athletics (< 30) | +5% |
| Event went wrong | +15% |
| Bad weather (if implemented) | +5% |

#### Outcomes

| Roll Result | Effect |
|-------------|--------|
| Minor injury | -10 HP, status text only |
| Moderate injury | -25 HP, +10 Medical Risk |
| Serious injury | -40 HP, +25 Medical Risk, order may fail |

Injury triggers existing Medical Progression System for recovery.

### Skill Checks

Orders use skill checks at key moments.

#### Success Calculation

```
Base Success = 50% + (Skill / 2.5)

Examples:
  Skill 0:   50% + 0%  = 50%
  Skill 30:  50% + 12% = 62%
  Skill 60:  50% + 24% = 74%
  Skill 100: 50% + 40% = 90%
  Skill 150: 50% + 60% = 110% (capped at 95%, bonus XP)
```

#### Modifiers

| Condition | Modifier |
|-----------|----------|
| Fatigue > 50 | -10% |
| Fatigue > 75 | -20% |
| Night phase | -5% |
| Recent injury | -10% |
| Good equipment | +5% |
| High morale | +5% |
| Trait bonus (relevant) | +5% to +15% |

#### Outcomes

- **Success**: Progress continues, XP gain, possible bonus
- **Failure**: Progress penalty, fatigue, possible injury, reputation risk
- **Critical Success** (roll > 95% threshold): Double XP, bonus rewards
- **Critical Failure** (roll < 5% threshold): Injury likely, order may fail

---

## XP Progression

### Design: All XP is Earned Through Orders & Events

**CRITICAL CHANGE (2025-12-31):** Passive daily XP and automatic battle XP have been **removed entirely**. All XP now comes through:
1. **Order completion** - Small XP for finishing assigned duties
2. **Order events** - Larger XP for meaningful events during orders
3. **Combat participation** - Only if on an active order during battle

**Why this change?**
- Makes orders the central progression path (aligns with mod's design)
- Removes "passive income" feel - you progress by doing your job, not just existing
- Ensures all XP gains are tied to meaningful gameplay moments
- Eliminates "stand around and wait for daily tick" strategies

### Order Completion XP (Baseline Rewards)

| Tier | XP on Complete |
|------|----------------|
| T1-T3 | +10-15 |
| T4-T6 | +20-30 |

**Example: 2-day Guard Post (T2 soldier, no events)**
```
Order completion: +10 XP
= 10 total XP for 2 days of quiet duty
```

That's minimal. As intended - routine duty shouldn't accelerate progression.

### Event XP (Something Happened)

| Outcome | XP Bonus |
|---------|----------|
| Event resolved (any outcome) | +15-25 |
| Skill check passed | +10 |
| Critical success | +25 |
| Exceptional choice (helped comrade, etc.) | +15 |
| Caught something/someone | +15 |
| Intel gathered | +20 |

**Example: 2-day Guard Post with one event**
```
8 XP (passive) + 10 XP (completion) + 25 XP (event) + 10 XP (skill check passed)
= 53 total XP
```

Nearly 3× the quiet order. Events matter.

### Combat XP (Where Real Growth Happens)

**CRITICAL:** There is **NO automatic battle XP**. You don't get XP just for being in a battle.

Combat XP only comes through orders:
- Order completion XP if you're on duty during battle (order completes with battle bonus)
- Order event XP if battle triggers an order event ("Valor in Combat", "Wounded in Battle", etc.)
- Special battle-related orders (distinguished service, heroic actions)

**Example:** A T2 soldier fights a major battle:
- **Without an active order**: 0 XP (just like a mercenary would get nothing for standing there)
- **With Guard Duty active**: +10 XP (order completion) + potential event XP if something notable happens
- **With Reconnaissance Patrol active**: +20 XP (order completion with higher stakes) + likely 25-50 XP from battle events

This design ensures:
1. All progression is tied to being an enlisted soldier (not a mercenary)
2. Combat accelerates progression when it intersects with your duties
3. No "stand in formation and collect XP" exploits

| Combat Source | XP |
|---------------|-----|
| Order completed during campaign | +20-50 |
| Battle event during order | +25-75 |
| Valor/distinguished service order | +50-100 |
| Survive critical injury during order | +30 |

**One battle during an order can provide 50-150 XP** through order completion and events.

### Progression Timeline

At these rates, approximate time to tier up:

| Tier | XP Required | Quiet Orders Only | With Events | With Combat |
|------|-------------|-------------------|-------------|-------------|
| T1→T2 | ~200 | 11 orders (~25 days) | 8 orders (~18 days) | 3-4 battles |
| T2→T3 | ~400 | 22 orders (~50 days) | 12 orders (~28 days) | 5-6 battles |
| T3→T4 | ~800 | 44 orders (~100 days) | 20 orders (~45 days) | 8-10 battles |
| T4→T5 | ~1200 | 50 orders (~125 days) | 25 orders (~60 days) | 10-12 battles |
| T5→T6 | ~1800 | 75 orders (~180 days) | 35 orders (~85 days) | 12-15 battles |

**A soldier who just does their job takes 1-2 in-game years to reach T6.**
**A soldier who fights frequently can reach T6 in months.**

This matches historical reality: soldiers who see combat advance faster.

---

## Phase Recaps & Forecasting

### Order Phase Recaps

Players see narrative recaps of what happened during each order phase in the Camp Hub "SINCE LAST MUSTER" section. Recaps replace mechanical XP displays with immersive narrative.

**Recap Types:**

| Phase Type | Example Text | When Used |
|------------|--------------|-----------|
| Routine | "Morning muster complete. Nothing to report." | Non-slot phase, no event |
| Foreshadowing | "Spotted tracks near the perimeter. Origin unknown." | Slot phase, no event fired |
| Event | "Dead Man's Purse. Dealt with." | Event fired during phase |

**Implementation:**
- `OrderProgressionBehavior` records a `PhaseRecap` for each phase
- Recaps stored in memory (max 40, trimmed oldest first)
- Displayed in Camp Hub with day/phase context
- Color-coded by event type (routine = white, foreshadowing = gold, event = green)

**Example Recap Display:**
```
_____ SINCE LAST MUSTER (8 days) _____

Day 1, Midday: Routine watch. All quiet.
Day 1, Dusk: Spotted tracks near perimeter.
Day 1, Night: The Wounded Enemy. Resolved.
Day 2, Dawn: Morning muster complete. Nothing to report.
Day 2, Midday: Routine duties. All in order.
```

### Order Forecasting & Warnings (Phase 10)

**Status:** ✅ Implemented (2025-12-31)  
**Implementation:** `src/Features/Orders/Behaviors/OrderManager.cs`, `src/Features/Orders/Models/Order.cs`

**Purpose:** Provide advance warnings before orders issue so players have time to react at fast-forward speeds (>>) without missing duties.

**Problem Solved:**  
At fast-forward speed, orders appeared instantly with no time to prepare. Players missed the chance to complete activities, adjust equipment, or use QM before duty started.

**Solution: Imminent Warning System**

Orders now transition through warning states before becoming mandatory:

```
IMMINENT (4-8h warning)
  ↓
  "Sergeant will call for you soon"
  Shows in AHEAD forecast with countdown
  
PENDING (issued)
  ↓
  Appears in Orders menu
  Player can accept/decline
  
ACTIVE (accepted)
  ↓
  Order progresses through phases
  Events fire during execution
  
COMPLETE (finished)
  ↓
  Results in Recent Activity feed
```

**Implementation Details:**

```csharp
// Order.cs
public enum OrderState
{
    Imminent,  // 4-8h warning before issue
    Pending,   // Issued, awaiting player response
    Active,    // Accepted, progressing
    Complete   // Finished
}

public class Order
{
    public OrderState State { get; set; }
    public CampaignTime ImminentTime { get; set; }  // When warning began
    public CampaignTime IssueTime { get; set; }     // When order will be issued
    public CampaignTime ExpirationTime { get; set; }
}

// OrderManager.cs
public void CreateImminentOrder(Order order)
{
    var hoursUntilIssue = MBRandom.RandomFloatRanged(4f, 8f);
    order.State = OrderState.Imminent;
    order.ImminentTime = CampaignTime.Now;
    order.IssueTime = CampaignTime.HoursFromNow(hoursUntilIssue);
    order.ExpirationTime = CampaignTime.HoursFromNow(hoursUntilIssue + 72f);
}

public string GetImminentWarningText() { ... }
public float GetHoursUntilIssue() { ... }
public bool IsOrderImminent() { ... }
```

**UI Integration:**
- **AHEAD section:** Shows imminent warnings with countdown
- **ORDERS menu:** Displays "Order Assignment: Guard Duty (in 6 hours)"
- **Hourly tick:** `OrderManager.UpdateOrderState()` transitions Imminent → Pending when IssueTime arrives

**Time Speed Support:**
- **Play (1x):** 4-8h = 13-26 real seconds warning
- **FastForward (>>):** 4-8h = 3.3-6.6 real seconds warning
- Critical for playability at high speeds

**Benefits:**
- Players can prepare before duty starts
- Natural tension builds ("Something's coming")
- Time to finish current activities
- Immersive command structure (orders aren't instant surprises)

**Forecast Elements:**

| Element | Example | Data Source |
|---------|---------|-------------|
| Imminent order | "Guard Duty in 6 hours" | OrderManager.GetImminentWarningText() |
| Next commitment | "Training this evening (3h)" | CampOpportunityGenerator |
| Order status | "Guard Duty - Day 2 of 3" | OrderManager.GetCurrentOrder() |
| Schedule forecast | "Dusk: Downtime, Evening meal" | CampScheduleManager |

**Example Forecast Display:**
```
_____ UPCOMING _____

Imminent: Guard Duty in 6 hours
Current Order: None (between duties)
Next Activity: Training drill this evening (3h)

Tomorrow's Schedule:
  Dawn: Formation, Morning muster
  Midday: Work detail
  Dusk: Downtime, Evening meal
  Night: Open (rest recommended)
```

**See Also:**  
- [Event System Schemas - Order State Enum](../Features/Content/event-system-schemas.md#order-state-enum-c-model) - Complete order state model  
- [Content System Architecture](../Features/Content/content-system-architecture.md) - Orchestrator integration

---

## Orchestrator Integration

The Content Orchestrator manages event injection during orders.

### Activity Level Affects Event Chance

**Base Chances (2025-12-31 Update):**
- Slot phase base: 8% (reduced from 15%)
- Slot! phase base: 15% (reduced from 35%)
- **Result:** Routine phases are now the majority (~12% daily event chance = 1 event per 8 days)

| Activity Level | Slot Modifier | Slot! Modifier |
|----------------|---------------|----------------|
| Quiet (garrison) | ×0.25 | ×0.25 |
| Routine (peacetime) | ×0.50 | ×0.50 |
| Active (campaign) | ×1.0 | ×1.0 |
| Intense (siege) | ×2.0 | ×2.0 |

**Example: Guard Post during siege vs garrison**

Guard Post has slots at: Dusk Day 1 (8%), Night Day 1 (15%), Dawn Day 2 (8%)

Garrison (Quiet):
- Dusk: 8% × 0.25 = 2%
- Night: 15% × 0.25 = 3.75%
- Dawn: 8% × 0.25 = 2%
- **Expected events: ~0.08** (very rare, routine duty)

Siege (Intense):
- Dusk: 8% × 2.0 = 16%
- Night: 35% × 1.3 = 45.5%
- Dawn: 15% × 1.5 = 22.5%
- **Expected events: ~0.9** (something probably happens)

### Phase Processing Logic

```csharp
private void ProcessOrderPhase(ActiveOrder order, int phaseIndex)
{
    var block = order.Definition.Blocks[phaseIndex];
    var worldState = WorldStateAnalyzer.GetCurrentSituation();
    
    // 1. Always add status to Recent Activity
    var entry = new ActivityEntry
    {
        Timestamp = CampaignTime.Now,
        Phase = block.Phase,
        Day = order.CurrentDay,
        Type = ActivityType.OrderProgress,
        Body = block.StatusText
    };
    ActivityLog.AddEntry(entry);
    
    // 2. Accumulate passive consequences
    order.AccumulatedFatigue += block.FatiguePerPhase;
    order.AccumulatedXP += GetPassiveXPPerPhase(order.PlayerTier);
    
    // 3. Check for injury (physical orders)
    if (order.Definition.InjuryRisk > 0)
    {
        float injuryChance = CalculateInjuryChance(order);
        if (MBRandom.RandomFloat < injuryChance)
        {
            ApplyInjury(order, entry);
        }
    }
    
    // 4. Check if slot should fire event
    if (block.Type == BlockType.Slot || block.Type == BlockType.HighSlot)
    {
        float baseChance = block.Type == BlockType.HighSlot ? 0.35f : 0.15f;
        float modifier = GetActivityModifier(worldState, block);
        float finalChance = baseChance * modifier;
        
        if (MBRandom.RandomFloat < finalChance)
        {
            var orderEvent = SelectOrderEvent(order, worldState);
            if (orderEvent != null)
            {
                StoryDirector.Instance.EmitCandidate(/* orderEvent wrapped in StoryCandidate */); // migrated in commit 2f297c6
                entry.AwaitingDecision = true;
                entry.Icon = "⚠️";
                entry.Body += "\n→ Something's happening...";
            }
        }
    }
    
    // 5. Handle checkpoint (mini-reward)
    if (block.Type == BlockType.Checkpoint)
    {
        ApplyCheckpointReward(order, entry);
    }
    
    // 6. Check for completion
    if (block.Type == BlockType.Resolve)
    {
        ResolveOrder(order);
    }
}
```

### Order Completion Resolution

```csharp
private void ResolveOrder(ActiveOrder order)
{
    // Calculate final outcome based on accumulated choices
    var outcome = CalculateOutcome(order);
    
    // Apply consequences
    ApplyFatigue(order.AccumulatedFatigue);
    ApplyXP(order.AccumulatedXP + GetCompletionBonus(outcome));
    ApplyReputation(order.Definition.Resolution[outcome]);
    ApplyCompanyNeeds(order.Definition.Resolution[outcome]);
    
    // Gold reward (T4+ orders)
    if (order.Definition.GoldReward > 0)
    {
        int gold = CalculateGoldReward(order, outcome);
        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, gold);
    }
    
    // Log completion
    var summary = BuildCompletionSummary(order, outcome);
    ActivityLog.AddEntry(new ActivityEntry
    {
        Type = ActivityType.OrderComplete,
        Icon = outcome == OrderOutcome.Success ? "✅" : outcome == OrderOutcome.Partial ? "⚡" : "❌",
        Title = $"ORDER COMPLETE: {order.Definition.Title}",
        Body = summary
    });
    
    // Clear active order
    OrderManager.ClearActiveOrder();
}
```

---

## UI Integration

### Recent Activity as Order Journal

The Recent Activity section in Camp Hub displays the order's progression:

```
_____ RECENT ACTIVITY _____

[Night - Day 2] ✅ ORDER COMPLETE
━━━━━━━━━━━━━━━━━━━━━━━━━━━
Guard Post

"Quiet watch. Nothing to report."
- Sergeant Aldric

OUTCOME: Success
• +8 Officer Reputation
• +18 Athletics XP
• +5 Readiness
• Fatigue: +12

━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Dusk - Day 2]
Almost done. Shift ends soon.

[Midday - Day 2]
Long hours. A soldier nods as she passes.

[Dawn - Day 2] ✨ CHECKPOINT
Morning inspection. All clear. (+5 XP)

[Night - Day 1]
Dark and quiet. Stars visible between clouds.

[Dusk - Day 1]
Evening settles over camp. Quiet.

[Midday - Day 1]
Sun overhead. Boring duty.

[Dawn - Day 1] 📋 ORDER ACCEPTED
You report to the guard post. Sergeant assigns your position.
```

### Entry Icons

| Icon | Meaning |
|------|---------|
| 📋 | Order accepted/started |
| ⏳ | Routine phase |
| ✨ | Checkpoint (mini-reward) |
| ⚠️ | Event pending or resolved |
| ✅ | Order complete (success) |
| ⚡ | Order complete (partial) |
| ❌ | Order failed |
| 🩹 | Injury occurred |

### Combat Log Notifications

Order state changes are announced in the combat log for immediate player feedback:

- **New Order Available**: Yellow message when order is issued
- **Order Started**: Green message when player accepts
- **Order Declined**: Red message when player declines
- **Order Completed**: Green message with outcome when order finishes

These provide real-time awareness without requiring the player to open menus.

### Active Order Card

When order is active, Camp Hub shows status:

```
┌─────────────────────────────────────────┐
│ 🛡️ CURRENT DUTY                         │
│ Guard Post                              │
│ Assigned by: Sergeant Aldric            │
│                                         │
│ [████████████░░░░] Day 2, Midday        │
│                                         │
│ Fatigue: +8 so far                      │
│ Status: Quiet duty. Waiting...          │
└─────────────────────────────────────────┘
```

When event fires:

```
┌─────────────────────────────────────────┐
│ ⚠️ DUTY REQUIRES ATTENTION               │
│ Guard Post                              │
│                                         │
│ Something's happening at your post!     │
│                                         │
│ [Handle Situation →]                    │
└─────────────────────────────────────────┘
```

---

## Technical Specification

### Data Models

#### OrderDefinition
```csharp
public class OrderDefinition
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string TitleId { get; set; }  // Localization
    public string Description { get; set; }
    public string DescriptionId { get; set; }
    
    public string Issuer { get; set; }  // "Sergeant", "Captain", "Lord"
    public bool Mandatory { get; set; } = false;  // If true, automatically assigned (no player choice)
    public int TierMin { get; set; }
    public int TierMax { get; set; }
    
    public string PrimarySkill { get; set; }
    public string SecondarySkill { get; set; }
    
    public int DurationDays { get; set; }
    public OrderWeight FatigueWeight { get; set; }  // Light, Medium, Heavy
    public float BaseInjuryRisk { get; set; }
    
    public List<OrderBlock> Blocks { get; set; }
    public List<string> EventPool { get; set; }
    
    public Dictionary<OrderOutcome, OrderResolution> Resolution { get; set; }
    
    public List<string> StrategicTags { get; set; }
    public int CooldownDays { get; set; }
    
    // Order-Decision Tension: How strictly is this order supervised?
    // 1.0 = normal, 1.5 = strict oversight, 0.7 = relaxed oversight
    public float OversightMultiplier { get; set; } = 1.0f;
}

public class OrderBlock
{
    public string Phase { get; set; }  // "Dawn", "Midday", "Dusk", "Night"
    public int Day { get; set; }
    public BlockType Type { get; set; }  // Routine, Slot, HighSlot, Checkpoint, Resolve
    public string StatusText { get; set; }
    public string StatusTextId { get; set; }
    public float EventChance { get; set; }  // Override base chance if specified
}

public enum BlockType
{
    Routine,
    Slot,
    HighSlot,
    Checkpoint,
    Resolve
}

public enum OrderWeight
{
    Light,
    Medium,
    Heavy,
    Physical
}

public enum OrderOutcome
{
    Success,
    Partial,
    Failure
}
```

#### ActiveOrder
```csharp
public class ActiveOrder
{
    public OrderDefinition Definition { get; set; }
    public CampaignTime StartTime { get; set; }
    public int CurrentPhaseIndex { get; set; }
    public int CurrentDay { get; set; }
    
    public float AccumulatedFatigue { get; set; }
    public float AccumulatedXP { get; set; }
    public int EventsHandled { get; set; }
    public int SkillChecksPassed { get; set; }
    public int SkillChecksFailed { get; set; }
    
    public List<string> ChoicesMade { get; set; }
    public bool HasPendingEvent { get; set; }
    public OrderEvent PendingEvent { get; set; }
}
```

### File Structure

```
src/Features/Orders/
├── Behaviors/
│   ├── OrderManager.cs           (existing - modify)
│   └── OrderProgressionBehavior.cs (new - phase ticking)
├── OrderDefinition.cs            (existing - extend)
├── OrderCatalog.cs               (existing - keep)
├── Progression/
│   ├── ActiveOrder.cs            (new)
│   ├── OrderPhaseProcessor.cs    (new)
│   ├── OrderEventSelector.cs     (new)
│   └── OrderConsequenceApplier.cs (new)
└── Events/
    └── OrderEventDefinition.cs   (new)

ModuleData/Enlisted/Orders/
├── orders_t1_t3.json             (existing - extend with blocks)
├── orders_t4_t6.json             (existing - extend with blocks)
└── order_events/
    ├── guard_events.json         (new)
    ├── patrol_events.json        (new)
    ├── scout_events.json         (new)
    └── ... (per order type)
```

### Phase Tick Registration

```csharp
public class OrderProgressionBehavior : CampaignBehaviorBase
{
    private const string LogCategory = "Orders";
    
    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }
    
    private void OnHourlyTick()
    {
        if (!EnlistmentBehavior.Instance.IsEnlisted) return;
        if (!OrderManager.Instance.HasActiveOrder) return;
        
        var currentHour = CampaignTime.Now.GetHourOfDay;
        var phaseHour = GetPhaseHour(currentHour);
        
        if (phaseHour != -1 && !HasProcessedPhaseThisHour())
        {
            ProcessCurrentPhase();
            MarkPhaseProcessed();
        }
    }
    
    private int GetPhaseHour(int hour)
    {
        // Dawn: 6, Midday: 12, Dusk: 18, Night: 0
        if (hour == 6) return 0;   // Dawn
        if (hour == 12) return 1;  // Midday
        if (hour == 18) return 2;  // Dusk
        if (hour == 0) return 3;   // Night
        return -1;
    }
}
```

### Save/Load

```csharp
public override void SyncData(IDataStore dataStore)
{
    SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
    {
        // Active order state
        dataStore.SyncData("order_hasActive", ref _hasActiveOrder);
        dataStore.SyncData("order_activeId", ref _activeOrderId);
        dataStore.SyncData("order_startTime", ref _orderStartTime);
        dataStore.SyncData("order_currentPhase", ref _currentPhaseIndex);
        dataStore.SyncData("order_accumulatedFatigue", ref _accumulatedFatigue);
        dataStore.SyncData("order_accumulatedXP", ref _accumulatedXP);
        dataStore.SyncData("order_eventsHandled", ref _eventsHandled);
        dataStore.SyncData("order_choicesMade", ref _choicesMade);
    });
}
```

---

## Order Issuance & Non-Order Time

### When Orders Are Issued

Orders are issued by the chain of command (Sergeant for T1-T3, Captain for T4-T6) when:

1. **Player has no active order** - Prerequisite for new assignment
2. **Player fatigue < 80** - Too exhausted = "rest first, soldier"
3. **Orchestrator timing allows** - Coordinated with world state rhythm
4. **Cooldown passed** - Minimum 4-8 hours between order completion and new assignment

**Issuance Frequency by Activity Level:**

| Activity Level | Time Between Orders | Notes |
|----------------|---------------------|-------|
| Quiet (garrison) | 3-5 days | Plenty of downtime |
| Routine (peacetime) | 2-3 days | Regular duty rotation |
| Active (campaign) | 1-2 days | Constant work |
| Intense (siege) | 0.5-1 day | Barely time to breathe |

### Order Issuance Flow

```
1. Player completes order (or has none)
2. OrderManager checks:
   - Is player rested enough? (fatigue < 80)
   - Has cooldown passed?
   - Does orchestrator allow? (checks world state timing)
3. If all yes → Select appropriate order for tier + context
4. Issue order popup → Player accepts/declines
5. If accepted → Order starts, phases begin ticking
6. If declined → Reputation penalty, try again next evaluation
```

### Non-Order Time (Between Duties)

When player has no active order, they're "off duty" and can:

**Available Actions:**
- **Camp Hub decisions** - Training, social, economic, rest
- **Rest to recover fatigue** - Critical after heavy orders
- **Wait for next assignment** - Time passes

**Narrative Events During Off-Duty:**

EventPacingManager fires **camp life events** during non-order time:
- Social interactions with soldiers
- Camp incidents (arguments, visitors, rumors)
- Personal opportunities

Event selection uses world state from ContentOrchestrator for context-appropriate content. Daily event attempts respect global pacing limits (max 1/day, 4/week, 12+ hours apart).

**Expected frequency varies by activity level:**
- Quiet: ~1/week, Routine: ~3/week, Active: ~5/week, Intense: ~7/week

**The Rhythm:**

```
Typical gameplay loop:

[Order: Guard Post - 2 days]
  └→ Phases tick, maybe 1 event fires
  └→ Order completes, +fatigue accumulated

[Off-Duty: ~4-12 hours]
  └→ Player uses Camp Hub to rest
  └→ Maybe a camp life event fires
  
[Order: Camp Patrol - 1 day]
  └→ Phases tick, probably quiet
  └→ Order completes

[Off-Duty: ~4-8 hours]
  └→ Player trains, socializes
  
[Order: Sentry Duty - 1 day]
  └→ Night slot fires an event!
  └→ Player handles it, gets bonus XP
  └→ Order completes

... repeat for years of service
```

### Orchestrator's Role in Orders

The Content Orchestrator provides coordination but does NOT directly control orders:

| Responsibility | Who Handles It |
|----------------|----------------|
| When to issue new order | OrderManager (with orchestrator timing) |
| Which order to issue | OrderManager (based on tier, context) |
| When order phases tick | OrderProgressionBehavior (hourly) |
| Event chance during order | OrderProgressionBehavior (uses WorldSituation) |
| Camp life events (off-duty) | ContentOrchestrator (standard flow) |

**Data Flow:**

```csharp
// OrderManager checks with orchestrator before issuing
if (ContentOrchestrator.Instance.CanIssueOrderNow())
{
    var worldState = WorldStateAnalyzer.GetCurrentSituation();
    var order = SelectOrderForContext(playerTier, worldState);
    IssueOrder(order);
}

// OrderProgressionBehavior uses world state for event weighting
var worldState = WorldStateAnalyzer.GetCurrentSituation();
float modifier = GetActivityModifier(worldState.ExpectedActivity);
float eventChance = baseChance * modifier;
```

---

## Edge Cases

### Battle Interrupts Order

If a battle starts while order is active:

1. Order pauses (no phase progression during battle)
2. After battle, order resumes at current phase
3. If player was seriously wounded, order may auto-fail ("unfit for duty")
4. If player captured, order fails

### Lord Moves While Scouting

If lord moves settlements while player is on multi-day scout order:

1. Order continues (you're out scouting, lord can move)
2. On completion, you return to wherever lord is now
3. If lord was defeated/captured during your order, special event fires

### Player Declines Mid-Order Event

If player ignores an event popup:

1. After 2 phases, event auto-resolves with penalty
2. "You hesitated too long. The moment passed."
3. Negative consequence applied

### Order Stacking

Only one order active at a time for T1-T6. Player must complete or fail current order before accepting new one.

Exception: Emergency orders (siege assault, retreat) can override and cancel current order.

### Fatigue Blocks New Order

If fatigue > 80, player cannot accept new order until rested:
- "You're too exhausted for duty. Rest first."
- Forces player to use Rest decision

### Order During Muster

If muster cycle hits while order is active:

1. Order pauses for muster
2. Resume after muster completes
3. No penalty - muster takes precedence

---

## Acceptance Criteria

### Core System

- [ ] Orders define blocks with phase, day, and type
- [ ] Phase ticks process at 6am, 12pm, 6pm, 12am game time
- [ ] Recent Activity updates with status text each phase
- [ ] Fatigue accumulates per phase based on order weight
- [ ] Passive XP accumulates per phase based on tier

### Event Integration

- [ ] Slot phases roll for event chance
- [ ] Event chance modified by world state activity level
- [ ] Events selected from order-specific event pool
- [ ] Event popups fire when events trigger
- [ ] Player choices recorded and affect outcome

### Consequences

- [ ] Fatigue affects skill check success
- [ ] Injury checks run on physical orders
- [ ] Injury triggers Medical Progression System
- [ ] Failed skill checks have consequences

### Completion

- [ ] Orders resolve after final phase
- [ ] Outcome calculated from accumulated choices
- [ ] Rewards applied (rep, XP, gold, company needs)
- [ ] Completion summary displayed in Recent Activity

### Orchestrator

- [ ] Orchestrator provides world state for event weighting
- [ ] Activity level modifies event frequency
- [ ] World state affects event pool selection

### UI

- [ ] Recent Activity shows order progression
- [ ] Active order card shows current status
- [ ] Event pending state clearly indicated
- [ ] Completion summary is comprehensive

### Save/Load

- [ ] Active order persists through save/load
- [ ] Phase progress preserved
- [ ] Accumulated consequences preserved

---

## Future Expansion (T7-T9)

T7-T9 orders will be covered in a separate specification. Key differences:

- **Multiple subordinate orders**: Player assigns orders to retinue
- **Strategic orders**: Multi-week campaigns, not daily tasks
- **Delegation**: Choose who handles complications
- **Command events**: Managing men, not doing the work
- **Higher stakes**: Failure affects more people

---

**End of Document**

