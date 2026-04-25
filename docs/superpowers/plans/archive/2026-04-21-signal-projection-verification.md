# Plan 3 — Signal Projection Verification

**Status:** Code-level verification complete. T28 (14-day in-game smoke) pending human operator.

**Shipped:** 2026-04-22 on `development`. Commit range `5fedf88..189e75c` (25 feature commits + this status-closure commit).
**Plan doc:** [docs/superpowers/plans/2026-04-21-signal-projection.md](2026-04-21-signal-projection.md)
**Design reference:** [docs/superpowers/specs/2026-04-21-enlisted-campaign-intelligence-design.md](../specs/2026-04-21-enlisted-campaign-intelligence-design.md)

---

## 1. Summary

Plan 3 ships the **signal projection layer** that translates Plan 1's `EnlistedLordIntelligenceSnapshot` (ground truth about the enlisted lord's strategic state) into **imperfect player-visible signals** — rumors, camp talk, dispatches, order-shift cues, quartermaster warnings, scout returns, prisoner reports, aftermath notices, tension incidents, and pointed silence — delivered through `StoryDirector` as floor-tier storylets.

What ships:
- Ten signal families (enum-keyed) with confidence + change-type metadata.
- Pure projector `EnlistedCampaignSignalBuilder` (snapshot → `List<EnlistedCampaignSignal>`).
- Hourly emitter `EnlistedSignalEmitterBehavior` (throttle + pool lookup + cooldown + weighted-diversity picker + per-family counters).
- `StoryCandidate` signal-metadata extensions (5 optional fields + `SourcePerspective`/`SignalRecency` enums).
- `DailyDriftApplicator.FlushSummary` rewired through the signal pipeline (retains pending XP on throttle rejection).
- 48 authored floor storylets across 10 families.

What does NOT ship here (deferred by design):
- Lord AI interventions (Plan 2 — independent track).
- Ambient duty opportunity storylets (Plan 4).
- Snapshot-backed trigger predicates for per-storylet gating (Plan 4 Phase B.5).
- Path crossroads, culture/trait overlays (Plan 5).
- Live 14-day cadence verification under varied lord states (T28, pending human operator).

---

## 2. Architecture shipped

All new code lives under `src/Features/CampaignIntelligence/Signals/` unless noted.

| Component | Role | Location |
| :--- | :--- | :--- |
| `SignalType` enum | 10 signal families (Rumor, CampTalk, Dispatch, OrderShift, QuartermasterWarning, ScoutReturn, PrisonerReport, AftermathNotice, TensionIncident, SilenceWithImplication) | `Signals/SignalType.cs` |
| `SignalConfidence` enum | Vague / Probable / Confirmed | `Signals/SignalConfidence.cs` |
| `SignalChangeType` enum | NewDevelopment / Escalation / Deescalation / OngoingState | `Signals/SignalChangeType.cs` |
| `EnlistedCampaignSignal` | Transient POCO carrying type, confidence, change-type, pool prefix, drift-XP payload | `Signals/EnlistedCampaignSignal.cs` |
| `SignalEmissionRecord` | Persistent per-storylet cooldown (`CampaignTime` last-fired + count) — save-definer offset 49 | `Signals/SignalEmissionRecord.cs` |
| `EnlistedCampaignSignalBuilder` | Pure projector from snapshot to ranked signal list | `Signals/EnlistedCampaignSignalBuilder.cs` |
| `EnlistedSignalEmitterBehavior` | Hourly-tick `CampaignBehaviorBase` — throttle, pool lookup, cooldown enforcement, weighted picker, per-family counters, error categorization | `Signals/EnlistedSignalEmitterBehavior.cs` |
| `StoryCandidate` metadata fields | `SignalType?`, `SignalConfidence?`, `SignalChangeType?`, `SourcePerspective?`, `SignalRecency?` + two enums in `Features.Content` | `src/Features/Content/StoryCandidate.cs` + sibling enum files |
| `DailyDriftApplicator.FlushSummary` rewire | Builds a synthetic `DailyDrift` signal, routes through `EmitExternalSignal`, returns `bool` so callers retain pending XP on throttle reject | `src/Features/CampaignIntelligence/DailyDriftApplicator.cs` |

