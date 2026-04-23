# Player Agency Redesign — Design

**Status:** Draft v1 (2026-04-23). Redesign of the passive simulation layer under the "orders over duties" mod vision — replace the background tick engine that mutates player state without player input with an explicit choice-driven model.

**North star:** between decisions, nothing changes. Every mutation to the player's skill XP, conditions, gold, scrutiny, relations, or path scores traces back to a choice the player made — accepting an order, picking a camp activity, or responding to a modal. The world can still move without the player (kingdom wars, lord decisions, sieges) but the player's own state only advances when they act.

---

## Problem statement

An audit of `src/` surfaced 22 auto-firing systems that mutate player-visible state on daily / hourly ticks or on passive event listeners — no player input required. The top five offenders:

1. `CompanySimulationBehavior.OnDailyTick` (four sub-handlers: `ProcessRosterRecovery`, `ProcessNewConditions`, `ProcessIncidents`, `CheckPressureArcEvents`) — random sickness / desertion / incident rolls applying outcomes silently.
2. `EnlistmentBehavior.OnDailyTick` — wage accrual + pension + leave-duration checks.
3. `EscalationManager.OnDailyTick` — scrutiny and medical-risk passive decay + threshold firing.
4. `PlayerConditionBehavior.OnDailyTick` — injury / illness day countdown + medical-risk auto-escalation.
5. `CampOpportunityGenerator.OnHourlyTick` — scheduled commitments auto-firing (intent-scoped, so partial agency already).

Combined effect: the player's soldier gets skills, wounds, scrutiny, relation changes, and gold while the human sits staring at the map. The systems treat the main hero as an NPC being managed, not an agent making choices. This contradicts the mod's "soldier career simulator" framing.

Philosophy choice (C from brainstorm): **both rewards and penalties** must tie to player choice. Cut the passive simulation layer; keep only world-scope events the player is correctly powerless over (kingdom wars, lord decisions, siege starts).

---

## Architecture

**One idea:** every mutation that changes player-visible state routes through a single `StateMutator` gate. The gate checks for an active `DecisionScope` — a consent envelope created by the player accepting an order, picking a camp activity, or responding to a modal prompt. No scope → mutation refused.

### Core abstractions

| Type | Role |
| :--- | :--- |
| `DecisionScope` | Consent envelope. Carries `ActivityId` / `OrderStoryletId`, intent id, `StartedAt` / `ExpiresAt`, `AllowedCategories` (skills / conditions / gold / quality / scrutiny / path / relation). Lives on a stack in `AgencyGate` while active. |
| `AgencyGate` | Static service owning the scope stack. `Push(scope)` / `Pop()` / `TryGetCurrent(out scope)`. Populated by `OrderActivity`, camp activity providers, and modal resolvers. |
| `StateMutator` | Single gateway for all player-state writes: `TryApplySkillXp(hero, skill, amount, reason)`, `TryAdjustCondition(...)`, `TryGiveGold(...)`, `TryWriteQuality(...)`, `TryWriteFlag(...)`, `TryAdjustScrutiny(...)`, `TryBumpPath(...)`, `TryAdjustRelation(...)`. Each call checks `AgencyGate.TryGetCurrent` and the requested mutation's category against `scope.AllowedCategories`. Rejected calls log `Expected("AGENCY", "mutation_without_scope", ...)` or `Expected("AGENCY", "mutation_outside_scope_categories", ...)`. |
| `CampActivityMenuProvider` | New camp-hub slot-bank provider (patterned on `HomeEveningMenuProvider`). Surfaces named activities when no order is active. |
| `IncidentResponseModal` | Replaces `CompanySimulationBehavior.ProcessIncidents` auto-apply. Simulation proposes candidate incidents; modal fires; outcome lands only when the player picks. |

### File map

