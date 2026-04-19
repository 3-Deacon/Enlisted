# Camp Life Simulation

**Summary:** The camp life simulation combines two layers: Company Needs (transparent player-facing metrics) and CampLifeBehavior (backend simulation for Quartermaster mood). Players interact through 34 Camp Hub decisions and context-triggered camp events, with Fatigue gating intensive actions. This system provides a living military camp experience with meaningful choices and consequences.

**Status:** ✅ Current  
**Last Updated:** 2025-12-23  
**Related Docs:** [Company Supply](../Equipment/company-supply-simulation.md), [Core Gameplay](../Core/core-gameplay.md), [Camp Fatigue](../Core/camp-fatigue.md)

---

## Index
- [Overview](#overview)
- [Architecture](#architecture)
- [The Two Company Needs](#the-two-company-needs)
- [Camp Activities System](#camp-activities-system)
- [CampLifeBehavior (Backend Layer)](#camplifebehavior-backend-layer)
- [Fatigue System Integration](#fatigue-system-integration)
- [Daily Simulation](#daily-simulation)
- [Player Experience](#player-experience)
- [System Integrations](#system-integrations)
- [Configuration](#configuration)
- [Technical Implementation](#technical-implementation)

---

## Overview

Camp life simulation runs continuously while enlisted, tracking the state of your company through multiple interconnected systems. The simulation has two distinct layers:

**Player-Facing Layer (Company Needs):** Two transparent metrics (0-100) that represent your unit's operational status. Players see these values, understand their impact, and take actions to manage them.

**Note:** Company Rest was removed (2026-01-11) as redundant with the Player Fatigue system (0-24 budget).

**Backend Layer (CampLifeBehavior):** Hidden metrics that respond to campaign events (battles, time away from towns, pay delays) and drive dynamic Quartermaster pricing and mood.

**Player Agency:** 34 player-initiated decisions in the Camp Hub menu, plus context-triggered camp events that respond to game situations.

---

## Architecture

Camp life simulation consists of four integrated systems:

```
┌─────────────────────────────────────────────────────┐
│  PLAYER-FACING LAYER (Transparent & Interactive)    │
├─────────────────────────────────────────────────────┤
│                                                      │
│  Company Needs (0-100)                              │
│  ├─ Readiness: Combat preparation                   │
│  └─ Supply: Food & logistics                        │
│  Note: Rest removed 2026-01-11 (use Player Fatigue) │
│                                                      │
│  Fatigue (0-24)                                     │
│  └─ Gates intensive camp actions                    │
│                                                      │
│  Camp Decisions (34 player-initiated)               │
│  └─ Training, Social, Economic, Career, etc.        │
│                                                      │
│  Camp Events (context-triggered)                    │
│  └─ Gambling, storytelling, etc.                    │
│                                                      │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│  BACKEND LAYER (Hidden Simulation)                  │
├─────────────────────────────────────────────────────┤
│                                                      │
│  CampLifeBehavior Meters (0-100)                    │
│  ├─ LogisticsStrain: Days from town + raids         │
│  ├─ MoraleShock: Battle aftermath (spikes/decays)   │
│  ├─ PayTension: Unpaid wages                        │
│  └─ TerritoryPressure: Village raids                │
│                                                      │
│  Quartermaster Mood (derived from above)            │
│  └─ Fine → Tense → Sour → Predatory                 │
│      (affects pricing: 0.98x to 1.15x)              │
│                                                      │
└─────────────────────────────────────────────────────┘
```

These layers interact:
- Orders affect Company Needs
- Low Company Needs trigger events
- Backend meters adjust Quartermaster pricing
- Fatigue gates decision availability

---

## The Two Company Needs

The player-facing simulation layer tracks two transparent metrics (0-100) representing your company's operational status.

**Note:** Company Rest was removed (2026-01-11). Player Fatigue (0-24 budget) handles stamina for camp decisions.

### 1. Readiness (Combat Preparation)
**Range:** 0-100  
**What it represents:** Unit combat effectiveness, training level, tactical sharpness

**Affected by:**
- Training decisions (+5 to +15 per session)
- Successful orders (+3 to +10)
- Combat results (victory +5, defeat -10)
- Daily degradation (-2 base, -5 if on march)

**Impact when low (<30):**
- Reduced order success chance
- Lower reputation gains
- Risk of poor battle performance

### 2. Supply (Food & Consumables)
**Range:** 0-100  
**What it represents:** Food stocks and basic consumables

**Special:** Player receives rations directly as part of enlisted service; all supplies (rations, ammo, repairs, camp supplies) tracked as single metric via CompanySupplyManager

**Affected by:**
- Resupply orders (+10 to +20)
- CompanySupplyManager daily consumption
- Party movement and size

**Impact when low (<30):**
- Quartermaster restricts high-tier gear access
- Risk of food shortage events
- Reduced company effectiveness

### 3. ~~Rest~~ (REMOVED 2026-01-11)
**Replaced by:** Player Fatigue (0-24 budget system)

**Why removed:** Company-wide Rest metric was redundant. Player Fatigue already gates camp decisions with health penalties. No gameplay value was lost - the functional system remains.

### Critical Thresholds

**Status Levels:**
- **Excellent (80-100)**: Bonuses to order success chance (+10%), reputation gains (+20%)
- **Good (60-79)**: Standard operations, no modifiers
- **Fair (40-59)**: Minor penalties to order outcomes
- **Poor (30-39)**: Warnings issued, risk of threshold events
- **Critical (0-29)**: Severe penalties, access restrictions, high-risk events

**Threshold Checks:**
- Evaluated daily by CompanyNeedsManager
- Crossing below 30 triggers warnings in Daily Brief
- Crossing below 20 triggers immediate intervention events

### Needs Prediction

The system forecasts upcoming requirements based on **Strategic Context** (loaded from `strategic_context_config.json`):

**Grand Campaign (Offensive):**
- Readiness: 85 (high combat demands)
- Supply: 80 (long operations)
- Rest: 50 (intense tempo)

**Last Stand (Desperate Defense):**
- Readiness: 90 (critical preparation)
- Supply: 60 (disrupted logistics)

**Winter Camp (Seasonal Rest):**
- Readiness: 55 (training continues)
- Supply: 60 (stable)

**Note:** Rest predictions removed 2026-01-11 (metric no longer exists).

Players can view these dynamics in the main Enlisted Status menu (COMPANY REPORTS) and Camp Hub (COMPANY STATUS with UPCOMING section) to prepare for upcoming operations.

---

## Camp Activities System

Camp activities are delivered through two mechanisms: **Player-Initiated Decisions** (Camp Hub menu) and **Context-Triggered Events** (automatic popups).

### Player-Initiated Decisions (34 Total)

Accessed via **Camp Hub (C key)** → Category navigation. Decisions are filtered by tier, cooldowns, costs, and requirements.

**Decision Categories:**

**CAMP_LIFE (3 decisions):**
- `dec_rest`: Quick rest (1-day cooldown, restores 2-5 fatigue)
- `dec_rest_extended`: Full night's sleep (3-day cooldown, 8 hours, +5 fatigue, +10 HP)
- `dec_seek_treatment`: Visit surgeon (costs denars, treats conditions)

**TRAINING (9 decisions):**
- `dec_weapon_drill`: Practice with your weapon (+weapon skill XP)
- `dec_spar`: Spar with other soldiers (+Athletics, +weapon skills)
- `dec_endurance`: Build stamina (+Athletics, +Riding)
- `dec_study_tactics`: Learn military theory (+Tactics, +Leadership)
- `dec_practice_medicine`: Train medical skills (+Medicine)
- `dec_train_troops`: Commander training (T7+, +Leadership)
- `dec_combat_drill`: Advanced combat training
- `dec_weapon_specialization`: Focus on weapon mastery
- `dec_lead_drill`: Formation leadership practice (T5+)

**SOCIAL (6 decisions):**
- `dec_join_men`: Socialize with soldiers (+Soldier Rep)
- `dec_join_drinking`: Drink with the men (+Soldier Rep, -denars)
- `dec_seek_officers`: Network with officers (+Officer Rep)
- `dec_keep_to_self`: Maintain distance (neutral)
- `dec_write_letter`: Correspondence (personal time)
- `dec_confront_rival`: Handle conflicts (risky)

**ECONOMIC (5 decisions):**
- `dec_gamble_low`: Low-stakes gambling (risk vs reward)
- `dec_gamble_high`: High-stakes gambling (bigger risk/reward)
- `dec_side_work`: Earn extra denars (+25-50 gold)
- `dec_shady_deal`: Risky money schemes (+scrutiny risk)
- `dec_visit_market`: Trade with locals (if near town)

**CAREER (3 decisions):**
- `dec_request_audience`: Meet with your lord (reputation check)
- `dec_volunteer_duty`: Take extra assignments (+reputation)
- `dec_request_leave`: Ask for temporary leave (tier-gated)

**INFORMATION (3 decisions):**
- `dec_listen_rumors`: Gather intelligence (+Scouting)
- `dec_scout_area`: Reconnaissance work (+Scouting, strategic info)
- `dec_check_supplies`: Audit company stocks (reveals needs)

**EQUIPMENT (2 decisions):**
- `dec_maintain_gear`: Perform maintenance (roleplay flavor)
- `dec_visit_quartermaster`: Open quartermaster shop

**RISK_TAKING (3 decisions):**
- `dec_dangerous_wager`: High-risk bet (skill check)
- `dec_prove_courage`: Challenge or dare (+reputation or -reputation)
- `dec_challenge`: Personal combat challenge (risky)

**Decision Mechanics:**
- **Cooldowns:** 1-10 days per decision (prevents spam)
- **Costs:** Fatigue (1-8 points), denars (10-100), time (hours)
- **Requirements:** Tier gates, skill checks, context filters
- **Effects:** Company Needs (+/-), reputation, skill XP, trait XP, gold
- **Delivery:** Via `DecisionManager` + `DecisionCatalog`

### Context-Triggered Camp Events

Automatic events that fire when conditions are met (via `EventPacingManager`).

**Camp Context Events (examples from `camp_events.json`):**

**evt_camp_gambling:**
- **Trigger:** Camp context, T1+, 8-day cooldown
- **Setup:** Dice game in progress
- **Options:**
  - Play (risk coin for +50 gold, +5 Soldier Rep, +8 Roguery XP)
  - Watch (safe, +2 Soldier Rep, +3 Roguery XP)
  - Report (-12 Soldier Rep, +6 Officer Rep, +5 Discipline)

**evt_camp_storytelling:**
- **Trigger:** Camp context, T2+, 10-day cooldown
- **Setup:** Evening fire, request for tales
- **Options:**
  - Tell battle stories (Charm 20+, +12 Charm XP, +6 Soldier Rep)
- Tell legends (Charm 35+, +15 Charm XP, +8 Soldier Rep)
  - Listen (no requirements, small gains)

**Event Characteristics:**
- Fire every 3-5 days (global pacing limits)
- Context-filtered (only when in "Camp" strategic context)
- No cooldown overlap with other event categories
- Can affect all reputation tracks and Company Needs

### Decision vs Event Comparison

| Aspect | Player Decisions | Context Events |
|--------|------------------|----------------|
| **Trigger** | Player initiates via menu | Game triggers based on conditions |
| **Frequency** | Always available (if not on cooldown) | Every 3-5 days (pacing controlled) |
| **Control** | Full player control | Player responds to situation |
| **Cooldowns** | Per-decision (1-10 days) | Global pacing (category cooldowns) |
| **Costs** | Fatigue, denars, time | Usually consequence-based |
| **Access** | Camp Hub (C key) → Category | Popup inquiry (automatic) |

---

## CampLifeBehavior (Backend Layer)

While Company Needs are player-facing, `CampLifeBehavior` runs a hidden simulation that responds to campaign events and drives dynamic systems.

### Internal Meters (0-100, Player Never Sees These)

**LogisticsStrain:**
- Calculated daily from: (days since town × 12) + (villages looted × 10) + (battles this week × 4)
- Represents supply chain stress, fatigue of logistics system
- Feeds into Quartermaster mood calculation (55% weight)

**MoraleShock:**
- Spikes to 70 immediately after battles
- Decays -8 per day when no combat
- Additional +5 if 2+ battles this week
- Represents psychological aftermath of combat
- Feeds into Quartermaster mood calculation (15% weight)

**PayTension:**
- Read directly from `EnlistmentBehavior.PayTension` (authoritative source)
- Tracks unpaid wages, backpay, late payment issues
- Managed by pay muster system
- Feeds into Quartermaster mood calculation (35% weight)

**TerritoryPressure:**
- Calculated from: villages looted × 12
- Light placeholder for territorial stress
- Minimal current impact

### Quartermaster Mood Derivation

**Formula:** `(LogisticsStrain × 0.55) + (PayTension × 0.35) + (MoraleShock × 0.15)`

**Mood Tiers:**
- **Fine (0-24):** Baseline pricing (0.98x purchase, 1.0x buyback)
- **Tense (25-49):** Slightly worse (1.0x purchase, 0.95x buyback)
- **Sour (50-74):** Significant markup (1.07x purchase, 0.85x buyback)
- **Predatory (75-100):** Exploitative pricing (1.15x purchase, 0.75x buyback)

**Why This Exists:**
- Creates dynamic pricing that responds to campaign stress
- QM becomes expensive after long operations, cheap during stable periods
- Provides feedback about campaign intensity without direct visibility
- Feels natural (supply issues → higher prices)

**What Players See:**
- Quartermaster dialogue hints at mood ("Times are tight...")
- Price differences in equipment shop
- No direct meter display

### Weekly Counters

Tracked per calendar week (resets every 7 days):
- `_battlesThisWeek`: Counts player-involved battles
- `_villagesLootedThisWeek`: Tracks raid frequency

These feed into the daily snapshot calculation.

---

## Fatigue System Integration

Fatigue acts as a **stamina budget** for intensive camp actions, preventing players from exhausting all activities at once.

### Fatigue Basics

**Capacity:** 24 points (default), capped at 18 during probation  
**Display:** Shows in Enlisted Status menu as "Fatigue: 14/24"  
**Purpose:** Gates demanding activities to create meaningful choices

### What Consumes Fatigue

**Camp Decisions:**
- Training activities: 2-5 fatigue per session
- Economic work: 1-3 fatigue
- Social activities: 1-2 fatigue
- Risk-taking: 2-4 fatigue

**Orders:**
- Standard orders: 3-5 fatigue
- Intensive orders: 6-10 fatigue
- Strategic context affects costs (Grand Campaign +2)

**Events:**
- Some event options cost fatigue
- High-intensity choices: 1-4 fatigue

### Fatigue Restoration

**Primary:** Rest decisions
- `dec_rest`: Restores 2 fatigue (1-day cooldown)
- `dec_rest_extended`: Restores 5 fatigue (3-day cooldown, 8 hours)

**Secondary:** Event rewards with `fatigueRelief`
- Some camp events restore 1-3 fatigue
- Social events may restore small amounts

**Recovery Rate:** Affected by strategic context
- Winter Camp / Garrison: Faster natural recovery
- Last Stand / Grand Campaign: Slower recovery
- Rest need affects recovery efficiency

### Health Penalties

When fatigue drops critically low:
- `CheckFatigueHealthPenalty()` evaluates risk
- Can trigger injury or illness conditions
- Encourages rest management

**API Access:**
- `EnlistmentBehavior.TryConsumeFatigue(amount, reason)`: Returns false if insufficient
- `EnlistmentBehavior.RestoreFatigue(amount, reason)`: Restores by amount
- `EnlistmentBehavior.FatigueCurrent`: Read-only property

---

## Daily Simulation

Every day at daily tick, multiple systems run:

### 1. Company Needs Degradation (CompanyNeedsManager)

**Base Rates:**
- Readiness: -2 per day
- Supply: Handled by CompanySupplyManager

**Accelerated Conditions:**
- In combat: Additional degradation effects
- On long march: Readiness -5

**Note:** Rest degradation removed 2026-01-11 (metric no longer tracked).

### 2. CampLifeBehavior Snapshot Update

**Triggers:**
- Daily tick (gates to prevent duplicate runs)
- Weekly counter reset (every 7 days)

**Calculations:**
- LogisticsStrain from campaign state
- MoraleShock decay or spike
- PayTension read from pay system
- TerritoryPressure from raid count

**Result:** Updates QuartermasterMoodTier

### 3. Critical Need Checks

**Thresholds:**
- Poor (<30): Warnings issued
- Critical (<20): Immediate intervention needed

**Actions:**
- Warnings posted to Daily Brief
- Risk of threshold events triggering
- Access restrictions may apply

### 4. Event Pacing Evaluation

**EventPacingManager:**
- Checks if event window elapsed (3-5 days)
- Evaluates current context and role
- Selects candidate events
- Fires 0-1 event per day (pacing limits)

**Camp events only fire when:**
- Strategic context = "Camp"
- No other event in last 6 hours
- Daily/weekly limits not exceeded

---

## Player Experience

### Typical Day While Enlisted

**Morning (Hour 8):**
- Daily Brief shows Company Needs status
- Warnings if any needs are critical
- Order may arrive (if 3-5 days elapsed)

**Daytime (Hours 8-20):**
- Player can open Camp Hub (C key) at any time
- Navigate categories, select decisions
- Decisions with cooldowns show "X days remaining"
- Fatigue-gated decisions show "Requires X fatigue"
- Context events may fire (gambling, storytelling, etc.)

**Evening (Hour 20):**
- Social decisions more available
- Rest decisions always accessible
- Camp events more common

**Daily Tick (End of Day):**
- Company Needs degrade automatically
- CampLifeBehavior updates snapshot
- Fatigue may restore slightly
- Critical warnings issued if needed

### Decision-Making Flow

1. Player presses **C** → Camp Hub menu opens
2. Navigates to category (Training, Social, Economic, etc.)
3. Sees available decisions (filtered by tier, cooldown, fatigue)
4. Selects decision → reads setup text
5. Chooses option → sees result
6. Effects applied: Company Needs, reputation, skills, fatigue
7. Cooldown starts
8. Returns to Camp Hub or closes menu

### Managing Company Needs

**When Readiness is low (<40):**
- Select Training decisions (Weapon Drill, Combat Drill)
- Accept training orders from officers
- Avoid exhausting activities

**When Supply is low (<30):**
- Prioritize resupply orders
- Visit market if near town
- Check with Quartermaster

**When Equipment is low (<40):**
- Select Equipment Check decision
- Visit Quartermaster for repairs
- Avoid combat-heavy orders

**Note:** Company Rest removed 2026-01-11. Use Player Fatigue budget (0-24) to manage stamina for camp decisions.

---

## System Integrations

### Orders System
**Integration Point:** `OrderManager.ApplyOrderOutcome()`

Orders have `CompanyNeeds` effects defined in JSON:
```json
"effects": {
  "companyNeeds": {
    "Readiness": 10
  }
}
```

**Examples:**
- Successful patrol: +10 Readiness
- Training detail: +15 Readiness, -3 Fatigue

**Note:** Rest effects removed 2026-01-11 (metric no longer exists).

### Quartermaster Integration

**Dynamic Pricing:**
- CampLifeBehavior.QuartermasterMoodTier → pricing multiplier
- Fine mood: 2% discount (0.98x)
- Predatory mood: 15% markup (1.15x)

**Access Restrictions:**
- Supply < 30: May restrict high-tier gear

**Dialogue System:**
- Quartermaster references mood in conversation
- "Things are tight right now" (Sour mood)
- "Supply lines are stretched thin" (high LogisticsStrain)

### News & Reports System

**Daily Brief Integration:**
- Company Needs summarized with narrative descriptions
- Critical needs highlighted in red
- Recent changes noted ("Rest has declined significantly")

**Company Status Report:**
- Full breakdown of company needs (Readiness, Supply)
- Status level text (Excellent/Good/Fair/Poor/Critical)
- Contextual explanations (why each need is changing)
- Upcoming predictions based on strategic context

**Threshold Notifications:**
- Crossing below 30: Warning in Daily Brief
- Crossing below 20: Immediate popup notification

**Note:** Rest notifications removed 2026-01-11 (metric no longer tracked).

### Event System Integration

**Event Effects:**
```json
"effects": {
  "companyNeeds": {
    "Readiness": 5
  }
},
"rewards": {
  "fatigueRelief": 2
}
```

**Context Filtering:**
- Camp events only fire in "Camp" strategic context
- Training events available in "Winter Camp" or "Garrison Duty"
- Social events more common in peacetime

**Pacing Interaction:**
- Company Needs affect event selection weight
- Low Supply → more foraging/supply event options
- High stress → more rest-focused events

### Combat System

**Pre-Battle:**
- High Readiness (80+): +10% combat effectiveness
- Low Equipment (<30): Risk of broken gear in battle

**Post-Battle:**
- MoraleShock spikes (backend)
- Readiness may change based on outcome

**Note:** Rest depletion removed 2026-01-11 (metric no longer exists).

### Medical System

**Condition Triggers:**
- Low Fatigue (<5): Risk of collapse and injury

**Note:** Rest-based condition triggers removed 2026-01-11 (metric no longer tracked).

**Treatment Integration:**
- `dec_seek_treatment` uses Medical system
- Treatment restores HP and clears conditions
- Costs denars based on severity

---

## Configuration

### enlisted_config.json → camp_life Section

```json
"camp_life": {
  "enabled": true,
  "LogisticsHighThreshold": 70,
  "MoraleLowThreshold": 70,
  "PayTensionHighThreshold": 70,
  
  "QuartermasterPurchaseFine": 0.98,
  "QuartermasterPurchaseTense": 1.0,
  "QuartermasterPurchaseSour": 1.07,
  "QuartermasterPurchasePredatory": 1.15,
  
  "QuartermasterBuybackFine": 1.0,
  "QuartermasterBuybackTense": 0.95,
  "QuartermasterBuybackSour": 0.85,
  "QuartermasterBuybackPredatory": 0.75
}
```

**Disable CampLifeBehavior:**
Set `"enabled": false` to disable the backend simulation layer. Company Needs will still function, but Quartermaster pricing will be static.

### enlisted_config.json → decision_events.pacing

```json
"decision_events": {
  "pacing": {
    "event_window_min_days": 3,
    "event_window_max_days": 5,
    "max_per_day": 2,
    "max_per_week": 8,
    "min_hours_between": 6,
    "evaluation_hours": [8, 14, 20],
    "quiet_day_chance": 0.15,
    "per_category_cooldown_days": {
      "narrative": 1,
      "map_incident": 1
    }
  }
}
```

**Adjust Event Frequency:**
- Increase `max_per_day` for more events
- Decrease `event_window_min_days` for more frequent delivery
- Increase `quiet_day_chance` for more peaceful days

### strategic_context_config.json

Contains needs prediction templates for each strategic context. Used by `CompanyNeedsManager.PredictUpcomingNeeds()`.

**Example:**
```json
"strategic_contexts": {
  "Grand Campaign": {
    "needs_prediction": {
      "Readiness": 85,
      "Supplies": 80,
      "Equipment": 75,
      "Morale": 70,
      "Rest": 50
    }
  }
}
```

---

## Technical Implementation

### Core Classes

**Company Needs (Player-Facing):**
- `CompanyNeedsState.cs`: Data model storing current values (0-100)
- `CompanyNeedsManager.cs`: Static helpers for degradation, recovery, prediction
- `CompanyNeed.cs`: Enum defining need types (Readiness, Supply) - Rest removed 2026-01-11

**Access:** `EnlistmentBehavior.Instance.CompanyNeeds`

**CampLifeBehavior (Backend):**
- `CampLifeBehavior.cs`: Daily snapshot, mood calculation, event listeners
- Singleton: `CampLifeBehavior.Instance`

**Camp Menu:**
- `CampMenuHandler.cs`: Service Records, Retinue, Companions submenus
- `EnlistedMenuBehavior.cs`: Main Camp Hub, status displays

**Content Delivery:**
- `DecisionManager.cs`: Manages decision availability and cooldowns
- `DecisionCatalog.cs`: Loads and organizes all 37 decisions
- `EventPacingManager.cs`: Controls camp event firing
- `EventDeliveryManager.cs`: Handles event popups and effects

### Data Flow

**Company Needs Modification:**
```csharp
// Via Order outcome
state.SetNeed(CompanyNeed.Readiness, currentValue + delta);

// Via Decision effect
enlistment.CompanyNeeds.ModifyNeed(CompanyNeed.Readiness, +5);

// Via Daily degradation
CompanyNeedsManager.ProcessDailyDegradation(needs, army);
```

**CampLifeBehavior Update:**
```csharp
// Daily tick
OnDailyTick() {
  Calculate logistics strain
  Update morale shock (decay or spike)
  Read pay tension from EnlistmentBehavior
  Derive QuartermasterMoodTier
  Log snapshot
}
```

**Decision Execution:**
```csharp
// Player selects decision → effects processed
EventDeliveryManager.ApplyEffects(option.Effects);
EventDeliveryManager.ApplyRewards(option.Rewards);
SetDecisionCooldown(decision.Id, cooldownDays);
```

### Persistence

**Company Needs:**
- Serialized in `EnlistmentBehavior.SyncData()`
- Stored as three integer fields (Readiness, Supply, Rest)
- Validated on load (clamped 0-100)

**CampLifeBehavior:**
- Serialized in `CampLifeBehavior.SyncData()`
- Stores four float meters + mood tier
- Stores weekly counters and last update day

**Decision Cooldowns:**
- Stored in `EscalationState.EventLastFired` dictionary
- Key: decision ID, Value: CampaignTime last fired
- Shared with event cooldown system

### Performance

**Daily Tick Cost:**
- Company Needs degradation: ~5 arithmetic operations
- CampLifeBehavior snapshot: ~15 operations + 1 config read
- Critical need checks: ~5 comparisons per need
- Total: <100 operations per day, negligible performance impact

**Event Pacing:**
- Checks run 3 times per day (evaluation hours: 8, 14, 20)
- Early exit if pacing window not elapsed
- Minimal cost when no events fire

**Decision Filtering:**
- Runs only when player opens Camp Hub menu
- Filters ~37 decisions against requirements
- Results cached until next menu open
- No performance impact during normal gameplay
