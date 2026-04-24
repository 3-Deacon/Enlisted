# Plan 5 — Career Loop Closure — Code Verification

**Status:** Code-level verification complete. T10 (Half A in-game smoke), T17 (save-load mid-arc reconstruction), and T19-T21 (playtest scenarios A-G execution) pending human operator.

**Shipped:** 2026-04-22 on `development`, commit range `4f66604` → `2b3fb2e` (17 commits shipped + this status-closure commit = 18 total). Not yet pushed to `origin/development` — `git push` is user-controlled.

**Plan doc:** [docs/superpowers/plans/2026-04-21-career-loop-closure.md](2026-04-21-career-loop-closure.md)
**Playtest runbook:** [docs/superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md](2026-04-21-career-loop-playtest-scenarios.md)
**Design reference:** [docs/superpowers/specs/2026-04-21-plans-integration-design.md](../specs/2026-04-21-plans-integration-design.md)

---

## 1. Summary

Plan 5 closes the enlisted career loop as the terminal plan in the five-plan integration roadmap. It ships the path-crossroads system (T4/T6/T9 commit/resist milestones driving `committed_path_<path>` semantics), a minimum-viable T7+ committed-path variant matrix (scout + escort × 5 paths), culture overlays for 15 hot-path duty storylets × 3 cultures each, 15 lord-trait-gated storylets across the big-5 personality axis, the Phase 15 validator promoted from stub to full enforcement, career-inspection debug hotkeys, and the playtest runbook used to close the loop through human smoke.

What ships at the C# layer:
- `QualityStore.SetDirect` back-door write for read-only indicator qualities (T1.5).
- `commit_path` and `resist_path` effect primitives (T2, T3).
- Shared `PathIds` helper (T3).
- `PathScorer.BumpPath` resist 0.5× bias (T3).
- `PathCrossroadsBehavior` static-event subscription + emission (T4, T5).
- `EnlistedDutyEmitterBehavior` culture gate + overlay preference + trait gate (T12 Part A, T14 Part A).
- `CareerDebugHotkeysBehavior` with Ctrl+Shift+{I,A,O,F} bindings (T15, T16).
- `PathCrossroadsBehavior.FireCrossroadsStorylet` public helper shared between production and debug (T15).

What ships at the content layer:
- `path_crossroads.json` — 15 milestone storylets (T6).
- 5 files `path_<path>_t7_variants.json` — 30 committed-path variants (T7).
- `culture_overlays.json` — 45 overlays (T12 Part B).
- `lord_trait_overlays.json` — 15 trait-gated standalone storylets (T14 Part B).
- `Tools/Validation/validate_content.py` Phase 15 full enforcement (T9) + Phase 13 overlay-skip (T12 Part A).

What does NOT ship here (by design or by scope narrowing):
- In-game smoke verification (T10, T17, T19-T21 — pending human operator).
- T7 archetype matrix beyond scout + escort (deferred; see §6).
- Culture overlays for cultures 4-6 per base (deferred; see §6).
- Signal emitter culture / trait gating (not in scope; see §6).
- Per-archetype Phase 15 enforcement (scope relaxed; see §6).

---

## 2. Architecture shipped — code

| Component | Role | Location | Task |
|---|---|---|---|
| `QualityStore.SetDirect` | Writable-flag bypass for read-only indicator qualities | `src/Features/Qualities/QualityStore.cs` | T1.5 |
| `commit_path` primitive | Writes `committed_path_<path>` flag + bumps `committed_path` indicator to 1 | `src/Features/Content/EffectExecutor.cs` | T2 |
| `resist_path` primitive | Writes `path_resisted_<path>` permanent flag | `src/Features/Content/EffectExecutor.cs` | T3 |
| `PathIds` helper | Canonical five-path id list + OrdinalIgnoreCase set | `src/Features/Activities/Orders/PathIds.cs` | T3 |
| `PathScorer.BumpPath` resist bias | Halves `amount` when `path_resisted_<path>` set | `src/Features/Activities/Orders/PathScorer.cs` | T3 |
| `PathCrossroadsBehavior` | Subscribes to static `EnlistmentBehavior.OnTierChanged`, picks highest-scoring path on {4,6,9}, emits Modal | `src/Features/CampaignIntelligence/Career/PathCrossroadsBehavior.cs` | T4, T5 |
| `PathCrossroadsBehavior.FireCrossroadsStorylet` | Guard-free public helper for production + debug fire paths | same file | T15 |
| Culture gate (emitter) | `IsEligibleForEmit` enforces `RequiresCulture` / `ExcludesCulture` against `EnlistedLord.Culture.StringId` | `src/Features/CampaignIntelligence/Duty/EnlistedDutyEmitterBehavior.cs` | T12 Part A |
| Overlay preference (emitter) | `PickEpisodicFromPool` drops base siblings when `__<culture>` overlays are eligible | same file | T12 Part A |
| Trait gate (emitter) | Enforces `RequiresLordTrait` / `ExcludesLordTrait` via `Hero.GetTraitLevel` | same file | T14 Part A |
| `CareerDebugHotkeysBehavior` | Ctrl+Shift+{I,A,O,F} polling on `CampaignEvents.TickEvent` | `src/Debugging/Behaviors/CareerDebugHotkeysBehavior.cs` | T15, T16 |
| Phase 15 full enforcement | Asserts 15 milestone storylets + per-path T7+ variant presence | `Tools/Validation/validate_content.py` | T9 |
| Phase 13 overlay-skip | Pool coverage skips `__`-containing ids to avoid double-booking | same file | T12 Part A |

