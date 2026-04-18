# Enlisted Architecture Fixes — Design

**Date:** 2026-04-18
**Author:** Claude (Opus 4.7) with Arkpointt
**Scope:** Tier A + Tier B of the architectural audit performed 2026-04-18
**Target branch:** `development`

---

## Overview

Following the quartermaster reliability fixes (commit `c6e9bff`), an architectural audit flagged eight debt items across the Enlisted mod. This spec addresses the four that are either bug-risk (`#2`, `#8`) or cheap docs wins (`#5`, `#7`). The remaining four (`#1` god-object split, `#3` compound gating, `#4` rules engine unification, `#6` NextFrameDispatcher lint) are deferred to dedicated specs — see *Deferred* at the end.

## Goals

1. Eliminate the foot-gun class where `QuartermasterManager.CapturedTimeMode` leaks state across the 27 call sites that touch it.
2. Replace every raw `qm.PartyBelongedTo?.Party` access with a null-safe resolver so the "QM has no party" design assumption is enforced in one place instead of scattered.
3. Establish a maintained error-code registry (`docs/error-codes.md`) so the `E-*-NNN` codes scattered across the codebase have a single source of truth.
4. Document the `Occupation.Soldier` workaround for the QM hero so future maintainers know why Wanderer isn't used.

## Non-goals

- No gameplay changes. This is purely structural/documentation.
- No dialogue, menu, or UI content changes.
- No changes to save format.
- No god-object split (deferred).
- No rules-engine rework (deferred).
- No new tests (project uses runtime validators + build verification, not unit tests).

---

## Item #2 — `EnlistedTimeScope`

### Problem

`QuartermasterManager.CapturedTimeMode` is a static `CampaignTimeControlMode?` read/written by ~27 call sites across `CampMenuHandler.cs` and `EnlistedMenuBehavior.cs`. The pattern today:

```csharp
QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
GameMenu.SwitchToMenu(...);
// ...later, on menu exit, restore manually.
```

Any path that throws, early-returns, or forgets to restore leaks state into the next session. Runtime symptom: time appears frozen or resumes at the wrong speed after menu navigation.

### Solution

An `IDisposable readonly struct` that captures on construction and restores on `Dispose`, usable with C#'s `using` statement for exception-safe lifetime management.

### Public API

```csharp
namespace Enlisted.Mod.Core.TimeControl
{
    public readonly struct EnlistedTimeScope : IDisposable
    {
        /// <summary>
        /// Capture the current TimeControlMode, optionally pause and lock time
        /// while the scope is active, and restore on Dispose.
        /// </summary>
        /// <param name="pauseAndLock">
        /// When true (default), set TimeControlMode.Stop and SetTimeControlModeLock(true)
        /// for the scope lifetime. When false, only capture — caller manages time itself.
        /// </param>
        public static EnlistedTimeScope Capture(bool pauseAndLock = true);

        public void Dispose();
    }
}
```

### Usage pattern

```csharp
using (EnlistedTimeScope.Capture())
{
    GameMenu.SwitchToMenu("enlisted_status");
}
```

### Restore semantics

On `Dispose`:
1. Release the time-mode lock (only if this scope acquired it).
2. Check whether `Campaign.Current.TimeControlMode` still equals `Stop` or the captured value. If the engine has advanced to some other mode (e.g., player entered a settlement during the scope and the engine overrode), skip the restore — the engine's state is now authoritative.
3. If restore proceeds, use `QuartermasterManager.NormalizeToStoppable(captured)` to pick a stoppable equivalent (preserves the existing "Stop when paused" behavior).

### Decompile verification (Phase 1 of implementation)

Before migrating call sites, verify in `Decompile/TaleWorlds.CampaignSystem/Campaign.cs`:
- Does the engine write `TimeControlMode` during settlement entry, battle start, map-event resolution? If yes, the "skip restore on engine override" logic is required. If no, the check is defensive-but-harmless.
- Does `SetTimeControlModeLock(false)` compose correctly when called twice (e.g., nested scopes)? If not, the scope must track ownership (which it already does via `_ownedLock`).

Record findings in the PR description. If the engine does override, add a unit-style guard log line on every mismatch so we can observe it.

### Migration

1. Add `EnlistedTimeScope.cs` to `src/Mod.Core/TimeControl/`.
2. Grep for `CaptureTimeStateBeforeMenuActivation` and `CapturedTimeMode`. Each call site becomes a `using` block. Expected ~27 sites.
3. Delete from `QuartermasterManager.cs`:
   - `static CampaignTimeControlMode? CapturedTimeMode { get; set; }` property
   - `static void ActivateMenuPreserveTime(string menuId)`
   - `static void CaptureTimeStateBeforeMenuActivation()`
