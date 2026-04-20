# Spec 1 (Enlisted Home Surface) — Phase C handoff

**Branch:** `development`
**Spec:** [2026-04-19-enlisted-home-surface-design.md](specs/2026-04-19-enlisted-home-surface-design.md)
**Plan:** [2026-04-19-enlisted-home-surface.md](plans/2026-04-19-enlisted-home-surface.md)
**Living reference:** [storylet-backbone.md](../Features/Content/storylet-backbone.md)

**Phase A (Tasks 1-5, generic substrate) complete 2026-04-19.** Commits `fc93285` → `24b7e50`.
**Phase B (Tasks 6-9, HomeActivity + save + triggers + lifecycle starter) complete 2026-04-20.** Commits `e4b6806` → `a48983c`.
**Phase C (Tasks 10-12) is next.**

## What Phase B shipped (consumed by Phase C)

- **`HomeActivity`** at `src/Features/Activities/Home/HomeActivity.cs`. Save offset **45**. Four phases declared in JSON (not yet authored — that's Task 13). `OnPlayerChoice` sets `Intent = optionId` and flips `EveningFired = true`. `OnBeat("departure_imminent", ...)` fast-forwards to `break_camp`.
- **`HomeTriggers`** at `src/Features/Activities/Home/HomeTriggers.cs` — 34 engine-state predicates registered via `TriggerRegistry.Register(name, (args, ctx) => ...)`. Named predicates: food (3) / morale (4) / lord-condition (5) / traits (8) / clan-kingdom (3) / settlement-type (3) / settlement-state (3) / time (3) / mod-state (2). Seed predicates (`is_enlisted`, `rank_gte`, `flag`, `quality_gte` etc.) are NOT re-registered — use them directly.
- **`HomeActivityStarter`** at `src/Features/Activities/Home/HomeActivityStarter.cs`. Listens on `CampaignEvents.SettlementEntered` and `OnSettlementLeftEvent` with strict `party == lord.PartyBelongedTo` gate. Rejects hideouts, hostile fiefs, non-fiefs, duplicate starts. `PickInitialIntent` uses `MBRandom.RandomFloat` with scaled weights (brood +3 wounded, brood/unwind +2 morale<40, train_hard +2 Valor, scheme +2 merc+Calculating, commune +3 home+spouse). Evening-phase player choice overrides the initial intent.
- **`ActivityContext.FromCurrent()`** — single-responsibility static factory on `src/Features/Activities/ActivityContext.cs`. Returns `new ActivityContext { EnteredAt = CampaignTime.Now }`. Generic substrate; Phase C starters / callers reuse this.
- **`EnlistedSaveDefiner`** claims offset **45** for `HomeActivity`. Offsets 46-60 still reserved for Specs 2-5.
- **`EnlistmentBehavior` accessors used by HomeTriggers:** `Instance`, `EnlistedLord` (Hero), `EnlistmentTier` (int), `DaysServed` (float), `IsEnlisted` (bool). **No new accessors were added** — HomeTriggers uses the existing public surface. (Plan originally proposed aliases `CurrentTier` / `DaysEnlisted`; these were dropped in favor of the real names.)

## Phase C tasks

| Task | File | Notes |
| :--- | :--- | :--- |
| 10 | `src/Features/Activities/Home/HomeEveningMenuProvider.cs` | Supplies 5 Evening intent slots to the slot-bank. `IsSlotActive` / `OnSlotSelected`. Hidden unless `HomeActivity` is in the `evening` phase AND intent pool has ≥1 eligible storylet. |
| 11 | `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` | Preallocated 5-slot option bank on `enlisted_status`. Mirrors existing `enlisted_decision_slot_0..4` pattern at line 1150-1158 (verify exact line). Priority 9 (above camp_hub at 10). |
| 12 | (none modified) | Phase A-C smoke: build clean, launch game, enter friendly town, confirm no crash and no Evening slots visible (content pool empty until Phase D). Checkpoint only — no commit. |

## Plan drift to watch for before Task 10

Phase A and Phase B both surfaced plan-vs-substrate drift during implementation. Consider dispatching a **plan-audit subagent** (Explore thoroughness medium) before Task 10 to verify the plan's Task 10 code block references real APIs. Likely areas of drift:

1. **`StoryletContext` field names.** Plan uses `ActivityTypeId`, `PhaseId`, `CurrentContext`. Grep `src/Features/Content/StoryletContext.cs` (or wherever it lives) to confirm exact field names.
2. **`Storylet.Immediate` / `Storylet.ToCandidate(ctx)` / `Storylet.Choices`.** Plan references these on the `Storylet` class. Read `src/Features/Content/Models/Storylet.cs` (or equivalent) to confirm.
3. **`EffectExecutor.Apply(effects, ctx)`** signature. Grep `src/Features/Content/EffectExecutor.cs` — note that another AI session may have it open, so treat with care.
4. **Pool resolution.** Plan assumes `HomeEveningMenuProvider` can get the evening-phase pool via `home.CurrentPhase.Pool` or similar. Confirm `Phase.Pool` is the field name on `src/Features/Activities/Phase.cs`, and that `CurrentPhase` returns the `Phase` (not an index).
5. **`StoryletSelector` or equivalent.** Plan's `PickStorylet` helper isn't detailed — it needs a pool + intent_bias + trigger-filter loop. Look for an existing `StoryletSelector.Pick(...)` (Spec 0 substrate) and reuse; don't reimplement.
6. **`AddGameMenuOption` signature.** Plan's Task 11 uses `starter.AddGameMenuOption(..., condition, consequence, false, 9)`. Confirm the last two args are `isRepeatable, priority` in that order (they are in vanilla Bannerlord, but double-check the overload).

## Gotchas carried forward

1. **CSPROJ wildcard** `<Compile Include="src\Features\Activities\*.cs"/>` (line 390) is **non-recursive** — Home subfolder files need explicit `<Compile Include="src\Features\Activities\Home\<file>.cs" />` entries. HomeActivity/HomeTriggers/HomeActivityStarter are already registered; HomeEveningMenuProvider needs adding.
2. **`ModLogger.Surfaced` / `Caught` / `Expected` require string literals at the call site.** Category + summary must NOT be consts — the `generate_error_codes.py` scanner rejects const-folded values.
3. **Don't `git add -A`.** User has concurrent edits in tree (`AGENTS.md`, `EffectExecutor.cs`, `TriggerRegistry.cs`). Stage only your specific Phase C files.
4. **Don't re-run `Tools/normalize_crlf.ps1` on existing files** — double-BOM bug. Only for newly-Write'd `.cs` / `.csproj` / `.sln` / `.ps1`.
5. **Close `BannerlordLauncher.exe` before `dotnet build`** — holds DLL open, fails with MSB3021.
6. **Regenerate the error-code registry** after any new `ModLogger.Surfaced` call: `/c/Python313/python.exe Tools/Validation/generate_error_codes.py`. Validator Phase 10 fails otherwise.
7. **Menu condition callbacks run every frame** — keep `IsSlotActive` cheap. Null-guard everything, no heap allocations in the hot path. `CountEligibleForIntent` may need memoization or a simple "at least one" short-circuit to avoid re-evaluating triggers 5× per tick.
8. **Menu slots are preallocated.** 5 slots always exist on the menu; their condition callback decides visibility. When `HomeActivity` isn't active or isn't in `evening` phase, all five must return `false` so the bank is silent. Task 12's smoke test specifically verifies this.
9. **Evening fires once per in-game night.** `HomeActivity.EveningFired` gates re-entry. `OnPhaseEnter("evening")` resets it to false; `OnPlayerChoice` flips it true and `ResolvePlayerChoice` advances the phase.

## Process that worked in Phases A and B

- **Subagent-driven development**: implementer → spec reviewer → code quality reviewer per task.
- **Plan audit subagent BEFORE implementer** for any non-trivial task — caught 9 drift points in Phase B (wrong method names, missing factories, wrong field names, retired APIs).
- **Trivial mechanical tasks** (e.g. Task 7 save-definer 1-liner) can be done directly by the controller without subagent overhead — save the full loop for code with judgment.
- **Bundle small review findings into a single fix-up commit** rather than amending; each commit is cheap and traceable.
- **Every new save-state field** needs either `[SaveableProperty]` + `AddClassDefinition` in `EnlistedSaveDefiner`, OR auto-serialization via `[Serializable]` base (for `Activity` subclasses).

## Commands

```bash
export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
/c/Python313/python.exe Tools/Validation/generate_error_codes.py  # after any ModLogger.Surfaced change
```

## First action in new chat

> Continue Spec 1 implementation at Phase C. Read the plan (`docs/superpowers/plans/2026-04-19-enlisted-home-surface.md`) and this handoff, then start Task 10 (HomeEveningMenuProvider). Use subagent-driven development (plan-audit → implementer → spec reviewer → code quality reviewer per task).
