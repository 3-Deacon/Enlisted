# Battle AI Implementation Prompts

**Summary:** Copy-paste prompts for each implementation phase of the Battle AI system. Each prompt is designed for a NEW AI chat session with full context recovery.

**Status:** 📋 Reference | **Updated:** 2025-12-31 (Agent Micro-Tactics System Added)

**Design Note:** This Battle AI system is an original architecture designed for the Enlisted mod, featuring a layered approach (Orchestrator → Formation → Agent) where each layer operates at the appropriate scope. The orchestrator provides strategic coordination, formations execute tactical roles, and individual agents make bounded micro-decisions that support the overall plan. Battlefield realism systems (ammunition tracking, line relief, morale contagion, feints) add depth and create cinematic moments. This architecture creates intelligent, coordinated behavior without the chaos of pure bottom-up autonomy or the rigidity of pure top-down control.
**Related Docs:** 
- [BATTLE-AI-IMPLEMENTATION-SPEC.md](BATTLE-AI-IMPLEMENTATION-SPEC.md) - Master implementation spec (all phases, edge cases, work items)
- [battle-ai-plan.md](battle-ai-plan.md) - Design foundation and tactical details
- [advanced-tactical-behaviors.md](advanced-tactical-behaviors.md) - Agent AI, cavalry, micro-positioning specs
- [BLUEPRINT](../../BLUEPRINT.md) - Project constraints and verification requirements

---

## How to Use These Prompts

Each phase prompt is **self-contained for a fresh AI chat**. Every prompt includes:
1. **Prerequisites** - What must exist before starting
2. **Context Recovery** - How the new AI verifies previous work
3. **Tasks** - What to implement
4. **Edge Cases** - Critical scenarios to handle
5. **Acceptance Criteria** - Definition of done
6. **Handoff Notes Template** - What to capture for the next AI

**Start each new chat by copying the entire prompt block (inside the triple backticks).**

---

## Index

