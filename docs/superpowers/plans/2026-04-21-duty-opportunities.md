# Duty Opportunities Implementation Plan (Plan 4 of 5)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Integration context:** This is Plan 4 in the five-plan integration roadmap — see [docs/superpowers/specs/2026-04-21-plans-integration-design.md](../specs/2026-04-21-plans-integration-design.md). Plans 1-5: (1) Campaign Intelligence Backbone; (2) Lord AI Intervention; (3) Signal Projection; **(4) Duty Opportunities — this plan**; (5) Career Loop Closure. Plan 4 reads Plan 1's `EnlistedCampaignIntelligenceBehavior.Current` accessor and generates believable soldier-facing duty opportunities — ambient storylet emissions for camp texture, prompted Modal duties for actionable work, and transition-interrupt storylets fired when duty profiles change mid-arc.

**Goal:** Build an enlisted-only duty-opportunity layer that turns Plan 1's snapshot into soldier-scaled work the player can experience. Two opportunity shapes per design spec §6.1 "Opportunity scale" — Episodic (storylet flow, no durable activity) and ArcScale (proposes a named order via the existing `OrderActivity` + `NamedOrderState` pipeline). Absorbs all remaining Orders-era duty content — six duty-profile ambient pools (~110 storylets), 15 transition storylets, imprisoned-profile + lord-state edges, Phase 13 validator (pool coverage), and Phase B smoke verification.

**Architecture:** One `CampaignBehaviorBase` (`EnlistedDutyEmitterBehavior`) owns hourly emission of ambient episodic duties. A pure `EnlistedDutyOpportunityBuilder` projects snapshot + flags into a `List<DutyOpportunity>` — one tick, one pass, no side effects. The emitter picks duties from the appropriate profile pool (gated by current duty profile + snapshot state + trigger evaluation), throttles through `OrdersNewsFeedThrottle.TryClaim()`, and emits via `StoryDirector.EmitCandidate` at `StoryTier.Log` for ambient texture or `StoryTier.Modal` for interactive duty prompts. Arc-scale opportunities route through the existing `NamedOrderArcRuntime.SpliceArc` after player accept. Transition-interrupt storylets fire from `DutyProfileBehavior.CommitTransition` when a profile changes while `OrderActivity.ActiveNamedOrder != null` (integration spec §5.3 resolution). A small extension to `TriggerRegistry` adds snapshot-backed predicates (`snapshot_supply_pressure_gte:N`, `snapshot_front_pressure_gte:N`, `snapshot_army_strain_gte:N`, `snapshot_recent_change:<flag>`) so authored content can gate on snapshot state directly.

**Tech Stack:** C# 7.3 / .NET Framework 4.7.2, Mount & Blade II: Bannerlord `CampaignBehaviorBase` + `SaveableTypeDefiner` + `CampaignEvents` + Enlisted `ModLogger` / `EnlistmentBehavior` / `StoryDirector` / `OrdersNewsFeedThrottle` / `OrderActivity` / `NamedOrderArcRuntime` / `DutyProfileBehavior` / storylet backbone (`StoryletCatalog`, `TriggerRegistry`, `EffectExecutor`).

**Scope boundary — what this plan does NOT do:**

- No AI model wrappers, no Harmony patches against vanilla behavior loops. Reserved for Plan 2.
- No signal projection (`rumor`, `camp_talk`, `scout_return`, etc.). Reserved for Plan 3.
- No path crossroads, `committed_path` wiring, or path-variant named-order authoring. Reserved for Plan 5.
- No culture or lord-trait overlays on authored content. Those are Plan 5 polish.
- No modifications to Plan 1's snapshot or `RecentChangeFlags`. Plan 1 is frozen.
- No new enacted-duty state machine. Per design spec §6.1 opportunity-scale rule, opportunities resolve into Episodic (storylet-only) or ArcScale (existing `OrderActivity`). Plan 4 does not introduce a third durable state.

**Verification surface:** `dotnet build` + `validate_content.py` + in-game smoke via `ModLogger` heartbeat traces + news-feed spot-check. Over a 14 in-game day run covering all seven duty profiles (garrisoned → marching → besieging → raiding → escorting → wandering → imprisoned), verify: (a) every profile receives at least one ambient-duty emission; (b) snapshot-gated storylets (e.g. ration-tension when `SupplyPressure >= Strained`) fire only under matching state; (c) arc-scale Modal prompts become named orders via existing accept flow; (d) transitions fire correctly when profile changes mid-arc; (e) all 18 skill axes receive XP across the run; (f) Phase 13 validator passes (≥ 5 covering storylets per (profile, skill) cell).

---

## Design decisions locked before task 1

### Episodic vs arc-scale rule (design spec §6.1)

- **Episodic**: resolves inside a single storylet flow (or a short flag-gated chain via `FlagStore` short windows). Fires via `StoryDirector.EmitCandidate(StoryTier.Log)` for ambient texture, `StoryTier.Modal` for interactive duty prompts. No `OrderActivity.ActiveNamedOrder` created. ~90% of Plan 4 content authoring.
- **Arc-scale**: player accepts a Modal duty prompt → `NamedOrderArcRuntime.SpliceArc` creates `NamedOrderState`, splicing arc phases onto `OrderActivity`. Uses the already-shipped named-order archetypes (`order_escort`, `order_scout`, `order_siege_works`, `order_treat_wounded`, `order_inspection`, etc.). Plan 4's role is choosing which archetype to propose based on snapshot state, not authoring new archetypes.
- A storylet that discovers mid-flow it wants to become durable MUST escalate by proposing a named order through the existing Spec 2 pipeline, not by instantiating its own activity.

### Tick cadence

- **Baseline: `CampaignEvents.HourlyTickEvent`.** One global tick per in-game hour. The emitter reads the snapshot, invokes `DutyOpportunityBuilder`, throttle-claims, picks one opportunity from the prioritized list, emits.
- Cadence matches Plan 3's signal emitter — same throttle semantics. Plans 3 and 4 compete for the same 20s wall-clock throttle slot by design; the wall-clock ceiling is global, not per-source.

### Snapshot-backed trigger predicates (Phase B.5, NEW grammar)

Plan 1's snapshot fields are enum values on a POCO, not qualities or flags. Existing `TriggerRegistry` predicates (`is_enlisted`, `flag:X`, `quality_gte:X:N`, `rank_gte:N`, etc.) don't reach the snapshot. Plan 4 extends the registry with four new predicates:

- `snapshot_supply_pressure_gte:N` (N maps to `SupplyPressure` ordinal: 0=None, 1=Tightening, 2=Strained, 3=Critical)
- `snapshot_army_strain_gte:N` (ordinal into `ArmyStrainLevel`)
- `snapshot_front_pressure_gte:N` (ordinal into `FrontPressure`)
- `snapshot_recent_change:<flag_name>` (boolean — flag is set on current snapshot)

