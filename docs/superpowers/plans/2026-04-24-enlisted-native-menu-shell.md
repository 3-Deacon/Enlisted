# Enlisted Native Menu Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a custom Gauntlet renderer for Enlisted-owned native GameMenus while keeping native GameMenu as the authoritative behavior source and fallback.

**Architecture:** The shell reads the active `Campaign.Current.CurrentMenuContext`, snapshots only Enlisted-owned native menu text/options, maps those native option ids into display-only sections, and invokes native consequences through `MenuContext.InvokeConsequence(index)`. A Gauntlet layer renders collapse/expand, cards, tabs, scroll areas, and tooltips, then hides itself when vanilla menus or screens take over.

**Tech Stack:** C# .NET Framework 4.7.2, TaleWorlds CampaignSystem GameMenu APIs, TaleWorlds Gauntlet UI, XML prefabs, xUnit source-level unit tests, existing Enlisted logging and validation.

---

## File Structure

- Create `src/Features/Interface/Shell/EnlistedNativeMenuSnapshot.cs`
  - Immutable native menu snapshot types: menu id, title text, context text, and option rows.
- Create `src/Features/Interface/Shell/EnlistedNativeMenuAdapter.cs`
  - Reads `Campaign.Current.CurrentMenuContext`, filters supported Enlisted menu ids, snapshots options, and invokes native option consequences.
- Create `src/Features/Interface/Shell/EnlistedMenuPresentationCatalog.cs`
  - Display-only metadata for supported Enlisted menu ids and option grouping.
- Create `src/Features/Interface/Shell/EnlistedNativeMenuShellBehavior.cs`
  - Owns Gauntlet layer lifecycle, refresh tick, native override rules, and fallback behavior.
- Create `src/Features/Interface/ViewModels/EnlistedNativeMenuShellVM.cs`
  - ViewModel for collapsed/expanded state, section list, tab state, and command handlers.
- Create `src/Features/Interface/ViewModels/EnlistedNativeMenuSectionVM.cs`
  - ViewModel for a rendered section/card.
- Create `src/Features/Interface/ViewModels/EnlistedNativeMenuOptionVM.cs`
  - ViewModel for a native option button.
- Create `GUI/Prefabs/Interface/EnlistedNativeMenuShell.xml`
  - Native-looking collapsible shell prefab.
- Modify `src/Mod.Entry/SubModule.cs`
  - Register `EnlistedNativeMenuShellBehavior`.
- Modify `Enlisted.csproj`
  - Add compile entries and prefab copy entry.
- Create `Tools/Tests/Enlisted.UnitTests/EnlistedNativeMenuShellDesignTests.cs`
  - Source-level regression tests for native-backend/custom-renderer rules.
- Modify `docs/Features/UI/news-reporting-system.md`
  - Document the custom shell as a renderer over native GameMenu, not separate gameplay logic.

---

### Task 1: Lock the Native-Backend Rule With Source Tests

**Files:**
- Create: `Tools/Tests/Enlisted.UnitTests/EnlistedNativeMenuShellDesignTests.cs`

- [ ] **Step 1: Write the failing test file**

Create `Tools/Tests/Enlisted.UnitTests/EnlistedNativeMenuShellDesignTests.cs`:

```csharp
using System;
using System.IO;
using Xunit;

namespace Enlisted.UnitTests;

public sealed class EnlistedNativeMenuShellDesignTests
{
    [Fact]
    public void ShellAdapterReadsNativeMenuAndInvokesNativeConsequences()
    {
        var adapter = ReadRepoFile("src", "Features", "Interface", "Shell", "EnlistedNativeMenuAdapter.cs");

        Assert.Contains("Campaign.Current.CurrentMenuContext", adapter, StringComparison.Ordinal);
        Assert.Contains("GameMenuManager.GetMenuText", adapter, StringComparison.Ordinal);
        Assert.Contains("GetVirtualMenuOptionAmount", adapter, StringComparison.Ordinal);
        Assert.Contains("GetVirtualMenuOptionText", adapter, StringComparison.Ordinal);
        Assert.Contains("GetVirtualMenuOptionTooltip", adapter, StringComparison.Ordinal);
        Assert.Contains("GetVirtualMenuOptionIsEnabled", adapter, StringComparison.Ordinal);
        Assert.Contains("GetVirtualGameMenuOption", adapter, StringComparison.Ordinal);
        Assert.Contains("InvokeConsequence", adapter, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellBehaviorOnlyRendersEnlistedMenusAndKeepsFallback()
    {
        var behavior = ReadRepoFile("src", "Features", "Interface", "Shell", "EnlistedNativeMenuShellBehavior.cs");

        Assert.Contains("IsSupportedEnlistedMenu", behavior, StringComparison.Ordinal);
        Assert.Contains("CloseShell", behavior, StringComparison.Ordinal);
        Assert.Contains("ScreenManager.TopScreen", behavior, StringComparison.Ordinal);
        Assert.Contains("ConversationManager", behavior, StringComparison.Ordinal);
        Assert.Contains("ModLogger.Caught(\"INTERFACE\"", behavior, StringComparison.Ordinal);
    }

    [Fact]
    public void PresentationCatalogIsDisplayOnlyAndUsesNativeOptionIds()
    {
        var catalog = ReadRepoFile("src", "Features", "Interface", "Shell", "EnlistedMenuPresentationCatalog.cs");

        Assert.Contains("enlisted_status", catalog, StringComparison.Ordinal);
        Assert.Contains("enlisted_camp_hub", catalog, StringComparison.Ordinal);
        Assert.Contains("enlisted_reports", catalog, StringComparison.Ordinal);
        Assert.Contains("enlisted_service_stance", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("GiveGoldAction", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("ModifyReputation", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("QualityStore.Instance?.Set", catalog, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, Path.Combine(relativeParts));
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(relativeParts));
    }
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
dotnet test Tools\Tests\Enlisted.UnitTests\Enlisted.UnitTests.csproj --no-restore --filter EnlistedNativeMenuShellDesignTests
```