| New file | Purpose |
| :--- | :--- |
| `src/Features/Agency/AgencyGate.cs` | DecisionScope stack + observers |
| `src/Features/Agency/DecisionScope.cs` | POCO carrying scope metadata + `MutationCategory` enum |
| `src/Features/Agency/StateMutator.cs` | Gateway with Try- methods; routes to existing store APIs when allowed |
| `src/Features/Agency/MutationCategory.cs` | `[Flags]` enum: `Skill / Condition / Gold / Quality / Flag / Scrutiny / Path / Relation / CompanyRoster` |
| `src/Features/Activities/Camp/CampActivityActivity.cs` | Concrete `Activity` subclass for camp activities (claims the next free save-definer offset 51) |
| `src/Features/Activities/Camp/CampActivityStarter.cs` | Lifecycle starter gated on "enlisted + no active OrderActivity + no active HomeActivity" |
| `src/Features/Activities/Camp/CampActivityMenuProvider.cs` | 5–7 slot bank on the camp hub for activity picks |
| `ModuleData/Enlisted/Activities/camp_activity.json` | ActivityTypeDefinition: single phase, `PhaseDelivery.PlayerChoice`, pool per intent |
| `ModuleData/Enlisted/Storylets/camp_rest.json` | ~8 rest-intent storylets |
| `ModuleData/Enlisted/Storylets/camp_drill.json` | ~10 drill-intent storylets |
| `ModuleData/Enlisted/Storylets/camp_tend.json` | ~6 tend-intent storylets |
| `ModuleData/Enlisted/Storylets/camp_patrol.json` | ~8 patrol-intent storylets |
| `ModuleData/Enlisted/Storylets/camp_scheme.json` | ~6 scheme-intent storylets |
| `ModuleData/Enlisted/Storylets/camp_attend_lord.json` | ~6 attend-lord storylets |
| `ModuleData/Enlisted/Storylets/camp_study.json` | ~5 study-intent storylets |
| `ModuleData/Enlisted/Storylets/incident_response_*.json` | Incident response chains (one per incident type) |

Count of new storylets target: ~50 camp + ~15 incident response = ~65.

---

## Time-control taxonomy (authoritative, decompile-verified)

Bannerlord exposes exactly three map speeds via `Campaign.Current.TimeControlMode` (a `CampaignTimeControlMode`). The enum has seven values, but the tick math at `Campaign.cs:843` collapses them to three behaviors:

| Speed | Enum values that produce this behavior | `TickMapTime` `num` value |
| :--- | :--- | :--- |
| **Paused** | `Stop`, `FastForwardStop` | `0` (map time does not advance) |
| **Play (1×)** | `StoppablePlay`, `UnstoppablePlay` | `0.25 × realDt` |
| **Fast-forward (4× default)** | `StoppableFastForward`, `UnstoppableFastForward`, `UnstoppableFastForwardForPartyWaitTime` | `0.25 × realDt × SpeedUpMultiplier` |

**`SpeedUpMultiplier` defaults to `4f`** (`Campaign.cs:373, :615`). It is settable at runtime (`CampaignCheats.cs:3143-3146` sets it from the cheat console); mods may raise it. The UI only has three buttons — pause / play / fast-forward. There is no native 8× or 16×; higher speeds only exist if another mod bumped `SpeedUpMultiplier`.

**Unstoppable variants** are game-forced (army-wait, hideout-wait, in-menu waits). The player cannot switch into an Unstoppable mode manually. For gate design, treat Unstoppable fast/play exactly like their Stoppable siblings.

**Two families for gate logic:**

- **Play-family (player is reading or the game is not skipping):** `Stop`, `StoppablePlay`, `UnstoppablePlay`, `FastForwardStop`.
- **Fast-family (game is accelerating, player is not reading):** `StoppableFastForward`, `UnstoppableFastForward`, `UnstoppableFastForwardForPartyWaitTime`.

This taxonomy is referenced below for the idle-reprimand trigger and for consistency with the news-unification spec. (Note: the news spec v2 mis-bucketed `FastForwardStop` as fast-family — see "Open issues" below for the fix.)

