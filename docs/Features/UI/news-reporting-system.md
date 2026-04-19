# News & Reporting System

**Summary:** The news and reporting system tracks game events and generates narrative feedback for the player. It manages two feed types (kingdom-wide and personal), a daily brief with company/player/kingdom context, and a company needs status report showing Readiness and Supplies. Event and order outcomes are displayed in Recent Activities using a queue system instead of popups to reduce UI interruption. All text uses immersive Bannerlord military flavor instead of raw statistics. Order recaps show narrative summaries ("Routine watch", "Spotted tracks") instead of mechanical XP displays.

**2025-12-31 MAJOR UPDATE:** Menu narratives now comprehensively integrate with `WorldStateAnalyzer`, `CampLifeBehavior` pressures, and `CompanySimulationBehavior` for rich, context-aware storytelling that reflects actual simulated world state. Order outcomes now use RP-appropriate fallback text when JSON text is missing. XP displays removed from recaps in favor of narrative summaries.

**2026-01-01 UPDATE:** Routine activity outcomes now display full flavor text from `routine_outcomes.json` in all contexts (combat log, news feed, Recent Activity). Replaces generic summaries like "Combat Training: Completed" with immersive text like "Sharp focus today. Movements feel natural."

**2026-01-03 BUG FIX:** Supply status reporting now requires BOTH logistics strain AND low supplies to show "Supply lines stretched thin" warning. Previously showed warning when logistics was high even if supplies were at 86%.

**2026-01-03 BUG FIX:** Routine activity "Poor" outcomes now correctly display in yellow (Attention) instead of green (Positive). Previously, negative events like "A soldier reports fever this morning" appeared in green alongside "Excellent performance today" due to incorrect severity mapping. Fixed severity assignment: Poor → 2 (Attention/Yellow), Mishap → 2 (Attention/Yellow).

**Status:** ✅ Current - Comprehensive Integration Complete  
**Last Updated:** 2026-01-03 (Routine outcome severity fix)  
**Related Docs:** [Core Gameplay](../Core/core-gameplay.md), [UI Systems Master](ui-systems-master.md), [Color Scheme](color-scheme.md), [Order Progression System](../Core/order-progression-system.md), [Orders Content](../Content/orders-content.md), [Injury System](../Content/injury-system.md), [Camp Routine Schedule](../Campaign/camp-routine-schedule-spec.md)

---

## Index

