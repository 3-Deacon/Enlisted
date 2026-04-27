# Plan 5 — CK3 Wanderer Endeavor System: Verification Report

**Status:** 🟡 **Code-level + content-level complete (29 of 30 tasks); in-game smoke pending human operator (T29 runbook in §4 below).** Plan 5 ships the full endeavor system end-to-end on a feature branch. T26 deferred to Plan 7 polish per stretch flag in plan §6.

**Plan:** [2026-04-24-ck3-wanderer-endeavor-system.md](2026-04-24-ck3-wanderer-endeavor-system.md)
**Brief:** [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md)
**Branch:** `feature/plan5-endeavor-system` (in worktree `.worktrees/plan5-endeavor-system/`)
**Commits on the branch (newest first):**
- `de1a840` — `feat(endeavors): Plan 5 Phase C+D — endeavor catalog + 94 storylets + contracts` (T14-T25)
- `95e63e8` — `docs(plan5): Phase A + B hand-off — verification doc + cross-doc updates`
- `4e854b4` — `feat(endeavors): Plan 5 Phase B — C# wiring stack + Camp menu slot 5` (T7-T13)
- `81fa6de` — `feat(endeavors): Plan 5 Phase A — substrate + effects + Phase 19 validator` (T1-T6 + T28)

**Date:** 2026-04-26 (last update)

---

## §1 — What shipped (29 of 30 tasks: T1-T25 + T27-T28; T26 deferred; T29 = runbook in §4; T30 = this report)

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

### Phase C — Endeavor catalog data (commit `de1a840`, T14-T18)

**`ModuleData/Enlisted/Endeavors/endeavor_catalog.json`** — 27 endeavor templates across 5 categories:
- Soldier (6): drill_competition, teach_recruit, lord_notice_skirmish, physical_training, weapon_mastery, lead_patrol
- Rogue (6): dice_game, smuggle_wine, pickpocket, black_market, gambling_ring, bribe_officer
- Medical (5): forage_herbs, tend_sick, poultice_recipes, field_surgery, disease_outbreak
- Scouting (5): map_terrain, track_patrol, report_morale, deep_recon, escape_routes
- Social (5): befriend_sergeant, sway_lord, court_camp_follower, influence_companion, peer_officer_alliance

All resolutions point to category-shared `endeavor_<category>_resolution_<bucket>_generic` storylets (Plan 7 polish can swap to per-template unique resolutions where differentiation matters).

**`Enlisted.csproj`** — new `<EndeavorsData Include="ModuleData\Enlisted\Endeavors\*.json"/>` ItemGroup + matching `<MakeDir>` + `<Copy>` in `AfterBuild` per CLAUDE.md project conventions.

### Phase D — Endeavor + contract storylets (commit `de1a840`, T19-T25)

Authored via 6 parallel subagents — one per category plus contracts. Each subagent operated against the shared schema doc + design guide + Phase 19 validator running as the schema gate. **94 storylets across 7 new files:**

| File | Storylets | Phase / Resolution split |
| :-- | :-: | :-- |
| `endeavor_soldier.json` | 15 | 12 phase + 3 generic resolutions |
| `endeavor_rogue.json` | 16 | 13 phase + 3 generic resolutions |
| `endeavor_medical.json` | 14 | 11 phase + 3 generic resolutions |
| `endeavor_scouting.json` | 14 | 11 phase + 3 generic resolutions |
| `endeavor_social.json` | 16 | 13 phase + 3 generic resolutions |
| `contract_archetypes.json` (storylets file) | 19 | 16 phase + 3 generic resolutions |

**`ModuleData/Enlisted/Endeavors/contract_archetypes.json`** — 10 contract templates across 6 notable types (headman 2, merchant 3, gangleader 2, artisan 1, rural_notable 1, urban_notable 1).

### Phase 19 validator + schema doc cleanup (commit `de1a840`, fold-in)

