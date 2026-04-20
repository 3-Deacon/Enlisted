# Enlisted Home Surface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Spec 1 — the enlisted-home surface. When the lord's party parks at a friendly town/castle/village, run a four-phase `HomeActivity` (Arrive / Settle / Evening / Break Camp) with five player-choice intents (`unwind` / `train_hard` / `brood` / `commune` / `scheme`). Content is data-driven via storylets; triggers read live engine state; the Evening-phase intents surface through a preallocated 5-slot menu bank that mirrors the existing decisions-slot pattern.

**Architecture:** Two generic primitives unblock all surface specs (1-5): `ActivityTypeCatalog` (loads `ModuleData/Enlisted/Activities/*.json`) and `StoryletEventAdapter` (builds `InquiryData`-backed `InteractiveEvent` from a `Storylet` so player-choice storylets can render as Modal events). `ActivityRuntime` gains the `PhaseDelivery.PlayerChoice` branch. `HomeActivity` is the first concrete `Activity` subclass (save offset 45). Detection is event-driven via `CampaignEvents.SettlementEntered` / `OnSettlementLeftEvent` with strict party-identity filtering. Trigger predicates read `MobileParty` / `Hero` / `Clan` / `Settlement` / `CampaignTime` APIs directly — no parallel mod fictions.

**Tech Stack:** C# 11 on .NET Framework 4.7.2, Bannerlord v1.3.13 (`TaleWorlds.CampaignSystem`, `TaleWorlds.CampaignSystem.Inquiries`, `TaleWorlds.SaveSystem`, `TaleWorlds.Localization`), Newtonsoft.Json. No unit-test framework — verification via `dotnet build` + `Tools/Validation/validate_content.py` + in-game `DebugToolsBehavior` smoke helpers.

**Spec:** [docs/superpowers/specs/2026-04-19-enlisted-home-surface-design.md](../specs/2026-04-19-enlisted-home-surface-design.md) (commits `133a147` + `46272c1` on `development`).

**Status:** Phase A (Tasks 1–5) complete 2026-04-19 on `development` (commits `fc93285` → `24b7e50`). Phase B (Tasks 6–9: HomeActivity + save registration + triggers + starter) complete 2026-04-20 on `development` (commits `e4b6806` → `a48983c`). Phase C (Tasks 10–12: menu integration + smoke) complete 2026-04-20 on `development` (commits `18b4021` → `19391a4`, includes two bug-fix commits for latent Phase B/0 issues discovered on first boot — `0ff3a2d` unsupported `HashSet<StoryBeat>` container, `19391a4` eager `DefaultTraits` lookup at `OnGameStart`). Phase D (Tasks 13–18: content authoring) complete 2026-04-20 on `development` (commits `8b96294` → `d9bcf8b`). Phase E (Tasks 19–21: debug/validator/acceptance) not yet started.

---

## Conventions used in every task

- **Commit hygiene.** Every task ends with a commit. Always `git add <specific paths>`; never `git add -A` (another AI session may be editing in parallel).
- **Commit messages.** Follow repo style `category(scope): short subject`; trailer `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Multi-line messages go through a temp file (bash here has no `cat` heredoc) and `git commit -F`.
- **CRLF normalization.** The Write tool creates LF-only files; `.gitattributes` enforces CRLF on `.cs / .csproj / .sln / .ps1`. For **every newly created** file in those extensions, run once (ONLY once — the script double-BOMs on repeat): `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path <path>`. For edits to existing `.cs` files, rely on `.gitattributes` at commit time.
- **csproj registration.** Every new `.cs` must be added to `Enlisted.csproj` as `<Compile Include="src\Features\..." />` — missing entries break the build.
- **TaleWorlds API source of truth.** All TaleWorlds API verification happens against `C:\Dev\Enlisted\Decompile\`. Never web search or Context7 for TaleWorlds.
- **ModLogger.** `ModLogger.Surfaced("CATEGORY", "summary string literal", ex, ctx?)` — category matches `[A-Z][A-Z0-9\-]*`, summary is a string literal (registry scanner). Defensive catch-and-log uses `Caught`; known-branch early exits use `Expected(category, key, summary, ctx?)`. Never use the retired `ModLogger.Error` — `validate_content.py` Phase 11 blocks it.
- **Bash on Windows.** Prepend PATH: `export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"`. Python is at `/c/Python313/python.exe`.
- **Build command.** `dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64` (quote the config — bash splits on the space). Close `BannerlordLauncher.exe` first — it holds the DLL open and the csproj post-build mirror fails with MSB3021.
- **Validator.** `/c/Python313/python.exe Tools/Validation/validate_content.py` — run after every content-layer change.

---

## File structure

### New C# files (6)

```
src/Features/Activities/
  ActivityTypeCatalog.cs              -- generic; loads Activities/*.json, calls ActivityRuntime.RegisterType
  Home/HomeActivity.cs                -- Activity subclass, save offset 45
  Home/HomeActivityStarter.cs         -- CampaignBehaviorBase; lifecycle detection with party-identity gate
  Home/HomeEveningMenuProvider.cs     -- slot-bank condition/consequence callbacks
  Home/HomeTriggers.cs                -- ~30 engine-state predicates

src/Features/Content/
  StoryletEventAdapter.cs             -- generic; Storylet -> InteractiveEvent (InquiryData-backed)
```

### Modified C# files (5)

```
src/Features/Activities/Phase.cs                     -- add int FireIntervalHours
src/Features/Activities/Activity.cs                  -- add virtual OnPlayerChoice + OnBeat
src/Features/Activities/ActivityRuntime.cs           -- implement PhaseDelivery.PlayerChoice branch
src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs  -- register 5-slot evening option bank
src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs       -- register HomeActivity at offset 45
Enlisted.csproj                                       -- register 6 new .cs files
```

### New ModuleData content (6)

```
ModuleData/Enlisted/Activities/home_activity.json         -- ActivityTypeDefinition with 4 phases, pool IDs
ModuleData/Enlisted/Storylets/home_arrive.json            -- 8 storylets
ModuleData/Enlisted/Storylets/home_settle.json            -- 12 storylets
ModuleData/Enlisted/Storylets/home_evening.json           -- 25 storylets (5 per intent)
ModuleData/Enlisted/Storylets/home_break_camp.json        -- 5 storylets
ModuleData/Enlisted/Localization/strings_home.xml         -- generated by sync_event_strings.py
```

### Modified tooling (2)

```
src/Debugging/Behaviors/DebugToolsBehavior.cs   -- add Ctrl+Shift+H/E/B/T smoke hotkeys
Tools/Validation/validate_content.py            -- Phase 12 extension for ActivityTypeDefinition + home_* refs
```

---

## Execution order

**Phase A (Tasks 1-5): Substrate extensions.** Must land before any HomeActivity code compiles.
**Phase B (Tasks 6-9): HomeActivity scaffolding + lifecycle.** Class + save + triggers + settlement-entry starter.
**Phase C (Tasks 10-12): Menu integration + smoke.** Wires the Evening slot-bank to live gameplay.
**Phase D (Tasks 13-18): Content authoring.** 50 storylets + localization sync.
**Phase E (Tasks 19-21): Debug helpers + validator + acceptance smoke.**

Each task is independent where possible. Phase order is strict; within a phase some tasks can parallelize (noted where applicable).

---

## Task 1: Extend `Phase` with `FireIntervalHours`

**Files:**
- Modify: `src/Features/Activities/Phase.cs`

- [x] **Step 1: Add the field**

Edit `src/Features/Activities/Phase.cs`. After the `DurationHours` property, add `FireIntervalHours`:

```csharp
public sealed class Phase
{
    public string Id { get; set; } = string.Empty;
    public int DurationHours { get; set; }
    public int FireIntervalHours { get; set; }               // 0 = fire once per phase; N = fire at most every N in-game hours
    public List<string> Pool { get; set; } = new List<string>();
    public Dictionary<string, float> IntentBias { get; set; } = new Dictionary<string, float>();
    public PhaseDelivery Delivery { get; set; } = PhaseDelivery.Auto;
    public int PlayerChoiceCount { get; set; } = 4;
}
```

- [x] **Step 2: Confirm JSON loader picks it up (likely a no-op)**

Grep `src/Features/Activities/ActivityTypeDefinition.cs` for `duration_hours` or `JObject`-based phase parsing.

**If phase JSON parsing exists there** (unlikely — checked during plan-writing and it was a plain POCO): add `FireIntervalHours = (int?)phaseObj["fire_interval_hours"] ?? 0,` inside the existing phase parse loop.

**If phase JSON parsing does NOT exist there** (expected case): leave the file untouched. The `fire_interval_hours` → `Phase.FireIntervalHours` wire-up lives in Task 3's new `ActivityTypeCatalog` JSON loader (Task 3 Step 1 already includes that line). No action required here.

- [x] **Step 3: Build — must pass**

Run:

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`.

- [x] **Step 4: Commit**

```bash
git add src/Features/Activities/Phase.cs src/Features/Activities/ActivityTypeDefinition.cs
git commit -F /c/Users/coola/commit.txt
# commit.txt:
# feat(activities): Phase.FireIntervalHours for Spec 1 pacing
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 2: Add `Activity.OnPlayerChoice` + `Activity.OnBeat` virtual hooks

**Files:**
- Modify: `src/Features/Activities/Activity.cs`

- [x] **Step 1: Add virtual methods with empty defaults**

Edit `src/Features/Activities/Activity.cs`. After the abstract method declarations (just below `Finish(...)`), add:

```csharp
public abstract void Finish(ActivityEndReason reason);

/// Called by ActivityRuntime.ResolvePlayerChoice after the player selects
/// an option in a PlayerChoice-delivery phase. Default no-op.
public virtual void OnPlayerChoice(string optionId, ActivityContext ctx) { }

/// Called by starter behaviors to signal a campaign beat relevant to this
/// activity (e.g. "departure_imminent"). Default no-op.
public virtual void OnBeat(string beatId, ActivityContext ctx) { }
```

- [x] **Step 2: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`.

- [x] **Step 3: Commit**

```bash
git add src/Features/Activities/Activity.cs
git commit -F /c/Users/coola/commit.txt
# feat(activities): Activity.OnPlayerChoice + OnBeat virtual hooks
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 3: Create `ActivityTypeCatalog` (JSON loader)

**Files:**
- Create: `src/Features/Activities/ActivityTypeCatalog.cs`
- Modify: `Enlisted.csproj` (register new file)
- Modify: `src/Features/Activities/ActivityRuntime.cs` (call `LoadAll()` from `OnSessionLaunched`)

- [x] **Step 1: Write the catalog loader**

