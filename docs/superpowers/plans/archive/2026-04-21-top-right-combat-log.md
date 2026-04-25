# Top-Right Combat Log Implementation Plan

> **RETIRED (2026-04-24).** This plan is a frozen-in-time execution record. The top-right live feed, drag/resize behavior, persisted UI state, background modes, and updated feature documentation shipped on `development` in the combat-log work chain (`d963c56`, `424cffc`, `2d64497`, `69927f9`, `5f595c1`). Current behavior is documented at [`docs/Features/UI/enlisted-combat-log.md`](../../../Features/UI/enlisted-combat-log.md).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the Enlisted campaign-map combat log to the top-right corner, keep it as an always-visible scrollable live feed, and add a manual expanded mode with remembered expanded size.

**Architecture:** Extend the existing custom Enlisted combat log instead of importing Bannerlord's full chat stack. `EnlistedCombatLogVM` gains compact/expanded state plus size properties, a new `CombatLogUiStateStore` persists only the expanded size to an Enlisted-owned per-user file, and `EnlistedCombatLogBehavior` takes over drag-resize orchestration using standard widgets in the rebuilt top-right prefab. The bottom-right avoidance logic is removed because the log no longer lives in the alert corner.

**Tech Stack:** C# on .NET Framework 4.7.2, Bannerlord v1.3.13 TaleWorlds UI APIs (`TaleWorlds.GauntletUI`, `TaleWorlds.Engine.GauntletUI`, `TaleWorlds.InputSystem`, `TaleWorlds.ScreenSystem`), existing Enlisted Gauntlet XML prefabs/brushes, Newtonsoft.Json for lightweight per-user UI-state persistence. No unit-test framework in this repo; verification is `dotnet build`, `python Tools/Validation/validate_content.py`, and in-game smoke using the existing custom combat log flow.

**Design spec:** [docs/superpowers/specs/2026-04-21-top-right-combat-log-design.md](../../specs/2026-04-21-top-right-combat-log-design.md)

---

## Notes for the implementer

- **Working tree.** The repo may already have an unrelated tracked change in `.codex/config.toml`. Do not stage or revert it.
- **csproj registration.** Any new `.cs` file must be added to `Enlisted.csproj` as an explicit `<Compile Include="..."/>` entry.
- **GUI assets.** `GUI/Prefabs/Interface/EnlistedCombatLog.xml` is already copied by the existing csproj `Content` item. No new XML content registration is needed if that file is only edited in place.
- **Persistence scope.** The spec explicitly forbids Bannerlord native chat config keys and campaign save data for expanded-size persistence. Store it in an Enlisted-owned per-user file under `Environment.SpecialFolder.LocalApplicationData` using `Path.Combine(...)`.
- **Logging.** New `ModLogger.Caught(...)` calls should use uppercase literal categories like `"INTERFACE"`.
- **TaleWorlds API verification.** Any UI/input API questions must be verified against `../Decompile/`, not web docs.
- **Build command.** Use:
  ```bash
  dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
  ```
- **Validation command.** Use:
  ```bash
  python Tools/Validation/validate_content.py
  ```

---

## File structure

### New C# files (1)

```text
src/Features/Interface/Utils/CombatLogUiStateStore.cs
  -- stores/restores expanded combat-log size in an Enlisted-owned per-user JSON file
```

### Modified C# files (3)

```text
src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs
  -- add compact/expanded state, size properties, resize-preview state, expand command

src/Features/Interface/Behaviors/EnlistedCombatLogBehavior.cs
  -- load/store UI state, wire top-right widgets, drive drag-resize, remove bottom-right reposition logic

Enlisted.csproj
  -- register CombatLogUiStateStore.cs
```

### Modified UI / docs (2)

```text
GUI/Prefabs/Interface/EnlistedCombatLog.xml
  -- move anchor to top-right, add header/expand affordance, add expanded resize widgets

docs/Features/UI/enlisted-combat-log.md
  -- update behavior and layout documentation to match the shipped top-right design
```

