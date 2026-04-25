# Orders Surface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Spec 2 — replace the 11-file `src/Features/Orders/` subsystem with a single `OrderActivity` (save offset 46) on the Spec 0 storylet backbone. Reframe Orders as the career-arc text-RPG layer that hums in the background while Bannerlord's foreground campaign plays out: real Bannerlord-native rewards (skill XP, focus points, attribute points, traits, renown, relations on real heroes, native-issue loot from the lord's faction troop tree), six auto-classified duty profiles with 2-hour hysteresis, ten named-order arcs with player-picked intent, five-path emergent specialization with explicit T3/T5/T7 crossroads, and ~260 storylets covering all 18 Bannerlord skill axes from any profile.

**Architecture:** `OrderActivity` runs continuously while `EnlistmentBehavior.IsEnlisted == true`. `DutyProfileBehavior` samples lord state hourly and commits a profile after 2-hour hysteresis. `NamedOrderArcRuntime` splices arc phases onto `OrderActivity` when an arc-tagged storylet's accept option fires (`apply: "start_arc"`); arc state survives save/load via `OrderActivity.OnGameLoaded` reconstructing phases from `ActiveNamedOrder.OrderStoryletId`. `PathScorer` accumulates path scores from intent picks + skill XP gains and fires crossroads storylets at tier-up. `LordStateListener` handles captured / killed / kingdom-changed via `CampaignEvents.HeroPrisonerTaken / HeroPrisonerReleased / HeroKilledEvent`. Loot resolves through the same `BuildCultureTroopTree` walk Quartermaster uses (extracted into a shared `CultureTroopTreeHelper`).

**Tech Stack:** C# 11 on .NET Framework 4.7.2, Bannerlord v1.3.13 (`TaleWorlds.CampaignSystem`, `TaleWorlds.SaveSystem`, `TaleWorlds.Localization`, `TaleWorlds.Core`), Newtonsoft.Json. No unit-test framework — verification via `dotnet build` + `Tools/Validation/validate_content.py` + in-game `DebugToolsBehavior` smoke helpers.

**Spec:** [docs/superpowers/specs/2026-04-20-orders-surface-design.md](../../specs/2026-04-20-orders-surface-design.md) (commits `015128b` + `5d7e311` on `development`).

**Status:** Phase A (Tasks 1-17) complete 2026-04-21 on `development` (commits `2f64137` → `b8039cf`). Phase B §13.1 consumer migrations complete — 14 OrderManager.Instance sites migrated across 5 files. Task 19f (ShowOrdersMenu) now a read-only details card from `OrderDisplayHelper.GetCurrent()`; no remaining `OrderManager.Instance` reads in `src/` outside the deprecated `src/Features/Orders/` subtree. Phase B content authoring + validators + old-subsystem deletion not yet started.

