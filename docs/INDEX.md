# Documentation Index — Enlisted

Master catalog of all AI-context files, design docs, and references for the Enlisted Bannerlord mod.

**For agentic workers:** rules live in the AGENTS.md cascade. Open this index to find a specific topic; once located, work from the nested AGENTS.md or living reference, not from this catalog.

---

## AGENTS.md cascade

The rule cascade — both Codex (path-walk) and Claude Code (sibling CLAUDE.md auto-discovery) load these.

| Scope | File |
|---|---|
| Project-wide | [../AGENTS.md](../AGENTS.md) |
| Content / storylets / events / StoryDirector | [../src/Features/Content/AGENTS.md](../src/Features/Content/AGENTS.md) |
| Save system / offset table / serialization | [../src/Mod.Core/SaveSystem/AGENTS.md](../src/Mod.Core/SaveSystem/AGENTS.md) |
| ModuleData / JSON authoring | [../ModuleData/Enlisted/AGENTS.md](../ModuleData/Enlisted/AGENTS.md) |
| Validator / error-codes / lint | [../Tools/Validation/AGENTS.md](../Tools/Validation/AGENTS.md) |
| Conversations / dialog tokens | [../src/Features/Conversations/AGENTS.md](../src/Features/Conversations/AGENTS.md) |
| Activities / Orders / Home | [../src/Features/Activities/AGENTS.md](../src/Features/Activities/AGENTS.md) |
| Specs / plans / verification (`docs/superpowers/AGENTS.md`) | [superpowers/AGENTS.md](superpowers/AGENTS.md) |
| Build / deploy / Workshop | [../Tools/AGENTS.md](../Tools/AGENTS.md) |

---

## Project-wide top-level docs

| Topic | File |
|---|---|
| Project overview (player-facing) | [PROJECT-OVERVIEW.md](PROJECT-OVERVIEW.md) |
| Current status | [superpowers/STATUS.md](superpowers/STATUS.md) |
| Project resources & links | [PROJECT-RESOURCES.md](PROJECT-RESOURCES.md) |
| Architecture briefs (cross-cutting) | [architecture/](architecture/) |
| Error code registry | [error-codes.md](error-codes.md) |
| Error code archive (historical) | [error-codes-archive.md](error-codes-archive.md) |
| Tools reference | [../Tools/README.md](../Tools/README.md) + [../Tools/TECHNICAL-REFERENCE.md](../Tools/TECHNICAL-REFERENCE.md) |

---

## docs/Features/ — per-subsystem handbooks

Mirror of `src/Features/`. Each `docs/Features/<X>/` corresponds to a `src/Features/<X>/` (with optional nested AGENTS.md when rule density justifies it).

| Subsystem | Docs folder |
|---|---|
| Activities | [Features/Activities/](Features/Activities/) |
| Camp | [Features/Camp/](Features/Camp/) |
| CampaignIntelligence | [Features/CampaignIntelligence/](Features/CampaignIntelligence/) |
| Ceremonies | [Features/Ceremonies/](Features/Ceremonies/) |
| Combat | [Features/Combat/](Features/Combat/) |
| Companions | [Features/Companions/](Features/Companions/) |
| Company | [Features/Company/](Features/Company/) |
| Content | [Features/Content/](Features/Content/) |
| Conversations | [Features/Conversations/](Features/Conversations/) |
| Enlistment | [Features/Enlistment/](Features/Enlistment/) |
| Equipment | [Features/Equipment/](Features/Equipment/) |
| Identity | [Features/Identity/](Features/Identity/) |
| Interface | [Features/Interface/](Features/Interface/) |
| Patrons | [Features/Patrons/](Features/Patrons/) |
| Ranks | [Features/Ranks/](Features/Ranks/) |
| Retinue | [Features/Retinue/](Features/Retinue/) |

`src/Features/` subdirectories without a `docs/Features/<X>/` folder (no design docs authored yet): Conditions, Context, Contracts, Endeavors, Escalation, Flags, Lifestyles, Logistics, PersonalKit, Qualities.

---

## docs/Reference/ — research and analysis

Decompile-based research, native API analyses, system deep-dives. Living/historical artifacts — may go stale across game updates.

See [Reference/](Reference/) for the full list.

---

## docs/superpowers/ — specs and plans

| | |
|---|---|
| Conventions | [superpowers/AGENTS.md](superpowers/AGENTS.md) |
| Current status | [superpowers/STATUS.md](superpowers/STATUS.md) |
| Active specs | [superpowers/specs/](superpowers/specs/) |
| Active plans | [superpowers/plans/](superpowers/plans/) |
| Archived specs | [superpowers/specs/archive/](superpowers/specs/archive/) |
| Archived plans | [superpowers/plans/archive/](superpowers/plans/archive/) |

---

## docs/Archive/ — superseded docs

Pre-AGENTS.md-cascade documentation. Retained as historical context. Authoritative content has moved into the cascade.

| File | Archived | Why |
|---|---|---|
| [Archive/BLUEPRINT-2026-04-archived.md](Archive/BLUEPRINT-2026-04-archived.md) | 2026-05-24 | Superseded by AGENTS.md cascade |
| [Archive/DEVELOPER-GUIDE-2026-04-archived.md](Archive/DEVELOPER-GUIDE-2026-04-archived.md) | 2026-05-24 | Superseded by AGENTS.md cascade + Tools/AGENTS.md |
| [Archive/BUILD-CONFIGURATIONS-2026-04-archived.md](Archive/BUILD-CONFIGURATIONS-2026-04-archived.md) | 2026-05-24 | Folded into [Tools/AGENTS.md](../Tools/AGENTS.md) |
| [Archive/Core-index-2026-05-archived.md](Archive/Core-index-2026-05-archived.md) | 2026-05-24 | `docs/Features/Core/` was split into per-subsystem folders during the docs+AGENTS.md restructure |

---

## Other resources

- **Memory (per-user, Claude Code only):** `~/.claude/projects/<repo>/memory/MEMORY.md` and topic files. Personal; outside the repo.
- **Per-user repo overrides:** `CLAUDE.local.md` (Claude Code) or `AGENTS.override.md` (Codex). Gitignored.
- **Steam Workshop page:** see [PROJECT-RESOURCES.md](PROJECT-RESOURCES.md).
