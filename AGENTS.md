# Enlisted - Bannerlord Mod

C# mod transforming Mount & Blade II: Bannerlord into a soldier career simulator. Player enlists with a lord, follows orders, earns wages, progresses through 9 ranks. 245+ narrative content pieces, data-driven via JSON + XML.

This file is the shared source of truth for AI coding agents (Claude Code, Codex, Cursor, Copilot, Aider, etc.). Tool-specific extras live alongside: `CLAUDE.md` imports this file.

---

## Quick Commands

```bash
# Build — produces Enlisted.dll in BOTH Win64_Shipping_Client/ and
# Win64_Shipping_wEditor/ via csproj post-build mirror. Close BannerlordLauncher
# first — it holds the DLL open and fails the copy with MSB3021.
dotnet build -c "Enlisted RETAIL" /p:Platform=x64

# Validate content (ALWAYS before commit)
python Tools/Validation/validate_content.py

# Sync localization strings
python Tools/Validation/sync_event_strings.py

# Run repo lint stack (.editorconfig + content validators + Ruff + PSScriptAnalyzer)
./Tools/Validation/lint_repo.ps1

# Upload to Steam Workshop
./Tools/Steam/upload.ps1

# Regenerate ../Decompile/ from your local Bannerlord install (first-time setup)
./Tools/Decompile-Bannerlord.bat
```

---

## Critical Rules (Will Break Mod)

### 1. Verify every TaleWorlds API against `../Decompile/`

- The decompile is the only authoritative reference for the Bannerlord API surface the user has installed. It's a sibling of the repo root, external to git, regenerated via `Tools/Decompile-Bannerlord.bat` from the local install.
- NEVER use online docs, Context7, or training knowledge for TaleWorlds APIs — they drift across patches.
- If the user upgrades Bannerlord, regenerate the decompile before verifying APIs for new work.

### 2. New C# Files Must Be Registered in .csproj

```xml
<Compile Include="src\Features\MyFeature\MyNewClass.cs"/>
```

### 3. Gold Transactions — use GiveGoldAction

```csharp
// CORRECT - visible in UI
GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount);
// WRONG - not visible, breaks UI feedback
Hero.MainHero.ChangeHeroGold(amount);
```

### 4. Equipment Iteration — numeric loop only

```csharp
// CORRECT
for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
// WRONG - crashes (Enum.GetValues includes count values)
foreach (EquipmentIndex slot in Enum.GetValues(typeof(EquipmentIndex)))
```

### 5. Hero Safety — null-safe, IsAlive checks

```csharp
var hero = CampaignSafetyGuard.SafeMainHero;
if (hero == null) return;
if (hero.IsAlive) VisualTrackerManager.RegisterObject(hero);
```

### 6. JSON Field Order — fallback immediately after ID

```json
{ "titleId": "key", "title": "Fallback", "setupId": "key2", "setup": "Text" }
```

### 7. Event Tooltips Required

All options need tooltips (<80 chars). Format: action + effects + cooldown.

### 8. Save System Registration

In `EnlistedSaveDefiner` — missing = "Cannot Create Save" error:

```csharp
DefineEnumType(typeof(MyNewEnum));
DefineClassType(typeof(MyNewClass));
```

Persist in-progress flags in `SyncData()` too — otherwise state is lost on reload.

### 9. Windows/WSL Portability

This repo is developed on both Windows and Linux (WSL). For C# code that builds paths:

```csharp
// CORRECT - cross-platform
Path.Combine(basePath, "Prompts", "order_prompts.json");
ModulePaths.GetContentPath("Prompts"); // preferred for mod content

// WRONG - breaks on Linux
basePath + "\\Prompts\\order_prompts.json"
```

Line endings are enforced by `.gitattributes` (`.cs` / `.csproj` / `.sln` / `.ps1` = CRLF; everything else `text=auto`). Don't override locally.

### 10. Event Delivery — route through StoryDirector

