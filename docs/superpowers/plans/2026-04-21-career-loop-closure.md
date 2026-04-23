# Career Loop Closure Implementation Plan (Plan 5 of 5)

> **Status:** 🟡 **Half A partial** — Tasks T1-T9 shipped locally on `development` 2026-04-22 (commits `4f66604` → `e35f445`, 10 commits; not yet pushed to origin). T8 was verified no-op (`prior_service_*` flags already emitted at `EnlistmentLifecycleListener.cs:100-101`). Pending: T10 (in-game smoke — human operator). Half B (T11-T22) has not been started; prerequisites remain met (Plan 4 Phase F shipped 2026-04-22). See the Shipped Status Appendix at the bottom of this file for per-commit detail.

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Integration context:** This is Plan 5, the terminal plan in the five-plan integration roadmap — see [docs/superpowers/specs/2026-04-21-plans-integration-design.md](../specs/2026-04-21-plans-integration-design.md). Plans 1-5: (1) Campaign Intelligence Backbone; (2) Lord AI Intervention; (3) Signal Projection; (4) Duty Opportunities; **(5) Career Loop Closure — this plan**. Plan 5 closes the entire enlisted career loop: path crossroads + committed-path system + T7+ named-order variants + culture/trait overlays + debug hotkeys + save-load test + final playtest + verification doc. Plan 5 has two conceptual halves with different dependencies.

**Half A / Half B split:**

- **Half A (Path crossroads system)** — Phases A-F, Tasks T1-T10. Independent of Plan 1 snapshot; fires on existing `PathScorer` + `EnlistmentBehavior.OnTierChanged` events that already exist. **Parallelizable with Plans 2/3/4 after Plan 1 ships.** Does not need to wait for any downstream plan.
- **Half B (Polish + integration closure)** — Phases G-L, Tasks T11-T22. Culture/trait overlays operate on content authored in Plan 4. **Plan 4 Phase F shipped 2026-04-22 — Half B is unblocked.** Debug hotkeys, save-load test, playtest scenarios, and verification doc do not require Plan 4 content but follow after it to test the full loop.

**Goal:** Close the career arc — give players a meaningful T4/T6/T9 crossroads moment where their skill-gain pattern commits them to a path (or they resist and earn the flag), author T7+ named-order variants that reflect committed-path character, overlay culture/trait variants on hot-path storylets, ship debug hotkeys for Spec 2+ inspection, verify save-load reconstructs the full loop, run final playtest scenarios A-G across the integrated Plans 1-5 surface, and write the final verification doc that closes the Enlisted v2 redesign.

**Architecture:**

- **Crossroads (Half A)** fire on `EnlistmentBehavior.OnTierChanged`. Subscription lives in a new `PathCrossroadsBehavior` (or extends existing `PathScorer`). When tier reaches 4, 6, or 9 AND `committed_path` is unset for this milestone, pick the highest-scoring path from `path_*_score` qualities and emit a Modal crossroads storylet. Storylet options: commit (writes `committed_path` quality + grants focus-point + trait shift + renown) or resist (writes `path_resisted_<path>` flag to bias future scoring).
- **Committed-path gating (Half A)** — existing `StoryletCatalog` already reads `profile_requires` / `trigger`. T7+ variants use `trigger: ["rank_gte:7", "quality_gte:committed_path:<pathId>"]` to gate on committed path.
- **Culture/trait overlays (Half B)** — overlay storylets use existing Storylet schema fields `requires_culture` / `excludes_culture` / `requires_lord_trait` / `excludes_lord_trait` (already parsed by `StoryletCatalog` at lines 109-112). Each overlay targets a specific hot-path storylet id and replaces default rendering under matching culture/trait conditions.
- **Debug hotkeys (Half B)** — new `CareerDebugHotkeysBehavior` listens for key events (or console commands) and dumps snapshot / duty profile / arc state to session log.

**Tech Stack:** C# 7.3 / .NET Framework 4.7.2, Mount & Blade II: Bannerlord `CampaignBehaviorBase` + Enlisted `EnlistmentBehavior` / `PathScorer` / `QualityStore` / `FlagStore` / storylet backbone.

**Scope boundary — what this plan does NOT do:**

- No new Plan 1 snapshot fields. No new AI wrappers (Plan 2). No new signal families (Plan 3). No new duty-opportunity emission pipeline (Plan 4).
- Plan 5 Half B authoring CAN add content that Plan 4's emitter fires — but the emission path stays in Plan 4.
- No crossroads content for paths that don't exist. The existing `path_*_score` qualities are `path_ranger_score`, `path_enforcer_score`, `path_support_score`, `path_diplomat_score`, `path_rogue_score` — five paths. Plan 5 authors crossroads for these five paths at each milestone (T4/T6/T9).

**Verification surface:** `dotnet build` + `validate_content.py` + in-game smoke. Final playtest scenarios A-G cover the integrated loop. Verification doc (Task T22) captures the commit trail + known limitations + ownership transfer notes.

---

## Design decisions locked before task 1

### Crossroads milestones

- Fire at T4, T6, T9. Three milestones per path × 5 paths = 15 base crossroads storylets.
- At each milestone: pick the highest-scoring `path_*_score` quality. Emit the matching path's crossroads storylet.
- `committed_path` starts unset (null / empty string). After the first commit, it's locked for the remainder of the career — subsequent milestones can still fire but propose variants of the existing committed path.

### Commit vs resist semantics