**Key architectural properties:**
- **Pure builder.** `EnlistedCampaignSignalBuilder` has no side effects, no time, no randomness — snapshot in, ranked `List` out. Trivially unit-testable.
- **Throttle discipline.** `EnlistedSignalEmitterBehavior.OnHourlyTick` does the single throttle check. Individual emissions call `EmitExternalSignal` which returns `bool`; callers use that to decide whether to clear pending state.
- **Cooldown correctness.** `EmitPicked` short-circuits with an `Expected` log if `StoryDirector.Instance` is null — the cooldown stamp is skipped so the storylet isn't shelved for 36 hours on a no-op.
- **Weighted diversity.** The picker biases against storylets used recently within the family, preventing visible repetition without a hard ban.

---

## 3. Authored content

10 floor storylet families, **48 storylets total**, authored in `ModuleData/Enlisted/Storylets/`:

| File | Count | Family |
| :--- | ---: | :--- |
| `floor_rumor.json` | 5 | Rumor (low confidence, background churn) |
| `floor_camp_talk.json` | 6 | CampTalk (soldier chatter; baseline ambience) |
| `floor_dispatch.json` | 5 | Dispatch (courier / written order cues) |
| `floor_order_shift.json` | 5 | OrderShift (formation / march intent change) |
| `floor_quartermaster_warning.json` | 5 | QuartermasterWarning (supply / readiness drag) |
| `floor_scout_return.json` | 5 | ScoutReturn (recon confidence increase / decrease) |
| `floor_prisoner_report.json` | 4 | PrisonerReport (enemy stakes, interrogation) |
| `floor_aftermath_notice.json` | 5 | AftermathNotice (post-battle / siege recovery) |
| `floor_tension_incident.json` | 5 | TensionIncident (army strain, discipline brittle) |
| `floor_silence_with_implication.json` | 3 | SilenceWithImplication (pointed quiet) |

All 48 storylets are Tier = Floor, bare-bones triggers (`["is_enlisted"]`), and lean on SignalBuilder's family-level snapshot gating rather than per-storylet predicates. Localization is inline `{=key}Fallback` (192 new loc-keys; see §6 for localization-sync deferral).

---

## 4. Save-definer footprint

Claimed under Plan 3 in `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`:

| Offset | Kind | Type |
| ---: | :--- | :--- |
| 49 | Class | `Features.CampaignIntelligence.Signals.SignalEmissionRecord` |
| 99 | Enum | `Features.CampaignIntelligence.Signals.SignalType` |
| 100 | Enum | `Features.CampaignIntelligence.Signals.SignalConfidence` |
| 101 | Enum | `Features.CampaignIntelligence.Signals.SignalChangeType` |
| 102 | Enum | `Features.Content.SourcePerspective` |
| 103 | Enum | `Features.Content.SignalRecency` |

Class offsets 50-60 remain reserved for Specs 3–5 per AGENTS.md Rule #11. Enum offsets 104+ are available.

No new container definitions were required — `SignalEmissionRecord` fields serialize via existing registrations.

---

## 5. Build + validator state

At T26 close (commit `189e75c`):

- **Build:** `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` — **0 errors, 0 warnings**.
- **Validator:** `python Tools/Validation/validate_content.py` — **PASS WITH WARNINGS**. Warnings are the pre-existing repo baseline (not introduced by Plan 3 content). All 48 new floor storylets parse clean, no Phase 10/11/12 regressions.
- **Error-code registry:** regenerated via `generate_error_codes.py` after each substantive C# commit that shifted Surfaced/Caught/Expected lines; `docs/error-codes.md` current at T26.

---

## 6. Known limitations / deferred items

- **T28 — 14-day in-game smoke pending.** Requires a live Bannerlord session enlisted with a lord through multiple strategic-posture shifts (offensive → defensive, successful raid, supply pressure arc). Code-level verification is complete; operator smoke confirms cadence and diversity feel right in practice.