- Phase 19 category-coherence rule loosened from "rogue-only scrutiny" to "rogue + scouting may carry scrutiny" — aligns with design guide §2.4 which explicitly allows mild Scouting risk on infiltration templates.
- `endeavor.scouting.track_patrol` `scrutiny_risk_per_phase` bumped 0.0 → 0.05 to align template with the storylets that ship scrutiny effects.
- Schema doc §6 worked example updated: replaced stale `endeavor_set_choice_flag` + `endeavor_set_score` references (never registered as scripted effects) with the actual shipped pattern (inline `set_flag` + `endeavor_score_plus_1` etc.). New paragraph documents the 13 registered skill-XP wrappers and notes that uncommon skill axes route through the closest registered wrapper.

### Phase E — Cross-system flag wiring (commit will follow this report, T27)

- `endeavor.rogue.dice_game` — `flag_blockers: ["ceremony_choice_t2_frugal"]`. A player who picked the disciplined option at the T2 ceremony cannot run dice in camp; the storylet is filtered out of the available list.
- `endeavor.social.court_camp_follower` — `flag_gates: ["ceremony_choice_t2_generous"]`. Available only to players whose T2 ceremony pick read as generous (the camp followers see them as a means).

These two demonstrate the cross-system gating mechanism; downstream content can layer more `flag_gates` / `flag_blockers` against `ceremony_choice_t<N>_<id>` flags or any other `FlagStore` boolean. Per-storylet flavor variants conditional on ceremony picks are deferred to Plan 7 polish (would require authoring 3-4 variant-storylets per gated phase).

---

## §2 — Verification gates passed

- ✅ `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` — clean (0 warnings, 0 errors) on `feature/plan5-endeavor-system` HEAD.
- ✅ `python Tools/Validation/validate_content.py` — passes; "OK: 27 endeavor template(s) and 10 contract template(s) validated." 0 errors. 64 warnings, all pre-existing or loc-key-fallback (translator-only, no runtime impact).
- ✅ `python Tools/Validation/generate_error_codes.py` — registry regenerated at Phase B (commit `4e854b4`); no new Surfaced calls in Phase C+D.
- ✅ All new C# files normalized to CRLF via `Tools/normalize_crlf.ps1` (one-time per file).
- ✅ Activity backbone polymorphism compiles — `EndeavorActivity` and `ContractActivity` both override the five-method `Activity` abstract surface (`OnStart(ActivityContext)`, `Tick(bool)`, `OnPhaseEnter(Phase)`, `OnPhaseExit(Phase)`, `Finish(ActivityEndReason)`).
- ✅ All 27 endeavor templates' `phase_pool` + `resolution_storylets` references resolve in the storylet catalog. All 10 contract templates' refs resolve.
- ✅ All scripted-effect IDs referenced in storylet options exist in `scripted_effects.json` (Phase 12 validator).
- ✅ Phase 19 category-scrutiny coherence: Soldier / Medical / Social all `scrutiny_risk_per_phase: 0.0`; Rogue uses 0.05-0.20; Scouting uses 0.05-0.10 on infiltration templates only.
- ⏳ In-game smoke pending — runbook in §4 below.

---

## §3 — Pending work (1 of 30 tasks: T29 in-game smoke)

**T29 — End-to-end smoke test** is the only remaining work. It is **handed to the human operator** per Plans 1-3 precedent — code-level work cannot validate save-load round-trip, modal queue stacking under real time-control speeds, or the player-experience feel of the storylet prose. The runbook is in §4 below.

After the human operator runs the smoke scenarios, they update this verification report's status from 🟡 to ✅ (or file follow-up bugs). The branch then becomes mergeable to `development`.

**T26 — Endeavor sub-menu integration with companion talk-to** is **deferred to Plan 7 polish** per the plan §6 stretch flag. The companion talk-to surface (Plan 2) does not currently surface "Help me with [endeavor]" options when the player has an active endeavor; adding that requires augmenting `EnlistedDialogManager`'s companion conversation flow with a check against `EndeavorActivity.Instance.GetAssignedCompanions()`. Low player-impact in v1; clean Plan 7 enhancement.