4. Keep `QuartermasterManager.NormalizeToStoppable` as a public static helper (still used by the scope and by unrelated code paths).

### Error codes

- `E-TIME-001` — scope captured without an active `Campaign.Current` (caller called during pre-game; no-op scope returned).
- `E-TIME-002` — restore skipped because engine changed `TimeControlMode` mid-scope (informational, not an error).

---

## Item #8 — `QuartermasterPartyResolver`

### Problem

The QM hero is spawned via `HeroCreator.CreateSpecialHero` and parked in the enlisted lord's home settlement. Consequence: `qm.PartyBelongedTo == null` permanently. The existing `QuartermasterConversationScenePatch` Harmony patch substitutes the lord's party for conversation-partner data at `ConversationManager.OpenMapConversation`. But other call sites that pass `qm.PartyBelongedTo?.Party` to `ConversationCharacterData` rely on the null propagating silently — the Harmony patch fixes only the specific path, leaving the null-passing pattern scattered.

### Solution

A single static resolver that returns a guaranteed-non-null `PartyBase` (or `null` only when both QM and lord are unavailable, which is a fatal state).

### Public API

```csharp
namespace Enlisted.Features.Equipment.Managers
{
    public static class QuartermasterPartyResolver
    {
        /// <summary>
        /// Returns a non-null party for a conversation with the QM hero.
        /// Falls back to the enlisted lord's party when the QM has no party of its own
        /// (the QM is parked in the lord's home settlement and carries no MobileParty).
        /// Returns null only if both the QM and the enlisted lord are unreachable —
        /// callers MUST treat that as a hard error and abort the conversation.
        /// </summary>
        public static PartyBase GetConversationParty(Hero qmHero);
    }
}
```

### Usage pattern

**Before:**
```csharp
var qmData = new ConversationCharacterData(qm.CharacterObject, qm.PartyBelongedTo?.Party);
```

**After:**
```csharp
var party = QuartermasterPartyResolver.GetConversationParty(qm);
if (party == null)
{
    ModLogger.ErrorCode("Quartermaster", "E-QM-PARTY-001", "Both QM and enlisted lord have no party — cannot open conversation");
    InformationManager.DisplayMessage(new InformationMessage(
        new TextObject("{=qm_party_unavailable}The quartermaster cannot be reached right now.").ToString()));
    return;
}
var qmData = new ConversationCharacterData(qm.CharacterObject, party);
```

### Migration

1. Add `QuartermasterPartyResolver.cs` to `src/Features/Equipment/Managers/` (colocated with `QMInventoryState`).
2. Grep `qm.PartyBelongedTo` and `QuartermasterHero.PartyBelongedTo` across `src/`. Expected 3–5 sites.
3. At each site, replace with the resolver. Any call site that builds a `ConversationCharacterData` must also check for null return and abort with `E-QM-PARTY-001`.
4. Thin the `QuartermasterConversationScenePatch` Harmony patch — have the prefix call the resolver for substitution logic. The patch stays because `OpenMapConversation` is an engine entry point we can't route through.

### Decompile verification (Phase 1 of implementation)

Check `Decompile/TaleWorlds.CampaignSystem/Conversation/ConversationCharacterData.cs`:
- What does the constructor do when `PartyBase party` is null? Does it crash on dereference in `CharacterAttributes`, or does it degrade (pick a default scene)?
- If it crashes, `GetConversationParty` returning null MUST be fatal — the caller message must abort conversation, not just log.
- If it degrades, null return is tolerable; we still log `E-QM-PARTY-001` because it's an illegal state if `IsEnlisted == true`.

Record findings in the PR description.

### Error codes

- `E-QM-PARTY-001` — both QM hero and enlisted lord have no party (illegal state).

---

## Item #5 — `docs/error-codes.md`

### Problem

Error codes are sprayed across the codebase (`E-QM-*`, `E-QM-UI-*`, `E-UI-*`, `E-DIALOG-*`, `E-SYSTEM-*`) with no registry. New codes get invented ad-hoc; old codes drift in meaning. Modders and support readers see codes in log output but have no reference for what they mean.

### Solution

A registry file listing every `E-*-NNN` code currently in the codebase with one-line meaning, suggested remediation, and owning subsystem. Link from `CLAUDE.md` Key Documentation table.

### File structure

