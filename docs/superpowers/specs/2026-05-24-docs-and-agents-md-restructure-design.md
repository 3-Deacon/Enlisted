# Docs + AGENTS.md Restructure — Design Spec

**Date:** 2026-05-24
**Author:** Brainstormed with Claude (superpowers:brainstorming)
**Status:** Approved by user (2026-05-24), ready for writing-plans skill

---

## Problem

The Enlisted repo currently centralizes all AI-context rules in a single 410-line `AGENTS.md` plus a 132-line `CLAUDE.md` shim and a 31-line `.gemini/GEMINI.md` that doesn't integrate with AGENTS.md at all. Symptoms:

1. **Context bloat** — every Codex/Claude session loads the full 410-line root file regardless of which subsystem the work touches.
2. **Discoverability** — 121 markdown files across `docs/`, with mismatches between `docs/Features/<X>/` and `src/Features/<X>/` folder names; no clear pointer from a code location to its design docs.
3. **Duplication and drift** — rules appear in `BLUEPRINT.md` (2026-04-19) and `AGENTS.md` and `CLAUDE.md` simultaneously; BLUEPRINT.md is now superseded but not retired.
4. **Workflow friction** — three tools (Codex, Claude Code, Gemini CLI), three context-loading paths, no clean separation of what goes in shared rules vs Claude-only vs per-user/machine.

## Solution: Layered Mirror

Two parallel hierarchies, joined by AGENTS.md cross-links, plus a clear three-layer memory model.

- **Code tree** carries `AGENTS.md` files at subsystem level — *rules, gotchas, patterns*. Loaded by Codex on path-walk; loaded by Claude Code on-demand when files in that subtree are touched.
- **docs tree** carries the *handbook* — *design intent, living references, spec dumps*. Mirrors the code tree folder-for-folder.
- **Memory tiers** are explicit: shared (AGENTS.md cascade), per-user repo (`CLAUDE.local.md`, `AGENTS.override.md`), per-machine private (`~/.claude/projects/.../memory/`).

Codex (cascading hierarchical AGENTS.md, walks root → CWD) and Claude Code (on-demand nested CLAUDE.md / AGENTS.md discovery, with `@import` shim) both read the same files. No format duplication required.

---

## Detailed Design

### 1. AGENTS.md cascade

**Root** (`./AGENTS.md`, target ~150 lines, down from 410): project overview, quick commands, critical rules 1-9 (Gold, Equipment, Hero safety, JSON order, tooltips, save-system overview, Windows/WSL portability, StoryDirector intro), code standards (ModLogger discipline), project structure, pre-commit checklist, deprecated systems, diagnostic logs, **Platform notes (Windows / WSL)** section promoted up from CLAUDE.md, and a new **"Documentation & rules architecture"** section (~20 lines) that explains the three-layer model and cascade map.

**Nested AGENTS.md** — 8 files, only where rule density justifies them:

