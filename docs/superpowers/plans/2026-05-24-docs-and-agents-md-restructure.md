# Docs + AGENTS.md Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the Enlisted repo's AI-context system from a single 410-line root `AGENTS.md` into a nested cascade (root + 8 subsystem-scoped files), each accompanied by a 3-line `CLAUDE.md` shim so both Codex and Claude Code load the right scope on demand. Align `docs/Features/` to mirror `src/Features/`. Archive superseded top-level docs. Add a validator (Phase 21 of `validate_content.py`) that enforces the structure going forward.

**Architecture:** Layered Mirror per spec §1-2 — two parallel hierarchies (code tree carries rules, docs tree carries handbook), joined by per-subsystem `AGENTS.md` files with `CLAUDE.md` siblings. Codex auto-cascades root → CWD; Claude Code auto-discovers nested `CLAUDE.md` and through them the sibling `AGENTS.md`. Three-layer memory model is documented but no memory files move (audit found current entries belong in private layer).

**Tech Stack:** Markdown files, Python 3 validators (`Tools/Validation/validate_content.py` Phase 21 + `find_stale_refs.py` helper), bash for `git mv` / ripgrep, `dotnet build` for verification.

**Source spec:** [docs/superpowers/specs/2026-05-24-docs-and-agents-md-restructure-design.md](../specs/2026-05-24-docs-and-agents-md-restructure-design.md) (see for full rationale, depth table, decision rules).

**Precondition status:** Green baseline verified via commit `d558ffc` (build clean, `validate_content.py` passes with warnings only). Phase A may begin.

**Execution venue:** Works in any branch (development directly or in a worktree). Spec §6 notes feature branches `feature/plan4-officer-trajectory` and `feature/plan5-endeavor-system` will need a rebase after Phase E; this is a one-time mechanical conflict resolution, not blocking this plan.

---

## File Structure

**Created (new files):**

| Path | Purpose |
|---|---|
| `src/Features/Content/AGENTS.md` | Subsystem rules: storylet backbone, StoryDirector, scripted-effects, ModalEventBuilder |
| `src/Features/Content/CLAUDE.md` | 3-line shim importing sibling AGENTS.md |
| `src/Mod.Core/SaveSystem/AGENTS.md` | Save-definer offset table + serialization gotchas |
| `src/Mod.Core/SaveSystem/CLAUDE.md` | Shim |
| `ModuleData/Enlisted/AGENTS.md` | JSON authoring rules, tooltips, localization |
| `ModuleData/Enlisted/CLAUDE.md` | Shim |
| `Tools/Validation/AGENTS.md` | Validator phases 1-21, ModLogger discipline, error-codes regen |
| `Tools/Validation/CLAUDE.md` | Shim |
| `src/Features/Conversations/AGENTS.md` | Token interpolation discipline |
| `src/Features/Conversations/CLAUDE.md` | Shim |
| `src/Features/Activities/AGENTS.md` | Activities runtime + Orders sub-subsystem rules |
| `src/Features/Activities/CLAUDE.md` | Shim |
| `docs/superpowers/AGENTS.md` | Plan-vs-codebase drift, API corrections appendix, doc-comment rule |
| `docs/superpowers/CLAUDE.md` | Shim |
| `Tools/AGENTS.md` | Build configurations, deploy scripts |
| `Tools/CLAUDE.md` | Shim |
| `docs/Features/Conversations/index.md` | Stub for new mirror folder |
| `docs/Features/Activities/index.md` | Stub for new mirror folder |
| `Tools/Validation/validate_docs_structure.py` | Mirror validator (Phase 21 of validate_content.py) |
| `Tools/Validation/find_stale_refs.py` | Helper script for rename ref-repair |
| `docs/superpowers/STATUS.md` | Lifted project status (from CLAUDE.md) |
| `docs/PROJECT-OVERVIEW.md` | Renamed from docs/Features/Core/core-gameplay.md |
| `docs/Archive/BLUEPRINT-2026-04-archived.md` | Archived BLUEPRINT.md |
| `docs/Archive/DEVELOPER-GUIDE-2026-04-archived.md` | Archived DEVELOPER-GUIDE.md |

**Modified (existing files):**

| Path | Change |
|---|---|
| `AGENTS.md` (root) | Shrunk to ~150 lines; adds "Platform notes" + "Documentation & rules architecture" sections |
| `CLAUDE.md` (root) | Shrunk to ~25 lines; @AGENTS.md import + Claude-specific layer only |
| `.gemini/GEMINI.md` | Shrunk to ~10 lines; uses `@../AGENTS.md` import; removes hardcoded path |
| `.gitignore` | Adds `CLAUDE.local.md` |
| `Tools/Validation/validate_content.py` | Adds Phase 21 hook calling validate_docs_structure.py |
| `docs/INDEX.md` | Rewritten as ~150-line master catalog |
| `docs/Features/Camp/` → `docs/Features/Camp/` | Folder rename |
| `docs/Features/UI/` → `docs/Features/Interface/` | Folder rename |
| `docs/Features/Core/*.md` | Split into per-subsystem folders |
| `docs/Features/Technical/*.md` | Split into owning subsystems, folder deleted |
| `Enlisted.csproj` | Line 14 ref to BLUEPRINT.md updated to new doc |
| `src/Features/Escalation/EscalationState.cs:10` | Comment ref to core-gameplay.md updated |
| `ModuleData/Enlisted/Events/events_retinue.json:13` | Doc URL updated |
| Files under renamed paths | Update any internal cross-refs |

**Deleted (folders):**

| Path | After |
|---|---|
| `docs/Features/Core/` | Replaced by per-subsystem folders; index.md rewritten as redirect stub then deleted |
| `docs/Features/Technical/` | Files redistributed |
| `docs/BLUEPRINT.md` | Moved to `docs/Archive/` |
| `docs/DEVELOPER-GUIDE.md` | Moved to `docs/Archive/` |
| `docs/BUILD-CONFIGURATIONS.md` | Folded into `Tools/AGENTS.md` |
| `docs/Features/Core/core-gameplay.md` | Promoted to `docs/PROJECT-OVERVIEW.md` |

---

# Phase A — Add nested AGENTS.md + CLAUDE.md shims (additive)

**Goal:** Create all 8 nested `AGENTS.md` files + 8 sibling `CLAUDE.md` shims + 2 stub docs folders. Root files unchanged. Both old and new context coexist after this phase — sessions get more context, not less.

**Files:** 18 new files (see File Structure above for full list).

---

### Task A1: Create `src/Features/Content/AGENTS.md`

**Files:**
- Create: `src/Features/Content/AGENTS.md`

- [ ] **Step 1: Create the file with this exact header**

```markdown
# AGENTS.md — Features/Content

> Parent: [../../../AGENTS.md](../../../AGENTS.md)
> Handbook: [../../../docs/Features/Content/](../../../docs/Features/Content/)

This file owns rules for the Content subsystem: storylet backbone, event delivery, scripted-effects catalog, and the ModalEventBuilder pipeline. Source-of-truth for any file under `src/Features/Content/`.

---

## Storylet backbone

```

- [ ] **Step 2: Copy storylet backbone rule from root AGENTS.md**

Open `AGENTS.md` (root). Extract the body of Critical Rule #11 — line 143 (heading "### 11. Content authoring — route through the storylet backbone") through line 149 (end of "Enum offsets MUST stay disjoint" paragraph). Paste it under the "## Storylet backbone" heading in the new file. Remove the "### 11." prefix (it's now at file scope, no longer a numbered rule).

- [ ] **Step 3: Add the Save-definer offset convention sub-section**

The "Save-definer offset convention." paragraph (currently line 147 of root) lives between content rules and save rules — it's referenced both here AND in the SaveSystem AGENTS.md. Keep a SHORT pointer here (3 lines max):

```markdown
## Save-definer offsets (cross-ref)

Storylet/content classes claim offsets in [src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs](../../Mod.Core/SaveSystem/EnlistedSaveDefiner.cs). See [../Mod.Core/SaveSystem/AGENTS.md](../Mod.Core/SaveSystem/AGENTS.md) for the full offset table and the class-vs-enum disjointness rule. Spec 0 owns class offsets 40-44 and enum offsets 82-83.

```

- [ ] **Step 4: Add Event Delivery rule**

Copy from root AGENTS.md Critical Rule #10 (line 117 heading "### 10. Event Delivery — route through StoryDirector" through line 141 end of "Spec: ..." line). Paste under heading "## StoryDirector routing". Remove the "### 10." prefix.

- [ ] **Step 5: Add Content pitfalls**

Append these pitfalls from root AGENTS.md (current "Common Pitfalls" section, lines 266-376). Reformat as one heading per pitfall:

- Pitfall #12 (line 284-289): `EventDeliveryManager.Instance.QueueEvent` direct call bypasses StoryDirector → heading: "## Pitfall: Direct QueueEvent bypasses pacing"
- Pitfall #14 (line 294-297): Inventing scripted-effect ids → heading: "## Pitfall: Unknown scripted-effect id"
- Pitfall #16 (line 308-312): Cyclic scripted-effect references → heading: "## Pitfall: Scripted-effect cycles"
- Pitfall #17 (line 313-319): `Campaign.Current.CampaignBehaviorManager` empty at OnGameStart → heading: "## Pitfall: CampaignBehaviorManager empty at OnGameStart"
- Pitfall #18 (line 320-325): Content catalogs initialize AFTER OnGameStart → heading: "## Pitfall: Catalogs init after OnGameStart"
- Pitfall #21 (line 341-346): `OrdersNewsFeedThrottle` rejects at SpeedUpMultiplier — keep here (Content owns story feed) and ALSO mention in Activities/AGENTS.md → heading: "## Pitfall: News-feed throttle silent at >4x speed"
- Pitfall #22 (line 347-354): Plans drift from codebase → heading: "## Pitfall: Plan-vs-codebase API drift"

Copy each pitfall body VERBATIM. Don't rewrite. Don't paraphrase.

- [ ] **Step 6: Add "See also" footer**

Append:

```markdown
---

## See also

- [docs/Features/Content/storylet-backbone.md](../../../docs/Features/Content/storylet-backbone.md) — living reference for the Spec 0 backbone
- [docs/superpowers/specs/2026-04-18-event-pacing-design.md](../../../docs/superpowers/specs/2026-04-18-event-pacing-design.md) — event pacing spec
- [../Mod.Core/SaveSystem/AGENTS.md](../Mod.Core/SaveSystem/AGENTS.md) — full offset table
- [../../ModuleData/Enlisted/AGENTS.md](../../ModuleData/Enlisted/AGENTS.md) — JSON authoring rules for storylet/event content
```

- [ ] **Step 7: Verify the file is ~80-130 lines and renders**

Run: `wc -l src/Features/Content/AGENTS.md`
Expected: between 80 and 130 lines.

Run: `head -10 src/Features/Content/AGENTS.md`
Expected: shows the heading + cross-link template.

---

### Task A2: Create `src/Features/Content/CLAUDE.md` (shim)

**Files:**
- Create: `src/Features/Content/CLAUDE.md`

- [ ] **Step 1: Write the 3-line shim**

```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

- [ ] **Step 2: Verify**

Run: `cat src/Features/Content/CLAUDE.md`
Expected: 3 lines as above.

---

### Task A3: Create `src/Mod.Core/SaveSystem/AGENTS.md`

**Files:**
- Create: `src/Mod.Core/SaveSystem/AGENTS.md`

- [ ] **Step 1: Create with header**

```markdown
# AGENTS.md — Mod.Core/SaveSystem

> Parent: [../../../AGENTS.md](../../../AGENTS.md)
> Handbook: [../../../docs/architecture/](../../../docs/architecture/) (cross-cutting briefs)

This file owns save-system rules: the full save-definer offset table, class-vs-enum disjointness, EnsureInitialized pattern, and the `Campaign.Current.X`-null-at-OnGameStart family. Source-of-truth for `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` and any `[Serializable]` class registered there.

---

## Critical Rule: Save System Registration

```

- [ ] **Step 2: Copy Critical Rule #8 from root AGENTS.md**

Extract root AGENTS.md lines 91-100 (heading "### 8. Save System Registration" through "Persist in-progress flags in `SyncData()` too — otherwise state is lost on reload."). Paste under "## Critical Rule: Save System Registration". Remove the "### 8." prefix.

- [ ] **Step 3: Add full offset table**

Extract root AGENTS.md line 147 ("**Save-definer offset convention.**" paragraph). Paste under new heading "## Save-definer offset convention". Keep the entire paragraph verbatim — this is the authoritative table that Pitfall #15 (root) refers to.

- [ ] **Step 4: Add enum-vs-class disjointness rule**

Extract root AGENTS.md line 149 ("**Enum offsets MUST stay disjoint from the class-offset numeric range.**" paragraph). Paste under heading "## Enum offsets must be disjoint from class offsets".

- [ ] **Step 5: Add EnsureInitialized + HashSet pitfalls**

These pitfalls are CURRENTLY in CLAUDE.md (not in root AGENTS.md — they were in the "Project conventions" section that's getting absorbed). From CLAUDE.md, extract the bullets currently at line 108 (`HashSet<T> is not a saveable container`) and line 110 (`[Serializable] save stores deserialize with null Dictionary/List properties`). Paste each as its own heading under "## Pitfall: …" in this new file.

If those CLAUDE.md lines have already been removed in a parallel session, copy from git history: `git show a1c443c:CLAUDE.md | sed -n '105,115p'`.

- [ ] **Step 6: Add Campaign.Current null-at-OnGameStart pitfall**

From CLAUDE.md current line 109 (`Campaign.Current.X-backed statics are null at OnGameStart`) — paste under heading "## Pitfall: Campaign.Current.X statics null at OnGameStart".

- [ ] **Step 7: Add Pitfall #15 (SaveableTypeDefiner offset claims)**

Extract root AGENTS.md lines 298-307 (Pitfall #15 body — "Claiming a `SaveableTypeDefiner` offset without grepping..."). Paste under heading "## Pitfall: Claiming an offset without checking the registry".

- [ ] **Step 8: Add "See also" footer**

```markdown
---

## See also

- [EnlistedSaveDefiner.cs](EnlistedSaveDefiner.cs) — the live offset registry
- [../../Features/Content/AGENTS.md](../../Features/Content/AGENTS.md) — Content classes that claim offsets 40-44
- [../../../docs/superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md](../../../docs/superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md) — menu+duty offsets 51-52
- [../../../docs/architecture/ck3-wanderer-architecture-brief.md](../../../docs/architecture/ck3-wanderer-architecture-brief.md) — CK3 wanderer offsets 54-58
```

- [ ] **Step 9: Verify file is ~80-130 lines**

Run: `wc -l src/Mod.Core/SaveSystem/AGENTS.md`
Expected: between 80 and 160 lines.

---

### Task A4: Create `src/Mod.Core/SaveSystem/CLAUDE.md` (shim)

- [ ] **Step 1: Write the shim**

```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

