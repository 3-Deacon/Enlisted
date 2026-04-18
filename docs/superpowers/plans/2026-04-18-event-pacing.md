# Event Pacing Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace six independent event schedulers with a single `StoryDirector` that routes all candidates through a three-tier model (Modal / Accordion / Log) with beat-anchored triggers, relevance filter, severity classifier, aggregation, and wall-clock-aware modal discipline.

**Architecture:** Director-pattern broker. Story-sources emit `StoryCandidate` objects; the Director filters (relevance), classifies (severity → tier), aggregates (48h window), and surfaces through the appropriate renderer (existing `EnlistedMenuBehavior` accordion for Headline/Pertinent/Log; existing `EventDeliveryManager` for Modal). Uses existing Bannerlord campaign event hooks as beats. No free-running timers.

**Tech Stack:** C# / .NET 4.7.2 / Bannerlord v1.3.13 API / Harmony / Newtonsoft.Json. No unit test project — verification is `dotnet build`, `python Tools/Validation/validate_content.py`, plus in-game smoke scenarios at phase boundaries.

**Spec:** [`docs/superpowers/specs/2026-04-18-event-pacing-design.md`](../specs/2026-04-18-event-pacing-design.md)

---

## Conventions for every task

- **After each code change:** run `dotnet build -c "Enlisted RETAIL" /p:Platform=x64`. Must succeed before moving on.
- **After each JSON change:** run `python Tools/Validation/validate_content.py`. Must pass.
- **New C# files:** register in `Enlisted.csproj` with `<Compile Include="src\path\File.cs"/>`.
- **New save types:** register in `EnlistedSaveDefiner` (save-type base id range **735700–735799** reserved for pacing subsystem).
- **Commit after each task** with conventional prefix: `feat:`, `refactor:`, `build:`, `docs:`.
- **Never use Context7 or web for TaleWorlds APIs** — verify against local `Decompile/` only.

---

## Phase 1 — Director infrastructure

Ship plan: all of Phase 1 is a no-op at runtime (Director is registered but no source emits yet, no renderer reads from it yet). Safe to merge mid-phase.

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

Add inside the existing `<ItemGroup>` of `<Compile Include>` entries:

```xml
<Compile Include="src\Features\Content\StoryTier.cs"/>
<Compile Include="src\Features\Content\StoryBeat.cs"/>
```

- [ ] **Step 4: Build**

Run: `dotnet build -c "Enlisted RETAIL" /p:Platform=x64`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
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
using TaleWorlds.CampaignSystem.Party;
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

- [ ] **Step 2: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Content\RelevanceKey.cs"/>
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/RelevanceKey.cs Enlisted.csproj
git commit -m "feat: add RelevanceKey struct for pacing filter"
```

---

### Task 3: Add StoryCandidate and StoryCandidatePersistent

**Files:**
- Create: `src/Features/Content/StoryCandidate.cs`
- Create: `src/Features/Content/StoryCandidatePersistent.cs`
- Create: `src/Features/Content/IStorySource.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `IStorySource.cs`**

```csharp
using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Content
{
    public interface IStorySource
    {
        string SourceId { get; }
        Action<StoryTier> RehydratePayload(string payloadKey);
    }
}
```

- [ ] **Step 2: Create `StoryCandidate.cs`**

```csharp
using System;
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
        public Action<StoryTier> Payload { get; set; }
        public string PayloadKey { get; set; }
        public string RenderedTitle { get; set; }
        public string RenderedBody { get; set; }
    }
}
```

- [ ] **Step 3: Create `StoryCandidatePersistent.cs`**

```csharp
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.Content
{
    public sealed class StoryCandidatePersistent
    {
        [SaveableField(1)] public string SourceId;
        [SaveableField(2)] public string CategoryId;
        [SaveableField(3)] public StoryTier GrantedTier;
        [SaveableField(4)] public float Severity;
        [SaveableField(5)] public int EmittedDayNumber;
        [SaveableField(6)] public int EmittedHour;
        [SaveableField(7)] public string PayloadKey;
        [SaveableField(8)] public string RenderedTitle;
        [SaveableField(9)] public string RenderedBody;
    }
}
```

- [ ] **Step 4: Register in csproj, build, commit**

```xml
<Compile Include="src\Features\Content\IStorySource.cs"/>
<Compile Include="src\Features\Content\StoryCandidate.cs"/>
<Compile Include="src\Features\Content\StoryCandidatePersistent.cs"/>
```

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/IStorySource.cs src/Features/Content/StoryCandidate.cs src/Features/Content/StoryCandidatePersistent.cs Enlisted.csproj
git commit -m "feat: add StoryCandidate schema and IStorySource interface"
```

---

### Task 4: Add RelevanceFilter

**Files:**
- Create: `src/Features/Content/RelevanceFilter.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `RelevanceFilter.cs`**

```csharp
using Enlisted.Features.Enlistment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    public static class RelevanceFilter
    {
        private const float TravelDaysThreshold = 7f;

        public static bool Passes(RelevanceKey key, Hero enlistedLord, MobileParty playerParty)
        {
            if (enlistedLord == null || playerParty == null)
            {
                return false;
            }

            if (key.TouchesEnlistedLord)
            {
                return true;
            }

            if (key.TouchesPlayerKingdom)
            {
                return true;
            }

            if (key.SubjectHero != null && key.SubjectHero == enlistedLord)
            {
                return true;
            }

            if (key.SubjectKingdom != null && key.SubjectKingdom == Hero.MainHero?.Clan?.Kingdom)
            {
                return true;
            }

            if (key.SubjectSettlement != null && VisitedSettlementTracker.HasVisited(key.SubjectSettlement))
            {
                return true;
            }

            if (key.SubjectHero != null && HeroKnownToPlayer(key.SubjectHero))
            {
                return true;
            }

            if (!key.SubjectPosition.IsNonZero)
            {
                return false;
            }

            float distance = playerParty.Position2D.Distance(key.SubjectPosition);
            float daysOfTravel = distance / (playerParty.Speed > 0f ? playerParty.Speed : 5f);
            if (daysOfTravel <= TravelDaysThreshold)
            {
                return true;
            }

            if (key.SubjectFaction != null && IsActiveEnemyOfLord(key.SubjectFaction, enlistedLord))
            {
                return true;
            }

            return false;
        }

        private static bool HeroKnownToPlayer(Hero hero)
        {
            if (hero?.IsAlive != true)
            {
                return false;
            }

            float rel = Hero.MainHero.GetRelation(hero);
            return rel > 0f;
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

- [ ] **Step 2: Note dependency**

The `VisitedSettlementTracker.HasVisited` call assumes an existing tracker. Confirm with Grep — if missing, create a stub under `src/Features/Enlistment/VisitedSettlementTracker.cs`:

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.Enlistment
{
    public static class VisitedSettlementTracker
    {
        private static readonly HashSet<string> _visited = new HashSet<string>();

        public static void Record(Settlement s)
        {
            if (s?.StringId != null)
            {
                _visited.Add(s.StringId);
            }
        }

        public static bool HasVisited(Settlement s) =>
            s?.StringId != null && _visited.Contains(s.StringId);
    }
}
```

