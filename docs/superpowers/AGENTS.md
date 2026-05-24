# AGENTS.md — docs/superpowers

> Parent: [../../AGENTS.md](../../AGENTS.md)
> Handbook: this is the handbook (it's the specs/plans folder)

This file owns conventions for the specs and plans system: the plan-vs-codebase drift pitfall, the "API corrections appendix" pattern, the verification doc convention, and the doc-comment rule (no forward-spec, no change history).

---

## Plan-vs-codebase API drift

Plans drift before full execution. Grep prescribed file paths and symbol names against the codebase before implementing any multi-file task from an older plan. Long plans append an "API corrections appendix" at the bottom as deviations are discovered.

---

## Verification doc convention

Plans that ship get a paired `-verification.md` at the same path, recording what shipped vs prescribed, API corrections, and smoke-test status. Archived alongside the plan when the feature stabilizes.

---

## Doc-comment rule

Rewrite plan-prescribed doc comments as one behavioral sentence. Don't copy literal plan text that references future specs or explains why a file exists — it rots into stale fiction.

---

## Archival convention

When a plan's feature ships and stabilizes, `git mv` the plan + verification doc into `plans/archive/`. Active plans live at top-level `plans/`; archived at `plans/archive/`. Same pattern applies to docs (e.g. `docs/Archive/`).

---

## See also

- [plans/archive/](plans/archive/) — shipped plans
- [../../AGENTS.md](../../AGENTS.md) — project root
