# Error Codes

This registry is **auto-generated** from `ModLogger.Surfaced(...)` calls
by `Tools/Validation/generate_error_codes.py`. Do not hand-edit — your
changes will be overwritten on the next run.

For pre-redesign codes (format `E-SUBSYSTEM-NNN`), see
[error-codes-archive.md](error-codes-archive.md).

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

## CAMPLIFE

| Code | Summary | Source |
|---|---|---|
| E-CAMPLIFE-0311 | Failed to parse camp_opportunities.json — decisions won't appear in menu. Check JSON syntax. | src/Features/Camp/CampOpportunityGenerator.cs:1789 |
| E-CAMPLIFE-2336 | No opportunity definitions found — check camp_opportunities.json exists and has valid content. Decisions won't appear in the accordion menu. | src/Features/Camp/CampOpportunityGenerator.cs:480 |
| E-CAMPLIFE-537e | Opportunity definitions not yet loaded - loading now (this is normal on first access after load) | src/Features/Camp/CampOpportunityGenerator.cs:474 |
| E-CAMPLIFE-7476 | No candidates passed filtering — check tier/context requirements in camp_opportunities.json match current state. | src/Features/Camp/CampOpportunityGenerator.cs:535 |
| E-CAMPLIFE-7b2d | No opportunities array found in camp_opportunities.json — file may be corrupt or invalid. Decisions won't appear in menu. | src/Features/Camp/CampOpportunityGenerator.cs:1767 |
| E-CAMPLIFE-803c | camp_opportunities.json not found — decisions won't appear in menu. Verify mod installation is complete. | src/Features/Camp/CampOpportunityGenerator.cs:1754 |

## DIALOGMANAGER

| Code | Summary | Source |
|---|---|---|
| E-DIALOGMANAGER-1e6d | No conversation hero found during enlistment acceptance | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:4893 |
| E-DIALOGMANAGER-3528 | EnlistmentBehavior.Instance became null before deferred enlistment | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:4919 |
| E-DIALOGMANAGER-f44a | EnlistmentBehavior.Instance is null during enlistment | src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:4900 |

## ENLISTMENT

| Code | Summary | Source |
|---|---|---|
| E-ENLISTMENT-133d | Cross-faction baggage prompt failed | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2301 |
| E-ENLISTMENT-27b8 | Error transferring service to new lord | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:11526 |
| E-ENLISTMENT-547f | Error removing player from army | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3384 |
| E-ENLISTMENT-5b05 | Error triggering post-enlist bag check | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3267 |
| E-ENLISTMENT-6c6b | Error finishing PlayerEncounter before enlistment | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3205 |
| E-ENLISTMENT-9118 | Error stashing belongings | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2726 |
| E-ENLISTMENT-a2f0 | Error returning baggage train stash to inventory | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2628 |
| E-ENLISTMENT-ae8c | Error liquidating belongings | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2812 |
| E-ENLISTMENT-b553 | Error handling baggage transfer choice | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2403 |
| E-ENLISTMENT-d167 | Error smuggling item | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:2883 |
| E-ENLISTMENT-d974 | Error opening baggage train stash | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:851 |
| E-ENLISTMENT-e6fd | Error joining lord's kingdom | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3122 |
| E-ENLISTMENT-f31d | Reservist re-entry boost failed | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:948 |
| E-ENLISTMENT-fda6 | Error restoring kingdom during discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3590 |

## EQUIPMENT

| Code | Summary | Source |
|---|---|---|
| E-EQUIPMENT-4e07 | Gear handling on discharge failed | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5621 |

## FORMATIONASSIGNMENT

| Code | Summary | Source |
|---|---|---|
| E-FORMATIONASSIGNMENT-0ae6 | Error setting up squad command | src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:1288 |
| E-FORMATIONASSIGNMENT-3e61 | Error during lord attach | src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:688 |
| E-FORMATIONASSIGNMENT-aac4 | Error teleporting player to formation | src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:887 |

## INTERFACE

