# Campaign Intelligence Backbone Implementation Plan (Plan 1 of 5)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Integration context:** This is Plan 1 in the five-plan integration roadmap — see [docs/superpowers/specs/2026-04-21-plans-integration-design.md](../specs/2026-04-21-plans-integration-design.md) for the full sequence (Plan 2: Lord AI Intervention; Plan 3: Signal Projection; Plan 4: Duty Opportunities; Plan 5: Career Loop Closure). Plan 1 is the load-bearing foundation — all four downstream plans read its `EnlistedCampaignIntelligenceBehavior.Current` accessor. No downstream plan ships code until Plan 1 lands.

**Goal:** Build the enlisted-only campaign-intelligence backbone — a compact strategic assessment of the enlisted lord that is recomputed on a fixed cadence, persisted across save/load, and exposed through a read-only accessor. No consumers in this plan; downstream plans (AI intervention, signal projection, duty opportunities) consume the accessor.

**Architecture:** One `CampaignBehaviorBase` (`EnlistedCampaignIntelligenceBehavior`) owns a single `EnlistedLordIntelligenceSnapshot?` instance. Live TaleWorlds state is gathered by a pure `SnapshotCollector` into a transient `IntelligenceInputs` struct, then classified by a pure `SnapshotClassifier` into the snapshot's enum fields. Recompute cadence is **hourly** (baseline) plus **event-driven invalidation** (WarDeclared / MakePeace / ArmyCreated / ArmyDispersed / MapEventStarted set a dirty bit that forces the next hourly tick to set the corresponding `RecentChangeFlags`). The backbone is gated behind `EnlistmentBehavior.Instance.IsEnlisted` — no writes, no reads, no work when the player is not actively enlisted.

**Tech Stack:** C# 7.3 / .NET Framework 4.7.2, Mount & Blade II: Bannerlord `CampaignBehaviorBase` + `SaveableTypeDefiner` + `CampaignEvents` + Enlisted `ModLogger` / `EnlistmentBehavior`.

**Scope boundary — what this plan does NOT do:**

- No `GameModel` wrapper or Harmony patch. Reserved for Plan 2 (AI intervention).
- No `StoryCandidate` field additions or `SignalBuilder`. Reserved for Plan 3 (signal projection).
- No `EmitCandidate`, `DutyOpportunityBuilder`, or storylet JSON. Reserved for Plan 4 (duty opportunities).
- No tuning of classification thresholds beyond sensible seed values. Thresholds are configurable in code; calibration is an iterative pass after downstream consumers exist.

**Verification surface:** Pure-logic classifiers are unit-testable (but the project has no test harness today — set up is optional in Task T-U1). The realistic verification stack is `dotnet build` + `validate_content.py` + in-game smoke via ModLogger heartbeat traces. The plan uses that stack.

---

## Design decisions locked before task 1

### Tick cadence

- **Baseline: `CampaignEvents.HourlyTickEvent`.** One global tick per in-game hour. Handler reads the enlisted lord's current party via `EnlistmentBehavior.Instance.EnlistedLord.PartyBelongedTo` and recomputes the snapshot.
- **Event-driven invalidation:** `WarDeclared`, `MakePeace`, `ArmyCreated`, `ArmyDispersed`, `MapEventStarted` set specific bits on a `_pendingChangeFlags` field. The next hourly recompute OR's those bits into the published snapshot's `RecentChangeFlags` regardless of delta detection, then clears `_pendingChangeFlags`.
- **Daily decay: `CampaignEvents.DailyTickEvent`** — any `RecentChangeFlags` bit older than 24 in-game hours is cleared (tracked per-bit via `_flagAgeHours` Dictionary), so the snapshot only advertises "recent" changes for one game day.
- **Why not per-frame or quarter-hourly:** cost is dominated by strategic-neighborhood enumeration (§7.2), which walks nearby parties; hourly is frequent enough to surface state change before the player observes it, and cheap enough to ignore cost. Per-frame would add measurable CPU in long marches with no behavioral gain. `CampaignEvents.HourlyTickPartyEvent` fires per-party (thousands per tick) and is strictly worse than gating a single global `HourlyTickEvent` handler on `IsEnlisted`.

### Read-only accessor shape

```csharp
public EnlistedLordIntelligenceSnapshot? Current { get; }
```

- Returns `null` when `IsEnlisted` is false.
- Returns the most recent snapshot otherwise. Never exposes the interior `_working` buffer.
- Snapshot is a reference type (class, not struct) because it has 13+ fields and consumers may cache by reference for a single tick.

### Snapshot identity

- The snapshot does NOT store a `Hero` reference. Lord identity is derived at runtime from `EnlistmentBehavior.Instance.EnlistedLord`. Persisting `Hero` reference inside the snapshot would add save-system coupling without benefit — the enlistment behavior already persists the lord.
- The snapshot does NOT store a `MobileParty`, `Settlement`, `MapEvent`, or `Army` reference. All classification is reduced to enums + numeric primitives before assignment. This keeps the serialized form small, avoids dangling references after save/load, and satisfies §12.2 ("recompute volatile assessment on load").

### Save-definer offsets (claim in T1 after verifying free)

- Class offset: `48` — `EnlistedLordIntelligenceSnapshot`.
- Enum offsets: `86` through `98` — 13 enums (12 classification enums + `RecentChangeFlags`), see T2.
- AGENTS.md "Save-definer offset convention" was broadened 2026-04-21 to reserve class offsets 45-60 for concrete `Activity` subclasses AND closely-related surface-spec persistent state (the Intelligence snapshot falls in the latter category). Previously the reservation was strictly for `Activity` subclasses. The broadening ships alongside this plan (see AGENTS.md commit trail).
- Offsets 10-14 were held by the legacy Orders subsystem; retired 2026-04-21 in commit `a8719bb`. Those offsets remain reserved; do not reuse.
- T1 greps to confirm no other in-flight branch claimed 48 or 86-98 first.

---

## File structure

```
src/Features/CampaignIntelligence/
  IntelligenceEnums.cs                         — all enum types + RecentChangeFlags [Flags] enum
  EnlistedLordIntelligenceSnapshot.cs           — [Serializable] POCO with classified fields
  IntelligenceInputs.cs                          — transient data struct produced by the collector
  SnapshotCollector.cs                           — static: live TaleWorlds state → IntelligenceInputs
  SnapshotClassifier.cs                          — static: IntelligenceInputs → snapshot field values
  EnlistedCampaignIntelligenceBehavior.cs        — CampaignBehaviorBase: lifecycle + tick + accessor
```

Six files, each with one responsibility. Collector is the ONLY file that touches the TaleWorlds API for gather; classifier is pure data-in → data-out; behavior is the ONLY file with event subscriptions.

---

## Task index

- **Phase 0 — Save-definer + csproj claim**: T1
- **Phase A — Type foundation**: T2, T3, T4
- **Phase B — Behavior skeleton + lifecycle**: T5, T6, T7
- **Phase C — Live-input collector**: T8, T9, T10, T11, T12
- **Phase D — Classification**: T13, T14, T15, T16, T17, T18
- **Phase E — Tick + change flags**: T19, T20, T21, T22
- **Phase F — Persistence**: T23, T24
- **Phase G — Observability + error handling**: T25, T26
- **Phase H — Smoke verification + sign-off**: T27, T28, T29

Total: 29 tasks.

---

## Phase 0 — Save-definer + csproj claim

### Task T1: Verify and claim save-definer offsets + csproj entries

**Files:**
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs:38-113`
- Modify: `Enlisted.csproj` (add explicit `<Compile Include>` entries for the 6 new files)

- [ ] **Step 1: Confirm offsets are free**

Grep the definer to confirm no other branch has claimed these offsets:

```
Grep pattern: "AddClassDefinition\(typeof\([^)]+\), 48\)|AddEnumDefinition\(typeof\([^)]+\), 8[6-9]\)|AddEnumDefinition\(typeof\([^)]+\), 9[0-7]\)"
Expected: zero matches
```

If any match, STOP and revise offsets up. CLAUDE.md pitfall #15 applies — colliding offsets corrupt saves silently.

- [ ] **Step 2: Add class-definition placeholder**

Append to `DefineClassTypes()` at the tail (after line 73, after `NamedOrderState` at offset 47):

```csharp
// Campaign Intelligence backbone (Plan 1 of 4) — offset 48
AddClassDefinition(typeof(Features.CampaignIntelligence.EnlistedLordIntelligenceSnapshot), 48);
```

- [ ] **Step 3: Add enum-definition placeholders**

Append to `DefineEnumTypes()` at the tail (after line 112, after `DutyProfileId` at offset 85):

```csharp
// Campaign Intelligence backbone enums (Plan 1 of 4) — offsets 86-97
AddEnumDefinition(typeof(Features.CampaignIntelligence.StrategicPosture), 86);
AddEnumDefinition(typeof(Features.CampaignIntelligence.ObjectiveType), 87);
AddEnumDefinition(typeof(Features.CampaignIntelligence.ObjectiveConfidence), 88);
AddEnumDefinition(typeof(Features.CampaignIntelligence.FrontPressure), 89);
AddEnumDefinition(typeof(Features.CampaignIntelligence.ArmyStrainLevel), 90);
AddEnumDefinition(typeof(Features.CampaignIntelligence.SupplyPressure), 91);
AddEnumDefinition(typeof(Features.CampaignIntelligence.InformationConfidence), 92);
AddEnumDefinition(typeof(Features.CampaignIntelligence.EnemyContactRisk), 93);
AddEnumDefinition(typeof(Features.CampaignIntelligence.PursuitViability), 94);
AddEnumDefinition(typeof(Features.CampaignIntelligence.RecoveryNeed), 95);
AddEnumDefinition(typeof(Features.CampaignIntelligence.PrisonerStakes), 96);
AddEnumDefinition(typeof(Features.CampaignIntelligence.PlayerTrustWindow), 97);
AddEnumDefinition(typeof(Features.CampaignIntelligence.RecentChangeFlags), 98);
```

Note: this is 13 enum offsets (86-98). `RecentChangeFlags` is a `[Flags]` enum and must also be registered. CLAUDE.md notes enum offsets 86-99 free; this consumes 86-98 leaving 99.

- [ ] **Step 4: Add csproj `<Compile Include>` lines**

The csproj uses explicit per-file includes (see `Enlisted.csproj:237-294` for Enlistment + Interface patterns). Add a new block near the Interface block, sorted alphabetically by feature folder. Before line 237 (i.e., in the alphabetic neighborhood of "Camp" and "Combat" — search for `src\Features\Camp\` and `src\Features\Combat\` to find the right anchor):

```xml
    <Compile Include="src\Features\CampaignIntelligence\IntelligenceEnums.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\EnlistedLordIntelligenceSnapshot.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\IntelligenceInputs.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\SnapshotCollector.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\SnapshotClassifier.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\EnlistedCampaignIntelligenceBehavior.cs"/>
