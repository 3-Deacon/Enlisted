# Campaign Intelligence — Reference

**Status:** Live. Campaign Intelligence is the hidden truth layer for the enlisted lord only. It provides read-only strategic texture and AI bias inputs while the player is enlisted, then reverts to vanilla immediately when service ends.

## Runtime Contract

- `EnlistedCampaignIntelligenceBehavior` owns one reduced `EnlistedLordIntelligenceSnapshot`.
- `Current` returns `null` when the player is not enlisted or before the first hourly recompute.
- The snapshot stores enums, numbers, flags, and lightweight names; consumers treat it as read-only and do not hold TaleWorlds object references from it.
- Recompute runs on `HourlyTickEvent`; event hooks set pending `RecentChangeFlags` for war, peace, army creation/dispersal, and map-event entry.
- Expected log marker: `INTEL hourly_recompute`.

## Consumers

| Consumer | Contract |
| :--- | :--- |
| Plan 2 Lord AI wrappers | `EnlistedTargetScoreModel`, `EnlistedArmyManagementModel`, and `EnlistedMobilePartyAiModel` bias vanilla model outputs only after `EnlistedAiGate.TryGetSnapshotForParty` passes. Failed gates delegate to `BaseModel` unmodified. |
| Plan 3 Signal Projection | `EnlistedCampaignSignalBuilder` projects snapshot state into floor signal families; `EnlistedSignalEmitterBehavior` owns throttled hourly emission, cooldown, diversity, and `StoryDirector.EmitCandidate`. |
| Plan 4 Duty Opportunities | `EnlistedDutyOpportunityBuilder` projects snapshot + current duty profile into opportunities; `EnlistedDutyEmitterBehavior` resolves storylets and persists cooldowns in `DutyCooldownStore`. |
| Plan 5 Career Debug | `CareerDebugHotkeysBehavior` can dump snapshot fields for smoke verification. |

If the enlisted lord is attached as a non-leader army member, strategic authority belongs to the army leader. Plan 2 does not bias the army leader on the enlisted lord's behalf. `SettlementValueModel` is intentionally not wrapped; settlement value already feeds target scoring.

## Save Registration

`EnlistedLordIntelligenceSnapshot` owns class offset 48 in `EnlistedSaveDefiner`. Related campaign-intelligence claims:

- class 49: `SignalEmissionRecord`
- class 50: `DutyCooldownStore`
- enum 86-98: Plan 1 campaign-intelligence enums
- enum 99-103: Plan 3 signal/content metadata enums

Grep `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` before claiming anything near this range.

## Day-To-Day References

- [Signal Projection Mapping](signal-projection-mapping.md)
- [Duty Opportunity Mapping](duty-opportunity-mapping.md)
- [Plan 2 Phase H Smoke Log](plan2-phase-h-log.md)
- [Plan 3 Verification](../../superpowers/plans/2026-04-21-signal-projection-verification.md)
- [Plan 4 Verification](../../superpowers/plans/2026-04-21-duty-opportunities-verification.md)
