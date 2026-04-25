# Plan 1 — CK3 Wanderer Architecture Foundation: Verification Report

**Status:** 🟡 Code-level verification complete; in-game manual smoke pending human operator.

**Plan:** [2026-04-24-ck3-wanderer-architecture-foundation.md](2026-04-24-ck3-wanderer-architecture-foundation.md)
**Brief:** [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md)
**Date:** 2026-04-25

---

## §1 — What shipped

### New files (12)

**Architecture brief + verification (2)**
- `docs/architecture/ck3-wanderer-architecture-brief.md` — locked contract for Plans 2-7
- `docs/superpowers/plans/2026-04-24-ck3-wanderer-architecture-foundation-verification.md` — this report

**Substrate classes (10)**
- `src/Features/Patrons/FavorKind.cs` — enum (`None = 0` only; Plan 6 expands)
- `src/Features/Patrons/PatronEntry.cs` — POCO save-class (offset 55)
- `src/Features/Patrons/PatronRoll.cs` — POCO save-class with `Instance` (offset 54)
- `src/Features/Patrons/PatronRollBehavior.cs` — hosting `CampaignBehaviorBase`
- `src/Features/Endeavors/EndeavorActivity.cs` — `Activity` subclass (offset 57)
- `src/Features/Contracts/ContractActivity.cs` — `Activity` subclass (offset 56)
- `src/Features/Lifestyles/LifestyleUnlockStore.cs` — POCO save-class with `Instance` (offset 58)
- `src/Features/Lifestyles/LifestyleUnlockBehavior.cs` — hosting `CampaignBehaviorBase`
- `src/Features/Ceremonies/RankCeremonyBehavior.cs` — `OnTierChanged` listener (log-only body; Plan 3 fills)
- `src/Features/PersonalKit/PersonalKitTickHandler.cs` — hourly + daily tick seam (no-op body; Plan 7 fills)
- `src/Mod.Core/Helpers/ModalEventBuilder.cs` — canonical Storylet → BuildModal → EmitCandidate helper

### Edits (4)

- `AGENTS.md` — Rule #11 amended: offset cluster 45-60 → 45-70, with cross-link to brief and explicit 51-70 sub-reservation for the wanderer plan family
- `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` — class offsets 54/55/56/57/58, enum offset 84, generic containers `List<PatronEntry>` + `List<MBGUID>`
- `src/Mod.Entry/SubModule.cs` — registers `PatronRollBehavior`, `LifestyleUnlockBehavior`, `RankCeremonyBehavior`, `PersonalKitTickHandler`
- `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:188-193` — stay-back gate fix (T7+ tier guard removed; Stay-Back toggle now honoured at every tier)
- `src/Features/Retinue/Core/CompanionAssignmentManager.cs` — added `_companionEndeavorAssignment` dictionary + `IsAssignedToEndeavor` / `SetAssignedToEndeavor` accessors + `SyncData` persistence + `ClearAllSettings` extension
- `Enlisted.csproj` — `<Compile Include>` lines for the 11 new `.cs` files (10 substrate + ModalEventBuilder)
- `docs/error-codes.md` — regenerated; 115 Surfaced calls across 30 categories (was 114 across 30); added one new code (`CEREMONY tier_changed` is `Expected`, not `Surfaced`, so no registry entry — `MODAL` codes from `ModalEventBuilder` are `Expected` too)

### Save-definer offset claims (Brief §1)

| Offset | Owner | Notes |
| :---: | :--- | :--- |
| Class 54 | `PatronRoll` | claimed |
| Class 55 | `PatronEntry` | claimed |
| Class 56 | `ContractActivity` | claimed |
| Class 57 | `EndeavorActivity` | claimed |
| Class 58 | `LifestyleUnlockStore` | claimed |
| Class 53 | reserved | Personal Kit state lives in `QualityStore` — no class |
| Class 59 | reserved | Rank Ceremony state lives in `FlagStore` — no class |
| Class 51-52 | reserved | menu+duty unification spec |
| Class 60-70 | reserved | future surface specs (Specs 3-5) |
| Enum 84 | `FavorKind` | claimed |

---

## §2 — Verification gates passed

