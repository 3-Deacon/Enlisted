# Event Meaning Prerequisites — Implementation Plan

> **RETIRED (2026-04-24).** This plan is a frozen-in-time execution record. The prerequisite work shipped on `development`: PR-b persisted `EventDeliveryManager` pending queues (`1551e7c` + `546a426`), and PR-c added typed `DispatchItem` fields (`Tier`, `Beats`, `Body`) plus consumer migrations (`a26a977` + `13e2333`). Current behavior is owned by `StoryDirector`, `EventDeliveryManager`, and `EnlistedNewsBehavior.DispatchItem`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the three Director-plumbing prerequisites from the Event Meaning Design spec (§5) so Plan 1 (Foundations) can build on a clean foundation and downstream arcs/variants inherit no pre-existing bugs.

**Architecture:** Three independent, sequential commits on the `development` branch. PR-a is a one-line docstring fix. PR-b persists `EventDeliveryManager._pendingEvents` by event-ID so modals survive save/reload and pacing state never diverges from queue state. PR-c extends `DispatchItem` with typed `Tier`, `Beats`, and `Body` fields and migrates the two consumer sites that currently read magic numbers or substring-match headline keys.

**Tech Stack:** C# / .NET Framework 4.7.2, Bannerlord v1.3.13 (`TaleWorlds.SaveSystem.IDataStore`, `MBObjectManager` for lookup, `CampaignEvents` for tick hooks), Python 3 for content validation (`Tools/Validation/validate_content.py`).

