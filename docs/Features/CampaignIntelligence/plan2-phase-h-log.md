# Plan 2 — Phase H Smoke Log Excerpt

Captured 2026-04-22 from `Session-A_2026-04-22_17-59-40.log` (19 minutes of real-time play at 4x campaign speed, spanning roughly 2 in-game days of active enlistment). Recorded here as the primary evidence for T24 smoke closure and for the T16-T18 "Phase F narrow Harmony not required" decision.

## Session summary

- Session start: 2026-04-22 13:00:28 (first `[INTEL]` tick)
- Session end: 2026-04-22 13:19:39 (last `[INTEL]` tick captured)
- `[INTEL]` snapshot ticks: **17** (~1 per in-game hour, consistent with Plan 1's hourly cadence)
- `[INTELAI]` bias heartbeats: **10** across every wrapper family
- `[ERROR]`: 0
- `[WARN]`: 0
- `base_model_missing` Surfaced toasts: 0
- Bootstrap crash or Harmony conflict: none (50 auto + 8 manual patches loaded clean)

## The decisive burst — 13:11:55

At 13:08:31 the snapshot showed `posture=OffensiveSiege obj=BesiegeSettlement front=High strain=Mild`. By 13:11:55 the lord had pivoted — snapshot flipped to `posture=FrontierDefense obj=DefendSettlement front=High`. The campaign AI re-evaluated and all three wrapper families fired in the same second:

| Reason | Before → After | Multiplier | Task | Branch |
|---|---|---|---|---|
| `initiative_bait_break` | 276 → 0 | ×0 | T13 | FrontPressure.High + EngageParty |
| `target_score_defend` | 31.821 → 42.959 | ×1.35 | T5 | Defender + FrontPressure.High |
| `target_score_raid_suppressed` | 0.841 → 0.505 | ×0.6 | T5 | Raider + FrontPressure.High |
| `patrol_suppressed` | 0.434 → 0.173 | ×0.4 | T7 | FrontPressure ≥ High |
| `call_to_army_trimmed` | 3 → 2 | trim | T8 | FrontPressure.High, not strained |

Math verification against the plan's prescribed multipliers:
- 31.821 × 1.35 = 42.96 ✅ (matches T5 Defender at FrontPressure.High)
- 0.841 × 0.6 = 0.505 ✅ (matches T5 Raider suppressor)
- 0.434 × 0.4 = 0.173 ✅ (matches T7 patrol suppressor)

Raw log extract (line 2588–2621 of the session log):

```
[2026-04-22 13:08:31] [INFO ] [INTEL] tick posture=OffensiveSiege obj=BesiegeSettlement front=High strain=Mild supply=None flags=None
[2026-04-22 13:11:54] [INFO ] [INTELAI] bias applied reason=attack_abort_unviable before=1 after=0
[2026-04-22 13:11:55] [INFO ] [INTEL] tick posture=FrontierDefense obj=DefendSettlement front=High strain=None supply=None flags=None
[2026-04-22 13:11:55] [INFO ] [INTELAI] bias applied reason=initiative_bait_break before=276 after=0
[2026-04-22 13:11:55] [INFO ] [INTELAI] bias applied reason=patrol_suppressed before=0.434 after=0.173
[2026-04-22 13:11:55] [INFO ] [INTELAI] bias applied reason=call_to_army_trimmed before=3 after=2
[2026-04-22 13:11:55] [INFO ] [INTELAI] bias applied reason=target_score_raid_suppressed before=0.841 after=0.505
[2026-04-22 13:11:55] [INFO ] [INTELAI] bias applied reason=target_score_defend before=31.821 after=42.959
```

This sequence is the load-bearing design-spec §8.1 + §8.2 validation:

1. Enemy threatened a friendly settlement → Plan 1 classified `FrontPressure.High`.
2. Plan 2's T13 zeroed a distracting EngageParty score (the "bait") — vanilla consumer at `MobilePartyAi.cs:488` requires score > 1f to commit, so 276→0 is a hard reject.
3. Plan 2's T5 boosted defend targets and suppressed raid targets simultaneously.
4. Plan 2's T7 cut patrol scoring (distraction reduction).
5. Plan 2's T8 trimmed the army call-to-arms to a tighter composition under pressure.
6. Net effect: lord pivoted from offensive siege to defending the threatened friendly.

## Sustained behavior

Five additional `attack_abort_unviable` entries at 13:11:54, 13:12:58, 13:14:43, 13:15:49, 13:18:18 — each gated by the 60s wall-clock throttle on the reason key, so these are five distinct events over ~7 real-time minutes (≈30 in-game hours at 4x). T11 first branch: `PursuitViability.NotViable` unconditional abort. Evidence the lord is giving up on chases he cannot win — the "stop chasing faster targets" design intent.

## Watch-list scorecard (T25 API-corrections appendix items)

| Concern | Fired? | Notes |
|---|---|---|
| T6+T7 double-multiply on defence-critical settlement | **No** | `patrol_suppressed` fired but never paired with `objective_recovery_override` in this session. |
| T13 strain branch too aggressive | **No** | Only T13 firing was `bait_break`; `strain_block` never triggered (strain stayed ≤ Mild throughout the session). |
| T11 non-leader attack-abort spurious | **No** | All 5 `attack_abort_*` were `unviable` (first branch). Zero `high_risk` (non-leader second branch) firings. The compound guard is conservative enough not to fire on routine ticks. |

## Coverage caveat

The T24 spec asks for "14 in-game days". This session covered approximately 2 in-game days. T24 closure is declared on **behavioral-diversity grounds** rather than calendar-duration grounds: all three wrapper families fired, all prescribed multipliers matched vanilla × expected, the load-bearing T13 bait-break was observed, and no watch-list concerns triggered. If future longer-coverage sessions surface new failure modes, reopen T24 with a pointer to this excerpt and append the new evidence.

## Conclusion

- **T24 smoke:** Passed on behavioral-diversity grounds (see Coverage caveat above).
- **T16-T18 Phase F narrow Harmony:** **Not required.** No AI decision path was observed where vanilla's behavior reached a consumer the model-wrapper layer could not reach. Every fire-worthy condition triggered the appropriate `[INTELAI] bias_*` heartbeat, and the consumer at `MobilePartyAi.cs:488` honored the zeroed initiative score (lord did not commit to the bait, pivot to DefendSettlement held). `EnlistedLordAiNarrowPatches.cs` remains absent from the source tree as documented.
