# Formation Assignment and Teleportation System

**Summary:** The formation assignment system ensures enlisted soldiers (T1-T6) are correctly positioned on the battlefield based on their equipped weapons. Players with bows spawn with Ranged, horses with Cavalry, both with Horse Archers, and melee weapons with Infantry. The system then teleports them to the correct position within their assigned formation. At T7+ (Commander tier), players control their own party and the system does not intervene with formation assignment.

**Status:** ✅ Current  
**Last Updated:** 2025-12-24  
**Related Docs:** Core Gameplay, [Retinue System](../Retinue/retinue-system.md)

---

## Index

- [Overview](#overview)
- [How It Works](#how-it-works)
- [Positioning Logic](#positioning-logic)
- [Technical Implementation](#technical-implementation)
- [Edge Cases](#edge-cases)

---

## Overview

In Bannerlord, your map party normally spawns slightly behind the main army. For enlisted soldiers (T1-T6), this system fixes that by:
1. Detecting your formation type from equipped weapons (bow/crossbow → Ranged, horse → Cavalry, both → Horse Archer, else Infantry)
2. Assigning you to that formation
3. Teleporting you to the formation's position

At T7+ (Commander tier), you have your own party and the system **does not** assign you to formations - you control where you spawn and fight, like a lord commanding their own troops.

---

## How It Works

### Formation Assignment (T1-T6 Only)

At the start of every battle, for **T1-T6 soldiers only**:

1. **Equipment Detection**: The system checks your `CharacterObject.IsRanged` and `IsMounted` properties to determine your formation:
   - Horse + Bow/Crossbow = **Horse Archer** formation
   - Horse (no ranged) = **Cavalry** formation  
   - Bow/Crossbow/Throwing (no horse) = **Ranged** formation
   - Melee weapons only = **Infantry** formation

2. **Formation Assignment**: You are assigned to the detected formation and placed under the command of the formation's AI or lord.

3. **Position Teleport**: The system teleports you to the correct position within your assigned formation to fix cases where your party spawned behind the main army.

### T7+ Commander Autonomy

At **Tier 7+ (Commander tier)**, you have your own party and command:
- **No Auto-Assignment**: The formation system does not intervene. You spawn wherever the native game places you.
- **Party Control**: Your companions and retinue follow standard party behavior.
- **Formation Freedom**: You can command your troops and position them as you see fit.

This reflects that commanders are independent officers with their own command, not regular soldiers taking orders.

### Command Authority (T1-T6)
-   **No command authority**: You are a soldier in the ranks with no ability to issue orders.
-   **No squad command**: Even with companions/retinue at lower tiers, you don't command them in battle.
-   **Follow orders**: You fight where your equipment places you (archers with archers, cavalry with cavalry, etc.).

---

## Positioning Logic (T1-T6 Only)

To prevent soldiers from being stranded far from their comrades (which happens when your map party was slightly behind the lord when battle started), the system executes a precise teleportation sequence:

### Teleport Priority
1.  **Formation Position**: Teleports to the center of all units assigned to your detected formation type (Infantry/Ranged/Cavalry/Horse Archer).
2.  **Solo Formation Fallback**: If your formation only contains you, the system searches for the largest populated allied formation to join.
3.  **Lord Position**: Falls back to the lord's formation location if no populated allied formation is found.
4.  **Allied Agent**: Last resort fallback to any allied soldier if the lord hasn't spawned yet.

### Context-Aware Placement
-   **Field Battles**: Positioned 5 meters behind the formation's front line to avoid spawning stuck inside other troops.
-   **Siege Assaults**: Positioned at the exact formation center to ensure you are with the initial wave of attackers.

### Distance Threshold
-   **Initial Spawn**: Only teleports if more than 10 meters from target formation.
-   **Reinforcements**: Always teleports reinforcement spawns to the formation regardless of distance (they spawn at map edge).

---

## Technical Implementation

-   **Behaviors**: `EnlistedFormationAssignmentBehavior.cs` manages the mission-start logic.
-   **Retry Window**: The system makes up to 120 attempts (~2 seconds) to find a valid position, allowing time for all army agents to spawn.
-   **Mounts**: Teleportation automatically handles mounts and rider synchronization.

---

## Edge Cases

-   **Naval Battles**: Formation assignment and teleportation are disabled in naval missions, as the Naval DLC uses a separate ship-based spawn system.
-   **Reinforcements**: The system detects when you spawn as a reinforcement wave (spawning at map edge, not with formation) and always teleports you to your formation regardless of distance.
-   **T7+ Commanders**: All formation assignment and teleportation logic is skipped for T7+ players. They are treated as independent commanders with their own party.
-   **Equipment Changes**: If you change equipment before a battle (e.g., equip a bow when you normally use melee), you'll spawn with the appropriate formation for that loadout.
-   **Mixed Loadouts**: If you have multiple weapon sets, the system uses Bannerlord's built-in `IsRanged`/`IsMounted` detection which checks all weapon slots to determine your primary combat role.
