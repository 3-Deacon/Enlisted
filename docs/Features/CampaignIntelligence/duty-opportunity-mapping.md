# Duty Opportunity Mapping

Living reference mapping Plan 1 campaign-intelligence snapshot state to the duty-opportunity pools produced by `EnlistedDutyOpportunityBuilder`. Used by:

- `EnlistedDutyOpportunityBuilder` — projects snapshot + `CurrentDutyProfile` into `List<DutyOpportunity>` each hourly tick.
- `EnlistedDutyEmitterBehavior` — picks the highest-priority eligible opportunity, resolves a matching storylet from the pool, emits via `StoryDirector`.
- Ambient duty-pool authors (profile files `duty_<profile>.json` + `transition.json`) — keeps authored content aligned with the snapshot-to-pool contract.

Opportunity shapes (`DutyOpportunityShape`): `Episodic` resolves inside a storylet flow (emitted at `StoryTier.Log`); `ArcScale` proposes a named-order archetype via the existing `OrderActivity` / `NamedOrderArcRuntime.SpliceArc` pipeline (emitted at `StoryTier.Modal`). Snapshot properties referenced below match `EnlistedLordIntelligenceSnapshot` field names, not plan-era drafts.

## Trigger → Opportunity Pool

| Trigger (snapshot state or flag) | Shape | Pool prefix | Priority | Notes |
|---|---|---|---|---|
| `SupplyPressure >= Strained` AND profile = `marching` | Episodic | `duty_marching_` | 3.0 | Ration-tension / wagon-forage content |
| `ArmyStrain >= Elevated` AND profile in settlement/camp | Episodic | `duty_<profile>_` | 2.5 | Discipline slippage content |
| `FrontPressure >= High` AND profile = `garrisoned` | Episodic | `duty_garrisoned_` | 2.8 | Alert-posture content |
| `RecentChanges has ObjectiveShift` AND profile = `escorting` | Episodic | `duty_escorting_` | 3.2 | Sudden redirect content |
| `InformationConfidence == Low` AND profile = `marching` | ArcScale | `order_scout_` | 2.0 | Proposes scout named order |
| `RecoveryNeed >= High` AND profile = `garrisoned` | ArcScale | `order_treat_wounded_` | 2.5 | Proposes field-medic named order |
| `Posture == OffensiveSiege` AND profile = `besieging` | ArcScale | `order_siege_works_` | 2.8 | Proposes siege-works named order |
| `PrisonerStakes >= Medium` | Episodic | `duty_<profile>_` | 2.2 | Prisoner-relevant storylet from current profile pool |
| Quiet stretch (all strain None, no change flags) | Episodic | `duty_<profile>_` | 1.0 | Routine ambient-texture fallback — always emitted so every tick has at least one candidate |
| `flag:lord_imprisoned` | Episodic | `duty_imprisoned_` | fallback | Forces imprisoned-profile content while the lord is captive |

## Priority Ordering

Higher priorities win. The Builder sorts candidates by descending `Priority` before returning. When two candidates tie, insertion order breaks the tie — ArcScale opportunities are added before Episodic so they surface first at equal priority.

## Profile Pool Class-Affinity Bias

Each duty-profile pool carries class-affinity multipliers on its authored storylets. The emitter's weighted-diversity picker applies these at selection time; the Builder does not read them.

| Profile | Heavy bias | Moderate | Thin |
|---|---|---|---|
| `garrisoned` | OneHanded / Polearm / Athletics (Infantry); Bow / Scouting (Ranged); Riding / Polearm (Cavalry); Bow / Riding (HorseArcher) | Charm, Steward, Leadership, Medicine, Trade | Roguery, Throwing, Crossbow |
| `marching` | Athletics, Riding, Scouting, Tactics | Charm, Steward, Leadership | Smithing |
| `besieging` | Engineering, Crossbow, Bow, Athletics | Tactics, Medicine, Leadership | Roguery, Trade |
| `raiding` | Athletics, Roguery, OneHanded, Riding | Charm, Tactics, Scouting | Engineering, Steward |
| `escorting` | Tactics, Leadership, Riding | Scouting, Charm | Smithing, Engineering (thinner profile per Spec 2 §15) |
| `wandering` | Scouting, Trade, Roguery | Charm, Athletics, Riding | Engineering, Steward |
| `imprisoned` | Roguery, Charm | Athletics, Medicine | Ranged / mounted skills (locked out) |

## Cooldown Defaults

Storylets default to a 36 in-game-hour cooldown, tracked per storylet ID in `DutyCooldownStore`. Authors may override per storylet via the `cooldown_days` schema field.

## Runtime Contract

- Authored corpus: 140 storylets across seven `duty_<profile>.json` files plus `transition.json`.
- `EnlistedDutyOpportunityBuilder` is pure and stable-sorts by descending priority. Equal priorities preserve insertion order, so ArcScale candidates win ties.
- ArcScale opportunities resolve a specific accept storylet id, not a prefix scan. Modal candidates must call `StoryletEventAdapter.BuildModal` and set `candidate.InteractiveEvent`, or they fall through to Log.
- `EnlistedDutyEmitterBehavior` filters ArcScale opportunities out while `OrderActivity.ActiveNamedOrder != null`.
- Transition interrupts fire from `DutyProfileBehavior.CommitTransition` with fallback order: `transition_<old>_to_<new>_mid_<orderId>` -> `transition_<old>_to_<new>_generic` -> `transition_generic_interrupted`.
- Transition candidates use `ChainContinuation = true` to bypass the in-game floor and category cooldown while retaining wall-clock pacing.
- Validator coverage for `(profile, skill)` cells is warning-level `>=1`. Known accepted gaps are imprisoned combat/mounted skills and wandering Leadership.
- Expected smoke markers: `DUTY heartbeat`, `DUTY daily_counts`, `DUTY emitted Episodic`, `DUTY emitted ArcScale`, and `DUTYPROFILE transition_emitted`.
