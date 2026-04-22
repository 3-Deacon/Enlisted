# CLAUDE.md

Claude Code project memory for the Enlisted Bannerlord mod.

## Primary instructions

Universal rules, commands, patterns, pitfalls, and the docs map live in `AGENTS.md` — shared across all AI coding tools (Claude Code, Codex, Cursor, Copilot, Aider, etc.). Claude Code loads it automatically via this import:

@AGENTS.md

Everything below is **Claude-specific** and layers on top of AGENTS.md. Read AGENTS.md first.

---

## Current project status (2026-04-21)

- **Spec 0 (Storylet Backbone)** — ✅ shipped (`45b38bf`).
- **Spec 1 (Enlisted Home Surface)** — ✅ shipped on `development` (commits `fc93285` → `0390fdf`). Living reference: [docs/Features/Content/home-surface.md](docs/Features/Content/home-surface.md).
- **Spec 2 (Orders Surface)** — Phase A code shipped (Tasks 1-17, save-definer offsets 46/47/85 claimed, in-game smoke verified). Phase B code shipped (consumer migrations, validator rails Phases 14-17, Tasks 33/34 named-order T1-T6 archetypes authored, ~100 storylets in commits `f8e028b` → `deb88db`). Legacy `src/Features/Orders/` + `ModuleData/Enlisted/Orders/` deleted 2026-04-21 (`a8719bb`). Old plan archived to [docs/superpowers/plans/archive/2026-04-20-orders-surface.md](docs/superpowers/plans/archive/2026-04-20-orders-surface.md). Remaining Orders tasks (ambient pools, transitions, path crossroads, polish) are migrated into the five-plan integration roadmap below.
- **Five-plan integration roadmap (2026-04-21)** — the Spec 2 Orders Surface remainder and the Campaign Intelligence Backbone were merged into one integrated loop. Integration spec: [docs/superpowers/specs/2026-04-21-plans-integration-design.md](docs/superpowers/specs/2026-04-21-plans-integration-design.md). Design spec (truth layer): [docs/superpowers/specs/2026-04-21-enlisted-campaign-intelligence-design.md](docs/superpowers/specs/2026-04-21-enlisted-campaign-intelligence-design.md).
  - **Plan 1** — Campaign Intelligence Backbone (snapshot + collector + classifier + hourly tick + persistence, no consumers). Plan at [docs/superpowers/plans/2026-04-21-campaign-intelligence-backbone.md](docs/superpowers/plans/2026-04-21-campaign-intelligence-backbone.md). ✅ Shipped on `development` 2026-04-22 (commits `34322d4` → `bc13096`, 23 commits). All 29 tasks complete except T28 (in-game manual smoke verification — pending human operator). First `INTEL/hourly_recompute` heartbeats observed live in `Session-A_2026-04-22_05-00-20.log`.
  - **Plan 2** — Lord AI Intervention (model wrappers + narrow Harmony, snapshot-driven). Plan at [docs/superpowers/plans/2026-04-21-lord-ai-intervention.md](docs/superpowers/plans/2026-04-21-lord-ai-intervention.md). ✅ Code complete on `development` 2026-04-22 (commits `4929246` → `4df12ad`, 22 commits). Three MBGameModel wrappers registered via `SubModule.OnGameStart` bias target choice, army formation, and pursuit for the enlisted lord only; every override gated per-call on `EnlistedAiGate.TryGetSnapshotForParty` + falls through to vanilla `BaseModel`. T24 14-day in-game smoke pending human operator; T16-T18 Phase F narrow Harmony deferred pending T24 evidence.
  - **Plan 3** — Signal Projection (`SignalBuilder` projects snapshot into imperfect player-visible signals; absorbs floor storylets; rewires `DailyDriftApplicator` emission). Drafted at [docs/superpowers/plans/2026-04-21-signal-projection.md](docs/superpowers/plans/2026-04-21-signal-projection.md) (commit `1db040e`, fixes `31b1048` / `133f394`). ✅ Shipped on `development` 2026-04-22 (commits `5fedf88` → `189e75c`, 25 commits + status closure = 26). Verification doc: [docs/superpowers/plans/2026-04-21-signal-projection-verification.md](docs/superpowers/plans/2026-04-21-signal-projection-verification.md). T28 in-game smoke pending human operator.
  - **Plan 4** — Duty Opportunities (`DutyOpportunityBuilder` generates ambient duty storylets; absorbs Orders Tasks 27-32 profile pools + Task 35 transitions + validator Phase 13). Drafted at [docs/superpowers/plans/2026-04-21-duty-opportunities.md](docs/superpowers/plans/2026-04-21-duty-opportunities.md) (commit `1db040e`, fixes `31b1048` / `133f394`). Ready to start — Plan 1 signed off 2026-04-22.
  - **Plan 5** — Career Loop Closure (path crossroads + culture/trait overlays + debug hotkeys + save-load + final playtest). Drafted at [docs/superpowers/plans/2026-04-21-career-loop-closure.md](docs/superpowers/plans/2026-04-21-career-loop-closure.md) (commit `1db040e`, fixes `31b1048` / `133f394`). Ready to start — Plan 1 signed off 2026-04-22.