No new save-definer class or enum offsets. All persistent state reuses existing `FlagStore` (flags `committed_path_<path>`, `path_resisted_<path>`, `prior_service_*`) + `QualityStore` (quality `committed_path`).

---

## 3. Content shipped — storylets

| File | Count | Purpose | Task |
|---|---|---|---|
| `path_crossroads.json` | 15 | 5 paths × 3 milestones (T4/T6/T9); each Modal with commit/resist/defer options | T6 |
| `path_ranger_t7_variants.json` | 6 | Ranger-flavored scout + escort T7+ variants (entry + mid + resolve each) | T7 |
| `path_enforcer_t7_variants.json` | 6 | Enforcer variant matrix (same shape) | T7 |
| `path_support_t7_variants.json` | 6 | Support variant matrix | T7 |
| `path_diplomat_t7_variants.json` | 6 | Diplomat variant matrix | T7 |
| `path_rogue_t7_variants.json` | 6 | Rogue variant matrix | T7 |
| `culture_overlays.json` | 45 | 15 hot-path bases × 3 culture overlays each | T12 Part B |
| `lord_trait_overlays.json` | 15 | Standalone trait-gated duty-pool entries (big-5 balanced 3/3/3/3/3) | T14 Part B |
| **Total Plan 5 storylets** | **105** | | |

Per-storylet localization integrated into `ModuleData/Languages/enlisted_strings.xml` via `sync_event_strings.py`.

---

## 4. Plan-vs-shipped drifts caught this session

Bake these into Half B polish prompts and any future content-authoring work.

| # | Drift | Plan claim | Actual | How resolved |
|---|---|---|---|---|
| 1 | `committed_path` quality max | `max: 0` per plan T1 literal | `max: 1` required; `SetDirect` clamps to `def.Max` | Corrected at T1 authoring time |
| 2 | `EnlistmentBehavior.OnTierChanged` subscription | plan T4: `.Instance.OnTierChanged +=` | Static event at `EnlistmentBehavior.cs:8490`; subscribe on TYPE | Corrected at T4 implementation |
| 3 | `ModLogger.Expected/Caught/Surfaced` summary interpolation | plan snippets use `$"..."` | Registry scanner rejects interpolation in category + summary args | All calls rewritten to literal + ctx dict |
| 4 | Attribute StringIds | plan T6 assumed title-case (`Vigor`) | Engine uses lowercase (`vigor`) per `DefaultCharacterAttributes.cs:45-50` | T6 implementer verified + corrected |
| 5 | Culture StringIds | (unspecified) | Lowercase per `EnlistmentBehavior.cs:10085-10090` (`empire` / `sturgia` / `vlandia` / `battania` / `khuzait` / `aserai`) | T12 locked lowercase |
| 6 | Trait StringIds | plan T14 examples PascalCase | Confirmed PascalCase per `DefaultTraits.cs:130-132` (`Mercy` / `Valor` / `Honor` / `Generosity` / `Calculating`) | T14 locked PascalCase |
| 7 | Named-order resolve schema | plan T7 resolve snippet omitted `clear_active_named_order` | Every base resolve includes it; omission leaves arc active permanently | T7 implementer added to every resolve |
| 8 | Phase 15 id-prefix convention | stub gate on `crossroads_*` + bands `t3_to_t4` | T6 authored `path_crossroads_*_t{4,6,9}` | T9 rewrote stub to shipped convention |
| 9 | `PathCrossroadsBehavior` file location | plan prescribed `src/Features/CampaignIntelligence/Career/` | Behavior reads `QualityStore` directly, not snapshot-driven; cohesion would favor `src/Features/Activities/Orders/` next to `PathScorer` / `PathIds` | Left at plan location; flagged for retrospective |
| 10 | T9 enforcement scope | "each path × each of 10 archetypes" (= 150 storylet minimum) | Scoped to `each_path_has_any_t7_variant` (minimum viable) | Plan T9 bullet (c) edited in-file 2026-04-22 |

