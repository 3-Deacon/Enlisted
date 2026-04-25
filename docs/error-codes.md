# Error Codes

This registry is **auto-generated** from `ModLogger.Surfaced(...)` calls
by `Tools/Validation/generate_error_codes.py`. Do not hand-edit — your
changes will be overwritten on the next run.

For pre-redesign codes (format `E-SUBSYSTEM-NNN`), see
[error-codes-archive.md](error-codes-archive.md).

## ACTIVITY

| Code | Summary | Source |
|---|---|---|
| E-ACTIVITY-5468 | activity started with no phases resolved — type may be missing from ActivityTypeCatalog | src/Features/Activities/ActivityRuntime.cs:132 |
| E-ACTIVITY-6383 | ActivityTypeCatalog registered 0 types despite JSON files present | src/Features/Activities/ActivityTypeCatalog.cs:64 |
| E-ACTIVITY-d0bb | Failed to load activity type | src/Features/Activities/ActivityTypeCatalog.cs:55 |

## BAGGAGE

| Code | Summary | Source |
|---|---|---|
| E-BAGGAGE-e516 | BaggageTrainManager not available for emergency access | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:3424 |

## CAMP

| Code | Summary | Source |
|---|---|---|
| E-CAMP-0092 | Error initializing reinforcement request menu | src/Features/Camp/CampMenuHandler.cs:2094 |
| E-CAMP-0673 | Error initializing Faction Records | src/Features/Camp/CampMenuHandler.cs:581 |
| E-CAMP-1199 | Error initializing purchase menu | src/Features/Camp/CampMenuHandler.cs:1464 |
| E-CAMP-2540 | ExecutePurchase: manager null | src/Features/Camp/CampMenuHandler.cs:1701 |
| E-CAMP-4c9c | Error initializing Lifetime Summary | src/Features/Camp/CampMenuHandler.cs:802 |
| E-CAMP-7c0f | Error initializing Current Posting | src/Features/Camp/CampMenuHandler.cs:363 |
| E-CAMP-9518 | ExecuteRequisition: manager null | src/Features/Camp/CampMenuHandler.cs:2110 |
| E-CAMP-b154 | Error initializing dismiss menu | src/Features/Camp/CampMenuHandler.cs:1856 |
| E-CAMP-c3d5 | Error initializing companion assignments | src/Features/Camp/CampMenuHandler.cs:2254 |
| E-CAMP-c8fb | Error initializing Faction Detail | src/Features/Camp/CampMenuHandler.cs:696 |
| E-CAMP-db74 | Error initializing Retinue menu | src/Features/Camp/CampMenuHandler.cs:1129 |

## COMPANION

| Code | Summary | Source |
|---|---|---|
| E-COMPANION-1a21 | schema_version_mismatch | src/Features/Companions/CompanionSpawnFactory.cs:79 |
| E-COMPANION-1dad | duplicate_companion_type | src/Features/Companions/CompanionSpawnFactory.cs:103 |
| E-COMPANION-2e14 | companion_killed_in_battle | src/Features/Companions/CompanionLifecycleHandler.cs:202 |
| E-COMPANION-3c31 | companion_type_no_archetypes | src/Features/Companions/CompanionSpawnFactory.cs:303 |
| E-COMPANION-56bb | unknown_companion_type | src/Features/Companions/CompanionSpawnFactory.cs:162 |
| E-COMPANION-6309 | spawn_no_template | src/Features/Companions/CompanionSpawnFactory.cs:198 |
| E-COMPANION-7de6 | no_archetype | src/Features/Companions/CompanionSpawnFactory.cs:186 |
| E-COMPANION-7ee5 | companion_type_missing_id | src/Features/Companions/CompanionSpawnFactory.cs:270 |
| E-COMPANION-aad7 | main_party_missing | src/Features/Companions/CompanionSpawnFactory.cs:246 |
| E-COMPANION-aaee | archetype_missing_id | src/Features/Companions/CompanionSpawnFactory.cs:339 |
| E-COMPANION-e4a8 | empty_catalog | src/Features/Companions/CompanionSpawnFactory.cs:88 |
| E-COMPANION-ed13 | spawn_no_clan | src/Features/Companions/CompanionSpawnFactory.cs:169 |
| E-COMPANION-edd6 | catalog_load_failed | src/Features/Companions/CompanionSpawnFactory.cs:116 |
| E-COMPANION-fe1f | companion_type_no_valid_archetypes | src/Features/Companions/CompanionSpawnFactory.cs:320 |

## COMPANYNEEDS

| Code | Summary | Source |
|---|---|---|
| E-COMPANYNEEDS-a395 | Strategic context config not found - needs prediction unavailable | src/Features/Company/CompanyNeedsManager.cs:193 |

## CONTEXT

| Code | Summary | Source |
|---|---|---|
| E-CONTEXT-47ff | Strategic context config not found - army analysis unavailable | src/Features/Context/ArmyContextAnalyzer.cs:170 |

## DIALOGMANAGER

