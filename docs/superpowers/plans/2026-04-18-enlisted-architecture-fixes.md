# Enlisted Architecture Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the four Tier A + B architectural fixes from the 2026-04-18 design spec: `EnlistedTimeScope` for the three synchronous Gauntlet overlays, `QuartermasterPartyResolver` for scene-correctness, `docs/error-codes.md` registry, and the `Occupation.Soldier` pitfall documentation.

**Architecture:** Four independently-revertible commits on branch `development`. Each commit is gated by build success (`0 Warning(s) 0 Error(s)` on `Enlisted RETAIL | x64`) and content validation. Decompile verification steps are explicit plan tasks, not prose. New C# files require manual `<Compile Include=...>` entries in `Enlisted.csproj` per CLAUDE.md Rule 2.

**Tech Stack:** .NET Framework 4.7.2, C#, Bannerlord v1.3.13 modding APIs, Harmony patches, Python 3 for content validation, PowerShell/bash for build tooling.

**Design spec:** [docs/superpowers/specs/2026-04-18-enlisted-architecture-fixes-design.md](../specs/2026-04-18-enlisted-architecture-fixes-design.md)

---

## Pre-work

### Task 0: Verify starting state

**Files:** None (verification only).

- [ ] **Step 1: Confirm clean working tree on `development`**

```bash
cd C:/Dev/Enlisted/Enlisted
git status
```
Expected: `On branch development` and `nothing to commit, working tree clean`.
If not clean, stop and consult the user.

- [ ] **Step 2: Confirm baseline build is green**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 3: Confirm baseline validation state**

```bash
cd C:/Dev/Enlisted/Enlisted
python Tools/Validation/validate_content.py 2>&1 | grep -E "Errors:|\\[X\\]" | head -5
```
Expected: `Errors: 2` (the pre-existing `camp_decisions.json` tooltip errors — these are baseline noise, not from this work).

- [ ] **Step 4: Verify spec is committed**

```bash
git log --oneline docs/superpowers/specs/2026-04-18-enlisted-architecture-fixes-design.md | head -3
```
Expected: at least one commit touching the spec file; the top line should contain `3047173` or a later revision commit.

---

## Commit 1 — Item #2: `EnlistedTimeScope` (narrowed)

Narrow scope: only the three synchronous Gauntlet overlays (`ShowEquipmentSelector`, `ShowUpgradeScreen`, `ShowProvisionsScreen`). The 24 cross-frame menu-transition captures stay on `QuartermasterManager.CapturedTimeMode` pending deferred item #2b.

### Task 1.1: Decompile verification

**Files:** None (read-only verification in `../Decompile/`). Record findings in a scratch note to reference later when writing the commit message.

- [ ] **Step 1: Confirm `ActivateGameMenu` clobbers `TimeControlMode`**

```bash
sed -n '336,342p' "C:/Dev/Enlisted/Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameMenus/GameMenu.cs"
```
Expected: line 338 is `Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;`.
Finding: the engine sets time to Stop on every menu activation — which is exactly why the mod uses `SetTimeControlModeLock(true)`. Gauntlet overlays don't call `ActivateGameMenu` during their open lifetime, so this doesn't affect the narrowed scope.

- [ ] **Step 2: Confirm `SetTimeControlModeLock` is a plain setter**

```bash
sed -n '1743,1746p' "C:/Dev/Enlisted/Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Campaign.cs"
```
Expected: `public void SetTimeControlModeLock(bool isLocked)` with body `TimeControlModeLock = isLocked;`.
Finding: repeated `true` sets are idempotent; the scope's `Dispose` safely calls `SetTimeControlModeLock(false)` once.

- [ ] **Step 3: Confirm engine-internal mode writes are tick-driven (not Gauntlet-driven)**

```bash
grep -n "TimeControlMode = " "C:/Dev/Enlisted/Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Campaign.cs" | head -15
```
Expected: hits around lines 912–938 in logic gated by `MainParty.DefaultBehavior == AiBehavior.Hold`, `IsMainPartyWaiting`, army leadership. These writes fire on tick evaluation — not during Gauntlet overlay lifecycle events.
Finding: narrow scope is safe from engine overrides during the 3 overlay windows.

### Task 1.2: Create `EnlistedTimeScope`

**Files:**
- Create: `src/Mod.Core/TimeControl/EnlistedTimeScope.cs`

- [ ] **Step 1: Verify target directory exists, create if needed**

```bash
ls "C:/Dev/Enlisted/Enlisted/src/Mod.Core" 2>&1
```
If `TimeControl/` is not listed:
```bash
mkdir -p "C:/Dev/Enlisted/Enlisted/src/Mod.Core/TimeControl"
```

- [ ] **Step 2: Write `EnlistedTimeScope.cs`**

Write `C:/Dev/Enlisted/Enlisted/src/Mod.Core/TimeControl/EnlistedTimeScope.cs` with this content:

```csharp
using System;
using TaleWorlds.CampaignSystem;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.Core.TimeControl
{
    /// <summary>
    /// Scoped capture of Campaign.TimeControlMode with automatic pause + lock,
    /// restored on Dispose. Use ONLY for regions where the open call and close
    /// call run synchronously on the main campaign thread and the close is
    /// guarded by a finally block (the three Gauntlet overlays qualify).
    ///
    /// DO NOT use for cross-frame cases where a menu is activated via
    /// NextFrameDispatcher or where restore happens on an engine menu-exit
    /// callback — the scope would dispose before the work completes.
    /// See deferred item #2b in the 2026-04-18 architecture-fixes spec.
    /// </summary>
    public readonly struct EnlistedTimeScope : IDisposable
    {
        private readonly CampaignTimeControlMode? _captured;
        private readonly bool _ownedLock;

        private EnlistedTimeScope(CampaignTimeControlMode? captured, bool ownedLock)
        {
            _captured = captured;
            _ownedLock = ownedLock;
        }

        /// <summary>
        /// Capture the current TimeControlMode. If pauseAndLock is true (default),
        /// sets TimeControlMode.Stop and SetTimeControlModeLock(true) for the
        /// scope's lifetime.
        /// </summary>
        public static EnlistedTimeScope Capture(bool pauseAndLock = true)
        {
            if (Campaign.Current == null)
            {
                ModLogger.ErrorCode("TimeControl", "E-TIME-001",
                    "EnlistedTimeScope.Capture called with no active Campaign.Current");
                return default;
            }

            var captured = Campaign.Current.TimeControlMode;
            if (pauseAndLock)
            {
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                Campaign.Current.SetTimeControlModeLock(true);
            }
            return new EnlistedTimeScope(captured, pauseAndLock);
        }

        public void Dispose()
        {
            if (Campaign.Current == null || _captured == null)
            {
                return;
            }
            if (_ownedLock)
            {
                Campaign.Current.SetTimeControlModeLock(false);
            }
            // Defensive: skip restore if the engine changed the mode during the
            // scope (e.g., battle start, settlement entry). For the three narrow
            // Gauntlet sites, decompile verification showed no engine writes fire
            // during their windows, but the check is cheap insurance.
            var current = Campaign.Current.TimeControlMode;
            if (current != CampaignTimeControlMode.Stop && current != _captured.Value)
            {
                return;
            }
            Campaign.Current.TimeControlMode = QuartermasterManager.NormalizeToStoppable(_captured.Value);
        }
    }
}
```

### Task 1.3: Register `EnlistedTimeScope.cs` in the project

**Files:**
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Locate an existing `<Compile Include=` block near `Mod.Core`**

```bash
grep -n "Mod.Core" "C:/Dev/Enlisted/Enlisted/Enlisted.csproj" | head -10
```
Expected: multiple lines listing `src\Mod.Core\...` files. Pick any one as the insertion anchor.

- [ ] **Step 2: Add the compile include**

Insert the following line into `Enlisted.csproj` in the same `<ItemGroup>` as the other `src\Mod.Core\...` entries, placed alphabetically (after `SaveSystem` lines, before or after another entry — alphabetical by folder):

```xml
<Compile Include="src\Mod.Core\TimeControl\EnlistedTimeScope.cs" />
```

- [ ] **Step 3: Verify the include was added**

```bash
grep -n "EnlistedTimeScope" "C:/Dev/Enlisted/Enlisted/Enlisted.csproj"
```
Expected: one hit showing the line just added.

### Task 1.4: Build to verify `EnlistedTimeScope` compiles

- [ ] **Step 1: Run build**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.
If the build fails, read the errors carefully — common causes: namespace typo, missing using, misplaced `<Compile Include=...>` line. Fix and re-run.

### Task 1.5: Migrate `ShowEquipmentSelector` to use the scope

**Files:**
- Modify: `src/Features/Equipment/UI/QuartermasterEquipmentSelectorBehavior.cs`

- [ ] **Step 1: Add `using` for the new namespace**

Find the `using` block at the top of `QuartermasterEquipmentSelectorBehavior.cs` and add:
```csharp
using Enlisted.Mod.Core.TimeControl;
```
Insert it in alphabetical order within the existing `using Enlisted.*` cluster.

- [ ] **Step 2: Replace the `_capturedTimeMode` field with a `_timeScope` field**

Locate (approx line 40):
```csharp
// Captured time state for restoring after UI closes
private static CampaignTimeControlMode? _capturedTimeMode;
```
Replace with:
```csharp
// Time scope spanning the equipment selector's open lifetime.
// Held as a field because open (ShowEquipmentSelector) and close
// (CloseEquipmentSelector) are separate entry points — the scope
// captures on open and disposes in CloseEquipmentSelector's finally.
private static EnlistedTimeScope? _equipmentTimeScope;
```

- [ ] **Step 3: Replace the inline capture in `ShowEquipmentSelector`**

Locate the block (approx lines 211–218):
```csharp
// Capture current time mode and pause time while browsing equipment
if (Campaign.Current != null)
{
    _capturedTimeMode = Campaign.Current.TimeControlMode;
    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
    Campaign.Current.SetTimeControlModeLock(true);
    ModLogger.Debug("QuartermasterUI", $"Time paused (captured: {_capturedTimeMode})");
}
```
Replace with:
```csharp
// Capture via EnlistedTimeScope; disposed in CloseEquipmentSelector's finally.
_equipmentTimeScope = EnlistedTimeScope.Capture();
ModLogger.Debug("QuartermasterUI", "Time paused via EnlistedTimeScope for equipment selector");
```

- [ ] **Step 4: Replace the manual restore in `CloseEquipmentSelector`'s `finally`**

Locate the block in the `finally` of `CloseEquipmentSelector` (approx lines 342–355):
```csharp
// Restore time control mode
if (Campaign.Current != null)
{
    Campaign.Current.SetTimeControlModeLock(false);

    if (_capturedTimeMode.HasValue)
    {
        var normalized = QuartermasterManager.NormalizeToStoppable(_capturedTimeMode.Value);
        Campaign.Current.TimeControlMode = normalized;
        ModLogger.Debug("QuartermasterUI", $"Time restored: {normalized}");
    }

    _capturedTimeMode = null;
}
```
Replace with:
```csharp
// Dispose the scope to release lock and restore time mode.
_equipmentTimeScope?.Dispose();
_equipmentTimeScope = null;
ModLogger.Debug("QuartermasterUI", "Equipment selector time scope disposed");
```