**Known API corrections discovered during execution:** see [§ API corrections appendix](#api-corrections-appendix) at the bottom of this file. The plan's task bodies were written against assumed v1.3.13 APIs; several diverged from the actual decompile and were adapted in the shipped implementation. Future task implementers should verify against `../Decompile/` first.

---

## Phase A task ledger (Tasks 1-17 shipped)

| # | Task | Commit(s) | Notes |
|---|------|-----------|-------|
| 1 | `EnlistmentBehavior.OnTierChanged` event | `2f64137` + `af8425d` + `4a22340` | Guard fix + doc rewrite follow-ups |
| 2 | `EnlistmentBehavior.OnEnlistmentEnded` event | `7cec2d4` | Doc-comment preemptively scoped |
| — | Registry resync after Tasks 1-3 | `af129d0` | Was misnamed `feat(enlistment)…`; subject corrected via non-interactive rebase |
| 3 | `CultureTroopTreeHelper` extraction | `49fdfb4` + `e3388c7` | Style follow-up |
| 4 | `Storylet` schema + `ArcSpec` POCO | `dde8f5c` + `37bbf63` | `ToObject<T>` replaced with graceful `ParseStringList`/`ParseFloatDict` |
| 5 | 8 reward primitives in `EffectExecutor` | `a3b999e` + `a57e0f9` | Key-collision fix on skill-lookup `Expected` calls |
| 6 | 4 runtime-control primitives (3 stubs) | `9445c63` | `grant_item` wired; `start_arc` / `clear_active_named_order` / `grant_random_item_from_pool` stubbed for Phase A |
| 7 | `CombatClassResolver` extraction | `ec61007` + `97fb19f` | Dead `TaleWorlds.Library` using dropped |
| 8 | `OrderActivity` + `NamedOrderState` + inline `NamedOrderArcRuntime` stub | `f68dca5` + `f75ebeb` | `CurrentPhaseIndex` documented as arc-relative; parent namespace using added |
| 9 | `DutyProfileId` enum + `DutyProfileIds` constants | `24bdcfa` | |
| 10 | Save-definer offsets 46/47/85 | `5143ef2` (+ later fix) | Originally registered offset 84 for FormationClass; removed in a follow-up commit — vanilla already registers it. See appendix. |
| 11 | `DutyProfileSelector` pure function | `36a32c9` | All 8 TaleWorlds APIs matched v1.3.13 decompile on first pass |
| 12 | `DutyProfileBehavior` + `ActivityContext.Args` | `0eca58e` + `7a09557` | Review fix: skip beat on same-profile hard-bypass |
| 13 | `EnlistmentLifecycleListener` (narrowed) + `ActivityEndReason.Discharged` + `ActivityRuntime.Stop` | `f3a9ea2` + `a4c6fd9` + `4472a6c` | Plan-vs-grace-period design gap; Revised Option A narrowed scope — see appendix |
| 14 | `NamedOrderArcRuntime` + `StoryletContext.SourceStorylet` + `ActivityRuntime.GetTypes()` | `bde9f54` + `87d683f` | Review fixes: post-splice CurrentPhaseIndex advance + HomeEveningMenuProvider SourceStorylet prime |
| 15 + 15a | `DailyDriftApplicator` + `OrdersNewsFeedThrottle` + `OrderActivity.DriftPendingXp` | `f42522a` + `6c4bb1f` | Review fix: atomic accumulator / XP grant on unknown skill id |
| 16 | `PathScorer` + 5 new `path_*_score` qualities | `b798265` + `fc72bb7` | Review fix: static-event subscription guard (recurring Phase A pattern) |
| 17 | Phase A smoke — all subsystems observed ticking | smoke 2026-04-21 | DutyProfile/DRIFT/PATH heartbeats + OrderActivity start confirmed; DutyProfileBehavior thrashing tuned via beat throttle bump |

---

## Conventions used in every task

- **Commit hygiene.** Every task ends with a commit. Always `git add <specific paths>`; never `git add -A` (another AI session may be editing in parallel).
- **Commit messages.** Repo style `category(scope): short subject`; trailer `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`. Multi-line messages go through a temp file (bash here has no `cat` heredoc) and `git commit -F`.
- **CRLF normalization.** The Write tool creates LF-only files; `.gitattributes` enforces CRLF on `.cs / .csproj / .sln / .ps1`. For **every newly created** file in those extensions, run once (only once — the script double-BOMs on repeat): `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path <path>`. For edits to existing `.cs` files, rely on `.gitattributes` at commit time.
- **csproj registration.** Every new `.cs` must be added to `Enlisted.csproj` as `<Compile Include="src\Features\..." />` — `Activities\*.cs` is non-recursive and does NOT match `Activities\Orders\*.cs`. Same for `Equipment\*.cs`.
- **TaleWorlds API source of truth.** All TaleWorlds API verification happens against `../Decompile/`. Never web search or Context7 for TaleWorlds.
- **ModLogger.** `ModLogger.Surfaced("CATEGORY", "summary string literal", ex, ctx?)` — category matches `[A-Z][A-Z0-9\-]*`, summary is a string literal (registry scanner). Defensive catch-and-log uses `Caught`; known-branch early exits use `Expected(category, key, summary, ctx?)`. Never use the retired `ModLogger.Error` — `validate_content.py` Phase 11 blocks it.
- **Bash on Windows.** Prepend PATH: `export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"`. Python is at `/c/Python313/python.exe`.
- **Build command.** `dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64` (quote the config — bash splits on the space). Close `BannerlordLauncher.exe` first — it holds the DLL open and the csproj post-build mirror fails with MSB3021.
- **Validator.** `/c/Python313/python.exe Tools/Validation/validate_content.py` — run after every content-layer change.
- **Save offset claims.** Before adding a `DefineClassType` / `DefineEnumType` line, grep `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` to confirm the offset is unclaimed. Spec 1 holds 45 / 82 / 83. Spec 2 claims 46 (OrderActivity), 47 (NamedOrderState), 85 (DutyProfileId). FormationClass — originally planned for enum offset 84 — is already registered by TaleWorlds.Core.SaveableCoreTypeDefiner at id 2008; re-registering it crashes Module.Initialize (see appendix).

---

## File structure

### New C# files (9)

```
src/Features/Activities/Orders/
  OrderActivity.cs                  -- Activity subclass, save offset 46; arc reconstruction in OnGameLoaded
  NamedOrderState.cs                -- nullable struct, save offset 47
  DutyProfileSelector.cs            -- pure function MobileParty -> DutyProfileId
  DutyProfileBehavior.cs            -- hourly sampler + 2-hour hysteresis + transition dispatch
  CombatClassResolver.cs            -- shared helper extracted from EnlistedFormationAssignmentBehavior
  NamedOrderArcRuntime.cs           -- splice / unsplice arc phases on OrderActivity
  DailyDriftApplicator.cs           -- daily skill-XP drift via floor storylets
  PathScorer.cs                     -- intent + XP + tier accumulator; fires crossroads
  EnlistmentLifecycleListener.cs    -- enlistment-end prior-service flag capture + kingdom-change observer (narrowed from LordStateListener per Revised Option A — see appendix)

src/Features/Equipment/
  CultureTroopTreeHelper.cs         -- extracted from TroopSelectionManager.BuildCultureTroopTree
```

### Modified C# files (10)

```
src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs    -- rewire DetectFormationFromEquipment to call CombatClassResolver
src/Features/Equipment/Behaviors/TroopSelectionManager.cs               -- rewire BuildCultureTroopTree caller to use helper
src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs                 -- add OnTierChanged + OnEnlistmentEnded events
src/Features/Content/EffectExecutor.cs                                  -- add 12 new scripted-effect primitives
src/Features/Content/Storylet.cs                                        -- add Arc, ProfileRequires, ClassAffinity, IntentAffinity, RequiresCombatClass, RequiresCulture, ExcludesCulture, RequiresLordTrait, ExcludesLordTrait fields
src/Features/Content/StoryletCatalog.cs                                 -- parse new schema fields
src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs                          -- register OrderActivity, NamedOrderState, CombatClass, DutyProfileId
src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs                -- migrate 3 OrderManager.Instance reads
src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs                -- migrate 2 OrderManager.Instance reads
src/Features/Content/ForecastGenerator.cs                               -- migrate 1 OrderManager.Instance read
src/Features/Camp/CampOpportunityGenerator.cs                           -- migrate 1 OrderManager.Instance read
src/Features/Content/ContentOrchestrator.cs                             -- migrate 1 OrderManager.Instance read
src/Debugging/Behaviors/DebugToolsBehavior.cs                           -- add Spec 2 debug hotkeys
Enlisted.csproj                                                          -- register 9 new .cs files; add Loot ItemGroup + AfterBuild copy; remove deleted Orders entries
```

### Deleted C# files (12)

```
src/Features/Orders/Behaviors/OrderManager.cs
src/Features/Orders/Behaviors/OrderProgressionBehavior.cs
src/Features/Orders/OrderCatalog.cs
src/Features/Orders/PromptCatalog.cs
src/Features/Orders/Models/Order.cs
src/Features/Orders/Models/OrderPrompt.cs
src/Features/Orders/Models/OrderConsequence.cs
src/Features/Orders/Models/OrderOutcome.cs
src/Features/Orders/Models/OrderRequirement.cs
src/Features/Orders/Models/PhaseRecap.cs
src/Features/Orders/Models/PromptOutcome.cs
src/Features/Orders/Models/PromptOutcomeData.cs
```

### Deleted JSON (19)

```
ModuleData/Enlisted/Orders/orders_t1_t3.json
ModuleData/Enlisted/Orders/orders_t4_t6.json
ModuleData/Enlisted/Orders/orders_t7_t9.json
ModuleData/Enlisted/Orders/order_events/*.json   (16 files)
```

### New ModuleData content (~25 files, ~260 storylets)

```
ModuleData/Enlisted/Activities/duty_profiles.json                   -- 7 entries (6 profiles + imprisoned)
ModuleData/Enlisted/Loot/loot_pools.json                            -- ~12 pools

ModuleData/Enlisted/Storylets/duty_garrisoned.json                  -- ~20 ambient
ModuleData/Enlisted/Storylets/duty_marching.json                    -- ~20
ModuleData/Enlisted/Storylets/duty_besieging.json                   -- ~20
ModuleData/Enlisted/Storylets/duty_raiding.json                     -- ~20
ModuleData/Enlisted/Storylets/duty_escorting.json                   -- ~10
ModuleData/Enlisted/Storylets/duty_wandering.json                   -- ~20
ModuleData/Enlisted/Storylets/duty_imprisoned.json                  -- ~10

ModuleData/Enlisted/Storylets/order_guard_post.json                 -- accept storylet w/ arc block
ModuleData/Enlisted/Storylets/order_guard_post_mid.json             -- ~5 mid-arc storylets
ModuleData/Enlisted/Storylets/order_guard_post_resolve.json         -- ~5 resolve storylets
[... × 10 named-order archetypes; ~30 storylets per archetype = ~100 named-order storylets]

ModuleData/Enlisted/Storylets/transition.json                       -- ~15-20 profile-change storylets
ModuleData/Enlisted/Storylets/crossroads.json                       -- ~25-30 path crossroads
ModuleData/Enlisted/Storylets/floor.json                            -- ~50 daily-drift narrative
ModuleData/Enlisted/Storylets/lord_state.json                       -- ~12 (lord_killed, lord_kingdom_changed, etc.)

ModuleData/Languages/strings_orders.xml                             -- regenerated by sync_event_strings.py
```

### Modified tooling (2)

```
Tools/Validation/validate_content.py                                -- Phases 13-17 added
src/Debugging/Behaviors/DebugToolsBehavior.cs                       -- Spec 2 smoke hotkeys
```

---

## Execution order

**Phase A (Tasks 1–17): Backbone, refactors & dual-run.** All new C# scaffolding compiles and registers; old Orders subsystem still runs in parallel (shadow mode). No content authored yet. Smoke verifies QM gear lists unchanged after `BuildCultureTroopTree` extraction and duty profile transitions log correctly.

**Phase B (Tasks 18–43): Content migration + consumer rewire.** Migrate the 8 `OrderManager.Instance` consumer call sites to read from `OrderActivity`. Author all profile + named-order + transition storylets. Implement Phase 13–17 validators. Delete old Orders code. Smoke verifies daily drift, named-order arc completion, transition storylets, all 18 skills receive XP.

**Phase C (Tasks 44–52): Path system + crossroads.** PathScorer's intent / skill-level / tier subscriptions are already wired in Phase A (Tasks 14 + 16 + `aa745dc`). Phase C authors the crossroads storylets, wires `committed_path` + `prior_service_*` emission, and replaces `PathScorer.OnTierChanged`'s log-only body with a crossroads dispatcher. Smoke verifies crossroads fire correctly with score-based path leader detection.

**Phase D (Tasks 53–60): Polish, save-load & playtest.** Floor storylets, lord-state edge cases, culture / lord-trait variant overlays, debug hotkeys, save-load arc reconstruction test, final playtest scenarios A–G, verification doc.

Within a phase, some tasks can parallelize (noted where applicable). Phase order is strict.

---

## Phase A — Backbone, refactors & dual-run

## Task 1: Add `EnlistmentBehavior.OnTierChanged` event

**Status:** ✅ shipped — `2f64137` + `af8425d` + `4a22340`.

**Files:**
- Modify: `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`

- [ ] **Step 1: Locate the existing event declarations**

Grep for the `OnXPGained` event declared at `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:8451`:

```bash
grep -n "OnXPGained\|OnWagePaid\|OnGracePeriodStarted" src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs
```

The events are clustered around line 8430–8470. Add `OnTierChanged` adjacent to `OnXPGained`.

- [ ] **Step 2: Add the event field**

Insert after the `OnXPGained` event declaration (around line 8451):

```csharp
/// <summary>
///     Fired when EnlistmentTier changes via promotion or demotion.
///     Spec 2 PathScorer subscribes for crossroads firing; Spec 4 will subscribe for promotion ceremony.
///     Params: oldTier, newTier.
/// </summary>
public static event Action<int, int> OnTierChanged;
```

- [ ] **Step 3: Locate the tier-mutation site**

Grep for the field that backs `EnlistmentTier`:

```bash
grep -n "EnlistmentTier\s*=\|_tier\s*=\|SetTier" src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs | head -20
```

Find the public `SetTier(int newTier)` method (or equivalent — Spec 1's CLAUDE.md note mentions `SetTier` as the rank-advancement entry point). The mutation happens there.

- [ ] **Step 4: Fire the event from the mutation site**

In `SetTier` (or the tier-mutation method), capture the old tier before mutating and invoke the event after:

```csharp
public void SetTier(int newTier)
{
    var oldTier = EnlistmentTier;
    if (oldTier == newTier) return;

    EnlistmentTier = newTier;
    // ... existing post-mutation work ...

    try
    {
        OnTierChanged?.Invoke(oldTier, newTier);
    }
    catch (Exception ex)
    {
        ModLogger.Caught("ENLISTMENT", "OnTierChanged listener threw", ex);
    }
}
```

- [ ] **Step 5: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: 0 errors, 0 warnings related to this change.

- [ ] **Step 6: Commit**

```bash
git add src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(enlistment): add OnTierChanged event for downstream consumers

Spec 2 PathScorer subscribes to fire path-crossroads storylets at
T3/T5/T7 tier transitions. Spec 4 (Promotion+Muster) will be the
second consumer for the rank-up ceremony flow.

Pure addition — no logic change to existing tier-mutation path.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 2: Add `EnlistmentBehavior.OnEnlistmentEnded` event

**Status:** ✅ shipped — `7cec2d4`.

**Files:**
- Modify: `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`

- [ ] **Step 1: Locate `StopEnlist`**

Grep for the discharge entry point:

```bash
grep -n "public void StopEnlist\|public.*Discharge\|FinalizePendingDischarge" src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs | head -10
```

Confirmed location (per Spec 2 §11): `StopEnlist(string reason, bool isHonorableDischarge = false, bool retainKingdomDuringGrace = false)` at `EnlistmentBehavior.cs:3333`.

- [ ] **Step 2: Add the event field next to `OnTierChanged`**

After the `OnTierChanged` event added in Task 1:

```csharp
/// <summary>
///     Fired by StopEnlist immediately before tear-down work runs, so subscribers
///     can capture lord/tier/path state before reset. Spec 2 LordStateListener
///     subscribes to write prior_service_* flags. Params: reason, isHonorableDischarge.
/// </summary>
public static event Action<string, bool> OnEnlistmentEnded;
```

- [ ] **Step 3: Fire the event at the top of `StopEnlist`**

Inside `StopEnlist`, immediately after the entry-condition guards (`if (!IsEnlisted) return;` etc.) but BEFORE any tear-down state mutation:

```csharp
public void StopEnlist(string reason, bool isHonorableDischarge = false, bool retainKingdomDuringGrace = false)
{
    try
    {
        ModLogger.Info("ENLISTMENT", $"Service ended: {reason} (Honorable: {isHonorableDischarge})");

        // NEW — fire BEFORE any state reset so subscribers see live lord/tier/path state.
        try
        {
            OnEnlistmentEnded?.Invoke(reason, isHonorableDischarge);
        }
        catch (Exception ex)
        {
            ModLogger.Caught("ENLISTMENT", "OnEnlistmentEnded listener threw", ex);
        }

        _isProcessingDischarge = true;
        // ... existing tear-down work continues unchanged ...
```

- [ ] **Step 4: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 5: Commit**

```bash
git add src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(enlistment): add OnEnlistmentEnded event fired pre-teardown

Spec 2 LordStateListener subscribes to write prior_service_*
flags (culture, rank, path) on the player hero so subsequent
enlistments can fire veteran-flavored content variants.

Pure addition — fires before existing StopEnlist tear-down so
subscribers see live state. Listener exceptions are caught.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 3: Extract `CultureTroopTreeHelper` from `TroopSelectionManager`

**Status:** ✅ shipped — `49fdfb4` + `e3388c7`.

**Files:**
- Create: `src/Features/Equipment/CultureTroopTreeHelper.cs`
- Modify: `src/Features/Equipment/Behaviors/TroopSelectionManager.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Read the existing `BuildCultureTroopTree` method**

Read `src/Features/Equipment/Behaviors/TroopSelectionManager.cs:494-561` to understand the current implementation.

- [ ] **Step 2: Create the new helper file**

Create `src/Features/Equipment/CultureTroopTreeHelper.cs`:

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Enlisted.Features.Equipment
{
    /// <summary>
    /// Shared helper for walking a faction's troop tree. Extracted from
    /// TroopSelectionManager.BuildCultureTroopTree so Spec 2's loot system
    /// (grant_random_item_from_pool with source: faction_troop_tree) and
    /// Spec 5's Quartermaster can both consume the same source of truth
    /// without one owning the other.
    /// </summary>
    public static class CultureTroopTreeHelper
    {
        /// <summary>
        /// Build the culture's troop tree by traversing UpgradeTargets from
        /// BasicTroop and EliteBasicTroop. Returns all reachable non-hero
        /// CharacterObjects of the given culture.
        /// </summary>
        public static List<CharacterObject> BuildCultureTroopTree(CultureObject culture)
        {
            var results = new List<CharacterObject>();
            try
            {
                if (culture == null) { return results; }

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<CharacterObject>();

                void EnqueueIfValid(CharacterObject start)
                {
                    if (start == null) { return; }
                    if (start.Culture != culture) { return; }
                    if (start.IsHero) { return; }
                    if (!visited.Add(start.StringId)) { return; }
                    queue.Enqueue(start);
                }

                EnqueueIfValid(culture.BasicTroop);
                EnqueueIfValid(culture.EliteBasicTroop);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    results.Add(node);

                    try
                    {
                        var upgrades = node.UpgradeTargets;
                        if (upgrades != null)
                        {
                            foreach (var next in upgrades)
                            {
                                if (next != null && next.Culture == culture && !next.IsHero && visited.Add(next.StringId))
                                {
                                    queue.Enqueue(next);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // best-effort; continue on any API differences
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TROOPTREE", "BuildCultureTroopTree failed", ex);
            }
            return results;
        }
    }
}
```

- [ ] **Step 3: Normalize CRLF on the new file (run ONCE)**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Equipment/CultureTroopTreeHelper.cs
```

- [ ] **Step 4: Rewire `TroopSelectionManager.BuildCultureTroopTree` to call the helper**

In `src/Features/Equipment/Behaviors/TroopSelectionManager.cs`, replace the `BuildCultureTroopTree` method body (lines 494-561) with a one-line delegation:

```csharp
private List<CharacterObject> BuildCultureTroopTree(CultureObject culture)
{
    return CultureTroopTreeHelper.BuildCultureTroopTree(culture);
}
```

Add `using Enlisted.Features.Equipment;` at the top of the file if not already present (the helper is in the same namespace `Enlisted.Features.Equipment` — but `TroopSelectionManager` is in `Enlisted.Features.Equipment.Behaviors`, so it needs the explicit using).

- [ ] **Step 5: Register the new file in `Enlisted.csproj`**

Find the `<Compile Include="src\Features\Equipment\..." />` block and add (alphabetically):

```xml
<Compile Include="src\Features\Equipment\CultureTroopTreeHelper.cs" />
```

- [ ] **Step 6: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 7: Smoke — verify QM gear unchanged**

Launch Bannerlord, enlist with any lord, open Quartermaster gear browser. Verify the visible troop options match the pre-refactor list (same culture, same tier gating, same items). Capture before/after screenshots if needed for regression confidence.

- [ ] **Step 8: Commit**

```bash
git add src/Features/Equipment/CultureTroopTreeHelper.cs \
        src/Features/Equipment/Behaviors/TroopSelectionManager.cs \
        Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
refactor(equipment): extract CultureTroopTreeHelper for shared use

Spec 2 loot system (grant_random_item_from_pool with source:
faction_troop_tree) needs the same faction-troop-tree walk
that Quartermaster uses. Promote the previously-private
TroopSelectionManager.BuildCultureTroopTree into a shared static
under Enlisted.Features.Equipment namespace.

TroopSelectionManager becomes a thin one-line delegator. Pure
refactor — zero behavior change. Smoke verified QM gear lists
unchanged after extraction.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 4: Extend `Storylet` schema with Spec 2 fields

**Status:** ✅ shipped — `dde8f5c` + `37bbf63`. See [§ API corrections appendix](#task-4--storyletcatalog-parse-robustness) — `ToObject<T>()` replaced with graceful `ParseStringList`/`ParseFloatDict`.

**Files:**
- Modify: `src/Features/Content/Storylet.cs`
- Modify: `src/Features/Content/StoryletCatalog.cs`

- [ ] **Step 1: Read the existing `Storylet` POCO**

Read `src/Features/Content/Storylet.cs` to confirm current fields.

- [ ] **Step 2: Add the new fields to `Storylet`**

Add these fields (preserve existing ones):

```csharp
// --- Spec 2 additions ---

/// <summary>Duty profile gate. Empty list = any profile. Matches DutyProfileBehavior._committedProfile.</summary>
public List<string> ProfileRequires { get; set; } = new List<string>();

/// <summary>Per-CombatClass selection-weight multipliers. Missing class defaults to 1.0.</summary>
public Dictionary<string, float> ClassAffinity { get; set; } = new Dictionary<string, float>();

/// <summary>Per-Intent selection-weight multipliers. Missing intent defaults to 1.0.</summary>
public Dictionary<string, float> IntentAffinity { get; set; } = new Dictionary<string, float>();

/// <summary>Hard combat-class gate. Empty = no gate. Values: Infantry / Ranged / Cavalry / HorseArcher.</summary>
public List<string> RequiresCombatClass { get; set; } = new List<string>();

/// <summary>Hard culture gate (lord's culture). Empty = no gate.</summary>
public List<string> RequiresCulture { get; set; } = new List<string>();
public List<string> ExcludesCulture { get; set; } = new List<string>();

/// <summary>Hard lord-trait gate (DefaultTraits.* string id, e.g. "Mercy", "Valor"). Empty = no gate.</summary>
public List<string> RequiresLordTrait { get; set; } = new List<string>();
public List<string> ExcludesLordTrait { get; set; } = new List<string>();

/// <summary>Named-order arc declaration. Null on non-arc storylets.</summary>
public ArcSpec Arc { get; set; }
```

Add the `ArcSpec` POCO at the bottom of the file (or in a new file `src/Features/Content/ArcSpec.cs`):

```csharp
public sealed class ArcSpec
{
    public int DurationHours { get; set; }
    public List<ArcPhaseSpec> Phases { get; set; } = new List<ArcPhaseSpec>();
    public string OnTransitionInterruptStorylet { get; set; } = string.Empty;
}

public sealed class ArcPhaseSpec
{
    public string Id { get; set; } = string.Empty;
    public int DurationHours { get; set; }
    public string PoolPrefix { get; set; } = string.Empty;
    public int FireIntervalHours { get; set; }
    public string Delivery { get; set; } = "auto";  // "auto" | "player_choice"
}
```

- [ ] **Step 3: Parse the new fields in `StoryletCatalog`**

Read `src/Features/Content/StoryletCatalog.cs` to find the JSON parse loop. Add field assignments (per-storylet, inside the existing parse loop):

```csharp
ProfileRequires = (storyletObj["profile_requires"]?.ToObject<List<string>>()) ?? new List<string>(),
ClassAffinity   = (storyletObj["class_affinity"]?.ToObject<Dictionary<string, float>>()) ?? new Dictionary<string, float>(),
IntentAffinity  = (storyletObj["intent_affinity"]?.ToObject<Dictionary<string, float>>()) ?? new Dictionary<string, float>(),
RequiresCombatClass = (storyletObj["requires_combat_class"]?.ToObject<List<string>>()) ?? new List<string>(),
RequiresCulture     = (storyletObj["requires_culture"]?.ToObject<List<string>>()) ?? new List<string>(),
ExcludesCulture     = (storyletObj["excludes_culture"]?.ToObject<List<string>>()) ?? new List<string>(),
RequiresLordTrait   = (storyletObj["requires_lord_trait"]?.ToObject<List<string>>()) ?? new List<string>(),
ExcludesLordTrait   = (storyletObj["excludes_lord_trait"]?.ToObject<List<string>>()) ?? new List<string>(),
Arc = ParseArc(storyletObj["arc"] as JObject),
```

Add the `ParseArc` private static method at the bottom of the catalog class:

```csharp
private static ArcSpec ParseArc(JObject arcObj)
{
    if (arcObj == null) { return null; }
    var arc = new ArcSpec
    {
        DurationHours = (int?)arcObj["duration_hours"] ?? 0,
        OnTransitionInterruptStorylet = (string)arcObj["on_transition_interrupt_storylet"] ?? string.Empty,
    };
    var phasesArr = arcObj["phases"] as JArray;
    if (phasesArr != null)
    {
        foreach (JObject p in phasesArr.OfType<JObject>())
        {
            arc.Phases.Add(new ArcPhaseSpec
            {
                Id = (string)p["id"] ?? string.Empty,
                DurationHours = (int?)p["duration_hours"] ?? 0,
                PoolPrefix = (string)p["pool_prefix"] ?? string.Empty,
                FireIntervalHours = (int?)p["fire_interval_hours"] ?? 0,
                Delivery = (string)p["delivery"] ?? "auto",
            });
        }
    }
    return arc;
}
```

If `ArcSpec` lives in its own file, create `src/Features/Content/ArcSpec.cs`, normalize CRLF, and add a `<Compile Include>` line to `Enlisted.csproj`.

- [ ] **Step 4: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 5: Validator — must pass (no content changed yet, but parse extension shouldn't break existing storylets)**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: existing Spec 1 storylets still validate; no new errors.

- [ ] **Step 6: Commit**

```bash
git add src/Features/Content/Storylet.cs src/Features/Content/StoryletCatalog.cs Enlisted.csproj
# include ArcSpec.cs if extracted to own file
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(content): extend Storylet schema with Spec 2 fields

Adds 8 new optional fields to Storylet and the parsing in
StoryletCatalog: ProfileRequires, ClassAffinity (multipliers),
IntentAffinity (multipliers), RequiresCombatClass (hard gate),
RequiresCulture/ExcludesCulture (hard gates), RequiresLordTrait/
ExcludesLordTrait (hard gates), and Arc (named-order arc spec).

All fields default to empty/null so existing Spec 1 storylets
continue to parse identically.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 5: Add 8 character-development reward primitives to `EffectExecutor`

**Status:** ✅ shipped — `a3b999e` + `a57e0f9`. See [§ API corrections appendix](#task-5--herodeveloperaddfocus--addattribute-third-argument) — `AddFocus`/`AddAttribute` need `checkUnspent*: false` third argument; `GainRenownAction.Apply` takes `float`, not `int`.

**Files:**
- Modify: `src/Features/Content/EffectExecutor.cs`

- [ ] **Step 1: Read the existing `EffectExecutor` to understand the dispatch pattern**

Read `src/Features/Content/EffectExecutor.cs` to find the primitive-dispatch switch and the existing `give_gold` / `quality_add` / `set_flag` / `apply_scripted` handlers. New primitives follow the same shape.

- [ ] **Step 2: Add `grant_skill_level` handler**

Inside the dispatch switch, add a case:

```csharp
case "grant_skill_level":
{
    try
    {
        var skillId = (string)effectObj["skill"];
        var amount  = (int?)effectObj["amount"] ?? 0;
        if (string.IsNullOrEmpty(skillId) || amount == 0) { break; }
        var skill = MBObjectManager.Instance.GetObject<SkillObject>(skillId);
        if (skill == null)
        {
            ModLogger.Expected("EFFECT", "skill_not_found", $"grant_skill_level: skill='{skillId}' not in catalog");
            break;
        }
        Hero.MainHero.HeroDeveloper.ChangeSkillLevel(skill, amount);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_skill_level threw", ex); }
    break;
}
```

- [ ] **Step 3: Add `grant_focus_point` handler**

```csharp
case "grant_focus_point":
{
    try
    {
        var skillId = (string)effectObj["skill"];
        var amount  = (int?)effectObj["amount"] ?? 0;
        if (string.IsNullOrEmpty(skillId) || amount == 0) { break; }
        var skill = MBObjectManager.Instance.GetObject<SkillObject>(skillId);
        if (skill == null)
        {
            ModLogger.Expected("EFFECT", "skill_not_found", $"grant_focus_point: skill='{skillId}' not in catalog");
            break;
        }
        Hero.MainHero.HeroDeveloper.AddFocus(skill, amount);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_focus_point threw", ex); }
    break;
}
```

- [ ] **Step 4: Add `grant_unspent_focus` handler**

```csharp
case "grant_unspent_focus":
{
    try
    {
        var amount = (int?)effectObj["amount"] ?? 0;
        if (amount == 0) { break; }
        Hero.MainHero.HeroDeveloper.UnspentFocusPoints += amount;
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_unspent_focus threw", ex); }
    break;
}
```

- [ ] **Step 5: Add `grant_attribute_level` handler**

```csharp
case "grant_attribute_level":
{
    try
    {
        var attrId = (string)effectObj["attribute"];
        var amount = (int?)effectObj["amount"] ?? 0;
        if (string.IsNullOrEmpty(attrId) || amount == 0) { break; }
        var attr = MBObjectManager.Instance.GetObject<CharacterAttribute>(attrId);
        if (attr == null)
        {
            ModLogger.Expected("EFFECT", "attribute_not_found", $"grant_attribute_level: attribute='{attrId}' not in catalog");
            break;
        }
        Hero.MainHero.HeroDeveloper.AddAttribute(attr, amount);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_attribute_level threw", ex); }
    break;
}
```

- [ ] **Step 6: Add `grant_unspent_attribute` handler**

```csharp
case "grant_unspent_attribute":
{
    try
    {
        var amount = (int?)effectObj["amount"] ?? 0;
        if (amount == 0) { break; }
        Hero.MainHero.HeroDeveloper.UnspentAttributePoints += amount;
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_unspent_attribute threw", ex); }
    break;
}
```

- [ ] **Step 7: Add `set_trait_level` handler**

```csharp
case "set_trait_level":
{
    try
    {
        var traitId = (string)effectObj["trait"];
        var level   = (int?)effectObj["level"] ?? 0;
        if (string.IsNullOrEmpty(traitId)) { break; }
        var trait = MBObjectManager.Instance.GetObject<TraitObject>(traitId);
        if (trait == null)
        {
            ModLogger.Expected("EFFECT", "trait_not_found", $"set_trait_level: trait='{traitId}' not in catalog");
            break;
        }
        Hero.MainHero.SetTraitLevel(trait, level);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "set_trait_level threw", ex); }
    break;
}
```

- [ ] **Step 8: Add `grant_renown` handler**

```csharp
case "grant_renown":
{
    try
    {
        var amount = (int?)effectObj["amount"] ?? 0;
        if (amount == 0) { break; }
        GainRenownAction.Apply(Hero.MainHero, amount);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_renown threw", ex); }
    break;
}
```

- [ ] **Step 9: Add `relation_change` handler with slot resolution**

```csharp
case "relation_change":
{
    try
    {
        var slotName = (string)effectObj["target_slot"];
        var delta    = (int?)effectObj["delta"] ?? 0;
        if (string.IsNullOrEmpty(slotName) || delta == 0) { break; }
        if (ctx == null || ctx.ResolvedSlots == null || !ctx.ResolvedSlots.TryGetValue(slotName, out var hero) || hero == null)
        {
            ModLogger.Expected("EFFECT", "relation_target_unresolved", $"relation_change: slot='{slotName}' not resolved on storylet");
            break;
        }
        ChangeRelationAction.ApplyPlayerRelation(hero, delta);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "relation_change threw", ex); }
    break;
}
```

- [ ] **Step 10: Add the required `using` directives at the top of the file**

```csharp
using TaleWorlds.CampaignSystem.Actions;        // ChangeRelationAction, GainRenownAction
using TaleWorlds.Core;                          // SkillObject, CharacterAttribute, TraitObject
using TaleWorlds.ObjectSystem;                  // MBObjectManager
```

- [ ] **Step 11: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 12: Commit**

```bash
git add src/Features/Content/EffectExecutor.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(content): add 8 character-development reward primitives

Spec 2 §7.1: grant_skill_level, grant_focus_point,
grant_unspent_focus, grant_attribute_level, grant_unspent_attribute,
set_trait_level, grant_renown, relation_change.

These mirror Bannerlord's NarrativeMenuOptionArgs vocabulary from
character creation, bringing focus / attribute / trait / renown /
relation rewards into the storylet authoring surface for the first
time.

relation_change resolves target_slot against StoryletContext.
ResolvedSlots — no pseudo-selectors, no ad-hoc strings. Unresolved
slot logs Expected and no-ops.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 6: Add 4 runtime-control primitives to `EffectExecutor`

**Status:** ✅ shipped — `9445c63`. `grant_item` fully wired; `grant_random_item_from_pool` / `start_arc` / `clear_active_named_order` are Phase A stubs that log `Expected` and return (real runtime lands in Tasks 14 and 26).

**Files:**
- Modify: `src/Features/Content/EffectExecutor.cs`

These four primitives wire effects to Spec 2's runtime state. `start_arc` and `clear_active_named_order` reference types created in later tasks (`OrderActivity`, `NamedOrderArcRuntime`); writing the handlers as forward-references is intentional — Tasks 8 + 14 land the missing types and the build only needs to pass once both ends exist. Per the dependency order: implement these handlers as **stubs that log `Expected("EFFECT", "primitive_pre_runtime", ...)`** and return — then revisit in Task 14 to wire to the real runtime.

- [ ] **Step 1: Add `grant_item` handler**

```csharp
case "grant_item":
{
    try
    {
        var itemId = (string)effectObj["item_id"];
        var count  = (int?)effectObj["count"] ?? 1;
        if (string.IsNullOrEmpty(itemId)) { break; }
        var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
        if (item == null)
        {
            ModLogger.Expected("EFFECT", "item_not_found", $"grant_item: item_id='{itemId}' not in catalog");
            break;
        }
        PartyBase.MainParty.ItemRoster.AddToCounts(item, count);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_item threw", ex); }
    break;
}
```

- [ ] **Step 2: Add `grant_random_item_from_pool` handler (Phase A stub)**

The full pool-resolution pipeline lands in Phase B (Task 26 authors `loot_pools.json`; the resolver helper is built alongside). For Phase A, ship the dispatch entry but log a stub:

```csharp
case "grant_random_item_from_pool":
{
    try
    {
        var poolId = (string)effectObj["pool_id"];
        if (string.IsNullOrEmpty(poolId))
        {
            ModLogger.Expected("EFFECT", "pool_id_missing", "grant_random_item_from_pool: pool_id required");
            break;
        }
        // TODO Task 26: wire to LootPoolResolver.ResolveAndGrant(poolId, ctx).
        ModLogger.Expected("EFFECT", "loot_pool_pre_phaseB", $"grant_random_item_from_pool stub: pool='{poolId}'");
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_random_item_from_pool threw", ex); }
    break;
}
```

- [ ] **Step 3: Add `start_arc` handler (Phase A stub)**

```csharp
case "start_arc":
{
    try
    {
        // TODO Task 14: wire to NamedOrderArcRuntime.SpliceArc(ctx.Storylet).
        ModLogger.Expected("EFFECT", "start_arc_pre_runtime", $"start_arc stub on storylet '{ctx?.Storylet?.Id ?? "<null>"}'");
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "start_arc threw", ex); }
    break;
}
```

(If `StoryletContext` doesn't currently carry a `Storylet` reference, log against `ctx?.ActivityTypeId` + `ctx?.PhaseId` instead — adjust per actual fields.)

- [ ] **Step 4: Add `clear_active_named_order` handler (Phase A stub)**

```csharp
case "clear_active_named_order":
{
    try
    {
        // TODO Task 14: wire to NamedOrderArcRuntime.UnspliceArc().
        ModLogger.Expected("EFFECT", "clear_arc_pre_runtime", "clear_active_named_order stub");
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "clear_active_named_order threw", ex); }
    break;
}
```

- [ ] **Step 5: Add `using` directive for `ItemObject` / `PartyBase`**

```csharp
using TaleWorlds.CampaignSystem.Party;          // PartyBase
using TaleWorlds.Core;                          // ItemObject (already added if Task 5 added it)
```

- [ ] **Step 6: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 7: Commit**

```bash
git add src/Features/Content/EffectExecutor.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(content): add 4 runtime-control primitives (Phase A scaffolding)

Spec 2 §7.2: grant_item, grant_random_item_from_pool, start_arc,
clear_active_named_order.

grant_item is fully wired (resolves ItemObject, calls AddToCounts).
The other three are dispatch stubs that log Expected — wired to
real runtime in Tasks 14 (arc) and 26 (loot pool resolver).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 7: Create `CombatClassResolver` + rewire `EnlistedFormationAssignmentBehavior`

**Status:** ✅ shipped — `ec61007` + `97fb19f`. See [§ API corrections appendix](#tasks-7810--formationclass-namespace) — `FormationClass` is in `TaleWorlds.Core`, not `TaleWorlds.Library`.

**Files:**
- Create: `src/Features/Activities/Orders/CombatClassResolver.cs`
- Modify: `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Read the existing detection code**

Read `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:1339-1385` to confirm the current `DetectFormationFromEquipment()` body. The four-branch logic (Horse+Ranged → HorseArcher; Horse → Cavalry; Ranged → Ranged; default → Infantry) is what the resolver inherits.

- [ ] **Step 2: Create the new resolver file**

Create `src/Features/Activities/Orders/CombatClassResolver.cs`:

```csharp
using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;     // FormationClass

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Resolves the player's effective combat class from current battle equipment.
    /// Extracted from EnlistedFormationAssignmentBehavior.DetectFormationFromEquipment
    /// so Spec 2's storylet weighting (class_affinity) and named-order gating
    /// (requires_combat_class) can read the same source of truth as battle formation.
    /// </summary>
    public static class CombatClassResolver
    {
        public static FormationClass Resolve(Hero hero)
        {
            try
            {
                if (hero?.CharacterObject == null) { return FormationClass.Infantry; }
                var character = hero.CharacterObject;

                if (character.IsRanged && character.IsMounted) { return FormationClass.HorseArcher; }
                if (character.IsMounted) { return FormationClass.Cavalry; }
                if (character.IsRanged) { return FormationClass.Ranged; }
                return FormationClass.Infantry;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMBATCLASS", "Resolve failed; defaulting to Infantry", ex);
                return FormationClass.Infantry;
            }
        }
    }
}
```

- [ ] **Step 3: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/CombatClassResolver.cs
```

- [ ] **Step 4: Rewire the behavior to delegate**

In `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs`, replace the body of `DetectFormationFromEquipment()` (lines 1343-1385) with:

```csharp
private FormationClass DetectFormationFromEquipment()
{
    return CombatClassResolver.Resolve(Hero.MainHero);
}
```

Add `using Enlisted.Features.Activities.Orders;` at the top.

- [ ] **Step 5: Register in `Enlisted.csproj`**

Add to the `<Compile>` block (alphabetical):

```xml
<Compile Include="src\Features\Activities\Orders\CombatClassResolver.cs" />
```

- [ ] **Step 6: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 7: Smoke — verify formation assignment unchanged**

Launch Bannerlord, enlist with any lord, get into a battle. Verify the player is auto-assigned to the same formation that pre-refactor logic produced (Infantry default; Cavalry if you have a mount; etc.). The session log should show the same `FORMATIONASSIGNMENT` debug lines as before.

- [ ] **Step 8: Commit**

```bash
git add src/Features/Activities/Orders/CombatClassResolver.cs \
        src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs \
        Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
refactor(activities): extract CombatClassResolver for shared use

Spec 2 storylet weighting (class_affinity) and named-order gating
(requires_combat_class) need to read the same combat-class
classification that battle-formation assignment uses. Promote the
private DetectFormationFromEquipment logic into a shared static
helper under Enlisted.Features.Activities.Orders.

EnlistedFormationAssignmentBehavior becomes a one-line delegator.
Pure refactor — zero behavior change. Smoke verified formation
assignment unchanged.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 8: Create `OrderActivity` class with arc reconstruction

**Status:** ✅ shipped — `f68dca5` + `f75ebeb`. See [§ API corrections appendix](#task-8--storyletcatalog-access-pattern) — `StoryletCatalog.GetById(id)` is static (no `Instance.Get`); stale-arc branch uses `Expected`, not `Caught` with a synthetic exception. Includes an inline `NamedOrderArcRuntime` stub that Task 14 replaces.

**Files:**
- Create: `src/Features/Activities/Orders/OrderActivity.cs`
- Create: `src/Features/Activities/Orders/NamedOrderState.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `NamedOrderState`**

Create `src/Features/Activities/Orders/NamedOrderState.cs`:

```csharp
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;     // FormationClass

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Persisted state for an active named-order arc. Save offset 47.
    /// OrderStoryletId is the reconstruction key used by OrderActivity.OnGameLoaded
    /// to re-resolve the arc's Phase list (which lives in [NonSerialized]
    /// Activity._cachedPhases and would otherwise be lost across save/load).
    /// CombatClassAtAccept is cached so the arc's mid-arc storylet weighting stays
    /// stable even if the player swaps loadout mid-arc (Spec 2 §17 R4).
    /// </summary>
    [Serializable]
    public sealed class NamedOrderState
    {
        public string OrderStoryletId { get; set; } = string.Empty;
        public CampaignTime StartedAt { get; set; } = CampaignTime.Zero;
        public int CurrentPhaseIndex { get; set; }
        public string Intent { get; set; } = string.Empty;
        public FormationClass CombatClassAtAccept { get; set; } = FormationClass.Infantry;
        public Dictionary<string, float> AccumulatedOutcomes { get; set; } = new Dictionary<string, float>();
    }
}
```

- [ ] **Step 2: Create `OrderActivity`**

Create `src/Features/Activities/Orders/OrderActivity.cs`:

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Activities;
using Enlisted.Features.Content;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// The Spec 2 career-arc activity. Save offset 46. Runs continuously while
    /// EnlistmentBehavior.IsEnlisted == true. Holds duty profile state, an optional
    /// active named order, and a cached combat class. Owns arc reconstruction in
    /// OnGameLoaded — see Spec 2 §10.
    /// </summary>
    [Serializable]
    public sealed class OrderActivity : Activity
    {
        public const string TYPE_ID = "order_activity";

        public string CurrentDutyProfile { get; set; } = "wandering";
        public Dictionary<string, int> PendingProfileMatches { get; set; } = new Dictionary<string, int>();
        public int LastProfileSampleHourTick { get; set; }
        public NamedOrderState ActiveNamedOrder { get; set; }    // null when no arc active
        public FormationClass CachedCombatClass { get; set; } = FormationClass.Infantry;
        public int CombatClassResampleHourTick { get; set; }

        public static OrderActivity Instance => ActivityRuntime.Instance?.FindActive<OrderActivity>();

        public override void OnStart(ActivityContext ctx)
        {
            TypeId = TYPE_ID;
            EnsureInitialized();
            CachedCombatClass = CombatClassResolver.Resolve(Hero.MainHero);
        }

        public override void Tick(bool hourly) { /* ambient ticking handled by phase pool selector + DailyDriftApplicator */ }

        public override void OnPhaseEnter(Phase phase) { }
        public override void OnPhaseExit(Phase phase) { }
        public override void Finish(ActivityEndReason reason) { ActiveNamedOrder = null; }

        /// <summary>
        /// Spec 2 §10: arc reconstruction. Base ResolvePhasesFromType restores the
        /// static "on_duty" phase from the type def. If a named order was active at
        /// save time, we must ALSO re-extract the arc block from its source storylet
        /// and re-splice the arc phases so CurrentPhaseIndex resolves correctly.
        /// </summary>
        public void ReconstructArcOnLoad()
        {
            EnsureInitialized();
            if (ActiveNamedOrder == null || string.IsNullOrEmpty(ActiveNamedOrder.OrderStoryletId)) { return; }

            try
            {
                var storylet = StoryletCatalog.Instance?.Get(ActiveNamedOrder.OrderStoryletId);
                if (storylet?.Arc == null)
                {
                    ModLogger.Caught("ARC", "stale_arc_id_on_load",
                        new Exception($"OrderStoryletId='{ActiveNamedOrder.OrderStoryletId}' missing or has no arc; clearing"));
                    ActiveNamedOrder = null;
                    return;
                }
                NamedOrderArcRuntime.RebuildSplicedPhasesOnLoad(this, storylet);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ARC", "reconstruct_threw", ex);
                ActiveNamedOrder = null;
            }
        }

        public void EnsureInitialized()
        {
            PendingProfileMatches ??= new Dictionary<string, int>();
            if (ActiveNamedOrder != null)
            {
                ActiveNamedOrder.AccumulatedOutcomes ??= new Dictionary<string, float>();
            }
        }
    }
}
```

(`NamedOrderArcRuntime.RebuildSplicedPhasesOnLoad` is implemented in Task 14; the static method exists by the time this code runs in production. For Phase A build, declare a stub `public static class NamedOrderArcRuntime { public static void RebuildSplicedPhasesOnLoad(OrderActivity a, Storylet s) { } }` at the bottom of `OrderActivity.cs` if needed and remove during Task 14. **Cleaner:** Implement Task 14's stub immediately as part of this task — see Task 14 Step 1.)

- [ ] **Step 3: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/OrderActivity.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/NamedOrderState.cs
```

- [ ] **Step 4: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Orders\OrderActivity.cs" />
<Compile Include="src\Features\Activities\Orders\NamedOrderState.cs" />
```

- [ ] **Step 5: Build — must pass (after Task 14's stub exists, or with the temporary stub mentioned in Step 2)**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 6: Commit**

```bash
git add src/Features/Activities/Orders/OrderActivity.cs \
        src/Features/Activities/Orders/NamedOrderState.cs \
        Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): create OrderActivity and NamedOrderState

Spec 2 §5: the career-arc Activity subclass. Holds duty profile,
optional active named-order arc, cached combat class, hysteresis
counters. Owns arc reconstruction in ReconstructArcOnLoad — re-
extracts the arc block from the source storylet so CurrentPhaseIndex
resolves correctly across save/load (the base Activity._cachedPhases
field is [NonSerialized]).

EnsureInitialized reseats null dictionaries per Critical Rule
("[Serializable] save stores deserialize with null Dictionary
properties"). Save offset registration lands in Task 10.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 9: Define `DutyProfileId` enum + `imprisoned` profile constant

**Status:** ✅ shipped — `24bdcfa`.

**Files:**
- Create: `src/Features/Activities/Orders/DutyProfileId.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create the enum file**

Create `src/Features/Activities/Orders/DutyProfileId.cs`:

```csharp
namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Six auto-resolved profiles + one externally-forced ("imprisoned") that
    /// LordStateListener sets when the lord is captured. Save enum offset 85.
    ///
    /// DutyProfileSelector.Resolve never returns Imprisoned — only the listener
    /// can force it via DutyProfileBehavior.ForceProfile.
    /// </summary>
    public enum DutyProfileId
    {
        Wandering   = 0,
        Marching    = 1,
        Garrisoned  = 2,
        Escorting   = 3,
        Raiding     = 4,
        Besieging   = 5,
        Imprisoned  = 6
    }

    public static class DutyProfileIds
    {
        public const string Wandering  = "wandering";
        public const string Marching   = "marching";
        public const string Garrisoned = "garrisoned";
        public const string Escorting  = "escorting";
        public const string Raiding    = "raiding";
        public const string Besieging  = "besieging";
        public const string Imprisoned = "imprisoned";
    }
}
```

(String constants alongside the enum — used by storylet `profile_requires` matching and by DutyProfileSelector.)

- [ ] **Step 2: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/DutyProfileId.cs
```

- [ ] **Step 3: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Orders\DutyProfileId.cs" />
```

- [ ] **Step 4: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 5: Commit**

```bash
git add src/Features/Activities/Orders/DutyProfileId.cs Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): define DutyProfileId enum and string constants

Six auto-resolved profiles (Wandering / Marching / Garrisoned /
Escorting / Raiding / Besieging) plus Imprisoned (externally
forced by LordStateListener when the lord is captured).

String constants alongside the enum keep storylet JSON
profile_requires matching against a single source of truth.
Save enum offset registration (85) lands in Task 10.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 10: Register Spec 2 types in `EnlistedSaveDefiner` (offsets 46/47/84/85)

**Status:** ✅ shipped — `5143ef2`. Existing `Dictionary<string,int>` and `Dictionary<string,float>` container registrations already cover Spec 2's new fields — no new `ConstructContainerDefinition` calls needed.

**Files:**
- Modify: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`

- [ ] **Step 1: Grep to confirm offsets 46/47/84/85 are unclaimed**

```bash
grep -n "DefineClassType\|DefineEnumType\|offset" src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
```

Expected: Spec 1 holds 45 (HomeActivity) + 82 (HomeIntent enum) + 83 (PhaseDelivery enum). Offsets 46/47 and 84/85 are unclaimed.

- [ ] **Step 2: Add the four registrations**

In `EnlistedSaveDefiner.DefineClassTypes` (or wherever class types are registered), add after the Spec 1 entries:

```csharp
// --- Spec 2: Orders Surface ---
AddClassDefinition(typeof(Enlisted.Features.Activities.Orders.OrderActivity), 46);
AddClassDefinition(typeof(Enlisted.Features.Activities.Orders.NamedOrderState), 47);
```

In `DefineEnumTypes`:

```csharp
// --- Spec 2: Orders Surface ---
AddEnumDefinition(typeof(TaleWorlds.Library.FormationClass), 84);   // CombatClass alias
AddEnumDefinition(typeof(Enlisted.Features.Activities.Orders.DutyProfileId), 85);
```

(If the actual definer uses different method names — `DefineClassType` vs `AddClassDefinition` — adapt to the existing pattern. Spec 1's CLAUDE.md note used `DefineClassType` / `DefineEnumType`.)

**Note on FormationClass at offset 84:** Spec 2 uses Bannerlord's existing `FormationClass` enum directly (Infantry/Ranged/Cavalry/HorseArcher) rather than minting a new `CombatClass` enum. This avoids duplication and matches what `CombatClassResolver` returns. The save definer registers the TaleWorlds enum at our offset 84 — Bannerlord doesn't auto-register external enums via the mod's definer.

- [ ] **Step 3: Add containers if `OrderActivity` carries `Dictionary<string, int>` / similar**

The base Activity already declares `Dictionary<string, int> PhaseQuality`; that uses Spec 0's existing container registration. New Spec 2 containers (`Dictionary<string, int> PendingProfileMatches`, `Dictionary<string, float> AccumulatedOutcomes`) re-use the same registrations — no new container offsets needed unless a unique generic instantiation surfaces a missing one. If the build complains about a missing container, register the matching `IsContainer` definition adjacent to the existing ones.

- [ ] **Step 4: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 5: Smoke — confirm "Cannot Create Save" doesn't appear**

Launch Bannerlord, start a new campaign, enlist, **save the game**. If save offset registration is wrong, save fails with "Cannot Create Save" red banner. Reload the save — verify it loads cleanly and `OrderActivity` instance state restores (will be at default values until the runtime is wired in Task 12).

- [ ] **Step 6: Commit**

```bash
git add src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(savesystem): register Spec 2 types at offsets 46/47/84/85

OrderActivity (46), NamedOrderState (47), FormationClass enum (84,
reused as combat class), DutyProfileId enum (85). Reserves the
remaining 48-60 / 86+ band for Specs 3-5 per the Spec 0 convention.

Smoke verified save/load cycle clean with empty OrderActivity state.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 11: Create `DutyProfileSelector` (pure function)

**Status:** ✅ shipped — `36a32c9`. All 8 TaleWorlds APIs (`MobileParty.BesiegedSettlement`/`DefaultBehavior`/`Army.LeaderParty`/`CurrentSettlement`/`IsMoving`/`MapFaction`, `Settlement.MapFaction`, `FactionManager.IsAtWarAgainstFaction`) matched the plan exactly against the v1.3.13 decompile — first-pass clean.

**Files:**
- Create: `src/Features/Activities/Orders/DutyProfileSelector.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create the selector file**

Create `src/Features/Activities/Orders/DutyProfileSelector.cs`:

```csharp
using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Pure function: resolves a duty profile from a MobileParty's current state.
    /// First match wins per the priority order in Spec 2 §6. Never returns
    /// Imprisoned — only LordStateListener can force that via DutyProfileBehavior.
    /// </summary>
    public static class DutyProfileSelector
    {
        public static string Resolve(MobileParty lordParty)
        {
            try
            {
                if (lordParty == null) { return DutyProfileIds.Wandering; }

                // Priority 1: Besieging
                if (lordParty.BesiegedSettlement != null) { return DutyProfileIds.Besieging; }

                // Priority 2: Raiding
                if (lordParty.DefaultBehavior == AiBehavior.RaidSettlement) { return DutyProfileIds.Raiding; }

                // Priority 3: Escorting (subordinate in army)
                if (lordParty.Army != null && lordParty.Army.LeaderParty != lordParty) { return DutyProfileIds.Escorting; }

                // Priority 4: Garrisoned (at friendly settlement, stationary)
                if (lordParty.CurrentSettlement != null && !lordParty.IsMoving)
                {
                    var settlement = lordParty.CurrentSettlement;
                    if (settlement != null && lordParty.MapFaction != null && settlement.MapFaction != null
                        && !FactionManager.IsAtWarAgainstFaction(lordParty.MapFaction, settlement.MapFaction))
                    {
                        return DutyProfileIds.Garrisoned;
                    }
                }

                // Priority 5: Marching
                if (lordParty.IsMoving && lordParty.DefaultBehavior != AiBehavior.Hold)
                {
                    return DutyProfileIds.Marching;
                }

                // Priority 6: fallback
                return DutyProfileIds.Wandering;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTYPROFILE", "Resolve threw; defaulting to wandering", ex);
                return DutyProfileIds.Wandering;
            }
        }
    }
}
```

- [ ] **Step 2: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/DutyProfileSelector.cs
```

- [ ] **Step 3: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Orders\DutyProfileSelector.cs" />
```

- [ ] **Step 4: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 5: Commit**

```bash
git add src/Features/Activities/Orders/DutyProfileSelector.cs Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): create DutyProfileSelector pure resolver

Spec 2 §6: first-match-wins resolution order over six profiles.
Reads MobileParty state cheaply (BesiegedSettlement, DefaultBehavior,
Army, CurrentSettlement + IsMoving, faction-at-war check). Never
returns Imprisoned — that's externally forced by LordStateListener.

Pure function, no state. Consumed by DutyProfileBehavior (Task 12)
on each hourly tick.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 12: Create `DutyProfileBehavior` with hourly sampling + 2-hour hysteresis

**Files:**
- Create: `src/Features/Activities/Orders/DutyProfileBehavior.cs`
- Modify: `Enlisted.csproj`
- Modify: `src/SubModule.cs` (or wherever campaign behaviors are registered)

- [ ] **Step 1: Create the behavior file**

Create `src/Features/Activities/Orders/DutyProfileBehavior.cs`:

```csharp
using System;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Hourly-tick sampler that classifies the lord's party into a duty profile
    /// and commits the transition only after 2 consecutive matching samples
    /// (≈ 2-hour hysteresis). Per Spec 2 §6 and the AI investigation that found
    /// lords flip behavior every 1-6 hours with 3-10% per-tick probability.
    ///
    /// On a committed transition while ActiveNamedOrder != null, fires a
    /// transition storylet from the duty_transition_* pool.
    /// </summary>
    public sealed class DutyProfileBehavior : CampaignBehaviorBase
    {
        private const int HYSTERESIS_THRESHOLD = 2;
        private const int MIN_HOURS_BETWEEN_TRANSITION_BEATS = 4;

        private int _lastTransitionBeatHourTick = int.MinValue;

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore) { /* state lives on OrderActivity */ }

        private void OnHourlyTick()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted) { return; }

                var lordParty = enlistment.EnlistedLord?.PartyBelongedTo;
                if (lordParty == null) { return; }

                var orderActivity = OrderActivity.Instance;
                if (orderActivity == null) { return; }

                var observed = DutyProfileSelector.Resolve(lordParty);
                var committed = orderActivity.CurrentDutyProfile;

                // Hard-trigger bypass: BesiegedSettlement cleared, Army dissolved, RethinkAtNextHourlyTick
                bool hardBypass = ShouldBypassHysteresis(orderActivity, lordParty);

                if (observed == committed && !hardBypass)
                {
                    orderActivity.PendingProfileMatches.Clear();
                    return;
                }

                if (!hardBypass)
                {
                    if (!orderActivity.PendingProfileMatches.TryGetValue(observed, out var count)) { count = 0; }
                    orderActivity.PendingProfileMatches[observed] = count + 1;
                    if (orderActivity.PendingProfileMatches[observed] < HYSTERESIS_THRESHOLD) { return; }
                }

                // Commit
                CommitTransition(orderActivity, committed, observed);
            }
            catch (Exception ex) { ModLogger.Caught("DUTYPROFILE", "OnHourlyTick threw", ex); }
        }

        private static bool ShouldBypassHysteresis(OrderActivity activity, MobileParty lordParty)
        {
            if (activity.CurrentDutyProfile == DutyProfileIds.Besieging && lordParty.BesiegedSettlement == null) return true;
            if (activity.CurrentDutyProfile == DutyProfileIds.Escorting && lordParty.Army == null) return true;
            if (lordParty.Ai != null && lordParty.Ai.RethinkAtNextHourlyTick) return true;
            return false;
        }

        private void CommitTransition(OrderActivity activity, string oldProfile, string newProfile)
        {
            activity.CurrentDutyProfile = newProfile;
            activity.PendingProfileMatches.Clear();

            var nowHour = (int)CampaignTime.Now.ToHours;
            if (nowHour - _lastTransitionBeatHourTick < MIN_HOURS_BETWEEN_TRANSITION_BEATS)
            {
                ModLogger.Expected("DUTYPROFILE", "transition_beat_throttled",
                    $"profile_changed beat coalesced: {oldProfile} → {newProfile}");
                return;
            }
            _lastTransitionBeatHourTick = nowHour;

            ModLogger.Info("DUTYPROFILE", $"profile_changed: {oldProfile} → {newProfile}");

            // profile_changed beats route through StoryDirector as Modal events —
            // those are paced by StoryDirector itself (5-day in-game + 60s wall-clock
            // + per-category cooldown per AGENTS.md #10). NO OrdersNewsFeedThrottle
            // gate here; the modal pathway is the right one for "the lord's context
            // just changed" — that IS the foreground.
            // Transition-storylet dispatch handled by NamedOrderArcRuntime on a beat (Task 14).
            activity.OnBeat("profile_changed", new ActivityContext
            {
                Args = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "old_profile", oldProfile },
                    { "new_profile", newProfile }
                }
            });
        }

        /// <summary>Externally invoked by LordStateListener when the lord is captured.</summary>
        public void ForceProfile(string profileId)
        {
            var activity = OrderActivity.Instance;
            if (activity == null) { return; }
            var old = activity.CurrentDutyProfile;
            activity.CurrentDutyProfile = profileId;
            activity.PendingProfileMatches.Clear();
            ModLogger.Info("DUTYPROFILE", $"profile_forced: {old} → {profileId}");
        }
    }
}
```

(`ActivityContext.Args` may not exist — adapt to whatever bag-of-strings is on the existing context type. If `ActivityContext` only carries the standard fields, extend it with a small `Dictionary<string,string> Args` field as part of this task; that's a Spec-0-extension comparable to the other minor schema additions.)

- [ ] **Step 2: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/DutyProfileBehavior.cs
```

- [ ] **Step 3: Register in `Enlisted.csproj`**

```xml
<Compile Include="src\Features\Activities\Orders\DutyProfileBehavior.cs" />
```

- [ ] **Step 4: Register the behavior in the SubModule**

Find `src/SubModule.cs` (or `EnlistedSubModule.cs`) where `CampaignGameStarter.AddBehavior(...)` calls cluster. Add:

```csharp
starter.AddBehavior(new DutyProfileBehavior());
```

next to the existing Spec 1 `HomeActivityStarter` behavior registration.

- [ ] **Step 5: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 6: Smoke — observe profile transitions in session log**

Launch Bannerlord, enlist with any lord. Open the session log (in `Modules/Enlisted/Debugging/Session-A_*.log`) and look for `DUTYPROFILE` entries. Walk the lord into a town (should commit `garrisoned` after 2 hours), then leave (should commit `marching` after 2 hours). Verify no flicker — if the lord re-thinks rapidly, hysteresis should suppress all but the stable final state.

- [ ] **Step 7: Commit**

```bash
git add src/Features/Activities/Orders/DutyProfileBehavior.cs Enlisted.csproj src/SubModule.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): create DutyProfileBehavior with 2-hour hysteresis

Spec 2 §6: hourly sampler with N=2 hysteresis tames the AI's
3-10%/tick flip probability into stable transitions. Hard-trigger
bypass (BesiegedSettlement cleared, Army dissolved,
RethinkAtNextHourlyTick) commits immediately to honor mass-state
changes that hysteresis would otherwise lag.

Profile-change beats are throttled to ≥4-hour gaps to suppress
spam from worst-case lord chaos. Transition-storylet dispatch
hooks the activity's OnBeat — handler lands in Task 14.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 13: Create `LordStateListener` with corrected hook names

**Files:**
- Create: `src/Features/Activities/Orders/LordStateListener.cs`
- Modify: `Enlisted.csproj`
- Modify: `src/SubModule.cs`

- [ ] **Step 1: Verify hook signatures against decompile**

Open `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs` and confirm:

- `HeroPrisonerTaken` (line 723): `IMbEvent<PartyBase, Hero>` — params `(PartyBase capturer, Hero prisoner)`
- `HeroPrisonerReleased` (line 725): `IMbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool>` — params `(Hero hero, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification)`
- `HeroKilledEvent` (line 707): `IMbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool>` — params `(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)`

If the signatures differ in your local decompile, adapt the listener delegate signatures below to match.

- [ ] **Step 2: Create the listener file**

Create `src/Features/Activities/Orders/LordStateListener.cs`:

```csharp
using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Spec 2 §11: handles lord captured / released / killed / kingdom-changed.
    /// All hook names verified against the v1.3.13 decompile (CampaignEvents.cs:707/723/725).
    /// Subscribes to the new EnlistmentBehavior.OnEnlistmentEnded event to capture
    /// prior_service_* flags before tear-down.
    /// </summary>
    public sealed class LordStateListener : CampaignBehaviorBase
    {
        private DutyProfileBehavior _profileBehavior;

        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);

            EnlistmentBehavior.OnEnlistmentEnded += OnEnlistmentEnded;
        }

        public override void SyncData(IDataStore dataStore) { }

        private static bool IsTheLord(Hero hero)
        {
            return hero != null && EnlistmentBehavior.Instance?.IsEnlisted == true
                && hero == EnlistmentBehavior.Instance.EnlistedLord;
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (!IsTheLord(prisoner)) { return; }
                FlagStore.Instance?.SetFlag("lord_imprisoned");
                ResolveProfileBehavior()?.ForceProfile(DutyProfileIds.Imprisoned);
                ModLogger.Info("LORDSTATE", "Lord captured; switched to imprisoned profile");
            }
            catch (Exception ex) { ModLogger.Caught("LORDSTATE", "OnHeroPrisonerTaken threw", ex); }
        }

        private void OnHeroPrisonerReleased(Hero hero, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification)
        {
            try
            {
                if (!IsTheLord(hero)) { return; }
                FlagStore.Instance?.ClearFlag("lord_imprisoned");
                ResolveProfileBehavior()?.ForceProfile(DutyProfileIds.Wandering);
                ModLogger.Info("LORDSTATE", "Lord released; profile reset to wandering for re-sampling");
            }
            catch (Exception ex) { ModLogger.Caught("LORDSTATE", "OnHeroPrisonerReleased threw", ex); }
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            try
            {
                if (!IsTheLord(victim)) { return; }
                ModLogger.Surfaced("LORDSTATE", "lord killed; service ended", null);
                // Fire farewell storylet (Phase D content); for now, trigger StopEnlist directly.
                EnlistmentBehavior.Instance?.StopEnlist("lord_killed", isHonorableDischarge: true);
            }
            catch (Exception ex) { ModLogger.Caught("LORDSTATE", "OnHeroKilled threw", ex); }
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            try
            {
                var enlistedLord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (enlistedLord == null || enlistedLord.Clan != clan) { return; }
                ModLogger.Info("LORDSTATE", $"Lord's clan changed kingdom: {oldKingdom?.Name} → {newKingdom?.Name}");
                // Cache invalidation handled by per-storylet variant resolution at fire time; nothing to do beyond log.
            }
            catch (Exception ex) { ModLogger.Caught("LORDSTATE", "OnClanChangedKingdom threw", ex); }
        }

        private static void OnEnlistmentEnded(string reason, bool isHonorableDischarge)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                var lord = enlistment?.EnlistedLord;
                if (lord == null) { return; }

                var cultureId = lord.Culture?.StringId ?? "neutral";
                var rank = enlistment.EnlistmentTier;
                FlagStore.Instance?.SetFlag($"prior_service_culture_{cultureId}");
                FlagStore.Instance?.SetFlag($"prior_service_rank_{rank}");

                var committedPath = QualityStore.Instance?.GetString("committed_path") ?? string.Empty;
                if (!string.IsNullOrEmpty(committedPath))
                {
                    FlagStore.Instance?.SetFlag($"prior_service_path_{committedPath}");
                }

                // Tear down OrderActivity
                var activity = OrderActivity.Instance;
                if (activity != null)
                {
                    ActivityRuntime.Instance?.Stop(activity, ActivityEndReason.Discharged);
                }

                ModLogger.Info("LORDSTATE", $"Prior service captured: culture={cultureId}, rank={rank}, path={committedPath}");
            }
            catch (Exception ex) { ModLogger.Caught("LORDSTATE", "OnEnlistmentEnded threw", ex); }
        }

        private DutyProfileBehavior ResolveProfileBehavior()
        {
            return _profileBehavior ??= Campaign.Current?.GetCampaignBehavior<DutyProfileBehavior>();
        }
    }
}
```

(`QualityStore.GetString` and `FlagStore.SetFlag` are Spec 0 APIs — names are illustrative; adapt to actual `QualityStore` / `FlagStore` interfaces. If the stores don't have a "string-valued quality" pattern, store `committed_path` as an int enum value and resolve to string locally. `ActivityRuntime.Stop` may be `Finish` or `End` — match the actual Spec 0 API.)

- [ ] **Step 3: Normalize CRLF**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/LordStateListener.cs
```

- [ ] **Step 4: Register in `Enlisted.csproj` + SubModule**

```xml
<Compile Include="src\Features\Activities\Orders\LordStateListener.cs" />
```

```csharp
starter.AddBehavior(new LordStateListener());
```

- [ ] **Step 5: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 6: Smoke — verify hook subscription doesn't NRE**

Launch, enlist, walk around. The session log should NOT show `LORDSTATE` errors. Optionally: cheat-capture the lord (use a debug command or let them get caught) and verify the `lord captured; switched to imprisoned profile` line appears.

- [ ] **Step 7: Commit**

```bash
git add src/Features/Activities/Orders/LordStateListener.cs Enlisted.csproj src/SubModule.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): create LordStateListener for captured/killed/kingdom-change

Spec 2 §11. Hook names corrected per adversarial review against the
v1.3.13 decompile: CampaignEvents.HeroPrisonerTaken (:723),
HeroPrisonerReleased (:725), HeroKilledEvent (:707),
OnClanChangedKingdomEvent.

Subscribes to the new EnlistmentBehavior.OnEnlistmentEnded event to
capture prior_service_* flags (culture, rank, path) before
StopEnlist's tear-down runs. Tears down OrderActivity via
ActivityRuntime.Stop on discharge.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 14: Create `NamedOrderArcRuntime` with arc splice + load reconstruction

**Files:**
- Create: `src/Features/Activities/Orders/NamedOrderArcRuntime.cs`
- Modify: `src/Features/Content/EffectExecutor.cs` (rewire Phase A stubs from Task 6)
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create the runtime file**

Create `src/Features/Activities/Orders/NamedOrderArcRuntime.cs`:

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Activities;
using Enlisted.Features.Content;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Splices and unsplices arc phases on OrderActivity. SpliceArc is called
    /// by EffectExecutor when a storylet option's effects include start_arc.
    /// UnspliceArc is called by clear_active_named_order or by the abort
    /// path on profile_changed beats.
    ///
    /// RebuildSplicedPhasesOnLoad is called by OrderActivity.ReconstructArcOnLoad
    /// (Task 8) to restore the spliced Phase list from ActiveNamedOrder.
    /// OrderStoryletId, since the base Activity._cachedPhases is [NonSerialized].
    /// </summary>
    public static class NamedOrderArcRuntime
    {
        public static void SpliceArc(Storylet sourceStorylet, string intent)
        {
            try
            {
                if (sourceStorylet?.Arc == null) { return; }
                var activity = OrderActivity.Instance;
                if (activity == null) { return; }
                if (activity.ActiveNamedOrder != null)
                {
                    ModLogger.Expected("ARC", "splice_already_active",
                        $"Cannot splice {sourceStorylet.Id}: order {activity.ActiveNamedOrder.OrderStoryletId} still active");
                    return;
                }

                activity.ActiveNamedOrder = new NamedOrderState
                {
                    OrderStoryletId = sourceStorylet.Id,
                    StartedAt = CampaignTime.Now,
                    CurrentPhaseIndex = 0,
                    Intent = intent ?? string.Empty,
                    CombatClassAtAccept = activity.CachedCombatClass,
                };

                BuildAndSeatPhases(activity, sourceStorylet);
                ModLogger.Info("ARC", $"spliced {sourceStorylet.Id} (intent={intent})");
            }
            catch (Exception ex) { ModLogger.Caught("ARC", "SpliceArc threw", ex); }
        }

        public static void UnspliceArc()
        {
            try
            {
                var activity = OrderActivity.Instance;
                if (activity?.ActiveNamedOrder == null) { return; }
                var orderId = activity.ActiveNamedOrder.OrderStoryletId;
                activity.ActiveNamedOrder = null;
                // Reset Phases to the type def's base phases
                activity.ResolvePhasesFromType(ActivityRuntime.Instance?.GetTypes());
                activity.CurrentPhaseIndex = 0;
                ModLogger.Info("ARC", $"unspliced {orderId}");
            }
            catch (Exception ex) { ModLogger.Caught("ARC", "UnspliceArc threw", ex); }
        }

        public static void RebuildSplicedPhasesOnLoad(OrderActivity activity, Storylet sourceStorylet)
        {
            try
            {
                if (activity?.ActiveNamedOrder == null || sourceStorylet?.Arc == null) { return; }
                BuildAndSeatPhases(activity, sourceStorylet);
                ModLogger.Info("ARC", $"reconstructed {sourceStorylet.Id} on load (phaseIndex={activity.CurrentPhaseIndex})");
            }
            catch (Exception ex) { ModLogger.Caught("ARC", "RebuildSplicedPhasesOnLoad threw", ex); }
        }

        private static void BuildAndSeatPhases(OrderActivity activity, Storylet sourceStorylet)
        {
            // Compose: [base on_duty phase] + [arc phases]
            var combined = new List<Phase>();

            // Re-resolve the base phases from the type def (gives us the on_duty phase)
            activity.ResolvePhasesFromType(ActivityRuntime.Instance?.GetTypes());
            if (activity.Phases != null) { combined.AddRange(activity.Phases); }

            foreach (var arcPhase in sourceStorylet.Arc.Phases)
            {
                combined.Add(new Phase
                {
                    Id = arcPhase.Id,
                    DurationHours = arcPhase.DurationHours,
                    FireIntervalHours = arcPhase.FireIntervalHours,
                    Pool = ResolvePoolByPrefix(arcPhase.PoolPrefix),
                    Delivery = arcPhase.Delivery == "player_choice" ? PhaseDelivery.PlayerChoice : PhaseDelivery.Auto
                });
            }

            activity.Phases = combined;
        }

        private static List<string> ResolvePoolByPrefix(string prefix)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(prefix)) { return result; }
            var catalog = StoryletCatalog.Instance;
            if (catalog == null) { return result; }
            foreach (var s in catalog.All())
            {
                if (s.Id != null && s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(s.Id);
                }
            }
            return result;
        }
    }
}
```

(`StoryletCatalog.Instance.All()` may be `Storylets` or another iterator name — match the actual API. `ActivityRuntime.GetTypes()` may need to be added if the runtime doesn't currently expose its `_types` dict; if so, add a public read-only accessor as a small Spec 0 extension.)

- [ ] **Step 2: Rewire Phase A stubs in `EffectExecutor`**

Edit the `start_arc` and `clear_active_named_order` cases in `EffectExecutor.cs` from Task 6 — replace the `Expected` stubs with real calls:

```csharp
case "start_arc":
{
    try
    {
        if (ctx?.Storylet == null)
        {
            ModLogger.Expected("EFFECT", "start_arc_no_storylet", "start_arc with no storylet on context");
            break;
        }
        var intent = ctx?.SelectedOption?.Intent ?? string.Empty;
        NamedOrderArcRuntime.SpliceArc(ctx.Storylet, intent);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "start_arc threw", ex); }
    break;
}

case "clear_active_named_order":
{
    try { NamedOrderArcRuntime.UnspliceArc(); }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "clear_active_named_order threw", ex); }
    break;
}
```

(Adapt `ctx.Storylet` / `ctx.SelectedOption.Intent` to the actual context model — if the option's intent is on the option object selected by the player and not yet in the context, extend the context to carry it. Track this minor extension as a Spec 0 amendment.)

- [ ] **Step 3: Wire `OrderActivity.ReconstructArcOnLoad` into `ActivityRuntime.OnGameLoaded`**

Modify `src/Features/Activities/ActivityRuntime.cs:71-93` `OnGameLoaded` body to call `OrderActivity.ReconstructArcOnLoad` for each active OrderActivity AFTER `ResolvePhasesFromType`:

```csharp
private void OnGameLoaded(CampaignGameStarter starter)
{
    Instance = this;
    ActivityTypeCatalog.LoadAll();
    foreach (var a in _active)
    {
        a.ResolvePhasesFromType(_types);
    }
    // Spec 2: OrderActivity needs to reconstruct spliced arc phases
    foreach (var a in _active)
    {
        if (a is Enlisted.Features.Activities.Orders.OrderActivity oa)
        {
            oa.ReconstructArcOnLoad();
        }
    }
    // existing PlayerChoice cache restore...
    foreach (var a in _active)
    {
        var phase = a.CurrentPhase;
        if (phase != null && phase.Delivery == PhaseDelivery.PlayerChoice)
        {
            _activeChoicePhase = (a, phase);
            break;
        }
    }
}
```

- [ ] **Step 4: Normalize CRLF + register in csproj**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/NamedOrderArcRuntime.cs
```

```xml
<Compile Include="src\Features\Activities\Orders\NamedOrderArcRuntime.cs" />
```

- [ ] **Step 5: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 6: Smoke — verify no regressions**

Launch, enlist. No content authored yet, so no arcs will splice. Verify session log shows no `ARC` errors.

- [ ] **Step 7: Commit**

```bash
git add src/Features/Activities/Orders/NamedOrderArcRuntime.cs \
        src/Features/Content/EffectExecutor.cs \
        src/Features/Activities/ActivityRuntime.cs \
        Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): create NamedOrderArcRuntime with save-load reconstruction

Spec 2 §10: SpliceArc + UnspliceArc + RebuildSplicedPhasesOnLoad.
The reconstruction path closes the Activity._cachedPhases gap
identified in adversarial review — arc phases survive save/load
by being re-derived from ActiveNamedOrder.OrderStoryletId at
ActivityRuntime.OnGameLoaded.

start_arc and clear_active_named_order primitives in EffectExecutor
rewired from Phase A stubs to real runtime calls.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 15: Create `DailyDriftApplicator` with summary pattern (Phase A: accumulator only)

**Files:**
- Create: `src/Features/Activities/Orders/DailyDriftApplicator.cs`
- Modify: `src/Features/Activities/Orders/OrderActivity.cs` (add `DriftPendingXp` dict)
- Modify: `Enlisted.csproj`
- Modify: `src/SubModule.cs`

Per Spec 2 §17 R10 + the native investigation: drift uses the summary pattern (`CharacterRelationCampaignBehavior.cs:43-79` precedent — accumulate silently, surface ONE summary entry). No "fire one floor storylet per game day" — that breaks under fast-forward. Instead: every game day, accumulate XP into a per-skill dict on `OrderActivity`. A separate flush path (gated by `OrdersNewsFeedThrottle`) drains the accumulator and emits one summary news-feed entry whenever the throttle next allows.

- [ ] **Step 1: Add `DriftPendingXp` field to `OrderActivity`**

Edit `src/Features/Activities/Orders/OrderActivity.cs` (created Task 8). Add field:

```csharp
public Dictionary<string, int> DriftPendingXp { get; set; } = new Dictionary<string, int>();
```

Update `EnsureInitialized()`:

```csharp
public void EnsureInitialized()
{
    PendingProfileMatches ??= new Dictionary<string, int>();
    DriftPendingXp ??= new Dictionary<string, int>();
    if (ActiveNamedOrder != null)
    {
        ActiveNamedOrder.AccumulatedOutcomes ??= new Dictionary<string, float>();
    }
}
```

- [ ] **Step 2: Create the applicator file (Phase A: accumulator + flush, no storylet content yet)**

Create `src/Features/Activities/Orders/DailyDriftApplicator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Daily skill-XP drift via the summary pattern. Each game day adds XP
    /// to OrderActivity.DriftPendingXp. The hourly flush path drains the
    /// accumulator and emits ONE summary news-feed entry — but only when
    /// OrdersNewsFeedThrottle.TryClaim() returns true (default 20s wall-clock
    /// floor + suppression above 4x speed).
    ///
    /// Effect: at 1x the player sees a steady trickle of "the past few days
    /// you've drilled..." entries; at 4x they see one every ~30 real seconds;
    /// at 16x they see nothing (pure background, lord-state Modals only).
    /// </summary>
    public sealed class DailyDriftApplicator : CampaignBehaviorBase
    {
        // Per-profile / per-class skill weights — same shape used by ambient
        // storylets via class_affinity, but here computed in C# since drift is
        // algorithmic, not authored.
        private static readonly Dictionary<string, string[]> ProfileHeavy = new Dictionary<string, string[]>
        {
            { DutyProfileIds.Garrisoned, new[] { "OneHanded", "Polearm", "Athletics", "Charm", "Steward" } },
            { DutyProfileIds.Marching,   new[] { "Athletics", "Riding", "Scouting", "Tactics" } },
            { DutyProfileIds.Besieging,  new[] { "Engineering", "Crossbow", "Bow", "Athletics" } },
            { DutyProfileIds.Raiding,    new[] { "Athletics", "Roguery", "OneHanded", "Riding" } },
            { DutyProfileIds.Escorting,  new[] { "Tactics", "Leadership", "Riding" } },
            { DutyProfileIds.Wandering,  new[] { "Scouting", "Trade", "Roguery" } },
            { DutyProfileIds.Imprisoned, new[] { "Roguery", "Charm" } }
        };

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            try
            {
                if (EnlistmentBehavior.Instance?.IsEnlisted != true) { return; }
                var activity = OrderActivity.Instance;
                if (activity == null) { return; }
                activity.EnsureInitialized();

                if (!ProfileHeavy.TryGetValue(activity.CurrentDutyProfile, out var heavy)) { return; }

                // Pick 2-3 skills weighted toward profile-heavy
                var pickCount = activity.CurrentDutyProfile == DutyProfileIds.Imprisoned ? 1 : 2;
                for (int i = 0; i < pickCount; i++)
                {
                    var skillId = heavy[MBRandom.RandomInt(heavy.Length)];
                    var amount = MBRandom.RandomInt(5, 16);  // 5-15 XP per skill per day
                    if (!activity.DriftPendingXp.ContainsKey(skillId)) { activity.DriftPendingXp[skillId] = 0; }
                    activity.DriftPendingXp[skillId] += amount;

                    // Apply XP immediately (silent — no notification yet; the summary handles UI)
                    var skill = MBObjectManager.Instance.GetObject<SkillObject>(skillId);
                    if (skill != null) { Hero.MainHero.HeroDeveloper.AddSkillXp(skill, amount); }
                }
            }
            catch (Exception ex) { ModLogger.Caught("DRIFT", "OnDailyTick threw", ex); }
        }

        private void OnHourlyTick()
        {
            try
            {
                if (EnlistmentBehavior.Instance?.IsEnlisted != true) { return; }
                var activity = OrderActivity.Instance;
                if (activity?.DriftPendingXp == null || activity.DriftPendingXp.Count == 0) { return; }

                // Throttle check — if we can't emit, accumulator keeps growing; player
                // sees a denser summary on the next allowed slot. No queue pile-up.
                if (!OrdersNewsFeedThrottle.TryClaim()) { return; }

                FlushSummary(activity);
            }
            catch (Exception ex) { ModLogger.Caught("DRIFT", "OnHourlyTick flush threw", ex); }
        }

        private static void FlushSummary(OrderActivity activity)
        {
            var pieces = activity.DriftPendingXp
                .OrderByDescending(kv => kv.Value)
                .Take(4)
                .Select(kv => $"{kv.Key} +{kv.Value}");
            var summary = "Past few days: " + string.Join(", ", pieces) + " XP.";

            // Emit at NewsFeed tier through StoryDirector (no Modal — drift is background).
            StoryDirector.Instance?.EmitCandidate(new StoryCandidate
            {
                SourceId = "orders.drift.summary",
                CategoryId = "orders.drift",
                ProposedTier = StoryTier.NewsFeed,
                EmittedAt = CampaignTime.Now,
                RenderedTitle = "Service notes",
                RenderedBody = summary,
                StoryKey = "orders_drift_summary"
            });

            activity.DriftPendingXp.Clear();
            ModLogger.Info("DRIFT", $"Flushed summary: {summary}");
        }
    }
}
```

(`StoryCandidate` field names — `RenderedTitle`/`RenderedBody`/`SourceId`/`CategoryId`/`ProposedTier`/`EmittedAt`/`StoryKey` — match the AGENTS.md Critical Rule #10 example. Adapt to the actual API surface if names differ.)

- [ ] **Step 2: Normalize CRLF + register in csproj + SubModule**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/DailyDriftApplicator.cs
```

```xml
<Compile Include="src\Features\Activities\Orders\DailyDriftApplicator.cs" />
```

```csharp
starter.AddBehavior(new DailyDriftApplicator());
```

- [ ] **Step 3: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Features/Activities/Orders/DailyDriftApplicator.cs Enlisted.csproj src/SubModule.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): create DailyDriftApplicator (Phase A registered, disabled)

Spec 2 §5. Daily-tick subscriber that will award profile + class
weighted skill XP via floor storylets. Phase A ships the registration
and tick subscription so the wiring is in place; the actual floor-
storylet selection lands in Phase B once content is authored.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 15a: Create `OrdersNewsFeedThrottle` (wall-clock pre-filter for non-modal emissions)

**Files:**
- Create: `src/Features/Activities/Orders/OrdersNewsFeedThrottle.cs`
- Modify: `Enlisted.csproj`

Per Spec 2 §5 + §17 R10. Native investigation confirmed Bannerlord has no global throttle and no fast-forward suppression; Spec 0's `StoryDirector` paces Modal events but non-modal NewsFeed entries pass through unthrottled. This is Spec 2's wall-clock pre-filter for its own non-modal emissions only.

- [ ] **Step 1: Create the file**

```csharp
using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Single-category wall-clock floor on Spec 2's non-modal StoryDirector
    /// emissions (ambient profile storylets + daily drift summaries). Modal
    /// emissions bypass — StoryDirector already paces them per AGENTS.md
    /// Critical Rule #10 (5-day in-game + 60s wall-clock + per-category).
    ///
    /// Drop on overflow; no queueing. At Campaign.SpeedUpMultiplier > 4f
    /// the player has explicitly chosen ludicrous-speed (mod-enabled): we
    /// suppress non-modals entirely so they only see Modal interrupts.
    /// </summary>
    internal static class OrdersNewsFeedThrottle
    {
        private static readonly TimeSpan FLOOR = TimeSpan.FromSeconds(20);
        private static DateTime _lastEmit = DateTime.MinValue;

        public static bool TryClaim()
        {
            try
            {
                var speed = Campaign.Current?.SpeedUpMultiplier ?? 1f;
                if (speed > 4f) { return false; }

                var now = DateTime.UtcNow;
                if (now - _lastEmit < FLOOR) { return false; }
                _lastEmit = now;
                return true;
            }
            catch { return false; }
        }
    }
}
```

- [ ] **Step 2: Normalize CRLF + register**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/OrdersNewsFeedThrottle.cs
```

```xml
<Compile Include="src\Features\Activities\Orders\OrdersNewsFeedThrottle.cs" />
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
git add src/Features/Activities/Orders/OrdersNewsFeedThrottle.cs Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): create OrdersNewsFeedThrottle wall-clock pre-filter

Spec 2 §5 + §17 R10. 20-second wall-clock floor on Spec 2's non-
modal StoryDirector emissions; suppression above 4x speed. Drop
on overflow, no queueing. Modal emissions bypass entirely
(StoryDirector already paces them per AGENTS.md Critical Rule
#10).

Native investigation confirmed Bannerlord has no global throttle
and no fast-forward suppression; this is Spec 2's narrow gap-fill
for the non-modal NewsFeed pathway. Sole consumers: DutyProfile-
Behavior (Task 12) and DailyDriftApplicator (Task 15 / 43).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 16: Create `PathScorer` (Phase A: registered, no crossroads firing)

**Files:**
- Create: `src/Features/Activities/Orders/PathScorer.cs`
- Modify: `Enlisted.csproj`
- Modify: `src/SubModule.cs`

- [ ] **Step 1: Create the scorer file (Phase A: subscriptions + scoring, no crossroads)**

Create `src/Features/Activities/Orders/PathScorer.cs`:

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Spec 2 §9. Accumulates path scores from intent picks (+3) and major
    /// skill-XP gains (+1 per ≥50 XP event). Phase A: scoring works, crossroads
    /// firing on tier-up wired in Phase C Task 46.
    ///
    /// Path map:
    ///   ranger    ← Bow / Scouting / Riding XP; train_hard / diligent intent in marching/wandering
    ///   enforcer  ← OneHanded / Polearm / Athletics / Leadership XP; diligent / train_hard in besieging/garrisoned
    ///   support   ← Medicine / Engineering / Trade / Steward XP; diligent in any
    ///   diplomat  ← Charm / Leadership / Steward XP; sociable intent
    ///   rogue     ← Roguery / Trade XP; slack / scheme intent
    /// </summary>
    public sealed class PathScorer : CampaignBehaviorBase
    {
        private static readonly Dictionary<string, string> SkillToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Bow", "ranger" }, { "Scouting", "ranger" }, { "Riding", "ranger" },
            { "OneHanded", "enforcer" }, { "Polearm", "enforcer" }, { "Athletics", "enforcer" }, { "Leadership", "enforcer" },
            { "Medicine", "support" }, { "Engineering", "support" }, { "Trade", "support" }, { "Steward", "support" },
            { "Charm", "diplomat" },
            { "Roguery", "rogue" }
        };

        private static readonly Dictionary<string, string> IntentToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "diligent", "enforcer" },
            { "train_hard", "ranger" },
            { "sociable", "diplomat" },
            { "scheme", "rogue" },
            { "slack", "rogue" }
        };

        public override void RegisterEvents()
        {
            EnlistmentBehavior.OnXPGained += OnXPGained;
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
            // Intent-pick scoring is invoked by NamedOrderArcRuntime.SpliceArc — see Task 14.
            // Crossroads firing wired in Phase C Task 46.
        }

        public override void SyncData(IDataStore dataStore) { }

        public static void OnIntentPicked(string intent)
        {
            if (string.IsNullOrEmpty(intent)) { return; }
            if (!IntentToPath.TryGetValue(intent, out var path)) { return; }
            BumpPath(path, 3);
        }

        private void OnXPGained(int amount, string source)
        {
            try
            {
                if (amount < 50) { return; }
                // source is something like "Skill:Bow:50". Parse the skill name.
                var skill = ExtractSkillFromSource(source);
                if (string.IsNullOrEmpty(skill)) { return; }
                if (!SkillToPath.TryGetValue(skill, out var path)) { return; }
                BumpPath(path, 1);
            }
            catch (Exception ex) { ModLogger.Caught("PATH", "OnXPGained threw", ex); }
        }

        private void OnTierChanged(int oldTier, int newTier)
        {
            try
            {
                ModLogger.Info("PATH", $"OnTierChanged {oldTier} → {newTier} (crossroads firing wired in Phase C)");
                // Phase C Task 46: query path scores, fire crossroads_<path>_<band>
            }
            catch (Exception ex) { ModLogger.Caught("PATH", "OnTierChanged threw", ex); }
        }

        private static void BumpPath(string path, int amount)
        {
            try
            {
                var qualityKey = $"path_{path}_score";
                var current = QualityStore.Instance?.Get(qualityKey) ?? 0;
                QualityStore.Instance?.Set(qualityKey, Math.Min(100, current + amount));
            }
            catch (Exception ex) { ModLogger.Caught("PATH", "BumpPath threw", ex); }
        }

        private static string ExtractSkillFromSource(string source)
        {
            if (string.IsNullOrEmpty(source)) { return null; }
            // Spec 2 doesn't dictate the source-string format; use a permissive contains check.
            foreach (var skill in SkillToPath.Keys)
            {
                if (source.IndexOf(skill, StringComparison.OrdinalIgnoreCase) >= 0) { return skill; }
            }
            return null;
        }
    }
}
```

(`QualityStore.Get(string)` / `Set(string, int)` are illustrative — match the real Spec 0 API. If `OnXPGained`'s source string format differs, adapt `ExtractSkillFromSource`.)

- [ ] **Step 2: Normalize CRLF + register**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/PathScorer.cs
```

```xml
<Compile Include="src\Features\Activities\Orders\PathScorer.cs" />
```

```csharp
starter.AddBehavior(new PathScorer());
```

- [ ] **Step 3: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Features/Activities/Orders/PathScorer.cs Enlisted.csproj src/SubModule.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): create PathScorer (Phase A scoring; crossroads in Phase C)

Spec 2 §9. Subscribes to EnlistmentBehavior.OnXPGained (≥50 XP
events bump matching path +1) and exposes static OnIntentPicked
called by NamedOrderArcRuntime.SpliceArc (intent picks bump
matching path +3).

Path qualities (path_ranger_score etc.) capped at 100. Crossroads-
firing on OnTierChanged stubbed; full wiring lands Phase C Task 46
once crossroads storylets are authored.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 17: Phase A smoke — backbone wiring + dual-run verification

**Files:**
- No code changes; verification only.

- [ ] **Step 1: Full clean build**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: 0 errors, 0 new warnings.

- [ ] **Step 2: Validator must pass**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: 0 failures (Phase 13–17 validators not yet authored, so they don't run).

- [ ] **Step 3: Launch + smoke — duty profile transitions**

Close BannerlordLauncher, launch the game, start a new campaign or load an existing save where the player is enlisted.

Open `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\Session-A_*.log` after ~10 in-game minutes.

Verify:
- `DUTYPROFILE` log lines appear at hourly cadence
- A profile change (walk into a town, then leave) shows the hysteresis: 2 ticks pending, then commit, then `profile_changed: <old> → <new>`
- No `ARC` errors (no arcs to splice yet)
- No `LORDSTATE` errors
- `DRIFT` log shows `phaseA_disabled` daily

- [ ] **Step 4: Smoke — old Orders subsystem still functional**

Verify the old Orders accordion still appears on the `enlisted_status` menu, the sergeant still issues orders via the existing `OrderManager` flow, and accepting/declining works as it did pre-Phase-A. **The old subsystem and the new shadow-mode system run side-by-side.**

- [ ] **Step 5: Smoke — QM gear unchanged + battle formation unchanged**

Verify QM gear browser still shows the same items as pre-extraction (Task 3). Verify a battle still auto-assigns the player to the same formation as pre-extraction (Task 7).

- [ ] **Step 6: Smoke — save/load cycle**

Save the game, exit to main menu, reload. Verify `OrderActivity` state persists (`CurrentDutyProfile`, `PendingProfileMatches`, `CachedCombatClass`). Reload should not throw "Cannot Create Save" or any save-related error.

- [ ] **Step 7: Mark Phase A complete in plan**

Update the plan's `**Status:**` line at the top of this file:

```markdown
**Status:** Phase A (Tasks 1–17) complete YYYY-MM-DD on `development` (commits `<first>` → `<last>`). Phase B not yet started.
```

- [ ] **Step 8: No commit (verification-only task)**

If smoke uncovered a regression, fix it in a follow-up task and commit; this task itself produces no commit.

---

## Phase B — Content migration + consumer rewire

### Migration overview

Tasks 18–24 migrate the eight `OrderManager.Instance` consumer call sites enumerated in Spec 2 §13.1. **Each task replaces one call site (or a tightly-grouped pair within the same file)** so reverts are clean. After Task 24, `grep -r "OrderManager.Instance" src/` must return zero hits before Task 42 (deletion).

The replacement pattern is consistent. Today's reads access `OrderManager.Instance?.GetCurrentOrder()` (returns `Order` or null) or `OrderManager.Instance?.IsOrderActive`. After migration, each site calls `OrderActivity.Instance?.ActiveNamedOrder` (returns `NamedOrderState` or null), and resolves any display text (title, description, day-of-X) by looking up `ActiveNamedOrder.OrderStoryletId` via `StoryletCatalog.Instance.Get(...)`. **Where the old code rendered "Guard Post (day 2 of 3)", the new code renders "{storylet.title} (hour X of Y)" computed from `StartedAt + arc.duration_hours`.**

A small helper centralizes the title + progress lookup:

```csharp
// src/Features/Activities/Orders/OrderDisplayHelper.cs (created in Task 18, reused by all 8 migrations)
namespace Enlisted.Features.Activities.Orders
{
    public static class OrderDisplayHelper
    {
        public sealed class Display
        {
            public string Title { get; set; } = string.Empty;
            public int HoursElapsed { get; set; }
            public int HoursTotal { get; set; }
            public string Intent { get; set; } = string.Empty;
        }

        public static Display GetCurrent()
        {
            var activity = OrderActivity.Instance;
            if (activity?.ActiveNamedOrder == null) { return null; }

            var state = activity.ActiveNamedOrder;
            var storylet = StoryletCatalog.Instance?.Get(state.OrderStoryletId);
            var title = storylet?.Title ?? storylet?.Id ?? "Order";
            var totalHours = storylet?.Arc?.DurationHours ?? 0;
            var elapsedHours = (int)((CampaignTime.Now - state.StartedAt).ToHours);

            return new Display
            {
                Title = title,
                HoursElapsed = elapsedHours,
                HoursTotal = totalHours,
                Intent = state.Intent
            };
        }

        public static bool IsOrderActive() => OrderActivity.Instance?.ActiveNamedOrder != null;
    }
}
```

Task 18 creates this helper alongside the first migration; Tasks 19–24 simply call its methods.

---

## Task 18: Migrate `EnlistedMenuBehavior.cs:956` (Orders accordion header) + create `OrderDisplayHelper`

**Files:**
- Create: `src/Features/Activities/Orders/OrderDisplayHelper.cs`
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` (single call site)
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create `OrderDisplayHelper`**

Create the file with the contents shown in the migration overview above.

- [ ] **Step 2: Normalize CRLF + register**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/OrderDisplayHelper.cs
```

```xml
<Compile Include="src\Features\Activities\Orders\OrderDisplayHelper.cs" />
```

- [ ] **Step 3: Locate the call site**

Read `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:954-980` to confirm the current `OrderManager.Instance?.GetCurrentOrder()` read. Confirm it computes a header string from the order's title and tracks `_ordersCollapsed` / `_ordersLastSeenOrderId` based on the order's id.

- [ ] **Step 4: Replace the read**

Replace:

```csharp
var currentOrder = OrderManager.Instance?.GetCurrentOrder();
if (currentOrder == null)
{
    _ordersCollapsed = true;
    _ordersLastSeenOrderId = string.Empty;
}
else
{
    var currentId = currentOrder.Id ?? string.Empty;
    var isNewOrder = string.IsNullOrEmpty(_ordersLastSeenOrderId) ||
                     !string.Equals(_ordersLastSeenOrderId, currentId, StringComparison.OrdinalIgnoreCase);
    // ... existing header-text composition uses currentOrder.Title
}
```

with:

```csharp
var current = OrderDisplayHelper.GetCurrent();
if (current == null)
{
    _ordersCollapsed = true;
    _ordersLastSeenOrderId = string.Empty;
}
else
{
    var currentId = OrderActivity.Instance?.ActiveNamedOrder?.OrderStoryletId ?? string.Empty;
    var isNewOrder = string.IsNullOrEmpty(_ordersLastSeenOrderId) ||
                     !string.Equals(_ordersLastSeenOrderId, currentId, StringComparison.OrdinalIgnoreCase);
    // ... header text uses current.Title and current.HoursElapsed/HoursTotal
}
```

Add `using Enlisted.Features.Activities.Orders;` at the top if not present.

- [ ] **Step 5: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 6: Smoke — Orders accordion**

The header still reads "no order" / collapses correctly when no `ActiveNamedOrder`. Once Phase B authors a named-order storylet and one accepts in-game, the header renders the storylet's title with hour-progress.

- [ ] **Step 7: Commit**

```bash
git add src/Features/Activities/Orders/OrderDisplayHelper.cs \
        src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs \
        Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
refactor(menu): migrate Orders accordion header to OrderActivity

Spec 2 §13.1, first of eight consumer migrations. Replaces
OrderManager.Instance?.GetCurrentOrder() with
OrderDisplayHelper.GetCurrent() — a tiny shared helper that
resolves the active named-order storylet's title + hour-progress
from OrderActivity.ActiveNamedOrder + StoryletCatalog.

Header behavior preserved: collapses on no order, expands with
[NEW] marker on order id change. Display now renders hour-of-N
instead of day-of-N (named orders run 8-24 hours, not days).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 19: Migrate 5 display/state sites in `EnlistedMenuBehavior.cs`

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` (5 sites — A through E)
- Modify: `ModuleData/Languages/enlisted_strings.xml` (add `enlisted_orders_tooltip_active_row`)
- Regenerate: `docs/error-codes.md` (line shifts under Surfaced calls)

An audit found 6 remaining `OrderManager.Instance` sites after Task 18. 5 are display/state and covered here; 1 is imperative (deferred — see Site F note below).

- [x] **Step 1: Read all 5 sites**

- Site A: `~1004-1079` — `enlisted_active_order` accordion row (condition + tooltip + label)
- Site B: `~2088-2203` — `BuildPlayerPersonalStatus` Fresh/On duty/Still on duty block
- Site C: `~2381-2465` — `BuildUpcomingSection` current-order forecast line
- Site D: `~3515-3668` — `BuildPlayerNarrativeParagraph` current-duty opening sentence
- Site E: `~4886-4912` — `ToggleOrdersAccordion` null-check guard

- [x] **Step 2: Replace each site**

Each site reads `OrderActivity.Instance?.ActiveNamedOrder` for state and `OrderDisplayHelper.GetCurrent()` for display title. Time-since-started uses `NamedOrderState.StartedAt`. The new model has no Imminent/Mandatory/Pending/Assigned states — named orders are either active or not, so imminent-branch logic is dropped throughout. `BuildBriefPlayerForecast` is parameterless and reads only `CampOpportunityGenerator` and `WorldStateAnalyzer`; the imminent branch and its `IsOrderImminent()` / `OrderManager.Instance` references were deleted in commit `9994b39`.

- [x] **Step 3: Build + validator**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

- [x] **Step 4: Commit**

```bash
git add src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs \
        ModuleData/Languages/enlisted_strings.xml \
        docs/superpowers/plans/2026-04-20-orders-surface.md \
        docs/error-codes.md
git commit -F /c/Users/coola/commit_msg.txt
```

**Site F (ShowOrdersMenu at ~line 5297) is deferred to Task 19f.** It uses imperative/mutating fields (`Issuer`, `Requirements.MinSkills`, `Requirements.MinTraits`) and accept/decline callbacks with no clean equivalent in `OrderActivity` until the storylet Modal flow lands in Tasks 34-35.

---

## Task 19f: Neutralize `ShowOrdersMenu` click handler

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` (ShowOrdersMenu method + 1 doc comment + 1 using)
- Modify: `ModuleData/Languages/enlisted_strings.xml` (4 new keys)
- Regenerate: `docs/error-codes.md`

Converts the last remaining `OrderManager.Instance` consumer in `src/` (outside the deprecated `src/Features/Orders/` subtree) from an imperative accept/decline dialog into a read-only details card. The new model has no accept/decline — named orders start via the storylet Modal flow (Tasks 34-35) and run to completion via the arc.

The outer accordion row at `EnlistedMenuBehavior.cs:1002-1022` is already gated on `OrderDisplayHelper.GetCurrent() != null`, so the dialog is entered only when a named order is active. That made the old "no active orders" branch inside `ShowOrdersMenu` dead code in practice — dropped along with Accept/Decline and the decline-confirm chain.

- [x] **Step 1: Read + rewrite ShowOrdersMenu**

Replaced the method body (previously 116 lines) with a ~35-line read-only details dialog built from `OrderDisplayHelper.GetCurrent()`. Fields displayed: Title (plain first line), Intent (when non-empty), Progress (hours elapsed of total, when total > 0). Single "Close" button; no state mutation, no refresh call.

- [x] **Step 2: Remove stale using + doc-comment reference**

Dropped `using Enlisted.Features.Orders.Behaviors;` (OrderManager was the only import from that namespace after the rewrite). Rewrote the `RefreshEnlistedStatusMenuUi` doc comment to describe current behavior without the specific `OrderManager` name reference (which becomes fiction once Task 42 deletes the old subsystem).

- [x] **Step 3: Add 4 new loc-keys**

Added `{=enlisted_orders_details_title}`, `{=enlisted_orders_details_intent}`, `{=enlisted_orders_details_progress}`, `{=enlisted_orders_details_close}` to `enlisted_strings.xml` adjacent to the existing `enlisted_orders_tooltip_*` entries.

Orphaned loc-keys (left in XML per Task 20 precedent, pruned at Task 43): `orders_title`, `orders_none_available`, `orders_continue`, `orders_active_title`, `orders_accept`, `orders_decline`, `orders_decline_confirm_title`, `orders_decline_confirm_text`, `orders_yes_decline`, `orders_cancel`.

- [x] **Step 4: Build + validator + error-codes regen**

Build clean (0 warnings, 0 errors). Validator passes with warnings. Error-codes registry regenerated.

- [x] **Step 5: Commit**

**Smoke-test caveat:** `OrderDisplayHelper.GetCurrent()` returns null until named-order archetypes author storylets that land in `ActiveNamedOrder` (Tasks 34-35). Until then the outer row never renders → the dialog is unreachable in-game. Code path is statically verified; live smoke deferred to Phase B content authoring.

---

## Task 20: Migrate `EnlistedNewsBehavior.cs` — 4 sites (expanded)

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs` (four sites)

Audit found 4 `OrderManager.Instance` reads, not the originally estimated 2. Three are
Imminent-specific forecast lines whose premise (a pre-issue "coming soon" state) no longer
exists in the new model. One is an Active-order display that migrates cleanly.

- [x] **Site 1: `BuildKingdomForecastLine` (~line 764)**

Entire method was Imminent-specific (strategic-order coming-soon forecast). Stubbed to
`return string.Empty;` — strategic-order cues arrive via StoryDirector Modal events.
Orphan loc-key `{=forecast_strategic_orders}` left in strings XML; pruned at Phase B
cleanup (Task 43).

- [x] **Site 2: `BuildCompanyForecastLine` (~line 796)**

Same pattern at company scope. Stubbed to `return string.Empty;`. Orphan loc-key
`{=forecast_order_imminent}` (company variant) left; pruned at Task 43.

- [x] **Site 3: `BuildPlayerForecastLine` (~line 833)**

Two concerns: scheduled commitments (kept) + imminent orders (dropped). Rewritten to
return `BuildCommitmentLine()` only. Orphan loc-keys `{=forecast_duty_imminent}` and
`{=forecast_order_imminent}` (player branch) left; pruned at Task 43.

- [x] **Site 4: `BuildDailyUnitLine` Priority-0 branch (~line 4212)**

Migrated `OrderManager.Instance` + `OrderCatalog.GetDisplayTitle(currentOrder)` to
`OrderDisplayHelper.IsOrderActive()` + `OrderDisplayHelper.GetCurrent().Title`. Added
`using Enlisted.Features.Activities.Orders;` directive.

- [x] **Build + validate + error-codes regen**

Build succeeded (0 warnings, 0 errors). `validate_content.py` passes with warnings.
Error-codes registry regenerated.

- [x] **Commit**

```bash
git add src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs \
        docs/superpowers/plans/2026-04-20-orders-surface.md \
        docs/error-codes.md
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
refactor(news): migrate order-state reads in EnlistedNewsBehavior

Spec 2 §13.1 sites 7-10 of 11 (expanded). Audit found 4
OrderManager.Instance reads in the file, not 2. Migration:

- BuildKingdomForecastLine: stub to empty string. Imminent
  strategic-order forecasts have no equivalent in the new model;
  storylet Modal events handle strategic-order cues.
- BuildCompanyForecastLine: stub to empty string. Same rationale
  at company scope.
- BuildPlayerForecastLine: drop the imminent-orders branch; keep
  the scheduled-commitments branch (unrelated to orders).
- BuildDailyUnitLine: Priority-0 active-order branch migrated
  to OrderDisplayHelper.IsOrderActive() + GetCurrent().Title.

Three loc-keys (forecast_strategic_orders, forecast_duty_imminent,
forecast_order_imminent) are now orphan; pruned at Phase B
cleanup.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 21: Migrate `ForecastGenerator.cs:66`

**Files:**
- Modify: `src/Features/Content/ForecastGenerator.cs` (single site)

- [ ] **Step 1: Read the call site (~lines 60-90)**

Today renders something like `"On duty: Guard Post (day 2 of 3)"`.

- [ ] **Step 2: Replace with `OrderDisplayHelper.GetCurrent()`**

```csharp
var current = OrderDisplayHelper.GetCurrent();
if (current != null)
{
    forecast.AppendLine($"On duty: {current.Title} (hour {current.HoursElapsed} of {current.HoursTotal})");
}
```

(Adapt to the actual forecast-rendering pattern — `StringBuilder`, `TextObject`, etc.)

- [ ] **Step 3: Build + smoke**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Features/Content/ForecastGenerator.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
refactor(forecast): migrate on-duty line to OrderActivity

Spec 2 §13.1 site 6/8.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 22: Migrate `CampOpportunityGenerator.cs:1598`

**Files:**
- Modify: `src/Features/Camp/CampOpportunityGenerator.cs` (single site)

- [ ] **Step 1: Read the call site (~lines 1590-1610)**

Today gates camp-opportunity availability on `OrderManager.Instance?.IsOrderActive`.

- [ ] **Step 2: Replace**

```csharp
if (OrderDisplayHelper.IsOrderActive())
{
    // Suppress camp opportunities while named order active — same as before
    return;
}
```

- [ ] **Step 3: Build + smoke**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Features/Camp/CampOpportunityGenerator.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
refactor(camp): gate camp opportunities on OrderActivity

Spec 2 §13.1 site 7/8.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 23: Migrate `ContentOrchestrator.cs:571`

**Files:**
- Modify: `src/Features/Content/ContentOrchestrator.cs` (single site)

- [ ] **Step 1: Read the call site (~lines 565-585)**

Today gates ambient-content firing on `OrderManager.Instance?.IsOrderActive`.

- [ ] **Step 2: Replace**

```csharp
if (OrderDisplayHelper.IsOrderActive())
{
    // Suppress ambient content while named order active — preserved semantic
    return false;  // or whatever the existing return value was
}
```

- [ ] **Step 3: Build + smoke**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Features/Content/ContentOrchestrator.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
refactor(orchestrator): gate ambient content on OrderActivity

Spec 2 §13.1 site 8/8 — final consumer migration.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 24: Verify zero remaining `OrderManager.Instance` reads

**Files:**
- No code changes; verification only.

- [ ] **Step 1: Grep**

```bash
grep -rn "OrderManager.Instance\|OrderManager\b" src/ | grep -v "src/Features/Orders/"
```

Expected: zero hits. If any remain, they were missed in Tasks 18-23 — go back and migrate.

- [ ] **Step 2: Grep for indirect references**

```bash
grep -rn "OrderProgressionBehavior\|OrderCatalog\b\|PromptCatalog\b" src/ | grep -v "src/Features/Orders/"
```

Expected: zero hits.

- [ ] **Step 3: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Smoke — full feature parity playthrough**

Enlist with a lord, walk around for ~10 in-game days, accept and decline a few orders (still using the OLD `OrderManager` UI since the NEW system has no content yet — old subsystem still ships unchanged), verify:
- Orders accordion still renders correctly
- Forecast still mentions on-duty
- Camp opportunities still gated correctly
- News feed still populates
- Daily report still mentions order outcomes

If any of these regressed, the migration broke a semantic in one of Tasks 18-23. Fix and re-verify.

- [ ] **Step 5: No commit (verification-only)**

---

## Task 25: Author `duty_profiles.json`

**Status:** ✅ shipped — `85d8b35`. The Spec 1 `<ActivitiesData Include="ModuleData\Enlisted\Activities\*.json"/>` wildcard + matching AfterBuild `Copy` step already covered the new file; no csproj edit was needed. Build deployed `duty_profiles.json` to the install on first build, validator passed on the same run.

**Files:**
- Create: `ModuleData/Enlisted/Activities/duty_profiles.json`
- Modify: `Enlisted.csproj` (add ItemGroup + AfterBuild copy if `Activities/` deploy isn't already wired for new files)

- [ ] **Step 1: Create the file**

Create `ModuleData/Enlisted/Activities/duty_profiles.json`:

```json
{
  "schemaVersion": 1,
  "profiles": [
    { "id": "garrisoned",  "pool_prefix": "duty_garrisoned_",  "daily_drift_skill_count": 2, "daily_drift_xp_range": [5, 15] },
    { "id": "marching",    "pool_prefix": "duty_marching_",    "daily_drift_skill_count": 2, "daily_drift_xp_range": [5, 15] },
    { "id": "besieging",   "pool_prefix": "duty_besieging_",   "daily_drift_skill_count": 3, "daily_drift_xp_range": [8, 20] },
    { "id": "raiding",     "pool_prefix": "duty_raiding_",     "daily_drift_skill_count": 3, "daily_drift_xp_range": [8, 20] },
    { "id": "escorting",   "pool_prefix": "duty_escorting_",   "daily_drift_skill_count": 2, "daily_drift_xp_range": [5, 12] },
    { "id": "wandering",   "pool_prefix": "duty_wandering_",   "daily_drift_skill_count": 2, "daily_drift_xp_range": [4, 10] },
    { "id": "imprisoned",  "pool_prefix": "duty_imprisoned_",  "daily_drift_skill_count": 1, "daily_drift_xp_range": [2, 6] }
  ]
}
```

- [ ] **Step 2: Verify `Activities/` deploy still picks up the new file**

The csproj already has an `<ActivitiesData>` ItemGroup (Spec 1) — confirm it's `<ActivitiesData Include="ModuleData\Enlisted\Activities\*.json" />` (wildcard). If wildcarded, no csproj edit needed. If individually listed, add a `<Compile Include>`-style entry plus the `MakeDir` + `Copy` triplet.

- [ ] **Step 3: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

- [ ] **Step 4: Verify deploy**

```bash
ls "/c/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Enlisted/ModuleData/Enlisted/Activities/duty_profiles.json"
```

Expected: file exists in the deploy target.

- [ ] **Step 5: Validator must pass**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

- [ ] **Step 6: Commit**

```bash
git add ModuleData/Enlisted/Activities/duty_profiles.json Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): author duty_profiles.json (7 entries)

Spec 2 §12. Six auto-resolved profiles + imprisoned. Each declares
the pool_prefix that DutyProfileBehavior reads to find its ambient
storylet pool, plus daily-drift parameters DailyDriftApplicator
will consume.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 26: Author `loot_pools.json` + create `LootPoolResolver`

**Status:** ✅ shipped — `90bef6a`. See [§ API corrections appendix](#task-26--lootpoolresolver-api-corrections) — `ModulePaths.GetContentPath` (not `GetContentRoot`), JSON `"categories"` values parse as `ItemObject.ItemTypeEnum` (not `ItemCategory.StringId`), `(int)item.Tier` cast required (`item.Tier` is `ItemTiers` enum), and the Phase A stub is a dedicated method `DoGrantRandomItemFromPool` (not an inline switch case).

**Files:**
- Create: `ModuleData/Enlisted/Loot/loot_pools.json`
- Create: `src/Features/Activities/Orders/LootPoolResolver.cs`
- Modify: `src/Features/Content/EffectExecutor.cs` (rewire `grant_random_item_from_pool` from Phase A stub)
- Modify: `Enlisted.csproj` (Loot deploy + new .cs)

- [ ] **Step 1: Create the loot pool catalog**

Create `ModuleData/Enlisted/Loot/loot_pools.json`:

```json
{
  "schemaVersion": 1,
  "pools": [
    { "id": "issue_helmet",        "source": "faction_troop_tree", "filter": { "categories": ["HeadArmor"], "max_troop_tier": "player_tier" } },
    { "id": "issue_body",          "source": "faction_troop_tree", "filter": { "categories": ["BodyArmor"], "max_troop_tier": "player_tier" } },
    { "id": "issue_arm",           "source": "faction_troop_tree", "filter": { "categories": ["HandArmor"], "max_troop_tier": "player_tier" } },
    { "id": "issue_leg",           "source": "faction_troop_tree", "filter": { "categories": ["LegArmor"], "max_troop_tier": "player_tier" } },
    { "id": "issue_weapon_melee",  "source": "faction_troop_tree", "filter": { "categories": ["OneHandedWeapon", "TwoHandedWeapon", "Polearm"], "max_troop_tier": "player_tier" } },
    { "id": "issue_weapon_ranged", "source": "faction_troop_tree", "filter": { "categories": ["Bow", "Crossbow", "Thrown"], "max_troop_tier": "player_tier" } },
    { "id": "issue_horse",         "source": "faction_troop_tree", "filter": { "categories": ["Horse"], "max_troop_tier": "player_tier" } },
    { "id": "issue_horse_harness", "source": "faction_troop_tree", "filter": { "categories": ["HorseHarness"], "max_troop_tier": "player_tier" } },

    { "id": "trade_goods_common",  "source": "global_catalog", "filter": { "categories": ["Goods"], "tier_range": [1, 3] } },
    { "id": "narrative_trinkets",  "source": "global_catalog", "filter": { "categories": ["Goods"], "tier_range": [1, 2] } },
    { "id": "foreign_curios",      "source": "global_catalog", "filter": { "categories": ["Goods"], "tier_range": [1, 4] } },
    { "id": "courier_documents",   "source": "global_catalog", "filter": { "categories": ["Goods"], "tier_range": [1, 1] } }
  ]
}
```

- [ ] **Step 2: Create `LootPoolResolver`**

Create `src/Features/Activities/Orders/LootPoolResolver.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Equipment;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Resolves loot-pool ids to ItemObject grants. Faction-troop-tree pools
    /// reuse Spec 5's BuildCultureTroopTree (extracted to CultureTroopTreeHelper
    /// in Task 3); global_catalog pools query MBObjectManager directly.
    /// </summary>
    public static class LootPoolResolver
    {
        private static readonly Dictionary<string, PoolDef> _pools = new Dictionary<string, PoolDef>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded) { return; }
            try
            {
                var paths = ModulePaths.GetContentRoot("Loot");
                var file = Path.Combine(paths, "loot_pools.json");
                if (!File.Exists(file))
                {
                    ModLogger.Expected("LOOT", "no_loot_pools_file", $"loot_pools.json missing at {file}");
                    _loaded = true; return;
                }
                var root = JObject.Parse(File.ReadAllText(file));
                foreach (var poolObj in root["pools"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var def = ParsePool(poolObj);
                    if (def != null) { _pools[def.Id] = def; }
                }
            }
            catch (Exception ex) { ModLogger.Caught("LOOT", "EnsureLoaded threw", ex); }
            finally { _loaded = true; }
        }

        public static void ResolveAndGrant(string poolId)
        {
            try
            {
                EnsureLoaded();
                if (!_pools.TryGetValue(poolId, out var def))
                {
                    ModLogger.Expected("LOOT", "pool_not_found", $"pool='{poolId}' not in loot_pools.json");
                    return;
                }
                var item = PickItem(def);
                if (item == null)
                {
                    ModLogger.Expected("LOOT", "pool_no_match", $"pool='{poolId}' resolved to zero items");
                    return;
                }
                PartyBase.MainParty.ItemRoster.AddToCounts(item, 1);
                ModLogger.Info("LOOT", $"granted {item.StringId} from pool {poolId}");
            }
            catch (Exception ex) { ModLogger.Caught("LOOT", "ResolveAndGrant threw", ex); }
        }

        private static ItemObject PickItem(PoolDef def)
        {
            var candidates = new List<ItemObject>();

            if (def.Source == "faction_troop_tree")
            {
                var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (lord?.Culture == null) { return null; }
                var maxTier = ResolveMaxTier(def);
                var tree = CultureTroopTreeHelper.BuildCultureTroopTree(lord.Culture);
                foreach (var troop in tree)
                {
                    if (troop.Tier > maxTier) { continue; }
                    foreach (var equip in troop.BattleEquipments)
                    {
                        for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                        {
                            var element = equip[(EquipmentIndex)i];
                            if (element.Item == null) { continue; }
                            if (!def.Categories.Contains(element.Item.ItemCategory.StringId, StringComparer.OrdinalIgnoreCase)) { continue; }
                            if (!candidates.Contains(element.Item)) { candidates.Add(element.Item); }
                        }
                    }
                }
            }
            else  // global_catalog
            {
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item.Tier < def.TierMin || item.Tier > def.TierMax) { continue; }
                    if (!def.Categories.Contains(item.ItemCategory.StringId, StringComparer.OrdinalIgnoreCase)) { continue; }
                    candidates.Add(item);
                }
            }

            if (candidates.Count == 0) { return null; }
            // Tier-weighted pick: low tier slightly more likely, no top-shelf flooding.
            return candidates[MBRandom.RandomInt(candidates.Count)];
        }

        private static int ResolveMaxTier(PoolDef def)
        {
            if (def.MaxTroopTier == "player_tier")
            {
                return EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
            }
            if (int.TryParse(def.MaxTroopTier, out var t)) { return t; }
            return 9;
        }

        private static PoolDef ParsePool(JObject obj)
        {
            try
            {
                var def = new PoolDef
                {
                    Id = (string)obj["id"],
                    Source = (string)obj["source"]
                };
                var filter = obj["filter"] as JObject;
                if (filter != null)
                {
                    def.Categories = filter["categories"]?.ToObject<List<string>>() ?? new List<string>();
                    def.MaxTroopTier = (string)filter["max_troop_tier"] ?? string.Empty;
                    var range = filter["tier_range"] as JArray;
                    if (range != null && range.Count == 2)
                    {
                        def.TierMin = (int)range[0];
                        def.TierMax = (int)range[1];
                    }
                    else { def.TierMin = 0; def.TierMax = 9; }
                }
                return def;
            }
            catch { return null; }
        }

        private sealed class PoolDef
        {
            public string Id; public string Source;
            public List<string> Categories = new List<string>();
            public string MaxTroopTier = string.Empty;
            public int TierMin, TierMax;
        }
    }
}
```

(`ModulePaths.GetContentRoot("Loot")` is a project utility — adapt to actual path-resolution helper. Per AGENTS.md Critical Rule #9, use `Path.Combine` for cross-platform.)

- [ ] **Step 3: Rewire `grant_random_item_from_pool` in EffectExecutor**

Replace the Phase A stub from Task 6 Step 2 with:

```csharp
case "grant_random_item_from_pool":
{
    try
    {
        var poolId = (string)effectObj["pool_id"];
        if (string.IsNullOrEmpty(poolId))
        {
            ModLogger.Expected("EFFECT", "pool_id_missing", "grant_random_item_from_pool: pool_id required");
            break;
        }
        LootPoolResolver.ResolveAndGrant(poolId);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "grant_random_item_from_pool threw", ex); }
    break;
}
```

- [ ] **Step 4: Add `Loot` deploy to `Enlisted.csproj`**

Add to ItemGroup section:

```xml
<LootData Include="ModuleData\Enlisted\Loot\*.json" />
```

Add to `AfterBuild` target:

```xml
<MakeDir Directories="$(OutputPath)..\..\ModuleData\Enlisted\Loot" />
<Copy SourceFiles="@(LootData)" DestinationFolder="$(OutputPath)..\..\ModuleData\Enlisted\Loot\" />
```

Add the new `.cs` to Compile entries:

```xml
<Compile Include="src\Features\Activities\Orders\LootPoolResolver.cs" />
```

- [ ] **Step 5: Normalize CRLF + build + validate**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path src/Features/Activities/Orders/LootPoolResolver.cs
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
```

