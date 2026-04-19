# Camp Routine Schedule System

**Summary:** A default daily schedule system that gives the camp a predictable military routine. The camp follows a baseline schedule (dawn = formations, midday = work, dusk = social, night = rest) with world state causing deviations. Players can see the upcoming schedule and make commitments that override routine activities.

**Status:** ✅ Implemented  
**Last Updated:** 2026-01-01 (Reduced slot2 frequency - routine phases are now majority)  
**Implementation:** `src/Features/Camp/CampScheduleManager.cs`, `src/Features/Camp/CampRoutineProcessor.cs`, `src/Features/Content/ContentOrchestrator.cs`  
**Config Files:** `camp_schedule.json`, `routine_outcomes.json`, `orchestrator_overrides.json`  
**Related Docs:** [Camp Simulation System](camp-simulation-system.md), [Content System Architecture](../Content/content-system-architecture.md), [Event System Schemas](../Content/event-system-schemas.md)

**RECENT CHANGES (2026-01-01):**
- **Slot2 weights significantly reduced**: Dawn 0.5→0.2, Midday 0.6→0.3, Dusk 0.5→0.2, Night 0.3→0.1
- **Slot2 now skips during routine operations**: Added `"routine"`, `"quiet"`, `"marching"`, `"siege"` to skip conditions
- **Result**: ~4-5 routine outcomes per day instead of 8. Routine phases are now truly the majority
- **Flavor text now shows everywhere**: Full narrative text from `routine_outcomes.json` displays in combat log, news feed, and Recent Activity

---

## Index

