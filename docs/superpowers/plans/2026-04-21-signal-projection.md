# Signal Projection Implementation Plan (Plan 3 of 5)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Integration context:** This is Plan 3 in the five-plan integration roadmap — see [docs/superpowers/specs/2026-04-21-plans-integration-design.md](../specs/2026-04-21-plans-integration-design.md). Plans 1-5: (1) Campaign Intelligence Backbone; (2) Lord AI Intervention; **(3) Signal Projection — this plan**; (4) Duty Opportunities; (5) Career Loop Closure. Plan 3 reads Plan 1's `EnlistedCampaignIntelligenceBehavior.Current` accessor and projects the snapshot into imperfect soldier-perspective signals for the news feed. Plan 3 ships independently of Plans 2/4/5 once Plan 1 lands.

**Goal:** Build an enlisted-only signal-projection layer that turns Plan 1's compact strategic snapshot into the imperfect, soldier-perspective signals the player actually experiences — rumors in the mess, quartermaster warnings, returning-scout reports, post-battle aftermath notices, silences that carry weight. Preserve the routing contract: every projected signal carries perspective, confidence, recency, change-type, and inference-intent so downstream consumers can interpret it faithfully.

**Architecture:** One `CampaignBehaviorBase` (`EnlistedSignalEmitterBehavior`) owns the hourly emission cadence, gated by `EnlistmentBehavior.Instance.IsEnlisted`. A pure `EnlistedCampaignSignalBuilder` projects Plan 1's snapshot + `RecentChangeFlags` into a `List<EnlistedCampaignSignal>` — one tick, one pass, no side effects. The emitter picks one candidate signal per tick (throttled by `OrdersNewsFeedThrottle.TryClaim()`), looks up a matching floor-storylet from the signal-family pool, and emits via `StoryDirector.EmitCandidate` at `StoryTier.Log`. `DailyDriftApplicator`'s existing flush path is rewired to route through `SignalBuilder` as a dedicated `scout_return` / `camp_talk` signal rather than posting a direct "Past few days: X" summary. `StoryCandidate` gains optional signal-metadata fields (source perspective, confidence, recency, change-type, inference, signal-type) for downstream consumers; existing emit sites remain valid. Cooldown tracking prevents the same floor-storylet firing twice within a configurable window; stored compactly in a new `SignalEmissionRecord` POCO persisted by the emitter's `SyncData`.

**Tech Stack:** C# 7.3 / .NET Framework 4.7.2, Mount & Blade II: Bannerlord `CampaignBehaviorBase` + `SaveableTypeDefiner` + `CampaignEvents` + Enlisted `ModLogger` / `EnlistmentBehavior` / `StoryDirector` / `OrdersNewsFeedThrottle` / storylet backbone (`StoryletCatalog`, `TriggerRegistry`, `EffectExecutor`).

**Scope boundary — what this plan does NOT do:**

- No duty opportunities, no Modal-tier player-interactive duty prompts. Reserved for Plan 4.
- No AI model wrappers, no Harmony patches against vanilla behavior loops. Reserved for Plan 2.
- No new snapshot fields or `RecentChangeFlags` additions. Plan 1 is frozen for Plan 3.
- No new Modal-tier story routing. All Plan 3 emissions are `StoryTier.Log`; StoryDirector routes them as non-modal news-feed entries.
- No path-commitment content, crossroads storylets, culture overlays, or lord-trait gates. Reserved for Plan 5.
- No calibration of signal-trigger thresholds beyond sensible seed values. Calibration is an iterative pass after smoke.

**Verification surface:** The realistic verification stack is `dotnet build` + `validate_content.py` + in-game smoke via `ModLogger` heartbeat traces. `EnlistedCampaignSignalBuilder` is a pure projector and is unit-testable (though the project has no test harness today; unit-test scaffolding is optional, not required). In-game smoke verifies the signal cadence: over a 14 in-game day run with the lord in varied strategic states (marching → besieging → raiding), the news feed shows varied signals across at least 6 of the 10 families, quiet campaigns produce silence-with-implication as the dominant family, and no signal contradicts live campaign state. Throttle suppression at 4× speed should yield ≤ 5 news-feed entries per real minute.

---

## Design decisions locked before task 1

### Tick cadence

- **Baseline: `CampaignEvents.HourlyTickEvent`.** One global tick per in-game hour. Handler reads Plan 1's `EnlistedCampaignIntelligenceBehavior.Current`, invokes `SignalBuilder.Build(snapshot)` to produce candidate signals, throttle-claims via `OrdersNewsFeedThrottle.TryClaim()`, and if claim succeeds, emits one signal via `StoryDirector.EmitCandidate(StoryTier.Log)`.
- **No event-driven invalidation.** Plan 1's `RecentChangeFlags` already carry the change-event semantics; SignalBuilder reads them each tick.
- **Why not daily cadence:** signal families like `scout_return`, `quartermaster_warning`, `aftermath_notice` are tied to recent state changes (hours old, not days). Hourly cadence preserves timeliness; `OrdersNewsFeedThrottle` enforces wall-clock pacing regardless of in-game speed.
- **Why not per-minute / per-frame:** wall-clock throttle at 20s + 4× suppression cap (existing `OrdersNewsFeedThrottle` semantics) already provides the pacing ceiling. Firing more than once per hour would compete with that ceiling without adding player-visible variety.

### Read-only accessor shape

No public accessor from Plan 3 itself — this plan is a producer, not a source of truth. The only "accessor" a downstream consumer might care about is the enriched `StoryCandidate` carried through `StoryDirector`. Backward compatibility: the new signal-metadata fields are optional and default to null / none; existing emit sites unaffected.

### Snapshot identity