- **Specs 3–5** — Land/Sea, Promotion+Muster, Quartermaster — not yet started; save-definer class offset range 49-60 reserved (offset 48 claimed by Plan 1 per AGENTS.md Rule #11).

---

## Context7 MCP Library IDs

For third-party library docs, use the Context7 MCP with these IDs:

| Library | Context7 ID |
| :--- | :--- |
| Harmony | `/pardeike/harmony` |
| Newtonsoft.Json | `/jamesnk/newtonsoft.json` |
| C# Language | `/websites/learn_microsoft_en-us_dotnet_csharp` |

**TaleWorlds APIs:** NEVER use Context7, web search, or training knowledge. Always use the decompile at `../Decompile/` (sibling of the repo root, not tracked in git) — it is the only authoritative reference for the Bannerlord API surface the user has installed locally. Regenerate it with `Tools/Decompile-Bannerlord.bat` when the install is updated or the directory is missing.

---

## MCP Server Usage

- **Context7** — Third-party library docs only (Harmony, Newtonsoft). Not for TaleWorlds APIs.
- **Microsoft Learn** — Use for .NET Framework 4.7.2 and C# language questions Context7 doesn't cover.
- **Playwright** — UI testing if/when a browser-facing tool is added; not applicable to the mod itself.
- **Cloudflare / Gmail / Google Drive / Google Calendar / any other ambient MCP servers** — Not relevant to this project; ignore.

**Error code registry:** [docs/error-codes.md](docs/error-codes.md) is auto-generated by `Tools/Validation/generate_error_codes.py` from `ModLogger.Surfaced(...)` call sites. Do not hand-edit — changes are overwritten on the next run. Historical `E-<SUBSYSTEM>-NNN` codes from pre-redesign logs are preserved in [docs/error-codes-archive.md](docs/error-codes-archive.md). `validate_content.py` Phase 11 separately fails the build if any `ModLogger.Error(...)` call appears in `src/` (the public API was retired 2026-04-19 — use `Surfaced` / `Caught` / `Expected`).

---

## Recommended Skills

Match the task to the right skill:

| Task | Skill |
| :--- | :--- |
| Reviewing a PR | `code-review:code-review` |
| Before proposing a bug fix | `superpowers:systematic-debugging` |
| Before claiming work done | `superpowers:verification-before-completion` |
| New feature implementation | `superpowers:test-driven-development` |
| Before designing any feature | `superpowers:brainstorming` |
| Writing an implementation plan | `superpowers:writing-plans` |
| Executing a multi-task plan | `superpowers:subagent-driven-development` |
| Security review of the branch | `security-review` |
| Updating this CLAUDE.md file | `claude-md-management:revise-claude-md` (small targeted edits) or `claude-md-management:claude-md-improver` (full audit + improve) |
| Reducing permission prompts | `fewer-permission-prompts` |

---

## Session-Specific Guidance

### Shell & PATH

- Shell is bash on Windows — use Unix paths (`/dev/null`, forward slashes), not `NUL` or backslashes
- The bash here is a thin shim — `cat`, `head`, `tail`, `grep`, `file`, `which` are not on PATH. Use the Grep / Read / Write tools instead of shell equivalents
- `dotnet`, `git`, `python` aren't on PATH by default. Prepend: `export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"`. Python is at `/c/Python313/python.exe`

### Build & commit

- AGENTS.md's build form `/p:Platform=x64` trips bash's argument parser when the config has a space. From bash use: `dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64`
- Multi-line commit messages: no `cat` heredoc here. Write the message to a temp file (e.g. `/c/Users/<you>/commit.txt`) and pass with `git commit -F <file>`. **Known footgun:** the Write tool errors if you skip `Read` first, and when it errors, the OLD content from a prior task's message stays in place — `git commit -F` then ships a misnamed commit with the previous task's subject. Always `Read` the temp file before `Write`, every time, even for a throwaway scratch path.
- Another AI session may be editing files concurrently. Stage with `git add <path>`, never `git add -A` — in-flight edits belong to the other session and don't belong in your commit

### File handling

- Write tool creates LF-only files; `.gitattributes` enforces CRLF on `.cs` / `.csproj` / `.sln` / `.ps1`. For **newly created** files, run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path <file>` exactly once. **Known bug:** the script prepends a UTF-8 BOM unconditionally, so a second run on the same file creates a double-BOM defect. For **edits** to existing `.cs` files, rely on `.gitattributes` at commit time — don't run the script.
- **`Enlisted.csproj` wildcards are NON-RECURSIVE.** `<Compile Include="src\Features\Activities\*.cs"/>` (line 390) and similar patterns only match files directly in the named directory — they do NOT match subfolders. A file at `src\Features\Activities\Home\Foo.cs` needs an explicit `<Compile Include>` line even though `Activities\*.cs` exists. Before creating a new `.cs` file, grep `Enlisted.csproj` for a wildcard that covers your exact directory (not a parent).

### Claude workflow

- For broad codebase exploration (>3 searches), spawn an `Explore` subagent rather than searching directly
- Parallelize independent Agent / tool calls; serialize only when one result feeds the next
- If the user needs to run an interactive command, suggest the `!` prefix so output lands in-context

### Project conventions

- **Storylet loc-keys use inline `{=key}Fallback`** (in `title` / `setup` / `options[].text` / `options[].tooltip`), NOT the legacy Event schema's separate `titleId`+`title` pairs. `sync_event_strings.py` scans both schemas; run it after authoring and integrate the generated XML into `ModuleData/Languages/enlisted_strings.xml`. The game falls back to inline text when a key is missing from XML (zero runtime impact), so missing keys only affect translators.
- **`ModLogger.Surfaced` / `Caught` / `Expected`** need string *literals at the call site* for category + summary. `generate_error_codes.py`'s scanner doesn't follow `private const string Cat = "X"` and will reject the call. Write `ModLogger.Surfaced("CATEGORY", "summary literal", ex)` at every call site, not a shared const.
- Regenerate the error-code registry after **any line-shifting C# edit in a file that contains `ModLogger.Surfaced(...)` calls** — codes track `(category, file, line)`, so adding or removing lines above a Surfaced call (even adding an unrelated event declaration or extracting a method) invalidates the registry and fails `validate_content.py` Phase 10. Run `/c/Python313/python.exe Tools/Validation/generate_error_codes.py` and stage `docs/error-codes.md` in the same commit. Don't hand-edit.
- **Plan-vs-decompile API divergence.** Spec/plan files in `docs/superpowers/plans/` prescribe TaleWorlds API calls that were written at spec time and can drift from the actual API surface in ../Decompile/. Spec 2 hit ~5 divergences across Tasks 4/5/7/8/10 (e.g. `StoryletCatalog.Instance.Get` vs actual static `GetById`; `HeroDeveloper.AddFocus(skill, amount)` vs actual 3-arg overload that decrements the unspent pool if you pass only 2; `GainRenownAction.Apply(Hero, int)` vs actual `float`; `FormationClass` namespace). Every implementer subagent must verify prescribed APIs against `../Decompile/` before writing the call. Long plans should append an "API corrections appendix" at the bottom as they discover deviations — see the Spec 2 plan for the pattern.
- **Doc comments describe behavior, never forward-spec or change history.** AGENTS.md already states this, but spec/plan files occasionally prescribe XML doc comments like `/// Spec 2 PathScorer subscribes for crossroads firing; Spec 4 will subscribe for promotion ceremony.` Copy these verbatim and the next session with different context will find them rotted into stale fiction. Rewrite as one behavioral sentence at the implementer-prompt stage — don't ship the plan's literal doc text when it references future specs or explains why a file exists.
- **Never re-register a TaleWorlds built-in type in `EnlistedSaveDefiner`.** `TaleWorlds.Core.SaveableCoreTypeDefiner` registers `FormationClass` at id 2008 (decompile `TaleWorlds.Core/SaveableCoreTypeDefiner.cs:66`); `SaveableCampaignTypeDefiner` registers many more. All `SaveableTypeDefiner` instances share one `DefinitionContext`, and `AddEnumDefinition` / `AddClassDefinition` key their internal dictionaries by the `Type` via plain `Dictionary.Add` (decompile `DefinitionContext.cs:118-124`) — no dedupe, no overwrite. A second registration of a vanilla Type throws `ArgumentException` from `Dictionary.Insert` at `Module.Initialize()` before any mod logging is available (native watchdog log only). Only register types **your own code defines**. If you need to serialize a vanilla type (e.g. `FormationClass`) as a field on your `[Serializable]` class, just declare the field — vanilla's existing registration handles it (same as `CampaignTime`, `Hero`, `MBGUID` fields throughout our serialized classes).
- **`HashSet<T>` is not a saveable container in the TaleWorlds SaveSystem.** `TaleWorlds.SaveSystem.ContainerType` only knows `List / Queue / Dictionary / Array / CustomList / CustomReadOnlyList` — `IsContainer` falls through for `HashSet<T>`, leaving both `SaveId`s null and crashing `ContainerSaveId.CalculateStringId` during `Module.Initialize()` (before mod logs exist). Use `List<T>` with runtime dedup, or serialize-to-CSV + rebuild on load (see `CompanySimulationBehavior._activeFlags`). If the game crashes before the session log writes a single line, check the native stack in `C:\ProgramData\Mount and Blade II Bannerlord\logs\watchdog_log_<pid>.txt`.
- **`Campaign.Current.X`-backed statics are null at `OnGameStart`.** `DefaultTraits.Mercy/Valor/Honor/Calculating`, `DefaultSkills.*`, `DefaultPerks.*` all dereference `Campaign.Current.DefaultXxx` internally. Touching them eagerly at registration (e.g. `RegisterTrait("mercy", DefaultTraits.Mercy)`) NREs before `OnGameStart` finishes and aborts the rest of bootstrap (menu regs, deferred patches, enlisted activation). Pass providers (`Func<TraitObject>`) or resolve inside the handler body — lookup must happen after `OnSessionLaunched`.
- **`[Serializable]` save stores deserialize with null `Dictionary`/`List` properties.** TaleWorlds `IDataStore.SyncData` uses a deserialization path that skips the ctor, so `public Dictionary<...> Foo { get; set; } = new(...)` field initializers don't run when loading a save that predates the field. Any `foreach (... in Foo.Keys)` in `OnGameLoaded` or on an hourly/daily tick NREs and crosses into native, killing the process (no managed log line — only a crash dump). Add an `EnsureInitialized()` method on the store that reseats null dict/list fields with empty instances, and call it from `SyncData` (after the `dataStore.SyncData(...)` line), `OnSessionLaunched`, and `OnGameLoaded`. See `FlagStore.EnsureInitialized` / `QualityStore.EnsureInitialized` for the pattern.
- **`Enlisted.csproj` `AfterBuild` target needs explicit entries per `ModuleData/Enlisted/*/` subdir.** Adding a new content directory requires three additions: an `<XxxData Include="ModuleData\Enlisted\Xxx\*.json"/>` ItemGroup, a matching `<MakeDir Directories="$(OutputPath)..\..\ModuleData\Enlisted\Xxx"/>` inside `AfterBuild`, and a `<Copy SourceFiles="@(XxxData)" DestinationFolder="...\Xxx\"/>` step. Missing any of the three = content silently not deployed to the game install. Runtime loaders log `Expected("XXX", "no_xxx_dir", "directory not found: ...")` at info level, so the failure is easy to miss. Pattern at `Enlisted.csproj:614-671` (ItemGroups) and `:728-745` (AfterBuild).
- **`CampaignGameStarter.AddGameMenuOption(..., index)` is positional `List.Insert`, NOT priority sort.** Decompile: `TaleWorlds.CampaignSystem.GameMenus/GameMenu.cs:147-156`. Registering N options all at the same `index` inserts them in reverse visual order and splices them around any later options that claim a lower index. Use unique indices per option (e.g. `9 + i` in a loop), and give each "trailing" option (Camp, Visit Settlement) an index past the maximum of any preallocated slot bank above it.