1. [Overview](#overview)
2. [Baseline Schedule Definition](#baseline-schedule-definition)
3. [Schedule Generation Algorithm](#schedule-generation-algorithm)
4. [Integration with Existing Systems](#integration-with-existing-systems)
5. [Deviation Rules](#deviation-rules)
6. [Reversion to Baseline](#reversion-to-baseline)
7. [UI Integration](#ui-integration)
8. [Learning System Integration](#learning-system-integration)
9. [Implementation Plan](#implementation-plan)
10. [Questions Resolved](#questions-resolved)
11. [Files to Create/Modify](#files-to-createmodify)

---

## Overview

### The Problem

Currently the camp opportunity system is **reactive** - it generates what's valid NOW based on filters. This creates context-aware experiences but lacks the feel of a predictable military routine. Real military camps follow schedules: morning formations, afternoon work details, evening social time, night rest. Deviations from this routine signal something unusual.

### The Solution

Add a **baseline schedule layer** that:
1. Defines what activities the camp normally runs at each day phase
2. Shows this schedule to the player ("This morning: Weapon drill scheduled")
3. Deviates from baseline when world state requires it
4. Reverts to baseline when conditions normalize
5. Allows player commitments to override scheduled activities

### Core Philosophy

**The camp has a rhythm.** A well-run military company follows routine:
- Dawn: Formations, inspections, morning drill
- Midday: Work details, training, maintenance
- Dusk: Social time, relaxation, personal activities
- Night: Rest, sleep, quiet activities

**Context disrupts the rhythm.** Low supplies trigger foraging duty. Exhaustion cancels training. Siege conditions collapse into survival mode.

**Players observe and exploit the rhythm.** Knowing that training happens at dawn helps plan the day. Seeing the schedule deviate signals something is wrong.

---

## Baseline Schedule Definition

### Config File: `ModuleData/Enlisted/Config/camp_schedule.json`

```json
{
  "schemaVersion": 1,
  "phases": {
    "Dawn": {
      "slot1": {
        "category": "formation",
        "weight": 0.7,
        "description": "Morning formation and inspection",
        "skippedWhen": ["exhausted", "siege", "marching"]
      },
      "slot2": {
        "category": "training",
        "weight": 0.2,
        "description": "Early drill",
        "skippedWhen": ["exhausted", "marching", "siege", "routine", "quiet"]
      },
      "flavor": "The camp stirs to life. Sergeants bark orders."
    },
    "Midday": {
      "slot1": {
        "category": "training",
        "weight": 0.8,
        "description": "Combat training",
        "boostedWhen": ["active_campaign", "pre_battle"]
      },
      "slot2": {
        "category": "work",
        "weight": 0.3,
        "description": "Work details and maintenance",
        "skippedWhen": ["siege", "routine", "quiet", "marching"]
      },
      "flavor": "The sun climbs high. The serious work begins."
    },
    "Dusk": {
      "slot1": {
        "category": "social",
        "weight": 0.8,
        "description": "Evening leisure",
        "skippedWhen": ["high_scrutiny", "siege"]
      },
      "slot2": {
        "category": "economic",
        "weight": 0.2,
        "description": "Trading and gambling",
        "skippedWhen": ["high_scrutiny", "routine", "quiet", "siege", "marching"]
      },
      "flavor": "Work ends. Men gather around fires."
    },
    "Night": {
      "slot1": {
        "category": "recovery",
        "weight": 0.9,
        "description": "Rest and sleep",
        "boostedWhen": ["exhausted", "marching"]
      },
      "slot2": {
        "category": "special",
        "weight": 0.1,
        "description": "Night activities for those who can't sleep",
        "skippedWhen": ["exhausted", "routine", "quiet", "marching", "siege"]
      },
      "flavor": "The camp grows quiet. Torches gutter."
    }
  },
  "activityOverrides": {
    "Quiet": {
      "description": "Garrison routine - relaxed schedule",
      "modifiers": {
        "training": 0.8,
        "social": 1.3,
        "recovery": 1.2
      }
    },
    "Routine": {
      "description": "Standard operations - baseline schedule",
      "modifiers": {}
    },
    "Active": {
      "description": "Active campaign - extra training, less leisure",
      "modifiers": {
        "training": 1.5,
        "social": 0.6,
        "formation": 1.2
      }
    },
    "Intense": {
      "description": "Combat operations - minimal schedule",
      "modifiers": {
        "training": 0.3,
        "social": 0.2,
        "recovery": 1.5,
        "formation": 0.0
      }
    }
  },
  "pressureOverrides": {
    "low_supplies": {
      "threshold": 30,
      "effect": "boost_foraging",
      "description": "Foraging parties prioritized"
    },
    "high_scrutiny": {
      "threshold": 70,
      "effect": "restrict_leisure",
      "description": "Men on best behavior"
    },
    "exhausted": {
      "threshold": 30,
      "effect": "boost_recovery",
      "description": "Rest becomes priority"
    },
    "pre_battle": {
      "hoursBeforeBattle": 24,
      "effect": "combat_focus",
      "description": "Final preparations for battle"
    }
  }
}
```

### Category Mappings

Categories map to opportunity types:

| Category | Opportunity Types | Typical Activities |
|----------|-------------------|-------------------|
| formation | training, special | Morning roll call, inspections |
| training | training | Weapon drill, sparring, formation practice |
| work | economic, special | Repairs, maintenance, foraging |
| social | social | Card games, stories, singing, drinking |
| economic | economic | Trading, gambling, repair work |
| recovery | recovery | Rest, prayer, meditation |
| special | special | Officer audience, volunteering |

---

## Schedule Generation Algorithm

### Step 1: Load Baseline

```csharp
public ScheduledPhase GetBaselineForPhase(DayPhase phase)
{
    var baseline = _scheduleConfig.Phases[phase.ToString()];
    return new ScheduledPhase
    {
        Phase = phase,
        Slot1Category = baseline.Slot1.Category,
        Slot2Category = baseline.Slot2.Category,
        FlavorText = baseline.Flavor
    };
}
```

### Step 2: Apply Activity Level Modifiers

```csharp
public void ApplyActivityModifiers(ScheduledPhase schedule, ActivityLevel level)
{
    var overrides = _scheduleConfig.ActivityOverrides[level.ToString()];
    
    foreach (var mod in overrides.Modifiers)
    {
        if (mod.Value == 0)
        {
            // Skip this category entirely
            if (schedule.Slot1Category == mod.Key)
                schedule.Slot1Skipped = true;
            if (schedule.Slot2Category == mod.Key)
                schedule.Slot2Skipped = true;
        }
        else
        {
            // Adjust weight
            if (schedule.Slot1Category == mod.Key)
                schedule.Slot1Weight *= mod.Value;
            if (schedule.Slot2Category == mod.Key)
                schedule.Slot2Weight *= mod.Value;
        }
    }
}
```

### Step 3: Apply Pressure Overrides

```csharp
public void ApplyPressureOverrides(ScheduledPhase schedule, CompanyNeeds needs)
{
    // Check low supplies
    if (needs.Supply < _scheduleConfig.PressureOverrides["low_supplies"].Threshold)
    {
        schedule.BoostForaging = true;
        schedule.DeviationReason = "Foraging prioritized due to supply shortage";
    }
    
    // etc.
}
```

### Step 4: Check Player Commitments

```csharp
public void ApplyPlayerCommitments(ScheduledPhase schedule)
{
    var commitment = _commitments.GetCommitmentForPhase(schedule.Phase);
    if (commitment != null)
    {
        schedule.HasPlayerCommitment = true;
        schedule.PlayerCommitmentTitle = commitment.Title;
        // Player commitment takes precedence
    }
}
```

### Step 5: Generate Matching Opportunities

```csharp
public List<CampOpportunity> GenerateScheduledOpportunities(ScheduledPhase schedule)
{
    var opportunities = new List<CampOpportunity>();
    
    if (!schedule.Slot1Skipped && !schedule.HasPlayerCommitment)
    {
        var slot1Opp = SelectBestOpportunityForCategory(schedule.Slot1Category, schedule);
        if (slot1Opp != null)
        {
            slot1Opp.IsScheduled = true;
            slot1Opp.ScheduleLabel = schedule.Slot1Description;
            opportunities.Add(slot1Opp);
        }
    }
    
    // Similar for slot 2
    
    return opportunities;
}
```

---

## Integration with Existing Systems

### CampOpportunityGenerator Changes

```csharp
public List<CampOpportunity> GenerateCampLife()
{
    // ... existing context gathering ...
    
    // NEW: Get scheduled activities for current phase
    var scheduledPhase = _scheduleManager.GetScheduleForPhase(campContext.DayPhase);
    
    // NEW: Check for deviations
    _scheduleManager.ApplyDeviations(scheduledPhase, worldSituation, companyNeeds);
    
    // Generate candidates - now includes schedule boost
    var candidates = GenerateCandidates(worldSituation, campContext, playerPrefs);
    
    // NEW: Boost scheduled category opportunities
    foreach (var candidate in candidates)
    {
        if (IsScheduledCategory(candidate.Type, scheduledPhase))
        {
            candidate.ScheduleBoost = ScheduledActivityBoost; // e.g., 1.5x
            candidate.IsScheduled = true;
        }
    }
    
    // Score and select
    var selected = SelectTopN(candidates, budget);
    
    // NEW: Ensure at least one scheduled activity if available
    EnsureScheduledActivityPresent(selected, candidates, scheduledPhase);
    
    return selected;
}
```

### UI Display Changes

The Decisions menu should show the current schedule:

```
┌─────────────────────────────────────────┐
│ CAMP LIFE - Midday                      │
│                                         │
│ ═══ ROUTINE ═══                         │
│ Combat training in progress             │
│ Work details available                  │
│                                         │
│ ═══ OPPORTUNITIES ═══                   │
│ • Weapon Drill [Scheduled] ⭐           │
│ • Equipment Maintenance                 │
│ • Card Game (Dusk)                      │
└─────────────────────────────────────────┘
```

When there's a deviation:

```
┌─────────────────────────────────────────┐
│ CAMP LIFE - Dawn                        │
│                                         │
│ ═══ ROUTINE DISRUPTED ═══               │
│ Morning formation cancelled             │
│ (Morale too low for formations)         │
│                                         │
│ ═══ OPPORTUNITIES ═══                   │
│ • Rest in Tent                          │
│ • Help the Wounded                      │
│ • Prayer Service                        │
└─────────────────────────────────────────┘
```

### Forecast Display

Future phases can be previewed:

```csharp
public List<ScheduleForecast> GetDayForecast()
{
    var forecasts = new List<ScheduleForecast>();
    var currentPhase = GetCurrentPhase();
    
    foreach (DayPhase phase in Enum.GetValues(typeof(DayPhase)))
    {
        if (phase <= currentPhase) continue; // Already passed
        
        var schedule = GetScheduleForPhase(phase);
        ApplyDeviations(schedule, _worldSituation, _companyNeeds);
        
        forecasts.Add(new ScheduleForecast
        {
            Phase = phase,
            PrimaryActivity = schedule.Slot1Skipped ? null : schedule.Slot1Description,
            IsDeviation = schedule.HasDeviation,
            DeviationReason = schedule.DeviationReason,
            PlayerCommitment = schedule.PlayerCommitmentTitle
        });
    }
    
    return forecasts;
}
```

UI display:

```
═══ TODAY'S SCHEDULE ═══
Dawn (now): Weapon drill in progress
Midday:     Work details, maintenance
Dusk:       Evening leisure [You: Card Game]
Night:      Rest period
```

---

## Schedule Manager Implementation

### New Class: `CampScheduleManager`

```csharp
/// <summary>
/// Manages the daily camp routine schedule. Provides baseline schedules for each day phase
/// and calculates deviations based on world state and company pressure.
/// </summary>
public class CampScheduleManager
{
    private const string LogCategory = "CampSchedule";
    
    private ScheduleConfig _config;
    private ScheduledPhase _currentSchedule;
    
    public static CampScheduleManager Instance { get; private set; }
    
    /// <summary>
    /// Gets the baseline schedule for a day phase, with deviations applied.
    /// </summary>
    public ScheduledPhase GetScheduleForPhase(DayPhase phase)
    {
        var baseline = GetBaselineForPhase(phase);
        ApplyActivityModifiers(baseline, GetCurrentActivityLevel());
        ApplyPressureOverrides(baseline, GetCompanyNeeds());
        ApplyPlayerCommitments(baseline);
        return baseline;
    }
    
    /// <summary>
    /// Checks if the current phase has deviated from baseline routine.
    /// </summary>
    public bool IsRoutineDeviation()
    {
        return _currentSchedule?.HasDeviation ?? false;
    }
    
    /// <summary>
    /// Gets the reason for current deviation, if any.
    /// </summary>
    public string GetDeviationReason()
    {
        return _currentSchedule?.DeviationReason;
    }
    
    /// <summary>
    /// Gets forecast for remaining day phases.
    /// </summary>
    public List<ScheduleForecast> GetDayForecast() { ... }
    
    /// <summary>
    /// Called when phase changes to update current schedule.
    /// </summary>
    public void OnPhaseChanged(DayPhase newPhase)
    {
        _currentSchedule = GetScheduleForPhase(newPhase);
        
        if (_currentSchedule.HasDeviation)
        {
            ModLogger.Info(LogCategory, 
                $"Schedule deviation for {newPhase}: {_currentSchedule.DeviationReason}");
        }
    }
}
```

### Data Models

```csharp
/// <summary>
/// Represents the scheduled activities for a single day phase.
/// </summary>
public class ScheduledPhase
{
    public DayPhase Phase { get; set; }
    
    // Slot 1 (primary activity)
    public string Slot1Category { get; set; }
    public string Slot1Description { get; set; }
    public float Slot1Weight { get; set; } = 1.0f;
    public bool Slot1Skipped { get; set; }
    
    // Slot 2 (secondary activity)
    public string Slot2Category { get; set; }
    public string Slot2Description { get; set; }
    public float Slot2Weight { get; set; } = 1.0f;
    public bool Slot2Skipped { get; set; }
    
    // Deviation tracking
    public bool HasDeviation => Slot1Skipped || Slot2Skipped || !string.IsNullOrEmpty(DeviationReason);
    public string DeviationReason { get; set; }
    
    // Player commitment
    public bool HasPlayerCommitment { get; set; }
    public string PlayerCommitmentTitle { get; set; }
    
    // Flavor
    public string FlavorText { get; set; }
}

/// <summary>
/// Forecast entry for UI display.
/// </summary>
public class ScheduleForecast
{
    public DayPhase Phase { get; set; }
    public string PhaseName => Phase.ToString();
    public string PrimaryActivity { get; set; }
    public bool IsDeviation { get; set; }
    public string DeviationReason { get; set; }
    public string PlayerCommitment { get; set; }
    public bool HasPlayerCommitment => !string.IsNullOrEmpty(PlayerCommitment);
    public bool IsCurrent { get; set; }
}
```

---

## Deviation Rules

### Activity Level Effects

| Activity Level | Training | Social | Formation | Recovery |
|---------------|----------|--------|-----------|----------|
| Quiet | 0.8x | 1.3x | 0.8x | 1.2x |
| Routine | 1.0x | 1.0x | 1.0x | 1.0x |
| Active | 1.5x | 0.6x | 1.2x | 0.8x |
| Intense | 0.3x | 0.2x | 0.0x | 1.5x |

### Pressure Effects

| Condition | Threshold | Effect |
|-----------|-----------|--------|
|| Exhausted (Rest <30%) | <30% | Skip training, boost recovery |
| Low Supplies | <30% | Boost foraging opportunities |
| High Scrutiny | >70% | Restrict leisure/gambling |
| Exhausted (Rest <30%) | <30% | Skip training, boost rest |
| Pre-Battle | 24h before | Combat training focus |
| Siege | Any | Collapse to survival mode |

### Lord Situation Effects

| Situation | Schedule Modification |
|-----------|----------------------|
| PeacetimeGarrison | Full schedule, extra leisure |
| WarMarching | Morning/evening only, skip midday |
| WarActiveCampaign | Training focused, reduced social |
| SiegeAttacking | Minimal schedule, work focus |
| SiegeDefending | Emergency only, survival mode |

---

## Reversion to Baseline

The schedule automatically reverts to baseline when conditions normalize:

```csharp
private void CheckBaselineReversion()
{
    var needs = GetCompanyNeeds();
    var previousDeviations = _activeDeviations.ToList();
    
    foreach (var deviation in previousDeviations)
    {
        bool shouldRevert = deviation.Type switch
        {
            "low_supplies" => needs.Supply >= 40, // Above threshold + buffer
            "exhausted" => needs.Rest >= 40,
            _ => false
        };
        
        if (shouldRevert)
        {
            _activeDeviations.Remove(deviation);
            
            // Notify player
            NewsReporter.AddNews(
                "Camp routine returning to normal",
                $"{deviation.Description} has resolved. Schedule resuming.",
                NewsCategory.CampMood);
        }
    }
}
```

---

## UI Integration

### Schedule Panel in Camp Hub

Add a small schedule panel to the Camp Hub screen:

```xml
<!-- GUI/Prefabs/Camp/SchedulePanel.xml -->
<Widget>
    <Children>
        <TextWidget Text="@ScheduleHeader" />
        <ListPanel DataSource="{ScheduleItems}">
            <ItemTemplate>
                <ScheduleEntryWidget 
                    PhaseName="@PhaseName"
                    Activity="@PrimaryActivity"
                    IsDeviation="@IsDeviation"
                    IsCurrent="@IsCurrent"
                    HasCommitment="@HasPlayerCommitment" />
            </ItemTemplate>
        </ListPanel>
    </Children>
</Widget>
```

### Localization Strings

```xml
<!-- In enlisted_strings.xml -->
<string id="schedule_header" text="Today's Routine" />
<string id="schedule_routine_normal" text="Normal routine" />
<string id="schedule_deviation" text="Routine disrupted: {REASON}" />
<string id="schedule_formation" text="Morning formation and inspection" />
<string id="schedule_training" text="Combat training" />
<string id="schedule_work" text="Work details and maintenance" />
<string id="schedule_social" text="Evening leisure" />
<string id="schedule_rest" text="Rest period" />
<string id="schedule_cancelled" text="Cancelled" />
<string id="schedule_player_committed" text="You: {ACTIVITY}" />
```

---

## Learning System Integration

The schedule system works alongside the existing learning system:

1. **Schedule provides context** - "Training is scheduled at midday" boosts training opportunity fitness
2. **Learning personalizes within schedule** - If player ignores weapon drill but engages sparring, sparring gets boosted within training category
3. **70/30 split maintained** - Schedule becomes part of the 70% fitness, learning remains 30%

```csharp
float CalculateFitness(CampOpportunity opp, ScheduledPhase schedule, ...)
{
    float score = opp.BaseFitness;
    
    // Schedule boost (part of fitness layer)
    if (IsScheduledCategory(opp.Type, schedule))
    {
        score *= 1.3f; // Scheduled activities more likely
    }
    
    // Apply 70/30 split
    float fitness = score * 0.7f;
    float learning = _history.GetEngagementRate(opp.Id) * 0.3f;
    
    return fitness + learning;
}
```

---

## Implementation Plan

### Phase 1: Core Schedule System
1. Create `CampScheduleManager` class
2. Create `camp_schedule.json` config file
3. Add schedule data models
4. Integrate with `CampOpportunityGenerator`

### Phase 2: Deviation Logic
1. Implement activity level modifiers
2. Implement pressure overrides
3. Add reversion detection
4. Add news feed integration

### Phase 3: UI Integration
1. Add schedule panel to Camp Hub
2. Add forecast display
3. Add deviation indicators
4. Add localization strings

### Phase 4: Polish
1. Test all deviation scenarios
2. Balance schedule weights
3. Add schedule-related news
4. Documentation

---

## Orchestrator Integration & Automatic Routine Processing

### Overview

The camp routine schedule forms the baseline, but the **ContentOrchestrator** serves as the "brain" that can:
1. **Override the schedule** when company needs are critical (foraging when supplies low, extended rest when exhausted)
2. **Inject variety** periodically to break up monotony (patrol duty, scouting assignments)
3. **Process routine automatically** at phase boundaries, generating dynamic outcomes without player interaction

### Architecture Flow

```
Phase Transition
    ↓
ContentOrchestrator.CheckForScheduleOverride()
    ↓
┌─────────────┬──────────────┬─────────────┐
│ Need-Based? │ Variety Due? │ Use Baseline│
└─────────────┴──────────────┴─────────────┘
    ↓               ↓               ↓
Foraging Duty   Patrol Duty    Normal Schedule
    └───────────────┴───────────────┘
                    ↓
        CampScheduleManager.ApplyOverride()
                    ↓
        CampRoutineProcessor.ProcessPhaseTransition()
                    ↓
            Roll Outcome Quality
          (Excellent/Good/Normal/Poor/Mishap)
                    ↓
        Apply XP, Resources, Conditions
                    ↓
    ┌───────────────┴────────────────┐
Combat Log Message          News Feed Item
```

### Need-Based Overrides

The orchestrator monitors company needs and injects urgent activities when thresholds are crossed:

| Condition | Threshold | Override Activity | Effect |
|-----------|-----------|-------------------|--------|
| Supplies < 30 | Critical | Foraging Duty | Replaces training, generates supplies |
| Supplies < 15 | Emergency | Emergency Foraging | All phases, maximum effort |
| Rest < 20 | Exhausted | Extended Rest | Half-day rest, skip training |
| Readiness < 40 | Low | Emergency Drill | Extra training, fatigue cost |
| Morale < 25 | Dangerous | Light Duty | Easy work, morale recovery |

**Config:** `ModuleData/Enlisted/Config/orchestrator_overrides.json`

```json
{
  "needBasedOverrides": {
    "low_supplies": {
      "trigger": {
        "need": "supplies",
        "threshold": 30,
        "comparison": "lessThan"
      },
      "override": {
        "category": "foraging",
        "name": "Foraging Duty",
        "reason": "Supplies critical",
        "priority": 100,
        "addressesNeed": "supplies",
        "replaceBothSlots": true,
        "affectedPhases": ["Midday"]
      },
      "activationText": "No training today. Entire company on foraging duty.",
      "recoveryThreshold": 50
    }
  }
}
```

### Variety Injections

To prevent monotony, the orchestrator periodically (every 3-5 days) injects special assignments:

- **Patrol Duty** - Perimeter patrols (Dawn/Midday)
- **Scouting Assignment** - Area reconnaissance (Dawn)
- **Guard Rotation** - Night watch duty (Night)
- **Supply Escort** - Wagon escort duty (Midday)
- **Camp Fortification** - Defensive work (Midday)
- **Equipment Inspection** - Full gear check (Dawn)
- **Messenger Duty** - Dispatch carrying (Dawn/Midday)

**Selection:** Weighted random based on phase preference and variety settings.

**Frequency:** Configured via `varietySettings`:
```json
{
  "varietySettings": {
    "minDaysBetweenInjections": 3,
    "maxDaysBetweenInjections": 5,
    "injectionChancePerDay": 0.35,
    "maxInjectionsPerWeek": 2,
    "skipDuringIntense": true,
    "skipDuringSiege": true
  }
}
```

### Automatic Routine Processing

At each phase boundary, `CampRoutineProcessor` automatically processes scheduled activities:

#### 1. Activity Processing

For each non-skipped slot in the schedule:
- Determine activity category (training, foraging, patrol, etc.)
- Roll outcome quality based on player state
- Calculate XP, resources, and effects
- Check for mishap conditions

#### 2. Outcome Quality System

Outcomes are rolled using weighted probabilities:

| Outcome | Default Weight | Description | XP Modifier |
|---------|----------------|-------------|-------------|
| Excellent | 10% | Exceptional performance | +50% |
| Good | 25% | Above average | +20% |
| Normal | 40% | Standard result | +0% |
| Poor | 18% | Below average | -50% |
| Mishap | 7% | Something went wrong | -80% |

**Weight Sets:**
- `default` - Normal conditions
- `highSkill` - Player skilled in activity (more excellent/good)
- `fatigued` - Player exhausted (more poor/mishap)
- `lowSupply` - Company supply low (more poor outcomes)

#### 3. Outcome Effects

Each activity has configurable effect ranges per outcome type:

**Example: Training Activity**
```json
{
  "training": {
    "name": "Combat Training",
    "skill": "OneHanded",
    "xpRanges": {
      "excellent": { "min": 18, "max": 28 },
      "good": { "min": 12, "max": 18 },
      "normal": { "min": 8, "max": 12 },
      "poor": { "min": 3, "max": 7 },
      "mishap": { "min": 0, "max": 3 }
    },
    "fatigueChange": 12,
    "mishapCondition": "minor_injury",
    "mishapChance": 0.4
  }
}
```

**Applied Effects:**
- **XP** → Hero.AddSkillXp() for appropriate skill
- **Fatigue** → CompanyNeeds.Rest modified (inverted)
- **Gold** → Hero.ChangeHeroGold() for found items
- **Supply** → CompanyNeeds.Supply for foraging
- **Conditions** → Applied via condition system (injuries, illness)

#### 4. Player Feedback

**Combat Log Messages:**
```
[Green] Sharp focus today. Movements feel natural. (+22 OneHanded XP)
[Yellow] Sluggish today. The heat didn't help. (+5 OneHanded XP)
[Red] Twisted ankle during drill. (minor_injury)
```

Colors indicate outcome quality:
- Excellent → Bright Green (#44AA44)
- Good → Light Green (#88CC88)
- Normal → Light Gray (#CCCCCC)
- Poor → Yellow (#CCCC44)
- Mishap → Red (#CC4444)

**News Feed Integration:**
```
Combat Training: Good progress
Foraging Duty: Excellent results (Supplies critical)
```

### Config Files

**Three configuration files work together:**

1. **`camp_schedule.json`** - Baseline schedules and deviation rules
2. **`routine_outcomes.json`** - Activity outcome tables and flavor text
3. **`orchestrator_overrides.json`** - Need triggers and variety pool

See [Event System Schemas](../Content/event-system-schemas.md#camp-routine-configs) for full schema definitions.

### Implementation Details

**Key Classes:**
- `ContentOrchestrator` - Override decision logic
- `OrchestratorOverride` - Override data model
- `CampScheduleManager` - Schedule generation with override application
- `CampRoutineProcessor` - Automatic activity processing and outcome rolling
- `RoutineOutcome` - Outcome data model
- `EnlistedNewsBehavior.AddRoutineOutcome()` - News integration

**Execution Points:**
- `ContentOrchestrator.OnDayPhaseChanged()` - Phase transition detected
- `CampOpportunityGenerator.OnPhaseChanged()` - Calls routine processor
- `CampOpportunityGenerator.ProcessCompletedPhaseRoutine()` - Processes previous phase

**Player Control:**
- Player commitments still override automatic routine
- "Scheduled" tag appears on matching opportunities
- Routine processes only when player has no commitment for that phase

---

## Questions Resolved

### Should baseline schedule be in config JSON or hardcoded?
**Config JSON.** Matches existing patterns, allows tweaking without code changes.

### How should world state modifiers work?
**Two layers:**
1. ActivityLevel-based category weight modifiers (garrison = more social)
2. Pressure threshold triggers (exhausted = skip training)

### Should schedule show in UI forecast?
**Yes.** Helps player plan their day, adds immersion.

### How do player commitments interact with baseline schedule?
**Player commitments override.** If you commit to sparring at midday, the routine training slot becomes your sparring session.

### Should orders override the camp schedule or coexist?
**Orders take priority.** If on duty, schedule opportunities are filtered by order compatibility (existing system). Schedule runs in parallel for non-duty soldiers.

---

## RECENT CHANGES

**2026-01-01:**
- **Sea-Aware Flavor Text:** Added `seaVariants` to all routine activities in `routine_outcomes.json` for immersive naval travel narratives (deck drills, fishing, hammock rest, lookout duty, etc.)
- **Travel Context Detection:** `CampRoutineProcessor.GetFlavorText()` now checks if party is at sea using native `IsCurrentlyAtSea` property and selects appropriate variant
- **Slot2 Frequency Reduction:** Reduced slot2 weights dramatically (Dawn 0.5→0.2, Midday 0.6→0.3, Dusk 0.5→0.2, Night 0.3→0.1) to reduce routine outcome spam
- **Skip Conditions Expanded:** Added `"routine"`, `"quiet"`, `"marching"`, `"siege"` to slot2 `skippedWhen` arrays to prevent slot2 during common/peaceful world states
- **Expected Impact:** Routine outcomes reduced from ~8/day to 4-5/day, with slot2 only firing in special situations
- **Flavor Text Implementation:** Modified `RoutineOutcome.GetNewsSummary()` to return full `FlavorText` for display in news feeds and recent activity, providing immersive narratives in combat logs, camp summaries, and player status

---

## Files Created/Modified

**New Files:**
- `src/Features/Camp/CampScheduleManager.cs` - Schedule management and override application
- `src/Features/Camp/CampRoutineProcessor.cs` - Automatic routine processing and outcome rolling
- `src/Features/Camp/Models/ScheduledPhase.cs` - Schedule data model
- `src/Features/Camp/Models/ScheduleForecast.cs` - Forecast UI model
- `src/Features/Camp/Models/RoutineOutcome.cs` - Routine outcome data model
- `src/Features/Content/OrchestratorOverride.cs` - Override data model
- `ModuleData/Enlisted/Config/camp_schedule.json` - Schedule definitions
- `ModuleData/Enlisted/Config/routine_outcomes.json` - Outcome tables and flavor text
- `ModuleData/Enlisted/Config/orchestrator_overrides.json` - Override triggers and variety pool

**Modified Files:**
- `src/Features/Content/ContentOrchestrator.cs` - Added override system methods
- `src/Features/Camp/CampOpportunityGenerator.cs` - Integrate schedule boost and routine processing
- `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs` - Added routine outcome integration
- `ModuleData/Languages/enlisted_strings.xml` - Added routine and override localization
- `Enlisted.csproj` - Added new source files

---

**Last Updated:** 2026-01-01  
**Status:** 🔵 Design Complete