- [ ] **Step 6: Verify deploy**

```bash
ls "/c/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Enlisted/ModuleData/Enlisted/Loot/loot_pools.json"
```

- [ ] **Step 7: Commit**

```bash
git add ModuleData/Enlisted/Loot/loot_pools.json \
        src/Features/Activities/Orders/LootPoolResolver.cs \
        src/Features/Content/EffectExecutor.cs \
        Enlisted.csproj
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(loot): author loot_pools.json + LootPoolResolver

Spec 2 §8 + §11 + §13.3. 12 pools: 8 issue (faction_troop_tree)
+ 4 outlier (global_catalog). Issue pools resolve through
CultureTroopTreeHelper — same source of truth as QM.

LootPoolResolver caches pool defs on first use. PickItem walks
the troop tree's BattleEquipments (numeric loop per Critical Rule
#4) and filters by category + max tier (player tier or fixed).
Global-catalog path queries MBObjectManager directly.

Wires the Phase A grant_random_item_from_pool stub from Task 6.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

### Profile-storylet authoring conventions (Tasks 27–32)

Each profile pool = ~20 ambient storylets covering all 18 Bannerlord skill axes with class-affinity bias. Per Spec 2 §2 and the Phase 13 validator: every (profile, skill) cell needs ≥ 5 covering storylets. Spread the 20 across skills so that:

- ~5 storylets target the profile's "heavy" skills via `class_affinity` for the appropriate combat class (e.g. `garrisoned`'s heavy skills for Ranged class are Bow, Scouting, Athletics)
- ~10 storylets cover the "moderate" skills shared across classes (Charm, Steward, Leadership, Medicine, Trade, Engineering)
- ~5 storylets cover the "thin" skills with low base weight (Roguery, Throwing, Crossbow for non-ranged classes)

**Storylet template** for an ambient profile storylet (write each new storylet from this template):

```jsonc
{
  "id": "duty_<profile>_<descriptor>_<n>",
  "title": "{=duty_<profile>_<descriptor>_<n>_title}<short title>",
  "setup": "{=duty_<profile>_<descriptor>_<n>_setup>}<2-3 sentences of setup prose>",
  "scope": {
    "context": "any",
    "slots": {
      "lord": { "role": "enlisted_lord" },
      "buddy": { "role": "squadmate", "tag": "companion_present" }
    }
  },
  "profile_requires": ["<profile_id>"],
  "class_affinity": { "Ranged": 1.4 },         // per the heavy/moderate/thin guidance
  "intent_affinity": { "diligent": 1.2 },      // optional
  "weight": 1.0,
  "options": [
    {
      "id": "main",
      "text": "{=...}<choice text>",
      "tooltip": "{=...}<≤80-char tooltip per AGENTS.md Critical Rule #7>",
      "effects": [
        { "skill_xp": { "skill": "Bow", "amount": 25 } }
      ],
      "result_text": "{=...}<1-2 sentences of resolution>"
    }
  ]
}
```

**Localization strings:** every `{=key}fallback` pair is sync'd by `Tools/Validation/sync_event_strings.py` into `ModuleData/Languages/strings_orders.xml` after each batch of authoring (Task 41).

**Example heavy storylet** (Bow on `garrisoned` for Ranged):

```jsonc
{
  "id": "duty_garrisoned_archery_drill_1",
  "title": "{=duty_garr_arch_1_t}Archery yard.",
  "setup": "{=duty_garr_arch_1_s}You shoulder the practice bow at the butts. Three rounds of ten before mess.",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "profile_requires": ["garrisoned"],
  "class_affinity": { "Ranged": 1.4, "HorseArcher": 1.2 },
  "intent_affinity": { "train_hard": 1.3, "diligent": 1.1 },
  "weight": 1.0,
  "options": [
    {
      "id": "main",
      "text": "{=duty_garr_arch_1_o}Loose.",
      "tooltip": "{=duty_garr_arch_1_tt}Practice bow. Bow XP. Light fatigue.",
      "effects": [
        { "skill_xp": { "skill": "Bow", "amount": 30 } },
        { "quality_add": { "quality": "fatigue_today", "amount": 1 } }
      ],
      "result_text": "{=duty_garr_arch_1_r}Your shoulders ache. The grouping is tighter than yesterday."
    }
  ]
}
```

**Example moderate storylet** (Charm on `garrisoned`):

```jsonc
{
  "id": "duty_garrisoned_mess_hall_2",
  "title": "{=duty_garr_mess_2_t}Mess hall politics.",
  "setup": "{=duty_garr_mess_2_s}A sergeant from another company sits across from you. You can size him up or talk shop.",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "profile_requires": ["garrisoned"],
  "weight": 1.0,
  "options": [
    {
      "id": "talk_shop",
      "text": "{=duty_garr_mess_2_o1}Talk shop.",
      "tooltip": "{=duty_garr_mess_2_tt1}Charm XP. Maybe a contact.",
      "effects": [ { "skill_xp": { "skill": "Charm", "amount": 22 } } ],
      "result_text": "{=duty_garr_mess_2_r1}You learn his lord owes wages. Worth knowing."
    },
    {
      "id": "size_up",
      "text": "{=duty_garr_mess_2_o2}Size him up.",
      "tooltip": "{=duty_garr_mess_2_tt2}Roguery XP. Risk: scrutiny.",
      "effects": [ { "skill_xp": { "skill": "Roguery", "amount": 18 } }, { "quality_add": { "quality": "scrutiny", "amount": 1 } } ],
      "result_text": "{=duty_garr_mess_2_r2}He notices you noticing. Eats faster."
    }
  ]
}
```

---

## Task 27: Author `duty_garrisoned.json` (~20 storylets)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/duty_garrisoned.json`