Modal events go through `StoryDirector.EmitCandidate(...)`, not `EventDeliveryManager.Instance.QueueEvent(...)` directly. The Director gates Modal firing with a 5-day in-game floor + 60s wall-clock floor + per-category cooldown, and writes non-Modal items to the news feed as accordion entries.

```csharp
// CORRECT - gated by pacing, supports deferral + accordion routing
StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "myfeature.context",
    CategoryId = "myfeature.subcategory",
    ProposedTier = StoryTier.Modal,
    SeverityHint = 0.5f,
    Beats = { StoryBeat.OrderPhaseTransition },
    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
    EmittedAt = CampaignTime.Now,
    InteractiveEvent = evt,
    RenderedTitle = evt.TitleFallback,
    RenderedBody = evt.SetupFallback,
    StoryKey = evt.Id
});
// Fallback for when Director isn't registered yet (early boot only):
// EventDeliveryManager.Instance?.QueueEvent(evt);
```

Use `ChainContinuation = true` for player-opted continuations (promotions, bag checks, chain events) so the in-game floor + category cooldown don't defer them (60s wall-clock still applies). Spec: [docs/superpowers/specs/2026-04-18-event-pacing-design.md](docs/superpowers/specs/2026-04-18-event-pacing-design.md).

### 11. Content authoring — route through the storylet backbone

Content is authored as **storylets** (`ModuleData/Enlisted/Storylets/*.json`), not as legacy `EventDefinition` JSON. State lives in `QualityStore` (typed numeric, global or per-hero) and `FlagStore` (named booleans with expiry). Durable player engagements are `Activity` subclasses with phases and intent-biased storylet pools. Effects are either named scripted effects from `ModuleData/Enlisted/Effects/scripted_effects.json` (preferred — the seed catalog has 22 entries) or registered primitives (`quality_add`, `set_flag`, `give_gold`, etc.). Triggers are named C# predicates resolved by `TriggerRegistry`. Reference: [docs/Features/Content/storylet-backbone.md](docs/Features/Content/storylet-backbone.md) (living doc — seed catalogs, trigger/slot/primitive lists, save-definer offsets, pitfalls).

**Save-definer offset convention.** Spec 0 owns class offsets 40-44 and enum offsets 82-83 in `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`. Class offsets **45-60** are reserved for concrete `Activity` subclasses from surface specs (Home / Orders / Land-Sea / Promotion+Muster / Quartermaster) AND closely-related surface-spec persistent state that surface specs own (snapshots, accessors, compact POCOs serving as sources of truth). Intelligence backbone's `EnlistedLordIntelligenceSnapshot` holds offset 48 under this broadening; Spec 2's `OrderActivity` + `NamedOrderState` hold 46/47. Grep the definer before claiming an offset — collisions corrupt saves silently. Offsets 10-14 were held by the legacy Orders subsystem (retired 2026-04-21, commit `a8719bb`) and remain reserved; do not reuse without audit.

---

## Code Standards

- Braces required on all control statements (no single-line `if`)
- Error reporting uses three severity-specific `ModLogger` methods:
  - `ModLogger.Surfaced(category, summary, ex, ctx?)` — player/dev-visible failure. Emits a red on-screen toast once per session per code, writes full context to the session log, appears in the registry. Use when the player or developer needs to see the failure.
  - `ModLogger.Caught(category, summary, ex, ctx?)` — defensive catch for Harmony patches, cleanup, refresh. Logs only, no toast, not in the registry. Throttled per (category, file, line) on a 60s window.
  - `ModLogger.Expected(category, key, summary, ctx?)` — known-branch early exit ("not eligible", "no faction"). Info level, no stack trace, no toast. Throttled by `key` on a 60s window.

  Codes are auto-generated from the summary string — you never pick a number. Category argument MUST be a string literal matching `[A-Z][A-Z0-9\-]*` (required by the registry scanner). Summary MUST be a string literal too. The registry at [docs/error-codes.md](docs/error-codes.md) is regenerated by `Tools/Validation/generate_error_codes.py` and verified by `validate_content.py` Phase 10. Phase 11 separately fails the build if any `ModLogger.Error(...)` call appears in `src/` (the method was retired 2026-04-19; use `Surfaced` / `Caught` / `Expected` instead).