Plan 3 reads `EnlistedCampaignIntelligenceBehavior.Current` (Plan 1's accessor) read-only. It never mutates the snapshot. It never caches the snapshot beyond a single tick. `SignalBuilder` takes the snapshot as a parameter; it does not look up the behavior statically.

### Save-definer offsets (claim in T1 after verifying free)

- Class offset: `49` — `SignalEmissionRecord` (compact cooldown state: `Dictionary<string, CampaignTime>` of storylet-id → last-fired-time).
- Enum offsets: `99` through `101` — `SignalType`, `SignalConfidence`, `SignalChangeType`.
- AGENTS.md Rule #11 was broadened 2026-04-21 to cover "closely-related surface-spec persistent state" — `SignalEmissionRecord` falls in that range. Offset 48 is Plan 1; 50-60 remain reserved for surface specs. 49 is the first free slot.
- T1 greps the definer to confirm no other branch claimed these offsets first.

### Per-signal cooldown

- Default cooldown per floor-storylet: 36 in-game hours. Storylets may override via `cooldown_days` (existing schema field, converted to hours at runtime by the emitter).
- Cooldown is wall-clock-adjacent — it measures game time (`CampaignTime.Now - lastFired`), not real time. `OrdersNewsFeedThrottle` handles the real-time axis.
- No global across-family cooldown; throttle handles global pacing.

### `StoryCandidate` field additions — backward compatibility

Plan 3 adds five optional fields to `StoryCandidate`:

- `SourcePerspective` (enum: `Narrator`, `Sergeant`, `Rumor`, `OwnObservation`, `Courier`, `Surgeon`, `Quartermaster`) — default `Narrator`.
- `SignalConfidence` (enum: `Low`, `Medium`, `High`) — default `Medium`.
- `SignalRecency` (enum: `Immediate`, `RecentHours`, `RecentDays`, `Stale`) — default `Immediate`.
- `SignalChangeType` (enum: `ObjectiveShift`, `NewThreat`, `SiegeTransition`, `DispersalRisk`, `ConfirmedScoutInfo`, `SupplyBreak`, `PrisonerUpdate`, `None`) — default `None`.
- `SignalInference` (short string, ≤ 80 chars) — default null — human-authored short hint describing what the player is expected to infer or do.

These additions are purely additive; existing emit sites default the fields. `StoryCandidatePersistent` is NOT extended — the signal-metadata block lives on transient `StoryCandidate` only (see integration spec §5.2 + design spec §10.4).

### Floor-storylet pool pattern

- 10 signal families = 10 `floor_<family>.json` files under `ModuleData/Enlisted/Storylets/`, following existing storylet-backbone schema (see `src/Features/Content/StoryletCatalog.cs` parser).
- ~5 storylets per family = 50 total. Small families (`silence_with_implication`) may have as few as 3; high-variance families (`camp_talk`, `rumor`) may have 6-8.
- Each storylet uses `profile_requires` (optional — some families are cross-profile) + `trigger` (list; `is_enlisted` + snapshot-gate predicate).
- Snapshot-gate predicates are the same ones Plan 4 adds to `TriggerRegistry` (Phase B.5 of Plan 4). **Plan 3 declares those predicates as a dependency on Plan 4's trigger extensions.** If Plan 3 ships before Plan 4's Phase B.5, authored floor storylets may use placeholder gates (e.g. `quality_gte:scrutiny:30` on existing quality) until Plan 4's gates land.

### Single-option floor storylets

Floor storylets have exactly one option (id `"main"`) with auto-applied effects. No multi-option prompts at `StoryTier.Log` — that's a Modal pattern reserved for Plan 4. Effects are typically light: small `quality_add` nudges to `scrutiny` / `supplies` / `readiness`, or simply null effects where the signal is pure texture.

### Rewiring DailyDriftApplicator

- `DailyDriftApplicator.FlushSummary` currently emits `StoryTier.Log` with body `"Past few days: Bow +45, Athletics +25 XP."`. This direct-emit site is replaced by a call to `SignalBuilder.BuildDriftSummarySignal(activity)` → `SignalBuilder` produces an `EnlistedCampaignSignal` of type `CampTalk` or `ScoutReturn` (depending on the player's current profile) with body text similar to today's but wrapped in signal metadata.
- The XP accumulation itself (Phase A+B Applicator) is unchanged. Only the emission route changes.

---

## File structure

```
src/Features/CampaignIntelligence/Signals/
  EnlistedCampaignSignal.cs               — [Serializable-but-transient] POCO; produced by Builder, consumed by Emitter
  SignalType.cs                            — enum: 10 signal families
  SignalConfidence.cs                      — enum: Low/Medium/High
  SignalChangeType.cs                      — enum: mirrors Plan 1's RecentChangeFlags
  EnlistedCampaignSignalBuilder.cs         — static: snapshot + flags → List<EnlistedCampaignSignal>
  SignalEmissionRecord.cs                  — [Serializable] POCO: per-storylet cooldown state
  EnlistedSignalEmitterBehavior.cs         — CampaignBehaviorBase: hourly tick + emission + cooldown

src/Features/Content/ (modified)
  StoryCandidate.cs                         — add 5 optional signal-metadata fields

src/Features/Activities/Orders/ (modified)
  DailyDriftApplicator.cs                   — rewire FlushSummary emission to go through SignalBuilder

ModuleData/Enlisted/Storylets/
  floor_rumor.json                          — ~5 storylets
  floor_camp_talk.json                      — ~6 storylets
  floor_dispatch.json                       — ~5 storylets
  floor_order_shift.json                    — ~5 storylets
  floor_quartermaster_warning.json          — ~5 storylets
  floor_scout_return.json                   — ~5 storylets
  floor_prisoner_report.json                — ~4 storylets
  floor_aftermath_notice.json               — ~5 storylets
  floor_tension_incident.json               — ~5 storylets
  floor_silence_with_implication.json       — ~3 storylets
```

Seven new C# files, two modified. 50 authored storylets. The SignalBuilder is the ONLY file that projects snapshot → signal; the Emitter is the ONLY file that mutates cooldown state or calls StoryDirector.

---

## Task index

- **Phase 0 — Save-definer + csproj claim**: T1
- **Phase A — Type foundation**: T2, T3, T4
- **Phase B — Signal builder (pure)**: T5, T6, T7
- **Phase C — StoryCandidate field additions**: T8, T9
- **Phase D — Emitter behavior + cooldown**: T10, T11, T12, T13
- **Phase E — DailyDriftApplicator rewire**: T14, T15
- **Phase F — Authored floor storylets (10 families)**: T16-T25 (one task per family)
- **Phase G — Observability + error handling**: T26, T27
- **Phase H — Smoke verification**: T28, T29

Total: 29 tasks.

---

## Phase 0 — Save-definer + csproj claim

### Task T1: Verify + claim save-definer offsets + csproj entries

**Files:**
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Confirm offsets are free**

Grep pattern: `AddClassDefinition\(typeof\([^)]+\), 49\)|AddEnumDefinition\(typeof\([^)]+\), (99|100|101)\)` — expected zero matches. If any match, STOP and revise offsets up. AGENTS.md Rule #15 applies.

- [ ] **Step 2: Add class-definition placeholder**

Append to `DefineClassTypes()` at the tail (after Plan 1's offset 48 claim):

```csharp
// Signal Projection (Plan 3 of 5) — offset 49
AddClassDefinition(typeof(Features.CampaignIntelligence.Signals.SignalEmissionRecord), 49);
```

- [ ] **Step 3: Add enum-definition placeholders**

Append to `DefineEnumTypes()`:

```csharp
// Signal Projection enums (Plan 3 of 5) — offsets 99-101
AddEnumDefinition(typeof(Features.CampaignIntelligence.Signals.SignalType), 99);
AddEnumDefinition(typeof(Features.CampaignIntelligence.Signals.SignalConfidence), 100);
AddEnumDefinition(typeof(Features.CampaignIntelligence.Signals.SignalChangeType), 101);
```

- [ ] **Step 4: Add csproj `<Compile Include>` entries**

Add a new block near the CampaignIntelligence block Plan 1 added. Alphabetically keep `Signals/` after the Plan 1 root files.

```xml
    <Compile Include="src\Features\CampaignIntelligence\Signals\EnlistedCampaignSignal.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Signals\SignalType.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Signals\SignalConfidence.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Signals\SignalChangeType.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Signals\EnlistedCampaignSignalBuilder.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Signals\SignalEmissionRecord.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Signals\EnlistedSignalEmitterBehavior.cs"/>
```

- [ ] **Step 5: Build — expect compile failure**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: FAIL. The type names don't exist yet. T2-T4 satisfy the types.

- [ ] **Step 6: Commit**

```
feat(signals): claim save-definer offset 49 + enum offsets 99-101 for Plan 3

Reserves class offset 49 (SignalEmissionRecord) and enum offsets
99-101 (SignalType / SignalConfidence / SignalChangeType) for the
signal-projection layer. Offset 48 is Plan 1 (Intelligence
snapshot); 50-60 remain reserved for surface specs. Build currently
fails — types land in T2-T4. Part of Plan 3 of 5 (Signal Projection).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase A — Type foundation

### Task T2: Create `SignalType.cs` + `SignalConfidence.cs` + `SignalChangeType.cs`

**Files:**
- Create: `src/Features/CampaignIntelligence/Signals/SignalType.cs`
- Create: `src/Features/CampaignIntelligence/Signals/SignalConfidence.cs`
- Create: `src/Features/CampaignIntelligence/Signals/SignalChangeType.cs`

- [ ] **Step 1: Write SignalType.cs**

```csharp
using System;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// The ten signal families this layer projects. Each family pairs with a
    /// floor_&lt;family&gt;.json storylet pool. Rendering in the news feed
    /// varies by family (header text, icon, voice).
    /// </summary>
    public enum SignalType : byte
    {
        Rumor,
        CampTalk,
        Dispatch,
        OrderShift,
        QuartermasterWarning,
        ScoutReturn,
        PrisonerReport,
        AftermathNotice,
        TensionIncident,
        SilenceWithImplication
    }
}
```

- [ ] **Step 2: Write SignalConfidence.cs**

```csharp
using System;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// How much reliable signal the source has. Low = rumor-heavy
    /// speculation; High = confirmed observation or received order.
    /// </summary>
    public enum SignalConfidence : byte
    {
        Low,
        Medium,
        High
    }
}
```

- [ ] **Step 3: Write SignalChangeType.cs**

```csharp
using System;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// The change event the signal is projecting — mirrors Plan 1's
    /// RecentChangeFlags subset. None means the signal is ambient
    /// (not tied to a recent change).
    /// </summary>
    public enum SignalChangeType : byte
    {
        None,
        ObjectiveShift,
        NewThreat,
        SiegeTransition,
        DispersalRisk,
        ConfirmedScoutInfo,
        SupplyBreak,
        PrisonerUpdate
    }
}
```

- [ ] **Step 4: CRLF normalize the three new files**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Signals/SignalType.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Signals/SignalConfidence.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Signals/SignalChangeType.cs
```

- [ ] **Step 5: Commit**

