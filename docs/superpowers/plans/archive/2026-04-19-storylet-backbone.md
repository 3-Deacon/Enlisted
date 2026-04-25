# Storylet Backbone Implementation Plan

> **RETIRED (2026-04-19).** This plan is a frozen-in-time execution record. For day-to-day authoring reference — seed catalogs, trigger/slot/primitive lists, save-definer offsets, pitfalls — use the living doc at [`docs/Features/Content/storylet-backbone.md`](../../Features/Content/storylet-backbone.md). For design rationale, see the (also retired) spec at [`../specs/2026-04-19-storylet-backbone-design.md`](../specs/2026-04-19-storylet-backbone-design.md).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps used checkbox (`- [ ]` / `- [x]`) syntax for tracking.

**Status:** Implementation complete (2026-04-19). All 25 C# files landed, quality_defs.json + scripted_effects.json seeded, validate_content.py Phase 12 live, `dotnet build` + validator green end-to-end. Commit trail `a5c3344` → `45b38bf` on `development`; post-review hardening (scripted-effect depth cap + TriggerRegistry Caught-summary literal) in the subsequent commit. Surface specs 1–5 may now author content against this substrate.

**Goal:** Ship Spec 0 — the content-layer substrate (Storylets, Qualities, Flags, Activities, Scopes, Chains, Scripted Effects) for the Enlisted mod. Plumbing only. No content migration. Surface specs 1–5 consume this.

**Architecture:** Hybrid code-first / data-first — storylet text + effect parameters live in JSON; triggers and primitive effects live in a C# registry. `QualityStore` is the single home for typed numeric state (with read-through proxies over `EnlistmentBehavior` + native `Hero` APIs). `FlagStore` owns named boolean state (global + hero-scoped). `ActivityRuntime` drives Banner-Kings–style stateful ticking activities with intent-biased phase pools. `Storylet.ToCandidate` compiles a content definition into a `StoryCandidate` the existing `StoryDirector` already knows how to route.

**Tech Stack:** C# 11 on .NET Framework 4.7.2, Bannerlord v1.3.13 (`TaleWorlds.CampaignSystem`, `TaleWorlds.SaveSystem`, `TaleWorlds.Localization`), Newtonsoft.Json. No unit-test framework — verification via `dotnet build` + `Tools/Validation/validate_content.py` + in-game `DebugToolsBehavior` smoke helpers.

**Spec:** [docs/superpowers/specs/2026-04-19-storylet-backbone-design.md](../specs/2026-04-19-storylet-backbone-design.md) (commits `8fce95b` → `c89bd4d`).

---

## Conventions used in every task

- **Commit hygiene.** Every task ends with a commit. Always `git add <specific paths>`; never `git add -A` (another AI session may be editing in parallel).
- **Commit messages.** Follow the repo style: `category(scope): short subject` with a `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>` trailer. Multi-line messages go through a temp file (bash here has no `cat` heredoc).
- **CRLF normalization.** The Write tool creates LF-only files; `.gitattributes` enforces CRLF on `.cs / .csproj / .sln / .ps1`. For **every newly created** file in one of those extensions, run once (and ONLY once — the script has a double-BOM bug if re-run): `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path <path>`. For edits to existing `.cs` files, rely on `.gitattributes` CRLF at commit time.
- **csproj registration.** Every new `.cs` must be added to `Enlisted.csproj` as `<Compile Include="src\Features\..." />` — missing entries break the build.
- **TaleWorlds API source of truth.** All TaleWorlds API verification happens against `C:\Dev\Enlisted\Decompile\` — never web search, never Context7 for TaleWorlds. Third-party libs (Harmony, Newtonsoft) may use Context7.
- **ModLogger.** Surfaced errors use `ModLogger.Surfaced("CATEGORY", "summary string literal", ex, ctx?)` — category MUST be a `[A-Z][A-Z0-9\-]*` string literal; summary MUST be a string literal (registry scanner requires it). Catch-and-log defensively uses `ModLogger.Caught(...)`; known-branch early exits use `ModLogger.Expected(...)`. Never use the retired `ModLogger.Error` — `validate_content.py` Phase 11 blocks it.
- **Bash on Windows.** `dotnet` / `git` / `python` need `PATH` prepended: `export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"`. Use Unix slashes (`/dev/null`), not `NUL`.
- **Build command.** `dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64` (the space in the config name trips bash; quote it). Close `BannerlordLauncher.exe` first — it holds the DLL open and the csproj post-build mirror fails with MSB3021.

---

## File structure

### New C# files (25)

```
src/Features/Content/
  Storylet.cs                   -- base class + default IsEligible/WeightFor/ToCandidate
  Choice.cs                     -- option model
  EffectDecl.cs                 -- { apply: id, params: {...} }
  ChainDecl.cs                  -- { id, days? } | { id, when: "activity.phase" }
  ScopeDecl.cs                  -- { context, slots }
  SlotDecl.cs                   -- { role, tag? }
  WeightDecl.cs                 -- { base, modifiers[] }
  StoryletContext.cs            -- per-evaluation bag (hero, world state, resolved slots)
  StoryletCatalog.cs            -- loads ModuleData/Enlisted/Storylets/*.json
  TriggerRegistry.cs            -- named C# predicate dispatch
  EffectExecutor.cs             -- primitive effect dispatch (quality_add, set_flag, ...)
  ScriptedEffectRegistry.cs     -- loads ModuleData/Enlisted/Effects/scripted_effects.json
  SlotFiller.cs                 -- resolves {slot} to Hero; flattens dotted syntax for TextObject

src/Features/Qualities/
  QualityScope.cs               -- enum: Global | PerHero
  QualityKind.cs                -- enum: Stored | ReadThrough
  QualityValue.cs               -- { Current, LastChanged, LastDecayed }
  QualityDefinition.cs          -- { Id, Min, Max, Default, Scope, Kind, Decay, Reader?, Writer? }
  QualityCatalog.cs             -- loads ModuleData/Enlisted/Qualities/quality_defs.json
  QualityStore.cs               -- the single home; Get/Set/Add, PerHero variants
  QualityBehavior.cs            -- CampaignBehaviorBase; decay tick + SyncData

src/Features/Flags/
  FlagStore.cs                  -- global + hero-scoped flags with expiry
  FlagBehavior.cs               -- CampaignBehaviorBase; expiry sweep + SyncData

src/Features/Activities/
  PhaseDelivery.cs              -- enum: Auto | PlayerChoice
  ActivityEndReason.cs          -- enum
  Phase.cs                      -- { Id, DurationHours, Pool, IntentBias, Delivery, PlayerChoiceCount }
  ActivityContext.cs            -- passed into Activity hooks
  ActivityTypeDefinition.cs     -- JSON-loaded template
  Activity.cs                   -- abstract base (state + OnStart/Tick/OnPhaseEnter/OnPhaseExit/Finish)
  ActivityRuntime.cs            -- singleton; hosts active activities, ticks, parks chains
```

### Modified files (4)

```
Enlisted.csproj                                         -- <Compile Include> for every new .cs
src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs          -- new classes, enums, containers
src/Mod.Entry/SubModule.cs                              -- register QualityBehavior, FlagBehavior, ActivityRuntime
src/Debugging/Behaviors/DebugToolsBehavior.cs           -- smoke-test helpers for each subsystem
Tools/Validation/validate_content.py                    -- new Phase 12 (scripted-effect refs, quality IDs)
```

### New JSON files (2)

```
ModuleData/Enlisted/Qualities/quality_defs.json         -- seed definitions (§7.2)
ModuleData/Enlisted/Effects/scripted_effects.json       -- seed career-progression effects (§11.3)
```

### Save-definer offset claims

| Type | Offset | Reason |
| :--- | :--- | :--- |
| `QualityStore` (class) | 40 | Spec §14 |
| `QualityValue` (class) | 41 | Spec §14 |
| `QualityDefinition` (class) | 42 | Loaded from JSON but registered for completeness |
| `FlagStore` (class) | 43 | Spec §14 |
| `Activity` (abstract class) | 44 | Spec §14 — concrete subclasses claim 45-60 in surface specs |
| `QualityScope` (enum) | 82 | Spec §14 |
| `ActivityEndReason` (enum) | 83 | Spec §14 |

Containers added to `DefineContainerDefinitions`:
- `Dictionary<string, QualityValue>` — global quality storage
- `Dictionary<MBGUID, Dictionary<string, QualityValue>>` — hero-scoped quality storage
- `Dictionary<string, CampaignTime>` — FlagStore._global + QualityStore decay timestamps (reuse)
- `Dictionary<MBGUID, Dictionary<string, CampaignTime>>` — FlagStore._hero
- `List<Activity>` — active activities

---

## Stage 0 — Directory scaffolding

### Task 0.1: Create directories and empty placeholder files

**Files:**
- Create directories: `src/Features/Qualities/`, `src/Features/Flags/`, `src/Features/Activities/`
- Create empty placeholder: `ModuleData/Enlisted/Qualities/`, `ModuleData/Enlisted/Effects/`, `ModuleData/Enlisted/Storylets/`

- [x] **Step 1: Create the three new source directories**

```bash
mkdir -p src/Features/Qualities src/Features/Flags src/Features/Activities
```

- [x] **Step 2: Create the three new module-data directories**

```bash
mkdir -p ModuleData/Enlisted/Qualities ModuleData/Enlisted/Effects ModuleData/Enlisted/Storylets
```

- [x] **Step 3: Verify directories exist**

```bash
ls -d src/Features/Qualities src/Features/Flags src/Features/Activities ModuleData/Enlisted/Qualities ModuleData/Enlisted/Effects ModuleData/Enlisted/Storylets
```
Expected: all six paths listed.

- [x] **Step 4: Commit the directory scaffolding (no-op — directories are only tracked when they contain files; proceed to Stage 1 which will populate them)**

No commit yet; directories are implicit. Stage 1 writes the first files into them.

---

## Stage 1 — Storylet POCO models (Content folder)

### Task 1.1: Write Storylet content models

**Files:**
- Create: `src/Features/Content/Storylet.cs`
- Create: `src/Features/Content/Choice.cs`
- Create: `src/Features/Content/EffectDecl.cs`
- Create: `src/Features/Content/ChainDecl.cs`
- Create: `src/Features/Content/ScopeDecl.cs`
- Create: `src/Features/Content/SlotDecl.cs`
- Create: `src/Features/Content/WeightDecl.cs`
- Create: `src/Features/Content/StoryletContext.cs`

- [x] **Step 1: Write `src/Features/Content/ScopeDecl.cs`**

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>Authored scope declaration: context filter + slot table.</summary>
    public sealed class ScopeDecl
    {
        public string Context { get; set; } = "any";   // "land" | "sea" | "siege" | "settlement" | "any"
        public Dictionary<string, SlotDecl> Slots { get; set; } = new Dictionary<string, SlotDecl>();
    }
}
```

- [x] **Step 2: Write `src/Features/Content/SlotDecl.cs`**

```csharp
namespace Enlisted.Features.Content
{
    /// <summary>Named slot role binding. Tag is optional; when set the filler requires that flag on the candidate hero.</summary>
    public sealed class SlotDecl
    {
        public string Role { get; set; } = string.Empty;   // enlisted_lord | squadmate | named_veteran | chaplain | quartermaster | enemy_commander | settlement_notable | kingdom
        public string Tag { get; set; } = string.Empty;    // optional flag name
    }
}
```

- [x] **Step 3: Write `src/Features/Content/WeightDecl.cs`**

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>Authored weight: base value plus conditional multiplicative modifiers.</summary>
    public sealed class WeightDecl
    {
        public float Base { get; set; } = 1.0f;
        public List<WeightModifier> Modifiers { get; set; } = new List<WeightModifier>();
    }

    public sealed class WeightModifier
    {
        public string If { get; set; } = string.Empty;     // named trigger predicate id
        public float Factor { get; set; } = 1.0f;
    }
}
```

- [x] **Step 4: Write `src/Features/Content/EffectDecl.cs`**

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>An effect to apply. `Apply` is the primitive id (quality_add, set_flag, ...) or scripted effect id.</summary>
    public sealed class EffectDecl
    {
        public string Apply { get; set; } = string.Empty;
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();
    }
}
```

- [x] **Step 5: Write `src/Features/Content/ChainDecl.cs`**

```csharp
namespace Enlisted.Features.Content
{
    /// <summary>
    /// Chain continuation. Either time-delay form (Days set, When empty) or phase-target form
    /// (When set, Days ignored). Empty When + Days==0 fires synchronously on option-select.
    /// </summary>
    public sealed class ChainDecl
    {
        public string Id { get; set; } = string.Empty;
        public int Days { get; set; }
        public string When { get; set; } = string.Empty;   // e.g. "next_muster.promotion_phase"
    }
}
```

- [x] **Step 6: Write `src/Features/Content/Choice.cs`**

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>One option inside a Storylet.</summary>
    public sealed class Choice
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;          // "{=id}Fallback"
        public string Tooltip { get; set; } = string.Empty;       // "{=id}Fallback"
        public List<string> Trigger { get; set; } = new List<string>();       // option-level gate
        public List<EffectDecl> Effects { get; set; } = new List<EffectDecl>();
        public List<EffectDecl> EffectsSuccess { get; set; } = new List<EffectDecl>();
        public List<EffectDecl> EffectsFailure { get; set; } = new List<EffectDecl>();
        public ChainDecl Chain { get; set; }                       // optional
    }
}
```

- [x] **Step 7: Write `src/Features/Content/StoryletContext.cs`**

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Per-evaluation bag handed into IsEligible / WeightFor / ToCandidate. Populated by
    /// ActivityRuntime or beat handlers before invoking the storylet.
    /// </summary>
    public sealed class StoryletContext
    {
        public string CurrentContext { get; set; } = "any";       // runtime-derived: land | sea | siege | settlement
        public Hero SubjectHero { get; set; }
        public Dictionary<string, Hero> ResolvedSlots { get; set; } = new Dictionary<string, Hero>();
        public string ActivityTypeId { get; set; } = string.Empty;
        public string PhaseId { get; set; } = string.Empty;
        public CampaignTime EvaluatedAt { get; set; } = CampaignTime.Now;
    }
}
```

- [x] **Step 8: Write `src/Features/Content/Storylet.cs`**

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Authored narrative unit. 95% of storylets use the default implementations;
    /// complex cases (chain coordinators, bespoke slot-fillers) subclass.
    /// </summary>
    public class Storylet
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Setup { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "normal";
        public bool Observational { get; set; }                 // news-style storylets have this true + empty Options
        public ScopeDecl Scope { get; set; } = new ScopeDecl();
        public List<string> Trigger { get; set; } = new List<string>();
        public WeightDecl Weight { get; set; } = new WeightDecl();
        public List<EffectDecl> Immediate { get; set; } = new List<EffectDecl>();
        public List<Choice> Options { get; set; } = new List<Choice>();
        public int CooldownDays { get; set; }
        public bool OneTime { get; set; }

        // Default impls are filled in during Stage 9 (Task 9.1) after the registry wiring exists.
        public virtual bool IsEligible(StoryletContext ctx) => true;
        public virtual float WeightFor(StoryletContext ctx) => Weight?.Base ?? 1.0f;
        public virtual StoryCandidate ToCandidate(StoryletContext ctx) => null;
    }
}
```

