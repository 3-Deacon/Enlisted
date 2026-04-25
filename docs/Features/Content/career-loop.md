# Career Loop Closure — Reference

**Status:** Code-level complete; human smoke pending for T10, T17, and playtest Scenarios A-H. Day-to-day reference for the shipped Plan 5 career-loop surface: path scoring, T4/T6/T9 crossroads, committed-path gating, T7+ named-order variants, culture + lord-trait overlays, and debug hotkeys. Plan 5 Half A shipped on `development` 2026-04-22 (commits `4f66604` → `e35f445`, 10 commits). Half B authoring (overlays + debug hotkeys + verification doc) landed in the same window.

**For design rationale / problem framing:** see the plan at [`../../superpowers/plans/2026-04-21-career-loop-closure.md`](../../superpowers/plans/2026-04-21-career-loop-closure.md).

**For verification + closure notes:** see [`../../superpowers/plans/2026-04-21-career-loop-verification.md`](../../superpowers/plans/2026-04-21-career-loop-verification.md) and the human playtest runbook at [`../../superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md`](../../superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md).

The Career Loop surface is the terminal plan in the five-plan integration roadmap — it sits on top of the [storylet backbone](storylet-backbone.md) (Spec 0), Plan 1's [campaign intelligence snapshot](../../superpowers/plans/2026-04-21-campaign-intelligence-backbone.md), and Plan 4's [duty opportunities](../../superpowers/plans/2026-04-21-duty-opportunities.md) emitter. It answers: "over a full career, which path is this soldier walking, and does the content reflect the culture they serve under and the lord they follow?"

---

## What the career loop does

Five named paths — `ranger`, `enforcer`, `support`, `diplomat`, `rogue` — accumulate score from passive skill gains and explicit intent picks. At tiers 4, 6, and 9, the top-scoring path fires a Modal crossroads storylet with commit / resist options. Committing writes a permanent `committed_path_<path>` flag that unlocks T7+ named-order variants keyed on that path; resisting writes `path_resisted_<path>` and halves future score bumps on that path.

Orthogonal to paths, the enlisted lord's **culture** and **personality traits** overlay flavor onto authored storylets via the `__<culture>` suffix convention and `requires_lord_trait` / `excludes_lord_trait` lists (see [overlay rule](#culture--lord-trait-overlays) below).

No new save-definer offsets. Everything persists through existing `QualityStore` (`path_*_score`, `committed_path` indicator) and `FlagStore` (`committed_path_<path>`, `path_resisted_<path>`).

---

## Path scoring (`PathScorer`)

File: [`src/Features/Activities/Orders/PathScorer.cs`](../../../src/Features/Activities/Orders/PathScorer.cs). Canonical path-id helper: [`src/Features/Activities/Orders/PathIds.cs`](../../../src/Features/Activities/Orders/PathIds.cs).

```
CampaignEvents.HeroGainedSkill  (main hero only, positive delta)
  ↓ SkillToPath lookup           (Bow/Scouting/Riding → ranger, OneHanded/Polearm/… → enforcer, …)
  → BumpPath(path, change, "skill_level")

PathScorer.OnIntentPicked(intent)   (static, called by Evening menu + OrderActivity)
  ↓ IntentToPath lookup          (diligent → enforcer, train_hard → ranger, sociable → diplomat, scheme/slack → rogue)
  → BumpPath(path, 3, "intent_pick")

BumpPath:
  ↓ if FlagStore.Has("path_resisted_<path>"):  amount *= 0.5f
       → ModLogger.Expected("PATH", "bump_resisted_<path>", …)   (60s per-key throttle)
  → QualityStore.Set("path_<path>_score", clamp[0,100], reason)
```

**Skill → path map** (from `SkillToPath` dictionary):

| Path | Source skills |
| :--- | :--- |
| ranger | Bow, Scouting, Riding |
| enforcer | OneHanded, Polearm, Athletics, Leadership |
| support | Medicine, Engineering, Trade, Steward |
| diplomat | Charm |
| rogue | Roguery |