- [ ] **Step 2: Verify**

Run: `cat src/Mod.Core/SaveSystem/CLAUDE.md`
Expected: 3 lines.

---

### Task A5: Create `ModuleData/Enlisted/AGENTS.md`

**Files:**
- Create: `ModuleData/Enlisted/AGENTS.md`

- [ ] **Step 1: Create with header**

```markdown
# AGENTS.md — ModuleData/Enlisted

> Parent: [../../AGENTS.md](../../AGENTS.md)
> Handbook: [../../docs/Features/Content/](../../docs/Features/Content/) (content authoring lives in Content handbook)

This file owns content-authoring rules: JSON field order, tooltip rules, inline localization, and `Enlisted.csproj` AfterBuild parity. Source-of-truth for any file under `ModuleData/Enlisted/`.

---
```

- [ ] **Step 2: Copy Critical Rule #6 (JSON Field Order)**

Extract root AGENTS.md lines 81-85 (heading "### 6. JSON Field Order — fallback immediately after ID" plus the JSON snippet). Paste under heading "## JSON Field Order — fallback immediately after ID". Remove the "### 6." prefix.

- [ ] **Step 3: Copy Critical Rule #7 (Tooltips)**

Extract root AGENTS.md lines 87-89. Paste under "## Event Tooltips Required".

- [ ] **Step 4: Add inline `{=key}Fallback` localization convention**

Write the following section:

```markdown
## Localization — inline `{=key}Fallback`

Storylet/event JSON authors loc-keys inline as `{=key_id}Fallback Text` in `title` / `setup` / `options[].text` / `options[].tooltip` fields, NOT as the legacy Event schema's separate `titleId`+`title` pairs. The game falls back to inline text when a key is missing from `ModuleData/Languages/enlisted_strings.xml`, so missing keys only affect translators (zero runtime impact).

After authoring, run `python3 Tools/Validation/sync_event_strings.py` and integrate the generated XML.
```

- [ ] **Step 5: Add AfterBuild parity rule**

Write:

```markdown
## `Enlisted.csproj` AfterBuild parity — authoring discipline

Adding a new content directory under `ModuleData/Enlisted/` requires three additions to `Enlisted.csproj`:

1. An `<XxxData Include="ModuleData\Enlisted\Xxx\*.json"/>` ItemGroup
2. A matching `<MakeDir Directories="$(OutputPath)..\..\ModuleData\Enlisted\Xxx"/>` inside `AfterBuild`
3. A `<Copy SourceFiles="@(XxxData)" DestinationFolder="...\Xxx\"/>` step

Missing any of the three = content silently not deployed to the game install. Runtime loaders log `Expected("XXX", "no_xxx_dir", "directory not found: ...")` at info level, so the failure is easy to miss.

Pattern at `Enlisted.csproj:614-671` (ItemGroups) and `:728-745` (AfterBuild).

**Tooling note:** `validate_content.py` does NOT currently enforce csproj↔ModuleData parity — `content_dirs_to_check` at `Tools/Validation/validate_content.py:1475` is empty. Adding enforcement is a follow-up TODO. Until then, this is authoring discipline.
```

- [ ] **Step 6: Copy `Enlisted.csproj` wildcards-non-recursive quirk**

From CLAUDE.md current (line 92, in "File handling" section), extract the "`Enlisted.csproj` wildcards are NON-RECURSIVE." paragraph. Paste under heading "## `Enlisted.csproj` wildcards are NON-RECURSIVE".

- [ ] **Step 7: Add "See also" footer**

```markdown
---

## See also

- [../../src/Features/Content/AGENTS.md](../../src/Features/Content/AGENTS.md) — runtime rules for storylets/events
- [../../docs/Features/Content/storylet-backbone.md](../../docs/Features/Content/storylet-backbone.md) — living reference
- [../../docs/Features/Content/writing-style-guide.md](../../docs/Features/Content/writing-style-guide.md) — voice and tone
- [Effects/scripted_effects.json](Effects/scripted_effects.json) — seed catalog for scripted-effect ids
```

- [ ] **Step 8: Verify**

Run: `wc -l ModuleData/Enlisted/AGENTS.md`
Expected: between 50 and 90 lines.

---

### Task A6: Create `ModuleData/Enlisted/CLAUDE.md` (shim)

- [ ] **Step 1: Write the shim**

```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

- [ ] **Step 2: Verify**

Run: `cat ModuleData/Enlisted/CLAUDE.md`
Expected: 3 lines.

---

### Task A7: Create `Tools/Validation/AGENTS.md`

**Files:**
- Create: `Tools/Validation/AGENTS.md`

- [ ] **Step 1: Create with header**