Hook `Record(settlement)` into the existing `OnSettlementEntered` handler in `EnlistmentBehavior` (search for existing subscription — add a single line to the existing method).

- [ ] **Step 3: Register in csproj, build, commit**

```xml
<Compile Include="src\Features\Content\RelevanceFilter.cs"/>
<Compile Include="src\Features\Enlistment\VisitedSettlementTracker.cs"/>
```

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/RelevanceFilter.cs src/Features/Enlistment/VisitedSettlementTracker.cs src/Features/Enlistment/EnlistmentBehavior.cs Enlisted.csproj
git commit -m "feat: add RelevanceFilter and VisitedSettlementTracker"
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

        public static StoryTier Classify(StoryCandidate c, bool touchesEnlistedLord, bool touchesPlayerKingdom, bool visitedSettlement)
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
            if (visitedSettlement) score += 0.05f;

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

- [ ] **Step 2: Register in csproj, build, commit**

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

- [ ] **Step 2: Register in csproj, build, commit**

```xml
<Compile Include="src\Features\Content\StoryAggregator.cs"/>
```

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/StoryAggregator.cs Enlisted.csproj
git commit -m "feat: add StoryAggregator 48h window compression"
```

---

### Task 7: Add QuietStretchPool and JSON content

**Files:**
- Create: `src/Features/Content/QuietStretchPool.cs`
- Create: `ModuleData/Enlisted/events/quiet_stretch_pool.json`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create authoring stub `quiet_stretch_pool.json`**

```json
{
  "schema_version": 1,
  "pool": [
    {
      "id": "quiet_letter_from_home",
      "titleId": "qsp_letter_title",
      "title": "A Letter from Home",
      "setupId": "qsp_letter_setup",
      "setup": "A courier finds you between watches. The hand is your sister's. You break the seal.",
      "options": [
        {
          "id": "read_letter",
          "textId": "qsp_letter_read",
          "text": "Read it",
          "tooltip": "Reflect on home. +1 Lord rep, small morale.",
          "effects": { "lordReputation": 1 },
          "resultText": "You read it twice and fold it into your gear."
        }
      ],
      "requirements": { "tier": { "min": 1, "max": 9 } }
    },
    {
      "id": "quiet_sergeant_checkin",
      "titleId": "qsp_sergeant_title",
      "title": "A Word from the Sergeant",
      "setupId": "qsp_sergeant_setup",
      "setup": "The sergeant finds you during drill and nods once. 'Hard stretch ahead. Keep your edge.'",
      "options": [
        {
          "id": "acknowledge",
          "textId": "qsp_sergeant_ack",
          "text": "Understood",
          "tooltip": "Acknowledge. +1 Officer rep.",
          "effects": { "officerReputation": 1 },
          "resultText": "He moves on. The drill continues."
        }
      ],
      "requirements": { "tier": { "min": 1, "max": 9 } }
    }
  ]
}
```

- [ ] **Step 2: Create `QuietStretchPool.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Platform;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Content
{
    public static class QuietStretchPool
    {
        private static readonly List<JObject> _pool = new List<JObject>();
        private static readonly Random _rng = new Random();
        private static bool _loaded;

        public static void Load()
        {
            _loaded = true;
            _pool.Clear();
            var path = ModulePaths.GetContentPath(Path.Combine("events", "quiet_stretch_pool.json"));
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var root = JObject.Parse(json);
            var pool = root["pool"] as JArray;
            if (pool == null)
            {
                return;
            }

            foreach (var entry in pool.OfType<JObject>())
            {
                _pool.Add(entry);
            }
        }

        public static JObject PickOne()
        {
            if (!_loaded)
            {
                Load();
            }

            if (_pool.Count == 0)
            {
                return null;
            }

            return _pool[_rng.Next(_pool.Count)];
        }
    }
}
```

Note: `ModulePaths.GetContentPath` is an existing helper per AGENTS.md rule 9. Confirm with Grep. If missing, use `Path.Combine(TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath("Enlisted"), "ModuleData", "Enlisted", ...)`.

- [ ] **Step 3: Register, validate content, build, commit**

```xml
<Compile Include="src\Features\Content\QuietStretchPool.cs"/>
```

```bash
python Tools/Validation/validate_content.py
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/QuietStretchPool.cs ModuleData/Enlisted/events/quiet_stretch_pool.json Enlisted.csproj
git commit -m "feat: add quiet-stretch fallback pool loader and authoring stub"
```

---

### Task 8: Add StoryDirector skeleton

**Files:**
- Create: `src/Features/Content/StoryDirector.cs`
- Modify: `Enlisted.csproj`
- Modify: `src/Mod/SubModule.cs` (or the behavior registration site — grep for `AddBehavior` to find it)

- [ ] **Step 1: Create `StoryDirector.cs` with emit-only API and state bags**

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment;
using Enlisted.Platform;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.Content
{
    public sealed class StoryDirector : CampaignBehaviorBase
    {
        public static StoryDirector Instance { get; private set; }

        [SaveableField(1)] private int _lastModalDay;
        [SaveableField(2)] private long _lastModalUtcTicks;
        [SaveableField(3)] private Dictionary<string, int> _categoryCooldowns = new Dictionary<string, int>();
        [SaveableField(4)] private List<StoryCandidatePersistent> _pertinentBuffer = new List<StoryCandidatePersistent>();
        [SaveableField(5)] private List<StoryCandidatePersistent> _logRing = new List<StoryCandidatePersistent>();

        public IReadOnlyList<StoryCandidatePersistent> HeadlineAccordionState => _headlineBuffer;
        public IReadOnlyList<StoryCandidatePersistent> PertinentDigestState => _pertinentBuffer;
        public IReadOnlyList<StoryCandidatePersistent> LogRing => _logRing;

        private readonly List<StoryCandidatePersistent> _headlineBuffer = new List<StoryCandidatePersistent>();
        private readonly Dictionary<string, IStorySource> _sources = new Dictionary<string, IStorySource>();

        private const int LogRingCapacity = 200;

        public override void RegisterEvents()
        {
            Instance = this;
            QuietStretchPool.Load();
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_lastModalDay", ref _lastModalDay);
            dataStore.SyncData("_lastModalUtcTicks", ref _lastModalUtcTicks);
            dataStore.SyncData("_categoryCooldowns", ref _categoryCooldowns);
            dataStore.SyncData("_pertinentBuffer", ref _pertinentBuffer);
            dataStore.SyncData("_logRing", ref _logRing);
        }

        public void RegisterSource(IStorySource source)
        {
            if (source == null || string.IsNullOrEmpty(source.SourceId))
            {
                return;
            }
            _sources[source.SourceId] = source;
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

                bool visited = candidate.Relevance.SubjectSettlement != null
                    && VisitedSettlementTracker.HasVisited(candidate.Relevance.SubjectSettlement);
                var finalTier = SeverityClassifier.Classify(
                    candidate,
                    candidate.Relevance.TouchesEnlistedLord,
                    candidate.Relevance.TouchesPlayerKingdom,
                    visited);

                Route(candidate, finalTier);
            }
            catch (Exception ex)
            {
                ModLogger.Log("E-PACE-001", $"StoryDirector.EmitCandidate failed: {ex.Message}");
            }
        }

        private void Route(StoryCandidate c, StoryTier tier)
        {
            // Phase 1 skeleton: persist to the correct buffer, no actual rendering yet.
            var persistent = new StoryCandidatePersistent
            {
                SourceId = c.SourceId,
                CategoryId = c.CategoryId,
                GrantedTier = tier,
                Severity = c.SeverityHint,
                EmittedDayNumber = (int)CampaignTime.Now.ToDays,
                EmittedHour = CampaignTime.Now.GetHourOfDay,
                PayloadKey = c.PayloadKey,
                RenderedTitle = c.RenderedTitle,
                RenderedBody = c.RenderedBody
            };

            switch (tier)
            {
                case StoryTier.Modal:
                    // Populated in Task 15 (modal firing) — for now, drop to headline.
                    _headlineBuffer.Add(persistent);
                    break;
                case StoryTier.Headline:
                    _headlineBuffer.Add(persistent);
                    break;
                case StoryTier.Pertinent:
                    _pertinentBuffer.Add(persistent);
                    break;
                case StoryTier.Log:
                    _logRing.Add(persistent);
                    while (_logRing.Count > LogRingCapacity)
                    {
                        _logRing.RemoveAt(0);
                    }
                    break;
            }
        }

        public bool TryReconstructPayload(StoryCandidatePersistent p, out Action<StoryTier> payload)
        {
            payload = null;
            if (p == null || string.IsNullOrEmpty(p.SourceId))
            {
                return false;
            }
            if (!_sources.TryGetValue(p.SourceId, out var source))
            {
                return false;
            }
            payload = source.RehydratePayload(p.PayloadKey);
            return payload != null;
        }
    }
}
```