- [x] **Step 9: Add entries to `Enlisted.csproj`**

Open `Enlisted.csproj` and locate the `<ItemGroup>` that already contains `<Compile Include="src\Features\Content\...">` entries. Add these inside it (preserve any existing sort order):

```xml
<Compile Include="src\Features\Content\Storylet.cs" />
<Compile Include="src\Features\Content\Choice.cs" />
<Compile Include="src\Features\Content\EffectDecl.cs" />
<Compile Include="src\Features\Content\ChainDecl.cs" />
<Compile Include="src\Features\Content\ScopeDecl.cs" />
<Compile Include="src\Features\Content\SlotDecl.cs" />
<Compile Include="src\Features\Content\WeightDecl.cs" />
<Compile Include="src\Features\Content\StoryletContext.cs" />
```

- [x] **Step 10: Normalize CRLF on the eight new files**

```bash
for f in src/Features/Content/Storylet.cs src/Features/Content/Choice.cs src/Features/Content/EffectDecl.cs src/Features/Content/ChainDecl.cs src/Features/Content/ScopeDecl.cs src/Features/Content/SlotDecl.cs src/Features/Content/WeightDecl.cs src/Features/Content/StoryletContext.cs; do
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path "$f"
done
```

- [x] **Step 11: Build to verify the POCOs compile**

```bash
export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: `Build succeeded.` with 0 Errors (warnings from other files are acceptable but should not increase).

- [x] **Step 12: Commit**

Write the message to a temp file (bash here has no heredoc):

```bash
cat > /tmp/storylet-models-commit.txt << 'EOF'
feat(content): storylet content POCO models

Spec 0 Stage 1 — Storylet, Choice, EffectDecl, ChainDecl, ScopeDecl,
SlotDecl, WeightDecl, StoryletContext. POCOs only; IsEligible /
WeightFor / ToCandidate default impls are stubs that Stage 9 will
fill in after the trigger + effect + slot registries exist.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
```

```bash
git add src/Features/Content/Storylet.cs src/Features/Content/Choice.cs src/Features/Content/EffectDecl.cs src/Features/Content/ChainDecl.cs src/Features/Content/ScopeDecl.cs src/Features/Content/SlotDecl.cs src/Features/Content/WeightDecl.cs src/Features/Content/StoryletContext.cs Enlisted.csproj
git commit -F /tmp/storylet-models-commit.txt
rm /tmp/storylet-models-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT A — Storylet models

Confirm: build green, 8 POCO files present, csproj entries added, no new warnings. Proceed to Stage 2.

---

## Stage 2 — Quality POCO models

### Task 2.1: Write Quality models + enums

**Files:**
- Create: `src/Features/Qualities/QualityScope.cs`
- Create: `src/Features/Qualities/QualityKind.cs`
- Create: `src/Features/Qualities/QualityValue.cs`
- Create: `src/Features/Qualities/QualityDefinition.cs`

- [x] **Step 1: Write `src/Features/Qualities/QualityScope.cs`**

```csharp
namespace Enlisted.Features.Qualities
{
    /// <summary>Whether a quality lives in the global store or per-hero map.</summary>
    public enum QualityScope
    {
        Global,
        PerHero
    }
}
```

- [x] **Step 2: Write `src/Features/Qualities/QualityKind.cs`**

```csharp
namespace Enlisted.Features.Qualities
{
    /// <summary>
    /// Stored = QualityStore owns the int; persists in save.
    /// ReadThrough = the definition carries Reader/Writer delegates that proxy to native state
    /// (e.g. EnlistmentBehavior.EnlistmentXP, Hero.GetRelationWithPlayer). Not persisted.
    /// </summary>
    public enum QualityKind
    {
        Stored,
        ReadThrough
    }
}
```

- [x] **Step 3: Write `src/Features/Qualities/QualityValue.cs`**

```csharp
using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Qualities
{
    /// <summary>Per-(quality,owner) value + decay bookkeeping. Saveable.</summary>
    [Serializable]
    public sealed class QualityValue
    {
        public int Current { get; set; }
        public CampaignTime LastChanged { get; set; } = CampaignTime.Zero;
        public CampaignTime LastDecayed { get; set; } = CampaignTime.Zero;
    }
}
```

- [x] **Step 4: Write `src/Features/Qualities/QualityDefinition.cs`**

```csharp
using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Qualities
{
    /// <summary>Authoring record — loaded from quality_defs.json at startup, never saved.</summary>
    public sealed class QualityDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;   // localization key
        public int Min { get; set; }
        public int Max { get; set; }
        public int Default { get; set; }
        public QualityScope Scope { get; set; } = QualityScope.Global;
        public QualityKind Kind { get; set; } = QualityKind.Stored;
        public DecayRule Decay { get; set; }                       // null = no decay
        public bool Writable { get; set; } = true;                 // read-only read-throughs (rank, days_in_rank) set false

        // Populated by QualityStore.RegisterAll at boot for ReadThrough entries; not serialized.
        public Func<Hero, int> Reader { get; set; }
        public Action<Hero, int, string> Writer { get; set; }      // (subject, delta, reason) — delta, not absolute
    }

    /// <summary>Decay specification. RequireIdle = only decay while LastChanged is older than IntervalDays.</summary>
    public sealed class DecayRule
    {
        public int Amount { get; set; } = -1;       // typically negative
        public int IntervalDays { get; set; } = 5;
        public bool RequireIdle { get; set; } = true;
        public int Floor { get; set; }              // clamps against this after decay; typically Min
    }
}
```

- [x] **Step 5: Add entries to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Qualities\QualityScope.cs" />
<Compile Include="src\Features\Qualities\QualityKind.cs" />
<Compile Include="src\Features\Qualities\QualityValue.cs" />
<Compile Include="src\Features\Qualities\QualityDefinition.cs" />
```

- [x] **Step 6: Normalize CRLF**

```bash
for f in src/Features/Qualities/QualityScope.cs src/Features/Qualities/QualityKind.cs src/Features/Qualities/QualityValue.cs src/Features/Qualities/QualityDefinition.cs; do
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path "$f"
done
```

- [x] **Step 7: Build**

```bash
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: `Build succeeded.` 0 errors.

- [x] **Step 8: Commit**

```bash
cat > /tmp/quality-models-commit.txt << 'EOF'
feat(qualities): quality POCO models + kind/scope enums

Spec 0 Stage 2 — QualityScope (Global|PerHero), QualityKind
(Stored|ReadThrough), QualityValue (Current + decay bookkeeping),
QualityDefinition (authoring record with Reader/Writer delegate
slots for read-through qualities).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Qualities/QualityScope.cs src/Features/Qualities/QualityKind.cs src/Features/Qualities/QualityValue.cs src/Features/Qualities/QualityDefinition.cs Enlisted.csproj
git commit -F /tmp/quality-models-commit.txt
rm /tmp/quality-models-commit.txt
```

---

## Stage 3 — Activity POCO models

### Task 3.1: Write Activity models + enums + Phase + context

**Files:**
- Create: `src/Features/Activities/ActivityEndReason.cs`
- Create: `src/Features/Activities/PhaseDelivery.cs`
- Create: `src/Features/Activities/Phase.cs`
- Create: `src/Features/Activities/ActivityContext.cs`
- Create: `src/Features/Activities/ActivityTypeDefinition.cs`

- [x] **Step 1: Write `src/Features/Activities/ActivityEndReason.cs`**

```csharp
namespace Enlisted.Features.Activities
{
    /// <summary>Why an Activity.Finish was called.</summary>
    public enum ActivityEndReason
    {
        Completed,
        PlayerCancelled,
        Interrupted,
        InvalidStateAfterLoad
    }
}
```

- [x] **Step 2: Write `src/Features/Activities/PhaseDelivery.cs`**

```csharp
namespace Enlisted.Features.Activities
{
    /// <summary>
    /// Auto: ActivityRuntime picks one weighted storylet from the phase pool per tick.
    /// PlayerChoice: the phase renders N eligible storylets as menu options; player picks one.
    /// </summary>
    public enum PhaseDelivery
    {
        Auto,
        PlayerChoice
    }
}
```

- [x] **Step 3: Write `src/Features/Activities/Phase.cs`**

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Activities
{
    /// <summary>
    /// One phase inside an Activity. Phases come from ActivityTypeDefinition at startup;
    /// an Activity instance carries only a CurrentPhaseIndex in save, phases are re-resolved
    /// from the type def on load.
    /// </summary>
    public sealed class Phase
    {
        public string Id { get; set; } = string.Empty;
        public int DurationHours { get; set; }
        public List<string> Pool { get; set; } = new List<string>();     // storylet ids
        public Dictionary<string, float> IntentBias { get; set; } = new Dictionary<string, float>();  // intent -> weight multiplier
        public PhaseDelivery Delivery { get; set; } = PhaseDelivery.Auto;
        public int PlayerChoiceCount { get; set; } = 4;                  // how many options to offer when PlayerChoice
    }
}
```

- [x] **Step 4: Write `src/Features/Activities/ActivityContext.cs`**

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Activities
{
    /// <summary>Passed into Activity hooks (OnStart/Tick/OnPhaseEnter/OnPhaseExit).</summary>
    public sealed class ActivityContext
    {
        public string TypeId { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public List<Hero> Attendees { get; set; } = new List<Hero>();
        public CampaignTime EnteredAt { get; set; } = CampaignTime.Now;
    }
}
```

- [x] **Step 5: Write `src/Features/Activities/ActivityTypeDefinition.cs`**

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Activities
{
    /// <summary>Loaded from ModuleData/Enlisted/Activities/*.json (surface specs author these). Spec 0 defines the shape only.</summary>
    public sealed class ActivityTypeDefinition
    {
        public string Id { get; set; } = string.Empty;                  // "activity_camp", "activity_muster", ...
        public string DisplayName { get; set; } = string.Empty;         // loc key
        public List<Phase> Phases { get; set; } = new List<Phase>();
        public List<string> ValidIntents { get; set; } = new List<string>();
    }
}
```

- [x] **Step 6: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\ActivityEndReason.cs" />
<Compile Include="src\Features\Activities\PhaseDelivery.cs" />
<Compile Include="src\Features\Activities\Phase.cs" />
<Compile Include="src\Features\Activities\ActivityContext.cs" />
<Compile Include="src\Features\Activities\ActivityTypeDefinition.cs" />
```

- [x] **Step 7: Normalize CRLF**

```bash
for f in src/Features/Activities/ActivityEndReason.cs src/Features/Activities/PhaseDelivery.cs src/Features/Activities/Phase.cs src/Features/Activities/ActivityContext.cs src/Features/Activities/ActivityTypeDefinition.cs; do
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path "$f"
done
```

- [x] **Step 8: Build**

```bash
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 9: Commit**

```bash
cat > /tmp/activity-models-commit.txt << 'EOF'
feat(activities): activity POCO models + enums + Phase + context

Spec 0 Stage 3 — ActivityEndReason, PhaseDelivery (Auto|PlayerChoice),
Phase (pool + intent bias + delivery mode), ActivityContext,
ActivityTypeDefinition. Phases are config, not state — Activity
instances re-resolve phases from TypeId on load.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Activities/ActivityEndReason.cs src/Features/Activities/PhaseDelivery.cs src/Features/Activities/Phase.cs src/Features/Activities/ActivityContext.cs src/Features/Activities/ActivityTypeDefinition.cs Enlisted.csproj
git commit -F /tmp/activity-models-commit.txt
rm /tmp/activity-models-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT B — All POCOs landed

Confirm: 3 stages green, 17 POCO files committed, build clean, csproj consistent. Proceed to Stage 4 (first runtime: Flags).

---

## Stage 4 — Flag subsystem

### Task 4.1: FlagStore

**Files:**
- Create: `src/Features/Flags/FlagStore.cs`

- [x] **Step 1: Write `src/Features/Flags/FlagStore.cs`**

