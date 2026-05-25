# Captivity & Village Raid Systems - Complete Analysis

**Date:** 2026-01-02  
**Purpose:** Comprehensive documentation of captivity menus and village raid flow for enlisted mod

---

## PART 1: CAPTIVITY SYSTEM - When Player is Captured During Battle

### Captivity Initiation

**When player is captured (file: `TakePrisonerAction.cs:16-46`):**

```csharp
private static void ApplyInternal(PartyBase capturerParty, Hero prisonerCharacter, bool isEventCalled = true)
{
  // Remove from party
  if (prisonerCharacter.PartyBelongedTo != null)
  {
    if (prisonerCharacter.PartyBelongedTo.LeaderHero == prisonerCharacter)
      prisonerCharacter.PartyBelongedTo.RemovePartyLeader();
    prisonerCharacter.PartyBelongedTo.MemberRoster.RemoveTroop(prisonerCharacter.CharacterObject);
  }
  
  // Set captivity state
  prisonerCharacter.CaptivityStartTime = CampaignTime.Now;
  prisonerCharacter.ChangeState(Hero.CharacterStates.Prisoner);
  capturerParty.AddPrisoner(prisonerCharacter.CharacterObject, 1);
  
  // Special handling for MAIN HERO
  if (prisonerCharacter == Hero.MainHero)
  {
    if (MobileParty.MainParty.IsDisorganized)
      MobileParty.MainParty.SetDisorganized(false);
    
    PlayerCaptivity.StartCaptivity(capturerParty);  // ← KEY LINE
    
    // Destroy ships if at sea
    if (MobileParty.MainParty.IsCurrentlyAtSea)
    {
      for (int index = MobileParty.MainParty.Ships.Count - 1; index >= 0; --index)
        DestroyShipAction.Apply(MobileParty.MainParty.Ships[index]);
    }
  }
  
  CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(capturerParty, prisonerCharacter);
}
```

### PlayerCaptivity.StartCaptivity() Flow

**File:** `PlayerCaptivity.cs:155-173`

```csharp
private void StartCaptivityInternal(PartyBase captorParty)
{
  this._captivityStartTime = CampaignTime.Now;
  this._lastCheckTime = CampaignTime.Now;
  
  // Exit encounter if active
  if (PlayerEncounter.Current != null)
    PlayerEncounter.LeaveEncounter = true;
  
  // Leave settlement if inside one
  if (MobileParty.MainParty.CurrentSettlement != null)
    LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
  
  // DISABLE MAIN PARTY
  MobileParty.MainParty.IsActive = false;  // ← Player party disabled
  PartyBase.MainParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position);
  
  // Track captor
  this._captorParty = captorParty;
  this._captorParty.SetAsCameraFollowParty();  // Camera follows captor now
  this._captorParty.UpdateVisibilityAndInspected(MobileParty.MainParty.Position);
  
  // Handle army membership
  if (MobileParty.MainParty.Army != null)
  {
    if (MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty)
      DisbandArmyAction.ApplyByPlayerTakenPrisoner(MobileParty.MainParty.Army);
    MobileParty.MainParty.Army = null;  // ← REMOVED FROM ARMY
  }
}
```

**CRITICAL:** When player is captured:
1. **Army membership is removed** → `MobileParty.MainParty.Army = null`
2. **Party is deactivated** → `IsActive = false`
3. **Encounter is exited** → `PlayerEncounter.LeaveEncounter = true`
4. **Camera follows captor** → Player travels with their captors

### Captivity Menus

**All 13 captivity-related menus:**

1. **menu_captivity_end_no_more_enemies** - Captors have no use for you, let you go
   - When: Captor faction eliminated/defeated
   - Outcome: Released, return to map

2. **menu_captivity_end_by_ally_party_saved** - Ally paid your ransom
   - When: Friendly party ransoms player
   - Outcome: Released, return to map

3. **menu_captivity_end_by_party_removed** - Captors dispersed, you escape
   - When: Captor party destroyed/disbanded
   - Outcome: Released, return to map

4. **menu_captivity_end_wilderness_escape** - Found chance to escape during travel
   - When: Random escape chance while being dragged around
   - Outcome: Released, return to map