**Verification Strategy (no C# unit tests in project):** Every phase verifies via:

1. **Build:** `dotnet build Enlisted.csproj -c 'Enlisted RETAIL' -p:Platform=x64` produces `Enlisted.dll` in both `bin/Win64_Shipping_Client/` and `bin/Win64_Shipping_wEditor/`. Close `BannerlordLauncher` first — it holds the DLL open and fails the copy with MSB3021.
2. **Validator:** `python Tools/Validation/validate_content.py` passes with zero hard-fail errors.
3. **In-game smoke tests:** explicit step-by-step scenarios with expected log output. Logs land at `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\Session-*.log`.
4. **Debug commands:** PR-b adds two new commands to `DebugToolsBehavior` for queue-persistence verification without needing to trigger real gameplay events.

**Reference spec:** `docs/superpowers/specs/2026-04-19-event-meaning-design.md` §5.1 / §5.2 / §5.3 / §7.1 / §7.2 / §7.3.

**Setup note:** This plan runs directly on `development`. Each phase (A/B/C) produces exactly one commit on that branch. If you prefer isolation, create a worktree first: `git worktree add ../Enlisted-prereqs development`.

---

## File Structure Overview

**Files modified:**
- `src/Features/Content/SeverityClassifier.cs` — PR-a: docstring correction (lines 5–10)
- `src/Features/Content/EventDeliveryManager.cs` — PR-b: add `_pendingEventIds` field, `SyncData` implementation, `OnSessionLaunched` resolution hook, bounded-cap enforcement
- `src/Features/Content/StoryDirector.cs` — PR-c: update `WriteDispatchItem` (line ~308) to populate new DispatchItem fields
- `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs` — PR-c: extend `DispatchItem` struct (line ~5809) with `Tier`, `Beats`, `Body`, `IsHeadline`; extend `AddPersonalDispatch` (line ~5227) with new parameters via overload
- `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` — PR-c: migrate `GetUnreadHighSeverity` (line ~1557) from `Severity < 2` filter to `IsHeadline`
- `src/Features/Enlistment/Behaviors/MusterMenuHandler.cs` — PR-c: migrate `CountBattlesThisPeriod` (line ~3691) from substring match to typed `Beats` filter
- `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` — PR-b: no changes needed (`List<string>` already registered at line 102). PR-c: register `HashSet<StoryBeat>` container
- `src/Debugging/Behaviors/DebugToolsBehavior.cs` — PR-b: add two debug commands (`debug_queue_test_event`, `debug_print_queue`) for save/reload verification

**No new files created.**

---

## Phase A — PR-a: SeverityClassifier docstring correction

**Files:**
- Modify: `src/Features/Content/SeverityClassifier.cs:5-10`

The existing docstring claims the candidate's tier is "bounded by the strictest BeatMaxTier among the candidate's beats." The code at lines 66–77 in fact selects the **most permissive** cap (starts `tierCap = Log`, then takes `max`). All current emitters pass single-beat sets, so the bug is dormant, but the comment misleads readers. Spec §5.1 resolution: rewrite the comment, keep the code.

### Task A1: Update the class-level docstring

- [ ] **Step 1: Read the current docstring**

Open `src/Features/Content/SeverityClassifier.cs` and confirm lines 5–10 read:

```csharp
/// <summary>
/// Scores a StoryCandidate and caps its tier. Score = SeverityHint + max beat weight
/// in the candidate's Beats set + player-stake bonuses (enlisted lord, kingdom, visited
/// settlement). Final tier is max(candidate.ProposedTier, score-derived-tier), bounded
/// by the strictest BeatMaxTier among the candidate's beats.
/// </summary>
```

- [ ] **Step 2: Replace the misleading "strictest" wording**

Edit `src/Features/Content/SeverityClassifier.cs` — replace the docstring block above with:

```csharp
/// <summary>
/// Scores a StoryCandidate and caps its tier. Score = SeverityHint + max beat weight
/// in the candidate's Beats set + player-stake bonuses (enlisted lord, kingdom, visited
/// settlement). Final tier is max(candidate.ProposedTier, score-derived-tier), bounded
/// by the most permissive BeatMaxTier among the candidate's beats — so a Modal-eligible
/// beat (e.g. LordCaptured) is not demoted by an accompanying minor beat (e.g.
/// OrderPhaseTransition). All current emitters pass single-beat sets, so min == max in
/// practice.
/// </summary>
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build Enlisted.csproj -c 'Enlisted RETAIL' -p:Platform=x64`

Expected: Build succeeds, `Enlisted.dll` updated in both `bin/Win64_Shipping_Client/` and `bin/Win64_Shipping_wEditor/`.

- [ ] **Step 4: Normalize CRLF**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/SeverityClassifier.cs`

Expected: Script reports the file already has CRLF or fixes it silently. (The `Write`/`Edit` tools produce LF; `.gitattributes` enforces CRLF on `.cs` files.)

- [ ] **Step 5: Commit**

Run:

```bash
git add src/Features/Content/SeverityClassifier.cs
git commit -m "docs(content): clarify SeverityClassifier cap direction is most-permissive, not strictest"
```

Expected: Single-file commit on `development`. No other files in the diff.

---

## Phase B — PR-b: EventDeliveryManager queue persistence

**Files:**
- Modify: `src/Features/Content/EventDeliveryManager.cs:41-59` and `:400-440` (approximate — OnEventOptionChosen area where queue advances)
- Modify: `src/Debugging/Behaviors/DebugToolsBehavior.cs` (add two debug commands)
- Reference: `docs/superpowers/specs/2026-04-19-event-meaning-design.md §5.2`

Closes the save/load gap: `StoryDirector.Route()` writes `_lastModalDay` synchronously after calling `delivery.QueueEvent(evt)`, but `EventDeliveryManager._pendingEvents` is transient (`SyncData` empty). If the player saves between queue-add and UI-delivery, the event is lost and the pacing floor is burned.

**Approach:** Serialize only event `Id` strings (not full `EventDefinition` object graphs — the catalog owns the authoritative definitions). On load, re-resolve IDs to `EventDefinition` instances via `EventCatalog`. Missing IDs (author removed the event) are dropped with a `ModLogger.Expected` info log. Enforce a bounded cap of 32 (matches `_deferredInteractive` cap in the Director).

### Task B1: Confirm the EventCatalog resolver contract

- [ ] **Step 1: Confirm resolver exists**

Open `src/Features/Content/EventCatalog.cs:127`. Confirm it contains:

```csharp
public static EventDefinition GetEvent(string eventId)
{
    if (!_initialized)
    {
        Initialize();
    }
    EventsById.TryGetValue(eventId, out var eventDef);
    return eventDef;
}
```

The resolver is **static** (not instance-based — do not use `EventCatalog.Instance`) and **self-initializes** (safe to call at any time after mod load, including before any campaign is live). Returns `null` for unknown IDs. No exception on null or empty input (`TryGetValue` handles that).

### Task B2: Add `_pendingEventIds` serialization field to EventDeliveryManager

- [ ] **Step 1: Open the file and locate the field block**

File: `src/Features/Content/EventDeliveryManager.cs`. Current fields at lines 41–43:

```csharp
private readonly Queue<EventDefinition> _pendingEvents = new();
private bool _isShowingEvent;
private EventDefinition _currentEvent;
```

- [ ] **Step 2: Add the persistent ID list as a parallel structure**

Below the existing fields, add:

```csharp
// Save-backed snapshot of _pendingEvents. We serialize event Ids only and resolve
// back to EventDefinition instances on load via EventCatalog. Full EventDefinition
// graphs are authored content — the catalog is authoritative. See spec §5.2 and
// docs/superpowers/specs/2026-04-19-event-meaning-design.md.
private List<string> _pendingEventIds = new List<string>();

// Bounded cap. Matches StoryDirector._deferredInteractive cap (see StoryDirector.cs
// DeferredInteractiveCap). Prevents a pathological pile-up from growing unbounded
// in save files.
private const int PendingQueueCap = 32;
```

Note: `List<string>` is already registered in `EnlistedSaveDefiner.DefineContainerDefinitions` at line 102, so no save-definer change is needed for PR-b.

### Task B3: Implement SyncData to persist the pending IDs

- [ ] **Step 1: Replace the empty SyncData**

File: `src/Features/Content/EventDeliveryManager.cs:55-59`. Current body:

```csharp
public override void SyncData(IDataStore dataStore)
{
    // Event queue is transient - doesn't persist across saves
    // Events will be re-triggered by conditions when save is loaded
}
```

Replace with:

```csharp
public override void SyncData(IDataStore dataStore)
{
    if (dataStore.IsSaving)
    {
        // Snapshot the current live queue into the serializable ID list.
        _pendingEventIds = _pendingEvents.Select(evt => evt?.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
        if (_pendingEventIds.Count > PendingQueueCap)
        {
            _pendingEventIds = _pendingEventIds.Take(PendingQueueCap).ToList();
        }
    }

    dataStore.SyncData("evt_delivery_pendingIds", ref _pendingEventIds);

    // Re-hydration happens lazily on the next tick via ResolvePendingFromIds — not
    // here, because EventCatalog load-order relative to SyncData is not guaranteed.
}
```

- [ ] **Step 2: Verify using directives include `System.Linq`**

Check the top of `EventDeliveryManager.cs` for `using System.Linq;`. It is present (line 3 per the file header). No new usings needed.

### Task B4: Add lazy re-hydration on first access after load

- [ ] **Step 1: Add a re-hydration method**

File: `src/Features/Content/EventDeliveryManager.cs`, near the existing private helpers (below `TryDeliverNextEvent`). Add:

```csharp
// True until the first call after load has drained _pendingEventIds into _pendingEvents.
private bool _needsHydration = true;

private void HydrateFromPendingIdsIfNeeded()
{
    if (!_needsHydration)
    {
        return;
    }
    _needsHydration = false;

    if (_pendingEventIds == null || _pendingEventIds.Count == 0)
    {
        return;
    }

    int resolved = 0;
    int dropped = 0;
    foreach (var id in _pendingEventIds)
    {
        // EventCatalog.GetEvent is static and self-initializing (see Task B1).
        var evt = EventCatalog.GetEvent(id);
        if (evt != null)
        {
            _pendingEvents.Enqueue(evt);
            resolved++;
        }
        else
        {
            dropped++;
        }
    }

    if (dropped > 0)
    {
        ModLogger.Expected(LogCategory, "queue_hydrate_dropped",
            $"Dropped {dropped} pending event id(s) on load — not found in catalog (removed/renamed in content?)");
    }

    ModLogger.Info(LogCategory, $"Hydrated {resolved} pending event(s) from save");
    _pendingEventIds.Clear();  // Live queue is authoritative from here.
}
```

- [ ] **Step 2: Call HydrateFromPendingIdsIfNeeded at the top of QueueEvent and TryDeliverNextEvent**

File: `src/Features/Content/EventDeliveryManager.cs`.

In `QueueEvent` (line 65), as the first statement of the method body:

```csharp
public void QueueEvent(EventDefinition evt)
{
    HydrateFromPendingIdsIfNeeded();   // <-- Add this as the first line
    if (evt == null)
    {
        ModLogger.Expected(LogCategory, "event_queue_null", "Attempted to queue null event - check event ID and catalog loading");
        return;
    }
    // ... rest unchanged
}
```

In `TryDeliverNextEvent` (line 87), as the first statement:

```csharp
private void TryDeliverNextEvent()
{
    HydrateFromPendingIdsIfNeeded();   // <-- Add this as the first line
    if (_isShowingEvent)
    {
        // ... rest unchanged
    }
}
```

### Task B5: Enforce the bounded cap in QueueEvent

- [ ] **Step 1: Update QueueEvent's enqueue path**

In `src/Features/Content/EventDeliveryManager.cs:73`, the current enqueue is:

```csharp
_pendingEvents.Enqueue(evt);
```

Wrap with the cap enforcement:

```csharp
if (_pendingEvents.Count >= PendingQueueCap)
{
    // Drop the oldest to keep the queue bounded. Preserves the "newest event is most
    // relevant to current gameplay" heuristic while preventing runaway growth in save.
    var dropped = _pendingEvents.Dequeue();
    ModLogger.Expected(LogCategory, "queue_cap_exceeded",
        $"Queue at cap ({PendingQueueCap}); dropped oldest event {dropped?.Id ?? "unknown"} to make room for {evt.Id}");
}
_pendingEvents.Enqueue(evt);
```

### Task B6: Add debug commands to DebugToolsBehavior for verification

- [ ] **Step 1: Read the existing DebugToolsBehavior to understand its command-registration pattern**

Open `src/Debugging/Behaviors/DebugToolsBehavior.cs`. Locate the existing command at line 141 (the direct `EventDeliveryManager.Instance.QueueEvent` bypass call referenced in the audit). Note the registration pattern (typically `GameMenu.AddOption` or a keybinding / chat command).

- [ ] **Step 2: Add `debug_queue_test_event` command**

Add a new command that queues a known-safe test event by ID. The exact command mechanism must match the existing pattern from Step 1. Command body:

```csharp
// Debug command: queue a test event to verify persistence across save/load.
// Usage: trigger this command, save, reload the save, run debug_print_queue.
public void DebugQueueTestEvent()
{
    // Use an event known to exist in the shipping catalog.
    // evt_quiet_letter_from_home is from events_quiet_stretch.json, no preconditions.
    const string testId = "evt_quiet_letter_from_home";
    var evt = EventCatalog.GetEvent(testId);
    if (evt == null)
    {
        ModLogger.Info("DEBUG", $"Test event id '{testId}' not in catalog; confirm events_quiet_stretch.json is loaded and retry");
        return;
    }

    EventDeliveryManager.Instance?.QueueEvent(evt);
    ModLogger.Info("DEBUG", $"Queued test event {testId}. Save now, reload, then run debug_print_queue.");
}
```

- [ ] **Step 3: Add `debug_print_queue` command**

Add a second command that prints the current queue contents to the session log:

```csharp
// Debug command: dump the current EventDeliveryManager pending queue to the session
// log so we can verify persistence worked after reload.
public void DebugPrintQueue()
{
    var mgr = EventDeliveryManager.Instance;
    if (mgr == null)
    {
        ModLogger.Info("DEBUG", "EventDeliveryManager.Instance is null");
        return;
    }

    // Access internal queue count via a new read-only accessor added below.
    var count = mgr.PendingQueueCountForDebug;
    var ids = mgr.PendingQueueIdsForDebug;
    ModLogger.Info("DEBUG", $"PendingQueue count={count}, ids=[{string.Join(", ", ids)}]");
}
```

- [ ] **Step 4: Expose read-only queue accessors on EventDeliveryManager**

File: `src/Features/Content/EventDeliveryManager.cs`, near the `Instance` property (line 39). Add:

```csharp
// Debug-only accessors used by DebugToolsBehavior for save/reload verification.
// Do not consume these from gameplay code — use the normal event flow instead.
internal int PendingQueueCountForDebug => _pendingEvents.Count;
internal IEnumerable<string> PendingQueueIdsForDebug => _pendingEvents.Select(e => e?.Id ?? "(null)");
```

### Task B7: Build and CRLF normalize

- [ ] **Step 1: Build**

Run: `dotnet build Enlisted.csproj -c 'Enlisted RETAIL' -p:Platform=x64`

Expected: Build succeeds. Common failures:
- `The name 'EventCatalog' does not exist in the current context` → add `using Enlisted.Features.Content;` to the top of `DebugToolsBehavior.cs`.
- `LogCategory is not defined` in the hydration method → the new code uses the existing `LogCategory` constant at `EventDeliveryManager.cs:37`; confirm it's still in scope.

- [ ] **Step 2: Normalize CRLF on the two modified files**

Run:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/EventDeliveryManager.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Debugging/Behaviors/DebugToolsBehavior.cs
```

### Task B8: In-game save/reload smoke test

- [ ] **Step 1: Close BannerlordLauncher if running**

The launcher holds `Enlisted.dll` open; launching the game separately is fine, but the launcher itself must be closed during builds.

- [ ] **Step 2: Launch Bannerlord from Steam, load any enlisted save**

Confirm the player is enlisted (`EnlistmentBehavior.Instance.IsEnlisted == true`). If not, use an existing enlisted-state save or enlist before starting.

- [ ] **Step 3: Trigger debug_queue_test_event**

Via the same trigger mechanism the existing DebugToolsBehavior uses (Task B6 Step 1 clarified this). The session log should contain:

```
[INFO] [DEBUG] Queued test event evt_quiet_letter_from_home. Save now, reload, then run debug_print_queue.
```

**Immediately save** the campaign (main menu → Save → new slot, call it `prereq_b_test`). Do **not** let the modal deliver first — save before it appears if possible, or dismiss it and the queue still advances on the save-immediately path.

Actually, to test cleanly: queue the event, then before the UI renders it (the queue-cap logic runs after a one-frame delay), pause the game via ESC, save, reload.

- [ ] **Step 4: Reload the save**

Main menu → Load `prereq_b_test`.

- [ ] **Step 5: Trigger debug_print_queue**

Check the session log at `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\Session-*.log`.

**Expected output:**

```
[INFO] [DEBUG] PendingQueue count=1, ids=[evt_quiet_letter_from_home]
```

(plus a prior `Hydrated 1 pending event(s) from save` from the hydration hook)

If count is 0, persistence failed — revisit Tasks B3–B5.

- [ ] **Step 6: Run content validator**

Run: `python Tools/Validation/validate_content.py`

Expected: All phases pass.

### Task B9: Commit PR-b

- [ ] **Step 1: Stage and commit**

Run:

```bash
git add src/Features/Content/EventDeliveryManager.cs src/Debugging/Behaviors/DebugToolsBehavior.cs
git commit -m "fix(content): persist EventDeliveryManager pending queue across save/load

Closes the save-reload gap where StoryDirector would consume modal pacing
floors but the associated event could be lost if a save intervened between
queue-add and UI delivery. Queue now serializes event Ids (catalog is
authoritative) and re-hydrates lazily on first post-load tick. Bounded cap
of 32 matches StoryDirector._deferredInteractive.

Adds two debug commands (debug_queue_test_event, debug_print_queue) for
manual save/reload verification.

Spec: docs/superpowers/specs/2026-04-19-event-meaning-design.md §5.2"
```

Expected: Two-file commit on `development`.

---

## Phase C — PR-c: DispatchItem typed fields + consumer migration

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs:5809-5930` (DispatchItem struct extension)
- Modify: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs:5227-5248` (AddPersonalDispatch overload)
- Modify: `src/Features/Content/StoryDirector.cs:308-331` (WriteDispatchItem populates new fields)
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:1557-1575` (migrate Severity<2 to IsHeadline)
- Modify: `src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:3691-3697` (migrate substring match to typed Beats)
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs:94-108` (register HashSet<StoryBeat>)
- Reference: `docs/superpowers/specs/2026-04-19-event-meaning-design.md §5.3 / §7.1 / §7.2 / §7.3`

Replaces `Severity >= 2` magic-number filtering and `HeadlineKey.Contains("battle")` substring matching with typed reads off `DispatchItem`. `StoryCandidate.RenderedBody` stops being dead weight.

### Task C1: Extend DispatchItem struct with typed fields

- [ ] **Step 1: Locate the existing DispatchItem struct**

File: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs:5809`. Current definition is a `struct DispatchItem : IEquatable<DispatchItem>` with fields: `DayCreated`, `Category`, `HeadlineKey`, `PlaceholderValues`, `StoryKey`, `Type`, `Confidence`, `MinDisplayDays`, `FirstShownDay`, `Severity`.

- [ ] **Step 2: Add three new fields after Severity (line ~5863)**

Insert before the `Equals` method (line 5865):

```csharp
/// <summary>
/// Authoritative tier for this dispatch. Replaces the magic-number read of Severity
/// for "is this a headline?" queries. Set by StoryDirector.WriteDispatchItem.
/// 0 (Log) is the default for dispatches created through legacy paths that don't
/// yet set this field; treat those as non-headline.
/// </summary>
public Enlisted.Features.Content.StoryTier Tier { get; set; }

/// <summary>
/// World-beat set the originating StoryCandidate carried. Downstream consumers
/// (muster summaries, news headline classifiers) read this instead of substring-
/// matching HeadlineKey or Category. Empty or null means "unknown beats," which
/// preserves backward-compatibility with dispatches created before PR-c.
/// </summary>
public HashSet<Enlisted.Features.Content.StoryBeat> Beats { get; set; }

/// <summary>
/// Rendered body text from the originating StoryCandidate.RenderedBody. Previously
/// dropped on route; PR-c carries it through so UI consumers can render full
/// observational content when a HeadlineKey lookup isn't sufficient.
/// </summary>
public string Body { get; set; }

/// <summary>
/// Semantic predicate replacing the Severity >= 2 magic number used by menu code
/// to filter for "headline" items. Source of truth for "is this a headline?" in
/// PR-c and beyond.
/// </summary>
public bool IsHeadline => Tier == Enlisted.Features.Content.StoryTier.Headline;
```

- [ ] **Step 3: Update Equals to include the new fields**

File: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs:5865-5876`. Current `Equals`:

```csharp
public bool Equals(DispatchItem other)
{
    return DayCreated == other.DayCreated &&
           Category == other.Category &&
           HeadlineKey == other.HeadlineKey &&
           StoryKey == other.StoryKey &&
           Type == other.Type &&
           Confidence == other.Confidence &&
           MinDisplayDays == other.MinDisplayDays &&
           FirstShownDay == other.FirstShownDay &&
           Severity == other.Severity;
}
```

Replace with:

```csharp
public bool Equals(DispatchItem other)
{
    return DayCreated == other.DayCreated &&
           Category == other.Category &&
           HeadlineKey == other.HeadlineKey &&
           StoryKey == other.StoryKey &&
           Type == other.Type &&
           Confidence == other.Confidence &&
           MinDisplayDays == other.MinDisplayDays &&
           FirstShownDay == other.FirstShownDay &&
           Severity == other.Severity &&
           Tier == other.Tier &&
           Body == other.Body &&
           BeatsEqual(Beats, other.Beats);
}

private static bool BeatsEqual(HashSet<Enlisted.Features.Content.StoryBeat> a, HashSet<Enlisted.Features.Content.StoryBeat> b)
{
    if (a == null && b == null) { return true; }
    if (a == null || b == null) { return false; }
    return a.SetEquals(b);
}
```

- [ ] **Step 4: Update GetHashCode to include Tier and Body**

File: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs:5883` (GetHashCode body). Locate the existing unchecked hash block. Add to it:

```csharp
hash = hash * 31 + (int)Tier;
hash = hash * 31 + (Body?.GetHashCode() ?? 0);
// Beats intentionally excluded from hash — HashSet ordering is not stable and
// SetEquals already covers equality. Hash collisions for same non-Beats fields
// are acceptable.
```

(Do not include `Beats` in the hash — it's a reference-type set whose hash is not stable and the equality check handles it.)

### Task C2: Register HashSet<StoryBeat> in EnlistedSaveDefiner

- [ ] **Step 1: Open the save definer**

File: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`. Locate the container definitions block (lines 94–108).

- [ ] **Step 2: Add the HashSet<StoryBeat> registration**

Insert after line 107 (`ConstructContainerDefinition(typeof(System.Collections.Generic.List<Enlisted.Features.Content.StoryCandidatePersistent>));`):

```csharp
// DispatchItem.Beats — typed beat set replacing substring matches on HeadlineKey.
// See docs/superpowers/specs/2026-04-19-event-meaning-design.md §5.3.
ConstructContainerDefinition(typeof(HashSet<Enlisted.Features.Content.StoryBeat>));
```

Note: `StoryBeat` is already registered as an enum at line 86 (id 81). The container registration is new.

### Task C3: Add AddPersonalDispatch overload accepting new fields

- [ ] **Step 1: Locate the existing public entry point**

File: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs:5227`. Existing signature:

```csharp
public void AddPersonalDispatch(
    string category,
    string headlineKey,
    Dictionary<string, string> placeholderValues,
    string storyKey,
    int severity,
    int minDisplayDays)
```

- [ ] **Step 2: Add a new overload that carries Tier, Beats, and Body**

Immediately after the existing method (before the `#endregion` at ~5250), add:

```csharp
/// <summary>
/// Tier/Beats/Body-aware entry point. Used by StoryDirector so downstream consumers
/// (muster, menu headline filter) can read typed fields instead of magic numbers or
/// substring matches. Forwards to AddPersonalNews and attaches the new fields to
/// the DispatchItem written to the feed.
/// See docs/superpowers/specs/2026-04-19-event-meaning-design.md §7.
/// </summary>
public void AddPersonalDispatch(
    string category,
    string headlineKey,
    Dictionary<string, string> placeholderValues,
    string storyKey,
    int severity,
    int minDisplayDays,
    Enlisted.Features.Content.StoryTier tier,
    HashSet<Enlisted.Features.Content.StoryBeat> beats,
    string body)
{
    if (string.IsNullOrEmpty(headlineKey))
    {
        return;
    }

    try
    {
        AddPersonalNews(category, headlineKey, placeholderValues, storyKey, minDisplayDays, severity, tier, beats, body);
    }
    catch (Exception ex)
    {
        ModLogger.Caught("NEWS", "AddPersonalDispatch (typed) failed", ex);
    }
}
```

- [ ] **Step 3: Keep the original overload as a forwarder**

The existing 6-parameter overload already exists. Update it to forward to the new typed one with null/default typed fields so existing callers continue to work:

```csharp
public void AddPersonalDispatch(
    string category,
    string headlineKey,
    Dictionary<string, string> placeholderValues,
    string storyKey,
    int severity,
    int minDisplayDays)
{
    AddPersonalDispatch(category, headlineKey, placeholderValues, storyKey, severity, minDisplayDays,
        tier: Enlisted.Features.Content.StoryTier.Log,
        beats: null,
        body: null);
}
```

### Task C4: Thread new fields through AddPersonalNews

- [ ] **Step 1: Locate the private `AddPersonalNews` method**

Find it by searching `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs` for the signature `private.*AddPersonalNews`. Note its current parameter list.

- [ ] **Step 2: Extend AddPersonalNews with optional typed parameters**

Add three new parameters to the end of its signature (with default values so any other callers continue to work):

```csharp
private void AddPersonalNews(
    string category,
    string headlineKey,
    Dictionary<string, string> placeholderValues,
    string storyKey,
    int minDisplayDays,
    int severity,
    Enlisted.Features.Content.StoryTier tier = Enlisted.Features.Content.StoryTier.Log,
    HashSet<Enlisted.Features.Content.StoryBeat> beats = null,
    string body = null)
```

- [ ] **Step 3: Populate new DispatchItem fields at the construction site**

Inside `AddPersonalNews`, find where the `DispatchItem` is constructed and appended to the feed. Add the three new fields to the initializer:

```csharp
var item = new DispatchItem
{
    DayCreated = today,
    Category = category,
    HeadlineKey = headlineKey,
    PlaceholderValues = placeholderValues,
    StoryKey = storyKey,
    Type = /* existing */,
    Confidence = /* existing */,
    MinDisplayDays = minDisplayDays,
    FirstShownDay = -1,
    Severity = severity,
    Tier = tier,
    Beats = beats,
    Body = body,
};
```

Leave the existing fields unchanged — only add the three new initializer entries.

### Task C5: Update StoryDirector.WriteDispatchItem to pass typed fields

- [ ] **Step 1: Locate WriteDispatchItem**

File: `src/Features/Content/StoryDirector.cs:308`. Current body:

```csharp
private static void WriteDispatchItem(StoryCandidate c, StoryTier tier)
{
    var news = Campaign.Current?.GetCampaignBehavior<Enlisted.Features.Interface.Behaviors.EnlistedNewsBehavior>();
    if (news == null) { return; }

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
        category: c.DispatchCategory ?? DefaultDispatchCategory,
        headlineKey: c.RenderedTitle,
        placeholderValues: null,
        storyKey: c.StoryKey,
        severity: severity,
        minDisplayDays: c.MinDisplayDays);
}
```

- [ ] **Step 2: Switch the call site to the new typed overload**

Replace the `news.AddPersonalDispatch(...)` call with:

```csharp
news.AddPersonalDispatch(
    category: c.DispatchCategory ?? DefaultDispatchCategory,
    headlineKey: c.RenderedTitle,
    placeholderValues: null,
    storyKey: c.StoryKey,
    severity: severity,
    minDisplayDays: c.MinDisplayDays,
    tier: tier,
    beats: c.Beats != null ? new HashSet<Enlisted.Features.Content.StoryBeat>(c.Beats) : null,
    body: c.RenderedBody);
```

Note: we defensively copy `c.Beats` into a fresh `HashSet` because the candidate's set may be mutated elsewhere; the DispatchItem should own its own snapshot.

### Task C6: Migrate EnlistedMenuBehavior's headline filter

- [ ] **Step 1: Locate GetUnreadHighSeverity**

File: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:1557-1570`. Current body:

```csharp
private List<DispatchItem> GetUnreadHighSeverity(EnlistedNewsBehavior news)
{
    if (news == null) { return new List<DispatchItem>(); }
    int today = (int)CampaignTime.Now.ToDays;
    var items = news.GetPersonalFeedSince(today - 7);
    var result = new List<DispatchItem>();
    foreach (var it in items)
    {
        if (it.Severity < 2) { continue; }
        if (!string.IsNullOrEmpty(it.StoryKey) && _viewedHeadlineStoryKeys.Contains(it.StoryKey)) { continue; }
        result.Add(it);
    }
    return result;
}
```

- [ ] **Step 2: Migrate the severity predicate to IsHeadline**

Change the filter line from `if (it.Severity < 2) { continue; }` to:

```csharp
// Prefer the typed predicate when the dispatch carries a Tier (i.e. was written
// via the StoryDirector path post-PR-c). Fall back to the legacy Severity check
// for dispatches created through older code paths that don't set Tier.
bool isHeadline = it.Tier != Enlisted.Features.Content.StoryTier.Log
    ? it.IsHeadline
    : it.Severity >= 2;
if (!isHeadline) { continue; }
```

This keeps the method compiling for mixed-age dispatch items in a save loaded from pre-PR-c.

### Task C7: Migrate MusterMenuHandler's battle count

- [ ] **Step 1: Locate CountBattlesThisPeriod**

File: `src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:3675-3697`. Current body ends with:

```csharp
// Count battle-related feed items
return feedItems.Count(item =>
    item.Category?.Contains("battle") == true ||
    item.HeadlineKey?.ToLowerInvariant().Contains("battle") == true ||
    item.HeadlineKey?.ToLowerInvariant().Contains("victory") == true ||
    item.HeadlineKey?.ToLowerInvariant().Contains("defeat") == true);
```

- [ ] **Step 2: Replace with typed Beats filter + legacy fallback**

```csharp
// Typed read: feed items created post-PR-c carry the originating StoryBeats. We
// count anything whose beats include a battle beat. For legacy items (Beats null)
// we fall back to the substring match so mixed-age save data still works.
return feedItems.Count(item =>
{
    if (item.Beats != null && item.Beats.Count > 0)
    {
        return item.Beats.Contains(Enlisted.Features.Content.StoryBeat.LordMajorBattleEnd)
            || item.Beats.Contains(Enlisted.Features.Content.StoryBeat.PlayerBattleEnd);
    }

    // Legacy fallback for pre-PR-c dispatches.
    return item.Category?.Contains("battle") == true
        || item.HeadlineKey?.ToLowerInvariant().Contains("battle") == true
        || item.HeadlineKey?.ToLowerInvariant().Contains("victory") == true
        || item.HeadlineKey?.ToLowerInvariant().Contains("defeat") == true;
});
```

- [ ] **Step 3: Verify `using` directives**

Confirm the top of `MusterMenuHandler.cs` has (or add):

```csharp
using Enlisted.Features.Content;
```

If adding, place it alphabetically among the existing Enlisted namespace usings.

### Task C8: Build and validator

- [ ] **Step 1: Build**

Run: `dotnet build Enlisted.csproj -c 'Enlisted RETAIL' -p:Platform=x64`

Expected: Build succeeds. Common failures:
- `Cannot convert from 'StoryTier' to 'int'` → an enum field needs `(int)` cast (check Task C1 Step 4 hash computation)
- `StoryTier is inaccessible` → verify fully-qualified name `Enlisted.Features.Content.StoryTier`

- [ ] **Step 2: Content validator**

Run: `python Tools/Validation/validate_content.py`

Expected: All phases pass. PR-c does not change any JSON schema, so validator output should match pre-PR-c baseline.

- [ ] **Step 3: Normalize CRLF**

Run:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Enlistment/Behaviors/MusterMenuHandler.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/StoryDirector.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
```

### Task C9: In-game smoke test — headline filter

- [ ] **Step 1: Launch Bannerlord, load an enlisted save**

- [ ] **Step 2: Trigger a known Headline beat**

Wait for an in-world Headline-tier beat (war declaration, army formed, siege begin) — these are the Headline-eligible beats per spec §6.2. If none fires naturally in ~5 in-game days, use DebugToolsBehavior to force-queue a test candidate at Headline tier if that capability exists; otherwise save-scumming works (save before a war declaration, reload).

- [ ] **Step 3: Open the enlisted camp menu**

Check the "unread headlines" section. Verify the item appears with its existing formatting.

- [ ] **Step 4: Inspect the session log**

Expected log entries (non-exhaustive):
- No `ModLogger.Caught` entries from `AddPersonalDispatch (typed)`
- No regression errors from `GetUnreadHighSeverity` or muster-menu paths

### Task C10: In-game smoke test — muster battle count

- [ ] **Step 1: Trigger a battle via normal gameplay or debug**

Engage in a battle, end the battle (win or lose).

- [ ] **Step 2: Wait for the next muster (12-day cycle)**

Or use debug-advance-time if that capability exists in DebugToolsBehavior.

- [ ] **Step 3: Open the muster menu**

Verify the "battles this period" count reflects the actual battle you triggered (should be at least 1).

- [ ] **Step 4: Session log inspection**

Confirm no new `ModLogger.Caught` exceptions from the muster path.

### Task C11: Commit PR-c

- [ ] **Step 1: Stage and commit**

Run:

```bash
git add src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs src/Features/Enlistment/Behaviors/MusterMenuHandler.cs src/Features/Content/StoryDirector.cs src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
git commit -m "feat(content): add typed Tier/Beats/Body fields to DispatchItem

Replaces the Severity >= 2 magic-number headline filter in EnlistedMenuBehavior
and the HeadlineKey.Contains(battle|victory|defeat) substring filter in
MusterMenuHandler with typed reads off the dispatch item.

DispatchItem gains Tier (StoryTier), Beats (HashSet<StoryBeat>), Body (string),
and an IsHeadline semantic predicate. StoryDirector.WriteDispatchItem populates
them at route time so downstream consumers no longer fan out through string
heuristics. Legacy dispatch items (pre-PR-c) fall back to the old predicates
so mixed-age saves still work.

Spec: docs/superpowers/specs/2026-04-19-event-meaning-design.md §5.3 / §7.1 /
§7.2 / §7.3"
```

Expected: Five-file commit on `development`.

---

## Self-Review Checklist

After all three phases merge, verify:

- [ ] `git log --oneline development -3` shows three new commits (PR-a, PR-b, PR-c) in order.
- [ ] `dotnet build Enlisted.csproj -c 'Enlisted RETAIL' -p:Platform=x64` succeeds on a clean checkout of `development`.
- [ ] `python Tools/Validation/validate_content.py` passes.
- [ ] An enlisted save created before PR-c still loads and the menu/muster UI behaves correctly (legacy fallbacks hold).
- [ ] An enlisted save created after PR-c shows typed `Tier` / `Beats` / `Body` on new dispatch items (inspect via `debug_print_queue` or dedicated log statements added during Task C9).
- [ ] The `docs/superpowers/specs/2026-04-19-event-meaning-design.md` reference to prerequisite PRs (spec §5.1, §5.2, §5.3) points at three closed commits.

If all pass, the Event Meaning design's prerequisites are cleared and **Plan 1 (Foundations)** can be drafted in a separate plan document: `docs/superpowers/plans/2026-04-20-event-meaning-plan1-foundations.md` (or next-available date).

---

## What this plan does NOT cover (out of scope)

Per spec §16 and the scope-check at the top of this document, these are deferred to subsequent plans:

- **Plan 1 — Foundations:** context taxonomy, ContextDetector, VariantSelector, StateReader, PlayerIdentity (reader + writer), `character_tag` parse/apply, state/trait variant axes, effect vocabulary validator, the 557 → ~100 cull. See spec §14.2.
- **Plan 2 — Lord Memory:** LordMemoryStore service, marker vocabulary, `write_lord_memory` option effect, retrofit. See spec §14.3.
- **Plan 3 — Consequence Arcs:** ArcTracker (singleton + instanced), 12-arc catalog, `grantGear` effect, arc-act gate in Director, retire `EscalationManager` free-firing path. See spec §14.4.
- **`BuildEventHeadline` at `EnlistedNewsBehavior.cs:3629`** — operates on `EventOutcomeRecord`, a different data model from `DispatchItem`. Its `eventId.Contains("dice"|"training"|"hunt"|"lend")` substring matching is a legitimate smell but requires adding a typed `EventKind` field to `EventOutcomeRecord` and populating it at every write site. Deferred to Plan 1's cull pass when surviving events get typed during authoring.