- [ ] **Step 1: Author the file**

Use the template + examples above. Cover all 18 skills with ≥ 1 storylet each. Heavy bias per class:
- Infantry: OneHanded, Polearm, Athletics
- Ranged: Bow, Scouting
- Cavalry: Riding, Polearm
- HorseArcher: Bow, Riding

20 storylets, each with title/setup/options/result_text + localization keys + tooltips per AGENTS.md Critical Rule #7. JSON field order per Critical Rule #6 (loc-key fallback adjacent to id).

- [ ] **Step 2: Validate**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: existing Phase 1-12 validators pass. Phase 13 (pool coverage) implemented in Task 36 — ignore for now.

- [ ] **Step 3: Verify deploy**

```bash
ls "/c/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Enlisted/ModuleData/Enlisted/Storylets/duty_garrisoned.json"
```

- [ ] **Step 4: Commit**

```bash
git add ModuleData/Enlisted/Storylets/duty_garrisoned.json
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): author duty_garrisoned.json (20 ambient storylets)

Spec 2 §2 + §12. Covers all 18 skill axes. Heavy bias per class
(Infantry → OneHanded/Polearm/Athletics; Ranged → Bow/Scouting;
Cavalry → Riding/Polearm; HorseArcher → Bow/Riding). Moderate
storylets cover Charm/Steward/Leadership/Medicine/Trade. Thin
slots cover Roguery/Throwing/Crossbow.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Tasks 28–32: Author the remaining five profile pools

**Pattern:** Each task mirrors Task 27's structure for one of the remaining profiles. Author ~20 storylets per pool (10 for `escorting` per Spec 2 §15 Phase B — escorting is a thinner profile).

- **Task 28** — `duty_marching.json` (~20). Heavy: Athletics, Riding, Scouting; bias `Cavalry`/`HorseArcher` higher on mounted-marching variants. **Distinct narrative texture:** road, tents at dusk, campfire chatter, rations. Relevant traits: weather/seasonal flavor.

- **Task 29** — `duty_besieging.json` (~20). Heavy: Engineering, Crossbow, Bow; siege-camp tedium balanced with breach moments. Higher daily-drift XP per profile (per `duty_profiles.json`). **Distinct narrative texture:** earthworks, ladder-building, archer fire from the walls.

- **Task 30** — `duty_raiding.json` (~20). Heavy: Athletics, Roguery, OneHanded; ethical-weight options ("burn the granary" / "spare the village"). Rogue-path-friendly. **Distinct narrative texture:** smoke, civilians, looted goods, prisoners. Many storylets offer `slack` / `scheme` intent paths.

- **Task 31** — `duty_escorting.json` (~10). Subordinate-in-army flavor; lord defers to army-leader's plan. Heavy: Tactics, Leadership, Riding. **Distinct narrative texture:** marching in someone else's column; another lord's officers giving orders.

- **Task 32** — `duty_wandering.json` (~20). Mercenary-band feel; low-direction. Heavy: Scouting, Trade, Roguery. **Distinct narrative texture:** "between contracts," scouting opportunities, side-jobs from rural notables. Many storylets `intent_affinity: { "scheme": 1.3 }`.

For each task:
- [ ] Author the JSON per the template + the profile's heavy/moderate/thin skill table
- [ ] Validate (`validate_content.py` Phases 1-12)
- [ ] Verify deploy
- [ ] Commit with message `content(orders): author <profile>.json (N ambient storylets)`

(Each task ~30-60 minutes of authoring; commit per task.)

---

## Task 33: Author 5 named-order storylets — T1-T3 archetypes

**Files:**
- Create: `ModuleData/Enlisted/Storylets/order_guard_post.json` (+ `_mid.json` + `_resolve.json`)
- Create: `ModuleData/Enlisted/Storylets/order_patrol.json` (+ companions)
- Create: `ModuleData/Enlisted/Storylets/order_fatigue_detail.json` (+ companions)
- Create: `ModuleData/Enlisted/Storylets/order_drill.json` (+ companions)
- Create: `ModuleData/Enlisted/Storylets/order_courier.json` (+ companions)

### Named-order authoring conventions

Each archetype = three files (or one file with three storylet groups, depending on `StoryletCatalog` non-recursive load constraint — use one file per archetype with multiple storylets inside):

1. **Accept storylet** — one storylet with the `arc` block. Setup is the lord/sergeant's offer. Each option is an intent pick + `start_arc` effect; the decline option uses `relation_change` with target_slot=`lord`, delta=−1.
2. **Mid-arc storylets** — ~5 per archetype, keyed `order_<id>_mid_<n>`. Fire during the arc's `mid` phase via the `pool_prefix: "order_<id>_mid_"` resolver.
3. **Resolve storylets** — ~3-5 per archetype keyed `order_<id>_resolve_<intent>`. Branch by intent: per-intent resolve so the diligent player gets a different ending than the slacker.

**Example accept storylet with `arc` block:**

```jsonc
{
  "id": "order_guard_post",
  "title": "{=order_guard_post_t}Guard duty.",
  "setup": "{=order_guard_post_s}The sergeant pulls you aside. 'Guard the east gate tonight.'",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" }, "sergeant": { "role": "settlement_notable" } } },
  "profile_requires": ["garrisoned", "besieging"],
  "tier_min": 1, "tier_max": 9,
  "weight": 1.0,
  "options": [
    {
      "id": "accept_diligent",
      "text": "{=order_guard_post_o1}Yes, sergeant.",
      "tooltip": "{=order_guard_post_tt1}Stand watch by the book. Lord relation +1.",
      "intent": "diligent",
      "effects": [ { "start_arc": {} } ]
    },
    {
      "id": "accept_slack",
      "text": "{=order_guard_post_o2}Fine, I suppose.",
      "tooltip": "{=order_guard_post_tt2}Punch the clock. Scrutiny risk.",
      "intent": "slack",
      "effects": [ { "start_arc": {} } ]
    },
    {
      "id": "accept_scheme",
      "text": "{=order_guard_post_o3}Sure. *Look for an angle.*",
      "tooltip": "{=order_guard_post_tt3}Watch for opportunity. Scrutiny risk + possible coin.",
      "intent": "scheme",
      "effects": [ { "start_arc": {} } ]
    },
    {
      "id": "decline",
      "text": "{=order_guard_post_o4}Not tonight.",
      "tooltip": "{=order_guard_post_tt4}Lord relation −1.",
      "effects": [ { "relation_change": { "target_slot": "lord", "delta": -1 } } ]
    }
  ],
  "arc": {
    "duration_hours": 12,
    "phases": [
      { "id": "mid", "duration_hours": 10, "pool_prefix": "order_guard_post_mid_", "fire_interval_hours": 3, "delivery": "auto" },
      { "id": "resolve", "duration_hours": 2, "pool_prefix": "order_guard_post_resolve_", "fire_interval_hours": 0, "delivery": "auto" }
    ],
    "on_transition_interrupt_storylet": "transition_guard_interrupted"
  }
}
```

For each of the 5 archetypes (`order_guard_post`, `order_patrol`, `order_fatigue_detail`, `order_drill`, `order_courier`):

- [ ] Author the accept storylet with arc block (1 storylet)
- [ ] Author 5 mid-arc storylets (`order_<id>_mid_1` through `_5`), each tagged `intent_affinity` so they fire preferentially under their matching intent
- [ ] Author 4 resolve storylets (`order_<id>_resolve_diligent`, `_slack`, `_scheme`, `_sociable` — depending on which intents the order exposes) with appropriate scripted effects (skill XP, occasional `grant_focus_point` for diligent, occasional `grant_random_item_from_pool: { pool_id: "issue_weapon_melee" }` for slack-with-luck, etc.)

After authoring all 5 archetypes:

- [ ] Validate
- [ ] Deploy verify
- [ ] Commit per archetype OR as a single bulk commit if authored together

```bash
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): author 5 T1-T3 named-order archetypes (~50 storylets)