All four support the `not:` prefix for negation (existing registry convention). Unknown predicates continue to return false + log `Expected`. Authored content CAN mix snapshot predicates with existing predicates (`trigger: ["is_enlisted", "profile_requires_implicit", "snapshot_supply_pressure_gte:2"]`).

### Cooldown tracking

Per-storylet cooldown tracked in a new `DutyCooldownStore` POCO — same pattern as Plan 3's `SignalEmissionRecord`. Persisted by the emitter's `SyncData`. Default cooldown 36 in-game hours; storylets may override via `cooldown_days` schema field.

### Transition-interrupt hook (integration spec §5.3)

- Location: `DutyProfileBehavior.CommitTransition` (existing method).
- Plan 4 adds a small block at the end of `CommitTransition` that, IF `OrderActivity.ActiveNamedOrder != null`, looks up a transition storylet by fallback chain `transition_<old>_to_<new>_mid_<orderId>` → `transition_<old>_to_<new>_generic` → `transition_generic_interrupted`, emits via `StoryDirector.EmitCandidate(ChainContinuation=true)`.
- `ChainContinuation = true` bypasses the 5-day in-game floor + category cooldown (AGENTS.md Critical Rule #10) since these are user-opted flows closing a committed order. 60s wall-clock still applies.

### Save-definer offsets (claim in T1 after verifying free)

- Class offset: `50` — `DutyCooldownStore`.
- No new enums (Plan 4 reuses Plan 1 enums for its predicates; `DutyOpportunityShape` is registered too if we persist it, but we don't — see §Arc-scale emission).
- Actually one enum: `DutyOpportunityShape` — transient only, no persistence needed. Not registered.
- Plan 1 holds 48; Plan 3 holds 49 + 99-101; Plan 4 holds 50. Offsets 51-60 remain reserved.

### Content authoring boundary (integration spec §5.4)

- Plan 4 authors NEW snapshot-driven content: six profile pools, transitions, imprisoned edges. ~140 new storylets total.
- Plan 4 does NOT author T7+ named-order variants (Plan 5).
- Plan 4 does NOT author culture / lord-trait overlays (Plan 5).
- Plan 4 does NOT re-author the already-shipped named-order archetypes (`order_guard_post`, `order_patrol`, `order_fatigue_detail`, `order_drill`, `order_courier`, `order_escort`, `order_scout`, `order_treat_wounded`, `order_siege_works`, `order_inspection`). It wires them into the arc-scale opportunity path.

---

## File structure

```
src/Features/CampaignIntelligence/Duty/
  DutyOpportunity.cs                      — transient POCO
  DutyOpportunityShape.cs                  — enum: Episodic / ArcScale
  EnlistedDutyOpportunityBuilder.cs        — static pure projector
  DutyCooldownStore.cs                     — [Serializable] POCO
  EnlistedDutyEmitterBehavior.cs           — CampaignBehaviorBase hourly tick + emission

src/Features/Content/ (modified)
  TriggerRegistry.cs                        — add 4 snapshot-backed predicates

src/Features/Activities/Orders/ (modified)
  DutyProfileBehavior.cs                    — add transition-storylet emission at end of CommitTransition

Tools/Validation/ (modified)
  validate_content.py                       — implement Phase 13 (pool coverage)

ModuleData/Enlisted/Storylets/
  duty_garrisoned.json                     — ~20 storylets
  duty_marching.json                        — ~20 storylets
  duty_besieging.json                       — ~20 storylets
  duty_raiding.json                         — ~20 storylets
  duty_escorting.json                       — ~10 storylets (thinner per Spec 2 §15)
  duty_wandering.json                       — ~20 storylets
  duty_imprisoned.json                      — ~10 storylets (captivity content)
  transition.json                            — ~15 storylets
```

Five new C# files, three modified. ~135 new storylets. The Emitter is the ONLY file that mutates cooldown state or calls `StoryDirector`; the Builder is the ONLY projector; `CommitTransition` is the ONLY site that emits transitions.

---

## Task index

- **Phase 0 — Save-definer + csproj claim**: T1
- **Phase A — Type foundation**: T2, T3
- **Phase B — Duty opportunity builder (pure)**: T4, T5
- **Phase B.5 — Snapshot-backed trigger predicates (NEW grammar)**: T6, T7
- **Phase C — Episodic emission behavior**: T8, T9, T10, T11
- **Phase D — Arc-scale emission**: T12, T13
- **Phase E — Transition-interrupt hook**: T14
- **Phase F — Authored content (profile pools + transitions)**: T15-T22 (eight authoring tasks)
- **Phase G — Validator Phase 13 (pool coverage)**: T23
- **Phase H — Observability + error handling**: T24, T25
- **Phase I — Smoke verification**: T26, T27, T28

Total: 28 tasks.

---

## Phase 0 — Save-definer + csproj claim

### Task T1: Verify + claim save-definer offset 50 + csproj entries

**Files:**
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Confirm offset 50 free**

Grep pattern: `AddClassDefinition\(typeof\([^)]+\), 50\)` — expected zero matches.

- [ ] **Step 2: Add class-definition placeholder**

Append to `DefineClassTypes()`:

```csharp
// Duty Opportunities (Plan 4 of 5) — offset 50
AddClassDefinition(typeof(Features.CampaignIntelligence.Duty.DutyCooldownStore), 50);
```

- [ ] **Step 3: Add csproj `<Compile Include>` entries**

```xml
    <Compile Include="src\Features\CampaignIntelligence\Duty\DutyOpportunity.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Duty\DutyOpportunityShape.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Duty\EnlistedDutyOpportunityBuilder.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Duty\DutyCooldownStore.cs"/>
    <Compile Include="src\Features\CampaignIntelligence\Duty\EnlistedDutyEmitterBehavior.cs"/>
```

- [ ] **Step 4: Build (expect FAIL)**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected FAIL until T2+T3 create the types.

- [ ] **Step 5: Commit**

```
feat(duty): claim save-definer offset 50 + csproj slots for Plan 4

Reserves class offset 50 (DutyCooldownStore) for the duty-opportunity
layer. Plan 1 holds 48, Plan 3 holds 49. Build currently fails —
types land in T2+T3.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase A — Type foundation

### Task T2: Create `DutyOpportunityShape.cs` + `DutyOpportunity.cs`

**Files:**
- Create: `src/Features/CampaignIntelligence/Duty/DutyOpportunityShape.cs`
- Create: `src/Features/CampaignIntelligence/Duty/DutyOpportunity.cs`

- [ ] **Step 1: Write `DutyOpportunityShape.cs`**

```csharp
namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// The two opportunity shapes per the design spec §6.1 "Opportunity scale"
    /// taxonomy. Episodic resolves inside a storylet flow; ArcScale proposes a
    /// named order through the existing OrderActivity pipeline.
    /// </summary>
    public enum DutyOpportunityShape : byte
    {
        Episodic,
        ArcScale
    }
}
```

- [ ] **Step 2: Write `DutyOpportunity.cs`**

```csharp
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Transient opportunity produced by EnlistedDutyOpportunityBuilder.
    /// Not persisted. The emitter picks one per tick, looks up a matching
    /// storylet from the pool, and emits via StoryDirector.
    /// </summary>
    public sealed class DutyOpportunity
    {
        public DutyOpportunityShape Shape { get; set; }
        public string PoolPrefix { get; set; } = string.Empty;
        public string ArchetypeStoryletId { get; set; }    // arc-scale only — the accept storylet
        public float Priority { get; set; } = 1.0f;         // higher = picked first
        public CampaignTime AsOf { get; set; } = CampaignTime.Zero;

        /// <summary>
        /// Optional descriptor string — used in ModLogger + debug hotkey
        /// inspection to identify which snapshot state produced the opportunity.
        /// </summary>
        public string TriggerReason { get; set; }
    }
}
```

- [ ] **Step 3: CRLF + commit**

```
feat(duty): DutyOpportunityShape enum + DutyOpportunity POCO

