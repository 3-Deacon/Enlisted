# Archived Plans

Superseded plan files preserved for historical reference. Active plans live one level up at `docs/superpowers/plans/`.

Archived plans are kept because they contain load-bearing institutional knowledge — API corrections appendices, Phase ship history with commit references, ownership decisions for individual tasks — that future sessions may need when encountering similar surfaces or when auditing past work.

## Contents

| File | Archived | Superseded by |
|---|---|---|
| [`2026-04-20-orders-surface.md`](2026-04-20-orders-surface.md) | 2026-04-21 | Five-plan integration roadmap at [`docs/superpowers/specs/2026-04-21-plans-integration-design.md`](../../specs/2026-04-21-plans-integration-design.md). Phase A (Tasks 1-17) + parts of Phase B shipped; remaining tasks migrated to Plans 1-5 per the integration spec §4 ownership table. Legacy `src/Features/Orders/` + `ModuleData/Enlisted/Orders/` deleted in commit `a8719bb` (Task 42 closed). |

## Reading archived plans

- Ship history + commit references are still accurate — the git log reflects the commits called out.
- Pending tasks listed in archived plans are **not** pending in the live roadmap. Cross-reference the integration spec's task-ownership table to find where each task lives now.
- API corrections appendices are the most valuable part — they document plan-vs-decompile divergences discovered during implementation, useful as a reference pattern for new work.