```markdown
# AGENTS.md — Tools/Validation

> Parent: [../../AGENTS.md](../../AGENTS.md)
> Handbook: [../../docs/INDEX.md](../../docs/INDEX.md) (Tools/ docs catalogued in INDEX)

This file owns rules for the content validator, error-codes registry, and lint stack. Source-of-truth for `Tools/Validation/*.py` and `Tools/Validation/*.ps1`.

---
```

- [ ] **Step 2: Add ModLogger discipline section**

Extract root AGENTS.md lines 156-161 (the "Error reporting uses three severity-specific `ModLogger` methods:" bullets and following paragraph). Paste under heading "## ModLogger discipline — string-literal scanner rules". This is the canonical rule both Code Standards (root) and this file refer to.

- [ ] **Step 3: Add Pitfall #20 (interpolated-string rejection)**

Extract root AGENTS.md lines 334-340 (Pitfall #20: "`ModLogger.Surfaced` / `Caught` / `Expected` require ... string literals at the call site"). Paste under heading "## Pitfall: Interpolated strings rejected by error-codes scanner".

- [ ] **Step 4: Add validator phases catalog**

Write the following section. (The validator currently has ~20 phases per current code; Phase 21 will be added in Phase B of this plan, so reference it as "added by docs-restructure Phase B"):

```markdown
## Validator phases (`validate_content.py`)

The validator runs as a series of numbered phases. Each phase has a `--check-only` mode for CI and a regenerate mode for some (e.g. Phase 10 invokes `generate_error_codes.py`).

- **Phase 10** — Error-codes registry sync. Calls `generate_error_codes.py --check`. **Regenerate after any line-shifting C# edit in a file with `ModLogger.Surfaced` calls** — codes track `(category, file, line)`.
- **Phase 11** — Blocks `ModLogger.Error(...)` calls in `src/` (the public API was retired 2026-04-19; use `Surfaced` / `Caught` / `Expected` instead).
- **Phase 12** — Blocks read-only quality writes (`rank`, `days_in_rank`, `days_enlisted`) from storylet effects. Also blocks unknown `apply` values pointing at non-existent scripted-effect ids.
- **Phase 14-17** — Storylet/event schema and reference validation rails.
- **Phase 18** — Companion dialog catalog completeness (added with Plan 2).
- **Phase 20** — Ceremony storylet completeness, fail-closed (added with Plan 3).
- **Phase 21** — Docs structure mirror validator (added by docs-restructure Phase B; see [validate_docs_structure.py](validate_docs_structure.py)).

Full phase list: see header of `validate_content.py`. When adding a phase, update this catalog and `docs/INDEX.md`.
```

- [ ] **Step 5: Add error-codes regen workflow**

```markdown
## Error-codes registry regen workflow

Regenerate after any line-shifting C# edit in a file that contains `ModLogger.Surfaced(...)` calls — codes track `(category, file, line)`, so adding or removing lines above a Surfaced call (even adding an unrelated event declaration or extracting a method) invalidates the registry and fails Phase 10.

```bash
python3 Tools/Validation/generate_error_codes.py
```

Stage `docs/error-codes.md` in the same commit as the C# changes. Don't hand-edit the registry.
```

- [ ] **Step 6: Add lint stack invocation**

```markdown
## Lint stack — single source of truth per language

| Language | Config | Invoke |
|---|---|---|
| C# / JSON / XML | `.editorconfig` + Roslyn | (driven by `dotnet build`) |
| Python | `ruff.toml` | `ruff check Tools/` |
| PowerShell | `PSScriptAnalyzerSettings.psd1` | `Invoke-ScriptAnalyzer -Path Tools/ -Recurse` |
| Everything end-to-end | n/a | `Tools/Validation/lint_repo.ps1` |
```

- [ ] **Step 7: Add "See also" footer**

```markdown
---

## See also

- [../README.md](../README.md) — Tools/ catalog
- [validate_content.py](validate_content.py) — validator entry point
- [generate_error_codes.py](generate_error_codes.py) — registry generator
- [../../docs/error-codes.md](../../docs/error-codes.md) — current registry
- [../../docs/error-codes-archive.md](../../docs/error-codes-archive.md) — historical codes
- [../AGENTS.md](../AGENTS.md) — Tools/ build configurations
```

- [ ] **Step 8: Verify**

Run: `wc -l Tools/Validation/AGENTS.md`
Expected: between 50 and 100 lines.

---

### Task A8: Create `Tools/Validation/CLAUDE.md` (shim)

- [ ] **Step 1: Write the shim**

```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

- [ ] **Step 2: Verify**

Run: `cat Tools/Validation/CLAUDE.md`

---

### Task A9: Create `src/Features/Conversations/AGENTS.md`

**Files:**
- Create: `src/Features/Conversations/AGENTS.md`

- [ ] **Step 1: Create with header**

```markdown
# AGENTS.md — Features/Conversations

> Parent: [../../../AGENTS.md](../../../AGENTS.md)
> Handbook: [../../../docs/Features/Conversations/](../../../docs/Features/Conversations/) (folder stubbed by Task A18; populate as design content emerges)

This file owns rules for conversation/dialog wiring: the token-interpolation discipline, the six required tokens, and the wiring vs authored-content gap that bites if you do one without the other. Source-of-truth for any file under `src/Features/Conversations/` and for any dialog/conversation-firing flow elsewhere.

---
```

- [ ] **Step 2: Copy Pitfall #23 (token interpolation)**

Extract root AGENTS.md lines 355-368 (Pitfall #23 body). Paste under heading "## Token interpolation discipline".

- [ ] **Step 3: Add "See also" footer**

```markdown
---

## See also

- [../../../docs/Features/Companions/companion-archetype-catalog.md](../../../docs/Features/Companions/companion-archetype-catalog.md) — companion dialog catalogs (Plan 2 reference)
- [../../../ModuleData/Enlisted/Dialogue/](../../../ModuleData/Enlisted/Dialogue/) — authored dialog content
```

- [ ] **Step 4: Verify**

Run: `wc -l src/Features/Conversations/AGENTS.md`
Expected: between 30 and 60 lines.

---

### Task A10: Create `src/Features/Conversations/CLAUDE.md` (shim)

- [ ] **Step 1: Write the shim**

```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

- [ ] **Step 2: Verify**

Run: `cat src/Features/Conversations/CLAUDE.md`

---

### Task A11: Create `src/Features/Activities/AGENTS.md`

**Files:**
- Create: `src/Features/Activities/AGENTS.md`

- [ ] **Step 1: Create with header**

```markdown
# AGENTS.md — Features/Activities

> Parent: [../../../AGENTS.md](../../../AGENTS.md)
> Handbook: [../../../docs/Features/Activities/](../../../docs/Features/Activities/) (folder stubbed by Task A18)

This file owns Activities runtime rules. Activities are stateful ticking C# objects with intent-biased phase pools (Banner-Kings Feast pattern). Subdirs covered:
- `Home/` — Home surface activities
- `Orders/` — Orders-surface activities (legacy `src/Features/Orders/` was retired 2026-04-21; this is the current Orders owner)

---
```

- [ ] **Step 2: Copy Pitfall #13 (read-only quality writes)**

Extract root AGENTS.md lines 290-293 (Pitfall #13). Paste under heading "## Pitfall: Writing to a read-only quality from a storylet effect". This applies to Orders storylets specifically.

- [ ] **Step 3: Copy Pitfall #19 (`int.MinValue` throttle sentinel)**

Extract root AGENTS.md lines 326-333 (Pitfall #19). Paste under heading "## Pitfall: `int.MinValue` throttle sentinel overflow".

- [ ] **Step 4: Add Orders-specific "News-feed throttle silent at >4x" pointer**

```markdown
## Orders news-feed throttle (cross-ref to Content)

`OrdersNewsFeedThrottle.TryClaim()` rejects when `Campaign.Current.TimeControlMode == SpeedUpMultiplier` with the multiplier above 4x. Intentional silence at extreme fast-forward. Full pitfall in [../Content/AGENTS.md](../Content/AGENTS.md#pitfall-news-feed-throttle-silent-at-4x-speed). Tick-driven `ModLogger` entries (DRIFT, DUTYPROFILE heartbeats, PATH heartbeats) still log at any speed.
```

- [ ] **Step 5: Add Orders retirement note**

```markdown
## Legacy Orders folder retired

The directory `src/Features/Orders/` was deleted on 2026-04-21 (commit `a8719bb`). Orders code now lives under `src/Features/Activities/Orders/` as an Activity subtype. Old plan archived at [docs/superpowers/plans/archive/2026-04-20-orders-surface.md](../../../docs/superpowers/plans/archive/2026-04-20-orders-surface.md).
```

- [ ] **Step 6: Add "See also" footer**

```markdown
---

## See also

- [../Content/AGENTS.md](../Content/AGENTS.md) — storylet backbone these activities consume
- [../../../docs/Features/Content/career-loop.md](../../../docs/Features/Content/career-loop.md) — career-loop integration spec
- [../../../docs/superpowers/specs/2026-04-21-plans-integration-design.md](../../../docs/superpowers/specs/2026-04-21-plans-integration-design.md) — five-plan integration roadmap
```

- [ ] **Step 7: Verify**

Run: `wc -l src/Features/Activities/AGENTS.md`
Expected: between 40 and 80 lines.

---

### Task A12: Create `src/Features/Activities/CLAUDE.md` (shim)

- [ ] **Step 1: Write the shim**

```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

- [ ] **Step 2: Verify**

Run: `cat src/Features/Activities/CLAUDE.md`

---

### Task A13: Create `docs/superpowers/AGENTS.md`

**Files:**
- Create: `docs/superpowers/AGENTS.md`

- [ ] **Step 1: Create with header**

```markdown
# AGENTS.md — docs/superpowers

> Parent: [../../AGENTS.md](../../AGENTS.md)
> Handbook: this is the handbook (it's the specs/plans folder)

This file owns conventions for the specs and plans system: the plan-vs-codebase drift pitfall, the "API corrections appendix" pattern, the verification doc convention, and the doc-comment rule (no forward-spec, no change history).

---
```

- [ ] **Step 2: Copy Pitfall #22 (plan-vs-codebase drift)**

Extract root AGENTS.md lines 347-354 (Pitfall #22 body). Paste under heading "## Plan-vs-codebase API drift". Add a closing line:

```markdown
Mitigation pattern: long plans append an "API corrections appendix" at the bottom as deviations are discovered during execution. See `specs/2026-04-20-orders-surface.md` (archived) for the pattern.
```

- [ ] **Step 3: Add verification doc convention**

Write:

```markdown
## Verification doc convention

Plans that ship get a paired verification doc at the same path with `-verification.md` suffix. Verification docs:
- Record what was actually shipped vs what the plan prescribed
- Document any API corrections discovered during execution
- Note "code-level verification complete; in-game smoke pending" status if a human operator hasn't smoke-tested yet
- Get archived alongside the plan when the feature ships and stabilizes

Examples: `plans/2026-04-24-ck3-wanderer-rank-ceremony-arc-verification.md`, `plans/2026-04-24-ck3-wanderer-companion-substrate-verification.md`.
```

- [ ] **Step 4: Add doc-comment rule**

Write:

```markdown
## Doc-comment rule — describe behavior, never forward-spec or change history

Spec/plan files occasionally prescribe XML doc comments like `/// Spec 2 PathScorer subscribes for crossroads firing; Spec 4 will subscribe for promotion ceremony.` Copying these verbatim into the code rots into stale fiction the next session — Spec 4 may rename PathScorer, ship a different design, or never ship at all.

Rewrite plan-prescribed doc comments as one behavioral sentence at the implementer-prompt stage. Don't ship the plan's literal doc text when it references future specs or explains why a file exists.
```

- [ ] **Step 5: Add archival convention**

Write:

```markdown
## Archival convention

When a plan's feature ships and stabilizes (verification doc green, smoke-tested), `git mv` the plan + verification doc into `plans/archive/YYYY-MM-DD-<name>.md`. The original path stays as the pointer in commit history. Active plans live at top-level `plans/`; archived plans live at `plans/archive/`.

For the docs-restructure ([docs-and-agents-md-restructure-design.md](specs/2026-05-24-docs-and-agents-md-restructure-design.md)), Phase G archives BLUEPRINT.md and DEVELOPER-GUIDE.md to `docs/Archive/` — same pattern, different folder (top-level docs/Archive/ vs plans/archive/).
```

- [ ] **Step 6: Add "See also" footer**

```markdown
---

## See also

- [specs/](specs/) — design specs
- [plans/](plans/) — active implementation plans
- [plans/archive/](plans/archive/) — shipped plans
- [../../AGENTS.md](../../AGENTS.md) — project root
```

- [ ] **Step 7: Verify**

Run: `wc -l docs/superpowers/AGENTS.md`
Expected: between 30 and 60 lines.

---

### Task A14: Create `docs/superpowers/CLAUDE.md` (shim)

- [ ] **Step 1: Write the shim**

```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

- [ ] **Step 2: Verify**

Run: `cat docs/superpowers/CLAUDE.md`

---

### Task A15: Create `Tools/AGENTS.md`

**Files:**
- Create: `Tools/AGENTS.md`

- [ ] **Step 1: Create with header**

```markdown
# AGENTS.md — Tools

> Parent: [../AGENTS.md](../AGENTS.md)
> Handbook: [../docs/INDEX.md](../docs/INDEX.md) (no dedicated mirror)

This file owns Tools/ rules: build configurations, deploy scripts, and the workshop upload flow. The Validation subdir has its own AGENTS.md at [Validation/AGENTS.md](Validation/AGENTS.md).

---
```

- [ ] **Step 2: Add a build configurations overview**

Phase G of this plan folds `docs/BUILD-CONFIGURATIONS.md` (currently 270 lines) into this file. For Phase A, leave a placeholder:

```markdown
## Build configurations

The full build-configuration depth currently lives in [../docs/BUILD-CONFIGURATIONS.md](../docs/BUILD-CONFIGURATIONS.md). Phase G of the 2026-05-24 docs restructure folds the worth-keeping bits into THIS file and archives BUILD-CONFIGURATIONS.md.

Until Phase G ships, refer to that file for the depth. The top-level build command lives in root [../AGENTS.md](../AGENTS.md) Quick Commands.
```

- [ ] **Step 3: Add deploy scripts overview**

```markdown
## Deploy scripts

- [Steam/upload.ps1](Steam/upload.ps1) — Steam Workshop upload. Requires `STEAM_USERNAME` / `STEAM_PASSWORD` env vars (or interactive login). Uses the Workshop app ID at Steam Workshop page.
- [Decompile-Bannerlord.bat](Decompile-Bannerlord.bat) — Windows: regenerate the TaleWorlds decompile reference. See root [../AGENTS.md](../AGENTS.md) Quick Commands for the WSL equivalent (`ilspycmd` flow).
- [Decompile-Bannerlord.ps1](Decompile-Bannerlord.ps1) — PowerShell variant; cross-platform with `pwsh` if available (note: Windows `.exe` interop is broken from WSL — see root AGENTS.md Platform notes).
```

- [ ] **Step 4: Add Workshop publish note**

```markdown
## Workshop publish flow

1. Build clean: `dotnet build -c "Enlisted RETAIL" /p:Platform=x64`
2. Validate: `python3 Tools/Validation/validate_content.py` passes
3. Upload: `./Tools/Steam/upload.ps1`

Workshop page: [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3621116083).
```

- [ ] **Step 5: Add "See also" footer**

```markdown
---

## See also

- [Validation/AGENTS.md](Validation/AGENTS.md) — validator + error-codes + lint stack
- [README.md](README.md) — Tools/ catalog
- [TECHNICAL-REFERENCE.md](TECHNICAL-REFERENCE.md) — logging, saves, dialogue, menu patterns reference
```

- [ ] **Step 6: Verify**

Run: `wc -l Tools/AGENTS.md`
Expected: between 30 and 80 lines.

---

### Task A16: Create `Tools/CLAUDE.md` (shim)

- [ ] **Step 1: Write the shim**

```markdown
# CLAUDE.md
Claude-specific layer. Authoritative rules are in the sibling AGENTS.md.
@AGENTS.md
```

- [ ] **Step 2: Verify**

Run: `cat Tools/CLAUDE.md`

---

### Task A17: Create stub `docs/Features/Conversations/index.md`

**Files:**
- Create: `docs/Features/Conversations/index.md`

- [ ] **Step 1: Create folder and stub**

```bash
mkdir -p docs/Features/Conversations
```

- [ ] **Step 2: Write the index.md**

```markdown
# Conversations — Design Reference

This folder will hold design intent and living references for the Conversations subsystem (dialog wiring, token interpolation, `MBTextManager.SetTextVariable` flow).

Rules and patterns live in [../../../src/Features/Conversations/AGENTS.md](../../../src/Features/Conversations/AGENTS.md).

Folder created 2026-05-24 as part of the docs-restructure mirror; populate with design docs as they emerge.
```

- [ ] **Step 3: Verify**

Run: `ls docs/Features/Conversations/`
Expected: shows `index.md`.

---

### Task A18: Create stub `docs/Features/Activities/index.md`

**Files:**
- Create: `docs/Features/Activities/index.md`

- [ ] **Step 1: Create folder and stub**

```bash
mkdir -p docs/Features/Activities
```

- [ ] **Step 2: Write the index.md**

```markdown
# Activities — Design Reference

This folder will hold design intent and living references for the Activities subsystem (Activity runtime, intent-biased phase pools, Home/Orders subtypes).

Rules and patterns live in [../../../src/Features/Activities/AGENTS.md](../../../src/Features/Activities/AGENTS.md).

Folder created 2026-05-24 as part of the docs-restructure mirror; populate with design docs as they emerge.
```

- [ ] **Step 3: Verify**

Run: `ls docs/Features/Activities/`
Expected: shows `index.md`.

---

### Task A19: Verify Phase A and commit

- [ ] **Step 1: Build clean**

Run: `dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -5`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 2: Validator still green**

Run: `python3 Tools/Validation/validate_content.py 2>&1 | tail -3`
Expected: `VALIDATION PASSED WITH WARNINGS` (no errors).

- [ ] **Step 3: Confirm 18 new files exist**

Run: `git status --short | grep '^??' | wc -l`
Expected: 18 (the new nested AGENTS.md + CLAUDE.md shims + 2 docs/Features stubs).

Run: `git status --short | grep '^??'`
Expected output:
```
?? ModuleData/Enlisted/AGENTS.md
?? ModuleData/Enlisted/CLAUDE.md
?? Tools/AGENTS.md
?? Tools/CLAUDE.md
?? Tools/Validation/AGENTS.md
?? Tools/Validation/CLAUDE.md
?? docs/Features/Activities/
?? docs/Features/Conversations/
?? docs/superpowers/AGENTS.md
?? docs/superpowers/CLAUDE.md
?? src/Features/Activities/AGENTS.md
?? src/Features/Activities/CLAUDE.md
?? src/Features/Content/AGENTS.md
?? src/Features/Content/CLAUDE.md
?? src/Features/Conversations/AGENTS.md
?? src/Features/Conversations/CLAUDE.md
?? src/Mod.Core/SaveSystem/AGENTS.md
?? src/Mod.Core/SaveSystem/CLAUDE.md
```

- [ ] **Step 4: Manual session-load smoke test**

Open a fresh Claude Code session in `src/Features/Content/` (e.g., `claude` from that directory). Ask "what rules apply when I add a new storylet?" Expected: response references storylet backbone rules from the sibling AGENTS.md (via the CLAUDE.md shim auto-discovery).

(Skip if no fresh-session capacity; the file structure being correct is the gate.)

- [ ] **Step 5: Commit**

```bash
git add \
  src/Features/Content/AGENTS.md src/Features/Content/CLAUDE.md \
  src/Mod.Core/SaveSystem/AGENTS.md src/Mod.Core/SaveSystem/CLAUDE.md \
  ModuleData/Enlisted/AGENTS.md ModuleData/Enlisted/CLAUDE.md \
  Tools/Validation/AGENTS.md Tools/Validation/CLAUDE.md \
  src/Features/Conversations/AGENTS.md src/Features/Conversations/CLAUDE.md \
  src/Features/Activities/AGENTS.md src/Features/Activities/CLAUDE.md \
  docs/superpowers/AGENTS.md docs/superpowers/CLAUDE.md \
  Tools/AGENTS.md Tools/CLAUDE.md \
  docs/Features/Conversations/ docs/Features/Activities/

git commit -m "$(cat <<'EOF'
docs: add nested AGENTS.md + CLAUDE.md shims (additive)

Phase A of docs-and-agents-md-restructure
(spec: docs/superpowers/specs/2026-05-24-docs-and-agents-md-restructure-design.md).

Creates 8 nested AGENTS.md at subsystem scope, each with a sibling 3-line
CLAUDE.md shim (@AGENTS.md) so Claude Code's nested-CLAUDE.md auto-discovery
wires through to the shared content Codex reads via path-cascade.

Subsystem coverage:
- src/Features/Content/        storylet backbone, StoryDirector, scripted-effects
- src/Mod.Core/SaveSystem/     full offset table, EnsureInitialized, HashSet pitfall
- ModuleData/Enlisted/         JSON authoring rules, tooltips, localization
- Tools/Validation/            validator phases, ModLogger discipline, error-codes
- src/Features/Conversations/  token interpolation discipline
- src/Features/Activities/     activities runtime + Orders sub-subsystem
- docs/superpowers/            plan/spec/verification conventions
- Tools/                       build configurations + deploy scripts

Also creates docs/Features/Conversations/ and docs/Features/Activities/
with stub index.md files so the Phase 21 mirror validator (added in Phase B)
will find a docs folder matching each new src/Features/<X>/AGENTS.md.

Root AGENTS.md and CLAUDE.md unchanged at this phase. Phase B-G shrink them.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: commit succeeds, working tree clean.

Run: `git log --oneline -1`
Expected: commit message subject visible.

---

# Phase B — Add validator (Phase 21 of validate_content.py)

**Goal:** Create the mirror validator and reference-repair helper. Wire validator into `validate_content.py` as Phase 21. Initial run must pass against Phase A's tree.

**Files:**
- Create: `Tools/Validation/validate_docs_structure.py`
- Create: `Tools/Validation/find_stale_refs.py`
- Modify: `Tools/Validation/validate_content.py` (add Phase 21 hook)

---

### Task B1: Create the mirror validator script

**Files:**
- Create: `Tools/Validation/validate_docs_structure.py`

- [ ] **Step 1: Write the script**

```python
#!/usr/bin/env python3
"""Phase 21 — Docs structure mirror validator.

Enforces the docs+AGENTS.md restructure design
(docs/superpowers/specs/2026-05-24-docs-and-agents-md-restructure-design.md).

Fail-closed checks (return 1):
  1. Every nested AGENTS.md (anywhere except root) has a sibling CLAUDE.md
     shim that contains @AGENTS.md.
  2. Every src/Features/<X>/AGENTS.md has a corresponding docs/Features/<X>/
     folder.
  3. Every @import path in CLAUDE.md, GEMINI.md, and any nested CLAUDE.md shim
     resolves to an existing file.
  4. Cross-language reference scan: *.cs, *.json, *.csproj, *.xml, *.md files
     anywhere in the repo MUST NOT reference moved/deleted doc paths from the
     STALE_PATHS list below. (List is updated each rename phase.)

Warning checks (do not fail, report count):
  5. Every nested AGENTS.md uses the cross-link template at the top
     (Parent + Handbook links).
  6. Every docs/Features/<X>/ folder has an index.md.
  7. docs/INDEX.md lists every nested AGENTS.md path.
  8. AGENTS.md files >300 lines (signal to split or shed).

Usage:
  python3 Tools/Validation/validate_docs_structure.py        # run all checks
  python3 Tools/Validation/validate_docs_structure.py --json # JSON output for CI
"""
from __future__ import annotations
import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

# Stale paths from prior rename phases. Phase F/G updates this list as renames land.
# Format: each entry is a substring; if found in any tracked file outside Archive/,
# the file's reference is stale and must be repaired.
STALE_PATHS: list[str] = [
    # Populated by Phase F sub-commits. Empty after Phase A; grows during rename
    # phases. Phase G clears entries as their underlying paths are fully repaired.
]

NESTED_AGENTS_PATHS = [
    "src/Features/Content/AGENTS.md",
    "src/Mod.Core/SaveSystem/AGENTS.md",
    "ModuleData/Enlisted/AGENTS.md",
    "Tools/Validation/AGENTS.md",
    "src/Features/Conversations/AGENTS.md",
    "src/Features/Activities/AGENTS.md",
    "docs/superpowers/AGENTS.md",
    "Tools/AGENTS.md",
]


@dataclass
class Result:
    failures: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def fail(self, msg: str) -> None:
        self.failures.append(msg)

    def warn(self, msg: str) -> None:
        self.warnings.append(msg)


def check_shim_siblings(result: Result) -> None:
    """Check 1: every nested AGENTS.md has a sibling CLAUDE.md with @AGENTS.md."""
    for rel in NESTED_AGENTS_PATHS:
        agents = REPO_ROOT / rel
        if not agents.exists():
            result.fail(f"Missing nested AGENTS.md: {rel}")
            continue
        shim = agents.parent / "CLAUDE.md"
        if not shim.exists():
            result.fail(f"Missing CLAUDE.md shim sibling: {shim.relative_to(REPO_ROOT)}")
            continue
        content = shim.read_text(encoding="utf-8")
        if "@AGENTS.md" not in content:
            result.fail(
                f"CLAUDE.md shim missing @AGENTS.md import: {shim.relative_to(REPO_ROOT)}"
            )


def check_features_mirror(result: Result) -> None:
    """Check 2: every src/Features/<X>/AGENTS.md has docs/Features/<X>/ folder."""
    features_src = REPO_ROOT / "src" / "Features"
    if not features_src.exists():
        return
    for agents_file in features_src.glob("*/AGENTS.md"):
        subsystem = agents_file.parent.name
        docs_folder = REPO_ROOT / "docs" / "Features" / subsystem
        if not docs_folder.exists():
            result.fail(
                f"Mirror missing: {agents_file.relative_to(REPO_ROOT)} has no "
                f"matching docs/Features/{subsystem}/ folder"
            )


