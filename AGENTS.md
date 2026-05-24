# Enlisted - Bannerlord Mod

C# mod transforming Mount & Blade II: Bannerlord into a soldier career simulator. Player enlists with a lord, follows orders, earns wages, progresses through 9 ranks. 245+ narrative content pieces, data-driven via JSON + XML.

This file is the shared source of truth for AI coding agents (Claude Code, Codex, Cursor, Copilot, Aider, etc.). Tool-specific extras live alongside: `CLAUDE.md` imports this file.

**Active work:** see [docs/superpowers/STATUS.md](docs/superpowers/STATUS.md) for current plan/spec progress.

---

## Quick Commands

```bash
# Build — produces Enlisted.dll in BOTH Win64_Shipping_Client/ and
# Win64_Shipping_wEditor/ via csproj post-build mirror. Close BannerlordLauncher
# first — it holds the DLL open and fails the copy with MSB3021.
dotnet build -c "Enlisted RETAIL" /p:Platform=x64

# Validate content (ALWAYS before commit)
python Tools/Validation/validate_content.py

# Sync localization strings
python Tools/Validation/sync_event_strings.py

# Run repo lint stack (.editorconfig + content validators + Ruff + PSScriptAnalyzer)
./Tools/Validation/lint_repo.ps1

# Upload to Steam Workshop
./Tools/Steam/upload.ps1

# Regenerate the Decompile tree from your local Bannerlord install.
# Path depends on env: ../Decompile/ (Windows, sibling of repo) OR ./Decompile/ (WSL, inside repo, gitignored).
# Windows: ./Tools/Decompile-Bannerlord.bat
# WSL Linux: dotnet tool install -g ilspycmd --version 8.2.0.7535
#            DOTNET_ROLL_FORWARD=LatestMajor ~/.dotnet/tools/ilspycmd -p <DLL> -o Decompile/<AssemblyName>
#            (loop over the 40 assemblies in bin/Win64_Shipping_Client + Modules/{Native,SandBox,SandBoxCore,StoryMode,NavalDLC}/bin/Win64_Shipping_Client)
./Tools/Decompile-Bannerlord.bat
```

---

## Critical Rules (Will Break Mod)

### 1. Verify every TaleWorlds API against the local Decompile tree

- The decompile is the only authoritative reference for the Bannerlord API surface the user has installed.
- **Location depends on environment:** `../Decompile/` (Windows, sibling of repo root, external to git) OR `./Decompile/` (WSL, inside the repo, gitignored — see `.gitignore`). Check `[ -d ./Decompile ] || ls ../Decompile` to pick the right base path. Code in this repo references the Windows form (`../Decompile`); from WSL, use `./Decompile`.
- Regenerate per the Quick Commands section above when the install is patched.
- NEVER use online docs, Context7, or training knowledge for TaleWorlds APIs — they drift across patches.

### 2. New C# Files Must Be Registered in .csproj

```xml
<Compile Include="src\Features\MyFeature\MyNewClass.cs"/>
```

### 3. Gold Transactions — use GiveGoldAction

```csharp
// CORRECT - visible in UI
GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount);
// WRONG - not visible, breaks UI feedback
Hero.MainHero.ChangeHeroGold(amount);
```

### 4. Equipment Iteration — numeric loop only

```csharp
// CORRECT
for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
// WRONG - crashes (Enum.GetValues includes count values)
foreach (EquipmentIndex slot in Enum.GetValues(typeof(EquipmentIndex)))
```

### 5. Hero Safety — null-safe, IsAlive checks

```csharp
var hero = CampaignSafetyGuard.SafeMainHero;
if (hero == null) return;
if (hero.IsAlive) VisualTrackerManager.RegisterObject(hero);
```

### 6. JSON Field Order — fallback immediately after ID
See [ModuleData/Enlisted/AGENTS.md](ModuleData/Enlisted/AGENTS.md).

### 7. Event Tooltips Required
See [ModuleData/Enlisted/AGENTS.md](ModuleData/Enlisted/AGENTS.md).

### 8. Save System Registration

In `EnlistedSaveDefiner` — missing = "Cannot Create Save" error:

```csharp
DefineEnumType(typeof(MyNewEnum));
DefineClassType(typeof(MyNewClass));
```

Full registration table + `SyncData()` pattern: [src/Mod.Core/SaveSystem/AGENTS.md](src/Mod.Core/SaveSystem/AGENTS.md).

### 9. Windows/WSL Portability

This repo is developed on both Windows and Linux (WSL). For C# code that builds paths:

```csharp
// CORRECT - cross-platform
Path.Combine(basePath, "Prompts", "order_prompts.json");
ModulePaths.GetContentPath("Prompts"); // preferred for mod content

// WRONG - breaks on Linux
basePath + "\\Prompts\\order_prompts.json"
```

Line endings are enforced by `.gitattributes` (`.cs` / `.csproj` / `.sln` / `.ps1` = CRLF; everything else `text=auto`). Don't override locally.

### 10. Event Delivery — route through StoryDirector

Modal events go through `StoryDirector.EmitCandidate(...)`, not `EventDeliveryManager.Instance.QueueEvent(...)` directly. Full pattern + `StoryCandidate` construction example in [src/Features/Content/AGENTS.md](src/Features/Content/AGENTS.md).

### 11. Content authoring — route through the storylet backbone

Storylets in `ModuleData/Enlisted/Storylets/`, state in `QualityStore`/`FlagStore`, durable engagements as `Activity` subclasses. Full rules + save-definer offset table + enum disjointness in [src/Features/Content/AGENTS.md](src/Features/Content/AGENTS.md) and [src/Mod.Core/SaveSystem/AGENTS.md](src/Mod.Core/SaveSystem/AGENTS.md).

---

## Code Standards

- Braces required on all control statements (no single-line `if`)
- Error reporting uses `ModLogger.Surfaced/Caught/Expected` — full discipline + string-literal scanner rules in [Tools/Validation/AGENTS.md](Tools/Validation/AGENTS.md).
- Localized strings: `new TextObject("{=id}Fallback")`
- Private fields: `_camelCase`
- Comments describe current behavior — never changelogs, PR references, or "added for X"

### Safe Patterns

```csharp
// Deferred menu activation
NextFrameDispatcher.RunNextFrame(() => GameMenu.ActivateGameMenu("menu_id"));
// Item comparison by StringId (not reference equality)
if (element.Item.StringId == targetItem.StringId)
// Settlement safety check
if (!PlayerEncounter.InsideSettlement) PlayerEncounter.Finish();
// Centralized manager for reputation/needs changes
EscalationManager.Instance.ModifyReputation(ReputationType.Soldier, 5, "reason");
```

---

## Platform notes (Windows + WSL)

This repo is developed on both Windows Git Bash and WSL Linux. Detect with `uname -s` (`MINGW64_NT-*` = Git Bash, `Linux` = WSL). Quirks differ:

**Windows Git Bash:**

- Unix paths (`/dev/null`, forward slashes), not `NUL` or backslashes
- Thin shim: `cat`, `head`, `tail`, `grep`, `file`, `which` not on PATH — use Grep/Read/Write tools instead
- `dotnet`, `git`, `python` not on PATH: `export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"`. Python at `/c/Python313/python.exe`
- Multi-line commit messages: no `cat` heredoc — write to a temp file and `git commit -F <file>`

**WSL Linux:**

- Standard Unix tools native; `dotnet`, `git`, `python3` on PATH at `/usr/bin/...`
- Windows `.exe` interop is broken in this environment — `cmd.exe`, `powershell.exe`, `ssh.exe` all return "Invalid argument". Don't invoke Windows binaries from WSL.
- For GitHub, this repo is wired with HTTPS + `gh` credential helper (see `git config --local --get-regexp '^(credential|url|remote)'`)
- Game install + Windows checkout accessible via `/mnt/c/...` mounts. Steam Bannerlord at `/mnt/c/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord`; canonical Windows checkout at `/mnt/c/dev/Enlisted/Enlisted`

**Both environments:**

- Build form `/p:Platform=x64` trips bash's argument parser when config has a space. Use: `dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64`
- Stage with `git add <path>`, never `git add -A` — concurrent AI sessions may be editing files
- Line endings enforced by `.gitattributes` (`.cs`/`.csproj`/`.sln`/`.ps1` = CRLF; everything else `text=auto`)

---

## Documentation & rules architecture

Rules and docs live in three layers. The decision rule for new info is: *would another contributor benefit?* → shared. *Same machine, different repo?* → user-global. *Just me, just here?* → private.

| Layer | Where | Audience | Lifecycle |
|---|---|---|---|
| Shared rules | `AGENTS.md` cascade — root + 8 nested files (see map below) | Codex, Claude Code, Cursor, Aider, humans | Committed; changes via PR |
| Per-user repo overrides | `CLAUDE.local.md` (per-level, Claude Code) / `AGENTS.override.md` (Codex) | The tool that owns the file | Gitignored, personal |
| Per-machine private | `~/.claude/projects/<repo>/memory/` (Claude Code only) | You, Claude Code | Outside repo entirely |

**Nested cascade map** — open the file in each subsystem you work in for full rules:

| Subsystem | File |
|---|---|
| Content / storylets / events / StoryDirector | [src/Features/Content/AGENTS.md](src/Features/Content/AGENTS.md) |
| Save system / offset table / serialization | [src/Mod.Core/SaveSystem/AGENTS.md](src/Mod.Core/SaveSystem/AGENTS.md) |
| ModuleData JSON authoring | [ModuleData/Enlisted/AGENTS.md](ModuleData/Enlisted/AGENTS.md) |
| Validator / error-codes / lint | [Tools/Validation/AGENTS.md](Tools/Validation/AGENTS.md) |
| Conversations / dialog tokens | [src/Features/Conversations/AGENTS.md](src/Features/Conversations/AGENTS.md) |
| Activities / Orders / Home | [src/Features/Activities/AGENTS.md](src/Features/Activities/AGENTS.md) |
| Specs / plans / verification conventions | [docs/superpowers/AGENTS.md](docs/superpowers/AGENTS.md) |
| Build / deploy / Workshop | [Tools/AGENTS.md](Tools/AGENTS.md) |

Both Codex (cascades root → CWD) and Claude Code (auto-discovers sibling CLAUDE.md shims that import each AGENTS.md) load these on demand when you work in the matching subtree.

---

## AI Maintainability Priorities

Repo-local rules win over generic style defaults. Run `Tools/Validation/lint_repo.ps1` — it encodes everything machine-checkable across `.editorconfig` (C#/JSON/XML), `ruff.toml` (Python), `PSScriptAnalyzerSettings.psd1` (PowerShell).

Rules generic AI defaults won't catch:

- **Don't reformat unrelated lines** — match surrounding code; let `.editorconfig` drive
- **Don't invent a new manager / store / catalog / behavior / runtime** without grepping for an existing one
- **Don't bundle refactor + behavior change + content migration in one patch** unless required; smallest stable surface first
- **When a change crosses C# + content boundaries** (loader + JSON, validator + schema), update both sides in the same commit
- **Use `nameof(...)`** for member/type names — not for player-facing text, loc fallbacks, or content IDs
- **Route player/dev-visible failures through `ModLogger`** (Surfaced / Caught / Expected); catch only what you can actually handle

---

## Project Structure

```
src/Features/          C# gameplay features
ModuleData/Enlisted/   JSON events, storylets, incidents
ModuleData/Languages/  enlisted_strings.xml (localization)
docs/                  All documentation (see docs/INDEX.md)
Tools/Validation/      Validators (run before commit)
../Decompile/          Bannerlord API reference — AUTHORITATIVE (Windows: sibling of repo; WSL: ./Decompile/ inside repo, gitignored). Regenerate per Quick Commands.
```

### Key Feature Folders (one-line summaries)

| Folder | Responsibility |
|---|---|
| `Enlistment/` | Service state, retirement |
| `Content/` | Storylets + triggers + effects + StoryDirector (pacing gate). Orders driven by storylets (legacy `Orders/` retired 2026-04-21) |
| `Qualities/` | Typed numeric state (scrutiny, supplies, readiness, loyalty, lord_relation, rank_xp). Daily decay |
| `Flags/` | Named boolean state, global + hero-scoped, with expiry |
| `Activities/` | Stateful ticking activities with intent-biased phase pools (Banner-Kings Feast pattern) |
| `Escalation/` | Reputation, scrutiny/discipline |
| `Company/` | Readiness, supply needs |
| `Equipment/` | Quartermaster, gear |

---

## Pre-Commit Checklist

- [ ] APIs verified against the local Decompile tree (`../Decompile/` on Windows or `./Decompile/` on WSL — regenerate if the install has been patched since last run)
- [ ] New C# files added to `Enlisted.csproj`
- [ ] JSON field order correct (fallback after ID)
- [ ] Tooltips on all event options (<80 chars)
- [ ] `python Tools/Validation/validate_content.py` passes
- [ ] `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds

---

## Key Documentation

Link, don't duplicate — open these for depth:

| Topic | File |
| :--- | :--- |
| Master documentation catalog | [docs/INDEX.md](docs/INDEX.md) |
| Current project status | [docs/superpowers/STATUS.md](docs/superpowers/STATUS.md) |
| Architecture briefs (cross-cutting) | [docs/architecture/](docs/architecture/) |
| Writing style (voice, tone) | [docs/Features/Content/writing-style-guide.md](docs/Features/Content/writing-style-guide.md) |
| Error code registry (auto-generated) | [docs/error-codes.md](docs/error-codes.md) |
| Storylet backbone (content layer, Spec 0) | [docs/Features/Content/storylet-backbone.md](docs/Features/Content/storylet-backbone.md) |
| Event pacing (delivery layer) | [docs/superpowers/specs/archive/2026-04-18-event-pacing-design.md](docs/superpowers/specs/archive/2026-04-18-event-pacing-design.md) |
| Build & deploy scripts | [Tools/AGENTS.md](Tools/AGENTS.md) |
| Validation / error-codes / lint | [Tools/Validation/AGENTS.md](Tools/Validation/AGENTS.md) |
| Logging, saves, dialogue, menu patterns | [Tools/TECHNICAL-REFERENCE.md](Tools/TECHNICAL-REFERENCE.md) |
| Validation tool reference | [Tools/README.md](Tools/README.md) |

---

## Common Pitfalls (project-wide)

Subsystem-specific pitfalls live in the matching nested AGENTS.md (see Documentation & rules architecture above). Cross-cutting pitfalls:

1. `ChangeHeroGold` instead of `GiveGoldAction` (Rule #3)
2. `Enum.GetValues` for equipment iteration (Rule #4)
3. Tracking a hero without checking `IsAlive` (Rule #5)
4. `PlayerEncounter.Finish()` while inside a settlement
5. Forgetting to add new files to `.csproj` (Rule #2)
6. Missing tooltips on event options (Rule #7 → [ModuleData/Enlisted/AGENTS.md](ModuleData/Enlisted/AGENTS.md))
7. Wrong JSON field order — ID and fallback not adjacent (Rule #6 → [ModuleData/Enlisted/AGENTS.md](ModuleData/Enlisted/AGENTS.md))
8. Not persisting in-progress flags in `SyncData()` (Rule #8 → [src/Mod.Core/SaveSystem/AGENTS.md](src/Mod.Core/SaveSystem/AGENTS.md))
9. Missing `SaveableTypeDefiner` registration (Rule #8 → [src/Mod.Core/SaveSystem/AGENTS.md](src/Mod.Core/SaveSystem/AGENTS.md))
10. Relying on external API docs (wrong version) — see Rule #1, NEVER use online docs for TaleWorlds
11. Creating mod-spawned heroes with `Occupation.Wanderer` triggers vanilla wanderer-introduction dialogue. Use `Occupation.Soldier`. Verified in the developer-local decompile (see Critical Rule #1 for location): `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs:607` (`AddWandererConversations`) and `:1274` (`conversation_wanderer_on_condition`, checks `Occupation == Occupation.Wanderer`).

Subsystem-specific (open the nested AGENTS.md for full rules):

- Content / StoryDirector / scripted-effects: [src/Features/Content/AGENTS.md](src/Features/Content/AGENTS.md)
- Save system / offsets: [src/Mod.Core/SaveSystem/AGENTS.md](src/Mod.Core/SaveSystem/AGENTS.md)
- ModLogger interpolated strings: [Tools/Validation/AGENTS.md](Tools/Validation/AGENTS.md)
- Activities / Orders read-only writes / throttle sentinels: [src/Features/Activities/AGENTS.md](src/Features/Activities/AGENTS.md)
- Plan-vs-codebase drift: [docs/superpowers/AGENTS.md](docs/superpowers/AGENTS.md)
- Dialog token interpolation: [src/Features/Conversations/AGENTS.md](src/Features/Conversations/AGENTS.md)

---

## Deprecated Systems

- **Morale System** — Removed 2026-01-11, save-load only
- **Company Rest** — Removed 2026-01-11, save-load only
- Player Fatigue (0-24 budget) remains functional

---

## Diagnostic Logs

When debugging runtime issues, check both log sources. Paths are Windows-side; from WSL Linux prepend `/mnt/c/` (so `C:\ProgramData\…` becomes `/mnt/c/ProgramData/…`).

- **Native Bannerlord logs** — `C:\ProgramData\Mount and Blade II Bannerlord\`
  WSL: `/mnt/c/ProgramData/Mount and Blade II Bannerlord/`
  (engine crashes, save errors, low-level TaleWorlds output)
- **Enlisted mod logs** — `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\`
  WSL: `/mnt/c/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Enlisted/Debugging/`
  (`ModLogger` output, session logs, conflict reports, validation reports)

Details on log categories, session naming (`Session-A_*.log`), and error-code conventions live in [Tools/TECHNICAL-REFERENCE.md](Tools/TECHNICAL-REFERENCE.md).

---

## External Resources

- Steam Workshop: <https://steamcommunity.com/sharedfiles/filedetails/?id=3621116083>
- Requires: Harmony for Bannerlord

---

**When in doubt, check `Decompile/`. Never hallucinate APIs.**