- **Commit** writes `committed_path = "<pathId>"` (e.g. `"ranger"`) + grants rewards:
  - T4 commit: +1 focus point (applied to path's heavy skill) + trait nudge (+1 toward path's character axis) + 10 renown.
  - T6 commit: +1 attribute point (applied to path's heavy attribute) + 15 renown.
  - T9 commit: +1 additional focus + 25 renown + unlocks T7+ named-order variants.
  - (Actual values authored in Half A. Numbers are indicative.)
- **Resist** writes flag `path_resisted_<path>` (e.g. `path_resisted_ranger`). Future `PathScorer.BumpPath` calls check this flag and apply a 0.5× score multiplier when the resisted path tries to bump itself.

### Read-only quality semantics

`committed_path` is already in the validator's `_READ_ONLY_QUALITY_IDS` set (from earlier Spec 2 work). Plan 5 does NOT write it from storylet effects — storylets call a dedicated primitive that Plan 5 adds to `EffectExecutor`.

### New `EffectExecutor` primitive — `commit_path`

- Primitive id: `commit_path`. Params: `path` (string, one of the 5 path ids).
- Writes `committed_path` quality via a QualityStore back-door (bypasses Writable flag). Internal-only — not authored content.
- Plan 5 Phase A adds this primitive. Validator allows it only for storylets with ids matching `path_crossroads_*` (or a whitelist).

### Save-definer offsets

- No new class offsets. Plan 5 persists via existing QualityStore / FlagStore.
- No new enums.

### Culture overlay rule

- An overlay storylet has the same `id` base as the hot-path target but with a culture suffix: `<target_id>__khuzait` / `<target_id>__battanian` etc. (double-underscore separator).
- The emitter (Plan 3 or Plan 4) picks the overlay when `requires_culture` matches the player culture; falls back to the base storylet otherwise.
- ~15 hot-path storylets × 3-6 cultures = 45-90 overlay storylets. Target is ~15 hot-path × 3 cultures = 45. Finalize after Plan 4 ships and hot-path identification is possible.

### Lord-trait gate rule

- 15 storylets with `requires_lord_trait: [trait_id]` or `excludes_lord_trait: [trait_id]`. Applied to existing storylets (re-author via Edit) or as dedicated variant storylets.
- Example: an honorable lord's raiding content leans toward "spare the village" options; a calculating lord's content leans toward pragmatic gain.

### Half B dependency

Half B Phases G + H (culture/trait overlays) hard-require Plan 4 Phase F completion, which shipped 2026-04-22. Plan 5 Tasks T11-T14 no longer queue on Plan 4 — their "wait for Plan 4 Phase F" gates are now satisfied; proceed when Half A lands or earlier if content authoring is desired first.

---

## File structure

```
src/Features/CampaignIntelligence/Career/
  PathCrossroadsBehavior.cs              — fires crossroads on OnTierChanged

src/Features/Content/ (modified)
  EffectExecutor.cs                       — add commit_path primitive + resist_path primitive

src/Features/Debug/
  CareerDebugHotkeysBehavior.cs           — debug inspection hotkeys

Tools/Validation/ (modified)
  validate_content.py                     — Phase 15 full enforcement (path crossroads completeness)

ModuleData/Enlisted/Storylets/
  path_crossroads.json                    — 15 crossroads storylets (5 paths × 3 milestones)
  path_ranger_t7_variants.json            — T7+ named-order ranger variants
  path_enforcer_t7_variants.json          — T7+ named-order enforcer variants
  path_support_t7_variants.json           — T7+ named-order support variants
  path_diplomat_t7_variants.json          — T7+ named-order diplomat variants
  path_rogue_t7_variants.json             — T7+ named-order rogue variants
  culture_overlays.json                   — ~45 culture-overlay storylets (Half B)
  lord_trait_overlays.json                — ~15 lord-trait-gated storylets (Half B)

docs/superpowers/plans/
  2026-04-21-career-loop-verification.md  — final closure verification doc
```

Three new C# files, two modified. ~75+ authored storylets across Half A + Half B. No new save-definer offsets (everything persists through existing QualityStore + FlagStore + StoryletCatalog).

---

## Task index

**Half A — Path crossroads system (independent, parallelizable after Plan 1)**

- **Phase 0 — Scaffolding**: T1
- **Phase A — `commit_path` + `resist_path` primitives in `EffectExecutor`**: T2, T3
- **Phase B — `PathCrossroadsBehavior`**: T4, T5
- **Phase C — Authored crossroads storylets**: T6
- **Phase D — T7+ named-order variants (5 paths)**: T7
- **Phase E — `prior_service_*` flag verification**: T8
- **Phase F — Validator Phase 15 full enforcement**: T9, T10

**Half B — Polish + closure** (Plan 4 Phase F shipped 2026-04-22; no longer queued)

- **Phase G — Culture overlays (~45 storylets)**: T11, T12
- **Phase H — Lord-trait gated storylets (~15)**: T13, T14
- **Phase I — Debug hotkeys**: T15, T16
- **Phase J — Save-load arc reconstruction test**: T17
- **Phase K — Final playtest scenarios A-G**: T18, T19, T20, T21
- **Phase L — Verification doc + status closure**: T22

Total: 22 tasks.

---

# Half A — Path crossroads system

## Phase 0 — Scaffolding

### Task T1: csproj entries + confirm existing qualities

**Files:**
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Confirm `committed_path` exists as a read-only quality**

Grep `ModuleData/Enlisted/Qualities/quality_defs.json` for `committed_path` entry. If missing, add:

```json
{ "id": "committed_path", "displayName": "{=q_committed_path}Committed Path", "min": 0, "max": 0, "default": 0, "scope": "Global", "kind": "Stored", "writable": false, "decay": null }
```

Note: `committed_path` is a STRING concept stored as a quality placeholder + a flag with the path name. OR use `QualityStore` with integer-encoded path ids (0=unset, 1=ranger, 2=enforcer, etc.). Finalize encoding in T2 based on what `EffectExecutor.commit_path` primitive writes.

Actually — better encoding: use a **flag** `committed_path_<pathId>` (e.g. `committed_path_ranger`), not a quality. Flags are named-boolean, which matches this "one of five, or none" semantics naturally. `committed_path` quality can stay as a 0/1 "is any path committed" indicator if needed for validator purposes.

Lock decision here: **Flag-encoded commit.** Five flags: `committed_path_ranger`, `committed_path_enforcer`, `committed_path_support`, `committed_path_diplomat`, `committed_path_rogue`. `committed_path` quality (boolean integer 0/1) tracks "any path committed" for validator / debug purposes.

- [ ] **Step 2: Add csproj entries for new C# files**

```xml
    <Compile Include="src\Features\CampaignIntelligence\Career\PathCrossroadsBehavior.cs"/>
    <Compile Include="src\Features\Debug\CareerDebugHotkeysBehavior.cs"/>
```

- [ ] **Step 3: Commit**

```
feat(career): csproj + committed_path encoding decision

Plan 5 scaffolding. committed_path is encoded as five flags
(committed_path_<pathId>) + an integer quality indicator
(committed_path: 0=uncommitted, 1=committed). Flags match the
"one-of-five" semantics naturally. Adds csproj entries for
PathCrossroadsBehavior + CareerDebugHotkeysBehavior.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase A — `commit_path` + `resist_path` primitives

### Task T1.5: Add `SetDirect` to `QualityStore` (commit-path back door)

**Files:**
- Modify: `src/Features/Qualities/QualityStore.cs`

**Why this exists.** `QualityStore.Add` / `AddForHero` / `Set` all route through the `def.Writable` check (see `QualityStore.cs:119`, `:150`, `:178`) and refuse the write when the quality is declared `writable: false` in `quality_defs.json`. The `committed_path` indicator quality is authored `writable: false` on purpose — it must be unreachable from authored content so a stray storylet `quality_set` can't forge a commit. But the `commit_path` primitive (T2) is the ONE legitimate caller that needs to bump it; a dedicated bypass method is the clean way to express that exception.

`validate_content.py` Phase 12 continues to reject authored `quality_set` / `quality_add` writes to `writable: false` qualities — it scans effect JSON, not C#. `SetDirect` is a C#-only back door; storylet authors see no change.

- [ ] **Step 1: Add `SetDirect` method**

Insert a new public method in `QualityStore.cs` near `Set` (line 178):

```csharp
/// <summary>
/// Writes a stored quality's value DIRECTLY, bypassing the <see cref="QualityDefinition.Writable"/>
/// flag. Intended only for primitives that own a quality authored as read-only for storylet-authoring
/// safety (e.g. <c>committed_path</c> via the <c>commit_path</c> primitive). Authored content cannot
/// reach this path — validator Phase 12 continues to block <c>quality_set</c> / <c>quality_add</c>
/// on <c>writable:false</c> qualities. Unknown qualities and ReadThrough kinds still early-exit
/// with an Expected log — the Writable bypass applies only to Stored qualities declared in the
/// catalog.
/// </summary>
public void SetDirect(string qualityId, int value, string reason)
{
    var def = GetDefinition(qualityId);
    if (def == null)
    {
        ModLogger.Expected(
            "QUALITY",
            "set_direct_missing_def_" + qualityId,
            "SetDirect on unknown quality",
            new Dictionary<string, object> { { "id", qualityId } });
        return;
    }
    if (def.Kind == QualityKind.ReadThrough)
    {
        ModLogger.Expected(
            "QUALITY",
            "set_direct_readthrough_" + qualityId,
            "SetDirect refused: ReadThrough quality has no stored value to write",
            new Dictionary<string, object> { { "id", qualityId } });
        return;
    }
    if (!GlobalValues.TryGetValue(qualityId, out var v))
    {
        v = new QualityValue { Current = def.Default };
        GlobalValues[qualityId] = v;
    }
    v.Current = Clamp(v.Current + (value - v.Current), def.Min, def.Max);
    v.LastChanged = CampaignTime.Now;
}
```

(The clamp is preserved so `committed_path: 0/1` and any future bounded indicator quality continue to respect their authored min/max.)

VERIFY IN DECOMPILE: none — pure Enlisted-internal wiring.

- [ ] **Step 2: Commit**

```
feat(quality): add SetDirect for read-only indicator qualities

Narrow back-door write that bypasses def.Writable. Unlocks the
commit_path primitive (Plan 5 T2) to bump committed_path, which is
authored writable:false so storylets can't forge a commit.
validate_content.py Phase 12 continues to reject authored writes to
writable:false qualities — SetDirect is C# only. Rejects unknown /
ReadThrough qualities with Expected logs. Clamps to def.Min/Max.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T2: Add `commit_path` primitive to `EffectExecutor`

**Files:**
- Modify: `src/Features/Content/EffectExecutor.cs`

- [ ] **Step 1: Add case branch**

```csharp
case "commit_path":
    DoCommitPath(eff, ctx);
    break;
```

- [ ] **Step 2: Implement `DoCommitPath`**

```csharp
private static void DoCommitPath(EffectDecl eff, StoryletContext ctx)
{
    var pathId = GetStr(eff, "path");
    if (string.IsNullOrEmpty(pathId))
    {
        ModLogger.Expected("EFFECT", "commit_path_no_path", "commit_path primitive missing 'path' parameter");
        return;
    }

    var valid = new[] { "ranger", "enforcer", "support", "diplomat", "rogue" };
    if (!valid.Contains(pathId))
    {
        ModLogger.Expected("EFFECT", "commit_path_unknown", $"commit_path received unknown path '{pathId}'");
        return;
    }

    // Already committed? don't double-commit.
    if (FlagStore.Instance?.Has("committed_path_" + pathId) == true)
    {
        ModLogger.Expected("EFFECT", "commit_path_already", $"Path {pathId} already committed; no-op");
        return;
    }

    // CampaignTime.Never = permanent flag. Path commitment is career-lifetime;
    // matches the idiom at src/Features/Activities/Orders/EnlistmentLifecycleListener.cs:100.
    FlagStore.Instance?.Set("committed_path_" + pathId, CampaignTime.Never);

    // Back-door write to the committed_path indicator quality
    // (read-only from authored content; writable internally by this primitive).
    QualityStore.Instance?.SetDirect("committed_path", 1, "commit_path primitive");

    ModLogger.Info("PATH", $"committed path={pathId}");
}
```

VERIFY IN DECOMPILE: none — internal wiring. `QualityStore.SetDirect` is the back door this primitive needs; it is added by Task T1.5 above, which runs before this task. The `commit_path` primitive is its sole legitimate caller (Phase 12 continues to block authored-content writes to `writable:false` qualities).

- [ ] **Step 3: Commit**

```
feat(effects): add commit_path primitive

Writes committed_path_<pathId> flag + bumps committed_path quality
to 1 via back-door SetDirect (read-only for authored content;
writable internally). Guards against double-commit. Unknown path
ids log Expected.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T3: Add `resist_path` primitive to `EffectExecutor`

**Files:**
- Modify: `src/Features/Content/EffectExecutor.cs`

- [ ] **Step 1: Add case branch + implement `DoResistPath`**

```csharp
case "resist_path":
    DoResistPath(eff, ctx);
    break;

// ...

private static void DoResistPath(EffectDecl eff, StoryletContext ctx)
{
    var pathId = GetStr(eff, "path");
    if (string.IsNullOrEmpty(pathId))
    {
        ModLogger.Expected("EFFECT", "resist_path_no_path", "resist_path primitive missing 'path' parameter");
        return;
    }
    var valid = new[] { "ranger", "enforcer", "support", "diplomat", "rogue" };
    if (!valid.Contains(pathId))
    {
        ModLogger.Expected("EFFECT", "resist_path_unknown", $"resist_path received unknown path '{pathId}'");
        return;
    }

    // CampaignTime.Never = permanent. Plan T3 Step 3 commit note specifies
    // "no clearing mechanism (by design; the player's choice persists)."
    FlagStore.Instance?.Set("path_resisted_" + pathId, CampaignTime.Never);
    ModLogger.Info("PATH", $"resisted path={pathId}");
}
```

- [ ] **Step 2: Extend `PathScorer.BumpPath` to check resisted flag**

In `PathScorer.BumpPath(pathId, amount)`, before applying the bump, check if `path_resisted_<pathId>` flag is set. If set, multiply amount by 0.5.

```csharp
if (FlagStore.Instance?.Has("path_resisted_" + pathId) == true)
{
    amount = (int)(amount * 0.5f);
}
```

- [ ] **Step 3: Commit**

```
feat(effects+path): add resist_path primitive + PathScorer resist bias

resist_path writes the path_resisted_<pathId> flag. PathScorer.BumpPath
now checks the flag and applies a 0.5× score multiplier to resisted
paths. The resist stays in effect for the duration of the career —
no clearing mechanism (by design; the player's choice persists).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase B — `PathCrossroadsBehavior`

### Task T4: Create `PathCrossroadsBehavior.cs`

**Files:**
- Create: `src/Features/CampaignIntelligence/Career/PathCrossroadsBehavior.cs`

- [ ] **Step 1: Write the behavior**

```csharp
using System;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Qualities;
using Enlisted.Features.Flags;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Career
{
    /// <summary>
    /// Subscribes to EnlistmentBehavior.OnTierChanged. When tier reaches 4, 6,
    /// or 9, picks the highest-scoring path from path_*_score qualities and
    /// fires the matching crossroads storylet. Player's choice (commit vs
    /// resist) is handled by storylet options via commit_path / resist_path
    /// primitives.
    /// </summary>
    public sealed class PathCrossroadsBehavior : CampaignBehaviorBase
    {
        public static PathCrossroadsBehavior Instance { get; private set; }

        private static readonly int[] MilestoneTiers = { 4, 6, 9 };
        private static readonly string[] PathIds = { "ranger", "enforcer", "support", "diplomat", "rogue" };

        public override void RegisterEvents()
        {
            Instance = this;
            if (EnlistmentBehavior.Instance != null)
            {
                EnlistmentBehavior.Instance.OnTierChanged += OnTierChanged;
            }
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnTierChanged(int oldTier, int newTier)
        {
            try
            {
                if (Array.IndexOf(MilestoneTiers, newTier) < 0) return;

                // Pick the highest-scoring path.
                var pickedPath = "";
                var pickedScore = int.MinValue;
                foreach (var p in PathIds)
                {
                    var score = QualityStore.Instance?.Get("path_" + p + "_score") ?? 0;
                    if (score > pickedScore)
                    {
                        pickedScore = score;
                        pickedPath = p;
                    }
                }

                if (string.IsNullOrEmpty(pickedPath))
                {
                    ModLogger.Expected("PATH", "crossroads_no_path", "No path scored high enough for crossroads");
                    return;
                }

                // Check: if this path is already committed, fire a T7+-variant
                // storylet instead (the variant pool gates on committed_path).
                if (FlagStore.Instance?.Has("committed_path_" + pickedPath) == true
                    && newTier < 7)
                {
                    ModLogger.Expected("PATH", "crossroads_already_committed", $"Path {pickedPath} already committed; skipping crossroads at T{newTier}");
                    return;
                }

                var storyletId = $"path_crossroads_{pickedPath}_t{newTier}";
                var storylet = StoryletCatalog.GetById(storyletId);
                if (storylet == null)
                {
                    ModLogger.Expected("PATH", "crossroads_missing_storylet", $"No crossroads storylet for id={storyletId}");
                    return;
                }

                StoryDirector.Instance?.EmitCandidate(new StoryCandidate
                {
                    SourceId = "path.crossroads." + pickedPath,
                    CategoryId = "path.crossroads",
                    ProposedTier = StoryTier.Modal,
                    ChainContinuation = true, // user-opted milestone; bypass in-game floor
                    EmittedAt = CampaignTime.Now,
                    RenderedTitle = storylet.Title,
                    RenderedBody = storylet.Setup,
                    StoryKey = storyletId
                });

                ModLogger.Info("PATH", $"crossroads_fired: {storyletId} (score={pickedScore})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PATH", "OnTierChanged crossroads threw", ex);
            }
        }
    }
}
```

VERIFY IN DECOMPILE: none direct — uses Enlisted internal events.

- [ ] **Step 2: CRLF + commit**

```
feat(path): PathCrossroadsBehavior fires crossroads at T4/T6/T9

Subscribes to EnlistmentBehavior.OnTierChanged. At milestone tiers,
picks the highest-scoring path_*_score quality and fires the matching
path_crossroads_<path>_t<tier> storylet via StoryDirector at Modal
tier with ChainContinuation=true (user-opted milestone bypasses the
in-game floor + category cooldown; 60s wall-clock still applies).
Skips if the path is already committed (except for T7+ variants).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T5: Register `PathCrossroadsBehavior` in `SubModule`

**Files:**
- Modify: `src/Mod.Entry/SubModule.cs`

- [ ] **Step 1: Add AddBehavior call**

```csharp
campaignStarter.AddBehavior(new PathCrossroadsBehavior());
```

- [ ] **Step 2: Build + commit**

---

## Phase C — Authored crossroads storylets

### Task T6: Author `path_crossroads.json` (15 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/path_crossroads.json`

- [ ] **Step 1: Author 15 crossroads storylets (5 paths × 3 milestones)**

Each storylet has exactly three options: commit, resist, defer (or "noncommit" — leaves the path uncommitted but doesn't resist; re-rolls at the next milestone).

Sample storylet (ranger at T4):

```json
{
  "id": "path_crossroads_ranger_t4",
  "title": "{=path_cx_ranger_t4_t}The ranger's road.",
  "setup": "{=path_cx_ranger_t4_s}Your eye has sharpened. Your stride has quickened. Men ask for you when they need someone on the edge of the column — the scouts accept your company without comment, now. A choice opens before you.",
  "theme": "career",
  "category": "path_crossroads",
  "severity": "elevated",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "trigger": ["is_enlisted", "rank_gte:4"],
  "options": [
    {
      "id": "commit",
      "text": "{=path_cx_ranger_t4_commit}Take the ranger's road. Own it.",
      "tooltip": "{=path_cx_ranger_t4_commit_tt}Commit to ranger path. +1 Scouting focus + 10 renown.",
      "effects": [
        { "apply": "commit_path", "path": "ranger" },
        { "apply": "grant_focus_point", "skill": "Scouting", "amount": 1 },
        { "apply": "grant_renown", "amount": 10 }
      ]
    },
    {
      "id": "resist",
      "text": "{=path_cx_ranger_t4_resist}Not yet. Not the label.",
      "tooltip": "{=path_cx_ranger_t4_resist_tt}Resist ranger path. Future ranger bumps halved.",
      "effects": [
        { "apply": "resist_path", "path": "ranger" }
      ]
    },
    {
      "id": "defer",
      "text": "{=path_cx_ranger_t4_defer}Decide later. Keep the work.",
      "tooltip": "{=path_cx_ranger_t4_defer_tt}No commitment either way.",
      "effects": []
    }
  ]
}
```

Repeat for:
- Enforcer (heavy OneHanded / Polearm / Athletics)
- Support (heavy Medicine / Charm / Steward)
- Diplomat (heavy Leadership / Charm / Trade)
- Rogue (heavy Roguery / Charm / OneHanded)

T6 variants: similar but with attribute-point rewards instead of focus. T9 variants: larger renown rewards + unlock T7+ variants.

- [ ] **Step 2: Validator + sync loc + commit**

```
content(path): author path_crossroads.json (15 milestone storylets)

Plan 5 Phase C. Five paths × three milestones = 15 crossroads
storylets. Each Modal-tier, 3 options (commit / resist / defer).
Commit effects use the new commit_path primitive + grant_focus_point
(T4), grant_attribute_level (T6), or grant_renown (T9). Resist uses
resist_path. Defer has no effects — re-rolls at next milestone.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase D — T7+ named-order variants

### Task T7: Author T7+ variants for 5 paths

**Files:**
- Create: `ModuleData/Enlisted/Storylets/path_ranger_t7_variants.json`
- Create: `ModuleData/Enlisted/Storylets/path_enforcer_t7_variants.json`
- Create: `ModuleData/Enlisted/Storylets/path_support_t7_variants.json`
- Create: `ModuleData/Enlisted/Storylets/path_diplomat_t7_variants.json`
- Create: `ModuleData/Enlisted/Storylets/path_rogue_t7_variants.json`

- [ ] **Step 1: Author variants per path**

For each path: author ~3-5 T7+ variant storylets that overlay existing named-order archetypes (e.g. `order_scout`, `order_escort`, `order_treat_wounded`). Variants have higher severity, distinct outcomes, and narratively reflect the committed-path character.

Example (ranger T7+ scout variant):

```json
{
  "id": "order_scout_ranger_t7",
  "title": "{=order_scout_ranger_t7_t}Ride ahead. Alone.",
  "setup": "{=order_scout_ranger_t7_s}The column trusts your eye now. You ride ahead of the whole army — not with a scouting squadron, not with a partner — and the orders are to return when you've seen what needs seeing, not before.",
  "theme": "duty",
  "category": "named_order",
  "severity": "normal",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "trigger": ["is_enlisted", "rank_gte:7", "flag:committed_path_ranger"],
  "arc": {
    "duration_hours": 12,
    "phases": [
      {
        "id": "scout_solo_mid",
        "duration_hours": 10,
        "pool_prefix": "order_scout_ranger_t7_mid_",
        "fire_interval_hours": 3,
        "delivery": "auto"
      },
      {
        "id": "scout_solo_resolve",
        "duration_hours": 2,
        "pool_prefix": "order_scout_ranger_t7_resolve_",
        "fire_interval_hours": 0,
        "delivery": "auto"
      }
    ]
  },
  "options": [
    {
      "id": "accept",
      "text": "{=order_scout_ranger_t7_accept}Saddle up. Ride alone.",
      "tooltip": "{=order_scout_ranger_t7_accept_tt}Solo scout. Large Scouting + Riding XP.",
      "effects": [
        { "apply": "start_arc", "intent": "diligent" }
      ]
    }
  ]
}
```

Mid-arc pool and resolve pool follow same pattern (3-5 mid + 3 resolve-by-intent per variant — keep the scope small since there are 5 paths × 3-5 variants already).

- [ ] **Step 2: Validator + sync loc + commit (one commit per path, or one bundled commit)**

```
content(path): author T7+ path variants (ranger/enforcer/support/diplomat/rogue)

Plan 5 Phase D. 5 path variant files, ~4 archetypes per path. Each
variant is a new named order gated on rank_gte:7 +
flag:committed_path_<path>. Reuses arc-splice pipeline.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase E — `prior_service_*` flag verification

### Task T8: Verify `prior_service_*` flag emission

**Files:**
- None (verification-only task)

- [ ] **Step 1: Grep existing flag-emission code**

Search `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` for flag emissions on `OnEnlistmentEnded`. Expected: `FlagStore.Instance?.Set("prior_service_<faction>", CampaignTime.Never)` or similar (prior service is a permanent career record; `EnlistmentLifecycleListener.cs:100-101` is the established emission site).

- [ ] **Step 2: Log-verify during smoke**

If flag emission is present, smoke-verify: enlist, retire from service, check session log for `prior_service_<faction>` flag-set entry. If missing, implement in `OnEnlistmentEnded` handler and commit.

- [ ] **Step 3: Commit IF a fix was needed; otherwise no commit**

---

## Phase F — Validator Phase 15 full enforcement

### Task T9: Implement Phase 15 path-crossroads completeness check

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Extend Phase 15 from stub to full enforcement**

Phase 15 exists as a stub (see `validate_content.py` `phase15_path_crossroads` function at line 2952). **The stub gates on legacy id prefix `crossroads_` with bands `t3_to_t4`/`t5_to_t6`/`t7_to_t8`** — T6 shipped `path_crossroads_*_t{4,6,9}`, so the stub's guard never matches and Phase 15 currently no-ops. T9 rewrites the stub to the authored convention and adds full enforcement:

- Gate on prefix `path_crossroads_` (not `crossroads_`).
- For each of 5 paths × 3 milestones (T4 / T6 / T9) = 15 expected combos, assert a matching storylet exists.
- For each path, assert at least one T7+ variant exists with id matching `order_*_<path>_t7` AND triggers that include `flag:committed_path_<path>`. **Scope relaxation:** the original plan asked for "at least one T7+ variant for EACH of the 10 named-order archetypes per path" (= 150 storylet minimum). T7 shipped 5 paths × 2 shared archetypes (scout + escort) = 10 variants as the minimum viable Half A scope. Half B polish expands the archetype matrix later. Phase 15 enforcement must match the authored reality: `each_path_has_any_t7_variant`, NOT `each_path_has_variant_for_each_archetype`.

- [ ] **Step 2: Run validator + commit**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

```
feat(validator): Phase 15 path-crossroads completeness full enforcement

Promotes Phase 15 from stub to full enforcement. Rewrites the gate
from legacy prefix crossroads_ to the shipped path_crossroads_
convention, and asserts: (a) 15 base crossroads storylets (5 paths
× T4/T6/T9) exist; (b) each path has ≥1 T7+ variant whose triggers
include flag:committed_path_<path>. Per-archetype-per-path
enforcement is deferred to Half B polish (T7 shipped scout+escort
shared across the five paths as the minimum viable scope).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T10: Phase C smoke — crossroads fire + commit/resist applies

- [ ] **Step 1: Enlist + grind to T4**

Debug-hotkey XP injection or grind naturally. Verify `path_crossroads_<pickedPath>_t4` Modal fires.

- [ ] **Step 2: Commit option**

Pick commit. Verify: `committed_path_<pickedPath>` flag set, focus point granted, renown applied, `committed_path` quality = 1.

- [ ] **Step 3: Grind to T6**

Verify: T6 crossroads for the same path fires with attribute-point reward.

- [ ] **Step 4: Alternative — start new save, test resist**

On T4 crossroads, pick resist. Verify `path_resisted_<path>` flag set. Over the next 5 in-game days, verify `PathScorer.BumpPath` logs show 0.5× multiplier applied when the resisted path tries to bump.

- [ ] **Step 5: No commit** — verification only.

---

# Half B — Polish + integration closure

**Half B prerequisite met — Plan 4 Phase F shipped 2026-04-22.** The overlay targets (~15 hot-path storylets) can only be identified after Plan 4's authored duty pools exist.

## Phase G — Culture overlays

### Task T11: Identify hot-path storylets (Plan 4 Phase F shipped 2026-04-22)

**Files:**
- None (analysis task; output feeds T12)

- [x] **Step 1: Plan 4 Phase F prerequisite** — Plan 4 T15-T22 (duty pool authoring) shipped 2026-04-22 as part of Plan 4. Proceed.

- [ ] **Step 2: Identify hot-path targets**

Read Plan 4's authored pools (`duty_*.json`). Identify the 15 highest-frequency storylets — those most likely to fire under common snapshot states (e.g. `duty_garrisoned_routine`, `duty_marching_road_chatter`, `duty_besieging_work_detail`). Document in a hot-path table at the top of T12.

- [ ] **Step 3: No commit** — analysis only.

---

### Task T12: Author `culture_overlays.json` (~45 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/culture_overlays.json`

- [ ] **Step 1: Author 15 hot-path × 3 culture-variant overlays = 45 storylets**

Each overlay storylet uses the base id with a double-underscore culture suffix: `<base_id>__khuzait`, `<base_id>__battanian`, etc.

Example:

```json
{
  "id": "duty_marching_road_chatter_1__khuzait",
  "title": "{=duty_mar_chat_1_khu_t}At the horse lines.",
  "setup": "{=duty_mar_chat_1_khu_s}The Khuzait riders murmur low about a watering-hole three days east. Horse talk is half of soldiering, they say.",
  "theme": "duty",
  "category": "duty_marching",
  "severity": "low",
  "cooldown_days": 2,
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "profile_requires": ["marching"],
  "requires_culture": ["khuzait"],
  "trigger": ["is_enlisted"],
  "options": [
    {
      "id": "main",
      "text": "{=duty_mar_chat_1_khu_opt}Listen in.",
      "tooltip": "{=duty_mar_chat_1_khu_tt}Riding XP.",
      "effects": [
        { "apply": "skill_xp", "skill": "Riding", "amount": 15 }
      ]
    }
  ]
}
```

Cultures to overlay: Khuzait, Battanian, Sturgian, Vlandian, Empire, Aserai — pick 3 per hot-path target (not all 6) for target 45.

- [ ] **Step 2: Emitter behavior — culture-overlay preference**

Modify Plan 4's `EnlistedDutyEmitterBehavior.PickStoryletFromPool` (or Plan 3's equivalent) to prefer culture-overlay variants when `requires_culture` matches the player's culture. Ordering: overlay-variants first, base second.

```csharp
// In PickStoryletFromPool: prefer culture overlay over base.
var playerCulture = Hero.MainHero?.Culture?.StringId;
var overlayMatches = StoryletCatalog.All
    .Where(s => s?.Id != null
        && s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && s.Id.Contains("__")
        && s.RequiresCulture.Contains(playerCulture, StringComparer.OrdinalIgnoreCase))
    .ToList();
// Try overlay first, fall back to base.
```

- [ ] **Step 3: Validator + sync loc + commit**

```
content(path): author culture_overlays.json (~45 storylets)

Plan 5 Phase G. Covers 15 hot-path storylets × 3 cultures per target.
Overlay convention: <base_id>__<culture_id>. Emitter prefers overlays
over base when player culture matches.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase H — Lord-trait gated storylets

### Task T13: Identify lord-trait gate targets

**Files:**
- None (analysis task)

- [ ] **Step 1: Select 15 storylets for trait gating**

Pick storylets where trait-variance is narratively meaningful. Examples: raiding storylets (Honor trait — "spare the village"/"burn it"), inspection storylets (Calculating trait — "cut the corner"), diplomacy-heavy storylets (Mercy trait).

---

### Task T14: Author `lord_trait_overlays.json` (~15 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/lord_trait_overlays.json`

- [ ] **Step 1: Author 15 trait-gated storylets**

Each uses `requires_lord_trait: ["<trait_id>"]` or `excludes_lord_trait: ["<trait_id>"]`.

```json
{
  "id": "duty_raiding_spare_village_1",
  "title": "{=duty_raid_spare_1_t}A mother at the door.",
  "setup": "{=duty_raid_spare_1_s}...",
  "theme": "duty",
  "category": "duty_raiding",
  "severity": "elevated",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "profile_requires": ["raiding"],
  "requires_lord_trait": ["Mercy"],
  "trigger": ["is_enlisted"],
  "options": [
    { "id": "main", "text": "...", "tooltip": "...", "effects": [ ... ] }
  ]
}
```

VERIFY IN DECOMPILE: trait ids — which string values does the game use? Check `TaleWorlds.Core.DefaultTraits` or `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultTraits.cs` for canonical trait string ids.

- [ ] **Step 2: Validator + sync loc + commit**

---

## Phase I — Debug hotkeys

### Task T15: Create `CareerDebugHotkeysBehavior.cs`

**Files:**
- Create: `src/Features/Debug/CareerDebugHotkeysBehavior.cs`

- [ ] **Step 1: Write debug hotkey behavior**

Implements three commands (accessed via chat-console or key combinations):

- `?intel` — dumps current `EnlistedLordIntelligenceSnapshot` to session log (all fields + RecentChangeFlags).
- `?force_profile <profile>` — forces `OrderActivity.CurrentDutyProfile` to the given string via `DutyProfileBehavior.ForceProfile`.
- `?arc` — dumps current `OrderActivity.ActiveNamedOrder` state (order id, phase index, combat class at accept, intent).
- `?crossroads <path> <tier>` — fires a specific crossroads storylet for testing.

Uses Bannerlord's existing console command infrastructure. VERIFY IN DECOMPILE: `TaleWorlds.MountAndBlade.CommandLineFunctionality` or similar — check `../Decompile/` for the canonical console-command registration path.

- [ ] **Step 2: CRLF + commit**

```
feat(debug): CareerDebugHotkeysBehavior for Spec 2+ inspection

Adds four console commands: ?intel (dumps Plan 1 snapshot), 
?force_profile <profile> (forces duty profile), ?arc (dumps
ActiveNamedOrder state), ?crossroads <path> <tier> (fires
crossroads storylet for testing). Debug-only.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T16: Register `CareerDebugHotkeysBehavior`

**Files:**
- Modify: `src/Mod.Entry/SubModule.cs`

- [ ] **Step 1: Register behavior (debug gate)**

Only register if a debug-mode flag is enabled (existing convention in SubModule — look for `DebugToolsBehavior` registration pattern).

- [ ] **Step 2: Build + commit**

---

## Phase J — Save-load arc reconstruction test

### Task T17: Save-load mid-arc test scenario

**Files:**
- None (smoke-test procedural)

- [ ] **Step 1: Procedure**

1. Launch, enlist with a lord.
2. Accept a named order (e.g. `order_guard_post`).
3. Advance to mid-arc phase (observe `DUTYPROFILE heartbeat` + arc phase index > 0).
4. Save to slot.
5. Exit to main menu.
6. Reload from slot.
7. Verify: `OrderActivity.Instance.ActiveNamedOrder != null`, `OrderStoryletId` matches accepted order, `CurrentPhaseIndex` matches pre-save index. Arc phases rebuild via `OrderActivity.ReconstructArcOnLoad` (already shipped in Phase A).
8. Advance to resolve phase. Verify resolve storylet fires.

- [ ] **Step 2: No commit** — procedural verification only. Document result in T22 verification doc.

---

## Phase K — Final playtest scenarios A-G

### Task T18: Author playtest scenario doc

**Files:**
- Create: `docs/superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md`

- [ ] **Step 1: Author 7 scenarios**

- **Scenario A — Peaceful garrison run.** 30 in-game days at a friendly town, no active war. Verify quiet-stretch signals dominate; ambient garrisoned storylets fire at expected cadence; no crashes; named-order accept-and-complete ≥ 3 times.
- **Scenario B — Marching army campaign.** Enlist with a lord who joins an army. 20 days marching. Verify marching-profile pool, snapshot signals shift toward front pressure, lord-AI behavior under Plan 2 wrappers.
- **Scenario C — Full siege cycle.** Start siege, besiege, breach, capture. Verify besieging-profile pool, Plan 4 arc-scale `order_siege_works` proposals, transition to marching on siege-end.
- **Scenario D — Raid campaign.** Raiding profile, ethical-choice storylets, post-raid aftermath notices.
- **Scenario E — Lord captured + released.** Force lord capture. Verify profile forces to imprisoned, imprisoned pool fires, transitions back on release. `prior_service_*` flag sets on enlistment end.
- **Scenario F — Cross-culture service.** Enlist with a Khuzait lord as a Vlandian player; verify culture-overlay storylets fire (not only the base versions).
- **Scenario G — T9 full-career arc.** Grind from T1 to T9 with path commitments at T4/T6/T9. Verify crossroads fire each time; committed-path T7+ variants appear; path-resist flags persist and bias future scoring.

- [ ] **Step 2: Commit**

```
docs(path): add final-playtest scenario doc (A-G)

Plan 5 Phase K scenarios for full-loop integration smoke. Covers
peaceful garrison, marching army, siege cycle, raid, capture +
release, cross-culture service, and full T9 career arc.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T19: Execute Scenarios A + B

Execute Scenarios A and B per the doc. Log outcomes + any defects.

### Task T20: Execute Scenarios C + D + E

### Task T21: Execute Scenarios F + G

---

## Phase L — Verification doc + status closure

### Task T22: Final verification doc + CLAUDE.md closure

**Files:**
- Create: `docs/superpowers/plans/2026-04-21-career-loop-verification.md`
- Modify: `CLAUDE.md`
- Modify: `docs/INDEX.md`

- [ ] **Step 1: Author verification doc**

Summarize: tasks shipped, commit trail, known limitations, deferred items (e.g. T4+ culture overlays limited to 3 cultures per target; expand later), scenarios A-G results. Link to per-plan verification docs (Plan 1 Phase H, Plan 2 Phase H, Plan 3 T29, Plan 4 T28).

- [ ] **Step 2: Update CLAUDE.md status**

Mark all five plans ✅ shipped. Update "Current project status" block to reflect end-of-v2-redesign state.

- [ ] **Step 3: Update docs/INDEX.md**

Final entries for all five plans. Retire any remaining references to the archived Orders plan or the integration spec as the live roadmap (integration spec becomes historical).

- [ ] **Step 4: Commit**

```
docs: Plan 5 + integrated Plans 1-5 closure verification

Plan 5 (Career Loop Closure) complete. Closes the five-plan
integrated career arc. Ships PathCrossroadsBehavior + commit_path/
resist_path primitives, 15 path-crossroads storylets, 5 T7+ path
variant files, Phase 15 full enforcement, culture overlays (~45),
lord-trait gates (~15), debug hotkeys, save-load test, scenarios
A-G executed. CLAUDE.md + docs/INDEX.md updated to reflect the
v2 redesign complete.

Enlisted v2 shipped.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Cross-plan integration summary

- **Plan 1 dependency:** Plan 5 Half A is independent of Plan 1 snapshot. Plan 5 Half B touches no Plan 1 code.
- **Plan 2 relationship:** Independent. Plan 5 doesn't read Plan 2.
- **Plan 3 relationship:** Plan 5 Half B culture-overlay emission preference modifies Plan 3's / Plan 4's picker — minor shared-code touch in `PickStoryletFromPool`. Document in T12.
- **Plan 4 dependency:** Plan 5 Half B (Phase G + H) hard-requires Plan 4 Phase F completion. Plan 4 shipped 2026-04-22; prerequisite met. Half A has no Plan 4 dependency.
- **Shared code surface:** `EffectExecutor.cs` (Plan 5 adds 2 primitives), `PathScorer.cs` (Plan 5 adds resist-multiplier check), `PickStoryletFromPool` in Plan 3/4 emitter (Plan 5 adds culture-overlay preference). All additive.

---

## Shipping checklist

**Half A (after Plan 1, parallelizable):**
- [x] T1 — scaffolding + committed_path encoding decision (shipped `4f66604`; also added `T1.5 — QualityStore.SetDirect` at `cbf59a5`)
- [x] T2-T3 — commit_path + resist_path primitives + PathScorer resist bias (shipped `0bee054`, `bb772d7`; `PathIds` helper extracted for dedupe)
- [x] T4-T5 — PathCrossroadsBehavior registered (shipped `70b1f33`, `2be5966`)
- [x] T6 — 15 crossroads storylets authored (shipped `e6ef67e`; attribute StringIds corrected to lowercase per `DefaultCharacterAttributes.cs:45-50`)
- [x] T7 — T7+ path variants authored (shipped `88e7486`; minimum-viable scope = 5 paths × 2 shared archetypes (scout + escort) × (entry + mid + resolve) = 30 storylets. Per-archetype matrix deferred to Half B polish)
- [x] T8 — prior_service_* flag verification (verified no-op — `prior_service_culture_<id>` + `prior_service_rank_<N>` already emitted at `src/Features/Activities/Orders/EnlistmentLifecycleListener.cs:100-101`; zero commits)
- [x] T9 — Phase 15 validator rewrite (shipped `e35f445`; gate rewritten from legacy `crossroads_*_t3_to_t4` to `path_crossroads_*_t{4,6,9}`; per-path `has_any_t7_variant` enforcement via `flag:committed_path_<path>` trigger check; emits "OK: all 15 milestone storylets present; T7+ variants per path — ranger=2, enforcer=2, support=2, diplomat=2, rogue=2.")
- [ ] T10 — in-game smoke (human operator pending)

**Half B (Plan 4 Phase F shipped 2026-04-22; no longer queued):**
- [ ] T11-T12 — hot-path identification + culture overlays (~45 storylets)
- [ ] T13-T14 — lord-trait gate targets + overlays (~15 storylets)
- [ ] T15-T16 — debug hotkeys behavior + registration
- [ ] T17 — save-load arc reconstruction test
- [ ] T18-T21 — playtest scenarios A-G doc + execution
- [ ] T22 — final verification doc + CLAUDE.md closure

Total: 22 tasks. Estimated effort: Half A — 2-3 implementation sessions; Half B — 3-4 sessions; Plan 4 Phase F already shipped 2026-04-22.

---

## Shipped status appendix (updated 2026-04-22)

Half A tasks T1-T7 shipped locally on `development` 2026-04-22 across 8 commits. Not pushed to `origin/development` at appendix-time; `git push` is user-controlled.

| Commit | Task | What landed |
| --- | --- | --- |
| `4f66604` | T1 | `committed_path` indicator quality (Stored, Global, `writable:false`, `min=0 max=1 default=0`) in `ModuleData/Enlisted/Qualities/quality_defs.json`. **Plan-vs-shipped delta:** the plan's literal `"max": 0` was changed to `"max": 1` — `QualityStore.SetDirect` clamps to `def.Max`, so `max=0` would silently null any write. `csproj` entries for `PathCrossroadsBehavior` and `CareerDebugHotkeysBehavior` were NOT added in T1 (plan prescribed both) — instead moved to the tasks that actually create those files (T4 created the Career entry; Half B T15 will create the debug entry). Registering a `Compile` for a non-existent file breaks intermediate builds. |
| `cbf59a5` | T1.5 | `QualityStore.SetDirect(qualityId, value, reason)` back-door write method that bypasses `def.Writable`. Rejects unknown + ReadThrough qualities with `ModLogger.Expected`; for Stored qualities, writes `Clamp(value, def.Min, def.Max)` to `GlobalValues[qualityId].Current` and bumps `LastChanged`. Authored-content paths cannot reach `SetDirect` — validator Phase 12 continues to block `quality_set`/`quality_add` on `writable:false` qualities. |
| `0bee054` | T2 | `commit_path` effect primitive in `EffectExecutor.cs` (switch case + `DoCommitPath` helper). Validates `path ∈ {ranger,enforcer,support,diplomat,rogue}` via a local HashSet (refactored to the shared `PathIds.Set` in T3). Idempotent double-commit via `FlagStore.Has` check. Writes `committed_path_<path>` flag permanent + `SetDirect("committed_path", 1, ...)`. Also added `commit_path` to the Phase 12 known-primitives set so authored T6 storylets pass validation (coupled change). |
| `bb772d7` | T3 | New `src/Features/Activities/Orders/PathIds.cs` with `All` (`IReadOnlyList<string>` of the five ids) + `Set` (`HashSet<string>` with OrdinalIgnoreCase). T2's local HashSet deleted. New `resist_path` primitive mirrors `commit_path` for the rejection side (no idempotent guard — permanent-flag write is naturally idempotent). `PathScorer.BumpPath` extended: if `path_resisted_<path>` flag is set, `amount = (int)(amount * 0.5f)` before the quality write. Validator Phase 12 known-primitives set gained `resist_path`. |
| `70b1f33` | T4 | New `src/Features/CampaignIntelligence/Career/PathCrossroadsBehavior.cs`. **Plan-vs-shipped delta:** plan line 435 prescribed `EnlistmentBehavior.Instance.OnTierChanged +=` — WRONG. `OnTierChanged` is `public static event Action<int, int>` at `EnlistmentBehavior.cs:8490`. Subscription goes on the TYPE, not `.Instance`. Subscription pattern matches `PathScorer.cs:47-48` (`-= / +=` guard against double-sub on save/load). Handler walks `PathIds.All`, picks highest `path_<id>_score` with stable first-match tie-break (`>`, not OrderByDescending). Skips already-committed-before-T7 and no-score-above-zero cases. Builds `StoryletContext` + calls `StoryletEventAdapter.BuildModal(storylet, ctx, null)` + emits `StoryCandidate` with `InteractiveEvent = evt`, `ChainContinuation = true` (milestone bypasses in-game floor + category cooldown per `StoryDirector.Route`). All `Expected`/`Caught` calls use literal category + summary (no `$"..."` interpolation — Phase 10 gate). `csproj` Compile entry added. |
| `2be5966` | T5 | Behavior registered in `SubModule.cs` after the Plan 4 Duty emitter. Keeps Intelligence / Signal / Duty / Career cadence aligned. Known-footgun hit during this commit: the `Write` tool errored because `/c/Users/coola/commit.txt` had been modified since the last Read, and `git commit -F` shipped stale content with T4's subject. Fixed via message-only `git commit --amend` on the local unpushed commit. |
| `e6ef67e` | T6 | `ModuleData/Enlisted/Storylets/path_crossroads.json` — 15 storylets, 5 paths × 3 milestones (T4 / T6 / T9). Each Modal with 3 options (`commit`, `resist`, `defer`). T4 commit = `commit_path` + `grant_focus_point` on path's lead skill + `grant_renown:10`. T6 commit = `commit_path` + `grant_attribute_level` on path's heavy attribute + `grant_renown:15`. T9 commit = `commit_path` + `grant_unspent_focus:1` + `grant_renown:25`. Resist = `resist_path`. Defer = no-op. **Plan-vs-shipped delta:** engine's `DefaultCharacterAttributes` at `../Decompile/TaleWorlds.Core/DefaultCharacterAttributes.cs:45-50` registers attributes with **lowercase** StringIds (`vigor`, `cunning`, `social`, `intelligence`) — NOT the title-case the prompt showed. All T6 attribute effects use lowercase. 120 `path_cx_*` loc entries synced to `enlisted_strings.xml`. |
| `88e7486` | T7 | Five new files `ModuleData/Enlisted/Storylets/path_{ranger,enforcer,support,diplomat,rogue}_t7_variants.json`. 6 storylets per file × 5 files = 30 total. Two shared archetypes (scout + escort) per path × (1 entry + 1 mid + 1 resolve) per archetype. Entry triggers `["is_enlisted", "rank_gte:7", "flag:committed_path_<path>"]`. Arc 10h = 8h mid + 2h resolve. **Plan-vs-shipped delta:** plan T7 resolve schema omitted `clear_active_named_order` — every base resolve in `order_scout.json` / `order_escort.json` includes it, and without it the named order never unsplices. Implementer added to every resolve. `class_affinity` included on entries with path-themed biases (ranger cavalry/horse-archer, enforcer heavy-infantry, etc.). 40 string blocks added to `enlisted_strings.xml`. |
| — | T8 | Verified no-op. `prior_service_culture_<id>` and `prior_service_rank_<N>` flags are already set on `OnEnlistmentEnded` permanent (`CampaignTime.Never`) at `src/Features/Activities/Orders/EnlistmentLifecycleListener.cs:100-101`. Subscription guard `-=` / `+=` at `:24-25`, upstream invocation at `EnlistmentBehavior.cs:3287` inside try/catch. Zero commits; no C# edit required. |
| `e35f445` | T9 | `Tools/Validation/validate_content.py` Phase 15 rewritten from stub to full enforcement (`phase15_path_crossroads` at line 2954). Gate now fires on prefix `path_crossroads_` (was legacy `crossroads_`); milestones `t4 / t6 / t9` (were `t3_to_t4 / t5_to_t6 / t7_to_t8`). Asserts (a) 15 base crossroads storylets exist, (b) each of five paths has at least one T7+ variant (any storylet whose `trigger` array includes `flag:committed_path_<path>`). Per-archetype-per-path enforcement is explicitly NOT implemented — Half B polish expands the archetype matrix later. Phase 15 now prints "OK: all 15 milestone storylets present; T7+ variants per path — ranger=2, enforcer=2, support=2, diplomat=2, rogue=2." |

### Plan-vs-shipped deltas carrying forward

| # | Drift | Impact for next session |
| --- | --- | --- |
| 1 | `committed_path` quality max | Already fixed at T1. No action. |
| 2 | `OnTierChanged` is static | Already fixed at T4. Next session's T8-T10 implementers don't touch it. |
| 3 | `ModLogger.Expected/Caught/Surfaced` summary/category must be literal | Bake into every C# implementer prompt going into Half B. |
| 4 | Attribute StringIds lowercase (`vigor` not `Vigor`) | Bake into any Half B content-authoring prompt that adds `grant_attribute_level`. |
| 5 | Resolves need `clear_active_named_order` | Bake into Half B content-authoring prompts that add new named-order resolves. |
| 6 | Phase 15 stub uses legacy `crossroads_` prefix + `t3_to_t4` bands | T9 implementer rewrites to `path_crossroads_*_t{4,6,9}`. |
| 7 | T9 bullet (c) scope relaxed from per-archetype-per-path to per-path-any-variant | See T9 in-body spec (line 719 onwards). Half B polish can expand. |
| 8 | `PathCrossroadsBehavior` file location (`CampaignIntelligence/Career/`) | Reads QualityStore directly, not snapshot-driven. Cohesion would favor `Activities/Orders/` next to `PathScorer` / `PathIds`. Left as-is per plan; flagged in Plan 5 retrospective. |

### Next-session starting point

T1-T9 all landed this session. T8 was verified no-op; T9 shipped at `e35f445`. The only Half A action remaining is a human-operator smoke (T10), plus an optional `git push origin development` once the session owner approves.

1. **T10 — in-game smoke (human only).** Grind to T4 crossroads on a save with a pre-existing path-score lean; commit one path + verify `committed_path_<path>` flag set + reward effects applied + indicator quality `committed_path == 1` + a T6 re-fire for the same path with attribute-grant reward. Separately on a fresh save: resist at T4 and verify `path_resisted_<path>` flag set + `PathScorer.BumpPath` halves future bumps to that path (watch session-log `PATH` entries). Then grind past T7 with the committed path and verify a `order_scout_<path>_t7` or `order_escort_<path>_t7` variant fires in place of the base scout/escort order.
2. **Half B (T11-T22).** Culture overlays, lord-trait gates, debug hotkeys, save-load test, playtest scenarios A-G, verification doc. Prerequisites (Plan 4 Phase F content) already met.
3. **Optional:** `git push origin development` — session is 11 commits ahead of origin and no commits have been published. Push is deferred to user consent per CLAUDE.md policy.