```

- [ ] **Step 5: Build — expect compile failure**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **FAIL** with errors like `The type or namespace name 'EnlistedLordIntelligenceSnapshot' does not exist in the namespace 'Enlisted.Features.CampaignIntelligence'`. That's correct — types don't exist yet. T2-T4 will make this build succeed.

- [ ] **Step 6: Commit**

```bash
git add src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs Enlisted.csproj
git commit -F <message-file>
```

Commit message:

```
feat(intel): claim save-definer offsets 48/86-98 + csproj slots for Plan 1

Reserves class offset 48 (EnlistedLordIntelligenceSnapshot) and enum
offsets 86-98 (twelve classification enums plus RecentChangeFlags) for
the campaign-intelligence backbone. Build currently fails — types land
in T2-T4. Part of Plan 1 of 4 (Campaign Intelligence).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase A — Type foundation

### Task T2: Create `IntelligenceEnums.cs` — 12 classification enums + RecentChangeFlags

**Files:**
- Create: `src/Features/CampaignIntelligence/IntelligenceEnums.cs`

- [ ] **Step 1: Write the enums file**

```csharp
using System;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Top-level strategic stance the enlisted lord's party is currently operating in.
    /// Computed from live state; not author-overridable.
    /// </summary>
    public enum StrategicPosture : byte
    {
        March,
        ShadowingEnemy,
        FrontierDefense,
        Relief,
        OffensiveSiege,
        OpportunisticRaid,
        Recovery,
        Regroup,
        ArmyGather,
        GarrisonHold
    }

    /// <summary>
    /// Concrete operational objective the enlisted lord is pursuing right now, if any.
    /// </summary>
    public enum ObjectiveType : byte
    {
        None,
        Move,
        Pursue,
        DefendSettlement,
        RelieveSettlement,
        BesiegeSettlement,
        RaidVillage,
        GatherArmy,
        Recover
    }

    public enum ObjectiveConfidence : byte { Low, Medium, High }

    public enum FrontPressure : byte { None, Low, Medium, High, Critical }

    public enum ArmyStrainLevel : byte { None, Mild, Elevated, Severe, Breaking }

    public enum SupplyPressure : byte { None, Tightening, Strained, Critical }

    public enum InformationConfidence : byte { Low, Medium, High }

    public enum EnemyContactRisk : byte { Low, Medium, High }

    public enum PursuitViability : byte { NotViable, Marginal, Viable, Strong }

    public enum RecoveryNeed : byte { None, Low, Medium, High }

    public enum PrisonerStakes : byte { None, Low, Medium, High }

    public enum PlayerTrustWindow : byte { None, Narrow, Moderate, Broad }

    /// <summary>
    /// Bit flags describing what has changed recently enough that downstream consumers
    /// may still need to notice. Set by event-driven invalidation + delta detection
    /// during recompute; decayed on DailyTickEvent.
    /// </summary>
    [Flags]
    public enum RecentChangeFlags : ushort
    {
        None              = 0,
        ObjectiveShift    = 1 << 0,
        NewThreat         = 1 << 1,
        SiegeTransition   = 1 << 2,
        DispersalRisk     = 1 << 3,
        ConfirmedScoutInfo = 1 << 4,
        SupplyBreak       = 1 << 5,
        PrisonerUpdate    = 1 << 6,
        WarDeclared       = 1 << 7,
        MakePeace         = 1 << 8,
        ArmyFormed        = 1 << 9,
        ArmyDispersed     = 1 << 10,
        MapEventJoined    = 1 << 11
    }
}
```

- [ ] **Step 2: Run CRLF normalizer (new file only)**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/IntelligenceEnums.cs
```

- [ ] **Step 3: Build — expect enum types resolve, others still fail**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: still fails, but `StrategicPosture` / `ObjectiveType` / etc. errors disappear. `EnlistedLordIntelligenceSnapshot` still missing.

- [ ] **Step 4: Commit**

```bash
git add src/Features/CampaignIntelligence/IntelligenceEnums.cs
git commit -F <message-file>
```

Message: `feat(intel): add classification enums + RecentChangeFlags (T2)`.

---

### Task T3: Create `EnlistedLordIntelligenceSnapshot.cs` — serializable POCO

**Files:**
- Create: `src/Features/CampaignIntelligence/EnlistedLordIntelligenceSnapshot.cs`

- [ ] **Step 1: Write the class**

```csharp
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Compact assessed state for the enlisted lord. Recomputed on hourly tick and on
    /// specific campaign-event invalidation. Never stores live TaleWorlds references —
    /// all fields are enums or primitives so the snapshot round-trips cleanly through
    /// save/load without dangling pointers. §12.2 recompute-on-load is honored by the
    /// owning behavior clearing volatile fields on game-load.
    /// </summary>
    [Serializable]
    public sealed class EnlistedLordIntelligenceSnapshot
    {
        [SaveableProperty(1)] public StrategicPosture Posture { get; set; }
        [SaveableProperty(2)] public ObjectiveType Objective { get; set; }
        [SaveableProperty(3)] public ObjectiveConfidence ObjectiveConfidence { get; set; }
        [SaveableProperty(4)] public FrontPressure FrontPressure { get; set; }
        [SaveableProperty(5)] public ArmyStrainLevel ArmyStrain { get; set; }
        [SaveableProperty(6)] public SupplyPressure SupplyPressure { get; set; }
        [SaveableProperty(7)] public InformationConfidence InformationConfidence { get; set; }
        [SaveableProperty(8)] public EnemyContactRisk EnemyContactRisk { get; set; }
        [SaveableProperty(9)] public PursuitViability PursuitViability { get; set; }
        [SaveableProperty(10)] public RecoveryNeed RecoveryNeed { get; set; }
        [SaveableProperty(11)] public PrisonerStakes PrisonerStakes { get; set; }
        [SaveableProperty(12)] public PlayerTrustWindow PlayerTrustWindow { get; set; }
        [SaveableProperty(13)] public RecentChangeFlags RecentChanges { get; set; }

        /// <summary>In-game time at which this snapshot was last refreshed.</summary>
        [SaveableProperty(14)] public CampaignTime LastRefreshed { get; set; }

        /// <summary>
        /// Reset fields to default. Called by the behavior on enlistment end so the
        /// accessor returns a clean no-state object should something read it during
        /// the OnEnlistmentEnded tear-down window.
        /// </summary>
        public void Clear()
        {
            Posture = StrategicPosture.March;
            Objective = ObjectiveType.None;
            ObjectiveConfidence = ObjectiveConfidence.Low;
            FrontPressure = FrontPressure.None;
            ArmyStrain = ArmyStrainLevel.None;
            SupplyPressure = SupplyPressure.None;
            InformationConfidence = InformationConfidence.Low;
            EnemyContactRisk = EnemyContactRisk.Low;
            PursuitViability = PursuitViability.NotViable;
            RecoveryNeed = RecoveryNeed.None;
            PrisonerStakes = PrisonerStakes.None;
            PlayerTrustWindow = PlayerTrustWindow.None;
            RecentChanges = RecentChangeFlags.None;
            LastRefreshed = CampaignTime.Never;
        }
    }
}
```

- [ ] **Step 2: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/EnlistedLordIntelligenceSnapshot.cs
```

- [ ] **Step 3: Build — expect snapshot resolves, still other missing types**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: snapshot-related errors clear. `IntelligenceInputs`, `SnapshotCollector`, `SnapshotClassifier`, `EnlistedCampaignIntelligenceBehavior` still unresolved — expected.

- [ ] **Step 4: Commit**

```bash
git add src/Features/CampaignIntelligence/EnlistedLordIntelligenceSnapshot.cs
git commit -F <message-file>
```

Message: `feat(intel): add EnlistedLordIntelligenceSnapshot serializable POCO (T3)`.

---

### Task T4: Fill remaining csproj-registered files as empty stubs so build completes

**Files:**
- Create: `src/Features/CampaignIntelligence/IntelligenceInputs.cs` (stub)
- Create: `src/Features/CampaignIntelligence/SnapshotCollector.cs` (stub)
- Create: `src/Features/CampaignIntelligence/SnapshotClassifier.cs` (stub)
- Create: `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs` (stub)

Rationale: Task T1 registered four file paths in the csproj. Without empty stubs, the build fails. Phases B-D fill them.

- [ ] **Step 1: Write `IntelligenceInputs.cs` as a stub**

```csharp
namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Transient plain-data bag produced by SnapshotCollector and consumed by
    /// SnapshotClassifier. Fields land in T8; kept empty here so the csproj
    /// registration compiles.
    /// </summary>
    internal struct IntelligenceInputs
    {
    }
}
```

- [ ] **Step 2: Write `SnapshotCollector.cs` stub**

```csharp
namespace Enlisted.Features.CampaignIntelligence
{
    internal static class SnapshotCollector
    {
    }
}
```

- [ ] **Step 3: Write `SnapshotClassifier.cs` stub**

```csharp
namespace Enlisted.Features.CampaignIntelligence
{
    internal static class SnapshotClassifier
    {
    }
}
```

- [ ] **Step 4: Write `EnlistedCampaignIntelligenceBehavior.cs` stub**

```csharp
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Campaign-intelligence backbone behavior. Populated across T5-T22. Registered
    /// in SubModule.cs in T7.
    /// </summary>
    public sealed class EnlistedCampaignIntelligenceBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents() { }
        public override void SyncData(IDataStore dataStore) { }
    }
}
```

