# Agency News Status Integration Implementation Plan

> **Status:** Active branch-scoped plan. Current `development` has not merged the typed agency/news routing implementation; the work exists on `feature/agency-news-status-integration`, where the task checklist has drifted behind branch commits. Before merging or resuming, refresh this plan against the branch history and current `development`; do not archive it yet.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add typed agency/news routing so service stance, order, camp override, modal, and realm story content reaches the correct Enlisted news and status surfaces without string-guessing.

**Architecture:** This plan implements the integration slice of the player-agency redesign, not the full agency gate/state-mutation system. It adds typed dispatch metadata, carries it through `StoryCandidate`, `StoryDirector`, `DispatchItem`, and `EnlistedNewsBehavior`, then teaches `EnlistedMenuBehavior` and storylet validation to consume it. The broader `StateMutator`, service stance runtime, and camp activity override runtime should be planned separately after this routing substrate lands.

**Tech Stack:** C# targeting Bannerlord/.NET Framework 4.7.2, Newtonsoft.Json storylet loading, xUnit net9.0 pure unit tests, Python content validator, PowerShell validation/build commands.

---

## Scope Boundaries

This plan includes:

- Typed dispatch metadata: `DispatchDomain`, `DispatchSourceKind`, `DispatchSurfaceHint`.
- Propagation through `StoryCandidate`, deferred candidates, news dispatches, persistence, and personal dispatch APIs.
- Status/camp menu filtering and layout ownership for `DISPATCHES`, `UPCOMING`, `YOU`, `SINCE LAST MUSTER`, and `CAMP ACTIVITIES`.
- Storylet `agency` metadata loading and validation.
- A lightweight stance-summary producer API that writes typed personal dispatches.

This plan excludes:

- `StateMutator`, `AgencyGate`, and magnitude-band enforcement.
- Full `ServiceStanceManager` implementation.
- Full short activity override runtime.
- Rebalancing all storylet rewards.

Those excluded items depend on this routing layer but are not required to make this layer testable.

---

## File Structure

| Path | Responsibility |
| :--- | :--- |
| `src/Features/Interface/Models/DispatchRouting.cs` | New typed routing enums and pure helper predicates. |
| `src/Features/Content/StoryCandidate.cs` | Carries routing metadata from story sources. |
| `src/Features/Content/StoryCandidatePersistent.cs` | Persists routing metadata for deferred interactive candidates. |
| `src/Features/Content/StoryDirector.cs` | Copies routing metadata into persisted candidates and `DispatchItem`. |
| `src/Features/Content/StoryletAgency.cs` | New storylet `agency` metadata POCO. |
| `src/Features/Content/Storylet.cs` | Loads agency metadata and maps storylets into routed `StoryCandidate`s. |
| `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs` | Stores, saves, loads, filters, and emits typed `DispatchItem`s. |
| `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` | Routes status/camp sections by typed metadata instead of text inference. |
| `Tools/Validation/validate_content.py` | Validates `agency` metadata, flavor no-effect rules, and preview requirements. |
| `Tools/Tests/Enlisted.UnitTests/DispatchRoutingTests.cs` | Unit tests for pure routing helpers. |
| `Tools/Tests/Enlisted.UnitTests/StoryletAgencyTests.cs` | Unit tests for agency role mapping and JSON names. |
| `Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj` | Links pure source files used by tests. |
| `Enlisted.csproj` | Registers new C# files. |
| `docs/Features/UI/news-reporting-system.md` | Docs sync for typed routing and surface ownership. |
| `docs/Features/Content/storylet-backbone.md` | Docs sync for storylet `agency` metadata. |

---

### Task 1: Add Typed Dispatch Routing Model

**Files:**
- Create: `src/Features/Interface/Models/DispatchRouting.cs`
- Modify: `Enlisted.csproj`
- Modify: `Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj`
- Create: `Tools/Tests/Enlisted.UnitTests/DispatchRoutingTests.cs`

- [ ] **Step 1: Write failing unit tests for routing defaults and predicates**

Create `Tools/Tests/Enlisted.UnitTests/DispatchRoutingTests.cs`:

```csharp
using Enlisted.Features.Interface.Models;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class DispatchRoutingTests
{
    [Fact]
    public void DefaultsPreserveLegacyPersonalFeedBehavior()
    {
        var route = DispatchRoute.DefaultPersonal;

        Assert.Equal(DispatchDomain.Personal, route.Domain);
        Assert.Equal(DispatchSourceKind.Unknown, route.SourceKind);
        Assert.Equal(DispatchSurfaceHint.Auto, route.SurfaceHint);
    }

    [Fact]
    public void KingdomDispatchesOnlyUseDispatchesSurface()
    {
        var route = new DispatchRoute(
            DispatchDomain.Kingdom,
            DispatchSourceKind.Flavor,
            DispatchSurfaceHint.Dispatches);

        Assert.True(route.IsKingdomDispatch);
        Assert.False(route.IsPersonalForYou);
        Assert.False(route.IsCampActivity);
    }

    [Fact]
    public void ServiceStanceDefaultsToYouAndMusterSurfaces()
    {
        var route = new DispatchRoute(
            DispatchDomain.Personal,
            DispatchSourceKind.ServiceStance,
            DispatchSurfaceHint.You);

        Assert.True(route.IsPersonalForYou);
        Assert.True(route.CountsForMusterRecap);
        Assert.False(route.IsCampActivity);
    }

    [Fact]
    public void ActivityOverrideCanStayCampScoped()
    {
        var route = new DispatchRoute(
            DispatchDomain.Camp,
            DispatchSourceKind.ActivityOverride,
            DispatchSurfaceHint.CampActivities);

        Assert.False(route.IsPersonalForYou);
        Assert.True(route.IsCampActivity);
        Assert.False(route.IsKingdomDispatch);
    }
}
```

