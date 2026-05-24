# Town Access System

**Summary:** The town access system provides enlisted soldiers with full access to settlement features (trade, arena, tavern, workshops) while maintaining the stability of the invisible party state. The system handles safe enter/exit transitions and menu access without breaking the enlistment state.

**Status:** ✅ Current  
**Last Updated:** 2025-12-22  
**Related Docs:** [Enlistment](../Core/enlistment.md), [Core Gameplay](../Core/core-gameplay.md)

---

## Index

- [Overview](#overview)
- [Purpose](#purpose)
- [Access Flow](#access-flow)
- [Technical Implementation](#technical-implementation)
- [Edge Cases](#edge-cases)

---

## Overview

Enlisted soldiers do not occupy a separate party on the map, which normally prevents standard settlement interaction. This system uses synthetic encounters to temporarily activate the player's presence within a town or castle without breaking the enlisted follow behavior.

---

## Purpose

- **Full Functionality**: Enable trading, smithing, arena practice, and tavern visits while enlisted.
- **Engine Stability**: Avoid "Encounter Type" assertion crashes by using native-compliant synthetic encounters.
- **Seamless Experience**: Provide a clear "Visit Town" path that returns the player safely to the army camp.

---

## Access Flow

1.  **Detection**: When the enlisted lord enters a town or castle, a "Visit [Settlement]" option appears in the main Enlisted Status menu.
2.  **Activation**: Selecting the option triggers a synthetic encounter. The player's party is temporarily made active, and the game switches to the `town_outside` or `castle_outside` menu.
3.  **Interaction**: The player can use all vanilla settlement options.
4.  **Return**: A "Return to Army Camp" option is added to all settlement menus. Selecting it cleans up the synthetic encounter and restores the player's invisible, following state.

---

## Technical Implementation

- **Behaviors**: `EnlistedMenuBehavior.cs` manages the encounter lifecycle.
- **Synthetic Encounters**: Uses `EncounterManager.StartSettlementEncounter` to ensure the engine recognizes the player as being inside the settlement.
- **State Management**: A `_syntheticOutsideEncounter` flag tracks the state to ensure proper cleanup and party deactivation upon exit.

---

## Edge Cases

- **Siege State**: Town access is restricted if the settlement is under active siege.
- **Prisoner State**: If the player becomes a prisoner while in town (rare), native captivity logic takes priority.
- **Army Dispersal**: If the army disbands while the player is in town, the system detects the loss of the lord and initiates the standard grace period flow.
