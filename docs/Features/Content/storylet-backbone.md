# Storylet Backbone — Reference

**Status:** Live. This is the authoritative day-to-day reference for authoring against the storylet backbone (Spec 0, shipped 2026-04-19).

**For design rationale / problem framing:** see the retired design doc at [`superpowers/specs/2026-04-19-storylet-backbone-design.md`](../../superpowers/specs/2026-04-19-storylet-backbone-design.md). The spec is frozen-in-time; this file is updated as the substrate evolves.

**For execution history:** see the retired plan at [`superpowers/plans/2026-04-19-storylet-backbone.md`](../../superpowers/plans/2026-04-19-storylet-backbone.md).

The backbone is the content layer beneath [`StoryDirector`](../../superpowers/specs/2026-04-18-event-pacing-design.md). Surface specs 1–5 (Home, Orders, Land/Sea, Promotion+Muster, Quartermaster) author **storylets** against this substrate; they do not author raw `EventDefinition` JSON.

---

## When to use this doc

| Task | Section |
| :--- | :--- |
| Pick a scripted-effect id for a storylet option | [Scripted-effect seed catalog](#scripted-effect-seed-catalog) |
| Add a new scripted effect | [Scripted-effect seed catalog](#scripted-effect-seed-catalog) + [Primitive effects](#primitive-effects) |
| Read or write a quality (Scrutiny, Rank XP, Lord Relation, …) | [Quality registry](#quality-registry) |
| Check what a named trigger does | [Seed triggers](#seed-triggers) |
| Bind `{veteran}` / `{lord}` in a storylet | [Slot roles](#slot-roles) |
| Claim a save-definer offset for a new `Activity` subclass | [Save-definer offsets](#save-definer-offsets) |
| Route a follow-up event | [Chains](#chains) |
| Understand where the C# lives | [File map](#file-map) |

---

## Vocabulary

Canonical terms. Surface specs use these names; legacy terms are migrated out, not kept alongside.

| Canonical term | What it is |
| :--- | :--- |
| **Storylet** | Self-contained authored narrative unit: title, setup, trigger list, options, effects |
| **Choice** | One option inside a storylet |
| **Trigger** | Named C# predicate resolved by string ID (`is_enlisted`, `quality_gte:scrutiny:75`, `not:flag:x`) |
| **Immediate** | Effect list applied on storylet fire, before the player sees options |
| **Effect** | Unit of outcome — inline primitive (`quality_add`) or a reference to a scripted-effect id (`lord_relation_up_small`) |
| **Scripted effect** | Reusable named effect body in `scripted_effects.json` |
| **Quality** | Typed numeric state with min/max, decay rule, optional hero-scoped |
| **Flag** | Named boolean state with optional expiry; global or hero-scoped |
| **Scope** | Declared context of a storylet — `context: "sea"`, plus named slots |
| **Slot** | Placeholder in storylet text or trigger (`{lord}`, `{veteran}`); filled at fire time |
| **Chain** | `{ id, days }` or `{ id, when: "activity.phase" }` — continuation primitive |
| **Activity** | Stateful, phased, intent-biased, time-bounded player engagement |
| **Intent** | Player's declared purpose at activity start; biases phase-pool weights |
| **Phase** | Time window inside an activity with its own storylet pool and delivery mode |
| **Pool** | Storylet ids eligible in a phase |
| **Emission** | A candidate a source hands to the Director (`StoryCandidate`) |

---

## File map

| Subsystem | Location |
| :--- | :--- |
| Storylet POCOs + defaults | `src/Features/Content/Storylet.cs`, `Choice.cs`, `EffectDecl.cs`, `ScopeDecl.cs`, `SlotDecl.cs`, `WeightDecl.cs`, `ChainDecl.cs`, `StoryletContext.cs` |
| Storylet loader | `src/Features/Content/StoryletCatalog.cs` (reads `ModuleData/Enlisted/Storylets/*.json`) |
| Trigger dispatch | `src/Features/Content/TriggerRegistry.cs` |
| Primitive effect dispatch | `src/Features/Content/EffectExecutor.cs` |
| Scripted effect registry | `src/Features/Content/ScriptedEffectRegistry.cs` (reads `ModuleData/Enlisted/Effects/scripted_effects.json`) |
| Slot resolver + text flattening | `src/Features/Content/SlotFiller.cs` |
| Quality subsystem | `src/Features/Qualities/QualityStore.cs`, `QualityCatalog.cs`, `QualityBehavior.cs` (reads `ModuleData/Enlisted/Qualities/quality_defs.json`) |
| Flag subsystem | `src/Features/Flags/FlagStore.cs`, `FlagBehavior.cs` |
| Activity subsystem | `src/Features/Activities/Activity.cs`, `ActivityRuntime.cs`, `Phase.cs`, `ActivityTypeDefinition.cs`, `ActivityContext.cs`, `ActivityEndReason.cs`, `PhaseDelivery.cs` |
| Save registrations | `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` — class offsets 40, 41, 43, 44; enum offsets 82, 83 |
| Validator phase | `Tools/Validation/validate_content.py` Phase 12 (scripted-effect refs + quality ids) |

`Enlisted.csproj` registers every `.cs` file (AGENTS.md §2). New `.cs` files must be added before building.

---

## Quality registry

Seed quality definitions. `Stored` = data lives in `QualityStore`. `ReadThrough` = reader delegate proxies a native TaleWorlds value; writes (if any) route through a native action.

| Id | Range | Scope | Kind | Writable | Source / writer |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `scrutiny` | 0–100 | Global | Stored | yes | `QualityStore` — absorbed the old Discipline track |
| `medical_risk` | 0–5 | Global | Stored | yes | `QualityStore` |
| `supplies` | 0–100 | Global | Stored | yes | `QualityStore` (company needs) |
| `readiness` | 0–100 | Global | Stored | yes | `QualityStore` (company needs) |
| `loyalty` | 0–100 | **PerHero** | Stored | yes | `QualityStore` — retinue-soldier-scoped |
| `rank_xp` | 0–∞ | Global | ReadThrough | yes | reader `EnlistmentBehavior.EnlistmentXP` / writer `AddEnlistmentXP(delta, reason)` |
| `days_in_rank` | 0–∞ | Global | ReadThrough | no | reader `EnlistmentBehavior.DaysInRank` |
| `battles_survived` | 0–∞ | Global | ReadThrough | yes (positive only) | reader `EnlistmentBehavior.BattlesSurvived` / writer loops `IncrementBattlesSurvived()` |
| `rank` | 1–9 | Global | ReadThrough | no | reader `EnlistmentBehavior.EnlistmentTier` — write via `EnlistmentBehavior.SetTier` only |
| `days_enlisted` | 0–∞ | Global | ReadThrough | no | reader `EnlistmentBehavior` |
| `lord_relation` | -100–+100 | Global | ReadThrough | yes | reader `EnlistedLord.GetRelationWithPlayer()` / writer `ChangeRelationAction.ApplyPlayerRelation` |

**Writing a read-only quality from a storylet effect is a build error** (validate_content.py Phase 12). Rank advances through the promotion-ceremony chain, not a raw `quality_set`.

**Decay rule.** `requireIdle: true` means "don't decay while the quality has been written in the last `intervalDays`." `QualityStore.Add` / `AddForHero` stamp `LastChanged` on every write; decay short-circuits when `daysSinceChanged < intervalDays`.

**Threshold crossings are authored, not automatic.** Author `trigger: ["quality_gte:scrutiny:75", "not:flag:scrutiny_75_seen"]` + option effect `{ apply: "set_flag", name: "scrutiny_75_seen", days: 30 }`. When the flag decays the storylet is re-eligible.

---

## Flag conventions

- Flag names are snake_case strings. No enum — mint flags freely in JSON.
- `FlagStore.Set(name, expiry)` — `CampaignTime.Never` = permanent; `CampaignTime.DaysFromNow(n)` = time-boxed.
- Hero-scoped flags persist with the hero's GUID. `FlagStore.PurgeMissingHeroes()` runs on `OnGameLoaded` and drops flags for heroes who no longer exist.
- Arc progress: convention is `flag:arc_<arcname>_step_N` + `flag:arc_<arcname>_complete` on the final step.

---

## Seed triggers

`TriggerRegistry.EvaluateOne(trigger, ctx)` supports the `not:` prefix on any trigger. `Evaluate(triggers, ctx)` is conjunction (all must pass). Weight modifiers (`Storylet.Weight.Modifiers[].If`) are independent — each `mod.If` is its own `EvaluateOne` call.

| Trigger | Arg form | What it checks |
| :--- | :--- | :--- |
| `is_enlisted` | — | `EnlistmentBehavior.Instance.IsEnlisted` |
| `is_at_sea` | — | `MobileParty.MainParty.IsCurrentlyAtSea` |
| `in_settlement` | — | `MobileParty.MainParty.CurrentSettlement != null` |
| `in_siege` | — | `BesiegerCamp` set OR `CurrentSettlement.SiegeEvent` non-null |
| `in_mapevent` | — | `MobileParty.MainParty.MapEvent != null` |
| `context:<name>` | 1 arg | `ctx.CurrentContext` equals arg (case-insensitive) |
| `flag:<name>` | 1 arg | `FlagStore.Instance.Has(name)` |
| `quality_gte:<id>:<n>` | 2 args | `QualityStore.Get(id) >= n` |
| `quality_lte:<id>:<n>` | 2 args | `QualityStore.Get(id) <= n` |
| `rank_gte:<n>` | 1 arg | `EnlistmentBehavior.EnlistmentTier >= n` |
| `veteran_has_flag:<name>` | 1 arg | `ctx.ResolvedSlots["veteran"]` exists and has the hero-scoped flag |
| `beat:<name>` | 1 arg | Marker only — returns `true`; beat matching is upstream at emission |

Register additional named predicates via `TriggerRegistry.Register(name, predicate)` at boot. Keep predicates flat (static delegates); cap one file per surface spec for ownership clarity.

---

## Slot roles

`SlotFiller.Resolve(scope)` picks a `Hero` per declared slot. Dotted text references (`{veteran.name}`, `{lord.culture}`) flatten to native `SetTextVariable` keys (`VETERAN_NAME`, `LORD_CULTURE`) at render time.

| Role | Returns |
| :--- | :--- |
| `enlisted_lord` | `EnlistmentBehavior.EnlistedLord` |
| `squadmate` | A random alive `Hero` with `Occupation.Soldier`; if `tag` declared, filtered to heroes with that hero-scoped flag |
| `chaplain` | First alive hero tagged with hero-scoped flag `chaplain` |
| `quartermaster` | `EnlistmentBehavior.QuartermasterHero` |
| `enemy_commander` | Opposing-side leader hero in the current `MapEvent` |
| `settlement_notable` | First notable in `MobileParty.MainParty.CurrentSettlement.Notables` |
| `kingdom` | Returns null (faction-style role — callers read `scope.slots` meta directly) |
| `named_veteran` | Returns null; stub reserved for a surface spec that wires a named-query API |

Unknown roles log `Expected("SLOT", "unknown_role_<role>", ...)` once and return null.

**Available text properties on any filled slot:** `.name`, `.occupation`, `.culture`. Extend `SlotFiller.Render` to add more.

---

## Primitive effects

`EffectExecutor.ApplyOne` dispatches these. Unknown primitives log `Expected("EFFECT", "unknown_primitive_<apply>", ...)` once and no-op.

| Primitive | Parameters |
| :--- | :--- |
| `quality_add` | `quality`, `amount` |
| `quality_add_for_hero` | `quality`, `amount`, `slot` |
| `quality_set` | `quality`, `value` |
| `set_flag` | `name`, `days?` (default permanent) |
| `clear_flag` | `name` |
| `set_flag_on_hero` | `slot`, `name`, `days?` |
| `trait_xp` | `trait`, `amount` (single-level step via `Math.Sign(amount)`) |
| `skill_xp` | `skill`, `amount` (positive only) |
| `give_gold` | `amount` (positive grant / negative charge, routed through `GiveGoldAction`) |
| `give_item` | `item_id`, `count` |

**Gold must route through `GiveGoldAction`** — never `Hero.ChangeHeroGold` (AGENTS.md Critical Rule #3).

---

## Scripted-effect seed catalog

Shared across all surface specs. Prefer reusing these; only mint a new scripted effect when no existing one fits. Inventing a one-off `apply` id that isn't in `scripted_effects.json` and isn't a primitive fails validation.

Defined in `ModuleData/Enlisted/Effects/scripted_effects.json`:

| Effect id | Body | Use for |
| :--- | :--- | :--- |
| `rank_xp_minor` | `quality_add rank_xp +25` | Chore done well, word in the right ear |
| `rank_xp_moderate` | `quality_add rank_xp +75` | Leading a patrol, besting a duel, lord's public thanks |
| `rank_xp_major` | `quality_add rank_xp +200` | Heroic — saved a noble, decisive battlefield act, arc climax |
| `battles_survived_credit` | `quality_add battles_survived +1` | Scouting ambush, escort skirmish, siege sortie — real danger but not a main battle |
| `lord_relation_up_small` | `+2 lord_relation`, `+1 loyalty` | Small courtesy, capable report |
| `lord_relation_up_moderate` | `+5 lord_relation`, `+2 loyalty` | Solid favour — saved him embarrassment, good counsel |
| `lord_relation_up_major` | `+10 lord_relation`, `+3 loyalty` | Life-debt moment |
| `lord_relation_down_small` | `-2 lord_relation` | Blunt reply, missed a flourish |
| `lord_relation_down_major` | `-10 lord_relation`, `+5 scrutiny` | Public insubordination |
| `scrutiny_up_small` | `+3 scrutiny` | Raised eyebrow, a rumor |
| `scrutiny_up_moderate` | `+7 scrutiny` | Written report, NCO remembers |
| `scrutiny_up_major` | `+15 scrutiny`, `set_flag under_investigation days:5` | Caught at something; sergeant has a notebook |
| `scrutiny_down_small` | `-3 scrutiny` | Good act rebuilds your name |
| `scrutiny_down_moderate` | `-7 scrutiny` | Public redemption — pulled comrade from danger |
| `medical_risk_up_small` | `+1 medical_risk` | Light wound, shaken |
| `medical_risk_up_major` | `+3 medical_risk`, `set_flag wounded days:7` | Serious injury |
| `supplies_up_small` | `+5 supplies` | Forage success, merchant windfall |
| `supplies_down_moderate` | `-15 supplies` | Wagon lost, cask spoiled, raiders hit train |
| `readiness_up_small` | `+3 readiness` | Successful drill, kit inspection passed |
| `readiness_down_moderate` | `-10 readiness` | Long march bad weather, gear neglected |
| `loyalty_up_small_for_slot` | `set_flag_on_hero {slot} loyalty_boost_recent days:5`, `quality_add_for_hero loyalty +2` | Retinue member's loyalty nudged up |
| `loyalty_down_major_for_slot` | `quality_add_for_hero loyalty -5` | Retinue mistreated, betrayal |

**Recursion cap.** `EffectExecutor` caps scripted-effect expansion at depth 8. A cyclic catalog (A→B→A) trips `Expected("EFFECT", "scripted_depth_limit", ...)` with the offending `apply` id in the log ctx — the chain no-ops at the cap.

---

## Save-definer offsets

`src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`. **Grep before claiming an offset — collisions corrupt saves silently.**

| Kind | Offset | Type / owner |
| :--- | :--- | :--- |
| Class | 40 | `QualityStore` |
| Class | 41 | `QualityValue` |
| Class | 42 | `QualityDefinition` (registered for completeness; not saved directly) |
| Class | 43 | `FlagStore` |
| Class | 44 | `Activity` (abstract base for the polymorphic `List<Activity>` container) |
| Class | 45 | `HomeActivity` (Spec 1) |
| Class | 46 | `OrderActivity` (Spec 2) |
| Class | 47 | `NamedOrderState` (Spec 2) |
| Class | 48 | `EnlistedLordIntelligenceSnapshot` (Plan 1 / Campaign Intelligence) |
| Class | 49 | `SignalEmissionRecord` (Plan 3 / Signal Projection) |
| Class | 50 | `DutyCooldownStore` (Plan 4 / Duty Opportunities) |
| **Class** | **51–60** | **Reserved** for remaining surface specs (Specs 3–5) |
| Enum | 82 | `QualityScope` |
| Enum | 83 | `ActivityEndReason` |
| Enum | 85 | `DutyProfileId` (Spec 2) |
| **Enum** | **86+** | **Reserved** for Specs 3–5 |

Surface specs claim their class offset from the 45–60 range in their owning spec (Activity subclasses AND closely-related surface-spec persistent state per AGENTS.md Rule #11). Current claims: `HomeActivity` at **45** (Spec 1); `OrderActivity` at **46** + `NamedOrderState` at **47** (Spec 2); `EnlistedLordIntelligenceSnapshot` at **48** (Plan 1); `SignalEmissionRecord` at **49** (Plan 3); `DutyCooldownStore` at **50** (Plan 4). Spec 2 also claims enum offset **85** (`DutyProfileId`); Plan 1 claims enum offsets 86-98; Plan 3 claims enum offsets 99-103. `TaleWorlds.Core.FormationClass` is referenced by `OrderActivity.CachedCombatClass` and `NamedOrderState.CombatClassAtAccept` but is NOT registered in our definer — `TaleWorlds.Core.SaveableCoreTypeDefiner` already registers it at id 2008, and the shared `DefinitionContext` dictionary throws on duplicate `Type` keys (crashes `Module.Initialize` natively, no mod log). Reserved next claims in the 51–60 / 86+ band: `LandSeaActivity` (Spec 3), `MusterActivity` (Spec 4), `QuartermasterActivity` (Spec 5). Grep `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` before claiming — collisions corrupt saves silently.

Pre-Spec-0 claims (class 1, 10–14, 20–24, 30; enum 50–52, 60–64, 70–71, 80–81) remain owned by their prior subsystems — don't reuse.

---

## Chains

Two chain forms on a `Choice`:

```json
"chain": { "id": "slt_followup", "days": 7 }
```

Time-delay — fires after an in-game delay. Parked in `StoryDirector.ScheduleChain`, persisted in `PendingChainEvents`.

```json
"chain": { "id": "slt_promoted", "when": "next_muster.promotion_phase" }
```

Phase-target — parks on `ActivityRuntime.ParkChain(activityTypeId, phaseId, storyletId)` until the named activity reaches the named phase. Survives saves.

Either form sets `ChainContinuation = true` on the resulting `StoryCandidate`, so the Director's in-game floor + category cooldown don't defer it (60s wall-clock guard still applies — see [pacing spec](../../superpowers/specs/2026-04-18-event-pacing-design.md) §4).

Arc conventions: each storylet's chain links to the next; `flag:arc_*_step_N` tracks progression; final storylet sets `flag:arc_*_complete`.

---

## Generic primitives from Spec 1 (inherited by Specs 2–5)

Spec 1 Phase A (Tasks 1–5, `development` commits `fc93285`→`24b7e50`) shipped three generic pieces that every surface spec (2–5) inherits, and Phase B (Tasks 6–9, `e4b6806`→`a48983c`) added a fourth (`ActivityContext.FromCurrent()`) plus the first concrete subclass (`HomeActivity` at save offset 45) as the canonical reference implementation. Phase C (Tasks 10–12, `18b4021`→`19391a4`) wired the Evening slot-bank on `enlisted_status` via `HomeEveningMenuProvider` — the canonical pattern for `PhaseDelivery.PlayerChoice` menu integration (preallocated N-slot option bank, per-intent eligibility check, priority-9 visibility gated by current phase). You don't need to re-implement any of these; just know how to use them.

### `ActivityTypeCatalog.LoadAll()`

**Where to put activity JSON:** `ModuleData/Enlisted/Activities/*.json`. Each file is one `ActivityTypeDefinition` object (not an array). `LoadAll()` is called from `ActivityRuntime.OnSessionLaunched` and again from `OnGameLoaded`; both passes register types with `ActivityRuntime` so fresh-session and save-reload paths both work.

**Required JSON fields:**

```json
{
  "id": "my_activity",
  "display_name": "My Activity",
  "valid_intents": ["unwind", "train_hard"],
  "phases": [
    {
      "id": "phase_one",
      "duration_hours": 2,
      "fire_interval_hours": 0,
      "delivery": "auto",
      "pool": ["storylet_id_a", "storylet_id_b"],
      "intent_bias": { "unwind": 1.2, "train_hard": 0.9 },
      "player_choice_count": 4
    }
  ]
}
```

**Key field semantics:**

| Field | Type | Meaning |
| :--- | :--- | :--- |
| `id` | string | Must match `Activity.TypeId` set in `OnStart()` — otherwise `ResolvePhasesFromType` no-ops and the activity has zero phases |
| `display_name` | string | Human-readable label (for debug/UI); not rendered in vanilla menus by default |
| `valid_intents` | string[] | Intent values this activity accepts; used to filter initial intent selection |
| `fire_interval_hours` | int | `0` = fire at most once per phase entry; `N` = fire at most once every N in-game hours during the phase window. Paced against `Activity.LastAutoFireHour` |
| `delivery` | string | `"auto"` or `"player_choice"` — maps to `PhaseDelivery` enum |
| `intent_bias` | object | Per-intent float multiplier applied to storylet weights during `StoryletSelector.Pick`. Missing intents default to `1.0` |
| `player_choice_count` | int | How many options the menu slot-bank should surface (default 4; Spec 1 uses 5) |

**Error behavior:** A file with a missing or empty `id` logs `Expected("ACTIVITY", "bad_activity_file", ...)` and is skipped. A file that fails to parse logs `Surfaced("ACTIVITY", "Failed to load activity type", ...)` with the file path in ctx.

---

### `StoryletEventAdapter.BuildModal(Storylet, StoryletContext, Activity)`

**What it returns:** an `EventDefinition` (not `InteractiveEvent` — the mod's Modal delivery unit is `EventDefinition`, placed in `StoryCandidate.InteractiveEvent`). `StoryDirector.Route` requires `candidate.InteractiveEvent != null` to take the Modal path (`StoryDirector.cs:218`); the adapter fills this field.

**Typical call pattern** (in a menu provider or phase-fire handler):

```csharp
var evt = StoryletEventAdapter.BuildModal(picked, ctx, activity);
var candidate = picked.ToCandidate(ctx);
candidate.InteractiveEvent = evt;          // fills the Modal gate
candidate.ProposedTier = StoryTier.Modal;
candidate.ChainContinuation = true;        // bypass in-game floor + category cooldown (pacing spec §6.2)
StoryDirector.Instance?.EmitCandidate(candidate);
```

**Synthetic IDs:** `BuildModal` generates `"storylet_{id}_{monotonic_counter}"` as the `EventDefinition.Id`. The counter is global and monotonic so the same storylet can fire twice (rare, but possible) without key collisions in the pending-effects registry.

**Effect bridging:** `Choice.Effects` (`List<EffectDecl>`) are stored in a static `_pendingEffects` registry keyed by `(syntheticEventId, choiceId)`. They are **not** put into `EventOption.Effects` (which uses the legacy `EventEffects` shape). Instead, `EventDeliveryManager.OnOptionSelected` calls `StoryletEventAdapter.DrainPendingEffects(eventId, optionId)` after its normal effect pass — this hook is already wired as of Phase A.

**Chain parking:** After applying effects, `DrainPendingEffects` calls `ParkChainIfAny`, which reads `Choice.Chain.When` and calls `ActivityRuntime.Instance?.ParkChain(typeId, phaseId, chain.Id)`. The `when` field uses `"{typeId}.{phaseId}"` syntax (e.g. `"home_activity.evening"`). An absent `when` field falls back to the owner activity's current type/phase.

**Fallback option:** If no `Choice` passes its option-level trigger, the adapter inserts a synthetic `"continue"` option (`text: "{=home_continue}Continue."`) so the modal never renders with zero buttons.

---

### `PhaseDelivery.PlayerChoice` runtime

**How `_activeChoicePhase` works:** When `ActivityRuntime` enters a phase whose `Delivery == PhaseDelivery.PlayerChoice`, it sets `_activeChoicePhase = (activity, phase)`. This field is the signal that a choice phase is live. `TryFireAutoPhaseStorylet` returns early when `phase.Delivery != PhaseDelivery.Auto`, so auto-fire is suppressed during a PlayerChoice phase.

**Who reads it:** Menu providers (e.g. `HomeEveningMenuProvider`) call `ActivityRuntime.Instance?.GetActiveChoicePhase()` in their slot-bank condition callbacks. If the return is null or the activity type doesn't match, the slot is hidden.

**Who resolves it:** After the player picks a slot, the menu provider calls `ActivityRuntime.Instance.ResolvePlayerChoice(activity)`. This nulls `_activeChoicePhase` and calls `AdvancePhase(activity)` — advancing to the next phase or finishing the activity.

**Save-load restore:** On `OnGameLoaded`, `ActivityRuntime` re-resolves `_activeChoicePhase` by scanning the reloaded `_active` list for any activity whose current phase is `PhaseDelivery.PlayerChoice`. This means mid-choice saves reload with the menu slot-bank still visible.

**`FireIntervalHours` pacing for Auto phases:** `Activity` base now carries `LastAutoFireHour` (default `-1`). `TryFireAutoPhaseStorylet` checks: if `phase.FireIntervalHours > 0` AND `currentHour - a.LastAutoFireHour < phase.FireIntervalHours`, skip the tick. On fire, `LastAutoFireHour` is updated to `currentHour`. This is on the base class so all surface-spec activities inherit pacing without extra code.

---

### `ActivityContext.FromCurrent()`

**What it returns:** a fresh `ActivityContext` snapshot with `EnteredAt = CampaignTime.Now`; `TypeId`, `Intent`, and `Attendees` are left at their defaults. Callers populate `Intent` on the `Activity` itself (object-initializer before `Start(...)`), not on the context.

**Typical call pattern** (in a settlement-entry starter behavior):

```csharp
var activity = new HomeActivity { Intent = PickInitialIntent(lord, settlement) };
var ctx = ActivityContext.FromCurrent();
ActivityRuntime.Instance?.Start(activity, ctx);
```

For beat emissions (e.g., `OnSettlementLeftEvent`), reuse the factory:

```csharp
home.OnBeat("departure_imminent", ActivityContext.FromCurrent());
```

The factory is a single-responsibility helper — no lord lookup, no logging. Keeps starter behaviors free of boilerplate without introducing a subsystem.

---

## Pitfalls

1. **Writing a read-only quality** (`rank`, `days_in_rank`, `days_enlisted`) from a storylet effect — Phase 12 blocks. Route rank via the promotion chain.
2. **Inventing a scripted-effect id without adding it to `scripted_effects.json`** — Phase 12 blocks unknown `apply` values. Prefer reusing the seed catalog.
3. **Nesting scripted effects that cycle** — `EffectExecutor` caps at depth 8 and logs `scripted_depth_limit`. Flatten or split the catalog.
4. **Claiming a save-definer offset in 45–60 without grepping first** — reserved for surface-spec `Activity` subclasses. Silent save corruption on collision.
5. **Direct `EventDeliveryManager.QueueEvent`** — bypasses Director pacing. Use `StoryDirector.Instance?.EmitCandidate(...)`; see AGENTS.md Critical Rule #10.
6. **Mod-spawned heroes with `Occupation.Wanderer`** — triggers vanilla wanderer dialogue. Use `Occupation.Soldier` (AGENTS.md Pitfall #11).

---

## Related docs

- [Event Pacing (delivery layer, Spec above this one)](../../superpowers/specs/2026-04-18-event-pacing-design.md)
- [Writing Style Guide](writing-style-guide.md) — voice, tone, tooltip format
- [Error Code Registry](../../error-codes.md) — auto-generated
- [Content System Architecture (legacy, pre-Spec-0)](content-system-architecture.md) — scheduled for replacement by surface specs 1–5