Create `src/Features/Activities/ActivityTypeCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;

namespace Enlisted.Features.Activities
{
    /// <summary>
    /// Loads ActivityTypeDefinition JSON from ModuleData/Enlisted/Activities/*.json
    /// and registers each with ActivityRuntime. Spec 0 leaves _types empty by design;
    /// surface specs (1-5) drop files here, and this loader wires them up at session launch.
    /// </summary>
    public static class ActivityTypeCatalog
    {
        public static void LoadAll()
        {
            var runtime = ActivityRuntime.Instance;
            if (runtime == null)
            {
                ModLogger.Expected("ACTIVITY", "catalog_no_runtime",
                    "ActivityRuntime.Instance null at LoadAll — order-of-boot issue");
                return;
            }

            var dir = ModulePaths.GetContentPath("Activities");
            if (!Directory.Exists(dir))
            {
                ModLogger.Expected("ACTIVITY", "no_activities_dir",
                    "Activities directory not found: " + dir);
                return;
            }

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);
                    var def = Parse(root);
                    if (def == null || string.IsNullOrEmpty(def.Id))
                    {
                        ModLogger.Expected("ACTIVITY", "bad_activity_file",
                            "Activity JSON missing id: " + file);
                        continue;
                    }
                    runtime.RegisterType(def);
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("ACTIVITY", "Failed to load activity type",
                        ex, new Dictionary<string, object> { ["file"] = file });
                }
            }
        }

        private static ActivityTypeDefinition Parse(JObject root)
        {
            if (root == null) { return null; }
            var def = new ActivityTypeDefinition
            {
                Id = (string)root["id"] ?? string.Empty,
                Phases = new List<Phase>()
            };
            var phases = root["phases"] as JArray;
            if (phases == null) { return def; }
            foreach (var p in phases.OfType<JObject>())
            {
                def.Phases.Add(new Phase
                {
                    Id = (string)p["id"] ?? string.Empty,
                    DurationHours = (int?)p["duration_hours"] ?? 0,
                    FireIntervalHours = (int?)p["fire_interval_hours"] ?? 0,
                    Pool = (p["pool"] as JArray)?.Select(t => (string)t).Where(s => !string.IsNullOrEmpty(s)).ToList()
                           ?? new List<string>(),
                    IntentBias = ParseIntentBias(p["intent_bias"] as JObject),
                    Delivery = ParseDelivery((string)p["delivery"]),
                    PlayerChoiceCount = (int?)p["player_choice_count"] ?? 4
                });
            }
            return def;
        }

        private static Dictionary<string, float> ParseIntentBias(JObject obj)
        {
            var dict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (obj == null) { return dict; }
            foreach (var prop in obj.Properties())
            {
                dict[prop.Name] = (float?)prop.Value ?? 1.0f;
            }
            return dict;
        }

        private static PhaseDelivery ParseDelivery(string s)
        {
            if (string.IsNullOrEmpty(s)) { return PhaseDelivery.Auto; }
            return string.Equals(s, "player_choice", StringComparison.OrdinalIgnoreCase)
                ? PhaseDelivery.PlayerChoice
                : PhaseDelivery.Auto;
        }
    }
}
```

- [x] **Step 2: Register the new file in `Enlisted.csproj`**

Open `Enlisted.csproj`. Locate the `<ItemGroup>` containing `<Compile Include="src\Features\Activities\ActivityRuntime.cs" />`. Add alongside (alphabetical order or nearby):

```xml
<Compile Include="src\Features\Activities\ActivityTypeCatalog.cs" />
```

- [x] **Step 3: Call `LoadAll()` from `ActivityRuntime.OnSessionLaunched`**

Edit `src/Features/Activities/ActivityRuntime.cs`. Locate `OnSessionLaunched`:

```csharp
private void OnSessionLaunched(CampaignGameStarter starter)
{
    Instance = this;
    // ActivityTypeDefinition JSON loading is a surface-spec concern. Spec 0 leaves _types empty.
}
```

Replace the comment with the call:

```csharp
private void OnSessionLaunched(CampaignGameStarter starter)
{
    Instance = this;
    ActivityTypeCatalog.LoadAll();
}
```

- [x] **Step 4: Normalize CRLF for the new file**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/ActivityTypeCatalog.cs
```

- [x] **Step 5: Build**

```bash
export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`.

- [x] **Step 6: Commit**

```bash
git add src/Features/Activities/ActivityTypeCatalog.cs src/Features/Activities/ActivityRuntime.cs Enlisted.csproj
git commit -F /c/Users/coola/commit.txt
# feat(activities): ActivityTypeCatalog JSON loader for surface specs
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 4: Create `StoryletEventAdapter` (Storylet → InteractiveEvent)

**Files:**
- Create: `src/Features/Content/StoryletEventAdapter.cs`
- Modify: `Enlisted.csproj`

- [x] **Step 1: Verify the `InteractiveEvent` wrapper constructor signature**

Grep for the mod's `InteractiveEvent` class:

Search for `class InteractiveEvent` in `src/`. Open the file. Note the constructor signature — the adapter must match it. Common shapes:
- `new InteractiveEvent(string id, string titleText, string bodyText, InquiryData inquiry)`
- `new InteractiveEvent(InquiryData inquiry, string titleFallback, string setupFallback)`
- Other.

**Do not proceed until the signature is confirmed.** If the shape differs from the skeleton below, adapt the `return new InteractiveEvent(...)` line accordingly.

- [x] **Step 2: Write the adapter**

Create `src/Features/Content/StoryletEventAdapter.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Activities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Builds an InteractiveEvent (InquiryData-backed Modal) from a Storylet so that
    /// player-choice storylets can render as Modal events with buttons, effects, and chains.
    /// Spec 0 Storylet.ToCandidate only populates title/body — this adapter fills InteractiveEvent
    /// so StoryDirector.Route takes the Modal path (StoryDirector.cs:218).
    /// </summary>
    public static class StoryletEventAdapter
    {
        /// <summary>
        /// Build a Modal event from a Storylet. Applies choice.Effects on selection,
        /// parks choice.Chain via ActivityRuntime.ParkChain (fires on next phase-entry
        /// match; no real-time timer).
        /// </summary>
        public static InteractiveEvent BuildModal(Storylet s, StoryletContext ctx, Activity owner)
        {
            if (s == null) { return null; }
            var eligibleChoices = (s.Options ?? new List<Choice>())
                .Where(c => c != null && TriggerRegistry.Evaluate(c.Trigger, ctx))
                .ToList();

            // Fallback: if no choice passes its option-level trigger, offer a single "continue"
            // so the modal doesn't render with zero buttons. Author-side rarely hits this.
            if (eligibleChoices.Count == 0)
            {
                eligibleChoices.Add(new Choice { Id = "continue", Text = "{=home_continue}Continue.", Tooltip = "" });
            }

            var elements = eligibleChoices
                .Select(c => new InquiryElement(c, c.Text ?? "", null, true, c.Tooltip ?? ""))
                .ToList();

            var inquiry = new MultiSelectionInquiryData(
                titleText: s.Title ?? "",
                descriptionText: s.Setup ?? "",
                inquiryElements: elements,
                isExitShown: false,
                minSelectableOptionCount: 1,
                maxSelectableOptionCount: 1,
                affirmativeText: new TextObject("{=home_confirm}OK").ToString(),
                negativeText: "",
                affirmativeAction: selected =>
                {
                    if (selected == null || selected.Count == 0) { return; }
                    if (!(selected[0].Identifier is Choice chosen)) { return; }
                    try
                    {
                        EffectExecutor.Apply(chosen.Effects, ctx);
                        ParkChainIfAny(chosen, owner);
                    }
                    catch (System.Exception ex)
                    {
                        ModLogger.Surfaced("STORYLET-ADAPT", "Affirmative action failed", ex);
                    }
                },
                negativeAction: null
            );

            // Construct the mod's InteractiveEvent wrapper. Signature verified in Step 1.
            return new InteractiveEvent(inquiry, s.Title ?? "", s.Setup ?? "");
        }

        private static void ParkChainIfAny(Choice chosen, Activity owner)
        {
            if (chosen?.Chain == null || string.IsNullOrEmpty(chosen.Chain.Id)) { return; }
            var (typeId, phaseId) = ParseWhen(
                chosen.Chain.When,
                fallbackTypeId: owner?.TypeId ?? string.Empty,
                fallbackPhaseId: owner?.CurrentPhase?.Id ?? string.Empty);
            if (string.IsNullOrEmpty(typeId) || string.IsNullOrEmpty(phaseId))
            {
                ModLogger.Expected("STORYLET-ADAPT", "chain_no_target",
                    "chain.when absent and owner activity had no type/phase; chain dropped");
                return;
            }
            ActivityRuntime.Instance?.ParkChain(typeId, phaseId, chosen.Chain.Id);
        }

        private static (string typeId, string phaseId) ParseWhen(
            string when, string fallbackTypeId, string fallbackPhaseId)
        {
            if (string.IsNullOrEmpty(when)) { return (fallbackTypeId, fallbackPhaseId); }
            var parts = when.Split('.');
            return parts.Length == 2 ? (parts[0], parts[1]) : (fallbackTypeId, fallbackPhaseId);
        }
    }
}
```

Note: the Step 1 signature verification is critical. If the mod's `InteractiveEvent` wrapper constructor differs, adapt the final `return new InteractiveEvent(...)` line before continuing.

- [x] **Step 3: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Content\StoryletEventAdapter.cs" />
```

- [x] **Step 4: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Content/StoryletEventAdapter.cs
```

- [x] **Step 5: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`.

If the build fails on the `InteractiveEvent` constructor call, re-do Step 1 verification and adjust.

- [x] **Step 6: Commit**

```bash
git add src/Features/Content/StoryletEventAdapter.cs Enlisted.csproj
git commit -F /c/Users/coola/commit.txt
# feat(content): StoryletEventAdapter — Storylet to InteractiveEvent Modal
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 5: Implement `PhaseDelivery.PlayerChoice` branch in `ActivityRuntime`

**Files:**
- Modify: `src/Features/Activities/ActivityRuntime.cs`

- [x] **Step 1: Add the fields + accessors**

Edit `src/Features/Activities/ActivityRuntime.cs`. Near the other private fields (around line 20-28), add:

```csharp
// Set when the current active activity enters a PlayerChoice phase; menu providers
// query this to decide when their slot-bank options are visible.
private (Activity activity, Phase phase)? _activeChoicePhase;

public (Activity activity, Phase phase)? GetActiveChoicePhase() => _activeChoicePhase;

public T FindActive<T>() where T : Activity =>
    _active.OfType<T>().FirstOrDefault();
```

- [x] **Step 2: Gate `TryFireAutoPhaseStorylet` to `Auto` only**

Already gated — the method checks `phase.Delivery != PhaseDelivery.Auto` and returns (Activity.cs inspection confirmed this). Leave it.

- [x] **Step 2b: Honour `FireIntervalHours` in the auto-fire path**

Without this, `Phase.FireIntervalHours > 0` is silently ignored — the `settle` phase fires every tick instead of every N in-game hours.

