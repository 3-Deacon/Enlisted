# Spec 1 (Enlisted Home Surface) — Phase E handoff

**Branch:** `development`
**Spec:** [2026-04-19-enlisted-home-surface-design.md](specs/2026-04-19-enlisted-home-surface-design.md)
**Plan:** [2026-04-19-enlisted-home-surface.md](plans/2026-04-19-enlisted-home-surface.md)
**Living reference:** [storylet-backbone.md](../Features/Content/storylet-backbone.md)

**Phase A (Tasks 1–5, generic substrate) complete 2026-04-19.** Commits `fc93285` → `24b7e50`.
**Phase B (Tasks 6–9, HomeActivity + save + triggers + lifecycle starter) complete 2026-04-20.** Commits `e4b6806` → `a48983c`.
**Phase C (Tasks 10–12, menu integration + smoke) complete 2026-04-20.** Commits `18b4021` → `19391a4`.
**Phase D (Tasks 13–18, content + localization) complete 2026-04-20.** Commits `8b96294` → `d9bcf8b`.
**Phase E (Tasks 19–21) is next.**

## What Phase D shipped (consumed by Phase E)

- **`home_activity.json`** at `ModuleData/Enlisted/Activities/` — four-phase ActivityTypeDefinition (`arrive` / `settle` / `evening` / `break_camp`). `evening` is `player_choice` delivery; the rest are `auto`. Pool IDs match the storylet corpus exactly.
- **50 storylets** under `ModuleData/Enlisted/Storylets/`: `home_arrive.json` (8 observational), `home_settle.json` (12 observational), `home_evening.json` (25 player-choice, 5 per intent), `home_break_camp.json` (5 observational). All triggers resolve against `HomeTriggers.cs`; all effects use the Spec 0 seed catalog or primitives. Two evening storylets park continuation chains (`home_chain_ledger_rumored` from `brood_dead_friend`, `home_chain_lord_remembered` from `commune_lord_at_hearth`) — the chain storylets themselves don't exist yet; first-fire will no-op gracefully until they're authored (out of Spec 1 scope).
- **`sync_event_strings.py` extended** (commit `7c32d53`) with `extract_strings_from_storylet()` and `scan_storylet_files()` — parses the inline `{=key}Fallback` pattern used by the storylet backbone (distinct from the legacy Event/Decision schema's `titleId`+`title` pair).
- **224 loc keys synced into `enlisted_strings.xml`** (commit `d9bcf8b`) — 206 home_* keys + 18 pre-existing `evt_quiet_*` keys that had been missing since `26b0000`. XML now parses clean, sync tool reports 0 missing.
- **Scaffolding-phase fixes.** Spec review caught two bugs in `home_evening.json` that shipped with the fix applied: `unwind_firelit` had `trigger: ["is_autumn"]` which gated out winter (fixed to empty trigger + cold-season weight modifiers); `train_hard_foreign_form` had a malformed loc-key `{=home_train_foreign_title_setup}` (fixed to `{=home_train_foreign_setup}`).

## Known first-draft prose in home_evening.json

23 of the 25 evening storylets are first-draft scaffolded prose from the plan's matrix (the two keystones — `brood_dead_friend` and `unwind_dice` — are plan-canonical). The user plans an authorial pass in a follow-up commit. The prose is shipping-quality (professional floor, correct length caps) but unpolished.

**Invented NPC names worth a later naming-pass decision:** Seoras (dead friend, plan-canon), Sergeant Burec (flask), Dervyn (sparring partner), Aldric (confession-bearer), Mira (old soldier), plus "a Vakken swordsman" and "an Aserai auxiliary" as unnamed figures.

## Phase E tasks

| Task | File(s) | Notes |
| :--- | :--- | :--- |
| 19 | `src/Debugging/Behaviors/DebugToolsBehavior.cs` (+ possibly `src/Features/Content/TriggerRegistry.cs` for one accessor) | Four smoke hotkeys: `Ctrl+Shift+H` force-start HomeActivity, `Ctrl+Shift+E` advance to evening, `Ctrl+Shift+B` advance to break_camp, `Ctrl+Shift+T` dump trigger eval table. Plan L2083-2199 has full code. If `TriggerRegistry.AllRegisteredNames()` doesn't exist, one-line expose or inline-iterate. C# change — CSPROJ wildcards for `src/Debugging/Behaviors/*.cs` (check line ~390 of `Enlisted.csproj` before adding if a new file is needed; modifying an existing file needs no csproj change). |
| 20 | `Tools/Validation/validate_content.py` | Extend Phase 12 with two new checks: (a) `Activities/*.json` pool entries must exist in the storylet catalog + delivery must be `auto` or `player_choice`; (b) `chain.when` values must parse to `{known_activity_type}.{known_phase_id}`. Plan L2203-2289 has scaffolded Python. Expected result after run: PASS. |
| 21 | `docs/superpowers/specs/2026-04-19-enlisted-home-surface-design.md` (status flip only) | End-to-end acceptance smoke. 8 in-game scenarios: friendly town entry (arrive/settle fire), Ctrl+Shift+E advancing to evening (5 slots visible, click fires Modal, option applies effect), party-identity gate (other parties don't trigger HomeActivity), save/load mid-activity, chain park then fire across settlements, no OrchestratorOverride regression. No commits except the final spec-status flip to "Implementation complete". Plan L2293-2359. |

## Process

- **Task 19 is C# with full plan code.** Subagent-driven-development works cleanly; spec review should be strict about the exact method signatures and the `TriggerRegistry` accessor (add or inline — don't guess).
- **Task 20 is Python tooling.** Similar pattern: dispatch implementer + spec reviewer + code quality reviewer. Watch for Phase 12's existing structure — the new checks integrate into the driver, don't replace it.
- **Task 21 is manual in-game.** Not subagent-driven; the user runs Bannerlord and ticks off scenarios. Subagent can only verify the build + validator are green as the preflight gate (Step 1).

## Gotchas carried forward

1. **CSPROJ wildcards are non-recursive.** `<Compile Include="src\Features\Activities\*.cs"/>` and siblings only match files directly in that dir, not subfolders. Modifying `src/Debugging/Behaviors/DebugToolsBehavior.cs` (existing file) needs no csproj change, but adding any new `.cs` in a subfolder requires an explicit `<Compile Include>` line.
2. **`ModLogger.Surfaced / Caught / Expected` require string literals** at the call site for category + summary. Regenerate the error-code registry with `/c/Python313/python.exe Tools/Validation/generate_error_codes.py` after adding any new `Surfaced` call; Phase 10 of the content validator fails otherwise.
3. **Don't `git add -A`.** Concurrent edits in tree (`AGENTS.md`, `src/Features/Content/EffectExecutor.cs`, `src/Features/Content/TriggerRegistry.cs`, and untracked files in `Tools/` + `docs/superpowers/specs/`). Stage only the files you touch.
4. **Normalize CRLF for newly-created `.cs` files.** `Tools/normalize_crlf.ps1 -Path <file>`, once per new file (double-BOM bug on re-run). **Editing** an existing `.cs` relies on `.gitattributes` at commit time — don't re-run the script.
5. **Close `BannerlordLauncher.exe` before `dotnet build`** — MSB3021 otherwise.
6. **Any `Campaign.Current.X`-backed static** (DefaultTraits, DefaultSkills, DefaultPerks, DefaultCharacterAttributes, DefaultBodyProperties) is null at `OnGameStart`. The Task 19 debug helpers run at runtime (player input), so they're past `OnSessionLaunched` — should be safe. But if you add anything that runs at module boot, pass providers (`Func<T>`) or defer the lookup.
7. **Regenerating localization.** If Task 19 introduces any `InformationMessage(new InformationMessage("..."))` with literal English, it won't flow through `sync_event_strings.py` (that tool scans JSON content only). Inline debug text for hotkeys is fine — these are dev-facing strings. Don't invent a `{=key}` for debug toast text.
8. **`validate_content.py` Phase 11** separately fails the build if any `ModLogger.Error(...)` call appears in `src/` — retired 2026-04-19. Use `Surfaced` / `Caught` / `Expected`.

## Commands

```bash
export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
/c/Python313/python.exe Tools/Validation/generate_error_codes.py   # only if a ModLogger.Surfaced call site changes
/c/Python313/python.exe Tools/Validation/sync_event_strings.py     # if any new loc-keys appear (unlikely in Phase E)
```

## First action in new chat

> Continue Spec 1 implementation at Phase E. Read the plan (`docs/superpowers/plans/2026-04-19-enlisted-home-surface.md`) and this handoff, then start Task 19 (debug hotkeys in `DebugToolsBehavior.cs`). Subagent-driven-development: implementer → spec reviewer → code quality reviewer per task. Task 19 is C# with full plan code; Task 20 is Python validator extension; Task 21 is manual in-game smoke — only the preflight build+validator gate is subagent-appropriate.
