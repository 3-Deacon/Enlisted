# Plans Integration — Design Spec

**Date:** 2026-04-21
**Status:** Draft — pending user review
**Scope:** Reconcile the remaining work from the Orders Surface plan (2026-04-20) with the Campaign Intelligence Backbone plan (2026-04-21) + three downstream plans into a single coherent roadmap. Partition the work across five plan files, define scope boundaries, resolve four known conflicts, and archive the superseded Orders plan.

This is a plan-of-plans. It does not design features. It defines which plan owns which task and in what order the plans execute.

---

## 1. Two source ideas being integrated

**Orders Surface** (`docs/superpowers/plans/2026-04-20-orders-surface.md`) — the player-facing layer: duty profiles, named-order arcs, arc splicing, daily skill drift, path scoring, content validators. Phase A and most of Phase B have shipped. Remaining work is mostly content authoring (Tasks 27-35), validator + cleanup (36, 41-43), path crossroads (Phase C), and polish (Phase D).

**Campaign Intelligence Backbone** (`docs/superpowers/specs/2026-04-21-enlisted-campaign-intelligence-design.md` + `docs/superpowers/plans/2026-04-21-campaign-intelligence-backbone.md`) — a compact hidden-truth assessment of the enlisted lord's strategic situation, recomputed hourly, consumed downstream to drive smarter lord AI, imperfect player-visible signals, and believable soldier-work generation.

The two were drafted one day apart and overlap implicitly. This spec makes the overlap explicit and partitions cleanly.

---

## 2. Merged vision

From the Intelligence spec §5:

> `live campaign state → enlisted-lord intelligence assessment → smarter enlisted-lord weighting/behavior → imperfect soldier-visible signals and duties → player outcomes and story routing → updated live campaign state`

The Orders surface is the **right-hand side** of this loop. Duty profiles, named orders, and content pools are how the player experiences the imperfect signals and duties the Intelligence backbone projects. Ambient profile storylets (Orders Tasks 27-32) and transition storylets (Task 35) are **signal and duty content** whose consumer lives in the Intelligence downstream plans, not in the Orders backbone itself.

The merge partitions the remaining work by **lifecycle position in this loop**, not by original plan membership.

---

## 3. Five-plan roadmap

| # | Plan | Status | Scope | Size |
|---|---|---|---|---|
| 1 | Campaign Intelligence Backbone | Exists — needs light revision | Snapshot + collector + classifier + hourly tick + persistence. No consumers. | 29 tasks |
| 2 | Lord AI Intervention | New | Model wrappers (`TargetScoreCalculatingModel`, `ArmyManagementCalculationModel`, `MobilePartyAIModel`) + narrow Harmony for behavior loops. Snapshot-driven bias. Enlisted-gated. | ~25 tasks est. |
| 3 | Signal Projection | New | `SignalBuilder` projects snapshot into imperfect soldier-visible signals (rumor, camp_talk, scout_return, quartermaster_warning, etc.). Absorbs Orders Task 53 (floor storylets → signal authoring) and rewires `DailyDriftApplicator`'s emission path. | ~25 tasks est. |
| 4 | Duty Opportunities | New | `DutyOpportunityBuilder` generates ambient duty storylets from snapshot. Fires at `StoryTier.Log` through `StoryDirector` + `OrdersNewsFeedThrottle`. Absorbs Orders Tasks 27-32, 35, 36, 41, 43, 54. | ~30 tasks est. |
| 5 | Career Loop Closure | New | Path crossroads (Phase C 44-52 from Orders) + lord-trait/culture overlays (Phase D 55-56) + debug/save-load/playtest (Phase D 57-60). Final integration smoke. | ~25 tasks est. |

---

## 4. Task ownership — Orders residual mapped to new plans

After Tasks 33 and 34 shipped today (commits `f8e028b`, `deb88db`), the Orders plan has the following pending tasks. Each is migrated to its new owner below.