```csharp
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Flags
{
    /// <summary>
    /// Replaces EscalationState.ActiveFlags, informal identity tags, and arc progress markers.
    /// Global + hero-scoped named booleans with optional expiry (CampaignTime.Never = permanent).
    /// Hero-scoped flags purge when the hero is no longer in the game on load.
    /// </summary>
    [Serializable]
    public sealed class FlagStore
    {
        public static FlagStore Instance { get; private set; }

        public Dictionary<string, CampaignTime> GlobalFlags { get; set; } =
            new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<MBGUID, Dictionary<string, CampaignTime>> HeroFlags { get; set; } =
            new Dictionary<MBGUID, Dictionary<string, CampaignTime>>();

        internal static void SetInstance(FlagStore instance) => Instance = instance;

        public bool Has(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return false;
            if (!GlobalFlags.TryGetValue(flag, out var expiry)) return false;
            return expiry == CampaignTime.Never || expiry.IsFuture;
        }

        public bool HasForHero(Hero hero, string flag)
        {
            if (hero == null || string.IsNullOrEmpty(flag)) return false;
            if (!HeroFlags.TryGetValue(hero.Id, out var map)) return false;
            if (!map.TryGetValue(flag, out var expiry)) return false;
            return expiry == CampaignTime.Never || expiry.IsFuture;
        }

        public void Set(string flag, CampaignTime expiry)
        {
            if (string.IsNullOrEmpty(flag)) return;
            GlobalFlags[flag] = expiry;
        }

        public void SetForHero(Hero hero, string flag, CampaignTime expiry)
        {
            if (hero == null || string.IsNullOrEmpty(flag)) return;
            if (!HeroFlags.TryGetValue(hero.Id, out var map))
            {
                map = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
                HeroFlags[hero.Id] = map;
            }
            map[flag] = expiry;
        }

        public void Clear(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            GlobalFlags.Remove(flag);
        }

        public void ClearForHero(Hero hero, string flag)
        {
            if (hero == null || string.IsNullOrEmpty(flag)) return;
            if (HeroFlags.TryGetValue(hero.Id, out var map)) map.Remove(flag);
        }

        public int DaysSinceSet(string flag)
        {
            if (!GlobalFlags.TryGetValue(flag, out _)) return int.MaxValue;
            // Expiry isn't the same as set-time. Callers that need days-since must set a
            // companion flag with a known expiry when the event happens; DaysSinceSet is a
            // convenience only for flags whose expiry == CampaignTime.Never (permanent; returns 0).
            return 0;
        }

        /// <summary>Sweep expired entries. Called by FlagBehavior on hourly tick.</summary>
        public void PurgeExpired(CampaignTime now)
        {
            PurgeExpiredIn(GlobalFlags, now);
            foreach (var kv in HeroFlags) PurgeExpiredIn(kv.Value, now);
        }

        private static void PurgeExpiredIn(Dictionary<string, CampaignTime> map, CampaignTime now)
        {
            List<string> toRemove = null;
            foreach (var kv in map)
            {
                if (kv.Value != CampaignTime.Never && !kv.Value.IsFuture)
                {
                    (toRemove ??= new List<string>()).Add(kv.Key);
                }
            }
            if (toRemove == null) return;
            foreach (var k in toRemove) map.Remove(k);
        }

        /// <summary>On load, drop hero-scoped flags whose hero is no longer known to the game.</summary>
        public void PurgeMissingHeroes()
        {
            List<MBGUID> toRemove = null;
            foreach (var id in HeroFlags.Keys)
            {
                if (MBObjectManager.Instance.GetObject(id) == null)
                {
                    (toRemove ??= new List<MBGUID>()).Add(id);
                }
            }
            if (toRemove == null) return;
            foreach (var id in toRemove) HeroFlags.Remove(id);
        }
    }
}
```

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Flags\FlagStore.cs" />
```

- [x] **Step 3: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Flags/FlagStore.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 4: Commit**

```bash
cat > /tmp/flagstore-commit.txt << 'EOF'
feat(flags): FlagStore with global + hero-scoped flags and expiry sweep

Spec 0 Stage 4.1 — FlagStore singleton with Has/Set/Clear + hero-scope
variants + PurgeExpired (called by FlagBehavior next task) +
PurgeMissingHeroes (called on load). MBGUID-keyed hero map survives
load; missing-hero purge drops flags whose owner is gone.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Flags/FlagStore.cs Enlisted.csproj
git commit -F /tmp/flagstore-commit.txt
rm /tmp/flagstore-commit.txt
```

### Task 4.2: FlagBehavior

**Files:**
- Create: `src/Features/Flags/FlagBehavior.cs`

- [x] **Step 1: Write `src/Features/Flags/FlagBehavior.cs`**

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Flags
{
    /// <summary>CampaignBehavior that owns FlagStore lifecycle + SyncData + hourly expiry sweep.</summary>
    public sealed class FlagBehavior : CampaignBehaviorBase
    {
        private FlagStore _store = new FlagStore();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_flagStore", ref _store);
                _store ??= new FlagStore();
                FlagStore.SetInstance(_store);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FLAGS", "FlagBehavior SyncData failed", ex);
                _store = new FlagStore();
                FlagStore.SetInstance(_store);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            FlagStore.SetInstance(_store);
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _store.PurgeMissingHeroes();
            FlagStore.SetInstance(_store);
        }

        private void OnHourlyTick()
        {
            try
            {
                _store.PurgeExpired(CampaignTime.Now);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FLAGS", "FlagStore hourly purge failed", ex);
            }
        }
    }
}
```

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Flags\FlagBehavior.cs" />
```

- [x] **Step 3: Register behavior in `src/Mod.Entry/SubModule.cs`**

Open the file. Locate the `CampaignGameStarter` behavior registration block (search for `AddBehavior`). Add, alongside the existing registrations (order matters — Flags should register BEFORE Qualities so qualities can read flags during init):

```csharp
campaignStarter.AddBehavior(new Enlisted.Features.Flags.FlagBehavior());
```

- [x] **Step 4: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Flags/FlagBehavior.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors. `FlagStore` field registered with save but save-type definitions come in Task 4.3 — on first run it will log a SaveableType warning; that's intentional until Task 4.3 lands.

- [x] **Step 5: Commit**

```bash
cat > /tmp/flagbehavior-commit.txt << 'EOF'
feat(flags): FlagBehavior for SyncData + hourly expiry sweep

Spec 0 Stage 4.2 — CampaignBehaviorBase that owns FlagStore lifecycle.
SyncData persists via ref-assigned field. OnGameLoaded purges flags
owned by heroes no longer in the game. HourlyTick purges expired
entries. Registered in SubModule.cs ahead of QualityBehavior.

Save-type registration lands in Task 4.3; first boot will log one
SaveableType warning until then (expected, not an error).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Flags/FlagBehavior.cs src/Mod.Entry/SubModule.cs Enlisted.csproj
git commit -F /tmp/flagbehavior-commit.txt
rm /tmp/flagbehavior-commit.txt
```

### Task 4.3: Flag save-type registration

**Files:**
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`

- [x] **Step 1: Read current file to confirm offset reservations**

```bash
grep -n "AddClassDefinition\|AddEnumDefinition\|ConstructContainerDefinition" src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
```
Expected output includes `AddClassDefinition(typeof(Enlisted.Features.Content.StoryCandidatePersistent), 30);` and `AddEnumDefinition(typeof(Enlisted.Features.Content.StoryBeat), 81);` — confirms 30 and 81 are the current ceiling for the existing block.

- [x] **Step 2: Add `FlagStore` class registration**

In `DefineClassTypes()`, after the pacing subsystem line (offset 30), add a new "Storylet backbone" section:

```csharp
// Storylet backbone (Spec 0)
AddClassDefinition(typeof(Enlisted.Features.Flags.FlagStore), 43);
```

- [x] **Step 3: Add container definitions**

In `DefineContainerDefinitions()`, after the pacing subsystem containers, add:

```csharp
// Storylet backbone containers (Spec 0)
ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, TaleWorlds.CampaignSystem.CampaignTime>));
ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<TaleWorlds.ObjectSystem.MBGUID, System.Collections.Generic.Dictionary<string, TaleWorlds.CampaignSystem.CampaignTime>>));
```

- [x] **Step 4: Build**

```bash
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 5: Commit**

```bash
cat > /tmp/flag-savereg-commit.txt << 'EOF'
feat(saves): register FlagStore + dictionary containers

Spec 0 Stage 4.3 — FlagStore class at offset 43,
Dictionary<string, CampaignTime> and
Dictionary<MBGUID, Dictionary<string, CampaignTime>> containers for
global + hero-scoped flag storage.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
git commit -F /tmp/flag-savereg-commit.txt
rm /tmp/flag-savereg-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT C — Flag subsystem live

Smoke-test: launch the mod in-game, confirm no SaveableType warnings in session log, save, reload, confirm FlagStore survives the round-trip. Proceed to Stage 5 (Qualities).

---

## Stage 5 — Quality subsystem

### Task 5.1: QualityStore

**Files:**
- Create: `src/Features/Qualities/QualityStore.cs`

- [x] **Step 1: Write `src/Features/Qualities/QualityStore.cs`**

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Qualities
{
    /// <summary>
    /// Single home for typed numeric state. Stored qualities persist in GlobalValues /
    /// HeroValues. ReadThrough qualities proxy through Reader/Writer delegates on their
    /// QualityDefinition and don't persist here. Definitions are supplied by
    /// QualityCatalog at boot (JSON + C# read-through registration in QualityBehavior).
    /// </summary>
    [Serializable]
    public sealed class QualityStore
    {
        public static QualityStore Instance { get; private set; }
        internal static void SetInstance(QualityStore instance) => Instance = instance;

        public Dictionary<string, QualityValue> GlobalValues { get; set; } =
            new Dictionary<string, QualityValue>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<MBGUID, Dictionary<string, QualityValue>> HeroValues { get; set; } =
            new Dictionary<MBGUID, Dictionary<string, QualityValue>>();

        [NonSerialized] private Dictionary<string, QualityDefinition> _defsById =
            new Dictionary<string, QualityDefinition>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<QualityDefinition> AllDefs => _defsById.Values;

        public void RegisterDefinitions(IEnumerable<QualityDefinition> defs)
        {
            _defsById = new Dictionary<string, QualityDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in defs)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                _defsById[def.Id] = def;
            }
        }

        public QualityDefinition GetDefinition(string id)
        {
            _defsById.TryGetValue(id ?? string.Empty, out var def);
            return def;
        }

        public int Get(string qualityId)
        {
            var def = GetDefinition(qualityId);
            if (def == null)
            {
                ModLogger.Expected("QUALITY", "missing_def_" + qualityId, "Quality definition not registered: " + qualityId);
                return 0;
            }
            if (def.Kind == QualityKind.ReadThrough)
            {
                return def.Reader == null ? def.Default : Clamp(def.Reader(null), def.Min, def.Max);
            }
            return GlobalValues.TryGetValue(qualityId, out var v) ? v.Current : def.Default;
        }

        public int GetForHero(Hero hero, string qualityId)
        {
            if (hero == null) return Get(qualityId);
            var def = GetDefinition(qualityId);
            if (def == null)
            {
                ModLogger.Expected("QUALITY", "missing_def_" + qualityId, "Quality definition not registered: " + qualityId);
                return 0;
            }
            if (def.Kind == QualityKind.ReadThrough)
            {
                return def.Reader == null ? def.Default : Clamp(def.Reader(hero), def.Min, def.Max);
            }
            if (HeroValues.TryGetValue(hero.Id, out var map) && map.TryGetValue(qualityId, out var v))
                return v.Current;
            return def.Default;
        }

        public void Add(string qualityId, int delta, string reason)
        {
            var def = GetDefinition(qualityId);
            if (def == null || !def.Writable)
            {
                ModLogger.Expected("QUALITY", "write_blocked_" + qualityId, "Write blocked (unknown or read-only): " + qualityId);
                return;
            }
            if (def.Kind == QualityKind.ReadThrough)
            {
                def.Writer?.Invoke(null, delta, reason);
                return;
            }
            if (!GlobalValues.TryGetValue(qualityId, out var v))
            {
                v = new QualityValue { Current = def.Default };
                GlobalValues[qualityId] = v;
            }
            v.Current = Clamp(v.Current + delta, def.Min, def.Max);
            v.LastChanged = CampaignTime.Now;
        }

        public void AddForHero(Hero hero, string qualityId, int delta, string reason)
        {
            if (hero == null) { Add(qualityId, delta, reason); return; }
            var def = GetDefinition(qualityId);
            if (def == null || !def.Writable)
            {
                ModLogger.Expected("QUALITY", "write_blocked_hero_" + qualityId, "Write blocked (unknown or read-only): " + qualityId);
                return;
            }
            if (def.Kind == QualityKind.ReadThrough)
            {
                def.Writer?.Invoke(hero, delta, reason);
                return;
            }
            if (!HeroValues.TryGetValue(hero.Id, out var map))
            {
                map = new Dictionary<string, QualityValue>(StringComparer.OrdinalIgnoreCase);
                HeroValues[hero.Id] = map;
            }
            if (!map.TryGetValue(qualityId, out var v))
            {
                v = new QualityValue { Current = def.Default };
                map[qualityId] = v;
            }
            v.Current = Clamp(v.Current + delta, def.Min, def.Max);
            v.LastChanged = CampaignTime.Now;
        }

        public void Set(string qualityId, int value, string reason)
        {
            Add(qualityId, value - Get(qualityId), reason);
        }

        /// <summary>Called by QualityBehavior on daily tick. Applies decay rules to stored qualities.</summary>
        public void ApplyDecayTick(CampaignTime now)
        {
            foreach (var def in _defsById.Values)
            {
                if (def.Kind != QualityKind.Stored || def.Decay == null) continue;
                ApplyDecayToMap(def, GlobalValues, now);
                foreach (var heroMap in HeroValues.Values) ApplyDecayToMap(def, heroMap, now);
            }
        }

        private static void ApplyDecayToMap(QualityDefinition def, Dictionary<string, QualityValue> map, CampaignTime now)
        {
            if (!map.TryGetValue(def.Id, out var v)) return;
            var daysSinceDecayed = (now - v.LastDecayed).ToDays;
            if (daysSinceDecayed < def.Decay.IntervalDays) return;
            if (def.Decay.RequireIdle)
            {
                var daysSinceChanged = (now - v.LastChanged).ToDays;
                if (daysSinceChanged < def.Decay.IntervalDays) return;
            }
            v.Current = Clamp(v.Current + def.Decay.Amount, Math.Max(def.Decay.Floor, def.Min), def.Max);
            v.LastDecayed = now;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
    }
}
```

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Qualities\QualityStore.cs" />
```

- [x] **Step 3: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Qualities/QualityStore.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 4: Commit**

```bash
cat > /tmp/qualitystore-commit.txt << 'EOF'
feat(qualities): QualityStore Get/Add/Set with Stored + ReadThrough paths

Spec 0 Stage 5.1 — single home for typed numeric state. Stored goes
through GlobalValues / HeroValues dictionaries, ReadThrough goes
through def.Reader / def.Writer delegates. Writes to unknown or
read-only qualities no-op with an Expected-level log (not an error).
ApplyDecayTick honors RequireIdle + IntervalDays per QualityDefinition.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Qualities/QualityStore.cs Enlisted.csproj
git commit -F /tmp/qualitystore-commit.txt
rm /tmp/qualitystore-commit.txt
```

### Task 5.2: QualityCatalog + seed JSON

**Files:**
- Create: `src/Features/Qualities/QualityCatalog.cs`
- Create: `ModuleData/Enlisted/Qualities/quality_defs.json`