**Intent → path map:**

| Intent | Path |
| :--- | :--- |
| diligent | enforcer |
| train_hard | ranger |
| sociable | diplomat |
| scheme, slack | rogue |

Qualities are capped at 100 so extreme grinding on a single skill class saturates around mid-career. Intent picks are the only source that can swing a player into `support` or `diplomat` in practice (fewer skills map there).

**Session heartbeat** — on `OnSessionLaunchedEvent`, `PathScorer` logs one `PATH session_heartbeat: ranger=… enforcer=… support=… diplomat=… rogue=…` line as smoke evidence the behavior is registered.

---

## Crossroads firing (`PathCrossroadsBehavior`)

File: [`src/Features/CampaignIntelligence/Career/PathCrossroadsBehavior.cs`](../../../src/Features/CampaignIntelligence/Career/PathCrossroadsBehavior.cs).

```
EnlistmentBehavior.OnTierChanged  (static event, public static event Action<int,int>)
  ↓ filter: newTier ∈ {4, 6, 9}
  ↓ QualityStore.Instance present (else Expected:"crossroads_no_quality_store")
  ↓ pick highest path_<id>_score across PathIds.All (stable first-match on ties)
  ↓ pickedScore > 0 (else Expected:"crossroads_no_scored_path")
  ↓ at newTier < 7: not already committed to picked path (else Expected:"crossroads_already_committed")
  → FireCrossroadsStorylet(pickedPath, newTier, pickedScore)
       → StoryletCatalog.GetById("path_crossroads_<path>_t<tier>")
       → StoryletEventAdapter.BuildModal(storylet, ctx, null)
       → StoryDirector.EmitCandidate(Modal, ChainContinuation=true)
       → ModLogger.Info("PATH", "crossroads_emitted: …")
```

**Static-event subscription discipline:** `OnTierChanged` is re-bound with a defensive `-= / +=` pair on every `RegisterEvents()` call, so save/load cycles don't produce duplicate subscriptions.

**Tie-break** is stable first-match in `PathIds.All` order (`ranger, enforcer, support, diplomat, rogue`). When two paths tie at the highest score, the earlier-listed one wins.

**Debug force-fire.** `FireCrossroadsStorylet` is `public static` specifically so `CareerDebugHotkeysBehavior.ForceCrossroadsFire` (Ctrl+Shift+F) can bypass all guards and trigger a T4 crossroads against the top-scoring path — useful when the player hasn't naturally reached T4 yet. Fired crossroads go through the same `BuildModal` + `EmitCandidate` path as production; the only difference is the log line says `force_crossroads` and the tier gate is skipped.

---

## `commit_path` / `resist_path` effects

Added to `EffectExecutor.ApplyOne` in Plan 5 Phase A. These are internal-only primitives — the only legitimate authoring site is a `path_crossroads_<path>_t{4,6,9}` option `effects` entry.

```
commit_path { path: "<id>" }
  ↓ PathIds.Set contains pathId (else Expected:"commit_path_unknown")
  ↓ not already committed to pathId (else Expected:"commit_path_already_<id>", no-op)
  → FlagStore.Set("committed_path_<id>", CampaignTime.Never)
  → QualityStore.SetDirect("committed_path", 1, "commit_path primitive")   ← only legitimate SetDirect caller
  → ModLogger.Info("PATH", "committed path=<id>")

resist_path { path: "<id>" }
  ↓ PathIds.Set contains pathId (else Expected:"resist_path_unknown")
  → FlagStore.Set("path_resisted_<id>", CampaignTime.Never)
  → ModLogger.Info("PATH", "resisted path=<id>")
```