---

## Systems gutted / converted

| System | Current behavior | New behavior |
| :--- | :--- | :--- |
| `CompanySimulationBehavior.ProcessRosterRecovery` | Daily random sickness recovery + desertion rolls mutate roster | **Delete the mutations.** `_sickCount` etc. now only move on camp-activity resolve or incident-modal resolve. Fields kept for display. |
| `CompanySimulationBehavior.ProcessNewConditions` | Daily random sickness / desertion / wound rolls | **Delete the auto-apply path.** New conditions arise only as downstream consequences of player choices (e.g. player chose harsh discipline → desertion chain fires as a modal). |
| `CompanySimulationBehavior.ProcessIncidents` | Daily random incident rolls (0–2 / day) with auto-applied effects | **Refactor to IncidentResponseModal.** The simulation still computes incident probabilities but produces **candidate incidents** that emit as Modals. Player picks the outcome; effects apply through `StateMutator`. |
| `CompanySimulationBehavior.CheckPressureArcEvents` | Emits Modals for threshold crossings | **Keep.** This is already the right shape — Modal prompts for player decision. |
| `PlayerConditionBehavior.ApplyDailyRecoveryAndRisk` | Daily day-countdown on `InjuryDaysRemaining` / `IllnessDaysRemaining` + auto-raises `MedicalRisk` on untreated | **Delete the countdown and auto-raise.** Recovery fires only when `Rest`, `Tend`, or `Seek Medic` camp activity resolves. Medical-risk changes route through explicit scope. |
| `EscalationManager.ApplyPassiveDecay` | Daily scrutiny decay if no recent change | **Delete.** Scrutiny drops via `Patrol`, `Attend Lord` with an honorable-trait lord, or certain order-resolve storylets. It does not bleed down with time. |
| `PathScorer.OnHeroGainedSkill` | Every vanilla skill-XP gain bumps a path score | **Delete.** Path only accumulates via `OnIntentPicked` (existing, intent-scoped). Vanilla skill XP from combat still flows; it just no longer feeds paths without explicit player commitment. |
| `EnlistmentBehavior.OnDailyTick` wage accrual | Writes pending muster-pay each day | **Convert to ledger-only tracking.** No `StateMutator` gold writes until the muster ceremony the player opts into. Pension accrual stays as a tracked number, paid out at muster. |
| `EnlistmentBehavior.OnDailyTick` leave / captivity checks | State sync | **Keep.** State reads, not state writes. |
| `EnlistmentBehavior.OnHourlyTick` | Party-state visibility sync | **Keep.** No player-state mutations. |
| `QualityBehavior.OnDailyTick` (decay) | Applies decay rules from `quality_defs.json` | **Keep DATA plumbing, remove decay rules from player-facing qualities.** `quality_defs.json` entries for `scrutiny`, `readiness`, `supplies`, `medical_risk` lose their `decay` blocks. Housekeeping-scoped qualities (if any) keep decay. |
| `FlagStore.OnHourlyTick` (expiry sweeps) | Drops expired flags | **Keep.** Data housekeeping. |
| `StoryDirector.OnDailyTick` (pacing gates + retries) | Deferred-interactive retry queue | **Keep.** Content-delivery plumbing; doesn't write player state directly. |
| `EnlistedCampaignIntelligenceBehavior.HandleHourlyTick` | Recomputes snapshot, emits candidates | **Keep snapshot recompute (read-only).** Candidate emission must produce Modals, not silent mutations. Already largely the shape. |
| `CampOpportunityGenerator.OnHourlyTick` | Scheduled commitments auto-fire | **Gate on `AgencyGate`.** The commitment itself was picked by the player, which should count as a scope — but the commitment must push a fresh scope when it fires, not auto-apply effects. Wrap the fire path through `StateMutator`. |
| `CampLifeBehavior.OnDailyTick` + event listeners | Pressure recompute | **Keep as pressure computation (read-only).** Pressure values feed displays and eligibility checks; they no longer trigger auto-applied effects. |