- [ ] **Step 2: Link the missing production file in the test project and verify the test fails**

Modify `Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj`:

```xml
  <ItemGroup>
    <Compile Include="..\..\..\src\Features\Enlistment\Core\GraceLordMarkerRefreshPolicy.cs" Link="GraceLordMarkerRefreshPolicy.cs" />
    <Compile Include="..\..\..\src\Features\Interface\Models\DispatchRouting.cs" Link="DispatchRouting.cs" />
  </ItemGroup>
```

Run:

```powershell
dotnet test Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj --filter DispatchRoutingTests
```

Expected: FAIL with compiler errors for missing `Enlisted.Features.Interface.Models.DispatchRoute`.

- [ ] **Step 3: Create the routing model**

Create `src/Features/Interface/Models/DispatchRouting.cs`:

```csharp
namespace Enlisted.Features.Interface.Models
{
    public enum DispatchDomain
    {
        Unknown = 0,
        Kingdom = 1,
        Personal = 2,
        Camp = 3
    }

    public enum DispatchSourceKind
    {
        Unknown = 0,
        ServiceStance = 1,
        Order = 2,
        ActivityOverride = 3,
        ModalIncident = 4,
        Routine = 5,
        Battle = 6,
        Muster = 7,
        Promotion = 8,
        Condition = 9,
        Flavor = 10
    }

    public enum DispatchSurfaceHint
    {
        Auto = 0,
        Dispatches = 1,
        Upcoming = 2,
        You = 3,
        SinceLastMuster = 4,
        CampActivities = 5,
        ModalOnly = 6
    }

    public readonly struct DispatchRoute
    {
        public static DispatchRoute DefaultPersonal =>
            new DispatchRoute(DispatchDomain.Personal, DispatchSourceKind.Unknown, DispatchSurfaceHint.Auto);

        public DispatchRoute(DispatchDomain domain, DispatchSourceKind sourceKind, DispatchSurfaceHint surfaceHint)
        {
            Domain = domain;
            SourceKind = sourceKind;
            SurfaceHint = surfaceHint;
        }

        public DispatchDomain Domain { get; }
        public DispatchSourceKind SourceKind { get; }
        public DispatchSurfaceHint SurfaceHint { get; }

        public bool IsKingdomDispatch => Domain == DispatchDomain.Kingdom &&
            (SurfaceHint == DispatchSurfaceHint.Auto || SurfaceHint == DispatchSurfaceHint.Dispatches);

        public bool IsPersonalForYou => Domain == DispatchDomain.Personal &&
            (SurfaceHint == DispatchSurfaceHint.Auto || SurfaceHint == DispatchSurfaceHint.You);

        public bool IsCampActivity => Domain == DispatchDomain.Camp ||
            SourceKind == DispatchSourceKind.ActivityOverride ||
            SurfaceHint == DispatchSurfaceHint.CampActivities;

        public bool CountsForMusterRecap => Domain == DispatchDomain.Personal &&
            (SourceKind == DispatchSourceKind.ServiceStance ||
             SourceKind == DispatchSourceKind.Order ||
             SourceKind == DispatchSourceKind.Battle ||
             SourceKind == DispatchSourceKind.Condition ||
             SourceKind == DispatchSourceKind.Promotion ||
             SourceKind == DispatchSourceKind.Muster ||
             SourceKind == DispatchSourceKind.ModalIncident);
    }
}
```

- [ ] **Step 4: Register the new C# file in the mod project**

Add to `Enlisted.csproj` near the existing `src\Features\Interface` includes:

```xml
    <Compile Include="src\Features\Interface\Models\DispatchRouting.cs"/>
```

Run:

```powershell
dotnet test Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj --filter DispatchRoutingTests
```

Expected: PASS for all `DispatchRoutingTests`.

- [ ] **Step 5: Commit**

```powershell
git add src/Features/Interface/Models/DispatchRouting.cs Tools/Tests/Enlisted.UnitTests/DispatchRoutingTests.cs Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj Enlisted.csproj
git commit -m "feat: add typed dispatch routing model"
```

---

### Task 2: Carry Routing Through StoryCandidate and StoryDirector

**Files:**
- Modify: `src/Features/Content/StoryCandidate.cs`
- Modify: `src/Features/Content/StoryCandidatePersistent.cs`
- Modify: `src/Features/Content/StoryDirector.cs`

