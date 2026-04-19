# Camp Simulation System: Living Military Company

**Summary:** The Camp Simulation System creates a living, breathing military company through two integrated layers: Background Simulation (autonomous company life that runs automatically) and Camp Opportunities (player-facing activities generated contextually). Together they make the camp feel real - things happen whether you engage or not, and you can choose when and how to participate in camp life.

**Status:** ✅ Implemented  
**Last Updated:** 2026-01-03 (Bug fixes: opportunities persist when lord leaves, decisions correctly disappear after selection)  
**Implementation:** `src/Features/Camp/CompanySimulationBehavior.cs`, `src/Features/Camp/CampOpportunityGenerator.cs`  
**Related Docs:** [Content System Architecture](../Content/content-system-architecture.md), [News Reporting System](../UI/news-reporting-system.md), [Company Needs](../Core/company-needs.md), [Camp Routine Schedule](camp-routine-schedule-spec.md), [Orchestrator Spec](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md)

---

## Index

**Overview:**
1. [System Overview](#system-overview)
2. [Core Philosophy](#core-philosophy)
3. [System Integration](#system-integration)

**Background Simulation (Autonomous):**
4. [Background Simulation Systems](#background-simulation-systems)
5. [Company Roster Tracking](#company-roster-tracking)
6. [Daily Tick System](#daily-tick-system)
7. [Background Incidents](#background-incidents)
8. [Pulse Events & Cascading Pressure](#pulse-events--cascading-pressure)

**Camp Opportunities (Player-Facing):**
9. [Camp Opportunity Generation](#camp-opportunity-generation)
10. [Intelligence Layers](#intelligence-layers)
11. [Opportunity Types](#opportunity-types)
12. [Fitness Scoring](#fitness-scoring)
13. [Learning System](#learning-system)

**Integration & Technical:**
14. [Orchestrator Integration](#orchestrator-integration)
15. [News Feed Integration](#news-feed-integration)
16. [Data Models](#data-models)
17. [Configuration](#configuration)
18. [Examples](#examples)

---

## System Overview

### The Two Layers

The Camp Simulation System consists of two complementary layers that work together to create a living military company:

| Layer | Purpose | Player Interaction | Implementation |
|-------|---------|-------------------|----------------|
| **Background Simulation** | Autonomous company life - soldiers get sick, equipment degrades, morale shifts | Observe via news feed | CompanySimulationBehavior |
| **Camp Opportunities** | Pre-scheduled activities player can engage with | Choose to participate or ignore | ContentOrchestrator + CampOpportunityGenerator |

**How They Work Together:**

```
Daily Tick (6am)
    ↓
┌─────────────────────────────────────┐
│   BACKGROUND SIMULATION             │
│   • Soldiers get sick/recover       │
│   • Equipment degrades              │
│   • Incidents occur                 │
│   • Pressure builds/releases        │
└────────────┬────────────────────────┘
             │
             ├──→ News Feed (what happened)
             ├──→ Pressure Tracking (simulation stress)
             └──→ World State (current conditions)
                      ↓
         ┌────────────────────────────┐
         │  CONTENT ORCHESTRATOR      │
         │  • Reads world state       │
         │  • PRE-SCHEDULES opportunities │
         │    for next 24 hours       │
         │  • LOCKS schedule (stable) │
         │  • Generates narrative hints │
         └───────────┬────────────────┘
                     ↓
         ┌────────────────────────────┐
         │  CAMP OPPORTUNITY GEN      │
         │  (Candidate Generator)     │
         │  • Provides candidates     │
         │  • Scores fitness          │
         │  • Orchestrator selects    │
         └───────────┬────────────────┘
                     ↓
         _scheduledOpportunities[phase] = LOCKED
                     ↓
         Daily Brief shows hints:
         Camp Rumors: "Torgan mentioned a card game tonight." (lavender)
         Your Status: "Your condition needs attention." (default)
                     ↓
         Player opens DECISIONS menu
                     ↓
         Shows LOCKED opportunities for current phase
         (persist until consumed or phase ends)
```

**Key Design Principle:** Opportunities are scheduled once at daily tick and **locked**. Context changes (lord leaving castle, phase transitions) do NOT regenerate opportunities. This prevents jarring disappearance of content the player was about to interact with.

**Architecture Reference:** See [Orchestrator Opportunity Unification Spec](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md) for full details.

---

## Core Philosophy

### You Are One Soldier in a Company

**The camp has its own life.** Things happen whether you act or not:
- Soldiers get sick, injured, or desert
- Equipment breaks, gets stolen, or wears out
- Morale rises and falls based on conditions
- Small incidents occur constantly
- The company consumes supplies, accumulates fatigue, deals with problems

### Two Modes of Engagement

**1. Observe (Background Simulation)**
- You see what happened through the news feed
- "Private Jonas fell ill overnight"
- "Equipment theft in the baggage train"
- "Three men deserted during the march"
- You don't control it - you observe it

**2. Participate (Camp Opportunities)**
- Camp generates activities you can engage with
- Training session starting, card game forming, comrade needs help
- You choose when and how to participate
- Or you ignore it and take your own initiative

### Not a Management Game

**You don't command the company.** You're one soldier among many:
- You can't order training drills (but you can join them)
- You can't prevent desertions (but you can help with morale)
- You can't stop equipment theft (but you can be vigilant)
- You experience camp life, not control it

**Stories emerge from living this life, not from managing systems.**

---

## System Integration

### How Background Simulation Feeds Orchestrator

```csharp
// ContentOrchestrator reads simulation state
var simulation = CompanySimulationBehavior.Instance;

// Roster health affects event selection
float casualtyRate = simulation.Roster.CasualtyRate;
bool hasManySick = simulation.Roster.SickCount > 5;
bool hasDesertions = simulation.Pressure.RecentDesertions > 0;

// Pressure affects urgency
bool supplyPressure = simulation.Pressure.DaysLowSupplies > 2;
bool moralePressure = simulation.Pressure.DaysLowMorale > 2;

// Use in world state for opportunity generation
worldState["company_stressed"] = supplyPressure || moralePressure;
worldState["high_casualties"] = casualtyRate > 0.2f;
worldState["recent_desertions"] = hasDesertions;
```

### What Orchestrator Does With This

**Event Selection:**
- High casualties → medical opportunities more likely
- Recent desertions → discipline/morale events available
- Supply pressure → foraging/scavenging options appear

**Frequency Adjustment:**
- Stressed company → fewer opportunities (men need rest)
- Healthy company → normal opportunity generation
- Crisis conditions → critical events only

**Crisis Response:**
- When pressure hits threshold → orchestrator queues mandatory crisis event
- Background simulation detects → orchestrator delivers → player must respond

---

## Background Simulation Systems

### Daily Tick Flow

Six systems run each dawn (6am), in order:

```
Daily Tick (6am)
│
├── 1. CONSUMPTION
│   └── Supplies consumed, equipment used
│
├── 2. ROSTER UPDATES
│   └── Sick recover/worsen, wounded heal, deserters leave
│
├── 3. CONDITION CHECKS
│   └── New sickness, injuries, desertion attempts
│
├── 4. INCIDENT ROLLS
│   └── Random camp incidents (0-2 per day)
│
├── 5. PULSE EVALUATION
│   └── Did morale/supplies/rest cross a threshold?
│
└── 6. NEWS GENERATION
    └── Push all results to news feed
```

### 1. Daily Consumption

**Supplies:**
- Base rate: 1.0 supply/day per 10 soldiers
- Modifiers: Marching +20%, Siege +30%, Winter +25%
- High morale reduces waste by 10%
- Low morale increases waste by 15%

**Equipment Degradation:**
- 2% chance per day per soldier that gear degrades
- Higher in combat (5% during active campaign)
- Weather increases risk (rain +3%, snow +5%)

**Fatigue Accumulation:**
- Marching: +15 fatigue/day
- Siege work: +20 fatigue/day
- Garrison duty: -10 fatigue/day (recovery)

### 2. Roster Updates

**Sick Soldiers:**
- Mild illness: 70% recover after 3 days
- Serious illness: 40% recover after 7 days, 10% worsen
- Critical illness: 20% recover after 14 days, 30% die

**Wounded Soldiers:**
- Light wounds: 80% heal after 5 days
- Moderate wounds: 50% heal after 10 days, 20% worsen
- Serious wounds: 30% heal after 20 days, 15% die

**Deserters:**
- Leave at dawn if they decided to desert
- Roster updated, loss reported to news feed
- Impacts company atmosphere

### 3. Condition Checks (New Issues)

**Sickness Rolls:**
- Base chance: 2% per soldier per day
- Modifiers:
  - Low supplies: +3%
  - Siege: +4%
  - Winter: +2%
  - Recent battles: +2%
  - Crowded (>100 soldiers): +1%

**Injury Rolls (Non-Combat):**
- Base chance: 1% per soldier per day
- Modifiers:
  - Heavy labor: +2%
  - Poor equipment: +1%
  - Rushing (forced march): +3%

**Desertion Attempts:**
- Base chance: 0.5% per soldier per day
- Modifiers:
  - Low supply (<30): +4%
  - Unpaid (backpay owed): +3%
  - High discipline (>70): -2%
  - Recent defeat: +4%
  - In friendly territory: -1%

### 4. Background Incidents

Random camp events that don't require player input:

**Common Incidents (60% chance):**
- Card game turned into argument
- Equipment found missing
- Soldier caught stealing rations
- Minor brawl between men
- Rumors spreading through camp

**Rare Incidents (15% chance):**
- Serious fight requiring intervention
- Equipment theft discovered
- Soldier found drunk on duty
- Desertion attempt foiled

**0-2 incidents roll per day based on company size and stress level.**

---

## Company Roster Tracking

### Roster Data Model

```csharp
public class CompanyRoster
{
    // Tracked Soldiers
    public List<TrackedSoldier> Soldiers { get; set; }
    
    // Current Counts
    public int HealthyCount { get; set; }
    public int SickCount { get; set; }
    public int WoundedCount { get; set; }
    public int DeadCount { get; set; }  // Total deaths this enlistment
    public int DesertedCount { get; set; }  // Total desertions
    
    // Rates (for pressure calculation)
    public float CasualtyRate => (DeadCount + DesertedCount) / (float)Math.Max(1, StartingCount);
    public float SickRate => SickCount / (float)Math.Max(1, HealthyCount + SickCount);
    
    // History
    public int StartingCount { get; set; }
    public CampaignTime EnlistmentStart { get; set; }
}

public class TrackedSoldier
{
    public string Id { get; set; }
    public string Name { get; set; }
    public SoldierCondition Condition { get; set; }
    public int DaysSick { get; set; }
    public int DaysWounded { get; set; }
    public SoldierTroop TroopType { get; set; }
}
```

### Soldier Conditions

| Condition | Description | Recovery Time | Mortality Risk |
|-----------|-------------|---------------|----------------|
| Healthy | Normal, fit for duty | N/A | 0% |
| MildIllness | Cold, minor ailment | 3 days | 0% |
| SeriousIllness | Fever, infection | 7 days | 5% |
| CriticalIllness | Severe disease | 14 days | 30% |
| LightWound | Scratches, bruises | 5 days | 0% |
| ModerateWound | Deep cuts, fractures | 10 days | 5% |
| SeriousWound | Major trauma | 20 days | 15% |

---

## Pulse Events & Cascading Pressure

### Pulse Events

When company needs cross critical thresholds, pulse events fire:

**Supply Crisis (Supply < 20%):**
- News: "Supplies critically low. Men are hungry."
- Effect: Readiness penalty, desertion risk +5%
- Opportunity: "Forage for supplies" becomes highly weighted

**Exhaustion Crisis (Rest < 15%):**
- News: "Men are exhausted. Performance suffering."
- Effect: Combat penalties, injury risk +3%
- Opportunity: "Push for rest day" appears

### Cascading Pressure

Pressure cascades from one system to another:

```
Low Supplies → Men hungry → Discipline issues → Desertions increase
    ↓              ↓              ↓              ↓
Foraging      Brawls start   Scrutiny      Roster shrinks
needed        over food      rises      
    ↓              ↓              ↓              ↓
More          Injuries      More          Combat
incidents     occur         pressure      weakness
```

**Pressure Tracking:**

```csharp
public class CompanyPressure
{
    // Pressure Sources
    public int DaysLowSupplies { get; set; }  // Consecutive days <30%
    public int DaysLowRest { get; set; }      // Consecutive days <40%
    public int DaysHighScrutiny { get; set; } // Consecutive days >70%
    public int RecentDeaths { get; set; }     // Deaths in last 7 days
    public int RecentDesertions { get; set; } // Desertions in last 7 days
    
    // Total Pressure (0-100)
    public int TotalPressure => CalculateTotal();
    
    private int CalculateTotal()
    {
        int pressure = 0;
        pressure += DaysLowSupplies * 5;      // +5 per day
        pressure += DaysLowRest * 4;          // +4 per day
        pressure += DaysHighScrutiny * 3;     // +3 per day
        pressure += RecentDeaths * 8;         // +8 per death
        pressure += RecentDesertions * 6;     // +6 per desertion
        return Math.Min(100, pressure);
    }
}
```

---

## Camp Opportunity Generation

### Intelligence Layers

When generating camp opportunities, the system analyzes four layers:

**Layer 1: World State (Macro Context)**
- Lord situation (garrison, campaign, siege)
- Kingdom war status (peace, active war, desperate)
- Recent world events (battles, sieges)
- Strategic context (offensive, defensive, recovery)

**Layer 2: Camp Context (Meso Context)**
- Day Phase (Dawn, Midday, Dusk, Night)
- Day of week (pay coming? muster soon?)
- Recent camp activity (training yesterday? fight last night?)
- Current orders status (who's on duty? who's off?)
- Camp mood (celebration? mourning? tense?)

**Layer 3: Player State (Micro Context)**
- Physical condition (fatigue, injury, health)
- Economic state (gold, debts, needs)
- Social standing (reputation levels)
- Recent actions (what did player do yesterday?)
- Pending matters (owe debt? promise made?)

**Layer 4: Opportunity History (Meta Context)**
- When was this type last offered?
- Did player engage or ignore it?
- What does player tend to choose?
- Variety maintenance (don't repeat too much)
- Novelty injection (introduce new things periodically)

### Scheduling Algorithm (Orchestrator-Owned)

Opportunities are scheduled once per day by the ContentOrchestrator, not generated on-demand:

```csharp
// ContentOrchestrator.ScheduleOpportunities() - called at 6am daily tick
private void ScheduleOpportunities()
{
    var currentDay = (int)CampaignTime.Now.ToDays;
    if (_scheduledDay == currentDay) return; // Once per day
    
    _scheduledDay = currentDay;
    _scheduledOpportunities = new Dictionary<DayPhase, List<ScheduledOpportunity>>();
    
    var worldSituation = WorldStateAnalyzer.AnalyzeSituation();
    
    // Schedule for each phase in next 24 hours
    foreach (DayPhase phase in Enum.GetValues(typeof(DayPhase)))
    {
        // Step 1: Predict context for this phase
        var predictedContext = PredictContextForPhase(phase, worldSituation);
        
        // Step 2: Get budget for this phase
        int budget = generator.DetermineOpportunityBudget(worldSituation, predictedContext);
        if (budget <= 0) continue;
        
        // Step 3: Generate candidates (via CampOpportunityGenerator)
        var candidates = generator.GenerateCandidatesForPhase(phase, worldSituation, predictedContext);
        
        // Step 4: Score and select
        var selected = candidates
            .OrderByDescending(c => c.FitnessScore)
            .Take(budget)
            .Select(c => new ScheduledOpportunity { ... })
            .ToList();
        
        // Step 5: LOCK - stored until consumed or day ends
        _scheduledOpportunities[phase] = selected;
    }
    
    // Step 6: Generate narrative hints for Company Reports
    BuildNarrativeHints();
}

// Menu just displays locked opportunities - no regeneration
public IReadOnlyList<ScheduledOpportunity> GetCurrentPhaseOpportunities()
{
    var currentPhase = WorldStateAnalyzer.GetCurrentDayPhase();
    return _scheduledOpportunities[currentPhase].Where(o => !o.Consumed).ToList();
}
```

**Key Change:** `CampOpportunityGenerator.GenerateCampLife()` is deprecated. The generator now provides candidates via `GenerateCandidatesForPhase()`, but the **Orchestrator owns the lifecycle** (scheduling, locking, consumption tracking).

---

## Opportunity Types

### The 36 Opportunities (Complete List)

**Training (5):**
- `opp_weapon_drill` - Weapon training session → dec_training_drill
- `opp_sparring` - Practice bout → dec_training_spar
- `opp_formation_drill` - Formation practice → dec_training_formation
- `opp_veteran_advice` - Learn from veteran → dec_training_veteran
- `opp_archery_practice` - Archery session → dec_training_archery

**Social (7):**
- `opp_card_game` - Card game forming → dec_gamble_cards
- `opp_campfire_stories` - Stories around fire → dec_social_stories
- `opp_tavern_visit` - Drinks with soldiers → dec_tavern_drink
- `opp_storytelling` - Tell a tale → dec_social_storytelling
- `opp_drinking_contest` - Drinking competition → dec_drinking_contest
- `opp_singing` - Join the singing → dec_social_singing
- `opp_arm_wrestling` - Arm wrestling match → dec_arm_wrestling

**Economic (5):**
- `opp_dice_game` - Dice game → dec_gamble_dice
- `opp_forage` - Gather supplies → dec_forage
- `opp_camp_repairs` - Help with repairs → dec_work_repairs
- `opp_maintain_gear` - Equipment maintenance → dec_maintain_gear (EXISTS)
- `opp_trade_browse` - Browse trade goods → dec_trade_browse

**Recovery (5):**
- `opp_rest` - Get some sleep → dec_rest_sleep
- `opp_help_wounded` - Assist wounded → dec_help_wounded
- `opp_prayer` - Quiet prayer → dec_prayer
- `opp_short_rest` - Brief rest break → dec_rest_short
- `opp_meditate` - Meditate on events → dec_meditate

**Special (7):**
- `opp_officer_meeting` - Request audience → dec_officer_audience
- `opp_baggage_access` - Visit baggage train → dec_baggage_access (bypasses popup, routes to stash/QM)
- `opp_mentor_recruit` - Help train recruit → dec_mentor_recruit
- `opp_volunteer_duty` - Volunteer for extra duty → dec_volunteer_extra
- `opp_night_patrol` - Volunteer for patrol → dec_night_patrol
- `opp_write_letter` - Write a letter → dec_write_letter (EXISTS)
- `opp_high_stakes_gamble` - High stakes game → dec_gamble_high (EXISTS)

### Opportunity Data Model

```json
{
  "id": "opp_weapon_drill",
  "displayName": "Weapon Training",
  "description": "Sergeant is running drills. Join the session?",
  "targetDecision": "dec_training_drill",
  "category": "training",
  "timeOfDay": ["Dawn", "Midday"],
  "orderCompatibility": {
    "compatible": ["guard", "patrol"],
    "incompatible": ["scout", "courier"]
  },
  "detection": {
    "lordSituations": ["InGarrison", "InCampaign"],
    "minFatigue": 0,
    "maxFatigue": 70,
    "requiresOfficer": false
  },
  "fitness": {
    "baseWeight": 1.0,
    "worldStateModifiers": {
      "peacetime_garrison": 1.5,
      "war_active_campaign": 0.8
    },
    "playerStateModifiers": {
      "low_skill": 1.3,
      "high_fatigue": 0.5
    }
  }
}
```

---

## Fitness Scoring

### How Fitness Works

Each opportunity gets a fitness score (0.0-10.0) based on how well it matches current context:

```csharp
public float CalculateFitness(CampOpportunity opp, 
                              WorldSituation world,
                              CampContext camp,
                              PlayerState player,
                              OpportunityHistory history)
{
    float score = opp.BaseWeight;  // Start with base (typically 1.0)
    
    // World state modifiers
    score *= GetWorldStateModifier(opp, world);
    
    // Time-of-day modifier
    if (!opp.ValidTimeOfDay.Contains(camp.CurrentPhase))
        return 0.0f;  // Invalid time = no show
    
    // Player state modifiers
    score *= GetPlayerStateModifier(opp, player);
    
    // Learning system (70/30 split)
    score *= 0.7f;  // 70% fitness
    score += (history.GetPlayerEngagement(opp) * 0.3f);  // 30% learning
    
    // Variety penalty (recently shown?)
    if (history.LastShown(opp) < 2)  // Shown in last 2 days
        score *= 0.3f;
    
    // Novelty boost (never shown before?)
    if (history.NeverShown(opp))
        score *= 1.2f;
    
    return Math.Min(10.0f, score);
}
```

### Example Scoring

**Scenario:** Garrison duty, morning, player is fatigued but low on gold

```
opp_weapon_drill:
  Base: 1.0
  × World (garrison morning): 1.5
  × Player (high fatigue): 0.5
  × Learning (player always ignores): 0.3
  = 0.225 (low fitness, unlikely to show)

opp_rest:
  Base: 1.0
  × World (garrison quiet): 1.2
  × Player (high fatigue): 2.0
  × Learning (player often chooses): 1.4
  = 3.36 (high fitness, likely to show)

opp_card_game:
  Base: 1.0
  × World (garrison social time): 1.3
  × Player (low gold need): 1.5
  × Learning (player sometimes tries): 1.0
  = 1.95 (moderate fitness)
```

Result: Shows "Rest" and "Card Game", skips "Weapon Drill"

---

## Learning System

### 70/30 Split

Opportunity selection uses a **70% fitness / 30% learning** split:

- **70% Fitness:** How well does this fit the current situation?
- **30% Learning:** How much does player engage with this type?

**Why This Works:**
- Fitness ensures contextual appropriateness (no sparring during siege)
- Learning ensures player sees content they enjoy
- 70/30 balance prevents over-personalization (variety maintained)

### Engagement Tracking

```csharp
public class OpportunityHistory
{
    // Per-opportunity tracking
    Dictionary<string, OpportunityRecord> _records;
    
    public float GetPlayerEngagement(string oppId)
    {
        var record = _records[oppId];
        
        float presentedCount = record.TimesPresented;
        float engagedCount = record.TimesEngaged;
        float ignoredCount = record.TimesIgnored;
        
        // Engagement rate: 0.0 (never engaged) to 1.0 (always engaged)
        return engagedCount / Math.Max(1, presentedCount);
    }
    
    public void RecordPresentation(string oppId)
    {
        _records[oppId].TimesPresented++;
        _records[oppId].LastPresented = CampaignTime.Now;
    }
    
    public void RecordEngagement(string oppId, bool engaged)
    {
        if (engaged)
            _records[oppId].TimesEngaged++;
        else
            _records[oppId].TimesIgnored++;
    }
}
```

### Learning Evolution

Over time, the system learns patterns:

**Week 1:** Player ignores all training, always chooses social/economic
- Learning weight still low (few samples)
- Fitness dominates selection

**Week 4:** Clear pattern established
- Player engagement: Training 0.1, Social 0.8, Economic 0.9
- Learning weight increases selection of social/economic
- But fitness still ensures contextual variety

**Week 12:** Mature profile
- System knows player preferences well
- Still shows occasional training (novelty, variety)
- But heavily weights social/economic opportunities

---

## Orchestrator Integration

### Orchestrator Owns Opportunity Lifecycle

The ContentOrchestrator now **owns** the opportunity lifecycle, using CampOpportunityGenerator as a candidate provider:

**Orchestrator Responsibilities:**
- Schedules opportunities once per day (6am tick)
- Locks schedule - no regeneration on context changes
- Tracks consumption (player interacted)
- Provides narrative hints for Company Reports
- Exposes `GetCurrentPhaseOpportunities()` to menu

**CampOpportunityGenerator Responsibilities:**
- Provides `GenerateCandidatesForPhase()` for candidate generation
- Performs fitness scoring
- Tracks engagement history for learning
- Does NOT own the cached state (Orchestrator does)

### Analysis Components

**WorldStateAnalyzer:**
- LordSituation (garrison/campaign/siege)
- WarStance (peace/active/desperate)
- ActivityLevel (quiet/routine/active/intense)
- DayPhase (dawn/midday/dusk/night)

**SimulationPressureCalculator:**
- TotalPressure (0-100)
- PressureSources (supplies/rest/scrutiny/casualties)
- PressureReleases (pay/victory/rest)

**PlayerBehaviorTracker:**
- Player preferences (combat vs social, risky vs safe)
- Recent behavior patterns
- Engagement history

### Scheduling Flow

```csharp
// ContentOrchestrator.ScheduleOpportunities() - called at 6am

// Determine budget per phase based on predicted context
int budget = (worldState.LordIs, phase) switch
{
    (LordSituation.PeacetimeGarrison, DayPhase.Dawn) => 3,
    (LordSituation.PeacetimeGarrison, DayPhase.Dusk) => 3,
    (LordSituation.WarMarching, DayPhase.Dusk) => 2,
    (LordSituation.WarMarching, DayPhase.Night) => 0,  // Quiet time
    (LordSituation.SiegeDefending, _) => 0,            // No opportunities
    _ => 2
};

// Get candidates from generator
var candidates = CampOpportunityGenerator.Instance
    .GenerateCandidatesForPhase(phase, worldState, predictedContext);

// Score, select, and LOCK
_scheduledOpportunities[phase] = SelectAndLock(candidates, budget);
var candidates = GenerateCandidates(worldState, pressure);
```

### Orchestrator Override System

**Added:** 2025-12-31 - The orchestrator now actively manages the camp routine schedule.

**Schedule Overrides:**
The ContentOrchestrator can override the baseline camp schedule when company needs are critical:
- Supply < 30 → Foraging Duty (replaces training)
- Rest < 20 → Extended Rest (skips formations)
- Readiness < 40 → Emergency Drill (extra training)

**Variety Injections:**
Periodically (every 3-5 days), the orchestrator injects special assignments to break monotony:
- Patrol Duty, Scouting Assignment, Guard Rotation, etc.

**Automatic Routine Processing:**
At phase boundaries, `CampRoutineProcessor` automatically processes scheduled activities and generates outcomes with dynamic rolls (Excellent/Good/Normal/Poor/Mishap), applying XP, resources, and conditions without player interaction.

**Integration:**
```csharp
// At phase transition
var override = ContentOrchestrator.Instance.CheckForScheduleOverride(phase);
if (override != null)
{
    // Apply foraging/rest/drill override
    CampScheduleManager.Instance.ApplyOverride(override);
}

// Process completed phase routine
CampRoutineProcessor.ProcessPhaseTransition(completedPhase, schedule);
// → Generates combat log messages
// → Adds news feed entries
// → Applies XP, gold, supply changes
```

**Related Docs:** [Camp Routine Schedule](camp-routine-schedule-spec.md), [Content System Architecture](../Content/content-system-architecture.md#camp-routine-orchestration)

---

## News Feed Integration

### What Gets Reported

**Background Simulation News:**
- Soldier sickness/recovery
- Equipment issues
- Desertion attempts
- Camp incidents
- Resource consumption warnings

**Camp Opportunity News:**
- Activities that occurred
- Social events player participated in
- Training results
- Economic outcomes

### News Categories

```csharp
public enum NewsCategory
{
    CompanyRoster,     // Sick, wounded, dead, deserted
    Equipment,         // Degradation, theft, loss
    Supplies,          // Consumption, shortages
    Incidents,         // Random camp events
    PlayerActivity,    // What player did
    CampMood,          // Camp mood, atmosphere
    Orders            // Order-related news
}
```

### Example News Feed

```
[ROSTER] Private Jonas fell ill overnight. (2 hours ago)
[SUPPLIES] Supplies running low. 4 days remaining. (3 hours ago)
[INCIDENT] Equipment found missing from baggage. (4 hours ago)
[PLAYER] You trained with the veterans this morning. (+15 One Handed XP) (6 hours ago)
[MOOD] Men are in good spirits after the victory. (8 hours ago)
[ORDERS] Guard duty assignment completed successfully. (10 hours ago)
```

---

## Decision Scheduling System (Phase 9)

**Status:** ✅ Implemented (2025-12-31)  
**Implementation:** `src/Features/Camp/CampOpportunityGenerator.cs`, `src/Features/Camp/Models/PlayerCommitments.cs`

### Overview

Players can schedule camp opportunities for specific day phases instead of only doing them immediately. This creates anticipation and allows planning around duties.

### How It Works

**Opportunity Scheduling:**
1. Player sees an opportunity (e.g., "sparring match")
2. Can choose to act immediately OR schedule for a specific phase
3. Commitment tracked with phase, day, target decision, display text
4. Shows in AHEAD section with countdown: `"Sparring match at midday (3h)"`
5. When phase arrives, decision fires automatically as popup
6. Removed from commitments after firing

**Day Phases:**
- **Dawn** (6am) - Morning drills, prayers, morning duties
- **Midday** (12pm) - Work details, repairs, training
- **Dusk** (6pm) - Social events, card games, drinking
- **Night** (12am) - Risky activities, sneaking, gambling

**Implementation Details:**
```csharp
// PlayerCommitments.cs
public class ScheduledCommitment
{
    public string OpportunityId { get; set; }
    public string TargetDecision { get; set; }
    public string ScheduledPhase { get; set; }   // Dawn/Midday/Dusk/Night
    public int ScheduledDay { get; set; }
    public string DisplayText { get; set; }
}

// CampOpportunityGenerator.cs
public bool IsCommittedTo(string opportunityId) { ... }
public ScheduledCommitment GetNextCommitment() { ... }
public float GetHoursUntilCommitment(ScheduledCommitment c) { ... }
```

**UI Integration:**
- `ForecastGenerator.BuildNowText()` shows upcoming commitments with countdown
- `CampScheduleManager.ApplyPlayerCommitments()` marks them on schedule
- Phase transition detection fires commitments automatically

**Opportunity Configuration:**
```json
{
  "id": "opp_card_game",
  "scheduledPhase": "Dusk",
  "validPhases": ["Dusk", "Night"],
  "baseFitness": 50
}
```

**Phase-Aware Scheduling (2026-01-04):**
- Orchestrator generates candidates for each phase independently using `GenerateCandidatesForPhase(phase)`
- Each opportunity appears only ONCE per day in its first valid phase
- Opportunities with multiple valid phases (e.g., `["Dusk", "Night"]`) scheduled for earliest phase only
- All opportunities compete on fitness score (no "immediate" bypass)

**Commitment Model:**
- Future-phase opportunities: Click to commit → greys out → auto-fires at phase
- Current/past-phase opportunities: Click to fire immediately
- Uncommitted opportunities: Disappear when phase passes (missed window)

**Benefits:**
- Immersive time awareness (anticipation of scheduled activities)
- Players can plan around duties and obligations
- Natural pacing (activities happen when contextually appropriate)
- No duplicates across phases

**See Also:**  
- [Camp Routine Schedule](camp-routine-schedule-spec.md) - Daily schedule baseline  
- [Event System Schemas](../Content/event-system-schemas.md) - Opportunity definitions

---

## Data Models

### CompanySimulationState

```csharp
public class CompanySimulationState
{
    // Roster
    public CompanyRoster Roster { get; set; }
    
    // Pressure
    public CompanyPressure Pressure { get; set; }
    
    // History
    public List<SimulationEvent> RecentEvents { get; set; }
    public int TotalDaysEnlisted { get; set; }
    
    // Crisis Tracking
    public bool InCrisis { get; set; }
    public string CrisisType { get; set; }
    public int CrisisDuration { get; set; }
}
```

### CampOpportunityState

```csharp
public class CampOpportunityState
{
    // Current Opportunities
    public List<CampOpportunity> CurrentOpportunities { get; set; }
    
    // History
    public OpportunityHistory History { get; set; }
    
    // Learning
    public PlayerPreferences Preferences { get; set; }
    
    // Cache
    public CampaignTime LastGenerated { get; set; }
    public CampaignTime CacheExpiry { get; set; }
}
```

---

## Configuration

### Background Simulation Config

```json
{
  "background_simulation": {
    "enabled": true,
    "sickness_base_chance": 0.02,
    "injury_base_chance": 0.01,
    "desertion_base_chance": 0.005,
    "incident_max_per_day": 2,
    "pressure_crisis_threshold": 80
  }
}
```

### Camp Opportunity Config

```json
{
  "camp_opportunities": {
    "enabled": true,
    "max_opportunities": 3,
    "cache_duration_hours": 2,
    "learning_weight": 0.3,
    "fitness_weight": 0.7,
    "variety_penalty": 0.3,
    "novelty_boost": 1.2
  }
}
```

---

## Examples

### Example Day: Garrison Morning

**Background Simulation (6am tick):**
```
Supplies consumed: -2 (98/100 remaining)
Private Marcus recovered from illness
No new sickness today
Equipment check: All good
Incident roll: Card game argument (minor)
Pulse check: All needs normal
News: "Private Marcus is back on his feet."
```

**Camp Opportunities (player opens menu at 8am):**
```
World: Garrison, Dawn, Peacetime
Camp: Morning, good mood, no recent activity
Player: Rested, moderate gold, good rep

Generated opportunities:
1. Weapon Training (fitness: 3.2)
2. Card Game (fitness: 2.1)
3. Help Wounded (fitness: 1.8)

Shows: Weapon Training, Card Game
```

### Example Day: Siege Afternoon

**Background Simulation (6am tick):**
```
Supplies consumed: -4 (siege modifier, 45/100 remaining)
Two soldiers fell ill (siege conditions)
One desertion attempt foiled
Equipment degradation: 3 items damaged
Incident roll: Fight over rations
Pulse check: Low supplies warning (45%)
News: "Supplies are running low. Men are worried."
News: "A fight broke out over rations last night."
```

**Camp Opportunities (player opens menu at 2pm):**
```
World: Siege, Midday, Desperate War
Camp: Afternoon, tense mood, recent fight
Player: Exhausted, low gold, stressed

Generated opportunities:
0 (siege = intense = no opportunities)

Shows: Empty (no time for camp activities during siege)
```

---

## Implementation Files

**Background Simulation:**
- `src/Features/Camp/CompanySimulationBehavior.cs` - Main simulation behavior
- `src/Features/Camp/Models/CompanyRoster.cs` - Roster tracking
- `src/Features/Camp/Models/CompanyPressure.cs` - Pressure calculation
- `src/Features/Camp/Models/SimulationEvent.cs` - Event records

**Camp Opportunities:**
- `src/Features/Camp/CampOpportunityGenerator.cs` - Opportunity generation
- `src/Features/Camp/Models/CampOpportunity.cs` - Opportunity definition
- `src/Features/Camp/Models/OpportunityHistory.cs` - Learning system
- `src/Features/Camp/Models/PlayerPreferences.cs` - Preference tracking

**Data Files:**
- `ModuleData/Enlisted/Decisions/camp_opportunities.json` - 36 opportunity definitions
- `ModuleData/Enlisted/Decisions/decisions.json` - Target decisions (26 need creation)
- `ModuleData/Enlisted/Config/camp_simulation_config.json` - Configuration

**Localization:**
- `ModuleData/Languages/enlisted_strings.xml` - All opportunity text
- News feed messages
- Incident descriptions

---

**Last Updated:** 2025-12-31  
**Status:** ✅ Implemented (Background Simulation + Camp Opportunities both complete)
