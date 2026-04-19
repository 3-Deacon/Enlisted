# Error Codes

This registry is **auto-generated** from `ModLogger.Surfaced(...)` calls
by `Tools/Validation/generate_error_codes.py`. Do not hand-edit — your
changes will be overwritten on the next run.

For pre-redesign codes (format `E-SUBSYSTEM-NNN`), see
[error-codes-archive.md](error-codes-archive.md).

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

## RETIREMENT

| Code | Summary | Source |
|---|---|---|
| E-RETIREMENT-100b | Error applying pension on discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5787 |
| E-RETIREMENT-14b8 | Error applying subsequent re-enlistment bonuses | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:8940 |
| E-RETIREMENT-973e | Error finalizing discharge | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:5416 |
| E-RETIREMENT-da8e | Error applying relation bonuses | src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:8866 |