5. **menu_escape_captivity_during_battle** - Escape during battle
   - When: Captor enters battle, player escapes in chaos
   - Outcome: Released, return to map

6. **menu_released_after_battle** - Released after captor's battle
   - When: Specific battle outcomes
   - Outcome: Released, return to map

7. **menu_captivity_end_propose_ransom_wilderness** - Captor offers ransom in wilderness
   - When: Random event while traveling with captors
   - Options: Pay ransom / Refuse
   - Outcome: Pay → released; Refuse → stay captive

8. **menu_captivity_transfer_to_town** - Taken to dungeon
   - When: Captors reach a settlement
   - Outcome: Transferred to prison, new menu

9. **menu_captivity_end_exchanged_with_prisoner** - Exchanged for another prisoner
   - When: Diplomatic prisoner exchange
   - Outcome: Released, return to map

10. **menu_captivity_end_propose_ransom_in_prison** - Ransom offer while in dungeon
    - When: Random event while imprisoned
    - Options: Pay ransom / Refuse
    - Outcome: Pay → released; Refuse → stay captive

11. **menu_captivity_castle_remain** - Days pass in dungeon
    - When: Still imprisoned, time passing
    - Outcome: Continue captivity

12. **menu_captivity_end_prison_escape** - Escape from dungeon
    - When: Random escape chance from prison
    - Outcome: Released, return to map

13. **menu_captivity_castle_taken_prisoner** - Thrown in dungeon
    - When: Captured and brought to castle/town
    - Outcome: Enter captivity cycle

### Captivity Daily Checks

**File:** `PlayerCaptivity.cs:218-232`

```csharp
internal void Update(float dt)
{
  MapState activeState = Game.Current.GameStateManager.ActiveState as MapState;
  
  if (!PlayerCaptivity.IsCaptive) return;
  
  // Follow captor
  if (this._captorParty.IsMobile && this._captorParty.MobileParty.IsActive)
    PartyBase.MainParty.MobileParty.Position = this._captorParty.MobileParty.Position;
  else if (this._captorParty.IsSettlement)
    PartyBase.MainParty.MobileParty.Position = this._captorParty.Settlement.GatePosition;
  
  // Show captivity menu if not already at menu
  if (activeState != null && !activeState.AtMenu)
    GameMenu.ActivateGameMenu(PlayerCaptivity.CaptorParty.IsSettlement ? "settlement_wait" : "prisoner_wait");
  
  // Check for captivity changes (escape, ransom, etc.)
  this.CaptivityCampaignBehavior?.CheckCaptivityChange(dt);
}
```

**Daily checks can trigger:**
- Random escape attempts
- Ransom offers
- Prisoner exchanges
- Release due to faction changes

### Execution Risk

**File:** `PlayerCaptivityCampaignBehavior.cs:74-80`

```csharp
private void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
{
  if (prisoner != Hero.MainHero) return;
  if (capturer.LeaderHero == null) return;
  if (capturer.LeaderHero.GetRelation(prisoner) >= -30.0) return;
  if (MBRandom.RandomFloat > 0.02) return;  // 2% chance
  
  this._isMainHeroExecuted = true;  // Player will be executed!
  this._mainHeroExecuter = capturer.LeaderHero;
}
```

**Player can be executed if:**
- Relation with captor leader < -30
- 2% random chance
- Results in game over

---

## PART 2: ENLISTED PLAYER CAPTIVITY

### What Happens When Enlisted Player is Captured?

**Timeline:**

1. **Battle goes badly** → Player knocked unconscious / defeated
2. **TakePrisonerAction.Apply()** called
3. **PlayerCaptivity.StartCaptivity()** runs:
   ```
   - MobileParty.MainParty.Army = null  ← REMOVED FROM ARMY
   - MobileParty.MainParty.IsActive = false
   - PlayerEncounter.LeaveEncounter = true
   - Camera follows captor
   ```
4. **Enlisted state implications:**
   - Player is NO LONGER enlisted (removed from army)
   - Enlisted mod's encounter safety disables party
   - Player follows captor around map
   - Lord continues campaign without player

### End of Captivity

**File:** `PlayerCaptivity.cs:175-214`