```
feat(signals): add SignalType / SignalConfidence / SignalChangeType enums

Three enums for Plan 3's signal projection layer. SignalType names
the 10 signal families (rumor / camp_talk / dispatch / order_shift
/ quartermaster_warning / scout_return / prisoner_report /
aftermath_notice / tension_incident / silence_with_implication).
SignalConfidence is low/medium/high. SignalChangeType mirrors the
subset of Plan 1's RecentChangeFlags that produce signals.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T3: Create `EnlistedCampaignSignal.cs` POCO

**Files:**
- Create: `src/Features/CampaignIntelligence/Signals/EnlistedCampaignSignal.cs`

- [ ] **Step 1: Write the POCO**

```csharp
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// A projected signal produced by EnlistedCampaignSignalBuilder.
    /// Transient — not persisted. The emitter consumes it, picks a matching
    /// floor-storylet from the pool, and forwards rendering through
    /// StoryDirector.EmitCandidate at StoryTier.Log.
    /// </summary>
    public sealed class EnlistedCampaignSignal
    {
        public SignalType Type { get; set; }
        public SignalConfidence Confidence { get; set; } = SignalConfidence.Medium;
        public SignalChangeType ChangeType { get; set; } = SignalChangeType.None;

        /// <summary>
        /// Short string identifying which authored floor-storylet family this
        /// signal maps to. For most families this is `floor_&lt;familyName&gt;_`
        /// and the emitter greps StoryletCatalog by that prefix.
        /// </summary>
        public string PoolPrefix { get; set; } = string.Empty;

        /// <summary>
        /// CampaignTime of the change event that triggered the signal, or
        /// CampaignTime.Now for ambient signals with no specific triggering event.
        /// Used for recency classification.
        /// </summary>
        public CampaignTime AsOf { get; set; } = CampaignTime.Zero;

        /// <summary>
        /// Optional — if set, overrides the default per-family source perspective.
        /// </summary>
        public string SourcePerspectiveOverride { get; set; }

        /// <summary>
        /// Optional — if set, a short (≤80 char) inference hint authored on the
        /// signal itself rather than on the picked storylet.
        /// </summary>
        public string InferenceOverride { get; set; }
    }
}
```

- [ ] **Step 2: CRLF normalize**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Signals/EnlistedCampaignSignal.cs
```

- [ ] **Step 3: Commit**

