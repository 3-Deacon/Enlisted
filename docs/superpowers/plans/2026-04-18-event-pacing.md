# Event Pacing Redesign — Implementation Plan (v2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a `StoryDirector` that gates the **Modal** delivery path (floor + wall-clock + category cooldown) and adds a **Headlines** accordion on `enlisted_status` surfacing high-severity items from the existing `EnlistedNewsBehavior` feeds. Beat-anchored, relevance-filtered, speed-aware. Preserves all authored content and existing consumer contracts.

**Architecture:** Additive director. Sources keep producing `EventDefinition` from `EventCatalog` / `DecisionCatalog` as today; they now emit a `StoryCandidate` carrying that `EventDefinition` (for interactive items) into the Director instead of calling `EventDeliveryManager.QueueEvent` directly. The Director routes: Modal-eligible → modal (through existing delivery), otherwise defer or write an observational `DispatchItem` into the appropriate news feed. `_personalFeed` / `_kingdomFeed` / DispatchItem remain the authoritative news data model; the Headlines accordion is a filtered view, not a duplicate store.

**Tech Stack:** C# / .NET 4.7.2 / Bannerlord v1.3.13 API / Harmony / Newtonsoft.Json.

**Spec:** [`docs/superpowers/specs/2026-04-18-event-pacing-design.md`](../specs/2026-04-18-event-pacing-design.md)

**History:** v1 of this plan was rewritten after an adversarial audit surfaced six material defects (wrong payload type, 8 unaccounted callers, deletion of shipped content via Tasks 21/23, DispatchItem contract loss, path/case errors, removed-feature effects in sample content). See commit history for v1 and the audit outcome.

---

## Conventions for every task

- **After each code change:** `dotnet build -c "Enlisted RETAIL" /p:Platform=x64`. Must succeed.
- **After each JSON change:** `python Tools/Validation/validate_content.py`. Must pass.
- **New C# files:** register in `Enlisted.csproj`.
- **New save types:** register in `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` per its existing `BaseId = 735000 + offset` pattern.
- **Commit after each task** with conventional prefix (`feat:`, `refactor:`, `build:`, `docs:`).
- **Commit selectively** with `git add <file>` (not `git add -A`). The working tree may have in-flight changes from parallel docs work — preserve them.
- **TaleWorlds APIs:** verify against `C:\Dev\Enlisted\Decompile\` only. Never use web or training knowledge.
- **Content directory is `ModuleData/Enlisted/Events/` (capital E).**
- **Logging uses `ModLogger.WarnCode(category, code, message)` / `ErrorCode(category, code, message, ex)`** from `Enlisted.Mod.Core.Logging` (not `Mod.Core.Util`). Dual-emits to log file + on-screen panel per `948fc72`.
- **Error codes: `E-PACE-NNN`** under a new `Pacing (PACE)` section in `docs/error-codes.md` (registered in Task 0). Never renumber; append.

---

## Task 0: Register Pacing error codes in the registry

**Files:**
- Modify: `docs/error-codes.md`

**Context:** Arch-fixes Commit 3 landed the project's error-code registry at `docs/error-codes.md` with format `E-<SUBSYSTEM>-<NNN>`. Policy: "never renumber, gaps are fine." Existing subsystems: QM, QM-UI, UI, DIALOG, SYSTEM, TIME, MUSTER, QM-PARTY, QM-DEAL. No PACE section exists yet. Add one with the two codes this plan will emit.

- [ ] **Step 1: Append a new `## Pacing (PACE)` section**

Append at the end of the file, after the last existing subsystem section:

```markdown
## Pacing (PACE)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-PACE-001 | `StoryDirector.EmitCandidate` threw — candidate dropped silently | See exception log; relevance filter or classifier likely received unexpected state | Content |
| E-PACE-002 | Quiet-stretch fallback tick threw — no fallback fired this day | See exception log; confirm `EventCatalog.GetEventsByCategory("quiet_stretch")` returns events | Content |
| E-PACE-003 | `EnlistedNewsBehavior.AddPersonalDispatch` threw — dispatch item dropped | See exception log; likely DispatchItem construction or dedup logic regressed | Content |
```

- [ ] **Step 2: Commit**

```bash
git add docs/error-codes.md
git commit -m "docs: register E-PACE-* error codes for StoryDirector"
```

---

## Phase 1 — Director infrastructure (shippable in isolation)

Runtime no-op at end of Phase 1: Director registered, no source emits, no renderer reads.

### Task 1: Add tier and beat enums

**Files:**
- Create: `src/Features/Content/StoryTier.cs`
- Create: `src/Features/Content/StoryBeat.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `StoryTier.cs`**

```csharp
namespace Enlisted.Features.Content
{
    public enum StoryTier
    {
        Log = 0,
        Pertinent = 1,
        Headline = 2,
        Modal = 3
    }
}
```

- [ ] **Step 2: Create `StoryBeat.cs`**

```csharp
namespace Enlisted.Features.Content
{
    public enum StoryBeat
    {
        LordCaptured,
        LordKilled,
        LordMajorBattleStart,
        LordMajorBattleEnd,
        PlayerBattleEnd,
        SiegeBegin,
        SiegeEnd,
        WarDeclared,
        PeaceSigned,
        ArmyFormed,
        ArmyDispersed,
        SettlementEntered,
        SettlementLeft,
        PaydayBeat,
        OrderPhaseTransition,
        OrderComplete,
        CompanyCrisisThreshold,
        CompanyPressureThreshold,
        EscalationThreshold,
        QuietStretchTimeout
    }
}
```

- [ ] **Step 3: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Content\StoryTier.cs"/>
<Compile Include="src\Features\Content\StoryBeat.cs"/>
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/StoryTier.cs src/Features/Content/StoryBeat.cs Enlisted.csproj
git commit -m "feat: add StoryTier and StoryBeat enums for pacing director"
```

---

### Task 2: Add RelevanceKey struct

**Files:**
- Create: `src/Features/Content/RelevanceKey.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `RelevanceKey.cs`**

```csharp
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Enlisted.Features.Content
{
    public struct RelevanceKey
    {
        public Hero SubjectHero;
        public Hero ObjectHero;
        public Settlement SubjectSettlement;
        public Kingdom SubjectKingdom;
        public IFaction SubjectFaction;
        public Vec2 SubjectPosition;
        public bool TouchesEnlistedLord;
        public bool TouchesPlayerKingdom;

        public static RelevanceKey Empty => default;
    }
}
```

- [ ] **Step 2: Register, build, commit**

```xml
<Compile Include="src\Features\Content\RelevanceKey.cs"/>
```

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/RelevanceKey.cs Enlisted.csproj
git commit -m "feat: add RelevanceKey struct for pacing filter"
```

---

### Task 3: Add StoryCandidate, StoryCandidatePersistent, IStorySource

**Context:** `StoryCandidate` carries EITHER an `EventDefinition` (interactive — has options/effects/chain) OR an observational payload (title/body only, renders to news feed or accordion). The Director branches on that. Do NOT try to render an `EventDefinition` as an accordion entry without its `Options`; demotion means "defer this interactive event until budget opens" rather than "render degraded."

**Files:**
- Create: `src/Features/Content/StoryCandidate.cs`
- Create: `src/Features/Content/StoryCandidatePersistent.cs`
- Create: `src/Features/Content/IStorySource.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `IStorySource.cs`**

```csharp
namespace Enlisted.Features.Content
{
    public interface IStorySource
    {
        string SourceId { get; }
    }
}
```

(Rehydration is handled via `EventCatalog.GetEventById` by the Director itself — sources don't need a rehydrate method.)

- [ ] **Step 2: Create `StoryCandidate.cs`**

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    public sealed class StoryCandidate
    {
        public string SourceId { get; set; }
        public string CategoryId { get; set; }
        public StoryTier ProposedTier { get; set; } = StoryTier.Log;
        public float SeverityHint { get; set; }
        public HashSet<StoryBeat> Beats { get; set; } = new HashSet<StoryBeat>();
        public RelevanceKey Relevance { get; set; }
        public CampaignTime EmittedAt { get; set; }

        // Set one of:
        public EventDefinition InteractiveEvent { get; set; }       // has options/effects
        public bool HasObservationalRender { get; set; }            // title+body only

        // Observational / deferred-interactive rendering:
        public string RenderedTitle { get; set; }
        public string RenderedBody { get; set; }

        // DispatchItem compatibility (when routed through news feed):
        public string StoryKey { get; set; }          // dedup key
        public string DispatchCategory { get; set; }  // "war" | "battle" | "siege" | etc.
        public int SeverityLevel { get; set; }        // 0 Normal .. 4 Critical
        public int MinDisplayDays { get; set; } = 1;
    }
}
```

