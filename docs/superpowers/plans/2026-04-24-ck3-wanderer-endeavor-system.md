# Plan 5 — CK3 Wanderer Mechanics: Endeavor System

**Status:** Draft v1 (2026-04-24). Fifth of seven plans implementing the [CK3 Wanderer Mechanics Systems Analysis (v6)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md). See spec §8 for the full plan structure.

**Scope:** Text-based, multi-phase, archetype-themed activities the player initiates from a new Camp sub-menu. Five categories (Soldier / Rogue / Medical / Scouting / Social), each with hybrid skill-OR-companion gating per spec §3.8. ~30 endeavor templates with ~60 phase storylets. `EndeavorActivity` + `ContractActivity` siblings on the existing Activity backbone. `EndeavorPhaseProvider` modeled literally on `HomeEveningMenuProvider`.

**Estimated tasks:** 30 (largest plan in the spec). **Estimated effort:** 5-7 days with AI-driven implementation.

**Dependencies:** Plans 1-3 are required (substrate + companions + ceremony flags). Plan 4 is referenced for officer-tier flavor only — see Lock 6 below; Plan 5 has NO hard cross-branch dependency on Plan 4 and can begin on `development` independently of when Plan 4 merges.

---

## 📍 EXECUTION PROGRESS — 2026-04-26 (HAND-OFF — partial)

**Status:** 🟡 **13 of 30 tasks complete (T1-T13 + T28). Phase A + B shipped on `feature/plan5-endeavor-system` worktree. Content authoring (Phase C-E) + integration + smoke (Phase F) pending.** Next operator should read the verification doc first: [Plan 5 Verification — PARTIAL](2026-04-24-ck3-wanderer-endeavor-system-verification.md). It contains the full hand-off record including the resume runbook (§4), what shipped (§1), what's pending (§3), and the smoke-scenario seeds (§4).

**Branch:** `feature/plan5-endeavor-system` (worktree at `.worktrees/plan5-endeavor-system/`).
**Commits on the branch (newest first):**
- `4e854b4` — `feat(endeavors): Plan 5 Phase B — C# wiring stack + Camp menu slot 5` (T7-T13)
- `81fa6de` — `feat(endeavors): Plan 5 Phase A — substrate + effects + Phase 19 validator` (T1-T6 + T28)

**Verification gates as of 2026-04-26:**
- ✅ `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` — clean (0 warnings, 0 errors).
- ✅ `python Tools/Validation/validate_content.py` — passes; Phase 19 emits 2 expected warnings (endeavor_catalog.json + contract_archetypes.json absent — intentional, T14/T24 ship them).
- ✅ Error-codes registry regenerated (131 Surfaced calls across 30 categories).
- ⏳ In-game smoke pending — runbook seed in verification doc §4.

**Pending phases:**
- **Phase C — Endeavor catalog data (T14-T18, ~30 templates).** First task ALSO wires `Endeavors` data dir in `Enlisted.csproj` (3 lines per CLAUDE.md project conventions).
- **Phase D — Endeavor storylets (T19-T23, ~50 storylets).** T19 Soldier in main thread as tone exemplar; T20-T23 dispatched to parallel subagents per Lock 10.
- **Phase E — Contracts (T24-T25).** ~10 templates + ~15 storylets.
- **Phase F — Integration + smoke (T27 + T29-T30).** Cross-system flag wiring + smoke runbook + finalize verification doc. **T26 deferred to Plan 7 polish** per stretch flag in §6.

**Resume**: `cd` into the worktree, follow the verification doc §4 runbook.

---

## 🔒 LOCKED 2026-04-26 — readiness amendments (pre-execution)

This block consolidates the pre-execution readiness audit (Plan 5 was authored 2026-04-24 before Plans 2-4 shipped — drift accumulated). **Locks override the body of the plan where they conflict.** Same pattern as Plans 3 and 4. No game-design locks — Plan 5's design is already pinned by §4.1-§4.8 and the user has not asked for design rethinks.

### Audit-correction locks (1-5)

**Lock 1 — `IsOfficer()` helper does NOT ship from Plan 4 (BLOCKING for §0 ref 19 + officer-flavor task hooks).** §0 ref 19 reads "Plan 4 verification — `IsOfficer()` helper available; officer-tier endeavor flavor possible." Verified on the `feature/plan4-officer-trajectory` branch: no public or private `IsOfficer()` method exists. The only `IsOfficer` references in the codebase are (a) `EnlistedDialogManager.cs:1720` setting `IsOfficer = playerTier >= 7` on a `QMDialogueContext` DTO, (b) `QuartermasterManager.IsOfficerExclusive` (boolean flag on a different concept, equipment exclusivity). Plan 4 inline-checks `playerTier >= 7` at every dialog hook site.

