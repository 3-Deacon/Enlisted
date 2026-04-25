# Archived Plans

Superseded plan files preserved for historical reference. Active plans live one level up at `docs/superpowers/plans/`.

Archived plans are kept because they contain load-bearing institutional knowledge — API corrections appendices, Phase ship history with commit references, ownership decisions for individual tasks — that future sessions may need when encountering similar surfaces or when auditing past work.

## Contents

### Foundation plans (shipped)

| File | Archived | Status |
|---|---|---|
| [`2026-04-18-event-pacing.md`](2026-04-18-event-pacing.md) | 2026-04-25 | Shipped. StoryDirector pacing rails live. Companion verification doc: [`2026-04-18-event-pacing-verification.md`](2026-04-18-event-pacing-verification.md). |
| [`2026-04-18-event-pacing-verification.md`](2026-04-18-event-pacing-verification.md) | 2026-04-25 | Verification report for event-pacing plan. |
| [`2026-04-18-enlisted-architecture-fixes.md`](2026-04-18-enlisted-architecture-fixes.md) | 2026-04-25 | Shipped. Architecture fixes integrated. |
| [`2026-04-18-error-code-redesign.md`](2026-04-18-error-code-redesign.md) | 2026-04-25 | Shipped. ModLogger.Surfaced/Caught/Expected + auto-generated registry. |
| [`2026-04-19-error-warn-cleanup.md`](2026-04-19-error-warn-cleanup.md) | 2026-04-25 | Shipped 2026-04-19. ModLogger.Error retired; Phase 11 validator enforces. |
| [`2026-04-19-event-meaning-prerequisites.md`](2026-04-19-event-meaning-prerequisites.md) | 2026-04-25 | Subsumed by Spec 0 backbone implementation. |

### Spec 0 / 1 / 2 (shipped)

| File | Archived | Status |
|---|---|---|
| [`2026-04-19-storylet-backbone.md`](2026-04-19-storylet-backbone.md) | 2026-04-25 | Spec 0 shipped (commit `45b38bf`). Living reference: [`docs/Features/Content/storylet-backbone.md`](../../../Features/Content/storylet-backbone.md). |
| [`2026-04-19-enlisted-home-surface.md`](2026-04-19-enlisted-home-surface.md) | 2026-04-25 | Spec 1 shipped (`fc93285` → `0390fdf`). Living reference: [`docs/Features/Content/home-surface.md`](../../../Features/Content/home-surface.md). |
| [`2026-04-20-orders-surface.md`](2026-04-20-orders-surface.md) | 2026-04-21 | Five-plan integration roadmap at [`../../specs/archive/2026-04-21-plans-integration-design.md`](../../specs/archive/2026-04-21-plans-integration-design.md). Phase A (Tasks 1-17) + parts of Phase B shipped; remaining tasks migrated to Plans 1-5 per the integration spec §4 ownership table. Legacy `src/Features/Orders/` + `ModuleData/Enlisted/Orders/` deleted in commit `a8719bb` (Task 42 closed). |

### Five-plan integration (shipped)