### Unchanged by design

```text
src/Mod.GameAdapters/Patches/InformationManagerDisplayMessagePatch.cs
  -- routing logic stays as-is; no behavior change needed for top-right/expanded UI

GUI/Brushes/EnlistedCombatLog.xml
  -- keep current text/link styling unless implementation discovers a specific missing style
```

---

## Execution order

**Task 0** validates the baseline.  
**Task 1** adds the persistent expanded-size store and VM state.  
**Task 2** rebuilds the prefab for top-right compact/expanded presentation.  
**Task 3** wires behavior-side resize/persistence and removes bottom-right avoidance logic.  
**Task 4** refreshes docs and runs full acceptance verification.

Do not reorder Tasks 1-3. The prefab depends on the new VM bindings, and the behavior depends on the new widget ids.

---

## Task 0: Verify baseline and isolate scope

**Files:** None (verification only)

- [ ] **Step 1: Confirm the starting working tree**

```bash
cd C:/Dev/Enlisted/Enlisted
git status --short
```

Expected:

- `.codex/config.toml` may already appear as modified; leave it alone.
- No other tracked-file changes should be present before starting implementation.

- [ ] **Step 2: Confirm the current combat-log files exist and are the expected targets**

```bash
cd C:/Dev/Enlisted/Enlisted
rg -n "class EnlistedCombatLogBehavior|class EnlistedCombatLogVM|EnlistedCombatLogWidget" src GUI/Prefabs
```

Expected: hits in:

- `src/Features/Interface/Behaviors/EnlistedCombatLogBehavior.cs`
- `src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs`
- `GUI/Prefabs/Interface/EnlistedCombatLog.xml`

- [ ] **Step 3: Build the current baseline**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded.`

---

## Task 1: Add expanded-size persistence and view-model state

**Files:**
- Create: `src/Features/Interface/Utils/CombatLogUiStateStore.cs`
- Modify: `src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Create the per-user UI-state store**

Add `src/Features/Interface/Utils/CombatLogUiStateStore.cs` with this shape:

```csharp
using System;
using System.IO;
using Enlisted.Mod.Core.Logging;
using Newtonsoft.Json;

namespace Enlisted.Features.Interface.Utils
{
    internal sealed class CombatLogUiState
    {
        public float ExpandedWidth { get; set; } = 640f;
        public float ExpandedHeight { get; set; } = 420f;
    }

    internal static class CombatLogUiStateStore
    {
        internal const float DefaultExpandedWidth = 640f;
        internal const float DefaultExpandedHeight = 420f;
        internal const float MinExpandedWidth = 480f;
        internal const float MinExpandedHeight = 320f;

        private static string StatePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Enlisted",
                "combat_log_ui_state.json");

        public static CombatLogUiState Load()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return new CombatLogUiState();
                }

                var json = File.ReadAllText(StatePath);
                return JsonConvert.DeserializeObject<CombatLogUiState>(json) ?? new CombatLogUiState();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to load combat log UI state", ex);
                return new CombatLogUiState();
            }
        }

        public static void Save(float width, float height)
        {
            try
            {
                var state = new CombatLogUiState
                {
                    ExpandedWidth = width,
                    ExpandedHeight = height
                };

                var directory = Path.GetDirectoryName(StatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(StatePath, json);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to save combat log UI state", ex);
            }
        }

        public static float ClampWidth(float width, float viewportWidth)
        {
            var maxWidth = Math.Max(MinExpandedWidth, viewportWidth - 80f);
            return Math.Max(MinExpandedWidth, Math.Min(width, maxWidth));
        }

        public static float ClampHeight(float height, float viewportHeight)
        {
            var maxHeight = Math.Max(MinExpandedHeight, viewportHeight - 80f);
            return Math.Max(MinExpandedHeight, Math.Min(height, maxHeight));
        }
    }
}
```

- [ ] **Step 2: Register the new file in `Enlisted.csproj`**

