# Enlisted - Project Blueprint

**Bannerlord v1.3.13 soldier career mod** | **Last Updated:** 2026-04-19 (Storylet Backbone Spec 0 shipped — Qualities/Flags/Activities/Storylets runtime + seed catalogs + validate_content.py Phase 12.)

---

## 🧠 REQUIRED: Technical Standards

**Before making ANY code changes, you MUST:**

1. **Target Bannerlord v1.3.13** — Never assume APIs from later versions exist
2. **Verify all APIs** against `Decompile/` in workspace root (NEVER use online docs)
3. **Add new C# files** to `Enlisted.csproj` manually via `<Compile Include="..."/>` entries
4. **Use ModLogger** for all logging — see [Tools/TECHNICAL-REFERENCE.md](../Tools/TECHNICAL-REFERENCE.md) for the three-tier API (`Surfaced` / `Caught` / `Expected`). Error codes auto-generate into [docs/error-codes.md](error-codes.md); don't hand-edit.
5. **Never suppress ReSharper or repo lint warnings** without documented justification
6. **Run the repo lint stack** before committing: `.\Tools\Validation\lint_repo.ps1`
7. **Check if features exist** by searching codebase first — never hallucinate

---

## 📋 TASK LOOKUP - Read the right doc for your task

| Task | READ THIS FILE | Key Info |
| --- | --- | --- |
| **Runtime debug logs** | `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging` | ModLogger output, error codes, save diagnostics |
| **Steam Workshop upload** | `Tools/Steam/WORKSHOP_UPLOAD.md` | VDF char limits, run `.\Tools\Steam\upload.ps1` in interactive PS |
| **Add/edit events/orders** | `docs/Features/Content/writing-style-guide.md` | Voice, tone, JSON schema |
| **Understand a feature** | `docs/Features/Core/enlistment.md` or relevant feature doc | Check docs/INDEX.md for full catalog |
| **Code quality issues** | `.editorconfig`, `ruff.toml`, `PSScriptAnalyzerSettings.psd1`, `Tools/Validation/lint_repo.ps1` | ReSharper settings in .sln.DotSettings |
| **Validation errors** | `Tools/README.md` | Run `.\Tools\Validation\lint_repo.ps1` for full repo checks or `python Tools/Validation/validate_content.py` for content-only checks |
| **API questions** | Local decompile at `C:\Dev\Enlisted\Decompile\` | v1.3.13 specific, don't use online docs |
| **Opportunity/orchestration** | `docs/ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md` | Commitment model, phase scheduling |

## ⚡ QUICK COMMANDS

```powershell
dotnet build -c "Enlisted RETAIL" /p:Platform=x64     # Build mod
.\Tools\Validation\lint_repo.ps1                      # Full repo lint stack
python Tools/Validation/validate_content.py           # Validate content
.\Tools\Steam\upload.ps1                              # Upload to Workshop (interactive PS only!)
python Tools/Validation/sync_event_strings.py         # Sync localization
```

## 🚨 CRITICAL RULES

1. **Target Version:** Bannerlord v1.3.13 (not latest)
2. **API Verification:** Use `Decompile/` in workspace root (never online docs)
3. **New C# Files:** Manually add to `Enlisted.csproj` → run validator
4. **Logging:** Use `ModLogger` — see [Tools/TECHNICAL-REFERENCE.md](../Tools/TECHNICAL-REFERENCE.md) for the current API
5. **Opportunity Model:** Each opportunity once/day, commitment = click future to schedule, click current to fire
6. **Code Quality:** Follow ReSharper and the repo lint stack, don't suppress without reason

## 📁 KEY PATHS

| Path | Purpose |
| --- | --- |
| `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging` | **Runtime mod logs** (ModLogger output, error codes) |
| `src/Features/` | All gameplay code (Enlistment, Orders, Content, Combat, Equipment, Qualities, Flags, Activities) |
| `src/Features/Qualities/` · `Flags/` · `Activities/` | Storylet Backbone runtime (Spec 0, 2026-04-19) — QualityStore (typed numeric state, stored + read-through), FlagStore (named booleans + expiry), ActivityRuntime (stateful phased activities) |
| `ModuleData/Enlisted/` | JSON config, events, orders, decisions |
| `ModuleData/Enlisted/Storylets/` · `Qualities/` · `Effects/` | Storylet Backbone content dirs — storylet definitions (populated by surface specs 1-5), quality_defs.json, scripted_effects.json |
| `ModuleData/Languages/enlisted_strings.xml` | All localized strings |
| `Tools/Steam/` | Workshop upload scripts and VDF |
| `Tools/Validation/` | Content validators |
| `Tools/Debugging/` | Validation reports and analysis scripts |
| `docs/` | All documentation |
| `C:\Dev\Enlisted\Decompile\` | Native API reference (v1.3.13) |

## 📚 DOCUMENTATION INDEX

| Topic | File |
| --- | --- |
| Full doc catalog | [docs/INDEX.md](INDEX.md) |
| Core gameplay | [docs/Features/Core/core-gameplay.md](Features/Core/core-gameplay.md) |
| Enlistment system | [docs/Features/Core/enlistment.md](Features/Core/enlistment.md) |
| All content files | [docs/Features/Content/content-index.md](Features/Content/content-index.md) |
| Writing style | [docs/Features/Content/writing-style-guide.md](Features/Content/writing-style-guide.md) |
| JSON schemas | [docs/Features/Content/event-system-schemas.md](Features/Content/event-system-schemas.md) |
| Orchestrator | [docs/ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md](ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md) |
| Steam upload | [Tools/Steam/WORKSHOP_UPLOAD.md](../Tools/Steam/WORKSHOP_UPLOAD.md) |
| Tooling guide | [Tools/README.md](../Tools/README.md) |
| Technical patterns | [Tools/TECHNICAL-REFERENCE.md](../Tools/TECHNICAL-REFERENCE.md) |

---

## Directory Structure

```text
Enlisted/
├── src/                    C# source code
│   ├── Mod.Entry/          SubModule + Harmony init
│   ├── Mod.Core/           Logging, config, save system, helpers
│   ├── Mod.GameAdapters/   Harmony patches
│   └── Features/           All gameplay features
│       ├── Enlistment/     Core service state, retirement
│       ├── MyNewFeature/   (example placeholder)
│       ├── Orders/         Mission-driven directives
│       ├── Content/        Events, Decisions, narrative delivery
│       ├── Combat/         Battle participation, formation, Battle AI
│       ├── Equipment/      Quartermaster and gear management
│       └── ...             (see full list below)
├── ModuleData/             Game data files
│   ├── Enlisted/           JSON config, events, orders, decisions
│   └── Languages/          XML localization (enlisted_strings.xml)
│
├── Tools/                  Development utilities
│   ├── Validation/         Content + project structure validators
│   ├── Debugging/          Reports and debug scripts (safe to delete)
│   ├── Steam/              Workshop upload scripts
│   └── Research/           Native extraction, analysis utilities
│
├── docs/                   All documentation
│   ├── Features/           Feature specifications by category
│   ├── Reference/          API research and analysis
│   └── INDEX.md            Master documentation catalog
│
├── GUI/                    Gauntlet UI prefabs
│
└── Decompile/              Bannerlord v1.3.13 decompiled source (API reference)
```

### Feature Folders (src/Features/)

| Folder | Purpose |
| --- | --- |
| Enlistment | Core service state, retirement |
| Orders | Mission-driven directives (Chain of Command) |
| Content | Events, Decisions, narrative delivery |
| Identity | Role detection (Traits), Reputation helpers |
| Escalation | Lord/Officer/Soldier reputation, Scrutiny/Discipline |
| Company | Company-wide Needs (Readiness, Supply) - Note: Rest removed 2026-01-11 |
| Context | Army context and objective analysis |
| Interface | Camp Hub menu, News/Reports |
| Equipment | Quartermaster and gear management |
| Ranks | Promotions and culture-specific titles |
| Conversations | Dialog management |
| Combat | Battle participation, formation assignment |
| Conditions | Player medical status (injury/illness) |
| Retinue | Commander's Retinue (T7+), Service Records |
| Camp | Camp activities and rest logic |

---

## Quick Commands

```powershell
# Build the mod
dotnet build -c "Enlisted RETAIL" /p:Platform=x64

