# Error Codes

Codes follow the pattern `E-<SUBSYSTEM>-<NNN>`, where subsystem is an uppercase
short identifier (`QM`, `QM-UI`, `UI`, `DIALOG`, `SYSTEM`, `TIME`, `MUSTER`,
`QM-PARTY`, etc.) and NNN is a three-digit sequence within that subsystem.

**Policy.** Gaps are fine — never renumber. When adding a code, append the next
unused number in the relevant subsystem. Removed codes (code deleted from
source) get strikethrough + a `removed YYYY-MM-DD` note; do not delete rows
so log readers can still look up historical codes.

## Engine parallel

The Bannerlord engine has its own time-mode capture pattern for popups in the
v1.3.13 decompile (developer-local, sibling of the repo root — see `AGENTS.md` §1):
`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs:2218`
defines `_timeControlModeBeforePopUpOpened`. Our `E-TIME-*` codes and the upcoming
menu-lifecycle refactor (deferred item #2b) should stay aligned with this
pattern where practical.

## Quartermaster (QM)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-QM-005 | Error charging gold | See exception log | Enlistment |
| E-QM-006 | Error processing equipment variant request | See exception log | Enlistment |
| E-QM-007 | `GetCultureHorseGear` failed | See exception log | Enlistment |
| E-QM-008 | Cannot create quartermaster: no enlisted lord | Check `IsEnlisted` before `GetOrCreateQuartermaster` | Enlistment |
| E-QM-009 | Cannot create quartermaster: lord has no culture | Investigate how lord was created | Enlistment |
| E-QM-010 | `HeroCreator.CreateSpecialHero` returned null | Check template + birth settlement | Enlistment |
| E-QM-011 | Failed to apply wealthy quartermaster attire | Log only; QM still spawns | Enlistment |
| E-QM-020 | Upgrade failed: `Hero.MainHero` is null / manager instance not found | Ensure player is active and QM registered | QM UI / Equipment |
| E-QM-021 | Upgrade failed: empty slot | Equip an item before upgrade | Equipment |
| E-QM-022 | Upgrade failed: item has no modifier group | Item not upgradeable; skip | Equipment |
| E-QM-023 | Upgrade failed: item has no modifiers for target quality | Pick different target quality | Equipment |
| E-QM-024 | Upgrade failed: invalid cost calculation | Investigate cost formula | Equipment |
| E-QM-025 | `GetOrCreateQuartermaster` returned null or dead hero while enlisted | Upstream spawn failure — check earlier E-QM-* codes | Interface |
| E-QM-026 | Failed to open provisions UI | See exception log | Dialogue |
| E-QM-100 | `BuildArmorOptionsFromCurrentTroop` failed | See exception log | Equipment |
| E-QM-101 | `BuildWeaponOptionsFromCurrentTroop` failed | See exception log | Equipment |
| E-QM-102 | `CollectWeaponVariantsFromNodes` failed | See exception log | Equipment |
| E-QM-103 | `CollectWeaponVariantsFromAllTiers` failed | See exception log | Equipment |
| E-QM-104 | `BuildVariantOptionsForWeapons` failed | See exception log | Equipment |
| E-QM-105 | `CollectArmorVariantsFromAllTiers` failed | See exception log | Equipment |
| E-QM-106 | `BuildVariantOptionsForArmor` failed | See exception log | Equipment |
| E-QM-107 | `BuildTroopBranchNodes` failed | See exception log | Equipment |
| E-QM-108 | `CollectArmorVariantsFromNodes` failed | See exception log | Equipment |
| E-QM-111 | `BuildShieldOptionsFromWeapons` failed | See exception log | Equipment |
| E-QM-PARTY-001 | Both QM and enlisted lord have no party (illegal state during enlistment) | Investigate follow/attach flow; QM conversation cannot proceed | Enlistment |
| E-QM-DEAL-001 | Failed to select equipment by skills (Quartermaster's Deal) | See exception log | Enlistment |

## Quartermaster UI (QM-UI)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-QM-UI-001 | `ScreenManager.TopScreen` null on equipment popup | Likely a modal blocking; retry after frame | QM UI |
| E-QM-UI-002 | Equipment selector failed to open (exception caught) | Inspect inner exception in log | QM UI |
| E-QM-UI-003 | Failed to open upgrade screen | Inspect inner exception | QM UI |
| E-QM-UI-004 | Failed to open provisions screen | Inspect inner exception | QM UI |
| E-QM-UI-005 | `ScreenManager.TopScreen` null when adding provisions layer | Retry after frame | QM UI |
| E-QM-UI-006 | `ScreenManager.TopScreen` null when adding upgrade layer | Retry after frame | QM UI |
| E-QMUI-001 | Error refreshing character model | See exception log | QM UI |
| E-QMUI-002 | Error recalculating variant states | See exception log | QM UI |
| E-QMUI-003 | Cannot select equipment - variant or item is null | Investigate VM binding | QM UI |
| E-QMUI-004 | Cannot select item - variant item is null | Investigate VM binding | QM UI |

## Interface (UI)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-UI-001 | Error handling battle menu transition | See exception log | Interface |
| E-UI-002 | Error in siege battle detection | See exception log | Interface |
| E-UI-003 | Error opening debug tools | See exception log | Interface |
| E-UI-004 | Deferred enlisted menu activation error | See exception log | Interface |
| E-UI-006 | Failed to add Return to camp options | See exception log | Interface |
| E-UI-007 | Error returning to camp | See exception log | Interface |
| E-UI-009 | `OnSettlementLeftReturnToCamp` error | See exception log | Interface |
| E-UI-010 | Error initializing Camp hub | See exception log | Interface |
| E-UI-011 | Error initializing enlisted status menu | See exception log | Interface |
| E-UI-012 | Error refreshing menu | See exception log | Interface |
| E-UI-013 | Error opening Master at Arms | See exception log | Interface |
| E-UI-014 | Error in Talk to My Lord | See exception log | Interface |
| E-UI-015 | Error finding nearby lords | See exception log | Interface |
| E-UI-016 | Error starting lord conversation | See exception log | Interface |
| E-UI-017 | Error showing lord selection | See exception log | Interface |
| E-UI-018 | Error opening conversation with lord | See exception log | Interface |
| E-UI-019 | Error in settlement entered tracking | See exception log | Interface |
| E-UI-020 | `VisitTown` failed | See exception log | Interface |
| E-UI-021 | Error requesting leave / error initializing status detail menu | See exception log | Interface |
| E-UI-022 | Error in Ask for Leave | See exception log | Interface |
| E-UI-023 | Error granting temporary leave | See exception log | Interface |
| E-UI-024 | Error opening desertion menu | See exception log | Interface |
| E-UI-025 | Error in free desertion | See exception log | Interface |
| E-UI-026 | Error initializing desertion menu | See exception log | Interface |
| E-UI-027 | Error returning from desertion menu | See exception log | Interface |
| E-UI-029 | Error confirming desertion | See exception log | Interface |
| E-UI-030 | Error during enlisted status tick | See exception log | Interface |
| E-UI-036 | Error refreshing enlisted status | See exception log | Interface |
| E-UI-038 | Error opening quartermaster conversation | See exception log | Interface |
| E-UI-040 | Error handling baggage train access / cannot desert (EnlistmentBehavior unavailable) / error showing orders menu | See exception log | Interface |
| E-UI-041 | Error opening baggage request conversation | See exception log | Interface |
| E-UI-046 | Failed to toggle orders accordion | Interface bug | Interface |
| E-UI-047 | Failed to toggle decisions accordion | Interface bug | Interface |
| E-UI-048 | Failed to select decision slot | See exception log | Interface |

## Dialogue (DIALOG)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-DIALOG-001 | Unknown dialogue action in JSON | Add handler to `CheckGateCondition` or fix JSON | Dialogue |
| E-DIALOG-002 | Unknown gate condition in JSON | Add handler or fix JSON; option is treated as gated | Dialogue |
| E-DIALOG-003 | Error cleaning up menu after discharge | See exception log | Dialogue |
| E-DIALOG-004 | Failed to add retirement supplies | See exception log | Dialogue |

## Time Control (TIME)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-TIME-001 | `EnlistedTimeScope.Capture` called with no active `Campaign.Current` | Defensive; should not occur in normal play | Time Control |

## Muster (MUSTER)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-MUSTER-001 | Failed to register muster menus (falls back to legacy) | See exception log | Enlistment |
| E-MUSTER-002 | Stage transition / menu init failed | See exception log | Enlistment |
| E-MUSTER-003 | Corrupted muster state on load; failed to restore muster state | Muster sequence aborted; check save integrity | Enlistment |
| E-MUSTER-004 | Effect application / pay resolution / discharge finalization failed | See exception log | Enlistment |
| E-MUSTER-005 | `BeginMusterSequenceInternal` called but player not enlisted / unhandled exception / error processing discharge confirmation | See exception log | Enlistment |
| E-MUSTER-006 | Cannot start muster: `Campaign.Current` is null / error showing discharge confirmation | See exception log | Enlistment |
| E-MUSTER-007 | Cannot start muster: `GameMenuManager` is null / error processing final pay discharge | See exception log | Enlistment |
| E-MUSTER-008 | Deferred menu activation failed / pre-activation validation failed / error showing final pay confirmation | See exception log | Enlistment |
| E-MUSTER-009 | Failed to pre-initialize text variables | See exception log | Enlistment |
| E-MUSTER-010 | Muster menus not registered, deferring to next cycle | Verify menu registration ran; retry on next tick | Enlistment |

## Enlistment (ENLIST)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-ENLIST-001 | Error opening baggage train stash | See exception log | Enlistment |
| E-ENLIST-002 | Reservist re-entry boost failed | See exception log | Enlistment |
| E-ENLIST-004 | Error stashing belongings | See exception log | Enlistment |
| E-ENLIST-005 | Error liquidating belongings | See exception log | Enlistment |
| E-ENLIST-006 | Error smuggling item | See exception log | Enlistment |
| E-ENLIST-007 | Error joining lord's kingdom | See exception log | Enlistment |
| E-ENLIST-008 | Error finishing PlayerEncounter before enlistment | See exception log | Enlistment |
| E-ENLIST-009 | `TroopSelectionManager` unavailable - cannot restore saved equipment | Investigate manager registration | Enlistment |
| E-ENLIST-011 | Error removing player from army | See exception log | Enlistment |
| E-ENLIST-012 | Error restoring kingdom during discharge | See exception log | Enlistment |
| E-ENLIST-014 | Error triggering post-enlist bag check / failed to grant commander retinue on tier change | See exception log | Enlistment |
| E-ENLIST-015 | Error returning baggage train stash to inventory / error transferring service to new lord | See exception log | Enlistment |
| E-ENLIST-016 | Cross-faction baggage prompt failed / `TransferServiceToLord` called with null lord | See exception log | Enlistment |
| E-ENLIST-017 | Error handling baggage transfer choice | See exception log | Enlistment |
| E-ENLIST-018 | Error processing courier arrival | See exception log | Enlistment |

## Retirement (RETIRE)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-RETIRE-001 | Error finalizing discharge | See exception log | Enlistment |
| E-RETIRE-002 | Error applying pension on discharge | See exception log | Enlistment |
| E-RETIRE-003 | Error applying relation bonuses | See exception log | Enlistment |
| E-RETIRE-004 | Error applying subsequent re-enlistment bonuses | See exception log | Enlistment |
| E-RETIRE-005 | Cannot process first-term retirement - not eligible | Check `IsEnlisted && IsEligibleForRetirement` | Enlistment |
| E-RETIRE-006 | Cannot process renewal retirement - not in renewal term | Check term state | Enlistment |
| E-RETIRE-007 | Cannot start renewal term - not enlisted | Check `IsEnlisted` | Enlistment |
| E-RETIRE-008 | Cannot re-enlist - not eligible or wrong faction | Check `CanReEnlistAfterCooldown` | Enlistment |
| E-RETIRE-009 | Cannot apply relation bonuses - no faction | Check lord/faction state | Enlistment |
| E-RETIRE-010 | Cannot apply subsequent bonuses - no faction | Check lord/faction state | Enlistment |

## Battle & Battle Wait (BATTLE / BATTLEWAIT)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-BATTLE-001 | `ShouldJoinPlayerBattles` property not found via reflection | Verify engine compatibility | Enlistment |
| E-BATTLE-002 | Failed to set `ShouldJoinPlayerBattles` (direct + reflection) | See aggregated exception | Enlistment |
| E-BATTLE-003 | Error joining lord's army | See exception log | Enlistment |
| E-BATTLE-004 | Error in battle participation setup | See exception log | Enlistment |
| E-BATTLE-005 | Failed to sync besieger camp | See exception log | Enlistment |
| E-BATTLE-006 | Failed to teleport player to safety | See exception log | Enlistment |
| E-BATTLE-012 | Error restoring campaign flow after battle | See exception log | Enlistment |
| E-BATTLE-013 | Error in `OnAfterMissionStarted` | See exception log | Enlistment |
| E-BATTLE-014 | Error in `MapEventStarted` battle handler | See exception log | Enlistment |
| E-BATTLE-015 | Error in MapEvent end handler | See exception log | Enlistment |
| E-BATTLE-016 | Error in `OnPlayerBattleEnd` | See exception log | Enlistment |
| E-BATTLE-018 | Failed to switch to native menu | See exception log | Enlistment |
| E-BATTLE-019 | Error finishing PlayerEncounter after enlistment ended | See exception log | Enlistment |
| E-BATTLE-020 | Error in deferred encounter cleanup | See exception log | Enlistment |
| E-BATTLEWAIT-001 | Error entering reserve mode | See exception log | Combat |
| E-BATTLEWAIT-002 | Error initializing battle wait menu | See exception log | Combat |
| E-BATTLEWAIT-003 | Error in battle wait tick | See exception log | Combat |
| E-BATTLEWAIT-004 | Error in rejoin menu switch | See exception log | Combat |
| E-BATTLEWAIT-005 | Error rejoining battle | See exception log | Combat |
| E-BATTLEWAIT-006 | Error during fallback exit | See exception log | Combat |

## Combat subsystems (COMBAT / KILLTRACK / FORMASSIGN)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-COMBAT-001 | Failed to initialize encounter behavior | See exception log | Combat |
| E-KILLTRACK-001 | Error in `AfterStart` (kill tracker) | See exception log | Combat |
| E-KILLTRACK-002 | Error in `OnMissionTick` (kill tracker) | See exception log | Combat |
| E-KILLTRACK-003 | Error in `OnAgentRemoved` (kill tracker) | See exception log | Combat |
| E-KILLTRACK-004 | Error in `OnEndMission` (kill tracker) | See exception log | Combat |
| E-FORMASSIGN-001 | Error in `AfterStart` (formation assignment) | See exception log | Combat |
| E-FORMASSIGN-002 | Error in `OnDeploymentFinished` | See exception log | Combat |
| E-FORMASSIGN-003 | Error in `OnAgentBuild` | See exception log | Combat |
| E-FORMASSIGN-004 | Error removing stay-back companion | See exception log | Combat |
| E-FORMASSIGN-005 | Error teleporting player to formation | See exception log | Combat |
| E-FORMASSIGN-006 | Error teleporting squad to formation | See exception log | Combat |
| E-FORMASSIGN-007 | Error during lord attach | See exception log | Combat |
| E-FORMASSIGN-008 | Error assigning player party to formation | See exception log | Combat |
| E-FORMASSIGN-009 | Error setting up squad command | See exception log | Combat |
| E-FORMASSIGN-010 | Error transferring army command | See exception log | Combat |
| E-FORMASSIGN-011 | Error in `OnEndMission` (formation assignment) | See exception log | Combat |

## Harmony patches (PATCH)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-PATCH-001 | Error in `GetGenericStateMenu` patch | See exception log | GameAdapters |
| E-PATCH-002 | Error in encounter suppression patch | See exception log | GameAdapters |
| E-PATCH-006 | Error in `Finish` prefix | See exception log | GameAdapters |
| E-PATCH-007 | `SafeCleanupEncounter` failed | See exception log | GameAdapters |
| E-PATCH-008 | `PlayerEncounter.Finish` crashed while enlisted | See crash context in log | GameAdapters |
| E-PATCH-009 | Error in loot screen patch | See exception log | GameAdapters |
| E-PATCH-010 | Error in cohesion exclusion patch | See exception log | GameAdapters |
| E-PATCH-011 | Error in activation protection patch | See exception log | GameAdapters |
| E-PATCH-012 | Error in visibility enforcement patch | See exception log | GameAdapters |
| E-PATCH-013 | Failed to auto-join battle | See exception log | GameAdapters |
| E-PATCH-014 | Error in `join_encounter` patch | See exception log | GameAdapters |
| E-PATCH-015 | Error cleaning prison rosters | See exception log | GameAdapters |
| E-PATCH-016 | Error in skill suppression patch | See exception log | GameAdapters |
| E-PATCH-017 | Error tracking combat XP for enlistment / error in `ReturnToArmySuppressionPatch` | See exception log | GameAdapters |
| E-PATCH-018 | Error flushing accumulated combat XP / error in Order of Battle suppression patch | See exception log | GameAdapters |
| E-PATCH-019 | `HidePartyNamePlatePatch`: failed to apply patch | See exception log | GameAdapters |
| E-PATCH-020 | `HidePartyNamePlatePatch`: error in `UpdatePostfix` | See exception log | GameAdapters |
| E-PATCH-021 | Error in influence suppression patch | See exception log | GameAdapters |
| E-PATCH-022 | Error in captured lord conversation suppression patch | See exception log | GameAdapters |
| E-PATCH-023 | Error in `EncounterCaptureEnemySuppressionPatch` | See exception log | GameAdapters |
| E-PATCH-024 | `EffectiveEngineer` patch error | See exception log | GameAdapters |
| E-PATCH-025 | `EffectiveScout` patch error | See exception log | GameAdapters |
| E-PATCH-026 | `EffectiveQuartermaster` patch error | See exception log | GameAdapters |
| E-PATCH-027 | `EffectiveSurgeon` patch error | See exception log | GameAdapters |
| E-PATCH-028 | Error in `TownLeaveButtonPatch` | See exception log | GameAdapters |
| E-PATCH-029 | Error in `SettlementOutsideLeaveButtonPatch` | See exception log | GameAdapters |
| E-PATCH-030 | Error in `ArmyDispersedMenuPatch` | See exception log | GameAdapters |

## Naval patches (NAVALPATCH / NAVAL)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-NAVALPATCH-003 | `PlayerIsAtSeaTag` safety catch | See exception log | GameAdapters |
| E-NAVALPATCH-004 | Failed to apply `UpdateEntityPosition` crash guard patch | See exception log | GameAdapters |
| E-NAVALPATCH-005 | Error in `UpdateEntityPosition` crash guard prefix | See exception log | GameAdapters |
| E-NAVALPATCH-006 | `GetSuitablePlayerShip` threw an exception | See exception log | GameAdapters |
| E-NAVALPATCH-007 | `GetOrderedCaptainsForPlayerTeamShips` prefix threw an exception | See exception log | GameAdapters |
| E-NAVALPATCH-008 | `OnShipRemoved` prefix threw an exception | See exception log | GameAdapters |
| E-NAVALPATCH-009 | `BattleObserverMissionLogic.OnAgentRemoved` prefix threw an exception | See exception log | GameAdapters |
| E-NAVALPATCH-010 | `NavalAgentsLogic.OnAgentRemoved` prefix threw an exception | See exception log | GameAdapters |
| E-NAVALPATCH-011 | Failed to apply raft state suppression patch | See exception log | GameAdapters |
| E-NAVALPATCH-012 | Error in raft state suppression prefix | See exception log | GameAdapters |
| E-NAVALPATCH-013 | Failed to apply `OnPartyLeftArmy` patch | See exception log | GameAdapters |
| E-NAVALPATCH-014 | Error in `OnPartyLeftArmy` prefix | See exception log | GameAdapters |
| E-NAVALPATCH-015 | Failed to deactivate raft state | See exception log | GameAdapters |
| E-NAVALPATCH-016 | Failed to clean up army reference | See exception log | GameAdapters |
| E-NAVALPATCH-017 | Error in `ArmyWaitMenuTick_Prefix` | See exception log | GameAdapters |
| E-NAVALPATCH-018 | Error in `ArmyWaitOnInit_Prefix` | See exception log | GameAdapters |
| E-NAVALPATCH-019 | Error in `WaitMenuArmyWaitOnInit_Prefix` | See exception log | GameAdapters |
| E-NAVALPATCH-020 | Error in `SafeExitArmyWaitMenu` | See exception log | GameAdapters |
| E-NAVALPATCH-021 | Error in ship damage protection patch | See exception log | GameAdapters |
| E-NAVALPATCH-022 | Error in naval navigation capability patch | See exception log | GameAdapters |
| E-NAVAL-001 | Failed to sync naval position | See exception log | Enlistment |
| E-NAVAL-002 | Failed to teleport stranded player to nearest port | See exception log | Enlistment |
| E-NAVAL-003 | Error validating army state | See exception log | Enlistment |

## Camp (CAMP)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-CAMP-001 | `camp_opportunities.json` not found / failed to register camp menus | Verify mod installation and file presence | Camp |
| E-CAMP-002 | No `opportunities` array found in `camp_opportunities.json` / error initializing Current Posting | Check JSON structure | Camp |
| E-CAMP-003 | Failed to parse `camp_opportunities.json` / error calculating days remaining | Check JSON syntax | Camp |
| E-CAMP-004 | Error initializing Faction Records | See exception log | Camp |
| E-CAMP-005 | Error initializing Faction Detail | See exception log | Camp |
| E-CAMP-006 | Error initializing Lifetime Summary | See exception log | Camp |
| E-CAMP-007 | Error initializing Retinue menu | See exception log | Camp |
| E-CAMP-008 | Error initializing purchase menu | See exception log | Camp |
| E-CAMP-009 | `ExecutePurchase`: manager null | Investigate manager lifecycle | Camp |
| E-CAMP-010 | Error initializing dismiss menu | See exception log | Camp |
| E-CAMP-011 | Error initializing reinforcement request menu | See exception log | Camp |
| E-CAMP-012 | `ExecuteRequisition`: manager null | Investigate manager lifecycle | Camp |
| E-CAMP-013 | Error initializing companion assignments | See exception log | Camp |

## Retinue (RETINUE / CASUALTY / VET / SRM / LIFECYCLE)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-RETINUE-001 | Error in daily trickle tick | See exception log | Retinue |
| E-CASUALTY-001 | Error in `OnMapEventStarted` | See exception log | Retinue |
| E-CASUALTY-002 | Error in `OnMapEventEnded` | See exception log | Retinue |
| E-CASUALTY-003 | Error in `OnPlayerBattleEnd` | See exception log | Retinue |
| E-CASUALTY-004 | Error in daily sync (casualty tracker) | See exception log | Retinue |
| E-VET-001 | Error generating veteran name | See exception log | Retinue |
| E-SRM-002 | Error handling promotion for retinue | See exception log | Retinue |
| E-LIFECYCLE-001 | Error in daily lifecycle tick | See exception log | Retinue |

## Save / Load (SAVELOAD)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-SAVELOAD-001 | Save/load failed in a behavior during Load/Save/SyncData | Try loading an older save; check Session-A / Conflicts-A logs | Core |
| E-SAVELOAD-002 | Error serializing minor faction desertion cooldowns | See exception log | Enlistment |
| E-SAVELOAD-003 | Error serializing issued rations / tracking migration failed / error during roster deduplication | See exception log | Enlistment |
| E-SAVELOAD-004 | Error in `RestorePartyStateAfterLoad` | See exception log | Enlistment |

## Troop selection (TROOPSEL)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-TROOPSEL-001 | Master at Arms: missing culture on current lord | Investigate lord state | Equipment |
| E-TROOPSEL-002 | Master at Arms apply failed | See exception log | Equipment |
| E-TROOPSEL-003 | Master at Arms popup failed | See exception log | Equipment |
| E-TROOPSEL-004 | `GetUnlockedTroops` failed | See exception log | Equipment |
| E-TROOPSEL-005 | Failed to open Master at Arms popup | See exception log | Equipment |
| E-TROOPSEL-006 | Failed to switch back to enlisted status | See exception log | Equipment |
| E-TROOPSEL-007 | Cannot show troop selection - player not enlisted | Check `IsEnlisted` | Equipment |
| E-TROOPSEL-008 | Cannot show troop selection - missing culture on current lord | Investigate lord state | Equipment |
| E-TROOPSEL-009 | No troops found for culture/tier | Check culture roster data | Equipment |
| E-TROOPSEL-010 | `BuildCultureTroopTree` failed | See exception log | Equipment |
| E-TROOPSEL-011 | No equipment found for selected troop | Check troop equipment data | Equipment |
| E-TROOPSEL-012 | Failed to apply selected troop equipment | See exception log | Equipment |
| E-TROOPSEL-013 | No troop available for formation type | Check formation/roster data | Equipment |
| E-TROOPSEL-014 | Error in troop selection | See exception log | Equipment |

## Desertion (DESERT)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-DESERT-001 | Error applying minor faction desertion penalties | See exception log | Enlistment |
| E-DESERT-004 | Cannot start grace period - kingdom is null | Caller must supply kingdom | Enlistment |
| E-DESERT-005 | Cannot apply penalties - player clan is null | Investigate clan state | Enlistment |

## Discharge / Pay / Wage / Gold (DISCHARGE / PAY / WAGE / GOLD)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-DISCHARGE-001 | Error in relation penalty suppression | See exception log | GameAdapters |
| E-DISCHARGE-002 | Error restoring kingdom without penalties | See exception log | GameAdapters |
| E-PAY-001 | Error resolving smuggle discharge | See exception log | Enlistment |
| E-PAY-002 | Error awarding battle loot share | See exception log | Enlistment |
| E-PAY-004 | Error checking NPC desertion from pay tension | See exception log | Enlistment |
| E-WAGE-001 | Failed to calculate wage breakdown (using fallback values) | See exception log | Enlistment |
| E-GOLD-001 | Pension processing failed; pension paused | See exception log | Enlistment |

## Equipment (EQUIP)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-EQUIP-004 | Gear handling on discharge failed | See exception log | Enlistment |
| E-EQUIP-005 | Failed to clear equipment | See exception log | Enlistment |

## Settlement (SETTLEMENT)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-SETTLEMENT-001 | Failed to pause time on settlement entry | See exception log | Enlistment |
| E-SETTLEMENT-002 | Error finishing PlayerEncounter before settlement entry (player) | See exception log | Enlistment |
| E-SETTLEMENT-003 | Error finishing PlayerEncounter before settlement entry (lord) | See exception log | Enlistment |
| E-SETTLEMENT-004 | Error in settlement entry detection | See exception log | Enlistment |
| E-SETTLEMENT-005 | Error pulling player from settlement to follow lord | See exception log | Enlistment |
| E-SETTLEMENT-006 | Error in settlement exit detection | See exception log | Enlistment |

## Encounter (ENCOUNTER / ENCOUNTER-POST)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-ENCOUNTER-001 | Error in `TryLeaveEncounter` | See exception log | Enlistment |
| E-ENCOUNTER-002 | Failed to finish lingering PlayerEncounter | See exception log | Enlistment |
| E-ENCOUNTER-POST-001 | `CleanupPostEncounterState` failed | See exception log | Enlistment |

## Event safety (EVENTSAFE / EVT)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-EVENTSAFE-001 | Could not find `EndCaptivityAction.ApplyByEscape` | Verify engine compatibility | Enlistment |
| E-EVENTSAFE-002 | Error forcing player escape | See exception log | Enlistment |
| E-EVT-001 | Selected option identifier is not an `EventOption` | Internal error - investigate event flow | Content |

## Faction relations (FACTIONREL)

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-FACTIONREL-001 | Error mirroring war relations | See exception log | Enlistment |
| E-FACTIONREL-002 | Error restoring war relations | See exception log | Enlistment |
| E-FACTIONREL-003 | Error refreshing faction visuals | See exception log | Enlistment |

## Miscellaneous

| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-INCIDENT-002 | Failed to register pay muster incident | See exception log | Enlistment |
| E-INCIDENT-004 | `MusterMenuHandler` not found / error triggering pay muster (deferring) | See exception log | Enlistment |
| E-MAP-001 | Error delivering incident for context | See exception log | Content |
| E-SIEGE-001 | Error finishing PlayerEncounter when entering settlement | See exception log | Enlistment |
| E-POSSYNC-001 | Error in aggressive position sync | See exception log | Enlistment |
| E-FOLLOW-001 | Error releasing escort | See exception log | Enlistment |
| E-GRACE-001 | Failed to grant grace period interaction window | See exception log | Enlistment |
| E-PROG-001 | Error checking promotion notification | See exception log | Enlistment |
| E-DIAG-001 | Error logging party state | See exception log | Core |
| E-DIAG-002 | Failed to run startup diagnostics | See exception log | Core |
| E-DIAG-003 | Failed to refresh deferred patch diagnostics | See exception log | Core |
| E-DIAG-004 | Failed to log behaviors | See exception log | Core |
| E-BAG-001 | Error processing emergency baggage access | See exception log | Dialogue |
| E-BAG-002 | Error processing column halt request | See exception log | Dialogue |