- [ ] **Step 5: Normalize CRLF on all four new files**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/IntelligenceInputs.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/SnapshotCollector.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/SnapshotClassifier.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs
```

- [ ] **Step 6: Build — expect success**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**. Build green with stubs in place.

- [ ] **Step 7: Commit**

```bash
git add src/Features/CampaignIntelligence/IntelligenceInputs.cs src/Features/CampaignIntelligence/SnapshotCollector.cs src/Features/CampaignIntelligence/SnapshotClassifier.cs src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs
git commit -F <message-file>
```

Message: `feat(intel): add stub files for collector/classifier/behavior (T4)`.

---

## Phase B — Behavior skeleton + lifecycle

### Task T5: Behavior singleton + `Current` accessor + `_working` snapshot buffer

**Files:**
- Modify: `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs`

- [ ] **Step 1: Flesh out the behavior skeleton**

Replace the stub body with:

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Tracks the enlisted lord's strategic situation on an hourly cadence plus
    /// event-driven invalidation. Produces one EnlistedLordIntelligenceSnapshot
    /// exposed to consumers via <see cref="Current"/>. Only runs while the player
    /// is actively enlisted; returns null and clears interior state otherwise.
    /// </summary>
    [UsedImplicitly("Registered in SubModule.OnGameStart via campaignStarter.AddBehavior.")]
    public sealed class EnlistedCampaignIntelligenceBehavior : CampaignBehaviorBase
    {
        public static EnlistedCampaignIntelligenceBehavior Instance { get; private set; }

        private EnlistedLordIntelligenceSnapshot _working;
        private RecentChangeFlags _pendingChangeFlags;
        private readonly Dictionary<RecentChangeFlags, CampaignTime> _flagFirstSeen = new Dictionary<RecentChangeFlags, CampaignTime>();

        public EnlistedCampaignIntelligenceBehavior()
        {
            Instance = this;
        }

        /// <summary>
        /// Read-only access to the most recent strategic snapshot. Returns null when
        /// the player is not currently enlisted, or when no tick has yet populated it.
        /// Consumers MUST treat the returned reference as read-only — mutating it from
        /// outside the behavior will desync the backbone.
        /// </summary>
        public EnlistedLordIntelligenceSnapshot Current
        {
            get
            {
                if (EnlistmentBehavior.Instance == null || !EnlistmentBehavior.Instance.IsEnlisted)
                {
                    return null;
                }
                return _working;
            }
        }

        public override void RegisterEvents() { /* T6 + T8-T21 fill this in */ }

        public override void SyncData(IDataStore dataStore) { /* T23 fills this in */ }

        internal void EnsureInitialized()
        {
            if (_working == null) { _working = new EnlistedLordIntelligenceSnapshot(); _working.Clear(); }
            if (_flagFirstSeen == null) { /* field init covers this, but SyncData can null it */ }
        }
    }
}
```

Note: `EnsureInitialized` will be called from `SyncData` in T23 and from the OnEnlisted handler in T6 — pattern matches `FlagStore.EnsureInitialized` / `QualityStore.EnsureInitialized` per CLAUDE.md project pitfall on `[Serializable]` deserialization.

- [ ] **Step 2: Build — expect success**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs
git commit -F <message-file>
```

Message: `feat(intel): behavior singleton + Current accessor (T5)`.

---

### Task T6: Wire `OnEnlisted` / `OnEnlistmentEnded` handlers — clear-on-end, stub recompute on start

**Files:**
- Modify: `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs`

- [ ] **Step 1: Extend `RegisterEvents()`**

```csharp
public override void RegisterEvents()
{
    EnlistmentBehavior.OnEnlisted += HandleOnEnlisted;
    EnlistmentBehavior.OnEnlistmentEnded += HandleOnEnlistmentEnded;
}

private void HandleOnEnlisted(Hero lord)
{
    try
    {
        EnsureInitialized();
        _pendingChangeFlags = RecentChangeFlags.None;
        _flagFirstSeen.Clear();
        // Recompute here so the first snapshot after enlist is populated before
        // the first hourly tick. Body arrives in T19 — for now, just clear the
        // prior state.
        _working.Clear();
    }
    catch (Exception ex)
    {
        ModLogger.Surfaced("INTEL", "enlist_init_failed", ex);
    }
}

private void HandleOnEnlistmentEnded(string reason, bool voluntary)
{
    try
    {
        _working?.Clear();
        _pendingChangeFlags = RecentChangeFlags.None;
        _flagFirstSeen.Clear();
    }
    catch (Exception ex)
    {
        ModLogger.Caught("INTEL", "clear_on_end_failed", ex);
    }
}
```

Note: `OnEnlisted` is `Action<Hero>`; `OnEnlistmentEnded` is `Action<string, bool>` — verified at `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:8433` and `:8496`.

- [ ] **Step 2: Build — expect success**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Regenerate error-codes registry**

Because we added `ModLogger.Surfaced` calls, the registry is now stale:

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

Expect `docs/error-codes.md` to grow with `E-INTEL-001` (or next free) entries for `enlist_init_failed` and `clear_on_end_failed`.

- [ ] **Step 4: Commit**

```bash
git add src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs docs/error-codes.md
git commit -F <message-file>
```

Message: `feat(intel): enlist/unenlist handlers + state clear (T6)`.

---

### Task T7: Register behavior in `SubModule.cs`

**Files:**
- Modify: `src/Mod.Entry/SubModule.cs` — insert one `AddBehavior` line near the existing activity-registration block (around line 350-362 per recent grep).

- [ ] **Step 1: Find the anchor**

Grep for `campaignStarter.AddBehavior(new Features.Activities.Orders.PathScorer());` at approximately `src/Mod.Entry/SubModule.cs:362` — that's the final existing AddBehavior in the Orders block. Insert after it.

- [ ] **Step 2: Add the registration**

After the `PathScorer` line, insert:

```csharp
                    // Campaign Intelligence backbone (Plan 1 of 4). Observes the enlisted
                    // lord on an hourly cadence, classifies strategic posture, front pressure,
                    // strain, supply, pursuit viability, prisoner stakes, information quality,
                    // and recent-change flags. Exposes a read-only EnlistedLordIntelligenceSnapshot
                    // via EnlistedCampaignIntelligenceBehavior.Instance.Current for downstream
                    // consumers (AI intervention, signal projection, duty opportunities). Gated
                    // behind EnlistmentBehavior.Instance.IsEnlisted; reverts cleanly on unenlist.
                    campaignStarter.AddBehavior(new Features.CampaignIntelligence.EnlistedCampaignIntelligenceBehavior());
```

- [ ] **Step 3: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 4: Regenerate error-codes** (SubModule.cs doesn't contain Surfaced calls but the registry regenerator tolerates no-op regen — run anyway to confirm clean):

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

- [ ] **Step 5: Commit**

```bash
git add src/Mod.Entry/SubModule.cs
git commit -F <message-file>
```

Message: `feat(intel): register EnlistedCampaignIntelligenceBehavior (T7)`.

---

## Phase C — Live-input collector

### Task T8: Fill `IntelligenceInputs.cs` — transient data bag

**Files:**
- Modify: `src/Features/CampaignIntelligence/IntelligenceInputs.cs`

Design: a struct of plain-data fields grouped by §7 subsection. No TaleWorlds references at snapshot time; the struct is populated in one pass and consumed in the next. Collectors fill subsets; the classifier reads all.

- [ ] **Step 1: Replace stub with the real struct**

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Transient plain-data inputs produced by SnapshotCollector and consumed by
    /// SnapshotClassifier. One instance per recompute. Fields are grouped by §7
    /// subsection of the Campaign Intelligence spec.
    /// </summary>
    internal struct IntelligenceInputs
    {
        // §7.1 Lord and party state
        public bool LordAlive;
        public bool LordIsPrisoner;
        public bool LordIsWounded;
        public MobileParty LordParty;
        public Settlement CurrentSettlement;
        public Settlement BesiegedSettlement;
        public bool InMapEvent;
        public bool IsArmyLeader;
        public bool InArmy;
        public Army.ArmyTypes ArmyType;      // Besieger / Raider / Defender / Patrolling / NumberOfArmyTypes
        public bool ArmyIsGathering;         // true if army exists but has not yet converged (proxy heuristic)
        public float ArmyCohesion;
        public float FoodDaysRemaining;
        public float PartySizeRatio;
        public float WoundedRatio;

        // §7.2 Strategic neighborhood
        public int NearbyHostileCount;
        public float NearestHostileDistance;
        public float NearestHostileStrengthRatio;
        public int NearbyAlliedCount;
        public bool NearbyAlliedReliefPlausible;
        public int ThreatenedFriendlySettlementCount;
        public Settlement NearestThreatenedFriendly;
        public bool FrontierHeatingUp;

        // §7.3 Information evidence
        public bool HasFreshTracks;
        public bool HasEnemyBuildupEvidence;
        public bool HasGarrisonObservation;
        public int ImportantPrisonerCount;
        public bool RecentWarTransition;
        public bool RecentPeaceTransition;
        public bool RecentArmyFormation;
        public bool RecentArmyDispersal;

        // §7.4 Player relevance
        public int PlayerTier;
        public int PlayerRelationWithLord;
        public bool PlayerProximate;

        // Transient carry-over: the dirty flags the hourly handler sucks out of
        // _pendingChangeFlags. NOT serialized, not read by classifier directly
        // — the behavior OR's these into the published snapshot after classifier runs.
        public RecentChangeFlags PendingFlagsAtTickStart;
    }
}
```

Notes:
- `Army.ArmyTypes` enum exists (decompile `TaleWorlds.CampaignSystem/Army.cs:22`) with values Besieger / Raider / Defender / Patrolling / NumberOfArmyTypes. `Army.ArmyState` and `Army.AIBehaviorFlags` do NOT exist — the "gathering" state is NOT exposed as a public enum; it is inferred from the private `_armyGatheringStartTime` field vs current time. T9 uses a proxy heuristic instead.
- `Army.Cohesion` — confirmed float getter/setter at `Army.cs:106`.
- `Army.LeaderParty` — confirmed `MobileParty` getter at `Army.cs:118`.
- `Army.Parties` — confirmed `MBReadOnlyList<MobileParty>` at `Army.cs:95`.

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/IntelligenceInputs.cs
git commit -F <message-file>
```

Message: `feat(intel): IntelligenceInputs transient data bag (T8)`.

---

### Task T9: Collect §7.1 lord + party state

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotCollector.cs`

- [ ] **Step 1: Replace stub with the collector entry-point + §7.1 method**

