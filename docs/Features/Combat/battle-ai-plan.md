# Battle AI Upgrade Plan

This document combines native AI research, modern AI techniques, tactical enhancements, and the proposed Battle Orchestrator feature. Implementation details will be in a separate document.

**Goal**: Make AI smarter through better coordination and decision-making, not through cheating.

---

## Table of Contents

### Part 1: Native AI Analysis
- [1.1 Architecture Overview](#11-architecture-overview)
- [1.2 Team AI (Strategy Layer)](#12-team-ai-strategy-layer)
- [1.3 Formation AI (Behavior Layer)](#13-formation-ai-behavior-layer)
- [1.4 Query Systems (Battlefield Sensing)](#14-query-systems-battlefield-sensing)
- [1.5 Order System](#15-order-system)
- [1.6 Morale System](#16-morale-system)
- [1.7 Tactical Terrain](#17-tactical-terrain)
- [1.8 Agent-Level AI](#18-agent-level-ai)
- [1.9 Siege AI](#19-siege-ai)
- [1.10 Key Thresholds](#110-key-thresholds)

### Part 2: Identified Gaps
- [2.1 What Native AI Knows vs Uses](#21-what-native-ai-knows-vs-uses)
- [2.2 What Native AI Lacks](#22-what-native-ai-lacks)

### Part 3: Modern AI Techniques
- [3.1 Applicable Techniques](#31-applicable-techniques)
- [3.2 Techniques NOT Requiring Machine Learning](#32-techniques-not-requiring-machine-learning)

### Part 4: Battle Orchestrator Proposal
- [4.1 Purpose](#41-purpose)
- [4.2 Inputs / Outputs](#42-inputs--outputs)
- [4.3 Decision Loop (OODA)](#43-decision-loop-ooda)
- [4.4 Strategic Behaviors](#44-strategic-behaviors)
- [4.5 Edge Cases](#45-edge-cases)
- [4.6 Acceptance Criteria](#46-acceptance-criteria)

### Part 5: Tactical Enhancements
- [5.1 Smart Charge Decisions](#51-smart-charge-decisions)
- [5.2 Infantry Flanking When Advantaged](#52-infantry-flanking-when-advantaged)
- [5.3 Threat Assessment for Targeting](#53-threat-assessment-for-targeting)
- [5.4 Reserve Management](#54-reserve-management)
- [5.5 Context-Aware Agent Tuning](#55-context-aware-agent-tuning)
- [5.6 Coordination Signals](#56-coordination-signals)

### Part 6: Modding Entry Points
- [6.1 MissionBehavior Hook](#61-missionbehavior-hook)
- [6.2 Harmony Patch Targets](#62-harmony-patch-targets)
- [6.3 Useful APIs](#63-useful-apis)

### Part 7: Player Counter-Intelligence (T7+ Only)
- [7.1 Activation Gate](#71-activation-gate)
- [7.2 Player Formation Tracking](#72-player-formation-tracking)
- [7.3 Threat Projection](#73-threat-projection)
- [7.4 Flank Detection](#74-flank-detection)
- [7.5 Counter-Composition Reserve](#75-counter-composition-reserve)
- [7.6 Flank Response Decision Tree](#76-flank-response-decision-tree)
- [7.7 When Outnumbered — Maximize Casualties](#77-when-outnumbered--maximize-casualties)
- [7.8 Organized Withdrawal](#78-organized-withdrawal)
- [7.9 Reserve Caution](#79-reserve-caution)
- [7.10 Coordination Across All Formations](#710-coordination-across-all-formations)

### Part 8: Dual Orchestrator Architecture
- [8.1 Why Two Orchestrators](#81-why-two-orchestrators)
- [8.2 Adversarial Intelligence](#82-adversarial-intelligence)
- [8.3 What Each Orchestrator Manages](#83-what-each-orchestrator-manages)
- [8.4 Same Code, Different Sides](#84-same-code-different-sides)

### Part 9: AI vs AI Battle Intelligence
- [9.1 Battle Phases](#91-battle-phases)
- [9.2 Situational Cavalry Aggression](#92-situational-cavalry-aggression)
- [9.3 Formation Discipline](#93-formation-discipline)
- [9.4 Combined Arms Coordination](#94-combined-arms-coordination)
- [9.5 Formation Viability Checks](#95-formation-viability-checks)

### Part 10: Tactical Decision Engine
- [10.1 Multi-Factor Weighing](#101-multi-factor-weighing)
- [10.2 Threat vs Opportunity](#102-threat-vs-opportunity)
- [10.3 Enemy Formation Assessment](#103-enemy-formation-assessment)
- [10.4 Archer Targeting Decisions](#104-archer-targeting-decisions)
- [10.5 Cavalry Reserve Timing](#105-cavalry-reserve-timing)
- [10.6 Cavalry Cycle Charging (Lance Doctrine)](#106-cavalry-cycle-charging-lance-doctrine)
- [10.7 Reserve Commitment — Collapse Their Line](#107-reserve-commitment--collapse-their-line)
- [10.8 Pursuit Decisions](#108-pursuit-decisions)
- [10.9 Future Gating (Optional)](#109-future-gating-optional)

### Part 11: Agent Formation Behavior
- [11.1 Formation Cohesion Levels](#111-formation-cohesion-levels)
- [11.2 Formation-Level Casualty Decisions](#112-formation-level-casualty-decisions)
- [11.3 Fight to Death vs Tactical Retreat](#113-fight-to-death-vs-tactical-retreat)
- [11.4 Attacker vs Defender Roles](#114-attacker-vs-defender-roles)
- [11.5 Stalemate Prevention](#115-stalemate-prevention)
- [11.6 Player Formation Exception (T7+)](#116-player-formation-exception-t7)
- [11.7 Exploiting Enemy Disorder](#117-exploiting-enemy-disorder)
- [11.8 Pike/Spear Infantry Weapon Discipline](#118-pikespear-infantry-weapon-discipline)
- [11.9 Post-Victory Decisions](#119-post-victory-decisions)

### Part 12: Intelligent Formation Organization
- [12.1 Native API Support](#121-native-api-support)
- [12.2 Front-Line Score Calculation](#122-front-line-score-calculation)
- [12.3 Self-Organizing Ranks](#123-self-organizing-ranks)
- [12.4 Flank Spillover (When Line Stable)](#124-flank-spillover-when-line-stable)
- [12.5 Gap Filling Logic](#125-gap-filling-logic)
- [12.6 Safeguards — Formation Integrity First](#126-safeguards--formation-integrity-first)
- [12.7 Positional Combat Behavior](#127-positional-combat-behavior)

### Part 13: Formation Doctrine System
- [13.1 Two Battle Scales](#131-two-battle-scales)
- [13.2 Lord Party Battles (Small Scale)](#132-lord-party-battles-small-scale)
- [13.3 Army Battles (Large Scale)](#133-army-battles-large-scale)
- [13.4 Formation Count Logic](#134-formation-count-logic)
- [13.5 Formation Doctrines](#135-formation-doctrines)
- [13.6 Line Depth Decisions](#136-line-depth-decisions)
- [13.7 Orchestrator Formation Reading](#137-orchestrator-formation-reading)
- [13.8 Counter-Formation Tactics](#138-counter-formation-tactics)
- [13.9 Reinforcement Wave Strategy](#139-reinforcement-wave-strategy)
- [13.10 Formation Sizing](#1310-formation-sizing)
- [13.11 Troop Distribution Across Formations](#1311-troop-distribution-across-formations)

### Part 14: Battle Plan Generation
- [14.1 Reactive vs Proactive AI](#141-reactive-vs-proactive-ai)
- [14.2 Plan Types](#142-plan-types)
- [14.3 Plan Selection Logic](#143-plan-selection-logic)
- [14.4 Main Effort Designation](#144-main-effort-designation)
- [14.5 Formation Objective Assignment](#145-formation-objective-assignment)
- [14.6 Sequential Objectives (Phases)](#146-sequential-objectives-phases)
- [14.7 Cavalry Tasking](#147-cavalry-tasking)
- [14.8 Screening and Refusing Flanks](#148-screening-and-refusing-flanks)
- [14.9 Plan Adaptation](#149-plan-adaptation)
- [14.10 Plan Execution State Machine](#1410-plan-execution-state-machine)
- [14.11 Tactical Adaptation vs Flip-Flopping](#1411-tactical-adaptation-vs-flip-flopping)
- [14.12 Enemy Composition Recognition](#1412-enemy-composition-recognition)
- [14.13 Defensive Counter-Formations](#1413-defensive-counter-formations)
- [14.14 Commitment Timing (When to Engage)](#1414-commitment-timing-when-to-engage)
- [14.15 Recognizing When Defense Is Killing You](#1415-recognizing-when-defense-is-killing-you)

### Part 15: Unit Type Formations
- [15.1 Native Formation Types](#151-native-formation-types)
- [15.2 Infantry Formations](#152-infantry-formations)
- [15.3 Archer Formations](#153-archer-formations)
- [15.4 Cavalry Formations](#154-cavalry-formations)
- [15.5 Formation Positioning (Combined Arms)](#155-formation-positioning-combined-arms)
- [15.6 Dynamic Formation Switching](#156-dynamic-formation-switching)
- [15.7 Formation Width and Depth Control](#157-formation-width-and-depth-control)
- [15.8 Formation Facing and Movement](#158-formation-facing-and-movement)
- [15.9 Multi-Formation Coordination](#159-multi-formation-coordination)
- [15.10 Formation API Reference](#1510-formation-api-reference)

### Part 16: Terrain Exploitation
- [16.1 Native Terrain Data](#161-native-terrain-data)
- [16.2 High Ground Strategy](#162-high-ground-strategy)
- [16.3 Choke Point Exploitation](#163-choke-point-exploitation)
- [16.4 Forest and Difficult Terrain](#164-forest-and-difficult-terrain)
- [16.5 Cliff and Impassable Terrain](#165-cliff-and-impassable-terrain)
- [16.6 Terrain-Aware Battle Plans](#166-terrain-aware-battle-plans)
- [16.7 Proactive Terrain Seeking](#167-proactive-terrain-seeking)

### Part 17: Morale Exploitation
- [17.1 Native Morale System](#171-native-morale-system)
- [17.2 Reading Enemy Morale](#172-reading-enemy-morale)
- [17.3 Triggering Enemy Routs](#173-triggering-enemy-routs)
- [17.4 Protecting Own Morale](#174-protecting-own-morale)
- [17.5 Morale-Aware Targeting](#175-morale-aware-targeting)
- [17.6 Strategic Withdrawal Before Collapse](#176-strategic-withdrawal-before-collapse)

### Part 18: Coordinated Retreat
- [18.1 Individual Panic vs Organized Retreat](#181-individual-panic-vs-organized-retreat)
- [18.2 Retreat Decision Logic](#182-retreat-decision-logic)
- [18.3 Covering Force (Rearguard)](#183-covering-force-rearguard)
- [18.4 Step-by-Step Withdrawal](#184-step-by-step-withdrawal)
- [18.5 Preserving Force When Battle Is Lost](#185-preserving-force-when-battle-is-lost)
- [18.6 Rally Points and Regrouping](#186-rally-points-and-regrouping)

### Part 19: Battle Pacing and Cinematics
- [19.1 The Problem with Fast Battles](#191-the-problem-with-fast-battles)
- [19.2 Cinematic Battle Phases](#192-cinematic-battle-phases)
- [19.3 Deliberate Approach Speed](#193-deliberate-approach-speed)
- [19.4 The Skirmish Phase](#194-the-skirmish-phase)
- [19.5 Tension Before Contact](#195-tension-before-contact)
- [19.6 Dramatic Moments](#196-dramatic-moments)
- [19.7 Ebb and Flow](#197-ebb-and-flow)
- [19.8 Battle Duration Targets](#198-battle-duration-targets)
- [19.9 Morale-Driven Endings](#199-morale-driven-endings)
- [19.10 Spectacle Preservation](#1910-spectacle-preservation)

### Part 20: Reinforcement Intelligence
- [20.1 Native Reinforcement System](#201-native-reinforcement-system)
- [20.2 Problems with Native Reinforcements](#202-problems-with-native-reinforcements)
- [20.3 Strategic Wave Timing](#203-strategic-wave-timing)
- [20.4 Big Wave Strategic Impact](#204-big-wave-strategic-impact)
- [20.5 Formation-Aware Assignment](#205-formation-aware-assignment)
- [20.6 Quality Distribution](#206-quality-distribution)
- [20.7 Spawn Point Tactics](#207-spawn-point-tactics)
- [20.8 Reinforcement Integration](#208-reinforcement-integration)
- [20.9 Wave Coordination Between Sides](#209-wave-coordination-between-sides)
- [20.10 Desperation Waves](#2010-desperation-waves)
- [20.11 Cinematic Reinforcement Phases](#2011-cinematic-reinforcement-phases)
- [20.12 Phase Respect vs Survival Priority](#2012-phase-respect-vs-survival-priority)
- [20.13 Additional Orchestrator Opportunities](#2013-additional-orchestrator-opportunities)
- [20.14 Modding Entry Points](#2014-modding-entry-points)

### Part 21: Agent-Level Combat Director
- [21.1 Purpose](#211-purpose)
- [21.2 Champion Duel System](#212-champion-duel-system)
- [21.3 Last Stand Scenarios](#213-last-stand-scenarios)
- [21.4 Banner Bearer Drama](#214-banner-bearer-drama)
- [21.5 Small Squad Actions](#215-small-squad-actions)
- [21.6 Integration with Formation AI](#216-integration-with-formation-ai)
- [21.7 Summary: Agent-Level Drama](#217-summary-agent-level-drama)

---

# Part 1: Native AI Analysis

## 1.1 Architecture Overview

Bannerlord's battle AI is a **layered heuristic system**:

```
┌─────────────────────────────────────────────────────┐
│  TeamAIComponent (Strategy Layer)                   │
│  - Selects a TacticComponent every ~5 seconds       │
│  - Uses GetTacticWeight() with 1.5x hysteresis      │
└────────────────────────┬────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────┐
│  TacticComponent (Tactic Layer)                     │
│  - Assigns behavior weights to formations           │
│  - Sets arrangement, movement, facing orders        │
└────────────────────────┬────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────┐
│  FormationAI (Behavior Layer)                       │
│  - Selects a BehaviorComponent every tick           │
│  - Uses GetAiWeight() with 1.1x hysteresis          │
└────────────────────────┬────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────┐
│  HumanAIComponent (Agent Layer)                     │
│  - Individual soldier: formation integrity,         │
│    item pickup, mount finding, aggression tuning    │
└─────────────────────────────────────────────────────┘
```

---

## 1.2 Team AI (Strategy Layer)

**Core Classes**: `TeamAIComponent`, `TeamAIGeneral`, `TeamAISiegeAttacker`, `TeamAISiegeDefender`

**Decision Interval**: Every **5 seconds** via `MakeDecision()`

**Selection Algorithm**:
```csharp
foreach (TacticComponent tactic in _availableTactics)
{
    float weight = tactic.GetTacticWeight();
    if (tactic == _currentTactic)
        weight *= 1.5f;  // Hysteresis
    // Select highest
}
```

### Available Tactics (Field Battle)

| Tactic | Purpose | Key Weight Factors |
|--------|---------|-------------------|
| `TacticFullScaleAttack` | Offensive push | Infantry ratio × remaining power |
| `TacticDefensiveLine` | Hold position | Infantry+ranged ratios, tactical position |
| `TacticDefensiveEngagement` | Active defense | Infantry ratio, high ground proximity |
| `TacticDefensiveRing` | Circle defense | Ability to form ring |
| `TacticHoldChokePoint` | Choke defense | Choke point availability, slope |
| `TacticRangedHarrassmentOffensive` | Skirmish attack | Ranged unit ratio |
| `TacticFrontalCavalryCharge` | Cavalry rush | Cavalry ratio, remaining power |
| `TacticCoordinatedRetreat` | Organized withdrawal | Power ratio < 0.4 |
| `TacticCharge` | All-out charge | Last resort |

### Tactic Weight Examples

**TacticFullScaleAttack**:
```csharp
return Team.QuerySystem.InfantryRatio * Team.QuerySystem.RemainingPowerRatio;
```

**TacticCoordinatedRetreat**:
```csharp
if (powerRatio > 0.4f) return 0f;  // Not losing badly enough
return 1.2f + Team.QuerySystem.CavalryRatio * 0.5f;
```

---

## 1.3 Formation AI (Behavior Layer)

**Core Classes**: `FormationAI`, `BehaviorComponent`

**Decision Frequency**: Every tick (with 1.1x hysteresis for current behavior)

### Available Behaviors (Field Battle)

| Behavior | Purpose |
|----------|---------|
| `BehaviorAdvance` | Move toward enemy |
| `BehaviorCautiousAdvance` | Ranged advance with pull-back |
| `BehaviorCharge` | Direct charge |
| `BehaviorTacticalCharge` | Charge with reform states |
| `BehaviorDefend` | Hold position |
| `BehaviorHoldHighGround` | Hold high ground |
| `BehaviorFlank` | Flanking maneuver |
| `BehaviorProtectFlank` | Guard a flank |
| `BehaviorReserve` | Stay behind main line (weight: 0.04) |
| `BehaviorSkirmish` | Ranged hit-and-run |
| `BehaviorScreenedSkirmish` | Skirmish behind line |
| `BehaviorCavalryScreen` | Screen flanks |
| `BehaviorGeneral` | Commander positioning |
| `BehaviorVanguard` | Lead advance |
| `BehaviorRetreat` | Full retreat |

---

## 1.4 Query Systems (Battlefield Sensing)

### TeamQuerySystem

| Property | Description |
|----------|-------------|
| `MemberCount`, `EnemyUnitCount` | Unit counts |
| `InfantryRatio`, `RangedRatio`, `CavalryRatio` | Composition |
| `TeamPower` | Raw power of all units |
| `TotalPowerRatio` | TeamPower / EnemyTeamPower |
| `RemainingPowerRatio` | CurrentPower / InitialPower |
| `AveragePosition`, `MedianPosition` | Team centroids |
| `AverageEnemyPosition` | Enemy centroid |
| `GetLocalAllyPower(Vec2)` | Ally power near a point |
| `GetLocalEnemyPower(Vec2)` | Enemy power near a point |
| `MaxUnderRangedAttackRatio` | Highest ranged suppression |

### FormationQuerySystem

| Property | Description |
|----------|-------------|
| `IsInfantryFormation`, `IsRangedFormation` | Type classification |
| `FormationPower` | Formation's combat power |
| `LocalAllyPower`, `LocalEnemyPower` | Power near formation |
| `LocalPowerRatio` | Local superiority |
| `ClosestEnemyFormation` | Nearest enemy |
| `ClosestSignificantlyLargeEnemyFormation` | Nearest threat |
| `UnderRangedAttackRatio` | Being shot at |
| `HighGroundCloseToForeseenBattleGround` | Nearby terrain |

---

## 1.5 Order System

**MovementOrder**: Charge, ChargeToTarget, Move, Follow, Advance, FallBack, Retreat, Stop

**ArrangementOrder**:
| Order | Spacing | Speed |
|-------|---------|-------|
| Line | 2 | 0.8x |
| Loose | 6 | 0.9x |
| ShieldWall | 0 | 0.3x |
| Square | 0 | 0.3x |
| Circle | — | 0.5x |
| Column | — | 1.0x |

**FacingOrder**: LookAtEnemy, LookAtDirection

---

## 1.6 Morale System

- **Per-agent** morale (0–100), starting at 35–65
- **Panic threshold**: < 1%
- **Recovery**: 0.4/sec up to 50% of initial
- **Kill morale change**: ±3–4 base × importance × weapon modifier (4m radius, up to 10 agents)
- **Casualty factor**: Morale loss scales with `1.0 + (removed ratio × 2.0)`

---

## 1.7 Tactical Terrain

**TacticalPosition** (map-placed objects):
- Types: HighGround, ChokePoint, Forest, Cliff
- Properties: Position, Direction, Width, Slope, IsInsurmountable

**TacticalRegion** (areas):
- Types: Forest, DifficultTerrain, Opening
- Contains linked TacticalPositions

**Usage**: Tactics search for and score these; `BehaviorHoldHighGround` uses `FormationQuerySystem` height inference.

---

## 1.8 Agent-Level AI

**HumanAIComponent**: Formation integrity, item pickup, mount finding

**BehaviorValueSet** (per-soldier aggression tuning):
| Set | Context |
|-----|---------|
| Default | Balanced |
| DefensiveArrangementMove | Shield wall |
| Charge | Charging |
| DefaultDetached | Detached units |

---

## 1.9 Siege AI

- **SiegeQuerySystem**: Attacker counts by region (Left/Middle/Right/Inside)
- **SiegeLane**: Attack lanes with HasGate, IsBreach, DefensePoints
- **Siege Tactics**: `TacticBreachWalls`, `TacticDefendCastle`, `TacticPerimeterDefense`
- **Siege Behaviors**: `BehaviorUseSiegeMachines`, `BehaviorAssaultWalls`, `BehaviorDefendCastleKeyPosition`

---

## 1.10 Key Thresholds

| Value | Usage |
|-------|-------|
| 5.0 sec | Tactic decision interval |
| 1.5x | Tactic hysteresis |
| 1.1x | Behavior hysteresis |
| 0.4 | Power ratio for retreat |
| 4.0m | Morale effect radius |
| 0.01 | Morale panic threshold |
| 0.4/sec | Morale recovery rate |

---

# Part 2: Identified Gaps

## 2.1 What Native AI Knows vs Uses

The native AI has excellent battlefield awareness but **doesn't use it tactically**:

### Data the AI Has Access To

| Data Available | Location | Actually Used For |
|----------------|----------|-------------------|
| `RemainingPowerRatio` | TeamQuerySystem | Retreat trigger, some tactic weights |
| `TotalPowerRatio` | TeamQuerySystem | Limited |
| `LocalPowerRatio` | FormationQuerySystem | Some behavior decisions |
| `InfantryRatio`, `CavalryRatio` | TeamQuerySystem | Tactic selection |
| `EnemyUnitCount` | TeamQuerySystem | Formation sizing |
| `HasShield`, `IsInfantryFormation` | FormationQuerySystem | Classification only |
| `CasualtyRatio` | FormationQuerySystem | Barely used |
| Flank positions | TeamQuerySystem | Cavalry flanking only |

### What AI Does With This Data

| Behavior | Uses Power Data? | Uses Composition? |
|----------|------------------|-------------------|
| Retreat decision | ✅ Yes (< 0.4) | ❌ No |
| Cavalry flanking | ❌ No | ❌ No |
| Charge decision | ⚠️ Minor (±10% height) | ❌ No |
| Infantry splitting | ❌ No | ❌ No |
| Target selection | ❌ No | ❌ No |
| Reserve commitment | ❌ No (weight 0.04) | ❌ No |

### What AI Doesn't Do With This Data

| Expected Behavior | Reality |
|-------------------|---------|
| Split infantry to flank when outnumbering | Only cavalry flanks |
| Avoid charging spear/shield formations | Charges anyway |
| Use numerical advantage for envelopment | Single axis advance |
| Hold reserves for exploitation | Everything engages immediately |
| Pincer movement | Not implemented |
| Assess enemy formation type before charging | Power ratio only |

---

## 2.2 What Native AI Lacks

| Gap | Description |
|-----|-------------|
| **No multi-formation coordination** | Tactics assign weights independently; no coherent plan |
| **No reserve management** | `BehaviorReserve` exists but weight is only 0.04; no commit logic |
| **No concentration of force** | Formations spread evenly; no massing on weak points |
| **Limited terrain exploitation** | Requires map-placed `TacticalPosition`; no inference |
| **No trend analysis** | Only instant snapshots; no "losing for 30 seconds" tracking |
| **No coordinated withdrawal** | Individual panic/retreat; no formation-level disengage |
| **Morale is per-agent** | Cascading panic is emergent, not strategically managed |
| **No smart charge decisions** | Doesn't consider enemy shields, spears, formation state |
| **No threat assessment targeting** | Doesn't prioritize weak/isolated/damaged units |
| **Cavalry ignores enemy composition** | Charges pike walls head-on |

---

# Part 3: Modern AI Techniques

## 3.1 Applicable Techniques

Research into modern game AI reveals several techniques applicable to Bannerlord modding without requiring machine learning:

### Commander/Strategic Level

| Technique | Description | Applicability | Effort |
|-----------|-------------|---------------|--------|
| **Utility AI** | Score-based action selection — each option gets a weighted score | ✅ High — Enhance existing system | Low |
| **Influence Maps** | Spatial grid tracking control zones, threat areas | ✅ High — Identify weak points, flanking | Medium |
| **Threat Projection** | Predict unit positions based on velocity | ✅ High — Preemptive positioning | Medium |
| **AI Director** | Meta-AI monitoring battle state, adjusting intensity | ✅ Medium — Reserve timing, retreat calls | Medium |
| **Behavior Trees** | Hierarchical decisions with fallbacks | ✅ High — Clean formation decision structure | Medium |

### Individual Agent Level

| Technique | Description | Applicability | Effort |
|-----------|-------------|---------------|--------|
| **Context-Aware Tuning** | Adjust `AgentDrivenProperties` by situation | ✅ High — Direct hook exists | Low |
| **Combat Rhythm** | Timing patterns for attack sequences | ✅ High — Tune reactions | Low |
| **Coordination Signals** | Agents aware of ally actions | ✅ High — Reduce mob behavior | Medium |
| **Morale-Proactive Behavior** | React to morale before panicking | ✅ High — Hook into existing morale | Low |
| **Threat Assessment** | Better target selection | ✅ High — Smarter targeting | Medium |

---

## 3.2 Techniques NOT Requiring Machine Learning

All of the following can be implemented purely in C#:

| Technique | Implementation |
|-----------|----------------|
| Influence Maps | Spatial math on grid |
| Threat Projection | Velocity × time prediction |
| Utility AI Enhancement | Additional weight factors |
| Context-Aware Agent Tuning | Conditional property changes |
| Reserve Logic | Rule-based heuristics |
| Coordination Signals | Neighbor awareness queries |
| Smart Charge Decisions | Enemy composition checks |

---

# Part 4: Battle Orchestrator Proposal

## 4.1 Purpose

Add a **commander-layer** that sits above native AI to provide:
- Coordinated, human-like battlefield decisions
- Reserve management
- Concentration of force
- Flank refusal
- Exploitation of local superiority
- Coordinated withdrawal

**Goal**: More realistic + more difficult battles via better coordination, not cheating.

---

## 4.2 Inputs / Outputs

### Inputs (read-only signals)
- **Team scale**: Power ratios, composition, local superiority samples
- **Formation scale**: Closest enemies, local power, suppression
- **Terrain**: TacticalPosition/TacticalRegion if present; navmesh inference if not

### Outputs (bounded interventions)
- **Strategy modes**: EngageBalanced, DelayDefend, Exploit, Withdraw
- **Formation roles**: MainLine, Screen, FlankGuard, Reserve
- **Interventions**: Behavior weight nudges, formation side assignment, reserve commit/hold

---

## 4.3 Decision Loop (OODA)

Every 1–2 seconds:

1. **Observe**: Capture signals, maintain rolling trend history
2. **Orient**: Estimate frontline axis, sample local superiority, detect threats/opportunities
3. **Decide**: Choose strategy mode (with hysteresis), pick primary effort, decide reserve posture
4. **Act**: Apply bounded interventions with cooldowns

### Telemetry Requirement

The orchestrator must be observable enough that we can answer three questions from logs alone:

1. **What did each side decide?**
2. **Why did it decide that?**
3. **Did that decision improve or worsen the battle state over the next window?**

This is not optional debug garnish. It is part of the design.

Required telemetry:
- **Army snapshots** every orchestrator interval or coarse heartbeat:
  current troop counts, committed reserves, casualty ratio, ranged pressure, local power ratio, suppression pressure, current phase, current strategy, and current primary effort
- **Decision logs** whenever strategy, reserve posture, or major formation role/objective changes:
  previous state, new state, top contributing factors, and blocked alternatives if cooldowns or constraints prevented action
- **Order/intervention logs** whenever the orchestrator changes formation intent:
  target formation, requested behavior/order, reason, and expected effect
- **Outcome windows** after major decisions:
  10-30 second follow-up comparing whether the local/global state improved, stalled, or worsened
- **Battle summary** at mission end for both armies:
  casualties inflicted/taken, reserve effectiveness, plan/phase churn, time spent in each strategy, and notable successful/failed interventions

Telemetry should stay army-first and formation-first. Individual-agent logs are useful only as sampled diagnostics or for notable events, not as a full-tick firehose.

---

## 4.4 Strategic Behaviors

### "Know when in trouble"
Signals: Outnumbered for N seconds, losing trend, local collapse, sustained suppression

Response: Switch to DelayDefend → Withdraw if severe

### "Know when to exploit"
Signals: Local superiority on a segment, enemy disorganized

Response: Commit reserve, concentrate on weak point

### Terrain usage
- Use TacticalPosition/TacticalRegion when present
- Fallback: Infer defensible points via navmesh slope

---

## 4.5 Edge Cases

- No enemies: Fall back to no-op
- Missing terrain objects: Rely on inference
- Navmesh islands: Validate reachability
- Sieges/naval: Out of scope for field battle orchestrator
- Multiplayer: Orchestrator should not run
- Player as commander: Respect player orders

---

## 4.6 Acceptance Criteria

**Diagnostics**: Stable intent transitions with logged factors

**Observability**:
- Every major army decision can be reconstructed from the session log
- Both sides emit enough telemetry to compare effectiveness, plan stability, and reserve usage
- Testers can identify what changed, why it changed, and whether the change helped

**Behavior**:
- Outnumbered → hold/refuse instead of piecemeal charges
- Maintains reserve and commits appropriately
- Exploits local superiority (concentrates force)
- Withdraws in coordinated fashion

**Performance**: No frame-time spikes in large battles

**Compatibility**: No crashes with missing terrain; no multiplayer interference

---

# Part 5: Tactical Enhancements

## 5.1 Smart Charge Decisions

Make AI assess whether charging is wise:

```csharp
bool ShouldCharge(Formation us, Formation enemy)
{
    // Don't charge braced infantry with shields
    if (enemy.QuerySystem.HasShield && enemy.QuerySystem.IsInfantryFormation)
    {
        if (us.QuerySystem.IsCavalryFormation && !enemy.IsInMelee)
            return false;  // They're braced for us
    }
    
    // Don't charge uphill into formed infantry
    float heightDiff = us.CachedMedianPosition.GetNavMeshZ() 
                     - enemy.CachedMedianPosition.GetNavMeshZ();
    if (heightDiff < -2.0f && enemy.QuerySystem.IsInfantryFormation)
        return false;
        
    // Don't charge if locally outnumbered
    if (us.QuerySystem.LocalPowerRatio < 0.7f)
        return false;
        
    return true;
}
```

**Implementation**: Patch `BehaviorTacticalCharge.GetAiWeight()` to return 0 when charge is unwise.

---

## 5.2 Infantry Flanking When Advantaged

When significantly outnumbering, split infantry for envelopment:

```csharp
void ConsiderEnvelopment()
{
    float powerAdvantage = Team.QuerySystem.RemainingPowerRatio;
    
    // Need 1.5x power and enough troops to split
    if (powerAdvantage > 1.5f && _mainInfantry.CountOfUnits > 60)
    {
        // Create flanking formation
        Formation flankForce = CreateFlankingFormation(_mainInfantry, 0.3f);
        
        // Main body pins, flank attacks side
        _mainInfantry.AI.SetBehaviorWeight<BehaviorAdvance>(1f);
        flankForce.AI.SetBehaviorWeight<BehaviorFlank>(1.5f);
        flankForce.AI.Side = FormationAI.BehaviorSide.Left;
    }
}
```

**Triggers**:
- Power ratio > 1.5
- Infantry count > 60
- Enemy is engaged (pinned)

---

## 5.3 Threat Assessment for Targeting

Score enemies before engaging to prioritize weak targets:

```csharp
float ScoreTarget(Formation enemy)
{
    float score = 1.0f;
    
    // Prefer isolated targets
    if (IsIsolatedFromAllies(enemy))
        score *= 1.5f;
    
    // Prefer already-damaged formations
    score *= 1.0f + enemy.QuerySystem.CasualtyRatio;
    
    // Cavalry should avoid hardened targets
    if (OurFormation.QuerySystem.IsCavalryFormation)
    {
        if (enemy.QuerySystem.HasShield && !IsInMelee(enemy))
            score *= 0.5f;  // Shields up = bad target
    }
    
    // Prefer low-morale targets (about to break)
    float avgMorale = GetAverageMorale(enemy);
    score *= (2.0f - avgMorale);
    
    return score;
}
```

---

## 5.4 Reserve Management

Hold part of force back until opportunity:

```csharp
class ReserveManager
{
    Formation _reserve;
    bool _committed = false;
    float _battleStartTime;
    
    void Tick()
    {
        if (_committed || _reserve == null) return;
        
        float battleTime = Mission.Current.CurrentTime - _battleStartTime;
        
        // Don't commit in first 30 seconds
        if (battleTime < 30f) return;
        
        // Commit if main line is crumbling
        if (MainFormation.QuerySystem.CasualtyRatio > 0.25f)
        {
            CommitReserve(ReserveCommitReason.MainLineCollapsing);
            return;
        }
        
        // Commit if we spot an opening
        if (EnemyFlankExposed())
        {
            CommitReserveToFlank();
            return;
        }
        
        // Commit if enemy committed theirs and we're not winning
        if (EnemyReservesEngaged() && !Winning())
        {
            CommitReserve(ReserveCommitReason.Matching);
        }
    }
    
    void CommitReserve(ReserveCommitReason reason)
    {
        _committed = true;
        _reserve.AI.SetBehaviorWeight<BehaviorCharge>(1.5f);
        Log($"Reserve committed: {reason}");
    }
}
```

---

## 5.5 Context-Aware Agent Tuning

Dynamically adjust agent combat properties based on situation:

```csharp
void UpdateAgentForSituation(Agent agent)
{
    var props = agent.AgentDrivenProperties;
    
    // Under missile fire → use shield more
    if (IsUnderRangedAttack(agent))
    {
        props.AiUseShieldAgainstEnemyMissileProbability = 0.95f;
        props.AiDefendWithShieldDecisionChanceValue = 0.9f;
    }
    
    // Outnumbered locally → more defensive
    int localEnemies = CountLocalEnemies(agent, 5f);
    int localAllies = CountLocalAllies(agent, 5f);
    if (localEnemies > localAllies * 1.5f)
    {
        props.AIBlockOnDecideAbility *= 1.3f;
        props.AIAttackOnDecideChance *= 0.7f;
    }
    
    // Winning overall → press the attack
    if (agent.Team.QuerySystem.RemainingPowerRatio > 1.5f)
    {
        props.AIAttackOnParryChance *= 1.4f;
    }
    
    agent.UpdateAgentProperties();
}
```

---

## 5.6 Coordination Signals

Reduce mob behavior where everyone attacks the same target:

```csharp
bool ShouldAttackTarget(Agent agent, Agent target)
{
    // Count allies already attacking this target
    int alliesAttacking = CountAlliesAttacking(target, 3f);
    
    // If 2+ allies already on target, find another
    if (alliesAttacking >= 2 && !target.IsLowHealth())
        return false;
    
    // If target is engaged with an ally, flank instead
    if (target.IsEngagedWith(ally) && !IsFlankingPosition(agent, target))
        return false;
        
    return true;
}
```

---

# Part 6: Modding Entry Points

## 6.1 MissionBehavior Hook

```csharp
// In SubModule.OnMissionBehaviorInitialize(Mission mission):
mission.AddMissionBehavior(new BattleOrchestratorBehavior());
```

## 6.2 Harmony Patch Targets

| Target | Purpose |
|--------|---------|
| `TeamAIComponent.MakeDecision()` | Override tactic selection |
| `FormationAI.FindBestBehavior()` | Override behavior selection |
| `TacticComponent.GetTacticWeight()` | Adjust tactic weights |
| `BehaviorComponent.GetAiWeight()` | Adjust behavior weights |
| `BehaviorTacticalCharge.GetAiWeight()` | Smart charge decisions |
| `AgentStatCalculateModel.SetAiRelatedProperties()` | Agent combat tuning |

## 6.3 Useful APIs

| API | Purpose |
|-----|---------|
| `Team.QuerySystem` | Team-level data |
| `Formation.QuerySystem` | Formation-level data |
| `Formation.AI.SetBehaviorWeight<T>(weight)` | Adjust behavior weights |
| `Formation.SetMovementOrder()` | Issue orders |
| `Formation.TransferUnits()` | Split formations |
| `Agent.AgentDrivenProperties` | Combat AI tuning |
| `Agent.UpdateAgentProperties()` | Apply property changes |
| `Mission.GetFleePositionsForSide()` | Get retreat destinations |

---

# Part 7: Player Counter-Intelligence

When the player reaches **T7-T9 rank** and commands their own formation, the AI activates special counter-player logic.

## 7.1 Activation Gate

```csharp
bool ShouldActivatePlayerCounterAI()
{
    return EnlistedRank >= 7;  // Player is now a commander
}
```

| Rank | Player Role | AI Behavior |
|------|-------------|-------------|
| **T1-T6** | Soldier in NPC formation | Standard native AI — player is just another soldier |
| **T7-T9** | Lord commanding troops | **Player Counter-AI activates** |

---

## 7.2 Player Formation Tracking

The AI specifically tracks the player's formation:

```csharp
class PlayerTracker
{
    Formation PlayerFormation;        // The formation containing player's lord
    Vec2 LastPosition;
    Vec2 CurrentVelocity;
    float ThreatAngle;                // Angle relative to AI's main facing
    bool IsFlankingThreat;            // > 45° off main axis
    float TimeToContact;              // ETA if approaching
    
    void Update()
    {
        PlayerFormation = FindFormationContainingPlayerLord();
        CurrentVelocity = (PlayerFormation.Position - LastPosition) / deltaTime;
        ThreatAngle = CalculateAngleToAIFrontline();
        IsFlankingThreat = Mathf.Abs(ThreatAngle) > 45f;
        LastPosition = PlayerFormation.Position;
    }
}
```

---

## 7.3 Threat Projection

The AI reacts to **where the player will be in 10-15 seconds**, not where they are now.

```csharp
Vec2 ProjectedPosition(Formation f, float seconds)
{
    return f.Position + (f.Velocity * seconds);
}

bool WillFlankWithinTime(Formation player, float seconds)
{
    Vec2 futurePos = ProjectedPosition(player, seconds);
    float futureAngle = AngleToAILine(futurePos);
    return Mathf.Abs(futureAngle) > 45f;  // Will be on our flank
}

float EstimateTimeToFlank(Formation player)
{
    // How long until player reaches flanking position?
    Vec2 flankPoint = CalculateNearestFlankPoint();
    float distance = Vector2.Distance(player.Position, flankPoint);
    float speed = player.Velocity.magnitude;
    return speed > 0 ? distance / speed : float.MaxValue;
}
```

### Prediction Tiers

| Tier | Method | Complexity |
|------|--------|------------|
| **Simple** | 5-10 second velocity projection | Low — always active |
| **Medium** | Factor terrain, obstacles, likely objectives | Medium — when CPU allows |
| **Complex** | Learn from player patterns across battles | High — future enhancement |

---

## 7.4 Flank Detection

**Multiple signals** determine if player is flanking:

| Signal | Detection |
|--------|-----------|
| **Position** | Player > 45° off main engagement axis |
| **Movement** | Moving laterally, not toward AI |
| **Contact status** | Not currently engaged in melee |
| **Projection** | Will be on flank within 10-15 seconds |

```csharp
bool IsPlayerFlankingThreat()
{
    // Current position check
    if (Mathf.Abs(ThreatAngle) > 45f)
        return true;
    
    // Projection check
    if (WillFlankWithinTime(PlayerFormation, 15f))
        return true;
    
    // Movement direction check
    Vec2 toAICenter = (AICenter - PlayerFormation.Position).normalized;
    float approachAngle = Vector2.Angle(PlayerFormation.Velocity.normalized, toAICenter);
    if (approachAngle > 60f && PlayerFormation.Velocity.magnitude > 1f)
        return true;  // Moving sideways, not toward us
    
    return false;
}
```

---

## 7.5 Counter-Composition Reserve

The AI analyzes player's party and keeps the **right counter** in reserve — but only if that counter isn't already deployed.

### Player Threat Analysis

| Player Composition | Primary Threat | Ideal Counter |
|--------------------|----------------|---------------|
| **40%+ archers** | Ranged harassment | Cavalry to rush archers |
| **40%+ cavalry** | Flanking/hammer | Spears/pikes to brace |
| **50%+ infantry** | Direct assault | Ranged to soften |
| **Balanced** | Flexible | Cavalry (mobile reserve) |

### Smart Reserve Logic

```csharp
Formation SelectReserve(PartyComposition player, List<Formation> deployed)
{
    ThreatType primaryThreat = AnalyzePlayerThreat(player);
    
    // Check if counter already in main force
    if (IsCounterAlreadyDeployed(primaryThreat, deployed))
    {
        // Counter handled — check secondary threat or minimal reserve
        ThreatType secondary = GetSecondaryThreat(player);
        if (secondary != ThreatType.None)
            return SelectCounterFor(secondary);
        else
            return null;  // Minimal/no reserve needed
    }
    else
    {
        // Counter NOT deployed — reserve it
        return SelectCounterFor(primaryThreat);
    }
}

bool IsCounterAlreadyDeployed(ThreatType threat, List<Formation> formations)
{
    switch (threat)
    {
        case ThreatType.Ranged:
            int deployedCav = formations.Sum(f => f.CavalryCount);
            return deployedCav >= 20;  // Enough cavalry already
            
        case ThreatType.Cavalry:
            int deployedSpears = formations.Sum(f => f.SpearCount);
            return deployedSpears >= 15;  // Enough spears already
            
        case ThreatType.Infantry:
            int deployedRanged = formations.Sum(f => f.RangedCount);
            return deployedRanged >= 15;  // Enough ranged already
            
        default:
            return true;
    }
}
```

### Reserve Sizing

| Situation | Reserve Size |
|-----------|--------------|
| Counter already deployed, no secondary threat | **Minimal (0-10%)** |
| Counter already deployed, secondary threat exists | **Small (10-15%)** |
| Counter NOT deployed, AI has counter units | **Standard (20-30%)** |
| Counter NOT deployed, AI lacks counter units | **Standard (20-30%)** best available |
| Player significantly stronger | **Larger (30%+)** to react |

---

## 7.6 Flank Response Decision Tree

```
DETECT: Player formation moving to flank
    │
    ├─ Is reserve available?
    │   ├─ YES: Is reserve strong enough to block?
    │   │       ├─ YES → INTERCEPT (move reserve to block)
    │   │       └─ NO → DELAY (reserve slows player, main line adjusts)
    │   │
    │   └─ NO: Can main line wheel?
    │           ├─ YES → REFUSE FLANK (rotate formation)
    │           └─ NO → COMPACT (defensive ring/square)
    │
    └─ Is flank imminent (< 10 seconds)?
        ├─ YES → Emergency response (whatever available)
        └─ NO → Measured response (optimal counter)
```

### Reserve Behaviors

| Reserve Type | Waiting Behavior | Commit Trigger |
|--------------|------------------|----------------|
| **Cavalry vs Archers** | Stay behind line | Player archers exposed |
| **Spears vs Cavalry** | Block likely charge lanes | Player cavalry moving to flank |
| **Ranged vs Infantry** | Behind main line, firing | Main line engaged, player pinned |
| **General mobile** | Behind center | Any flank threat OR main line crumbling |

---

## 7.7 When Outnumbered — Maximize Casualties

If the player significantly outnumbers the AI, the AI accepts probable defeat but maximizes enemy losses.

### Response Priority (Outnumbered)

1. **Refuse Both Flanks** — Force head-on where AI is strongest
2. **Concentrate Firepower** — Create kill zone
3. **Fighting Withdrawal** — Trade space for casualties
4. **Compact Formation** — Deny easy encirclement

```
A. Refuse Flanks          B. Compact (Ring)         C. Fighting Withdrawal
                          
    Player                     Player                Player advances →
       ↓                          ↓                  AI pulls back →
┌──────────────┐            ┌──────┐                AI fires →
│ AI (angled)  │            │ ●●●● │                Player takes losses →
│ flanks refused│           │ ●●●● │                Repeat until final stand
└──────────────┘            └──────┘                
```

---

## 7.8 Organized Withdrawal

The AI can execute a controlled retreat when losing:

```csharp
class WithdrawalManager
{
    enum WithdrawalPhase { None, Covering, Disengaging, Retreating }
    WithdrawalPhase Phase = WithdrawalPhase.None;
    
    void InitiateWithdrawal()
    {
        Phase = WithdrawalPhase.Covering;
        
        // Ranged/cavalry cover
        foreach (var rangedFormation in GetRangedFormations())
            rangedFormation.AI.SetBehaviorWeight<BehaviorSkirmish>(2f);
        
        // Infantry begins fallback
        foreach (var infantryFormation in GetInfantryFormations())
            infantryFormation.SetMovementOrder(MovementOrder.FallBack);
    }
    
    void Tick()
    {
        if (Phase == WithdrawalPhase.Covering)
        {
            // Once infantry has disengaged, covering force retreats
            if (InfantryHasDisengaged())
            {
                Phase = WithdrawalPhase.Retreating;
                OrderFullRetreat();
            }
        }
    }
}
```

**Withdrawal is better than native AI** which just panics and routes piecemeal.

---

## 7.9 Reserve Caution

The AI stays **cautious** with reserves:
- Don't release reserves just because one threat is neutralized
- The reserve exists to handle **unexpected** situations
- Reserve commits to main line only when:
  - Main line is crumbling (> 25% casualties)
  - Clear exploitation opportunity
  - Battle is decided and mop-up needed

```csharp
bool ShouldCommitReserveToMainLine()
{
    // Main line crumbling
    if (MainFormation.CasualtyRatio > 0.25f)
        return true;
    
    // Clear victory — mop up
    if (EnemyPowerRatio < 0.3f && !PlayerFormationThreatening())
        return true;
    
    // Enemy routed — chase
    if (EnemyRouting())
        return true;
    
    // Otherwise stay cautious
    return false;
}
```

---

## 7.10 Coordination Across All Formations

The AI coordinates all formations as a unified force:

```csharp
class BattleCoordinator
{
    List<Formation> MainLine;
    Formation Reserve;
    Formation FlankGuard;
    
    void Tick()
    {
        // Track player
        PlayerTracker.Update();
        
        // Check for flank threat
        if (PlayerTracker.IsFlankingThreat)
        {
            if (Reserve != null)
                InterceptWithReserve(PlayerTracker.ProjectedPosition);
            else
                RefuseFlank(PlayerTracker.ThreatAngle > 0 ? Side.Right : Side.Left);
        }
        
        // Check main line status
        if (MainLineStressed())
        {
            if (Reserve != null && ShouldCommitReserveToMainLine())
                CommitReserveToMainLine();
        }
        
        // Check for exploitation opportunity
        if (EnemyWeakPointDetected() && Reserve != null)
        {
            CommitReserveToExploit(WeakPoint);
        }
    }
}
```

---

# Part 8: Dual Orchestrator Architecture

## 8.1 Why Two Orchestrators

Each side in battle gets its **own independent orchestrator**. This creates adversarial intelligence where both sides are actively trying to win.

```
┌───────────────────────────────────────────────────────────────────┐
│                           BATTLE                                  │
├───────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────┐         ┌─────────────────────────────┐ │
│  │  PLAYER PARTY       │         │  ENEMY PARTY                │ │
│  │  ORCHESTRATOR       │   VS    │  ORCHESTRATOR               │ │
│  │                     │         │                             │ │
│  │  Observes enemy     │◄───────►│  Observes enemy             │ │
│  │  Analyzes threats   │         │  Analyzes threats           │ │
│  │  Issues orders      │         │  Issues orders              │ │
│  │  Adapts tactics     │         │  Adapts tactics             │ │
│  └─────────────────────┘         └─────────────────────────────┘ │
│           │                                    │                  │
│           ▼                                    ▼                  │
│  ┌─────────────────────┐         ┌─────────────────────────────┐ │
│  │  Player's Infantry  │         │  Enemy Infantry             │ │
│  │  Player's Cavalry   │         │  Enemy Cavalry              │ │
│  │  Player's Archers   │         │  Enemy Archers              │ │
│  └─────────────────────┘         └─────────────────────────────┘ │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

---

## 8.2 Adversarial Intelligence

The orchestrators are **independent and trying to beat each other**. This creates emergent tactical gameplay:

```
┌─────────────────┐                    ┌─────────────────┐
│  ORCHESTRATOR A │                    │  ORCHESTRATOR B │
│  (Player Side)  │                    │  (Enemy Side)   │
├─────────────────┤                    ├─────────────────┤
│                 │                    │                 │
│  "I see their   │                    │  "They're       │
│   archers are   │  ───observes───►   │   sending       │
│   exposed..."   │                    │   cavalry!"     │
│                 │                    │                 │
│  "Send cavalry  │                    │  "Pull archers  │
│   to charge     │                    │   back, move    │
│   archers!"     │                    │   spears to     │
│                 │                    │   intercept!"   │
└─────────────────┘                    └─────────────────┘
        │                                      │
        ▼                                      ▼
   Orders cavalry                        Orders defensive
   to charge                             repositioning
```

**Result**: The player's cavalry arrives to find spears waiting instead of exposed archers. The AI **countered** the player's move.

---

## 8.3 What Each Orchestrator Manages

| Responsibility | Description |
|----------------|-------------|
| **Battle Phase** | Forming → Advancing → Engaged → Retreating |
| **Formation Roles** | Assign main line, flankers, reserve |
| **Threat Assessment** | Who is the biggest threat? What's exposed? |
| **Cavalry Deployment** | When to charge, who to target, when to withdraw |
| **Reserve Commitment** | When to commit, where to send |
| **Withdrawal Decision** | When to retreat, organized vs rout |

---

## 8.4 Same Code, Different Sides

Both orchestrators use the **same logic** — no cheating, no scripted advantages:

```csharp
class BattleOrchestrator
{
    Team MyTeam;
    Team EnemyTeam;
    
    // Both sides run identical analysis
    BattleState State;
    ThreatAssessment Threats;
    FormationRoles Roles;
    
    // The only difference is perspective
    public BattleOrchestrator(Team myTeam)
    {
        MyTeam = myTeam;
        EnemyTeam = Mission.GetEnemyTeam(myTeam);
    }
    
    void Update()
    {
        // Same intelligence, opposite sides
        State = AnalyzeBattleState(MyTeam, EnemyTeam);
        Threats = AssessThreats(EnemyTeam);
        Roles = AssignFormationRoles(MyTeam, Threats);
        IssueOrders(Roles);
    }
}

// In mission initialization:
void OnMissionStarted(Mission mission)
{
    foreach (Team team in mission.Teams)
    {
        if (team.IsValid && !team.IsPlayerGeneral)
        {
            var orchestrator = new BattleOrchestrator(team);
            RegisterOrchestrator(team, orchestrator);
        }
    }
}
```

**Key**: Smarter battles emerge from two intelligent systems competing, not from one system with scripted behavior.

---

# Part 9: AI vs AI Battle Intelligence

This section covers how AI fights AI (not just player counter-AI). Both orchestrators follow these principles.

## 9.1 Battle Phases

The AI should **form up before engaging**, not blob toward each other:

```csharp
enum BattlePhase 
{ 
    Forming,      // Establish formation, no advance
    Advancing,    // Move toward enemy in formation
    Engaged,      // In combat
    Retreating    // Organized withdrawal
}

class PhaseManager
{
    BattlePhase CurrentPhase = BattlePhase.Forming;
    float FormingDuration = 15f;  // Seconds to form up
    float FormingTimer = 0f;
    
    void Update()
    {
        switch (CurrentPhase)
        {
            case BattlePhase.Forming:
                // Hold position, establish formation
                if (AllFormationsReady() || FormingTimer > FormingDuration)
                    CurrentPhase = BattlePhase.Advancing;
                FormingTimer += dt;
                break;
                
            case BattlePhase.Advancing:
                // Move toward enemy in formation
                if (EnemyInContactRange())
                    CurrentPhase = BattlePhase.Engaged;
                break;
                
            case BattlePhase.Engaged:
                // Combat decisions
                if (ShouldRetreat())
                    CurrentPhase = BattlePhase.Retreating;
                break;
                
            case BattlePhase.Retreating:
                // Organized withdrawal
                break;
        }
    }
    
    bool AllFormationsReady()
    {
        // Formations are in position and facing enemy
        return MyFormations.All(f => f.IsInPosition && f.IsFacingEnemy);
    }
}
```

---

## 9.2 Situational Cavalry Aggression

Cavalry aggression should be **decided by the orchestrator** based on the situation, not a fixed setting:

| Situation | Aggression Level | Behavior |
|-----------|------------------|----------|
| Enemy archers exposed, no spears nearby | **Aggressive** | Charge immediately |
| Enemy has spear screen | **Conservative** | Wait for opportunity |
| Enemy cavalry threatening our archers | **Moderate** | Counter-charge to intercept |
| We're winning decisively | **Aggressive** | Chase down fleeing troops |
| We're losing, cavalry is last reserve | **Conservative** | Protect, don't throw away |

```csharp
CavalryAggression DetermineCavalryAggression(FormationQueryData cavalry, BattleState state)
{
    // Check for exposed high-value targets
    if (EnemyArchersExposed() && !SpearsBetweenUs(cavalry, EnemyArchers))
        return CavalryAggression.Aggressive;
    
    // Check if we need to defend
    if (EnemyCavalryThreateningOurArchers())
        return CavalryAggression.Moderate;  // Counter-charge
    
    // Check if cavalry is our last hope
    if (state.RemainingPowerRatio < 0.5f && cavalry.IsLargestFormation)
        return CavalryAggression.Conservative;  // Don't waste them
    
    // Check for pursuit opportunity
    if (state.EnemyFleeing)
        return CavalryAggression.Aggressive;  // Run them down
    
    return CavalryAggression.Moderate;  // Default
}
```

---

## 9.3 Formation Discipline

Prevent armies from blobbing:

| Principle | Implementation |
|-----------|----------------|
| **Hold formation during advance** | Slower advance, maintain spacing |
| **Don't charge prematurely** | Wait for signal from orchestrator |
| **Maintain combined arms structure** | Archers behind infantry, cavalry on flanks |
| **Don't run to map edge** | Only advance as far as needed |

```csharp
void EnforceFormationDiscipline(Formation f)
{
    // Don't advance past the main line
    if (f.FormationType == FormationClass.Ranged)
    {
        float mainLineDistance = DistanceToMainLine(f);
        if (mainLineDistance > MaxArcherAdvance)
        {
            f.SetMovementOrder(MovementOrder.MovementOrderStop);
        }
    }
    
    // Cavalry waits for clear target
    if (f.FormationType == FormationClass.Cavalry)
    {
        if (!HasClearChargeTarget(f))
        {
            // Hold on flank, don't charge into spears
            f.SetMovementOrder(MovementOrder.MovementOrderStop);
        }
    }
}
```

---

## 9.4 Combined Arms Coordination

The orchestrator coordinates all formations as a unit:

```csharp
void CoordinateCombinedArms()
{
    // Infantry: Main line
    foreach (var inf in InfantryFormations)
    {
        inf.Role = FormationRole.MainLine;
        inf.Order = AdvanceToEngagementRange();
    }
    
    // Archers: Stay behind infantry
    foreach (var archer in RangedFormations)
    {
        archer.Role = FormationRole.RangedSupport;
        archer.Order = StayBehindMainLine(distance: 30f);
    }
    
    // Cavalry: Flanks, wait for opportunity
    foreach (var cav in CavalryFormations)
    {
        cav.Role = FormationRole.FlankingForce;
        cav.Order = HoldOnFlank();
        
        // Orchestrator signals when to charge
        if (ShouldCavalryCharge(cav))
        {
            cav.Order = ChargeTarget(GetBestTarget(cav));
        }
    }
}
```

**Key coordination signals**:
- "Infantry engage" — main line advances to contact
- "Archers hold fire" — too close to friendlies
- "Cavalry charge" — target exposed
- "Cavalry return" — reform after charge
- "All withdraw" — organized retreat

---

## 9.5 Formation Viability Checks

Don't form formations that don't make sense:

```csharp
bool ShouldFormSquare(Formation f)
{
    // Need minimum troops for a square
    if (f.CountOfUnits < 16)
        return false;  // Too few, square is weak
    
    // Only form square if cavalry threat exists
    if (!EnemyHasCavalry())
        return false;  // No point
    
    // Only if cavalry is actually threatening
    if (!CavalryWithinRange(f, threatDistance: 80f))
        return false;  // Not yet
    
    return true;
}

bool ShouldFormShieldWall(Formation f)
{
    // Need shields
    float shieldRatio = f.CountOfUnitsWithShield / (float)f.CountOfUnits;
    if (shieldRatio < 0.5f)
        return false;  // Not enough shields
    
    // Only against ranged threat
    if (!UnderRangedAttack(f))
        return false;
    
    return true;
}
```

---

## Design Summary

| Problem | Solution |
|---------|----------|
| Armies blob toward each other | Battle phases: Form → Advance → Engage |
| Cavalry charges into spears | Situational aggression determined by orchestrator |
| No formation discipline | Hold positions, maintain structure |
| Archers get overrun | Combined arms: archers stay behind infantry |
| Pointless formations | Viability checks before forming squares/shieldwalls |
| No coordination | Orchestrator coordinates all formations as a unit |
| One-sided intelligence | Two orchestrators, same code, adversarial |

---

# Part 10: Tactical Decision Engine

The AI uses a **full tactical decision engine** that weighs multiple factors simultaneously. Every AI commander uses this system (gating by bandit/troop count can be added later).

## 10.1 Multi-Factor Weighing

The AI doesn't follow simple rules. It weighs tradeoffs:

```
┌────────────────────────────────────────────────────────────────┐
│                    TACTICAL DECISION ENGINE                    │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│   What's killing us?  ←──┐      ┌──→  What can we exploit?    │
│                          │      │                              │
│                     ┌────┴──────┴────┐                        │
│                     │   WEIGHING     │                        │
│                     │   TRADEOFFS    │                        │
│                     └────────────────┘                        │
│                            │                                   │
│              ┌─────────────┼─────────────┐                    │
│              ▼             ▼             ▼                    │
│         DEFEND        COMMIT         EXPLOIT                  │
│         (stop loss)   (calculated)   (opportunity)            │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

---

## 10.2 Threat vs Opportunity

The AI considers **both** stopping what's killing us AND taking opportunities:

```csharp
class TacticalWeighing
{
    float EvaluateAction(TacticalAction action)
    {
        float score = 0;
        
        // What do we gain?
        score += action.ExpectedDamageToEnemy;
        score += action.PositionalAdvantage;
        score += action.MoraleImpact;
        
        // What do we lose while doing this?
        score -= action.ExpectedCasualties;
        score -= action.OngoingThreatWhileExecuting;  // Key: time cost
        
        // Time factor — how much do we bleed while executing?
        score -= action.TimeToExecute * CurrentBleedRate;
        
        return score;
    }
}
```

**Example**: Cavalry can flank archers in 20 seconds. During those 20 seconds, archers kill 5 more infantry. Is the flank worth 5 casualties?

---

## 10.3 Enemy Formation Assessment

Before deciding responses, classify each enemy formation:

```csharp
enum ThreatLevel { None, Low, Medium, High, Critical }
enum AccessLevel { Open, Contested, Blocked }

class EnemyFormationAssessment
{
    Formation Target;
    ThreatLevel Threat;       // How much damage are they doing?
    AccessLevel Access;       // Can we reach them?
    float DistanceToUs;
    Formation BlockingUnit;   // Who's in the way?
    
    // Quality assessment
    float AverageArmor;       // Can our archers hurt them?
    float AverageTier;        // Elite or conscripts?
    float AverageSkill;       // Trained or green?
}

class ThreatAssessor
{
    List<EnemyFormationAssessment> AssessAllEnemies()
    {
        var assessments = new List<EnemyFormationAssessment>();
        
        foreach (var enemy in EnemyFormations)
        {
            var assessment = new EnemyFormationAssessment
            {
                Target = enemy,
                Threat = CalculateThreat(enemy),
                Access = CalculateAccess(enemy),
                BlockingUnit = FindBlockingUnit(enemy),
                AverageArmor = enemy.GetAverageArmor(),
                AverageTier = enemy.GetAverageTier(),
                AverageSkill = enemy.GetAverageSkill()
            };
            assessments.Add(assessment);
        }
        
        return assessments;
    }
    
    ThreatLevel CalculateThreat(Formation enemy)
    {
        float damagePerSecond = enemy.DamageOutputToUs;
        
        if (damagePerSecond > CriticalDamageThreshold)
            return ThreatLevel.Critical;
        if (damagePerSecond > HighDamageThreshold)
            return ThreatLevel.High;
        if (damagePerSecond > MediumDamageThreshold)
            return ThreatLevel.Medium;
        if (damagePerSecond > 0)
            return ThreatLevel.Low;
        
        return ThreatLevel.None;
    }
    
    AccessLevel CalculateAccess(Formation enemy)
    {
        // Is there a formation between us and them?
        var blocking = FindBlockingUnit(enemy);
        
        if (blocking == null)
            return AccessLevel.Open;
        
        // How strong is the blocker?
        if (blocking.Power > SignificantBlockerThreshold)
            return AccessLevel.Blocked;
        
        return AccessLevel.Contested;
    }
}
```

---

## 10.4 Archer Targeting Decisions

Archers decide targets based on **enemy quality**, not just proximity:

```csharp
class ArcherTargetDecision
{
    Formation DecideArcherTarget(Formation ourArchers, BattleState state)
    {
        var enemyArchers = state.EnemyRanged;
        var enemyInfantry = state.EnemyInfantry;
        
        // Assess enemy infantry quality
        float infantryThreat = AssessInfantryThreat(enemyInfantry);
        
        // Assess archer threat
        float archerThreat = AssessArcherThreat(enemyArchers);
        
        // Decision logic
        if (infantryThreat > archerThreat)
        {
            // Their infantry will collapse our line — shoot them
            return enemyInfantry;
        }
        else if (CanWinArcherDuel(ourArchers, enemyArchers))
        {
            // We can silence their archers, then focus infantry
            return enemyArchers;
        }
        else
        {
            // Their archers are stronger — don't waste arrows on losing duel
            return enemyInfantry;
        }
    }
    
    float AssessInfantryThreat(Formation infantry)
    {
        float threat = 0;
        
        // Armor — can our arrows even hurt them?
        float avgArmor = infantry.AverageArmorValue;
        if (avgArmor > HighArmorThreshold)
            threat -= 0.5f;  // Arrows won't do much, lower priority
        
        // Tier — are they elite?
        float avgTier = infantry.AverageTroopTier;
        threat += avgTier * 0.2f;
        
        // Numbers vs our line
        float ratio = infantry.Count / (float)OurInfantry.Count;
        threat += ratio * 0.5f;
        
        // Skill — will they beat our infantry in melee?
        float skillDiff = infantry.AverageSkill - OurInfantry.AverageSkill;
        threat += skillDiff * 0.3f;
        
        return threat;
    }
    
    bool CanWinArcherDuel(Formation ours, Formation theirs)
    {
        // Compare archer power
        float ourPower = ours.Count * ours.AverageSkill;
        float theirPower = theirs.Count * theirs.AverageSkill;
        
        // We need advantage to commit to duel
        return ourPower > theirPower * 1.2f;
    }
}
```

---

## 10.5 Cavalry Reserve Timing

**"Should we keep cavalry in reserve to see when and where we need them?"**

```csharp
class CavalryReserveDecision
{
    enum CavalryPosture { Reserve, Screening, Committed }
    
    CavalryPosture DecideCavalryPosture(Formation cavalry, BattleState state)
    {
        // Early battle — hold in reserve, observe
        if (state.Phase == BattlePhase.Forming || state.Phase == BattlePhase.Advancing)
            return CavalryPosture.Reserve;
        
        // Immediate threat to our archers?
        if (EnemyCavalryThreateningArchers())
            return CavalryPosture.Screening;  // Defensive counter
        
        // Check for opportunities
        var opportunity = FindBestCavalryOpportunity(cavalry);
        if (opportunity != null && ShouldCommitNow(opportunity, state))
            return CavalryPosture.Committed;
        
        // No clear opportunity — keep watching
        return CavalryPosture.Reserve;
    }
    
    bool ShouldCommitNow(CavalryOpportunity opportunity, BattleState state)
    {
        // Time-sensitive opportunity?
        if (opportunity.TimeWindow < 10f)
            return true;  // Now or never
        
        // Could a better opportunity appear?
        if (state.Phase == BattlePhase.Advancing && !state.Desperate)
            return false;  // Wait and see
        
        // Are we losing and need to act?
        if (state.RemainingPowerRatio < 0.7f)
            return true;  // Can't afford to wait
        
        return opportunity.Score > HighConfidenceThreshold;
    }
    
    CavalryOpportunity FindBestCavalryOpportunity(Formation cavalry)
    {
        var opportunities = new List<CavalryOpportunity>();
        
        // Check for exposed archers
        if (EnemyArchersExposed())
        {
            opportunities.Add(new CavalryOpportunity
            {
                Target = EnemyArchers,
                Score = 1.0f,
                TimeWindow = EstimateTimeBeforeProtected(EnemyArchers)
            });
        }
        
        // Check for isolated units
        foreach (var enemy in EnemyFormations.Where(f => IsIsolated(f)))
        {
            opportunities.Add(new CavalryOpportunity
            {
                Target = enemy,
                Score = 0.7f,
                TimeWindow = 30f  // Usually have time
            });
        }
        
        // Check for fleeing units
        foreach (var enemy in EnemyFormations.Where(f => f.IsRetreating))
        {
            opportunities.Add(new CavalryOpportunity
            {
                Target = enemy,
                Score = 0.8f,
                TimeWindow = 60f  // Chase them down
            });
        }
        
        return opportunities.OrderByDescending(o => o.Score).FirstOrDefault();
    }
}
```

---

## 10.6 Cavalry Cycle Charging (Lance Doctrine)

**"Cavalry must charge, impact, disengage, and reform at distance - not get bogged down in sustained melee"**

Lance-armed cavalry is devastating **on the charge** but vulnerable in prolonged melee. Native AI lets them charge once then fight as inferior infantry. This section implements proper cycle charging.

### The Problem with Native Cavalry AI

```
NATIVE AI:
┌──────────────────────────────────────────────────────────────┐
│  Cavalry charges → Impacts with lances → Gets stuck in melee │
│  Fighting infantry with swords → Eventually dies             │
│                                                               │
│  Lance used ONCE, then wasted                                │
└──────────────────────────────────────────────────────────────┘

PROPER CAVALRY DOCTRINE:
┌──────────────────────────────────────────────────────────────┐
│  Charge (50-80m away) → Impact with lances → Disengage      │
│  → Reform 80m+ away → Charge again → Repeat                 │
│                                                               │
│  Lance used EVERY charge cycle                               │
└──────────────────────────────────────────────────────────────┘
```

### Cavalry State Machine

```csharp
enum CavalryState
{
    Reserve,        // Waiting for orders
    Positioning,    // Moving to charge position
    Charging,       // Active charge (lances ready)
    Impact,         // Hitting enemy (1-3 seconds)
    Melee,          // Stuck in fight (BAD if too long)
    Disengaging,    // Breaking contact
    Rallying,       // Moving to rally point
    Reforming       // Reorganizing formation
}

class CavalryCycleManager
{
    CavalryState CurrentState;
    float StateEnterTime;
    Vec2 RallyPoint;
    Formation TargetFormation;
    
    const float MaxMeleeTime = 12f;          // Max 12 seconds in melee
    const float MinChargeDistance = 60f;     // Need 60m for effective charge
    const float ReformDistance = 80f;        // Rally 80m away
    const float ReformDuration = 8f;         // 8 seconds to reform
}
```

### Charge Cycle Logic

```csharp
void TickCavalryCycle(Formation cavalry, BattleState state)
{
    float timeSinceStateChange = Mission.Current.CurrentTime - StateEnterTime;
    
    switch (CurrentState)
    {
        case CavalryState.Reserve:
            // Waiting for opportunity
            if (ShouldCommitCavalry(cavalry, state))
            {
                TargetFormation = SelectChargeTarget(cavalry, state);
                ChangeState(CavalryState.Positioning);
            }
            break;
            
        case CavalryState.Positioning:
            // Moving to get proper charge distance
            Vec2 chargePosition = CalculateChargePosition(cavalry, TargetFormation);
            float distanceToPosition = cavalry.QuerySystem.AveragePosition.Distance(chargePosition);
            
            if (distanceToPosition < 10f)
            {
                // In position, ready to charge
                ChangeState(CavalryState.Charging);
            }
            else
            {
                cavalry.SetMovementOrder(MovementOrder.Move(chargePosition));
                cavalry.SetArrangementOrder(ArrangementOrder.Line);
            }
            break;
            
        case CavalryState.Charging:
            // Active charge
            cavalry.SetMovementOrder(MovementOrder.ChargeToTarget(TargetFormation));
            cavalry.SetArrangementOrder(ArrangementOrder.Skein);  // Wedge for impact
            
            // Detect impact (entered melee)
            if (cavalry.IsInMelee())
            {
                ChangeState(CavalryState.Impact);
            }
            break;
            
        case CavalryState.Impact:
            // Just hit enemy — let momentum carry through
            // This is where lances do their damage
            
            if (timeSinceStateChange > 3f)
            {
                // Impact complete, now in melee
                ChangeState(CavalryState.Melee);
            }
            break;
            
        case CavalryState.Melee:
            // In sustained combat
            // Lances are spent, fighting with swords
            
            // CRITICAL: Don't stay here long
            if (timeSinceStateChange > MaxMeleeTime || ShouldDisengageNow(cavalry, state))
            {
                // Time to leave
                RallyPoint = CalculateRallyPoint(cavalry, state);
                ChangeState(CavalryState.Disengaging);
            }
            break;
            
        case CavalryState.Disengaging:
            // Breaking contact and moving away
            cavalry.SetMovementOrder(MovementOrder.Move(RallyPoint));
            cavalry.SetArrangementOrder(ArrangementOrder.Loose);  // Spread out to disengage
            
            float distanceFromEnemy = cavalry.QuerySystem.AveragePosition.Distance(
                TargetFormation.QuerySystem.AveragePosition);
            
            if (distanceFromEnemy > ReformDistance * 0.8f)
            {
                // Far enough, start reforming
                ChangeState(CavalryState.Rallying);
            }
            break;
            
        case CavalryState.Rallying:
            // Moving to rally point
            float distanceToRally = cavalry.QuerySystem.AveragePosition.Distance(RallyPoint);
            
            if (distanceToRally < 20f)
            {
                // At rally point, reform
                ChangeState(CavalryState.Reforming);
            }
            else
            {
                cavalry.SetMovementOrder(MovementOrder.Move(RallyPoint));
            }
            break;
            
        case CavalryState.Reforming:
            // Reorganizing formation
            cavalry.SetMovementOrder(MovementOrder.Stop);
            cavalry.SetArrangementOrder(ArrangementOrder.Line);
            cavalry.SetFacingOrder(FacingOrder.LookAtEnemy);
            
            if (timeSinceStateChange > ReformDuration && IsFormationReformed(cavalry))
            {
                // Reformed, ready to charge again or return to reserve
                if (ShouldChargeAgain(cavalry, state))
                    ChangeState(CavalryState.Positioning);  // Another charge
                else
                    ChangeState(CavalryState.Reserve);  // Back to reserve
            }
            break;
    }
}

void ChangeState(CavalryState newState)
{
    CurrentState = newState;
    StateEnterTime = Mission.Current.CurrentTime;
}
```

### When to Disengage

```csharp
bool ShouldDisengageNow(Formation cavalry, BattleState state)
{
    // DISENGAGE: Been in melee too long
    float timeInMelee = Mission.Current.CurrentTime - StateEnterTime;
    if (timeInMelee > MaxMeleeTime)
        return true;
    
    // DISENGAGE: Taking heavy casualties
    if (cavalry.QuerySystem.CasualtyRatio > 0.3f)
        return true;
    
    // DISENGAGE: Enemy reinforcements incoming
    if (EnemyReinforcementsApproaching(cavalry.Position, state))
        return true;
    
    // DISENGAGE: Target is routing (job done)
    if (TargetFormation.QuerySystem.IsRetreating)
        return true;
    
    // STAY: Enemy is breaking (finish them)
    if (TargetFormation.QuerySystem.AverageMorale < 30f)
        return false;
    
    // STAY: Still effective (getting kills)
    if (cavalry.RecentKills > cavalry.RecentCasualties)
        return false;
    
    return false;  // Continue melee
}
```

### Rally Point Calculation

```csharp
Vec2 CalculateRallyPoint(Formation cavalry, BattleState state)
{
    // Goal: Position 80m+ from enemy, behind friendly lines or on flank
    // Must have clear space for next charge
    
    Vec2 currentPos = cavalry.QuerySystem.AveragePosition;
    Vec2 enemyCenter = state.EnemyCenter;
    Vec2 ourCenter = state.OurCenter;
    
    // Direction away from enemy
    Vec2 awayFromEnemy = (ourCenter - enemyCenter).Normalized();
    
    // Base rally: Behind our lines
    Vec2 rallyBase = ourCenter + awayFromEnemy * 60f;
    
    // Offset to flank (perpendicular to enemy line)
    Vec2 flankDirection = new Vec2(-awayFromEnemy.Y, awayFromEnemy.X);
    
    // Prefer the flank we charged from
    float flankSide = DeterminedFlankSide(cavalry, state);  // +1 or -1
    Vec2 rallyPoint = rallyBase + flankDirection * (50f * flankSide);
    
    // Validate: Is this point clear and navigable?
    if (!IsPositionClear(rallyPoint, MinChargeDistance))
    {
        // Try opposite flank
        rallyPoint = rallyBase + flankDirection * (50f * -flankSide);
    }
    
    // Validate: Minimum distance from enemy
    float distToEnemy = rallyPoint.Distance(enemyCenter);
    if (distToEnemy < ReformDistance)
    {
        // Push further back
        rallyPoint = rallyPoint + awayFromEnemy * (ReformDistance - distToEnemy + 20f);
    }
    
    return rallyPoint;
}
```

### Charge Position Calculation

```csharp
Vec2 CalculateChargePosition(Formation cavalry, Formation target)
{
    // Need to position 60-80m from target for effective charge
    // Prefer flanks or rear
    
    Vec2 targetPos = target.QuerySystem.AveragePosition;
    Vec2 targetFacing = target.Direction;
    
    // Check flank vulnerability
    float leftFlankExposure = AssessFlankExposure(target, FlankSide.Left);
    float rightFlankExposure = AssessFlankExposure(target, FlankSide.Right);
    float rearExposure = AssessRearExposure(target);
    
    Vec2 chargeVector;
    
    if (rearExposure > 0.7f)
    {
        // Rear charge (devastating)
        chargeVector = targetFacing;  // Same direction they're facing
    }
    else if (leftFlankExposure > rightFlankExposure)
    {
        // Left flank charge
        chargeVector = new Vec2(-targetFacing.Y, targetFacing.X);  // Perpendicular
    }
    else
    {
        // Right flank charge
        chargeVector = new Vec2(targetFacing.Y, -targetFacing.X);  // Perpendicular
    }
    
    // Position: 70m away in charge direction
    Vec2 chargePosition = targetPos + chargeVector * 70f;
    
    return chargePosition;
}
```

### Lance-Specific Considerations

```csharp
class LanceAwareness
{
    float CalculateLanceEffectiveness(Formation cavalry, Formation target)
    {
        // Lances are effective when:
        // 1. Cavalry has speed (charge from distance)
        // 2. Target is NOT braced spears facing you
        // 3. Target is NOT in shield wall facing you
        
        float effectiveness = 1.0f;
        
        // Check if target is braced against us
        if (IsTargetBracedAgainst(cavalry, target))
            effectiveness *= 0.2f;  // Very bad matchup
        
        // Check charge distance
        float chargeDistance = cavalry.QuerySystem.AveragePosition.Distance(
            target.QuerySystem.AveragePosition);
        
        if (chargeDistance < MinChargeDistance)
            effectiveness *= chargeDistance / MinChargeDistance;  // Scale with distance
        
        // Check cavalry speed
        float speed = cavalry.QuerySystem.MovementSpeed;
        if (speed < 3.0f)  // Walking/slow trot
            effectiveness *= 0.3f;  // Need speed for lance impact
        
        return effectiveness;
    }
    
    bool IsTargetBracedAgainst(Formation cavalry, Formation target)
    {
        // Infantry in shield wall or square formation, facing cavalry
        if (!target.QuerySystem.IsInfantryFormation)
            return false;
        
        if (target.ArrangementOrder.OrderType != ArrangementOrder.ArrangementOrderEnum.ShieldWall
            && target.ArrangementOrder.OrderType != ArrangementOrder.ArrangementOrderEnum.Square)
            return false;
        
        // Check if they're facing us
        Vec2 toUs = (cavalry.QuerySystem.AveragePosition - target.QuerySystem.AveragePosition).Normalized();
        Vec2 theirFacing = target.Direction;
        float facingDot = Vec2.DotProduct(toUs, theirFacing);
        
        return facingDot > 0.7f;  // Facing us
    }
}
```

### Weapon-Aware Cavalry Behavior

```csharp
void AdjustCavalryBehaviorByWeapons(Formation cavalry)
{
    // Check what weapons cavalry actually have
    float lanceRatio = CalculateLanceRatio(cavalry);
    float meleeWeaponRatio = 1.0f - lanceRatio;
    
    if (lanceRatio > 0.6f)
    {
        // Lance-heavy: SHORT melee time, MUST cycle charge
        MaxMeleeTime = 10f;
        MinChargeDistance = 70f;
    }
    else if (lanceRatio > 0.3f)
    {
        // Mixed: Moderate melee capability
        MaxMeleeTime = 15f;
        MinChargeDistance = 60f;
    }
    else
    {
        // Melee cavalry (swords/axes): Can sustain combat longer
        MaxMeleeTime = 25f;
        MinChargeDistance = 40f;
    }
}

float CalculateLanceRatio(Formation formation)
{
    int totalUnits = formation.CountOfUnits;
    int lanceCount = 0;
    
    foreach (Agent agent in formation.GetUnitsWithoutDetachedOnes())
    {
        // Check for lance/spear in equipment
        for (int i = 0; i < 4; i++)
        {
            MissionWeapon weapon = agent.Equipment[i];
            if (weapon.Item != null && IsLanceWeapon(weapon.Item))
            {
                lanceCount++;
                break;
            }
        }
    }
    
    return totalUnits > 0 ? (float)lanceCount / totalUnits : 0f;
}

bool IsLanceWeapon(ItemObject item)
{
    if (item.PrimaryWeapon == null)
        return false;
    
    WeaponClass weaponClass = item.PrimaryWeapon.WeaponClass;
    
    // Lances and long spears
    return weaponClass == WeaponClass.Lance 
        || (weaponClass == WeaponClass.OneHandedPolearm && item.PrimaryWeapon.WeaponLength > 200);
}
```

### Formation Cohesion During Cycle

```csharp
bool IsFormationReformed(Formation cavalry)
{
    // Check if cavalry is tight and ready
    float cohesion = CalculateCohesion(cavalry);
    
    // Need 80%+ cohesion to charge effectively
    return cohesion > 0.8f;
}

float CalculateCohesion(Formation cavalry)
{
    // How tight is the formation?
    Vec2 center = cavalry.QuerySystem.AveragePosition;
    float avgDistance = 0f;
    int count = 0;
    
    foreach (Agent agent in cavalry.GetUnitsWithoutDetachedOnes())
    {
        avgDistance += agent.Position.AsVec2.Distance(center);
        count++;
    }
    
    avgDistance /= count;
    
    // Cohesion: Tighter = higher cohesion
    // 0m = 1.0 cohesion, 50m+ = 0.0 cohesion
    return MathF.Clamp(1.0f - (avgDistance / 50f), 0f, 1f);
}
```

### Integration with Orchestrator

```csharp
class BattleOrchestrator
{
    Dictionary<Formation, CavalryCycleManager> CavalryManagers = new();
    
    void ManageCavalry(BattleState state)
    {
        foreach (var cavalry in state.OurCavalry)
        {
            if (!CavalryManagers.ContainsKey(cavalry))
                CavalryManagers[cavalry] = new CavalryCycleManager();
            
            CavalryManagers[cavalry].TickCavalryCycle(cavalry, state);
        }
    }
}
```

### Summary: Cavalry Cycle Charging Doctrine

| Phase | Duration | Formation | Movement | Purpose |
|-------|----------|-----------|----------|---------|
| **Reserve** | Variable | Line | Stop | Wait for opportunity |
| **Positioning** | 10-20s | Line | Move | Get proper charge distance (60-80m) |
| **Charging** | 5-10s | Wedge | Charge | Build speed, lances ready |
| **Impact** | 1-3s | Wedge | Charge | Lance impact, maximum damage |
| **Melee** | 8-12s | Loose | Melee | Finish off immediate threats |
| **Disengaging** | 5-10s | Loose | Move | Break contact, move away |
| **Rallying** | 10-15s | Loose | Move | Move to rally point |
| **Reforming** | 8-12s | Line | Stop | Tighten formation, prepare for next charge |

**Total cycle time**: 50-80 seconds  
**Effective lance charges per battle**: 3-6 (vs native's 1)

---

## 10.7 Reserve Commitment — Collapse Their Line

**"Should we commit reserve to the right flank and try to collapse their line?"**

```csharp
class ReserveCommitmentDecision
{
    bool ShouldCommitReserveToCollapse(Formation reserve, Flank targetFlank, BattleState state)
    {
        // Is there actually a weak point?
        float flankStrength = AssessFlankStrength(targetFlank);
        if (flankStrength > StrongThreshold)
            return false;  // Not weak enough
        
        // Can we actually collapse it?
        float ourStrength = reserve.Power;
        float requiredStrength = flankStrength * 1.5f;  // Need 1.5x to collapse
        if (ourStrength < requiredStrength)
            return false;  // Not strong enough
        
        // What happens to OUR line if we commit?
        if (OurMainLineUnderPressure() && reserve.IsOnlyReserve)
            return false;  // Can't afford to commit everything
        
        // Is this a decisive moment?
        if (EnemyReservesExhausted() && WeHaveMomentum())
            return true;  // Push for the win
        
        // Risk/reward assessment
        float riskOfFailure = CalculateCollapseRisk(reserve, targetFlank);
        float rewardOfSuccess = CalculateCollapseReward(targetFlank);
        
        return rewardOfSuccess > riskOfFailure * 1.5f;  // Need good odds
    }
    
    float AssessFlankStrength(Flank flank)
    {
        float strength = 0;
        
        foreach (var formation in flank.Formations)
        {
            strength += formation.Power;
            
            // Bonus for defensive formations
            if (formation.ArrangementOrder == ArrangementOrder.ShieldWall)
                strength *= 1.2f;
            
            // Penalty for already engaged/damaged
            if (formation.IsEngaged)
                strength *= 0.8f;
            if (formation.CasualtyRatio > 0.3f)
                strength *= 0.7f;
        }
        
        return strength;
    }
}
```

---

## 10.8 Pursuit Decisions

**"Are they retreating? What's our casualties? Should we pursue?"**

```csharp
class PursuitDecision
{
    enum PursuitAction { FullPursuit, CavalryOnly, HoldPosition, Regroup }
    
    PursuitAction DecidePursuit(BattleState state)
    {
        if (!state.EnemyRetreating)
            return PursuitAction.HoldPosition;
        
        // Our condition
        float ourCasualties = state.OurCasualtyRatio;
        float ourMoraleRatio = state.OurMorale;  // No fatigue system in native
        bool ourMoraleStable = state.OurMorale > 0.5f;
        
        // Enemy condition
        float enemyRemaining = state.EnemyRemainingRatio;
        bool enemyRouting = state.EnemyMorale < 0.2f;
        
        // Reinforcement situation
        float distanceToOurReinforcements = state.DistanceToOurSpawn;
        float distanceToTheirReinforcements = state.DistanceToEnemySpawn;
        bool theyHaveReinforcementsComing = state.EnemyReinforcementsIncoming;
        
        // Don't chase into reinforcements
        if (theyHaveReinforcementsComing && distanceToTheirReinforcements < 100f)
            return PursuitAction.HoldPosition;
        
        // We're hurt — don't push our luck
        if (ourCasualties > 0.4f)
        {
            if (enemyRouting)
                return PursuitAction.CavalryOnly;  // Let cavalry mop up
            else
                return PursuitAction.Regroup;
        }
        
        // They're routing and no reinforcements coming — crush them
        if (enemyRouting && !theyHaveReinforcementsComing)
            return PursuitAction.FullPursuit;
        
        // We're farther from safety than they are — limited pursuit
        if (distanceToOurReinforcements > distanceToTheirReinforcements)
            return PursuitAction.CavalryOnly;
        
        // Default: cautious cavalry pursuit
        return PursuitAction.CavalryOnly;
    }
}
```

---

## 10.9 Future Gating (Optional)

Currently all AI uses the full decision engine. Later, we can gate by:

| Factor | Effect |
|--------|--------|
| **Bandits** | Simpler tactics, less coordination |
| **Troop count** | Small parties = simpler decisions |
| **Lord skill** | Tactics skill affects decision quality |
| **Culture** | Some cultures more aggressive/defensive |

```csharp
// Future implementation
float GetDecisionQuality(PartyBase party)
{
    if (party.IsBandit)
        return 0.5f;  // Bandits are dumber
    
    if (party.MemberRoster.Count < 20)
        return 0.7f;  // Small parties less sophisticated
    
    float tacticsSkill = party.Leader?.GetSkillValue(DefaultSkills.Tactics) ?? 100f;
    return MathF.Min(1.0f, tacticsSkill / 200f);
}
```

---

## Decision Summary

| Decision | Key Factors |
|----------|-------------|
| **Threat vs Opportunity** | What's killing us vs what we can exploit. Time cost of actions. |
| **Archer Targeting** | Enemy armor, tier, skill. Who wins the duel? Who threatens our line more? |
| **Cavalry Timing** | Hold until clear opportunity. Don't waste on low-value charges. |
| **Reserve Commitment** | Can we collapse? What happens to our line? Is this decisive? |
| **Pursuit** | Our casualties/morale. Their reinforcements. Distance to safety. |

---

# Part 11: Agent Formation Behavior

This section covers how agents behave within their formations — cohesion, casualty responses, and when formations fall back vs fight to the death.

## 11.1 Formation Cohesion Levels

Agents prioritize staying in formation. Cohesion level is **context-aware**:

| Situation | Cohesion Level | Behavior |
|-----------|----------------|----------|
| **Enemy in formation** | **Tight** | Never break ranks |
| **Enemy scattered/routing** | **Moderate** | Can pursue within 15m of position |
| **Surrounded** | **Tight** | Stay together or die |

```csharp
enum CohesionLevel { Tight, Moderate, Loose }

CohesionLevel DetermineCohesion(Formation formation, BattleState state)
{
    bool enemyOrganized = state.EnemyFormations.Any(f => f.IsOrganized);
    bool enemyScattered = state.EnemyFormations.All(f => f.IsRouting || f.IsBroken);
    bool surrounded = formation.QuerySystem.LocalEnemyRatio > 2.0f;
    
    if (surrounded)
        return CohesionLevel.Tight;  // Stay together
    
    if (enemyScattered)
        return CohesionLevel.Moderate;  // Can pursue
    
    return CohesionLevel.Tight;  // Default: hold formation
}

void EnforceCohesion(Agent agent, CohesionLevel level)
{
    float maxDeviation = level switch
    {
        CohesionLevel.Tight => 5f,      // Very close to position
        CohesionLevel.Moderate => 15f,  // Some freedom
        CohesionLevel.Loose => 30f,     // Pursuit mode
        _ => 5f
    };
    
    float distanceFromPosition = agent.Position.Distance(agent.FormationPosition);
    if (distanceFromPosition > maxDeviation)
    {
        // Pull back to formation, don't chase
        agent.SetBehaviorValueSet(BehaviorValueSet.DefensiveArrangementMove);
    }
}
```

---

## 11.2 Formation-Level Casualty Decisions

**Fallback is a FORMATION decision, not individual.** The formation commander decides based on casualties.

```csharp
enum FormationPosture 
{ 
    Engaged,           // Normal combat
    FightingRetreat,   // Pull back while fighting
    TacticalWithdrawal,// Fall back for reinforcements
    LastStand          // Fight to the death
}

class FormationCasualtyManager
{
    FormationPosture CurrentPosture = FormationPosture.Engaged;
    
    FormationPosture DecidePosture(Formation formation, BattleState state)
    {
        float casualtyRatio = formation.CasualtyRatio;
        float recentCasualtyRate = formation.CasualtiesInLast60Seconds / formation.InitialCount;
        bool reinforcementsAvailable = state.ReinforcementsRemaining > 0;
        bool canDisengage = !IsSurrounded(formation);
        
        // 60%+ casualties — decision point
        if (casualtyRatio > 0.6f)
        {
            if (reinforcementsAvailable && canDisengage)
                return FormationPosture.TacticalWithdrawal;
            else
                return FormationPosture.LastStand;
        }
        
        // 30%+ casualties AND bleeding fast
        if (casualtyRatio > 0.3f && recentCasualtyRate > 0.1f)
        {
            if (canDisengage)
                return FormationPosture.FightingRetreat;
            else
                return FormationPosture.Engaged;
        }
        
        return FormationPosture.Engaged;
    }
}
```

---

## 11.3 Fight to Death vs Tactical Retreat

At high casualties, the formation reaches a **decision point**:

```
                    CASUALTIES > 60%
                           │
                           ▼
            ┌──────────────────────────────┐
            │  CAN WE DISENGAGE SAFELY?    │
            └──────────────────────────────┘
                    │              │
                   YES             NO
                    │              │
                    ▼              ▼
    ┌─────────────────────┐  ┌─────────────────────┐
    │ REINFORCEMENTS      │  │ LAST STAND          │
    │ AVAILABLE?          │  │ Fight to the death  │
    └─────────────────────┘  │ Maximize enemy      │
           │       │         │ casualties          │
          YES      NO        └─────────────────────┘
           │       │
           ▼       ▼
    ┌──────────┐ ┌──────────────────────┐
    │ FALL BACK│ │ HOLD POSITION        │
    │ Get fresh│ │ No point retreating  │
    │ troops   │ │ without reinforcement│
    └──────────┘ └──────────────────────┘
```

### Strategic Positioning — The Real Advantage

**Having the battle near YOUR spawn is GOOD:**
- Your reinforcements spawn and immediately join the fight
- Enemy reinforcements have to march across the battlefield
- You get constant fresh troops; they get trickles

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   [YOUR SPAWN]──(close)──[BATTLE]──────(far)──────[THEIR SPAWN]│
│        ↑                    ↑                          ↑        │
│   Your reinforcements   IDEAL BATTLE              Don't push   │
│   arrive quickly        POSITION                  to here!     │
│                                                                 │
│   ✅ Your troops reinforce fast                                │
│   ✅ Their troops have to march                                │
│   ❌ Don't push to THEIR spawn (their reinforcements instant)  │
│                                                                 │
│   EXCEPTION: If they have NO reinforcements, push all the way! │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Falling back toward your spawn = Luring them into your reinforcement zone:**
- ✅ Pulls the fight closer to YOUR reinforcements
- ✅ Pulls the fight AWAY from their reinforcements  
- ✅ Their dead respawn far away and have to march
- ✅ Your dead respawn and immediately rejoin

```csharp
bool ShouldFallBackForReinforcements(Formation formation, BattleState state)
{
    if (formation.CasualtyRatio < 0.4f)
        return false;  // Not hurt enough
    
    if (state.ReinforcementsRemaining <= 0)
        return false;  // No reinforcements anyway
    
    if (IsSurrounded(formation))
        return false;  // Can't escape
    
    // Key insight: We WANT the battle near OUR spawn
    float distanceToOurSpawn = formation.Position.Distance(state.OurSpawnPoint);
    
    // If we're far from our spawn, falling back is GOOD
    // It pulls the enemy into our reinforcement zone
    if (distanceToOurSpawn > 100f)
        return true;  // Fall back toward reinforcements
    
    // We're already near our spawn — HOLD THE LINE
    // Reinforcements are pouring in, this is the ideal position
    return false;
}

bool ShouldPushForward(Formation formation, BattleState state)
{
    // Only push if enemy has NO reinforcements
    if (state.EnemyReinforcementsRemaining > 0)
    {
        // Don't push to their spawn — their reinforcements would arrive instantly
        float enemyDistanceToTheirSpawn = state.Enemy.Position.Distance(state.EnemySpawnPoint);
        if (enemyDistanceToTheirSpawn < 80f)
            return false;  // They're near their spawn, don't chase
    }
    else
    {
        // They have no reinforcements — push all the way, finish them
        return true;
    }
    
    return state.WeHaveAdvantage;
}
```

### Fighting Retreat — Luring Them In

When falling back, don't run — **fighting retreat** drags them toward your spawn:

```csharp
void ExecuteFightingRetreat(Formation formation, BattleState state)
{
    // Fall back SLOWLY while fighting
    // Goal: Drag enemy toward our spawn
    
    formation.SetArrangementOrder(ArrangementOrder.ShieldWall);
    formation.SetFacingOrder(FacingOrder.LookAtEnemy);
    
    // Move toward our spawn, but slowly
    Vec2 retreatDirection = (state.OurSpawnPoint - formation.Position).Normalized();
    Vec2 retreatPosition = formation.Position + retreatDirection * 20f;
    
    formation.SetMovementOrder(MovementOrder.Move(retreatPosition));
    
    // Archers continue firing during retreat
    // Cavalry screens the retreat
}
```

---

## 11.4 Attacker vs Defender Roles

**Battle context determines who must act:**

| Role | Responsibility | Behavior |
|------|----------------|----------|
| **Attacker** | Initiated the fight | MUST attack, cannot sit and wait |
| **Defender** | Was attacked | CAN hold position, defend |

```csharp
enum BattleRole { Attacker, Defender }

class BattleRoleManager
{
    BattleRole GetRole(Team team, BattleState state)
    {
        // The party that initiated the battle is the attacker
        return state.InitiatingParty.Team == team 
            ? BattleRole.Attacker 
            : BattleRole.Defender;
    }
    
    void ApplyRoleBehavior(Team team, BattleRole role, BattleState state)
    {
        switch (role)
        {
            case BattleRole.Attacker:
                // Attackers MUST press the attack
                // They started this fight, they need to finish it
                team.Orchestrator.SetMinimumAggression(0.6f);
                break;
                
            case BattleRole.Defender:
                // Defenders CAN hold, but should counter-attack on advantage
                if (DetectClearAdvantage(team, state))
                {
                    team.Orchestrator.SetPosture(BattlePosture.CounterAttack);
                }
                else
                {
                    team.Orchestrator.SetPosture(BattlePosture.Defensive);
                }
                break;
        }
    }
    
    bool DetectClearAdvantage(Team defender, BattleState state)
    {
        // Defender should counter-attack if:
        // 1. Enemy formation is broken/routing
        // 2. Enemy exposed a flank
        // 3. Enemy cavalry is neutralized and we have cavalry
        // 4. Significant power advantage developed
        
        bool enemyBroken = state.EnemyFormations.Any(f => f.IsBroken);
        bool enemyFlankExposed = DetectExposedFlank(state.EnemySide);
        bool cavalryAdvantage = defender.CavalryPower > state.EnemySide.CavalryPower * 1.5f;
        bool powerAdvantage = defender.TotalPower > state.EnemySide.TotalPower * 1.3f;
        
        return enemyBroken || enemyFlankExposed || cavalryAdvantage || powerAdvantage;
    }
}
```

---

## 11.5 Stalemate Prevention

**Someone must attack for gameplay's sake.** No sitting and staring.

```csharp
class StalemateBreaker
{
    float StalemateTimer = 0f;
    const float MaxStalemateDuration = 45f;  // 45 seconds max
    
    void CheckStalemate(BattleState state)
    {
        bool bothStationary = !state.PlayerSide.IsAdvancing && !state.EnemySide.IsAdvancing;
        bool noRecentCombat = state.TimeSinceLastCasualty > 20f;
        bool noManeuvering = !AnyFormationMoving(state);
        
        if (bothStationary && noRecentCombat && noManeuvering)
        {
            StalemateTimer += deltaTime;
            
            if (StalemateTimer > MaxStalemateDuration)
            {
                BreakStalemate(state);
            }
        }
        else
        {
            StalemateTimer = 0f;  // Reset on any activity
        }
    }
    
    void BreakStalemate(BattleState state)
    {
        // Priority: Attacker attacks (they started this)
        Team attacker = GetAttackingTeam(state);
        
        if (attacker != null)
        {
            attacker.Orchestrator.ForceAdvance();
            return;
        }
        
        // If no clear attacker, whoever has advantage
        Team stronger = DetermineStrongerSide(state);
        stronger.Orchestrator.ForceAdvance();
    }
    
    Team DetermineStrongerSide(BattleState state)
    {
        // Power advantage
        if (state.PlayerSide.TotalPower > state.EnemySide.TotalPower * 1.2f)
            return state.PlayerSide.Team;
        if (state.EnemySide.TotalPower > state.PlayerSide.TotalPower * 1.2f)
            return state.EnemySide.Team;
        
        // Cavalry advantage = should attack (mobility)
        if (state.EnemySide.CavalryRatio > state.PlayerSide.CavalryRatio + 0.15f)
            return state.EnemySide.Team;
        if (state.PlayerSide.CavalryRatio > state.EnemySide.CavalryRatio + 0.15f)
            return state.PlayerSide.Team;
        
        // More reinforcements = less to lose per casualty
        if (state.EnemySide.ReinforcementsRemaining > state.PlayerSide.ReinforcementsRemaining * 1.5f)
            return state.EnemySide.Team;
        
        // Default: attacker side attacks
        return state.InitiatingParty.Team;
    }
}
```

**Maneuvering is NOT a stalemate:**
- Cavalry probing flanks ✅
- Archers repositioning ✅
- Infantry adjusting line ✅
- Skirmishing at range ✅

Only **both sides stationary AND no combat** triggers the timer.

---

## 11.6 Player Formation Exception (T7+)

When the player commands their own formation (T7-T9 rank), that formation follows **player orders**, not AI logic:

| Behavior | Player Formation | AI Formations |
|----------|------------------|---------------|
| **Tight cohesion** | ✅ Yes | ✅ Yes |
| **Auto fallback on casualties** | ❌ No — player decides | ✅ Yes |
| **Smart disengagement** | ❌ No — player decides | ✅ Yes |
| **Casualty-based retreat** | ❌ No — player decides | ✅ Yes |

```csharp
bool ShouldApplyAIFormationBehavior(Formation formation)
{
    // Player's own formation — they command it
    if (IsPlayerFormation(formation))
        return false;
    
    // All other formations get AI behavior
    return true;
}

bool IsPlayerFormation(Formation formation)
{
    // Player is T7+ and this is their commanded formation
    if (EnlistedRank < 7)
        return false;  // Player doesn't command a formation yet
    
    return formation.ContainsAgent(PlayerLord);
}
```

**Why?** The player is the commander. They decide when to advance, retreat, or fight to the death. AI agents in the player's formation follow orders, not survival instincts.

---

## 11.7 Exploiting Enemy Disorder

When the enemy retreats or becomes disordered, it's an opportunity — but exploit it **without losing your own discipline**.

### Recognizing Enemy Disorder

```csharp
class DisorderDetector
{
    bool IsEnemyDisordered(Formation enemyFormation)
    {
        float cohesion = enemyFormation.QuerySystem.FormationIntegrity;
        bool isRetreating = enemyFormation.IsRetreating;
        bool isMoving = enemyFormation.IsMoving && !enemyFormation.IsFacingUs;
        
        return cohesion < 0.5f || isRetreating || isMoving;
    }
}
```

### Exploit With Discipline

```
   WRONG:                              RIGHT:
   ┌─────────┐  Enemy retreats        ┌─────────┐
   │ ▪▪▪▪▪▪▪ │  ──────────────►       │ ▪▪▪▪▪▪▪ │  FORMATION
   │ ▪▪▪▪▪▪▪ │  You chase as blob     │ ▪▪▪▪▪▪▪ │  ADVANCE
   └─────────┘                        └─────────┘
        ↓                                  ↓
   │ ▪  ▪ ▪  │  ← DISORDERED          │ ▪▪▪▪▪▪▪ │  ← ORGANIZED
   │▪ ▪  ▪▪  │                        │ ▪▪▪▪▪▪▪ │
   
   ❌ You lose formation too          ✅ You stay organized
   ❌ Fast troops get isolated        ✅ Cavalry hits flanks
   ❌ Vulnerable to counter           ✅ Infantry catches stragglers
```

### Exploitation Logic

```csharp
class DisorderExploitation
{
    void ExploitDisorder(Formation disorderedEnemy, BattleState state)
    {
        // Cavalry: Charge their rear/flanks — they can't defend
        if (OurCavalry != null && !OurCavalry.IsEngaged)
        {
            OurCavalry.SetMovementOrder(MovementOrder.Charge);
            OurCavalry.SetTargetFormation(disorderedEnemy);
        }
        
        // Infantry: ADVANCE IN FORMATION, don't chase
        foreach (var infantry in OurInfantry)
        {
            infantry.SetMovementOrder(MovementOrder.Advance);
            infantry.SetArrangementOrder(ArrangementOrder.Line);
            // Stay organized, let them come to you
        }
        
        // Archers: Target fleeing troops — easy kills
        foreach (var archers in OurArchers)
        {
            archers.SetTargetFormation(disorderedEnemy);
            archers.SetFiringOrder(FiringOrder.FireAtWill);
        }
    }
}
```

### Pursuit Modes

| Mode | When | Infantry | Cavalry | Archers |
|------|------|----------|---------|---------|
| **Hold** | Enemy reforming | Stop | Hold | Fire |
| **Formation Advance** | Enemy moving | Advance in line | Hold for opportunity | Fire |
| **Cavalry Pursuit** | Enemy retreating | Advance in formation | Charge | Fire |
| **Full Pursuit** | Enemy routing, no reserves | Charge | Charge | Advance and fire |

### Don't Become Disordered Yourself

```csharp
void MaintainDisciplineDuringPursuit(Formation ourFormation)
{
    float ourCohesion = ourFormation.QuerySystem.FormationIntegrity;
    
    if (ourCohesion < 0.6f)
    {
        // We're getting strung out — STOP and reform
        ourFormation.SetMovementOrder(MovementOrder.Stop);
        ourFormation.SetArrangementOrder(ArrangementOrder.Line);
        
        // Don't sacrifice your order for pursuit
    }
}
```

---

## 11.8 Pike/Spear Infantry Weapon Discipline

**"Pikemen must keep their pikes out and use formation reach advantage - don't switch to sidearms prematurely"**

Pike and spear infantry are devastating **at reach** but vulnerable if they switch to swords. Native AI switches to sidearms far too early, throwing away their primary advantage.

### The Problem with Native Pike AI

```
NATIVE AI:
┌──────────────────────────────────────────────────────────────┐
│  Enemy approaches (30m away)                                 │
│  → Pikemen switch to swords "just in case"                  │
│  → Enemy arrives, pikemen fight with swords                 │
│  → Pikes are NEVER used effectively                         │
└──────────────────────────────────────────────────────────────┘

PROPER PIKE DOCTRINE:
┌──────────────────────────────────────────────────────────────┐
│  Enemy approaches → Keep pike out and braced                │
│  Enemy at 3-6m → Pike thrusts, enemy cannot reach           │
│  Enemy inside 2m → Formation holds, DO NOT SWITCH           │
│  Enemy literally IN formation → Only then switch to sidearm │
└──────────────────────────────────────────────────────────────┘
```

### Pike Weapon Range Zones

```csharp
enum WeaponRange
{
    PikeSuperiority,    // 3.0m - 6.0m: Pike dominates
    PikeEffective,      // 2.0m - 3.0m: Pike still effective
    Contested,          // 1.5m - 2.0m: Both weapons can hit
    SidearmRequired     // 0.0m - 1.5m: Enemy inside pike reach
}

class PikeWeaponManager
{
    const float PikeLength = 5.5f;           // Average pike length
    const float LongSpearLength = 3.5f;      // Long spear
    const float SidearmSwitchDistance = 1.3f; // Only switch when THIS close
    const float RearmPikeDistance = 3.0f;     // Switch back to pike when enemy this far
}
```

### Agent-Level Pike Discipline

```csharp
void ManagePikeWeaponChoice(Agent agent, Formation formation)
{
    // Only manage if agent HAS a pike/long spear
    if (!HasPikeWeapon(agent))
        return;
    
    EquipmentIndex currentWeapon = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
    bool hasPikeOut = IsPikeEquipped(agent, currentWeapon);
    
    // Find closest enemy
    Agent closestEnemy = FindClosestEnemy(agent);
    if (closestEnemy == null)
    {
        // No immediate threat - keep pike out
        if (!hasPikeOut)
            SwitchToPike(agent);
        return;
    }
    
    float distanceToEnemy = agent.Position.Distance(closestEnemy.Position);
    
    // DECISION LOGIC
    if (hasPikeOut)
    {
        // Currently wielding pike
        // ONLY switch to sidearm if enemy is INSIDE pike reach
        if (distanceToEnemy < SidearmSwitchDistance)
        {
            // Enemy is literally in your face - switch
            if (HasSidearm(agent))
                SwitchToSidearm(agent);
        }
        else
        {
            // Keep pike out - this is your advantage
            // (Native AI would switch here - DON'T)
        }
    }
    else
    {
        // Currently wielding sidearm
        // Switch BACK to pike when enemy is at proper distance
        if (distanceToEnemy > RearmPikeDistance)
        {
            SwitchToPike(agent);
        }
    }
}

bool HasPikeWeapon(Agent agent)
{
    for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; 
         i < EquipmentIndex.NumAllWeaponSlots; i++)
    {
        MissionWeapon weapon = agent.Equipment[i];
        if (!weapon.IsEmpty && IsPolearmWeapon(weapon) && weapon.GetModifiedItemLength() > 250)
            return true;
    }
    return false;
}

bool IsPolearmWeapon(MissionWeapon weapon)
{
    WeaponClass weaponClass = weapon.CurrentUsageItem.WeaponClass;
    return weaponClass == WeaponClass.TwoHandedPolearm 
        || weaponClass == WeaponClass.OneHandedPolearm
        || weaponClass == WeaponClass.LowGripPolearm;
}

void SwitchToPike(Agent agent)
{
    // Find pike in equipment
    for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; 
         i < EquipmentIndex.NumAllWeaponSlots; i++)
    {
        MissionWeapon weapon = agent.Equipment[i];
        if (!weapon.IsEmpty && IsPolearmWeapon(weapon) && weapon.GetModifiedItemLength() > 250)
        {
            agent.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.Instant);
            agent.TryToWieldWeaponInSlot(i, Agent.WeaponWieldActionType.Instant, false);
            break;
        }
    }
}

void SwitchToSidearm(Agent agent)
{
    // Find shortest melee weapon (sword/axe/mace)
    EquipmentIndex shortestWeapon = EquipmentIndex.None;
    float shortestLength = float.MaxValue;
    
    for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; 
         i < EquipmentIndex.NumAllWeaponSlots; i++)
    {
        MissionWeapon weapon = agent.Equipment[i];
        if (!weapon.IsEmpty && weapon.CurrentUsageItem.IsMeleeWeapon)
        {
            float length = weapon.GetModifiedItemLength();
            if (length < 200 && length < shortestLength)  // Not a pike
            {
                shortestLength = length;
                shortestWeapon = i;
            }
        }
    }
    
    if (shortestWeapon != EquipmentIndex.None)
    {
        agent.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.Instant);
        agent.TryToWieldWeaponInSlot(shortestWeapon, Agent.WeaponWieldActionType.Instant, false);
    }
}
```

### Formation-Level Pike Doctrine

```csharp
class PikeFormationManager
{
    void EnforcePikeFormationDiscipline(Formation formation)
    {
        // Only applies to pike-heavy formations
        float pikeRatio = CalculatePikeRatio(formation);
        if (pikeRatio < 0.4f)
            return;  // Not a pike formation
        
        // Pike formations MUST maintain cohesion
        // Loose formation = pikes ineffective
        if (formation.ArrangementOrder.OrderType == ArrangementOrder.ArrangementOrderEnum.Loose)
        {
            // Tighten up - pikes need density
            formation.SetArrangementOrder(ArrangementOrder.ArrangementOrder.Line);
        }
        
        // Check each agent's weapon choice
        foreach (Agent agent in formation.GetUnitsWithoutDetachedOnes())
        {
            if (agent.IsAIControlled)
                ManagePikeWeaponChoice(agent, formation);
        }
        
        // Formation facing discipline
        // Pikes must face the threat, not turn sideways
        if (formation.HasEnemyInRange(10f))
        {
            formation.SetFacingOrder(FacingOrder.FacingOrderLookAtEnemy);
        }
    }
    
    float CalculatePikeRatio(Formation formation)
    {
        int pikeCount = 0;
        int totalUnits = formation.CountOfUnits;
        
        foreach (Agent agent in formation.GetUnitsWithoutDetachedOnes())
        {
            if (HasPikeWeapon(agent))
                pikeCount++;
        }
        
        return totalUnits > 0 ? (float)pikeCount / totalUnits : 0f;
    }
}
```

### Anti-Cavalry Pike Bracing

```csharp
void BracePikesAgainstCavalry(Formation pikeFormation, Formation enemyCavalry)
{
    // Distance check
    float distance = pikeFormation.QuerySystem.AveragePosition.Distance(
        enemyCavalry.QuerySystem.AveragePosition);
    
    bool cavalryCharging = enemyCavalry.MovementOrder.OrderType == MovementOrder.MovementOrderEnum.Charge
        || enemyCavalry.QuerySystem.MovementSpeed > 5.0f;
    
    if (distance < 40f && cavalryCharging)
    {
        // Cavalry incoming - BRACE
        
        // 1. Tighten formation
        pikeFormation.SetArrangementOrder(ArrangementOrder.ArrangementOrder.Line);
        
        // 2. Face the cavalry
        Vec2 toCavalry = (enemyCavalry.QuerySystem.AveragePosition 
            - pikeFormation.QuerySystem.AveragePosition).Normalized();
        pikeFormation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(toCavalry));
        
        // 3. STOP moving - stand firm
        pikeFormation.SetMovementOrder(MovementOrder.MovementOrderStop);
        
        // 4. Ensure ALL agents have pikes out
        foreach (Agent agent in pikeFormation.GetUnitsWithoutDetachedOnes())
        {
            if (HasPikeWeapon(agent))
            {
                EquipmentIndex currentWeapon = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                if (!IsPikeEquipped(agent, currentWeapon))
                {
                    // Switch to pike NOW
                    SwitchToPike(agent);
                }
            }
        }
        
        // 5. Set defensive behavior
        pikeFormation.AI.SetBehaviorWeight<BehaviorDefend>(1.5f);
    }
}
```

### Pike vs Infantry Combat

```csharp
void ManagePikeVsInfantry(Formation pikeFormation, Formation enemyInfantry)
{
    // Against infantry, pikes dominate at reach
    // Stay in formation, thrust, don't let them close
    
    float distance = pikeFormation.QuerySystem.AveragePosition.Distance(
        enemyInfantry.QuerySystem.AveragePosition);
    
    if (distance < 15f && distance > 5f)
    {
        // Perfect pike range
        
        // Hold position - let them come to you
        pikeFormation.SetMovementOrder(MovementOrder.MovementOrderStop);
        pikeFormation.SetArrangementOrder(ArrangementOrder.ArrangementOrder.Line);
        
        // Face enemy
        pikeFormation.SetFacingOrder(FacingOrder.FacingOrderLookAtEnemy);
        
        // DO NOT CHARGE - pikes lose advantage when running
        // Native AI loves to charge here - WRONG
    }
    else if (distance < 5f)
    {
        // Enemy is close but not inside pike reach
        // HOLD THE LINE - this is still your advantage
        
        pikeFormation.SetMovementOrder(MovementOrder.MovementOrderStop);
        
        // Only if enemy is literally IN the formation (mixed melee)
        // do individual soldiers switch to sidearms
        // That's handled per-agent above
    }
    else
    {
        // Enemy far away
        // Can advance in formation if needed
        if (pikeFormation.Team.TeamAI.IsAttackingTeam)
        {
            pikeFormation.SetMovementOrder(MovementOrder.MovementOrderAdvance);
            pikeFormation.SetArrangementOrder(ArrangementOrder.ArrangementOrder.Line);
        }
    }
}
```

### Shield + Pike Combination

```csharp
void ManageShieldedPikemen(Agent agent)
{
    // Some troops have both shield and pike
    // Priority: Keep pike out, shields on back UNLESS under missile fire
    
    bool underMissileFire = agent.GetLastRangedHitTime() > Mission.Current.CurrentTime - 3f;
    bool hasShield = agent.HasShieldCached;
    bool hasPike = HasPikeWeapon(agent);
    
    if (hasPike && hasShield)
    {
        if (underMissileFire)
        {
            // Being shot at - raise shield, keep pike in other hand
            // (Native handles this automatically)
        }
        else
        {
            // Not under fire - pike takes priority
            // Shield on back, pike in hands
            EquipmentIndex currentWeapon = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            if (!IsPikeEquipped(agent, currentWeapon))
            {
                SwitchToPike(agent);
            }
        }
    }
}
```

### Integration with Formation AI

```csharp
class FormationAIExtension
{
    PikeFormationManager PikeManager = new();
    
    void OnTick(Formation formation)
    {
        // Check if this is a pike formation
        float pikeRatio = PikeManager.CalculatePikeRatio(formation);
        
        if (pikeRatio > 0.4f)
        {
            // This is a pike formation - apply pike doctrine
            PikeManager.EnforcePikeFormationDiscipline(formation);
            
            // Check for cavalry threats
            Formation enemyCavalry = FindClosestEnemyCavalry(formation);
            if (enemyCavalry != null)
            {
                float distance = formation.QuerySystem.AveragePosition.Distance(
                    enemyCavalry.QuerySystem.AveragePosition);
                
                if (distance < 40f)
                {
                    // Cavalry threat - brace
                    PikeManager.BracePikesAgainstCavalry(formation, enemyCavalry);
                }
            }
        }
    }
}
```

### Multi-Weapon Infantry (Javelins + Spears)

```csharp
class MultiWeaponInfantryManager
{
    // Many infantry have: Javelins (throwing) + Spear (melee) + Sword (backup)
    // Native AI often throws javelins then switches to sword, IGNORING the melee spear
    // Proper order: Javelins (far) → Melee Spear (medium) → Sword (close)
    
    void ManageMultiWeaponInfantry(Agent agent)
    {
        // Categorize agent's weapons
        WeaponLoadout loadout = AnalyzeWeaponLoadout(agent);
        
        if (!loadout.HasMultipleWeaponTypes)
            return;  // Simple loadout, native AI handles fine
        
        // Find closest enemy
        Agent closestEnemy = FindClosestEnemy(agent);
        if (closestEnemy == null)
        {
            // No threat - default to best melee weapon
            if (!IsWieldingBestMeleeWeapon(agent, loadout))
                SwitchToBestMeleeWeapon(agent, loadout);
            return;
        }
        
        float distance = agent.Position.Distance(closestEnemy.Position);
        EquipmentIndex currentWeapon = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
        
        // Decision tree based on distance and weapon availability
        if (distance > 15f && loadout.HasThrowingWeapon && loadout.ThrowingAmmoRemaining > 0)
        {
            // Throwing range - use javelins/throwing spears
            if (currentWeapon != loadout.ThrowingWeaponSlot)
                agent.TryToWieldWeaponInSlot(loadout.ThrowingWeaponSlot, 
                    Agent.WeaponWieldActionType.WithAnimation, false);
        }
        else if (distance > 1.5f && loadout.HasMeleePolearm)
        {
            // Melee range but not close combat - use melee spear/polearm
            // THIS IS THE KEY FIX - native AI skips this
            if (currentWeapon != loadout.MeleePolearmSlot)
                agent.TryToWieldWeaponInSlot(loadout.MeleePolearmSlot, 
                    Agent.WeaponWieldActionType.Instant, false);
        }
        else if (distance <= 1.5f && loadout.HasSidearm)
        {
            // Close combat - sword/axe/mace
            if (currentWeapon != loadout.SidearmSlot)
                agent.TryToWieldWeaponInSlot(loadout.SidearmSlot, 
                    Agent.WeaponWieldActionType.Instant, false);
        }
        else if (loadout.HasMeleePolearm)
        {
            // Fallback to melee polearm (no sidearm or other conditions)
            if (currentWeapon != loadout.MeleePolearmSlot)
                agent.TryToWieldWeaponInSlot(loadout.MeleePolearmSlot, 
                    Agent.WeaponWieldActionType.Instant, false);
        }
    }
    
    WeaponLoadout AnalyzeWeaponLoadout(Agent agent)
    {
        WeaponLoadout loadout = new WeaponLoadout();
        
        for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; 
             i < EquipmentIndex.NumAllWeaponSlots; i++)
        {
            MissionWeapon weapon = agent.Equipment[i];
            if (weapon.IsEmpty)
                continue;
            
            WeaponComponentData weaponData = weapon.CurrentUsageItem;
            WeaponClass weaponClass = weaponData.WeaponClass;
            int weaponLength = weaponData.WeaponLength;
            
            // Categorize weapon
            if (IsThrowingWeapon(weaponClass))
            {
                loadout.HasThrowingWeapon = true;
                loadout.ThrowingWeaponSlot = i;
                loadout.ThrowingAmmoRemaining = weapon.Amount;
            }
            else if (IsMeleePolearm(weaponClass, weaponLength))
            {
                loadout.HasMeleePolearm = true;
                loadout.MeleePolearmSlot = i;
                loadout.MeleePolearmLength = weaponLength;
            }
            else if (IsSidearmWeapon(weaponClass, weaponLength))
            {
                loadout.HasSidearm = true;
                loadout.SidearmSlot = i;
            }
        }
        
        loadout.HasMultipleWeaponTypes = 
            (loadout.HasThrowingWeapon ? 1 : 0) + 
            (loadout.HasMeleePolearm ? 1 : 0) + 
            (loadout.HasSidearm ? 1 : 0) >= 2;
        
        return loadout;
    }
    
    bool IsThrowingWeapon(WeaponClass weaponClass)
    {
        return weaponClass == WeaponClass.ThrowingAxe
            || weaponClass == WeaponClass.ThrowingKnife
            || weaponClass == WeaponClass.Javelin;
    }
    
    bool IsMeleePolearm(WeaponClass weaponClass, int weaponLength)
    {
        // Melee spears and polearms (not throwing, not lances)
        return (weaponClass == WeaponClass.TwoHandedPolearm
            || weaponClass == WeaponClass.OneHandedPolearm
            || weaponClass == WeaponClass.LowGripPolearm)
            && weaponLength >= 150  // Long enough to be effective melee polearm
            && weaponLength < 450;  // Not a pike (pikes are longer)
    }
    
    bool IsSidearmWeapon(WeaponClass weaponClass, int weaponLength)
    {
        // Swords, axes, maces - short melee weapons
        return weaponLength < 150 && (
            weaponClass == WeaponClass.OneHandedSword
            || weaponClass == WeaponClass.TwoHandedSword
            || weaponClass == WeaponClass.OneHandedAxe
            || weaponClass == WeaponClass.TwoHandedAxe
            || weaponClass == WeaponClass.Mace
            || weaponClass == WeaponClass.TwoHandedMace);
    }
}

class WeaponLoadout
{
    public bool HasThrowingWeapon;
    public EquipmentIndex ThrowingWeaponSlot;
    public int ThrowingAmmoRemaining;
    
    public bool HasMeleePolearm;
    public EquipmentIndex MeleePolearmSlot;
    public int MeleePolearmLength;
    
    public bool HasSidearm;
    public EquipmentIndex SidearmSlot;
    
    public bool HasMultipleWeaponTypes;
}
```

### Weapon Priority by Range

```
DISTANCE-BASED WEAPON SELECTION:

┌──────────────────────────────────────────────────────────────┐
│  20m+     │ Javelins/Throwing Spears (if ammo remaining)      │
├──────────────────────────────────────────────────────────────┤
│  15-20m   │ Switch to melee weapon (prepare for contact)      │
├──────────────────────────────────────────────────────────────┤
│  3-15m    │ MELEE SPEAR/POLEARM (reach advantage)            │
│           │ ← THIS IS WHAT NATIVE AI SKIPS                    │
├──────────────────────────────────────────────────────────────┤
│  1.5-3m   │ MELEE SPEAR (still effective)                     │
├──────────────────────────────────────────────────────────────┤
│  0-1.5m   │ Sword/Axe/Mace (close combat backup)             │
└──────────────────────────────────────────────────────────────┘
```

### Common Loadout Examples

**Example 1: Javelin + Spear + Sword**
```
Equipment:
- Slot 0: Javelin (throwing, 3x ammo)
- Slot 1: Spear (melee, 250cm)
- Slot 2: Sword (melee, 95cm)
- Slot 3: Shield

AI Behavior:
1. 25m: Throw javelins (ammo: 3 → 2 → 1 → 0)
2. 15m: Out of javelins, switch to SPEAR (not sword!)
3. 10m: Keep spear out, use reach advantage
4. 4m: Still spear (enemy cannot reach you)
5. 1m: Enemy inside spear reach, switch to sword
6. 3m: Enemy pushed back, switch back to spear
```

**Example 2: Javelin + Shield (no spear)**
```
Equipment:
- Slot 0: Javelin (throwing + melee, 5x ammo)
- Slot 1: Sword (melee, 85cm)
- Slot 2: Shield

AI Behavior:
1. 25m: Throw javelins (ammo: 5 → 4 → 3...)
2. 8m: Keep ONE javelin for melee (don't throw last one)
3. 5m: Use javelin as melee weapon (it's a spear when not thrown)
4. 1m: If overwhelmed, can switch to sword
```

**Example 3: Throwing Axe + Two-Handed Axe**
```
Equipment:
- Slot 0: Throwing Axes (3x ammo)
- Slot 1: Two-Handed Axe (melee, 120cm)

AI Behavior:
1. 20m: Throw axes (ammo: 3 → 2 → 1 → 0)
2. 10m: Switch to two-handed axe
3. Stay with two-handed axe (no polearm in loadout)
```

### Ammo Conservation

```csharp
bool ShouldKeepOneThrowingWeapon(Agent agent, WeaponLoadout loadout)
{
    // If throwing weapon can be used in melee AND agent has no melee polearm
    // Keep one for melee use instead of throwing all
    
    if (!loadout.HasThrowingWeapon || loadout.HasMeleePolearm)
        return false;  // Either no throwing weapon or has better melee option
    
    MissionWeapon throwingWeapon = agent.Equipment[loadout.ThrowingWeaponSlot];
    
    // Javelins can be used in melee
    if (throwingWeapon.CurrentUsageItem.WeaponClass == WeaponClass.Javelin)
    {
        // Keep one for melee if no other polearm
        return loadout.ThrowingAmmoRemaining <= 1 && !loadout.HasMeleePolearm;
    }
    
    return false;  // Throwing axes/knives are not good melee weapons
}
```

### Integration with Formation AI

```csharp
class FormationWeaponDiscipline
{
    MultiWeaponInfantryManager MultiWeaponManager = new();
    
    void EnforceWeaponDiscipline(Formation formation)
    {
        foreach (Agent agent in formation.GetUnitsWithoutDetachedOnes())
        {
            if (!agent.IsAIControlled)
                continue;
            
            // Pike/spear formations
            if (formation.IsPikeFormation)
            {
                PikeManager.ManagePikeWeaponChoice(agent, formation);
            }
            // Multi-weapon infantry (javelins + spears)
            else
            {
                MultiWeaponManager.ManageMultiWeaponInfantry(agent);
            }
        }
    }
}
```

### Summary: Pike Infantry Weapon Discipline

| Range | Pike Status | Behavior |
|-------|-------------|----------|
| **6m+** | Pike out | Advance in formation, pike ready |
| **3-6m** | Pike out | HOLD, thrust attacks, pike superiority |
| **2-3m** | Pike out | HOLD, enemy cannot reach you |
| **1.5-2m** | Pike out | Contested range, maintain formation |
| **<1.5m** | Switch to sidearm | Enemy inside pike reach |
| **>3m again** | Switch back to pike | Enemy retreated, re-establish reach advantage |

### Summary: Multi-Weapon Infantry Discipline

| Range | Weapon Priority | Rationale |
|-------|----------------|-----------|
| **20m+** | Javelins/Throwing | Ranged damage, soften enemy |
| **15-20m** | Prepare melee weapon | Throwing done or conserve last one |
| **3-15m** | Melee Spear/Polearm | **Reach advantage zone** |
| **1.5-3m** | Keep melee polearm | Still effective |
| **<1.5m** | Sword/Axe/Mace | Close combat backup |

**Key Principles:**
1. **Default weapon = Pike** (unless enemy is literally on top of you)
2. **Formation cohesion = Pike effectiveness** (loose formation = wasted pikes)
3. **Facing discipline** (pikes must face threat)
4. **Never charge with pikes** (lose formation = lose advantage)
5. **Brace against cavalry** (stop, face, pikes out, stand firm)
6. **Switch to sidearm ONLY when necessary** (< 1.5m enemy distance)
7. **Switch BACK to pike** when enemy withdraws (> 3m)
8. **Multi-weapon priority**: Throwing → Melee Polearm → Sidearm (NOT skip melee polearm!)
9. **Conserve last javelin** for melee use if no other polearm
10. **Distance-aware weapon selection** (don't keep throwing weapons out in melee)

**Result:**
- **Native AI**: Pikes barely used, switched to swords immediately; javelins thrown then sword used, melee spears ignored
- **New AI**: Pikes kept out, used at proper range, devastating in formation; proper weapon progression (throwing → melee polearm → sidearm)

---

## 11.9 Post-Victory Decisions

After defeating an enemy wave, **don't rush forward into their fresh reinforcements**. Regroup, assess, then decide.

### The Post-Victory Trap

```
┌─────────────────────────────────────────────────────────────────┐
│  You just won a fight near your spawn...                        │
│                                                                 │
│   [YOUR SPAWN]────[YOU (scattered)]────────────[THEIR SPAWN]   │
│        ↑              ↑                             ↑           │
│   Your reinforcements  Tired,                  FRESH WAVE      │
│   trickling in         disorganized            spawning NOW    │
│                                                                 │
│   ❌ Chase survivors → run into fresh wave                     │
│   ❌ Stand around disorganized → get hit by fresh wave         │
│   ✅ Regroup → Assess → Defend or Advance                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Post-Victory Decision Tree

```
                      WON THE FIGHT
                           │
                           ▼
               ┌───────────────────────┐
               │  ARE WE ORGANIZED?    │
               └───────────────────────┘
                    │           │
                   NO          YES
                    │           │
                    ▼           ▼
               ┌─────────┐  ┌────────────────────────┐
               │ REGROUP │  │ ENEMY REINFORCEMENTS?  │
               └─────────┘  └────────────────────────┘
                                │           │
                               YES          NO
                                │           │
                                ▼           ▼
               ┌─────────────────────┐  ┌─────────────────┐
               │ DEFENSIVE ADVANTAGE?│  │ FULL ADVANCE    │
               └─────────────────────┘  │ Finish them     │
                    │           │       └─────────────────┘
                   YES          NO
                    │           │
                    ▼           ▼
               ┌─────────┐  ┌─────────────────┐
               │ DEFEND  │  │ CAUTIOUS ADVANCE│
               │ HERE    │  │ or REGROUP      │
               └─────────┘  └─────────────────┘
```

### Post-Victory Logic

```csharp
class PostVictoryManager
{
    enum PostVictoryAction { Regroup, DefendHere, CautiousAdvance, FullAdvance }
    
    PostVictoryAction DecidePostVictory(BattleState state)
    {
        // Step 1: ALWAYS regroup first if disorganized
        if (!OurFormationsOrganized())
            return PostVictoryAction.Regroup;
        
        // Step 2: Assess enemy reinforcement situation
        bool enemyWaveIncoming = state.EnemyReinforcementsRemaining > 0;
        float timeUntilWave = EstimateTimeUntilEnemyReinforcements(state);
        
        // Step 3: Assess our position
        bool haveDefensiveAdvantage = DetectDefensiveAdvantage(state);
        bool nearOurSpawn = state.OurPosition.Distance(state.OurSpawnPoint) < 80f;
        
        // No more enemy reinforcements — push and finish
        if (!enemyWaveIncoming)
            return PostVictoryAction.FullAdvance;
        
        // Fresh wave coming SOON
        if (timeUntilWave < 30f)
        {
            if (haveDefensiveAdvantage || nearOurSpawn)
                return PostVictoryAction.DefendHere;
            else
                return PostVictoryAction.Regroup;
        }
        
        // Wave coming but we have time
        if (haveDefensiveAdvantage)
            return PostVictoryAction.DefendHere;
        else
            return PostVictoryAction.CautiousAdvance;
    }
    
    bool DetectDefensiveAdvantage(BattleState state)
    {
        bool nearOurSpawn = state.OurPosition.Distance(state.OurSpawnPoint) < 80f;
        bool archersInPosition = OurArchers?.IsInPosition == true;
        
        // Near our spawn = reinforcements fast = defensive advantage
        return nearOurSpawn || archersInPosition;
    }
    
    bool OurFormationsOrganized()
    {
        return OurFormations.All(f => f.QuerySystem.FormationIntegrity > 0.7f);
    }
}
```

### Regroup Phase

```csharp
void ExecuteRegroup()
{
    // Stop all pursuit
    foreach (var formation in OurFormations)
    {
        formation.SetMovementOrder(MovementOrder.Stop);
        formation.SetArrangementOrder(GetDefaultArrangement(formation));
        formation.SetFacingOrder(FacingOrder.LookAtEnemy);
    }
    
    // Collect stragglers
    foreach (var agent in ScatteredAgents)
    {
        agent.SetBehaviorValueSet(BehaviorValueSet.Follow);
    }
}
```

### Establish Defensive Position

```csharp
void EstablishDefensivePosition(BattleState state)
{
    // Infantry: Form defensive line facing enemy spawn
    foreach (var infantry in OurInfantry)
    {
        Vec2 faceDirection = (state.EnemySpawnPoint - infantry.Position).Normalized();
        infantry.SetFacingOrder(FacingOrder.LookAtDirection(faceDirection));
        
        if (infantry.HasEnoughShields())
            infantry.SetArrangementOrder(ArrangementOrder.ShieldWall);
        else
            infantry.SetArrangementOrder(ArrangementOrder.Line);
    }
    
    // Archers: Position behind infantry
    foreach (var archers in OurArchers)
    {
        Vec2 behindInfantry = GetPositionBehindFormation(OurInfantry.First(), 30f);
        archers.SetPositionTarget(behindInfantry);
    }
    
    // Cavalry: Hold on flanks, ready to counter-charge fresh wave
    foreach (var cavalry in OurCavalry)
    {
        cavalry.SetPosture(CavalryPosture.Reserve);
    }
}
```

---

## Part 11 Summary

| Principle | Implementation |
|-----------|----------------|
| **Tight cohesion** | Agents stay in formation unless enemy is scattered |
| **Fallback is formation-level** | Commander decides, not individuals |
| **Fight to death vs retreat** | Based on reinforcements + ability to disengage |
| **Strategic positioning** | Fight near YOUR spawn, far from theirs |
| **Attackers must attack** | They initiated the fight |
| **Defenders can hold** | But counter-attack on clear advantage |
| **No stalemates** | 45-second timer forces action |
| **Maneuvering is OK** | Movement resets stalemate timer |
| **Player formation exception** | T7+ player controls their formation |
| **Exploit disorder with discipline** | Cavalry charges, infantry advances in formation |
| **Regroup after victory** | Don't rush into fresh enemy wave |
| **Assess before advancing** | Defend if advantage, advance if enemy has no reserves |

---

# Part 12: Intelligent Formation Organization

Troops self-organize within formations — heavy/elite to the front, light/weak to the rear. When the line is stable, rear troops can spill to flanks. **Formation integrity is always the priority.**

## 12.1 Native API Support

Bannerlord provides the tools we need:

```csharp
// IFormationUnit properties (on every agent)
int FormationFileIndex { get; set; }   // Column position (left-right)
int FormationRankIndex { get; set; }   // Row position (front-back, 0 = front)

// Formation method to swap positions
formation.SwitchUnitLocations(agentA, agentB);

// Native already does this for shields (LineFormation.cs line 2292-2316):
// "If agent has shield and is behind agent without shield, swap them"
```

**Key insight**: Native code already promotes shielded units to the front. We extend this principle for armor and tier.

---

## 12.2 Front-Line Score Calculation

Score each agent's suitability for front-line duty:

```csharp
class FrontLineScorer
{
    float CalculateFrontLineScore(Agent agent)
    {
        float score = 0;
        
        // Tier matters
        score += agent.Character.Tier * 10f;
        
        // Armor matters A LOT for front line
        float armorValue = GetTotalArmorValue(agent);
        score += armorValue * 0.5f;
        
        // Shield is critical
        if (agent.HasShieldCached)
            score += 20f;
        
        // Current health
        score += agent.HealthRatio * 10f;
        
        // Combat skills
        float meleeSkill = agent.Character.GetSkillValue(DefaultSkills.OneHanded);
        score += meleeSkill * 0.1f;
        
        return score;
    }
    
    float GetTotalArmorValue(Agent agent)
    {
        // Sum armor from head, body, arms, legs
        float total = 0;
        for (int i = 0; i < 4; i++)
        {
            var armor = agent.SpawnEquipment.GetEquipmentFromSlot((EquipmentIndex)i);
            if (armor.Item?.ArmorComponent != null)
                total += armor.Item.ArmorComponent.BodyArmor;
        }
        return total;
    }
}
```

---

## 12.3 Self-Organizing Ranks

Periodically reorganize the formation:

```csharp
class FormationOrganizer
{
    const float ReorganizeInterval = 10f;  // Every 10 seconds
    float _lastReorganizeTime = 0f;
    
    void OnTick(Formation formation)
    {
        if (Mission.Current.CurrentTime - _lastReorganizeTime < ReorganizeInterval)
            return;
        
        // SAFEGUARD: Don't reorganize during combat
        if (formation.IsEngaged())
            return;
        
        _lastReorganizeTime = Mission.Current.CurrentTime;
        ReorganizeFormation(formation);
    }
    
    void ReorganizeFormation(Formation formation)
    {
        // Get all agents with their scores
        var scoredAgents = formation.GetUnitsWithoutDetachedOnes()
            .OfType<Agent>()
            .Select(a => new { Agent = a, Score = CalculateFrontLineScore(a) })
            .OrderByDescending(x => x.Score)
            .ToList();
        
        int frontRankCount = formation.Arrangement.FileCount;
        
        for (int i = 0; i < scoredAgents.Count; i++)
        {
            var agent = scoredAgents[i].Agent;
            int desiredRank = i / frontRankCount;  // Higher score = lower rank index (front)
            
            // Find an agent in the desired position who should move back
            var agentInPosition = GetAgentAtRank(formation, desiredRank);
            
            if (agentInPosition != null && agentInPosition != agent)
            {
                // Swap if we have higher score
                float otherScore = CalculateFrontLineScore(agentInPosition);
                if (scoredAgents[i].Score > otherScore + 5f)  // Threshold to avoid constant swapping
                {
                    formation.SwitchUnitLocations(agent, agentInPosition);
                }
            }
        }
    }
}
```

---

## 12.4 Flank Spillover (When Line Stable)

Rear troops can work the flanks ONLY when the line is stable:

```csharp
class FlankSpillover
{
    const int MaxFlankers = 5;           // Don't send too many
    const float MaxFlankDistance = 20f;  // Don't go too far
    
    List<Agent> ActiveFlankers = new List<Agent>();
    
    void ManageFlankSpillover(Formation formation, BattleState state)
    {
        // SAFEGUARD: Only when line is stable
        if (!IsLineStable(formation))
        {
            // Recall all flankers immediately
            RecallFlankers(formation);
            return;
        }
        
        // SAFEGUARD: Only if enemy flank is actually exposed
        if (!IsEnemyFlankExposed(state))
            return;
        
        // Get rear-rank troops who could flank
        var candidates = GetRearRankTroops(formation)
            .Where(a => !ActiveFlankers.Contains(a))
            .Take(MaxFlankers - ActiveFlankers.Count);
        
        foreach (var agent in candidates)
        {
            // SAFEGUARD: Position is close to formation, not a chase
            Vec2 flankPosition = CalculateFlankPosition(formation, state);
            float distanceFromFormation = flankPosition.Distance(formation.CachedMedianPosition.AsVec2);
            
            if (distanceFromFormation > MaxFlankDistance)
                continue;  // Too far, don't send
            
            // Detach but keep leash
            agent.SetScriptedPosition(new WorldPosition(Mission.Current.Scene, flankPosition));
            ActiveFlankers.Add(agent);
        }
    }
    
    bool IsLineStable(Formation formation)
    {
        // Line is stable if:
        float casualtyRate = formation.QuerySystem.CasualtyRatio;
        int frontRankCount = CountFrontRankTroops(formation);
        bool beingPushed = formation.QuerySystem.IsUnderAttack;
        bool engaged = formation.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Charge;
        
        // Not stable if: heavy casualties, low front rank, being pushed, or charging
        if (casualtyRate > 0.1f)
            return false;
        if (frontRankCount < 5)
            return false;
        if (beingPushed)
            return false;
        
        return true;
    }
    
    void RecallFlankers(Formation formation)
    {
        foreach (var agent in ActiveFlankers.ToList())
        {
            // Clear scripted position, return to formation
            agent.ClearScriptedPosition();
            agent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Follow);
            ActiveFlankers.Remove(agent);
        }
    }
}
```

---

## 12.5 Gap Filling Logic

When front-line troops fall, middle ranks step up:

```csharp
class GapFiller
{
    void CheckForGaps(Formation formation)
    {
        // SAFEGUARD: Only during combat (gaps matter then)
        if (!formation.IsEngaged())
            return;
        
        int frontRankCount = CountFrontRankTroops(formation);
        int idealFrontRank = formation.Arrangement.FileCount;
        
        // 30% or more gaps in front rank
        if (frontRankCount < idealFrontRank * 0.7f)
        {
            // Get middle rank troops, sorted by front-line score
            var middleTroops = GetMiddleRankTroops(formation)
                .OrderByDescending(a => CalculateFrontLineScore(a))
                .Take(idealFrontRank - frontRankCount);
            
            foreach (var agent in middleTroops)
            {
                // Find empty front position
                var emptyFrontPos = FindEmptyFrontPosition(formation);
                if (emptyFrontPos.HasValue)
                {
                    // Move forward to fill gap
                    PromoteToPosition(agent, emptyFrontPos.Value);
                }
            }
        }
    }
}
```

---

## 12.6 Safeguards — Formation Integrity First

**Critical safeguards to prevent breaking formation:**

```csharp
class FormationSafeguards
{
    // SAFEGUARD 1: Maximum distance from formation center
    const float MaxDeviationDistance = 25f;
    
    // SAFEGUARD 2: Never reorganize during active combat
    bool CanReorganize(Formation formation)
    {
        if (formation.IsEngaged())
            return false;
        if (formation.QuerySystem.IsUnderRangedAttack)
            return false;
        if (formation.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Charge)
            return false;
        return true;
    }
    
    // SAFEGUARD 3: Flankers have a leash
    void EnforceFlankLeash(Agent flanker, Formation formation)
    {
        float distance = flanker.Position.AsVec2.Distance(formation.CachedMedianPosition.AsVec2);
        
        if (distance > MaxDeviationDistance)
        {
            // Too far — force return
            flanker.ClearScriptedPosition();
            flanker.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Follow);
        }
    }
    
    // SAFEGUARD 4: No chasing
    void PreventChasing(Agent agent)
    {
        // If agent is pursuing an enemy, check distance
        if (agent.IsActivelyAttacking)
        {
            float distanceFromFormation = agent.Position.AsVec2.Distance(
                agent.Formation.CachedMedianPosition.AsVec2);
            
            if (distanceFromFormation > MaxDeviationDistance)
            {
                // Stop chasing, return to formation
                agent.ClearTargetAgent();
                agent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Follow);
            }
        }
    }
    
    // SAFEGUARD 5: Instant recall on formation order
    void OnFormationOrderReceived(Formation formation, MovementOrder order)
    {
        if (order.OrderEnum == MovementOrder.MovementOrderEnum.Charge ||
            order.OrderEnum == MovementOrder.MovementOrderEnum.Retreat ||
            order.OrderEnum == MovementOrder.MovementOrderEnum.Move)
        {
            // Recall all flankers immediately
            RecallAllFlankers(formation);
        }
    }
    
    // SAFEGUARD 6: Line pressure = tighten up
    void OnLinePressure(Formation formation)
    {
        if (formation.QuerySystem.CasualtyRatio > 0.1f ||
            formation.QuerySystem.LocalEnemyPower > formation.QuerySystem.LocalAllyPower)
        {
            // Recall flankers, tighten formation
            RecallAllFlankers(formation);
            formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);
        }
    }
}
```

---

## 12.7 Positional Combat Behavior

Agents fight differently based on their position in the formation:

```csharp
class PositionalCombatBehavior
{
    void UpdateCombatBehavior(Agent agent)
    {
        int rankIndex = ((IFormationUnit)agent).FormationRankIndex;
        int maxRank = agent.Formation.Arrangement.RankCount;
        
        if (rankIndex == 0)
        {
            // FRONT RANK: Hold the line, fight aggressively
            agent.SetAIDrivenProperty(DrivenProperty.AIBlockOnDecideAbility, 0.85f);
            agent.SetAIDrivenProperty(DrivenProperty.AIAttackOnDecideChance, 0.7f);
            // Don't chase — HOLD
        }
        else if (rankIndex < maxRank / 2)
        {
            // MIDDLE RANK: Support front, fill gaps
            agent.SetAIDrivenProperty(DrivenProperty.AIBlockOnDecideAbility, 0.8f);
            agent.SetAIDrivenProperty(DrivenProperty.AIAttackOnDecideChance, 0.5f);
        }
        else
        {
            // REAR RANK: Defensive, ranged if available
            agent.SetAIDrivenProperty(DrivenProperty.AIBlockOnDecideAbility, 0.95f);
            agent.SetAIDrivenProperty(DrivenProperty.AIAttackOnDecideChance, 0.3f);
            
            // Throw javelins if available
            if (agent.HasRangedWeapon() && !agent.IsInMeleeRange())
            {
                agent.TryToSwitchToWeapon(WeaponClass.Javelin);
            }
        }
    }
}
```

---

## Part 12 Summary

| Principle | Implementation |
|-----------|----------------|
| **Armor/elite to front** | Score agents, swap positions via native API |
| **Light troops to rear** | Lower score = higher rank index |
| **Flank spillover** | Only when line stable, max 5 troops, 20m leash |
| **Gap filling** | Middle rank fills front when casualties occur |
| **No chasing** | 25m max deviation, instant recall on orders |
| **Positional fighting** | Front aggressive, rear defensive/ranged |

| Safeguard | Trigger |
|-----------|---------|
| **No reorganize in combat** | Wait for lull |
| **Flanker leash** | 20m max from formation |
| **Chase prevention** | 25m max deviation |
| **Instant recall** | On any formation order |
| **Tighten on pressure** | Casualties or local enemy superiority |

---

# Part 13: Formation Doctrine System

This part covers how armies organize into formations before and during battle. The missing layer between "we have troops" and "the orchestrator makes decisions."

## 13.1 Two Battle Scales

Bannerlord has two distinct battle scales that require different formation approaches:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         BATTLE SCALE COMPARISON                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  LORD PARTY BATTLES                    ARMY BATTLES                          │
│  ──────────────────                    ─────────────                         │
│  50-200 per side                       400-500 on field (per side)           │
│  Everyone fits on field                2000+ total, waves spawn in           │
│  No reinforcement waves                Reinforcement management critical     │
│  Quick, decisive                       Attrition warfare                     │
│  Simple formations                     Complex formations possible           │
│  1-3 formations typical                4-6 formations possible               │
│  Single commander (lord)               Multiple lords coordinating           │
│                                                                              │
│  MAJORITY OF COMBAT                    BIG SET-PIECE BATTLES                 │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 13.2 Lord Party Battles (Small Scale)

Most combat in Bannerlord is lord party vs lord party. Keep formations simple:

```
                    TYPICAL LORD PARTY FORMATION (100-200 troops)
                    
        ┌────────────────────────────────────────┐
        │  ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○   │  ARCHERS (20-40)
        │                                        │  Formation V
        │  ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │
        │  ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │  INFANTRY (60-120)
        │  ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │  Formation I
        └────────────────────────────────────────┘
        
        ◆◆◆◆◆◆◆◆                    ◆◆◆◆◆◆◆◆      CAVALRY (20-40)
        Left flank                  Right flank   Formation VII
```

**Three formations maximum.** Infantry holds, archers shoot, cavalry flanks.

### Small Scale Characteristics

| Aspect | Behavior |
|--------|----------|
| **Formations** | 2-3 (Infantry, Archers, Cavalry) |
| **Reserves** | None needed (everyone fights) |
| **Reinforcements** | None (everyone on field) |
| **Orchestrator complexity** | Simple: winning → push, losing → hold/retreat |
| **Key decisions** | When cavalry charges, when to close distance |

### Small Scale Formation Logic

```csharp
class SmallBattleFormations
{
    void OrganizePartyBattle(Party party)
    {
        // Simple: one formation per troop type
        Formation infantry = GetOrCreateFormation(FormationClass.Infantry);
        Formation archers = GetOrCreateFormation(FormationClass.Ranged);
        Formation cavalry = GetOrCreateFormation(FormationClass.Cavalry);
        
        // Assign all troops by type
        foreach (var troop in party.Troops)
        {
            if (troop.IsMounted && !troop.IsRanged)
                cavalry.AddUnit(troop);
            else if (troop.IsRanged)
                archers.AddUnit(troop);
            else
                infantry.AddUnit(troop);
        }
        
        // Position: Infantry front, archers behind, cavalry flanks
        PositionFormations(infantry, archers, cavalry);
    }
}
```

---

## 13.3 Army Battles (Large Scale)

When armies clash, the field cap (800-1000 total, ~400-500 per side) creates reinforcement waves:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ARMY BATTLE DYNAMICS                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  YOUR ARMY: 1000 troops                ENEMY ARMY: 1000 troops              │
│  ──────────────────────                ────────────────────────              │
│  Wave 1: 400 spawn                     Wave 1: 400 spawn                    │
│  Wave 2: 400 spawn (as deaths occur)   Wave 2: 400 spawn                    │
│  Wave 3: 200 spawn (final)             Wave 3: 200 spawn                    │
│                                                                              │
│                         THE BATTLE IS WON IN WAVES                           │
│                                                                              │
│  If you crush Wave 1 with few losses:                                        │
│    → Your survivors + reinforcements vs their fresh Wave 2                  │
│    → Advantage snowballs                                                     │
│                                                                              │
│  If Wave 1 is a bloodbath:                                                   │
│    → Both sides reset with fresh troops                                      │
│    → Repeat until one side runs out                                          │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### The Spawn Point Advantage

```
        YOUR SPAWN                                              ENEMY SPAWN
            ▼                                                       ▼
        ┌───────┐                                               ┌───────┐
        │ FRESH │                                               │ FRESH │
        │ TROOPS│                                               │ TROOPS│
        │SPAWNING                                               │SPAWNING
        └───┬───┘                                               └───┬───┘
            │                                                       │
            │  ← 20 seconds march →  [BATTLE]  ← 20 seconds march → │
            │                            ↑                          │
            │                      If battle is HERE                │
            │                      Both have equal march            │
            │                                                       │
            
        BUT IF BATTLE DRIFTS:
        
        YOUR SPAWN              [BATTLE HERE]                   ENEMY SPAWN
            ▼                        ↑                              ▼
        ┌───────┐               Your troops                     ┌───────┐
        │ 5 sec │               reinforce FAST                  │ 35 sec│
        │ march │               Enemy reinforcements            │ march │
        └───────┘               arrive tired, piecemeal         └───────┘
        
        ✅ ADVANTAGE: Fight near YOUR spawn
```

**Fighting near your spawn is good** — your reinforcements arrive fast, theirs have to march.

---

## 13.4 Formation Count Logic

How many formations should an army use?

```csharp
class FormationCountDecider
{
    int DecideFormationCount(int troopsOnField, ArmyComposition comp)
    {
        // SMALL PARTIES (50-100)
        if (troopsOnField < 100)
        {
            // 2-3 formations: Infantry + maybe Archers + maybe Cavalry
            int count = 1;  // Infantry always
            if (comp.ArcherRatio > 0.1f) count++;
            if (comp.CavalryRatio > 0.1f) count++;
            return count;
        }
        
        // MEDIUM PARTIES (100-200)
        if (troopsOnField < 200)
        {
            // 3 formations: Infantry + Archers + Cavalry
            return 3;
        }
        
        // SMALL ARMIES (200-400)
        if (troopsOnField < 400)
        {
            // 4 formations: Can split infantry or add reserve
            return 4;
        }
        
        // LARGE ARMIES (400+)
        // 5-6 formations: Three-wing or classical with proper reserve
        return comp.WantsEnvelopment ? 5 : 5;
    }
}
```

### Formation Count Summary

| Troops on Field | Formations | Typical Structure |
|-----------------|------------|-------------------|
| **50-100** | 2-3 | Infantry + Archers + Cavalry |
| **100-200** | 3 | Infantry + Archers + Cavalry |
| **200-400** | 4 | Infantry + Reserve/Flank + Archers + Cavalry |
| **400-500** | 5-6 | Three-wing OR Main + Reserve + Archers + 2x Cavalry |

---

## 13.5 Formation Doctrines

### Doctrine A: Single Deep Line + Reserve

Best for medium armies (200-400), balanced composition, defending.

```
                    ┌──────────────────────────────────────┐
   ARCHERS →        │ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○  │  (behind)
                    │                                      │
   INFANTRY →       │ ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │
                    │ ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │
                    │ ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │  (3-4 ranks deep)
                    └──────────────────────────────────────┘
                    
   RESERVE →              │ ●●●●●●●●●● │  (cavalry or infantry, behind center)
   
   CAVALRY →        ◆◆◆◆◆                           ◆◆◆◆◆  (flanks)
```

**Formations used:** 4-5 (Infantry I, Infantry II as reserve, Archers V, Cavalry VII/VIII)

### Doctrine B: Three-Wing Line

Best for large armies (400+), when you want to envelope, clear numerical advantage.

```
        LEFT WING               CENTER                  RIGHT WING
        ┌──────────┐      ┌──────────────────┐        ┌──────────┐
        │▪▪▪▪▪▪▪▪▪▪│      │  ▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │        │▪▪▪▪▪▪▪▪▪▪│
        │▪▪▪▪▪▪▪▪▪▪│      │  ▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │        │▪▪▪▪▪▪▪▪▪▪│
        └──────────┘      │  ▪▪▪▪▪▪▪▪▪▪▪▪▪▪  │        └──────────┘
        Formation I       └──────────────────┘        Formation III
        (100 troops)           Formation II           (100 troops)
                               (150 troops)
                               
                          ○○○○○○○○○○○○○○○○○○
                          Archers (V)
                          
        ◆◆◆◆◆                                              ◆◆◆◆◆
        Left Cav (VII)                                Right Cav (VIII)
```

**Formations used:** 5-6 (Infantry I/II/III, Archers V, Cavalry VII/VIII)

### Doctrine C: Extended Thin Line (Outnumbered)

When outnumbered, match enemy frontage with a thin line. Use cavalry/archers to compensate.

```
        YOU: 300                                    ENEMY: 500
        
        ───────────────────────────────────────     ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
        ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪     ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
        ───────────────────────────────────────     ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
                                                    ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
        Thin (2 ranks) but WIDE                     Deep (4 ranks) but narrow
        
        ✅ Can't be outflanked                      ❌ Can outflank you
        ❌ Can be punched through                   ✅ Can punch through
```

**Strategy:**
- Match frontage so they can't envelope
- Cavalry harasses flanks (they can't cover everything)
- Archers attrit before contact
- **DELAY, don't defeat** — every minute costs them
- Fall back toward YOUR spawn (your reinforcements faster)

### Doctrine D: Hammer and Anvil (Cavalry-Heavy)

When you have 30%+ cavalry and enemy lacks anti-cavalry.

```
                    ┌────────────────────┐
   INFANTRY →       │ ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪ │   (anvil - holds enemy in place)
                    │ ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪ │
                    └────────────────────┘
                    
                    ○ ○ ○ ○ ○ ○ ○ ○        (archers behind)
                    
   LEFT CAVALRY →   ◆◆◆◆◆                          ◆◆◆◆◆  ← RIGHT CAVALRY
                        ↘                      ↙
                          ╲  HAMMER  ╱
                           (charge rear/flanks when infantry engaged)
```

---

## 13.6 Line Depth Decisions

The key tradeoff: **thin and wide** vs **deep and narrow**.

```csharp
class LineDepthDecider
{
    LineConfiguration DecideLineDepth(Army us, Army enemy)
    {
        float ourTroops = us.TotalInfantry;
        float theirTroops = enemy.TotalInfantry;
        float troopRatio = ourTroops / theirTroops;
        
        // OUTNUMBERED (< 70% of enemy)
        if (troopRatio < 0.7f)
        {
            // Can't match their depth AND their frontage
            // Choice: Match frontage (thin) OR match depth (short)
            
            bool haveMobility = us.CavalryRatio > 0.15f || us.ArcherRatio > 0.25f;
            
            if (haveMobility)
            {
                // Go thin, use mobility to compensate
                return new LineConfiguration
                {
                    Ranks = 2,
                    Frontage = FrontageType.Extended,
                    Strategy = "Match frontage, thin ranks. Use cavalry/archers to inflict casualties. Trade space for time."
                };
            }
            else
            {
                // Go deep, refuse flanks, compact defense
                return new LineConfiguration
                {
                    Ranks = 4,
                    Frontage = FrontageType.Compact,
                    Strategy = "Deep ranks, refuse flanks. Concentrate on one point if attacking."
                };
            }
        }
        
        // OUTNUMBERING (> 130% of enemy)
        if (troopRatio > 1.3f)
        {
            bool theyHaveStrongCenter = enemy.CenterFormation?.AverageTier > 4;
            
            if (theyHaveStrongCenter)
            {
                // Envelope, avoid their strength
                return new LineConfiguration
                {
                    Ranks = 3,
                    Frontage = FrontageType.Extended,
                    Strategy = "Extend wings to envelop. Pin center, crush flanks."
                };
            }
            else
            {
                // Mass and punch through
                return new LineConfiguration
                {
                    Ranks = 5,
                    Frontage = FrontageType.Concentrated,
                    Strategy = "Deep formation. Punch through their center."
                };
            }
        }
        
        // ROUGHLY EQUAL
        return new LineConfiguration
        {
            Ranks = 3,
            Frontage = FrontageType.Standard,
            Strategy = "Standard line. Look for opportunities, protect flanks."
        };
    }
}
```

### Line Depth Summary

| Situation | Ranks | Frontage | Strategy |
|-----------|-------|----------|----------|
| **Outnumbered + mobile** | 2 | Extended | Thin line, use cavalry/archers |
| **Outnumbered + no mobility** | 4 | Compact | Deep, refuse flanks |
| **Outnumbering + weak enemy center** | 3 | Extended | Envelope, crush flanks |
| **Outnumbering + strong enemy center** | 5 | Concentrated | Punch through |
| **Equal forces** | 3 | Standard | Balanced, seek opportunities |

---

## 13.7 Orchestrator Formation Reading

The dual orchestrators must **read** enemy formation structure and react:

```csharp
class FormationReader
{
    FormationAnalysis AnalyzeEnemyFormation(Team enemy)
    {
        var analysis = new FormationAnalysis();
        
        // Count active formations
        analysis.FormationCount = enemy.Formations.Count(f => f.CountOfUnits > 0);
        
        // Measure frontage and depth
        analysis.TotalFrontage = CalculateTotalFrontage(enemy);
        analysis.AverageDepth = CalculateAverageDepth(enemy);
        
        // Identify structure
        if (analysis.FormationCount == 1)
            analysis.Doctrine = EnemyDoctrine.SingleLine;
        else if (analysis.FormationCount >= 3 && HasFlankFormations(enemy))
            analysis.Doctrine = EnemyDoctrine.ThreeWing;
        else if (DetectReserve(enemy))
            analysis.Doctrine = EnemyDoctrine.LineWithReserve;
        else
            analysis.Doctrine = EnemyDoctrine.Standard;
        
        // Check for reserves
        analysis.HasReserve = DetectReserve(enemy);
        analysis.ReserveStrength = analysis.HasReserve 
            ? GetReserveFormation(enemy).Power / enemy.QuerySystem.TeamPower 
            : 0f;
        
        // Identify weak points
        analysis.Gaps = FindGapsBetweenFormations(enemy);
        analysis.ThinSections = FindThinSections(enemy);
        analysis.ExposedFlanks = !HasFlankProtection(enemy);
        
        return analysis;
    }
    
    float CalculateTotalFrontage(Team team)
    {
        // Sum the width of all front-line formations
        return team.Formations
            .Where(f => f.FormationIndex <= FormationClass.HeavyCavalry)
            .Where(f => !IsReserve(f))
            .Sum(f => f.Width);
    }
    
    float CalculateAverageDepth(Team team)
    {
        // Average rank count of infantry formations
        var infantryFormations = team.Formations
            .Where(f => f.QuerySystem.IsInfantryFormation);
        
        if (!infantryFormations.Any()) return 0f;
        
        return infantryFormations.Average(f => f.Arrangement.RankCount);
    }
    
    bool DetectReserve(Team team)
    {
        // A reserve is a formation behind the main line, not yet engaged
        foreach (var formation in team.Formations)
        {
            if (formation.CountOfUnits < 10) continue;
            
            float distanceFromFront = DistanceFromFrontLine(formation, team);
            bool notEngaged = !formation.IsEngaged();
            
            if (distanceFromFront > 30f && notEngaged)
                return true;
        }
        return false;
    }
}
```

---

## 13.8 Counter-Formation Tactics

How the orchestrator responds to enemy formation structure:

```csharp
class FormationCounterTactics
{
    TacticalResponse DecideResponse(FormationAnalysis enemy, Army us)
    {
        // Enemy has THIN extended line?
        if (enemy.AverageDepth < 2.5f && enemy.TotalFrontage > us.TotalFrontage * 0.9f)
        {
            // PUNCH THROUGH — their thin line can't stop concentrated force
            return new TacticalResponse
            {
                Priority = "Concentrate force on one section",
                CavalryRole = "Charge the weakest section",
                InfantryRole = "Mass on center or one flank, break through",
                ArcherRole = "Suppress where we're attacking"
            };
        }
        
        // Enemy has DEEP compact formation?
        if (enemy.AverageDepth > 4f && enemy.TotalFrontage < us.TotalFrontage * 0.8f)
        {
            // ENVELOP — extend wings, hit their flanks
            return new TacticalResponse
            {
                Priority = "Envelop their flanks",
                CavalryRole = "Wide flank, hit rear",
                InfantryRole = "Pin front, extend wings to wrap",
                ArcherRole = "Focus fire on their flanks from angle"
            };
        }
        
        // Enemy has THREE-WING with gaps?
        if (enemy.Gaps.Any() && enemy.Gaps.Max(g => g.Width) > 15f)
        {
            // EXPLOIT GAPS — drive through and split their army
            return new TacticalResponse
            {
                Priority = "Drive through gap between formations",
                CavalryRole = "Charge through gap, hit rear",
                InfantryRole = "Fix wings in place, reserve punches gap",
                ArcherRole = "Suppress nearest formation to gap"
            };
        }
        
        // Enemy has STRONG RESERVE?
        if (enemy.HasReserve && enemy.ReserveStrength > 0.2f)
        {
            // BAIT AND PUNISH — force them to commit, then exploit
            return new TacticalResponse
            {
                Priority = "Bait reserve commitment, then exploit",
                CavalryRole = "Feint, force reaction",
                InfantryRole = "Steady pressure, don't overcommit",
                ArcherRole = "Attrit their reserve while it waits"
            };
        }
        
        // Standard engagement
        return new TacticalResponse
        {
            Priority = "Standard engagement, look for opportunities",
            CavalryRole = "Screen, wait for exposed target",
            InfantryRole = "Advance in formation, maintain cohesion",
            ArcherRole = "Fire at highest-value target"
        };
    }
}
```

### Counter-Tactics Summary

| Enemy Formation | Response | Key Move |
|-----------------|----------|----------|
| **Thin extended line** | Punch through | Concentrate on one point, break it |
| **Deep compact formation** | Envelop | Extend wings, hit flanks and rear |
| **Three-wing with gaps** | Exploit gaps | Drive through gap, split their army |
| **Strong reserve** | Bait and punish | Force commitment, then exploit |
| **Standard line** | Opportunistic | Standard engagement, seek weakness |

---

## 13.9 Reinforcement Wave Strategy

Unique to army battles — managing multiple waves:

```csharp
class WaveStrategy
{
    enum WavePosture { Aggressive, Efficient, Grinding, Balanced }
    
    WavePosture DecideWavePosture(BattleState state)
    {
        int ourRemaining = state.OurReinforcementsRemaining;
        int theirRemaining = state.EnemyReinforcementsRemaining;
        float casualtyRatio = state.OurCasualties / Math.Max(1f, state.TheirCasualties);
        
        // We're trading well (killing more than losing)
        if (casualtyRatio < 0.7f)
        {
            return WavePosture.Aggressive;
            // Keep pressure, we're winning attrition
        }
        
        // They have more waves than us
        if (theirRemaining > ourRemaining * 1.3f)
        {
            return WavePosture.Efficient;
            // Need to trade BETTER than even
            // Use archers, use cavalry, avoid grinding infantry fights
        }
        
        // We have more waves than them
        if (ourRemaining > theirRemaining * 1.3f)
        {
            return WavePosture.Grinding;
            // Can afford 1:1 trades
            // Push hard, exhaust their waves
        }
        
        return WavePosture.Balanced;
    }
    
    void ApplyWavePosture(WavePosture posture, Army army)
    {
        switch (posture)
        {
            case WavePosture.Aggressive:
                // Commit everything, press advantage
                army.Orchestrator.SetAggression(0.8f);
                army.Orchestrator.CommitReserves();
                break;
                
            case WavePosture.Efficient:
                // Careful, preserve troops
                army.Orchestrator.SetAggression(0.4f);
                army.Orchestrator.HoldReserves();
                army.Orchestrator.PrioritizeRangedDamage();
                break;
                
            case WavePosture.Grinding:
                // Steady pressure, trade troops
                army.Orchestrator.SetAggression(0.7f);
                army.Orchestrator.CommitReservesIfNeeded();
                break;
                
            case WavePosture.Balanced:
                army.Orchestrator.SetAggression(0.5f);
                break;
        }
    }
}
```

### Wave Strategy Summary

| Situation | Posture | Behavior |
|-----------|---------|----------|
| **Trading well** (K/D > 1.4) | Aggressive | Press advantage, commit reserves |
| **They have more waves** | Efficient | Preserve troops, use ranged, avoid grinding |
| **We have more waves** | Grinding | Steady pressure, trade 1:1 is fine |
| **Equal** | Balanced | Standard engagement |

---

## 13.10 Formation Sizing

How to set formation width and depth using native APIs:

```csharp
class FormationSizer
{
    // Bannerlord uses ~2m per file (soldier), ~1.5m per rank
    const float MetersPerFile = 2f;
    const float MetersPerRank = 1.5f;
    
    FormationLayout CalculateLayout(int troopCount, int desiredRanks)
    {
        int filesPerRank = troopCount / desiredRanks;
        float frontageMeters = filesPerRank * MetersPerFile;
        float depthMeters = desiredRanks * MetersPerRank;
        
        return new FormationLayout
        {
            Ranks = desiredRanks,
            FilesPerRank = filesPerRank,
            FrontageMeters = frontageMeters,
            DepthMeters = depthMeters
        };
    }
    
    void SetFormationWidth(Formation formation, float targetFrontageMeters)
    {
        // Native API: FormOrder.FormOrderCustom sets target width
        formation.FormOrder = FormOrder.FormOrderCustom(targetFrontageMeters);
    }
    
    void ExtendLine(Formation formation)
    {
        // Make line wider (fewer ranks)
        float currentWidth = formation.Width;
        formation.FormOrder = FormOrder.FormOrderCustom(currentWidth * 1.5f);
    }
    
    void CompactLine(Formation formation)
    {
        // Make line deeper (more ranks)
        float currentWidth = formation.Width;
        formation.FormOrder = FormOrder.FormOrderCustom(currentWidth * 0.7f);
    }
}
```

### Example Formation Sizes

| Troops | Ranks | Files | Frontage |
|--------|-------|-------|----------|
| 100 | 3 | 33 | ~66m |
| 100 | 2 | 50 | ~100m |
| 200 | 4 | 50 | ~100m |
| 200 | 2 | 100 | ~200m |
| 300 | 3 | 100 | ~200m |
| 400 | 4 | 100 | ~200m |

---

## 13.11 Troop Distribution Across Formations

When splitting an army into multiple formations, troops must be distributed intelligently. You don't want all your green recruits in one formation — that formation becomes a weak point the enemy can exploit.

### The Problem: Naive Distribution

```
BAD: Simple type-based split creates quality imbalance

    FORMATION I              FORMATION II             FORMATION III
    ┌──────────────┐         ┌──────────────┐         ┌──────────────┐
    │ T1 T1 T1 T1  │         │ T3 T3 T3 T3  │         │ T5 T5 T5 T5  │
    │ T1 T1 T2 T2  │         │ T3 T3 T4 T4  │         │ T5 T5 T5 T5  │
    │ T2 T2 T2 T2  │         │ T4 T4 T4 T4  │         │ T6 T6 T6 T6  │
    └──────────────┘         └──────────────┘         └──────────────┘
    Avg Tier: 1.5            Avg Tier: 3.5            Avg Tier: 5.5
    
    ❌ Formation I crumbles instantly
    ❌ Enemy focuses weak point
    ❌ Elite troops wasted in reserve while recruits die
```

### The Solution: Balanced Distribution

```
GOOD: Mix quality across formations, weight by role

    LEFT WING                CENTER (Main Effort)     RIGHT WING
    ┌──────────────┐         ┌──────────────┐         ┌──────────────┐
    │ T4 T3 T2 T3  │         │ T5 T5 T4 T5  │         │ T4 T3 T2 T3  │
    │ T3 T2 T3 T2  │         │ T4 T4 T5 T4  │         │ T3 T2 T3 T2  │
    │ T2 T1 T2 T1  │         │ T3 T3 T4 T3  │         │ T2 T1 T2 T1  │
    └──────────────┘         └──────────────┘         └──────────────┘
    Avg Tier: 2.5            Avg Tier: 4.2            Avg Tier: 2.5
    
    ✅ Each formation has a mix
    ✅ Best troops in center (main effort)
    ✅ No formation is "all green"
    ✅ Elite troops in front rows of each formation
```

### Distribution Algorithm

```csharp
class TroopDistributor
{
    void DistributeTroops(List<TroopRoster> troops, List<Formation> formations, FormationRoles roles)
    {
        // Step 1: Sort all troops by tier (descending)
        var sortedTroops = troops
            .OrderByDescending(t => t.Tier)
            .ThenByDescending(t => t.ArmorValue)
            .ToList();
        
        // Step 2: Determine formation weights based on role
        var weights = CalculateFormationWeights(formations, roles);
        // Example: Center = 1.3, Flanks = 1.0, Reserve = 0.8
        
        // Step 3: Distribute using weighted round-robin
        // Best troops go to highest-weighted formations first
        // But EVERY formation gets SOME quality troops
        
        int formationIndex = 0;
        float[] qualityBudget = new float[formations.Count];
        
        foreach (var troop in sortedTroops)
        {
            // Find formation that needs this troop most
            int targetFormation = FindBestFormation(troop, formations, weights, qualityBudget);
            
            formations[targetFormation].AddTroop(troop);
            qualityBudget[targetFormation] += troop.Tier;
        }
    }
    
    int FindBestFormation(TroopRoster troop, List<Formation> formations, 
                          float[] weights, float[] currentQuality)
    {
        float bestScore = float.MinValue;
        int bestFormation = 0;
        
        for (int i = 0; i < formations.Count; i++)
        {
            // How much does this formation need quality?
            float targetQuality = weights[i] * AverageDesiredTier;
            float currentAvg = currentQuality[i] / Math.Max(1, formations[i].Count);
            float qualityNeed = targetQuality - currentAvg;
            
            // High-tier troops go where quality is most needed
            // Low-tier troops fill remaining slots
            float score = qualityNeed * weights[i];
            
            // Don't overfill formations
            if (formations[i].Count >= formations[i].TargetSize)
                score -= 100f;
            
            if (score > bestScore)
            {
                bestScore = score;
                bestFormation = i;
            }
        }
        
        return bestFormation;
    }
    
    float[] CalculateFormationWeights(List<Formation> formations, FormationRoles roles)
    {
        float[] weights = new float[formations.Count];
        
        for (int i = 0; i < formations.Count; i++)
        {
            weights[i] = roles.GetRole(formations[i]) switch
            {
                FormationRole.Center => 1.3f,      // Main effort gets best troops
                FormationRole.Reserve => 1.2f,     // Reserve is your trump card
                FormationRole.LeftWing => 1.0f,    // Wings get balanced
                FormationRole.RightWing => 1.0f,
                FormationRole.Flanking => 0.9f,    // Flankers can be lighter
                FormationRole.Screening => 0.8f,   // Screens don't need elite
                _ => 1.0f
            };
        }
        
        return weights;
    }
}
```

### Role-Based Quality Weighting

| Formation Role | Quality Weight | Reasoning |
|----------------|----------------|-----------|
| **Center/Main Line** | 1.3x | Takes the brunt, needs to hold |
| **Reserve** | 1.2x | Your decisive force, should be strong |
| **Wings** | 1.0x | Balanced, may need to refuse or extend |
| **Flanking Force** | 0.9x | Speed matters more than armor |
| **Screening** | 0.8x | Expendable, buying time |
| **Archers** | By skill | Distribute by Bow skill, not tier |

### Special Cases

**All Low-Tier Army:**
When most of your army is tier 1-3, you can't give everyone elites. Strategy:
- Concentrate what elites you have in CENTER
- Accept that flanks will be weaker
- Use formation depth (more ranks) to compensate for quality

```csharp
void HandleLowQualityArmy(List<TroopRoster> troops, List<Formation> formations)
{
    float avgTier = troops.Average(t => t.Tier);
    
    if (avgTier < 2.5f)
    {
        // Low quality army — concentrate strength
        // Put ALL tier 4+ in center formation
        var elites = troops.Where(t => t.Tier >= 4).ToList();
        var regulars = troops.Where(t => t.Tier < 4).ToList();
        
        // Elites go to center
        foreach (var elite in elites)
            formations[CenterIndex].AddTroop(elite);
        
        // Regulars distributed evenly across all (including center)
        DistributeEvenly(regulars, formations);
    }
}
```

**Mixed Cavalry/Infantry:**
Don't mix mounted and foot in same formation. Separate first, then balance within type:

```csharp
void DistributeByType(List<TroopRoster> troops, FormationSet formations)
{
    var infantry = troops.Where(t => !t.IsMounted && !t.IsRanged).ToList();
    var archers = troops.Where(t => t.IsRanged && !t.IsMounted).ToList();
    var cavalry = troops.Where(t => t.IsMounted).ToList();
    
    // Distribute each type across their formations with quality balancing
    DistributeWithQuality(infantry, formations.InfantryFormations);
    DistributeWithQuality(archers, formations.ArcherFormations);
    DistributeWithQuality(cavalry, formations.CavalryFormations);
}
```

**Archer Distribution:**
For archers, skill matters more than tier. A Tier 3 with 150 Bow beats a Tier 5 with 80 Bow:

```csharp
void DistributeArchers(List<TroopRoster> archers, List<Formation> archerFormations)
{
    // Sort by Bow skill, not tier
    var sorted = archers
        .OrderByDescending(a => a.GetSkillValue(DefaultSkills.Bow))
        .ToList();
    
    // Best archers spread across all formations (not concentrated)
    // So every archer formation can shoot effectively
    DistributeRoundRobin(sorted, archerFormations);
}
```

### Formation Quality Validation

After distribution, validate that no formation is critically weak:

```csharp
class FormationQualityValidator
{
    void ValidateDistribution(List<Formation> formations)
    {
        float overallAvgTier = formations.SelectMany(f => f.Troops).Average(t => t.Tier);
        
        foreach (var formation in formations)
        {
            float formationAvgTier = formation.Troops.Average(t => t.Tier);
            float tierRatio = formationAvgTier / overallAvgTier;
            
            // Flag if formation is significantly below average
            if (tierRatio < 0.7f)
            {
                Log.Warn($"Formation {formation.Name} quality ({formationAvgTier:F1}) " +
                         $"is {(1-tierRatio)*100:F0}% below army average ({overallAvgTier:F1})");
                
                // Consider: merge with another formation, or accept weakness
                if (formation.Role == FormationRole.FlankingForce)
                {
                    // Flanking force can be weak — they hit and run
                    // Accept it
                }
                else
                {
                    // Main line formation is weak — rebalance needed
                    RequestRebalance(formation);
                }
            }
        }
    }
}
```

### Distribution Summary

| Principle | Implementation |
|-----------|----------------|
| **No all-green formations** | Weighted round-robin distribution |
| **Best troops where they matter** | Center/reserve get 1.2-1.3x weight |
| **Every formation gets quality** | Even flanks get some elite troops |
| **Archers by skill, not tier** | Bow skill determines placement |
| **Validate after distribution** | Flag formations below 70% of average |
| **Low-quality armies concentrate** | If avg tier < 2.5, stack center |

---

## Part 13 Summary

| Principle | Implementation |
|-----------|----------------|
| **Two battle scales** | Party (50-200) vs Army (400-500 on field) |
| **Simple for small** | 2-3 formations max for party battles |
| **Complex for large** | 5-6 formations possible for army battles |
| **Doctrine selection** | Based on composition, numbers, intent |
| **Line depth tradeoff** | Thin+wide vs deep+narrow |
| **Formation reading** | Orchestrator analyzes enemy structure |
| **Counter-tactics** | Specific responses to enemy formation |
| **Wave management** | Attrition strategy for large battles |
| **Spawn advantage** | Fight near your spawn for faster reinforcements |
| **Troop distribution** | Balanced quality across formations, weighted by role |
| **No all-green formations** | Round-robin with quality weighting |
| **Best troops where needed** | Center/reserve get 1.2-1.3x weight |

| Battle Scale | Formations | Reserves | Waves | Orchestrator Complexity |
|--------------|------------|----------|-------|-------------------------|
| **Party (50-100)** | 2-3 | No | No | Simple |
| **Party (100-200)** | 3 | No | No | Simple |
| **Army (200-400)** | 4 | Maybe | Maybe | Medium |
| **Army (400-500)** | 5-6 | Yes | Yes | Full |

---

# Part 14: Battle Plan Generation

The orchestrator doesn't just react to situations — it generates a **coherent battle plan** before contact and executes it. This is the difference between "respond to what's happening" and "make something happen."

## 14.1 Reactive vs Proactive AI

```
REACTIVE AI (Current Parts 1-13):
    
    Battle starts → Read situation → React to threats → Adjust
    
    Each formation acts somewhat independently
    No unified concept of "we're attacking LEFT"
    Good at responding, but doesn't CREATE opportunities

PROACTIVE AI (Part 14):

    Assess → Generate Plan → Assign Objectives → Execute → Adapt
    
    "Our plan: Attack their left flank"
    "Cavalry: eliminate archers, then rear attack"
    "Right flank: screen only, don't advance"
    "Infantry I: lead attack left, Infantry II: pin center"
    
    Creates opportunities, doesn't just exploit them
```

**Both are needed.** Proactive AI generates the plan, reactive AI adapts when things go wrong.

---

## 14.2 Plan Types

The AI selects from a library of battle plans based on the situation:

### Plan: Left Hook

Attack the enemy's left flank, roll up their line from left to right.

```
                        PLAN: LEFT HOOK
                        
    MAIN EFFORT: Left flank
    SUPPORTING: Center pins, right screens
    
         ENEMY LINE
         ═══════════════════════════════
              ↑           ↑
         [PIN]       [ATTACK]
              │           │
         ┌────┴────┐ ┌────┴────┐
         │ INF II  │ │ INF I   │  ← Main effort (reinforced)
         │  (pin)  │ │(attack) │
         └─────────┘ └─────────┘
                          ↑
                     ◆◆◆◆◆◆◆◆  CAVALRY
                     Target: Archers → Rear
         
         ┌─────────┐
         │ INF III │  ← Screen (refuse right)
         │(screen) │
         └─────────┘
```

### Plan: Right Hook

Mirror of Left Hook — attack their right flank.

### Plan: Center Punch

Concentrate force in the center, break through, split their army.

```
                        PLAN: CENTER PUNCH
                        
    MAIN EFFORT: Center
    SUPPORTING: Wings pin, cavalry exploits breach
    
         ENEMY LINE
         ═══════════════════════════════
                      ↑
                 [BREAKTHROUGH]
                      │
         ┌─────────┐ ┌───────────────┐ ┌─────────┐
         │ INF I   │ │    INF II     │ │ INF III │
         │  (pin)  │ │  (MAIN EFFORT)│ │  (pin)  │
         └─────────┘ └───────────────┘ └─────────┘
                           ↑
                      ●●●●●●●●  RESERVE
                      (exploit breach)
         
         ◆◆◆◆◆                              ◆◆◆◆◆
         L. CAV                              R. CAV
         (screen until breach, then pursue)
```

### Plan: Double Envelopment

Attack both flanks simultaneously, pin center.

```
                        PLAN: DOUBLE ENVELOPMENT
                        
    MAIN EFFORT: Both flanks
    SUPPORTING: Center pins
    
         ENEMY LINE
         ═══════════════════════════════
         ↑               ↑               ↑
    [ATTACK]          [PIN]          [ATTACK]
         │               │               │
    ┌────┴────┐    ┌────┴────┐    ┌────┴────┐
    │ INF I   │    │ INF II  │    │ INF III │
    │(attack) │    │  (pin)  │    │(attack) │
    └─────────┘    └─────────┘    └─────────┘
         ↑                               ↑
    ◆◆◆◆◆◆◆                         ◆◆◆◆◆◆◆
    L. CAV                           R. CAV
    (flank → rear)                (flank → rear)
```

### Plan: Hammer and Anvil

Infantry pins, cavalry delivers the killing blow.

```
                        PLAN: HAMMER AND ANVIL
                        
    ANVIL: Infantry holds enemy in place
    HAMMER: Cavalry charges rear/flank
    
         ENEMY LINE
         ═══════════════════════════════
                      ↑
                   [HOLD]
                      │
              ┌───────┴───────┐
              │   INFANTRY    │  ← ANVIL (engage, don't push)
              │   (hold)      │
              └───────────────┘
         
         ◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆  CAVALRY
              (wide flank)
                   ↓
              [REAR CHARGE]  ← HAMMER
```

### Plan: Delay / Fighting Withdrawal

Buy time, inflict casualties, don't get destroyed.

```
                        PLAN: DELAY
                        
    OBJECTIVE: Trade space for time
    
         ENEMY LINE → advancing →
         ═══════════════════════════════
                      ↑
                  [SKIRMISH]
                      │
              ┌───────┴───────┐
              │   INFANTRY    │  ← Fall back slowly
              │  (fighting    │     Face enemy, don't rout
              │   retreat)    │
              └───────────────┘
         
         ○○○○○○○○  ARCHERS: Fire and fall back
         
         ◆◆◆◆◆◆◆◆  CAVALRY: Harass flanks, cover retreat
```

### Plan: Refused Flank

Anchor one flank, attack with the other.

```
                        PLAN: REFUSED FLANK (Right)
                        
    STRONG: Left flank attacks
    WEAK: Right flank anchored/refused
    
         ENEMY LINE
         ═══════════════════════════════
         ↑                           
    [ATTACK]                          ↑
         │                       [ANCHOR]
    ┌────┴────────────────────┐      │
    │        INFANTRY         │ ┌────┴────┐
    │    (echelon attack)     │ │REFUSED  │  ← Angled back
    └─────────────────────────┘ │ FLANK   │     or on terrain
                                └─────────┘
         ◆◆◆◆◆◆◆◆◆  CAVALRY
         (support attack flank)
```

---

## 14.3 Plan Selection Logic

The orchestrator selects a plan based on multiple factors:

```csharp
class BattlePlanSelector
{
    BattlePlan SelectPlan(BattleContext context)
    {
        var candidates = new List<ScoredPlan>();
        
        foreach (var planType in AllPlanTypes)
        {
            float score = ScorePlan(planType, context);
            candidates.Add(new ScoredPlan(planType, score));
        }
        
        // Select highest scoring plan
        return candidates.OrderByDescending(p => p.Score).First().Plan;
    }
    
    float ScorePlan(PlanType plan, BattleContext context)
    {
        float score = 0f;
        
        switch (plan)
        {
            case PlanType.LeftHook:
            case PlanType.RightHook:
                // Flank attacks favor:
                // - Cavalry advantage
                // - Enemy flank is weak or exposed
                // - We have enough infantry to pin
                score += context.OurCavalryRatio * 20f;
                score += context.EnemyFlankExposed(plan == PlanType.LeftHook ? Side.Left : Side.Right) ? 15f : 0f;
                score += context.OurInfantryRatio > 0.4f ? 10f : -10f;
                break;
                
            case PlanType.CenterPunch:
                // Center attacks favor:
                // - Infantry advantage
                // - Enemy center is weak or thin
                // - We have reserves to exploit breach
                score += context.OurInfantryRatio * 15f;
                score += context.EnemyCenterWeak ? 20f : 0f;
                score += context.HaveReserve ? 10f : -5f;
                break;
                
            case PlanType.DoubleEnvelopment:
                // Requires significant numerical advantage
                score += context.PowerRatio > 1.4f ? 25f : -20f;
                score += context.OurCavalryRatio > 0.2f ? 15f : 0f;
                break;
                
            case PlanType.HammerAnvil:
                // Cavalry-focused
                score += context.OurCavalryRatio > 0.25f ? 30f : -10f;
                score += context.EnemyLacksCavalryCounter ? 15f : 0f;
                break;
                
            case PlanType.Delay:
                // When outnumbered or buying time
                score += context.PowerRatio < 0.6f ? 25f : -20f;
                score += context.DefensiveObjective ? 20f : 0f;
                break;
                
            case PlanType.RefusedFlank:
                // When one flank has terrain advantage or enemy strength
                score += context.TerrainAdvantage(Side.Right) ? 15f : 0f;
                score += context.EnemyStrengthOn(Side.Left) ? 10f : 0f;
                break;
        }
        
        return score;
    }
}
```

### Plan Selection Summary

| Plan | Best When |
|------|-----------|
| **Left/Right Hook** | Cavalry advantage, enemy flank exposed |
| **Center Punch** | Infantry advantage, enemy center thin, have reserves |
| **Double Envelopment** | 1.4x+ numerical advantage, mobile force |
| **Hammer and Anvil** | 25%+ cavalry, enemy lacks anti-cavalry |
| **Delay** | Outnumbered, buying time, defensive objective |
| **Refused Flank** | Terrain advantage on one side, or enemy concentrated there |

---

## 14.4 Main Effort Designation

Every plan has a **main effort** — where you concentrate your best troops and combat power:

```csharp
class MainEffortDesignator
{
    MainEffort DesignateMainEffort(BattlePlan plan, Army army)
    {
        var mainEffort = new MainEffort();
        
        switch (plan.Type)
        {
            case PlanType.LeftHook:
                mainEffort.Axis = Axis.Left;
                mainEffort.PrimaryFormation = army.GetFormation(FormationClass.Infantry, 0);
                mainEffort.SupportingCavalry = army.GetCavalryFormations();
                mainEffort.ReinforcedWith = army.GetBestTroops(0.3f); // Top 30% go here
                break;
                
            case PlanType.CenterPunch:
                mainEffort.Axis = Axis.Center;
                mainEffort.PrimaryFormation = army.GetFormation(FormationClass.Infantry, 1);
                mainEffort.SupportingFormation = army.GetReserve();
                mainEffort.ReinforcedWith = army.GetBestTroops(0.4f); // Top 40% go here
                break;
                
            case PlanType.DoubleEnvelopment:
                mainEffort.Axis = Axis.BothFlanks;
                mainEffort.LeftFormation = army.GetFormation(FormationClass.Infantry, 0);
                mainEffort.RightFormation = army.GetFormation(FormationClass.Infantry, 2);
                // Split best troops between both flanks
                break;
        }
        
        return mainEffort;
    }
    
    void ApplyMainEffort(MainEffort effort, Army army)
    {
        // Redistribute troops to reinforce main effort
        foreach (var troop in effort.ReinforcedWith)
        {
            MoveToFormation(troop, effort.PrimaryFormation);
        }
        
        // Main effort formation gets:
        // - More depth (extra ranks)
        // - Best troops in front
        // - Priority for reserves if needed
        effort.PrimaryFormation.SetDepth(4); // Deeper than normal
        effort.PrimaryFormation.Priority = FormationPriority.MainEffort;
    }
}
```

### Main Effort Characteristics

| Aspect | Main Effort Formation | Supporting Formation |
|--------|----------------------|---------------------|
| **Troop Quality** | Best troops | Average/lower |
| **Depth** | 4+ ranks | 2-3 ranks |
| **Reserve Priority** | First to receive | Last to receive |
| **Objective** | Attack/breakthrough | Pin/hold/screen |
| **Cavalry Support** | Yes | Only if needed |

---

## 14.5 Formation Objective Assignment

Each formation gets a specific objective, not just a behavior:

```csharp
enum FormationObjective
{
    Attack,         // Advance and engage aggressively
    Pin,            // Engage to hold enemy in place, don't push
    Screen,         // Delay/protect, don't get decisively engaged
    Breakthrough,   // Punch through enemy line
    Flank,          // Maneuver to enemy flank
    RearAttack,     // Get behind enemy and attack
    Pursue,         // Chase and destroy fleeing enemies
    Hold,           // Defend position, don't advance
    FightingRetreat // Fall back while fighting
}

class ObjectiveAssigner
{
    void AssignObjectives(BattlePlan plan, List<Formation> formations)
    {
        switch (plan.Type)
        {
            case PlanType.LeftHook:
                // Left infantry: ATTACK (main effort)
                SetObjective(formations[0], FormationObjective.Attack, "Lead attack on enemy left flank");
                
                // Center infantry: PIN
                SetObjective(formations[1], FormationObjective.Pin, "Engage center, hold them in place");
                
                // Right infantry: SCREEN
                SetObjective(formations[2], FormationObjective.Screen, "Protect right flank, don't advance");
                
                // Cavalry: FLANK → REAR ATTACK (sequential)
                SetObjective(formations.Cavalry, FormationObjective.Flank, 
                    "Eliminate enemy archers, then attack enemy rear");
                
                // Archers: Support main effort
                SetObjective(formations.Archers, FormationObjective.Attack,
                    "Focus fire on enemy left flank");
                break;
                
            case PlanType.HammerAnvil:
                // Infantry: HOLD (anvil)
                SetObjective(formations.Infantry, FormationObjective.Hold,
                    "Engage enemy, hold them in place, don't push");
                
                // Cavalry: FLANK → REAR ATTACK (hammer)
                SetObjective(formations.Cavalry, FormationObjective.RearAttack,
                    "Wide flank, attack enemy rear when infantry engaged");
                break;
        }
    }
    
    void SetObjective(Formation formation, FormationObjective objective, string description)
    {
        formation.CurrentObjective = objective;
        formation.ObjectiveDescription = description;
        
        // Objective affects behavior weights
        ApplyObjectiveBehaviors(formation, objective);
    }
    
    void ApplyObjectiveBehaviors(Formation formation, FormationObjective objective)
    {
        switch (objective)
        {
            case FormationObjective.Attack:
                formation.AI.SetBehaviorWeight<BehaviorAdvance>(1.2f);
                formation.AI.SetBehaviorWeight<BehaviorTacticalCharge>(1.0f);
                break;
                
            case FormationObjective.Pin:
                formation.AI.SetBehaviorWeight<BehaviorAdvance>(0.8f);
                formation.AI.SetBehaviorWeight<BehaviorDefend>(1.0f);
                // Engage but don't push hard
                break;
                
            case FormationObjective.Screen:
                formation.AI.SetBehaviorWeight<BehaviorDefend>(1.2f);
                formation.AI.SetBehaviorWeight<BehaviorSkirmish>(1.0f);
                formation.AI.SetBehaviorWeight<BehaviorAdvance>(0.3f);
                // Stay back, don't get decisively engaged
                break;
                
            case FormationObjective.Hold:
                formation.AI.SetBehaviorWeight<BehaviorDefend>(1.5f);
                formation.AI.SetBehaviorWeight<BehaviorAdvance>(0.0f);
                formation.SetMovementOrder(MovementOrder.Stop);
                break;
                
            case FormationObjective.Flank:
                formation.AI.SetBehaviorWeight<BehaviorFlank>(1.5f);
                formation.AI.Side = plan.MainEffortSide;
                break;
        }
    }
}
```

---

## 14.6 Sequential Objectives (Phases)

Some objectives have phases — "do X, then do Y":

```csharp
class SequentialObjective
{
    List<ObjectivePhase> Phases;
    int CurrentPhaseIndex = 0;
    
    public ObjectivePhase CurrentPhase => Phases[CurrentPhaseIndex];
    
    public void CheckPhaseTransition(Formation formation, BattleState state)
    {
        if (CurrentPhase.IsComplete(formation, state))
        {
            CurrentPhaseIndex++;
            if (CurrentPhaseIndex < Phases.Count)
            {
                ApplyPhase(formation, CurrentPhase);
                Log($"{formation.Name}: Phase {CurrentPhaseIndex} - {CurrentPhase.Description}");
            }
        }
    }
}

class CavalrySequentialObjective : SequentialObjective
{
    public CavalrySequentialObjective()
    {
        Phases = new List<ObjectivePhase>
        {
            new ObjectivePhase
            {
                Name = "Eliminate Archers",
                Objective = FormationObjective.Attack,
                Target = TargetType.EnemyArchers,
                CompletionCondition = (f, s) => s.EnemyArcherStrength < 0.3f, // 70% destroyed
                Description = "Charge and destroy enemy archers"
            },
            new ObjectivePhase
            {
                Name = "Rear Attack",
                Objective = FormationObjective.RearAttack,
                Target = TargetType.EnemyRear,
                CompletionCondition = (f, s) => s.EnemyRouting,
                Description = "Swing around and attack enemy rear"
            },
            new ObjectivePhase
            {
                Name = "Pursue",
                Objective = FormationObjective.Pursue,
                Target = TargetType.FleeingEnemies,
                CompletionCondition = (f, s) => s.BattleOver,
                Description = "Chase down fleeing enemies"
            }
        };
    }
}

// Usage in battle tick:
void UpdateCavalryObjective(Formation cavalry, BattleState state)
{
    var objective = cavalry.SequentialObjective as CavalrySequentialObjective;
    
    // Check if current phase is complete
    objective.CheckPhaseTransition(cavalry, state);
    
    // Execute current phase
    switch (objective.CurrentPhase.Objective)
    {
        case FormationObjective.Attack:
            var archers = FindEnemyArchers(state);
            if (archers != null)
                cavalry.SetMovementOrder(MovementOrder.ChargeToTarget(archers));
            break;
            
        case FormationObjective.RearAttack:
            var rearPosition = CalculateEnemyRear(state);
            cavalry.SetMovementOrder(MovementOrder.Move(rearPosition));
            // When in position, charge
            if (IsInRearPosition(cavalry, state))
                cavalry.SetMovementOrder(MovementOrder.Charge);
            break;
            
        case FormationObjective.Pursue:
            cavalry.SetMovementOrder(MovementOrder.Charge);
            break;
    }
}
```

### Common Sequential Objectives

| Formation | Phase 1 | Phase 2 | Phase 3 |
|-----------|---------|---------|---------|
| **Cavalry (Archer Hunt)** | Charge archers | Rear attack | Pursue |
| **Cavalry (Flank)** | Wide flank | Charge flank | Roll up line |
| **Reserve** | Hold position | Exploit breach | Pursue/mop up |
| **Screening Force** | Skirmish/delay | Fall back | Rejoin main line |

---

## 14.7 Cavalry Tasking

Cavalry gets specific, high-value targets:

```csharp
enum CavalryTask
{
    ScreenFlanks,       // Protect our flanks from their cavalry
    DestroyArchers,     // Eliminate enemy ranged threat
    FlankInfantry,      // Hit infantry from the side
    RearAttack,         // Circle around, hit from behind
    CounterCavalry,     // Engage enemy cavalry
    PursueRouters,      // Chase fleeing enemies
    Reserve             // Hold until opportunity
}

class CavalryTasker
{
    CavalryTask AssignCavalryTask(Formation cavalry, BattlePlan plan, BattleState state)
    {
        // Priority 1: Counter enemy cavalry threat to our archers
        if (EnemyCavalryThreateningOurArchers(state))
            return CavalryTask.CounterCavalry;
        
        // Priority 2: Execute plan's cavalry role
        switch (plan.CavalryRole)
        {
            case CavalryRole.ArcherHunter:
                if (EnemyArchersExist(state) && !EnemyArchersProtected(state))
                    return CavalryTask.DestroyArchers;
                else
                    return CavalryTask.FlankInfantry; // Fallback
                    
            case CavalryRole.Flanker:
                if (EnemyFlankExposed(state, plan.MainEffortSide))
                    return CavalryTask.FlankInfantry;
                else
                    return CavalryTask.Reserve; // Wait for opening
                    
            case CavalryRole.Hammer:
                if (InfantryEngaged(state))
                    return CavalryTask.RearAttack;
                else
                    return CavalryTask.Reserve; // Wait for anvil to engage
        }
        
        // Priority 3: Pursuit if enemy routing
        if (state.EnemyRouting)
            return CavalryTask.PursueRouters;
        
        return CavalryTask.Reserve;
    }
    
    void ExecuteCavalryTask(Formation cavalry, CavalryTask task, BattleState state)
    {
        switch (task)
        {
            case CavalryTask.DestroyArchers:
                var archers = state.GetEnemyArchers();
                var chargePosition = CalculateChargeApproach(cavalry, archers);
                
                if (DistanceTo(cavalry, chargePosition) > ChargeDistance)
                    cavalry.SetMovementOrder(MovementOrder.Move(chargePosition));
                else
                    cavalry.SetMovementOrder(MovementOrder.ChargeToTarget(archers));
                break;
                
            case CavalryTask.RearAttack:
                var rearPos = CalculateEnemyRear(state);
                
                if (!IsInPosition(cavalry, rearPos))
                {
                    // Move to rear position (wide flanking)
                    var flankRoute = CalculateFlankingRoute(cavalry, rearPos);
                    cavalry.SetMovementOrder(MovementOrder.Move(flankRoute.NextWaypoint));
                }
                else
                {
                    // In position — charge!
                    cavalry.SetMovementOrder(MovementOrder.Charge);
                }
                break;
                
            case CavalryTask.Reserve:
                cavalry.SetMovementOrder(MovementOrder.Stop);
                cavalry.SetFacingOrder(FacingOrder.LookAtEnemy);
                // Wait for opportunity
                break;
        }
    }
}
```

---

## 14.8 Screening and Refusing Flanks

When you don't want to fight on a flank, you **screen** or **refuse** it:

```csharp
class FlankRefusal
{
    void SetupRefusedFlank(Formation formation, Side side, BattleContext context)
    {
        // Refused flank: Angle formation back, minimal troops
        formation.Role = FormationRole.Screen;
        formation.Objective = FormationObjective.Screen;
        
        // Position: Angled back from main line
        Vec2 mainLineEnd = GetMainLineEnd(side);
        Vec2 refusedPosition = mainLineEnd + GetRefusalOffset(side, 30f); // 30m back
        formation.SetPositionTarget(refusedPosition);
        
        // Facing: Toward enemy, but ready to fall back
        formation.SetFacingOrder(FacingOrder.LookAtEnemy);
        
        // Arrangement: Defensive
        if (formation.HasEnoughShields())
            formation.SetArrangementOrder(ArrangementOrder.ShieldWall);
        
        // Behavior: Don't advance, just hold
        formation.AI.SetBehaviorWeight<BehaviorAdvance>(0.0f);
        formation.AI.SetBehaviorWeight<BehaviorDefend>(1.5f);
        formation.AI.SetBehaviorWeight<BehaviorSkirmish>(1.0f);
    }
    
    void ScreeningBehavior(Formation screen, BattleState state)
    {
        // Screening force: Delay, don't get destroyed
        
        float localPowerRatio = screen.QuerySystem.LocalPowerRatio;
        
        if (localPowerRatio < 0.5f)
        {
            // Heavily outnumbered locally — fall back
            Vec2 fallbackPos = GetFallbackPosition(screen, 20f);
            screen.SetMovementOrder(MovementOrder.Move(fallbackPos));
        }
        else if (localPowerRatio < 0.8f)
        {
            // Outnumbered — skirmish, don't engage
            screen.AI.SetBehaviorWeight<BehaviorSkirmish>(1.5f);
        }
        else
        {
            // Can hold — defend position
            screen.AI.SetBehaviorWeight<BehaviorDefend>(1.2f);
        }
        
        // NEVER pursue — stay in screening position
        if (screen.IsAdvancing())
        {
            screen.SetMovementOrder(MovementOrder.Stop);
        }
    }
}
```

### Screen vs Hold vs Attack

| Objective | Behavior | Engagement | Movement |
|-----------|----------|------------|----------|
| **Screen** | Delay, don't get destroyed | Light, break off if pressed | Fall back if needed |
| **Hold** | Defend position | Full engagement | Don't advance or retreat |
| **Pin** | Keep enemy occupied | Engage but don't push | Minimal advance |
| **Attack** | Destroy enemy | Aggressive | Advance continuously |

---

## 14.9 Plan Adaptation

Plans fail. The orchestrator detects failure and adapts:

```csharp
class PlanAdaptation
{
    enum PlanStatus { Executing, Succeeding, Stalled, Failing, Failed }
    
    PlanStatus EvaluatePlanStatus(BattlePlan plan, BattleState state)
    {
        float progressScore = 0f;
        
        // Check main effort progress
        var mainEffort = plan.MainEffort;
        if (mainEffort.Formation.IsAdvancing)
            progressScore += 0.3f;
        if (mainEffort.CasualtiesLow())
            progressScore += 0.2f;
        if (mainEffort.GainingGround())
            progressScore += 0.3f;
        
        // Check if plan objectives being met
        if (plan.Type == PlanType.LeftHook)
        {
            if (EnemyLeftFlankCollapsing(state))
                progressScore += 0.5f;
            if (CavalryCompletedArcherDestruction(state))
                progressScore += 0.3f;
        }
        
        // Check for problems
        if (mainEffort.Formation.CasualtyRatio > 0.3f)
            progressScore -= 0.4f;
        if (mainEffort.Formation.IsStalled())
            progressScore -= 0.3f;
        if (ScreeningFlankCollapsing(state))
            progressScore -= 0.5f;
        
        // Determine status
        if (progressScore > 0.6f) return PlanStatus.Succeeding;
        if (progressScore > 0.2f) return PlanStatus.Executing;
        if (progressScore > -0.2f) return PlanStatus.Stalled;
        if (progressScore > -0.5f) return PlanStatus.Failing;
        return PlanStatus.Failed;
    }
    
    void AdaptPlan(BattlePlan plan, PlanStatus status, BattleState state)
    {
        switch (status)
        {
            case PlanStatus.Succeeding:
                // Commit reserves to exploit success
                if (plan.Reserve != null)
                    CommitReserveToMainEffort(plan);
                break;
                
            case PlanStatus.Stalled:
                // Reinforce main effort or find new approach
                if (CanReinforce(plan.MainEffort))
                    ReinforceMainEffort(plan);
                else
                    ShiftMainEffort(plan, FindBetterAxis(state));
                break;
                
            case PlanStatus.Failing:
                // Main effort in trouble — reinforce or abort
                if (plan.Reserve != null)
                    CommitReserveToRescue(plan);
                else
                    TransitionToDefensive(plan);
                break;
                
            case PlanStatus.Failed:
                // Plan has failed — new plan needed
                BattlePlan newPlan = SelectFallbackPlan(plan, state);
                TransitionToPlan(newPlan);
                break;
        }
    }
    
    BattlePlan SelectFallbackPlan(BattlePlan failedPlan, BattleState state)
    {
        // If attack failed, go defensive
        if (failedPlan.IsOffensive)
        {
            if (state.OurPowerRatio > 0.6f)
                return new BattlePlan(PlanType.RefusedFlank); // Regroup and try again
            else
                return new BattlePlan(PlanType.Delay); // We're losing, delay
        }
        
        // If delay failed, coordinate retreat
        return new BattlePlan(PlanType.OrganizedWithdrawal);
    }
}
```

### Adaptation Triggers

| Trigger | Detection | Response |
|---------|-----------|----------|
| **Main effort stalled** | No ground gained for 30 seconds | Reinforce or shift axis |
| **Screening flank collapsing** | Screen at 40%+ casualties | Commit reserve to stabilize |
| **Cavalry mission failed** | Archers still effective after 60 seconds | Redirect cavalry to flank |
| **Breakthrough achieved** | Gap in enemy line | Commit reserve through gap |
| **Enemy routing** | 30%+ enemy fleeing | Transition to pursuit |
| **We're losing** | 40%+ casualties, no progress | Transition to delay/retreat |

---

## 14.10 Plan Execution State Machine

The overall plan execution follows a state machine:

```csharp
enum PlanPhase
{
    Forming,        // Deploying into formation
    Advancing,      // Moving toward enemy
    Engaging,       // Main effort engaging
    Exploiting,     // Success — committing reserves
    Consolidating,  // Securing gains
    Pursuing,       // Enemy routing, chasing
    Adapting,       // Plan needs adjustment
    Withdrawing     // Retreat
}

class PlanExecutor
{
    BattlePlan CurrentPlan;
    PlanPhase CurrentPhase = PlanPhase.Forming;
    
    void Tick(BattleState state)
    {
        // Check for phase transitions
        PlanPhase newPhase = DeterminePhase(state);
        
        if (newPhase != CurrentPhase)
        {
            TransitionPhase(CurrentPhase, newPhase, state);
            CurrentPhase = newPhase;
        }
        
        // Execute current phase
        ExecutePhase(CurrentPhase, state);
    }
    
    PlanPhase DeterminePhase(BattleState state)
    {
        // Plan status check
        PlanStatus status = EvaluatePlanStatus(CurrentPlan, state);
        
        if (status == PlanStatus.Failed)
            return PlanPhase.Adapting;
        
        if (state.EnemyRouting)
            return PlanPhase.Pursuing;
        
        if (CurrentPlan.MainEffort.Breakthrough)
            return PlanPhase.Exploiting;
        
        if (CurrentPlan.MainEffort.Engaged)
            return PlanPhase.Engaging;
        
        if (CurrentPlan.MainEffort.Advancing)
            return PlanPhase.Advancing;
        
        return PlanPhase.Forming;
    }
    
    void ExecutePhase(PlanPhase phase, BattleState state)
    {
        switch (phase)
        {
            case PlanPhase.Forming:
                // All formations move to starting positions
                // Don't advance until formed
                EnsureFormationsInPosition();
                break;
                
            case PlanPhase.Advancing:
                // Main effort advances
                // Supporting formations move to support positions
                // Screen stays in position
                AdvanceMainEffort();
                PositionSupportingFormations();
                break;
                
            case PlanPhase.Engaging:
                // Main effort engaged with enemy
                // Watch for breakthrough or failure
                // Cavalry executes its task
                MonitorMainEffort();
                ExecuteCavalryTask();
                UpdateSupportingFormations();
                break;
                
            case PlanPhase.Exploiting:
                // Breakthrough achieved!
                // Commit reserves through gap
                // Cavalry to rear
                CommitReservesToExploit();
                CavalryToRear();
                break;
                
            case PlanPhase.Pursuing:
                // Enemy routing
                // Cavalry pursues
                // Infantry mops up
                CavalryPursue();
                InfantryMopUp();
                break;
                
            case PlanPhase.Adapting:
                // Plan failed — select new plan
                SelectNewPlan(state);
                break;
        }
    }
}
```

---

---

## 14.11 Tactical Adaptation vs Flip-Flopping

**Goal: AI should adapt to battlefield changes, but not ping-pong back and forth indecisively.**

The difference between good adaptation and bad flip-flopping:
- **Good**: Enemy commits cavalry to left, we shift reserves to counter → **legitimate response**
- **Bad**: We advance, enemy defends, we defend, enemy advances, we advance, enemy defends... → **ping-ponging**

### The Flip-Flop Problem

```
BAD AI (Flip-Flopping):
┌──────────────────────────────────────────────────────────┐
│  0:00 - We pick "Offensive Push"                         │
│  0:05 - Enemy picks "Defensive Line" (responds to us)    │
│  0:10 - We switch to "Defensive" (respond to them)       │
│  0:15 - Enemy switches to "Offensive" (respond to us)    │
│  0:20 - We switch to "Offensive" (respond to them)       │
│  → Repeat forever, no one commits, no battle happens     │
└──────────────────────────────────────────────────────────┘

GOOD AI (Adaptive):
┌──────────────────────────────────────────────────────────┐
│  0:00 - We pick "Offensive Push" → Commit for 30-45s     │
│  0:30 - Enemy cavalry threatens our flank (REAL CHANGE)  │
│       → Shift reserve to counter (adapt within plan)     │
│  1:00 - Main effort taking 40% casualties (REAL PROBLEM) │
│       → Change to "Defensive Hold" (justified switch)    │
│  2:00 - Enemy starts routing (PHASE CHANGE)              │
│       → Change to "Pursuit" (natural progression)        │
└──────────────────────────────────────────────────────────┘
```

### Solution: Phase-Based Commitment Windows

```csharp
class AdaptiveTacticalManager
{
    BattlePlan CurrentPlan;
    BattlePhase CurrentPhase;
    float PlanStartTime;
    float LastPlanChangeTime;
    List<PlanChange> RecentChanges = new();
    
    const float MinimumCommitmentTime = 30f;     // 30 seconds minimum
    const float PlanChangeCooldown = 20f;        // 20 seconds between changes
    const float FlipFlopWindow = 60f;            // Track changes in last 60s
    const int MaxChangesInWindow = 2;            // Max 2 changes per 60s
    
    bool CanChangePlan(BattleState state)
    {
        float timeSincePlanStart = state.CurrentTime - PlanStartTime;
        float timeSinceLastChange = state.CurrentTime - LastPlanChangeTime;
        
        // Rule 1: Minimum commitment time (30 seconds)
        if (timeSincePlanStart < MinimumCommitmentTime)
        {
            // Exception: Catastrophic failure
            if (IsCatastrophicFailure(state))
                return true;
            
            // Exception: Phase transition (phase gates allow switches)
            if (IsPhaseTransition(state))
                return true;
            
            return false;  // Too soon, stick with plan
        }
        
        // Rule 2: Cooldown between changes (prevent rapid switching)
        if (timeSinceLastChange < PlanChangeCooldown)
            return false;
        
        // Rule 3: Flip-flop detection (too many recent changes)
        if (IsFlipFlopping(state))
            return false;  // Locked out, stop flip-flopping
        
        return true;
    }
    
    bool IsFlipFlopping(BattleState state)
    {
        // Count plan changes in last 60 seconds
        int recentChanges = RecentChanges.Count(c => 
            state.CurrentTime - c.Time < FlipFlopWindow);
        
        return recentChanges >= MaxChangesInWindow;
    }
    
    bool IsPhaseTransition(BattleState state)
    {
        // Phase transitions allow plan changes
        // Phases naturally gate when tactics can shift
        
        BattlePhase newPhase = DetermineBattlePhase(state);
        
        if (newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            return true;
        }
        
        return false;
    }
    
    BattlePhase DetermineBattlePhase(BattleState state)
    {
        // Phases act as natural commitment boundaries
        
        if (!state.BattleStarted)
            return BattlePhase.Deployment;
        
        bool linesEngaged = state.FormationsInMelee > 0;
        bool routingStarted = state.EnemyRoutingFormations > 0;
        bool catastrophicLosses = state.CasualtyRatio > 0.5f;
        
        if (catastrophicLosses && !state.CanWin)
            return BattlePhase.Retreat;
        
        if (routingStarted)
            return BattlePhase.Pursuit;
        
        if (linesEngaged)
            return BattlePhase.MainEngagement;
        
        if (state.AverageDistance < 50f)
            return BattlePhase.Contact;
        
        if (state.AverageDistance < 150f)
            return BattlePhase.Approach;
        
        return BattlePhase.Positioning;
    }
}

enum BattlePhase
{
    Deployment,      // Setting up formations (plan selection allowed)
    Positioning,     // Moving to engagement (plan locked)
    Approach,        // Closing distance (plan locked, can adapt)
    Contact,         // Skirmishing begins (can change if needed)
    MainEngagement,  // Lines engaged (locked unless catastrophic)
    Pursuit,         // Enemy routing (pursuit mode)
    Retreat          // We're retreating (survival mode)
}
```

### When Plan Changes Are Allowed

| Situation | Can Change? | Why |
|-----------|-------------|-----|
| **< 30 seconds into plan** | ❌ No | Too soon, give plan a chance |
| **< 20 seconds since last change** | ❌ No | Cooldown prevents rapid switching |
| **2+ changes in last 60 seconds** | ❌ No | Flip-flop detected, locked out |
| **Phase transition** | ✅ Yes | Natural battlefield progression |
| **Catastrophic failure** | ✅ Yes | 40%+ casualties, encircled, plan failed |
| **After 30s + legitimate change** | ✅ Yes | But must pass hysteresis check |

### Phase-Based Plan Selection

```csharp
void UpdateBattlePlan(BattleState state)
{
    BattlePhase phase = DetermineBattlePhase(state);
    
    // Different phases allow different plan flexibility
    switch (phase)
    {
        case BattlePhase.Deployment:
            // Free to pick initial plan
            if (CurrentPlan == null)
                CurrentPlan = SelectInitialPlan(state);
            break;
            
        case BattlePhase.Positioning:
            // LOCKED - committed to initial plan
            // Can adapt formations but not change overall strategy
            AdaptFormationsWithinPlan(state);
            break;
            
        case BattlePhase.Approach:
            // Can change if enemy does something unexpected
            if (EnemyTacticalShift(state) && CanChangePlan(state))
                ConsiderPlanChange(state);
            break;
            
        case BattlePhase.Contact:
            // Skirmishing - can still adjust before main commitment
            if (NeedTacticalAdjustment(state) && CanChangePlan(state))
                ConsiderPlanChange(state);
            break;
            
        case BattlePhase.MainEngagement:
            // MOSTLY LOCKED - lines engaged, too late to change strategy
            // Only allow change for catastrophic failure
            if (IsCatastrophicFailure(state) && CanChangePlan(state))
                FallbackPlan(state);
            break;
            
        case BattlePhase.Pursuit:
            // Switch to pursuit tactics
            CurrentPlan = new PursuitPlan();
            break;
            
        case BattlePhase.Retreat:
            // Switch to retreat tactics
            CurrentPlan = new RetreatPlan();
            break;
    }
}
```

### Legitimate Reasons to Change Plans

```csharp
bool IsLegitimateReasonToChange(BattleState state, BattlePlan proposedPlan)
{
    // 1. Phase transition (natural progression)
    if (IsPhaseTransition(state))
        return true;
    
    // 2. Catastrophic failure of current plan
    if (IsCatastrophicFailure(state))
        return true;
    
    // 3. Major enemy tactical shift (not just normal moves)
    if (EnemyMajorTacticalShift(state))
    {
        // Enemy committed cavalry reserve to our flank
        // Enemy switched from defense to all-out assault
        // Enemy revealed hidden reserves
        return true;
    }
    
    // 4. Opportunity that wasn't present at plan start
    if (NewMajorOpportunity(state))
    {
        // Enemy flank collapsed unexpectedly
        // Enemy commander killed
        // Gap opened in enemy line
        return true;
    }
    
    // 5. Current plan is clearly failing (after minimum time)
    if (PlanFailingBadly(state))
    {
        float timeSincePlanStart = state.CurrentTime - PlanStartTime;
        if (timeSincePlanStart > MinimumCommitmentTime)
        {
            // Main effort repulsed, casualties mounting, no progress
            return true;
        }
    }
    
    return false;
}

bool EnemyMajorTacticalShift(BattleState state)
{
    // Not just "they moved a formation"
    // But "they fundamentally changed their approach"
    
    // Committed reserve (was holding, now committed)
    if (state.Enemy.JustCommittedReserve)
        return true;
    
    // Changed from defensive to offensive (stance change)
    if (state.Enemy.StanceChanged && state.TimeSinceStanceChange < 10f)
        return true;
    
    // Opened a second axis of attack
    if (state.Enemy.NewAxisOpened)
        return true;
    
    return false;
}
```

### Prevent Both Sides From Flip-Flopping Together

```csharp
class CoordinatedFlipFlopPrevention
{
    // If enemy just changed plan, WE should commit longer
    // Don't let both sides ping-pong off each other
    
    float EnemyLastPlanChangeTime = -1000f;
    
    bool CanChangePlan(BattleState state)
    {
        // Base rules from above...
        if (!BasicCanChangePlan(state))
            return false;
        
        // Additional rule: If enemy JUST changed plan, we should commit
        float timeSinceEnemyChange = state.CurrentTime - EnemyLastPlanChangeTime;
        
        if (timeSinceEnemyChange < 30f)
        {
            // Enemy just changed - we should NOT change in response
            // Give their change time to develop
            return false;
        }
        
        return true;
    }
    
    void OnEnemyPlanChanged(BattleState state)
    {
        EnemyLastPlanChangeTime = state.CurrentTime;
        
        // Enemy changed plan - extend OUR commitment window
        MinimumCommitmentTime = 45f;  // Longer commitment
    }
}
```

### Adaptation vs Plan Change

**Adaptation (within plan) - ALWAYS allowed:**
- Reinforce main effort with reserves
- Shift formations to counter threats
- Adjust facing/positioning
- Commit/hold reserves based on situation
- Change formation arrangements (line → shield wall)

**Plan Change - Gated by rules above:**
- Switch from Offensive → Defensive
- Switch from Flanking → Frontal Assault
- Switch main effort from left → right
- Abandon plan and retreat

### Summary: Anti-Flip-Flop Rules

| Rule | Purpose |
|------|---------|
| **30s minimum commitment** | Give plan time to work |
| **20s cooldown between changes** | Prevent rapid switching |
| **Max 2 changes per 60s** | Detect flip-flopping pattern |
| **Phase-based gating** | Allow changes at natural transitions |
| **Catastrophic exception** | Can emergency-change if plan failed |
| **Enemy change cooldown** | Don't respond to enemy's change immediately |
| **Hysteresis on scoring** | New plan must be significantly better |

### Phase Transition Examples

```
GOOD - Phase-based adaptation:
┌─────────────────────────────────────────────────────────┐
│  Deployment Phase (0-30s)                               │
│  → Pick "Flanking Assault" plan                         │
│  → Commit to it                                         │
│                                                         │
│  Positioning Phase (30s-90s)                            │
│  → Execute flanking movement                            │
│  → LOCKED (can't change mid-maneuver)                   │
│  → Adapt: Enemy cavalry appears, shift reserve          │
│                                                         │
│  Contact Phase (90s-120s)                               │
│  → Skirmishing begins                                   │
│  → Enemy committed to counter our flank (major shift)   │
│  → ALLOWED: Switch to "Pin & Breakthrough Center"       │
│  → New 30s commitment window starts                     │
│                                                         │
│  Main Engagement Phase (120s+)                          │
│  → Lines engaged                                        │
│  → LOCKED (too late to change strategy)                 │
│  → Adapt within plan only                               │
└─────────────────────────────────────────────────────────┘
```

**Result:**
- Tactics adapt to battlefield changes ✓
- No mindless flip-flopping ✓
- Natural commitment windows via phases ✓
- Emergency changes allowed when needed ✓
- Commit reserve
- Shift supporting formations
- Cavalry redirected to new target

**Plan change (new plan):**
- Attack axis changes (left → right)
- Offensive → defensive
- Attack → delay/withdraw

```csharp
void HandleSetback(BattleState state)
{
    PlanStatus status = EvaluatePlanStatus(CurrentPlan, state);
    
    if (status == PlanStatus.Stalled || status == PlanStatus.Failing)
    {
        // First: Try to ADAPT within the plan
        if (CanReinforce(CurrentPlan.MainEffort))
        {
            ReinforceMainEffort();  // Adaptation, not plan change
            return;
        }
        
        if (CurrentPlan.Reserve != null && !CurrentPlan.Reserve.Committed)
        {
            CommitReserve();  // Adaptation, not plan change
            return;
        }
        
        // Only if adaptations exhausted AND plan is truly failing
        // AND we've been at it for 90+ seconds
        if (CanChangePlan(state))
        {
            SelectNewPlan(state);
        }
        else
        {
            // Can't change yet — keep trying with adaptations
            Log("Plan struggling but committed. Continuing execution.");
        }
    }
}
```

---

## 14.12 Enemy Composition Recognition

Before choosing a plan, recognize WHAT you're fighting. Numerical advantage means nothing if you can't catch horse archers.

### Threat Type Classification

```csharp
enum EnemyThreatType
{
    InfantryHorde,      // Lots of infantry, few archers/cavalry
    ArcherHeavy,        // Ranged focus, will kite and shoot
    CavalryHeavy,       // Mobile, will flank and charge
    HorseArchers,       // SPECIAL: Will kite endlessly, hard to catch
    Balanced,           // Mixed composition
    EliteSmall,         // Few but high-quality troops
    PikeWall,           // Anti-cavalry formation
    ShieldWall          // Defensive infantry
}

class EnemyAnalyzer
{
    EnemyThreatType ClassifyEnemy(Army enemy)
    {
        float infantryRatio = enemy.InfantryCount / (float)enemy.TotalCount;
        float archerRatio = enemy.ArcherCount / (float)enemy.TotalCount;
        float cavalryRatio = enemy.CavalryCount / (float)enemy.TotalCount;
        float horseArcherRatio = enemy.HorseArcherCount / (float)enemy.TotalCount;
        float averageTier = enemy.AverageTroopTier;
        
        // Special case: Horse archers
        if (horseArcherRatio > 0.3f)
            return EnemyThreatType.HorseArchers;
        
        // Pike/spear heavy
        if (enemy.PikeCount > enemy.TotalCount * 0.3f)
            return EnemyThreatType.PikeWall;
        
        // Shield wall (lots of shields, defensive stance)
        if (enemy.ShieldRatio > 0.6f && enemy.IsDefensive)
            return EnemyThreatType.ShieldWall;
        
        // Composition-based
        if (cavalryRatio > 0.4f)
            return EnemyThreatType.CavalryHeavy;
        if (archerRatio > 0.4f)
            return EnemyThreatType.ArcherHeavy;
        if (infantryRatio > 0.6f && averageTier < 3f)
            return EnemyThreatType.InfantryHorde;
        if (enemy.TotalCount < 50 && averageTier > 4f)
            return EnemyThreatType.EliteSmall;
        
        return EnemyThreatType.Balanced;
    }
}
```

### Counter-Strategy by Enemy Type

| Enemy Type | Problem | Counter-Strategy |
|------------|---------|------------------|
| **Horse Archers** | Can't catch them, they kite forever | Use terrain, circle formation, wait for them to close |
| **Archer Heavy** | Will shoot you to pieces | Close FAST, use shields, or counter with own archers |
| **Cavalry Heavy** | Will flank and rear-charge | Spears ready, refuse flanks, compact formation |
| **Infantry Horde** | Numbers overwhelm | Quality over quantity, concentrated force |
| **Pike Wall** | Can't charge | Archers, flank, or wait them out |
| **Shield Wall** | Hard to break | Flank, archers from angle, don't frontal assault |
| **Elite Small** | Dangerous individuals | Swarm with numbers, don't duel |
| **Balanced** | Flexible threat | Standard tactics apply |

### Horse Archer Special Handling

Horse archers are a SPECIAL problem. They require specific tactics:

```csharp
class HorseArcherCounter
{
    BattlePlan CreateHorseArcherCounterPlan(Army us, Army enemy, Terrain terrain)
    {
        var plan = new BattlePlan(PlanType.AntiHorseArcher);
        
        // Key insight: You can't chase horse archers
        // You need to MAKE THEM come to you or corner them
        
        // Option 1: Use terrain to limit their movement
        var cornerPosition = FindTerrainCorner(terrain);
        if (cornerPosition != null)
        {
            plan.Strategy = "Back against terrain, force them to engage";
            plan.InitialPosition = cornerPosition;
            plan.Formation = FormationType.DefensiveArc;
        }
        // Option 2: Circle formation (all-around defense)
        else if (us.InfantryCount > 60)
        {
            plan.Strategy = "Circle formation, archers in center";
            plan.Formation = FormationType.Circle;
            plan.ArcherPosition = ArcherPosition.Center;
        }
        // Option 3: If we have faster cavalry, hunt them
        else if (OurCavalryFasterThanTheirs(us, enemy))
        {
            plan.Strategy = "Cavalry hunts horse archers, infantry holds";
            plan.CavalryTask = CavalryTask.HuntHorseArchers;
        }
        // Option 4: Endurance — wait them out, they'll run out of arrows
        else
        {
            plan.Strategy = "Shields up, wait for arrows to run out";
            plan.Formation = FormationType.ShieldWall;
            plan.Posture = BattlePosture.Defensive;
        }
        
        return plan;
    }
    
    Vec2? FindTerrainCorner(Terrain terrain)
    {
        // Look for positions where horse archers can't circle us:
        // - Back to cliff
        // - Back to forest (they can't ride through)
        // - Narrow passage
        // - River on one side
        
        foreach (var feature in terrain.Features)
        {
            if (feature.Type == TerrainType.Cliff || 
                feature.Type == TerrainType.Forest ||
                feature.Type == TerrainType.River)
            {
                // Position with back to this feature
                return CalculateDefensivePosition(feature);
            }
        }
        
        return null;  // Open field — no corner available
    }
}
```

---

## 14.13 Defensive Counter-Formations

When facing certain threats, you need specific defensive formations:

### Circle Formation (vs Horse Archers / Surrounded)

```
                CIRCLE FORMATION
                
                     ▪▪▪▪▪▪▪▪
                  ▪▪          ▪▪
                ▪▪  ○○○○○○○○  ▪▪
               ▪▪   ○ ARCHERS ○   ▪▪
               ▪▪   ○  HERE   ○   ▪▪
                ▪▪  ○○○○○○○○  ▪▪
                  ▪▪          ▪▪
                     ▪▪▪▪▪▪▪▪
                     
    ▪ = Infantry (shields out)
    ○ = Archers (protected in center)
    
    USE WHEN:
    - Horse archers circling you
    - Surrounded by cavalry
    - Need all-around defense
    
    ADVANTAGE:
    - No flank to exploit
    - Archers protected and can shoot in any direction
    
    DISADVANTAGE:
    - Can't advance
    - Thin everywhere (no concentration)
```

### Square Formation (vs Cavalry Charges)

```
                SQUARE FORMATION
                
                ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
                ▪              ▪
                ▪  ○○○○○○○○○○  ▪
                ▪  ○ ARCHERS ○  ▪
                ▪  ○○○○○○○○○○  ▪
                ▪              ▪
                ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
                
    USE WHEN:
    - Heavy cavalry threat
    - Need to move slowly while protected
    
    SPEARS ON CORNERS:
    - Best anti-cavalry troops at corners
    - Cavalry charges at corners get impaled
```

### Terrain-Anchored Formation

```
                TERRAIN-ANCHORED
                
        [CLIFF/FOREST/RIVER]
        ═══════════════════════
                    │
                    ▼
              ▪▪▪▪▪▪▪▪▪▪▪▪
              ▪▪▪▪▪▪▪▪▪▪▪▪
              ▪▪▪▪▪▪▪▪▪▪▪▪
              
              ○○○○○○○○○○○○  (archers behind)
              
    USE WHEN:
    - Fighting horse archers
    - Outnumbered and need to refuse flanks
    - Terrain available
    
    ADVANTAGE:
    - Back is protected
    - Can't be circled
    - Horse archers must attack from front
```

### Implementation

```csharp
class DefensiveFormationSelector
{
    FormationType SelectDefensiveFormation(EnemyThreatType threat, Terrain terrain, Army us)
    {
        switch (threat)
        {
            case EnemyThreatType.HorseArchers:
                // Priority: Terrain > Circle > Shield Wall
                if (HasUsableTerrain(terrain))
                    return FormationType.TerrainAnchored;
                else if (us.InfantryCount >= 60)
                    return FormationType.Circle;
                else
                    return FormationType.ShieldWall;
                    
            case EnemyThreatType.CavalryHeavy:
                // Square or Shield Wall with spears ready
                if (us.InfantryCount >= 80)
                    return FormationType.Square;
                else
                    return FormationType.ShieldWall;
                    
            case EnemyThreatType.ArcherHeavy:
                // Shield Wall and ADVANCE — close distance fast
                return FormationType.ShieldWallAdvancing;
                
            default:
                return FormationType.Line;
        }
    }
    
    void SetupCircleFormation(Army army)
    {
        Formation infantry = army.GetInfantryFormation();
        Formation archers = army.GetArcherFormation();
        
        // Infantry forms circle
        infantry.SetArrangementOrder(ArrangementOrder.Circle);
        infantry.SetFacingOrder(FacingOrder.FaceOutward);
        
        // Archers go to center
        if (archers != null && archers.CountOfUnits <= infantry.CountOfUnits * 0.5f)
        {
            // Only if archers will fit in center
            Vec2 center = infantry.CachedMedianPosition.AsVec2;
            archers.SetMovementOrder(MovementOrder.Move(center));
            archers.SetFacingOrder(FacingOrder.LookAtEnemy);
        }
        else
        {
            // Too many archers — integrate into circle
            infantry.TransferUnits(archers, archers.CountOfUnits);
        }
    }
    
    void SetupTerrainAnchoredFormation(Army army, TerrainFeature anchor)
    {
        // Position with back to terrain
        Vec2 position = CalculatePositionWithBackTo(anchor);
        Vec2 facing = (anchor.Position - position).Normalized(); // Face away from terrain
        facing = new Vec2(-facing.Y, facing.X); // Rotate 90° to face enemy
        
        foreach (var formation in army.Formations)
        {
            formation.SetMovementOrder(MovementOrder.Move(position));
            formation.SetFacingOrder(FacingOrder.FaceDirection(facing));
        }
        
        // Archers behind infantry, against the terrain
        if (army.HasArchers)
        {
            Vec2 archerPos = position + (anchor.Position - position).Normalized() * 20f;
            army.Archers.SetMovementOrder(MovementOrder.Move(archerPos));
        }
    }
}
```

---

## 14.14 Commitment Timing (When to Engage)

**Don't auto-engage.** The AI decides WHEN to commit based on the situation.

### Commitment Decision Factors

```csharp
class CommitmentDecider
{
    enum CommitmentDecision { Hold, Advance, FullCommit }
    
    CommitmentDecision ShouldCommit(BattlePlan plan, BattleState state)
    {
        // Factor 1: Is our formation ready?
        if (!AllFormationsInPosition())
            return CommitmentDecision.Hold;
        
        // Factor 2: Enemy composition — do we WANT to close?
        EnemyThreatType threat = ClassifyEnemy(state.Enemy);
        
        if (threat == EnemyThreatType.HorseArchers)
        {
            // Don't chase horse archers — let them come to us
            if (!EnemyInRange(state) && !HasTerrainAdvantage())
                return CommitmentDecision.Hold;
        }
        
        if (threat == EnemyThreatType.ArcherHeavy)
        {
            // CLOSE FAST — every second we wait, we take arrows
            return CommitmentDecision.FullCommit;
        }
        
        if (threat == EnemyThreatType.PikeWall)
        {
            // Don't charge pikes — maneuver first
            if (!FlankAvailable(state))
                return CommitmentDecision.Hold;
        }
        
        // Factor 3: Terrain — are we in a good position?
        if (HasTerrainAdvantage())
        {
            // We're on high ground / good position — let them come
            if (plan.Type != PlanType.Delay)
                return CommitmentDecision.Hold;
        }
        
        // Factor 4: Plan type
        if (plan.Type == PlanType.Delay)
            return CommitmentDecision.Hold;  // Never fully commit on delay
        
        if (plan.Type == PlanType.HammerAnvil && !CavalryInPosition())
            return CommitmentDecision.Hold;  // Wait for hammer
        
        // Factor 5: Who should advance first?
        if (state.WeAreAttacker)
            return CommitmentDecision.Advance;  // We started this, we advance
        else
            return CommitmentDecision.Hold;  // Defender waits
    }
}
```

### Wait vs Advance Decision Tree

```
                    SHOULD WE ADVANCE?
                           │
                           ▼
              ┌────────────────────────┐
              │  Formations in position? │
              └────────────────────────┘
                     │           │
                    NO          YES
                     │           │
                     ▼           ▼
                  [HOLD]   ┌────────────────────────┐
                           │  What are we fighting?  │
                           └────────────────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    │                 │                 │
               HORSE ARCHERS     ARCHERS          INFANTRY
                    │                 │                 │
                    ▼                 ▼                 ▼
               [HOLD]            [ADVANCE         [Check if
           (let them come)        FAST]           attacker]
                                (close gap)            │
                                                  ┌────┴────┐
                                                  │         │
                                              ATTACKER  DEFENDER
                                                  │         │
                                                  ▼         ▼
                                             [ADVANCE]  [HOLD]
```

### Horse Archer Engagement Logic

```csharp
void HandleHorseArcherBattle(BattleState state)
{
    EnemyThreatType threat = ClassifyEnemy(state.Enemy);
    
    if (threat != EnemyThreatType.HorseArchers)
        return;
    
    // Rule 1: DON'T CHASE
    // Chasing horse archers just tires your troops while they shoot you
    
    // Rule 2: Get defensive position
    var terrain = FindDefensivePosition(state);
    if (terrain != null)
    {
        MoveToPosition(terrain);
        SetupTerrainAnchoredFormation(terrain);
    }
    else
    {
        SetupCircleFormation();
    }
    
    // Rule 3: Wait for them to close
    // Horse archers eventually need to get closer to be effective
    // Or they run out of arrows
    
    // Rule 4: If we have cavalry, use it to hunt
    if (OurCavalry != null && OurCavalry.CountOfUnits > state.Enemy.HorseArcherCount * 0.5f)
    {
        // Send cavalry to hunt — they're the only ones who can catch them
        OurCavalry.SetObjective(CavalryTask.HuntHorseArchers);
    }
    
    // Rule 5: Infantry holds, doesn't chase
    foreach (var infantry in OurInfantryFormations)
    {
        infantry.SetMovementOrder(MovementOrder.Stop);
        infantry.SetArrangementOrder(ArrangementOrder.ShieldWall);
        // Don't advance, don't chase
    }
    
    // Rule 6: Archers counter-fire
    if (OurArchers != null)
    {
        OurArchers.SetTargetFormation(state.Enemy.HorseArchers);
        OurArchers.SetFiringOrder(FiringOrder.FireAtWill);
    }
}
```

### Commitment Summary

| Enemy Type | Commitment Decision | Reasoning |
|------------|---------------------|-----------|
| **Horse Archers** | HOLD | Don't chase, let them come or corner them |
| **Archer Heavy** | ADVANCE FAST | Every second = more arrows in your face |
| **Pike Wall** | HOLD (until flank available) | Frontal charge = death |
| **Cavalry Heavy** | HOLD (defensive formation) | Let them charge into your spears |
| **Infantry Horde** | Plan-dependent | Standard tactics |
| **Balanced** | Attacker advances, defender holds | Normal engagement |

---

## 14.15 Recognizing When Defense Is Killing You

**Critical:** Plan commitment doesn't mean "stand there and die." If the defensive plan is getting you slaughtered, you need to adapt.

### The Problem: Stubborn Defense

```
SCENARIO: Anti-Horse Archer Plan

    You: Circle formation, shields up, archers in center
    
    Expected: Horse archers run out of arrows, forced to close
    
    Reality: Their archers are MUCH better than yours
             Your troops dying at 3:1 ratio
             Waiting is just dying slower
             
    WRONG: Keep waiting (plan commitment!)
    RIGHT: Recognize defense is failing → switch to MAX CASUALTIES mode
```

### Bleed Rate Monitoring

The AI continuously monitors **how fast we're losing troops**:

```csharp
class BleedRateMonitor
{
    float CasualtiesPerMinute;
    float EnemyCasualtiesPerMinute;
    float BleedRatio;  // Our losses / Their losses
    
    void Update(BattleState state)
    {
        // Calculate casualties in last 60 seconds
        CasualtiesPerMinute = state.OurCasualtiesInLast60Seconds;
        EnemyCasualtiesPerMinute = state.EnemyCasualtiesInLast60Seconds;
        
        // Bleed ratio: < 1.0 means we're winning, > 1.0 means we're losing
        BleedRatio = CasualtiesPerMinute / Math.Max(1f, EnemyCasualtiesPerMinute);
    }
    
    bool DefenseIsFailing()
    {
        // Defense is failing if:
        // 1. We're losing troops faster than them (bleed ratio > 1.5)
        // 2. We're losing troops at unsustainable rate (> 10% per minute)
        
        float casualtyRatePercent = CasualtiesPerMinute / InitialTroopCount;
        
        return BleedRatio > 1.5f || casualtyRatePercent > 0.10f;
    }
    
    bool StrategyShouldChange()
    {
        // Consider strategy change if:
        // - Bleed ratio > 2.0 (losing 2:1)
        // - Total casualties > 30% while on defense
        // - No enemy casualties in 60 seconds while we're taking hits
        
        if (BleedRatio > 2.0f)
            return true;
        if (TotalCasualtyRatio > 0.3f && CurrentPosture == Posture.Defensive)
            return true;
        if (EnemyCasualtiesPerMinute < 1f && CasualtiesPerMinute > 5f)
            return true;  // We can't hurt them, they can hurt us
            
        return false;
    }
}
```

### Max Casualties Mode

When defense is failing and we're going to lose anyway, switch to **maximize enemy casualties**:

```csharp
class MaxCasualtiesMode
{
    void ActivateMaxCasualtiesMode(Army army, BattleState state)
    {
        Log("Defense failing. Switching to MAX CASUALTIES mode.");
        
        // Goal: We're probably going to lose, but take as many with us as possible
        
        // 1. Commit everything — no reserves, no holding back
        foreach (var formation in army.Formations)
        {
            formation.SetObjective(FormationObjective.Attack);
            formation.AI.SetBehaviorWeight<BehaviorCharge>(1.5f);
            formation.AI.SetBehaviorWeight<BehaviorDefend>(0.0f);
        }
        
        // 2. Cavalry: Charge their archers/ranged — even if suicidal
        // Those archers are killing us, take them with us
        if (army.Cavalry != null)
        {
            Formation enemyArchers = state.Enemy.GetArcherFormation();
            if (enemyArchers != null)
                army.Cavalry.SetMovementOrder(MovementOrder.ChargeToTarget(enemyArchers));
            else
                army.Cavalry.SetMovementOrder(MovementOrder.Charge);
        }
        
        // 3. Infantry: CHARGE — standing still is just dying
        foreach (var infantry in army.InfantryFormations)
        {
            infantry.SetMovementOrder(MovementOrder.Charge);
        }
        
        // 4. Archers: Keep shooting, but move forward with the charge
        if (army.Archers != null)
        {
            army.Archers.SetMovementOrder(MovementOrder.Advance);
            army.Archers.SetFiringOrder(FiringOrder.FireAtWill);
        }
    }
}
```

### Adaptation Triggers

```csharp
class DefenseAdaptation
{
    void MonitorDefensivePlan(BattlePlan plan, BattleState state)
    {
        if (plan.Posture != Posture.Defensive)
            return;  // Only applies to defensive plans
        
        var bleed = state.BleedRateMonitor;
        
        // TRIGGER 1: Bleed ratio terrible (losing 2:1 or worse)
        if (bleed.BleedRatio > 2.0f)
        {
            Log($"Bleed ratio {bleed.BleedRatio:F1}:1 against us. Defense failing.");
            ConsiderModeSwitch(plan, state);
        }
        
        // TRIGGER 2: High casualties while "safe" (shields up, etc.)
        if (plan.Type == PlanType.AntiHorseArcher && bleed.CasualtiesPerMinute > 8f)
        {
            Log("Taking heavy casualties in anti-horse-archer stance. Their archers are winning.");
            ConsiderModeSwitch(plan, state);
        }
        
        // TRIGGER 3: 30% casualties with no progress
        if (state.OurCasualtyRatio > 0.3f && !MakingProgress(state))
        {
            Log("30%+ casualties, no progress. Defense is just slow death.");
            ConsiderModeSwitch(plan, state);
        }
    }
    
    void ConsiderModeSwitch(BattlePlan plan, BattleState state)
    {
        float ourRemaining = 1.0f - state.OurCasualtyRatio;
        float theirRemaining = 1.0f - state.EnemyCasualtyRatio;
        
        // If we're still ahead, keep defending
        if (ourRemaining > theirRemaining * 1.2f)
        {
            Log("Still ahead overall. Continuing defense.");
            return;
        }
        
        // We're losing — switch to max casualties
        ActivateMaxCasualtiesMode(state.OurArmy, state);
    }
}
```

### Decision Matrix: When to Switch from Defense

| Condition | Continue Defense? | Switch to Attack? |
|-----------|-------------------|-------------------|
| Bleed ratio < 1.5 | ✅ Yes | ❌ No |
| Bleed ratio 1.5-2.0 | ⚠️ Monitor | Consider |
| Bleed ratio > 2.0 | ❌ No | ✅ Yes |
| 30%+ casualties, no enemy losses | ❌ No | ✅ Yes |
| Their archers winning duel badly | ❌ No | ✅ Charge them |
| Still have numerical advantage | ✅ Yes (be patient) | ❌ No |
| Enemy advancing into our position | ✅ Yes (plan working) | ❌ No |

### The Full Logic Flow

```
WHILE EXECUTING DEFENSIVE PLAN:
    │
    ├─ Monitor bleed rate every 10 seconds
    │
    ├─ IF bleed_ratio < 1.5:
    │      Continue defense (it's working)
    │
    ├─ IF bleed_ratio 1.5-2.0:
    │      Log warning, continue but prepare to adapt
    │
    ├─ IF bleed_ratio > 2.0 OR casualties > 30% with no progress:
    │      │
    │      ├─ Are we still ahead overall?
    │      │      YES → Continue defense (winning slowly is still winning)
    │      │      NO  → Switch to MAX CASUALTIES mode
    │      │
    │      └─ MAX CASUALTIES:
    │             - Commit everything
    │             - Cavalry charges their archers
    │             - Infantry charges
    │             - Take as many with us as possible
    │
    └─ IF enemy closes to melee range:
           Defense succeeded — transition to normal engagement
```

### Example: Horse Archers Winning

```
SCENARIO:
    You: 150 troops, circle formation, shields up
    Enemy: 80 horse archers (Khuzait, very good)
    
    T+0 min:   You: 150, Enemy: 80
    T+1 min:   You: 142, Enemy: 78   (bleed ratio 4:1 against you)
    T+2 min:   You: 130, Enemy: 76   (still losing badly)
    T+3 min:   You: 115, Enemy: 74   (30% casualties, enemy barely scratched)
    
    AI LOGIC:
    T+1 min: Bleed ratio 4:1 — WARNING
    T+2 min: Still 4:1, 13% casualties — CONSIDER SWITCH
    T+3 min: 23% casualties, enemy only 7.5% — SWITCH TO MAX CASUALTIES
    
    ACTION:
    - Cavalry charges horse archers (finally gets to fight them)
    - Infantry charges (at least they'll hit SOMETHING)
    - Stop standing around being shot
    
    OUTCOME:
    - You probably still lose
    - But you might take 30-40 of them with you instead of 10
    - More importantly: it's more FUN to watch than slow death
```

---

## Part 14 Summary

| Principle | Implementation |
|-----------|----------------|
| **Proactive, not just reactive** | Generate coherent battle plan before contact |
| **Plan types** | Left/Right Hook, Center Punch, Envelopment, Hammer/Anvil, Delay |
| **Main effort** | Concentrate best troops and support on attack axis |
| **Formation objectives** | Each formation gets specific objective (attack, pin, screen) |
| **Sequential objectives** | Cavalry: archers first, then rear attack, then pursue |
| **Cavalry tasking** | Specific high-value targets assigned |
| **Screening/refusing** | Weak flank gets minimal force, just delays |
| **Plan adaptation** | Detect failure, reinforce, shift axis, or fall back |
| **State machine execution** | Forming → Advancing → Engaging → Exploiting → Pursuing |
| **Plan commitment** | Stick with plan for 90+ seconds, no flip-flopping |
| **Enemy recognition** | Horse archers ≠ infantry horde — different problems |
| **Defensive formations** | Circle, square, terrain-anchored for specific threats |
| **Commitment timing** | Don't auto-engage — decide WHEN to commit |
| **Bleed rate monitoring** | Track casualties/minute, detect when defense is failing |
| **Max casualties mode** | If losing anyway, charge and take them with us |

| Plan Type | Main Effort | Support | Cavalry Role |
|-----------|-------------|---------|--------------|
| **Left Hook** | Left flank | Center pins, right screens | Archers → rear |
| **Right Hook** | Right flank | Center pins, left screens | Archers → rear |
| **Center Punch** | Center | Wings pin | Screen/exploit |
| **Double Envelopment** | Both flanks | Center pins | Wide flank both sides |
| **Hammer and Anvil** | Cavalry | Infantry holds | Rear attack |
| **Delay** | None (survival) | All screen | Harass/cover |
| **Anti-Horse Archer** | Defensive | Circle/terrain | Hunt if possible |

| Enemy Type | Counter-Strategy | Formation |
|------------|------------------|-----------|
| **Horse Archers** | Terrain anchor, circle, wait | Circle or terrain-anchored |
| **Cavalry Heavy** | Spears ready, refuse flanks | Square or shield wall |
| **Archer Heavy** | Close FAST | Shield wall advancing |
| **Pike Wall** | Flank or wait | Standard + flanking cavalry |
| **Infantry Horde** | Quality beats quantity | Concentrated, deep |

---

# Part 15: Unit Type Formations

Formations are how troops actually fight. This part covers what formation types exist in Bannerlord, when to use each for different unit types, and how the AI should manage formations dynamically.

## 15.1 Native Formation Types

Bannerlord has these built-in arrangement orders:

```csharp
public enum ArrangementOrder
{
    Line,           // Standard battle line
    Loose,          // Spread out (vs archers)
    Scatter,        // Very spread (skirmishers)
    Circle,         // All-around defense
    Square,         // Anti-cavalry box
    ShieldWall,     // Tight, shields front
    Skein,          // Wedge/flying V (cavalry)
    Column          // Marching column
}
```

### Formation Characteristics

| Formation | Spacing | Speed | Defense | Offense | Best For |
|-----------|---------|-------|---------|---------|----------|
| **Line** | 2m | 80% | Medium | Medium | Standard combat |
| **Loose** | 6m | 90% | Low | Medium | Avoiding arrows |
| **Scatter** | 10m+ | 100% | Very Low | Low | Skirmishing |
| **Circle** | Tight | 50% | High (all sides) | Low | Surrounded/horse archers |
| **Square** | Tight | 30% | High (corners) | Low | Anti-cavalry |
| **ShieldWall** | 0m | 30% | Very High (front) | Low | Advancing vs arrows |
| **Skein** | 2m | 100% | Low | High (charge) | Cavalry wedge charge |
| **Column** | 1m | 100% | Very Low | None | Movement only |

### Native API

```csharp
// Set formation arrangement
formation.SetArrangementOrder(ArrangementOrder.ShieldWall);

// Get current arrangement
ArrangementOrder current = formation.GetReadonlyArrangementOrder();

// Formation width (affects depth)
formation.FormOrder = FormOrder.FormOrderCustom(targetWidth);

// Facing direction
formation.SetFacingOrder(FacingOrder.LookAtEnemy);
formation.SetFacingOrder(FacingOrder.FaceDirection(direction));

// Movement
formation.SetMovementOrder(MovementOrder.Move(position));
formation.SetMovementOrder(MovementOrder.Charge);
formation.SetMovementOrder(MovementOrder.Stop);
```

---

## 15.2 Infantry Formations

Infantry is your main line. Formation choice depends on the threat:

### Line Formation (Default)

```
    ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
    ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
    ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
    
    USE: Default combat, balanced offense/defense
    SPACING: ~2m between soldiers
    DEPTH: 2-4 ranks depending on numbers
```

### Shield Wall

```
    ████████████████████████████████
    ████████████████████████████████
    ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
    
    █ = Shield front (tight, overlapping)
    ▪ = Rear ranks
    
    USE: 
    - Advancing into archer fire
    - Holding against frontal assault
    - Protecting archers behind
    
    REQUIRES: 50%+ troops have shields
    SPEED: Very slow (30%)
    WEAKNESS: Flanks exposed, slow to turn
```

### Loose Formation

```
    ▪   ▪   ▪   ▪   ▪   ▪   ▪   ▪   ▪
      ▪   ▪   ▪   ▪   ▪   ▪   ▪   ▪
    ▪   ▪   ▪   ▪   ▪   ▪   ▪   ▪   ▪
    
    USE:
    - Under heavy archer fire (less dense target)
    - Rough terrain
    - When shields not available
    
    SPACING: ~6m between soldiers
    WEAKNESS: Weak in melee (no mutual support)
```

### Square Formation

```
         ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
         ▪              ▪
         ▪              ▪
         ▪              ▪
         ▪              ▪
         ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
         
    CORNERS: Best anti-cavalry troops (spears)
    
    USE:
    - Cavalry charging you
    - Need to hold position
    - Protecting archers/commanders inside
    
    REQUIRES: ~80+ troops (thin square is weak)
    SPEED: Very slow (30%)
    STRENGTH: All-around defense, cavalry can't flank
    WEAKNESS: Can't advance, arrows still hurt
```

### Circle Formation

```
              ▪▪▪▪▪▪▪▪
           ▪▪          ▪▪
         ▪▪              ▪▪
        ▪▪                ▪▪
        ▪▪                ▪▪
         ▪▪              ▪▪
           ▪▪          ▪▪
              ▪▪▪▪▪▪▪▪
              
    CENTER: Archers, commanders, wounded
    
    USE:
    - Surrounded
    - Horse archers circling
    - Last stand
    
    REQUIRES: ~60+ troops
    SPEED: Cannot move
    STRENGTH: No flanks to exploit
    WEAKNESS: Thin everywhere, can't advance
```

### Infantry Formation Decision Logic

```csharp
class InfantryFormationSelector
{
    ArrangementOrder SelectFormation(Formation infantry, BattleState state)
    {
        EnemyThreatType threat = state.PrimaryThreat;
        float shieldRatio = infantry.ShieldRatio;
        bool underArcherFire = infantry.QuerySystem.UnderRangedAttackRatio > 0.3f;
        bool cavalryNearby = state.EnemyCavalryWithinRange(80f);
        bool surrounded = infantry.QuerySystem.LocalEnemyRatio > 2.5f;
        
        // Priority 1: Surrounded → Circle
        if (surrounded && infantry.CountOfUnits >= 60)
            return ArrangementOrder.Circle;
        
        // Priority 2: Cavalry charging → Square
        if (cavalryNearby && threat == EnemyThreatType.CavalryHeavy && infantry.CountOfUnits >= 80)
            return ArrangementOrder.Square;
        
        // Priority 3: Under archer fire → Shield Wall or Loose
        if (underArcherFire)
        {
            if (shieldRatio >= 0.5f)
                return ArrangementOrder.ShieldWall;
            else
                return ArrangementOrder.Loose;  // No shields, spread out
        }
        
        // Priority 4: Advancing into archer fire → Shield Wall
        if (state.AdvancingIntoArcherFire && shieldRatio >= 0.5f)
            return ArrangementOrder.ShieldWall;
        
        // Default: Line
        return ArrangementOrder.Line;
    }
}
```

---

## 15.3 Archer Formations

Archers need to shoot effectively while staying protected:

### Line Formation (Default for Archers)

```
    ○○○○○○○○○○○○○○○○○○○○○○○○○○○○○○
    ○○○○○○○○○○○○○○○○○○○○○○○○○○○○○○
    
    USE: Maximum volleys, all archers can fire
    POSITION: Behind infantry
    DEPTH: 2-3 ranks (more = rear can't shoot)
```

### Loose Formation

```
    ○   ○   ○   ○   ○   ○   ○   ○   ○
      ○   ○   ○   ○   ○   ○   ○   ○
    ○   ○   ○   ○   ○   ○   ○   ○   ○
    
    USE: 
    - Enemy has archers (less dense target)
    - Skirmishing
    
    BENEFIT: Harder to hit, all can still fire
```

### Scatter Formation

```
    ○       ○       ○       ○       ○
        ○       ○       ○       ○
    ○       ○       ○       ○       ○
    
    USE:
    - Skirmishers (javelin throwers)
    - Avoiding cavalry
    - Hit and run
    
    WARNING: Very spread out, hard to control
```

### Staggered/Checkerboard (Behind Infantry)

```
    ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪  ← Infantry front
    ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪
    
      ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○   ← Archers behind
        ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○ ○
        
    POSITION: 15-30m behind infantry
    ANGLE: Slightly elevated if possible (can shoot over)
    FIRE ARC: Clear line of sight to enemy
```

### Archer Positioning Logic

```csharp
class ArcherPositioner
{
    void PositionArchers(Formation archers, Formation infantry, BattleState state)
    {
        // Calculate position behind infantry
        Vec2 infantryFacing = infantry.Direction;
        Vec2 archerPosition = infantry.CachedMedianPosition.AsVec2 - (infantryFacing * 25f);
        
        archers.SetMovementOrder(MovementOrder.Move(archerPosition));
        archers.SetFacingOrder(FacingOrder.LookAtEnemy);
        
        // Formation based on situation
        if (state.EnemyHasArchers && state.EnemyArcherCount > archers.CountOfUnits * 0.5f)
        {
            // Enemy archers are a threat — spread out
            archers.SetArrangementOrder(ArrangementOrder.Loose);
        }
        else
        {
            // Standard line — maximum fire
            archers.SetArrangementOrder(ArrangementOrder.Line);
        }
        
        // Width: Match infantry width (covers the line)
        float infantryWidth = infantry.Width;
        archers.FormOrder = FormOrder.FormOrderCustom(infantryWidth);
    }
    
    void HandleArchersInCircle(Formation archers, Formation infantry)
    {
        // Special case: Circle formation, archers go in center
        if (infantry.GetReadonlyArrangementOrder() == ArrangementOrder.Circle)
        {
            Vec2 center = infantry.CachedMedianPosition.AsVec2;
            archers.SetMovementOrder(MovementOrder.Move(center));
            archers.SetArrangementOrder(ArrangementOrder.Loose);
            archers.SetFacingOrder(FacingOrder.LookAtEnemy);
        }
    }
}
```

### Archer Targeting

```csharp
class ArcherTargeting
{
    Formation SelectArcherTarget(Formation archers, BattleState state)
    {
        var enemyFormations = state.EnemyFormations
            .Where(f => f.CountOfUnits > 5)
            .ToList();
        
        // Priority 1: Enemy archers (if we can win the duel)
        var enemyArchers = enemyFormations.FirstOrDefault(f => f.QuerySystem.IsRangedFormation);
        if (enemyArchers != null && CanWinArcherDuel(archers, enemyArchers))
            return enemyArchers;
        
        // Priority 2: Enemy cavalry (dangerous, soft target)
        var enemyCavalry = enemyFormations.FirstOrDefault(f => f.QuerySystem.IsCavalryFormation);
        if (enemyCavalry != null && !enemyCavalry.IsEngaged)
            return enemyCavalry;
        
        // Priority 3: Engaged enemy infantry (distracted, easy target)
        var engagedInfantry = enemyFormations
            .Where(f => f.QuerySystem.IsInfantryFormation && f.IsEngaged)
            .OrderByDescending(f => f.CountOfUnits)
            .FirstOrDefault();
        if (engagedInfantry != null)
            return engagedInfantry;
        
        // Priority 4: Closest threat
        return enemyFormations
            .OrderBy(f => f.CachedMedianPosition.Distance(archers.CachedMedianPosition))
            .FirstOrDefault();
    }
    
    bool CanWinArcherDuel(Formation ours, Formation theirs)
    {
        float ourPower = ours.CountOfUnits * GetAverageArcherSkill(ours);
        float theirPower = theirs.CountOfUnits * GetAverageArcherSkill(theirs);
        return ourPower > theirPower * 1.2f;  // Need 20% advantage
    }
}
```

---

## 15.4 Cavalry Formations

Cavalry is mobile, hits hard, but vulnerable when stationary:

### Line Formation

```
    ◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆
    ◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆◆
    
    USE: Waiting, screening, broad charge
    DEPTH: 2 ranks
```

### Skein (Wedge) Formation

```
              ◆
            ◆ ◆ ◆
          ◆ ◆ ◆ ◆ ◆
        ◆ ◆ ◆ ◆ ◆ ◆ ◆
      ◆ ◆ ◆ ◆ ◆ ◆ ◆ ◆ ◆
      
    TIP: Best armored rider (breakthrough point)
    
    USE:
    - Charging infantry (wedge punches through)
    - Breaking formations
    
    BENEFIT: Concentrated impact, breaks lines
    WEAKNESS: Narrow front, flanks exposed
```

### Loose Formation

```
    ◆   ◆   ◆   ◆   ◆   ◆   ◆
      ◆   ◆   ◆   ◆   ◆   ◆
    ◆   ◆   ◆   ◆   ◆   ◆   ◆
    
    USE:
    - Avoiding archers
    - Pursuit (spreading to catch fleers)
    - Screening flanks
```

### Cavalry Formation and Behavior Logic

```csharp
class CavalryFormationManager
{
    void ManageCavalryFormation(Formation cavalry, BattleState state, CavalryTask task)
    {
        switch (task)
        {
            case CavalryTask.Reserve:
                // Waiting in reserve — line, facing enemy
                cavalry.SetArrangementOrder(ArrangementOrder.Line);
                cavalry.SetMovementOrder(MovementOrder.Stop);
                cavalry.SetFacingOrder(FacingOrder.LookAtEnemy);
                break;
                
            case CavalryTask.ScreenFlanks:
                // Screening — loose, mobile
                cavalry.SetArrangementOrder(ArrangementOrder.Loose);
                // Position on designated flank
                break;
                
            case CavalryTask.ChargeInfantry:
                // Charging infantry — wedge for maximum impact
                cavalry.SetArrangementOrder(ArrangementOrder.Skein);
                cavalry.SetMovementOrder(MovementOrder.Charge);
                break;
                
            case CavalryTask.DestroyArchers:
                // Charging archers — wedge or line (archers are soft)
                cavalry.SetArrangementOrder(ArrangementOrder.Skein);
                Formation enemyArchers = state.GetEnemyArchers();
                cavalry.SetMovementOrder(MovementOrder.ChargeToTarget(enemyArchers));
                break;
                
            case CavalryTask.PursueRouters:
                // Pursuit — loose to cover more ground
                cavalry.SetArrangementOrder(ArrangementOrder.Loose);
                cavalry.SetMovementOrder(MovementOrder.Charge);
                break;
                
            case CavalryTask.CounterCavalry:
                // Cavalry vs cavalry — line for width
                cavalry.SetArrangementOrder(ArrangementOrder.Line);
                Formation enemyCavalry = state.GetEnemyCavalry();
                cavalry.SetMovementOrder(MovementOrder.ChargeToTarget(enemyCavalry));
                break;
        }
    }
    
    void HandleCavalryAfterCharge(Formation cavalry, BattleState state)
    {
        // After a charge, cavalry needs to reform
        // Don't get bogged down in melee
        
        float timeInMelee = cavalry.TimeInMelee;
        
        if (timeInMelee > 15f && !IsEnemyRouting(state))
        {
            // In melee too long — disengage and reform
            Vec2 rallyPoint = CalculateRallyPoint(cavalry, state);
            cavalry.SetMovementOrder(MovementOrder.Move(rallyPoint));
            cavalry.SetArrangementOrder(ArrangementOrder.Line);
            
            // Re-evaluate for another charge
        }
    }
    
    Vec2 CalculateRallyPoint(Formation cavalry, BattleState state)
    {
        // Rally point behind friendly lines or on a flank
        Vec2 center = state.OurCenter;
        Vec2 toEnemy = (state.EnemyCenter - center).Normalized();
        
        // Rally behind our line, offset to flank
        Vec2 behind = center - toEnemy * 50f;
        Vec2 flankOffset = new Vec2(-toEnemy.Y, toEnemy.X) * 40f;
        
        return behind + flankOffset;
    }
}
```

### Cavalry Charge Timing

```csharp
class CavalryChargeTimer
{
    bool ShouldCharge(Formation cavalry, Formation target, BattleState state)
    {
        // Don't charge:
        // - Braced spears/pikes facing you
        // - Shield wall that sees you coming
        // - Into prepared enemy
        
        if (target.QuerySystem.IsInfantryFormation)
        {
            // Check if they have spears
            if (target.SpearRatio > 0.3f && !target.IsEngaged)
                return false;  // They're ready for us
            
            // Check if they're facing us
            float facingAngle = CalculateFacingAngle(target, cavalry);
            if (facingAngle < 45f && target.GetReadonlyArrangementOrder() == ArrangementOrder.ShieldWall)
                return false;  // Shield wall facing us
        }
        
        // DO charge:
        // - Engaged enemy (distracted)
        // - Archers (soft target)
        // - Enemy flank or rear
        // - Routing enemies
        
        if (target.IsEngaged)
            return true;  // They're busy
        
        if (target.QuerySystem.IsRangedFormation)
            return true;  // Archers are soft
        
        if (IsFlankOrRear(cavalry, target))
            return true;  // Hit their weak side
        
        if (target.IsRouting)
            return true;  // Chase them down
        
        return false;  // Wait for better opportunity
    }
    
    bool IsFlankOrRear(Formation cavalry, Formation target)
    {
        Vec2 targetFacing = target.Direction;
        Vec2 toUs = (cavalry.CachedMedianPosition.AsVec2 - target.CachedMedianPosition.AsVec2).Normalized();
        
        float dotProduct = Vec2.Dot(targetFacing, toUs);
        
        // dot < -0.5 means we're behind them
        // |dot| < 0.5 means we're on their flank
        return dotProduct < 0.5f;
    }
}
```

---

## 15.5 Formation Positioning (Combined Arms)

How formations should be positioned relative to each other:

### Standard Battle Line

```
                          ENEMY
                            ↓
                            
    ┌─────────────────────────────────────────────────────────┐
    │                                                         │
    │                   FRONT LINE (25-50m)                   │
    │                                                         │
    │  ◆◆◆◆◆         ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪         ◆◆◆◆◆  │
    │  L.CAV         ▪▪▪▪▪ INFANTRY ▪▪▪▪▪▪         R.CAV  │
    │                ▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪▪                │
    │                                                         │
    │                                                         │
    │                   SECOND LINE (50-80m)                  │
    │                                                         │
    │                ○○○○○○○○○○○○○○○○○○○○○○○○                │
    │                     ARCHERS                              │
    │                                                         │
    │                                                         │
    │                   RESERVE (100m+)                       │
    │                                                         │
    │                     ●●●●●●●●●●                          │
    │                     RESERVE                              │
    │                                                         │
    └─────────────────────────────────────────────────────────┘
```

### Positioning Logic

```csharp
class FormationPositioner
{
    void PositionAllFormations(Army army, BattleState state)
    {
        Vec2 battleCenter = state.BattleCenter;
        Vec2 enemyDirection = (state.EnemyCenter - battleCenter).Normalized();
        Vec2 lineDirection = new Vec2(-enemyDirection.Y, enemyDirection.X);  // Perpendicular
        
        // INFANTRY: Front and center
        Formation infantry = army.GetPrimaryInfantry();
        Vec2 infantryPos = battleCenter;
        infantry.SetMovementOrder(MovementOrder.Move(infantryPos));
        infantry.SetFacingOrder(FacingOrder.FaceDirection(enemyDirection));
        
        // ARCHERS: Behind infantry
        Formation archers = army.GetArchers();
        if (archers != null)
        {
            Vec2 archerPos = infantryPos - enemyDirection * 30f;
            archers.SetMovementOrder(MovementOrder.Move(archerPos));
            archers.SetFacingOrder(FacingOrder.FaceDirection(enemyDirection));
        }
        
        // CAVALRY LEFT: Left flank
        Formation leftCav = army.GetCavalry(Side.Left);
        if (leftCav != null)
        {
            float flankDistance = infantry.Width * 0.6f;
            Vec2 leftFlankPos = infantryPos + lineDirection * flankDistance;
            leftCav.SetMovementOrder(MovementOrder.Move(leftFlankPos));
            leftCav.SetFacingOrder(FacingOrder.LookAtEnemy);
        }
        
        // CAVALRY RIGHT: Right flank
        Formation rightCav = army.GetCavalry(Side.Right);
        if (rightCav != null)
        {
            float flankDistance = infantry.Width * 0.6f;
            Vec2 rightFlankPos = infantryPos - lineDirection * flankDistance;
            rightCav.SetMovementOrder(MovementOrder.Move(rightFlankPos));
            rightCav.SetFacingOrder(FacingOrder.LookAtEnemy);
        }
        
        // RESERVE: Behind center
        Formation reserve = army.GetReserve();
        if (reserve != null)
        {
            Vec2 reservePos = infantryPos - enemyDirection * 80f;
            reserve.SetMovementOrder(MovementOrder.Move(reservePos));
            reserve.SetFacingOrder(FacingOrder.FaceDirection(enemyDirection));
        }
    }
}
```

### Distance Guidelines

| Formation | Distance from Front | Notes |
|-----------|---------------------|-------|
| **Infantry (Main)** | 0m (IS the front) | Facing enemy |
| **Infantry (Flank)** | Same line, offset | Cover flanks of main |
| **Archers** | 20-40m behind infantry | Far enough to shoot over, close enough to be protected |
| **Cavalry (Waiting)** | On flanks, 30-50m back | Ready to charge |
| **Cavalry (Screening)** | Forward, 50-100m | Early warning, skirmish |
| **Reserve** | 80-120m behind | Out of immediate combat |

---

## 15.6 Dynamic Formation Switching

Formations should change based on the situation:

### Switch Triggers

```csharp
class FormationSwitcher
{
    ArrangementOrder currentFormation;
    float lastSwitchTime;
    const float SwitchCooldown = 10f;  // Don't switch too often
    
    void EvaluateFormationSwitch(Formation formation, BattleState state)
    {
        if (state.CurrentTime - lastSwitchTime < SwitchCooldown)
            return;  // Cooldown
        
        ArrangementOrder desired = DetermineOptimalFormation(formation, state);
        
        if (desired != currentFormation)
        {
            // Only switch if situation warrants it
            bool shouldSwitch = EvaluateSwitchPriority(currentFormation, desired, state);
            
            if (shouldSwitch)
            {
                formation.SetArrangementOrder(desired);
                currentFormation = desired;
                lastSwitchTime = state.CurrentTime;
                Log($"Formation switch: {currentFormation} → {desired}");
            }
        }
    }
    
    bool EvaluateSwitchPriority(ArrangementOrder current, ArrangementOrder desired, BattleState state)
    {
        // Always switch FOR:
        // - Cavalry incoming (any → Square)
        // - Surrounded (any → Circle)
        // - Entering melee (ShieldWall → Line)
        
        // Don't switch:
        // - Mid-charge
        // - While actively engaged in melee (disruptive)
        
        if (state.CavalryChargingUs && desired == ArrangementOrder.Square)
            return true;
        
        if (state.Surrounded && desired == ArrangementOrder.Circle)
            return true;
        
        if (state.InMelee && current == ArrangementOrder.ShieldWall && desired == ArrangementOrder.Line)
            return true;  // Shield wall is bad in melee (need to attack)
        
        // Mid-melee switches are disruptive
        if (state.InMelee && desired != ArrangementOrder.Line)
            return false;
        
        return true;  // Default: allow switch
    }
}
```

### Formation Switch Matrix

| Current | Trigger | Switch To |
|---------|---------|-----------|
| **Any** | Cavalry charging | Square |
| **Any** | Surrounded | Circle |
| **Line** | Heavy archer fire + shields | Shield Wall |
| **Line** | Heavy archer fire, no shields | Loose |
| **Shield Wall** | Entering melee | Line |
| **Shield Wall** | Enemy on flank | Line + face threat |
| **Square** | Cavalry defeated | Line |
| **Circle** | No longer surrounded | Line |
| **Loose** | Entering melee | Line |

---

## 15.7 Formation Width and Depth Control

Width affects how many can fight at once; depth affects staying power:

### Width Calculation

```csharp
class FormationWidthManager
{
    void SetFormationWidth(Formation formation, int desiredRanks)
    {
        int troopCount = formation.CountOfUnits;
        int filesPerRank = troopCount / desiredRanks;
        float targetWidth = filesPerRank * 2f;  // ~2m per soldier
        
        formation.FormOrder = FormOrder.FormOrderCustom(targetWidth);
    }
    
    int DetermineDesiredRanks(Formation formation, BattleState state)
    {
        int count = formation.CountOfUnits;
        float qualityRatio = formation.AverageTier / 6f;  // 1.0 = max tier
        
        // Base depth based on count
        int baseRanks = count switch
        {
            < 30 => 2,
            < 60 => 3,
            < 100 => 3,
            < 200 => 4,
            _ => 4
        };
        
        // High quality troops: thinner line (quality over quantity at front)
        if (qualityRatio > 0.7f)
            baseRanks = Math.Max(2, baseRanks - 1);
        
        // Main effort: deeper (need staying power)
        if (formation.Role == FormationRole.MainEffort)
            baseRanks += 1;
        
        // Screening: thinner (just delaying)
        if (formation.Role == FormationRole.Screen)
            baseRanks = 2;
        
        return Math.Clamp(baseRanks, 2, 6);
    }
}
```

### Width Guidelines

| Situation | Ranks | Effect |
|-----------|-------|--------|
| **Outnumbered, need width** | 2 | Wide but fragile |
| **Standard** | 3 | Balanced |
| **Main effort** | 4-5 | Deep, staying power |
| **Quality troops** | 2-3 | Let quality shine |
| **Low quality troops** | 4-5 | Depth compensates |
| **Screening force** | 2 | Just slowing enemy |

---

## 15.8 Formation Facing and Movement

### Facing Orders

```csharp
class FormationFacing
{
    void UpdateFacing(Formation formation, BattleState state)
    {
        FormationObjective objective = formation.CurrentObjective;
        
        switch (objective)
        {
            case FormationObjective.Attack:
            case FormationObjective.Pin:
                // Face the enemy
                formation.SetFacingOrder(FacingOrder.LookAtEnemy);
                break;
                
            case FormationObjective.Screen:
                // Face the threat direction
                Formation nearestEnemy = state.GetNearestEnemyFormation(formation);
                if (nearestEnemy != null)
                {
                    Vec2 toEnemy = nearestEnemy.CachedMedianPosition.AsVec2 - formation.CachedMedianPosition.AsVec2;
                    formation.SetFacingOrder(FacingOrder.FaceDirection(toEnemy.Normalized()));
                }
                break;
                
            case FormationObjective.Hold:
                // Face specified direction (defending a position)
                formation.SetFacingOrder(FacingOrder.FaceDirection(state.PrimaryThreatDirection));
                break;
        }
    }
}
```

### Movement Orders

```csharp
class FormationMovement
{
    void SetMovement(Formation formation, FormationObjective objective, BattleState state)
    {
        switch (objective)
        {
            case FormationObjective.Attack:
                // Move toward enemy
                formation.SetMovementOrder(MovementOrder.Advance);
                break;
                
            case FormationObjective.Pin:
                // Move to contact but don't push hard
                if (!formation.IsEngaged)
                    formation.SetMovementOrder(MovementOrder.Advance);
                else
                    formation.SetMovementOrder(MovementOrder.Stop);
                break;
                
            case FormationObjective.Screen:
                // Hold position, fall back if pressed
                if (formation.QuerySystem.LocalPowerRatio < 0.5f)
                    FallBack(formation, 20f);
                else
                    formation.SetMovementOrder(MovementOrder.Stop);
                break;
                
            case FormationObjective.Hold:
                formation.SetMovementOrder(MovementOrder.Stop);
                break;
                
            case FormationObjective.Breakthrough:
                formation.SetMovementOrder(MovementOrder.Charge);
                break;
                
            case FormationObjective.FightingRetreat:
                // Fall back slowly while facing enemy
                Vec2 fallbackPos = CalculateFallbackPosition(formation, state, 15f);
                formation.SetMovementOrder(MovementOrder.Move(fallbackPos));
                formation.SetFacingOrder(FacingOrder.LookAtEnemy);
                break;
        }
    }
    
    void FallBack(Formation formation, float distance)
    {
        Vec2 current = formation.CachedMedianPosition.AsVec2;
        Vec2 awayFromEnemy = -formation.Direction;  // Opposite of facing
        Vec2 fallbackPos = current + awayFromEnemy * distance;
        
        formation.SetMovementOrder(MovementOrder.Move(fallbackPos));
    }
}
```

---

## 15.9 Multi-Formation Coordination

When you have multiple infantry formations, they need to coordinate:

### Echelon Attack

```
                ECHELON LEFT
                
                         ENEMY
                         ═════════════════════════
                              ↑
                         [FIRST CONTACT]
                              │
                    ┌─────────┘
                    │
           INF I ───┘  (attacks first)
           ▪▪▪▪▪▪▪
           
              INF II ─── (follows, covers flank)
              ▪▪▪▪▪▪▪
              
                 INF III ─── (reserve or refused)
                 ▪▪▪▪▪▪▪
                 
    BENEFIT: Each formation hits in sequence, can support the one ahead
    USE: When attacking with multiple formations
```

### Refused Flank with Multiple Formations

```
           REFUSED RIGHT FLANK
           
                    ENEMY
                    ═════════════════════════════════
                         ↑
                    [MAIN ATTACK]
                         │
           ┌─────────────┘
           │
           │         ┌────────────────────┐
           │         │   INF I (MAIN)     │
           │         │   Attack left      │
           │         └────────────────────┘
           │
           │              ┌──────────────────┐
           │              │   INF II (PIN)   │
           │              │   Hold center    │
           │              └──────────────────┘
           │
           │                      ┌──────────┐  ← Angled back
           │                      │ INF III  │
           └──────────────────────│ (REFUSE) │
                                  └──────────┘
           
    INF III: Doesn't advance, angled back, just protects right flank
```

### Coordination Logic

```csharp
class MultiFormationCoordinator
{
    void CoordinateInfantryFormations(List<Formation> infantryFormations, BattlePlan plan)
    {
        if (infantryFormations.Count == 1)
            return;  // Nothing to coordinate
        
        switch (plan.AttackStyle)
        {
            case AttackStyle.EchelonLeft:
                SetupEchelon(infantryFormations, Side.Left);
                break;
                
            case AttackStyle.EchelonRight:
                SetupEchelon(infantryFormations, Side.Right);
                break;
                
            case AttackStyle.RefuseLeft:
                SetupRefusedFlank(infantryFormations, Side.Left);
                break;
                
            case AttackStyle.RefuseRight:
                SetupRefusedFlank(infantryFormations, Side.Right);
                break;
                
            case AttackStyle.CenterPunch:
                SetupCenterPunch(infantryFormations);
                break;
        }
    }
    
    void SetupEchelon(List<Formation> formations, Side leadingSide)
    {
        // Leading formation attacks first
        Formation lead = leadingSide == Side.Left ? formations[0] : formations.Last();
        lead.SetObjective(FormationObjective.Attack);
        
        // Following formations staggered
        float stagger = 30f;
        for (int i = 1; i < formations.Count; i++)
        {
            formations[i].SetObjective(FormationObjective.Attack);
            // Position offset and delayed
            float delay = i * stagger;
            formations[i].SetAdvanceDelay(delay);
        }
    }
    
    void SetupRefusedFlank(List<Formation> formations, Side refusedSide)
    {
        // Main attack formations
        foreach (var f in formations.Where(f => f.FlankPosition != refusedSide))
        {
            f.SetObjective(FormationObjective.Attack);
        }
        
        // Refused flank formation
        foreach (var f in formations.Where(f => f.FlankPosition == refusedSide))
        {
            f.SetObjective(FormationObjective.Screen);
            f.SetArrangementOrder(ArrangementOrder.ShieldWall);
            
            // Angle back from main line
            RotateFormationBack(f, 30f);
        }
    }
    
    void SetupCenterPunch(List<Formation> formations)
    {
        // Center formation is main effort
        Formation center = formations.FirstOrDefault(f => f.FlankPosition == Side.Center);
        if (center != null)
        {
            center.SetObjective(FormationObjective.Breakthrough);
            center.Role = FormationRole.MainEffort;
        }
        
        // Flanks pin
        foreach (var f in formations.Where(f => f.FlankPosition != Side.Center))
        {
            f.SetObjective(FormationObjective.Pin);
        }
    }
}
```

---

## 15.10 Formation API Reference

Quick reference for native Bannerlord formation APIs:

### Arrangement Orders

```csharp
// Set arrangement
formation.SetArrangementOrder(ArrangementOrder.Line);
formation.SetArrangementOrder(ArrangementOrder.ShieldWall);
formation.SetArrangementOrder(ArrangementOrder.Loose);
formation.SetArrangementOrder(ArrangementOrder.Scatter);
formation.SetArrangementOrder(ArrangementOrder.Circle);
formation.SetArrangementOrder(ArrangementOrder.Square);
formation.SetArrangementOrder(ArrangementOrder.Skein);
formation.SetArrangementOrder(ArrangementOrder.Column);

// Get current
ArrangementOrder current = formation.GetReadonlyArrangementOrder();
```

### Form Orders (Width)

```csharp
// Custom width
formation.FormOrder = FormOrder.FormOrderCustom(widthInMeters);

// Preset widths
formation.FormOrder = FormOrder.FormOrderWide;      // Widest
formation.FormOrder = FormOrder.FormOrderWider;     // Wider
formation.FormOrder = FormOrder.FormOrderDeep;      // Deepest

// Get current width
float width = formation.Width;
float depth = formation.Depth;
```

### Facing Orders

```csharp
// Look at enemy
formation.SetFacingOrder(FacingOrder.LookAtEnemy);

// Face specific direction
formation.SetFacingOrder(FacingOrder.FaceDirection(directionVec2));

// Get facing direction
Vec2 facing = formation.Direction;
```

### Movement Orders

```csharp
// Stop
formation.SetMovementOrder(MovementOrder.Stop);

// Advance toward enemy
formation.SetMovementOrder(MovementOrder.Advance);

// Charge (full speed attack)
formation.SetMovementOrder(MovementOrder.Charge);

// Charge specific target
formation.SetMovementOrder(MovementOrder.ChargeToTarget(targetFormation));

// Move to position
formation.SetMovementOrder(MovementOrder.Move(targetPosition));

// Fall back
formation.SetMovementOrder(MovementOrder.FallBack);

// Retreat
formation.SetMovementOrder(MovementOrder.Retreat);

// Follow another formation
formation.SetMovementOrder(MovementOrder.Follow(targetFormation));
```

### Firing Orders (Ranged)

```csharp
// Fire at will
formation.SetFiringOrder(FiringOrder.FireAtWill);

// Hold fire
formation.SetFiringOrder(FiringOrder.HoldFire);
```

### Useful Properties

```csharp
// Unit counts
int count = formation.CountOfUnits;
int countWithShield = formation.CountOfUnitsWithCondition(a => a.HasShieldCached);

// Position
Vec2 center = formation.CachedMedianPosition.AsVec2;
float distanceToEnemy = formation.QuerySystem.ClosestEnemyFormation.Distance;

// State
bool isEngaged = formation.IsEngaged;
float casualtyRatio = formation.QuerySystem.CasualtyRatio;
bool underRangedAttack = formation.QuerySystem.UnderRangedAttackRatio > 0.3f;

// Type detection
bool isInfantry = formation.QuerySystem.IsInfantryFormation;
bool isRanged = formation.QuerySystem.IsRangedFormation;
bool isCavalry = formation.QuerySystem.IsCavalryFormation;
```

---

## Part 15 Summary

| Unit Type | Default Formation | When to Change |
|-----------|-------------------|----------------|
| **Infantry** | Line | Shield Wall (arrows), Square (cavalry), Circle (surrounded) |
| **Archers** | Line (behind infantry) | Loose (enemy archers), Center (if circle) |
| **Cavalry** | Line (waiting) | Skein (charging), Loose (pursuit) |

| Formation | Best For | Weakness |
|-----------|----------|----------|
| **Line** | Standard combat | Average at everything |
| **Shield Wall** | Advancing vs archers, holding | Slow, weak flanks |
| **Loose** | Avoiding arrows | Weak in melee |
| **Square** | Anti-cavalry | Can't move, thin lines |
| **Circle** | Surrounded, horse archers | Can't advance |
| **Skein** | Cavalry charge | Narrow, flanks exposed |

| Position | Distance from Front | Role |
|----------|---------------------|------|
| **Infantry Main** | 0m | THE front line |
| **Archers** | 20-40m behind | Fire support |
| **Cavalry Flanks** | 30-50m back, sides | Ready to charge |
| **Reserve** | 80-120m behind | Exploit or reinforce |

---

# Part 16: Terrain Exploitation

Native Bannerlord detects and marks terrain features but uses them reactively rather than strategically. The Orchestrator should proactively seek and exploit terrain advantages.

## 16.1 Native Terrain Data

The game provides two terrain classification systems:

### TacticalPosition Types
```csharp
public enum TacticalPositionTypeEnum
{
    Regional,           // General area, no special properties
    HighGround,         // Elevated position with combat advantage
    ChokePoint,         // Narrow passage limiting frontage
    Cliff,              // Impassable or dangerous terrain
    SpecialMissionPosition  // Siege-specific positions
}
```

### TacticalRegion Types
```csharp
public enum TacticalRegionTypeEnum
{
    Forest,             // Dense vegetation, limits cavalry and visibility
    DifficultTerrain,   // Rough ground, slows movement
    Opening             // Clear ground, favors cavalry
}
```

### Accessing Terrain Data
```csharp
// Get tactical positions for team
List<TacticalPosition> positions = team.TeamAI.TacticalPositions;

// Get tactical regions
List<TacticalRegion> regions = team.TeamAI.TacticalRegions;

// Get high ground near expected battle location
Vec2 highGround = formation.QuerySystem.HighGroundCloseToForeseenBattleGround;

// Check position properties
foreach (TacticalPosition pos in positions)
{
    if (pos.TacticalPositionType == TacticalPosition.TacticalPositionTypeEnum.HighGround)
    {
        WorldPosition position = pos.Position;
        float width = pos.Width;
        float slope = pos.Slope;
        Vec2 direction = pos.Direction;
        bool insurmountable = pos.IsInsurmountable;
    }
}
```

## 16.2 High Ground Strategy

High ground provides significant advantages:
- Archers have better range and line of sight
- Infantry charging downhill has momentum advantage
- Defenders can see approaching threats earlier
- Missile weapons have gravity assist

### When to Use High Ground

| Situation | High Ground Value |
|-----------|------------------|
| **Defending** | Very high — make them come to you |
| **Outnumbered** | Very high — force frontal attacks |
| **Archer-heavy** | Very high — better lines of fire |
| **Cavalry-heavy** | Medium — limits your own mobility |
| **Attacking** | Low — don't waste time, close and fight |
| **Ambush** | High — strike downhill |

### Behavior Weight Adjustment
```csharp
// Native behavior exists but isn't weighted highly enough
formation.AI.SetBehaviorWeight<BehaviorHoldHighGround>(weight);

// Orchestrator adjusts weight based on situation
float highGroundWeight = CalculateHighGroundWeight(context);
// Base weight: 1.0
// Defending: +0.5
// Outnumbered: +0.3
// Archer-heavy: +0.5
// Already on high ground: +1.0 (hold it!)
// Attacking: -0.3
```

## 16.3 Choke Point Exploitation

Choke points limit the number of enemies that can engage simultaneously, negating numerical advantage.

### Choke Point Value
- Narrower is better (limits enemy frontage more)
- Only valuable if enemy must come through it
- More valuable when outnumbered
- Less useful if we have more troops (choke limits US too)

### Choke Point Tactics
- Position infantry to block the choke (match formation width to choke width)
- Archers behind, elevated if possible
- Cavalry hidden on flanks for when enemy commits
- When enemy commits to the choke, cavalry hits their rear

## 16.4 Forest and Difficult Terrain

### Forest Properties
- **Cavalry disadvantage**: Horses can't maneuver, charges ineffective
- **Archer disadvantage**: Blocked lines of sight
- **Infantry advantage**: Close-quarters fighting suits infantry
- **Ambush potential**: Can hide units until enemy is close

### Tactical Uses
- Infantry vs cavalry: Pull infantry INTO the forest
- Enemy has archers: Advance THROUGH forest to avoid arrows
- We have archers: Stay in open ground, make THEM come to us
- Difficult terrain: Anchor a flank on it (enemy cavalry can't flank through rough ground)

## 16.5 Cliff and Impassable Terrain

### Defensive Use
- Back against cliff means no rear attack possible
- Only works if not badly outnumbered
- Warning: No retreat possible - commit to this position

### Offensive Use
- Push enemy toward cliff, cut off retreat
- Calculate attack axis that forces enemy toward the cliff

### Danger
- Cliff behind us: If we rout, troops will fall
- Recommendation: Reposition away from cliff edge

## 16.6 Terrain-Aware Battle Plans

Integrate terrain into battle plan selection (extends Part 14):
- High ground available + defending/outnumbered: Seek it pre-battle
- Choke point available + outnumbered: Choke point defense with cavalry flank
- Forest available + enemy cavalry: Forest defense
- Flank anchor available: Anchor formation flank against terrain

## 16.7 Proactive Terrain Seeking

Key improvement over native AI: don't just use terrain if you're already on it — actively seek it.

### When to Seek Terrain
- YES: Forming phase, not yet engaged
- YES: Have time before enemy reaches us (distance > 200m)
- NO: Already fighting
- NO: Attacking and stronger (just close and fight)
- MAYBE: Disengaging to better position if worth it

### Pre-Battle Terrain Rush Priority
1. High ground on our side
2. Choke point if outnumbered
3. Flank anchor
4. Open ground if we have archers

---

## Part 16 Summary

| Terrain Type | Best For | Worst For | Orchestrator Action |
|-------------|----------|-----------|---------------------|
| **High Ground** | Defending, archers, outnumbered | Attacking when stronger | Seek pre-battle, hold during battle |
| **Choke Point** | Outnumbered, infantry-heavy | Outnumbering enemy | Block with infantry, cavalry flank |
| **Forest** | Infantry vs cavalry, avoiding arrows | Cavalry, archers | Use as approach route or anchor |
| **Difficult Terrain** | Flank protection | Mobility | Anchor flank against it |
| **Cliff** | Preventing rear attacks | If behind you | Back against OR push enemy toward |

---

# Part 17: Morale Exploitation

Native Bannerlord has a full morale system that tracks per-agent morale, triggers panic, and spreads morale effects. The AI doesn't exploit this tactically. We can.

## 17.1 Native Morale System

### How Morale Works
```csharp
// Each agent has morale (0-100)
float morale = agent.GetMorale();
agent.ChangeMorale(delta);

// Morale changes on kills
const float BaseMoraleGainOnKill = 3f;    // For killer's side
const float BaseMoraleLossOnKill = 4f;    // For victim's side
const float BaseMoraleGainOnPanic = 2f;   // When enemy panics
const float BaseMoraleLossOnPanic = 1.1f; // When ally panics

// Weapon type affects morale impact
const float MeleeWeaponMoraleMultiplier = 0.75f;
const float RangedWeaponMoraleMultiplier = 0.5f;
const float SiegeWeaponMoraleMultiplier = 0.25f;
```

### Morale Spread
- When an agent dies or panics, nearby allies lose morale
- Nearby enemies gain morale
- Casualty factor increases morale swings as battle goes on
- Early battle: small morale changes; Late battle: big morale swings

### Formation Morale
```csharp
float avgMorale = MissionGameModels.Current.BattleMoraleModel.GetAverageMorale(formation);
```

## 17.2 Reading Enemy Morale

We can't directly read enemy agent morale, but we can infer it:

### Observable Indicators
- Router count (panicked agents from formation)
- Recent casualties (morale shock)
- Formation cohesion (wavering formations spread out)
- Movement patterns (retreating or advancing?)

### Morale States
- **Strong** (>80): Confident, will fight hard
- **Steady** (60-80): Normal combat morale
- **Wavering** (40-60): At risk, close to breaking
- **Breaking** (20-40): About to rout
- **Routed** (<20): Running away

## 17.3 Triggering Enemy Routs

The most effective way to destroy an army is to break its morale, not kill every soldier.

### Morale Cascade Strategy
- Morale loss spreads - one routing formation can trigger others
- Focus force to break ONE formation, then spread the panic
- Target wavering formations first

### High-Value Morale Targets
| Target | Why |
|--------|-----|
| **Wavering formations** | Close to breaking, small push triggers rout |
| **Main infantry line** | Breaking this collapses the army |
| **Commander** | Massive morale penalty on death |
| **Banner bearer** | Banner loss hurts morale |
| **Isolated formations** | Already stressed, easier to break |

### Commander Targeting
- Commander death causes massive morale penalty
- But commanders usually have bodyguards
- Only target exposed commanders (bodyguard < 5)

## 17.4 Protecting Own Morale

### When Formation Morale < 50 (Wavering)
- Option 1: Pull back before they break
- Option 2: Send reserve to reinforce
- Option 3: Commander rally (commander presence boosts morale)

### When Formation Morale < 30 (Breaking)
- Emergency: Route them AWAY from the main line
- Prevent cascade to other formations

### Isolation Prevention
- Isolated formations break faster
- Keep formations mutually supporting (< 80m apart)

## 17.5 Morale-Aware Targeting

### Archer Targeting for Morale
1. Formation about to fight our infantry (soften them before melee)
2. Already wavering (push them over the edge)
3. Highest value target

### Cavalry Morale Strikes
- Rear charges cause devastating morale shock
- Target formations already fighting (exposed rear)
- Hit weakest morale first

## 17.6 Strategic Withdrawal Before Collapse

### Morale Situation Assessment
- **Critical** (avg < 40): Start organized withdrawal NOW
- **Shaky** (avg < 60): Prepare withdrawal, be ready
- **Dominant** (ours > enemy + 20): We're winning morale war
- **Stable**: Normal combat

### Managed Withdrawal
- Withdraw lowest morale first (they'll break anyway)
- Have healthy formations cover them
- Middle morale: follow the healthiest

---

## Part 17 Summary

| Morale Action | When | How |
|---------------|------|-----|
| **Read enemy morale** | Always | Router count, cohesion, movement |
| **Target weak morale** | Mid-battle | Concentrate force on wavering formation |
| **Trigger cascade** | Enemy weakening | Break one formation, others follow |
| **Protect own morale** | Formation < 50 morale | Pull back, reinforce, commander rally |
| **Withdraw before collapse** | Avg morale < 40 | Organized retreat, covering force |

| Morale Weapons | Effect |
|----------------|--------|
| **Rear cavalry charge** | Devastating morale shock |
| **Killing rout** | Panicked soldiers spreading fear |
| **Commander death** | Army-wide morale collapse |
| **Concentration of force** | Overwhelm one formation, cascade |

---

# Part 18: Coordinated Retreat

Native Bannerlord handles individual panic (agents flee when morale breaks) but doesn't coordinate organized retreats. The Orchestrator can manage fighting withdrawals and preserve force when battle is lost.

## 18.1 Individual Panic vs Organized Retreat

### Native Behavior
- Agent morale drops below threshold → flee toward FleePosition
- No coordination, formation loses cohesion
- Other agents see fleeing, lose morale → cascade of routs

### What We Want
- Formations maintain cohesion while withdrawing
- Covering force screens the withdrawal
- Orderly handoff between formations
- Minimize casualties during withdrawal
- Prevent total rout cascade

## 18.2 Retreat Decision Logic

### When to Retreat
| Condition | Decision |
|-----------|----------|
| Power ratio > 1.5 AND morale > 60 | **No Retreat** - winning |
| Siege defense / last stand | **No Retreat** - fight to death |
| Avg morale < 30 OR remaining < 10 | **Immediate Retreat** - about to collapse |
| Power ratio < 0.4 AND morale < 50 | **Tactical Retreat** - losing badly |
| Casualty rate > 60% AND power < 0.7 | **Tactical Retreat** - not worth continuing |
| Power ratio < 0.6 OR morale < 50 | **Prepare** - get ready but don't execute yet |

### Retreat Types
- **Fighting Withdrawal**: Step by step, still fighting
- **Covering Retreat**: Rearguard holds while others escape
- **Route to Rally**: Broken, regroup at rally point
- **Total Rout**: AVOID THIS - every man for himself

## 18.3 Covering Force (Rearguard)

### Best Rearguard Candidates
| Type | Quality | Why |
|------|---------|-----|
| **Cavalry** | Excellent | Mobile, can escape, can delay |
| **High morale infantry** | Good | Will hold, not run |
| **Shielded troops** | Good | Can hold in shield wall |
| **Low tier troops** | Last resort | Sacrifice to save elites |

### Rearguard Behavior
- Position between enemy and retreating main force
- If cavalry: Harassing skirmish (delay, don't commit)
- If infantry: Defensive hold (shield wall)
- When main force has 150m+ lead, rearguard can start withdrawing

## 18.4 Step-by-Step Withdrawal

### Bounding Overwatch
1. Split formations into two groups
2. Group A holds and covers
3. Group B retreats 100m
4. Swap roles
5. Repeat until at rally point or escaped

### Withdrawal Phases
1. Set up rearguard
2. Main force breaks contact
3. Wait for separation
4. Retreating formations become new rearguard
5. Original rearguard retreats through

## 18.5 Preserving Force When Battle Is Lost

### Force Preservation Priority
| Priority | Type | Why |
|----------|------|-----|
| 1 | **Commander** | Campaign-critical, must survive |
| 2 | **Companions** | Irreplaceable |
| 3 | **Elite troops (T5+)** | Expensive, hard to replace |
| 4 | **Cavalry** | Expensive, useful for pursuit/escape |
| 5 | **Standard troops** | Replaceable |

### Commander Escape
- If mounted: Run toward safe zone
- If bodyguards available: Extract commander
- If cavalry available: Protect commander

## 18.6 Rally Points and Regrouping

### Rally Point Criteria
| Criterion | Why |
|-----------|-----|
| **Spawn point** | Reinforcements arrive, natural anchor |
| **High ground** | Defensible if enemy pursues |
| **Choke point** | Hold with few troops |
| **Visible** | Retreating troops can find it |
| **On escape route** | Can continue retreat if needed |

### Regrouping Logic
1. Formations converge on rally point
2. Wait for arrival (60 second timeout)
3. Assess situation (survivors, enemy pursuing?)
4. If safe: Reform formations
5. If pursued: Continue retreat or set ambush

### Reformation After Retreat
- Merge understrength formations (< 10 troops)
- Rebalance troop types to correct formations
- Reset morale (rallied troops recover +20 morale)

---

## Part 18 Summary

| Retreat Type | When | Execution |
|--------------|------|-----------|
| **Fighting Withdrawal** | Losing but controllable | Bound back, rearguard covers |
| **Covering Retreat** | Need to escape | Sacrifice rearguard, main force flees |
| **Route to Rally** | Formations breaking | Fall back to rally point, regroup |
| **Total Rout** | AVOID THIS | Uncontrolled, maximum casualties |

---

# Part 19: Battle Pacing and Cinematics

Battles should feel epic, not rushed. Current Bannerlord battles often devolve into instant charges and chaotic blobs that end in under a minute. Smarter AI should also mean more cinematic, satisfying battles with tension, drama, and spectacle.

## 19.1 The Problem with Fast Battles

### What Goes Wrong
| Issue | Result |
|-------|--------|
| **Instant charges** | Armies blob together immediately |
| **No formation integrity** | Units scatter, can't see battle lines |
| **No buildup** | No tension before engagement |
| **Too fast** | Battle over in 60-90 seconds |
| **No dramatic moments** | Cavalry charge is just "more units in blob" |
| **Anticlimactic endings** | Grind to last man, no decisive rout |

### What We Want
| Goal | Experience |
|------|------------|
| **Phased combat** | Clear stages: approach, skirmish, melee, climax |
| **Formation integrity** | See actual battle lines, not mobs |
| **Tension** | Armies face off, archers exchange, waiting... |
| **Dramatic charges** | THE moment when cavalry hits |
| **Ebb and flow** | Lines push back and forth |
| **Decisive moments** | Flank collapses, army routes, drama |
| **Satisfying duration** | 3-8 minutes for meaningful battle |

## 19.2 Cinematic Battle Phases

Battles should progress through distinct phases:

```
PHASE 1: DEPLOYMENT (30-60 seconds)
├── Armies form up at spawn points
├── Formations organize and dress ranks
├── Cavalry moves to flanks
└── Dramatic: Two armies facing each other across the field

PHASE 2: APPROACH (30-60 seconds)
├── Armies advance toward each other
├── SLOW, deliberate march (not sprint)
├── Formations maintain cohesion
└── Dramatic: The distance closes, tension builds

PHASE 3: SKIRMISH (30-90 seconds)
├── Archers begin firing at range
├── Infantry may pause/slow while arrows fly
├── Cavalry screens or positions for charge
└── Dramatic: Arrows darken the sky, casualties mount

PHASE 4: ENGAGEMENT (60-180 seconds)
├── Infantry lines meet
├── Shield walls clash
├── Formations push and grind
└── Dramatic: The crash of the lines

PHASE 5: EXPLOITATION (30-120 seconds)
├── Cavalry charges at critical moment
├── Reserves commit to exploit weakness
├── Flanks may collapse
└── Dramatic: The decisive stroke

PHASE 6: RESOLUTION (30-60 seconds)
├── One side's morale breaks
├── Rout spreads
├── Pursuit or regroup
└── Dramatic: Victory and defeat
```

### Minimum Phase Durations
```csharp
// Orchestrator should enforce minimum time in phases
const float MinDeploymentTime = 15f;   // Don't advance until formed
const float MinApproachTime = 20f;     // Slow march, not sprint
const float MinSkirmishTime = 15f;     // Let archers work
const float MinEngageBeforeCharge = 20f; // Infantry fight before cav charges
```

## 19.3 Deliberate Approach Speed

The approach should be dramatic, not a sprint.

### Speed Control
```csharp
// Instead of charging immediately, advance in formation
enum ApproachSpeed
{
    Halt,      // Standing still (forming, waiting)
    Slow,      // Walking pace (shield wall advance)
    Normal,    // Standard march (formation advance)
    Fast,      // Quick march (closing distance)
    Charge     // Full sprint (final approach to melee)
}

void SetApproachSpeed(Formation formation, ApproachSpeed speed)
{
    switch (speed)
    {
        case ApproachSpeed.Slow:
            // Shield wall or formation advance
            formation.SetMovementOrder(MovementOrder.Advance);
            formation.ArrangementOrder = ArrangementOrder.ShieldWall;
            break;
            
        case ApproachSpeed.Normal:
            formation.SetMovementOrder(MovementOrder.Advance);
            // Maintain line formation
            break;
            
        case ApproachSpeed.Fast:
            formation.SetMovementOrder(MovementOrder.Advance);
            // Loosen formation for speed
            break;
            
        case ApproachSpeed.Charge:
            // Only when close enough
            formation.SetMovementOrder(MovementOrder.Charge);
            break;
    }
}
```

### When to Transition Speeds
```
function DecideApproachSpeed(BattleContext context):
    distanceToEnemy = context.DistanceToEnemyCenter
    
    if distanceToEnemy > 150:
        return ApproachSpeed.Normal  // Standard march, maintain formation
    
    elif distanceToEnemy > 80:
        // In archer range - decisions matter
        if context.WeHaveMoreArchers:
            return ApproachSpeed.Slow  // Let archers work
        elif context.EnemyHasMoreArchers:
            return ApproachSpeed.Fast  // Close distance to stop arrows
        else:
            return ApproachSpeed.Normal
    
    elif distanceToEnemy > 30:
        // Close range - prepare for impact
        if context.IsInfantryFormation:
            return ApproachSpeed.Slow  // Maintain formation integrity
        elif context.IsCavalryFormation:
            return ApproachSpeed.Charge  // Cavalry needs momentum
    
    else:
        // Contact imminent
        return ApproachSpeed.Charge
```

### No Premature Charges
```
function ShouldAllowCharge(formation, context):
    // Prevent instant charge at battle start
    
    if context.BattleTime < 20:
        return false  // Minimum 20 seconds before any charge
    
    if formation.IsInfantry:
        // Infantry only charges at close range
        if context.DistanceToEnemy > 40:
            return false
    
    if formation.IsCavalry:
        // Cavalry needs setup time
        if context.BattleTime < 45:
            return false  // Let infantry engage first
        if NOT context.InfantryEngaged:
            return false  // Wait for infantry to pin enemy
    
    return true
```

## 19.4 The Skirmish Phase

Archers should have time to shine before melee.

### Archer Engagement Window
```
function ManageSkirmishPhase(BattleContext context):
    ourArchers = context.Our.GetFormation(FormationClass.Ranged)
    ourInfantry = context.Our.GetFormation(FormationClass.Infantry)
    
    // If we have archers and they're not engaged
    if ourArchers != null AND ourArchers.CountOfUnits > 10:
        distanceToEnemy = Distance(ourInfantry, context.Enemy.Center)
        
        // In archer effective range (50-120m)
        if distanceToEnemy > 50 AND distanceToEnemy < 120:
            // SLOW DOWN infantry to let archers work
            ourInfantry.SetBehavior(SlowAdvance)
            ourArchers.SetBehavior(Fire)
            
            // Hold this phase for minimum time
            if context.SkirmishPhaseDuration < 20:
                return PhasePriority.HoldSkirmish
    
    return PhasePriority.Normal
```

### Shield Wall Advance
```
// Classic "shield wall advancing under arrow fire"
function ShieldWallApproach(infantry, context):
    if context.UnderArcherFire:
        // Slow, protected advance
        infantry.ArrangementOrder = ArrangementOrder.ShieldWall
        infantry.SetBehavior(SlowAdvance)
        
        // Don't charge until through the arrow zone
        infantry.AllowCharge = false
```

## 19.5 Tension Before Contact

The moment before lines clash should be dramatic.

### The Pause Before Impact
```
function CreatePreContactTension(BattleContext context):
    distanceToEnemy = context.DistanceToEnemyInfantry
    
    // At 20-40m range, brief pause/slow for tension
    if distanceToEnemy > 20 AND distanceToEnemy < 40:
        // Formations may briefly halt or slow
        if RandomChance(0.3):  // 30% chance for dramatic pause
            context.Our.Infantry.SetBehavior(HoldBriefly, duration: 3)
            // Soldiers shout, bang shields, war cries
            // Then charge
```

### War Cry Moment
```
function WarCryBeforeCharge(formation, context):
    // At close range, brief pause then charge
    // This creates the "Spartans, ATTACK!" moment
    
    if formation.DistanceToEnemy < 30:
        if NOT formation.HasPausedForCharge:
            formation.SetBehavior(HoldBriefly, duration: 2)
            formation.HasPausedForCharge = true
            // Play war cry sounds here
            return
        
        // After pause, charge
        formation.SetBehavior(Charge)
```

## 19.6 Dramatic Moments

Cavalry charges and reserve commitments should be EVENTS.

### The Perfect Cavalry Charge
```
function OrchestrateCalvaryCharge(cavalry, context):
    // Don't throw cavalry away early
    // The charge should be A MOMENT
    
    // Wait for conditions:
    requirements = [
        context.BattleTime > 60,           // Minimum battle duration
        context.InfantryEngaged,           // Lines are fighting
        context.EnemyFocusedOnInfantry,    // Not watching cavalry
        context.HasFlankAccess,            // Can hit flank/rear
        context.TargetNotBraced            // Not expecting cavalry
    ]
    
    if ALL(requirements):
        // THE CHARGE
        cavalry.SetBehavior(FullCharge)
        cavalry.SetTarget(context.BestImpactPoint)
        cavalry.FormationOrder = ArrangementOrder.Skein  // Wedge
        
        // This should feel like a decisive moment
        Log("CAVALRY CHARGE at " + context.BattleTime)
    else:
        // Wait, position, skirmish
        cavalry.SetBehavior(PositionForCharge)
```

### Reserve Commitment Drama
```
function CommitReserves(reserve, context):
    // Reserves shouldn't trickle in
    // They should hit all at once for maximum impact
    
    // Wait for the right moment
    if NOT ShouldCommitReserve(context):
        reserve.SetBehavior(Hold)
        return
    
    // THE COMMITMENT
    // All reserves go at once
    for each formation in context.Our.Reserves:
        formation.SetBehavior(ChargeToPoint, context.CriticalPoint)
    
    // This should feel like a turning point
    context.ReservesCommitted = true
```

### Flank Collapse
```
function DetectFlankCollapse(context):
    // When a flank breaks, it should cascade dramatically
    
    for each formation in context.Enemy.Formations:
        if formation.MoraleState == Breaking:
            if formation.IsFlankFormation:
                // DRAMATIC MOMENT: Flank is breaking!
                
                // Focus exploitation on the gap
                nearestCavalry = context.Our.GetNearestCavalry(formation)
                if nearestCavalry != null:
                    nearestCavalry.SetBehavior(ExploitBreak, formation)
                
                // Infantry can wheel to envelop
                nearestInfantry = context.Our.GetNearestInfantry(formation)
                if nearestInfantry != null:
                    nearestInfantry.SetBehavior(EnvelopFlank)
```

## 19.7 Ebb and Flow

Lines should push back and forth, not just blob and grind.

### Disengagement Phases

Both sides may disengage to reset, regroup, and prepare for the next clash. Or one side may press their advantage — but risk overextending.

```
ENGAGEMENT CYCLE:

    ┌─────────────────────────────────────────────────────────┐
    │                                                         │
    │   CLASH ─────► GRIND ─────► DECISION POINT              │
    │     ▲                            │                      │
    │     │         ┌──────────────────┴──────────────────┐   │
    │     │         │                                     │   │
    │     │    DISENGAGE                              PRESS   │
    │     │    (Both sides)                        (One side) │
    │     │         │                                     │   │
    │     │    ┌────┴────┐                           ┌────┴───┐
    │     │    │ REGROUP │                           │ PURSUE │
    │     │    │ REFORM  │                           │ RISK   │
    │     │    │ RECOVER │                           │ OVER-  │
    │     │    └────┬────┘                           │EXTEND  │
    │     │         │                                └────┬───┘
    │     └─────────┘                                     │
    │                                                     │
    │     (Next clash with fresh formations)         (Exploitation
    │                                                 or Disaster)
    └─────────────────────────────────────────────────────────┘
```

### Disengagement Decision
```
function ShouldDisengage(formation, context):
    // Reasons to disengage
    
    // DISENGAGE: Heavy casualties, need to absorb reinforcements
    if formation.CasualtyRate > 0.3 AND context.ReinforcementsIncoming:
        return DisengageReason.AbsorbReinforcements
    
    // DISENGAGE: Morale dropping, need to rally
    if formation.Morale < 50 AND formation.Morale > 30:
        return DisengageReason.Rally
    
    // DISENGAGE: Formation cohesion broken
    if formation.Cohesion < 0.4:
        return DisengageReason.Reform
    
    // DISENGAGE: Enemy reserve about to hit us
    if context.EnemyReserveApproaching(formation):
        return DisengageReason.AvoidReserve
    
    return DisengageReason.None

function ExecuteDisengagement(formation, context):
    // Pull back 50-80m, reform, catch breath
    pullbackDistance = 60
    pullbackPos = CalculatePullbackPosition(formation, pullbackDistance)
    
    formation.SetBehavior(FightingRetreat)
    formation.SetDestination(pullbackPos)
    
    // Once at position, reform
    When AtDestination:
        formation.SetBehavior(Reform)
        formation.SetArrangement(ArrangementOrder.Line)
        formation.ReformDuration = 15  // 15 seconds to reform
```

### Pressing the Advantage
```
function ShouldPressAdvantage(context):
    // If enemy is disengaging, we can press — but it's risky
    
    if context.EnemyDisengaging:
        // Calculate risk vs reward
        
        // PRESS: We have cavalry to exploit
        if context.Our.CavalryAvailable AND context.Our.CavalryFresh:
            return PressDecision.CavalryPursuit
        
        // PRESS: Enemy is wavering, one more push breaks them
        if context.Enemy.AverageMorale < 40:
            return PressDecision.BreakMorale
        
        // PRESS: We have reserves, they don't
        if context.Our.HasReserve AND NOT context.Enemy.HasReserve:
            return PressDecision.Overwhelm
        
        // CAUTION: They might be baiting us
        if context.Enemy.HasReserve:
            return PressDecision.Cautious  // Follow but don't overcommit
        
        // CAUTION: We're also tired
        if context.Our.AverageMorale < 60:
            return PressDecision.LetThemGo  // We need rest too
    
    return PressDecision.None

function ExecutePress(decision, context):
    switch decision:
        case CavalryPursuit:
            // Cavalry chases, infantry follows cautiously
            context.Our.Cavalry.SetBehavior(Pursue)
            context.Our.Infantry.SetBehavior(ControlledAdvance)
            break
            
        case BreakMorale:
            // Everyone pushes hard
            for each formation in context.Our.Formations:
                formation.SetBehavior(Charge)
            break
            
        case Overwhelm:
            // Commit reserves now
            context.Our.Reserve.SetBehavior(Charge)
            break
            
        case Cautious:
            // Infantry advances slowly, cavalry screens
            context.Our.Infantry.SetBehavior(ControlledAdvance)
            context.Our.Cavalry.SetBehavior(Screen)
            break
            
        case LetThemGo:
            // Hold position, reform
            for each formation in context.Our.Formations:
                formation.SetBehavior(HoldAndReform)
            break
```

### Overextension Risk
```
function DetectOverextension(context):
    // Pressing too hard can be disastrous
    
    for each formation in context.Our.AdvancingFormations:
        // Check distance from other friendly formations
        isolation = CalculateIsolation(formation, context.Our.Formations)
        
        if isolation > 100:  // More than 100m from support
            // DANGER: Overextended!
            
            // Check for enemy threats
            if context.Enemy.HasFreshFormations:
                // HIGH RISK: Could be counterattacked
                formation.OverextensionRisk = High
                
                // Warn and slow down
                formation.SetBehavior(SlowAdvance)
                formation.MaxAdvanceDistance = 50  // Don't go further
            
            // Check if we're chasing into bad terrain
            if context.Terrain.AheadHasChokePoint OR context.Terrain.AheadHasForest:
                formation.OverextensionRisk = Medium
                formation.SetBehavior(Halt)
        
        // Check for enemy reserve
        if context.Enemy.ReservePosition != null:
            distanceToEnemyReserve = Distance(formation, context.Enemy.ReservePosition)
            
            if distanceToEnemyReserve < 80:
                // DANGER: Walking into their reserve
                formation.SetBehavior(Halt)
                formation.Warning = "Enemy reserve ahead"
```

### Mutual Disengagement
```
function HandleMutualDisengagement(context):
    // Both sides pulled back — now what?
    
    if context.BothSidesDisengaged:
        // This is a pause in the battle
        
        // Phase: RECOVERY
        for each formation in context.All.Formations:
            // Reform
            formation.SetBehavior(Reform)
            
            // Absorb reinforcements
            if formation.HasPendingReinforcements:
                formation.IntegrateReinforcements()
            
            // Morale recovery
            for each agent in formation.Agents:
                if agent.Morale < 60:
                    agent.ChangeMorale(5)  // Slight recovery
        
        // After 30-60 seconds, decide next phase
        recoveryDuration = Random(30, 60)
        
        After(recoveryDuration):
            // Assess situation and start next engagement
            context.Phase = BattlePhase.Approach
            DecideBattlePlan(context)  // May choose different strategy
```

### Line Movement
```
function SimulateLineEbbFlow(context):
    // Lines shouldn't be static - they should breathe
    
    for each formation in context.Our.EngagedInfantry:
        lineBalance = CalculateLineBalance(formation)
        
        if lineBalance > 0.2:  // We're winning this section
            // Push forward slightly
            formation.SetBehavior(ControlledAdvance, distance: 5)
        
        elif lineBalance < -0.2:  // We're losing this section
            // Give ground
            formation.SetBehavior(ControlledWithdraw, distance: 5)
        
        else:
            // Stalemate - hold
            formation.SetBehavior(Hold)

function CalculateLineBalance(formation):
    // Positive = winning, negative = losing
    
    balance = 0
    balance += (formation.Kills - formation.Casualties) * 0.5
    balance += (formation.Morale - 50) * 0.02
    balance += formation.CombatAdvantage  // Terrain, numbers, etc.
    
    return Clamp(balance, -1, 1)
```

### Momentum Shifts
```
function DetectMomentumShift(context):
    // Track who's winning over time
    
    if context.OurMomentum > 0.3:
        // We're winning - press the advantage
        context.BattleTempo = Tempo.Aggressive
        // Formations push forward more
    
    elif context.OurMomentum < -0.3:
        // We're losing - stabilize
        context.BattleTempo = Tempo.Defensive
        // Formations give ground carefully
    
    else:
        // Contested
        context.BattleTempo = Tempo.Balanced
```

## 19.8 Battle Duration Targets

Battles should last long enough to be satisfying.

### Duration By Battle Size
| Battle Size | Target Duration | Minimum Duration |
|-------------|-----------------|------------------|
| **Small** (< 50 total) | 2-4 minutes | 1.5 minutes |
| **Medium** (50-200) | 4-6 minutes | 3 minutes |
| **Large** (200-500) | 6-10 minutes | 5 minutes |
| **Massive** (500+) | 8-15 minutes | 7 minutes |

### Pacing Enforcement
```
function EnforceBattlePacing(context):
    expectedDuration = GetExpectedDuration(context.TotalTroops)
    currentDuration = context.BattleTime
    
    // Battle going too fast?
    if currentDuration < expectedDuration * 0.3:
        // We're in early phase - slow things down
        
        if context.Phase == Approach:
            // Extend approach phase
            SlowAllInfantryAdvance()
        
        if context.Phase == Skirmish:
            // Let archers work longer
            HoldInfantryForArchers()
        
        if context.Phase == Engagement:
            // Don't commit reserves yet
            HoldReserves()
    
    // Battle dragging too long?
    if currentDuration > expectedDuration * 1.5:
        // Force decisive action
        
        if context.HasReserves:
            CommitReserves()
        
        if context.Stalemate:
            ForceFlankingAction()
```

### Anti-Rush Mechanics
```
function PreventPrematureEndings(context):
    // Don't let battles end too quickly
    
    minBattleDuration = GetMinDuration(context.TotalTroops)
    
    if context.BattleTime < minBattleDuration:
        // Slow down aggressive actions
        
        // No mass charges
        for each formation in context.Our.Formations:
            if formation.Behavior == Charge:
                if Distance(formation, context.Enemy.Center) > 50:
                    formation.SetBehavior(ControlledAdvance)
        
        // No committing all forces at once
        if context.ReservesAvailable:
            HoldReserves()
```

## 19.9 Morale-Driven Endings

Battles should end with dramatic routs, not grinding to the last man.

### Rout Threshold
```
function ShouldArmyRout(context):
    // Armies should break and run, not fight to extinction
    
    casualtyRate = context.Our.Casualties / context.Our.Starting
    avgMorale = context.Our.AverageMorale
    formationsRouting = context.Our.Formations.Count(f => f.IsRouting)
    
    // Army breaks when:
    if avgMorale < 25:
        return true  // General collapse
    
    if casualtyRate > 0.5 AND avgMorale < 40:
        return true  // Heavy casualties, morale shattered
    
    if formationsRouting >= context.Our.Formations.Count * 0.5:
        return true  // Half the army is already running
    
    return false
```

### Cascade Rout
```
function TriggerArmyRout(context):
    // When army breaks, it should be dramatic
    
    // All formations begin withdrawing
    for each formation in context.Our.Formations:
        if formation.Morale < 50:
            formation.SetBehavior(Flee)
        else:
            formation.SetBehavior(CoveringRetreat)
    
    // Dramatic moment - the army breaks!
    Log("ARMY ROUT at " + context.BattleTime)
```

### Victory Celebration Moment
```
function VictoryMoment(context):
    // When enemy routs, give a moment of triumph
    
    if context.EnemyRouting:
        // Don't immediately pursue - savor the victory
        HoldFormationsFor(5)  // 5 seconds
        
        // Then controlled pursuit
        for each formation in context.Our.Formations:
            if formation.IsCavalry:
                formation.SetBehavior(Pursuit)
            else:
                formation.SetBehavior(Regroup)
```

## 19.10 Spectacle Preservation

Keep the battle visually impressive throughout.

### Formation Integrity
```
function MaintainFormationIntegrity(formation):
    // Formations should look like formations, not mobs
    
    cohesion = formation.Cohesion
    
    if cohesion < 0.5:
        // Formation is blobbing - tighten up
        formation.SetBehavior(Regroup)
        formation.AllowCharge = false
        
        // Wait for reformation
        WaitUntil(formation.Cohesion > 0.7)
```

### Camera-Worthy Moments
```
// Design battles with spectacle in mind

CinematicMoments = [
    "Armies form up facing each other across field",
    "Shield wall advances under arrow fire",
    "Lines clash with shield bash and spear thrust",
    "Cavalry sweeps around the flank",
    "Reserve charges into the gap",
    "Flank collapses, men flee",
    "Victorious army stands on field"
]

// Each of these should be visible during a battle
// Not lost in instant blob-fighting
```

### Line Visibility
```
function PreserveLineVisibility(context):
    // The player should be able to SEE battle lines
    
    for each formation in context.All.Formations:
        if formation.InMelee:
            // Prevent deep interpenetration
            if formation.Depth > MaxVisibleDepth:
                // Rear ranks hold back
                formation.RearBehavior = Hold
            
            // Prevent full envelopment/blob
            if formation.IsSurrounded:
                // Fight out, don't disappear into enemy
                formation.SetBehavior(FightToBreakout)
```

---

## Part 19 Summary

| Principle | Implementation |
|-----------|----------------|
| **Battles last longer** | 3-10 minutes based on size, minimum durations enforced |
| **Phased combat** | Deployment → Approach → Skirmish → Engage → Exploit → Resolution |
| **Deliberate pacing** | Slow advance, no premature charges |
| **Archer phase** | Infantry slows to let archers work |
| **Tension moments** | Pause before contact, war cries |
| **Dramatic charges** | Cavalry waits for perfect moment |
| **Ebb and flow** | Lines push back and forth |
| **Morale-driven endings** | Routs, not grinding to last man |
| **Formation integrity** | Armies look like armies, not mobs |

| Battle Phase | Duration | What Happens |
|--------------|----------|--------------|
| **Deployment** | 30-60s | Form up, organize |
| **Approach** | 30-60s | Slow march, tension builds |
| **Skirmish** | 30-90s | Archers exchange fire |
| **Engagement** | 60-180s | Lines fight, push/pull |
| **Exploitation** | 30-120s | Cavalry charge, flanks collapse |
| **Resolution** | 30-60s | Rout, pursuit, victory |

| Size | Target Duration | Min Duration |
|------|-----------------|--------------|
| Small (<50) | 2-4 min | 1.5 min |
| Medium (50-200) | 4-6 min | 3 min |
| Large (200-500) | 6-10 min | 5 min |
| Massive (500+) | 8-15 min | 7 min |

---

# Part 20: Reinforcement Intelligence

Native Bannerlord spawns reinforcements on a simple timer with basic formation assignment. The Orchestrator can make reinforcement waves strategic, coordinated with the battle situation.

**Important: This section applies primarily to Army Battles, not Lord Party Battles.**

### When Reinforcements Matter

| Battle Type | Troops | On Field | Reinforcements? |
|-------------|--------|----------|-----------------|
| **Lord Party Battle** | 50-200 total | All | **NO** - everyone fights from start |
| **Army Battle** | 500-2000+ total | 400-500 max | **YES** - waves throughout battle |

In Lord Party Battles (lord vs lord, small parties), the battle size limit (400-500 per side) typically exceeds the total troops, so everyone is on the field immediately. No reinforcement management needed.

In Army Battles (multiple parties forming armies), the total troops exceed the field limit, requiring reinforcement waves. This is where intelligent reinforcement management matters.

## 20.1 Native Reinforcement System

### How Native Works
```csharp
// ACTUAL Campaign battle settings (from SandBoxMissionSpawnHandler)
GlobalReinforcementInterval = 3f;           // Check every 3 seconds
ReinforcementWavePercentage = 0.5f;         // 50% of initial spawn per wave!
MaximumReinforcementWaveCount = BannerlordConfig.GetReinforcementWaveCount();

// Wave spawns when: casualties >= waveSize
// So if 200 troops initially, wave = 100 troops
// Lose 100 troops → BOOM → 100 more spawn

// Spawn methods
enum ReinforcementSpawnMethod
{
    Balanced,   // Maintain percentage on field (not used in campaign)
    Wave,       // BIG waves when casualties reach threshold (CAMPAIGN)
    Fixed       // Fixed batch size (not used in campaign)
}
```

### Wave Size Calculation
```
initialSpawnPerSide = 200-250 (depends on battle size setting)
waveSize = initialSpawnPerSide * 0.5 = 100-125 troops per wave

Trigger: When casualties >= waveSize
Result: 100-125 troops spawn at once, join their formations
```

### Native Formation Assignment
```csharp
// Priority for formation assignment
enum ReinforcementFormationPriority
{
    Default,                // Fallback
    AlternativeCommon,      // Alternative class, 25%+ match
    AlternativeDominant,    // Alternative class, 50%+ match
    EmptyNoMatch,           // Empty formation
    EmptyRepresentativeMatch, // Empty formation, class matches
    Common,                 // Matching class, 25%+
    Dominant                // Matching class, 50%+ (BEST)
}

// Formation score
score = 0.6 * (1 - formationSize/teamSize)  // Prefer understrength
      + 0.4 * (1 - distanceToSpawn/250);    // Prefer near spawn
```

### Native Limitations
- Spawns on fixed timer regardless of battle state
- No consideration of tactical situation
- Reinforcements may spawn into bad positions
- No coordination with battle plan
- No quality/tier distribution logic

### Important: No Combat Fatigue System
Native Bannerlord has **NO fatigue/stamina system during battles**. The only stamina in the game is for smithing at the forge. Soldiers do not tire from fighting.

For our AI, we use **Health + Morale** as a "combat readiness" proxy:
```
combatReadiness = agent.Health * agent.Morale / 100

// A soldier at 50% HP and 60% morale has readiness of 30
// A fresh soldier at 100% HP and 80% morale has readiness of 80
// We treat low-readiness troops as "spent" even without true fatigue
```

This affects Unit Rotation, Relief in Place, and other decisions where we'd normally consider "tiredness".

### Battle Type Detection
```csharp
// Determine if this is a reinforcement battle
bool IsReinforcementBattle(Mission mission)
{
    var spawnLogic = mission.GetMissionBehavior<MissionAgentSpawnLogic>();
    
    // Check if there are remaining troops to spawn
    int remainingDefenders = spawnLogic.NumberOfRemainingDefenderTroops;
    int remainingAttackers = spawnLogic.NumberOfRemainingAttackerTroops;
    
    // If remaining troops > 0, this is an Army Battle with reinforcements
    return remainingDefenders > 0 || remainingAttackers > 0;
}

// Skip reinforcement logic for Lord Party Battles
void OnMissionTick(float dt)
{
    if (!IsReinforcementBattle(Mission.Current))
    {
        // Lord Party Battle - skip all reinforcement intelligence
        // Everyone is already on the field
        return;
    }
    
    // Army Battle - apply reinforcement intelligence
    UpdateReinforcementStrategy(context);
}
```

## 20.2 Problems with Native Reinforcements

| Problem | Why It's Bad |
|---------|--------------|
| **Fixed timer** | Reinforcements arrive during retreat |
| **Dumb assignment** | Just fills understrength formations |
| **No quality control** | Elite troops may all go to one formation |
| **Spawn timing** | Wave arrives when it's too late |
| **No coordination** | Left flank collapses, reinforcements go to right |
| **Enemy spawn camping** | Enemy cavalry farms your spawn point |

## 20.3 Strategic Wave Timing

Don't spawn reinforcements into a disaster.

### When to Hold Reinforcements
```
function ShouldHoldReinforcements(BattleContext context):
    // HOLD: We're retreating
    if context.AllFormationsRetreating:
        return HoldReason.Retreating
    
    // HOLD: Enemy has cavalry near our spawn
    enemyCavNearSpawn = context.Enemy.Cavalry
        .Where(c => Distance(c, context.OurSpawnPoint) < 100)
        .Any()
    
    if enemyCavNearSpawn:
        return HoldReason.SpawnCamped
    
    // HOLD: Battle is lost, don't waste troops
    if context.BattleLost AND NOT context.MaxCasualtiesMode:
        return HoldReason.PreservingForce
    
    // HOLD: About to retreat (don't commit more)
    if context.RetreatImminent:
        return HoldReason.RetreatPending
    
    // SPAWN: Otherwise, bring them in
    return HoldReason.None
```

### When to Rush Reinforcements
```
function ShouldRushReinforcements(BattleContext context):
    // RUSH: Critical formation about to break
    criticalFormation = context.Our.Formations
        .Where(f => f.IsMainInfantry OR f.HoldingCriticalPosition)
        .Where(f => f.Morale < 40 OR f.CasualtyRate > 0.5)
        .FirstOrDefault()
    
    if criticalFormation != null:
        return RushReason.CriticalFormation
    
    // RUSH: We're winning and can press advantage
    if context.Winning AND context.EnemyWavering:
        return RushReason.ExploitAdvantage
    
    // RUSH: About to commit reserves (need numbers)
    if context.ReserveCommitmentPending:
        return RushReason.SupportingCommitment
    
    return RushReason.None
```

### Wave Timing State Machine
```
enum ReinforcementState
{
    Normal,         // Use standard timer
    Holding,        // Don't spawn
    Rushing,        // Spawn immediately
    Coordinated     // Spawn with tactical trigger
}

function UpdateReinforcementState(context):
    holdReason = ShouldHoldReinforcements(context)
    rushReason = ShouldRushReinforcements(context)
    
    if holdReason != None:
        return ReinforcementState.Holding
    elif rushReason != None:
        return ReinforcementState.Rushing
    elif context.Phase == Exploitation:
        return ReinforcementState.Coordinated
    else:
        return ReinforcementState.Normal
```

## 20.4 Big Wave Strategic Impact

With 100+ troops spawning at once, reinforcement waves are major battle events, not gradual trickles.

### Wave as Battlefield Event
```
WAVE IMPACT VISUALIZATION:

    Before Wave:
    ┌─────────────────────────────────────────────────────────┐
    │  ENEMY: ████████████████  (200 troops)                  │
    │                                                         │
    │  US:    ████████░░░░░░░░  (100 troops, 100 dead)       │
    │                                                         │
    │  Status: Losing badly, line about to break              │
    └─────────────────────────────────────────────────────────┘
    
    *BOOM* — Wave spawns! (100 fresh troops)
    
    After Wave:
    ┌─────────────────────────────────────────────────────────┐
    │  ENEMY: ██████████████░░  (180 troops, some casualties) │
    │                                                         │
    │  US:    ████████████████  (200 troops, FRESH wave!)    │
    │                                                         │
    │  Status: Suddenly even, or even advantaged              │
    └─────────────────────────────────────────────────────────┘
```

### Reacting to Waves
```
function OnWaveSpawning(side, waveSize, context):
    // This is a major battlefield event — 100+ troops at once
    
    log("WAVE: {} troops spawning for {}", waveSize, side)
    
    if side == OurSide:
        // OPPORTUNITY: We just got 100+ fresh troops
        
        // Option 1: Push now while enemy deals with our fresh troops
        if context.EnemyTired AND context.WeControlSpawn:
            SetStrategyMode(AggressiveIntegration)
        
        // Option 2: Pull back to spawn to integrate
        if context.CurrentlyLosing:
            SetStrategyMode(FightingRetreat_ToSpawn)
        
        // Option 3: Launch coordinated attack with wave
        if context.PlanReady:
            ExecuteAttackWithFreshTroops(waveSize)
    
    else:  // Enemy wave (100+ fresh enemies!)
        // DANGER: Enemy getting 100+ fresh troops at once
        
        // Option 1: Attack their spawn point NOW
        if context.WeHaveCavalry AND context.CanReachEnemySpawn:
            DisruptEnemySpawn()  // Kill them as they spawn
        
        // Option 2: Push hard BEFORE wave integrates
        if context.EngagementActive:
            PushHardBeforeWaveArrives()  // Break them now
        
        // Option 3: Disengage and prepare for fresh enemy
        if context.NearEnemySpawn:
            TacticalWithdrawal()  // Don't fight fresh troops
```

### Spawn Point as Strategic Terrain (Army Battles)
```
function DecideSpawnPosture(context):
    // In Army Battles, spawn point matters a LOT
    
    spawnDistance = Distance(context.Our.Center, context.Our.SpawnPoint)
    wavesLeft = context.WavesRemaining
    
    if wavesLeft > 2:
        // Many waves left — fight near our spawn
        // Fresh troops join immediately
        // Wounded can escape to spawn area
        if spawnDistance > 150:
            return PostureDecision.PullBackTowardSpawn
        else:
            return PostureDecision.HoldNearSpawn
    
    else if wavesLeft == 1:
        // Last wave — can push out a bit
        return PostureDecision.Neutral
    
    else:  // No waves left
        // All in — spawn doesn't matter anymore
        return PostureDecision.FullCommit

    // Conversely: Attack enemy spawn if possible
    if context.WeControlCenter AND context.EnemyHasWavesRemaining:
        if context.CavalryAvailable:
            AssignCavalryToHarassSpawn()  // Kill as they spawn
```

## 20.5 Formation-Aware Assignment

Assign reinforcements where they're NEEDED, not just to understrength formations.

### Tactical Need Scoring
```
function ScoreFormationNeed(formation, context):
    score = 0
    
    // Base: How understrength is it?
    strengthRatio = formation.CountOfUnits / formation.TargetSize
    score += (1 - strengthRatio) * 30  // Up to 30 for empty
    
    // Tactical importance
    if formation.IsMainInfantry:
        score += 20  // Main line is critical
    if formation.HoldingChokePoint:
        score += 25  // Choke point must hold
    if formation.Role == Reserve:
        score -= 10  // Reserves less urgent
    
    // Current pressure
    if formation.InMeleeCombat:
        score += 15  // In the fight, needs bodies
    if formation.UnderHeavyFire:
        score += 10  // Taking casualties
    
    // Morale state
    if formation.Morale < 40:
        score += 20  // About to break, fresh troops boost morale
    
    // Position value
    if formation.Position == CriticalFlank:
        score += 15
    if formation.Position == Refused:
        score -= 5  // Less important
    
    return score
```

### Override Native Assignment
```
function AssignReinforcementsStrategically(troops, context):
    assignments = []
    
    // Score all formations
    formationScores = context.Our.Formations
        .Select(f => { Formation = f, Score = ScoreFormationNeed(f, context) })
        .OrderByDescending(f => f.Score)
    
    // Assign troops to highest-need formations
    for each troop in troops:
        troopClass = GetTroopClass(troop)  // Infantry, Ranged, Cavalry
        
        // Find best matching formation of appropriate class
        bestFormation = formationScores
            .Where(f => f.Formation.AcceptsClass(troopClass))
            .FirstOrDefault()
        
        if bestFormation != null:
            assignments.Add((troop, bestFormation.Formation.Index))
            // Reduce score slightly (don't over-reinforce one)
            bestFormation.Score -= 5
    
    return assignments
```

## 20.6 Quality Distribution

Don't put all elites in one formation or all green troops in another.

### Troop Quality Balancing
```
function DistributeByQuality(troops, formations, context):
    // Sort troops by tier/quality
    sortedTroops = troops.OrderByDescending(t => GetTroopQuality(t))
    
    // Round-robin by quality bands
    elites = sortedTroops.Where(t => t.Tier >= 5)
    veterans = sortedTroops.Where(t => t.Tier >= 3 AND t.Tier < 5)
    regulars = sortedTroops.Where(t => t.Tier < 3)
    
    assignments = []
    
    // Elites: To formations under most pressure
    pressuredFormations = formations.OrderByDescending(f => f.CombatPressure)
    for each elite in elites:
        formation = GetMatchingFormation(elite, pressuredFormations)
        assignments.Add((elite, formation))
        // Rotate to spread elites
        pressuredFormations = pressuredFormations.Skip(1).Concat(pressuredFormations.Take(1))
    
    // Veterans: Balanced distribution
    for i, veteran in enumerate(veterans):
        formation = formations[i % formations.Count]
        if formation.AcceptsClass(veteran):
            assignments.Add((veteran, formation))
    
    // Regulars: Fill remaining slots
    for regular in regulars:
        formation = GetLowestStrengthFormation(regular.Class, formations)
        assignments.Add((regular, formation))
    
    return assignments
```

### Prevent Quality Stacking
```
function PreventQualityStacking(assignments, formations):
    // Check for formations with too many elites or too many greens
    
    for each formation in formations:
        formationTroops = assignments.Where(a => a.Formation == formation)
        
        eliteRatio = formationTroops.Count(t => t.Tier >= 5) / formationTroops.Count
        greenRatio = formationTroops.Count(t => t.Tier < 3) / formationTroops.Count
        
        // Rebalance if too skewed
        if eliteRatio > 0.5:
            // Too many elites, redistribute some
            RedistributeElites(formation, formations)
        
        if greenRatio > 0.7:
            // Too many greens, swap some with veterans from other formations
            SwapForVeterans(formation, formations)
```

## 20.7 Spawn Point Tactics

Use spawn point positioning tactically.

### Spawn Near Your Lines
```
function CalculateReinforcementPosition(context):
    // Native spawns at fixed spawn point
    // We can influence where reinforcements form up after spawn
    
    // Ideal: Behind our lines, not at spawn point far away
    ourCenter = context.Our.FormationCenter
    spawnPoint = context.OurSpawnPoint
    
    // Rally reinforcements to a point behind our line
    rallyPoint = CalculatePointBetween(spawnPoint, ourCenter, 0.7)
    // 70% of way from spawn to our lines
    
    return rallyPoint
```

### Spawn Point Defense
```
function ProtectSpawnPoint(context):
    spawnPoint = context.OurSpawnPoint
    
    // Check for enemy near spawn
    enemiesNearSpawn = context.Enemy.Agents
        .Where(a => Distance(a, spawnPoint) < 80)
    
    if enemiesNearSpawn.Any():
        // Spawn is threatened!
        
        // Option 1: Delay reinforcements
        context.HoldReinforcements = true
        
        // Option 2: Send cavalry to clear spawn
        cavalry = context.Our.GetFormation(FormationClass.Cavalry)
        if cavalry != null AND NOT cavalry.InMeleeCombat:
            cavalry.SetBehavior(ClearSpawn)
            cavalry.SetTarget(spawnPoint)
        
        // Option 3: If we have reserves, use them
        reserve = context.Our.Reserve
        if reserve != null:
            reserve.SetBehavior(ProtectSpawn)
```

### Fighting Near Spawn (Large Battles)
```
function SpawnAdvantageStrategy(context):
    // In large battles with reinforcement waves, fighting near YOUR spawn is advantageous
    
    if context.IsLargeBattle AND context.HasRemainingReinforcements:
        ourSpawn = context.OurSpawnPoint
        theirSpawn = context.EnemySpawnPoint
        battleCenter = context.BattleCenter
        
        // Calculate which spawn is closer to battle
        ourSpawnProximity = Distance(ourSpawn, battleCenter)
        theirSpawnProximity = Distance(theirSpawn, battleCenter)
        
        if ourSpawnProximity < theirSpawnProximity:
            // Good! Battle is near our spawn
            context.SpawnAdvantage = true
            // Our reinforcements arrive fresh and fast
        else:
            // Bad! Consider repositioning
            context.SpawnAdvantage = false
            
            if context.CanReposition:
                // Fall back toward our spawn
                SetFormationObjective(context.Our.Infantry, FallBackToward, ourSpawn)
```

## 20.8 Reinforcement Integration

Help new arrivals integrate smoothly.

### Fresh Troop Handling
```
function IntegrateReinforcements(newTroops, formation, context):
    // New troops are fresh but disoriented
    
    // Give them a moment to form up
    if formation.RecentReinforcements:
        formation.FormUpPause = 5  // 5 seconds to integrate
    
    // Place fresh troops in rear ranks initially
    for each troop in newTroops:
        formation.PlaceInRank(troop, Rank.Rear)
    
    // After form-up pause, shuffle based on quality
    After(5 seconds):
        ReorganizeFormation(formation)
```

### Morale Boost from Reinforcements
```
function ApplyReinforcementMoraleBoost(formation, reinforcementCount):
    // Fresh troops arriving boosts morale of tired veterans
    
    boostAmount = Min(reinforcementCount * 2, 15)  // Up to +15 morale
    
    for each agent in formation.ExistingAgents:
        agent.ChangeMorale(boostAmount)
    
    // Log for dramatic effect
    Log("Reinforcements arrived! Formation morale boosted")
```

### Formation Cohesion After Reinforcement
```
function MaintainCohesionDuringReinforcement(formation):
    // Prevent formation from becoming disorganized
    
    if formation.ReceivingReinforcements:
        // Tighten formation
        formation.SetArrangement(ArrangementOrder.Line)
        
        // Slow/stop advance briefly
        if NOT formation.InMeleeCombat:
            formation.SetBehavior(HoldPosition)
            formation.HoldDuration = 10  // 10 seconds to absorb reinforcements
```

## 20.9 Wave Coordination Between Sides

Both sides get reinforcements. Coordinate timing.

### Reinforcement Parity Tracking
```
function TrackReinforcementParity(context):
    ourReinforcements = context.Our.ReinforcementsPending
    theirReinforcements = context.Enemy.ReinforcementsEstimate  // Estimate from starting numbers
    
    ourRecentWave = context.Our.LastWaveSize
    theirRecentWave = context.Enemy.LastWaveSize  // Observe when they spawn
    
    // Track relative reinforcement advantage
    context.ReinforcementBalance = ourReinforcements - theirReinforcements
```

### Counter-Wave Timing
```
function TimeWaveStrategically(context):
    // Don't spawn right after enemy gets a wave
    // Wait for their fresh troops to be committed
    
    if context.Enemy.JustReceivedReinforcements:
        timeSinceEnemyWave = context.Time - context.Enemy.LastWaveTime
        
        if timeSinceEnemyWave < 30:
            // Their fresh troops not committed yet
            // Delay our wave slightly
            context.DelayNextWave = 15  // Wait 15 more seconds
    
    // Ideal: Our wave arrives when their wave is depleted
    if context.Enemy.WaveDepleted AND context.Our.WaveReady:
        context.SpawnImmediately = true
```

### Attrition Warfare Mode
```
function AttritionReinforcementStrategy(context):
    // When both sides have many reinforcements, it's a war of attrition
    
    if context.IsAttritionBattle:
        // Priority: Preserve our reinforcements longer than theirs
        
        // Conservative spawning
        context.ReinforcementStrategy = "Conservative"
        
        // Spawn smaller waves, more frequently
        context.WaveSize = context.DefaultWaveSize * 0.5
        context.WaveInterval = context.DefaultInterval * 0.7
        
        // Focus on defense, let them spend troops attacking
        if context.OurRole == Defender:
            context.SpawnOnlyWhenNeeded = true
```

## 20.10 Desperation Waves

When things are dire, change reinforcement strategy.

### Last Stand Reinforcements
```
function DesperationReinforcementMode(context):
    if context.Our.RemainingReinforcements < 20:
        // Few reinforcements left
        context.ReinforcementMode = "LastStand"
        
        // All remaining come at once for maximum impact
        context.SpawnAll = true
        
        // Direct them to the main fight
        context.ReinforcementRallyPoint = context.BattleCenter
        
        // Morale bonus for "the last reserve"
        for each troop in context.IncomingReinforcements:
            troop.MoraleBonus = 10  // Fighting with their back to the wall
```

### Desperation Cavalry Call
```
function EmergencyCavalryReinforcement(context):
    // If our infantry is about to break, rush cavalry reinforcements
    
    if context.Our.MainInfantry.Morale < 30:
        cavalryReinforcements = context.Our.PendingReinforcements
            .Where(t => t.IsMounted)
        
        if cavalryReinforcements.Any():
            // Rush cavalry NOW
            SpawnImmediately(cavalryReinforcements)
            
            // Direct them to flank the enemy engaging our infantry
            cavalry.SetBehavior(FlankCharge)
            cavalry.SetTarget(context.Enemy.EngagingInfantry)
```

## 20.11 Cinematic Reinforcement Phases

The native system spawns 100+ troops at once — but they instantly teleport into formations. We can make this dramatic.

### The Problem with Native
```
Native behavior:
  - Wave spawns at spawn point
  - Troops immediately assigned to existing formations
  - They run individually to join their formation
  - No cohesion, no drama, just "more guys appeared"
```

### The Vision: Reinforcement as Battle Phase
```
CINEMATIC REINFORCEMENT FLOW:

    ┌─────────────────────────────────────────────────────────────────┐
    │  MAIN LINE FIGHTING                                             │
    │  ████████████████  vs  ████████████████                        │
    │                                                                 │
    │  (Battle rages, casualties mounting)                            │
    └─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
    ┌─────────────────────────────────────────────────────────────────┐
    │  WAVE SPAWNS (100+ troops)                                      │
    │                                                                 │
    │  ░░░░░░░░░░░░░░░░  ← Fresh troops at spawn                     │
    │                                                                 │
    │  Instead of running individually...                             │
    └─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
    ┌─────────────────────────────────────────────────────────────────┐
    │  STAGING PHASE (30-60 seconds)                                  │
    │                                                                 │
    │  MAIN LINE:  ██████████░░░░  (battered, holding)               │
    │                                                                 │
    │  STAGING:    ████████████████  (forming up behind)             │
    │              Infantry in line, cavalry on flanks                │
    │              Archers getting positioned                         │
    │                                                                 │
    │  SKIRMISH:   ⚔ Cavalry duels, archer exchanges                 │
    │              (both sides probing while reforming)               │
    └─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
    ┌─────────────────────────────────────────────────────────────────┐
    │  DECISION POINT: Merge or New Line?                            │
    │                                                                 │
    │  Option A: MERGE INTO MAIN LINE                                 │
    │    - Main line still strong (>50% strength)                     │
    │    - Reinforcements fill gaps                                   │
    │    - Continuous pressure maintained                             │
    │                                                                 │
    │  Option B: FORM SECOND LINE                                     │
    │    - Main line shattered (<30% strength)                        │
    │    - Main line pulls back THROUGH reinforcements                │
    │    - Fresh troops become new front line                         │
    │    - Survivors reform as reserve behind                         │
    │                                                                 │
    │  Option C: RELIEF IN PLACE                                      │
    │    - Main line exhausted but holding                            │
    │    - Reinforcements advance to line                             │
    │    - Main line systematically replaced                          │
    │    - Tired troops rotate to rear                                │
    └─────────────────────────────────────────────────────────────────┘
```

### Implementation: Staging Formation System
```
class ReinforcementStagingManager
{
    // Instead of assigning to existing formations immediately,
    // create temporary STAGING formations at spawn point
    
    function OnWaveSpawning(troops, side, context):
        // Create staging formations behind the line
        stagingPos = CalculateStagingPosition(context)
        
        // Group by type
        stagingInfantry = CreateStagingFormation(FormationType.Infantry, stagingPos)
        stagingArchers = CreateStagingFormation(FormationType.Ranged, stagingPos + offset)
        stagingCavalry = CreateStagingFormation(FormationType.Cavalry, stagingPos + flankOffset)
        
        // Assign troops to staging (not main formations yet)
        for each troop in troops:
            switch troop.Class:
                case Infantry: stagingInfantry.Add(troop)
                case Ranged: stagingArchers.Add(troop)
                case Cavalry: stagingCavalry.Add(troop)
        
        // Order: Form up at staging position
        stagingInfantry.SetArrangement(Line)
        stagingInfantry.SetBehavior(HoldPosition)
        
        // Begin staging phase
        context.Phase = BattlePhase.Staging
        context.StagingTimer = 45  // 45 seconds to form up
        
        return StagingFormations(stagingInfantry, stagingArchers, stagingCavalry)
    
    function CalculateStagingPosition(context):
        // 80-120m behind the current main line
        mainLineCenter = context.Our.MainInfantry.Position
        retreatDirection = (context.Our.SpawnPoint - mainLineCenter).Normalized
        
        stagingDistance = 100  // meters behind main line
        return mainLineCenter + retreatDirection * stagingDistance
```

### Skirmish Phase While Staging
```
function ManageSkirmishPhase(context):
    // While infantry is staging, cavalry and archers can skirmish
    
    if context.Our.StagingInProgress:
        // CAVALRY: Probe and harass
        if context.Our.Cavalry.Available:
            // Don't commit fully — just probe
            context.Our.Cavalry.SetBehavior(Skirmish)
            context.Our.Cavalry.SetAggressionLevel(0.3)  // Cautious
            
            // Target enemy archers or cavalry, not infantry
            if context.Enemy.Archers.Exposed:
                context.Our.Cavalry.SetTarget(context.Enemy.Archers)
            else if context.Enemy.Cavalry.Probing:
                context.Our.Cavalry.SetTarget(context.Enemy.Cavalry)
        
        // ARCHERS: Suppressive fire
        if context.Our.Archers.Available:
            context.Our.Archers.SetBehavior(LooseFormation)
            context.Our.Archers.SetFireMode(Volley)
            
            // Keep enemy busy while we reform
            context.Our.Archers.SetTarget(context.Enemy.MainInfantry)
        
        // MAIN LINE: Defensive posture
        context.Our.MainInfantry.SetBehavior(ShieldWall)
        context.Our.MainInfantry.SetAggressionLevel(0.2)  // Hold, don't push
```

### Orchestrator: Exploit Enemy Staging
```
function CheckStagingExploitation(context):
    // If ENEMY is staging reinforcements, this is our chance!
    
    if context.Enemy.StagingInProgress:
        enemyStagingTroops = context.Enemy.StagingFormations.TotalCount
        enemyMainLineStrength = context.Enemy.MainInfantry.Count
        
        // Calculate window of opportunity
        stagingTimeRemaining = context.Enemy.StagingTimer
        
        if stagingTimeRemaining > 30:
            // They need 30+ seconds to form up
            // We have a window to attack their weakened main line
            
            ourStrength = context.Our.TotalCombatPower
            theirCurrentStrength = context.Enemy.MainInfantry.CombatPower
            
            if ourStrength > theirCurrentStrength * 1.3:
                // We have 30% advantage RIGHT NOW
                // Push before their reinforcements form up!
                
                return ExploitDecision.PushNow(
                    reason: "Enemy staging - attack before they reform",
                    target: context.Enemy.MainInfantry,
                    urgency: High,
                    timeWindow: stagingTimeRemaining - 15  // 15s buffer
                )
        
        // Alternative: Harass their staging formations
        if context.Our.Cavalry.Fresh:
            return ExploitDecision.HarassStagingArea(
                target: context.Enemy.StagingFormations,
                goal: "Disrupt formation, delay integration"
            )
    
    return ExploitDecision.None
```

### Decision: Merge vs New Line
```
function DecideReinforcementIntegration(context):
    mainLineStrength = context.Our.MainInfantry.StrengthRatio  // 0.0 - 1.0
    mainLineMorale = context.Our.MainInfantry.AverageMorale
    mainLineCohesion = context.Our.MainInfantry.Cohesion
    
    stagingStrength = context.Our.StagingFormations.TotalCount
    
    // MERGE: Main line still viable
    if mainLineStrength > 0.5 AND mainLineMorale > 40:
        return IntegrationDecision.Merge(
            method: "Reinforcements advance to fill gaps in main line",
            execution: function():
                // Dissolve staging, assign to main formations
                for each troop in context.Our.StagingFormations:
                    targetFormation = FindWeakestFormation(troop.Class)
                    targetFormation.Add(troop)
        )
    
    // NEW LINE: Main line shattered, form fresh front
    if mainLineStrength < 0.3 OR mainLineMorale < 30:
        return IntegrationDecision.NewLine(
            method: "Staging becomes new front; survivors fall back",
            execution: function():
                // Convert staging to main formations
                context.Our.StagingInfantry.Rename("Main Infantry")
                context.Our.StagingInfantry.SetRole(MainLine)
                
                // Order battered main line to fall back THROUGH staging
                context.Our.MainInfantry.SetBehavior(FightingRetreat)
                context.Our.MainInfantry.SetDestination(BehindStagingLine)
                
                // Once through, old main line becomes reserve
                When context.Our.MainInfantry.Behind(context.Our.StagingInfantry):
                    context.Our.MainInfantry.SetRole(Reserve)
                    context.Our.MainInfantry.SetBehavior(Rally)
        )
    
    // RELIEF IN PLACE: Main line holding but exhausted
    if mainLineStrength > 0.3 AND mainLineMorale < 50:
        return IntegrationDecision.Relief(
            method: "Systematic replacement - fresh troops swap with tired",
            execution: function():
                // Staging advances to main line position
                context.Our.StagingInfantry.SetDestination(context.Our.MainInfantry.Position)
                
                // As staging arrives, main line peels off to rear
                // Do this in segments to maintain line integrity
                for each segment in context.Our.MainInfantry.Segments:
                    When stagingSegment.Arrives(segment.Position):
                        segment.SetBehavior(WithdrawToRear)
                        stagingSegment.SetBehavior(HoldLine)
        )
```

### New Battle Line Formation
```
function FormNewBattleLine(stagingFormations, oldMainLine, context):
    // This is the dramatic "second wave becomes the fight" moment
    
    // Step 1: Staging formations advance to become front line
    newLinePosition = CalculateNewLinePosition(context)
    
    stagingFormations.Infantry.SetBehavior(AdvanceInLine)
    stagingFormations.Infantry.SetDestination(newLinePosition)
    stagingFormations.Infantry.SetArrangement(Line)
    
    // Step 2: Old main line withdraws THROUGH the new line
    withdrawPath = CalculateWithdrawPath(oldMainLine, stagingFormations)
    
    oldMainLine.SetBehavior(FightingRetreat)
    oldMainLine.SetPathThrough(stagingFormations)  // Gaps open to let them through
    
    // Step 3: Cavalry covers the transition
    if context.Our.Cavalry.Available:
        context.Our.Cavalry.SetBehavior(Screen)
        context.Our.Cavalry.SetScreenTarget(oldMainLine)  // Protect withdrawing troops
    
    // Step 4: Once old line is through, they become reserve
    When oldMainLine.Behind(stagingFormations.Infantry):
        oldMainLine.SetRole(Reserve)
        oldMainLine.SetBehavior(Rally)
        oldMainLine.RecoverMorale(15)  // Morale boost from being relieved
        
        // Staging is now the main line
        stagingFormations.Infantry.SetRole(MainLine)
        
        log("Battle line relieved. Fresh troops now engaging.")
```

### Cavalry Duels During Lulls
```
function ManageCavalryDuels(context):
    // During staging/reformation, cavalry on both sides probe each other
    
    if context.Phase == BattlePhase.Staging OR context.Phase == BattlePhase.Reformation:
        ourCav = context.Our.Cavalry
        theirCav = context.Enemy.Cavalry
        
        if ourCav.Available AND theirCav.Active:
            // Cavalry duel rules:
            // - Don't fully commit (keep 30% in reserve)
            // - Break off if taking heavy casualties
            // - Goal: screen, not destroy
            
            duelForce = ourCav.Split(0.7)  // 70% to duel
            reserveForce = ourCav.Split(0.3)  // 30% reserve
            
            duelForce.SetBehavior(CavalryDuel)
            duelForce.SetTarget(theirCav)
            duelForce.SetBreakOffThreshold(0.3)  // Break off at 30% casualties
            
            reserveForce.SetBehavior(HoldFlank)
            reserveForce.SetReadyToIntervene(true)
            
            // If we win the cavalry duel, harass their staging
            When duelForce.Wins OR theirCav.Withdraws:
                duelForce.SetBehavior(HarassFormingTroops)
                duelForce.SetTarget(context.Enemy.StagingFormations)
```

### Archer Exchanges During Lulls
```
function ManageArcherExchanges(context):
    // Archers keep busy during reformation phases
    
    if context.Phase == BattlePhase.Staging:
        ourArchers = context.Our.Archers
        theirArchers = context.Enemy.Archers
        
        // Suppressive volleys to keep pressure on
        ourArchers.SetBehavior(LooseSkirmish)
        ourArchers.SetFireMode(Volley)
        
        // Target priority during staging:
        // 1. Enemy archers (counter-battery)
        // 2. Enemy staging formations (disrupt formation)
        // 3. Enemy main line (maintain pressure)
        
        if theirArchers.Exposed AND theirArchers.InRange:
            ourArchers.SetTarget(theirArchers)
            ourArchers.SetPriority(CounterBattery)
        else if context.Enemy.StagingFormations.Forming:
            ourArchers.SetTarget(context.Enemy.StagingFormations)
            ourArchers.SetPriority(DisruptFormation)
        else:
            ourArchers.SetTarget(context.Enemy.MainInfantry)
            ourArchers.SetPriority(Suppression)
```

### Orchestrator Push During Enemy Staging
```
function DecidePushDuringEnemyStaging(context):
    // The enemy is reforming — do we attack now?
    
    if NOT context.Enemy.StagingInProgress:
        return PushDecision.None
    
    // Calculate our advantage window
    enemyCurrentStrength = context.Enemy.MainInfantry.CombatPower
    enemyStagingStrength = context.Enemy.StagingFormations.CombatPower
    enemyTotalAfterStaging = enemyCurrentStrength + enemyStagingStrength
    
    ourStrength = context.Our.TotalCombatPower
    
    // Current advantage vs future disadvantage
    currentRatio = ourStrength / enemyCurrentStrength
    futureRatio = ourStrength / enemyTotalAfterStaging
    
    // If we have advantage NOW but won't later — push!
    if currentRatio > 1.3 AND futureRatio < 1.0:
        // We're stronger now but will be weaker when their staging integrates
        // This is our window!
        
        return PushDecision.AttackNow(
            urgency: Critical,
            reason: "Enemy staging - attack before reinforcements integrate",
            objective: "Break their main line before staging arrives",
            commitment: Full,  // This is our shot
            timeWindow: context.Enemy.StagingTimer - 20  // Leave 20s buffer
        )
    
    // Moderate advantage — probe and harass
    if currentRatio > 1.1:
        return PushDecision.Probe(
            urgency: Moderate,
            reason: "Enemy staging - test their line",
            objective: "Force them to integrate early or lose ground",
            commitment: Partial
        )
    
    // No advantage — focus on our own staging
    return PushDecision.Defend(
        reason: "No clear advantage - focus on own reformation"
    )
```

### Formation Integration Mechanics

How do staging troops actually join the main formations? It depends on the integration decision.

#### Option A: MERGE — Dissolve and Reassign
```
function ExecuteMerge(stagingFormations, mainFormations, context):
    // Staging formations dissolve; troops join matching main formations
    
    // Use our custom BattleSpawnModel to assign intelligently
    for each stagingFormation in stagingFormations:
        troopsToAssign = stagingFormation.GetAllAgents()
        
        for each troop in troopsToAssign:
            // Find best main formation for this troop
            targetFormation = FindBestFormationForTroop(troop, mainFormations)
            
            // Transfer agent from staging to main formation
            stagingFormation.RemoveAgent(troop)
            targetFormation.AddAgent(troop)
            
            // Troop runs from staging position to their new formation
            troop.SetBehavior(MoveToFormation)
        
        // Once empty, dissolve staging formation
        stagingFormation.Dissolve()
    
function FindBestFormationForTroop(troop, mainFormations):
    // Priority: matching class, then weakest, then nearest spawn
    
    matchingFormations = mainFormations
        .Where(f => f.RepresentativeClass == troop.Class)
        .OrderBy(f => f.StrengthRatio)  // Weakest first
    
    if matchingFormations.Any():
        return matchingFormations.First()  // Fill weakest matching formation
    
    // Fallback: any formation that accepts this class
    return mainFormations
        .Where(f => f.AcceptsClass(troop.Class))
        .OrderBy(f => f.StrengthRatio)
        .First()
```

#### Option B: NEW LINE — Role Swap, No Reassignment
```
function ExecuteNewLine(stagingFormations, oldMainFormations, context):
    // Staging becomes main; old main becomes reserve
    // NO troop reassignment needed — just swap roles
    
    // Step 1: Old main line retreats through staging
    for each oldFormation in oldMainFormations:
        oldFormation.SetBehavior(FightingRetreat)
        retreatPos = CalculatePositionBehind(stagingFormations)
        oldFormation.SetDestination(retreatPos)
    
    // Step 2: Wait for old line to pass through
    When AllFormationsThrough(oldMainFormations, stagingFormations):
        
        // Step 3: Swap roles — staging is now main
        for each stagingFormation in stagingFormations:
            switch stagingFormation.Class:
                case Infantry:
                    stagingFormation.SetFormationIndex(FormationClass.Infantry)  // Formation 0
                    stagingFormation.SetRole(MainLine)
                    context.Our.MainInfantry = stagingFormation
                    break
                case Ranged:
                    stagingFormation.SetFormationIndex(FormationClass.Ranged)  // Formation 2
                    stagingFormation.SetRole(MainArchers)
                    context.Our.MainArchers = stagingFormation
                    break
                case Cavalry:
                    stagingFormation.SetFormationIndex(FormationClass.Cavalry)  // Formation 3
                    stagingFormation.SetRole(MainCavalry)
                    context.Our.MainCavalry = stagingFormation
                    break
        
        // Step 4: Old formations become reserves
        for each oldFormation in oldMainFormations:
            // Renumber to reserve slots
            oldFormation.SetFormationIndex(oldFormation.Index + 4)  // Move to reserve slots
            oldFormation.SetRole(Reserve)
            oldFormation.SetBehavior(Rally)
            oldFormation.RecoverMorale(10)  // Relief of being pulled back
        
        // Step 5: If old reserve existed, merge survivors into it
        if context.Our.Reserve.Exists:
            for each survivor in oldMainFormations.Agents:
                context.Our.Reserve.Add(survivor)
            oldMainFormations.Dissolve()
```

#### Option C: RELIEF IN PLACE — Segment-by-Segment Swap
```
function ExecuteRelief(stagingFormations, mainFormations, context):
    // Gradual replacement — most complex but cleanest result
    
    // Divide line into segments (e.g., 3 segments: left, center, right)
    mainSegments = DivideIntoSegments(mainFormations.Infantry, 3)
    stagingSegments = DivideIntoSegments(stagingFormations.Infantry, 3)
    
    // Relief one segment at a time, left to right
    for i = 0 to mainSegments.Count:
        mainSegment = mainSegments[i]
        stagingSegment = stagingSegments[i]
        
        // Step 1: Staging segment advances to line position
        stagingSegment.SetDestination(mainSegment.Position)
        stagingSegment.SetBehavior(AdvanceInLine)
        
        // Step 2: When staging arrives, main segment peels off
        When stagingSegment.AtPosition(mainSegment.Position):
            // Main segment withdraws to rear
            mainSegment.SetBehavior(WithdrawToRear)
            mainSegment.SetDestination(context.Our.RallyPoint)
            
            // Staging segment holds the line
            stagingSegment.SetBehavior(HoldLine)
            
            // Wait before next segment (staggered relief)
            Wait(10)  // 10 seconds between segments
    
    // After all segments relieved:
    When AllSegmentsRelieved:
        // Merge old main troops into staging formation
        // (They're now in the same formation, just older troops moved to rear ranks)
        
        for each troop in mainFormations.Infantry.Agents:
            // Transfer to staging (now main) formation
            stagingFormations.Infantry.AddAgent(troop)
            
            // Position in rear ranks (tired troops behind fresh)
            troop.SetRankPosition(RearRanks)
        
        // Dissolve old formation shell
        mainFormations.Infantry.Dissolve()
        
        // Staging is now the only main infantry formation
        stagingFormations.Infantry.SetFormationIndex(FormationClass.Infantry)
```

#### Native API for Formation Transfers
```csharp
// Native Bannerlord API we use:

// Transfer agent between formations
formation.TransferAgent(agent, targetFormation);

// Alternative: Direct assignment
agent.Formation = targetFormation;

// Get/set formation index (0-7)
formation.Index
formation.FormationIndex  // The FormationClass enum value

// Check formation composition
formation.QuerySystem.InfantryUnitRatio
formation.QuerySystem.RangedUnitRatio
formation.CountOfUnits

// Formation orders we'll use
formation.SetMovementOrder(MovementOrder.MovementOrderMove(position));
formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
formation.SetMovementOrder(MovementOrder.MovementOrderRetreat);
formation.ArrangementOrder  // Line, Column, Circle, etc.
```

#### Visual: How Each Integration Looks
```
MERGE (troops run to fill gaps):

Before:
    STAGING:  ████████████████  (100 fresh troops)
    MAIN:     ██░░██░░██░░██░░  (gaps from casualties)

During:
    STAGING:  ██████──────────  (troops running forward)
    MAIN:     ██░→██░→██░→██░→  (arrows = troops joining)

After:
    STAGING:  (dissolved)
    MAIN:     ████████████████  (full strength, mixed fresh/veteran)


NEW LINE (role swap):

Before:
    STAGING:  ████████████████  (fresh, formed behind)
    MAIN:     ██░░░░██░░░░░░██  (shattered, barely holding)

During:
    STAGING:  ████████████████  (holds position)
    MAIN:     ←←←██░░██←←←←←←  (retreating through gaps)

After:
    NEW MAIN: ████████████████  (was staging, now front line)
    RESERVE:  ██████            (survivors, rallying in rear)


RELIEF IN PLACE (segment swap):

Before:
    STAGING:  [AAA][BBB][CCC]   (3 segments behind)
    MAIN:     [aaa][bbb][ccc]   (tired, holding)

Phase 1:
    STAGING:  [→→→][BBB][CCC]   (A advances)
    MAIN:     [←←←][bbb][ccc]   (a withdraws)

Phase 2:
    STAGING:  [AAA][→→→][CCC]   (B advances)
    MAIN:     [   ][←←←][ccc]   (b withdraws)

Phase 3:
    STAGING:  [AAA][BBB][→→→]   (C advances)
    MAIN:     [   ][   ][←←←]   (c withdraws)

After:
    MAIN:     [AAA][BBB][CCC]   (fresh front, old troops in rear ranks)
```

### AI Adjusting to Push During Staging
```
function HandleEnemyPushDuringStaging(context):
    // Enemy is attacking while we're still forming up!
    
    if context.Our.StagingInProgress AND context.Enemy.Pushing:
        // Options:
        // 1. Rush staging integration (messy but fast)
        // 2. Commit staging early (fresh but unformed)
        // 3. Main line holds while staging continues
        
        mainLineHoldTime = EstimateHoldTime(context.Our.MainInfantry)
        stagingTimeNeeded = context.Our.StagingTimer
        
        if mainLineHoldTime < stagingTimeNeeded:
            // Main line will break before staging is ready
            // Need to commit staging early!
            
            if mainLineHoldTime < 15:
                // CRITICAL: Main line collapsing
                // Rush staging — messy integration
                return StagingDecision.EmergencyCommit(
                    method: "Staging charges forward to reinforce line immediately",
                    formation: "None - just get bodies there",
                    morale: -10  // Chaotic commitment hurts morale
                )
            else:
                // URGENT: Main line weakening
                // Early commit — partial formation
                return StagingDecision.EarlyCommit(
                    method: "Staging advances now, forms up on the move",
                    formation: "Loose line - not ideal but functional",
                    morale: -5
                )
        else:
            // Main line can hold
            // Complete staging, then commit properly
            return StagingDecision.ContinueStaging(
                method: "Main line holds; staging completes formation",
                mainLineOrder: DefensivePosture,
                stagingPriority: SpeedUp  // Form faster
            )
```

## 20.12 Phase Respect vs Survival Priority

The Orchestrator should prefer cinematic phases — but never sacrifice the battle for aesthetics.

### The Balance
```
PRIORITY HIERARCHY:

    1. SURVIVAL     — Don't lose the battle for cinematics
    2. ADVANTAGE    — Exploit clear opportunities
    3. PHASES       — Follow cinematic flow when safe
    4. AESTHETICS   — Nice-to-have, never mandatory

If Phase says "wait and form up" but Survival says "push now or die"
→ PUSH NOW
```

### Phase Override Conditions
```
function ShouldOverridePhase(currentPhase, context):
    // ALWAYS override phases for survival
    
    // CRITICAL: Main line about to collapse
    if context.Our.MainInfantry.StrengthRatio < 0.2:
        return OverrideReason.MainLineCollapsing
    
    // CRITICAL: Morale cascade starting
    if context.Our.FormationsRouting > 0:
        return OverrideReason.MoraleCrisis
    
    // CRITICAL: Being overrun
    if context.Enemy.DeepInOurTerritory:
        return OverrideReason.BeingOverrun
    
    // URGENT: Clear exploitation window closing
    if context.ExploitationWindow AND context.ExploitationWindow.TimeRemaining < 15:
        return OverrideReason.ClosingWindow
    
    // OPPORTUNITY: Massive advantage we'd waste by waiting
    if context.PowerRatio > 2.0 AND currentPhase == Staging:
        return OverrideReason.WastingAdvantage
    
    // No override — respect the phase
    return OverrideReason.None
```

### Flexible Phase Timing
```
function GetPhaseDuration(phase, context):
    // Base durations (ideal cinematic timing)
    baseDuration = switch phase:
        case Staging: 45
        case Skirmish: 60
        case Reformation: 30
        default: 30
    
    // SHORTEN phases when pressured
    pressureMultiplier = 1.0
    
    if context.Our.UnderPressure:
        pressureMultiplier = 0.5  // Half duration
    
    if context.Our.TakingHeavyCasualties:
        pressureMultiplier = 0.3  // Rush it
    
    if context.Enemy.Pushing:
        pressureMultiplier = 0.4  // Can't wait
    
    // LENGTHEN phases when safe (more cinematic)
    if context.BothSidesReforming:
        pressureMultiplier = 1.5  // Enjoy the lull
    
    if context.Our.StrongAdvantage:
        pressureMultiplier = 1.2  // No rush
    
    return baseDuration * pressureMultiplier

// Example outcomes:
// Normal staging: 45 seconds
// Under pressure: 22 seconds (rush it)
// Both reforming: 67 seconds (take your time, enjoy cavalry duels)
```

### Graceful Phase Interruption
```
function HandlePhaseInterruption(currentPhase, overrideReason, context):
    // Don't just abandon the phase — transition gracefully
    
    switch overrideReason:
        case MainLineCollapsing:
            // EMERGENCY: Skip remaining phase, commit everything
            if currentPhase == Staging:
                // Staging troops charge forward NOW
                context.Our.StagingFormations.SetBehavior(EmergencyCharge)
                context.Our.StagingFormations.SetTarget(context.Enemy.MainInfantry)
                log("PHASE OVERRIDE: Staging committed early - main line failing")
            break
            
        case ClosingWindow:
            // URGENT: Accelerate phase, don't skip
            // Complete in 10 seconds instead of remaining time
            context.PhaseTimer = Min(context.PhaseTimer, 10)
            context.Our.AllFormations.SetUrgency(High)
            log("PHASE OVERRIDE: Accelerating {} - window closing", currentPhase)
            break
            
        case WastingAdvantage:
            // OPPORTUNITY: Can skip, but do it smoothly
            // Give formations 5 seconds to ready themselves
            context.PhaseTimer = 5
            log("PHASE OVERRIDE: Cutting {} short - exploiting advantage", currentPhase)
            break
            
        case BeingOverrun:
            // CRITICAL: Immediate response
            context.Phase = BattlePhase.Emergency
            context.Our.AllFormations.SetBehavior(FightForSurvival)
            break
```

### "Enough Cinematics" Thresholds
```
function CheckCinematicBudget(context):
    // Track how much time we've spent on cinematic phases
    // If battle is dragging or we're losing, cut the art
    
    totalBattleTime = context.BattleTimer
    timeInCinematicPhases = context.TimeInStaging + context.TimeInSkirmish + context.TimeInReformation
    
    cinematicRatio = timeInCinematicPhases / totalBattleTime
    
    // If more than 40% of battle is "waiting around", push things along
    if cinematicRatio > 0.4:
        context.CinematicBudgetExhausted = true
        // Future phases get minimum durations
        context.PhaseMultiplier = 0.5
    
    // If we're losing AND being cinematic, stop it
    if context.Our.Losing AND cinematicRatio > 0.25:
        context.CinematicBudgetExhausted = true
        context.PhaseMultiplier = 0.3
        log("Cutting cinematics - need to focus on winning")
    
    // If winning decisively, can afford more cinematics
    if context.Our.WinningDecisively:
        context.CinematicBudgetExhausted = false
        context.PhaseMultiplier = 1.2  // Enjoy the victory
```

### Individual Skirmisher Duels

During lull phases, small groups of soldiers can advance for individual combat — buying time, testing the enemy, creating cinematic moments.

#### When to Allow Skirmisher Duels
```
function ShouldAllowSkirmisherDuels(context):
    // Only in appropriate phases
    if context.Phase NOT IN [Staging, Skirmish, Reformation]:
        return DuelDecision.No("Wrong phase")
    
    // Only when evenly matched (don't waste men)
    powerRatio = context.Our.CombatPower / context.Enemy.CombatPower
    if powerRatio < 0.7:
        return DuelDecision.No("Outmatched - preserve every soldier")
    if powerRatio > 1.5:
        return DuelDecision.No("Winning easily - no need to risk individuals")
    
    // Only when reinforcements coming (can afford losses)
    if context.Our.ReinforcementsRemaining < 50:
        return DuelDecision.No("Low reinforcements - preserve strength")
    
    // Only when main line is stable
    if context.Our.MainInfantry.StrengthRatio < 0.5:
        return DuelDecision.No("Main line weak - need every man")
    
    // Only reasonable frequency (don't look silly)
    if context.RecentDuelCount > 3:
        return DuelDecision.No("Enough duels recently")
    
    // Good conditions for some heroics
    return DuelDecision.Yes(
        maxSkirmishers: CalculateSkirmisherCount(context),
        duration: 20  // Max 20 seconds per duel event
    )
```

#### How Many Skirmishers
```
function CalculateSkirmisherCount(context):
    // Base: 1-3 soldiers at a time (realistic, not silly)
    baseCount = Random(1, 3)
    
    // Scale slightly with battle size
    if context.TotalTroops > 400:
        baseCount += 1  // Bigger battle, can afford more
    
    // Reduce if we're trading poorly
    if context.Our.SkirmishKillRatio < 0.5:
        baseCount = 1  // We're losing these fights, minimize
    
    // Cap at 5 (more looks like a charge, not duels)
    return Min(baseCount, 5)
```

#### Selecting Skirmishers
```
function SelectSkirmishers(formation, count, context):
    // Pick soldiers suited for individual combat
    candidates = formation.Agents
        .Where(a => a.Health > 0.7)           // Healthy
        .Where(a => a.Morale > 60)            // Brave
        .Where(a => NOT a.IsShieldBearer)     // Don't pull shields from wall
        .Where(a => a.Position.IsInFrontRank) // Already at front
    
    // Prefer skilled fighters
    ranked = candidates
        .OrderByDescending(a => a.GetSkillValue(CombatSkills.OneHanded))
        .ThenByDescending(a => a.GetSkillValue(CombatSkills.Athletics))
    
    selected = ranked.Take(count)
    
    // Don't strip formation if too few remain
    if formation.CountOfUnits - count < 20:
        return []  // Formation too small
    
    return selected
```

#### Executing Skirmisher Duel
```
function ExecuteSkirmisherDuel(skirmishers, context):
    // Calculate "no man's land" position (between the lines)
    noMansLand = (context.Our.MainLine.Position + context.Enemy.MainLine.Position) / 2
    
    // Offset slightly toward our side (safer)
    safeOffset = (context.Our.MainLine.Position - noMansLand).Normalized * 20
    duelPosition = noMansLand + safeOffset
    
    // Detach from formation temporarily
    for each soldier in skirmishers:
        soldier.DetachFromFormation()
        soldier.SetBehavior(SkirmishDuel)
        soldier.SetDestination(duelPosition)
        soldier.SetAggressionLevel(0.8)  // Aggressive but not suicidal
    
    // Set retreat conditions
    for each soldier in skirmishers:
        soldier.SetRetreatCondition(
            healthThreshold: 0.4,      // Retreat if badly wounded
            outnumberedRatio: 3,       // Retreat if 3v1
            friendsDown: 2             // Retreat if 2 friends fall
        )
    
    // Track the duel
    context.ActiveDuel = new DuelEvent(
        skirmishers: skirmishers,
        startTime: context.BattleTimer,
        maxDuration: 20
    )
```

#### Managing Active Duels
```
function ManageActiveDuels(context):
    if context.ActiveDuel == null:
        return
    
    duel = context.ActiveDuel
    
    // Check duration
    if context.BattleTimer - duel.StartTime > duel.MaxDuration:
        RecallSkirmishers(duel, "Time limit reached")
        return
    
    // Check casualties
    surviving = duel.Skirmishers.Where(s => s.IsAlive)
    if surviving.Count == 0:
        // All down - don't send more immediately
        context.DuelCooldown = 30  // 30 second cooldown
        context.ActiveDuel = null
        return
    
    // Check if overwhelmed
    enemySkirmishers = context.Enemy.AgentsNear(duel.Position, 30)
    if enemySkirmishers.Count > surviving.Count * 2:
        RecallSkirmishers(duel, "Outnumbered")
        return
    
    // Check if enemy withdrew
    if enemySkirmishers.Count == 0:
        RecallSkirmishers(duel, "Enemy withdrew - victory")
        context.Our.Morale += 2  // Small morale boost
        return

function RecallSkirmishers(duel, reason):
    log("Recalling skirmishers: {}", reason)
    
    for each soldier in duel.Skirmishers.Where(s => s.IsAlive):
        soldier.SetBehavior(ReturnToFormation)
        soldier.ReattachToFormation()
    
    context.ActiveDuel = null
    context.RecentDuelCount += 1
```

#### Delaying Action Purpose
```
function UseSkirmishersForDelay(context):
    // Skirmishers can buy time during critical staging
    
    if context.Our.StagingInProgress AND context.Enemy.Advancing:
        // Enemy is pushing while we're not ready
        // Send skirmishers to slow them down
        
        delayingForce = SelectSkirmishers(context.Our.MainInfantry, 3, context)
        
        if delayingForce.Count > 0:
            // Position between enemy and our staging
            interceptPosition = CalculateInterceptPoint(
                context.Enemy.AdvanceVector,
                context.Our.StagingFormations.Position
            )
            
            for each soldier in delayingForce:
                soldier.DetachFromFormation()
                soldier.SetBehavior(FightingDelay)
                soldier.SetDestination(interceptPosition)
                soldier.SetObjective(DelayEnemy)
                
                // They know this is a sacrifice
                soldier.SetRetreatCondition(
                    healthThreshold: 0.3,  // Fight longer
                    outnumberedRatio: 5    // Hold even outnumbered
                )
            
            log("Delaying force deployed - buying time for staging")
            
            return DelayDecision.Deployed(
                force: delayingForce,
                objective: "Buy {} seconds for staging".format(context.Our.StagingTimer)
            )
    
    return DelayDecision.NotNeeded
```

#### Visual: Skirmisher Flow
```
SKIRMISH PHASE WITH DUELS:

Main Lines:
    OUR LINE:    ████████████████
                        ↓ ↓ ↓  (2-3 soldiers advance)
    
    No Man's Land:      ⚔ ⚔      (fighting)
                        ↑ ↑
    ENEMY LINE:  ████████████████
                 (enemy sends own skirmishers)

Timeline:
    0s:   Phase starts, lines holding
    10s:  "Conditions good" - select 2 skirmishers
    12s:  Skirmishers advance to no man's land
    15s:  Enemy responds with own skirmishers
    15-30s: Individual combat (cavalry watching, archers covering)
    30s:  Recall skirmishers OR casualties mount
    35s:  Skirmishers return to formation
    40s:  Maybe send another group, or phase ends

CONSTRAINTS:
    - Max 3-5 soldiers at once (realistic)
    - Max 20 seconds per duel event (don't drag on)
    - 30 second cooldown after casualties
    - Only when evenly matched (0.7-1.5 power ratio)
    - Stop if main line needs them
```

#### Orchestrator Control
```
function OrchestratorSkirmishDecision(context):
    // Orchestrator decides if/when to allow duels
    
    // Strategic value of duels
    value = 0
    
    // Buying time for staging? High value
    if context.Our.StagingInProgress:
        value += 30
    
    // Testing enemy response? Medium value
    if context.Enemy.BehaviorUnknown:
        value += 15
    
    // Entertainment/morale during lull? Low value
    if context.Phase == Skirmish:
        value += 10
    
    // Risk assessment
    risk = 0
    
    if context.Our.Outnumbered:
        risk += 40
    
    if context.Our.MainInfantry.Weak:
        risk += 30
    
    if context.Our.LowReinforcements:
        risk += 25
    
    // Decision
    if value > risk:
        return AllowDuels(intensity: value / 10)  // 1-5 scale
    else:
        return ProhibitDuels(reason: "Risk outweighs value")
```

### Smart Skirmish Phase
```
function ManageSkirmishSafely(context):
    // Skirmishing is fun but shouldn't cost us the battle
    
    skirmishCasualties = context.Our.CasualtiesDuringSkirmish
    skirmishTime = context.SkirmishTimer
    
    // If skirmish is costing too much, end it
    if skirmishCasualties > 20 AND skirmishTime > 30:
        // We've lost 20+ troops in 30 seconds of "probing"
        // That's not probing, that's bleeding
        return SkirmishDecision.EndNow(
            reason: "Skirmish too costly",
            action: "Commit or withdraw"
        )
    
    // If cavalry is getting wrecked, pull them back
    if context.Our.Cavalry.CasualtyRate > 0.3:
        context.Our.Cavalry.SetBehavior(Withdraw)
        context.Our.Cavalry.SetStatus(Recovering)
        log("Cavalry pulled from skirmish - 30% casualties")
    
    // If archers are being suppressed, reposition
    if context.Our.Archers.TakingFire AND context.Our.Archers.CasualtyRate > 0.2:
        context.Our.Archers.SetBehavior(Reposition)
        log("Archers repositioning - taking too much fire")
    
    // Skirmish is fine, continue
    return SkirmishDecision.Continue
```

### Phase Summary: Fun But Smart
```
PHASE BEHAVIOR MATRIX:

                    SAFE          PRESSURED      CRITICAL
                    ────          ─────────      ────────
Staging:            45 sec        22 sec         SKIP
Skirmish:           60 sec        30 sec         SKIP
Cavalry Duels:      Yes           Cautious       Pull Back
Archer Exchange:    Full          Suppressive    Cover Fire
Reformation:        30 sec        15 sec         On The Move
Relief in Place:    Full Process  Accelerated    Emergency Merge

OVERRIDE TRIGGERS:
- Main line < 20% → Emergency commit
- Morale breaking → All hands on deck
- 2:1 advantage → Stop waiting, attack
- Window closing → Accelerate everything
- 40%+ time in phases → Enough art, fight
```

### The Goal
```
CINEMATIC BATTLE FLOW:

    Opening → Approach → First Clash → 
    [Skirmish Phase - cavalry duels, archer fire] →
    Main Engagement → Casualties Mount →
    [Reinforcement Wave] →
    [Staging Phase - 30-60 seconds] →
    Integration Decision →
    Second Clash → ...repeat...

BUT if things go wrong:
    Main line collapsing? → Skip staging, commit NOW
    Getting routed? → Forget aesthetics, fight to survive
    Crushing advantage? → Don't wait, exploit it

The AI should PREFER phases but SURVIVE first.
```

## 20.13 Additional Orchestrator Opportunities

More ways the Orchestrator can create interesting, realistic battle moments.

### 1. Reserve Commitment Timing (The Dramatic Charge)
```
function DecideReserveCommitment(context):
    // Reserves should be committed at THE RIGHT MOMENT
    // Too early = wasted; too late = battle lost
    
    reserve = context.Our.Reserve
    if reserve == null OR reserve.Count < 20:
        return ReserveDecision.None
    
    // PERFECT MOMENT: Enemy committed their reserve, ours is fresh
    if context.Enemy.ReserveCommitted AND reserve.Fresh:
        return ReserveDecision.CommitNow(
            target: context.Enemy.WeakestFlank,
            reason: "Counter-stroke while enemy reserves engaged"
        )
    
    // PERFECT MOMENT: Enemy line wavering, one push breaks them
    if context.Enemy.MainInfantry.Morale < 40:
        return ReserveDecision.CommitNow(
            target: context.Enemy.MainInfantry,
            reason: "Finish them - morale breaking"
        )
    
    // DESPERATE: Our line is breaking, reserve is last hope
    if context.Our.MainInfantry.StrengthRatio < 0.3:
        return ReserveDecision.CommitNow(
            target: context.CriticalPoint,
            reason: "Emergency - plug the breach"
        )
    
    // PATIENT: Battle is even, save reserves
    if context.PowerRatio.Between(0.8, 1.2):
        return ReserveDecision.Hold(
            reason: "Battle even - wait for opportunity"
        )
    
    // Default: Hold unless critical
    return ReserveDecision.Hold()
```

### 2. Feigned Retreat (Baiting the Enemy)
```
function ConsiderFeignedRetreat(context):
    // Classic tactic: pretend to flee, enemy pursues in disorder, turn and strike
    
    // Only works if:
    // - Enemy is aggressive and likely to pursue
    // - We have cavalry or reserves to spring the trap
    // - Our troops are disciplined enough to fake retreat
    
    if context.Enemy.Aggression < 0.6:
        return FeignDecision.No("Enemy too cautious - won't pursue")
    
    if NOT context.Our.Cavalry.Available AND NOT context.Our.Reserve.Available:
        return FeignDecision.No("No force to spring the trap")
    
    if context.Our.MainInfantry.AverageDiscipline < 60:
        return FeignDecision.No("Troops may actually rout")
    
    // Good candidate
    return FeignDecision.Consider(
        formation: SelectFeigningFormation(context),
        retreatDistance: 100,  // meters
        trapForce: context.Our.Cavalry OR context.Our.Reserve
    )

function ExecuteFeignedRetreat(plan, context):
    // Step 1: Selected formation begins "retreat"
    plan.Formation.SetBehavior(ControlledRetreat)
    plan.Formation.SetMorale(Fake)  // Don't actually panic
    
    // Step 2: Wait for enemy to pursue
    When context.Enemy.Pursuing(plan.Formation):
        // Step 3: Enemy has taken the bait
        // Check if they're disordered
        if context.Enemy.PursuingFormation.Cohesion < 0.5:
            // SPRING THE TRAP
            plan.Formation.SetBehavior(AboutFace)  // Turn and fight
            plan.TrapForce.SetBehavior(FlankCharge)
            plan.TrapForce.SetTarget(context.Enemy.PursuingFormation)
            
            log("Feigned retreat successful - springing trap")
        else:
            // Enemy maintained discipline - abort
            plan.Formation.SetBehavior(RealRetreat)
            log("Enemy didn't take bait - retreating for real")
```

### 3. Commander Protection & Heroics
```
function ManageCommanderBehavior(context):
    commander = context.Our.Commander
    
    // Commander should generally stay safe but visible
    
    // SAFE: Position behind main line, visible to troops
    if context.Phase IN [Approach, Deployment]:
        commander.SetPosition(BehindMainLine, 50)  // 50m behind
        commander.SetBehavior(Commanding)
        return
    
    // HEROIC MOMENT: Battle at critical point, commander charges
    if context.BattleAtCriticalPoint AND context.Our.Morale < 40:
        // Commander leading from front can rally troops
        return CommanderDecision.LeadCharge(
            target: context.CriticalPoint,
            moraleBoost: 15,  // +15 morale to nearby troops
            risk: High
        )
    
    // PROTECT: Commander threatened, bodyguards respond
    if commander.UnderThreat:
        context.Our.Bodyguard.SetBehavior(ProtectCommander)
        context.Our.Bodyguard.SetTarget(commander.Threats)
        return CommanderDecision.Protect
    
    // DEFAULT: Stay safe, give orders
    return CommanderDecision.Command

function CommanderMoraleEffect(commander, context):
    // Nearby troops get morale bonus from visible commander
    for each agent in context.Our.Agents:
        distance = Distance(agent, commander)
        
        if distance < 30:
            agent.MoraleModifier += 10  // Strong bonus
        else if distance < 60:
            agent.MoraleModifier += 5   // Moderate bonus
        
        // If commander falls, HUGE morale penalty
    
    if commander.IsDown:
        for each agent in context.Our.Agents:
            agent.ChangeMorale(-20)
            if agent.Morale < 30:
                agent.SetBehavior(Panic)
```

### 4. Ammunition Awareness (Archers Running Dry)
```
function ManageArcherAmmunition(context):
    archers = context.Our.Archers
    
    avgAmmo = archers.Agents.Average(a => a.RemainingAmmoPercentage)
    
    // PLENTY: Fire freely
    if avgAmmo > 0.7:
        archers.SetFireMode(Normal)
        return
    
    // CONSERVING: Aimed shots only
    if avgAmmo > 0.3:
        archers.SetFireMode(Conserve)  // Only shoot at good targets
        log("Archers conserving ammunition - {} remaining".format(avgAmmo))
        return
    
    // LOW: Prepare for melee
    if avgAmmo > 0.1:
        // Last volleys - make them count
        archers.SetFireMode(FinalVolleys)
        archers.SetTarget(context.Enemy.HighValueTargets)
        
        // Start positioning for melee transition
        archers.SetSecondaryBehavior(PrepareForMelee)
        return
    
    // EMPTY: Become melee fighters
    if avgAmmo <= 0.1:
        archers.SetBehavior(TransitionToMelee)
        
        // Decision: Join infantry or become skirmishers?
        if archers.HasMeleeWeapons:
            archers.SetFormation(LooseSkirmish)
            archers.SetTarget(context.Enemy.Flanks)
        else:
            // No melee weapons - retreat to rear
            archers.SetBehavior(WithdrawToRear)
            log("Archers ammunition depleted - withdrawing")
```

### 5. Unit Rotation (Fresh Troops Forward)
```
function ConsiderUnitRotation(context):
    // Rotate battered front-line troops with healthier rear troops
    // Only during lulls, not mid-engagement
    // 
    // NOTE: Native has no "fatigue" stat. We use Health + Morale
    // as a proxy for "combat readiness" - troops with low HP and
    // low morale are effectively "spent" even without fatigue.
    
    if context.Phase NOT IN [Skirmish, Staging, Reformation]:
        return RotationDecision.No("Can't rotate mid-fight")
    
    mainLine = context.Our.MainInfantry
    reserve = context.Our.Reserve
    
    if reserve == null OR reserve.Count < 30:
        return RotationDecision.No("No reserve to rotate with")
    
    // Check if main line is battered (low health + low morale)
    // No fatigue system exists - we use health*morale as readiness proxy
    mainLineCondition = mainLine.AverageHealth * mainLine.AverageMorale / 100
    reserveCondition = reserve.AverageHealth * reserve.AverageMorale / 100
    
    if reserveCondition > mainLineCondition + 20:
        // Reserve is in significantly better shape
        return RotationDecision.Rotate(
            batteredFormation: mainLine,
            freshFormation: reserve,
            method: "Relief in place"
        )
    
    return RotationDecision.No("Conditions similar")

function ExecuteRotation(tired, fresh, context):
    // Fresh advances to line, tired falls back
    
    fresh.SetBehavior(AdvanceToLine)
    fresh.SetDestination(tired.Position)
    
    When fresh.AtPosition(tired.Position):
        tired.SetBehavior(WithdrawToRear)
        tired.SetRole(Reserve)
        fresh.SetRole(MainLine)
        
        log("Rotation complete - fresh troops on line")
```

### 6. Pursuit vs Mercy (When Enemy Routs)
```
function DecidePursuitPolicy(context):
    // When enemy breaks, do we chase them down or let them go?
    
    if NOT context.Enemy.Routing:
        return PursuitDecision.NotApplicable
    
    routingCount = context.Enemy.RoutingAgents.Count
    ourCavalry = context.Our.Cavalry
    
    // PURSUE: Eliminate them (decisive victory)
    if context.Our.WantsTotalVictory:
        if ourCavalry.Available:
            ourCavalry.SetBehavior(PursueAndDestroy)
            ourCavalry.SetTarget(context.Enemy.RoutingAgents)
            return PursuitDecision.FullPursuit
        else:
            // Infantry pursuit is slow but possible
            context.Our.FastestFormation.SetBehavior(Pursue)
            return PursuitDecision.LimitedPursuit
    
    // LET GO: Preserve strength (pragmatic)
    if context.Our.Casualties > 0.4:
        // We've lost too many - don't risk more
        return PursuitDecision.LetThemGo(
            reason: "Preserving remaining strength"
        )
    
    // CAPTURE: Take prisoners (political/economic value)
    if context.WantsPrisoners:
        ourCavalry.SetBehavior(RoundUp)  // Surround, don't kill
        return PursuitDecision.Capture
    
    // Default: Limited pursuit, then regroup
    return PursuitDecision.LimitedPursuit(
        duration: 60,  // Chase for 1 minute max
        thenRegroup: true
    )
```

### 7. False Advance (Testing Enemy)
```
function ConsiderFalseAdvance(context):
    // Advance aggressively, then pull back
    // Tests enemy reaction and discipline
    
    if context.Phase != Skirmish:
        return FalseAdvanceDecision.No("Wrong phase")
    
    // Only if we don't know how enemy will react
    if context.Enemy.BehaviorKnown:
        return FalseAdvanceDecision.No("Already know their pattern")
    
    testFormation = context.Our.MostExpendable  // Or cavalry
    
    return FalseAdvanceDecision.Test(
        formation: testFormation,
        advanceDistance: 50,  // Advance 50m
        retreatTrigger: EnemyResponse  // Pull back when they react
    )

function ExecuteFalseAdvance(plan, context):
    plan.Formation.SetBehavior(AggressiveAdvance)
    plan.Formation.SetMaxAdvance(plan.AdvanceDistance)
    
    // Watch enemy response
    enemyReaction = ObserveEnemyResponse(context)
    
    switch enemyReaction:
        case Charges:
            // They're aggressive - useful info
            plan.Formation.SetBehavior(ControlledRetreat)
            context.Enemy.BehaviorProfile = Aggressive
            break
            
        case HoldsPosition:
            // They're disciplined - adjust tactics
            plan.Formation.SetBehavior(ReturnToLine)
            context.Enemy.BehaviorProfile = Defensive
            break
            
        case Retreats:
            // They're nervous - we can push
            context.Enemy.BehaviorProfile = Timid
            // Maybe turn false advance into real one?
            break
```

### 8. Flanking Coordination (Multi-Formation Attack)
```
function CoordinateFlankingAttack(context):
    // Multiple formations attacking different angles simultaneously
    
    if context.Our.FormationCount < 3:
        return FlankDecision.No("Not enough formations")
    
    // Assign roles
    pinningForce = context.Our.MainInfantry
    flankingForce = context.Our.Cavalry OR context.Our.SecondaryInfantry
    reserveForce = context.Our.Reserve
    
    return FlankDecision.Coordinate(
        pin: AssignPinning(pinningForce, context.Enemy.MainInfantry),
        flank: AssignFlanking(flankingForce, context.Enemy.Flank),
        reserve: AssignExploitation(reserveForce)
    )

function ExecuteCoordinatedFlank(plan, context):
    // Phase 1: Pinning force advances and engages
    plan.Pin.Formation.SetBehavior(AdvanceAndEngage)
    plan.Pin.Formation.SetAggressionLevel(0.6)  // Engage but don't overcommit
    
    // Phase 2: Wait for enemy to commit to pinning force
    When context.Enemy.EngagedWith(plan.Pin.Formation):
        // Phase 3: Flanking force strikes
        plan.Flank.Formation.SetBehavior(FlankCharge)
        plan.Flank.Formation.SetTarget(context.Enemy.ExposedFlank)
        
        log("Flanking attack launched - enemy engaged frontally")
    
    // Phase 4: Reserve exploits success
    When plan.Flank.Formation.MakingProgress:
        plan.Reserve.Formation.SetBehavior(Exploit)
        plan.Reserve.Formation.SetTarget(context.Enemy.BreakingPoint)
```

### 9. Weather & Time Exploitation
```
function ExploitEnvironmentalConditions(context):
    // Use weather and time of day tactically
    
    weather = context.Weather
    timeOfDay = context.TimeOfDay
    
    // FOG: Close quickly, reduce archer effectiveness
    if weather.Fog:
        context.Our.Archers.SetEffectivenessModifier(0.5)
        return EnvironmentDecision.CloseFast(
            reason: "Fog reduces ranged - close to melee"
        )
    
    // RAIN: Muddy ground slows cavalry
    if weather.Rain:
        if context.Our.CavalryHeavy:
            // Disadvantage - avoid cavalry charges
            context.Our.Cavalry.SetBehavior(Dismount OR Reserve)
        if context.Enemy.CavalryHeavy:
            // Advantage - their cavalry is weakened
            return EnvironmentDecision.ExploitMud
    
    // SUNSET: Sun in enemy eyes?
    if timeOfDay.SunPosition == InEnemyEyes:
        context.Our.Archers.SetTarget(context.Enemy.WithSunInEyes)
        return EnvironmentDecision.ExploitSun
    
    // DUSK: Battle ending soon, force conclusion
    if timeOfDay.DuskApproaching:
        return EnvironmentDecision.ForceConclusion(
            reason: "Darkness approaching - press now or withdraw"
        )
```

### 10. Psychological Warfare
```
function ConsiderPsychologicalTactics(context):
    // Non-combat actions that affect enemy morale
    
    tactics = []
    
    // WAR CRY: Coordinated shout before charge
    if context.Phase == PreCharge:
        tactics.Add(WarCry(
            formation: context.Our.ChargingFormation,
            effect: EnemyMorale - 5,
            ownMorale: + 5
        ))
    
    // DISPLAY STRENGTH: Parade formations before battle
    if context.Phase == Deployment AND context.Our.Advantage:
        tactics.Add(DisplayStrength(
            formation: context.Our.LargestFormation,
            effect: EnemyMorale - 3
        ))
    
    // INTIMIDATING ADVANCE: Slow, deliberate, drums
    if context.Our.Disciplined AND context.Enemy.Nervous:
        tactics.Add(IntimidatingAdvance(
            speed: 0.5,  // Half normal speed
            drums: true,
            effect: EnemyMorale - 8 over time
        ))
    
    return tactics
```

### Summary: Orchestrator Opportunities
```
DRAMATIC MOMENTS:
┌────────────────────────────────────────────────────────────────┐
│ Reserve Commitment    │ The dramatic charge at the right moment│
│ Feigned Retreat       │ Classic bait-and-destroy trap          │
│ Commander Heroics     │ Lord leads charge at critical point    │
│ War Cry              │ Coordinated shout before impact         │
│ Pursuit Decision     │ Chase them down or show mercy?          │
└────────────────────────────────────────────────────────────────┘

TACTICAL DEPTH:
┌────────────────────────────────────────────────────────────────┐
│ Ammunition Awareness  │ Archers adapt as ammo depletes        │
│ Unit Rotation        │ Fresh troops replace battered ones     │
│ False Advance        │ Test enemy reactions before committing │
│ Flanking Coordination │ Multi-formation synchronized attack   │
│ Weather Exploitation │ Use fog, rain, sun position            │
└────────────────────────────────────────────────────────────────┘

REALISM:
┌────────────────────────────────────────────────────────────────┐
│ Commander Protection  │ Bodyguards, visibility, morale effects│
│ Psychological Warfare │ Intimidation, displays, war drums     │
│ Pursuit Policy       │ Total victory vs pragmatic preservation│
│ Environmental Factors│ Mud, fog, sunset timing                │
└────────────────────────────────────────────────────────────────┘
```

## 20.14 Modding Entry Points

### Hook: Custom Spawn Assignment
```csharp
// Override BattleSpawnModel
public class IntelligentBattleSpawnModel : BattleSpawnModel
{
    public override List<(IAgentOriginBase origin, int formationIndex)> 
        GetReinforcementAssignments(BattleSideEnum battleSide, List<IAgentOriginBase> troopOrigins)
    {
        // Get battle context
        var context = BattleOrchestrator.GetContext(battleSide);
        
        // Use intelligent assignment
        return AssignReinforcementsStrategically(troopOrigins, context);
    }
}
```

### Hook: Spawn Timing Control
```csharp
// Implement ICustomReinforcementSpawnTimer
public class IntelligentReinforcementTimer : ICustomReinforcementSpawnTimer
{
    public bool Check(BattleSideEnum side)
    {
        var context = BattleOrchestrator.GetContext(side);
        
        // Check if we should spawn
        var state = UpdateReinforcementState(context);
        
        return state == ReinforcementState.Rushing 
            || (state == ReinforcementState.Normal && BaseTimerExpired());
    }
    
    public void ResetTimer(BattleSideEnum side)
    {
        // Reset base timer
    }
}

// Register custom timer
spawnLogic.SetCustomReinforcementSpawnTimer(new IntelligentReinforcementTimer());
```

### Hook: Spawn Settings Override
```csharp
// Create custom spawn settings for intelligent battles
var intelligentSettings = new MissionSpawnSettings(
    InitialSpawnMethod.BattleSizeAllocating,
    ReinforcementTimingMethod.CustomTimer,  // Use our custom timer
    ReinforcementSpawnMethod.Wave,          // Wave-based for drama
    globalReinforcementInterval: 15f,       // Slower than default
    reinforcementWavePercentage: 0.2f,      // Bigger waves, less frequent
    defenderAdvantageFactor: 1.2f           // Slight defender advantage
);

spawnLogic.InitWithSinglePhase(..., in intelligentSettings);
```

### Hook: Formation Assignment Override
```csharp
// In MissionBehavior, intercept reinforcement spawning
public override void OnAgentCreated(Agent agent)
{
    if (IsReinforcement(agent))
    {
        // Check if we want to redirect this agent
        var betterFormation = FindBetterFormation(agent);
        
        if (betterFormation != agent.Formation)
        {
            agent.Formation = betterFormation;
        }
    }
}
```

---

## Part 20 Summary

### Applicability

| Battle Type | Reinforcement Intelligence |
|-------------|---------------------------|
| **Lord Party Battle** (50-200) | **N/A** - All troops on field from start |
| **Army Battle** (500-2000+) | **FULL** - Waves, timing, assignment, coordination |

### Native vs Intelligent (Army Battles Only)

| Native Behavior | Intelligent Behavior |
|-----------------|---------------------|
| Fixed 10-second timer | Context-aware timing |
| Spawn regardless of situation | Hold during retreat/disaster |
| Random formation assignment | Need-based assignment |
| No quality consideration | Distribute elites evenly |
| Ignore spawn point threats | Protect spawn, spawn advantage |
| No coordination | Coordinate with battle phases |

| When to Hold | When to Rush |
|--------------|--------------|
| All formations retreating | Critical formation breaking |
| Enemy camping spawn | Winning and pressing |
| Battle is lost | Supporting reserve commitment |
| Retreat imminent | Main line needs bodies |

| Reinforcement Strategy | When to Use |
|-----------------------|-------------|
| **Normal** | Standard battle flow |
| **Conservative** | Attrition battle, preserve force |
| **Aggressive** | Winning, press advantage |
| **Desperation** | Last reinforcements, all at once |

| Formation Need Scoring | Points |
|-----------------------|--------|
| Understrength | Up to 30 |
| Main infantry | +20 |
| Holding choke point | +25 |
| In melee combat | +15 |
| Low morale | +20 |
| Critical flank | +15 |
| Reserve | -10 |

---

# Part 21: Agent-Level Combat Director

While the Battle Orchestrator manages formations and strategy, the **Agent-Level Combat Director** creates dramatic moments with individual soldiers - champions facing off, heroic last stands, banner bearers rallying troops, small squad actions, and cinematic individual combat.

## 21.1 Purpose

**Create memorable individual soldier moments that enhance the spectacle without interfering with tactical AI.**

### What It Does:
- Identifies opportunities for dramatic agent-level moments
- Orchestrates champion duels between heroes/lords
- Creates last stand scenarios (wounded heroes, banner bearers)
- Directs small squad actions during lulls
- Manages banner bearer behavior and morale effects
- Coordinates heroic charges by small groups
- Creates cinematictension in officer combat

### What It Doesn't Do:
- Override formation AI (works within formation context)
- Cheat or break rules (just makes interesting choices)
- Force scripted sequences (opportunistic, not scripted)

---

## 21.2 Champion Duel System

When two heroes/lords/elite warriors are nearby, orchestrate a proper duel.

```csharp
class ChampionDuelDirector
{
    List<DuelPair> ActiveDuels = new();
    const float DuelEngagementRange = 15f;
    const float DuelProtectionRadius = 8f;
    
    void TickDuelDirector(Mission mission)
    {
        // Find potential champions
        var champions = FindChampions(mission);
        
        // Check for duel opportunities
        foreach (var champion in champions)
        {
            if (IsInActiveDuel(champion))
                continue;  // Already dueling
            
            // Find worthy opponent
            var opponent = FindWorthyOpponent(champion, champions);
            
            if (opponent != null && ShouldInitiateDuel(champion, opponent))
            {
                InitiateDuel(champion, opponent);
            }
        }
        
        // Manage active duels
        foreach (var duel in ActiveDuels.ToList())
        {
            ManageDuel(duel);
        }
    }
    
    List<Agent> FindChampions(Mission mission)
    {
        List<Agent> champions = new();
        
        foreach (Agent agent in mission.Agents)
        {
            if (!agent.IsActive() || !agent.IsHuman)
                continue;
            
            // Champions are:
            // - Lords/Heroes
            // - High tier troops (T6+)
            // - Banner bearers
            // - Sergeants/Captains
            
            if (agent.IsHero)
                champions.Add(agent);
            else if (agent.Character != null && agent.Character.Tier >= 6)
                champions.Add(agent);
            else if (agent.IsPlayerControlled && EnlistedRank >= 7)  // T7+ player
                champions.Add(agent);
            else if (IsBannerBearer(agent))
                champions.Add(agent);
        }
        
        return champions;
    }
    
    Agent FindWorthyOpponent(Agent champion, List<Agent> allChampions)
    {
        Agent bestOpponent = null;
        float bestScore = 0f;
        
        foreach (var candidate in allChampions)
        {
            if (candidate.Team == champion.Team)
                continue;  // Same team
            
            float distance = champion.Position.Distance(candidate.Position);
            if (distance > DuelEngagementRange)
                continue;  // Too far
            
            // Score opponent worthiness
            float score = ScoreOpponent(champion, candidate);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestOpponent = candidate;
            }
        }
        
        return bestOpponent;
    }
    
    float ScoreOpponent(Agent champion, Agent opponent)
    {
        float score = 1.0f;
        
        // Prefer similar tier (fair fight)
        int tierDiff = Math.Abs(champion.Character.Tier - opponent.Character.Tier);
        score *= (1.0f - tierDiff * 0.15f);
        
        // Prefer closer distance
        float distance = champion.Position.Distance(opponent.Position);
        score *= (1.0f - distance / DuelEngagementRange);
        
        // Strongly prefer if both are heroes
        if (champion.IsHero && opponent.IsHero)
            score *= 3.0f;
        
        // Prefer if they're both on horseback or both on foot
        if (champion.HasMount == opponent.HasMount)
            score *= 1.5f;
        
        // Prefer if area is relatively clear (not in middle of blob)
        if (IsClearArea(champion.Position, 10f))
            score *= 1.3f;
        
        return score;
    }
    
    void InitiateDuel(Agent champion, Agent opponent)
    {
        var duel = new DuelPair
        {
            ChampionA = champion,
            ChampionB = opponent,
            StartTime = Mission.Current.CurrentTime,
            DuelCenter = (champion.Position + opponent.Position) * 0.5f,
            ProtectionActive = true
        };
        
        ActiveDuels.Add(duel);
        
        // Slight AI adjustments for dramatic combat
        AdjustForDuel(champion);
        AdjustForDuel(opponent);
        
        // Signal nearby troops to give space (optional)
        RequestDuelSpace(duel);
    }
    
    void AdjustForDuel(Agent champion)
    {
        // Slightly reduce defensive AI (more aggressive, cinematic)
        var props = champion.AgentDrivenProperties;
        props.AIAttackOnDecideChance *= 1.15f;  // More aggressive
        props.AiDefendWithShieldDecisionChanceValue *= 0.9f;  // Less defensive
        champion.UpdateAgentProperties();
    }
    
    void RequestDuelSpace(DuelPair duel)
    {
        // Ask nearby friendly troops to not interfere
        // (They'll naturally engage other enemies, just not these champions)
        
        foreach (Agent agent in Mission.Current.Agents)
        {
            if (agent == duel.ChampionA || agent == duel.ChampionB)
                continue;
            
            float distToCenter = agent.Position.Distance(duel.DuelCenter);
            
            if (distToCenter < DuelProtectionRadius)
            {
                // Nearby troop - ask them to focus elsewhere
                if (agent.Team == duel.ChampionA.Team || agent.Team == duel.ChampionB.Team)
                {
                    // Find different target (not the dueling champion)
                    // (Let native AI handle targeting, just hint away from duel)
                }
            }
        }
    }
    
    void ManageDuel(DuelPair duel)
    {
        // Check if duel is still valid
        if (!duel.ChampionA.IsActive() || !duel.ChampionB.IsActive())
        {
            EndDuel(duel, won: duel.ChampionA.IsActive() ? duel.ChampionA : duel.ChampionB);
            return;
        }
        
        float distance = duel.ChampionA.Position.Distance(duel.ChampionB.Position);
        
        // Duel broken off? (too far apart, or interrupted)
        if (distance > DuelEngagementRange * 2f)
        {
            EndDuel(duel, won: null);  // Inconclusive
            return;
        }
        
        // Check for decisive moment
        if (duel.ChampionA.Health < duel.ChampionA.HealthLimit * 0.2f 
            || duel.ChampionB.Health < duel.ChampionB.HealthLimit * 0.2f)
        {
            // One champion near death - this is the climax
            duel.ClimaxPhase = true;
        }
    }
    
    void EndDuel(DuelPair duel, Agent won)
    {
        ActiveDuels.Remove(duel);
        
        if (won != null)
        {
            // Victor - slight morale boost to nearby allies
            ApplyVictorMoraleBonus(won);
            
            // Log the dramatic moment
            LogDuelVictory(won, duel);
        }
        
        // Restore normal AI properties
        RestoreNormalAI(duel.ChampionA);
        RestoreNormalAI(duel.ChampionB);
    }
    
    void ApplyVictorMoraleBonus(Agent victor)
    {
        // Small morale boost to nearby allies who witnessed the duel
        foreach (Agent agent in Mission.Current.Agents)
        {
            if (agent.Team != victor.Team)
                continue;
            
            float distance = agent.Position.Distance(victor.Position);
            if (distance < 20f)
            {
                agent.ChangeMorale(3f);  // Small boost
            }
        }
    }
}

class DuelPair
{
    public Agent ChampionA;
    public Agent ChampionB;
    public float StartTime;
    public Vec3 DuelCenter;
    public bool ProtectionActive;
    public bool ClimaxPhase;
}
```

---

## 21.3 Last Stand Scenarios

When a hero/important unit is wounded and surrounded, create a dramatic last stand moment.

```csharp
class LastStandDirector
{
    List<LastStandScenario> ActiveLastStands = new();
    
    void TickLastStands(Mission mission)
    {
        foreach (Agent agent in mission.Agents)
        {
            if (!agent.IsActive() || !IsImportantUnit(agent))
                continue;
            
            if (IsInLastStandSituation(agent) && !HasActiveLastStand(agent))
            {
                InitiateLastStand(agent);
            }
        }
        
        // Manage active last stands
        foreach (var lastStand in ActiveLastStands.ToList())
        {
            ManageLastStand(lastStand);
        }
    }
    
    bool IsInLastStandSituation(Agent agent)
    {
        // Last stand conditions:
        // - Health < 30%
        // - Surrounded by enemies (3+ nearby)
        // - Few/no allies nearby
        
        if (agent.Health > agent.HealthLimit * 0.3f)
            return false;
        
        int nearbyEnemies = CountNearbyEnemies(agent, 10f);
        int nearbyAllies = CountNearbyAllies(agent, 10f);
        
        return nearbyEnemies >= 3 && nearbyAllies <= 2;
    }
    
    void InitiateLastStand(Agent agent)
    {
        var lastStand = new LastStandScenario
        {
            Hero = agent,
            StartTime = Mission.Current.CurrentTime,
            StartHealth = agent.Health,
            LastStandPosition = agent.Position
        };
        
        ActiveLastStands.Add(lastStand);
        
        // AI adjustments for last stand
        // More defensive, desperate
        var props = agent.AgentDrivenProperties;
        props.AIBlockOnDecideAbility *= 1.2f;  // Better blocking
        props.AIAttackOnParryChance *= 1.3f;  // Counter-attack when possible
        props.AiDefendWithShieldDecisionChanceValue *= 1.5f;  // Use shield more
        agent.UpdateAgentProperties();
        
        // Try to back against something (wall, tree, rock)
        Vec3 defensivePosition = FindDefensivePosition(agent.Position);
        // (Optional: Hint movement toward defensive position)
    }
    
    void ManageLastStand(LastStandScenario lastStand)
    {
        if (!lastStand.Hero.IsActive())
        {
            EndLastStand(lastStand, survived: false);
            return;
        }
        
        // Check if rescued (allies arrived)
        int nearbyAllies = CountNearbyAllies(lastStand.Hero, 15f);
        int nearbyEnemies = CountNearbyEnemies(lastStand.Hero, 15f);
        
        if (nearbyAllies > nearbyEnemies)
        {
            // Rescued!
            EndLastStand(lastStand, survived: true);
            return;
        }
        
        // Still in last stand - track time survived
        float timeAlive = Mission.Current.CurrentTime - lastStand.StartTime;
        lastStand.Timesurvived = timeAlive;
        
        // Epic if survived > 30 seconds while wounded
        if (timeAlive > 30f)
            lastStand.IsEpic = true;
    }
    
    void EndLastStand(LastStandScenario lastStand, bool survived)
    {
        ActiveLastStands.Remove(lastStand);
        
        if (survived && lastStand.IsEpic)
        {
            // Epic survival - big morale boost
            foreach (Agent agent in Mission.Current.Agents)
            {
                if (agent.Team == lastStand.Hero.Team)
                {
                    float distance = agent.Position.Distance(lastStand.Hero.Position);
                    if (distance < 30f)
                        agent.ChangeMorale(5f);  // Inspired!
                }
            }
        }
        
        // Restore normal AI
        RestoreNormalAI(lastStand.Hero);
    }
}

class LastStandScenario
{
    public Agent Hero;
    public float StartTime;
    public float StartHealth;
    public Vec3 LastStandPosition;
    public float TimeSurvived;
    public bool IsEpic;
}
```

---

## 21.4 Banner Bearer Drama

Banner bearers are critical morale assets - create moments around them.

```csharp
class BannerBearerDirector
{
    void TickBannerBearers(Mission mission)
    {
        foreach (Agent agent in mission.Agents)
        {
            if (!agent.IsActive() || !IsBannerBearer(agent))
                continue;
            
            ManageBannerBearer(agent);
        }
    }
    
    void ManageBannerBearer(Agent bearer)
    {
        // Banner bearers should:
        // 1. Stay near formation center (not front line)
        // 2. Raise banner when formation is struggling
        // 3. Rally nearby troops
        // 4. Be protected by nearby troops
        
        Formation formation = bearer.Formation;
        if (formation == null)
            return;
        
        // Position: Stay in second or third rank
        int desiredRank = 2;  // Second rank (behind front line)
        int currentRank = bearer.FormationRankIndex;
        
        if (currentRank == 0)  // Front rank - too dangerous!
        {
            // Try to swap with someone behind
            SwapToRearRank(bearer, formation);
        }
        
        // Rally check: If formation morale is low, raise banner
        if (formation.QuerySystem.AverageMorale < 50f)
        {
            RaiseBannerToRally(bearer, formation);
        }
        
        // Protection: Nearby troops prioritize defending banner bearer
        ProtectBannerBearer(bearer);
    }
    
    void RaiseBannerToRally(Agent bearer, Formation formation)
    {
        // Banner raising provides morale boost
        // (Native system already handles this, we just orchestrate timing)
        
        foreach (Agent agent in formation.GetUnitsWithoutDetachedOnes())
        {
            float distance = agent.Position.Distance(bearer.Position);
            if (distance < 15f)
            {
                // Within banner range - morale boost
                agent.ChangeMorale(2f);
            }
        }
    }
    
    void ProtectBannerBearer(Agent bearer)
    {
        // Nearby allies should prioritize threats to banner bearer
        foreach (Agent agent in bearer.Formation.GetUnitsWithoutDetachedOnes())
        {
            if (agent == bearer)
                continue;
            
            float distance = agent.Position.Distance(bearer.Position);
            if (distance < 8f)
            {
                // Close to banner - check for threats
                Agent threatToBearer = FindThreatToBannerBearer(bearer);
                
                if (threatToBearer != null)
                {
                    // (Hint targeting toward this threat)
                    // Native AI will naturally engage nearby threats
                }
            }
        }
    }
    
    Agent FindThreatToBannerBearer(Agent bearer)
    {
        Agent closestThreat = null;
        float closestDist = float.MaxValue;
        
        foreach (Agent enemy in Mission.Current.Agents)
        {
            if (enemy.Team == bearer.Team || !enemy.IsActive())
                continue;
            
            float dist = enemy.Position.Distance(bearer.Position);
            
            if (dist < 10f && dist < closestDist)
            {
                closestDist = dist;
                closestThreat = enemy;
            }
        }
        
        return closestThreat;
    }
    
    bool IsBannerBearer(Agent agent)
    {
        // Check if agent is carrying a banner
        for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; 
             i < EquipmentIndex.NumAllWeaponSlots; i++)
        {
            MissionWeapon weapon = agent.Equipment[i];
            if (!weapon.IsEmpty && weapon.Item != null)
            {
                if (weapon.Item.ItemFlags.HasFlag(ItemFlags.CannotBePickedUp)  // Banners often have this
                    && weapon.Item.Type == ItemObject.ItemTypeEnum.Banner)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
```

---

## 21.5 Small Squad Actions

During battle lulls or skirmish phases, send small groups forward for dramatic individual combat.

```csharp
class SmallSquadDirector
{
    List<SquadAction> ActiveSquads = new();
    
    void TickSquadActions(BattleState state)
    {
        // Only during lull phases or skirmish
        if (state.Phase != BattlePhase.Lull && state.Phase != BattlePhase.Skirmish)
            return;
        
        // Check if we can send a squad forward
        if (CanSendSquad(state))
        {
            var squad = SelectSquad(state);
            if (squad != null)
                InitiateSquadAction(squad, state);
        }
        
        // Manage active squads
        foreach (var action in ActiveSquads.ToList())
        {
            ManageSquadAction(action, state);
        }
    }
    
    bool CanSendSquad(BattleState state)
    {
        // Conditions for squad action:
        // - Battle in lull (lines 30-80m apart, not engaged)
        // - We have quality troops available
        // - Not too many active squads already
        
        if (ActiveSquads.Count >= 2)
            return false;  // Max 2 squads at once
        
        float lineSeparation = state.AverageDistance;
        return lineSeparation > 30f && lineSeparation < 80f;
    }
    
    List<Agent> SelectSquad(BattleState state)
    {
        // Pick 3-6 good troops for squad action
        // Prefer: High tier, good morale, not wounded
        
        List<Agent> candidates = new();
        
        foreach (Formation formation in state.OurFormations)
        {
            foreach (Agent agent in formation.GetUnitsWithoutDetachedOnes())
            {
                if (agent.Character.Tier >= 4  // T4+ only
                    && agent.Health > agent.HealthLimit * 0.7f  // Not wounded
                    && agent.GetMorale() > 60f)  // Good morale
                {
                    candidates.Add(agent);
                }
            }
        }
        
        // Pick 3-6 agents
        int squadSize = MBRandom.RandomInt(3, 7);
        return candidates.OrderByDescending(a => a.Character.Tier)
                        .Take(squadSize)
                        .ToList();
    }
    
    void InitiateSquadAction(List<Agent> squad, BattleState state)
    {
        var action = new SquadAction
        {
            Squad = squad,
            StartTime = Mission.Current.CurrentTime,
            TargetPosition = CalculateSkirmishPosition(state),
            Purpose = SquadPurpose.Skirmish
        };
        
        ActiveSquads.Add(action);
        
        // Detach from formations temporarily
        foreach (Agent agent in squad)
        {
            agent.SetTeam(agent.Team, sync: false);  // Stay on same team
            // (Optionally: Set as detached to remove from formation temporarily)
        }
        
        // Move squad forward
        // (Native AI will handle movement, just set general direction)
    }
    
    Vec3 CalculateSkirmishPosition(BattleState state)
    {
        // Position between the lines
        Vec3 ourLine = state.OurCenter;
        Vec3 enemyLine = state.EnemyCenter;
        
        // 2/3 of the way toward enemy
        return ourLine + (enemyLine - ourLine) * 0.65f;
    }
    
    void ManageSquadAction(SquadAction action, BattleState state)
    {
        // Check if squad should return
        float timeOut = Mission.Current.CurrentTime - action.StartTime;
        
        if (timeOut > 45f  // Been out 45 seconds
            || state.Phase == BattlePhase.MainEngagement  // Main battle starting
            || SquadInDanger(action.Squad))  // Squad is overwhelmed
        {
            RecallSquad(action);
            return;
        }
    }
    
    void RecallSquad(SquadAction action)
    {
        ActiveSquads.Remove(action);
        
        // Reintegrate into formations
        foreach (Agent agent in action.Squad)
        {
            if (agent.IsActive())
            {
                // Return to original formation
                // (Native AI will handle rejoining)
            }
        }
    }
    
    bool SquadInDanger(List<Agent> squad)
    {
        // Check if squad is being overwhelmed
        int alive = squad.Count(a => a.IsActive());
        if (alive <= 1)
            return true;  // Only 1 left
        
        // Check if heavily outnumbered
        Vec3 squadCenter = CalculateSquadCenter(squad);
        int nearbyEnemies = CountEnemiesNear(squadCenter, 15f);
        
        return nearbyEnemies > alive * 3;  // 3:1 outnumbered
    }
}

class SquadAction
{
    public List<Agent> Squad;
    public float StartTime;
    public Vec3 TargetPosition;
    public SquadPurpose Purpose;
}

enum SquadPurpose
{
    Skirmish,       // Engage in middle ground
    Probe,          // Test enemy defenses
    Distraction     // Draw attention
}
```

---

## 21.6 Integration with Formation AI

The Agent Director works **alongside** Formation AI, not against it:

```csharp
class AgentLevelCombatDirector
{
    ChampionDuelDirector DuelDirector = new();
    LastStandDirector LastStandDirector = new();
    BannerBearerDirector BannerDirector = new();
    SmallSquadDirector SquadDirector = new();
    
    void Tick(Mission mission, BattleState state)
    {
        // Only operate when formations aren't in critical maneuvers
        if (state.Phase == BattlePhase.Deployment)
            return;  // Let formations deploy
        
        // Tick all directors
        DuelDirector.TickDuelDirector(mission);
        LastStandDirector.TickLastStands(mission);
        BannerDirector.TickBannerBearers(mission);
        
        // Squad actions only during lulls
        if (state.Phase == BattlePhase.Lull || state.Phase == BattlePhase.Skirmish)
            SquadDirector.TickSquadActions(state);
    }
}
```

---

## 21.7 Summary: Agent-Level Drama

| System | Purpose | When Active |
|--------|---------|-------------|
| **Champion Duels** | Hero vs hero combat | Throughout battle when champions nearby |
| **Last Stands** | Wounded hero surrounded | When important unit at <30% health, surrounded |
| **Banner Bearers** | Morale rallying | Throughout battle, especially when morale low |
| **Small Squads** | Individual combat moments | During lulls/skirmish phase (lines 30-80m apart) |

**Result:**
- **Formation AI** handles tactics and strategy
- **Agent Director** creates memorable individual moments
- Both systems work together for cinematic, tactical battles

---

## Related Documents

- **[agent-combat-ai.md](agent-combat-ai.md)** — Individual soldier combat AI: 40+ tunable properties for blocking, attacking, aiming, reactions, shield use

---

## Next Steps

Implementation plan will be created separately, covering:

### Quick Wins (Low Effort, High Impact)
1. **Context-Aware Agent Tuning** — Adjust blocking/attacking based on situation
2. **Smart Charge Decisions** — Don't charge shields/spears head-on
3. **Combat Rhythm Tuning** — Better counter-attack timing

### Medium Effort
4. **Reserve Management** — Hold and commit reserves intelligently
5. **Threat Assessment** — Target weak/isolated/damaged units
6. **Infantry Flanking** — Split when advantaged

### Full Orchestrator
7. **Battle State Tracking** — Trend analysis over time
8. **Coordinated Withdrawal** — Formation-level retreat
9. **Influence Mapping** — Spatial battlefield awareness

### Agent Combat AI ([agent-combat-ai.md](agent-combat-ai.md))
- Phase A: Custom AgentStatCalculateModel
- Phase B: Skill-based AI profiles (Recruit → Veteran → Elite)
- Phase C: Context-aware adjustments (defensive when outnumbered)
- Phase D: Fatigue/experience integration

### Player Counter-Intelligence (Part 7)
- Player Tracker (T7+ gate)
- Threat Projection (5-15 second prediction)
- Counter-Composition Reserves
- Flank Detection & Response
- Organized Withdrawal

### Dual Orchestrator Architecture (Part 8)
- One orchestrator per side
- Adversarial intelligence (both trying to win)
- Same code, different perspectives

### AI vs AI Battle Intelligence (Part 9)
- Battle phases (Form → Advance → Engage → Retreat)
- Situational cavalry aggression
- Formation discipline (no blobbing)
- Combined arms coordination
- Formation viability checks

### Tactical Decision Engine (Part 10)
- Multi-factor weighing (threat vs opportunity)
- Enemy formation assessment (armor, tier, skill)
- Archer targeting decisions
- Cavalry reserve timing
- Reserve commitment logic
- Pursuit decisions
- Future gating by bandit/troop count (optional)

### Agent Formation Behavior (Part 11)
- Formation cohesion levels (tight by default, context-aware)
- Formation-level casualty decisions (not individual)
- Fight to death vs tactical retreat
- Strategic positioning (fight near your spawn)
- Attacker vs defender roles
- Stalemate prevention (45-second timer)
- Player formation exception (T7+)
- Exploiting enemy disorder (with discipline)
- Post-victory decisions (regroup, defend, or advance)

### Intelligent Formation Organization (Part 12)
- Native API support (SwitchUnitLocations)
- Front-line score calculation (armor, tier, shield)
- Self-organizing ranks (elite to front)
- Flank spillover (when line stable, with leash)
- Gap filling (middle rank steps up)
- Safeguards (no chasing, instant recall, 25m max deviation)
- Positional combat behavior (front aggressive, rear defensive)

### Formation Doctrine System (Part 13)
- Two battle scales (party vs army)
- Formation count logic (2-3 for small, 5-6 for large)
- Formation doctrines (single line, three-wing, thin extended, hammer/anvil)
- Line depth decisions (thin+wide vs deep+narrow)
- Orchestrator formation reading (analyze enemy structure)
- Counter-formation tactics (punch through, envelop, exploit gaps)
- Reinforcement wave strategy (aggressive, efficient, grinding)
- Formation sizing via native APIs
- Troop distribution across formations (balanced quality, weighted by role)

### Battle Plan Generation (Part 14)
- Proactive battle plan generation (not just reactive)
- Plan types (Left Hook, Right Hook, Center Punch, Envelopment, Hammer/Anvil, Delay)
- Plan selection logic based on composition, terrain, numbers
- Main effort designation (concentrate best troops on attack axis)
- Formation objective assignment (attack, pin, screen, hold)
- Sequential objectives (cavalry: archers first, then rear attack)
- Cavalry tasking (specific high-value targets)
- Screening and refusing flanks
- Plan adaptation (detect failure, reinforce, shift, fallback)
- Plan execution state machine
- Plan commitment (90 second minimum, no flip-flopping)
- Enemy composition recognition (horse archers, pike wall, etc.)
- Defensive counter-formations (circle, square, terrain-anchored)
- Commitment timing (when to engage vs hold)
- Bleed rate monitoring (detect when defense is failing)
- Max casualties mode (if losing anyway, charge and take them with us)

### Unit Type Formations (Part 15)
- Native formation types (Line, Shield Wall, Loose, Square, Circle, Skein)
- Infantry formations (when to use each, switch triggers)
- Archer formations (positioning behind infantry, targeting)
- Cavalry formations (wedge charge, screening, pursuit)
- Formation positioning (combined arms spacing)
- Dynamic formation switching (cooldowns, priorities)
- Width and depth control
- Multi-formation coordination (echelon, refused flank)

### Terrain Exploitation (Part 16)
- Native terrain data (TacticalPosition, TacticalRegion)
- High ground strategy (seek pre-battle, hold during battle)
- Choke point exploitation (block with infantry, cavalry flank)
- Forest and difficult terrain (infantry vs cavalry, flank anchoring)
- Cliff tactics (back against OR push enemy toward)
- Terrain-aware battle plans (integrate with Part 14)
- Proactive terrain seeking (don't wait, actively race to terrain)

### Morale Exploitation (Part 17)
- Native morale system (per-agent, spread, panic)
- Reading enemy morale (routers, cohesion, movement)
- Triggering enemy routs (concentrate force, break one formation)
- High-value morale targets (wavering, main line, commander)
- Protecting own morale (pull back, reinforce, commander rally)
- Morale-aware targeting (archers/cavalry focus on wavering)
- Strategic withdrawal before collapse

### Coordinated Retreat (Part 18)
- Individual panic vs organized retreat
- Retreat decision logic (when to retreat)
- Covering force selection (cavalry best, sacrifice low value)
- Rearguard behavior (delay, don't commit)
- Step-by-step withdrawal (bounding overwatch)
- Force preservation priority (commander, companions, elites first)
- Rally points and regrouping

### Battle Pacing and Cinematics (Part 19)
- Cinematic battle phases (deployment → approach → skirmish → engage → exploit → resolution)
- Deliberate approach speed (slow march, not sprint)
- Skirmish phase (archers work before melee)
- Tension before contact (pause, war cries)
- Dramatic moments (cavalry charge timing, reserve commitment)
- Ebb and flow (lines push back and forth)
- Battle duration targets (3-10 minutes based on size)
- Morale-driven endings (routs, not grind to last man)
- Spectacle preservation (formations look like formations)

### Reinforcement Intelligence (Part 20)
- Native reinforcement system analysis
- Strategic wave timing (hold during retreat, rush when critical)
- Formation-aware assignment (need-based, not just understrength)
- Quality distribution (spread elites, prevent stacking)
- Spawn point tactics (protect spawn, spawn advantage)
- Reinforcement integration (morale boost, form-up pause)
- Wave coordination between sides
- Desperation waves (last stand mode)
- Modding entry points (BattleSpawnModel, custom timer)

### Implementation Priority

| Priority | Feature | Impact | Effort |
|----------|---------|--------|--------|
| 1 | Context-Aware Agent Tuning | High | Low |
| 2 | Smart Charge Decisions | High | Low |
| 3 | **Battle Phases** (no blobbing) | High | Low |
| 4 | **Formation Cohesion Enforcement** | High | Low |
| 5 | **Enemy Formation Assessment** | High | Medium |
| 6 | **Threat vs Opportunity Weighing** | High | Medium |
| 7 | Reserve Management | High | Medium |
| 8 | **Archer Targeting Decisions** | High | Medium |
| 9 | **Situational Cavalry Aggression** | High | Medium |
| 10 | **Cavalry Reserve Timing** | High | Medium |
| 11 | **Combined Arms Coordination** | High | Medium |
| 12 | **Attacker/Defender Role Logic** | High | Medium |
| 13 | **Formation Casualty Decisions** | High | Medium |
| 14 | **Player Tracker (T7+)** | High | Medium |
| 15 | **Flank Detection & Response** | High | Medium |
| 16 | **Counter-Composition Reserve** | High | Medium |
| 17 | **Reserve Commitment Logic** | High | Medium |
| 18 | **Stalemate Prevention** | Medium | Low |
| 19 | **Exploit Enemy Disorder** | High | Medium |
| 20 | **Post-Victory Regroup** | High | Medium |
| 21 | **Pursuit Decisions** | Medium | Medium |
| 22 | Infantry Flanking (AI offensive) | Medium | Medium |
| 23 | Coordination Signals | Medium | Medium |
| 24 | **Self-Organizing Ranks** (elite to front) | High | Medium |
| 25 | **Flank Spillover** (with safeguards) | Medium | Medium |
| 26 | **Positional Combat Behavior** | High | Low |
| 27 | **Dual Orchestrator Architecture** | High | High |
| 28 | **Full Tactical Decision Engine** | High | High |
| 29 | **Formation Count Logic** (Part 13) | High | Low |
| 30 | **Doctrine Selection** (Part 13) | High | Medium |
| 31 | **Line Depth Decisions** (Part 13) | High | Medium |
| 32 | **Formation Reading** (Part 13) | High | Medium |
| 33 | **Counter-Formation Tactics** (Part 13) | High | Medium |
| 34 | **Wave Strategy** (Part 13) | High | Medium |
| 35 | **Troop Distribution** (Part 13) | High | Medium |
| 36 | **Plan Types Library** (Part 14) | High | Medium |
| 37 | **Plan Selection Logic** (Part 14) | High | Medium |
| 38 | **Main Effort Designation** (Part 14) | High | Medium |
| 39 | **Formation Objective Assignment** (Part 14) | High | Medium |
| 40 | **Sequential Objectives** (Part 14) | High | Medium |
| 41 | **Cavalry Tasking** (Part 14) | High | Medium |
| 42 | **Plan Adaptation** (Part 14) | High | High |
| 43 | **Plan Execution State Machine** (Part 14) | High | High |
| 44 | **Plan Commitment / No Flip-Flop** (Part 14) | High | Low |
| 45 | **Enemy Composition Recognition** (Part 14) | High | Medium |
| 46 | **Defensive Counter-Formations** (Part 14) | High | Medium |
| 47 | **Commitment Timing** (Part 14) | High | Medium |
| 48 | **Bleed Rate Monitoring** (Part 14) | High | Low |
| 49 | **Max Casualties Mode** (Part 14) | High | Medium |
| 50 | **Infantry Formation Selection** (Part 15) | High | Medium |
| 51 | **Archer Positioning & Targeting** (Part 15) | High | Medium |
| 52 | **Cavalry Formation & Charge Logic** (Part 15) | High | Medium |
| 53 | **Combined Arms Positioning** (Part 15) | High | Medium |
| 54 | **Dynamic Formation Switching** (Part 15) | High | Medium |
| 55 | **Multi-Formation Coordination** (Part 15) | High | High |
| 56 | **High Ground Seeking** (Part 16) | High | Low |
| 57 | **Choke Point Exploitation** (Part 16) | High | Medium |
| 58 | **Forest/Terrain Tactics** (Part 16) | Medium | Medium |
| 59 | **Terrain-Aware Battle Plans** (Part 16) | High | Medium |
| 60 | **Proactive Terrain Race** (Part 16) | High | Medium |
| 61 | **Enemy Morale Reading** (Part 17) | High | Low |
| 62 | **Morale Cascade Targeting** (Part 17) | High | Medium |
| 63 | **Own Morale Protection** (Part 17) | High | Medium |
| 64 | **Morale-Aware Targeting** (Part 17) | High | Medium |
| 65 | **Strategic Withdrawal Trigger** (Part 17) | High | Medium |
| 66 | **Retreat Decision Logic** (Part 18) | High | Medium |
| 67 | **Rearguard Selection** (Part 18) | High | Medium |
| 68 | **Covering Force Behavior** (Part 18) | High | Medium |
| 69 | **Bounding Overwatch** (Part 18) | Medium | High |
| 70 | **Force Preservation Priority** (Part 18) | High | Low |
| 71 | **Rally Point Selection** (Part 18) | Medium | Low |
| 72 | **Commander Escape Logic** (Part 18) | High | Medium |
| 73 | **Cinematic Battle Phases** (Part 19) | High | Medium |
| 74 | **Deliberate Approach Speed** (Part 19) | High | Low |
| 75 | **Skirmish Phase Enforcement** (Part 19) | High | Medium |
| 76 | **Pre-Contact Tension** (Part 19) | Medium | Low |
| 77 | **Cavalry Charge Timing** (Part 19) | High | Medium |
| 78 | **Line Ebb and Flow** (Part 19) | Medium | Medium |
| 79 | **Battle Duration Enforcement** (Part 19) | High | Low |
| 80 | **Anti-Rush Mechanics** (Part 19) | High | Low |
| 81 | **Morale-Driven Rout** (Part 19) | High | Medium |
| 82 | **Formation Integrity Preservation** (Part 19) | High | Medium |
| 83 | **Strategic Wave Timing** (Part 20) | High | Medium |
| 84 | **Formation Need Scoring** (Part 20) | High | Medium |
| 85 | **Quality Distribution** (Part 20) | Medium | Medium |
| 86 | **Spawn Point Protection** (Part 20) | High | Low |
| 87 | **Reinforcement Morale Boost** (Part 20) | Medium | Low |
| 88 | **Wave Coordination** (Part 20) | Medium | Medium |
| 89 | **Desperation Wave Mode** (Part 20) | High | Low |
| 90 | **Custom BattleSpawnModel** (Part 20) | High | Medium |
