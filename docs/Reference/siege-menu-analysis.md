# Native Siege Menu System Analysis

**Date:** 2026-01-02  
**Status:** Reference documentation  
**Purpose:** Comprehensive analysis of native siege menu behavior to guide correct handling of siege encounters for enlisted soldiers

## Menu Flow Diagram

### Key Menus

1. **`join_siege_event`** - Appears when arriving at a siege location
2. **`menu_siege_strategies`** - Siege command menu (for commanders)
3. **`encounter`** - Active battle menu (Attack/Send Troops/Surrender)
4. **`join_encounter`** - Help attacker/defender choice
5. **`army_wait`** - Waiting with army (no battle)
6. **`army_wait_at_settlement`** - Waiting with army at settlement

---

## `join_siege_event` Menu

**When it appears** (`GetEncounterMenu`, lines 60-66):
- Settlement has a siege (`settlement.Party.SiegeEvent != null`)
- Player is NOT the siege commander
- `MobileParty.MainParty.BesiegedSettlement == null`
- `MobileParty.MainParty.CurrentSettlement == null`
- **NO active battle** (otherwise shows `join_encounter`)

**Menu options:**
1. **"Join the continuing siege"** (`join_siege_event` option)
2. **"Assault the siege camp"** (`attack_besiegers`) - help defenders
3. **"Break in to help the defenders"** (`join_siege_event_break_in`)
4. **"Don't get involved"** (`join_encounter_leave`)

**What "Join the continuing siege" does** (`game_menu_join_siege_event_on_consequence`, lines 738-762):

If **there IS a battle**:
```csharp
PlayerEncounter.JoinBattle(Settlement.CurrentSettlement.Party.MapEvent.IsSallyOut ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
GameMenu.SwitchToMenu("encounter");
```

If **NO battle**:
```csharp
PlayerEncounter.Finish();
MobileParty.MainParty.BesiegerCamp = currentSettlement.SiegeEvent.BesiegerCamp;
PlayerSiege.StartPlayerSiege(BattleSideEnum.Attacker, settlement: currentSettlement);
PlayerSiege.StartSiegePreparation();
Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppablePlay;
```

**This makes the player the siege commander!**

---

## `menu_siege_strategies` Menu

**When it appears** (`GetGenericStateMenu`, line 146):
- `MobileParty.MainParty.BesiegerCamp != null` (player is besieging)

OR (line 155):
- In army (`mainParty.AttachedTo != null`)
- Army is at a settlement under siege