- [ ] **Step 2: Register the behavior**

Grep for `AddBehavior` to find where other campaign behaviors are registered (likely `src/Mod/SubModule.cs` or a `CampaignGameStarter`-using behavior-adder). Add:

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
git add src/Features/Content/StoryDirector.cs src/Mod/SubModule.cs Enlisted.csproj
git commit -m "feat: add StoryDirector skeleton with emit/route/save"
```

---

### Task 9: Register save types in EnlistedSaveDefiner

**Files:**
- Modify: `src/Features/Enlistment/EnlistedSaveDefiner.cs` (grep for file — exact path may differ)

- [ ] **Step 1: Add pacing types to the existing definer**

Find the existing `DefineEnumType` / `DefineClassType` calls in `EnlistedSaveDefiner` and add in a pacing subsystem section with fresh ids in the 735700–735799 range:

```csharp
// Pacing subsystem (base ids 735700–735799)
AddEnumDefinition(typeof(Enlisted.Features.Content.StoryTier), 735701);
AddEnumDefinition(typeof(Enlisted.Features.Content.StoryBeat), 735702);
AddClassDefinition(typeof(Enlisted.Features.Content.StoryCandidatePersistent), 735703);
AddGenericClassDefinition(typeof(System.Collections.Generic.List<Enlisted.Features.Content.StoryCandidatePersistent>), 735704);
AddGenericClassDefinition(typeof(System.Collections.Generic.Dictionary<string, int>), 735705);
```

Adjust call names (`AddEnumDefinition` vs `DefineEnumType` etc.) to match the existing file — read it first before editing.

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Enlistment/EnlistedSaveDefiner.cs
git commit -m "feat: register StoryDirector save types in EnlistedSaveDefiner"
```

---

### Task 10: Smoke-test Phase 1 — save and load

**Files:** none (integration check)

- [ ] **Step 1: Launch Bannerlord with the build**

Use the existing launch workflow (Tools/Steam or manual).

- [ ] **Step 2: Load an existing Enlisted save**

Confirm no "Cannot Create Save" errors. Confirm `ModLogger` log has no `E-PACE-001` entries.

- [ ] **Step 3: Save the game**

Close and reload — verify no save errors and `StoryDirector.Instance != null` (add a temporary `ModLogger.Log` at end of `RegisterEvents` to print `"StoryDirector online"` if needed; remove before commit).

- [ ] **Step 4: Commit (only if any fixes were needed)**

End of Phase 1. Director is live but no sources emit yet and no renderer reads from its state — behavior is unchanged from today.

---

## Phase 2 — Menu rendering

Ship plan: menu now reads from Director's state bags; still no source emits, so accordion sections remain empty. Safe to merge.

