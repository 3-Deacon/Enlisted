# Enlisted Architecture Fixes — Design

**Date:** 2026-04-18
**Author:** Claude (Opus 4.7) with Arkpointt
**Scope:** Tier A + Tier B of the architectural audit performed 2026-04-18
**Target branch:** `development`
**Revision:** 2026-04-18 post-adversarial-review (narrowed item #2, corrected decompile citations, added .csproj registration steps)

---

## Overview

Following the quartermaster reliability fixes (commit `c6e9bff`), an architectural audit flagged eight debt items across the Enlisted mod. This spec addresses four: two bug-risk (`#2` narrowed, `#8`) and two cheap docs wins (`#5`, `#7`). The remaining four (`#1` god-object split, `#3` compound gating, `#4` rules engine unification, `#6` NextFrameDispatcher lint) are deferred — see *Deferred* at the end.

**Revision note:** An adversarial review after v1 of this spec confirmed via decompile that the original design for item #2 assumed all call sites were synchronous. They aren't — 24 of 27 are "capture sync, activate menu async via `NextFrameDispatcher`, restore on menu-exit callback much later." An `IDisposable using` scope cannot span a next-frame boundary. Item #2 is therefore narrowed to the 3 synchronous sites (Gauntlet overlays); the cross-frame menu-lifecycle piece is split out as a new deferred item *#2b*. Other findings from the review — missing `.csproj` registrations, incorrect decompile citations for item #7, and the fact that `ConversationCharacterData` tolerates null party — are all corrected below.

## Goals

1. Eliminate the foot-gun in the 3 synchronous Gauntlet-overlay time captures where exceptions between `SetTimeControlModeLock(true)` and the `finally` block could leak state.
2. Replace every raw `qm.PartyBelongedTo?.Party` access with a null-safe resolver so the "QM has no party" design assumption is enforced in one place — for scene-selection correctness, not crash prevention.
3. Establish a maintained error-code registry (`docs/error-codes.md`).
4. Document the `Occupation.Soldier` workaround for the QM hero with correct decompile references.

## Non-goals

- No gameplay changes. Structural and documentation only.
- No dialogue, menu, or UI content changes.
- No changes to save format.
- No god-object split (deferred).
- No rules-engine rework (deferred).
- **No cross-frame menu-lifecycle refactor** — the 24 cross-frame time captures stay on the current static-field pattern. See *Deferred* item #2b.
- No new unit tests — project uses runtime validators + build verification.

---

## Item #2 — `EnlistedTimeScope` (narrowed to synchronous sites)

### Problem (narrowed)

Three sites in the Gauntlet overlay behaviors do synchronous time capture:

| File | Method | Lines (approx) |
|---|---|---|
| `QuartermasterEquipmentSelectorBehavior.cs` | `ShowEquipmentSelector` | capture ~212, restore in `CloseEquipmentSelector` finally ~342 |
| `QuartermasterEquipmentSelectorBehavior.cs` | `ShowUpgradeScreen` | capture ~424, restore in `CloseUpgradeScreen` finally |
| `QuartermasterProvisionsBehavior.cs` | `ShowProvisionsScreen` | capture ~161, restore in `CloseProvisionsScreen` finally |

Each stores a static `_capturedTimeMode` per behavior and relies on `finally` blocks in the corresponding `Close*Screen` methods. The pattern works, but the try/finally discipline is hand-rolled per-site and duplicated. An exception between `TimeControlMode = Stop` and the Gauntlet layer successfully attaching can leak the lock.

### Solution

A `readonly struct EnlistedTimeScope : IDisposable` that pairs capture with guaranteed restore via C#'s `using` idiom. Applied only to the three sites above.

### Public API

```csharp
namespace Enlisted.Mod.Core.TimeControl
{
    /// <summary>
    /// Scoped capture of Campaign.TimeControlMode with automatic pause + lock,
    /// restored on Dispose. Use ONLY for synchronous regions that fully complete
    /// before the scope disposes.
    ///
    /// DO NOT use for regions where a menu is activated via NextFrameDispatcher
    /// or where restore happens on a menu-exit callback many frames later — the
    /// scope would dispose before the work completes. See deferred item #2b.
    /// </summary>
    public readonly struct EnlistedTimeScope : IDisposable
    {
        public static EnlistedTimeScope Capture(bool pauseAndLock = true);
        public void Dispose();
    }
}
```

### Usage — the Gauntlet overlays

Before (current pattern in `QuartermasterEquipmentSelectorBehavior.cs`):
```csharp
// Capture
if (Campaign.Current != null)
{
    _capturedTimeMode = Campaign.Current.TimeControlMode;
    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
    Campaign.Current.SetTimeControlModeLock(true);
}
// ... work ...
// Restore happens later in CloseEquipmentSelector's finally block
```

After:
```csharp
// Scope field held for the lifetime the Gauntlet layer is open.
private static EnlistedTimeScope? _timeScope;

public static void ShowEquipmentSelector(...)
{
    _timeScope = EnlistedTimeScope.Capture();
    // ... attach layer ...
}

public static void CloseEquipmentSelector(...)
{
    try { /* tear down layer */ }
    finally
    {
        _timeScope?.Dispose();
        _timeScope = null;
    }
}
```

The scope is still cross-call (ShowX captures, CloseX disposes), but the lifetime is bounded to a single Gauntlet overlay's open window, contained within the behavior class, with no frame-spanning dispatcher in between. IDisposable is safe for this shape because the open call and close call are both synchronous Bannerlord-thread entries, and the `finally` in CloseX guarantees Dispose runs even on exception.

### What is NOT touched

The 24 cross-frame captures in `CampMenuHandler.cs` and `EnlistedMenuBehavior.cs` — where capture is sync and menu activation is deferred via `NextFrameDispatcher`, and restore happens on a menu-exit event — keep the current `QuartermasterManager.CapturedTimeMode` static and helper methods. See deferred item #2b for the future design.

### Decompile verification (Phase 1 of implementation)

Before migration, confirm in `../Decompile/`:

- `TaleWorlds.CampaignSystem/Campaign.cs:912-938` — engine internal writes to `TimeControlMode` based on main party state / AI / army leadership. These fire on `Tick`, not on our Gauntlet layer events, so the 3 scoped sites are safe during their window.
- `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameMenus/GameMenu.cs:338` — `ActivateGameMenu` unconditionally sets `TimeControlMode = Stop`. Since the Gauntlet overlays do not call `ActivateGameMenu` during their open window (they sit on top of the current menu), this is not a restore-race risk for the 3 narrow sites.
- `TaleWorlds.CampaignSystem/Campaign.cs:1743` — `SetTimeControlModeLock(bool)` is a plain field assignment. Nesting is a no-op-if-already-set issue for us to manage. The scope's `Dispose` releases the lock once regardless of how many times `SetTimeControlModeLock(true)` was called during the scope. Acceptable for these 3 sites because only one Gauntlet overlay is open at a time (each `ShowX` calls the corresponding `CloseX` first if already open — see the existing `IsOpen` guards).

Record findings in the PR description. If any of the above is inaccurate, the scope's `Dispose` logic must be revisited.

### Migration

1. Add `src/Mod.Core/TimeControl/EnlistedTimeScope.cs`.
2. Add `<Compile Include="src\Mod.Core\TimeControl\EnlistedTimeScope.cs" />` to `Enlisted.csproj` (project disables implicit compile discovery — see CLAUDE.md Rule 2).
3. Migrate the 3 Gauntlet-overlay sites in order: `ShowEquipmentSelector`, `ShowUpgradeScreen`, `ShowProvisionsScreen`. Build after each.
4. Do NOT delete `QuartermasterManager.CapturedTimeMode` or `CaptureTimeStateBeforeMenuActivation` — the 24 cross-frame sites still need them. Add a doc comment to both noting they are retained pending deferred item #2b.

### Error codes

- `E-TIME-001` — `EnlistedTimeScope.Capture` called with no active `Campaign.Current` (defensive; should not occur in normal play).

---

## Item #8 — `QuartermasterPartyResolver`

### Problem (reframed)

The QM hero is spawned via `HeroCreator.CreateSpecialHero` and parked in the enlisted lord's home settlement (`EnlistmentBehavior.cs:9816`). Consequence: `qm.PartyBelongedTo == null` permanently.

**Decompile verification (already done):** `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Conversation/ConversationCharacterData.cs:27` — the constructor takes `PartyBase party = null` as default and just stores it. No crash on null. The `v1` spec incorrectly framed this as crash prevention; the real problem is **scene-selection correctness and invariant scattering**.

The existing `QuartermasterConversationScenePatch` Harmony patch substitutes the lord's party at `ConversationManager.OpenMapConversation` so the conversation scene (land/sea/terrain) picks correctly. But other call sites pass `qm.PartyBelongedTo?.Party` directly to `ConversationCharacterData`, letting the null propagate — and the Harmony patch does not touch those paths. Result: inconsistent scene selection depending on which entry point opened the conversation.

### Solution

A single static resolver so every call site uses the same "who is the QM's effective party" rule, making scene selection consistent.

### Public API

```csharp
namespace Enlisted.Features.Equipment.Managers
{
    public static class QuartermasterPartyResolver
    {
        /// <summary>
        /// Returns a non-null party for a conversation with the QM hero, chosen
        /// so Bannerlord picks the correct conversation scene (land / sea / terrain)
        /// based on where the lord's column actually is.
        ///
        /// The QM has no MobileParty of its own (it is parked in the lord's home
        /// settlement). This resolver substitutes the enlisted lord's party.
        ///
        /// Returns null only if both the QM hero is absent AND the enlisted lord
        /// has no party — an illegal state while IsEnlisted is true. Callers MUST
        /// check for null, log E-QM-PARTY-001, and abort the conversation.
        /// ConversationCharacterData itself tolerates null (decompile ref:
        /// ConversationCharacterData.cs:27), but a null-party conversation gets
        /// degraded scene selection.
        /// </summary>
        public static PartyBase GetConversationParty(Hero qmHero);
    }
}
```

### Usage pattern

**Before** (pattern at multiple sites; example from `EnlistedMenuBehavior.cs:4231`):
```csharp
var qmData = new ConversationCharacterData(qm.CharacterObject, qm.PartyBelongedTo?.Party);
```

**After:**
```csharp
var party = QuartermasterPartyResolver.GetConversationParty(qm);
if (party == null)
{
    ModLogger.ErrorCode("Quartermaster", "E-QM-PARTY-001",
        "Both QM and enlisted lord have no party — cannot open conversation with correct scene");
    InformationManager.DisplayMessage(new InformationMessage(
        new TextObject("{=qm_party_unavailable}The quartermaster cannot be reached right now.").ToString()));
    return;
}
var qmData = new ConversationCharacterData(qm.CharacterObject, party);
```

### Migration

1. Add `src/Features/Equipment/Managers/QuartermasterPartyResolver.cs` (colocated with existing `QMInventoryState`).
2. Add `<Compile Include="src\Features\Equipment\Managers\QuartermasterPartyResolver.cs" />` to `Enlisted.csproj`.
3. Grep: `qm\.PartyBelongedTo`, `QuartermasterHero\.PartyBelongedTo`, `_quartermasterHero\.PartyBelongedTo`. Expected sites: `EnlistedMenuBehavior.cs` (≥2), `MusterMenuHandler.cs`, `QuartermasterConversationScenePatch.cs`, possibly others.
4. At each site, replace with the resolver. If the site builds a `ConversationCharacterData`, also add the null-check + `E-QM-PARTY-001` abort path.
5. Simplify `QuartermasterConversationScenePatch` — its Prefix now calls `QuartermasterPartyResolver.GetConversationParty(qmHero)` instead of reimplementing the lord-party substitution. The Harmony patch itself stays because `ConversationManager.OpenMapConversation` is an engine entry point we can't route our callers through in every case (some paths call it indirectly).

### Error codes

- `E-QM-PARTY-001` — both QM hero and enlisted lord have no usable party (illegal state during enlistment).

---

## Item #5 — `docs/error-codes.md`

### Problem

Error codes are sprayed across the codebase (`E-QM-*`, `E-QM-UI-*`, `E-UI-*`, `E-DIALOG-*`, `E-SYSTEM-*`) with no registry. New codes get invented ad-hoc; old codes drift in meaning.

### Solution

A registry file listing every `E-*-NNN` code currently in the codebase with one-line meaning, suggested remediation, and owning subsystem. Linked from `CLAUDE.md` Key Documentation table.

### File structure

```markdown
# Error Codes

Codes follow the pattern `E-<SUBSYSTEM>-<NNN>`, where subsystem is an uppercase
short identifier (`QM`, `QM-UI`, `UI`, `DIALOG`, `SYSTEM`, `TIME`) and NNN is a
three-digit sequence within that subsystem.

**Policy:** Gaps are fine — never renumber. When adding a code, append the next
unused number in the relevant subsystem. Removed codes (code deleted from source)
get strikethrough + a `removed YYYY-MM-DD` note; do not delete rows so log
readers can still look up historical codes.

## Engine parallel
The Bannerlord engine has its own time-mode capture pattern for popups at
`../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs:2218`
(`_timeControlModeBeforePopUpOpened`). Our `E-TIME-*` codes and the upcoming
menu-lifecycle refactor (deferred item #2b) should stay aligned with this
pattern where practical.

## Quartermaster (QM)
| Code | Meaning | Remediation | Owner |
|---|---|---|---|
| E-QM-008 | No enlisted lord during QM spawn | Check `IsEnlisted` before `GetOrCreateQuartermaster` | Enlistment |
| E-QM-009 | QM spawn: lord has no culture | Investigate how lord was created | Enlistment |
| E-QM-010 | `HeroCreator.CreateSpecialHero` returned null | Check template + birth settlement | Enlistment |
| E-QM-011 | Failed to apply wealthy QM attire (non-fatal) | Log only; QM still spawns | Enlistment |
| E-QM-025 | `GetOrCreateQuartermaster` returned null while enlisted | Upstream spawn failure — check earlier E-QM-* codes | Enlistment |
| ~~E-QM-110~~ | ~~`ShowArmorSlotPicker` failed~~ | removed 2026-04-18 — method deleted | — |
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

## System (SYSTEM)

Implementation task: grep `src/` for `E-SYSTEM-\d+` and populate this table with one row per code found. Do not invent meanings — take the message argument of each `ModLogger.ErrorCode(...)` call as the literal "Meaning" and derive "Remediation" from the surrounding exception context.
```

### Implementation approach

1. Grep `ModLogger.ErrorCode(...` and `"E-[A-Z-]+-\d+"` across `src/`.
2. Extract code + message per call.
3. Populate the table rows.
4. Write the intro describing format + policy.
5. Link from `CLAUDE.md` Key Documentation section.

### Required .csproj entries

None — docs file, not compiled.

### Maintenance policy

- New codes: append to the relevant subsystem table.
- Removed codes: strikethrough with `removed YYYY-MM-DD` note — do not delete rows.
- Renumber never.

---

## Item #7 — `Occupation.Soldier` pitfall

### Problem

`EnlistmentBehavior.CreateQuartermasterForLord` sets `qm.SetNewOccupation(Occupation.Soldier)` explicitly to prevent vanilla Bannerlord from triggering companion-recruitment dialogue (which fires for `Occupation.Wanderer`). Non-obvious workaround; could be "fixed" back to Wanderer by future maintainers who don't know why.

### Solution

1. **Comment at the site** (`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9802`):
   ```csharp
   // Using Occupation.Soldier (not Wanderer) to prevent vanilla wanderer
   // dialogue gating from firing against the QM. The engine gates the
   // wanderer-introduction dialogue on Occupation == Wanderer — see
   // ../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs:1274
   // (conversation_wanderer_on_condition) and :607 (AddWandererConversations).
   // See also AGENTS.md "Common Pitfalls" item 11.
   qm.SetNewOccupation(Occupation.Soldier);
   ```

2. **`AGENTS.md` Common Pitfalls addition** (append as item 11):
   ```
   11. Creating mod-spawned heroes (QM, etc.) with `Occupation.Wanderer` triggers
       vanilla wanderer-introduction dialogue. Use `Occupation.Soldier`. Verified
       at `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs:607`
       (`AddWandererConversations`) and `:1274` (`conversation_wanderer_on_condition`,
       checks `Occupation == Occupation.Wanderer`).
   ```

### Decompile verification (already done at review time)

Confirmed:
- `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs:607` registers wanderer-specific dialogue lines via `AddWandererConversations`.
- `:1274` `conversation_wanderer_on_condition` checks `CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Wanderer`. This is the exact gate that `Occupation.Soldier` bypasses.

The v1 spec's pointer to `CompanionsCampaignBehavior.cs` was wrong — that file tracks wanderer population for companion recruitment at `:188` but does not host the dialogue gating. Corrected in this revision.

### Required .csproj entries

None — documentation + one-line comment in existing file.

---

## Implementation order

One PR, four commits on branch `development`:

1. **`refactor(core): introduce EnlistedTimeScope for synchronous Gauntlet-overlay time capture`**
   - Add `src/Mod.Core/TimeControl/EnlistedTimeScope.cs`.
   - Add `.csproj` compile include.
   - Migrate the 3 Gauntlet-overlay sites. Retain `QuartermasterManager.CapturedTimeMode` + helpers for the 24 cross-frame sites (those go with deferred item #2b).
   - Build must pass. In-game smoke: open Camp Hub → Visit Quartermaster → browse weapons → close — confirm time restored.

2. **`refactor(equipment): centralize QM party resolution via QuartermasterPartyResolver`**
   - Add `src/Features/Equipment/Managers/QuartermasterPartyResolver.cs`.
   - Add `.csproj` compile include.
   - Migrate all `qm.PartyBelongedTo?.Party` sites; add null-check + `E-QM-PARTY-001` abort path at each `ConversationCharacterData` construction.
   - Thin `QuartermasterConversationScenePatch` to use the resolver.
   - In-game smoke: open QM conversation from Camp Hub and from muster-complete auto-open; confirm scene picks correctly on land and at sea.

3. **`docs: add error-code registry`**
   - Write `docs/error-codes.md` from the seed table above, populated by grep of `src/`.
   - Add link to `CLAUDE.md` Key Documentation table.
   - No code changes.

4. **`docs: document Occupation.Soldier workaround for QM hero`**
   - Append pitfall #11 to `AGENTS.md` with correct decompile citations.
   - Add doc comment at `EnlistmentBehavior.cs:9802`.
   - No code changes.

Each commit independently revertible.

---

## Testing and verification

Per-commit gate:
- `dotnet build Enlisted.sln -c "Enlisted RETAIL" -p:Platform=x64` → `0 Warning(s) 0 Error(s)`.
- `python Tools/Validation/validate_content.py` must not introduce new errors beyond the 2 pre-existing `camp_decisions.json` tooltip errors.

Per-commit in-game smoke:
- **Commit 1:** open each of the 3 Gauntlet overlays (equipment, provisions, upgrade), close each, observe `Session-A_*.log` — no `E-TIME-*` codes in happy path; time restored correctly after each close.
- **Commit 2:** open QM conversation from Camp Hub (land) and from muster complete (potentially sea); verify scene; no `E-QM-PARTY-001`.
- **Commits 3 + 4:** markdown renders; `CLAUDE.md` link resolves; `AGENTS.md` Pitfalls list parses.

Pre-merge full-session smoke:
- New save → enlist → complete muster → visit QM → buy equipment → open provisions → close. No new error codes in session log.

---

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Scope of item #2 narrows too aggressively and leaves the 24 cross-frame sites untouched | High (intended) | No change to 24 existing sites — they keep working fragile pattern | Explicitly captured in deferred item #2b; no regression introduced |
| Migration of Gauntlet overlays misses a `finally` placement | Low | Time leaks on the 3 narrow sites | Pre-migration grep for `_capturedTimeMode`; compile fails if old static still referenced; smoke-test all three on/off cycles |
| Engine-internal `TimeControlMode` writes during a Gauntlet overlay's open window | Low | Time restores over engine state | Decompile verification confirms engine writes are tied to `ActivateGameMenu` / AI / popups — none fire during Gauntlet overlay lifetimes |
| `QuartermasterPartyResolver` null return in an unmigrated caller | Low | Scene mismatches (current behavior) | Grep-based migration sweep; post-migration grep for lingering `qm.PartyBelongedTo?.Party` |
| Harmony patch thinning introduces subtle scene-selection change | Low | QM conversation scene picks wrong variant | Smoke test both land and sea |
| Error-code registry drifts from source | High (ongoing) | Registry becomes stale | Maintenance policy stated in file; append-only discipline |

## Deferred

Not addressed by this spec — tracked for future specs when bandwidth allows:

- **#1 God-object split** (`EnlistedDialogManager.cs`, `QuartermasterManager.cs` ~4000 lines each). ~1-week refactor each with real regression risk; each deserves its own dedicated spec.

- **#2b Cross-frame menu-lifecycle time capture** (new, split from original #2). The 24 sites in `CampMenuHandler.cs` + `EnlistedMenuBehavior.cs` that do `CaptureTimeStateBeforeMenuActivation()` followed by `NextFrameDispatcher.RunNextFrame(() => GameMenu.SwitchToMenu(...))` with restore on a menu-exit callback many frames later. The `IDisposable using` shape from the original spec does not fit — the scope would dispose before the next frame runs. A future spec needs to either: (a) hook into `CampaignEvents.OnGameMenuOpened`/`OnGameMenuOptionSelected`/`OnGameMenuExited` for true menu-lifecycle capture-and-release, OR (b) adopt an explicit `Begin()`/`End()` token pair with a watchdog that detects held-too-long captures. Decompile survey required before choosing.

- **#3 Compound gating consolidation** — `BaggageAccessState` + supply threshold + rank gates collapsed into a single `QMAvailability` check returning `(bool, reason)`. UX polish, not a bug.

- **#4 Rules-engine unification** — `QMDialogueContext` specificity matcher + `CheckGateCondition` string switch are two parallel rules engines. Touching the dialogue context schema means retesting every QM dialogue path.

- **#6 NextFrameDispatcher consistency** — convention lint. Can be addressed by a pre-commit checklist update without a spec.

---

## Open questions

None at spec time. All design decisions locked. Decompile verification for item #2 is re-run during implementation Phase 1 against the narrowed scope (3 Gauntlet overlays only).
