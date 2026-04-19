# Battle AI & Agent Combat: Master Implementation Spec

**Purpose:** Consolidated implementation specification for the Battle AI Upgrade and Agent Combat systems. This document serves as the master checklist for development, tracking all systems, their dependencies, entry points, and implementation status.

**Scope:** Field battles only. Siege/naval AI is out of scope.

**Core Principle:** Make AI smarter through better coordination and decision-making, not through cheating.

**Key Features:**
- **Dynamic Battle Scaling:** AI adapts complexity based on actual troop counts (Skirmish to Massive battles)
- **Player Configurability:** Handles player battle size settings from 200 to 1000+ troops
- **Three-Layer Architecture:** Orchestrator → Formation → Agent for coordinated intelligence
- **Context-Aware Systems:** All decisions informed by battle phase, objectives, and scale

---

## Table of Contents

0. [Build Configuration Prerequisites](#0-build-configuration-prerequisites)
1. [Architecture Overview](#1-architecture-overview)
2. [Implementation Phases](#2-implementation-phases)
   - 2b. [Edge Cases By Phase](#2b-edge-cases-by-phase)
3. [System Specifications](#3-system-specifications)
4. [API Reference](#4-api-reference)
5. [Configuration & Tuning](#5-configuration--tuning)
6. [Testing & Validation](#6-testing--validation)

---

# 0. Build Configuration Prerequisites

## 0.1 Optional SubModule Architecture

Battle AI is implemented as an **optional SubModule** that users can disable in the Bannerlord launcher. The project uses a single build configuration that always compiles Battle AI code.

**Key Points:**
- Single build: `Enlisted RETAIL` configuration
- Battle AI always compiled (BATTLE_AI constant always defined)
- Battle AI runs as separate SubModule users can toggle
- No performance cost when disabled (SubModule never initializes)

**See [BLUEPRINT.md - Build & Deployment](../../BLUEPRINT.md#build--deployment) for complete documentation.**

## 0.2 Quick Setup Checklist

Before implementing Battle AI, verify:

1. ✅ **Build configuration exists in .csproj:**
   - `Enlisted RETAIL` (outputs to `Modules\Enlisted\`, defines `BATTLE_AI` constant)

2. ✅ **SubModule.xml has two SubModule entries:**
   - `Enlisted Core` (required, always enabled)
   - `Enlisted Battle AI` (optional, users can disable in launcher)

3. ✅ **Build command works:**
   ```powershell
   cd C:\Dev\Enlisted\Enlisted; dotnet build -c "Enlisted RETAIL" /p:Platform=x64
   ```

4. ✅ **BattleAISubModule.cs exists:**
   - `src/Features/Combat/BattleAI/BattleAISubModule.cs`

## 0.3 Battle AI Code Structure

All Battle AI code must:

1. **Use conditional compilation:** Wrap entire files in `#if BATTLE_AI ... #endif`
2. **Live in dedicated folder:** `src/Features/Combat/BattleAI/`
3. **Be added to .csproj:** Add `<Compile Include="..."/>` entries for all new files
4. **Follow folder structure:**
   ```
   src/Features/Combat/BattleAI/
   ├── Behaviors/
   │   └── EnlistedBattleAIBehavior.cs      # Mission behavior entry point
   ├── Orchestration/
   │   ├── BattleOrchestrator.cs            # Strategic coordinator
   │   ├── BattleContext.cs                 # Battle state
   │   └── TacticalAnalyzer.cs              # Threat assessment
   ├── Formation/
   │   ├── FormationController.cs           # Formation AI
   │   └── FormationRoleAssigner.cs         # Role logic
   └── Agent/
       ├── AgentCombatEnhancer.cs           # Agent-level AI
       └── TargetingLogic.cs                # Smart targeting
   ```

## 0.4 File Template

**Every Battle AI file must use this template:**

```csharp
#if BATTLE_AI
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Combat.BattleAI.Orchestration
{
    /// <summary>
    /// Battle AI component - only included in Enhanced version.
    /// [Description of what this class does]
    /// </summary>
    public class MyBattleAIClass
    {
        // Implementation here
        
        // Use ModLogger with "BattleAI" category
        private void LogSomething()
        {
            ModLogger.Debug("BattleAI", "Your message here");
        }
    }
}
#endif
```

## 0.5 Keeping Battle AI Toggle-able

**⚠️ CRITICAL RULE: Complete Separation from Core SubModule**

To ensure users can disable Battle AI via launcher checkbox:

**NEVER do these in Core SubModule (`Enlisted.Mod.Entry.SubModule`):**
- ❌ Initialize Battle AI systems
- ❌ Register Battle AI mission behaviors
- ❌ Call any Battle AI code
- ❌ Reference Battle AI classes

**ALWAYS do these in BattleAISubModule (`Enlisted.Features.Combat.BattleAI.BattleAISubModule`):**
- ✅ All Battle AI initialization
- ✅ All mission behavior registration
- ✅ All Battle AI code execution

**Why this matters:** If BattleAISubModule is disabled in launcher, it never loads. If core SubModule references Battle AI, it will try to load disabled code and cause errors OR prevent users from disabling it.

## 0.6 Activation Gate

**Battle AI only activates when:**

1. ✅ User has enabled "Enlisted Battle AI" SubModule in Bannerlord launcher
2. ✅ Player is enlisted (`EnlistmentState.IsEnlisted`)
3. ✅ In a field battle (not siege/naval)
4. ✅ Mission has initialized properly

**Implementation in BattleAISubModule:**

```csharp
#if BATTLE_AI
using TaleWorlds.MountAndBlade;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Combat.BattleAI
{
    public class BattleAISubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLogger.Info("BattleAI", "Battle AI SubModule loaded");
        }
        
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            // Register mission behavior callbacks here
        }
        
        private void RegisterBattleAIBehaviors(Mission mission)
        {
            // Only field battles
            if (mission.Scene.GetName().Contains("siege") || 
                mission.Scene.GetName().Contains("naval"))
            {
                return;
            }
            
            // Only when player is enlisted
            if (!EnlistmentState.IsEnlisted)
            {
                return;
            }
            
            // Add Battle AI behaviors
            mission.AddMissionBehavior(new EnlistedBattleAIBehavior());
            ModLogger.Debug("BattleAI", "Battle AI activated for mission");
        }
    }
}
#endif
```

## 0.7 SubModule Structure

**SubModule.xml Configuration:**
```xml
<Module>
    <Name value="Enlisted"/>
    <Id value="Enlisted"/>
    <SubModules>
        <!-- Core SubModule (Required) -->
        <SubModule>
            <Name value="Enlisted Core"/>
            <DLLName value="Enlisted.dll"/>
            <SubModuleClassType value="Enlisted.Mod.Entry.SubModule"/>
        </SubModule>
        
        <!-- Battle AI SubModule (Optional) -->
        <SubModule>
            <Name value="Enlisted Battle AI"/>
            <DLLName value="Enlisted.dll"/>
            <SubModuleClassType value="Enlisted.Features.Combat.BattleAI.BattleAISubModule"/>
        </SubModule>
    </SubModules>
</Module>
```

Users see two checkboxes in Bannerlord launcher:
- ☑️ **Enlisted Core** (keep enabled)
- ☑️ **Enlisted Battle AI** (optional)

## 0.8 Adding Battle AI Files to .csproj

When creating new Battle AI files, add them to `Enlisted.csproj`:

```xml
<ItemGroup>
  <!-- Battle AI SubModule and implementation files -->
  <Compile Include="src\Features\Combat\BattleAI\BattleAISubModule.cs"/>
  <Compile Include="src\Features\Combat\BattleAI\Behaviors\EnlistedBattleAIBehavior.cs"/>
  <Compile Include="src\Features\Combat\BattleAI\Orchestration\BattleOrchestrator.cs"/>
  <Compile Include="src\Features\Combat\BattleAI\Orchestration\BattleContext.cs"/>
  <Compile Include="src\Features\Combat\BattleAI\Orchestration\TacticalAnalyzer.cs"/>
  <!-- Add more as created -->
</ItemGroup>
```

**Remember:** All files use `#if BATTLE_AI` internally. BATTLE_AI is always defined in the build.

---

# 1. Architecture Overview

## 1.1 Layer Hierarchy

```
┌─────────────────────────────────────────────────────────────────────┐
│  BATTLE ORCHESTRATOR (New - Per Team)                               │
│  Coordinates all formations, manages reserves, executes battle plans│
│  Decision Interval: 1-2 seconds                                     │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────────┐
│  TeamAIComponent (Native - Strategy Layer)                          │
│  Selects TacticComponent every ~5 seconds                           │
│  Enhanced via behavior weight nudges from Orchestrator              │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────────┐
│  FormationAI (Native - Behavior Layer)                              │
│  Selects BehaviorComponent per tick                                 │
│  Enhanced via context-aware behavior weights                        │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────────┐
│  HumanAIComponent / AgentDrivenProperties (Native - Agent Layer)    │
│  Individual combat decisions, reactions, targeting                   │
│  Enhanced via context-aware property tuning                          │
└─────────────────────────────────────────────────────────────────────┘
```

## 1.2 Dual Orchestrator Model

Both sides in battle receive independent orchestrators running identical logic. This creates adversarial intelligence where both sides actively try to win.

```
┌─────────────────────┐         ┌─────────────────────┐
│  PLAYER TEAM        │   VS    │  ENEMY TEAM         │
│  ORCHESTRATOR       │         │  ORCHESTRATOR       │
│                     │◄───────►│                     │
│  Same code          │ Observe │  Same code          │
│  Different side     │ React   │  Different side     │
└─────────────────────┘         └─────────────────────┘
```

## 1.3 Three-Layer Superiority

**Why This Architecture Works:**

Our three-layer architecture (Orchestrator → Formation → Agent) provides superior AI because each layer operates at the appropriate scope:

1. **Orchestrator Layer (Strategic):** Sees the entire battlefield, coordinates all formations, executes battle plans, manages reserves, and adapts to changing conditions. This prevents the common AI problem of formations making locally optimal but globally suboptimal decisions.

2. **Formation Layer (Tactical):** Each formation knows its role in the battle plan (main effort, supporting, screen, reserve) and makes tactical decisions (when to charge, when to reform, target selection) that support the overall strategy while responding to immediate threats.

3. **Agent Layer (Micro):** Individual soldiers make small tactical adjustments (flanking steps, supporting allies, seeking advantageous positions) within formation bounds, creating realistic combat behavior without breaking formation cohesion.

**Key Innovation:** Unlike approaches that operate at a single layer (either pure strategic control OR pure agent autonomy), our layered approach ensures strategic coherence while allowing realistic tactical and individual behavior. The orchestrator provides context to formations, formations provide context to agents, and each layer respects the constraints of the layers above it.

## 1.4 Activation Gate: Enlisted Only

**CRITICAL:** The Battle AI system ONLY runs when the player is actively enlisted in the mod. If the player is not enlisted (playing as a regular lord, before starting enlisted career, or after discharge), the system is completely disabled and native AI runs unmodified.

### 1.4.1 Activation Check
```csharp
bool ShouldActivateBattleAI()
{
    // Only run if player is currently enlisted
    if (!EnlistmentState.IsEnlisted)
        return false;
    
    // Only run in field battles (not siege/naval)
    if (!IsFieldBattle())
        return false;
    
    // Don't run in multiplayer
    if (Mission.Current?.IsMultiplayer == true)
        return false;
    
    return true;
}
```

### 1.4.2 Enlisted State Check Points
| Check Point | Action if NOT Enlisted |
|-------------|------------------------|
| `OnMissionBehaviorInitialize` | Skip adding `BattleOrchestratorBehavior` |
| `OnBattleStarted` | Return early, no orchestrator creation |
| Every Orchestrator tick | Early exit if enlisted state changed mid-battle |

### 1.4.3 Player Rank Tiers (When Enlisted)

| Rank | Player Role | AI Behavior |
|------|-------------|-------------|
| **T1-T6** | Soldier in NPC formation | Standard AI, player is just another soldier |
| **T7-T9** | Commander with own troops | Player Counter-AI activates |

### 1.4.4 Why This Matters
- Players may start a campaign, play as a regular lord, then enlist later
- Players may be discharged and return to regular gameplay
- The Battle AI should NOT affect normal Bannerlord gameplay outside the enlisted experience
- This ensures mod compatibility and allows players to opt-in/opt-out

---

# 2. Implementation Phases

## Phase 1: Foundation (Priority: Critical)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 1.1 | **Enlisted Activation Gate** | Check `EnlistmentState.IsEnlisted` before any AI runs | ⬜ TODO | Low |
| 1.2 | MissionBehavior Entry | `BattleOrchestratorBehavior` hook into mission (if enlisted) | ⬜ TODO | Low |
| 1.3 | Basic Orchestrator Shell | Per-team orchestrator instantiation | ⬜ TODO | Low |
| 1.4 | Battle State Model | Read-only signals from TeamQuerySystem/FormationQuerySystem | ⬜ TODO | Medium |
| 1.5 | Agent Stat Model Override | `EnlistedAgentStatCalculateModel` registration (if enlisted) | ⬜ TODO | Low |
| 1.6 | Configuration Loading | Tunable values from JSON config | ⬜ TODO | Low |
| 1.7 | **Activity Detection Utilities** | `IsFormationShooting`, `FormationFightingInMelee`, activity metrics | ⬜ TODO | Low |
| 1.8 | **Battle Joined Detection (Hysteresis)** | `HasBattleBeenJoined` with +5m buffer to prevent flip-flop | ⬜ TODO | Low |
| 1.9 | **Formation State Tracking** | Track Idle/Moving/Shooting/InMelee/Retreating per formation | ⬜ TODO | Medium |
| 1.10 | **Battle Scale Detection** | Detect Skirmish/Small/Medium/Large/Massive based on troop counts, scale AI systems dynamically | ⬜ TODO | Medium |

## Phase 2: Agent Combat Enhancements (Priority: High)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 2.1 | **Skill-Based Property Tuning** | AIBlock = aiLevel * 2f, AIParry = aiLevel * 2.2f, formulas for all properties | ⬜ TODO | Medium |
| 2.2 | Coordination Signals | Reduce mob behavior (don't pile on one target) | ⬜ TODO | Medium |
| 2.3 | **Ranged Weapon Differentiation** | Bow: 0.003f error, Crossbow: 0.001f error, skill-scaled accuracy | ⬜ TODO | Medium |
| 2.4 | **Objective-Aware Combat Modifiers** | Attack: +20% aggro, Hold: -30% aggro +20% block, adapt to objective | ⬜ TODO | Medium |
| 2.5 | **Battle Phase Combat Modifiers** | Crisis: +15% aggro, Rout: -40% all, Pursuit: +30% aggro | ⬜ TODO | Low |

## Phase 3: Formation Intelligence (Priority: High)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 3.1 | Smart Charge Decisions | Patch BehaviorTacticalCharge to assess targets | ⬜ TODO | Medium |
| 3.2 | Infantry Flanking | Split infantry for envelopment when 1.5x advantage | ⬜ TODO | Medium |
| 3.3 | Threat Assessment Targeting | Score enemies before engaging | ⬜ TODO | Medium |
| 3.4 | **Formation Arrangement Intelligence** | Line/Loose/Shield/Square, match enemy, suppression response, objective-aware | ⬜ TODO | Medium |
| 3.5 | Pike/Spear Weapon Discipline | Keep polearms out at proper range | ⬜ TODO | High |
| 3.6 | Multi-Weapon Infantry | Throwing → Melee Polearm → Sidearm progression | ⬜ TODO | High |
| 3.7 | Self-Organizing Ranks | Heavy/elite to front, light to rear | ⬜ TODO | Medium |
| 3.8 | **Multi-Tier Position Validation** | Try position → fallback → search nearby → tactical scoring, notify orchestrator | ⬜ TODO | High |
| 3.9 | **Tactical Position Scoring** | Height, cover, spacing, approach quality, score 0-2.0, pick best | ⬜ TODO | High |
| 3.10 | **Suppression Detection & Response** | UnderRangedAttackRatio > 0.2 → loose, reposition, notify orchestrator | ⬜ TODO | Medium |
| 3.11 | **Infantry Cavalry Threat Detection** | Detect charging cavalry, stop, face, form square/shield/loose, request support | ⬜ TODO | High |
| 3.12 | **Plan-Aware Flank Protection** | Position based on battle plan, intercept threats to main effort | ⬜ TODO | High |

## Phase 4: Battle Orchestrator Core (Priority: High)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 4.1 | OODA Decision Loop | Observe → Orient → Decide → Act (1-2 sec) | ⬜ TODO | Medium |
| 4.2 | Strategy Modes | EngageBalanced, DelayDefend, Exploit, Withdraw | ⬜ TODO | Medium |
| 4.3 | Formation Roles | MainLine, Screen, FlankGuard, Reserve assignment | ⬜ TODO | Medium |
| 4.4 | Battle Phases | Forming → Advancing → Engaged → Retreating | ⬜ TODO | Medium |
| 4.5 | Trend Analysis | Track power ratio over time, detect "losing for N seconds" | ⬜ TODO | Medium |

## Phase 5: Tactical Decision Engine (Priority: High)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 5.1 | Threat vs Opportunity Weighing | Multi-factor action scoring | ⬜ TODO | High |
| 5.2 | **Archer Effectiveness Awareness** | LOS raycast, hit detection, micro-adjust if shooting but not hitting, hold when effective | ⬜ TODO | High |
| 5.3 | **Plan-Based Cavalry Deployment** | Hammer&Anvil: wait for infantry engaged, Hook: 30-50m from enemy, Delay: only if threatened | ⬜ TODO | High |
| 5.4 | **Enhanced Cavalry Cycling (9 States)** | Reserve → Positioning → Charging → ChargingPast → Impact → Melee → Disengaging → Rallying → Reforming → Bracing | ⬜ TODO | Very High |
| 5.4a | **Formation Integrity Gates** | Deviation < 5f to charge, < 12f to advance, > 25f trigger rally, width matching | ⬜ TODO | High |
| 5.4b | **Adaptive Cavalry Timers** | Charge: 15s base (adaptive), Melee: 5s (+3 if winning, -2 if losing), Reform: 12s (*0.75 if suppressed) | ⬜ TODO | Medium |
| 5.4c | **Intelligent Target Selection** | Plan-aware scoring: class, distance, vulnerability, threat to team, approach angle | ⬜ TODO | High |
| 5.5 | Reserve Commitment Logic | Flank collapse, main line support | ⬜ TODO | High |
| 5.6 | Pursuit Decisions | Full pursuit, cavalry only, hold, regroup | ⬜ TODO | Medium |

## Phase 6: Battle Plan Generation (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 6.1 | Plan Types | Left Hook, Right Hook, Center Punch, Double Envelopment, Hammer Anvil, Delay | ⬜ TODO | High |
| 6.2 | Plan Selection | Score plans by composition, terrain, power ratio | ⬜ TODO | High |
| 6.3 | Main Effort Designation | Concentrate best troops on primary axis | ⬜ TODO | Medium |
| 6.4 | Formation Objectives | Attack, Pin, Screen, Breakthrough, Flank, Hold | ⬜ TODO | Medium |
| 6.5 | Sequential Objectives | Phase-based cavalry tasking | ⬜ TODO | High |
| 6.6 | Plan Adaptation | Detect success/failure, shift effort | ⬜ TODO | High |

## Phase 7: Reserve & Retreat Management (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 7.1 | Reserve Manager | Hold reserves, commit on triggers | ⬜ TODO | Medium |
| 7.2 | Casualty-Based Posture | Engaged → FightingRetreat → TacticalWithdrawal → LastStand | ⬜ TODO | Medium |
| 7.3 | Organized Withdrawal | Covering force, step-by-step fallback | ⬜ TODO | High |
| 7.4 | Rally Points & Regrouping | Post-retreat reformation | ⬜ TODO | Medium |
| 7.5 | Stalemate Prevention | 45-second timer forces action | ⬜ TODO | Low |

## Phase 8: Player Counter-Intelligence (T7+) (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 8.1 | Player Formation Tracking | Position, velocity, threat angle | ⬜ TODO | Medium |
| 8.2 | Threat Projection | Predict player position 10-15 seconds ahead | ⬜ TODO | Medium |
| 8.3 | Flank Detection | Multiple signals for flanking threat | ⬜ TODO | Medium |
| 8.4 | Counter-Composition Reserve | Hold counters for player's composition | ⬜ TODO | High |
| 8.5 | Flank Response Tree | Intercept, delay, refuse flank, compact | ⬜ TODO | High |

## Phase 9: Reinforcement Intelligence (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 9.1 | Strategic Wave Timing | Hold/rush reinforcements based on context | ⬜ TODO | High |
| 9.2 | Formation-Aware Assignment | Route to formations by tactical need | ⬜ TODO | Medium |
| 9.3 | Quality Distribution | Spread elites/veterans/regulars | ⬜ TODO | Medium |
| 9.4 | Spawn Point Tactics | Fight near your spawn for advantage | ⬜ TODO | Medium |
| 9.5 | Reinforcement Integration | Form up, morale boost, cohesion | ⬜ TODO | Medium |
| 9.6 | Cinematic Staging | Staging formations, skirmish phase | ⬜ TODO | High |

## Phase 10: Battle Pacing & Cinematics (Priority: Low)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 10.1 | Deliberate Approach Speed | Form up before blobbing forward | ⬜ TODO | Low |
| 10.2 | Skirmish Phase | Probing attacks before full engagement | ⬜ TODO | Medium |
| 10.3 | Dramatic Moments | Champion duels, last stands, banner drama | ⬜ TODO | High |
| 10.4 | Morale-Driven Endings | Rout cascades, ordered withdrawal | ⬜ TODO | Medium |

## Phase 11: Terrain & Morale Exploitation (Priority: Low)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 11.1 | High Ground Strategy | Seek and hold elevation | ⬜ TODO | Medium |
| 11.2 | Choke Point Exploitation | Funnel enemies through narrow terrain | ⬜ TODO | Medium |
| 11.3 | Forest/Difficult Terrain | Use terrain to negate cavalry | ⬜ TODO | Medium |
| 11.4 | Terrain-Aware Battle Plans | Select plans based on available terrain | ⬜ TODO | High |
| 11.5 | Morale Reading | Target low-morale formations | ⬜ TODO | Low |
| 11.6 | Rout Triggering | Focus fire to break enemy morale | ⬜ TODO | Medium |
| 11.7 | Protecting Own Morale | Shield fragile formations | ⬜ TODO | Medium |
| 11.8 | Strategic Withdrawal Before Collapse | Disengage before rout | ⬜ TODO | Medium |

## Phase 12: Formation Doctrine System (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 12.1 | Formation Count by Scale | Use Phase 1.10 battle scale to determine formation count (Skirmish: 1-2, Medium: 3-4, Massive: 5-8) | ⬜ TODO | Low |
| 12.2 | Formation Depth by Scale | Use battle scale to determine max ranks (Skirmish: 1-2, Medium: 2-4, Massive: 4-6) | ⬜ TODO | Low |
| 12.3 | Formation Doctrines | Single Deep Line, Three-Wing, Extended Thin, Hammer-Anvil | ⬜ TODO | High |
| 12.4 | Counter-Formation Tactics | Detect enemy doctrine, select counter | ⬜ TODO | High |
| 12.5 | Troop Distribution | Spread elites, quality balance across formations | ⬜ TODO | Medium |
| 12.6 | Spawn Point Strategy | Fight near your spawn for reinforcement advantage | ⬜ TODO | Medium |

## Phase 13: Unit Type Formations (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 13.1 | Infantry Formation Shapes | Line, Shield Wall, Loose, Square, Circle | ⬜ TODO | Medium |
| 13.2 | Archer Formation Shapes | Line, Loose, Scatter, Staggered behind infantry | ⬜ TODO | Medium |
| 13.3 | Cavalry Formation Shapes | Line, Skein/Wedge, Loose | ⬜ TODO | Medium |
| 13.4 | Formation Positioning | Combined arms: archers behind, cavalry on flanks | ⬜ TODO | Medium |
| 13.5 | Dynamic Formation Switching | Auto-switch based on threat (square vs cavalry) | ⬜ TODO | High |
| 13.6 | Width and Depth Control | Adjust frontage to match/exceed enemy | ⬜ TODO | Medium |
| 13.7 | Formation Facing/Movement | Maintain facing during advance | ⬜ TODO | Low |
| 13.8 | Multi-Formation Coordination | All formations move as coherent army | ⬜ TODO | High |

## Phase 14: Intelligent Formation Organization (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 14.1 | Front-Line Scoring | Calculate suitability for front rank (armor, shield, tier) | ⬜ TODO | Medium |
| 14.2 | Flank Spillover | Rear troops work flanks when line stable | ⬜ TODO | Medium |
| 14.3 | Gap Filling Logic | Middle ranks step up when front-line falls | ⬜ TODO | Medium |
| 14.4 | Formation Safeguards | Max deviation distance, no chase, instant recall | ⬜ TODO | Medium |
| 14.5 | Positional Combat Behavior | Front aggressive, rear defensive/ranged | ⬜ TODO | Medium |

## Phase 15: Plan Execution & Anti-Flip-Flop (Priority: High)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 15.1 | Plan Execution State Machine | Forming → Advancing → Engaging → Exploiting → Pursuing | ⬜ TODO | High |
| 15.2 | Anti-Flip-Flop Rules | 30s minimum commitment, 20s cooldown, max 2 changes/60s | ⬜ TODO | High |
| 15.3 | Phase-Based Gating | Allow changes at natural phase transitions | ⬜ TODO | Medium |
| 15.4 | Legitimate Change Detection | Catastrophic failure, enemy major shift, new opportunity | ⬜ TODO | High |
| 15.5 | Enemy Composition Recognition | Infantry Horde, Archer Heavy, Cavalry Heavy, Horse Archers, etc. | ⬜ TODO | High |
| 15.6 | Defensive Counter-Formations | Auto-select counters based on enemy composition | ⬜ TODO | High |
| 15.7 | Commitment Timing | When to engage vs when to delay | ⬜ TODO | Medium |

## Phase 16: Agent-Level Combat Director (Priority: Low)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 16.1 | Champion Duel System | Hero vs hero dramatic fights | ⬜ TODO | High |
| 16.2 | Last Stand Scenarios | Dramatic final moments for surrounded units | ⬜ TODO | Medium |
| 16.3 | Banner Bearer Drama | Protect/capture banner events | ⬜ TODO | Medium |
| 16.4 | Small Squad Actions | 2-5 man coordinated assaults | ⬜ TODO | Medium |
| 16.5 | Integration with Formation AI | Agent drama respects formation orders | ⬜ TODO | Medium |
| 16.6 | **Bounded Agent Autonomy** | Infantry in melee: Attack/BackStep/FindAlly/Flank decisions, max 5m deviation | ⬜ TODO | High |
| 16.6a | **Agent Situation Sampling** | Sample nearby allies/enemies (10m radius), count/position/status detection | ⬜ TODO | Medium |
| 16.6b | **Micro-Decision Evaluation** | Utility scoring for Attack/BackStep/FindAlly/FlankLeft/FlankRight/SupportAlly/SeekAdvantage | ⬜ TODO | High |
| 16.6c | **Decision Execution** | Execute micro-movement, constrain to autonomy radius, respect formation orders | ⬜ TODO | Medium |
| 16.6d | **Rank-Based Autonomy** | Front rank (0): NO autonomy, Middle ranks (1-2): MICRO autonomy, Rear ranks (3+): NO autonomy | ⬜ TODO | Medium |
| 16.6e | **Formation Stability Gates** | Check formation integrity, casualty rate, reorganization state before micro-tactics | ⬜ TODO | Medium |
| 16.6f | **Dynamic Parameter Adjustment** | Autonomy radius adapts to objective, phase, formation priority (2m-8m range) | ⬜ TODO | Low |
| 16.7 | **Formation Cohesion Management** | Autonomy radius by objective: MainEffort 2m, Hold 3m, Attack 5m, Retreat 8m | ⬜ TODO | Medium |

## Phase 17: Coordinated Retreat (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 17.1 | **Tactical Withdrawal Decision** | Power < 0.7 → fall back to rally point (NOT leave map), regroup near spawn | ⬜ TODO | Medium |
| 17.2 | Covering Force (Rearguard) | Designate units to cover retreat | ⬜ TODO | High |
| 17.3 | Step-by-Step Withdrawal | Bounds: move, cover, move | ⬜ TODO | High |
| 17.4 | **Rally Point Selection** | Priority: near spawn (100m), defensible terrain, or 150m fallback | ⬜ TODO | Medium |
| 17.5 | **Full Rout vs Tactical Withdrawal** | Tactical: regroup on map, Full: morale collapse, leave map (vanilla) | ⬜ TODO | Medium |

## Phase 18: Reinforcement Details (Priority: Low)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 18.1 | Big Wave Strategic Impact | 100+ troops as major battlefield event | ⬜ TODO | Medium |
| 18.2 | Wave Coordination Between Sides | Time waves to counter enemy waves | ⬜ TODO | High |
| 18.3 | Desperation Waves | All-in spawning when losing badly | ⬜ TODO | Medium |
| 18.4 | Spawn Point Defense/Attack | Protect your spawn, harass theirs | ⬜ TODO | Medium |
| 18.5 | Merge vs Second Line Decision | Fresh troops join or form new line | ⬜ TODO | High |

## Phase 19: Battlefield Realism Enhancements (Priority: Medium)

| ID | System | Description | Status | Complexity |
|----|--------|-------------|--------|------------|
| 19.1 | **Ammunition Tracking & Behavior** | Archers track arrow count, switch to melee when depleted, formations reposition when ammo low | ⬜ TODO | Medium |
| 19.2 | **Line Relief & Rotation** | Active rotation of front-line troops with middle/rear ranks (not just gap filling), timed rotations during lulls | ⬜ TODO | High |
| 19.3 | **Morale Contagion** | Fear/courage spreads between nearby agents (5m radius), routing agents affect nearby morale, rallying agents boost nearby morale | ⬜ TODO | Medium |
| 19.4 | **Commander Death Response** | Formation behavior changes when leader killed, temporary confusion period, NCO/hero takes over with transition delay | ⬜ TODO | Medium |
| 19.5 | **Breaking Point Detection** | Detect specific formation crack moments (flanked + casualties > 30%, surrounded, leader dead + losing), trigger coordinated collapse or last stand | ⬜ TODO | Medium |
| 19.6 | **Feint Maneuvers** | Orchestrator can order fake attacks to draw enemy, formation advances then retreats, triggers if enemy reserves committed | ⬜ TODO | High |
| 19.7 | **Pursuit Depth Control** | Calculated pursuit distance (don't chase too far), stop at 200m or terrain boundary, recall if enemy rallies | ⬜ TODO | Medium |
| 19.8 | **Banner Rallying Points** | Banners act as visual rally points for scattered troops, agents gravitate toward friendly banners when disorganized, bannermen stay central to formation | ⬜ TODO | Medium |
| 19.9 | **High Ground Preference** | Agents prefer elevated positions when available, formations position for height advantage over enemy, simple Z-axis comparison (agent.Z > enemy.Z + 2m) | ⬜ TODO | Medium |

---

# 2b. Edge Cases By Phase

Each phase must handle these edge cases. Implementations should include null checks, early returns, and graceful degradation.

## Phase 1: Foundation

| Edge Case | Handling Strategy |
|-----------|-------------------|
| `Mission.Current` is null | Guard all entry points, abort initialization |
| Enlisted state changes during battle load | Re-check before orchestrator tick starts |
| Multiple missions loading in sequence | Clean up previous orchestrator on mission end |
| Mission ends before orchestrator initializes | Register cleanup in `OnMissionEnded` |
| Configuration file missing/corrupt | Use hardcoded defaults, log warning |
| Multiplayer battle detected | Activation gate returns false, skip entirely |
| Save/load during battle | Handle via `OnAfterMissionLoad` hook |
| Team is null or has no agents | Early exit from orchestrator creation |
| **Battle size limit very low (< 50 per side)** | Detect Skirmish scale, disable advanced features (line relief, feints) |
| **Battle size changes mid-battle (reinforcements)** | Re-evaluate scale every 30 seconds, smooth transitions |
| **Asymmetric battle sizes (100 vs 500)** | Use average of both sides for scale detection |
| **Player sets battle size to 1000** | Detect Massive scale, reduce tick frequency to prevent performance issues |

## Phase 2: Agent Combat Enhancements

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Agent dies during property calculation | Null-check `Agent.State`, skip dead agents |
| Agent has no weapon equipped | Use baseline properties, don't error |
| Agent switches weapons mid-combat | Recalculate properties on weapon change event |
| Horse archer dismounted | Transition from mounted to foot profile |
| Agent is player-controlled | Skip AI property overrides for player agent |
| Agent retreating/routing | Apply retreat-specific behavior (no blocking) |
| Agent in water/falling/ragdoll | Skip property updates until agent stable |
| All agents same skill level | System still works, no special handling needed |
| **Agent.Formation is null** | Skip objective modifiers, use skill-based only |
| **Orchestrator is null (not enlisted)** | Skip all modifiers, native properties remain unchanged |
| **Context is null** | Skip objective modifiers, continue with skill-based |
| **Battle phase unknown** | Treat as Engagement (no modifier applied) |
| **Skill level is 0** | Use minimum AI level (0.1), avoid division by zero |
| **Harmony patch throws exception** | Catch, log error, continue with native properties (safe fallback) |

## Phase 3: Formation Intelligence

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Formation has 0 agents | Skip formation entirely, mark as invalid |
| Formation has 1 agent | Treat as solo unit, limited tactics available |
| Formation disbanded mid-charge | Abort charge, reassign agents to other formations |
| All cavalry dead | No cavalry charges possible, remove from options |
| Mixed formation (infantry + cavalry) | Use dominant type for formation behavior |
| Formation leader dies | Native handles succession, orchestrator continues |
| Pike infantry have no pikes (dropped/broken) | Fall back to secondary weapons, skip pike logic |
| Multi-weapon soldier all weapons dropped | Agent becomes non-combatant, low priority |
| Target formation destroyed mid-charge | Abort charge, acquire new target |
| Self-organizing with identical troops | Random tie-breaking, any order valid |
| **Position invalid (NavMesh Zero)** | Search expanding circles max 50m, fallback to current position |
| **No valid position in search radius** | Use current formation position, log warning, notify orchestrator |
| **All sample positions score 0** | Stay in current position (best available) |
| **Cavalry charging but < 80 units for square** | Use shield wall if shields, loose if no shields |
| **No shields when cavalry threatens** | Use loose arrangement to minimize charge impact |
| **Multiple cavalry threats detected** | Respond to highest threat score (closest + fastest) |
| **Threat destroyed while intercepting** | Acquire new target or return to default flank position |
| **Orchestrator null when notifying** | Continue with local behavior only, log warning |
| **UnderRangedAttackRatio unavailable** | Skip suppression response, use objective-based arrangement |

## Phase 4: Battle Orchestrator Core

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Battle ends during OODA loop | Check `Mission.IsEnding` at start of each tick |
| All formations destroyed | Team eliminated, stop orchestrator |
| Team has no agents remaining | Early exit, no decisions to make |
| Power ratio is NaN/infinity | Guard division by zero, default to 1.0 |
| Enemy team retreated (no targets) | Transition to consolidation/pursuit |
| Trend data insufficient (<5 samples) | Use current snapshot, mark trend as unknown |
| Strategy mode indeterminate | Default to EngageBalanced |
| Enemy orchestrator data unavailable | Act on observable enemy behavior only |

## Phase 5: Tactical Decision Engine

| Edge Case | Handling Strategy |
|-----------|-------------------|
| No archers on team | Skip archer-specific targeting logic |
| No cavalry on team | Skip cavalry reserve/timing logic |
| All enemies same armor/tier | Random target selection, equal priority |
| Cavalry cannot reach target (terrain) | Terrain check before charge commit |
| Reserve already committed, commit called again | Ignore duplicate commit commands |
| Pursuit targets teleport/despawn | End pursuit, transition to consolidation |
| Cycle charging cavalry destroyed mid-cycle | Cancel cycle, no further cycles |
| No valid charge targets | Hold cavalry, wait for opportunity |
| Reserve commit threshold never reached | Continue holding until battle outcome clear |
| **Archer target destroyed during shooting** | Acquire new target, transition to Approaching state |
| **Raycast fails or Scene null** | Assume clear LOS (safer than stuck), continue shooting |
| **All micro-adjustment positions invalid** | Stay in current position, log warning |
| **MissileRangeAdjusted is 0** | Use fallback range of 100m |
| **Formation has no ranged weapons** | Skip archer behavior entirely |
| **Cavalry timer null when checked** | Create new timer with default duration |
| **Target formation null mid-charge** | Abort charge, transition to Reforming |
| **DeviationOfPositions returns NaN** | Treat as very high (50f), trigger reform immediately |
| **Target width is 0** | Use default cavalry width (don't crash) |
| **Speed is 0 (not moving)** | Adaptive timer: use maximum duration (20s charge) |
| **Kill ratio undefined (no kills)** | Adaptive timer: use base duration (5s melee) |
| **All cavalry targets score equally** | Select closest, then random tie-break |

## Phase 6: Battle Plan Generation

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Terrain data unavailable | Use flat-terrain default plan |
| No valid plan matches conditions | Fall back to balanced engagement |
| Plan becomes invalid mid-execution | Trigger plan adaptation early |
| Main effort formation destroyed | Reassign main effort to next strongest |
| All formations assigned same objective | Spread objectives to avoid overlap |
| Sequential phase formation killed | Skip phase, continue to next |
| Plan scored identically (tie) | Random tie-breaking |
| Insufficient troops for selected plan | Scale down plan complexity |
| Enemy composition unknown | Use balanced counter-plan |

## Phase 7: Reserve & Retreat Management

| Edge Case | Handling Strategy |
|-----------|-------------------|
| No reserve designated (all committed) | Mark reserve as empty, skip reserve logic |
| Rally point unreachable (terrain) | Select nearest accessible point |
| Covering force destroyed while covering | Main force continues retreat, accept losses |
| Stalemate timer triggers but enemy routing | Cancel stalemate, transition to pursuit |
| Last stand triggered but battle won | Cancel last stand, transition to exploitation |
| Retreat ordered with only 1 formation | That formation becomes rearguard AND retreating |
| All formations below retreat threshold | Team-wide retreat, no covering force |
| Rally point occupied by enemies | Select alternate rally point |

## Phase 8: Player Counter-Intelligence (T7+)

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Player T7+ but in simulation battle | Check if player is actually commanding |
| Player changes formation mid-battle | Update tracking on formation change |
| Player dismounts/mounts | Recalculate threat projection (speed change) |
| Player dead but battle continues | Stop tracking player, normal AI behavior |
| Player far from main battle (scouting) | Reduce threat priority, focus on main battle |
| Player has no troops (solo) | Treat as single high-value target |
| Player controlled by AI (auto-battle) | May not trigger T7+ logic at all |
| Cannot determine player formation | Fall back to tracking player agent directly |
| Player is on AI's team | Skip counter-AI entirely (same team) |

## Phase 9: Reinforcement Intelligence

| Edge Case | Handling Strategy |
|-----------|-------------------|
| No more reinforcements available | Mark reinforcement system complete |
| Spawn point occupied by enemies | Delay spawn or force spawn with protection |
| Reinforcements spawn into melee | Immediate engagement mode, skip forming up |
| All formations at capacity | Create new formation for reinforcements |
| Elite distribution when no elites remain | Distribute available quality fairly |
| Wave coordination with no enemy waves | Use own tactical timing only |
| Spawn blocked by terrain | Engine handles, no mod action needed |
| Reinforcement wave during retreat | Consider delaying wave or emergency spawn |

## Phase 10: Battle Pacing & Cinematics

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Battle ends during skirmish phase | Allow natural conclusion, no forced drama |
| No heroes for champion duels | Skip duel system for this battle |
| All banners already captured | Skip banner drama |
| Dramatic moment interrupted by rout | Let rout take priority, skip drama |
| Very short battle (<30 seconds) | Skip pacing system, natural conclusion |
| No suitable last stand candidates | Skip last stand scenarios |
| Champion killed instantly (one-shot) | No duel established, continue normally |
| Dramatic moment would affect outcome | Never let drama override tactical reality |

## Phase 11: Terrain & Morale Exploitation

| Edge Case | Handling Strategy |
|-----------|-------------------|
| No terrain features on map | Use flat-terrain behaviors |
| Completely flat battlefield | Skip terrain-seeking logic |
| Morale system disabled (mod conflict) | Check for morale values, skip if unavailable |
| All enemies max morale | Don't rely on morale breaks, use attrition |
| Terrain data stale after battle moves | Periodic terrain re-query (every 10s) |
| High ground occupied by enemy | Contest or find alternative position |
| Choke point benefits enemy more | Avoid that choke point |
| Forest terrain crashes pathfinding | Use simpler pathfinding, avoid dense areas |

## Phase 12: Formation Doctrine System

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Extreme imbalance (10 vs 500) | Use asymmetric scaling per side (Skirmish vs Massive) |
| All troops same type | Simplified doctrine, no combined arms |
| Reinforcements change battle scale | Re-evaluate scale every 30s (Phase 1.10), smooth transition |
| Counter-doctrine for unknown doctrine | Use balanced counter |
| **Formation count exceeds available troops** | Reduce to min 1 formation, minimum 20 troops per formation |
| Single-formation army (Skirmish scale) | Skip multi-formation doctrine, use simple line |
| Doctrine requires cavalry but none available | Fall back to infantry-only doctrine |
| **Battle scale changes mid-fight** | Don't immediately reorganize formations, wait for lull or phase transition |
| Battle scale ambiguous (250 troops) | Use medium-scale rules |

## Phase 13: Unit Type Formations

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Infantry have no shields | Cannot use Shield Wall, use Line/Loose |
| Fewer than 80 troops | Cannot form Square, use tighter formations |
| Mixed cavalry/infantry in formation | Native handles, respect native assignment |
| Formation switching during charge | Delay switch until charge complete |
| Multi-formation coordination with destroyed formations | Remove from coordination, continue with survivors |
| All archers killed | Skip archer positioning logic |
| All cavalry killed | Skip cavalry flank positioning |
| Formation shape not supported (modded) | Fall back to Line formation |

## Phase 14: Intelligent Formation Organization

| Edge Case | Handling Strategy |
|-----------|-------------------|
| All troops identical equipment score | Random front-line assignment |
| Formation has only ranged troops | Use loose formation, all treated as ranged |
| Front line all killed, only ranged remain | Ranged become front line by necessity |
| Gap filling when no reserves available | Accept gaps, maintain formation as-is |
| Tiny formation (3 or fewer troops) | Skip organization logic, all front-line |
| Score calculation returns same value | Tie-break by agent index |
| Equipment data unavailable | Use troop tier as fallback score |

## Phase 15: Plan Execution & Anti-Flip-Flop

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Battle starts in Engaging phase (close spawn) | Skip Forming/Advancing phases |
| Catastrophic failure false positive | Require sustained failure (10+ seconds) |
| Anti-flip-flop locks needed change | Emergency override for genuine catastrophe |
| Enemy composition changes (reinforcements) | Re-evaluate composition periodically |
| Plan execution with missing formations | Reassign objectives to remaining formations |
| State machine in invalid state | Reset to Engaging, log error |
| Phase transition during plan change cooldown | Allow changes at transitions |
| All phases complete but battle continues | Stay in Pursuing/Consolidating |

## Phase 16: Agent-Level Combat Director

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Hero killed during duel | End duel, other hero returns to formation |
| No suitable duel opponent | Skip duel system |
| Last stand with only 1 agent | Treat as heroic last stand (solo) |
| Banner bearer killed before drama | Skip banner drama for this bearer |
| Small squad all killed mid-action | End action, no completion callback |
| Drama conflicts with formation orders | Formation orders take priority |
| Multiple duels requested | Only one active duel per side at a time |
| Champion is player agent | Skip AI champion behavior, player controls |
| **Agent is dead when micro-tactics tick** | Skip entirely, check `Agent.State` |
| **Agent has no formation (agent.Formation null)** | Skip micro-tactics, use native behavior |
| **FormationRankIndex unavailable** | Treat as rank 0 (no autonomy, safer) |
| **FormationOrderPosition is invalid (NavMesh Zero)** | Use agent's current position |
| **No enemies nearby for sampling** | Default to Attack decision (press forward) |
| **No allies nearby for FindAlly** | Fallback to BackStep or SeekAdvantage |
| **Agent is in main effort formation** | Early exit, follow formation strictly (no micro) |
| **All decision scores are 0** | Default to Attack (aggressive fallback) |
| **Deviation calculation produces negative** | Use absolute value |
| **Autonomy disabled mid-fight (formation unstable)** | Smoothly return to formation position |
| **Context is null when evaluating decisions** | Deny autonomy (safer default) |
| **Orchestrator is null** | Skip micro-tactics entirely |
| **Situation sampling throws exception** | Catch, log, continue without micro-tactics for this agent |
| **Agent is player-controlled** | Skip micro-tactics entirely (player controls) |
| **Agent in water/falling/ragdoll state** | Skip micro-tactics, wait for stable state |
| **Formation moving orders active (charge/advance)** | Disable micro-tactics, follow movement order |
| **Micro-movement produces invalid position** | Stay at formation order position |
| **Multiple agents select same micro-position** | Allow (natural clustering), don't force spread |

## Phase 17: Coordinated Retreat

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Rearguard destroyed while covering | Main force accepts losses, continues retreat |
| Rally point in enemy territory | Find nearest safe rally point |
| Retreat blocked by terrain | Find alternate retreat path |
| Step-by-step with only one formation | Single formation does fight-retreat-fight |
| Retreat decision but battle instantly ends | No action needed, battle over |
| Covering force refuses to hold | Force hold behavior, override morale |
| Rally point changes during retreat | Notify all formations of new rally |
| Enemy doesn't pursue | Stop retreat, transition to consolidation |

## Phase 18: Reinforcement Details

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Desperation wave when already winning | Skip desperation, use normal timing |
| Spawn point captured by enemy | Force spawn with immediate combat flag |
| Merge with incompatible formation types | Create new formation instead |
| Wave coordination with no enemy waves | Self-paced wave timing |
| Big wave arrives but battle over | Wave doesn't spawn, battle ends |
| Multiple desperation triggers | Only one desperation wave per battle |
| Merge decision when formations destroyed | Create new formation for wave |
| All spawn points blocked | Engine handles, accept spawn location |

## Phase 19: Battlefield Realism Enhancements

| Edge Case | Handling Strategy |
|-----------|-------------------|
| **Ammo tracking when agent dies** | Reset tracking, don't carry over to replacement |
| **Ammo count unavailable (unsupported weapon)** | Skip ammo tracking for that agent, assume unlimited |
| **Archer switches weapon mid-tracking** | Reset ammo count for new weapon type |
| **Formation ammo depleted but battle critical** | Keep fighting with melee, don't retreat for ammo |
| **Line relief when formation too small (< 20 troops)** | Skip rotation, fight as-is |
| **Line relief during active charge** | Cancel rotation, complete charge first |
| **Middle rank killed during rotation forward** | Next rank steps up immediately (emergency gap fill) |
| **Morale contagion creating panic cascade** | Orchestrator can override with "hold the line" order |
| **Morale boost from rally when formation routing** | Rally only works if casualties < 60%, else full rout |
| **Commander dies but no NCO/hero available** | Formation becomes leaderless, defensive stance, reduced effectiveness |
| **Multiple commanders die in sequence** | Each death compounds confusion (cumulative penalty) |
| **Sound awareness when Scene audio unavailable** | Fall back to visual only, log warning |
| **Breaking point detection false positive** | Require sustained conditions (10+ seconds) before triggering |
| **Formation breaks but orchestrator needs them** | Allow break, reassign objectives to remaining formations |
| **Feint maneuver when no enemy reserves** | Cancel feint, use normal attack instead |
| **Feint fails to draw enemy** | Timeout after 30s, convert to real attack |
| **Pursuit into ambush/reinforcements** | Detect trap (enemy power suddenly increases), emergency recall |
| **Pursuit beyond map boundary** | Hard stop at boundary, reform |
| **Banner bearer killed during rally** | Nearby agent picks up banner, continue rally |
| **All banners lost/captured** | Form emergency rally points at formation centers |
| **Agent seeks cover during charge** | Disable cover-seeking during aggressive objectives |
| **Terrain exploitation blocks formation movement** | Override micro-terrain for formation macro-movement |
| **Agent scavenging when formation needs them** | Cancel scavenge on formation orders (charge/retreat) |
| **Scavenging in enemy-controlled area** | Risk calculation: only if safe (no enemies within 20m) |

---

# 3. System Specifications

## 3.1 Battle Scale Detection System

**Purpose:** Dynamically detect battle size and scale AI complexity based on actual troop counts. Handles player-configurable battle size limits (200-1000 troops).

**Integration Points:**
- Phase 1: Core context component, evaluated on initialization and every 30 seconds
- Phase 12: Formation count scaling
- Phase 14: Formation depth (ranks) scaling
- Phase 16.6: Agent micro-tactics sampling radius
- Phase 19: Enable/disable advanced features based on scale

**Battle Scale Enum:**
```csharp
public enum BattleScale
{
    Skirmish,      // < 100 troops per side (avg)
    SmallBattle,   // 100-200 per side
    MediumBattle,  // 200-350 per side
    LargeBattle,   // 350-500 per side
    MassiveBattle  // 500+ per side
}
```

**Detection Logic:**
```csharp
public BattleScale DetectBattleScale()
{
    int ourTroops = Team.ActiveAgents.Count + EstimatedReinforcements();
    int enemyTroops = EnemyTeam.ActiveAgents.Count + EstimatedEnemyReinforcements();
    int avgTroops = (ourTroops + enemyTroops) / 2;
    
    if (avgTroops < 100) return BattleScale.Skirmish;
    if (avgTroops < 200) return BattleScale.SmallBattle;
    if (avgTroops < 350) return BattleScale.MediumBattle;
    if (avgTroops < 500) return BattleScale.LargeBattle;
    return BattleScale.MassiveBattle;
}
```

**Scale Configuration Table:**

| System | Skirmish | Small | Medium | Large | Massive |
|--------|----------|-------|--------|-------|---------|
| **Formation Count** | 1-2 | 2-3 | 3-4 | 4-6 | 5-8 |
| **Max Ranks** | 1-2 | 2-3 | 2-4 | 3-5 | 4-6 |
| **Reserve %** | 10% | 15% | 20% | 25% | 30% |
| **Micro Sample Radius** | 5m | 8m | 10m | 12m | 15m |
| **Orchestrator Tick** | 2.0s | 1.5s | 1.0s | 1.0s | 0.8s |
| **Line Relief** | ❌ | ✅ (40+ troops) | ✅ | ✅ | ✅ |
| **Feint Maneuvers** | ❌ | ❌ | ✅ | ✅ | ✅ |
| **Min Troops for Rotation** | N/A | 40 | 40 | 40 | 40 |

**Smooth Transitions:**
- Re-evaluate scale every 30 seconds (not every tick)
- Use hysteresis: require 20% change to shift scale (prevents flip-flop)
- Log scale changes: `"[BattleAI] Scale changed: Medium → Large (troops: 320→480)"`

**Edge Cases:**
- **Very low battle size (< 50):** Treat as Skirmish, disable all advanced features
- **Asymmetric battles (50 vs 500):** Use average for scale, adjust formation count per side
- **Massive battles (1000+):** Cap at Massive, reduce tick frequency to 0.8s minimum
- **Mid-battle reinforcements:** Smoothly transition scales, don't reset AI state

**Configuration (JSON):**
```json
"battleScaling": {
  "skirmishThreshold": 100,
  "smallThreshold": 200,
  "mediumThreshold": 350,
  "largeThreshold": 500,
  "reevaluateInterval": 30.0,
  "scaleChangeHysteresis": 0.2
}
```

---

## 3.2 BattleOrchestrator

**Purpose:** Commander-layer AI that coordinates all formations for a team.

### Files
- `src/Features/Combat/BattleOrchestrator.cs`
- `src/Features/Combat/BattleOrchestratorBehavior.cs`

### Inputs (Read-Only)
| Source | Data |
|--------|------|
| `Team.QuerySystem` | Power ratios, composition, positions |
| `Formation.QuerySystem` | Local power, casualties, suppression |
| `TacticalPosition/Region` | Terrain features (if present) |

### Outputs (Bounded Interventions)
| Output | Description |
|--------|-------------|
| Strategy Mode | EngageBalanced, DelayDefend, Exploit, Withdraw |
| Formation Roles | MainLine, Screen, FlankGuard, Reserve |
| Behavior Weight Nudges | Adjust formation behavior weights |
| Reserve Commands | Commit, hold, intercept |

### Decision Loop (OODA)
```
Every 1-2 seconds:
1. OBSERVE: Capture signals, update rolling trend history
2. ORIENT: Estimate frontline, sample local superiority, detect threats
3. DECIDE: Choose strategy mode (with hysteresis), pick main effort
4. ACT: Apply bounded interventions with cooldowns
```

### Key Thresholds
| Threshold | Value | Usage |
|-----------|-------|-------|
| Decision Interval | 1.5 sec | OODA loop tick |
| Strategy Hysteresis | 1.3x | Prevent flip-flopping |
| Retreat Threshold | 0.4 | RemainingPowerRatio |
| Reserve Commit (Casualties) | 25% | Main line casualties |
| Reserve Commit (Opportunity) | Exposed flank | Enemy flank open |

---

## 3.3 Agent Combat AI

**Purpose:** Individual soldier combat behavior tuning via AgentDrivenProperties.

### Files
- `src/Features/Combat/EnlistedAgentStatCalculateModel.cs`
- `src/Features/Combat/AgentCombatTuner.cs`

### Core Properties (40+ total, key ones listed)

#### Melee Combat
| Property | Range | Effect |
|----------|-------|--------|
| `AIBlockOnDecideAbility` | 0.5–0.99 | Blocking decision quality |
| `AIParryOnDecideAbility` | 0.5–0.95 | Parry decision quality |
| `AiRandomizedDefendDirectionChance` | 0.0–1.0 | Wrong block direction % |
| `AIAttackOnDecideChance` | 0.05–0.48 | Attack decision likelihood |
| `AIAttackOnParryChance` | 0.0–0.08 | Counter-attack after parry |

#### Ranged Combat
| Property | Range | Effect |
|----------|-------|--------|
| `AiShooterError` | ~0.008 | Base aiming error |
| `AiRangerLeadErrorMin/Max` | -0.35–0.2 | Lead target error |
| `AiShootFreq` | 0.3–1.0 | Shooting frequency |

#### Reactions
| Property | Range | Effect |
|----------|-------|--------|
| `AiCheckApplyMovementInterval` | 0.05–0.1 | Movement update rate |
| `AiMovementDelayFactor` | 1.0–1.33 | Reaction delay |

### AI Profiles

#### Veteran Profile
```csharp
props.AIBlockOnDecideAbility = 0.92f;
props.AIParryOnDecideAbility = 0.85f;
props.AiRandomizedDefendDirectionChance = 0.15f;
props.AIAttackOnDecideChance = 0.4f;
props.AiShooterError = 0.005f;
props.AiShootFreq = 0.9f;
```

#### Elite Guard Profile
```csharp
props.AIBlockOnDecideAbility = 0.98f;
props.AIParryOnDecideAbility = 0.95f;
props.AiRandomizedDefendDirectionChance = 0.05f;
props.AIAttackOnParryChance = 0.2f;
props.AiCheckApplyMovementInterval = 0.03f;
```

### Context-Aware Tuning Triggers
| Situation | Property Changes |
|-----------|------------------|
| Under ranged attack | Shield probability ↑ |
| Locally outnumbered | Block ↑, Attack ↓ |
| Winning overall | Counter-attack chance ↑ |
| Formation charging | Aggression ↑ |

---

## 3.4 Cavalry Cycle Manager

**Purpose:** Implement proper lance doctrine with charge → impact → disengage → reform cycles with formation integrity gates.

### Files
- `src/Features/Combat/CavalryCycleManager.cs` (enhanced 9-state machine)
- `src/Features/Combat/CavalryDeploymentDirector.cs` (plan-based release timing)
- `src/Features/Combat/CavalryTargetSelector.cs` (intelligent target selection)
- `src/Features/Combat/AdaptiveTimingManager.cs` (dynamic timer calculations)

### State Machine (Enhanced)
| State | Duration | Integrity Gate | Purpose |
|-------|----------|----------------|---------|
| Reserve | Variable | N/A | Wait for orchestrator release |
| Positioning | 10-20s | N/A | Get 50-80m from target |
| Charging | 15s (adaptive) | Deviation < 5f to advance | Build speed, lances ready |
| ChargingPast | 6s | Tight formation punch-through | Pass through enemy formation |
| Impact | 1-3s | N/A | Lance damage window |
| Melee | 5s (+3/-2 adaptive) | Check bogged down | Finish immediate threats |
| Disengaging | 5-10s | Speed > 2m/s | Break contact before stuck |
| Rallying | 10-15s | Gather scattered units | Move to rally point |
| Reforming | 12s (*0.75 if suppressed) | Deviation < 12f to complete | Prepare for next charge |
| Bracing | Variable | N/A | Counter-charge ready stance |

### Key Parameters
| Parameter | Value | Rationale |
|-----------|-------|-----------|
| MaxMeleeTime | 12 sec | Don't get bogged down |
| MinChargeDistance | 60m | Need speed for lance impact |
| ReformDistance | 80m | Rally far enough from enemy |
| ReformDuration | 8 sec | Time to tighten formation |

### Weapon-Aware Adjustments
| Lance Ratio | MaxMeleeTime | MinChargeDistance |
|-------------|--------------|-------------------|
| > 60% | 10s | 70m |
| 30-60% | 15s | 60m |
| < 30% | 25s | 40m |

---

## 3.5 Formation Weapon Discipline

**Purpose:** Ensure soldiers use optimal weapons for range (pikes, spears, multi-weapon loadouts).

### Files
- `src/Features/Combat/PikeInfantryManager.cs`
- `src/Features/Combat/MultiWeaponInfantryManager.cs`

### Pike/Spear Distance Rules
| Distance | Weapon | Action |
|----------|--------|--------|
| 6m+ | Pike out | Advance in formation |
| 3-6m | Pike out | HOLD, thrust attacks |
| 1.5-3m | Pike out | Contested range |
| < 1.5m | Switch sidearm | Enemy inside reach |
| > 3m again | Switch back | Re-establish reach |

### Multi-Weapon Priority (Javelin + Spear + Sword)
| Distance | Weapon | Rationale |
|----------|--------|-----------|
| 20m+ | Throwing | Ranged damage |
| 15-20m | Prepare melee | Switch before contact |
| 3-15m | Melee polearm | **Reach advantage zone** |
| 1.5-3m | Keep polearm | Still effective |
| < 1.5m | Sidearm | Close combat |

---

## 3.6 Reserve Manager

**Purpose:** Hold reserves until optimal commit moment.

### Files
- `src/Features/Combat/ReserveManager.cs`

### Commit Triggers
| Trigger | Response |
|---------|----------|
| Main line > 25% casualties | Commit to reinforce |
| Enemy flank exposed | Commit to exploit |
| Enemy reserves engaged (not winning) | Commit to match |
| Battle time > 30s | Allow commitment |

### Reserve Sizing by Situation
| Situation | Reserve Size |
|-----------|--------------|
| Counter already deployed | 0-10% (minimal) |
| Counter not deployed, we have it | 20-30% |
| Player significantly stronger | 30%+ |

---

## 3.7 Battle Plan System

**Purpose:** Generate and execute coordinated battle plans.

### Files
- `src/Features/Combat/BattlePlanSelector.cs`
- `src/Features/Combat/FormationObjectiveAssigner.cs`

### Plan Types
| Plan | Best When | Main Effort |
|------|-----------|-------------|
| Left/Right Hook | Cavalry advantage, flank exposed | Flank axis |
| Center Punch | Infantry advantage, thin center | Center axis |
| Double Envelopment | 1.4x+ numerical advantage | Both flanks |
| Hammer & Anvil | 25%+ cavalry | Infantry pins, cavalry rear |
| Delay | Outnumbered, defensive objective | Screen all |
| Refused Flank | Terrain advantage on one side | Strong side |

### Formation Objectives
| Objective | Behavior | Engagement |
|-----------|----------|------------|
| Attack | Advance aggressively | Full |
| Pin | Engage to hold | Moderate |
| Screen | Delay, don't get destroyed | Light |
| Hold | Defend position | Full, no advance |
| Flank | Maneuver to side | When in position |
| Pursue | Chase fleeing | After rout |

---

## 3.8 Formation Doctrine System

**Purpose:** Organize armies into appropriate formations based on battle scale and composition.

### Files
- `src/Features/Combat/FormationDoctrineManager.cs`
- `src/Features/Combat/BattleScaleDetector.cs`

### Battle Scales
| Scale | Troops on Field | Formations | Characteristics |
|-------|-----------------|------------|-----------------|
| **Lord Party** | 50-200 | 2-3 | Everyone fits, no waves, quick |
| **Small Army** | 200-400 | 4 | Can split infantry, add reserve |
| **Large Army** | 400-500 | 5-6 | Three-wing or classical with reserve |

### Formation Doctrines
| Doctrine | Best For | Structure |
|----------|----------|-----------|
| **Single Deep Line + Reserve** | Medium armies, balanced, defending | 3-4 rank infantry, archers behind, reserve behind |
| **Three-Wing Line** | Large armies, envelopment | Left/Center/Right infantry wings, cavalry flanks |
| **Extended Thin Line** | Outnumbered | Match frontage with thin (2 rank) line |
| **Hammer and Anvil** | Cavalry-heavy (30%+) | Infantry holds, cavalry rear attack |

### Formation Count by Size
```csharp
if (troops < 100) return 2-3;       // Infantry + maybe Archers/Cavalry
if (troops < 200) return 3;         // Infantry + Archers + Cavalry  
if (troops < 400) return 4;         // + Reserve or Flank formation
if (troops >= 400) return 5-6;      // Three-wing or classical
```

---

## 3.9 Unit Type Formations

**Purpose:** Configure infantry/archer/cavalry formation shapes for different situations.

### Files
- `src/Features/Combat/InfantryFormationSelector.cs`
- `src/Features/Combat/ArcherPositioner.cs`
- `src/Features/Combat/CavalryFormationManager.cs`

### Infantry Formations
| Formation | Use Case | Speed | Weakness |
|-----------|----------|-------|----------|
| **Line** | Default combat | 80% | None |
| **Shield Wall** | Advancing into archers, holding | 30% | Flanks, slow to turn |
| **Loose** | Under archer fire (no shields) | 90% | Weak in melee |
| **Square** | Cavalry charging you (80+ troops) | 30% | Can't advance, arrows hurt |
| **Circle** | Surrounded, last stand | 0% | Thin everywhere |

### Infantry Formation Selection
```
1. Surrounded? → Circle
2. Cavalry charging? → Square
3. Under archer fire + have shields? → Shield Wall
4. Under archer fire + no shields? → Loose
5. Default → Line
```

### Archer Positioning
| Position | Distance from Infantry | Notes |
|----------|------------------------|-------|
| Behind infantry | 20-40m | Standard, protected |
| Inside circle | 0m (center) | When surrounded |
| On flank | 30m offset | Independent fire |

### Cavalry Formations by Task
| Task | Formation | Arrangement |
|------|-----------|-------------|
| Reserve/Waiting | Line | 2 ranks, facing enemy |
| Screening | Loose | Spread for coverage |
| Charging infantry | Skein (Wedge) | Concentrated impact |
| Charging archers | Skein or Line | Soft target, either works |
| Pursuit | Loose | Cover ground |
| Counter-cavalry | Line | Width for collision |

---

## 3.10 Plan Execution State Machine

**Purpose:** Execute battle plans through phases with anti-flip-flop protection.

### Files
- `src/Features/Combat/PlanExecutor.cs`
- `src/Features/Combat/FlipFlopPrevention.cs`

### Plan Phases
| Phase | Description | Plan Changes? |
|-------|-------------|---------------|
| **Forming** | Deploy into formation | ✅ Free to pick |
| **Positioning** | Move to engagement position | ❌ Locked |
| **Approach** | Closing distance | ⚠️ If enemy shifts |
| **Contact** | Skirmishing begins | ⚠️ If needed |
| **MainEngagement** | Lines engaged | ❌ Locked (unless catastrophic) |
| **Exploiting** | Breakthrough achieved | ✅ Commit reserves |
| **Pursuing** | Enemy routing | ✅ Pursuit mode |
| **Withdrawing** | We're retreating | ✅ Survival mode |

### Anti-Flip-Flop Rules
| Rule | Value | Purpose |
|------|-------|---------|
| Minimum commitment | 30 sec | Give plan time to work |
| Change cooldown | 20 sec | Prevent rapid switching |
| Max changes in window | 2 per 60s | Detect flip-flopping |
| Enemy change cooldown | 30 sec | Don't respond immediately to enemy change |

### Legitimate Change Triggers
- Phase transition (natural progression)
- Catastrophic failure (40%+ casualties, encircled)
- Major enemy tactical shift (committed reserve, stance change)
- New major opportunity (gap in enemy line, commander killed)

---

## 3.11 Agent-Level Combat Director

**Purpose:** Create dramatic agent-level moments (duels, last stands, banner drama).

### Files
- `src/Features/Combat/AgentCombatDirector.cs`
- `src/Features/Combat/ChampionDuelManager.cs`

### Champion Duel System
| Trigger | Behavior |
|---------|----------|
| Two heroes in proximity | Clear space, focus on each other |
| One hero heavily wounded | Dramatic final clash |
| Hero kills hero | Morale cascade effect |

### Last Stand Scenarios
| Trigger | Behavior |
|---------|----------|
| Formation < 10 troops, surrounded | Form tight circle, fight to death |
| Single hero remaining | Dramatic solo against many |
| Banner bearer alone | Protect banner at all costs |

### Banner Bearer Drama
| Event | Effect |
|-------|--------|
| Banner planted | Morale boost to nearby allies |
| Banner carrier killed | Nearby troop scrambles to recover |
| Banner captured | Major morale hit to owning side |

---

## 3.12 Agent Micro-Tactics System

**Purpose:** Context-aware individual agent tactical micro-decisions that integrate with formation organization and orchestrator strategy.

**Design Rationale:** While our orchestrator controls formation-level strategy and our formation organization system places troops in optimal ranks, individual soldiers in combat need the ability to make small tactical adjustments within their assigned position. This system bridges the gap between strategic formation control and realistic individual combat behavior, allowing middle-rank soldiers to respond to immediate tactical situations (flanking opportunities, supporting endangered allies, seeking advantageous positioning) while maintaining formation cohesion and respecting the orchestrator's overall battle plan.

### Files
- `src/Features/Combat/AgentMicroTacticsSystem.cs`
- `src/Features/Combat/AgentSituationSampler.cs`
- `src/Features/Combat/MicroDecisionEvaluator.cs`
- `src/Features/Combat/Models/MicroDecision.cs`

### Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│  ORCHESTRATOR                                           │
│  Provides: Battle plan, phase, formation objectives     │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│  FORMATION ORGANIZATION (Phase 14)                      │
│  - Heavy troops in front ranks (0)                      │
│  - Light troops in rear ranks (3+)                      │
│  - Gap filling when front falls                         │
│  - Flank spillover for rear troops                      │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│  RANK-BASED AUTONOMY (Phase 16.6d)                      │
│  - Front rank (0): NO micro-tactics                     │
│  - Middle ranks (1-2): MICRO-TACTICS ACTIVE             │◄─┐
│  - Rear ranks (3+): NO micro-tactics                    │  │
└──────────────────────┬──────────────────────────────────┘  │
                       │                                      │
┌──────────────────────▼──────────────────────────────────┐  │
│  FORMATION STABILITY GATES (Phase 16.6e)                │  │
│  - Formation integrity check                            │  │
│  - Casualty rate check                                  │  │
│  - Reorganization state check                           │  │
└──────────────────────┬──────────────────────────────────┘  │
                       │                                      │
┌──────────────────────▼──────────────────────────────────┐  │
│  AGENT MICRO-TACTICS (Phase 16.6a-c)                    │  │
│  - Sample situation (allies/enemies)                    │  │
│  - Evaluate decisions (utility scores)                  │  │
│  - Execute micro-movement (3m max deviation)            │──┘
└─────────────────────────────────────────────────────────┘
```

### Decision Types (Phase 16.6b)

| Decision | Description | Use When |
|----------|-------------|----------|
| **Attack** | Press toward nearest enemy | Default aggressive action |
| **BackStep** | Step back 1-2m | Outnumbered, defensive posture |
| **FindAlly** | Move toward nearest ally | Isolated, seeking support |
| **FlankLeft** | Sidestep left | Enemy exposed on left |
| **FlankRight** | Sidestep right | Enemy exposed on right |
| **SupportAlly** | Move toward embattled ally | Ally outnumbered nearby |
| **SeekAdvantage** | Move to better position | Height/cover available nearby |

### Situation Sampling (Phase 16.6a)

Sample nearby agents within 10m radius:

```csharp
public class TacticalSituation
{
    public int AlliesNearby { get; set; }      // Allies within 10m
    public int EnemiesNearby { get; set; }     // Enemies within 10m
    public Agent NearestAlly { get; set; }     // Closest ally
    public Agent NearestEnemy { get; set; }    // Closest enemy
    public Agent MostThreatenedAlly { get; set; } // Ally with worst ratio
    public Vec3 HighGround { get; set; }       // Best terrain nearby
    public bool IsIsolated { get; set; }       // < 2 allies within 10m
    public bool IsOutnumbered { get; set; }    // Enemies > Allies * 1.5
}
```

### Utility Scoring (Phase 16.6b)

Each decision receives a normalized utility score (0-1):

```csharp
public class MicroDecisionScore
{
    // Attack
    public static float ScoreAttack(TacticalSituation situation, FormationContext context)
    {
        float score = 0.5f; // Base score
        
        // Objective modifier
        if (context.CurrentObjective == FormationObjective.Attack)
            score += 0.3f;
        else if (context.CurrentObjective == FormationObjective.Hold)
            score -= 0.2f;
        
        // Local superiority
        if (situation.AlliesNearby > situation.EnemiesNearby)
            score += 0.2f;
        else if (situation.IsOutnumbered)
            score -= 0.3f;
        
        // Agent combat readiness
        if (agent.Health < 50f)
            score -= 0.2f;
        
        return MBMath.ClampFloat(score, 0f, 1f);
    }
    
    // BackStep
    public static float ScoreBackStep(TacticalSituation situation, FormationContext context)
    {
        float score = 0f;
        
        // Defensive objective
        if (context.CurrentObjective == FormationObjective.Hold)
            score += 0.3f;
        
        // Outnumbered
        if (situation.IsOutnumbered)
            score += 0.4f;
        
        // Low health
        if (agent.Health < 40f)
            score += 0.3f;
        
        return MBMath.ClampFloat(score, 0f, 1f);
    }
    
    // FindAlly
    public static float ScoreFindAlly(TacticalSituation situation)
    {
        float score = 0f;
        
        if (situation.IsIsolated)
            score += 0.6f; // Strong incentive
        
        if (situation.AlliesNearby < 2)
            score += 0.3f;
        
        return MBMath.ClampFloat(score, 0f, 1f);
    }
    
    // Similar scoring for FlankLeft, FlankRight, SupportAlly, SeekAdvantage
}
```

### Rank-Based Autonomy (Phase 16.6d)

```csharp
public bool ShouldAllowMicroTactics(Agent agent, Formation formation)
{
    // Get agent's rank in formation
    int rankIndex = ((IFormationUnit)agent).FormationRankIndex;
    
    // Front rank (0): NO micro-tactics (need solid line)
    if (rankIndex == 0)
        return false;
    
    // Rear ranks (3+): NO micro-tactics (ranged/reserve)
    if (rankIndex >= 3)
        return false;
    
    // Middle ranks (1-2): CAN use micro-tactics
    return true;
}
```

### Formation Stability Gates (Phase 16.6e)

```csharp
public bool IsFormationStableForMicroTactics(Formation formation, FormationContext context)
{
    // Check formation integrity
    float deviation = formation.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
    if (deviation > 15f)
        return false; // Formation scattered
    
    // Check casualty rate
    if (formation.QuerySystem.CasualtyRatio > 0.2f)
        return false; // Taking heavy losses
    
    // Check if actively reorganizing (gap filling, etc.)
    if (context.IsReorganizing)
        return false;
    
    // Check if formation is moving (don't micro during maneuver)
    if (formation.QuerySystem.MovementSpeedMaximum > 1f && 
        formation.QuerySystem.FormationMovementOrder.OrderType != OrderType.None)
        return false;
    
    return true;
}
```

### Dynamic Parameter Adjustment (Phase 16.6f)

Autonomy radius adapts to context:

```csharp
public float GetMicroTacticsRadius(FormationContext context)
{
    // Base radius
    float radius = 3f;
    
    // Priority modifier
    if (context.Priority == FormationPriority.MainEffort)
        radius = 2f; // Tighter control
    
    // Objective modifier
    switch (context.CurrentObjective)
    {
        case FormationObjective.Hold:
        case FormationObjective.Screen:
            radius = 3f; // Tight
            break;
            
        case FormationObjective.Attack:
        case FormationObjective.Pin:
            radius = 5f; // Moderate
            break;
            
        case FormationObjective.FightingRetreat:
            radius = 8f; // Loose (survival)
            break;
    }
    
    // Phase modifier
    if (context.BattlePhase == BattlePhase.Crisis)
        radius *= 0.8f; // Tighten up in crisis
    
    return MBMath.ClampFloat(radius, 2f, 8f);
}
```

### Integration with Formation Organization

Micro-tactics layer **on top of** existing formation organization:

```csharp
public WorldPosition CalculateMicroPosition(Agent agent, WorldPosition formationOrderPosition)
{
    // LAYER 1: Formation Organization (Phase 14)
    // This already happened - heavy troops in front, light in rear, gap filling active
    
    int rankIndex = ((IFormationUnit)agent).FormationRankIndex;
    
    // LAYER 2: Rank-Based Autonomy (Phase 16.6d)
    if (rankIndex == 0 || rankIndex >= 3)
        return formationOrderPosition; // NO micro-tactics
    
    // LAYER 3: Formation Stability (Phase 16.6e)
    var context = _orchestrator.GetFormationContext(agent.Formation);
    if (!IsFormationStableForMicroTactics(agent.Formation, context))
        return formationOrderPosition; // Formation needs cohesion
    
    // LAYER 4: Micro-Tactics (Phase 16.6a-c)
    var situation = SampleSituation(agent);
    var decision = EvaluateBestDecision(agent, situation, context);
    WorldPosition desired = ExecuteDecision(agent, decision, formationOrderPosition);
    
    // LAYER 5: Constrain to Autonomy Radius (Phase 16.6f)
    float maxDeviation = GetMicroTacticsRadius(context);
    return ConstrainToRadius(desired, formationOrderPosition, maxDeviation);
}
```

### Integration with Other Phase 3 Systems

**Weapon Discipline (Phase 3.5, 3.6):** Micro-tactics do not interfere with weapon switching. Pike infantry will still switch to sidearms at < 1.5m regardless of micro-decisions. Multi-weapon soldiers will still progress through throwing → polearm → sword based on distance. Micro-tactics only affect positioning, not weapon choice.

**Gap Filling (Phase 14.3):** When front rank falls and middle ranks are ordered to step up (gap filling), micro-tactics are automatically disabled for those agents during the reorganization. The `IsReorganizing` flag in FormationContext blocks micro-tactics until the formation stabilizes. Once gap filling is complete and the agent is settled in their new rank, micro-tactics can resume if they're still in rank 1-2.

**Flank Spillover (Phase 14.2):** Rear troops (rank 3+) working flanks do NOT get micro-tactics - they follow their spillover orders exactly. Only middle ranks (1-2) in the main formation body use micro-tactics. This ensures flanking maneuvers execute as planned.

**Self-Organizing Ranks (Phase 3.7):** Formation reorganization based on FrontLineScore happens BEFORE micro-tactics are evaluated. Heavy troops are assigned to rank 0 (no micro-tactics), light troops to rank 3+ (no micro-tactics), and medium troops to rank 1-2 (micro-tactics eligible). This ensures micro-tactics only apply to troops who are neither critical frontline holders nor vulnerable rear support.

**Formation Safeguards (Phase 14.4):** The existing 25m max deviation safeguard still applies as an outer boundary. Micro-tactics operate within a much smaller radius (2m-8m), so they never approach the safeguard limit. If an agent somehow exceeds the safeguard distance, instant recall overrides micro-tactics.

### Key Parameters

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Sample Radius | 10m | Tactical awareness range |
| Max Deviation (Attack) | 5m | Moderate autonomy |
| Max Deviation (Hold) | 3m | Tight control |
| Max Deviation (Retreat) | 8m | Survival priority |
| Stability Threshold | 15f deviation | Formation integrity gate |
| Casualty Threshold | 20% | Heavy losses gate |
| Active Ranks | 1-2 only | Middle ranks only |

### Acceptance Criteria

- [ ] Agent situation sampling detects allies/enemies within 10m
- [ ] Utility scoring produces normalized values (0-1) for each decision
- [ ] Best decision selected based on highest utility score
- [ ] Rank-based autonomy: front rank (0) gets NO micro-tactics
- [ ] Rank-based autonomy: middle ranks (1-2) get MICRO-TACTICS
- [ ] Rank-based autonomy: rear ranks (3+) get NO micro-tactics
- [ ] Formation stability gates block micro-tactics when formation scattered
- [ ] Formation stability gates block micro-tactics when casualties > 20%
- [ ] Dynamic radius adapts to objective (Hold: 3m, Attack: 5m, Retreat: 8m)
- [ ] Micro-movements constrained to autonomy radius
- [ ] Integration with formation organization: heavy troops still in front
- [ ] Integration with gap filling: middle ranks step up when front falls
- [ ] Logs show: "[BattleAI] Agent:X Rank:1 Decision:FlankLeft Score:0.72 Deviation:2.8m"

---

## 3.13 Battlefield Realism Systems

**Purpose:** Add depth and realism that makes battles feel like actual combat rather than abstract strategy.

### 3.12.1 Ammunition Tracking & Behavior

**Files:**
- `src/Features/Combat/AmmunitionTracker.cs`
- `src/Features/Combat/ArcherAmmoManager.cs`

**Tracking Per Agent:**
```csharp
public class AgentAmmoState
{
    public int CurrentAmmo { get; set; }
    public int MaxAmmo { get; set; }  // Based on quiver size
    public float AmmoRatio => (float)CurrentAmmo / MaxAmmo;
    public bool IsDepleted => CurrentAmmo <= 0;
    public bool IsLow => AmmoRatio < 0.25f;
}
```

**Formation Behavior Changes:**
| Ammo Level | Formation Behavior |
|------------|-------------------|
| > 50% | Normal archer behavior |
| 25-50% | Conserve ammo, selective targeting (high-value only) |
| < 25% | Formation commander notified, prepare melee transition |
| Depleted | Switch to melee weapons, rejoin as infantry |

**Formation-Level Response:**
- If 60%+ of archers depleted → formation switches to melee formation
- If 40%+ depleted but battle critical → hold position, melee ready
- Orchestrator notified when archer formation loses ranged capability

### 3.12.2 Line Relief & Rotation

**Files:**
- `src/Features/Combat/LineReliefManager.cs`
- `src/Features/Combat/FormationRotationCoordinator.cs`

**Rotation Triggers:**
- Front rank casualties > 40%
- Lull in combat (no melee for 15+ seconds)
- Orchestrator manual rotation order
- Extended engagement (> 60 seconds continuous melee)

**Rotation Sequence:**
```
1. Detect rotation opportunity (lull or exhaustion)
2. Front rank (0) takes 2 steps back
3. Middle rank (1) steps forward 2 steps
4. Ranks swap: old front becomes rank 1, old middle becomes rank 0
5. Brief formation integrity check (5 seconds stabilization)
6. Resume combat
```

**Benefits:**
- Front rank gets brief respite from direct combat
- Less-damaged troops engage enemy
- Maintains formation cohesion
- Psychological boost (help is coming)
- Extends formation endurance in prolonged battles

**Restrictions:**
- Only if formation > 40 troops (need depth)
- Not during active melee (must be lull)
- Not during charge or retreat orders
- Disabled if formation is main effort in critical moment

### 3.12.3 Morale Contagion

**Files:**
- `src/Features/Combat/MoraleContagionSystem.cs`

**Propagation Mechanics:**
```csharp
public class MoraleContagion
{
    private const float CONTAGION_RADIUS = 5f;
    
    public void PropagateEmotion(Agent source, EmotionType emotion, float intensity)
    {
        // Find nearby agents
        var nearbyAgents = GetAgentsInRadius(source, CONTAGION_RADIUS);
        
        foreach (var agent in nearbyAgents)
        {
            if (agent.Formation == source.Formation) // Same team
            {
                // Emotions spread more to allies
                ApplyMoraleChange(agent, emotion, intensity * 0.7f);
            }
        }
    }
}
```

**Emotion Types:**
| Emotion | Source | Effect |
|---------|--------|--------|
| **Fear** | Routing ally, seeing formation break | -10 morale to nearby |
| **Courage** | Rallying cry, officer present, victory moment | +10 morale to nearby |
| **Panic** | Surrounded, leader dead, 50%+ casualties | -20 morale to nearby (spreads fast) |
| **Confidence** | Winning melee, enemy routing, reinforcements arrive | +15 morale to nearby |

**Cascade Prevention:**
- Maximum 3 propagation hops (fear doesn't spread infinitely)
- Officers immune to fear contagion (steady presence)
- Formation morale has floor (won't drop below 10 from contagion alone)

### 3.12.4 Feint Maneuvers

**Files:**
- `src/Features/Combat/FeintManager.cs`
- `src/Features/Combat/TacticalDeceptionSystem.cs`

**When to Use Feints:**
- Enemy has large uncommitted reserve (> 30% of force)
- We have numerical advantage on a flank
- Enemy formation positioned to exploit our weakness
- Need to buy time for reinforcements

**Feint Execution:**
```
1. Select formation for feint (usually cavalry or mobile infantry)
2. Advance toward target (60% speed, loose formation)
3. Enemy responds (commits reserve or repositions)
4. Feinting formation retreats (planned withdrawal)
5. Actual main effort attacks weak point created
```

**Success Indicators:**
- Enemy reserve moves toward feint (> 50m from original position)
- Enemy formation repositions defensively
- Enemy cavalry commits to intercept feint

**Failure Handling:**
- If enemy doesn't react after 30 seconds → convert to real attack
- If enemy sees through feint (doesn't commit) → rejoin main force

### 3.12.5 Banner Rallying Points

**Files:**
- `src/Features/Combat/BannerRallySystem.cs`

**Banner Effects:**
- Agents within 15m of banner: +5 morale
- Scattered agents gravitate toward banner when reforming
- Banner bearer stays in formation center (rank 1, middle position)
- If bannerman killed, nearby sergeant/hero picks up (automatic transfer)

**Rally Behavior:**
```csharp
public WorldPosition GetRallyPoint(Agent agent)
{
    // If scattered and banner exists, move toward it
    if (agent.Formation.IsBannerActive)
    {
        var banner = agent.Formation.GetBannerBearer();
        if (banner != null && IsAgentScattered(agent))
        {
            return banner.Position; // Rally to banner
        }
    }
    
    // Else use formation center
    return agent.Formation.CurrentPosition;
}
```

**Banner Loss Impact:**
- -15 morale penalty to formation
- Formation becomes harder to rally (no visual focal point)
- Scattered troops take longer to reform

## 3.14 Coordinated Retreat System

**Purpose:** Execute organized withdrawal instead of piecemeal panic routs.

### Files
- `src/Features/Combat/WithdrawalManager.cs`
- `src/Features/Combat/RearguardManager.cs`

### Withdrawal Phases
| Phase | Behavior |
|-------|----------|
| **Covering** | Ranged/cavalry skirmish, infantry begins fallback |
| **Disengaging** | Main body breaks contact |
| **Retreating** | Full retreat, covering force follows |
| **Rallying** | Reform at rally point |

### Rearguard Selection
| Priority | Unit Type | Rationale |
|----------|-----------|-----------|
| 1 | Cavalry | Mobile, can disengage easily |
| 2 | Ranged | Harassment while retreating |
| 3 | Heavy infantry | Can hold briefly if needed |

### Step-by-Step Withdrawal
```
1. Rearguard holds position, main body moves 50m back
2. Main body stops, faces enemy
3. Rearguard breaks contact, moves through main body
4. Rearguard becomes new rear, main body moves 50m back
5. Repeat until safe distance or rally point
```

---

# 4. API Reference

## 4.1 Harmony Patch Targets

| Target | Purpose | Priority |
|--------|---------|----------|
| `TeamAIComponent.MakeDecision()` | Override tactic selection | High |
| `FormationAI.FindBestBehavior()` | Override behavior selection | High |
| `TacticComponent.GetTacticWeight()` | Adjust tactic weights | Medium |
| `BehaviorComponent.GetAiWeight()` | Adjust behavior weights | Medium |
| `BehaviorTacticalCharge.GetAiWeight()` | Smart charge decisions | High |
| `AgentStatCalculateModel.SetAiRelatedProperties()` | Agent tuning | High |

## 4.2 Useful Native APIs

| API | Purpose |
|-----|---------|
| `Team.QuerySystem` | Team-level power, composition |
| `Formation.QuerySystem` | Formation-level data |
| `Formation.AI.SetBehaviorWeight<T>(weight)` | Adjust behaviors |
| `Formation.SetMovementOrder()` | Issue movement orders |
| `Formation.TransferUnits()` | Split formations |
| `Formation.SwitchUnitLocations()` | Reorganize ranks |
| `Agent.AgentDrivenProperties` | Combat AI properties |
| `Agent.UpdateAgentProperties()` | Apply property changes |
| `Mission.GetFleePositionsForSide()` | Retreat destinations |

## 4.3 Query System Properties

### TeamQuerySystem
| Property | Description |
|----------|-------------|
| `MemberCount` / `EnemyUnitCount` | Unit counts |
| `InfantryRatio` / `RangedRatio` / `CavalryRatio` | Composition |
| `TeamPower` / `TotalPowerRatio` | Power metrics |
| `RemainingPowerRatio` | Current/Initial power |
| `GetLocalAllyPower(Vec2)` / `GetLocalEnemyPower(Vec2)` | Local power |

### FormationQuerySystem
| Property | Description |
|----------|-------------|
| `FormationPower` | Formation combat power |
| `LocalPowerRatio` | Local superiority |
| `ClosestEnemyFormation` | Nearest enemy |
| `UnderRangedAttackRatio` | Suppression level |
| `CasualtyRatio` | Losses percentage |

---

# 5. Configuration & Tuning

## 5.1 Config File

**Path:** `ModuleData/Enlisted/battle_ai_config.json`

```json
{
  "battleScaling": {
    "skirmishThreshold": 100,
    "smallThreshold": 200,
    "mediumThreshold": 350,
    "largeThreshold": 500,
    "reevaluateIntervalSec": 30.0,
    "scaleChangeHysteresis": 0.2
  },
  "orchestrator": {
    "decisionIntervalSec": 1.5,
    "strategyHysteresis": 1.3,
    "retreatThreshold": 0.4
  },
  "reserves": {
    "minBattleTimeBeforeCommit": 30,
    "casualtyCommitThreshold": 0.25,
    "defaultReserveRatio": 0.2
  },
  "cavalry": {
    "maxMeleeTime": 12,
    "minChargeDistance": 60,
    "reformDistance": 80,
    "reformDuration": 8
  },
  "agentProfiles": {
    "veteran": {
      "blockAbility": 0.92,
      "parryAbility": 0.85,
      "wrongBlockChance": 0.15
    },
    "elite": {
      "blockAbility": 0.98,
      "parryAbility": 0.95,
      "wrongBlockChance": 0.05
    }
  },
  "weaponDiscipline": {
    "pikeKeepDistance": 3.0,
    "pikeSwitchDistance": 1.5,
    "throwingStopDistance": 15
  },
  "stalemate": {
    "maxDurationSec": 45,
    "noActivityThresholdSec": 20
  },
  "agentMicroTactics": {
    "enabled": true,
    "sampleRadius": 10.0,
    "maxDeviationAttack": 5.0,
    "maxDeviationHold": 3.0,
    "maxDeviationRetreat": 8.0,
    "stabilityThreshold": 15.0,
    "casualtyThreshold": 0.2,
    "updateIntervalSec": 0.5,
    "maxAgentsPerFrame": 10
  }
}
```

## 5.2 Difficulty Scaling

| Difficulty | AI Level Cap | Property Modifier |
|------------|--------------|-------------------|
| Very Easy | 10% | Base × 0.7 |
| Easy | 21% | Base × 0.85 |
| Normal | 32% | Base × 1.0 |
| Hard | 64% | Base × 1.15 |
| Very Hard | 96% | Base × 1.3 |

---

# 6. Testing & Validation

## 6.1 Acceptance Criteria

### Enlisted-Only Activation (Critical)
- [ ] System completely disabled when player NOT enlisted
- [ ] No BattleOrchestratorBehavior added in non-enlisted battles
- [ ] No AgentStatCalculateModel override in non-enlisted battles
- [ ] Native AI unchanged when player is regular lord (pre-enlistment)
- [ ] Native AI unchanged when player is discharged (post-enlistment)
- [ ] Battles work correctly if player enlists/discharges mid-campaign

### Battle Scale Detection (Critical)
- [ ] Correctly detects Skirmish scale (< 100 per side avg)
- [ ] Correctly detects Small/Medium/Large/Massive scales
- [ ] Re-evaluates scale every 30 seconds (not every tick)
- [ ] Logs scale changes with troop counts
- [ ] Skirmish battles use 1-2 formations, simplified AI
- [ ] Massive battles (500+) use 5-8 formations, full AI features
- [ ] Line relief disabled in Skirmish/Small battles
- [ ] Feint maneuvers disabled in Skirmish/Small battles
- [ ] Agent micro-tactics sampling radius scales appropriately (5m→15m)
- [ ] Handles asymmetric battles (50 vs 500) without crashing
- [ ] Player changing battle size setting (200→1000) handled smoothly

### Orchestrator
- [ ] Outnumbered → hold/refuse instead of piecemeal charges
- [ ] Maintains reserve and commits appropriately
- [ ] Exploits local superiority (concentrates force)
- [ ] Withdraws in coordinated fashion
- [ ] Stable intent transitions with logged factors
- [ ] No flip-flopping (respects commitment windows)

### Agent Combat
- [ ] Veteran soldiers demonstrably more effective
- [ ] Context-aware adjustments visible (shield use under fire)
- [ ] Reduced mob behavior (enemies spread across multiple targets)
- [ ] Positional combat (front aggressive, rear defensive)

### Cavalry
- [ ] Lance charges execute with proper distance (60m+)
- [ ] Disengages after 10-15 seconds of melee
- [ ] Reforms before next charge
- [ ] 3-6 effective charges per battle vs native's 1
- [ ] Avoids charging braced spears/shield walls

### Weapon Discipline
- [ ] Pikes kept out until enemy inside 1.5m
- [ ] Multi-weapon soldiers use proper progression
- [ ] Throwing weapons not used in melee range
- [ ] Javelins conserved for melee if no other polearm

### Formation Doctrine
- [ ] Battle scale correctly detected (small/large)
- [ ] Formation count appropriate for army size
- [ ] Doctrines selected based on composition/terrain
- [ ] Troop quality balanced across formations

### Unit Formations
- [ ] Infantry switches to Shield Wall under archer fire
- [ ] Infantry forms Square when cavalry charges (80+ troops)
- [ ] Archers positioned correctly behind infantry
- [ ] Combined arms positioning maintained during advance

### Plan Execution
- [ ] Plans execute through proper phases
- [ ] No flip-flopping between offensive/defensive
- [ ] Catastrophic failures trigger emergency changes
- [ ] Enemy composition correctly recognized and countered

### Coordinated Retreat
- [ ] Organized withdrawal instead of piecemeal rout
- [ ] Rearguard designated and covers retreat
- [ ] Step-by-step bounds executed
- [ ] Rally point reached and formation reformed

### Performance
- [ ] No frame-time spikes in 500+ troop battles
- [ ] < 1ms per tick for orchestrator decisions
- [ ] Formation organization doesn't cause stuttering
- [ ] Agent micro-tactics sampling efficient (max 10 agents per frame)
- [ ] Situation sampling uses spatial caching (don't rescan every agent every frame)

## 6.2 Test Scenarios

### Enlisted Activation (Critical)
| Scenario | Expected Behavior |
|----------|-------------------|
| Battle before player enlists | Native AI only, no orchestrator |
| Battle while player enlisted (T1-T6) | Full Battle AI active |
| Battle while player is commander (T7+) | Full Battle AI active + Counter-AI |
| Battle after player discharged | Native AI only, no orchestrator |
| Player enlists mid-campaign | Battle AI activates from next battle |
| Player discharges mid-campaign | Battle AI deactivates from next battle |

### Core Orchestrator
| Scenario | Expected Behavior |
|----------|-------------------|
| 2:1 numerical advantage | Envelopment, cavalry flank |
| 1:2 numerical disadvantage | Refused flanks, fighting retreat |
| Cavalry vs exposed archers | Charge, destroy, reform |
| Cavalry vs spear screen | Hold, wait for opening |
| Player T7+ flanking | AI tracks, intercepts with reserve |
| High casualties (60%+) | Tactical withdrawal or last stand |
| Large battle reinforcements | Staged arrival, tactical integration |

### Formation Doctrine
| Scenario | Expected Behavior |
|----------|-------------------|
| 50 troops vs 50 | 2-3 formations (simple) |
| 400 troops vs 400 | 5-6 formations (three-wing or reserve) |
| Outnumbered 2:1 | Extended thin line to prevent flanking |
| 40%+ cavalry | Hammer-anvil doctrine selected |

### Plan Execution
| Scenario | Expected Behavior |
|----------|-------------------|
| Plan fails after 30s | Adapt (reinforce or shift axis) |
| Enemy changes stance | Wait 30s before responding |
| Rapid plan changes | Flip-flop detection locks changes |
| Phase transition | Plan changes allowed |
| Catastrophic failure | Emergency plan change allowed |

### Unit Type Formations
| Scenario | Expected Behavior |
|----------|-------------------|
| Infantry under archer fire | Shield Wall (if shields) or Loose |
| Cavalry charging infantry | Square formation (if 80+ troops) |
| Surrounded | Circle formation |
| Combined arms positioning | Archers 20-40m behind, cavalry on flanks |

### Weapon Discipline
| Scenario | Expected Behavior |
|----------|-------------------|
| Pike infantry vs approaching enemy | Pike out at 6m+, switch at <1.5m |
| Javelin + spear troops | Throw javelins 20m+, melee spear 3-15m, sword <1.5m |
| Pike formation breaking up | Immediate recall, tighten formation |

### Enemy Composition Recognition
| Enemy Type | Counter Response |
|------------|------------------|
| Infantry Horde | Standard line, let archers attrit |
| Archer Heavy | Shield wall advance, cavalry rush archers |
| Cavalry Heavy | Square/spear screen, protect archers |
| Horse Archers | Compact formation, wait for them to close |
| Pike Wall | Flank with cavalry, don't charge front |
| Shield Wall | Flank, or archer fire to soften |

### Coordinated Retreat
| Scenario | Expected Behavior |
|----------|-------------------|
| 40%+ casualties, can disengage | Tactical withdrawal toward spawn |
| 60%+ casualties, surrounded | Last stand formation |
| Retreat ordered | Rearguard covers, step-by-step fallback |
| Reached rally point | Reform and reassess |

## 6.3 Logging Requirements

All orchestrator decisions must be logged:
```
[BattleAI] Team:Defender Strategy:DelayDefend→Exploit (PowerRatio:1.2, EnemyFlankExposed:true)
[BattleAI] Cavalry:Formation3 State:Charging→Impact Target:EnemyArchers
[BattleAI] Reserve:Formation5 Commit:MainLineCollapsing (Casualties:0.27)
[BattleAI] Agent:X Rank:1 Decision:FlankLeft Score:0.72 Deviation:2.8m Situation:(Allies:3 Enemies:5)
[BattleAI] Formation:2 MicroTacticsDisabled Reason:FormationUnstable Deviation:18.2f
```

**Logging Levels:**
- **INFO:** Strategic decisions (orchestrator strategy changes, plan selection, reserve commits)
- **DEBUG:** Tactical decisions (formation objectives, cavalry state transitions, agent micro-decisions)
- **WARN:** Issues (formation instability blocking micro-tactics, position validation failures)
- **ERROR:** Critical failures (orchestrator null when expected, formation destroyed mid-operation)

---

# Implementation Dependency Graph

```
Phase 1: Foundation
    ├── 1.1 ENLISTED ACTIVATION GATE (blocks all if not enlisted)
    │       │
    ├── 1.2 MissionBehavior Entry (if enlisted)
    └── 1.3 Orchestrator Shell
            │
            ├── 1.4 Battle State Model ─────────────────┐
            └── 1.5 Agent Stat Model ─┐                 │
                                      │                 │
Phase 2: Agent Combat ◄───────────────┘                 │
    ├── 2.1 Context-Aware Tuning                        │
    ├── 2.2 Coordination Signals                        │
    └── 2.3/2.4 Profiles & Scaling                      │
                                                        │
Phase 3: Formation Intel ◄──────────────────────────────┤
    ├── 3.1 Smart Charge                                │
    ├── 3.5 Pike Discipline                             │
    └── 3.7 Self-Organizing Ranks                       │
                                                        │
Phase 4: Orchestrator Core ◄────────────────────────────┘
    ├── 4.1 OODA Loop
    ├── 4.2 Strategy Modes
    └── 4.4 Battle Phases
            │
            ├──────────────────────┐
            │                      │
Phase 5: Tactical Engine           Phase 6: Battle Plans ◄── Phase 15: Plan Execution
    ├── 5.3 Cavalry Timing         ├── 6.1 Plan Types         ├── 15.1 State Machine
    ├── 5.4 Cycle Charging         └── 6.3 Main Effort        └── 15.2 Anti-Flip-Flop
    └── 5.5 Reserve Logic                  │
            │                              │
            └──────────────┬───────────────┘
                           │
Phase 7: Reserve & Retreat ◄── Phase 17: Coordinated Retreat
    ├── 7.1 Reserve Manager      ├── 17.2 Rearguard
    └── 7.3 Organized Withdrawal └── 17.3 Step-by-Step
            │
Phase 8: Player Counter-AI (T7+ only) ◄
    ├── 8.1 Player Tracking
    └── 8.4 Counter-Composition
            │
            ├──────────────────────────────────────┐
            │                                      │
Phase 12: Formation Doctrine                Phase 13: Unit Type Formations
    ├── 12.1 Battle Scale                       ├── 13.1-13.3 Shapes
    ├── 12.3 Doctrines                          └── 13.5 Dynamic Switching
    └── 12.5 Counter-Formation                         │
            │                                          │
            └──────────────────────────────────────────┘
                           │
Phase 14: Formation Organization ◄
    ├── 14.1 Front-Line Scoring
    ├── 14.3 Gap Filling
    └── 14.5 Positional Combat
            │
Phase 9-11: Core Polish ◄
    ├── 9: Reinforcement Intelligence
    ├── 10: Battle Pacing & Cinematics
    └── 11: Terrain & Morale Exploitation
            │
Phase 16-18: Advanced Polish ◄
    ├── 16: Agent-Level Combat Director (Duels, Drama)
    └── 18: Reinforcement Details (Waves, Desperation)
```

---

# Implementation Summary

| Category | Phases | Work Items | Priority |
|----------|--------|------------|----------|
| **Foundation** | 1 | 10 (+3 tactical utils, +1 battle scale) | Critical |
| **Core Combat** | 2-5 | 33 (+9 tactical enhancements) | High |
| **Battle Planning** | 6, 15 | 13 | High |
| **Reserve/Retreat** | 7, 17 | 11 (+1 tactical withdrawal) | Medium |
| **Counter-AI** | 8 | 5 | Medium |
| **Formation Systems** | 12-14 | 19 | Medium |
| **Unit Formations** | 13 | 8 | Medium |
| **Reinforcements** | 9, 18 | 11 | Medium |
| **Polish/Drama** | 10-11, 16 | 15 (+7 agent micro-tactics) | Low |
| **Battlefield Realism** | 19 | 9 | Medium |
| **TOTAL** | **19 Phases** | **~137 Items** | — |

---

# Source Documents

- `docs/Features/Combat/battle-ai-plan.md` - Full design document (13,000+ lines, 21 parts)
- `docs/Features/Combat/tactical-formation-behavior-enhancement.md` - Formation tactical behaviors (1,128 lines)
- `docs/Features/Combat/advanced-tactical-behaviors.md` - Agent AI, cavalry, micro-positioning (588 lines)
- `docs/Features/Combat/agent-combat-ai.md` - Agent-level AI reference (486 lines)
- `docs/Features/Combat/formation-assignment.md` - Formation placement for enlisted soldiers
- `docs/Features/Combat/training-system.md` - Training and XP progression

---

# Coverage Matrix

This spec covers all 21 parts of the battle-ai-plan.md:

| Part | Topic | Phase(s) |
|------|-------|----------|
| 1 | Native AI Analysis | Architecture Overview, API Reference |
| 2 | Identified Gaps | (Context for design, not implementation) |
| 3 | Modern AI Techniques | (Techniques inform implementation) |
| 4 | Battle Orchestrator Proposal | Phase 4 |
| 5 | Tactical Enhancements | Phases 2, 3, 5 |
| 6 | Modding Entry Points | API Reference |
| 7 | Player Counter-Intelligence | Phase 8 |
| 8 | Dual Orchestrator Architecture | Architecture 1.2 |
| 9 | AI vs AI Battle Intelligence | Phases 4, 5 |
| 10 | Tactical Decision Engine | Phase 5 |
| 11 | Agent Formation Behavior | Phases 3, 7, 14 |
| 12 | Intelligent Formation Organization | Phase 14 |
| 13 | Formation Doctrine System | Phase 12 |
| 14 | Battle Plan Generation | Phases 6, 15 |
| 15 | Unit Type Formations | Phase 13 |
| 16 | Terrain Exploitation | Phase 11 |
| 17 | Morale Exploitation | Phase 11 |
| 18 | Coordinated Retreat | Phases 7, 17 |
| 19 | Battle Pacing and Cinematics | Phase 10 |
| 20 | Reinforcement Intelligence | Phases 9, 18 |
| 21 | Agent-Level Combat Director | Phase 16 |

---

# Design Philosophy Summary

This Battle AI system represents an original architecture designed specifically for the Enlisted mod. The key innovations are:

1. **Layered Intelligence:** Strategic (Orchestrator), Tactical (Formation), Micro (Agent) layers work together, each operating at the appropriate scope.

2. **Context Propagation:** The orchestrator provides battle plan and phase context to formations, which provide objective and priority context to agents. This ensures all decisions support the overall strategy.

3. **Bounded Autonomy:** Agents can make micro-decisions, but only when appropriate (middle ranks, stable formation, not main effort) and always within constrained bounds. This creates realistic behavior without chaos.

4. **Integration Over Replacement:** We enhance native AI systems rather than replacing them entirely. The orchestrator nudges behavior weights and provides context, formations respect native movement and arrangement systems, agents adjust within formation positions.

5. **Battlefield Realism:** Troops aren't perfect machines - they run out of arrows, need rotation during prolonged engagements, spread fear/courage to nearby comrades, and react to the chaos of combat. Formations adapt to ammunition depletion, line casualties, and morale contagion.

6. **Cinematic Moments:** Beyond pure tactics, the system creates dramatic battlefield moments - feint maneuvers that draw enemy forces, line relief rotations during lulls, banner rallies for scattered troops, and breaking points where formations crack under pressure.

7. **Enlisted-Only Activation:** The system only runs when the player is enlisted, ensuring the mod enhances the enlisted experience without affecting normal Bannerlord gameplay.

This architecture solves the common AI problems of either too much top-down control (robotic behavior) or too much bottom-up autonomy (incoherent tactics) by providing appropriate intelligence at each layer while maintaining coordination. The realism systems ensure battles feel like actual combat - dynamic, resource-constrained, and full of human elements like ammunition depletion, line relief needs, fear, and courage.

---

**Last Updated:** 2025-12-31  
**Status:** Specification Complete (137 items across 19 phases, API verified), Implementation Not Started
