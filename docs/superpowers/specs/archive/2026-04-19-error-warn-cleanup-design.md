# Error / Warn Cleanup — Design Spec

**Date:** 2026-04-19
**Author:** Claude (Opus 4.7) with Arkpointt
**Status:** Design — awaiting user approval before plan generation
**Target branch:** `development`
**Previous related work:** [2026-04-18 error-code redesign](2026-04-18-error-code-redesign-design.md) (the three-tier `Surfaced` / `Caught` / `Expected` API and auto-generated registry)

---

## Overview

The April error-code redesign migrated the 327 hand-coded `ErrorCode` / `WarnCode` call sites onto the three-tier `Surfaced` / `Caught` / `Expected` API and shipped an auto-generated `docs/error-codes.md`. It deliberately left the lower-tier `ModLogger.Error(...)` and `ModLogger.Warn(...)` methods in place as plain log-only primitives.

Six months on, those primitives are no longer pulling their weight:

- `ModLogger.Error(category, message, ex)` overlaps semantically with `Caught` — same shape, same intent, but it doesn't appear in the registry, doesn't get throttled, and doesn't carry the `[CallerFilePath]` / `[CallerLineNumber]` metadata that makes `Caught` actionable.
- 363 `Error` call sites still exist across 66 files (302 with an exception argument, 61 without). Most of them are Harmony-patch defensive catches that *should* be `Caught` — they look identical to the migrated sites in shape and intent.
- 278 `Warn` call sites are mixed: some are legitimate "noteworthy non-error" log lines, others are defensive guards that misclassified to `Warn` because the engineer didn't want a red toast and didn't yet know `Caught` existed.

This spec drops `ModLogger.Error` from the API entirely, retains `ModLogger.Warn` as the legitimate "log-only, non-error" primitive, reclassifies every `Error` call site, and audits `Warn` opportunistically as we walk the files.

## Goals

1. Delete `ModLogger.Error(...)` from the public surface — every site that used it ends up in one of `Surfaced` / `Caught` / `Expected`.
2. Preserve `ModLogger.Warn(...)` and `ModLogger.Debug(...)` / `ModLogger.Info(...)` as the log-only tier (no toast, not in registry, not throttled by fingerprint).
3. Add a permanent regression gate so future code can never reintroduce `ModLogger.Error(...)`.
4. Opportunistically reclassify `Warn` sites that are *clearly* misusing the primitive as a stealth error logger.

## Non-goals

