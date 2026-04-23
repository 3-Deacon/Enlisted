# Player Agency Redesign — Design (v2)

**Status:** Draft v2 (2026-04-23). Revised after spec-review feedback flagged v1's over-strict gate model, broken save-load semantics, under-scoped migration, wrong PlayerChoice timing, FF dead-end, chore-spam risk, and false "keep" claims on pressure-arc + enlistment daily tick.

**North star (unchanged):** the mod should stop silently changing the player's soldier as if they were an NPC. The "orders over duties" vision holds.

**Contract (revised):** not "nothing changes between decisions" but **"no unpreviewed authored player-state consequences."** The player accepts an envelope (order, camp activity, modal) and the envelope declares what categories of mutation are allowed, up to what magnitude, across what duration, at what risk bands, with what interruption points. Baseline service physics (tiny XP drift, small condition tick inside an accepted duty envelope, supply consumption on the march, daily wage accrual while serving) continues **inside** a valid envelope. High-stakes mutations (lose 3 men, +20 scrutiny, trait-level change, gold gain above a threshold) require either a modal at the moment of impact OR an explicit preview at envelope acceptance.

---

## Problem statement

Audit found 22 auto-firing systems mutating player-visible state on daily / hourly ticks without player input. v1's response was "gate every mutation behind an active `DecisionScope`; reject everything between scopes." Spec review found this over-corrects: breaks save/load, under-scopes the migration, and makes Bannerlord time feel fake for categories of change that are legitimately continuous inside service (a soldier's pay doesn't pause when he's between named orders).

The user's actual pain: rewards and penalties **that feel like they happened TO the soldier without foreshadowing or consent**. Tiny ambient XP gains are annoying because they're unasked-for; random sickness outbreaks are insulting because they couldn't be anticipated.

Sharper rule: **everything the player feels as "a consequence landed on me" must have been previewed at envelope acceptance or must fire as a modal at the landing moment.** Inside a previewed envelope, low-magnitude physics runs free.

---

## Envelope model

An **envelope** is a player-acknowledged activity with a declared mutation contract. Three envelope types:

| Envelope | Triggered by | Typical duration | Example allowed mutations |
| :--- | :--- | :--- | :--- |
| **Order envelope** | Player accepts a named / daily order | Hours to days | Skill XP in expected skills, small conditions (fatigue, minor wounds from combat), path bumps for the order's path tag, small gold earned, scrutiny shifts with magnitude caps |
| **Activity envelope** | Player picks a camp activity from the slot bank | Minutes to hours | Category-scoped: Drill allows Skill; Rest allows Condition; Patrol allows Gold/Scrutiny; etc. |
| **Modal envelope** | Player opens a `StoryDirector`-delivered Modal (incident, dialogue, proclamation) | Single decision | Only the option's declared effects |

Plus one implicit low-level envelope that persists as long as the player is enlisted and the lord's column is moving:

| **Service envelope** | Auto-active while `IsEnlisted == true` AND the lord's party is doing duty-consistent things (marching, garrisoning, resting) | Entire enlistment | Daily wage accrual (ledger only, no modal), supply consumption on the march, tiny fatigue drift, column-movement skill micro-XP inside vanilla caps |

The service envelope is the answer to "FF in a peaceful garrison has zero consequences." It exists implicitly so that baseline physics continues without the player clicking every 4 hours — but its allowed magnitudes are deliberately small and its contents are **previewed at enlistment acceptance** (the enlistment dialog tells the player: "You earn pay daily, you consume supplies while marching, you may take small knocks in routine duty — anything bigger requires an order").

Envelopes form a stack. Most specific (order > activity > modal) wins for mutation categorization. Service sits at the bottom as the default fallback.

---

## Core abstractions