Expected: FAIL because `EnlistedNativeMenuAdapter.cs`, `EnlistedNativeMenuShellBehavior.cs`, and `EnlistedMenuPresentationCatalog.cs` do not exist.

- [ ] **Step 3: Commit the failing tests**

Run:

```powershell
git add Tools\Tests\Enlisted.UnitTests\EnlistedNativeMenuShellDesignTests.cs
git commit -m "test: lock native menu shell contract"
```

---

### Task 2: Add Native Menu Snapshot and Adapter

**Files:**
- Create: `src/Features/Interface/Shell/EnlistedNativeMenuSnapshot.cs`
- Create: `src/Features/Interface/Shell/EnlistedNativeMenuAdapter.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Add snapshot models**

Create `src/Features/Interface/Shell/EnlistedNativeMenuSnapshot.cs`:

```csharp
using System.Collections.Generic;

namespace Enlisted.Features.Interface.Shell
{
    public sealed class EnlistedNativeMenuSnapshot
    {
        public EnlistedNativeMenuSnapshot(string menuId, string titleText, string contextText, IReadOnlyList<EnlistedNativeMenuOptionSnapshot> options)
        {
            MenuId = menuId ?? string.Empty;
            TitleText = titleText ?? string.Empty;
            ContextText = contextText ?? string.Empty;
            Options = options ?? new List<EnlistedNativeMenuOptionSnapshot>();
        }

        public string MenuId { get; }

        public string TitleText { get; }

        public string ContextText { get; }

        public IReadOnlyList<EnlistedNativeMenuOptionSnapshot> Options { get; }
    }

    public sealed class EnlistedNativeMenuOptionSnapshot
    {
        public EnlistedNativeMenuOptionSnapshot(
            int index,
            string optionId,
            string text,
            string tooltip,
            string leaveType,
            bool isEnabled)
        {
            Index = index;
            OptionId = optionId ?? string.Empty;
            Text = text ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
            LeaveType = leaveType ?? string.Empty;
            IsEnabled = isEnabled;
        }

        public int Index { get; }

        public string OptionId { get; }

        public string Text { get; }

        public string Tooltip { get; }

        public string LeaveType { get; }

        public bool IsEnabled { get; }
    }
}
```

- [ ] **Step 2: Add native adapter**

Create `src/Features/Interface/Shell/EnlistedNativeMenuAdapter.cs`:

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace Enlisted.Features.Interface.Shell
{
    public sealed class EnlistedNativeMenuAdapter
    {
        public EnlistedNativeMenuSnapshot TryCreateSnapshot()
        {
            var context = Campaign.Current?.CurrentMenuContext;
            var manager = Campaign.Current?.GameMenuManager;
            var menu = context?.GameMenu;
            if (context == null || manager == null || menu == null)
            {
                return null;
            }

            if (!EnlistedMenuPresentationCatalog.IsSupportedEnlistedMenu(menu.StringId))
            {
                return null;
            }

            try
            {
                var options = new List<EnlistedNativeMenuOptionSnapshot>();
                var amount = manager.GetVirtualMenuOptionAmount(context);
                for (var i = 0; i < amount; i++)
                {
                    manager.SetCurrentRepeatableIndex(context, i);
                    if (!manager.GetVirtualMenuOptionConditionsHold(context, i))
                    {
                        continue;
                    }

                    var option = manager.GetVirtualGameMenuOption(context, i);
                    var text = manager.GetVirtualMenuOptionText(context, i)?.ToString() ?? string.Empty;
                    var tooltip = manager.GetVirtualMenuOptionTooltip(context, i)?.ToString() ?? string.Empty;
                    var enabled = manager.GetVirtualMenuOptionIsEnabled(context, i);
                    options.Add(new EnlistedNativeMenuOptionSnapshot(
                        i,
                        option?.IdString ?? string.Empty,
                        text,
                        tooltip,
                        option?.OptionLeaveType.ToString() ?? GameMenuOption.LeaveType.Default.ToString(),
                        enabled));
                }

                return new EnlistedNativeMenuSnapshot(
                    menu.StringId,
                    menu.MenuTitle?.ToString() ?? string.Empty,
                    manager.GetMenuText(context)?.ToString() ?? string.Empty,
                    options);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to snapshot native Enlisted menu", ex);
                return null;
            }
        }

        public bool InvokeOption(int optionIndex)
        {
            var context = Campaign.Current?.CurrentMenuContext;
            var menu = context?.GameMenu;
            if (context == null || menu == null)
            {
                return false;
            }

            if (!EnlistedMenuPresentationCatalog.IsSupportedEnlistedMenu(menu.StringId))
            {
                return false;
            }

            try
            {
                context.InvokeConsequence(optionIndex);
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to invoke native Enlisted menu option", ex);
                return false;
            }
        }
    }
}
```

