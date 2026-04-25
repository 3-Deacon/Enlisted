# Plan 1 — CK3 Wanderer Mechanics: Architecture Foundation

**Status:** Draft v2 (2026-04-24). First of seven plans implementing the [CK3 Wanderer Mechanics Systems Analysis (v6)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md). See spec §8 for the full plan structure and dependency graph.

**Scope:** Architectural commits + substrate scaffolding only. **NO content authoring. NO new mechanic surfaces visible to the player.** The deliverable is a build that compiles cleanly with all new save-definer registrations + helper classes + empty store shells in place — ready for Plans 2-7 to populate.

**Estimated tasks:** 15. **Estimated effort:** 2-3 days with AI-driven implementation.

**Dependencies:** None. Plan 1 is the gate; Plans 2-7 cannot begin until Plan 1's verification report ships.

---

## §0 — Read these first (mandatory orientation for fresh agent chats)

This plan is self-contained but assumes the executor has read these files in order before touching any code:

### Required spec reading

1. **[Spec v6 §0 Scoping](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — mod operates enlisted-only; mechanics silence on retirement.
2. **[Spec v6 §3.1-3.10](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — the ten mechanics being implemented across Plans 1-7 (Plan 1 builds *substrate* for these; doesn't implement any).
3. **[Spec v6 §4.1 Save-definer offset ledger](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — the locked offset assignments. Plan 1 §3 below repeats them inline.
4. **[Spec v6 §6.8 Canonical modal-popup pipeline](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — the `Storylet → BuildModal → EmitCandidate → ShowEventPopup` chain. Plan 1 T5 implements the helper that wraps this.
5. **[Spec v6 §6.9 Mod's existing menu + dialog wiring](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — current Camp hub structure, dialog patterns, namespace conventions.
6. **[Spec v6 §9 Canonical rank system reference](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — the 9-tier ladder. Relevant because T6 fixes a tier-7 gate.

### Required project guidance

7. **[AGENTS.md](../../../AGENTS.md)** — universal critical rules. Especially: §1 (verify TaleWorlds APIs against decompile), §2 (csproj registration), §8 (save system registration), §9 (Windows/WSL portability), §10 (StoryDirector routing), §11 (storylet backbone + save-definer offset convention).
8. **[CLAUDE.md](../../../CLAUDE.md)** — Claude-specific session guidance. Especially the "Common Pitfalls" section with known footguns 4 (deserialization skips ctor), 11 (Occupation.Soldier not Wanderer), 14 (HashSet not saveable), 16 (vanilla type re-registration), 17 (Campaign.Current dereferencing at OnGameStart), 19 (int.MinValue sentinel overflow). Each is referenced inline below where relevant.

### Required existing-code orientation (read before touching the file in question)

9. **`src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`** — current state of save-definer registrations. Plan 1 T2 adds 7 new class offsets + 1 enum offset + 1 generic container. **Plan 1 must NOT re-register any vanilla type already registered here.**
10. **`src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:186-220`** — `TryRemoveStayBackCompanion()`. Plan 1 T6 removes the T7+ early-return at line 188-193.
11. **`src/Features/Retinue/Core/CompanionAssignmentManager.cs`** — existing companion battle-participation toggle. Plan 1 T8 extends it with `IsAssignedToEndeavor` parallel field.
12. **`src/Features/Activities/Home/HomeEveningMenuProvider.cs:37`** — `OnSlotSelected` is the canonical menu→modal precedent. Plan 1 T5 builds `ModalEventBuilder` modeled on this exact shape.
13. **`src/Features/Content/StoryletEventAdapter.cs:56`** — `BuildModal(s, ctx, owner)`. Plan 1 T5's helper wraps this.
14. **`src/Features/Content/StoryDirector.cs:61, 213`** — `EmitCandidate` + `Route`. Plan 1 T5's helper invokes these.
15. **`src/Features/Content/EventDeliveryManager.cs:93, 209`** — `QueueEvent` + `ShowEventPopup`. The bottom of the modal pipeline; Plan 1 doesn't touch but T15 smoke-tests through it.
16. **`src/Features/Conversations/Data/QMDialogueCatalog.cs`** — JSON schema precedent. Plan 1 T7 builds `CompanionDialogueCatalog` modeled on this.
17. **`Enlisted.csproj`** — content deployment config. Plan 1 T7 adds new ItemGroup + MakeDir + Copy per CLAUDE.md known issue (csproj wildcards are non-recursive; new content directories need explicit entries).

### Required decompile orientation (verify against these before T2)

18. **`Decompile/TaleWorlds.Core/SaveableCoreTypeDefiner.cs`** — vanilla core type registrations. Cross-check every new class name in T2 against this list; a duplicate registration crashes module init silently (CLAUDE.md known issue #16).
19. **`Decompile/TaleWorlds.CampaignSystem/SaveableCampaignTypeDefiner.cs`** — vanilla campaign type registrations. Same cross-check requirement.
20. **`Decompile/TaleWorlds.SaveSystem/`** — `ContainerType` enum (List/Queue/Dictionary/Array/CustomList/CustomReadOnlyList only); confirms HashSet is NOT a saveable container (CLAUDE.md known issue #14).

---

## §1 — What this plan delivers

After Plan 1 ships, the codebase is in a state where:

- All save-definer offsets for Plans 2-7 are claimed and registered with empty class shells. **Save-offset claims are one-way commitments;** locking them on day 1 prevents save-compat retrofits across the rest of the 7-plan execution.
- A canonical helper class (`ModalEventBuilder`) wraps the Storylet→Modal pipeline. Plans 3 (Ceremonies), 4 (Officer dialog), 5 (Endeavors), 6 (Patron favors) all call this helper instead of repeating the 10-line `BuildModal + EmitCandidate + Tier + ChainContinuation` boilerplate.
- The companion JSON dialog schema is documented and one stub catalog file is loaded at runtime, proving the loader infrastructure works for non-QM dialogs. Plan 2 authors against this schema.
- The stay-back combat gate fix is shipped, enabling tier-wide withhold-from-combat (required by Plan 2's T1 Sergeant).
- Validator phases 18-20 stubs land in `validate_content.py`, ready for Plans 2/3/5 to populate with content-specific validation.
- The locked-in architecture brief at `docs/architecture/ck3-wanderer-architecture-brief.md` is the contract Plans 2-7 reference.

**No content. No new mechanic surfaces visible to the player.** Just substrate.

---

## §2 — Subsystems explored (audits that informed this plan)

This plan distills findings from five audit rounds across the conversation that produced spec v1-v6. A fresh agent does NOT need to re-run these audits; references are provided so the executor knows where each design decision is grounded.

| Audit topic | Key finding informing Plan 1 | Spec section |
| :-- | :-- | :-- |
| Equipment + ItemModifier | `MBObjectManager.RegisterObject<ItemModifier>` is open at runtime; reflection helper needed for private fields | §6.1 |
| Hero healing + wound model | Healing rate hooks via `DefaultPartyHealingModel.GetDailyHealingHpForHeroes`; vanilla wound model is binary | §6.2 |
| Hero spawning lifecycle | `HeroCreator.CreateSpecialHero(template, settlement, clan, supporter, age)` + post-spawn customization works | §6.5 |
| Companion combat lifecycle | Companions in MainParty fight in lord's battles via `MapEventSide` (`EnlistmentBehavior.cs:1142-1164`); withhold-from-combat enforcement gated to T7+ at `EnlistedFormationAssignmentBehavior.cs:188-193` | §6.5 |
| Companion vanilla preferences | `CompanionGrievanceBehavior` auto-fires trait-gated complaints (Valor/Mercy/Generosity); `DeclareWarDecision` voting weights | §6.6 |
| Rank system end-to-end | 9 tiers, T1-6 line / T7-9 officer; only T6→T7 has dialog ceremony, but `PromotionBehavior.cs:330-401` ALSO emits proving events for T6→T7 (three paths total); `OnTierChanged` event fires for all | §9 |
| Native menu architecture | `AddGameMenu` / `AddWaitGameMenu`; `AddGameMenuOption(..., index)` is positional `List.Insert` (CLAUDE.md issue #19); `InformationManager.ShowMultiSelectionInquiry` is the modal API | §6.8 |
| Native dialog architecture | `DialogFlow.AddPlayerLine` / `AddDialogLine`; tokens are global per session — mod must use unique prefixes; `InteractiveEvent` is NOT a vanilla class (zero matches in decompile) | §6.8 |
| Modal pipeline | `StoryletEventAdapter.BuildModal` → `StoryDirector.EmitCandidate` → `EventDeliveryManager.ShowEventPopup`; `HomeEveningMenuProvider.OnSlotSelected` is the canonical precedent | §6.8 |
| Mod existing wiring | Camp hub options at indices 1/2/3/4/7/100 (slots 5+6 free); JSON dialog schema in `QMDialogueCatalog` is reusable for companions | §6.9 |

---

## §3 — Subsystems Plan 1 touches

Every existing system Plan 1 modifies, with file:line references for executor orientation:

### Files modified (existing)

| File | Change | Task |
| :-- | :-- | :-- |
| `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` | Add 7 class offsets (51-58 minus skipped) + 1 enum offset (84) + 1 generic container | T2, T3, T4 |
| `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:188-193` | Remove T7+ early-return | T6 |
| `src/Features/Retinue/Core/CompanionAssignmentManager.cs` | Add `IsAssignedToEndeavor` field + accessors + SyncData persistence | T8 |
| `Enlisted.csproj` | Add ItemGroup `<CompanionDialogData>` + MakeDir + Copy for `companion_*.json` | T7 |
| `Tools/Validation/validate_content.py` | Add Phase 18/19/20 stub implementations | T14 |
| `AGENTS.md` Rule #11 | Expand reserved offset cluster from 51-60 to 51-70 | T1 |

### Files created (new)

| File | Purpose | Task |
| :-- | :-- | :-- |
| `docs/architecture/ck3-wanderer-architecture-brief.md` | Locked architecture brief — single source of truth for Plans 2-7 | T1 |
| `docs/Features/Conversations/companion-dialog-schema.md` | Companion JSON schema reference doc | T7 |
| `src/Mod.Core/Helpers/ModalEventBuilder.cs` | Canonical modal-pipeline helper | T5 |
| `src/Features/Endeavors/EndeavorActivity.cs` | Empty Activity subclass for player-issued endeavors | T2, T10 |
| `src/Features/Contracts/ContractActivity.cs` | Empty Activity subclass for notable-issued endeavors (sibling) | T2, T10 |
| `src/Features/Patrons/PatronRoll.cs` | Empty Campaign behavior holding `List<PatronEntry>` | T2, T9 |
| `src/Features/Patrons/PatronEntry.cs` | Empty save-class for patron entries | T2, T9 |
| `src/Features/Patrons/FavorKind.cs` | Enum (stub member only initially) | T4 |
| `src/Features/Lifestyles/LifestyleUnlockStore.cs` | Empty Campaign behavior holding `List<string>` | T2, T12 |
| `src/Features/Ceremonies/RankCeremonyBehavior.cs` | Empty Campaign behavior subscribed to `OnTierChanged` (logs only) | T11 |
| `src/Features/PersonalKit/PersonalKitTickHandler.cs` | Empty hourly/daily tick handler (no-op stub) | T13 |
| `src/Features/Conversations/Data/CompanionDialogueCatalog.cs` | JSON dialog loader (mirror `QMDialogueCatalog`) | T7 |
| `ModuleData/Enlisted/Dialogue/companion_test.json` | Stub catalog (2 nodes) to verify loader | T7 |
| `docs/superpowers/plans/2026-04-24-ck3-wanderer-architecture-foundation-verification.md` | Plan 1 verification report | T15 |

### Subsystems Plan 1 does NOT touch (out of scope)

- Storylet authoring (any `*.json` storylet content)
- Hero spawning (`HeroCreator.CreateSpecialHero` calls — Plan 2)
- Dialog branch registration (`AddPlayerLine` / `AddDialogLine` calls — Plans 2-6)
- ItemModifier registrations (Plan 4)
- Healing model patches (Plan 4)
- News-feed substrate (separate from this spec)
- Lifestyle Perks (PerkObject path — explicitly excluded; option 3 unlocks only)

---

## §4 — Locked architecture decisions (the brief, INLINE)

T1 creates a documentation file containing this section verbatim. Subsequent plans reference the doc; this plan section IS the doc content. **All decisions below are locked. No "or" hedging. Plans 2-7 do not relitigate.**

### §4.1 Save-definer offset ledger

Live registrations in `EnlistedSaveDefiner.cs` stop at offset 50 (`DutyCooldownStore`, owned by menu+duty Plan 4). All offsets below are claimed by this plan family.

**Class offsets:**

| Offset | Owner | Mechanic | Plan |
| :---: | :--- | :--- | :---: |
| 51 | `DutyActivity` | Menu+duty Plan C (different spec) | (reserved by other spec) |
| 52 | `ChoreThrottleStore` | Menu+duty Plan B (different spec) | (reserved by other spec) |
| 53 | (RECLAIMED — see T13 decision; if `QualityStore` slots used, this offset stays free) | Personal Kit | T13 |
| 54 | `PatronRoll` | Roll of Patrons | 6 |
| 55 | `PatronEntry` | Roll of Patrons | 6 |
| 56 | `ContractActivity` | Endeavor System (notable-issued) | 5 |
| 57 | `EndeavorActivity` | Endeavor System (player-issued) | 5 |
| 58 | `LifestyleUnlockStore` | Lifestyle Unlocks | 7 |
| 59 | (RECLAIMED — see T11 decision; if `FlagStore` extension used, this offset stays free) | Rank Ceremony state | T11 |
| 60-70 | RESERVED | Future Activity-and-related (Specs 3-5) | — |

**Enum offsets:**

| Offset | Owner | Notes |
| :---: | :--- | :--- |
| 84 | `FavorKind` | LetterOfIntroduction / GoldLoan / TroopLoan / AudienceArrangement / MarriageFacilitation / AnotherContract — populated in Plan 6, stub `None = 0` only in T4 |

(Spec 0 holds 82-83 per AGENTS.md; offsets 80-81 + 85+ are free.)

**Container types — required explicit registrations:**

```csharp
DefineGenericClassDefinition(typeof(List<PatronEntry>));
// (List<string> for LifestyleUnlockStore is already vanilla-registered — no explicit registration needed)
```

Per CLAUDE.md known issue #14: `HashSet<T>` is NOT a saveable container in the TaleWorlds SaveSystem (`ContainerType` only knows `List / Queue / Dictionary / Array / CustomList / CustomReadOnlyList`). Use `List<T>` with runtime dedup, or serialize-to-CSV + rebuild on load. Pattern: `FlagStore.EnsureInitialized` / `QualityStore.EnsureInitialized` from existing code.

### §4.2 Namespace conventions (locked)

| Namespace | Purpose | Mechanic |
| :-- | :-- | :-- |
| `Enlisted.Features.Endeavors` | EndeavorActivity, EndeavorPhaseProvider, catalog | §3.8 |
| `Enlisted.Features.Contracts` | ContractActivity (notable-issued sibling) | §3.8 |
| `Enlisted.Features.Patrons` | PatronRoll, PatronEntry, FavorKind | §3.3 |
| `Enlisted.Features.Lifestyles` | LifestyleUnlockStore | §3.6 |
| `Enlisted.Features.Ceremonies` | RankCeremonyBehavior, CeremonyProvider | §3.9 |
| `Enlisted.Features.PersonalKit` | PersonalKitTickHandler (or PersonalKitStore if T13 dedicated path) | §3.2 |
| `Enlisted.Features.CompanionAptitude` | (no new types in Plan 1; placeholder for Plan 2) | §3.5 |
| `Enlisted.Mod.Core.Helpers` | ModalEventBuilder | T5 |

**Namespace collision check:** None of the above collide with menu+duty Plan 4's `Enlisted.Features.Activities.Orders` or `Enlisted.Features.CampaignIntelligence.Duty`. Plans 2-7 must NOT introduce new namespaces without amending this brief.

### §4.3 Dialog token prefixes (locked)

Dialog token namespace is **global per session** (CLAUDE.md / spec §6.4). Tokens use these prefixes to avoid vanilla and intra-mod collision:

| Prefix | Purpose | Mechanic |
| :-- | :-- | :-- |
| `enlisted_*` | Existing enlistment dialogs (already in use) | shipped |
| `endeavor_*` | Endeavor System | §3.8 |
| `ceremony_*` | Rank Ceremonies | §3.9 |
| `companion_<archetype>_*` | Companion-specific (e.g. `companion_sergeant_intro`, `companion_field_medic_advice`) | §3.10 |
| `patron_*` | Roll of Patrons favor branches | §3.3 |
| `lifestyle_*` | Lifestyle unlock branches | §3.6 |

**Vanilla token reuse rule:** Always layer mod player-lines on existing vanilla input tokens (`lord_pretalk`, `lord_talk_speak_diplomacy_2`, `notable_pretalk`, `hero_main_options`, `companion_role_pretalk`). Use mod-prefixed *output* tokens to keep our sub-trees isolated.

### §4.4 Schema rules (locked)

1. **`List<T>` not `HashSet<T>`** in any save state. Runtime dedup if uniqueness needed. (CLAUDE.md issue #14.)
2. **CSV-encoded dictionaries** in save state where dictionary semantics needed. Pattern: `string PerFavorCooldownsCsv` rebuilt to runtime `Dictionary<FavorKind, CampaignTime>` on load.
3. **`EnsureInitialized()` on every save-class** that has dict/list-typed properties. Reseats null fields with empty instances. Called from `SyncData` (after `dataStore.SyncData(...)`), `OnSessionLaunched`, and `OnGameLoaded`. Pattern: `FlagStore.EnsureInitialized` / `QualityStore.EnsureInitialized` from existing code. (CLAUDE.md issue #4.)
4. **JSON dialog schema** mirrors `QMDialogueCatalog` exactly (id, speaker, textId, text, context, options[]). Companion-specific context fields: `archetype`, `relation_min`, `relation_max`, `tier_min`, `tier_max`, `has_active_endeavor`. Variants share id; specificity-ranked at runtime.
5. **`schemaVersion: 1`** in every JSON catalog file. `dialogueType` field discriminates loader (e.g. `"quartermaster"`, `"companion"`).

### §4.5 "Do not" list (locked)

Plans 2-7 must NOT do any of the following:

1. **Re-register a vanilla TaleWorlds type in `EnlistedSaveDefiner`.** Crashes module init silently. (CLAUDE.md issue #16.) Cross-check against `Decompile/TaleWorlds.Core/SaveableCoreTypeDefiner.cs` and `Decompile/TaleWorlds.CampaignSystem/SaveableCampaignTypeDefiner.cs` before adding any registration.
2. **Use `Occupation.Wanderer` for spawned heroes.** Triggers vanilla wanderer-introduction dialogue. Use `Occupation.Soldier` (CLAUDE.md issue #11). Confirmed via `Decompile/TaleWorlds.CampaignSystem/.../LordConversationsCampaignBehavior.cs:1274`.
3. **Dereference `Campaign.Current.X` at `OnGameStart`.** `DefaultTraits.Mercy/Valor/Honor/Calculating`, `DefaultSkills.*`, `DefaultPerks.*` all NRE at registration time. Pass providers (`Func<TraitObject>`) or resolve inside `OnSessionLaunched`. (CLAUDE.md issue #17.)
4. **Use `HashSet<T>` in save state.** See §4.4 rule 1.
5. **Write to a read-only `QualityStore` quality** from a storylet effect (`rank`, `days_in_rank`, `days_enlisted`). Validator Phase 12 blocks at build. (CLAUDE.md pitfall #13.)
6. **Author scripted-effect ids without registering them.** Validator Phase 12 rejects unknown `apply` values. Reuse seed catalog (`rank_xp_minor`, `lord_relation_up_*`, `scrutiny_down_*`, etc.) where possible. (CLAUDE.md pitfall #14.)
7. **Use `int.MinValue` as a "never fired" sentinel** for throttle fields. Subtraction overflow trips `diff >= interval` checks backwards. Use `int.MinValue / 2`. (CLAUDE.md issue #19.)
8. **Use `EventDeliveryManager.Instance.QueueEvent(evt)` directly** for new mechanic emissions. Bypasses StoryDirector pacing (no floor, no cooldown, no deferral). Use `StoryDirector.Instance?.EmitCandidate(...)` via `ModalEventBuilder` helper. (Critical Rule #10.)
9. **Implement Lifestyle Perks** via `Hero.HeroDeveloper.AddPerk` (PerkObject path). API gap: `RequiredSkillValue` enforced internally; perks inert below skill threshold. Use the unlocks-version (option 3) only — `LifestyleUnlockStore` with feature-id strings.
10. **Migrate `OrderActivity` into `EndeavorActivity`.** Sibling not merge. OrderActivity is shipped with duty-profile state, named-order arc state, reconstruction code; migration risk is unjustified.

### §4.6 Canonical modal pipeline recipe (locked)

For ANY player-choice modal (ceremony, endeavor phase, decision outcome, patron favor outcome), use the helper:

```csharp
ModalEventBuilder.FireCeremony(storyletId, ctx);
// or
ModalEventBuilder.FireEndeavorPhase(storyletId, ctx, ownerActivity);
// or
ModalEventBuilder.FireSimpleModal(storyletId, ctx, chainContinuation: true);
```

Implementation under the hood (T5 builds this verbatim):

```csharp
public static class ModalEventBuilder
{
    public static void FireSimpleModal(string storyletId, StoryletContext ctx, bool chainContinuation)
    {
        var s = StoryletCatalog.Instance?.GetById(storyletId);
        if (s == null)
        {
            ModLogger.Expected("MODAL", "storylet_not_found", $"storylet '{storyletId}' missing", new { storyletId });
            return;
        }
        var evt = StoryletEventAdapter.BuildModal(s, ctx, owner: null);
        var cand = s.ToCandidate(ctx);
        cand.InteractiveEvent = evt;
        cand.ProposedTier = StoryTier.Modal;
        cand.ChainContinuation = chainContinuation;
        StoryDirector.Instance?.EmitCandidate(cand);
    }
    
    public static void FireCeremony(string storyletId, StoryletContext ctx)
        => FireSimpleModal(storyletId, ctx, chainContinuation: true);  // bypass cooldowns
    
    public static void FireEndeavorPhase(string storyletId, StoryletContext ctx, EndeavorActivity owner)
    {
        var s = StoryletCatalog.Instance?.GetById(storyletId);
        if (s == null) { /* expected log */ return; }
        var evt = StoryletEventAdapter.BuildModal(s, ctx, owner);
        var cand = s.ToCandidate(ctx);
        cand.InteractiveEvent = evt;
        cand.ProposedTier = StoryTier.Modal;
        cand.ChainContinuation = true;  // in-progress phases bypass cooldowns
        StoryDirector.Instance?.EmitCandidate(cand);
    }
    
    public static void FireDecisionOutcome(string storyletId, StoryletContext ctx)
        => FireSimpleModal(storyletId, ctx, chainContinuation: false);  // respect cooldowns
}
```

**Pacing rails (`DensitySettings`):** `ModalFloorInGameDays = 5`, `ModalFloorWallClockSeconds = 60`, `CategoryCooldownDays = 3`, `QuietStretchDays = 7`. Modal candidates pass through `ModalFloorsAllow(c, today)`; blocked candidates park in `_deferredInteractive` FIFO queue and retry on daily tick. **`ChainContinuation = true` bypasses the in-game floor and category cooldown** (still respects 60s wall-clock). Use for: ceremony-after-promotion, in-progress endeavor phase-2-3.

### §4.7 AGENTS.md Rule #11 amendment (locked)

T1 amends AGENTS.md Rule #11 ("Save-definer offset convention"):

**Before:** `Class offsets 45-60 are reserved for concrete Activity subclasses...`

**After:** `Class offsets 45-70 are reserved for concrete Activity subclasses AND closely-related surface-spec persistent state... Offsets 51-70 specifically reserved for the CK3 wanderer mechanics cluster (Plans 1-7 of the wanderer spec) plus future surface specs (Specs 3-5).`

---

## §5 — Tooling and safeguards (CLAUDE.md inline)

This section reproduces the relevant build/test/log commands from CLAUDE.md so a fresh agent doesn't need to re-derive them.

### Build command (bash quoting note)

```bash
# CORRECT (bash on Windows, the project's primary shell):
dotnet build -c "Enlisted RETAIL" -p:Platform=x64

# WRONG (the AGENTS.md form trips bash arg parser when config has a space):
# dotnet build Enlisted.sln -c "Enlisted RETAIL" /p:Platform=x64

# PATH prerequisite (add at start of shell session):
export PATH="/c/Program Files/dotnet:/c/Program Files/Git/cmd:$PATH"
```

### Validator commands

```bash
# Full content validation (runs all phases):
/c/Python313/python.exe Tools/Validation/validate_content.py

# Sync localization strings:
/c/Python313/python.exe Tools/Validation/sync_event_strings.py

# Full lint stack (recommended pre-commit):
./Tools/Validation/lint_repo.ps1

# Regenerate error-codes registry (REQUIRED after any line-shifting C# edit
# in a file containing ModLogger.Surfaced/Caught/Expected calls):
/c/Python313/python.exe Tools/Validation/generate_error_codes.py
```

### Save-load round-trip recipe

For each task that adds a new save-class or modifies SyncData, run this manual smoke:

1. Build clean (`dotnet build -c "Enlisted RETAIL" -p:Platform=x64`).
2. Launch game with mod loaded (close BannerlordLauncher first — it holds the DLL open and breaks build).
3. Start a new save (any faction, any character).
4. Press F11 (debug-tools quicksave) or use the menu Save.
5. Reload the save.
6. Save again. Reload again. (4x round-trip.)
7. Inspect logs:
   - Native watchdog: `C:\ProgramData\Mount and Blade II Bannerlord\logs\watchdog_log_<pid>.txt` — must be clean (no native crashes or assertion failures).
   - Mod session: `Modules\Enlisted\Debugging\Session-A_<date>.log` — must show no `Surfaced` errors, no NREs, no missing-field warnings.

### Failure modes to watch for

| Symptom | Cause | Fix |
| :-- | :-- | :-- |
| Game crashes at module init before any logs appear | Vanilla type re-registered in EnlistedSaveDefiner | Cross-check class names against `Decompile/TaleWorlds.Core/SaveableCoreTypeDefiner.cs` |
| Game crashes on save-load with no managed log line | `HashSet<T>` in save state OR null dict/list field on load (no EnsureInitialized) | Use `List<T>`; add `EnsureInitialized()` reseating null fields |
| `validate_content.py` Phase 10 fails | Line-shifting C# edit in a file with `ModLogger.Surfaced/Caught/Expected` calls | Run `Tools/Validation/generate_error_codes.py` and stage `docs/error-codes.md` in the same commit |
| Build fails with MSB3021 (DLL copy fails) | BannerlordLauncher running, holding the DLL open | Close launcher, rebuild |
| New JSON content silently not deployed to game install | Missing csproj `AfterBuild` entries | Verify ItemGroup + MakeDir + Copy entries in `Enlisted.csproj` |

### Git hygiene (per CLAUDE.md)

- Stage with `git add <path>`, NEVER `git add -A` or `git add .` (concurrent AI sessions may have in-flight edits).
- For multi-line commit messages: write to temp file, pass with `git commit -F <file>`. Read the temp file before Write to avoid leaking previous task's content.
- Newly-created `.cs/.csproj/.sln/.ps1` files: run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/normalize_crlf.ps1 -Path <file>` ONCE. Don't re-run (creates double BOM). For edits to existing `.cs` files, rely on `.gitattributes`.

---

## §6 — Tasks (sequential — must complete in order)

Each task has: Goal, Before-state, After-state, Files, Concrete change, Verification, Footgun callouts.

---

### T1 — Architecture brief committed + AGENTS.md amended

**Goal:** Create the canonical architecture brief that Plans 2-7 reference. Amend AGENTS.md Rule #11 to expand the offset cluster.

**Before-state:** No architecture brief. AGENTS.md Rule #11 reserves offsets 45-60.

**After-state:** Brief at `docs/architecture/ck3-wanderer-architecture-brief.md` with the full content of Plan 1 §4 above. AGENTS.md Rule #11 updated to reserve 45-70.

**Files:**
- New: `docs/architecture/ck3-wanderer-architecture-brief.md`
- Edit: `AGENTS.md` (Rule #11 paragraph)

**Concrete change:** Copy Plan 1 §4 (this file's locked decisions section) into the brief. Add a one-paragraph header explaining the brief is the contract Plans 2-7 reference. Add cross-link back to the spec.

**Verification:** Document review. No code changes here. `git diff AGENTS.md` shows only the Rule #11 paragraph changed.

**Footgun:** None — pure documentation.

---

### T2 — Save-definer offsets registered (class types)

**Goal:** Add empty class definitions to `EnlistedSaveDefiner.cs` for offsets 54-58 + conditional offsets per T11/T13 decisions. Build clean. Save-load round-trip clean.

**Before-state:** `EnlistedSaveDefiner.cs` registers offsets 40-50 (Spec 0 + menu+duty Plan 4).

**After-state:** Adds offsets 54-58 explicitly. Offsets 53 and 59 may or may not be claimed depending on T11/T13 decisions; T2 stubs both with empty placeholder classes that T11/T13 may delete if those tasks pick "extend existing store" paths.

**Files:**
- Edit: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`
- New: `src/Features/Endeavors/EndeavorActivity.cs` (empty class extending Activity)
- New: `src/Features/Contracts/ContractActivity.cs` (empty class extending Activity)
- New: `src/Features/Patrons/PatronRoll.cs` (empty Campaign behavior)
- New: `src/Features/Patrons/PatronEntry.cs` (empty save-class)
- New: `src/Features/Lifestyles/LifestyleUnlockStore.cs` (empty Campaign behavior)
- (Conditional T13) New: `src/Features/PersonalKit/PersonalKitStore.cs` — leave empty for now; T13 may delete
- (Conditional T11) New: `src/Features/Ceremonies/RankCeremonyState.cs` — leave empty for now; T11 may delete
- Edit: `Enlisted.csproj` — add `<Compile Include>` lines for each new `.cs` file (CLAUDE.md note: csproj wildcards are non-recursive)

**Concrete change in `EnlistedSaveDefiner.cs`:**

```csharp
// In DefineClassTypes() method, after existing offset 50:
AddClassDefinition(typeof(Features.PersonalKit.PersonalKitStore), 53);  // T13 may remove
AddClassDefinition(typeof(Features.Patrons.PatronRoll), 54);
AddClassDefinition(typeof(Features.Patrons.PatronEntry), 55);
AddClassDefinition(typeof(Features.Contracts.ContractActivity), 56);
AddClassDefinition(typeof(Features.Endeavors.EndeavorActivity), 57);
AddClassDefinition(typeof(Features.Lifestyles.LifestyleUnlockStore), 58);
AddClassDefinition(typeof(Features.Ceremonies.RankCeremonyState), 59);  // T11 may remove
```

**Skeleton class template** (apply to each new save-class):

```csharp
namespace Enlisted.Features.<Namespace>
{
    [Serializable]
    public sealed class <ClassName>
    {
        // Plan 1 T2: empty shell. Plan N populates.
        public <ClassName>() { }
        public void EnsureInitialized() { /* no fields yet */ }
    }
}
```

**Verification:**
1. `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` passes.
2. Save-load round-trip recipe (§5) — 4x cycle, no native watchdog errors.
3. Mod session log shows all 7 new types registered without warnings.

**Footgun:** Per CLAUDE.md known issue #16, **read `Decompile/TaleWorlds.Core/SaveableCoreTypeDefiner.cs` and `Decompile/TaleWorlds.CampaignSystem/SaveableCampaignTypeDefiner.cs` BEFORE running this task** to confirm none of the class names collide with vanilla. (Names like `EndeavorActivity` / `PatronRoll` are mod-specific; risk is low but verify.)

---

### T3 — Generic class definitions registered (containers)

**Goal:** Register `List<PatronEntry>` so the SaveSystem can serialize it.

**Before-state:** `List<PatronEntry>` not registered. PatronRoll cannot serialize its `Entries` field.

**After-state:** `List<PatronEntry>` registered via `DefineGenericClassDefinition`.

**Files:** Edit `EnlistedSaveDefiner.cs`

**Concrete change:** In `DefineGenericClassDefinitions()` (or equivalent — read existing structure), add:

```csharp
ConstructContainerDefinition(typeof(List<Features.Patrons.PatronEntry>));
// (Or DefineGenericClassDefinition — check existing usage for the right method.)
```

(`List<string>` for `LifestyleUnlockStore` is vanilla-registered — no explicit registration needed.)

**Verification:** Save-load round-trip with one stub `PatronEntry` in a stub `PatronRoll.Entries` list round-trips correctly. (Use Debug Tools to instantiate; confirm via mod session log.)

**Footgun:** Wrong registration method (e.g. `AddClassDefinition` instead of generic) silently fails to register the container shape. Verify by saving and reloading with a non-empty list.

---

### T4 — `FavorKind` enum registered

**Goal:** Register the enum at offset 84. Empty body with stub member; full member list added in Plan 6.

**Before-state:** No `FavorKind` enum exists.

**After-state:** Enum exists with one stub member; registered in `EnlistedSaveDefiner.cs:DefineEnumTypes()`.

**Files:**
- New: `src/Features/Patrons/FavorKind.cs`
- Edit: `EnlistedSaveDefiner.cs`
- Edit: `Enlisted.csproj` (add `<Compile Include>` for the new file)

**Concrete change:**

```csharp
// src/Features/Patrons/FavorKind.cs
namespace Enlisted.Features.Patrons
{
    public enum FavorKind
    {
        None = 0,
        // Plan 6 populates: LetterOfIntroduction, GoldLoan, TroopLoan, AudienceArrangement, MarriageFacilitation, AnotherContract
    }
}

// EnlistedSaveDefiner.cs DefineEnumTypes():
AddEnumDefinition(typeof(Features.Patrons.FavorKind), 84);
```

**Verification:** Build clean. Save-load round-trip clean. (No instances yet, but registration must not crash module init.)

**Footgun:** None — registering an enum is the lowest-risk save-definer addition.

---

### T5 — `ModalEventBuilder` helper class

**Goal:** Centralize the modal pipeline recipe (§4.6) so Plans 3, 4, 5, 6 don't reinvent it.

**Before-state:** No helper exists. Future code would write 10 lines of boilerplate per modal-fire site.

**After-state:** `src/Mod.Core/Helpers/ModalEventBuilder.cs` provides static methods. A Debug Tools menu option fires a stub modal via the helper, proving the path end-to-end.

**Files:**
- New: `src/Mod.Core/Helpers/ModalEventBuilder.cs`
- New stub storylet: `ModuleData/Enlisted/Storylets/test_modal.json` (2 options, simple effects)
- Edit: `src/Debugging/Behaviors/DebugToolsBehavior.cs` — add a "Fire test modal" debug menu option
- Edit: `Enlisted.csproj` — add `<Compile Include>` for the new helper

**Concrete change:** Implement `ModalEventBuilder` per the §4.6 recipe verbatim. Add a Debug Tools menu option (in the existing debug menu) that calls `ModalEventBuilder.FireSimpleModal("test.modal", new StoryletContext { ActivityTypeId = "debug", PhaseId = "smoke" }, true)`.

**Stub storylet JSON:**

```json
{
  "schemaVersion": 1,
  "storylets": [
    {
      "id": "test.modal",
      "category": "debug",
      "title": "Test Modal",
      "setup": "This is a Plan 1 T5 smoke test. Pick an option.",
      "options": [
        { "id": "ok", "text": "Looks good", "effects": [] },
        { "id": "fail", "text": "Something's broken", "effects": [] }
      ]
    }
  ]
}
```

**Verification:**
1. Build clean.
2. Launch game; trigger Debug Tools "Fire test modal" option.
3. Verify modal popup renders with title, body, and 2 options.
4. Verify selecting either option closes the modal cleanly without errors.
5. Verify mod session log shows the storylet emitted via `StoryDirector.EmitCandidate` and option-selected via `EventDeliveryManager.OnOptionSelected`.

**Footgun:** If `StoryletEventAdapter.BuildModal` returns null (storylet not found), the helper must Expected-log and bail without crashing. Test by deliberately referencing a non-existent storylet id; verify graceful failure.

---

### T6 — Stay-back gate fix at line 190

**Goal:** Remove T7+ early-return so withhold-from-combat applies at all tiers.

**Before-state:** `EnlistedFormationAssignmentBehavior.cs:188-193` returns early if `EnlistmentTier < CommanderTier1` (T7). Companions at T1-T6 spawn into battle regardless of `ShouldCompanionFight` setting.

**After-state:** Gate removed. `IsEnlisted` check preserved. Companions at any tier respect the toggle.

**Files:** Edit `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs`

**Concrete change:**

```csharp
// BEFORE (lines 186-193):
private void TryRemoveStayBackCompanion(Agent agent)
{
    // Only process companions from player's party at Commander tier (T7+).
    var enlistment = EnlistmentBehavior.Instance;
    if (enlistment?.IsEnlisted != true || enlistment.EnlistmentTier < RetinueManager.CommanderTier1)
    {
        return;
    }
    // ... rest of method continues at line 195

// AFTER:
private void TryRemoveStayBackCompanion(Agent agent)
{
    // Plan 1 T6: gate removed; withhold-from-combat applies at all tiers.
    var enlistment = EnlistmentBehavior.Instance;
    if (enlistment?.IsEnlisted != true)
    {
        return;
    }
    // ... rest of method continues
```

**Verification (manual smoke test required):**
1. Use Debug Tools to spawn a test companion (any troop template) in the player's MainParty at T1.
2. Use the existing Camp menu Companions option to set the test companion's "Fight" toggle to OFF.
3. Trigger a battle (combat with a bandit party).
4. In the deployment screen / mission, confirm the test companion does NOT spawn as an agent.
5. Set the toggle back to ON, trigger another battle, confirm the companion DOES spawn.
6. Repeat at T3, T5 to confirm tier-wide behavior.

**Footgun:** Line numbers shift. **Run `Tools/Validation/generate_error_codes.py` after this edit** if any `ModLogger.Surfaced/Caught/Expected` call sites in this file have line shifts. Stage `docs/error-codes.md` in the same commit as the gate fix.

---

### T7 — Companion JSON dialog schema doc + reference catalog

**Goal:** Document the schema; add `CompanionDialogueCatalog` loader (mirror `QMDialogueCatalog`); ship one stub catalog file proving the loader works.

**Before-state:** No companion JSON dialog infrastructure. Only `QMDialogueCatalog` exists for QM dialog.

**After-state:**
- Schema reference doc at `docs/Features/Conversations/companion-dialog-schema.md`.
- Loader at `src/Features/Conversations/Data/CompanionDialogueCatalog.cs` mirrors QM loader.
- Stub catalog at `ModuleData/Enlisted/Dialogue/companion_test.json` with 2 nodes proves the loader registers variants without crash.

**Files:**
- New: `docs/Features/Conversations/companion-dialog-schema.md`
- New: `src/Features/Conversations/Data/CompanionDialogueCatalog.cs`
- New: `ModuleData/Enlisted/Dialogue/companion_test.json`
- Edit: `Enlisted.csproj` — add ItemGroup, MakeDir, Copy for `companion_*.json`. CRITICAL per CLAUDE.md known issue: csproj `AfterBuild` needs three additions (ItemGroup `<CompanionDialogData Include>`, MakeDir for the directory, Copy step).

**Schema reference doc structure (mirror QM exactly):**
- Top-level fields: `schemaVersion`, `dialogueType: "companion"`, `nodes[]`
- Per-node: `id` (prefixed `companion_<archetype>_*`), `speaker: "companion"`, `textId`, `text`, `context`, `options[]`
- Context fields: `archetype`, `relation_min`, `relation_max`, `tier_min`, `tier_max`, `has_active_endeavor`, `is_introduced`
- Per-option: `id`, `textId`, `text`, `action`, `next_node`, `requirements`, `gate`
- Specificity ranking: more specific context = higher runtime priority

**Stub catalog JSON:**

```json
{
  "schemaVersion": 1,
  "dialogueType": "companion",
  "nodes": [
    {
      "id": "companion_test_intro",
      "speaker": "companion",
      "textId": "companion_test_intro_text",
      "text": "Hello, soldier. I'm a test companion.",
      "context": { "is_introduced": false },
      "options": [
        { "id": "ack", "text": "Hello.", "next_node": "companion_test_hub" }
      ]
    },
    {
      "id": "companion_test_hub",
      "speaker": "companion",
      "textId": "companion_test_hub_text",
      "text": "What do you want?",
      "context": {},
      "options": [
        { "id": "leave", "text": "Nothing. Goodbye.", "next_node": "close_window" }
      ]
    }
  ]
}
```

**csproj additions (verbatim — three changes):**

```xml
<!-- ItemGroup near the existing data ItemGroups: -->
<ItemGroup>
  <CompanionDialogData Include="ModuleData\Enlisted\Dialogue\companion_*.json" />
</ItemGroup>

<!-- Inside <Target Name="AfterBuild">, add a MakeDir for the directory: -->
<MakeDir Directories="$(OutputPath)..\..\ModuleData\Enlisted\Dialogue" />
<!-- (Already exists if QM dialog uses the same directory; verify before duplicating.) -->

<!-- Inside <Target Name="AfterBuild">, add a Copy step: -->
<Copy SourceFiles="@(CompanionDialogData)"
      DestinationFolder="$(OutputPath)..\..\ModuleData\Enlisted\Dialogue\" />
```

**Verification:**
1. Build clean.
2. Launch game with mod loaded.
3. Mod session log shows `CompanionDialogueCatalog` loaded with 2 nodes from `companion_test.json` without errors.
4. The stub catalog is NOT actually wired to dialog yet (Plan 2 does that). T7 only verifies the loader infrastructure.

**Footgun:** Missing csproj `AfterBuild` Copy step = JSON file not deployed to game install at `Modules\Enlisted\ModuleData\Enlisted\Dialogue\`. Loader logs "no_companion_dialog_dir" or "directory not found" at info level. Easy to miss — verify the file exists in the deployed location before declaring T7 done.

---

### T8 — `CompanionAssignmentManager` extension scaffolding

**Goal:** Add `IsAssignedToEndeavor` field + accessors. Empty implementations now; populated in Plan 5.

**Before-state:** `CompanionAssignmentManager` tracks only `_companionBattleParticipation` (Fight / Stay Back).

**After-state:** Adds parallel `_companionEndeavorAssignment` field with same Dictionary<string, bool> shape. `IsAssignedToEndeavor(Hero)` getter, `SetAssignedToEndeavor(Hero, bool)` setter. SyncData persists.

**Files:** Edit `src/Features/Retinue/Core/CompanionAssignmentManager.cs`

**Concrete change:**

```csharp
// Add field at top of class, near _companionBattleParticipation:
private Dictionary<string, bool> _companionEndeavorAssignment = new Dictionary<string, bool>();

// Add getter:
public bool IsAssignedToEndeavor(Hero hero)
{
    if (hero?.StringId == null) return false;
    return _companionEndeavorAssignment.TryGetValue(hero.StringId, out var assigned) && assigned;
}

// Add setter:
public void SetAssignedToEndeavor(Hero hero, bool assigned)
{
    if (hero?.StringId == null) return;
    _companionEndeavorAssignment[hero.StringId] = assigned;
}

// Update SyncData() to persist:
dataStore.SyncData("_companionEndeavorAssignment", ref _companionEndeavorAssignment);

// Update EnsureInitialized() (if exists) to reseat null:
if (_companionEndeavorAssignment == null) _companionEndeavorAssignment = new Dictionary<string, bool>();
```

**Verification:**
1. Build clean.
2. Save-load round-trip with at least one assignment flag set via Debug Tools — flag persists across reload.
3. `validate_content.py` Phase 10 (error registry) still passes (line shifts may have moved Surfaced/Caught/Expected calls — regenerate if so).

**Footgun:** Per CLAUDE.md issue #4, `_companionEndeavorAssignment` must be reseated in `EnsureInitialized()` because SyncData on a save predating this field returns null.

---

### T9 — `PatronRoll` empty class shell + lifecycle stubs

**Goal:** Stub the PatronRoll Campaign behavior + PatronEntry save-class with the design from spec §3.3, but no logic — just shells. Plan 6 populates.

**Before-state:** `PatronRoll.cs` and `PatronEntry.cs` are empty placeholder classes from T2.

**After-state:** Both classes have full field declarations + stub method signatures + `EnsureInitialized()`. Bodies are empty / log-only.

**Files:**
- Edit: `src/Features/Patrons/PatronRoll.cs`
- Edit: `src/Features/Patrons/PatronEntry.cs`

**Concrete change for `PatronRoll.cs`:**

```csharp
namespace Enlisted.Features.Patrons
{
    public sealed class PatronRoll : CampaignBehaviorBase
    {
        public static PatronRoll Instance { get; private set; }
        
        [SaveableField(1)]
        private List<PatronEntry> _entries = new List<PatronEntry>();
        
        public IReadOnlyList<PatronEntry> Entries => _entries;
        
        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            // Plan 6 adds: discharge hook, OnHeroKilled hook
        }
        
        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_entries", ref _entries);
            EnsureInitialized();
        }
        
        public void EnsureInitialized()
        {
            if (_entries == null) _entries = new List<PatronEntry>();
        }
        
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            EnsureInitialized();
        }
        
        // Stub methods — Plan 6 populates:
        public void OnDischarge(Hero lord, /* DischargeReason */ object reason) { /* stub */ }
        public void OnHeroKilled(Hero deceased) { /* stub */ }
        public void Clear() { _entries.Clear(); }
        public bool Has(MBGUID heroId) => false; // stub
        public bool TryGrantFavor(MBGUID patronId, FavorKind kind, out object outcome)
        { outcome = null; return false; } // stub
    }
}
```

**Concrete change for `PatronEntry.cs`:**

```csharp
namespace Enlisted.Features.Patrons
{
    [Serializable]
    public sealed class PatronEntry
    {
        [SaveableField(1)] public MBGUID HeroId;
        [SaveableField(2)] public int DaysServed;
        [SaveableField(3)] public int MaxRankReached;
        [SaveableField(4)] public CampaignTime DischargedOn;
        [SaveableField(5)] public CampaignTime LastFavorRequestedOn;
        [SaveableField(6)] public string PerFavorCooldownsCsv = "";
        [SaveableField(7)] public int RelationSnapshotOnDischarge;
        [SaveableField(8)] public bool IsDead;
        [SaveableField(9)] public string FactionAtDischarge = "";
        
        public PatronEntry() { }
    }
}
```

**Verification:**
1. Build clean.
2. Save-load round-trip with one stub `PatronEntry` injected via Debug Tools (set HeroId to MainHero, fill other fields with sentinel values). Confirm round-trip preserves all 9 fields.
3. Test load of a pre-Plan-1 save (where PatronRoll didn't exist): `EnsureInitialized()` must reseat `_entries` to empty list without crash.

**Footgun:** Per CLAUDE.md issue #4, missing `EnsureInitialized()` call in `SyncData` causes `Entries` to be null on load from old saves; subsequent `_entries.Add(...)` NREs.

---

### T10 — `EndeavorActivity` + `ContractActivity` empty Activity subclasses

**Goal:** Both extend `Activity` (Spec 0 backbone). Stub fields for phase tracking; stub completion logic. Save-class registered (already done in T2). Build clean.

**Before-state:** Empty placeholder classes from T2.

**After-state:** Both classes have full field declarations + stub method signatures matching the Spec 0 Activity contract. Bodies log-only.

**Files:**
- Edit: `src/Features/Endeavors/EndeavorActivity.cs`
- Edit: `src/Features/Contracts/ContractActivity.cs`

**Concrete change:** Read existing `OrderActivity.cs` (per spec §3.4 — `src/Features/Activities/Orders/OrderActivity.cs`) as the model. Both new Activity subclasses follow the same shape — `OnStart`, `OnTick`, `OnComplete`, save fields. Stubs for now.

```csharp
[Serializable]
public sealed class EndeavorActivity : Activity
{
    [SaveableField(1)] public string EndeavorId = "";
    [SaveableField(2)] public int CurrentPhase;
    [SaveableField(3)] public int TotalPhases;
    [SaveableField(4)] public List<MBGUID> AssignedCompanionIds = new List<MBGUID>();
    [SaveableField(5)] public CampaignTime StartedOn;
    
    public override void OnStart() { /* Plan 5 populates */ }
    public override void OnTick(/* args */) { /* Plan 5 populates */ }
    public override void OnComplete() { /* Plan 5 populates */ }
}
```

(Same pattern for `ContractActivity`.)

**Verification:**
1. Build clean.
2. Use Debug Tools (or in-test instantiation) to spawn a stub `EndeavorActivity` in the ActivityRuntime. Verify it persists across save-load.
3. Confirm OrderActivity (existing) still works — Plan 1 must not regress it.

**Footgun:** If ActivityRuntime expects specific virtual method overrides, missing ones may cause silent failures. Cross-check `OrderActivity` for the full virtual surface and stub all of them.

---

### T11 — `RankCeremonyState` decision + scaffold

**Goal:** Pick path: dedicated state class at offset 59, OR extend existing `FlagStore` with ceremony flags. Implement the chosen path.

**Decision (Plan 1 v2 RECOMMENDS option 2):** Use `FlagStore` extension. Choices go to existing `FlagStore` with keys like `ceremony.t3.choice = "frugal"`. Don't burn an offset on what fits in an existing store.

**Before-state:** Empty placeholder `RankCeremonyState.cs` from T2 reserves offset 59.

**After-state (option 2):**
- `RankCeremonyState.cs` deleted
- Offset 59 reclaimed (T2's `AddClassDefinition` line for offset 59 removed from `EnlistedSaveDefiner.cs`)
- New file `src/Features/Ceremonies/RankCeremonyBehavior.cs` — empty Campaign behavior subscribed to `OnTierChanged`, logs only (no modal fire yet)

**Files:**
- Delete: `src/Features/Ceremonies/RankCeremonyState.cs` (and its `<Compile Include>` in csproj)
- Edit: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` (remove offset 59 registration)
- New: `src/Features/Ceremonies/RankCeremonyBehavior.cs`
- Edit: `Enlisted.csproj` (add `<Compile Include>` for new file)

**Concrete change for `RankCeremonyBehavior.cs`:**

```csharp
namespace Enlisted.Features.Ceremonies
{
    public sealed class RankCeremonyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            // Subscribe to OnTierChanged to detect promotions across all paths
            // (auto proving-event, decline-then-dialog, dialog-request)
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
        }
        
        public override void SyncData(IDataStore dataStore) { }
        
        private void OnTierChanged(int previousTier, int newTier)
        {
            // Plan 1 T11: log-only. Plan 3 populates with ModalEventBuilder.FireCeremony.
            ModLogger.Expected("CEREMONY", "tier_changed",
                $"Tier {previousTier} -> {newTier} (Plan 3 will fire ceremony)",
                new { previousTier, newTier });
        }
    }
}
```

**Verification:**
1. Build clean.
2. Use Debug Tools to force a tier change (e.g. `EnlistmentBehavior.Instance.SetTier(currentTier + 1)`).
3. Mod session log shows `CEREMONY tier_changed` line with correct previous/new tiers.
4. Save-load round-trip clean.

**Footgun:** `EnlistmentBehavior.OnTierChanged` is a static event. Subscriber must unsubscribe on session-end if the behavior is destroyed (typically not an issue for CampaignBehaviorBase).

---

### T12 — `LifestyleUnlockStore` empty class

**Goal:** Stub the LifestyleUnlockStore Campaign behavior with `List<string>` for unlocked feature ids. Plan 7 populates the catalog.

**Before-state:** Empty placeholder from T2.

**After-state:** Full class with field, IsUnlocked, Unlock with runtime dedup, EnsureInitialized.

**Files:** Edit `src/Features/Lifestyles/LifestyleUnlockStore.cs`

**Concrete change:**

```csharp
namespace Enlisted.Features.Lifestyles
{
    public sealed class LifestyleUnlockStore : CampaignBehaviorBase
    {
        public static LifestyleUnlockStore Instance { get; private set; }
        
        [SaveableField(1)]
        private List<string> _unlockedFeatures = new List<string>();
        
        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }
        
        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_unlockedFeatures", ref _unlockedFeatures);
            EnsureInitialized();
        }
        
        public void EnsureInitialized()
        {
            if (_unlockedFeatures == null) _unlockedFeatures = new List<string>();
        }
        
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            EnsureInitialized();
        }
        
        public bool IsUnlocked(string featureId)
        {
            EnsureInitialized();
            return _unlockedFeatures.Contains(featureId);
        }
        
        public void Unlock(string featureId)
        {
            EnsureInitialized();
            if (!_unlockedFeatures.Contains(featureId))  // runtime dedup
            {
                _unlockedFeatures.Add(featureId);
            }
        }
    }
}
```

**Verification:**
1. Build clean.
2. Save-load round-trip with 3 features unlocked via Debug Tools — all 3 persist after reload.
3. `IsUnlocked("test1")` returns true after `Unlock("test1")`.
4. Calling `Unlock("test1")` twice doesn't duplicate (list still has 1 entry).

**Footgun:** Using `HashSet<string>` instead of `List<string>` crashes on save (CLAUDE.md issue #14). Verify `List<string>` is used.

---

### T13 — `PersonalKitStore` decision + scaffold

**Goal:** Decide: dedicated `PersonalKitStore` at offset 53 OR extend `QualityStore` with quality slots.

**Decision (Plan 1 v2 RECOMMENDS option B):** Use `QualityStore` slots (`kit.bedroll_level`, `kit.sharpening_stone_level`, `kit.field_kit_level` as 0-3 int qualities). Don't burn offset 53.

**After-state (option B):**
- `PersonalKitStore.cs` deleted
- Offset 53 reclaimed (T2's `AddClassDefinition` line for offset 53 removed from `EnlistedSaveDefiner.cs`)
- New file `src/Features/PersonalKit/PersonalKitTickHandler.cs` — empty hourly/daily tick handler that reads `QualityStore.GetInt("kit.bedroll_level")` etc. and applies bonuses (no-op stub)

**Files:**
- Delete: `src/Features/PersonalKit/PersonalKitStore.cs` (and its `<Compile Include>`)
- Edit: `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` (remove offset 53 registration)
- New: `src/Features/PersonalKit/PersonalKitTickHandler.cs`
- Edit: `Enlisted.csproj` (add `<Compile Include>` for new file)

**Concrete change for `PersonalKitTickHandler.cs`:**

```csharp
namespace Enlisted.Features.PersonalKit
{
    public sealed class PersonalKitTickHandler : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }
        
        public override void SyncData(IDataStore dataStore) { }
        
        private void OnHourlyTick()
        {
            // Plan 1 T13: stub. Plan 7 populates: read QualityStore kit.* qualities, apply bonuses.
            // ModLogger.Expected("PERSONALKIT", "hourly_tick", "stub", ...);  // Disabled to avoid log spam
        }
        
        private void OnDailyTick()
        {
            // Plan 7 populates daily-scope effects (e.g. healing rate boost from Field Kit level).
        }
    }
}
```

**Verification:**
1. Build clean.
2. Verify tick handler subscribes (set a temporary log line, verify it fires hourly).
3. Save-load round-trip clean.

**Footgun:** None — pure scaffolding.

---

### T14 — Validator gates tightened + Phase 18-20 stubs

**Goal:** Confirm existing validator phases run on every commit. Add Phase 18/19/20 stubs ready for Plans 2/3/5 to populate.

**Before-state:** `validate_content.py` runs phases 1-17 (per existing behavior).

**After-state:** Phases 18, 19, 20 added as stubs that pass trivially (no content to validate yet).

**Files:**
- Edit: `Tools/Validation/validate_content.py`
- Edit: `Tools/Validation/lint_repo.ps1` (verify phases 18-20 in run order)

**Concrete change in `validate_content.py`:** Add stubs near the bottom of the existing phase chain:

```python
# Phase 18 — Companion JSON dialog schema validation (stub)
print("[Phase 18] Validating companion dialog schemas...")
# Plan 2 populates: parse companion_*.json, verify schema, check archetype + token prefixes.
# For Plan 1: trivially pass (no content to validate yet).

# Phase 19 — Endeavor catalog validation (stub)
print("[Phase 19] Validating endeavor catalogs...")
# Plan 5 populates: verify endeavor templates have valid skill axes, companion archetype refs,
# phase storylet refs, scrutiny risk values within bounds.
# For Plan 1: trivially pass.

# Phase 20 — Ceremony storylet completeness (stub)
print("[Phase 20] Validating ceremony storylet completeness...")
# Plan 3 populates: verify all 8 tier transitions have at least one ceremony storylet authored.
# For Plan 1: trivially pass.
```

**Verification:**
1. `python Tools/Validation/validate_content.py` — all phases including 18/19/20 print "passing" or equivalent.
2. `Tools/Validation/lint_repo.ps1` — full lint stack passes.

**Footgun:** None — stubs are designed to pass trivially.

---

### T15 — Build + save-load smoke + verification report

**Goal:** Final plan-end verification. Confirm Plan 1 deliverables are all in place; the build remains stable; Plans 2-7 are unblocked.

**Steps:**

1. `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` — clean build, no warnings beyond pre-existing project baseline.
2. `python Tools/Validation/validate_content.py` — all 20 phases pass.
3. `Tools/Validation/lint_repo.ps1` — full lint stack passes.
4. Launch game with mod loaded. Start fresh save (T1 enlistment with any faction).
5. Save → reload → save → reload — round-trip 4x.
6. Inspect native watchdog log at `C:\ProgramData\Mount and Blade II Bannerlord\logs\watchdog_log_<pid>.txt` — must be clean.
7. Inspect mod session log at `Modules\Enlisted\Debugging\Session-A_<date>.log`. Verify:
   - `OnSessionLaunched` fires cleanly for all new behaviors (`PatronRoll`, `LifestyleUnlockStore`, `RankCeremonyBehavior`, `PersonalKitTickHandler`).
   - SyncData round-trip logs no errors or warnings.
   - `OnTierChanged` test fires (use Debug Tools to force `SetTier(currentTier + 1)`); verify `CEREMONY tier_changed` log line appears.
   - `ModalEventBuilder.FireSimpleModal` smoke modal pops up correctly via Debug Tools "Fire test modal" option.
   - Stay-back gate fix verified at T1 with test companion in MainParty (per T6 smoke test recipe).
8. Document the smoke results in `docs/superpowers/plans/2026-04-24-ck3-wanderer-architecture-foundation-verification.md` with:
   - Build output transcript (or summary)
   - Each verification step's result (pass/fail)
   - Any deviations from expected behavior + resolutions
   - Sign-off: Plan 1 ✅ complete, Plans 2-7 unblocked

**Verification:** Verification report committed. CLAUDE.md current-status block updated to reference Plan 1 shipped.

**Footgun:** Skipping the verification report = no durable record of "Plan 1 done" for downstream plans. The report IS the gate.

---

## §7 — Risks (HIGH/MEDIUM/LOW with mitigations)

### Risk H1 — `SaveableTypeDefiner` crashes on module init (HIGH)

**Vector:** T2/T3/T4 add 7 class offsets + 1 enum offset + 1 generic container. Per CLAUDE.md issues #4, #14, #16, vanilla type re-registration crashes module init silently before mod logging.

**Mitigation:**
- Before T2: read `Decompile/TaleWorlds.Core/SaveableCoreTypeDefiner.cs` and `Decompile/TaleWorlds.CampaignSystem/SaveableCampaignTypeDefiner.cs` and cross-check every new class name against vanilla.
- Incremental commits: each save-definer addition gets its own commit; build + save-load round-trip after each commit. If a commit breaks save-load, revert immediately and debug.
- T2 separated from T3 from T4 specifically to allow per-commit verification.

### Risk M1 — Stay-back gate fix surfaces unintended interactions (MEDIUM)

**Vector:** T6 removes a tier gate that has only ever been tested at T7+. Tier-wide enforcement may surface edge cases (e.g. low-tier mods adding companions through other paths).

**Mitigation:**
- T6 smoke test thoroughly per the recipe in §6 T6.
- If issues surface, restore the gate behind a feature flag and address in Plan 2 with playtest data.

### Risk M2 — `ModalEventBuilder` API design (MEDIUM)

**Vector:** Plans 3, 4, 5, 6 all use this helper. Bad signature = rework everywhere.

**Mitigation:**
- T5 implements the recipe verbatim from §4.6 (locked). Don't deviate.
- Code review of T5 before T6 begins.
- If signature change needed in a future plan, add overloads — don't break existing callers.

### Risk M3 — Generic container registration missed (MEDIUM)

**Vector:** `List<PatronEntry>` needs explicit `DefineGenericClassDefinition` (or equivalent). Missing registration = save crash on load when the list has entries.

**Mitigation:**
- T3 explicitly tests save-load with one stub entry in the list.
- T15 final smoke confirms.

### Risk L1 — JSON content not deployed to game install (LOW)

**Vector:** csproj `AfterBuild` entries missing for `companion_*.json`. Loader logs at info level but content silently absent.

**Mitigation:**
- T7 includes the explicit three-add csproj recipe.
- T15 verifies via game-side log inspection.

---

## §8 — Verification gates (must pass before Plan 1 complete)

- [ ] `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` passes
- [ ] `python Tools/Validation/validate_content.py` passes (all 20 phases)
- [ ] `Tools/Validation/lint_repo.ps1` passes
- [ ] Game launches with mod loaded; no crash
- [ ] Fresh save / reload round-trip 4x with no native-watchdog errors
- [ ] All new Campaign behaviors register in `OnSessionLaunched` (verified via mod session log)
- [ ] SyncData round-trip logs clean (no NREs, no missing-field warnings)
- [ ] `ModalEventBuilder.FireSimpleModal` smoke test fires modal correctly (verified via Debug Tools)
- [ ] `OnTierChanged` event hook fires on forced rank-up (verified via Debug Tools)
- [ ] Stay-back gate fix verified at T1 with test companion in MainParty
- [ ] Architecture brief reviewed and committed at `docs/architecture/ck3-wanderer-architecture-brief.md`
- [ ] Companion JSON dialog schema reference committed at `docs/Features/Conversations/companion-dialog-schema.md`
- [ ] AGENTS.md Rule #11 amended (offset cluster expansion 51-60 → 51-70)
- [ ] Verification report committed at `docs/superpowers/plans/2026-04-24-ck3-wanderer-architecture-foundation-verification.md`

---

## §9 — Definition of done

Plan 1 is complete when:

1. All 15 tasks marked ✅ done with their per-task verifications passed.
2. All §8 verification checkboxes pass.
3. Verification report committed.
4. CLAUDE.md current-status block updated to reference Plan 1 shipped.
5. Plans 2-7 can begin parallel implementation against the locked architecture.

**Plans 2-7 cannot begin until Plan 1 is complete.** This is the foundation.

---

## §10 — Hand-off to Plan 2

After Plan 1 ships, Plan 2 (Companion Substrate) inherits the following locked-in contracts:

### Locked save-definer offsets (Plan 2 uses)
- `EnlistmentBehavior.SyncData` extends with companion MBGUID + archetype string fields (no new offset; Plan 2 adds fields to existing behavior)
- `CompanionAssignmentManager` extension shipped (Plan 2 reads `IsAssignedToEndeavor`)

### Locked helpers (Plan 2 uses)
- `ModalEventBuilder` (T5 — Plan 2 doesn't fire modals directly, but Plan 3 does after Plan 2)
- `CompanionDialogueCatalog` (T7 — Plan 2 authors `companion_<archetype>_*.json` against this loader)

### Locked schema rules (Plan 2 enforces)
- JSON dialog schema per §4.4 rule 4
- Token prefix per §4.3 (`companion_<archetype>_*`)
- Namespace per §4.2 (`Enlisted.Features.<each-archetype>`? Or single `Enlisted.Features.Companions`? — Plan 2 brainstorm decides)

### Locked behaviors (Plan 2 builds on)
- Stay-back gate at all tiers (T6)
- `OnTierChanged` event subscription pattern (T11) — Plan 2 may add own subscribers

### Plan 2 starting checklist (executor reads this first)
1. Read this Plan 1 doc end-to-end.
2. Read Plan 1's verification report (must be ✅ complete).
3. Read the architecture brief at `docs/architecture/ck3-wanderer-architecture-brief.md`.
4. Read spec §3.10 (Companion Substrate) and §6.5 (companion combat lifecycle).
5. Read existing `EnlistmentBehavior.GetOrCreateQuartermaster` as the spawn precedent.
6. Begin Plan 2 brainstorm session.

---

## §11 — Out of scope (explicitly NOT in Plan 1)

- Companion spawn recipes (Plan 2)
- Companion JSON dialog content (Plan 2 — Plan 1 ships only stub catalog `companion_test.json`)
- Talk-to sub-menu Camp option (Plan 2)
- Ceremony storylet authoring (Plan 3 — Plan 1 ships `RankCeremonyBehavior` stub that logs only)
- Officer equipment registrations + healing model patches (Plan 4)
- Endeavor catalog content + EndeavorPhaseProvider implementation (Plan 5 — Plan 1 ships only the empty Activity subclass and the `IsAssignedToEndeavor` flag scaffolding)
- Patron favor outcomes + audience flow extension (Plan 6 — Plan 1 ships only the empty `PatronRoll` + `PatronEntry` shells)
- Personal Kit catalog content + tick-handler bonuses (Plan 7 — Plan 1 ships only the `PersonalKitTickHandler` stub)
- Lifestyle unlock catalog content + milestone hooks (Plan 7)
- Smoke testing beyond architecture-substrate verification (Plan 7)
- Numeric tuning passes (Plan 7)
- News-v2 typed-dispatch substrate (separate from this spec; menu+duty unification design)
- Lifestyle Perks (PerkObject path — explicitly excluded per spec §3.6 + §4.5 "do not" list)
- OrderActivity migration (siblings not merge — §4.5 "do not" list)

---

## §12 — References

- [CK3 Wanderer Mechanics Systems Analysis (v6)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md) — design source
- [AGENTS.md](../../../AGENTS.md) — universal rules
- [CLAUDE.md](../../../CLAUDE.md) — Claude-specific session guidance + known footguns
- Existing `EnlistedSaveDefiner.cs` — current offset registrations
- Existing `EnlistedFormationAssignmentBehavior.cs:188-220` — stay-back enforcement
- Existing `CompanionAssignmentManager.cs` — Fight/Stay-Back toggle (extension pattern)
- Existing `HomeEveningMenuProvider.cs:37` — modal pipeline precedent
- Existing `StoryletEventAdapter.cs:56` — BuildModal
- Existing `StoryDirector.cs:61, 213` — EmitCandidate + Route
- Existing `EventDeliveryManager.cs:93, 209` — QueueEvent + ShowEventPopup
- Existing `QMDialogueCatalog.cs` — JSON dialog schema precedent
- Existing `EnlistmentBehavior.GetOrCreateQuartermaster` — hero spawn precedent (Plan 2 will use)