- [x] **Step 1: Write `src/Features/Qualities/QualityCatalog.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Qualities
{
    /// <summary>Loads quality definitions from ModuleData/Enlisted/Qualities/*.json at boot.</summary>
    public static class QualityCatalog
    {
        public static List<QualityDefinition> LoadAll()
        {
            var result = new List<QualityDefinition>();
            var dir = Path.Combine(ModulePaths.GetContentPath("Qualities"));
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("QUALITY", "no_quality_dir", "Quality definitions directory not found: " + dir);
                return result;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);
                    var defs = root["qualities"] as JArray;
                    if (defs == null) continue;
                    foreach (var item in defs)
                    {
                        var def = ParseDefinition(item as JObject);
                        if (def != null) result.Add(def);
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("QUALITY", "Failed to load quality definition file", ex, new { file });
                }
            }
            return result;
        }

        private static QualityDefinition ParseDefinition(JObject obj)
        {
            if (obj == null) return null;
            var def = new QualityDefinition
            {
                Id = (string)obj["id"] ?? string.Empty,
                DisplayName = (string)obj["displayName"] ?? string.Empty,
                Min = (int?)obj["min"] ?? 0,
                Max = (int?)obj["max"] ?? 100,
                Default = (int?)obj["default"] ?? 0,
                Scope = ParseEnum<QualityScope>((string)obj["scope"], QualityScope.Global),
                Kind = ParseEnum<QualityKind>((string)obj["kind"], QualityKind.Stored),
                Writable = (bool?)obj["writable"] ?? true,
            };
            var decayObj = obj["decay"] as JObject;
            if (decayObj != null)
            {
                def.Decay = new DecayRule
                {
                    Amount = (int?)decayObj["amount"] ?? -1,
                    IntervalDays = (int?)decayObj["intervalDays"] ?? 5,
                    RequireIdle = (bool?)decayObj["requireIdle"] ?? true,
                    Floor = (int?)decayObj["floor"] ?? def.Min,
                };
            }
            return string.IsNullOrEmpty(def.Id) ? null : def;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return !string.IsNullOrEmpty(value) && Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
                ? parsed
                : fallback;
        }
    }
}
```

- [x] **Step 2: Write `ModuleData/Enlisted/Qualities/quality_defs.json`**

Seed list from spec §7.2 (post–dead-code purge). Stored qualities only; ReadThrough qualities are registered in C# by QualityBehavior (Task 5.3) so their Reader/Writer delegates bind correctly.

```json
{
  "qualities": [
    { "id": "scrutiny",     "displayName": "{=q_scrutiny}Scrutiny",      "min": 0, "max": 100, "default": 0,  "scope": "Global",  "kind": "Stored", "writable": true, "decay": { "amount": -1, "intervalDays": 5,  "requireIdle": true,  "floor": 0 } },
    { "id": "medical_risk", "displayName": "{=q_med_risk}Medical Risk",  "min": 0, "max": 5,   "default": 0,  "scope": "Global",  "kind": "Stored", "writable": true, "decay": { "amount": -1, "intervalDays": 10, "requireIdle": true,  "floor": 0 } },
    { "id": "supplies",     "displayName": "{=q_supplies}Supplies",      "min": 0, "max": 100, "default": 50, "scope": "Global",  "kind": "Stored", "writable": true, "decay": null },
    { "id": "readiness",    "displayName": "{=q_readiness}Readiness",    "min": 0, "max": 100, "default": 50, "scope": "Global",  "kind": "Stored", "writable": true, "decay": null },
    { "id": "loyalty",      "displayName": "{=q_loyalty}Loyalty",        "min": 0, "max": 100, "default": 50, "scope": "PerHero", "kind": "Stored", "writable": true, "decay": null }
  ]
}
```

- [x] **Step 3: Add C# file to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Qualities\QualityCatalog.cs" />
```

- [x] **Step 4: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Qualities/QualityCatalog.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 5: Commit**

```bash
cat > /tmp/qualitycatalog-commit.txt << 'EOF'
feat(qualities): QualityCatalog + seed quality_defs.json