| Orders task | Description | New owner | Reason |
|---|---|---|---|
| 27-32 | Author 6 duty-profile pools (~110 storylets) | **Plan 4** | Consumer (`DutyOpportunityBuilder`) lives in Plan 4; content gated on Intelligence snapshot state. |
| 35 | Author transition.json (~15 storylets) | **Plan 4** | Emission hook (profile-change or snapshot `RecentChangeFlags`) lives in Plan 4. |
| 36 | Validator Phase 13 — pool coverage | **Plan 4** | Runs after pool authoring; natural closure for Plan 4's content. |
| 41 | Sync localization strings | **Per-commit verification step** | Not a plan-level task. Each authoring plan runs `sync_event_strings.py` as part of its content commits. Dropped from the task inventory. |
| 42 | Delete old Orders subsystem (`src/Features/Orders/`) | **Pre-plan hygiene** | Single-commit cleanup. Does not require a plan. Ships before Plan 1 T1 or alongside. |
| 43 | Phase B smoke — daily drift + named-order arc + transitions | **Plan 4** | Verifies the loop Plan 4 delivers. |
| 44 | PathScorer.OnIntentPicked (already shipped in Task 16) | — | Already done. |
| 45-52 | Phase C path system — committed_path, crossroads, prior_service flags, Phase 15 validator, Phase C smoke | **Plan 5** | Independent of Intelligence; career-commitment subsystem closes the player arc. |
| 53 | Floor storylets (~50) | **Plan 3** | Floor storylets are signal-tier news-feed texture — exactly what `SignalBuilder` produces. |
| 54 | Imprisoned-profile + lord-state edge-case storylets | **Plan 4** | Imprisoned is a duty profile; edge cases are duty-opportunity content. |
| 55 | Culture-variant overlays on ~15 hot-path storylets | **Plan 5** | Overlay pass on already-authored content; polish, not primary authoring. |
| 56 | Lord-trait gates on ~15 storylets | **Plan 5** | Same — overlay pass on existing content. |
| 57 | Debug hotkeys | **Plan 5** | Final closure polish. |
| 58 | Save-load arc reconstruction test | **Plan 5** | Plan 1 Phase F already tests snapshot persistence; this tests the full loop's save-load. |
| 59 | Final playtest scenarios A-G | **Plan 5** | Integration smoke for the full loop. |
| 60 | Verification doc + final status update | **Plan 5** | Final doc. |

**Net effect:** Orders plan's remaining pending tasks all migrate. The Orders plan goes from "Phase A done, Phase B partial" to "fully superseded" — archive it.

---

## 5. Conflict resolutions

### 5.1 Save-definer offset 48
**Conflict:** AGENTS.md Critical Rule #11 reserves class offsets 45-60 for "concrete `Activity` subclasses." Plan 1 claims offset 48 for `EnlistedLordIntelligenceSnapshot`, which is a POCO, not an Activity subclass.
**Resolution:** Broaden AGENTS.md Rule #11 to read *"Class offsets 45-60 reserved for concrete `Activity` subclasses AND closely-related surface-spec persistent state (snapshots, accessors) that surface specs own."* Plan 1 keeps offset 48. The rule broadening ships as part of Plan 1's T1 revision (see §8 below).

### 5.2 DailyDriftApplicator emission vs. Plan 3 signals
**Conflict:** Today `DailyDriftApplicator.FlushSummary` emits a `StoryTier.Log` "Past few days: Bow +45, Athletics +25 XP" entry through `StoryDirector`. Plan 3's `SignalBuilder` produces richer signals (`rumor`, `camp_talk`, `scout_return`, etc.) that would overlap.
**Resolution:** Plan 3 absorbs the emission path. `DailyDriftApplicator` keeps the XP accumulator (per-skill pending-XP dictionary + daily apply) but its news-feed emit site is rewired to emit through `SignalBuilder` as a `scout_return`-or-similar signal. XP bookkeeping = DailyDriftApplicator's job. News-feed voice = Plan 3's job.

### 5.3 Transition-interrupt hook location — **flagged for user checkpoint**
**Conflict:** Orders Task 35 transition storylets fire when a profile change commits while a named order is active. Two viable sites:
- **(a)** `DutyProfileBehavior.CommitTransition` emits the transition storylet directly. Profile-change is the trigger; the hook lives with the state-change that generates it. Plan 4 authors the content; Plan 4 does not own the emission call site. Simplest wiring.
- **(b)** Plan 4's `DutyOpportunityBuilder` subscribes to `DutyProfileBehavior`'s existing `profile_changed` beat on `OrderActivity` and emits the transition storylet there. Centralizes all content emission in Plan 4's builder. Requires Plan 4 to subscribe to a beat emitted by an Orders-era behavior.

Note: Plan 1's `RecentChangeFlags` cover `WarDeclared / MakePeace / ArmyCreated / ArmyDispersed / MapEventStarted` — NOT `DutyProfileId` changes. So option (b) routes through the existing beat, not through the snapshot.

**Status:** Unresolved. User must decide before Plan 4 is dispatched to a subagent. See §8.