```csharp
private void EndCaptivityInternal()
{
  if (Hero.MainHero.IsAlive)
  {
    Hero.MainHero.ChangeState(Hero.CharacterStates.Active);
    PartyBase.MainParty.AddElementToMemberRoster(CharacterObject.PlayerCharacter, 1, true);
    MobileParty.MainParty.ChangePartyLeader(Hero.MainHero);
  }
  
  // Leave settlement if in one
  if (Hero.MainHero.CurrentSettlement != null)
  {
    if (PlayerEncounter.Current != null)
      PlayerEncounter.LeaveSettlement();
    else if (Hero.MainHero.IsAlive)
      LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
  }
  
  // Finish encounter
  if (PlayerEncounter.Current != null)
    PlayerEncounter.Finish();
  else if (Campaign.Current.CurrentMenuContext != null)
    GameMenu.ExitToLast();
  
  // Remove from captor's prison roster
  if (this._captorParty.IsActive)
  {
    this._captorParty.PrisonRoster.RemoveTroop(Hero.MainHero.CharacterObject);
    if (this._captorParty.IsMobile && !this._captorParty.MobileParty.IsCurrentlyAtSea)
      MobileParty.MainParty.TeleportPartyToOutSideOfEncounterRadius();
  }
  
  // REACTIVATE PARTY
  if (Hero.MainHero.IsAlive)
  {
    MobileParty.MainParty.IsActive = true;  // ← Party reactivated
    PartyBase.MainParty.SetAsCameraFollowParty();
    MobileParty.MainParty.SetMoveModeHold();
    // XP for surviving captivity
    SkillLevelingManager.OnMainHeroReleasedFromCaptivity(CaptivityStartTime.ElapsedHoursUntilNow);
  }
  
  this._captorParty = null;
  this.CountOfOffers = 0;
  this.CurrentRansomAmount = 0;
}
```

**After captivity ends:**
- Party reactivated (`IsActive = true`)
- Player back on map
- **NO LONGER ENLISTED** (army membership was cleared)
- Player must re-enlist if they want to continue service

---

## PART 3: VILLAGE RAID SYSTEM

### Village Raid Initiation

**When lord (or player) raids a village:**

1. Approach village
2. Choose "Take a hostile action" from village menu
3. Select "Raid the village"
4. If at peace, warning menu appears
5. If at war, raid starts immediately

### Raiding Village Menu

**Menu:** `raiding_village` - Wait menu showing raid progress

**File:** `VillageHostileActionCampaignBehavior.cs:91-94`

```csharp
campaignGameSystemStarter.AddWaitGameMenu("raiding_village", 
  "{=hWwr3mrC}You are raiding {VILLAGE_NAME}.", 
  village_raid_game_menu_init, 
  wait_menu_start_raiding_on_condition, 
  wait_menu_end_raiding_on_consequence, 
  wait_menu_raiding_village_on_tick, 
  GameMenu.MenuAndOptionType.WaitMenuShowOnlyProgressOption);
```

**Options available:**
1. **End Raiding** - Stop raid early (if raid leader)
2. **Leave Army** - Leave army during raid (if in army, not leader)
3. **Abandon Army** - Abandon army during raid (if in army)

**Raid Progress:**
- Tick-based wait menu
- Shows progress bar
- Village takes damage over time
- Raid complete when village HP reaches 0

### Raid Consequences

**After raid completes:**

**If player is raid leader:**
```
OnMapEventEnded → checks if player led raid
→ GameMenu.ActivateGameMenu("village_player_raid_ended")
```

**Menu:** `village_player_raid_ended` - Shows raid results
- Display loot gained
- Show village damage
- Consequences (relation loss, etc.)
- Continue → back to map

**If player is army member (enlisted case):**
```
OnMapEventEnded → checks if someone else led raid
→ GameMenu.ActivateGameMenu("village_raid_ended_leaded_by_someone_else")
```

**Menu:** `village_raid_ended_leaded_by_someone_else` - Army member raid result
- Shows that raid completed under someone else's leadership
- Less detailed info than leader menu
- Continue → back to map

### Complete Village Hostile Action Menus

