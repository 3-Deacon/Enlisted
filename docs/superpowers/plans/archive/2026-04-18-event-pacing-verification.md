# Event Pacing Redesign — Phase 3 Verification Battery

**Status:** Framework committed. Results to be filled in by the next in-game playtest session.

Playtesting covers Phase 1 (director infrastructure), Phase 2 (routing live), and Phase 3 (all 15 production `EventDeliveryManager.Instance.QueueEvent(...)` call sites migrated). Phase 4 is this doc.

For each scenario below, capture:
- **Game day** when the scenario started
- **Wall-clock timestamps** (UTC or local, just be consistent)
- **Outcome** — ✅ pass / ❌ fail / ⚠️ partial — with a one-paragraph note

Log files to inspect:
- `...\Modules\Enlisted\Debugging\Session-A_*.log` — mod log, StoryDirector emit/route decisions
- `C:\ProgramData\Mount and Blade II Bannerlord\*.log` — native engine log (crash reports, save errors)

## Scenarios

### Scenario 1 — Quiet week

Let 14+ in-game days pass without any modal-eligible event firing (don't accept any orders or engage in combat).

**Expected:** On day 14 exactly, the Director's `OnDailyTick` fires a quiet-stretch event from `ModuleData/Enlisted/Events/events_quiet_stretch.json`. A modal appears with one of the three authored quiet-stretch titles. Session log contains `StoryDirector.quiet_stretch` source ID and `QuietStretchTimeout` beat.

**Known tolerances:** If the deferred-retry queue happens to have an entry, it flushes instead and suppresses the quiet-stretch for that tick (by design). Wait another day if so.

### Scenario 2 — Active week

In quick succession (within ~2 minutes wall-clock and same in-game day), trigger three modal-eligible events. Options: accept 3 orders back-to-back, or route three map incidents through battle-end and two settlement entries.

**Expected:**
- First modal fires immediately (0s offset)
- Second modal is deferred — landed in `_deferredInteractive` list (visible via save-data inspection or via the `DeferredInteractive` public read-only accessor if a debug panel exposes it)
- Third modal likewise deferred
- On subsequent DailyTicks (one per day), each deferred entry re-fires in FIFO order as floors open (5-day in-game floor per `DensitySettings.ModalFloorInGameDays`)

**Known tolerances:** Chain continuations (promotions, bag checks, EventPacingManager chain events) bypass the floor, so if one of the three is a chain, expect it to fire immediately alongside the first.

### Scenario 3 — FastForward compression

Set campaign speed to 3x (StoppableFastForward). Trigger two modal candidates within 20 seconds wall-clock.

**Expected:**
- First modal fires and triggers the speed downshift (`StoppableFastForward` → `StoppablePlay`)
- Second modal deferred by the 60s wall-clock guard (`DensitySettings.ModalFloorWallClockSeconds`), regardless of in-game days elapsed
- After 60s wall-clock, if the player re-accelerates, the second can fire once floors allow

### Scenario 4 — Chain event bypass

Reach a promotion threshold (e.g. meet T2 requirements after T1 proving). The proving event fires through `PromotionBehavior.cs:367` with `ChainContinuation = true`.

**Expected:** The proving event fires immediately — no 5-day floor check, no category cooldown check. The 60s wall-clock guard still applies (only relevant if another modal fired <60s ago, which is unlikely at promotion boundaries).

**Additional check:** The post-proving ceremony `ShowInquiry` at `PromotionBehavior.cs:434` also fires immediately as an intentional Director bypass (annotated in Task 21b). If the T7 promotion path is triggered, the `RetinueManager.cs:1017` Commander's Commission modal (Task 28b) also fires as annotated bypass.

### Scenario 5 — Muster digest

Let DispatchItems accumulate over ~10 in-game days (triggers: any mix of threshold events, memorials, supply pressure stages, observational writes from the router).

Then select the "Muster" menu option on return from a long mission.

**Expected:** The muster intro digest (implemented in Phase 2 Task 15) shows a severity-filtered summary of DispatchItems under "Since your last muster:".

### Scenario 6 — Headlines accordion

Trigger at least one high-severity DispatchItem (easiest: let an escalation threshold fire, or let the company supply pressure stage_1 event land in the accordion per Task 24).

Open `enlisted_status` from the map menu.

**Expected:** A "Headlines" drilldown appears showing unviewed high-severity items (`Severity >= 2`) from the last 7 days. Entries marked `[NEW]` until the player clicks through them.

### Scenario 7 — Save/load with deferred queue

After Scenario 2 leaves deferred candidates in the queue, save the game. Exit to main menu. Reload the save.

**Expected:**
- Deferred candidates persist via `SyncData` on `_deferredInteractive` (already implemented in Phase 1)
- DailyTick continues flushing from where it left off
- No `E-PACE-*` errors in session log
- Save load time remains under 10ms for the Director payload

### Scenario 8 — Relevance drop

Let a war be declared between two distant AI factions (neither is the player's kingdom, neither contains the player's enlisted lord).

**Expected:**
- No modal fires
- No DispatchItem created
- No Headlines entry appears
- Session log shows the filter-drop trace in `RelevanceFilter.Passes` (if debug logging is enabled)

## Phase 3 Call-Site Migration Ledger

All 15 production `QueueEvent` call sites now route through `StoryDirector.EmitCandidate`:

| # | Site | Commit | Tier | ChainContinuation |
|---|---|---|---|---|
| 17 | `MapIncidentManager.cs:361` | 1e3768e / 8a41375 | context-dependent | ❌ |
| 18 | `ContentOrchestrator.cs:274` (committed opportunity) | 1f559f6 | Modal | ❌ |
| 18b | `ContentOrchestrator.cs:621` (QueueCrisisEvent) | f084385 | Modal | ❌ |
| 18c | `ContentOrchestrator.cs:834` (illness onset) | 76710c6 | Modal | ❌ |
| 19 | `EnlistedMenuBehavior.cs:5549` | 84a06ce | Modal | ❌ |
| 20 | `EventPacingManager.cs:111` | 0789b72 | Modal | ✅ |
| 21 | `PromotionBehavior.cs:367` | caafd54 | Modal | ✅ |
| 22 | `EscalationManager.cs:938` | 754c39f | Modal | ❌ |
| 23 | `EnlistmentBehavior.cs:2120/:6437` | 094c5e4 | Modal | ✅ |
| 24 | `CompanySimulationBehavior.cs:671` (supply pressure arc) | 23dae6b | **Pertinent (demoted)** | ❌ |
| 25 | `CampOpportunityGenerator.cs:183` | b4d40b1 | Modal | ❌ |
| 26 | `OrderProgressionBehavior.cs:308` | 2f297c6 | Modal | ❌ |
| 27 | `RetinueCasualtyTracker.cs:669` (memorial) | a8d42bf | **Pertinent (demoted)** | ❌ |
| 28 | `RetinueManager.cs:739` (loyalty) | f6150c2 | Modal | ❌ |
| 29 | `DebugToolsBehavior.cs:139` | 1762aca | — (annotated bypass) | — |

**Annotated intentional bypasses** (observational modals with no EventDefinition):

| # | Site | Commit | Reason |
|---|---|---|---|
| 21b | `PromotionBehavior.cs:434` (ceremony) | 8fb8e3a | 2-beat chain with Director-gated proving event |
| 21c | `OrderManager.cs:1043` (insubordination) | 3380560 | self-paced by decline-count threshold |
| 21d | `CampOpportunityGenerator.cs:1594` (dereliction) | b977a39 | self-paced by detection probability |
| 28b | `RetinueManager.cs:1017` (Commander's Commission) | df37ff6 | 2-beat chain with T7 promotion ceremony |

## Deferred Retry

`OnDailyTick` flushes one deferred candidate per day (commit `a678ada` + polish `b13227e`). List is capped at 32 entries; oldest drops on overflow.