| Code | Summary | Source |
|---|---|---|
| E-DIALOGMANAGER-1e6d | No conversation hero found during enlistment acceptance | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:4126 |
| E-DIALOGMANAGER-3528 | EnlistmentBehavior.Instance became null before deferred enlistment | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:4152 |
| E-DIALOGMANAGER-f44a | EnlistmentBehavior.Instance is null during enlistment | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:4133 |

## EFFECT

| Code | Summary | Source |
|---|---|---|
| E-EFFECT-68b7 | Failed to load scripted-effects file | src/Features/Content/ScriptedEffectRegistry.cs:59 |

## ENLISTEDDIALOGMANAGER

| Code | Summary | Source |
|---|---|---|
| E-ENLISTEDDIALOGMANAGER-3a7e | QM JSON dialogue failed to load - install may be corrupt/incomplete | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:1230 |

## ENLISTMENT

| Code | Summary | Source |
|---|---|---|
| E-ENLISTMENT-133d | Cross-faction baggage prompt failed | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2410 |
| E-ENLISTMENT-27b8 | Error transferring service to new lord | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:11825 |
| E-ENLISTMENT-547f | Error removing player from army | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3444 |
| E-ENLISTMENT-5b05 | Error triggering post-enlist bag check | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3318 |
| E-ENLISTMENT-6c6b | Error finishing PlayerEncounter before enlistment | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3256 |
| E-ENLISTMENT-9118 | Error stashing belongings | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2835 |
| E-ENLISTMENT-a2f0 | Error returning baggage train stash to inventory | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2737 |
| E-ENLISTMENT-ae8c | Error liquidating belongings | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2921 |
| E-ENLISTMENT-b553 | Error handling baggage transfer choice | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2512 |
| E-ENLISTMENT-d167 | Error smuggling item | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2992 |
| E-ENLISTMENT-d974 | Error opening baggage train stash | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:899 |
| E-ENLISTMENT-e6fd | Error joining lord's kingdom | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3173 |
| E-ENLISTMENT-f31d | Reservist re-entry boost failed | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:996 |
| E-ENLISTMENT-fda6 | Error restoring kingdom during discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3650 |

## EQUIPMENT

| Code | Summary | Source |
|---|---|---|
| E-EQUIPMENT-4e07 | Gear handling on discharge failed | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5746 |

## EVENTCATALOG

| Code | Summary | Source |
|---|---|---|
| E-EVENTCATALOG-a291 | EventCatalog.Initialize failed during file load — catalog will be empty and retry on next access | src/Features/Content/EventCatalog.cs:80 |

## FORMATIONASSIGNMENT

| Code | Summary | Source |
|---|---|---|
| E-FORMATIONASSIGNMENT-0ae6 | Error setting up squad command | src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:1207 |
| E-FORMATIONASSIGNMENT-3e61 | Error during lord attach | src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:693 |
| E-FORMATIONASSIGNMENT-aac4 | Error teleporting player to formation | src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:892 |

## HOME-EVENING

| Code | Summary | Source |
|---|---|---|
| E-HOME-EVENING-5e40 | No eligible storylet at fire | src/Features/Activities/Home/HomeEveningMenuProvider.cs:57 |

## HOME-START

| Code | Summary | Source |
|---|---|---|
| E-HOME-START-348a | Failed to start HomeActivity | src/Features/Activities/Home/HomeActivityStarter.cs:56 |
| E-HOME-START-5528 | Failed to emit departure_imminent beat | src/Features/Activities/Home/HomeActivityStarter.cs:86 |

## INTELAI

| Code | Summary | Source |
|---|---|---|
| E-INTELAI-0439 | base_model_missing | src/Features/CampaignIntelligence/Models/EnlistedArmyManagementModel.cs:66 |

## INTERFACE

| Code | Summary | Source |
|---|---|---|
| E-INTERFACE-086a | Error returning to camp | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:883 |
| E-INTERFACE-0f3a | Error in Talk to My Lord | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3744 |
| E-INTERFACE-2965 | Error opening Master at Arms | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3582 |
| E-INTERFACE-320d | Error opening conversation with companion | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4018 |
| E-INTERFACE-3980 | Error opening conversation with lord | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3897 |
| E-INTERFACE-44ac | Error opening quartermaster conversation | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3646 |
| E-INTERFACE-6014 | Error starting lord conversation | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3842 |
| E-INTERFACE-726f | Error handling baggage train access | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3710 |
| E-INTERFACE-811a | Error showing companion selection inquiry | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3986 |
| E-INTERFACE-9713 | VisitTown failed | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4159 |
| E-INTERFACE-a2e9 | Error showing lord selection | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3856 |
| E-INTERFACE-a6ab | Failed to toggle orders accordion | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4314 |
| E-INTERFACE-ab43 | Error starting companion conversation | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3977 |
| E-INTERFACE-bc27 | Error showing orders menu | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4371 |
| E-INTERFACE-c9df | Error opening debug tools | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:395 |
| E-INTERFACE-e474 | Error in Talk to Companion | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3930 |

## MUSTER