- [ ] **Step 3: Create `StoryCandidatePersistent.cs`**

Follow project convention (see `src/Features/Orders/Models/OrderOutcome.cs`): plain public properties, no `[SaveableField]`. Persists deferred-interactive candidates by `EventId` so the Director can rehydrate via `EventCatalog.GetEventById`.

```csharp
namespace Enlisted.Features.Content
{
    public sealed class StoryCandidatePersistent
    {
        public string SourceId { get; set; }
        public string CategoryId { get; set; }
        public StoryTier GrantedTier { get; set; }
        public float Severity { get; set; }
        public int EmittedDayNumber { get; set; }
        public int EmittedHour { get; set; }
        public string InteractiveEventId { get; set; }    // null if observational
        public string RenderedTitle { get; set; }
        public string RenderedBody { get; set; }
        public string StoryKey { get; set; }
    }
}
```

- [ ] **Step 4: Register, build, commit**

```xml
<Compile Include="src\Features\Content\IStorySource.cs"/>
<Compile Include="src\Features\Content\StoryCandidate.cs"/>
<Compile Include="src\Features\Content\StoryCandidatePersistent.cs"/>
```

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/IStorySource.cs src/Features/Content/StoryCandidate.cs src/Features/Content/StoryCandidatePersistent.cs Enlisted.csproj
git commit -m "feat: add StoryCandidate schema with InteractiveEvent + observational paths"
```

---

### Task 4: Add RelevanceFilter (use CampaignTriggerTrackerBehavior)

**Files:**
- Create: `src/Features/Content/RelevanceFilter.cs`
- Modify: `Enlisted.csproj`

**Context:** The project already has `src/Mod.Core/Triggers/CampaignTriggerTrackerBehavior.cs` with a timestamp-based API (`IsWithinDays(CampaignTime when, float windowDays)`, `LastSettlementEnteredId`, etc.). "Visited" here means *within the last 60 in-game days* — the tracker does not flag "ever visited". Use recency, not persistence.

- [ ] **Step 1: Create `RelevanceFilter.cs`**

```csharp
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Triggers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    public static class RelevanceFilter
    {
        private const float RecentSettlementDays = 60f;
        private const float TravelDaysThreshold = 7f;

        public static bool Passes(RelevanceKey key, Hero enlistedLord, MobileParty playerParty)
        {
            if (enlistedLord == null || playerParty == null)
            {
                return false;
            }

            if (key.TouchesEnlistedLord || key.TouchesPlayerKingdom)
            {
                return true;
            }

            if (key.SubjectHero != null && key.SubjectHero == enlistedLord)
            {
                return true;
            }

            if (key.SubjectKingdom != null && Hero.MainHero?.Clan?.Kingdom != null
                && key.SubjectKingdom == Hero.MainHero.Clan.Kingdom)
            {
                return true;
            }

            if (key.SubjectSettlement != null && RecentlyVisited(key.SubjectSettlement))
            {
                return true;
            }

            if (key.SubjectHero != null && HeroKnownToPlayer(key.SubjectHero))
            {
                return true;
            }

            if (key.SubjectPosition.IsNonZero())
            {
                float distance = playerParty.GetPosition2D.Distance(key.SubjectPosition);
                float partySpeed = playerParty.Speed > 0f ? playerParty.Speed : 5f;
                float daysOfTravel = distance / partySpeed;
                if (daysOfTravel <= TravelDaysThreshold)
                {
                    return true;
                }
            }

            if (key.SubjectFaction != null && IsActiveEnemyOfLord(key.SubjectFaction, enlistedLord))
            {
                return true;
            }

            return false;
        }

        private static bool RecentlyVisited(TaleWorlds.CampaignSystem.Settlements.Settlement s)
        {
            var tracker = Campaign.Current?.GetCampaignBehavior<CampaignTriggerTrackerBehavior>();
            if (tracker == null || s?.StringId == null)
            {
                return false;
            }
            return tracker.LastSettlementEnteredId == s.StringId
                && tracker.IsWithinDays(tracker.LastSettlementEnteredTime, RecentSettlementDays);
        }

        private static bool HeroKnownToPlayer(Hero hero)
        {
            if (hero?.IsAlive != true)
            {
                return false;
            }
            return Hero.MainHero.GetRelation(hero) > 0;
        }

        private static bool IsActiveEnemyOfLord(IFaction faction, Hero enlistedLord)
        {
            var lordKingdom = enlistedLord?.Clan?.Kingdom;
            if (lordKingdom == null || faction == null)
            {
                return false;
            }
            return FactionManager.IsAtWarAgainstFaction(lordKingdom, faction);
        }
    }
}
```

- [ ] **Step 2: Register, build, commit**

```xml
<Compile Include="src\Features\Content\RelevanceFilter.cs"/>
```

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/RelevanceFilter.cs Enlisted.csproj
git commit -m "feat: add RelevanceFilter using existing CampaignTriggerTrackerBehavior"
```

---

### Task 5: Add SeverityClassifier

**Files:**
- Create: `src/Features/Content/SeverityClassifier.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `SeverityClassifier.cs`**

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    public static class SeverityClassifier
    {
        private static readonly Dictionary<StoryBeat, float> BeatWeights = new Dictionary<StoryBeat, float>
        {
            { StoryBeat.LordCaptured, 0.95f },
            { StoryBeat.LordKilled, 0.95f },
            { StoryBeat.PaydayBeat, 0.90f },
            { StoryBeat.CompanyCrisisThreshold, 0.85f },
            { StoryBeat.EscalationThreshold, 0.80f },
            { StoryBeat.LordMajorBattleEnd, 0.70f },
            { StoryBeat.SiegeBegin, 0.55f },
            { StoryBeat.SiegeEnd, 0.55f },
            { StoryBeat.OrderComplete, 0.50f },
            { StoryBeat.WarDeclared, 0.45f },
            { StoryBeat.PeaceSigned, 0.40f },
            { StoryBeat.ArmyFormed, 0.35f },
            { StoryBeat.ArmyDispersed, 0.35f },
            { StoryBeat.LordMajorBattleStart, 0.35f },
            { StoryBeat.PlayerBattleEnd, 0.30f },
            { StoryBeat.CompanyPressureThreshold, 0.25f },
            { StoryBeat.SettlementEntered, 0.15f },
            { StoryBeat.SettlementLeft, 0.10f },
            { StoryBeat.OrderPhaseTransition, 0.05f },
            { StoryBeat.QuietStretchTimeout, 0.70f }
        };

        private static readonly Dictionary<StoryBeat, StoryTier> BeatMaxTier = new Dictionary<StoryBeat, StoryTier>
        {
            { StoryBeat.LordCaptured, StoryTier.Modal },
            { StoryBeat.LordKilled, StoryTier.Modal },
            { StoryBeat.LordMajorBattleEnd, StoryTier.Modal },
            { StoryBeat.PaydayBeat, StoryTier.Modal },
            { StoryBeat.CompanyCrisisThreshold, StoryTier.Modal },
            { StoryBeat.EscalationThreshold, StoryTier.Modal },
            { StoryBeat.OrderComplete, StoryTier.Modal },
            { StoryBeat.QuietStretchTimeout, StoryTier.Modal },
            { StoryBeat.WarDeclared, StoryTier.Headline },
            { StoryBeat.PeaceSigned, StoryTier.Headline },
            { StoryBeat.ArmyFormed, StoryTier.Headline },
            { StoryBeat.ArmyDispersed, StoryTier.Headline },
            { StoryBeat.SiegeBegin, StoryTier.Headline },
            { StoryBeat.SiegeEnd, StoryTier.Headline },
            { StoryBeat.LordMajorBattleStart, StoryTier.Headline },
            { StoryBeat.CompanyPressureThreshold, StoryTier.Pertinent },
            { StoryBeat.SettlementEntered, StoryTier.Pertinent },
            { StoryBeat.SettlementLeft, StoryTier.Pertinent },
            { StoryBeat.PlayerBattleEnd, StoryTier.Pertinent },
            { StoryBeat.OrderPhaseTransition, StoryTier.Log }
        };

        public static StoryTier Classify(StoryCandidate c, bool touchesEnlistedLord, bool touchesPlayerKingdom, bool recentlyVisitedSettlement)
        {
            float score = c.SeverityHint;

            float maxBeatWeight = 0f;
            StoryTier tierCap = StoryTier.Log;
            foreach (var beat in c.Beats)
            {
                if (BeatWeights.TryGetValue(beat, out var w) && w > maxBeatWeight)
                {
                    maxBeatWeight = w;
                }
                if (BeatMaxTier.TryGetValue(beat, out var cap) && (int)cap > (int)tierCap)
                {
                    tierCap = cap;
                }
            }
            score += maxBeatWeight;

            if (touchesEnlistedLord) score += 0.15f;
            if (touchesPlayerKingdom) score += 0.10f;
            if (recentlyVisitedSettlement) score += 0.05f;

            StoryTier byScore =
                score >= 0.70f ? StoryTier.Modal :
                score >= 0.35f ? StoryTier.Headline :
                score >= 0.10f ? StoryTier.Pertinent :
                                 StoryTier.Log;

            StoryTier maxOfProposedAndScore = (int)c.ProposedTier > (int)byScore ? c.ProposedTier : byScore;
            return (int)maxOfProposedAndScore > (int)tierCap ? tierCap : maxOfProposedAndScore;
        }
    }
}
```

