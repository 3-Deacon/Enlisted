# Plan 5 — CK3 Wanderer Endeavor System: Verification Report (PARTIAL — 2026-04-26)

**Status:** 🟡 **Partial — Phase A + B shipped on a feature branch; content authoring (Phase C-E) + integration + smoke (Phase F) pending.** This report is the formal hand-off record for the next session/operator picking up where the current session paused.

**Plan:** [2026-04-24-ck3-wanderer-endeavor-system.md](2026-04-24-ck3-wanderer-endeavor-system.md)
**Brief:** [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md)
**Branch:** `feature/plan5-endeavor-system` (in worktree `.worktrees/plan5-endeavor-system/`)
**Commits on the branch (newest first):**
- `4e854b4` — `feat(endeavors): Plan 5 Phase B — C# wiring stack + Camp menu slot 5`
- `81fa6de` — `feat(endeavors): Plan 5 Phase A — substrate + effects + Phase 19 validator`

**Date:** 2026-04-26

---

## §1 — What shipped (13 of 30 tasks: T1-T13 + T28)

### Phase A — substrate + effects + validator (commit `81fa6de`)

**New docs (2)**
- `docs/Features/Endeavors/endeavor-catalog-schema.md` — schema reference for the endeavor template JSON (~280 lines). T1.
- `docs/Features/Endeavors/endeavor-design-guide.md` — design counterpart (~250 lines): category identities, scrutiny tuning, companion synergy patterns, phase pacing, worked example. T2.

**Plan amendments (added inline before execution started)**
- Lock 9 — phase advancement is driven by `EndeavorRunner`, NOT `ActivityRuntime.TryAdvancePhase`. Score lives in `endeavor_active_score` global quality, NOT per-endeavor.
- Lock 10 — T28 (validator Phase 19) lands BEFORE the T20-T23 storylet content dispatch.

**C# substrate populated (2 files)**
- `src/Features/Endeavors/EndeavorActivity.cs` — full Activity subclass at offset 57. `Instance` accessor mirrors `OrderActivity.Instance`. `OnStart` sets companion endeavor-assignment locks via `CompanionAssignmentManager`; `Finish` clears them. Phase index + total + score live in `AccumulatedOutcomes` under reserved `__-prefixed` keys (`__phase_index__`, `__total_phases__`, `__score__`). `GetPhaseIndex/SetPhaseIndex/GetScore/AddScore` helpers; `GetAssignedCompanions` resolves `MBGUID` → `Hero` via `MBObjectManager`. T3.
- `src/Features/Contracts/ContractActivity.cs` — sibling at offset 56. Mirrors `EndeavorActivity` shape. Adds `IssuingNotableId` (notable origin), `SettlementId`, `DeadlineOn`, `IsPastDeadline()`. Same companion-locking + score helpers. T4.

**T5 verification only** — `CompanionAssignmentManager.IsAssignedToEndeavor` / `SetAssignedToEndeavor` / `SyncData` were already populated by Plan 1 (commit `aa3ef16`); no further work needed. Save-load round-trip pending in-game smoke (T29 deliverable).

**Two new ActivityType definitions (per Lock 9)**
- `ModuleData/Enlisted/Activities/activity_endeavor.json` — single long-duration "running" phase (Auto delivery, empty pool). Keeps `ActivityRuntime.Start` quiet (otherwise it Surfaced-logs "activity started with no phases resolved"); EndeavorRunner does its own advancement.
- `ModuleData/Enlisted/Activities/activity_contract.json` — same shape for contracts.

**31 endeavor scripted effects + 1 quality (T6)**
- `ModuleData/Enlisted/Effects/scripted_effects.json` extended with prefixed entries:
  - 4 score wrappers (`endeavor_score_plus_1` / `_minus_1` / `_plus_2` / `_minus_2`)
  - 13 skill-XP wrappers covering each category's dominant skill at 2 sizes (combat / athletics / leadership / roguery / charm / medicine / scouting)
  - 4 lord-relation wrappers (`endeavor_lord_relation_up_small` etc.)
  - 3 scrutiny-drift wrappers (`_minor` / `_moderate` / `_major`)
  - 3 gold-reward wrappers (`_small` 100 / `_moderate` 500 / `_large` 1500 denars)
  - 4 readiness + medical_risk wrappers