Opportunity shape enum (Episodic / ArcScale) per design spec §6.1.
Opportunity POCO transient — produced by the pure Builder, consumed
by the emitter. Priority + PoolPrefix + optional ArchetypeStoryletId
(arc-scale only) + TriggerReason for diagnostics.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T3: Create `DutyCooldownStore.cs`

**Files:**
- Create: `src/Features/CampaignIntelligence/Duty/DutyCooldownStore.cs`

- [ ] **Step 1: Write the POCO**

```csharp
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Persistent per-storylet last-fired timestamps for the duty emitter's
    /// cooldown logic. Same pattern as Plan 3's SignalEmissionRecord. Cleared
    /// when enlistment ends.
    /// </summary>
    [Serializable]
    public sealed class DutyCooldownStore
    {
        public Dictionary<string, CampaignTime> LastFiredAt { get; set; }
            = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reseats null dictionaries after deserialization. See CLAUDE.md pitfall
        /// on [Serializable] + SyncData null-dict crashes.
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

- [ ] **Step 2: Verify container registration**

`Dictionary<string, CampaignTime>` is already registered in `EnlistedSaveDefiner.DefineContainerDefinitions()` (spec 0 backbone, line 136). No new container registration needed.

- [ ] **Step 3: CRLF + commit**

```
feat(duty): DutyCooldownStore persistent per-storylet cooldown POCO

[Serializable] POCO tracking last-fired CampaignTime per storylet id.
Serialized via definer offset 50. EnsureInitialized reseats null
dicts on deserialization paths that skip ctors.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase B — Duty opportunity builder (pure)

### Task T4: Design snapshot → opportunity mapping

**Files:**
- Create: `docs/Features/CampaignIntelligence/duty-opportunity-mapping.md`

- [ ] **Step 1: Author the mapping table**

Document which snapshot fields produce which opportunity pools, with priority and shape.

| Trigger | Opportunity shape | Pool prefix | Priority | Notes |
|---|---|---|---|---|
| `SupplyPressure >= Strained` + current profile = marching | Episodic | `duty_marching_supply_` | 3.0 | Ration-tension content |
| `ArmyStrain >= Medium` + in settlement/camp | Episodic | `duty_<profile>_tension_` | 2.5 | Discipline slippage content |
| `FrontPressure >= High` + current profile = garrisoned | Episodic | `duty_garrisoned_alert_` | 2.8 | Alert-posture content |
| `RecentChangeFlags.ObjectiveShift` + current profile = escorting | Episodic | `duty_escorting_redirect_` | 3.2 | Sudden redirect content |
| `InformationConfidence == Low` + current profile = marching | ArcScale | `order_scout_accept` | 2.0 | Proposes a scout named order |
| `RecoveryNeed >= High` + current profile = garrisoned | ArcScale | `order_treat_wounded_accept` | 2.5 | Proposes field-medic named order |
| `StrategicPosture == OffensiveSiege` + current profile = besieging | ArcScale | `order_siege_works_accept` | 2.8 | Proposes siege-works named order |
| `PrisonerStakes >= Medium` | Episodic | `duty_<profile>_prisoner_` | 2.2 | Prisoner-relevant storylet |
| Quiet-stretch (all strain None, no change flags) | Episodic | `duty_<profile>_routine_` | 1.0 | Routine ambient texture |
| Lord captured (`lord_imprisoned` flag) | Episodic | `duty_imprisoned_` | fallback | Forces profile content |

- [ ] **Step 2: Commit**

```
docs(duty): add duty-opportunity mapping reference

Living reference mapping Plan 1 snapshot state to Plan 4 opportunity
pools. Defines priority + shape per mapping. Used by Builder (T5)
and content authors (T15-T22).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T5: Implement `EnlistedDutyOpportunityBuilder.cs`

**Files:**
- Create: `src/Features/CampaignIntelligence/Duty/EnlistedDutyOpportunityBuilder.cs`

- [ ] **Step 1: Write the static projector**

```csharp
using System.Collections.Generic;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.CampaignIntelligence;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Pure projector — reads a Plan 1 snapshot + the current duty profile
    /// and returns candidate duty opportunities. No side effects, no campaign
    /// reads beyond the snapshot parameter, unit-testable.
    /// </summary>
    public static class EnlistedDutyOpportunityBuilder
    {
        public static List<DutyOpportunity> Build(
            EnlistedLordIntelligenceSnapshot snapshot,
            string currentDutyProfile)
        {
            var opps = new List<DutyOpportunity>();
            if (snapshot == null || string.IsNullOrEmpty(currentDutyProfile))
            {
                return opps;
            }

            // Arc-scale opportunities — propose a named order when the snapshot
            // strongly supports it. Priority scoring biases higher for more
            // acute strain or more confident signals.
            if (snapshot.InformationConfidence == InformationConfidence.Low
                && currentDutyProfile == "marching")
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.ArcScale,
                    ArchetypeStoryletId = "order_scout_accept",
                    PoolPrefix = "order_scout_",
                    Priority = 2.0f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "InformationConfidence=Low"
                });
            }

            if (snapshot.RecoveryNeed >= RecoveryNeed.High
                && currentDutyProfile == "garrisoned")
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.ArcScale,
                    ArchetypeStoryletId = "order_treat_wounded_accept",
                    PoolPrefix = "order_treat_wounded_",
                    Priority = 2.5f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "RecoveryNeed>=High"
                });
            }

            if (snapshot.StrategicPosture == StrategicPosture.OffensiveSiege
                && currentDutyProfile == "besieging")
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.ArcScale,
                    ArchetypeStoryletId = "order_siege_works_accept",
                    PoolPrefix = "order_siege_works_",
                    Priority = 2.8f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "StrategicPosture=OffensiveSiege"
                });
            }

            // Episodic opportunities — snapshot-gated ambient storylets from the
            // current profile's pool. The actual storylet lookup happens in the
            // emitter via TriggerRegistry; Builder here only proposes the pool
            // prefix + priority.
            var profilePrefix = "duty_" + currentDutyProfile + "_";

            if (snapshot.SupplyPressure >= SupplyPressure.Strained)
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.Episodic,
                    PoolPrefix = profilePrefix,
                    Priority = 3.0f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "SupplyPressure>=Strained"
                });
            }

            if (snapshot.ArmyStrain >= ArmyStrainLevel.Medium)
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.Episodic,
                    PoolPrefix = profilePrefix,
                    Priority = 2.5f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "ArmyStrain>=Medium"
                });
            }

            if (snapshot.FrontPressure >= FrontPressure.High)
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.Episodic,
                    PoolPrefix = profilePrefix,
                    Priority = 2.8f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "FrontPressure>=High"
                });
            }

            if (snapshot.PrisonerStakes >= PrisonerStakes.Medium)
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.Episodic,
                    PoolPrefix = profilePrefix,
                    Priority = 2.2f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "PrisonerStakes>=Medium"
                });
            }

            // Quiet-stretch fallback — always add a routine opportunity at low
            // priority so every tick has SOMETHING to emit unless throttle blocks.
            opps.Add(new DutyOpportunity
            {
                Shape = DutyOpportunityShape.Episodic,
                PoolPrefix = profilePrefix,
                Priority = 1.0f,
                AsOf = CampaignTime.Now,
                TriggerReason = "routine"
            });

            // Sort by descending priority so emitter picks the highest-priority
            // opportunity first.
            opps.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return opps;
        }
    }
}
```

VERIFY IN DECOMPILE: none direct — uses Plan 1 snapshot enum types that Plan 1 defines.

- [ ] **Step 2: CRLF + commit**

```
feat(duty): EnlistedDutyOpportunityBuilder pure projector