```csharp
using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Pure static collector. Reads live TaleWorlds state for the enlisted lord
    /// and packages it into an IntelligenceInputs struct for classification.
    /// Returns an empty inputs struct with LordAlive=false when the lord is not
    /// available; classifier treats that as "no assessment possible this tick."
    /// </summary>
    internal static class SnapshotCollector
    {
        /// <summary>Collect all inputs for the given lord. Caller must already have gated on IsEnlisted.</summary>
        public static IntelligenceInputs Collect(Hero lord, RecentChangeFlags pendingFlags)
        {
            var inputs = new IntelligenceInputs
            {
                PendingFlagsAtTickStart = pendingFlags
            };

            try
            {
                CollectLordAndPartyState(ref inputs, lord);
                CollectStrategicNeighborhood(ref inputs, lord);   // T10
                CollectInformationEvidence(ref inputs, lord);     // T11
                CollectPlayerRelevance(ref inputs, lord);         // T12
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTEL", "collector_failed", ex);
                // Partial inputs preserved; classifier will degrade gracefully.
            }

            return inputs;
        }

        private static void CollectLordAndPartyState(ref IntelligenceInputs inputs, Hero lord)
        {
            if (lord == null)
            {
                return;
            }

            inputs.LordAlive = lord.IsAlive;
            inputs.LordIsPrisoner = lord.IsPrisoner;
            inputs.LordIsWounded = lord.IsWounded;

            var party = lord.PartyBelongedTo;
            inputs.LordParty = party;
            if (party == null)
            {
                return;
            }

            inputs.CurrentSettlement = party.CurrentSettlement;
            inputs.BesiegedSettlement = party.BesiegedSettlement;
            inputs.InMapEvent = party.MapEvent != null;

            var army = party.Army;
            inputs.InArmy = army != null;
            inputs.IsArmyLeader = army != null && army.LeaderParty == party;
            if (army != null)
            {
                inputs.ArmyCohesion = army.Cohesion;
                inputs.ArmyType = army.ArmyType;

                // "Gathering" isn't a public state on Army — _armyGatheringStartTime is private.
                // Proxy: some non-leader party hasn't attached to the leader yet (AttachedTo differs).
                // MobileParty.AttachedTo is verified in decompile at MobileParty.cs:630.
                bool anyUnattached = false;
                foreach (var p in army.Parties)
                {
                    if (p == army.LeaderParty) continue;
                    if (p.AttachedTo != army.LeaderParty) { anyUnattached = true; break; }
                }
                inputs.ArmyIsGathering = anyUnattached;
            }

            // Food days remaining: Party.Food / daily consumption. Actual API is
            // MobileParty.Food (int) + GetConsumedFoodPerDay(). Verify in decompile.
            if (party.Food > 0)
            {
                var perDay = party.GetTotalFoodAtInventory(); // placeholder — see API Corrections
                inputs.FoodDaysRemaining = perDay > 0 ? (float)party.Food / perDay : float.PositiveInfinity;
            }

            // Party size ratio: current / max
            var partyRoster = party.MemberRoster;
            if (partyRoster != null && party.LimitedPartySize > 0)
            {
                inputs.PartySizeRatio = (float)partyRoster.TotalManCount / party.LimitedPartySize;
                inputs.WoundedRatio = partyRoster.TotalManCount > 0
                    ? (float)partyRoster.TotalWounded / partyRoster.TotalManCount
                    : 0f;
            }
        }

        // §7.2 — filled in T10
        private static void CollectStrategicNeighborhood(ref IntelligenceInputs inputs, Hero lord) { }
        // §7.3 — filled in T11
        private static void CollectInformationEvidence(ref IntelligenceInputs inputs, Hero lord) { }
        // §7.4 — filled in T12
        private static void CollectPlayerRelevance(ref IntelligenceInputs inputs, Hero lord) { }
    }
}
```

**IMPORTANT:** The following property names MUST be verified against `../Decompile/TaleWorlds.CampaignSystem/` before relying on them: `Army.ArmyType`, `Army.Cohesion`, `Army.Parties`, `Army.LeaderParty`, `MobileParty.AttachedTo`, `MobileParty.GetTotalFoodAtInventory`, `MobileParty.LimitedPartySize`, `PartyRoster.TotalManCount`, `PartyRoster.TotalWounded`. If any name is wrong, correct it inline and append an entry to the API Corrections Appendix at the bottom of this plan. Confirmed non-existent as of 2026-04-21: `Army.ArmyState`, `Army.AIBehaviorFlags` (see appendix).

- [ ] **Step 2: Verify APIs against decompile**

Grep the decompile directory:

```
Glob: ../Decompile/TaleWorlds.CampaignSystem/**/Army.cs
Glob: ../Decompile/TaleWorlds.CampaignSystem/**/MobileParty.cs
Glob: ../Decompile/TaleWorlds.CampaignSystem/**/PartyRoster.cs
```

For each: grep for `public.*(Cohesion|AIBehavior|Food|LimitedPartySize|TotalManCount|TotalWounded)`. Confirm signatures. Patch code inline; record any drift in the API Corrections Appendix.

- [ ] **Step 3: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS** (after API corrections).

- [ ] **Step 4: Regen error-codes**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

- [ ] **Step 5: Commit**

```bash
git add src/Features/CampaignIntelligence/SnapshotCollector.cs docs/error-codes.md
git commit -F <message-file>
```

Message: `feat(intel): collect §7.1 lord + party state (T9)`.

---

### Task T10: Collect §7.2 strategic neighborhood

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotCollector.cs`

- [ ] **Step 1: Replace the `CollectStrategicNeighborhood` stub**

```csharp
private static void CollectStrategicNeighborhood(ref IntelligenceInputs inputs, Hero lord)
{
    var party = inputs.LordParty;
    if (party == null)
    {
        return;
    }

    const float NearbyRadius = 50f; // in-game distance units; calibrated later

    int hostileCount = 0;
    int alliedCount = 0;
    float nearestHostileDistance = float.PositiveInfinity;
    float nearestHostileStrengthRatio = 0f;
    MobileParty nearestHostile = null;

    foreach (var candidate in MobileParty.AllLordParties)
    {
        if (candidate == null || candidate == party) continue;
        if (!candidate.IsActive || candidate.IsDisbanding) continue;

        var distance = candidate.Position2D.Distance(party.Position2D);
        if (distance > NearbyRadius) continue;

        bool isHostile = FactionManager.IsAtWarAgainstFaction(candidate.MapFaction, party.MapFaction);
        if (isHostile)
        {
            hostileCount++;
            if (distance < nearestHostileDistance)
            {
                nearestHostileDistance = distance;
                nearestHostile = candidate;
            }
        }
        else if (candidate.MapFaction == party.MapFaction)
        {
            alliedCount++;
        }
    }

    inputs.NearbyHostileCount = hostileCount;
    inputs.NearbyAlliedCount = alliedCount;
    inputs.NearestHostileDistance = hostileCount > 0 ? nearestHostileDistance : float.PositiveInfinity;
    if (nearestHostile != null && party.Party != null && party.Party.TotalStrength > 0f)
    {
        inputs.NearestHostileStrengthRatio = nearestHostile.Party.TotalStrength / party.Party.TotalStrength;
    }
    inputs.NearbyAlliedReliefPlausible = alliedCount >= 2;

    // Threatened friendly settlements: any friendly settlement within NearbyRadius
    // that is besieged OR hosts a hostile party inside the radius.
    int threatenedCount = 0;
    Settlement nearestThreatened = null;
    float nearestThreatenedDistance = float.PositiveInfinity;

    foreach (var settlement in Settlement.All)
    {
        if (settlement == null || settlement.MapFaction != party.MapFaction) continue;
        var sd = settlement.Position2D.Distance(party.Position2D);
        if (sd > NearbyRadius) continue;

        if (settlement.IsUnderSiege)
        {
            threatenedCount++;
            if (sd < nearestThreatenedDistance)
            {
                nearestThreatenedDistance = sd;
                nearestThreatened = settlement;
            }
        }
    }

    inputs.ThreatenedFriendlySettlementCount = threatenedCount;
    inputs.NearestThreatenedFriendly = nearestThreatened;
    inputs.FrontierHeatingUp = threatenedCount > 0 || hostileCount >= 3;
}
```

**API verification for T10:** confirm `MobileParty.AllLordParties`, `MobileParty.Position2D`, `MobileParty.IsActive`, `MobileParty.IsDisbanding`, `Party.TotalStrength`, `FactionManager.IsAtWarAgainstFaction`, `Settlement.All`, `Settlement.IsUnderSiege` all exist with these signatures. Patch inline; log drift in appendix.

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/SnapshotCollector.cs
git commit -F <message-file>
```

Message: `feat(intel): collect §7.2 strategic neighborhood (T10)`.

---

### Task T11: Collect §7.3 information evidence

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotCollector.cs`

Design: Phase 1 seed implementation — wire trivially-available inputs (recent war/peace/army transitions come from `_pendingChangeFlags`) and stub the richer tracks/buildup/garrison observations. Actual "read tracks" and "observed enemy buildup" live in Plan 4 (duty opportunities provide the player-observation surface). For Plan 1 these stay as `false` placeholders; the classifier degrades to medium/low `InformationConfidence` when they are all false.

- [ ] **Step 1: Replace the `CollectInformationEvidence` stub**

```csharp
private static void CollectInformationEvidence(ref IntelligenceInputs inputs, Hero lord)
{
    // §7.3 evidence. Most of the "rich" evidence (scout tracks, garrison observations,
    // prisoner stakes computed from specific heroes) arrives in Plan 4. For Plan 1,
    // wire the coarse signals that come for free:

    inputs.RecentWarTransition = (inputs.PendingFlagsAtTickStart & RecentChangeFlags.WarDeclared) != 0;
    inputs.RecentPeaceTransition = (inputs.PendingFlagsAtTickStart & RecentChangeFlags.MakePeace) != 0;
    inputs.RecentArmyFormation = (inputs.PendingFlagsAtTickStart & RecentChangeFlags.ArmyFormed) != 0;
    inputs.RecentArmyDispersal = (inputs.PendingFlagsAtTickStart & RecentChangeFlags.ArmyDispersed) != 0;

    // Important-prisoner count: count notable prisoners held by the enlisted lord's party.
    inputs.ImportantPrisonerCount = 0;
    var party = inputs.LordParty;
    if (party != null && party.PrisonRoster != null)
    {
        // PrisonRoster.Count / TotalManCount and foreach — verify exact API at decompile.
        for (int i = 0; i < party.PrisonRoster.Count; i++)
        {
            var element = party.PrisonRoster.GetElementCopyAtIndex(i);
            if (element.Character != null && element.Character.IsHero && element.Character.HeroObject != null)
            {
                var prisoner = element.Character.HeroObject;
                if (prisoner.Clan?.Leader == prisoner || prisoner.IsNoble)
                {
                    inputs.ImportantPrisonerCount++;
                }
            }
        }
    }

    // Fresh tracks / enemy buildup / garrison observation deferred to Plan 4.
    inputs.HasFreshTracks = false;
    inputs.HasEnemyBuildupEvidence = false;
    inputs.HasGarrisonObservation = false;
}
```

**API verification for T11:** confirm `MobileParty.PrisonRoster`, `TroopRoster.Count`, `TroopRoster.GetElementCopyAtIndex(int)`, `CharacterObject.IsHero`, `CharacterObject.HeroObject`, `Hero.IsNoble`, `Hero.Clan.Leader`.

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/SnapshotCollector.cs
git commit -F <message-file>
```

Message: `feat(intel): collect §7.3 information evidence seed (T11)`.

---

### Task T12: Collect §7.4 player relevance

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotCollector.cs`

- [ ] **Step 1: Replace the `CollectPlayerRelevance` stub**

```csharp
private static void CollectPlayerRelevance(ref IntelligenceInputs inputs, Hero lord)
{
    var player = Hero.MainHero;
    if (player == null || lord == null)
    {
        return;
    }

    // Player tier — from EnlistmentBehavior current rank. That is stored internally;
    // for Plan 1, read it via the public CurrentTier property.
    inputs.PlayerTier = Enlistment.Behaviors.EnlistmentBehavior.Instance?.CurrentTier ?? 0;
    inputs.PlayerRelationWithLord = (int)player.GetRelationWithPlayer(lord);
    // Player is proximate if in same party as lord OR within close range (cheap check
    // — the enlisted player party follows the lord closely by design, so this is
    // mostly true while enlisted).
    var playerParty = PartyBase.MainParty?.MobileParty;
    if (playerParty != null && inputs.LordParty != null)
    {
        var d = playerParty.Position2D.Distance(inputs.LordParty.Position2D);
        inputs.PlayerProximate = d < 3f;
    }
    else
    {
        inputs.PlayerProximate = true; // escort-AI default during enlistment
    }
}
```

**API verification for T12:**
- `EnlistmentBehavior.Instance.CurrentTier` — if the property is named differently (`Tier`, `Rank`), update to match; verify at `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` via grep.
- `Hero.GetRelationWithPlayer(Hero)` — on `Hero` class in decompile.
- `PartyBase.MainParty` — static.

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/SnapshotCollector.cs
git commit -F <message-file>
```