- **Localization XML sync deferred.** 192 new loc-keys (48 storylets × 4 keys each for `title` / `setup` / option `text` / option `tooltip`). All authored as inline `{=key}Fallback`; the game falls back to inline text at runtime (zero runtime impact). `Tools/Validation/sync_event_strings.py` identifies missing keys and can generate an XML payload via `--generate output.xml`; a future translator pass integrates the delta into `ModuleData/Languages/enlisted_strings.xml`.

- **Floor-storylet trigger calibration.** All 48 storylets currently gate on `["is_enlisted"]`; family-level relevance is enforced in `SignalBuilder` (e.g., `TensionIncident` only emits when `ArmyStrain >= Elevated`). Plan 4 Phase B.5 added `snapshot_*_gte:level` predicates to `TriggerRegistry` (shipped 2026-04-22 with Plan 4, `ca22111..0c519d3`). A follow-up authoring pass can now refine storylet-level triggers for finer gating — no longer blocked.

- **Signal thresholds are seed values.** `SignalBuilder`'s threshold choices (e.g., `FrontPressure.High` for Rumor elevation, `ArmyStrainLevel.Elevated` for TensionIncident, `SupplyPressure.Strained` for QuartermasterWarning) reflect the design spec's initial reading. They may need recalibration once T28 smoke data and longer playtests land — simple enum bumps in `EnlistedCampaignSignalBuilder`, no architectural change.

---

## 7. Cross-plan integration notes

- **Plan 1 dependency met.** `EnlistedCampaignIntelligenceBehavior.Current` is the single accessor consumed in `OnHourlyTick`. During T6, plan-prescribed `snapshot.RecentChangeFlags.HasFlag(...)` was corrected to `snapshot.RecentChanges.HasFlag(...)` (typed `RecentChangeFlags`); caught during implementation and mapped in 8 call sites.

- **Plan 2 — independent.** Lord AI Intervention (wrappers + narrow Harmony) does not share code with Plan 3. Plan 2 may ship before or after; no ordering constraint.

- **Plan 4 integration point.** Plan 4 Phase B.5 introduced snapshot-backed `TriggerRegistry` predicates (`snapshot_front_pressure_gte:High`, `snapshot_army_strain_gte:Elevated`, etc.) on 2026-04-22. Plan 3 floor storylets **do not yet use them** — they still gate on the placeholder `is_enlisted` trigger. A follow-up authoring pass can now refine per-storylet relevance at any time.

- **Plan 5 — compatible.** Plan 5 may identify floor storylets as overlay targets for culture/trait variants (e.g., a Battanian-flavored `floor_camp_talk` overlay). Plan 3 doesn't constrain Plan 5 — floor storylets are valid overlay candidates with no special handling required.

---

## 8. API drift corrections discovered during execution

