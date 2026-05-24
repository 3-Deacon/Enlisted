# Muster System

**Summary:** The Muster System is a multi-stage GameMenu sequence that occurs every 12 days, guiding players through pay day ceremonies, rank progression displays, and comprehensive period summaries. Replaces the simple pay inquiry popup with an immersive muster experience that integrates pay, rations, and promotion acknowledgments.

**Status:** ✅ Current  
**Last Updated:** 2026-01-03 (Quartermaster stock refresh now uses cross-formation equipment discovery)  
**Related Docs:** [Pay System](pay-system.md), [Enlistment](enlistment.md), [Promotion System](promotion-system.md), [Quartermaster System](../Equipment/quartermaster-system.md), [News & Reporting](../UI/news-reporting-system.md)

---

## Index

- [Overview](#overview)
- [Menu Flow](#menu-flow)
- [Time Control Behavior](#time-control-behavior)
- [Menu Stages](#menu-stages)
- [Rank Progression Display](#rank-progression-display)
- [Event Integration](#event-integration)
- [System Integrations](#system-integrations)
- [Edge Cases & Special Behaviors](#edge-cases--special-behaviors)

---

## Overview

The pay muster occurs every 12 days (configurable via `payday_interval_days`) and serves as the primary checkpoint for wages, rations, equipment availability, and rank progression. When the muster day arrives, players enter a multi-stage menu sequence that:

1. **Summarizes the 12-day period** - Shows events, battles, gold earned, XP gained
2. **Displays service progression** - Current rank, XP to next rank, unlocks coming
3. **Guides through muster stages** - Pay → Events → Summary
4. **Integrates key events** - Recruit interactions
5. **Provides context for decisions** - Shows pay status, supply levels, unit condition
6. **Creates natural transitions** - Direct links to Quartermaster, Service Records
7. **Generates comprehensive reports** - Final summary saved to news feed

### What Happens at Muster

**Automatic Processes:**
- Fatigue restored to maximum (muster day = rest day)
- Ration exchange (T1-T6: old ration reclaimed, new ration issued based on QM reputation and supply)
- Quartermaster stock refresh (availability re-rolled based on supply)
- Period statistics recorded (XP, gold, battles, casualties)

**Player Choices:**
- Select pay option (accept, recount, side deal, IOU if tension high)
- Mentor/ignore/haze green recruits (T3+, if cooldown ready)
- Acknowledge promotions (if tier increased during period)
- Review retinue status (T7+ commanders)
- Visit Quartermaster or request leave after muster

---

## Menu Flow

```
[Map] → Muster Day Arrives
   ↓
[FATIGUE RESET] (automatic)
   • Fatigue restored to maximum
   • Logged in period summary
   ↓
[1. MUSTER INTRO]
   • Strategic context flavor text
   • Service record (rank, XP, days served)
   • Period summary (events, battles, XP gained)
   • Health status (if wounded)
   • Option: Continue to Pay Line
   ↓
[2. PAY LINE]
   • Pay status (wages owed, backpay, lord's finances)
   • Options: Accept Pay | Recount | Side Deal | IOU | Discharge
   ↓
[RATION EXCHANGE] (automatic)
   • T1-T6: Previous ration reclaimed (if any), new ration issued based on QM reputation
   • T7+: Officers exempt (provision their own retinue)
   • Low supply (<30%): No ration issued, warning in summary
   • Note: First ration is issued at initial enlistment, not at first muster
   ↓
[3. GREEN RECRUIT] (10-day cooldown, T3+)
   • Nervous recruit joins formation
   • Options: Mentor | Ignore | Haze
   • Skipped if T1-T2 or cooldown active
   ↓
[4. PROMOTION RECAP] (only if tier increased during period)
   • Formal acknowledgment of promotion
   • Shows benefits unlocked, new abilities, date promoted
   • Options: Continue | Visit QM Now
   ↓
[5. RETINUE MUSTER] (T7+ only)
   • Retinue strength, casualties, morale
   • Fallen soldiers memorial
   • Options: Continue | Recruit After Muster
   ↓
[6. MUSTER COMPLETE]
   • All outcomes (pay, ration)
   • XP sources breakdown
   • Period summary (gold, skills, items, unit status)
   • Options: Dismiss | Visit QM | Review Records | Request Leave
   ↓
[Map] → Muster cycle complete, comprehensive report saved
```

### Stage Count by Tier

| Tier | Typical Stages | Notes |
|------|----------------|-------|
| T1-T2 | 2-3 stages | Intro → Pay → Complete |
| T3-T6 | 3-4 stages | + Green Recruit? → Promotion? |
| T7-T9 | 4-5 stages | + Retinue Muster |

**Note:** Stages marked with "?" are conditional (only appear if triggered by cooldowns, tier increases, etc.)

---

## Time Control Behavior

The muster menu includes configurable time pause behavior controlled via `settings.json`:

```json
{
  "PauseGameDuringMuster": true
}
```

### Pause Mode (Default: Enabled)

When `PauseGameDuringMuster = true`:
- Game time stops completely during muster
- Time control buttons disabled (locked)
- Player cannot advance time during muster sequence
- Muster feels like an administrative checkpoint outside game time
- Time controls unlock when exiting muster

**Rationale:** Muster is a strategic decision point reviewing period performance and making choices about pay, equipment, and career progression. Pausing prevents attacks, events, or time-sensitive opportunities from expiring while reviewing comprehensive muster data. This is standard for administrative tasks in strategy games.

### Time-Preserving Mode (Optional)

When `PauseGameDuringMuster = false`:
- Game respects player's current time speed (paused, normal, or fast-forward)
- Player can advance time during muster using speed controls
- Allows waiting for movement or passive activities while reviewing muster
- Advanced player preference for workflow flexibility

**Implementation Details:**
- Pause mode: Forces `Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop` and locks controls
- Time-preserving mode: Uses `QuartermasterManager.CaptureTimeStateBeforeMenuActivation()` pattern and calls `StartWait()` to enable controls

---

## Menu Stages

### 1. Muster Intro

**Menu ID:** `enlisted_muster_intro`

**Purpose:** Provides context for the muster period, displays service record, and shows recent activity.

**Display Elements:**

```
⚔  PAY MUSTER - DAY 24  ⚔
Army of Vlandia

[Strategic context flavor text - varies by situation]

_____ YOUR SERVICE RECORD _____

Rank: Corporal (Tier 3)
Days Served This Term: 48 days
Experience: 2400 / 3000 XP (80% to Free Sword)
Health Status: Fit for duty
Baggage Access: Available (muster window)

This Period: +120 XP (3 battles, training, orders completed)
Fatigue: Restored to full (muster rest day)

_____ EVENTS SINCE LAST MUSTER _____

• Day 13: Dice game in camp - won 15 denars
• Day 16: Battle at Pen Cannoc - Victory (+50 XP)
• Day 18: Extra training session - gained weapon skill (+20 XP)
• Day 21: Comrade repaid borrowed coin
• Day 23: Completed Patrol Duty order (+20 XP, +5 Officer Rep)
• Day 24: Completed Guard Watch order (+15 XP)
• Day 24: Failed Camp Patrol order (-8 Officer Rep, +3 Scrutiny)

[Pay: 156 denars | Supply: 78% | Unit: 2 lost, 1 sick | Battles: 3]
```

**Strategic Context Variations:**

The intro text varies based on the army's current situation (detected via `ArmyContextAnalyzer`):

| Context | Flavor Text |
|---------|-------------|
| Coordinated Offensive | "The host prepares for the Grand Campaign. Spirits are high as the sergeants call the muster." |
| Desperate Defense | "The realm bleeds. Muster is brief—every sword is needed on the line." |
| Siege Operation | "The walls loom above. Muster proceeds in the muddy siege lines." |
| Raid Operation | "Smoke rises in the distance. The company assembles between raids." |
| Patrol Peacetime | "A quiet day on the march. The sergeants call muster at the usual hour." |
| Garrison Duty | "Within the castle walls, the garrison assembles for pay day." |
| Winter Camp | "Snow blankets the camp. Men huddle close as the sergeants call muster." |
| Recruitment Drive | "Fresh faces join the ranks. The company swells as muster is called." |
| Default | "The sergeants call the muster. Your company assembles in formation." |

**Health Status Display:**

| Condition | Display |
|-----------|---------|
| Full health | "Health Status: Fit for duty" |
| Wounded 50-99% | "Health Status: Wounded (X%) - Light duties" |
| Wounded 30-49% | "Health Status: Badly Wounded (X%) - Limited duties" |
| Wounded <30% | "Health Status: Critically Wounded (X%)" |

**Supply Crisis Warning:**

When supply drops below 20%, a warning banner appears:
```
⚠️ CRITICAL SUPPLY - Quartermaster has locked all baggage
```

**Options:**

| Icon | Text | Tooltip | Action |
|------|------|---------|--------|
| 💰 Continue | Proceed to Pay Line | Step forward to receive wages. | → Pay Line stage |

---

### 2. Pay Line

**Menu ID:** `enlisted_muster_pay`

**Purpose:** Present payment options based on lord's financial status and player's situation.

**Display:**

```
💰  PAYMASTER'S LINE  💰

You step forward to the paymaster's table. He opens his ledger.

_____ PAY STATUS _____

Wages Owed:           156 denars
Backpay Outstanding:  0 denars
Lord's Treasury:      Comfortable
```

**Payment Options:**

| Icon | Option | Description | Availability |
|------|--------|-------------|--------------|
| 💰 Continue | Accept Your Pay | Receive wages owed. Lord's finances determine full/partial payment. | Always |
| 🎲 TakeQuest | Demand a Recount | Roguery/Charm check. 120% pay on success. 10 fatigue cost. | Always |
| ⚔ Trade | Quartermaster's Deal | Take 40% pay for surplus equipment. 70% chance (your tier). QM rep modifies chance. 6 fatigue. | Always |
| 📜 Manage | Accept Payment in Arrears | Accept delayed payment until next muster. Wages remain owed. Paid at next muster. | Lord can't afford to pay |
| 🏴 Leave | Process Final Discharge | Process discharge. Receive final pay + pension. Service ends. | Pending discharge |
| ⚠ Leave | Smuggle Out (Deserter) | Desert with your gear. Forfeit pension. Hunted as deserter. | Pending discharge |

**Discharge Confirmation:**

When selecting "Process Final Discharge", a confirmation popup appears showing exactly what the player will receive based on their discharge band:

- **Veteran/Heroic Discharge:** Shows 3000 denars severance, pension details with days served, +30 relation, and gear handling (keep armor, weapons to inventory)
- **Honorable Discharge:** Shows 3000 denars severance, pension details with days served, +10 relation, and gear handling
- **Washout Discharge:** Shows no severance, no pension, -10 relation, and all equipment confiscated

The popup uses the native game's `InquiryData` system with Yes/No buttons. The player must confirm before the discharge is processed.

**Payment Outcomes:**

The "Accept Your Pay" option resolves based on the lord's financial status:

- **Full Payment:** Lord pays all pending wages plus backpay. Pay Tension reduced by 30. Clears consecutive delay counter.
- **Partial Payment:** Lord pays current period + 50% of backpay. Pay Tension reduced by 10. Remaining backpay carries forward.
- **Payment Delayed:** Lord cannot afford wages. Backpay accumulates. Pay Tension increases (10 base + 5 per week overdue).

See [Pay System](pay-system.md) for complete details on wage calculation and payment resolution.

---

### 3. Green Recruit

**Menu ID:** `enlisted_muster_recruit`

**Trigger:** 10-day cooldown, Tier 3+, skipped if on cooldown or T1-T2

**Display:**

```
👥  GREEN RECRUIT  👥

At morning muster, a terrified-looking recruit stands trembling in
the front rank. He's barely holding his spear correctly, and his eyes
dart nervously at every sound.

The veterans are already snickering. As a seasoned soldier, you could
step in—or let him learn the hard way.
```

**Options:**

| Icon | Option | Requirements | Outcome |
|------|--------|--------------|---------|
| 🤝 Conversation | [Leadership 25+] Take Him Aside and Train Him | Leadership 25+ | +15 Leadership XP, +30 Sergeant trait XP, +5 Officer, +6 Soldier, +30 troop XP |
| 🤷 Leave | Let Him Figure It Out | Always | -2 Soldier Rep |
| 😈 OrderTroopsToAttack | Give Him the Traditional Welcome | Always | +4 Soldier, -5 Officer, +8 Discipline |

**Behavior:**
- Integrates `evt_muster_new_recruit` event as menu stage
- Only available to T3+ soldiers (enough experience to mentor)
- Cooldown prevents this from appearing at every muster

---

### 4. Promotion Recap

**Menu ID:** `enlisted_muster_promotion_recap`

**Trigger:** Only if player's tier increased during the 12-day period

**Purpose:** Formal acknowledgment of a promotion that already occurred during the period. The actual promotion happens immediately when requirements are met (via `PromotionBehavior` hourly check and proving event popup). This stage is a ceremonial recap for the service record.

**Display:**

```
⭐  PROMOTION ACKNOWLEDGED  ⭐

The captain addresses the formation. Your promotion is formally 
recognized before the assembled company.

"On Day 20 of this campaign, this soldier was promoted to Sergeant
for distinguished service. The rank has been earned and noted."

The men salute. Your promotion is now part of the official record.

_____ PROMOTION DETAILS _____

Promoted From:       Corporal (Tier 3)
Current Rank:        Sergeant (Tier 4)
Date of Promotion:   Day 20 (8 days ago)

Benefits Unlocked at Promotion:
• Wages increased: 40 → 55 denars/day
• Tier 4 equipment available from Quartermaster
• New authority: Lead small patrols
• Enhanced reputation gains from successful orders

New Abilities Unlocked:
• Request Extra Training (camp decision)
• Organize Squad Drill (camp decision)
• Recommend Comrades for Promotion (social decision)

Current XP Progress: 850 / 6000 XP to Veteran (14%)

The promotion proving event occurred when you earned it. This is a 
formal acknowledgment for your service record.
```

**Options:**

| Icon | Text | Action |
|------|------|--------|
| ✅ Continue | Acknowledge Promotion | Proceed to next stage |
| 🛒 Trade | Visit Quartermaster Now | Flag QM visit → Next stage |

**Behavior:**
- Detects tier change by comparing current tier to cached `TierAtLastMuster`
- Shows date promotion occurred, benefits unlocked, new XP requirements
- Reminds player of newly available equipment and abilities
- See [Promotion System](promotion-system.md) for how promotions trigger

---

### 5. Retinue Muster (T7+ Only)

**Menu ID:** `enlisted_muster_retinue`

**Trigger:** Tier 7+ commanders with active retinue

**Display:**

```
🏴  RETINUE MUSTER  🏴

Your soldiers form up for inspection. You walk the line, checking
each man's gear and bearing. They salute as you pass.

_____ RETINUE STRENGTH _____

Current Strength:    18 / 20 soldiers
Morale:              Steady (62)
Equipment:           Serviceable
Experience:          Veterans (avg 45 days service)

_____ THIS PERIOD _____

Casualties:          2 killed, 1 wounded
Recruits Added:      0
Desertions:          0

_____ FALLEN THIS PERIOD _____

• Otho the Axeman - Killed at Battle of Pen Cannoc
• Brand of Uxhal - Killed in skirmish near Sargot

[2 recruitment slots available]
```

**Options:**

| Icon | Text | Availability | Action |
|------|------|--------------|--------|
| ✅ Continue | Dismiss Retinue | Always | Proceed to muster complete |
| 👥 TroopSelection | Recruit After Muster | Slots available | Flag recruitment → Complete stage |

**Behavior:**
- Shows current retinue composition and morale
- Memorializes fallen soldiers with names and battle locations
- Enables post-muster recruitment if slots available
- See [Retinue System](retinue-system.md) for complete retinue mechanics

---

### 6. Muster Complete

**Menu ID:** `enlisted_muster_complete`

**Purpose:** Comprehensive summary of all muster outcomes and period statistics.

**Display:**

```
⚔  MUSTER COMPLETE - DAY 24  ⚔

Muster is dismissed. The sergeants release the company.

_____ MUSTER OUTCOMES _____

Pay Received:         156 denars (Full Payment)
Ration Issued:        Field Ration (replaces old Hardtack)
Supply Status:        78% - Adequate condition

_____ RANK & PROGRESSION _____

Current Rank:         Corporal (Tier 3)
XP This Period:       +120 XP
Total Experience:     850 / 2000 XP to Sergeant (43%)
Status:               1150 XP needed (~10 days at current pace)

XP Sources This Period:
• Daily service: +60 XP (5 XP × 12 days)
• Battles won: +50 XP (3 engagements)
• Orders completed: +20 XP (patrol duty)
• Training: +20 XP (extra drill session)
• Officer reputation gains: +10 XP (order bonus)

_____ PERIOD SUMMARY (12 Days) _____

Net Gold:            +171 denars (156 pay + 15 gambling)
Reputation Changes:  +6 Officer, +3 Soldier
Skills Improved:     OneHanded +2, Tactics +1, Leadership +1
Items Acquired:      Vlandian Sword (looted), Leather Gloves (purchased)

Unit Status:         2 casualties, 1 sick, morale steady
Next Muster:         Day 36 (12 days)
```

**Options:**

| Icon | Text | Availability | Action |
|------|------|--------------|--------|
| ✅ Leave | Dismiss | Always | Complete muster → enlisted_status menu |
| 🛒 Trade | Visit Quartermaster | Supply >= 15% | Open QM conversation |
| 📋 Manage | Review Service Record | Always | Open service records menu |
| 🏕 Submenu | Request Temporary Leave | Not in combat/siege | Start leave process |
| 📜 Manage | Request Retirement | 252+ days served | Process honorable retirement immediately |
| 📜 Manage | Request Discharge | Always | Process discharge immediately (outcome varies) |

**Behavior:**
- All muster outcomes recorded to `EnlistedNewsBehavior` for news feed
- XP sources breakdown shows where progression came from
- Comprehensive period statistics (gold, skills, items, casualties)
- Direct access to Quartermaster with freshly refreshed stock
- Players can also access baggage via Camp Hub menu throughout muster
- Baggage access remains available for 6 hours after muster completes

**Post-Muster Actions:**
- Quartermaster stock has been refreshed based on current supply level
- Newly unlocked equipment (from tier-up) marked with [NEW] indicator
- Leave requests transition to temporary leave system
- Discharge/retirement requests set pending flag (processes at next muster)
- Full muster report saved to news feed archive

**Discharge/Retirement Options:**

Two separate buttons appear at Muster Complete:

**1. Request Retirement (Greyed out until eligible)**
- **Requirements:** 252+ days of service
- **Tooltip when eligible:** "Complete your term with honor. Receive severance pay and pension. Positive relations."
- **Tooltip when not eligible:** Shows days served and days remaining (e.g., "Retirement requires 252 days of service. You have served 150 days (102 days remaining).")
- **Outcome:** Honorable retirement with full benefits

**2. Request Discharge (Always enabled)**
- **Availability:** Always clickable
- **Tooltip:** Shows expected discharge band outcome based on current service record:
  - **Veteran/Heroic (200+ days, T4+):** "Honorable discharge. Receive severance (3000g) and pension. +30 relation with lord."
  - **Honorable (100-199 days):** "Honorable discharge. Receive severance (3000g) and pension. +10 relation with lord."
  - **Washout (<100 days or negative lord relation):** "Early discharge (washout). No severance or pension. -10 relation. QM-issued items reclaimed from baggage."
- **Confirmation Popup:** When clicked, a confirmation popup appears showing the full breakdown of what the player will receive (severance, pension, relation changes, gear handling) based on the calculated discharge band. The player must confirm before discharge is processed.
- **Outcome:** Processes immediately upon confirmation based on actual discharge band calculation

Both options process the discharge immediately during the muster sequence (no pending flag or waiting for next muster). Both discharge options now show confirmation popups using the native game's `InquiryData` system, allowing the player to review the consequences and cancel if desired.

---

## Rank Progression Display

### Current Rank Information

Displayed in the intro stage and final summary:

```
Rank: Corporal (Tier 3)
Days Served This Term: 48 days
Experience: 850 / 2000 XP (43% to Sergeant)

This Period: +120 XP (3 battles, training, orders completed)
```

**Data Sources:**
- Current tier from `EnlistmentBehavior._enlistmentTier`
- Rank name from `progression_config.json` (culture-specific)
- Days served from `EnlistmentBehavior.DaysServed`
- XP requirements from `progression_config.json` per tier
- Period XP calculated as delta from `_xpAtLastMuster`

### XP Sources Breakdown

Displayed in the final summary:

```
XP Sources This Period:
• Daily service: +60 XP (5 XP × 12 days)
• Battles won: +50 XP (3 engagements)
• Orders completed: +20 XP (patrol duty)
• Training: +20 XP (extra drill session)
• Officer reputation gains: +10 XP (order bonus)
```

**Tracking:**
- `EnlistmentBehavior` maintains `_xpSourcesThisPeriod` dictionary
- `AddEnlistmentXP(amount, source)` method tracks sources
- Top 5 sources displayed in breakdown
- Dictionary reset after muster complete

### Promotion Detection

**Logic:**
- Cache `_tierAtLastMuster` from previous muster
- Compare current tier to cached tier at muster start
- If increased, trigger promotion recap stage
- Show promotion date, benefits unlocked, new requirements

---

## Event Integration

The muster system integrates several events that previously fired randomly:

| Event ID | Previous Trigger | New Trigger | Menu Stage |
|----------|------------------|-------------|------------|
| `evt_muster_new_recruit` | Random camp, 10-day CD | **During muster**, 10-day CD | Green Recruit |

**Benefits of Integration:**
- Event occurs at appropriate time (muster ceremony)
- Player has context (just saw service record, pay status, supply level)
- Event feels part of muster ritual rather than random interruption
- Cooldown prevents repetition every muster

**Event Filtering:**
- `EventPacingManager` filters `evt_muster_new_recruit` from random camp events
- This event only triggers during the muster menu sequence
- Effects still applied via `EventDeliveryManager.ApplyEffects()`

---

## System Integrations

The muster system integrates with numerous systems across the mod:

### EnlistmentBehavior
- Tracks `_payMusterPending` flag during muster sequence
- Records `_lastMusterCompletionTime` for 6-hour baggage access window
- Maintains `_tierAtLastMuster` and `_xpAtLastMuster` for period calculations
- Tracks `_dayOfLastPromotion` for promotion recap display
- Stores `_xpSourcesThisPeriod` dictionary for XP breakdown

### Pay System
- Pay calculation and lord financial status from [Pay System](pay-system.md)
- Payment options (accept, recount, side deal, IOU) resolved via pay handlers
- Pay Tension increases/decreases based on outcomes
- Discharge processing for pending discharge players

### Quartermaster System
- Stock refresh via `QuartermasterManager.RollStockAvailability()` and `RefreshInventoryAtMuster()` at muster completion
- Uses cross-formation equipment discovery (`GetAvailableEquipmentAllFormations()`) to populate inventory with gear from all formations (infantry, archer, cavalry, horse archer)
- Ration exchange for T1-T6 (old ration reclaimed, new ration issued based on QM reputation)
  - First ration is issued at initial enlistment
  - Subsequent musters: exchange old ration for new one
- QM reputation affects baggage inspection options
- Newly unlocked items marked via `UpdateNewlyUnlockedItems()` after tier-up
- Supply <15% blocks QM access entirely (critical supply gate)
- See [Quartermaster System](../Equipment/quartermaster-system.md)

### Baggage Train System
- `BaggageTrainManager.GetCurrentAccess()` returns `FullAccess` during muster
- Muster window grants 6-hour access window after completion
- Supply <20% overrides muster access (Priority 3 Locked state)
- Baggage "caught up" events suppressed during muster
- See [Baggage Train Availability](../Equipment/baggage-train-availability.md)

### Promotion System
- Promotion detection via tier comparison (`_tierAtLastMuster` vs current tier)
- Promotion recap shows benefits unlocked, new abilities, promotion date
- Actual promotion still occurs via `PromotionBehavior` hourly checks and proving events
- See [Promotion System](promotion-system.md)

### Retinue System (T7+)
- Retinue muster stage queries `RetinueManager` for strength, casualties, morale
- Fallen soldiers memorial with names and battle locations
- Post-muster recruitment option if slots available
- See [Retinue System](retinue-system.md)

### News & Reporting
- Period summary queries `EnlistedNewsBehavior.GetPersonalFeedSince(lastMusterDay)`
- Orders summary queries `GetOrderOutcomesSince(lastMusterDay)`
- Muster outcomes recorded via `MusterOutcomeRecord`
- Comprehensive report added to news feed archive
- See [News & Reporting System](../UI/news-reporting-system.md)

### Army Context
- Strategic context flavor text from `ArmyContextAnalyzer.GetCurrentContext()`
- Varies intro text based on situation (offensive, defense, siege, raid, etc.)
- Captured at muster start, doesn't update mid-flow

### Fatigue System
- `EnlistmentBehavior.ResetFatigue()` called at muster start
- Fatigue restored to maximum (muster day = rest day)
- Pay options (recount, side deal) cost fatigue

### Temporary Leave
- Request Leave option in final summary
- Calls `TemporaryLeaveManager.RequestLeave()`
- Available if not in combat/siege and not on probation

---

## Edge Cases & Special Behaviors

### Combat and Capture Interruption

| Scenario | Handling |
|----------|----------|
| **Battle starts during muster** | Muster deferred. `_payMusterPending` remains true. Resumes after battle. |
| **Player captured during muster** | Muster canceled. Pay cycle continues while prisoner. Triggers after release. |
| **Lord dies during muster** | Muster canceled. Discharge process begins. |
| **Player deserts during muster** | Muster canceled. Desertion penalties applied. |

### Supply and Equipment Gating

| Scenario | Handling |
|----------|----------|
| **Supply <30% at muster** | No ration issued. Warning in summary: "No rations available - supplies depleted." |
| **Supply <20% at muster** | Baggage locked (Priority 3 overrides muster access). Banner: "⚠️ CRITICAL SUPPLY - Quartermaster has locked all baggage." |
| **Supply <15% at muster** | QM visit disabled. Tooltip: "Quartermaster unavailable - supplies critically low." |
| **Supply drops <20% mid-muster** | Baggage becomes Locked immediately. Warning if player tries to access. |

### Baggage Access Window

| Scenario | Handling |
|----------|----------|
| **Muster access window** | 6 hours after muster completion. Tracked via `_lastMusterCompletionTime`. |
| **Player defers muster** | Access window also deferred. No special access until muster proceeds. |
| **Mid-muster baggage access** | Allowed via Camp Hub menu. Doesn't interrupt muster flow. |
| **Multiple access sources** | NCO daily window, Commander halt column, and muster window can coexist. Don't conflict. |
| **Access window expires** | Reverts to normal access evaluation after 6 hours. |

### Orders and Events Summary

| Scenario | Handling |
|----------|----------|
| **No orders this period** | Show "No orders assigned this period." |
| **All orders completed** | Show count with positive tone: "Orders: 3 completed, 0 failed - Exemplary service." |
| **All orders failed** | Show count with warning: "Orders: 0 completed, 2 failed - Command is displeased." |
| **No events this period** | Show "A quiet period. No significant events." |
| **OrderManager unavailable** | Show "Order records unavailable." Log warning. |

### Fatigue and Health

| Scenario | Handling |
|----------|----------|
| **Fatigue already at max** | Show "Fatigue: Already well-rested" instead of "Restored to full." |
| **Fatigue was critical (0-4)** | Show "Fatigue: Restored from exhaustion (was 3/24)." |
| **Player critically wounded** | Show "Health Status: Critically Wounded (X%)." |
| **FatigueManager unavailable** | Skip fatigue display. Log warning. |

### Quartermaster Integration

| Scenario | Handling |
|----------|----------|
| **"Visit QM After" from intro** | Sets flag. After muster completes, opens QM conversation automatically. |
| **"Visit QM" from complete** | Opens QM conversation immediately. Player returns to map after (not back to muster). |
| **Stock refresh during muster** | Called in `CompleteMusterSequence()` before allowing QM access. |
| **Newly unlocked items** | Marked [NEW] after tier-up. All skipped tiers' items if multi-tier promotion. |
| **QM hero unavailable** | Show error message. Disable visit option. Player can dismiss muster. |
| **Stock depleted** | Inventory refresh at muster restores quantities. High-tier items limited (2-5), common items more (5-10). |

### Retinue Edge Cases (T7+)

| Scenario | Handling |
|----------|----------|
| **Just got retinue (T7 promotion)** | Show initial status: "Retinue: 20/20 soldiers assigned." |
| **Retinue casualties this period** | Show losses with names: "Retinue Losses: 3 soldiers killed, 1 wounded." |
| **Retinue is full (20/20)** | Show "Retinue at capacity." Disable recruit option. |
| **Retinue wiped out (0 soldiers)** | Show "Your retinue has been lost. All soldiers have fallen." Special memorial tone. |
| **No casualties, no changes** | Show "Your retinue stands ready. No losses this period." |

### Promotion Edge Cases

| Scenario | Handling |
|----------|----------|
| **Multiple tier promotions** | Rare, but possible if player was offline for multiple promotion checks. Show latest tier with aggregate benefits. |
| **Promotion on muster day** | If promotion occurs on same day as muster, recap shows "Promoted today (earlier)". |
| **No promotion data** | If `DayOfLastPromotion` unavailable, show "Promoted recently" without specific day. |

### Leave Request Edge Cases

| Scenario | Handling |
|----------|----------|
| **Request during siege** | Option disabled. Tooltip: "Cannot request leave during active siege." |
| **Request during battle** | Option disabled. Tooltip: "Cannot request leave in combat zone." |
| **On probation** | Option disabled. Tooltip: "Leave denied - currently on probation." |
| **Leave cooldown active** | Show cooldown: "Leave available in X days." |

### Save/Load Edge Cases

| Scenario | Handling |
|----------|----------|
| **Game loaded mid-muster** | Restore `MusterSessionState` from save. Resume at saved stage. |
| **Save corrupted / state missing** | Abort muster. Reset `_payMusterPending = false`. Trigger fresh muster next cycle. |
| **Old save without muster tracking** | Initialize tracking to defaults. First muster shows "Since Enlistment". |

### Error Handling

| Error | Fallback |
|-------|----------|
| **Null EnlistmentBehavior** | Log error, skip muster entirely. Reset pending flag. |
| **Null CompanySupplyManager** | Show supply as "Unknown". Log warning. Continue muster. |
| **Null EnlistedNewsBehavior** | Show "No events recorded." Continue muster. |
| **Stage transition fails** | Log error, jump to Muster Complete stage. Ensure player isn't stuck. |
| **Effect application fails** | Log error, continue muster. Warning in summary: "Some effects may not have applied." |

**Critical Rule:** Player must never get stuck in muster menu. Any unhandled exception logs full error details and forces muster to close, resetting `_payMusterPending = false`.

---

## Configuration

### Muster Timing

**File:** `ModuleData/Enlisted/enlisted_config.json`

```json
{
  "payday_interval_days": 12
}
```

Controls how often muster occurs. Default 12 days with ±1 day jitter.

### Time Control

**File:** `ModuleData/Enlisted/settings.json`

```json
{
  "PauseGameDuringMuster": true
}
```

Controls whether game time pauses during muster (true = pause, false = preserve player's time speed).

### Event Cooldowns

Recruit events have cooldowns to prevent repetition:

| Event | Cooldown | Requirement |
|-------|----------|-------------|
| Green Recruit | 10 days | Tier 3+ |

---

**Implementation Files:**
- `src/Features/Enlistment/Behaviors/MusterMenuHandler.cs` - Main menu handler
- `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` - Muster tracking state
- `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs` - Period summaries and outcomes
- `src/Features/Equipment/Behaviors/QuartermasterManager.cs` - Stock refresh and rations
- `src/Features/Equipment/Behaviors/BaggageTrainManager.cs` - Baggage access during muster

**Content Files:**
- `ModuleData/Languages/enlisted_strings.xml` - All `muster_*` localization strings
- `ModuleData/Enlisted/Events/muster_events.json` - Recruit events
- `ModuleData/Enlisted/enlisted_config.json` - Muster interval configuration
- `ModuleData/Enlisted/settings.json` - Time control behavior setting