Spec 0 Stage 5.2 — loader reads ModuleData/Enlisted/Qualities/*.json
at boot. Seed set matches spec §7.2 after dead-code purge: scrutiny,
medical_risk, supplies, readiness, loyalty (per-hero). ReadThrough
qualities (rank_xp, days_in_rank, battles_survived, rank,
days_enlisted, lord_relation) register in C# next task so their
native-proxy Reader/Writer delegates bind.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Qualities/QualityCatalog.cs ModuleData/Enlisted/Qualities/quality_defs.json Enlisted.csproj
git commit -F /tmp/qualitycatalog-commit.txt
rm /tmp/qualitycatalog-commit.txt
```

### Task 5.3: QualityBehavior (lifecycle + read-through registration)

**Files:**
- Create: `src/Features/Qualities/QualityBehavior.cs`
- Modify: `src/Mod.Entry/SubModule.cs`

- [x] **Step 1: Write `src/Features/Qualities/QualityBehavior.cs`**

```csharp
using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Enlisted.Features.Qualities
{
    /// <summary>
    /// CampaignBehavior that owns QualityStore lifecycle. Loads JSON quality definitions,
    /// registers the ReadThrough qualities (native TaleWorlds proxies), persists stored
    /// values via SyncData, runs decay on daily tick.
    /// </summary>
    public sealed class QualityBehavior : CampaignBehaviorBase
    {
        private QualityStore _store = new QualityStore();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_qualityStore", ref _store);
                _store ??= new QualityStore();
                QualityStore.SetInstance(_store);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUALITY", "QualityBehavior SyncData failed", ex);
                _store = new QualityStore();
                QualityStore.SetInstance(_store);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            QualityStore.SetInstance(_store);
            RegisterDefinitions();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            QualityStore.SetInstance(_store);
            RegisterDefinitions();
        }

        private void OnDailyTick()
        {
            try
            {
                _store.ApplyDecayTick(CampaignTime.Now);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUALITY", "Quality decay tick failed", ex);
            }
        }

        /// <summary>Load JSON defs and stitch in the C# ReadThrough proxies.</summary>
        private void RegisterDefinitions()
        {
            var defs = QualityCatalog.LoadAll();

            defs.Add(new QualityDefinition
            {
                Id = "rank_xp", DisplayName = "{=q_rank_xp}Rank XP",
                Min = 0, Max = int.MaxValue, Default = 0,
                Scope = QualityScope.Global, Kind = QualityKind.ReadThrough, Writable = true,
                Reader = _ => EnlistmentBehavior.Instance?.EnlistmentXP ?? 0,
                Writer = (_, delta, reason) => EnlistmentBehavior.Instance?.AddEnlistmentXP(delta, reason ?? "Quality")
            });

            defs.Add(new QualityDefinition
            {
                Id = "days_in_rank", DisplayName = "{=q_days_in_rank}Days In Rank",
                Min = 0, Max = int.MaxValue, Default = 0,
                Scope = QualityScope.Global, Kind = QualityKind.ReadThrough, Writable = false,
                Reader = _ => EnlistmentBehavior.Instance?.DaysInRank ?? 0,
                Writer = null
            });

            defs.Add(new QualityDefinition
            {
                Id = "battles_survived", DisplayName = "{=q_battles}Battles Survived",
                Min = 0, Max = int.MaxValue, Default = 0,
                Scope = QualityScope.Global, Kind = QualityKind.ReadThrough, Writable = true,
                Reader = _ => EnlistmentBehavior.Instance?.BattlesSurvived ?? 0,
                Writer = (_, delta, _2) =>
                {
                    var e = EnlistmentBehavior.Instance;
                    if (e == null) return;
                    for (var i = 0; i < delta; i++) e.IncrementBattlesSurvived();
                }
            });

            defs.Add(new QualityDefinition
            {
                Id = "rank", DisplayName = "{=q_rank}Rank",
                Min = 1, Max = 9, Default = 1,
                Scope = QualityScope.Global, Kind = QualityKind.ReadThrough, Writable = false,
                Reader = _ => EnlistmentBehavior.Instance?.EnlistmentTier ?? 1,
                Writer = null
            });

            defs.Add(new QualityDefinition
            {
                Id = "days_enlisted", DisplayName = "{=q_days_enlisted}Days Enlisted",
                Min = 0, Max = int.MaxValue, Default = 0,
                Scope = QualityScope.Global, Kind = QualityKind.ReadThrough, Writable = false,
                Reader = _ => (int)(EnlistmentBehavior.Instance?.GetDaysEnlisted() ?? 0),
                Writer = null
            });

            defs.Add(new QualityDefinition
            {
                Id = "lord_relation", DisplayName = "{=q_lord_rel}Lord Relation",
                Min = -100, Max = 100, Default = 0,
                Scope = QualityScope.Global, Kind = QualityKind.ReadThrough, Writable = true,
                Reader = _ => EnlistmentBehavior.Instance?.EnlistedLord?.GetRelationWithPlayer() ?? 0,
                Writer = (_, delta, _2) =>
                {
                    var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                    if (lord == null) return;
                    ChangeRelationAction.ApplyPlayerRelation(lord, delta, affectRelatives: false, showQuickNotification: false);
                }
            });

            _store.RegisterDefinitions(defs);
        }
    }
}
```

Note: if `EnlistmentBehavior` lacks a `GetDaysEnlisted()` helper, substitute the equivalent expression using the enlistment start time the behavior already tracks (grep `EnlistmentBehavior.cs` for `EnlistmentStartTime` or similar). Keep the delegate pure-read — do not mutate state from it.

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Qualities\QualityBehavior.cs" />
```

- [x] **Step 3: Register in `src/Mod.Entry/SubModule.cs`**

Place the registration AFTER `FlagBehavior` (qualities may read flags during init, so flags must exist first) and BEFORE `StoryDirector` (the Director will eventually consult qualities):

```csharp
campaignStarter.AddBehavior(new Enlisted.Features.Qualities.QualityBehavior());
```

- [x] **Step 4: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Qualities/QualityBehavior.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors. If `GetDaysEnlisted()` doesn't exist, the build will flag it — substitute the right expression and rebuild.

- [x] **Step 5: Commit**

```bash
cat > /tmp/qualitybehavior-commit.txt << 'EOF'
feat(qualities): QualityBehavior + ReadThrough native proxies

Spec 0 Stage 5.3 — loads JSON stored defs via QualityCatalog, registers
six ReadThrough qualities that proxy into native state:

  rank_xp             -> EnlistmentBehavior.EnlistmentXP (rw)
  days_in_rank        -> EnlistmentBehavior.DaysInRank (ro)
  battles_survived    -> EnlistmentBehavior.BattlesSurvived (rw)
  rank                -> EnlistmentBehavior.EnlistmentTier (ro)
  days_enlisted       -> (derived from enlistment start) (ro)
  lord_relation       -> Hero.GetRelationWithPlayer + ChangeRelationAction (rw)

Stored values persist via SyncData. Daily tick applies decay per
QualityDefinition.Decay. Registered in SubModule.cs after
FlagBehavior, before StoryDirector.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Qualities/QualityBehavior.cs src/Mod.Entry/SubModule.cs Enlisted.csproj
git commit -F /tmp/qualitybehavior-commit.txt
rm /tmp/qualitybehavior-commit.txt
```

### Task 5.4: Quality save-type registration

**Files:**
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`

- [x] **Step 1: Add class + enum registrations**

Add to `DefineClassTypes()` in the storylet-backbone section (after the `FlagStore` line from Task 4.3):

```csharp
AddClassDefinition(typeof(Enlisted.Features.Qualities.QualityStore), 40);
AddClassDefinition(typeof(Enlisted.Features.Qualities.QualityValue), 41);
```

Add to `DefineEnumTypes()` in a new "Storylet backbone enums" block:

```csharp
// Storylet backbone enums (Spec 0)
AddEnumDefinition(typeof(Enlisted.Features.Qualities.QualityScope), 82);
```

Add to `DefineContainerDefinitions()` (after the flag containers):

```csharp
ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, Enlisted.Features.Qualities.QualityValue>));
ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<TaleWorlds.ObjectSystem.MBGUID, System.Collections.Generic.Dictionary<string, Enlisted.Features.Qualities.QualityValue>>));
```

- [x] **Step 2: Build**

```bash
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 3: Commit**

```bash
cat > /tmp/quality-savereg-commit.txt << 'EOF'
feat(saves): register QualityStore + QualityValue + QualityScope

Spec 0 Stage 5.4 — QualityStore class at offset 40, QualityValue at
41, QualityScope enum at 82. Containers for
Dictionary<string, QualityValue> and
Dictionary<MBGUID, Dictionary<string, QualityValue>>.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
git commit -F /tmp/quality-savereg-commit.txt
rm /tmp/quality-savereg-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT D — Quality subsystem live

Smoke-test: launch, confirm no SaveableType warnings, confirm stored quality round-trips through save (set scrutiny via debug, save, reload, re-read — should match). Proceed to Stage 6.

---

## Stage 6 — Effect execution

### Task 6.1: EffectExecutor (primitive dispatch)

**Files:**
- Create: `src/Features/Content/EffectExecutor.cs`

- [x] **Step 1: Write `src/Features/Content/EffectExecutor.cs`**

```csharp
using System.Collections.Generic;
using Enlisted.Features.Flags;
using Enlisted.Features.Qualities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Dispatch for primitive effects. Scripted effects resolve to a list of primitives via
    /// ScriptedEffectRegistry before arriving here. Unknown primitives log once and no-op.
    /// </summary>
    public static class EffectExecutor
    {
        public static void Apply(IList<EffectDecl> effects, StoryletContext ctx)
        {
            if (effects == null || effects.Count == 0) return;
            foreach (var eff in effects) ApplyOne(eff, ctx);
        }

        public static void ApplyOne(EffectDecl eff, StoryletContext ctx)
        {
            if (eff == null || string.IsNullOrEmpty(eff.Apply)) return;

            // Scripted effect? Expand and recurse.
            var scripted = ScriptedEffectRegistry.Resolve(eff.Apply);
            if (scripted != null)
            {
                Apply(scripted, ctx);
                return;
            }

            switch (eff.Apply)
            {
                case "quality_add":       DoQualityAdd(eff, ctx, perHero: false); break;
                case "quality_add_for_hero": DoQualityAdd(eff, ctx, perHero: true); break;
                case "quality_set":       DoQualitySet(eff); break;
                case "set_flag":          DoSetFlag(eff); break;
                case "clear_flag":        DoClearFlag(eff); break;
                case "set_flag_on_hero":  DoSetFlagOnHero(eff, ctx); break;
                case "trait_xp":          DoTraitXp(eff); break;
                case "skill_xp":          DoSkillXp(eff); break;
                case "give_gold":         DoGiveGold(eff); break;
                case "give_item":         DoGiveItem(eff); break;
                default:
                    ModLogger.Expected("EFFECT", "unknown_primitive_" + eff.Apply, "Unknown effect primitive: " + eff.Apply);
                    break;
            }
        }

        private static void DoQualityAdd(EffectDecl eff, StoryletContext ctx, bool perHero)
        {
            var quality = GetStr(eff, "quality");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(quality) || amount == 0) return;

            if (perHero)
            {
                var slot = GetStr(eff, "slot");
                Hero hero = null;
                if (!string.IsNullOrEmpty(slot) && ctx?.ResolvedSlots != null)
                    ctx.ResolvedSlots.TryGetValue(slot, out hero);
                QualityStore.Instance?.AddForHero(hero, quality, amount, "storylet");
            }
            else
            {
                QualityStore.Instance?.Add(quality, amount, "storylet");
            }
        }

        private static void DoQualitySet(EffectDecl eff)
        {
            var quality = GetStr(eff, "quality");
            var value = GetInt(eff, "value");
            if (string.IsNullOrEmpty(quality)) return;
            QualityStore.Instance?.Set(quality, value, "storylet");
        }

        private static void DoSetFlag(EffectDecl eff)
        {
            var name = GetStr(eff, "name");
            var days = GetInt(eff, "days");
            if (string.IsNullOrEmpty(name)) return;
            var expiry = days > 0 ? CampaignTime.DaysFromNow(days) : CampaignTime.Never;
            FlagStore.Instance?.Set(name, expiry);
        }

        private static void DoClearFlag(EffectDecl eff)
        {
            var name = GetStr(eff, "name");
            if (string.IsNullOrEmpty(name)) return;
            FlagStore.Instance?.Clear(name);
        }

        private static void DoSetFlagOnHero(EffectDecl eff, StoryletContext ctx)
        {
            var slot = GetStr(eff, "slot");
            var name = GetStr(eff, "name");
            var days = GetInt(eff, "days");
            if (string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(name)) return;
            if (ctx?.ResolvedSlots == null) return;
            if (!ctx.ResolvedSlots.TryGetValue(slot, out var hero) || hero == null) return;
            var expiry = days > 0 ? CampaignTime.DaysFromNow(days) : CampaignTime.Never;
            FlagStore.Instance?.SetForHero(hero, name, expiry);
        }

        private static void DoTraitXp(EffectDecl eff)
        {
            var trait = GetStr(eff, "trait");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(trait) || amount == 0) return;
            var traitObj = TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.All
                .FirstOrDefaultQ(t => string.Equals(t.StringId, trait, System.StringComparison.OrdinalIgnoreCase));
            if (traitObj == null)
            {
                ModLogger.Expected("EFFECT", "unknown_trait_" + trait, "Unknown trait id: " + trait);
                return;
            }
            Hero.MainHero?.AddTraitLevel(traitObj, 0); // force-init slot to avoid null path
            Hero.MainHero?.SetTraitLevelInternal(traitObj, Hero.MainHero.GetTraitLevel(traitObj) + System.Math.Sign(amount));
        }

        private static void DoSkillXp(EffectDecl eff)
        {
            var skill = GetStr(eff, "skill");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(skill) || amount <= 0) return;
            var skillObj = TaleWorlds.Core.SkillObject.GetAll()
                .FirstOrDefaultQ(s => string.Equals(s.StringId, skill, System.StringComparison.OrdinalIgnoreCase));
            if (skillObj == null)
            {
                ModLogger.Expected("EFFECT", "unknown_skill_" + skill, "Unknown skill id: " + skill);
                return;
            }
            Hero.MainHero?.AddSkillXp(skillObj, amount);
        }

        private static void DoGiveGold(EffectDecl eff)
        {
            var amount = GetInt(eff, "amount");
            if (amount == 0 || Hero.MainHero == null) return;
            // Positive = grant; negative = charge. Route through GiveGoldAction (CLAUDE.md §3).
            if (amount >= 0)
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount);
            else
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, -amount);
        }

        private static void DoGiveItem(EffectDecl eff)
        {
            var itemId = GetStr(eff, "item_id");
            var count = System.Math.Max(1, GetInt(eff, "count"));
            if (string.IsNullOrEmpty(itemId)) return;
            var item = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<TaleWorlds.Core.ItemObject>(itemId);
            if (item == null)
            {
                ModLogger.Expected("EFFECT", "unknown_item_" + itemId, "Unknown item id: " + itemId);
                return;
            }
            PartyBase.MainParty?.ItemRoster?.AddToCounts(item, count);
        }

        private static string GetStr(EffectDecl eff, string key)
        {
            return eff?.Params != null && eff.Params.TryGetValue(key, out var v) && v != null ? v.ToString() : string.Empty;
        }

        private static int GetInt(EffectDecl eff, string key)
        {
            if (eff?.Params == null || !eff.Params.TryGetValue(key, out var v) || v == null) return 0;
            return v switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                float f => (int)f,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => 0
            };
        }
    }
}
```

Note: `DefaultTraits.All`, `Hero.AddTraitLevel`, `Hero.SetTraitLevelInternal`, `SkillObject.GetAll`, and `Hero.AddSkillXp` are all v1.3.13 TaleWorlds APIs — verify each at `C:\Dev\Enlisted\Decompile\` before implementation. If any method signature has drifted (e.g., `SetTraitLevelInternal` is private), fall back to the public `Hero.AddTraitLevel(trait, level)` absolute-set API and compute the target level from the current one.

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Content\EffectExecutor.cs" />
```

- [x] **Step 3: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/EffectExecutor.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors. Any TaleWorlds API that doesn't match v1.3.13 must be fixed before proceeding — the decompile is authoritative.

- [x] **Step 4: Commit**

```bash
cat > /tmp/effect-executor-commit.txt << 'EOF'
feat(content): EffectExecutor primitive dispatch

Spec 0 Stage 6.1 — ten primitives: quality_add, quality_add_for_hero,
quality_set, set_flag, clear_flag, set_flag_on_hero, trait_xp,
skill_xp, give_gold (routed through GiveGoldAction per CLAUDE.md §3),
give_item. Scripted effects expand via ScriptedEffectRegistry before
dispatch; unknown primitives log once and no-op.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Content/EffectExecutor.cs Enlisted.csproj
git commit -F /tmp/effect-executor-commit.txt
rm /tmp/effect-executor-commit.txt
```

### Task 6.2: ScriptedEffectRegistry + seed JSON

**Files:**
- Create: `src/Features/Content/ScriptedEffectRegistry.cs`
- Create: `ModuleData/Enlisted/Effects/scripted_effects.json`

- [x] **Step 1: Write `src/Features/Content/ScriptedEffectRegistry.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Content
{
    /// <summary>Resolves named scripted effect ids to their primitive effect bodies. Loaded at boot.</summary>
    public static class ScriptedEffectRegistry
    {
        private static Dictionary<string, List<EffectDecl>> _registry =
            new Dictionary<string, List<EffectDecl>>(StringComparer.OrdinalIgnoreCase);

        public static void LoadAll()
        {
            _registry.Clear();
            var dir = ModulePaths.GetContentPath("Effects");
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("EFFECT", "no_effects_dir", "Scripted-effects directory not found: " + dir);
                return;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);
                    var effects = root["effects"] as JObject;
                    if (effects == null) continue;
                    foreach (var kv in effects)
                    {
                        var id = kv.Key;
                        var arr = kv.Value as JArray;
                        if (arr == null) continue;
                        var list = new List<EffectDecl>();
                        foreach (var item in arr)
                        {
                            var parsed = ParseEffect(item as JObject);
                            if (parsed != null) list.Add(parsed);
                        }
                        _registry[id] = list;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("EFFECT", "Failed to load scripted-effects file", ex, new { file });
                }
            }
        }

        public static IList<EffectDecl> Resolve(string id) =>
            !string.IsNullOrEmpty(id) && _registry.TryGetValue(id, out var list) ? list : null;

        public static IEnumerable<string> AllIds => _registry.Keys;

        private static EffectDecl ParseEffect(JObject obj)
        {
            if (obj == null) return null;
            var apply = (string)obj["apply"];
            if (string.IsNullOrEmpty(apply)) return null;
            var eff = new EffectDecl { Apply = apply };
            foreach (var prop in obj.Properties())
            {
                if (prop.Name == "apply") continue;
                eff.Params[prop.Name] = prop.Value.Type switch
                {
                    JTokenType.Integer => (object)(long)prop.Value,
                    JTokenType.Float => (object)(double)prop.Value,
                    JTokenType.Boolean => (object)(bool)prop.Value,
                    _ => (object)(string)prop.Value
                };
            }
            return eff;
        }
    }
}
```

- [x] **Step 2: Write `ModuleData/Enlisted/Effects/scripted_effects.json`**

Seed set from spec §11.3 (post–dead-code purge):

```json
{
  "effects": {
    "rank_xp_minor":             [ { "apply": "quality_add", "quality": "rank_xp", "amount": 25 } ],
    "rank_xp_moderate":          [ { "apply": "quality_add", "quality": "rank_xp", "amount": 75 } ],
    "rank_xp_major":             [ { "apply": "quality_add", "quality": "rank_xp", "amount": 200 } ],
    "battles_survived_credit":   [ { "apply": "quality_add", "quality": "battles_survived", "amount": 1 } ],

    "lord_relation_up_small":    [ { "apply": "quality_add", "quality": "lord_relation", "amount": 2 } ],
    "lord_relation_up_moderate": [ { "apply": "quality_add", "quality": "lord_relation", "amount": 5 } ],
    "lord_relation_up_major":    [ { "apply": "quality_add", "quality": "lord_relation", "amount": 10 } ],
    "lord_relation_down_small":  [ { "apply": "quality_add", "quality": "lord_relation", "amount": -2 } ],
    "lord_relation_down_major":  [ { "apply": "quality_add", "quality": "lord_relation", "amount": -10 }, { "apply": "quality_add", "quality": "scrutiny", "amount": 5 } ],

    "scrutiny_up_small":         [ { "apply": "quality_add", "quality": "scrutiny", "amount": 3 } ],
    "scrutiny_up_moderate":      [ { "apply": "quality_add", "quality": "scrutiny", "amount": 7 } ],
    "scrutiny_up_major":         [ { "apply": "quality_add", "quality": "scrutiny", "amount": 15 }, { "apply": "set_flag", "name": "under_investigation", "days": 5 } ],
    "scrutiny_down_small":       [ { "apply": "quality_add", "quality": "scrutiny", "amount": -3 } ],
    "scrutiny_down_moderate":    [ { "apply": "quality_add", "quality": "scrutiny", "amount": -7 } ],

    "medical_risk_up_small":     [ { "apply": "quality_add", "quality": "medical_risk", "amount": 1 } ],
    "medical_risk_up_major":     [ { "apply": "quality_add", "quality": "medical_risk", "amount": 3 }, { "apply": "set_flag", "name": "wounded", "days": 7 } ],

    "supplies_up_small":         [ { "apply": "quality_add", "quality": "supplies", "amount": 5 } ],
    "supplies_down_moderate":    [ { "apply": "quality_add", "quality": "supplies", "amount": -15 } ],
    "readiness_up_small":        [ { "apply": "quality_add", "quality": "readiness", "amount": 3 } ],
    "readiness_down_moderate":   [ { "apply": "quality_add", "quality": "readiness", "amount": -10 } ],

    "loyalty_up_small_for_slot":   [ { "apply": "quality_add_for_hero", "quality": "loyalty", "amount": 2 } ],
    "loyalty_down_major_for_slot": [ { "apply": "quality_add_for_hero", "quality": "loyalty", "amount": -5 } ]
  }
}
```

- [x] **Step 3: Load scripted effects at boot**

Modify `src/Features/Qualities/QualityBehavior.cs` — in `OnSessionLaunched` and `OnGameLoaded`, add a call to load scripted effects BEFORE RegisterDefinitions (so storylet-loading later can validate references):

```csharp
private void OnSessionLaunched(CampaignGameStarter starter)
{
    QualityStore.SetInstance(_store);
    Enlisted.Features.Content.ScriptedEffectRegistry.LoadAll();
    RegisterDefinitions();
}

private void OnGameLoaded(CampaignGameStarter starter)
{
    QualityStore.SetInstance(_store);
    Enlisted.Features.Content.ScriptedEffectRegistry.LoadAll();
    RegisterDefinitions();
}
```

- [x] **Step 4: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Content\ScriptedEffectRegistry.cs" />
```

- [x] **Step 5: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/ScriptedEffectRegistry.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 6: Commit**

```bash
cat > /tmp/scripted-effects-commit.txt << 'EOF'
feat(content): ScriptedEffectRegistry + seed career-progression effects

Spec 0 Stage 6.2 — registry loads
ModuleData/Enlisted/Effects/scripted_effects.json at boot. Seed
catalog per spec §11.3: rank_xp_minor/moderate/major,
battles_survived_credit, lord_relation_*/scrutiny_*/medical_risk_*/
supplies_*/readiness_*/loyalty_*_for_slot.

QualityBehavior calls ScriptedEffectRegistry.LoadAll during
OnSessionLaunched / OnGameLoaded so scripted effects are resolvable
when storylets first reference them.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Content/ScriptedEffectRegistry.cs ModuleData/Enlisted/Effects/scripted_effects.json src/Features/Qualities/QualityBehavior.cs Enlisted.csproj
git commit -F /tmp/scripted-effects-commit.txt
rm /tmp/scripted-effects-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT E — Effects live

Proceed to triggers + slots.

---

## Stage 7 — TriggerRegistry

### Task 7.1: TriggerRegistry with seed predicates

**Files:**
- Create: `src/Features/Content/TriggerRegistry.cs`