In `ActivityRuntime.cs`, track the last auto-fire hour on `Activity` (`HomeActivity.LastAutoFireHour` already declared in Task 6's `[SaveableProperty(5)]`). The auto-fire check reads it:

```csharp
private void TryFireAutoPhaseStorylet(Activity a)
{
    var phase = a.CurrentPhase;
    if (phase == null || phase.Delivery != PhaseDelivery.Auto) { return; }
    if (phase.Pool == null || phase.Pool.Count == 0) { return; }

    // Interval gate: skip if we've fired within the last FireIntervalHours.
    if (phase.FireIntervalHours > 0)
    {
        var currentHour = (int)CampaignTime.Now.ToHours;
        if (a is Enlisted.Features.Activities.Home.HomeActivity home)
        {
            if (home.LastAutoFireHour >= 0
                && currentHour - home.LastAutoFireHour < phase.FireIntervalHours)
            {
                return;
            }
            home.LastAutoFireHour = currentHour;
        }
    }

    // ... existing weighted-pick body follows unchanged ...
}
```

If Activity subclasses beyond `HomeActivity` need the same pacing (Specs 2-5), promote `LastAutoFireHour` onto `Activity` base as a `[SaveableProperty]` when they land — don't generalize prematurely in Spec 1. For now the cast-to-`HomeActivity` is acceptable.

- [x] **Step 3: Hook phase entry for PlayerChoice**

Find `Start(Activity activity, ActivityContext ctx)` (around line 80). After `activity.OnPhaseEnter(activity.CurrentPhase)`, add:

```csharp
public void Start(Activity activity, ActivityContext ctx)
{
    if (activity == null) { return; }
    activity.ResolvePhasesFromType(_types);
    activity.StartedAt = CampaignTime.Now;
    activity.OnStart(ctx);
    if (activity.CurrentPhase != null)
    {
        activity.OnPhaseEnter(activity.CurrentPhase);
        if (activity.CurrentPhase.Delivery == PhaseDelivery.PlayerChoice)
        {
            _activeChoicePhase = (activity, activity.CurrentPhase);
        }
    }
    _active.Add(activity);
    ConsumeParkedChains(activity);
}
```

- [x] **Step 4: Add `AdvancePhase` helper + `ResolvePlayerChoice`**

If `AdvancePhase` already exists (grep for it in `ActivityRuntime.cs`), skip to defining `ResolvePlayerChoice`. Otherwise add both. Place them after `ConsumeParkedChains`:

```csharp
private void AdvancePhase(Activity activity)
{
    if (activity == null) { return; }
    var oldPhase = activity.CurrentPhase;
    activity.OnPhaseExit(oldPhase);
    activity.CurrentPhaseIndex++;
    var newPhase = activity.CurrentPhase;
    if (newPhase != null)
    {
        activity.OnPhaseEnter(newPhase);
        if (newPhase.Delivery == PhaseDelivery.PlayerChoice)
        {
            _activeChoicePhase = (activity, newPhase);
        }
        else if (_activeChoicePhase?.activity == activity)
        {
            _activeChoicePhase = null;
        }
        ConsumeParkedChains(activity);
    }
    else
    {
        if (_activeChoicePhase?.activity == activity) { _activeChoicePhase = null; }
        activity.Finish(ActivityEndReason.Completed);
        _active.Remove(activity);
    }
}

public void ResolvePlayerChoice(Activity activity)
{
    if (activity == null) { return; }
    if (_activeChoicePhase?.activity != activity) { return; }
    AdvancePhase(activity);
}
```

If the existing `TryAdvancePhase(Activity)` method (called from `OnHourlyTick`) exists, verify it calls `AdvancePhase` (extract the body if needed so both code paths share it). The key invariant: whenever a phase transitions, `_activeChoicePhase` gets set or cleared correctly.

- [x] **Step 5: Build + commit**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded`.

```bash
git add src/Features/Activities/ActivityRuntime.cs
git commit -F /c/Users/coola/commit.txt
# feat(activities): PhaseDelivery.PlayerChoice branch + ResolvePlayerChoice
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 6: Create `HomeActivity` class (save offset 45)

**Files:**
- Create: `src/Features/Activities/Home/HomeActivity.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Write the class**

Create `src/Features/Activities/Home/HomeActivity.cs`:

```csharp
using System;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.Activities.Home
{
    /// <summary>
    /// Concrete Activity for "lord's party parked at a friendly settlement."
    /// Four phases: arrive (auto, 1h), settle (auto, 2h fire interval, 4h window),
    /// evening (player_choice, once per in-game night), break_camp (auto, fires on departure beat).
    /// Save offset 45 per EnlistedSaveDefiner surface-spec reservation 45-60.
    /// </summary>
    [SaveableClass(45)]
    public sealed class HomeActivity : Activity
    {
        public const string TypeIdConst = "home_activity";

        [SaveableProperty(1)] public string StartedAtSettlementId { get; set; } = string.Empty;
        [SaveableProperty(2)] public CampaignTime PhaseStartedAt { get; set; } = CampaignTime.Zero;
        [SaveableProperty(3)] public bool DigestReleased { get; set; }
        [SaveableProperty(4)] public bool EveningFired { get; set; }
        [SaveableProperty(5)] public int LastAutoFireHour { get; set; }

        public override void OnStart(ActivityContext ctx)
        {
            TypeId = TypeIdConst;
            StartedAtSettlementId = MobileParty.MainParty?.CurrentSettlement?.StringId ?? string.Empty;
            CurrentPhaseIndex = 0;
            PhaseStartedAt = CampaignTime.Now;
            // Intent was set by HomeActivityStarter before calling Start(...). Do not overwrite.
        }

        public override void OnPhaseEnter(Phase phase)
        {
            if (phase == null) { return; }
            PhaseStartedAt = CampaignTime.Now;
            LastAutoFireHour = -1;
            if (phase.Id == "arrive" && !DigestReleased)
            {
                // Digest release hook — see pacing spec §4.3. Release the "news from the road"
                // aggregation buffer on SettlementEntered beat when >= 2 days elapsed.
                // StoryDirector exposes this; if the API name differs in the live code, adjust.
                var director = Enlisted.Features.Content.StoryDirector.Instance;
                if (director != null)
                {
                    try { director.ReleaseDigest("SettlementEntered"); }
                    catch (Exception ex) { ModLogger.Caught("HOME", "Digest release failed", ex); }
                }
                DigestReleased = true;
            }
            if (phase.Id == "evening")
            {
                EveningFired = false;
            }
        }

        public override void OnPhaseExit(Phase phase) { }

        public override void Tick(bool hourly)
        {
            if (!hourly) { return; }
            var phase = CurrentPhase;
            if (phase == null) { return; }

            var hoursElapsed = (int)(CampaignTime.Now - PhaseStartedAt).ToHours;

            // Auto phases advance by DurationHours clock.
            if (phase.Delivery == PhaseDelivery.Auto && phase.DurationHours > 0
                && hoursElapsed >= phase.DurationHours)
            {
                ActivityRuntime.Instance?.ResolvePlayerChoice(this);
                // ResolvePlayerChoice only advances PlayerChoice phases; auto phases
                // advance via ActivityRuntime.TryAdvancePhase on the same tick.
                // If the runtime's auto-advance path differs, trigger it here.
            }
        }

        public override void OnPlayerChoice(string optionId, ActivityContext ctx)
        {
            if (string.IsNullOrEmpty(optionId)) { return; }
            Intent = optionId;
            EveningFired = true;
        }

        public override void OnBeat(string beatId, ActivityContext ctx)
        {
            if (string.Equals(beatId, "departure_imminent", StringComparison.OrdinalIgnoreCase))
            {
                // Skip directly to break_camp phase regardless of where we are.
                var breakCampIndex = Phases?.FindIndex(p => p.Id == "break_camp") ?? -1;
                if (breakCampIndex >= 0 && breakCampIndex != CurrentPhaseIndex)
                {
                    CurrentPhaseIndex = breakCampIndex - 1;  // AdvancePhase will increment
                    ActivityRuntime.Instance?.ResolvePlayerChoice(this);
                }
            }
        }

        public override void Finish(ActivityEndReason reason) { }
    }
}
```

- [ ] **Step 2: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Home\HomeActivity.cs" />
```

- [ ] **Step 3: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Home/HomeActivity.cs
```

- [ ] **Step 4: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

If `StoryDirector.ReleaseDigest` does not exist, comment out the call block in `OnPhaseEnter` and insert a `ModLogger.Expected("HOME", "digest_api_missing", "StoryDirector.ReleaseDigest not implemented yet");` as a placeholder. Revisit in Task 21 acceptance.

- [ ] **Step 5: Commit**

```bash
git add src/Features/Activities/Home/HomeActivity.cs Enlisted.csproj
git commit -F /c/Users/coola/commit.txt
# feat(home): HomeActivity class (save offset 45) with four-phase lifecycle
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 7: Register `HomeActivity` in `EnlistedSaveDefiner`

**Files:**
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`

- [ ] **Step 1: Verify offset 45 is free**

Grep for `45`:

```bash
# Grep tool preferred. Pattern: "offset.*45|DefineClassType.*offset.*45"
# Confirm no existing registration at 45.
```

- [ ] **Step 2: Register `HomeActivity`**

Open `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`. Locate the comment block reserving offsets 45-60 for surface specs. Add:

```csharp
// Spec 1 (Enlisted Home Surface) — offset 45.
DefineClassType(typeof(Enlisted.Features.Activities.Home.HomeActivity));
```

- [ ] **Step 3: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
git commit -F /c/Users/coola/commit.txt
# feat(save): register HomeActivity at offset 45 (Spec 1 claim)
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 8: Create `HomeTriggers` skeleton + registration

**Files:**
- Create: `src/Features/Activities/Home/HomeTriggers.cs`
- Modify: `Enlisted.csproj`
- Modify: `src/Mod.Entry/SubModule.cs` (call `HomeTriggers.Register()` at boot)

- [ ] **Step 1: Write the skeleton**

Create `src/Features/Activities/Home/HomeTriggers.cs`:

```csharp
using System;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Activities.Home
{
    /// <summary>
    /// Engine-state-reading trigger predicates for Spec 1 home-camp storylets.
    /// Every predicate is null-safe and reads live Bannerlord state — never QualityStore
    /// mirrors of engine data. Registered by SubModule at boot.
    /// </summary>
    public static class HomeTriggers
    {
        public static void Register()
        {
            var reg = TriggerRegistry.Instance;
            if (reg == null) { return; }

            // --- Party / food ---
            reg.Register("party_food_lte_3_days", ctx => PartyFoodDays() <= 3);
            reg.Register("party_food_lte_5_days", ctx => PartyFoodDays() <= 5);
            reg.Register("party_food_starving",   ctx => Party()?.Food < 1);

            // --- Party / morale ---
            reg.Register("lord_morale_lte_30", ctx => Party()?.Morale <= 30);
            reg.Register("lord_morale_lte_50", ctx => Party()?.Morale <= 50);
            reg.Register("lord_morale_lte_70", ctx => Party()?.Morale <= 70);
            reg.Register("lord_morale_gte_80", ctx => Party()?.Morale >= 80);

            // --- Lord condition ---
            reg.Register("lord_is_wounded",       ctx => Lord()?.IsWounded == true);
            reg.Register("lord_is_disorganized",  ctx => Party()?.IsDisorganized == true);
            reg.Register("lord_at_home_settlement", ctx =>
            {
                var lord = Lord(); var party = Party();
                return lord?.HomeSettlement != null && party?.CurrentSettlement == lord.HomeSettlement;
            });
            reg.Register("lord_has_spouse",   ctx => Lord()?.Spouse != null);
            reg.Register("lord_has_children", ctx => Lord()?.Children != null && Lord().Children.Count > 0);

            // --- Lord traits (Mercy / Valor / Honor / Calculating) ---
            RegisterTrait(reg, "mercy", DefaultTraits.Mercy);
            RegisterTrait(reg, "valor", DefaultTraits.Valor);
            RegisterTrait(reg, "honor", DefaultTraits.Honor);
            RegisterTrait(reg, "calculating", DefaultTraits.Calculating);

            // --- Clan / kingdom ---
            reg.Register("enlisted_lord_is_mercenary",
                ctx => Lord()?.Clan?.IsUnderMercenaryService == true);
            reg.Register("kingdom_at_war",
                ctx => (Lord()?.Clan?.FactionsAtWarWith?.Count ?? 0) > 0);
            reg.Register("kingdom_multi_front_war",
                ctx => (Lord()?.Clan?.FactionsAtWarWith?.Count ?? 0) > 1);

            // --- Settlement type ---
            reg.Register("at_town",    ctx => Settlement()?.IsTown == true);
            reg.Register("at_castle",  ctx => Settlement()?.IsCastle == true);
            reg.Register("at_village", ctx => Settlement()?.Village != null);

            // --- Settlement state ---
            reg.Register("settlement_under_siege", ctx => Settlement()?.IsUnderSiege == true);
            reg.Register("settlement_recent_threat", ctx =>
            {
                var s = Settlement();
                if (s == null) { return false; }
                return (CampaignTime.Now - s.LastThreatTime).ToDays < 7;
            });
            reg.Register("settlement_low_loyalty", ctx =>
            {
                var s = Settlement(); return s?.Town != null && s.Town.Loyalty < 40;
            });

            // --- Time ---
            reg.Register("is_night",   ctx => CampaignTime.Now.IsNightTime);
            reg.Register("is_autumn",  ctx => CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Autumn);
            reg.Register("is_winter",  ctx => CampaignTime.Now.GetSeasonOfYear == CampaignTime.Seasons.Winter);

            // --- Mod state ---
            reg.Register("days_enlisted_gte_30", ctx => DaysEnlisted() >= 30);
            reg.Register("days_enlisted_gte_90", ctx => DaysEnlisted() >= 90);
            reg.Register("rank_gte_3",           ctx => CurrentTier() >= 3);
        }

        private static void RegisterTrait(TriggerRegistry reg, string name, TraitObject trait)
        {
            reg.Register($"lord_trait_{name}_positive",
                ctx => (Lord()?.GetTraitLevel(trait) ?? 0) >= 1);
            reg.Register($"lord_trait_{name}_negative",
                ctx => (Lord()?.GetTraitLevel(trait) ?? 0) <= -1);
        }

        private static Hero Lord() => EnlistmentBehavior.Instance?.EnlistedLord;
        private static MobileParty Party() => Lord()?.PartyBelongedTo;
        private static Settlement Settlement() => Party()?.CurrentSettlement;

        private static int PartyFoodDays()
        {
            var p = Party();
            if (p == null) { return int.MaxValue; }
            try { return p.GetNumDaysForFoodToLast(); }
            catch { return int.MaxValue; }
        }

        private static int DaysEnlisted()
        {
            // Prefer a public accessor; fall back to 0 if not exposed.
            var eb = EnlistmentBehavior.Instance;
            if (eb == null) { return 0; }
            // EnlistmentBehavior exposes DaysEnlisted (verify at build time; if the
            // accessor is named differently or non-public, adjust here).
            try { return (int)eb.DaysEnlisted; }
            catch { return 0; }
        }

        private static int CurrentTier()
        {
            var eb = EnlistmentBehavior.Instance;
            if (eb == null) { return 0; }
            try { return eb.CurrentTier; }
            catch { return 0; }
        }
    }
}
```

- [ ] **Step 2: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Home\HomeTriggers.cs" />
```

- [ ] **Step 3: Wire into SubModule boot**

Open `src/Mod.Entry/SubModule.cs`. Find where other trigger-registering calls happen at campaign start (search for `TriggerRegistry` or existing `Register()` calls). Add after Spec 0's registrations:

```csharp
Enlisted.Features.Activities.Home.HomeTriggers.Register();
```

- [ ] **Step 4: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Home/HomeTriggers.cs
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

If `EnlistmentBehavior.DaysEnlisted` or `.CurrentTier` don't exist as public members, expose them as narrow read-only properties in `EnlistmentBehavior.cs` (one-line each). Do not create new subsystems.

If `CampaignTime.Seasons.Autumn` is the wrong enum path, consult `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignTime.cs` and fix.

- [ ] **Step 5: Commit**

```bash
git add src/Features/Activities/Home/HomeTriggers.cs src/Mod.Entry/SubModule.cs Enlisted.csproj
# If EnlistmentBehavior accessors were added:
# git add src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs
git commit -F /c/Users/coola/commit.txt
# feat(home): HomeTriggers — 30 engine-state predicates for Spec 1 storylets
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 9: Create `HomeActivityStarter` (lifecycle detection)

**Files:**
- Create: `src/Features/Activities/Home/HomeActivityStarter.cs`
- Modify: `Enlisted.csproj`
- Modify: `src/Mod.Entry/SubModule.cs` (add behavior to campaign starter)

- [ ] **Step 1: Write the starter**

Create `src/Features/Activities/Home/HomeActivityStarter.cs`:

```csharp
using System;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.Activities.Home
{
    /// <summary>
    /// Starts/finishes HomeActivity on the enlisted lord's settlement enter/leave.
    /// Strict party-identity filter — only reacts to the enlisted lord's party,
    /// not any friendly party passing through.
    /// </summary>
    public sealed class HomeActivityStarter : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero heroLeader)
        {
            try
            {
                if (!ShouldStart(party, settlement)) { return; }
                var ctx = Enlisted.Features.Activities.ActivityContext.FromCurrent();
                var activity = new HomeActivity { Intent = PickInitialIntent(party, settlement) };
                ActivityRuntime.Instance?.Start(activity, ctx);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("HOME-START", "Failed to start HomeActivity", ex);
            }
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            try
            {
                var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (lord?.PartyBelongedTo == null) { return; }
                if (party != lord.PartyBelongedTo) { return; }

                var home = ActivityRuntime.Instance?.FindActive<HomeActivity>();
                if (home == null) { return; }
                home.OnBeat("departure_imminent", Enlisted.Features.Activities.ActivityContext.FromCurrent());
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("HOME-START", "Failed to end HomeActivity on settlement leave", ex);
            }
        }

        private static bool ShouldStart(MobileParty party, Settlement settlement)
        {
            if (settlement == null) { return false; }
            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            if (lord?.PartyBelongedTo == null)
            {
                ModLogger.Expected("HOME-START", "not_enlisted", "No enlisted lord; HomeActivity skipped");
                return false;
            }
            if (party != lord.PartyBelongedTo)
            {
                return false;  // someone else's party — ignore silently
            }
            if (settlement.IsHideout) { return false; }
            if (!(settlement.IsTown || settlement.IsCastle || settlement.Village != null))
            {
                return false;
            }
            if (settlement.MapFaction != lord.MapFaction)
            {
                ModLogger.Expected("HOME-START", "hostile_fief", "Settlement not friendly to lord");
                return false;
            }
            if (ActivityRuntime.Instance?.FindActive<HomeActivity>() != null)
            {
                return false;  // already running
            }
            return true;
        }

        private static string PickInitialIntent(MobileParty party, Settlement settlement)
        {
            // Weighted-random from five candidates by engine state. Player's Evening
            // choice overrides this; the initial value biases Arrive + Settle only.
            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            var weights = new System.Collections.Generic.Dictionary<string, float>
            {
                { "unwind", 1.0f },
                { "train_hard", 0.0f },
                { "brood", 0.0f },
                { "commune", 0.0f },
                { "scheme", 0.0f },
            };
            if (lord?.IsWounded == true) { weights["brood"] += 3.0f; }
            if (party?.Morale < 40) { weights["brood"] += 2.0f; weights["unwind"] += 2.0f; }
            if (lord != null && lord.GetTraitLevel(DefaultTraits.Valor) >= 1) { weights["train_hard"] += 2.0f; }
            if (lord?.Clan?.IsUnderMercenaryService == true
                && lord.GetTraitLevel(DefaultTraits.Calculating) >= 1) { weights["scheme"] += 2.0f; }
            if (lord?.HomeSettlement == settlement && lord?.Spouse != null) { weights["commune"] += 3.0f; }

            var total = 0f;
            foreach (var w in weights.Values) { total += w; }
            if (total <= 0f) { return "unwind"; }
            var r = (float)(new Random().NextDouble() * total);
            var cursor = 0f;
            foreach (var kv in weights)
            {
                cursor += kv.Value;
                if (r <= cursor) { return kv.Key; }
            }
            return "unwind";
        }
    }
}
```

- [ ] **Step 2: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Home\HomeActivityStarter.cs" />
```