Pure static builder: snapshot + current duty profile → List<DutyOpportunity>.
Arc-scale opportunities propose existing named-order archetypes (scout,
treat_wounded, siege_works) when snapshot state supports them. Episodic
opportunities emit from the current profile's ambient pool under
snapshot-gated priorities (supply / strain / front / prisoner).
Quiet-stretch fallback ensures every tick has at least a routine
episodic candidate. Sorted by descending priority.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase B.5 — Snapshot-backed trigger predicates

### Task T6: Extend `TriggerRegistry` with snapshot predicates

**Files:**
- Modify: `src/Features/Content/TriggerRegistry.cs`

- [ ] **Step 1: Read existing `TriggerRegistry.EvaluateOne` dispatch**

Identify where predicates are parsed and dispatched. Existing shape handles `is_enlisted`, `is_at_sea`, `in_settlement`, `in_siege`, `in_mapevent`, `context:X`, `flag:X`, `quality_gte:X:N`, `quality_lte:X:N`, `rank_gte:N`, `veteran_has_flag:X`, `beat`. Supports `not:` prefix.

- [ ] **Step 2: Add four new predicates**

```csharp
// snapshot_supply_pressure_gte:N — gate on Plan 1 snapshot SupplyPressure ordinal
if (pred.StartsWith("snapshot_supply_pressure_gte:", StringComparison.OrdinalIgnoreCase))
{
    var parts = pred.Split(':');
    if (parts.Length < 2 || !int.TryParse(parts[1], out var threshold)) return false;
    var snap = CampaignIntelligence.EnlistedCampaignIntelligenceBehavior.Instance?.Current;
    if (snap == null) return false;
    return (int)snap.SupplyPressure >= threshold;
}

// snapshot_army_strain_gte:N
if (pred.StartsWith("snapshot_army_strain_gte:", StringComparison.OrdinalIgnoreCase))
{
    var parts = pred.Split(':');
    if (parts.Length < 2 || !int.TryParse(parts[1], out var threshold)) return false;
    var snap = CampaignIntelligence.EnlistedCampaignIntelligenceBehavior.Instance?.Current;
    if (snap == null) return false;
    return (int)snap.ArmyStrain >= threshold;
}

// snapshot_front_pressure_gte:N
if (pred.StartsWith("snapshot_front_pressure_gte:", StringComparison.OrdinalIgnoreCase))
{
    var parts = pred.Split(':');
    if (parts.Length < 2 || !int.TryParse(parts[1], out var threshold)) return false;
    var snap = CampaignIntelligence.EnlistedCampaignIntelligenceBehavior.Instance?.Current;
    if (snap == null) return false;
    return (int)snap.FrontPressure >= threshold;
}

// snapshot_recent_change:<flag_name> — expects RecentChangeFlags enum name
if (pred.StartsWith("snapshot_recent_change:", StringComparison.OrdinalIgnoreCase))
{
    var parts = pred.Split(':');
    if (parts.Length < 2) return false;
    if (!Enum.TryParse<CampaignIntelligence.RecentChangeFlags>(parts[1], true, out var bit)) return false;
    var snap = CampaignIntelligence.EnlistedCampaignIntelligenceBehavior.Instance?.Current;
    if (snap == null) return false;
    return snap.RecentChangeFlags.HasFlag(bit);
}
```

VERIFY IN DECOMPILE: none direct — this is Enlisted-internal wiring.

- [ ] **Step 3: `not:` prefix** — the existing `not:` dispatch already wraps inner-predicate evaluation. Confirm the new predicates work under `not:` by passing a negation test in T7.

- [ ] **Step 4: Commit**