Spec 2 §3. Five named-order archetypes for the early-career band:
guard_post, patrol, fatigue_detail, drill, courier. Each archetype
ships ~10 storylets (1 accept + 5 mid-arc + 4 resolve-by-intent).

All accepts splice an arc block onto OrderActivity via start_arc.
Decline uses relation_change with target_slot lord. Mid-arc
storylets are intent-biased via intent_affinity. Resolve storylets
branch on intent — diligent gets focus-point chance, scheme can
grant a coin pull from the issue_weapon_melee pool.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 34: Author 5 named-order storylets — T4+ archetypes

**Files:**
- Create: `ModuleData/Enlisted/Storylets/order_escort.json` (+ companions)
- Create: `ModuleData/Enlisted/Storylets/order_scout.json` (+ companions)
- Create: `ModuleData/Enlisted/Storylets/order_treat_wounded.json` (+ companions)
- Create: `ModuleData/Enlisted/Storylets/order_siege_works.json` (+ companions)
- Create: `ModuleData/Enlisted/Storylets/order_inspection.json` (+ companions)

Same pattern as Task 33. Tier ranges and combat-class gates:

- `order_escort` — `tier_min: 3`, `eligible_profiles: marching/escorting`, intents `[diligent, slack, scheme, sociable]`
- `order_scout` — `tier_min: 4`, `requires_combat_class: [Cavalry, HorseArcher]` (or no gate but heavy `class_affinity` on Riding); profiles `marching/wandering`; intents `[diligent, train_hard, scheme]`
- `order_treat_wounded` — `tier_min: 2`, `excludes_flag: ["lord_imprisoned"]`, profile `any` but `requires_flag: ["recent_battle"]` (or similar); intents `[diligent, sociable, slack]`
- `order_siege_works` — `tier_min: 2`, `profile_requires: ["besieging"]`; intents `[diligent, slack, train_hard]`
- `order_inspection` — `tier_min: 5` (T7+ player or NCO-track), `profile_requires: ["garrisoned"]`; intents `[diligent, sociable]`