- [ ] **Step 1: Extend `StoryCandidate` with routing fields**

Add using:

```csharp
using Enlisted.Features.Interface.Models;
```

Add properties after `MinDisplayDays`:

```csharp
        public DispatchDomain DispatchDomain { get; set; } = DispatchDomain.Personal;
        public DispatchSourceKind DispatchSourceKind { get; set; } = DispatchSourceKind.Unknown;
        public DispatchSurfaceHint DispatchSurfaceHint { get; set; } = DispatchSurfaceHint.Auto;
```

- [ ] **Step 2: Extend `StoryCandidatePersistent` with routing fields**

Add using:

```csharp
using Enlisted.Features.Interface.Models;
```

Add properties after `StoryKey`:

```csharp
        public DispatchDomain DispatchDomain { get; set; } = DispatchDomain.Personal;
        public DispatchSourceKind DispatchSourceKind { get; set; } = DispatchSourceKind.Unknown;
        public DispatchSurfaceHint DispatchSurfaceHint { get; set; } = DispatchSurfaceHint.Auto;
```

- [ ] **Step 3: Copy routing metadata into deferred persistent candidates**

In `StoryDirector.MakePersistent`, add these assignments:

```csharp
                StoryKey = c.StoryKey,
                DispatchDomain = c.DispatchDomain,
                DispatchSourceKind = c.DispatchSourceKind,
                DispatchSurfaceHint = c.DispatchSurfaceHint
```

When reconstructing the pseudo candidate from `StoryCandidatePersistent`, copy the fields:

```csharp
                    DispatchDomain = next.DispatchDomain,
                    DispatchSourceKind = next.DispatchSourceKind,
                    DispatchSurfaceHint = next.DispatchSurfaceHint
```

- [ ] **Step 4: Pass routing metadata to the news dispatch path**

In `StoryDirector.WriteDispatchItem`, extend the `news.AddPersonalDispatch(...)` call:

```csharp
                body: c.RenderedBody,
                domain: c.DispatchDomain,
                sourceKind: c.DispatchSourceKind,
                surfaceHint: c.DispatchSurfaceHint);
```

Task 3 adds this overload, so this task may not compile until Task 3 is completed if implemented in isolation. Keep Task 2 and Task 3 in the same short branch checkpoint before building.

- [ ] **Step 5: Commit**

```powershell
git add src/Features/Content/StoryCandidate.cs src/Features/Content/StoryCandidatePersistent.cs src/Features/Content/StoryDirector.cs
git commit -m "feat: carry dispatch routing through story candidates"
```

---

### Task 3: Persist Routing in DispatchItem and News APIs

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs`

- [ ] **Step 1: Add routing fields to `DispatchItem`**

Add using:

```csharp
using Enlisted.Features.Interface.Models;
```

Add fields after `Body`:

```csharp
        public DispatchDomain Domain { get; set; }
        public DispatchSourceKind SourceKind { get; set; }
        public DispatchSurfaceHint SurfaceHint { get; set; }

        public DispatchRoute Route => new DispatchRoute(
            Domain == DispatchDomain.Unknown ? DispatchDomain.Personal : Domain,
            SourceKind,
            SurfaceHint);
```

Update `Equals` to include:

```csharp
                   Domain == other.Domain &&
                   SourceKind == other.SourceKind &&
                   SurfaceHint == other.SurfaceHint &&
```

Update `GetHashCode` to include:

```csharp
                hash = hash * 31 + Domain.GetHashCode();
                hash = hash * 31 + SourceKind.GetHashCode();
                hash = hash * 31 + SurfaceHint.GetHashCode();
```

- [ ] **Step 2: Set routing defaults in every direct `new DispatchItem`**

For kingdom-feed items, set:

```csharp
Domain = DispatchDomain.Kingdom,
SourceKind = DispatchSourceKind.Flavor,
SurfaceHint = DispatchSurfaceHint.Dispatches,
```

For existing personal-feed items whose source is not yet typed, set:

```csharp
Domain = DispatchDomain.Personal,
SourceKind = DispatchSourceKind.Unknown,
SurfaceHint = DispatchSurfaceHint.Auto,
```

For `AddRoutineOutcome`, set:

```csharp
Domain = DispatchDomain.Personal,
SourceKind = DispatchSourceKind.Routine,
SurfaceHint = DispatchSurfaceHint.You,
```

- [ ] **Step 3: Extend `AddPersonalDispatch` overloads**

Change the current typed overload signature to:

```csharp
        public void AddPersonalDispatch(
            string category,
            string headlineKey,
            Dictionary<string, string> placeholderValues,
            string storyKey,
            int severity,
            int minDisplayDays,
            StoryTier tier,
            HashSet<StoryBeat> beats,
            string body,
            DispatchDomain domain = DispatchDomain.Personal,
            DispatchSourceKind sourceKind = DispatchSourceKind.Unknown,
            DispatchSurfaceHint surfaceHint = DispatchSurfaceHint.Auto)
```

Forward these to `AddPersonalNews` by extending its private signature with the same three optional arguments. In the `DispatchItem` initializer inside `AddPersonalNews`, set:

```csharp
                Domain = domain,
                SourceKind = sourceKind,
                SurfaceHint = surfaceHint,