_IMPORT_RE = re.compile(r"^\s*@([^\s]+)\s*$", re.MULTILINE)


def check_imports_resolve(result: Result) -> None:
    """Check 3: @import paths in CLAUDE.md / GEMINI.md / nested shims resolve."""
    candidates = [
        REPO_ROOT / "CLAUDE.md",
        REPO_ROOT / ".gemini" / "GEMINI.md",
    ]
    candidates.extend(
        (REPO_ROOT / p).parent / "CLAUDE.md" for p in NESTED_AGENTS_PATHS
    )
    for f in candidates:
        if not f.exists():
            continue
        text = f.read_text(encoding="utf-8")
        for m in _IMPORT_RE.finditer(text):
            import_path = m.group(1)
            target = (f.parent / import_path).resolve()
            if not target.exists():
                result.fail(
                    f"Broken @import in {f.relative_to(REPO_ROOT)}: "
                    f"@{import_path} -> {target} does not exist"
                )


def check_stale_refs(result: Result) -> None:
    """Check 4: no live references to moved/deleted doc paths."""
    if not STALE_PATHS:
        return
    extensions = {".cs", ".json", ".csproj", ".xml", ".md"}
    skip_dirs = {".git", "bin", "obj", "Decompile", ".worktrees", "Archive"}
    for path in REPO_ROOT.rglob("*"):
        if not path.is_file() or path.suffix not in extensions:
            continue
        if any(part in skip_dirs for part in path.parts):
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for stale in STALE_PATHS:
            if stale in text:
                rel = path.relative_to(REPO_ROOT)
                result.fail(f"Stale reference in {rel}: {stale!r}")


_PARENT_LINK_RE = re.compile(r"Parent:\s*\[.*?AGENTS\.md\]")
_HANDBOOK_LINK_RE = re.compile(r"Handbook:\s*\[")


def check_cross_link_templates(result: Result) -> None:
    """Check 5: every nested AGENTS.md has Parent + Handbook lines near top."""
    for rel in NESTED_AGENTS_PATHS:
        f = REPO_ROOT / rel
        if not f.exists():
            continue
        head = "\n".join(f.read_text(encoding="utf-8").splitlines()[:10])
        if not _PARENT_LINK_RE.search(head):
            result.warn(f"{rel}: missing 'Parent: [...AGENTS.md]' link near top")
        if not _HANDBOOK_LINK_RE.search(head):
            result.warn(f"{rel}: missing 'Handbook: [...]' link near top")


def check_docs_features_indexes(result: Result) -> None:
    """Check 6: every docs/Features/<X>/ folder has index.md."""
    features_docs = REPO_ROOT / "docs" / "Features"
    if not features_docs.exists():
        return
    for sub in features_docs.iterdir():
        if not sub.is_dir():
            continue
        if not (sub / "index.md").exists():
            result.warn(f"{sub.relative_to(REPO_ROOT)}: missing index.md")


def check_index_catalogues_nested(result: Result) -> None:
    """Check 7: docs/INDEX.md lists every nested AGENTS.md path."""
    index = REPO_ROOT / "docs" / "INDEX.md"
    if not index.exists():
        result.warn("docs/INDEX.md missing")
        return
    text = index.read_text(encoding="utf-8")
    for rel in NESTED_AGENTS_PATHS:
        if rel not in text:
            result.warn(f"docs/INDEX.md does not catalog: {rel}")


def check_agents_size_budget(result: Result) -> None:
    """Check 8: warn if any AGENTS.md >300 lines."""
    root_paths = [REPO_ROOT / "AGENTS.md"]
    root_paths.extend(REPO_ROOT / p for p in NESTED_AGENTS_PATHS)
    for f in root_paths:
        if not f.exists():
            continue
        n = sum(1 for _ in f.open(encoding="utf-8"))
        if n > 300:
            result.warn(f"{f.relative_to(REPO_ROOT)}: {n} lines (>300, consider splitting)")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true", help="Emit JSON output")
    args = parser.parse_args()

    result = Result()
    check_shim_siblings(result)
    check_features_mirror(result)
    check_imports_resolve(result)
    check_stale_refs(result)
    check_cross_link_templates(result)
    check_docs_features_indexes(result)
    check_index_catalogues_nested(result)
    check_agents_size_budget(result)

    if args.json:
        print(json.dumps({"failures": result.failures, "warnings": result.warnings}, indent=2))
    else:
        print(f"docs-structure validator (Phase 21)")
        print(f"  failures: {len(result.failures)}")
        print(f"  warnings: {len(result.warnings)}")
        for f in result.failures:
            print(f"  FAIL: {f}")
        for w in result.warnings:
            print(f"  WARN: {w}")

    return 1 if result.failures else 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Make it executable and test**

```bash
chmod +x Tools/Validation/validate_docs_structure.py
python3 Tools/Validation/validate_docs_structure.py
```

Expected:
- 0 failures
- Warnings present (cross-link template warnings for any nested AGENTS.md whose Phase A author used slightly different wording; docs/INDEX.md catalog warnings since INDEX hasn't been rewritten yet; size budget OK at this phase since root AGENTS.md is ~410 lines — wait, 410 > 300, so root AGENTS.md WILL produce a warning. That's expected and signals Phase E should shrink it.)

Specifically, after Phase A you should see warnings like:
- `AGENTS.md: 410 lines (>300, consider splitting)` — expected, addressed in Phase E
- `docs/INDEX.md does not catalog: src/Features/Content/AGENTS.md` (etc, for all 8) — expected, addressed in Phase G

0 failures is the gate. Warnings are fine.

---

### Task B2: Create the stale-refs helper script

**Files:**
- Create: `Tools/Validation/find_stale_refs.py`

- [ ] **Step 1: Write the script**

```python
#!/usr/bin/env python3
"""Find (and optionally repair) stale references to moved/deleted doc paths.

Used by Phase F (rename phases) of the docs-restructure to ensure live
references in .cs / .json / .csproj / .xml / .md files get updated in the
same commit as the rename.

Usage:
  python3 Tools/Validation/find_stale_refs.py <old-path> [<new-path>]
  python3 Tools/Validation/find_stale_refs.py <old-path> <new-path> --apply

Without --apply, prints each match (file:line: matched text).
With --apply AND a <new-path>, rewrites <old-path> -> <new-path> in place.

Searches: *.cs, *.json, *.csproj, *.xml, *.md
Skips: .git, bin, obj, Decompile, .worktrees, Archive
"""
from __future__ import annotations
import argparse
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
EXTENSIONS = {".cs", ".json", ".csproj", ".xml", ".md"}
SKIP_DIRS = {".git", "bin", "obj", "Decompile", ".worktrees", "Archive"}


def scan(old_path: str) -> list[tuple[Path, int, str]]:
    hits: list[tuple[Path, int, str]] = []
    for path in REPO_ROOT.rglob("*"):
        if not path.is_file() or path.suffix not in EXTENSIONS:
            continue
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        try:
            for i, line in enumerate(path.open(encoding="utf-8", errors="ignore"), 1):
                if old_path in line:
                    hits.append((path, i, line.rstrip()))
        except OSError:
            continue
    return hits


def apply_rewrite(old_path: str, new_path: str, hits: list[tuple[Path, int, str]]) -> int:
    """Rewrite old_path -> new_path in every file containing a hit."""
    touched: set[Path] = set()
    for path, _, _ in hits:
        if path in touched:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except OSError:
            print(f"  SKIP (unreadable): {path.relative_to(REPO_ROOT)}", file=sys.stderr)
            continue
        new_text = text.replace(old_path, new_path)
        if new_text != text:
            path.write_text(new_text, encoding="utf-8")
            touched.add(path)
    return len(touched)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("old_path", help="Path substring to search for")
    parser.add_argument("new_path", nargs="?", help="Replacement (required with --apply)")
    parser.add_argument(
        "--apply", action="store_true", help="Rewrite in place (requires new_path)"
    )
    args = parser.parse_args()

    if args.apply and not args.new_path:
        parser.error("--apply requires <new_path>")

    hits = scan(args.old_path)
    if not hits:
        print(f"No references to {args.old_path!r} found.")
        return 0

    print(f"Found {len(hits)} reference(s) to {args.old_path!r}:")
    for path, line_no, line in hits:
        print(f"  {path.relative_to(REPO_ROOT)}:{line_no}: {line}")

    if args.apply:
        rewritten = apply_rewrite(args.old_path, args.new_path, hits)
        print(f"\nRewrote {args.old_path!r} -> {args.new_path!r} in {rewritten} file(s).")

    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Make it executable and dry-run test**

```bash
chmod +x Tools/Validation/find_stale_refs.py
python3 Tools/Validation/find_stale_refs.py "docs/BLUEPRINT.md"
```

Expected: prints hits in `Enlisted.csproj`, `AGENTS.md`, and `CLAUDE.md` (currently — the references that will need repair in Phase G).

Do NOT run `--apply` yet. Phase G uses it.

---

### Task B3: Wire Phase 21 into validate_content.py

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Locate the phase-orchestration code in validate_content.py**

Run: `grep -n 'Phase 20\|VALIDATION PASSED\|VALIDATION FAILED' Tools/Validation/validate_content.py | head -10`

Identify where Phase 20 is invoked. The Phase 21 invocation goes after it but before the final pass/fail summary.

- [ ] **Step 2: Add the Phase 21 hook**

The exact insertion line depends on the current validate_content.py structure. Pattern: add a new block (search for similar phase invocations and mirror the pattern). The block must:

1. Run `python3 Tools/Validation/validate_docs_structure.py --json`
2. Parse output's `failures` array
3. Add each failure to the global ERROR set with category `docs-structure`
4. Add each warning to the global WARNING set with category `docs-structure`

Exact code to add (insert after Phase 20 invocation, before the summary):

```python
    # Phase 21: docs structure mirror validator
    import subprocess as _sp  # local import to avoid changing imports at top
    try:
        _r = _sp.run(
            ["python3", str(Path(__file__).parent / "validate_docs_structure.py"), "--json"],
            capture_output=True, text=True, check=False
        )
        if _r.returncode != 0 or _r.stdout.strip():
            import json as _json
            _payload = _json.loads(_r.stdout) if _r.stdout.strip() else {"failures": [], "warnings": []}
            for _f in _payload.get("failures", []):
                add_error("docs-structure", _f, "Phase 21: docs+AGENTS.md structure mirror")
            for _w in _payload.get("warnings", []):
                add_info("docs-structure", _w, "Phase 21: docs+AGENTS.md structure mirror")
    except Exception as _e:
        add_error("docs-structure", f"Phase 21 validator failed to run: {_e}", "Phase 21 invocation error")
```

(NOTE: `add_error` and `add_info` are placeholder names. Replace with the actual logging helpers `validate_content.py` uses — grep for `add_error\|errors\.append\|log_error` to find the canonical helper. The pattern from earlier phases will tell you the right name.)

- [ ] **Step 3: Run validate_content.py and confirm Phase 21 runs**

```bash
python3 Tools/Validation/validate_content.py 2>&1 | grep -i 'phase 21\|docs-structure' | head
```

Expected: at least one line mentioning Phase 21 or docs-structure (info messages from the warning categories).

- [ ] **Step 4: Confirm validator still passes overall**

```bash
python3 Tools/Validation/validate_content.py 2>&1 | tail -5
```

Expected: `VALIDATION PASSED WITH WARNINGS` (Phase 21 contributes a few `info_docs-structure` warnings since INDEX.md isn't rewritten and root AGENTS.md is still >300 lines).

---

### Task B4: Verify Phase B and commit

- [ ] **Step 1: Final validator + build pass**

```bash
python3 Tools/Validation/validate_docs_structure.py
```

Expected: 0 failures.

```bash
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
```

Expected: `VALIDATION PASSED WITH WARNINGS`.

```bash
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 2: Confirm files staged**

```bash
git status --short
```

Expected:
```
M  Tools/Validation/validate_content.py
?? Tools/Validation/find_stale_refs.py
?? Tools/Validation/validate_docs_structure.py
```

- [ ] **Step 3: Commit**

```bash
git add Tools/Validation/validate_docs_structure.py \
        Tools/Validation/find_stale_refs.py \
        Tools/Validation/validate_content.py

git commit -m "$(cat <<'EOF'
tools: add validate_docs_structure.py (Phase 21) + find_stale_refs.py

Phase B of docs-and-agents-md-restructure
(spec: docs/superpowers/specs/2026-05-24-docs-and-agents-md-restructure-design.md).

validate_docs_structure.py enforces the layered mirror structure:
- shim-sibling check (each nested AGENTS.md has a CLAUDE.md @AGENTS.md sibling)
- src/Features/<X>/ ↔ docs/Features/<X>/ folder mirror
- @import path resolution
- cross-language stale-reference scan
- cross-link template + size-budget warnings

find_stale_refs.py is the helper Phase F rename phases use to repair
live references in *.cs/*.json/*.csproj/*.xml/*.md when moving doc paths.

Hooked into validate_content.py as Phase 21. Initial run passes
(warnings expected — root AGENTS.md >300 lines [addressed Phase E];
INDEX.md missing nested catalog [addressed Phase G]).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Expected: commit succeeds.

---

# Phase C — Lift project status to docs/superpowers/STATUS.md

**Goal:** Move the per-plan project-status section out of root CLAUDE.md into a dedicated file at `docs/superpowers/STATUS.md`. Codex doesn't see CLAUDE.md, so today the status is invisible to it; the new location is in the cascade.

**Files:**
- Create: `docs/superpowers/STATUS.md`
- Modify: `CLAUDE.md` (replace status section with a pointer)
- Modify: `AGENTS.md` (add a 1-line pointer near top)

---

### Task C1: Extract status into STATUS.md

**Files:**
- Create: `docs/superpowers/STATUS.md`

- [ ] **Step 1: Identify the status block in CLAUDE.md**

Run: `grep -n 'Current project status' CLAUDE.md`

Expected: heading line found (currently around line 15 with date 2026-05-24).

- [ ] **Step 2: Determine exact line range**

The status block is the entire section between "## Current project status (2026-05-24)" heading and the next `---` separator or top-level `##` heading. Use:

```bash
awk '/^## Current project status/{flag=1} flag && /^---$/{print NR; exit} flag' CLAUDE.md
```

Note the result (the line number of the `---` after the status). The status block runs from the "## Current project status" line through the line BEFORE that `---`.

- [ ] **Step 3: Create STATUS.md with the extracted block**

```bash
# Adjust START and END based on the line numbers from Step 2.
# Example: if status starts at line 15 and `---` is at line 30,
# extract lines 15-29 into STATUS.md.
START=$(grep -n '^## Current project status' CLAUDE.md | head -1 | cut -d: -f1)
END=$(($(awk -v s=$START 'NR>s && /^---$/{print NR; exit}' CLAUDE.md) - 1))
sed -n "${START},${END}p" CLAUDE.md > /tmp/status-block.md
```

Then write `docs/superpowers/STATUS.md` with this header + the extracted block:

```markdown
# Project Status

> Lifted from CLAUDE.md on 2026-05-24 (docs-restructure Phase C). This file is loaded by both Codex (via root AGENTS.md cascade) and Claude Code (via root CLAUDE.md pointer + sibling CLAUDE.md in this folder), so any tool sees current project state.

```

Then append the extracted block (skip the `## Current project status` heading line; the new file's H1 already serves):

```bash
cat <<'HEADER' > docs/superpowers/STATUS.md
# Project Status

> Lifted from CLAUDE.md on 2026-05-24 (docs-restructure Phase C). This file is loaded by both Codex (via root AGENTS.md cascade) and Claude Code (via root CLAUDE.md pointer + sibling CLAUDE.md in this folder), so any tool sees current project state.

HEADER
# Skip the heading line from the extracted block (we have H1 above):
tail -n +2 /tmp/status-block.md >> docs/superpowers/STATUS.md
rm /tmp/status-block.md
```

- [ ] **Step 4: Verify STATUS.md renders**

Run: `wc -l docs/superpowers/STATUS.md`
Expected: roughly the length of the status section + 3 (for new header).

Run: `head -10 docs/superpowers/STATUS.md`
Expected: H1 + lifted-from note + first project-status bullet.

---

### Task C2: Replace status block in CLAUDE.md with a pointer

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Read the current CLAUDE.md**

Use the Read tool: `Read CLAUDE.md`.

- [ ] **Step 2: Replace the status block with a single line**

Use the Edit tool to replace the entire status section (from "## Current project status (2026-05-24)" heading through the `---` separator AFTER it) with this:

```markdown
## Current project status

Lives at [docs/superpowers/STATUS.md](docs/superpowers/STATUS.md). Updates on a different cadence than rules and is loaded by both Codex (via AGENTS.md cascade) and Claude Code.

---
```

- [ ] **Step 3: Verify CLAUDE.md shrunk appropriately**

Run: `wc -l CLAUDE.md`
Expected: roughly current_line_count minus (status_block_size - 5) — the status block was ~30 lines, replaced with ~4 lines, so ~26 lines smaller.

---

### Task C3: Add status pointer in root AGENTS.md

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Read root AGENTS.md**

Use the Read tool: `Read AGENTS.md` (limit to first 50 lines).

- [ ] **Step 2: Add a pointer line right after the file's opening description paragraph**

Insertion point: after the line "This file is the shared source of truth for AI coding agents..." (currently line 5), before the `---` separator.

Use Edit:

```diff
 This file is the shared source of truth for AI coding agents (Claude Code, Codex, Cursor, Copilot, Aider, etc.). Tool-specific extras live alongside: `CLAUDE.md` imports this file.

+**Active work:** see [docs/superpowers/STATUS.md](docs/superpowers/STATUS.md) for current plan/spec progress.
+
 ---
```

- [ ] **Step 3: Verify AGENTS.md is still ~410 lines (no removal yet, just a 2-line addition)**

Run: `wc -l AGENTS.md`
Expected: ~412 lines.

---

### Task C4: Verify Phase C and commit

- [ ] **Step 1: Validator + build pass**

```bash
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
```

Expected: both pass.

- [ ] **Step 2: Confirm STATUS.md links resolve manually**

Run: `python3 -c "import pathlib; print(pathlib.Path('docs/superpowers/STATUS.md').exists())"`
Expected: `True`.

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/STATUS.md CLAUDE.md AGENTS.md
git commit -m "$(cat <<'EOF'
docs: lift project status to docs/superpowers/STATUS.md

Phase C of docs-and-agents-md-restructure.

Status changes on a different cadence than rules. Lives next to specs/plans
where it semantically belongs. Both tools see it:
- Codex via root AGENTS.md cascade (AGENTS.md now has an "Active work" pointer)
- Claude Code via root CLAUDE.md (replaced status block with a pointer)

CLAUDE.md shrunk by ~26 lines; AGENTS.md +2 lines. No content lost.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

# Phase D — Shrink CLAUDE.md and GEMINI.md to compatibility shims

**Goal:** Reduce root `CLAUDE.md` to ~25 lines (`@AGENTS.md` import + Claude-specific layer only); reduce `.gemini/GEMINI.md` to ~10 lines with actual `@../AGENTS.md` import (currently just a markdown link, doesn't load); add `CLAUDE.local.md` to `.gitignore`.

**Files:**
- Modify: `CLAUDE.md`
- Modify: `.gemini/GEMINI.md`
- Modify: `.gitignore`

---

### Task D1: Shrink root CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Read current CLAUDE.md**

Use the Read tool: `Read CLAUDE.md`.

After Phase C, CLAUDE.md has: header, `@AGENTS.md`, project-status pointer (added Phase C), and the "Session-Specific Guidance" / "Recommended Skills" / "Context7" / "MCP Server Usage" sections. Most of "Session-Specific Guidance" (Shell & PATH / Build & commit / File handling) belongs in root AGENTS.md (Phase E moves it there). For Phase D, prepare CLAUDE.md to be the final shim shape AFTER Phase E.

Phase D removes from CLAUDE.md:
- The Shell & PATH section
- The Build & commit section
- The File handling section
- The "Project conventions" deep-dive section (`HashSet not supported`, `Campaign.Current.X` statics, `Enlisted.csproj` wildcards, etc.) — these moved to nested AGENTS.md in Phase A

Phase D keeps in CLAUDE.md:
- `@AGENTS.md` import line
- Project-status pointer (added Phase C)
- Recommended Skills table
- Context7 MCP IDs note
- MCP Server Usage note
- One paragraph noting Auto/plan mode hints if any

- [ ] **Step 2: Rewrite CLAUDE.md as the final shim**

Use the Write tool to overwrite CLAUDE.md with this exact content (the only Claude-specific surface that survives):

```markdown
# CLAUDE.md

Claude Code project memory for the Enlisted Bannerlord mod. Authoritative rules for all AI tools live in AGENTS.md and its nested cascade (`src/Features/Content/AGENTS.md`, etc.). This file holds Claude-specific extras only.

@AGENTS.md

---

## Current project status

Lives at [docs/superpowers/STATUS.md](docs/superpowers/STATUS.md). Loaded by both Codex (via AGENTS.md cascade) and Claude Code.

---

## Recommended Skills

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

## MCP Server Usage

- **Context7** — Third-party library docs only. IDs: Harmony `/pardeike/harmony`, Newtonsoft.Json `/jamesnk/newtonsoft.json`, C# `/websites/learn_microsoft_en-us_dotnet_csharp`. NOT for TaleWorlds APIs (use the local Decompile per AGENTS.md Critical Rule #1).
- **Microsoft Learn** — .NET Framework 4.7.2 and C# language questions Context7 doesn't cover.
- **Playwright** — UI testing if/when a browser-facing tool is added; not applicable to the mod itself.
- **Cloudflare / Gmail / Google Drive / Google Calendar / any other ambient MCP servers** — Not relevant; ignore.
```

- [ ] **Step 3: Verify**

Run: `wc -l CLAUDE.md`
Expected: ~30-40 lines.

Run: `grep -E '^@|@AGENTS' CLAUDE.md`
Expected: `@AGENTS.md` line present.

---

### Task D2: Shrink `.gemini/GEMINI.md` and add real `@import`

**Files:**
- Modify: `.gemini/GEMINI.md`

- [ ] **Step 1: Read current GEMINI.md**

Use the Read tool: `Read .gemini/GEMINI.md`.

Currently 31 lines with hardcoded `/home/onebodyamerica/Projects/Enlisted` path (line 9) and generic C# game advice that duplicates AGENTS.md.

- [ ] **Step 2: Overwrite with the shim**

Use Write to replace GEMINI.md with this exact content:

```markdown
# Identity

Senior C# Bannerlord modder for the Enlisted project.

@../AGENTS.md

## Reasoning

Use Deep Think for AI-behavior, campaign-map, or complex Harmony patches. Explain "why" briefly for architectural choices (especially performance or stability decisions).

## Authoritative source

All project rules, commands, gotchas, and standards live in [AGENTS.md](../AGENTS.md) and its nested cascade. Do NOT re-derive guidance — defer to the cascade.
```

- [ ] **Step 3: Verify Gemini actually loads AGENTS.md (manual)**

If Gemini CLI is available locally, open a Gemini session in the repo and ask "what's Critical Rule #1?" Expected: response references the Decompile rule (which lives in AGENTS.md, loaded via `@../AGENTS.md`).

If Gemini's `@` syntax does NOT load (verify against current docs: https://github.com/google-gemini/gemini-cli/blob/main/docs/cli/gemini-md.md), fall back to configuring `.gemini/settings.json`:

```bash
# Inspect current settings
cat .gemini/settings.json
```

Add a context-file entry. The exact JSON shape depends on Gemini CLI version; per the agents.md spec note "Gemini CLI requires .gemini/settings.json configuration." If the schema changed since this plan was authored, check Gemini docs first; otherwise:

```json
{
  "...existing settings...": "...",
  "contextFiles": ["../AGENTS.md"]
}
```

- [ ] **Step 4: Verify**

Run: `wc -l .gemini/GEMINI.md`
Expected: ~10-12 lines.

Run: `grep -E '@\.\./|@AGENTS' .gemini/GEMINI.md`
Expected: `@../AGENTS.md` line present.

Run: `grep -i 'onebodyamerica' .gemini/GEMINI.md`
Expected: no output (hardcoded path removed).

---

### Task D3: Add `CLAUDE.local.md` to `.gitignore`

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Read .gitignore**

Use the Read tool: `Read .gitignore`.

- [ ] **Step 2: Add CLAUDE.local.md entry**

Find the section that already has `AGENTS.override.md` (currently in the "Codex CLI" comment block) and add `CLAUDE.local.md` next to it. Use Edit:

```diff
 # Codex CLI — keep .codex/config.toml tracked, ignore personal overrides and local state
 AGENTS.override.md
+CLAUDE.local.md
 .codex/cache/
 .codex/logs/
```

(Or if the comment doesn't fit semantically, add `CLAUDE.local.md` as a separate entry under a new "# Claude Code — per-user overrides" comment near `AGENTS.override.md`.)

- [ ] **Step 3: Verify**

Run: `grep -E '^CLAUDE\.local\.md|^AGENTS\.override\.md' .gitignore`
Expected: both entries shown.

Run: `git check-ignore CLAUDE.local.md`
Expected: outputs `CLAUDE.local.md` (confirmed ignored).

---

### Task D4: Verify Phase D and commit

- [ ] **Step 1: Validator + build pass**

```bash
python3 Tools/Validation/validate_docs_structure.py
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
```

Expected: 0 failures from Phase 21, validate_content passes with warnings, build clean.

- [ ] **Step 2: Confirm @import paths resolve (Phase 21 check #3)**

Phase 21's import-resolution check should pass. Specifically:
- `CLAUDE.md`'s `@AGENTS.md` resolves to `./AGENTS.md` ✓
- `.gemini/GEMINI.md`'s `@../AGENTS.md` resolves to `./AGENTS.md` ✓
- All 8 nested CLAUDE.md `@AGENTS.md` resolve to sibling AGENTS.md ✓

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md .gemini/GEMINI.md .gitignore
git commit -m "$(cat <<'EOF'
docs: shrink CLAUDE.md and GEMINI.md to compatibility shims

Phase D of docs-and-agents-md-restructure.

Root CLAUDE.md: 132 → ~35 lines. Removes Session-Specific Guidance
(moves to root AGENTS.md in Phase E as Platform notes), Project
conventions (already moved to nested AGENTS.md in Phase A). Keeps:
@AGENTS.md import, project-status pointer, Recommended Skills table,
MCP Server Usage notes.

.gemini/GEMINI.md: 31 → ~10 lines. Replaces markdown-only AGENTS link
with actual @../AGENTS.md import (Gemini CLI loads it via cascade).
Removes hardcoded /home/onebodyamerica/Projects/Enlisted user path bug.
Drops generic C# game advice that AGENTS.md covers project-specifically.

.gitignore: adds CLAUDE.local.md (parallels AGENTS.override.md Codex pattern).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

# Phase E — Shrink root AGENTS.md (content now in nested cascade)

**Goal:** Remove duplicated content from root `AGENTS.md` (rules and pitfalls that now live in nested files). Promote "Platform notes" from old CLAUDE.md to root AGENTS.md. Root file → ~150-200 lines.

**Files:**
- Modify: `AGENTS.md`

---

### Task E1: Identify what to keep vs cut in root AGENTS.md

- [ ] **Step 1: Read current root AGENTS.md**

Use the Read tool: `Read AGENTS.md`.

- [ ] **Step 2: Map each existing section to its fate**

Reference the spec §1 root file scope. Keep in root:
- Title + description paragraph + "Active work" pointer (added Phase C)
- Quick Commands (line ~9 through ~36)
- Critical Rules 1-5, 9 (Decompile, csproj registration, Gold, Equipment, Hero safety, Windows/WSL portability) — generic enough to be project-wide
- Code Standards (line ~153, but trim ModLogger paragraph — the depth moved to Tools/Validation/AGENTS.md)
- AI Maintainability Priorities (line ~181)
- Project Structure (line ~213)
- Pre-Commit Checklist (line ~237)
- Key Documentation table (line ~248, rewritten to point at nested AGENTS.md)
- Deprecated Systems
- Diagnostic Logs (with WSL paths)
- External Resources
- Final tagline

Remove from root (moved to nested):
- Critical Rules 6, 7 (JSON order, tooltips) → ModuleData/Enlisted/AGENTS.md (Task A5)
- Critical Rule 8 (Save System Registration depth) → keep ONE-PARAGRAPH summary in root; full table in src/Mod.Core/SaveSystem/AGENTS.md
- Critical Rule 10 (Event Delivery / StoryDirector depth) → keep ONE-PARAGRAPH summary; full pattern in src/Features/Content/AGENTS.md
- Critical Rule 11 (Storylet backbone + offset convention + enum disjointness) → src/Features/Content/AGENTS.md + src/Mod.Core/SaveSystem/AGENTS.md (Task A1 + A3)
- ModLogger depth (under Code Standards) → keep 3-line summary; full discipline in Tools/Validation/AGENTS.md
- Common Pitfalls section (line ~266 through ~376): keep ONLY the project-wide ones; move subsystem-specific ones to the nested files where they were copied in Phase A. Specifically:
  - Pitfall 1-5 (Gold, Enum, IsAlive, PlayerEncounter, csproj) → KEEP in root (they're general, repeated for memorability)
  - Pitfall 6-10 (tooltips, JSON, SyncData, SaveableType, external docs) → KEEP in root (cross-cutting basics)
  - Pitfall 11 (Occupation.Wanderer) → KEEP in root (cross-cutting, not Content-only)
  - Pitfall 12 (QueueEvent direct call) → REMOVE; moved to Content
  - Pitfall 13 (read-only quality writes) → REMOVE; moved to Activities
  - Pitfall 14 (unknown scripted-effect) → REMOVE; moved to Content
  - Pitfall 15 (offset claims) → REMOVE; moved to SaveSystem
  - Pitfall 16 (effect cycles) → REMOVE; moved to Content
  - Pitfall 17 (CampaignBehaviorManager) → REMOVE; moved to Content
  - Pitfall 18 (catalogs init) → REMOVE; moved to Content
  - Pitfall 19 (int.MinValue) → REMOVE; moved to Activities
  - Pitfall 20 (interpolated strings) → REMOVE; moved to Tools/Validation
  - Pitfall 21 (news feed throttle) → REMOVE; moved to Content + Activities
  - Pitfall 22 (plan drift) → REMOVE; moved to docs/superpowers
  - Pitfall 23 (token interpolation) → REMOVE; moved to Conversations

Add to root (new):
- "Platform notes (Windows / WSL)" section, copied from the CLAUDE.md Session-Specific Guidance content that was removed in Phase D
- "Documentation & rules architecture" section (~20 lines) per spec §5 — explains the cascade map

---

### Task E2: Add "Platform notes" section to root AGENTS.md

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Find insertion point**

After "## Code Standards" section, before "## AI Maintainability Priorities". Insert a new `## Platform notes` section.

- [ ] **Step 2: Write the section**

Use Edit to insert before "## AI Maintainability Priorities":

```markdown
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
```

- [ ] **Step 3: Verify**

Run: `grep -n '## Platform notes' AGENTS.md`
Expected: heading line shown.

---

### Task E3: Add "Documentation & rules architecture" section

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Insert after Platform notes, before AI Maintainability Priorities**

Use Edit to insert:

```markdown
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
| Validator / error-codes / lint stack | [Tools/Validation/AGENTS.md](Tools/Validation/AGENTS.md) |
| Conversations / dialog tokens | [src/Features/Conversations/AGENTS.md](src/Features/Conversations/AGENTS.md) |
| Activities / Orders / Home | [src/Features/Activities/AGENTS.md](src/Features/Activities/AGENTS.md) |
| Specs / plans / verification conventions | [docs/superpowers/AGENTS.md](docs/superpowers/AGENTS.md) |
| Build / deploy / Workshop | [Tools/AGENTS.md](Tools/AGENTS.md) |

Both Codex (cascades root → CWD) and Claude Code (auto-discovers sibling CLAUDE.md shims that import each AGENTS.md) load these on demand when you work in the matching subtree.

---
```

- [ ] **Step 2: Verify**

Run: `grep -n '## Documentation & rules architecture' AGENTS.md`
Expected: heading shown.

---

### Task E4: Remove duplicated content from root AGENTS.md

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Read current root AGENTS.md**

- [ ] **Step 2: Use Edit (with replace) to remove each subsystem-specific section**

Per the map in Task E1, remove these blocks (each as a separate Edit):

a) Critical Rule #6 (JSON Field Order — lines ~81-85): replace the whole block with:
   ```
   ### 6. JSON Field Order — fallback immediately after ID
   See [ModuleData/Enlisted/AGENTS.md](ModuleData/Enlisted/AGENTS.md).
   ```

b) Critical Rule #7 (Tooltips): same compression to 2 lines, pointing at ModuleData/Enlisted/AGENTS.md.

c) Critical Rule #8 (Save System Registration): keep just the 4-line minimal example (DefineEnumType / DefineClassType / SyncData reminder); remove the "Persist in-progress flags" paragraph extension (now in SaveSystem AGENTS.md).

d) Critical Rule #10 (Event Delivery): keep just the 3-line summary "Modal events go through StoryDirector.EmitCandidate, not EventDeliveryManager directly. Full pattern + StoryCandidate construction example in [src/Features/Content/AGENTS.md](src/Features/Content/AGENTS.md)." Remove the 20-line code example.

e) Critical Rule #11 (storylet backbone, offset convention, enum disjointness — lines 143-149): replace with:
   ```
   ### 11. Content authoring — route through the storylet backbone
   Storylets in `ModuleData/Enlisted/Storylets/`, state in `QualityStore`/`FlagStore`, durable engagements as `Activity` subclasses. Full rules + save-definer offset table + enum disjointness in [src/Features/Content/AGENTS.md](src/Features/Content/AGENTS.md) and [src/Mod.Core/SaveSystem/AGENTS.md](src/Mod.Core/SaveSystem/AGENTS.md).
   ```

