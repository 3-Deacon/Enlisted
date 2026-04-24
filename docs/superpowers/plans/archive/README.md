# Archived Plans

Superseded plan files preserved for historical reference. Active plans live one level up at `docs/superpowers/plans/`.

Archived plans are kept because they contain load-bearing institutional knowledge — API corrections appendices, Phase ship history with commit references, ownership decisions for individual tasks — that future sessions may need when encountering similar surfaces or when auditing past work.

## Contents

| File | Archived | Superseded by |
|---|---|---|
| [`2026-04-20-orders-surface.md`](2026-04-20-orders-surface.md) | 2026-04-21 | Five-plan integration roadmap at [`docs/superpowers/specs/2026-04-21-plans-integration-design.md`](../../specs/2026-04-21-plans-integration-design.md). Phase A (Tasks 1-17) + parts of Phase B shipped; remaining tasks migrated to Plans 1-5 per the integration spec §4 ownership table. Legacy `src/Features/Orders/` + `ModuleData/Enlisted/Orders/` deleted in commit `a8719bb` (Task 42 closed). |
| [`2026-04-21-lord-ai-intervention.md`](2026-04-21-lord-ai-intervention.md) | 2026-04-24 | Shipped Plan 2 implementation on `development` (`4929246` → `f289b9b`). Phase H smoke passed 2026-04-22; no narrow Harmony patch required. Current proof lives in [`docs/Features/CampaignIntelligence/plan2-phase-h-log.md`](../../../Features/CampaignIntelligence/plan2-phase-h-log.md). |
| [`2026-04-21-top-right-combat-log.md`](2026-04-21-top-right-combat-log.md) | 2026-04-24 | Shipped top-right combat-log implementation and current UI reference at [`docs/Features/UI/enlisted-combat-log.md`](../../../Features/UI/enlisted-combat-log.md). |

## Reading archived plans

- Ship history + commit references are still accurate — the git log reflects the commits called out.
- Pending tasks listed in archived plans are **not** pending in the live roadmap. Cross-reference the integration spec's task-ownership table to find where each task lives now.
- API corrections appendices are the most valuable part — they document plan-vs-decompile divergences discovered during implementation, useful as a reference pattern for new work.