- [ ] **Step 3: Register files in project**

Modify `Enlisted.csproj` in the source file item group near the Interface entries:

```xml
    <Compile Include="src\Features\Interface\Shell\EnlistedNativeMenuSnapshot.cs"/>
    <Compile Include="src\Features\Interface\Shell\EnlistedNativeMenuAdapter.cs"/>
```

- [ ] **Step 4: Run focused tests**

Run:

```powershell
dotnet test Tools\Tests\Enlisted.UnitTests\Enlisted.UnitTests.csproj --no-restore --filter EnlistedNativeMenuShellDesignTests
```

Expected: still FAIL because behavior and catalog are not implemented yet.

- [ ] **Step 5: Commit**

Run:

```powershell
git add Enlisted.csproj src\Features\Interface\Shell\EnlistedNativeMenuSnapshot.cs src\Features\Interface\Shell\EnlistedNativeMenuAdapter.cs
git commit -m "feat: add native menu snapshot adapter"
```

---

### Task 3: Add Presentation Catalog

**Files:**
- Create: `src/Features/Interface/Shell/EnlistedMenuPresentationCatalog.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Add catalog**

Create `src/Features/Interface/Shell/EnlistedMenuPresentationCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Enlisted.Features.Interface.Shell
{
    public sealed class EnlistedMenuPresentation
    {
        public EnlistedMenuPresentation(string menuId, string title, IReadOnlyList<EnlistedMenuPresentationSection> sections)
        {
            MenuId = menuId ?? string.Empty;
            Title = title ?? string.Empty;
            Sections = sections ?? new List<EnlistedMenuPresentationSection>();
        }

        public string MenuId { get; }

        public string Title { get; }

        public IReadOnlyList<EnlistedMenuPresentationSection> Sections { get; }
    }

    public sealed class EnlistedMenuPresentationSection
    {
        public EnlistedMenuPresentationSection(string id, string title, string style, IReadOnlyList<string> optionIds)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Style = style ?? "card";
            OptionIds = optionIds ?? new List<string>();
        }

        public string Id { get; }

        public string Title { get; }

        public string Style { get; }

        public IReadOnlyList<string> OptionIds { get; }
    }

    public static class EnlistedMenuPresentationCatalog
    {
        private static readonly IReadOnlyDictionary<string, EnlistedMenuPresentation> Presentations =
            new Dictionary<string, EnlistedMenuPresentation>(StringComparer.OrdinalIgnoreCase)
            {
                ["enlisted_status"] = new EnlistedMenuPresentation(
                    "enlisted_status",
                    "Enlisted Service",
                    new List<EnlistedMenuPresentationSection>
                    {
                        new EnlistedMenuPresentationSection("duty", "Current Duty", "summary", new[]
                        {
                            "enlisted_orders_header",
                            "enlisted_active_order"
                        }),
                        new EnlistedMenuPresentationSection("actions", "Actions", "button_grid", new[]
                        {
                            "enlisted_service_stance",
                            "enlisted_camp_hub",
                            "enlisted_reports",
                            "enlisted_visit_settlement"
                        })
                    }),
                ["enlisted_camp_hub"] = new EnlistedMenuPresentation(
                    "enlisted_camp_hub",
                    "Camp",
                    new List<EnlistedMenuPresentationSection>
                    {
                        new EnlistedMenuPresentationSection("camp_actions", "Available", "button_grid", new[]
                        {
                            "camp_hub_activities",
                            "camp_hub_change_stance",
                            "camp_hub_quartermaster",
                            "camp_hub_lord"
                        }),
                        new EnlistedMenuPresentationSection("camp_management", "Records", "button_grid", new[]
                        {
                            "camp_hub_service_records",
                            "camp_hub_companions",
                            "camp_hub_retinue"
                        })
                    }),
                ["enlisted_reports"] = new EnlistedMenuPresentation(
                    "enlisted_reports",
                    "Reports",
                    new List<EnlistedMenuPresentationSection>
                    {
                        new EnlistedMenuPresentationSection("reports", "Dispatches", "scroll", new[]
                        {
                            "reports_back"
                        })
                    }),
                ["enlisted_service_stance"] = new EnlistedMenuPresentation(
                    "enlisted_service_stance",
                    "Service Stance",
                    new List<EnlistedMenuPresentationSection>
                    {
                        new EnlistedMenuPresentationSection("stances", "Posture", "button_grid", new[]
                        {
                            "service_stance_0",
                            "service_stance_1",
                            "service_stance_2",
                            "service_stance_3",
                            "service_stance_4",
                            "service_stance_5",
                            "service_stance_6"
                        })
                    })
            };

        public static bool IsSupportedEnlistedMenu(string menuId)
        {
            return !string.IsNullOrWhiteSpace(menuId) && Presentations.ContainsKey(menuId);
        }

        public static EnlistedMenuPresentation Get(string menuId)
        {
            return !string.IsNullOrWhiteSpace(menuId) && Presentations.TryGetValue(menuId, out var presentation)
                ? presentation
                : null;
        }

        public static IEnumerable<EnlistedNativeMenuOptionSnapshot> GetOptionsForSection(
            EnlistedNativeMenuSnapshot snapshot,
            EnlistedMenuPresentationSection section)
        {
            if (snapshot == null || section == null)
            {
                return Enumerable.Empty<EnlistedNativeMenuOptionSnapshot>();
            }

            var byId = snapshot.Options.ToDictionary(o => o.OptionId, StringComparer.OrdinalIgnoreCase);
            return section.OptionIds
                .Where(byId.ContainsKey)
                .Select(id => byId[id]);
        }
    }
}
```

- [ ] **Step 2: Register catalog in project**

Modify `Enlisted.csproj` near the other Shell entries:

```xml
    <Compile Include="src\Features\Interface\Shell\EnlistedMenuPresentationCatalog.cs"/>