Add this compile entry near the existing interface utilities:

```xml
<Compile Include="src\Features\Interface\Utils\CombatLogUiStateStore.cs"/>
```

- [ ] **Step 3: Extend `EnlistedCombatLogVM` with compact/expanded state and size bindings**

Edit `src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs` to add:

```csharp
private const float CompactWidth = 500f;
private const float CompactHeight = 340f;

private bool _isExpanded;
private float _containerWidth;
private float _containerHeight;
private float _expandedWidth;
private float _expandedHeight;
private float _resizePreviewWidth;
private float _resizePreviewHeight;
private bool _showResizeFrame;
private string _expandButtonText;

[DataSourceProperty]
public bool IsExpanded { get; set; }

[DataSourceProperty]
public float ContainerWidth { get; set; }

[DataSourceProperty]
public float ContainerHeight { get; set; }

[DataSourceProperty]
public float ResizePreviewWidth { get; set; }

[DataSourceProperty]
public float ResizePreviewHeight { get; set; }

[DataSourceProperty]
public bool ShowResizeFrame { get; set; }

[DataSourceProperty]
public bool ShowResizeHandle => IsExpanded;

[DataSourceProperty]
public string ExpandButtonText { get; set; }

[DataSourceProperty]
public string HeaderText => "Campaign Log";
```

Implement those properties with the same explicit backing-field pattern already used elsewhere in `EnlistedCombatLogVM`. At minimum, `IsExpanded` must also raise `ShowResizeHandle`, because that value is derived:

```csharp
[DataSourceProperty]
public bool IsExpanded
{
    get => _isExpanded;
    set
    {
        if (_isExpanded != value)
        {
            _isExpanded = value;
            OnPropertyChangedWithValue(value, nameof(IsExpanded));
            OnPropertyChangedWithValue(ShowResizeHandle, nameof(ShowResizeHandle));
        }
    }
}
```

Follow the same pattern for `ContainerWidth`, `ContainerHeight`, `ResizePreviewWidth`, `ResizePreviewHeight`, `ShowResizeFrame`, and `ExpandButtonText`, then add these methods:

```csharp
public void RestoreExpandedSize(float width, float height)
{
    _expandedWidth = width;
    _expandedHeight = height;
    ApplyCurrentSize();
}

public void ExecuteToggleExpanded()
{
    IsExpanded = !IsExpanded;
    ExpandButtonText = IsExpanded ? "[-]" : "[+]";
    ApplyCurrentSize();
}

public void BeginResizePreview()
{
    ResizePreviewWidth = _expandedWidth;
    ResizePreviewHeight = _expandedHeight;
    ShowResizeFrame = true;
}

public void UpdateResizePreview(float width, float height)
{
    ResizePreviewWidth = width;
    ResizePreviewHeight = height;
}

public void CommitExpandedSize(float width, float height)
{
    _expandedWidth = width;
    _expandedHeight = height;
    ShowResizeFrame = false;
    ApplyCurrentSize();
}

public void CancelResizePreview()
{
    ShowResizeFrame = false;
}

private void ApplyCurrentSize()
{
    ContainerWidth = IsExpanded ? _expandedWidth : CompactWidth;
    ContainerHeight = IsExpanded ? _expandedHeight : CompactHeight;
}
```

Initialize the constructor with:

```csharp
ExpandButtonText = "[+]";
_expandedWidth = CombatLogUiStateStore.DefaultExpandedWidth;
_expandedHeight = CombatLogUiStateStore.DefaultExpandedHeight;
ApplyCurrentSize();
```

Remove the old `PositionYOffset` property and `UpdatePositioning(bool isMenuOpen)` method entirely. Top-right layout should no longer move based on bottom-right overlays.

- [ ] **Step 4: Build**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add Enlisted.csproj \
  src/Features/Interface/Utils/CombatLogUiStateStore.cs \
  src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs
git commit -m "feat(interface): add combat log expanded state"
```

---

## Task 2: Rebuild the combat-log prefab for top-right compact and expanded modes

**Files:**
- Modify: `GUI/Prefabs/Interface/EnlistedCombatLog.xml`

- [ ] **Step 1: Move the root widget to the top-right and bind width/height**

Replace the root widget geometry with:

```xml
<Widget Id="EnlistedCombatLogWidget"
        DoNotAcceptEvents="false"
        WidthSizePolicy="Fixed"
        HeightSizePolicy="Fixed"
        SuggestedWidth="@ContainerWidth"
        SuggestedHeight="@ContainerHeight"
        HorizontalAlignment="Right"
        VerticalAlignment="Top"
        MarginRight="30"
        MarginTop="30"
        IsVisible="@IsVisible"
        AlphaFactor="@ContainerAlpha"
        Command.HoverBegin="OnUserInteraction"
        Command.MouseScroll="OnUserInteraction">
```

This intentionally replaces the old bottom-right anchor and removes `PositionYOffset`.

- [ ] **Step 2: Add a header row with a manual expand button**

Add a simple header inside the root widget above the scrollable area:

```xml
<Widget Id="HeaderBar"
        WidthSizePolicy="StretchToParent"
        HeightSizePolicy="Fixed"
        SuggestedHeight="34"
        MarginLeft="8"
        MarginRight="8"
        MarginTop="4"
        HorizontalAlignment="Right"
        VerticalAlignment="Top">
  <Children>
    <RichTextWidget WidthSizePolicy="CoverChildren"
                    HeightSizePolicy="CoverChildren"
                    HorizontalAlignment="Left"
                    VerticalAlignment="Center"
                    Brush="SPGeneral.MediumText"
                    Brush.FontSize="16"
                    Text="@HeaderText" />

    <ButtonWidget Id="ExpandButton"
                  WidthSizePolicy="Fixed"
                  HeightSizePolicy="Fixed"
                  SuggestedWidth="42"
                  SuggestedHeight="26"
                  HorizontalAlignment="Right"
                  VerticalAlignment="Center"
                  Brush="ButtonBrush1"
                  Command.Click="ExecuteToggleExpanded">
      <Children>
        <RichTextWidget WidthSizePolicy="CoverChildren"
                        HeightSizePolicy="CoverChildren"
                        HorizontalAlignment="Center"
                        VerticalAlignment="Center"
                        Brush="SPGeneral.MediumText"
                        Brush.FontSize="14"
                        Text="@ExpandButtonText" />
      </Children>
    </ButtonWidget>
  </Children>
</Widget>
```

- [ ] **Step 3: Shift the scrollable body below the header and add resize widgets**

Update the scrollable body margins so it starts below the header, then add these two widgets at the bottom of the root:

```xml
<Widget Id="ResizeFrameWidget"
        WidthSizePolicy="Fixed"
        HeightSizePolicy="Fixed"
        SuggestedWidth="@ResizePreviewWidth"
        SuggestedHeight="@ResizePreviewHeight"
        HorizontalAlignment="Right"
        VerticalAlignment="Top"
        MarginRight="0"
        MarginTop="0"
        IsVisible="@ShowResizeFrame"
        Sprite="BlankWhiteSquare_9"
        Color="#FFFFFF22" />

<Widget Id="ResizeHandle"
        WidthSizePolicy="Fixed"
        HeightSizePolicy="Fixed"
        SuggestedWidth="18"
        SuggestedHeight="18"
        HorizontalAlignment="Right"
        VerticalAlignment="Bottom"
        MarginRight="2"
        MarginBottom="2"
        IsVisible="@ShowResizeHandle"
        Sprite="BlankWhiteSquare_9"
        Color="#A37434FF" />