**Menu options:**
- "Lead an assault" (only if you're the commander)
- "Send troops" (only if you're the commander)
- "Request a parley" (defenders only)
- "Leave" (if you're the commander)
- "Leave Army" (if in army but not commander)

---

## Army Member Behavior During Sieges

From `GetGenericStateMenu` (lines 147-156):

```csharp
if (mainParty.AttachedTo != null)
{
    // In army
    if (mainParty.AttachedTo.CurrentSettlement == null || !mainParty.AttachedTo.CurrentSettlement.IsUnderSiege)
        return "army_wait";  // Army traveling or at non-siege location
    
    return PlayerEncounter.Current != null && PlayerEncounter.Current.IsPlayerWaiting 
        ? "encounter_interrupted_siege_preparations" 
        : "menu_siege_strategies";  // Army at siege location
}
```

**Key insight:** Army members at a siege see `menu_siege_strategies`, NOT `join_siege_event`!

---

## Problem Analysis for Enlisted Mod

### Scenario 1: Enlisted in Lord's Army at Siege (No Battle)

**Native behavior:**
- Shows `menu_siege_strategies` (because `mainParty.AttachedTo != null`)
- Player can only "Leave Army" (not commander)
- When lord starts assault, player auto-joins via native army mechanics

**Enlisted mod interference:**
- None if player is properly in army
- **Problem:** If player is following via escort (not in army), they see `join_siege_event`

### Scenario 2: Enlisted Following Lord via Escort at Siege (No Battle)

**Native behavior:**
- Shows `join_siege_event` (because `mainParty.AttachedTo == null`)
- "Join the continuing siege" tries to make player the commander ❌

**Enlisted mod should:**
- Block the `join_siege_event` menu
- Return player to `enlisted_status` or custom wait menu
- Wait for lord to initiate assault
- Auto-join when battle starts

### Scenario 3: Enlisted in Army at Siege (Active Battle)

**Native behavior:**
- Shows `encounter` menu (Attack/Send Troops/Surrender)
- Player participates normally

**Enlisted mod:**
- Already handles this correctly via battle detection

### Scenario 4: Enlisted Following via Escort at Siege (Active Battle)

**Native behavior:**
- Shows `join_encounter` menu ("Help X / Don't get involved")
- Mod's `JoinSiegeEventAutoSelectPatch` intercepts and auto-joins

**Enlisted mod:**
- Working correctly when battle is active

---

## Root Cause of Bug

When an enlisted player following via escort arrives at a siege **without an active battle**:

1. Native shows `join_siege_event` menu
2. Player clicks "Join the continuing siege" (the "Attack!" button in your screenshot)
3. Native code tries to:
   - Set `MobileParty.MainParty.BesiegerCamp`
   - Call `PlayerSiege.StartPlayerSiege()` 
   - Make player the siege commander
4. This conflicts with enlisted state (party is hidden, in special mode)
5. Button doesn't work properly / causes issues

---

## Solution Design

### Option A: Close Encounter & Return to Enlisted Menu (CURRENT IMPLEMENTATION)

**Pros:**
- Simple and clean
- Clear that soldier doesn't make siege decisions
- Consistent with enlisted role

**Cons:**
- Breaks player's connection to siege location
- May feel jarring
- Requires lord to be at exact location for battles

### Option B: Join Army Silently

When `join_siege_event` appears for enlisted soldiers:
- Close the encounter
- **Add player to lord's army** (if not already)
- Show `menu_siege_strategies` as army member
- Player can only "Leave Army" option
- Auto-participate when lord starts assault

**Pros:**
- Uses native siege mechanics
- Seamless integration
- Army system handles battle participation

**Cons:**
- Requires army management
- May complicate escort-based following

### Option C: Custom Wait Menu During Siege Preparation

Create `enlisted_siege_wait` menu:
- Shows narrative about siege camp
- Options: "Wait for orders", "Talk to companions", "Rest"
- Monitor for lord's assault start
- Auto-join when battle begins

**Pros:**
- Immersive
- Clear soldier role
- Maintains mod's narrative style

**Cons:**
- Most complex
- Requires new menu infrastructure
- Need to handle all siege edge cases

---

## Recommendation

**Best solution: Option B (Join Army Silently) for army-based lords, Option A for escort-based**

**Implementation:**

1. In `JoinSiegeEventAutoSelectPatch`, when no battle exists:
   - Check if player is already in lord's army
   - If NOT in army: add player to army silently
   - Don't show `join_siege_event` menu
   - Let native system show `menu_siege_strategies`
   - Player sees only "Leave Army" option (not commander)
   - When lord starts assault: native army system handles participation

2. For escort-based following (no army):
   - Close encounter (current implementation)
   - Return to `enlisted_status`
   - Battle detection remains unchanged

This leverages native siege mechanics instead of fighting them.

---

---

## Critical Discovery: The Race Condition

### The Real Problem

When an enlisted player arrives at a siege location:

1. **Native encounter system activates FIRST** (happens in `EncounterManager.HandleEncounterForMobileParty`)
   - Checks if party should have an encounter
   - Creates encounter and shows menu
   - This happens BEFORE the hourly tick

2. **Enlisted mod joins army LATER** (happens in `OnRealtimeTick`)
   - Checks if lord is in army
   - Adds player to army via `mainParty.Army = targetArmy`
   - This happens AFTER the encounter is already created

**Result:** Player sees `join_siege_event` menu because `mainParty.AttachedTo == null` at the moment the menu is selected.

### Native Menu Selection Logic

From `DefaultEncounterGameMenuModel.GetGenericStateMenu()`:

```csharp
if (mainParty.AttachedTo != null)  // AttachedTo gets set when joining army
{
    if (mainParty.AttachedTo.CurrentSettlement == null || !mainParty.AttachedTo.CurrentSettlement.IsUnderSiege)
        return "army_wait";
    return "menu_siege_strategies";  // This is what army members see at sieges
}
```

**Key insight:** `AttachedTo` is ONLY set when `Army` is set. If player isn't in army yet, they're not "attached to" anything.

### Why Current Fix is Wrong

The current implementation in `JoinSiegeEventAutoSelectPatch` closes the encounter and returns to enlisted menu. But this:
- Fights against native siege mechanics
- Breaks if lord is commanding siege (player should participate)
- Doesn't leverage the army system that's already implemented

### The Correct Fix

**Do nothing in `JoinSiegeEventAutoSelectPatch` when there's no battle.**

Let the native system show whatever menu it wants. By the NEXT tick:
1. `OnRealtimeTick` will join player to army
2. `GetGenericStateMenu()` will see `mainParty.AttachedTo != null`
3. Native will switch to `menu_siege_strategies` automatically
4. Player sees only "Leave Army" option (not commander)
5. When lord starts assault, native army system handles participation

The `join_siege_event` menu will flash briefly, but that's better than breaking siege participation.

---

## Final Recommendation

**Revert the fix. Leave the "return true" in place when there's no active battle.**

The flash of `join_siege_event` is a timing issue, not a real bug. The native game will correct itself on the next tick when the player joins the army.

**Alternative:** If the flash is unacceptable, check if lord is in army and player isn't yet, then:
```csharp
if (battle == null)
{
    var mainParty = MobileParty.MainParty;
    var lordParty = lord?.PartyBelongedTo;
    
    // If lord is in army and player isn't yet, join now
    if (lordParty?.Army != null && (mainParty?.Army == null || mainParty.Army != lordParty.Army))
    {
        mainParty.Army = lordParty.Army;
        lordParty.Army.AddPartyToMergedParties(mainParty);
        // Now let native show menu_siege_strategies
        return true;
    }
    
    // Otherwise let native handle it
    return true;
}
```

This forces the army join to happen immediately, so native shows the correct menu.

---

## Testing Scenarios

1. ✅ Enlisted in army, arrive at siege (no battle) - Shows `menu_siege_strategies`, only "Leave Army"
2. ✅ Enlisted in army, lord starts assault - Native army system collects player
3. ⚠️ Enlisted not yet in army, arrive at siege (no battle) - Briefly shows `join_siege_event`, then switches to `menu_siege_strategies` on next tick
4. ✅ Enlisted via escort, lord starts assault - Battle detection joins player
5. ✅ Enlisted in army, sally out battle - Works via native army
6. ✅ Enlisted in army, siege already in progress - Army member participation
7. ✅ Lord captured during siege preparation - Service ends, cleanup runs
8. ✅ Army disbanded during siege preparation - Handled by discharge logic

---

## References

**Native files:**
- `EncounterGameMenuBehavior.cs` - Menu registration and handlers (line 146-150: join_siege_event menu)
- `DefaultEncounterGameMenuModel.cs` - Menu selection logic (line 147-156: army member menu selection)
- `EncounterManager.cs` - Encounter creation (line 38-51: encounter triggering)
- `SiegeEventCampaignBehavior.cs` - Siege-specific menus

**Mod files:**
- `JoinSiegeEventAutoSelectPatch.cs` - Intercepts siege arrival
- `EnlistmentBehavior.cs` - Army management (line 7440-7471) and battle detection
- `EncounterGuard.cs` - Encounter state utilities

**Key mod behavior:**
- Line 7917-7931: `TrySyncBesiegerCamp` actively PREVENTS setting `BesiegerCamp` on enlisted soldiers
- Line 7922 comment: "Native GetGenericStateMenu() returns menu_siege_strategies whenever MainParty.BesiegerCamp != null"
- This confirms the mod is designed to work WITH the army system, not against it