```

- [ ] **Step 3: Run focused tests**

Run:

```powershell
dotnet test Tools\Tests\Enlisted.UnitTests\Enlisted.UnitTests.csproj --no-restore --filter EnlistedNativeMenuShellDesignTests
```

Expected: still FAIL because shell behavior is not implemented yet.

- [ ] **Step 4: Commit**

Run:

```powershell
git add Enlisted.csproj src\Features\Interface\Shell\EnlistedMenuPresentationCatalog.cs
git commit -m "feat: add enlisted menu presentation catalog"
```

---

### Task 4: Add Shell ViewModels

**Files:**
- Create: `src/Features/Interface/ViewModels/EnlistedNativeMenuOptionVM.cs`
- Create: `src/Features/Interface/ViewModels/EnlistedNativeMenuSectionVM.cs`
- Create: `src/Features/Interface/ViewModels/EnlistedNativeMenuShellVM.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Add option VM**

Create `src/Features/Interface/ViewModels/EnlistedNativeMenuOptionVM.cs`:

```csharp
using System;
using Enlisted.Features.Interface.Shell;
using TaleWorlds.Library;

namespace Enlisted.Features.Interface.ViewModels
{
    public sealed class EnlistedNativeMenuOptionVM : ViewModel
    {
        private readonly Action<int> _invoke;
        private string _text;
        private string _tooltip;
        private string _leaveType;
        private bool _isEnabled;

        public EnlistedNativeMenuOptionVM(EnlistedNativeMenuOptionSnapshot option, Action<int> invoke)
        {
            Index = option?.Index ?? -1;
            OptionId = option?.OptionId ?? string.Empty;
            _invoke = invoke;
            Text = option?.Text ?? string.Empty;
            Tooltip = option?.Tooltip ?? string.Empty;
            LeaveType = option?.LeaveType ?? string.Empty;
            IsEnabled = option?.IsEnabled == true;
        }

        public int Index { get; }

        public string OptionId { get; }

        [DataSourceProperty]
        public string Text
        {
            get => _text;
            set
            {
                if (value != _text)
                {
                    _text = value;
                    OnPropertyChangedWithValue(value, "Text");
                }
            }
        }

        [DataSourceProperty]
        public string Tooltip
        {
            get => _tooltip;
            set
            {
                if (value != _tooltip)
                {
                    _tooltip = value;
                    OnPropertyChangedWithValue(value, "Tooltip");
                }
            }
        }

        [DataSourceProperty]
        public string LeaveType
        {
            get => _leaveType;
            set
            {
                if (value != _leaveType)
                {
                    _leaveType = value;
                    OnPropertyChangedWithValue(value, "LeaveType");
                }
            }
        }

        [DataSourceProperty]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    OnPropertyChangedWithValue(value, "IsEnabled");
                }
            }
        }

        public void ExecuteAction()
        {
            if (IsEnabled && Index >= 0)
            {
                _invoke?.Invoke(Index);
            }
        }
    }
}
```

- [ ] **Step 2: Add section VM**

Create `src/Features/Interface/ViewModels/EnlistedNativeMenuSectionVM.cs`:

```csharp
using System;
using System.Collections.Generic;
using Enlisted.Features.Interface.Shell;
using TaleWorlds.Library;

namespace Enlisted.Features.Interface.ViewModels
{
    public sealed class EnlistedNativeMenuSectionVM : ViewModel
    {
        private string _title;
        private string _style;

        public EnlistedNativeMenuSectionVM(
            EnlistedMenuPresentationSection section,
            IEnumerable<EnlistedNativeMenuOptionSnapshot> options,
            Action<int> invoke)
        {
            Id = section?.Id ?? string.Empty;
            Title = section?.Title ?? string.Empty;
            Style = section?.Style ?? "card";
            Options = new MBBindingList<EnlistedNativeMenuOptionVM>();

            if (options == null)
            {
                return;
            }

            foreach (var option in options)
            {
                Options.Add(new EnlistedNativeMenuOptionVM(option, invoke));
            }
        }

        public string Id { get; }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value != _title)
                {
                    _title = value;
                    OnPropertyChangedWithValue(value, "Title");
                }
            }
        }

        [DataSourceProperty]
        public string Style
        {
            get => _style;
            set
            {
                if (value != _style)
                {
                    _style = value;
                    OnPropertyChangedWithValue(value, "Style");
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<EnlistedNativeMenuOptionVM> Options { get; }
    }
}
```