```
feat(signals): add EnlistedCampaignSignal transient POCO

Produced by SignalBuilder, consumed by the emitter behavior. Not
persisted. Holds type / confidence / change-type / pool-prefix /
as-of timestamp + optional perspective and inference overrides.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T4: Create `SignalEmissionRecord.cs` (cooldown store POCO)

**Files:**
- Create: `src/Features/CampaignIntelligence/Signals/SignalEmissionRecord.cs`

- [ ] **Step 1: Write the POCO**

```csharp
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// Persistent per-storylet cooldown state for the signal emitter. Maps
    /// storylet id → last-fired CampaignTime. Cleared when enlistment ends
    /// (the emitter clears its instance on OnEnlistmentEnded).
    /// </summary>
    [SaveableClass(735049)]
    [Serializable]
    public sealed class SignalEmissionRecord
    {
        [SaveableField(1)]
        public Dictionary<string, CampaignTime> LastFiredAt { get; set; }
            = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reseats null dictionary after deserialization paths that skip ctors.
        /// See CLAUDE.md pitfall on [Serializable] + SyncData null-dict crashes.
        /// </summary>
        public void EnsureInitialized()
        {
            if (LastFiredAt == null)
            {
                LastFiredAt = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
```

VERIFY IN DECOMPILE: the `[SaveableClass(...)]` attribute pattern. Existing Enlisted save types do NOT use this attribute — they use the base-id + offset scheme at `EnlistedSaveDefiner.AddClassDefinition(typeof(X), offset)`. Remove the `[SaveableClass]` attribute if it conflicts with the definer registration. The definer path (offset 49 registered in T1) is sufficient.

- [ ] **Step 2: Remove `[SaveableClass]` attribute** — the definer is the registration source; attributes are not used in this codebase. Keep only `[Serializable]`.

- [ ] **Step 3: Remove `[SaveableField]` attributes** for the same reason. Field serialization is driven by `[Serializable]` + the save definer's `ConstructContainerDefinition` registrations — no per-field attribute needed.

- [ ] **Step 4: Verify container registration**

`Dictionary<string, CampaignTime>` is already registered in `EnlistedSaveDefiner.DefineContainerDefinitions()` (spec 0 backbone block — see `EnlistedSaveDefiner.cs:136`). No new `ConstructContainerDefinition` call needed.

- [ ] **Step 5: CRLF normalize + commit**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/Signals/SignalEmissionRecord.cs
```

```
feat(signals): add SignalEmissionRecord persistent cooldown POCO

Persistent per-storylet last-fired timestamps for the signal
emitter's cooldown logic. Serialized via the definer's offset 49
claim (no per-class/per-field attributes — Enlisted save convention
uses definer registration only). EnsureInitialized reseats null
dictionaries on deserialization paths that skip ctors.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase B — Signal builder (pure)

### Task T5: Design the snapshot → signal mapping table

**Files:**
- Create: `docs/Features/CampaignIntelligence/signal-projection-mapping.md` (reference doc, not code)

- [ ] **Step 1: Author the mapping table**

Document which snapshot fields + RecentChangeFlags produce which signal families, with example inference and default confidence. This is a living reference for both SignalBuilder implementation (T6) and floor-storylet authoring (T16-T25). Sample rows:

| Trigger (snapshot state or flag) | Signal family | Default confidence | Example inference |
|---|---|---|---|
| `SupplyPressure >= Strained` | QuartermasterWarning | Medium | "Stores may need to move sooner than expected." |
| `ArmyStrain >= High AND not in settlement` | TensionIncident | Medium | "The men are restless at the campfires." |
| `FrontPressure >= High` | Rumor | Low | "Enemy riders seen to the east — unconfirmed." |
| `RecentChangeFlags has SiegeTransition` | Dispatch | High | "The army marches; camp breaks by dawn." |
| `RecentChangeFlags has ConfirmedScoutInfo` | ScoutReturn | High | "Scouts returned; numbers confirmed." |
| `PrisonerStakes >= Medium AND RecentChangeFlags has PrisonerUpdate` | PrisonerReport | Medium | "Word on the prisoner — not all good." |
| `SupplyPressure == None AND ArmyStrain == None AND FrontPressure == None` | SilenceWithImplication | Low | (no inference text — the silence IS the point) |
| `RecentChangeFlags has ObjectiveShift` | OrderShift | High | "New orders came down; old plans scrapped." |
| `RecentChangeFlags has NewThreat` | AftermathNotice | Medium | "Trouble on the road — some didn't make it back." |
| `RecoveryNeed >= High` | CampTalk | Low | "Talk in the mess: how much longer?" |

- [ ] **Step 2: Document default source perspective per family**

| Family | Default source perspective |
|---|---|
| Rumor | Rumor |
| CampTalk | OwnObservation |
| Dispatch | Courier |
| OrderShift | Sergeant |
| QuartermasterWarning | Quartermaster |
| ScoutReturn | Narrator (describing the scout's return) |
| PrisonerReport | Narrator |
| AftermathNotice | Narrator |
| TensionIncident | OwnObservation |
| SilenceWithImplication | Narrator |

- [ ] **Step 3: Commit**

```
docs(signals): add signal-projection mapping reference

Living reference mapping Plan 1 snapshot state and RecentChangeFlags
bits to the 10 signal families. Defines default confidence and
source perspective per family. Used by SignalBuilder (T6) and by
floor-storylet authors (T16-T25) to keep content aligned with the
snapshot-to-signal contract.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T6: Implement `EnlistedCampaignSignalBuilder.cs` (pure projector)

**Files:**
- Create: `src/Features/CampaignIntelligence/Signals/EnlistedCampaignSignalBuilder.cs`

- [ ] **Step 1: Write the static projector**

```csharp
using System.Collections.Generic;
using Enlisted.Features.CampaignIntelligence;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// Pure projector — reads a Plan 1 snapshot and returns candidate signals.
    /// No side effects, no campaign reads, no statics. Unit-testable.
    /// The emitter behavior picks one signal per tick from this list.
    /// </summary>
    public static class EnlistedCampaignSignalBuilder
    {
        /// <summary>
        /// Projects the snapshot into candidate signals. Returns an empty list
        /// if the snapshot is null or the enlisted-gate isn't satisfied at the
        /// call site (caller's responsibility). The order of the returned list
        /// is NOT prioritized — emitter applies its own picker.
        /// </summary>
        public static List<EnlistedCampaignSignal> Build(EnlistedLordIntelligenceSnapshot snapshot)
        {
            var signals = new List<EnlistedCampaignSignal>();
            if (snapshot == null)
            {
                return signals;
            }

            // Change-driven signals (RecentChangeFlags). These fire on the
            // specific change event and are higher-confidence by default.
            if (snapshot.RecentChangeFlags.HasFlag(RecentChangeFlags.ObjectiveShift))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.OrderShift,
                    Confidence = SignalConfidence.High,
                    ChangeType = SignalChangeType.ObjectiveShift,
                    PoolPrefix = "floor_order_shift_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChangeFlags.HasFlag(RecentChangeFlags.SiegeTransition))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.Dispatch,
                    Confidence = SignalConfidence.High,
                    ChangeType = SignalChangeType.SiegeTransition,
                    PoolPrefix = "floor_dispatch_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChangeFlags.HasFlag(RecentChangeFlags.ConfirmedScoutInfo))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.ScoutReturn,
                    Confidence = SignalConfidence.High,
                    ChangeType = SignalChangeType.ConfirmedScoutInfo,
                    PoolPrefix = "floor_scout_return_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChangeFlags.HasFlag(RecentChangeFlags.NewThreat))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.AftermathNotice,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.NewThreat,
                    PoolPrefix = "floor_aftermath_notice_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChangeFlags.HasFlag(RecentChangeFlags.DispersalRisk))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.Dispatch,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.DispersalRisk,
                    PoolPrefix = "floor_dispatch_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChangeFlags.HasFlag(RecentChangeFlags.SupplyBreak))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.QuartermasterWarning,
                    Confidence = SignalConfidence.High,
                    ChangeType = SignalChangeType.SupplyBreak,
                    PoolPrefix = "floor_quartermaster_warning_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecentChangeFlags.HasFlag(RecentChangeFlags.PrisonerUpdate))
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.PrisonerReport,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.PrisonerUpdate,
                    PoolPrefix = "floor_prisoner_report_",
                    AsOf = CampaignTime.Now
                });
            }

            // State-driven signals (ambient; no change event). Lower-confidence,
            // contextually gated.
            if (snapshot.SupplyPressure >= SupplyPressure.Strained)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.QuartermasterWarning,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_quartermaster_warning_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.ArmyStrain >= ArmyStrainLevel.High)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.TensionIncident,
                    Confidence = SignalConfidence.Medium,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_tension_incident_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.FrontPressure >= FrontPressure.High)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.Rumor,
                    Confidence = SignalConfidence.Low,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_rumor_",
                    AsOf = CampaignTime.Now
                });
            }

            if (snapshot.RecoveryNeed >= RecoveryNeed.High)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.CampTalk,
                    Confidence = SignalConfidence.Low,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_camp_talk_",
                    AsOf = CampaignTime.Now
                });
            }

            // Quiet-stretch fallback — all strain axes calm AND no recent
            // changes → emit a silence-with-implication signal.
            if (signals.Count == 0
                && snapshot.SupplyPressure == SupplyPressure.None
                && snapshot.ArmyStrain == ArmyStrainLevel.None
                && snapshot.FrontPressure == FrontPressure.None
                && snapshot.RecentChangeFlags == RecentChangeFlags.None)
            {
                signals.Add(new EnlistedCampaignSignal
                {
                    Type = SignalType.SilenceWithImplication,
                    Confidence = SignalConfidence.Low,
                    ChangeType = SignalChangeType.None,
                    PoolPrefix = "floor_silence_with_implication_",
                    AsOf = CampaignTime.Now
                });
            }

            return signals;
        }

        /// <summary>
        /// Builds a drift-summary signal for DailyDriftApplicator's flush path.
        /// Takes the formatted summary text; SignalBuilder decides which family
        /// to render through based on the current profile.
        /// </summary>
        public static EnlistedCampaignSignal BuildDriftSummarySignal(
            string summaryBody,
            string currentDutyProfile)
        {
            // Marching / scouting / wandering feel like "scout return" summary.
            // Garrisoned / besieging / raiding / escorting feel like "camp talk."
            var isMovementProfile = currentDutyProfile == "marching"
                || currentDutyProfile == "wandering";

            return new EnlistedCampaignSignal
            {
                Type = isMovementProfile ? SignalType.ScoutReturn : SignalType.CampTalk,
                Confidence = SignalConfidence.Medium,
                ChangeType = SignalChangeType.None,
                PoolPrefix = isMovementProfile ? "floor_scout_return_" : "floor_camp_talk_",
                AsOf = CampaignTime.Now,
                InferenceOverride = summaryBody // short XP-summary text, already formatted
            };
        }
    }
}
```

VERIFY IN DECOMPILE: none — SignalBuilder uses Plan 1's types and `CampaignTime.Now`. Plan 1 owns its enum names (see Plan 1 T2); Plan 3 implementation will match.

- [ ] **Step 2: CRLF normalize + commit**

```
feat(signals): implement EnlistedCampaignSignalBuilder pure projector

Pure static: snapshot + RecentChangeFlags → List<EnlistedCampaignSignal>.
No campaign reads, no side effects, unit-testable. Change-driven
signals (7 families) fire on specific RecentChangeFlag bits with
high/medium confidence. State-driven signals (4 families) fire on
SupplyPressure/ArmyStrain/FrontPressure/RecoveryNeed thresholds with
lower confidence. Quiet-stretch fallback emits SilenceWithImplication
when all axes are calm. BuildDriftSummarySignal re-expresses the
daily XP summary as a ScoutReturn (movement profiles) or CampTalk
(camp profiles) signal for the rewired DailyDriftApplicator flush.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T7: Build + validator clean

- [ ] **Step 1: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: **PASS** — all six Phase A/B files compile, the definer offset claim resolves, no behavior registered yet.

- [ ] **Step 2: Validator clean**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: clean. Phase B adds no content files; no new validator phase coverage required yet.

- [ ] **Step 3: No commit** — this is a verification step.

---

## Phase C — `StoryCandidate` field additions

### Task T8: Extend `StoryCandidate` with signal-metadata fields

**Files:**
- Modify: `src/Features/Content/StoryCandidate.cs`

- [ ] **Step 1: Read existing `StoryCandidate.cs`**

Identify the existing field set. Note where SourceId / CategoryId / ProposedTier / Beats / Relevance / InteractiveEvent / RenderedTitle / RenderedBody / StoryKey / ChainContinuation / EmittedAt live.

- [ ] **Step 2: Add five optional fields**

Append the following fields. Each is optional; existing emit sites leave them at defaults.

```csharp
// Plan 3 signal-metadata block — optional, defaults preserve existing behavior.
public SourcePerspective SourcePerspective { get; set; } = SourcePerspective.Narrator;
public SignalConfidence SignalConfidence { get; set; } = SignalConfidence.Medium;
public SignalRecency SignalRecency { get; set; } = SignalRecency.Immediate;
public SignalChangeType SignalChangeType { get; set; } = SignalChangeType.None;

/// <summary>
/// Optional short (&lt;= 80 char) hint describing what the player is expected
/// to infer or do from this signal. Null = no hint.
/// </summary>
public string SignalInference { get; set; }
```

- [ ] **Step 3: Add `using` for the enums**

```csharp
using Enlisted.Features.CampaignIntelligence.Signals;
```

- [ ] **Step 4: Create `SourcePerspective.cs` enum**

Create `src/Features/Content/SourcePerspective.cs`:

```csharp
namespace Enlisted.Features.Content
{
    /// <summary>
    /// Who is speaking / observing in the projected signal. Determines
    /// rendering voice in the news feed. Plan 3 signal-projection layer
    /// populates this; emit sites outside Plan 3 default to Narrator.
    /// </summary>
    public enum SourcePerspective : byte
    {
        Narrator,
        Sergeant,
        Rumor,
        OwnObservation,
        Courier,
        Surgeon,
        Quartermaster
    }
}
```

- [ ] **Step 5: Create `SignalRecency.cs` enum**

Create `src/Features/Content/SignalRecency.cs`:

```csharp
namespace Enlisted.Features.Content
{
    /// <summary>
    /// How old the signal's underlying information is from the player's
    /// perspective. Rendering UI may use this to add "stale" / "late"
    /// qualifiers to the signal body.
    /// </summary>
    public enum SignalRecency : byte
    {
        Immediate,
        RecentHours,
        RecentDays,
        Stale
    }
}
```

- [ ] **Step 6: csproj entries + save-definer enum offsets**

Add to Enlisted.csproj:

```xml
    <Compile Include="src\Features\Content\SourcePerspective.cs"/>
    <Compile Include="src\Features\Content\SignalRecency.cs"/>
```

Add to `EnlistedSaveDefiner.DefineEnumTypes()` (continuing from 101):

```csharp
// StoryCandidate signal-metadata enums (Plan 3 of 5) — offsets 102-103
AddEnumDefinition(typeof(Features.Content.SourcePerspective), 102);
AddEnumDefinition(typeof(Features.Content.SignalRecency), 103);
```

- [ ] **Step 7: CRLF normalize new files**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/SourcePerspective.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/SignalRecency.cs
```

- [ ] **Step 8: Commit**

```
feat(signals): extend StoryCandidate with signal-metadata fields

Adds five optional fields to StoryCandidate: SourcePerspective,
SignalConfidence, SignalRecency, SignalChangeType, SignalInference.
Defaults preserve existing-emit-site behavior (Narrator / Medium /
Immediate / None / null). Plan 3 SignalBuilder + Emitter populate
these when routing signals. Adds SourcePerspective and SignalRecency
enums in src/Features/Content/. StoryCandidatePersistent is NOT
extended — signal metadata lives on transient StoryCandidate only
(integration spec §5.2).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T9: Build + verify existing emit sites unaffected

- [ ] **Step 1: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: PASS. Existing emit sites in StoryDirector, DailyDriftApplicator, EnlistedNewsBehavior, etc. compile without modification because all new fields have defaults.

- [ ] **Step 2: Grep-scan emit sites**

Confirm no emit site is passing unexpected positional arguments that could be reassigned by the new fields (should be none — all emit sites use object-initializer syntax).

```bash
grep -rn "new StoryCandidate" src/ | head -20
```

- [ ] **Step 3: No commit** — verification only.

---

## Phase D — Emitter behavior + cooldown

### Task T10: Create `EnlistedSignalEmitterBehavior.cs` skeleton

**Files:**
- Create: `src/Features/CampaignIntelligence/Signals/EnlistedSignalEmitterBehavior.cs`

- [ ] **Step 1: Write the behavior skeleton**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Signals
{
    /// <summary>
    /// Hourly-tick emitter for Plan 3 signal projection. Gated by
    /// EnlistmentBehavior.IsEnlisted. Reads the Plan 1 snapshot each tick,
    /// invokes SignalBuilder, throttle-claims via OrdersNewsFeedThrottle,
    /// picks a floor-storylet from the signal family's pool, emits via
    /// StoryDirector.EmitCandidate at StoryTier.Log. Cooldown state
    /// persists across save/load in SignalEmissionRecord.
    /// </summary>
    public sealed class EnlistedSignalEmitterBehavior : CampaignBehaviorBase
    {
        public static EnlistedSignalEmitterBehavior Instance { get; private set; }

        private SignalEmissionRecord _record = new SignalEmissionRecord();
        private int _lastHeartbeatHourTick = int.MinValue / 2;

        private const int HEARTBEAT_INTERVAL_HOURS = 12;
        private const int DEFAULT_COOLDOWN_HOURS = 36;

        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                _ = dataStore.SyncData("_signalRecord", ref _record);
                _record = _record ?? new SignalEmissionRecord();
                _record.EnsureInitialized();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SIGNAL", "SyncData failed", ex);
                _record = new SignalEmissionRecord();
            }
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _record = _record ?? new SignalEmissionRecord();
            _record.EnsureInitialized();
        }

        private void OnHourlyTick()
        {
            try
            {
                LogHeartbeatIfDue();

                if (EnlistmentBehavior.Instance?.IsEnlisted != true)
                {
                    return;
                }

                var snapshot = EnlistedCampaignIntelligenceBehavior.Instance?.Current;
                if (snapshot == null)
                {
                    return;
                }

                var candidates = EnlistedCampaignSignalBuilder.Build(snapshot);
                if (candidates == null || candidates.Count == 0)
                {
                    return;
                }

                // Throttle BEFORE doing content lookup — avoids wasted grep work
                // when we know we can't emit anyway.
                if (!OrdersNewsFeedThrottle.TryClaim())
                {
                    return;
                }

                // Pick one candidate. Simple strategy: first change-driven signal
                // (high priority), else first state-driven signal. Prevents
                // overlapping floods when many candidates are present.
                var picked = candidates[0];

                var storyletId = PickStoryletFromPool(picked.PoolPrefix);
                if (storyletId == null)
                {
                    ModLogger.Expected("SIGNAL", "no_storylet_for_family",
                        $"SignalBuilder produced signal {picked.Type} but floor-pool {picked.PoolPrefix}* had no eligible storylet (cooldown or empty)");
                    return;
                }

                EmitPicked(picked, storyletId);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("SIGNAL", "OnHourlyTick threw", ex);
            }
        }

        private string PickStoryletFromPool(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return null;
            }

            var nowHours = (int)CampaignTime.Now.ToHours;

            // Grep StoryletCatalog.All for matching prefix + cooldown-available +
            // trigger-satisfied. Simple first-match strategy; a future iteration
            // could weight by weight.base + modifiers.
            foreach (var s in Content.StoryletCatalog.All)
            {
                if (s?.Id == null || !s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_record.LastFiredAt.TryGetValue(s.Id, out var last))
                {
                    var cooldownHours = s.CooldownDays > 0 ? s.CooldownDays * 24 : DEFAULT_COOLDOWN_HOURS;
                    if (nowHours - (int)last.ToHours < cooldownHours)
                    {
                        continue; // on cooldown
                    }
                }

                // Trigger evaluation (TriggerRegistry) — gates on `is_enlisted` + any
                // snapshot-state triggers. TriggerRegistry existing grammar is used;
                // Plan 4 Phase B.5 adds snapshot-backed predicates.
                if (!Content.TriggerRegistry.EvaluateAll(s.Trigger))
                {
                    continue;
                }

                return s.Id;
            }

            return null;
        }

        private void EmitPicked(EnlistedCampaignSignal signal, string storyletId)
        {
            var storylet = Content.StoryletCatalog.GetById(storyletId);
            if (storylet == null)
            {
                return;
            }

            // Default source perspective from signal type (overridable by signal).
            var perspective = signal.SourcePerspectiveOverride != null
                ? ParsePerspective(signal.SourcePerspectiveOverride)
                : DefaultPerspectiveFor(signal.Type);

            StoryDirector.Instance?.EmitCandidate(new StoryCandidate
            {
                SourceId = "signal." + signal.Type.ToString().ToLowerInvariant(),
                CategoryId = "signal." + signal.Type.ToString().ToLowerInvariant(),
                ProposedTier = StoryTier.Log,
                EmittedAt = CampaignTime.Now,
                RenderedTitle = storylet.Title,
                RenderedBody = storylet.Setup,
                StoryKey = storyletId,
                SourcePerspective = perspective,
                SignalConfidence = signal.Confidence,
                SignalRecency = ClassifyRecency(signal.AsOf),
                SignalChangeType = signal.ChangeType,
                SignalInference = signal.InferenceOverride
            });

            _record.LastFiredAt[storyletId] = CampaignTime.Now;

            // Apply single-option effects auto (floor storylets are single-option
            // Log-tier texture with light effects).
            Content.EffectExecutor.ExecuteOptionEffects(storylet, "main", null);

            ModLogger.Info("SIGNAL", $"emitted {signal.Type} storylet={storyletId} confidence={signal.Confidence}");
        }

        private SourcePerspective DefaultPerspectiveFor(SignalType type)
        {
            switch (type)
            {
                case SignalType.Rumor: return SourcePerspective.Rumor;
                case SignalType.CampTalk: return SourcePerspective.OwnObservation;
                case SignalType.Dispatch: return SourcePerspective.Courier;
                case SignalType.OrderShift: return SourcePerspective.Sergeant;
                case SignalType.QuartermasterWarning: return SourcePerspective.Quartermaster;
                case SignalType.ScoutReturn: return SourcePerspective.Narrator;
                case SignalType.PrisonerReport: return SourcePerspective.Narrator;
                case SignalType.AftermathNotice: return SourcePerspective.Narrator;
                case SignalType.TensionIncident: return SourcePerspective.OwnObservation;
                case SignalType.SilenceWithImplication: return SourcePerspective.Narrator;
                default: return SourcePerspective.Narrator;
            }
        }

        private SignalRecency ClassifyRecency(CampaignTime asOf)
        {
            var hours = (CampaignTime.Now - asOf).ToHours;
            if (hours < 2) return SignalRecency.Immediate;
            if (hours < 24) return SignalRecency.RecentHours;
            if (hours < 72) return SignalRecency.RecentDays;
            return SignalRecency.Stale;
        }

        private SourcePerspective ParsePerspective(string s)
        {
            return Enum.TryParse<SourcePerspective>(s, true, out var p) ? p : SourcePerspective.Narrator;
        }

        private void LogHeartbeatIfDue()
        {
            var nowHour = (int)CampaignTime.Now.ToHours;
            if (nowHour - _lastHeartbeatHourTick < HEARTBEAT_INTERVAL_HOURS)
            {
                return;
            }
            _lastHeartbeatHourTick = nowHour;

            var isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted == true;
            var snapCount = EnlistedCampaignIntelligenceBehavior.Instance?.Current != null ? 1 : 0;
            var trackedStorylets = _record?.LastFiredAt?.Count ?? 0;
            ModLogger.Info("SIGNAL", $"heartbeat: enlisted={isEnlisted} snapshot_present={snapCount} tracked={trackedStorylets}");
        }
    }
}
```

VERIFY IN DECOMPILE: `Content.EffectExecutor.ExecuteOptionEffects` method signature and whether it accepts a null context for floor-storylet single-option auto-apply. If the signature differs, adapt or use `EffectExecutor.Execute` / `EffectExecutor.ExecuteEffects` as appropriate based on the actual public API in `src/Features/Content/EffectExecutor.cs`.

- [ ] **Step 2: CRLF normalize + commit**

```
feat(signals): add EnlistedSignalEmitterBehavior skeleton

Hourly-tick behavior gated by IsEnlisted. Reads Plan 1 snapshot,
invokes SignalBuilder, throttles via OrdersNewsFeedThrottle, picks
floor-storylet from signal family's pool (first-match,
cooldown-respecting), emits via StoryDirector.EmitCandidate at
StoryTier.Log. Cooldown state in SignalEmissionRecord persists
across save/load. Heartbeat log every 12 in-game hours.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T11: Register emitter in SubModule

**Files:**
- Modify: `src/Mod.Entry/SubModule.cs`

- [ ] **Step 1: Add `using` for the emitter**

```csharp
using Enlisted.Features.CampaignIntelligence.Signals;
```

- [ ] **Step 2: Add `AddBehavior` call**

Find the `campaignStarter.AddBehavior(new ...)` block that registers other CampaignIntelligence behaviors (post-Plan 1 landing). Add:

```csharp
campaignStarter.AddBehavior(new EnlistedSignalEmitterBehavior());
```

- [ ] **Step 3: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: PASS.

- [ ] **Step 4: Commit**

```
feat(signals): register EnlistedSignalEmitterBehavior in SubModule

Wires the signal emitter into the campaign behavior pipeline. Behavior
is fully idempotent when not enlisted (IsEnlisted gate in OnHourlyTick).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T12: Cooldown-rotation + diversity-picker improvements

**Files:**
- Modify: `src/Features/CampaignIntelligence/Signals/EnlistedSignalEmitterBehavior.cs`

- [ ] **Step 1: Replace first-match picker with weighted-diversity picker**

The Phase 1 T10 picker is first-match. Replace with a picker that:
1. Lists all eligible storylets for the prefix (cooldown-available + triggers pass).
2. Sorts by `weight.base × Π(modifier factors where `if` is satisfied)`.
3. Biases AGAINST recently-fired storylets from the same family (the last 3 emissions in that family lose 30% weight each).
4. Applies `MBRandom.RandomFloat` against normalized weights for diversity.

Keep the existing null-return semantics (returns null if pool is empty or all on cooldown).

- [ ] **Step 2: Track recent emissions per family**

Add a transient in-memory `Dictionary<SignalType, Queue<string>>` of last-3-fired storylet ids per family. Reset on save-load (not persisted). Used only for weight biasing.

- [ ] **Step 3: Commit**

```
feat(signals): weighted-diversity picker for floor-storylet pool

Replaces first-match picker with weight-and-bias selection. Sorts
eligible storylets by base-weight × satisfied-modifiers, then biases
AGAINST the last 3 fired in that family (30% weight penalty each)
to prevent same-storylet repeat in a short window. Random normalized-
weighted pick for diversity. Last-fired queue is transient — no
persistence need.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T13: Build + validator + in-game no-op smoke

- [ ] **Step 1: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 2: Validator**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

- [ ] **Step 3: In-game smoke (no-content — behaviors present, no storylets authored yet)**

Launch a campaign, enlist with a lord, run 2 in-game days. Verify session log shows `SIGNAL heartbeat` every 12 hours AND `SIGNAL no_storylet_for_family` entries when SignalBuilder produces candidates (expected — no content authored yet). Verify no crashes.

- [ ] **Step 4: No commit** — verification only.

---

## Phase E — DailyDriftApplicator rewire

### Task T14: Rewire `DailyDriftApplicator.FlushSummary` through SignalBuilder

**Files:**
- Modify: `src/Features/Activities/Orders/DailyDriftApplicator.cs`

- [ ] **Step 1: Read existing `FlushSummary`**

Identify the `StoryDirector.Instance?.EmitCandidate(new StoryCandidate { ... })` block. Today it emits directly with `SourceId = "orders.drift.summary"`, `CategoryId = "orders.drift"`, `ProposedTier = StoryTier.Log`, `StoryKey = "orders_drift_summary"`, and title/body strings.

- [ ] **Step 2: Replace with SignalBuilder call**

```csharp
private static void FlushSummary(OrderActivity activity)
{
    var pieces = activity.DriftPendingXp
        .OrderByDescending(kv => kv.Value)
        .Take(4)
        .Select(kv =>
        {
            var name = MBObjectManager.Instance.GetObject<SkillObject>(kv.Key)?.Name?.ToString();
            return $"{name ?? kv.Key} +{kv.Value}";
        });
    var summary = "Past few days: " + string.Join(", ", pieces) + " XP.";

    // Plan 3: route through SignalBuilder instead of direct emit. SignalBuilder
    // picks ScoutReturn (movement profiles) or CampTalk (camp profiles) family.
    var signal = EnlistedCampaignSignalBuilder.BuildDriftSummarySignal(
        summary,
        activity.CurrentDutyProfile ?? "wandering");

    // Route through the Emitter's public API. If the emitter is not registered
    // yet (pre-Plan-1 fallback), fall back to direct StoryDirector emit preserving
    // the Log tier contract.
    var emitter = EnlistedSignalEmitterBehavior.Instance;
    if (emitter != null)
    {
        emitter.EmitExternalSignal(signal, summary);
    }
    else
    {
        StoryDirector.Instance?.EmitCandidate(new StoryCandidate
        {
            SourceId = "orders.drift.summary",
            CategoryId = "orders.drift",
            ProposedTier = StoryTier.Log,
            EmittedAt = CampaignTime.Now,
            RenderedTitle = "Service notes",
            RenderedBody = summary,
            StoryKey = "orders_drift_summary"
        });
    }

    activity.DriftPendingXp.Clear();
    ModLogger.Info("DRIFT", $"flushed summary via signals: {summary}");
}
```

- [ ] **Step 3: Add `EmitExternalSignal` public method to the emitter**

In `EnlistedSignalEmitterBehavior.cs`:

```csharp
/// <summary>
/// Emits an externally-built signal (e.g. DailyDriftApplicator drift summary)
/// through the signal pipeline's throttle + storylet-pool lookup. Does NOT
/// go through SignalBuilder — the caller has already produced the signal.
/// </summary>
public void EmitExternalSignal(EnlistedCampaignSignal signal, string bodyText)
{
    if (signal == null) return;
    if (!OrdersNewsFeedThrottle.TryClaim()) return;

    var storyletId = PickStoryletFromPool(signal.PoolPrefix);
    if (storyletId == null)
    {
        // Fall back to a synthesized emit if the pool is empty — DailyDriftApplicator
        // still needs its summary delivered. Body carries the XP text.
        StoryDirector.Instance?.EmitCandidate(new StoryCandidate
        {
            SourceId = "signal." + signal.Type.ToString().ToLowerInvariant(),
            CategoryId = "signal." + signal.Type.ToString().ToLowerInvariant(),
            ProposedTier = StoryTier.Log,
            EmittedAt = CampaignTime.Now,
            RenderedTitle = "Service notes",
            RenderedBody = bodyText,
            StoryKey = "signal_drift_summary",
            SourcePerspective = DefaultPerspectiveFor(signal.Type),
            SignalConfidence = signal.Confidence,
            SignalRecency = SignalRecency.Immediate,
            SignalChangeType = signal.ChangeType,
            SignalInference = signal.InferenceOverride
        });
        return;
    }

    EmitPicked(signal, storyletId);
}
```

- [ ] **Step 4: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 5: Commit**

```
feat(signals): rewire DailyDriftApplicator.FlushSummary through signal pipeline

Drift summary no longer emits directly to StoryDirector. Instead it
routes through SignalBuilder.BuildDriftSummarySignal (picks
ScoutReturn for movement profiles, CampTalk for camp profiles) then
through EnlistedSignalEmitterBehavior.EmitExternalSignal (which
applies throttle + pool lookup + fallback synthesized emit if the
pool is empty). XP accumulation path is unchanged.

Integration spec §5.2 resolution applied.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T15: Smoke — drift flush routes as signal

- [ ] **Step 1: In-game smoke**

Launch campaign, enlist, fast-forward 5 in-game days. Expected:
- `DRIFT flushed summary via signals: ...` appears in session log.
- News feed shows a drift-summary entry with `SourcePerspective = Narrator` or `Courier` equivalent (whichever default matches ScoutReturn/CampTalk), AND a body line carrying the XP text.
- `SIGNAL emitted ScoutReturn` or `SIGNAL emitted CampTalk` entry (if the pool was populated) OR fallback direct emit.

- [ ] **Step 2: No commit** — verification only.

---

## Phase F — Authored floor storylets (10 families)

**Authoring template for every floor storylet:**

```json
{
  "id": "floor_<family>_<descriptor>_<n>",
  "title": "{=floor_<family>_<descriptor>_<n>_title}<short title, 3-6 words>",
  "setup": "{=floor_<family>_<descriptor>_<n>_setup}<1-2 sentences of prose, ≤240 chars, soldier perspective>",
  "theme": "signal",
  "category": "floor_<family>",
  "severity": "low",
  "cooldown_days": 2,
  "scope": { "context": "any" },
  "trigger": ["is_enlisted", "<snapshot-gate-trigger-if-any>"],
  "options": [
    {
      "id": "main",
      "text": "{=floor_<family>_<descriptor>_<n>_opt}<acknowledge / react>",
      "tooltip": "{=floor_<family>_<descriptor>_<n>_tt}<≤80 chars effect summary>",
      "effects": [
        { "apply": "<optional light effect: quality_add, rank_xp_minor, etc.>" }
      ]
    }
  ]
}
```

Each family task follows this shape. Storylets within a family share the theme and source perspective; variation comes from the specific snapshot state the storylet was written to express.

### Task T16: Author `floor_rumor.json` (~5 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_rumor.json`

- [ ] **Step 1: Author 5 rumor storylets**

Rumors are low-confidence, second-hand information. Source perspective is `Rumor`. Typical triggers: `FrontPressure >= Medium` OR `InformationConfidence == Low`.

Seed ids:
- `floor_rumor_enemy_riders_east` — "Riders to the east, or so the word runs." Gated on `FrontPressure >= Medium`.
- `floor_rumor_prices_in_the_market` — "Oats up in town. Not a good sign for a marching army."
- `floor_rumor_farmer_saw_hounds` — "A farmer said he saw war-hounds on the ridge. Or heard them. Or dreamed them."
- `floor_rumor_deserters_inn` — "Deserters at some inn two ridges over. So says the blacksmith's cousin."
- `floor_rumor_noble_letters` — "Letters going between nobles, thick this week, the couriers say."

- [ ] **Step 2: Include CRLF-safe JSON + inline loc-keys**

All text uses `{=key}Fallback` inline format (AGENTS.md Critical Rule #6 — loc-key adjacent to id field). Long-form effects only.

- [ ] **Step 3: Run validator**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: zero errors.

- [ ] **Step 4: Commit**

```
content(signals): author floor_rumor.json (5 low-confidence rumor storylets)

Plan 3 Phase F family 1 of 10. Rumors are second-hand low-confidence
information — "farmer said", "blacksmith's cousin heard", etc. Fires
when FrontPressure >= Medium or InformationConfidence is low. Source
perspective Rumor.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T17: Author `floor_camp_talk.json` (~6 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_camp_talk.json`

- [ ] **Step 1: Author 6 camp-talk storylets**

Camp talk is first-person observation from the billet / mess / campfire. Source perspective is `OwnObservation`. Typical triggers: `RecoveryNeed >= Medium`, `ArmyStrain >= Low`, or simply quiet-stretch fallback.

Seed ids:
- `floor_camp_talk_mess_grumbles` — "Talk at the mess: how much longer?"
- `floor_camp_talk_dice_rattle` — "Dice rattle by the fires. Half the watch is broke."
- `floor_camp_talk_old_campaign` — "A veteran reminisces. Most of his squad-mates are dead."
- `floor_camp_talk_homesick` — "A young one writes home. Mostly about the weather."
- `floor_camp_talk_war_songs` — "Someone starts a song. Others drift in."
- `floor_camp_talk_sergeant_complaints` — "The sergeant grumbles about the lord's latest order. Not loudly."

- [ ] **Step 2: Validator + commit**

```
content(signals): author floor_camp_talk.json (6 camp-talk storylets)

Plan 3 Phase F family 2 of 10. Camp talk is first-person observation
from the billet / mess / campfire. Fires on RecoveryNeed >= Medium,
ArmyStrain >= Low, or quiet stretches. Source perspective
OwnObservation.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T18: Author `floor_dispatch.json` (~5 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_dispatch.json`

- [ ] **Step 1: Author 5 dispatch storylets**

Dispatches are received orders — high-confidence, confirmed, actionable-seeming. Source perspective `Courier`. Triggers include `RecentChangeFlags.ObjectiveShift / SiegeTransition / DispersalRisk`.

Seed ids:
- `floor_dispatch_march_at_dawn` — "Orders down the line: march at dawn."
- `floor_dispatch_strike_camp` — "Strike camp. New heading. No time for questions."
- `floor_dispatch_siege_lifted` — "The siege is lifted. Column moves out."
- `floor_dispatch_army_breaking` — "Orders reach camp: the army disperses. Each lord to his own."
- `floor_dispatch_relief_column` — "A relief column is called. We ride to join it."

- [ ] **Step 2: Validator + commit**

---

### Task T19: Author `floor_order_shift.json` (~5 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_order_shift.json`

- [ ] **Step 1: Author 5 order-shift storylets**

Order shift = the plan changes mid-operation. Source perspective `Sergeant`. Triggers: `RecentChangeFlags.ObjectiveShift`.

Seed ids:
- `floor_order_shift_pursuit_cancelled` — "The pursuit is called off. Back to the column."
- `floor_order_shift_target_changes` — "New target. Some lord cousin's bright idea."
- `floor_order_shift_return_to_base` — "Orders came back to garrison."
- `floor_order_shift_position_held` — "Hold the position. Whatever was out there isn't coming."
- `floor_order_shift_formation_changes` — "Formation shift. The sergeant curses through every adjustment."

- [ ] **Step 2: Validator + commit**

---

### Task T20: Author `floor_quartermaster_warning.json` (~5 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_quartermaster_warning.json`

- [ ] **Step 1: Author 5 QM-warning storylets**

Quartermaster warnings are high-confidence supply warnings with a "get ready" flavor. Source perspective `Quartermaster`. Triggers: `SupplyPressure >= Tightening`, `RecentChangeFlags.SupplyBreak`.

Seed ids:
- `floor_quartermaster_warning_short_rations` — "Short rations from tomorrow. Plan accordingly."
- `floor_quartermaster_warning_wagon_late` — "The supply wagon is late. We eat what we have."
- `floor_quartermaster_warning_feed_the_horses` — "Fodder is thin. Feed the horses first; men second."
- `floor_quartermaster_warning_no_wine` — "Wine ration is done. It will not be replaced this week."
- `floor_quartermaster_warning_forage_tomorrow` — "Foragers ride out at dawn. Hope they come back with something."

- [ ] **Step 2: Validator + commit**

---

### Task T21: Author `floor_scout_return.json` (~5 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_scout_return.json`

- [ ] **Step 1: Author 5 scout-return storylets**

Scout returns report confirmed intel — high-confidence, specific observations. Source perspective `Narrator`. Triggers: `RecentChangeFlags.ConfirmedScoutInfo`.

Seed ids:
- `floor_scout_return_numbers_confirmed` — "Scouts in: enemy numbers confirmed. Not as bad as feared."
- `floor_scout_return_road_clear` — "Road clear ahead. Two days of easy marching."
- `floor_scout_return_bridge_held` — "The bridge is held by ours. Good news for tomorrow."
- `floor_scout_return_village_peaceful` — "The village was untouched. Locals offer water."
- `floor_scout_return_tracks_stale` — "Tracks confirmed — two days old. Whoever it was is long gone."

- [ ] **Step 2: Validator + commit**

---

### Task T22: Author `floor_prisoner_report.json` (~4 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_prisoner_report.json`

- [ ] **Step 1: Author 4 prisoner-report storylets**

Prisoner news — capture, ransom, escape. Source perspective `Narrator`. Triggers: `RecentChangeFlags.PrisonerUpdate`, `PrisonerStakes >= Medium`.

Seed ids:
- `floor_prisoner_report_captive_noble` — "A captured noble is traded for coin. A lord somewhere is pleased."
- `floor_prisoner_report_escape_attempt` — "Someone tried to walk out of the cell-wagon. He's back in it, quieter."
- `floor_prisoner_report_interrogation` — "The captive has been questioned. He said enough."
- `floor_prisoner_report_ransom_paid` — "A ransom paid. Two saddles lighter, one life heavier."

- [ ] **Step 2: Validator + commit**

---

### Task T23: Author `floor_aftermath_notice.json` (~5 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_aftermath_notice.json`

- [ ] **Step 1: Author 5 aftermath-notice storylets**

After-action notices — casualty reports, damage assessments, memorial beats. Source perspective `Narrator`. Triggers: `RecentChangeFlags.NewThreat` (contradictory-seeming — a new threat appears AS an aftermath of something prior) OR `MapEventStarted`-aftermath once the event resolves.

Seed ids:
- `floor_aftermath_notice_casualties` — "Three names added to the rolls. A sergeant cleans his sword."
- `floor_aftermath_notice_village_burned` — "A village burns behind us. Orders said to move on."
- `floor_aftermath_notice_prisoners_marched` — "A column of prisoners marches behind the army."
- `floor_aftermath_notice_wounded_cart` — "The wounded cart rolls slow. Some will not make the next town."
- `floor_aftermath_notice_captured_standard` — "A captured standard rides with us. The men are quieter than last week."

- [ ] **Step 2: Validator + commit**

---

### Task T24: Author `floor_tension_incident.json` (~5 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_tension_incident.json`

- [ ] **Step 1: Author 5 tension-incident storylets**

Tension incidents are discipline slippage — fight in the camp, theft, insubordination. Source perspective `OwnObservation`. Triggers: `ArmyStrain >= Medium`.

Seed ids:
- `floor_tension_incident_fight_at_the_fires` — "Two soldiers come to blows at the fires. Sergeants break it up."
- `floor_tension_incident_missing_bread` — "Someone's bread ration is missing. Nobody admits."
- `floor_tension_incident_late_return` — "A squad comes back after curfew. The sergeant waits."
- `floor_tension_incident_sergeants_argue` — "Two sergeants argue by the wagons. Something about prisoners."
- `floor_tension_incident_drunken_fool` — "A drunken fool staggers from the billet. Camp discipline watches."

- [ ] **Step 2: Validator + commit**

---

### Task T25: Author `floor_silence_with_implication.json` (~3 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor_silence_with_implication.json`

- [ ] **Step 1: Author 3 silence storylets**

Silence-with-implication fires during quiet stretches. The point is that NOTHING is happening — the storylet title and setup convey that, often with a detail that weights meaning (wind, a bird, a forgotten lantern). Source perspective `Narrator`. Trigger: quiet-stretch fallback (all strain axes None + no RecentChangeFlags).

Seed ids:
- `floor_silence_with_implication_wind_only` — "Only the wind tonight. Colder than last week."
- `floor_silence_with_implication_bird_at_dawn` — "A single crow calls at dawn. No reply."
- `floor_silence_with_implication_campfire_embers` — "The campfire is down to embers. No one is telling stories tonight."

- [ ] **Step 2: Validator + commit**

---

## Phase G — Observability + error handling

### Task T26: ModLogger heartbeat polish + error categorization

**Files:**
- Modify: `src/Features/CampaignIntelligence/Signals/EnlistedSignalEmitterBehavior.cs`

- [ ] **Step 1: Audit all throw sites**

Every public method has a try/catch wrapping to `ModLogger.Caught("SIGNAL", "...", ex)`. Every early-exit branch for expected conditions logs `ModLogger.Expected("SIGNAL", "<key>", "<summary>")` once per 60s by key.

- [ ] **Step 2: Add per-family heartbeat counters**

Track per-family emission counts within a session for diagnostic telemetry. Log the counts every 24 in-game hours.

- [ ] **Step 3: Commit**

```
feat(signals): observability polish — per-family counters + error cats

ModLogger calls categorized: Caught for unexpected exceptions,
Expected for known early-exits (throttled, cooldown, no-candidate).
Per-family emission counters log every 24 hours so session traces
show signal family distribution at a glance.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T27: Regenerate error-code registry

- [ ] **Step 1: Run generator**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

Stage `docs/error-codes.md` in the next content commit. No separate commit needed if run alongside final smoke closure.

---

## Phase H — Smoke verification

### Task T28: 14-in-game-day signal-cadence smoke

- [ ] **Step 1: Launch + enlist**

Launch campaign, enlist with a lord. Run at 4× speed for 14 in-game days. Mix lord movements: garrison → march → siege → raid → wander.

- [ ] **Step 2: Session log check**

Expect:
- `SIGNAL heartbeat` entries every 12 in-game hours.
- `SIGNAL emitted <Type>` entries at a sustainable pace — should see at least 6 of 10 families represented across 14 days.
- `SIGNAL no_storylet_for_family` entries when SignalBuilder produces a candidate but the pool has nothing eligible (cooldown-heavy day). Should NOT dominate the log.
- `DRIFT flushed summary via signals` entries roughly hourly under the 4× speed.

- [ ] **Step 3: News feed spot-check**

Open the news feed after each real minute of fast-forward. Verify varied signal voices (rumor / camp talk / dispatch / warning) across the page. No duplicates in the top 5 entries.

- [ ] **Step 4: Throttle verification at 4× speed**

Verify ≤ 5 news-feed entries per real minute. Bump to 16× speed if a speed mod is installed and verify near-zero non-modal entries (throttle suppression).

- [ ] **Step 5: Mode-switch test**

Force a `RecentChangeFlags.ObjectiveShift` bit via debug hotkey (if Plan 5 has shipped debug hotkeys) or wait for natural trigger. Verify the next emission is `order_shift` family.

- [ ] **Step 6: No commit** — verification only.

---

### Task T29: Verification doc + status update

**Files:**
- Create: `docs/superpowers/plans/2026-04-21-signal-projection-verification.md`

- [ ] **Step 1: Author verification doc**

Summarize what shipped: task trail, commit refs, known limitations (e.g. signal trigger thresholds may need calibration after real play data), deferred items, cross-plan integration notes (Plan 1 dependency, Plan 4 integration through shared TriggerRegistry predicates).

- [ ] **Step 2: Update CLAUDE.md status paragraph**

Change Plan 3 status line from "Not yet drafted" to "✅ Shipped YYYY-MM-DD on `development` (commits `<first>` → `<last>`)."

- [ ] **Step 3: Commit**

```
docs(signals): Plan 3 verification + status closure

Plan 3 (Signal Projection) complete. Ships EnlistedCampaignSignalBuilder
projecting Plan 1 snapshot → 10 signal families, EnlistedSignalEmitterBehavior
with throttled hourly emission + cooldown tracking, StoryCandidate
signal-metadata extensions, DailyDriftApplicator flush-summary rewired
through the signal pipeline, and ~48 authored floor storylets across
10 signal families. Verification doc + CLAUDE.md status updated.

Ready for Plan 4 Phase B.5 (snapshot-backed TriggerRegistry predicates)
to further refine floor-storylet gating.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Cross-plan integration summary

- **Plan 1 dependency:** Plan 3 hard-requires `EnlistedCampaignIntelligenceBehavior.Current` + `RecentChangeFlags` + all Plan 1 snapshot enums. Plan 3 does not ship until Plan 1 ships.
- **Plan 2 relationship:** Plan 2 (AI intervention) is independent of Plan 3. They can ship in either order.
- **Plan 4 integration:** Plan 4 Phase B.5 adds snapshot-backed `TriggerRegistry` predicates (e.g. `snapshot_supply_pressure_gte:2`). Plan 3's floor-storylet authoring (T16-T25) uses PLACEHOLDER gates (existing `quality_gte` on existing qualities like `scrutiny`) until Plan 4's predicates land. A follow-up authoring pass after Plan 4 ships refines the triggers to use the richer snapshot predicates — scope out of Plan 3.
- **Plan 5 relationship:** Plan 5 (career loop closure) authors culture/trait overlays on hot-path storylets. Plan 3's floor storylets CAN be overlay targets if any prove to be hot-path; that identification happens in Plan 5's overlay-audit phase.
- **Shared code surface with Plans 2/4/5:** none. Plan 3 writes to `src/Features/CampaignIntelligence/Signals/` + adds optional fields to `src/Features/Content/StoryCandidate.cs` + modifies one emit site in `src/Features/Activities/Orders/DailyDriftApplicator.cs`. None of those are touched by Plans 2/4/5.

---

## Shipping checklist

- [ ] Plan 1 shipped (Intelligence Backbone accessor available)
- [ ] Phase 0 T1 — offsets claimed, csproj entries
- [ ] Phase A T2-T4 — enums + POCOs
- [ ] Phase B T5-T7 — SignalBuilder pure projector + mapping ref doc
- [ ] Phase C T8-T9 — StoryCandidate field additions
- [ ] Phase D T10-T13 — Emitter behavior + cooldown + picker
- [ ] Phase E T14-T15 — DailyDriftApplicator rewire
- [ ] Phase F T16-T25 — 10 floor-storylet family files authored
- [ ] Phase G T26-T27 — Observability polish + error codes
- [ ] Phase H T28-T29 — 14-day smoke + verification doc

Total: 29 tasks. Estimated effort: 2-3 implementation sessions for code + content, 1 session for smoke.