For each:

- [ ] Author accept + mid + resolve as in Task 33
- [ ] Validate, deploy verify, commit

Commit message:
```
content(orders): author 5 T4+ named-order archetypes (~50 storylets)

Spec 2 §3. escort, scout, treat_wounded, siege_works, inspection.
T4+ tier-gated; combat-class hints via class_affinity. Some gate
on world-state flags (treat_wounded requires recent_battle flag,
siege_works requires besieging profile).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 35: Author transition storylets (~15-20)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/transition.json`

Per Spec 2 §6 + §17 R3: when `DutyProfileBehavior` commits a profile change while `OrderActivity.ActiveNamedOrder != null`, it fires a transition storylet from the `transition_*` pool keyed `(old_profile, new_profile, active_order_id)` where possible, otherwise falling back to a generic transition storylet.

- [ ] **Step 1: Author the high-leverage transition storylets**

Cover the most-likely flip-flop pairs identified in the AI investigation:

1. `transition_besieging_to_marching_mid_guard_post` — siege ends mid-guard-post arc ("retreat horn sounds")
2. `transition_besieging_to_marching_mid_siege_works` — siege abandoned mid-earthworks ("we tear down what we built")
3. `transition_marching_to_besieging_mid_escort` — pivot to siege mid-escort arc
4. `transition_raiding_to_marching_mid_fatigue_detail` — raid ends, back on the road
5. `transition_escorting_to_wandering_mid_any` — army disbands mid-anything ("the army dissolves around you")
6. `transition_garrisoned_to_marching_mid_drill` — orders to march mid-drill ("the column forms")
7. `transition_garrisoned_to_marching_mid_inspection` — same, mid-inspection ("the parade is cut short")
8. `transition_marching_to_garrisoned_mid_courier` — courier delivered, back to garrison ("the message reaches the wall")
9. `transition_to_imprisoned_mid_any` — lord captured ("the column collapses; the enemy ride down on us")
10. `transition_from_imprisoned_mid_any` — lord released; this fires AFTER the imprisoned period ends
11–15. Generic per pair (no order-specific): `transition_<old>_to_<new>_generic`
16. `transition_generic_interrupted` — global fallback

Each transition storylet:
- Single option (no choice — narrative beat closes the arc)
- Effects include `clear_active_named_order` plus partial outcome (e.g. half the named-order's expected XP via `skill_xp` calculations applied as scripted effects)
- Setup prose ≤ 3 sentences

Example:

```jsonc
{
  "id": "transition_besieging_to_marching_mid_guard_post",
  "title": "{=trans_bes_marc_guard_t}The retreat sounds.",
  "setup": "{=trans_bes_marc_guard_s}Horn calls in the night. The siege is broken — we strike camp before dawn. Your post is forgotten.",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" } } },
  "weight": 1.0,
  "options": [
    {
      "id": "main",
      "text": "{=trans_bes_marc_guard_o}Pack and march.",
      "tooltip": "{=trans_bes_marc_guard_tt}The order is closed. Half-rewards.",
      "effects": [
        { "skill_xp": { "skill": "Athletics", "amount": 15 } },
        { "skill_xp": { "skill": "Perception", "amount": 8 } },
        { "clear_active_named_order": {} }
      ],
      "result_text": "{=trans_bes_marc_guard_r}The column moves out at dawn. The siege camp burns behind you."
    }
  ]
}
```

- [ ] **Step 2: Validate, deploy verify, commit**

```bash
git add ModuleData/Enlisted/Storylets/transition.json
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): author transition storylets (~15-18)

Spec 2 §6 + §17 R3. Covers the documented AI flip-flop modes
(siege-abandoned-mid-X, raid-to-march, army-disband, etc.) with
order-specific variants for high-traffic pairs and a generic
fallback. Each closes the arc via clear_active_named_order and
applies partial outcomes.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 36: Implement `validate_content.py` Phase 13 — pool coverage

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Read the existing validator structure**

```bash
head -100 Tools/Validation/validate_content.py
```

Find where Phases 1-12 are defined (functions or callable steps). Spec 1's plan added Phase 12 in Task 20 — follow the same insertion pattern.

- [ ] **Step 2: Add Phase 13 function**

```python
def phase13_pool_coverage(storylets, errors):
    """Spec 2 §14. Each duty profile's pool must touch every Bannerlord skill ≥5x."""
    SKILLS = ["OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing",
              "Riding", "Athletics", "Smithing", "Scouting", "Tactics", "Roguery",
              "Charm", "Leadership", "Trade", "Steward", "Medicine", "Engineering"]
    PROFILES = ["garrisoned", "marching", "besieging", "raiding", "escorting", "wandering"]
    counts = {(p, s): 0 for p in PROFILES for s in SKILLS}

    for s in storylets:
        profiles = s.get("profile_requires", [])
        for p in profiles:
            if p not in PROFILES: continue
            for opt in s.get("options", []):
                for eff in opt.get("effects", []):
                    if "skill_xp" in eff:
                        skill = eff["skill_xp"].get("skill")
                        if skill in SKILLS: counts[(p, skill)] += 1
                    if "grant_focus_point" in eff:
                        skill = eff["grant_focus_point"].get("skill")
                        if skill in SKILLS: counts[(p, skill)] += 1

    for (p, s), c in counts.items():
        if c < 5:
            errors.append(f"Phase 13: profile '{p}' covers skill '{s}' only {c}× (need ≥5)")
```

Wire it in the main run loop:

```python
phase13_pool_coverage(all_storylets, errors)
```

- [ ] **Step 3: Run the validator — likely fails**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: Phase 13 reports gaps where profile pools don't cover specific skills 5x. **This is the validator's job — to surface gaps in authoring.** Authors then add or adjust storylets to close the gaps.

- [ ] **Step 4: Close any gaps revealed**

Edit profile-pool JSON from Tasks 27-32 to add `skill_xp` entries that cover the gaps. Re-run validator until clean.

- [ ] **Step 5: Commit**

```bash
git add Tools/Validation/validate_content.py
# plus any profile-pool JSON edits to close gaps
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
validator: Phase 13 — duty pool skill coverage

Spec 2 §14. Asserts each (profile, skill) cell is covered by ≥5
storylets via skill_xp or grant_focus_point effects. Surfaces the
"I never get Bow on garrison runs" gap at build time.

Existing pool authoring extended where Phase 13 reported gaps.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 37: Implement `validate_content.py` Phase 14 — arc integrity

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Add Phase 14 function**

```python
def phase14_arc_integrity(storylets, errors):
    """Spec 2 §14. Arc-tagged storylets must have valid duration, phases, pool refs."""
    storylet_ids = {s.get("id") for s in storylets}
    storylet_prefixes = set()
    for s in storylets:
        sid = s.get("id", "")
        for i in range(len(sid)):
            storylet_prefixes.add(sid[:i+1])

    for s in storylets:
        arc = s.get("arc")
        if not arc: continue
        sid = s.get("id", "<unknown>")
        if arc.get("duration_hours", 0) <= 0:
            errors.append(f"Phase 14: storylet '{sid}' arc.duration_hours must be > 0")
        phases = arc.get("phases", [])
        if not phases:
            errors.append(f"Phase 14: storylet '{sid}' arc.phases empty")
        for ph in phases:
            prefix = ph.get("pool_prefix", "")
            if not prefix:
                errors.append(f"Phase 14: storylet '{sid}' phase '{ph.get('id')}' missing pool_prefix")
                continue
            # at least one storylet must start with this prefix
            if not any(other.get("id","").startswith(prefix) for other in storylets):
                errors.append(f"Phase 14: storylet '{sid}' phase prefix '{prefix}' has no matching storylets")
        interrupt_id = arc.get("on_transition_interrupt_storylet", "")
        if interrupt_id and interrupt_id not in storylet_ids:
            errors.append(f"Phase 14: storylet '{sid}' references unknown interrupt storylet '{interrupt_id}'")

        # start_arc effect should only appear on options of arc-tagged storylets
        for s in storylets:
            for opt in s.get("options", []):
                for eff in opt.get("effects", []):
                    if "start_arc" in eff and not s.get("arc"):
                        errors.append(f"Phase 14: storylet '{s.get('id')}' uses start_arc but has no arc block")
```

- [ ] **Step 2: Run validator, fix any reported gaps**

- [ ] **Step 3: Commit**

```bash
git add Tools/Validation/validate_content.py
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
validator: Phase 14 — named-order arc integrity

Spec 2 §14. arc.duration_hours > 0, phases non-empty, pool_prefix
declared and matches authored storylets, on_transition_interrupt_
storylet references resolve, start_arc effect only on arc-tagged
storylets.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 38: Implement `validate_content.py` Phase 15 — path crossroads completeness (stub now, full in Phase C)

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Add Phase 15 stub**

```python
def phase15_path_crossroads(storylets, errors):
    """Spec 2 §14. Phase C content; assert completeness when crossroads.json exists."""
    PATHS = ["ranger", "enforcer", "support", "diplomat", "rogue"]
    BANDS = ["t3_to_t4", "t5_to_t6", "t7_to_t8"]
    storylet_ids = {s.get("id") for s in storylets}
    expected = {f"crossroads_{p}_{b}" for p in PATHS for b in BANDS}
    have_any_crossroads = any(sid.startswith("crossroads_") for sid in storylet_ids)
    if not have_any_crossroads:
        # Phase B: skip — Phase C will author content.
        return
    missing = expected - storylet_ids
    for m in missing:
        errors.append(f"Phase 15: missing crossroads storylet '{m}'")
```

- [ ] **Step 2: Run + commit**

```bash
git add Tools/Validation/validate_content.py
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
validator: Phase 15 stub — path crossroads completeness

Spec 2 §14. Asserts all 15 (path × band) crossroads storylets exist
once any crossroads_* storylet is authored. Skips silently in
Phase B (no crossroads yet); enforces fully in Phase C Task 49.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 39: Implement `validate_content.py` Phase 16 — loot pool sanity

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Add Phase 16 function**

```python
def phase16_loot_pool_sanity(loot_pools, errors):
    """Spec 2 §14. Pool source + filter required; tier_range required for global_catalog."""
    for pool in loot_pools:
        pid = pool.get("id", "<unknown>")
        src = pool.get("source")
        if src not in ("faction_troop_tree", "global_catalog"):
            errors.append(f"Phase 16: pool '{pid}' invalid source '{src}'")
            continue
        flt = pool.get("filter", {})
        if not flt.get("categories"):
            errors.append(f"Phase 16: pool '{pid}' missing filter.categories")
        if src == "global_catalog" and not flt.get("tier_range"):
            errors.append(f"Phase 16: pool '{pid}' (global_catalog) missing filter.tier_range")
        if src == "faction_troop_tree" and not flt.get("max_troop_tier"):
            errors.append(f"Phase 16: pool '{pid}' (faction_troop_tree) missing filter.max_troop_tier")
```

- [ ] **Step 2: Wire the loot-pool loader into `validate_content.py`**

Add a function to read `ModuleData/Enlisted/Loot/loot_pools.json` and pass `pools` into `phase16_loot_pool_sanity`.

- [ ] **Step 3: Run validator + commit**

```bash
git add Tools/Validation/validate_content.py
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
validator: Phase 16 — loot pool sanity

Spec 2 §14. Pool source must be faction_troop_tree or global_catalog;
filter.categories required; tier_range required for global_catalog;
max_troop_tier required for faction_troop_tree.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 40: Implement `validate_content.py` Phase 17 — read-only quality + rate caps

**Files:**
- Modify: `Tools/Validation/validate_content.py`

- [ ] **Step 1: Extend Spec 0's Phase 12 function with Spec 2 read-only qualities**

Phase 12 today blocks writes to `rank`, `days_in_rank`, `days_enlisted`. Add `committed_path` to that list. Also add Spec 2's per-storylet rate caps from §7 (≤1 attribute_level per storylet, ≤1 focus_point per storylet, ≤3 skill_level per storylet, ≤3 renown per storylet, |delta| ≤5 on relation_change, ≤1 grant_item / grant_random_item_from_pool per storylet).

```python
def phase17_rate_caps(storylets, errors):
    """Spec 2 §7. Per-storylet caps on permanent reward currencies."""
    READ_ONLY_QUALITIES = {"rank", "days_in_rank", "days_enlisted", "committed_path"}
    for s in storylets:
        sid = s.get("id", "<unknown>")
        for opt in s.get("options", []):
            counts = {"grant_attribute_level": 0, "grant_focus_point": 0, "grant_skill_level": 0,
                      "grant_renown": 0, "grant_item": 0, "grant_random_item_from_pool": 0}
            for eff in opt.get("effects", []):
                for k in counts:
                    if k in eff: counts[k] += 1
                if "relation_change" in eff:
                    delta = abs(eff["relation_change"].get("delta", 0))
                    if delta > 5:
                        errors.append(f"Phase 17: storylet '{sid}' option '{opt.get('id')}' relation_change |delta|={delta} > 5")
                if "quality_add" in eff:
                    q = eff["quality_add"].get("quality")
                    if q in READ_ONLY_QUALITIES:
                        errors.append(f"Phase 17: storylet '{sid}' option '{opt.get('id')}' writes read-only quality '{q}'")
            if counts["grant_attribute_level"] > 1:
                errors.append(f"Phase 17: storylet '{sid}' option '{opt.get('id')}' has {counts['grant_attribute_level']} grant_attribute_level (cap 1)")
            if counts["grant_focus_point"] > 1:
                errors.append(f"Phase 17: storylet '{sid}' option '{opt.get('id')}' has {counts['grant_focus_point']} grant_focus_point (cap 1)")
            if counts["grant_skill_level"] > 3:
                errors.append(f"Phase 17: storylet '{sid}' option '{opt.get('id')}' has {counts['grant_skill_level']} grant_skill_level (cap 3)")
            if counts["grant_renown"] > 3:
                errors.append(f"Phase 17: storylet '{sid}' option '{opt.get('id')}' has {counts['grant_renown']} grant_renown (cap 3)")
            if counts["grant_item"] > 1 or counts["grant_random_item_from_pool"] > 1:
                errors.append(f"Phase 17: storylet '{sid}' option '{opt.get('id')}' has multiple item grants (cap 1 each)")
```

- [ ] **Step 2: Wire in + run + commit**

```bash
git add Tools/Validation/validate_content.py
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
validator: Phase 17 — read-only quality + per-storylet rate caps

Spec 2 §7 + §14. Adds committed_path to the read-only quality list.
Enforces per-storylet caps on permanent-currency grants
(attribute/focus/skill_level/renown/item) and |delta| ≤5 on
relation_change. Keeps named-order completions and rank promotions
load-bearing rather than letting authors flood ambient content
with permanent rewards.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 41: Sync localization strings

**Files:**
- Modify: `ModuleData/Languages/strings_orders.xml` (regenerated)

- [ ] **Step 1: Run sync tool**

```bash
/c/Python313/python.exe Tools/Validation/sync_event_strings.py
```

This scans all storylets authored in Tasks 25-35 + 33-34 and writes (or updates) `ModuleData/Languages/strings_orders.xml` with `<string id="..." text="..."/>` entries for every `{=key}fallback` pair.

- [ ] **Step 2: Verify deploy**

```bash
ls "/c/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Enlisted/ModuleData/Languages/strings_orders.xml"
```

- [ ] **Step 3: Commit**

```bash
git add ModuleData/Languages/strings_orders.xml
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): sync localization strings

Generated by Tools/Validation/sync_event_strings.py from all
Spec 2 storylets authored in Tasks 25-35.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 42: Delete old Orders subsystem

**Files:**
- Delete: 12 .cs files under `src/Features/Orders/`
- Delete: 19 JSON files under `ModuleData/Enlisted/Orders/`
- Modify: `Enlisted.csproj` (remove old `<Compile Include>` entries)
- Possibly delete: `src/Features/Orders/` directory if empty

- [ ] **Step 1: Final verification that nothing reads OrderManager**

```bash
grep -rn "OrderManager\|OrderProgressionBehavior\|OrderCatalog\b\|PromptCatalog\b\|Models/Order\b" src/ | grep -v "src/Features/Orders/"
```

Expected: zero hits. If any remain, STOP and migrate them before proceeding.

- [ ] **Step 2: Remove csproj entries for old Orders files**

Open `Enlisted.csproj` and delete all `<Compile Include="src\Features\Orders\..." />` entries. There are 12 such lines.

- [ ] **Step 3: Delete the .cs files**

```bash
rm src/Features/Orders/Behaviors/OrderManager.cs
rm src/Features/Orders/Behaviors/OrderProgressionBehavior.cs
rm src/Features/Orders/OrderCatalog.cs
rm src/Features/Orders/PromptCatalog.cs
rm src/Features/Orders/Models/Order.cs
rm src/Features/Orders/Models/OrderPrompt.cs
rm src/Features/Orders/Models/OrderConsequence.cs
rm src/Features/Orders/Models/OrderOutcome.cs
rm src/Features/Orders/Models/OrderRequirement.cs
rm src/Features/Orders/Models/PhaseRecap.cs
rm src/Features/Orders/Models/PromptOutcome.cs
rm src/Features/Orders/Models/PromptOutcomeData.cs
```

- [ ] **Step 4: Delete the JSON files**

```bash
rm ModuleData/Enlisted/Orders/orders_t1_t3.json
rm ModuleData/Enlisted/Orders/orders_t4_t6.json
rm ModuleData/Enlisted/Orders/orders_t7_t9.json
rm ModuleData/Enlisted/Orders/order_events/*.json
```

- [ ] **Step 5: Remove empty directories (if any)**

```bash
rmdir src/Features/Orders/Behaviors src/Features/Orders/Models src/Features/Orders 2>/dev/null
rmdir ModuleData/Enlisted/Orders/order_events ModuleData/Enlisted/Orders 2>/dev/null
```

- [ ] **Step 6: Remove csproj entries that referenced the now-deleted JSON deploy paths**

If `Enlisted.csproj` had any `<OrdersData>` ItemGroup or `MakeDir`/`Copy` triplets for `Orders/`, remove them.

- [ ] **Step 7: Build — must pass**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: clean build. If anything still references a deleted type, the build will tell you exactly what — fix and retry.

- [ ] **Step 8: Commit**

```bash
git add -u Enlisted.csproj src/Features/Orders ModuleData/Enlisted/Orders
git commit -F /c/Users/coola/commit_msg.txt
```

(`git add -u` for tracked files including deletions; the `src/Features/Orders` and `ModuleData/Enlisted/Orders` paths capture the deletions.)

Commit message:
```
chore(orders): delete OrderManager subsystem after migration

Spec 2 §13.2. Removes 12 .cs files (~1.5K lines) under
src/Features/Orders/ and 19 .json files under ModuleData/Enlisted/
Orders/. All consumer reads migrated to OrderActivity in Tasks
18-23; Task 24 verified zero remaining OrderManager.Instance hits.

The replacement OrderActivity + duty profiles + named orders +
storylet-backbone integration shipped across Tasks 8-35.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 43: Phase B smoke — daily drift + named-order arc + transitions

**Files:**
- No code changes; verification only.

- [ ] **Step 1: Build + validate clean**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: 0 errors, all Spec 2 validators (Phase 13–17) pass.

- [ ] **Step 2: Wire ambient profile-storylet emission through `OrdersNewsFeedThrottle`**

In `src/Features/Activities/Orders/DutyProfileBehavior.cs` (created Task 12), the hourly tick may opportunistically emit one ambient profile storylet from the current profile pool to the news feed. **Per Spec 2 §17 R10**, this emission must check `OrdersNewsFeedThrottle.TryClaim()` first. Find the ambient-emit site (likely a small helper at the bottom of `OnHourlyTick` or in `NamedOrderArcRuntime` for the base `on_duty` phase ticking — wherever a profile-pool storylet is selected to fire as NewsFeed) and wrap:

```csharp
if (!OrdersNewsFeedThrottle.TryClaim())
{
    ModLogger.Expected("DRIFT", "ambient_throttled", $"ambient {pickedStoryletId} dropped by wall-clock throttle");
    return;
}
StoryDirector.Instance?.EmitCandidate(new StoryCandidate { /* ... */ });
```

(Modal emissions — `profile_changed` beats, named-order accepts, transitions, crossroads, lord-state — bypass entirely; StoryDirector paces them.)

- [ ] **Step 3: Build + commit the throttle wiring**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
git add src/Features/Activities/Orders/DutyProfileBehavior.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): gate ambient news-feed emissions on OrdersNewsFeedThrottle

Spec 2 §17 R10. Drops ambient profile-storylet entries that
arrive faster than the 20s wall-clock floor or while speed > 4x.
Modal beats (profile_changed, named-order accept, transitions)
bypass — StoryDirector already paces them.

Daily drift summaries (Task 15's flush path) already throttled
since Task 15 itself.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

- [ ] **Step 4: Launch + smoke — full Phase B verification + fast-forward soak**

Close BannerlordLauncher, launch the game, start a fresh campaign. Enlist with a lord. Run the campaign for 14+ in-game days at fast speed. Watch the session log + news feed.

Verify:
- **Daily drift logs** something every game day (`DRIFT no_floor_for_profile` is expected — confirms the daily tick is firing)
- **Profile transitions** commit correctly (walk lord into a town → 2 hours later `garrisoned` commits; lord marches out → 2 hours later `marching` commits)
- **Ambient profile storylets** fire at expected cadence — open the news feed and verify entries appear from `duty_garrisoned_*` / `duty_marching_*` / etc. as the lord moves
- **Named-order accept** works — when a `garrisoned` ambient fires (or via debug hotkey), the sergeant offers e.g. `order_guard_post`. Accept → `start_arc` splices arc phases → mid-arc storylets fire over the next 8-12 hours → resolve storylet fires → news-feed entry confirms outcome
- **Transition storylet** fires on a profile change mid-arc (e.g. accept Guard Post in `garrisoned`, then have the lord march out — `transition_garrisoned_to_marching_mid_guard_post` should fire as a Modal)
- **All 18 skills receive XP** over the 14 days — open the character sheet and confirm skill XP gains across at least 12 of the 18 skills
- **Save/load mid-arc** — accept a named order, advance to mid-phase, save, exit to main menu, reload. Verify the named order is still active (`OrderDisplayHelper.GetCurrent() != null`) and the mid-arc phase resumes ticking
- **Fast-forward soak (Spec 2 §17 R10).** Set time to 4× and run for 30 real seconds. Verify the news feed receives **≤ 5 entries** total (not 20+); verify daily-drift summaries fire on the throttled cadence ("Past few days: ..."), not as 5 individual per-day entries; verify Modal interrupts (named-order accept, profile_changed if it fires) still fire correctly. Then crank to 16× (if any speed-mod is installed) and run 30 real seconds — verify zero ambient + zero drift entries (suppression active) but Modal interrupts still surface.

If any of these fail, fix in a follow-up task before declaring Phase B complete.

- [ ] **Step 5: Mark Phase B complete in plan**

Update the plan's `**Status:**` line.

---

## Phase C — Path system + crossroads

## Task 44: Wire `PathScorer` to intent picks ✅ shipped as part of Task 16

The originally-planned Task 44 work — adding `PathScorer.OnIntentPicked(intent)` inside `NamedOrderArcRuntime.SpliceArc` — was folded into Task 16 implementation (commit `b798265`). `NamedOrderArcRuntime.SpliceArc` already calls `PathScorer.OnIntentPicked(intent)` immediately after the arc splice info-log. No additional work for Phase C.

---

## Task 45: Verify `HeroGainedSkill` subscription is firing path bumps

**Files:**
- No code changes; verification only.

- [ ] **Step 1: Enable temporary path-score logging**

Add a `ModLogger.Info` line in `PathScorer.BumpPath` to log every increment with the path id, amount, and new total. (Remove in Task 57 once debug hotkey provides a cleaner inspection surface.)

- [ ] **Step 2: Smoke — 7 in-game days**

Enlist, run for 7 days at fast speed (no manual intent picks needed — let ambient skill XP from profile storylets and daily drift accumulate). Verify session log shows `PATH BumpPath` entries whenever a skill crosses a level boundary on the main hero.

- [ ] **Step 3: Verify path-score distribution**

After 7 days, expect `path_*_score` qualities to show non-zero values across multiple paths (proportional to which skills the player gained levels in). E.g. a `garrisoned`-heavy player might show high `path_enforcer_score` from OneHanded/Athletics level-ups, moderate `path_diplomat_score` from Charm level-ups, low `path_rogue_score` from sparse Roguery level-ups.

If no path scores increment: check that `CampaignEvents.HeroGainedSkill` is firing at all (ModLogger.Info inside `OnHeroGainedSkill` before the MainHero filter), then that the MainHero filter isn't rejecting legitimate events, then that `skill.StringId` matches an entry in the `SkillToPath` dictionary. The `HeroGainedSkill` event fires on skill LEVEL change (not per-XP-event), so events are sparse early in a career and dense during active training.

- [ ] **Step 4: No commit (verification-only). Remove the temporary `ModLogger.Info` from `BumpPath` in Task 57.**

---

## Task 46: Wire `PathScorer.OnTierChanged` to crossroads firing

**Files:**
- Modify: `src/Features/Activities/Orders/PathScorer.cs`

- [ ] **Step 1: Replace the stub `OnTierChanged` with full crossroads dispatch**

```csharp
private void OnTierChanged(int oldTier, int newTier)
{
    try
    {
        // Crossroads bands: T3→T4, T5→T6, T7→T8
        string band = null;
        if (oldTier == 3 && newTier == 4) band = "t3_to_t4";
        else if (oldTier == 5 && newTier == 6) band = "t5_to_t6";
        else if (oldTier == 7 && newTier == 8) band = "t7_to_t8";
        if (band == null) { return; }

        // Find leading path
        string leadingPath = null;
        int leadingScore = 0, secondScore = 0;
        foreach (var p in new[] { "ranger", "enforcer", "support", "diplomat", "rogue" })
        {
            var key = $"path_{p}_score";
            var score = QualityStore.Instance?.Get(key) ?? 0;
            if (score > leadingScore) { secondScore = leadingScore; leadingScore = score; leadingPath = p; }
            else if (score > secondScore) { secondScore = score; }
        }

        if (leadingPath == null || leadingScore < 30)
        {
            ModLogger.Expected("PATH", "no_clear_leader",
                $"Crossroads at {band}: leadingScore={leadingScore} < 30 — staying undefined");
            return;
        }
        if (leadingScore - secondScore < 10)
        {
            ModLogger.Expected("PATH", "leader_too_close",
                $"Crossroads at {band}: lead={leadingScore} second={secondScore}, diff < 10 — drifting");
            return;
        }

        // Fire crossroads_<path>_<band>
        var crossroadsId = $"crossroads_{leadingPath}_{band}";
        var storylet = StoryletCatalog.Instance?.Get(crossroadsId);
        if (storylet == null)
        {
            ModLogger.Expected("PATH", "crossroads_storylet_missing",
                $"Crossroads storylet '{crossroadsId}' not authored");
            return;
        }

        StoryDirector.Instance?.EmitCandidate(new StoryCandidate
        {
            SourceId = $"orders.crossroads.{leadingPath}",
            CategoryId = "orders.crossroads",
            ProposedTier = StoryTier.Modal,
            ChainContinuation = true,
            Beats = { },
            Relevance = new RelevanceKey { TouchesEnlistedLord = true },
            EmittedAt = CampaignTime.Now,
            StoryKey = crossroadsId
        });

        ModLogger.Info("PATH", $"Fired crossroads {crossroadsId} (leading={leadingScore} second={secondScore})");
    }
    catch (Exception ex) { ModLogger.Caught("PATH", "OnTierChanged threw", ex); }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
git add src/Features/Activities/Orders/PathScorer.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): wire path crossroads firing on tier-up