- [ ] **Step 3: Add shell VM**

Create `src/Features/Interface/ViewModels/EnlistedNativeMenuShellVM.cs`:

```csharp
using System;
using Enlisted.Features.Interface.Shell;
using TaleWorlds.Library;

namespace Enlisted.Features.Interface.ViewModels
{
    public sealed class EnlistedNativeMenuShellVM : ViewModel
    {
        private readonly Action<int> _invoke;
        private bool _isVisible;
        private bool _isExpanded = true;
        private string _title;
        private string _contextText;
        private string _collapsedSummary;

        public EnlistedNativeMenuShellVM(Action<int> invoke)
        {
            _invoke = invoke;
            Sections = new MBBindingList<EnlistedNativeMenuSectionVM>();
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (value != _isVisible)
                {
                    _isVisible = value;
                    OnPropertyChangedWithValue(value, "IsVisible");
                }
            }
        }

        [DataSourceProperty]
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (value != _isExpanded)
                {
                    _isExpanded = value;
                    OnPropertyChangedWithValue(value, "IsExpanded");
                    OnPropertyChangedWithValue(!value, "IsCollapsed");
                }
            }
        }

        [DataSourceProperty]
        public bool IsCollapsed => !IsExpanded;

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value != _title)
                {
                    _title = value;
                    OnPropertyChangedWithValue(value, "Title");
                }
            }
        }

        [DataSourceProperty]
        public string ContextText
        {
            get => _contextText;
            set
            {
                if (value != _contextText)
                {
                    _contextText = value;
                    OnPropertyChangedWithValue(value, "ContextText");
                }
            }
        }

        [DataSourceProperty]
        public string CollapsedSummary
        {
            get => _collapsedSummary;
            set
            {
                if (value != _collapsedSummary)
                {
                    _collapsedSummary = value;
                    OnPropertyChangedWithValue(value, "CollapsedSummary");
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<EnlistedNativeMenuSectionVM> Sections { get; }

        public void RefreshFrom(EnlistedNativeMenuSnapshot snapshot)
        {
            if (snapshot == null)
            {
                IsVisible = false;
                Sections.Clear();
                return;
            }

            var presentation = EnlistedMenuPresentationCatalog.Get(snapshot.MenuId);
            Title = presentation?.Title ?? snapshot.TitleText;
            ContextText = snapshot.ContextText;
            CollapsedSummary = string.IsNullOrWhiteSpace(snapshot.TitleText) ? Title : snapshot.TitleText;
            Sections.Clear();

            if (presentation != null)
            {
                foreach (var section in presentation.Sections)
                {
                    var options = EnlistedMenuPresentationCatalog.GetOptionsForSection(snapshot, section);
                    Sections.Add(new EnlistedNativeMenuSectionVM(section, options, _invoke));
                }
            }

            IsVisible = true;
        }

        public void ExecuteToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
```

- [ ] **Step 4: Register VMs in project**

Modify `Enlisted.csproj` near existing Interface ViewModels:

```xml
    <Compile Include="src\Features\Interface\ViewModels\EnlistedNativeMenuShellVM.cs"/>
    <Compile Include="src\Features\Interface\ViewModels\EnlistedNativeMenuSectionVM.cs"/>
    <Compile Include="src\Features\Interface\ViewModels\EnlistedNativeMenuOptionVM.cs"/>
```

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test Tools\Tests\Enlisted.UnitTests\Enlisted.UnitTests.csproj --no-restore --filter EnlistedNativeMenuShellDesignTests
```

Expected: still FAIL until shell behavior exists.

- [ ] **Step 6: Commit**

Run:

```powershell
git add Enlisted.csproj src\Features\Interface\ViewModels\EnlistedNativeMenuShellVM.cs src\Features\Interface\ViewModels\EnlistedNativeMenuSectionVM.cs src\Features\Interface\ViewModels\EnlistedNativeMenuOptionVM.cs
git commit -m "feat: add native menu shell view models"
```

---

### Task 5: Add Shell Behavior and Lifecycle Gates

**Files:**
- Create: `src/Features/Interface/Shell/EnlistedNativeMenuShellBehavior.cs`
- Modify: `src/Mod.Entry/SubModule.cs`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Add shell behavior**

Create `src/Features/Interface/Shell/EnlistedNativeMenuShellBehavior.cs`:

```csharp
using System;
using Enlisted.Features.Interface.ViewModels;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace Enlisted.Features.Interface.Shell
{
    public sealed class EnlistedNativeMenuShellBehavior : CampaignBehaviorBase
    {
        public static EnlistedNativeMenuShellBehavior Instance { get; private set; }

        private readonly EnlistedNativeMenuAdapter _adapter = new EnlistedNativeMenuAdapter();
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private EnlistedNativeMenuShellVM _viewModel;
        private ScreenBase _ownerScreen;
        private bool _initialized;

        public EnlistedNativeMenuShellBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _initialized = false);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.ConversationStarted.AddNonSerializedListener(this, _ => CloseShell());
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, _ => RefreshShell());
            CampaignEvents.OnGameOverEvent.AddNonSerializedListener(this, CloseShell);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                CloseShell();
            }
        }

        private void OnTick(float dt)
        {
            if (!_initialized)
            {
                InitializeShell();
                _initialized = true;
            }

            RefreshShell();
        }

        private void InitializeShell()
        {
            try
            {
                if (_layer != null)
                {
                    return;
                }

                var topScreen = ScreenManager.TopScreen;
                if (topScreen == null)
                {
                    return;
                }

                _layer = new GauntletLayer("EnlistedNativeMenuShell", 1002);
                _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _layer.InputRestrictions.SetInputRestrictions(false);
                _viewModel = new EnlistedNativeMenuShellVM(InvokeNativeOption);
                _movie = _layer.LoadMovie("EnlistedNativeMenuShell", _viewModel);
                topScreen.AddLayer(_layer);
                _ownerScreen = topScreen;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to initialize Enlisted native menu shell", ex);
                CloseShell();
            }
        }

        private void RefreshShell()
        {
            try
            {
                if (ShouldYieldToNative())
                {
                    if (_viewModel != null)
                    {
                        _viewModel.IsVisible = false;
                    }

                    return;
                }

                if (_layer == null)
                {
                    InitializeShell();
                }

                var snapshot = _adapter.TryCreateSnapshot();
                if (snapshot == null)
                {
                    if (_viewModel != null)
                    {
                        _viewModel.IsVisible = false;
                    }

                    return;
                }

                _viewModel?.RefreshFrom(snapshot);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to refresh Enlisted native menu shell", ex);
                if (_viewModel != null)
                {
                    _viewModel.IsVisible = false;
                }
            }
        }

        private static bool ShouldYieldToNative()
        {
            if (Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
            {
                return true;
            }

            var menuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
            return !IsSupportedEnlistedMenu(menuId);
        }

        private static bool IsSupportedEnlistedMenu(string menuId)
        {
            return EnlistedMenuPresentationCatalog.IsSupportedEnlistedMenu(menuId);
        }

        private void InvokeNativeOption(int optionIndex)
        {
            if (!_adapter.InvokeOption(optionIndex))
            {
                ModLogger.Expected("INTERFACE", "shell_option_invoke_rejected", "Native shell option invocation was rejected");
            }
        }

        private void CloseShell()
        {
            try
            {
                if (_layer != null)
                {
                    _layer.InputRestrictions.ResetInputRestrictions();
                    if (_movie != null)
                    {
                        _layer.ReleaseMovie(_movie);
                    }

                    _ownerScreen?.RemoveLayer(_layer);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTERFACE", "Failed to close Enlisted native menu shell", ex);
            }
            finally
            {
                _movie = null;
                _layer = null;
                _viewModel = null;
                _ownerScreen = null;
            }
        }
    }
}
```

- [ ] **Step 2: Register behavior in SubModule**

Modify `src/Mod.Entry/SubModule.cs` in the behavior registration block near other interface behaviors:

```csharp
campaignStarter.AddBehavior(new Features.Interface.Shell.EnlistedNativeMenuShellBehavior());
```

- [ ] **Step 3: Register behavior in project**

Modify `Enlisted.csproj` near other Interface compile entries:

```xml
    <Compile Include="src\Features\Interface\Shell\EnlistedNativeMenuShellBehavior.cs"/>
```

- [ ] **Step 4: Run focused tests**

Run:

```powershell
dotnet test Tools\Tests\Enlisted.UnitTests\Enlisted.UnitTests.csproj --no-restore --filter EnlistedNativeMenuShellDesignTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```powershell
git add Enlisted.csproj src\Mod.Entry\SubModule.cs src\Features\Interface\Shell\EnlistedNativeMenuShellBehavior.cs
git commit -m "feat: add native menu shell lifecycle"
```

---

### Task 6: Add Gauntlet Prefab

**Files:**
- Create: `GUI/Prefabs/Interface/EnlistedNativeMenuShell.xml`
- Modify: `Enlisted.csproj`

- [ ] **Step 1: Add conservative first prefab**

Create `GUI/Prefabs/Interface/EnlistedNativeMenuShell.xml`:

```xml
<Prefab>
  <Window>
    <Widget Id="EnlistedNativeMenuShell"
            DoNotAcceptEvents="false"
            WidthSizePolicy="Fixed"
            HeightSizePolicy="Fixed"
            SuggestedWidth="620"
            SuggestedHeight="520"
            HorizontalAlignment="Left"
            VerticalAlignment="Top"
            MarginLeft="28"
            MarginTop="86"
            IsVisible="@IsVisible">
      <Children>
        <Widget WidthSizePolicy="StretchToParent"
                HeightSizePolicy="StretchToParent"
                Sprite="BlankWhiteSquare_9"
                Color="#08111BDD"
                DoNotAcceptEvents="true" />

        <Widget WidthSizePolicy="StretchToParent"
                HeightSizePolicy="StretchToParent"
                Sprite="BlankWhiteSquare_9"
                Color="#C89C4A22"
                MarginLeft="1"
                MarginRight="1"
                MarginTop="1"
                MarginBottom="1"
                DoNotAcceptEvents="true" />

        <ButtonWidget Id="CollapsedButton"
                      WidthSizePolicy="Fixed"
                      HeightSizePolicy="Fixed"
                      SuggestedWidth="240"
                      SuggestedHeight="42"
                      IsVisible="@IsCollapsed"
                      Brush="ButtonBrush1"
                      Command.Click="ExecuteToggleExpanded">
          <Children>
            <RichTextWidget WidthSizePolicy="CoverChildren"
                            HeightSizePolicy="CoverChildren"
                            HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            Brush="SPGeneral.MediumText"
                            Brush.FontSize="16"
                            Text="@CollapsedSummary" />
          </Children>
        </ButtonWidget>

        <Widget Id="ExpandedPanel"
                WidthSizePolicy="StretchToParent"
                HeightSizePolicy="StretchToParent"
                IsVisible="@IsExpanded"
                DoNotAcceptEvents="false">
          <Children>
            <Widget Id="HeaderBar"
                    WidthSizePolicy="StretchToParent"
                    HeightSizePolicy="Fixed"
                    SuggestedHeight="44"
                    MarginLeft="10"
                    MarginRight="10"
                    MarginTop="8">
              <Children>
                <RichTextWidget WidthSizePolicy="CoverChildren"
                                HeightSizePolicy="CoverChildren"
                                HorizontalAlignment="Left"
                                VerticalAlignment="Center"
                                MarginLeft="10"
                                Brush="SPGeneral.MediumText"
                                Brush.FontSize="20"
                                Text="@Title" />

                <ButtonWidget WidthSizePolicy="Fixed"
                              HeightSizePolicy="Fixed"
                              SuggestedWidth="40"
                              SuggestedHeight="30"
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
                                    Brush.FontSize="16"
                                    Text="&lt;" />
                  </Children>
                </ButtonWidget>
              </Children>
            </Widget>

            <ScrollablePanel Id="ShellScroll"
                             WidthSizePolicy="StretchToParent"
                             HeightSizePolicy="StretchToParent"
                             MarginLeft="12"
                             MarginRight="12"
                             MarginTop="58"
                             MarginBottom="12"
                             ClipRect="ClipRect"
                             InnerPanel="ClipRect\InnerPanel"
                             AutoHideScrollBars="true">
              <Children>
                <Widget Id="ClipRect"
                        WidthSizePolicy="StretchToParent"
                        HeightSizePolicy="StretchToParent"
                        ClipContents="true">
                  <Children>
                    <ListPanel Id="InnerPanel"
                               WidthSizePolicy="StretchToParent"
                               HeightSizePolicy="CoverChildren"
                               StackLayout.LayoutMethod="VerticalBottomToTop">
                      <Children>
                        <RichTextWidget WidthSizePolicy="StretchToParent"
                                        HeightSizePolicy="CoverChildren"
                                        MarginBottom="12"
                                        Brush="SPGeneral.SmallText"
                                        Brush.FontSize="16"
                                        Text="@ContextText" />

                        <ListPanel DataSource="{Sections}"
                                   WidthSizePolicy="StretchToParent"
                                   HeightSizePolicy="CoverChildren"
                                   StackLayout.LayoutMethod="VerticalBottomToTop">
                          <ItemTemplate>
                            <Widget WidthSizePolicy="StretchToParent"
                                    HeightSizePolicy="CoverChildren"
                                    MarginBottom="10">
                              <Children>
                                <Widget WidthSizePolicy="StretchToParent"
                                        HeightSizePolicy="CoverChildren"
                                        Sprite="BlankWhiteSquare_9"
                                        Color="#102235AA"
                                        DoNotAcceptEvents="true" />

                                <ListPanel WidthSizePolicy="StretchToParent"
                                           HeightSizePolicy="CoverChildren"
                                           MarginLeft="10"
                                           MarginRight="10"
                                           MarginTop="8"
                                           MarginBottom="8"
                                           StackLayout.LayoutMethod="VerticalBottomToTop">
                                  <Children>
                                    <RichTextWidget WidthSizePolicy="CoverChildren"
                                                    HeightSizePolicy="CoverChildren"
                                                    Brush="SPGeneral.MediumText"
                                                    Brush.FontSize="16"
                                                    Text="@Title" />

                                    <ListPanel DataSource="{Options}"
                                               WidthSizePolicy="StretchToParent"
                                               HeightSizePolicy="CoverChildren"
                                               StackLayout.LayoutMethod="VerticalBottomToTop">
                                      <ItemTemplate>
                                        <ButtonWidget WidthSizePolicy="StretchToParent"
                                                      HeightSizePolicy="Fixed"
                                                      SuggestedHeight="34"
                                                      MarginTop="4"
                                                      Brush="ButtonBrush1"
                                                      IsEnabled="@IsEnabled"
                                                      Command.Click="ExecuteAction">
                                          <Children>
                                            <RichTextWidget WidthSizePolicy="CoverChildren"
                                                            HeightSizePolicy="CoverChildren"
                                                            HorizontalAlignment="Left"
                                                            VerticalAlignment="Center"
                                                            MarginLeft="8"
                                                            Brush="SPGeneral.MediumText"
                                                            Brush.FontSize="15"
                                                            Text="@Text" />
                                          </Children>
                                        </ButtonWidget>
                                      </ItemTemplate>
                                    </ListPanel>
                                  </Children>
                                </ListPanel>
                              </Children>
                            </Widget>
                          </ItemTemplate>
                        </ListPanel>
                      </Children>
                    </ListPanel>
                  </Children>
                </Widget>
              </Children>
            </ScrollablePanel>
          </Children>
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>
```