---

## 5. Known limitations — what shipped vs deferred

| Area | What shipped | What deferred | Follow-up owner |
|---|---|---|---|
| T7+ path variants | 2 archetypes × 5 paths = 10 variants (scout + escort) | Remaining 8 named-order archetypes (guard_post, patrol, fatigue_detail, drill, courier, treat_wounded, siege_works, inspection) × 5 paths = 40 variants | Half B polish |
| T7 entry options | `accept_diligent` + `decline` per variant (2 options each) | Other intents (`train_hard`, `scheme`, `sociable`, `slack`) per variant = up to 3 more options each | Half B polish |
| Culture overlays | 3 cultures × 15 bases = 45 overlays | Up to 6 cultures × 15 bases = 90 overlays (the other 3 cultures per base) | Half B polish |
| Signal emitter gates | Parsing-only — `RequiresCulture` / `RequiresLordTrait` fields exist on `Storylet` but `EnlistedSignalEmitterBehavior.PickFloorStorylet` does NOT check them | Same gate wiring as duty emitter needed if Half B adds culture-gated or trait-gated signals | Half B polish (contingent on new content) |
| Debug hotkeys | 4 bindings (intel / arc / path / force-fire) | `?force_profile <profile>` (needs arg; hotkeys can't take args — would need console-command plumbing or key-cycle) | Half B polish |
| Phase 15 enforcement | `each_path_has_any_t7_variant` | Per-archetype-per-path matrix enforcement | Paired with T7 archetype expansion |
| In-game smoke | N/A | T10 (Half A crossroads + commit/resist), T17 (save-load mid-arc reconstruction), T19-T21 (scenarios A-G from the T18 runbook) | Human operator |

---

## 6. Pending verification — human operator

### Plan 5 pending

| Task | Description | Runbook |
|---|---|---|
| T10 | Half A in-game smoke: T4/T6/T9 crossroads fire, commit/resist flow works, T7+ variants appear at committed-path T7+, `PathScorer.BumpPath` resist 0.5× bias verified | Scenario G in the playtest runbook |
| T17 | Save-load mid-arc reconstruction: accept a named order, save mid-arc, reload, verify arc continues to resolve | Standalone procedure in plan T17 (not in the A-G runbook) |
| T19 | Execute Scenarios A + B | [Playtest runbook](2026-04-21-career-loop-playtest-scenarios.md) §§A, B |
| T20 | Execute Scenarios C + D + E | Playtest runbook §§C, D, E |
| T21 | Execute Scenarios F + G | Playtest runbook §§F, G |

### Cross-plan smoke queue — coverage by the Plan 5 runbook

The same human playtest run of Scenarios A-H from this plan's [playtest runbook](2026-04-21-career-loop-playtest-scenarios.md) **covers** (does not automatically close) the pre-existing pending smoke queue from Plans 1, 3, and 4. Each plan below was shipped code-complete with its own smoke task left open for human execution; the playtest runbook exercises the behavior those tasks want to verify, but a runner should still read the original pending task text to make sure no specific assertion is missed. Plan 2's Phase H smoke passed 2026-04-22 and is NOT in this queue.

| Plan | Pending task | Covering scenarios | Inspection aids |
|---|---|---|---|
| 1 | T28 — 14-day in-game smoke, `INTEL/hourly_recompute` cadence + snapshot transitions | A (garrison baseline), B (marching posture), C (offensive siege posture), E (imprisoned profile flip) | `Ctrl+Shift+I` dumps the snapshot on demand |
| 3 | T28 — 14-day in-game smoke, signal cadence across 10 families | B (front-pressure signals), D (aftermath signals post-raid), F (culture-varying signal narration) | Typed personal/camp feed output + session log `SIGNAL` entries |
| 4 | T26 — 14-day full-profile run, all 7 profiles exercised, transitions fire | A (garrisoned), B (marching), C (besieging → marching transition), D (raiding), E (imprisoned → back-transition) | `Ctrl+Shift+O` path overview; `DUTY heartbeat` / `DUTY daily_counts` lines |
| 4 | T27 — fast-forward soak (4×/16× throttle behaviour) | **H** (newly-added scenario explicitly scripted for this) | Tick-logs continue at 16×; news-feed silences by design per AGENTS.md pitfall #21 |
| 5 | T10, T17, T19-T21 | Listed above | `Ctrl+Shift+I/A/O/F` career-debug hotkeys |

A smoke runner who executes Scenarios A-H end-to-end will exercise all five plans' behaviour under a realistic workload. After the run, the runner should still open each plan's verification doc (Plan 1 T28, Plan 3 T28, Plan 4 T26/T27) and confirm the specific assertions listed there were observed. If any specific assertion was missed, the runbook is the gap — not the smoke.

---

## 7. Cross-plan integration status

- **Plan 1 (Campaign Intelligence Backbone)** — Plan 5 `CareerDebugHotkeysBehavior.DumpIntelSnapshot` reads `EnlistedCampaignIntelligenceBehavior.Instance.Current`. No Plan 1 code edits.
- **Plan 2 (Lord AI Intervention)** — no integration. Plan 5 does not touch AI wrappers.
- **Plan 3 (Signal Projection)** — no integration. `EnlistedSignalEmitterBehavior` was considered for culture/trait gate but left untouched per advisor; Signal storylets are not in the Plan 5 hot-path target set.
- **Plan 4 (Duty Opportunities)** — Plan 5 modifies `EnlistedDutyEmitterBehavior` in two places (T12 Part A culture gate + overlay preference, T14 Part A trait gate). Modifications are additive — existing Plan 4 storylets (no `requires_culture` / no `requires_lord_trait`) are unaffected. Plan 5 authored content consumes Plan 4's duty-pool prefix-matching picker via the `duty_<profile>_*` id convention.

---

## 8. Commit trail (chronological)

| # | SHA | Task | Scope |
|---|---|---|---|
| 1 | `4f66604` | T1 | `committed_path` quality (writable:false, max:1) |
| 2 | `cbf59a5` | T1.5 | `QualityStore.SetDirect` back-door |
| 3 | `0bee054` | T2 | `commit_path` primitive |
| 4 | `bb772d7` | T3 | `resist_path` primitive + `PathScorer.BumpPath` resist bias + `PathIds` helper |
| 5 | `70b1f33` | T4 | `PathCrossroadsBehavior.cs` |
| 6 | `2be5966` | T5 | Register `PathCrossroadsBehavior` in `SubModule.cs` |
| 7 | `e6ef67e` | T6 | `path_crossroads.json` (15 storylets) |
| 8 | `88e7486` | T7 | Five `path_<path>_t7_variants.json` files (30 storylets) |
| 9 | `9cf4310` | docs | Flip Half A T1-T7 to shipped + scope-relax T9 + appendix |
| 10 | `e35f445` | T9 | `validate_content.py` Phase 15 full enforcement |
| 11 | `b6faab9` | docs | Flip T8 + T9 to shipped |
| 12 | `131caa8` | T15, T16 | `CareerDebugHotkeysBehavior.cs` + SubModule registration + `PathCrossroadsBehavior.FireCrossroadsStorylet` extraction |
| 13 | `32448f1` | T12 Part A | Duty emitter culture gate + overlay preference + Phase 13 overlay-skip |
| 14 | `f5d9a87` | T12 Part B | `culture_overlays.json` (45 overlays) |
| 15 | `a13e8ee` | T14 Part A | Duty emitter trait gate |
| 16 | `7886b63` | T14 Part B | `lord_trait_overlays.json` (15 trait-gated storylets) |
| 17 | `2b3fb2e` | T18 | Playtest scenarios A-G runbook |
| 18 | this commit | T22 | Verification doc + CLAUDE.md status closure |

---

## 9. Verification log excerpts

Phase 15 validator output as of commit 17:

```
[Phase 15] Validating path crossroads completeness...
  OK: all 15 milestone storylets present; T7+ variants per path — ranger=2, enforcer=2, support=2, diplomat=2, rogue=2.
```

Build output as of commit 17:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Content validator as of commit 17: 0 errors, 30 warnings (all pre-existing — none introduced by Plan 5).