- [ ] **Step 5: Build to verify**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. If any reference to `_capturedTimeMode` remains unresolved, grep for it inside the same file and remove or convert accordingly.

### Task 1.6: Migrate `ShowUpgradeScreen` to use the scope

**Files:**
- Modify: `src/Features/Equipment/UI/QuartermasterEquipmentSelectorBehavior.cs` (same file as Task 1.5)

- [ ] **Step 1: Replace the `_capturedUpgradeTimeMode` field with a scope field**

Locate (approx line 398):
```csharp
// Captured time state for upgrade screen
private static CampaignTimeControlMode? _capturedUpgradeTimeMode;
```
Replace with:
```csharp
// Time scope spanning the upgrade screen's open lifetime.
private static EnlistedTimeScope? _upgradeTimeScope;
```

- [ ] **Step 2: Replace the inline capture in `ShowUpgradeScreen`**

Locate the block (approx lines 421–428):
```csharp
// Capture current time mode and pause time while viewing upgrades
if (Campaign.Current != null)
{
    _capturedUpgradeTimeMode = Campaign.Current.TimeControlMode;
    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
    Campaign.Current.SetTimeControlModeLock(true);
    ModLogger.Debug("QuartermasterUI", $"Time paused for upgrade screen (captured: {_capturedUpgradeTimeMode})");
}
```
Replace with:
```csharp
// Capture via EnlistedTimeScope; disposed in CloseUpgradeScreen's finally.
_upgradeTimeScope = EnlistedTimeScope.Capture();
ModLogger.Debug("QuartermasterUI", "Time paused via EnlistedTimeScope for upgrade screen");
```

- [ ] **Step 3: Replace the manual restore in `CloseUpgradeScreen`**

Read `CloseUpgradeScreen` (approx lines 461–540) and locate its `finally` block that releases the lock and restores `_capturedUpgradeTimeMode`. Replace the block with:
```csharp
_upgradeTimeScope?.Dispose();
_upgradeTimeScope = null;
ModLogger.Debug("QuartermasterUI", "Upgrade screen time scope disposed");
```

Keep all other cleanup (layer release, movie release, view model teardown) intact.

- [ ] **Step 4: Grep for any residual `_capturedUpgradeTimeMode` references**

```bash
grep -n "_capturedUpgradeTimeMode" "C:/Dev/Enlisted/Enlisted/src/Features/Equipment/UI/QuartermasterEquipmentSelectorBehavior.cs"
```
Expected: 0 hits. If any remain, remove or convert.

- [ ] **Step 5: Build to verify**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

### Task 1.7: Migrate `ShowProvisionsScreen` to use the scope

**Files:**
- Modify: `src/Features/Equipment/UI/QuartermasterProvisionsBehavior.cs`

- [ ] **Step 1: Add `using` for the new namespace**

Add to the top of `QuartermasterProvisionsBehavior.cs`:
```csharp
using Enlisted.Mod.Core.TimeControl;
```

- [ ] **Step 2: Replace `_capturedTimeMode` field**

Locate the static field declaration for `_capturedTimeMode` near the top of the class. Replace with:
```csharp
// Time scope spanning the provisions screen's open lifetime.
private static EnlistedTimeScope? _provisionsTimeScope;
```

- [ ] **Step 3: Replace the inline capture in `ShowProvisionsScreen`**

Locate the block (approx lines 159–165):
```csharp
// Capture current time mode and pause time while purchasing provisions
if (Campaign.Current != null)
{
    _capturedTimeMode = Campaign.Current.TimeControlMode;
    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
    Campaign.Current.SetTimeControlModeLock(true);
    ModLogger.Debug("QuartermasterUI", $"Time paused for provisions screen (captured: {_capturedTimeMode})");
}
```
Replace with:
```csharp
// Capture via EnlistedTimeScope; disposed in CloseProvisionsScreen's finally.
_provisionsTimeScope = EnlistedTimeScope.Capture();
ModLogger.Debug("QuartermasterUI", "Time paused via EnlistedTimeScope for provisions screen");
```

- [ ] **Step 4: Replace the manual restore in `CloseProvisionsScreen`**

Locate the manual restore inside `CloseProvisionsScreen`'s `finally` and replace with:
```csharp
_provisionsTimeScope?.Dispose();
_provisionsTimeScope = null;
ModLogger.Debug("QuartermasterUI", "Provisions screen time scope disposed");
```

- [ ] **Step 5: Grep for residuals**

```bash
grep -n "_capturedTimeMode" "C:/Dev/Enlisted/Enlisted/src/Features/Equipment/UI/QuartermasterProvisionsBehavior.cs"
```
Expected: 0 hits.

- [ ] **Step 6: Build to verify**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

### Task 1.8: Validate content + final build check

- [ ] **Step 1: Run content validation**

```bash
cd C:/Dev/Enlisted/Enlisted
python Tools/Validation/validate_content.py 2>&1 | grep -E "Errors:|\\[X\\]" | head -5
```
Expected: `Errors: 2` (unchanged from baseline). If a new error appears, investigate and resolve before continuing.

- [ ] **Step 2: Final build check**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

### Task 1.9: Document manual smoke test (for user)

**Files:** None (this task produces a note for the user to run in-game).

- [ ] **Step 1: Present the smoke-test checklist to the user**

Post this message to the user before committing:

> **Manual smoke test for Commit 1.** Load a save where the player is enlisted. From the Camp Hub, open each of the three Gauntlet overlays in sequence and confirm time restores correctly after closing:
> 1. Camp Hub → Visit Quartermaster → Browse weapons (grid appears) → close (either select or cancel) → time resumes.
> 2. Camp Hub → Visit Quartermaster → Request upgrades → close → time resumes.
> 3. Camp Hub → Visit Quartermaster → Request provisions → close → time resumes.
>
> Check `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\Session-A_*.log` for any `E-TIME-001` codes — none expected on happy path.
>
> Confirm result before I commit.

Wait for the user's confirmation. If they report issues, diagnose against the log and adjust. Do not commit until the user confirms smoke test passed.

### Task 1.10: Commit

- [ ] **Step 1: Stage the four files**

```bash
cd C:/Dev/Enlisted/Enlisted
git add Enlisted.csproj \
        src/Mod.Core/TimeControl/EnlistedTimeScope.cs \
        src/Features/Equipment/UI/QuartermasterEquipmentSelectorBehavior.cs \
        src/Features/Equipment/UI/QuartermasterProvisionsBehavior.cs
git status
```
Expected: exactly these four files staged.

- [ ] **Step 2: Create the commit**

```bash
cd C:/Dev/Enlisted/Enlisted
git commit -m "$(cat <<'EOF'
refactor(core): introduce EnlistedTimeScope for Gauntlet-overlay time capture

Replaces the hand-rolled capture+restore pattern in the three synchronous
Gauntlet overlays (equipment selector, upgrade screen, provisions screen)
with a scoped IDisposable that captures on construction and restores on
Dispose. Each overlay holds the scope as a field and disposes in its
Close*Screen method's finally block, so any exception between open and
close still releases the time lock.

Scope is NARROW by design. The 24 cross-frame captures in CampMenuHandler
and EnlistedMenuBehavior (capture sync + menu activate via
NextFrameDispatcher + restore on menu-exit callback) stay on the existing
static QuartermasterManager.CapturedTimeMode pattern pending deferred
item #2b — an IDisposable using block cannot span a next-frame boundary.

Decompile-verified (v1.3.13):
- GameMenu.cs:338 (ActivateGameMenu clobbers TimeControlMode = Stop —
  not relevant to Gauntlet-overlay windows, which don't call it)
- Campaign.cs:1743 (SetTimeControlModeLock is a plain field setter,
  idempotent on repeated true)
- Campaign.cs:912-938 (engine-internal mode writes are tick-driven,
  not Gauntlet-driven)

New file registered in Enlisted.csproj per CLAUDE.md Rule 2.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 3: Verify commit**

```bash
cd C:/Dev/Enlisted/Enlisted
git log --oneline -1
```
Expected: top commit title is `refactor(core): introduce EnlistedTimeScope for Gauntlet-overlay time capture`.

---

## Commit 2 — Item #8: `QuartermasterPartyResolver`

### Task 2.1: Decompile verification (already done — record evidence)

**Files:** None.

- [ ] **Step 1: Confirm `ConversationCharacterData` tolerates null party**

```bash
sed -n '27,37p' "C:/Dev/Enlisted/Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Conversation/ConversationCharacterData.cs"
```
Expected: constructor signature includes `PartyBase party = null`, body just stores `Party = party;`. No null check. Finding: no crash on null; only downstream scene selection is affected.

### Task 2.2: Create `QuartermasterPartyResolver`

**Files:**
- Create: `src/Features/Equipment/Managers/QuartermasterPartyResolver.cs`

- [ ] **Step 1: Verify target directory exists**

```bash
ls "C:/Dev/Enlisted/Enlisted/src/Features/Equipment/Managers"
```
Expected: `QMInventoryState.cs` listed. If directory doesn't exist, stop and investigate — the spec assumes colocation with existing managers.

- [ ] **Step 2: Write `QuartermasterPartyResolver.cs`**

Write `C:/Dev/Enlisted/Enlisted/src/Features/Equipment/Managers/QuartermasterPartyResolver.cs` with this content:

```csharp
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Enlisted.Features.Enlistment.Behaviors;