---

## What stays passive (correctly)

- Kingdom / lord / faction-level events (wars, sieges, clan-leader deaths, political shifts). The player is a soldier, not a sovereign — these are correctly outside their scope.
- Catalog loading + JSON hot-reload.
- StoryDirector pacing floor enforcement + retry queue.
- Save / load plumbing.
- Intelligence snapshot recompute (read-only).
- Flag expiry sweeps, quality decay for non-player-facing qualities.

World-scope events still reach the player as **Modals requesting a response**, not as silent state writes. A war declaration emits a Modal ("runners cry through camp — read the proclamation") which the player opens on their own time.

---

## New player-facing surfaces

### Camp activity slot bank

When no order is active AND the player is enlisted AND the lord is stationary (garrisoned, or waiting in a camp), the camp hub shows 5–7 activity slots patterned on the Home Evening menu:

| Slot | Intent | Default effect at resolve | Typical duration |
| :--- | :--- | :--- | :--- |
| Rest | `rest` | Heal 1 injury day + fatigue recovery | 12h |
| Drill | `drill` | +skill XP in a chosen skill + readiness | 3h |
| Tend | `tend` | −1 company sickness + loyalty for retinue | 6h |
| Patrol | `patrol` | Small gold + scrutiny −3 + chance of minor-incident modal | 6h |
| Scheme | `scheme` | Scrutiny −5 illegally OR failure flag | 4h |
| Attend Lord | `attend` | +relation + rank-XP + exposes next named-order prompt | 2h |
| Study | `study` | +weapon or skill XP at reduced rate + refreshes path score | 2h |

Each slot pushes a `DecisionScope` whose `AllowedCategories` match the activity's effects. Storylet-pool-driven content per intent, same backbone as Home Evening.

### Incident response modals

`IncidentResponseModal` replaces the auto-applied incident outcome. Example flow:

- Simulation detects roster risk (e.g. 3+ days at high logistics strain) → emits candidate `incident_desertion_risk` with `StoryDirector.EmitCandidate` (Modal tier).
- Modal renders: "Corporal says three men wavering — flog one / reason with them / ignore."
- Player picks. The choice's effects pass through `StateMutator` under the scope the modal pushed when it opened. Scope pops on modal-resolve.

Incident categories to implement Phase 4: desertion risk, sickness outbreak, supply shortfall, camp brawl, raid aftermath.

### Idle-reprimand modal

If the player sits at play-family (Stop / Play) for more than 3 in-game days with no `DecisionScope` ever pushed AND the lord is stationary, fire a Modal: "Sergeant notes you've been idle — pick an activity today or expect a reprimand (scrutiny +5 next muster)." Forces engagement.

If the player sits at fast-family (FF), the mutation-drop behavior is the real discipline: time passes but nothing changes, so eventually the kingdom / lord fires a world event that creates a decision point anyway.

---

## Data flow

```
Idle state — no DecisionScope active
    ↓
Player accepts order OR picks camp activity OR responds to modal
    → AgencyGate.Push(DecisionScope{ActivityId, Intent, AllowedCategories})
    ↓
Time advances during activity / order arc (at any of the 3 game speeds)
    → storylet effects fire
    → StateMutator.Try{...}(...) called
       → AgencyGate.TryGetCurrent passes the scope
       → category check passes
       → underlying store API (QualityStore, FlagStore, skill XP, gold) writes
    ↓
Storylet arc resolves
    → AgencyGate.Pop()
    ↓
Back to idle
```

Between Pop() and the next Push(), every `StateMutator.Try{...}` call logs `Expected("AGENCY", "mutation_without_scope", ...)` with the attempted mutation in ctx and returns false. The underlying store is never touched.

---

## Playflows

### Playflow 1 — Fresh recruit, Day 1, peaceful garrison