Message: `feat(intel): collect §7.4 player relevance (T12)`.

---

## Phase D — Classification

Each classifier task adds one or more `Classify*` static methods to `SnapshotClassifier.cs`. All classifiers are pure functions of `IntelligenceInputs` (+ sometimes the previous snapshot for delta detection). All return enum values defined in T2.

### Task T13: Classify `StrategicPosture`, `ObjectiveType`, `ObjectiveConfidence`

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotClassifier.cs`

- [ ] **Step 1: Flesh out classifier with the entry point + 3 methods**

```csharp
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Pure static classifier. Takes IntelligenceInputs + the previous snapshot (for
    /// delta detection) and produces a fresh snapshot with all enum fields set.
    /// No side effects.
    /// </summary>
    internal static class SnapshotClassifier
    {
        public static void Classify(
            ref IntelligenceInputs inputs,
            EnlistedLordIntelligenceSnapshot previous,
            EnlistedLordIntelligenceSnapshot output)
        {
            // Guard: lord not alive / not in party → leave defaults.
            if (!inputs.LordAlive || inputs.LordParty == null)
            {
                output.Clear();
                return;
            }

            output.Objective = ClassifyObjective(inputs);
            output.Posture = ClassifyPosture(inputs, output.Objective);
            output.ObjectiveConfidence = ClassifyObjectiveConfidence(inputs);
            // Remaining fields filled by T14-T18 methods; keep stubs resolving to defaults.
            output.FrontPressure = ClassifyFrontPressure(inputs);                       // T14
            output.ArmyStrain = ClassifyArmyStrain(inputs);                             // T15
            output.SupplyPressure = ClassifySupplyPressure(inputs);                     // T16
            output.PursuitViability = ClassifyPursuitViability(inputs);                 // T17
            output.EnemyContactRisk = ClassifyEnemyContactRisk(inputs);                 // T17
            output.RecoveryNeed = ClassifyRecoveryNeed(inputs);                         // T18
            output.PrisonerStakes = ClassifyPrisonerStakes(inputs);                     // T18
            output.PlayerTrustWindow = ClassifyPlayerTrustWindow(inputs);               // T18
            output.InformationConfidence = ClassifyInformationConfidence(inputs);       // T18
            output.RecentChanges = inputs.PendingFlagsAtTickStart | DetectDeltas(previous, output);
        }

        private static ObjectiveType ClassifyObjective(IntelligenceInputs inputs)
        {
            var party = inputs.LordParty;
            if (inputs.BesiegedSettlement != null)
            {
                return ObjectiveType.BesiegeSettlement;
            }
            if (inputs.ThreatenedFriendlySettlementCount > 0 && !inputs.InArmy)
            {
                return ObjectiveType.DefendSettlement;
            }
            if (inputs.ThreatenedFriendlySettlementCount > 0 && inputs.InArmy)
            {
                return ObjectiveType.RelieveSettlement;
            }
            if (inputs.ArmyIsGathering && !inputs.IsArmyLeader)
            {
                return ObjectiveType.GatherArmy;
            }
            if (party.DefaultBehavior == AiBehavior.EngageParty && inputs.NearbyHostileCount > 0)
            {
                return ObjectiveType.Pursue;
            }
            if (inputs.WoundedRatio > 0.3f || inputs.LordIsWounded)
            {
                return ObjectiveType.Recover;
            }
            if (party.ShortTermBehavior == AiBehavior.RaidSettlement
                || party.DefaultBehavior == AiBehavior.RaidSettlement)
            {
                return ObjectiveType.RaidVillage;
            }
            return ObjectiveType.Move;
        }

        private static StrategicPosture ClassifyPosture(IntelligenceInputs inputs, ObjectiveType objective)
        {
            switch (objective)
            {
                case ObjectiveType.BesiegeSettlement: return StrategicPosture.OffensiveSiege;
                case ObjectiveType.DefendSettlement:  return StrategicPosture.FrontierDefense;
                case ObjectiveType.RelieveSettlement: return StrategicPosture.Relief;
                case ObjectiveType.GatherArmy:        return StrategicPosture.ArmyGather;
                case ObjectiveType.Recover:           return StrategicPosture.Recovery;
                case ObjectiveType.RaidVillage:       return StrategicPosture.OpportunisticRaid;
                case ObjectiveType.Pursue:            return StrategicPosture.ShadowingEnemy;
            }
            if (inputs.CurrentSettlement != null && inputs.CurrentSettlement.IsFortification)
            {
                return StrategicPosture.GarrisonHold;
            }
            if (inputs.FoodDaysRemaining < 1.5f && inputs.WoundedRatio > 0.15f)
            {
                return StrategicPosture.Regroup;
            }
            return StrategicPosture.March;
        }

        private static ObjectiveConfidence ClassifyObjectiveConfidence(IntelligenceInputs inputs)
        {
            // Confidence is HIGH when the objective is unambiguous (settlement target present)
            // and the lord is not being reactive to new threats. MEDIUM when threats are
            // freshly detected but there's an objective. LOW in all noisy / reactive cases.
            if (inputs.BesiegedSettlement != null || inputs.NearestThreatenedFriendly != null)
            {
                if ((inputs.PendingFlagsAtTickStart & (RecentChangeFlags.WarDeclared | RecentChangeFlags.MapEventJoined)) != 0)
                {
                    return ObjectiveConfidence.Medium;
                }
                return ObjectiveConfidence.High;
            }
            if (inputs.NearbyHostileCount > 2) return ObjectiveConfidence.Low;
            return ObjectiveConfidence.Medium;
        }

        private static RecentChangeFlags DetectDeltas(
            EnlistedLordIntelligenceSnapshot previous,
            EnlistedLordIntelligenceSnapshot current)
        {
            if (previous == null) return RecentChangeFlags.None;
            var flags = RecentChangeFlags.None;
            if (previous.Objective != current.Objective) flags |= RecentChangeFlags.ObjectiveShift;
            if (current.FrontPressure > previous.FrontPressure) flags |= RecentChangeFlags.NewThreat;
            if ((previous.Objective != ObjectiveType.BesiegeSettlement) != (current.Objective != ObjectiveType.BesiegeSettlement))
            {
                flags |= RecentChangeFlags.SiegeTransition;
            }
            if (current.SupplyPressure >= SupplyPressure.Strained && previous.SupplyPressure < SupplyPressure.Strained)
            {
                flags |= RecentChangeFlags.SupplyBreak;
            }
            return flags;
        }

        // Stubs — filled by T14-T18.
        private static FrontPressure ClassifyFrontPressure(IntelligenceInputs _) => FrontPressure.None;
        private static ArmyStrainLevel ClassifyArmyStrain(IntelligenceInputs _) => ArmyStrainLevel.None;
        private static SupplyPressure ClassifySupplyPressure(IntelligenceInputs _) => SupplyPressure.None;
        private static PursuitViability ClassifyPursuitViability(IntelligenceInputs _) => PursuitViability.NotViable;
        private static EnemyContactRisk ClassifyEnemyContactRisk(IntelligenceInputs _) => EnemyContactRisk.Low;
        private static RecoveryNeed ClassifyRecoveryNeed(IntelligenceInputs _) => RecoveryNeed.None;
        private static PrisonerStakes ClassifyPrisonerStakes(IntelligenceInputs _) => PrisonerStakes.None;
        private static PlayerTrustWindow ClassifyPlayerTrustWindow(IntelligenceInputs _) => PlayerTrustWindow.None;
        private static InformationConfidence ClassifyInformationConfidence(IntelligenceInputs _) => InformationConfidence.Low;
    }
}
```

**API verification:** `AiBehavior` enum values (`EngageParty`, `RaidSettlement`) — confirm in decompile `TaleWorlds.CampaignSystem.Party/AiBehavior.cs`. `MobileParty.DefaultBehavior`, `MobileParty.ShortTermBehavior` — confirm property existence and types. `Settlement.IsFortification` — confirm.

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/SnapshotClassifier.cs
git commit -F <message-file>
```

Message: `feat(intel): classify StrategicPosture / Objective / Confidence (T13)`.

---