- `ModuleData/Enlisted/Qualities/quality_defs.json` — added `endeavor_active_score` (range −20..+20, default 0, no decay). `EndeavorRunner` clears to 0 on endeavor start AND finish so score doesn't leak between endeavors.

**Validator Phase 19 from scratch (T28)**
- `Tools/Validation/validate_content.py` — added `phase19_endeavor_catalog` (~280 lines) wired between Phase 18 and Phase 20. Soft on missing files (warning — incremental landing per T14/T24 schedule). Hard errors on schema violations once files exist:
  - Required fields, reference integrity (phase_pool + resolution_storylets resolve in StoryletCatalog), bounds (duration_days `[1,14]`, scrutiny_risk_per_phase `[0,1]`, tier_min `[1,9]`, companion_slots ≤2, phase_pool length `[1,4]`).
  - Category-scrutiny coherence (non-rogue MUST have scrutiny=0).
  - Valid skill IDs (case-insensitive against the 18 `DefaultSkills` StringIds).
  - Valid companion archetype IDs (Plan 2 set: sergeant / field_medic / pathfinder / veteran / qm_officer / junior_officer).

### Phase B — C# wiring stack + Camp menu (commit `4e854b4`)

**7 new src files in `src/Features/Endeavors/`**
- `EndeavorTemplate.cs` (T10 partial) — POCO loaded fresh per session. Sibling `EndeavorCompanionSlot`. Not a save-class.
- `EndeavorCatalog.cs` (T10) — `LoadAll` runs from `EndeavorRunner.OnSessionLaunched` (NOT `OnGameStart` per AGENTS.md pitfall #18). Static `GetById` / `GetByCategory` / `All` / `Count`.
- `EndeavorGatingResolver.cs` (T11) — Plan 5 §4.2 hybrid gating. Returns `Resolution { IsAvailable, LockReason, DetailKey }`. `LockReason` enum surfaces tooltip routing reasons (`BelowTier` / `FlagGateMissing` / `FlagBlockerSet` / `SkillAndCompanionMissing` / `RequiredCompanionMissing`). `Required: true` companion slots make the companion side mandatory rather than alternative.
- `EndeavorPhaseProvider.cs` (T9) — `FirePhase(activity)` + `FireResolution(activity)`. Modeled on `HomeEveningMenuProvider`; routes through `ModalEventBuilder.FireEndeavorPhase`. Populates the standard six dialog tokens before every fire (`PLAYER_NAME` / `PLAYER_RANK` (culture-aware via `RankHelper`) / `LORD_NAME` / `PLAYER_TIER` / `COMPANION_NAME` / `COMPANION_FIRST_NAME`) plus `ENDEAVOR_TITLE`. Slot resolution populates `agent_<archetype>` slots from the assigned companions for storylets that want to relation-target them. Resolution selection by score: success ≥ template `ResolutionSuccessMinScore` (default +2), failure ≤ `ResolutionFailureMaxScore` (default −2), partial otherwise.
- `EndeavorRunner.cs` (T12) — `CampaignBehaviorBase` orchestrating lifecycle. Owns `_categoryLastResolved` `Dictionary<string, CampaignTime>` (per-category 3-day cooldown) persisted via `SaveLoadDiagnostics.SafeSyncData`. `OnSessionLaunched` calls `EndeavorCatalog.LoadAll()` + sets `Instance`. `OnHourlyTick` walks active endeavor, computes target phase index from `StartedOn + offsets`, fires any phases due via `EndeavorPhaseProvider.FirePhase`. Resolution fires when all phases done AND `duration_days * 24 + 1` grace hour elapsed. Public surface: `StartEndeavor(template, companions)` / `CancelEndeavor(reason)` / `CanStartCategory(category)` / `GetCategoryCooldownEnd(category)` / `GetSpawnedCompanions()`. **Time advances phases — option picks contribute score via the `endeavor_score_*` scripted effects but do not directly advance phase index.** If the player ignores a phase modal, that phase's score is zero.
- `ScrutinyRiskCalculator.cs` (T13) — §4.6 discovery roll. `effective_risk = baseRisk - 0.001 * (player.Charm + 0.5 * sum(companionCharm))`. Pure `ComputeEffectiveRisk` for tooltip use; `RollDiscovery` wraps with `MBRandom.RandomFloat`. Charm coefficients are constants — change there to retune.
- `EndeavorsMenuHandler.cs` (T8) — `CampaignBehaviorBase` registering `enlisted_endeavors` sub-menu. Status text rebuilt in init (active endeavor + `phase / total` + `available / total catalog`). Browse opens single `MultiSelectionInquiryData` of available endeavors with `[Category]` prefix in the display label per the locked single-inquiry pattern. Selecting an endeavor opens an assign-companions inquiry (filters spawned companions by archetype + skips already-assigned-elsewhere); skips entirely if no slots required and no matching companions present. Cancel option behind a confirmation inquiry.

**Two existing files modified**
- `src/Mod.Entry/SubModule.cs` — registers `EndeavorRunner` and `EndeavorsMenuHandler` alongside the other CK3 wanderer behaviors (after `CompanionLifecycleHandler`).
- `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` — `RegisterCampHubMenu` adds Camp menu slot 5 "Endeavors" between QM (slot 4) and Talk-to-companion (slot 6). Disabled with explanatory tooltip when no catalog loaded; tooltip switches when active endeavor exists. `using Enlisted.Features.Endeavors;` added.

**Csproj registrations (T7+T12)**
- Seven new `<Compile Include="src\Features\Endeavors\*.cs"/>` lines after the existing `EndeavorActivity.cs` line.

**Error-codes registry regenerated** — 131 Surfaced calls across 30 categories (was 115/30 at Plan 1 hand-off). New ENDEAVOR / CONTRACT category codes from the new files.

---

## §2 — Verification gates passed

- ✅ `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` — clean (0 warnings, 0 errors) on `feature/plan5-endeavor-system` HEAD (`4e854b4`).
- ✅ `python Tools/Validation/validate_content.py` — passes; pre-existing warnings + Phase 19 emits two soft warnings (endeavor_catalog.json + contract_archetypes.json not yet authored — expected at this point in the plan).
- ✅ `python Tools/Validation/generate_error_codes.py` — registry regenerated and committed in commit `4e854b4`.
- ✅ All new C# files normalized to CRLF via `Tools/normalize_crlf.ps1` (one-time per file).
- ✅ Activity backbone polymorphism compiles — `EndeavorActivity` and `ContractActivity` both override the five-method `Activity` abstract surface (`OnStart(ActivityContext)`, `Tick(bool)`, `OnPhaseEnter(Phase)`, `OnPhaseExit(Phase)`, `Finish(ActivityEndReason)`).
- ✅ Worktree cleanly committed; no pending changes; `git status` clean.

---

## §3 — Pending work for the next session (17 of 30 tasks)

The remaining tasks fall into four phases. Lock 10 (in the plan file) defines the sequencing.

### Phase C — Endeavor catalog data (T14-T18, ~30 templates)

- **T14 — Soldier templates (~6) + csproj**
  Author Soldier category in `ModuleData/Enlisted/Endeavors/endeavor_catalog.json`. ALSO wire the `Endeavors` data dir in `Enlisted.csproj`: three additions per CLAUDE.md project conventions (`<EndeavorsData Include="ModuleData\Enlisted\Endeavors\*.json"/>` ItemGroup + matching `<MakeDir>` + `<Copy>` in `AfterBuild`). Without these three, content silently doesn't deploy to the game install. Templates: drill_competition / teach_recruit / lord_notice_skirmish / physical_training / weapon_mastery / lead_patrol.
- **T15-T18 — Rogue / Medical / Scouting / Social templates (~5-6 each)** appended to the same `endeavor_catalog.json`. Templates are listed in the plan body §6 T15-T18; design guide §2 covers tone per category.

The Phase 19 validator catches schema violations on every template — author with the validator open as a tight feedback loop.

### Phase D — Endeavor storylets (T19-T23, ~50 storylets)

- **T19 — Soldier storylets (~12) — author in main thread as tone exemplar.** Schema follows the standard storylet shape (storylet-backbone.md). Each phase storylet is 2-3 options; each option's effects include `endeavor_score_plus_1` or `_minus_1` (or _plus_2 / _minus_2 for the most consequential picks) plus skill XP / lord relation / set_flag for choice memory (`endeavor_choice_<endeavor_id>_<phase>_<option_id>`). Resolution storylets are similarly shaped but emit larger payouts.
- **T20-T23 — Rogue / Medical / Scouting / Social storylets** dispatched to parallel subagents per Lock 10. Subagent prompt should include: (a) the design guide, (b) the shipped Soldier files as tone reference, (c) Phase 19 validator running as the schema gate. Each subagent owns one category file family.

### Phase E — Contracts (T24-T25)

- T24 ships `contract_archetypes.json` — 10 notable-issued contract templates. Same schema family as endeavors with three field swaps (see schema doc §8): `category` → `notable_type`, plus `payment_denars` and `notable_relation_min` fields.
- T25 ships ~15 contract phase storylets (some 1-phase, some 2-phase).

### Phase F — Integration + smoke + verification (T27 + T29-T30)

- **T26 deferred to Plan 7 polish** (stretch goal in plan §6).
- T27 — cross-system flag integration. Examples: `endeavor.rogue.dice_game` requires `ceremony_choice_t2_frugal` ; `endeavor.medical.poultice_recipes` flavored by `ceremony_choice_t4_<id>`. Author by editing template `flag_gates` / `flag_blockers` arrays after T20+T21 storylets ship. T4 + T7 ceremony option IDs must be grepped at task time per Lock 2 in the plan file.
- T29 — end-to-end smoke. Per Plans 1-3 precedent, this is the in-game manual smoke runbook handed to the human operator. Author the runbook as part of this report (§4 below is the seed).
- T30 — finalize this verification report (move from PARTIAL to DONE with smoke results filled in).

---

## §4 — Resume runbook (for the next session)

### Picking up the work

1. **Find the worktree.** `git worktree list` in the main repo shows
   `.worktrees/plan5-endeavor-system` on branch `feature/plan5-endeavor-system`.
   `cd` into that directory before doing anything.

2. **Verify the build.** `dotnet build -c 'Enlisted RETAIL' -p:Platform=x64` from
   the worktree should succeed in ~2s. The worktree has its own `packages/`
   directory (copied from the main repo at branch creation; if missing, `cp -r
   ../../packages packages`). If the build fails on missing
   `Microsoft.NETFramework.ReferenceAssemblies.net472.targets`, re-copy.

3. **Verify the validator state.** `python Tools/Validation/validate_content.py`
   exits 0 with two expected warnings about the absent
   `endeavor_catalog.json` + `contract_archetypes.json`. Any other endeavor
   warning means a regression.

4. **Read the plan in this order:**
   - **Locks 1-10 at the top** — these override the body where they conflict.
   - The schema doc + design guide before authoring content.
   - This verification report's §3 for the precise next-step bucket.

### Picking up at T14 (start of Phase C)

The plan body lists T14-T18 templates verbatim. Author them into a single new
file at `ModuleData/Enlisted/Endeavors/endeavor_catalog.json` with the schema
the design guide §10 worked example shows. Run the validator after each
category to confirm schema correctness. Wire the csproj entries listed at
CLAUDE.md project conventions section (three lines: ItemGroup + MakeDir +
Copy in AfterBuild). The validator's Phase 19 will start emitting concrete
errors instead of "file not authored" warnings the moment the file exists —
those errors are the ground truth.

### Picking up at T19 (start of Phase D, content authoring)

Author Soldier storylets (T19) in main thread as the tone exemplar. Then
dispatch four parallel subagents for T20-T23 (Rogue / Medical / Scouting /
Social), each with: the design guide path, the shipped Soldier storylet
files as references, and instructions to run `validate_content.py` before
declaring done. Use the standard storylet schema (see
`docs/Features/Content/storylet-backbone.md`) — these are NOT a new content
shape, just storylets that happen to be referenced by endeavor templates.

### Known gaps + watch-outs

- **Localization XML.** Phase B introduced ~25 `{=enlisted_endeavors_*}` loc
  keys via `TextObject(...)` calls in `EndeavorsMenuHandler.cs`. These rely
  on the inline-fallback mechanism (game falls back to the `Fallback` text
  when the key isn't in `enlisted_strings.xml`); zero runtime impact, but
  translators will complain. Plans 2+3 ran `sync_event_strings.py` for their
  storylet keys and merged the output XML into `enlisted_strings.xml`. Plan 5
  storylets (T19-T25) will need the same sync after they're authored. The
  C# menu keys would need a separate manual XML population pass; defer until
  the storylets are authored.
- **In-game smoke pending.** All Plans 1-3 left this as 🟡 "code shipped,
  in-game smoke pending human operator". Plan 5 follows the same pattern:
  T29 will produce the runbook, but a human must run the actual playtest
  scenarios.
- **Plan-vs-codebase drift discipline.** Plan 1's verification §5 caught 7
  divergences. Locks 1-10 in the plan file caught more. Stay vigilant per
  AGENTS.md pitfall #22 — verify prescribed APIs against `../Decompile/` +
  actual source as you go; append findings to the plan file's locks block.
- **Phase advancement model.** Per Lock 9, time advances phases —
  player option picks contribute score via the `endeavor_score_*` scripted
  effects but do NOT advance the phase index directly. Don't try to add a
  primitive that walks the activity from EffectExecutor; Plan §11 forbids
  new EffectExecutor primitives.

### Smoke test seed (for T29's runbook)

A draft of the smoke scenarios. The human operator runs these in-game once
all content ships:

1. **Discovery + start (Soldier path, T3).** Create new save → enlist with
   any lord → reach T3 → open Camp → confirm "Endeavors" appears at slot 5
   below QM and above Talk-to-companion. Tooltip says "no endeavor active".
   Click → enlisted_endeavors menu opens with status "No endeavor active.
   Endeavors available: <N> / <total>". Browse → MultiSelectionInquiry
   shows `[Soldier] Win the regimental drill competition` and similar.
2. **Run end-to-end.** Pick `endeavor.soldier.drill_competition` (no companion
   slot if Veteran not spawned yet). Confirm endeavor starts (info-msg
   "Endeavor begun:..."). Wait 1 in-game day. Confirm phase 1 modal fires.
   Pick brash option (+1 score). Wait 2 days. Confirm phase 2 modal fires.
   Pick smart option (+1 score). Wait 2 more days. Confirm resolution modal
   fires (success — score 2). Verify effects applied: `endeavor_active_score`
   reset to 0, lord relation drifted, skill XP applied.
3. **Save-load mid-endeavor.** Start a fresh endeavor; wait until phase 1 has
   fired but before phase 2. Save → reload → confirm `EndeavorActivity.Instance`
   non-null + `GetPhaseIndex() == 1` + companion locks preserved. Wait for
   phase 2 to fire. No regressions.
4. **Rogue scrutiny smoke.** Set up player with Roguery 80, no Charm
   companion. Pick `endeavor.rogue.dice_game`. Track `scrutiny` quality
   before + after each phase. Confirm increments on discovery roll
   (probabilistic — may need 2-3 runs). Repeat with a Charm-30 Field Medic
   assigned; confirm reduced increment frequency.
5. **Companion lock smoke.** Pick a Soldier endeavor with Veteran assigned.
   Open Camp → Endeavors → try to start a Soldier endeavor that also wants
   Veteran. Confirm Veteran is filtered out of the assignment inquiry
   (already locked).
6. **Cancel.** From an active endeavor's status menu, pick "Abandon current
   endeavor" → confirm inquiry → pick Abandon. Verify `EndeavorActivity.Instance`
   nulls, `endeavor_active_score` resets to 0, no category cooldown
   stamped (per `EndeavorRunner.CancelEndeavor` semantics).
7. **Category cooldown.** Complete a Soldier endeavor; immediately try to
   start another Soldier one. Confirm filtered out of available list.
   Wait 3 in-game days. Confirm available again.

---

## §5 — Plan-vs-codebase divergences caught (added inline by the executing session)

These prescribed APIs / shapes from the plan body were wrong against the
actual code; corrections shipped inline:

1. **T3 plan body prescribes `[SaveableField]` attribute + `OnStart()` /
   `OnTick()` / `OnComplete()` overrides.** Mod convention is `[Serializable]`
   class with public auto-properties; `Activity` abstract surface is
   `OnStart(ActivityContext)` / `Tick(bool)` / `OnPhaseEnter(Phase)` /
   `OnPhaseExit(Phase)` / `Finish(ActivityEndReason)`. Already caught by
   Plan 1's verification §5 fix #4; reaffirmed here.
2. **T3 plan body prescribes new fields `CurrentPhase`, `TotalPhases`,
   `Phase1ChoiceFlag`.** Per Lock 9, these would be redundant — phase index
   + total + score live in `AccumulatedOutcomes` under reserved
   `__-prefixed` keys; per-phase choice memory rides on `FlagStore` keys
   `endeavor_choice_<endeavor_id>_<phase>_<option_id>`. Plan 1's existing
   shape was kept verbatim.
3. **T5 plan body says "Replace Plan 1 T8 scaffolded stubs".** Plan 1 already
   shipped the real `IsAssignedToEndeavor` / `SetAssignedToEndeavor` /
   `SyncData` (commit `aa3ef16`). T5 collapsed to verification only.
4. **T9 plan body's `EndeavorPhaseProvider` example uses
   `StoryletCatalog.Instance?.Get`.** Actual API is `StoryletCatalog.GetById(...)`
   (static). Caught at Plan 1 verification §5 fix #2; reaffirmed here.
5. **`ActivityRuntime.Start` Surfaced-logs when an Activity has no resolved
   phases.** Plan body's Lock 9 implementation strategy left this gap —
   without a registered ActivityType, every endeavor start would emit a
   false-positive Surfaced log. Fix: ship two minimal ActivityType
   definitions (`activity_endeavor.json` + `activity_contract.json`) with
   one long-duration "running" phase + empty pool. Caught by advisor before
   Phase A commit.
6. **Schema doc §6 originally wrote "per-endeavor stored quality with id
   `endeavor_score_<endeavor_id>`".** Actual implementation uses single
   global `endeavor_active_score` quality. Fixed in Phase A.
7. **`EnlistedMenuBehavior.RegisterCampHubMenu` line range (plan claimed
   1345-1482).** Actual range on `development` is 1290-1417. Caught by Lock
   4 in the plan file pre-execution.
8. **Plan §6 T8 says `MultiSelectionInquiryData` "same pattern as Plan 2
   T20 talk-to" AND §1 says "Endeavor sub-menu listing all currently-available
   endeavors".** These imply different patterns (category-grouped sub-menu
   vs single inquiry across categories). Resolved at advisor checkpoint:
   single-inquiry pattern (§1 lock) with `[Category]` prefix in display
   label. Implemented in `EndeavorsMenuHandler.OnBrowseSelected`.
9. **`MBTextManager` namespace.** Mod's existing usage doesn't qualify the
   prefix; it lives in `TaleWorlds.Localization`, not `TaleWorlds.Core`.
   Caught at Phase B build-error pass.
10. **`CharacterImageIdentifier` namespace.** Lives in
    `TaleWorlds.Core.ImageIdentifiers`. Plan 2's Talk-to inquiry uses it
    without explicit `using` (transitive include from another using); my
    handler needed the explicit `using TaleWorlds.Core.ImageIdentifiers;`.
    Caught at Phase B build-error pass.
11. **`ActivityEndReason.Cancelled`.** Actual enum value is `PlayerCancelled`.
    Caught at Phase B build-error pass.

---

## §6 — Hand-off surface (Plans 6-7 and ongoing maintenance may use)

These public APIs ship in Phase B and are stable:

- `Enlisted.Features.Endeavors.EndeavorActivity.Instance` — `null` when no endeavor active. `EndeavorId` / `CategoryId` / `AssignedCompanionIds` / `StartedOn` / `AccumulatedOutcomes` (with reserved keys) / helpers `GetPhaseIndex`, `SetPhaseIndex`, `GetTotalPhases`, `SetTotalPhases`, `GetScore`, `AddScore`, `GetAssignedCompanions`.
- `Enlisted.Features.Contracts.ContractActivity.Instance` — sibling shape; adds `GetIssuingNotable()` and `IsPastDeadline()`.
- `Enlisted.Features.Endeavors.EndeavorRunner.Instance.StartEndeavor(template, companions)` — programmatic endeavor start (used by the menu; downstream plans may invoke for scripted starts).
- `EndeavorRunner.Instance.CancelEndeavor(reason)` — programmatic cancel.
- `EndeavorRunner.Instance.CanStartCategory(category)` / `GetCategoryCooldownEnd(category)` — cooldown query API.
- `Enlisted.Features.Endeavors.EndeavorCatalog.GetById(id)` / `GetByCategory(category)` / `All` / `Count` — catalog query API.
- `Enlisted.Features.Endeavors.EndeavorGatingResolver.IsAvailable(template, player, spawnedCompanions)` / `Resolve(...)` — hybrid gating resolver. `Resolution.LockReason` enum surfaces tooltip routing reasons.
- `Enlisted.Features.Endeavors.ScrutinyRiskCalculator.ComputeEffectiveRisk(baseRisk, player, agents)` / `RollDiscovery(...)` — pure risk math + roll wrapper.
- `Enlisted.Features.Endeavors.EndeavorPhaseProvider.FirePhase(activity)` / `FireResolution(activity)` / `PickResolutionStoryletId(template, score)` — modal-fire helpers.

`endeavor_active_score` quality is reserved for `EndeavorRunner` only; downstream plans should not write to it (`EndeavorRunner` resets it on every start + finish, so any external writes would be clobbered).

---

## §7 — References

- [Plan 5 — CK3 Wanderer Endeavor System](2026-04-24-ck3-wanderer-endeavor-system.md) — owning plan with locks 1-10 at the top.
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md) — locked decisions Plan 5 inherits.
- [Endeavor catalog schema](../../Features/Endeavors/endeavor-catalog-schema.md) — JSON shape reference.
- [Endeavor design guide](../../Features/Endeavors/endeavor-design-guide.md) — what to put in each field.
- [Storylet backbone reference](../../Features/Content/storylet-backbone.md) — base storylet schema for phase + resolution storylets.
- [Companion archetype catalog](../../Features/Companions/companion-archetype-catalog.md) — Plan 2 archetype IDs.
- [AGENTS.md](../../../AGENTS.md) — Critical Rule #10 (StoryDirector routing), Pitfall #14 (scripted-effect IDs registered), #18 (catalog init after OnGameStart), #22 (plan-vs-codebase drift), #23 (token interpolation contract).
- [CLAUDE.md](../../../CLAUDE.md) — project conventions (csproj wildcards, CRLF, error-codes regen).