- Localized strings: `new TextObject("{=id}Fallback")`
- Private fields: `_camelCase`
- Comments describe current behavior — never changelogs, PR references, or "added for X"

### Safe Patterns

```csharp
// Deferred menu activation
NextFrameDispatcher.RunNextFrame(() => GameMenu.ActivateGameMenu("menu_id"));
// Item comparison by StringId (not reference equality)
if (element.Item.StringId == targetItem.StringId)
// Settlement safety check
if (!PlayerEncounter.InsideSettlement) PlayerEncounter.Finish();
// Centralized manager for reputation/needs changes
EscalationManager.Instance.ModifyReputation(ReputationType.Soldier, 5, "reason");
```

---

## AI Maintainability Priorities

Repo-local rules win over generic style defaults. Run the lint stack — it
encodes everything below that's machine-checkable.

Lint config — single source of truth per language:

- `.editorconfig` — C# / JSON / XML formatting and Roslyn diagnostics
- `ruff.toml` — `Tools/**/*.py`
- `PSScriptAnalyzerSettings.psd1` — `Tools/**/*.ps1`
- `Tools/Validation/lint_repo.ps1` — runs the full stack end-to-end

Repo-specific rules generic AI defaults won't catch:

- **Don't reformat unrelated lines** just because a different style is valid.
  Match surrounding code; let `.editorconfig` drive.
- **Don't invent a new manager / store / catalog / behavior / runtime** without
  checking whether one already exists for that responsibility. Grep first.
- **Don't bundle refactor + behavior change + content migration in one patch**
  unless the task explicitly requires that bundle. Smallest stable surface
  first.
- **When a change crosses C# + content boundaries** (loader + JSON, validator
  + schema), update both sides together — never leave authored data ahead of
  the code that consumes it.
- **Use `nameof(...)`** for member/type names — not for player-facing text,
  localization fallbacks, or authored content IDs.
- **Route player/dev-visible failures through `ModLogger`** (Surfaced /
  Caught / Expected — see Code Standards above). Catch only what you can
  actually handle.

---

## Project Structure

```
src/Features/          C# gameplay features
ModuleData/Enlisted/   JSON events, orders, decisions
ModuleData/Languages/  enlisted_strings.xml (localization)
docs/                  All documentation (see docs/INDEX.md)
Tools/Validation/      Validators (run before commit)
../Decompile/          Bannerlord API reference — AUTHORITATIVE (external to repo, regenerate from local install with Tools/Decompile-Bannerlord.bat)
```

### Key Feature Folders

- `Enlistment/` — Service state, retirement
- `Orders/` — Mission directives
- `Content/` — Events, decisions, narrative, StoryDirector (pacing gate), storylets + triggers + effects + slots + scripted effects (Spec 0 backbone)
- `Qualities/` — Typed numeric state (scrutiny, supplies, readiness, loyalty, lord_relation, rank_xp). Stored + read-through kinds. Single home; decays on daily tick
- `Flags/` — Named boolean state, global + hero-scoped, with expiry
- `Activities/` — Stateful ticking activities with intent-biased phase pools; Banner-Kings Feast pattern; ActivityRuntime hosts
- `Escalation/` — Reputation, scrutiny/discipline
- `Company/` — Readiness, supply needs
- `Equipment/` — Quartermaster, gear

---

## Pre-Commit Checklist

