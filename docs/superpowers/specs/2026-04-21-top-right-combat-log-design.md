# Top-Right Combat Log Redesign - Design Spec

**Date:** 2026-04-21
**Status:** Draft written for user review. Awaiting review before implementation planning.
**Scope:** Redesign the custom Enlisted campaign-map combat log so it remains a visible live feed, moves from the crowded bottom-right corner to the top-right corner, and gains a manual native-style expanded/inspect mode with remembered expanded size.

Grounding sources for this design:

- Current Enlisted implementation:
  - `src/Features/Interface/Behaviors/EnlistedCombatLogBehavior.cs`
  - `src/Features/Interface/ViewModels/EnlistedCombatLogVM.cs`
  - `GUI/Prefabs/Interface/EnlistedCombatLog.xml`
  - `src/Mod.GameAdapters/Patches/InformationManagerDisplayMessagePatch.cs`
- Native Bannerlord decompile:
  - `../Decompile/TaleWorlds.MountAndBlade.GauntletUI/TaleWorlds.MountAndBlade.GauntletUI/GauntletChatLogView.cs`
  - `../Decompile/TaleWorlds.MountAndBlade.GauntletUI.Widgets/TaleWorlds.MountAndBlade.GauntletUI.Widgets.Chat/ChatLogWidget.cs`
  - `../Decompile/TaleWorlds.MountAndBlade.ViewModelCollection/TaleWorlds.MountAndBlade.ViewModelCollection.Multiplayer/MPChatVM.cs`
  - `../Decompile/TaleWorlds.MountAndBlade.View/TaleWorlds.MountAndBlade.View/IChatLogHandlerScreen.cs`

---

## 1. Problem

The current Enlisted combat log solved the "native messages vanish too quickly" problem, but it now has three layout failures:

1. **The bottom-right corner is over-subscribed.**
   The existing widget sits in the same corner as alert surfaces and already has custom repositioning logic to dodge party and army UI. Keeping the log there while making it larger or expandable compounds collision risk instead of reducing it.

2. **The current widget is always half-open and fixed-size.**
   It is scrollable, which is correct, but it has no true inspect mode, no resize affordance, and no persisted user preference for a larger reading surface.

3. **Its sizing model is too rigid for varied resolutions.**
   The current prefab is a fixed `400x280` box with hard-coded offsets. That is acceptable for a compact first pass, but it is not a robust design for a permanent always-visible feed.

The native Bannerlord chat/combat log pattern is useful here, but only in part. Native code proves the value of:

- a visible compact state,
- an explicit inspect/expanded state,
- user-driven resizing,
- persisted log size,
- widget-level resize behavior rather than ad hoc layer repositioning.

The native code is **not** a drop-in solution for Enlisted because it is tied to multiplayer chat channels, typing state, hotkeys, and `IChatLogHandlerScreen` input plumbing that our campaign-map feed does not need.

## 2. Goals

- Move the Enlisted combat log to the **top-right** corner of the campaign map.
- Keep it visible at all times as a **live feed** while enlisted on the campaign map.
- Preserve **scrollability in compact mode** so users can read both newer and older messages without expanding first.
- Add a **manual expanded/inspect mode** inspired by the native log.
- Allow **user resizing in expanded mode**.
- Persist the user's chosen **expanded size** across sessions.
- Remove the design dependence on bottom-right avoidance logic for alerts and other bottom-corner surfaces.

## 3. Non-goals

- No adoption of Bannerlord's full native `MPChatVM` stack.
- No multiplayer chat features: no channel switching, typing mode, bark/combat filtering, or chat hotkey handling.
- No auto-expansion on high-severity messages.
- No disappearance into a tiny header/tab when collapsed; the compact mode remains a visible feed.
- No mission-scene redesign. The current rule that this custom log is campaign-map-only remains unchanged unless a later spec explicitly changes it.

## 4. Current State Summary

Today's Enlisted log behaves as follows:

- `InformationManager.DisplayMessage(...)` is intercepted and rerouted into the custom log while the player is enlisted and not in a mission.
- The log is owned by `EnlistedCombatLogBehavior` as a `MapScreen` layer.
- The prefab is fixed-size and bottom-right anchored.
- The view model keeps up to 50 messages, ages them out after 5 minutes, and dims the container after inactivity.
- The behavior manually shifts the log upward to avoid bottom-right map UI and party/army overlays.

This design is functional but now constrained by its corner choice more than by message handling.

## 5. Native Reference Findings

The decompile confirms several reusable design patterns:

1. **Inspect state is explicit.**
   Native `MPChatVM` distinguishes between the passive state and `IsInspectingMessages`, and forces messages visible while inspecting.

2. **Resize belongs in the widget.**
   Native `ChatLogWidget` owns the resize handle, resize frame, animated lerp, and final size application.

3. **Persisted size is a first-class behavior.**
   Native code stores chat box dimensions and restores them instead of relying on a single fixed prefab size.

4. **Screen/input plumbing is native-chat-specific.**
   `GauntletChatLogView` and `IChatLogHandlerScreen` coordinate focus and typing. That part should not be imported into Enlisted's read-only feed.

This produces the right boundary for our redesign:

- **Borrow:** compact/expanded states, resize affordance, remembered size, widget-driven size changes.
- **Do not borrow:** chat typing, screen focus ownership, channel toggles, chat option toggles, multiplayer history model.

## 6. Approved Design

### 6.1 Layout

The combat log moves to the **top-right** corner of the campaign map and expands inward/downward from that corner.

Two presentation states exist:

- **Compact state**
  - Always visible while the log is active.
  - Slightly larger than the current `400x280` box.
  - Fully scrollable.
  - Intended to function as the default reading surface during normal play.