- [ ] **Step 2: Register prefab in project content**

Modify `Enlisted.csproj` near the combat log interface prefab:

```xml
    <Content Include="GUI\Prefabs\Interface\EnlistedNativeMenuShell.xml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
```

Modify the post-build copy block near `EnlistedCombatLog.xml`:

```xml
    <Copy SourceFiles="GUI\Prefabs\Interface\EnlistedNativeMenuShell.xml" DestinationFolder="$(OutputPath)..\..\GUI\Prefabs\Interface\"/>
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: build succeeds and copies `GUI\Prefabs\Interface\EnlistedNativeMenuShell.xml` into the deployed module.

- [ ] **Step 4: Commit**

Run:

```powershell
git add Enlisted.csproj GUI\Prefabs\Interface\EnlistedNativeMenuShell.xml
git commit -m "feat: add enlisted native menu shell prefab"
```

---

### Task 7: Add Docs and Final Verification

**Files:**
- Modify: `docs/Features/UI/news-reporting-system.md`
- Modify: `docs/INDEX.md`

- [ ] **Step 1: Update UI docs**

Add this section to `docs/Features/UI/news-reporting-system.md` after the Overview:

```markdown
## Enlisted Native Menu Shell

The Enlisted Native Menu Shell is a custom Gauntlet renderer for Enlisted-owned native GameMenus. It does not own gameplay state. Native GameMenu remains authoritative for menu state, time-control behavior, option availability, tooltips, and option consequences.

The shell reads the active native menu, applies display-only presentation metadata, and renders cards/sections/collapsible UI. Clicking a shell action invokes the live native GameMenu option by index. When vanilla menus or non-Enlisted screens take over, the shell hides and leaves the stock native flow alone.
```

- [ ] **Step 2: Update docs index**

Modify the `news-reporting-system.md` row in `docs/INDEX.md` so the description includes `custom native-menu shell renderer`.

- [ ] **Step 3: Run full tests**

Run:

```powershell
dotnet test Tools\Tests\Enlisted.UnitTests\Enlisted.UnitTests.csproj --no-restore
```

Expected: all unit tests pass.

- [ ] **Step 4: Run content validator**

Run:

```powershell
python Tools\Validation\validate_content.py
```

Expected: validation has 0 errors. Existing warnings may remain if unrelated.

- [ ] **Step 5: Run build**

Run:

```powershell
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Expected: build succeeds with 0 compiler errors and copies DLL plus shell prefab.

- [ ] **Step 6: Run diff whitespace check**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors. CRLF notices are acceptable in this repo.

- [ ] **Step 7: Commit docs**

Run:

```powershell
git add docs\Features\UI\news-reporting-system.md docs\INDEX.md
git commit -m "docs: document native menu shell renderer"
```

---

## Manual In-Game Verification

- [ ] Launch Bannerlord with Enlisted enabled.
- [ ] Enlist with a lord.
- [ ] Confirm stock native `enlisted_status` remains reachable if shell is hidden.
- [ ] Confirm shell appears only while an Enlisted-owned native menu is active.
- [ ] Confirm collapse/expand works.
- [ ] Confirm shell buttons invoke native actions: Camp, Reports, Service Stance, Orders if available.
- [ ] Enter a town/castle/village through native interaction. Confirm shell hides.
- [ ] Trigger or enter a battle/siege menu. Confirm shell hides and native menu owns the flow.
- [ ] Return to enlisted idle/status state. Confirm shell can reappear without duplicating layers.
- [ ] Open Quartermaster equipment/provisions. Confirm shell does not fight Quartermaster custom screens.

---

## Self-Review

Spec coverage:
- Native GameMenu remains authoritative: Tasks 2, 5.
- Custom renderer owns presentation: Tasks 3, 4, 6.
- Hide for non-Enlisted/native takeover: Task 5.
- Invoke native consequences only: Tasks 1, 2, 4.
- Fallback to stock native menu on failure: Task 5.
- Collapse/expand: Tasks 4, 6.
- Project registration and prefab copy: Tasks 2, 3, 4, 5, 6.
- Docs and verification: Task 7.

Placeholder scan:
- No intentionally blank implementation steps.
- No gameplay behavior is delegated to presentation metadata.
- Manual in-game verification is listed separately because it requires launching Bannerlord.

Type consistency:
- `EnlistedNativeMenuSnapshot` and `EnlistedNativeMenuOptionSnapshot` are created before adapter and ViewModels consume them.
- `EnlistedMenuPresentationCatalog.IsSupportedEnlistedMenu` is used by both adapter and behavior.
- `EnlistedNativeMenuShellVM.RefreshFrom(...)` consumes the same snapshot type produced by the adapter.