```

- [ ] **Step 4: Persist routing fields**

In `SaveDispatchItem`, after `severity`, add:

```csharp
            var domain = (int)(item.Domain == DispatchDomain.Unknown ? DispatchDomain.Personal : item.Domain);
            var sourceKind = (int)item.SourceKind;
            var surfaceHint = (int)item.SurfaceHint;
```

Sync them:

```csharp
            _ = dataStore.SyncData($"{prefix}_domain", ref domain);
            _ = dataStore.SyncData($"{prefix}_sourceKind", ref sourceKind);
            _ = dataStore.SyncData($"{prefix}_surfaceHint", ref surfaceHint);
```

In `LoadDispatchItem`, add local defaults:

```csharp
            var domain = (int)DispatchDomain.Personal;
            var sourceKind = (int)DispatchSourceKind.Unknown;
            var surfaceHint = (int)DispatchSurfaceHint.Auto;
```

Sync them:

```csharp
            _ = dataStore.SyncData($"{prefix}_domain", ref domain);
            _ = dataStore.SyncData($"{prefix}_sourceKind", ref sourceKind);
            _ = dataStore.SyncData($"{prefix}_surfaceHint", ref surfaceHint);
```

Set them in the loaded item:

```csharp
                Domain = (DispatchDomain)domain,
                SourceKind = (DispatchSourceKind)sourceKind,
                SurfaceHint = (DispatchSurfaceHint)surfaceHint,
```

- [ ] **Step 5: Build**

Run:

```powershell
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: build succeeds. If BannerlordLauncher holds the DLL, close it and rerun the same command.

- [ ] **Step 6: Commit**

```powershell
git add src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs
git commit -m "feat: persist typed dispatch routing"
```

---

### Task 4: Add Storylet Agency Metadata

**Files:**
- Create: `src/Features/Content/StoryletAgency.cs`
- Modify: `src/Features/Content/Storylet.cs`
- Modify: `Enlisted.csproj`
- Modify: `Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj`
- Create: `Tools/Tests/Enlisted.UnitTests/StoryletAgencyTests.cs`

- [ ] **Step 1: Write failing tests for agency role mapping**

Create `Tools/Tests/Enlisted.UnitTests/StoryletAgencyTests.cs`:

```csharp
using Enlisted.Features.Content;
using Enlisted.Features.Interface.Models;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class StoryletAgencyTests
{
    [Theory]
    [InlineData("stance_drift", DispatchDomain.Personal, DispatchSourceKind.ServiceStance, DispatchSurfaceHint.You)]
    [InlineData("order_accept", DispatchDomain.Personal, DispatchSourceKind.Order, DispatchSurfaceHint.Upcoming)]
    [InlineData("order_outcome", DispatchDomain.Personal, DispatchSourceKind.Order, DispatchSurfaceHint.SinceLastMuster)]
    [InlineData("activity_override", DispatchDomain.Camp, DispatchSourceKind.ActivityOverride, DispatchSurfaceHint.CampActivities)]
    [InlineData("realm_dispatch", DispatchDomain.Kingdom, DispatchSourceKind.Flavor, DispatchSurfaceHint.Dispatches)]
    public void DefaultForRoleMapsToExpectedRoute(
        string role,
        DispatchDomain domain,
        DispatchSourceKind sourceKind,
        DispatchSurfaceHint surfaceHint)
    {
        var agency = StoryletAgency.DefaultForRole(role);

        Assert.Equal(domain, agency.ToDomain());
        Assert.Equal(sourceKind, agency.ToSourceKind());
        Assert.Equal(surfaceHint, agency.ToSurfaceHint());
    }
}
```

Add test project links:

```xml
    <Compile Include="..\..\..\src\Features\Content\StoryletAgency.cs" Link="StoryletAgency.cs" />
```

Run:

```powershell
dotnet test Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj --filter StoryletAgencyTests
```

Expected: FAIL with compiler errors for missing `StoryletAgency`.

- [ ] **Step 2: Create `StoryletAgency`**

Create `src/Features/Content/StoryletAgency.cs`:

```csharp
using Enlisted.Features.Interface.Models;
using Newtonsoft.Json;

namespace Enlisted.Features.Content
{
    public sealed class StoryletAgency
    {
        [JsonProperty("role")]
        public string Role { get; set; } = string.Empty;

        [JsonProperty("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; } = string.Empty;

        [JsonProperty("surfaceHint")]
        public string SurfaceHint { get; set; } = string.Empty;

        public static StoryletAgency DefaultForRole(string role)
        {
            switch ((role ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "stance_drift":
                case "stance_interrupt":
                    return new StoryletAgency { Role = role, Domain = "personal", SourceKind = "service_stance", SurfaceHint = "you" };
                case "order_accept":
                    return new StoryletAgency { Role = role, Domain = "personal", SourceKind = "order", SurfaceHint = "upcoming" };
                case "order_phase":
                    return new StoryletAgency { Role = role, Domain = "personal", SourceKind = "order", SurfaceHint = "you" };
                case "order_outcome":
                    return new StoryletAgency { Role = role, Domain = "personal", SourceKind = "order", SurfaceHint = "since_last_muster" };
                case "activity_override":
                    return new StoryletAgency { Role = role, Domain = "camp", SourceKind = "activity_override", SurfaceHint = "camp_activities" };
                case "modal_incident":
                    return new StoryletAgency { Role = role, Domain = "personal", SourceKind = "modal_incident", SurfaceHint = "modal_only" };
                case "news_flavor":
                    return new StoryletAgency { Role = role, Domain = "personal", SourceKind = "flavor", SurfaceHint = "auto" };
                case "realm_dispatch":
                    return new StoryletAgency { Role = role, Domain = "kingdom", SourceKind = "flavor", SurfaceHint = "dispatches" };
                default:
                    return new StoryletAgency { Role = role ?? string.Empty, Domain = "personal", SourceKind = "unknown", SurfaceHint = "auto" };
            }
        }

        public DispatchDomain ToDomain()
        {
            switch ((Domain ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "kingdom": return DispatchDomain.Kingdom;
                case "camp": return DispatchDomain.Camp;
                default: return DispatchDomain.Personal;
            }
        }

        public DispatchSourceKind ToSourceKind()
        {
            switch ((SourceKind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "service_stance": return DispatchSourceKind.ServiceStance;
                case "order": return DispatchSourceKind.Order;
                case "activity_override": return DispatchSourceKind.ActivityOverride;
                case "modal_incident": return DispatchSourceKind.ModalIncident;
                case "routine": return DispatchSourceKind.Routine;
                case "battle": return DispatchSourceKind.Battle;
                case "muster": return DispatchSourceKind.Muster;
                case "promotion": return DispatchSourceKind.Promotion;
                case "condition": return DispatchSourceKind.Condition;
                case "flavor": return DispatchSourceKind.Flavor;
                default: return DispatchSourceKind.Unknown;
            }
        }

        public DispatchSurfaceHint ToSurfaceHint()
        {
            switch ((SurfaceHint ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "dispatches": return DispatchSurfaceHint.Dispatches;
                case "upcoming": return DispatchSurfaceHint.Upcoming;
                case "you": return DispatchSurfaceHint.You;
                case "since_last_muster": return DispatchSurfaceHint.SinceLastMuster;
                case "camp_activities": return DispatchSurfaceHint.CampActivities;
                case "modal_only": return DispatchSurfaceHint.ModalOnly;
                default: return DispatchSurfaceHint.Auto;
            }
        }
    }
}
```

- [ ] **Step 3: Add agency to `Storylet` and candidate conversion**

In `Storylet.cs`, add property near `Arc`:

```csharp
        public StoryletAgency Agency { get; set; }
```

In `ToCandidate`, before returning the candidate:

```csharp
            var agency = Agency ?? StoryletAgency.DefaultForRole(string.Empty);
```

Add candidate assignments:

```csharp
                DispatchDomain = agency.ToDomain(),
                DispatchSourceKind = agency.ToSourceKind(),
                DispatchSurfaceHint = agency.ToSurfaceHint()
```

- [ ] **Step 4: Register and test**

Add to `Enlisted.csproj`:

```xml
    <Compile Include="src\Features\Content\StoryletAgency.cs"/>
```

Run:

```powershell
dotnet test Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj --filter StoryletAgencyTests
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: tests pass and build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add src/Features/Content/StoryletAgency.cs src/Features/Content/Storylet.cs Tools/Tests/Enlisted.UnitTests/StoryletAgencyTests.cs Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj Enlisted.csproj
git commit -m "feat: add storylet agency routing metadata"
```

---

### Task 5: Route Status and Camp Menu Sections by Typed Metadata

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`

- [ ] **Step 1: Add typed feed selectors**

Add using:

```csharp
using Enlisted.Features.Interface.Models;
```

Add helper methods near `GetUnreadHighSeverity`:

```csharp
        private static List<DispatchItem> GetPersonalItemsForYou(EnlistedNewsBehavior news, int maxItems)
        {
            if (news == null)
            {
                return new List<DispatchItem>();
            }

            return news.GetVisiblePersonalFeedItems(Math.Max(maxItems * 2, maxItems))
                .Where(item => item.Route.IsPersonalForYou)
                .Take(maxItems)
                .ToList();
        }

        private static List<DispatchItem> GetPersonalItemsForMusterPeriod(EnlistedNewsBehavior news, int sinceDay)
        {
            if (news == null)
            {
                return new List<DispatchItem>();
            }

            return news.GetPersonalFeedSince(sinceDay)
                .Where(item => item.Route.CountsForMusterRecap)
                .ToList();
        }

        private static List<DispatchItem> GetCampActivityItems(EnlistedNewsBehavior news, int maxItems)
        {
            if (news == null)
            {
                return new List<DispatchItem>();
            }

            return news.GetVisiblePersonalFeedItems(Math.Max(maxItems * 2, maxItems))
                .Where(item => item.Route.IsCampActivity)
                .Take(maxItems)
                .ToList();
        }