### Task 11: Add Headline accordion section to enlisted_status

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`

- [ ] **Step 1: Add the accordion section**

Locate the `enlisted_status` menu construction (around line 931 per existing docs — grep for `enlisted_status` menu ID). Add **above** the orders accordion section:

```csharp
// Headlines (from StoryDirector)
campaignGameStarter.AddGameMenuOption(
    "enlisted_status",
    "enlisted_status_headlines",
    "{=enl_headlines}Headlines {NEW_TAG}",
    args =>
    {
        var director = Enlisted.Features.Content.StoryDirector.Instance;
        int count = director?.HeadlineAccordionState.Count ?? 0;
        if (count == 0)
        {
            return false;
        }
        MBTextManager.SetTextVariable("NEW_TAG", "[NEW]");
        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        return true;
    },
    args => GameMenu.SwitchToMenu("enlisted_headlines"),
    false, -1, false);
```

- [ ] **Step 2: Create the `enlisted_headlines` submenu**

In the same file's menu registration block:

```csharp
campaignGameStarter.AddGameMenu(
    "enlisted_headlines",
    "{=enl_headlines_body}{HEADLINES_TEXT}",
    args =>
    {
        var director = Enlisted.Features.Content.StoryDirector.Instance;
        var sb = new System.Text.StringBuilder();
        foreach (var item in director?.HeadlineAccordionState ?? System.Array.Empty<Enlisted.Features.Content.StoryCandidatePersistent>())
        {
            sb.AppendLine($"• {item.RenderedTitle}");
            if (!string.IsNullOrEmpty(item.RenderedBody))
            {
                sb.AppendLine($"  {item.RenderedBody}");
            }
        }
        MBTextManager.SetTextVariable("HEADLINES_TEXT", sb.ToString());
    });

campaignGameStarter.AddGameMenuOption(
    "enlisted_headlines",
    "enlisted_headlines_back",
    "{=enl_back}Back",
    args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
    args => GameMenu.SwitchToMenu("enlisted_status"),
    true, -1, false);
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs
git commit -m "feat: render Director headlines as enlisted_status accordion"
```

---

### Task 12: Add News log section to enlisted_status

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`

- [ ] **Step 1: Add "News" log-viewer submenu**

Add below the headlines section:

```csharp
campaignGameStarter.AddGameMenuOption(
    "enlisted_status",
    "enlisted_status_news",
    "{=enl_news}News",
    args =>
    {
        var director = Enlisted.Features.Content.StoryDirector.Instance;
        if ((director?.LogRing.Count ?? 0) == 0)
        {
            return false;
        }
        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        return true;
    },
    args => GameMenu.SwitchToMenu("enlisted_news"),
    false, -1, false);

campaignGameStarter.AddGameMenu(
    "enlisted_news",
    "{=enl_news_body}{NEWS_TEXT}",
    args =>
    {
        var director = Enlisted.Features.Content.StoryDirector.Instance;
        var sb = new System.Text.StringBuilder();
        var ring = director?.LogRing;
        if (ring != null)
        {
            for (int i = ring.Count - 1; i >= 0; i--)
            {
                sb.AppendLine($"Day {ring[i].EmittedDayNumber}: {ring[i].RenderedTitle}");
            }
        }
        MBTextManager.SetTextVariable("NEWS_TEXT", sb.ToString());
    });

campaignGameStarter.AddGameMenuOption(
    "enlisted_news",
    "enlisted_news_back",
    "{=enl_back}Back",
    args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
    args => GameMenu.SwitchToMenu("enlisted_status"),
    true, -1, false);
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs
git commit -m "feat: render Director log ring as enlisted_status News accordion"
```

---

### Task 13: Wire Pertinent digest into muster-menu intro

**Files:**
- Modify: `src/Features/Camp/MusterMenuHandler.cs` (grep for `muster_intro` menu id)

- [ ] **Step 1: Append digest section to muster intro text**

In the `muster_intro` menu's `args =>` init lambda, append to the existing text variable assembly:

```csharp
var director = Enlisted.Features.Content.StoryDirector.Instance;
if (director != null && director.PertinentDigestState.Count > 0)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine();
    sb.AppendLine("Since your last muster:");
    foreach (var item in director.PertinentDigestState)
    {
        sb.AppendLine($"• {item.RenderedTitle}");
    }
    // Append sb.ToString() to the existing muster intro body variable.
}
```

The exact variable name depends on the existing code — read the file, find the intro body text-variable set call, and append the digest string there.

- [ ] **Step 2: Clear the buffer on muster close**

In the `muster_complete` menu's close handler (or wherever the muster cycle ends), call:

```csharp
Enlisted.Features.Content.StoryDirector.Instance?.ClearPertinentBuffer();
```

Add the clear method to `StoryDirector.cs`:

```csharp
public void ClearPertinentBuffer()
{
    _pertinentBuffer.Clear();
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Camp/MusterMenuHandler.cs src/Features/Content/StoryDirector.cs
git commit -m "feat: fold Director pertinent digest into muster intro"
```

---

## Phase 3 — Modal path + time discipline

Director now has live renderers for Headline/Pertinent/Log. Next, wire up the Modal path with its guards.

### Task 14: Add Modal delivery and time guards

**Files:**
- Modify: `src/Features/Content/StoryDirector.cs`
- Modify: `src/Features/Content/EventDeliveryManager.cs`

- [ ] **Step 1: Add density config reader**

Create `src/Features/Content/DensitySettings.cs`:

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

Register in csproj.

- [ ] **Step 2: Replace the skeleton `Route` method in `StoryDirector.cs`**

Replace the current `case StoryTier.Modal:` branch body with full guard logic:

```csharp
case StoryTier.Modal:
{
    int today = (int)CampaignTime.Now.ToDays;
    double wallSeconds = (System.DateTime.UtcNow.Ticks - _lastModalUtcTicks)
        / (double)System.TimeSpan.TicksPerSecond;

    bool inGameFloor = today - _lastModalDay >= DensitySettings.ModalFloorInGameDays;
    bool wallClockFloor = _lastModalUtcTicks == 0 || wallSeconds >= DensitySettings.ModalFloorWallClockSeconds;
    bool categoryOk = !_categoryCooldowns.TryGetValue(c.CategoryId ?? "_none", out var lastDay)
        || (today - lastDay) >= DensitySettings.CategoryCooldownDays;

    if (!inGameFloor || !wallClockFloor || !categoryOk)
    {
        _headlineBuffer.Add(persistent);
        break;
    }

    if (DensitySettings.SpeedDownshiftOnModal
        && Campaign.Current.TimeControlMode == CampaignTimeControlMode.StoppableFastForward)
    {
        Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
    }

    if (c.Payload != null)
    {
        c.Payload(StoryTier.Modal);
    }
    else
    {
        EventDeliveryManager.Instance?.ShowFallback(c.RenderedTitle, c.RenderedBody);
    }

    _lastModalDay = today;
    _lastModalUtcTicks = System.DateTime.UtcNow.Ticks;
    _categoryCooldowns[c.CategoryId ?? "_none"] = today;
    break;
}
```

- [ ] **Step 3: Add `ShowFallback` to `EventDeliveryManager.cs`**

```csharp
public void ShowFallback(string title, string body)
{
    var inquiry = new InquiryData(
        title ?? "",
        body ?? "",
        true, false,
        new TaleWorlds.Localization.TextObject("{=close}Close").ToString(),
        null,
        () => { }, null);
    InformationManager.ShowInquiry(inquiry, true);
}
```

- [ ] **Step 4: Load density config on submodule init**

In `SubModule.OnGameStart` (or equivalent), add:
```csharp
Enlisted.Features.Content.DensitySettings.Load();
```

- [ ] **Step 5: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/StoryDirector.cs src/Features/Content/EventDeliveryManager.cs src/Features/Content/DensitySettings.cs src/Mod/SubModule.cs Enlisted.csproj
git commit -m "feat: modal delivery with in-game + wall-clock + category guards"
```

---

### Task 15: Add density entry to enlisted_config.json

**Files:**
- Modify: `ModuleData/Enlisted/Config/enlisted_config.json`

- [ ] **Step 1: Add keys if not present**

Add at the top level:

```json
"event_density": "normal",
"speed_downshift_on_modal": true
```

- [ ] **Step 2: Validate, build, commit**

```bash
python Tools/Validation/validate_content.py
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add ModuleData/Enlisted/Config/enlisted_config.json
git commit -m "build: add event_density and speed_downshift config keys"
```

---

### Task 16: Quiet-stretch fallback tick

**Files:**
- Modify: `src/Features/Content/StoryDirector.cs`

- [ ] **Step 1: Subscribe to DailyTickEvent and fire fallback when stretch exceeds threshold**

In `RegisterEvents`:

```csharp
CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
```

Add the method:

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

        var entry = QuietStretchPool.PickOne();
        if (entry == null)
        {
            return;
        }

        var title = entry["title"]?.ToString() ?? "A Quiet Moment";
        var body = entry["setup"]?.ToString() ?? "";

        var candidate = new StoryCandidate
        {
            SourceId = "director.quiet_stretch",
            CategoryId = "quiet_stretch",
            ProposedTier = StoryTier.Modal,
            SeverityHint = 0.70f,
            Beats = { StoryBeat.QuietStretchTimeout },
            Relevance = new RelevanceKey { TouchesEnlistedLord = true },
            EmittedAt = CampaignTime.Now,
            PayloadKey = entry["id"]?.ToString(),
            RenderedTitle = title,
            RenderedBody = body
        };

        EmitCandidate(candidate);
    }
    catch (System.Exception ex)
    {
        ModLogger.Log("E-PACE-002", $"Quiet-stretch tick failed: {ex.Message}");
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/StoryDirector.cs
git commit -m "feat: quiet-stretch fallback fires Modal after 14 silent days"
```

---

## Phase 4 — Source migrations

One source per task. Each migration keeps the old direct-queue path behind a feature flag for one commit, then removes the flag in the next. Each task independently ships.

### Task 17: Migrate MapIncidentManager — add tier field to incident JSON

**Files:**
- Modify: All `ModuleData/Enlisted/events/incidents_*.json` files
- Modify: `src/Features/Content/MapIncidentManager.cs`
- Modify: `Tools/Validation/validate_events.py`

- [ ] **Step 1: Define default tier per incident context**

Edit each `incidents_*.json` — add a `"tier"` field to each incident entry. Defaults:
- `incidents_leaving_battle.json` → `"tier": "headline"`
- `incidents_entering_town.json` → `"tier": "pertinent"`
- `incidents_entering_village.json` → `"tier": "pertinent"`
- `incidents_leaving_settlement.json` → `"tier": "pertinent"`
- `incidents_siege.json` → `"tier": "headline"`
- `incidents_waiting.json` → `"tier": "log"`

For a handful of "narrative fork" incidents (manual author review — any with meaningful `options` that change character arc), set `"tier": "modal"`.

- [ ] **Step 2: Update `validate_events.py` to accept the new field**

Grep for the incident schema and add `"tier"` to the allowed keys list.

- [ ] **Step 3: Migrate `MapIncidentManager.TryDeliverIncident`**

Replace the direct `EventDeliveryManager.Instance.QueueEvent(...)` call with a candidate emit. Add near the top:

```csharp
using Enlisted.Features.Content;
```

Replace the delivery call:

```csharp
var tierStr = incidentJson["tier"]?.ToString() ?? "pertinent";
var tier = tierStr switch
{
    "modal" => StoryTier.Modal,
    "headline" => StoryTier.Headline,
    "pertinent" => StoryTier.Pertinent,
    _ => StoryTier.Log
};

StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "map_incident",
    CategoryId = $"incident.{context}",
    ProposedTier = tier,
    SeverityHint = tier == StoryTier.Modal ? 0.8f : 0.3f,
    Beats = { ResolveBeat(context) },
    Relevance = new RelevanceKey
    {
        SubjectSettlement = Settlement.CurrentSettlement,
        TouchesEnlistedLord = true
    },
    EmittedAt = CampaignTime.Now,
    PayloadKey = incidentJson["id"]?.ToString(),
    RenderedTitle = incidentJson["title"]?.ToString() ?? "",
    RenderedBody = incidentJson["setup"]?.ToString() ?? "",
    Payload = grantedTier =>
    {
        if (grantedTier == StoryTier.Modal)
        {
            EventDeliveryManager.Instance?.QueueEvent(incidentJson);
        }
    }
});
```

Add helper:

```csharp
private static StoryBeat ResolveBeat(string context) => context switch
{
    "leaving_battle" => StoryBeat.PlayerBattleEnd,
    "entering_town" => StoryBeat.SettlementEntered,
    "entering_village" => StoryBeat.SettlementEntered,
    "leaving_settlement" => StoryBeat.SettlementLeft,
    "during_siege" => StoryBeat.SiegeBegin,
    "waiting_in_settlement" => StoryBeat.SettlementEntered,
    _ => StoryBeat.OrderPhaseTransition
};
```

- [ ] **Step 4: Implement IStorySource on MapIncidentManager**

```csharp
public string SourceId => "map_incident";

public Action<StoryTier> RehydratePayload(string payloadKey)
{
    // Find incident JSON by id, return payload closure — or null if not found.
    var incident = LoadIncidentById(payloadKey);
    if (incident == null) return null;
    return grantedTier =>
    {
        if (grantedTier == StoryTier.Modal)
        {
            EventDeliveryManager.Instance?.QueueEvent(incident);
        }
    };
}
```

Register with Director in `OnGameStart`:
```csharp
StoryDirector.Instance?.RegisterSource(this);
```

- [ ] **Step 5: Validate, build, commit**

```bash
python Tools/Validation/validate_content.py
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add ModuleData/Enlisted/events/ src/Features/Content/MapIncidentManager.cs Tools/Validation/validate_events.py
git commit -m "refactor: MapIncidentManager emits StoryCandidates instead of direct queue"
```

---

### Task 18: Migrate EnlistedNewsBehavior — emit candidates for captured world events

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs`

- [ ] **Step 1: Convert `AddCampNews` and event handlers to emit**

For each `CampaignEvents.*.AddNonSerializedListener` handler in this file, instead of appending directly to `_personalFeed`, emit a candidate:

```csharp
private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
{
    try
    {
        bool touchesLord = victim == EnlistmentBehavior.Instance?.EnlistedLord;
        var candidate = new StoryCandidate
        {
            SourceId = "news.hero_killed",
            CategoryId = "hero_death",
            ProposedTier = touchesLord ? StoryTier.Modal : StoryTier.Headline,
            SeverityHint = touchesLord ? 0.9f : 0.2f,
            Beats = { touchesLord ? StoryBeat.LordKilled : StoryBeat.PlayerBattleEnd },
            Relevance = new RelevanceKey
            {
                SubjectHero = victim,
                ObjectHero = killer,
                TouchesEnlistedLord = touchesLord
            },
            EmittedAt = CampaignTime.Now,
            RenderedTitle = $"{victim?.Name} has died.",
            RenderedBody = detail == KillCharacterAction.KillCharacterActionDetail.DiedInBattle
                ? "Word reaches you from the field."
                : "Word reaches your camp."
        };
        StoryDirector.Instance?.EmitCandidate(candidate);
    }
    catch (Exception ex)
    {
        ModLogger.Log("E-PACE-003", $"OnHeroKilled emit failed: {ex.Message}");
    }
}
```

Repeat the pattern for: `OnHeroPrisonerTaken`, `OnHeroPrisonerReleased`, `OnSiegeEventStarted`, `OnSettlementOwnerChanged`, `WarDeclared`, `MakePeace`, `OnArmyCreated`, `OnArmyDispersed`.

For each, set:
- `SourceId` — unique per handler
- `CategoryId` — broad bucket (e.g., "hero_death", "siege", "settlement_control")
- `ProposedTier` — Headline for observed-world events; Modal only if it touches the enlisted lord directly
- `Beats` — the matching beat from `StoryBeat`
- `Relevance` — populate the appropriate fields

- [ ] **Step 2: Remove direct `_personalFeed.Add` calls from these handlers**

The Director's Log-tier routing will write into the equivalent buffer. For backward compatibility, keep `_personalFeed` readable (it's referenced elsewhere in the mod for muster recap and QM dialog quoting) — add a projection method:

```csharp
public IEnumerable<string> GetFeedStrings()
{
    var director = Enlisted.Features.Content.StoryDirector.Instance;
    if (director == null) yield break;
    foreach (var entry in director.LogRing)
    {
        yield return $"Day {entry.EmittedDayNumber}: {entry.RenderedTitle}";
    }
}
```

Update any existing `_personalFeed` readers to call `GetFeedStrings()`.

- [ ] **Step 3: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs
git commit -m "refactor: EnlistedNewsBehavior emits StoryCandidates for world events"
```

---

### Task 19: Migrate EscalationManager — emit on threshold cross

**Files:**
- Modify: `src/Features/Escalation/EscalationManager.cs`

- [ ] **Step 1: Replace `QueueEvent` calls with candidate emits**

Locate threshold-cross points (scrutiny, medical risk, lord reputation). For each:

```csharp
StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "escalation.scrutiny_crisis",
    CategoryId = "escalation",
    ProposedTier = StoryTier.Modal,
    SeverityHint = 0.85f,
    Beats = { StoryBeat.EscalationThreshold },
    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
    EmittedAt = CampaignTime.Now,
    PayloadKey = thresholdEventId,
    RenderedTitle = thresholdEventTitle,
    RenderedBody = thresholdEventSetup,
    Payload = tier =>
    {
        if (tier == StoryTier.Modal)
        {
            EventDeliveryManager.Instance?.QueueEvent(thresholdEventJson);
        }
    }
});
```

Implement `IStorySource` and register on game start.

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Escalation/EscalationManager.cs
git commit -m "refactor: EscalationManager emits StoryCandidates on thresholds"
```

---

### Task 20: Migrate CompanySimulationBehavior — crisis and pressure emits

**Files:**
- Modify: `src/Features/Camp/CompanySimulationBehavior.cs`

- [ ] **Step 1: Replace crisis queuing with emits**

At each crisis / pressure-stage trigger point:

```csharp
StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "company_sim.supply_crisis",
    CategoryId = "company_crisis",
    ProposedTier = StoryTier.Modal,
    SeverityHint = 0.80f,
    Beats = { StoryBeat.CompanyCrisisThreshold },
    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
    EmittedAt = CampaignTime.Now,
    PayloadKey = crisisEventId,
    RenderedTitle = crisisEventTitle,
    RenderedBody = crisisEventSetup,
    Payload = tier =>
    {
        if (tier == StoryTier.Modal)
        {
            EventDeliveryManager.Instance?.QueueEvent(crisisEventJson);
        }
    }
});
```

For pressure stages 1–2, use:
```csharp
ProposedTier = StoryTier.Pertinent,
SeverityHint = 0.25f,
Beats = { StoryBeat.CompanyPressureThreshold },
```

- [ ] **Step 2: Remove the direct `EventDeliveryManager.QueueEvent` calls in this file**

- [ ] **Step 3: Implement IStorySource, register, build, commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Camp/CompanySimulationBehavior.cs
git commit -m "refactor: CompanySimulationBehavior emits StoryCandidates for crises"
```

