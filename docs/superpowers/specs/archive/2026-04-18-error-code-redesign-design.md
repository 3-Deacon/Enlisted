# Error Code Redesign — Design Spec

**Date:** 2026-04-18
**Status:** Design approved, ready for implementation planning
**Owner:** Kyle (@Arkpointt)

---

## Problem

The current `ModLogger.ErrorCode(...)` system has three compounding pain points:

1. **Registration toil.** Every error call site has a hand-picked `E-SUBSYSTEM-NNN` code that must be manually added to `docs/error-codes.md` (200+ rows). No automation prevents drift, duplicates, or orphaned entries.
2. **Signal-finding.** 330 call sites (many in defensive Harmony-patch catches) emit red toasts and log lines indiscriminately. The important failures drown in the forgettable ones.
3. **AI handoff.** The session log is a flat wall of timestamps. To diagnose a failure, a human (or Claude) has to reconstruct player state, mod version, and recent context from scratch.

The mod is built primarily via AI collaboration. The logging system needs to produce output that Claude can read, diagnose, and fix from — without the user authoring deep debugging prose.

## Goals

- Kill the manual registry — codes self-register from source
- Cut noise: only genuinely user-visible failures surface on-screen
- Every surfaced error carries enough context (player state, file:line, stack) that Claude can fix it from the log alone
- Keep the existing `Modules/Enlisted/Debugging/Session-A_*.log` as the sole output channel — no new files, banners, or hotkeys
- No log spam, even when a patch fails in a tight loop

## Non-goals

- New delivery mechanisms (in-game report popups, REPORT-latest.md files, hotkey dumps, telemetry uploads). **Explicitly out of scope** per user direction.
- Migrating historic log files. Archive old registry, move on.
- Changing the underlying Bannerlord `InformationManager.DisplayMessage(...)` toast pipeline.

---

## Design

### 1. Three-tier API

Replace the single `ModLogger.ErrorCode(...)` surface with three tiers chosen by severity at the call site:

| Method | Toast? | Log? | Registry? | When |
|---|---|---|---|---|
| `ModLogger.Surfaced(category, summary, ex = null, ctx = null)` | Yes (red) | Yes | Yes (auto) | Failure visibly affects gameplay — QM, SAVELOAD, ENLIST, RETIRE, MUSTER, BATTLE, DIALOG |
| `ModLogger.Caught(category, summary, ex, ctx = null)` | No | Yes | No | Defensive catch-all in Harmony patches, cleanup, refresh. "This path failed, game continued." |
| `ModLogger.Expected(category, summary, ctx = null)` | No | Yes (Info) | No | Known-branch early exit. "Cannot apply bonuses — no faction." Not an error. |

The `ctx` parameter is a lightweight anonymous object or `Dictionary<string, object>` carrying 3–6 relevant state values (`IsEnlisted`, `Lord`, `Rank`, `Gold`, etc.) — formatted inline with the emission (see §4).

`WarnCode(...)` is retired. Use `Surfaced(...)` for player-visible problems, `Caught(...)` otherwise.

**Migration:** ~20 existing `ErrorCode` sites become `Surfaced`; ~280 become `Caught`; a handful of "cannot proceed, not eligible" sites become `Expected`. Mechanical find-and-replace at call sites; classification is owned by the engineer doing the migration (see writing-plans phase for detailed mapping).

### 2. Auto-registered codes

Codes are computed deterministically from `(category, summary)` at source-scan time — never picked by hand.