- [ ] **Step 2: Register, build, commit**

```xml
<Compile Include="src\Features\Content\SeverityClassifier.cs"/>
```

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/SeverityClassifier.cs Enlisted.csproj
git commit -m "feat: add SeverityClassifier with beat weights and tier caps"
```

---

### Task 6: Add StoryAggregator

**Files:**
- Create: `src/Features/Content/StoryAggregator.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `StoryAggregator.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    public static class StoryAggregator
    {
        private const float WindowHours = 48f;

        public static List<StoryCandidate> CompressSameCategoryInWindow(
            List<StoryCandidate> input,
            CampaignTime now)
        {
            if (input == null || input.Count == 0)
            {
                return input;
            }

            var output = new List<StoryCandidate>();
            var grouped = input
                .Where(c => now.ToHours - c.EmittedAt.ToHours <= WindowHours)
                .GroupBy(c => c.CategoryId ?? "_none");

            foreach (var g in grouped)
            {
                var list = g.ToList();
                if (list.Count <= 1 || string.IsNullOrEmpty(g.Key) || g.Key == "_none")
                {
                    output.AddRange(list);
                    continue;
                }

                var compressed = list.OrderByDescending(c => c.SeverityHint).First();
                compressed.RenderedBody = $"{compressed.RenderedBody} ({list.Count} similar in past 48 hours)";
                output.Add(compressed);
            }

            return output;
        }
    }
}
```

- [ ] **Step 2: Register, build, commit**