**Fix:** Plan 5 uses inline `EnlistmentBehavior.Instance?.EnlistmentTier >= 7` at every officer-flavor call site (mirrors Plan 4's idiom; keeps cross-feature coupling minimal). Alternative — extract a single helper into `src/Features/Officer/OfficerHelpers.cs` — is rejected for v1 because the check is one line and lives at <10 sites total. Update §0 ref 19 to read "Plan 4 ships officer-tier dialog flavor via inline `EnlistmentTier >= 7` checks; Plan 5 uses the same idiom for officer-tier endeavor flavor."

**Lock 2 — Ceremony flag schema is `ceremony_choice_t<N>_<id>` bool, NOT `ceremony.t<N>.choice` string (BLOCKING for §0 ref 18 + lines 305 + 435 + 436).** Plan 5 §0 ref 18 reads "choice-memory `FlagStore` keys (`ceremony.t{N}.choice`) available". Wrong on two counts:
1. Notation is flat underscore (architecture brief §4 rule 6 + Plan 4 Lock 2): `ceremony_choice_t<N>_<id>`, never dotted.
2. `FlagStore` is bool-only (Plan 4 Lock 2 verified `src/Features/Flags/FlagStore.cs:44-106` — `Has`/`Set`/`Clear` only, no `GetString`).

Plan 3 ships one bool flag per option pick + a dedup `ceremony_fired_t<N>` per fired tier. Verified option IDs from `ModuleData/Enlisted/Storylets/ceremony_t2_to_t3.json`: `frugal`, `generous`, `family`, `hedonist`. T4→T5 + T7→T8 option IDs must be grepped from their respective ceremony JSON at task time per AGENTS.md pitfall #22 — the option IDs are short stable identifiers, not random GUIDs.

**Fixes (line-by-line):**
- §0 ref 18: replace `ceremony.t{N}.choice` with `ceremony_choice_t<N>_<id>` and add "(bool flag per pick; one row per option ID)".
- Line 305 (`endeavor_set_choice_flag` description): replace `set ceremony.endeavor.<id>.<phase>.<choice>` with `set endeavor_choice_<endeavor_id>_<phase>_<option_id> bool`. Same flat-underscore convention; lives in `FlagStore` global namespace alongside ceremony flags.
- Line 435 (`endeavor.rogue.dice_game` gating example): replace `requires ceremony.t2.choice == "frugal"` with `requires FlagStore.Instance.Has("ceremony_choice_t2_frugal")`.
- Line 436 (`endeavor.medical.poultice_recipes` flavor example): replace `flavored by ceremony.t4.choice` with `flavored by ceremony_choice_t4_<id>` and add author-time note: implementer must grep `ModuleData/Enlisted/Storylets/ceremony_t4_to_t5.json` for actual option IDs (likely 3-4 IDs in the same shape as T2's `frugal`/`generous`/`family`/`hedonist`).

**Lock 3 — `ActivityRuntime.cs` line ref drift (advisory, NOT BLOCKING).** §0 ref 13 says `src/Features/Content/ActivityRuntime.cs:267`. Two issues:
1. The file lives at `src/Features/Activities/ActivityRuntime.cs`, not `src/Features/Content/ActivityRuntime.cs` (Plan 1 rehoming).
2. Line 267 is wrong. Actual `storylet.ToCandidate(ctx)` call sites: **line 220** (`activity.ToCandidate(ctx)` inside `EmitForActivePhases` loop, gated by `ChainContinuation = true` at line 223) and **line 338** (`s.ToCandidate(ctx)` inside the auto-emit pool path).

Plan 5's narrative claim ("auto-emits phase storylets via `storylet.ToCandidate(ctx)`. Plan 5 does NOT use this path for player-choice modals; uses `ModalEventBuilder.FireEndeavorPhase` instead.") is correct — only the line ref is stale. Implementer must grep `ToCandidate(ctx)` per AGENTS.md pitfall #22. **Fix §0 ref 13 path + line:** `src/Features/Activities/ActivityRuntime.cs:220 + 338`.

**Lock 4 — `EnlistedMenuBehavior` Camp menu line range drift (advisory, BLOCKING for §3 file change list + T7-T8 task).** §0 ref 18 + §3 file change list both reference `EnlistedMenuBehavior.cs:1345-1482`. Actual Camp menu range on `development`: lines **1290-1417** (extends through `camp_hub_back` at line 1417). Camp menu slot indices verified:

| Slot | Option | Source |
| :-- | :-- | :-- |
| 1 | `camp_hub_service_records` | pre-existing |
| 2 | `camp_hub_companions` | pre-existing |
| 3 | `camp_hub_retinue` (T7+) | pre-existing |
| 4 | `camp_hub_quartermaster` | pre-existing |
| **5** | **FREE** | **Plan 5 claims** |
| 6 | `camp_hub_talk_to_companion` | Plan 2 |
| 7 | `camp_hub_talk_to_lord` | pre-existing |
| 8 | `camp_hub_inspect_officer_tent` | Plan 4 (`feature/plan4-officer-trajectory`, T7+ visible) |
| 100 | `camp_hub_back` | pre-existing |

Slot 5 is free for Plan 5's "Endeavors" sub-menu entry both before and after Plan 4 merges to development. **Fixes:** §3 file change list updates `1345-1482` to `1290-1417` (or simply removes the line range and instructs implementer to grep `CampHubMenuId` per AGENTS.md pitfall #22). T7-T8 task narrative confirms slot 5 is the target index.

**Lock 5 — Phase 19 validator stub does NOT exist; Plan 5 authors from scratch (clarification, BLOCKING for §T28 task description).** §1 deliverable line + §3 file change list both imply "Phase 19 (endeavor catalog validation) populated from Plan 1 stub". Verified: `Tools/Validation/validate_content.py` contains zero references to "phase 19", "endeavor", or `validate_phase_19`. Plan 1 verification doc explicitly states: "no validator phase 18-20 stubs (those land with the Plan that needs them)". Plan 2 ships Phase 18 (companion archetype validation), Plan 3 ships Phase 20 (ceremony completeness, fail-closed). Plan 5 ships Phase 19 from scratch.

**Fix:** §T28 task description amends from "populated from Plan 1 stub" to "authored from scratch following the same shape as Plan 3's `validate_phase_20_ceremony_completeness` (validator function + integration into the main validator dispatch + fail-closed semantics for missing required catalog entries)". §1 deliverable line drops "populated" and reads "Phase 19 validator authored: validates endeavor templates have valid skill axes, companion archetype refs, phase storylet refs, scrutiny risk values within bounds."

### Research-derived locks (6-7)

**Lock 6 — Cross-branch dependency analysis: Plan 5 has NO hard dependency on Plan 4 (BLOCKING for execution-branch decision).** Plan 4 ships on `feature/plan4-officer-trajectory`; not yet merged to `development` as of 2026-04-26. Plan 5's actual substrate dependencies:

| Plan 5 dependency | Source | Branch | Available for Plan 5 prep? |
| :-- | :-- | :-- | :-- |
| `ModalEventBuilder.FireEndeavorPhase` | Plan 1 | `development` | ✓ |
| `EndeavorActivity.cs` + `ContractActivity.cs` empty shells | Plan 1 | `development` | ✓ |
| `CompanionAssignmentManager.IsAssignedToEndeavor` getter/setter scaffolding | Plan 1 | `development` | ✓ |
| `EnlistmentBehavior.EnlistmentTier` (T7+ check for officer flavor) | shipped years | `development` | ✓ |
| `CompanionLifecycleHandler.GetSpawnedCompanions()` (gating predicate) | Plan 2 | `development` | ✓ |
| `ceremony_choice_t<N>_<id>` flags (Rogue-track gating, Lock 2) | Plan 3 | `development` | ✓ |
| `IsOfficer()` helper (Lock 1 says inline-check instead) | Plan 4 | not needed | n/a |

**Net:** Plan 5 prep + execution can begin on `development` immediately. The Plan 4 reference at §0 ref 19 becomes informational ("Plan 4 ships officer-tier dialog flavor via inline `EnlistmentTier >= 7` checks; Plan 5 uses the same idiom for officer-tier endeavor flavor"). No worktree branched off the Plan 4 feature branch is required. **Fix:** rewrite the dependency line at the top of §0 to read "Plans 1-3 are required (substrate + companions + ceremony flags); Plan 4 is referenced for officer-tier idiom only — no hard dependency."

**Lock 7 — Scripted-effect ID collision check passed (verification, NOT BLOCKING).** Plan 5 §3 file change list claims ~20 new effect IDs with prefixes `scrutiny_drift_endeavor_*`, `endeavor_skill_xp_*`, `lord_relation_endeavor_*`. Verified against `ModuleData/Enlisted/Effects/scripted_effects.json` (current 44 entries from the Plan 3 ceremony catalog): no IDs in the proposed prefix space exist today. Plan 3's catalog uses `ceremony_drift_*` for trait drift and `ceremony_witness_reaction_*` for witness reactions; Plan 5's prefixes are disjoint. Plan 5 T6 implementer can author the ~20 endeavor effects without re-checking collision per-ID at task time. Source-of-truth check: at write time, just verify no exact-string match against existing keys (Phase 12 validator catches unknown `apply` values at build time, but does not catch duplicate definitions — JSON loader is "last write wins").

### Execution-shape locks (9-10) — added 2026-04-26 at start of execution

**Lock 9 — Phase advancement is driven by `EndeavorRunner`, NOT by `ActivityRuntime.TryAdvancePhase` (BLOCKING for T1, T3, T9, T10, T12, T14-T18, T28).** The plan's prescribed shape implies a parallel phase system: §4.8 `phase_pool` is an ordered storylet list (one entry = one phase), and T12 has `EndeavorRunner` subscribe to `OnHourlyTick` to advance phases. This conflicts with `ActivityRuntime`'s built-in `DurationHours` + `TryAdvancePhase` model where `Phase.Pool` is a weighted bag picked per tick.

Resolution: **path (a2) — minimal `activity_endeavor.json` + `activity_contract.json` ActivityType definitions**, each with a single long-duration "running" phase (Auto delivery, empty pool, `DurationHours` set well above any endeavor's max so `TryAdvancePhase` never advances). This keeps:

1. Save-load polymorphism via `List<Activity>` at offset 57 (and 56 for contracts).
2. The `Instance => ActivityRuntime.Instance?.FindActive<EndeavorActivity>()` accessor pattern (mirrors `OrderActivity.Instance`).
3. `ActivityRuntime.Start` quiet — without the type registration, `Start` would emit `ModLogger.Surfaced("ACTIVITY", "activity started with no phases resolved — type may be missing from ActivityTypeCatalog")` (verified at `src/Features/Activities/ActivityRuntime.cs:130-134`).

`EndeavorRunner` then:
- Reads `EndeavorTemplate.PhasePool` (ordered storylet IDs) from the loaded catalog.
- Tracks current phase via `EndeavorActivity.AccumulatedOutcomes` (e.g. key `__phase_index__` = N) — no new `CurrentPhase`/`TotalPhases` fields needed; existing dictionary is sufficient.
- Fires phase modals via `ModalEventBuilder.FireEndeavorPhase` on its own hourly schedule.
- Calls `ActivityRuntime.Stop(activity, ActivityEndReason.Completed)` when the resolution storylet has fired.

T3 implication: `EndeavorActivity` keeps Plan 1's existing field set (`EndeavorId, CategoryId, AssignedCompanionIds, StartedOn, AccumulatedOutcomes`). No `CurrentPhase`/`TotalPhases`/`Phase1ChoiceFlag` additions — phase index lives in `AccumulatedOutcomes` under a reserved key, choice memory under per-phase keys (`phase_<N>_choice` etc.). T4 mirrors for `ContractActivity`. Auto-properties on a `[Serializable]` class — NOT `[SaveableField]` (Plan 1 verif §5 fix #4).

**Lock 10 — Execution-order tweak: T28 (validator Phase 19) lands BEFORE the T20-T23 storylet content dispatch (clarification, NOT BLOCKING).** Plan body sequences T28 after T27. But T19-T23 are five categories of ~10-12 storylets each, naturally parallelizable via subagent dispatch. Authoring the validator first (so it fail-closes on missing phase storylet refs / unknown skill axes / out-of-band scrutiny risk values) catches schema violations during the parallel author phase rather than after-the-fact. Final execution order: T1 → T2 → T3-T6 → T28 → T7-T13 → T14-T18 → T19 (main thread, tone exemplar) → T20-T23 (parallel subagents with T2 design guide + T19 references + Phase 19 validator as the gate) → T24-T25 → T26 (defer per stretch flag) → T27 → T29-T30.

### Logistics

**Lock 8 — Architecture brief Plan 4 hand-off lands as a SEPARATE commit on `development`, NOT folded into this Plan 5 prep commit.** Plan 5 prep commit is purely doc edits to this plan file. The architecture brief Plan 4 hand-off section (lists the 18 `lord_gifted_<culture>_t<tier>` ItemModifier StringIds + the `mod_lord_gifted` ItemModifierGroup, the cape mapping in `cape_progression.json` with 6 culture buckets, the 3 banner-flag schema keys `officer_banner_t<7,8,9>`, the 4 added dialog tokens `IS_OFFICER` / `PLAYER_RANK_TITLE` / `BANNER_NAME` / `PATRON_NAME`, the 3 Harmony-patched APIs, the subscriber-order convention that `OfficerTrajectoryBehavior` registers after `RankCeremonyBehavior`) gets its own small commit on `development` AFTER Plan 4 merges. Keeping the two commits separate isolates the Plan 5 prep diff for review and lets the Plan 4 hand-off entry land precisely when its claims are true on `development`.

---

## §0 — Read these first

### Required prior plan documentation
1. **[Plan 1](2026-04-24-ck3-wanderer-architecture-foundation.md)** + verification — `ModalEventBuilder.FireEndeavorPhase` already exists; `EndeavorActivity` + `ContractActivity` empty shells already registered at offsets 56-57; `CompanionAssignmentManager.IsAssignedToEndeavor` flag scaffolded.
2. **[Plan 2](2026-04-24-ck3-wanderer-companion-substrate.md)** + verification — six companion archetypes spawn at appropriate tiers; `CompanionLifecycleHandler.GetSpawnedCompanions()` returns active list. Plan 5 reads this for hybrid gating.
3. **[Plan 3](2026-04-24-ck3-wanderer-rank-ceremony-arc.md)** + verification — choice-memory `FlagStore` keys (`ceremony.t{N}.choice`) available for endeavor flavor + gating.
4. **[Plan 4](2026-04-24-ck3-wanderer-officer-trajectory.md)** + verification — `IsOfficer()` helper available; officer-tier endeavor flavor possible.
5. **[Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)**.

### Required spec reading
6. **[Spec v6 §3.8 Endeavor System](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — design source. Five categories, hybrid gating, phase pattern, scrutiny risk for Rogue, sibling-not-merge with Orders.
7. **[Spec v6 §3.4 Side-contracts](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — notable-issued sibling flavor; ships in same backbone as `ContractActivity` (offset 56).
8. **[Spec v6 §6.7 CK3 schemes → Endeavor mapping](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — what we keep vs drop from CK3.
9. **[Spec v6 §6.8 Canonical modal-popup pipeline](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — `ModalEventBuilder.FireEndeavorPhase` recipe.

### Required project guidance
10. **[AGENTS.md](../../../AGENTS.md)** — Critical Rule #10 (StoryDirector routing for endeavor phase modals).
11. **[CLAUDE.md](../../../CLAUDE.md)** — Pitfall #14 (scripted-effect IDs registered), Pitfall #16 (no cyclic effects), Pitfall #21 (news-feed throttle silences extreme fast-forward — endeavor phases ticking at FF 4x+ should still log but may not surface as news).

### Required existing-code orientation
12. **`src/Features/Activities/Orders/OrderActivity.cs`** — read end-to-end. The shipped Activity subclass that `EndeavorActivity` + `ContractActivity` mirror. Phase tracking, OnStart, OnTick, OnComplete, save fields.
13. **`src/Features/Content/ActivityRuntime.cs:267`** — auto-emits phase storylets via `storylet.ToCandidate(ctx)`. Plan 5 does NOT use this path for player-choice modals; uses `ModalEventBuilder.FireEndeavorPhase` instead.
14. **`src/Features/Activities/Home/HomeEveningMenuProvider.cs:37-72`** — canonical menu→modal precedent. `EndeavorPhaseProvider` mirrors this exact shape.
15. **`src/Features/Endeavors/EndeavorActivity.cs`** + **`src/Features/Contracts/ContractActivity.cs`** — empty Activity subclasses from Plan 1 T2/T10. Plan 5 populates.
16. **`src/Features/Retinue/Core/CompanionAssignmentManager.cs`** — `IsAssignedToEndeavor` field from Plan 1 T8. Plan 5 populates.
17. **`src/Features/Companions/CompanionLifecycleHandler.cs`** — Plan 2 ships `GetSpawnedCompanions()`. Plan 5 reads for gating.
18. **`src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:1345-1482`** — Camp hub menu registration. Plan 5 adds slot 5 "Endeavors" + sub-menu.
19. **`src/Features/Content/StoryletCatalog.cs`** — content loader. Plan 5 endeavor phase storylets land in `ModuleData/Enlisted/Storylets/endeavor_*.json`.
20. **`src/Features/Qualities/QualityStore.cs`** — `scrutiny` quality is read-write. Plan 5 Rogue scrutiny risk applies via existing `quality_add` primitive.
21. **`ModuleData/Enlisted/Effects/scripted_effects.json`** — existing effects catalog. Plan 5 may add ~20 endeavor-specific effects.

### Required decompile orientation
22. **`Decompile/TaleWorlds.CampaignSystem/CampaignTime.cs`** — phase scheduling uses `CampaignTime.HoursFromNow` / `DaysFromNow`. Plan 5 phase ticks check elapsed time.
23. **`Decompile/TaleWorlds.CampaignSystem/MBRandom.cs`** — random rolls for endeavor success. Plan 5 uses `MBRandom.RandomFloat` for skill-vs-difficulty checks.

---

## §1 — What this plan delivers

After Plan 5 ships:

- **Camp menu slot 5 "Endeavors" sub-option** opens an Endeavor sub-menu listing all currently-available endeavors filtered by hybrid gating.
- **Five endeavor categories live:** Soldier (combat / leadership), Rogue (illicit, scrutiny risk), Medical (healing / herbalism), Scouting (terrain / intel), Social (relationships / influence).
- **~30 endeavor templates authored** across the 5 categories. Each template has ID, category, duration (3-7 in-game days), skill axis, optional companion archetype slots, phase storylet pool, resolution storylets.
- **~60 phase storylets authored** (~2 phases per endeavor on average). Each phase fires as a modal via `ModalEventBuilder.FireEndeavorPhase` with `ChainContinuation = true` to bypass cooldowns for in-progress phases.
- **Hybrid skill-OR-companion gating** works per spec §3.8: a category unlocks when player skill ≥ threshold OR a companion of matching archetype exists.
- **Companion-agent locking** ships: assigning a companion to an endeavor sets `CompanionAssignmentManager.IsAssignedToEndeavor(hero, true)`; companion cannot join other endeavors during the run.
- **Scrutiny risk for Rogue category** mechanics: each phase has a discovery roll; failure adds to player's `scrutiny` quality (existing) and triggers downstream consequences (e.g. lord relation drop, possible flogging storylet — Plan 7 polish).
- **`ContractActivity` notable-issued sibling** ships as a separate Activity subclass at offset 56. Notables in towns can offer endeavor-flavored contracts. ~10 contract archetypes initially; full breadth in Plan 7.
- **Phase 19 validator populated:** validates endeavor templates have valid skill axes, companion archetype refs, phase storylet refs, scrutiny risk values within bounds.

**Player-visible delta:** "Endeavors" appears in Camp menu at slot 5; selecting it shows available activities filtered by what the player can do (their skills + spawned companions); each endeavor opens a multi-day text-driven story arc with 2-3 modal choices per phase culminating in a resolution.

**No new substrate beyond ContractActivity + EndeavorActivity. No new save-classes beyond what Plan 1 reserved.**

---

## §2 — Subsystems explored

| Audit | Finding | Spec |
| :-- | :-- | :-- |
| CK3 schemes mechanic | Multi-phase + agents + secrecy + power-vs-resistance; we adapt phases + agents (capped 1-2), drop spymaster math | §6.7 |
| CK3 Roads to Power contracts | Bread and butter income; we adapt as ContractActivity sibling, notable-issued | §3.4 |
| Modal pipeline | `ModalEventBuilder.FireEndeavorPhase` with `ChainContinuation = true` for back-to-back phase emission | §6.8 |
| Activity backbone | `OrderActivity` is shipped; mirror its structure for `EndeavorActivity` / `ContractActivity` | §3.4 |
| Companion gating | `CompanionLifecycleHandler.GetSpawnedCompanions()` enables hybrid model — companion presence unlocks category access | §3.8 |
| Pacing rails | `DensitySettings.CategoryCooldownDays = 3` blocks within-category modals; `ChainContinuation = true` bypasses for in-progress phases | §6.8 |
| Scrutiny mechanic | Existing `QualityStore.scrutiny` quality with thresholds; Rogue endeavors add via existing `quality_add` primitive | §3.8 |

---

## §3 — Subsystems Plan 5 touches

### Files modified

| File | Change | Tasks |
| :-- | :-- | :-- |
| `src/Features/Endeavors/EndeavorActivity.cs` (Plan 1 stub) | Populate full Activity logic — phase tracking, agent assignment, lifecycle | T3 |
| `src/Features/Contracts/ContractActivity.cs` (Plan 1 stub) | Populate notable-issued sibling logic | T4 |
| `src/Features/Retinue/Core/CompanionAssignmentManager.cs` (Plan 1 T8 scaffolded) | Populate `IsAssignedToEndeavor` getter/setter and SyncData | T5 |
| `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:1345-1482` | Add slot 5 "Endeavors" Camp menu option + endeavor sub-menu | T7-T8 |
| `Tools/Validation/validate_content.py` | Phase 19 (endeavor catalog validation) populated from Plan 1 stub | T28 |
| `ModuleData/Enlisted/Effects/scripted_effects.json` | Add ~20 endeavor-specific scripted effects (skill_xp_endeavor_*, scrutiny_drift_*, lord_relation_endeavor_*, etc.) | T6 |

### Files created

| File | Purpose | Tasks |
| :-- | :-- | :-- |
| `docs/Features/Endeavors/endeavor-catalog-schema.md` | Schema reference for endeavor template JSON | T1 |
| `docs/Features/Endeavors/endeavor-design-guide.md` | Design guide for authors (category boundaries, scrutiny tuning, companion synergy patterns) | T2 |
| `src/Features/Endeavors/EndeavorPhaseProvider.cs` | Modeled on `HomeEveningMenuProvider`; fires phase modals | T9 |
| `src/Features/Endeavors/EndeavorCatalog.cs` | Loads `endeavor_catalog.json` + provides query API | T10 |
| `src/Features/Endeavors/EndeavorGatingResolver.cs` | Hybrid skill-OR-companion gating logic | T11 |
| `src/Features/Endeavors/EndeavorRunner.cs` | Coordinates EndeavorActivity lifecycle: phase scheduling, agent locking, resolution dispatch | T12 |
| `src/Features/Endeavors/ScrutinyRiskCalculator.cs` | Computes Rogue category discovery roll per phase | T13 |
| `ModuleData/Enlisted/Endeavors/endeavor_catalog.json` | All endeavor template definitions (~30) | T14-T18 |
| `ModuleData/Enlisted/Storylets/endeavor_soldier_*.json` | Soldier category phase + resolution storylets (~12 storylets) | T19 |
| `ModuleData/Enlisted/Storylets/endeavor_rogue_*.json` | Rogue category storylets (~12) | T20 |
| `ModuleData/Enlisted/Storylets/endeavor_medical_*.json` | Medical category storylets (~10) | T21 |
| `ModuleData/Enlisted/Storylets/endeavor_scouting_*.json` | Scouting category storylets (~10) | T22 |
| `ModuleData/Enlisted/Storylets/endeavor_social_*.json` | Social category storylets (~10) | T23 |
| `ModuleData/Enlisted/Endeavors/contract_archetypes.json` | Notable-issued contract templates (~10) | T24 |
| `ModuleData/Enlisted/Storylets/contract_*.json` | Contract phase storylets (~15 storylets) | T25 |
| `Enlisted.csproj` | New ItemGroup + MakeDir + Copy for Endeavors data dir | T14 |
| `docs/superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system-verification.md` | Plan 5 verification report | T30 |

### Subsystems Plan 5 does NOT touch

- OrderActivity (Plan 5 is sibling, not migration — per spec §3.4 sibling-not-merge)
- Companion spawning (Plan 2)
- Ceremony storylets (Plan 3)
- Officer Trajectory equipment (Plan 4)
- Patron favors (Plan 6)
- Personal Kit catalog (Plan 7)

---

## §4 — Locked design decisions

### §4.1 Five categories (LOCKED)

Per spec §3.8:

| Category | Skill axis | Scrutiny risk? | Companion synergy |
| :-- | :-- | :-: | :-- |
| **Soldier** | One-Handed / Two-Handed / Athletics / Leadership | No | Veteran (combatant boost) |
| **Rogue** | Roguery / Charm | YES | Field Medic (Charm) or Veteran (Roguery if rolled archetype) |
| **Medical** | Medicine / Steward | No | Field Medic (Medicine 100 — massive bonus) |
| **Scouting** | Scouting / Riding / Bow | Mild | Pathfinder (Scouting 80) |
| **Social** | Charm / Roguery | No | Companion-as-confidant (any high-relation companion gives +relation drift) |

### §4.2 Hybrid gating (LOCKED)

Category unlocks when **either** (OR, not AND):
- Player skill ≥ threshold (per category, see §4.3 table)
- A companion of matching archetype exists (`CompanionLifecycleHandler.GetSpawnedCompanions()` returns one matching the category)

### §4.3 Skill thresholds for self-gating (LOCKED)

| Category | Self-gate skill ≥ |
| :-- | :-- |
| Soldier | One-Handed/Two-Handed 50 OR Leadership 40 |
| Rogue | Roguery 60 |
| Medical | Medicine 50 |
| Scouting | Scouting 50 |
| Social | Charm 50 OR Leadership 60 |

If player skill is below threshold AND no matching companion exists → category locked, sub-menu shows it greyed with explanation ("Need Roguery 60 or a Roguery companion").

### §4.4 Endeavor concurrency (LOCKED)

Player can have **1 active major endeavor at a time** (multi-phase, takes 3-7 in-game days). Player can ALSO trigger up to **3 minor endeavors per in-game week** (single-phase, instant resolution — formerly "Adventurer Decisions" per spec §3.1).

Major endeavor active → endeavor sub-menu shows "Cancel current endeavor (forfeit progress)" plus ability to start minors.

### §4.5 Companion-agent locking (LOCKED)

When a companion is assigned to an endeavor, `CompanionAssignmentManager.SetAssignedToEndeavor(hero, true)`. They cannot be assigned to another endeavor until the current one resolves. Stay-back toggle from Plan 1 T6 is **independent** (a companion can be both endeavor-assigned AND combat-stay-back; they're locked into the endeavor task but not fighting).

Cap: **1-2 companions per endeavor**, defined per template.

### §4.6 Scrutiny mechanic (LOCKED)

Rogue category endeavors carry per-phase discovery risk. Each phase rolls:

```
discovery_chance = baseRisk - (companionCharmContribution + playerCharmContribution)
roll = MBRandom.RandomFloat
if (roll < discovery_chance) → discovery; apply scrutiny_drift_<magnitude>
```

`baseRisk` per phase: 0.15 (15% per phase) typical. With Charm 60 player + Charm 60 Rogue companion, contribution can reduce to ~0.05 (5%).

`scrutiny_drift_<magnitude>` is an existing scripted effect; Plan 5 may add `scrutiny_drift_endeavor_minor` (delta +5) and `scrutiny_drift_endeavor_major` (delta +15) per discovery severity.

When `scrutiny` quality crosses thresholds (50, 75, 90), downstream consequences fire (per existing escalation system; Plan 5 hooks into existing).

### §4.7 Endeavor lifecycle (LOCKED)

1. **Selection:** Player opens Camp → Endeavors → picks one available endeavor.
2. **Setup:** Inquiry to choose 1-2 companions to assign (if endeavor template defines slots). Sets companion-agent flag.
3. **Phase 1 fires:** ~1 in-game day after start. Modal pops up with prompt + 2-3 choices. Each choice has effects + may flag for next phase.
4. **Phase 2 fires:** ~2-3 in-game days later. Builds on phase 1's flagged choice if any.
5. **Resolution:** ~5-7 in-game days from start. Modal with success/partial/failure outcome based on accumulated phase choices + skill rolls. Effects apply (gold, skill XP, lord relation, scrutiny, trait drift, follow-up flags).
6. **Cleanup:** `CompanionAssignmentManager.SetAssignedToEndeavor(hero, false)` for each agent. EndeavorActivity destroyed.

### §4.8 Endeavor template JSON schema (LOCKED)

```json
{
  "schemaVersion": 1,
  "endeavors": [
    {
      "id": "endeavor.soldier.drill_competition",
      "category": "soldier",
      "title_id": "endeavor_soldier_drill_title",
      "title": "Win the regimental drill competition",
      "description_id": "endeavor_soldier_drill_desc",
      "description": "Outperform your peers in the company's monthly drill.",
      "duration_days": 5,
      "skill_axis": ["one_handed", "athletics"],
      "self_gate_skill_threshold": 50,
      "companion_slots": [
        { "archetype": "veteran", "role": "mentor", "required": false }
      ],
      "phase_pool": [
        "endeavor.soldier.drill.phase_1_warmup",
        "endeavor.soldier.drill.phase_2_match"
      ],
      "resolution_storylets": {
        "success": "endeavor.soldier.drill.resolution_success",
        "partial": "endeavor.soldier.drill.resolution_partial",
        "failure": "endeavor.soldier.drill.resolution_failure"
      },
      "scrutiny_risk_per_phase": 0.0
    }
  ]
}
```

---

## §5 — Tooling and safeguards

Inherits Plans 1-4. Plan 5-specific:

### Endeavor smoke recipe

For each endeavor (T19-T23):
1. Build clean.
2. Set up player state to satisfy gating (skill or companion).
3. Camp → Endeavors → confirm endeavor appears in available list.
4. Select endeavor; assign companions if slots; confirm flags set.
5. Wait 1 in-game day; confirm phase 1 modal fires.
6. Pick option; confirm flags set + skill XP applied.
7. Wait 2-3 in-game days; confirm phase 2 modal fires.
8. Pick option; wait until resolution.
9. Confirm resolution modal fires; effects apply; agent flags cleared.
10. Save → reload → mid-endeavor state preserved (if saved between phases).

### Rogue scrutiny smoke recipe (T20)

For Rogue endeavors:
1. Set up player with Roguery 80 (above self-gate).
2. Run a Rogue endeavor with NO Charm companion (high baseRisk).
3. Track scrutiny quality before + after each phase. Confirm increases on discovery rolls.
4. Repeat with high-Charm Field Medic assigned. Confirm reduced increase.

---

## §6 — Tasks (sequential)

### T1 — Endeavor catalog schema documented
**Goal:** Schema reference at `docs/Features/Endeavors/endeavor-catalog-schema.md` per §4.8.

### T2 — Endeavor design guide
**Goal:** Author guide at `docs/Features/Endeavors/endeavor-design-guide.md`. Covers category boundaries, scrutiny tuning rules of thumb, companion synergy patterns, phase pacing.

### T3 — `EndeavorActivity` populated
**Goal:** Replace Plan 1 stub with full Activity subclass. Phase tracking, agent assignment, lifecycle hooks.

**Files:** Edit `src/Features/Endeavors/EndeavorActivity.cs`. Mirror `OrderActivity` structure.

**Concrete:** Add `[SaveableField]` for `EndeavorId`, `CurrentPhase`, `TotalPhases`, `AssignedCompanionIds` (List<MBGUID>), `StartedOn`, `Phase1ChoiceFlag` etc. Implement OnStart (set companion flags), OnTick (check phase timing), OnComplete (clear companion flags).

**Verification:** Save-load round-trip with active endeavor; confirm preserved.

### T4 — `ContractActivity` populated
**Goal:** Sibling Activity for notable-issued contracts. Same structure as EndeavorActivity but origin is a notable hero, not the player.

**Verification:** Same as T3.

### T5 — `CompanionAssignmentManager` endeavor flag populated
**Goal:** Replace Plan 1 T8 scaffolded stubs with real implementation.

**Concrete:** `IsAssignedToEndeavor(hero)` reads `_companionEndeavorAssignment[hero.StringId]`. `SetAssignedToEndeavor(hero, bool)` writes. SyncData persists. EnsureInitialized handles null.

**Verification:** Save-load round-trip with one assignment flag.

### T6 — Endeavor scripted effects added
**Goal:** Add ~20 endeavor-specific entries to `scripted_effects.json`.

**Examples:**
- `endeavor_skill_xp_one_handed_minor` (delta +20)
- `endeavor_skill_xp_one_handed_major` (delta +50)
- `endeavor_lord_relation_up_small` (delta +5 with current lord)
- `endeavor_lord_relation_down_medium` (delta -10)
- `endeavor_scrutiny_drift_minor` (delta +5)
- `endeavor_scrutiny_drift_major` (delta +15)
- `endeavor_gold_reward_small` (give 100 denars)
- `endeavor_gold_reward_medium` (500 denars)
- `endeavor_gold_reward_large` (1500 denars)
- `endeavor_set_choice_flag` (set ceremony.endeavor.<id>.<phase>.<choice>)

Plus existing `quality_add`, `set_flag`, `give_gold` primitives are reused.

### T7 — Camp menu slot 5 "Endeavors" option

**Goal:** Add Camp menu option at index 5. Condition: at least one endeavor available OR active. Consequence: switches to `enlisted_endeavors` sub-menu.

**Files:** Edit `EnlistedMenuBehavior.cs:RegisterCampHubMenu`.

### T8 — `enlisted_endeavors` sub-menu
**Goal:** New sub-menu listing available endeavors per category, with active endeavor's status displayed at top if any.

**Concrete:** Use `MultiSelectionInquiryData` to list endeavors (same pattern as Plan 2 T20 talk-to). Selecting one opens an "Assign companions" inquiry, then starts the endeavor.

### T9 — `EndeavorPhaseProvider`
**Goal:** Modeled on `HomeEveningMenuProvider.OnSlotSelected`. Builds phase modal via `ModalEventBuilder.FireEndeavorPhase`.

**Concrete:**

```csharp
public static class EndeavorPhaseProvider
{
    public static void FirePhase(EndeavorActivity activity)
    {
        var template = EndeavorCatalog.GetTemplate(activity.EndeavorId);
        if (template == null) return;
        
        var phaseId = template.PhasePool[activity.CurrentPhase];
        var ctx = BuildContext(activity);
        ModalEventBuilder.FireEndeavorPhase(phaseId, ctx, activity);
        // ChainContinuation = true is set inside FireEndeavorPhase
    }
}
```

### T10 — `EndeavorCatalog`
**Goal:** Load `endeavor_catalog.json` at session launch. Provide `GetTemplate(id)`, `GetAvailableEndeavors(player, companions)`, `GetByCategory(category)`.

### T11 — `EndeavorGatingResolver`
**Goal:** Implements §4.2 hybrid gating. `bool IsAvailable(EndeavorTemplate, Hero player, List<Hero> spawnedCompanions)`.

### T12 — `EndeavorRunner`
**Goal:** Campaign behavior orchestrating endeavor lifecycle. Subscribes to `OnHourlyTick` to check active endeavor's phase timing; fires `EndeavorPhaseProvider.FirePhase` when phase due. On resolution, applies effects and clears agent flags.

### T13 — `ScrutinyRiskCalculator`
**Goal:** §4.6 discovery roll math. `bool RollDiscovery(EndeavorActivity, Hero player, List<Hero> agents)` returns true if discovered.

### T14 — `endeavor_catalog.json` Soldier templates (~6)

**Templates:**
1. `endeavor.soldier.drill_competition` — Win regimental drill (5 days)
2. `endeavor.soldier.teach_recruit` — Teach a recruit your trade (3 days)
3. `endeavor.soldier.lord_notice_skirmish` — Earn lord's notice (depends on next battle)
4. `endeavor.soldier.physical_training` — Athletics-focused training (4 days)
5. `endeavor.soldier.weapon_mastery` — Skill-specific weapon training (5 days)
6. `endeavor.soldier.lead_patrol` — Lead a small patrol (3 days, T5+ only)

### T15 — `endeavor_catalog.json` Rogue templates (~6)

**Templates:**
1. `endeavor.rogue.dice_game` — Run a dice game in camp (3 days, scrutiny risk)
2. `endeavor.rogue.smuggle_wine` — Smuggle wine past the QM (4 days, high scrutiny risk)
3. `endeavor.rogue.pickpocket` — Pickpocket camp followers (3 days, high scrutiny risk)
4. `endeavor.rogue.black_market` — Sell looted goods on the black market (5 days, medium scrutiny risk)
5. `endeavor.rogue.gambling_ring` — Build a gambling ring (7 days, T3+, medium scrutiny risk)
6. `endeavor.rogue.bribe_officer` — Bribe an officer for inside info (4 days, T5+, high scrutiny risk)

### T16 — `endeavor_catalog.json` Medical templates (~5)

**Templates:**
1. `endeavor.medical.forage_herbs` — Forage medicinal herbs (3 days)
2. `endeavor.medical.tend_sick` — Tend to a sick officer (2 days)
3. `endeavor.medical.poultice_recipes` — Learn new poultice recipes (4 days)
4. `endeavor.medical.field_surgery` — Perform field surgery (5 days)
5. `endeavor.medical.disease_outbreak` — Manage a disease outbreak (7 days, T5+)

### T17 — `endeavor_catalog.json` Scouting templates (~5)

**Templates:**
1. `endeavor.scouting.map_terrain` — Map the surrounding terrain (4 days)
2. `endeavor.scouting.track_patrol` — Track an enemy patrol (3 days, mild scrutiny risk)
3. `endeavor.scouting.report_morale` — Report on village morale (3 days)
4. `endeavor.scouting.deep_recon` — Deep reconnaissance behind enemy lines (7 days, T5+)
5. `endeavor.scouting.escape_routes` — Identify escape routes from current position (3 days)

### T18 — `endeavor_catalog.json` Social templates (~5)

**Templates:**
1. `endeavor.social.befriend_sergeant` — Befriend Sergeant Garreth (7 days, requires Sergeant)
2. `endeavor.social.sway_lord` — Sway the lord's opinion (5 days)
3. `endeavor.social.court_camp_follower` — Court a camp follower (7 days)
4. `endeavor.social.influence_companion` — Deepen relation with one companion (5 days)
5. `endeavor.social.peer_officer_alliance` — Build an alliance with a peer officer (7 days, T7+)

### T19 — Soldier category storylets (~12 storylets — phases + resolutions)

**Goal:** Author phase + resolution storylets for the 6 Soldier endeavors.

**Files:** New `ModuleData/Enlisted/Storylets/endeavor_soldier_*.json` per endeavor.

**Each storylet:** prompt + 2-3 options each with text + effects (skill XP, lord relation, flags) + witness reactions if applicable.

### T20 — Rogue category storylets (~12)
Same as T19 but Rogue category. Each phase includes scrutiny roll. Each option has scrutiny-mitigation factor (clever options reduce, brash options increase).

### T21 — Medical category storylets (~10)
Same.

### T22 — Scouting category storylets (~10)
Same. Mild scrutiny mainly on `track_patrol`.

### T23 — Social category storylets (~10)
Same. Companion-relation drift heavy.

### T24 — `contract_archetypes.json` notable-issued contracts (~10)
**Goal:** Notable-issued sibling templates. Each has notable type (innkeeper, merchant, headman) + payment + risk.

### T25 — Contract phase storylets (~15)
**Goal:** Author for the 10 contracts. Some 1-phase, some 2-phase.

### T26 — Endeavor sub-menu integration with companion talk-to
**Goal:** When player talks to a companion (Plan 2 T20 sub-menu), if there's an endeavor option that companion is eligible for, surface "Help me with [endeavor]" as a dialog option.

**Stretch goal — defer to Plan 7 polish if time-constrained.**

### T27 — Cross-system flag integration
**Goal:** Endeavor templates can read ceremony choice flags + lifestyle unlock flags as gating/flavor conditions.

**Examples:**
- `endeavor.rogue.dice_game` requires `ceremony.t2.choice == "frugal"` to be available (you've shown discipline; no recruit-dice for you initially)
- `endeavor.medical.poultice_recipes` flavored by `ceremony.t4.choice` (your moral character)

### T28 — Phase 19 validator populated

**Goal:** Replace Plan 1 T14 stub with real validation:
- Endeavor template skill_axis values are valid skill IDs (cross-ref Skills.xml)
- Companion archetype refs are valid (sergeant/field_medic/pathfinder/veteran/qm_officer/junior_officer)
- Phase storylet refs resolve in StoryletCatalog
- Resolution storylet refs resolve
- scrutiny_risk_per_phase in [0.0, 1.0]
- Duration in [1, 14] days

### T29 — End-to-end smoke test

**Goal:** Run a full Soldier endeavor (drill competition) end-to-end:
1. Set up T3 player with Veteran companion (T5 — wait, Veteran spawns at T5; choose Soldier endeavor that doesn't require Veteran for T3 demo).
2. Better: demo with `endeavor.soldier.drill_competition` requiring no companion at T3.
3. Open Camp → Endeavors → see drill competition → pick.
4. Wait 1 day; phase 1 modal fires; pick option.
5. Wait 2 days; phase 2 modal fires; pick option.
6. Wait 2 more days; resolution modal fires; effects apply.
7. Verify: skill XP added, lord relation drifted, no scrutiny added (Soldier doesn't trigger).

Run additional smokes for Rogue (verify scrutiny accumulates) and Medical (verify Field Medic agent provides bonus).

### T30 — Plan 5 verification report

**Goal:** Document smoke results, content authoring stats, sign-off.

---

## §7 — Risks

### Risk H1 — Phase pacing conflicts with DensitySettings (HIGH)

**Vector:** Endeavor phases use `ChainContinuation = true` to bypass cooldowns. If multiple endeavors active or phase + ceremony fire same tick, modal queue may stack.

**Mitigation:**
- §4.4 caps to 1 active major endeavor at a time
- `EventDeliveryManager` queues modals naturally; chained endeavor phase doesn't fire if ceremony is currently showing
- T29 end-to-end smoke verifies no stacking

### Risk M1 — Content authoring quality across 30 templates + 60 storylets (MEDIUM)

**Vector:** AI-generated dialog can be technically schema-valid but emotionally repetitive or thin.

**Mitigation:**
- T1 + T2 design guide locks tone reference (match QM dialog density)
- Author Soldier templates first as tone reference (T19); other categories follow
- Plan 7 polish pass available for tuning + content quality

### Risk M2 — Hybrid gating UX confusing (MEDIUM)

**Vector:** Player sees Roguery endeavor greyed; needs to know they need either skill or companion. UI may be unclear.

**Mitigation:**
- Sub-menu shows greyed entries with explanation tooltip ("Need Roguery 60 OR a Roguery companion")
- T8 implements clear visual hierarchy

### Risk M3 — Scrutiny mechanics too punishing or too lenient (MEDIUM)

**Vector:** discovery_chance numbers in §4.6 are placeholders; actual playtest may show too many or too few discoveries.

**Mitigation:**
- T29 smoke validates the math
- Plan 7 polish pass tunes after playtest data

### Risk L1 — Contract sibling adds complexity (LOW)

**Vector:** ContractActivity at offset 56 is a parallel system to EndeavorActivity at 57. Two save classes for similar concepts.

**Mitigation:**
- Keep them genuinely sibling — different entry points, different state, no shared code beyond the Activity backbone
- Per spec §3.4, this was the explicit decision

---

## §8 — Verification gates

- [ ] Build clean
- [ ] Validators pass (Phase 19 populated)
- [ ] All 30 endeavor templates load without error
- [ ] All ~60 phase storylets resolve in catalog
- [ ] Camp menu slot 5 "Endeavors" appears with available list
- [ ] Hybrid gating works (skill OR companion unlocks category)
- [ ] Multi-phase endeavor runs end-to-end (T29 smoke)
- [ ] Scrutiny accumulates correctly for Rogue endeavors
- [ ] Companion-agent locking prevents double-assignment
- [ ] Save-load preserves mid-endeavor state
- [ ] ContractActivity sibling registers and runs (basic smoke; full notable integration deferred to Plan 7)
- [ ] Verification report committed

---

## §9 — Definition of done

Plan 5 complete when 30 tasks ✅, §8 gates pass, report committed.

---

## §10 — Hand-off to Plans 6-7

### For Plan 6 (Roll of Patrons)
- Patron favors can include "another contract" — links into ContractActivity sibling.

### For Plan 7 (Polish + Smoke)
- Tune scrutiny risk values per playtest.
- Expand contract archetypes from 10 → 20+.
- Author cultural variants for endeavors.
- Custom 3D mesh assets for endeavor-related items if any.
- Stretch: T26 endeavor-companion talk-to integration if deferred.

---

## §11 — Out of scope

- News-feed integration of endeavor outcomes
- Cultural variants per endeavor (Plan 7)
- Endeavor-companion talk-to deep integration (deferred)
- New scripted-effect primitives beyond what's listed (use existing)
- Order migration (Plan 5 is sibling per spec §3.4)

---

## §12 — References

- Plans 1-4 + verification reports
- Spec v6 §3.8 + §3.4 + §6.7 + §6.8
- AGENTS.md / CLAUDE.md
- Existing `OrderActivity`, `ActivityRuntime`, `HomeEveningMenuProvider`, `ModalEventBuilder`, `CompanionLifecycleHandler`, `CompanionAssignmentManager`, `EnlistedMenuBehavior`, `StoryletCatalog`, `EffectExecutor`, `QualityStore`
- Decompile: `CampaignTime`, `MBRandom`, `MultiSelectionInquiryData`