### Task T14: Classify `FrontPressure`

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotClassifier.cs:ClassifyFrontPressure`

- [ ] **Step 1: Replace the stub**

```csharp
private static FrontPressure ClassifyFrontPressure(IntelligenceInputs inputs)
{
    int score = 0;
    if (inputs.ThreatenedFriendlySettlementCount > 0) score += 2;
    if (inputs.ThreatenedFriendlySettlementCount > 2) score += 2;
    if (inputs.NearbyHostileCount >= 2) score += 1;
    if (inputs.NearbyHostileCount >= 5) score += 1;
    if (inputs.FrontierHeatingUp) score += 1;
    if (inputs.RecentWarTransition) score += 2;
    if (inputs.NearestHostileStrengthRatio > 1.5f && inputs.NearestHostileDistance < 15f) score += 2;

    if (score >= 7) return FrontPressure.Critical;
    if (score >= 5) return FrontPressure.High;
    if (score >= 3) return FrontPressure.Medium;
    if (score >= 1) return FrontPressure.Low;
    return FrontPressure.None;
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/SnapshotClassifier.cs
git commit -F <message-file>
```

Message: `feat(intel): classify FrontPressure (T14)`.

---

### Task T15: Classify `ArmyStrainLevel` (composite)

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotClassifier.cs:ClassifyArmyStrain`

Per §7.1: driven by cohesion + food + wounded ratio + morale proxies + attached-party condition + inactivity + prisoner burden. Plan 1 uses the directly-available inputs; morale-proxy and attached-party-condition refinements live in later plans if needed.

- [ ] **Step 1: Replace the stub**

```csharp
private static ArmyStrainLevel ClassifyArmyStrain(IntelligenceInputs inputs)
{
    if (!inputs.InArmy) return ArmyStrainLevel.None;

    int score = 0;
    if (inputs.ArmyCohesion < 50f) score += 1;
    if (inputs.ArmyCohesion < 25f) score += 2;
    if (inputs.FoodDaysRemaining < 3f) score += 1;
    if (inputs.FoodDaysRemaining < 1f) score += 2;
    if (inputs.WoundedRatio > 0.2f) score += 1;
    if (inputs.WoundedRatio > 0.4f) score += 2;
    if (inputs.PartySizeRatio < 0.7f) score += 1;
    if (inputs.ImportantPrisonerCount > 0) score += 1;

    if (score >= 7) return ArmyStrainLevel.Breaking;
    if (score >= 5) return ArmyStrainLevel.Severe;
    if (score >= 3) return ArmyStrainLevel.Elevated;
    if (score >= 1) return ArmyStrainLevel.Mild;
    return ArmyStrainLevel.None;
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/SnapshotClassifier.cs
git commit -F <message-file>
```

Message: `feat(intel): classify ArmyStrain composite (T15)`.

---

### Task T16: Classify `SupplyPressure`

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotClassifier.cs:ClassifySupplyPressure`

- [ ] **Step 1: Replace the stub**

```csharp
private static SupplyPressure ClassifySupplyPressure(IntelligenceInputs inputs)
{
    if (inputs.FoodDaysRemaining < 0.5f) return SupplyPressure.Critical;
    if (inputs.FoodDaysRemaining < 2f) return SupplyPressure.Strained;
    if (inputs.FoodDaysRemaining < 4f) return SupplyPressure.Tightening;
    return SupplyPressure.None;
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/SnapshotClassifier.cs
git commit -F <message-file>
```

Message: `feat(intel): classify SupplyPressure (T16)`.

---

### Task T17: Classify `PursuitViability` + `EnemyContactRisk`

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotClassifier.cs:ClassifyPursuitViability,ClassifyEnemyContactRisk`

- [ ] **Step 1: Replace both stubs**

```csharp
private static PursuitViability ClassifyPursuitViability(IntelligenceInputs inputs)
{
    if (inputs.NearbyHostileCount == 0) return PursuitViability.NotViable;
    if (inputs.NearestHostileDistance > 20f) return PursuitViability.Marginal;

    bool strongEnough = inputs.NearestHostileStrengthRatio < 1.3f;
    bool closeEnough = inputs.NearestHostileDistance < 10f;
    bool strained = inputs.FoodDaysRemaining < 2f || inputs.WoundedRatio > 0.35f;

    if (strongEnough && closeEnough && !strained) return PursuitViability.Strong;
    if (strongEnough && !strained) return PursuitViability.Viable;
    if (closeEnough && !strained) return PursuitViability.Viable;
    return PursuitViability.Marginal;
}

private static EnemyContactRisk ClassifyEnemyContactRisk(IntelligenceInputs inputs)
{
    if (inputs.InMapEvent) return EnemyContactRisk.High;
    if (inputs.NearbyHostileCount == 0) return EnemyContactRisk.Low;

    bool outnumbered = inputs.NearestHostileStrengthRatio > 1.3f;
    bool close = inputs.NearestHostileDistance < 8f;

    if (outnumbered && close) return EnemyContactRisk.High;
    if (outnumbered || close) return EnemyContactRisk.Medium;
    return EnemyContactRisk.Low;
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/SnapshotClassifier.cs
git commit -F <message-file>
```

Message: `feat(intel): classify PursuitViability + EnemyContactRisk (T17)`.

---

### Task T18: Classify `RecoveryNeed`, `PrisonerStakes`, `PlayerTrustWindow`, `InformationConfidence`

**Files:**
- Modify: `src/Features/CampaignIntelligence/SnapshotClassifier.cs`

- [ ] **Step 1: Replace all four stubs**

```csharp
private static RecoveryNeed ClassifyRecoveryNeed(IntelligenceInputs inputs)
{
    int score = 0;
    if (inputs.WoundedRatio > 0.15f) score++;
    if (inputs.WoundedRatio > 0.3f) score += 2;
    if (inputs.PartySizeRatio < 0.75f) score++;
    if (inputs.FoodDaysRemaining < 2f) score++;
    if (inputs.LordIsWounded) score++;

    if (score >= 4) return RecoveryNeed.High;
    if (score >= 3) return RecoveryNeed.Medium;
    if (score >= 1) return RecoveryNeed.Low;
    return RecoveryNeed.None;
}

private static PrisonerStakes ClassifyPrisonerStakes(IntelligenceInputs inputs)
{
    if (inputs.ImportantPrisonerCount >= 3) return PrisonerStakes.High;
    if (inputs.ImportantPrisonerCount == 2) return PrisonerStakes.Medium;
    if (inputs.ImportantPrisonerCount == 1) return PrisonerStakes.Low;
    return PrisonerStakes.None;
}

private static PlayerTrustWindow ClassifyPlayerTrustWindow(IntelligenceInputs inputs)
{
    int tier = inputs.PlayerTier;
    int relation = inputs.PlayerRelationWithLord;

    if (tier >= 7 && relation >= 30) return PlayerTrustWindow.Broad;
    if (tier >= 5 && relation >= 10) return PlayerTrustWindow.Moderate;
    if (tier >= 3 || relation >= 0) return PlayerTrustWindow.Narrow;
    return PlayerTrustWindow.None;
}

private static InformationConfidence ClassifyInformationConfidence(IntelligenceInputs inputs)
{
    int score = 0;
    if (inputs.HasFreshTracks) score++;
    if (inputs.HasEnemyBuildupEvidence) score++;
    if (inputs.HasGarrisonObservation) score++;
    if (inputs.PlayerProximate && inputs.PlayerTier >= 5) score++;
    // Recent war/peace transitions reduce confidence — the lord's prior objective may be stale.
    if (inputs.RecentWarTransition || inputs.RecentPeaceTransition) score--;

    if (score >= 3) return InformationConfidence.High;
    if (score >= 1) return InformationConfidence.Medium;
    return InformationConfidence.Low;
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/SnapshotClassifier.cs
git commit -F <message-file>
```

Message: `feat(intel): classify RecoveryNeed / PrisonerStakes / PlayerTrustWindow / InformationConfidence (T18)`.

---

## Phase E — Tick + change flags

### Task T19: Hourly-tick recompute wiring

**Files:**
- Modify: `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs`

- [ ] **Step 1: Subscribe to `HourlyTickEvent` and call collector+classifier**

Extend `RegisterEvents`:

```csharp
public override void RegisterEvents()
{
    EnlistmentBehavior.OnEnlisted += HandleOnEnlisted;
    EnlistmentBehavior.OnEnlistmentEnded += HandleOnEnlistmentEnded;
    CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, HandleHourlyTick);
}
```

Add the handler:

```csharp
private void HandleHourlyTick()
{
    if (EnlistmentBehavior.Instance == null || !EnlistmentBehavior.Instance.IsEnlisted)
    {
        return;
    }

    var lord = EnlistmentBehavior.Instance.EnlistedLord;
    if (lord == null)
    {
        ModLogger.Expected("INTEL", "no_enlisted_lord", "hourly tick with IsEnlisted but EnlistedLord == null");
        return;
    }

    try
    {
        EnsureInitialized();

        var pending = _pendingChangeFlags;
        _pendingChangeFlags = RecentChangeFlags.None;

        var inputs = SnapshotCollector.Collect(lord, pending);

        var fresh = new EnlistedLordIntelligenceSnapshot();
        fresh.Clear();
        SnapshotClassifier.Classify(ref inputs, _working, fresh);
        fresh.LastRefreshed = CampaignTime.Now;

        // Age-track newly-set flags so the daily decay in T20 can clear stale bits.
        var newBits = fresh.RecentChanges & ~_working.RecentChanges;
        foreach (RecentChangeFlags bit in System.Enum.GetValues(typeof(RecentChangeFlags)))
        {
            if (bit == RecentChangeFlags.None) continue;
            if ((newBits & bit) != 0)
            {
                _flagFirstSeen[bit] = CampaignTime.Now;
            }
        }

        _working = fresh;

        ModLogger.Expected("INTEL", "hourly_recompute",
            "tick posture=" + fresh.Posture
            + " obj=" + fresh.Objective
            + " front=" + fresh.FrontPressure
            + " strain=" + fresh.ArmyStrain
            + " supply=" + fresh.SupplyPressure
            + " flags=" + fresh.RecentChanges);
    }
    catch (System.Exception ex)
    {
        ModLogger.Caught("INTEL", "hourly_recompute_failed", ex);
    }
}
```

Note: `ModLogger.Expected(category, key, summary)` — all three are strings. `summary` may be a string-literal + runtime-concat expression (matches `src/Features/Activities/ActivityTypeCatalog.cs:23-26`). The registry scanner captures the leading literal fragment as the summary. Do NOT use `$"..."` interpolation (rejected by the scanner — CLAUDE.md pitfall #20) and do NOT pass `ctx:` as an anonymous object (the parameter expects `IDictionary<string, object>`).

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Regen error-codes + commit**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
git add src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs docs/error-codes.md
git commit -F <message-file>
```

Message: `feat(intel): hourly-tick recompute (T19)`.

---

### Task T20: Daily decay of `RecentChangeFlags` bits

**Files:**
- Modify: `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs`

- [ ] **Step 1: Subscribe to `DailyTickEvent` and decay stale bits**

Extend `RegisterEvents`:

```csharp
CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, HandleDailyTick);
```

Add:

```csharp
private void HandleDailyTick()
{
    if (_working == null) return;

    var now = CampaignTime.Now;
    var decayCutoff = now - CampaignTime.Days(1);
    var stale = RecentChangeFlags.None;

    foreach (var kv in _flagFirstSeen)
    {
        if (kv.Value < decayCutoff)
        {
            stale |= kv.Key;
        }
    }

    if (stale != RecentChangeFlags.None)
    {
        _working.RecentChanges &= ~stale;
        foreach (RecentChangeFlags bit in System.Enum.GetValues(typeof(RecentChangeFlags)))
        {
            if (bit == RecentChangeFlags.None) continue;
            if ((stale & bit) != 0)
            {
                _flagFirstSeen.Remove(bit);
            }
        }
    }
}
```

Note: `CampaignTime.Days(1)` and `CampaignTime` operators — verify in decompile `TaleWorlds.CampaignSystem/CampaignTime.cs`.

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs
git commit -F <message-file>
```

Message: `feat(intel): daily decay of RecentChangeFlags (T20)`.

---

### Task T21: Event-driven invalidation handlers

**Files:**
- Modify: `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs`

- [ ] **Step 1: Subscribe to the five invalidation events**

Extend `RegisterEvents`:

```csharp
CampaignEvents.WarDeclared.AddNonSerializedListener(this, HandleWarDeclared);
CampaignEvents.MakePeace.AddNonSerializedListener(this, HandleMakePeace);
CampaignEvents.ArmyCreated.AddNonSerializedListener(this, HandleArmyCreated);
CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, HandleArmyDispersed);
CampaignEvents.MapEventStarted.AddNonSerializedListener(this, HandleMapEventStarted);
```

Add handlers — each ONLY sets bits on `_pendingChangeFlags` if the event is relevant to the enlisted lord:

```csharp
private void HandleWarDeclared(IFaction side1, IFaction side2, DeclareWarAction.DeclareWarDetail _)
{
    if (!IsRelevantFaction(side1) && !IsRelevantFaction(side2)) return;
    _pendingChangeFlags |= RecentChangeFlags.WarDeclared | RecentChangeFlags.NewThreat;
}

private void HandleMakePeace(IFaction side1, IFaction side2, MakePeaceAction.MakePeaceDetail _)
{
    if (!IsRelevantFaction(side1) && !IsRelevantFaction(side2)) return;
    _pendingChangeFlags |= RecentChangeFlags.MakePeace;
}

private void HandleArmyCreated(Army army)
{
    if (army?.LeaderParty?.LeaderHero == EnlistmentBehavior.Instance?.EnlistedLord
        || army?.Parties?.Any(p => p?.LeaderHero == EnlistmentBehavior.Instance?.EnlistedLord) == true)
    {
        _pendingChangeFlags |= RecentChangeFlags.ArmyFormed | RecentChangeFlags.ObjectiveShift;
    }
}

private void HandleArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayerArmy)
{
    if (!WasEnlistedLordArmy(army)) return;
    _pendingChangeFlags |= RecentChangeFlags.ArmyDispersed | RecentChangeFlags.DispersalRisk;
}

private void HandleMapEventStarted(MapEvent ev, PartyBase attacker, PartyBase defender)
{
    var lordParty = EnlistmentBehavior.Instance?.EnlistedLord?.PartyBelongedTo?.Party;
    if (lordParty == null) return;
    if (attacker == lordParty || defender == lordParty)
    {
        _pendingChangeFlags |= RecentChangeFlags.MapEventJoined | RecentChangeFlags.NewThreat;
    }
}

private static bool IsRelevantFaction(IFaction faction)
{
    var lord = EnlistmentBehavior.Instance?.EnlistedLord;
    if (lord == null || faction == null) return false;
    return faction == lord.MapFaction || faction == lord.Clan?.Kingdom;
}

private static bool WasEnlistedLordArmy(Army army)
{
    // Army.Parties may already be cleared when dispersed fires; check LeaderParty if available.
    var lord = EnlistmentBehavior.Instance?.EnlistedLord;
    if (lord == null || army == null) return false;
    return army.LeaderParty?.LeaderHero == lord;
}
```

Required `using` additions:
```csharp
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
```

**API verification:** `IFaction`, `DeclareWarAction.DeclareWarDetail`, `MakePeaceAction.MakePeaceDetail`, `Army.ArmyDispersionReason`, `Army.LeaderParty`, `Army.Parties`, `MobileParty.LeaderHero`, `PartyBase.LeaderHero`. Confirm signatures at decompile before final commit.

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs
git commit -F <message-file>
```

Message: `feat(intel): event-driven invalidation — war/peace/army/mapevent (T21)`.

---

### Task T22: Verify dirty-bit consumption in hourly recompute

**Files:** verification-only.

The `HandleHourlyTick` in T19 already reads `_pendingChangeFlags` and clears it. The classifier `DetectDeltas` (T13) OR's those bits into the output. Task T22 is a read-through audit: confirm the pending→consumed→cleared data path is correct.

- [ ] **Step 1: Trace the path**

Read `HandleHourlyTick` in `EnlistedCampaignIntelligenceBehavior.cs`. Confirm the order:
1. `var pending = _pendingChangeFlags;` — snapshot bits locally.
2. `_pendingChangeFlags = RecentChangeFlags.None;` — clear immediately.
3. `SnapshotCollector.Collect(lord, pending)` — pending rides into inputs.
4. Collector's §7.3 reads those flags (T11).
5. Classifier `DetectDeltas` OR's into snapshot output bits.

If the order is wrong (e.g., clearing `_pendingChangeFlags` AFTER `Collect`), any event arriving between Collect and clear would be lost. The order above is safe.

- [ ] **Step 2: Read the code to confirm, then mark task done**

No code change unless the audit reveals a bug.

- [ ] **Step 3: Commit** — only if a fix was needed. Otherwise skip commit; mark task complete.

---

## Phase F — Persistence

### Task T23: `SyncData` + `EnsureInitialized` pattern

**Files:**
- Modify: `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs`

Per §12: persist compact state only. The snapshot itself is compact (14 fields, all enums / primitives / one CampaignTime). Recompute volatile fields on load — i.e., we DO persist the snapshot (so the first post-load tick sees prior posture), but the next hourly recompute overwrites it from live world state immediately.

- [ ] **Step 1: Implement SyncData + EnsureInitialized for load-correctness**

```csharp
public override void SyncData(IDataStore dataStore)
{
    dataStore.SyncData("EnlistedIntel._working", ref _working);
    // _pendingChangeFlags: persist because events can fire between tick and save,
    // and losing those bits on load would mean missing a recent change the player
    // has not yet seen surfaced.
    int pendingInt = (int)_pendingChangeFlags;
    dataStore.SyncData("EnlistedIntel._pendingChangeFlags", ref pendingInt);
    _pendingChangeFlags = (RecentChangeFlags)pendingInt;

    // _flagFirstSeen: Dictionary<RecentChangeFlags, CampaignTime> — container not
    // registered on definer. Serialize as parallel List<int> keys + List<CampaignTime>
    // values to avoid adding a new container registration JUST for the decay tracker.
    EnsureInitialized();
    var flagKeys = new System.Collections.Generic.List<int>();
    var flagTimes = new System.Collections.Generic.List<CampaignTime>();
    if (dataStore.IsSaving)
    {
        foreach (var kv in _flagFirstSeen)
        {
            flagKeys.Add((int)kv.Key);
            flagTimes.Add(kv.Value);
        }
    }
    dataStore.SyncData("EnlistedIntel._flagKeys", ref flagKeys);
    dataStore.SyncData("EnlistedIntel._flagTimes", ref flagTimes);
    if (dataStore.IsLoading)
    {
        _flagFirstSeen.Clear();
        int n = System.Math.Min(flagKeys?.Count ?? 0, flagTimes?.Count ?? 0);
        for (int i = 0; i < n; i++)
        {
            _flagFirstSeen[(RecentChangeFlags)flagKeys[i]] = flagTimes[i];
        }
    }

    EnsureInitialized();
}

internal void EnsureInitialized()
{
    if (_working == null)
    {
        _working = new EnlistedLordIntelligenceSnapshot();
        _working.Clear();
    }
}
```

Note on the parallel-list trick: registering one more Dictionary container (`Dictionary<RecentChangeFlags, CampaignTime>`) in `EnlistedSaveDefiner` would be cleaner but consumes another definer slot. The parallel-list encoding is load-safe, only costs 12 bytes per flag bit, and avoids definer churn. `List<int>` and `List<CampaignTime>` — check `EnlistedSaveDefiner.DefineContainerDefinitions()` to confirm both containers are already registered. `List<int>` likely is NOT — add a single line to the definer:

```csharp
// Inside DefineContainerDefinitions():
ConstructContainerDefinition(typeof(System.Collections.Generic.List<int>));
ConstructContainerDefinition(typeof(System.Collections.Generic.List<CampaignTime>));
```

(only if not already present — grep first).

- [ ] **Step 2: Build**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS**.

- [ ] **Step 3: Commit**

```bash
git add src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
git commit -F <message-file>
```

Message: `feat(intel): SyncData + EnsureInitialized persistence (T23)`.

---

### Task T24: Enlistment-end clears state

**Files:** verification-only for T6's `HandleOnEnlistmentEnded` — confirm it clears everything.

- [ ] **Step 1: Read the handler**

Open `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs` and find `HandleOnEnlistmentEnded`. Confirm it:
1. Calls `_working?.Clear()` (leaves snapshot object but resets fields).
2. Sets `_pendingChangeFlags = RecentChangeFlags.None`.
3. Clears `_flagFirstSeen`.

Also confirm `Current` getter returns `null` when `!IsEnlisted`, even if `_working` is non-null. That's the contract.

- [ ] **Step 2: Add a failsafe**

If the code in T6 nulled `_working` instead of clearing, consumers re-entering the `Current` accessor during tear-down could see a stale struct. The T6 handler uses `_working?.Clear()` which preserves the ref — that's correct. Confirm; no code change unless audit finds a bug.

- [ ] **Step 3: Commit** — skip if no code change.

---

## Phase G — Observability + error handling

### Task T25: ModLogger pass — confirm severity conventions

**Files:** read-only audit.

- [ ] **Step 1: Audit every ModLogger call in the new code**

Grep:

```
Grep: 'ModLogger\.(Surfaced|Caught|Expected)' in src/Features/CampaignIntelligence/
```

Confirm for each call:
- Category is a **string literal** (not an interpolated string or constant).
- Summary is a **string literal**.
- Severity is correct:
  - `Surfaced` — player- or dev-visible failures that break the backbone (collector init, enlistment-start init).
  - `Caught` — defensive catches during tick (hourly recompute, flag-clear-on-end). Player never sees; log only.
  - `Expected` — known benign branches ("no enlisted lord on tick", "hourly_recompute" heartbeat).

Fix any drift inline.

- [ ] **Step 2: Confirm no `ModLogger.Error`**

```
Grep: 'ModLogger\.Error' in src/Features/CampaignIntelligence/
```

Expected: zero matches. `.Error` was retired on 2026-04-19.

- [ ] **Step 3: Commit** — only if fixes were needed.

---

### Task T26: Regenerate error-codes registry

**Files:**
- Modify: `docs/error-codes.md` (auto-generated)

- [ ] **Step 1: Run the generator**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

- [ ] **Step 2: Confirm new INTEL codes landed**

```
Grep: 'INTEL' in docs/error-codes.md
```

Expected matches: `enlist_init_failed`, `clear_on_end_failed`, `no_enlisted_lord`, `hourly_recompute`, `hourly_recompute_failed`, `collector_failed`.

- [ ] **Step 3: Stage and commit**

```bash
git add docs/error-codes.md
git commit -F <message-file>
```

Message: `chore(intel): regenerate error-codes registry after T19-T23 logs (T26)`.

(If no diff, skip the commit — registry already current from T19's regen.)

---

## Phase H — Smoke verification + sign-off

### Task T27: Full build + content-validator pass

- [ ] **Step 1: Build clean**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: **PASS** with 0 warnings in the `CampaignIntelligence` folder.

- [ ] **Step 2: Run the content validator**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: **PASS** with 0 errors. (Phase 10 validates the error-codes registry against `(category, file, line)` triples in the source — if line numbers shifted in any file with `ModLogger.Surfaced`/`Caught`/`Expected`, regen at T26 should have handled it; re-run the generator + validator if Phase 10 fails.)

- [ ] **Step 3: Run the repo lint stack**

```bash
./Tools/Validation/lint_repo.ps1
```

Expected: all 4 phases pass (.editorconfig / content validators / Ruff / PSScriptAnalyzer).

- [ ] **Step 4: Commit** — no staged changes expected. Skip if `git status` is clean.

---

### Task T28: In-game smoke test plan

**Purpose:** confirm the backbone runs without crashing, snapshot fields populate correctly, and enlistment-end clears state. No unit tests; the verification is session-log evidence.

- [ ] **Step 1: Close `BannerlordLauncher.exe`; launch the game fresh**

- [ ] **Step 2: Start a new campaign; play to the point of enlisting with a lord**

Open the mod session log in `...\Modules\Enlisted\Debugging\Session-A_*.log` after enlist. Expect:

- One `E-INTEL-NNN enlist_init_failed` entry? → **bug** — investigate.
- Session log shows `hourly_recompute` (Expected, category INTEL) entries at roughly one-per-in-game-hour cadence — **expected**.
- Each heartbeat's context includes `posture`, `obj`, `front`, `strain`, `supply`, `flags` — **expected**.

- [ ] **Step 3: Provoke a war declaration against the enlisted lord's faction**

Use campaign cheats (if available) or play through. At the next hourly tick, the `flags` field in the `hourly_recompute` log entry should include `WarDeclared, NewThreat`.

- [ ] **Step 4: Provoke an army formation the enlisted lord joins**

At the next hourly tick, `flags` should include `ArmyFormed, ObjectiveShift`. The `posture` field should shift to `ArmyGather`.

- [ ] **Step 5: Save + reload the campaign**

Re-open the session log. Expect no exceptions at load time. The first post-load `hourly_recompute` heartbeat should re-populate the snapshot from live world state (fields may differ from pre-save if world state moved during save/load).

- [ ] **Step 6: End enlistment (discharge, fire, retire)**

At the next hourly tick, the `hourly_recompute` log entry should NOT appear (gated by `IsEnlisted`). The `Current` accessor (if probed via debug tool) should return `null`.

- [ ] **Step 7: Sign-off**

If all six steps pass, Plan 1 is complete. Record the session-log excerpt in the PR description for reviewers.

- [ ] **Step 8: No commit** — verification only.

---

### Task T29: API corrections appendix (maintain through implementation)

**Files:**
- Modify: this plan's "API Corrections Appendix" section below.

Every implementer subagent MUST verify prescribed APIs against `../Decompile/` before writing the call. When drift is found, append an entry here inline. Pattern:

```
### T<task>: <member>

**Prescribed:** <plan code>
**Actual in decompile:** <real signature>
**Patch applied:** <one-sentence description + commit hash>
```

- [ ] **Step 1: After each task with a code block referencing a TaleWorlds API, run the API verification step described in that task.**

- [ ] **Step 2: If drift is found, patch the code inline AND add an entry to the appendix.**

- [ ] **Step 3: No commit solely for appendix edits** — fold them into the same commit as the code fix.

---

## API Corrections Appendix

### T3: save-attribute convention

**Prescribed:** `[Serializable]` POCO with `[SaveableProperty(N)]` attributes on every property; `using TaleWorlds.SaveSystem;`.
**Actual in this mod:** `[Serializable]`-only classes without `[SaveableProperty]` attributes. Fields serialize via the owning `CampaignBehaviorBase.SyncData` calling `dataStore.SyncData(key, ref obj)` — TaleWorlds walks public fields by reflection. See `src/Features/Qualities/QualityStore.cs:15` and `src/Features/Flags/FlagStore.cs:13` for the idiom.
**Patch applied:** authored `EnlistedLordIntelligenceSnapshot` without `[SaveableProperty]` + dropped `using TaleWorlds.SaveSystem;` (commit `56bea2f`).

### T9: `MobileParty.LimitedPartySize`

**Prescribed:** `party.LimitedPartySize` (int).
**Actual in decompile:** does not exist. `MobileParty.PartySizeRatio` (float) is the native property that pre-computes members/limit.
**Patch applied:** read `party.PartySizeRatio` directly into `inputs.PartySizeRatio` (commit `fe037b7`).

### T9: `MobileParty.GetTotalFoodAtInventory()`

**Prescribed:** method returning daily food consumption.
**Actual in decompile:** method does not exist. `MobileParty.FoodChange` (float) is the daily net delta (negative = net consumption).
**Patch applied:** `foodChange < -0.001f ? party.Food / -foodChange : +Infinity` with epsilon guard against near-zero divisions (commit `fe037b7`).

### T9: `PartyRoster.TotalManCount` / `TotalWounded`

**Prescribed:** type `PartyRoster`.
**Actual in decompile:** the type is `TroopRoster` at `Roster/TroopRoster.cs:59,61`.
**Patch applied:** references corrected to `TroopRoster` (commit `fe037b7`).

### T10: `MobileParty.Position2D` / `Settlement.Position2D`

**Prescribed:** property access `party.Position2D`.
**Actual in decompile:** `GetPosition2D` (expression-bodied property `=> Position.ToVec2()` at `MobileParty.cs:1019` and `Settlement.cs:207`).
**Patch applied:** call sites use `party.GetPosition2D.Distance(...)` — property access, no parens (commit `96efc5a`).

### T10: `PartyBase.TotalStrength`

**Prescribed:** property on PartyBase.
**Actual in decompile:** renamed to `PartyBase.EstimatedStrength`.
**Patch applied:** nearest-hostile strength-ratio uses `nearest.Party.EstimatedStrength / party.Party.EstimatedStrength` (commit `96efc5a`).

### T12: `EnlistmentBehavior.CurrentTier`

**Prescribed:** `EnlistmentBehavior.Instance?.CurrentTier`.
**Actual:** the property is `EnlistmentBehavior.EnlistmentTier` at `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:686`.
**Patch applied:** `inputs.PlayerTier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 0` (commit `5357311`).

### T12: `Hero.GetRelationWithPlayer(Hero)`

**Prescribed:** `player.GetRelationWithPlayer(lord)` returning the relation.
**Actual in decompile:** `Hero.GetRelationWithPlayer()` is parameterless (returns float, self-to-main-hero). The correct int-returning symmetric method is `Hero.GetRelation(Hero)` at `Hero.cs:2025`.
**Patch applied:** `inputs.PlayerRelationWithLord = lord.GetRelation(player)` (commit `5357311`).

### T11: `Hero.IsNoble`

**Prescribed:** `prisoner.IsNoble` on Hero.
**Actual in decompile:** property does not exist on `Hero`. `Clan.IsNoble` is the only noble flag (member of noble clan).
**Patch applied:** important-prisoner check uses `prisoner.Clan?.IsNoble == true` (commit `5357311`).

### T13: first-tick `ObjectiveShift` artifact (design decision)

**Prescribed:** `DetectDeltas(previous, current)` returns `None` on `previous == null`; otherwise computes deltas.
**Issue discovered during implementation:** after `EnsureInitialized` seats `_working` with `Clear()` defaults, the first real hourly tick compares live state against those defaults and spuriously fires `ObjectiveShift`, `NewThreat`, `SiegeTransition`, `SupplyBreak`.
**Resolution (user-approved):** added `private bool _primedOnce` to the behavior. T19's `HandleHourlyTick` passes `_primedOnce ? _working : null` as `previous` to `Classify`. Reset to `false` in both enlist/end handlers; set to `true` after first successful classify. Persisted across save/load per T23 (commit `f935dc5`).
**Idiom mirror:** `EnlistmentBehavior._initializationTicksRemaining = 5` (existing skip-first-N-ticks pattern for post-load settling).

---

## Self-review log (plan author)

Completed before handoff:

1. **Spec coverage:**
   - §5 core loop → T19 (hourly recompute wiring) drives the loop; T21 provides event-driven invalidation.
   - §6.1 behavior responsibilities → T5 (Instance + Current), T6 (lifecycle), T19 (tick), T23 (persist), T24 (clear-on-end).
   - §6.1 snapshot field families → T2 (enums), T3 (class), T13-T18 (classifiers).
   - §7.1 / §7.2 / §7.3 / §7.4 → T9 / T10 / T11 / T12.
   - §8 lord logic — **OUT OF SCOPE** (Plan 2). Not covered here.
   - §9 duty surfaces — **OUT OF SCOPE** (Plan 4).
   - §10 story and routing — **OUT OF SCOPE** (Plan 3).
   - §11 pacing — partially in T20 (daily decay keeps change-flags quiet after 1 day); finer pacing is a Plan 3/4 concern.
   - §12 persistence — T23, T24.
   - §13 error handling — T6 / T19 / T25.
   - §14 verification strategy — T1 (offset gate), T9-T12 (API checks), T28 (in-game smoke).

2. **Placeholder scan:** every step has concrete code or a concrete command. "TBD" / "TODO" / "implement later" do not appear in any task body.

3. **Type consistency:**
   - `EnlistedLordIntelligenceSnapshot` methods: `Clear()` — used consistently in T3, T6, T19, T24.
   - `EnlistedCampaignIntelligenceBehavior.Current` — used consistently in T5 and referenced in downstream plan docs.
   - `SnapshotClassifier.Classify(ref IntelligenceInputs, prev, output)` — signature consistent in T13, T19.
   - `SnapshotCollector.Collect(Hero, RecentChangeFlags)` — consistent in T9-T12, T19.
   - Enum names: `StrategicPosture`, `ObjectiveType`, `ObjectiveConfidence`, `FrontPressure`, `ArmyStrainLevel`, `SupplyPressure`, `InformationConfidence`, `EnemyContactRisk`, `PursuitViability`, `RecoveryNeed`, `PrisonerStakes`, `PlayerTrustWindow`, `RecentChangeFlags` — used consistently from T2 through T23.

---

## Handoff to next plan

When Plan 1 signs off (T28 green):

- **Plan 2 (AI intervention)** can begin. It reads `EnlistedCampaignIntelligenceBehavior.Instance?.Current` inside model wrappers (`TargetScoreCalculatingModel`, `ArmyManagementCalculationModel`, `MobilePartyAIModel`) and narrow Harmony patches. Plan 2 does NOT modify the snapshot shape; if it needs new fields, it files them as Plan-1-follow-up tasks.
- **Plan 3 (signal projection)** consumes the snapshot in `EnlistedCampaignSignalBuilder`, adds `SignalKind/Confidence/Perspective/Recency` to `StoryCandidate`, and routes through `StoryDirector.EmitCandidate`. Plan 3 depends on Plan 1 only (not Plan 2).
- **Plan 4 (duty opportunities)** consumes the snapshot in `EnlistedDutyOpportunityBuilder`, gates storylet emission with snapshot-backed triggers, and proposes arc-scale opportunities into `OrderActivity` via the existing Spec 2 pipeline. Depends on Plan 1 only.

Plans 2, 3, 4 can proceed in parallel once Plan 1 ships — this is the point of isolating the backbone first.