```

- [ ] **Step 2: Fold personal feed into `YOU`**

In `BuildRecentActivitiesNarrative`, replace direct `news?.GetVisiblePersonalFeedItems(3)` use with:

```csharp
                var personalItems = GetPersonalItemsForYou(news, 3);
```

In `BuildCampHubText`, merge the current `RECENT ACTIVITY` and `YOUR STATUS` blocks under a `YOU` header by building a single string:

```csharp
                var youParts = new List<string>();
                var playerStatus = BuildPlayerPersonalStatus(enlistment);
                if (!string.IsNullOrWhiteSpace(playerStatus))
                {
                    youParts.Add(playerStatus);
                }

                var recentActivities = BuildRecentActivitiesNarrative(enlistment);
                if (!string.IsNullOrWhiteSpace(recentActivities))
                {
                    youParts.Add(recentActivities);
                }

                if (youParts.Count > 0)
                {
                    var headerText = new TextObject("{=status_header_you}YOU").ToString();
                    _ = sb.AppendLine($"<span style=\"Header\">{headerText}</span>");
                    _ = sb.AppendLine(string.Join(" ", youParts));
                    _ = sb.AppendLine();
                }
```

Remove the separate `RECENT ACTIVITY` and `YOUR STATUS` header append blocks after this insertion.

- [ ] **Step 3: Keep `SINCE LAST MUSTER` period-bounded and typed**

Inside `BuildPeriodRecapSection`, replace:

```csharp
                var eventOutcomes = news?.GetRecentEventOutcomes(12) ?? new List<EventOutcomeRecord>();
```

Add after `lastMusterDay` and `currentDay` are calculated:

```csharp
                var periodDispatches = GetPersonalItemsForMusterPeriod(news, lastMusterDay);
                var stanceSummaries = periodDispatches
                    .Where(item => item.SourceKind == DispatchSourceKind.ServiceStance)
                    .Take(2)
                    .Select(item => EnlistedNewsBehavior.FormatDispatchForDisplay(item, includeColor: true))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();
```

Append stance summaries before the muster countdown:

```csharp
                foreach (var summary in stanceSummaries)
                {
                    parts.Add(summary);
                }
```

- [ ] **Step 4: Move camp activity items to `CAMP ACTIVITIES`**

In `BuildCompanyStatusSummary`, after company needs are appended, add:

```csharp
                var news = EnlistedNewsBehavior.Instance;
                var campItems = GetCampActivityItems(news, 2);
                foreach (var item in campItems)
                {
                    var text = EnlistedNewsBehavior.FormatDispatchForDisplay(item, includeColor: true);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parts.Add(text);
                    }
                }
```

- [ ] **Step 5: Build**

Run:

```powershell
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 6: Commit**

```powershell
git add src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs
git commit -m "feat: route status menus by dispatch metadata"
```

---

### Task 6: Add Stance Summary Dispatch API

**Files:**
- Create: `src/Features/Agency/StanceSummary.cs`
- Create: `src/Features/Agency/StanceSummaryAccumulator.cs`
- Modify: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create the stance summary data types**

Create `src/Features/Agency/StanceSummary.cs`:

```csharp
namespace Enlisted.Features.Agency
{
    public sealed class StanceSummary
    {
        public string StanceId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public int Severity { get; set; }
        public int DaysCovered { get; set; }
        public string StoryKey { get; set; } = string.Empty;
    }
}
```

Create `src/Features/Agency/StanceSummaryAccumulator.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Enlisted.Features.Agency
{
    public sealed class StanceSummaryAccumulator
    {
        private readonly List<string> _lines = new List<string>();

        public string StanceId { get; private set; } = string.Empty;
        public int DaysCovered { get; private set; }

        public void Add(string stanceId, string line, int daysCovered)
        {
            StanceId = stanceId ?? string.Empty;
            DaysCovered += daysCovered;

            if (!string.IsNullOrWhiteSpace(line))
            {
                _lines.Add(line.Trim());
            }
        }

        public bool CanFlush => DaysCovered >= 2 && _lines.Count > 0;

        public StanceSummary Flush(int currentDay)
        {
            var body = string.Join(" ", _lines.Take(3));
            var summary = new StanceSummary
            {
                StanceId = StanceId,
                Title = "Service routine",
                Body = body,
                Severity = 0,
                DaysCovered = DaysCovered,
                StoryKey = $"stance:{StanceId}:{currentDay}"
            };

            _lines.Clear();
            DaysCovered = 0;
            return summary;
        }
    }
}
```

- [ ] **Step 2: Register new files**

Add to `Enlisted.csproj`:

```xml
    <Compile Include="src\Features\Agency\StanceSummary.cs"/>
    <Compile Include="src\Features\Agency\StanceSummaryAccumulator.cs"/>
```

- [ ] **Step 3: Add `AddStanceSummary` to news behavior**

In `EnlistedNewsBehavior.cs`, add using:

```csharp
using Enlisted.Features.Agency;
using Enlisted.Features.Interface.Models;
```

Add public method near `AddRoutineOutcome`:

```csharp
        public void AddStanceSummary(StanceSummary summary)
        {
            if (summary == null || string.IsNullOrWhiteSpace(summary.Body))
            {
                return;
            }

            AddPersonalDispatch(
                category: "stance",
                headlineKey: string.IsNullOrWhiteSpace(summary.Title) ? "Service routine" : summary.Title,
                placeholderValues: null,
                storyKey: string.IsNullOrWhiteSpace(summary.StoryKey)
                    ? $"stance:{summary.StanceId}:{(int)CampaignTime.Now.ToDays}"
                    : summary.StoryKey,
                severity: summary.Severity,
                minDisplayDays: 1,
                tier: summary.Severity >= 2 ? StoryTier.Headline : StoryTier.Log,
                beats: null,
                body: summary.Body,
                domain: DispatchDomain.Personal,
                sourceKind: DispatchSourceKind.ServiceStance,
                surfaceHint: DispatchSurfaceHint.You);
        }
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add src/Features/Agency/StanceSummary.cs src/Features/Agency/StanceSummaryAccumulator.cs src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs Enlisted.csproj
git commit -m "feat: add stance summary news dispatches"
```

---

### Task 7: Validate Storylet Agency Metadata

**Files:**
- Modify: `Tools/Validation/validate_content.py`
- Modify: `ModuleData/Enlisted/Storylets/*.json` only for minimal metadata required by validation

- [ ] **Step 1: Add allowed agency constants**

In `Tools/Validation/validate_content.py`, near the Phase 12 constants, add:

```python
_AGENCY_ROLES = {
    "stance_drift",
    "stance_interrupt",
    "order_accept",
    "order_phase",
    "order_outcome",
    "activity_override",
    "modal_incident",
    "news_flavor",
    "realm_dispatch",
}

_AGENCY_DOMAINS = {"personal", "kingdom", "camp"}
_AGENCY_SOURCE_KINDS = {
    "unknown",
    "service_stance",
    "order",
    "activity_override",
    "modal_incident",
    "routine",
    "battle",
    "muster",
    "promotion",
    "condition",
    "flavor",
}
_AGENCY_SURFACE_HINTS = {
    "auto",
    "dispatches",
    "upcoming",
    "you",
    "since_last_muster",
    "camp_activities",
    "modal_only",
}
```

- [ ] **Step 2: Add effect detection helpers**

Add:

```python
def _storylet_has_effects(storylet: dict) -> bool:
    if storylet.get("immediate"):
        return True
    for option in storylet.get("options", []) or []:
        if option.get("effects"):
            return True
    return False


def _storylet_has_preview(storylet: dict) -> bool:
    preview = storylet.get("preview")
    return isinstance(preview, dict) and any(preview.get(k) for k in ("grants", "may_cost", "mayCost", "risks"))
```

- [ ] **Step 3: Validate `agency` block in `_validate_storylet_file`**

Inside the per-storylet loop in `_validate_storylet_file`, after `sid` is set, add:

```python
        agency = storylet.get("agency")
        if agency is not None:
            if not isinstance(agency, dict):
                ctx.add_issue("error", "storylet-agency", f"storylet '{sid}' agency must be an object", str(file_path), sid)
            else:
                role = str(agency.get("role", "")).strip()
                domain = str(agency.get("domain", "")).strip()
                source_kind = str(agency.get("sourceKind", "")).strip()
                surface_hint = str(agency.get("surfaceHint", "")).strip()

                if role and role not in _AGENCY_ROLES:
                    ctx.add_issue("error", "storylet-agency", f"storylet '{sid}' has unknown agency.role '{role}'", str(file_path), sid)
                if domain and domain not in _AGENCY_DOMAINS:
                    ctx.add_issue("error", "storylet-agency", f"storylet '{sid}' has unknown agency.domain '{domain}'", str(file_path), sid)
                if source_kind and source_kind not in _AGENCY_SOURCE_KINDS:
                    ctx.add_issue("error", "storylet-agency", f"storylet '{sid}' has unknown agency.sourceKind '{source_kind}'", str(file_path), sid)
                if surface_hint and surface_hint not in _AGENCY_SURFACE_HINTS:
                    ctx.add_issue("error", "storylet-agency", f"storylet '{sid}' has unknown agency.surfaceHint '{surface_hint}'", str(file_path), sid)

                if role == "news_flavor" and _storylet_has_effects(storylet):
                    ctx.add_issue("error", "storylet-agency", f"storylet '{sid}' agency.role news_flavor cannot have effects", str(file_path), sid)
                if role == "realm_dispatch" and _storylet_has_effects(storylet):
                    ctx.add_issue("error", "storylet-agency", f"storylet '{sid}' agency.role realm_dispatch cannot mutate player state", str(file_path), sid)
                if role in {"order_accept", "activity_override", "stance_interrupt", "modal_incident"} and not _storylet_has_preview(storylet):
                    ctx.add_issue("error", "storylet-agency", f"storylet '{sid}' agency.role '{role}' requires preview metadata", str(file_path), sid)
```

- [ ] **Step 4: Run validator and fix only metadata errors**

Run:

```powershell
python Tools/Validation/validate_content.py
```

Expected before metadata fixes: errors in category `storylet-agency` for any newly annotated storylet that violates the rules.

Add minimal `agency` blocks to storylets touched by this integration. Use these patterns:

For `ModuleData/Enlisted/Storylets/floor_*.json` flavor-only entries:

```json
"agency": {
  "role": "news_flavor",
  "domain": "camp",
  "sourceKind": "flavor",
  "surfaceHint": "auto"
}
```

For `ModuleData/Enlisted/Storylets/order_*.json` order acceptance/outcome entries:

```json
"agency": {
  "role": "order_phase",
  "domain": "personal",
  "sourceKind": "order",
  "surfaceHint": "you"
}
```

Run again:

```powershell
python Tools/Validation/validate_content.py
```

Expected: validation succeeds.

- [ ] **Step 5: Commit**

```powershell
git add Tools/Validation/validate_content.py ModuleData/Enlisted/Storylets
git commit -m "feat: validate storylet agency metadata"
```

---

### Task 8: Sync Documentation

**Files:**
- Modify: `docs/Features/UI/news-reporting-system.md`
- Modify: `docs/Features/Content/storylet-backbone.md`
- Modify: `docs/INDEX.md` if it links individual UI/content references

- [ ] **Step 1: Update news reporting docs**

In `docs/Features/UI/news-reporting-system.md`, add a section titled `Typed Routing Metadata`:

```markdown
## Typed Routing Metadata

Dispatches carry three routing fields:

| Field | Values | Purpose |
| :--- | :--- | :--- |
| `Domain` | `Kingdom`, `Personal`, `Camp` | Selects the feed family. |
| `SourceKind` | `ServiceStance`, `Order`, `ActivityOverride`, `ModalIncident`, `Routine`, `Battle`, `Muster`, `Promotion`, `Condition`, `Flavor`, `Unknown` | Describes what produced the dispatch. |
| `SurfaceHint` | `Auto`, `Dispatches`, `Upcoming`, `You`, `SinceLastMuster`, `CampActivities`, `ModalOnly` | Suggests where UI should render the item. |

`DISPATCHES` consumes kingdom-domain items. Camp `YOU` consumes personal items intended for the player. `SINCE LAST MUSTER` reads period-bounded personal outcomes. `CAMP ACTIVITIES` consumes camp-domain items and optional short activity override outcomes.
```

- [ ] **Step 2: Update storylet backbone docs**

In `docs/Features/Content/storylet-backbone.md`, add `Agency metadata` after the vocabulary section:

````markdown
## Agency metadata

Storylets may include an `agency` object:

```json
"agency": {
  "role": "order_phase",
  "domain": "personal",
  "sourceKind": "order",
  "surfaceHint": "you"
}
```

`role` controls authoring rules. `domain`, `sourceKind`, and `surfaceHint` map into typed dispatch routing when the storylet emits a `StoryCandidate`.

Flavor-only storylets use `role: "news_flavor"` and cannot have effects. Realm dispatch storylets use `role: "realm_dispatch"` and cannot mutate player state. Order acceptance, camp override, stance interruption, and modal incident storylets require preview metadata.
````

- [ ] **Step 3: Run docs/content validation**

Run:

```powershell
python Tools/Validation/validate_content.py
```

Expected: validation succeeds.

- [ ] **Step 4: Commit**

```powershell
git add docs/Features/UI/news-reporting-system.md docs/Features/Content/storylet-backbone.md docs/INDEX.md
git commit -m "docs: document agency news routing"
```

---

### Task 9: Final Verification

**Files:**
- No new files.

- [ ] **Step 1: Run unit tests**

```powershell
dotnet test Tools/Tests/Enlisted.UnitTests/Enlisted.UnitTests.csproj
```

Expected: all tests pass.

- [ ] **Step 2: Run content validation**

```powershell
python Tools/Validation/validate_content.py
```

Expected: validation succeeds.

- [ ] **Step 3: Build the mod**

```powershell
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: build succeeds and copies `Enlisted.dll` to both Bannerlord output folders. If `MSB3021` reports the DLL is locked, close BannerlordLauncher and rerun the same command.

- [ ] **Step 4: Review changed files**

```powershell
git status --short
git diff --stat
git diff --check
```

Expected:

- `git diff --check` prints no errors.
- Changed files are limited to routing/news/status/storylet/docs/test files from this plan.
- No unrelated `.codex/config.toml` changes are staged.

- [ ] **Step 5: Finish verification state**

If Step 1-4 required edits, return to the task that owns the edited file, rerun that task's verification command, and use that task's commit command. If no edits were required, do not create an empty commit.

---

## Self-Review Notes

Spec coverage:

- Typed routing contract: Tasks 1-3.
- Status/news surface behavior: Task 5.
- Storylet/event authoring metadata: Tasks 4 and 7.
- Stance summary batching API: Task 6.
- Docs sync: Task 8.
- Validation/build: Task 9.

Known intentional split:

- The full agency gate, stance runtime, and short activity override runtime are separate implementation plans. This plan builds the routing substrate those systems will use.