---

### Task 21: Migrate OrderProgressionBehavior — remove random rolls

**Files:**
- Modify: `src/Features/Orders/Behaviors/OrderProgressionBehavior.cs`

- [ ] **Step 1: Delete the random-roll triggers**

Find `TryGetOrderEvent` and the 8–15% probability rolls in `ProcessPhase`. **Delete** those probability branches entirely — no longer clock-gated.

- [ ] **Step 2: Emit one Log-tier candidate per phase transition**

Replace the deleted block with:

```csharp
StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "order_progression.phase",
    CategoryId = $"order.{phaseId}",
    ProposedTier = StoryTier.Log,
    SeverityHint = 0.05f,
    Beats = { StoryBeat.OrderPhaseTransition },
    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
    EmittedAt = CampaignTime.Now,
    RenderedTitle = $"Phase complete: {phaseName}",
    RenderedBody = phaseRecapText
});
```

- [ ] **Step 3: At order completion, emit Modal-eligible if narrative fork**

Where order-complete results render a branching choice today:

```csharp
StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "order_progression.complete",
    CategoryId = "order_complete",
    ProposedTier = StoryTier.Modal,
    SeverityHint = 0.55f,
    Beats = { StoryBeat.OrderComplete },
    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
    EmittedAt = CampaignTime.Now,
    PayloadKey = orderCompleteEventId,
    RenderedTitle = orderCompleteTitle,
    RenderedBody = orderCompleteSetup,
    Payload = tier =>
    {
        if (tier == StoryTier.Modal)
        {
            EventDeliveryManager.Instance?.QueueEvent(orderCompleteJson);
        }
    }
});
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Orders/Behaviors/OrderProgressionBehavior.cs
git commit -m "refactor: OrderProgression removes random rolls, emits on beats only"
```

---

### Task 22: Migrate CampRoutineProcessor — outcome emits

**Files:**
- Modify: `src/Features/Camp/CampRoutineProcessor.cs`

- [ ] **Step 1: Replace `AddToNewsFeed` with candidate emits**

Per outcome:
- Excellent / Mishap → `ProposedTier = StoryTier.Pertinent`, `SeverityHint = 0.2f`
- Good / Normal / Poor → `ProposedTier = StoryTier.Log`, `SeverityHint = 0.05f`

```csharp
StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "camp_routine.outcome",
    CategoryId = $"camp_routine.{activityId}",
    ProposedTier = tier,
    SeverityHint = severity,
    Beats = { StoryBeat.OrderPhaseTransition },
    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
    EmittedAt = CampaignTime.Now,
    RenderedTitle = $"{activityName}: {outcome}",
    RenderedBody = flavorText
});
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Camp/CampRoutineProcessor.cs
git commit -m "refactor: CampRoutineProcessor emits StoryCandidates per outcome"
```

---

### Task 23: Neutralize ContentOrchestrator direct queuing

**Files:**
- Modify: `src/Features/Content/ContentOrchestrator.cs`

- [ ] **Step 1: Delete the `EventDeliveryManager.QueueEvent` calls**

ContentOrchestrator is now read-only for world-state / activity-level. Remove any code path that queues events directly.

- [ ] **Step 2: Expose `CurrentActivityLevel` and `CurrentWorldState` as public read-only properties**