- **Expanded state**
  - Entered manually by the user.
  - Larger reading area for deeper review.
  - Still anchored to the top-right.
  - User-resizable.

The compact state is the default state on load. The expanded state is an optional reading mode, not a required state for basic usability.

### 6.2 Interaction Model

The feed remains visible at all times during its normal active contexts. It does **not** collapse into a minimal tab or button-only stub.

Expansion is **manual only**:

- no automatic expansion for severe messages,
- no pop-open behavior tied to new entries,
- no severity-driven interruption changes.

Compact mode remains scrollable. The redesign explicitly preserves the user's ability to browse up and down through the feed without first expanding it.

### 6.3 Resizing

Expanded mode supports native-style resizing through a dedicated widget affordance, modeled after the native `ChatLogWidget` pattern.

Resize rules:

- resizing is available only in expanded mode,
- width and height clamp to safe min/max bounds,
- the box must remain fully on-screen after resize,
- resize should animate or settle cleanly rather than snapping through invalid intermediate states.

Compact mode does **not** become arbitrarily user-sized. It keeps a tuned mod-defined default size so the always-visible feed remains predictable and readable.

### 6.4 Persistence

The only persisted size preference is the user's **expanded** size.

Persistence rules:

- compact mode uses a stable mod-defined default size,
- expanded mode restores the last user-selected width/height,
- restored sizes clamp to safe bounds on load,
- invalid or missing saved data falls back cleanly to default values.

This avoids a failure mode where a user accidentally leaves compact mode too small or too large for day-to-day reading.

## 7. Architecture

The redesign extends the existing custom Enlisted surface rather than replacing it with native chat systems.

### 7.1 Behavior responsibilities

`EnlistedCombatLogBehavior` remains the owner of:

- layer creation and disposal,
- message intake,
- conversation suspension/resume,
- widget discovery and wiring,
- top-level expand/collapse commands if those are routed through behavior hooks.

Its bottom-right-specific reposition logic is removed or drastically simplified because the new design no longer lives in the crowded alert corner.

### 7.2 View-model responsibilities

`EnlistedCombatLogVM` grows from a message list model into a UI-state model as well.

New responsibilities:

- compact vs expanded state,
- compact default width/height,
- expanded width/height,
- persisted expanded size application,
- command-facing properties needed by the prefab for expansion and resize feedback.

Existing responsibilities remain:

- message collection,
- expiration,
- inactivity fading,
- visibility gating by enlistment/mission state.

### 7.3 Prefab responsibilities

`GUI/Prefabs/Interface/EnlistedCombatLog.xml` becomes the primary home for the redesign.

Prefab changes include:

- top-right anchoring,
- larger compact default geometry,
- explicit expand affordance,
- expanded visual frame,
- resize handle and resize preview support,
- layout that stays readable at both compact and expanded sizes.

The widget should continue to favor scrolling over pagination or hard line count limits.

### 7.4 Persistence storage

Expanded-size persistence should use an **Enlisted-owned UI settings surface**, not Bannerlord's native chat config keys and not campaign save data.

Reason:

- the Enlisted combat log is not the native chat log,
- sharing native chat config keys would create hidden coupling,
- campaign saves are the wrong durability scope for a per-user UI preference,
- Enlisted-owned UI settings keep the data model explicit and reversible.

## 8. Failure Modes

The redesign must fail soft:

- If expanded-size persistence fails, the feed still renders in compact mode with defaults.
- If the expand affordance fails to wire up, the feed still works as a compact live feed.
- If a restored expanded size exceeds the current viewport, it clamps instead of rendering off-screen.
- If top-right overlap is discovered with another surface later, the first mitigation is clamping or tuning the box, not reviving the old bottom-right migration logic.

## 9. Verification Requirements

Implementation is not complete until the redesigned log is verified against actual usage conditions.

Required verification areas:

1. **Resolution coverage**
   - common desktop resolutions,
   - different UI scales,
   - no off-screen rendering in compact or expanded state.

2. **Interaction coverage**
   - compact scrolling,
   - expand,
   - resize,
   - collapse,
   - restore remembered expanded size on a fresh session.

3. **Behavior continuity**
   - message routing still works,
   - link clicks still work,
   - inactivity fade still works,
   - conversation suspend/resume still works.

4. **Corner-collision regression check**
   - alerts no longer compete with the combat log for the same corner,
   - removal of bottom-right avoidance logic does not create a new top-right overlap failure.

## 10. Recommended Implementation Direction

Implement this as an **evolution of the current custom log**, not a transplant of the native chat stack.

Recommended sequence:

1. Move the prefab and layout model to top-right with a larger compact default size.
2. Add explicit compact/expanded state in the VM.
3. Add manual expansion affordance.
4. Add resize affordance in expanded mode.
5. Add expanded-size persistence and restore.
6. Remove or simplify bottom-right-specific reposition code that is no longer justified.

This sequence keeps the feature usable after each stage and minimizes the chance of breaking message delivery while experimenting with native-style behavior.

## 11. Decision Record

These decisions were explicitly approved during brainstorming:

- Top-right is a hard requirement.
- The feed remains visible at all times.
- Expansion is manual only.
- Compact mode remains scrollable.
- The compact feed should be a little larger than today.
- Users can expand it like the native log.
- Expanded size should be remembered.

---

## 12. Summary

The redesign keeps the successful part of the current Enlisted combat log - a persistent readable feed of important campaign-map messages - and removes its largest weakness: living in the same bottom-right corner as alerts and other map surfaces.

The final target is a **top-right, always-visible, scrollable live feed** with a **manual native-style expanded mode** and **remembered expanded size**, implemented by extending Enlisted's existing custom UI rather than importing Bannerlord's full chat system.
