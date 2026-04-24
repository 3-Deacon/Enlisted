# Encounter Safety System

**Summary:** Prevents enlisted players from triggering unwanted map encounters while hidden, ensures automatic battle participation when lord enters combat, provides reliable cleanup on discharge, and integrates with native menu system to yield when appropriate (e.g., siege menus).

**Status:** ✅ Current  
**Last Updated:** 2026-01-04 (Fixed battle side determination when lord is in army)  
**Related Docs:** [enlistment.md](../Core/enlistment.md), [formation-assignment.md](../Combat/formation-assignment.md), [battle-system-complete-analysis.md](../../Reference/battle-system-complete-analysis.md)

---

## Quick Reference

| State | Party Visibility | Encounter Behavior | Battle Participation |
|-------|-----------------|-------------------|---------------------|
| Enlisted (Peace) | Hidden (`IsActive = false`) | No encounters | N/A |
| Enlisted (Battle) | Hidden but Active (`IsActive = true`) | Auto-joins lord's battle | Yes |
| Not Enlisted | Visible (`IsActive = true`) | Normal encounters | Normal |

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
  - [During Enlistment (Peace/Travel)](#during-enlistment-peacetravel)
  - [During Service (Battle)](#during-service-battle)
  - [On Discharge](#on-discharge)
- [Technical Details](#technical-details)
  - [System Architecture](#system-architecture)
  - [State Management](#state-management)
  - [Battle Participation](#battle-participation)
- [Edge Cases](#edge-cases)
- [API Reference](#api-reference)
- [Debugging](#debugging)

---

## Overview

Prevents the player from triggering map encounters while enlisted by using the game engine's `IsActive` property and Harmony patches, with enhanced battle participation and cleanup logic.

**Key Features:**
- No map encounters during peaceful service (bandit raids, etc.)
- Automatic battle participation when lord enters combat
- Player party hidden from map systems (UI and logical)
- Smooth transitions between peace and battle states
- Reliable cleanup when returning to normal campaign play
- Hides native "Abandon army" and "Leave..." menu options while enlisted (join_encounter, encounter, army_wait, raiding_village)
- Automatic cleanup of stale encounters after auto-resolve victories (prevents stuck encounter menus)

**Purpose:**
Keep enlisted players from accidentally entering encounters that would break military service or cause pathfinding crashes, while ensuring they correctly join their Lord's battles without engine conflicts.

**Files:**
- `src/Features/Enlistment/Behaviors/EncounterGuard.cs` - Static utility class for encounter state management
- `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` - Active battle monitoring, siege sync, and participation logic
- `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` - enlisted_status menu with native-yielding tick handler
- `src/Features/Combat/Behaviors/EnlistedEncounterBehavior.cs` - Wait in Reserve menu, battle wait handling, and stale encounter auto-cleanup (`TriggerPostBattleCleanup`)
- `src/Mod.GameAdapters/Patches/HidePartyNamePlatePatch.cs` - UI suppression
- `src/Mod.GameAdapters/Patches/JoinEncounterAutoSelectPatch.cs` - Auto-joins lord's battles
- `src/Mod.GameAdapters/Patches/GenericStateMenuPatch.cs` - Controls menu override logic, allows native menus when appropriate (sieges, battles)
- `src/Mod.GameAdapters/Patches/AbandonArmyBlockPatch.cs` - Removes "Abandon army" options while enlisted; applied as a deferred patch on first campaign tick to avoid EncounterGameMenuBehavior static init issues
- `src/Mod.GameAdapters/Patches/EncounterLeaveSuppressionPatch.cs` - Hides "Leave..." option from encounter menu while enlisted; applied as a deferred patch
- `src/Mod.GameAdapters/Patches/EncounterSuppressionPatch.cs` - Blocks unwanted encounters, clears stale reserve flags
- `src/Mod.GameAdapters/Patches/PostDischargeProtectionPatch.cs` - Blocks party activation in vulnerable states
- `src/Mod.GameAdapters/Patches/VisibilityEnforcementPatch.cs` - Controls party visibility, allows captivity system control
- `src/Mod.GameAdapters/Patches/NavalBattleShipAssignmentPatch.cs` - Fixes naval battle crash (assigns ship from lord's fleet)
- `src/Mod.GameAdapters/Patches/PlayerEncounterFinishSafetyPatch.cs` - Crash protection for siege battle cleanup race condition
- `src/Mod.GameAdapters/Patches/RaftStateSuppressionPatch.cs` - Prevents "stranded at sea" menu when lord has ships
- `src/Mod.GameAdapters/Patches/NavalNavigationCapabilityPatch.cs` - Makes player inherit naval capability from lord/army to prevent army kick when traveling by sea

---

## How It Works

### Native Menu Transition System

Before understanding the enlisted mod's encounter handling, you must understand how the native game manages menu transitions and state changes.

#### The Tick-Based Refresh System

The native game uses a **tick handler** on every menu to automatically update state every frame:

**Example:** `menu_siege_strategies` tick handler (`SiegeEventCampaignBehavior.cs:249-264`)

```csharp
private void game_menu_siege_strategies_on_tick(MenuCallbackArgs args, CampaignTime dt)
{
  // Check if this is still the correct menu to show
  string genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
  
  if (genericStateMenu != "menu_siege_strategies")
  {
    // Wrong menu! Switch to correct one
    if (!string.IsNullOrEmpty(genericStateMenu))
      GameMenu.SwitchToMenu(genericStateMenu);
    else
      GameMenu.ExitToLast();
  }
  else
  {
    // Correct menu - refresh options based on current state
    Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(args.MenuContext);
  }
}
```

**Similar tick handlers exist for:**
- `army_wait` → checks if player is still in army
- `army_wait_at_settlement` → checks if army still at settlement
- `encounter` → checks if battle still active
- ALL settlement menus → check if player still at that location

#### How Menus Decide What to Show

The game uses two key methods:

1. **`GetEncounterMenu()`** - When creating a NEW encounter (approaching enemy, arriving at siege)
2. **`GetGenericStateMenu()`** - When checking CURRENT state (every tick while menu is open)

**Example flow when approaching a siege:**

```
Frame N:   Player approaches siege camp
           EncounterManager checks GetEncounterMenu()
           Sees: mainParty.AttachedTo == null (not in army yet)
           Opens: "join_siege_event" menu
           
Frame N+1: EnlistmentBehavior.OnRealtimeTick() runs
           Joins player to lord's army: mainParty.Army = lordParty.Army
           Sets: mainParty.AttachedTo = lordParty
           
Frame N+2: menu tick handler runs
           Calls GetGenericStateMenu()
           Sees: mainParty.AttachedTo != null (in army!)
           Sees: army at siege location
           Returns: "menu_siege_strategies"
           Switches to that menu
           
Frame N+3: menu_siege_strategies tick handler runs
           Calls RefreshMenuOptionConditions()
           Updates available buttons based on:
           - mainParty.BesiegerCamp (null = not commander)
           - mainParty.Army (not null = army member)
           Shows only: "Leave Army" option
```

#### The Race Condition Timeline

| Frame | What Happens | Menu | Army State | Why |
|-------|--------------|------|------------|-----|
| N | Encounter check | - | Not in army | Native checks before tick |
| N | Opens menu | `join_siege_event` | Not in army | Based on `AttachedTo == null` |
| N+1 | Tick runs | `join_siege_event` | **Joined!** | `EnlistmentBehavior` processed |
| N+2 | Tick handler checks | Switching... | In army | `GetGenericStateMenu()` returns different menu |
| N+3 | New menu active | `menu_siege_strategies` | In army | Correct state |

**Result:** For 1-2 frames, the wrong menu flashes before auto-correcting.

#### Why This Matters for Enlisted Mod

The enlisted mod must work WITH this system, not against it:

1. **Respect tick timing** - State changes happen over multiple frames
2. **Don't fight auto-correction** - Native will switch menus automatically
3. **Use menu patches carefully** - Blocking wrong menus can break the flow
4. **Sync army joining** - If possible, join army BEFORE encounter is created

**Key files that handle this:**
- `JoinEncounterAutoSelectPatch.cs` - Handles joining battles
- `GenericStateMenuPatch.cs` - Keeps player in enlisted menus, allows native menus when appropriate
- `EncounterSuppressionPatch.cs` - Blocks unwanted encounters entirely
- `EnlistmentBehavior.cs` - State management via IsActive for siege waiting
- `EnlistedMenuBehavior.cs` - enlisted_status tick handler that yields to native menus

#### How enlisted_status Menu Integrates with Native System

The `enlisted_status` menu is a **wait menu** (like `army_wait` or `menu_siege_strategies`) that keeps the player in their enlisted state while displaying information and options. However, there are times when native menus MUST take over:

- **Sieges:** When lord's army starts besieging, `menu_siege_strategies` should appear
- **Battles:** When lord enters combat, `encounter` menu should appear
- **Settlements:** When explicitly visiting a town/castle (via "Visit Settlement" option)

**The Two-Part Integration System:**

**Part 1: GenericStateMenuPatch (Postfix on GetGenericStateMenu)**

```csharp
// File: GenericStateMenuPatch.cs
public static void Postfix(ref string __result)
{
    // Check if lord is in siege
    var lordInSiege = lordParty?.SiegeEvent != null || 
                      lordParty?.BesiegerCamp != null || 
                      lordParty?.BesiegedSettlement != null;
    
    // If at siege, allow native menu to flow through
    if (lordInSiege)
    {
        ModLogger.Debug("Menu", $"Allowing native menu '{__result}' during siege");
        return;  // Don't override - let native siege menu show
    }
    
    // Otherwise, override to enlisted_status
    if (__result == "army_wait" || __result == "army_wait_at_settlement")
    {
        __result = "enlisted_status";
    }
}
```

This patch controls WHAT menu native systems WANT to show. When at a siege, it returns early and lets native's result (`menu_siege_strategies` or `army_wait`) flow through unchanged.

**Part 2: enlisted_status Tick Handler (OnEnlistedStatusTick)**

```csharp
// File: EnlistedMenuBehavior.cs
private void OnEnlistedStatusTick(MenuCallbackArgs args, CampaignTime dt)
{
    // Check if native wants a different menu
    var desiredMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
    
    // If native wants a different menu, yield to it
    if (!string.IsNullOrEmpty(desiredMenu) && 
        desiredMenu != "enlisted_status" && 
        desiredMenu != "enlisted_battle_wait")
    {
        ModLogger.Info("Menu", 
            $"OnEnlistedStatusTick: Native wants '{desiredMenu}' - yielding to native menu system");
        GameMenu.SwitchToMenu(desiredMenu);
        return;
    }
    
    // Otherwise, stay in enlisted_status and refresh display
    RefreshEnlistedStatusDisplay(args);
}
```

This tick handler runs **every frame** while `enlisted_status` is open. It actively checks if native wants a different menu and yields when appropriate.

**The Complete Flow When Siege Starts:**

```
Frame N:   Lord's army arrives at enemy castle
           Player in enlisted_status menu (open for hours)
           
Frame N+1: BesiegerCamp created, lord.BesiegerCamp set
           enlisted_status tick handler runs
           Calls GetGenericStateMenu()
           
           → GenericStateMenuPatch.Postfix runs
           → Detects lordInSiege = true
           → Returns early, allowing native result
           → Native returns "menu_siege_strategies"
           
           → OnEnlistedStatusTick receives "menu_siege_strategies"
           → Logs: "Native wants 'menu_siege_strategies' - yielding to native menu system"
           → Calls GameMenu.SwitchToMenu("menu_siege_strategies")
           
Frame N+2: menu_siege_strategies now active
           Shows army member options ("Leave Army")
           Player can now interact with siege
```

**Why This Design Works:**

1. **No race conditions:** Tick handlers run every frame, catching state changes immediately
2. **Native-friendly:** Mirrors exactly how native menus work (they all check GetGenericStateMenu)
3. **Two-way coordination:** Patch controls what menu is desired, tick handler acts on it
4. **Graceful transition:** Player sees smooth menu switch without flashing or loops

**What Happens When Muster Fires Mid-Siege:**

This explains why catching a muster during siege building caused the correct menu to appear:

```
Frame N:   enlisted_status open (hasn't checked GetGenericStateMenu yet this frame)
Frame N+1: Muster sequence completes
           Calls EnlistedMenuBehavior.SafeActivateEnlistedMenu()
           
           → SafeActivateEnlistedMenu checks lordInSiege
           → Detects lord.BesiegerCamp != null
           → Skips activation, logs: "Lord/army at siege - allowing native menu flow"
           → Returns early without activating enlisted_status
           
           → Native menu system takes over
           → GetGenericStateMenu() returns "menu_siege_strategies"
           → menu_siege_strategies opens
           → Correct menu appears!
```

The muster callback essentially "kicked" the menu system into re-evaluating what menu should be shown, which triggered the siege detection logic.

**Before This Fix:**

The `enlisted_status` menu would stay open indefinitely during sieges because:
- It never called `GetGenericStateMenu()` to check if native wanted a different menu
- Native's tick system only runs for the CURRENT menu - it couldn't switch away from `enlisted_status`
- Player was stuck until something (like muster) caused a menu re-evaluation

**After This Fix:**

The `enlisted_status` menu actively participates in the native tick system:
- Checks `GetGenericStateMenu()` every frame like native menus do
- Yields immediately when native wants to show siege/battle/settlement menus
- Returns to `enlisted_status` when those situations end (via the same mechanism in reverse)

### During Enlistment (Peace/Travel)

**State:** Player party is inactive and hidden

**Process:**
1. Player enlists -> `MobileParty.MainParty.IsActive = false`
2. Nameplate hidden via `HidePartyNamePlatePatch`
3. Game engine stops considering player for encounters
4. Player follows lord's army without map interference
5. System continuously enforces state (prevents manual changes)

**Result:**
- No bandit raids or random encounters
- No pathfinding crashes
- Player party invisible on map
- Smooth following behavior

### During Service (Battle)

**State:** Player party is active but visually hidden, participates in battle

**Process:**
1. Lord enters battle (`MapEvent` detected)
2. `EnlistmentBehavior` activates player party (`IsActive = true`) while keeping it visually hidden
3. Player party kept in same army/attachment so vanilla encounter stack automatically collects it
4. `JoinEncounterAutoSelectPatch` intercepts "join_encounter" menu and automatically joins battle on lord's side
5. Player sees standard encounter menu (Attack/Send Troops/Wait) instead of "Help X's Party / Don't get involved"
6. `AbandonArmyBlockPatch` and `EncounterLeaveSuppressionPatch` hide "Abandon army" and "Leave..." options while enlisted
7. Player participates in battle
7. If the player is a prisoner or capture cleanup is queued, battle handling is skipped to let native captivity finish (prevents crash when captors are defeated by friendlies).

**Battle Types:**

**Manual Battle:**
- Player's troops participate directly in combat
- Standard battle interface
- Player controls their squad

**Autosim (Send Troops):**
- Relies on vanilla autosim (player party isn't injected)
- After seeing report, expect encounter menu (Attack/Surrender) if army loses
- Player always resolves outcome through standard encounter UI

**Wait in Reserve:**
- Available for field battles, sally out battles, and siege preparation (not siege assaults)
- Player sits out the battle while the army fights
- `IsWaitingInReserve` flag tracks reserve state
- Player stays in `enlisted_battle_wait` menu until army finishes or player rejoins
- `GenericStateMenuPatch` intercepts `GetGenericStateMenu()` to return `enlisted_battle_wait` when in reserve, preventing native menu systems from switching to `army_wait` (which would cause visual stutter)
- Available on three menus: `encounter` (field battles), `join_siege_event` (sally outs), and `menu_siege_strategies` (siege preparation)
- Blocked only during active siege assaults (`mapEvent.IsSiegeAssault == true`) when attacking walls
- Sally out battles (defenders attacking) are treated as optional battles, not siege assaults
- Rejoin removes player from reserve, re-adds them to the MapEvent on lord's side, then activates encounter menu
- **Auto-cleanup:** If the battle has already ended (`mapEvent.HasWinner` or `mapEvent.IsFinalized`), the option won't show - instead, `TriggerPostBattleCleanup()` runs and returns player to `enlisted_status` automatically
- Handled by `EnlistedEncounterBehavior` and `GenericStateMenuPatch`

**Result:**
- Player automatically joins lord's battles
- Correct encounter menus (not "Help or Leave")
- Predictable battle behavior
- No manual intervention needed

### On Discharge / End of Service

**State:** Player party returns to normal visibility and encounter behavior

**Process:**
1. Player leaves service -> `MobileParty.MainParty.IsActive = true`
2. Game engine re-enables normal encounter behavior
3. Player returns to normal world interaction
4. Nameplate restored

**Result:**
- Normal encounter behavior restored
- Player party visible on map
- Full campaign map interaction available

---

## Technical Details

### System Architecture

**Component Structure:**
```
EncounterGuard (Static Utility)
    ├── Initialize() - Placeholder for system init
    ├── TryLeaveEncounter() - Safely exits encounters and returns to enlisted menu
    ├── TryAttachOrEscort(lord) - Attaches player party to lord using escort AI
    ├── HidePlayerPartyVisual() - Sets IsVisible = false
    └── ShowPlayerPartyVisual() - Sets IsVisible = true

EnlistmentBehavior (Campaign Behavior)
    ├── OnTick() - Continuous state enforcement (sets IsActive directly)
    ├── Battle detection (MapEvent monitoring)
    └── Battle participation logic

HidePartyNamePlatePatch (Harmony Patch)
    └── Suppresses party nameplate UI

JoinEncounterAutoSelectPatch (Harmony Patch)
    └── Auto-joins lord's battle, bypasses choice menu
```

### State Management

**Direct State Manipulation:**

The encounter system controls party visibility and activity by setting `IsActive` and `IsVisible` directly throughout `EnlistmentBehavior`, rather than through abstraction methods. This inline approach provides fine-grained control during the many state transitions that occur during enlistment.

```csharp
// Throughout EnlistmentBehavior, state is set directly:
MobileParty.MainParty.IsActive = false;  // Disable encounters during service
MobileParty.MainParty.IsVisible = false; // Hide party on map

// Visibility helpers are in EncounterGuard:
EncounterGuard.HidePlayerPartyVisual();  // Sets IsVisible = false
EncounterGuard.ShowPlayerPartyVisual();  // Sets IsVisible = true
```

**Monitoring:**
- `EnlistmentBehavior.OnTick()` continuously enforces party state
- State checked every frame during military service
- Immediate response to enlistment status changes
- Prevents manual state changes by player or other systems

**State Validation:**
- System continuously resets `IsActive = false` during service
- Logs warnings if state doesn't match expected service status
- Recovers automatically on next tick

### Battle Participation

**Battle Detection:**
- Monitors `MapEvent` state for lord's party
- Detects when lord enters combat
- Activates player party for battle participation

**Auto-Join Logic:**
- `JoinEncounterAutoSelectPatch` intercepts encounter menu
- Automatically selects lord's side
- Bypasses "Help or Leave" choice menu
- Routes to standard encounter menu (Attack/Send Troops/Wait)

**Battle Side Determination:**

When determining which side the player should join in battle, the code must handle **two race conditions** related to army membership timing:

---

**Race Condition #1: Lord in army but not in MapEvent parties yet**

**Problem:** Only the army leader's party may be listed in `mapEvent.AttackerSide.Parties` initially - the enlisted lord's individual party isn't added until later. Checking only for the lord's party would incorrectly default to Defender side.

**Example Scenario:**
```
Player enlisted with Lord Hecard
Hecard is in King Derthert's army
Derthert attacks Lamcard
mapEvent.AttackerSide.Parties contains: [Derthert] (not Hecard yet!)
lordIsAttacker check fails → defaults to Defender → player joins Lamcard's side ❌
```

**Fix #1 in `ForceImmediateBattleJoin` (EnlistmentBehavior.cs ~1477):**

```csharp
// ARMY FIX #1: Check lord's army leader when lord isn't in parties list yet
if (!lordIsAttacker && lordParty.Army != null)
{
    var armyLeaderParty = lordParty.Army.LeaderParty?.Party;
    if (armyLeaderParty != null)
    {
        lordIsAttacker = attackerSide?.Parties?.Any(p => p?.Party == armyLeaderParty) == true;
    }
}
```

---

**Race Condition #2: Player auto-collected into army BEFORE lord joins**

**Problem:** When the lord is approaching an army to join, the native game may auto-collect the player's party into the army (within proximity radius) before the lord has physically merged. If a battle starts during this window, `lordParty.Army` is `null` but `mainParty.Army` is not null.

**Example Scenario:**
```
Lord Hecard approaching King Derthert's army
Player party collected into army (proximity-based) before Hecard merges
Battle starts while Hecard is still "in transit"
lordParty.Army == null (lord not in army yet!)
mainParty.Army == Derthert's army (player IS in army)
lordIsAttacker check fails → defaults to Defender → player joins wrong side ❌
```

**Fix #2 in `ForceImmediateBattleJoin` (EnlistmentBehavior.cs ~1493):**

```csharp
// ARMY FIX #2: Player in army before lord - use player's army leader
if (!lordIsAttacker && lordParty.Army == null && main.Army != null)
{
    var playerArmyLeaderParty = main.Army.LeaderParty?.Party;
    if (playerArmyLeaderParty != null)
    {
        lordIsAttacker = attackerSide?.Parties?.Any(p => p?.Party == playerArmyLeaderParty) == true;
    }
}
```

---

**Complete implementation in `JoinEncounterAutoSelectPatch.cs` (~88-131):**

The patch implements a comprehensive check that handles both scenarios:
- Checks if lord's party is directly in battle
- Checks if ANY army member is in battle (handles both race conditions)
- Determines side based on either lord or army member presence

**Log Patterns:**

```
# Fix #1 - Lord in army but not in parties:
[Battle] Lord's army leader (Derthert) is attacker - joining attacker side

# Fix #2 - Player in army before lord:
[Battle] Player in army before lord - using player's army leader (Derthert) to determine attacker side
```

**Related:** Line 7667 in `EnlistmentBehavior.cs` (lord with no army path) doesn't need these checks because it's specifically for cases where neither party is in an army.

**Menu Routing:**
- Ensures "Encounter" menu instead of "Help or Leave"
- Standard battle interface for player
- Predictable behavior across all battle types

**Abandon Army & Leave Suppression:**
- Targeted menus: 
  - `join_encounter` (join_encounter_abandon)
  - `encounter` (abandon_army, leave)
  - `army_wait` (abandon_army)
  - `raiding_village` (abandon_army)
- Patch files: 
  - `AbandonArmyBlockPatch` (army_wait, raiding_village)
  - `EncounterAbandonArmyBlockPatch/EncounterAbandonArmyBlockPatch2` (join_encounter, encounter abandon)
  - `EncounterLeaveSuppressionPatch` (encounter leave)
- Application timing: encounter-menu patches are deferred to first campaign tick (after localization init) to avoid EncounterGameMenuBehavior static-init issues

**Siege Auto-Resolve Crash Guard:**
- Patch file: `PlayerEncounterFinishSafetyPatch` (Harmony finalizer/prefix on `PlayerEncounter.Finish`)
- Purpose: Prevents siege cleanup crashes (commonly triggered after auto-resolve) by validating encounter state and swallowing NullReferenceException while enlisted; lets other exceptions propagate

**Naval Battle Safety:**
- Sea-state sync: `ForceImmediateBattleJoin` syncs `IsCurrentlyAtSea` state and position to lord before `PlayerEncounter.Start/Init()`. Matches native requirement (`encounteredBattle.IsNavalMapEvent == MainParty.IsCurrentlyAtSea`).
- Ship assignment: `NavalBattleShipAssignmentPatch` provides five patches to prevent crashes:

  **Patch 1 - GetSuitablePlayerShip:** Intercepts before Naval DLC calls `MinBy` on empty ship collection:
  - Tier 1-6: Always board lord's ship (soldiers don't command vessels)
  - Tier 7+ with retinue AND ships: Can command own vessel (rare edge case)
  - Capacity-aware: Prefers ships that can fit player's party size, falls back to largest
  - Logs ship name, hull health, capacity, and fleet composition per battle

  **Patch 2 - GetOrderedCaptainsForPlayerTeamShips:** Assigns lord as captain when player borrows a ship (prevents null reference when looking up ship owner).

  **Patch 3 - AllocateAndDeployInitialTroopsOfPlayerTeam:** Handles cases where player team's `MissionShip` is null. Finds any friendly ship in the mission and spawns player there as crew.

  **Patch 4 - OnUnitAddedToFormationForTheFirstTime:** Prevents crash when adding AI behaviors to formations without ships. The Naval DLC creates `BehaviorNavalEngageCorrespondingEnemy` for all formations, but crashes if a formation has no ship assigned (common when enlisted player has no ships). This patch checks for a ship BEFORE creating behaviors - if no ship exists, it skips behavior creation entirely and just calls `ForceCalculateCaches()`.

  **Patch 5 - OnShipRemoved:** Safe mission cleanup when battle ends. The Naval DLC's cleanup code iterates through agents and calls `FadeOut()`, but agents in certain states (null Team, already inactive) cause a native crash. This patch intercepts `NavalTeamAgents.OnShipRemoved` and performs cleanup with null checks before each operation. Skips FadeOut for agents that are inactive or have null Team.

  **Why Patch 4 is critical:** When there are more formations than ships (e.g., player formations without ships when enlisted under a lord), the Naval DLC tries to create AI behaviors for all formations. The `BehaviorNavalEngageCorrespondingEnemy` constructor calls `GetShip()` and then immediately accesses `_formationMainShip.GameEntity` - if GetShip returns null, this crashes. We can't patch the constructor itself (returning false from a constructor prefix leaves the object uninitialized), so we patch the caller to prevent behavior creation when no ship exists.

  **Why Patch 5 is critical:** After battle ends, `OnEndMission()` removes all ships, which triggers `OnShipRemoved` for each ship. The original code assumes all agents are still valid, but during enlisted naval battles, agents may be in transitional states. The `DequeueReservedTroop` method has multiple overloads - must use `AccessTools.Method(type, "DequeueReservedTroop", new[] { navalShipAgentsType })` to avoid "Ambiguous match" exception.

  **Patch 6 - Raft State Suppression (stranded UI):** Prevents the Naval DLC `player_raft_state`/"stranded at sea" menu from firing when the enlisted player's lord (or army leader) still has ships. Deactivates the raft state and resyncs the player to the lord at sea; allows the raft menu only when the lord truly has no naval capability.

### Siege Encounters

**Overview:**
Sieges present special challenges because the native game uses multiple menus depending on the player's role (commander vs. army member) and the presence of active battles.

#### Siege Menu Types

**1. `join_siege_event`** - Initial arrival at siege location
- Shows when approaching a siege camp for the first time
- Appears when `mainParty.AttachedTo == null` (not in army yet)
- Has "Attack!" button that sets `BesiegerCamp` and makes player the commander
- **Enlisted players should NOT see this** - they should be in army already

**2. `menu_siege_strategies`** - Commander or army member at siege
- Shows based on two conditions:
  - `mainParty.BesiegerCamp != null` (player is commander), OR
  - `mainParty.AttachedTo != null && army at siege location` (player is army member)
- Options depend on role:
  - **Commander:** "Lead assault", "Send troops", "Leave"
  - **Army member:** "Leave Army" only
- **This is what enlisted players SHOULD see** at sieges

**3. `encounter`** - Active battle at siege
- Shows when `settlement.Party.MapEvent != null` (battle in progress)
- Standard battle menu: "Attack", "Send Troops", "Wait in Reserve"
- Used for sally outs and active assaults

#### The Join Siege Event Flow

**When clicking "Attack!" in `join_siege_event`:**

**File:** `EncounterGameMenuBehavior.cs:738-762`

```csharp
private void game_menu_join_siege_event_on_consequence(MenuCallbackArgs args)
{
  if (Settlement.CurrentSettlement.Party.MapEvent != null)
  {
    // Active battle exists - join it
    PlayerEncounter.JoinBattle(...);
    GameMenu.SwitchToMenu("encounter");
  }
  else
  {
    // No battle - enter siege preparation mode
    PlayerEncounter.Finish();
    MobileParty.MainParty.BesiegerCamp = settlement.SiegeEvent.BesiegerCamp;  // ← CRITICAL
    PlayerSiege.StartPlayerSiege(BattleSideEnum.Attacker, settlement);
    PlayerSiege.StartSiegePreparation();  // Opens menu_siege_strategies
  }
}
```

**The Problem Line:** `MobileParty.MainParty.BesiegerCamp = settlement.SiegeEvent.BesiegerCamp`

Once this is set:
- Player becomes the siege commander
- `GetGenericStateMenu()` ALWAYS returns `"menu_siege_strategies"` (commander view)
- Player sees commander options, not army member options
- Conflicts with enlisted role

**The Mod's Prevention:**

**File:** `EnlistmentBehavior.cs:7917-7931`

```csharp
// Do not copy BesiegerCamp onto it. Native GetGenericStateMenu() returns menu_siege_strategies
// whenever MainParty.BesiegerCamp != null, which forces commander-level siege menus.
if (mainParty != null && IsEnlisted && !_isOnLeave)
{
    if (mainParty.BesiegerCamp != null)
    {
        mainParty.BesiegerCamp = null;  // Clear it immediately
    }
    return;
}
```

Enlisted players' `BesiegerCamp` is forcibly cleared every tick. This prevents them from becoming siege commanders.

#### The Race Condition at Siege Arrival

**Timeline:**

| Frame | Event | Menu | Army State | BesiegerCamp |
|-------|-------|------|------------|--------------|
| N | Approach siege | - | Not joined | null |
| N | Encounter check | - | Not joined | null |
| N | `GetEncounterMenu()` | - | `AttachedTo == null` | null |
| N | Menu opens | `join_siege_event` | Not joined | null |
| N+1 | Player clicks "Attack!" | Processing | Not joined | null |
| N+1 | Consequence runs | - | Not joined | **SET!** |
| N+1 | `StartSiegePreparation()` | - | Not joined | SET (mod clears) |
| N+1 | Menu opens | `menu_siege_strategies` | Not joined | null (cleared) |
| N+2 | `EnlistmentBehavior` tick | `menu_siege_strategies` | **Joined!** | null |
| N+3 | Menu tick handler | `menu_siege_strategies` | In army | null |
| N+3 | `RefreshMenuOptionConditions()` | `menu_siege_strategies` | In army | null |
| N+3 | Options update | Shows "Leave Army" | ✅ Correct | ✅ Correct |

**The Issue:** 
For frames N to N+1, the wrong menu (`join_siege_event`) is visible with a non-functional "Attack!" button.

**Why "Attack!" Doesn't Work:**
1. Player clicks it
2. Sets `BesiegerCamp` (making player commander)
3. Mod immediately clears `BesiegerCamp` on next tick
4. Menu opens but player is NOT actually a commander
5. Menu shows commander options, but they fail because `BesiegerCamp == null`

#### Current Implementation (Tick-Based Yielding)

**The enlisted_status menu now participates in the native tick system:**

1. Player arrives at siege location while `enlisted_status` is open
2. Every frame, `OnEnlistedStatusTick` calls `GetGenericStateMenu()`
3. `GenericStateMenuPatch` detects `lordInSiege=true` and allows native result to flow through
4. Native returns `"menu_siege_strategies"` (army member at siege)
5. `OnEnlistedStatusTick` sees different menu desired → switches immediately
6. Player sees `menu_siege_strategies` with correct army member options

**Log Pattern:**
```
[Menu] OnEnlistedStatusTick: Native wants 'menu_siege_strategies' - yielding to native menu system
[Menu] GenericStateMenuPatch: Allowing native menu 'menu_siege_strategies' during siege
```

**Advantages:**
- ✅ **Zero race conditions:** Tick handler catches state changes every frame
- ✅ **Native-compatible:** Mirrors exactly how native menus work
- ✅ **Smooth transitions:** No menu flashing or loops
- ✅ **Works for all scenarios:** Sieges, battles, settlements
- ✅ **Minimal code:** Simple tick check, no complex synchronization logic

**Why This Is Better Than Force-Join Approaches:**

Previous approaches tried to prevent the wrong menu from opening. The tick-based approach instead:
- Lets enlisted_status stay open most of the time (correct behavior)
- Yields immediately when native needs to take over (sieges, battles)
- Returns to enlisted_status when those situations end
- No fighting with native's encounter system
- No duplicating army-join logic with potential timing conflicts

**Integration Points:**

1. **GenericStateMenuPatch.Postfix** - Controls what menu native WANTS to show
2. **OnEnlistedStatusTick** - Acts on what native wants, switching when needed
3. **SafeActivateEnlistedMenu** - Prevents activating enlisted_status when at siege/battle
4. **TrySyncBesiegerCamp** - Correctly sets `mainParty.BesiegerCamp` for army members

**The Complete Siege Integration System:**

```
Component                    | Responsibility
-----------------------------|------------------------------------------------------
TrySyncBesiegerCamp         | Sets mainParty.BesiegerCamp = lordCamp (for siege participation)
GenericStateMenuPatch       | When lordInSiege: allows native "menu_siege_strategies" result
OnEnlistedStatusTick        | Calls GetGenericStateMenu() every frame, switches when needed
SafeActivateEnlistedMenu    | Checks lordInSiege before activating, skips if true
menu_siege_strategies       | Native menu filters options based on role (commander vs army)
```

All components work together to ensure seamless siege integration.

### Complete Menu Flow: Siege Arrival Example

This section shows the complete interaction between all components when the player's lord starts a siege.

**Timeline: Lord's Army Arrives at Enemy Castle**

| Frame | Component | Action | Result |
|-------|-----------|--------|--------|
| N | EnlistmentBehavior.OnRealtimeTick | Detects lord at siege location | - |
| N | TrySyncBesiegerCamp | Checks if siege camp exists | Found: Hvalvik siege |
| N | TrySyncBesiegerCamp | Sets `mainParty.BesiegerCamp = lordCamp` | Player now in `_besiegerParties` |
| N | TrySyncBesiegerCamp | Logs integration status | ✅ InArmyParties, ✅ InBesiegerParties |
| N | Player | Currently viewing `enlisted_status` menu | Menu open for hours |
| N+1 | OnEnlistedStatusTick | Tick handler runs (every frame) | Check state |
| N+1 | OnEnlistedStatusTick | Calls `GetGenericStateMenu()` | Query native |
| N+1 | GenericStateMenuPatch.Postfix | Intercepts call | Evaluate state |
| N+1 | GenericStateMenuPatch | Checks `lordInSiege` | TRUE - lord.BesiegerCamp != null |
| N+1 | GenericStateMenuPatch | Returns early (allows native result) | No override |
| N+1 | Native GetGenericStateMenu | Checks `mainParty.BesiegerCamp != null` | TRUE |
| N+1 | Native GetGenericStateMenu | Returns `"menu_siege_strategies"` | Native menu desired |
| N+1 | OnEnlistedStatusTick | Receives `"menu_siege_strategies"` | Different from current! |
| N+1 | OnEnlistedStatusTick | Compares to current menu | enlisted_status != menu_siege_strategies |
| N+1 | OnEnlistedStatusTick | Logs: "Native wants 'menu_siege_strategies' - yielding" | Info message |
| N+1 | OnEnlistedStatusTick | Calls `GameMenu.SwitchToMenu("menu_siege_strategies")` | Switch menu |
| N+2 | menu_siege_strategies | Menu opens, init handler runs | Setup |
| N+2 | menu_siege_strategies | Calls `RefreshMenuOptionConditions()` | Evaluate options |
| N+2 | Native siege logic | Checks if player is commander | `BesiegerCamp` != null? |
| N+2 | Native siege logic | Checks if player is army member | `Army` != null? TRUE |
| N+2 | Native siege logic | Filters options for role | Army member options |
| N+2 | Player UI | Shows "Leave Army" option only | ✅ Correct role |
| N+3+ | menu_siege_strategies tick | Runs every frame | Updates display |
| N+3+ | menu_siege_strategies tick | Calls `GetGenericStateMenu()` | Still "menu_siege_strategies"? |
| N+3+ | GenericStateMenuPatch | Still `lordInSiege=true` | Allows native result |
| N+3+ | Native | Returns "menu_siege_strategies" | Same menu |
| N+3+ | menu_siege_strategies tick | No change needed | Stay on current menu |

**What Changed vs. Before:**

**Before Fix:**
- `enlisted_status` never called `GetGenericStateMenu()`
- Menu stayed open indefinitely during sieges
- Only switched when something (like muster) caused a re-evaluation
- Player couldn't interact with siege mechanics

**After Fix:**
- `enlisted_status` checks `GetGenericStateMenu()` every frame
- Detects siege within 1-2 frames (< 100ms)
- Yields immediately to native siege menu
- Player sees correct menu and can interact with siege

**Integration Verification:**

The system includes diagnostic logging to verify siege integration is working:

```csharp
LogSiegeIntegrationStatus(mainParty, lordParty, camp, "TrySyncBesiegerCamp");
```

**Log Output:**
```
[SiegeIntegration] === Siege Integration Status (TrySyncBesiegerCamp) ===
[SiegeIntegration] Settlement: Hvalvik
[SiegeIntegration] Player Status:
[SiegeIntegration]   - InArmyParties: True (1/14 parties)
[SiegeIntegration]   - InBesiegerParties: True (1/14 parties)
[SiegeIntegration]   - AttachedToLeader: True
[SiegeIntegration]   - InMapEvent: N/A (no battle yet)
[SiegeIntegration] Integration: ✅ COMPLETE
```

This confirms the player is correctly integrated into the siege before the menu switch occurs.

### Activation Gating (Inactive Mode)

- Global gate: `EnlistedActivation` (default off). Flips on at enlist start, off at discharge; synced on load from `IsEnlisted`.
- Guard pattern: behaviors and patches early-return when inactive; logs once if something runs while inactive.
- Active while enlisted, on leave, or in desertion grace so protections stay on during grace/leave; deactivates only once all three are false.
- Crash guards: currently off while inactive per design; can be whitelisted later if needed.
- Menus: remain registered, but handlers/ticks are inert when inactive.
- Scope: finance/food/XP/formation/influence/encounter/nameplate/visibility/captain/return-to-army/order-of-battle, etc., all guarded.

---

## Edge Cases

### Player Manually Changes Party State

**Scenario:** Player or other system tries to change `IsActive` during service

**Handling:**
- System continuously resets `IsActive = false` during service
- Prevents player from accidentally re-enabling encounters
- State enforced every tick

### Game Engine State Conflicts

**Scenario:** Unexpected `IsActive` changes from game engine

**Handling:**
- Monitor for unexpected `IsActive` changes
- Log warnings if state doesn't match expected service status
- Recover automatically on next tick
- No crashes or state corruption

### Post-Victory Menu Loop (Field Battles and Sieges)

**Scenario:** After winning a battle via auto-resolve, the encounter menu reopens instead of closing

**Root Cause:** 
- After clicking "Done" on the scoreboard, the native game calls `GameMenu.ActivateGameMenu("encounter")`
- This is normal behavior for ongoing battles, but incorrect when the battle is actually over
- For enlisted players, this causes either the encounter menu or siege menu to reopen in a loop

**Handling:**
- For SIEGE battles: Native handles post-victory flow completely (menu_settlement_taken_*, etc.)
- For FIELD battles: `OnMapEventEnded` defers cleanup and returns player to enlisted menu
- Native victory menus are allowed to flow naturally
- Works for both field battles and siege battles
- Prevents "You have arrived at X to lay siege" loops after siege victories

**Related Fix - Save Load:**
- When loading a save during a siege, `MobileParty.MainParty.BesiegerCamp` is restored from the save
- This causes `GetGenericStateMenu()` to return `menu_siege_strategies` on the first campaign tick
- Fixed by clearing `BesiegerCamp` in `SyncData()` immediately during load
- Enlisted soldiers don't command sieges - only the lord does

### Stale Encounter After Auto-Resolve Victory

**Scenario:** After army wins a battle via auto-resolve, the encounter menu stays open with only "Wait in Reserve" as an option, forcing the player to click it to proceed.

**Root Cause:**
- The native game creates an encounter when the battle starts
- When the battle auto-resolves (victory), the MapEvent ends but the PlayerEncounter may persist
- The encounter menu stays open because PlayerEncounter.Current != null
- The "Wait in Reserve" option was available because it only checked for MapEvent existence, not whether the battle was already over
- Player was forced to click "Wait in Reserve" to trigger cleanup, even though there was no battle to wait for

**The Problem Code Path:**
```
1. Army engages enemy → PlayerEncounter created → encounter menu opens
2. Battle auto-resolves → MapEvent.HasWinner = true (battle over)
3. BUT PlayerEncounter still exists → encounter menu stays open
4. IsWaitInReserveAvailable() checks: mapEvent != null? YES → shows option
5. Player clicks Wait in Reserve → triggers cleanup → returns to enlisted_status
```

**The Fix (EnlistedEncounterBehavior.IsWaitInReserveAvailable):**

Added early detection for already-ended battles:
```csharp
// CRITICAL: Check if battle is already over (auto-resolved)
if (mapEvent != null && (mapEvent.HasWinner || mapEvent.IsFinalized))
{
    ModLogger.Info("EncounterGuard", 
        $"WAIT_IN_RESERVE: Battle already ended (HasWinner={mapEvent.HasWinner}, " +
        $"IsFinalized={mapEvent.IsFinalized}) - triggering auto-cleanup");
    
    // Auto-cleanup instead of showing stale option
    TriggerPostBattleCleanup();
    return false;
}
```

**The TriggerPostBattleCleanup() Method:**

**CRITICAL FIX (2026-01-03):** Cleanup is now **DEFERRED to the next frame** to prevent race condition crashes:

```csharp
private void TriggerPostBattleCleanup()
{
    // Guard against duplicate cleanup scheduling
    if (_postBattleCleanupScheduled)
    {
        return;
    }
    
    _postBattleCleanupScheduled = true;
    
    // CRITICAL: Defer to next frame to avoid modifying state during menu condition evaluation
    // The crash occurs because we're called from GetConditionsHold() during menu rendering,
    // and modifying encounter state corrupts the menu refresh loop.
    NextFrameDispatcher.RunNextFrame(() =>
    {
        _postBattleCleanupScheduled = false;
        ExecutePostBattleCleanup();
    });
}

private void ExecutePostBattleCleanup()
{
    // 1. Finish the PlayerEncounter if it exists
    if (PlayerEncounter.Current != null)
    {
        PlayerEncounter.LeaveEncounter = true;
        if (PlayerEncounter.InsideSettlement)
            PlayerEncounter.LeaveSettlement();
        PlayerEncounter.Finish();
    }
    
    // 2. Deactivate player party (prevent further stale encounters)
    if (mainParty != null && enlistment?.IsEnlisted == true)
    {
        mainParty.IsActive = false;
        mainParty.IsVisible = false;
    }
    
    // 3. Clear reserve state flag
    ClearReserveState();
    
    // 4. Return to appropriate menu
    if (mainParty?.Army != null && mainParty.Army.LeaderParty != mainParty)
        GameMenu.SwitchToMenu("army_wait");
    else
        GameMenu.SwitchToMenu("enlisted_status");
}
```

**Fixed Code Path:**
```
1. Army engages enemy → PlayerEncounter created → encounter menu opens
2. Battle auto-resolves → MapEvent.HasWinner = true (battle over)
3. IsWaitInReserveAvailable() checks: mapEvent.HasWinner? YES → schedules cleanup for next frame
4. Menu rendering completes safely → next frame → TriggerPostBattleCleanup() executes
5. ExecutePostBattleCleanup() runs → PlayerEncounter finished → party deactivated
6. Menu switches to enlisted_status automatically → player never sees stale menu
```

**Log Pattern (successful auto-cleanup):**
```
[EncounterGuard] WAIT_IN_RESERVE: Battle already ended (HasWinner=True, IsFinalized=False, WinningSide=Attacker) - triggering auto-cleanup
[EncounterGuard] AUTO-CLEANUP: Scheduling deferred cleanup for next frame (currentMenu=encounter, hasEncounter=True, mapEventId=12345678)
[EncounterGuard] AUTO-CLEANUP: Executing deferred cleanup (delay=0.016s, originalMenu=encounter, currentMenu=encounter, hasEncounter=True)
[EncounterGuard] AUTO-CLEANUP: PlayerEncounter finished
[EncounterGuard] AUTO-CLEANUP: Deactivated party
[EncounterGuard] AUTO-CLEANUP: Switched to enlisted_status menu
```

**Why Deferring Is Critical:**

The original synchronous cleanup caused a **race condition crash** in native code:

**Crash Stack Trace:**
```
MenuHelper.CheckEnemyAttackableHonorably ← CRASH (NullReferenceException)
game_menu_encounter_attack_on_condition ← Evaluating "Attack" option
GameMenuOption.GetConditionsHold ← Checking all option conditions
GameMenuVM.Refresh ← Refreshing menu during render
MapScreen.OnActivate ← Returning from battle
```

**The Race Condition:**
1. Menu rendering begins, evaluating option conditions
2. "Wait in Reserve" condition check detects battle ended
3. `TriggerPostBattleCleanup()` called **SYNCHRONOUSLY** during condition evaluation
4. `PlayerEncounter.Finish()` modifies encounter state
5. Menu continues evaluating other options ("Attack", "Send Troops")
6. `CheckEnemyAttackableHonorably()` tries to access now-corrupted encounter state
7. **CRASH** - NullReferenceException in native code

**The Fix:**
Deferring cleanup to the next frame ensures:
- Menu condition evaluation completes with stable state
- All menu options are evaluated with consistent encounter data
- State modification happens AFTER rendering completes
- No corruption during the menu refresh loop

**Diagnostic Logging:**

Added comprehensive diagnostics to detect future race conditions:

```
[EncounterGuard] AUTO-CLEANUP: Scheduling deferred cleanup (currentMenu=X, mapEventId=Y)
[EncounterGuard] AUTO-CLEANUP: Executing deferred cleanup (delay=Xs)
[EncounterGuard] AUTO-CLEANUP: MapEvent changed during defer (was=X, now=Y)
```

**What to Look For If Crash Happens Again:**

1. ✅ **Should see**: `"AUTO-CLEANUP: Scheduling deferred cleanup"` 
2. ❌ **Should NOT see**: `"Executing deferred cleanup"` with delay < 0.001s (would mean NextFrameDispatcher bug)
3. ❌ **Should NOT see**: Multiple schedules with same mapEventId within < 0.1s (race condition)
4. ❌ **Should NOT see**: OnMapEventEnded logs interleaved without time gap (simultaneous execution)

**Key Insight - Encounter Creation Path:**

This fix also revealed an important technical detail: **stale encounters after auto-resolve are NOT created through `EncounterManager.StartPartyEncounter`**. The `EncounterSuppressionPatch` (which patches `StartPartyEncounter`) never logged any `ENCOUNTER ATTEMPT` messages for these stale encounters. Instead, the encounter persists from the original battle creation and simply isn't cleaned up properly by native code.

This is why the fix is in `IsWaitInReserveAvailable()` (detection) rather than `EncounterSuppressionPatch` (prevention) - there's no new encounter being created to block.

### Consecutive Battles While in Reserve

**Scenario:** Player is waiting in reserve when army engages in another battle

**Handling:**
- `OnMapEventStarted` checks `IsWaitingInReserve` flag before processing
- If true, skips all battle processing (menu switching, teleport, army setup)
- `GenericStateMenuPatch` ensures native menu calls return `enlisted_battle_wait` instead of `army_wait`
- Player remains in `enlisted_battle_wait` menu until army finishes fighting
- Prevents duplicate XP awards and menu flickering during rapid consecutive battles
- During sieges: sally out battles won't kick player out of reserve (only actual assaults do)

### Rejoining Battle from Reserve

**Scenario:** Player clicks "Rejoin Battle" while waiting in reserve

**Handling:**
- `IsWaitingInReserve` flag cleared immediately
- Player party re-added to lord's MapEvent on same side (`playerParty.MapEventSide = lordSide`)
- Encounter menu activated via `GameMenu.ActivateGameMenu("encounter")`
- Player can then choose to Attack, Send Troops, or Leave
- If battle ended while waiting, player exits to appropriate post-battle menu

### Arriving at Siege Location Without Active Battle

**Scenario:** Enlisted player arrives at a siege location where their lord is besieging, but no battle is currently happening (just the siege camp)

**Handling:**
- Player party kept `IsActive = false` during siege waiting phase
- Native game doesn't create encounters for inactive parties
- Player INITIALLY sees `enlisted_status` menu
- **Tick system detects siege:** `OnEnlistedStatusTick` calls `GetGenericStateMenu()` every frame
- `GenericStateMenuPatch` detects `lordInSiege=true` → allows native `"menu_siege_strategies"` to return
- `OnEnlistedStatusTick` sees native wants different menu → switches immediately
- Player now sees `menu_siege_strategies` with army member options ("Leave Army")
- When assault starts, `OnMapEventStarted` detects battle → activates party → player auto-joins

**Expected Flow:**
1. Lord's army arrives at enemy castle → siege camp established
2. Enlisted player party remains `IsActive = false` 
3. Player initially sees `enlisted_status` menu (default state)
4. **Within 1-2 frames:** Tick handler detects siege → switches to `menu_siege_strategies`
5. Player can interact with siege (see progress, leave army if desired)
6. Lord initiates assault → `MapEvent` created → `IsActive = true` → player auto-joins
7. Menu switches to `encounter` (Attack/Send Troops/Wait in Reserve)

**Log Pattern:**
```
[SiegeIntegration] Synced player besieger camp with lord's siege at Hvalvik
[Menu] OnEnlistedStatusTick: Native wants 'menu_siege_strategies' - yielding to native menu system
[Menu] GenericStateMenuPatch: Allowing native menu 'menu_siege_strategies' during siege
```

**Why This Is Correct:**

Army members at sieges should see `menu_siege_strategies`, not `enlisted_status`. The native menu:
- Shows siege progress (construction, bombardment, etc.)
- Displays correct army member options
- Integrates with native siege system
- Updates in real-time as siege progresses

The mod no longer "keeps them in enlisted_status" during sieges - it yields to the native siege menu system.

### Sally Out Battle Menu Switching During Reserve

**Scenario:** Player clicks "Wait in Reserve" during a sally out battle (defenders attacking), but is immediately kicked out and sees the siege preparation menu

**Root Cause:**
- Sally out battles occur when defenders leave the castle to attack besiegers
- These battles have both `SiegeEvent` (ongoing siege) and `MapEvent` (the sally out battle)
- Original detection logic treated ANY battle during a siege as a "siege assault"
- This incorrectly forced players out of reserve during optional sally out battles

**Handling:**
- `EnlistedEncounterBehavior` tick handler now checks `mapEvent.IsSiegeAssault` specifically
- Sally out battles: `IsSiegeAssault == false` → player stays in reserve ✅
- Actual siege assaults (attacking walls): `IsSiegeAssault == true` → player forced to participate ✅
- This allows players to sit out defensive battles while still participating in wall assaults
- Log pattern when working correctly: "Siege preparation detected - staying in reserve menu"

### Lingering PlayerEncounter After Discharge

**Scenario:** Service ends (lord captured, player captured, discharge) but PlayerEncounter remains without a MapEvent

**Handling:**
- `PostDischargeProtectionPatch` blocks party activation while `PlayerEncounter.Current != null`
- This prevents crashes from "Attack or Surrender" encounters created immediately after discharge
- `SchedulePostEncounterVisibilityRestore()` queues visibility restoration
- `RestoreVisibilityAfterEncounter()` waits until PlayerEncounter clears
- **Watchdog timeout**: If encounter persists >5 seconds with no MapEvent and player is discharged (not enlisted, not prisoner), `ForceFinishLingeringEncounter()` clears it
- This prevents permanent stuck state when encounter cleanup fails

**Log Pattern:**
```
[Enlistment] Player in vulnerable state when ending service (MapEvent: False, Encounter: True, Prisoner: False) - deferring activation
[PostDischargeProtection] Prevented party activation - discharged but still in battle state (MapEvent: False, Encounter: True)
[EncounterCleanup] Force finishing lingering PlayerEncounter after discharge (requested at {time})
[Enlistment] Party visibility restored after encounter cleanup
```

### Lord Death While Enlisted

**Scenario:** Lord dies while player is enlisted

**Handling:**
- Encounter safety disabled before retirement processing
- Prevents crashes during emergency service termination
- Grace period begins normally
- State cleanup happens automatically
- Naval safety: if the player is at sea without ships when the lord dies (or any service-ending event), the system always teleports the player to the nearest port before continuing grace/retirement so they cannot get stranded on water.

### Battle Defeat (Autosim)

**Scenario:** Lord's army loses in autosim

**Handling:**
- Player sees encounter menu (Attack/Surrender) after report
- Standard native behavior
- Player resolves outcome manually
- No special handling needed

### Player Captured (Prisoner State)

**Scenario:** Player is taken prisoner after battle defeat or lord capture

**Handling:**
- Mod detects capture via `OnHeroPrisonerTaken` event
- Service ends with grace period (14 days to rejoin same kingdom after escape)
- All map event processing skipped while `Hero.MainHero.IsPrisoner == true`
- Logs show: `Skipping MapEventEnded - player prisoner or cleanup pending`
- Native captivity system takes full control of player state

**Critical - Party Activation During Captivity:**
- Native `PlayerCaptivity.StartCaptivity()` sets `MobileParty.MainParty.IsActive = false`
- `StopEnlist()` now checks `Hero.MainHero.IsPrisoner` before activating the party
- If prisoner, party stays deactivated; `SchedulePostEncounterVisibilityRestore()` waits for captivity to end
- `RestoreVisibilityAfterEncounter()` also checks prisoner state and defers until released
- This prevents the mod from fighting native captivity's party state management

**Important - Prisoner Transfers (Base Game Behavior):**
- While imprisoned, the base game's `TransferPrisonerBarterBehavior` remains active
- When enemy lords meet and interact (dialog/barter), they can trade prisoners
- This includes transferring the player to a different captor
- This is **expected native behavior**, not a mod bug
- The mod correctly stays inactive during captivity, letting native mechanics operate
- Grace period remains valid regardless of which enemy holds the player

**Log Pattern:**
```
[EventSafety] Lord {name} captured - starting grace period
[Battle] Encounter active during lord capture - letting native surrender capture handle the player.
[Enlistment] Service ended: Lord captured (Honorable: False)
[Enlistment] Player in vulnerable state when ending service (MapEvent: False, Encounter: False, Prisoner: True) - deferring activation
[EventSafety] Skipping MapEventEnded - player prisoner or cleanup pending (IsPrisoner=True, CaptureCleanupScheduled=False)
```

### Lord Captured While Player Active

**Scenario:** Lord is captured while player is still fighting or in encounter

**Handling:**
- `TryCapturePlayerAlongsideLord()` checks if player should be captured too
- If player already in encounter (surrender screen), native flow handles capture
- If player was in reserve, reserve state is cleared and player is teleported to safety with protection
- Grace period starts for the kingdom the player was serving
- Cleanup deferred via `SchedulePlayerCaptureCleanup()` until encounter closes

### Lord's Party Disbanded While Player in Reserve

**Scenario:** Lord's party disbands (not captured) while player is waiting in reserve during battle

**Handling:**
- `StopEnlist()` now clears `IsWaitingInReserve` immediately when service ends for ANY reason
- Harmony patches check `IsEnlisted` alongside `IsWaitingInReserve` before blocking
- If reserve flag is stale (not enlisted), patches clear it automatically
- This prevents player getting stuck with invisible/inactive party after party disband
- Player receives grace period and is restored to normal map state

**Log Pattern (when fix triggers):**
```
[Enlistment] Lord's party disbanded (Lord: {name}, Kingdom: {kingdom}) - starting grace period
[Battle] Clearing reserve state during service end
[Enlistment] Party activated and made visible (no active battle state)
```

**Log Pattern (stale flag cleared by patches):**
```
[PostDischargeProtection] Clearing stale reserve flag during activation
```

---

## Special Scenarios: Captivity & Village Raids

### Player Captured During Battle

**Scenario:** Enlisted player is defeated in battle and captured by enemy

**Native Game Behavior:**
1. `TakePrisonerAction.Apply()` called on player hero
2. `PlayerCaptivity.StartCaptivity()` executes:
   - Exits encounter (`PlayerEncounter.LeaveEncounter = true`)
   - Disables main party (`MobileParty.MainParty.IsActive = false`)
   - **Removes from army** (`MobileParty.MainParty.Army = null`)
   - Camera follows captor party
3. Player travels with captors until released

**Enlisted Mod Impact:**
- ✅ Party already hidden via encounter safety (no conflict)
- ❌ **Enlistment automatically ended** (army membership removed)
- ❌ **No tracking of pre-captivity enlistment state**
- ❌ **No auto-return to lord after release**

**Current State:**
- System works, but player loses enlistment
- After captivity ends, player is independent again
- Must manually re-enlist if desired
- No special enlisted captivity events

**Potential Improvements:**
1. **Track enlistment before capture** - Save lord and rank to mod state
2. **Post-release dialog** - Offer choice: "Return to lord?" vs "Go independent"
3. **Lord ransom attempts** - Friendly lord may pay ransom sooner
4. **Enlisted captivity events** - Special decisions/consequences during captivity

**Captivity Flow (13 menus):**
```
Captured → menu_captivity_castle_taken_prisoner
   ↓
[Multiple paths to release:]
- Wilderness escape
- Ransom offer (wilderness or prison)
- Transfer to settlement dungeon
- Prison escape
- Captor defeated/destroyed
- Ally pays ransom
- Prisoner exchange
- Escape during captor's battle
- Released after battle
- (Rare) Execution if relation < -30
   ↓
Released → EndCaptivityInternal()
   - Party reactivated (IsActive = true)
   - Camera returns to player
   - Army membership NOT restored
```

See: `docs/Reference/battle-system-complete-analysis.md` Part 6B for complete captivity flow.

---

### Lord Raids Village While Player Enlisted

**Scenario:** Army lord initiates village raid with enlisted player in army

**Native Game Behavior:**
1. Lord approaches village and starts raid
2. Creates `RaidEventComponent` on settlement
3. Opens `raiding_village` menu (wait menu) for all army members
4. Raid progresses via tick handler (village HP decreases)
5. On completion:
   - If player is raid leader → `village_player_raid_ended`
   - If player is army member → `village_raid_ended_leaded_by_someone_else`
6. Loot distributed via army system

**Enlisted Mod Handling:**
- ✅ Works correctly with current patches
- ✅ `AbandonArmyBlockPatch` hides "Abandon army" button on `raiding_village`
- ✅ Player participates passively as army member
- ✅ Correct post-raid menu appears
- ✅ Returns to normal army operations after

**Menu Options During Raid:**
- "End Raiding" → Hidden (not raid leader)
- "Leave Army" → Available (player can abandon mission)
- "Abandon Army" → **Blocked by enlisted mod**

**No changes needed** - Village raid system works as designed.

**Village Raid Flow (10 menus):**
```
Village Menu → "Take a hostile action"
   ↓
village_hostile_action (choose type)
   ├─ Raid village
   │  ├─ If at peace → raid_village_no_resist_warn_player
   │  └─ raiding_village (wait menu, progress bar)
   │     └─ Completes → village_player_raid_ended OR village_raid_ended_leaded_by_someone_else
   │
   ├─ Force supplies
   │  └─ force_supplies_village_resist_warn_player → force_supplies_village
   │
   └─ Force volunteers
      └─ force_troops_village_resist_warn_player → force_volunteers_village
```

See: `docs/Reference/battle-system-complete-analysis.md` Part 6C for complete raid flow.

---

## API Reference

### EncounterGuard Methods

```csharp
// Initialize the encounter guard system (currently a placeholder)
public static void Initialize()

// Safely leave current encounter and activate enlisted menu
// Uses NextFrameDispatcher to defer operations and prevent timing conflicts
internal static void TryLeaveEncounter()

// Attach player party to lord using escort AI for following
// Uses SetMoveEscortParty instead of AttachedTo to avoid army requirement crashes
public static void TryAttachOrEscort(Hero lord)

// Hide/show player party 3D model on map (separate from nameplate)
public static void HidePlayerPartyVisual()  // Sets IsVisible = false
public static void ShowPlayerPartyVisual()  // Sets IsVisible = true
```

### Encounter State (Direct Property Access)

```csharp
// The system controls encounters by setting these properties directly:
MobileParty.MainParty.IsActive = false;   // Disables encounters
MobileParty.MainParty.IsActive = true;    // Enables encounters
MobileParty.MainParty.IsVisible = false;  // Hides party on map

// Check party state
bool encountersDisabled = !MobileParty.MainParty.IsActive;
bool partyHidden = !MobileParty.MainParty.IsVisible;
```

### Battle Detection

```csharp
// Check if lord is in battle
bool IsLordInBattle()
{
    var lord = EnlistmentBehavior.Instance?.CurrentLord;
    return lord?.PartyBelongedTo?.MapEvent != null;
}

// Get lord's MapEvent
MapEvent GetLordMapEvent()
{
    var lord = EnlistmentBehavior.Instance?.CurrentLord;
    return lord?.PartyBelongedTo?.MapEvent;
}
```

### Party Visibility

```csharp
// Hide party nameplate
// Handled by HidePartyNamePlatePatch
// Suppresses UI display of party nameplate

// Check party visibility state
bool IsPartyVisible()
{
    return MobileParty.MainParty.IsVisible;
}

// Check party active state
bool IsPartyActive()
{
    return MobileParty.MainParty.IsActive;
}
```

### Battle Participation

```csharp
// Activate party for battle (while keeping hidden)
void ActivateForBattle()
{
    // Set IsActive = true for battle participation
    // Keep IsVisible = false for visual hiding
    MobileParty.MainParty.IsActive = true;
}

// Deactivate after battle
void DeactivateAfterBattle()
{
    // Return to inactive state during peace
    MobileParty.MainParty.IsActive = false;
}
```

---

## Debugging

**Log Categories:**
- `"EncounterGuard"` - State management operations
- `"ENLISTMENT"` - Service state changes that affect encounters
- `"BATTLE"` - Battle join logic and vanilla encounter coordination
- `"EVENTSAFETY"` - Capture/prisoner handling and map event skipping
- `"MENU"` - Menu transitions and native menu system integration
- `"SiegeIntegration"` - Siege-specific state sync and participation

**Key Log Points:**
```csharp
// State changes
ModLogger.Info("EncounterGuard", $"Encounters disabled (IsActive = false)");
ModLogger.Info("EncounterGuard", $"Encounters enabled (IsActive = true)");

// Battle detection
ModLogger.Info("BATTLE", $"Lord entered battle: {mapEvent}");
ModLogger.Debug("BATTLE", $"Player party activated for battle participation");

// State validation
ModLogger.Warn("EncounterGuard", $"Unexpected IsActive state: {isActive}, expected: {expected}");
ModLogger.Debug("EncounterGuard", $"State reset to expected value");

// Stale encounter auto-cleanup (post-victory)
ModLogger.Info("EncounterGuard", $"WAIT_IN_RESERVE: Battle already ended (HasWinner=..., IsFinalized=...) - triggering auto-cleanup");
ModLogger.Info("EncounterGuard", $"AUTO-CLEANUP: Cleaning up stale post-battle encounter");
ModLogger.Info("EncounterGuard", $"AUTO-CLEANUP: PlayerEncounter finished");
ModLogger.Info("EncounterGuard", $"AUTO-CLEANUP: Deactivated party");
ModLogger.Info("EncounterGuard", $"AUTO-CLEANUP: Switched to enlisted_status/army_wait menu");

// Capture/prisoner handling
ModLogger.Info("EVENTSAFETY", $"Lord {name} captured - starting grace period");
ModLogger.Info("EVENTSAFETY", $"Player captured - deferring enlistment teardown until encounter closes");
ModLogger.Info("EVENTSAFETY", $"Skipping MapEventEnded - player prisoner or cleanup pending");
ModLogger.Info("EVENTSAFETY", $"Finalizing deferred capture cleanup for player");
ModLogger.Info("BATTLE", $"Encounter active during lord capture - letting native surrender capture handle the player.");

// Menu transitions and native integration
ModLogger.Info("MENU", $"OnEnlistedStatusTick: Native wants '{desiredMenu}' - yielding to native menu system");
ModLogger.Debug("MENU", $"GenericStateMenuPatch: Allowing native menu '{menuName}' during siege");
ModLogger.Info("MENU", $"GenericStateMenuPatch: {originalMenu} -> {newMenu} (keeping enlisted menu)");
ModLogger.Debug("MENU", $"GenericStateMenuPatch: Not overriding {menu} - player explicitly visited settlement");

// Siege integration
ModLogger.Info("SiegeIntegration", $"Synced player besieger camp with lord's siege at {settlement}");
ModLogger.Warn("SiegeIntegration", $"Integration may be broken - player not in Army.Parties");
ModLogger.Warn("SiegeIntegration", $"Integration may be broken - player not in BesiegerCamp._besiegerParties");
```

**Common Issues:**

**Encounters still happening:**
- Check `IsActive` is actually false
- Verify `EnlistmentBehavior.OnTick()` is setting `IsActive = false`
- Check for conflicting systems changing party state
- Review logs for state validation warnings

**Not joining battles:**
- Check `MapEvent` detection logic in `EnlistmentBehavior`
- Verify battle detection is working correctly
- Check if player party is in same army as lord
- Review battle participation logs

**"Help or Leave" menu appearing:**
- Fixed by `JoinEncounterAutoSelectPatch`
- If still appearing, check patch is registered in logs
- Verify patch is applied correctly
- Check for patch conflicts with other mods

**"Attack or Surrender" after autosim defeat:**
- Expected outcome when lord loses
- Player should resolve encounter manually (Attack/Surrender)
- Matches native behavior
- Not a bug - working as designed

**enlisted_status menu stays open during siege (doesn't switch to siege menu):**
- **Cause:** `OnEnlistedStatusTick` not calling `GetGenericStateMenu()` to check for menu changes
- **Fix:** Tick handler now checks every frame and yields to native when appropriate
- **Expected log:** `OnEnlistedStatusTick: Native wants 'menu_siege_strategies' - yielding to native menu system`
- **If not switching:** Check if `GenericStateMenuPatch` is detecting `lordInSiege` correctly
- **Debug:** Enable Debug level for "Menu" category in settings.json
- **Note:** Muster callbacks can "kick" the menu system into re-evaluating, which is why catching a muster mid-siege would show the correct menu

**Siege menu appearing when it shouldn't (not at siege):**
- Check if `lordInSiege` detection is too broad in `GenericStateMenuPatch`
- Verify `lordParty.BesiegerCamp`, `SiegeEvent`, and army leader are actually at a siege
- Should only appear when lord/army is ACTIVELY besieging
- Log should show: `Allowing native menu 'menu_siege_strategies' during siege`

**Post-victory menu loop (encounter/siege menu reopening):**
- After clicking "Done" on scoreboard, the encounter or siege menu reopens
- For SIEGES: Native now handles post-victory flow completely - mod doesn't interfere
- For FIELD BATTLES: `OnMapEventEnded` defers cleanup to next frame and returns to enlisted menu
- If still happening, check if `CleanupPostEncounterState` is being called inappropriately
- For save-load loops, check if `BesiegerCamp` is being cleared in `SyncData()`

**Stale encounter after auto-resolve (stuck on encounter menu with only Wait in Reserve):**
- After army wins via auto-resolve, encounter menu stays open instead of closing
- Look for: `WAIT_IN_RESERVE: Battle already ended (HasWinner=True) - triggering auto-cleanup`
- If this log appears, auto-cleanup should handle it and switch to `enlisted_status`
- If NOT appearing, the `IsWaitInReserveAvailable` check isn't detecting the ended battle
- Check `mapEvent.HasWinner` and `mapEvent.IsFinalized` values in logs
- If auto-cleanup fails, look for: `AUTO-CLEANUP failed:` error message
- The stale encounter is NOT created through `EncounterManager.StartPartyEncounter` - it persists from the original battle

**Crash in MenuHelper.CheckEnemyAttackableHonorably (NullReferenceException):**
- Crash occurs during menu rendering after battle ends
- **Root cause:** Race condition between cleanup and menu condition evaluation
- **Stack trace pattern:**
  ```
  MenuHelper.CheckEnemyAttackableHonorably ← CRASH
  game_menu_encounter_attack_on_condition
  GameMenuOption.GetConditionsHold ← During menu refresh
  GameMenuVM.Refresh
  MapScreen.OnActivate ← Returning from battle
  ```
- **Fix:** Cleanup is now deferred to next frame via `NextFrameDispatcher.RunNextFrame()`
- **Log pattern (healthy):**
  ```
  [EncounterGuard] AUTO-CLEANUP: Scheduling deferred cleanup for next frame
  [EncounterGuard] AUTO-CLEANUP: Executing deferred cleanup (delay=0.016s)
  ```
- **Red flags to look for:**
  - Delay < 0.001s means NextFrameDispatcher failed to defer (framework bug)
  - Multiple schedules with same mapEventId within < 0.1s (duplicate cleanup race)
  - OnMapEventEnded logs interleaved with AUTO-CLEANUP without time gap (simultaneous execution)
- **If crash persists:** Check if `TriggerPostBattleCleanup()` is being called from a different code path synchronously

**Reserve mode menu stutter (flickering between menus):**
- `GenericStateMenuPatch` should intercept `GetGenericStateMenu()` calls
- Verify patch is registered in `Conflicts-A_*.log`
- Check `IsWaitingInReserve` is true when in reserve
- If stutter persists, the patch may be missing from `Enlisted.csproj`

**Rejoin battle not working:**
- Log should show: "Player rejoining battle from reserve"
- Player must be re-added to MapEvent before encounter menu activates
- Check `lordParty.Party.MapEventSide` is not null
- Verify `GameMenu.ActivateGameMenu("encounter")` is called

**Wait in Reserve kicks player out during sally out battles:**
- Sally out battles are when defenders leave the castle to attack besiegers
- These should be treated as optional field battles, not siege assaults
- Check log for: "Siege assault started - cleared reserve state"
- If appearing during sally out, the detection logic is too broad
- Fix: Only exit reserve when `mapEvent.IsSiegeAssault == true` (actual wall assault)
- Sally outs have `MapEvent` but `IsSiegeAssault == false`

**Party state not persisting:**
- Check state enforcement in `OnTick()`
- Verify state is being reset continuously
- Check for save/load issues
- Review state validation logs

**Repeated "Skipping MapEventEnded" logs while prisoner:**
- This is **expected behavior** when `IsPrisoner=True`
- The mod correctly skips all map event processing during captivity
- Native captivity system handles all prisoner mechanics
- Grace period remains active for rejoining after escape
- Log pattern: `Skipping MapEventEnded - player prisoner or cleanup pending (IsPrisoner=True, CaptureCleanupScheduled=False)`

**Player transferred to different captor:**
- This is **base game behavior**, not a mod bug
- Native `TransferPrisonerBarterBehavior` allows lords to trade prisoners
- When enemy lords meet/dialog, they can exchange prisoners including the player
- The mod correctly stays inactive during captivity
- Grace period is unaffected by captor changes

**Party stuck invisible/inactive after discharge:**
- Check if PlayerEncounter is lingering: log shows `Prevented party activation - discharged but still in battle state (Encounter: True)`
- Watchdog should force-finish after 5 seconds: `Force finishing lingering PlayerEncounter after discharge`
- If still stuck, check if player is prisoner (`IsPrisoner=True`) - native captivity owns state until release
- Log pattern when prisoner blocks activation: `Player in vulnerable state when ending service (Prisoner: True) - deferring activation`
- If stuck after party disband while in reserve, check log for `Clearing reserve state during service end` - this should clear the flag
- If patches see stale reserve flag, they auto-clear it: `Clearing stale reserve flag during activation`

**Crash in PlayerEncounter.Finish() after siege battle (NullReferenceException):**
- This was caused by a race condition between our mod and native AI
- After siege battles, native `AiPartyThinkBehavior.PartyHourlyAiTick` calls `PlayerEncounter.Finish()` when parties change behavior
- If our deferred cleanup ran at the same time, internal state became inconsistent
- Fixed by: (1) finishing siege encounters immediately instead of deferring, (2) `PlayerEncounterFinishSafetyPatch` catches and recovers from crashes
- Log pattern on crash recovery: `Recovering from NullReferenceException in Finish - cleaning up encounter state`
- Log pattern showing siege immediate cleanup: `Siege battle ended - finishing encounter immediately to avoid native AI race`
- Debug context shows caller: `NativeAI` = native system, `EnlistmentBattle` = our OnMapEventEnded
- Rapid call detection: `Rapid Finish calls detected: Xms apart` indicates race condition was occurring

**"Party activated and made visible" but player is prisoner:**
- This was a bug (now fixed) - StopEnlist didn't check prisoner state before activating
- Current code checks `playerIsPrisoner` alongside MapEvent/Encounter before deciding to activate
- If you see this log while prisoner, ensure you have the latest code

**Naval Battle Crash Debugging:**

If you see crashes in naval battles with stack traces containing:
- `NavalDLC.GameComponents.NavalDLCShipDeploymentModel.GetSuitablePlayerShip` -> Patch 1 should handle this
- `NavalDLC.GameComponents.NavalDLCShipDeploymentModel.GetOrderedCaptainsForPlayerTeamShips` -> Patch 2 should handle this
- `NavalDLC.Missions.MissionLogics.NavalAgentsLogic.AddReservedTroopToShip` -> Patch 3 should handle this
- `NavalDLC.Missions.AI.Behaviors.BehaviorNavalEngageCorrespondingEnemy..ctor` -> Patch 4 should handle this
- `NavalDLC.Missions.MissionLogics.NavalTeamAgents.OnShipRemoved` -> Patch 5 should handle this
- `TaleWorlds.MountAndBlade.BattleObserverMissionLogic.OnAgentRemoved` (during naval cleanup) -> Patch 5 handles indirectly

Log messages to look for:
```
[Naval] Naval enlisted crew fix registered (GetSuitablePlayerShip)
[Naval] Naval captain assignment fix registered
[Naval] Naval troop allocation fix registered
[Naval] Naval TeamAI formation patch registered
[Naval] Naval OnShipRemoved safety fix registered
```

If formation crash persists:
- Enable Debug level for "Naval" category in `settings.json`
- Check if `Skipping naval behaviors for Formation X - no ship assigned` appears
- If not appearing, the patch isn't detecting the shipless formation
- Check `GetShip` reflection is working (look for method lookup errors)

If mission cleanup crash persists (crash after battle ends):
- Look for `OnShipRemoved: Handling cleanup` in logs
- Check if cleanup completes: `OnShipRemoved: Cleanup complete`
- Common cause: agent with null Team being passed to FadeOut
- If falling back to original: "OnShipRemoved error" will show the exception

**Settlement wait menu disappears when clicking "Wait here for some time":**
- After clicking "Wait here for some time" in a town/castle, the menu disappears and kicks player out
- **Root cause:** Clicking the native wait option fires `OnSettlementLeftEvent`, and when an **unrelated battle ends** elsewhere on the map, `CleanupPostEncounterState("OnMapEventEnded-Unrelated")` finishes the `PlayerEncounter` even though the player is still inside the settlement
- **Log pattern:**
  ```
  [MenuGuard] NATIVE MENU REQUEST: 'town_wait_menus' | ...
  [MenuGuard] ALLOWING native menu 'town_wait_menus' - player explicitly visited settlement
  [Settlement] Player left Ostican (town_V8)
  [EncounterCleanup] CleanupPostEncounterState(OnMapEventEnded-Unrelated): Finished PlayerEncounter
  ```
- **Fix (2026-01-04):** `CleanupPostEncounterState` now checks if player is inside a settlement before finishing the encounter:
  ```csharp
  var isPlayerInSettlement = PlayerEncounter.InsideSettlement ||
                             Features.Interface.Behaviors.EnlistedMenuBehavior.HasExplicitlyVisitedSettlement;
  
  if (encounter != null && (encounterBattle == null || encounterHasWinner) && !isPlayerInSettlement)
  {
      // Only finish encounter if player is NOT in a settlement
      PlayerEncounter.Finish();
  }
  ```
- **Location:** `EnlistmentBehavior.cs` line ~13327 in `CleanupPostEncounterState()`
- This prevents the cleanup from kicking players out of settlements when they're using native wait menus, browsing shops, or engaging in any other settlement activity

**Key Technical Details:**

*Constructor prefix limitation:* You CANNOT prevent object creation by returning `false` from a Harmony prefix on a constructor - the object is already allocated, only the constructor body is skipped. This leaves the object uninitialized and it will crash when used. Always patch the CALLER instead.

*Method overload ambiguity:* When using `AccessTools.Method` for Naval DLC internals, always specify parameter types. `DequeueReservedTroop` has 2 overloads - use `AccessTools.Method(type, "DequeueReservedTroop", new[] { navalShipAgentsType })` to avoid "Ambiguous match" exceptions that silently break patches.

**Debug Output Location:**
- `Modules/Enlisted/Debugging/Session-A_*.log`

**Related Files:**
- `src/Features/Enlistment/Behaviors/EncounterGuard.cs` - Static utility for encounter state management
- `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` - Service state, battle detection, siege sync
- `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` - enlisted_status menu with native-yielding tick handler
- `src/Features/Combat/Behaviors/EnlistedEncounterBehavior.cs` - Wait in Reserve menu, battle wait handling, and stale encounter auto-cleanup
- `src/Mod.GameAdapters/Patches/HidePartyNamePlatePatch.cs` - UI suppression for party nameplate
- `src/Mod.GameAdapters/Patches/JoinEncounterAutoSelectPatch.cs` - Auto-joins lord's battles
- `src/Mod.GameAdapters/Patches/GenericStateMenuPatch.cs` - Controls menu override logic, allows native menus when appropriate
- `src/Mod.GameAdapters/Patches/AbandonArmyBlockPatch.cs` - Removes "Abandon army" options while enlisted
- `src/Mod.GameAdapters/Patches/EncounterLeaveSuppressionPatch.cs` - Hides "Leave..." option from encounter menu
- `src/Mod.GameAdapters/Patches/EncounterSuppressionPatch.cs` - Blocks unwanted encounters, clears stale flags
- `src/Mod.GameAdapters/Patches/PostDischargeProtectionPatch.cs` - Prevents activation in vulnerable states
- `src/Mod.GameAdapters/Patches/VisibilityEnforcementPatch.cs` - Controls party visibility
- `src/Mod.GameAdapters/Patches/NavalBattleShipAssignmentPatch.cs` - Fixes naval battle crashes
- `src/Mod.GameAdapters/Patches/PlayerEncounterFinishSafetyPatch.cs` - Crash protection for siege cleanup
- `src/Mod.GameAdapters/Patches/RaftStateSuppressionPatch.cs` - Prevents "stranded at sea" menu
- `src/Mod.GameAdapters/Patches/NavalNavigationCapabilityPatch.cs` - Inherits naval capability from lord/army to allow sea travel

---

## Related Documentation

- [Enlistment System](../Core/enlistment.md) - Service state management
- [Formation Assignment](../Combat/formation-assignment.md) - Battle formation assignment details