```xml
<Compile Include="src\Features\Content\StoryAggregator.cs"/>
```

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/StoryAggregator.cs Enlisted.csproj
git commit -m "feat: add StoryAggregator 48h window compression"
```

---

### Task 7: Add quiet-stretch pool JSON (author via existing event system)

**Context:** Author the quiet-stretch pool as real `EventDefinition` entries in a dedicated JSON file under `ModuleData/Enlisted/Events/` (capital E). Load via the existing `EventCatalog` pipeline — no custom loader needed. Effects restricted to `lordReputation` only (per `EventDeliveryManager.cs:620` — officer/soldier reputation is removed; morale is removed per 2026-01-11 cleanup).

**Files:**
- Create: `ModuleData/Enlisted/Events/events_quiet_stretch.json`
- Modify: whichever config/catalog file maps JSON filename → `EventCatalog` loader (grep for an existing `events_*.json` registration)

- [ ] **Step 1: Create `ModuleData/Enlisted/Events/events_quiet_stretch.json`**

Match the schema of an existing `events_*.json` file exactly. **Before writing, read one existing file** (e.g. `events_promotion.json`) to confirm the current field ordering (AGENTS.md rule: `titleId` / `title` pair adjacent). Example entry shape:

```json
{
  "schema_version": 1,
  "events": [
    {
      "id": "evt_quiet_letter_from_home",
      "titleId": "qsp_letter_title",
      "title": "A Letter from Home",
      "setupId": "qsp_letter_setup",
      "setup": "A courier finds you between watches. The hand is your sister's. You break the seal.",
      "category": "quiet_stretch",
      "options": [
        {
          "id": "read_letter",
          "textId": "qsp_letter_read",
          "text": "Read it",
          "tooltip": "Reflect on home. +1 Lord relation.",
          "effects": { "lordReputation": 1 },
          "resultTextId": "qsp_letter_result",
          "resultTextFallback": "You read it twice and fold it into your gear."
        }
      ],
      "requirements": { "tier": { "min": 1, "max": 9 } },
      "timing": { "cooldown_days": 20, "priority": "normal", "one_time": false }
    }
  ]
}
```

Add at least 3 entries (letter-from-home, sergeant-check-in, comrade-banter). **Do not** use `officerReputation`, `soldierReputation`, or any `morale` effect — they are contract-dead.

- [ ] **Step 2: Register the file in EventCatalog's loader list**

Grep for `events_promotion.json` or `events_incidents*.json` in `src/` and `ModuleData/Enlisted/Config/` — add an entry for `events_quiet_stretch.json` in whichever registration point the project uses. (If events are auto-loaded from the directory, no registration is needed — confirm via reading the existing loader.)

- [ ] **Step 3: Validate, commit**

```bash
python Tools/Validation/validate_content.py
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add ModuleData/Enlisted/Events/events_quiet_stretch.json
git commit -m "feat: author quiet-stretch event pool for Director fallback"
```

---

### Task 8: Add StoryDirector skeleton

**Files:**
- Create: `src/Features/Content/StoryDirector.cs`
- Modify: `Enlisted.csproj`
- Modify: whichever `SubModule` / behavior-registration file adds other `CampaignBehaviorBase` instances (grep for `AddBehavior(new` to find it)

- [ ] **Step 1: Create `StoryDirector.cs`**

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    public sealed class StoryDirector : CampaignBehaviorBase
    {
        public static StoryDirector Instance { get; private set; }

        // Project convention: behavior fields use SyncData only, no [SaveableField].
        private int _lastModalDay;
        private long _lastModalUtcTicks;
        private Dictionary<string, int> _categoryCooldowns = new Dictionary<string, int>();
        private List<StoryCandidatePersistent> _deferredInteractive = new List<StoryCandidatePersistent>();

        public IReadOnlyList<StoryCandidatePersistent> DeferredInteractive => _deferredInteractive;

        public override void RegisterEvents()
        {
            Instance = this;
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("storydir_lastModalDay", ref _lastModalDay);
            dataStore.SyncData("storydir_lastModalUtcTicks", ref _lastModalUtcTicks);
            dataStore.SyncData("storydir_categoryCooldowns", ref _categoryCooldowns);
            dataStore.SyncData("storydir_deferredInteractive", ref _deferredInteractive);
        }

        public void EmitCandidate(StoryCandidate candidate)
        {
            try
            {
                if (candidate == null || Hero.MainHero == null)
                {
                    return;
                }

                var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (lord == null)
                {
                    return;
                }

                if (!RelevanceFilter.Passes(candidate.Relevance, lord, MobileParty.MainParty))
                {
                    return;
                }

                bool recentlyVisited = candidate.Relevance.SubjectSettlement != null
                    && Campaign.Current?.GetCampaignBehavior<Enlisted.Mod.Core.Triggers.CampaignTriggerTrackerBehavior>() != null;

                var finalTier = SeverityClassifier.Classify(
                    candidate,
                    candidate.Relevance.TouchesEnlistedLord,
                    candidate.Relevance.TouchesPlayerKingdom,
                    recentlyVisited);

                Route(candidate, finalTier);
            }
            catch (Exception ex)
            {
                ModLogger.ErrorCode("Content", "E-PACE-001", "StoryDirector.EmitCandidate failed", ex);
            }
        }

        // Filled in by Task 14 (modal delivery) and Task 12 (news integration).
        private void Route(StoryCandidate c, StoryTier tier)
        {
            // Skeleton: no-op for now. Implemented in Phase 2+3.
        }
    }
}
```

- [ ] **Step 2: Register the behavior**

Grep for existing `AddBehavior(new ` calls (likely in a `CampaignGameStarter`-consuming behavior-adder file). Append:

```csharp
campaignGameStarter.AddBehavior(new Enlisted.Features.Content.StoryDirector());
```

Register in csproj:
```xml
<Compile Include="src\Features\Content\StoryDirector.cs"/>
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/StoryDirector.cs Enlisted.csproj
git commit -m "feat: add StoryDirector skeleton (registered but routes to no-op)"
```

---

### Task 9: Register save types in EnlistedSaveDefiner

**Files:**
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`

**Context:** Single `BaseId = 735000` with per-override offsets. Existing offsets used: classes 1, 10–14, 20–24; enums 50–52, 60–64, 70–71. `Dictionary<string,int>` container is already registered (line 90). Do not re-register it.

- [ ] **Step 1: Add pacing model class in `DefineClassTypes()`**

```csharp
// Pacing subsystem
AddClassDefinition(typeof(Enlisted.Features.Content.StoryCandidatePersistent), 30);
```

- [ ] **Step 2: Add pacing enums in `DefineEnumTypes()`**

```csharp
// Pacing subsystem enums
AddEnumDefinition(typeof(Enlisted.Features.Content.StoryTier), 80);
AddEnumDefinition(typeof(Enlisted.Features.Content.StoryBeat), 81);
```

- [ ] **Step 3: Add container in `DefineContainerDefinitions()`**

```csharp
// Pacing subsystem containers (Dictionary<string,int> is already registered above)
ConstructContainerDefinition(typeof(System.Collections.Generic.List<Enlisted.Features.Content.StoryCandidatePersistent>));
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
git commit -m "feat: register StoryDirector save types in EnlistedSaveDefiner"
```

---

### Task 10: Smoke-test Phase 1

- [ ] **Step 1:** Build clean, launch Bannerlord, load an existing Enlisted save. Confirm no "Cannot Create Save" or save-load errors. `rgrep E-PACE-` in the mod log expects zero hits.

- [ ] **Step 2:** Save and reload; confirm Director deserializes without exceptions.

(No commit — pure integration check.)

---

## Phase 2 — Routing: Modal gate + news-feed observational path

At end of Phase 2 the Director routes candidates to the right destination; no source emits yet.

### Task 11: Add DensitySettings config reader

**Files:**
- Create: `src/Features/Content/DensitySettings.cs`
- Modify: `ModuleData/Enlisted/Config/enlisted_config.json`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `DensitySettings.cs`**

```csharp
using System.IO;
using Enlisted.Platform;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Content
{
    public static class DensitySettings
    {
        public static int ModalFloorInGameDays { get; private set; } = 5;
        public static int ModalFloorWallClockSeconds { get; private set; } = 60;
        public static int QuietStretchDays { get; private set; } = 14;
        public static bool SpeedDownshiftOnModal { get; private set; } = true;
        public static int CategoryCooldownDays { get; private set; } = 12;

        public static void Load()
        {
            var path = ModulePaths.GetContentPath(Path.Combine("Config", "enlisted_config.json"));
            if (!File.Exists(path)) return;

            var root = JObject.Parse(File.ReadAllText(path));
            var density = root["event_density"]?.ToString() ?? "normal";
            ModalFloorInGameDays = density switch
            {
                "sparse" => 7,
                "dense" => 3,
                _ => 5
            };

            SpeedDownshiftOnModal = root["speed_downshift_on_modal"]?.ToObject<bool>() ?? true;
        }
    }
}
```

Confirm `Enlisted.Platform.ModulePaths.GetContentPath` exists via grep. If absent, replace with direct `ModuleHelper.GetModuleFullPath("Enlisted")` composition — verify against Decompile `TaleWorlds.ModuleManager.ModuleHelper`.

- [ ] **Step 2: Add config keys**

In `ModuleData/Enlisted/Config/enlisted_config.json`, add at top level if missing:

```json
"event_density": "normal",
"speed_downshift_on_modal": true
```

- [ ] **Step 3: Load density on submodule init**

Find `SubModule.OnGameStart` / `OnSubModuleLoad` (grep) and add:

```csharp
Enlisted.Features.Content.DensitySettings.Load();
```

- [ ] **Step 4: Register, validate, build, commit**

```xml
<Compile Include="src\Features\Content\DensitySettings.cs"/>
```

```bash
python Tools/Validation/validate_content.py
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/DensitySettings.cs ModuleData/Enlisted/Config/enlisted_config.json Enlisted.csproj
git commit -m "feat: add DensitySettings reader + config keys"
```

---

### Task 12: Implement Director Route — Modal + Observational → DispatchItem

**Files:**
- Modify: `src/Features/Content/StoryDirector.cs`

**Context:** The Route method must handle three cases:
1. **Modal granted, interactive event present** → pass floors, fire `EventDeliveryManager.QueueEvent(evt)`, optionally downshift game speed. Record cooldowns.
2. **Modal granted, interactive event present, floors fail** → defer the candidate (persist by `InteractiveEventId`) for a later fire. Nothing shown now.
3. **Non-Modal or observational** → compose a `DispatchItem` and append to `EnlistedNewsBehavior._personalFeed` via its public `AddPersonalDispatch` method (if missing, add a thin public wrapper in Task 13).

- [ ] **Step 1: Replace the skeleton `Route` with the real implementation**

```csharp
private void Route(StoryCandidate c, StoryTier tier)
{
    int today = (int)CampaignTime.Now.ToDays;

    if (tier == StoryTier.Modal && c.InteractiveEvent != null)
    {
        if (!ModalFloorsAllow(c, today))
        {
            _deferredInteractive.Add(MakePersistent(c, tier, today));
            return;
        }

        DownshiftSpeedIfNeeded();
        EventDeliveryManager.Instance?.QueueEvent(c.InteractiveEvent);
        _lastModalDay = today;
        _lastModalUtcTicks = System.DateTime.UtcNow.Ticks;
        _categoryCooldowns[c.CategoryId ?? "_none"] = today;
        return;
    }

    // Observational / non-Modal: write through the existing news feed so
    // DispatchItem consumers (muster recap, camp menu) keep their contract.
    WriteDispatchItem(c, tier);
}

private bool ModalFloorsAllow(StoryCandidate c, int today)
{
    double wallSeconds = (System.DateTime.UtcNow.Ticks - _lastModalUtcTicks)
        / (double)System.TimeSpan.TicksPerSecond;

    bool inGameFloor = today - _lastModalDay >= DensitySettings.ModalFloorInGameDays;
    bool wallClockFloor = _lastModalUtcTicks == 0 || wallSeconds >= DensitySettings.ModalFloorWallClockSeconds;
    bool categoryOk = !_categoryCooldowns.TryGetValue(c.CategoryId ?? "_none", out var lastDay)
        || (today - lastDay) >= DensitySettings.CategoryCooldownDays;

    return inGameFloor && wallClockFloor && categoryOk;
}

private static void DownshiftSpeedIfNeeded()
{
    if (!DensitySettings.SpeedDownshiftOnModal) return;
    if (Campaign.Current == null) return;
    if (Campaign.Current.TimeControlMode == CampaignTimeControlMode.StoppableFastForward)
    {
        Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
    }
}

private static StoryCandidatePersistent MakePersistent(StoryCandidate c, StoryTier tier, int today)
{
    return new StoryCandidatePersistent
    {
        SourceId = c.SourceId,
        CategoryId = c.CategoryId,
        GrantedTier = tier,
        Severity = c.SeverityHint,
        EmittedDayNumber = today,
        EmittedHour = CampaignTime.Now.GetHourOfDay,
        InteractiveEventId = c.InteractiveEvent?.Id,
        RenderedTitle = c.RenderedTitle,
        RenderedBody = c.RenderedBody,
        StoryKey = c.StoryKey
    };
}

private static void WriteDispatchItem(StoryCandidate c, StoryTier tier)
{
    var news = Campaign.Current?.GetCampaignBehavior<Enlisted.Features.Interface.Behaviors.EnlistedNewsBehavior>();
    if (news == null) return;

    int severity = c.SeverityLevel;
    if (severity == 0)
    {
        severity = tier switch
        {
            StoryTier.Headline => 2,
            StoryTier.Pertinent => 1,
            _ => 0
        };
    }

    news.AddPersonalDispatch(
        category: c.DispatchCategory ?? "general",
        headlineKey: c.RenderedTitle,
        placeholderValues: null,
        storyKey: c.StoryKey,
        severity: severity,
        minDisplayDays: c.MinDisplayDays);
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/StoryDirector.cs
git commit -m "feat: Director routes Modal via guards; observational via news feed"
```

---

### Task 13: Expose `AddPersonalDispatch` on EnlistedNewsBehavior (thin wrapper)

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs`

**Context:** The behavior already constructs `DispatchItem`s in many places. Find an existing internal add path (grep for `_personalFeed.Add` or a `DispatchItem` constructor call) and expose a minimal public wrapper that mirrors the same severity-based replace/dedup behavior the internal paths use.

- [ ] **Step 1: Read the existing internal add logic**

Find the existing block that:
1. Checks `_personalFeed.FindIndex(x => x.StoryKey == storyKey)`
2. Compares severities (new must be > existing to replace)
3. Falls back to append

Copy that logic into a new public method:

```csharp
public void AddPersonalDispatch(
    string category,
    string headlineKey,
    Dictionary<string, string> placeholderValues,
    string storyKey,
    int severity,
    int minDisplayDays)
{
    try
    {
        if (string.IsNullOrEmpty(headlineKey)) return;

        var item = new DispatchItem
        {
            DayCreated = (int)CampaignTime.Now.ToDays,
            Category = category ?? "general",
            HeadlineKey = headlineKey,
            PlaceholderValues = placeholderValues,
            StoryKey = storyKey,
            Type = DispatchType.Report,
            Confidence = 100,
            MinDisplayDays = Math.Max(1, minDisplayDays),
            FirstShownDay = -1,
            Severity = severity
        };

        // Dedup / sticky-replace by StoryKey if higher severity.
        if (!string.IsNullOrEmpty(storyKey))
        {
            int idx = _personalFeed.FindIndex(x => x.StoryKey == storyKey);
            if (idx >= 0 && _personalFeed[idx].Severity >= severity)
            {
                return;
            }
            if (idx >= 0)
            {
                _personalFeed[idx] = item;
                return;
            }
        }

        _personalFeed.Add(item);
    }
    catch (Exception ex)
    {
        ModLogger.ErrorCode("News", "E-PACE-003", "AddPersonalDispatch failed", ex);
    }
}
```

Match the exact existing pattern (the internal path may have additional trim/cap logic — replicate exactly). **Before writing, read lines 3330–3460 of the file for the canonical pattern.**

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs
git commit -m "feat: expose AddPersonalDispatch for Director observational writes"
```

---

### Task 14: Headlines accordion on enlisted_status

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`

**Context:** The "Headlines" accordion is a filtered view over `_personalFeed` — items with `Severity >= 2` (Attention) not yet displayed to the player this session. Use the existing `[NEW]` badge pattern (lines ~931 for orders).

- [ ] **Step 1: Add Headlines option + submenu**

Insert **above** the orders accordion block:

```csharp
campaignGameStarter.AddGameMenuOption(
    "enlisted_status", "enlisted_status_headlines",
    "{=enl_headlines}Headlines {NEW_TAG}",
    args =>
    {
        var news = Campaign.Current?.GetCampaignBehavior<Enlisted.Features.Interface.Behaviors.EnlistedNewsBehavior>();
        int headlineCount = CountUnreadHeadlines(news);
        if (headlineCount == 0) return false;
        MBTextManager.SetTextVariable("NEW_TAG", "[NEW]");
        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        return true;
    },
    args => GameMenu.SwitchToMenu("enlisted_headlines"),
    false, -1, false);
```

And the submenu:

```csharp
campaignGameStarter.AddGameMenu(
    "enlisted_headlines",
    "{=enl_headlines_body}{HEADLINES_TEXT}",
    args =>
    {
        var news = Campaign.Current?.GetCampaignBehavior<Enlisted.Features.Interface.Behaviors.EnlistedNewsBehavior>();
        MBTextManager.SetTextVariable("HEADLINES_TEXT", FormatHeadlines(news));
    });

campaignGameStarter.AddGameMenuOption(
    "enlisted_headlines", "enlisted_headlines_back",
    "{=enl_back}Back",
    args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
    args => GameMenu.SwitchToMenu("enlisted_status"),
    true, -1, false);
```

And helper methods on the same class:

```csharp
private static int CountUnreadHeadlines(Enlisted.Features.Interface.Behaviors.EnlistedNewsBehavior news)
{
    if (news == null) return 0;
    int today = (int)CampaignTime.Now.ToDays;
    var items = news.GetPersonalFeedSince(today - 7);
    int c = 0;
    foreach (var it in items)
    {
        if (it.Severity >= 2 && it.FirstShownDay < 0) c++;
    }
    return c;
}

private static string FormatHeadlines(Enlisted.Features.Interface.Behaviors.EnlistedNewsBehavior news)
{
    if (news == null) return string.Empty;
    int today = (int)CampaignTime.Now.ToDays;
    var items = news.GetPersonalFeedSince(today - 7);
    var sb = new System.Text.StringBuilder();
    foreach (var it in items)
    {
        if (it.Severity < 2) continue;
        sb.AppendLine($"• {it.HeadlineKey}");
        if (it.FirstShownDay < 0) it.FirstShownDay = today; // mark shown
    }
    return sb.ToString();
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs
git commit -m "feat: Headlines accordion on enlisted_status filtered over _personalFeed"
```

---

### Task 15: Wire muster-intro digest from existing news feed

**Files:**
- Modify: `src/Features/Enlistment/Behaviors/MusterMenuHandler.cs` ← corrected path

- [ ] **Step 1: Append digest to muster-intro body**

Locate the `muster_intro` menu's init lambda (grep for the menu id within the file; the recon confirmed the handler is at `src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:37`). Append to the existing text-variable set:

```csharp
var news = Campaign.Current?.GetCampaignBehavior<Enlisted.Features.Interface.Behaviors.EnlistedNewsBehavior>();
if (news != null)
{
    int lastMuster = /* existing session state field — use the already-tracked LastMusterDayNumber if available; else fallback: today - 12 */;
    var since = news.GetPersonalFeedSince(lastMuster);
    if (since.Count > 0)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine().AppendLine("Since your last muster:");
        foreach (var it in since)
        {
            if (it.Severity < 1) continue;
            sb.AppendLine($"• {it.HeadlineKey}");
        }
        // Append sb.ToString() to the existing body variable
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Enlistment/Behaviors/MusterMenuHandler.cs
git commit -m "feat: muster intro shows news digest from _personalFeed since last muster"
```

---

### Task 16: Quiet-stretch fallback tick

**Files:**
- Modify: `src/Features/Content/StoryDirector.cs`

- [ ] **Step 1: Subscribe to DailyTickEvent and fire fallback**

In `RegisterEvents`:

```csharp
CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
```

Method:

```csharp
private void OnDailyTick()
{
    try
    {
        int today = (int)CampaignTime.Now.ToDays;
        if (today - _lastModalDay < DensitySettings.QuietStretchDays)
        {
            return;
        }

        // Pick a random quiet-stretch event from EventCatalog by category.
        var candidates = EventCatalog.GetEventsByCategory("quiet_stretch").ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var rng = new Random();
        var evt = candidates[rng.Next(candidates.Count)];

        EmitCandidate(new StoryCandidate
        {
            SourceId = "director.quiet_stretch",
            CategoryId = "quiet_stretch",
            ProposedTier = StoryTier.Modal,
            SeverityHint = 0.70f,
            Beats = { StoryBeat.QuietStretchTimeout },
            Relevance = new RelevanceKey { TouchesEnlistedLord = true },
            EmittedAt = CampaignTime.Now,
            InteractiveEvent = evt,
            RenderedTitle = evt.TitleFallback,
            RenderedBody = evt.SetupFallback
        });
    }
    catch (Exception ex)
    {
        ModLogger.ErrorCode("Content", "E-PACE-002", "Quiet-stretch tick failed", ex);
    }
}
```

Confirm `EventCatalog.GetEventsByCategory` exists; if the project uses a different method name, adapt. Grep.

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/StoryDirector.cs
git commit -m "feat: quiet-stretch fallback fires Modal after 14 silent days"
```

---

## Phase 3 — Caller migrations

**The pattern** (apply to each caller in Tasks 17–29):

```csharp
// BEFORE:
EventDeliveryManager.Instance?.QueueEvent(evt);

// AFTER:
StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "<caller>.<context>",
    CategoryId = "<category>",
    ProposedTier = StoryTier.Modal,           // or Pertinent / Log per recon table
    SeverityHint = <0.0–1.0>,
    Beats = { <appropriate beat> },
    Relevance = new RelevanceKey { TouchesEnlistedLord = true /* or set SubjectHero/etc */ },
    EmittedAt = CampaignTime.Now,
    InteractiveEvent = evt,
    RenderedTitle = evt.TitleFallback,
    RenderedBody = evt.SetupFallback,
    StoryKey = evt.Id
});
```

Each task below gives the specific source/category/tier/beat bindings. **Do not delete any source-side selection logic** (filters, weights, `EventCatalog.GetEventsByOrderType`, etc.) — only replace the delivery call.

### Task 17: MapIncidentManager

**File:** `src/Features/Content/MapIncidentManager.cs:361`

Bindings by context: `leaving_battle` → Beat `PlayerBattleEnd`, Tier `Modal`, Severity 0.6, Category `incident.leaving_battle`. `entering_town|entering_village` → Beat `SettlementEntered`, Tier `Pertinent`, Severity 0.3. `during_siege` → Beat `SiegeBegin`, Tier `Headline`, Severity 0.5. Relevance `TouchesEnlistedLord = true`.

- [ ] **Step:** Apply the migration pattern. Build + commit.

```bash
git commit -m "refactor: MapIncidentManager routes incidents through StoryDirector"
```

### Task 18: ContentOrchestrator (committed opportunities)

**File:** `src/Features/Content/ContentOrchestrator.cs:274`

Beat `OrderPhaseTransition`, Tier `Modal`, Severity 0.5, Category `opportunity.{opp.Type}`. Keep the full `ConvertDecisionToEvent` logic; swap only the terminal `QueueEvent` call.

- [ ] **Step:** Apply pattern. Build + commit.

```bash
git commit -m "refactor: ContentOrchestrator routes committed opportunities through Director"
```

### Task 19: EnlistedMenuBehavior (camp opportunity pick)

**File:** `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:5419`

Beat `OrderPhaseTransition`, Tier `Modal`, Severity 0.5, Category `opportunity.{decision.Category}`. Relevance `TouchesEnlistedLord = true`.

- [ ] **Step:** Apply pattern. Build + commit.

```bash
git commit -m "refactor: EnlistedMenuBehavior opportunity click routes through Director"
```

### Task 20: EventPacingManager (chain events)

**File:** `src/Features/Content/EventPacingManager.cs:111`

Beat `OrderComplete`, Tier `Modal`, Severity 0.55, Category `chain.{chainId}`. **Important:** This is a chain continuation; the Modal gate *should not defer* these — they're narrative continuations the player already opted into. Override the floor for chain events: set `ProposedTier = StoryTier.Modal` and set `CategoryId = "chain_event"` (no cooldown — or add an exception list in `ModalFloorsAllow`). Recommend: add a `ChainContinuation` flag to `StoryCandidate` that bypasses `_categoryCooldowns` (but still respects the wall-clock 60s for sanity).

- [ ] **Step 1: Add `ChainContinuation` flag to `StoryCandidate` and skip cooldown in `ModalFloorsAllow` when set**

```csharp
// In StoryCandidate.cs:
public bool ChainContinuation { get; set; }
```

```csharp
// In StoryDirector.ModalFloorsAllow:
if (c.ChainContinuation)
{
    return wallClockFloor; // skip in-game floor + category cooldown
}
```

- [ ] **Step 2:** Apply migration pattern with `ChainContinuation = true`. Build + commit.

```bash
git commit -m "refactor: EventPacingManager chain events route through Director (bypass floor)"
```

### Task 21: PromotionBehavior

**File:** `src/Features/Ranks/Behaviors/PromotionBehavior.cs:367`

Beat `OrderComplete`, Tier `Modal`, Severity 0.85, Category `promotion.tier_{tier}`. Relevance `TouchesEnlistedLord = true`. Promotion events are *rare and load-bearing* — treat as `ChainContinuation = true` so they don't get deferred behind a pacing floor.

- [ ] **Step:** Apply pattern with `ChainContinuation = true`. Build + commit.

```bash
git commit -m "refactor: PromotionBehavior routes through Director (bypass floor)"
```

### Task 21b: PromotionBehavior — annotate ceremony ShowInquiry as intentional bypass

**File:** `src/Features/Ranks/Behaviors/PromotionBehavior.cs:434`

**Context:** `TriggerPromotionNotification` fires a ceremonial `ShowInquiry` modal (title + body + single "Understood" button) *synchronously from inside the proving event's success callback*. The proving event itself was already gated by the Director in Task 21. The ceremony popup is the completion half of a two-beat narrative moment — it must fire immediately after the proving option is chosen, within the same wall-clock second.

Routing the ceremony through the Director is the **wrong** fix: `ChainContinuation = true` bypasses the in-game floor and category cooldown but still respects the 60s wall-clock guard, which would swallow the ceremony. And the ceremony has no `Options`/`Effects`/`Requirements` graph — it is not an `EventDefinition`, only an inline `InquiryData`. So we annotate it as an intentional bypass, matching the pattern used for `DebugToolsBehavior` (Task 29).

- [ ] **Step 1: Add annotating comment above the ShowInquiry call**

Directly above line 434:

```csharp
// Intentional bypass of StoryDirector — this ceremony popup fires synchronously
// as the completion beat of a proving event that the Director already paced.
// Routing it through the Director would trip the 60s wall-clock guard and
// swallow the ceremony. If the promotion ceremony UX changes (e.g. demoted
// to accordion headline instead of a modal), revisit this bypass.
InformationManager.ShowInquiry(data);
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Ranks/Behaviors/PromotionBehavior.cs
git commit -m "refactor: annotate PromotionBehavior ceremony ShowInquiry as Director bypass"
```

---

### Task 21c: OrderManager — annotate insubordination warning as intentional bypass

**File:** `src/Features/Orders/Behaviors/OrderManager.cs:1043`

**Context:** `TriggerDischargeWarning` fires a system-initiated `ShowInquiry` after the player declines orders repeatedly. It's a pure feedback modal — no options, no effects, no `EventDefinition` graph. Fires at most once per decline-count threshold crossing, so it self-paces by trigger condition.

Router support for observational modals (non-`EventDefinition` Modal candidates) is a followup. For v1: annotate as bypass.

- [ ] **Step 1: Add annotating comment**

Above line 1043:

```csharp
// Intentional bypass of StoryDirector — this warning fires once per decline-count
// threshold and is self-paced by its trigger condition. Routing through Director
// would require observational-modal support (InteractiveEvent == null + tier == Modal),
// which is a followup. If the warning starts firing excessively, revisit.
InformationManager.ShowInquiry(new InquiryData( ... ), true);
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Orders/Behaviors/OrderManager.cs
git commit -m "refactor: annotate OrderManager insubordination warning as Director bypass"
```

---

### Task 21d: CampOpportunityGenerator — annotate dereliction notification as intentional bypass

**File:** `src/Features/Camp/CampOpportunityGenerator.cs:1594`

**Context:** "Dereliction of Duty" modal fires when the player is caught skipping duty. Like insubordination, it's a player-feedback modal with no options/effects — self-paces by the underlying detection probability and scrutiny state.

- [ ] **Step 1: Add annotating comment**

Above line 1594:

```csharp
// Intentional bypass of StoryDirector — fires on detection check; self-paced by
// the underlying detection probability and scrutiny state. Observational-modal
// routing is a followup.
InformationManager.ShowInquiry(new InquiryData( ... ), true);
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Camp/CampOpportunityGenerator.cs
git commit -m "refactor: annotate CampOpportunityGenerator dereliction modal as Director bypass"
```

---

### Task 22: EscalationManager

**File:** `src/Features/Escalation/EscalationManager.cs:938`

Beat `EscalationThreshold`, Tier `Modal`, Severity 0.80, Category `escalation.{thresholdKind}` (scrutiny / medical / lord_rep). Relevance `TouchesEnlistedLord = true`.

- [ ] **Step:** Apply pattern. Build + commit.

```bash
git commit -m "refactor: EscalationManager threshold events route through Director"
```

### Task 23: EnlistmentBehavior (bag check, 2 call sites)

**File:** `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2120` and `:6438`

Beat `OrderComplete`, Tier `Modal`, Severity 0.90, Category `enlistment.bag_check`. **Critical:** these are mandatory gates before enlistment completes — `ChainContinuation = true`. Under no circumstances should the Director defer a bag check.

- [ ] **Step:** Apply pattern with `ChainContinuation = true` at both call sites. Build + commit.

```bash
git commit -m "refactor: EnlistmentBehavior bag-check routes through Director (bypass floor)"
```

### Task 24: CompanySimulationBehavior (crisis + illness)

**File:** `src/Features/Camp/CompanySimulationBehavior.cs:621` (crisis) and `:834` (illness onset)

**Crisis:** Beat `CompanyCrisisThreshold`, Tier `Modal`, Severity 0.80, Category `company.crisis.{kind}`.
**Illness onset:** Beat `EscalationThreshold`, Tier `Modal`, Severity 0.70, Category `company.illness`.

- [ ] **Step:** Apply pattern at both sites. Build + commit.

```bash
git commit -m "refactor: CompanySimulationBehavior crisis and illness route through Director"
```

### Task 25: CampOpportunityGenerator (supply pressure + scheduled commitment)

**File:** `src/Features/Camp/CampOpportunityGenerator.cs:671` (supply pressure) and `:183` (scheduled commitment)

**Supply pressure:** Beat `CompanyPressureThreshold`, Tier `Pertinent`, Severity 0.25, Category `company.supply_pressure`. Demote away from Modal — these should land in the accordion as headlines, not block the player.

**Scheduled commitment:** Beat `OrderPhaseTransition`, Tier `Modal`, Severity 0.40, Category `commitment.{decisionId}`. Player opted in.

- [ ] **Step:** Apply pattern. Supply pressure uses `ProposedTier = StoryTier.Pertinent` and a short observational title/body instead of `InteractiveEvent` (it becomes an accordion entry, not a modal). Scheduled commitment keeps `InteractiveEvent`. Build + commit.

```bash
git commit -m "refactor: CampOpportunityGenerator demotes supply pressure, routes commitments"
```

### Task 26: OrderProgressionBehavior (order outcome event — preserve authored content)

**File:** `src/Features/Orders/Behaviors/OrderProgressionBehavior.cs:308` (outcome)

**Outcome:** Beat `OrderComplete`, Tier `Modal`, Severity 0.55, Category `order_complete.{orderId}`, `ChainContinuation = true` (player already opted into the order — don't defer the outcome).

**For the phase-roll at `TryGetOrderEvent` (line 181):** do **not** delete. Instead, replace the terminal `QueueEvent` inside `ProcessPhase` with a Director emit. The random roll + `EventCatalog.GetEventsByOrderType` + filtering logic stays intact. The Director then decides whether to fire Modal or defer/demote. Beat `OrderPhaseTransition`, Tier `Modal` (it's an authored interactive event), Category `order_phase.{orderId}`, Severity 0.45.

- [ ] **Step 1:** Migrate the outcome fire with `ChainContinuation = true`.
- [ ] **Step 2:** Migrate the phase-event fire (no `ChainContinuation` — it respects the 5-day floor; this is correct because phase events are incidental, not narrative-load-bearing). Build + commit.

```bash
git commit -m "refactor: OrderProgressionBehavior routes outcomes and phase events through Director"
```

### Task 27: RetinueCasualtyTracker (veteran memorial → demote)

**File:** `src/Features/Retinue/Systems/RetinueCasualtyTracker.cs:669`

Veteran memorials are flavor, not crisis. Demote: Beat `PlayerBattleEnd`, Tier `Pertinent`, Severity 0.20, Category `retinue.memorial`, **no `InteractiveEvent`** — instead set `RenderedTitle = "Veteran fallen: {name}"`, `RenderedBody = evt.SetupFallback`, `StoryKey = "memorial:{heroStringId}"`, `DispatchCategory = "retinue"`, `SeverityLevel = 1` (Positive/quiet). Routes as accordion-only entry.

- [ ] **Step:** Apply pattern without `InteractiveEvent`. Build + commit.

```bash
git commit -m "refactor: RetinueCasualtyTracker demotes veteran memorials to accordion entries"
```

### Task 28: RetinueManager (loyalty threshold)

**File:** `src/Features/Retinue/Core/RetinueManager.cs:739`

Beat `EscalationThreshold`, Tier `Modal`, Severity 0.70, Category `retinue.loyalty.{threshold}`.

- [ ] **Step:** Apply pattern. Build + commit.

```bash
git commit -m "refactor: RetinueManager loyalty threshold routes through Director"
```

### Task 28b: RetinueManager — annotate Commander's Commission as intentional bypass

**File:** `src/Features/Retinue/Core/RetinueManager.cs:1017`

**Context:** "Commander's Commission" modal fires when the player reaches T7 — a retinue-unlock narrative beat that follows the T7 promotion ceremony. Same two-beat-chain reasoning as `PromotionBehavior.cs:434` (Task 21b): tied to a Director-gated event the player just completed, must fire within seconds, has no `EventDefinition` graph. Fires exactly once per character (T7 is one-time), so zero spam risk.

- [ ] **Step 1: Add annotating comment**

Above line 1017:

```csharp
// Intentional bypass of StoryDirector — fires exactly once when the player reaches
// T7, immediately after the promotion ceremony (PromotionBehavior.cs:434). Same
// two-beat chain reasoning as Task 21b: routing through Director would trip the
// 60s wall-clock guard and swallow this commission modal.
InformationManager.ShowInquiry(new InquiryData( ... ));
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Retinue/Core/RetinueManager.cs
git commit -m "refactor: annotate RetinueManager Commander's Commission as Director bypass"
```

---

### Task 29: DebugToolsBehavior — explicit bypass

**File:** `src/Debugging/Behaviors/DebugToolsBehavior.cs:139`

Debug tool must bypass the Director to allow immediate testing of arbitrary events. Leave `EventDeliveryManager.Instance?.QueueEvent(evt)` in place; add a comment:

```csharp
// Intentional bypass of StoryDirector — this debug path must fire arbitrary events
// immediately for testing, regardless of pacing guards.
EventDeliveryManager.Instance?.QueueEvent(evt);
```

- [ ] **Step:** Add the comment. Build + commit.

```bash
git commit -m "refactor: annotate DebugToolsBehavior QueueEvent as intentional Director bypass"
```

### Task 30: Retry deferred interactive candidates on DailyTick

**Files:** `src/Features/Content/StoryDirector.cs`

- [ ] **Step 1:** In `OnDailyTick`, before the quiet-stretch check, attempt to flush deferred interactive candidates (at most 1 per tick):

```csharp
if (_deferredInteractive.Count > 0)
{
    var next = _deferredInteractive[0];
    var evt = string.IsNullOrEmpty(next.InteractiveEventId) ? null : EventCatalog.GetEventById(next.InteractiveEventId);
    if (evt != null)
    {
        int today = (int)CampaignTime.Now.ToDays;
        var pseudo = new StoryCandidate
        {
            SourceId = next.SourceId,
            CategoryId = next.CategoryId,
            InteractiveEvent = evt
        };
        if (ModalFloorsAllow(pseudo, today))
        {
            _deferredInteractive.RemoveAt(0);
            DownshiftSpeedIfNeeded();
            EventDeliveryManager.Instance?.QueueEvent(evt);
            _lastModalDay = today;
            _lastModalUtcTicks = DateTime.UtcNow.Ticks;
            _categoryCooldowns[next.CategoryId ?? "_none"] = today;
            return; // one per day
        }
    }
    else
    {
        // Event no longer in catalog; drop silently.
        _deferredInteractive.RemoveAt(0);
    }
}
```

- [ ] **Step 2:** Cap deferred list at 32 entries (drop oldest on overflow in `Route`):

```csharp
const int DeferredCap = 32;
if (_deferredInteractive.Count > DeferredCap)
{
    _deferredInteractive.RemoveAt(0);
}
```

- [ ] **Step 3:** Build + commit.

```bash
git commit -m "feat: Director retries deferred interactive candidates on DailyTick"
```

---

## Phase 4 — Verification

### Task 31: Integration smoke-test battery

Document in `docs/superpowers/plans/2026-04-18-event-pacing-verification.md`.

- [ ] **Scenario 1: Quiet week** — 14 in-game days pass without a modal-eligible event. Expect: one quiet-stretch modal fires.
- [ ] **Scenario 2: Active week** — 3 Modal-eligible events fire in quick succession. Expect: first fires, second+third deferred, deferred fire on successive DailyTicks once floors pass.
- [ ] **Scenario 3: FastForward compression** — player at 3x speed; 2 modals qualify within 20s wall-clock. Expect: first fires with speed downshift, second deferred by 60s wall-clock guard.
- [ ] **Scenario 4: Chain event** — a promotion or chain event fires inside a pacing window. Expect: `ChainContinuation` bypasses the floor and fires immediately.
- [ ] **Scenario 5: Muster digest** — DispatchItems accumulate over 10 days. Muster menu shows them under "Since your last muster:".
- [ ] **Scenario 6: Headlines accordion** — high-severity DispatchItem posts; `[NEW]` badge appears on `enlisted_status`; clicking clears the badge on that item.
- [ ] **Scenario 7: Save/load with deferred queue** — save mid-deferred-queue, reload; deferred items persist; DailyTick continues flushing.
- [ ] **Scenario 8: Relevance drop** — a war declared between two distant factions (neither your kingdom nor your lord's enemy). Expect: no DispatchItem created, no accordion entry, zero log output beyond the filter-drop trace.

- [ ] **Step:** Run all 8 scenarios. Record results with game-day + wall-clock timestamps. Commit the verification doc.

```bash
git add docs/superpowers/plans/2026-04-18-event-pacing-verification.md
git commit -m "docs: record event-pacing smoke-test verification"
```

### Task 32: Docs update

- [ ] Add a row to `docs/INDEX.md` linking the spec + plan + verification.
- [ ] Update `docs/BLUEPRINT.md` content-delivery section to reference `StoryDirector` as the single gate for modal events.
- [ ] Commit.

```bash
git commit -m "docs: link event-pacing spec/plan/verification from INDEX + BLUEPRINT"
```

---

## Out-of-scope (explicit)

- New content authoring beyond the 3 quiet-stretch event stubs
- MCM UI for density (JSON-only for v1)
- Changes to `EnlistmentBehavior`'s escort-AI / encounter-suppression stack beyond the two bag-check migrations
- Refactoring `EnlistedNewsBehavior`'s internal feed trimming, kingdom-feed logic, or rumor system
- `EventDeliveryManager.QueueEvent` access modifier changes — intentionally kept public (with a conventional comment that only Director + debug should call it)
- Removal of `CampRoutineProcessor`'s existing `AddToNewsFeed` pattern (already writes to `_personalFeed` — leave alone; a future task can route through Director if needed)

## Followups (parked for a subsequent spec)

Identified during the adversarial audit but deliberately out of this plan. Each deserves its own design pass.

- **Ambient reputation surfacing.** `EscalationManager.ModifyLordReputation(delta, reason)` at `EscalationManager.cs:473` is a pure mutator — reputation deltas from event effects currently only appear in `ModLogger`. Could emit Log-tier Director candidates so players see a scrolling "+1 Lord Relation (for X)" trail in the news feed. Design question: which reasons surface, and do we aggregate same-day deltas into one entry? Not a refactor prerequisite; decide separately.

- **Promotion pre-thresholds / rank-approaching hints.** No content today surfaces "1 battle from Sergeant" or "scrutiny at 60, one more incident and you'll be Watched." These would be Pertinent-tier Director candidates at sub-threshold boundaries (e.g. 80% of promotion requirement met; scrutiny ≥ (threshold − 15); medical risk ≥ 2). Design question: which thresholds get hints, and at what proximity? Not a refactor prerequisite.

- **Promotion ceremony demotion option.** Task 21b preserves the proving → ceremony two-modal flow. If playtesting finds the ceremony feels redundant after the proving event, a followup can demote the ceremony to an accordion Headline + chat banner (the chat banner at `PromotionBehavior.cs:411, 415` already prints the rank change). Preserves all information, drops one modal per promotion. Design call only — do not pre-implement.

- **Observational modal support in StoryDirector.** Tasks 21b, 21c, 21d, 28b annotate four system-initiated `ShowInquiry` sites as intentional bypass because they have no `EventDefinition` graph (no options, effects, requirements, or chains — just title + body + "Understood"). A proper router would add an `ObservationalModal` path to `StoryDirector.Route`: `tier == Modal && InteractiveEvent == null` renders via `ShowInquiry` using `RenderedTitle`/`RenderedBody`, subject to the same guards as interactive modals. Worth doing after v1 ships and playtest confirms the 4 bypassed sites don't cause pacing issues in practice.

## Notes on departures from v1

- **v1 Task 24 (make QueueEvent internal) is DELETED.** The Director is additive; `QueueEvent` stays public.
- **v1 Task 21 (delete OrderProgressionBehavior random rolls) is REPLACED by Task 26**, which preserves `EventCatalog.GetEventsByOrderType` selection and routes the result through the Director instead.
- **v1 Task 18 (flatten `_personalFeed` to strings) is DELETED.** `DispatchItem` contract preserved.
- **v1 Task 23 (gut ContentOrchestrator direct queuing) is REPLACED by Task 18**, which preserves committed-opportunity delivery via Director routing.
- **Invented `VisitedSettlementTracker` is DELETED.** Replaced by calls into the existing `CampaignTriggerTrackerBehavior.IsWithinDays` API.
- **Added `ChainContinuation` flag** for narrative continuations (chain events, promotions, bag checks) that must bypass the 5-day floor.
- **Added deferred-interactive retry queue (Task 30)** so events blocked by the floor aren't lost — they fire on subsequent days once budget opens.

## Self-review

- **Spec coverage:** Tiers (Tasks 1, 8, 12), beats (Task 5), relevance (Task 4 via CampaignTriggerTrackerBehavior), severity (Task 5), aggregation (Task 6), digest (Task 15), modal guards (Task 12), wall-clock (Task 12), speed downshift (Task 12), density (Task 11), fallback (Tasks 7, 16), quiet-stretch (Tasks 7, 16), caller migrations (Tasks 17–29), retry (Task 30), smoke tests (Task 31).
- **All 14 production `QueueEvent` callers accounted for:** MapIncident (17), ContentOrchestrator (18), EnlistedMenuBehavior (19), EventPacingManager (20), Promotion (21), Escalation (22), Enlistment ×2 (23), CompanySim ×2 (24), CampOpportunity ×2 (25), OrderProgression ×2 (26), RetinueCasualty (27), Retinue (28), Debug (29 — bypass).
- **Plus four non-QueueEvent system-initiated modal sites accounted for (all annotated as intentional bypass):**
  - `PromotionBehavior.cs:434` (ceremony popup) — Task 21b; two-beat chain after proving event
  - `OrderManager.cs:1043` (insubordination warning) — Task 21c; self-paced by decline-count threshold
  - `CampOpportunityGenerator.cs:1594` (dereliction notification) — Task 21d; self-paced by detection check
  - `RetinueManager.cs:1017` (Commander's Commission at T7) — Task 28b; two-beat chain after T7 promotion
- **User-initiated `ShowInquiry` sites are NOT gated** (intentional): `EnlistedMenuBehavior` leave/desert/orders confirmations, `CampMenuHandler` retinue confirmations, `MusterMenuHandler` muster confirmations. Player clicked a menu option; the modal is a natural continuation of that click, not a system-initiated interrupt.
- **No deletion of authored content:** OrderProgression retains `EventCatalog.GetEventsByOrderType`; ContentOrchestrator retains opportunity delivery; MapIncident retains `EventCatalog.GetEventsForContext`; EnlistedNewsBehavior retains `DispatchItem` and `GetPersonalFeedSince`.
- **Path corrections:** `Events/` (capital E); `src/Features/Enlistment/Behaviors/MusterMenuHandler.cs`; `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`.
- **No reintroduction of removed concepts:** quiet-stretch JSON uses only `lordReputation` effect; no `morale`, no `officerReputation`, no `soldierReputation`.
- **API grounding:** Every TaleWorlds reference verified against Decompile in the prior audit pass.

## Completion criteria

- All 32 tasks committed.
- `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds.
- `python Tools/Validation/validate_content.py` passes.
- All 8 smoke-test scenarios pass and are documented.
- No `E-PACE-*` warnings in a 30-minute play session.
- `EventDeliveryManager.QueueEvent` has exactly two non-Director callers: `DebugToolsBehavior` (explicit bypass) and the Director itself.
