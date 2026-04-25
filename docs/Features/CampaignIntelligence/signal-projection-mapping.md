# Signal Projection Mapping

Living reference mapping Plan 1 campaign-intelligence snapshot state (and its `RecentChangeFlags` bits) to the ten signal families produced by `EnlistedCampaignSignalBuilder`. Used by:

- `EnlistedCampaignSignalBuilder` (Plan 3 T6) — picks which family to project for a given snapshot.
- Floor-storylet authors (Plan 3 T16-T25) — keeps authored pool content aligned with the snapshot-to-signal contract.

## Trigger → Signal Family

| Trigger (snapshot state or flag) | Signal family | Default confidence | Example inference |
|---|---|---|---|
| `SupplyPressure >= Strained` | QuartermasterWarning | Medium | "Stores may need to move sooner than expected." |
| `ArmyStrain >= Elevated` | TensionIncident | Medium | "The men are restless at the campfires." |
| `FrontPressure >= High` | Rumor | Low | "Enemy riders seen to the east — unconfirmed." |
| `RecentChangeFlags has SiegeTransition` | Dispatch | High | "The army marches; camp breaks by dawn." |
| `RecentChangeFlags has ConfirmedScoutInfo` | ScoutReturn | High | "Scouts returned; numbers confirmed." |
| `PrisonerStakes >= Medium AND RecentChangeFlags has PrisonerUpdate` | PrisonerReport | Medium | "Word on the prisoner — not all good." |
| `SupplyPressure == None AND ArmyStrain == None AND FrontPressure == None` | SilenceWithImplication | Low | (no inference text — the silence IS the point) |
| `RecentChangeFlags has ObjectiveShift` | OrderShift | High | "New orders came down; old plans scrapped." |
| `RecentChangeFlags has NewThreat` | AftermathNotice | Medium | "Trouble on the road — some didn't make it back." |
| `RecoveryNeed >= High` | CampTalk | Low | "Talk in the mess: how much longer?" |

## Default Source Perspective per Family

| Family | Default source perspective |
|---|---|
| Rumor | Rumor |
| CampTalk | OwnObservation |
| Dispatch | Courier |
| OrderShift | Sergeant |
| QuartermasterWarning | Quartermaster |
| ScoutReturn | Narrator (describing the scout's return) |
| PrisonerReport | Narrator |
| AftermathNotice | Narrator |
| TensionIncident | OwnObservation |
| SilenceWithImplication | Narrator |

## Runtime Contract

- `EnlistedCampaignSignalBuilder` is pure: snapshot in, candidate signal list out. It does not read campaign state or mutate cooldowns.
- `EnlistedSignalEmitterBehavior` owns hourly emission, `OrdersNewsFeedThrottle`, per-storylet cooldown, weighted diversity, and `StoryDirector.EmitCandidate`.
- Authored corpus: 48 floor storylets across 10 `floor_<family>.json` files.
- Cooldown state persists in `SignalEmissionRecord` at class offset 49. Signal/content enums use offsets 99-103.
- `DailyDriftApplicator.FlushSummary` routes through the signal emitter and only clears pending XP after `EmitExternalSignal` returns true.
- Current floor-storylet triggers are intentionally broad (`is_enlisted`). Snapshot predicates now exist, so future authoring can tighten per-storylet relevance.
- Expected log markers: `SIGNAL` family counters, throttle rejections, and no cooldown stamp when `StoryDirector` is unavailable.
