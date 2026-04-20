# Conflict Detection System

**Summary:** Documents the `ModConflictDiagnostics` runtime system that automatically detects Harmony patch conflicts with other mods, verifies mod installation integrity, and validates content catalog loading. Also covers internal system integration points and state management priorities for development.

**Status:** ✅ Current  
**Last Updated:** 2026-01-03  
**Related Docs:** [Event System Schemas](../Content/event-system-schemas.md), [Encounter Safety](encounter-safety.md), [Content System Architecture](../Content/content-system-architecture.md), [Orchestrator Opportunity Unification](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md), [Systems Integration Analysis](../../ANEWFEATURE/systems-integration-analysis.md)

---

## Index

### Part 1: Runtime Conflict Detection (End Users & Support)
1. [ModConflictDiagnostics Overview](#modconflictdiagnostics-overview)
2. [How It Works](#how-it-works)
3. [Reading Conflict Logs](#reading-conflict-logs)
4. [When to Share Logs](#when-to-share-logs)

### Part 2: Development Guidelines (Internal)
5. [System Integration Points](#system-integration-points)
6. [State Modification Priorities](#state-modification-priorities)
7. [Content Validation Rules](#content-validation-rules)
8. [Testing & Verification](#testing--verification)
9. [Adding New Systems](#adding-new-systems)

---

# Part 1: Runtime Conflict Detection

## ModConflictDiagnostics Overview

**File:** `src/Mod.Core/Logging/ModConflictDiagnostics.cs` (828 lines)

**Purpose:** Automatically detects when other mods patch the same game methods as Enlisted, helping diagnose incompatibilities.

**When It Runs:**
- **Phase 1:** Automatically on game startup (`OnSubModuleLoad`)
- **Phase 2:** When campaign starts (deferred patches applied on first tick)

**Output Location:**
```
<BannerlordInstall>\Modules\Enlisted\Debugging\Conflicts-A_2025-12-23_14-30-15.log
```

**Log Rotation:** Keeps last 3 conflict logs (Conflicts-A, Conflicts-B, Conflicts-C)

**Performance:** Lightweight - only runs at startup, no per-frame overhead

---

## How It Works

### What It Detects

**ModConflictDiagnostics** performs comprehensive diagnostics:

1. **Harmony Patch Conflicts:** Uses Harmony's patch introspection to find methods that multiple mods patch
2. **Module Health Check:** Verifies presence of critical JSON/XML files for all content types (Dialogue, Events, Decisions, Orders, Config, Localization)
3. **Runtime Catalog Status:** Confirms successful loading and parsing of content catalogs via reflection
4. **Patch Application Status:** Reports total methods patched and breakdown by patch type (prefix, postfix, transpiler, finalizer)
5. **Installation Path Detection:** Logs game root, mod DLL path, and module path to help diagnose Steam Workshop vs manual installs

**Harmony Patch Conflict Detection:**

```csharp
// For each method Enlisted patches
foreach (var method in harmony.GetPatchedMethods())
{
    var patchInfo = Harmony.GetPatchInfo(method);
    
    // Check if other mods also patch this method
    var otherMods = patchInfo.Prefixes
        .Concat(patchInfo.Postfixes)
        .Concat(patchInfo.Transpilers)
        .Select(p => p.owner)
        .Where(owner => owner != "com.enlisted.mod")
        .ToList();
    
    if (otherMods.Any())
    {
        // Log potential conflict
    }
}
```

### Two-Phase Detection

**Phase 1: Main Patches (Startup)**
- Applied during `OnSubModuleLoad`
- Most patches (encounter safety, UI, menus, etc.)
- Logged immediately

**Phase 2: Deferred Patches (Campaign Start)**
- Applied on first `Campaign.Tick()`
- Encounter menu patches, abandon army blocks
- Prevents Linux/Proton crashes from early patching
- Appended to same log file

**Why Two Phases?**
Some patches require game systems to initialize first. Patching too early causes `TypeInitializationException` on Proton/Linux.

---

### Module Health & Installation Diagnostics

**Module Health Check** verifies file presence for all content types:

```
==============================
MODULE HEALTH CHECK
==============================

Dialogue System:
  Directory: C:\...\Modules\Enlisted\ModuleData\Enlisted\Dialogue
  ✓ qm_dialogue.json (3245 bytes)
  ✓ qm_gates.json (1876 bytes)
  ✓ qm_intro.json (2134 bytes)
  Result: OK - All dialogue files present

Events System:
  Directory: C:\...\Modules\Enlisted\ModuleData\Enlisted\Events
  ✓ camp_events.json (45678 bytes)
  ⚠ Warning: events_crisis.json missing (expected in directory)
  Result: DEGRADED - Some files missing

Localization:
  Directory: C:\...\Modules\Enlisted\ModuleData\Languages
  ✓ enlisted_strings.xml (234567 bytes)
  ✓ enlisted_qm_dialogue.xml (12345 bytes)
  Result: OK - All localization files present
```

**Runtime Catalog Status** confirms successful content loading:

```
==============================
RUNTIME CATALOG STATUS
==============================

EventCatalog:
  Status: Initialized
  Event Count: 68
  Result: OK

QMDialogueCatalog:
  Status: Initialized
  Node Count: 47
  Result: OK

DecisionCatalog:
  Status: Initialized
  Decision Count: 38
  Result: OK
```

**Patch Application Status** reports Harmony patching success:

```
==============================
PATCH APPLICATION STATUS
==============================

Harmony Instance: com.enlisted.mod
Total Methods Patched: 42
  Prefixes: 28
  Postfixes: 31
  Transpilers: 3
  Finalizers: 2
Result: SUCCESS - All patches applied
```

**Installation Path Detection** clarifies mod location:

```
==============================
ENVIRONMENT INFO
==============================

Game Root Path:     C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord
Mod DLL Path:       C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\3621116083\bin\Win64_Shipping_Client\Enlisted.dll
Mod Module Path:    C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\3621116083
Installation Type:  Steam Workshop
```

---

## Reading Conflict Logs

### Log Structure

```
========================================================================
              ENLISTED MOD - CONFLICT DIAGNOSTICS
========================================================================
Generated: 2025-12-23 14:30:15

-- ENVIRONMENT --
  Game Version:       1.3.13.xxxxx
  Enlisted Version:   v0.9.0
  Target Game:        1.3.13
  CLR Version:        4.0.30319.42000
  OS:                 Microsoft Windows NT 10.0.26200.0
  64-bit Process:     True

-- LOADED MODULES --
  Total: 15 modules
   1. SandBoxCore.ModuleData
   2. StoryMode.ModuleData
   3. Enlisted.Mod.Entry.SubModule <-- THIS MOD
   ... (other mods)

-- HARMONY PATCH CONFLICT ANALYSIS (MAIN PATCHES) --
  Enlisted patches (main patches): 45 methods

  [OK] NO CONFLICTS DETECTED
  
  All Enlisted patches are exclusive - no other mods are patching
  the same game methods. If you're having issues, the problem is
  likely not a mod conflict.

-- ENLISTED PATCH LIST (Main) --
  Total: 45 methods patched

  [Army/Party] (8)
    - MobileParty.SetMoveEscortParty
    - MobileParty.Tick
    ... (more)

  [Encounter] (12)
    - PlayerEncounter.Init
    - PlayerEncounter.Finish
    ... (more)

-- DEFERRED PATCHES (Applied on Campaign Start) --
  [Same format as main patches]

-- COMBINED CONFLICT SUMMARY --
  Total Enlisted patches: 53 methods
  Other mods sharing patches: 2

  Mods with shared patches (potential conflict sources):
    - some.other.mod
    - another.mod.id
```

### Conflict Example

```
-- HARMONY PATCH CONFLICT ANALYSIS (MAIN PATCHES) --
  [!] POTENTIAL CONFLICTS: 3

  Mod: some.economy.overhaul
    Shared patches: 2
      - Hero.ChangeHeroGold
      - Clan.AddRenown

  -- Patch Execution Order --
  Hero.ChangeHeroGold:
    Postfixes (run after original, lowest priority first):
      [800] com.enlisted.mod <- Enlisted
      [400] some.economy.overhaul
```

**What This Means:**
- Both Enlisted and the economy mod patch `Hero.ChangeHeroGold`
- Enlisted's postfix runs AFTER the other mod's (higher priority number)
- **Not necessarily a problem** - patches can coexist if they don't contradict
- **Possible issue** - if the other mod expects its changes to be final

### Categories

Patches are auto-categorized by type name:

| Category | Example Types | Purpose |
|----------|--------------|---------|
| Army/Party | `MobileParty`, `Army` | Party movement, escort, visibility |
| Encounter | `PlayerEncounter`, `MapEvent` | Battle/encounter state |
| Finance | `Clan`, `Hero`, `ChangeGold` | Gold/income modifications |
| UI/Menu | `GameMenu`, `Screen`, `Message` | Interface and menus |
| Combat | `Mission`, `Battle`, `Agent` | Combat mechanics |
| Kingdom/Clan | `Kingdom`, `Relation` | Diplomacy and faction |

---

## When to Share Logs

**Include `Conflicts-A_*.log` when reporting:**

✅ **Definite conflict issues:**
- Mod works alone but breaks with specific other mods
- UI elements don't appear
- Gold/equipment changes don't apply
- Encounter/battle state broken

✅ **General troubleshooting:**
- Crashes on startup
- Errors in game logs mentioning Enlisted
- Save game compatibility issues

✅ **Support requests:**
- Bug reports on Nexus/GitHub
- Help requests in Discord
- Compatibility questions

❌ **Not needed for:**
- Feature requests
- Content suggestions
- Documentation issues

**Where to find it:**
```
<Steam>\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\
```

### Summary: ModConflictDiagnostics

**What it IS:**
- ✅ Automatic runtime detection of Harmony patch conflicts
- ✅ Categorized list of all patches Enlisted applies
- ✅ Helps diagnose mod incompatibilities
- ✅ Useful for support/bug reports

**What it is NOT:**
- ❌ Does not detect content errors (malformed events, invalid JSON)
- ❌ Does not detect logic bugs (wrong calculations, broken features)
- ❌ Does not detect performance issues
- ❌ Does not need updates when we add features

**Does it need updates for new features (retinue, baggage train, etc.)?**
**NO** - The system automatically detects and categorizes new patches. It uses Bannerlord class name patterns for categorization, so new features are handled automatically.

**Code Location:** `src/Mod.Core/Logging/ModConflictDiagnostics.cs` (828 lines, well-commented)

---

# Part 2: Development Guidelines

**Note:** The sections below are for internal development use. They document how our systems interact and provide guidelines for avoiding conflicts when adding new features.

---

## Recent Architectural Changes (2026-01-03)

### Orchestrator Opportunity Unification

**Problem Solved:** Opportunities would disappear from the Camp Hub when the lord left a settlement mid-session, creating a jarring UX where content the player was about to interact with vanished.

**Root Cause:** Menu was regenerating opportunities on-demand based on current context, rather than using a locked schedule.

**Solution Implemented:**
- **ContentOrchestrator** now owns the opportunity lifecycle
- Opportunities pre-scheduled at 6am daily tick for all 4 phases (Dawn/Midday/Dusk/Night)
- Schedule **locked** for 24 hours once generated
- Menu queries `ContentOrchestrator.GetCurrentPhaseOpportunities()` (no cache, no regeneration)
- **CampOpportunityGenerator** role changed to candidate provider only
- Narrative hints generated from locked schedule for Daily Brief

**Impact on Development:**
- ✅ Opportunities persist when context changes (lord leaves, phase changes)
- ✅ Menu has no cache - single source of truth in Orchestrator
- ✅ Player can see hints about upcoming opportunities
- ✅ Quiet phases are intentional and communicated via hints
- ⚠️ **Breaking Change:** `CampOpportunityGenerator.GenerateCampLife()` now internal, only Orchestrator calls it
- ⚠️ **Deprecated:** `DecisionManager.GetAvailableOpportunities()` replaced by Orchestrator queries

**Related Documentation:** [Orchestrator Opportunity Unification Spec](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md)

### Bug Fixes Applied

**2026-01-03:**
- Decision tree now persists when lord leaves castle (uses locked schedule instead of regenerating)
- Decisions correctly disappear after selection (added fallback consumption by decision ID)

**2026-01-04:**
- Decisions no longer fire as popups (MapIncidentManager filters "decision" and "onboarding" categories)
- Deprecation warnings eliminated (GetUpcomingHints and GetCampActivityFlavor now use Orchestrator)

### New Systems Added

| System | Purpose | Integration Points |
|--------|---------|-------------------|
| **ContentOrchestrator** | Pre-schedules opportunities, provides world state analysis | Daily tick, menu queries, narrative hints |
| **CampOpportunityGenerator** | Generates opportunity candidates (no longer owns state) | Called by Orchestrator only |
| **WorldStateAnalyzer** | Analyzes lord situation, war stance, activity level | Used by Orchestrator, EventPacingManager, OrderProgressionBehavior |
| **BaggageTrainAvailability** | World-state-aware baggage access simulation | Integrated with Quartermaster, Orchestrator provides context |
| **RetinueManager** | Commander's retinue management (T7+) | Pay system, formation assignment, baggage access |
| **CompanySimulationBehavior** | Background camp life simulation | Daily tick, news feed integration |

**For Full System Analysis:** See [Systems Integration Analysis](../../ANEWFEATURE/systems-integration-analysis.md) for comprehensive analysis of all tracking systems (Supply, Morale, Reputation, Escalation) and how they integrate with ContentOrchestrator.

---

## System Integration Points

**Purpose:** Documents where our systems interact and potential conflict points during development.

**Development Note:** This section documents integration between our systems (retinue, baggage, quartermaster, etc.) to help developers avoid conflicts when adding features.

### System Integration Matrix

### EventSelector (Content Eligibility)

**File:** `src/Features/Content/EventSelector.cs`

**What It Does:**
- Filters events by one-time flags (skip if already fired)
- Checks cooldowns (skip if recently fired)
- Validates requirements via `EventRequirementChecker`
- Validates trigger conditions (flags, contexts, escalation)

**When It Runs:**
- Every time `EventPacingManager` wants to deliver an event
- Every time player opens Camp Hub (for decisions)

**Filtering Logic:**
```csharp
foreach (var evt in allEvents)
{
    // Skip decision events in automatic pacing
    if (evt.Category == "decision") continue;
    
    // Skip one-time events that already fired
    if (evt.Timing.OneTime && HasFired(evt.Id)) continue;
    
    // Skip events on cooldown
    if (IsOnCooldown(evt.Id, evt.Timing.CooldownDays)) continue;
    
    // Check requirements (tier, role, context, skills, etc.)
    if (!EventRequirementChecker.MeetsRequirements(evt.Requirements)) continue;
    
    // Check trigger conditions (flags, contexts, escalation)
    if (!CheckTriggerConditions(evt)) continue;
    
    candidates.Add(evt);
}
```

**What To Do:**
- Ensure events have specific enough requirements to avoid spam
- Use trigger conditions to make events mutually exclusive
- Set appropriate cooldowns (typically 7-30 days)

### EventRequirementChecker (Eligibility Validation)

**File:** `src/Features/Content/EventRequirementChecker.cs`

**What It Does:**
- Validates tier ranges (MinTier, MaxTier)
- Validates role requirements (Scout, Officer, etc.)
- Validates context (War, Peace, Siege, Battle, Town)
- Validates skill minimums
- Validates trait minimums
- Validates escalation thresholds
- Validates onboarding stage/track
- Validates HP requirements

**When It Runs:**
- Called by `EventSelector` during candidate filtering
- Called by `CampOpportunityGenerator` during candidate generation (for Orchestrator)
- **DEPRECATED:** Previously called by `DecisionManager` - now Orchestrator provides pre-filtered opportunities

**What To Do:**
- Use specific requirements to prevent overlapping events
- Avoid impossible combinations (e.g., MinTier=1, Role=Officer)
- Document requirement logic in event JSON comments
- **NEW:** Requirements checked once at daily scheduling time (6am), then locked for 24h

### Centralized State Managers

**Files:**
- `src/Features/Escalation/EscalationManager.cs` - Reputation and escalation
- `src/Features/Company/CompanyNeedsManager.cs` - Readiness, Morale, Supply
- `src/Features/Camp/FatigueManager.cs` - Rest and fatigue

**What They Do:**
- Enforce valid ranges (clamp to 0-100, -50 to +50, etc.)
- Log all state changes with category
- Prevent direct field modification
- Provide consistent API for all systems

**Pattern:**
```csharp
// ❌ WRONG: Direct modification
playerState.LordReputation += 10;

// ✅ CORRECT: Use manager
EscalationManager.ChangeLordRep(10, "Event: Successful patrol");
```

**What To Do:**
- Always use managers for state changes
- Include descriptive reason strings in logs
- Check manager logs when debugging state issues

### Encounter Safety Checks

**Files:** `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`, various patches

**What They Do:**
- Prevent menu activation during battles
- Block party state changes during encounters
- Detect siege-related battle types
- Handle edge cases (lord captured, player prisoner, etc.)

**Checks:**
```csharp
// Block menus during critical states
if (MobileParty.MainParty.MapEvent != null) return false;
if (PlayerEncounter.Current != null) return false;
if (Hero.MainHero.IsPrisoner) return false;
```

**What To Do:**
- Never assume player is in peaceful state
- Check for active battles before opening menus
- Defer state changes during encounters
- See [Encounter Safety](encounter-safety.md) for full guide

### Content Validation Tool

**File:** `Tools/Validation/validate_content.py` (enhanced validator)

**What It Does:**
- **Phase 1:** Structure validation (JSON schema, required fields, enum values)
- **Phase 2:** Reference validation (localization strings, skills, traits)
- **Phase 3:** Logical validation (impossible combinations, reasonable values)
- **Phase 4:** Consistency checks (flags, multi-stage events, priorities)

**Features:**
- Checks for duplicate event IDs across all files
- Validates option counts (2-4 range)
- Validates localization references against `enlisted_strings.xml`
- Detects impossible tier × role combinations
- Validates skill/trait names
- Checks cooldown reasonableness
- Tracks flag usage consistency
- Validates escalation thresholds
- **NEW (2026-01-03):** Validates opportunity hints (`hint`/`hintId` fields for narrative foreshadowing)

**What To Do:**
- Run before committing content changes: `python Tools/Validation/validate_content.py`
- Use `--strict` mode for pre-merge validation: `python Tools/Validation/validate_content.py --strict`
- See [../../../Tools/README.md](../../../Tools/README.md) for complete usage guide
- **NEW:** Add `hint` or `hintId` to camp opportunities for Daily Brief integration (see [Event System Schemas](../Content/event-system-schemas.md#narrative-hints-orchestrator-pre-scheduling))

**Legacy Tool:** `Tools/Validation/validate_events.py` (basic ID/option count checks, use enhanced tool instead)

---

## System Integration Points

This matrix shows where systems interact and potential conflict points.

### ContentOrchestrator × Content Systems

**MAJOR CHANGE (2026-01-03):** The ContentOrchestrator now **owns the opportunity lifecycle**. Opportunities are pre-scheduled 24 hours ahead, locked once generated, and provided via `GetCurrentPhaseOpportunities()`. This prevents jarring content disappearance when context changes mid-day.

| System | Integration Point | Conflict Risk | Resolution |
|--------|------------------|---------------|------------|
| ContentOrchestrator | Opportunity scheduling | Opportunities disappear when lord leaves castle | **FIXED:** Pre-schedule at 6am daily tick, lock schedule for 24h |
| ContentOrchestrator | Camp decisions menu | Stale opportunities shown | Menu queries Orchestrator directly, no cache |
| ContentOrchestrator | Daily Brief hints | Hints mismatch visible opportunities | Hints generated from same locked schedule |
| CampOpportunityGenerator | Candidate generation | Generator owns state vs Orchestrator owns state | **FIXED:** Generator is now candidate provider only |
| EventDeliveryManager | Decision/Event separation | Decisions firing as popups | Decisions filtered from automatic event selection |
| MapIncidentManager | Decision/Event separation | Decisions appearing in map incidents | **FIXED:** Filter "decision" and "onboarding" categories |

**Key Rule:** ContentOrchestrator owns all opportunity state. CampOpportunityGenerator only generates candidates on request.

**Related Documentation:** [Orchestrator Opportunity Unification Spec](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md)

### Enlistment × Equipment

| System | Integration Point | Conflict Risk | Resolution |
|--------|------------------|---------------|------------|
| Enlistment | Quartermaster access | Player could access QM after discharge | QM checks `IsEnlisted` before opening |
| Enlistment | Baggage train access | Player could access baggage after discharge | Baggage checks `IsEnlisted` |
| Enlistment | Provisions shop | Shop open during invalid states | Shop checks `IsEnlisted` AND `CanAccessBaggage()` |
| Discharge | Equipment cleanup | Player keeps issued gear | System tracks issued items, reclaims on discharge |

**Key Rule:** All equipment systems must check enlistment status before allowing access.

### Retinue × Company Systems

| System | Integration Point | Conflict Risk | Resolution |
|--------|------------------|---------------|------------|
| Retinue | Company Supply | Retinue affects supply needs | Supply calculation includes retinue size modifier |
| Retinue | Pay System | Retinue costs gold | Pay deduction includes retinue wages |
| Retinue | Baggage Train | Retinue requires baggage access | Retinue trickle blocked when baggage unavailable |
| Retinue | Formation Assignment | Retinue in player formation | Formation logic accounts for retinue units |

**Key Rule:** Retinue features only active for T7+ and check commander status.

### Quartermaster × Baggage Train

| System | Integration Point | Conflict Risk | Resolution |
|--------|------------------|---------------|------------|
| QM Equipment | Baggage availability | Sell/upgrade when baggage unavailable | QM checks `CanAccessBaggage()` for sells/upgrades |
| QM Provisions | Baggage availability | Buy food when baggage unavailable | Provisions requires `CanAccessBaggage()` |
| QM Dialogue | March state | QM mentions baggage but it's unavailable | Context-aware dialogue variants |
| QM Inventory | Muster refresh | Inventory refreshes during baggage-unavailable periods | Refresh only happens at valid baggage access times |

**Key Rule:** Quartermaster systems respect baggage train availability gates.

**World-State Integration (NEW 2026-01-03):** `ContentOrchestrator.RefreshBaggageSimulation()` provides world situation to `BaggageTrainManager` for dynamic probability calculation. Activity level, lord situation, war stance, and terrain all affect baggage catch-up/delay/raid chances.

**Related Documentation:** [Baggage Train Availability](../Equipment/baggage-train-availability.md)

### Events × State Systems

| System | Integration Point | Conflict Risk | Resolution |
|--------|------------------|---------------|------------|
| Events | Reputation changes | Multiple events modifying same track | Use `EscalationManager` for all changes |
| Events | Company Needs | Events affecting Readiness/Morale/Supply | Use `CompanyNeedsManager` for all changes |
| Events | Fatigue | Events awarding rest or adding fatigue | Use `FatigueManager` for all changes |
| Events | Gold | Events giving/taking gold | Use `GiveGoldAction` for all gold changes |
| Events | Equipment | Events giving/removing items | Use party `ItemRoster` methods with logging |

**Key Rule:** Events never modify state directly—always use managers and actions.

### Camp Hub × Automatic Events

| System | Integration Point | Conflict Risk | Resolution |
|--------|------------------|---------------|------------|
| Decisions | Event pacing | Player decision blocks automatic event | Decisions have priority (player-initiated) |
| Decisions | Cooldowns | Same content as decision and event | Use separate IDs, shared cooldown if needed |
| Camp Hub | Event popups | Menu closes when event fires | Events defer until player closes menu |
| Decisions | Requirements | Decision available but event can't fire | Decisions have lower requirements (more accessible) |

**Key Rule:** Player-initiated decisions take priority over automatic events. Event popups wait for menu closure.

**Orchestrator Integration (NEW 2026-01-03):** ContentOrchestrator pre-schedules opportunities at daily tick (6am), locks them for 24 hours, and provides them via `GetCurrentPhaseOpportunities()`. Menu queries Orchestrator directly with no cache. Consumed opportunities are marked and removed from display. This prevents opportunities from disappearing when context changes (e.g., lord leaves castle).

**Bug Fixes Applied:**
- 2026-01-03: Decision tree now persists when lord leaves castle (uses locked schedule)
- 2026-01-03: Decisions correctly disappear after selection (improved consumption)
- 2026-01-04: Decisions no longer fire as popups (filtered from MapIncidentManager)

**Related Documentation:** [Orchestrator Opportunity Unification](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md)

### Orders × Other Systems

| System | Integration Point | Conflict Risk | Resolution |
|--------|------------------|---------------|------------|
| Orders | Company Needs | Order completion affects needs | Use `CompanyNeedsManager` in completion handlers |
| Orders | Reputation | Order success/failure affects rep | Use `EscalationManager` in completion handlers |
| Orders | Events | Event fires during active order | Events can reference active order in triggers |
| Orders | Map Incidents | Incident fires during order | Incidents can check `ActiveOrderId` flag |

**Key Rule:** Orders are the primary gameplay driver—other systems should reference order state when relevant.

### Daily Tick Systems (Order Matters)

These systems all run on `Campaign.Tick()` - order matters:

1. **EnlistmentBehavior.OnTick()** - Updates enlistment state, detects battles, handles encounter safety
2. **ContentOrchestrator.OnDailyTick()** - **NEW (2026-01-03):** Pre-schedules opportunities for next 24 hours, locks schedule, generates narrative hints
3. **OrdersManager.OnDailyTick()** - Checks order progress, handles expiration
4. **CompanyNeedsManager.OnDailyTick()** - Updates Readiness/Morale/Supply decay
5. **FatigueManager.OnDailyTick()** - Updates fatigue from marching
6. **PayManager.OnWeeklyTick()** - Processes wage payments (weekly, not daily)
7. **RetinueManager.OnDailyTick()** - Processes retinue trickle, loyalty changes
8. **EventPacingManager.OnDailyTick()** - Considers delivering automatic events
9. **CompanySimulationBehavior.OnDailyTick()** - **NEW:** Background camp simulation, incidents

**Why Order Matters:**
- Enlistment check must run first (other systems check `IsEnlisted`)
- **Orchestrator schedules early** (provides world state to other systems)
- Order progress before events (events can reference order state)
- Company needs before events (events check need thresholds)
- Pay before retinue (retinue costs deducted from wages)
- **Orchestrator provides context** to baggage simulation, event pacing, order event frequency

---

## State Modification Priorities

When multiple systems want to modify the same state, these rules determine priority.

### Reputation & Escalation

**Managed By:** `EscalationManager`

**Tracks:**
- Lord Reputation (0-100)
- Officer Reputation (0-100)
- Soldier Reputation (-50 to +50)
- Scrutiny (0-10)
- Discipline (0-10)
- Medical Risk (0-5)
- Pay Tension (0-100)

**Priority Order (highest to lowest):**

1. **Direct Player Actions** (Camp Hub decisions, dialogue choices)
   - Immediate feedback to player choice
   - Example: "Request Audience" decision modifies Lord Rep

2. **Order Completion/Failure**
   - Core gameplay loop must have clear consequences
   - Example: Failed patrol order decreases Officer Rep

3. **Automatic Events**
   - Fire based on thresholds and context
   - Example: Escalation threshold event at Discipline 6

4. **Daily Decay/Passive Changes**
   - Gradual changes over time
   - Example: Soldier Rep slowly drifts toward 0

**Conflict Resolution:**
- All changes logged with source category
- Changes accumulate (not override) within same tick
- Managers clamp final value to valid range
- If conflicting changes occur, review logs to identify source

### Company Needs

**Managed By:** `CompanyNeedsManager`

**Tracks:**
- Readiness (0-100)
- Morale (0-100)
- Supply (0-100)
- Rest (0-100)

**Priority Order:**

1. **Order Requirements**
   - Orders can set minimum need values
   - Example: Scout order requires Readiness 50+

2. **Direct Player Actions**
   - Decisions affecting company needs
   - Example: "Share Extra Rations" decision increases Morale

3. **Battle/Major Events**
   - Significant state changes from major events
   - Example: Battle decreases Readiness and Equipment

4. **Daily Decay**
   - Gradual decline during march
   - Faster decline during forced march
   - Slower decline during rest

5. **Automatic Recovery**
   - Gradual increase when camped
   - Faster in towns/villages

**Conflict Resolution:**
- All changes accumulate within same tick
- Final value clamped to 0-100
- Critical thresholds (below 30) trigger warning events

**Orchestrator Integration (NEW 2026-01-03):**
- `WorldStateAnalyzer` reads Company Needs to determine `LifePhase` (Routine/Strained/Crisis)
- `SimulationPressureCalculator` tracks sustained pressure (days below threshold)
- Activity level affects opportunity budget (fewer opportunities when stressed)
- **Future Enhancement:** Gradient need influence on opportunity fitness scoring (see [Systems Integration Analysis](../../ANEWFEATURE/systems-integration-analysis.md))

### Gold (Player Treasury)

**Managed By:** `GiveGoldAction` (native Bannerlord)

**Sources:**
- Pay system (weekly wages)
- Event rewards
- Decision outcomes (gambling, side work)
- Order bonuses
- Selling equipment (quartermaster)

**Costs:**
- Retinue wages (weekly deduction)
- Equipment purchases (quartermaster)
- Provisions purchases (quartermaster)
- Event costs (bribes, gambling losses)
- Medical treatment costs

**Priority Order:**

1. **Mandatory Deductions** (retinue wages, issued equipment damage fees)
2. **Player Purchases** (equipment, provisions)
3. **Event Costs** (player chose the option)
4. **Pay Income** (weekly wage deposit)
5. **Event Rewards** (bonus income)

**Conflict Resolution:**
- All gold changes use `GiveGoldAction.ApplyBetweenCharacters()`
- Purchases blocked if insufficient funds
- Negative balance not allowed (purchase validation required)

### Fatigue

**Managed By:** `FatigueManager`

**Range:** 0-100 (0 = fully rested, 100 = exhausted)

**Sources of Increase:**
- Marching (daily increase based on speed)
- Battle participation
- Training decisions (weapon drill, sparring)
- Forced march (accelerated increase)
- Camp activities (some decisions cost fatigue)

**Sources of Decrease:**
- Rest decision (manual)
- Camped state (automatic recovery)
- Town rest (faster recovery)
- Medical treatment (side effect of recovery)

**Priority Order:**

1. **Critical States** (battle, forced march) - highest accumulation
2. **Player Rest Decisions** - immediate relief
3. **Daily Accumulation** - marching fatigue
4. **Passive Recovery** - camped rest
5. **Decision Costs** - training and activities

**Conflict Resolution:**
- Multiple fatigue sources accumulate within same tick
- Value clamped to 0-100
- High fatigue (70+) blocks some decisions
- Exhaustion (90+) triggers warning events

### Equipment (Player Loadout)

**Sources:**
- Issued gear (automatic at enlistment, promotions)
- Quartermaster purchases
- Quartermaster upgrades
- Event rewards (rare)
- Battlefield loot (via native game)

**Restrictions:**
- Tier gates (equipment locked by rank)
- Reputation gates (quartermaster sell access)
- Baggage train availability (affects purchases/sells)
- Gold availability (purchase validation)

**Priority Order:**

1. **Issued Equipment** - mandatory gear for rank/role
2. **Player Purchases** - via quartermaster
3. **Event Rewards** - rare equipment gifts
4. **Reclamation on Discharge** - issued items taken back

**Conflict Resolution:**
- System tracks issued vs purchased items
- Issued items automatically upgraded at promotions
- Players can't sell issued equipment
- Baggage availability gates all transactions

---

## Content Validation Rules

These rules help prevent content conflicts. Many are not yet enforced by tooling.

### Event/Decision Requirements

**Rule 1: No Impossible Tier × Role Combinations**

❌ **Invalid:**
```json
{
  "requirements": {
    "minTier": 1,
    "maxTier": 3,
    "role": "Officer"
  }
}
```
**Why:** Players can't be Officers at T1-T3 (Officers start at T5).

✅ **Valid:**
```json
{
  "requirements": {
    "minTier": 5,
    "role": "Officer"
  }
}
```

**Validation Check:**
- If `role == "Officer"` then `minTier >= 5`
- If `role == "NCO"` then `minTier >= 4`
- Role-specific events should have appropriate tier requirements

---

**Rule 2: Context Requirements Must Be Achievable**

❌ **Invalid:**
```json
{
  "requirements": {
    "context": "Battle"
  },
  "timing": {
    "cooldownDays": 1
  }
}
```
**Why:** Camp decisions can't fire during battles (Camp Hub unavailable during MapEvent).

✅ **Valid:**
```json
{
  "requirements": {
    "context": "Any"
  }
}
```

**Validation Check:**
- Camp Hub decisions (`dec_*`) must use `context: "Any"` or camp-appropriate contexts
- Battle-specific content should use map incidents, not decisions

---

**Rule 3: Skill Requirements Must Match Role**

❌ **Invalid:**
```json
{
  "requirements": {
    "role": "Engineer",
    "minSkills": {
      "Medicine": 50
    }
  }
}
```
**Why:** Engineers use Engineering skill, not Medicine.

✅ **Valid:**
```json
{
  "requirements": {
    "role": "Engineer",
    "minSkills": {
      "Engineering": 50
    }
  }
}
```

**Validation Check:**
- Cross-reference role definitions with skill requirements
- Warn about mismatches between role and required skills

---

**Rule 4: Escalation Triggers Must Match Track Type**

❌ **Invalid:**
```json
{
  "triggers": {
    "escalation_requirements": {
      "scrutiny": 5,
      "pay_tension_min": 85
    }
  }
}
```
**Why:** `scrutiny` is 0-10 scale, `pay_tension_min` is 0-100 scale. Using both in single event is suspicious.

✅ **Valid:**
```json
{
  "triggers": {
    "escalation_requirements": {
      "scrutiny": 5
    }
  }
}
```

**Validation Check:**
- Events should focus on one escalation track per event
- Combining multiple tracks should be intentional (multi-cause events)

---

**Rule 5: Cooldowns Must Be Reasonable**

❌ **Invalid:**
```json
{
  "id": "dec_rest",
  "timing": {
    "cooldownDays": 30
  }
}
```
**Why:** Rest is a core decision that should be available frequently.

✅ **Valid:**
```json
{
  "id": "dec_rest",
  "timing": {
    "cooldownDays": 1
  }
}
```

**Validation Guidelines:**
- Core decisions (rest, training): 1-2 days
- Social/economic decisions: 3-7 days
- Rare/special decisions: 14-30 days
- Major events: 30-60 days
- One-time events: No cooldown needed

---

**Rule 6: Mutually Exclusive Events Need Flags**

**Pattern:** Multi-stage events (player choice leads to follow-up)

✅ **Example:**
```json
{
  "id": "evt_mutiny_opportunity",
  "options": [
    {
      "id": "join_mutiny",
      "effects": {
        "setFlags": ["mutiny_joined"]
      }
    },
    {
      "id": "report_mutiny",
      "effects": {
        "setFlags": ["mutiny_reported"]
      }
    }
  ]
}
```

Then follow-up events:
```json
{
  "id": "evt_mutiny_result_joined",
  "triggers": {
    "all": ["has_flag:mutiny_joined"],
    "none": ["has_flag:mutiny_resolved"]
  }
}
```

**Validation Check:**
- Events that set flags should have follow-up events
- Follow-up events should check for flag AND set resolution flag
- No infinite loops (flag set but never cleared)

---

**Rule 7: HP Requirements Only for Medical Decisions**

❌ **Invalid:**
```json
{
  "id": "dec_challenge_duel",
  "requirements": {
    "hp_below": 50
  }
}
```
**Why:** HP requirements are for medical treatment, not general decisions.

✅ **Valid:**
```json
{
  "id": "dec_seek_treatment",
  "requirements": {
    "hp_below": 90
  }
}
```

**Validation Check:**
- `hp_below` should only appear in medical-themed events
- Regular decisions shouldn't gate on HP (use fatigue instead)

---

**Rule 8: Localization References Must Exist**

**Required Check:**
- Every `titleId` has matching entry in `enlisted_strings.xml`
- Every `setupId` has matching entry
- Every option `textId` has matching entry
- Every `resultTextId` has matching entry

**Validation Tool Enhancement:**
```python
# Load all string IDs from XML
xml_ids = load_enlisted_strings_ids()

# Check each event
for event in events:
    if event['titleId'] not in xml_ids:
        warn(f"Missing string: {event['titleId']}")
    if event['setupId'] not in xml_ids:
        warn(f"Missing string: {event['setupId']}")
    # Check options...
```

---

**Rule 9: Weighted Events Must Have Valid Priorities**

**Priority Ranges:**
- **Critical** (80-100): Escalation thresholds, crisis events
- **High** (60-79): Role-specific events, major story beats
- **Normal** (40-59): Standard events (default)
- **Low** (20-39): Flavor events, social interactions
- **Rare** (1-19): Easter eggs, rare occurrences

**Validation Check:**
- Events with `oneTime: true` should have high priority
- Threshold events should have priority 70+
- Universal flavor events should have priority 20-40

---

**Rule 10: Option Counts and Tooltips**

**Requirements:**
- Every event must have 2-4 options
- Every option must have a tooltip (tooltips cannot be null)
- Tooltips must be under 80 characters
- Tooltips must be factual, concise descriptions

**Validation:**
```python
for event in events:
    options = event.get('options', [])
    if not (2 <= len(options) <= 4):
        error(f"Event {event['id']} has {len(options)} options (need 2-4)")
    
    for opt in options:
        if not opt.get('tooltip'):
            error(f"Option {opt['id']} missing tooltip (tooltips cannot be null)")
        elif len(opt['tooltip']) > 80:
            warn(f"Option {opt['id']} tooltip too long ({len(opt['tooltip'])} chars)")
```

---

### Validation Tool Enhancements Needed

Current `validate_events.py` only checks duplicate IDs and option counts. It should be enhanced to check:

**Phase 1: Structure Validation**
- [ ] JSON schema validation (correct field names, types)
- [ ] Required fields present (id, category, titleId, setupId)
- [ ] Valid enum values (category, role, context, etc.)
- [ ] Option count (2-4 range)
- [ ] Tooltip presence and length

**Phase 2: Reference Validation**
- [ ] All string IDs exist in enlisted_strings.xml
- [ ] All skill names match Bannerlord skills
- [ ] All trait names match defined traits
- [ ] All flag references documented somewhere

**Phase 3: Logical Validation**
- [ ] No impossible tier × role combinations
- [ ] Reasonable cooldown values
- [ ] Context appropriate for delivery mechanism
- [ ] HP requirements only in medical events
- [ ] Escalation thresholds within valid ranges

**Phase 4: Consistency Checks**
- [ ] Flag-setting options have follow-up events
- [ ] Multi-stage events properly sequenced
- [ ] One-time events have appropriate priority
- [ ] Events don't overlap without clear priority

---

## Testing & Verification

### Integration Testing Checklist

When adding a new feature or modifying existing systems, test these integration points:

**Equipment Systems:**
- [ ] Quartermaster accessible only when enlisted
- [ ] Provisions shop respects baggage availability
- [ ] Equipment purchases blocked when insufficient gold
- [ ] Sell access respects reputation gates
- [ ] Tier gates enforced for equipment browsing
- [ ] Baggage train unavailable during forced march
- [ ] Inventory refresh only at valid muster times

**Retinue System (T7+):**
- [ ] Retinue only available for T7+ enlisted
- [ ] Trickle blocked when baggage unavailable
- [ ] Wages deducted from weekly pay
- [ ] Formation assignment includes retinue units
- [ ] Retinue events only fire for commanders
- [ ] Retinue cleared on discharge
- [ ] Named veterans appear after 3+ battles

**State Management:**
- [ ] Reputation changes clamped to valid ranges
- [ ] Company needs decay during march, recover during rest
- [ ] Fatigue accumulates during march, recovers during rest
- [ ] Gold changes always use GiveGoldAction
- [ ] State changes logged with source category

**Event Delivery:**
- [ ] Events respect cooldowns (check EscalationState)
- [ ] One-time events don't repeat
- [ ] Requirements properly filter candidates
- [ ] Triggers properly validate flags and contexts
- [ ] Event popups don't fire during battles
- [ ] Camp Hub decisions respect section visibility

**Orchestrator Integration (NEW):**
- [ ] Opportunities scheduled at daily tick (6am)
- [ ] Schedule locked for 24 hours
- [ ] Opportunities persist when context changes
- [ ] Consumed opportunities removed correctly
- [ ] Narrative hints appear in Daily Brief
- [ ] Decision-category events filtered from map incidents
- [ ] Menu queries Orchestrator directly (no cache)

**Encounter Safety:**
- [ ] Menus blocked during active battles
- [ ] Party state locked during encounters
- [ ] Discharge clears all encounter state
- [ ] Lord capture handled gracefully
- [ ] Player capture handled gracefully

### Manual Testing Scenarios

**Scenario 1: Forced March + Equipment Access**
1. Start forced march (baggage becomes unavailable)
2. Try to access quartermaster provisions
3. **Expected:** Provisions blocked, dialogue mentions march state
4. Try to sell equipment
5. **Expected:** Sell blocked if rep-gated, mention baggage unavailable
6. End forced march
7. **Expected:** Access restored

**Scenario 2: Multiple Reputation Changes**
1. Complete order (Officer Rep +10)
2. Fire automatic event with rep change (Lord Rep +5)
3. Make camp decision with rep cost (Soldier Rep -3)
4. **Expected:** All changes logged, final values clamped, no conflicts

**Scenario 3: Event Cooldown Enforcement**
1. Fire event with 7-day cooldown
2. Force time advance (debug command)
3. Attempt to fire same event at day 5
4. **Expected:** Event filtered out (on cooldown)
5. Advance to day 8
6. **Expected:** Event eligible again

**Scenario 4: Battle + Menu Interaction**
1. Lord enters battle
2. Try to open Camp Hub
3. **Expected:** Menu blocked, log shows battle conflict
4. Wait for battle to end
5. **Expected:** Menu access restored

**Scenario 5: Discharge + System Cleanup**
1. Build up retinue (T7+)
2. Have active order
3. Have flags set from events
4. Discharge from service
5. **Expected:** Retinue cleared, order cancelled, flags cleared, baggage/QM access blocked

**Scenario 6: Orchestrator Opportunity Persistence (NEW 2026-01-03)**
1. Wait for daily tick (6am) when opportunities are scheduled
2. Note opportunities visible in Camp Hub
3. Lord leaves castle (context change)
4. **Expected:** Same opportunities still visible (locked schedule)
5. Select and complete an opportunity
6. **Expected:** Opportunity disappears, others remain
7. Wait for phase change (e.g., Dawn → Midday)
8. **Expected:** New phase opportunities appear, old phase opportunities cleared
9. Check Daily Brief
10. **Expected:** Hints about upcoming opportunities visible in Company Reports

**Scenario 7: Decision/Event Category Separation (NEW 2026-01-04)**
1. Add test decision with `"category": "decision"` and `"context": "Any"`
2. Open Camp Hub
3. **Expected:** Decision appears in accordion
4. Wait for map incident trigger
5. **Expected:** Decision does NOT fire as popup (filtered from MapIncidentManager)
6. Check logs
7. **Expected:** No "Selected decision as incident" messages for decision-category events

---

## Adding New Systems

When adding a new feature that interacts with existing systems:

### 1. Document Integration Points

Add entry to [System Integration Points](#system-integration-points) matrix showing:
- What systems it interacts with
- What could conflict
- How conflicts are resolved

### 2. Define State Modification Rules

If your system modifies shared state (reputation, gold, needs, etc.):
- Use existing managers (never modify directly)
- Document priority relative to other systems
- Add to [State Modification Priorities](#state-modification-priorities)

### 3. Add Validation Rules

If your system adds new content types or requirements:
- Define validation rules
- Add examples of valid/invalid configurations
- Update validation tool if needed

### 4. Add Test Scenarios

Add integration test scenarios covering:
- Normal operation
- Edge cases (discharge, capture, battle)
- Conflict scenarios with other systems
- State persistence across save/load

### 5. Update Conflict Detection

If your system uses Harmony patches:
- Check `ModConflictDiagnostics` output after adding patches
- Document any expected conflicts
- Test with popular mods

If your system affects event eligibility:
- Document requirement interactions
- Add trigger conditions for mutual exclusion if needed
- Test event delivery with other active systems

### Example: Adding Baggage Train Availability

**Step 1: Integration Points**
```
Baggage Train × Quartermaster
  - Sell equipment requires baggage access
  - Provisions shop requires baggage access
  - Dialogue variants for unavailable state

Baggage Train × Retinue
  - Trickle reinforcements blocked when unavailable
  - Events mention baggage state

Baggage Train × Context
  - Unavailable during forced march
  - Unavailable far from army
  - Available at valid musters
```

**Step 2: State Access Rules**
```csharp
// All systems check availability before baggage access
if (!BaggageTrainAvailability.CanAccessBaggage(out string reason))
{
    // Block access, show reason
    return;
}
```

**Step 3: Validation Rules**
```
- Events that give equipment should check baggage availability
- Decisions that require baggage should have context requirements
- QM dialogue must have unavailable variants
```

**Step 4: Test Scenarios**
```
- Start forced march → expect baggage blocked
- Try QM provisions → expect blocked with reason
- End forced march → expect access restored
- Save/load during unavailable → expect state persists
```

**Step 5: Conflict Detection**
```
- No Harmony patches (uses existing systems)
- No new event requirements (uses existing context checks)
- Logs show availability checks with reasons
```

---

## Summary

### Quick Reference

**When adding content (events/decisions):**
1. Run `validate_events.py` before committing
2. Check requirements for impossible combinations
3. Add appropriate cooldowns
4. Use flags for multi-stage events
5. Add localization strings
6. **NEW:** Add `hint`/`hintId` for camp opportunities (narrative foreshadowing)
7. Test with existing events

**When adding features:**
1. Document integration points
2. Use existing managers for state changes
3. Add test scenarios
4. Check for Harmony conflicts
5. Update this document
6. **NEW:** Consider orchestrator integration (does it need pre-scheduling?)

**Orchestrator Rules (NEW 2026-01-03):**
1. Opportunities scheduled at 6am daily tick
2. Never regenerate opportunities during the day
3. Query `ContentOrchestrator.GetCurrentPhaseOpportunities()` (no cache)
4. Mark consumed with `ConsumeOpportunity()` or `ConsumeOpportunityByDecisionId()`
5. Decision-category events must NOT appear in MapIncidentManager

**When debugging conflicts:**
1. Check `Conflicts-A_*.log` for mod conflicts
2. Check `Session-A_*.log` for state change sources
3. Review integration matrix for affected systems
4. Test edge cases (battle, discharge, capture)

**Validation Priorities:**

1. **Critical** (blocks merge): Duplicate IDs, invalid JSON, mod conflicts
2. **High** (fix before release): Missing localizations, impossible requirements, broken cooldowns
3. **Medium** (fix soon): Overlapping events, suspicious skill checks, priority issues
4. **Low** (enhance later): Tooltip improvements, consistency polish, rare edge cases

---

## Error Codes Reference

### Error Codes

Surfaced error codes auto-generate at build time from `ModLogger.Surfaced(...)`
call sites. The complete live registry is in
[docs/error-codes.md](../../error-codes.md). For historical codes found in
pre-redesign session logs (format `E-<SUBSYSTEM>-NNN`), see the frozen
snapshot at [docs/error-codes-archive.md](../../error-codes-archive.md).

### System-level Error Codes

System-specific codes (muster, incidents, orchestrator, events, decisions)
are now auto-generated and listed in
[docs/error-codes.md](../../error-codes.md). Recovery behavior is documented
in the code at the `ModLogger.Surfaced(...)` call sites.

### General Patterns

All error codes follow the format `E-<CATEGORY>-<4hex>` where:
- `E` = Error
- `CATEGORY` = System identifier string literal (e.g. `MUSTER`, `INCIDENT`, `CAMP`)
- `4-hex suffix` = deterministic SHA-256 hash of the summary string; stable as long as the summary is unchanged

Errors are logged via `ModLogger.Surfaced()` which includes:
- Error code for searchability
- Human-readable message
- Full exception stack trace (when applicable)

---

## Related Documentation

### Core Architecture
- [BLUEPRINT.md](../../BLUEPRINT.md) - Core patterns and standards
- [Content System Architecture](../Content/content-system-architecture.md) - Event delivery pipeline
- [Orchestrator Opportunity Unification](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md) - **NEW:** Opportunity scheduling system
- [Systems Integration Analysis](../../ANEWFEATURE/systems-integration-analysis.md) - **NEW:** How all tracking systems integrate

### Content & Events
- [Event System Schemas](../Content/event-system-schemas.md) - JSON field definitions, narrative hints
- [Content Index](../Content/content-index.md) - All content organized by category
- [Camp Simulation System](../Campaign/camp-simulation-system.md) - Background simulation + opportunities

### Safety & Validation
- [Encounter Safety](encounter-safety.md) - Party state conflicts and edge cases
- [Content Validation Tools](../../Tools/README.md) - Validation scripts and usage