namespace Enlisted.Features.Equipment.Managers
{
    /// <summary>
    /// Resolves the effective party for a conversation with the quartermaster hero.
    ///
    /// The QM is spawned via HeroCreator.CreateSpecialHero and parked in the
    /// enlisted lord's home settlement — it has no MobileParty of its own
    /// (qm.PartyBelongedTo is always null). Bannerlord's conversation scene
    /// selection (land / sea / terrain) reads the party's position, so passing
    /// null produces degraded scene selection.
    ///
    /// This resolver substitutes the enlisted lord's party, giving every call
    /// site the same "who is the QM's effective party" answer and making scene
    /// selection consistent.
    ///
    /// Decompile reference:
    /// ConversationCharacterData (../Decompile/TaleWorlds.CampaignSystem/
    /// TaleWorlds.CampaignSystem.Conversation/ConversationCharacterData.cs:27)
    /// tolerates a null PartyBase without throwing — but a null-party
    /// conversation has degraded scene selection.
    /// </summary>
    public static class QuartermasterPartyResolver
    {
        /// <summary>
        /// Returns a non-null party for a conversation with the QM hero, or null
        /// if both the QM and the enlisted lord are unreachable (illegal state
        /// during enlistment).
        ///
        /// Callers that receive null MUST log E-QM-PARTY-001 and abort the
        /// conversation open; do not pass null into ConversationCharacterData.
        /// </summary>
        public static PartyBase GetConversationParty(Hero qmHero)
        {
            if (qmHero?.PartyBelongedTo?.Party is { } qmParty)
            {
                return qmParty;
            }

            var enlistment = EnlistmentBehavior.Instance;
            return enlistment?.EnlistedLord?.PartyBelongedTo?.Party;
        }
    }
}
```

### Task 2.3: Register `QuartermasterPartyResolver.cs` in the project

**Files:**
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Add the compile include**

Insert into `Enlisted.csproj` in the same `<ItemGroup>` as other `src\Features\Equipment\Managers\...` entries:

```xml
<Compile Include="src\Features\Equipment\Managers\QuartermasterPartyResolver.cs" />
```

- [ ] **Step 2: Verify the include was added**

```bash
grep -n "QuartermasterPartyResolver" "C:/Dev/Enlisted/Enlisted/Enlisted.csproj"
```
Expected: one hit.

### Task 2.4: Build to verify the resolver compiles

- [ ] **Step 1: Run build**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

### Task 2.5: Grep for all call sites that currently use `qm.PartyBelongedTo`

**Files:** None (survey).

- [ ] **Step 1: Find every usage**

```bash
grep -rn "PartyBelongedTo" "C:/Dev/Enlisted/Enlisted/src" --include="*.cs" | grep -i "qm\|quartermaster"
```

Expected sites (verify against actual grep output):
- `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` — in `OnQuartermasterSelected` and `OpenQuartermasterConversationForBaggageRequest`
- `src/Features/Enlistment/Behaviors/MusterMenuHandler.cs` — in `OpenQuartermasterConversation`
- `src/Mod.GameAdapters/Patches/QuartermasterConversationScenePatch.cs` — in the Harmony prefix

Record the file:line of each hit; each becomes a migration step below.

### Task 2.6: Migrate each call site

For each hit in Task 2.5, apply the same transform. Below shows the pattern for the `EnlistedMenuBehavior.OnQuartermasterSelected` site; apply analogously to the others.

**Files:**
- Modify: each file from the Task 2.5 grep list

- [ ] **Step 1: Add `using` for the resolver namespace**

At the top of each modified file, ensure this `using` is present (insert alphabetically among the `using Enlisted.*` cluster):
```csharp
using Enlisted.Features.Equipment.Managers;
```

- [ ] **Step 2: Apply the migration pattern at each `new ConversationCharacterData(...)` call site that uses `qm.PartyBelongedTo?.Party`**

Locate the pattern (example from `EnlistedMenuBehavior.cs:4231`):
```csharp
var qmData = new ConversationCharacterData(qm.CharacterObject, qm.PartyBelongedTo?.Party);
```
Replace with:
```csharp
var party = QuartermasterPartyResolver.GetConversationParty(qm);
if (party == null)
{
    ModLogger.ErrorCode("Quartermaster", "E-QM-PARTY-001",
        "Both QM and enlisted lord have no party — cannot open conversation with correct scene");
    InformationManager.DisplayMessage(new InformationMessage(
        new TextObject("{=qm_party_unavailable}The quartermaster cannot be reached right now.").ToString()));
    return;
}
var qmData = new ConversationCharacterData(qm.CharacterObject, party);
```

- [ ] **Step 3: Verify each file still has required using directives**

After editing, check that `TaleWorlds.Library` (for `InformationManager` / `InformationMessage`) and `TaleWorlds.Localization` (for `TextObject`) are imported in any file where the fallback message is now used.

```bash
grep -E "^using TaleWorlds\\.(Library|Localization);" "C:/Dev/Enlisted/Enlisted/src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs"
```
Expected: both present (they already are from prior work).

For `MusterMenuHandler.cs` run the same check and add imports if missing.

- [ ] **Step 4: Build to verify**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

### Task 2.7: Thin the `QuartermasterConversationScenePatch` Harmony patch

**Files:**
- Modify: `src/Mod.GameAdapters/Patches/QuartermasterConversationScenePatch.cs`

- [ ] **Step 1: Update the patch to use the resolver**

Replace the current patch logic (which inlines the "fall back to lord's party" substitution) with a call to the resolver. The Prefix now looks like:

```csharp
[HarmonyPatch(typeof(ConversationManager), "OpenMapConversation")]
internal class QuartermasterConversationScenePatch
{
    [HarmonyPrefix]
    public static void Prefix(
        ref ConversationCharacterData playerCharacterData,
        ref ConversationCharacterData conversationPartnerData)
    {
        try
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return;
            }

            var qmHero = enlistment.GetOrCreateQuartermaster();
            if (qmHero == null || conversationPartnerData.Character != qmHero.CharacterObject)
            {
                return; // Not a QM conversation
            }

            // Substitute the effective party for scene selection (lord's party
            // when QM has none). Keeping the patch — some engine entry points
            // reach OpenMapConversation without going through our migrated
            // call sites, so we still need a safety net here.
            var effectiveParty = Managers.QuartermasterPartyResolver.GetConversationParty(qmHero);
            if (effectiveParty == null)
            {
                ModLogger.Warn("Interface",
                    "QM conversation scene patch: resolver returned null — leaving partner data unchanged");
                return;
            }

            conversationPartnerData = new ConversationCharacterData(
                character: conversationPartnerData.Character,
                party: effectiveParty,
                noHorse: conversationPartnerData.NoHorse,
                noWeapon: conversationPartnerData.NoWeapon,
                spawnAfterFight: conversationPartnerData.SpawnedAfterFight,
                isCivilianEquipmentRequiredForLeader: false,
                isCivilianEquipmentRequiredForBodyGuardCharacters: false,
                noBodyguards: conversationPartnerData.NoBodyguards
            );