| Type | Role |
| :--- | :--- |
| `MutationCategory` | `[Flags]` enum: `Skill / Condition / Gold / Quality / Flag / Scrutiny / Path / Relation / CompanyRoster`. A mutation carries its category. |
| `MagnitudeBand` | `Trivial / Minor / Moderate / Major`. Each envelope declares a per-category ceiling. A mutation whose magnitude exceeds the envelope's band is reclassified "high-stakes" and must be pre-previewed or modal-gated. |
| `Envelope` | Poco: `EnvelopeKind` (Service / Order / Activity / Modal), `Source` (storylet id / activity id / "implicit_service"), `StartedAt`, `ExpiresAt` (or null for Service), `AllowedCategories`, `PerCategoryMaxBand` (dict), `PreviewedIds` (HashSet of storylet / effect ids pre-announced at acceptance). |
| `AgencyGate` | Static service. Does NOT own a persisted stack. On every `StateMutator.Try{…}` call, derives the current envelope set by reading: `ActivityRuntime._active` (order + camp activities), `StoryDirector` active modal id, `EnlistmentBehavior.IsEnlisted`. No save serialization. |
| `StateMutator` | Gateway: `TryApplySkillXp(hero, skill, amount, reason, magnitudeHint)`, `TryAdjustCondition(...)`, `TryGiveGold(...)`, `TryWriteQuality(...)`, `TryWriteFlag(...)`, `TryAdjustScrutiny(...)`, `TryBumpPath(...)`, `TryAdjustRelation(...)`, `TryMutateRoster(...)`. Each call resolves the current envelope, checks category + magnitude band, applies or rejects. |
| `EnvelopeAcceptancePreview` | Rendered at order / activity acceptance: list of `PreviewedIds` bundled with "will grant / may cost / risks" summary. Text comes from the storylet's new `preview` field (see schema addendum). |
| `IncidentResponseModal` | Converts `CompanySimulationBehavior.ProcessIncidents` auto-apply into candidate emission → modal → player picks → effects fire under the modal envelope. |
| `ChoreThrottle` | Per-(category, source) cooldown registry. E.g. Drill cannot fire the "sharpen blade" XP-grant storylet more than once per 12 game-hours. Enforced in `StoryletCatalog.PickEligible` filter. |

### File map

| New file | Purpose |
| :--- | :--- |
| `src/Features/Agency/MutationCategory.cs` | `[Flags]` enum |
| `src/Features/Agency/MagnitudeBand.cs` | enum + helper `ClassifyAmount(category, amount)` |
| `src/Features/Agency/Envelope.cs` | POCO |
| `src/Features/Agency/AgencyGate.cs` | Static derivation service (no save state) |
| `src/Features/Agency/StateMutator.cs` | Gateway |
| `src/Features/Agency/EnvelopeAcceptancePreview.cs` | Render helper called from order/activity acceptance flows |
| `src/Features/Agency/ChoreThrottle.cs` | Per-(category, source) cooldown tracking (save-definer offset 52) |
| `src/Features/Activities/Camp/CampActivityActivity.cs` | Concrete `Activity` subclass (save-definer offset 51) |
| `src/Features/Activities/Camp/CampActivityStarter.cs` | Lifecycle starter |
| `src/Features/Activities/Camp/CampActivityMenuProvider.cs` | Slot-bank provider |
| `src/Features/Activities/Camp/CampActivityTypeJson.cs` | Two-phase template loader |
| `ModuleData/Enlisted/Activities/camp_activity.json` | Two-phase ActivityTypeDefinition (see "Activity timing" below) |
| `ModuleData/Enlisted/Storylets/camp_*.json` | Camp activity pools |
| `ModuleData/Enlisted/Storylets/incident_response_*.json` | Incident response chains |
| `ModuleData/Enlisted/Effects/scripted_effects_preview.json` | Preview-metadata addendum (optional authoring field) |

---

## Activity timing (fix for v1 blocker #3)

v1 assumed camp activities have 2–12h durations and resolve time-gated. `ActivityRuntime.ResolvePlayerChoice` at `ActivityRuntime.cs:379-383` calls `AdvancePhase` immediately, so a single `PhaseDelivery.PlayerChoice` phase resolves right away. Fix: **two-phase activity**.