f) Code Standards ModLogger paragraph (currently lines 156-161): collapse to:
   ```
   - Error reporting uses `ModLogger.Surfaced/Caught/Expected` — full discipline + string-literal scanner rules in [Tools/Validation/AGENTS.md](Tools/Validation/AGENTS.md).
   ```

g) Common Pitfalls 12-23 (lines 284-374): delete entirely. Replace the entire "## Common Pitfalls" section (lines 260-377) with this compressed version:

```markdown
---

## Common Pitfalls (project-wide)

Subsystem-specific pitfalls live in the matching nested AGENTS.md (see Documentation & rules architecture above). Cross-cutting pitfalls:

1. `ChangeHeroGold` instead of `GiveGoldAction` (Rule #3)
2. `Enum.GetValues` for equipment iteration (Rule #4)
3. Tracking a hero without checking `IsAlive` (Rule #5)
4. `PlayerEncounter.Finish()` while inside a settlement
5. Forgetting to add new files to `.csproj` (Rule #2)
6. Missing tooltips on event options (Rule #7 → ModuleData/Enlisted/AGENTS.md)
7. Wrong JSON field order — ID and fallback not adjacent (Rule #6 → ModuleData/Enlisted/AGENTS.md)
8. Not persisting in-progress flags in `SyncData()` (Rule #8 → src/Mod.Core/SaveSystem/AGENTS.md)
9. Missing `SaveableTypeDefiner` registration (Rule #8 → src/Mod.Core/SaveSystem/AGENTS.md)
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
```

- [ ] **Step 3: Update Key Documentation table**

