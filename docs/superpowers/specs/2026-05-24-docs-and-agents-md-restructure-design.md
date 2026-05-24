# Docs + AGENTS.md Restructure — Design Spec

**Date:** 2026-05-24
**Author:** Brainstormed with Claude (superpowers:brainstorming)
**Status:** Revised 2026-05-24 after code-review found 10 issues. Awaiting re-review before writing-plans handoff.

## Precondition: green baseline

The build on `development` HEAD (`dd6cc45`) currently fails with 11 compile errors from a Bannerlord game-update API drift in `src/Features/CampaignIntelligence/Models/` (new abstract methods on `TargetScoreCalculatingModel`, `MobilePartyAIModel`, `ArmyManagementCalculationModel`) and `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs` (missing `MissionAgentSpawnLogic`). `validate_content.py` may also fail (Phase 10 `--check` against error-code drift not yet verified on Linux).

This restructure does NOT include those fixes. They are pre-restructure work, landed as their own commit on `development`. Acceptance criteria in this spec assume a green `dotnet build` and `validate_content.py` on the starting commit.

---

## Problem

The Enlisted repo currently centralizes all AI-context rules in a single 410-line `AGENTS.md` plus a 132-line `CLAUDE.md` shim and a 31-line `.gemini/GEMINI.md` that doesn't integrate with AGENTS.md at all. Symptoms:

1. **Context bloat** — every Codex/Claude session loads the full 410-line root file regardless of which subsystem the work touches.
2. **Discoverability** — 121 markdown files across `docs/`, with mismatches between `docs/Features/<X>/` and `src/Features/<X>/` folder names; no clear pointer from a code location to its design docs.
3. **Duplication and drift** — rules appear in `BLUEPRINT.md` (2026-04-19) and `AGENTS.md` and `CLAUDE.md` simultaneously; BLUEPRINT.md is now superseded but not retired.
4. **Workflow friction** — three tools (Codex, Claude Code, Gemini CLI), three context-loading paths, no clean separation of what goes in shared rules vs Claude-only vs per-user/machine.

## Solution: Layered Mirror

Two parallel hierarchies, joined by per-subsystem AGENTS.md + a CLAUDE.md shim per subsystem, plus a three-layer memory model.