Plan-vs-actual divergences caught and fixed during implementation (same pattern as Spec 2's drift appendix):

| Task | Plan said | Actual | Fix |
| :--- | :--- | :--- | :--- |
| T4 | `EnlistedSaveDefiner.cs:134` for `Dictionary<string, CampaignTime>` container registration | Line `:160` (T1's enum block shifted the file) | Container was already registered; stale line ref, no new reg needed |
| T6 | `snapshot.RecentChangeFlags.HasFlag(...)` | Property is named `RecentChanges` (typed `RecentChangeFlags`) | Substituted in 8 places |
| T6 | `ArmyStrainLevel.High` | Enum is `{ None, Mild, Elevated, Severe, Breaking }` — no `High` | Mapped to `Elevated` (middle "noticeably tense" bucket) |
| T10 | `Content.TriggerRegistry.EvaluateAll(triggers)` | Actual API is `TriggerRegistry.Evaluate(IList<string>, StoryletContext)` | Two-arg call with `null` ctx (floor predicates don't need per-storylet context) |
| T10 | `ModLogger.Expected($"...")` with interpolation in summary | AGENTS.md pitfall #20 — summary must be a string literal | Refactored to literal summary + `Dictionary` ctx payload |
| T12 | `using TaleWorlds.Library;` for `MBRandom` | `MBRandom` lives in `TaleWorlds.Core` (verified against decompile + 2 existing call sites) | Correct namespace used |
| T14 | `PickStoryletFromPool(signal.PoolPrefix)` (1 arg) | T12's picker refactor changed signature to `(prefix, type)` | Updated to two-arg form |
| T14 | Double-throttle race — outer `TryClaim` in `OnHourlyTick` + inner in `EmitExternalSignal` would silently discard pending XP on throttle reject | N/A — design flaw in the plan | Removed outer `TryClaim`; `EmitExternalSignal` returns `bool`; callers gate `Clear()` on `emitted == true` |

---

## 9. Code-quality issues caught and fixed during review

- **T10 — Director-null cooldown leak.** Original `EmitPicked` relied on `Director?.EmitCandidate(...)` to no-op when Director was null, but the following cooldown-stamp line still ran, marking the storylet unavailable for 36 hours despite no delivery. Fixed with an explicit director-null guard + `Expected` log + early return **before** the cooldown stamp.

- **T26 — heartbeat-before-IsEnlisted ordering retained intentionally.** The hourly heartbeat log emits before the `IsEnlisted` early-exit so that in-flight debugging at low enlistment churn still shows the behavior alive. Retained with a one-line behavioral comment to prevent the ordering question from being re-raised by future sessions.

---

## 10. Commit chain reference

25 feature commits in chronological order (range `5fedf88..189e75c`):

```
5fedf88  feat(signals): claim save-definer offset 49 + enum offsets 99-101 for Plan 3
0586cb9  feat(signals): add SignalType / SignalConfidence / SignalChangeType enums
5d2d94b  feat(signals): add EnlistedCampaignSignal transient POCO
1e2ee97  feat(signals): add SignalEmissionRecord persistent cooldown POCO
03073b9  docs(signals): add signal-projection mapping reference
84106ce  feat(signals): implement EnlistedCampaignSignalBuilder pure projector
def7d42  chore(signals): strip unused using System from enum files
79f0e51  chore(signals): defer EnlistedSignalEmitterBehavior csproj entry to T10
b04ae43  feat(signals): extend StoryCandidate with signal-metadata fields
df10a8d  feat(signals): add EnlistedSignalEmitterBehavior skeleton
ea09d58  fix(signals): guard Director null in emit + tighten EmitPicked doc
e2b8e8b  feat(signals): register EnlistedSignalEmitterBehavior in SubModule
ee1db57  feat(signals): weighted-diversity picker for floor-storylet pool
a1deafd  feat(signals): rewire DailyDriftApplicator.FlushSummary through signal pipeline
f92c2fd  content(signals): author floor_rumor.json (5 low-confidence rumor storylets)
a70bd52  content(signals): author floor_camp_talk.json (6 camp-talk storylets)
ece316f  content(signals): author floor_dispatch.json (5 dispatch storylets)
0d223d9  content(signals): author floor_order_shift.json (5 order-shift storylets)
036de84  content(signals): author floor_quartermaster_warning.json (5 QM-warning storylets)
14ffbf2  content(signals): author floor_scout_return.json (5 scout-return storylets)
0b63683  content(signals): author floor_prisoner_report.json (4 prisoner-report storylets)
c2ee94b  content(signals): author floor_aftermath_notice.json (5 aftermath storylets)
35f346d  content(signals): author floor_tension_incident.json (5 tension-incident storylets)
ae7ce92  content(signals): author floor_silence_with_implication.json (3 silence storylets)
189e75c  feat(signals): observability polish — per-family counters + error cats
```

This verification doc's commit is the 26th and closes Plan 3.

Notable architectural commits:
- `84106ce` — pure projector lands (the heart of the plan).
- `ea09d58` — Director-null cooldown leak fix.
- `b04ae43` — `StoryCandidate` metadata extension (offsets 102/103 registered here).
- `ee1db57` — weighted-diversity picker (prevents visible repetition).
- `a1deafd` — `DailyDriftApplicator` rewire through signal pipeline.
- `189e75c` — observability polish (per-family counters, categorized errors).

---

**Next up:** Plan 5 (Career Loop Closure) now unblocked by Plan 4 shipping 2026-04-22. Plan 4's shipped snapshot predicates mean Plan 3's floor-storylet gating can now be tightened from the `is_enlisted` placeholder to per-storylet snapshot gates in a future authoring pass.
