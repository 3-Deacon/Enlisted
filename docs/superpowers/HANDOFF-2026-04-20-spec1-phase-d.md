# Spec 1 (Enlisted Home Surface) — Phase D handoff

**Branch:** `development`
**Spec:** [2026-04-19-enlisted-home-surface-design.md](specs/2026-04-19-enlisted-home-surface-design.md)
**Plan:** [2026-04-19-enlisted-home-surface.md](plans/2026-04-19-enlisted-home-surface.md)
**Living reference:** [storylet-backbone.md](../Features/Content/storylet-backbone.md)

**Phase A (Tasks 1–5, generic substrate) complete 2026-04-19.** Commits `fc93285` → `24b7e50`.
**Phase B (Tasks 6–9, HomeActivity + save + triggers + lifecycle starter) complete 2026-04-20.** Commits `e4b6806` → `a48983c`.
**Phase C (Tasks 10–12, menu integration + smoke) complete 2026-04-20.** Commits `18b4021` → `19391a4`.
**Phase D (Tasks 13–18) is next.**

## What Phase C shipped (consumed by Phase D)

- **`HomeEveningMenuProvider`** at `src/Features/Activities/Home/HomeEveningMenuProvider.cs`. Static class, no state. Public surface: `IsSlotActive(int slotIndex, out TextObject label, out TextObject tooltip)` + `OnSlotSelected(int slotIndex)`. `Intents` is `{ "unwind", "train_hard", "brood", "commune", "scheme" }` in array order — slot 0 → unwind, slot 4 → scheme. Eligibility uses an early-exit `AnyEligibleForIntent` over the active phase pool filtered by `home_evening_<intent>_` prefix. `PickStorylet` does a weighted pick (weight = `Storylet.WeightFor(ctx)`) and routes through `StoryletEventAdapter.BuildModal` + `StoryDirector.EmitCandidate(..., ChainContinuation=true)`.
- **5-slot Evening option bank** on `enlisted_status` in `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` at L1160-1187. Priority 9 (above camp_hub at 10). Mirrors the decision-slot loop pattern (L1149-1158). Text key `EVENING_SLOT_<i>_TEXT` hoisted per-slot to avoid hot-path string interpolation.
- **Phase A–C smoke verified in-game** by the user. `HomeActivity` starts silently on friendly-fief settlement entry under the lord's party; `enlisted_status` opens clean; no Evening slots visible (content pool empty until Phase D); no `Surfaced` errors.

## Latent bugs caught on first boot — now fixed

Phase C's in-game smoke surfaced two pre-existing bugs that had never run before because no launch had happened since their introduction:

1. **`HashSet<StoryBeat>` container registration** (`0ff3a2d`). TW v1.3.13 `SaveSystem` has zero `HashSet<T>` awareness — `ContainerType` enum is closed to `List/Queue/Dictionary/Array/CustomList/CustomReadOnlyList`, and `TypeExtensions.IsContainer` falls through for `HashSet<T>`. Passing it to `ConstructContainerDefinition` leaves `keyId`/`valueId` null → `ContainerSaveId.CalculateStringId` null-derefs at `Module.Initialize()` before any mod code runs. Fix: deleted the registration. `DispatchItem.Beats` is a transient DTO field; `StoryCandidatePersistent` never carried it; `EnlistedNewsBehavior` already persists beats manually via count-then-items `SyncData`. No functional change.

2. **Eager `DefaultTraits.Mercy` at `OnGameStart`** (`19391a4`). `DefaultTraits.Mercy` resolves `Campaign.Current.DefaultTraits._traitMercy`; `Campaign.Current.DefaultTraits` is null at `OnGameStart`, so `HomeTriggers.Register()` NRE'd on line 55 passing `DefaultTraits.Mercy` as a method argument. This aborted the rest of `OnGameStart` (including menu registration, deferred patches, enlisted activation). Fix: `RegisterTrait` now takes `Func<TraitObject>`; trait lookup deferred to predicate-eval time. Other `DefaultTraits.X` usages in `src/` are all inside method bodies (lazy already, unaffected).

**Gotcha to carry forward:** any `TaleWorlds.CampaignSystem.Campaign.Current.X`-backed static (DefaultTraits, DefaultSkills, DefaultPerks, DefaultCharacterAttributes, DefaultBodyProperties) is null during `OnGameStart`. Pass providers (`Func<T>`) or look up inside the predicate/handler, never as an eager argument at registration time.

## Phase D tasks