| Code | Summary | Source |
|---|---|---|
| E-MUSTER-01e0 | Failed to complete muster sequence | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:2114 |
| E-MUSTER-1563 | Failed to resolve promissory note | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1644 |
| E-MUSTER-18d2 | Failed to resolve smuggle discharge | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1741 |
| E-MUSTER-1c1d | Unhandled exception in BeginMusterSequence | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1130 |
| E-MUSTER-2462 | Failed to resolve Quartermaster's Deal | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1608 |
| E-MUSTER-4d8b | Failed to finalize discharge | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1698 |
| E-MUSTER-6090 | Error processing final pay discharge | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1935 |
| E-MUSTER-85a2 | Deferred menu activation failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1116 |
| E-MUSTER-98f4 | Pre-activation validation failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1123 |
| E-MUSTER-9f72 | Failed to pre-initialize text variables | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1079 |
| E-MUSTER-a9cc | OnMusterPayInit failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1270 |
| E-MUSTER-b956 | Error showing discharge confirmation | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1857 |
| E-MUSTER-c7a6 | Error processing discharge confirmation | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1845 |
| E-MUSTER-c832 | Error showing final pay confirmation | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1947 |
| E-MUSTER-e3f1 | Failed to resolve corruption pay | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1567 |
| E-MUSTER-e866 | OnMusterCompleteInit failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1390 |
| E-MUSTER-f83e | OnMusterIntroInit failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1239 |
| E-MUSTER-f9a8 | Effect application failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1204 |

## NAVAL

| Code | Summary | Source |
|---|---|---|
| E-NAVAL-b9b9 | Lord has no ships for enlisted player - cannot join naval battle safely | src/Mod.GameAdapters/Patches/NavalBattleShipAssignmentPatch.cs:136 |

## PATCH

| Code | Summary | Source |
|---|---|---|
| E-PATCH-f6cd | PlayerEncounter.Finish crashed while enlisted | src/Mod.GameAdapters/Patches/PlayerEncounterFinishSafetyPatch.cs:166 |

## PAY

| Code | Summary | Source |
|---|---|---|
| E-PAY-56f4 | Error resolving smuggle discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5599 |

## QUALITY

| Code | Summary | Source |
|---|---|---|
| E-QUALITY-6370 | Failed to load quality definition file | src/Features/Qualities/QualityCatalog.cs:53 |

## QUARTERMASTER

| Code | Summary | Source |
|---|---|---|
| E-QUARTERMASTER-4df4 | Both QM and enlisted lord have no party — cannot open conversation with correct scene | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3609 |
| E-QUARTERMASTER-6852 | GetOrCreateQuartermaster returned null or dead hero while enlisted | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3639 |
| E-QUARTERMASTER-8adc | Error processing equipment variant request | src/Features/Equipment/Behaviors/QuartermasterManager.cs:1428 |
| E-QUARTERMASTER-9cb7 | Cannot find troop template for culture - quartermaster hero creation will fail | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9947 |
| E-QUARTERMASTER-b40e | Cannot open sell popup: QuartermasterManager.Instance is null | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:3318 |

## QUARTERMASTERUI

| Code | Summary | Source |
|---|---|---|
| E-QUARTERMASTERUI-5699 | No food items found - cannot build provisions grid | src/Features/Equipment/UI/QuartermasterProvisionsVM.cs:106 |

## RECRUITGRANT

| Code | Summary | Source |
|---|---|---|
| E-RECRUITGRANT-90ad | Could not find recruit troop for culture and formation | src/Features/Retinue/Core/RetinueRecruitmentGrant.cs:138 |

## RETIREMENT

| Code | Summary | Source |
|---|---|---|
| E-RETIREMENT-100b | Error applying pension on discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5912 |
| E-RETIREMENT-14b8 | Error applying subsequent re-enlistment bonuses | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9116 |
| E-RETIREMENT-973e | Error finalizing discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5541 |
| E-RETIREMENT-da8e | Error applying relation bonuses | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9042 |

## STORYLET

| Code | Summary | Source |
|---|---|---|
| E-STORYLET-01f5 | Failed to load storylet file | src/Features/Content/StoryletCatalog.cs:62 |
| E-STORYLET-d94d | StoryletCatalog loaded 0 storylets despite JSON files present — check parser or JSON schema | src/Features/Content/StoryletCatalog.cs:71 |

## STORYLET-ADAPT

| Code | Summary | Source |
|---|---|---|
| E-STORYLET-ADAPT-9219 | DrainPendingEffects failed | src/Features/Content/StoryletEventAdapter.cs:135 |

## TROOPSELECTION

| Code | Summary | Source |
|---|---|---|
| E-TROOPSELECTION-10e0 | Failed to apply selected troop equipment | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:547 |
| E-TROOPSELECTION-569b | Error in troop selection | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:690 |
| E-TROOPSELECTION-5cf8 | Failed to open Master at Arms popup | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:274 |
| E-TROOPSELECTION-9828 | Master at Arms popup failed | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:161 |
| E-TROOPSELECTION-a67e | Master at Arms apply failed | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:147 |
| E-TROOPSELECTION-b0fa | Failed to switch back to enlisted status | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:299 |