(If they aren't already — grep to confirm.)

- [ ] **Step 3: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/ContentOrchestrator.cs
git commit -m "refactor: ContentOrchestrator becomes read-only world-state provider"
```

---

## Phase 5 — Cleanup and verification

### Task 24: Make EventDeliveryManager.QueueEvent internal

**Files:**
- Modify: `src/Features/Content/EventDeliveryManager.cs`

- [ ] **Step 1: Change access modifier**

```csharp
internal void QueueEvent(JObject eventJson) { ... }
```

Build — this catches any caller you missed in migration.

- [ ] **Step 2: Fix any stragglers**

Any compile errors pointing to external callers = sources not fully migrated. Route them through `StoryDirector.Instance?.EmitCandidate(...)` instead.

- [ ] **Step 3: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Content/EventDeliveryManager.cs
git commit -m "refactor: make EventDeliveryManager.QueueEvent internal"
```

---

### Task 25: Add debug/dev menu for Director inspection

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` (existing debug tools section at line 1153 per spec)

- [ ] **Step 1: Add three debug menu options**

Inside the existing debug-tools-in-production-hidden block:

```csharp
campaignGameStarter.AddGameMenuOption(
    "enlisted_status", "dbg_emit_headline", "[dbg] Emit synthetic headline",
    args => { args.optionLeaveType = GameMenuOption.LeaveType.Wait; return true; },
    args => Enlisted.Features.Content.StoryDirector.Instance?.EmitCandidate(new Enlisted.Features.Content.StoryCandidate
    {
        SourceId = "debug.synthetic",
        CategoryId = "debug",
        ProposedTier = Enlisted.Features.Content.StoryTier.Headline,
        SeverityHint = 0.4f,
        Beats = { Enlisted.Features.Content.StoryBeat.WarDeclared },
        Relevance = new Enlisted.Features.Content.RelevanceKey { TouchesEnlistedLord = true },
        EmittedAt = CampaignTime.Now,
        RenderedTitle = "Synthetic headline (debug)",
        RenderedBody = "Emit-path sanity check."
    }),
    true, -1, false);

campaignGameStarter.AddGameMenuOption(
    "enlisted_status", "dbg_emit_modal", "[dbg] Emit synthetic modal",
    args => { args.optionLeaveType = GameMenuOption.LeaveType.Wait; return true; },
    args => Enlisted.Features.Content.StoryDirector.Instance?.EmitCandidate(new Enlisted.Features.Content.StoryCandidate
    {
        SourceId = "debug.synthetic",
        CategoryId = "debug_modal",
        ProposedTier = Enlisted.Features.Content.StoryTier.Modal,
        SeverityHint = 0.9f,
        Beats = { Enlisted.Features.Content.StoryBeat.LordCaptured },
        Relevance = new Enlisted.Features.Content.RelevanceKey { TouchesEnlistedLord = true },
        EmittedAt = CampaignTime.Now,
        RenderedTitle = "Synthetic modal (debug)",
        RenderedBody = "Modal-path sanity check."
    }),
    true, -1, false);

campaignGameStarter.AddGameMenuOption(
    "enlisted_status", "dbg_clear_state", "[dbg] Clear Director state",
    args => { args.optionLeaveType = GameMenuOption.LeaveType.Wait; return true; },
    args => {
        // Add matching Clear method on StoryDirector.
    },
    true, -1, false);
```

Add a `ClearAllState()` helper to `StoryDirector.cs` for the third option.

- [ ] **Step 2: Build + commit**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
git add src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs src/Features/Content/StoryDirector.cs
git commit -m "feat: debug menu options for Director emit/clear inspection"
```

---

### Task 26: Integration smoke-test battery

**Files:** none (manual verification — log results in a short checklist comment on the PR)

- [ ] **Scenario 1: Quiet war week**
  - Load a save, enlist with a lord at peace
  - Wait 14 in-game days via wait menu
  - Expect: at least one Modal fallback from quiet-stretch pool

- [ ] **Scenario 2: Active battle week**
  - Load save where lord is at war and actively fighting
  - Observe that modals do not fire more than once per ~5 days regardless of world-event volume
  - Other world events land as Headlines in `enlisted_status`

- [ ] **Scenario 3: FastForward compression**
  - Engage FastForward for ~2 minutes wall-clock with active war
  - Expect: only one Modal within any 60s wall-clock window; others demoted to Headline

- [ ] **Scenario 4: Muster digest**
  - Let Pertinent items accumulate over 10 in-game days (enter/exit settlements)
  - Trigger muster
  - Expect: intro shows aggregated "Since your last muster" list

- [ ] **Scenario 5: Save/load during digest window**
  - Mid-digest, save and reload
  - Expect: no save errors; Pertinent buffer restored; digest still releases on next muster

- [ ] **Scenario 6: Irrelevant world events dropped**
  - Observe a distant-kingdom war between two factions neither the player nor the lord belongs to
  - Expect: no accordion or log entry generated

Document results in a follow-up commit to the spec or a new file `docs/superpowers/plans/2026-04-18-event-pacing-verification.md`.

- [ ] **Step 1: Commit results**

```bash
git add docs/superpowers/plans/2026-04-18-event-pacing-verification.md
git commit -m "docs: record event-pacing smoke test results"
```

---

### Task 27: Update INDEX.md and BLUEPRINT.md pointers

**Files:**
- Modify: `docs/INDEX.md`
- Modify: `docs/BLUEPRINT.md`

- [ ] **Step 1: Link to the spec and plan**

Add in the appropriate sections of INDEX.md:

```markdown
| Event pacing (StoryDirector) | [docs/superpowers/specs/2026-04-18-event-pacing-design.md](superpowers/specs/2026-04-18-event-pacing-design.md) |
```

In BLUEPRINT.md, update any sections describing content delivery to reference `StoryDirector` as the single broker.

- [ ] **Step 2: Commit**

```bash
git add docs/INDEX.md docs/BLUEPRINT.md
git commit -m "docs: link event-pacing spec from INDEX and BLUEPRINT"
```

---

## Out-of-scope (explicitly not in this plan)

- New content authoring (events, decisions, order events) — quiet-stretch pool has 2 stub entries only; content expansion is a separate initiative
- MCM (Mod Configuration Menu) UI for the density slider — JSON-config-only for v1
- Refactoring `EnlistmentBehavior.cs` internals beyond the single `VisitedSettlementTracker.Record` line
- Changes to quartermaster, muster flow, or retirement system except where explicitly listed
- Removal of deprecated morale / rest systems — separate cleanup
- Localization strings for new menu text — use fallback text; localization pass is a separate task

---

## Self-review checklist (author-run)

**Spec coverage:**
- §4 Principles — covered by Tasks 8, 14, 16 (Director + modal guards + fallback)
- §5 Architecture — Tasks 1–8 establish the Director + candidate schema
- §6 Beat taxonomy — encoded in `SeverityClassifier` (Task 5) and source migrations (Tasks 17–22)
- §7 Relevance filter — Task 4
- §8 Severity classifier — Task 5
- §9 Aggregation — Task 6 (aggregator); consumption in Task 13 (muster digest)
- §10 Surfacing rules — Tasks 11 (Headline), 12 (Log), 13 (Pertinent digest), 14 (Modal)
- §11 Time discipline — Tasks 14 (wall-clock + speed downshift), 15 (density config)
- §12 Migration per source — Tasks 17–23 cover all 10 affected files
- §13 Save surface — Tasks 3, 8, 9
- §14 Testing — Tasks 25 (debug menu) + 26 (smoke scenarios)
- §15 Risks — mitigated in-flight: try/catch in EmitCandidate (Task 8), fallback render in ShowFallback (Task 14), quiet-stretch pool (Tasks 7, 16)

**Placeholder scan:**
- Verified: every task has actual code blocks, no "TODO", no "similar to task N"
- Every new C# file has full content shown once

**Type consistency:**
- `StoryTier` enum order: Log < Pertinent < Headline < Modal — used consistently in classifier, Router switch, menu predicates
- `StoryCandidate` property names stable across tasks — verified
- `StoryDirector.Instance` singleton pattern consistent across all emit sites

**Missing / deferred:**
- No unit-test project exists; debug menu (Task 25) is the best-available inline inspection
- Density slider is config-file-only for v1 (§15 risk mitigation); MCM is explicit out-of-scope

---

## Completion criteria

- All 27 tasks committed
- `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds
- `python Tools/Validation/validate_content.py` passes
- All six smoke-test scenarios pass and are documented
- No `E-PACE-*` errors in `ModLogger` during a 30-minute play session
- `EventDeliveryManager.QueueEvent` is `internal` and only called by Director-routed payloads