1. Enlistment dialogue resolves; its storylet pushed a scope that granted initial rank XP + prior-service flags. Scope pops.
2. Main menu: `DISPATCHES` renders, `UPCOMING` reads "No orders — pick a camp activity."
3. Player opens Camp → 5 activity slots. Picks **Drill**, picks One-Handed skill.
4. Drill scope pushes, 3h advances, storylet resolves: `+One-Handed 40 XP`, `+readiness 3`. Scope pops.
5. Player fast-forwards 4 hours at FF. **Zero state change** — no scope active. Menu body identical to before.
6. Player picks **Patrol**: scope pushes, 6h advances, `+12 gold`, `scrutiny −3`. No incident this time. Scope pops.
7. Player picks **Rest**: scope pushes, 12h advances, `fatigue recovery`. Scope pops.
8. Dawn. Lord has not moved (no passive tick advanced his plans). Player must pick again.

**Feel:** player is the engine.

### Playflow 2 — Named order accepted, scout arc

1. `UPCOMING` shows "Sergeant proposed: Scout the eastern ridge. [Accept]"
2. Accept → `OrderActivity.AcceptNamedOrder("order_scout_ranger_t1")` → `AgencyGate.Push(Scout_3d)`.
3. Time advances across 3 days. Scout storylet phases fire; a mid-arc Modal pushes a child scope to resolve a "tracks in the mud" decision.
4. Arc resolves Day 3. `AgencyGate.Pop()`. `YOU` narrative reflects outcomes: `+Scouting 65→72`, `+ranger path score`, `+battles_survived 1`.

**Feel:** the order is the consent envelope. Everything fires within it.

### Playflow 3 — Kingdom war declared mid-activity

1. Player is in a Drill scope at 1×.
2. `OnKingdomDecision` → Vlandia declares war. World-scope passive event.
3. **No direct player-state mutation.** A Modal is queued via `StoryDirector`: "Runners cry through the camp — war with the Northern Empire. [Read the proclamation]"
4. Drill resolves first (current scope wins). `AgencyGate.Pop()`.
5. Modal fires, player reads, trivial scope for it pushes and pops. No mutations.
6. `DISPATCHES` stays frozen until next weekly edition. `UPCOMING` adds "Rumors of mobilization."
7. A day later, a named-order proposal fires: "Join the march on Pravend." Accept / decline routes war impact through orders.

**Feel:** world-scope events arrive as optional reading + opt-in orders.

### Playflow 4 — Wounded, recovery

1. Battle scope previously wrote `InjurySeverity=Moderate, days=7` via the gate. Scope popped.
2. `YOU` reads "Injured — moderate, 7 days. Training restricted until healed."
3. At 1× for 7 in-game days doing nothing → injury stays at `days=7`. **No passive healing.**
4. Player must pick **Rest** / **Tend** / **Seek Medic**. Each resolves a fixed reduction.
5. Four Rest picks (or two Tend + medicinal storylet fires) → cleared.

**Feel:** recovery is earned.

### Playflow 5 — Scrutiny pressure

1. Scrutiny sits at 68. **No passive decay.**
2. `UPCOMING` shows "Lieutenant requests to speak with you. [Attend]"
3. Modal fires: "Where were you Tuesday? [Lie / Deflect / Truthful]"
4. Each option writes through the gate with different effects.
5. Alternatively, player Patrols 3 times → scrutiny −9 cumulative. Scrutiny is earned down, not bled down.

**Feel:** scrutiny is a real pressure the player negotiates.

### Playflow 6 — T4 crossroads (existing Plan 5)

1. Rank 4 reached. `EnlistmentBehavior.OnTierChanged(3, 4)` fires.
2. `PathCrossroadsBehavior` picks highest path score, emits Modal, pushes scope.
3. Player picks Commit → `commit_path ranger` writes via gate.
4. Scope pops. Downstream T7+ variants now gate on `committed_path_ranger`.

**Feel:** this is already the model. Plan 5 was the prototype; agency redesign generalizes it.