**Why `SetDirect` exists.** The `committed_path` quality is `writable: false` in [`quality_defs.json`](../../../ModuleData/Enlisted/Qualities/quality_defs.json) so a stray storylet `quality_set` can't forge a commit. `QualityStore.SetDirect` is a C#-only back door that bypasses `def.Writable`; `commit_path` is the sole caller. Authored content cannot reach `SetDirect` — `EffectExecutor` primitives are keyed by `apply` string and `quality_set` / `quality_add` still route through the normal `Add/Set` paths that check `def.Writable`. `validate_content.py` Phase 12 continues to reject authored writes to `writable: false` qualities.

**Commit is permanent.** Once `committed_path_<id>` is set, `BumpPath` keeps running normally (the resist halving is path-specific; commits don't stop scoring), but subsequent crossroads at higher tiers for the same path are the only re-entry point — and at T7+ the `already_committed` guard is deliberately bypassed so a T6-committed player still sees the T9 crossroads fire.

---

## T7+ named-order variants

Authored in [`ModuleData/Enlisted/Storylets/path_<path>_t7_variants.json`](../../../ModuleData/Enlisted/Storylets/) — 6 storylets per file × 5 paths = **30 storylets** total.

Each file ships variants of two base archetypes (`order_scout_<path>_t7`, `order_escort_<path>_t7`) across three rank bands (T7 / T8 / T9). Gating uses the pre-existing `flag:<name>` trigger against the permanent commit flag:

```json
"trigger": [
  "rank_gte:7",
  "flag:committed_path_ranger"
]
```

The `OrderActivity` named-order emitter picks these variants over the archetype-level `order_scout` / `order_escort` bases when the player is committed to the matching path and has reached the right rank. Every resolve option includes `clear_active_named_order` — miss this and the arc never closes, deferring the emitter indefinitely.

**Half A scope note.** Per the T9 scope relaxation (see plan `Appendix: Half A task history`), the Half A completeness rule is "each path has ≥1 T7+ variant" (enforced by `validate_content.py` Phase 15's `has_any_t7_variant` check), not "each of 10 archetypes × each of 5 paths = 50 storylets." Half B polish can expand the archetype matrix if desired without breaking Phase 15.

---

## Culture + lord-trait overlays

Covered in depth by [storylet-backbone.md → Plan 5 additions](storylet-backbone.md#plan-5-additions--career-path-primitives--overlay-preference); summary here focuses on the authored content Plan 5 ships.

### Culture overlays — `__<culture>` convention

File: [`ModuleData/Enlisted/Storylets/culture_overlays.json`](../../../ModuleData/Enlisted/Storylets/culture_overlays.json) — **45 overlays** covering 15 hot-path base storylets × 3 culture flavors each.

Overlays share a base id with a `__<culture>` suffix (case-insensitive on the `__` separator). The enforcement lives in `EnlistedDutyEmitterBehavior.PickEpisodicFromPool`:

```
eligible ← storylets whose IsEligibleForEmit passes
overlaidBaseIds ← { s.Id.Substring(0, idx) : idx = s.Id.IndexOf("__") > 0 }
if overlaidBaseIds not empty:
    eligible ← eligible where s.Id not in overlaidBaseIds   ← base siblings dropped
```

Example: pool contains base `duty_garrisoned_sword_form_1` + overlay `duty_garrisoned_sword_form_1__khuzait`. A Khuzait-lord player's `IsEligibleForEmit` passes the overlay (via `requires_culture: ["khuzait"]`) and the base. The overlaidBaseIds sweep drops the base, leaving only the overlay. A Vlandian-lord player fails the overlay's `RequiresCulture` check in `IsEligibleForEmit`; the base survives. A lord of a culture with no overlay for that base (e.g. Aserai with no `__aserai` sibling) reads the base.

**Culture StringIds are lowercase** (`empire`, `sturgia`, `battania`, `vlandia`, `khuzait`, `aserai`) — verified against `DefaultCultures.cs`. Mixed-case fails the `String.Equals(… OrdinalIgnoreCase)` inside the gate check.

### Lord-trait-gated storylets

File: [`ModuleData/Enlisted/Storylets/lord_trait_overlays.json`](../../../ModuleData/Enlisted/Storylets/lord_trait_overlays.json) — **15 standalone storylets** gated on `requires_lord_trait` / `excludes_lord_trait` lists.

These do NOT use the `__` suffix convention — they're authored as standalone ids (e.g. `duty_raiding_spare_village_trait_1`) that stand alongside the base pool and fire when the lord's trait level is positive for a listed trait. The gate calls `MBObjectManager.Instance.GetObject<TraitObject>(traitId)` + `lord.GetTraitLevel(trait) > 0`.

**Trait StringIds are PascalCase** — `Mercy`, `Valor`, `Honor`, `Generosity`, `Calculating`. Verified against `DefaultTraits.cs`.

### Overlay consumer scope

Only `EnlistedDutyEmitterBehavior.IsEligibleForEmit` (Plan 4) currently filters on `RequiresCulture` / `ExcludesCulture` / `RequiresLordTrait` / `ExcludesLordTrait`. Plan 3's `SignalBuilder` and Plan 1's ambient paths don't consult these fields. Adding a new consumer = adding the same filter logic; see the Plan 4 file for the canonical shape.

---

## Debug hotkeys (`CareerDebugHotkeysBehavior`)

File: [`src/Debugging/Behaviors/CareerDebugHotkeysBehavior.cs`](../../../src/Debugging/Behaviors/CareerDebugHotkeysBehavior.cs). Registered by `SubModule.OnGameStart` alongside the Home-focused `DebugHotkeysBehavior`.

| Binding | Dump / action | Log category |
| :--- | :--- | :--- |
| `Ctrl+Shift+I` | `EnlistedLordIntelligenceSnapshot` fields (posture, FrontPressure, ArmyStrain, SupplyPressure, RecentChanges) | `CAREER-DEBUG` |
| `Ctrl+Shift+A` | `OrderActivity.ActiveNamedOrder` state (storylet id, phase index, intent) | `CAREER-DEBUG` |
| `Ctrl+Shift+O` | All 5 `path_*_score` values + `committed_path` indicator + all `committed_path_<id>` / `path_resisted_<id>` flags | `CAREER-DEBUG` |
| `Ctrl+Shift+F` | Force-fire `PathCrossroadsBehavior.FireCrossroadsStorylet` at tier 4 for the top-scoring path (ranger if no path is scored) | `CAREER-DEBUG` + `PATH` |

**Letter-choice discipline.** These bindings avoid `H / E / B / Y` (claimed by the Spec 1 Home `DebugHotkeysBehavior`) and `E / P / Tab / R / X / K / M / T` (native TaleWorlds `DebugHotKeyCategory` — `T` was added to this exclusion list after an observed in-game overlay collision during Spec 1 Phase E). Future career-debug additions should grep `src/Debugging/Behaviors/` before claiming a letter.

**Tick-polled.** Registered on `CampaignEvents.TickEvent`; `Input.IsKeyDown` checks Ctrl+Shift before `Input.IsKeyPressed` checks the letter. No persisted state — `SyncData` is a stub.

---

## Storylet corpus

All files under [`ModuleData/Enlisted/Storylets/`](../../../ModuleData/Enlisted/Storylets/). Loaded by `StoryletCatalog.LoadAll` (non-recursive — files must live at the top level).

| File | Count | Delivery | Shape |
| :--- | :---: | :--- | :--- |
| `path_crossroads.json` | 15 | Modal, player-choice | 5 paths × T4/T6/T9 milestones; each has commit / resist options |
| `path_ranger_t7_variants.json` | 6 | mixed (scout + escort) | T7+ named-order variants gated on `committed_path_ranger` |
| `path_enforcer_t7_variants.json` | 6 | mixed | Gated on `committed_path_enforcer` |
| `path_support_t7_variants.json` | 6 | mixed | Gated on `committed_path_support` |
| `path_diplomat_t7_variants.json` | 6 | mixed | Gated on `committed_path_diplomat` |
| `path_rogue_t7_variants.json` | 6 | mixed | Gated on `committed_path_rogue` |
| `culture_overlays.json` | 45 | auto / episodic | 15 hot-path bases × 3 culture overlays each (`__<culture>` suffix) |
| `lord_trait_overlays.json` | 15 | auto / episodic | Standalone ids gated on `requires_lord_trait` / `excludes_lord_trait` |

**Plan 5 total authored corpus: 105 storylets.** (Plan 5 authors no new activity JSON — crossroads fire as one-shot Modals outside any activity phase.)

---

## Integration points (plugs into storylet backbone + plans 1-4)

| Piece | How the career loop uses it |
| :--- | :--- |
| `StoryletCatalog` | Loads `path_crossroads.json`, 5× `path_<path>_t7_variants.json`, `culture_overlays.json`, `lord_trait_overlays.json` |
| `EffectExecutor` | Added two primitives: `commit_path`, `resist_path` |
| `QualityStore` | Added `SetDirect` back-door; `committed_path` indicator authored `writable: false` |
| `FlagStore` | Permanent flags: `committed_path_<path>` ×5, `path_resisted_<path>` ×5 |
| `EnlistmentBehavior` | Subscribes static `OnTierChanged(int oldTier, int newTier)` (Plan 5 does not add the event — existed pre-plan) |
| `StoryletEventAdapter.BuildModal` | Wraps a chosen crossroads storylet into an `InteractiveEvent` |
| `StoryDirector.EmitCandidate` | Routes crossroads as a Modal with `ChainContinuation=true` (bypasses in-game floor + category cooldown) |
| `EnlistedDutyEmitterBehavior.IsEligibleForEmit` | Enforces `RequiresCulture` / `ExcludesCulture` / `RequiresLordTrait` / `ExcludesLordTrait` on all duty-emitted storylets |
| `EnlistedDutyEmitterBehavior.PickEpisodicFromPool` | Drops base storylets when `__<culture>` overlay siblings are eligible |
| `validate_content.py` Phase 15 | Full enforcement: 15-combo `path_crossroads_*_t{4,6,9}` assertion + per-path `has_any_t7_variant` rule keyed on `flag:committed_path_<path>` |

---

## File map

Core C# (new files in **bold**):

| File | Purpose |
| :--- | :--- |
| **`src/Features/CampaignIntelligence/Career/PathCrossroadsBehavior.cs`** | Fires crossroads on `EnlistmentBehavior.OnTierChanged` |
| **`src/Features/Activities/Orders/PathIds.cs`** | Canonical 5-path id helper — `All` (ordered list) + `Set` (OrdinalIgnoreCase) |
| **`src/Debugging/Behaviors/CareerDebugHotkeysBehavior.cs`** | Ctrl+Shift+{I,A,O,F} inspection + force-fire hotkeys |
| `src/Features/Activities/Orders/PathScorer.cs` | Extended `BumpPath` with resist-bias 0.5× halving + throttled log |
| `src/Features/Content/EffectExecutor.cs` | Added `commit_path` / `resist_path` cases to `ApplyOne` switch |
| `src/Features/Qualities/QualityStore.cs` | Added `SetDirect` back-door (bypasses `def.Writable`) |
| `src/Features/CampaignIntelligence/Duty/EnlistedDutyEmitterBehavior.cs` | Overlay gating + culture/trait filters in `IsEligibleForEmit` + `PickEpisodicFromPool` |

Content (all under `ModuleData/Enlisted/`):

| File | Purpose |
| :--- | :--- |
| `Qualities/quality_defs.json` | Added `committed_path` indicator (`max: 1`, `writable: false`) |
| `Storylets/path_crossroads.json` | 15 crossroads storylets (5 paths × T4/T6/T9) |
| `Storylets/path_<path>_t7_variants.json` | 6 storylets each × 5 files = 30 T7+ named-order variants |
| `Storylets/culture_overlays.json` | 45 culture-flavored overlays (15 bases × 3 cultures) |
| `Storylets/lord_trait_overlays.json` | 15 trait-gated standalone storylets |

Validator:

| File | Phase | Purpose |
| :--- | :--- | :--- |
| `Tools/Validation/validate_content.py` | Phase 15 | Path-crossroads completeness (full enforcement; rewrote from stub) |

Save registration: **no new class or enum offsets.** The career loop persists through pre-existing `QualityStore` + `FlagStore` offsets (40, 43) plus the `committed_path` and `path_*_score` entries in `quality_defs.json`.

---

## Gotchas

1. **`EnlistmentBehavior.OnTierChanged` is a static event**, not an instance event. `EnlistmentBehavior.Instance?.OnTierChanged` doesn't compile — the plan doc had this wrong initially. Subscribe with `EnlistmentBehavior.OnTierChanged -= / +=`.
2. **`DefaultTraits.X` is null at `OnGameStart`** (same as Home Surface). `IsEligibleForEmit`'s trait check uses `MBObjectManager.Instance.GetObject<TraitObject>(id)` at tick time, not eager registration, so it's safe.
3. **Attribute StringIds are lowercase** (`vigor`, `cunning`, …) per `DefaultCharacterAttributes.cs:45-50`. Culture StringIds are **lowercase**; trait StringIds are **PascalCase**. Mixing casing in overlay JSON silently fails the case-sensitive comparisons at different gate sites.
4. **T7+ variants must include `clear_active_named_order`** in every resolve option. The `order_scout.json` precedent is load-bearing — miss it and the named-order arc never closes, which blocks the emitter from proposing the next arc.
5. **`committed_path` quality is `writable: false`** — never attempt `quality_set: committed_path` from a storylet effect. `validate_content.py` Phase 12 rejects it; the runtime refuses via `def.Writable`. Only `commit_path` primitive (via `SetDirect`) can write it.
6. **Crossroads at T7+ intentionally bypass the `already_committed` guard** (`PathCrossroadsBehavior.cs:68`). A T4-committed player still sees T6 and T9 fire — the commit is "locked path" not "done with crossroads."
7. **`Enlisted.csproj` wildcards are non-recursive.** `CareerDebugHotkeysBehavior.cs` lives at `src/Debugging/Behaviors/` and needs an explicit `<Compile Include>` — the build may pass locally without it (other behaviors already include the file via reference), but a clean build will miss it. Grep the csproj for the exact directory before adding a new file.
8. **`path_<path>_score` qualities are clamped to [0, 100].** Long careers plateau; the crossroads picker is robust to ties via stable first-match in `PathIds.All` order.

---

## Verification surface

- `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` must pass.
- `python Tools/Validation/validate_content.py` must pass — Phase 15 asserts the 15 `path_crossroads_*_t{4,6,9}` ids and per-path `has_any_t7_variant`.
- In-game smoke — see [`../../superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md`](../../superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md) (Scenario G covers the T4/T6/T9 + resist full-career arc; Scenario F covers cross-culture overlay firing).
- Log markers to expect: `PATH session_heartbeat`, `PATH crossroads_emitted`, `PATH committed path=<id>`, `PATH resisted path=<id>`, `Expected("PATH", "bump_resisted_<path>", …)`, `CAREER-DEBUG` dumps from the hotkeys.
- Zero `Surfaced` calls during nominal play.

## Known deferred scope

- T7+ variants currently cover scout + escort across five paths; the remaining named-order archetypes are polish.
- T7 entry options cover the minimum viable intent set; additional intent variants are polish.
- Culture overlays currently cover 15 hot-path bases across three cultures; broader culture coverage is polish.
- Signal emitter does not apply culture/trait gates. Only the duty emitter consumes those fields today.
- Phase 15 enforces the 15 crossroads ids plus at least one T7+ variant per path, not the full per-archetype matrix.
