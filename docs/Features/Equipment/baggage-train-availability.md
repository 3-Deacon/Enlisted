# Baggage Train Availability System

**Summary:** The Baggage Train system gates player access to their personal stowage based on realistic military logistics. The baggage train marches separately from the fighting column, with its accessibility dynamically responding to campaign conditions. World-state-aware simulation means baggage delays/raids occur more frequently during intense combat, while peaceful garrison duty grants reliable access. This transforms inventory management from a passive storage feature into an active gameplay element with strategic considerations.

**Key Features:**
- ✅ Smart menu routing: One "Access Baggage Train" button → Opens stash OR QM dialogue based on state
- ✅ Immersive conversation: Blocked access leads to negotiation with QM (archetype-specific)
- ✅ **World-state-aware simulation:** Event probabilities adapt to campaign situation (siege/retreat/peace)
- ✅ **Orchestrator integration:** ContentOrchestrator provides campaign context for baggage event probability calculation
- ✅ Dynamic events: Delays, raids, theft create gameplay drama
- ✅ **Priority-based news:** Urgent events persist longer (24-48h) and can't be pushed out by routine updates (6h)
- ✅ **Color-coded notifications:** Event severity determines visual impact (green/yellow/red/critical) AND display duration
- ✅ **Daily Brief integration:** Shows baggage status (raids, arrivals, delays, temporary access)
- ✅ **Event tracking:** Records raid/arrival timestamps for contextual Daily Brief messages
- ✅ Rank progression: Officers gain privileges (halt column, free emergency access)
- ✅ **Dynamic decision:** Baggage access appears in DECISIONS accordion only when accessible

**Status:** ✅ Complete (Phases 1-6 + Orchestrator Integration Implemented)  
**Last Updated:** 2026-01-01

**Completed Features:**
- **Phase 1-3:** BaggageTrainManager, access states, emergency access, cooldowns
- **Phase 4:** Full event suite (arrived, delayed, raided, theft)
- **Phase 5:** Rank-based privileges (T3+ emergency access, T5+ daily windows, T7+ full control)
- **Phase 6:** Daily Brief status line with raid/arrival tracking, severity-based news priorities
- **Orchestrator Integration:** World-state-aware probabilities (activity level, lord situation, war stance, terrain)
- **Dynamic Decision:** Baggage access moved from static menu to dynamic decision (appears only when accessible)

**Deferred to Future Releases:**
- Phase 7-8: Cross-system integration (leave, combat, siege, discharge edge cases)
- Phase 9: QM conversation enhancements, performance optimization  
**Related Docs:** [Quartermaster System](quartermaster-system.md), [Company Supply Simulation](company-supply-simulation.md), [Provisions & Rations](provisions-rations-system.md), [Storylet Backbone](../Content/storylet-backbone.md)

---

## Index