            ModLogger.Debug("Interface",
                "QM conversation scene fix: effective party resolved via QuartermasterPartyResolver");
        }
        catch (System.Exception ex)
        {
            ModLogger.Error("Interface", "Error in QuartermasterConversationScenePatch", ex);
        }
    }
}
```

Ensure the `using Enlisted.Features.Equipment;` or equivalent is present so `Managers.QuartermasterPartyResolver` resolves. Alternative: add `using Enlisted.Features.Equipment.Managers;` and drop the `Managers.` qualifier.

- [ ] **Step 2: Build to verify**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

### Task 2.8: Validate + final build check

- [ ] **Step 1: Run content validation**

```bash
cd C:/Dev/Enlisted/Enlisted
python Tools/Validation/validate_content.py 2>&1 | grep -E "Errors:|\\[X\\]" | head -5
```
Expected: `Errors: 2` (unchanged).

- [ ] **Step 2: Final build check**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

### Task 2.9: Document manual smoke test (for user)

- [ ] **Step 1: Present the smoke-test checklist to the user**

Post this message to the user:

> **Manual smoke test for Commit 2.** Two scenarios must pass:
> 1. **Land scene.** From a save where the player is enlisted on land, open Camp Hub → Visit Quartermaster. Confirm the QM conversation opens with the correct land-terrain scene (not water).
> 2. **Sea scene.** From a save where the lord's party is at sea (if available), trigger a QM conversation (via Camp Hub or muster-complete auto-open). Confirm the sea scene is used.
>
> Check `Session-A_*.log` for `E-QM-PARTY-001` — none expected on happy path. If you don't have a sea save, skip scenario 2 but note it in the commit comment.
>
> Confirm result before I commit.

Wait for user confirmation.

### Task 2.10: Commit

- [ ] **Step 1: Stage the files**

```bash
cd C:/Dev/Enlisted/Enlisted
git add Enlisted.csproj \
        src/Features/Equipment/Managers/QuartermasterPartyResolver.cs \
        src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs \
        src/Features/Enlistment/Behaviors/MusterMenuHandler.cs \
        src/Mod.GameAdapters/Patches/QuartermasterConversationScenePatch.cs
git status
```
Expected: the resolver + csproj + the modified callers staged. Any additional callers revealed by the Task 2.5 grep should also be staged.

- [ ] **Step 2: Create the commit**

```bash
cd C:/Dev/Enlisted/Enlisted
git commit -m "$(cat <<'EOF'
refactor(equipment): centralize QM party resolution via QuartermasterPartyResolver

Every call site that constructs ConversationCharacterData for the QM hero
now goes through QuartermasterPartyResolver.GetConversationParty, which
substitutes the enlisted lord's party when the QM has none. Scene
selection (land / sea / terrain) is now consistent across every entry
point — previously the Harmony patch fixed only engine OpenMapConversation
calls, and direct ConversationCharacterData construction passed null.

Decompile-verified (v1.3.13):
- ConversationCharacterData.cs:27 tolerates null PartyBase (just stores
  it, no crash). The resolver exists for scene-selection correctness
  and invariant centralization, not crash prevention.

The Harmony patch is retained (thinned) because some engine paths reach
OpenMapConversation without going through our callers. On null resolver
return, callers log E-QM-PARTY-001 and abort conversation rather than
pass null into ConversationCharacterData.

New file registered in Enlisted.csproj.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 3: Verify commit**

```bash
cd C:/Dev/Enlisted/Enlisted
git log --oneline -1
```
Expected: top commit title is `refactor(equipment): centralize QM party resolution via QuartermasterPartyResolver`.

---

## Commit 3 — Item #5: Error-code registry

### Task 3.1: Extract every error code from `src/`

**Files:** None (survey).

- [ ] **Step 1: Grep for every error-code invocation**

```bash
grep -rn "ErrorCode(" "C:/Dev/Enlisted/Enlisted/src" --include="*.cs" | grep -oE "\"E-[A-Z-]+-[0-9]+\"[^)]*\"[^\"]+\"" | head -100
```
Capture the output. Each match produces a `(code, message)` pair. Expected codes span `E-QM-*`, `E-QM-UI-*`, `E-UI-*`, `E-DIALOG-*`, `E-MUSTER-*`, `E-SYSTEM-*`, `E-TIME-*`, `E-QM-PARTY-*`.

- [ ] **Step 2: Also capture codes used via `ErrorCodeOnce`**

```bash
grep -rn "ErrorCodeOnce(" "C:/Dev/Enlisted/Enlisted/src" --include="*.cs" | head -20
```

### Task 3.2: Write `docs/error-codes.md`

**Files:**
- Create: `docs/error-codes.md`

- [ ] **Step 1: Write the registry file**

Write `C:/Dev/Enlisted/Enlisted/docs/error-codes.md` with the following content, populating the tables from the Task 3.1 grep output. The seed tables below come from the spec and are known to match codes in the codebase as of 2026-04-18; the SYSTEM and MUSTER tables must be populated from the grep output.