```markdown
# Error Codes

Codes follow the pattern `E-<SUBSYSTEM>-<NNN>`, where subsystem is an uppercase short
identifier (`QM`, `QM-UI`, `UI`, `DIALOG`, `SYSTEM`, `TIME`) and NNN is a three-digit
sequence within that subsystem.

**Policy:** Gaps are fine — never renumber. When adding a code, append the next unused
number in the relevant subsystem.

## Quartermaster (QM)
| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-QM-008 | No enlisted lord during QM spawn | Check `IsEnlisted` before `GetOrCreateQuartermaster` | Enlistment |
| E-QM-009 | QM spawn: lord has no culture | Investigate how lord was created | Enlistment |
| E-QM-010 | `HeroCreator.CreateSpecialHero` returned null | Check template + birth settlement | Enlistment |
| E-QM-011 | Failed to apply wealthy QM attire (non-fatal) | Log only; QM still spawns | Enlistment |
| E-QM-025 | `GetOrCreateQuartermaster` returned null while enlisted | Upstream spawn failure — check earlier E-QM-* codes | Enlistment |
| E-QM-110 | `ShowArmorSlotPicker` failed (dead code as of 2026-04-18) | N/A — code removed | — |
| E-QM-PARTY-001 | Both QM and lord have no party | Illegal state; investigate enlistment | Enlistment |

## Quartermaster UI (QM-UI)
| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-QM-UI-001 | `ScreenManager.TopScreen` null on equipment popup | Likely a modal blocking; retry after frame | QM UI |
| E-QM-UI-002 | Equipment selector failed to open (exception caught) | Inspect inner exception in log | QM UI |
| E-QM-UI-003 | Upgrade screen failed to open | Inspect inner exception | QM UI |
| E-QM-UI-004 | Provisions screen failed to open | Inspect inner exception | QM UI |
| E-QM-UI-005 | `TopScreen` null when adding provisions layer | Retry after frame | QM UI |
| E-QM-UI-006 | `TopScreen` null when adding upgrade layer | Retry after frame | QM UI |

## Interface (UI)
| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-UI-038 | Error opening quartermaster conversation | See exception log | Interface |
| E-UI-041 | Error opening baggage request conversation | See exception log | Interface |
| E-UI-046 | Failed to toggle orders accordion | Interface bug | Interface |
| E-UI-047 | Failed to toggle decisions accordion | Interface bug | Interface |

## Dialogue (DIALOG)
| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-DIALOG-001 | Unknown dialogue action in JSON | Add handler or fix JSON | Dialogue |
| E-DIALOG-002 | Unknown gate condition in JSON | Add handler or fix JSON | Dialogue |

## Time Control (TIME)
| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-TIME-001 | `EnlistedTimeScope.Capture` with no `Campaign.Current` | Defensive; should not occur in normal play | Time Control |
| E-TIME-002 | Scope restore skipped — engine override detected (informational) | Informational; no action | Time Control |

## System (SYSTEM)

Implementation task: grep `src/` for `E-SYSTEM-\d+` and populate this table with one row per code found. Do not invent meanings — take the message argument of each `ModLogger.ErrorCode(...)` call as the literal "Meaning" and derive "Remediation" from the surrounding exception context.
```

### Implementation approach

1. Grep `ModLogger.ErrorCode(...` and `\"E-[A-Z-]+-\\d+\"` across `src/`.
2. Extract code + message for each.
3. Populate the table.
4. Write an intro describing format + policy.
5. Link from `CLAUDE.md` Key Documentation.

### Maintenance policy

- New codes: append to the relevant subsystem table with the same row shape.
- Removed codes (code deleted from source): strikethrough the row and add a `removed YYYY-MM-DD` note; do not delete the row. Keeps the registry stable for log readers.
- Renumber never.

---

## Item #7 — `Occupation.Soldier` pitfall

### Problem

`EnlistmentBehavior.CreateQuartermasterForLord` sets `qm.SetNewOccupation(Occupation.Soldier)` explicitly to prevent vanilla Bannerlord from triggering companion-recruitment dialogue (which fires for `Occupation.Wanderer`). This is a non-obvious workaround. Future maintainers could "fix" it back to Wanderer without knowing why.

### Solution

1. **Comment at the site** (`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9802`):
   ```csharp
   // Using Occupation.Soldier (not Wanderer) to prevent vanilla companion-recruitment
   // dialogue from firing against the QM. See AGENTS.md "Common Pitfalls" item 11.
   qm.SetNewOccupation(Occupation.Soldier);
   ```