1. **village_hostile_action** - Choose hostile action type
   - Raid the village
   - Force volunteers (recruit peasants)
   - Force supplies (take food)
   - Forget it

2. **raid_village_no_resist_warn_player** - Warning if not at war
   - Shows consequences of raiding while at peace
   - Continue / Forget it

3. **raiding_village** - Active raid (wait menu)
   - Progress bar
   - End raiding / Leave army / Abandon army options

4. **village_player_raid_ended** - Raid complete (player leader)
   - Shows loot and results
   - Continue to map

5. **village_raid_ended_leaded_by_someone_else** - Raid complete (army member)
   - Shows lord completed raid
   - Continue to map

6. **force_supplies_village** - Successfully forced supplies
   - Shows goods taken
   - Continue to map

7. **force_supplies_village_resist_warn_player** - Warning before forcing supplies
   - Shows consequences
   - Continue / Forget it

8. **force_troops_village_resist_warn_player** - Warning before forcing volunteers
   - Shows consequences
   - Continue / Forget it

9. **force_volunteers_village** - Successfully forced volunteers
   - Shows recruits gained
   - Continue to map

10. **village_looted** - Village completely destroyed
    - Village is burnt and looted
    - Leave

---

## PART 4: ENLISTED PLAYER IN VILLAGE RAID

### Scenario: Lord Starts Raiding While Player Enlisted

**Flow:**

1. **Lord decides to raid** (AI behavior)
2. **Lord's army (including enlisted player) approaches village**
3. **Lord initiates raid** → Creates RaidEventComponent
4. **System opens menu based on player state:**
   
   **If player is army member (enlisted):**
   ```
   MobileParty.MainParty.Army != null
   MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty
   → Opens "raiding_village" menu
   → Shows "Leave Army" and "Abandon Army" options
   → Progress bar shows raid progress
   ```

5. **Raid progresses** (tick-based)
   - Village HP decreases
   - Loot accumulated
   - Player party participates passively

6. **Raid completes:**
   ```
   MapEvent.FinishBattle()
   → OnMapEventEnded fires
   → Checks: is player the raid leader?
     → NO (enlisted): "village_raid_ended_leaded_by_someone_else"
   ```

7. **Post-raid menu:**
   - Shows raid completed
   - Player shares in loot (army distribution)
   - Continue → back with army

### Enlisted Mod Patches for Raiding

**Current patches:**

1. **AbandonArmyBlockPatch** - Hides "Abandon army" button on `raiding_village`
   - ✅ Already implemented
   - Enlisted soldiers can't abandon during raid

2. **No special handling needed** - Raid system works naturally:
   - Player is in army
   - Lord leads raid
   - Player participates passively
   - Shares loot
   - Returns to normal army operations after

---

## CONCLUSION

### Captivity System for Enlisted Mod

**Current State:**
- ❌ No special handling for enlisted player captivity
- ❌ Captivity automatically ends enlistment (army removed)
- ❌ Player must re-enlist after release

**Gaps:**
1. No detection of "was enlisted before capture"
2. No auto-return to lord after release
3. No lord attempting to ransom player
4. No special enlisted captivity events

**Recommendations:**
1. **Track enlistment state before captivity** - Store lord and rank
2. **Add post-captivity choice** - "Return to lord?" vs "Go independent"
3. **Lord ransom attempts** - Friendly lord may pay ransom sooner
4. **Enlisted captivity events** - Special events for captured soldiers

### Village Raid System for Enlisted Mod

**Current State:**
- ✅ Works correctly with enlisted players
- ✅ "Abandon army" button already hidden
- ✅ Player participates in raids as army member
- ✅ Correct post-raid menu appears

**No changes needed** - System works as designed.

---

## FILES TO UPDATE

1. **`docs/Reference/battle-system-complete-analysis.md`**
   - Add captivity menus to catalog
   - Add village raid menus to catalog
   - Document captivity flow in Part 6

2. **`docs/Reference/complete-menu-analysis.md`**
   - Add all 13 captivity menus to menu list
   - Add all 10 village raid menus to menu list
   - Update menu count

3. **`docs/architecture/encounter-safety.md`**
   - Add section on captivity handling
   - Document army removal during captivity
   - Note gap: no auto-return after release