| File | Archived | Status |
|---|---|---|
| [`2026-04-21-campaign-intelligence-backbone.md`](2026-04-21-campaign-intelligence-backbone.md) | 2026-04-25 | Plan 1 shipped on `development` 2026-04-22 (`34322d4` → `bc13096`, 23 commits). 28 of 29 tasks ✅; T28 in-game smoke historically pending (rolled into the integrated smoke run on 2026-04-22). Living reference for the snapshot/classifier/collector/hourly-tick layer. |
| [`2026-04-21-lord-ai-intervention.md`](2026-04-21-lord-ai-intervention.md) | 2026-04-24 | Shipped Plan 2 implementation on `development` (`4929246` → `f289b9b`). Phase H smoke passed 2026-04-22; no narrow Harmony patch required. Current proof lives in [`docs/Features/CampaignIntelligence/plan2-phase-h-log.md`](../../../Features/CampaignIntelligence/plan2-phase-h-log.md). |
| [`2026-04-21-signal-projection.md`](2026-04-21-signal-projection.md) | 2026-04-25 | Plan 3 shipped on `development` 2026-04-22 (`5fedf88` → `189e75c`, 26 commits). Companion verification doc: [`2026-04-21-signal-projection-verification.md`](2026-04-21-signal-projection-verification.md). |
| [`2026-04-21-signal-projection-verification.md`](2026-04-21-signal-projection-verification.md) | 2026-04-25 | Verification report for Plan 3. T28 in-game smoke historically pending (rolled into integrated smoke run 2026-04-22). |
| [`2026-04-21-duty-opportunities.md`](2026-04-21-duty-opportunities.md) | 2026-04-25 | Plan 4 shipped on `development` 2026-04-22 (`ca22111` → `0c519d3`, 22 commits). 140 storylets across 7 ambient pools + transition.json. Companion verification doc: [`2026-04-21-duty-opportunities-verification.md`](2026-04-21-duty-opportunities-verification.md). |
| [`2026-04-21-duty-opportunities-verification.md`](2026-04-21-duty-opportunities-verification.md) | 2026-04-25 | Verification report for Plan 4. T26 + T27 in-game smoke historically pending (rolled into integrated smoke run 2026-04-22). |

### Other shipped

| File | Archived | Status |
|---|---|---|
| [`2026-04-21-top-right-combat-log.md`](2026-04-21-top-right-combat-log.md) | 2026-04-24 | Shipped top-right combat-log implementation and current UI reference at [`docs/Features/UI/enlisted-combat-log.md`](../../../Features/UI/enlisted-combat-log.md). |

### Superseded by menu+duty unification

| File | Archived | Superseded by |
|---|---|---|
| [`2026-04-23-agency-news-status-integration.md`](2026-04-23-agency-news-status-integration.md) | 2026-04-25 | [`../../specs/2026-04-24-enlisted-menu-duty-unification-design.md`](../../specs/2026-04-24-enlisted-menu-duty-unification-design.md) Plan A. Per audit in spec v6 §6.9, the typed-dispatch enums (`DispatchSurfaceHint` / `DispatchSourceKind`) and `BuildKingdomDigestSection` infrastructure this plan would have built **never shipped** — absorbed into menu+duty Plan A's news-v2 substrate work. The 30-site rename pass referenced in this plan is now a no-op since the source enums don't exist. |

## Reading archived plans

- Ship history + commit references are still accurate — the git log reflects the commits called out.
- Pending tasks listed in archived plans are **not** pending in the live roadmap. Cross-reference the integration spec's task-ownership table or the menu+duty spec's plan structure to find where each task lives now.
- API corrections appendices are the most valuable part — they document plan-vs-decompile divergences discovered during implementation, useful as a reference pattern for new work.
- For shipped plans: the code is the truth. Plans document the path taken at the time, not necessarily current behavior.

## Active plans (one level up)

After the 2026-04-25 archive cleanup, the active plans directory holds:

- **Career Loop Closure (Plan 5 of the five-plan family)** — code shipped 2026-04-22, in-game smoke (T10, T17, T19-T21) pending human operator. Three docs: [`../2026-04-21-career-loop-closure.md`](../2026-04-21-career-loop-closure.md), [`../2026-04-21-career-loop-verification.md`](../2026-04-21-career-loop-verification.md), [`../2026-04-21-career-loop-playtest-scenarios.md`](../2026-04-21-career-loop-playtest-scenarios.md). Will archive together when smoke closes.
- **CK3 Wanderer Mechanics 7-plan family** — drafted 2026-04-24 against [`../../specs/2026-04-24-ck3-wanderer-systems-analysis.md`](../../specs/2026-04-24-ck3-wanderer-systems-analysis.md). Plans 1-7 in `../2026-04-24-ck3-wanderer-*.md`.