- [x] **Step 1: Write `src/Features/Content/TriggerRegistry.cs`**

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Flags;
using Enlisted.Features.Qualities;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Named predicate dispatch. Triggers come in three forms:
    ///   - Flat name: "is_enlisted"
    ///   - Name:arg: "flag:veteran_meeting_done"
    ///   - Name:arg:arg: "quality_gte:scrutiny:75"
    /// Unknown trigger ids are treated as false + Expected-logged once.
    /// </summary>
    public static class TriggerRegistry
    {
        private static readonly Dictionary<string, Func<string[], StoryletContext, bool>> _predicates =
            new Dictionary<string, Func<string[], StoryletContext, bool>>(StringComparer.OrdinalIgnoreCase);

        static TriggerRegistry() { RegisterSeedPredicates(); }

        public static void Register(string name, Func<string[], StoryletContext, bool> predicate)
        {
            if (string.IsNullOrEmpty(name) || predicate == null) return;
            _predicates[name] = predicate;
        }

        public static bool Evaluate(IList<string> triggers, StoryletContext ctx)
        {
            if (triggers == null || triggers.Count == 0) return true;
            foreach (var t in triggers)
            {
                if (!EvaluateOne(t, ctx)) return false;
            }
            return true;
        }

        public static bool EvaluateOne(string trigger, StoryletContext ctx)
        {
            if (string.IsNullOrEmpty(trigger)) return true;
            var negate = trigger.StartsWith("not:", StringComparison.OrdinalIgnoreCase);
            var body = negate ? trigger.Substring(4) : trigger;
            var parts = body.Split(':');
            var name = parts[0];
            var args = parts.Length > 1 ? new string[parts.Length - 1] : System.Array.Empty<string>();
            for (var i = 1; i < parts.Length; i++) args[i - 1] = parts[i];

            if (!_predicates.TryGetValue(name, out var predicate))
            {
                ModLogger.Expected("TRIGGER", "unknown_" + name, "Unknown trigger: " + name);
                return false;
            }
            try
            {
                var result = predicate(args, ctx);
                return negate ? !result : result;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TRIGGER", "Trigger threw: " + name, ex);
                return false;
            }
        }

        // ---------- Seed predicates ----------

        private static void RegisterSeedPredicates()
        {
            Register("is_enlisted", (_, _2) => EnlistmentBehavior.Instance?.IsEnlisted == true);

            Register("is_at_sea", (_, _2) => MobileParty.MainParty?.IsCurrentlyAtSea == true);

            Register("in_settlement", (_, _2) => MobileParty.MainParty?.CurrentSettlement != null);

            Register("in_siege", (_, _2) =>
            {
                var p = MobileParty.MainParty;
                if (p == null) return false;
                if (p.BesiegerCamp != null) return true;
                return p.CurrentSettlement?.SiegeEvent != null;
            });

            Register("in_mapevent", (_, _2) => MobileParty.MainParty?.MapEvent != null);

            Register("context", (args, ctx) =>
                args.Length > 0 && string.Equals(ctx?.CurrentContext, args[0], StringComparison.OrdinalIgnoreCase));

            Register("flag", (args, _) =>
                args.Length > 0 && FlagStore.Instance?.Has(args[0]) == true);

            Register("quality_gte", (args, _) =>
            {
                if (args.Length < 2 || QualityStore.Instance == null) return false;
                return int.TryParse(args[1], out var threshold)
                    && QualityStore.Instance.Get(args[0]) >= threshold;
            });

            Register("quality_lte", (args, _) =>
            {
                if (args.Length < 2 || QualityStore.Instance == null) return false;
                return int.TryParse(args[1], out var threshold)
                    && QualityStore.Instance.Get(args[0]) <= threshold;
            });

            Register("rank_gte", (args, _) =>
            {
                if (args.Length < 1) return false;
                return int.TryParse(args[0], out var threshold)
                    && (EnlistmentBehavior.Instance?.EnlistmentTier ?? 0) >= threshold;
            });

            Register("veteran_has_flag", (args, ctx) =>
            {
                if (args.Length < 1) return false;
                if (ctx?.ResolvedSlots == null) return false;
                if (!ctx.ResolvedSlots.TryGetValue("veteran", out var hero) || hero == null) return false;
                return FlagStore.Instance?.HasForHero(hero, args[0]) == true;
            });

            Register("beat", (_, _2) =>
            {
                // Beat triggers are matched upstream by the source that emits the candidate.
                // At the storylet layer, treat as true — the source wouldn't have called us
                // if the beat didn't match. This is a marker, not a runtime check.
                return true;
            });
        }
    }
}
```

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Content\TriggerRegistry.cs" />
```

- [x] **Step 3: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/TriggerRegistry.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 4: Commit**

```bash
cat > /tmp/trigger-registry-commit.txt << 'EOF'
feat(content): TriggerRegistry with 12 seed predicates

Spec 0 Stage 7 — named-predicate dispatch. Seed predicates:
is_enlisted, is_at_sea, in_settlement, in_siege, in_mapevent,
context, flag, quality_gte, quality_lte, rank_gte,
veteran_has_flag, beat (marker). not: prefix negates any predicate.
Unknown triggers log once and evaluate false.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Content/TriggerRegistry.cs Enlisted.csproj
git commit -F /tmp/trigger-registry-commit.txt
rm /tmp/trigger-registry-commit.txt
```

---

## Stage 8 — SlotFiller

### Task 8.1: SlotFiller

**Files:**
- Create: `src/Features/Content/SlotFiller.cs`

- [x] **Step 1: Write `src/Features/Content/SlotFiller.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Resolves scope.slots against world state. Returns a dictionary of slot-name -> Hero
    /// (or null for faction-style roles). Null entries for required slots mean the storylet
    /// is ineligible; callers drop.
    ///
    /// Also provides text flattening — {veteran.name} -> SetTextVariable("VERTERAN_NAME", ...)
    /// because TaleWorlds TextObject doesn't parse dotted paths.
    /// </summary>
    public static class SlotFiller
    {
        private static readonly Regex DottedSlotPattern = new Regex(@"\{(\w+)\.(\w+)(?::([\w_]+))?\}", RegexOptions.Compiled);

        public static Dictionary<string, Hero> Resolve(ScopeDecl scope)
        {
            var resolved = new Dictionary<string, Hero>(StringComparer.OrdinalIgnoreCase);
            if (scope?.Slots == null || scope.Slots.Count == 0) return resolved;
            foreach (var kv in scope.Slots)
            {
                var slotName = kv.Key;
                var decl = kv.Value;
                var hero = ResolveOne(decl);
                resolved[slotName] = hero; // null if unresolvable — caller checks
            }
            return resolved;
        }

        public static bool AllRequiredFilled(Dictionary<string, Hero> resolved)
        {
            foreach (var kv in resolved)
            {
                if (kv.Value == null) return false;
            }
            return true;
        }

        /// <summary>Render a "{=id}Fallback" template with dotted-slot substitution flattened to native SetTextVariable keys.</summary>
        public static string Render(string template, Dictionary<string, Hero> slots)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            var flat = DottedSlotPattern.Replace(template, match =>
            {
                var slot = match.Groups[1].Value;
                var prop = match.Groups[2].Value;
                var key = (slot + "_" + prop).ToUpperInvariant();
                return "{" + key + "}";
            });
            var textObj = new TextObject(flat);
            foreach (var kv in slots ?? new Dictionary<string, Hero>())
            {
                if (kv.Value == null) continue;
                var upper = kv.Key.ToUpperInvariant();
                textObj.SetTextVariable(upper + "_NAME", kv.Value.Name);
                textObj.SetTextVariable(upper + "_OCCUPATION", kv.Value.Occupation.ToString());
                textObj.SetTextVariable(upper + "_CULTURE", kv.Value.Culture?.Name ?? new TextObject(string.Empty));
            }
            return textObj.ToString();
        }

        private static Hero ResolveOne(SlotDecl decl)
        {
            if (decl == null || string.IsNullOrEmpty(decl.Role)) return null;
            switch (decl.Role.ToLowerInvariant())
            {
                case "enlisted_lord": return EnlistmentBehavior.Instance?.EnlistedLord;
                case "squadmate":     return ResolveSquadmate(decl.Tag);
                case "named_veteran":
                    ModLogger.Expected("SLOT", "named_veteran_not_ready",
                        "named_veteran slot is reserved for a later surface spec that wires the query API.");
                    return null;
                case "chaplain":      return ResolveFlaggedHero("chaplain");
                case "quartermaster": return EnlistmentBehavior.Instance?.QuartermasterHero;
                case "enemy_commander":
                    var me = MobileParty.MainParty?.MapEvent;
                    return me?.AttackerSide?.LeaderParty == MobileParty.MainParty?.Party
                        ? me?.DefenderSide?.LeaderParty?.LeaderHero
                        : me?.AttackerSide?.LeaderParty?.LeaderHero;
                case "settlement_notable":
                    var s = MobileParty.MainParty?.CurrentSettlement;
                    return s?.Notables?.FirstOrDefault();
                case "kingdom":
                    // Faction-style role; Hero-typed return is inappropriate. SlotFiller returns null
                    // and callers that need a faction read scope.slots meta directly for kingdom slots.
                    return null;
                default:
                    ModLogger.Expected("SLOT", "unknown_role_" + decl.Role, "Unknown slot role: " + decl.Role);
                    return null;
            }
        }

        private static Hero ResolveSquadmate(string requiredTag)
        {
            // Read the retinue by its public API — the concrete call sits in RetinueManager.
            // Signature may be RetinueManager.Instance.AllRetinueHeroes or similar; verify
            // before shipping. This stub iterates "soldier" heroes the mod has spawned and
            // filters by the optional flag. Surface specs refine with the real API.
            if (!string.IsNullOrEmpty(requiredTag) && FlagStore.Instance == null) return null;

            var candidates = TaleWorlds.CampaignSystem.Hero.AllAliveHeroes
                .Where(h => h?.Occupation == Occupation.Soldier)
                .ToList();

            if (!string.IsNullOrEmpty(requiredTag))
            {
                candidates = candidates
                    .Where(h => FlagStore.Instance?.HasForHero(h, requiredTag) == true)
                    .ToList();
            }
            return candidates.FirstOrDefault();
        }

        private static Hero ResolveFlaggedHero(string flag)
        {
            if (FlagStore.Instance == null) return null;
            return TaleWorlds.CampaignSystem.Hero.AllAliveHeroes
                .Where(h => h?.Occupation == Occupation.Soldier)
                .FirstOrDefault(h => FlagStore.Instance.HasForHero(h, flag));
        }
    }
}
```

Note: `Hero.AllAliveHeroes` — verify this property exists on `Hero` in the decompile (grep `public static.*AllAliveHeroes`). If not, use `Campaign.Current.AliveHeroes` or the equivalent enumeration.

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Content\SlotFiller.cs" />
```

- [x] **Step 3: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/SlotFiller.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 4: Commit**

```bash
cat > /tmp/slot-filler-commit.txt << 'EOF'
feat(content): SlotFiller with role resolution + text flattening

Spec 0 Stage 8 — resolves ScopeDecl.Slots to Hero instances for seven
roles (enlisted_lord, squadmate, chaplain, quartermaster,
enemy_commander, settlement_notable, kingdom). named_veteran is
explicitly deferred to a later surface spec. Render flattens dotted
{veteran.name} syntax to flat VETERAN_NAME SetTextVariable keys —
TextObject doesn't parse dotted paths per decompile verification.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Content/SlotFiller.cs Enlisted.csproj
git commit -F /tmp/slot-filler-commit.txt
rm /tmp/slot-filler-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT F — Triggers + Slots live

Proceed to StoryletCatalog + Storylet default impls.

---

## Stage 9 — StoryletCatalog + Storylet.ToCandidate

### Task 9.1: Fill in Storylet default implementations

**Files:**
- Modify: `src/Features/Content/Storylet.cs`

- [x] **Step 1: Replace the stub methods in `Storylet.cs` with real implementations**

Open `src/Features/Content/Storylet.cs` and replace the three virtual methods at the bottom with:

```csharp
public virtual bool IsEligible(StoryletContext ctx)
{
    if (ctx == null) return false;

    // Scope.context filter — skipped for mid-encounter storylets (spec §10.3).
    if (!string.IsNullOrEmpty(Scope?.Context)
        && !string.Equals(Scope.Context, "any", System.StringComparison.OrdinalIgnoreCase)
        && !string.Equals(Scope.Context, ctx.CurrentContext, System.StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    // Slot requirements — any unfilled required slot = ineligible.
    var resolved = SlotFiller.Resolve(Scope);
    if (!SlotFiller.AllRequiredFilled(resolved)) return false;
    ctx.ResolvedSlots = resolved;

    // Trigger predicates.
    if (!TriggerRegistry.Evaluate(Trigger, ctx)) return false;

    return true;
}

public virtual float WeightFor(StoryletContext ctx)
{
    var w = Weight?.Base ?? 1.0f;
    if (Weight?.Modifiers == null) return w;
    foreach (var mod in Weight.Modifiers)
    {
        if (string.IsNullOrEmpty(mod.If)) continue;
        if (TriggerRegistry.EvaluateOne(mod.If, ctx)) w *= mod.Factor;
    }
    return w;
}

public virtual StoryCandidate ToCandidate(StoryletContext ctx)
{
    if (ctx == null) return null;
    return new StoryCandidate
    {
        SourceId = "storylet." + Id,
        CategoryId = string.IsNullOrEmpty(Category) ? "storylet" : Category,
        ProposedTier = Observational ? StoryTier.Log : StoryTier.Modal,
        SeverityHint = 0.4f,
        Beats = new System.Collections.Generic.HashSet<StoryBeat>(),
        Relevance = new RelevanceKey
        {
            SubjectHero = ctx.SubjectHero,
            TouchesEnlistedLord = ctx.SubjectHero != null &&
                ctx.SubjectHero == Enlisted.Features.Enlistment.Behaviors.EnlistmentBehavior.Instance?.EnlistedLord,
        },
        EmittedAt = ctx.EvaluatedAt,
        HasObservationalRender = Observational,
        RenderedTitle = SlotFiller.Render(Title, ctx.ResolvedSlots),
        RenderedBody = SlotFiller.Render(Setup, ctx.ResolvedSlots),
        StoryKey = Id
    };
}
```

- [x] **Step 2: Build**

```bash
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors. If `RelevanceKey` or `StoryCandidate` fields drifted since the pacing spec, adjust initializers accordingly — the pacing spec's struct shape is authoritative.

- [x] **Step 3: Commit**

```bash
cat > /tmp/storylet-impl-commit.txt << 'EOF'
feat(content): Storylet IsEligible/WeightFor/ToCandidate default impls