Spec 2 §9. On T3→T4 / T5→T6 / T7→T8 transitions: query path
scores, find leader, fire crossroads_<path>_<band> if leader
≥30 with ≥10 lead. Otherwise log Expected and let player drift.

Crossroads storylets authored in Task 47.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 47: Author crossroads storylets (~25-30)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/crossroads.json`

- [ ] **Step 1: Author 15 base crossroads storylets — 5 paths × 3 bands**

Each base crossroads storylet has the shape:

```jsonc
{
  "id": "crossroads_ranger_t3_to_t4",
  "title": "{=cross_ranger_t3_t}A scout's eye.",
  "setup": "{=cross_ranger_t3_s}The lord's veteran scout looks you over at evening mess. 'I've watched you lately. You move like one of us. Are you?'",
  "scope": { "context": "any", "slots": { "lord": { "role": "enlisted_lord" }, "veteran": { "role": "named_veteran" } } },
  "weight": 1.0,
  "options": [
    {
      "id": "commit",
      "text": "{=cross_ranger_t3_o1}Yes. Ranger is what I'm becoming.",
      "tooltip": "{=cross_ranger_t3_tt1}Commit to Ranger. +1 focus Bow, +1 focus Scouting, +Trait Calculating.",
      "effects": [
        { "quality_set": { "quality": "committed_path", "value": "ranger" } },
        { "grant_focus_point": { "skill": "Bow", "amount": 1 } },
        { "grant_focus_point": { "skill": "Scouting", "amount": 1 } },
        { "set_trait_level": { "trait": "Calculating", "level": 1 } },
        { "grant_renown": { "amount": 2 } }
      ],
      "result_text": "{=cross_ranger_t3_r1}You nod. The veteran claps your shoulder. 'Then drill with us tomorrow.'"
    },
    {
      "id": "resist",
      "text": "{=cross_ranger_t3_o2}No. I'm meant for something else.",
      "tooltip": "{=cross_ranger_t3_tt2}Resist Ranger path. Future Ranger gains halved.",
      "effects": [ { "set_flag": { "flag": "path_resisted_ranger" } } ],
      "result_text": "{=cross_ranger_t3_r2}The veteran shrugs and turns away."
    },
    {
      "id": "drift",
      "text": "{=cross_ranger_t3_o3}I haven't decided.",
      "tooltip": "{=cross_ranger_t3_tt3}Crossroads can fire again at the next band.",
      "effects": [ ],
      "result_text": "{=cross_ranger_t3_r3}The veteran nods once and lets it go."
    }
  ]
}
```

Author 15 such storylets — `crossroads_<path>_<band>` for each of (ranger, enforcer, support, diplomat, rogue) × (t3_to_t4, t5_to_t6, t7_to_t8). Tier-band variants escalate rewards (T3 = focus + trait; T5 = unspent focus + attribute level; T7 = unspent attribute + skill level + renown).

- [ ] **Step 2: Author ~10 culture-flavored variants on hot crossroads**

Override `crossroads_ranger_t3_to_t4` for Battanian culture (different setup prose acknowledging the player's Battanian forest-scout heritage), Khuzait (steppe-scout flavor), etc. Use `requires_culture` field to gate.

- [ ] **Step 3: Validate + deploy + commit**

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
git add ModuleData/Enlisted/Storylets/crossroads.json
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): author 25-30 crossroads storylets

Spec 2 §9. 15 base (5 paths × 3 bands) + ~10 culture variants.
Commit branches set committed_path quality + grant path-defining
permanent currency (focus / attribute / trait). Resist sets
path_resisted_<path> flag (PathScorer halves future gains for
that path). Drift no-ops; crossroads can re-fire at next band.

Phase 15 validator now enforces all 15 base entries exist.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 48: Wire `committed_path` quality + `path_resisted_<path>` flags into PathScorer

**Files:**
- Modify: `src/Features/Activities/Orders/PathScorer.cs`

- [ ] **Step 1: Halve increments for resisted paths**

Modify `BumpPath`:

```csharp
private static void BumpPath(string path, int amount)
{
    try
    {
        if (FlagStore.Instance?.HasFlag($"path_resisted_{path}") == true)
        {
            amount = Math.Max(1, amount / 2);
        }
        var qualityKey = $"path_{path}_score";
        var current = QualityStore.Instance?.Get(qualityKey) ?? 0;
        QualityStore.Instance?.Set(qualityKey, Math.Min(100, current + amount));
    }
    catch (Exception ex) { ModLogger.Caught("PATH", "BumpPath threw", ex); }
}
```

- [ ] **Step 2: Add `quality_set` primitive to EffectExecutor (if not present)**

Crossroads commit uses `quality_set` (write-once value, distinct from `quality_add`). If Spec 0's EffectExecutor doesn't have `quality_set`, add it now:

```csharp
case "quality_set":
{
    try
    {
        var qid = (string)effectObj["quality"];
        var val = effectObj["value"]?.ToString();
        if (string.IsNullOrEmpty(qid) || string.IsNullOrEmpty(val)) { break; }
        QualityStore.Instance?.SetString(qid, val);
    }
    catch (Exception ex) { ModLogger.Caught("EFFECT", "quality_set threw", ex); }
    break;
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
git add src/Features/Activities/Orders/PathScorer.cs src/Features/Content/EffectExecutor.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
feat(orders): wire path_resisted_* flag effect on BumpPath

Spec 2 §9. Resisted paths halve subsequent score increments.
Adds quality_set effect primitive (string-valued) for crossroads
commit to set committed_path.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 49: Author T7+ named-order variants gated on `committed_path`

**Files:**
- Modify: 5 named-order JSON files (`order_scout`, `order_inspection`, `order_treat_wounded`, etc.) OR create new variant storylets

Spec 2 §9: T7+ named orders should branch on committed_path so a Ranger-committed player gets a "lead a long-range scout patrol" version while a Diplomat-committed player gets a "represent the lord at a vassal court" version. **For Phase C minimum-viable: add 5 path-gated T7+ variants.**

Each variant is a separate storylet with `tier_min: 7` + `requires_quality: { quality: committed_path, equals: <path> }` (a new schema field — extend the parser if needed). Examples:

- `order_scout_ranger_t7` — Ranger-only deep-scout mission
- `order_inspection_diplomat_t7` — Diplomat-only court representation
- `order_treat_wounded_support_t7` — Support-only field hospital command
- `order_drill_enforcer_t7` — Enforcer-only training-the-recruits leadership
- `order_courier_rogue_t7` — Rogue-only smuggling-ish backchannel mission

For each:

- [ ] Author the variant storylet with appropriate intent options + arc + path-themed mid/resolve content
- [ ] Validate, deploy, commit

Commit message:
```
content(orders): author 5 T7+ path-gated named-order variants

Spec 2 §9. Each committed path unlocks a flagship T7+ named order:
ranger → deep_scout, diplomat → court_representation, support →
field_hospital, enforcer → recruit_command, rogue → backchannel.
Gated via requires_quality on committed_path.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 50: Verify `prior_service_*` flag emission

**Files:**
- No code changes (`OnEnlistmentEnded` listener already emits these per Task 13).

- [ ] **Step 1: Smoke**

Enlist with a Vlandian lord, level to T5, commit `committed_path = ranger` via crossroads, then discharge. Open save inspector or session log and verify:

- `prior_service_culture_vlandia` flag set
- `prior_service_rank_5` flag set
- `prior_service_path_ranger` flag set

- [ ] **Step 2: Re-enlist with a Khuzait lord, verify flags persist on player hero**

The flags should still be set after the second enlistment starts. Phase D Task 55's culture-variant overlays will use these for veteran-flavored content.

- [ ] **Step 3: No commit (verification-only).**

---

## Task 51: Phase 15 validator full enforcement

**Files:**
- Modify: `Tools/Validation/validate_content.py` (already has Phase 15 stub from Task 38)

- [ ] **Step 1: Re-run validator**

Now that crossroads.json (Task 47) authored 15 base storylets, Phase 15 should fully enforce.

```bash
/c/Python313/python.exe Tools/Validation/validate_content.py
```

Expected: Phase 15 reports any (path × band) cell missing. Should be zero.

- [ ] **Step 2: Fix any gaps + commit**

```bash
# Edit crossroads.json if any storylets missing
git add ModuleData/Enlisted/Storylets/crossroads.json
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message (only if gaps found):
```
content(orders): close crossroads gaps reported by Phase 15

Spec 2 §14.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

If no gaps, no commit.

---

## Task 52: Phase C smoke

**Files:**
- No code changes; verification only.

- [ ] **Step 1: Build + validate clean**

```bash
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
/c/Python313/python.exe Tools/Validation/validate_content.py
```

- [ ] **Step 2: Smoke — crossroads firing**

Enlist, level to T4 quickly (use debug hotkey if available, or grind), verify the appropriate crossroads storylet fires. Pick "commit" → verify `committed_path` quality is set, verify path-defining permanent rewards apply (focus point, trait shift, renown).

Repeat at T6 and T8 with a different intent profile to verify a different path's crossroads fires.

- [ ] **Step 3: Smoke — resist + drift**

Re-enlist (or load earlier save). Pick "resist" at the T4 crossroads. Verify `path_resisted_ranger` flag set. Run 5 more days, verify ranger-related XP no longer bumps `path_ranger_score` at full rate (halved).

- [ ] **Step 4: Smoke — undefined path stays drifting**

Re-enlist with all-rounder intent picks (mix all 5 intents equally). Reach T4. Verify NO crossroads fires (all path scores < 30 OR leading score within 10 of second). Session log shows `Expected("PATH", "no_clear_leader", ...)` or `"leader_too_close"`.

- [ ] **Step 5: Mark Phase C complete in plan**

---

## Phase D — Polish, save-load & playtest

## Task 53: Author floor storylets (~50)

**Files:**
- Create: `ModuleData/Enlisted/Storylets/floor.json`

Per Spec 2 §17 R2 + Revision 6: floor storylets wrap the daily skill-XP drift in 1-line news-feed texture. ~10 per profile × 5 main profiles = 50 floor storylets. Imprisoned uses 5 shared floor storylets (less needed since imprisoned is rare).

Floor storylet template:

```jsonc
{
  "id": "floor_garrisoned_drill_1",
  "title": "{=floor_garr_drill_1_t}",
  "setup": "{=floor_garr_drill_1_s}You drill at dawn. Polearm work, then the obstacle course.",
  "scope": { "context": "any", "slots": { } },
  "weight": 1.0,
  "options": [
    {
      "id": "main",
      "text": "{=floor_garr_drill_1_o}",
      "effects": [
        { "skill_xp": { "skill": "Polearm", "amount": 12 } },
        { "skill_xp": { "skill": "Athletics", "amount": 8 } }
      ],
      "result_text": "{=floor_garr_drill_1_r}Hands raw, breath ragged. The kit feels lighter."
    }
  ]
}
```

Each floor storylet:
- 1-2 sentences setup
- Single option (no choice — drift is automatic)
- 2-3 skill_xp effects matching the profile's heavy/moderate skills
- 1 sentence result text

For each profile, author 10 floor storylets covering the spread of that profile's skills. Mix narrative angles so they don't repeat (drill, mess, marketplace, perimeter walk, etc.).

- [ ] Author 50 floor storylets total
- [ ] Validate, deploy, commit

Commit message:
```
content(orders): author 50 floor storylets across 5 profiles

Spec 2 §17 R2 + Revision 6. Daily skill-XP drift now delivers
through floor_<profile>_<n> storylets at NewsFeed tier — same XP,
but every day shows a one-line texture in the news feed instead
of silent stat changes. ~10 per profile.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 54: Author imprisoned-profile + lord-state edge-case storylets

**Files:**
- Create: `ModuleData/Enlisted/Storylets/duty_imprisoned.json` (~10)
- Create: `ModuleData/Enlisted/Storylets/lord_state.json` (~5: lord_killed_farewell, lord_kingdom_changed, plus floor_imprisoned_*)

- [ ] **Step 1: Author imprisoned-profile pool**

10 ambient storylets reflecting captivity (your lord is in prison; you're in limbo). Heavy on Roguery / Charm (cell-mate dialogues, trying to escape, bribing guards).

- [ ] **Step 2: Author lord-state edge-case storylets**

- `lord_killed_farewell` — Modal storylet that fires when LordStateListener detects lord-killed. Setup: "The captain is dead. Banner falls. Service ends." Single option closes the storylet → StopEnlist auto-fires after.
- `lord_kingdom_changed` — Modal: "The lord has sworn to a new king. The colors change." Optional reaction.
- 5 floor_imprisoned storylets for daily drift while imprisoned.

- [ ] **Step 3: Validate + commit**

```bash
git add ModuleData/Enlisted/Storylets/duty_imprisoned.json ModuleData/Enlisted/Storylets/lord_state.json
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): author imprisoned + lord-state edge case storylets

Spec 2 §11. 10 imprisoned-profile ambients (Roguery/Charm heavy
— cell-mate intrigue, escape attempts), lord_killed_farewell
+ lord_kingdom_changed Modals, 5 floor_imprisoned for daily drift.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 55: Author culture-variant overlays on ~15 hot-path storylets

**Files:**
- Modify: ~15 storylet JSON files across `Storylets/` to add `culture_variants` blocks

Per Spec 2 §4.5 / Revision 4: storylets can declare per-culture overrides on `setup` / `result_text` so the same beat reads differently under each lord.

Schema extension (if not already in Storylet — add to Task 4's schema fields):

```jsonc
{
  "id": "duty_garrisoned_mess_hall_2",
  "setup": "{=...}<default setup>",
  "culture_variants": {
    "vlandia":  { "setup": "{=...vlandia}<Vlandian-flavored>", "result_text": "{=...}<...>" },
    "khuzait":  { "setup": "{=...khuzait}<Khuzait-flavored>", "result_text": "{=...}<...>" },
    "battania": { "setup": "{=...battania}<Battanian-flavored>", "result_text": "{=...}<...>" }
  }
}
```

- [ ] **Step 1: Identify the 15 highest-firing storylets**

After Phase B-C smoke, look at session-log frequency to find the 15 most-fired storylets. Add culture variants to those. (If smoke data isn't available yet, pick the most narratively significant per profile: 3-5 from `garrisoned`, 3-5 from `marching`, 3-5 from `besieging`, 1-2 from each rarer profile.)

- [ ] **Step 2: Add `culture_variants` blocks for all 6 cultures (vlandia / sturgia / battania / khuzait / empire / aserai)**

Per storylet, write 6 setup/result_text overrides. Sync localization after.

- [ ] **Step 3: Sync + validate + commit**

```bash
/c/Python313/python.exe Tools/Validation/sync_event_strings.py
/c/Python313/python.exe Tools/Validation/validate_content.py
git add ModuleData/Enlisted/Storylets/*.json ModuleData/Languages/strings_orders.xml
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): add culture variants to 15 hot-path storylets

Spec 2 §4.5. Each variant overrides setup + result_text per the
lord's culture (Vlandia/Sturgia/Battania/Khuzait/Empire/Aserai).
Storylet fires identically; only the prose changes by culture.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 56: Author lord-trait gates on ~15 storylets

**Files:**
- Modify: ~15 storylet JSON files to add `requires_lord_trait` / `excludes_lord_trait`

Per Spec 2 §4.5 / Revision 4: storylets gate on `DefaultTraits.*` levels so a cruel lord's company sees harsher events; merciful lords see softer.

Schema (already in Task 4's extensions):

```jsonc
{
  "id": "duty_raiding_burn_granary",
  "requires_lord_trait": ["Cruel"],     // lord trait level >= 1
  ...
}

{
  "id": "duty_raiding_spare_village",
  "requires_lord_trait": ["Merciful"],
  ...
}
```

- [ ] **Step 1: Identify ~15 storylets where lord personality matters**

Most are in `raiding`, `garrisoned` (mess-hall politics), and named-order resolves (e.g. lord's reaction to a scheme).

- [ ] **Step 2: Add gates + author "this storylet only fires under <X> lords" content**

Some storylets get one trait gate; some pairs get opposing-gate variants (one for cruel, one for merciful).

- [ ] **Step 3: Validate + commit**

```bash
git add ModuleData/Enlisted/Storylets/*.json
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
content(orders): add lord-trait gates to 15 storylets

Spec 2 §4.5. requires_lord_trait / excludes_lord_trait on raid,
mess-hall, and named-order-resolve storylets. Cruel lords' raids
include burn-the-granary options; merciful lords get spare-the-
village. Mess-hall politics differs by lord's Calculating /
Generous / Honest scores.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 57: Add Spec 2 debug hotkeys

**Files:**
- Modify: `src/Debugging/Behaviors/DebugToolsBehavior.cs`

Per Spec 1's pattern (Task 19): bind a few keyboard hotkeys for dev-only smoke testing. Spec 2 needs:

- **Ctrl+Shift+P** — Print path scores + committed_path + combat_class to a notification banner + session log.
- **Ctrl+Shift+O** — Force-fire the most-recently-eligible named-order accept storylet (skip the spontaneity threshold).
- **Ctrl+Shift+I** — Force the lord into `imprisoned` profile (test imprisoned pool); press again to release.
- **Ctrl+Shift+C** — Force-fire the `crossroads_<leading_path>_t3_to_t4` storylet (or whatever band matches current tier).

For each hotkey, add a `DebugHotKeyCategory` registration following the Spec 1 pattern. **Per CLAUDE.md gotcha note (commit `0390fdf`)**: hotkey rebinding required Spec 1's `Ctrl+Shift+T` → `Ctrl+Shift+Y` after a decompile audit of `DebugHotKeyCategory`. For Spec 2, audit your chosen letters against the same source before committing.

- [ ] **Step 1: Audit + register hotkeys**
- [ ] **Step 2: Implement the dispatch handlers**
- [ ] **Step 3: Build + smoke each hotkey**
- [ ] **Step 4: Remove the temporary `ModLogger.Info` lines added during Phase C smoke (Task 45)**
- [ ] **Step 5: Commit**

```bash
git add src/Debugging/Behaviors/DebugToolsBehavior.cs src/Features/Activities/Orders/PathScorer.cs
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
debug: add Spec 2 hotkeys for orders smoke testing

Ctrl+Shift+P (print path scores), Ctrl+Shift+O (force named-order
accept), Ctrl+Shift+I (toggle imprisoned profile), Ctrl+Shift+C
(force crossroads). Hotkey letters audited against
DebugHotKeyCategory in the v1.3.13 decompile per Spec 1's lesson
(commit 0390fdf rebind).

Removes temporary path-score logging from PathScorer.BumpPath.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Task 58: Save-load arc reconstruction test scenario

**Files:**
- No code changes; verification only.

Spec 2 §10 + §17 R9: a save taken mid-arc must reconstruct the spliced phase list on load via `OrderActivity.ReconstructArcOnLoad` (Task 8 + Task 14).

- [ ] **Step 1: Smoke the happy path**

1. Enlist, walk to a town until `garrisoned` commits.
2. Trigger `order_guard_post` accept (use the Ctrl+Shift+O debug hotkey from Task 57).
3. Pick `accept_diligent`. Verify `OrderActivity.Instance.ActiveNamedOrder.OrderStoryletId == "order_guard_post"`.
4. Wait until the arc is in its `mid` phase (`CurrentPhaseIndex == 1`, ~1-2 in-game hours).
5. Save the game. Exit to main menu.
6. Reload the save.
7. Verify `OrderActivity.Instance.ActiveNamedOrder` is still set with the same OrderStoryletId.
8. Verify `OrderActivity.Instance.CurrentPhaseIndex == 1` (still in mid phase).
9. Verify `OrderActivity.Instance.Phases.Count >= 3` (base on_duty + arc mid + arc resolve).
10. Continue play; verify mid-arc storylets fire normally and the arc resolves.

- [ ] **Step 2: Smoke the stale-arc-id failsafe**

1. Repeat steps 1-5 above.
2. Before reloading, edit `ModuleData/Enlisted/Storylets/order_guard_post.json` and change the `id` to `order_guard_post_renamed` (simulate content edit between save and load).
3. Reload the save.
4. Verify session log shows `Caught("ARC", "stale_arc_id_on_load", ...)`.
5. Verify `OrderActivity.Instance.ActiveNamedOrder == null` (cleared per the failsafe in §10).
6. Verify the player can continue play normally on the base on_duty phase.
7. Restore the storylet id afterward.

- [ ] **Step 3: No commit (verification-only).**

If either smoke fails, fix in a follow-up task before declaring Phase D complete.

---

## Task 59: Final playtest scenarios A-G

**Files:**
- No code changes; verification only.

Run all seven scenarios end-to-end. Mark each scenario PASS / FAIL with a one-line note.

- [ ] **Scenario A — Garrison-only run.** Enlist with a peaceful lord. Run for 30 in-game days at a friendly town. Verify: daily floor storylets every game day; ambient garrisoned storylets fire ~once per 4-6 hours; all 18 skills receive XP over the 30 days; no crashes; named-order accept-and-complete cycle ≥ 3 times.

- [ ] **Scenario B — High-war run.** Enlist with a faction at war. Run for 30 in-game days. Verify: profile transitions hit `marching` → `besieging` → `raiding` → `marching` cycles; at least 5 transition storylets fire mid-arc; combat skill XP heavy.

- [ ] **Scenario C — Path commit run (Ranger).** Enlist with a Battanian lord, pick `train_hard` intent on every named order, focus combat skills on Bow/Scouting/Riding. Verify: T3→T4 crossroads fires `crossroads_ranger_t3_to_t4`; commit option works; `committed_path == "ranger"` after; T7+ Ranger-only orders unlock.

- [ ] **Scenario D — Path resist run (Rogue).** Enlist with any lord; intentionally pick `slack`/`scheme` 3-4 times to bump rogue score. At T4 crossroads, pick "resist". Verify: future rogue increments halved; rogue score caps below 100 due to the halving; T7+ Rogue-only orders DO NOT unlock.

- [ ] **Scenario E — Lord captured + released.** Enlist with a lord. Force-capture via debug or wait for the AI to lose a battle. Verify: `lord_imprisoned` flag set; profile forced to `imprisoned`; imprisoned-pool storylets fire; lord released → flag clears, profile re-samples.

- [ ] **Scenario F — Lord killed.** Force-kill the lord (debug or natural). Verify: `lord_killed_farewell` Modal fires; `StopEnlist("lord_killed", honorable: true)` runs; `prior_service_*` flags emitted; OrderActivity torn down.

- [ ] **Scenario G — Cross-enlistment continuity.** Discharge from a Vlandian lord at T5 with `committed_path = ranger`. Re-enlist with a Khuzait lord at T1. Verify: previous `prior_service_culture_vlandia`, `prior_service_rank_5`, `prior_service_path_ranger` flags persist on player hero. Veteran-flavored storylets (if authored in Task 55-56) fire under the new lord.

- [ ] **Scenario H — Long-run fast-forward soak (Spec 2 §17 R10).** Enlist; set time to 4× and let the game run **untouched for 5 real minutes** (≈ 50 in-game days at 4×). PASS criteria: (a) news feed received roughly 12-18 entries over the 5 minutes — *not* hundreds; (b) drift summaries dominate the news feed (the "Past few days: ..." pattern), individual ambient entries are sparse; (c) any Modal interrupts that occurred (named-order accepts, profile transitions, crossroads if a tier-up landed) all surfaced correctly and were readable; (d) skill XP accumulated correctly across the 50 in-game days (open character sheet, verify per-skill totals match what the drift summaries reported); (e) no crashes; (f) no log spam. Then crank speed to 16× (if a speed mod is installed) and run another 5 minutes — verify ambient + drift go silent (only Modals surface), skill XP still accumulates correctly via the silent-apply path. **This is the canonical "shouldn't be annoying" verification**: if Scenario H passes, the wall-clock pacing layer is doing its job.

- [ ] **Step 2: Document results**

Record PASS/FAIL + notes for each scenario in a one-page verification doc (Task 60 writes this formally).

- [ ] **Step 3: No commit (verification-only). Bug fixes from FAIL scenarios go in follow-up tasks.**

---

## Task 60: Write verification doc + final status update

**Files:**
- Create: `docs/superpowers/plans/2026-04-20-orders-surface-verification.md`
- Modify: `docs/superpowers/plans/2026-04-20-orders-surface.md` (Status line)
- Modify: `docs/superpowers/specs/2026-04-20-orders-surface-design.md` (Status line)
- Modify: `docs/INDEX.md` (add Spec 2 entry to the Feature Lookup Quick Reference)
- Possibly: `docs/Features/Content/orders-surface.md` (new living-reference doc, mirroring Spec 1's `home-surface.md`)

- [ ] **Step 1: Write the verification doc**

Mirror Spec 1's verification doc shape (`docs/superpowers/plans/2026-04-19-enlisted-home-surface-verification.md` if present). Cover:
- Scenarios A-G with PASS/FAIL + notes
- Smoke playtest dates, save file ids, commit hashes
- Any open bugs / deferrals to a future content pass
- Storylet count summary (target ~260 vs actual)
- Validator state (Phase 13-17 all clean)

- [ ] **Step 2: Update plan + spec status lines**

Plan: `**Status:** Phase D complete YYYY-MM-DD on development (commits <first> → <last>). Spec 2 implementation complete.`

Spec: similar update.

- [ ] **Step 3: Add Spec 2 entry to `docs/INDEX.md`**

Mirror Spec 1's INDEX line. Add row to the Feature Lookup Quick Reference table:

```markdown
| **Orders Surface (Spec 2)** ✅ Implementation complete | [reference](Features/Content/orders-surface.md) / [design (frozen)](superpowers/specs/2026-04-20-orders-surface-design.md) | OrderActivity (offset 46), six duty profiles + imprisoned, ten named-order arcs, five-path emergent specialization with T3/T5/T7 crossroads, twelve scripted-effect primitives, ~260 storylets. Shipped YYYY-MM-DD on development; full commit trail <first> → <last>. |
```

- [ ] **Step 4: Author `docs/Features/Content/orders-surface.md` living reference**

Mirror Spec 1's `home-surface.md` structure: what OrderActivity does, lifecycle, profile resolution rules, named-order arc shape, path system, integration points, save offsets. **Living document — superseded design doc is frozen.**

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/plans/2026-04-20-orders-surface-verification.md \
        docs/superpowers/plans/2026-04-20-orders-surface.md \
        docs/superpowers/specs/2026-04-20-orders-surface-design.md \
        docs/INDEX.md \
        docs/Features/Content/orders-surface.md
git commit -F /c/Users/coola/commit_msg.txt
```

Commit message:
```
docs(spec): Spec 2 Orders surface complete after Task 59 smoke

Verification doc records Scenarios A-G results. Plan + spec status
lines updated. INDEX gains Feature Lookup entry. Living reference
docs/Features/Content/orders-surface.md authored — design spec
frozen.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

---

## Self-review checklist (run after authoring is complete)

After all 60 tasks land:

- [ ] **Spec coverage.** Walk through Spec 2 §1-§17 and confirm a task implements each section. Notably: §6 → Tasks 11-12, §7 → Tasks 5-6, §8 → Task 26, §9 → Tasks 16+44-49, §10 → Tasks 8+14, §11 → Task 13, §13 → Tasks 18-24+42, §14 → Tasks 36-40+51.
- [ ] **Placeholder scan.** Grep the plan file for `TBD`, `TODO`, `FIXME`, `???`. Should be zero hits except the one intentional `// TODO Task N` comment scaffolding noted in Task 6 (which is removed by Task 14).
- [ ] **Type consistency.** Method names referenced across tasks: `OrderActivity.Instance`, `OrderActivity.ReconstructArcOnLoad`, `NamedOrderArcRuntime.SpliceArc/UnspliceArc/RebuildSplicedPhasesOnLoad`, `CombatClassResolver.Resolve`, `DutyProfileSelector.Resolve`, `DutyProfileBehavior.ForceProfile`, `LootPoolResolver.ResolveAndGrant`, `OrderDisplayHelper.GetCurrent/IsOrderActive`, `PathScorer.OnIntentPicked/BumpPath`, `CultureTroopTreeHelper.BuildCultureTroopTree`. All consistent across the tasks that reference them.
- [ ] **Save offset claims.** 46 (OrderActivity), 47 (NamedOrderState), 84 (FormationClass alias), 85 (DutyProfileId). Spec 1 holds 45/82/83. Specs 3-5 inherit reservations 48-60 / 86+.
- [ ] **Validator phases.** 13 (pool coverage), 14 (arc integrity), 15 (crossroads completeness), 16 (loot pool sanity), 17 (rate caps + readonly). All authored in Tasks 36-40, with Phase 15 fully enforced after Task 47.

If gaps found, add or amend tasks inline and re-run this checklist.

---

## API corrections appendix

During execution of Tasks 1-11, the implementers verified each prescribed TaleWorlds / Newtonsoft call against `../Decompile/` and the existing codebase. Several plan-prescribed calls diverged from the actual v1.3.13 surface. This appendix logs the corrections so future task implementers know to cross-check and so a later plan-doc revision can fold them in.

### Task 4 — `StoryletCatalog` parse robustness

- **Plan prescribed:** `obj["profile_requires"]?.ToObject<List<string>>() ?? new List<string>()`
- **Actual:** `ToObject<T>()` throws `JsonSerializationException` on type mismatch; `??` does not catch exceptions. A content-author typo (e.g. `"requires_culture": "Vlandia"` as a string instead of an array) was killing the entire file's parse via the file-level catch in `LoadAll`.
- **Applied fix:** switch to `ParseStringList(obj["…"] as JArray)` (already exists in the file). Add a `ParseFloatDict(obj["…"] as JObject)` helper for the two dictionary fields. The `as JType` casts return null on mismatch, so the parse degrades gracefully to an empty collection.

### Task 5 — `HeroDeveloper.AddFocus` / `AddAttribute` third argument

- **Plan prescribed:** `HeroDeveloper.AddFocus(skill, amount)` and `HeroDeveloper.AddAttribute(attr, amount)`
- **Actual v1.3.13:** `AddFocus(SkillObject, int, bool checkUnspentFocusPoints)` and `AddAttribute(CharacterAttribute, int, bool checkUnspentPoints)`. The third parameter defaults to `true` in some overloads, which would deduct the grant from the unspent pool.
- **Applied fix:** pass `checkUnspentFocusPoints: false` / `checkUnspentPoints: false` — reward grants should not spend from the player's pool.

### Task 5 — `GainRenownAction.Apply` second argument type

- **Plan prescribed:** `GainRenownAction.Apply(Hero.MainHero, amount)` where `amount` was `int`.
- **Actual v1.3.13:** `Apply(Hero, float, bool)` — the second parameter is `float`, not `int`.
- **Applied fix:** cast via `(float)amount` at the call site.

### Task 8 — `StoryletCatalog` access pattern

- **Plan prescribed:** `StoryletCatalog.Instance?.Get(storyletId)` (instance-based)
- **Actual:** `StoryletCatalog` is a static class with a static `GetById(string)` method. No `Instance` property, no `Get` method.
- **Applied fix:** `StoryletCatalog.GetById(ActiveNamedOrder.OrderStoryletId)`.

### Task 8 — `ModLogger.Caught` misuse for stale-arc branch

- **Plan prescribed:** `ModLogger.Caught("ARC", "stale_arc_id_on_load", new Exception($"…"))` — using `Caught` as a general-purpose error log by wrapping dynamic context in a synthetic exception.
- **Actual convention:** `Caught` is reserved for real defensive try/catch. Known-branch early exits (like "save references a deleted storylet") go through `Expected(category, key, summary)` which has key+summary fields natively.
- **Applied fix:** `ModLogger.Expected("ARC", "stale_arc_id_on_load", $"OrderStoryletId='{id}' missing or has no arc; clearing")`. Reserve `Caught` for the outer try/catch around the actual reconstruction work.

### Tasks 7/8/10 — `FormationClass` namespace

- **Plan prescribed:** `using TaleWorlds.Library;` with comment `// FormationClass`
- **Actual v1.3.13:** `FormationClass` lives in `TaleWorlds.Core`, not `TaleWorlds.Library`. The plan's comment is factually wrong.
- **Applied fix:** use `using TaleWorlds.Core;` (already imported in most files that need it). In `EnlistedSaveDefiner`, register as `typeof(TaleWorlds.Core.FormationClass)`.

### Cross-task — doc-comment conventions

Multiple plan task snippets included XML doc-comments with forward-spec references ("Spec 2 PathScorer subscribes…", "Spec 4 will subscribe…", "Extracted from Y so Spec Z can…"). These violate AGENTS.md's rule "Comments describe current behavior — never changelogs, PR references, or 'added for X'". Each task's implementer was instructed to substitute behavioral one-liners. Future task implementers should expect to rewrite any doc comment the plan prescribes that references a future spec section or the reason for a change.

### Cross-task — brace style

The plan frequently used single-line `if (x) { return; }` / `{ break; }` guards. The project `.editorconfig` + AGENTS.md require expanded 3-line brace form on all control statements. Implementers expanded during each task.

### Cross-task — error-codes registry regeneration

Every C# source edit under `src/` that adds or removes lines in a file containing `ModLogger.Surfaced(...)` calls shifts those calls' line numbers in `docs/error-codes.md`. `validate_content.py` Phase 10 then fails. Implementers must run `/c/Python313/python.exe Tools/Validation/generate_error_codes.py` and stage the regenerated `docs/error-codes.md` in the same commit (or a dedicated chore commit, as in Tasks 1-3's combined `af129d0`).

### Task 13 — Narrowed to enlistment-lifecycle observation only (Revised Option A)

- **Design gap discovered during code review.** Task 13 prescribed a `LordStateListener` that would set `lord_imprisoned` and force the `Imprisoned` duty profile on lord-capture. The existing `EnlistmentBehavior.OnHeroPrisonerTaken` at `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9083` and `OnHeroKilled` at `:8912` already handle these events with grace-period semantics — `StopEnlist(..., retainKingdomDuringGrace: true)` + `StartDesertionGracePeriod(kingdom)` — giving the player 14 in-game days to re-enlist with another lord in the same kingdom. These two designs are mutually exclusive: a player cannot simultaneously remain bound to the captured lord (imprisoned profile) AND have a grace window to sign up with another.
- **Compounding:** `MbEvent` dispatches listeners LIFO. `EnlistmentBehavior` is registered after the new listener in `SubModule.cs`, so its handler fires first, clears `_enlistedLord` via `StopEnlist`, and the new listener's `IsTheLord(...)` guard returns false — the new handler's body was dead code.
- **Additional finding:** No scrutiny-triggered detention mechanic exists in the codebase. The scrutiny escalation system (`EscalationThresholds.cs`) fires narrative storylets at thresholds but never toggles `Hero.IsPrisoner` or mechanically detains the player. The `duty_imprisoned.json` storylet pool planned for Phase B would therefore have no existing write-path into the `Imprisoned` profile.
- **Decision applied.** Removed `OnHeroPrisonerTaken`, `OnHeroPrisonerReleased`, `OnHeroKilled` handlers and subscriptions. Kept `OnClanChangedKingdom` (clan-kingdom-change observer — useful telemetry) and the static `OnEnlistmentEnded` handler (captures `prior_service_culture_*` and `prior_service_rank_*` flags; tears down `OrderActivity` via `ActivityRuntime.Stop(activity, ActivityEndReason.Discharged)`). Renamed class + file to `EnlistmentLifecycleListener` — the original name became misleading after narrowing.
- **Infrastructure preserved for future use.** `DutyProfileIds.Imprisoned` enum value + `"imprisoned"` string constant, the `lord_imprisoned` flag name contract, the public `ActivityRuntime.Stop(Activity, ActivityEndReason)` method, and `ActivityEndReason.Discharged` remain in place. `Stop` + `Discharged` are used today by `OnEnlistmentEnded`. The `Imprisoned` profile stays reserved for a future scrutiny-overflow detention mechanic (e.g. Scrutiny ≥ `ScrutinyExposed` → narrative storylet's accept-option applies `set_flag("player_detained", DaysFromNow(N))` + `force_profile("imprisoned")`), which would land as a small follow-up spec, not in Spec 2.
- **Phase B impact.** The `duty_imprisoned.json` storylet pool task listed at plan line 130 is removed from Spec 2 scope. Re-add as a follow-up spec if/when the scrutiny-overflow detention mechanic is planned.

### Task 14 — `NamedOrderArcRuntime` API corrections

- **`StoryletCatalog.Instance.All()` doesn't exist.** `StoryletCatalog` is a static class with `public static IEnumerable<Storylet> All { get; }` — a property, no `Instance`, no parens. Matches the correction already logged under Task 8 for `GetById`. Applied via `foreach (var s in StoryletCatalog.All)`.
- **`ActivityRuntime.GetTypes()` didn't exist.** Plan prescribed `ActivityRuntime.Instance?.GetTypes()` twice (in `UnspliceArc` and `BuildAndSeatPhases`) but the private `_types` dictionary had no public accessor. Added `public IDictionary<string, ActivityTypeDefinition> GetTypes()` returning the internal dictionary. Chose `IDictionary<K,V>` over `IReadOnlyDictionary<K,V>` because `Activity.ResolvePhasesFromType` consumes the former and the latter isn't assignable to the former — a change to tighten to read-only-by-contract would require touching the `Activity` base class. Deferred.
- **`ctx.Storylet` and `ctx.SelectedOption.Intent` don't exist on `StoryletContext`.** Plan's effect-executor access pattern assumed fields that weren't on the context type. Added `public Storylet SourceStorylet { get; set; }` to `StoryletContext` and populated it in `StoryletEventAdapter.BuildModal` immediately before `RegisterPendingEffects`. Intent is now read via `GetStr(eff, "intent")` from the `start_arc` effect's params — consistent with how every other `Do*` primitive reads its config. No `Choice.Intent` field added.
- **Inline `NamedOrderArcRuntime` stub in `OrderActivity.cs` had to be deleted before the real class could be created.** Namespace collision would block compile. Task 8 left the stub at `OrderActivity.cs:103-111` with a `// TODO Task 14:` marker; deletion was a pre-step in Task 14.

### Task 15 + 15a — `DailyDriftApplicator` / `OrdersNewsFeedThrottle` API corrections

- **`StoryTier.NewsFeed` doesn't exist.** The enum at `src/Features/Content/StoryTier.cs` defines only `Log = 0, Pertinent = 1, Headline = 2, Modal = 3`. Closest semantic fit for the background drift summary is `StoryTier.Log` (scrollable-news tier per the enum's own doc comment). Applied.
- **`HeroDeveloper.AddSkillXp` default `shouldNotify: true` contradicts the "silent XP" intent.** Actual signature verified at `../Decompile/TaleWorlds.CampaignSystem/.../HeroDeveloper.cs:189` is `AddSkillXp(SkillObject skill, float rawXp, bool isAffectedByFocusFactor = true, bool shouldNotify = true)`. Plan's 2-arg call would have fired the vanilla per-skill-XP floating text every day. Explicit `shouldNotify: false` suppresses it so the hourly accumulator summary is the only UI surface.
- **Campaign speed threshold.** `Campaign.Current.SpeedUpMultiplier` defaults to `4f` (verified at `../Decompile/.../Campaign.cs:373`). Plan's `> 4f` threshold means "only suppress above default" — correct as stated, players on stock install rarely exceed 4x without a speed-mod.

### Task 16 — `PathScorer` API corrections

- **`QualityStore.Set` is 3-arg `(string id, int value, string reason)`, not 2-arg.** Plan's `Set(qualityKey, value)` is wrong; actual signature at `src/Features/Qualities/QualityStore.cs:178`. Applied via `BumpPath(path, amount, reason)` which threads a contextual reason (`"intent_pick"` or `"xp_gain"`) through to the store call.
- **5 path qualities must be registered in `quality_defs.json`.** Plan didn't include this — without registration, every `QualityStore.Get`/`Set` for `path_ranger_score` etc. would emit `Expected("QUALITY", "missing_def_...", ...)` on every bump, filling the session log. Added `path_ranger_score`, `path_enforcer_score`, `path_support_score`, `path_diplomat_score`, `path_rogue_score` — scope Global, kind Stored, range 0-100, default 0, no decay.
- **`NamedOrderArcRuntime.SpliceArc` had to be extended to call `PathScorer.OnIntentPicked(intent)`.** Plan's Task 16 doc comment said "invoked by NamedOrderArcRuntime.SpliceArc — see Task 14" but Task 14 didn't wire this. Added the call in Task 16 immediately after the `ARC spliced ...` info log.

### Task 16 — PathScorer XP-source rewiring (post-Phase-A quality fix)

- **Design defect discovered during quality-pass audit.** Plan-prescribed subscription to `EnlistmentBehavior.OnXPGained` captured the wrong signal: that event fires for enlistment-tier XP (the player's progression toward the next enlisted rank) with human-readable source tags like `"Combat"`, `"Training: One-Handed"`, `"Camp: Athletics"`, `"Reservist Re-entry"`. Per-skill XP events (from vanilla combat, `HeroDeveloper.AddSkillXp`, `EffectExecutor.DoSkillXp`) do NOT fire `OnXPGained`. The plan's `ExtractSkillFromSource` substring-matcher was trying to reverse-engineer skill attribution from these free-form tags.
- **Correct source:** `CampaignEvents.HeroGainedSkill` at `../Decompile/.../CampaignEvents.cs:595`, signature `IMbEvent<Hero, SkillObject, int, bool>` where the `int change` is the skill-level delta. Fires from `HeroDeveloper.ChangeSkillLevelFromXpChange:239` for every skill level-up on every hero, regardless of the XP source. Filters to `Hero.MainHero` in the handler to scope to player progression.
- **Applied fix:** Removed the `OnXPGained` subscription, its handler, its `-= / +=` guard, and the `ExtractSkillFromSource` helper. Subscribe to `CampaignEvents.HeroGainedSkill.AddNonSerializedListener(this, OnHeroGainedSkill)` instead — no duplicate-subscription guard needed because `IMbEvent.AddNonSerializedListener` deduplicates internally (distinct from project-internal static events like `EnlistmentBehavior.OnTierChanged`, which still require the `-= / +=` pattern).
- **Coverage improvement:** PathScorer now responds to every source of main-hero skill level-up — vanilla combat, training, daily drift, event rewards, debug grants — rather than just the narrow enlistment-tier XP tags. `BumpPath` receives the level delta (`change` parameter, typically 1) as the bump amount rather than a fixed +1, so rare multi-level XP grants bump the path proportionally.
- **Bump amount:** Changed from fixed `+1` per XP event to `+change` per level-up. With the `[0, 100]` clamp already in place, this is forward-compatible with any future XP-granting path that might spike `change > 1`.

### Cross-task — static-event subscription leak (Tasks 13, 14, 16)

`CampaignBehaviorManager.RegisterEvents()` is called on every new-game-start AND every save-load within a single process lifetime. Each invocation creates a fresh behavior instance and runs `RegisterEvents`. For subscriptions to `CampaignEvents.*` (i.e. `MbEvent`-backed campaign events), `AddNonSerializedListener(this, handler)` deduplicates per-listener internally — no guard needed. But for subscriptions to project-internal STATIC events (`EnlistmentBehavior.OnEnlistmentEnded`, `.OnXPGained`, `.OnTierChanged` — all `public static event Action<...>`), the invocation list accumulates across save-load cycles, causing N-fold handler firing after the Nth session.

Task 13 solved this for `OnEnlistmentEnded` in `EnlistmentLifecycleListener` with a `-= / +=` idempotent pair. Task 14's code review missed it as a separate concern (Task 14 only subscribes campaign events). Task 16's code review caught the same pattern in `PathScorer` and applied the same fix in commit `fc72bb7`. Future tasks subscribing to any `EnlistmentBehavior.On*` static event (or any new static event added elsewhere) MUST mirror this guard:

```csharp
EnlistmentBehavior.OnXPGained -= OnXPGained;
EnlistmentBehavior.OnXPGained += OnXPGained;
```

The handler method can be `static` or `instance` — `-=` matches by method group either way. Add a 2-line behavioral comment above the block: *"Static events persist across Campaign teardown; guard against duplicate subscription when RegisterEvents is re-invoked on save/load cycles."*

### Task 10 — FormationClass save-definer double-registration crash

- **Defect surfaced in-game on Phase A smoke.** The offset-84 registration `AddEnumDefinition(typeof(TaleWorlds.Core.FormationClass), 84)` shipped in commit `5143ef2` crashes `Module.Initialize()` before any mod logging is available. Native watchdog stack: `DefinitionContext.AddEnumDefinition` → `Dictionary.Insert` → `ArgumentException`.
- **Root cause.** `TaleWorlds.Core.SaveableCoreTypeDefiner.cs:66` already registers `FormationClass` at id 2008. The shared `DefinitionContext` keys `_enumDefinitions` by `Type` via plain `Dictionary.Add` (decompile `DefinitionContext.cs:118-124`) — any second registration of the same Type throws regardless of the offset id.
- **Applied fix.** Removed the offset-84 line. `OrderActivity.CachedCombatClass` and `NamedOrderState.CombatClassAtAccept` (both typed `FormationClass`) continue to serialize correctly via TaleWorlds' vanilla registration — the same pattern our `[Serializable]` classes already use for `CampaignTime` / `Hero` / `MBGUID` fields (none registered in `EnlistedSaveDefiner`, all work fine via vanilla's definers sharing the same `DefinitionContext`).
- **Save-compat.** No existing save references id 735084 because the game never successfully launched with Task 10's registration in place.
- **Convention tightened.** CLAUDE.md now documents the "never re-register a TaleWorlds built-in type" pitfall. Future save-definer additions must grep `../Decompile/TaleWorlds.Core/TaleWorlds.Core/SaveableCoreTypeDefiner.cs` and `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/SaveableCampaignTypeDefiner.cs` before registering any TW Type.
- **Offset 84 retired.** Intentionally unused in the Enlisted definer's 46-60 range. The removal-site comment explains why. If a future Spec needs an enum offset, claim 86+ (next available).

### Task 25 — `<ActivitiesData>` wildcard already covers new files

- **Plan prescribed (Step 2):** confirm the `<ActivitiesData>` ItemGroup in `Enlisted.csproj` is a wildcard (`Include="ModuleData\Enlisted\Activities\*.json"`) — if not, add MakeDir + Copy triplet.
- **Actual:** the Spec 1 entry at `Enlisted.csproj:671` is already a wildcard, and the matching `<Copy SourceFiles="@(ActivitiesData)" ...>` at line 776 + `<MakeDir Directories=".../Activities"/>` at line 758 were also pre-existing. No csproj edit was needed for the `duty_profiles.json` drop.
- **Applied fix.** Created the JSON only; first build deployed it via the existing AfterBuild Copy step.
- **Takeaway for later profile content (Tasks 27-32).** The six duty-pool storylet files (`duty_garrisoned.json`, `duty_marching.json`, etc.) target `ModuleData/Enlisted/Storylets/*.json`, which is a different ItemGroup (`<StoryletsData>` at the same neighborhood). Verify that wildcard before those tasks, but expect it to already be wildcarded for the same Spec 1 reason.

### Task 26 — LootPoolResolver API corrections

- **`ModulePaths.GetContentRoot` doesn't exist.** Plan prescribed `ModulePaths.GetContentRoot("Loot")`; actual API is `public static string GetContentPath(string subFolder)` at `src/Mod.Core/Util/ModulePaths.cs:103`. Returns the directory; caller joins with the filename via `Path.Combine`. Same shape every other catalog loader uses (`StoryletCatalog`, `QualityCatalog`, `ActivityTypeCatalog`). Applied `var dir = ModulePaths.GetContentPath("Loot"); var file = Path.Combine(dir, "loot_pools.json");` with an empty-dir Expected-log guard.
- **`ItemCategory.StringId` filter check resolves zero items silently.** Plan's JSON `"categories": ["HeadArmor", "Polearm", "Goods", ...]` values are `ItemObject.ItemTypeEnum` member names (verified at `../Decompile/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs:24-53`), not `ItemCategory` StringIds (which are trade-goods categorization keys like `"crossbow1"` / `"horse_pack"` / `"leather_armor"`). Plan's `def.Categories.Contains(element.Item.ItemCategory.StringId, ...)` would have silently zero-matched every faction_troop_tree pool; the resolver's `pool_no_match` Expected-log would have looked like a benign miss rather than a broken filter. **Applied fix:** parse JSON values at `ParsePool`-time via `Enum.TryParse<ItemObject.ItemTypeEnum>` into a typed `HashSet<ItemTypeEnum>`, and compare against `item.ItemType` (the property at `ItemObject.cs:220`, wraps the public field `ItemObject.Type` at `:72`). Unrecognized JSON entries (typo'd enum names) are silently dropped by `TryParse` — a future validator phase could warn on these.
- **`item.Tier` is the `ItemTiers` enum, not int.** `ItemObject.Tier` has type `ItemTiers` (enum defined alongside `ItemTypeEnum` at `ItemObject.cs:55-64`: `Tier1 ... Tier6, NumTiers`). Plan's `item.Tier < def.TierMin || item.Tier > def.TierMax` doesn't compile — enum-vs-int operator mismatch. Applied `(int)item.Tier` cast on the comparison side; kept `def.TierMin` / `def.TierMax` as plain ints sourced from the JSON `tier_range` array.
- **`DoGrantRandomItemFromPool` is already a dedicated method, not an inline switch case.** Plan's Step 3 replacement snippet was shaped as a `case "grant_random_item_from_pool": { ... } break;` body. The Phase A stub from Task 6 at `src/Features/Content/EffectExecutor.cs:541-552` is a standalone `private static void DoGrantRandomItemFromPool(EffectDecl eff)` method that the dispatch switch delegates to via `DoGrantRandomItemFromPool(eff)`. Applied: replaced the method body, kept the existing missing-pool_id early-exit (`Expected("EFFECT", "grant_random_item_pool_id_missing", ...)`), dropped the stub's `grant_random_item_pre_phaseB` probe log, added `using Enlisted.Features.Activities.Orders;` at the top of the file.
- **`PartyBase.MainParty?.ItemRoster?.AddToCounts` null-chain + discard idiom.** Matched the existing EffectExecutor grant_item pattern at `:317` / `:533`: `_ = (PartyBase.MainParty?.ItemRoster?.AddToCounts(item, count));` — null-conditional chain protects against a null main-party during early boot or cutscene transitions, `_ =` discards the returned `int` (vanilla signature returns slot index, unused by this caller).
- **Error-codes registry regen.** The edits to `EffectExecutor.cs` shortened `DoGrantRandomItemFromPool` by 2 lines (removed blank line + comment + the `grant_random_item_pre_phaseB` log). `EffectExecutor.cs` has zero `ModLogger.Surfaced` calls (grep-verified), so no registry shifts were produced by this edit. The new `LootPoolResolver.cs` adds only `Caught` + `Expected` calls (logs-only, not registered). Regen was run defensively and confirmed zero diff to `docs/error-codes.md`.