```markdown
# Error Codes

Codes follow the pattern `E-<SUBSYSTEM>-<NNN>`, where subsystem is an uppercase
short identifier (`QM`, `QM-UI`, `UI`, `DIALOG`, `SYSTEM`, `TIME`, `MUSTER`,
`QM-PARTY`) and NNN is a three-digit sequence within that subsystem.

**Policy.** Gaps are fine — never renumber. When adding a code, append the next
unused number in the relevant subsystem. Removed codes (code deleted from
source) get strikethrough + a `removed YYYY-MM-DD` note; do not delete rows
so log readers can still look up historical codes.

## Engine parallel

The Bannerlord engine has its own time-mode capture pattern for popups at
`../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs:2218`
(`_timeControlModeBeforePopUpOpened`). Our `E-TIME-*` codes and the upcoming
menu-lifecycle refactor (deferred item #2b) should stay aligned with this
pattern where practical.

## Quartermaster (QM)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-QM-008 | No enlisted lord during QM spawn | Check `IsEnlisted` before `GetOrCreateQuartermaster` | Enlistment |
| E-QM-009 | QM spawn: lord has no culture | Investigate how lord was created | Enlistment |
| E-QM-010 | `HeroCreator.CreateSpecialHero` returned null | Check template + birth settlement | Enlistment |
| E-QM-011 | Failed to apply wealthy QM attire (non-fatal) | Log only; QM still spawns | Enlistment |
| E-QM-014 | Gauntlet UI failed; formerly fell to conversation selection | Removed 2026-04-18 (fallback deleted) | — |
| E-QM-025 | `GetOrCreateQuartermaster` returned null while enlisted | Upstream spawn failure — check earlier E-QM-* codes | Enlistment |
| ~~E-QM-110~~ | ~~`ShowArmorSlotPicker` failed~~ | removed 2026-04-18 — method deleted | — |
| E-QM-PARTY-001 | Both QM and lord have no party | Illegal state; investigate enlistment | Enlistment |

## Quartermaster UI (QM-UI)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-QM-UI-001 | `ScreenManager.TopScreen` null on equipment popup | Likely a modal blocking; retry after frame | QM UI |
| E-QM-UI-002 | Equipment selector failed to open (exception caught) | Inspect inner exception in log | QM UI |
| E-QM-UI-003 | Upgrade screen failed to open | Inspect inner exception | QM UI |
| E-QM-UI-004 | Provisions screen failed to open | Inspect inner exception | QM UI |
| E-QM-UI-005 | `TopScreen` null when adding provisions layer | Retry after frame | QM UI |
| E-QM-UI-006 | `TopScreen` null when adding upgrade layer | Retry after frame | QM UI |

## Interface (UI)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-UI-038 | Error opening quartermaster conversation | See exception log | Interface |
| E-UI-041 | Error opening baggage request conversation | See exception log | Interface |
| E-UI-046 | Failed to toggle orders accordion | Interface bug | Interface |
| E-UI-047 | Failed to toggle decisions accordion | Interface bug | Interface |

## Dialogue (DIALOG)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-DIALOG-001 | Unknown dialogue action in JSON | Add handler or fix JSON | Dialogue |
| E-DIALOG-002 | Unknown gate condition in JSON | Add handler or fix JSON | Dialogue |

## Time Control (TIME)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-TIME-001 | `EnlistedTimeScope.Capture` with no `Campaign.Current` | Defensive; should not occur in normal play | Time Control |

## Muster (MUSTER)

Populate from grep output captured in Task 3.1. Each row uses the literal
`ModLogger.ErrorCode(...)` message string as "Meaning" and the owning
subsystem (typically `Muster`).

## System (SYSTEM)

Populate from grep output captured in Task 3.1. Each row uses the literal
`ModLogger.ErrorCode(...)` message string as "Meaning". Do not invent
meanings.
```

Once the file is written, populate the MUSTER and SYSTEM tables from your Task 3.1 grep output. Use this exact row format:

```
| E-SYSTEM-001 | <exact message string from ModLogger.ErrorCode call> | <remediation derived from exception context> | <subsystem> |
```

### Task 3.3: Link the registry from `CLAUDE.md`

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Add the link**

Open `CLAUDE.md` and locate the skills table (or create a new "Key Documentation" section if none exists at the Claude-specific layer). Add a reference line pointing to the new registry:

```markdown
**Error code registry:** See [docs/error-codes.md](docs/error-codes.md) for the canonical list of `E-*-NNN` codes, their meanings, and remediation. New codes MUST be registered there.
```

Place it near the "Context7 MCP Library IDs" or "MCP Server Usage" section — anywhere that fits the "reference docs" clustering.

- [ ] **Step 2: Also link from `AGENTS.md`**

Open `AGENTS.md` and add a row to the "Key Documentation" table:

```
| Error code registry | [docs/error-codes.md](docs/error-codes.md) |
```

### Task 3.4: Validate + commit

- [ ] **Step 1: Run content validation**

```bash
cd C:/Dev/Enlisted/Enlisted
python Tools/Validation/validate_content.py 2>&1 | grep -E "Errors:|\\[X\\]" | head -5
```
Expected: `Errors: 2`.