2. **`AGENTS.md` Common Pitfalls addition** (append as item 11):
   ```
   11. Creating mod-spawned heroes (QM, etc.) with `Occupation.Wanderer` triggers
       vanilla companion-recruitment dialogue. Use `Occupation.Soldier`. Verified
       in `../Decompile/TaleWorlds.CampaignSystem/Occupation.cs` and the SandBox
       dialogue registration for recruitable companions.
   ```

### Decompile verification (Phase 1 of implementation)

Before committing the pitfall doc, confirm in `Decompile/`:
- `TaleWorlds.CampaignSystem/Occupation.cs` — enum definition.
- `SandBox` dialogue registration for `Occupation.Wanderer` that gates the recruitment offer. Typical location: `SandBox.CampaignBehaviors/CompanionsCampaignBehavior.cs` or similar.

Record the exact file:line that confirms the gating in the pitfall entry.

---

## Implementation order

One PR, four commits on branch `development` (or a feature branch if preferred):

1. **`refactor(core): introduce EnlistedTimeScope`** — add scope + migrate 27 call sites + delete old `CapturedTimeMode` infrastructure. Build must pass. Manual test: open Camp Hub → Visit Quartermaster → close → confirm time is restored correctly.

2. **`refactor(equipment): centralize QM party resolution via QuartermasterPartyResolver`** — add resolver + migrate call sites + thin the Harmony patch. Build must pass. Manual test: open QM conversation from Camp Hub and from muster-complete auto-open; confirm scene selection is correct (land vs sea).

3. **`docs: add error-code registry`** — generate `docs/error-codes.md` + link from `CLAUDE.md`. No code changes.

4. **`docs: document Occupation.Soldier workaround for QM hero`** — append pitfall #11 to `AGENTS.md` + site comment. No code changes.

Each commit is independently revertible. If any commit introduces a regression, we revert just that one.

---

## Testing and verification

The project uses runtime validation, not unit tests. Per-commit verification:

**Per commit:**
- `dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64` must show `0 Warning(s) 0 Error(s)`.
- `python Tools/Validation/validate_content.py` must not introduce new errors beyond the 2 pre-existing `camp_decisions.json` tooltip errors.

**Per-commit manual smoke:**
- **Commit 1 (EnlistedTimeScope):** Open Camp Hub, switch menus rapidly, enter/exit QM conversation — observe `Session-A_*.log` for `E-TIME-*` codes and confirm no stuck time.
- **Commit 2 (Resolver):** Open QM conversation on land and at sea. Verify scene selection. Confirm no `E-QM-PARTY-001`.
- **Commits 3 + 4:** Markdown renders correctly; links in CLAUDE.md resolve.

**Pre-merge full-session smoke:**
- Start a new save, enlist, complete muster, visit QM, buy equipment, open provisions, close. Confirm no new error codes in the session log.

---

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| `EnlistedTimeScope` migration misses a call site | Medium | Time behaves inconsistently at that menu | Pre-migration grep is exhaustive; compile fails if old API is kept as deleted; grep post-migration for lingering `CapturedTimeMode` references |
| Engine overrides `TimeControlMode` mid-scope in a way we don't detect | Low | Scope restores over legitimate engine state | Decompile verification in Phase 1; the "skip restore if mismatch" guard is defensive |
| `QuartermasterPartyResolver` returns null in a path that doesn't handle it | Low | Crash or silent failure in conversation open | Migration checklist requires every call site to check null and log `E-QM-PARTY-001` |
| Harmony patch thinning introduces subtle behavior change | Low | QM conversation scene picks wrong variant (land vs sea) | Manual smoke test covers both scenes |
| Error-code registry drifts from source as new codes are added | High (ongoing) | Registry becomes stale | Maintenance policy stated in `docs/error-codes.md`; pre-commit doc audit by the contributor |

## Deferred

Not addressed by this spec — tracked for future specs when bandwidth allows:

- **#1 God-object split** (`EnlistedDialogManager.cs`, `QuartermasterManager.cs` ~4000 lines each). Deferred because each is a ~1-week refactor with real regression risk and deserves its own dedicated spec.
- **#3 Compound gating consolidation** — `BaggageAccessState` + supply threshold + rank gates collapsed into a single `QMAvailability` check returning `(bool, reason)`. Deferred because it's UX polish, not a bug.
- **#4 Rules-engine unification** — `QMDialogueContext` specificity matcher + `CheckGateCondition` string switch are two parallel rules engines. Deferred because touching the dialogue context schema means retesting every QM dialogue path.
- **#6 NextFrameDispatcher consistency** — convention lint. Can be addressed by a pre-commit checklist update without a spec.

---

## Open questions

None at brainstorm time. All design decisions locked. Decompile verification items are called out per-section and will be resolved during implementation Phase 1.