Spec 0 Stage 9.1 — wire the Stage 1 stubs to TriggerRegistry +
SlotFiller + StoryCandidate. Context filter gates by scope, slots
must all resolve, triggers must all pass. ToCandidate builds the
candidate with rendered text (slot-flattened). Observational
storylets propose Log tier; others propose Modal.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Content/Storylet.cs
git commit -F /tmp/storylet-impl-commit.txt
rm /tmp/storylet-impl-commit.txt
```

### Task 9.2: StoryletCatalog

**Files:**
- Create: `src/Features/Content/StoryletCatalog.cs`

- [x] **Step 1: Write `src/Features/Content/StoryletCatalog.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Loads storylet JSON from ModuleData/Enlisted/Storylets/*.json. Spec 0 ships NO
    /// storylet content — surface specs author and load. This catalog is the contract.
    /// </summary>
    public static class StoryletCatalog
    {
        private static Dictionary<string, Storylet> _byId =
            new Dictionary<string, Storylet>(StringComparer.OrdinalIgnoreCase);

        public static void LoadAll()
        {
            _byId.Clear();
            var dir = ModulePaths.GetContentPath("Storylets");
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("STORYLET", "no_storylet_dir", "Storylets directory not found: " + dir);
                return;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);
                    var arr = root["storylets"] as JArray ?? (root as JContainer);
                    if (arr == null) continue;
                    foreach (var item in arr.OfType<JObject>())
                    {
                        var s = Parse(item);
                        if (s == null || string.IsNullOrEmpty(s.Id)) continue;
                        _byId[s.Id] = s;
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("STORYLET", "Failed to load storylet file", ex, new { file });
                }
            }
        }

        public static Storylet GetById(string id) =>
            !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var s) ? s : null;

        public static IEnumerable<Storylet> All => _byId.Values;

        public static IEnumerable<string> AllIds => _byId.Keys;

        private static Storylet Parse(JObject obj)
        {
            if (obj == null) return null;
            var s = new Storylet
            {
                Id = (string)obj["id"] ?? string.Empty,
                Title = (string)obj["title"] ?? string.Empty,
                Setup = (string)obj["setup"] ?? string.Empty,
                Theme = (string)obj["theme"] ?? string.Empty,
                Category = (string)obj["category"] ?? string.Empty,
                Severity = (string)obj["severity"] ?? "normal",
                Observational = (bool?)obj["observational"] ?? false,
                CooldownDays = (int?)obj["cooldown_days"] ?? 0,
                OneTime = (bool?)obj["one_time"] ?? false,
                Scope = ParseScope(obj["scope"] as JObject),
                Weight = ParseWeight(obj["weight"] as JObject),
            };
            s.Trigger = ParseStringList(obj["trigger"] as JArray);
            s.Immediate = ParseEffectList(obj["immediate"] as JArray);
            s.Options = ParseChoices(obj["options"] as JArray);
            return s;
        }

        private static ScopeDecl ParseScope(JObject obj)
        {
            var scope = new ScopeDecl();
            if (obj == null) return scope;
            scope.Context = (string)obj["context"] ?? "any";
            var slots = obj["slots"] as JObject;
            if (slots == null) return scope;
            foreach (var prop in slots.Properties())
            {
                var decl = prop.Value as JObject;
                if (decl == null) continue;
                scope.Slots[prop.Name] = new SlotDecl
                {
                    Role = (string)decl["role"] ?? string.Empty,
                    Tag = (string)decl["tag"] ?? string.Empty
                };
            }
            return scope;
        }

        private static WeightDecl ParseWeight(JObject obj)
        {
            var w = new WeightDecl();
            if (obj == null) return w;
            w.Base = (float?)obj["base"] ?? 1.0f;
            var mods = obj["modifiers"] as JArray;
            if (mods == null) return w;
            foreach (var m in mods.OfType<JObject>())
            {
                w.Modifiers.Add(new WeightModifier
                {
                    If = (string)m["if"] ?? string.Empty,
                    Factor = (float?)m["factor"] ?? 1.0f
                });
            }
            return w;
        }

        private static List<string> ParseStringList(JArray arr)
        {
            var list = new List<string>();
            if (arr == null) return list;
            foreach (var item in arr) list.Add((string)item);
            return list;
        }

        private static List<EffectDecl> ParseEffectList(JArray arr)
        {
            var list = new List<EffectDecl>();
            if (arr == null) return list;
            foreach (var item in arr.OfType<JObject>())
            {
                var apply = (string)item["apply"];
                if (string.IsNullOrEmpty(apply)) continue;
                var eff = new EffectDecl { Apply = apply };
                foreach (var prop in item.Properties())
                {
                    if (prop.Name == "apply") continue;
                    eff.Params[prop.Name] = prop.Value.Type switch
                    {
                        JTokenType.Integer => (object)(long)prop.Value,
                        JTokenType.Float => (object)(double)prop.Value,
                        JTokenType.Boolean => (object)(bool)prop.Value,
                        _ => (object)(string)prop.Value
                    };
                }
                list.Add(eff);
            }
            return list;
        }

        private static List<Choice> ParseChoices(JArray arr)
        {
            var list = new List<Choice>();
            if (arr == null) return list;
            foreach (var item in arr.OfType<JObject>())
            {
                var c = new Choice
                {
                    Id = (string)item["id"] ?? string.Empty,
                    Text = (string)item["text"] ?? string.Empty,
                    Tooltip = (string)item["tooltip"] ?? string.Empty,
                    Trigger = ParseStringList(item["trigger"] as JArray),
                    Effects = ParseEffectList(item["effects"] as JArray),
                    EffectsSuccess = ParseEffectList(item["effectsSuccess"] as JArray),
                    EffectsFailure = ParseEffectList(item["effectsFailure"] as JArray),
                };
                var chain = item["chain"] as JObject;
                if (chain != null)
                {
                    c.Chain = new ChainDecl
                    {
                        Id = (string)chain["id"] ?? string.Empty,
                        Days = (int?)chain["days"] ?? 0,
                        When = (string)chain["when"] ?? string.Empty
                    };
                }
                list.Add(c);
            }
            return list;
        }
    }
}
```

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Content\StoryletCatalog.cs" />
```

- [x] **Step 3: Load storylets at boot (call from QualityBehavior for now — surface specs move this later)**

Add to `QualityBehavior.OnSessionLaunched` + `OnGameLoaded`, after `ScriptedEffectRegistry.LoadAll()`:

```csharp
Enlisted.Features.Content.StoryletCatalog.LoadAll();
```

- [x] **Step 4: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/StoryletCatalog.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 5: Commit**

```bash
cat > /tmp/storylet-catalog-commit.txt << 'EOF'
feat(content): StoryletCatalog JSON loader

Spec 0 Stage 9.2 — loads ModuleData/Enlisted/Storylets/*.json at
boot. Spec 0 ships no storylet content; this catalog is the contract
that surface specs author against. Parse covers scope, weight,
trigger, immediate, options (with per-option effects / effectsSuccess /
effectsFailure / chain). Loaded at session-launch + game-loaded
alongside scripted effects.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Content/StoryletCatalog.cs src/Features/Qualities/QualityBehavior.cs Enlisted.csproj
git commit -F /tmp/storylet-catalog-commit.txt
rm /tmp/storylet-catalog-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT G — Storylet layer complete (no Activity yet)

Storylets can be authored, loaded, evaluated, rendered, and turned into candidates. Proceed to activities.

---

## Stage 10 — Activity subsystem

### Task 10.1: Activity abstract base

**Files:**
- Create: `src/Features/Activities/Activity.cs`

- [x] **Step 1: Write `src/Features/Activities/Activity.cs`**

```csharp
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Activities
{
    /// <summary>
    /// Abstract stateful ticking activity. Banner-Kings Feast-shape: each subclass (CampActivity,
    /// MusterActivity, OrderActivity, QuartermasterActivity) ships its own OnStart/Tick/OnPhaseEnter/
    /// OnPhaseExit/Finish logic and claims a save-definer offset from 45-60 in its surface spec.
    /// </summary>
    [Serializable]
    public abstract class Activity
    {
        public string Id { get; set; } = string.Empty;                 // instance id
        public string TypeId { get; set; } = string.Empty;             // template id
        public string Intent { get; set; } = string.Empty;
        public int CurrentPhaseIndex { get; set; }
        public List<Hero> Attendees { get; set; } = new List<Hero>();
        public CampaignTime StartedAt { get; set; } = CampaignTime.Zero;
        public CampaignTime EndsAt { get; set; } = CampaignTime.Zero;
        public Dictionary<string, int> PhaseQuality { get; set; } = new Dictionary<string, int>();

        // Non-serialized runtime cache — resolved from ActivityTypeDefinition on load.
        [NonSerialized] private List<Phase> _cachedPhases;
        public List<Phase> Phases
        {
            get => _cachedPhases ?? new List<Phase>();
            set => _cachedPhases = value;
        }

        public Phase CurrentPhase =>
            _cachedPhases != null && CurrentPhaseIndex >= 0 && CurrentPhaseIndex < _cachedPhases.Count
                ? _cachedPhases[CurrentPhaseIndex]
                : null;

        public abstract void OnStart(ActivityContext ctx);
        public abstract void Tick(bool hourly);
        public abstract void OnPhaseEnter(Phase phase);
        public abstract void OnPhaseExit(Phase phase);
        public abstract void Finish(ActivityEndReason reason);

        /// <summary>Called by ActivityRuntime after load; resolves Phases from the TypeDefinition.</summary>
        public void ResolvePhasesFromType(IDictionary<string, ActivityTypeDefinition> types)
        {
            if (string.IsNullOrEmpty(TypeId)) return;
            if (types == null || !types.TryGetValue(TypeId, out var def)) return;
            _cachedPhases = def.Phases;
        }
    }
}
```

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Activity.cs" />
```

- [x] **Step 3: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Activity.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 4: Commit**

```bash
cat > /tmp/activity-base-commit.txt << 'EOF'
feat(activities): Activity abstract base class

Spec 0 Stage 10.1 — Banner-Kings Feast-shape stateful base. Instance
state (Id, TypeId, Intent, CurrentPhaseIndex, Attendees, StartedAt,
EndsAt, PhaseQuality) persists via SyncData. Phase list is
non-serialized cache — ResolvePhasesFromType re-binds on load from
the ActivityTypeDefinition registry. Abstract OnStart / Tick /
OnPhaseEnter / OnPhaseExit / Finish are filled in by concrete
subclasses (CampActivity, MusterActivity, OrderActivity,
QuartermasterActivity) in surface specs 1, 4, 2, 5 respectively;
each claims a save-definer offset from 45-60.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Activities/Activity.cs Enlisted.csproj
git commit -F /tmp/activity-base-commit.txt
rm /tmp/activity-base-commit.txt
```

### Task 10.2: ActivityRuntime

**Files:**
- Create: `src/Features/Activities/ActivityRuntime.cs`
- Modify: `src/Mod.Entry/SubModule.cs`

- [x] **Step 1: Write `src/Features/Activities/ActivityRuntime.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Activities
{
    /// <summary>
    /// Singleton host for active Activities. Ticks on hourly + daily campaign events,
    /// picks phase-pool storylets per tick (for Auto phases), parks chain candidates
    /// targeted at named activity phases via "when: activity.phase" until the activity
    /// reaches that phase.
    /// </summary>
    public sealed class ActivityRuntime : CampaignBehaviorBase
    {
        public static ActivityRuntime Instance { get; private set; }

        private List<Activity> _active = new List<Activity>();
        private Dictionary<string, ActivityTypeDefinition> _types =
            new Dictionary<string, ActivityTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        // Parked chain continuations: key = "activityType.phaseId", value = list of storylet ids to fire on entry.
        private Dictionary<string, List<string>> _parkedChains =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<Activity> ActiveActivities => _active;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_active", ref _active);
                dataStore.SyncData("_parkedChains", ref _parkedChains);
                _active ??= new List<Activity>();
                _parkedChains ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ACTIVITY", "ActivityRuntime SyncData failed", ex);
                _active = new List<Activity>();
                _parkedChains = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Instance = this;
            // ActivityTypeDefinition JSON loading is a surface-spec concern. Spec 0 leaves _types empty.
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            Instance = this;
            foreach (var a in _active) a.ResolvePhasesFromType(_types);
        }

        public void RegisterType(ActivityTypeDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            _types[def.Id] = def;
        }

        public void Start(Activity activity, ActivityContext ctx)
        {
            if (activity == null) return;
            activity.ResolvePhasesFromType(_types);
            activity.StartedAt = CampaignTime.Now;
            activity.OnStart(ctx);
            if (activity.CurrentPhase != null) activity.OnPhaseEnter(activity.CurrentPhase);
            _active.Add(activity);
            ConsumeParkedChains(activity);
        }

        public void ParkChain(string activityTypeId, string phaseId, string storyletId)
        {
            if (string.IsNullOrEmpty(activityTypeId) || string.IsNullOrEmpty(phaseId) || string.IsNullOrEmpty(storyletId)) return;
            var key = activityTypeId + "." + phaseId;
            if (!_parkedChains.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _parkedChains[key] = list;
            }
            list.Add(storyletId);
        }

        private void ConsumeParkedChains(Activity activity)
        {
            if (activity?.CurrentPhase == null) return;
            var key = activity.TypeId + "." + activity.CurrentPhase.Id;
            if (!_parkedChains.TryGetValue(key, out var list) || list.Count == 0) return;
            foreach (var storyletId in list)
            {
                var storylet = StoryletCatalog.GetById(storyletId);
                if (storylet == null) continue;
                var ctx = new StoryletContext { ActivityTypeId = activity.TypeId, PhaseId = activity.CurrentPhase.Id };
                var candidate = storylet.ToCandidate(ctx);
                if (candidate != null)
                {
                    candidate.ChainContinuation = true;
                    StoryDirector.Instance?.EmitCandidate(candidate);
                }
            }
            list.Clear();
        }

        private void OnHourlyTick()
        {
            foreach (var a in _active.ToList())
            {
                try { a.Tick(hourly: true); }
                catch (Exception ex) { ModLogger.Caught("ACTIVITY", "Activity hourly tick failed", ex); }

                if (a.CurrentPhase == null) continue;
                TryFireAutoPhaseStorylet(a);
                TryAdvancePhase(a);
            }
        }

        private void OnDailyTick()
        {
            foreach (var a in _active.ToList())
            {
                try { a.Tick(hourly: false); }
                catch (Exception ex) { ModLogger.Caught("ACTIVITY", "Activity daily tick failed", ex); }
            }
        }

        private void TryFireAutoPhaseStorylet(Activity a)
        {
            var phase = a.CurrentPhase;
            if (phase == null || phase.Delivery != PhaseDelivery.Auto) return;
            if (phase.Pool == null || phase.Pool.Count == 0) return;

            var ctx = new StoryletContext
            {
                ActivityTypeId = a.TypeId,
                PhaseId = phase.Id,
                CurrentContext = DeriveContext()
            };

            // Weighted eligible selection.
            var eligible = new List<(Storylet storylet, float weight)>();
            foreach (var id in phase.Pool)
            {
                var s = StoryletCatalog.GetById(id);
                if (s == null) continue;
                if (!s.IsEligible(ctx)) continue;
                var w = s.WeightFor(ctx);
                if (phase.IntentBias != null && phase.IntentBias.TryGetValue(a.Intent ?? string.Empty, out var bias))
                    w *= bias;
                if (w > 0f) eligible.Add((s, w));
            }
            if (eligible.Count == 0) return;

            var total = eligible.Sum(e => e.weight);
            var r = (float)(new Random().NextDouble() * total);
            var cursor = 0f;
            foreach (var (s, w) in eligible)
            {
                cursor += w;
                if (r > cursor) continue;
                var candidate = s.ToCandidate(ctx);
                if (candidate != null) StoryDirector.Instance?.EmitCandidate(candidate);
                return;
            }
        }

        private void TryAdvancePhase(Activity a)
        {
            var phase = a.CurrentPhase;
            if (phase == null) return;
            var elapsedHours = (CampaignTime.Now - a.StartedAt).ToHours;
            var cumulative = 0;
            for (var i = 0; i <= a.CurrentPhaseIndex && i < a.Phases.Count; i++)
                cumulative += a.Phases[i].DurationHours;
            if (elapsedHours < cumulative) return;

            a.OnPhaseExit(phase);
            a.CurrentPhaseIndex++;
            if (a.CurrentPhaseIndex >= a.Phases.Count)
            {
                a.Finish(ActivityEndReason.Completed);
                _active.Remove(a);
                return;
            }
            a.OnPhaseEnter(a.CurrentPhase);
            ConsumeParkedChains(a);
        }

        private static string DeriveContext()
        {
            var p = MobileParty.MainParty;
            if (p == null) return "any";
            if (p.BesiegerCamp != null || p.CurrentSettlement?.SiegeEvent != null) return "siege";
            if (p.CurrentSettlement != null) return "settlement";
            if (p.IsCurrentlyAtSea) return "sea";
            return "land";
        }
    }
}
```

- [x] **Step 2: Add to `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\ActivityRuntime.cs" />
```

- [x] **Step 3: Register in `src/Mod.Entry/SubModule.cs`**

Place AFTER `QualityBehavior` (so activities can read qualities during tick) and BEFORE `StoryDirector` (runtime emits candidates into it):

```csharp
campaignStarter.AddBehavior(new Enlisted.Features.Activities.ActivityRuntime());
```

- [x] **Step 4: Extend `EnlistedSaveDefiner` for Activity**

In `DefineClassTypes()`, storylet-backbone section:

```csharp
AddClassDefinition(typeof(Enlisted.Features.Activities.Activity), 44);
```

In `DefineEnumTypes()`, storylet-backbone enums block:

```csharp
AddEnumDefinition(typeof(Enlisted.Features.Activities.ActivityEndReason), 83);
```

In `DefineContainerDefinitions()`, storylet-backbone containers:

```csharp
ConstructContainerDefinition(typeof(System.Collections.Generic.List<Enlisted.Features.Activities.Activity>));
ConstructContainerDefinition(typeof(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>));
```

- [x] **Step 5: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/ActivityRuntime.cs
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 6: Commit**

```bash
cat > /tmp/activity-runtime-commit.txt << 'EOF'
feat(activities): ActivityRuntime + Activity save registration

Spec 0 Stage 10.2 — singleton host for active Activities. Hourly
tick drives Activity.Tick + Auto-phase storylet selection (weighted
by storylet weight * phase.IntentBias for the active intent).
Advances phase when cumulative duration elapsed; Finish on last
phase. ParkChain stores chain storylets keyed by activity.phase;
ConsumeParkedChains fires them with ChainContinuation=true when
the activity enters that phase. DeriveContext implements spec §10.3
siege > settlement > sea > land precedence.

Save registration: Activity abstract class at offset 44,
ActivityEndReason enum at 83, List<Activity> and
Dictionary<string,List<string>> containers.

Registered in SubModule after QualityBehavior, before StoryDirector.
Surface specs add concrete subclasses (CampActivity, MusterActivity,
OrderActivity, QuartermasterActivity) with their own offsets 45-60.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Features/Activities/ActivityRuntime.cs src/Mod.Entry/SubModule.cs src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs Enlisted.csproj
git commit -F /tmp/activity-runtime-commit.txt
rm /tmp/activity-runtime-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT H — Activity subsystem live

All runtime plumbing in place. Proceed to validation + harness.

---

## Stage 11 — validate_content.py Phase 12

### Task 11.1: Extend validator with scripted-effect + quality-id checks

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [x] **Step 1: Read the existing validator to find where phases are registered**

```bash
grep -n "def phase_\|PHASE_\|Phase 1\|Phase 10\|Phase 11" Tools/Validation/validate_content.py | head -30
```
Expected: a list of phase entry points. Note the numbering — Phase 11 currently blocks `ModLogger.Error(...)` per CLAUDE.md / spec §7.2 note.

- [x] **Step 2: Add Phase 12**

Append a new `phase_12_storylet_references` function patterned after the existing phase functions. It must:

1. Parse `ModuleData/Enlisted/Qualities/quality_defs.json` for the set of valid `qualities[*].id`.
2. Union with the six read-through ids hardcoded in `QualityBehavior.RegisterDefinitions`: `rank_xp`, `days_in_rank`, `battles_survived`, `rank`, `days_enlisted`, `lord_relation`.
3. Parse `ModuleData/Enlisted/Effects/scripted_effects.json` for the set of valid `effects.*` keys and the primitive ids referenced inside each effect body.
4. Walk `ModuleData/Enlisted/Storylets/**/*.json` (empty in Spec 0; surface specs populate) and check every `effects[*].apply` value is either a valid scripted-effect id or a known primitive (`quality_add`, `quality_add_for_hero`, `quality_set`, `set_flag`, `clear_flag`, `set_flag_on_hero`, `trait_xp`, `skill_xp`, `give_gold`, `give_item`). Check every `quality_add*` has a valid `quality`. Check no write targets a read-only quality (`rank`, `days_in_rank`, `days_enlisted`).
5. Fail the build with exit code non-zero on any violation, formatted like the existing phase errors.

The full Python implementation is left to the engineer working in the file — keep the style consistent with the existing `phase_10_*` and `phase_11_*` helpers; do not invent a new error format.

- [x] **Step 3: Wire Phase 12 into the main entry point**

Same pattern as Phase 11 registration — grep `Phase 11` usage and add Phase 12 alongside.

- [x] **Step 4: Run the validator**

```bash
python Tools/Validation/validate_content.py
```
Expected: all phases including Phase 12 pass. (Storylet directory is empty; scripted_effects + quality_defs cross-validate; no storylet references to check yet.)

- [x] **Step 5: Regenerate error-code registry**

```bash
python Tools/Validation/generate_error_codes.py
```

- [x] **Step 6: Commit**

```bash
cat > /tmp/validate-phase12-commit.txt << 'EOF'
feat(validation): validate_content.py Phase 12 — storylet references