- [ ] **Step 3: Register with `CampaignGameStarter`**

Open `src/Mod.Entry/SubModule.cs` (or the mod's `CampaignBehavior` registration site). Find where `ActivityRuntime` is added to the behaviors list. Add:

```csharp
gameStarter.AddBehavior(new Enlisted.Features.Activities.Home.HomeActivityStarter());
```

- [ ] **Step 4: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Home/HomeActivityStarter.cs
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 5: Commit**

```bash
git add src/Features/Activities/Home/HomeActivityStarter.cs src/Mod.Entry/SubModule.cs Enlisted.csproj
git commit -F /c/Users/coola/commit.txt
# feat(home): HomeActivityStarter — party-identity-gated lifecycle
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 10: Create `HomeEveningMenuProvider`

**Files:**
- Create: `src/Features/Activities/Home/HomeEveningMenuProvider.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Write the provider**

Create `src/Features/Activities/Home/HomeEveningMenuProvider.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Enlisted.Features.Activities.Home
{
    /// <summary>
    /// Supplies the five Evening intent options to the preallocated menu slot-bank
    /// in EnlistedMenuBehavior. Slot visibility gated by per-intent pool eligibility.
    /// </summary>
    public static class HomeEveningMenuProvider
    {
        public static readonly string[] Intents = { "unwind", "train_hard", "brood", "commune", "scheme" };

        public static bool IsSlotActive(int slotIndex, out TextObject label, out TextObject tooltip)
        {
            label = TextObject.Empty;
            tooltip = TextObject.Empty;
            if (slotIndex < 0 || slotIndex >= Intents.Length) { return false; }

            var runtime = ActivityRuntime.Instance;
            var active = runtime?.GetActiveChoicePhase();
            if (active == null) { return false; }
            if (!(active.Value.activity is HomeActivity home)) { return false; }
            if (active.Value.phase.Id != "evening") { return false; }

            var intent = Intents[slotIndex];
            var eligible = CountEligibleForIntent(home, intent);
            label = new TextObject("{=home_evening_" + intent + "_label}" + DefaultLabel(intent));
            tooltip = BuildTooltip(intent, eligible > 0);
            return eligible > 0;
        }

        public static void OnSlotSelected(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Intents.Length) { return; }
            var runtime = ActivityRuntime.Instance;
            var active = runtime?.GetActiveChoicePhase();
            if (active == null) { return; }
            if (!(active.Value.activity is HomeActivity home)) { return; }

            var intent = Intents[slotIndex];
            var ctx = new StoryletContext
            {
                ActivityTypeId = home.TypeId,
                PhaseId = "evening",
                CurrentContext = "settlement"
            };
            home.OnPlayerChoice(intent, Enlisted.Features.Activities.ActivityContext.FromCurrent());

            var picked = PickStorylet(home, intent, ctx);
            if (picked == null)
            {
                ModLogger.Surfaced("HOME-EVENING", "No eligible storylet at fire",
                    new System.InvalidOperationException("intent=" + intent));
                runtime.ResolvePlayerChoice(home);
                return;
            }

            EffectExecutor.Apply(picked.Immediate, ctx);
            var evt = StoryletEventAdapter.BuildModal(picked, ctx, home);
            var candidate = picked.ToCandidate(ctx);
            if (candidate != null && evt != null)
            {
                candidate.InteractiveEvent = evt;
                candidate.ProposedTier = StoryTier.Modal;
                candidate.ChainContinuation = true;
                StoryDirector.Instance?.EmitCandidate(candidate);
            }
            runtime.ResolvePlayerChoice(home);
        }

        private static int CountEligibleForIntent(HomeActivity home, string intent)
        {
            if (home?.CurrentPhase?.Pool == null) { return 0; }
            var ctx = new StoryletContext
            {
                ActivityTypeId = home.TypeId,
                PhaseId = "evening",
                CurrentContext = "settlement"
            };
            var prefix = "home_evening_" + intent + "_";
            var count = 0;
            foreach (var id in home.CurrentPhase.Pool)
            {
                if (id == null || !id.StartsWith(prefix)) { continue; }
                var s = StoryletCatalog.GetById(id);
                if (s != null && s.IsEligible(ctx)) { count++; }
            }
            return count;
        }

        private static Storylet PickStorylet(HomeActivity home, string intent, StoryletContext ctx)
        {
            var prefix = "home_evening_" + intent + "_";
            var eligible = new List<(Storylet s, float w)>();
            foreach (var id in home.CurrentPhase?.Pool ?? new List<string>())
            {
                if (id == null || !id.StartsWith(prefix)) { continue; }
                var s = StoryletCatalog.GetById(id);
                if (s == null || !s.IsEligible(ctx)) { continue; }
                var w = s.WeightFor(ctx);
                if (w > 0f) { eligible.Add((s, w)); }
            }
            if (eligible.Count == 0) { return null; }
            var total = eligible.Sum(e => e.w);
            var r = (float)(new System.Random().NextDouble() * total);
            var cursor = 0f;
            foreach (var (s, w) in eligible)
            {
                cursor += w;
                if (r <= cursor) { return s; }
            }
            return eligible[eligible.Count - 1].s;
        }

        private static TextObject BuildTooltip(string intent, bool eligible)
        {
            if (!eligible)
            {
                return new TextObject("{=home_evening_nothing}There's nothing here tonight.");
            }
            if (intent == "scheme"
                && EnlistmentBehavior.Instance?.EnlistedLord?.Clan?.IsUnderMercenaryService != true)
            {
                return new TextObject("{=home_scheme_tooltip_vassal}Risky under an honour-bound lord. Scrutiny may rise.");
            }
            return new TextObject("{=home_evening_" + intent + "_tooltip}…");
        }

        private static string DefaultLabel(string intent)
        {
            switch (intent)
            {
                case "unwind":     return "Drink with the squad";
                case "train_hard": return "Drill until blisters";
                case "brood":      return "Sit alone with your thoughts";
                case "commune":    return "Seek out the lord";
                case "scheme":     return "Slip away quietly";
                default:           return intent;
            }
        }
    }
}
```

- [ ] **Step 2: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Home\HomeEveningMenuProvider.cs" />
```

- [ ] **Step 3: Normalize CRLF + build**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Home/HomeEveningMenuProvider.cs
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded`. If `StoryCandidate.InteractiveEvent` or `ChainContinuation` property names differ, consult the actual Spec 0 class and correct.

- [ ] **Step 4: Commit**

```bash
git add src/Features/Activities/Home/HomeEveningMenuProvider.cs Enlisted.csproj
git commit -F /c/Users/coola/commit.txt
# feat(home): HomeEveningMenuProvider — 5-slot Evening intent menu provider
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 11: Register 5-slot Evening option bank in `EnlistedMenuBehavior`

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`

- [ ] **Step 1: Locate the decisions-slot loop**

In `EnlistedMenuBehavior.cs`, find the block around line 1150-1158 that registers `enlisted_decision_slot_0..4`. The new evening-slot bank registers using the same pattern.

- [ ] **Step 2: Insert the evening-slot loop**

Directly after the closing `}` of the decisions loop (before the camp_hub option at line 1161, which is at priority 10), add:

```csharp
// Evening intent slots — visible only when HomeActivity is in the evening phase.
// Priority 9 so Evening appears above the camp_hub (priority 10) during its phase,
// matching the decisions-slot pattern at priorities 4-8.
for (var i = 0; i < 5; i++)
{
    var slotIndex = i;
    starter.AddGameMenuOption("enlisted_status", $"enlisted_evening_intent_slot_{i}",
        $"{{EVENING_SLOT_{i}_TEXT}}",
        args =>
        {
            if (!Enlisted.Features.Activities.Home.HomeEveningMenuProvider
                    .IsSlotActive(slotIndex, out var label, out var tooltip))
            {
                return false;
            }
            MBTextManager.SetTextVariable($"EVENING_SLOT_{slotIndex}_TEXT", label.ToString(), false);
            args.Tooltip = tooltip;
            args.IsEnabled = true;
            return true;
        },
        _ =>
        {
            Enlisted.Features.Activities.Home.HomeEveningMenuProvider.OnSlotSelected(slotIndex);
        },
        false, 9);
}
```

Make sure the `MBTextManager` import is present (existing file already uses it for decision slots).

- [ ] **Step 3: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs
git commit -F /c/Users/coola/commit.txt
# feat(menu): Evening intent 5-slot bank on enlisted_status
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 12: Phase A-C smoke — confirm wiring with an empty content pool

**Files:** (none modified)

This task verifies A-C code compiles and boots without content; the slot-bank should be silent (all five slots hidden).

- [ ] **Step 1: Build + check no new ModLogger.Surfaced paths are hit during boot**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Launch the game manually. Load a save where the player is enlisted. Enter a friendly town. Confirm:
- `enlisted_status` opens without crashing.
- No Evening-slot options appear (content pool empty).
- Session log at `Modules/Enlisted/Debugging/Session-A_*.log` shows `HOME-START` Expected messages as expected (no Surfaced errors).

- [ ] **Step 2: Commit nothing; this is a checkpoint**

No commit.

---

## Task 13: Author `home_activity.json` (ActivityTypeDefinition)

**Files:**
- Create: `ModuleData/Enlisted/Activities/home_activity.json`

- [ ] **Step 1: Write the activity definition**

Create `ModuleData/Enlisted/Activities/home_activity.json`:

```json
{
  "id": "home_activity",
  "phases": [
    {
      "id": "arrive",
      "duration_hours": 1,
      "fire_interval_hours": 0,
      "delivery": "auto",
      "pool": [
        "home_arrive_home_fief",
        "home_arrive_lord_wounded_returning",
        "home_arrive_grim_mood",
        "home_arrive_post_defeat",
        "home_arrive_faction_empire",
        "home_arrive_faction_sturgia",
        "home_arrive_faction_battania",
        "home_arrive_faction_vlandia"
      ],
      "intent_bias": { "brood": 1.2, "unwind": 1.0, "commune": 1.3, "train_hard": 0.8, "scheme": 0.9 }
    },
    {
      "id": "settle",
      "duration_hours": 4,
      "fire_interval_hours": 2,
      "delivery": "auto",
      "pool": [
        "home_settle_food_anxiety_3d",
        "home_settle_food_anxiety_5d",
        "home_settle_wounded_tend",
        "home_settle_stores_short",
        "home_settle_weather_autumn",
        "home_settle_weather_winter",
        "home_settle_low_loyalty_town",
        "home_settle_recent_threat",
        "home_settle_lord_observation_mercy",
        "home_settle_lord_observation_valor",
        "home_settle_chaplain_passes",
        "home_settle_siege_preparations"
      ],
      "intent_bias": { "brood": 1.0, "unwind": 1.0, "commune": 1.0, "train_hard": 1.1, "scheme": 1.0 }
    },
    {
      "id": "evening",
      "duration_hours": 0,
      "fire_interval_hours": 0,
      "delivery": "player_choice",
      "pool": [
        "home_evening_unwind_dice",
        "home_evening_unwind_song",
        "home_evening_unwind_sergeant",
        "home_evening_unwind_firelit",
        "home_evening_unwind_stars",
        "home_evening_train_hard_sparring",
        "home_evening_train_hard_drill",
        "home_evening_train_hard_master",
        "home_evening_train_hard_armor",
        "home_evening_train_hard_foreign_form",
        "home_evening_brood_dead_friend",
        "home_evening_brood_lord_like_father",
        "home_evening_brood_letter_home",
        "home_evening_brood_cold_comfort",
        "home_evening_brood_drift",
        "home_evening_commune_lord_at_hearth",
        "home_evening_commune_squadmate_confession",
        "home_evening_commune_family_meal",
        "home_evening_commune_chaplain",
        "home_evening_commune_old_soldier",
        "home_evening_scheme_skim_stores",
        "home_evening_scheme_eavesdrop_hall",
        "home_evening_scheme_bribe_gate",
        "home_evening_scheme_tavern_rumors",
        "home_evening_scheme_ghost_sentry"
      ]
    },
    {
      "id": "break_camp",
      "duration_hours": 1,
      "fire_interval_hours": 0,
      "delivery": "auto",
      "pool": [
        "home_break_camp_parting_gate",
        "home_break_camp_lord_mood_after_home",
        "home_break_camp_supply_trains",
        "home_break_camp_sword_edge",
        "home_break_camp_rain_on_march"
      ]
    }
  ]
}
```

- [ ] **Step 2: Commit**

```bash
git add ModuleData/Enlisted/Activities/home_activity.json
git commit -F /c/Users/coola/commit.txt
# feat(content): home_activity.json — four-phase HomeActivity definition
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 14: Author `home_arrive` storylets (8)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/home_arrive.json`

Ambient auto-phase. Each storylet has no options (or a single "continue" option), observational flag true, and a trigger set grounded in engine state.

- [ ] **Step 1: Write the storylet file**

Create `ModuleData/Enlisted/Storylets/home_arrive.json`:

```json
{
  "storylets": [
    {
      "id": "home_arrive_home_fief",
      "category": "home_arrive",
      "observational": true,
      "trigger": ["lord_at_home_settlement"],
      "weight": { "base": 2.5 },
      "scope": { "context": "settlement" },
      "title": "{=home_arrive_home_fief_title}Under his own banner",
      "setup": "{=home_arrive_home_fief_setup}The column crunches up the avenue. Your lord rides ahead — this is his seat, and the banners know him. A boy in homespun sprints to fetch word. You unbuckle your helm."
    },
    {
      "id": "home_arrive_lord_wounded_returning",
      "category": "home_arrive",
      "observational": true,
      "trigger": ["lord_is_wounded"],
      "weight": { "base": 3.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_arrive_wounded_title}They see his arm",
      "setup": "{=home_arrive_wounded_setup}The gate-sergeant's face changes when he sees the splint. Whatever was prepared, it is all being prepared again, quicker. You help the wounded down before anyone tells you to."
    },
    {
      "id": "home_arrive_grim_mood",
      "category": "home_arrive",
      "observational": true,
      "trigger": ["lord_morale_lte_50"],
      "weight": {
        "base": 2.0,
        "modifiers": [ { "if": "lord_trait_honor_positive", "factor": 0.8 } ]
      },
      "scope": { "context": "settlement" },
      "title": "{=home_arrive_grim_title}No cheering at the gate",
      "setup": "{=home_arrive_grim_setup}The column rides in quiet. No one calls names from the walls. A few hands raised from the watch, and nothing more. Your lord's eyes never leave the road ahead."
    },
    {
      "id": "home_arrive_post_defeat",
      "category": "home_arrive",
      "observational": true,
      "trigger": ["lord_is_disorganized"],
      "weight": { "base": 3.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_arrive_post_defeat_title}Limp-in",
      "setup": "{=home_arrive_post_defeat_setup}You remember who you came out with and count them in as they come through. The numbers don't agree. Your lord sits his horse at the gate-keep until the last man is inside, then dismounts without speaking."
    },
    {
      "id": "home_arrive_faction_empire",
      "category": "home_arrive",
      "observational": true,
      "trigger": [],
      "weight": { "base": 1.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_arrive_empire_title}Under the purple",
      "setup": "{=home_arrive_empire_setup}Imperial standards snap in the wind above the gatehouse. A clerk checks names against a wax-sealed writ. You ride in ordered lines, as the drill-books demand."
    },
    {
      "id": "home_arrive_faction_sturgia",
      "category": "home_arrive",
      "observational": true,
      "trigger": [],
      "weight": { "base": 1.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_arrive_sturgia_title}Through the palisade",
      "setup": "{=home_arrive_sturgia_setup}The timber palisade creaks open. Smoke rises from a half-dozen hearths. Someone in wolf-fur shouts a greeting; your lord answers in the Sturgian tongue you haven't yet learned."
    },
    {
      "id": "home_arrive_faction_battania",
      "category": "home_arrive",
      "observational": true,
      "trigger": [],
      "weight": { "base": 1.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_arrive_battania_title}Hillfort gate",
      "setup": "{=home_arrive_battania_setup}The fort sits on a crag above a mist-wrapped glen. Hounds bark in the yard. The keep's old stones remember things no imperial could read; your lord lays his hand on the gatepost like he's greeting it."
    },
    {
      "id": "home_arrive_faction_vlandia",
      "category": "home_arrive",
      "observational": true,
      "trigger": [],
      "weight": { "base": 1.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_arrive_vlandia_title}Castellan's bow",
      "setup": "{=home_arrive_vlandia_setup}The castellan meets your lord at the gate with a formal bow. Spears rattle to attention along the curtain wall. You remember to remove your gauntlets before touching bread — the Vlandian custom, the sergeant said."
    }
  ]
}
```

- [ ] **Step 2: Commit**

```bash
git add ModuleData/Enlisted/Storylets/home_arrive.json
git commit -F /c/Users/coola/commit.txt
# feat(content): home_arrive storylets (8) — settlement-arrival variety
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 15: Author `home_settle` storylets (12)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/home_settle.json`

Auto-phase ambient beats. Each has triggers grounded in engine state; observational with no options or a single continue.

- [ ] **Step 1: Write the storylet file**

Create `ModuleData/Enlisted/Storylets/home_settle.json` with 12 storylets. For brevity here, each entry follows the same shape — trigger(s), weight, title, setup, observational=true, no options. Use these IDs and triggers; prose is author's concern but must be <160 chars for setup, <80 for title:

```json
{
  "storylets": [
    {
      "id": "home_settle_food_anxiety_3d",
      "category": "home_settle", "observational": true,
      "trigger": ["party_food_lte_3_days"], "weight": { "base": 3.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_food3_title}Three days of grain",
      "setup": "{=home_settle_food3_setup}The quartermaster counts sacks twice, and the tally doesn't change. Whatever the writ says, the writ is behind the barrels."
    },
    {
      "id": "home_settle_food_anxiety_5d",
      "category": "home_settle", "observational": true,
      "trigger": ["party_food_lte_5_days"], "weight": { "base": 1.5 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_food5_title}Short rations",
      "setup": "{=home_settle_food5_setup}The bread is thinner than yesterday. No one says it. You notice it when you break your own, and you say nothing either."
    },
    {
      "id": "home_settle_wounded_tend",
      "category": "home_settle", "observational": true,
      "trigger": ["lord_is_wounded"], "weight": { "base": 2.5 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_wounded_title}The surgeon's tent",
      "setup": "{=home_settle_wounded_setup}You help carry a man who won't stop bleeding. Your lord is in a smaller tent, behind a curtain, being quiet about his own hurt. You were seen pulling your weight."
    },
    {
      "id": "home_settle_stores_short",
      "category": "home_settle", "observational": true,
      "trigger": ["party_food_lte_5_days"], "weight": { "base": 1.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_stores_title}Counting barrels",
      "setup": "{=home_settle_stores_setup}The quartermaster and the castle stewards square off over an oat-count that doesn't match the writ. Your own rations aren't touched — yet."
    },
    {
      "id": "home_settle_weather_autumn",
      "category": "home_settle", "observational": true,
      "trigger": ["is_autumn"], "weight": { "base": 1.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_autumn_title}The first bite of autumn",
      "setup": "{=home_settle_autumn_setup}A chill settles into the bones of the walls. Someone sends out for more firewood. Your breath makes a plume on the morning air, and your hands take longer to warm."
    },
    {
      "id": "home_settle_weather_winter",
      "category": "home_settle", "observational": true,
      "trigger": ["is_winter"], "weight": { "base": 1.5 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_winter_title}Snow on the walls",
      "setup": "{=home_settle_winter_setup}The snow has settled into the crenellations. You are sent to help chip ice from the water-barrel and your hands ache after. The cold gets into the gear too; the old sergeant warns you to oil everything, twice."
    },
    {
      "id": "home_settle_low_loyalty_town",
      "category": "home_settle", "observational": true,
      "trigger": ["at_town", "settlement_low_loyalty"], "weight": { "base": 2.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_lowloy_title}Eyes in the market",
      "setup": "{=home_settle_lowloy_setup}The market-folk look through you, not at you. Someone spits after your patrol passes. Your sergeant tells you not to notice, so you don't."
    },
    {
      "id": "home_settle_recent_threat",
      "category": "home_settle", "observational": true,
      "trigger": ["settlement_recent_threat"], "weight": { "base": 2.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_threat_title}The last raid is still warm",
      "setup": "{=home_settle_threat_setup}You ride past a burned byre on the approach. Inside the walls, the talk is louder, uglier. Men grip their cups a little harder when they speak of it."
    },
    {
      "id": "home_settle_lord_observation_mercy",
      "category": "home_settle", "observational": true,
      "trigger": ["lord_trait_mercy_positive"], "weight": { "base": 1.2 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_mercy_title}A word for the wounded",
      "setup": "{=home_settle_mercy_setup}Your lord stops in the surgeon's tent longer than the rounds require. He has a short word for each man. Someone who can't answer him is told he is expected to live, and the lie kindly told."
    },
    {
      "id": "home_settle_lord_observation_valor",
      "category": "home_settle", "observational": true,
      "trigger": ["lord_trait_valor_positive"], "weight": { "base": 1.2 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_valor_title}Into the yard at dawn",
      "setup": "{=home_settle_valor_setup}Before the watch-horn, your lord is already in the practice yard, working through forms with a dented training-sword. Two squires stand watching like men who have learned not to interrupt."
    },
    {
      "id": "home_settle_chaplain_passes",
      "category": "home_settle", "observational": true,
      "trigger": [], "weight": { "base": 0.8 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_chaplain_title}The chaplain passes through",
      "setup": "{=home_settle_chaplain_setup}An old cleric moves from fire to fire, saying what he says over bowed heads. He pauses at yours, touches your shoulder, moves on."
    },
    {
      "id": "home_settle_siege_preparations",
      "category": "home_settle", "observational": true,
      "trigger": ["settlement_under_siege"], "weight": { "base": 3.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_settle_siege_title}Preparing for worse",
      "setup": "{=home_settle_siege_setup}Orders come down hourly. Arrows are bundled, water-butts filled, the keep's stores moved up a floor. No one asks you about your day."
    }
  ]
}
```

- [ ] **Step 2: Commit**

```bash
git add ModuleData/Enlisted/Storylets/home_settle.json
git commit -F /c/Users/coola/commit.txt
# feat(content): home_settle storylets (12) — ambient camp-day beats
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 16: Author `home_evening` storylets (25, five per intent)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/home_evening.json`

Player-choice storylets. Each has 2-3 options with effects. Scope context is "settlement". Use scripted effects from the Spec 0 seed catalog (`lord_relation_up_small`, `rank_xp_minor`, `scrutiny_up_small`, etc.).

Due to length, this task includes **one fully-worked example** (brood / dead-friend chain) and a **template-matrix** for the remaining 24. All 25 storylets follow the same shape.

- [ ] **Step 1: Start the storylet file with the keystone brood example**

Create `ModuleData/Enlisted/Storylets/home_evening.json`:

```json
{
  "storylets": [
    {
      "id": "home_evening_brood_dead_friend",
      "category": "home_evening_brood",
      "trigger": ["days_enlisted_gte_30"],
      "weight": {
        "base": 2.0,
        "modifiers": [
          { "if": "lord_trait_honor_positive", "factor": 1.3 },
          { "if": "is_autumn", "factor": 1.2 }
        ]
      },
      "scope": { "context": "settlement" },
      "title": "{=home_brood_empty_cot_title}In the empty cot",
      "setup": "{=home_brood_empty_cot_setup}Seoras had the cot next to yours since Pravend. His kit is still folded at the foot, the way he liked it — the boots turned just so. You sit with it in the dark a while.",
      "options": [
        {
          "id": "take_boots",
          "text": "{=home_brood_empty_cot_opt_take}Take the boots. He'd want someone to use them.",
          "tooltip": "{=home_brood_empty_cot_opt_take_tt}+Roguery. Scrutiny up if caught.",
          "effects": [
            { "apply": "skill_xp", "skill": "Roguery", "amount": 20 },
            { "apply": "set_flag", "name": "took_dead_friend_gear" },
            { "apply": "quality_add", "quality": "scrutiny", "amount": 2 }
          ]
        },
        {
          "id": "keep_kit",
          "text": "{=home_brood_empty_cot_opt_keep}Keep the kit as it is. Someone should know him still.",
          "tooltip": "{=home_brood_empty_cot_opt_keep_tt}+lord relation.",
          "effects": [
            { "apply": "lord_relation_up_small" },
            { "apply": "set_flag", "name": "kept_dead_friend_kit" }
          ]
        },
        {
          "id": "write_ledger",
          "text": "{=home_brood_empty_cot_opt_write}Write his name in the book no one asked you to keep.",
          "tooltip": "{=home_brood_empty_cot_opt_write_tt}+rank_xp. Parks a ledger chain.",
          "effects": [
            { "apply": "rank_xp_minor" },
            { "apply": "set_flag", "name": "keeps_death_ledger" }
          ],
          "chain": { "id": "home_chain_ledger_rumored", "when": "home_activity.evening" }
        }
      ]
    }
```

- [ ] **Step 2: Author the remaining 24 storylets using this matrix**

Add these objects to the `storylets` array in the same file. Each follows the shape: `id`, `category`, `trigger` (0-2 items), `weight` (1.0-2.0 base), `scope.context: "settlement"`, `title` + `setup` with `{=id}Fallback`, `options` array with 2-3 choices. Options include effects (scripted or primitive); scheme storylets add scrutiny; commune storylets add lord_relation; train_hard storylets grant skill_xp to Athletics / One-Handed / Two-Handed / Polearm / Bow; unwind storylets touch morale proxies or just social flags; brood storylets grant rank_xp or trait nudges.

**ID → trigger(s) → option shape:**

| Intent | ID | Trigger(s) | Primary option effects |
| :--- | :--- | :--- | :--- |
| unwind | `home_evening_unwind_dice` | `[]` | dice_won: +50 gold / dice_lost: -50 gold / walk_away: lord_relation_up_small |
| unwind | `home_evening_unwind_song` | `[]` | sing: set_flag `sang_at_camp` / listen: `lord_relation_up_small` |
| unwind | `home_evening_unwind_sergeant` | `[lord_morale_gte_80]` | drink: set_flag `friend_with_sergeant` / decline: `skill_xp Leadership 10` |
| unwind | `home_evening_unwind_firelit` | `[is_autumn]` or `[is_winter]` | stay_late: flag `firelit_stories` / turn_in_early: rank_xp_minor |
| unwind | `home_evening_unwind_stars` | `[is_night]` | reflect: flag `stargazer` / pass: (no effect) |
| train_hard | `home_evening_train_hard_sparring` | `[]` | spar: `skill_xp OneHanded 25` / bet_spar: gold±100 flag |
| train_hard | `home_evening_train_hard_drill` | `[]` | drill: `skill_xp Athletics 25` |
| train_hard | `home_evening_train_hard_master` | `[at_town]` | seek: `skill_xp TwoHanded 30` + gold -20 / defer: — |
| train_hard | `home_evening_train_hard_armor` | `[]` | condition: `skill_xp Crafting 15` + flag `gear_conditioned` |
| train_hard | `home_evening_train_hard_foreign_form` | `[]` | learn: `skill_xp Polearm 25` / laugh: (no effect) |
| brood | `home_evening_brood_dead_friend` | *(above)* | *(above)* |
| brood | `home_evening_brood_lord_like_father` | `[lord_trait_honor_positive, days_enlisted_gte_30]` | confide: `lord_relation_up_small` + flag / keep_silent: rank_xp_minor |
| brood | `home_evening_brood_letter_home` | `[]` | write_and_send: flag `sent_letter` / write_and_burn: `rank_xp_minor` / don't_write: — |
| brood | `home_evening_brood_cold_comfort` | `[lord_morale_lte_50]` | steel_self: flag `inured` / doubt: `quality_add scrutiny 1` |
| brood | `home_evening_brood_drift` | `[]` (fallback) | one option: "The night passes." (no effect) |
| commune | `home_evening_commune_lord_at_hearth` | `[]` | speak_plainly: `lord_relation_up_small` + chain home_chain_lord_remembered / listen: `lord_relation_up_small` (smaller) |
| commune | `home_evening_commune_squadmate_confession` | `[]` | keep_secret: flag `keeps_secrets` / report: `quality_add scrutiny -2` (approval) + flag |
| commune | `home_evening_commune_family_meal` | `[lord_at_home_settlement, lord_has_spouse]` | share: `lord_relation_up_small`*2 / decline: (polite) |
| commune | `home_evening_commune_chaplain` | `[]` | confess: `quality_add scrutiny -3` / evade: (no effect) |
| commune | `home_evening_commune_old_soldier` | `[days_enlisted_gte_30]` | listen: `rank_xp_minor` / cut_short: (no effect) |
| scheme | `home_evening_scheme_skim_stores` | `[enlisted_lord_is_mercenary]` | take_a_little: +50 gold + `quality_add scrutiny 3` + chain home_chain_stores_audited / leave_it: (no effect) |
| scheme | `home_evening_scheme_eavesdrop_hall` | `[enlisted_lord_is_mercenary]` | listen: flag `overheard_plans` + `quality_add scrutiny 4` / walk_away: — |
| scheme | `home_evening_scheme_bribe_gate` | `[enlisted_lord_is_mercenary, at_castle]` | pay: -30 gold + flag `gate_owed_favor` / refuse: — |
| scheme | `home_evening_scheme_tavern_rumors` | `[at_town]` (universal) | listen: flag `knows_rumor` / leave: — / spread: `quality_add scrutiny 2` + flag |
| scheme | `home_evening_scheme_ghost_sentry` | `[at_castle, is_night]` (universal, mid-scrutiny) | slip_out: flag `slipped_patrol` + `quality_add scrutiny 5` / stay_post: — |

Author prose for each title + setup + option-text + option-tooltip inline in the JSON. Keep setups ≤ 160 chars, option text ≤ 60 chars, tooltips ≤ 80 chars.

**Sample — `home_evening_unwind_dice`:**

```json
{
  "id": "home_evening_unwind_dice",
  "category": "home_evening_unwind",
  "trigger": [],
  "weight": { "base": 1.0 },
  "scope": { "context": "settlement" },
  "title": "{=home_unwind_dice_title}The dice come out",
  "setup": "{=home_unwind_dice_setup}Someone produces a pair of knucklebones and a flat stone. A small ring forms. A sergeant winks at you from the far side.",
  "options": [
    {
      "id": "play_and_win",
      "text": "{=home_unwind_dice_opt_win}Play. Feel the luck in your palm tonight.",
      "tooltip": "{=home_unwind_dice_opt_win_tt}+50 gold if luck holds.",
      "effects": [ { "apply": "give_gold", "amount": 50 } ]
    },
    {
      "id": "play_and_lose",
      "text": "{=home_unwind_dice_opt_lose}Play. You'll win some back tomorrow.",
      "tooltip": "{=home_unwind_dice_opt_lose_tt}-50 gold.",
      "effects": [ { "apply": "give_gold", "amount": -50 } ]
    },
    {
      "id": "walk_away",
      "text": "{=home_unwind_dice_opt_walk}Shake your head. Keep your coin.",
      "tooltip": "{=home_unwind_dice_opt_walk_tt}+lord relation (someone saw).",
      "effects": [ { "apply": "lord_relation_up_small" } ]
    }
  ]
}
```

Complete the remaining 23 storylets following the matrix + sample shape. Close the storylets array + file.

- [ ] **Step 3: Commit**

```bash
git add ModuleData/Enlisted/Storylets/home_evening.json
git commit -F /c/Users/coola/commit.txt
# feat(content): home_evening storylets (25, five per intent)
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 17: Author `home_break_camp` storylets (5)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/home_break_camp.json`

- [ ] **Step 1: Write the storylet file**

Create `ModuleData/Enlisted/Storylets/home_break_camp.json`:

```json
{
  "storylets": [
    {
      "id": "home_break_camp_parting_gate",
      "category": "home_break_camp", "observational": true,
      "trigger": ["lord_at_home_settlement", "lord_has_spouse"],
      "weight": { "base": 2.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_break_gate_title}Column forms up at the gate",
      "setup": "{=home_break_gate_setup}Your lord lingers a moment at the hall door with his wife. When he comes down the steps his face is a mask. The column rides out into first light."
    },
    {
      "id": "home_break_camp_lord_mood_after_home",
      "category": "home_break_camp", "observational": true,
      "trigger": ["lord_at_home_settlement"],
      "weight": { "base": 1.5 },
      "scope": { "context": "settlement" },
      "title": "{=home_break_mood_title}Quieter than yesterday",
      "setup": "{=home_break_mood_setup}Your lord is quieter on the road out than he was on the road in. He does not speak of the night just ended. No one asks."
    },
    {
      "id": "home_break_camp_supply_trains",
      "category": "home_break_camp", "observational": true,
      "trigger": [],
      "weight": { "base": 1.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_break_supply_title}Supply trains form up",
      "setup": "{=home_break_supply_setup}The baggage creaks into the column behind the outriders. A clerk double-checks manifests against the gate-writ. The cart-horses blow in the cold morning air."
    },
    {
      "id": "home_break_camp_sword_edge",
      "category": "home_break_camp", "observational": true,
      "trigger": [],
      "weight": { "base": 0.8 },
      "scope": { "context": "settlement" },
      "title": "{=home_break_edge_title}The sergeant's last word",
      "setup": "{=home_break_edge_setup}Before the gate, the old sergeant checks each blade with a thumb. He grunts at yours, says nothing, walks on. You count that as approval."
    },
    {
      "id": "home_break_camp_rain_on_march",
      "category": "home_break_camp", "observational": true,
      "trigger": ["is_autumn"],
      "weight": { "base": 1.0 },
      "scope": { "context": "settlement" },
      "title": "{=home_break_rain_title}A thin rain starts",
      "setup": "{=home_break_rain_setup}The sky opens an hour out of the gate. Cloaks come up over shoulders, hoods over helms. The road, the rain, the sound of harness — and the town at your back, smaller every minute."
    }
  ]
}
```

- [ ] **Step 2: Commit**

```bash
git add ModuleData/Enlisted/Storylets/home_break_camp.json
git commit -F /c/Users/coola/commit.txt
# feat(content): home_break_camp storylets (5) — departure transitions
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 18: Sync localization strings

**Files:**
- Create: `ModuleData/Enlisted/Localization/strings_home.xml` (generated)

- [ ] **Step 1: Run the sync tool**

```bash
/c/Python313/python.exe Tools/Validation/sync_event_strings.py
```

Expected: tool reports `N new keys added` where N = total unique `{=id}` tokens across `home_*.json` files. Output file: `ModuleData/Enlisted/Localization/strings_home.xml`.

If the tool writes to a different filename (e.g. single `enlisted_strings.xml` aggregate), accept that — read the sync tool's own output to see where it writes.

- [ ] **Step 2: Run the content validator**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: **fails** on Phase 12 because ActivityTypeDefinition + home_* references aren't yet registered with the validator. Capture the failure message verbatim — Task 20 implements the validator extension.

- [ ] **Step 3: Commit just the synced strings file**

```bash
git add ModuleData/Enlisted/Localization/strings_home.xml
# Or whatever the sync tool wrote — verify via git status
git commit -F /c/Users/coola/commit.txt
# feat(content): sync localization keys for home_* storylets
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 19: Debug hotkeys in `DebugToolsBehavior`

**Files:**
- Modify: `src/Debugging/Behaviors/DebugToolsBehavior.cs`

- [ ] **Step 1: Locate the key-binding section**

Open the file. Grep for existing `InputKey.Control` or `Key.H` bindings. Add the new bindings alongside.

- [ ] **Step 2: Add four smoke hotkeys**

In the tick/input-poll method:

```csharp
// Spec 1 smoke helpers — Home surface.
var input = TaleWorlds.InputSystem.Input.Keyboard;  // adjust to match existing pattern
var ctrlShift = input.IsKeyDown(InputKey.LeftControl) && input.IsKeyDown(InputKey.LeftShift);
if (ctrlShift && input.IsKeyPressed(InputKey.H))
{
    ForceStartHomeActivity();
}
else if (ctrlShift && input.IsKeyPressed(InputKey.E))
{
    AdvanceHomeToEvening();
}
else if (ctrlShift && input.IsKeyPressed(InputKey.B))
{
    AdvanceHomeToBreakCamp();
}
else if (ctrlShift && input.IsKeyPressed(InputKey.T))
{
    DumpTriggerEvalTable();
}
```

Then add helper methods:

```csharp
private static void ForceStartHomeActivity()
{
    var runtime = Enlisted.Features.Activities.ActivityRuntime.Instance;
    if (runtime == null) { return; }
    if (runtime.FindActive<Enlisted.Features.Activities.Home.HomeActivity>() != null)
    {
        InformationManager.DisplayMessage(new InformationMessage("HomeActivity already active."));
        return;
    }
    var ctx = Enlisted.Features.Activities.ActivityContext.FromCurrent();
    var activity = new Enlisted.Features.Activities.Home.HomeActivity { Intent = "brood" };
    runtime.Start(activity, ctx);
    InformationManager.DisplayMessage(new InformationMessage("[DEBUG] HomeActivity force-started."));
}

private static void AdvanceHomeToEvening()
{
    var runtime = Enlisted.Features.Activities.ActivityRuntime.Instance;
    var home = runtime?.FindActive<Enlisted.Features.Activities.Home.HomeActivity>();
    if (home == null) { return; }
    while (home.CurrentPhase != null && home.CurrentPhase.Id != "evening")
    {
        runtime.ResolvePlayerChoice(home);
    }
    InformationManager.DisplayMessage(new InformationMessage("[DEBUG] Home advanced to Evening."));
}

private static void AdvanceHomeToBreakCamp()
{
    var runtime = Enlisted.Features.Activities.ActivityRuntime.Instance;
    var home = runtime?.FindActive<Enlisted.Features.Activities.Home.HomeActivity>();
    if (home == null) { return; }
    home.OnBeat("departure_imminent",
        Enlisted.Features.Activities.ActivityContext.FromCurrent());
    InformationManager.DisplayMessage(new InformationMessage("[DEBUG] Home advanced to Break Camp."));
}

private static void DumpTriggerEvalTable()
{
    var ctx = new Enlisted.Features.Content.StoryletContext
    {
        ActivityTypeId = "home_activity",
        CurrentContext = "settlement"
    };
    var log = new System.Text.StringBuilder();
    log.AppendLine("[DEBUG] HomeTriggers eval dump:");
    foreach (var name in Enlisted.Features.Content.TriggerRegistry.Instance.AllRegisteredNames())
    {
        if (!name.StartsWith("home_") && !name.StartsWith("lord_") && !name.StartsWith("party_")
            && !name.StartsWith("settlement_") && !name.StartsWith("kingdom_")
            && !name.StartsWith("is_") && !name.StartsWith("at_")
            && !name.StartsWith("days_enlisted") && !name.StartsWith("rank_")
            && !name.StartsWith("enlisted_")) { continue; }
        var val = Enlisted.Features.Content.TriggerRegistry.Instance.EvaluateOne(name, ctx);
        log.Append("  ").Append(name).Append(" = ").AppendLine(val.ToString());
    }
    Enlisted.Mod.Core.Logging.ModLogger.Expected("HOME-DEBUG", "trigger_dump", log.ToString());
}
```

If `TriggerRegistry.AllRegisteredNames()` isn't exposed, either expose it (one-line addition) or inline the iterated list.

- [ ] **Step 3: Build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Debugging/Behaviors/DebugToolsBehavior.cs
# If TriggerRegistry needed a public accessor addition:
# git add src/Features/Content/TriggerRegistry.cs
git commit -F /c/Users/coola/commit.txt
# feat(debug): Ctrl+Shift+H/E/B/T smoke helpers for HomeActivity
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 20: Extend `validate_content.py` Phase 12 for Spec 1

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Locate Phase 12**

Grep `validate_content.py` for `Phase 12`. It validates storylet references to qualities, triggers, scripted effects. Spec 1 adds:

1. `ActivityTypeDefinition` loader — every `pool` entry must exist in the storylet catalog.
2. Every `trigger` name used in home_*.json must be registered somewhere (grep `TriggerRegistry.Register("name", ...)` in `src/`).
3. Every `scripted_effect` used must exist in `scripted_effects.json`.
4. `chain.when` values must parse to `{known_activity_type}.{known_phase_id}`.

- [ ] **Step 2: Add new validator checks**

Insert a new block into Phase 12, roughly:

```python
def _phase12_validate_activities(ctx, errors):
    """Validates ModuleData/Enlisted/Activities/*.json pool references + phase shapes."""
    activities_dir = os.path.join(ctx.moduledata_root, "Enlisted", "Activities")
    if not os.path.isdir(activities_dir):
        return
    for fname in os.listdir(activities_dir):
        if not fname.endswith(".json"):
            continue
        path = os.path.join(activities_dir, fname)
        try:
            with open(path, "r", encoding="utf-8") as f:
                doc = json.load(f)
        except Exception as e:
            errors.append(f"{path}: failed to parse ({e})")
            continue
        phases = doc.get("phases", [])
        for phase in phases:
            delivery = phase.get("delivery", "auto")
            if delivery not in ("auto", "player_choice"):
                errors.append(f"{path} phase {phase.get('id')}: invalid delivery '{delivery}'")
            for storylet_id in phase.get("pool", []):
                if storylet_id not in ctx.all_storylet_ids:
                    errors.append(
                        f"{path} phase {phase.get('id')}: pool references unknown storylet '{storylet_id}'"
                    )

def _phase12_validate_chain_when(ctx, errors):
    """Validates chain.when = '{typeId}.{phaseId}' across all storylets."""
    # activity_type_phases: dict[typeId, set[phaseId]] computed above
    for storylet_id, storylet in ctx.storylets.items():
        for option in storylet.get("options", []):
            chain = option.get("chain")
            if not chain:
                continue
            when = chain.get("when", "")
            if not when:
                continue
            parts = when.split(".")
            if len(parts) != 2:
                errors.append(f"{storylet_id} option {option.get('id')}: chain.when malformed '{when}'")
                continue
            typeId, phaseId = parts
            if typeId not in ctx.activity_type_phases:
                errors.append(f"{storylet_id}: chain.when references unknown activity type '{typeId}'")
                continue
            if phaseId not in ctx.activity_type_phases[typeId]:
                errors.append(f"{storylet_id}: chain.when references unknown phase '{typeId}.{phaseId}'")
```

Integrate into the existing Phase 12 driver.

- [ ] **Step 3: Run the validator**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: `PASS` (assuming Tasks 13-18 authored correctly). If it fails, fix the content to match; do not loosen the validator.

- [ ] **Step 4: Commit**

```bash
git add Tools/Validation/validate_content.py
git commit -F /c/Users/coola/commit.txt
# feat(validate): Phase 12 extensions for ActivityTypeDefinition + chain.when
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 21: Acceptance smoke — full end-to-end

**Files:** none

This is the final acceptance gate. No commits; only PASS/FAIL of the checklist.

- [ ] **Step 1: Build + validator green**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Both PASS.

- [ ] **Step 2: Launch Bannerlord. Load a save where player is enlisted to a lord.**

- [ ] **Step 3: Scenario A — friendly town entry.**

Follow the lord to a friendly town. When the party enters, verify:
- `Session-A_*.log` shows `HomeActivity` started (search for "HOME-START").
- Open `enlisted_status` menu. Within 1 in-game hour, a headline accordion entry appears (Arrive phase storylet).
- Within 4 in-game hours, 1-2 ambient storylets appear (Settle phase).

- [ ] **Step 4: Scenario B — Evening phase.**

Press `Ctrl+Shift+E` to fast-forward to Evening. Open `enlisted_status`:
- The camp_hub still shows its normal "Camp" option (priority 10).
- 1-5 evening intent slots appear above it (priority 9). Disabled intents are hidden.
- Click any enabled intent. A Modal event appears with 2-3 option buttons.
- Select an option. Effects apply (check `ModLogger` output for quality_add / flag_set).
- The Modal closes; Evening phase completes.

- [ ] **Step 5: Scenario C — party-identity gate.**

Save. Force the lord's party to split off (e.g. be taken prisoner), then watch a DIFFERENT friendly party enter the same settlement. Verify **no** `HomeActivity` starts. `Session-A` log should be silent on this event.

- [ ] **Step 6: Scenario D — save/load mid-activity.**

Enter a settlement. Allow Arrive + Settle to fire. Save. Quit to main menu. Reload. Verify:
- `HomeActivity` still active (hotkey `Ctrl+Shift+T` dumps trigger state; activity should be present).
- `CurrentPhaseIndex`, `Intent`, `EveningFired`, `DigestReleased` restored correctly.
- Continuing through to Evening fires storylets as expected.

- [ ] **Step 7: Scenario E — chain park.**

In Evening, select a brood intent and pick the "write_ledger" option. Verify:
- Session log shows `ParkChain` called with `home_activity.evening` and `home_chain_ledger_rumored`.
- Leave the settlement. Enter a *new* friendly settlement. Advance to Evening. Verify the chain storylet fires (check for `home_chain_ledger_rumored` in the log).

- [ ] **Step 8: Scenario F — OrchestratorOverride untouched.**

Launch a new campaign (or a save where supplies are low). Verify the legacy camp schedule still triggers foraging / etc. — no regression in `CampScheduleManager`.

- [ ] **Step 9: Update spec status to complete**

Edit `docs/superpowers/specs/2026-04-19-enlisted-home-surface-design.md` header; flip **Status** to "Implementation complete ({date}). Commit trail `<first>` → `<last>` on `development`."

- [ ] **Step 10: Final commit**

```bash
git add docs/superpowers/specs/2026-04-19-enlisted-home-surface-design.md
git commit -F /c/Users/coola/commit.txt
# docs(spec): Spec 1 enlisted home surface complete after Task 21 smoke
#
# Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Self-review

**Spec coverage.**

| Spec section | Implementing task |
| :--- | :--- |
| §5.1 `ActivityTypeCatalog` | Task 3 |
| §5.1 `StoryletEventAdapter` | Task 4 |
| §5.1 `HomeActivity` | Task 6 (+ Task 7 save register) |
| §5.1 `HomeActivityStarter` | Task 9 |
| §5.1 `HomeEveningMenuProvider` | Task 10 |
| §5.1 `HomeTriggers` | Task 8 |
| §5.2 `ActivityRuntime` PlayerChoice branch | Task 5 |
| §5.2 `Activity` virtuals | Task 2 |
| §5.2 `Phase.FireIntervalHours` | Task 1 |
| §5.2 `EnlistedMenuBehavior` slot bank | Task 11 |
| §5.2 `EnlistedSaveDefiner` offset 45 | Task 7 |
| §5.3 ModuleData content (all 4 storylet files + activity def) | Tasks 13-17 |
| §5.3 localization sync | Task 18 |
| §12.1 debug hotkeys | Task 19 |
| §12.2 validator extension | Task 20 |
| §12.3 acceptance checklist | Task 21 |
| §3 non-goals (no OrchestratorOverride deletion) | respected throughout |

**Placeholder scan.** Every code step has complete code. Content tasks include one fully-worked example + a matrix the author fills in — that matrix is concrete (IDs, triggers, effect shapes) and not a placeholder. No "TBD" / "implement later" remain.

**Type consistency.** Spot checks:
- `HomeActivity.TypeIdConst = "home_activity"` (Task 6) matches JSON `id: "home_activity"` (Task 13) matches trigger pool prefix checks (Task 10 `StartsWith("home_evening_" + intent + "_")`) matches storylet IDs (Task 16).
- `ActivityTypeCatalog.Parse` reads `"fire_interval_hours"` (Task 3) — matches `Phase.FireIntervalHours` field (Task 1) and JSON (Task 13).
- `ActivityRuntime.FindActive<T>()` (Task 5) called from `HomeEveningMenuProvider` (Task 10), `HomeActivityStarter` (Task 9), `DebugToolsBehavior` (Task 19) — all pass `<HomeActivity>`.
- `StoryletEventAdapter.BuildModal` (Task 4) called from `HomeEveningMenuProvider.OnSlotSelected` (Task 10) — signature `(Storylet, StoryletContext, Activity)` matches.

---

## Plan complete and saved to `docs/superpowers/plans/2026-04-19-enlisted-home-surface.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — Execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints.

**Which approach?**