---

## Save / migration

- `AgencyGate` scope stack is ephemeral runtime state, not persisted. On save-load with no active scope, no mutations occur until the player picks. Clean.
- Existing saves with accumulated `scrutiny`, `readiness`, `supplies`, `medical_risk` load as-is. Values persist until a player action changes them — acceptable ("state the save was in when the patch landed").
- `PlayerConditionBehavior.State` in-flight day counters survive the load but no longer advance on tick. Conditions clear on Rest / Tend / Seek Medic resolves OR via one-shot patch migration that zeros all in-flight counters below 7 days on first-load-after-patch (recommended — avoids confused "why is my old injury still here").
- No new save-definer offsets beyond `CampActivityActivity` claiming offset 51 (next after Plan 4's `DutyCooldownStore` at 50).

---

## Phasing

- **Phase 1 — Introduce gate in observer mode.** `StateMutator` / `AgencyGate` / `DecisionScope` added as wrappers. Every existing mutation call routes through `TryXxx`, but the gate returns `true` unconditionally (observer mode). Log count of would-reject calls per scope per hour for baseline. No behavior change.
- **Phase 2 — Flip gate to enforce.** Remove the "always allow" stub. Top-5 passive handlers (audit) route through the gate and now no-op between scopes. Existing orders / Home Evening / path crossroads still work. Idle-freeze behavior becomes visible.
- **Phase 3 — Ship `CampActivityActivity` + 7-slot menu + 50 storylets.** Save-definer offset 51 claimed. Player has explicit choices to make between orders.
- **Phase 4 — Refactor incidents to `IncidentResponseModal`.** Delete auto-apply from `ProcessIncidents`; ship 5 incident-response chains.
- **Phase 5 — Rebalance `quality_defs.json`** — remove decay blocks on player-facing qualities; delete obsolete decay tests.
- **Phase 6 — Content sweep.** Re-tune order resolve effects to carry rewards that the passive layer used to leak. Authoring pass for content density so the player has interesting picks.

Each phase is a reviewable diff. Budget: 2–3 weeks total.

---

## Risks

1. **Idle boredom.** Camp activities are hours-scale — idle gaps collapse to one menu pick. Idle-reprimand modal (3 in-game days at play-family with no scope) backstops.
2. **Existing smoke scenarios (Plans 1–5, A–H)** assume passive behavior in places. The agency refactor must re-run all eight scenarios to confirm nothing relied on passive drip. Phase 2 flip is the risk peak.
3. **Skill XP starvation.** Removing `PathScorer.OnHeroGainedSkill` cuts path bumps; camp Drill + intent picks must restore the pace. Tune in Phase 6.
4. **Unstoppable-forced FF.** `UnstoppableFastForward` + `UnstoppableFastForwardForPartyWaitTime` happen during army-wait / hideout scenarios. Gate treats them same as player-picked FF → no mutations. Acceptable: if the game is force-waiting the party, the narrative is "time passes but nothing happens to you specifically," which is fine.
5. **`SpeedUpMultiplier` mods.** Another mod raising `SpeedUpMultiplier` doesn't change our gate's behavior — we gate on the enum family, not the multiplier. Resilient.
6. **Idle-reprimand modal feeling nagging.** Cooldown: fire at most once per 7 in-game days. Skip entirely during active world events (war declared, siege started — player has reading to do).

---

## Testing

| Test | Method | Expected |
| :--- | :--- | :--- |
| Idle freeze smoke | Enlisted + lord stationary + no activity. Sit at 1× for 7 in-game days. | Zero delta on skill values, scrutiny, gold, conditions, quality values, path scores, roster. |
| Idle freeze at FF smoke | Same setup at 4× FF. | Zero delta. |
| Scope-matched mutations | Accept a scout order → run to resolve. | Mutations match storylet's declared effects exactly. No side deltas. |
| Camp activity resolves cleanly | Each of 7 activities fires and pops scope. | State changes match activity's declared effects. |
| Incident modal | Force incident candidate via debug → modal appears → pick option. | Outcome applies; no auto-apply before pick. |
| Idle-reprimand modal | 3 in-game days at Play/Stop with no scope. | Modal fires. |
| No reprimand at FF | Same 3 days at FF. | Modal does not fire (FF → no reading happening). |
| Plans 1–5 scenarios A–H | Re-run each scenario. | All pass. |
| Mutation-without-scope log | Hit any `StateMutator.Try` while no scope. | `Expected("AGENCY", "mutation_without_scope", ...)` emitted. Mutation dropped. |

---

## Open issues

1. **News-spec v2 FastForwardStop bucket error.** The news-unification spec at `docs/superpowers/specs/2026-04-23-news-and-status-unification-design.md` (v2, commit `2855866`) lists `FastForwardStop` in the fast-family for the regeneration-trigger taxonomy. That's wrong — the decompile treats `FastForwardStop` as Stop (tick `num = 0`, simplified mode returns `Stop`). A follow-up commit to the news spec should reclassify it to play-family. Small correction, does not invalidate the regeneration-trigger design.
2. **Which qualities lose decay?** Explicit list to author in Phase 5: `scrutiny`, `readiness`, `supplies`, `medical_risk`, `loyalty`. `rank_xp` stays read-through. `days_in_rank` / `days_enlisted` stay read-through. `path_*_score` has no decay today; no change needed.
3. **Does `AgencyGate` need to persist its scope stack?** Lean toward no — if the game crashes mid-activity, on reload the player is in a known idle state. But confirm this doesn't break muster sequences or long order arcs that might span save/load. Potentially: persist only the top scope.
4. **How many camp activities ship Phase 3?** 7 is the working target. If authoring load is too high, reduce to 5 core (Rest / Drill / Tend / Patrol / Attend Lord) and add Scheme / Study later.
5. **Intent bias migration from Home Evening.** Home Evening uses 5 intents (unwind / train_hard / brood / commune / scheme). Camp activities use different intents (rest / drill / tend / patrol / scheme / attend / study). The `PathScorer.IntentToPath` map (at `PathScorer.cs:33-40`) needs updating for the new intents.

---

## Deferred

- Ultra-long idle (30+ in-game days) housekeeping — the reprimand loop handles it, but for a true "I walked away from the keyboard" case, consider pausing time via `Campaign.Current.SetTimeControlModeLock(true)` on the reprimand modal.
- Culture flavor on camp activities (Khuzait "patrol the herds," Vlandian "drill with crossbows"). Deferred to a polish pass after core agency lands.
- Companion / retinue delegation — could the player send a companion on camp activities instead of doing them themselves? Out of scope for v1.

---

## Relation to other specs

- **News-unification spec v2** (`docs/superpowers/specs/2026-04-23-news-and-status-unification-design.md`): paused pending this spec. Once agency lands, the news spec's `DISPATCHES` / `YOU` / `SINCE LAST MUSTER` / `CAMP ACTIVITIES` sections stay valid, but `CAMP ACTIVITIES` pulls will have less passive-drift content to report (since passive routine outcomes are gone) — will be a pure activity-resolution log. Minor editing to the news spec after agency ships.
- **Plan 5 Career Loop** (`docs/superpowers/plans/2026-04-21-career-loop-closure.md`): crossroads are already scope-correct — they push a scope, player picks, scope pops. No change needed.
- **Plan 1 Campaign Intelligence** (`docs/superpowers/plans/2026-04-21-campaign-intelligence-backbone.md`): snapshot recompute stays read-only.
- **Plan 4 Duty Opportunities** (`docs/superpowers/plans/2026-04-21-duty-opportunities.md`): ambient duty storylets need audit — do they mutate state on emission or only when the player responds? Expected: they emit Modals, player responds, scope pushed at response time. If any fire auto-apply effects on emission, those break and need conversion in Phase 4.
