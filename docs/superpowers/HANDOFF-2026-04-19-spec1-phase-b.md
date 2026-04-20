# Spec 1 (Enlisted Home Surface) — Phase B handoff

**Branch:** `development`
**Spec:** [2026-04-19-enlisted-home-surface-design.md](specs/2026-04-19-enlisted-home-surface-design.md)
**Plan:** [2026-04-19-enlisted-home-surface.md](plans/2026-04-19-enlisted-home-surface.md)
**Living reference:** [storylet-backbone.md](../Features/Content/storylet-backbone.md)

**Phase A (Tasks 1-5, generic substrate) complete 2026-04-19.** Commits `fc93285` → `24b7e50`; doc sweep at `de5f737` + `36bf61e`. Phase B (Tasks 6-9) is next.

## What Phase A shipped (inherited by all surface specs 1-5)

- **`ActivityTypeCatalog.LoadAll()`** — parses `ModuleData/Enlisted/Activities/*.json`; called from both `OnSessionLaunched` and `OnGameLoaded` (critical for save-load).
- **`StoryletEventAdapter.BuildModal(Storylet, ctx, Activity)`** → returns `EventDefinition` (**not** an `InteractiveEvent` wrapper — that class doesn't exist; `StoryCandidate.InteractiveEvent` is typed `EventDefinition`). Uses monotonic-counter synthetic IDs. Effects live in a static pending-effects registry drained by `EventDeliveryManager.OnOptionSelected`.
- **`PhaseDelivery.PlayerChoice`** runtime in `ActivityRuntime` — `_activeChoicePhase` / `GetActiveChoicePhase()` / `FindActive<T>()` / `ResolvePlayerChoice(activity)` + unified `AdvancePhase` with try/catch around `OnPhaseExit` / `OnPhaseEnter` / `Finish`. `FireIntervalHours` guard via `Activity.LastAutoFireHour` (generic base field, serialized).
- **`_activeChoicePhase` restore** on `OnGameLoaded` so mid-Evening saves don't hide the slot-bank.

## Phase B tasks

| Task | File | Notes |
| :--- | :--- | :--- |
| 6 | `src/Features/Activities/Home/HomeActivity.cs` | Save offset **45**. MUST set `TypeId = "home_activity"` in `OnStart` or phases stay empty. |
| 7 | `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` | `DefineClassType(typeof(HomeActivity))` at offset 45 |
| 8 | `src/Features/Activities/Home/HomeTriggers.cs` | ~30 engine-state predicates reading `MobileParty.Food`/`Morale`/`IsDisorganized`, `Hero.IsWounded`/traits/`Spouse`, `Clan.IsUnderMercenaryService`/`FactionsAtWarWith`, `Settlement.IsTown`/`IsCastle`/`IsUnderSiege`/`LastThreatTime`, `Town.Loyalty`, `CampaignTime.IsNightTime`/`GetSeasonOfYear` |
| 9 | `src/Features/Activities/Home/HomeActivityStarter.cs` | Strict party-identity gate: `party == EnlistmentBehavior.EnlistedLord.PartyBelongedTo` on BOTH `SettlementEntered` and `OnSettlementLeftEvent` |

Register `HomeTriggers.Register()` + `new HomeActivityStarter()` in `src/Mod.Entry/SubModule.cs` alongside Spec 0 registrations.

## Gotchas carried forward from Phase A

1. **CSPROJ wildcard** `<Compile Include="src\Features\Activities\*.cs"/>` (line 390) covers top-level Activities. **Verify whether it extends to `src\Features\Activities\Home\*.cs`** before Task 6 — may need explicit registration for the subfolder.
2. **`ModLogger.Surfaced` requires string literals at the call site.** Const-folding doesn't work — the `generate_error_codes.py` scanner rejects it. Use `"CATEGORY"` directly at each call site, not a private `const` field.
3. **`EnlistmentBehavior.DaysEnlisted` / `CurrentTier` visibility unknown** — Task 8 triggers reference them. Check + expose as public read-only properties if needed (narrow accessors, not a new subsystem).
4. **`EnlistmentBehavior.RecentDeaths(days)` does NOT exist** — the `recent_company_casualty` trigger is already out of Spec 1 scope (spec §3).
5. **Verify `CampaignTime.GetSeasonOfYear` enum path** against `C:\Dev\Enlisted\Decompile\` before Task 8 (plan says `CampaignTime.Seasons.Autumn` — verify).
6. **`ActivityContext` nullability is implicit** — overrides in `HomeActivity.OnPlayerChoice` / `OnBeat` should null-check `ctx` defensively.
7. **Don't `git add -A`.** User has concurrent edits in tree (`AGENTS.md`, `EffectExecutor.cs`, `TriggerRegistry.cs`).
8. **Don't re-run `Tools/normalize_crlf.ps1` on existing files** — double-BOM bug. Only for newly-Write'd `.cs` / `.csproj` / `.sln` / `.ps1`.
9. **Close `BannerlordLauncher.exe` before `dotnet build`** — holds DLL open, fails with MSB3021.

## Process that worked in Phase A

- Subagent-driven development: implementer → spec reviewer → code quality reviewer per task.
- Adversarial code review caught **4 plan gaps** during Phase A. Before Phase B Tasks 6-9, consider dispatching a quick "plan audit" subagent to read those tasks' code examples against the actual Spec 0 substrate (especially `EnlistmentBehavior` API surface) and flag any stale assumptions.
- Every new save-state field needs either `[Serializable] SyncData` auto-serialization OR explicit `OnGameLoaded` restore.

## Commands

```bash
export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
```

## First action in new chat

> Continue Spec 1 implementation at Phase B. Read the plan (`docs/superpowers/plans/2026-04-19-enlisted-home-surface.md`) and this handoff, then start Task 6 (HomeActivity). Use subagent-driven development (implementer → spec reviewer → code quality reviewer per task).