```
feat(triggers): add snapshot-backed predicates for Plan 4 content gating

Four new TriggerRegistry predicates read from
EnlistedCampaignIntelligenceBehavior.Current:
- snapshot_supply_pressure_gte:N
- snapshot_army_strain_gte:N
- snapshot_front_pressure_gte:N
- snapshot_recent_change:<flag_name>

Plan 1 snapshot fields are POCO enums, not qualities or flags, so
existing quality_gte / flag predicates can't reach them. These four
close that gap so authored Plan 4 content can gate on snapshot state
(e.g. ration-tension storylets fire only when SupplyPressure>=Strained).
All four support the existing `not:` negation prefix.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T7: Build + unit-verify new predicates

- [ ] **Step 1: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 2: Verify predicates**

If the test harness is available, author a small unit test. Otherwise: in-game debug-hotkey or ModLogger spot-check during Phase I smoke.

- [ ] **Step 3: No commit** — verification only.

---

## Phase C — Episodic emission behavior

### Task T8: Create `EnlistedDutyEmitterBehavior.cs` skeleton

**Files:**
- Create: `src/Features/CampaignIntelligence/Duty/EnlistedDutyEmitterBehavior.cs`

- [ ] **Step 1: Write the skeleton**

Pattern matches Plan 3's `EnlistedSignalEmitterBehavior`:

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Hourly-tick duty emitter for Plan 4. Gated by IsEnlisted. Reads Plan 1
    /// snapshot + current duty profile, invokes DutyOpportunityBuilder,
    /// throttle-claims via OrdersNewsFeedThrottle, picks one opportunity,
    /// emits via StoryDirector. Cooldown state in DutyCooldownStore.
    /// </summary>
    public sealed class EnlistedDutyEmitterBehavior : CampaignBehaviorBase
    {
        public static EnlistedDutyEmitterBehavior Instance { get; private set; }

        private DutyCooldownStore _cooldowns = new DutyCooldownStore();
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
                _ = dataStore.SyncData("_dutyCooldowns", ref _cooldowns);
                _cooldowns = _cooldowns ?? new DutyCooldownStore();
                _cooldowns.EnsureInitialized();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTY", "SyncData failed", ex);
                _cooldowns = new DutyCooldownStore();
            }
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _cooldowns = _cooldowns ?? new DutyCooldownStore();
            _cooldowns.EnsureInitialized();
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

                var activity = OrderActivity.Instance;
                if (activity == null)
                {
                    return;
                }

                var snapshot = EnlistedCampaignIntelligenceBehavior.Instance?.Current;
                if (snapshot == null)
                {
                    return;
                }

                var candidates = EnlistedDutyOpportunityBuilder.Build(snapshot, activity.CurrentDutyProfile);
                if (candidates == null || candidates.Count == 0)
                {
                    return;
                }

                // Throttle before content lookup.
                if (!OrdersNewsFeedThrottle.TryClaim())
                {
                    return;
                }

                foreach (var opp in candidates)
                {
                    string storyletId = PickStoryletFromPool(opp.PoolPrefix);
                    if (storyletId == null) continue;

                    EmitOpportunity(opp, storyletId);
                    return; // one emission per tick
                }

                ModLogger.Expected("DUTY", "no_opportunity_storylet",
                    $"Builder produced {candidates.Count} candidates; no eligible storylet found (cooldown or trigger mismatch)");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTY", "OnHourlyTick threw", ex);
            }
        }

        private string PickStoryletFromPool(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return null;

            var nowHours = (int)CampaignTime.Now.ToHours;
            foreach (var s in StoryletCatalog.All)
            {
                if (s?.Id == null || !s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                if (_cooldowns.LastFiredAt.TryGetValue(s.Id, out var last))
                {
                    var cooldownHours = s.CooldownDays > 0 ? s.CooldownDays * 24 : DEFAULT_COOLDOWN_HOURS;
                    if (nowHours - (int)last.ToHours < cooldownHours) continue;
                }

                if (!TriggerRegistry.EvaluateAll(s.Trigger)) continue;

                return s.Id;
            }
            return null;
        }

        private void EmitOpportunity(DutyOpportunity opp, string storyletId)
        {
            var storylet = StoryletCatalog.GetById(storyletId);
            if (storylet == null) return;

            var tier = opp.Shape == DutyOpportunityShape.ArcScale
                ? StoryTier.Modal
                : StoryTier.Log;

            StoryDirector.Instance?.EmitCandidate(new StoryCandidate
            {
                SourceId = "duty." + opp.Shape.ToString().ToLowerInvariant(),
                CategoryId = "duty." + (storyletId.Split('_').Length > 1 ? storyletId.Split('_')[1] : "generic"),
                ProposedTier = tier,
                EmittedAt = CampaignTime.Now,
                RenderedTitle = storylet.Title,
                RenderedBody = storylet.Setup,
                StoryKey = storyletId
            });

            _cooldowns.LastFiredAt[storyletId] = CampaignTime.Now;

            // Episodic: auto-apply effects for single-option floor-style storylets.
            // Multi-option storylets (duty prompts) rely on player interaction.
            if (opp.Shape == DutyOpportunityShape.Episodic && storylet.Options.Count == 1)
            {
                EffectExecutor.ExecuteOptionEffects(storylet, "main", null);
            }

            ModLogger.Info("DUTY", $"emitted {opp.Shape} storylet={storyletId} reason={opp.TriggerReason}");
        }

        private void LogHeartbeatIfDue()
        {
            var nowHour = (int)CampaignTime.Now.ToHours;
            if (nowHour - _lastHeartbeatHourTick < HEARTBEAT_INTERVAL_HOURS) return;
            _lastHeartbeatHourTick = nowHour;

            var isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted == true;
            var hasSnap = EnlistedCampaignIntelligenceBehavior.Instance?.Current != null;
            var tracked = _cooldowns?.LastFiredAt?.Count ?? 0;
            ModLogger.Info("DUTY", $"heartbeat: enlisted={isEnlisted} snapshot={hasSnap} tracked={tracked}");
        }
    }
}
```

VERIFY IN DECOMPILE: `EffectExecutor.ExecuteOptionEffects` signature (same verification as Plan 3 T10); adapt to the actual public API if signature differs.

- [ ] **Step 2: CRLF + commit**

```
feat(duty): EnlistedDutyEmitterBehavior skeleton

Hourly-tick emitter gated by IsEnlisted. Reads snapshot +
CurrentDutyProfile, invokes Builder, throttles, picks from pool,
emits via StoryDirector at Log (episodic) or Modal (arc-scale).
Cooldown state persists via DutyCooldownStore. Heartbeat every
12 hours.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T9: Register emitter in SubModule

**Files:**
- Modify: `src/Mod.Entry/SubModule.cs`

- [ ] **Step 1: Add using + AddBehavior**

```csharp
using Enlisted.Features.CampaignIntelligence.Duty;
// ...
campaignStarter.AddBehavior(new EnlistedDutyEmitterBehavior());
```

- [ ] **Step 2: Build + commit**

```
feat(duty): register EnlistedDutyEmitterBehavior in SubModule

Wires the duty emitter into the campaign behavior pipeline.
Idempotent when not enlisted.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T10: Weighted-diversity picker polish

**Files:**
- Modify: `src/Features/CampaignIntelligence/Duty/EnlistedDutyEmitterBehavior.cs`

- [ ] **Step 1: Replace first-match picker with weighted-diversity picker**

Same pattern as Plan 3 T12 — compute each candidate's effective weight from `weight.base × Π(satisfied modifier factors)`, bias against last 3 fired from same prefix, random normalized-weighted pick.

- [ ] **Step 2: Commit**

---

### Task T11: Build + validator + no-op smoke

Same shape as Plan 3 T13: build passes, validator passes, 2-day in-game run shows heartbeats and "no_opportunity_storylet" expected-exits (content not yet authored).

---

## Phase D — Arc-scale emission

### Task T12: Arc-scale accept-flow wiring

**Files:**
- Modify: `src/Features/CampaignIntelligence/Duty/EnlistedDutyEmitterBehavior.cs`

- [ ] **Step 1: Route Modal-tier arc-scale emissions through StoryDirector**

Arc-scale opportunities emit at `StoryTier.Modal` with the named-order archetype's accept storylet (e.g. `order_scout_accept`) as `StoryKey`. The existing StoryDirector + NamedOrderArcRuntime flow handles the accept path via storylet options (the `start_arc` effect on the accept storylet's option invokes `NamedOrderArcRuntime.SpliceArc`).

