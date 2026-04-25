# Archived Specs

Superseded design documents preserved for historical reference. Active specs live one level up at `docs/superpowers/specs/`.

Archived specs are kept because they contain load-bearing institutional knowledge — design rationale, audit findings, API divergence notes, decisions made and why — that future sessions may need when encountering similar surfaces or auditing past work.

## Contents

### Foundation specs (shipped)

| File | Archived | Status |
|---|---|---|
| [`2026-04-18-event-pacing-design.md`](2026-04-18-event-pacing-design.md) | 2026-04-25 | Shipped. StoryDirector + EventDeliveryManager + DensitySettings live. Living reference: AGENTS.md Critical Rule #10 + spec v6 §6.8. |
| [`2026-04-18-enlisted-architecture-fixes-design.md`](2026-04-18-enlisted-architecture-fixes-design.md) | 2026-04-25 | Shipped. Architecture fixes integrated into the codebase. |
| [`2026-04-18-error-code-redesign-design.md`](2026-04-18-error-code-redesign-design.md) | 2026-04-25 | Shipped. `ModLogger.Surfaced/Caught/Expected` + auto-generated `docs/error-codes.md` registry. |
| [`2026-04-19-error-warn-cleanup-design.md`](2026-04-19-error-warn-cleanup-design.md) | 2026-04-25 | Shipped 2026-04-19. `ModLogger.Error` retired; `validate_content.py` Phase 11 enforces. |

### Spec 0 / 1 / 2 (shipped)

| File | Archived | Status |
|---|---|---|
| [`2026-04-19-event-meaning-design.md`](2026-04-19-event-meaning-design.md) | 2026-04-25 | Subsumed by Spec 0 storylet backbone. Concepts (intent-biased pools, Activity phases, scripted effects) preserved in shipped backbone. |
| [`2026-04-19-storylet-backbone-design.md`](2026-04-19-storylet-backbone-design.md) | 2026-04-25 | Spec 0 shipped (commit `45b38bf`). Living reference: [`docs/Features/Content/storylet-backbone.md`](../../../Features/Content/storylet-backbone.md). |
| [`2026-04-19-enlisted-home-surface-design.md`](2026-04-19-enlisted-home-surface-design.md) | 2026-04-25 | Spec 1 shipped on `development` (`fc93285` → `0390fdf`). Living reference: [`docs/Features/Content/home-surface.md`](../../../Features/Content/home-surface.md). |
| [`2026-04-20-orders-surface-design.md`](2026-04-20-orders-surface-design.md) | 2026-04-25 | Spec 2 Phase A + Phase B shipped (`f8e028b` → `deb88db`). Legacy Orders deleted 2026-04-21 (`a8719bb`). Plan archived separately at [`../../plans/archive/2026-04-20-orders-surface.md`](../../plans/archive/2026-04-20-orders-surface.md). |

### Five-plan integration design layer (shipped)

| File | Archived | Status |
|---|---|---|
| [`2026-04-21-enlisted-campaign-intelligence-design.md`](2026-04-21-enlisted-campaign-intelligence-design.md) | 2026-04-25 | Truth-layer design source for Plans 1-5. Plans 1-4 shipped 2026-04-22. Snapshot + classifier + collector + persistence all live. |
| [`2026-04-21-plans-integration-design.md`](2026-04-21-plans-integration-design.md) | 2026-04-25 | Five-plan integration roadmap. Plans 1-4 shipped 2026-04-22; Plan 5 (career loop closure) Half A + B shipped, smoke pending. |

### Other shipped

| File | Archived | Status |
|---|---|---|
| [`2026-04-21-top-right-combat-log-design.md`](2026-04-21-top-right-combat-log-design.md) | 2026-04-25 | Top-right combat-log shipped. Plan archived at [`../../plans/archive/2026-04-21-top-right-combat-log.md`](../../plans/archive/2026-04-21-top-right-combat-log.md). Living reference: [`docs/Features/UI/enlisted-combat-log.md`](../../../Features/UI/enlisted-combat-log.md). |

### Superseded by menu+duty unification design

| File | Archived | Superseded by |
|---|---|---|
| [`2026-04-23-news-and-status-unification-design.md`](2026-04-23-news-and-status-unification-design.md) | 2026-04-25 | [`../2026-04-24-enlisted-menu-duty-unification-design.md`](../2026-04-24-enlisted-menu-duty-unification-design.md) Plan A. Audit found news-v2 substrate (`BuildKingdomDigestSection`, typed-dispatch enums `DispatchSurfaceHint` / `DispatchSourceKind`, `_dailyBriefCompany` removal) was NEVER shipped despite this v2 spec; absorbed into menu+duty Plan A. Section-rename amendments (`UPCOMING` → `PROSPECTS`, `YOU` → `STATUS`, `CAMP ACTIVITIES` + `SINCE LAST MUSTER` → `COMPANY NEWS`) live in menu+duty spec. |
| [`2026-04-23-player-agency-redesign.md`](2026-04-23-player-agency-redesign.md) | 2026-04-25 | [`../2026-04-24-enlisted-menu-duty-unification-design.md`](../2026-04-24-enlisted-menu-duty-unification-design.md) Plan B (envelope substrate) and [`../2026-04-24-ck3-wanderer-systems-analysis.md`](../2026-04-24-ck3-wanderer-systems-analysis.md) §3.6 (lifestyle unlocks). Service Stance model entirely retired before implementation; envelope substrate (StateMutator / AgencyGate / MutationCategory / MagnitudeBand) survives as Plan B per menu+duty spec. Lifestyle "perks" path explicitly excluded due to PerkObject API gap; unlocks-version (option 3) is the v6 commitment. |

## Reading archived specs

- Design rationale + audit findings are still accurate at the time of writing — useful when the same surfaces come up again.
- Cross-references to other specs may be stale if those specs have also moved here.
- Living references (the `docs/Features/<area>/*.md` files) are the up-to-date day-to-day docs; specs archive is for historical context only.
- For shipped designs: the code is the truth. Specs document intent at design time, not necessarily current behavior.