```

Also move the existing `ScrollablePanelContainer` down with `MarginTop="34"` so the header does not overlap messages.

- [ ] **Step 4: Sanity check the XML bindings**

```bash
cd C:/Dev/Enlisted/Enlisted
rg -n "@ContainerWidth|@ContainerHeight|ExecuteToggleExpanded|ResizeFrameWidget|ResizeHandle|VerticalAlignment=\"Top\"" GUI/Prefabs/Interface/EnlistedCombatLog.xml
```

Expected: all six bindings/ids appear exactly once in the intended places.

- [ ] **Step 5: Commit**

```bash
git add GUI/Prefabs/Interface/EnlistedCombatLog.xml
git commit -m "feat(interface): move combat log prefab to top right"
```

---

## Task 3: Wire top-right behavior, manual resize, and persistence

**Files:**
- Modify: `src/Features/Interface/Behaviors/EnlistedCombatLogBehavior.cs`

- [ ] **Step 1: Add state-store and drag-resize fields**

At the top of `EnlistedCombatLogBehavior.cs`, add:

```csharp
using Enlisted.Features.Interface.Utils;
using TaleWorlds.InputSystem;
```

Then add fields:

```csharp
private Widget _resizeHandleWidget;
private Widget _resizeFrameWidget;
private bool _isResizing;
private Vec2 _resizeStartMousePosition;
private float _resizeStartWidth;
private float _resizeStartHeight;
private float _viewportWidth;
private float _viewportHeight;
```

Delete the bottom-right-specific fields:

```csharp
private bool _wasArmyManagementOpen;
private bool _wasRepositionedUp;
```

- [ ] **Step 2: Load the persisted expanded size and capture widget references**

Inside `InitializeUI()`, after creating `_dataSource` and loading the movie, add:

```csharp
var uiState = CombatLogUiStateStore.Load();
_dataSource.RestoreExpandedSize(uiState.ExpandedWidth, uiState.ExpandedHeight);

_resizeHandleWidget = rootWidget.FindChild("ResizeHandle", true);
_resizeFrameWidget = rootWidget.FindChild("ResizeFrameWidget", true);

_viewportWidth = rootWidget.Size.X;
_viewportHeight = rootWidget.Size.Y;
```

Keep the existing scroll-panel and message-list discovery unchanged.

- [ ] **Step 3: Replace bottom-right repositioning with drag-resize handling in `OnTick`**

Remove the entire block that calls `IsBarExtended()`, `IsArmyOverlayExtended()`, and `UpdatePositioning(...)`.

Replace it with:

```csharp
if (_movie?.Movie?.RootWidget != null)
{
    _viewportWidth = _movie.Movie.RootWidget.Size.X;
    _viewportHeight = _movie.Movie.RootWidget.Size.Y;
}

HandleResize();
```

Then add a new private method:

```csharp
private void HandleResize()
{
    if (_dataSource == null || !_dataSource.IsExpanded || _resizeHandleWidget == null)
    {
        return;
    }

    var hovered = _resizeHandleWidget.EventManager?.HoveredView == _resizeHandleWidget;

    if (!_isResizing && hovered && Input.IsKeyPressed(InputKey.LeftMouseButton))
    {
        _isResizing = true;
        _resizeStartMousePosition = Input.MousePositionPixel;
        _resizeStartWidth = _dataSource.ContainerWidth;
        _resizeStartHeight = _dataSource.ContainerHeight;
        _dataSource.BeginResizePreview();
        return;
    }

    if (_isResizing)
    {
        if (Input.IsKeyDown(InputKey.LeftMouseButton))
        {
            var delta = Input.MousePositionPixel - _resizeStartMousePosition;
            var width = CombatLogUiStateStore.ClampWidth(_resizeStartWidth + delta.X, _viewportWidth);
            var height = CombatLogUiStateStore.ClampHeight(_resizeStartHeight + delta.Y, _viewportHeight);
            _dataSource.UpdateResizePreview(width, height);
        }
        else
        {
            var width = CombatLogUiStateStore.ClampWidth(_dataSource.ResizePreviewWidth, _viewportWidth);
            var height = CombatLogUiStateStore.ClampHeight(_dataSource.ResizePreviewHeight, _viewportHeight);
            _dataSource.CommitExpandedSize(width, height);
            CombatLogUiStateStore.Save(width, height);
            _isResizing = false;
        }
    }
}
```

This uses the same intuitive drag direction as the native widget: dragging right increases width, dragging down increases height. The important ownership split is fixed: behavior owns drag detection, VM owns exposed size state, store owns persistence/clamping.

- [ ] **Step 4: Remove the dead bottom-right helpers**

Delete these methods and their dead supporting fields once the new top-right flow compiles:

- `UpdatePositioning(bool isMenuOpen)`
- `IsBarExtended()`
- `IsArmyOverlayExtended()`

Then confirm they are gone:

```bash
cd C:/Dev/Enlisted/Enlisted
rg -n "UpdatePositioning\\(|IsBarExtended\\(|IsArmyOverlayExtended\\(" src/Features/Interface/Behaviors/EnlistedCombatLogBehavior.cs src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs
```

Expected: no hits.

- [ ] **Step 5: Build**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/Features/Interface/Behaviors/EnlistedCombatLogBehavior.cs
git commit -m "feat(interface): add expandable top-right combat log behavior"
```