- **Code tree** carries `AGENTS.md` files at subsystem level — *rules, gotchas, patterns*. Loaded by Codex on path-walk (Codex cascades AGENTS.md root → CWD automatically). Loaded by Claude Code via a sibling `CLAUDE.md` 3-line shim file (`@AGENTS.md`) at each level — Claude Code auto-discovers nested `CLAUDE.md` on-demand when files in that subtree are read, but does NOT auto-discover nested `AGENTS.md`. The CLAUDE.md shim is what wires Claude Code into the cascade. (See [Anthropic memory docs](https://code.claude.com/docs/en/memory) — nested discovery is for `CLAUDE.md`/`CLAUDE.local.md` only.)
- **docs tree** carries the *handbook* — *design intent, living references, spec dumps*. Mirrors the code tree folder-for-folder.
- **Memory tiers** are explicit: shared (AGENTS.md cascade), per-user repo (`CLAUDE.local.md`, `AGENTS.override.md`), per-machine private (`~/.claude/projects/.../memory/`).

Both tools (Codex and Claude Code) read the same actual content — AGENTS.md per subsystem. The per-subsystem `CLAUDE.md` shim contains nothing but `@AGENTS.md`, so no duplication.

---

## Detailed Design

### 1. AGENTS.md cascade

**Root** (`./AGENTS.md`, target ~150 lines, down from 410): project overview, quick commands, critical rules 1-9 (Gold, Equipment, Hero safety, JSON order, tooltips, save-system overview, Windows/WSL portability, StoryDirector intro), code standards (ModLogger discipline), project structure, pre-commit checklist, deprecated systems, diagnostic logs, **Platform notes (Windows / WSL)** section promoted up from CLAUDE.md, and a new **"Documentation & rules architecture"** section (~20 lines) that explains the three-layer model and cascade map.

**Nested AGENTS.md + CLAUDE.md shim** — 8 subsystems, 16 files total (8 AGENTS.md + 8 CLAUDE.md shims), only where rule density justifies them:

| Path | Target lines | Owns |
|---|---|---|
| `src/Features/Content/AGENTS.md` | ~100 | Storylet backbone (current root-AGENTS Rule #11), StoryDirector routing (current Rule #10), scripted-effects catalog discipline, ModalEventBuilder pipeline, current pitfalls #12 #14 #16 #17 #18 #21 #22, plan-vs-decompile API divergence |
| `src/Mod.Core/SaveSystem/AGENTS.md` | ~120 | Full save-definer offset table (currently inside root-AGENTS Rule #11 + Pitfall #15), class-vs-enum disjointness, `EnsureInitialized` pattern, HashSet-not-supported, `Campaign.Current.X`-backed statics null-at-OnGameStart (current pitfalls #8 #9), `SaveableTypeDefiner` registration discipline (current Rule #8) |
| `ModuleData/Enlisted/AGENTS.md` | ~60 | JSON field order (current Rule #6), tooltip rules (current Rule #7), inline `{=key}Fallback` localization convention, `Enlisted.csproj` `AfterBuild` requirements per content dir (**authoring discipline only — `validate_content.py` does NOT currently enforce csproj↔ModuleData parity; `content_dirs_to_check` at `Tools/Validation/validate_content.py:1475` is empty. Adding enforcement is a follow-up TODO**), wildcard-non-recursive quirk |
| `Tools/Validation/AGENTS.md` | ~60 | Validator phases 1-21 (purpose of each), `ModLogger.Surfaced/Caught/Expected` string-literal scanner rules, error-codes registry regen workflow, lint stack invocation |
| `src/Features/Conversations/AGENTS.md` | ~40 | Token interpolation discipline (current Pitfall #23) — the six required tokens, where to wire `MBTextManager.SetTextVariable` calls |
| `src/Features/Activities/AGENTS.md` | ~50 | Activities runtime + intent-biased phase pools. **Includes** the Orders sub-subsystem (located at `src/Features/Activities/Orders/`, NOT `src/Features/Orders/` which was retired 2026-04-21): read-only quality writes (current Pitfall #13), `int.MinValue` throttle sentinel (current Pitfall #19), Orders storylet patterns, `OrdersNewsFeedThrottle` SpeedUp-multiplier rule (current Pitfall #21). Also covers Home subdir |
| `docs/superpowers/AGENTS.md` | ~30 | Plan-vs-codebase drift (current Pitfall #22), "API corrections appendix" pattern, verification doc convention, doc-comment rule (no forward-spec, no change history) |
| `Tools/AGENTS.md` | ~50 | Build configurations (folded from current `docs/BUILD-CONFIGURATIONS.md`), validator phase model, error-codes regen, lint stack, deploy scripts |

**Each AGENTS.md has a sibling CLAUDE.md shim** (3 lines, identical content across all):
```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

This is the wiring that makes Claude Code load the nested AGENTS.md content — without the sibling CLAUDE.md, Claude Code only auto-discovers `CLAUDE.md`/`CLAUDE.local.md` files, not `AGENTS.md`.

**Total budget:** root AGENTS.md ~150 lines + 8 nested AGENTS.md ~445 lines + 8 × 3-line CLAUDE.md shims ~24 lines ≈ 619 lines distributed (vs 410 currently in one root file). Marginal aggregate growth, large context-locality win — a Claude/Codex session touching `src/Features/Content/EventChainManager.cs` loads root AGENTS.md + Content AGENTS.md (via Claude's sibling CLAUDE.md discovery, or via Codex's path-cascade), never the storylet validator phase model or the JSON authoring rules.

**Cross-link template** — the relative path depths differ per file location. The mirror validator (§5, Phase B) computes correct depth automatically. Pattern at the top of every nested AGENTS.md:

```markdown
# AGENTS.md — <subsystem>

> Parent: [<COMPUTED_PATH_TO_ROOT>/AGENTS.md] (project root)
> Handbook: [<COMPUTED_PATH_TO_DOCS>/Features/<X>/] (design intent + living refs)

[rules, gotchas, patterns]

## See also
- [Related spec or living reference]
```

Depth reference table for the 8 nested locations (validator computes these):

| AGENTS.md location | Depth from root | Path to root AGENTS.md | Path to docs/ |
|---|---|---|---|
| `src/Features/Content/AGENTS.md` | 3 | `../../../AGENTS.md` | `../../../docs/Features/Content/` |
| `src/Mod.Core/SaveSystem/AGENTS.md` | 3 | `../../../AGENTS.md` | `../../../docs/architecture/` (no Features mirror — cross-cutting) |
| `ModuleData/Enlisted/AGENTS.md` | 2 | `../../AGENTS.md` | `../../docs/Features/Content/` (content authoring lives in Content handbook) |
| `Tools/Validation/AGENTS.md` | 2 | `../../AGENTS.md` | `../../docs/INDEX.md` (no dedicated mirror — Tools/ docs in INDEX) |
| `src/Features/Conversations/AGENTS.md` | 3 | `../../../AGENTS.md` | `../../../docs/Features/Conversations/` (created Phase A as stub if absent) |
| `src/Features/Activities/AGENTS.md` | 3 | `../../../AGENTS.md` | `../../../docs/Features/Activities/` (created Phase A as stub if absent) |
| `docs/superpowers/AGENTS.md` | 2 | `../../AGENTS.md` | `../../docs/superpowers/` (self) |
| `Tools/AGENTS.md` | 1 | `../AGENTS.md` | `../docs/INDEX.md` |

### 2. docs/ tree alignment (mirror)

**Principle:** every `src/Features/<X>/` with design docs has a `docs/Features/<X>/` folder using the same name. If no design docs exist for a subsystem, no folder. Cross-cutting docs (research, native API analysis) live outside `Features/` in `Reference/` or `architecture/`.

**Direct matches today (8, leave alone):** CampaignIntelligence, Ceremonies, Combat, Companions, Content, Equipment, Identity, Patrons.

**Renames / restructures:**

| Current | Action | Reason |
|---|---|---|
| `docs/Features/Camp/` (5 files) | Rename → `docs/Features/Camp/` | Mirrors `src/Features/Camp/`. Folder is about Camp Life, not Campaign |
| `docs/Features/Interface/` (5 files) | Rename → `docs/Features/Interface/` | Mirrors `src/Features/Interface/`. Code wins (less code churn than renaming src) |
| `docs/Features/Core/` (9 files, **including `index.md`**) | **Split per-file** by subsystem | None map to a "Core" src folder. `enlistment.md`, `pay-system.md`, `muster-system.md` → `Enlistment/`; `retinue-system.md` → `Retinue/`; `promotion-system.md` → `Ranks/`; `company-events.md` → `Company/`; `companion-management.md` → `Companions/`; `core-gameplay.md` → `docs/PROJECT-OVERVIEW.md` (standalone, player-facing). **`index.md` is rewritten** as a redirect stub ("This folder was split 2026-05-24 — see X / Y / Z") OR merged into `docs/INDEX.md`'s subsystem catalog. Audit its existing stale links (e.g. `order-progression-system.md` at line 75, `../Gameplay/...` at line 96) and either fix or drop them during the rewrite |
| `docs/Features/Technical/` (3 files) | **Split per-file** + delete folder | `commander-track-schema.md` → `Orders/`; `conflict-detection-system.md` → `Content/`; `encounter-safety.md` → `docs/architecture/` |

**Top-level docs/ triage:**

| File | Action | Notes |
|---|---|---|
| `BLUEPRINT.md` (632 lines, last edit 2026-04-19) | **Grep-and-rescue → archive** to `docs/Archive/BLUEPRINT-2026-04-archived.md` with deprecation header | Pre-AGENTS.md era. Grep for any rule/pattern not already in AGENTS.md cascade; fold survivors into the right nested file before archiving the husk |
| `DEVELOPER-GUIDE.md` (580 lines, last edit 2026-04-19) | **Grep-and-rescue → archive** to `docs/Archive/DEVELOPER-GUIDE-2026-04-archived.md` | Same treatment. Onboarding bits likely already in AGENTS.md root + `Tools/Validation/AGENTS.md` |
| `BUILD-CONFIGURATIONS.md` (270 lines) | **Fold into `Tools/AGENTS.md`** | Build-system depth; doesn't need top-level placement |
| `PROJECT-RESOURCES.md` (146 lines) | **Keep as-is** | External links/refs/Workshop/credits; doesn't fit AGENTS.md format |
| `INDEX.md` (294 lines) | **Rewrite** as ~150-line master catalog | New role: list every nested AGENTS.md location + `docs/Features/` topics + `Reference/` artifacts + `superpowers/` specs/plans |
| `README.md` (88 lines) | **Keep, refresh** to point at AGENTS.md + INDEX |
| `error-codes.md` + `error-codes-archive.md` | **Keep as-is** | Auto-generated; tooling-referenced paths |
| `architecture/` (2 files) | **Keep as-is** | Cross-cutting design briefs |
| `Reference/` (11 files) | **Keep as-is** | Research artifacts; possibly archive stale (decompile-based analyses can rot when game updates) |
| `superpowers/` | **Keep as-is** | Tooling-referenced (validate_content.py Phase 10, generate_error_codes.py); plans archived as they ship per existing convention |
| (new) `docs/superpowers/STATUS.md` | **Create** | Lift the per-plan project status section out of CLAUDE.md |

**Net effect:** `docs/` shrinks from 121 files to ~115 (Core/ split adds 4 single-file folders, Technical/ files relocate, 2 archives, 1 fold).

### 3. CLAUDE.md / GEMINI.md shim structure

**`CLAUDE.md` (target ~25 lines, down from 132):**

```markdown
# CLAUDE.md
Claude-specific layer for Enlisted. AGENTS.md is canonical for all AI tools.

@AGENTS.md

## Claude-specific
- Recommended skills (existing table — superpowers, code-review, etc.)
- MCP server usage policy (Context7 for libs, MS Learn for .NET, ignore unrelated MCPs)
- Auto/plan mode hints
- Current project status: [docs/superpowers/STATUS.md](docs/superpowers/STATUS.md)
```

The current "Session-Specific Guidance (Shell & PATH / Build & commit / File handling)" section moves OUT of CLAUDE.md and INTO AGENTS.md root as a "Platform notes (Windows / WSL)" section, because Codex needs it too.

The current "Current project status" section moves OUT of CLAUDE.md and INTO new `docs/superpowers/STATUS.md` because (a) it changes on a different cadence than rules, (b) Codex never sees CLAUDE.md, (c) keeps CLAUDE.md a true thin shim.

**`.gemini/GEMINI.md` (target ~10 lines, down from 31):**

```markdown
# Identity
Senior C# Bannerlord modder for the Enlisted project.

@../AGENTS.md

## Reasoning
Use Deep Think for AI-behavior, campaign-map, or complex Harmony patches. Explain "why" briefly for architectural choices.
```

The `@../AGENTS.md` import is what actually loads AGENTS.md content into Gemini's context — markdown links don't auto-load. Verify Gemini CLI honors `@` syntax via [github.com/google-gemini/gemini-cli docs/cli/gemini-md.md](https://github.com/google-gemini/gemini-cli/blob/main/docs/cli/gemini-md.md). If `@` syntax not supported by current Gemini version, fall back to configuring `.gemini/settings.json` with an explicit context-file list per the agents.md spec ("Gemini CLI requires .gemini/settings.json configuration"). Phase D verification includes a session-load test confirming AGENTS.md content is actually in Gemini's context.

Removes the hardcoded `/home/onebodyamerica/Projects/Enlisted` user-path bug (current `.gemini/GEMINI.md:9`). Removes generic C# game advice that AGENTS.md covers better.

**`.gitignore` additions:** `CLAUDE.local.md` (per-user Claude override; parallels existing `AGENTS.override.md` Codex pattern).

**Symlink option (rejected):** `ln -s AGENTS.md CLAUDE.md` doesn't survive Windows checkouts cleanly. `@AGENTS.md` import is portable across Windows + WSL.

### 4. Memory system organization

**Three-layer model:**

| Layer | Where | Scope | Audience | Lifecycle |
|---|---|---|---|---|
| **Shared rules** | `AGENTS.md` cascade | Anyone on this repo | Codex, Claude, Cursor, Aider, humans | Committed; changes via PR |
| **Per-user repo overrides** | `CLAUDE.local.md` (per-level), `AGENTS.override.md` (Codex) | Your machine + your tool | The tool that owns the file | Gitignored, personal |
| **Per-machine private** | `~/.claude/projects/-home-onebodyamerica-Projects-Enlisted/memory/` | Just you, Claude Code | Claude Code only | Outside repo entirely |

**Decision rule for new info:**

> *Would another contributor benefit?* If yes → AGENTS.md cascade.
> *Would I need this in a different repo on the same machine?* If yes → global `~/.claude/CLAUDE.md`.
> *Only me, only this repo, only this machine?* → project memory dir.

**Audit of current memory dir** (2 entries as of 2026-05-24):

| File | Type | Disposition | Reason |
|---|---|---|---|
| `wsl-windows-interop-broken.md` | feedback | **Keep private** | This user's WSL interop is broken; not a project rule |
| `enlisted-three-copies-layout.md` | project | **Keep private** | This user has Windows + WSL + remote checkouts; other contributors have their own layouts |

No promotions to AGENTS.md from current memory.

**Things to capture into memory during the restructure** (so future sessions don't re-derive):
- The git-config recipe to defeat the global SSH rewrite + use gh credential helper (the specific 3 commands run on 2026-05-24)
- Note that `BLUEPRINT.md` and `DEVELOPER-GUIDE.md` were archived 2026-05-24 in case future sessions search for them

**Naming + index discipline going forward:**

- `MEMORY.md` is an index only — one line per entry, **soft cap 50 lines**
- Topic files use kebab-case slugs matching frontmatter `name:`
- Body structure: rule/fact → `**Why:**` line → `**How to apply:**` line
- Link related entries with `[[name]]`
- Frontmatter required: `name`, `description`, `metadata.type` ∈ {user, feedback, project, reference}

### 5. Cross-linking + mirror validator

**Cross-link template** — applied to every nested AGENTS.md (see §1 above).

Each `docs/Features/<X>/` folder gets an `index.md` (entry point) that links back to its code-tree AGENTS.md:

```markdown
# <Subsystem> — Design Reference
Rules and patterns live in [../../src/Features/<X>/AGENTS.md](../../src/Features/<X>/AGENTS.md).

[design intent, living refs, spec dumps]
```

**New section in root AGENTS.md — "Documentation & rules architecture" (~20 lines):**

Explains the three-layer model once, with the cascade map and a quick decision tree for "where do I put this rule / where does that doc live?" Anchored by a top-level file so contributors find it without spelunking.

**Mirror validator** (`Tools/Validation/validate_docs_structure.py`, invoked as `validate_content.py` Phase 21):

**Checks:**
1. **Fail-closed:** Every nested `AGENTS.md` (anywhere except root) has a sibling `CLAUDE.md` shim that contains `@AGENTS.md`.
2. **Fail-closed:** Every `src/Features/<X>/AGENTS.md` has a corresponding `docs/Features/<X>/` folder per the §1 depth table.
3. **Fail-closed:** Every `@import` path (`CLAUDE.md`, `GEMINI.md`, any nested CLAUDE.md shim) resolves to an existing file.
4. **Fail-closed:** Cross-language reference scan — `*.cs`, `*.json`, `*.csproj`, `*.xml`, `*.md` files anywhere in the repo MUST NOT reference moved/deleted doc paths. Build a "moved-or-deleted" path list from the migration commits, fail on any live reference to those paths that wasn't updated in the same commit.
5. **Warning:** Every nested AGENTS.md uses the cross-link template at the top with correctly-computed relative paths per the §1 depth table.
6. **Warning:** Every `docs/Features/<X>/` folder has an `index.md`.
7. **Warning:** `docs/INDEX.md` lists every nested AGENTS.md path.
8. **Warning:** AGENTS.md files >300 lines (signal to split or shed).

**Out of scope:**
- Content quality / staleness audits inside doc files (handled by grep-and-rescue in Phase G)
- Whether rules are actually followed in code (existing `validate_content.py` phases handle this)
- Memory directory consistency (lives outside repo)

**Reference-repair tool:** Before each rename phase, a helper script `Tools/Validation/find_stale_refs.py <old-path> <new-path>` ripgreps the whole repo (`*.cs`, `*.json`, `*.csproj`, `*.xml`, `*.md`) for references to `<old-path>` and either prints them for manual fix or auto-rewrites with `--apply`. The rename commit must include the ref updates in the same commit so the validator passes.

**Hook point:** Phase 21 of `Tools/Validation/validate_content.py`. `lint_repo.ps1` continues to invoke the full stack. Pre-commit checklist in AGENTS.md root gains a "validate_content.py passes Phase 21" line.

### 6. Migration approach

**Strategy:** phased, additive-then-shrink. Each phase is one commit, independently reviewable and revertable. No big-bang.

| Phase | Commit subject | What lands | Verification |
|---|---|---|---|
| **A** | `docs: add nested AGENTS.md + CLAUDE.md shims (additive)` | Create all 8 nested AGENTS.md + 8 sibling CLAUDE.md 3-line shims by copying relevant sections from root. For each new subsystem whose `docs/Features/<X>/` folder doesn't yet exist (Conversations, Activities), create the folder with a stub `index.md` pointing back at the AGENTS.md. Root unchanged; both old and new context coexist | `dotnet build` clean; manual session-load test from a code subdirectory (verify Claude loads the sibling CLAUDE.md and through it the AGENTS.md; verify Codex loads the AGENTS.md directly) |
| **B** | `tools: add validate_docs_structure.py (Phase 21 + find_stale_refs.py helper)` | Create validator and helper. Wire validator into `validate_content.py` as Phase 21. Initial run should pass against Phase A's tree | Phase 21 runs clean; helper script lists zero stale refs |
| **C** | `docs: lift project status to docs/superpowers/STATUS.md` | Create new STATUS.md from CLAUDE.md current-status section. Add pointer in AGENTS.md root | Manual: confirm STATUS renders, links resolve. Phase 21 passes |
| **D** | `docs: shrink CLAUDE.md and GEMINI.md to compatibility shims` | Root CLAUDE.md → ~25 lines; GEMINI.md → ~10 lines with `@../AGENTS.md` import. Add `CLAUDE.local.md` to `.gitignore`. Confirm Gemini actually loads AGENTS.md (see §3 Gemini fallback note if `@` syntax unsupported) | Manual: open Gemini session, confirm AGENTS.md content present in context. Phase 21 passes |
| **E** | `docs: shrink root AGENTS.md (content now in nested cascade)` | Remove duplicated content from root AGENTS.md. Promote "Platform notes" from CLAUDE.md to AGENTS.md root. Root → ~150 lines | `dotnet build` clean; manual: open Codex session and confirm rule cascade resolves correctly. Phase 21 passes |
| **F** | `docs: align docs/Features/ tree (mirror) + repair live references` | All renames in one commit per rename group, with **paired ref repair**: Campaign→Camp + ref repair; UI→Interface + ref repair; Core/ split (including index.md handling) + ref repair; Technical/ split + delete + ref repair; core-gameplay.md→PROJECT-OVERVIEW.md + ref repair. Use `git mv` for history; use `find_stale_refs.py --apply` to update all live references in the same commit. **Audit Camp/ (formerly Campaign/) docs for staleness** — `camp-simulation-system.md` and `camp-life-simulation.md` reference retired `CampOpportunityGenerator` / `ContentOrchestrator`; archive stale files to `docs/Archive/` or rewrite to current state in this commit | Phase 21 (including ref-scan check #4) passes after each sub-commit; `dotnet build` clean |
| **G** | `docs: archive BLUEPRINT + DEVELOPER-GUIDE + fold BUILD-CONFIGURATIONS, rewrite INDEX` | Grep-and-rescue unique content into right nested AGENTS.md, then `git mv` to `docs/Archive/` (preserves history). Fold BUILD-CONFIGURATIONS into `Tools/AGENTS.md`. Rewrite `INDEX.md` as ~150-line master catalog. Update `Enlisted.csproj:14` ref to BLUEPRINT.md (use find_stale_refs.py) | Phase 21 passes; `dotnet build` clean; manual: read new INDEX, confirm every nested AGENTS.md and docs folder catalogued |

**Estimated effort:** ~6-8 hours total across all phases (revised up from 4-6 to account for ref-repair work in F and the validator scope expansion in B).

**Coordination with in-flight feature branches:** `feature/plan4-officer-trajectory` and `feature/plan5-endeavor-system` will hit merge conflicts on `AGENTS.md` and `CLAUDE.md` when they rebase post-restructure. Conflicts are mechanical (their branches edit pre-shrink content; restructure moved that content elsewhere). Plan: rebase those branches onto `development` post-Phase E as a one-time pain.

**Rollback plan:** Each phase is one commit (Phase F may be 4-5 sub-commits, one per rename group). `git revert <sha>` undoes any phase cleanly. Phase A is fully reversible (purely additive). Phases B-G touch overlapping content but each leaves a coherent state.

---

## Acceptance Criteria

1. Root `AGENTS.md` ≤ 200 lines.
2. 8 nested `AGENTS.md` files exist at the paths in §1, each within its target line budget ± 30%.
3. 8 nested `CLAUDE.md` shim files exist (one sibling to each nested AGENTS.md), each containing only the import `@AGENTS.md` + a one-line description.
4. Root `CLAUDE.md` ≤ 40 lines and is a `@AGENTS.md` + Claude-specifics shim.
5. `.gemini/GEMINI.md` ≤ 15 lines, contains no hardcoded user paths, and uses `@../AGENTS.md` (or `.gemini/settings.json` configuration) such that Gemini sessions actually load AGENTS.md content.
6. Every nested `AGENTS.md` resolves its cross-link template paths correctly (per the §1 depth table); Phase 21 validates this.
7. `docs/Features/Camp/` renamed to `Camp/` with stale-content audit done; `UI/` renamed to `Interface/`; `Core/` split into per-subsystem folders (including `index.md` rewrite/retire); `Technical/` files relocated and folder deleted.
8. `BLUEPRINT.md` and `DEVELOPER-GUIDE.md` archived to `docs/Archive/` via `git mv` (history preserved); any unique content rescued into nested AGENTS.md.
9. `BUILD-CONFIGURATIONS.md` folded into `Tools/AGENTS.md` and removed from `docs/` via `git mv`.
10. `docs/INDEX.md` rewritten as ≤ 200-line master catalog of nested AGENTS.md + docs/Features/ + Reference/ + superpowers/.
11. `docs/superpowers/STATUS.md` exists and holds current project status (lifted from old CLAUDE.md).
12. `CLAUDE.local.md` added to `.gitignore`.
13. `Tools/Validation/validate_docs_structure.py` exists and runs as `validate_content.py` Phase 21. All check categories pass: fail-closed (#1-#4) and warnings (#5-#8) clean.
14. `Tools/Validation/find_stale_refs.py` exists and reports zero stale references repo-wide after Phase G lands.
15. `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds at every commit boundary. (Assumes precondition: green baseline.)
16. All commits (phases A-G, with F as multiple sub-commits) landed on `development`, each independently revertable.

## Out of Scope

- Content quality / staleness audit of individual docs (handled separately if needed; this restructure preserves content even when moving it)
- Renames inside `src/` to align with `docs/` (one direction only: docs follows code)
- Splitting plans/specs in `docs/superpowers/` (existing structure preserved per "tooling-referenced paths" finding)
- New documentation creation beyond what's needed to fill the restructured tree
- Changes to `~/.claude/` global config (user-level; out of repo scope)
- Coordination with consumers of external doc URLs (per user: no constraints — anyone with a stale link follows their nose)