- No new `ModLogger` API surface beyond what already exists (no new tiers, no new helpers).
- No backfill of optional `ctx` dictionaries on migrated sites — passing `null` is fine; ctx is added organically as future work touches the call site.
- No changes to `Debug` / `Info` (they're the verbose-trace tier, untouched).
- No changes to `SessionHeaderWriter` / `SessionSummaryFooter` / `LogCtx` — those work as-is.
- No re-architecting of error categories — the few category renames that fall out of inlining literals are the only category churn allowed.
- No migration of the `Warn` calls that *are* legitimate log-only entries. We only touch `Warn` sites that are obviously misclassified.

---

## Design decisions (locked from brainstorming)

| Decision | Choice |
|---|---|
| Q1: Role of `Error` / `Warn` going forward | Drop `Error` entirely; keep `Warn` as the log-only primitive |
| Q2: Reclassification rubric | Two-pass migration: mechanical sweep (`Error(cat, msg, ex)` → `Caught`) + judgment pass (no-exception `Error` + opportunistic `Warn` audit) |
| Q3: Category strings on migrated sites | Inline the literal at the call site — no `const string LogCategory` indirection (the registry scanner requires literals anyway) |
| Q4: Batching | Two big commits (mechanical + judgment) plus a final commit that deletes the API and adds the regression gate. Three commits total. |

---

## Current state

### Numbers (from grep on `development` at 2026-04-19)

| Pattern | Sites | Files |
|---|---|---|
| `ModLogger.Error(...)` | 363 | 66 |
| ┗ with exception arg (`Error(cat, msg, ex)`) | 302 | 59 |
| ┗ no exception (`Error(cat, msg)`) | 61 | varies |
| `ModLogger.Warn(...)` | 278 | 75 |
| `ModLogger.ErrorCode(...)` (legacy) | 0 | — |
| `ModLogger.WarnCode(...)` (legacy) | 0 | — |
| `private const string LogCategory = "..."` | 30+ files | 30+ |

### `ModLogger` API surface today

```csharp
public static class ModLogger
{
    public static void Debug(string category, string message);                         // verbose trace
    public static void Info(string category, string message);                          // info trace
    public static void Warn(string category, string message);                          // log-only warning
    public static void Error(string category, string message, Exception ex = null);    // ← TO BE REMOVED
    public static void Caught(string category, string summary, Exception ex,
                              IDictionary<string, object> ctx = null,
                              [CallerFilePath] string callerFile = "",
                              [CallerLineNumber] int callerLine = 0);
    public static void Expected(string category, string key, string summary,
                                IDictionary<string, object> ctx = null);
    public static void Surfaced(string category, string summary, Exception ex = null,
                                IDictionary<string, object> ctx = null,
                                [CallerFilePath] string callerFile = "",
                                [CallerLineNumber] int callerLine = 0);
}
```

### `ModLogger` API surface after this spec

`Error` is gone. Everything else unchanged.

```csharp
public static class ModLogger
{
    public static void Debug(string category, string message);
    public static void Info(string category, string message);
    public static void Warn(string category, string message);
    public static void Caught(...);
    public static void Expected(...);
    public static void Surfaced(...);
}
```

---

## Migration plan

### Commit 1 — Mechanical sweep

**Scope:** the 302 `Error(cat, msg, ex)` sites that take an exception argument.

**Transform (per site):**

```csharp
// Before
ModLogger.Error("Bootstrap", "Failed to apply deferred patch", ex);

// After
ModLogger.Caught("Bootstrap", "Failed to apply deferred patch", ex);
```

**Inline literal categories (per Q3).** Where the call site uses `const string LogCategory`, the mechanical pass inlines the literal:

```csharp
// Before
ModLogger.Error(LogCategory, "Failed to close active menu before muster", ex);

// After
ModLogger.Caught("Muster", "Failed to close active menu before muster", ex);
```

The `LogCategory` const itself stays in the file — other call sites in the same file (e.g., `ModLogger.Debug(LogCategory, ...)`) may still reference it. A later cleanup can delete the const if it falls to zero references.

**Mechanical execution:**

1. Generate the full call-site list with `grep -rn 'ModLogger\.Error(' src/ --include='*.cs'`.
2. Walk the list file-by-file. For each `Error(cat, msg, ex)` call, apply the transform above.
3. For files where the category argument is `LogCategory`, look up the const value in the same file and inline it.
4. Build after each ~10-file batch: `dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64`.
5. Run `python Tools/Validation/validate_content.py` after the full sweep — Phase 10 (registry sync) will regenerate `docs/error-codes.md` with the ~302 newly-captured `Caught` codes.

**Acceptance:**
- Build green (`0 Warning(s) 0 Error(s)`).
- Validation green (Phase 10 reports the new codes; no other phase regresses).
- Post-sweep grep: `grep -rn 'ModLogger\.Error(' src/ --include='*.cs' | grep ', ex'` returns zero hits in the with-exception shape.
- The 61 no-exception `Error(cat, msg)` sites are still present at this point — they are commit 2's job.

**Estimated diff size:** ~600 lines changed (one before + one after per site, plus the LogCategory inlining).

### Commit 2 — Judgment pass

**Scope:** the 61 `Error(cat, msg)` no-exception sites + opportunistic `Warn` audit (any `Warn` site that, in the same file, sits next to a now-`Caught` call or whose message implies failure rather than information).

**Per-site rubric for the 61 no-exception `Error` calls:**

```
Read the call site. What does the code do *after* this log line?

  ├─ Returns / aborts because something the player will notice failed
  │     → Surfaced(category, message, ex: null, ctx?)
  │       (e.g., "Quartermaster hero unavailable" before bailing out of QM open)
  │
  ├─ Returns from a defensive guard ("this should never happen, but if it does...")
  │     → Caught(category, message, ex: null, ctx?)
  │       (e.g., "MusterMenuHandler not registered as campaign behavior" — debug
  │        scaffolding, log-only)
  │
  └─ Reports a known-impossible / known-illegal state we don't expect to hit
        → Expected(category, key, message, ctx?)
          (e.g., "OnSessionLaunched called with null CampaignGameStarter" —
           known guard against engine misuse)
```

If a site is genuinely ambiguous (e.g., one call inside a Harmony patch where the
engine could realistically hit either path), default to `Caught` — it's the
safest classification (logs without spamming a toast).

**Opportunistic `Warn` audit.** As the judgment pass walks each file, it also
inspects nearby `Warn` calls. A `Warn` is reclassified if:

- It logs an exception object (`Warn` doesn't take one — these are usually
  `Warn(cat, $"X failed: {ex.Message}")`. Convert to `Caught(cat, "X failed", ex)`.
- The message contains "failed", "error", "could not" — phrasing inconsistent
  with the "non-error noteworthy event" intent. Convert to `Caught` or
  `Surfaced` per the same rubric above.

A `Warn` that genuinely says something like *"Cached cooking value mismatched
expected; using freshest"* stays as `Warn` — that's exactly what the primitive is
for.

**Acceptance:**
- Build green; validation green.
- Post-pass grep: `grep -rn 'ModLogger\.Error(' src/ --include='*.cs'` returns
  zero hits.
- Diff is reviewable per-file; reviewer can spot-check classification choices.

**Estimated diff size:** ~150 lines (61 Error sites + estimated 30-50 Warn
reclassifications).

### Commit 3 — Delete `Error` + add regression gate

**Scope:** API removal + permanent enforcement.

1. **Delete `ModLogger.Error(...)`** from `src/Mod.Core/Logging/ModLogger.cs`
   (lines 247–274 today). Build verifies nothing references it.

2. **Add regression check** to `Tools/Validation/validate_content.py`. The
   simplest place is to extend Phase 10 (the existing `generate_error_codes.py`
   invocation), or to add a new Phase 11. Either way, the check is a single
   grep:

   ```python
   # In validate_content.py — runs after Phase 10
   def _check_no_modlogger_error_calls(repo_root: Path) -> List[ValidationError]:
       """Phase 11: confirm no ModLogger.Error(...) calls remain in src/.
       This API was deprecated 2026-04-19 — every error-tier log must use
       Surfaced / Caught / Expected. See
       docs/superpowers/specs/2026-04-19-error-warn-cleanup-design.md."""
       errors = []
       for cs_file in (repo_root / "src").rglob("*.cs"):
           content = cs_file.read_text(encoding="utf-8", errors="replace")
           # Strip C# comments to avoid matching cref / inline doc references
           stripped = strip_csharp_comments(content)  # reuse existing helper
           for lineno, line in enumerate(stripped.splitlines(), 1):
               if re.search(r"\bModLogger\.Error\s*\(", line):
                   errors.append(ValidationError(
                       file=str(cs_file.relative_to(repo_root)),
                       line=lineno,
                       phase="Phase 11",
                       message="ModLogger.Error is deprecated. Use Surfaced / Caught / Expected.",
                   ))
       return errors
   ```

   The `strip_csharp_comments` helper already exists in
   `generate_error_codes.py` (added in commit `680ebb0`) — extract it to a
   shared module if it isn't already, then reuse.

3. **Update docs:**
   - `AGENTS.md` "Code Standards" — drop `Error` from the list of severity tiers
     so the canonical four-method API is documented (`Surfaced` / `Caught` /
     `Expected` + `Warn`/`Debug`/`Info` log primitives).
   - `Tools/TECHNICAL-REFERENCE.md` — same.
   - `docs/error-codes-archive.md` — append a one-line note that the API
     was retired on this date.

**Acceptance:**
- Build green (the `Error` method's deletion compiles cleanly because commits
  1 and 2 already removed every caller).
- Validation green; the new Phase 11 (or extended Phase 10) fires zero errors.
- A deliberate sanity test: temporarily add `ModLogger.Error("X", "test");` to
  any source file → run `validate_content.py` → confirms the gate flags it.
  Then revert.

**Estimated diff size:** ~50 lines (one method deletion, ~20 lines of Python
gate, three doc edits).

---

## File-by-file impact summary

| Layer | Files touched | Notes |
|---|---|---|
| `src/Mod.Entry/SubModule.cs` | 1 | 8 mechanical Error→Caught conversions; bootstrap logging |
| `src/Mod.GameAdapters/Patches/*.cs` | ~10 | Mechanical sweep; Naval/Companion/Combat/Conversation patches all fit the pattern |
| `src/Features/Enlistment/*.cs` | 2 | EnlistmentBehavior (25 sites), MusterMenuHandler (10 sites) |
| `src/Features/Equipment/*.cs` | ~12 | QuartermasterManager (26 sites) is the largest; UI VMs and behaviors fill the rest |
| `src/Features/Content/*.cs` | ~10 | ContentOrchestrator, EventDeliveryManager, etc. |
| `src/Features/Conversations/*.cs` | 2 | EnlistedDialogManager (45 sites — second-largest) |
| `src/Features/Interface/*.cs` | 3 | EnlistedNewsBehavior (58 sites — single-largest contributor) |
| `src/Features/Camp/*.cs` | ~7 | All small (≤5 sites each) |
| `src/Features/Retinue/*.cs` | ~5 | All small |
| Other Features | ~14 | Small per-file counts |

The three top-volume files (EnlistedNewsBehavior 58, EnlistedDialogManager 45, QuartermasterManager 26) account for ~30% of the migration. None of them changes shape — every call is the mechanical `Error → Caught` transform.

---

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Mechanical sweep silently changes a real Surfaced-worthy site to Caught | Medium | Player misses a real failure that used to log loudly | The mechanical rule only matches sites that took an exception — those were already non-toasting under `Error`, so behavior is preserved. The judgment pass is where Surfaced choices are made. |
| Inlining `LogCategory` literals introduces typos | Low | Registry rows clustered under wrong category | Build catches typos at compile (literal still has to be a valid string). Phase 10 regen visualizes the result; a post-sweep diff of `docs/error-codes.md` shows category groupings. |
| Phase 10 registry regen produces hash collisions on the bulk new entries | Low | Build fails | The redesign spec acknowledged 4-hex collision is birthday-bound; if it fires, rephrase one summary string to break the tie. The generator already fails loudly with a useful message. |
| Reviewer fatigue on a 600-line mechanical commit | High | Real misclassifications slip through | The diff is genuinely uniform — pre-PR notes will state "this is exclusively `Error → Caught` for the ex-arg shape; review by spot-check, not line-by-line." Tooling (the Phase 11 gate) ensures nothing slips back. |
| The 61-site judgment pass mis-buckets some sites | Medium | Wrong toast behavior on edge cases | Per-site comments in the commit message for any non-obvious call. Easy to revert per-site post-merge if a real player-visible regression shows up. |
| Opportunistic `Warn` audit balloons the judgment commit | Medium | Loss of "two big commits" simplicity | Self-imposed cap: the judgment commit reclassifies ≤30 `Warn` sites; the rest is a follow-up commit if needed. Easy to enforce — the engineer notices when the diff grows past plan. |

---

## Testing and verification

Per-commit gate (identical to prior plans):
- `dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64` → `0 Warning(s) 0 Error(s)`.
- `python Tools/Validation/validate_content.py` → no new errors beyond the 2 pre-existing baseline errors.

Commit-specific verification:

- **Commit 1 (mechanical):** `grep -rn 'ModLogger\.Error(' src/ --include='*.cs' | grep ', ex'` → 0 hits.
  Manual smoke: load a save, trigger one Harmony-patched code path that previously logged `Error` (e.g., open QM conversation with the Scene patch active). Confirm the path still logs (now under `Caught`) without a toast. No behavioral change expected.
- **Commit 2 (judgment):** `grep -rn 'ModLogger\.Error(' src/ --include='*.cs'` → 0 hits.
  Manual smoke: trigger one of the now-`Surfaced` sites identified in the judgment pass (e.g., open QM with the hero deliberately destroyed via `DebugTools`). Confirm the red toast fires once, the registry row exists.
- **Commit 3 (deletion + gate):** Phase 11 gate fires zero on a clean tree; fires non-zero on a deliberately-reintroduced `Error` call. Documented in the commit message.

Pre-merge full-session smoke:
- New save → enlist → muster → visit QM (browse + provisions + upgrades) → camp → battle. Confirm no `Caught` log spam from the migrated sites (throttling is in `Caught`'s implementation already; the migration shouldn't change emission frequency from the prior `Error` baseline).

---

## Open questions

None at spec time. All four design decisions locked in brainstorming. The implementation plan (next step) will enumerate every file and produce the per-commit task list.

---

## Deferred (out of scope for this spec)

- **Backfill `ctx` dictionaries on the migrated sites.** Optional parameter; current value `null`. A future pass can add useful state per call site (player rank, lord, gold) — but doing so during this migration would inflate the diff and dilute reviewer attention. Defer to opportunistic future work as files are touched.
- **Audit `Debug` / `Info` for misclassification.** Both are verbose-trace tiers; a future pass could ask whether some `Debug` calls should promote to `Info` or vice versa. Out of scope here.
- **Delete `LogCategory` consts that fall to zero references after the inlining.** Cleanup work; can be a single follow-up commit on top of this spec's commit 3.
- **Consolidate categories.** The set of category strings in use today is informal (`"QM"`, `"Bootstrap"`, `"Naval"`, `"Muster"`, etc.). A future spec could publish a canonical category taxonomy. Out of scope here — we preserve whatever literal each site already used.