The emitter doesn't need custom accept-flow wiring; it just emits the Modal storylet and lets the existing pipeline take over.

- [ ] **Step 2: Guard against double-accept**

If `OrderActivity.ActiveNamedOrder != null`, skip arc-scale emissions — the player can't accept a new one while an arc is running. Add this guard at the top of the picker loop.

```csharp
// Cannot propose a new arc while one is active.
var activity = OrderActivity.Instance;
if (activity?.ActiveNamedOrder != null)
{
    // Filter to Episodic only.
    candidates = candidates.FindAll(c => c.Shape == DutyOpportunityShape.Episodic);
}
```

- [ ] **Step 3: Commit**

```
feat(duty): arc-scale Modal emissions + active-arc guard

Arc-scale opportunities emit at StoryTier.Modal via StoryDirector
using the existing named-order accept storylet (e.g.
order_scout_accept). Accept flow is handled by the existing
start_arc effect on the storylet's option. Emitter guards against
double-accept: if OrderActivity.ActiveNamedOrder != null, filters
to Episodic-only candidates.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T13: Arc-scale smoke

- [ ] **Step 1: Trigger an arc-scale state**

Enlist with a lord, walk lord into a town, wait for `garrisoned` profile with `RecoveryNeed >= High` (achievable by starting enlistment after a battle). Verify Modal prompt for `order_treat_wounded` fires.

- [ ] **Step 2: Accept + verify arc splice**

Pick an intent option in the accept storylet. Verify `OrderActivity.Instance.ActiveNamedOrder != null` and arc phases are spliced.

- [ ] **Step 3: No commit** — verification only.

---

## Phase E — Transition-interrupt hook

### Task T14: Add transition emission to `DutyProfileBehavior.CommitTransition`

**Files:**
- Modify: `src/Features/Activities/Orders/DutyProfileBehavior.cs`

- [ ] **Step 1: Read existing `CommitTransition`**

Identify the method's body. Today it: mutates `activity.CurrentDutyProfile`, fires `profile_changed` beat, logs. Plan 4 adds a transition-storylet emission block at the tail.

- [ ] **Step 2: Add transition-emission block**

At the end of `CommitTransition`, after the `OnBeat("profile_changed", ...)` call:

```csharp
// Plan 4: if a named order is active, fire a transition-interrupt storylet.
// Integration spec §5.3 resolution.
if (activity.ActiveNamedOrder != null)
{
    TryEmitTransitionStorylet(activity, oldProfile, newProfile);
}
```

And the new helper:

```csharp
private void TryEmitTransitionStorylet(
    OrderActivity activity,
    string oldProfile,
    string newProfile)
{
    try
    {
        var orderId = activity.ActiveNamedOrder.OrderStoryletId;
        if (string.IsNullOrEmpty(orderId)) return;

        // Fallback chain: mid_<orderId> → generic → generic_interrupted.
        var candidates = new[]
        {
            $"transition_{oldProfile}_to_{newProfile}_mid_{orderId}",
            $"transition_{oldProfile}_to_{newProfile}_generic",
            "transition_generic_interrupted"
        };

        foreach (var transitionId in candidates)
        {
            var storylet = Features.Content.StoryletCatalog.GetById(transitionId);
            if (storylet == null) continue;

            Features.Content.StoryDirector.Instance?.EmitCandidate(new Features.Content.StoryCandidate
            {
                SourceId = "duty.transition",
                CategoryId = "duty.transition",
                ProposedTier = Features.Content.StoryTier.Modal,
                ChainContinuation = true, // bypass 5-day floor + category cooldown
                EmittedAt = CampaignTime.Now,
                RenderedTitle = storylet.Title,
                RenderedBody = storylet.Setup,
                StoryKey = transitionId
            });
            ModLogger.Info("DUTYPROFILE", $"transition_emitted: {transitionId} ({oldProfile} -> {newProfile}, order={orderId})");
            return;
        }

        ModLogger.Expected("DUTYPROFILE", "no_transition_storylet",
            $"No transition storylet found for {oldProfile} -> {newProfile} mid {orderId}");
    }
    catch (System.Exception ex)
    {
        ModLogger.Caught("DUTYPROFILE", "TryEmitTransitionStorylet threw", ex);
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Commit**

```
feat(duty): transition-interrupt hook in DutyProfileBehavior.CommitTransition

Integration spec §5.3 resolution: when a profile change commits
while OrderActivity.ActiveNamedOrder != null, fire a transition
storylet via fallback chain:
  transition_<old>_to_<new>_mid_<orderId>
  → transition_<old>_to_<new>_generic
  → transition_generic_interrupted

Emits as Modal with ChainContinuation=true (bypasses the 5-day
in-game floor + category cooldown; 60s wall-clock still applies)
since these are closure storylets for a committed arc.

Content (transition.json) lands in Phase F T22.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase F — Authored content

**Authoring pattern for every ambient duty-profile storylet:**

```json
{
  "id": "duty_<profile>_<descriptor>_<n>",
  "title": "{=duty_<profile>_<descriptor>_<n>_title}<3-6 words>",
  "setup": "{=duty_<profile>_<descriptor>_<n>_setup}<1-2 sentences soldier POV>",
  "theme": "duty",
  "category": "duty_<profile>",
  "severity": "low",
  "cooldown_days": 2,
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "profile_requires": ["<profile>"],
  "class_affinity": { "<class>": 1.3 },
  "trigger": ["is_enlisted", "<optional-snapshot-gate>"],
  "options": [
    {
      "id": "main",
      "text": "{=duty_<profile>_<descriptor>_<n>_opt}<reaction>",
      "tooltip": "{=duty_<profile>_<descriptor>_<n>_tt}<≤80 char effect summary>",
      "effects": [
        { "apply": "skill_xp", "skill": "<skill>", "amount": 20 }
      ]
    }
  ]
}
```

Every profile pool covers all 18 Bannerlord skills with ≥ 1 storylet (Phase 13 validator enforces ≥ 5 per (profile, skill) cell per Spec 2 §2). Heavy / moderate / thin skill bias per profile per the archived Orders plan's Task 27-32 texture notes.

Content authoring tasks below use representative examples + skill-coverage tables rather than authoring all ~20 storylets verbatim (would bloat the plan). Implementers follow the existing home-evening / order-archetype style.

### Task T15: Author `duty_garrisoned.json` (~20 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/duty_garrisoned.json`

- [ ] **Step 1: Author 20 storylets covering all 18 skills**

Heavy bias per class per Spec 2 §2:
- Infantry: OneHanded, Polearm, Athletics
- Ranged: Bow, Scouting
- Cavalry: Riding, Polearm
- HorseArcher: Bow, Riding

Moderate coverage: Charm, Steward, Leadership, Medicine, Trade.
Thin coverage: Roguery, Throwing, Crossbow.

Representative storylet (Bow-heavy, Ranged class, quiet-stretch):

```json
{
  "id": "duty_garrisoned_archery_yard_1",
  "title": "{=duty_garr_arch_1_t}At the butts.",
  "setup": "{=duty_garr_arch_1_s}You shoulder a practice bow. Three rounds of ten before the evening mess.",
  "theme": "duty",
  "category": "duty_garrisoned",
  "severity": "low",
  "cooldown_days": 2,
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "profile_requires": ["garrisoned"],
  "class_affinity": { "Ranged": 1.4, "HorseArcher": 1.2 },
  "trigger": ["is_enlisted"],
  "options": [
    {
      "id": "main",
      "text": "{=duty_garr_arch_1_opt}Loose.",
      "tooltip": "{=duty_garr_arch_1_tt}Practice bow. Bow XP.",
      "effects": [
        { "apply": "skill_xp", "skill": "Bow", "amount": 30 }
      ]
    }
  ]
}
```

Snapshot-gated storylet (ration-tension, fires when supply pressure is up):

```json
{
  "id": "duty_garrisoned_short_rations_1",
  "title": "{=duty_garr_rat_1_t}Thin gruel at mess.",
  "setup": "{=duty_garr_rat_1_s}Dinner is thinner than last week. No one's complaining loud, but everyone notices.",
  "theme": "duty",
  "category": "duty_garrisoned",
  "severity": "normal",
  "cooldown_days": 3,
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "profile_requires": ["garrisoned"],
  "trigger": ["is_enlisted", "snapshot_supply_pressure_gte:2"],
  "options": [
    {
      "id": "main",
      "text": "{=duty_garr_rat_1_opt}Swallow. Say nothing.",
      "tooltip": "{=duty_garr_rat_1_tt}Small scrutiny hit.",
      "effects": [
        { "apply": "quality_add", "quality": "scrutiny", "amount": 1 }
      ]
    }
  ]
}
```

- [ ] **Step 2: Validator + sync loc + commit**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
/c/Python313/python.exe Tools/Validation/sync_event_strings.py
```

```
content(duty): author duty_garrisoned.json (20 ambient storylets)

Plan 4 Phase F pool 1 of 7. Covers 18 Bannerlord skill axes; heavy
bias on OneHanded/Polearm/Athletics (Infantry), Bow/Scouting
(Ranged), Riding/Polearm (Cavalry), Bow/Riding (HorseArcher).
Snapshot-gated subset uses snapshot_supply_pressure_gte and
snapshot_army_strain_gte triggers.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

### Task T16: Author `duty_marching.json` (~20 storylets)

Heavy: Athletics, Riding, Scouting, Tactics. Distinct narrative texture: road, tents at dusk, campfire chatter, rations, weather. Snapshot-gated subset for `snapshot_front_pressure_gte:2` (scouting tension) and `snapshot_supply_pressure_gte:2` (wagon trouble, forage tension).

Authoring + validator + commit same pattern as T15.

### Task T17: Author `duty_besieging.json` (~20 storylets)

Heavy: Engineering, Crossbow, Bow, Athletics. Siege-camp tedium + breach moments. Higher daily-drift XP per profile (see `duty_profiles.json`). Narrative texture: earthworks, ladder-building, archer fire from the walls. Snapshot-gated subset for `snapshot_army_strain_gte:2` (siege-camp tension).

### Task T18: Author `duty_raiding.json` (~20 storylets)

Heavy: Athletics, Roguery, OneHanded, Riding. Ethical-weight options ("burn the granary" / "spare the village"). Rogue-path-friendly. Narrative texture: smoke, civilians, loot. Intent affinity lean: many storylets `intent_affinity: { "scheme": 1.3, "slack": 1.1 }`. Snapshot-gated subset for `snapshot_recent_change:NewThreat`.

### Task T19: Author `duty_escorting.json` (~10 storylets)

Thinner profile per Spec 2 §15. Subordinate-in-army flavor; lord defers to army-leader's plan. Heavy: Tactics, Leadership, Riding. Narrative texture: marching in someone else's column; another lord's officers giving orders. Snapshot-gated subset for `snapshot_recent_change:DispersalRisk` and `snapshot_recent_change:ObjectiveShift`.

### Task T20: Author `duty_wandering.json` (~20 storylets)

Mercenary-band feel; low-direction. Heavy: Scouting, Trade, Roguery. Narrative texture: "between contracts," scouting opportunities, side-jobs from rural notables. Many storylets `intent_affinity: { "scheme": 1.3 }`. Snapshot-gated subset for `snapshot_information_confidence_lte:1` (will need Plan 4 T6 to add the inverse; or use `not:snapshot_*_gte:2`).

### Task T21: Author `duty_imprisoned.json` (~10 storylets)

Captivity content — lord is imprisoned, player is in limbo. Heavy: Roguery, Charm. Cell-mate dialogues, attempt to escape, bribe guards. Triggers include `profile_requires: ["imprisoned"]` + `flag:lord_imprisoned`.

### Task T22: Author `transition.json` (~15 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/transition.json`

- [ ] **Step 1: Author 15 transition storylets per archived Orders plan Task 35 spec**

Cover the high-leverage flip-flops:
1. `transition_besieging_to_marching_mid_order_guard_post` — "The retreat horn sounds. Pack and march."
2. `transition_besieging_to_marching_mid_order_siege_works` — "We tear down what we built. Column moves."
3. `transition_marching_to_besieging_mid_order_escort` — "The wagon will have to wait. Siege orders."
4. `transition_raiding_to_marching_mid_order_fatigue_detail` — "Raid done. Back on the road."
5. `transition_escorting_to_wandering_mid_any` — "The army dissolves around you. Every lord his own."
6. `transition_garrisoned_to_marching_mid_order_drill` — "Column forms. Drill is over."
7. `transition_garrisoned_to_marching_mid_order_inspection` — "The parade is cut short. Orders came down."
8. `transition_marching_to_garrisoned_mid_order_courier` — "The message reaches the wall. Ride back to quarters."
9. `transition_to_imprisoned_mid_any` — "The column collapses. The enemy rides down on us."
10. `transition_from_imprisoned_mid_any` — "Released. Back in harness. Things to remember."
11-13. Three `transition_<old>_to_<new>_generic` entries covering common pairs not handled by specific mid_<order>.
14-15. `transition_generic_interrupted` — catch-all: "Orders are closed. Whatever you were doing is no longer what you are doing."

Each transition storylet is SINGLE-OPTION, Modal tier, with effects that include `clear_active_named_order` + partial XP awards (half the expected resolve XP). Long-form effects only. Inline loc-keys.

Example:

```json
{
  "id": "transition_besieging_to_marching_mid_order_guard_post",
  "title": "{=trans_bes_mar_guard_t}The horn sounds in the night.",
  "setup": "{=trans_bes_mar_guard_s}Horn calls across the siege lines. The lord strikes camp before dawn. Your post is forgotten in the rush.",
  "theme": "duty",
  "category": "duty_transition",
  "severity": "elevated",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "trigger": ["is_enlisted"],
  "options": [
    {
      "id": "main",
      "text": "{=trans_bes_mar_guard_opt}Pack and march.",
      "tooltip": "{=trans_bes_mar_guard_tt}The order closes. Half-rewards.",
      "effects": [
        { "apply": "skill_xp", "skill": "Athletics", "amount": 15 },
        { "apply": "skill_xp", "skill": "Scouting", "amount": 8 },
        { "apply": "clear_active_named_order" }
      ]
    }
  ]
}
```

- [ ] **Step 2: Validator + sync loc + commit**

```
content(duty): author transition.json (15 interrupt-closure storylets)

Plan 4 Phase F pool 7 of 7. Fires when DutyProfileBehavior.CommitTransition
commits a profile change while OrderActivity.ActiveNamedOrder != null.
Fallback chain in CommitTransition: mid_<orderId> → generic → generic_interrupted.
Single-option, Modal tier, effects close the arc via clear_active_named_order
and award partial XP.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase G — Validator Phase 13 (pool coverage)

### Task T23: Implement `validate_content.py` Phase 13

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Add Phase 13 function**

Add after Phase 12 pattern (similar to Phase 14 / 15 structure). Scans `StoryletCatalog.All` and builds `(profile, skill)` coverage matrix. For each profile in [garrisoned, marching, besieging, raiding, escorting, wandering, imprisoned], for each of the 18 Bannerlord skills (OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing, Riding, Athletics, Scouting, Tactics, Trade, Leadership, Charm, Roguery, Steward, Medicine, Engineering, Smithing), count covering storylets (storylets where `profile_requires` contains the profile AND the effect list includes a `skill_xp` for that skill). Error when count < 5 (for escorting, < 3 per §15 thinner profile).

- [ ] **Step 2: Validator run + commit**

Once Plan 4 Phase F content authoring completes (T15-T22), this validator should pass. If it doesn't, the authoring iteration adds the missing coverage.

```
feat(validator): Phase 13 pool coverage enforcement

Asserts each (duty_profile, skill) cell has ≥5 covering storylets
(≥3 for the thinner escorting profile per Spec 2 §15). Fails with
specific (profile, skill) shortfall messages for iterative authoring.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Phase H — Observability + error handling

### Task T24: Observability polish + regenerate error-code registry

**Files:**
- Modify: `src/Features/CampaignIntelligence/Duty/EnlistedDutyEmitterBehavior.cs`
- Run: `Tools/Validation/generate_error_codes.py`

- [ ] **Step 1: Add per-profile emission counters**

Track per-profile emission counts per session. Log every 24 in-game hours.

- [ ] **Step 2: Regenerate error codes**

```bash
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

- [ ] **Step 3: Commit**

---

### Task T25: Build + validator clean

Full build + validator run. All 18 skill coverage cells satisfied. Commit the registry regen.

---

## Phase I — Smoke verification

### Task T26: 14-in-game-day full-profile smoke

- [ ] **Step 1: Launch + enlist**

Launch campaign, enlist with a lord. 14 in-game days at 4× speed. Mix lord movements to visit all seven profiles.

- [ ] **Step 2: Per-profile verification**

Expected outcomes:
- Each profile receives at least one ambient emission.
- Snapshot-gated storylets fire only under matching state (e.g. ration-tension only when `SupplyPressure >= Strained`).
- Transitions fire on profile change mid-arc.
- Arc-scale opportunities fire as Modal prompts; accept → splice arc.
- All 18 skills receive XP over the run.

- [ ] **Step 3: Save-load mid-arc**

Mid-run, save. Exit to main menu. Reload. Verify arc is reconstructed (existing Plan 1 Phase F persistence guarantees snapshot survival; existing `OrderActivity.ReconstructArcOnLoad` rebuilds arc phases).

- [ ] **Step 4: No commit** — verification only.

---

### Task T27: Fast-forward soak

- [ ] **Step 1: 4× speed 30 real-second soak**

Verify ≤ 5 news-feed entries per real minute. Throttle active.

- [ ] **Step 2: 16× speed soak (if speed-mod installed)**

Verify near-zero non-modal emissions. Modal interrupts (transitions, arc-scale accepts) still fire.

---

### Task T28: Verification doc + status closure

**Files:**
- Create: `docs/superpowers/plans/2026-04-21-duty-opportunities-verification.md`

Summarize. Commit closure + CLAUDE.md status update.

```
docs(duty): Plan 4 verification + status closure

Plan 4 (Duty Opportunities) complete. Ships EnlistedDutyOpportunityBuilder,
EnlistedDutyEmitterBehavior with throttled hourly episodic + arc-scale
emissions, TriggerRegistry extended with four snapshot-backed predicates,
DutyProfileBehavior.CommitTransition extended with transition-interrupt
hook, seven authored profile pools (~135 storylets), transition.json
(15 storylets), Phase 13 validator enforcing pool coverage. CLAUDE.md
status updated.

Plan 4 closes the bulk of the Orders Surface content residual.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## Cross-plan integration summary

- **Plan 1 dependency:** hard. Plan 4 reads `EnlistedCampaignIntelligenceBehavior.Current` + snapshot enums + `RecentChangeFlags`.
- **Plan 2 relationship:** independent. Plan 2 AI wrappers may bias lord behavior toward states that produce duty opportunities, but Plan 4 doesn't read Plan 2.
- **Plan 3 relationship:** independent. Plans 3 and 4 compete for the same `OrdersNewsFeedThrottle` wall-clock slot by design — both are non-modal story routes. Cooldown + throttle pace them together.
- **Plan 5 relationship:** Plan 5 culture/trait overlays operate on Plan 4's hot-path authored storylets. Plan 5 queues until Plan 4 content lands.
- **Shared code surface:** `TriggerRegistry.cs` (Plan 4 adds predicates; Plans 2/3/5 read), `DutyProfileBehavior.CommitTransition` (Plan 4 extends), `Enlisted.csproj` (additive entries). No conflicts expected.

---

## Shipping checklist

- [ ] Plan 1 shipped
- [ ] Phase 0 T1 — offset 50 claimed
- [ ] Phase A T2-T3 — types
- [ ] Phase B T4-T5 — Builder + mapping ref
- [ ] Phase B.5 T6-T7 — snapshot trigger predicates
- [ ] Phase C T8-T11 — episodic emission behavior
- [ ] Phase D T12-T13 — arc-scale Modal emission
- [ ] Phase E T14 — transition hook
- [ ] Phase F T15-T22 — 7 authored profile pools + transitions
- [ ] Phase G T23 — Phase 13 validator
- [ ] Phase H T24-T25 — observability + error codes
- [ ] Phase I T26-T28 — smoke + verification

Total: 28 tasks.
