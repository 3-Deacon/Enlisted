# Enlisted Native Menu Shell Design

**Status:** Approved direction, 2026-04-24.

**Goal:** Make Enlisted-owned native menus attractive and dynamic without replacing Bannerlord's native GameMenu state machine.

## Decision

Build a custom Gauntlet presentation layer that renders Enlisted-owned native GameMenus. The native GameMenu remains the behavior source: menu id, menu text, option availability, option tooltip, leave type, and option consequence invocation. The custom shell owns presentation only: sections, cards, tabs, collapse state, scrolling, badges, and visual grouping.

This is not a separate dashboard with separate gameplay logic. It is a better renderer for the active Enlisted native menu.

## Why Not Stock GameMenu Only

The decompile shows native GameMenu presentation is a single context text string plus a linear option list. `GameMenuVM` reads `ContextText` from `GameMenuManager.GetMenuText(...)` and options from `GetVirtualMenuOption...` methods. `GameMenuItemVM` exposes option text, enabled state, tooltip, and leave type. There is no native section metadata, cards, tabs, collapsible groups, or rich layout rules.

Trying to make a dashboard inside the stock text box produces dense, ugly pages. The fix is not more formatted text. The fix is a custom renderer that still calls the native menu backend.

## Non-Negotiable Safety Rules

- Render only Enlisted-owned menus, initially `enlisted_status`, `enlisted_camp_hub`, `enlisted_reports`, `enlisted_service_stance`, and order/camp detail menus.
- Hide or suspend immediately when the current native menu is not an Enlisted menu.
- Hide or suspend during conversation, settlement, siege, battle, inventory, party, barter, quartermaster equipment/provision screens, or any other non-Enlisted screen focus.
- Never duplicate gameplay consequences. Clicks invoke `MenuContext.InvokeConsequence(optionIndex)` on the live native menu.
- If a native option is absent, hidden, or disabled, the custom shell mirrors that state.
- If the shell fails to open or refresh, the stock native GameMenu remains usable.
- Presentation metadata is display-only. Native menu state is always authoritative.

## Target UX

Collapsed shell:

```text
ENLISTED | On Duty | No active orders
```

Expanded shell:

```text
Enlisted Service                                      [collapse]

Status | Camp | Reports | Stance

Current Duty                  Company
No active orders              72 soldiers, supplies tight
Stance: Drill with the Line   6 wounded

Signals
No urgent signals.

Actions
[Orders] [Camp] [Reports] [Visit Sargot]
```

Reports must not become a wall of text. Reports in the custom shell should use tabs or filters with a scrollable body:

```text
Reports
[Muster] [Personal] [Company] [Kingdom]

scrollable report list...
```

## Data Flow

```text
Campaign.Current.CurrentMenuContext
  -> EnlistedNativeMenuAdapter snapshots live native menu
  -> EnlistedMenuPresentationCatalog maps menu ids/options to visual sections
  -> EnlistedNativeMenuShellVM builds cards/tabs/buttons
  -> EnlistedNativeMenuShell.xml renders the shell
  -> clicking an action calls MenuContext.InvokeConsequence(optionIndex)
```

## Files

- `src/Features/Interface/Shell/EnlistedNativeMenuShellBehavior.cs`: Gauntlet layer lifecycle and native override rules.
- `src/Features/Interface/Shell/EnlistedNativeMenuAdapter.cs`: live native menu snapshot reader and option invoker.
- `src/Features/Interface/Shell/EnlistedMenuPresentationCatalog.cs`: display-only grouping metadata for Enlisted menus.
- `src/Features/Interface/Shell/EnlistedNativeMenuSnapshot.cs`: immutable menu snapshot model.
- `src/Features/Interface/ViewModels/EnlistedNativeMenuShellVM.cs`: shell view model.
- `src/Features/Interface/ViewModels/EnlistedNativeMenuOptionVM.cs`: option button view model.
- `src/Features/Interface/ViewModels/EnlistedNativeMenuSectionVM.cs`: card/section view model.
- `GUI/Prefabs/Interface/EnlistedNativeMenuShell.xml`: custom shell prefab.
- `Tools/Tests/Enlisted.UnitTests/EnlistedNativeMenuShellDesignTests.cs`: source-level regression tests for routing, fallback, and presentation-only rules.

## Open Implementation Choice

The first build should render a conservative shell for the front `enlisted_status` page and leave deeper menus to stock native rendering until the lifecycle proves stable. Once verified in game, add Camp, Reports, Stance, and details as additional menu layouts.

This keeps risk low while proving the custom renderer can collapse, expand, refresh native options, invoke native consequences, and disappear when vanilla menus take over.