# Validate content and project structure (run before committing)
python Tools/Validation/validate_content.py

# Get prioritized issue summary
python Tools/Validation/validate_content.py > Tools/Debugging/validation_report.txt
python Tools/Validation/analyze_validation.py

# Sync localization strings
python Tools/Validation/sync_event_strings.py

# Upload to Steam Workshop
.\Tools\Steam\upload.ps1
```

---

## Common Tasks

### Add a New C# File

1. Create file in appropriate `src/Features/` subfolder
2. **Manually add to `Enlisted.csproj`:**

   ```xml

   <Compile Include="src\Features\MyFeature\MyNewClass.cs"/>

   ```

3. Run validation to verify: `python Tools/Validation/validate_content.py`
4. Build to confirm compilation

### Add New Event/Decision/Order

1. **Read the [Writing Style Guide](Features/Content/writing-style-guide.md)** first for voice/tone/vocabulary
2. Add JSON definition to `ModuleData/Enlisted/Events/` or `Decisions/`
3. Add XML localization entries to `ModuleData/Languages/enlisted_strings.xml`
   - Use `&#xA;` for newlines in XML attributes
   - Escape: `&` → `&amp;`, `'` → `&apos;`, `"` → `&quot;`
4. Use placeholder variables (e.g., `{PLAYER_NAME}`, `{SERGEANT}`, `{LORD_NAME}`)
5. **For opportunities:** Add `hintId`/`hint` fields for Daily Brief foreshadowing (see [Opportunity Hints](Features/Content/writing-style-guide.md#opportunity-hints))
6. Run validation: `python Tools/Validation/validate_content.py`
7. Sync strings: `python Tools/Validation/sync_event_strings.py --check`
8. Update [content-index.md](Features/Content/content-index.md) if adding new content type

### Check If Feature Exists

1. Search [INDEX.md](INDEX.md) for topic
2. Check [core-gameplay.md](Features/Core/core-gameplay.md) for mentions
3. Search `src/` for implementation

### Before Committing

```powershell
# 1. Validate content
python Tools/Validation/validate_content.py

# 2. Build
dotnet build -c "Enlisted RETAIL" /p:Platform=x64

# 3. Fix any errors/warnings
```

---

## Code Standards

### Configuration Files (READ THESE)

**Before making code changes, read these files to understand our quality standards:**

1. **[.editorconfig](../.editorconfig)** - Formatting rules, naming conventions, indentation
   - Enforces 4-space indentation for C#, 2-space for JSON/XML
   - **Warns on unused `using` directives** (IDE0005, CS8019)
   - **Warns on redundant namespace qualifiers** (IDE0001, IDE0002)
   - **Warns on unnecessary suppressions** (IDE0079)
   - Naming conventions: private fields use `_camelCase`

2. **[Enlisted.sln.DotSettings](../Enlisted.sln.DotSettings)** - ReSharper inspection settings and suppressions
   - Disables markdown linting (docs are excluded from code analysis)

3. **[ruff.toml](../ruff.toml)** - Python lint/format configuration for `Tools/**/*.py`
   - Enforces imports, common errors, bug-prone patterns, safe upgrades, and simplifications
   - Excludes ad-hoc `Tools/Research/`

4. **[PSScriptAnalyzerSettings.psd1](../PSScriptAnalyzerSettings.psd1)** - PowerShell script analysis configuration
   - Enforces approved verbs, comment help, consistent indentation/whitespace, and common scripting pitfalls

5. **[Tools/Validation/lint_repo.ps1](../Tools/Validation/lint_repo.ps1)** - Unified lint entry point
   - Runs `.editorconfig`-backed C# checks via `dotnet format`
   - Runs content validation, Ruff, and PSScriptAnalyzer in one command

### General Rules

- **Braces required** on all `if`, `for`, `while`, `foreach` (even single-line)
- **No unused code** - remove unused imports, variables, methods
- **Comments describe current behavior** (no "Phase X added..." framing)
- **Follow ReSharper and repo lint output** - fix warnings, don't suppress without reason

### JSON Content Rules

**1. Fallback fields must immediately follow their ID fields:**

```json
✅ Correct:
{
  "titleId": "event_title_key",
  "title": "Fallback Title",
  "setupId": "event_setup_key",
  "setup": "Fallback setup text..."
}

❌ Wrong:
{
  "titleId": "event_title_key",
  "setupId": "event_setup_key",
  "title": "Fallback Title",
  "setup": "Fallback setup text..."
}
```

**2. Always include fallback text** - never empty strings

**3. Tooltips cannot be null** - every option must have a tooltip

### Tooltip Guidelines

- One sentence, under 80 characters
- Factual description of what happens
- Format: action + side effects + cooldown

```json
{"tooltip": "Trains equipped weapon. Causes fatigue. 3 day cooldown."}
{"tooltip": "Harsh welcome. +5 Officer rep. -3 Retinue Loyalty."}
{"tooltip": "Accept discharge. 90-day re-enlistment block applies."}
```

### Comment Style

```csharp
// ✅ Good: Describes current behavior
// Checks if the player can re-enlist based on cooldown and discharge history.
private bool CanReenlistWithFaction(Kingdom faction)

// ❌ Bad: Changelog framing
// Phase 2: Added re-enlistment check. Changed from using days to CampaignTime.
private bool CanReenlistWithFaction(Kingdom faction)
```

---

## API Verification

### API Key Assemblies

| Assembly | Contains |
| --- | --- |
| `TaleWorlds.CampaignSystem` | Party, Settlement, Campaign behaviors |
| `TaleWorlds.Core` | CharacterObject, ItemObject |
| `TaleWorlds.Library` | Vec2, MBList, utility classes |
| `TaleWorlds.MountAndBlade` | Mission, Agent, combat |
| `SandBox.View` | Menu views, map handlers |

**Process:**

1. Check decompile for actual API (authoritative)
2. Use official docs only for quick lookups (secondary)
3. Don't rely on external docs or assumptions
4. Verify method signatures, property types, enum values

---

## Code Review Checklist

### Code Quality

- [ ] Read [.editorconfig](../.editorconfig), [ruff.toml](../ruff.toml), [PSScriptAnalyzerSettings.psd1](../PSScriptAnalyzerSettings.psd1), [Enlisted.sln.DotSettings](../Enlisted.sln.DotSettings)
- [ ] All ReSharper/Rider warnings addressed
- [ ] No unused imports, variables, or methods
- [ ] Braces used for all control statements
- [ ] No redundant namespace qualifiers

### Functionality

- [ ] Code builds without errors
- [ ] Logging added for significant actions/errors
- [ ] Null checks where needed

### Documentation

- [ ] Comments describe current behavior
- [ ] XML localization strings added for new events
- [ ] Tooltips provided for all event options
- [ ] New files added to `Enlisted.csproj`

### Data Files & Project Structure

- [ ] JSON fallback fields immediately follow ID fields
- [ ] Order events include skillXp in effects
- [ ] New C# files added to .csproj (validator will catch this)
- [ ] No rogue files in root directory
- [ ] Repo lint passes: `.\Tools\Validation\lint_repo.ps1`

---

## Common Pitfalls

These mistakes cause real problems. Avoid them.

### 1. Using `ChangeHeroGold` Instead of `GiveGoldAction`

**Problem:** `ChangeHeroGold` modifies internal gold not visible in party UI

**Solution:** Use `GiveGoldAction.ApplyBetweenCharacters()`

### 2. Iterating Equipment with `Enum.GetValues`

**Problem:** Includes invalid count enum values, causes crashes

**Solution:** Use numeric loop to `NumEquipmentSetSlots`

### 3. Modifying Reputation/Needs Directly

**Problem:** Bypasses clamping and logging

**Solution:** Always use managers (EscalationManager, CompanyNeedsManager)

### 4. Not Adding New Files to .csproj

**Problem:** New .cs files exist but won't compile

**Solution:** Manually add `<Compile Include="..."/>` entries (validator will detect missing files)

### 5. Relying on External API Documentation

**Problem:** Outdated or incorrect API references

**Solution:** Always verify against local decompile first

### 6. Ignoring ReSharper Warnings

**Problem:** Code quality degrades over time

**Solution:** Fix warnings, don't suppress unless absolutely necessary

### 7. Forgetting Tooltips in Events

**Problem:** Tooltips null or missing, players don't understand consequences

**Solution:** Tooltips cannot be null. Every option must have a factual tooltip

### 8. Mixing JSON Field Order

**Problem:** Localization breaks when ID/fallback fields are separated

**Solution:** Always put fallback field immediately after ID field

### 9. Missing XML Localization Strings

**Problem:** Events show raw string IDs instead of text

**Solution:**

1. Run `python Tools/Validation/sync_event_strings.py`
2. Run `.\Tools\Validation\lint_repo.ps1` before committing

### 10. Missing SaveableTypeDefiner Registration

**Problem:** "Cannot Create Save" error when serializing custom types

**Solution:** Register new classes/enums in `EnlistedSaveDefiner`

### 11. Single-Line Statements Without Braces

**Problem:** Reduces readability, risk of logic errors

**Solution:** Always use braces for control statements

### 12. Redundant Namespace Qualifiers

**Problem:** Verbose, hard to read

**Solution:** Add proper `using` statements, remove full namespace paths

### 13. Unused Code

**Problem:** Clutters codebase, confuses developers

**Solution:** Remove unused imports, variables, methods

### 14. Redundant Default Parameters

**Problem:** Passing default values explicitly is redundant

**Solution:** Omit parameters that match method defaults

### 15. Missing XP Rewards in Order Events

**Problem:** Order events only grant reputation without skill XP

**Solution:**

1. All order event options must include `effects.skillXp`
2. Match XP to activity type (see [event-system-schemas.md](Features/Content/event-system-schemas.md))
3. Failed skill checks should grant reduced XP (50% of success)

### 16. Not Persisting In-Progress State Flags

**Problem:** UI state flags (like `_bagCheckInProgress`) are reset on load, causing duplicate events or lost progress

**Solution:**

1. Persist all workflow state flags in `SyncData()`, not just completed/scheduled flags
2. When transient data (like event queues) needs restoration after load, check in-progress flags in `ValidateLoadedState()` and re-queue
3. Example: The bag check event can be saved mid-popup; on load, `_bagCheckInProgress` must be persisted to prevent duplicate queueing

### 17. Finishing PlayerEncounter During Settlement Activity

**Problem:** Clicking "Wait here for some time" or other settlement menus causes the town menu to disappear when unrelated battles end elsewhere on the map

**Root Cause:** `CleanupPostEncounterState()` was finishing `PlayerEncounter` even when the player was still inside a settlement
**Solution:**

1. Always check if player is inside settlement before finishing encounters: `PlayerEncounter.InsideSettlement || HasExplicitlyVisitedSettlement`
2. Settlement encounters should only be finished when the player actually leaves the settlement, not during internal menu navigation
3. See `encounter-safety.md` for full details on the fix (2026-01-04)

### 18. Registering Dead Heroes with VisualTrackerManager

**Problem:** Attempting to register or unregister dead heroes with `VisualTrackerManager` causes crashes

**Root Cause:** When a lord dies, the grace period system tried to register the dead hero for map tracking, causing crashes when the player later tried to re-enlist
**Solution:**

1. Always check `Hero.IsAlive` before calling `VisualTrackerManager.RegisterObject()` or `RemoveTrackedObject()`
2. Wrap tracker operations in try-catch for defensive programming
3. Example from grace period system: Only register lord for tracking if `_enlistedLord.IsAlive` is true
4. Dead heroes cannot be displayed on the map tracker, so skipping them is correct behavior

### 19. Calling `EventDeliveryManager.Instance.QueueEvent` Directly

**Problem:** Calling `EventDeliveryManager.Instance.QueueEvent(evt)` directly bypasses StoryDirector pacing (no floor, no cooldown, no deferral). The only legitimate direct-call sites are (a) Director-null fallbacks inside a migrated caller, (b) the debug tool at `src/Debugging/Behaviors/DebugToolsBehavior.cs:141`, and (c) the Director's own internal `Route()`. Everything else must use `StoryDirector.Instance?.EmitCandidate(...)` — see Critical Rule #10.

**Solution:** Route all modal event delivery through `StoryDirector.Instance?.EmitCandidate(...)`.

### 20. Authoring New Content as Legacy `EventDefinition` JSON

**Problem:** Authoring new content as legacy `EventDefinition` JSON instead of as storylets. Storylets are the canonical target as of Spec 0 (2026-04-19); `EventDefinition` content still loads at runtime but is not the target for new authoring.

**Solution:** Author new content as storylets in `ModuleData/Enlisted/Storylets/*.json`. See [docs/Features/Content/storylet-backbone.md](Features/Content/storylet-backbone.md).

---

## Dependencies

The mod requires Harmony for Bannerlord:

```xml
<DependedModules>
  <DependedModule Id="Bannerlord.Harmony" />
</DependedModules>
```

Users must enable Harmony in the Bannerlord launcher before activating Enlisted.

---

## Understanding the Project

### Scope

The mod focuses on the **enlisted lord's party** (the Company). If the Company is part of a larger Army, acknowledge that as context only. Deep army-wide simulation is future work.

### Key Concepts

- Player uses an **invisible party** while enlisted (see [enlistment.md](Features/Core/enlistment.md))
- **Native integration** preferred over custom UI (use game menus, trait system, etc.)
- **Data-driven content** via JSON events/orders + XML localization
- **Emergent identity** from player choices (not menu selections)

**Event pacing:** `StoryDirector` (`src/Features/Content/StoryDirector.cs`) is
the single gate for modal event delivery. Sources emit `StoryCandidate` via
`EmitCandidate`; the Director routes Modal candidates subject to floor + wall-
clock + category-cooldown guards, defers floor-blocked interactives to a FIFO
retry queue, and writes observational items as `DispatchItem` entries via
`EnlistedNewsBehavior.AddPersonalDispatch`. See the
[pacing design spec](superpowers/specs/2026-04-18-event-pacing-design.md) and
[implementation plan](superpowers/plans/2026-04-18-event-pacing.md).
Direct `EventDeliveryManager.Instance.QueueEvent(...)` calls are reserved for
Director fallback paths (when the singleton isn't yet registered) and the
debug-tool test harness.

### Design Principles

- Emergent identity from choices, not menus
- Native Bannerlord integration (traits, skills, game menus)
- Minimal custom UI (use game's native systems)
- Choice-driven narrative progression
- Realistic military hierarchy and consequences

---

## Steam Workshop

**Workshop ID:** `3621116083`
**URL:** <https://steamcommunity.com/sharedfiles/filedetails/?id=3621116083>

For deployment instructions, see [Tools/Steam/WORKSHOP_UPLOAD.md](../Tools/Steam/WORKSHOP_UPLOAD.md).

---

## Deprecated Systems

The Morale System and Company Rest System were removed 2026-01-11 (redundant with discipline/rest/supply and Player Fatigue respectively). Save-load compat shims remain across ~25 files in `src/Features/Camp/`, `src/Features/Enlistment/`, `src/Features/Company/`. Full code removal deferred — grep `Morale` or `CompanyNeed.Rest` if you need to locate the remnants.

---

## Battle AI (Optional SubModule)

Battle AI is an **optional SubModule** that users can disable in the Bannerlord launcher.

### Key Rules

1. **Never initialize Battle AI from Core SubModule** - Core must not reference Battle AI
2. **All Battle AI initialization happens in BattleAISubModule** only
3. **Enlisted-Only Activation** - Battle AI only runs when player is enlisted
4. **Field Battles Only** - Siege and naval use native AI
5. **No Cheating** - Improvements come from better decisions, not stat buffs

### File Organization

All Battle AI code in `src/Features/Combat/BattleAI/` wrapped in `#if BATTLE_AI`.

**Full documentation:** [Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md](Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md)

---

## Documentation Standards

### Format Requirements

```markdown
# Title

**Summary:** 2-3 sentences explaining what this covers

**Status:** ✅ Current | ⚠️ In Progress | 📋 Specification | 📚 Reference
**Last Updated:** YYYY-MM-DD
**Related Docs:** [Link 1], [Link 2]

---

## Content
```

### File Naming

Use kebab-case: `my-new-feature.md`

### Location Guide

| Content Type | Location |
| --- | --- |
| Core systems | `Features/Core/` |
| Equipment/logistics | `Features/Equipment/` |
| Combat mechanics | `Features/Combat/` |
| Events/content | `Features/Content/` |
| Campaign/world | `Features/Campaign/` |
| UI systems | `Features/UI/` |
| Technical specs | `Features/Technical/` |
| API/research | `Reference/` |

---

## Detailed References

For comprehensive technical details:

- **Logging, Save System, Patterns:** [Tools/TECHNICAL-REFERENCE.md](../Tools/TECHNICAL-REFERENCE.md)
- **Build Configurations:** [BUILD-CONFIGURATIONS.md](BUILD-CONFIGURATIONS.md)
- **Steam Workshop:** [Tools/Steam/WORKSHOP_UPLOAD.md](../Tools/Steam/WORKSHOP_UPLOAD.md)
- **Validation Tools:** [Tools/README.md](../Tools/README.md)
- **UI Systems:** [Features/UI/ui-systems-master.md](Features/UI/ui-systems-master.md)
- **Native APIs:** [Reference/native-apis.md](Reference/native-apis.md)