- [Overview](#overview)
- [System Architecture](#system-architecture)
- [Event & Order Outcome Queue System](#event--order-outcome-queue-system)
- [Kingdom Feed](#kingdom-feed)
- [Personal Feed](#personal-feed)
- [Daily Brief](#daily-brief)
- [Company Status Report](#company-status-report)
- [Retinue Integration](#retinue-integration-t7-commanders)
- [Promotion Integration](#promotion-integration)
- [Pay Muster Integration](#pay-muster-integration)
- [Tracking Records](#tracking-records)
- [Data Structures](#data-structures)
- [Localization](#localization)
- [Implementation Files](#implementation-files)

---

## Overview

The news system operates as a read-only observer of campaign events. It listens to Bannerlord campaign events (battles, sieges, declarations, etc.) and generates localized narrative entries.

**Two Feed Types:**
1. **Kingdom Feed** - Kingdom-wide events (wars, battles, settlements, captures)
2. **Personal Feed** - Your enlisted lord's events and your direct participation

**Two Report Types:**
1. **Daily Brief** - Once-per-day narrative paragraph combining company situation, player status, and kingdom news
2. **Company Status Report** - Two company needs (Readiness, Supplies) with context-aware descriptions

**Key Behavior:**
- All feeds use `DispatchItem` struct (primitives only for save compatibility)
- Daily Brief caches at daily tick (stable for 24 hours to prevent jitter)
- Company Status generates on-demand when menu opens
- Records track outcomes for display in reports (orders, events, reputation changes)

---

## System Architecture

### Core Components

```
EnlistedNewsBehavior (singleton CampaignBehaviorBase)
├── Feed Management
│   ├── _kingdomFeed (List<DispatchItem>)          - Max 60 items
│   ├── _personalFeed (List<DispatchItem>)         - Max 20 items
│   └── Event listeners for battles/sieges/wars
│
├── Daily Brief (cached once per day)
│   ├── _dailyBriefCompany (string)                - Company situation
│   ├── _dailyBriefUnit (string)                   - Player status
│   ├── _dailyBriefKingdom (string)                - Kingdom context
│   └── BuildDailyBriefSection()                   - Assembles flowing paragraph
│
├── Company Status (generated on-demand)
│   └── BuildCompanyStatusReport()                 - Four needs with context
│
├── Tracking Records (for detailed reports)
│   ├── _orderOutcomes (List<OrderOutcomeRecord>)
│   ├── _eventOutcomes (List<EventOutcomeRecord>)
│   ├── _reputationChanges (List<ReputationChangeRecord>)
│   ├── _companyNeedChanges (List<CompanyNeedChangeRecord>)
│   ├── _pendingEvents (List<PendingEventRecord>)
│   └── _musterOutcomes (List<MusterOutcomeRecord>)
│
└── State Persistence
    ├── CampNewsState                               - 7-day report archive
    └── SyncData()                                  - Save/load integration
```

### Registered Campaign Events

The behavior subscribes to these Bannerlord campaign events:

| Event | Purpose |
|-------|---------|
| `DailyTickEvent` | Generate daily brief, detect army formations |
| `MapEventStarted` | Cache battle strengths for pyrrhic detection |
| `MapEventEnded` | Generate battle outcome dispatch |
| `OnSiegeEventStartedEvent` | Generate siege started dispatch |
| `HeroPrisonerTaken` | Generate capture dispatch |
| `HeroPrisonerReleased` | Generate release dispatch |
| `HeroKilledEvent` | Generate death/execution dispatch |
| `OnSettlementOwnerChangedEvent` | Generate settlement captured dispatch |
| `ArmyDispersed` | Generate army dispersal dispatch (personal feed) |
| `VillageBeingRaided` | Generate village raid dispatch |
| `WarDeclared` | Generate war declaration dispatch |
| `MakePeace` | Generate peace treaty dispatch |

---

## Comprehensive System Integration (2025-12-31)

### Overview

The main menu narratives (Kingdom News, Camp Status, Player Status) now integrate comprehensively with multiple backend systems to create rich, context-aware storytelling that reflects actual simulated world state.

### Integration Architecture

```
BuildMainMenuNarrative()
├── BuildKingdomNarrativeParagraph()
│   ├── WorldStateAnalyzer.WarStance (Desperate/Offensive/Defensive/MultiWar)
│   ├── GetVisibleKingdomFeedItems() - Recent news
│   ├── Kingdom.All + FactionManager - Active war enumeration
│   └── Settlement.All - Active siege counting
│
├── Camp Narrative Integration
│   ├── WorldStateAnalyzer.LordSituation (Siege/Campaign/Marching/Garrison)
│   ├── WorldStateAnalyzer.ActivityLevel (Quiet/Routine/Active/Intense)
│   ├── CampLifeBehavior pressures:
│   │   ├── LogisticsStrain (>60 = supply warnings)
│   │   ├── PayTension (>50 = pay disputes)
│   │   └── TerritoryPressure (>60 = hostile lands)
│   ├── GetVisiblePersonalFeedItems() - Recent events
│   ├── CompanyNeeds metrics (Readiness, Supplies)
│   └── BuildLordRumorsLine() - Strategic gossip based on all above
│
└── BuildPlayerNarrativeParagraph()
    ├── WorldStateAnalyzer for AHEAD forecast:
    │   ├── LordSituation - Context-aware duty forecasts
    │   └── ActivityLevel - Intensity predictions
    ├── CampLifeBehavior for critical warnings:
    │   ├── LogisticsStrain >70 - "Logistics collapsing"
    │   ├── MoraleShock >60 - "Morale crisis"
    │   └── PayTension >60 - "Pay disputes simmer"
    ├── OrderManager - Current duty state
    ├── CompanySimulationBehavior - Critical company warnings
    └── Culture-aware rank names (NCO/Officer titles)
```

### Lord Rumors System

**Purpose:** Contextual military camp gossip that reflects actual AI decisions and strategic situation.

**Integrates:**
- `WorldStateAnalyzer.LordSituation` - 8 distinct states (Siege, WarActiveCampaign, WarMarching, PeacetimeGarrison, etc.)
- `CampLifeBehavior` pressures - Supply/morale/pay/territory context
- Recent news feed - References recent victories, defeats, army formations
- Party AI state - Target settlements, army membership, siege status

**Example Outputs:**
- Siege + LogisticsStrain: *"Siege of Usanc grinds on. Whispers of withdrawal if supplies don't improve."*
- War + Recent victory: *"After our victory, Lord Hecard marches on Galend. Morale is high."*
- Garrison + PayTension: *"Men grumble about pay and boredom. Lord better have work for us soon."*
- Defeated: *"After the defeat, Lord regroups. Men speak of revenge in hushed tones."*

### Kingdom Narrative Integration

**WorldStateAnalyzer.WarStance Usage:**
- `Desperate` (>60% casualties, losing badly): *"Desperate struggle: X and Y press from all sides."*
- `Offensive` (wars we declared): *"Active war with X"*
- `Defensive` (wars against us): *"Conflict with X"*
- `MultiWar` (>1 enemy): *"War on N fronts: X and Y, plus Z others."*
- `Peace`: *"The realm is at peace. Trade routes flourish."*

**Siege Context:**
- Counts active sieges (ours vs. theirs)
- *"3 sieges underway against enemy strongholds"* vs *"2 of our settlements under siege"*
- Mixed: *"1 siege presses enemy holds, while 2 threaten our towns"*

### Camp Narrative Integration

**CampLifeBehavior Pressure Integration:**
```csharp
// Supply warnings: Only show "stretched thin" when BOTH conditions are true
// Bug fix (Jan 2026): Previously showed "stretched thin" when only logistics was high
if (logisticsHigh && suppliesLow)  // LogisticsStrain > 60 AND Supplies < 50
{
    // "Supply lines stretched thin" (combined warning)
}
else
{
    // Show actual supply level: "Well-stocked" / "Adequate" / "Low" / "Critical"
}

// Company needs are now just Readiness (0-100) and Supplies (0-100)
// Player fatigue (0-24 budget) remains separate from company needs
```

**Activity Level Context:**
- `Intense` + low supplies: *"Hard campaigning tests every soldier"*
- `Quiet` + low readiness: *"Garrison duty - training has slipped"*

### Player Status Integration

**NOW + AHEAD Combined Narrative:**

Current state is blended with world-state-aware forecasts:

```
OFF DUTY examples based on WorldStateAnalyzer:
- LordSituation.SiegeAttacking: "Off duty between siege shifts. The Decurion watches for the next call."
- ActivityLevel.Intense: "Brief respite. The Captain will have orders soon enough."
- LordSituation.WarMarching: "Off duty but ready. Wartime brings orders without warning."
- ActivityLevel.Quiet: "Off duty. Garrison life means quiet hours between routines."
```

**Critical Warnings from Pressures:**
```
CompanyNeeds + CampLifeBehavior integration:
- Supplies <20 OR LogisticsStrain >70: "Men whisper of hunger. Logistics collapsing — resupply urgently."
- PayTension >60: "Pay disputes simmer. The NCO works to keep tempers cool."
- Readiness <25: "Training has slipped. Combat readiness poor."
```

**Culture-Aware Rank Names:**
Uses `GetNCOTitle()` and `GetOfficerTitle()` with XML localization:
- Empire: Decurion/Centurion
- Vlandia: Sergeant/Captain
- Sturgia: Druzhina/Boyar
- (etc. for all 6 cultures)

---

## Event & Order Outcome Queue System

**Purpose:** Display event and order outcome narratives in Recent Activities instead of popup interruptions, using a queue to show one outcome at a time for better UX.

### Design Goals

1. **Reduce UI Interruption:** Event and order outcomes no longer show as popups after making choices
2. **Maintain Immersion:** Full narrative result text preserved and displayed in Recent Activities
3. **Prevent Clutter:** Only one outcome visible at a time (events and orders share the feed)
4. **Natural Discovery:** Players check their status menu to see what happened
5. **Immediate Feedback for Decisions:** Player-initiated decisions also show result text in combat log instantly

### What Still Uses Popups

- **Initial event choices** - Event setup and option selection still show as popups (for progression)
- **Multi-phase events** - Each phase shows a new event popup (chains work normally)
- **Reward choices** - Training focus selection and other sub-choices still use popups
- Only the **outcome narrative** after making a choice goes to Recent Activities

### Combat Log Display (Decisions Only)

**Decisions** (events with `category: "decision"`) display their result text immediately in the combat log for instant feedback:
- Success outcomes: Light green text
- Failure outcomes: Yellow text
- Also preserved in Recent Activities for later review

**Regular events** (non-decisions) only appear in Recent Activities, not the combat log.

### Queue Behavior

**Display Rules:**
- ONE event outcome shows in Recent Activities at any given time
- Each outcome displays for exactly 1 day
- Multiple events on the same day queue automatically (FIFO)
- Daily tick removes expired outcomes and promotes next in queue

**Example Flow:**
```
Day 1, Morning:
  - Event A fires → Shows immediately in Recent Activities
  - Event B fires later → Queued (hidden)

Day 2, Morning:
  - Event A expires (shown for 1 day) → Removed from display
  - Event B promoted → Now visible in Recent Activities
  - Event C fires → Queued

Day 3, Morning:
  - Event B expires → Removed
  - Event C promoted → Now visible
```

**Implementation:**
- `EventOutcomeRecord` tracks `IsCurrentlyShown` and `DayShown` for each outcome
- `AddEventOutcome()` checks if an outcome is already showing; if yes, queues new one
- `ProcessEventOutcomeQueue()` runs on daily tick to expire and promote outcomes
- Personal feed entries are added/removed dynamically as queue processes

**Display Format:**
```
Recent Activities:
• Crossing Gone Bad: You wait on the bank with the others, 
  watching the pioneers work. They know their business, but 
  the current is strong and the footing treacherous. It will 
  take time.

• ✗ Inspect the Picket Line: You missed a gap in the 
  pickets. The sergeant finds it on his own rounds.
```

**Technical Note:** The outcome popup system has been removed (dead code: `ShowResultText()` and `ShowOrderResult()` deleted). Event outcomes are recorded with their full `ResultNarrative` text, and order outcomes use their detailed summary text. Both are added to the personal feed only when actively shown. Decisions additionally display their result text in the combat log via `SendDecisionOutcomeToCombatLog()` for immediate player feedback. This reduces cognitive load while ensuring important player-initiated choices have instant visible results.

### Order Outcome Text (2025-12-31 Update)

Order outcomes now use RP-appropriate fallback text when JSON `text` field is missing. This ensures all order results sound like medieval military reports, not generic game messages.

**Fallback System:**

| Order Type | Success Brief | Failure Brief |
|------------|---------------|---------------|
| Guard/Watch/Sentry | "Watch ended, [Order] - all in order." | "[Order] - breach on your watch." |
| Patrol | "Patrol returned, [Order] - all in order." | "[Order] - patrol met trouble." |
| Scout | "Scout report filed, [Order] - all in order." | "[Order] - intelligence compromised." |
| Forage/Supply | "Resupply complete, [Order] - all in order." | "[Order] - returned empty-handed." |
| Repair/Equipment | "Work finished, [Order] - all in order." | "[Order] - botched the work." |
| Train/Drill | "Drill complete, [Order] - all in order." | "[Order] - men lost confidence." |
| Medical | "Wounded tended, [Order] - all in order." | "[Order] - patient worsened." |
| Default | "Duty complete, [Order] - all in order." | "[Order] - you fell short." |

**Detailed Fallbacks:**

Success examples:
- Guard: "The night passed without incident. Your vigilance kept the camp safe."
- Patrol: "You walked the rounds and found nothing amiss. The men rest easier."
- Scout: "You returned with useful intelligence. The officers will make good use of it."

Failure examples:
- Guard: "Something slipped past you in the dark. The sergeant's disappointment cuts deep."
- Patrol: "You missed the signs. The camp stirred with rumor of what you failed to notice."
- Scout: "Your report was wrong. Men may pay for your errors with blood."

**Implementation:** `OrderManager.GetSuccessBrief()`, `GetFailureBrief()`, `GetSuccessFallback()`, `GetFailureFallback()` provide context-aware text based on order ID patterns.

### Routine Activity Flavor Text (2026-01-01 Update)

Routine activity outcomes (training, foraging, patrol, formations, etc.) now display full narrative flavor text from `ModuleData/Enlisted/Config/routine_outcomes.json` instead of generic summaries.

**Before:**
```
Combat Training: Completed
Combat Training: Good progress
Combat Training: Exceptional performance
```

**After (uses flavor text from outcome roll):**
```
Combat Training: Sharp focus today. Movements feel natural.
Combat Training: Solid session. The drills are paying off.
Combat Training: Crisp movements. Best formation in the company.
```

**Technical Implementation:**
- `RoutineOutcome.GetNewsSummary()` now prioritizes `FlavorText` property over generic outcome text
- `CampRoutineProcessor` generates flavor text from `routine_outcomes.json` based on outcome roll (Excellent/Good/Normal/Poor/Mishap)
- Same flavor text appears in:
  - Combat log (bottom-left, real-time)
  - Personal feed (Recent Activity menu)
  - Camp Hub "RECENT ACTIVITY" section

**Example routine_outcomes.json entry:**
```json
"training": {
  "flavorText": {
    "excellent": [
      "Sharp focus today. Movements feel natural.",
      "Everything clicked. A veteran watched approvingly."
    ],
    "good": [
      "Solid session. The drills are paying off.",
      "Good practice. You're getting faster."
    ],
    "normal": [
      "Another day of practice.",
      "Standard drills completed."
    ]
  }
}
```

This change applies to all 7 routine activity categories: formation, training, work, social, economic, recovery, and special.

### Order Event Integration

Order events (fired during duty via `OrderProgressionBehavior`) use this same queue system:

```csharp
// In OrderProgressionBehavior, after player makes event choice:
EnlistedNewsBehavior.Instance.AddEventOutcome(
    eventTitle: orderEvent.Title,
    resultNarrative: selectedOption.ResultText,
    severity: orderEvent.Severity,
    dayNumber: (int)CampaignTime.Now.ToDays
);
```

**See Also:** [Orders Content](../Content/orders-content.md) for order definitions, event files at `ModuleData/Enlisted/Orders/order_events/`.

---

## Kingdom Feed

Kingdom-wide events visible to all enlisted lords in your kingdom.

**Storage:** `List<DispatchItem>` limited to 60 items (oldest pruned)

**Dispatch Categories:**
- `"war"` - War declarations and peace treaties
- `"battle"` - Victory/defeat/pyrrhic/inconclusive battle outcomes
- `"siege"` - Siege starts and conclusions
- `"settlement"` - Settlement ownership changes
- `"prisoner"` - Lord captures, releases, executions
- `"army"` - Army formations and dispersals
- `"village"` - Village raids

**Key Implementation Detail - Pyrrhic Victory Detection:**

The system tracks battle initial strengths in `_battleSnapshots` dictionary:
1. `OnMapEventStarted`: Cache attacker/defender troop counts
2. `OnMapEventEnded`: Compare final vs initial strength
3. If winner lost > 40% of initial strength → classify as pyrrhic

**Access:** `EnlistedNewsBehavior.Instance.KingdomFeed` (read-only)

---

## Personal Feed

Events specific to your enlisted lord and your direct participation.

**Storage:** `List<DispatchItem>` limited to 20 items (oldest pruned)

**Personal Events:**
- Lord's army formation/dispersal
- Battles where you participated
- Orders you completed
- Events you chose

**Access:** `EnlistedNewsBehavior.Instance.PersonalFeed` (read-only)

### Routine Outcome Integration

**Added:** 2025-12-31  
**Implementation:** `CampRoutineProcessor`, `AddRoutineOutcome()`  
**Related Docs:** [Camp Routine Schedule](../../Campaign/camp-routine-schedule-spec.md)

Automatic routine activities (training, foraging, patrol, etc.) generate feed entries without popups:

**Call Site:**
```csharp
// In CampRoutineProcessor.AddToNewsFeed()
EnlistedNewsBehavior.Instance?.AddRoutineOutcome(outcome);
```

**Feed Entry Format:**
```
Combat Training: Good progress
Foraging Duty: Excellent results (Supplies critical)
Morning Formation: Completed
Patrol Duty: Uneventful
Extended Rest: Good recovery (Men exhausted)
```

**Characteristics:**
- Category: `"routine_activity"`
- Severity: Based on outcome type
  - `Excellent` → Severity 1 (Positive/Green "Success")
  - `Good` → Severity 0 (Normal/Cream "Default")
  - `Normal` → Severity 0 (Normal/Cream "Default")
  - `Poor` → Severity 2 (Attention/Yellow "Warning")
  - `Mishap` → Severity 2 (Attention/Yellow "Warning")
- Minimum display: 1 day
- Override context shown in parentheses if applicable

**When Generated:**
- At phase boundaries (6am, 12pm, 6pm, 12am)
- One entry per routine activity processed
- Only when player has no commitment for that phase
- Skip if player is on duty

**Display:**
- Appears in RECENT ACTIVITY section with other camp news
- Sorted by timestamp
- Auto-trimmed when feed exceeds 35 items

**Deduplication:**
- Story key: `"routine:{Phase}:{ActivityCategory}:{DayNumber}"`
- Prevents multiple entries for same activity on same day

**Integration:**
- Works alongside event outcomes and order outcomes
- Uses same personal feed infrastructure
- No special UI treatment (same format as other news)

**Combat Log Parallel:**
Routine outcomes also generate instant feedback via combat log messages (see [UI Systems Master](ui-systems-master.md#combat-log--routine-feedback) for details).

---

## Main Menu Narrative

The main enlisted menu displays a flowing RP narrative, not structured data. Generated at menu open and cached for the session.

**Design Goals:**
- **Immersive prose** - Reads like a narrator describing your situation
- **Color-coded keywords** - Important words highlighted within sentences
- **No UI chrome** - No section headers, bullet lists, or stat labels
- **Contextual** - Content varies by situation (garrison vs march vs battle)

**Color Coding (embedded in prose):**
- **Success (Green):** "well-supplied", "good spirits", "morale is high"
- **Warning (Gold):** "rations running thin", "restless", "late pay"
- **Alert (Red):** "food running low", "enemy approaching", "wounded"

**Display Location:** Main enlisted menu body text

**Example - Garrison:**
```
Your lord holds court at Pravend. The afternoon heat keeps most 
soldiers in the shade of their tents, playing dice or mending kit. 
A <span style="Success">well-supplied</span> camp with 
<span style="Success">good spirits</span>.

Word from the realm: <span style="Warning">Battania presses our 
western border</span>, but no battles have reached you yet.
```

**Example - On Campaign:**
```
The column marches east through rolling grasslands. Dust clouds 
rise behind the baggage train. <span style="Warning">Rations 
are running thin</span> and the men grow restless.

<span style="Alert">A skirmish with raiders yesterday</span> 
left three wounded. The campaign wears on.
```

**Technical Implementation:**
- Generated by `BuildMainMenuNarrative()` in `EnlistedMenuBehavior.cs`
- Pulls from `EnlistedNewsBehavior` for kingdom feed, camp state, player status
- Color spans embedded directly in prose text
- Refreshes on menu init only (stable during session)

---

### Narrative Generation

The main menu narrative is built from multiple data sources, woven into flowing prose.

**Generated by:** `BuildMainMenuNarrative()` in `EnlistedMenuBehavior.cs`

**Data Sources:**
- Party state (battle, siege, settlement, army, march)
- Company needs (supplies, morale, rest, equipment)
- Kingdom feed (recent battles, wars, treaties)
- Player status (injuries, fatigue, active orders)

### Generation Flow

```
OnDailyTick() called
    ├→ EnsureDailyBriefGenerated()
    │   ├→ Check if already generated for current day
    │   ├→ If not, build each section:
    │   │   ├→ BuildDailyCompanyLine()      → _dailyBriefCompany
    │   │   ├→ BuildDailyUnitLine()         → _dailyBriefUnit
    │   │   └→ BuildDailyKingdomLine()      → _dailyBriefKingdom
    │   └→ Store in _lastDailyBriefDayNumber
    │
    └→ BuildDailyBriefSection() (when UI requests)
        ├→ Start with _dailyBriefCompany
        ├→ Add BuildSupplyContextLine() if supplies < 70% (color-coded by severity)
        ├→ Add BuildCasualtyReportLine() if losses/wounded (color-coded)
        ├→ Add BuildRecentEventLine() if events in last 24h
        ├→ Add BuildPendingEventsLine() if chain events pending
        ├→ Add BuildFlagContextLine() (random reputation flavor)
        ├→ Add BuildSkillProgressLine() if skill near levelup (color-coded)
        ├→ Add BuildBaggageStatusLine() if baggage events (color-coded by status)
        ├→ Add _dailyBriefUnit
        └→ Add _dailyBriefKingdom
            └→ Return as single flowing paragraph with embedded color spans
```

### Section Builders

#### BuildDailyCompanyLine()
**Checks:** `party.Party.MapEvent`, `party.Party.SiegeEvent`, `party.CurrentSettlement`, `party.Army`, `party.TargetSettlement`

**Outputs:**
- Battle: Enemy leader, atmospheric combat description
- Siege: Attacker vs defender, siege engines, enemy camps
- Settlement: Type-specific (town/castle/village) with sensory details
- Army: Army size, marshal, your position in column
- March: Target destination + `GetTerrainFlavor()` (terrain-aware, time-of-day aware)

**Context factors:** Party size descriptor (small/sizeable/large/formidable), terrain type, hour of day

#### BuildDailyUnitLine()
Uses the localized "brief" string system with tier/time-of-day variants.

**Priority order:**
1. Recent battle (within 24h): `brief_{battletype}_{won|lost}_{tierkey}_N`
2. Injuries: `brief_injury_{tierkey}_N`
3. Illness: `brief_illness_{tierkey}_N`
4. Low fatigue: `brief_exhausted_{tierkey}_N` or `brief_tired_{tierkey}_N`
5. Good condition: `brief_good_{tierkey}_{timekey}_N`

**Tier keys:** `recruit` (T1-2), `soldier` (T3-4), `nco` (T5-6), `veteran` (T7+)  
**Time keys:** `night` (<6 or >=20), `morning` (6-10), `evening` (17-20), `day` (default)  
**N:** Random 1-3 for variety

Implemented via `PickRandomLocalizedString(prefix, count, fallbackId)` with `FlavorRng`.

#### BuildDailyKingdomLine()
**Priority:** If kingdom feed has recent headline, use it. Otherwise generate strategic summary.

**Checks:** Active wars (via `FactionManager.IsAtWarAgainstFaction`), active sieges count

**Outputs:**
- Multi-front wars with sieges
- Multi-front wars without sieges
- Single war with siege
- Single war without siege
- Peace time

#### BuildSupplyContextLine()
Only shown when `CompanyNeeds.Supplies < 70`.

**Thresholds:**
- `< 30`: Critical (stores locked) - **Color: Alert (Red)**
- `< 50`: Low (rations cut) - **Color: Warning (Gold)**
- `< 70`: Moderate (dwindling) - **Color: Warning (Gold)**

#### BuildCasualtyReportLine()
Uses `_lostSinceLastMuster` (reset at muster) and current wounded count.

**Variations:**
- Heavy losses (10+): Somber tone - **Lost count in Alert (Red)**
- Moderate losses (5-9): Names spoken at fire - **Lost count in Alert (Red)**
- Few losses (2-4): Cost of march - **Lost count in Alert (Red)**
- Single loss: Lads are quiet - **Lost count in Alert (Red)**
- Wounded only: Scaled by count - **Wounded count in Warning (Gold)**

#### BuildRecentEventLine()
Checks `_eventOutcomes` for entries with `dayNumber` within 1 day of current.

**Event ID matching:**
- Dice/gambling/wager → dice game aftermath
- Training/drill/practice → sore muscles, improved skills
- Hunt → fresh game
- Lend/loan (not repay) → comrade owes debt
- Tavern/drinking → hangover
- Loot/battlefield → spoils weigh on you

#### BuildPendingEventsLine()
Checks `_pendingEvents` for scheduled chain events.

**Chain ID matching:**
- Repay/return/debt → debt reminder with days elapsed
- Gratitude/thank/favor → kindness remembered
- Revenge/grudge → enemy watching

Returns only the first match (avoids spam).

#### BuildFlagContextLine()
Reads `EscalationManager.Instance.State` flags and picks one random entry daily.

**Flags checked:**
- `has_helped_comrade` / `helped_comrade`
- `dice_winner` / `gambling_winner`
- `shared_winnings` / `generous`
- `officer_attention` / `noticed_by_officers`
- `training_focused` / `dedicated_training`
- `good_hunter` / `skilled_hunter`
- `drinks_with_soldiers` / `tavern_regular`

Uses `MBRandom.RandomInt()` for selection.

#### BuildSkillProgressLine()
Cached for 6 hours. Checks main combat skills (`OneHanded`, `TwoHanded`, `Polearm`, `Bow`, `Crossbow`) for level-up proximity.

Uses `CharacterDevelopmentModel.GetXpRequiredForLevel()` to calculate percentage to next level. Shows hint if any skill >= 80% to levelup.

**Color:** Skill name wrapped in **Success (Green)** to emphasize progress.

#### BuildBaggageStatusLine()
Reports on the baggage train status based on `BaggageTrainManager` state.

**Status Types:**
- **Raids/Locked:** "Baggage was raided" - **Color: Alert (Red)**
- **Delays:** "Baggage delayed X days" - **Color: Warning (Gold), days count also in Warning**
- **Arrivals:** "Baggage caught up" - **Color: Success (Green)**

### Player Battle Tracking

The system tracks player battle participation for aftermath flavor:
- `_lastPlayerBattleTime`: When last battle occurred
- `_lastPlayerBattleType`: "bandit", "army", "siege"
- `_lastPlayerBattleWon`: Victory status

Updated in `CheckPlayerBattleParticipation()` called from `OnMapEventEnded()`.

---

## Company Status Reporting

Company status information appears in two locations, both generated on-demand:

**Display Locations:**
1. **Main enlisted_status menu** → "COMPANY REPORTS" section (via `BuildCampNarrativeParagraph()`)
   - Atmospheric narrative paragraph blending world state, company needs, baggage status, and location context
   - Uses world-state-aware generation with activity levels and lord situation
   - Includes baggage train status when notable (raids, delays, arrivals)
   
2. **Camp Hub menu** → "COMPANY STATUS" summary (via `BuildCompanyStatusSummary()`)
   - Flowing prose combining atmosphere, troop strength/composition, company needs, and location
   - Includes baggage train status when notable
   - More detailed than main menu version

### Specialized Builders

Context-aware narrative builders used across both displays:

```csharp
GetReadinessPhrase(readiness)  // Company-wide combat readiness
GetSupplyPhrase(supplies)      // Company-wide provisions
```

Player fatigue (0-24 budget) is displayed separately in the player status section and is NOT part of company needs.

### Status Levels

**Company Needs (2 metrics):**

Each need uses 5 thresholds:
- **Excellent**: 80-100
- **Good**: 60-79
- **Fair**: 40-59
- **Poor**: 20-39
- **Critical**: 0-19

**Readiness Phrases:**
- 80+: "Combat-ready and drilled" (Success/Green)
- 60-79: "Adequately trained" (Success/Green)
- 40-59: "Readiness adequate for most duties" (Default)
- 25-39: "Training has slipped" (Warning/Yellow)
- 15-24: "Combat readiness poor" (Alert/Red)
- <15: "Unprepared for battle" (Alert/Red)

**Supplies Phrases:**
- 80+: "Well-stocked with provisions" (Success/Green)
- 60-79: "Supplies holding steady" (Success/Green)
- 40-59: "Rations adequate for now" (Default)
- 25-39: "Food stores running thin" (Warning/Yellow)
- 15-24: "Rations critically low" (Alert/Red)
- <15: "Starvation threatens the company" (Alert/Red)

**Player Fatigue (0-24 budget):**

Player fatigue is tracked separately from company needs:
- 24-17: Fresh, no display
- 16-9: "Weariness creeps in" (Warning/Yellow)
- 8-1: "Bone-deep exhaustion" (Alert/Red)
- 0: Collapse risk

---

## Retinue Integration (T7+ Commanders)

The news system tracks retinue activities for commanders (T7+).

### Personal Feed Entries

**Retinue Events:**
- All 10 retinue narrative events logged via `AddEventOutcome()`
- Format: "[Event Title]: [Option Chosen] - [Outcome]"
- Priority: 2-3 (normal)

**Veteran Activities:**
- Emergence: "[Veteran Name] has distinguished themselves in battle." (Priority 2)
- Death: "[Veteran Name], who served N battles, has fallen." (Priority 4, high)

**Casualties:**
- "Your retinue suffered losses: N killed, M wounded."
- Reported separately from lord's casualties
- Added via `AddRetinueCasualtyReport()` after battles

### Daily Brief Integration

**Company Context Section:**

`BuildRetinueContextLine()` adds retinue status when notable:
- **Under-strength** (<70% capacity): "Your retinue is at X of Y. Z replacements needed."
- **Low loyalty** (<30): "Your men are restless. Morale among your retinue is worryingly low."
- **High loyalty** (>80): "Your soldiers are devoted. They would follow you into any fight."

Called from `BuildDailyBriefSection()` after skill progress line.

**Casualty Report Section:**

Retinue casualties included in `BuildCasualtyReportLine()`:
- Distinguished from lord's troop casualties
- Shows retinue losses separately: "Your retinue lost N soldiers..."
- Uses `_retinueLostSinceLastMuster` counter (separate from main force)

### Reputation Changes

Retinue loyalty changes recorded as reputation changes:

```csharp
ReputationChangeRecord {
    Target: "Retinue",
    Delta: loyalty_change,
    NewValue: new_loyalty_value,
    Message: source_description,
    DayNumber: current_day
}
```

**Handled in `RecordReputationChange()`:**
- Target = "Retinue" alongside "Lord", "Officer", "Soldier"
- Appears in service record ledger
- Tracked for long-term analysis

### Veteran News

**Emergence:**
```csharp
AddVeteranEmergence(NamedVeteran veteran)
{
    var headline = "{NAME}, a soldier in your retinue, has distinguished themselves.";
    AddPersonalNews("retinue", headline, priority: 2);
}
```

**Death:**
```csharp
AddVeteranDeath(NamedVeteran veteran)
{
    var headline = "{NAME}, who served {BATTLES} battles, has fallen.";
    AddPersonalNews("retinue", headline, priority: 4);
}
```

### Implementation

**Methods Added to EnlistedNewsBehavior:**
- `BuildRetinueContextLine()` - Returns context string for Daily Brief
- `AddRetinueCasualtyReport(int killed, int wounded)` - Logs retinue casualties
- `AddVeteranEmergence(NamedVeteran veteran)` - Logs veteran emergence
- `AddVeteranDeath(NamedVeteran veteran)` - Logs veteran death

**Localization Strings:**
```xml
<string id="brief_retinue_understrength" text="Your retinue is at {CURRENT} of {CAPACITY}. {MISSING} replacements needed." />
<string id="brief_retinue_loyalty_low" text="Your men are restless. Morale among your retinue is worryingly low." />
<string id="brief_retinue_loyalty_high" text="Your soldiers are devoted. They would follow you into any fight." />
<string id="news_retinue_casualties" text="Your retinue suffered losses: {KILLED} killed, {WOUNDED} wounded." />
<string id="news_veteran_emerge" text="{NAME}, a soldier in your retinue, has distinguished themselves in battle." />
<string id="news_veteran_death" text="{NAME}, who served {BATTLES} battles under your command, has fallen." />
```

---

## Promotion Integration

Promotions are recorded to the Personal Feed when players advance in rank.

### Personal Feed Entries

Called from `PromotionBehavior.TriggerPromotionNotification()`:

```csharp
EnlistedNewsBehavior.Instance?.AddPromotionNews(newTier, rankName, retinueSoldiers);
```

**Tier-Specific Headlines:**
- T2-T6: Immersive rank recognition text
- T7-T9: Commander promotion with retinue size mentioned

**Localization Strings:**
```xml
<string id="News_Promotion_T2" text="The sergeant's stripe is sewn to your sleeve. You are now {RANK}." />
<string id="News_Promotion_T7" text="Twenty soldiers salute their new commander. You are {RANK}, with a retinue of your own." />
```

---

## Pay Muster Integration

Pay muster outcomes are recorded to the Personal Feed for payment history tracking.

### Personal Feed Entries

Called from `EnlistmentBehavior` payment processing:

```csharp
// ProcessFullPayment, ProcessPartialPayment, ProcessPayDelay
EnlistedNewsBehavior.Instance?.AddPayMusterNews(outcome, amountPaid, amountOwed);
```

**Outcome Types:**
- `full` / `backpay`: Full payment received
- `partial`: Partial payment with backpay still owed
- `delayed`: No payment, backpay accumulating
- `promissory`: IOU accepted
- `corruption` / `side_deal`: Alternative payment methods

**Localization Strings:**
```xml
<string id="News_PayMuster_Full" text="The paymaster counts out your coin. {AMOUNT} denars received in full." />
<string id="News_PayMuster_Delayed" text="The paymaster shakes his head. No coin today. {OWED} denars now owed." />
```

---

## Tracking Records

The system maintains several record lists for detailed report displays. These are NOT used in the Daily Brief but available for service record views.

### OrderOutcomeRecord

Tracks order completions for service record.

**Fields:**
- `OrderTitle`: Order name
- `Success`: Completed successfully?
- `BriefSummary`: One-line outcome
- `DetailedSummary`: Full outcome text
- `Issuer`: Who gave the order
- `DayNumber`: When completed

**Added via:** `RecordOrderOutcome()` (called by OrderManager)

**Retrieved via:** `GetRecentOrderOutcomes(maxDaysOld)`

### EventOutcomeRecord

Tracks event choices for personal feed using a queue system to display one outcome at a time.

**Note:** Orders also feed into the Recent Activities display via `AddOrderOutcome()`, but use a simpler system without queuing.

**Fields:**
- `EventId`: Event identifier
- `EventTitle`: Localized title
- `OptionChosen`: Which option selected
- `OutcomeSummary`: Mechanical effects summary (legacy)
- `ResultNarrative`: Full RP narrative outcome text from option's `resultText` field
- `DayNumber`: When event occurred
- `EffectsApplied`: Dictionary<string, int> of effects
- `Severity`: Color coding level (0=Normal, 1=Positive, 2=Attention, 3=Urgent, 4=Critical)
- `IsCurrentlyShown`: True if outcome is displayed in Recent Activities, false if queued
- `DayShown`: Day this outcome was first displayed (-1 if still in queue)

**Queue System Behavior:**
- Only ONE event outcome is shown in Recent Activities at any given time
- When multiple events fire, subsequent outcomes queue automatically
- Each outcome displays for exactly 1 day
- Daily tick automatically removes expired outcomes and promotes next queued outcome
- Queue processes first-in-first-out (FIFO)

**Display:**
- Outcome narrative replaces popup interruption to reduce UI overhead
- Players check Recent Activities section in status menu to see what happened
- Format: `"{EventTitle}: {ResultNarrative}"`
- Example: *"Crossing Gone Bad: You wait on the bank with the others, watching the pioneers work. They know their business, but the current is strong and the footing treacherous. It will take time."*

**Added via:** `AddEventOutcome()` (called by EventDeliveryManager)

**Retrieved via:** `GetRecentEventOutcomes(maxDaysOld)`

### ReputationChangeRecord

Tracks reputation changes for reports.

**Fields:**
- `Target`: "Lord", "Officer", or "Soldier"
- `Delta`: Change amount
- `NewValue`: Resulting reputation
- `Message`: Explanation text
- `DayNumber`: When occurred

**Added via:** `RecordReputationChange()` (called by EscalationManager)

**Retrieved via:** `GetRecentReputationChanges(maxDaysOld)`

### CompanyNeedChangeRecord

Tracks company need changes for reports.

**Fields:**
- `Need`: Need name (string)
- `Delta`: Change amount
- `OldValue`: Previous value
- `NewValue`: New value
- `Message`: Explanation
- `DayNumber`: When occurred

**Added via:** `RecordCompanyNeedChange()` (called by CompanyNeedsManager)

**Retrieved via:** `GetRecentCompanyNeedChanges(maxDaysOld)`

### PendingEventRecord

Tracks scheduled chain events for Daily Brief hints.

**Fields:**
- `SourceEventId`: Original event
- `ChainEventId`: Follow-up event identifier
- `ContextHint`: Text hint for brief
- `ScheduledDay`: When it will fire
- `CreatedDay`: When scheduled

**Added via:** `SchedulePendingChainEvent()` (called by EventDeliveryManager)

**Cleaned up:** Events > 7 days overdue are skipped in `BuildPendingEventsLine()`

### MusterOutcomeRecord

Tracks muster outcomes for camp news.

**Fields:**
- `DayNumber`: Muster day
- `StrategicContext`: Context tag for flavor text (e.g., "coordinated_offensive")
- `PayOutcome`: "paid", "partial", "delayed", "promissory", "corruption"
- `PayAmount`: Gold received
- `RationOutcome`: "issued", "none_low_supply", "none_critical", "officer_exempt"
- `RationItemId`: Item received
- `QmReputation`: QM rep at issue
- `SupplyLevel`: Company supply 0-100
- `LostSinceLast`: Casualties since previous muster
- `SickSinceLast`: Sick since previous muster
- `OrdersCompleted`: Count of orders completed this period
- `OrdersFailed`: Count of orders failed this period
- `BaggageOutcome`: "passed", "confiscated", "bribed", "skipped"
- `InspectionOutcome`: "perfect", "basic", "failed", "skipped"
- `RecruitOutcome`: "mentored", "ignored", "hazed", "skipped"
- `PromotionTier`: New tier if promoted (0 if no promotion)
- `RetinueStrength`: Current retinue size (T7+ only, 0 otherwise)
- `RetinueCasualties`: Retinue losses this period (T7+ only)
- `FallenRetinueNames`: List of fallen retinue member names (T7+ only)

**Added via:** `AddMusterOutcome()` (called by `MusterMenuHandler.CompleteMusterSequence()`)

**Retrieved via:** `GetLastMusterOutcome()`

---

## Data Structures

### DispatchItem (struct)

Used for both kingdom and personal feeds. Primitive-friendly for save compatibility.

**Fields:**
```csharp
int DayCreated           // Campaign day when created
string Category          // "war", "battle", "prisoner", "siege", etc.
string TemplateId        // Localization key (e.g., "News_BattleVictory")
string Headline          // Formatted headline text
string DetailText        // Optional detailed text
int Priority             // Display priority (higher = more important)
```

**Creation:** `AddKingdomNews()` and `AddPersonalNews()` helper methods

**Formatting:** `FormatDispatchItem()` applies placeholders and returns display string

### DailyReportSnapshot (class)

Factual snapshot with bands/tags. Populated by fact producers.

**Current Status:** Partially implemented. The producer pattern exists but Daily Brief primarily uses direct builders.

**Fields:**
- `DayNumber`: Day of snapshot
- Delta fields: `WoundedDelta`, `SickDelta`, `DeadDelta`, `ReplacementsDelta`
- Band enums: `ThreatBand`, `FoodBand`, `HealthDeltaBand`
- Tag strings: `ObjectiveTag`, `BattleTag`, `AttachedArmyTag`, `DisciplineTag`, `StrategicContextTag`, `TrainingTag`

**Note:** `MoraleBand` enum removed (deprecated 2026-01-11)

**Producers:**
- `CompanyMovementObjectiveProducer`: Populates objective/army tags
- `UnitStatusFactProducer`: Populates health deltas
- `KingdomHeadlineFactProducer`: (purpose unclear from names)

**Interface:** `IDailyReportFactProducer.Contribute(snapshot, context)`

**Note:** This appears to be infrastructure for future template-based report generation. Current Daily Brief does not use snapshots.

### CampNewsState (class)

Persisted state for report archive and ledger.

**Storage:**
- `_archive`: Ring buffer of `DailyReportRecord[7]`
- `_archiveHeadIndex`: Current write position
- `_ledger`: `CampLifeLedger` for 30-day rolling stats
- `_lastGeneratedDayNumber`: Gate to prevent duplicate generation

**Methods:**
- `AppendReport(record)`: Add to ring buffer
- `TryGetReportForDay(day)`: Retrieve specific day
- `TryGetLatestReport()`: Get most recent
- `GetBaselineRosterCounts()`: For delta calculation

**Save/Load:** Synced via `EnlistedNewsBehavior.SyncData()`

### BattleSnapshot (struct)

Temporary tracking for pyrrhic victory detection.

**Fields:**
```csharp
string MapEventId                    // Hash code as string
int AttackerInitialStrength
int DefenderInitialStrength
```

**Lifecycle:**
1. Created in `OnMapEventStarted()`
2. Stored in `_battleSnapshots` dictionary
3. Retrieved in `OnMapEventEnded()` for classification
4. Removed after battle ends

---

## Localization

All text uses `TextObject` with string IDs and placeholders.

### String ID Conventions

| Prefix | Purpose | Example |
|--------|---------|---------|
| `brief_*` | Daily Brief sections | `brief_march_army`, `brief_casualties_heavy` |
| `status_*` | Company Status Report | `status_readiness_excellent`, `status_morale_combat` |
| `News_*` | Kingdom dispatch templates | `News_BattleVictory`, `News_SiegeStarted` |

### Placeholder Usage

```csharp
var text = new TextObject("{=brief_march_army}{ARMY_DESC} marches under the banner of {LEADER}.");
text.SetTextVariable("ARMY_DESC", armySizeDesc);
text.SetTextVariable("LEADER", armyLeader);
return text.ToString();
```

### Common Placeholders

- `{LORD}`, `{LORD_NAME}` - Enlisted lord
- `{ENEMY}` - Enemy faction/leader
- `{KINGDOM}` - Player's kingdom
- `{SETTLEMENT}` - Settlement name
- `{COUNT}`, `{DEAD}`, `{WOUNDED}` - Casualty counts
- `{DAYS}` - Day counters
- `{TERRAIN}` - Terrain flavor text
- `{ARMY_DESC}`, `{SIZE}` - Army/party size descriptors

### File Location

`ModuleData/Languages/enlisted_strings.xml` contains ~120 news system strings.

The template includes fallback text for development:
```xml
<string id="brief_casualties_heavy" text="The company has paid dearly — {COUNT} souls lost since last muster." />
```

---

## Implementation Files

### Core Behavior

| File | Lines | Purpose |
|------|-------|---------|
| `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs` | ~4,280 | Main news system singleton |
| `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` | Partial | Company Status Report builders |

### Data Models

| File | Purpose |
|------|---------|
| `src/Features/Interface/News/Models/DailyReportModels.cs` | `DailyReportSnapshot`, band enums |
| `src/Features/Interface/News/Models/CampLifeLedger.cs` | 30-day rolling stats |
| `src/Features/Interface/News/State/CampNewsState.cs` | Report archive, persistence |

### Generation (Partial Implementation)

| File | Purpose |
|------|---------|
| `src/Features/Interface/News/Generation/Producers/IDailyReportFactProducer.cs` | Producer interface |
| `src/Features/Interface/News/Generation/Producers/CompanyMovementObjectiveProducer.cs` | Objective/army tags |
| `src/Features/Interface/News/Generation/Producers/UnitStatusFactProducer.cs` | Health deltas |
| `src/Features/Interface/News/Generation/Producers/KingdomHeadlineFactProducer.cs` | Kingdom headlines |
| `src/Features/Interface/News/Generation/Producers/CampNewsContext.cs` | Context for producers |
| `src/Features/Interface/News/Generation/DailyReportGenerator.cs` | Generator infrastructure |
| `src/Features/Interface/News/Templates/NewsTemplates.cs` | Template system |

**Note:** The producer/template system appears to be infrastructure for future development. Current Daily Brief uses direct builders in `EnlistedNewsBehavior`.

### Localization

| File | Purpose |
|------|---------|
| `ModuleData/Languages/enlisted_strings.xml` | All localized strings |

### Related Systems Integration

| System | Integration Point |
|--------|-------------------|
| **Orders** | `OrderManager` calls `RecordOrderOutcome()` |
| **Events** | `EventDeliveryManager` calls `AddEventOutcome()`, `SchedulePendingChainEvent()` |
| **Escalation** | `EscalationManager` calls `RecordReputationChange()` |
| **Company Needs** | `CompanyNeedsManager` provides current values |
| **Pay System** | `EnlistmentBehavior` calls `AddPayMusterNews()` after each muster resolution |
| **Promotion** | `PromotionBehavior` calls `AddPromotionNews()` after tier advancement |
| **Retinue** | `RetinueCasualtyTracker` calls `AddRetinueCasualtyReport()`, `AddVeteranEmergence()`, `AddVeteranDeath()` |

---

**End of Document**