- ✅ `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` — clean (0 warnings, 0 errors)
- ✅ `python Tools/Validation/validate_content.py` — passes; 30 pre-existing warnings, 0 new errors
- ✅ `python Tools/Validation/generate_error_codes.py` — registry regenerated; 115 Surfaced call sites across 30 categories; staged
- ✅ All new C# files normalized to CRLF via `Tools/normalize_crlf.ps1`
- ✅ `EnlistedFormationAssignmentBehavior.cs` line shifts handled; error-codes registry regenerated post-edit (Phase 10 still passes)
- ✅ `EndeavorActivity` + `ContractActivity` override the actual five-method `Activity` abstract surface (`OnStart(ActivityContext)`, `Tick(bool)`, `OnPhaseEnter(Phase)`, `OnPhaseExit(Phase)`, `Finish(ActivityEndReason)`) — verified by reading `src/Features/Activities/Activity.cs:45-49` and `OrderActivity.cs:33-56`
- ✅ Class offset cross-check against `Decompile/TaleWorlds.Core/SaveableCoreTypeDefiner.cs` and `Decompile/TaleWorlds.CampaignSystem/SaveableCampaignTypeDefiner.cs` — none of `PatronRoll` / `PatronEntry` / `ContractActivity` / `EndeavorActivity` / `LifestyleUnlockStore` / `FavorKind` collide with vanilla type names
- ✅ Existing csproj `<DialogueData>` glob already deploys `companion_*.json` — no csproj changes needed for Plan 2's dialog catalogs
- ✅ `OnTierChanged` static event verified at `EnlistmentBehavior.cs:8490` and confirmed fired from all three T6→T7 promotion paths (auto proving-event, decline-then-dialog, dialog-request) at `EnlistmentBehavior.cs:9799` per `SetTier`

## §3 — Pending: in-game manual smoke

The build + validator gates can't cover save-load round-trip behavior or runtime registration. A human operator must:

1. **Fresh save round-trip (4×).** Start a new save (any faction). Save → reload → save → reload. Native watchdog at `C:\ProgramData\Mount and Blade II Bannerlord\logs\watchdog_log_<pid>.txt` must be clean. Mod session log at `Modules\Enlisted\Debugging\Session-A_<date>.log` must show the four new behaviors registering on `OnSessionLaunched` (`PatronRollBehavior`, `LifestyleUnlockBehavior`, `RankCeremonyBehavior`, `PersonalKitTickHandler`).
2. **`OnTierChanged` ceremony seam.** Use Debug Tools to force a tier change. Confirm `CEREMONY tier_changed` info-line appears in the session log with both previous and new tier values reflected (the message body is static; tier values appear in the surrounding log context).
3. **Stay-back gate fix at low tier.** Recruit a companion at T1-T3 (any path that puts them in MainParty before T7). Open the Camp companions menu, set them to "Stay Back". Trigger combat (engage a bandit party). Confirm the companion does NOT spawn as an agent. Toggle to "Fight", trigger another battle, confirm they DO spawn. Repeat at T3 to confirm tier-wide behaviour.
4. **Pre-Plan-1 save load.** Load a save produced before this commit. Confirm `EnsureInitialized()` reseats `PatronRoll.Entries` and `LifestyleUnlockStore.UnlockedFeatures` to empty lists; no NRE, no save corruption, no native watchdog crash.

Pending tasks T1-T15 close on completion of the smoke; nothing further from this plan.

---

## §4 — Deviations from plan as written

The plan v2 prescribes 15 sequential tasks. Several were collapsed or skipped during execution; deviations are intentional and reflect the user's "no placeholders" directive (no files-to-be-deleted, no stubs that ship empty bodies solely to be filled by downstream plans when the same file edit can land the final shape now).

