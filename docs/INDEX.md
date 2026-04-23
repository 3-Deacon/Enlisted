# Enlisted Documentation - Complete Index

**Summary:** Master index of all documentation files organized by category. Use this to find documentation for specific topics or systems.

**Last Updated:** 2026-04-22
**Total Documents:** 44

> **Note:** Documents marked "⚠️ Mixed" have core features implemented but also contain planned/designed features not yet in code. Check their Implementation Checklist sections for details.

---

## Quick Start Guide

**New to this project? Start here:**

1. **Read [BLUEPRINT.md](BLUEPRINT.md)** - Architecture, coding standards, project constraints
2. **Read this INDEX** - Navigate to relevant docs for your task
3. **Read [Features/Core/core-gameplay.md](Features/Core/core-gameplay.md)** - Best overview of how systems work together

**Finding What You Need:**

| I need to... | Go to... |
| --- | --- |
| Understand the project scope | [BLUEPRINT.md](BLUEPRINT.md) → "Quick Orientation" |
| Learn core game mechanics | [Features/Core/core-gameplay.md](Features/Core/core-gameplay.md) |
| Understand rank progression | [Features/Core/promotion-system.md](Features/Core/promotion-system.md) |
| Find a specific feature quickly | [Feature Lookup Quick Reference](#feature-lookup-quick-reference) ⭐ NEW |
| Find how a feature works | Search this INDEX, check [Features/Core/](#core-systems) |
| See all events/decisions/orders | [Features/Content/orders-content.md](Features/Content/orders-content.md) |
| Verify Bannerlord APIs | [Reference/native-apis.md](Reference/native-apis.md) |
| Build/deploy the mod | [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md) |
| Create new documentation | [BLUEPRINT.md](BLUEPRINT.md) → "Creating New Documentation" |

**Documentation Structure:**

- **Features/** - How systems work (organized by category: Core, Equipment, Combat, etc.)
- **Content/** - Complete catalog of events, decisions, orders, map incidents
- **Reference/** - Technical references: native APIs, AI analysis, skill mechanics

---

## Index

1. [Root Documentation](#root-documentation)
2. [Feature Lookup Quick Reference](#feature-lookup-quick-reference)
3. [Features](#features)
4. [Content & Narrative](#content--narrative)
5. [Reference & Research](#reference--research)

---

## Feature Lookup Quick Reference

**Can't find a specific feature? Use this lookup table:**

| Feature / System | Found In Document | Section |
| --- | --- | --- |
| **Company Needs** | [camp-life-simulation.md](Features/Campaign/camp-life-simulation.md) | 2 transparent metrics (Readiness/Supply) - Rest removed 2026-01-11 |
| **StoryDirector (Pacing)** ✅ Phase 3 migration complete | [design](superpowers/specs/2026-04-18-event-pacing-design.md) / [plan](superpowers/plans/2026-04-18-event-pacing.md) / [verification](superpowers/plans/2026-04-18-event-pacing-verification.md) | All 15 production `QueueEvent` sites route through `StoryDirector.EmitCandidate`. Modal firing is gated by a 5-day in-game floor + 60s wall-clock floor + per-category cooldown. `ChainContinuation` flag bypasses the in-game floor and category cooldown for promotions, chain events, and bag checks (still honors wall-clock). Deferred-interactive retry queue flushes one entry per DailyTick (cap 32). Playtest verification scenarios in the verification doc. |
| **ModLogger.Error retirement** ✅ Committed 2026-04-19 | [design](superpowers/specs/2026-04-19-error-warn-cleanup-design.md) / [plan](superpowers/plans/2026-04-19-error-warn-cleanup.md) | Retired `ModLogger.Error(...)` from the public API. All 363 call sites reclassified to `Surfaced` / `Caught` / `Expected`. Phase 11 validation gate prevents reintroduction. `ModLogger.Warn` retained as log-only primitive. |
| **Spec 2 Orders Surface** 🛠 Code shipped, content migrated to five-plan roadmap | [design](superpowers/specs/2026-04-20-orders-surface-design.md) / [archived plan](superpowers/plans/archive/2026-04-20-orders-surface.md) | Replaced the legacy `src/Features/Orders/` subsystem (deleted 2026-04-21 in `a8719bb`) with `OrderActivity` on the storylet backbone. Phase A code + Phase B migrations + validator rails shipped; Tasks 33/34 named-order T1-T6 archetypes authored (~100 storylets). Remaining ambient-pool / transition / path-crossroads / polish tasks migrated into the five-plan integration roadmap (see below). |
| **Five-plan integration roadmap** ✅ Plans 1+2+3+4 shipped 2026-04-22; 🟡 Plan 5 code-complete, smoke pending | [integration spec](superpowers/specs/2026-04-21-plans-integration-design.md) / [intelligence design](superpowers/specs/2026-04-21-enlisted-campaign-intelligence-design.md) / [Plan 1](superpowers/plans/2026-04-21-campaign-intelligence-backbone.md) / [Plan 2](superpowers/plans/2026-04-21-lord-ai-intervention.md) / [Plan 3](superpowers/plans/2026-04-21-signal-projection.md) / [Plan 3 verification](superpowers/plans/2026-04-21-signal-projection-verification.md) / [Plan 4](superpowers/plans/2026-04-21-duty-opportunities.md) / [Plan 4 verification](superpowers/plans/2026-04-21-duty-opportunities-verification.md) / [Plan 5](superpowers/plans/2026-04-21-career-loop-closure.md) / [Plan 5 verification](superpowers/plans/2026-04-21-career-loop-verification.md) / [Playtest runbook A-H](superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md) | Merges Spec 2 remainder with Campaign Intelligence Backbone into five sequenced plans. Plan 1 (snapshot backbone) shipped on `development` 2026-04-22 (`34322d4` → `bc13096`, 23 commits) — `EnlistedCampaignIntelligenceBehavior.Current` accessor live, first `INTEL/hourly_recompute` heartbeats confirmed in-game; T28 in-game smoke pending human operator. Plan 2 (Lord AI Intervention) shipped on `development` 2026-04-22 (`4929246` → `f289b9b`) — three MBGameModel wrappers (`EnlistedTargetScoreModel`, `EnlistedArmyManagementModel`, `EnlistedMobilePartyAiModel`) registered via `SubModule.OnGameStart`, every override gated per-call on `EnlistedAiGate.TryGetSnapshotForParty` with vanilla fallthrough for non-enlisted parties; throttled `EnlistedAiBiasHeartbeat` routes bias events under the `INTELAI` category. Phase H smoke passed 2026-04-22 — load-bearing T13 bait-break validated in-game (score 276→0 zeroing; lord pivoted `OffensiveSiege` → `FrontierDefense`). Phase F narrow Harmony not required. Log excerpt: [plan2-phase-h-log.md](Features/CampaignIntelligence/plan2-phase-h-log.md). Plan 3 (Signal Projection) shipped on `development` 2026-04-22 (`5fedf88` → `189e75c`, 26 commits) — SignalBuilder pure projector, EnlistedSignalEmitterBehavior with throttled hourly emission + cooldown + diversity picker, StoryCandidate signal-metadata fields, DailyDriftApplicator rewired through signal pipeline, 48 authored floor storylets across 10 signal families; T28 14-day in-game smoke pending human operator. Plan 4 (duty opportunities + ambient pools + transitions) shipped 2026-04-22 (`ca22111..0c519d3`, 22 commits). 🟡 Plan 5 (Career Loop Closure) code-complete on `development` 2026-04-22 — Half A (`4f66604` → `e35f445`) + Half B authoring (culture_overlays.json 45 + lord_trait_overlays.json 15) + debug hotkeys + verification doc; 105 Plan 5 storylets total across 8 JSON files; T10 in-game smoke + cross-plan Scenario H fast-forward soak pending human operator. Plan 5 day-to-day reference: [career-loop.md](Features/Content/career-loop.md). Plan 1 owns save-definer class offset 48; Plan 3 owns class offset 49 + enum offsets 99-103; Plan 4 owns class offset 53 (`DutyCooldownStore`); Plan 5 persists entirely through existing `QualityStore` + `FlagStore` offsets — no new offsets (see AGENTS.md Rule #11). |
| **Injuries & Illnesses** | [injury-system.md](Features/Content/injury-system.md) | Unified condition tracking, maritime context awareness |
| **Camp Hub Decisions** | [camp-life-simulation.md](Features/Campaign/camp-life-simulation.md) | 33 player-initiated decisions |
| **Orchestrator Camp Simulation** | [camp-simulation-system.md](Features/Campaign/camp-simulation-system.md) | Background + Opportunities layers |
| **Camp Opportunities** | [camp-simulation-system.md](Features/Campaign/camp-simulation-system.md) | 36 contextual activities with learning |
| **Camp Opportunity Hints** | [camp-simulation-system.md](Features/Campaign/camp-simulation-system.md) | Narrative foreshadowing in Daily Brief (camp rumors + personal hints) |
| **Opportunity Pre-Scheduling** | [camp-simulation-system.md](Features/Campaign/camp-simulation-system.md) | 24h ahead locking, prevents disappearance on context changes |
| **Camp Background** | [camp-simulation-system.md](Features/Campaign/camp-simulation-system.md) | Autonomous roster tracking, incidents |
| **Camp Routine Schedule** | [camp-routine-schedule-spec.md](Features/Campaign/camp-routine-schedule-spec.md) | Baseline daily routine with deviations |
| **Camp Hub (Custom Gauntlet)** | [camp-hub-custom-gauntlet.md](Features/UI/camp-hub-custom-gauntlet.md) | — |
| **Combat Log (Enlisted)** | [enlisted-combat-log.md](Features/UI/enlisted-combat-log.md) | ✅ Native-styled scrollable feed with faction-colored encyclopedia links |
| **Company Events** | [company-events.md](Features/Core/company-events.md) | — |
| **Companion Integration** | [companion-management.md](Features/Core/companion-management.md) | — |
| **Contextual Dialogue** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Contextual Dialogue System |
| **Discharge Process** | [enlistment.md](Features/Core/enlistment.md) | Discharge |
| **Discounts (QM)** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Reputation System |
| **Enlistment** | [enlistment.md](Features/Core/enlistment.md) | — |
| **Equipment Purchasing** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Equipment Purchasing |
| **Equipment Quality/Tiers** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Equipment Quality System |
| **Equipment Upgrades** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Equipment Upgrade System |
| **Event System (JSON)** | [storylet-backbone.md](Features/Content/storylet-backbone.md) | Storylet schema, triggers, slots, effects |
| **Writing RP Text (Style)** | [writing-style-guide.md](Features/Content/writing-style-guide.md) | Voice, tone, vocabulary, opportunity hints for Bannerlord flavor |
| **Storylet Backbone (Spec 0)** | [storylet-backbone.md](Features/Content/storylet-backbone.md) | Storylets, Qualities, Flags, Activities, Scopes, Chains, Scripted Effects — content layer beneath StoryDirector (living reference; design spec retired) |
| **Enlisted Home Surface (Spec 1)** ✅ Implementation complete | [reference](Features/Content/home-surface.md) / [design (frozen)](superpowers/specs/2026-04-19-enlisted-home-surface-design.md) | Four-phase HomeActivity (Arrive/Settle/Evening/Break Camp) with five player-choice intents. Shipped 2026-04-20 on `development`; full commit trail `fc93285` → `0390fdf` across Phases A–E plus three post-Phase-E production fixes (csproj ModuleData mirror, `Ctrl+Shift+T`→`Y` hotkey rebind per `DebugHotKeyCategory` decompile audit, `ActivityRuntime.ForceAdvancePhase` to cross Auto phase time gates). Living day-to-day reference is [`home-surface.md`](Features/Content/home-surface.md); design spec is frozen. |
| **Career Loop Closure (Plan 5)** 🟡 Code-complete, smoke pending | [reference](Features/Content/career-loop.md) / [plan](superpowers/plans/2026-04-21-career-loop-closure.md) / [verification](superpowers/plans/2026-04-21-career-loop-verification.md) / [playtest runbook A-H](superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md) | Terminal plan of the five-plan integration roadmap. Five paths (`ranger` / `enforcer` / `support` / `diplomat` / `rogue`) score from skill gains + intent picks; T4/T6/T9 crossroads let the player commit or resist; T7+ named-order variants gate on `committed_path_<path>` flag; culture overlays (`__<culture>` suffix) + lord-trait gating add flavor. Adds two effect primitives (`commit_path`, `resist_path`) + `QualityStore.SetDirect` back-door + `PathCrossroadsBehavior` + `CareerDebugHotkeysBehavior` (Ctrl+Shift+I/A/O/F) + `validate_content.py` Phase 15 full enforcement. 105 authored storylets. No new save-definer offsets. Living reference is [`career-loop.md`](Features/Content/career-loop.md). |
| **Event Pacing (delivery)** | [2026-04-18-event-pacing-design.md](superpowers/specs/2026-04-18-event-pacing-design.md) | StoryDirector tier selection + pacing floors; the delivery half that Spec 0's content feeds |
| **First Meeting (QM)** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Quartermaster NPC |
| **Food & Rations** | [provisions-rations-system.md](Features/Equipment/provisions-rations-system.md) | — |
| **Battle AI** | [battle-ai-plan.md](Features/Combat/battle-ai-plan.md) | Native AI analysis, Orchestrator proposal |
| **Agent Combat AI** | [agent-combat-ai.md](Features/Combat/agent-combat-ai.md) | Individual soldier AI tuning, 40+ properties |
| **Formation Assignment** | [formation-assignment.md](Features/Combat/formation-assignment.md) | — |
| **Leave System** | [temporary-leave.md](Features/Campaign/temporary-leave.md) | — |
| **Muster System (Pay Day Ceremony)** | [muster-system.md](Features/Core/muster-system.md) | Menu Flow, All 8 Stages |
| **Officers Armory** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Officers Armory |
| **Onboarding** | [enlistment.md](Features/Core/enlistment.md) | Onboarding |
| **Orders (Chain of Command)** | [order-progression-system.md](Features/Core/order-progression-system.md) | — |
| **Pay & Wages** | [pay-system.md](Features/Core/pay-system.md) | — |
| **Progression System** | [storylet-backbone.md](Features/Content/storylet-backbone.md) | Generic probabilistic daily rolls for escalation tracks |
| **Promotion & Rank Progression** | [promotion-system.md](Features/Core/promotion-system.md) | — |
| **Provisions Shop** | [provisions-rations-system.md](Features/Equipment/provisions-rations-system.md) | T7+ Officer Provisions |
| **Quartermaster System** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | — |
| **Reputation (QM)** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Reputation System |
| **Reputation (Soldier)** | [identity-system.md](Features/Identity/identity-system.md) | — |
| **Retinue (Commander's)** | [retinue-system.md](Features/Core/retinue-system.md) | — |
| **Supply Tracking** | [company-supply-simulation.md](Features/Equipment/company-supply-simulation.md) | — |
| **Tier Gates/Restrictions** | [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Multiple sections |
| **Town Access** | [town-access-system.md](Features/Campaign/town-access-system.md) | — |
| **Training & XP** | [training-system.md](Features/Combat/training-system.md) | — |
| **Traits & Identity** | [identity-system.md](Features/Identity/identity-system.md) | — |

## Root Documentation

**Start here for entry points and core reference:**

| Document | Purpose | Status |
| --- | --- | --- |
| [README.md](README.md) | Main entry point and mod overview | ✅ Current |
| [BLUEPRINT.md](BLUEPRINT.md) | Project architecture and coding standards | ✅ Current |
| [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md) | Build guide, development patterns, and the active repo lint stack | ✅ Current |
| [../Tools/README.md](../Tools/README.md) | Validation tools, lint entry points, and maintenance scripts | ✅ Current |
| [../Tools/TECHNICAL-REFERENCE.md](../Tools/TECHNICAL-REFERENCE.md) | Logging, save-system patterns, and repo tooling config | ✅ Current |

---

## Features

### Core Systems

**Location:** `Features/Core/`

| Document | Topic | Status |
| --- | --- | --- |
| [index.md](Features/Core/index.md) | Core features index | ✅ Current |
| [core-gameplay.md](Features/Core/core-gameplay.md) | Complete gameplay overview covering all major systems and how they interact | ✅ Current |
| [enlistment.md](Features/Core/enlistment.md) | Enlistment system: joining process, lord selection, initial rank assignment, contract terms | ✅ Current |
| [order-progression-system.md](Features/Core/order-progression-system.md) | Multi-day order execution: phase progression (4/day), slot events during duty, consequence accumulation, order forecasting with imminent warnings. 17 orders with 84 order events active. | ✅ Implemented |
| [promotion-system.md](Features/Core/promotion-system.md) | Rank progression T1-T9: XP sources (combat, orders, training), multi-factor requirements (service days, battles fought, reputation thresholds, scrutiny cap), proving events (rank-up challenges), culture-specific rank titles, equipment tier unlocks, officer privileges (T7+) | ✅ Current |
| [pay-system.md](Features/Core/pay-system.md) | Wages and payment: 12-day muster cycle, rank-based pay scales, wage modifiers (performance, reputation, lord wealth), pay tension (mutiny risk), deductions (fines, missing gear) | ✅ Current |
| [muster-system.md](Features/Core/muster-system.md) | Muster System: 6-stage GameMenu sequence for pay day ceremonies, rank progression display, period summary (12-day recap), event integration (recruit), comprehensive reporting (combat/training/orders/XP breakdown), pay options, promotion recap, retinue muster (T7+), direct Quartermaster access | ✅ Current |
| [company-events.md](Features/Core/company-events.md) | Company-wide events: march events, camp life events, morale events, supply events | ✅ Current |
| [retinue-system.md](Features/Core/retinue-system.md) | Commander's retinue (T7+ officers): formation selection (player chooses retinue troops), context-aware trickle (1-3 troops per battle), relation-based reinforcements, loyalty tracking, 11 narrative events (character development), 6 post-battle incidents (heroism/casualties), 4 camp decisions (discipline/rewards), named veterans (persistent characters) | ✅ Current |
| [companion-management.md](Features/Core/companion-management.md) | Companion integration: how companions work with enlisted systems, role assignments, special interactions | ✅ Current |

### Equipment & Logistics

**Location:** `Features/Equipment/`

| Document | Topic | Status |
| --- | --- | --- |
| [quartermaster-system.md](Features/Equipment/quartermaster-system.md) | Complete quartermaster system with 10+ subsystems: equipment purchasing (category browsing, reputation discounts 0-30%), quality modifiers (6 tiers affecting stats/prices), upgrade system (Gauntlet grid UI, sequential quality improvements with real stat bonuses, native ItemModifier system), buyback service (sell QM gear back at 30-65%), provisions/rations (T1-T6 issued, T7+ shop with Gauntlet grid UI), Officers Armory (T7+, elite gear), tier gates (rank-based access control), supply integration (equipment blocked <30%), first-meeting intro, contextual dialogue (150+ dynamic responses) | ✅ Current |
| [provisions-rations-system.md](Features/Equipment/provisions-rations-system.md) | Food and rations: T1-T6 issued rations (12-day cycle, reclaimed at muster, quality by rep), T7+ provisions shop (premium prices 2.0-3.2x town markets, stock by supply level, Gauntlet grid UI with rank-based button gating), provision bundles (morale/fatigue boosts) | ✅ Current |
| [company-supply-simulation.md](Features/Equipment/company-supply-simulation.md) | Company supply tracking (0-100% scale, includes rations, ammo, repairs, camp supplies), supply effects (equipment access gates, ration availability, QM greeting tone, stock levels), supply-based messaging | ⚠️ Mixed |
| [baggage-train-availability.md](Features/Equipment/baggage-train-availability.md) | Baggage train access gating: world-state-aware simulation (probabilities adapt to campaign situation), dynamic decision system (appears only when accessible), orchestrator integration, rank-based privileges, emergency access, 5 baggage events | ✅ Current |

### Identity & Traits

**Location:** `Features/Identity/`

| Document | Topic | Status |
| --- | --- | --- |
| [identity-system.md](Features/Identity/identity-system.md) | Trait and identity system: personality traits (acquired through events/actions), trait effects on events/dialogues/options, reputation tracking (soldier rep, QM rep, discipline), identity development over career | ✅ Current |

### Combat & Training

**Location:** `Features/Combat/`

| Document | Topic | Status |
| --- | --- | --- |
| [training-system.md](Features/Combat/training-system.md) | Training and XP: camp training actions (weapon drills, fitness), skill progression (XP rates, skill caps by rank), training events (success/injury/fatigue), cooldowns | ✅ Current |
| [formation-assignment.md](Features/Combat/formation-assignment.md) | Battle formation logic: T1-T6 soldiers auto-assigned to formation based on equipped weapons (bow→Ranged, horse→Cavalry, both→Horse Archer, melee→Infantry), teleported to formation position. T7+ commanders control their own party, no auto-assignment. | ✅ Current |
| [battle-ai-plan.md](Features/Combat/battle-ai-plan.md) | Battle AI upgrade plan: native AI analysis (architecture, tactics, behaviors, query systems, morale, terrain, siege), identified gaps, Battle Orchestrator proposal (commander-layer AI for reserves, concentration, coordinated withdrawal), modding entry points | 📋 Plan |
| [agent-combat-ai.md](Features/Combat/agent-combat-ai.md) | Agent-level combat AI: 40+ tunable properties (blocking, parrying, aiming, reactions, shield use), AgentStatCalculateModel, AI level calculation, BehaviorValueSet, modding entry points, example profiles (Veteran, Elite Guard) | 📋 Plan |
| [BATTLE-AI-IMPLEMENTATION-SPEC.md](Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md) | Master implementation checklist for all Battle AI phases: Orchestrator → Formation → Agent three-layer architecture, phase dependencies, field-battle scope (no siege/naval), status tracking | 📋 Plan |
| [advanced-tactical-behaviors.md](Features/Combat/advanced-tactical-behaviors.md) | Enhancement specs for formation intelligence (Phase 3), cavalry cycle manager (Phase 9), and agent combat AI (Phase 11) — builds on base implementation spec | 📋 Plan |
| [tactical-formation-behavior-enhancement.md](Features/Combat/tactical-formation-behavior-enhancement.md) | Orchestrator-integrated formation behaviors: intelligent position validation, context-aware positioning, dynamic threat response, archer behavior director — Phases 3 and 10 | 📋 Plan |
| [battle-scale-system-summary.md](Features/Combat/battle-scale-system-summary.md) | Battle scale detection: dynamic AI complexity scaling from 200- to 1000-troop battles (Skirmish → Massive), formation count and tick interval tables | 📚 Reference |
| [phase19-api-verification.md](Features/Combat/phase19-api-verification.md) | API verification report for Phase 19 battlefield realism enhancements (ammunition tracking, line relief, morale contagion, feints) — verified feasible against v1.3.13 decompile (reference) | 📚 Reference |

### Campaign & World

**Location:** `Features/Campaign/`

| Document | Topic | Status |
| --- | --- | --- |
| [camp-life-simulation.md](Features/Campaign/camp-life-simulation.md) | Camp activities: daily routine events, social interactions, training opportunities, rest actions, company needs management in camp | ✅ Current |
| [temporary-leave.md](Features/Campaign/temporary-leave.md) | Leave system: requesting leave (rank-based approval), leave duration limits, leave activities (visit family, trade, rest), return requirements, AWOL consequences | ✅ Current |
| [town-access-system.md](Features/Campaign/town-access-system.md) | Town access rules: rank-based restrictions (T1-T4 limited, T5+ more freedom), permission requirements, town activities available by rank, leave of absence system | ✅ Current |
| [camp-routine-schedule-spec.md](Features/Campaign/camp-routine-schedule-spec.md) | Camp routine schedule: baseline daily routine (dawn formations, midday work, dusk social, night rest), world state deviations, schedule forecast UI | ✅ Implemented |

### Content System

**Location:** `Features/Content/`

| Document | Topic | Status |
| --- | --- | --- |
| [README.md](Features/Content/README.md) | Content folder overview | ✅ Current |
| [injury-system.md](Features/Content/injury-system.md) | Unified medical condition system: injuries (3 types), illnesses (4 types), medical risk escalation (0-5), context-aware treatment (land vs sea), illness onset triggers, recovery tracking, maritime illness variants, condition worsening mechanics. Fully integrated with ContentOrchestrator. | ✅ Implemented |
| [writing-style-guide.md](Features/Content/writing-style-guide.md) | Bannerlord RP writing guide: voice and tone (terse military prose), tense/perspective rules, vocabulary (medieval military register, avoid anachronisms), setup/option/result text patterns, tooltip formatting, **opportunity hints** (camp rumors vs personal hints, placeholder usage, categorization), dialogue patterns by rank, common mistakes to avoid, examples and checklists | ✅ Current |
| [home-surface.md](Features/Content/home-surface.md) | Living reference for the Enlisted Home Surface (Spec 1, Phases A–D complete). HomeActivity lifecycle, 4-phase flow, 5-intent Evening menu provider, 34 HomeTriggers predicates, 50-storylet corpus, save offset 45, integration points with the storylet backbone. | ✅ Current |
| [career-loop.md](Features/Content/career-loop.md) | Living reference for the Career Loop Closure surface (Plan 5). Five paths × T4/T6/T9 crossroads, `commit_path` / `resist_path` effect primitives, T7+ named-order variants (30 storylets), culture + lord-trait overlays (45 + 15), `CareerDebugHotkeysBehavior` hotkeys, `validate_content.py` Phase 15 enforcement, integration with Plans 1-4. No new save-definer offsets. | 🟡 Code-complete |
| [orders-content.md](Features/Content/orders-content.md) | Complete catalog of all 17 implemented orders with event pools, duration, skills, fatigue, injury risk, and sea/land filtering. Live reference until Spec 2 replaces the subsystem — **Spec 2 Phase A underway** (Tasks 1-16 of 60 shipped (Phase A code complete, Task 17 smoke pending); [plan](superpowers/plans/2026-04-20-orders-surface.md)). | ✅ Current |

### Technical Systems

**Location:** `Features/Technical/`

| Document | Topic | Status |
| --- | --- | --- |
| [conflict-detection-system.md](Features/Technical/conflict-detection-system.md) | Conflict detection and prevention: event cooldown tracking, mutually exclusive events, resource conflicts (gold/items), state validation, anti-spam protections | ✅ Current |
| [commander-track-schema.md](Features/Technical/commander-track-schema.md) | Commander tracking schema: save data structure for player progress, persistence patterns, serialization rules | 📋 Specification |
| [encounter-safety.md](Features/Technical/encounter-safety.md) | Encounter safety patterns: battle side determination (handles 2 army race conditions), null checks, save/load safety, state validation, edge case handling, external interruptions (battles, captures) | ✅ Current |

### UI Systems

**Location:** `Features/UI/`

| Document | Topic | Status |
| --- | --- | --- |
| [ui-systems-master.md](Features/UI/ui-systems-master.md) | Complete UI reference: all menus, screens, and interfaces (camp menu, muster menu, QM interfaces, equipment grids, dialogue flows), Gauntlet implementation patterns, UI technical requirements | ✅ Current |
| [enlisted-combat-log.md](Features/UI/enlisted-combat-log.md) | Custom combat log widget: native-styled scrollable feed (right side, 5min persistence, 50 message history), smart auto-scroll (pauses on manual scroll), inactivity fade (35% after 10s), clickable encyclopedia links with faction-specific colors (kingdoms display in banner colors: Vlandia=red, Sturgia=blue, Battania=green, etc.), suppresses native log while enlisted via Harmony patch, color-coded messages with shadows | ✅ Current |
| [camp-hub-custom-gauntlet.md](Features/UI/camp-hub-custom-gauntlet.md) | Custom Gauntlet main hub: replaces `enlisted_status` GameMenu with custom layout (horizontal buttons, dynamic order cards, settlement access), all submenus stay native GameMenu, complete implementation spec with ViewModel/XML/Behavior code | 📋 Specification |
| [color-scheme.md](Features/UI/color-scheme.md) | Professional color palette: hex codes for all UI elements (backgrounds, text, buttons, status indicators), quality tier colors, reputation colors, accessibility considerations | ✅ Current |
| [news-reporting-system.md](Features/UI/news-reporting-system.md) | News feeds and Daily Brief: event logging, combat summaries, period recaps, notification system, Daily Brief UI (shows last 12 days of activity) | ✅ Current |

---

## Content & Narrative

**Location:** `Features/Content/`

| Document | Purpose | Status |
| --- | --- | --- |
| [README.md](Features/Content/README.md) | Content system overview: 282 content pieces (17 orders, 84 order events, 37 decisions, 36 camp opportunities, 57 context events, 51 map incidents) | ✅ Current |

---

## Reference & Research

**Location:** `Reference/`

| Document | Purpose | Status |
| --- | --- | --- |
| [native-apis.md](Reference/native-apis.md) | Campaign System API reference: Bannerlord API patterns, CampaignBehavior structure, common APIs (Hero, Party, Clan, Settlement), event hooks, save/load patterns - use for API verification against decompiled source | 📚 Reference |
| [native-skill-xp.md](Reference/native-skill-xp.md) | Skill progression reference: attribute/skill hierarchy, focus points, learning rates, XP calculation formulas, thematic aliases - use when implementing training/skill systems | 📚 Reference |
| [content-effects-reference.md](Reference/content-effects-reference.md) | Complete effects reference: all effect types (skill XP, gold, HP, reputation, escalation, company needs, party, narrative), native API integration, processing flow - use when writing content JSON | 📚 Reference |
| [native-map-incidents.md](Reference/native-map-incidents.md) | Native game incidents: all vanilla map incidents (bandits, prisoners, travelers), trigger conditions, outcomes, loot tables - use to avoid conflicts with native content | 📚 Reference |
| [map-incidents-warsails.md](Reference/map-incidents-warsails.md) | Naval DLC map incidents: Warsails expansion content, naval encounters, coastal events - use to avoid DLC conflicts | 📚 Reference |
| [ai-behavior-analysis.md](Reference/ai-behavior-analysis.md) | AI behavior analysis: native AI decision-making patterns, party movement logic, combat AI, lord behavior - use for AI-aware feature design | 📚 Reference |
| [battle-system-complete-analysis.md](Reference/battle-system-complete-analysis.md) | Complete battle and encounter system analysis: all 68 battle/encounter/captivity/raid menus, UpdateInternal() state machine (12 states), complete battle lifecycles (manual attack, autosim, siege assault), captivity flow (13 menus), village raid flow (10 menus), menu transition flows with exact code references - use to understand native battle handling and verify enlisted mod coverage | 📚 Reference |
| [complete-menu-analysis.md](Reference/complete-menu-analysis.md) | Complete native menu system analysis: tick-based menu refresh system, GetGenericStateMenu() priority order, siege menu flow (join_siege_event vs menu_siege_strategies), BesiegerCamp mechanics, race condition timeline documentation, captivity and village raid menus cataloged - use to understand menu transitions and timing issues | 📚 Reference |
| [captivity-and-raid-analysis.md](Reference/captivity-and-raid-analysis.md) | Captivity and village raid systems deep dive: complete captivity flow (capture → 13 menu paths → release), army removal during captivity, village raid system (lord raiding while enlisted), enlisted mod gaps and recommendations - use to understand captivity handling and plan enlisted captivity features | 📚 Reference |
| [party-attachment-and-merging.md](Reference/party-attachment-and-merging.md) | Native implementation analysis of party attachment, escorting, and Army system — use when evaluating enlistment faking strategies or physical party merge options | 📚 Reference |
| [siege-menu-analysis.md](Reference/siege-menu-analysis.md) | Comprehensive analysis of native siege menu behavior (menu flow, key menus, timing) to guide correct handling of siege encounters for enlisted soldiers | 📚 Reference |
| [camp-simulation-system.md](Features/Campaign/camp-simulation-system.md) | Two-layer camp system: Background Simulation (autonomous company life) + Camp Opportunities (36 player activities with learning), Decision Scheduling (Phase 9). Content flows immediately on enlistment (no grace period). Complete implementation documentation. | ✅ Implemented |

---

## Status Legend

- ✅ **Current** - Actively maintained, reflects current implementation
- ⚠️ **In Progress** - Feature partially implemented, doc evolving
- ⚠️ **Planning** - Design document for future feature
- ⚠️ **Mixed** - Contains both completed and outdated/future content
- ❓ **Verify** - Implementation status needs verification
- 📦 **Archived** - Historical reference, no longer maintained
- ❌ **Deprecated** - Replaced by newer docs, kept for reference only

---

**Last reorganization:** 2026-01-03 (Added systems-integration-analysis.md)