```
Phase 0: "pick_intent"        — PhaseDelivery.PlayerChoice, duration 0
                                Player selects the camp activity (Drill / Rest / etc.)
                                ResolvePlayerChoice advances to Phase 1 immediately
                                Intent is locked onto the Activity

Phase 1: "execute_<intent>"   — PhaseDelivery.Auto, duration = intent-specific
                                  (Drill: 3h, Rest: 12h, Tend: 6h, Patrol: 6h,
                                   Scheme: 4h, Attend: 2h, Study: 2h)
                                fire_interval_hours = 0 (fire once at end)
                                pool contains resolution storylets for that intent
                                On duration elapse or fire, Auto runtime resolves
                                  → effects apply via StateMutator
                                Activity ends (OnBeat "activity_complete" pops envelope)
```

This matches the `FireIntervalHours` + phase-duration contract already in the runtime (see `Activity.LastAutoFireHour` gating at `storylet-backbone.md` Phase Flow). Home Evening is a one-phase shortcut because its player-choice IS the resolution; camp activities separate intent from timed execution because the physics needs the time envelope to justify the reward.

---

## Time-control taxonomy (authoritative, decompile-verified)

Bannerlord exposes exactly three map speeds via `Campaign.Current.TimeControlMode` (a `CampaignTimeControlMode`). Seven enum values; tick math at `Campaign.cs:843-875` buckets them by the `num` assignment:

| Behavior | Enum values | `TickMapTime` `num` |
| :--- | :--- | :--- |
| **Paused** | `Stop`, `FastForwardStop` | `0` |
| **Play (1×)** | `StoppablePlay`, `UnstoppablePlay` | `0.25 × realDt` |
| **Fast-forward** | `StoppableFastForward`, `UnstoppableFastForward`, `UnstoppableFastForwardForPartyWaitTime` | `0.25 × realDt × SpeedUpMultiplier` (default `4f`) |

**Play-family** (player reading or game not advancing): `Stop`, `StoppablePlay`, `UnstoppablePlay`, `FastForwardStop`.
**Fast-family**: `StoppableFastForward`, `UnstoppableFastForward`, `UnstoppableFastForwardForPartyWaitTime`.

`SpeedUpMultiplier` default `4f` (`Campaign.cs:373, :615`); settable at runtime. Unstoppable variants are game-forced (army-wait, hideout). Treat Unstoppable/Stoppable siblings identically for envelope logic.

---

## Systems converted (fuller migration table, replaces v1's narrow list)