| Code | Summary | Source |
|---|---|---|
| E-INTERFACE-0688 | Error confirming desertion | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:5192 |
| E-INTERFACE-086a | Error returning to camp | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:900 |
| E-INTERFACE-0f3a | Error in Talk to My Lord | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4592 |
| E-INTERFACE-27cd | Error granting temporary leave | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4995 |
| E-INTERFACE-27f8 | Error in free desertion | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:5075 |
| E-INTERFACE-2965 | Error opening Master at Arms | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4303 |
| E-INTERFACE-2cd0 | Error opening baggage request conversation | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4434 |
| E-INTERFACE-3980 | Error opening conversation with lord | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4745 |
| E-INTERFACE-430f | Error returning from desertion menu | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:5166 |
| E-INTERFACE-44ac | Error opening quartermaster conversation | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4367 |
| E-INTERFACE-44fc | Error in Ask for Leave | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4958 |
| E-INTERFACE-6014 | Error starting lord conversation | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4690 |
| E-INTERFACE-64e9 | Error opening desertion menu | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:5026 |
| E-INTERFACE-726f | Error handling baggage train access | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4558 |
| E-INTERFACE-7648 | Error requesting leave | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4945 |
| E-INTERFACE-9713 | VisitTown failed | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4886 |
| E-INTERFACE-a2e9 | Error showing lord selection | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4704 |
| E-INTERFACE-a6ab | Failed to toggle orders accordion | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:6188 |
| E-INTERFACE-bc27 | Error showing orders menu | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:6765 |
| E-INTERFACE-c9df | Error opening debug tools | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:412 |
| E-INTERFACE-f0bd | Failed to toggle decisions accordion | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:6230 |
| E-INTERFACE-f8a1 | Failed to select decision slot | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:6644 |

## MUSTER

| Code | Summary | Source |
|---|---|---|
| E-MUSTER-01e0 | Failed to complete muster sequence | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:2155 |
| E-MUSTER-1563 | Failed to resolve promissory note | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1678 |
| E-MUSTER-18d2 | Failed to resolve smuggle discharge | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1775 |
| E-MUSTER-1c1d | Unhandled exception in BeginMusterSequence | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1128 |
| E-MUSTER-2462 | Failed to resolve Quartermaster's Deal | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1642 |
| E-MUSTER-4852 | Stage transition failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1203 |
| E-MUSTER-4d8b | Failed to finalize discharge | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1732 |
| E-MUSTER-6090 | Error processing final pay discharge | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1969 |
| E-MUSTER-85a2 | Deferred menu activation failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1114 |
| E-MUSTER-98f4 | Pre-activation validation failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1121 |
| E-MUSTER-9f72 | Failed to pre-initialize text variables | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1077 |
| E-MUSTER-a9cc | OnMusterPayInit failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1304 |
| E-MUSTER-b956 | Error showing discharge confirmation | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1891 |
| E-MUSTER-c7a6 | Error processing discharge confirmation | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1879 |
| E-MUSTER-c832 | Error showing final pay confirmation | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1981 |
| E-MUSTER-e3f1 | Failed to resolve corruption pay | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1601 |
| E-MUSTER-e866 | OnMusterCompleteInit failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1424 |
| E-MUSTER-f83e | OnMusterIntroInit failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1273 |
| E-MUSTER-f9a8 | Effect application failed | src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:1238 |

## PATCH

| Code | Summary | Source |
|---|---|---|
| E-PATCH-f6cd | PlayerEncounter.Finish crashed while enlisted | src/Mod.GameAdapters/Patches/PlayerEncounterFinishSafetyPatch.cs:166 |

## PAY

| Code | Summary | Source |
|---|---|---|
| E-PAY-56f4 | Error resolving smuggle discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5474 |

## QUARTERMASTER

| Code | Summary | Source |
|---|---|---|
| E-QUARTERMASTER-4df4 | Both QM and enlisted lord have no party — cannot open conversation with correct scene | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4330 |
| E-QUARTERMASTER-6852 | GetOrCreateQuartermaster returned null or dead hero while enlisted | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4360 |
| E-QUARTERMASTER-8adc | Error processing equipment variant request | src/Features/Equipment/Behaviors/QuartermasterManager.cs:1442 |
| E-QUARTERMASTER-e56e | Both QM and enlisted lord have no party — cannot open baggage-request conversation with correct scene | src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:4395 |

## RETIREMENT

| Code | Summary | Source |
|---|---|---|
| E-RETIREMENT-100b | Error applying pension on discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5787 |
| E-RETIREMENT-14b8 | Error applying subsequent re-enlistment bonuses | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:8940 |
| E-RETIREMENT-973e | Error finalizing discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5416 |
| E-RETIREMENT-da8e | Error applying relation bonuses | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:8866 |

## TROOPSELECTION

| Code | Summary | Source |
|---|---|---|
| E-TROOPSELECTION-10e0 | Failed to apply selected troop equipment | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:626 |
| E-TROOPSELECTION-569b | Error in troop selection | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:769 |
| E-TROOPSELECTION-5cf8 | Failed to open Master at Arms popup | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:287 |
| E-TROOPSELECTION-9828 | Master at Arms popup failed | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:174 |
| E-TROOPSELECTION-a67e | Master at Arms apply failed | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:160 |
| E-TROOPSELECTION-b0fa | Failed to switch back to enlisted status | src/Features/Equipment/Behaviors/TroopSelectionManager.cs:312 |