**Format:** `E-<CATEGORY>-<hhhh>` where `hhhh` is the first 4 hex chars of SHA-256 of the canonical summary string. Example: `Surfaced("QM", "Error charging gold", ex)` → `E-QM-0a3f` (stable across refactors as long as the summary string doesn't change).

**Collision policy:** Four hex chars = 16-bit space (65 536 buckets). Birthday-bound collision probability within a single category of ~20 codes is well under 1% — small enough to accept. If a collision does occur, the generator script fails the build; the engineer rephrases one summary string to break the tie. Collisions are a global-across-categories concern too; the generator scopes hashes per-category, so two different categories can safely share a suffix.

**Script:** `Tools/Validation/generate_error_codes.py`
- Greps all `ModLogger.Surfaced(...)` calls across `src/`
- Extracts `(category, summary, file:line, last-modified-commit)` per call
- Computes hash → code
- Regenerates `docs/error-codes.md` from scratch (one table per category)
- Fails with non-zero exit on: collision, empty category, empty summary, non-string-literal `category`/`summary` arguments
- Invoked from `validate_content.py` (existing pre-commit validator) so registry stays in sync automatically

**Archive:** The current hand-curated `docs/error-codes.md` is copied to `docs/error-codes-archive.md` on first run for forensic lookup of old log files. The live registry is fully regenerated thereafter.

**Registry format (regenerated):**

```markdown
## Quartermaster (QM)

| Code | Summary | Source | Last changed |
|---|---|---|---|
| E-QM-0a3f | Error charging gold | QuartermasterManager.cs:142 | 2026-04-10 (1c75759) |
| E-QM-1d22 | Error processing equipment variant request | QuartermasterManager.cs:287 | 2026-03-22 (fd287bb) |
```

No more `Remediation` / `Owner` columns — those were rarely kept current. Git blame on the source line is a better answer.

### 3. Anti-spam policy

Every tier has dedup and throttling baked in.

| Tier | Toast policy | Log policy |
|---|---|---|
| `Surfaced` | **Once per session per code.** Subsequent occurrences bump an in-memory counter; no additional toasts. | **First occurrence logs full context + stack.** Subsequent occurrences log a one-line `[repeated ×N]` update, flushed every 30s or on session end. |
| `Caught` | N/A | Dedup by `(category, file, line)` fingerprint. First occurrence logs full. Further occurrences in the same 60s window are counted; on window expiry, single `(suppressed Nx)` line is written. |
| `Expected` | N/A | Throttle by key (first arg). Same key logs once per 60s with a count suffix. |

Exception stack traces are still deduped per-session by unique `ex.ToString()` (existing behavior). The session footer (§4) reports final counts so even suppressed events show up in the "what broke" summary.

### 4. Session log format

The existing `Session-A_*.log` gains three structural pieces. The file format remains line-delimited plain text (no JSON) — human readable first, AI-parseable second.

**4a. Header (written once at session start):**

```
=== ENLISTED SESSION ===
Started:  2026-04-18 14:23:10
Mod:      Enlisted 2.1.4 (commit 4260f51)
Game:     Bannerlord 1.3.13
Player:   Lord X, enlisted to Lord Y (Vlandian), rank 4
Flags:    IsEnlisted=true, InArmy=false, InSettlement=Pravend
========================
```

Values come from `EnlistmentBehavior.Instance` state at session init + reflection on `ApplicationVersion`. Kept to ~10 lines. Written to the session log only (no in-game display).

**4b. Inline context capsule on `Surfaced` emissions:**

```
[2026-04-18 14:31:02] [ERROR] [QM] [E-QM-0a3f] Error charging gold
  ctx: IsEnlisted=true Lord=Vlandian.Caladog Rank=4 Party=MainParty Gold=347
  at QuartermasterManager.cs:142 ChargeFromPlayer(int amount)
  → System.InvalidOperationException: Cannot charge negative amount
    at TaleWorlds.CampaignSystem.Actions.GiveGoldAction...
```

`Caught` emissions get a trimmed version: timestamp, category, summary, file:line, stack. No `ctx` line unless supplied.

**4c. Session summary footer (rolling, rewritten on each `Surfaced` emit and on `OnGameExit`):**

```
=== SUMMARY ===
Surfaced errors: 2
  [E-QM-0a3f] Error charging gold — QuartermasterManager.cs:142 (×3)
  [E-SAVELOAD-1b7c] Error in RestorePartyStateAfterLoad — EnlistmentBehavior.cs:2103 (×1)
Caught (non-surfaced): 7 exceptions across 4 sites — see lines 412, 588, 1103, 1455
Expected (guard-rail): 23 across 5 keys
===============
```

The footer is always at the end of the file — the reader's first jump is to the last 20 lines to see what happened this session, then up to cited line numbers for context. Writing strategy: append to the end on every `Surfaced` error, with the previous footer detected and stripped (simple sentinel line match). Worst case on a 10-MB log this rewrites a few KB per error — acceptable.

### 5. Components and ownership

| Component | Location | Purpose |
|---|---|---|
| `ModLogger` API additions | `src/Mod.Core/Logging/ModLogger.cs` | New `Surfaced` / `Caught` / `Expected` methods + dedup bookkeeping + footer writer |
| Session header writer | `src/Mod.Core/Logging/SessionHeaderWriter.cs` (new) | Builds and writes the header block on session init |
| Footer writer | `src/Mod.Core/Logging/SessionSummaryFooter.cs` (new) | Maintains in-memory counters + rewrites footer on error / exit |
| Code generator | `Tools/Validation/generate_error_codes.py` (new) | Regenerates `docs/error-codes.md` from source |
| Validator hook | `Tools/Validation/validate_content.py` (existing) | Calls generator, fails pre-commit on duplicate/empty |
| Archive | `docs/error-codes-archive.md` (new, one-time) | Frozen copy of current hand-curated registry |
| Call-site migration | All `src/**/*.cs` with `ErrorCode` / `WarnCode` | Mechanical reclassification to `Surfaced` / `Caught` / `Expected` |

### 6. Error handling (of the error system)

Paradox avoidance — the logging system failing must not itself break the mod:

- If footer rewrite fails (disk full, file lock), log the exception to the in-memory buffer and stop attempting footer writes for the rest of the session
- If `ctx` formatting throws (bad ToString on a supplied object), emit the error line without `ctx` and swallow
- `generate_error_codes.py` failing does not block builds by default (only `validate_content.py` gates commits); CI may promote it to an error if desired

### 7. Testing

- **Unit tests** (`tests/Logging/`, if test project exists — otherwise smoke tests): dedup counter, throttle window, footer-sentinel round trip, hash stability across summary string changes
- **Script tests** (`Tools/Validation/tests/`): collision detection, empty-string detection, non-literal argument detection, idempotent regeneration
- **Manual smoke**: start new session, force an error, verify toast fires once, verify footer reflects count, verify log contains header + context capsule + footer

---

## Migration plan (summary — full plan handled by writing-plans)

1. Add `Surfaced` / `Caught` / `Expected` to `ModLogger`. Old `ErrorCode` / `WarnCode` stay as deprecated thin wrappers mapping to `Surfaced` to avoid a big-bang merge
2. Add session header + footer writers. No behavior change for existing `ErrorCode` callers yet
3. Write + test `generate_error_codes.py`; archive current registry; regenerate
4. Migrate call sites in batches by feature folder: Enlistment → QM → Patches → Camp → Content. Each batch is a separate commit
5. Remove the old `ErrorCode` / `WarnCode` wrappers once all call sites are migrated
6. Update `AGENTS.md` / `CLAUDE.md` to reference the new API and drop the "register new codes" checklist item

## Risks / trade-offs

- **Hash codes are less memorable than sequential numbers.** `E-QM-0a3f` doesn't trip off the tongue like `E-QM-005`. Mitigation: registry is always regenerated fresh, so the mapping from hash to summary is one markdown lookup away. If this bites in practice (e.g., modders posting on Discord), switch to readable slugs (`E-QM-gold-charge`) — the same generator infrastructure supports either, only the encoding step changes.
- **Silent `Caught` sites could mask real bugs.** Mitigation: the footer counts `Caught` events per session, so a pattern ("site X fires 500 times per session") is visible at a glance. Promote to `Surfaced` if it turns out to matter.
- **Context capsule content is author-judged.** Bad `ctx` choices make logs less useful. Not fatal — iterate as needed. Provide a suggested ctx helper (e.g., `LogCtx.PlayerState()`) for common bundles.
- **Deprecation window means two APIs live side-by-side briefly.** Accepted cost to avoid a single mega-commit that touches 56 files.
