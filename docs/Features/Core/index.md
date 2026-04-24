# Enlisted Documentation Index

**Summary:** Single entry point for all Enlisted mod documentation, providing organized access to core systems, gameplay features, UI documentation, and technical references. This index reflects the current state of the mod (v0.9.0) targeting Bannerlord v1.3.13.

**Status:** ✅ Current  
**Last Updated:** 2025-12-23  
**Mod Version:** v0.9.0  
**Game Target:** v1.3.13

---

## Index

- [Quick Links](#quick-links)
- [How It All Works (End-to-End)](#how-it-all-works-end-to-end)
- [Core Systems](#core-systems)
- [User Interface](#user-interface)
- [Gameplay Features](#gameplay-features)
- [Technical Systems](#technical-systems)
- [Story Content & Events](#story-content--events)

---

## Quick Links

| What You Need | Where To Go |
|---------------|-------------|
| **Understand the core gameplay** | [Core Gameplay (Consolidated)](core-gameplay.md) |
| **Browse content** | [Content Overview](../Content/README.md) |

---

## How It All Works (End-to-End)

### 1) Enlist
Enlist with a lord to begin your career. Your party is hidden, and you follow the lord's movements on the campaign map.
- Doc: **[Enlistment System](enlistment.md)**

### 2) Initial Inspection: Bag Check
About one hour after enlisting, you must decide what to do with your personal gear: stash it (paid), sell it, smuggle it (skill check), or abort enlistment entirely.
- Doc: **[Enlistment System](enlistment.md)** (Behavior section)

### 3) Receive Orders
Instead of passive assignments, you receive explicit **Orders** from the chain of command every 3-5 days (config-driven). Success builds your reputation; failure damages it.
- Doc: **[Core Gameplay](core-gameplay.md)** (Orders System section)

### 4) Build Your Identity
Your role emerges from native Bannerlord traits that develop through event choices. Commander 10+ becomes Officer, ScoutSkills 10+ becomes Scout, Surgery 10+ becomes Medic, etc. We track your reputation with the Lord, the Officers, and your fellow Soldiers.
- Doc: **[Core Gameplay](core-gameplay.md)** (Emergent Identity section)
- Doc: **[Identity System](../Identity/identity-system.md)** (Complete trait reference)

### 5) Strategic Context & War Stance
The unit's experience is now strategically aware. Your orders and events reflect the lord's actual campaign plans, whether you're in a "Grand Campaign" offensive or a "Winter Camp" rest period.
- Doc: **[Core Gameplay](core-gameplay.md)** (Strategic Context section)

### 6) Monitor Company Status
The unit's effectiveness is tracked through company needs such as Readiness and Supplies. Enlisted Status and Camp summaries provide immersive descriptions with contextual explanations.
- Doc: **[News & Reporting System](../UI/news-reporting-system.md)** (Company Status section)

### 7) Read Status And Camp Dispatches
Enlisted Status and Camp summarize your company's situation, casualties, supply status, recent outcomes, and kingdom news in immersive narrative form.
- Doc: **[News & Reporting System](../UI/news-reporting-system.md)** (routing and menu consumers)

### 8) Visit the Quartermaster
The Quartermaster is your primary contact for gear and provisions. Access is gated by your reputation and the unit's supply levels.
- Doc: **[Quartermaster System](../Equipment/quartermaster-system.md)**

---

## Core Systems

The foundational systems that enable the Enlisted experience.

- **[Enlistment System](enlistment.md)** - Service mechanics, army following, and discharge.
- **[Storylet Backbone](../Content/storylet-backbone.md)** - Chain of command activities, storylet arcs, and authored duty outcomes.
- **[Promotion System](promotion-system.md)** - Rank progression T1-T9: XP sources, multi-factor requirements, proving events, culture-specific ranks.
- **[Pay System](pay-system.md)** - Wages, pay muster, and pay tension.
- **[Company Events](company-events.md)** - Role-based narrative and social events.
- **[Retinue System](retinue-system.md)** - Commander's personal force (T7+): formation selection, context-aware reinforcements, loyalty tracking, named veterans, and command decisions.
- **[Core Gameplay](core-gameplay.md)** - Consolidated overview of all systems and how they integrate.

---

## User Interface

Enlisted uses the native Bannerlord Game Menu system for all interactions.

- **[Native Menus](core-gameplay.md#native-game-menu-interface)** - Overview of the menu hub.
- **[News & Reporting System](../UI/news-reporting-system.md)** - Immersive narrative feedback: Daily Brief, Company Status, and kingdom news.
- **[UI Systems Master](../UI/ui-systems-master.md)** - Technical reference for all UI components.

---

## Gameplay Features

- **[Temporary Leave](../Campaign/temporary-leave.md)** - Suspend service while preserving progression.
- **[Camp Life Simulation](../Campaign/camp-life-simulation.md)** - Detailed tracking of unit-wide company needs.
- **[Provisions System](../Equipment/provisions-rations-system.md)** - Manage morale and fatigue through rations.
- **[Town Access](../Campaign/town-access-system.md)** - Safe exploration of settlements while enlisted.

---

## Technical Systems

- **[Encounter Safety](../Technical/encounter-safety.md)** - Crash prevention and state management.
- **[Formation Assignment](../Combat/formation-assignment.md)** - Battle deployment logic.

---

## Story Content & Events

- **[Storylet Backbone](../Content/storylet-backbone.md)** - Current source of truth for storylets, activities, triggers, effects, and qualities.
- **[Content Overview](../Content/README.md)** - Content authoring entry point.
