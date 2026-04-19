# Company Events

**Summary:** Data-driven, role-based event system that delivers camp life moments, training opportunities, reputation shifts, and narrative choices during enlisted service. Events emerge based on player role (Scout, Medic, NCO, etc.) and campaign context, delivered through camp menu, automatic inquiries, or map incidents.

**Status:** ✅ Current  
**Last Updated:** 2026-01-06 (Added pressure arc events documentation)
**Related Docs:** [Content System Architecture](../Content/content-system-architecture.md), [Core Gameplay](core-gameplay.md), [Company Supply Simulation](../Equipment/company-supply-simulation.md#supply-pressure-arc-events)

---

## Index

- [Overview](#overview)
- [Delivery Channels](#delivery-channels)
- [Role-Based Events](#role-based-events)
- [Event Types](#event-types)
- [Integration](#integration)

---

## Overview

Enlisted v0.9.0 uses a unified event delivery pipeline that is **Role-Based** and **Context-Aware**. Events are no longer tied to a rigid schedule but emerge based on your role in the unit and the current campaign situation.

### Delivery Channels

| Channel | Description |
|---------|-------------|
| `menu` | Player-initiated via the **Camp** activities menu. |
| `inquiry` | Automatic popup for narrative or consequence delivery. |
| `incident` | Native map incidents that occur on the campaign map (e.g., post-battle, settlement entry). |

---

## Event Packs

Events are organized into packs by context and role:

| Pack | Context / Role | Purpose |
|------|----------------|---------|
| `camp_events.json` | General Camp Life | Daily life, social interactions, gambling, storytelling. |
| `events_general.json` | Universal Soldier | General enlisted experiences, muster, daily duties. |
| `events_escalation_thresholds.json` | Crisis Triggers | Scrutiny, Discipline, and Medical threshold events. |
| `events_onboarding.json` | First Enlistment | Guaranteed first-day experiences for new enlisted. |
| `events_promotion.json` | Rank Progression | Promotion opportunities, proving events, leadership tests. |
| `events_training.json` | Training Activities | Training-related events and progression. |
| `events_retinue.json` | Commander (T7+) | Retinue management events for high-rank commanders. |
| `events_pay_*.json` | Payment Context | Pay tension, loyalty, and mutiny events (3 files). |
| `Role/scout_events.json` | Scout Role | Reconnaissance, tracking, intelligence gathering. |
| `incidents_*.json` | Map Incidents | Battle, siege, town, village, leaving, waiting contexts (7 files). |
| `pressure_arc_events.json` | Pressure Arcs | Supply/morale/rest pressure narrative events (9). |

---

## Pressure Arc Events

**Summary:** Narrative events that fire at specific thresholds when company needs (supplies, morale, rest) remain low for extended periods.

**File:** `ModuleData/Enlisted/Events/pressure_arc_events.json`

### Supply Pressure Arc

Fires when supplies remain low for consecutive days, escalating from warnings to crisis:

| Threshold | Stage | Event IDs |
|-----------|-------|-----------|
| Day 3 | Stage 1: Warning | `supply_pressure_stage_1_grunt/nco/cmd` |
| Day 5 | Stage 2: Tension | `supply_pressure_stage_2_grunt/nco/cmd` |
| Day 7 | Crisis | `supply_crisis_grunt/nco/cmd` |

**Tier Variants:** Each stage has 3 tier-specific events:
- **Grunt (T1-T4):** Witness perspective - hunger, fights, whispers
- **NCO (T5-T6):** Squad leader perspective - managing squad tensions
- **Commander (T7+):** Strategic perspective - discipline breakdown, desertions

**Implementation:** `CompanySimulationBehavior.CheckPressureArcEvents()` fires events at exact day thresholds using `_companyPressure.DaysLowSupplies` counter.

**See Also:** [Company Supply Simulation](../Equipment/company-supply-simulation.md#pressure-tracking)

---

## Role-Based Routing

The system detects your **Primary Role** based on your native Bannerlord traits and routes events accordingly.

**Role Determination (priority order):**
- **Commander 10+** → Officer role (leadership events)
- **ScoutSkills 10+** → Scout role (reconnaissance events)
- **Surgery 10+** → Medic role (medical events)
- **Siegecraft 10+** → Engineer role (engineering events)
- **RogueSkills 10+** → Operative role (covert events)
- **SergeantCommandSkills 8+** → NCO role (squad leadership events)
- **Default** → Soldier role (general events)

**Event Priority:**
-   **Priority 1**: Escalation/Crisis events (triggered by high Scrutiny or low Discipline).
-   **Priority 2**: Role-specific events matching your current specialization.
-   **Priority 3**: General Soldier events (fallback).

---

## Event Structure

Each event is defined in JSON and supports complex requirements and outcomes.

### Requirements
Events can be gated by:
-   **Rank**: Minimum and maximum enlistment tiers (T1-T9).
-   **Native Traits**: Minimum levels in Bannerlord traits (ScoutSkills, Surgery, Siegecraft, etc.).
-   **Skills**: Minimum levels in native skills (Scouting, Medicine, Engineering, etc.).
-   **Context**: Campaign state tags like `war`, `siege`, `peace`, `camp`, `town`.
-   **Reputation**: Minimum thresholds for Lord, Officer, or Soldier reputation.

### Outcomes
Choices within events impact:
-   **Reputation**: LordReputation, OfficerReputation, SoldierReputation.
-   **Company Needs**: Readiness, Morale, Supplies, Equipment, Rest.
-   **Escalation**: Scrutiny and Discipline levels.
-   **Rewards**: Denars, Renown, Items, and Skill/Trait XP.

---

## Trigger System

### Role-Based Triggering
Events fire at an appropriate frequency (~1-2 per day) during `HourlyTickEvent`. The trigger logic:
1.  Rolls for a chance to fire (e.g., 5% per hour).
2.  Determines the player's role and campaign context.
3.  Filters the available event pool for eligible matches.
4.  Selects a random event from the filtered pool.

### Strategic Context Tags
Events are filtered by the unit's current strategic situation:
-   `coordinated_offensive`: The Grand Campaign (allied hosts gathering).
-   `desperate_defense`: The Last Stand (bleeding realm, every sword needed).
-   `raid_operation`: Harrying the Lands (bleeding the enemy's purse).
-   `siege_operation`: Siege Works (active siege engines and encircling walls).
-   `patrol_peacetime`: Riding the Marches (vigilance against bandits).
-   `garrison_duty`: Watching the walls from within a settlement.
-   `recruitment_drive`: Mustering for War (stockpiling and sharpening blades).
-   `winter_camp`: Seasonal rest and daily duties in the snow.

Legacy tactical tags (`war`, `peace`, `siege`, `town`, `outdoor`, `camp`) remain supported for broad categorization.

---

## Effects Application

When an option is selected, effects are applied through centralized managers:
-   **Trait XP**: Awarded via native `TraitLevelingHelper`.
-   **Reputation**: Modified via `EscalationManager`.
-   **Company Needs**: Modified via `CompanyNeedsManager`.

---

## File Locations

| Path | Purpose |
|------|---------|
| `ModuleData/Enlisted/Events/` | Event JSON definitions (loaded recursively). |
| `ModuleData/Enlisted/Decisions/` | Player-initiated decision definitions. |
| `ModuleData/Enlisted/Orders/` | Chain of command order definitions (3 files by tier). |
| `ModuleData/Languages/enlisted_strings.xml` | Localized text and templates. |
| `src/Features/Content/` | Event system implementation (EventCatalog, EventDeliveryManager, etc.). |

**Current Event Files:**
- `camp_events.json` - General camp life events
- `events_general.json` - Universal soldier events
- `events_escalation_thresholds.json` - Escalation (Scrutiny/Discipline/Medical) threshold events
- `events_onboarding.json` - First-enlistment guaranteed events
- `events_promotion.json` - Promotion and proving events
- `events_training.json` - Training-related events
- `events_retinue.json` - Retinue events (T7+ commanders)
- `events_pay_*.json` - Pay-related events (tension, loyalty, mutiny)
- `incidents_*.json` - Map incident events (battle, siege, town, village, leaving, waiting, retinue)
- `muster_events.json` - Muster and recruitment events
- `Role/scout_events.json` - Scout role-specific events

The EventCatalog loads all JSON files from Events/ and Decisions/ directories recursively at startup.