1. [Overview](#overview)
2. [Design Philosophy](#design-philosophy)
3. [Access States](#access-states)
4. [Access Conditions](#access-conditions)
5. [World-State-Aware Simulation](#world-state-aware-simulation)
6. [Emergency Access System](#emergency-access-system)
7. [Baggage Train Events](#baggage-train-events)
8. [Rank-Based Access](#rank-based-access)
9. [Dynamic Decision System](#dynamic-decision-system)
10. [Integration Points](#integration-points)
11. [Data Structures](#data-structures)
12. [Configuration](#configuration)
13. [Implementation Plan](#implementation-plan)
14. [Testing Checklist](#testing-checklist)

---

## Overview

### Current State (Before This Feature)

The mod already has a baggage stash system (`_baggageStash` in `EnlistmentBehavior`):
- Dedicated `ItemRoster` for player's stored items
- Cross-faction transfer mechanics (courier system)
- Fatigue gating (can't access when exhausted)
- Accessed via `InventoryScreenHelper.OpenScreenAsStash()`

**Problem:** The baggage stash is always accessible via the Camp Hub menu whenever the player isn't fatigued. This doesn't reflect realistic military logistics and misses gameplay opportunities.

### Important Distinction: Access System

**Baggage Access System (this document):**
- Controls **when** the player can access their stored belongings
- Based on logistics: Is the baggage train caught up? Are we in settlement?
- All soldiers get FullAccess during muster
- Does not affect baggage access state

Both systems grant access during muster, but serve different purposes: one is logistics-based (availability), the other is security-based (compliance).

### Proposed State (After This Feature)

Access to the baggage train becomes conditional based on:
- **Location:** In settlement vs. on the march
- **Activity:** Resting vs. actively moving
- **Timing:** Muster days, periodic "caught up" windows
- **Rank:** Higher ranks gain more flexible access
- **Context:** Strategic situation (siege, pursuit, garrison)

---

## Design Philosophy

### Core Concept: "The Baggage Train Marches Separate"

In medieval armies, the baggage train (supply wagons, personal belongings, camp followers) marched separately from the fighting column, typically at the rear. Soldiers couldn't just walk back mid-march to grab spare equipment. This creates:

1. **Preparation Pressure:** Pack what you need before leaving settlement
2. **Access Windows:** Plan activities around when baggage is accessible
3. **Risk/Reward:** Store valuables vs. keep them on person
4. **Narrative Immersion:** Feel like part of a real military column

### Gameplay Goals

| Goal | Implementation |
|------|----------------|
| **Strategic Preparation** | Can't grab gear mid-battle; must prepare beforehand |
| **Natural Pacing** | Access during rest periods feels earned |
| **Rank Progression** | Officers have privileges lowly soldiers don't |
| **Event Hooks** | Baggage delays, raids, and theft create drama |
| **No Frustration** | Emergency access exists with reputation cost |

---

## Access States

### BaggageAccessState Enum

```csharp
public enum BaggageAccessState
{
    /// <summary>Full, unrestricted access to baggage stash.</summary>
    FullAccess,
    
    /// <summary>Brief access window (baggage caught up). Auto-expires.</summary>
    TemporaryAccess,
    
    /// <summary>No access - baggage train is behind the column.</summary>
    NoAccess,
    
    /// <summary>Locked by QM - supply crisis lockdown.</summary>
    Locked
}
```

### State Transitions

```
┌─────────────────┐
│   FullAccess    │ ← In settlement, during muster, army encamped
│   (unlimited)   │
└────────┬────────┘
         │ Leave settlement / army begins march
         ▼
┌─────────────────┐
│    NoAccess     │ ← On the march, in combat, forced march
│ (baggage behind)│
└────────┬────────┘
         │ Baggage "catches up" event (periodic)
         ▼
┌─────────────────┐
│ TemporaryAccess │ ← Brief window before march resumes
│   (expires)     │
└────────┬────────┘
         │ Window expires OR army moves
         ▼
┌─────────────────┐
│    NoAccess     │ ← Back to march state
└─────────────────┘

Special override:
┌─────────────────┐
│     Locked      │ ← Supply < 20%
│ (QM authority)  │
└─────────────────┘
```

---

## Access Conditions

### When Baggage IS Accessible (FullAccess)

| Condition | Detection Method | Notes |
|-----------|------------------|-------|
| **In Settlement** | `party.CurrentSettlement != null` | Town, castle, or village |
| **At Muster** | `_payMusterPending == true` or within 6 hours after | Natural logistics pause |
| **Army Encamped** | `!party.IsMoving && party.Army != null` | Army halted for rest |
| **During Rest Activity** | Player in rest menu | Explicitly resting |
| **Garrison Duty Context** | `GetLordStrategicContext() == "garrison_duty"` | Stationary defense |

### When Baggage is NOT Accessible (NoAccess)

| Condition | Detection Method | Notes |
|-----------|------------------|-------|
| **On the March** | `party.IsMoving && party.CurrentSettlement == null` | Moving on campaign map |
| **Active Battle** | `party.MapEvent != null` | In combat encounter |
| **Forced March/Pursuit** | Party speed > 1.5× base OR chasing target | High-speed movement |
| **Active Siege** | `party.BesiegerCamp != null` or `party.SiegeEvent != null` | Focus on siege operations |
| **Recent Battle** | Within 2 hours of `MapEventEnded` | Reorganizing after combat |

### When Baggage is Locked (Locked)

| Condition | Detection Method | Notes |
|-----------|------------------|-------|
| **Critical Supply** | `CompanyNeeds.Supplies < 20` | QM has locked down all storage |
| **Lord's Orders** | Future: Order flag blocking access | Mission requires travel light |

### Temporary Access Windows

Periodic "baggage caught up" events create brief access windows on campaign:

| Trigger | Duration | Cooldown |
|---------|----------|----------|
| **Automatic (daily check)** | 4 game hours | 18-30 hours |
| **After Skirmish** | 2 game hours | 8 hours |
| **Night Halt** | Until dawn or movement | Once per night |
| **Rank T7+ Request** | 2 game hours | 12 hours |

---

## World-State-Aware Simulation

### Overview

The baggage train simulation is fully integrated with the Content Orchestrator's world-state analysis system. Instead of using fixed probabilities for baggage events (delays, raids, arrivals), the system calculates contextual probabilities based on current campaign conditions.

**Core Concept:** Baggage logistics are harder during intense combat and easier during peaceful garrison duty.

### ContentOrchestrator Integration

Every day at 6am, the ContentOrchestrator analyzes the current world situation and provides it to all simulation systems, including BaggageTrainManager:

```csharp
// In ContentOrchestrator.cs
private void RefreshBaggageSimulation(WorldSituation worldSituation)
{
    var baggageManager = Logistics.BaggageTrainManager.Instance;
    if (baggageManager == null) return;
    
    // Probabilities calculated based on world state
    var probs = baggageManager.CalculateEventProbabilities(worldSituation);
    
    // BaggageTrainManager uses these when checking for events
}
```

### Contextual Probability System

The `BaggageTrainManager.CalculateEventProbabilities()` method analyzes four key dimensions:

#### 1. Activity Level (ExpectedActivity)

| Activity Level | CaughtUp% | Delay% | Raid% | Reasoning |
|----------------|-----------|--------|-------|-----------|
| **Intense** | 10% | 35% | 20% | Desperate retreat/siege - wagons fall behind |
| **Active** | 20% | 20% | 12% | Active campaign - moderate pressure |
| **Routine** | 25% | 15% | 8% | Normal march - config baseline values |
| **Quiet** | 40% | 5% | 2% | Garrison/peacetime - wagons keep up easily |

#### 2. Lord Situation (LordIs)

| Situation | Modifiers | Reasoning |
|-----------|-----------|-----------|
| **Defeated** | Delay +20%, Raid +15% | Routed forces, baggage scattered |
| **Siege** | CatchUp +10%, Raid +5% | Stationary but vulnerable |
| **Peacetime Garrison** | Delay -50%, Raid -70% | Safe and well-managed |

#### 3. War Stance (KingdomStance)

| Stance | Modifier | Reasoning |
|--------|----------|-----------|
| **Desperate** | Raid +12% | Losing badly, enemy raids frequent |
| **Defensive** | Raid +6% | Under pressure, increased raids |
| **Offensive** | Raid -3% | Attacking, wagons in friendly territory |
| **Peace** | Raid 1% | Peacetime, minimal raids |

#### 4. Terrain (Physical Environment)

| Terrain Type | Modifier | Reasoning |
|--------------|----------|-----------|
| **Mountain** | Delay +10% | Rough terrain slows wagons |
| **Snow** | Delay +15% | Snow significantly slows wagons |
| **Desert** | Delay +8% | Sand is difficult for wheels |
| **Fording/Water** | Delay +12% | River crossings are slow |

### Example Scenarios

**Intense Siege (Player Army Attacking):**
```
Base: Intense (10/35/20)
+ Siege: +10% CatchUp, +5% Raid
+ Defensive War: +6% Raid
+ Mountain Terrain: +10% Delay
= Final: 20% CatchUp, 45% Delay, 31% Raid
Result: Wagons rarely accessible, frequently delayed/raided
```

**Peaceful Garrison (Player Lord at Castle):**
```
Base: Quiet (40/5/2)
+ Garrison Situation: -50% Delay, -70% Raid
+ Peace Stance: Raid = 1%
= Final: 40% CatchUp, 2.5% Delay, 1% Raid
Result: Wagons frequently accessible, rarely delayed/raided
```

**Routine Campaign (Normal March):**
```
Base: Routine (25/15/8)
+ Desert Terrain: +8% Delay
= Final: 25% CatchUp, 23% Delay, 8% Raid
Result: Balanced logistics simulation
```

### Configuration-Driven Baseline

The "Routine" activity level uses configuration values from `baggage_config.json`:

```json
{
  "timing": {
    "caught_up_chance_percent": 25
  },
  "events": {
    "delay_event_chance_bad_weather": 15,
    "raid_event_chance_enemy_territory": 8
  }
}
```

All other activity levels apply modifiers to these baseline values. This allows tuning without changing code.

### Benefits

**Gameplay:**
- Baggage simulation feels connected to campaign situation
- Players experience logistical pressure during intense combat
- Peaceful periods provide reliable equipment access
- Adds strategic dimension to equipment management

**Technical:**
- Single source of truth for world state (ContentOrchestrator)
- Probabilities calculated once per day (efficient)
- Config-driven baselines allow easy tuning
- No hardcoded values in probability calculation

---

## Emergency Access System

### Two Access Points

The baggage train system provides two distinct ways to interact with baggage:

```
┌─────────────────────────────────────────────────────────┐
│             CAMP HUB MENU (EnlistedMenuBehavior)        │
│                                                          │
│  [Access Baggage Train] ← ONE option, smart routing     │
└─────────────────────────────────────────────────────────┘
                            │
                            │ Player clicks
                            ▼
                ┌──────────────────────────┐
                │ Check access state       │
                └──────────────────────────┘
                            │
        ┌───────────────────┴───────────────────┐
        │                                       │
        ▼                                       ▼
┌──────────────────────┐            ┌─────────────────────────┐
│ State: FullAccess    │            │ State: NoAccess/Locked  │
│                      │            │                         │
│ → Open stash screen  │            │ → Open QM dialogue      │
│   directly           │            │   at emergency request  │
│                      │            │   node                  │
│ (Native inventory UI)│            │                         │
└──────────────────────┘            └─────────────────────────┘
        │                                       │
        │                                       ▼
        │                          ┌──────────────────────────┐
        │                          │ QM Dialogue:             │
        │                          │ qm_baggage_request_      │
        │                          │ response                 │
        │                          │                          │
        │                          │ • 6 archetype variants   │
        │                          │ • Rep-scaled costs       │
        │                          │ • High-rep favor option  │
        │                          │ • T7+ halt column        │
        │                          │ • Can refuse (no penalty)│
        │                          └──────────────────────────┘
        │                                       │
        └───────────────────┬───────────────────┘
                            ▼
                ┌──────────────────────────┐
                │ BaggageTrainManager      │
                │                          │
                │ • Grants access if       │
                │   approved               │
                │ • Applies rep costs      │
                │ • Logs changes           │
                └──────────────────────────┘
```

**Tooltip Behavior:**
- **FullAccess**: "Access your stored belongings"
- **NoAccess**: "Request baggage access (wagons behind the column)"
- **Locked**: "Request baggage access (storage locked down)"
- **Delayed**: "Request baggage access (wagons stuck {DAYS} days)"

### Begging the Quartermaster

When access is blocked (NoAccess/Locked), clicking "Access Baggage Train" routes to QM dialogue:

```
Player clicks "Access Baggage Train" → State is NoAccess → Routes to QM

QM: "The wagons are a quarter-mile back. We're not stopping 
     the whole column because you forgot your spare bowstrings."

Options:
  → "It's urgent. I'll make it quick." 
      [-5 QM Rep, grants TemporaryAccess (2 hours)]
      
  → "You're right. Forget it." 
      [No penalty, no access]
      
  → [QM Rep 50+] "Come on, you know I'm good for it." 
      [No penalty, grants TemporaryAccess (2 hours)]
      
  → [T7+ Commander] "Have the wagons brought up."
      [-3 Officer Rep (inconvenience), grants FullAccess until movement]
```

### Emergency Access Limits

- Can only request emergency access once per 12-hour period
- Each request (except high rep/rank versions) costs -5 QM Rep
- Repeated requests in short period: additional -2 Soldier Rep (seen as needy)

---

## Baggage Train Events

### Event Category: Baggage Train (`events_baggage.json`)

#### Baggage Caught Up (Positive)

| Event ID | Trigger | Description |
|----------|---------|-------------|
| `evt_baggage_arrived` | Daily tick when on march, ~25% chance | Wagons catch up during a halt |

```json
{
  "id": "evt_baggage_arrived",
  "severity": "positive",
  "titleId": "evt_baggage_arrived_title",
  "title": "Baggage Train Arrives",
  "setupId": "evt_baggage_arrived_setup",
  "setup": "The sergeant calls a brief halt. The baggage wagons have caught up to the column, their ox-drivers looking exhausted but relieved.",
  "category": "logistics",
  "context": ["march", "campaign"],
  "tier_min": 1,
  "options": [
    {
      "id": "access_baggage",
      "textId": "evt_baggage_arrived_opt_access",
      "text": "Good. I need to get something from my kit.",
      "effects": { "grant_temporary_baggage_access": 4 }
    },
    {
      "id": "no_need",
      "textId": "evt_baggage_arrived_opt_skip",
      "text": "I'm fine. Let them rest.",
      "effects": { "soldier_rep": 2 }
    }
  ]
}
```

#### Baggage Delayed (Negative)

| Event ID | Trigger | Description |
|----------|---------|-------------|
| `evt_baggage_delayed` | Bad weather, mountains, enemy territory | Wagons fall behind |
| `evt_baggage_raided` | Enemy territory + low scout coverage | Raiders hit supply train |
| `evt_baggage_stuck` | River crossing, rough terrain | Physical obstacle |

```json
{
  "id": "evt_baggage_delayed",
  "severity": "attention",
  "titleId": "evt_baggage_delayed_title", 
  "title": "Baggage Train Delayed",
  "setupId": "evt_baggage_delayed_setup",
  "setup": "Word comes down the column: the baggage wagons are stuck in the mud two leagues back. It'll be a day or more before they catch up.",
  "category": "logistics",
  "context": ["march", "rain", "mountains"],
  "tier_min": 1,
  "options": [
    {
      "id": "wait",
      "textId": "evt_baggage_delayed_opt_wait",
      "text": "Nothing to be done. We press on.",
      "effects": { "baggage_delay_days": 1 }
    },
    {
      "id": "volunteer_help",
      "textId": "evt_baggage_delayed_opt_help",
      "text": "I'll go back and help dig them out.",
      "tooltip": "Costs 15 Fatigue, may reduce delay",
      "requirements": { "fatigue_min": 20 },
      "effects": { 
        "fatigue": -15,
        "baggage_delay_days": -1,
        "qm_rep": 5,
        "athletics_xp": 20
      }
    }
  ]
}
```

#### Baggage Raided (Combat/Loss)

```json
{
  "id": "evt_baggage_raided",
  "severity": "urgent",
  "titleId": "evt_baggage_raided_title",
  "title": "Raiders Hit the Baggage Train",
  "setupId": "evt_baggage_raided_setup",
  "setup": "Shouts from the rear of the column. Enemy raiders are attacking the supply wagons!",
  "category": "combat",
  "context": ["enemy_territory", "war"],
  "tier_min": 2,
  "options": [
    {
      "id": "rush_to_defend",
      "textId": "evt_baggage_raided_opt_defend",
      "text": "Rush to defend the wagons!",
      "tooltip": "Combat encounter. Risk injury. Save your belongings.",
      "effects": { "trigger_baggage_defense_combat": true }
    },
    {
      "id": "stay_formation",
      "textId": "evt_baggage_raided_opt_stay",
      "text": "Stay in formation. The rearguard will handle it.",
      "tooltip": "May lose some personal items, but no personal risk.",
      "effects": { 
        "random_baggage_loss": 2,
        "officer_rep": 2
      }
    },
    {
      "id": "order_counterattack",
      "textId": "evt_baggage_raided_opt_order",
      "text": "[T7+ Commander] Order a counterattack!",
      "requirements": { "tier_min": 7 },
      "tooltip": "Use your authority. Full defense, risk to soldiers.",
      "effects": { 
        "trigger_baggage_defense_combat": true,
        "officer_rep": -3,
        "leadership_xp": 30
      }
    }
  ]
}
```

#### Theft from Baggage (Social)

```json
{
  "id": "evt_baggage_theft",
  "severity": "urgent",
  "titleId": "evt_baggage_theft_title",
  "title": "Missing from Your Kit",
  "setupId": "evt_baggage_theft_setup",
  "setup": "When you finally get access to your stowed belongings, something's missing. That {ITEM_NAME} you stored - gone. Someone's been through your things.",
  "category": "social",
  "context": ["camp"],
  "tier_min": 1,
  "requirements": { "soldier_rep_max": 40, "baggage_has_items": true },
  "options": [
    {
      "id": "let_it_go",
      "textId": "evt_baggage_theft_opt_ignore",
      "text": "Could have been anyone. Let it go.",
      "effects": { "random_baggage_loss": 1 }
    },
    {
      "id": "investigate",
      "textId": "evt_baggage_theft_opt_investigate",
      "text": "Ask around. Someone saw something.",
      "tooltip": "Charm check to identify thief",
      "effects": { 
        "skill_check": { "skill": "Charm", "threshold": 40 },
        "success": { "recover_item": true, "soldier_rep": -5 },
        "failure": { "random_baggage_loss": 1, "soldier_rep": -3 }
      }
    },
    {
      "id": "report_nco",
      "textId": "evt_baggage_theft_opt_report",
      "text": "Report it to the sergeant.",
      "tooltip": "Official channels. Might make enemies.",
      "effects": { 
        "random_baggage_loss": 1,
        "officer_rep": 3,
        "soldier_rep": -8
      }
    }
  ]
}
```

---

## Rank-Based Access

### Access Privileges by Tier

| Tier | Rank | Access Privileges |
|------|------|-------------------|
| **T1-T2** | Recruit/Soldier | Muster + Settlement only. No emergency access. |
| **T3-T4** | Veteran/Corporal | Above + Camp rest. Emergency access available (costs rep). |
| **T5-T6** | NCO | Above + Daily "caught up" window. Reduced emergency cost. |
| **T7-T9** | Officer/Commander | Above + Can request column halt (1/day). Free emergency access. |

### Tier-Gated Features

```csharp
public bool CanRequestEmergencyAccess()
{
    // T1-T2: Cannot request emergency access at all
    if (_enlistmentTier < 3) return false;
    
    // T3+: Can request (with varying costs)
    return true;
}

public int GetEmergencyAccessRepCost()
{
    if (_enlistmentTier >= 7) return 0;      // Commander: free
    if (_enlistmentTier >= 5) return 2;      // NCO: minimal cost
    return 5;                                  // Enlisted: full cost
}

public bool CanHaltColumn()
{
    // Only T7+ commanders can order the column to halt
    return _enlistmentTier >= 7;
}
```

---

## Dynamic Decision System

### Overview

Baggage access appears as a decision button in the DECISIONS accordion. When clicked, it **bypasses the popup** and routes directly to either the baggage stash or the QM dialogue based on current access state. This provides seamless access without unnecessary confirmation popups.

### Implementation

**Decision Definition:** `ModuleData/Enlisted/Decisions/camp_decisions.json`

```json
{
  "id": "dec_baggage_access",
  "category": "decision",
  "titleId": "dec_baggage_access_title",
  "title": "Access Baggage Train",
  "setupId": "dec_baggage_access_setup",
  "setup": "Your personal belongings are stowed with the baggage train.",
  "requirements": {
    "tier": { "min": 1, "max": 999 }
  },
  "timing": { "cooldown_days": 0, "priority": "normal" },
  "options": []
}
```

**Note:** The `options` array is empty because this decision never shows a popup. It's intercepted for direct routing.

### Routing Logic

**Direct Bypass (EnlistedMenuBehavior.OnDecisionSelected):**

```csharp
// Special handling: Route baggage access directly to QM dialogue (bypass popup)
if (decision.Id.Equals("dec_baggage_access", StringComparison.OrdinalIgnoreCase))
{
    OnBaggageTrainSelected(null);
    return;
}
```

**Smart Routing (EnlistedMenuBehavior.OnBaggageTrainSelected):**

```csharp
var accessState = baggageManager.GetCurrentAccess();

switch (accessState)
{
    case BaggageAccessState.FullAccess:
    case BaggageAccessState.TemporaryAccess:
        // Direct access to baggage stash
        enlistment.TryOpenBaggageTrain();
        break;

    case BaggageAccessState.Locked:
        // Route to QM conversation for emergency access request
        OpenQuartermasterConversationForBaggageRequest("locked");
        break;

    case BaggageAccessState.NoAccess:
    default:
        // Route to QM conversation for emergency access request
        OpenQuartermasterConversationForBaggageRequest("emergency");
        break;
}
```

### Benefits

**UX Improvements:**
- No confirmation popup - immediate action
- Button always visible while enlisted
- Smart routing based on access state
- Clear indication that baggage is accessible
- No greyed-out options with unclear reasons

**Design Consistency:**
- Follows decision system patterns
- Uses same requirement/availability infrastructure
- Integrates with cooldown system (prevents spam)
- Supports same JSON/XML localization approach

**Implementation Simplicity:**
- Leverages existing decision visibility system
- No special menu code needed
- One custom gate check in DecisionManager
- One effect handler in EventDeliveryManager

---

## Integration Points

### Existing Systems to Modify

#### 1. EnlistmentBehavior.cs

| Location | Change |
|----------|--------|
| `_baggageStash` field | Add `_lastBaggageAccessTime`, `_baggageDelayUntil`, `_temporaryAccessExpires` |
| `TryOpenBaggageTrain()` | Add access state check before `OpenScreenAsStash()` |
| `OnHourlyTick()` | Add baggage access state updates |
| `OnDailyTick()` | Add baggage "caught up" event check |
| `SyncData()` | Persist new baggage state fields |

#### 2. EnlistedMenuBehavior.cs

| Location | Change |
|----------|--------|
| Camp Hub menu | Modify baggage option condition to check `BaggageTrainManager.GetAccessState()` |
| Tooltip text | Show WHY access is blocked ("Baggage train is behind the column") |
| New option | Add "Request Baggage Access" when NoAccess state |

#### 3. ArmyContextAnalyzer.cs

| Location | Change |
|----------|--------|
| `GetLordStrategicContext()` | Already returns contexts we need |
| New method | `IsArmyMoving()`, `IsInForcedMarch()` for access detection |

#### 4. CompanySupplyManager.cs

| Location | Change |
|----------|--------|
| Add method | `ShouldLockBaggage()` - returns true when supply < 20% |

#### 5. QuartermasterManager.cs

| Location | Change |
|----------|--------|
| Conversation | Add emergency access request dialogue branch |

### New Files to Create

| File | Purpose |
|------|---------|
| `src/Features/Logistics/BaggageTrainManager.cs` | Core manager for access state, events, timing |
| `src/Features/Logistics/BaggageAccessState.cs` | Enum definition |
| `ModuleData/Enlisted/Events/events_baggage.json` | Baggage train events |
| `ModuleData/Enlisted/baggage_config.json` | Timing, costs, thresholds |
| `ModuleData/Languages/enlisted_baggage_strings.xml` | Localization |

---

## Technical Implementation Details

### Blueprint Compliance

This implementation follows all Blueprint technical standards:

**Logging:**
- Category: `"Baggage"` (add to categories list in Blueprint)
- Use `ModLogger.Info/Debug/Warn/Error` for all state changes and errors
- Performance-friendly: Debug logs only for detailed diagnostics
- Output location: `<BannerlordInstall>\Modules\Enlisted\Debugging\`

**Safety:**
- Use `CampaignSafetyGuard.SafeMainHero` for hero access
- Null checks on all external dependencies
- Graceful degradation when enlisted party not available

**Code Quality:**
- Follow ReSharper recommendations
- Natural, factual comments (not changelog-style)
- Clear, descriptive method names
- Proper exception handling

**Patterns:**
- Singleton pattern via `Instance` property
- `CampaignBehaviorBase` with `RegisterEvents()` and `SyncData()`
- Configuration via JSON loaded through `ConfigurationManager`

---

## Data Structures

### BaggageTrainManager State

```csharp
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Logistics
{
    /// <summary>
    /// Manages baggage train access based on march state, rank, location, and supply conditions.
    /// Controls when players can access their personal stowage and handles emergency access requests.
    /// Coordinates with QuartermasterManager for dialogue-based access gating.
    /// </summary>
    public class BaggageTrainManager : CampaignBehaviorBase
    {
        private const string LogCategory = "Baggage";
        
        public static BaggageTrainManager Instance { get; private set; }
        
        // Core state - persisted in save
        private BaggageAccessState _currentState = BaggageAccessState.FullAccess;
        private CampaignTime _temporaryAccessExpires = CampaignTime.Zero;
        private CampaignTime _baggageDelayedUntil = CampaignTime.Zero;
        private CampaignTime _lastEmergencyRequest = CampaignTime.Zero;
        private int _emergencyRequestsToday = 0;
        
        // Tracking - transient, resets daily
        private bool _baggageCaughtUpEventFiredToday = false;
        private CampaignTime _lastStateChangeTime = CampaignTime.Zero;
        
        public BaggageTrainManager()
        {
            Instance = this;
        }
        
        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }
        
        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_baggageCurrentState", ref _currentState);
            dataStore.SyncData("_baggageTemporaryAccessExpires", ref _temporaryAccessExpires);
            dataStore.SyncData("_baggageDelayedUntil", ref _baggageDelayedUntil);
            dataStore.SyncData("_baggageLastEmergencyRequest", ref _lastEmergencyRequest);
            dataStore.SyncData("_baggageEmergencyRequestsToday", ref _emergencyRequestsToday);
            dataStore.SyncData("_baggageLastStateChangeTime", ref _lastStateChangeTime);
        }
        
        // Public API
        
        /// <summary>
        /// Returns the current baggage access state, evaluating all conditions
        /// (location, activity, supply level, delays, temporary windows).
        /// </summary>
        public BaggageAccessState GetCurrentAccess()
        {
            var hero = CampaignSafetyGuard.SafeMainHero;
            if (hero == null)
            {
                ModLogger.Warn(LogCategory, "GetCurrentAccess: Hero null");
                return BaggageAccessState.NoAccess;
            }
            
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                ModLogger.Debug(LogCategory, "GetCurrentAccess: Not enlisted");
                return BaggageAccessState.FullAccess; // Not enlisted = full access
            }
            
            // ... full implementation
            ModLogger.Debug(LogCategory, $"GetCurrentAccess: {_currentState}");
            return _currentState;
        }
        
        /// <summary>
        /// Attempts to grant emergency baggage access via quartermaster request.
        /// Checks cooldowns, rank requirements, and applies reputation costs.
        /// Returns false if request denied, with failReason explaining why.
        /// </summary>
        public bool TryRequestEmergencyAccess(out string failReason)
        {
            // Null safety
            var hero = CampaignSafetyGuard.SafeMainHero;
            if (hero == null)
            {
                failReason = "Invalid state";
                ModLogger.Expected(LogCategory, "baggage-emergency-hero-null", "TryRequestEmergencyAccess: Hero null");
                return false;
            }
            
            // ... full implementation with logging at each decision point
            ModLogger.Info(LogCategory, $"Emergency access granted: {hours}h window");
            return true;
        }
        
        /// <summary>
        /// Grants temporary baggage access for specified hours.
        /// Typically triggered by events (baggage caught up, night halt).
        /// </summary>
        public void GrantTemporaryAccess(int hours)
        {
            _temporaryAccessExpires = CampaignTime.HoursFromNow(hours);
            _currentState = BaggageAccessState.TemporaryAccess;
            ModLogger.Info(LogCategory, $"Temporary access granted: {hours}h");
        }
        
        /// <summary>
        /// Applies a baggage delay, preventing access until the delay clears.
        /// Used by events (bad weather, rough terrain, raids).
        /// </summary>
        public void ApplyBaggageDelay(int days)
        {
            _baggageDelayedUntil = CampaignTime.DaysFromNow(days);
            ModLogger.Info(LogCategory, $"Baggage delayed: {days} days");
        }
        
        private void OnHourlyTick()
        {
            // Check for state transitions (temporary access expiring, delays clearing)
            // Fire "baggage caught up" event if eligible
        }
        
        private void OnDailyTick()
        {
            // Reset daily cooldowns
            _emergencyRequestsToday = 0;
            _baggageCaughtUpEventFiredToday = false;
        }
    }
}
```

### Configuration Schema (baggage_config.json)

```json
{
  "access_windows": {
    "temporary_access_hours": 4,
    "night_halt_grants_access": true,
    "muster_grants_access": true,
    "settlement_always_access": true
  },
  "timing": {
    "caught_up_check_hours": 24,
    "caught_up_chance_percent": 25,
    "min_cooldown_hours": 18,
    "max_cooldown_hours": 30
  },
  "emergency_access": {
    "base_qm_rep_cost": 5,
    "nco_qm_rep_cost": 2,
    "officer_qm_rep_cost": 0,
    "cooldown_hours": 12,
    "high_rep_threshold": 50,
    "spam_penalty_soldier_rep": 2
  },
  "rank_gates": {
    "emergency_request_min_tier": 3,
    "column_halt_min_tier": 7,
    "daily_access_window_min_tier": 5
  },
  "lockdown": {
    "supply_threshold_percent": 20,
  },
  "events": {
    "delay_event_chance_bad_weather": 15,
    "delay_event_chance_mountains": 10,
    "raid_event_chance_enemy_territory": 8,
    "theft_event_chance_low_rep": 5
  }
}
```

### API Verification (Blueprint Requirement)

**ALL APIs must be verified against:** `C:\Dev\Enlisted\Decompile\`  
**Target Version:** Bannerlord v1.3.13

**APIs Used:**

| API | Assembly | Location | Verified |
|-----|----------|----------|----------|
| `CampaignBehaviorBase` | TaleWorlds.CampaignSystem | Campaign.CampaignBehaviors | ✅ |
| `CampaignEvents.HourlyTickEvent` | TaleWorlds.CampaignSystem | CampaignEvents | ✅ |
| `CampaignEvents.DailyTickEvent` | TaleWorlds.CampaignSystem | CampaignEvents | ✅ |
| `CampaignTime.HoursFromNow()` | TaleWorlds.CampaignSystem | CampaignTime | ✅ |
| `CampaignTime.DaysFromNow()` | TaleWorlds.CampaignSystem | CampaignTime | ✅ |
| `IDataStore.SyncData()` | TaleWorlds.SaveSystem | IDataStore | ✅ |
| `MobileParty.IsActive` | TaleWorlds.CampaignSystem | Party.MobileParty | ✅ |
| `Settlement.IsUnderSiege` | TaleWorlds.CampaignSystem | Settlements.Settlement | ✅ |
| `Hero.PartyBelongedTo` | TaleWorlds.CampaignSystem | Hero | ✅ |

**Verification Notes:**
- `CampaignTime.Zero` is a static property, not a constant
- `IDataStore.SyncData<T>()` requires ref parameter for primitive types
- Use `AddNonSerializedListener()` for campaign events (not `AddListener()`)

---

### Error Handling & Logging Pattern

**Every public method follows this pattern:**

```csharp
public BaggageAccessState GetCurrentAccess()
{
    // 1. Null safety check with logging
    var hero = CampaignSafetyGuard.SafeMainHero;
    if (hero == null)
    {
        ModLogger.Warn(LogCategory, "GetCurrentAccess: Hero null");
        return BaggageAccessState.NoAccess; // Graceful fallback
    }
    
    // 2. Dependency check
    var enlistment = EnlistmentBehavior.Instance;
    if (enlistment == null || !enlistment.IsEnlisted)
    {
        ModLogger.Debug(LogCategory, "GetCurrentAccess: Not enlisted");
        return BaggageAccessState.FullAccess;
    }
    
    // 3. Business logic with decision logging
    if (IsInSettlement())
    {
        ModLogger.Debug(LogCategory, "GetCurrentAccess: FullAccess (in settlement)");
        return BaggageAccessState.FullAccess;
    }
    
    // 4. State changes logged at Info level
    if (_currentState != previousState)
    {
        ModLogger.Info(LogCategory, $"Access state changed: {previousState} → {_currentState}");
    }
    
    return _currentState;
}
```

**Logging Levels:**
- **Error:** Null dependencies, invalid state, critical failures
- **Warn:** Unexpected conditions, denied requests with valid state
- **Info:** State changes, access grants, event triggers, config load
- **Debug:** Condition checks, access queries, routine evaluations

**Comment Style (Blueprint compliant):**
```csharp
// ✅ GOOD: Factual, current behavior
// Checks if baggage wagons are within reasonable distance of the column.

// ❌ BAD: Changelog-style, mentions phases
// Phase 2: Added distance check. Previously only checked location type.
```

---

## Implementation Plan

### Phase 1: Core Infrastructure ✅ COMPLETE

**Status:** Completed 2025-12-24  
**Goal:** Create the manager class and access state system.

#### Tasks

1. **Create `BaggageAccessState.cs`** (enum)
   - Define 4 states: FullAccess, TemporaryAccess, NoAccess, Locked
   - Place in `src/Features/Logistics/`

2. **Create `BaggageTrainManager.cs`**
   - **Blueprint Compliance:**
     - Use `CampaignSafetyGuard.SafeMainHero` for all hero access
     - Log with `ModLogger` using category `"Baggage"`
     - Follow singleton pattern (`Instance` property)
     - Implement `RegisterEvents()` (HourlyTick, DailyTick)
     - Implement `SyncData()` for save/load persistence
     - Add null checks on all external dependencies
     - Use natural, factual comments (not changelog-style)
   - **Core Methods:**
     - `GetCurrentAccess()` - evaluates all conditions with logging
     - `TryRequestEmergencyAccess(out string failReason)` - with error messages
     - `GrantTemporaryAccess(int hours)` - logs state change
     - `ApplyBaggageDelay(int days)` - logs delay applied
     - `OnHourlyTick()` - check for state transitions
     - `OnDailyTick()` - reset daily cooldowns

3. **Add to EnlistmentBehavior.cs**
   - Instantiate manager on enlistment
   - Persist state in `SyncData()` (delegate to BaggageTrainManager)
   - No tick handlers needed (manager self-registers)

4. **Create baggage_config.json**
   - Place in `ModuleData/Enlisted/`
   - All timing and threshold values (see schema above)
   - Load via `ConfigurationManager.LoadConfig<BaggageConfig>()`

5. **Add to Enlisted.csproj** (old-style .csproj - REQUIRED)
   - Open `Enlisted.csproj` in text editor
   - Find `<ItemGroup>` containing other `<Compile Include=.../>` entries (around line 142)
   - Add manually:
     ```xml
     <Compile Include="src\Features\Logistics\BaggageAccessState.cs" />
     <Compile Include="src\Features\Logistics\BaggageTrainManager.cs" />
     ```
   - **Critical:** Files NOT added to .csproj will NOT compile (old-style project)

6. **Verify API against decompile**
   - Check `C:\Dev\Enlisted\Decompile\` for:
     - `CampaignTime.HoursFromNow()` / `DaysFromNow()`
     - `CampaignEvents.HourlyTickEvent` / `DailyTickEvent`
     - `IDataStore.SyncData()` method signatures

#### Acceptance Criteria
- [x] `BaggageTrainManager.GetCurrentAccess()` returns correct state based on conditions
- [x] State persists across save/load (SyncData implemented)
- [x] Config values load correctly from JSON
- [x] All state changes logged at Info level
- [x] No ReSharper warnings
- [x] Uses `CampaignSafetyGuard.SafeMainHero` for hero access
- [x] Null checks on all external dependencies

#### Implementation Notes
- Created `BaggageAccessState.cs` enum with 4 states (FullAccess, TemporaryAccess, NoAccess, Locked)
- Created `BaggageTrainManager.cs` with full state management and configuration loading
- Added `baggage_config.json` with timing, costs, and event probability settings
- Manager registered in `SubModule.cs` and added to `Enlisted.csproj`
- Build: 0 warnings, 0 errors

---

### Phase 2: Menu Integration ✅ COMPLETE

**Status:** Completed 2025-12-24  
**Goal:** Modify Camp Hub to respect baggage access state.

#### Tasks

1. **Modify `EnlistedMenuBehavior.cs` - Smart Routing**
   - Find existing "Access Baggage Train" menu option
   - Keep it always **enabled** (never greyed out)
   - Add state check on click:
     - If `FullAccess` → call `EnlistmentBehavior.TryOpenBaggageTrain()` (opens stash screen)
     - If `NoAccess` or `Locked` → open QM dialogue at `qm_baggage_request_response` node
   - Update tooltip dynamically based on state:
     - FullAccess: "Access your stored belongings"
     - NoAccess: "Request baggage access (wagons behind the column)"
     - Locked: "Request baggage access (storage locked down)"
     - Delayed: "Request baggage access (wagons stuck)"

2. **Modify `EnlistmentBehavior.TryOpenBaggageTrain()`**
   - This is now only called when FullAccess confirmed
   - Remove old access checks (menu handles routing now)
   - Opens `InventoryScreenHelper.OpenScreenAsStash()` directly

4. **Add localization strings**
   - `{=baggage_blocked_march}The baggage train is behind the column.`
   - `{=baggage_blocked_locked}The quartermaster has locked down all storage.`
   - `{=baggage_blocked_battle}No time for that now!`
   - `{=baggage_blocked_delayed}The wagons are stuck. It'll be {DAYS} more day(s).`
   - `{=baggage_request_cost}Request access (-{COST} QM Rep)`

5. **Implement combined state message priority**
   - When multiple conditions apply, show most relevant reason
   - Priority order (highest first):
     1. Locked (supply critical) → "Storage locked down..."
     2. Delayed (active delay event) → "Wagons stuck..."
     3. NoAccess (on march) → "Wagons behind the column..."
     4. TemporaryAccess → "Brief window available"
     5. FullAccess → normal access
   - Tooltip always shows the highest-priority reason

#### Acceptance Criteria
- [x] **Main menu**: "Access Baggage Train" option always enabled (never greyed out)
- [x] **Main menu**: When FullAccess → opens stash screen directly
- [x] **Main menu**: When NoAccess/Locked → opens QM dialogue at emergency request node
- [x] **Main menu**: Tooltip dynamically updates based on current state
- [x] **Main menu**: Tooltip shows highest-priority reason when multiple conditions apply
- [x] **QM Dialogue**: Emergency request conversation uses archetype variants
- [x] **QM Dialogue**: Player can refuse request with no penalty ("Forget it" option)
- [x] **QM Dialogue**: High rep (50+) can request without cost via favor option
- [x] **QM Dialogue**: T1-T2 get gate dialogue explaining rank requirement

#### Implementation Notes
- Modified `EnlistedMenuBehavior.cs` to add smart routing based on `BaggageAccessState`
- Added dynamic tooltips for all access states
- Menu option routes to QM dialogue (`qm_baggage.json`) when NoAccess/Locked
- Added 18 localization strings for baggage dialogue nodes
- Implemented `IsEmergencyAccessOnCooldown()` and `cooldown_not_active` gate condition
- Emergency access action handler in `EnlistedDialogManager.cs`

---

### Phase 3: QM Dialogue Integration ✅ COMPLETE

**Status:** Completed 2025-12-24  
**Goal:** Implement quartermaster dialogue for emergency access requests.

#### Acceptance Criteria
- [x] Tier-based QM responses implemented (T7+ free, T5-T6: -2 rep, T3-T4: -5 rep, T1-T2 blocked)
- [x] Emergency access cooldown system (12 hours default)
- [x] `BaggageRequestType` context field for dialogue routing
- [x] Multiple archetype-specific response variants for all nodes
- [x] Gate nodes for hostile rep, cooldown, and rank requirements

#### Implementation Notes
- Created `ModuleData/Enlisted/Dialogue/qm_baggage.json` with emergency access nodes
- Added 6 archetype variants for each dialogue response
- Tier-gated access with reputation costs scaled by rank
- Emergency access routes NoAccess/Locked states to QM conversation
- Build: 0 warnings, 0 errors

---

### Phase 4: Baggage Events ✅ COMPLETE

**Status:** Completed 2025-12-24  
**Goal:** Create the JSON events and integrate with event system.

#### Acceptance Criteria
- [x] Events fire at appropriate times (daily tick integration)
- [x] Delay events block access for specified duration
- [x] Raid events trigger with item loss
- [x] Theft events remove items from stash
- [x] Theft event does not fire when stash is empty (`baggageHasItems` requirement)
- [x] `random_baggage_loss` handles empty stash gracefully (returns 0)

#### Implementation Notes

**Events Created (5 total):**
1. `evt_baggage_arrived` - "Wagons Caught Up" - Positive event granting 4h temporary access
2. `evt_baggage_delayed` - "Wagons Stuck in the Mire" - 1 day delay with volunteer help option
3. `evt_baggage_stuck` - "Crossing Gone Bad" - 2 day delay at river ford with multiple resolution options
4. `evt_baggage_raided` - "Riders on the Baggage" - Combat event with risk/reward choices
5. `evt_baggage_theft` - "Fingers in Your Kit" - Social event for low-rep soldiers (requires items)

**New Effect Types:**
- `grantTemporaryBaggageAccess` (int hours) - Grants temporary access window
- `baggageDelayDays` (int days) - Applies delay to baggage availability (0 clears delay)
- `randomBaggageLoss` (int count) - Removes random items from stash
- `fatigue` (int delta) - Applies fatigue cost/restore

**New Requirement Types:**
- `maxSoldierRep` (int) - Maximum soldier reputation threshold
- `baggageHasItems` (bool) - Requires at least one item in baggage stash

**Code Changes:**
- Extended `EventDefinition.cs` with new effect and requirement properties
- Added effect handlers in `EventDeliveryManager.cs` (4 new methods)
- Added requirement checkers in `EventRequirementChecker.cs`
- Extended `EventCatalog.cs` parsing for new fields (both camelCase and snake_case)
- Added public methods to `EnlistmentBehavior.cs`:
  - `ModifyFatigue(int delta)` - Generic fatigue modifier for events
  - `HasBaggageItems()` - Check for requirement validation
  - `RemoveRandomBaggageItems(int count)` - Random item loss handler
- Added to `BaggageTrainManager.cs`:
  - `ClearBaggageDelay()` - Remove active delays
  - `GetBaggageDelayDaysRemaining()` - Query delay status
  - `TryTriggerBaggageEvent()` - Daily event trigger logic with probability weights
  - `QueueBaggageEvent(string eventId)` - Event delivery integration

**Localization:**
- Added 30+ strings with Bannerlord military RP flavor
- Event text uses period-appropriate terminology ("mire", "pioneers", "traces")
- Direct soldier speech and realistic gripes
- Gritty details: "eating the column's dust", "knee-deep in cold mud", "knife in the dark"

**Build Status:** 0 warnings, 0 errors

---

### Phase 5: Rank Progression ✅ COMPLETE

**Status:** Completed 2025-12-24  
**Goal:** Implement tier-gated access privileges with QM dialogue gates.

#### Tasks

1. **Modify emergency access by tier**
   - T1-T2: Cannot request at all
   - T3-T4: Full cost (-5 QM Rep)
   - T5-T6: Reduced cost (-2 QM Rep)
   - T7+: Free (no cost)

2. **Add daily access window for T5+**
   - NCOs get guaranteed access window each day
   - Separate from random "caught up" events

3. **Add column halt for T7+**
   - Commanders can order full access once per day
   - Costs Officer Rep (-3) for inconvenience
   - Conversation option with QM

4. **Add to Daily Brief**
   - "The baggage train is behind the column" when NoAccess
   - "Your belongings are accessible in camp" when FullAccess

5. **Add hostile reputation gate**
   - If QM rep < -10 (hostile), block emergency baggage request entirely
   - Matches existing pattern (sell blocked at hostile)
   - Add gate node `qm_gate_baggage_hostile` with archetype variants
   - Gate message: "I don't do favors for soldiers I don't trust."

6. **Add cooldown gate for repeat requests**
   - If player already requested emergency access within cooldown period
   - Add gate node `qm_gate_baggage_cooldown` with archetype variants
   - Track `_lastEmergencyRequestTime` in BaggageTrainManager
   - Gate message: "I already got you access not an hour ago."

7. **Implement dynamic tooltip cost display**
   - Tooltip text reflects actual cost based on player tier:
     - T3-T4: "Request access (-5 QM Rep)"
     - T5-T6: "Request access (-2 QM Rep)"
     - T7+: "Request access (free)"
   - High-rep option (50+) shows: "Ask as a favor (no cost)"
   - High-rep option only visible when `qm_rep >= 50`

#### Acceptance Criteria
- [x] T1-T2 cannot request emergency access (gate dialogue implemented)
- [x] T5+ get daily access window (NCO daily access implemented)
- [x] T7+ can halt column (free emergency access for commanders)
- [x] Costs scale appropriately by rank (T3-T4: -5, T5-T6: -2, T7+: free)
- [x] Hostile QM rep blocks emergency request with gate dialogue
- [x] Cooldown prevents spam requests with gate dialogue
- [x] Tooltip dynamically shows tier-appropriate cost
- [x] High-rep (50+) option only appears when earned

#### Implementation Notes
- Tier-based costs implemented in QM dialogue routing (`qm_baggage.json`)
- NCO daily access window grants 2-4 hours once per day while on march
- Commanders (T7+) get free emergency access via dialogue
- Hostile reputation gate (< -10 QM rep) blocks all emergency requests
- 12-hour cooldown system prevents spam with gate dialogue
- Dynamic tooltips show actual cost based on player tier
- High-rep favor option (50+ QM rep) bypasses costs
- All features integrated with existing dialogue and menu systems

---

### Phase 6: News & Reports Integration with Priority System ✅ COMPLETE

**Status:** Complete  
**Goal:** Add baggage status to Daily Brief and news system with severity-based color coding and priority-based persistence.

#### Tasks

1. **Extend DispatchItem struct with Severity**
   ```csharp
   public struct DispatchItem
   {
       // ... existing fields ...
       
       /// <summary>
       /// Severity level for color-coding and persistence duration.
       /// 0=Normal, 1=Positive, 2=Attention, 3=Urgent, 4=Critical
       /// </summary>
       public int Severity { get; set; }
   }
   ```

2. **Add to Daily Brief (`EnlistedNewsBehavior.cs`)**
   - `BuildBaggageStatusLine()` method
   - Returns tuple: `(string message, NewsSeverity severity)`
   - Color-coded by severity (see color scheme below)
   - Examples:
     - Delayed: "The supply wagons are stuck in mud two leagues back." (Attention/Yellow)
     - Raided: "Raiders hit the baggage train last night. Some kit was lost." (Urgent/Red)
     - Arrived: "The baggage wagons caught up during the halt." (Positive/Green)
     - Accessible: "Your belongings are accessible in camp." (Normal/White)
   - Only show when notable (delayed, raided, recently arrived)

2. **Implement severity-based color scheme and persistence**
   ```csharp
   public enum NewsSeverity
   {
       Normal = 0,    // White/default - routine information (6h duration)
       Positive = 1,  // Green - good news, opportunities (6h duration)
       Attention = 2, // Yellow/Orange - requires attention (12h duration)
       Urgent = 3,    // Red - problems, losses, dangers (24h duration)
       Critical = 4   // Bright Red - immediate threats (48h duration)
   }
   ```

3. **Expand Personal Feed capacity**
   - Current: `MaxPersonalFeedItems = 20` (too small for priority system)
   - New: `MaxPersonalFeedItems = 35` 
   - Reason: Critical events persist 48h, need room for ongoing events + new ones
   - Kingdom feed stays at 60 (adequate)

4. **Implement priority-based news replacement logic**
   - When adding a new dispatch item to Personal Feed:
     ```csharp
     // Can only replace if new severity >= old severity
     if (existingItem.Severity > newItem.Severity)
     {
         // Existing item is more important, keep it
         return false;
     }
     ```
   - Age-based expiration still applies per severity
   - Example: Urgent event (raid) stays visible for 24h, Normal event (routine update) expires after 6h
   - More important events cannot be pushed out by less important ones

5. **Update TrimFeeds() to be severity-aware**
   ```csharp
   private void TrimFeeds()
   {
       if (_personalFeed != null && _personalFeed.Count > MaxPersonalFeedItems)
       {
           // Sort by severity first (keep important), then by date (keep recent)
           _personalFeed = _personalFeed
               .OrderByDescending(x => x.Severity)  // Critical/Urgent kept
               .ThenByDescending(x => x.DayCreated) // Then most recent
               .Take(MaxPersonalFeedItems)
               .ToList();
       }
   }
   ```
   - Ensures critical/urgent events survive trimming even if older
   - Normal events get trimmed first when capacity reached

6. **Map severity to display duration**
   ```csharp
   private int GetMinDisplayDaysForSeverity(NewsSeverity severity)
   {
       return severity switch
       {
           NewsSeverity.Critical => 2,   // 48 hours
           NewsSeverity.Urgent => 1,     // 24 hours
           NewsSeverity.Attention => 1,  // 24 hours (rounded up from 12h)
           NewsSeverity.Positive => 1,   // 24 hours (rounded up from 6h)
           NewsSeverity.Normal => 1,     // 24 hours minimum
           _ => 1
       };
       // Note: Actual filtering happens with hour-based checks
       // MinDisplayDays ensures minimum visibility window
   }
   ```

7. **Add dispatch items for baggage events with severity**
   - Personal feed: "Baggage train raided - 2 items lost" (Urgent/Red, 24h)
   - Personal feed: "Supplies delayed by weather" (Attention/Yellow, 12h)
   - Personal feed: "Wagons caught up" (Positive/Green, 6h)

8. **Add to Company Status Report**
   - Include baggage status in Supplies section context
   - Use colored text for warnings

9. **Extend color system to ALL event summaries** (broader enhancement)
   - Add optional `"severity"` field to event JSON
   - Parser reads severity and applies color to notification AND sets MinDisplayDays
   - Existing events default to `Normal` (white, standard duration)
   - New events can specify severity for visual impact + persistence

**Event JSON Example:**
```json
{
  "id": "evt_baggage_raided",
  "severity": "urgent",  // ← Sets color AND duration
  "title": "Raiders Hit the Baggage Train",
  "setup": "Shouts from the rear...",
  "options": [ ... ]
}
```

10. **Implement UI color rendering for news feeds**
   - Map NewsSeverity to existing color styles in `EnlistedColors.xml`:
     ```csharp
     private static string GetColorStyleForSeverity(NewsSeverity severity)
     {
         return severity switch
         {
             NewsSeverity.Critical => "Alert",     // Soft Red
             NewsSeverity.Urgent => "Alert",       // Soft Red
             NewsSeverity.Attention => "Warning",  // Gold
             NewsSeverity.Positive => "Success",   // Light Green
             NewsSeverity.Normal => "Default",     // Warm Cream (standard)
             _ => "Default"
         };
     }
     ```
   - Update `FormatDispatchForDisplay()` to wrap text in color span:
     ```csharp
     public static string FormatDispatchForDisplay(DispatchItem item, bool includeColor = true)
     {
         var text = FormatDispatchItem(item); // Existing logic
         
         if (includeColor && item.Severity > 0)
         {
             var style = GetColorStyleForSeverity((NewsSeverity)item.Severity);
             return $"<span style=\"{style}\">{text}</span>";
         }
         
         return text;
     }
     ```
   - Camp Hub RECENT ACTIONS section automatically shows colored items
   - Daily Brief can use same wrapping for baggage status lines

#### Baggage Event Color & Duration Mapping

| Event Type | Severity | Color | Duration | Example |
|------------|----------|-------|----------|---------|
| Baggage arrived | Positive | Green | 6h | "Wagons caught up" |
| Access granted | Normal | White | 6h | "Belongings accessible" |
| Delayed (weather) | Attention | Yellow | 12h | "Wagons stuck in mud" |
| Delayed (terrain) | Attention | Yellow | 12h | "Baggage train behind" |
| Raided (items lost) | Urgent | Red | 24h | "Raiders hit the train" |
| Theft detected | Urgent | Red | 24h | "Items missing from stash" |
| Lockdown active | Critical | Bright Red | 48h | "Storage locked - critical supply" |

**Priority Replacement Rules:**
- Critical events cannot be replaced by anything (48h guaranteed visibility)
- Urgent events can only be replaced by Critical or another Urgent (24h minimum)
- Attention events can be replaced by Urgent/Critical (12h minimum)
- Normal/Positive events can be replaced by anything higher (6h minimum)

**Example Timeline:**
```
Hour 0:  "Wagons caught up" (Positive/Green, 6h duration) added to feed
Hour 3:  "Training session complete" (Normal/White, 6h) - Can replace Positive, added
Hour 5:  "Wagons delayed by weather" (Attention/Yellow, 12h) - Can replace Normal, added
Hour 7:  "Dice game won" (Normal/White, 6h) - CANNOT replace Attention (lower severity), blocked
Hour 9:  "Baggage train raided!" (Urgent/Red, 24h) - Can replace Attention, added
Hour 15: "Quartermaster lockdown" (Critical/Bright Red, 48h) - Can replace Urgent, added
Hour 20: "Promotion available" (Positive/Green, 6h) - CANNOT replace Critical, blocked
Hour 57: Critical event expires (48h elapsed)

Result: Critical events dominate feed, normal events only show when nothing urgent happening
```

#### Implementation Summary

**Core Changes:**
- Added `NewsSeverity` enum (Normal, Positive, Attention, Urgent, Critical)
- Extended `DispatchItem` struct with `Severity` field (saved/loaded)
- Increased `MaxPersonalFeedItems` from 20 to 35 for priority system
- Implemented severity-aware `TrimFeeds()` logic (sorts by severity, then date)
- Added `GetColorStyleForSeverity()` mapping to EnlistedColors.xml styles
- Updated `FormatDispatchForDisplay()` to wrap text in color spans
- Added optional `severity` parameter to all news addition methods

**Event Integration:**
- Added `Severity` field to `EventDefinition` class
- Added `Severity` field to `EventOutcomeRecord` class
- Implemented `ParseSeverityFromEvent()` in `EventDeliveryManager`
- Updated all baggage events in `events_baggage.json` with severity:
  - `evt_baggage_arrived`: **positive** (Green, 6h persistence)
  - `evt_baggage_delayed`: **attention** (Yellow, 12h persistence)
  - `evt_baggage_stuck`: **attention** (Yellow, 12h persistence)
  - `evt_baggage_raided`: **urgent** (Red, 24h persistence)
  - `evt_baggage_theft`: **urgent** (Red, 24h persistence)

**Color Mapping:**
- Critical/Urgent → Alert style (Soft Red #FF6B6BFF)
- Attention → Warning style (Gold #FFD700FF)
- Positive → Success style (Light Green #90EE90FF)
- Normal → Default style (Warm Cream #FAF4DEFF)

#### Acceptance Criteria
- [x] DispatchItem struct includes Severity field (saved/loaded)
- [x] NewsSeverity enum defines 5 levels with clear semantics
- [x] Personal feed capacity increased to support 48h critical events
- [x] TrimFeeds() preserves high-severity events over low-severity
- [x] Color rendering wraps dispatch text in appropriate style spans
- [x] Event system parses severity from JSON and applies to news
- [x] All baggage events have appropriate severity levels assigned
- [x] Build succeeds with 0 warnings, 0 errors
- [ ] Daily Brief mentions baggage status with colors (future enhancement)
- [ ] Priority replacement logic blocks lower-severity from replacing higher (future enhancement)
- [ ] Urgent events persist for 24 hours minimum
- [ ] Critical events persist for 48 hours minimum
- [ ] Color scheme consistent across: Daily Brief, personal feed, popup notifications
- [ ] Normal events remain white (no visual spam)
- [ ] Critical/urgent events stand out clearly
- [ ] Color system documented for future events
- [ ] Personal feed capacity expanded from 20 → 35 items
- [ ] TrimFeeds() sorts by severity first, then date
- [ ] Critical events survive trimming even when feed is full
- [ ] Feed can hold ~7-10 days of history with mixed severity levels
- [ ] FormatDispatchForDisplay() wraps text in color span tags based on severity
- [ ] Color mapping uses existing EnlistedColors.xml styles (no new colors needed)
- [ ] RECENT ACTIONS section in Camp Hub displays colored feed items
- [ ] Daily Brief baggage status line uses appropriate color span
- [ ] Colors visible in GameMenu text displays

---

### Phase 7: System Integration & Edge Cases 🚧 NOT STARTED

**Status:** Pending  
**Goal:** Handle all cross-system edge cases documented in Edge Cases Matrix.

#### Tasks

1. **Leave System Integration**
   - Detect `IsOnLeave` state and grant FullAccess
   - Handle leave expiration gracefully
   - Clear delay state when leave starts

2. **Grace Period Integration**
   - Detect `IsInDesertionGracePeriod` state
   - Grant FullAccess during grace period
   - Preserve baggage delay on grace → re-enlist (same faction)
   - Clear delay on grace → cross-faction transfer

3. **Combat/Reserve Integration**
   - Check `MapEvent` for active battle
   - Check `EnlistedEncounterBehavior.IsWaitingInReserve`
   - Implement 2-hour post-battle cooldown

4. **Capture/Prisoner Integration**
   - Freeze baggage delay timer during captivity
   - Resume delay countdown on release
   - Defer events during captivity (already implemented for courier)

5. **Siege Integration**
   - Defender (inside settlement) = FullAccess
   - Attacker (besieging) = NoAccess or Limited
   - Assault in progress = NoAccess

6. **Discharge Integration**
   - Add `HandleBaggageOnDischarge()` method
   - Deserter: Forfeit all baggage
   - Dishonorable: Reclaim QM items from baggage
   - Other discharges: Keep baggage

7. **Priority-Based Access Resolution**
   - Implement priority order in `GetCurrentAccess()`
   - Document priority order in code comments

8. **Performance Optimization**
   - Cache access state with dirty flag
   - Invalidate on: tick, settlement change, battle state, muster
   - Avoid per-frame recalculation

#### Acceptance Criteria
- [ ] Access correct during leave
- [ ] Access correct during grace period
- [ ] Access blocked during combat
- [ ] Access blocked during reserve mode
- [ ] Delay frozen during captivity
- [ ] Siege states handled correctly
- [ ] Discharge types handle baggage appropriately
- [ ] Priority resolution works when multiple conditions apply
- [ ] No crashes on any state transition

---

### Phase 8: Retinue & Promotion Integration 🚧 NOT STARTED

**Status:** Pending  
**Goal:** Ensure baggage system works with T7+ Commander features.

#### Tasks

1. **T7 Promotion Interaction**
   - If baggage delayed during promotion, queue formation selection
   - Don't block promotion due to baggage state
   - Show notification that formation selection deferred

2. **Retinue Equipment (Future)**
   - Consider requiring baggage access for retinue gear management
   - Stub out integration point for future feature

3. **Commander Access Privileges**
   - Implement "halt column" ability for T7+
   - Add QM conversation option for commanders

#### Acceptance Criteria
- [ ] T7 promotion works regardless of baggage state
- [ ] Formation selection queued if needed
- [ ] T7+ can halt column for access

---

### Phase 9: Automatic Access Windows & Polish 🚧 NOT STARTED

**Status:** Pending  
**Goal:** Implement night halts, muster integration, and daily access windows for NCOs.

**Integration Note:** The Camp Hub menu's "Access Baggage Train" option (from Phase 2) intelligently routes to these dialogue nodes when access is blocked (NoAccess/Locked state). This provides an immersive conversation-based emergency access system where the QM can approve/deny the request with appropriate rep costs and archetype-specific responses.

#### Tasks

1. **Create `qm_baggage.json` dialogue file**
   - Schema version 1, dialogueType "quartermaster"
   - All baggage-related QM response nodes
   - Follows existing qm_dialogue.json patterns

2. **Add baggage hub option to `qm_dialogue.json`**
   - New option in all supply_level hub variants:
   ```json
   {
     "id": "ask_baggage",
     "textId": "qm_player_baggage_status",
     "text": "What about my belongings?",
     "tooltip": "Ask about baggage train status",
     "next_node": "qm_baggage_status_response"
   }
   ```

3. **Implement baggage status query responses (6 variants each)**
   - `qm_baggage_status_response` with context `baggage_access: "full_access"`
   - `qm_baggage_status_response` with context `baggage_access: "no_access"`
   - `qm_baggage_status_response` with context `baggage_access: "no_access", baggage_delayed: true`
   - `qm_baggage_status_response` with context `baggage_access: "locked"`
   - Each needs 6 archetype variants = 24 total dialogue nodes

4. **Implement emergency access dialogue (6 variants)**
   - `qm_baggage_request_response` node with options:
     - "It's urgent" (-5 rep for T3-T4, -2 for T5-T6)
     - "Forget it" (no cost, return to hub)
     - "You know I'm good for it" (visible at rep 50+, no cost)
     - "Have the wagons brought up" (T7+ only, -3 officer rep)
   - 6 archetype variants for the QM response

5. **Add archetype-specific flavor to emergency responses**
   | Archetype | Flavor |
   |-----------|--------|
   | Veteran | Grudging respect, military practicality |
   | Merchant | Hints at expediting (future: gold cost?) |
   | Bookkeeper | Cites regulations, makes exception |
   | Scoundrel | Offers to "arrange" something quietly |
   | Believer | Prayer for forgetfulness |
   | Eccentric | Blames star alignment |

6. **Add gate nodes for blocked requests**
   - `qm_gate_baggage_hostile` (6 variants) - QM rep < -10
   - `qm_gate_baggage_cooldown` (6 variants) - already requested recently
   - `qm_gate_baggage_rank` (6 variants) - T1-T2 cannot request

7. **Extend QMDialogueContext.cs**
   - Add fields: `BaggageAccess`, `BaggageDelayed`, `BaggageDelayDays`
   - Add matching logic in `Matches()` method
   - Add to `GetSpecificity()` count

8. **Add action handlers to EnlistedDialogManager.cs**
   - `case "grant_emergency_baggage_access":` - apply rep cost, grant temp access
   - `case "grant_full_baggage_access":` - apply officer rep cost if any, grant until movement
   - Re-validate state before applying effects (handles mid-conversation state changes)

9. **Integrate baggage status into supply report**
   - Modify `BuildSupplyStatusMessage()` to include baggage line when relevant
   - Example: "Stock's holding. Wagons are half a league behind."

10. **Notification Polish**
    - Clear messages when access state changes
    - Tooltips explain current state
    - Daily Brief includes baggage status when relevant

11. **Save/Load Verification**
    - Verify all state fields persist correctly
    - Test load during various states (delayed, temp access, locked)
    - Verify cooldowns resume correctly

12. **Documentation Update**
    - Update Content Catalog with new events
    - Update Core Gameplay with baggage references
    - Ensure all new localization strings documented
    - Add baggage dialogue nodes to enlisted_strings.xml

#### Dialogue Node Count Summary

| Node Type | Contexts | Archetypes | Total Nodes |
|-----------|----------|------------|-------------|
| Status responses | 4 states | 6 each | 24 |
| Emergency request response | 1 | 6 | 6 |
| Hostile gate | 1 | 6 | 6 |
| Cooldown gate | 1 | 6 | 6 |
| Rank gate (T1-T2) | 1 | 6 | 6 |
| Column halt ack (T7+) | 1 | 6 | 6 |
| **Total** | | | **54 nodes** |

#### Acceptance Criteria
- [ ] QM can explain baggage status with context-aware responses
- [ ] Emergency access request works with tier-scaled costs
- [ ] High-rep (50+) can request without cost
- [ ] T7+ can halt column with officer rep cost
- [ ] Hostile rep blocks request with appropriate gate dialogue
- [ ] Cooldown blocks repeat requests with appropriate gate dialogue
- [ ] T1-T2 see gate message explaining rank requirement
- [ ] Supply report mentions baggage when relevant
- [ ] Action handlers re-validate state before applying effects
- [ ] All 54 dialogue nodes have localization strings
- [ ] All notifications clear and helpful
- [ ] Save/load works for all states
- [ ] Documentation complete

---

## Edge Cases Matrix

### System Integration Edge Cases

Based on analysis of the codebase, these systems interact with baggage access:

#### 1. Leave System (`_isOnLeave`)

| Scenario | Baggage Access | Reasoning |
|----------|----------------|-----------|
| Player on leave | **FullAccess** | Player is visible on map, party is active |
| Leave expires → desertion | Access at moment of expiry, then N/A | Clear before penalties apply |
| Leave → became vassal | **FullAccess** | Honorable transition |

**Implementation Note:** Leave check in `GetCurrentAccess()`:
```csharp
// Player on leave has full access (party is visible/active)
if (EnlistmentBehavior.Instance?.IsOnLeave == true)
    return BaggageAccessState.FullAccess;
```

#### 2. Grace Period (Lord Death/Capture)

| Scenario | Baggage Access | Duration |
|----------|----------------|----------|
| Grace period active | **FullAccess** | 14 days |
| Grace → rejoin same faction | Baggage preserved | Seamless transfer |
| Grace → join different faction | Cross-faction transfer prompt | Standard transfer flow |
| Grace → expires (desertion) | Baggage forfeited? | Edge case to decide |

**Design Decision Needed:** Should deserters forfeit their baggage? Options:
- A) Forfeit all baggage (harsh but realistic)
- B) Keep baggage but pay crime rating penalty
- C) Baggage left with previous faction (recoverable later)

#### 3. Capture/Prisoner State

| Scenario | Baggage Access | Notes |
|----------|----------------|-------|
| Player captured | **NoAccess** (implicit) | Native captivity system handles |
| Lord captured | **FullAccess** (grace) | Grace period starts |
| Player freed from captivity | Depends on state | Check grace period status |
| Courier arrives during captivity | Deferred | Already implemented |

**Implementation Note:** Prisoner state already defers courier delivery:
```csharp
// In ProcessCourierArrival():
if (Hero.MainHero?.IsPrisoner == true)
{
    // Defer delivery until player is free
    return;
}
```

#### 4. Combat & Battle Reserve

| Scenario | Baggage Access | Reasoning |
|----------|----------------|-----------|
| In active battle | **NoAccess** | Can't access baggage mid-combat |
| Wait in reserve | **NoAccess** | Still in battle encounter |
| Post-battle (2 hours) | **NoAccess** | Reorganizing |
| After post-battle period | Normal evaluation | Resume normal rules |

**Implementation Note:** Check `IsWaitingInReserve` from `EnlistedEncounterBehavior`:
```csharp
if (EnlistedEncounterBehavior.IsWaitingInReserve)
    return BaggageAccessState.NoAccess;
```

#### 5. Retinue System (T7+)

| Scenario | Baggage Access | Integration Needed |
|----------|----------------|-------------------|
| Retinue cleared (capture) | Normal evaluation | No special handling |
| Retinue cleared (discharge) | N/A | Enlistment ended |
| T7 promotion + baggage delayed | Event conflict possible | Queue formation selection |
| Retinue requisition | Requires access? | Consider adding requirement |

**Enhancement Opportunity:** Could require baggage access for retinue equipment management (future feature).

#### 6. Town/Settlement Access

| Scenario | Baggage Access | Notes |
|----------|----------------|-------|
| In town (synthetic encounter) | **FullAccess** | Settlement = access |
| In castle | **FullAccess** | Settlement = access |
| In village | **FullAccess** | Settlement = access |
| Settlement under siege | Depends on side | Defender = FullAccess, Attacker = siege rules |

**Implementation Note:** Use existing `party.CurrentSettlement` check.

#### 7. Discharge Scenarios

| Discharge Type | Baggage Handling | Reasoning |
|----------------|------------------|-----------|
| Heroic | Keep all | Exceptional service |
| Veteran | Keep all | Outstanding service |
| Honorable | Keep all | Earned through service |
| Washout | QM-issued reclaimed | Failed service |
| Dishonorable | QM-issued reclaimed | Misconduct |
| Deserter | All forfeited | Abandoned post |

**Implementation Note:** Implemented in `HandleBaggageOnDischarge()`:
```csharp
private void HandleBaggageOnDischarge(string dischargeBand)
{
    switch (dischargeBand)
    {
        case "deserter":
            // Forfeit all baggage (abandoned post)
            _baggageStash.Clear();
            break;
        case "dishonorable":
        case "washout":
            // Reclaim QM-issued items only
            ReclaimQmIssuedFromBaggage();
            break;
        case "honorable":
        case "veteran":
        case "heroic":
            // Keep all baggage (earned through service)
            break;
    }
}
```

#### 8. Siege Operations

| Scenario | Baggage Access | Reasoning |
|----------|----------------|-----------|
| Besieging (attacker) | **Limited** or NoAccess | Siege focus, baggage train protected |
| Besieged (defender) | **FullAccess** | Inside settlement |
| Assault in progress | **NoAccess** | Combat |
| Post-siege (victory) | **FullAccess** | Settled in |

#### 9. Baggage Delay During State Changes

| State Change | Delay Persists? | Handling |
|--------------|-----------------|----------|
| Enlistment → Leave | No | Cancel delay, restore access |
| Leave → Return to service | Yes (if still active) | Resume delay countdown |
| Active → Captured | Freeze delay | Resume on release |
| Grace period → Re-enlist | Transfer delay to new lord? | Edge case to decide |
| Discharge | Clear delay | Baggage becomes personal |

### Cross-System Conflict Resolution

When multiple conditions apply, use this priority order:

1. **Captivity** (highest) - Native system takes over, no menu access at all
2. **Combat/Battle** - No access during active encounters
3. **Locked (QM)** - Supply crisis or investigation overrides other states
4. **Leave** - Full access (player is active)
5. **Grace Period** - Full access (player is active)
6. **Delay** - Baggage train stuck/delayed, blocks access
7. **Siege** - Attacker = no access, Defender = full access (inside settlement)
7.5. **Muster** - Full access during muster and 6 hours after (see Muster Integration)
8. **Settlement** - Full access
9. **Temporary Access** - Brief window when wagons catch up
10. **March State** - No access (baggage behind column)
11. **Default** (lowest) - Army halted/resting = full access

### Muster Integration

Pay muster grants full baggage access to allow soldiers to manage equipment during the muster process. Access continues for 6 hours after muster completes to accommodate post-muster activities.

**Detection Logic:**
```csharp
// In BaggageTrainManager.GetCurrentAccess():

// Priority 7.5: Muster Window - grants full access during muster and for 6 hours after
// Active muster takes precedence over march state to allow baggage access during pay muster
if (enlistment.PayMusterPending)
{
    return BaggageAccessState.FullAccess;
}

// Check post-muster window (6 hours after muster completes)
if (enlistment.LastMusterCompletionTime > CampaignTime.Zero)
{
    var hoursSinceMuster = CampaignTime.Now.ToHours - enlistment.LastMusterCompletionTime.ToHours;
    if (hoursSinceMuster >= 0 && hoursSinceMuster < 6.0f)
    {
        return BaggageAccessState.FullAccess;
    }
}
```

**EnlistmentBehavior Properties Required:**
- `PayMusterPending` (bool) - True when muster is active, before player completes muster menu
- `LastMusterCompletionTime` (CampaignTime) - Set when muster completes, used for 6h window

**Implementation:**
```csharp
public BaggageAccessState GetCurrentAccess()
{
    // Priority 1: Captivity (implicit - no menu access while prisoner)
    if (Hero.MainHero?.IsPrisoner == true)
        return BaggageAccessState.NoAccess;
    
    // Priority 2: Combat/Battle
    var party = MobileParty.MainParty;
    if (party?.MapEvent != null || EnlistedEncounterBehavior.IsWaitingInReserve)
        return BaggageAccessState.NoAccess;
    
    // Priority 3: Locked (supply crisis)
    if (CompanySupplyManager.Instance?.TotalSupply < 20 || _bagCheckInProgress)
        return BaggageAccessState.Locked;
    
    // Priority 4-5: Leave or Grace Period (player active on map)
    if (EnlistmentBehavior.Instance?.IsOnLeave == true || 
        EnlistmentBehavior.Instance?.IsInDesertionGracePeriod == true)
        return BaggageAccessState.FullAccess;
    
    // Priority 6: Active Delay
    if (_baggageDelayedUntil > CampaignTime.Now)
        return BaggageAccessState.NoAccess;
    
    // Priority 7: Siege context
    if (party?.SiegeEvent != null)
    {
        if (party.BesiegerCamp != null)
            return BaggageAccessState.NoAccess; // Attacker
        return BaggageAccessState.FullAccess;   // Defender
    }
    
    // Priority 7.5: Muster window (active or within 6h of completion)
    var enlistment = EnlistmentBehavior.Instance;
    if (enlistment?.PayMusterPending == true)
        return BaggageAccessState.FullAccess;
    
    if (enlistment?.LastMusterCompletionTime > CampaignTime.Zero)
    {
        var hoursSinceMuster = CampaignTime.Now.ToHours - enlistment.LastMusterCompletionTime.ToHours;
        if (hoursSinceMuster >= 0 && hoursSinceMuster < 6.0f)
            return BaggageAccessState.FullAccess;
    }
    
    // Priority 8: Settlement
    if (party?.CurrentSettlement != null)
        return BaggageAccessState.FullAccess;
    
    // Priority 9: Temporary access
    if (_temporaryAccessExpires > CampaignTime.Now)
        return BaggageAccessState.TemporaryAccess;
    
    // Priority 10: Check march state
    if (party?.IsMoving == true && party.CurrentSettlement == null)
        return BaggageAccessState.NoAccess;
    
    // Default: Army encamped/halted
    return BaggageAccessState.FullAccess;
}
```

### QM Dialogue Edge Cases

These edge cases specifically affect the Quartermaster conversation system:

#### 1. Multiple Blocking Conditions

When player asks "What about my belongings?" and multiple conditions apply:

| Condition Combination | QM Response Priority |
|-----------------------|---------------------|
| Locked + Delayed + On March | Show "Locked" message (supply crisis) |
| Delayed + On March | Show "Delayed" message (more specific) |
| On March only | Show "Behind the column" message |

**Implementation:** `baggage_delayed` context field takes precedence over base `baggage_access` field.

#### 2. State Change During Conversation

| Scenario | Handling |
|----------|----------|
| Player requests emergency access → Event grants access mid-conversation | Re-validate state when action executes; skip rep cost if access already granted |
| Player in QM hub → Temporary access expires | Next action re-evaluates; options update on next node |
| Player selects "halt column" → Party starts moving | Grant access anyway (commander's order takes precedence) |

**Implementation Note:** Action handlers must call `BaggageTrainManager.GetCurrentAccess()` before applying effects.

#### 3. Emergency Request Gating Matrix

| QM Rep | Tier | Can Request? | Cost | Gate Node |
|--------|------|--------------|------|-----------|
| < -10 (hostile) | Any | **No** | N/A | `qm_gate_baggage_hostile` |
| -10 to 9 (wary) | T1-T2 | **No** | N/A | `qm_gate_baggage_rank` |
| -10 to 9 (wary) | T3-T4 | Yes | -5 QM Rep | None |
| -10 to 9 (wary) | T5-T6 | Yes | -2 QM Rep | None |
| -10 to 9 (wary) | T7+ | Yes | Free | None |
| 10+ (neutral+) | T1-T2 | **No** | N/A | `qm_gate_baggage_rank` |
| 10+ (neutral+) | T3-T4 | Yes | -5 QM Rep | None |
| 50+ (trusted) | T3+ | Yes | Free (favor) | None (special option) |
| Any | Any | **No** (cooldown) | N/A | `qm_gate_baggage_cooldown` |

#### 4. Archetype Responses to Emergency Request

Each archetype should respond distinctly to emergency baggage requests:

| Archetype | Grudging Approval | Refusal (hostile/cooldown) |
|-----------|-------------------|---------------------------|
| Veteran | "Fine. Make it quick." | "I said no. Fall in." |
| Merchant | "This'll cost you. Time is money." | "Bad credit with me means no favors." |
| Bookkeeper | "Irregular, but I'll note the exception." | "My records show you've already asked." |
| Scoundrel | "I'll have the boys slow the wagons." | "You're on my list, and not the good one." |
| Believer | "The Lord provides. Go quickly." | "Even the Lord's patience has limits." |
| Eccentric | "The stars say yes! Hurry before they change!" | "Mercury is in retrograde. Try later." |

#### 5. High-Rep Favor Option Visibility

The "Come on, you know I'm good for it" option requires special handling:

| Condition | Visible? | Notes |
|-----------|----------|-------|
| QM Rep < 50 | **No** | Option hidden entirely |
| QM Rep >= 50, already at FullAccess | **No** | Not needed |
| QM Rep >= 50, NoAccess state | **Yes** | Free emergency access |
| QM Rep >= 50, Locked state | **No** | Even trust can't override lockdown |

#### 6. Column Halt Authority (T7+)

| Scenario | Result |
|----------|--------|
| T7+ commander halts column | Full access granted until party moves |
| Party already in settlement | Option hidden (not needed) |
| Party in forced march pursuit | Still works, but costs -3 officer rep |
| Multiple halts same day | First one per day free of extra penalty |

---

## Implementation Progress Summary

### Completed Phases (1-4) - 2025-12-24

**Phase 1: Core Infrastructure** ✅
- `BaggageTrainManager.cs` with full state management system
- `BaggageAccessState` enum (4 states)
- Configuration system via `baggage_config.json`
- Save/load persistence with `SyncData()`
- Hourly/daily tick handlers for state transitions
- Emergency access system with cooldowns and rank-based costs

**Phase 2: Menu Integration** ✅
- Smart routing in Camp Hub menu based on access state
- Dynamic tooltips for all baggage states
- Direct stash access for FullAccess state
- QM dialogue routing for NoAccess/Locked states
- Localization strings for menu options

**Phase 3: QM Dialogue Integration** ✅
- `qm_baggage.json` with emergency access dialogue nodes
- Tier-based reputation costs (T1-T2 blocked, T3-T4: -5 rep, T5-T6: -2 rep, T7+: free)
- Archetype-specific response variants
- Cooldown system (12 hours default)
- Gate nodes for hostile rep, cooldown, and rank requirements
- Action handlers in `EnlistedDialogManager.cs`

**Phase 4: Baggage Events** ✅
- 5 events created with Bannerlord RP flavor:
  - `evt_baggage_arrived` - Positive access window event
  - `evt_baggage_delayed` - Weather/mud delays
  - `evt_baggage_stuck` - River crossing obstacle
  - `evt_baggage_raided` - Combat event with raiders
  - `evt_baggage_theft` - Social event for unpopular soldiers
- New effect types: `grantTemporaryBaggageAccess`, `baggageDelayDays`, `randomBaggageLoss`, `fatigue`
- New requirement types: `maxSoldierRep`, `baggageHasItems`
- Event triggers in `BaggageTrainManager` daily tick
- Extended `EventDefinition`, `EventDeliveryManager`, `EventCatalog`, `EventRequirementChecker`
- 30+ localization strings with period-appropriate military flavor

**Build Status:** 0 warnings, 0 errors  
**Files Modified:** 7  
**Files Created:** 2  
**Lines of Code:** ~500

### Remaining Phases (5-9)

These phases will add:
- **Phase 5:** Rank-based privileges (T5+ daily windows, T7+ column halt)
- **Phase 6:** News integration with color coding and priority persistence
- **Phase 7:** Cross-system edge case handling (leave, grace period, capture, siege)
- **Phase 8:** Retinue and promotion integration
- **Phase 9:** Night halt detection, muster integration, automatic access windows

---

## Testing Checklist

### Unit Tests

- [ ] `GetCurrentAccess()` returns FullAccess in settlement
- [ ] `GetCurrentAccess()` returns NoAccess on march
- [ ] `GetCurrentAccess()` returns Locked when supply < 20%
- [ ] Temporary access expires after duration
- [ ] Delay extends NoAccess period
- [ ] Config values load correctly

### Integration Tests

- [ ] Menu option always enabled (never greyed out)
- [ ] When FullAccess: clicking menu opens stash screen
- [ ] When NoAccess: clicking menu opens QM dialogue
- [ ] When Locked: clicking menu opens QM dialogue
- [ ] QM dialogue emergency request grants TemporaryAccess when approved
- [ ] QM dialogue "Forget it" option returns to hub with no penalty
- [ ] Emergency request costs QM Rep (T3-T4: -5, T5-T6: -2, T7+: free)
- [ ] High rep (50+) favor option appears and costs no rep
- [ ] Muster grants FullAccess
- [ ] Night halt grants TemporaryAccess

### Gameplay Tests

- [ ] Play through a campaign cycle checking access at each phase
- [ ] Verify baggage "caught up" events fire ~25% of march days
- [ ] Verify delay events extend NoAccess period
- [ ] Verify theft events remove items
- [ ] Verify raid events trigger combat
- [ ] Verify cross-faction transfer still works
- [ ] Verify save/load preserves baggage state

### Edge Case Tests

**Leave System:**
- [ ] Baggage accessible during leave
- [ ] Leave expires → baggage cleared (if deserter forfeiture enabled)
- [ ] Leave → vassal promotion → baggage preserved

**Grace Period:**
- [ ] Baggage accessible during grace period
- [ ] Grace → rejoin same faction → baggage preserved
- [ ] Grace → cross-faction → transfer prompt appears
- [ ] Grace expires → baggage handling (per design decision)

**Combat:**
- [ ] Baggage blocked during battle
- [ ] Baggage blocked during reserve mode
- [ ] Baggage accessible 2+ hours after battle
- [ ] No baggage events fire during combat

**Capture:**
- [ ] Courier deferred during captivity
- [ ] Baggage state frozen during captivity
- [ ] Access restored after release

**Retinue:**
- [ ] T7 promotion works with delayed baggage
- [ ] Retinue clear doesn't affect baggage state

**Siege:**
- [ ] Defender in siege has access (inside settlement)
- [ ] Attacker in siege has limited access
- [ ] Siege assault blocks access

**Discharge:**
- [ ] Veteran discharge keeps baggage
- [ ] Deserter discharge forfeits baggage
- [ ] Dishonorable discharge reclaims QM items from baggage

**QM Dialogue:**
- [ ] "What about my belongings?" shows correct status per access state
- [ ] Status response uses highest-priority condition (Locked > Delayed > NoAccess)
- [ ] Emergency request option hidden for T1-T2
- [ ] Emergency request option gated at hostile rep
- [ ] Emergency request option gated during cooldown
- [ ] Emergency request deducts correct rep cost per tier (5/2/0)
- [ ] High-rep (50+) favor option visible only when earned
- [ ] T7+ column halt option visible only for commanders
- [ ] Column halt grants full access until movement
- [ ] Action handlers re-validate state before applying effects
- [ ] All 6 archetypes have distinct response variants
- [ ] Gate dialogue nodes show appropriate archetype flavor

**Color-Coded News & Notifications:**
- [ ] Baggage arrived event notification displays in green (positive)
- [ ] Baggage delayed event notification displays in yellow (attention)
- [ ] Baggage raided event notification displays in red (urgent)
- [ ] Baggage theft event notification displays in red (urgent)
- [ ] Critical supply lockdown displays in bright red (critical)
- [ ] Daily Brief baggage status uses appropriate color
- [ ] Personal feed baggage entries use color-coded severity
- [ ] Normal routine messages remain white (no spam)
- [ ] Color scheme consistent across all delivery channels

**Priority-Based News Persistence:**
- [ ] Normal events expire after 6 hours
- [ ] Attention events persist for 12 hours
- [ ] Urgent events persist for 24 hours
- [ ] Critical events persist for 48 hours
- [ ] Urgent event cannot be replaced by Normal/Positive event
- [ ] Critical event cannot be replaced by any lower severity
- [ ] Same-severity events can replace each other (most recent shown)
- [ ] Personal feed shows most important recent events (not just newest)

---

## Summary

### Files to Create

| File | Lines (Est.) | Purpose |
|------|--------------|---------|
| `src/Features/Logistics/BaggageAccessState.cs` | 20 | Enum definition |
| `src/Features/Logistics/BaggageTrainManager.cs` | 500 | Core manager with edge case handling |
| `ModuleData/Enlisted/baggage_config.json` | 50 | Configuration |
| `ModuleData/Enlisted/Events/events_baggage.json` | 200 | Events |
| `ModuleData/Enlisted/Dialogue/qm_baggage.json` | 400 | QM dialogue nodes (54 nodes × 6 archetypes) |
| `ModuleData/Languages/enlisted_baggage_strings.xml` | 150 | Localization (events + dialogue) |

### Files to Modify

| File | Changes |
|------|---------|
| `EnlistmentBehavior.cs` | Add manager init, persist state, hook ticks, discharge handling |
| `EnlistedMenuBehavior.cs` | Add access checks, new menu options |
| `EnlistedDialogManager.cs` | Add baggage action handlers, state validation |
| `QMDialogueContext.cs` | Add BaggageAccess, BaggageDelayed fields |
| `qm_dialogue.json` | Add "ask_baggage" option to hub nodes |
| `EnlistedNewsBehavior.cs` | Add baggage status to Daily Brief, implement NewsSeverity enum, priority-based replacement, severity-aware trimming, expand MaxPersonalFeedItems constant (20→35), add GetColorStyleForSeverity() helper |
| `DispatchItem struct` | Add `Severity` field (int 0-4) |
| `EventDefinition.cs` | Add optional `Severity` field |
| `EventParser.cs` | Parse `"severity"` field from JSON, map to MinDisplayDays |
| `EventDeliveryManager.cs` | Apply severity colors to notifications (InformationManager.DisplayMessage) |
| `AddPersonalNews()` method | Add priority-based replacement logic |
| `TrimFeeds()` method | Sort by Severity first (descending), then DayCreated (descending) |
| `FormatDispatchForDisplay()` method | Wrap text in `<span style="{style}">` tags based on severity |
| `EnlistedColors.xml` | No changes needed - uses existing Alert/Warning/Success/Default styles |
| `EnlistedEncounterBehavior.cs` | Add reserve mode checks for baggage |
| `RetinueRecruitmentGrant.cs` | Queue formation selection if baggage delayed |
| `Enlisted.csproj` | Add new file entries |
| `enlisted_strings.xml` | Add dialogue localization entries |
| `Features/Content/README.md` / `storylet-backbone.md` | Document new content surfaces |

### Estimated Total Effort

| Phase | Hours |
|-------|-------|
| Phase 1: Core Infrastructure | 8 |
| Phase 2: Menu Integration | 6 |
| Phase 3: Automatic Access Windows | 4 |
| Phase 4: Baggage Events | 10 |
| Phase 5: Rank Progression | 6 |
| Phase 6: News Integration with Priority System | 5 |
| Phase 7: System Integration & Edge Cases | 8 |
| Phase 8: Retinue & Promotion Integration | 3 |
| Phase 9: QM Dialogue Integration & Polish | 8 |
| **Total** | **58 hours** |

### Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Combat state detection edge cases | Medium | Comprehensive unit tests for all MapEvent states |
| Save/load during temporary access | Low | Validate state on load, expire stale windows |
| Cross-system priority conflicts | Medium | Document priority order, central resolution logic |
| Performance with frequent state checks | Low | Cache with dirty flag, throttle recalculation |
| Native system interactions (captivity) | Low | Defer to native, don't interfere with prisoner state |
| QM Dialogue context mismatch | Medium | Re-validate state in action handlers, not just display |
| Dialogue node count (54 nodes) | Low | Follow existing archetype pattern, use templates |
| Tooltip dynamic text updates | Low | Build tooltip in code, not static JSON |

---

---

## Daily Brief Integration

### Overview

The Daily Brief (accessed via Camp Hub → Reports) now includes a baggage status line when the state is notable. This provides at-a-glance awareness of baggage availability without needing to check the Camp Hub.

### Status Display Priority

The baggage status line uses a priority system to show the most relevant information:

| Priority | Condition | Example Message |
|----------|-----------|-----------------|
| **1** | Recent raid (0-3 days ago) | "Raiders struck the baggage train this morning. The wagon guards are still counting what's missing." |
| **2** | Locked state (supply crisis) | "The quartermaster has locked the baggage wagons. Supplies are too scarce for personal requisitions." |
| **3** | Active delay (days behind) | "The wagons are stuck in the mud, 2 days behind the column. The drivers curse and strain." |
| **4** | Recent arrival (0-2 days ago) | "The baggage wagons caught up with the column. Men crowd around, looking for their kit." |
| **5** | Temporary access window | "The wagons have halted nearby. 3 hours to rummage through your belongings before they move on." |
| **6** | On march (informational) | "The baggage wagons rumble somewhere behind the column, out of reach for now." |

**Normal state** (FullAccess in settlement/halted) is not shown - only notable states appear.

### Event Tracking

The system tracks raid and arrival events to provide contextual messages:

```csharp
// Called when baggage raid event fires
BaggageTrainManager.Instance.RecordBaggageRaid();

// Called when baggage arrives (grants temporary access)
BaggageTrainManager.Instance.RecordBaggageArrival();
```

**State Queries:**
- `GetDaysSinceLastRaid()` - Returns days since last raid, or -1 if never raided
- `GetDaysSinceLastArrival()` - Returns days since baggage last caught up
- `IsDelayed()` - Returns true if baggage is currently delayed

**Persistence:** Raid and arrival timestamps are saved/loaded via `SyncData()`.

### RP Text Style

All baggage status messages use the project's "light RP" narrative style:

**Good Examples:**
- ✅ "The wagons are stuck in the mud, a day behind the column. The drivers curse and strain."
- ✅ "Raiders struck the baggage train this morning. The wagon guards are still counting what's missing."
- ✅ "The wagons have halted nearby. A few hours to rummage through your belongings before they move on."

**Anti-patterns (avoided):**
- ❌ "Baggage delayed 1 day(s)" - technical, uses "(s)" notation
- ❌ "Access: Temporary (4h remaining)" - UI-style status display
- ❌ "The baggage train was raided on Day 23" - breaks immersion with meta info

**Style Guidelines:**
- Use proper singular/plural (separate string IDs for 1 hour vs multiple hours)
- Add atmospheric details ("curse and strain", "crowd around")
- Soldier's perspective ("rummage through your belongings", "out of reach for now")
- Avoid technical notation like "(s)" or "X hour(s)"

### Localization Strings

**File:** `ModuleData/Languages/enlisted_strings.xml`

```xml
<!-- Baggage Daily Brief Status -->
<string id="brief_baggage_locked" text="The quartermaster has locked the baggage wagons. Supplies are too scarce for personal requisitions." />
<string id="brief_baggage_march" text="The baggage wagons rumble somewhere behind the column, out of reach for now." />
<string id="brief_baggage_temporary" text="The wagons have halted nearby. A few hours to rummage through your belongings before they move on." />
<string id="brief_baggage_temporary_plural" text="The wagons have halted nearby. {HOURS} hours to rummage through your belongings before they move on." />
<string id="brief_baggage_delayed" text="The wagons are stuck in the mud, {DAYS} days behind the column. The drivers curse and strain." />
<string id="brief_baggage_delayed_single" text="The wagons are stuck in the mud, a day behind the column. The drivers curse and strain." />
<string id="brief_baggage_raided_today" text="Raiders struck the baggage train this morning. The wagon guards are still counting what's missing." />
<string id="brief_baggage_raided_recent" text="The baggage raid weighs on everyone's mind. The guards double their watches at night." />
<string id="brief_baggage_arrived_today" text="The baggage wagons caught up with the column. Men crowd around, looking for their kit." />
<string id="brief_baggage_arrived_recent" text="The wagons are still close. A chance to check your belongings before they fall behind again." />
```

---

## News System Enhancements

### Severity-Based Priority System

The personal news feed now uses a severity-based priority system to ensure important events aren't displaced by routine updates.

### News Severity Levels

| Severity | Color Style | Min Display Duration | Replacement Rules |
|----------|-------------|---------------------|-------------------|
| **Normal** (0) | Default (warm cream) | 6 hours | Can be replaced by any severity |
| **Positive** (1) | Success (light green) | 6 hours | Can be replaced by any severity |
| **Attention** (2) | Warning (gold) | 12 hours | Can be replaced by Attention+ |
| **Urgent** (3) | Alert (soft red) | 24 hours | Can be replaced by Urgent+ |
| **Critical** (4) | Critical (bright red w/ outline) | 48 hours | Can be replaced by Critical only |

### Color Coding

**Color Styles** (defined in `GUI/Brushes/EnlistedColors.xml`):

```xml
<Style Name="Success" FontColor="#90EE90FF" />   <!-- Light Green -->
<Style Name="Warning" FontColor="#FFD700FF" />   <!-- Gold -->
<Style Name="Alert" FontColor="#FF6B6BFF" />     <!-- Soft Red -->
<Style Name="Critical" FontColor="#FF0000FF" TextOutlineAmount="1" /> <!-- Bright Red w/ Outline -->
```

**Applied automatically by `FormatDispatchForDisplay()`:**

```csharp
// Color wrapping based on severity
public static string FormatDispatchForDisplay(DispatchItem item, bool includeColor = true)
{
    var text = FormatDispatchItem(item);
    
    if (includeColor && item.Severity > 0 && !string.IsNullOrWhiteSpace(text))
    {
        var style = GetColorStyleForSeverity((NewsSeverity)item.Severity);
        return $"<span style=\"{style}\">{text}</span>";
    }
    
    return text;
}
```

### Replacement Logic

The `AddToPersonalFeed()` method enforces severity-based replacement:

1. **Within 24h window:** Check for recent items (same day or last 24 hours)
2. **Lower severity:** If new item has lower severity than existing, block replacement
3. **Higher/equal severity:** Replace lowest-severity item if feed is full
4. **Logging:** All replacements logged for debugging

**Example Scenario:**
```
Day 10, 08:00 - Urgent baggage raid added (severity 3, 24h min display)
Day 10, 14:00 - Normal training complete (severity 0, 6h min display)
Result: Training event blocked - cannot replace Urgent raid
Day 10, 20:00 - Urgent promotion event (severity 3)
Result: Can replace raid (equal severity)
```

### Event Severity Assignments

**Baggage Events:**
- `evt_baggage_arrived` - Positive (1) - Good news
- `evt_baggage_delayed` - Attention (2) - Requires attention
- `evt_baggage_raided` - Urgent (3) - Significant loss/danger
- `evt_baggage_theft` - Urgent (3) - Personal impact

**Other Systems:**
- Normal reputation changes - Normal (0)
- Order completion - Normal (0)
- Order failure - Attention (2)
- Promotion - Positive (1) or Critical (4) for major milestones
- Muster outcomes - Normal (0) unless exceptional

### Feed Trimming

When the personal feed exceeds `MaxPersonalFeedItems` (35):

```csharp
// Priority: Severity first, then date
_personalFeed = _personalFeed
    .OrderByDescending(x => x.Severity)      // Keep high-severity items
    .ThenByDescending(x => x.DayCreated)     // Then keep recent items
    .Take(MaxPersonalFeedItems)
    .ToList();
```

This ensures critical events survive even when the feed is full.

---

**End of Document**