Spec 0 Stage 11 — phase validates every storylet's effect apply
values resolve to a known scripted-effect id or primitive, every
quality_add targets a registered quality, and no write targets a
read-only read-through quality (rank, days_in_rank, days_enlisted).
Also cross-checks that scripted_effects.json quality references
match quality_defs.json. Build-blocking on any mismatch.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add Tools/Validation/validate_content.py docs/error-codes.md
git commit -F /tmp/validate-phase12-commit.txt
rm /tmp/validate-phase12-commit.txt
```

---

## Stage 12 — Debug harness + final verification

### Task 12.1: DebugToolsBehavior smoke helpers

**Files:**
- Modify: `src/Debugging/Behaviors/DebugToolsBehavior.cs`

- [x] **Step 1: Append smoke-test methods**

Add these static methods to `DebugToolsBehavior`. Each writes to `SessionDiagnostics.LogEvent("Debug", ...)` so results land in the session log. Surface specs can wire them to a debug submenu later:

```csharp
public static void SmokeTestQualities()
{
    var store = Enlisted.Features.Qualities.QualityStore.Instance;
    if (store == null)
    {
        SessionDiagnostics.LogEvent("Debug", "SmokeTestQualities", "FAIL: QualityStore.Instance null");
        return;
    }
    var before = store.Get("scrutiny");
    store.Add("scrutiny", 5, "smoke-test");
    var after = store.Get("scrutiny");
    var rankXp = store.Get("rank_xp");
    var lordRel = store.Get("lord_relation");
    SessionDiagnostics.LogEvent("Debug", "SmokeTestQualities",
        $"scrutiny {before}->{after} (+5), rank_xp={rankXp} (read-through), lord_relation={lordRel}");
    store.Add("scrutiny", -5, "smoke-test-revert");
}

public static void SmokeTestFlags()
{
    var store = Enlisted.Features.Flags.FlagStore.Instance;
    if (store == null)
    {
        SessionDiagnostics.LogEvent("Debug", "SmokeTestFlags", "FAIL: FlagStore.Instance null");
        return;
    }
    store.Set("smoke_test_flag", TaleWorlds.CampaignSystem.CampaignTime.DaysFromNow(1));
    var hasIt = store.Has("smoke_test_flag");
    store.Clear("smoke_test_flag");
    SessionDiagnostics.LogEvent("Debug", "SmokeTestFlags",
        hasIt ? "PASS: set+has+clear round-trip" : "FAIL: set immediately missing");
}

public static void SmokeTestScriptedEffects()
{
    var ids = Enlisted.Features.Content.ScriptedEffectRegistry.AllIds;
    var count = 0;
    foreach (var _ in ids) count++;
    SessionDiagnostics.LogEvent("Debug", "SmokeTestScriptedEffects",
        $"registry loaded {count} scripted effects (expected 23 from seed catalog)");
}

public static void SmokeTestTriggers()
{
    var ctx = new Enlisted.Features.Content.StoryletContext { CurrentContext = "land" };
    var trueTriggers = new System.Collections.Generic.List<string> { "context:land" };
    var falseTriggers = new System.Collections.Generic.List<string> { "context:sea" };
    var okTrue = Enlisted.Features.Content.TriggerRegistry.Evaluate(trueTriggers, ctx);
    var okFalse = !Enlisted.Features.Content.TriggerRegistry.Evaluate(falseTriggers, ctx);
    SessionDiagnostics.LogEvent("Debug", "SmokeTestTriggers",
        okTrue && okFalse ? "PASS: context:land true, context:sea false while on land"
                          : $"FAIL: trueOK={okTrue} falseOK={okFalse}");
}
```

- [x] **Step 2: Build**

```bash
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: 0 errors.

- [x] **Step 3: Commit**

```bash
cat > /tmp/debug-harness-commit.txt << 'EOF'
feat(debug): storylet backbone smoke-test helpers

Spec 0 Stage 12.1 — four static methods on DebugToolsBehavior
(SmokeTestQualities / SmokeTestFlags / SmokeTestScriptedEffects /
SmokeTestTriggers) log pass/fail to the session log. Surface specs
wire them to a debug submenu; for now they're callable from any
debug entry point the engineer wants.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add src/Debugging/Behaviors/DebugToolsBehavior.cs
git commit -F /tmp/debug-harness-commit.txt
rm /tmp/debug-harness-commit.txt
```

### Task 12.2: In-game smoke run + final verification

- [x] **Step 1: Close BannerlordLauncher.exe** (it holds the DLL open and breaks the post-build mirror with MSB3021).

- [x] **Step 2: Full rebuild**

```bash
dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64
```
Expected: `Build succeeded. 0 Error(s)`.

- [x] **Step 3: Run content validator**

```bash
python Tools/Validation/validate_content.py
```
Expected: all 12 phases pass.

- [x] **Step 4: Launch Bannerlord, start a new campaign, enlist with a lord, save, reload**

Expected session-log excerpt:
- No `E-PACE-*`, `E-QUALITY-*`, `E-FLAG-*`, `E-EFFECT-*`, `E-TRIGGER-*`, `E-SLOT-*`, `E-ACTIVITY-*`, `E-STORYLET-*` errors
- No SaveableType warnings
- Save/reload round-trips cleanly (under ~5ms typical per pacing-spec baseline)

- [x] **Step 5: Call the debug helpers via an existing debug entry point**

Use a breakpoint, a console helper, or any existing debug menu to call each `SmokeTest*` method. Confirm all four log `PASS` (or equivalent positive message).

- [x] **Step 6: Tag the commit**

```bash
git log --oneline | head -20
```
Expected: the full sequence of Spec-0 commits landed in order. No orphan changes.

- [x] **Step 7: Final commit — spec cross-links**

Add a "Status: complete" header to the spec document (top of the file):

```markdown
**Status:** Implementation complete. All 25 C# files landed, quality_defs.json + scripted_effects.json seeded, validate_content.py Phase 12 live, in-game smoke passed. Surface specs 1–5 may now author content against this substrate.
```

```bash
cat > /tmp/spec-status-commit.txt << 'EOF'
docs(spec): storylet backbone marked complete after Stage 12 smoke

All plumbing in; surface specs may now author content against it.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
git add docs/superpowers/specs/2026-04-19-storylet-backbone-design.md
git commit -F /tmp/spec-status-commit.txt
rm /tmp/spec-status-commit.txt
```

---

### ⬛ REVIEW CHECKPOINT I — Spec 0 complete

Sanity-check the commit trail, confirm all review checkpoints (A–I) passed, confirm no regressions in existing mod functionality. Spec 0 ships; surface specs 1–5 are unblocked.

---

## Self-review checklist

- [x] **Spec coverage:** every §16 file is covered by a Stage 1–10 task; §14 save registration is covered across Stages 4.3, 5.4, and 10.2; §17.2 integration scenarios map to Stage 12.2 in-game smoke; §17.1 unit-test list is realized as Stage 12.1 debug smoke helpers (the repo has no xUnit/NUnit harness, and the mod's existing testing culture is in-game debug helpers).
- [x] **Placeholder scan:** no TBD / TODO / "implement later" / "similar to Task N" / "write tests for the above" placeholders. Every step shows code or an exact command.
- [x] **Type consistency:** method names on `QualityStore` (`Get`, `GetForHero`, `Add`, `AddForHero`, `Set`) are consistent across Tasks 5.1, 5.3, 6.1, 12.1. `FlagStore` method names (`Has`, `HasForHero`, `Set`, `SetForHero`, `Clear`, `ClearForHero`, `PurgeExpired`, `PurgeMissingHeroes`) are consistent across Tasks 4.1, 4.2, 6.1, 12.1. `ScopeDecl.Context` / `Scope.Slots` names match §10.3 spec + §6.1 JSON. `ChainDecl.Id/Days/When` match §12.1 schema.
- [x] **Save-definer offsets:** 40 (QualityStore), 41 (QualityValue), 43 (FlagStore), 44 (Activity) — match spec §14. Enums 82 (QualityScope), 83 (ActivityEndReason) — match. All unused by existing code (verified via grep of current `EnlistedSaveDefiner.cs`).

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-19-storylet-backbone.md`. Two execution options:

**1. Subagent-Driven (recommended)** — dispatch a fresh subagent per task, two-stage review (spec + code quality) between tasks, fast iteration.

**2. Inline Execution** — execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints for review.

Which approach?