- [ ] APIs verified against `../Decompile/` (regenerate if the install has been patched since last run)
- [ ] New C# files added to `Enlisted.csproj`
- [ ] JSON field order correct (fallback after ID)
- [ ] Tooltips on all event options (<80 chars)
- [ ] `python Tools/Validation/validate_content.py` passes
- [ ] `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds

---

## Key Documentation

Link, don't duplicate — open these for depth:

| Topic | File |
| :--- | :--- |
| Architecture & standards | [docs/BLUEPRINT.md](docs/BLUEPRINT.md) |
| Master documentation catalog | [docs/INDEX.md](docs/INDEX.md) |
| Build setup, developer onboarding | [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) |
| Logging, saves, dialogue, menu patterns | [Tools/TECHNICAL-REFERENCE.md](Tools/TECHNICAL-REFERENCE.md) |
| Validation tool reference | [Tools/README.md](Tools/README.md) |
| Writing style (voice, tone) | [docs/Features/Content/writing-style-guide.md](docs/Features/Content/writing-style-guide.md) |
| Error code registry (auto-generated) | [docs/error-codes.md](docs/error-codes.md) |
| Storylet backbone (content layer, Spec 0) | [docs/Features/Content/storylet-backbone.md](docs/Features/Content/storylet-backbone.md) — living reference |
| Event pacing (delivery layer) | [docs/superpowers/specs/2026-04-18-event-pacing-design.md](docs/superpowers/specs/2026-04-18-event-pacing-design.md) |

---

## Common Pitfalls

1. `ChangeHeroGold` instead of `GiveGoldAction`
2. `Enum.GetValues` for equipment iteration
3. Tracking a hero without checking `IsAlive`
4. `PlayerEncounter.Finish()` while inside a settlement
5. Forgetting to add new files to `.csproj`
6. Missing tooltips on event options
7. Wrong JSON field order (ID and fallback not adjacent)
8. Not persisting in-progress flags in `SyncData()`
9. Missing `SaveableTypeDefiner` registration
10. Relying on external API docs (wrong version)
11. Creating mod-spawned heroes (QM, etc.) with `Occupation.Wanderer` triggers
    vanilla wanderer-introduction dialogue. Use `Occupation.Soldier`. Verified
    in the developer-local decompile (sibling of repo root — see §1):
    `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs:607`
    (`AddWandererConversations`) and `:1274` (`conversation_wanderer_on_condition`,
    checks `Occupation == Occupation.Wanderer`).
12. Calling `EventDeliveryManager.Instance.QueueEvent(evt)` directly bypasses
    StoryDirector pacing (no floor, no cooldown, no deferral). The only
    legitimate direct-call sites are (a) Director-null fallbacks inside a
    migrated caller, (b) the debug tool at `src/Debugging/Behaviors/DebugToolsBehavior.cs:141`,
    and (c) the Director's own internal `Route()`. Everything else must use
    `StoryDirector.Instance?.EmitCandidate(...)` — see Critical Rule #10.
13. Writing to a read-only quality (`rank`, `days_in_rank`, `days_enlisted`)
    from a storylet effect. `validate_content.py` Phase 12 blocks this at
    build time. Rank advancement routes through `EnlistmentBehavior.SetTier`
    as the outcome of a promotion-ceremony chain, not a raw quality write.
14. Inventing a new scripted-effect id without adding it to
    `ModuleData/Enlisted/Effects/scripted_effects.json`. Phase 12 blocks
    unknown `apply` values. Prefer reusing the seed catalog (`rank_xp_minor`,
    `lord_relation_up_*`, `scrutiny_down_*`, etc.) over minting one-off names.
15. Claiming a `SaveableTypeDefiner` offset without grepping
    `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` first. Offsets 40-44
    (classes) and 82-83 (enums) are Spec 0. Offsets 45-60 are reserved for
    concrete `Activity` subclasses AND closely-related surface-spec
    persistent state (e.g. Intelligence snapshot at 48) — see the
    save-definer offset convention under "Content authoring — route through
    the storylet backbone" above. Offsets 10-14 are reserved (legacy Orders
    subsystem retired 2026-04-21); do not reuse without audit.
16. Authoring a scripted effect (in `ModuleData/Enlisted/Effects/scripted_effects.json`)
    whose body references another scripted effect that eventually references it
    back. `EffectExecutor` caps expansion at depth 8 and logs
    `Expected("EFFECT", "scripted_depth_limit", ...)` — the chain no-ops at the
    cap, but a cyclic catalog is a JSON bug worth surfacing in the session log.
17. `Campaign.Current.CampaignBehaviorManager` returns an empty collection at
    `OnGameStart` — behaviors have been registered via `AddBehavior` but not yet
    published to the Campaign. Read `campaignStarter.CampaignBehaviors` instead
    (decompile: `TaleWorlds.CampaignSystem/CampaignGameStarter.cs:19` — the
    starter holds the list directly). A diagnostic that iterates
    `Campaign.Current.GetCampaignBehaviors<T>()` at OnGameStart NREs in
    `Enumerable.OfType` or prints zero behaviors, depending on the call shape.
18. Content catalogs (Storylet, Event, Decision, ActivityType, Scripted
    Effects) initialize AFTER `OnGameStart`, during their owning behaviors'
    `OnSessionLaunchedEvent` handlers. A diagnostic that reads catalog counts
    at OnGameStart reports `0 loaded` even when load ultimately succeeds.
    Defer diagnostic reads to `OnSessionLaunchedEvent` (see
    `RuntimeCatalogStatusMarkerBehavior` for the pattern).
19. `int.MinValue` as a "never fired" sentinel for throttle fields overflows
    when subtracted: `nowHour - int.MinValue` goes negative and trips
    `diff >= interval` checks backwards, so either the throttle never gates
    OR it always gates, depending on comparison direction. Use
    `int.MinValue / 2` as the sentinel — gives plenty of headroom and keeps
    arithmetic well-defined. Known-bitten fields: `_lastHeartbeatHourTick`,
    `_lastTransitionBeatHourTick`, `_lastHourlyHeartbeatTick` in the
    Orders-surface tick behaviors.
20. `ModLogger.Surfaced` / `Caught` / `Expected` require the category AND
    summary arguments to be string literals at the call site — interpolated
    strings (`$"..."`) are rejected by `generate_error_codes.py`'s scanner
    (regex-based, doesn't evaluate interpolation). If you need dynamic values
    in the log entry, put them in the `ctx` anonymous object instead of the
    summary. Same rule as the existing shared-const pitfall in CLAUDE.md's
    project-conventions section — this is the interpolation variant.
21. `OrdersNewsFeedThrottle.TryClaim()` is designed to reject when
    `Campaign.Current.TimeControlMode == SpeedUpMultiplier` with the multiplier
    above 4x — news feed entries at extreme fast-forward would flood. This is
    intentional silence, not a bug. When smoke-testing the Orders surface, run
    at 1x–4x to see news output; tick-driven `ModLogger` entries (DRIFT,
    DUTYPROFILE heartbeats, PATH heartbeats) still log at any speed.
22. Plans in `docs/superpowers/plans/` can drift from the implementation
    before the plan is fully executed: task scope shifts, architectures are
    refactored, named files get renamed or deleted. Spec 2 saw this repeatedly
    (Tasks 19/20/21 all required audit-expansion of their consumer-site lists
    during execution; `ContentOrchestrator` was listed with zero migration
    sites). Before implementing a multi-file task from an older plan, grep the
    codebase for the plan's prescribed file paths and symbol names and confirm
    they still exist and still have the cardinality the plan assumed.

Full pitfalls list with solutions: [docs/BLUEPRINT.md](docs/BLUEPRINT.md).

---

## Deprecated Systems

- **Morale System** — Removed 2026-01-11, save-load only
- **Company Rest** — Removed 2026-01-11, save-load only
- Player Fatigue (0-24 budget) remains functional

---

## Diagnostic Logs

When debugging runtime issues, check both log sources:

- **Native Bannerlord logs** — `C:\ProgramData\Mount and Blade II Bannerlord\`
  (engine crashes, save errors, low-level TaleWorlds output)
- **Enlisted mod logs** — `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\`
  (`ModLogger` output, session logs, conflict reports, validation reports)

Details on log categories, session naming (`Session-A_*.log`), and error-code conventions live in [Tools/TECHNICAL-REFERENCE.md](Tools/TECHNICAL-REFERENCE.md).

---

## External Resources

- Steam Workshop: <https://steamcommunity.com/sharedfiles/filedetails/?id=3621116083>
- Requires: Harmony for Bannerlord

---

**When in doubt, check `Decompile/`. Never hallucinate APIs.**
