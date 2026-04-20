# Developer Guide

**Summary:** Quick reference for building, modifying, and extending the Enlisted mod. Covers setup, structure, common tasks, and integration patterns.

**Last Updated:** 2026-04-19 (Storylet Backbone Spec 0 shipped — new folders under `src/Features/`: Qualities, Flags, Activities. Content authoring now routes through storylets per AGENTS.md Critical Rule #11.)  
**Target Game:** Bannerlord v1.3.13  
**Related Docs:** [BLUEPRINT.md](BLUEPRINT.md), [Reference/native-apis.md](Reference/native-apis.md)

---

## Index

1. [Build](#build)
2. [Project Structure](#project-structure)
3. [Adding New Files](#adding-new-files)
4. [Configuration Files](#configuration-files)
5. [Debugging & Logging](#debugging--logging)
6. [API Reference](#api-reference)
7. [Adding a Harmony Patch](#adding-a-harmony-patch)
8. [Critical Patterns](#critical-patterns)
9. [Mod Integration](#mod-integration)
10. [Key Systems Overview](#key-systems-overview)
11. [Guidelines](#guidelines)

---

## Build

**Visual Studio:**

- Configuration: "Enlisted RETAIL"
- Platform: x64
- Click Build

**Command Line:**

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

**Output Locations** (one build populates both; see [BUILD-CONFIGURATIONS.md](BUILD-CONFIGURATIONS.md) for why):

```text
<BannerlordInstall>/Modules/Enlisted/bin/Win64_Shipping_Client/Enlisted.dll
<BannerlordInstall>/Modules/Enlisted/bin/Win64_Shipping_wEditor/Enlisted.dll
```

Close the Bannerlord launcher before building — `BannerlordLauncher` / `Watchdog` processes hold `Enlisted.dll` open and will fail the post-build copy.

---

## Project Structure

```text
src/
├── Mod.Entry/              # SubModule.cs - Harmony initialization
├── Mod.Core/               # Logging, config, helpers
├── Mod.GameAdapters/       # Harmony patches
└── Features/
    ├── Enlistment/         # Core state, wages, XP, retirement
    ├── Orders/             # Mission-driven directives (Chain of Command)
    ├── Content/            # Events, Decisions, and narrative delivery
    ├── Identity/           # Role detection (Traits) and Reputation helpers
    ├── Escalation/         # Lord/Officer/Soldier reputation and Discipline
    ├── Company/            # Company-wide Needs (Readiness, Morale, Supply)
    ├── Context/            # Army context and objective analysis
    ├── Interface/          # Camp Hub menu, News/Reports
    ├── Equipment/          # Quartermaster and gear management
    ├── Ranks/              # Promotions and culture-specific titles
    ├── Conversations/      # Dialog management
    ├── Combat/             # Battle participation and formation assignment
    ├── Conditions/         # Player medical status (injury/illness)
    ├── Retinue/            # Service Records and Retinue/Companion management
    └── Camp/               # Camp activities and rest logic
```

**Data and Localization:**

- `ModuleData/Enlisted/` - JSON config + content
- `ModuleData/Languages/` - XML localization (enlisted_strings.xml)

---

## Adding New Files

**CRITICAL**: This project uses an old-style `.csproj` with explicit file includes. New `.cs` files are NOT automatically compiled.

### Steps

1. Create the `.cs` file in the appropriate location
2. **Manually add it to `Enlisted.csproj`** in the `<ItemGroup>` with `<Compile Include="..."/>` entries
3. Build and verify the file is included
4. Run validation to confirm: `python Tools/Validation/validate_content.py`

### Example

Adding a new patch at `src\Mod.GameAdapters\Patches\YourNewPatch.cs`:

```xml
<Compile Include="src\Mod.GameAdapters\Patches\YourNewPatch.cs"/>
```

**If you forget this step:**

- The file will exist but won't be compiled, and your code won't run
- The validator will catch this in Phase 7 and report it as a [CRITICAL] error

---

## Configuration Files

### Game Configuration Files

All gameplay configuration files are in `ModuleData/Enlisted/`:

| File | Purpose |
| :--- | :--- |
| `settings.json` | Logging levels, encounter settings |
| `enlisted_config.json` | Tiers, wages, retirement, feature flags |
| `progression_config.json` | XP thresholds, culture-specific rank titles |
| `Orders/*.json` | Order definitions for Chain of Command |
| `Events/*.json` | Role-based narrative and social events |
| `Decisions/*.json` | Decision definitions for Camp Hub |
| `equipment_kits.json` | Culture-specific equipment loadouts |
| `equipment_pricing.json` | Quartermaster costs |

### Code Quality Configuration Files

**Read these before modifying code:**

| File | Purpose | Key Rules |
| :--- | :--- | :--- |
| [.editorconfig](../.editorconfig) | Formatting and style | 4-space C# indent, warns on unused `using`, warns on redundant qualifiers |
| [qodana.yaml](../qodana.yaml) | Static analysis (CI) | Enforces: unused code detection, redundant qualifier removal, documented suppressions only |
| [Enlisted.sln.DotSettings](../Enlisted.sln.DotSettings) | ReSharper settings | Excludes markdown from inspections |

**What these enforce (from Blueprint):**

- ✅ No unused `using` directives (warns in IDE)
- ✅ No redundant namespace qualifiers like `System.String.Empty` (warns in IDE)
- ✅ No unused methods, variables, or parameters (Qodana CI check)
- ✅ JSON/XML files use 2-space indentation
- ✅ All suppressions must be documented with reasons

See [Tools/TECHNICAL-REFERENCE.md](../Tools/TECHNICAL-REFERENCE.md#code-quality-configuration) for complete configuration details.

---

## Debugging & Logging

### Mod Logs (Enlisted)

**ALL ENLISTED MOD LOGS OUTPUT TO:**

```text
<BannerlordInstall>\Modules\Enlisted\Debugging\
```

**Example full path:**

```text
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\
```

**CRITICAL:** The mod writes all logs directly to the `Debugging` subfolder inside the Enlisted module directory. This is NOT the game's ProgramData crash folder and NOT your Documents folder.

**Files created in the Debugging folder:**

- `Session-A_{yyyy-MM-dd_HH-mm-ss}.log` - Current session (newest)
- `Session-B_{yyyy-MM-dd_HH-mm-ss}.log` - Previous session
- `Session-C_{yyyy-MM-dd_HH-mm-ss}.log` - Oldest kept session
- `Conflicts-A_{yyyy-MM-dd_HH-mm-ss}.log` - Current conflicts diagnostics (newest)
- `Conflicts-B_{yyyy-MM-dd_HH-mm-ss}.log` - Previous conflicts
- `Conflicts-C_{yyyy-MM-dd_HH-mm-ss}.log` - Oldest kept conflicts
- `Current_Session_README.txt` - Active log summary and sharing instructions

**Session logs contain:**

- Main activity log with category-based verbosity
- Categories: Enlistment, Equipment, Orders, Combat, Events, etc.

**Conflicts logs contain:**

- Comprehensive mod conflict diagnostics
- Harmony patch conflicts (other mods patching the same methods)
- Patch execution order and priorities
- All registered campaign behaviors
- Environment info (game version, mod version, OS, runtime, installation paths)
- Complete module list
- Categorized patch inventory (Army/Party, Encounter, Finance, UI/Menu, Combat, etc.)
- **Module health check:** Verifies presence of JSON/XML files for all content types (Dialogue, Events, Decisions, Orders, Config, Localization)
- **Runtime catalog status:** Confirms successful loading/parsing of content catalogs (EventCatalog, DecisionCatalog, QMDialogueCatalog, OrderCatalog)
- **Patch application status:** Reports total number of methods patched and patch counts (prefix, postfix, transpiler, finalizer)

**Configure Log Levels:**

Edit `ModuleData/Enlisted/settings.json`:

```json
{
  "LogLevels": {
    "Default": "Info",
    "BATTLE": "Debug",
    "EQUIPMENT": "Warn",
    "ENLISTMENT": "Info"
  }
}
```

### Game Crash Logs (Bannerlord)

**Separate location:** `C:\ProgramData\Mount and Blade II Bannerlord\crashes\`

Each crash creates a timestamped folder (e.g., `2025-12-08_03.41.58\`) containing:

- `crash_tags.txt` - Module versions and system info
- `rgl_log_*.txt` - Engine logs (large, check near end for crash context)
- `rgl_log_errors_*.txt` - Engine error summary
- `dump.dmp` - Memory dump for debugging
- `module_list.txt` - Active modules at crash time

---

## API Reference

When working with Bannerlord APIs, verify usage against:

- **The local native decompile** (authoritative) - `C:\Dev\Enlisted\Decompile\`
- The official API docs for v1.3.13 when needed for quick lookups

### Native Decompile Location

The decompiled Bannerlord source is located at:

```text
C:\Dev\Enlisted\Decompile\
```

**Key decompiled assemblies:**

| Assembly | Location | Contents |
| :--- | :--- | :--- |
| TaleWorlds.CampaignSystem | `TaleWorlds.CampaignSystem\TaleWorlds\` | Party, Settlement, Campaign behaviors |
| TaleWorlds.Core | `TaleWorlds.Core\` | Basic types, CharacterObject, ItemObject |
| TaleWorlds.Library | `TaleWorlds.Library\` | Vec2, MBList, utility classes |
| TaleWorlds.MountAndBlade | `TaleWorlds.MountAndBlade\TaleWorlds\` | Mission, Agent, combat |
| SandBox.View | `SandBox.View\` | Menu views, map handlers |

**Example:** To find the correct property for party position:

```text
C:\Dev\Enlisted\Decompile\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\Party\MobileParty.cs
```

Key APIs:

- `MobileParty.GetPosition2D` → `Vec2` (not `Position2D`)
- `Settlement.GetPosition2D` → `Vec2`
- `MobileParty.Position` → `CampaignVec2`

---

## Adding a Harmony Patch

### Patch Steps

1. Create patch file in `src/Mod.GameAdapters/Patches/`
2. Add to `Enlisted.csproj` (see [Adding New Files](#adding-new-files))
3. Document purpose in header comment

### Patch Example

```csharp
/// <summary>
/// Prevents party following from breaking when lord dies.
/// Targets MobileParty.OnLordRemoved.
/// </summary>
[HarmonyPatch(typeof(MobileParty), "OnLordRemoved")]
public class PartyFollowingPatch
{
    static bool Prefix(MobileParty __instance)
    {
        // Your patch logic
        return true; // Continue to original method
    }
}
```

### Harmony Priority

All Enlisted patches use default priority (400). If your mod patches the same methods:

- Use Priority.Low (200) to run after Enlisted
- Use Priority.High (600) to run before Enlisted

### Menu Override System

The mod uses a two-layer approach to prevent native menus from appearing while enlisted:

**Primary Layer - GenericStateMenuPatch:**

- Patches `DefaultEncounterGameMenuModel.GetGenericStateMenu()`
- Intercepts menu selection before native system activates it
- Overrides: `"castle"`, `"castle_outside"`, `"town"`, `"town_outside"`, `"village"`, `"army_wait"`, `"army_wait_at_settlement"`
- Returns `"enlisted_status"` to keep player in enlisted menu
- Respects `HasExplicitlyVisitedSettlement` flag (player clicked "Visit Settlement")

**Fallback Layer - OnMenuOpened:**

- Event handler in `EnlistedMenuBehavior.cs`
- Catches menus that bypass the Harmony patch
- Same menu IDs as primary layer
- Defers override to next frame via `NextFrameDispatcher`

**Critical Detail:** Both layers must handle `*_outside` menu variants. The native system returns `"castle_outside"` and `"town_outside"` when the lord enters a settlement without an explicit player encounter (pause-in-castle scenario).

---

## Critical Patterns

### Deferred Operations

Use for encounter transitions:

```csharp
NextFrameDispatcher.RunNextFrame(() => GameMenu.ActivateGameMenu("menu_id"));
```

### Safe Hero Access

Null-safe during character creation:

```csharp
var hero = CampaignSafetyGuard.SafeMainHero;
```

### Party Following

```csharp
party.SetMoveEscortParty(lordParty, NavigationType.Default, false);
party.IsVisible = false;  // Hide on map
```

### Gold Transactions

Always use `GiveGoldAction` (NOT `ChangeHeroGold`):

```csharp
// ❌ WRONG: ChangeHeroGold modifies internal gold not visible in UI
Hero.MainHero.ChangeHeroGold(-amount);

// ✅ CORRECT: GiveGoldAction updates party treasury visible in UI
GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount);  // Deduct
GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount);  // Grant
```

### Equipment Slot Iteration

Use numeric loop (NOT `Enum.GetValues`):

```csharp
// ❌ WRONG: Includes invalid count values, causes IndexOutOfRangeException
foreach (EquipmentIndex slot in Enum.GetValues(typeof(EquipmentIndex))) { ... }

// ✅ CORRECT: Iterate valid indices only (0-11)
for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
{
    var slot = (EquipmentIndex)i;
}
```

### Item Comparison and Confiscation

Use `StringId` comparison (NOT reference equality):

```csharp
// ❌ WRONG: Reference equality can fail if items are different instances
if (element.Item == itemToRemove) { ... }

// ✅ CORRECT: Use StringId comparison for reliable item matching
if (element.Item != null && element.Item.StringId == itemToRemove.StringId) { ... }
```

When removing items from inventory, especially equipped items:

```csharp
// 1. Check if equipped using StringId comparison
bool isEquipped = false;
for (int i = 0; i < 12; i++)
{
    var element = equipment.GetEquipmentFromSlot((EquipmentIndex)i);
    if (element.Item != null && element.Item.StringId == itemToRemove.StringId)
    {
        isEquipped = true;
        break;
    }
}

// 2. Unequip before removing from inventory
if (isEquipped)
{
    var newEquipment = equipment.Clone();
    // ... remove from newEquipment ...
    equipment.FillFrom(newEquipment);
}

// 3. Remove from inventory
party.ItemRoster.AddToCounts(itemToRemove, -1);
```

### Reputation & Needs

Always use centralized managers (they handle clamping and logging):

```csharp
EscalationManager.Instance.ModifyReputation(ReputationType.Officer, 5, "reason");
CompanyNeedsManager.Instance.ModifyNeed(NeedType.Morale, -10, "reason");
```

---

## Mod Integration

Other mods can hook into Enlisted's systems via public events and properties.

### Events

Subscribe in your `OnGameStart` or `OnCampaignStart`:

```csharp
using Enlisted.Features.Enlistment.Behaviors;

// Subscribe to enlistment events
EnlistmentBehavior.OnEnlisted += (lord) => {
    // Player enlisted with lord
};

EnlistmentBehavior.OnDischarged += (reason) => {
    // Player discharged (reason: "Retirement", "Leave expired", etc.)
};

EnlistmentBehavior.OnPromoted += (tier) => {
    // Player promoted to tier (1-6)
};

EnlistmentBehavior.OnXPGained += (xp, source) => {
    // XP gained (source: "Daily Service", "Battle", "Kill", etc.)
};

EnlistmentBehavior.OnWagePaid += (amount) => {
    // Daily wage paid
};

EnlistmentBehavior.OnLeaveStarted += () => {
    // Player started temporary leave
};

EnlistmentBehavior.OnLeaveEnded += () => {
    // Leave ended (returned or deserted)
};

EnlistmentBehavior.OnGracePeriodStarted += () => {
    // Grace period started (lord died, army defeated)
};
```

### Public Properties

```csharp
var eb = EnlistmentBehavior.Instance;
if (eb?.IsEnlisted == true)
{
    Hero lord = eb.CurrentLord;           // Serving lord
    int tier = eb.EnlistmentTier;         // 1-6
    int xp = eb.EnlistmentXP;             // Total XP
    bool onLeave = eb.IsOnLeave;          // On leave?
    bool inGrace = eb.IsInDesertionGracePeriod;
}
```

---

## Key Systems Overview

### EnlistmentBehavior

- Tracks `_enlistedLord`, tier, XP, kills, grace periods
- Real-time ticks for following, daily ticks for wages
- +25 XP per battle, +1 XP per kill
- 252-day first term, 84-day renewals

### Orders System

- JSON-defined in `ModuleData/Enlisted/Orders/*.json`
- Tier-gated missions from lord
- Completion affects reputation

### Equipment System

- Culture-specific loadouts via `equipment_kits.json`
- Quartermaster manages distribution
- Equipment replaced on promotion, restored on discharge

### Events & Decisions

- **Events:** Role-specific narrative delivered via popup (MultiSelectionInquiryData)
- **Decisions:** Player-initiated choices in Camp Hub
- Both use JSON definitions with localization support

---

## Guidelines

### Code Quality

- **Read the configuration files first**: [.editorconfig](../.editorconfig), [qodana.yaml](../qodana.yaml), [Enlisted.sln.DotSettings](../Enlisted.sln.DotSettings)
- **ReSharper/Rider is the linter**: Follow warnings and recommendations
- **Fix issues, don't suppress**: Only suppress with documented justification (see `qodana.yaml` for examples)
- **Enforced by CI**: Unused code, redundant qualifiers, and missing documentation are flagged by Qodana
- **Blueprint Constraint #6**: "Follow ReSharper recommendations (never suppress without documented reason)"

### Comments

- Comments explain *why*, not *what*
- Describe current behavior factually
- Avoid changelog-style framing ("Phase X added...", "Changed from...")
- Write professionally and naturally

### Data Files

- **XML for UI + localization**:
  - **Gauntlet UI layout** lives in XML prefabs under `GUI/Prefabs/**.xml`
  - **Localized strings** live in `ModuleData/Languages/enlisted_strings.xml`
  - In code, prefer `TextObject("{=some_key}Fallback text")` and add the same key to `enlisted_strings.xml`
  - **Dynamic dialogue**: For contextual text (like quartermaster responses), use the data-driven JSON pattern with dynamic runtime evaluation (see Blueprint).
    - Define dialogue variants in JSON with context requirements.
    - Register multiple variants per node ID.
    - Use condition delegates to evaluate context on every display turn.
    - Always provide fallback strings for missing IDs in JSON.
    - See `EnlistedDialogManager.cs` for reference.
- **Gameplay/config data** remains JSON (`ModuleData/Enlisted/*.json`) unless there's a specific reason to use XML

### Development Practices

- Use Harmony only when needed
- Keep changes small and focused
- Verify APIs against local decompile first
- Reuse existing patterns (copy OrderCatalog structure for new catalogs)

### Common Mistakes to Avoid

1. Not adding new files to .csproj ✅ **Validator catches this automatically**
2. Using `ChangeHeroGold` instead of `GiveGoldAction`
3. Iterating equipment with `Enum.GetValues`
4. Modifying reputation/needs directly instead of using managers
5. Relying on external API docs instead of local decompile
6. Tooltips set to null or missing (tooltips cannot be null, every option must have one) ✅ **Validator checks this**
7. Not validating inputs for dynamic dialogue (null checks, archetype validation, value clamping)
8. Using `GetLocalizedText()` without fallback handling (use `GetLocalizedTextSafe()` wrapper instead)
9. Assuming localized strings exist (always provide fallbacks for missing XML strings) ✅ **Validator reports missing strings**
10. Using `==` reference equality to compare `ItemObject` instances (use `StringId` comparison instead to avoid crashes when comparing inventory items to equipped items)

---

## Documentation

| Doc | Purpose |
| :--- | :--- |
| [BLUEPRINT.md](BLUEPRINT.md) | Architecture, patterns, standards |
| [Features/Core/index.md](Features/Core/index.md) | Feature specs and gameplay systems |
| [Reference/campaignsystem-apis.md](Reference/campaignsystem-apis.md) | API notes and research |
| [Content/content-index.md](Content/content-index.md) | Content catalog (events, orders, decisions) |

---

**Questions?** Check the [Blueprint](BLUEPRINT.md) for architecture details or explore the feature documentation in `docs/Features/`.