| Plan task | Status | Note |
| :--- | :--- | :--- |
| T1 — brief + AGENTS.md amendment | shipped | §4.1 enum-ledger paragraph corrected (84 free; 80-83 + 85-103 taken; 104+ free); offset table flattened (no "RECLAIMED" hedges); `ModLogger.Expected` recipe code corrected (plan's `new { storyletId }` ctx doesn't compile — see §5 below) |
| T2 — register class offsets + create shells | collapsed into one phase with T9/T10/T11/T12/T13 | Single-pass file creation; no placeholder shells. Offsets 53 + 59 reserved-in-comment, no `RankCeremonyState.cs` / `PersonalKitStore.cs` ever created |
| T3 — register `List<PatronEntry>` container | folded into T2 phase | Also added `List<MBGUID>` for `EndeavorActivity.AssignedCompanionIds` |
| T4 — register `FavorKind` enum at offset 84 | folded into T2 phase | `None = 0` stub; Plan 6 expands |
| T5 — `ModalEventBuilder` helper + test storylet + Debug Tools menu | helper shipped; test storylet + Debug Tools entry skipped | Test storylet is a placeholder (exists solely to verify the helper works); plan-3 ceremony fire is the first real exercise. API calls corrected (`StoryletCatalog.GetById` is static, not on `Instance`) |
| T6 — stay-back gate fix at line 188-193 | shipped | `EnlistmentTier < CommanderTier1` guard removed; `IsEnlisted` check preserved; comment updated to explain tier-wide rationale |
| T7 — companion dialog schema doc + loader + stub catalog + csproj entries | collapsed entirely | Existing `<DialogueData>` glob already deploys `companion_*.json`; brief §4 rule 4 is the schema contract; Plan 2 ships the `CompanionDialogueCatalog` loader (Plan 2's first task is the right place for it because Plan 2 defines the companion-specific context-field set) |
| T8 — `CompanionAssignmentManager` extension | shipped | `_companionEndeavorAssignment` dict + accessors + `SyncData` persistence + `ClearAllSettings` extension |
| T9 — `PatronRoll` + `PatronEntry` final shape | folded into T2 phase | `PatronRoll` + hosting `PatronRollBehavior` shipped per existing mod convention (POCO save-class + hosting `CampaignBehaviorBase` mirrors `FlagStore` + `FlagBehavior` precedent — plan's "PatronRoll IS the CampaignBehavior" prescription doesn't match the codebase) |
| T10 — `EndeavorActivity` + `ContractActivity` final shape | folded into T2 phase | Five-method `Activity` overrides — plan's `OnStart()` / `OnTick()` / `OnComplete()` shape doesn't compile against the actual abstract surface |
| T11 — `RankCeremonyBehavior` directly (FlagStore route) | folded into T2 phase | No `RankCeremonyState.cs`; `OnTierChanged` listener with log-only body; Plan 3 fires ceremony storylet via `ModalEventBuilder.FireCeremony`; choice memory will live in `FlagStore` keys `ceremony_fired_t{N}` (flat namespace per existing `FlagStore` precedent — plan's `ceremony.fired.t{N}` dotted form is inconsistent) |
| T12 — `LifestyleUnlockStore` final shape | folded into T2 phase | POCO + hosting `LifestyleUnlockBehavior` per the mod's Store + Behavior convention |
| T13 — `PersonalKitTickHandler` directly (QualityStore route) | folded into T2 phase | No `PersonalKitStore.cs`; tick handler with no-op body wired to hourly + daily; Plan 7 reads `QualityStore` `kit_*` keys (flat namespace; brief schema-rule 6 codifies) |
| T14 — validator phase 18-20 stubs | skipped | Empty validator phases that just print "passing" are placeholders by design; the validator is at Phase 15 today and new phases ship with the plan that needs them (Plan 2 / 3 / 5) |
| T15 — final smoke + verification report | this report | Manual smoke pending human operator (§3 above) |

## §5 — Plan-vs-codebase divergences caught

These prescribed APIs / patterns from the plan were wrong against the actual code; corrections shipped inline:

1. `ModLogger.Expected("CAT", "key", "summary", new { x, y })` — plan's anonymous-object ctx doesn't compile. Signature is `(string, string, string, IDictionary<string, object> = null)`. Existing call sites all drop the ctx; followed suit.
2. `StoryletCatalog.Instance?.GetById(...)` — plan's instance-call form. Actual API is `StoryletCatalog.GetById(...)` (static).
3. Plan's `EndeavorActivity` stub overrides `OnStart()` / `OnTick()` / `OnComplete()` — wrong signatures. `Activity` is abstract with `OnStart(ActivityContext)`, `Tick(bool)`, `OnPhaseEnter(Phase)`, `OnPhaseExit(Phase)`, `Finish(ActivityEndReason)` (5 methods, not 3).
4. Plan's `[SaveableField(N)] public Type Field;` style is not the mod's convention — `FlagStore`, `QualityStore`, `NamedOrderState`, `OrderActivity` all use plain public auto-properties on `[Serializable]` classes. Followed the mod convention.
5. Plan's "PatronRoll : CampaignBehaviorBase" prescription doesn't match the codebase pattern of POCO save-class + hosting `CampaignBehaviorBase`. Followed the `FlagStore` + `FlagBehavior` precedent.
6. Plan's `ceremony.fired.t{N}` dotted flag-key form is inconsistent with the existing `FlagStore` vocabulary (`prior_service_culture_*`, `path_resisted_*`). Brief schema-rule 6 codifies the flat-underscore convention.
7. Plan's §4.1 enum-ledger paragraph claimed "85+ free." Actual: 85 = `DutyProfileId`; 86-103 = Campaign Intelligence + Signal Projection. 104+ free.

These corrections are also reflected in the architecture brief, so Plans 2-7 inherit the corrected contract.

---

## §6 — Plans 2-7 unblocked

All locked decisions in the brief are in code. Plans 2-7 can begin parallel implementation against the registered substrate:

- **Plan 2 (Companion Substrate)** — companion archetype spawn + `CompanionDialogueCatalog` loader + talk-to sub-menu. Reads `IsAssignedToEndeavor` from the extended `CompanionAssignmentManager`. Stay-back gate fix is in place at every tier.
- **Plan 3 (Rank-Ceremony Arc)** — fires storylets via `ModalEventBuilder.FireCeremony` from `RankCeremonyBehavior.OnTierChanged`. `FlagStore` keys `ceremony_fired_t{N}` for dedup, `ceremony_choice_t{N}` for choice memory.
- **Plan 4 (Officer Trajectory)** — registers `ItemModifier` instances at session-launch with deterministic StringIds (per spec §3.7); patches `DefaultPartyHealingModel`. No new save offset.
- **Plan 5 (Endeavor System)** — populates `EndeavorActivity` phase logic, ships `EndeavorPhaseProvider` modeled on `HomeEveningMenuProvider`, fires phase events via `ModalEventBuilder.FireEndeavorPhase(..., ownerActivity)`. Reads/writes `CompanionAssignmentManager.IsAssignedToEndeavor`.
- **Plan 6 (Roll of Patrons)** — populates `PatronRollBehavior` discharge / `OnHeroKilled` / favor-grant pipeline; expands `FavorKind` enum members; ships favor-outcome storylets fired via `ModalEventBuilder.FireDecisionOutcome`.
- **Plan 7 (Personal Kit + Lifestyle Unlocks + Polish)** — populates `PersonalKitTickHandler` to read `QualityStore` `kit_*` keys; QM dialog branches for upgrade purchase; `LifestyleUnlockBehavior` milestone hooks; final tuning + smoke playtest.

No save-compat retrofit risk — all offsets reserved for Plans 2-7 are claimed up-front by Plan 1.

---

## §7 — Post-execution audit: cross-system conflicts + stale references

A targeted sweep after the substrate landed surfaced two real action items (both addressed in this commit) and one design conflict to flag for Plan 3.

### Resolved in this commit

1. **`SaveLoadDiagnostics.SafeSyncData` convention.** `PatronRollBehavior` and `LifestyleUnlockBehavior` originally used a plain `try/catch + reset-and-continue` pattern mirrored from `FlagBehavior`. The dominant convention in the codebase (~13 behaviors, including `CompanionAssignmentManager.cs:38-45`) is `SaveLoadDiagnostics.SafeSyncData(this, dataStore, () => { ... })` which logs with the behavior's full type name and **rethrows** rather than swallowing. Refactored both new behaviors. Rationale: silent reset on save corruption hides regressions; explicit rethrow surfaces them. `FlagBehavior` precedent stays as-is (older pattern; not a Plan 1 target).

2. **`AGENTS.md` Rule #11 + architecture brief offset-cluster ownership.** Original wording reserved offsets 51-70 for "the CK3 wanderer mechanics cluster (Plans 1-7) plus future surface specs (Specs 3-5)" but offsets 51-52 belong to the **menu+duty unification spec** (a parallel plan family, not Plans 1-7 of the wanderer brief). Both AGENTS.md and the architecture brief now name all three claimants of the 51-70 cluster (menu+duty 51-52, wanderer 54-58 + enum 84, future surface specs 60-70). Class offsets 53 + 59 reserved-no-class within the cluster.

### Flagged for Plan 3 brainstorm — not a Plan 1 fix

3. **`OnTierChanged` consumer collision at T4 / T6 / T9.** The mod has two pre-existing `OnTierChanged` subscribers:
   - `src/Features/Activities/Orders/PathScorer.cs` — accumulates path-specialization scores; doesn't emit modals; safe to compose.
   - `src/Features/CampaignIntelligence/Career/PathCrossroadsBehavior.cs` — emits a Modal `path_crossroads_{path}_t{tier}` storylet **at T4 / T6 / T9**.
   
   Plan 1's `RankCeremonyBehavior` is the third subscriber, registered after the two above (so its handler fires last). Plan 3's prescribed scope (8 ceremony storylets, T1→T2 through T8→T9) overlaps with PathCrossroads at three tiers — without coordination, the player would see two Modals back-to-back at T4, T6, T9. Plan 3's brainstorm must pick one of: (a) skip ceremonies at T4/T6/T9 and cover them via PathCrossroads alone; (b) chain ceremony as a follow-up to the crossroads commit/resist outcome; (c) emit a non-Modal news entry instead of a Modal at the colliding tiers. The brief §6 captures the options.

### Non-issues confirmed

4. **Storylet token prefixes (`endeavor_`, `ceremony_`, `companion_<archetype>_`, `patron_`, `lifestyle_`)** — no collisions found in `ModuleData/Enlisted/`. Namespace clear for Plans 2-7.

5. **`OnDischarged` event signature** for Plan 6's discharge hook. Existing event at `EnlistmentBehavior.cs:8438` is `Action<string>` (reason string only). Spec §3.3 says discharge differentiation needs to read `EnlistmentBehavior.IsRetiring` at hook time — works without event-param changes since the event fires after `IsEnlisted` flips. Plan 6 doesn't need any event-shape change here. Spec §3.3's prescribed `void OnDischarge(Hero, DischargeReason)` is a Plan-6-internal method signature, not an event signature; fine as-is.

6. **`.codex/config.toml`** — modified pre-session by another tool; not part of this work. Not staged.