---

## Task 4: Refresh docs and run acceptance verification

**Files:**
- Modify: `docs/Features/UI/enlisted-combat-log.md`

- [ ] **Step 1: Update the feature doc to match the new behavior**

Edit `docs/Features/UI/enlisted-combat-log.md` so these statements become true:

- it is described as **top-right**, not bottom-right,
- compact mode is called out as always visible and scrollable,
- expanded mode is manual,
- expanded mode is resizable,
- expanded size is remembered across sessions,
- bottom-right party/army repositioning is no longer presented as the active design.

Use concise prose; do not leave the old bottom-right narrative in place as historical text.

- [ ] **Step 2: Run build + validation**

```bash
cd C:/Dev/Enlisted/Enlisted
dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64
python Tools/Validation/validate_content.py
```

Expected:

- `Build succeeded.`
- validation completes without new errors attributable to this UI change.

- [ ] **Step 3: Manual in-game acceptance smoke**

Perform this exact smoke pass:

1. Launch Bannerlord with Enlisted enabled and load an enlisted campaign save.
2. Confirm the live feed appears in the **top-right** of the campaign map.
3. Scroll in compact mode and confirm older/newer lines are reachable.
4. Click the expand button and confirm the panel grows while remaining anchored top-right.
5. Drag the resize handle and confirm width/height change while staying on-screen.
6. Collapse the log and confirm compact size returns to the fixed default.
7. Re-expand and confirm the last resized expanded dimensions are restored.
8. Trigger a map conversation and confirm the layer still suspends/resumes correctly.
9. Confirm bottom-right alerts no longer collide with the combat log.
10. Restart the game, reload the same save, expand the log, and confirm the expanded size persisted.

Expected: all ten checks pass without off-screen rendering, overlap regressions, or lost message routing.

- [ ] **Step 4: Commit**

```bash
git add docs/Features/UI/enlisted-combat-log.md
git commit -m "docs(interface): update combat log guide for top-right redesign"
```

---

## Spec coverage checklist

- **Top-right hard requirement:** Task 2 Step 1, Task 3 Step 3, Task 4 Step 3
- **Always-visible compact live feed:** Task 1 Step 3, Task 2 Step 1
- **Compact scrollability:** Task 2 preserves the scrollable panel; Task 4 Step 3 verifies it
- **Manual-only expansion:** Task 1 Step 3 (`ExecuteToggleExpanded`), Task 2 Step 2
- **Expanded resize:** Task 2 Step 3, Task 3 Step 3
- **Remembered expanded size:** Task 1 Step 1, Task 3 Step 2/3, Task 4 Step 3
- **No bottom-right alert collision:** Task 3 Step 3/4, Task 4 Step 3

No spec gaps remain. The only intentionally deferred detail is live tuning of default compact/expanded dimensions during the Task 4 smoke pass.