- [ ] **Step 2: Build (should be unaffected — no C# changes)**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Stage and commit**

```bash
cd C:/Dev/Enlisted/Enlisted
git add docs/error-codes.md CLAUDE.md AGENTS.md
git commit -m "$(cat <<'EOF'
docs: add error-code registry

Canonical index of every E-*-NNN code currently in the codebase, with
one-line meaning, remediation, and owning subsystem. Populated by
grep of src/ at commit time. Policy is append-only: gaps are fine,
never renumber; removed codes get strikethrough + removed-date note.

Linked from CLAUDE.md and AGENTS.md Key Documentation.

The registry notes the engine's own popup-pause pattern at
CampaignEvents.cs:2218 (_timeControlModeBeforePopUpOpened) for future
alignment when deferred item #2b (cross-frame menu-lifecycle capture)
is designed.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 4: Verify commit**

```bash
cd C:/Dev/Enlisted/Enlisted
git log --oneline -1
```
Expected: top commit title is `docs: add error-code registry`.

---

## Commit 4 — Item #7: `Occupation.Soldier` pitfall

### Task 4.1: Verify decompile citations

**Files:** None (read-only check).

- [ ] **Step 1: Confirm the wanderer-gate line is where the spec says**

```bash
sed -n '1270,1282p' "C:/Dev/Enlisted/Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs"
```
Expected: around line 1274, `conversation_wanderer_on_condition` body checks `CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Wanderer`.

- [ ] **Step 2: Confirm `AddWandererConversations` is at the other cited line**

```bash
sed -n '605,610p' "C:/Dev/Enlisted/Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs"
```
Expected: around line 607, `private void AddWandererConversations(CampaignGameStarter starter)`.

### Task 4.2: Append pitfall #11 to `AGENTS.md`

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Locate the Common Pitfalls list**

```bash
grep -n "Common Pitfalls" "C:/Dev/Enlisted/Enlisted/AGENTS.md"
```
Expected: one hit (the `## Common Pitfalls` heading). The list below it ends at item 10.

- [ ] **Step 2: Append item 11**

After the line `10. Relying on external API docs (wrong version)`, insert:

```
11. Creating mod-spawned heroes (QM, etc.) with `Occupation.Wanderer` triggers
    vanilla wanderer-introduction dialogue. Use `Occupation.Soldier`. Verified
    at `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs:607`
    (`AddWandererConversations`) and `:1274` (`conversation_wanderer_on_condition`,
    checks `Occupation == Occupation.Wanderer`).
```

### Task 4.3: Add a doc comment at the source site

**Files:**
- Modify: `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`

- [ ] **Step 1: Locate the `SetNewOccupation` call**

```bash
grep -n "SetNewOccupation(Occupation.Soldier)" "C:/Dev/Enlisted/Enlisted/src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs"
```
Expected: one hit, around line 9802.

- [ ] **Step 2: Replace the existing inline comment with a detailed one**

The current line (around 9802) reads:
```csharp
// Use Soldier occupation to avoid triggering companion recruitment dialogue
// (Wanderer occupation causes vanilla companion recruitment to appear)
qm.SetNewOccupation(Occupation.Soldier);
```

Replace with:
```csharp
// Use Occupation.Soldier (not Wanderer) to prevent vanilla wanderer
// dialogue gating from firing against the QM. The engine gates the
// wanderer-introduction dialogue on Occupation == Wanderer — see
// ../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs:1274
// (conversation_wanderer_on_condition) and :607 (AddWandererConversations).
// See AGENTS.md "Common Pitfalls" item 11.
qm.SetNewOccupation(Occupation.Soldier);
```

### Task 4.4: Validate + commit

- [ ] **Step 1: Run content validation**

```bash
cd C:/Dev/Enlisted/Enlisted
python Tools/Validation/validate_content.py 2>&1 | grep -E "Errors:|\\[X\\]" | head -5
```
Expected: `Errors: 2`.

- [ ] **Step 2: Build (should be unaffected — comment-only code change)**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64 2>&1 | tail -8
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Stage and commit**

```bash
cd C:/Dev/Enlisted/Enlisted
git add AGENTS.md src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs
git commit -m "$(cat <<'EOF'
docs: document Occupation.Soldier workaround for QM hero

Pitfall entry #11 in AGENTS.md explains why the QM hero uses
Occupation.Soldier instead of Wanderer, with decompile citations
pointing at the actual dialogue gate:
  LordConversationsCampaignBehavior.cs:607  (AddWandererConversations)
  LordConversationsCampaignBehavior.cs:1274 (conversation_wanderer_on_condition,
                                              checks Occupation == Wanderer)

The previous inline comment at EnlistmentBehavior.cs:9802 is replaced
with a fuller one that cites the pitfall entry and the decompile
locations, so future maintainers don't "fix" it back to Wanderer.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 4: Verify commit**

```bash
cd C:/Dev/Enlisted/Enlisted
git log --oneline -1
```
Expected: top commit title is `docs: document Occupation.Soldier workaround for QM hero`.

---

## Post-work

### Task 5: Push the series to `origin/development`

- [ ] **Step 1: Show the four commits to be pushed**

```bash
cd C:/Dev/Enlisted/Enlisted
git log --oneline origin/development..HEAD
```
Expected: exactly four commits in this order (newest first):
1. `docs: document Occupation.Soldier workaround for QM hero`
2. `docs: add error-code registry`
3. `refactor(equipment): centralize QM party resolution via QuartermasterPartyResolver`
4. `refactor(core): introduce EnlistedTimeScope for Gauntlet-overlay time capture`

- [ ] **Step 2: Push**

```bash
cd C:/Dev/Enlisted/Enlisted
git push origin development 2>&1 | tail -5
```
Expected: `<prev-sha>..<new-sha>  development -> development`.

- [ ] **Step 3: Confirm HEAD matches origin**

```bash
cd C:/Dev/Enlisted/Enlisted
git log --oneline origin/development..HEAD
```
Expected: empty output (HEAD is at `origin/development`).

---

## Rollback playbook

If any commit introduces a regression after merge, revert just that commit — they are independently revertible by design. Example:

```bash
git revert <commit-sha>
git push origin development
```

The four commits have no internal dependencies. Reverting Commit 1 (EnlistedTimeScope) leaves Commits 2-4 functional. Reverting Commit 2 (resolver) leaves 1, 3, 4 functional. Docs commits (3, 4) are trivially revertible.