| Task | File(s) | Notes |
| :--- | :--- | :--- |
| 13 | `ModuleData/Enlisted/Activities/home_activity.json` | `ActivityTypeDefinition` with 4 phases (`arrive`, `settle`, `evening`, `break_camp`), pool IDs per phase, per-phase delivery, per-phase intent biases. Mechanical JSON — plan L1474-1585 has the full schema. Start here; unblocks the rest. |
| 14 | `ModuleData/Enlisted/Storylets/home_arrive.json` | 8 storylets. Auto delivery. |
| 15 | `ModuleData/Enlisted/Storylets/home_settle.json` | 12 storylets. Auto delivery. |
| 16 | `ModuleData/Enlisted/Storylets/home_evening.json` | 25 storylets, **5 per intent** (prefix `home_evening_<intent>_<n>`). PlayerChoice delivery — these are what the Task 10 provider surfaces. |
| 17 | `ModuleData/Enlisted/Storylets/home_break_camp.json` | 5 storylets. Auto delivery. |
| 18 | — | `/c/Python313/python.exe Tools/Validation/sync_event_strings.py` + commit the generated localization diff. |

## Process

- **Task 13 is mechanical** — schema is fully specified in the plan. Subagent-driven-development works; spec review is strict on field names/intent biases.
- **Tasks 14–17 are creative authoring.** 50 narrative beats that need authorial voice + lord/situation context. Subagent loops can scaffold storylet skeletons from the storylet-backbone effect catalog, but plan a human pass for narrative text. Reference: `docs/Features/Content/writing-style-guide.md` and the scripted-effect catalog at `ModuleData/Enlisted/Effects/scripted_effects.json` (22 seed entries — reuse aggressively over inventing new effects).
- **Validate aggressively.** `validate_content.py` Phase 12 already blocks unknown scripted-effect ids, read-only quality writes, and malformed intent biases. It will fail loudly if a storylet references a trigger that isn't in `TriggerRegistry` — the HomeTriggers catalog is at `src/Features/Activities/Home/HomeTriggers.cs`. Seed triggers from Spec 0 (`is_enlisted`, `rank_gte`, `flag`, `quality_gte`) are live; Home-specific predicates (34 of them) are in the HomeTriggers file.

## Gotchas carried forward from prior phases

1. **CSPROJ wildcard** `<Compile Include="src\Features\Activities\*.cs"/>` (L390) is **non-recursive** — Home subfolder files need explicit entries. Phase D is content-only, so this likely doesn't apply, but it does for Phase E (Task 19: debug hotkeys → modify `DebugToolsBehavior.cs`, no new `.cs` needed unless you add a helper).
2. **`ModLogger.Surfaced / Caught / Expected` require string literals** at the call site for category + summary. The `generate_error_codes.py` scanner rejects const-folded values.
3. **Don't `git add -A`.** User has concurrent edits in tree (`AGENTS.md`, `src/Features/Content/EffectExecutor.cs`, `src/Features/Content/TriggerRegistry.cs`, and untracked files in `Tools/` + `docs/superpowers/specs/`). Stage only your Phase D files.
4. **Don't re-run `Tools/normalize_crlf.ps1` on existing files** — double-BOM bug. Only for newly-Write'd `.cs / .csproj / .sln / .ps1`. Phase D is `.json` + `.xml`; `.gitattributes` has those as `text=auto`, so no normalization needed.
5. **Close `BannerlordLauncher.exe` before `dotnet build`** — holds DLL open, fails with MSB3021.
6. **Regenerate the error-code registry** if you add any `ModLogger.Surfaced` call: `/c/Python313/python.exe Tools/Validation/generate_error_codes.py`. Validator Phase 10 fails otherwise.
7. **Read-only qualities** (`rank`, `days_in_rank`, `days_enlisted`) cannot be written from storylet effects. Phase 12 blocks this at build time. Rank advancement routes through `EnlistmentBehavior.SetTier`.
8. **Prefer seed scripted-effect ids** (`rank_xp_minor`, `lord_relation_up_*`, `scrutiny_down_*`, etc.) over minting one-off names. If you need a new one, add it to `ModuleData/Enlisted/Effects/scripted_effects.json` and confirm it validates.
9. **Every new `SaveableTypeDefiner` offset claim** needs grepping `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` first. Offsets 45–60 are reserved for concrete Activity subclasses (45 = HomeActivity, claimed).

## Commands

```bash
export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
/c/Python313/python.exe Tools/Validation/generate_error_codes.py   # only if a ModLogger.Surfaced call site changes
/c/Python313/python.exe Tools/Validation/sync_event_strings.py      # Task 18
```

## First action in new chat

> Continue Spec 1 implementation at Phase D. Read the plan (`docs/superpowers/plans/2026-04-19-enlisted-home-surface.md`) and this handoff, then start Task 13 (`home_activity.json`). Subagent-driven-development: implementer → spec reviewer → code quality reviewer per task. Task 13 is mechanical JSON; Tasks 14–17 are narrative — scaffold with subagents, then pause for the user's authorial pass before committing.