| Path | Target lines | Owns |
|---|---|---|
| `src/Features/Content/AGENTS.md` | ~100 | Storylet backbone (current Rule #11), StoryDirector routing (current Rule #10), scripted-effects catalog discipline, ModalEventBuilder pipeline, current pitfalls #12 #14 #16 #17 #18 #21 #22, plan-vs-decompile API divergence |
| `src/Mod.Core/SaveSystem/AGENTS.md` | ~120 | Full save-definer offset table (currently inside Rule #11 + Pitfall #15), class-vs-enum disjointness, `EnsureInitialized` pattern, HashSet-not-supported, `Campaign.Current.X`-backed statics null-at-OnGameStart (current pitfalls #8 #9), `SaveableTypeDefiner` registration discipline (current Rule #8) |
| `ModuleData/Enlisted/AGENTS.md` | ~60 | JSON field order (current Rule #6), tooltip rules (current Rule #7), inline `{=key}Fallback` localization convention, `Enlisted.csproj` `AfterBuild` requirements per content dir, wildcard-non-recursive quirk |
| `Tools/Validation/AGENTS.md` | ~60 | Validator phases 1-21 (purpose of each), `ModLogger.Surfaced/Caught/Expected` string-literal scanner rules, error-codes registry regen workflow, lint stack invocation |
| `src/Features/Conversations/AGENTS.md` | ~40 | Token interpolation discipline (current Pitfall #23) — the six required tokens, where to wire `MBTextManager.SetTextVariable` calls |
| `src/Features/Orders/AGENTS.md` | ~35 | Read-only quality writes (current Pitfall #13), `int.MinValue` throttle sentinel (current Pitfall #19), Orders-surface storylet patterns, `OrdersNewsFeedThrottle` SpeedUp-multiplier rule (current Pitfall #21 sub-rule), legacy `src/Features/Orders/` retired note |
| `docs/superpowers/AGENTS.md` | ~30 | Plan-vs-codebase drift (current Pitfall #22), "API corrections appendix" pattern, verification doc convention, doc-comment rule (no forward-spec, no change history) |
| `Tools/AGENTS.md` | ~50 | Build configurations (folded from current `docs/BUILD-CONFIGURATIONS.md`), validator phase model, error-codes regen, lint stack, deploy scripts |

**Total budget:** root 150 + 8 nested ≈ 445 lines distributed (vs 410 currently centralized). Slight growth, large context-locality win — a session touching `src/Features/Content/EventChainManager.cs` loads root + Content + (if it touches saves) SaveSystem, never the storylet validator phase model or the JSON authoring rules.

**Cross-link template** (every nested file):

```markdown
# AGENTS.md — <subsystem>

> Parent: [../../AGENTS.md](../../AGENTS.md) (project root)
> Handbook: [docs/Features/<X>/](../../docs/Features/<X>/) (design intent + living refs)

[rules, gotchas, patterns]

## See also
- [Related spec or living reference]
```

### 2. docs/ tree alignment (mirror)

**Principle:** every `src/Features/<X>/` with design docs has a `docs/Features/<X>/` folder using the same name. If no design docs exist for a subsystem, no folder. Cross-cutting docs (research, native API analysis) live outside `Features/` in `Reference/` or `architecture/`.

**Direct matches today (8, leave alone):** CampaignIntelligence, Ceremonies, Combat, Companions, Content, Equipment, Identity, Patrons.

**Renames / restructures:**

| Current | Action | Reason |
|---|---|---|
| `docs/Features/Campaign/` (5 files) | Rename → `docs/Features/Camp/` | Mirrors `src/Features/Camp/`. Folder is about Camp Life, not Campaign |
| `docs/Features/UI/` (5 files) | Rename → `docs/Features/Interface/` | Mirrors `src/Features/Interface/`. Code wins (less code churn than renaming src) |
| `docs/Features/Core/` (9 files) | **Split per-file** by subsystem | None map to a "Core" src folder. `enlistment.md`, `pay-system.md`, `muster-system.md` → `Enlistment/`; `retinue-system.md` → `Retinue/`; `promotion-system.md` → `Ranks/`; `company-events.md` → `Company/`; `companion-management.md` → `Companions/`; `core-gameplay.md` → `docs/PROJECT-OVERVIEW.md` (standalone, player-facing) |
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
Senior C# Bannerlord modder for the Enlisted project. Read AGENTS.md before answering.

## Reasoning
Use Deep Think for AI-behavior, campaign-map, or complex Harmony patches. Explain "why" briefly for architectural choices.

## Authoritative source
All project rules, commands, gotchas, and standards live in [AGENTS.md](../AGENTS.md). Do NOT re-derive guidance — defer to AGENTS.md and its nested files.
```

Removes the hardcoded `/home/onebodyamerica/Projects/Enlisted` user-path bug (line 9 currently). Removes generic C# game advice that AGENTS.md covers better.

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
1. **Fail-closed:** Every `src/Features/<X>/AGENTS.md` has a sibling `docs/Features/<X>/` folder.
2. **Warning:** Every nested AGENTS.md links UP to root and SIDEWAYS to its docs folder per the cross-link template.
3. **Warning:** Every `docs/Features/<X>/` folder has an `index.md`.
4. **Warning:** `docs/INDEX.md` lists every nested AGENTS.md path.
5. **Warning:** AGENTS.md files >300 lines (signal to split or shed).
6. **Fail-closed:** `@import` paths in `CLAUDE.md` resolve to existing files.

**Out of scope:**
- Content quality / staleness audits
- Whether rules are actually followed in code (existing `validate_content.py` phases handle this)
- Memory directory consistency (lives outside repo)

**Hook point:** Phase 21 of `Tools/Validation/validate_content.py`. `lint_repo.ps1` continues to invoke the full stack. Pre-commit checklist in AGENTS.md root gains a "validate_content.py passes Phase 21" line.

### 6. Migration approach

**Strategy:** phased, additive-then-shrink. Each phase is one commit, independently reviewable and revertable. No big-bang.

| Phase | Commit subject | What lands | Verification |
|---|---|---|---|
| **A** | `docs: add nested AGENTS.md cascade (additive)` | Create all 8 nested AGENTS.md by copying relevant sections from root. For each new `src/Features/<X>/AGENTS.md` whose `docs/Features/<X>/` folder doesn't yet exist (Conversations, Orders), create the folder with a stub `index.md` pointing back at the AGENTS.md. Root unchanged; both old and new context coexist | `dotnet build` clean; manual session-load test from a code subdirectory |
| **B** | `docs: lift project status to docs/superpowers/STATUS.md` | Create new STATUS.md from CLAUDE.md current-status section. Add pointer in AGENTS.md root | Manual: confirm STATUS renders, links resolve |
| **C** | `docs: shrink CLAUDE.md and GEMINI.md to compatibility shims` | CLAUDE.md → ~25 lines; GEMINI.md → ~10 lines. Add `CLAUDE.local.md` to `.gitignore` | Manual: fresh-open both, confirm only Claude/Gemini-specific guidance remains |
| **D** | `docs: shrink root AGENTS.md (content now in nested cascade)` | Remove duplicated content from root AGENTS.md. Promote "Platform notes" from CLAUDE.md to AGENTS.md root. Root → ~150 lines | `dotnet build` clean; manual: open Codex session and confirm rule cascade resolves correctly |
| **E** | `docs: align docs/Features/ tree to src/Features/ (mirror)` | All renames: Campaign→Camp, UI→Interface, Core/ split, Technical/ split + delete, core-gameplay.md→PROJECT-OVERVIEW.md. Use `git mv` to preserve history | Phase 21 validator passes; grep for internal links to renamed files, fix |
| **F** | `docs: archive BLUEPRINT + DEVELOPER-GUIDE, fold BUILD-CONFIGURATIONS, rewrite INDEX` | Grep-and-rescue unique content into right nested AGENTS.md, then `git mv` to `docs/Archive/`. Fold BUILD-CONFIGURATIONS into `Tools/AGENTS.md`. Rewrite `INDEX.md` as ~150-line catalog | Phase 21 passes; manual: read new INDEX, confirm every nested AGENTS.md and docs folder catalogued |
| **G** | `tools: add validate_docs_structure.py (Phase 21)` | Mirror validator. Hook into `validate_content.py`. Initial run produces clean | Phase 21 runs clean on the full restructured tree |

**Estimated effort:** ~4-6 hours total across all phases.

**Coordination with in-flight feature branches:** `feature/plan4-officer-trajectory` and `feature/plan5-endeavor-system` will hit merge conflicts on `AGENTS.md` and `CLAUDE.md` when they rebase post-restructure. Conflicts are mechanical (their branches edit pre-shrink content; restructure moved that content elsewhere). Plan: rebase those branches onto `development` post-Phase D as a one-time pain.

**Rollback plan:** Each phase is one commit. `git revert <sha>` undoes any phase cleanly. Phase A is fully reversible (purely additive). Phases B-G touch overlapping content but each leaves a coherent state.

---

## Acceptance Criteria

1. Root `AGENTS.md` ≤ 200 lines.
2. 8 nested `AGENTS.md` files exist at the paths in §1, each within its target line budget ± 30%.
3. `CLAUDE.md` ≤ 40 lines and is a `@AGENTS.md` + Claude-specifics shim.
4. `.gemini/GEMINI.md` ≤ 15 lines and contains no hardcoded user paths.
5. Every `src/Features/<X>/AGENTS.md` has a matching `docs/Features/<X>/` folder; every nested file follows the cross-link template.
6. `docs/Features/Campaign/` renamed to `Camp/`; `UI/` renamed to `Interface/`; `Core/` split into per-subsystem folders; `Technical/` files relocated and folder deleted.
7. `BLUEPRINT.md` and `DEVELOPER-GUIDE.md` archived to `docs/Archive/`; any unique content rescued into nested AGENTS.md.
8. `BUILD-CONFIGURATIONS.md` folded into `Tools/AGENTS.md` and deleted from `docs/`.
9. `docs/INDEX.md` rewritten as master catalog of nested AGENTS.md + docs/Features/ + Reference/ + superpowers/.
10. `docs/superpowers/STATUS.md` exists and holds current project status (lifted from old CLAUDE.md).
11. `CLAUDE.local.md` added to `.gitignore`.
12. `Tools/Validation/validate_docs_structure.py` exists and runs as `validate_content.py` Phase 21. Initial run passes clean.
13. `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds at every commit boundary.
14. All 7 commits (phases A-G) landed on `development`, each independently revertable.

## Out of Scope

- Content quality / staleness audit of individual docs (handled separately if needed; this restructure preserves content even when moving it)
- Renames inside `src/` to align with `docs/` (one direction only: docs follows code)
- Splitting plans/specs in `docs/superpowers/` (existing structure preserved per "tooling-referenced paths" finding)
- New documentation creation beyond what's needed to fill the restructured tree
- Changes to `~/.claude/` global config (user-level; out of repo scope)
- Coordination with consumers of external doc URLs (per user: no constraints — anyone with a stale link follows their nose)