### 5.4 Boundary between Orders content authoring and Plan 4 content
**Conflict:** Orders Phase C Task 49 (T7+ named-order variants gated on `committed_path`) and Phase D Tasks 54-56 (imprisoned, culture, trait overlays) are all content authoring. Which plan owns them?
**Resolution:** Content authored against a **snapshot-driven consumer** → Plan 3 or 4. Content authored as **overlays on existing named-order/path pools** → Plan 5. Rationale: Plan 5 is the polish/closure lane and already owns path crossroads; overlay authoring fits there cleanly. Named-order T7+ variants (Task 49) are Plan 5 — they're variants on existing pools, not new snapshot-driven content.

---

## 6. Execution order + cross-plan dependencies

```
[pre-plan hygiene: delete src/Features/Orders/ legacy — Task 42, single commit]
          ↓
   Plan 1 (Intelligence Backbone) — snapshot exists, no consumers
          ↓
   ┌───┬───┬───┬───┐
   ↓   ↓   ↓   ↓
  P2  P3  P4  P5-early (path crossroads, Tasks 44-52)
              ↓
             P5-late (culture/trait overlays, save-load, final smoke, verification doc)
```

**Hard dependencies:**
- Plans 2, 3, 4, 5 all read `EnlistedCampaignIntelligenceBehavior.Current` → Plan 1 must ship first.
- Plan 5's culture/trait overlays (Tasks 55-56) operate on content authored in Plan 4 → Plan 5's late phase queues until Plan 4 content is landed.

**Parallelism:**
- Plans 2, 3, 4 are independent and can ship in any order after Plan 1. They share the `StoryDirector` + `OrdersNewsFeedThrottle` infrastructure (already live) but do not depend on each other's code.
- Plan 5's path-crossroads portion (Tasks 44-52) is Intelligence-independent and parallelizes with 2/3/4.
- Within each plan, Phase structure is preserved (see Plan 1's Phase 0-H as the reference shape).

**Loc-sync note:** `Tools/Validation/sync_event_strings.py` is a per-commit verification step, not a plan-level task. Each content-authoring plan runs it as part of its authoring commits; there is no dedicated "Task 41" in the new structure.

---

## 7. Archive strategy

Move `docs/superpowers/plans/2026-04-20-orders-surface.md` to `docs/superpowers/plans/archive/2026-04-20-orders-surface.md` in the integration commit. Preserves:
- Phase A task ledger with ship commits (institutional knowledge — what shipped when)
- API corrections appendix (Spec 2 plan-vs-decompile divergences — reference when new subagents hit similar)
- Ownership decisions for the 60 tasks (provenance for the migration table in §4)

Create `docs/superpowers/plans/archive/README.md` with a one-line explanation: *"Superseded plan files preserved for historical reference. Active plans live one level up."*

The Intelligence backbone plan (`2026-04-21-campaign-intelligence-backbone.md`) **stays in place** as Plan 1. It receives a light revision pass per Task #29 — offset-48 justification, AGENTS.md rule broadening, cross-plan references — but is not reshaped.

Update CLAUDE.md's "Current project status" paragraph to reflect the new five-plan structure. Update `docs/INDEX.md` if the old Orders plan is linked.

---

## 8. Open checkpoint before subagent dispatch

**Before spawning the four plan-drafting subagents (Plans 2, 3, 4, 5), the user must confirm:**

- Resolution **§5.3** — transition-interrupt hook in `DutyProfileBehavior` (option a) or in Plan 4's `DutyOpportunityBuilder` (option b, currently proposed).

The other three resolutions (offset 48, daily drift vs signals, content/overlay boundary) were approved implicitly when the user said "merge" after the earlier presentation. #5.3 involves a specific code surface that wasn't surfaced in that round.

---

## 9. Success criteria

This spec is successful when:
- User approves the five-plan roadmap + the four resolutions
- Four new plan files exist (Plans 2-5) drafted to the same quality bar as Plan 1
- Plan 1 revision pass ships (offset justification + cross-plan refs)
- Old Orders plan moved to `archive/`
- CLAUDE.md status paragraph reflects the new structure
- Execution begins at Plan 1 T1 with the full roadmap visible to every subagent and future session

---

## 10. Non-goals

- This spec does not design Plans 2, 3, 4, or 5. Each plan gets its own drafting pass via subagent after this spec is approved.
- This spec does not commit to calibration of Intelligence thresholds (supply pressure / strain / front pressure numeric thresholds). Those are per-plan-4 decisions.
- This spec does not re-verify any of Plan 1's 29 tasks. Plan 1 has already been drafted to the same format.
- This spec does not specify the Plan 2 AI wrapper surface. That's Plan 2's drafting pass.