Replace the existing Key Documentation table (line ~248) with one that ALSO catalogs nested AGENTS.md (preserves Phase 21 check #7):

```markdown
## Key Documentation

Link, don't duplicate — open these for depth:

| Topic | File |
| :--- | :--- |
| Master documentation catalog | [docs/INDEX.md](docs/INDEX.md) |
| Current project status | [docs/superpowers/STATUS.md](docs/superpowers/STATUS.md) |
| Architecture & rules architecture | [docs/architecture/](docs/architecture/) + "Documentation & rules architecture" above |
| Writing style (voice, tone) | [docs/Features/Content/writing-style-guide.md](docs/Features/Content/writing-style-guide.md) |
| Error code registry (auto-generated) | [docs/error-codes.md](docs/error-codes.md) |
| Storylet backbone (content layer, Spec 0) | [docs/Features/Content/storylet-backbone.md](docs/Features/Content/storylet-backbone.md) |
| Event pacing (delivery layer) | [docs/superpowers/specs/2026-04-18-event-pacing-design.md](docs/superpowers/specs/2026-04-18-event-pacing-design.md) |
| Build & deploy scripts | [Tools/AGENTS.md](Tools/AGENTS.md) |
| Validation / error-codes / lint | [Tools/Validation/AGENTS.md](Tools/Validation/AGENTS.md) |
| Logging, saves, dialogue, menu patterns | [Tools/TECHNICAL-REFERENCE.md](Tools/TECHNICAL-REFERENCE.md) |
| Validation tool reference | [Tools/README.md](Tools/README.md) |
```

(BLUEPRINT.md and DEVELOPER-GUIDE.md removed — they're archived in Phase G.)

- [ ] **Step 4: Verify size and structure**

Run: `wc -l AGENTS.md`
Expected: 150-220 lines (target was ~150; acceptance ceiling is ≤ 200; let's allow a bit of headroom).

Run: `grep -E '^## ' AGENTS.md`
Expected: sections present: Quick Commands, Critical Rules, Code Standards, Platform notes, Documentation & rules architecture, AI Maintainability Priorities, Project Structure, Pre-Commit Checklist, Key Documentation, Common Pitfalls, Deprecated Systems, Diagnostic Logs, External Resources.

---

### Task E5: Verify Phase E and commit

- [ ] **Step 1: Validator + build pass**

```bash
python3 Tools/Validation/validate_docs_structure.py
```

Expected: still 0 failures. The size-budget warning for root AGENTS.md (>300 lines) should now be GONE since root is ~150-200 lines.

```bash
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
```

Expected: both pass.

- [ ] **Step 2: Manual session-load smoke test**

Open a fresh Codex session in `src/Features/Content/`. Confirm:
- Root AGENTS.md content is loaded (cascade walks root → CWD)
- src/Features/Content/AGENTS.md content is loaded (path-cascade)
- Total context is leaner than before (root is ~150 lines vs 410, plus only the one nested file's ~80 lines)

Compare context size with `validate_content.py` runtime if helpful.

- [ ] **Step 3: Commit**

```bash
git add AGENTS.md
git commit -m "$(cat <<'EOF'
docs: shrink root AGENTS.md (content now in nested cascade)

Phase E of docs-and-agents-md-restructure.

Root AGENTS.md: 410 → ~180 lines. Subsystem-specific rules moved to
their nested AGENTS.md in Phase A:
- Rule #6/#7 (JSON field order, tooltips) → ModuleData/Enlisted/AGENTS.md
- Rule #10 depth (StoryDirector example) → src/Features/Content/AGENTS.md
- Rule #11 (storylet backbone + offset table) → src/Features/Content + Mod.Core/SaveSystem
- ModLogger discipline depth → Tools/Validation/AGENTS.md
- Pitfalls #12-23 (subsystem-specific) → their owning subsystem AGENTS.md

Added to root:
- Platform notes (Windows + WSL) — promoted from CLAUDE.md so Codex
  also sees the environment quirks
- Documentation & rules architecture — the cascade map + three-layer
  memory model decision rule (~20 lines)

Common Pitfalls section compressed to the 11 project-wide pitfalls
with pointers to subsystem AGENTS.md for the rest.

Key Documentation table updated to point at nested AGENTS.md;
BLUEPRINT.md and DEVELOPER-GUIDE.md entries removed (Phase G archives).

Phase 21 validator size-budget warning for root AGENTS.md (>300 lines)
should be cleared.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

# Phase F — Align docs/Features/ tree (mirror) + repair live references

**Goal:** Rename `docs/Features/Camp/` → `Camp/`; `UI/` → `Interface/`; split `Core/` per-subsystem; split `Technical/` and delete folder; promote `core-gameplay.md` to `docs/PROJECT-OVERVIEW.md`. Each rename group is ONE commit including the live-reference repair (find_stale_refs.py --apply runs in the same commit).

**Files (per sub-task):** see individual tasks.

---

### Task F1: Rename `docs/Features/Camp/` → `docs/Features/Camp/`

**Sub-phase F.1 — one commit.**

- [ ] **Step 1: Verify Campaign/ contents**

```bash
ls docs/Features/Camp/
```

Expected: 5 files (camp-life-simulation.md, camp-routine-schedule-spec.md, camp-simulation-system.md, temporary-leave.md, town-access-system.md).

- [ ] **Step 2: Audit for stale content references retired CampOpportunityGenerator / ContentOrchestrator**

```bash
grep -l 'CampOpportunityGenerator\|ContentOrchestrator\|DecisionManager\|DecisionCatalog' docs/Features/Camp/
```

Expected: at least `camp-simulation-system.md` and possibly `camp-life-simulation.md`.

For each flagged file, decide:
- **Archive** the file if entire content is stale (move to `docs/Archive/<filename>-2026-05-archived.md`)
- **Rewrite** the stale section in place if only part is stale (add a header note: "Updated 2026-05-24: references to retired DecisionManager / CampOpportunityGenerator cluster removed; current implementation is X.")
- **Mark deprecated** at top of file if the file describes a system that no longer exists (add a deprecation header pointing at the current alternative)

The implementer makes the call based on reading each file. Don't blindly carry stale content into Camp/.

- [ ] **Step 3: Use git mv to rename the folder**

```bash
git mv docs/Features/Camp docs/Features/Camp
```

- [ ] **Step 4: Scan for stale references to docs/Features/Camp**

```bash
python3 Tools/Validation/find_stale_refs.py "docs/Features/Camp"
```

Expected: any matches in `*.md`, `*.csproj`, `*.cs`, etc.

- [ ] **Step 5: Apply rewrites**

```bash
python3 Tools/Validation/find_stale_refs.py "docs/Features/Camp" "docs/Features/Camp" --apply
```

Expected: prints "Rewrote 'docs/Features/Camp' → 'docs/Features/Camp' in N file(s)."

- [ ] **Step 6: Update validate_docs_structure.py STALE_PATHS list**

After the apply rewrites everything, the STALE_PATHS list (line 27 of validate_docs_structure.py) does NOT need entries for this rename since the rename is complete. But if any references were intentionally left (e.g., archived plans that should keep historical paths), add `"docs/Features/Camp"` to STALE_PATHS so future commits don't reintroduce stale refs. For Phase F.1, this list stays empty IF the apply succeeded everywhere.

- [ ] **Step 7: Verify Phase 21 passes**

```bash
python3 Tools/Validation/validate_docs_structure.py
```

Expected: 0 failures.

- [ ] **Step 8: Build + validate passes**

```bash
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
```

Both pass.

- [ ] **Step 9: Commit**

```bash
git add docs/Features/Camp/ AGENTS.md CLAUDE.md docs/INDEX.md Enlisted.csproj 2>/dev/null
git add -u  # picks up any other modified files from --apply
git status --short  # verify only expected changes
git commit -m "$(cat <<'EOF'
docs: rename docs/Features/Camp → Camp (mirror src/Features/Camp)

Phase F.1 of docs-and-agents-md-restructure. Folder rename + live-ref
repair via find_stale_refs.py --apply. Stale Camp-cluster references
(CampOpportunityGenerator, ContentOrchestrator, DecisionManager —
retired 2026-04-25) audited; [retire/rewrite decision noted per file].

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task F2: Rename `docs/Features/UI/` → `docs/Features/Interface/`

**Sub-phase F.2 — one commit. Same shape as F.1.**

- [ ] **Step 1: Verify UI/ contents**

```bash
ls docs/Features/UI/
```

Expected: 5 files (camp-hub-custom-gauntlet.md, color-scheme.md, enlisted-combat-log.md, news-reporting-system.md, ui-systems-master.md).

- [ ] **Step 2: Rename via git mv**

```bash
git mv docs/Features/UI docs/Features/Interface
```

- [ ] **Step 3: Scan + apply**

```bash
python3 Tools/Validation/find_stale_refs.py "docs/Features/UI" "docs/Features/Interface" --apply
```

- [ ] **Step 4: Verify Phase 21 + build + validator**

```bash
python3 Tools/Validation/validate_docs_structure.py
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
```

All pass.

- [ ] **Step 5: Commit**

```bash
git add -A docs/Features/Interface/
git add -u
git commit -m "$(cat <<'EOF'
docs: rename docs/Features/UI → Interface (mirror src/Features/Interface)

Phase F.2 of docs-and-agents-md-restructure. Code wins on the naming
disagreement: matches existing src/Features/Interface/ folder (no
code-side rename required). Live references repaired via
find_stale_refs.py --apply.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task F3: Split `docs/Features/Core/` per-subsystem

**Sub-phase F.3 — one commit (this one is more involved; multiple file moves + index.md handling).**

- [ ] **Step 1: List current Core/ contents and target locations**

```bash
ls docs/Features/Core/
```

Expected: 9 files.

Target mapping (per spec §2):

| Source | Target |
|---|---|
| `enlistment.md` | `docs/Features/Enlistment/enlistment.md` |
| `pay-system.md` | `docs/Features/Enlistment/pay-system.md` |
| `muster-system.md` | `docs/Features/Enlistment/muster-system.md` |
| `retinue-system.md` | `docs/Features/Retinue/retinue-system.md` |
| `promotion-system.md` | `docs/Features/Ranks/promotion-system.md` |
| `company-events.md` | `docs/Features/Company/company-events.md` |
| `companion-management.md` | `docs/Features/Companions/companion-management.md` |
| `core-gameplay.md` | `docs/PROJECT-OVERVIEW.md` |
| `index.md` | (handled in Step 7 below — rewrite as redirect stub, then delete) |

- [ ] **Step 2: Create target folders**

```bash
mkdir -p docs/Features/Enlistment docs/Features/Retinue docs/Features/Ranks docs/Features/Company
# Companions already exists.
```

- [ ] **Step 3: git mv each file**

```bash
git mv docs/Features/Core/enlistment.md docs/Features/Enlistment/enlistment.md
git mv docs/Features/Core/pay-system.md docs/Features/Enlistment/pay-system.md
git mv docs/Features/Core/muster-system.md docs/Features/Enlistment/muster-system.md
git mv docs/Features/Core/retinue-system.md docs/Features/Retinue/retinue-system.md
git mv docs/Features/Core/promotion-system.md docs/Features/Ranks/promotion-system.md
git mv docs/Features/Core/company-events.md docs/Features/Company/company-events.md
git mv docs/Features/Core/companion-management.md docs/Features/Companions/companion-management.md
git mv docs/Features/Core/core-gameplay.md docs/PROJECT-OVERVIEW.md
```

- [ ] **Step 4: Audit moved files for stale references**

The files may internally reference each other or other Core/ paths. Run for each old path:

```bash
for old in docs/Features/Core/enlistment.md docs/Features/Core/pay-system.md docs/Features/Core/muster-system.md docs/Features/Core/retinue-system.md docs/Features/Core/promotion-system.md docs/Features/Core/company-events.md docs/Features/Core/companion-management.md docs/Features/Core/core-gameplay.md; do
  python3 Tools/Validation/find_stale_refs.py "$old"
done
```

- [ ] **Step 5: Apply rewrites for each**

For each old-path → new-path pair from the table in Step 1:

```bash
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/enlistment.md" "docs/Features/Enlistment/enlistment.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/pay-system.md" "docs/Features/Enlistment/pay-system.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/muster-system.md" "docs/Features/Enlistment/muster-system.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/retinue-system.md" "docs/Features/Retinue/retinue-system.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/promotion-system.md" "docs/Features/Ranks/promotion-system.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/company-events.md" "docs/Features/Company/company-events.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/companion-management.md" "docs/Features/Companions/companion-management.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/core-gameplay.md" "docs/PROJECT-OVERVIEW.md" --apply
```

- [ ] **Step 6: Sweep for any remaining `docs/Features/Core/` references**

```bash
python3 Tools/Validation/find_stale_refs.py "docs/Features/Core/"
```

Expected: only matches inside `docs/Features/Core/index.md` (which we delete next).

- [ ] **Step 7: Handle index.md**

Read `docs/Features/Core/index.md`. Two choices:
- If it's a useful catalog/redirect doc: rewrite as a single-line stub saying "This folder was split 2026-05-24. See: [Enlistment/](../Enlistment/), [Retinue/](../Retinue/), [Ranks/](../Ranks/), [Company/](../Company/), [Companions/](../Companions/), and [docs/PROJECT-OVERVIEW.md](../../PROJECT-OVERVIEW.md)." Then move it to `docs/Archive/Core-index-2026-05-archived.md` and delete the empty `docs/Features/Core/` folder.
- If it has no value: just delete it.

```bash
# Option A (preserve):
mkdir -p docs/Archive
git mv docs/Features/Core/index.md docs/Archive/Core-index-2026-05-archived.md
# (Edit the archived file to add deprecation header noting the split)
rmdir docs/Features/Core/

# Option B (delete):
git rm docs/Features/Core/index.md
rmdir docs/Features/Core/
```

- [ ] **Step 8: Verify Phase 21 + build + validator**

```bash
python3 Tools/Validation/validate_docs_structure.py
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
```

All pass. Phase 21 should now show `docs/Features/Enlistment/`, `docs/Features/Retinue/`, `docs/Features/Ranks/`, `docs/Features/Company/` folders without index.md (warning), since none exists yet for these split-into folders. That's fine — index.md is a warning, not failure.

If the implementer wants to clean those warnings now, create a 3-line `index.md` in each new folder listing the files it contains.

- [ ] **Step 9: Commit**

```bash
git add docs/Features/Enlistment/ docs/Features/Retinue/ docs/Features/Ranks/ docs/Features/Company/ docs/Features/Companions/ docs/PROJECT-OVERVIEW.md
git add -u
git commit -m "$(cat <<'EOF'
docs: split docs/Features/Core/ per-subsystem + repair live refs

Phase F.3 of docs-and-agents-md-restructure.

docs/Features/Core/ had no src/Features/Core/ mirror counterpart.
Files redistributed to their owning subsystems:
- enlistment/pay/muster → Enlistment/
- retinue → Retinue/
- promotion → Ranks/
- company-events → Company/
- companion-management → Companions/
- core-gameplay.md → docs/PROJECT-OVERVIEW.md (top-level player-facing overview)
- index.md → [archived OR deleted per file content]

Live references repaired via find_stale_refs.py --apply across
*.cs / *.json / *.csproj / *.xml / *.md.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task F4: Split `docs/Features/Technical/` and delete folder

**Sub-phase F.4 — one commit.**

- [ ] **Step 1: Review Technical/ contents**

```bash
ls docs/Features/Technical/
```

Expected: 3 files (commander-track-schema.md, conflict-detection-system.md, encounter-safety.md).

- [ ] **Step 2: Move per spec §2**

```bash
git mv docs/Features/Technical/commander-track-schema.md docs/Features/Activities/commander-track-schema.md
git mv docs/Features/Technical/conflict-detection-system.md docs/Features/Content/conflict-detection-system.md
mkdir -p docs/architecture  # already exists per audit but defensive
git mv docs/Features/Technical/encounter-safety.md docs/architecture/encounter-safety.md
rmdir docs/Features/Technical/
```

- [ ] **Step 3: Apply rewrites**

```bash
python3 Tools/Validation/find_stale_refs.py "docs/Features/Technical/commander-track-schema.md" "docs/Features/Activities/commander-track-schema.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Technical/conflict-detection-system.md" "docs/Features/Content/conflict-detection-system.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Technical/encounter-safety.md" "docs/architecture/encounter-safety.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/Features/Technical/"  # sweep for any holdouts
```

Last call should print "No references found."

- [ ] **Step 4: Verify Phase 21 + build + validator**

```bash
python3 Tools/Validation/validate_docs_structure.py
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
```

All pass.

- [ ] **Step 5: Commit**

```bash
git add docs/Features/Activities/ docs/Features/Content/ docs/architecture/
git add -u
git commit -m "$(cat <<'EOF'
docs: split docs/Features/Technical/ + delete folder

Phase F.4 of docs-and-agents-md-restructure.

- commander-track-schema.md → Activities/ (Orders consumes the schema)
- conflict-detection-system.md → Content/ (StoryDirector's conflict layer)
- encounter-safety.md → architecture/ (cross-cutting safety design)

Folder deleted. Live references repaired.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task F5: (Implicit) Verify the complete Phase F state

This is verification, not a new commit.

- [ ] **Step 1: Confirm docs/Features/ mirror is aligned**

```bash
diff <(ls src/Features/ | sort) <(ls docs/Features/ | sort)
```

Expected: src/Features/ has many subdirs without corresponding docs/Features/ (which is fine — no design docs yet). docs/Features/ should be a SUBSET of src/Features/ names (no docs/Features/X without src/Features/X). If any rogue docs/Features/<name> exists without a matching src/Features/<name>, that's a violation.

- [ ] **Step 2: Confirm Phase 21 fully passes**

```bash
python3 Tools/Validation/validate_docs_structure.py 2>&1 | head -10
```

Expected: 0 failures. Warnings may include "missing index.md" for newly created folders — fine.

If the implementer wants to add a quick index.md stub to each new docs/Features/<X>/ folder for cleanliness, that's a fine follow-up (could be a separate commit "docs: add index.md stubs to new mirror folders").

---

# Phase G — Archive BLUEPRINT + DEVELOPER-GUIDE, fold BUILD-CONFIGURATIONS, rewrite INDEX

**Goal:** Final cleanup. Grep-and-rescue any unique content from `BLUEPRINT.md` and `DEVELOPER-GUIDE.md` into the right nested AGENTS.md, then archive both. Fold `BUILD-CONFIGURATIONS.md` into `Tools/AGENTS.md`. Rewrite `INDEX.md` as the master catalog.

**Files:**
- Modify: `docs/INDEX.md` (full rewrite)
- Move: `docs/BLUEPRINT.md` → `docs/Archive/BLUEPRINT-2026-04-archived.md`
- Move: `docs/DEVELOPER-GUIDE.md` → `docs/Archive/DEVELOPER-GUIDE-2026-04-archived.md`
- Fold: `docs/BUILD-CONFIGURATIONS.md` content into `Tools/AGENTS.md`, then delete
- Modify: `Enlisted.csproj` (line 14 ref to BLUEPRINT — done via find_stale_refs)
- Modify: any other live references to those three docs

---

### Task G1: Grep-and-rescue from BLUEPRINT.md

- [ ] **Step 1: Read BLUEPRINT.md**

Use Read: `Read docs/BLUEPRINT.md`.

It's 632 lines. Scan for sections containing rules or patterns NOT already in the cascade (AGENTS.md root or any nested file).

- [ ] **Step 2: For each unique section, copy it into the right nested AGENTS.md**

Pattern for each rescue:
- Identify the rule (e.g., "Custom hot-reload pattern for X")
- Open the relevant nested AGENTS.md
- Append the rule under an appropriate heading
- Mark in BLUEPRINT.md (in a scratch file) which sections are now redundant

If a rule is **architectural diagram or walkthrough** that doesn't fit AGENTS.md's "rules/gotchas" format, leave it in the archived BLUEPRINT — it's still readable history.

The implementer's judgment determines what's worth rescuing. Common rescue candidates from BLUEPRINT-like docs:
- Build setup nuances not in Tools/AGENTS.md
- Pre-deployment checklists not in root AGENTS.md
- Project lifecycle conventions

- [ ] **Step 3: For each rescued section, stage the nested AGENTS.md edit**

After rescue, do NOT commit yet — accumulate all rescues for one big commit at the end of Phase G.

---

### Task G2: Grep-and-rescue from DEVELOPER-GUIDE.md

- [ ] **Step 1: Read DEVELOPER-GUIDE.md**

Same process as G1. Scan for content not in cascade.

- [ ] **Step 2: Rescue**

Likely targets:
- IDE config tips → `Tools/AGENTS.md` (or a new `docs/DEVELOPER-SETUP.md` if more than 30 lines)
- Onboarding flow → `Tools/AGENTS.md` or new file
- Common-tasks-by-example sections → not worth rescuing; the nested AGENTS.md already cover the rules

---

### Task G3: Fold BUILD-CONFIGURATIONS.md into Tools/AGENTS.md

- [ ] **Step 1: Read BUILD-CONFIGURATIONS.md**

Use Read: `Read docs/BUILD-CONFIGURATIONS.md`.

270 lines of build config detail.

- [ ] **Step 2: Append the content to Tools/AGENTS.md**

Open `Tools/AGENTS.md` (created Phase A — currently has a placeholder pointing AT BUILD-CONFIGURATIONS.md). Replace the placeholder section with the actual content from BUILD-CONFIGURATIONS.md, restructured to fit the AGENTS.md format.

The implementer should:
1. Drop any duplicated content (the Tools/AGENTS.md already mentions the basic build command)
2. Reorganize into AGENTS.md-style sections (rules/gotchas/patterns vs walkthrough prose)
3. Keep BUILD-CONFIGURATIONS sections that are real reference material; drop sections that became obvious or stale

- [ ] **Step 3: Delete BUILD-CONFIGURATIONS.md**

```bash
git rm docs/BUILD-CONFIGURATIONS.md
```

- [ ] **Step 4: Apply ref rewrites**

```bash
python3 Tools/Validation/find_stale_refs.py "docs/BUILD-CONFIGURATIONS.md" "Tools/AGENTS.md" --apply
```

---

### Task G4: Archive BLUEPRINT.md and DEVELOPER-GUIDE.md

- [ ] **Step 1: Move BLUEPRINT.md with deprecation header**

```bash
mkdir -p docs/Archive
git mv docs/BLUEPRINT.md docs/Archive/BLUEPRINT-2026-04-archived.md
```

Then prepend a header to the archived file:

```bash
# Read it, prepend header, write it back
HEADER='# ARCHIVED 2026-05-24

This document was the project blueprint before the docs+AGENTS.md restructure landed (Phase A-G commits dated 2026-05-24+). It is retained as historical context. Authoritative rules now live in the AGENTS.md cascade (root AGENTS.md + nested per-subsystem AGENTS.md files). See docs/INDEX.md.

---

'
echo -e "$HEADER" | cat - docs/Archive/BLUEPRINT-2026-04-archived.md > /tmp/blueprint.tmp
mv /tmp/blueprint.tmp docs/Archive/BLUEPRINT-2026-04-archived.md
```

- [ ] **Step 2: Same for DEVELOPER-GUIDE.md**

```bash
git mv docs/DEVELOPER-GUIDE.md docs/Archive/DEVELOPER-GUIDE-2026-04-archived.md
```

Prepend similar header.

- [ ] **Step 3: Apply ref rewrites**

```bash
python3 Tools/Validation/find_stale_refs.py "docs/BLUEPRINT.md" "docs/Archive/BLUEPRINT-2026-04-archived.md" --apply
python3 Tools/Validation/find_stale_refs.py "docs/DEVELOPER-GUIDE.md" "docs/Archive/DEVELOPER-GUIDE-2026-04-archived.md" --apply
```

Update `Enlisted.csproj` reference at line 14 — this should be picked up by the apply.

- [ ] **Step 4: Verify no stale refs remain**

```bash
python3 Tools/Validation/find_stale_refs.py "docs/BLUEPRINT.md"
python3 Tools/Validation/find_stale_refs.py "docs/DEVELOPER-GUIDE.md"
python3 Tools/Validation/find_stale_refs.py "docs/BUILD-CONFIGURATIONS.md"
```

Expected: all 3 say "No references to ... found."

---

### Task G5: Rewrite INDEX.md as master catalog

- [ ] **Step 1: Write the new INDEX.md**

Use Write to replace `docs/INDEX.md` with this template (target ~150 lines, ≤200):

```markdown
# Documentation Index — Enlisted

Master catalog of all AI-context files, design docs, and references.

**For agentic workers:** rules live in the AGENTS.md cascade. Open this index to find a specific topic; once located, work from the nested AGENTS.md or living reference, not from this catalog.

---

## AGENTS.md cascade

The rule cascade — both Codex (path-walk) and Claude Code (sibling CLAUDE.md auto-discovery) load these.

| Scope | File |
|---|---|
| Project-wide | [../AGENTS.md](../AGENTS.md) |
| Content / storylets / events / StoryDirector | [../src/Features/Content/AGENTS.md](../src/Features/Content/AGENTS.md) |
| Save system / offset table / serialization | [../src/Mod.Core/SaveSystem/AGENTS.md](../src/Mod.Core/SaveSystem/AGENTS.md) |
| ModuleData / JSON authoring | [../ModuleData/Enlisted/AGENTS.md](../ModuleData/Enlisted/AGENTS.md) |
| Validator / error-codes / lint | [../Tools/Validation/AGENTS.md](../Tools/Validation/AGENTS.md) |
| Conversations / dialog tokens | [../src/Features/Conversations/AGENTS.md](../src/Features/Conversations/AGENTS.md) |
| Activities / Orders / Home | [../src/Features/Activities/AGENTS.md](../src/Features/Activities/AGENTS.md) |
| Specs / plans / verification | [../docs/superpowers/AGENTS.md](../docs/superpowers/AGENTS.md) |
| Build / deploy / Workshop | [../Tools/AGENTS.md](../Tools/AGENTS.md) |

---

## Project-wide top-level docs

| Topic | File |
|---|---|
| Project overview (player-facing) | [PROJECT-OVERVIEW.md](PROJECT-OVERVIEW.md) |
| Current status | [superpowers/STATUS.md](superpowers/STATUS.md) |
| Project resources & links | [PROJECT-RESOURCES.md](PROJECT-RESOURCES.md) |
| Architecture briefs (cross-cutting) | [architecture/](architecture/) |
| Error code registry | [error-codes.md](error-codes.md) |
| Error code archive (historical) | [error-codes-archive.md](error-codes-archive.md) |

---

## docs/Features/ — per-subsystem handbooks

Mirror of `src/Features/` (each `docs/Features/<X>/` corresponds to a `src/Features/<X>/AGENTS.md`).

| Subsystem | Docs folder |
|---|---|
| CampaignIntelligence | [Features/CampaignIntelligence/](Features/CampaignIntelligence/) |
| Camp | [Features/Camp/](Features/Camp/) |
| Ceremonies | [Features/Ceremonies/](Features/Ceremonies/) |
| Combat | [Features/Combat/](Features/Combat/) |
| Companions | [Features/Companions/](Features/Companions/) |
| Company | [Features/Company/](Features/Company/) |
| Content | [Features/Content/](Features/Content/) |
| Conversations | [Features/Conversations/](Features/Conversations/) |
| Activities | [Features/Activities/](Features/Activities/) |
| Enlistment | [Features/Enlistment/](Features/Enlistment/) |
| Equipment | [Features/Equipment/](Features/Equipment/) |
| Identity | [Features/Identity/](Features/Identity/) |
| Interface | [Features/Interface/](Features/Interface/) |
| Patrons | [Features/Patrons/](Features/Patrons/) |
| Ranks | [Features/Ranks/](Features/Ranks/) |
| Retinue | [Features/Retinue/](Features/Retinue/) |

---

## docs/Reference/ — research and analysis

Decompile-based research, native API analyses, system deep-dives. Living/historical artifacts — may go stale across game updates.

See [Reference/](Reference/) for the full list (11 files as of 2026-05-24).

---

## docs/superpowers/ — specs and plans

| | |
|---|---|
| Conventions | [superpowers/AGENTS.md](superpowers/AGENTS.md) |
| Current status | [superpowers/STATUS.md](superpowers/STATUS.md) |
| Active specs | [superpowers/specs/](superpowers/specs/) |
| Active plans | [superpowers/plans/](superpowers/plans/) |
| Archived plans | [superpowers/plans/archive/](superpowers/plans/archive/) |

---

## docs/Archive/ — superseded docs

Pre-AGENTS.md-cascade documentation. Retained as historical context.

| File | Archived | Why |
|---|---|---|
| [Archive/BLUEPRINT-2026-04-archived.md](Archive/BLUEPRINT-2026-04-archived.md) | 2026-05-24 | Superseded by AGENTS.md cascade |
| [Archive/DEVELOPER-GUIDE-2026-04-archived.md](Archive/DEVELOPER-GUIDE-2026-04-archived.md) | 2026-05-24 | Superseded by AGENTS.md cascade + Tools/AGENTS.md |
| [Archive/Core-index-2026-05-archived.md](Archive/Core-index-2026-05-archived.md) | 2026-05-24 | docs/Features/Core/ was split into per-subsystem folders |

---

## Other folders

- `docs/Tools/` — tool reference. See [Tools/AGENTS.md](../Tools/AGENTS.md) for build/deploy and [Tools/Validation/AGENTS.md](../Tools/Validation/AGENTS.md) for validators.
```

(Adjust paths/folders based on what actually exists after F1-F4 — verify with `ls docs/Features/`.)

- [ ] **Step 2: Verify INDEX size**

```bash
wc -l docs/INDEX.md
```

Expected: ≤200 lines.

- [ ] **Step 3: Verify all referenced files exist**

The implementer can spot-check by clicking through, OR write a quick verify:

```bash
grep -oE '\[.*?\]\(.*?\)' docs/INDEX.md | grep -oE '\(.*?\)' | tr -d '()' | while read p; do
  full="docs/$p"
  full="${full#docs/../}"  # strip leading ../
  [ -e "$full" ] || echo "BROKEN: $p"
done
```

Expected: no "BROKEN" lines.

---

### Task G6: Verify Phase G fully + commit

- [ ] **Step 1: Phase 21 should pass with NO warnings about INDEX**

```bash
python3 Tools/Validation/validate_docs_structure.py
```

Expected: 0 failures, AND no "docs/INDEX.md does not catalog: X" warnings (INDEX now lists all 8 nested AGENTS.md).

- [ ] **Step 2: Build + content validator pass**

```bash
dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64 2>&1 | tail -3
python3 Tools/Validation/validate_content.py 2>&1 | tail -3
```

Both pass.

- [ ] **Step 3: Confirm all archived files are in place + no stale refs**

```bash
ls docs/Archive/
python3 Tools/Validation/find_stale_refs.py "docs/BLUEPRINT.md"
python3 Tools/Validation/find_stale_refs.py "docs/DEVELOPER-GUIDE.md"
python3 Tools/Validation/find_stale_refs.py "docs/BUILD-CONFIGURATIONS.md"
```

Expected: archive contains the 2-3 archived files; all three find_stale_refs show "No references found."

- [ ] **Step 4: Commit**

```bash
git add docs/Archive/ docs/INDEX.md Tools/AGENTS.md
git add -u  # picks up modifications (deleted BLUEPRINT/DEVELOPER-GUIDE/BUILD-CONFIGURATIONS + any rescued nested AGENTS.md + ref repairs)
git status --short  # verify
git commit -m "$(cat <<'EOF'
docs: archive BLUEPRINT + DEVELOPER-GUIDE, fold BUILD-CONFIGURATIONS, rewrite INDEX

Phase G of docs-and-agents-md-restructure. Final cleanup phase.

Archived (with deprecation headers + git mv to preserve history):
- docs/BLUEPRINT.md → docs/Archive/BLUEPRINT-2026-04-archived.md
- docs/DEVELOPER-GUIDE.md → docs/Archive/DEVELOPER-GUIDE-2026-04-archived.md

Folded:
- docs/BUILD-CONFIGURATIONS.md → Tools/AGENTS.md (and deleted)

Rescued unique content from archived files into nested AGENTS.md files
(see git diff of src/.../AGENTS.md files in this commit for details).

Rewrote docs/INDEX.md as ~150-line master catalog covering:
- AGENTS.md cascade (9 files)
- Project-wide top-level docs
- docs/Features/ mirror (16 subsystems)
- docs/Reference/ research artifacts
- docs/superpowers/ specs/plans
- docs/Archive/ history

Live references repaired via find_stale_refs.py --apply. Phase 21
validator passes with zero failures and zero INDEX-catalog warnings.

This is the final restructure commit. All 16 acceptance criteria
from the spec should be met.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

# Acceptance Verification

After Phase G ships, verify all 16 acceptance criteria from the spec:

- [ ] **AC1:** `wc -l AGENTS.md` ≤ 200
- [ ] **AC2:** 8 nested AGENTS.md exist; each within ±30% of target lines. Run:
  ```bash
  for f in src/Features/Content/AGENTS.md src/Mod.Core/SaveSystem/AGENTS.md ModuleData/Enlisted/AGENTS.md Tools/Validation/AGENTS.md src/Features/Conversations/AGENTS.md src/Features/Activities/AGENTS.md docs/superpowers/AGENTS.md Tools/AGENTS.md; do
    [ -f "$f" ] && printf '%6d  %s\n' "$(wc -l < $f)" "$f" || echo "MISSING: $f"
  done
  ```
- [ ] **AC3:** 8 nested CLAUDE.md shims exist; each ≤ 5 lines; each contains `@AGENTS.md`. Run:
  ```bash
  for f in src/Features/Content/CLAUDE.md src/Mod.Core/SaveSystem/CLAUDE.md ModuleData/Enlisted/CLAUDE.md Tools/Validation/CLAUDE.md src/Features/Conversations/CLAUDE.md src/Features/Activities/CLAUDE.md docs/superpowers/CLAUDE.md Tools/CLAUDE.md; do
    [ -f "$f" ] && grep -q '@AGENTS.md' "$f" && echo "OK: $f" || echo "BAD: $f"
  done
  ```
- [ ] **AC4:** `wc -l CLAUDE.md` ≤ 40 and `grep -q '@AGENTS.md' CLAUDE.md` succeeds
- [ ] **AC5:** `wc -l .gemini/GEMINI.md` ≤ 15, `grep -q '@../AGENTS.md' .gemini/GEMINI.md` succeeds, `grep -L 'onebodyamerica' .gemini/GEMINI.md` returns the filename (path absent)
- [ ] **AC6:** Phase 21 cross-link template check passes (look in its output for no "missing 'Parent:'" or "missing 'Handbook:'" warnings)
- [ ] **AC7:** `ls docs/Features/` lists Camp (not Campaign), Interface (not UI), Enlistment/Retinue/Ranks/Company/Companions (Core split), no Technical/
- [ ] **AC8:** `ls docs/Archive/` lists BLUEPRINT-2026-04-archived.md and DEVELOPER-GUIDE-2026-04-archived.md
- [ ] **AC9:** `[ ! -f docs/BUILD-CONFIGURATIONS.md ] && grep -q 'Build configurations' Tools/AGENTS.md`
- [ ] **AC10:** `wc -l docs/INDEX.md` ≤ 200, and all 8 nested AGENTS.md paths appear in it
- [ ] **AC11:** `[ -f docs/superpowers/STATUS.md ]`
- [ ] **AC12:** `grep -q '^CLAUDE.local.md$' .gitignore && git check-ignore -v CLAUDE.local.md`
- [ ] **AC13:** `python3 Tools/Validation/validate_docs_structure.py` exits 0
- [ ] **AC14:** `python3 Tools/Validation/find_stale_refs.py "docs/BLUEPRINT.md"` says "No references found." (and same for DEVELOPER-GUIDE.md, BUILD-CONFIGURATIONS.md)
- [ ] **AC15:** `dotnet build Enlisted.csproj -c "Enlisted RETAIL" /p:Platform=x64` succeeds (0 errors). Optional: walk back through phase commits with `git checkout <phase-commit>` + build verification at each.
- [ ] **AC16:** `git log --oneline -10` shows the phase commits in order (A, B, C, D, E, F.1, F.2, F.3, F.4, G).

---

# Coordination with feature branches

Per spec §6, the feature branches `feature/plan4-officer-trajectory` and `feature/plan5-endeavor-system` will hit conflicts on `AGENTS.md` and `CLAUDE.md` when rebasing post-Phase E. Recommendation:

```bash
# After Phase G lands on development:
git checkout feature/plan4-officer-trajectory
git rebase development
# Resolve mechanical conflicts in AGENTS.md and CLAUDE.md (their branches edit
# pre-shrink content; restructure moved that content to nested files). 
# The conflict resolution is: keep the restructure's structure, and re-apply
# the branch's content into the right nested AGENTS.md if it's still relevant.
git rebase --continue

git checkout feature/plan5-endeavor-system
# Same dance.
```

Not part of this plan (separate branch maintenance work), but worth flagging in the merge announcement.