| Phase | Description | Model | Chat Strategy | Status |
|-------|-------------|-------|---------------|--------|
| [Phase 1](#phase-1-foundation) | Foundation (activation gate, hooks) | Opus 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 2](#phase-2-agent-combat-enhancements) | Agent Combat AI tuning | Opus 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 3](#phase-3-formation-intelligence) | Formation-level tactics | Opus 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 4](#phase-4-battle-orchestrator-core) | Orchestrator OODA loop | Opus 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 5](#phase-5-tactical-decision-engine) | Cavalry, reserves, targeting | Opus 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 6](#phase-6-battle-plan-generation) | Plan types, selection | Opus 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 7](#phase-7-reserve--retreat-management) | Reserve & retreat | Sonnet 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 8](#phase-8-player-counter-intelligence-t7) | T7+ counter-AI | Opus 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 9-11](#phases-9-11-polish-combined) | Reinforcements, pacing, terrain | Sonnet 4 | ⚡ COMBINE | ⬜ TODO |
| [Phase 12-14](#phases-12-14-formation-systems-combined) | Formation doctrine, shapes, organization | Opus 4 | ⚡ COMBINE | ⬜ TODO |
| [Phase 15](#phase-15-plan-execution--anti-flip-flop) | State machine, anti-flip-flop | Opus 4 | 🔒 Standalone | ⬜ TODO |
| [Phase 16-18](#phases-16-18-advanced-polish-combined) | Agent micro-tactics, combat director, retreat, reinforcement | Opus 4 | ⚡ COMBINE | ⬜ TODO |
| [Phase 19](#phase-19-battlefield-realism-enhancements) | Ammo tracking, line relief, morale contagion, feints, banners | Opus 4 | 🔒 Standalone | ⬜ TODO |

---

## Chat Strategy Recommendations

| Strategy | Phases | Rationale |
|----------|--------|-----------|
| **STANDALONE** | 1, 2, 3, 4 | Core foundation; each phase builds on previous |
| **STANDALONE** | 5, 6 | Complex tactical logic; needs focus |
| **STANDALONE** | 7 | Reserve/retreat is self-contained |
| **STANDALONE** | 8 | Player counter-AI is specialized |
| **COMBINE** | 9-11 | Polish phases; natural continuation |
| **COMBINE** | 12-14 | Formation systems; tightly coupled |
| **STANDALONE** | 15 | State machine is complex |
| **COMBINE** | 16-18 | Advanced polish; related systems |

---

## Model Recommendations

| Phase | Complexity | Model | Est. Time | Rationale |
|-------|------------|-------|-----------|-----------|
| Phase 1 - Foundation | Medium | Claude Opus 4 | 2-3 hours | API hookups, game integration |
| Phase 2 - Agent Combat | High | Claude Opus 4 | 3-4 hours | 40+ properties, profiles |
| Phase 3 - Formation Intel | High | Claude Opus 4 | 3-4 hours | Weapon discipline, self-org |
| Phase 4 - Orchestrator | High | Claude Opus 4 | 4-5 hours | OODA loop, strategy modes |
| Phase 5 - Tactical Engine | High | Claude Opus 4 | 4-5 hours | Cavalry cycles, reserves |
| Phase 6 - Battle Plans | High | Claude Opus 4 | 3-4 hours | Plan generation/selection |
| Phase 7 - Reserve/Retreat | Medium | Claude Sonnet 4 | 2-3 hours | Straightforward logic |
| Phase 8 - Counter-AI | High | Claude Opus 4 | 3-4 hours | Player prediction |
| Phases 9-11 | Medium | Claude Sonnet 4 | 3-4 hours | Polish systems |
| Phases 12-14 | High | Claude Opus 4 | 4-5 hours | Formation doctrine |
| Phase 15 | High | Claude Opus 4 | 3-4 hours | State machine |
| Phases 16-18 | High | Claude Opus 4 | 5-7 hours | Agent micro-tactics + polish |
| Phase 19 | High | Claude Opus 4 | 3-4 hours | Battlefield realism (ammo, line relief, morale, feints, high ground) |

**Total estimated time:** ~49-63 hours (including agent micro-tactics, tactical enhancements, and battlefield realism)

---

## Quick Reference: Build & Test

```powershell
# Build command (PowerShell)
cd C:\Dev\Enlisted\Enlisted; dotnet build -c "Enlisted RETAIL" /p:Platform=x64

# Logs output to:
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\

# Decompile reference (API verification):
C:\Dev\Enlisted\Decompile\
```

## ⚠️ CRITICAL: Optional SubModule Architecture

**Battle AI is implemented as a separate, optional SubModule that users can disable in the Bannerlord launcher.**

**Where to put Battle AI code:**
- ✅ Entry point: `src/Features/Combat/BattleAI/BattleAISubModule.cs`
- ✅ All initialization in `BattleAISubModule` class
- ✅ All mission behavior registration in `BattleAISubModule`
- ✅ Implementation files in `src/Features/Combat/BattleAI/` subfolders

**Where NOT to put Battle AI code:**
- ❌ NEVER in Core SubModule (`src/Mod.Entry/SubModule.cs`)
- ❌ NEVER reference Battle AI from Core SubModule
- ❌ Core mod must function completely without Battle AI

**Why this matters:** When users uncheck "Enlisted Battle AI" in the launcher, `BattleAISubModule` never loads. If Core SubModule references Battle AI, it either crashes OR prevents disabling it.

**SubModule.xml Structure:**
```xml
<SubModules>
    <!-- Core SubModule (Required) -->
    <SubModule>
        <Name value="Enlisted Core"/>
        <SubModuleClassType value="Enlisted.Mod.Entry.SubModule"/>
    </SubModule>
    
    <!-- Battle AI SubModule (Optional) -->
    <SubModule>
        <Name value="Enlisted Battle AI"/>
        <SubModuleClassType value="Enlisted.Features.Combat.BattleAI.BattleAISubModule"/>
    </SubModule>
</SubModules>
```

**See [BLUEPRINT.md - Critical Battle AI Rules](../../BLUEPRINT.md#critical-battle-ai-rules) for complete documentation.**

---

## Critical Project Constraints

Include these in EVERY prompt:

```
═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET:
- Bannerlord v1.3.13 specifically (not latest version)

API VERIFICATION:
- ALWAYS verify against local decompile FIRST: C:\Dev\Enlisted\Decompile\
- Key assemblies: TaleWorlds.MountAndBlade, TaleWorlds.CampaignSystem, SandBox
- Never rely on external AI docs or outdated API references

PROJECT BUILD:
- Old-style .csproj: Must manually add new .cs files with <Compile Include="..."/>
- Build command: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
- Output: <Bannerlord>/Modules/Enlisted/bin/Win64_Shipping_Client/

LOGGING:
- Use ModLogger class (not Console.WriteLine or Debug.Log)
- Categories: Combat, BattleAI, Formation, etc.
- Output: Modules\Enlisted\Debugging\ (in Bannerlord install folder)
- Format: ModLogger.Info("BattleAI", "message"); ModLogger.Debug/Warn/Error

BATTLE AI ARCHITECTURE (⚠️ CRITICAL):
- Battle AI is an OPTIONAL SUBMODULE users can disable in Bannerlord launcher
- Entry point: src/Features/Combat/BattleAI/BattleAISubModule.cs
- ALL Battle AI initialization MUST happen in BattleAISubModule class
- NEVER reference Battle AI from Core SubModule (src/Mod.Entry/SubModule.cs)
- Core mod MUST function completely without Battle AI
- When SubModule disabled, zero Battle AI code executes (no performance cost)

SubModule.xml has TWO entries:
  <SubModule>
    <Name value="Enlisted Core"/>
    <SubModuleClassType value="Enlisted.Mod.Entry.SubModule"/>
  </SubModule>
  <SubModule>
    <Name value="Enlisted Battle AI"/>
    <SubModuleClassType value="Enlisted.Features.Combat.BattleAI.BattleAISubModule"/>
  </SubModule>

ENLISTED-ONLY ACTIVATION:
- All Battle AI ONLY runs when player is enlisted
- Check: EnlistmentState.IsEnlisted before any AI logic
- If not enlisted, native AI runs unmodified

CODE QUALITY (RESHARPER + REPO LINT):
- Follow ReSharper/Rider and the repo lint stack recommendations as strictly as possible
- Fix ALL warnings before considering code complete
- Never suppress warnings with pragmas unless truly necessary
- If suppressing is unavoidable, use [SuppressMessage] with clear Justification
- Common issues to proactively fix:
  - Unused using directives → remove them
  - Redundant namespace qualifiers → use using statements
  - Unused variables/parameters/methods → remove them
  - Redundant default parameter values → omit them
  - PossibleNullReferenceException → add null checks
  - Single-line statements without braces → add braces

COMMENT STYLE:
- Describe WHAT the code does NOW, not when it changed
- NO "Phase X added..." or changelog-style comments
- NO "legacy" or "migration" mentions
- Write professionally and naturally

BRACES:
- ALWAYS use braces for if/for/while/foreach, even single lines
- ✅ if (x) { return y; }
- ❌ if (x) return y;

COMMON PITFALLS TO AVOID:
1. Not adding new files to .csproj → files won't compile
2. Using external API docs instead of decompile → wrong APIs
3. Ignoring ReSharper/repo lint warnings → code quality degrades, PR rejected
4. Missing null checks → crashes
5. Using ChangeHeroGold instead of GiveGoldAction → gold UI breaks
6. Iterating equipment with Enum.GetValues → includes invalid count values
7. Leaving unused code → the repo lint stack flags it, wastes maintenance effort
8. Suppressing warnings with #pragma → User prefers fixing over suppressing

SAVE SYSTEM (if adding persistent state):
- Register new classes in EnlistedSaveDefiner
- Use SaveLoadDiagnostics.SafeSyncData() wrapper
- See src/Mod.Core/SaveSystem/ for examples
```

---

## Key Patterns & Managers

New AI chats should understand these existing systems:

```
EXISTING MANAGERS (use these, don't recreate):
- EnlistmentBehavior.Instance - Enlisted state, lord reference, IsEnlisted
- CompanyNeedsManager.Instance - Supplies, Morale, Rest, Readiness
- EscalationManager.Instance - Discipline, Scrutiny, Officer/Lord/Soldier rep
- ConfigurationManager.Instance - JSON config loading

EXISTING UTILITIES:
- ModLogger - Logging (Info, Debug, Warn, Error)
- NextFrameDispatcher.RunNextFrame() - Deferred operations
- CampaignSafetyGuard.SafeMainHero - Null-safe hero access

FOLDER STRUCTURE FOR BATTLE AI FILES:
- src/Features/Combat/BattleAI/BattleAISubModule.cs - Entry point (required!)
- src/Features/Combat/BattleAI/Behaviors/ - Mission behaviors
- src/Features/Combat/BattleAI/Orchestration/ - Strategic AI
- src/Features/Combat/BattleAI/Formation/ - Formation AI
- src/Features/Combat/BattleAI/Agent/ - Agent AI
- src/Features/Combat/BattleAI/Models/ - Data models
- All Battle AI code uses #if BATTLE_AI wrapper
- Keep related functionality together

FILE NAMING:
- Use PascalCase for files: BattleOrchestrator.cs, CavalryCycleManager.cs
- Models/enums in Models subfolder: Models/StrategyMode.cs

HARMONY PATCHES (if needed):
- Place in src/Mod.GameAdapters/
- Use [HarmonyPatch(typeof(TargetClass), "MethodName")]
- Prefer Postfix over Prefix when possible
- Log patch application for debugging
```

---

# Phase 1: Foundation

**Goal:** Build core infrastructure - activation gate, MissionBehavior hook, basic orchestrator shell, config loading

**Status:** ⬜ TODO

**Prerequisites:** None (this is the first phase)

```
I need you to implement Phase 1 (Foundation) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md - READ CAREFULLY)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET:
- Bannerlord v1.3.13 specifically (not latest version)

API VERIFICATION:
- ALWAYS verify against local decompile FIRST: C:\Dev\Enlisted\Decompile\
- Key assemblies: TaleWorlds.MountAndBlade, TaleWorlds.CampaignSystem, SandBox
- Never rely on external AI docs or outdated API references

PROJECT BUILD:
- Old-style .csproj: Must manually add new .cs files with <Compile Include="..."/>
- Build command: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
- Output: <Bannerlord>/Modules/Enlisted/bin/Win64_Shipping_Client/

LOGGING:
- Use ModLogger class (NOT Console.WriteLine)
- Format: ModLogger.Info("BattleAI", "message");
- Categories: BattleAI, Combat, Formation
- Output: Modules\Enlisted\Debugging\ (Bannerlord install folder)

ENLISTED-ONLY:
- All Battle AI ONLY runs when player is enlisted
- Check: EnlistmentBehavior.Instance.IsEnlisted before any AI logic
- If not enlisted, native AI runs unmodified

CODE QUALITY (RESHARPER + REPO LINT - CRITICAL):
- Follow ReSharper/repo lint recommendations as strictly as possible
- Fix ALL warnings before considering code complete (don't suppress with pragmas)
- ALWAYS use braces for if/for/while/foreach, even single lines
- Comments describe WHAT code does NOW (no changelog style)
- Remove: unused usings, unused variables, unused methods, redundant qualifiers
- Add null checks where needed (avoid PossibleNullReferenceException)

EXISTING MANAGERS TO USE:
- EnlistmentBehavior.Instance - Enlisted state, lord reference
- ConfigurationManager.Instance - JSON config loading
- ModLogger - Logging

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ FIRST
═══════════════════════════════════════════════════════════════════════════════

Read these docs BEFORE implementing:

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 1: Architecture Overview (layer hierarchy, dual orchestrator, activation gate)
   - Section 2: Phase 1 items (1.1-1.6)
   - Section 2b: Edge Cases for Phase 1

2. docs/Features/Combat/battle-ai-plan.md
   - Part 6: Modding Entry Points (MissionBehavior, Harmony targets, useful APIs)
   - Part 1: Native AI Analysis (understand what exists)

3. docs/Features/Combat/agent-combat-ai.md
   - AgentStatCalculateModel section (how to override agent stats)

4. docs/BLUEPRINT.md
   - Quick Orientation (project constraints)
   - Common Tasks (adding files, logging)

5. DECOMPILE REFERENCE (verify all APIs exist):
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Mission.cs
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\MissionBehavior.cs
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Team.cs
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\TeamQuerySystem.cs

═══════════════════════════════════════════════════════════════════════════════
PHASE 1 TASKS
═══════════════════════════════════════════════════════════════════════════════

⚠️⚠️⚠️ CRITICAL: BATTLE AI SUBMODULE ARCHITECTURE ⚠️⚠️⚠️

ALL Battle AI code MUST be:
- Created in src/Features/Combat/BattleAI/ folder structure
- Wrapped in #if BATTLE_AI ... #endif
- Registered through BattleAISubModule.cs (NOT Core SubModule!)
- Added to Enlisted.csproj with <Compile Include="..."/>

NEVER put Battle AI code in:
- src/Mod.Entry/SubModule.cs (Core SubModule)
- src/Features/Combat/ (outside BattleAI folder)

BattleAISubModule.cs already exists at: src/Features/Combat/BattleAI/BattleAISubModule.cs
Use it as entry point for ALL Battle AI initialization!

═══════════════════════════════════════════════════════════════════════════════

1.1 ENLISTED ACTIVATION GATE
- Create utility method: ShouldActivateBattleAI()
- Check EnlistmentState.IsEnlisted (use existing EnlistmentBehavior.Instance)
- Check Mission.Current is not null
- Check not multiplayer (Mission.Current.IsMultiplayer)
- Check field battle (not siege/arena)
- Return false if ANY check fails

1.2 MISSIONBEHAVIOR ENTRY POINT
- Create src/Features/Combat/BattleAI/Behaviors/EnlistedBattleAIBehavior.cs
- Inherit from MissionBehavior
- Override OnBehaviorInitialize() - create orchestrators IF activation gate passes
- Override OnMissionTick(float dt) - tick orchestrators
- Override OnMissionEnded() - cleanup
- Register behavior in BattleAISubModule (src/Features/Combat/BattleAI/BattleAISubModule.cs)
  - NOT in Core SubModule! Use Mission.Current.AddMissionBehavior() from BattleAISubModule
  - Hook into Mission creation callback in BattleAISubModule.OnBeforeInitialModuleScreenSetAsRoot

1.3 BASIC ORCHESTRATOR SHELL
- Create src/Features/Combat/BattleAI/Orchestration/BattleOrchestrator.cs
- One orchestrator per team (PlayerTeam + EnemyTeam)
- Store reference to Team
- Stub methods: Observe(), Orient(), Decide(), Act()
- Simple tick method that logs it's running

1.4 BATTLE STATE MODEL
- Create src/Features/Combat/BattleAI/Models/BattleState.cs
- Read-only data from Team.QuerySystem:
  - PowerRatio (ours vs theirs)
  - CavalryRatio
  - InfantryRatio
  - RangedRatio
  - TotalAlive, TotalCasualties
- Create helper: BattleStateReader.CaptureState(Team team)

1.5 AGENT STAT MODEL OVERRIDE
- Create src/Features/Combat/BattleAI/Models/EnlistedAgentStatCalculateModel.cs
- Inherit from SandboxAgentStatCalculateModel
- Override InitializeAgentStats() - call base, then apply enlisted modifiers
- Only apply modifiers if activation gate passes
- Register via GameModelsContext in BattleAISubModule.OnGameStart

1.6 CONFIGURATION LOADING
- Add "battle_ai" section to ModuleData/Enlisted/enlisted_config.json
- Include:
  * enabled (bool), log_decisions (bool), decision_interval_sec (float)
  * battleScaling sub-section (1.10): thresholds, reevaluateIntervalSec, hysteresis
- Load via existing ConfigurationManager
- Default: enabled=true, log_decisions=true, decision_interval_sec=1.5
- See 1.10 above for full battleScaling JSON structure

1.7 ACTIVITY DETECTION UTILITIES (NEW - TACTICAL ENHANCEMENT)
- Create src/Features/Combat/TacticalUtilities.cs
- IsFormationShooting(formation, threshold) - checks LastRangedAttackTime
- FormationFightingInMelee(formation, threshold) - checks LastMeleeAttackTime
- FormationActiveSkirmishersRatio(formation) - percentage actively shooting

1.8 BATTLE JOINED DETECTION WITH HYSTERESIS (NEW - TACTICAL ENHANCEMENT)
- HasBattleBeenJoined(mainInfantry, currentlyJoined, joinDistance)
- Uses +5m buffer to prevent flip-flopping (75m to join, 80m to un-join)
- Critical for cavalry timing and battle phase detection

1.9 FORMATION STATE TRACKING (NEW - TACTICAL ENHANCEMENT)
- Track formation states: Idle, Moving, Shooting, InMelee, Retreating
- Used by orchestrator for tactical decisions

1.10 BATTLE SCALE DETECTION (CRITICAL - NEW)
- Detect battle size: Skirmish (<100), Small (100-200), Medium (200-350), Large (350-500), Massive (500+)
- Calculate: avgTroops = (ourTroops + estimatedReinforcements + enemyTroops + enemyReinforcements) / 2
- Re-evaluate every 30 seconds (not every tick)
- Use hysteresis (20% threshold) to prevent flip-flop
- Scale AI complexity based on detection:
  * Skirmish: 1-2 formations, 1-2 ranks, 10% reserve, 5m sample radius, 2.0s tick
  * Small: 2-3 formations, 2-3 ranks, 15% reserve, 8m sample radius, 1.5s tick
  * Medium: 3-4 formations, 2-4 ranks, 20% reserve, 10m sample radius, 1.0s tick
  * Large: 4-6 formations, 3-5 ranks, 25% reserve, 12m sample radius, 1.0s tick
  * Massive: 5-8 formations, 4-6 ranks, 30% reserve, 15m sample radius, 0.8s tick
- Disable features for small battles:
  * Line Relief: OFF for Skirmish, ON for Small+ (if formation > 40 troops)
  * Feint Maneuvers: OFF for Skirmish/Small, ON for Medium+
- Store in BattleContext, accessed by all systems
- Log: "[BattleAI] Scale: Medium (avg 280 troops, formations: 4, maxRanks: 3)"

BATTLE SCALE CONFIG (add to battle_ai_config.json):
```json
"battleScaling": {
  "skirmishThreshold": 100,
  "smallThreshold": 200,
  "mediumThreshold": 350,
  "largeThreshold": 500,
  "reevaluateIntervalSec": 30.0,
  "scaleChangeHysteresis": 0.2
}
```

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES TO HANDLE
═══════════════════════════════════════════════════════════════════════════════

From BATTLE-AI-IMPLEMENTATION-SPEC.md Section 2b:

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Mission.Current is null | Guard all entry points, abort initialization |
| Enlisted state changes during battle load | Re-check before orchestrator tick starts |
| Multiple missions loading in sequence | Clean up previous orchestrator on mission end |
| Mission ends before orchestrator initializes | Register cleanup in OnMissionEnded |
| Configuration file missing/corrupt | Use hardcoded defaults, log warning |
| Multiplayer battle detected | Activation gate returns false, skip entirely |
| Save/load during battle | Handle via OnAfterMissionLoad hook |
| Team is null or has no agents | Early exit from orchestrator creation |
| **Battle size very low (< 50 per side)** | Detect Skirmish, disable advanced features (line relief, feints) |
| **Battle size changes mid-battle (reinforcements)** | Re-evaluate scale every 30 sec, smooth transition, don't reset AI |
| **Asymmetric battles (100 vs 500)** | Use average for scale detection, adjust per-side formations |
| **Player sets battle size to 1000** | Detect Massive, cap tick frequency at 0.8s minimum |

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE (ALL IN BattleAI FOLDER!)
═══════════════════════════════════════════════════════════════════════════════

BattleAISubModule.cs already exists - MODIFY IT to add mission behavior registration!

1. src/Features/Combat/BattleAI/Behaviors/EnlistedBattleAIBehavior.cs
2. src/Features/Combat/BattleAI/Orchestration/BattleOrchestrator.cs
3. src/Features/Combat/BattleAI/Models/BattleState.cs
4. src/Features/Combat/BattleAI/Orchestration/BattleStateReader.cs
5. src/Features/Combat/BattleAI/Models/EnlistedAgentStatCalculateModel.cs
6. src/Features/Combat/BattleAI/Orchestration/BattleScaleDetector.cs (NEW - handles 1.10)
7. src/Features/Combat/BattleAI/Models/BattleScale.cs (enum: Skirmish, Small, Medium, Large, Massive)
8. src/Features/Combat/BattleAI/Models/BattleScaleConfig.cs (scale-specific parameters)
9. src/Features/Combat/BattleAI/Orchestration/TacticalUtilities.cs (1.7-1.9 utilities)

REMEMBER: 
- Wrap ALL files in #if BATTLE_AI ... #endif
- Add all new .cs files to Enlisted.csproj with <Compile Include="..."/>

═══════════════════════════════════════════════════════════════════════════════
EXISTING FILES TO MODIFY
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/BattleAI/BattleAISubModule.cs (NOT Core SubModule!)
   - Add EnlistedBattleAIBehavior registration in OnBeforeInitialModuleScreenSetAsRoot
   - Hook into Mission.MissionBehaviorInitializeEvent or similar
   - Add EnlistedAgentStatCalculateModel registration in OnGameStart

2. ModuleData/Enlisted/enlisted_config.json
   - Add "battle_ai" configuration section

3. src/Mod.Core/Config/ConfigurationManager.cs
   - Add BattleAIConfig class and property

4. Enlisted.csproj
   - Add <Compile Include="..."/> for all new files

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

CRITICAL ARCHITECTURE CHECKS:
[ ] ALL Battle AI files are in src/Features/Combat/BattleAI/ folder
[ ] ALL Battle AI files wrapped in #if BATTLE_AI ... #endif
[ ] NO Battle AI references in Core SubModule (src/Mod.Entry/SubModule.cs)
[ ] ALL registration happens through BattleAISubModule.cs
[ ] Core mod still functions without Battle AI SubModule enabled

FUNCTIONALITY CHECKS:
[ ] Activation gate returns false when player not enlisted
[ ] Activation gate returns true when player is enlisted in field battle
[ ] EnlistedBattleAIBehavior added to mission when enlisted
[ ] BattleOrchestrator created for each team (PlayerTeam, EnemyTeam)
[ ] Orchestrator ticks log every ~1.5 seconds: "[BattleAI] Team:X Tick"
[ ] BattleState correctly reads power ratios from TeamQuerySystem
[ ] EnlistedAgentStatCalculateModel registered and base stats unchanged
[ ] Config loads from JSON, falls back to defaults if missing
[ ] All new files added to Enlisted.csproj with <Compile Include="..."/>
[ ] Build succeeds: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
[ ] No crashes when entering battle while enlisted
[ ] No crashes when entering battle while NOT enlisted (native AI only)
[ ] Battle scale correctly detected on initialization (log shows scale and troop count)
[ ] Skirmish scale (< 100) uses 1-2 formations, simplified AI
[ ] Medium scale (200-350) uses 3-4 formations, full features
[ ] Massive scale (500+) uses 5-8 formations, reduced tick frequency (0.8s)
[ ] Scale re-evaluates every 30 seconds (not every tick)
[ ] Hysteresis prevents flip-flop (requires 20% change to shift scale)
[ ] Asymmetric battles (50 vs 500) handled without crashing
[ ] Line relief disabled for Skirmish/Small scales
[ ] Feint maneuvers disabled for Skirmish/Small scales

═══════════════════════════════════════════════════════════════════════════════
HANDOFF NOTES (Capture these for Phase 2)
═══════════════════════════════════════════════════════════════════════════════

When complete, provide these handoff notes for the next AI session:

FILES CREATED:
- [ ] src/Features/Combat/BattleOrchestratorBehavior.cs
- [ ] src/Features/Combat/BattleOrchestrator.cs
- [ ] src/Features/Combat/Models/BattleState.cs
- [ ] src/Features/Combat/BattleStateReader.cs
- [ ] src/Features/Combat/EnlistedAgentStatCalculateModel.cs
- [ ] src/Features/Combat/BattleScaleDetector.cs
- [ ] src/Features/Combat/Models/BattleScale.cs (enum)
- [ ] src/Features/Combat/Models/BattleScaleConfig.cs
- [ ] src/Features/Combat/TacticalUtilities.cs

FILES MODIFIED:
- [ ] src/Mod.Entry/SubModule.cs
- [ ] ModuleData/Enlisted/enlisted_config.json
- [ ] src/Mod.Core/Config/ConfigurationManager.cs
- [ ] Enlisted.csproj

KEY APIS VERIFIED IN DECOMPILE:
- (list which APIs you verified exist)

KEY DECISIONS MADE:
- Battle scale detection method (troop count averaging, reinforcement estimation)
- Hysteresis threshold for scale changes (20% to prevent flip-flop)
- Scale re-evaluation interval (30 seconds)
- Feature enable/disable thresholds per scale
- (list any other architectural decisions)

BATTLE SCALE CONTEXT FOR NEXT PHASE:
- BattleScale enum and detector are now available in BattleContext/BattleState
- All phases should respect battle scale when making decisions
- Phase 12 will use scale for formation count/depth
- Phase 16.6 will use scale for agent sampling radius
- Phase 19 will check scale to enable/disable line relief and feints

KNOWN ISSUES/TECH DEBT:
- (list any incomplete items)

VERIFICATION PASSED:
[ ] Enlisted player enters battle - orchestrator logs appear
[ ] Non-enlisted player enters battle - no orchestrator logs
[ ] Build succeeds
[ ] No runtime errors
```

---

# Phase 2: Agent Combat Enhancements

**Goal:** Implement context-aware agent property tuning, AI profiles, and coordination signals

**Status:** ⬜ TODO

**Prerequisites:** Phase 1 complete (EnlistedAgentStatCalculateModel exists)

```
I need you to implement Phase 2 (Agent Combat Enhancements) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13

API VERIFICATION:
- ALWAYS verify against local decompile: C:\Dev\Enlisted\Decompile\
- Check: TaleWorlds.MountAndBlade\AgentDrivenProperties.cs for actual property names

PROJECT BUILD:
- Old-style .csproj: Manually add new files with <Compile Include="..."/>
- Build: dotnet build -c "Enlisted RETAIL" /p:Platform=x64

LOGGING: Use ModLogger.Info("BattleAI", "message");

ENLISTED-ONLY: Check EnlistmentBehavior.Instance.IsEnlisted before AI logic

CODE QUALITY (RESHARPER + REPO LINT):
- Follow ReSharper/repo lint - fix ALL warnings, don't suppress
- ALWAYS use braces for if/for/while/foreach
- Comments describe current behavior (no changelog style)
- Remove unused code, add null checks

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1 exists before starting)
═══════════════════════════════════════════════════════════════════════════════

Phase 1 is COMPLETE. Before implementing Phase 2, verify these files exist:

Phase 1 Files (MUST exist - read them to understand current implementation):
[ ] src/Features/Combat/BattleOrchestratorBehavior.cs - MissionBehavior with activation gate
[ ] src/Features/Combat/BattleOrchestrator.cs - Per-team orchestrator shell
[ ] src/Features/Combat/Models/BattleState.cs - Read-only battle data
[ ] src/Features/Combat/BattleStateReader.cs - Captures state from TeamQuerySystem
[ ] src/Features/Combat/EnlistedAgentStatCalculateModel.cs - Agent stat override (empty)

Verify Phase 1 is working:
1. Check logs for "[BattleAI] Team:X Tick" entries during battle
2. Verify BattleOrchestrator exists for both teams
3. Verify EnlistedAgentStatCalculateModel is registered

If any Phase 1 files are missing, they need to be created first (see Phase 1 prompt).

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ FIRST
═══════════════════════════════════════════════════════════════════════════════

Read these docs BEFORE implementing:

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 2 items (2.1-2.5 - includes NEW tactical enhancements)
   - Section 2b: Edge Cases for Phase 2
   - Section 3.2: Agent Combat AI specification

2. docs/Features/Combat/advanced-tactical-behaviors.md (NEW - CRITICAL)
   - Section 1: Enhanced Agent Combat AI (skill formulas, objective modifiers, phase modifiers)

3. docs/Features/Combat/agent-combat-ai.md (CRITICAL - read entire doc)
   - AgentDrivenProperties (40+ properties)
   - AI Level calculation from skills
   - BehaviorValueSet (formation-based aggression)
   - All property ranges and effects

3. DECOMPILE REFERENCE (verify all APIs exist):
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\AgentDrivenProperties.cs
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Agent.cs
   - C:\Dev\Enlisted\Decompile\SandBox\SandboxAgentStatCalculateModel.cs

═══════════════════════════════════════════════════════════════════════════════
PHASE 2 TASKS
═══════════════════════════════════════════════════════════════════════════════

2.1 CONTEXT-AWARE AGENT TUNING
- Modify EnlistedAgentStatCalculateModel.InitializeAgentStats()
- Detect combat context:
  - Formation under archer fire? → Increase shield use priority
  - Formation in melee? → Increase aggression
  - Formation retreating? → Reduce blocking (run away)
  - Agent surrounded? → Defensive stance
- Apply property adjustments based on context

2.2 COORDINATION SIGNALS (Reduce Mob Behavior)
- Track which enemies are being targeted by how many agents
- Agents prefer less-targeted enemies (reduce AITargetingChance for over-targeted)
- Implement targeting weight adjustment
- Goal: Enemies spread across multiple targets, not piling on one

2.3 AI PROFILES
- Create AgentCombatProfile enum: Green, Regular, Veteran, Elite
- Create profile definitions with property sets:

Green Profile (Tier 1-2 troops):
  - AIBlockOnDecideAbility = 0.55f
  - AIParryOnDecideAbility = 0.45f
  - AiRandomizedDefendDirectionChance = 0.5f
  - AIAttackOnDecideChance = 0.2f
  
Veteran Profile (Tier 4-5 troops):
  - AIBlockOnDecideAbility = 0.92f
  - AIParryOnDecideAbility = 0.85f
  - AiRandomizedDefendDirectionChance = 0.15f
  - AIAttackOnDecideChance = 0.4f

Elite Profile (Tier 6+ or heroes):
  - AIBlockOnDecideAbility = 0.98f
  - AIParryOnDecideAbility = 0.92f
  - AiRandomizedDefendDirectionChance = 0.05f
  - AIAttackOnDecideChance = 0.48f

2.4 SKILL-BASED PROPERTY SCALING WITH FORMULAS (ENHANCED - TACTICAL)
- Calculate AI level: aiLevel = skillValue / 330f * difficultyMultiplier
- Apply formulas (from advanced-tactical-behaviors.md):
  - AIBlockOnDecideAbility = aiLevel * 2f (clamped 0.3-1.0)
  - AIParryOnDecideAbility = aiLevel * 2.2f (clamped 0.05-0.95)
  - AiAttackOnDecideChance = aiLevel * 1.2f (clamped 0.1-0.8)
  - AiShooterError = baseError * (1 - aiLevel * 0.7)
    - Bow: baseError = 0.003f
    - Crossbow: baseError = 0.001f (more accurate)

2.5 OBJECTIVE-AWARE COMBAT MODIFIERS (NEW - TACTICAL ENHANCEMENT)
- Adjust properties based on formation objective:
  - Attack: AiAttackOnDecideChance * 1.2, AIBlockOnDecideAbility * 0.9
  - Hold: AiAttackOnDecideChance * 0.7, AIBlockOnDecideAbility * 1.2, Shield * 1.3
  - Screen: AiAttackOnDecideChance * 0.8, AIBlockOnDecideAbility * 1.1
  - Fighting Retreat: AiAttackOnDecideChance * 0.5, AIBlockOnDecideAbility * 1.4

2.6 BATTLE PHASE COMBAT MODIFIERS (NEW - TACTICAL ENHANCEMENT)
- Adjust properties based on battle phase:
  - Crisis: AiAttackOnDecideChance * 1.15, AIBlockOnDecideAbility * 1.1 (desperation)
  - Rout: All properties * 0.6-0.7 (panic)
  - Pursuit: AiAttackOnDecideChance * 1.3, AIBlockOnDecideAbility * 0.85 (confidence)

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES TO HANDLE
═══════════════════════════════════════════════════════════════════════════════

From BATTLE-AI-IMPLEMENTATION-SPEC.md Section 2b:

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Agent dies during property calculation | Null-check Agent.State, skip dead agents |
| Agent has no weapon equipped | Use baseline properties, don't error |
| Agent switches weapons mid-combat | Recalculate properties on weapon change event |
| Horse archer dismounted | Transition from mounted to foot profile |
| Agent is player-controlled | Skip AI property overrides for player agent |
| Agent retreating/routing | Apply retreat-specific behavior (no blocking) |
| Agent in water/falling/ragdoll | Skip property updates until agent stable |

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/AgentCombatTuner.cs (context-aware tuning logic)
2. src/Features/Combat/Models/AgentCombatProfile.cs (profile enum and definitions)
3. src/Features/Combat/TargetCoordinator.cs (mob behavior reduction)

REMEMBER: Add all new .cs files to Enlisted.csproj with <Compile Include="..."/>

═══════════════════════════════════════════════════════════════════════════════
FILES TO MODIFY
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/EnlistedAgentStatCalculateModel.cs
   - Call AgentCombatTuner methods
   - Apply profile-based property sets
   - Apply skill scaling

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] Veteran soldiers demonstrably fight better than green troops
[ ] Context-aware adjustments visible (shield use under archer fire)
[ ] Enemies spread across multiple targets (reduced mob behavior)
[ ] AI profiles apply correct property values
[ ] Skill-based scaling works (higher skill = better properties)
[ ] Player agent NOT affected by AI tuning
[ ] Dead/routing agents handled gracefully
[ ] Logs show property adjustments: "[BattleAI] Agent:X Profile:Veteran Context:UnderFire"
[ ] Build succeeds
[ ] No crashes or exceptions

═══════════════════════════════════════════════════════════════════════════════
HANDOFF NOTES (Capture these for Phase 3)
═══════════════════════════════════════════════════════════════════════════════

When complete, provide these handoff notes for the next AI session:

FILES CREATED:
- [ ] src/Features/Combat/AgentCombatTuner.cs
- [ ] src/Features/Combat/Models/AgentCombatProfile.cs
- [ ] src/Features/Combat/TargetCoordinator.cs

FILES MODIFIED:
- [ ] src/Features/Combat/EnlistedAgentStatCalculateModel.cs

PROPERTY RANGES USED:
- (list the property ranges for each profile)

KEY DECISIONS MADE:
- (how did you calculate AI level from skills?)
- (how did you implement targeting coordination?)

KNOWN ISSUES/TECH DEBT:
- (list any incomplete items)

VERIFICATION PASSED:
[ ] Veteran vs Green troops observable difference
[ ] Context adjustments working
[ ] Mob behavior reduced
```

---

# Phase 3: Formation Intelligence

**Goal:** Implement smart charge decisions, weapon discipline, self-organizing ranks

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-2 complete

```
I need you to implement Phase 3 (Formation Intelligence) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13

API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\

PROJECT BUILD:
- Old-style .csproj: Manually add new files with <Compile Include="..."/>
- Build: dotnet build -c "Enlisted RETAIL" /p:Platform=x64

LOGGING: Use ModLogger.Info("BattleAI", "message");

CODE QUALITY (RESHARPER + REPO LINT):
- Follow ReSharper/repo lint - fix ALL warnings, don't suppress
- ALWAYS use braces for if/for/while/foreach
- Comments describe current behavior (no changelog style)
- Remove unused code, add null checks

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1-2 exist before starting)
═══════════════════════════════════════════════════════════════════════════════

Phase 1-2 are COMPLETE. Before implementing Phase 3, verify these files exist:

Phase 1-2 Files (MUST exist):
[ ] src/Features/Combat/BattleOrchestratorBehavior.cs
[ ] src/Features/Combat/BattleOrchestrator.cs
[ ] src/Features/Combat/EnlistedAgentStatCalculateModel.cs
[ ] src/Features/Combat/AgentCombatTuner.cs
[ ] src/Features/Combat/Models/AgentCombatProfile.cs

If any files are missing, create them first (see Phase 1-2 prompts).

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ FIRST
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 3 items (3.1-3.7)
   - Section 2b: Edge Cases for Phase 3

2. docs/Features/Combat/battle-ai-plan.md
   - Part 5: Tactical Enhancements (Smart Charge Decisions)
   - Part 10: Tactical Decision Engine (Cavalry Cycle Charging)
   - Part 11: Agent Formation Behavior (Pike/Spear Weapon Discipline)
   - Part 12: Intelligent Formation Organization (Self-Organizing Ranks)

3. DECOMPILE REFERENCE:
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Formation.cs
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\FormationQuerySystem.cs
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\BehaviorTacticalCharge.cs

═══════════════════════════════════════════════════════════════════════════════
PHASE 3 TASKS
═══════════════════════════════════════════════════════════════════════════════

3.1 SMART CHARGE DECISIONS
- Harmony patch BehaviorTacticalCharge or influence via weights
- Check target before charging:
  - Is target formation braced spears? → DON'T CHARGE
  - Is target shield wall facing us? → DON'T CHARGE (flank instead)
  - Is target vulnerable archers? → CHARGE
  - Is target routing? → CHARGE (pursue)
- Minimum charge distance: 60m (need momentum)
- Log charge decisions

3.2 INFANTRY FLANKING
- When own infantry has 1.5x advantage:
  - Split formation if >80 troops
  - Detach flanking element
  - Main force pins, flanking force envelops
- Only if orchestrator decides it's appropriate

3.3 THREAT ASSESSMENT TARGETING
- Score enemy formations before engaging:
  - Low armor = higher priority for archers
  - Low morale = higher priority (break them)
  - Vulnerable position = high priority
- Pass priority to formation's targeting decisions

3.4 FORMATION COHESION LEVELS
- Three levels: Tight, Moderate, Loose
- Tight: Under ranged fire, advancing on enemy
- Moderate: Normal combat, stable engagement
- Loose: Exploiting, pursuing, enemy broken
- Adjust via formation spacing orders

3.5 PIKE/SPEAR WEAPON DISCIPLINE (CRITICAL)
- Pike infantry keep polearms out at 6m+ range
- Switch to sidearm at <1.5m (pike too close)
- Formation breaking up = instant recall, tighten
- Never drop pike for melee unless forced

3.6 MULTI-WEAPON INFANTRY PROGRESSION
- Soldiers with javelins + spear + sword:
  1. Throw javelins at 20m+ (don't waste in melee)
  2. Use spear at 3-15m
  3. Switch to sword at <1.5m
- Conserve throwing weapons if spear available

3.7 SELF-ORGANIZING RANKS
- Heavy/elite troops to front (high armor, shields)
- Light troops to rear (support, ranged)
- Calculate FrontLineScore per agent:
  - +Armor, +Shield, +Tier, +Melee Skill
  - -Ranged primary weapon
- Sort formation by FrontLineScore

3.8 MULTI-TIER POSITION VALIDATION
- When ordering formation to position:
  1. Try requested position
  2. If NavMesh invalid, search expanding circles (5m, 10m, 25m, max 50m)
  3. If no valid position, fallback to current position
  4. Log warnings and notify orchestrator of failure
- Validate before any movement order

3.9 TACTICAL POSITION SCORING
- Score each candidate position (0-2.0):
  - Height advantage: +0.5
  - Cover availability: +0.3
  - Formation spacing: +0.4
  - Approach quality: +0.3
  - Enemy LOS: +0.5 (if archers, want LOS; if infantry, don't)
- Pick best scored position from search radius

3.10 SUPPRESSION DETECTION & RESPONSE
- Check `UnderRangedAttackRatio` from TeamQuerySystem
- If > 0.2 (taking heavy fire):
  - Switch to Loose arrangement (minimize casualties)
  - Seek better position (use tactical scoring)
  - Notify orchestrator (may need counter-battery or repositioning)
- Log suppression events

3.11 INFANTRY CAVALRY THREAT DETECTION
- Detect charging cavalry within 50m moving toward formation
- Response:
  - Stop movement immediately
  - Face threat direction
  - Form Square (if 80+ units and shields), else Shield Wall, else Loose
  - Request cavalry support from orchestrator
  - Hold until threat passes or is destroyed
- Threat score = (cavalry speed * cavalry count) / distance

3.12 PLAN-AWARE FLANK PROTECTION
- If assigned FlankGuard role in battle plan:
  - Position 30-50m offset from main effort axis
  - Face likely enemy approach
  - Intercept threats to main effort
  - Don't over-commit (stay in supporting position)
- If no threats, support main effort from flank

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES TO HANDLE
═══════════════════════════════════════════════════════════════════════════════

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

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/SmartChargeEvaluator.cs (charge decisions)
2. src/Features/Combat/WeaponDiscipline.cs (pike/spear/multi-weapon logic)
3. src/Features/Combat/FormationOrganizer.cs (self-organizing ranks)
4. src/Features/Combat/ThreatAssessor.cs (target scoring)
5. src/Features/Combat/PositionValidator.cs (multi-tier position validation + tactical scoring)
6. src/Features/Combat/SuppressionDetector.cs (ranged suppression detection)
7. src/Features/Combat/CavalryThreatDetector.cs (infantry cavalry threat response)
8. src/Features/Combat/FlankProtector.cs (plan-aware flank positioning)

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] Cavalry doesn't charge braced spear formations
[ ] Cavalry charges from 60m+ with momentum
[ ] Pike infantry keep pikes out at proper range
[ ] Multi-weapon soldiers use proper progression
[ ] Heavy troops end up in front ranks
[ ] Light/ranged troops end up in rear
[ ] Invalid positions trigger fallback search
[ ] Suppressed formations reposition and notify orchestrator
[ ] Infantry respond to cavalry threats (square/shield/loose)
[ ] Flank guards intercept threats to main effort
[ ] Position scoring selects tactically sound locations
[ ] Charge decisions logged
[ ] Build succeeds
[ ] No crashes

═══════════════════════════════════════════════════════════════════════════════
HANDOFF NOTES (Capture these for Phase 4)
═══════════════════════════════════════════════════════════════════════════════

FILES CREATED: (list them)
FILES MODIFIED: (list them)
KEY DECISIONS: (how did you hook into charge behavior? Harmony or weights?)
KNOWN ISSUES: (any incomplete items)
```

---

# Phase 4: Battle Orchestrator Core

**Goal:** Implement OODA decision loop, strategy modes, formation roles, battle phases

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-3 complete

```
I need you to implement Phase 4 (Battle Orchestrator Core) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13

API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\

PROJECT BUILD:
- Old-style .csproj: Manually add new files with <Compile Include="..."/>
- Build: dotnet build -c "Enlisted RETAIL" /p:Platform=x64

LOGGING: Use ModLogger.Info("BattleAI", "message");

CODE QUALITY (RESHARPER + REPO LINT):
- Follow ReSharper/repo lint - fix ALL warnings, don't suppress
- ALWAYS use braces for if/for/while/foreach
- Comments describe current behavior (no changelog style)
- Remove unused code, add null checks

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY
═══════════════════════════════════════════════════════════════════════════════

Verify Phase 1-3 files exist:
[ ] src/Features/Combat/BattleOrchestrator.cs (empty OODA methods)
[ ] src/Features/Combat/Models/BattleState.cs
[ ] src/Features/Combat/BattleStateReader.cs
[ ] src/Features/Combat/SmartChargeEvaluator.cs
[ ] src/Features/Combat/WeaponDiscipline.cs
[ ] src/Features/Combat/FormationOrganizer.cs

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 4 items (4.1-4.5)
   - Section 2b: Edge Cases for Phase 4
   - Section 3.1: BattleOrchestrator specification

2. docs/Features/Combat/battle-ai-plan.md
   - Part 4: Battle Orchestrator Proposal (full detail)
   - Part 9: AI vs AI Battle Intelligence

═══════════════════════════════════════════════════════════════════════════════
PHASE 4 TASKS
═══════════════════════════════════════════════════════════════════════════════

4.1 OODA DECISION LOOP (Every 1-2 seconds)
- OBSERVE: Capture signals from TeamQuerySystem, FormationQuerySystem
  - Power ratios, casualty rates, positions
  - Store in rolling history (last 30 seconds)
- ORIENT: Analyze battlefield state
  - Estimate frontline center
  - Sample local superiority at key points
  - Detect flanking threats
- DECIDE: Choose strategy mode (with hysteresis)
  - Use 1.3x hysteresis to prevent flip-flopping
  - Log decision factors
- ACT: Apply bounded interventions
  - Nudge formation behavior weights
  - Issue reserve commands
  - Respect cooldowns

4.2 STRATEGY MODES
- EngageBalanced: Standard balanced attack
- DelayDefend: Defensive posture, refuse flanks
- Exploit: Aggressive exploitation of weakness
- Withdraw: Fighting retreat, preserve force

Mode transitions:
- PowerRatio < 0.4 → Withdraw
- PowerRatio > 1.5 + flank exposed → Exploit
- PowerRatio 0.4-0.7 → DelayDefend
- Otherwise → EngageBalanced

4.3 FORMATION ROLES
- MainLine: Primary combat formations
- Screen: Skirmishers, forward security
- FlankGuard: Protect exposed flanks
- Reserve: Held back, committed on triggers

Assign roles based on:
- Formation composition (cavalry = potential reserve)
- Current position
- Player command (respect player orders for player's formation)

4.4 BATTLE PHASES
- Forming: Initial deployment
- Advancing: Moving to contact
- Engaged: Active combat
- Retreating: Organized withdrawal

Detect phase from:
- Distance to enemy
- Casualty rates
- Formation states

4.5 TREND ANALYSIS
- Track power ratio over time (rolling 30s window)
- Detect: "Losing steadily for N seconds" → more aggressive action
- Detect: "Winning but slowing" → commit reserves
- Provide trend data to decision logic

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES
═══════════════════════════════════════════════════════════════════════════════

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Battle ends during OODA loop | Check Mission.IsEnding at start of tick |
| All formations destroyed | Early exit, stop orchestrator |
| Power ratio NaN/infinity | Guard division by zero, default to 1.0 |
| Enemy team retreated | Transition to consolidation/pursuit |
| Trend data insufficient (<5 samples) | Use current snapshot, mark unknown |
| Strategy mode indeterminate | Default to EngageBalanced |

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE/MODIFY
═══════════════════════════════════════════════════════════════════════════════

Create:
1. src/Features/Combat/Models/StrategyMode.cs (enum)
2. src/Features/Combat/Models/FormationRole.cs (enum)
3. src/Features/Combat/Models/BattlePhase.cs (enum)
4. src/Features/Combat/TrendAnalyzer.cs

Modify:
1. src/Features/Combat/BattleOrchestrator.cs (implement OODA methods)

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] OODA loop ticks every 1-2 seconds
[ ] Strategy mode changes with hysteresis (no rapid flipping)
[ ] Formation roles assigned appropriately
[ ] Battle phase detected correctly
[ ] Trend analysis provides useful data
[ ] Logs show: "[BattleAI] Team:X Strategy:EngageBalanced→Exploit (PowerRatio:1.8)"
[ ] Build succeeds
[ ] No crashes

═══════════════════════════════════════════════════════════════════════════════
HANDOFF NOTES
═══════════════════════════════════════════════════════════════════════════════

FILES CREATED: (list)
FILES MODIFIED: (list)
STRATEGY THRESHOLDS: (document what values you used)
KEY DECISIONS: (list)
KNOWN ISSUES: (list)
```

---

# Phase 5: Tactical Decision Engine

**Goal:** Cavalry cycle charging, reserve timing, targeting decisions

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-4 complete

```
I need you to implement Phase 5 (Tactical Decision Engine) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY
═══════════════════════════════════════════════════════════════════════════════

Verify Phase 1-4 files exist:
[ ] src/Features/Combat/BattleOrchestrator.cs (with OODA loop)
[ ] src/Features/Combat/Models/StrategyMode.cs
[ ] src/Features/Combat/Models/FormationRole.cs
[ ] src/Features/Combat/TrendAnalyzer.cs

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 5 items (5.1-5.6)
   - Section 2b: Edge Cases for Phase 5

2. docs/Features/Combat/battle-ai-plan.md
   - Part 10: Tactical Decision Engine (all sections)
   - Cavalry Cycle Charging section (CRITICAL)

═══════════════════════════════════════════════════════════════════════════════
PHASE 5 TASKS
═══════════════════════════════════════════════════════════════════════════════

5.1 THREAT VS OPPORTUNITY WEIGHING
- Multi-factor scoring for tactical decisions
- Factors: Enemy vulnerability, own strength, terrain, timing
- Score each potential action, pick highest

5.2 ARCHER EFFECTIVENESS AWARENESS (CRITICAL)
- Detect if archers are hitting targets:
  - Check LOS to target using Scene.RayCastForClosestEntityOrTerrain
  - Track recent ranged casualties caused by formation
  - Sample 5 agents for LOS checks (performance-friendly)
- If shooting but NOT hitting (blocked LOS, out of range):
  - Try micro-adjustments: 8 offsets (±5m, ±3.5m diagonals)
  - Score positions by: LOS quality, range to target, safety
  - Move to best micro-position (max 7m adjustment)
- If hitting targets:
  - HOLD position (don't break what works)
  - Continue current target
- Pass targeting hints for priority:
  1. Low armor targets (max damage)
  2. Low morale targets (break them)
  3. Enemy ranged (counter-battery)
  4. High-value targets (heroes)

5.3 PLAN-BASED CAVALRY DEPLOYMENT (CRITICAL)
- Deployment depends on battle plan:
  - **Hammer & Anvil**: Wait until infantry engaged (30+ sec in melee), then flank charge
  - **Hook Plans**: Position 30-50m from enemy flank, wait for infantry pin, then charge
  - **Delay/Defensive**: Hold cavalry unless threatened, only countercharge enemy cavalry
  - **Pursuit**: Full speed pursuit of routing formations
- Check formation state before deploying (don't charge if infantry not ready)
- Log deployment reason: "[BattleAI] Cavalry deployed: Plan=HammerAnvil InfantryEngaged=35s"

5.4 ENHANCED CAVALRY CYCLE CHARGING (9-STATE MACHINE, CRITICAL)
State machine:
1. **RESERVE**: Holding position, waiting for opportunity
2. **POSITIONING**: Moving to optimal charge position (30-80m from target)
3. **BRACING**: Final formation integrity check, preparing to charge
4. **CHARGING**: Full speed charge to contact (deviation < 5f required)
5. **CHARGING_PAST**: Passed through enemy formation (lance impact zone)
6. **IMPACT**: Initial contact with enemy (first 2-3 seconds)
7. **MELEE**: Sustained melee combat (adaptive timer)
8. **DISENGAGING**: Breaking contact, moving away from enemy
9. **RALLYING**: Reforming at rally point
10. **REFORMING**: Final preparation for next cycle

Formation Integrity Gates:
- Deviation < 5f to advance from Bracing → Charging
- Deviation < 12f to maintain charge
- Deviation > 25f triggers emergency rally
- Width matching: cavalry width = target width * 1.2 (wider for envelopment)

Adaptive Timers:
- Charge duration: 15s base, +5s if uphill, -3s if downhill
- Melee duration: 5s base, +3s if winning (kill ratio > 1.5), -2s if losing
- Reform duration: 12s base, *0.75 if suppressed, +3s if casualties > 30%

Intelligent Target Selection:
- Score targets by:
  - Class: Archers (2.0), Light Infantry (1.5), Heavy Infantry (0.8), Cavalry (0.5)
  - Distance: closer = better (1.0 at 50m, 0.5 at 150m+)
  - Vulnerability: routing (2.0), flanked (1.5), engaged (1.0), fresh (0.7)
  - Threat to team: high (1.5), medium (1.0), low (0.5)
  - Approach angle: rear/flank (1.5), side (1.0), front (0.6)
  - Plan priority: main effort target (1.5), supporting (1.0), other (0.7)
- Pick highest scored target (min score 0.8 to commit)

Rules:
- Min 60m for charge (momentum)
- Max melee time by adaptive timer (don't get bogged down)
- Rally point 50-80m behind own lines
- 3-6 charges per battle vs native's 1-2
- Formation cohesion checked every tick

5.5 RESERVE COMMITMENT LOGIC
- Commit reserve when:
  - Main line casualties > 25%
  - Flank collapsing
  - Enemy reserve committed (match)
  - Clear breakthrough opportunity
- Never commit all reserves at once (keep 30%)

5.6 PURSUIT DECISIONS
- Full pursuit: Enemy broken, we have cavalry
- Cavalry only: Infantry holds, cavalry pursues
- Hold: Enemy may rally, preserve force
- Regroup: High casualties, reform first

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES
═══════════════════════════════════════════════════════════════════════════════

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
| **Battle plan null when deploying cavalry** | Use default timing (wait for infantry engaged) |
| **Infantry not engaged when plan requires** | Hold cavalry, wait for pin condition |

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/TacticalDecisionEngine.cs
2. src/Features/Combat/CavalryCycleManager.cs (9-state machine + integrity gates + adaptive timers)
3. src/Features/Combat/ArcherEffectivenessDetector.cs (LOS checks, hit detection, micro-positioning)
4. src/Features/Combat/ReserveManager.cs
5. src/Features/Combat/Models/CavalryCycleState.cs (enum with 9 states)
6. src/Features/Combat/Models/CavalryTargetScore.cs (target scoring model)

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] Cavalry uses 9-state cycle (Reserve→Positioning→Bracing→...→Reforming)
[ ] Formation integrity gates prevent premature charges (deviation < 5f)
[ ] Cavalry width matches target width * 1.2
[ ] Adaptive timers adjust based on terrain, kill ratio, suppression
[ ] Intelligent target selection scores targets correctly
[ ] Plan-based deployment (HammerAnvil waits, Hook positions, Delay holds)
[ ] Cavalry reforms and charges 3-6 times per battle
[ ] Archers detect when not hitting targets (LOS blocked)
[ ] Archers micro-adjust position (±5-7m) to improve LOS
[ ] Archers hold position when hitting effectively
[ ] Reserve commits at appropriate triggers
[ ] Pursuit decision made at battle end
[ ] Logs show: "[BattleAI] Cavalry:F3 Bracing→Charging deviation=3.2f target=Archers score=1.8"
[ ] Logs show: "[BattleAI] Archers:F2 NoHits detected, adjusting +5m forward"
[ ] Build succeeds
[ ] No crashes

═══════════════════════════════════════════════════════════════════════════════
HANDOFF NOTES
═══════════════════════════════════════════════════════════════════════════════

FILES CREATED: (list)
CYCLE TIMING VALUES: (charge distance, melee duration, etc.)
RESERVE THRESHOLDS: (casualty %, etc.)
KEY DECISIONS: (list)
```

---

# Phase 6: Battle Plan Generation

**Goal:** Plan types, plan selection, main effort designation, adaptation

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-5 complete

```
I need you to implement Phase 6 (Battle Plan Generation) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY
═══════════════════════════════════════════════════════════════════════════════

Verify Phase 1-5 files exist:
[ ] src/Features/Combat/BattleOrchestrator.cs (OODA loop working)
[ ] src/Features/Combat/TacticalDecisionEngine.cs
[ ] src/Features/Combat/CavalryCycleManager.cs
[ ] src/Features/Combat/ReserveManager.cs

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 6 items (6.1-6.6)
   - Section 2b: Edge Cases for Phase 6

2. docs/Features/Combat/battle-ai-plan.md
   - Part 14: Battle Plan Generation (all sections)

═══════════════════════════════════════════════════════════════════════════════
PHASE 6 TASKS
═══════════════════════════════════════════════════════════════════════════════

6.1 PLAN TYPES
- LeftHook: Main effort left, pin center/right
- RightHook: Main effort right, pin center/left
- CenterPunch: Concentrated center breakthrough
- DoubleEnvelopment: Both flanks attack, center pins
- HammerAnvil: Infantry pins, cavalry flanks
- Delay: Defensive, trade space for time

6.2 PLAN SELECTION
Score each plan by:
- Composition fit (cavalry for HammerAnvil, etc.)
- Terrain suitability (high ground for defense)
- Power ratio (envelopment when advantaged)
- Enemy disposition (exploit weak flank)

6.3 MAIN EFFORT DESIGNATION
- Assign strongest formations to main effort axis
- Other formations support (pin, screen, reserve)
- Log main effort clearly

6.4 FORMATION OBJECTIVES
- Attack: Advance and destroy
- Pin: Engage and fix enemy
- Screen: Skirmish, delay, protect flank
- Breakthrough: Punch through line
- Flank: Envelop enemy position
- Hold: Defensive position

6.5 SEQUENTIAL OBJECTIVES
- Cavalry: Screen → Wait → Charge → Pursue
- Infantry: Advance → Engage → Exploit
- Phase-based tasking respects timing

6.6 PLAN ADAPTATION
- Detect plan success/failure every 30s
- Shift main effort if original axis failing
- Commit reserves to failing axis OR exploit success
- Log adaptation: "[BattleAI] Plan:LeftHook→CenterPunch (LeftFlankStalled)"

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES
═══════════════════════════════════════════════════════════════════════════════

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Terrain data unavailable | Use flat-terrain default plan |
| No valid plan matches | Fall back to EngageBalanced |
| Main effort formation destroyed | Reassign to next strongest |
| All formations same objective | Spread objectives |
| Plan scores tied | Random tie-breaking |
| Insufficient troops for plan | Scale down complexity |

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/BattlePlanGenerator.cs
2. src/Features/Combat/Models/BattlePlan.cs
3. src/Features/Combat/Models/BattlePlanType.cs (enum)
4. src/Features/Combat/Models/FormationObjective.cs (enum)

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] Plans generated based on composition/terrain/ratio
[ ] Main effort designated and logged
[ ] Formation objectives assigned
[ ] Plan adapts when axis failing
[ ] Logs show: "[BattleAI] Plan:HammerAnvil MainEffort:CavalryF3 Objective:Flank"
[ ] Build succeeds

═══════════════════════════════════════════════════════════════════════════════
HANDOFF NOTES
═══════════════════════════════════════════════════════════════════════════════

FILES CREATED: (list)
PLAN SCORING WEIGHTS: (document)
KEY DECISIONS: (list)
```

---

# Phase 7: Reserve & Retreat Management

**Goal:** Reserve management, organized withdrawal, rally points

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-6 complete

```
I need you to implement Phase 7 (Reserve & Retreat Management) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1-6 exist before starting)
═══════════════════════════════════════════════════════════════════════════════

Verify these key files exist from previous phases:
[ ] src/Features/Combat/BattleOrchestrator.cs (with OODA loop from Phase 4)
[ ] src/Features/Combat/TacticalDecisionEngine.cs (Phase 5)
[ ] src/Features/Combat/BattlePlanGenerator.cs (Phase 6)
[ ] src/Features/Combat/ReserveManager.cs (Phase 5 - you'll extend this)

If missing, complete earlier phases first.

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 7 items (7.1-7.5)
   - Section 2b: Edge Cases for Phase 7

2. docs/Features/Combat/battle-ai-plan.md
   - Part 18: Coordinated Retreat

═══════════════════════════════════════════════════════════════════════════════
PHASE 7 TASKS
═══════════════════════════════════════════════════════════════════════════════

7.1 RESERVE MANAGER
- Track which formations are reserves
- Hold until commit triggers
- Commit commands: Reinforce, Counterattack, Intercept, Pursue
- Never commit all (keep 30% uncommitted)

7.2 CASUALTY-BASED POSTURE
- Engaged: Normal combat (<20% casualties)
- FightingRetreat: Giving ground (20-40% casualties)
- TacticalWithdrawal: Organized fallback (40-60% casualties)
- LastStand: Fight to end (>60% OR cornered)

7.3 TACTICAL WITHDRAWAL DECISION (CRITICAL)
- Triggers:
  - Power ratio < 0.7 (significantly outmatched)
  - Casualties > 40%
  - Main effort formation destroyed
  - Trend shows losing for 30+ seconds
- **IMPORTANT: Tactical withdrawal = fall back to rally point ON MAP (NOT leave map)**
- Formations regroup at rally point, reform, reassess
- Distinguish from full rout (morale collapse, units flee map via vanilla retreat)
- Log: "[BattleAI] Tactical Withdrawal triggered: PowerRatio=0.58 Casualties=43%"

7.4 ORGANIZED WITHDRAWAL
- Designate covering force (rearguard): 20-30% of force
- Step-by-step fallback (leapfrog bounds):
  - Bound 1: Rearguard holds position, main force moves 50m back
  - Bound 2: Main force holds new position, rearguard moves through
  - Repeat until rally point reached
- Covering force engages pursuing enemy (delay, not destroy)
- Main force maintains cohesion during movement

7.5 RALLY POINT SELECTION (CRITICAL)
- Priority order for rally point:
  1. **Near spawn point (100m radius)** - reinforcements arrive here
  2. **Defensible terrain** - high ground, chokepoint, forest edge
  3. **150m fallback from current position** - if spawn unreachable
- Validate position (NavMesh, accessibility)
- Rally point should allow reformation + resumption of combat OR continued withdrawal
- Log rally point: "[BattleAI] Rally point selected: SpawnPoint X=500 Y=300 Distance=85m"

7.6 FULL ROUT VS TACTICAL WITHDRAWAL (CRITICAL)
- **Tactical Withdrawal** (orchestrator-controlled):
  - Formations stay on map
  - Rally at designated point
  - Can regroup and resume fighting
  - Still in battle, just repositioning
  - AI-driven decision, NOT morale collapse
- **Full Rout** (vanilla morale system):
  - Morale collapsed (native game triggers this)
  - Units flee map entirely (vanilla retreat behavior)
  - Battle lost
  - Do NOT interfere with vanilla rout
- Check morale before triggering tactical withdrawal (if morale already collapsed, don't bother)

7.7 STALEMATE PREVENTION
- If no significant change in 45 seconds:
  - Force action (commit reserve, attempt flank)
  - Prevents "staring contests"
- Timer resets on any major action

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES
═══════════════════════════════════════════════════════════════════════════════

| Edge Case | Handling Strategy |
|-----------|-------------------|
| No reserve designated | Mark reserve as empty, skip reserve logic |
| Rally point unreachable (NavMesh invalid) | Try priority 2 (terrain), then priority 3 (150m fallback) |
| All rally point options invalid | Stay in current position, fight it out (last stand) |
| Covering force destroyed during withdrawal | Accept losses, continue main force retreat |
| Stalemate timer but enemy routing | Cancel stalemate, transition to pursuit |
| Last stand triggered but won | Cancel last stand, exploit opportunity |
| **Tactical withdrawal triggered but morale already collapsed** | Skip withdrawal logic, let vanilla rout handle it |
| **Spawn point null or unavailable** | Use terrain-based rally (priority 2) or fallback (priority 3) |
| **Formations scattered during withdrawal** | Rally at nearest accessible position for each formation |
| **Enemy pursues aggressively during withdrawal** | Covering force engages, delay pursuit, accept casualties |
| **Withdrawal triggered but enemy also withdrawing** | Cancel withdrawal, hold position or cautious advance |
| **Power ratio improves during withdrawal** | Reassess: continue if momentum lost, stop if can resume fighting |

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/RetreatCoordinator.cs (tactical withdrawal + organized bounds)
2. src/Features/Combat/RallyPointCalculator.cs (spawn-based, terrain-based, fallback selection)
3. src/Features/Combat/Models/CombatPosture.cs (enum: Engaged, FightingRetreat, TacticalWithdrawal, LastStand)
4. src/Features/Combat/Models/WithdrawalType.cs (enum: Tactical vs FullRout)

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] Reserve manager tracks and commits reserves
[ ] Casualty thresholds trigger posture changes
[ ] Tactical withdrawal triggers at power < 0.7 or casualties > 40%
[ ] Formations withdraw to rally point ON MAP (do not leave map)
[ ] Rally point selected: spawn (100m) > terrain > fallback (150m)
[ ] Organized withdrawal with leapfrog bounds and covering force
[ ] Distinguish tactical withdrawal (on map) vs full rout (vanilla flee)
[ ] Formations reform at rally point and can resume combat
[ ] Stalemate prevention triggers action after 45s
[ ] Logs show: "[BattleAI] Tactical Withdrawal triggered: PowerRatio=0.58"
[ ] Logs show: "[BattleAI] Rally point: SpawnPoint X=500 Y=300 Distance=85m"
[ ] Build succeeds
[ ] No crashes
```

---

# Phase 8: Player Counter-Intelligence (T7+)

**Goal:** Tracking player formation, threat projection, counter-composition

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-7 complete

```
I need you to implement Phase 8 (Player Counter-Intelligence) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments
T7+ ACTIVATION: Only when player is T7+ (commander with own troops)

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1-7 exist before starting)
═══════════════════════════════════════════════════════════════════════════════

Verify these key files exist from previous phases:
[ ] src/Features/Combat/BattleOrchestrator.cs (with OODA loop)
[ ] src/Features/Combat/TacticalDecisionEngine.cs
[ ] src/Features/Combat/ReserveManager.cs
[ ] src/Features/Combat/RetreatCoordinator.cs (Phase 7)

If missing, complete earlier phases first.

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 8 items (8.1-8.5)
   - Section 2b: Edge Cases for Phase 8

2. docs/Features/Combat/battle-ai-plan.md
   - Part 7: Player Counter-Intelligence (all sections)

═══════════════════════════════════════════════════════════════════════════════
PHASE 8 TASKS
═══════════════════════════════════════════════════════════════════════════════

8.1 PLAYER FORMATION TRACKING
- Identify player's formation
- Track: Position, velocity, facing
- Calculate threat angle to own formations

8.2 THREAT PROJECTION
- Predict player position 10-15 seconds ahead
- Use velocity extrapolation
- Adjust for terrain obstacles
- Log predictions

8.3 FLANK DETECTION
- Multiple signals for flanking:
  - Player angle > 45° from front
  - Player velocity toward flank
  - Player composition (cavalry = fast flank threat)
- Flank threat score: 0-1

8.4 COUNTER-COMPOSITION RESERVE
- Hold units that counter player's composition:
  - Player has cavalry → Hold spear infantry
  - Player has archers → Hold cavalry
  - Player has infantry → Hold ranged
- Only commit counters when player commits

8.5 FLANK RESPONSE DECISION TREE
- FlankThreat < 0.3: Ignore
- FlankThreat 0.3-0.6: Reposition flank guard
- FlankThreat 0.6-0.8: Commit reserve to intercept
- FlankThreat > 0.8: Compact formation, refuse flank

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES
═══════════════════════════════════════════════════════════════════════════════

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Player T7+ but in simulation | Check if player commanding |
| Player changes formation mid-battle | Update tracking |
| Player dismounts/mounts | Recalculate threat (speed change) |
| Player dead but battle continues | Stop tracking player |
| Player far from battle (scouting) | Reduce threat priority |
| Player has no troops (solo) | Treat as single high-value target |
| Player on AI's team | Skip counter-AI entirely |

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/PlayerCounterAI.cs
2. src/Features/Combat/ThreatProjector.cs
3. src/Features/Combat/Models/FlankThreat.cs

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] Player formation tracked when T7+
[ ] Threat projection predicts player movement
[ ] Flank detection scores 0-1
[ ] Counter-composition reserves held
[ ] Flank response triggers at thresholds
[ ] Only activates when player is T7+ commander
[ ] Logs show: "[BattleAI] PlayerThreat:0.7 Response:InterceptReserve"
[ ] Build succeeds
```

---

# Phases 9-11: Polish (Combined)

**Goal:** Reinforcement intelligence, battle pacing, terrain/morale exploitation

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-8 complete

```
I need you to implement Phases 9-11 (Polish) of the Battle AI system for my Bannerlord mod.

These are polish phases that can be done together in one chat.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1-8 exist before starting)
═══════════════════════════════════════════════════════════════════════════════

Verify these key files exist from previous phases:
[ ] src/Features/Combat/BattleOrchestrator.cs (core orchestrator)
[ ] src/Features/Combat/BattlePlanGenerator.cs (plan system)
[ ] src/Features/Combat/TacticalDecisionEngine.cs (tactical decisions)
[ ] src/Features/Combat/ReserveManager.cs (reserve system)
[ ] src/Features/Combat/PlayerCounterAI.cs (Phase 8)

If missing, complete earlier phases first.

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phases 9, 10, 11 items
   - Section 2b: Edge Cases for Phases 9-11

2. docs/Features/Combat/battle-ai-plan.md
   - Part 20: Reinforcement Intelligence
   - Part 19: Battle Pacing and Cinematics
   - Part 16: Terrain Exploitation
   - Part 17: Morale Exploitation

═══════════════════════════════════════════════════════════════════════════════
PHASE 9: REINFORCEMENT INTELLIGENCE
═══════════════════════════════════════════════════════════════════════════════

9.1 Strategic Wave Timing
- Hold/rush reinforcements based on battle state
- Losing? Rush reinforcements
- Winning? Delay for dramatic arrival

9.2 Formation-Aware Assignment
- Route reinforcements to formations by need
- Understrength formations get priority

9.3 Quality Distribution
- Spread elites across formations
- Don't put all veterans in one formation

9.4 Spawn Point Tactics
- Fight near your spawn for advantage
- Consider spawn point defense

9.5 Reinforcement Integration
- Form up before joining combat
- Brief morale/cohesion boost on arrival

═══════════════════════════════════════════════════════════════════════════════
PHASE 10: BATTLE PACING & CINEMATICS
═══════════════════════════════════════════════════════════════════════════════

10.1 Deliberate Approach Speed
- Don't blob forward immediately
- Form up, then advance as formation

10.2 Skirmish Phase
- Probing attacks before full engagement
- Archers/skirmishers lead

10.3 Dramatic Moments
- Champion duels (heroes engage each other)
- Last stands (few vs many)
- Banner drama (protect banner bearer)

10.4 Morale-Driven Endings
- Rout cascades when morale breaks
- Ordered withdrawal when losing

═══════════════════════════════════════════════════════════════════════════════
PHASE 11: TERRAIN & MORALE EXPLOITATION
═══════════════════════════════════════════════════════════════════════════════

11.1 High Ground Strategy
- Seek and hold elevation
- Bonus for ranged from high ground

11.2 Choke Point Exploitation
- Funnel enemies through narrow terrain
- Deny enemy flanking through chokes

11.3 Forest/Difficult Terrain
- Use forest to negate cavalry

11.4 Terrain-Aware Battle Plans
- Select plans based on terrain features

11.5 Morale Reading
- Target low-morale formations

11.6 Rout Triggering
- Focus fire to break enemy morale

11.7 Protecting Own Morale
- Shield fragile formations

11.8 Strategic Withdrawal Before Collapse
- Disengage before routing

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/ReinforcementManager.cs
2. src/Features/Combat/BattlePacingManager.cs
3. src/Features/Combat/TerrainAnalyzer.cs
4. src/Features/Combat/MoraleExploiter.cs

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] Reinforcements routed intelligently
[ ] Deliberate approach, not blobbing
[ ] High ground sought when available
[ ] Low-morale targets prioritized
[ ] Build succeeds
```

---

# Phases 12-14: Formation Systems (Combined)

**Goal:** Formation doctrine, unit shapes, intelligent organization

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-11 complete

```
I need you to implement Phases 12-14 (Formation Systems) of the Battle AI system for my Bannerlord mod.

These are tightly coupled formation systems that should be done together.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1-11 exist before starting)
═══════════════════════════════════════════════════════════════════════════════

Verify these key files exist from previous phases:
[ ] src/Features/Combat/BattleOrchestrator.cs
[ ] src/Features/Combat/BattlePlanGenerator.cs
[ ] src/Features/Combat/FormationOrganizer.cs (Phase 3 - you'll extend)
[ ] src/Features/Combat/ReinforcementManager.cs (Phase 9)
[ ] src/Features/Combat/TerrainAnalyzer.cs (Phase 11)

If missing, complete earlier phases first.

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phases 12, 13, 14 items
   - Section 2b: Edge Cases for Phases 12-14
   - Section 3.7: Formation Doctrine System
   - Section 3.8: Unit Type Formations

2. docs/Features/Combat/battle-ai-plan.md
   - Part 13: Formation Doctrine System
   - Part 15: Unit Type Formations
   - Part 12: Intelligent Formation Organization

═══════════════════════════════════════════════════════════════════════════════
PHASE 12: FORMATION DOCTRINE SYSTEM
═══════════════════════════════════════════════════════════════════════════════

12.1 Battle Scale Detection
- Lord Party Battle: 50-200 troops
- Army Battle: 400+ troops

12.2 Formation Count Logic
- Small: 2-3 formations
- Large: 4-6 formations

12.3 Formation Doctrines
- SingleDeepLine: 2-rank line
- ThreeWing: Left, center, right
- ExtendedThin: Wide frontage (outnumbered)
- HammerAnvil: Infantry + cavalry reserve

12.4 Line Depth Decisions
- 2-4 ranks based on numbers/threat

12.5 Counter-Formation Tactics
- Detect enemy doctrine, select counter

12.6 Troop Distribution
- Spread elites across formations

═══════════════════════════════════════════════════════════════════════════════
PHASE 13: UNIT TYPE FORMATIONS
═══════════════════════════════════════════════════════════════════════════════

13.1 Infantry Shapes
- Line, Shield Wall, Loose, Square, Circle

13.2 Archer Shapes
- Line, Loose, Scatter, Staggered

13.3 Cavalry Shapes
- Line, Skein/Wedge, Loose

13.4 Formation Positioning
- Archers 20-40m behind infantry
- Cavalry on flanks

13.5 Dynamic Formation Switching
- Shield Wall under archer fire
- Square when cavalry charges (80+ troops)

13.6 Width and Depth Control
- Match/exceed enemy frontage

13.7 Formation Facing/Movement
- Maintain facing during advance

13.8 Multi-Formation Coordination
- All formations move as coherent army

═══════════════════════════════════════════════════════════════════════════════
PHASE 14: INTELLIGENT FORMATION ORGANIZATION
═══════════════════════════════════════════════════════════════════════════════

14.1 Front-Line Scoring
- Score: +Armor, +Shield, +Tier, +MeleeSkill

14.2 Flank Spillover
- Rear troops work flanks when stable

14.3 Gap Filling Logic
- Middle ranks step up when front falls

14.4 Formation Safeguards
- Max deviation distance
- No chase beyond formation
- Instant recall on order

14.5 Positional Combat Behavior
- Front: aggressive
- Rear: defensive/ranged

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/FormationDoctrineSystem.cs
2. src/Features/Combat/UnitFormationShapes.cs
3. src/Features/Combat/IntelligentFormationOrganizer.cs
4. src/Features/Combat/Models/FormationDoctrine.cs (enum)
5. src/Features/Combat/Models/FormationShape.cs (enum)

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] Battle scale correctly detected
[ ] Formation count appropriate for size
[ ] Infantry switches to Shield Wall under fire
[ ] Square formed when cavalry charges (80+ troops)
[ ] Heavy troops in front ranks
[ ] Formations move as coherent army
[ ] Build succeeds
```

---

# Phase 15: Plan Execution & Anti-Flip-Flop

**Goal:** State machine, anti-flip-flop rules, enemy composition recognition

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-14 complete

```
I need you to implement Phase 15 (Plan Execution & Anti-Flip-Flop) of the Battle AI system for my Bannerlord mod.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1-14 exist before starting)
═══════════════════════════════════════════════════════════════════════════════

Verify these key files exist from previous phases:
[ ] src/Features/Combat/BattleOrchestrator.cs
[ ] src/Features/Combat/BattlePlanGenerator.cs (Phase 6 - you'll extend)
[ ] src/Features/Combat/FormationDoctrineSystem.cs (Phase 12)
[ ] src/Features/Combat/UnitFormationShapes.cs (Phase 13)

If missing, complete earlier phases first.

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 15 items (15.1-15.7)
   - Section 2b: Edge Cases for Phase 15
   - Section 3.9: Plan Execution State Machine

2. docs/Features/Combat/battle-ai-plan.md
   - Part 14: Battle Plan Generation (Plan Execution section)

═══════════════════════════════════════════════════════════════════════════════
PHASE 15 TASKS
═══════════════════════════════════════════════════════════════════════════════

15.1 PLAN EXECUTION STATE MACHINE
States:
- Forming: Initial deployment
- Advancing: Moving to contact
- Engaging: Active combat
- Exploiting: Pushing advantage
- Consolidating: Holding gains
- Pursuing: Chasing routed enemy
- Adapting: Changing plans
- Withdrawing: Organized retreat

Transitions based on:
- Distance to enemy
- Casualty rates
- Enemy state (routing, reinforcing)

15.2 ANTI-FLIP-FLOP RULES (CRITICAL)
- Minimum commitment: 30 seconds on any plan
- Cooldown after change: 20 seconds
- Max changes: 2 per 60 seconds
- Enforce via timestamps and counters

15.3 PHASE-BASED GATING
- Allow plan changes at natural phase transitions
- E.g., Engaging→Exploiting is natural time to adapt

15.4 LEGITIMATE CHANGE DETECTION
Allow plan changes for:
- Catastrophic failure (40%+ casualties in 20s)
- Enemy major shift (reinforcement wave)
- Clear new opportunity (enemy flank collapsed)

15.5 ENEMY COMPOSITION RECOGNITION
Classify enemy:
- Infantry Horde: >70% infantry
- Archer Heavy: >40% archers
- Cavalry Heavy: >40% cavalry
- Horse Archers: >30% mounted ranged
- Balanced: Mixed

15.6 DEFENSIVE COUNTER-FORMATIONS
- vs Infantry Horde: Standard line, archer attrition
- vs Archer Heavy: Shield wall advance, cavalry rush
- vs Cavalry Heavy: Spear screen, protect archers
- vs Horse Archers: Compact, wait for them to close

15.7 COMMITMENT TIMING
- When to engage vs delay
- Consider: terrain, reinforcements, morale

═══════════════════════════════════════════════════════════════════════════════
EDGE CASES
═══════════════════════════════════════════════════════════════════════════════

| Edge Case | Handling Strategy |
|-----------|-------------------|
| Battle starts in Engaging (close spawn) | Skip Forming/Advancing |
| Catastrophic failure false positive | Require sustained failure (10+ seconds) |
| Anti-flip-flop locks needed change | Emergency override for genuine catastrophe |
| Enemy composition changes | Re-evaluate periodically |
| State machine in invalid state | Reset to Engaging, log error |

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

1. src/Features/Combat/PlanExecutionStateMachine.cs
2. src/Features/Combat/AntiFlipFlopGuard.cs
3. src/Features/Combat/EnemyCompositionAnalyzer.cs
4. src/Features/Combat/Models/PlanPhase.cs (enum)
5. src/Features/Combat/Models/EnemyCompositionType.cs (enum)

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

[ ] State machine transitions through phases correctly
[ ] Anti-flip-flop prevents rapid plan changes
[ ] Legitimate emergencies bypass anti-flip-flop
[ ] Enemy composition correctly classified
[ ] Counter-formations selected appropriately
[ ] Logs show: "[BattleAI] Phase:Advancing→Engaging (ContactMade)"
[ ] Logs show: "[BattleAI] PlanChange BLOCKED (Cooldown:15s remaining)"
[ ] Build succeeds
```

---

# Phases 16-18: Advanced Polish (Combined)

**Goal:** Agent micro-tactics, combat director, coordinated retreat details, reinforcement details

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-15 complete

```
I need you to implement Phases 16-18 (Advanced Polish) of the Battle AI system for my Bannerlord mod.

These are advanced polish phases that complete the system, including the Agent Micro-Tactics System.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1-15 exist before starting)
═══════════════════════════════════════════════════════════════════════════════

Verify these key files exist from previous phases:
[ ] src/Features/Combat/BattleOrchestrator.cs
[ ] src/Features/Combat/RetreatCoordinator.cs (Phase 7 - you'll extend)
[ ] src/Features/Combat/ReinforcementManager.cs (Phase 9 - you'll extend)
[ ] src/Features/Combat/PlanExecutionStateMachine.cs (Phase 15)
[ ] src/Features/Combat/AgentCombatTuner.cs (Phase 2)
[ ] src/Features/Combat/FormationOrganizer.cs (Phase 3 - self-organizing ranks)

If missing, complete earlier phases first.

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 16 items (16.1-16.7, including 16.6a-f for agent micro-tactics)
   - Section 2: Phases 17, 18 items
   - Section 2b: Edge Cases for Phases 16-18
   - Section 3.10: Agent-Level Combat Director
   - Section 3.11: Agent Micro-Tactics System (CRITICAL - NEW)
   - Section 3.12: Coordinated Retreat System

2. docs/Features/Combat/battle-ai-plan.md
   - Part 21: Agent-Level Combat Director
   - Part 18: Coordinated Retreat
   - Part 20: Reinforcement Intelligence (details)


═══════════════════════════════════════════════════════════════════════════════
PHASE 16.6: AGENT MICRO-TACTICS SYSTEM (CRITICAL - NEW)
═══════════════════════════════════════════════════════════════════════════════

This system completes our three-layer architecture by adding agent-level micro-decisions.
Individual agents make small tactical adjustments while staying within formation bounds and
respecting orchestrator strategy. This creates realistic individual combat behavior without
sacrificing formation cohesion or strategic coordination.

DESIGN PHILOSOPHY:
- Agents are NOT autonomous - they operate within strict bounds
- Micro-decisions support formation objectives (from orchestrator)
- Only middle ranks get autonomy (front/rear follow orders exactly)
- Formation stability gates prevent chaos during critical moments
- All decisions constrained to small radius (2m-8m depending on context)

16.6a AGENT SITUATION SAMPLING
- Sample nearby agents within 10m radius
- Detect:
  - AlliesNearby: count of allies within 10m
  - EnemiesNearby: count of enemies within 10m
  - NearestAlly: closest ally agent
  - NearestEnemy: closest enemy agent
  - MostThreatenedAlly: ally with worst enemy:ally ratio
  - HighGround: best terrain position nearby
  - IsIsolated: < 2 allies within 10m
  - IsOutnumbered: enemies > allies * 1.5

16.6b MICRO-DECISION EVALUATION (Utility-Based)
- Decision types:
  1. Attack: Press toward nearest enemy (default aggressive)
  2. BackStep: Step back 1-2m (outnumbered, defensive)
  3. FindAlly: Move toward nearest ally (isolated, seeking support)
  4. FlankLeft: Sidestep left (enemy exposed on left)
  5. FlankRight: Sidestep right (enemy exposed on right)
  6. SupportAlly: Move toward embattled ally (ally outnumbered nearby)
  7. SeekAdvantage: Move to better position (height/cover available)

- Each decision scored 0-1 based on:
  - Formation objective (Attack → +0.3 to Attack decision)
  - Local superiority (Outnumbered → +0.4 to BackStep decision)
  - Agent health (< 40 HP → +0.3 to BackStep decision)
  - Isolation (< 2 allies → +0.6 to FindAlly decision)
  - Terrain (High ground nearby → +0.5 to SeekAdvantage)

- Pick highest scoring decision

16.6c DECISION EXECUTION
- Execute chosen decision as micro-movement
- Calculate desired position based on decision
- Constrain to autonomy radius (see 16.6f)
- Apply movement to agent

16.6d RANK-BASED AUTONOMY (CRITICAL)
- Front rank (FormationRankIndex == 0): NO micro-tactics
  - These are heavy troops holding the line
  - Need solid, stable front
  - They follow formation orders EXACTLY
- Middle ranks (FormationRankIndex == 1-2): MICRO-TACTICS ACTIVE
  - These troops can make small adjustments
  - Within 3-5m of formation position
  - Still constrained by formation
- Rear ranks (FormationRankIndex >= 3): NO micro-tactics
  - These are ranged troops or reserves
  - They follow formation orders EXACTLY

Integration with Phase 14 (Self-Organizing Ranks):
- Phase 14 already put heavy troops in front (rank 0)
- Phase 14 already put light troops in rear (rank 3+)
- Micro-tactics ONLY for middle ranks (1-2)
- This ensures front line stability while allowing middle ranks to adapt

16.6e FORMATION STABILITY GATES
Before allowing micro-tactics, check:
- Formation integrity: DeviationOfPositionsExcludeFarAgents < 15f (not scattered)
- Casualty rate: CasualtyRatio < 0.2 (not taking heavy losses)
- Reorganization state: not actively gap filling or reorganizing
- Movement orders: no active charge/advance orders (respect formation movement)

If ANY gate fails → disable micro-tactics, follow formation orders exactly

16.6f DYNAMIC PARAMETER ADJUSTMENT
Autonomy radius adapts to context:
- Base: 3m
- Main Effort formation: 2m (tighter control)
- Objective Hold/Screen: 3m (tight)
- Objective Attack/Pin: 5m (moderate)
- Objective FightingRetreat: 8m (loose, survival)
- Battle Phase Crisis: radius * 0.8 (tighten up)

Clamp to 2m-8m range

KEY INTEGRATION POINTS:
- Orchestrator provides: battle plan, phase, formation objectives
- Formation Organization (Phase 14) provides: rank assignment (heavy in front)
- Cohesion Management (Phase 16.7) provides: autonomy radius by objective
- Micro-Tactics (Phase 16.6) provides: small adjustments within bounds

ARCHITECTURE:
```
┌─────────────────────────────────────────────────────────┐
│  ORCHESTRATOR → Battle plan, phase, objectives          │
└──────────────────────┬──────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────┐
│  FORMATION ORGANIZATION (Phase 14)                      │
│  Heavy troops → Front rank (0)                          │
│  Light troops → Rear rank (3+)                          │
└──────────────────────┬──────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────┐
│  RANK-BASED AUTONOMY (Phase 16.6d)                      │
│  Front (0): NO micro    Middle (1-2): MICRO    Rear: NO │
└──────────────────────┬──────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────┐
│  STABILITY GATES (Phase 16.6e)                          │
│  Check: integrity, casualties, reorganization           │
└──────────────────────┬──────────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────────┐
│  MICRO-TACTICS (Phase 16.6a-c)                          │
│  Sample → Evaluate → Execute → Constrain               │
└─────────────────────────────────────────────────────────┘
```

═══════════════════════════════════════════════════════════════════════════════
PHASE 16.1-16.5: COMBAT DIRECTOR (Drama & Cinematic Moments)
═══════════════════════════════════════════════════════════════════════════════

16.1 Champion Duel System
- Detect hero vs hero engagement
- Create "duel zone" where other agents don't interfere
- Allow dramatic 1v1 fights

16.2 Last Stand Scenarios
- Few agents remaining + surrounded
- Boost aggression, reduce blocking (go out fighting)
- Dramatic final moments

16.3 Banner Bearer Drama
- Identify banner bearers
- AI prioritizes protecting own banners
- AI prioritizes capturing enemy banners

16.4 Small Squad Actions
- 2-5 man coordinated assaults
- Synchronized attack on single target
- Used for elite squads

16.5 Integration with Formation AI
- Agent drama respects formation orders
- Never override critical tactical orders
- Drama is enhancement, not replacement

16.7 Formation Cohesion Management
- Provides autonomy radius to Phase 16.6f
- Coordinates with micro-tactics system

═══════════════════════════════════════════════════════════════════════════════
PHASE 17: COORDINATED RETREAT (Details)
═══════════════════════════════════════════════════════════════════════════════

17.1 Retreat Decision Logic
- When to retreat vs fight to death
- Consider: casualties, morale, escape route

17.2 Covering Force (Rearguard)
- Designate units to cover retreat
- Rearguard fights while main force withdraws

17.3 Step-by-Step Withdrawal
- Bounding overwatch: move, cover, move
- Organized fallback, not rout

17.4 Preserving Force
- When battle is lost, minimize casualties
- Don't throw lives away

17.5 Rally Points
- Calculate safe rally locations
- Regroup after retreat

═══════════════════════════════════════════════════════════════════════════════
PHASE 18: REINFORCEMENT DETAILS
═══════════════════════════════════════════════════════════════════════════════

18.1 Big Wave Strategic Impact
- 100+ troops = major battlefield event
- Coordinate timing for maximum impact

18.2 Wave Coordination Between Sides
- Time waves to counter enemy waves

18.3 Desperation Waves
- All-in spawning when losing badly
- Emergency reinforcement

18.4 Spawn Point Defense/Attack
- Protect own spawn
- Harass enemy spawn

18.5 Merge vs Second Line Decision
- Fresh troops join existing formations OR
- Form new second line
- Based on tactical need

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

Phase 16.6 (Agent Micro-Tactics):
1. src/Features/Combat/AgentMicroTacticsSystem.cs
2. src/Features/Combat/AgentSituationSampler.cs
3. src/Features/Combat/MicroDecisionEvaluator.cs
4. src/Features/Combat/Models/MicroDecision.cs (enum)
5. src/Features/Combat/Models/TacticalSituation.cs (data class)

Phase 16.1-16.5 (Combat Director):
6. src/Features/Combat/AgentCombatDirector.cs
7. src/Features/Combat/ChampionDuelManager.cs

Phase 17-18:
8. src/Features/Combat/CoordinatedRetreatManager.cs
9. src/Features/Combat/ReinforcementWaveManager.cs

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

Phase 16.6 (Agent Micro-Tactics):
[ ] Agent situation sampling detects allies/enemies within 10m
[ ] Utility scoring produces normalized values (0-1) for each decision
[ ] Best decision selected based on highest utility score
[ ] Rank-based autonomy: front rank (0) gets NO micro-tactics
[ ] Rank-based autonomy: middle ranks (1-2) get MICRO-TACTICS
[ ] Rank-based autonomy: rear ranks (3+) get NO micro-tactics
[ ] Formation stability gates block micro-tactics when formation scattered (deviation > 15f)
[ ] Formation stability gates block micro-tactics when casualties > 20%
[ ] Formation stability gates block micro-tactics during active movement orders
[ ] Dynamic radius adapts to objective (Hold: 3m, Attack: 5m, Retreat: 8m)
[ ] Micro-movements constrained to autonomy radius
[ ] Integration: heavy troops still in front ranks (Phase 14 preserved)
[ ] Integration: gap filling still active (middle ranks step up when front falls)
[ ] Logs show: "[BattleAI] Agent:X Rank:1 Decision:FlankLeft Score:0.72 Deviation:2.8m"

Phase 16.1-16.5 (Combat Director):
[ ] Champion duels occur between heroes
[ ] Last stand behavior triggers appropriately
[ ] Banner bearer drama active

Phase 17-18:
[ ] Coordinated retreat with covering force
[ ] Step-by-step withdrawal working
[ ] Reinforcement waves timed strategically
[ ] Desperation waves trigger when losing

General:
[ ] Build succeeds
[ ] No crashes or exceptions
[ ] All ReSharper/repo lint warnings fixed

═══════════════════════════════════════════════════════════════════════════════
HANDOFF NOTES
═══════════════════════════════════════════════════════════════════════════════

When complete, provide:

FILES CREATED: (list all)
FILES MODIFIED: (list all)

KEY MICRO-TACTICS DECISIONS:
- How did you implement utility scoring?
- How did you handle rank detection?
- How did you integrate with formation organization?

KEY DECISIONS: (list any architectural choices)
KNOWN ISSUES: (list any incomplete items)
VERIFICATION PASSED: (list what you tested)
```

---

# Phase 19: Battlefield Realism Enhancements

**Goal:** Add depth systems that make battles feel like real combat - ammunition depletion, line relief, morale contagion, tactical deception, high ground tactics

**Status:** ⬜ TODO

**Prerequisites:** Phase 1-18 complete (especially Phase 14 formation organization and Phase 16 agent systems)

```
I need you to implement Phase 19 (Battlefield Realism Enhancements) of the Battle AI system for my Bannerlord mod.

These systems add realistic constraints and human elements that make battles more dynamic and cinematic.

═══════════════════════════════════════════════════════════════════════════════
CRITICAL PROJECT CONSTRAINTS (from BLUEPRINT.md)
═══════════════════════════════════════════════════════════════════════════════

GAME TARGET: Bannerlord v1.3.13
API VERIFICATION: Use local decompile at C:\Dev\Enlisted\Decompile\
PROJECT BUILD: dotnet build -c "Enlisted RETAIL" /p:Platform=x64
CSPROJ: Manually add new files with <Compile Include="..."/>
LOGGING: Use ModLogger.Info("BattleAI", "message");
CODE QUALITY: Follow ReSharper/repo lint strictly, fix all warnings, use braces, no changelog comments

═══════════════════════════════════════════════════════════════════════════════
CONTEXT RECOVERY (Verify Phase 1-18 exist before starting)
═══════════════════════════════════════════════════════════════════════════════

Verify these key files exist from previous phases:
[ ] src/Features/Combat/BattleOrchestrator.cs
[ ] src/Features/Combat/FormationOrganizer.cs (Phase 3 - ranks system)
[ ] src/Features/Combat/AgentMicroTacticsSystem.cs (Phase 16)
[ ] src/Features/Combat/AgentCombatTuner.cs (Phase 2)
[ ] src/Features/Combat/RetreatCoordinator.cs (Phase 7)

If missing, complete earlier phases first.

═══════════════════════════════════════════════════════════════════════════════
DOCUMENTATION TO READ
═══════════════════════════════════════════════════════════════════════════════

1. docs/Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md
   - Section 2: Phase 19 items (19.1-19.9)
   - Section 2b: Edge Cases for Phase 19
   - Section 3.12: Battlefield Realism Systems (9 systems, API verified)

2. DECOMPILE REFERENCE:
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Agent.cs
   - C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Formation.cs
   - Check for: ammo APIs, morale APIs, fatigue systems (if any)

═══════════════════════════════════════════════════════════════════════════════
PHASE 19 SYSTEMS
═══════════════════════════════════════════════════════════════════════════════

19.1 AMMUNITION TRACKING & BEHAVIOR
- Track arrow/bolt/javelin count per agent (use weapon.MaxDataValue or estimate)
- Formation-level ammo status: Fresh (>50%), Low (<25%), Depleted (0%)
- Behavior changes: conserve when low, switch to melee when depleted
- Orchestrator notified when archer formation loses ranged capability
- Agents near fallen allies can scavenge ammo (5 sec action, only when safe)

19.2 LINE RELIEF & ROTATION
- Active rotation of front-line (rank 0) with middle ranks (rank 1)
- Triggers: front rank casualties > 40%, lull (no melee 15+ sec), or extended engagement (>60 sec)
- Sequence: front steps back, middle steps forward, ranks swap
- Only if formation > 40 troops (need depth for rotation)
- Not during active charges or retreats
- Brief stabilization period (5 sec) after rotation

19.3 MORALE CONTAGION
- Fear/courage spreads between nearby agents (5m radius)
- Emotions: Fear (routing ally), Courage (rallying cry), Panic (surrounded), Confidence (winning)
- Effects: Fear -10 morale, Courage +10, Panic -20, Confidence +15
- Max 3 propagation hops (doesn't spread infinitely)
- Officers immune to fear contagion
- Formation morale has floor (won't drop below 10 from contagion)

19.4 COMMANDER DEATH RESPONSE
- Formation behavior changes when leader killed
- Temporary confusion period (10-15 sec reduced effectiveness)
- NCO/hero takes over with transition delay
- Multiple deaths compound (cumulative penalty)

19.5 BREAKING POINT DETECTION
- Detect specific formation crack moments:
  - Flanked + casualties > 30%
  - Surrounded (enemies on 3+ sides)
  - Leader dead + losing melee
- Trigger coordinated collapse OR last stand (depends on context)
- Require sustained conditions (10+ sec) before triggering

19.6 FEINT MANEUVERS
- Orchestrator orders fake attacks to draw enemy reserves
- Execution: advance at 60% speed (loose formation), enemy responds, retreat, main effort attacks
- Success: enemy reserve moves > 50m toward feint
- Failure: if enemy doesn't react after 30s, convert to real attack
- Use when: enemy has large reserve (>30%), we need to create opening

19.7 PURSUIT DEPTH CONTROL
- Calculated pursuit distance (don't chase too far)
- Stop at 200m or terrain boundary
- Recall if enemy rallies (reforms > 50% of original strength)
- Log: "[BattleAI] Pursuit HALT Distance:195m Reason:TerrainBoundary"

19.8 BANNER RALLYING POINTS
- Banners act as visual rally points for scattered troops
- Agents within 15m: +5 morale
- Scattered agents gravitate toward banner when reforming
- If bannerman killed, nearby sergeant/hero picks up (automatic transfer)
- Banner loss: -15 morale penalty

19.9 HIGH GROUND PREFERENCE
- Agents prefer elevated positions when available (simple Z-axis comparison)
- Formations position for height advantage over enemy (agent.Z > enemy.Z + 2m)
- Orchestrator includes height advantage in terrain suitability scoring
- Only applied when not actively engaged in melee
- Log: "[BattleAI] Formation:Infantry SeekingHighGround ElevationAdvantage:+4.2m"

═══════════════════════════════════════════════════════════════════════════════
FILES TO CREATE
═══════════════════════════════════════════════════════════════════════════════

Ammunition:
1. src/Features/Combat/AmmunitionTracker.cs
2. src/Features/Combat/ArcherAmmoManager.cs

Line Relief:
3. src/Features/Combat/LineReliefManager.cs
4. src/Features/Combat/FormationRotationCoordinator.cs

Morale:
5. src/Features/Combat/MoraleContagionSystem.cs
6. src/Features/Combat/Models/EmotionType.cs (enum)

Tactical:
7. src/Features/Combat/FeintManager.cs
8. src/Features/Combat/BannerRallySystem.cs
9. src/Features/Combat/BreakingPointDetector.cs
10. src/Features/Combat/CommanderDeathHandler.cs

Terrain:
11. src/Features/Combat/HighGroundPositioningSystem.cs

═══════════════════════════════════════════════════════════════════════════════
ACCEPTANCE CRITERIA
═══════════════════════════════════════════════════════════════════════════════

Ammunition (19.1):
[ ] Agents track current ammo count
[ ] Formation detects when > 60% archers depleted
[ ] Depleted archers switch to melee weapons
[ ] Orchestrator notified of archer formation ammo depletion
[ ] Logs show: "[BattleAI] Formation:Archers AmmoStatus:Low (32%) Conserving:true"

Line Relief (19.2):
[ ] Front rank rotates with middle rank during lulls
[ ] Rotation triggers at 40% casualties, lull (15+ sec), or extended engagement (>60 sec)
[ ] Rotation disabled during active combat
[ ] Only formations > 40 troops can rotate
[ ] Brief stabilization period after rotation
[ ] Logs show: "[BattleAI] Formation:Infantry LineRelief FrontCasualties:42% Swapping ranks"

Morale Contagion (19.3):
[ ] Fear spreads from routing agents (5m radius)
[ ] Courage spreads from rallying agents
[ ] Officers immune to fear contagion
[ ] Max 3 propagation hops
[ ] Logs show: "[BattleAI] MoraleContagion Agent:X Emotion:Fear Intensity:0.7 Spread:3 agents"

Breaking Points (19.5):
[ ] Flanked + 30% casualties triggers break check
[ ] Surrounded formations enter crisis mode
[ ] Leader death compounds with other factors
[ ] Require sustained conditions (10+ sec)
[ ] Logs show: "[BattleAI] Formation:2 BREAKING_POINT Flanked:true Casualties:35% LeaderDead:true"

Feints (19.6):
[ ] Orchestrator can order feint maneuvers
[ ] Feinting formation advances at 60% speed
[ ] Detects if enemy responds (reserve moves)
[ ] Converts to real attack if enemy doesn't respond
[ ] Logs show: "[BattleAI] Feint EXECUTING Formation:Cavalry Target:EnemyCenter"

Banners (19.8):
[ ] Agents within 15m of banner get morale boost
[ ] Scattered agents move toward banner
[ ] Banner transfer when bearer killed
[ ] Banner loss causes morale penalty
[ ] Logs show: "[BattleAI] Banner RALLYING Agents:12 AvgDistance:8.3m"

High Ground (19.9):
[ ] Agents prefer elevated positions (Z-axis comparison)
[ ] Formations position for height advantage (agent.Z > enemy.Z + 2m)
[ ] Only applied when not actively in melee
[ ] Orchestrator scores terrain by elevation advantage
[ ] Logs show: "[BattleAI] Formation:Infantry SeekingHighGround ElevationAdvantage:+4.2m"

General:
[ ] Build succeeds
[ ] No crashes or exceptions
[ ] All ReSharper/repo lint warnings fixed
[ ] Systems integrate with existing orchestrator/formation architecture

═══════════════════════════════════════════════════════════════════════════════
HANDOFF NOTES
═══════════════════════════════════════════════════════════════════════════════

When complete, provide:

FILES CREATED: (list all)
FILES MODIFIED: (list all)

KEY DECISIONS:
- How did you detect ammo counts? (weapon API? estimation?)
- How did you implement morale contagion propagation?
- How did you implement high ground detection?

INTEGRATION POINTS:
- How does ammo depletion notify orchestrator?
- How does line relief integrate with formation ranks?
- How does high ground preference affect formation positioning?

KNOWN ISSUES: (list any incomplete items)
VERIFICATION PASSED: (list what you tested)
```

---

# Quick Reference: Build & Test

```powershell
# Build command (PowerShell - use semicolon, not &&)
cd C:\Dev\Enlisted\Enlisted; dotnet build -c "Enlisted RETAIL" /p:Platform=x64

# Logs output location
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\

# Decompile reference (API verification)
C:\Dev\Enlisted\Decompile\

# Key decompile files for Battle AI:
C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Mission.cs
C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\MissionBehavior.cs
C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Team.cs
C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\TeamQuerySystem.cs
C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Formation.cs
C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\FormationQuerySystem.cs
C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\Agent.cs
C:\Dev\Enlisted\Decompile\TaleWorlds.MountAndBlade\AgentDrivenProperties.cs
C:\Dev\Enlisted\Decompile\SandBox\SandboxAgentStatCalculateModel.cs
```

---

# Handoff Template

Use this template after completing any phase:

```
═══════════════════════════════════════════════════════════════════════════════
PHASE [X] HANDOFF NOTES
═══════════════════════════════════════════════════════════════════════════════

COMPLETED: [DATE]
BY: [AI MODEL]

FILES CREATED:
- [ ] path/to/file1.cs
- [ ] path/to/file2.cs

FILES MODIFIED:
- [ ] path/to/existing1.cs
- [ ] path/to/existing2.cs

ADDED TO ENLISTED.CSPROJ:
- [ ] <Compile Include="path\to\file1.cs"/>
- [ ] <Compile Include="path\to\file2.cs"/>

KEY APIs VERIFIED IN DECOMPILE:
- API1: exists, works as expected
- API2: exists, slight difference from docs

KEY DECISIONS MADE:
- Decision 1: chose X over Y because Z
- Decision 2: implemented using pattern A

THRESHOLDS/VALUES USED:
- Threshold 1: value
- Threshold 2: value

KNOWN ISSUES/TECH DEBT:
- Issue 1: description, future fix needed
- Issue 2: description

VERIFICATION PASSED:
[ ] Build succeeds
[ ] Logs appear correctly
[ ] No runtime errors
[ ] Feature works as expected

READY FOR PHASE [X+1]: YES/NO
```

---

**Last Updated:** 2025-12-31  
**Status:** Prompts Ready (Implementation Not Started)
