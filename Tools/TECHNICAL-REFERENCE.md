# Enlisted Technical Reference

Detailed technical specifications for the Enlisted mod codebase. For project overview and quick-start, see [BLUEPRINT.md](../docs/BLUEPRINT.md).

---

## Index

1. [Logging System](#logging-system)
2. [Save System & Serialization](#save-system--serialization)
3. [Key Code Patterns](#key-code-patterns)
4. [Menu System](#menu-system)
5. [Configuration Files](#configuration-files)

---

## Logging System

### Output Location

**All mod logs output to:** `<BannerlordInstall>\Modules\Enlisted\Debugging\`

Example: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\`

This is NOT the game's crash logs folder and NOT Documents.

### Installation Locations

**Steam Workshop subscribers:** `C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\3621116083\`  
**Manual/Nexus users:** `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\`

**Note:** Having both versions installed creates duplicate entries in the launcher and can cause conflicts. The mod includes automatic conflict detection at startup - check `Conflicts-A_{timestamp}.log` for mod compatibility issues.

### Session Rotation

The logger uses a **three-session rotation**:

| File | Purpose |
|------|---------|
| `Session-A_*.log` | Current/newest session |
| `Session-B_*.log` | Previous session |
| `Session-C_*.log` | Oldest session (deleted on next rotation) |
| `Current_Session_README.txt` | Points to active session |

When you start a new game session:
1. Old Session-B → Session-C (oldest is deleted)
2. Old Session-A → Session-B
3. New Session-A is created with current timestamp

### Usage

```csharp
ModLogger.Info("Category", "message");
ModLogger.Debug("Category", "detailed info");
ModLogger.Warn("Category", "warning");
ModLogger.Error("Category", "error details");
ModLogger.LogOnce("UniqueKey", "Category", "message"); // Only logs once per session

// Surfaced: player/dev-visible failure. Toasts once per (category, code) per session.
//           Codes auto-hash from the summary string; never hand-pick.
//           Toggle toast display with ModLogger.ShowCodedMessagesOnScreen (default true).
ModLogger.Surfaced("CATEGORY", "error summary", ex, LogCtx.PlayerState());

// Caught: defensive catch (Harmony, cleanup). Logs only, no toast.
//         Throttled per (category, file, line) on a 60s window.
ModLogger.Caught("CATEGORY", "error summary", ex);

// Expected: guard-rail early exit (no exception). INFO level, no stack trace.
//           Throttled per key on a 60s window.
ModLogger.Expected("CATEGORY", "stable_throttle_key", "guard summary");
```

### Categories

- **Core:** Enlistment, Combat, Equipment, Events, Orders, Reputation
- **Systems:** Identity, Company, Context, Interface, Ranks, Conversations
- **Features:** Retinue, Camp, Conditions, Supply, Logistics, Naval
- **Diagnostics:** SiegeIntegration, BattleIntegration, EncounterGuard, CaptivityStatus

### Logging Levels

| Level | Use For |
|-------|---------|
| `Info` | Key decisions affecting gameplay, user-visible actions, diagnostic info |
| `Debug` | Internal validation, tick/update details, intermediate values |
| `Warn` | Unexpected but recoverable situations, deprecated paths |
| `Error` | Exceptions, failures, invalid state |

### Configuration

Configure levels in `settings.json`:
```json
{
  "LogLevels": {
    "Default": "Info",
    "BATTLE": "Debug",
    "EQUIPMENT": "Warn"
  }
}
```

### Performance

All logging includes throttling and de-duplication to prevent log spam. Use `LogOnce` for messages that would otherwise repeat every tick.

---

## Save System & Serialization

Bannerlord's save system requires explicit registration of custom types.

### Type Registration

All mod-specific serializable types are registered in `EnlistedSaveDefiner` (`src/Mod.Core/SaveSystem/`).

**Adding new serializable types:**
1. Add the class/enum to `DefineClassTypes()` or `DefineEnumTypes()` with a unique save ID
2. If using `Dictionary<T1,T2>` or `List<T>` with custom types, add container definition in `DefineContainerDefinitions()`
3. Implement `SyncData()` in the behavior that owns the state

### SyncData Implementation

```csharp
public override void SyncData(IDataStore dataStore)
{
    SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
    {
        dataStore.SyncData("myKey", ref _myField);
    });
}
```

### Key Rules

- Custom classes need `AddClassDefinition(typeof(MyClass), saveId)` in the definer
- Container types like `Dictionary<string, int>` need `ConstructContainerDefinition(typeof(...))`
- Use primitive types in SyncData when possible
- Serialize dictionaries element-by-element
- Enums can be cast to/from int for serialization
- **Persist ALL workflow state flags**: Not just completed/scheduled flags, but also in-progress flags (e.g., `_bagCheckInProgress`)

### Common Save/Load Pitfalls

**Problem:** Event fires multiple times after save/load
- **Cause:** In-progress state flag (`_inProgress`) not persisted, only scheduled/completed flags
- **Symptom:** After load, guard check passes again and queues duplicate event
- **Solution:** Add in-progress flag to `SyncData()`, restore transient data (event queues) in load validation

**Example:**
```csharp
// ✅ CORRECT: Persist in-progress state
SyncData(dataStore, "_eventScheduled", ref _scheduled);
SyncData(dataStore, "_eventCompleted", ref _completed);
SyncData(dataStore, "_eventInProgress", ref _inProgress);  // Critical!

// In ValidateLoadedState():
if (_inProgress && !_completed) {
    // Re-queue event since queue is transient
    EventManager.QueueEvent(GetEvent());
}
```

---

## Key Code Patterns

### Party Following & Visibility

```csharp
party.SetMoveEscortParty(lordParty, NavigationType.Default, false);
party.IsVisible = false;  // Hide on map
```

### Gold Transactions

Always use `GiveGoldAction` to ensure the party treasury updates correctly in UI:
```csharp
GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount);
```

### Reputation & Needs

Always use centralized managers (`EscalationManager`, `CompanyNeedsManager`) to modify state. They handle clamping and logging.

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

### Equipment Slot Iteration

Use numeric loop (not `Enum.GetValues`):
```csharp
// ❌ WRONG: Includes invalid count values, causes IndexOutOfRangeException
foreach (EquipmentIndex slot in Enum.GetValues(typeof(EquipmentIndex))) { ... }

// ✅ CORRECT: Iterate valid indices only (0-11)
for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
{
    var slot = (EquipmentIndex)i;
}
```

### Dynamic Dialogue Registration

For context-sensitive dialogue that varies by game state:

```csharp
// Condition delegate evaluated every time dialogue navigates to this node
ConversationSentence.OnConditionDelegate condition = () => {
    var currentContext = GetCurrentDialogueContext();
    return nodeContext.Matches(currentContext);
};

// Register with Bannerlord system
starter.AddDialogLine(id, inputToken, outputToken, text, condition, consequence, priority);
```

**Manual Registration for Dynamic Text Variables:**

When a dialogue node needs to generate text dynamically:
```csharp
// Register NPC line with text variable setter
starter.AddDialogLine(
    "qm_supply_response_dynamic",
    "qm_supply_response",
    "qm_supply_response",
    "{=qm_supply_report}{SUPPLY_STATUS}",
    () => IsQuartermasterConversation() && SetSupplyStatusText(),
    null,
    200);

// Register player options that would normally come from JSON
starter.AddPlayerLine(
    "qm_supply_continue",
    "qm_supply_response",
    "qm_hub",
    "{=qm_continue}[Continue]",
    IsQuartermasterConversation,
    null,
    100);
```

---

## Menu System

### Menu IDs

| Menu ID | Purpose |
|---------|---------|
| `enlisted_camp_hub` | Central navigation hub with accordion-style decision sections |
| `enlisted_medical` | Medical care and treatment (when player has active condition) |
| `enlisted_muster_*` | Multi-stage muster system sequence (6 stages, every 12 days) |

### Decision Sections (within Camp Hub)

| Section | Description |
|---------|-------------|
| TRAINING | Training-related decisions (dec_weapon_drill, dec_spar, etc.) |
| SOCIAL | Social interactions (dec_join_men, dec_write_letter, etc.) |
| ECONOMIC | Economic decisions (dec_gamble_low, dec_side_work, etc.) |
| CAREER | Career advancement (dec_request_audience, dec_volunteer_duty, etc.) |
| INFORMATION | Intelligence gathering (dec_listen_rumors, dec_scout_area, etc.) |
| EQUIPMENT | Gear management (dec_maintain_gear, dec_visit_quartermaster) |
| RISK_TAKING | High-risk actions (dec_dangerous_wager, dec_prove_courage, etc.) |
| CAMP_LIFE | Self-care and rest (dec_rest, dec_seek_treatment) |
| LOGISTICS | Quartermaster-related (from events_player_decisions.json) |

### Event Delivery

- Uses `MultiSelectionInquiryData` popups for narrative events
- Triggered by: EventPacingManager, EscalationManager, DecisionManager
- Muster-specific events integrated as menu stages

**Full documentation:** See [ui-systems-master.md](../docs/Features/UI/ui-systems-master.md)

---

## Configuration Files

### Game Content Configuration

| File | Purpose |
|------|---------|
| `enlisted_config.json` | Feature flags and core balancing |
| `progression_config.json` | Tier XP thresholds and culture-specific rank titles |
| `Orders/*.json` | Mission definitions for the Orders system |
| `Events/*.json` | Role-based narrative events, automatic decisions |
| `Decisions/*.json` | Player-initiated Camp Hub decisions (34 total) |

### Code Quality Configuration

**Before modifying code, review these linter configuration files:**

#### `.editorconfig` - EditorConfig Rules

Controls formatting and style enforcement in IDEs (Rider, Visual Studio):

**C# Code Quality (Blueprint Standards):**
```editorconfig
dotnet_diagnostic.IDE0005.severity = warning  # Remove unnecessary using directives
dotnet_diagnostic.IDE0001.severity = warning  # Simplify name (remove redundant qualifiers)
dotnet_diagnostic.IDE0002.severity = warning  # Simplify member access (remove redundant this/base)
dotnet_diagnostic.CS8019.severity = warning   # Unnecessary using directive
dotnet_diagnostic.IDE0079.severity = warning  # Remove unnecessary suppression
```

**Formatting Rules:**
- **C# files**: 4-space indentation, braces on new lines, `_camelCase` for private fields
- **JSON files**: 2-space indentation (events, decisions, orders)
- **XML files**: 2-space indentation (localization strings)
- **Markdown**: Excluded from linting (`generated_code = true`)

#### `qodana.yaml` - Qodana Static Analysis

CI/CD pipeline configuration for automated code quality checks:

**Enforced Inspections (Blueprint Compliance):**
- `RedundantUsingDirective` - Flags unused `using` statements
- `RedundantNameQualifier` - Flags unnecessary namespace qualifiers (e.g., `System.String.Empty`)
- `UnusedMember.Local` - Flags unused private methods/fields
- `UnusedParameter.Local` - Flags unused method parameters
- `ConvertToConstant.Local` - Suggests `const` instead of `readonly` where applicable

**Excluded Paths:**
- `**/*.md` - Markdown documentation files
- `**/Tools/**` - Python/PowerShell development scripts
- `**/Debugging/**` - Temporary analysis and debug reports

**Documented Suppressions:**
All suppressions include comments explaining why:
- Harmony patches - Methods called dynamically by Harmony runtime
- Gauntlet ViewModels - Properties bound by Gauntlet XML at runtime
- Singleton patterns - Instance property getters used, setters internal
- False positives - Documented case-by-case

#### `Enlisted.sln.DotSettings` - ReSharper Settings

Solution-level ReSharper configuration:
- Treats `*.md` files as generated code (disables markdown inspections)
- Suppresses specific markdown warnings for documentation tables/links

**Rule of Thumb:** Never suppress inspections without documented justification (see `qodana.yaml` for examples)

---

## Related Documentation

- [BLUEPRINT.md](../docs/BLUEPRINT.md) - Project overview and standards
- [INDEX.md](../docs/INDEX.md) - Complete documentation catalog
- [Tools/README.md](README.md) - Development tools reference
- [native-apis.md](../docs/Reference/native-apis.md) - Bannerlord API snippets
