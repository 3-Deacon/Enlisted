# Enlisted Combat Log

**Status:** Implemented  
**Category:** UI / Information Display  
**Related Docs:** [News Reporting System](news-reporting-system.md), [UI Systems Master](ui-systems-master.md), [Color Scheme](color-scheme.md)

---

## Overview

The Enlisted combat log is a custom Gauntlet widget that displays campaign-map `InformationManager` messages in a persistent Enlisted-specific feed while the player is enlisted.

It replaces the old bottom-right compact log layout with a **top-right live feed** that stays visible during campaign play, remains scrollable in compact mode, and supports a **manual expanded mode** with drag resizing and remembered expanded size.

The mod still leaves native message handling alone during missions. This surface is specifically for enlisted campaign-map play.

---

## User-Facing Behavior

### Active contexts

The combat log is visible only when all of these are true:

- the player is enlisted,
- the player is on the campaign map,
- the player is not currently in a mission scene,
- the layer is not suspended for a map conversation or encyclopedia screen.

### Compact mode

Compact mode is the default.

- anchored in the **top-right** corner,
- always visible while active,
- scrollable,
- uses a slightly larger default footprint than the original bottom-right widget,
- keeps the existing fading and message-aging behavior.

### Expanded mode

Expanded mode is entered manually through the header button.

- still anchored top-right,
- larger reading surface,
- user-resizable through the resize handle,
- remembers the last expanded width/height across sessions,
- collapsing returns the widget to the compact default size.

### Message behavior

- newest messages still accumulate in the same feed,
- manual scrolling pauses auto-scroll temporarily,
- inactivity still dims the surface,
- messages still age out after five minutes,
- encyclopedia links still work inside message text.
- personal progression/reward toasts are intentionally excluded. `InformationManagerDisplayMessagePatch` suppresses XP, trait XP, enlistment XP, and "Past few days" updates from known Enlisted systems; those belong to the news/feed surfaces.
- the combat log remains for combat/system information and immediate decision feedback, not player progression recap spam.

---

## Architecture

### Message routing

`InformationManagerDisplayMessagePatch` continues to intercept campaign-map messages and route them into the Enlisted combat log while the player is enlisted.

This redesign does **not** change the routing rule. It changes only how the custom log is presented and interacted with.

### ViewModel

`src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs`

Responsibilities:

- message collection,
- visibility state,
- inactivity fade,
- compact vs expanded UI state,
- current container size,
- resize-preview state,
- expanded-size application.

The ViewModel owns UI state, not drag input orchestration.

### Behavior

`src/Features/Interface/Behaviors/EnlistedCombatLogBehavior.cs`

Responsibilities:

- Gauntlet layer creation and cleanup,
- scroll tracking and auto-scroll pause/resume,
- message link wiring,
- conversation/encyclopedia suspension,
- resize-handle input tracking,
- loading and saving expanded size through the UI-state store.

The behavior now owns the drag-resize interaction loop, mirroring the native split where widget/view code handles resize interaction and the ViewModel owns state.

### UI state persistence

`src/Features/Interface/Utils/CombatLogUiStateStore.cs`

Expanded-size persistence is stored in an Enlisted-owned per-user JSON file under local app data. This is intentionally separate from Bannerlord's native chat-box settings and separate from campaign save data.

### Prefab

`GUI/Prefabs/Interface/EnlistedCombatLog.xml`

The prefab now provides:

- top-right anchoring,
- header row with manual expand toggle,
- scrollable message body,
- resize preview frame,
- resize handle visible only while expanded.

---

## Implementation Notes

### Native patterns borrowed

This redesign intentionally borrows the **interaction split** from Bannerlord's native chat log implementation:

- state in the ViewModel,
- resize interaction in widget/view-side code,
- manual inspect/expanded behavior,
- remembered size for the expanded presentation.

It does **not** import Bannerlord's multiplayer chat channels, typing state, or full native chat VM stack.

### Why top-right

The old bottom-right implementation had to fight for space with alert surfaces and bottom map UI. Moving the combat log to the top-right removes that collision pressure and avoids the old bottom-right repositioning logic.

---

## Verification Checklist

After changing this system, verify all of the following in-game:

- compact feed appears in the top-right corner,
- compact mode remains scrollable,
- expand/collapse button works,
- resize handle works while expanded,
- expanded size persists across restart,
- conversation suspension/resume still works,
- encyclopedia links still open correctly,
- bottom-right alerts no longer collide with the combat log.

---

## Key Files

```text
src/Features/Interface/Behaviors/EnlistedCombatLogBehavior.cs
src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs
src/Features/Interface/ViewModels/CombatLogMessageVM.cs
src/Features/Interface/Utils/CombatLogUiStateStore.cs
src/Features/Interface/Utils/FactionLinkColorizer.cs
src/Mod.GameAdapters/Patches/InformationManagerDisplayMessagePatch.cs
src/Mod.GameAdapters/Patches/CombatLogConversationPatch.cs
GUI/Prefabs/Interface/EnlistedCombatLog.xml
GUI/Brushes/EnlistedCombatLog.xml
```

---

## Current Limitations

- The surface is verified by build and code inspection here, but final feel still depends on live Bannerlord smoke testing.
- Resize behavior is intentionally scoped to expanded mode only.
- The compact feed does not auto-expand for high-severity messages.