| System | Current behavior | New behavior |
| :--- | :--- | :--- |
| `CompanySimulationBehavior.ProcessRosterRecovery` / `ProcessNewConditions` | Daily random sickness/desertion roll with auto-applied roster mutations | Delete auto-apply. Produce candidate events; surface via `IncidentResponseModal`. |
| `CompanySimulationBehavior.ProcessIncidents` | Daily incident rolls with auto-apply | Refactor to candidate emission only. Modal responses apply effects. |
| `CompanySimulationBehavior.CheckPressureArcEvents` | Calls both `StoryDirector.EmitCandidate` (Modal) AND `EventDeliveryManager.QueueEvent` (non-interactive pertinent) at `:702, :723` | Convert all to Modal-or-previewed. Replace the non-interactive `QueueEvent` arm with `StoryDirector.EmitCandidate(Pertinent)` that renders a news line but does NOT write state. |
| `PlayerConditionBehavior.ApplyDailyRecoveryAndRisk` | Daily countdown + auto-raise MedicalRisk | Under Service envelope: allow tiny Trivial-band auto-healing (1 day per 7 real game-days) — previewed at enlistment as "the body mends over time." Anything faster requires Rest / Tend / Seek Medic activity envelope. MedicalRisk auto-raise becomes Modal ("wound fever — [seek medic / ride it out]"). |
| `EscalationManager.ApplyPassiveDecay` | Time-based scrutiny decay | Delete time-based. Scrutiny drops only via Patrol / honorable-order resolves. Decay arm re-enabled as Service-envelope Trivial-band *if* no scrutiny-delta in 14+ days (so very stale scrutiny slowly fades — previewed). |
| `PathScorer.OnHeroGainedSkill` | Every vanilla skill XP bump → path score | Delete. Path bumps only via `OnIntentPicked` (intent-scoped) + order resolves that preview their path tag. |
| `EnlistmentBehavior.OnDailyTick` (wage, supply, desertion, leave, captivity, grace period) | Many state writes | Split: **Service envelope allows** wage ledger accrual, daily supply consumption, company-supply tick. **Service envelope denies** desertion-penalty application (must modal: "X days AWOL — [rejoin / accept penalty]"). Grace period + captivity state sync are read-only and allowed. |
| `EnlistmentBehavior.OnHourlyTick` (party visibility, sea state) | Read-only sync | Keep. No mutations. |
| `CampRoutineProcessor.ApplyOutcome` | Routine XP / gold / supply / news writes, triggered by phase-end | Currently fires *inside* phase resolution — already envelope-scoped by construction. Audit each outcome to ensure the grants match the phase's declared contract. Wrap all store writes in `StateMutator.Try{…}` calls. |
| `EffectExecutor.Apply` primitives | Direct `QualityStore.Add`, `FlagStore.Set`, `GiveGoldAction`, skill XP, relation changes | Route every primitive through `StateMutator.Try{…}`. Adds one indirection layer; no behavior change under normal storylet flow because storylets run inside envelopes. Calls from non-envelope contexts (debug hotkeys, boot) get explicitly whitelisted via a `StateMutator.ApplyBootstrap(...)` escape hatch. |
| `EventDeliveryManager` direct hooks (baggage, raid tracking) | Direct mutations | Audit. Convert any state write to route through `StateMutator`; the `Modal` these ride on IS the envelope. |
| `DailyDriftApplicator` | Phase-3 Signal Projection applies drift to qualities on daily tick | Gate on Service envelope. Drift magnitudes are Trivial-band by design; previewed at enlistment as "the camp's mood shifts over time." |
| `QualityBehavior.OnDailyTick` (decay) | Rule-based decay from `quality_defs.json` | Keep, but remove decay blocks from player-facing qualities (scrutiny, readiness, supplies, medical_risk, loyalty). Decay survives only for housekeeping. |
| `FlagStore.OnHourlyTick` (expiry) | Data plumbing | Keep. |
| `StoryDirector.OnDailyTick` (pacing, retries) | Content delivery | Keep. |
| `EnlistedCampaignIntelligenceBehavior.HandleHourlyTick` | Snapshot recompute + candidate emission | Keep snapshot (read-only). Candidate emission must be Modal-or-Pertinent-news, never direct state write. |
| `CampOpportunityGenerator.OnHourlyTick` (scheduled commitments) | Commitments fire on time | Each scheduled commitment becomes its own Activity envelope on fire. Before fire, player saw the commitment scheduled (preview at schedule-time). |
| `CampLifeBehavior.OnDailyTick` + listeners | Pressure recompute | Keep as read-only. |
| Menu-path gold writes (`CampMenuHandler`, `QuartermasterManager`) | Direct `GiveGoldAction.Apply…` | Route through `StateMutator.TryGiveGold`. Menu clicks ARE player-initiated Modal envelopes. |
| Pension accrual + discharge reward writes | Direct gold + flag writes | Route through `StateMutator`. Discharge ceremony IS the Modal envelope. |

**Validator rail addition:** a new Phase in `validate_content.py` scans `src/` for direct writes to player-facing stores (`QualityStore.Set/Add`, `FlagStore.Set`, `Hero.AddSkillXp`, `GiveGoldAction.ApplyBetweenCharacters`, `ChangeRelationAction.Apply...`, `hero.ChangeHeroGold` if any snuck back in) and flags any site that is not inside `StateMutator`. This is the "compile-time discipline" that prevents regression.

---

## What stays passive (correctly)

- Kingdom / faction / lord events (war, siege, clan leader death). Fire as Modals requesting a response.
- Catalog loading, JSON hot-reload, save/load plumbing.
- Intelligence snapshot recompute (read-only).
- Flag expiry sweeps.
- Pressure / world-state computation (read-only).

World-scope mutation writes are gated: a kingdom war declaration CAN emit a news-feed item (the news feed is a display) but must go through Modal to mutate anything player-facing.

---

## Chore-spam mitigations (fix for v1 blocker #5)

Four layers:

1. **Per-activity cooldown** (`ChoreThrottle`): picking the same camp activity within N in-game hours is blocked with a flavor message ("The sergeant frowns — you drilled just this morning.").
2. **Diminishing returns**: repeating the same activity within a muster period halves the reward each time (implemented as a `repeat_count` quality the activity's storylet reads).
3. **Lord patience**: Attend Lord 3x in one muster period → lord's relation modifier flips negative ("he grows tired of your presence"). Authored in the storylet pool's weight modifiers.
4. **Failure chance**: Patrol / Scheme / Study carry authored failure branches that subtract instead of add. Scheme has the steepest risk-reward; Drill the mildest. Failure outcomes are ALSO previewed at acceptance.

Also: **opportunity cost** — picking one activity advances time, so other activities are gated until enough time passes. "You can't drill AND patrol AND rest in the same 6-hour block."

---

## FF dead-end fix (fix for v1 blocker #4)

Three layers:

1. **Service envelope persists across FF**. At FF in a peaceful garrison, Service envelope is active → wage accrues, supply consumes, Trivial-band drift physics runs. State is moving but nothing high-stakes lands without a modal.
2. **Modal prompts interrupt FF** (vanilla behavior — `Campaign.Current.TimeControlMode = Stop` on Modal display). So any high-stakes event forces the player into play-family at the moment of decision.
3. **Idle-reprimand modal fires at ANY time-control speed**, not just play-family. Trigger: `IsEnlisted` AND lord stationary AND no Order / Activity envelope AND 3+ in-game days since last envelope pop. FF hits this trigger naturally since FF passes days quickly — the reprimand will pause the game and prompt within seconds of real-world FF time.

Combination: player CAN FF through peaceful garrison for a day or two, earning Service-envelope wage and drift. On the third day the reprimand forces a choice. Pacing is preserved.

---

## Save / migration (fix for v1 blocker #1)

**Envelope derivation is not persisted.** On save-load, `AgencyGate.GetCurrentEnvelopes()` recomputes by reading:

1. `EnlistmentBehavior.IsEnlisted` → adds Service envelope if true.
2. `ActivityRuntime._active` (persisted via `SyncData` at `ActivityRuntime.cs:52`) → for each active activity, derives an Order or Activity envelope.
3. `StoryDirector` active-modal state (if modal is mid-display on save) → derives Modal envelope.

A save mid-order reloads with the order still active (`_active` list restored) → `GetCurrentEnvelopes` returns the Order envelope → resolve-phase effects apply cleanly. No ephemeral scope stack to worry about.

**`ChoreThrottle`** per-(category, source) cooldowns DO persist (save-definer offset 52). They're the only agency-specific state that needs save-ride.

**Existing saves:** accumulated `scrutiny`, `readiness`, `supplies`, `medical_risk` values load as-is. Decay rules removed → values persist until a player action changes them. One-shot migration on first load after patch: zero any in-flight `InjuryDaysRemaining` / `IllnessDaysRemaining` above 7 (prevents "old save healed you via deleted countdown" confusion).

**Save-definer offsets claimed:**
- 51 — `CampActivityActivity`
- 52 — `ChoreThrottleStore`

---

## Storylet schema addendum

Add an optional `preview` field to storylets used as envelope entry points (order-accept, camp-activity entry, modal display):

```json
{
  "id": "order_scout_ranger_t1",
  "preview": {
    "grants": ["scouting_xp_moderate", "ranger_path_bump_small"],
    "may_cost": ["fatigue_minor", "small_wound_risk"],
    "risks": ["ambush_encounter"]
  },
  ...
}
```

`EnvelopeAcceptancePreview` reads this and renders it into the acceptance dialogue / modal body so the player SEES the contract before consenting. Storylets without a `preview` block are treated as "Trivial contract — no advertised consequences beyond the storylet's own option effects."

Validator addition: Phase 16 (new) — any storylet with effects whose declared magnitude exceeds Trivial MUST carry a `preview` block. Missing preview on a high-stakes storylet is a build error.

---

## Playflows (revised for envelope model)

### Playflow 1 — Fresh recruit, Day 1, peaceful garrison

1. Enlistment dialogue → Service envelope becomes active (enlistment is the envelope acceptance; preview read at acceptance was "you earn pay daily, consume supplies while marching, take small knocks in routine duty — anything bigger requires an order").
2. Main menu: `DISPATCHES` + `UPCOMING`. Player opens Camp → 7 activity slots (with `ChoreThrottle` showing empty cooldowns).
3. Picks **Drill** (One-Handed). Activity envelope pushes on top of Service. Phase 0 resolves immediately (intent picked), Phase 1 runs 3h. End-of-phase storylet fires: `+One-Handed XP`, `+readiness`. StateMutator applies under Activity envelope's declared contract. Activity ends.
4. Player fast-forwards 4 hours. Service envelope allows Trivial-band drift: tiny fatigue, tiny supply tick. Previewed at enlistment. No big surprises.
5. At 3 hours of FF with no new envelope picked, idle-reprimand fires (trigger fires under any speed). Player must pick.

### Playflow 2 — Scout order arc

1. `UPCOMING` shows "Scout the eastern ridge — grants: `scouting_xp_moderate`, `ranger_path_bump_small`; may cost: `fatigue_minor`; risks: `ambush_encounter`. [Accept]" (preview rendered from storylet's `preview` block).
2. Accept → Order envelope pushes. 3-day arc. Service envelope remains underneath.
3. Day 2: mid-arc Modal fires ("tracks in the mud"). Modal envelope pushes on top. Player picks Follow. Modal envelope resolves → mutations apply under the modal's declared contract → Modal envelope pops.
4. Day 3: arc resolves. Order envelope pops. Service envelope still active.

### Playflow 3 — Kingdom war mid-activity

1. Player in Drill. Vlandia declares war (world-scope passive event).
2. Event emits a Modal candidate ("runners cry through camp — read the proclamation"). `StoryDirector` queues it. Game does NOT force-pause because the Modal isn't immediate-impact (read-only lore).
3. Drill finishes naturally. Proclamation Modal fires after. Player reads. No state mutation — the Modal's effects list is empty; this is informational.
4. Day later, Named order "Join the march on Pravend" proposes. Order envelope acceptance preview warns: "may cost `march_fatigue_major`, risks `front_line_battle`." Player opts in or out.

### Playflow 4 — Wounded, recovery

1. Battle storylet wrote `InjurySeverity=Moderate, days=7` via the gate under a Modal envelope. Scope popped.
2. Under Service envelope, Trivial-band auto-heal is allowed: 1 day per 7 real-game-days. Previewed at enlistment.
3. Player wants faster recovery → picks **Rest** (Activity envelope): 12h scope, resolves `InjuryDays -= 2`.
4. Three more Rest picks → cleared. Alternative: wait 49 in-game days of Service-envelope drift → also cleared, slowly.

### Playflow 5 — Scrutiny pressure

1. Scrutiny at 68. No time-based decay.
2. `UPCOMING`: "Lieutenant requests to speak. [Attend]" — preview: "may grant `scrutiny_down_major`, may cost `relation_down_small`, risks `caught_in_lie_flag`."
3. Modal fires with options. Player picks. Effects under the modal envelope.
4. Alternatively 3 Patrol picks → scrutiny earned down.
5. If scrutiny stays >= 70 for 14 days without change → Service envelope allows Trivial-band decay of 1 / 14 days (previewed). Very slow, keeps stale state from sticking forever.

### Playflow 6 — T4 path crossroads

1. Rank 4 reached. `PathCrossroadsBehavior` emits Modal via `StoryDirector.EmitCandidate(ChainContinuation=true)`. Modal envelope pushes.
2. Storylet has its own `preview` block: "Commit grants `+1 focus(Bow) / +10 renown / commits path=ranger permanently`. Resist grants `path_resisted_ranger flag (halves future score bumps)`."
3. Player picks Commit. `commit_path` primitive fires through `StateMutator.TryWriteFlag` under the Modal envelope's contract. Envelope pops.

---

## Phasing (revised)

- **Phase 1 — Define + instrument.** Author `MutationCategory` + `MagnitudeBand` + `Envelope` + `AgencyGate` + `StateMutator`. Make `StateMutator.Try{…}` observer-only: calls underlying stores and logs what would have been rejected. Add `validate_content.py` Phase 16 (preview-on-high-stakes). Deploy; collect one week of logs.
- **Phase 2 — Audit feedback.** Read Phase 1 logs. Any unexpected mutation sources (mods that write to `QualityStore` directly, boot-time writes) get explicit whitelist entries via `StateMutator.ApplyBootstrap(...)`. Tune magnitude-band thresholds.
- **Phase 3 — Convert menu + effect-executor paths.** `EffectExecutor` primitives route through `StateMutator`. Menu gold writes route through `StateMutator`. Routine outcomes route through `StateMutator`. Still observer mode.
- **Phase 4 — Flip gate to enforce.** `StateMutator.Try{…}` now rejects mutations that fail category / magnitude check. Top-5 passive handlers either get proper envelopes, get removed, or get converted to Modal-or-Pertinent-news emission. Player-facing quality decay rules deleted.
- **Phase 5 — Ship `CampActivityActivity`.** Two-phase template, 7 slots, ~50 storylets. `ChoreThrottle` save-definer offset 52. Preview metadata on every slot.
- **Phase 6 — Convert incident handlers.** `CompanySimulationBehavior.Process{RosterRecovery,NewConditions,Incidents}` rewired through `IncidentResponseModal`. `CheckPressureArcEvents` converts the `QueueEvent` arm to Pertinent-news.
- **Phase 7 — Content tuning.** Re-balance rewards so skill progression pace is acceptable. Author missing previews on existing orders.
- **Phase 8 — Idle-reprimand + FF handling.** Fires on any time-control speed. Tuned to 3 in-game days. Skip during active world events.

Budget: 3–4 weeks. Each phase is reviewable independently.

---

## Risks

1. **Service envelope too permissive.** If Service's Trivial band allows too much, we're back to v1's complaint. Mitigation: Phase 2 tune from Phase 1 log data; Trivial band capped tightly.
2. **Preview-authoring burden.** Every high-stakes storylet needs a `preview` block. Phase 7 content pass, Phase 16 validator catches misses.
3. **Migration surface risk.** Phase 3 routes many mutation sites through `StateMutator`. Observer mode (Phase 1–3) catches surprises before Phase 4 enforces.
4. **Modal fatigue.** If we modal every incident, players get spam. Mitigation: `StoryDirector` pacing already handles this via per-category cooldowns.
5. **Plan 1–5 smoke regressions.** Scenarios A–H must re-run post-Phase 4. Envelope-lookup on `ActivityRuntime._active` is cheap (O(active count), typically 0–2).
6. **FF in Service envelope still invisible.** Wage accrues, supply consumes, but the numeric change at speed might feel uncanny. Mitigation: news-feed inserts a line "Day 12 — wages logged, supplies at 82%" every N in-game days under FF so the change is communicated.
7. **ChoreThrottle authoring burden.** Every camp storylet needs a cooldown key. Keep shared throttles where possible (all Drill storylets share `drill_daily` throttle, etc.).

---

## Testing

| Test | Method | Expected |
| :--- | :--- | :--- |
| **Idle-freeze at FF (revised)** | Enlisted + lord stationary + no order/activity. FF for 2 in-game days. | Service envelope active → small wage delta + small supply delta observed and previewed. No big state changes. Day 3 → idle-reprimand modal fires at FF. |
| **Scope-matched mutation** | Accept scout order → run to resolve. | Mutations match storylet's `preview` block's `grants` / `may_cost`. Nothing in excess. |
| **Envelope stack derivation on save-load** | Save mid-order → reload → observe envelope stack. | Order envelope derived correctly from `ActivityRuntime._active`. Resolve-phase effects land. |
| **Camp activity two-phase** | Pick Drill from slot bank → time advances 3h → end-storylet fires. | `StateMutator` applies effects; activity ends cleanly. |
| **ChoreThrottle cooldown** | Drill → immediately attempt Drill again. | Slot rejects with "sergeant frowns." |
| **Preview rendering** | Accept an order with `preview` block. | Acceptance dialogue shows grants / costs / risks. |
| **Non-previewed high-stakes storylet** | Author a test storylet with Major-band effects and no `preview`. | `validate_content.py` Phase 16 rejects at build. |
| **Incident modal** | Force incident candidate → verify modal renders, effects apply only on pick. | Pass. |
| **Plans 1–5 scenarios A–H** | Re-run all. | All pass. |
| **Idle-reprimand fires at FF** | FF for 3 in-game days with no envelope. | Modal fires, game force-pauses. |

---

## Open questions

1. **Service envelope's Trivial band per category** — specific numbers need Phase 1 log data. Start with: skill XP ≤ 10 per real-game-day; gold ± ≤ 5; condition day ≤ 0.15 / real-game-day; scrutiny ± ≤ 1 per 14 days; no path or relation writes.
2. **Pension accrual** — lives in service envelope or order envelope? Lean service (it's continuous) — but then the "paid at muster" modal IS what prevents the bookkeeping from feeling unearned.
3. **How many camp activities Phase 5** — 7 in spec; fallback to 5 if authoring is slow (drop Scheme, Study; add them in Phase 9).
4. **What about `PathScorer.OnHeroGainedSkill`** — deletion is correct under the new contract (path bumps are Major-band, need explicit intent). But Plans 1–5 rely on path scores existing; the storylet-backbone spec may need a pointer update.
5. **`validate_content.py` Phase 16 — which `MagnitudeBand` threshold triggers "preview required"?** Lean: Moderate and above require preview. Trivial / Minor are exempt.
6. **ChoreThrottle save-definer offset 52** — verify no collision with Plan 4's `DutyCooldownStore` at 50 and Plan 1's `EnlistedLordIntelligenceSnapshot` at 48. Gap at 51 for `CampActivityActivity`. 52 is free.

---

## Relation to other specs

- **News-unification spec v2** (`docs/superpowers/specs/2026-04-23-news-and-status-unification-design.md`): paused. Once agency ships, revisit; `CAMP ACTIVITIES` pulls now read envelope-logged events instead of raw passive outcomes. News spec's FastForwardStop bucket already fixed in same commit.
- **Plan 5 Career Loop** (`docs/superpowers/plans/2026-04-21-career-loop-closure.md`): crossroads already scope-correct (Modal envelope on fire). No change. `PathScorer.OnHeroGainedSkill` deletion removes the auto-bump path, so path scores will accumulate slower — authors may want to tune intent-pick bumps upward (current `+3` in `PathScorer.cs:86`).
- **Plan 1 Campaign Intelligence**: snapshot stays read-only.
- **Plan 4 Duty Opportunities**: ambient duty storylets emit via `StoryDirector` as Modal candidates — already envelope-correct. Audit Phase 3 to confirm no direct writes bypass the gateway.
- **Plan 2 Lord AI**: zero player-state mutations, only biases `MBGameModel`. Unaffected.

---

## Changelog vs v1

- **Contract reframed** from "no state change between decisions" to "no unpreviewed high-stakes consequences." Baseline service physics allowed inside envelopes.
- **Envelope model** replaces `DecisionScope` stack. Envelopes derive from persistent state (`ActivityRuntime._active`, `EnlistmentBehavior.IsEnlisted`, `StoryDirector` modal state) — no separate ephemeral stack to persist. Fixes save/load.
- **Service envelope** added as the default low-magnitude envelope for enlisted play. Fixes FF dead-end.
- **Migration table widened** to include `EffectExecutor`, `EventDeliveryManager`, `CampRoutineProcessor`, `DailyDriftApplicator`, menu gold paths, pension, discharge. Plus a new `validate_content.py` Phase 16 that blocks new direct writes at build time.
- **Camp activity** now two-phase (instant pick-intent + timed execute-intent) to match `ActivityRuntime.ResolvePlayerChoice` actual runtime behavior.
- **Chore-spam mitigations** added: `ChoreThrottle` cooldown, diminishing returns, lord patience, failure chances, opportunity cost.
- **FF-safe idle-reprimand** fires at any time-control speed.
- **False "keep" claims fixed.** `CheckPressureArcEvents` needs its `QueueEvent` arm converted. `EnlistmentBehavior.OnDailyTick` explicitly split between Service-allowed (wage/supply) and Modal-required (desertion).
- **Preview metadata** added to storylet schema; rendered at envelope acceptance; enforced by validator Phase 16.
- **Two save-definer offsets claimed**: 51 (`CampActivityActivity`), 52 (`ChoreThrottleStore`). Was 1 in v1.