---

## §4 — In-game smoke runbook (T29)

This is the canonical T29 deliverable: the smoke scenarios the human operator
runs in-game to validate Plan 5 end-to-end. The branch is mergeable to
`development` once the scenarios pass.

### Pre-flight

1. **Branch state.** Confirm `feature/plan5-endeavor-system` is checked out
   (worktree at `.worktrees/plan5-endeavor-system/`). HEAD should be the
   latest Phase F commit (after Phase C+D + this report were written).
2. **Build + deploy.** `dotnet build -c 'Enlisted RETAIL' -p:Platform=x64`
   from the worktree. Post-build mirror should land `Enlisted.dll` in the
   game install. Close BannerlordLauncher first (CLAUDE.md known footgun).
3. **Validator green.** `python Tools/Validation/validate_content.py`
   reports "OK: 27 endeavor template(s) and 10 contract template(s)
   validated." 0 errors. Warnings are loc-key fallbacks (translator-only,
   not blocking).
4. **Native logs primed.** Native Bannerlord watchdog at
   `C:\ProgramData\Mount and Blade II Bannerlord\logs\` should be empty or
   stale. Mod session log at
   `Modules\Enlisted\Debugging\Session-A_<date>.log` will be created on
   game launch.

### Known gaps + watch-outs (read before running)

- **Localization XML not synced.** Phase B introduced ~25
  `{=enlisted_endeavors_*}` loc keys via `TextObject(...)` calls in
  `EndeavorsMenuHandler.cs`. Phase D introduced ~250 `{=endeavor_*}` /
  `{=contract_*}` storylet keys. None integrated into
  `enlisted_strings.xml`. The game falls back to the inline `Fallback`
  text — zero runtime impact, just no translation. Plan 7 polish can run
  `sync_event_strings.py` and integrate.
- **`{NOTABLE_NAME}` token unwired.** Contract storylets reference notable
  heroes by literal first name in setup prose (Headman Eadric, Merchant
  Reza, etc.) rather than via a token. Real notable hero names won't
  appear in v1; Plan 7 polish item.
- **No probabilistic scrutiny rolls.** Plan §4.6 envisioned a per-phase
  `MBRandom.RandomFloat` discovery roll based on baseRisk minus Charm
  contributions. Phase D ships deterministic scrutiny — each Rogue option
  has its scrutiny effect baked in (brash adds `_minor` to `_major`,
  clever adds `_minor` or none, bail-out adds none). `ScrutinyRiskCalculator`
  exists but is not currently called by `EndeavorPhaseProvider`.
  Tooltip-time integration is a Plan 7 polish item.
- **Resolution prose generic.** All 5 categories use 3 generic resolutions
  shared across their templates. The same "you took third place" prose
  fires whether the player ran drill_competition or weapon_mastery.
  Per-template marquee resolutions are a Plan 7 polish item if
  differentiation matters in playtest.

### Smoke scenarios

The human operator runs these in-game. Each scenario marks PASS / FAIL /
BLOCKED and updates the table at §6 below.

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
12. **Phase 19 category-scrutiny coherence too strict.** Initial validator
    enforced "non-rogue category requires `scrutiny_risk_per_phase=0.0`",
    but design guide §2.4 explicitly allows mild Scouting scrutiny on
    infiltration templates (track_patrol, deep_recon). Loosened validator
    to allow Rogue + Scouting scrutiny; Soldier / Medical / Social still
    enforced to 0.0. Caught when the Rogue subagent flagged the
    `endeavor.scouting.deep_recon` 0.10 scrutiny value as a Phase 19 error.
13. **Skill-XP wrapper coverage gap.** T6 catalog ships closed wrappers
    for the 13 most-used (skill, size) pairs (combat / athletics /
    leadership / roguery / charm / medicine / scouting). Plan 5 templates
    accept a wider skill_axis vocabulary (TwoHanded, Polearm, Bow,
    Crossbow, Throwing, Riding, Steward, Tactics) for hybrid gating, but
    no closed wrappers exist for those axes. Subagents routed all combat
    XP through `_major_combat` (OneHanded under the hood), Riding/Bow
    through `_athletics`, Steward through `_minor_charm`. Catalog-level
    skill-axis variety preserved for gating; storylet-level XP routing
    collapsed to the available palette. Plan 7 polish can add
    axis-specific wrappers if XP attribution per skill matters in
    playtest.
14. **Score primitive only ships ±1 / ±2.** No "+0" score effect exists;
    design guide §2.1 mentions "+0 score" for show-off options that are
    neutral on score but flavor relations. Subagents collapsed neutral
    options to ±1 (small effect either direction). Acceptable; the score
    distribution still reaches all three resolutions.
15. **`endeavor_set_choice_flag` + `endeavor_set_score` stale doc refs.**
    Original schema doc §6 example referenced these names as if they
    were registered scripted effects; they never were. Phase 5
    implementation uses inline `set_flag` for choice memory and the
    `endeavor_score_plus_1` / etc. wrappers for score. Schema doc fixed
    in commit `de1a840`.
16. **`{NOTABLE_NAME}` token not wired.** Contract storylets reference
    notable heroes by name; the subagent flavored them with literal
    first names (Headman Eadric, Merchant Reza, Gangleader Vask, etc.)
    rather than via a token. Plan 7 polish item: wire `{NOTABLE_NAME}`
    via a SetTextVariable call in `EndeavorPhaseProvider` reading from
    `ContractActivity.Instance?.GetIssuingNotable()?.Name`.
17. **Resolution storylet-sharing model.** Plan body §1 estimated "~50
    storylets across 5 categories"; per-template unique resolutions
    would have produced 6 templates × 3 resolutions × 5 categories ≈ 90
    storylets just for resolutions. Implementation collapses to 3
    category-shared generic resolutions per category (15 resolution
    storylets total + 75 phase storylets ≈ 90 storylets total per the
    plan estimate, with the breakdown different from what the plan body
    implied). Per-template marquee resolutions are a Plan 7 polish item.

---

## §5b — Subagent dispatch results (Phase D, T19-T25)

Phase D content was authored via 6 parallel subagents launched from a single message after Phase C templates landed. Each subagent received the schema reference, design guide pointer, full effect menu (closed list), per-category template ID list (extracted from the catalog), token interpolation contract, and the validator command as the schema gate.

| Subagent | Task | Storylet count | Validator residuals | Notes |
| :-- | :-- | :-: | :-: | :-- |
| T19 Soldier | endeavor_soldier.json | 15 | 0 | Flagged stale schema doc §6 refs (#15 above) + skill axis collapse (#13). |
| T20 Rogue | endeavor_rogue.json | 16 | 0 | Caught the Scouting `deep_recon` scrutiny coherence bug (#12). Per-storylet scrutiny effect placement summary in subagent report. |
| T21 Medical | endeavor_medical.json | 14 | 0 | No deviations. `disease_outbreak` 3-phase escalation tuned (phase 3 uses ±2 score). |
| T22 Scouting | endeavor_scouting.json | 14 | 0 | Reaffirmed deep_recon catalog bug. Tooltip XP values corrected to actual numbers (25 / 75 not the doc example "50"). |
| T23 Social | endeavor_social.json | 16 | 0 | Generic resolutions omit companion tokens (templates without companion slots would render literal `{COMPANION_FIRST_NAME}`). Hild named for court_camp_follower. Minor narrative seam in `sway_lord_phase2` flagged. |
| T24+T25 Contracts | contract_archetypes.json (catalog + storylets) | 10 templates + 19 storylets | 0 | Single-phase contracts use ±2 deltas to keep resolutions reachable. Notable names literal (Plan 7 token wiring). |

Total dispatched: 6 subagents in parallel. Wall-clock time: ~7-8 minutes for the slowest (Contracts at 8m16s). All returned without retry.

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
