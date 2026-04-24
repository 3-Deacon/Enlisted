# Retinue System

**Summary:** The retinue system provides high-rank enlisted commanders (T7+) with a personal military force. Players select their formation type at T7, manage reinforcements through context-aware trickle and lord requests, track loyalty, respond to narrative events, and lead named veterans through battles. The system transforms gameplay from individual soldier to commander of men.

**Status:** ✅ Current  
**Last Updated:** 2025-12-23  
**Related Docs:** [Promotion System](promotion-system.md), [Core Gameplay](core-gameplay.md), [Training System](../Combat/training-system.md), [News & Reporting](../UI/news-reporting-system.md)

---

## Index

1. [Overview](#overview)
2. [Command Progression](#command-progression)
3. [Formation Selection](#formation-selection)
4. [Granting Recruits](#granting-recruits)
5. [Reinforcement System](#reinforcement-system)
6. [Retinue Loyalty](#retinue-loyalty)
7. [Training & Development](#training--development)
8. [Battle Integration](#battle-integration)
9. [Casualty Tracking](#casualty-tracking)
10. [Retinue Events](#retinue-events)
11. [Post-Battle Incidents](#post-battle-incidents)
12. [Camp Decisions](#camp-decisions)
13. [Named Veterans](#named-veterans)
14. [News Integration](#news-integration)
15. [Technical Details](#technical-details)

---

## Overview

As you advance into the Commander tiers (T7-T9), you are assigned a personal squad of soldiers. These troops are embedded within your formation and look to you for leadership. The retinue system provides:

- **Formation Choice**: Select infantry, archers, cavalry, or horse archers at T7
- **Context-Aware Replenishment**: Reinforcements arrive faster after victories, slower after defeats
- **Relation-Based Requests**: Request reinforcements from your lord with pricing based on relationship
- **Loyalty Tracking**: Maintain your soldiers' morale through choices and care
- **Narrative Events**: 10 retinue-specific events with meaningful command decisions
- **Post-Battle Incidents**: 6 map incidents firing after battles with retinue present
- **Camp Decisions**: 4 commander-specific decisions for inspections, drills, and morale
- **Named Veterans**: Individual soldiers emerge with names, traits, and battle histories
- **News Integration**: Retinue activities appear in Personal Feed and Daily Brief

---

## Command Progression

Your retinue size scales with your enlistment tier:

| Tier | Rank Track | Soldiers | Total Force |
|------|------------|----------|-------------|
| T1-T6 | Enlisted/Officer | 0 | Companions Only |
| T7 | Commander I | 20 | 20 + Companions |
| T8 | Commander II | 30 | 30 + Companions |
| T9 | Commander III | 40 | 40 + Companions |

**Proving Events:**

Promotions to Commander ranks trigger proving events where your lord summons you:
- **T6→T7**: "Commander's Commission" - Accept immediate command or request later via dialog
- **T7→T8**: "Expanded Command" - Lord acknowledges your success, grants 10 more soldiers
- **T8→T9**: "Elite Commander" - Recognition as veteran leader, final 10 soldiers granted

**Player Agency:**

You can decline promotions and request them later via lord dialog. The "My lord, I have proven myself..." option appears when you're eligible for T7+ and have declined.

---

## Formation Selection

### First-Time Selection (T7 Promotion)

When first promoted to T7, you choose your retinue's formation type via dialog:

**Formation Options:**
- **Infantry** - Foot soldiers with sword and shield
- **Archers** - Skilled bowmen for ranged support
- **Cavalry** - Mounted lancers, swift and deadly
- **Horse Archers** - Mounted bowmen of the steppe (culture-restricted)

**Culture Restrictions:**

Horse archers are only available for cultures that historically used them:
- **Available**: Aserai, Khuzait, Empire
- **Not Available**: Vlandia, Battania, Sturgia

**Muster Acknowledgment:**

At the next pay muster after T7 promotion, a **Retinue Muster** stage appears showing:
- Initial retinue capacity (20/20 soldiers)
- Morale and equipment status
- Recruitment options if openings exist
- See [Muster System](muster-system.md) for details

**Selection Dialog:**

```
Title: "Choose Your Retinue"

Setup: "As a Commander, you lead your own soldiers. 
Choose the type of troops you wish to command."

[Infantry] [Archers] [Cavalry] [Horse Archers*]

*Horse Archers shown only for appropriate cultures
```

**Persistent Choice:**

Your selection is stored in `RetinueState.SelectedTypeId` and persists through:
- T8/T9 expansions (no re-selection, additional soldiers match your formation)
- Save/load
- Discharge and re-enlistment (until you enlist with incompatible culture)

**Culture Mismatch Handling:**

If you re-enlist with a lord whose culture doesn't support your previous formation (e.g., had horse archers with Khuzait, now enlist with Vlandia), you must select a new formation type.

---

## Granting Recruits

### Initial Grant at T7

When you accept T7 promotion (via event or dialog):
1. Formation selection dialog appears (if first time at T7)
2. You choose formation type
3. 20 raw recruits of selected type are added to your party
4. Notification: "Twenty raw recruits have joined your command."

### Expansion Grants at T8/T9

**T8 Promotion:**
- Adds 10 additional recruits of your existing formation type
- No formation re-selection
- Notification acknowledges expanded command

**T9 Promotion:**
- Adds final 10 recruits of your existing formation type
- Reaches maximum retinue capacity
- Notification acknowledges elite commander status

### Recruit Quality

**Culture Integration:**

Recruits match your enlisted lord's culture troop tree:
- **Vlandia**: Vlandian Levy, Vlandian Infantry Recruit, etc.
- **Khuzait**: Khuzait Nomad, Khuzait Archer, etc.
- **Empire**: Imperial Recruit, Imperial Vigla Recruit, etc.

**Formation Matching:**

The system selects the lowest-tier troop from your lord's culture that matches your formation:
- Infantry: Finds troops in Infantry formation class
- Archers: Finds troops in Ranged formation class
- Cavalry: Finds troops in Cavalry formation class
- Horse Archers: Finds troops in HorseArcher formation class

---

## Reinforcement System

The reinforcement system uses two mechanisms: automatic context-aware trickle and relation-based manual requests.

### Context-Aware Trickle

Reinforcements arrive automatically based on campaign context. The system tracks your last battle and adjusts trickle rates accordingly.

**Trickle Rates:**

| Context | Rate | Narrative |
|---------|------|-----------|
| **Victory** (within 3 days) | 1 per 2 days | Battle survivors join up |
| **Defeat** (within 5 days) | BLOCKED | Morale recovering |
| **Friendly Territory** | 1 per 3 days | Local levies assigned |
| **At Peace** (5+ days no battle) | 1 per 2 days | Training complete |
| **On Campaign** (default) | 1 per 4 days | Rearguard transfers |

**Context Detection:**

The system uses `RetinueState.LastBattleTime` and `RetinueState.LastBattleWon` to determine context:
- Recent victory: Faster reinforcements (post-battle survivors)
- Recent defeat: Blocked reinforcements (morale must recover)
- Friendly territory: Bonus reinforcements (local recruitment)
- Peace time: Faster reinforcements (training camps productive)

**Friendly Territory Detection:**

Uses `Settlement.FindSettlementsAroundPosition()` to check for friendly settlements within 30 map units of lord's party.

**Contextual Notifications:**

Trickle notifications change based on context:
- Victory: "A survivor from the battle has joined your retinue."
- Friendly Territory: "A local levy has been assigned to your command."
- Default: "A soldier from the rearguard has been transferred to your retinue."

### Request Reinforcements (Relation-Based)

You can manually request reinforcements from your lord via Camp menu, with cost and cooldown based on relationship.

**Access Requirements:**
- Must have selected retinue formation type
- Must be below tier capacity
- Lord relation must be 20+ (blocks hostile lords)

**Relation-Based Terms:**

| Lord Relation | Cost Multiplier | Cooldown | Lord Response |
|---------------|-----------------|----------|---------------|
| **50+** (Pleased) | 0.75× (25% discount) | 7 days | "Your men fought well. I can spare some from the reserves." |
| **20-49** (Neutral) | 1.0× (full cost) | 14 days | "Reinforcements? I'll see what I can do. It won't be free." |
| **<20** (Unfriendly) | BLOCKED | N/A | "Improve your standing first." |

**Cost Calculation:**

```
Base Cost per Soldier = CharacterObject.TroopWage × 10
Adjusted Cost = Base Cost × Relation Multiplier
Total Cost = Adjusted Cost × Missing Soldiers
```

**Request Flow:**

1. Open Camp menu → "Retinue" → "Request Reinforcements from [LORD]"
2. Dialog shows relation tier, cost, cooldown
3. Accept: Pay gold, receive soldiers immediately
4. Cooldown applied (7 or 14 days)

**Menu Status Display:**

The Retinue menu shows:
- Current retinue count vs capacity
- Last battle outcome and days since
- Trickle status:
  - "Replacements delayed (morale recovering)" if post-defeat
  - "New recruit expected: N days" if active
  - "Resume in: N days" if blocked
- Territory status (Friendly / Hostile)
- Next request available date

### Post-Battle Volunteer Event

After victorious battles (T7+ with retinue below capacity), the `evt_ret_post_battle_volunteers` event may fire:

**Event Setup:**

"The battle is won. As your men regroup, you notice soldiers from other units looking your way. Your reputation precedes you, Commander.

A corporal approaches. 'Sir, some of the lads want to join your retinue. What should I tell them?'"

**Options:**
- **Accept Volunteers**: +2-4 soldiers, Leadership XP. Leadership 50+ grants +2 bonus.
- **Request Transfers**: +4 soldiers, -5 lord relation (taking from his forces).
- **Decline**: +5 retinue loyalty (quality over quantity).

**Cooldown:** 5 days

---

## Retinue Loyalty

Your retinue has a loyalty track (0-100) representing their morale and devotion. Starts at 50 (neutral).

### Loyalty Effects

| Loyalty Level | Threshold | Effects |
|---------------|-----------|---------|
| **High** | 80+ | Bonus morale in battle, positive events |
| **Neutral** | 30-79 | Normal operation |
| **Low** | 20-29 | Performance warnings, grumbling events |
| **Critical** | 10-19 | Desertion risk, combat penalties |
| **Near Mutiny** | <10 | Address immediately or lose men |

### Loyalty Modifiers

Actions that affect loyalty:

| Action | Loyalty Change | Context |
|--------|----------------|---------|
| **Share rations with men** | +5 to +10 | Camp decision, costs gold |
| **Lead from front** | +3 | Participate in battles with retinue |
| **Discipline fairly** | -2 to +2 | Event responses |
| **Protect men from orders** | +10 | Refuse dangerous lord orders |
| **Get men killed recklessly** | -10 to -15 | Heavy casualties without cause |
| **Discipline harshly** | -5 to -10 | Brutal punishment choices |
| **Pay for medical care** | +5 | Camp decision, costs gold |
| **Ignore wounded** | -8 | Event choices |
| **Sacrifice men for lord** | -15 | Event choices |

### Loyalty Threshold Events

Special events fire when loyalty crosses thresholds:

**Low Loyalty (<30):**
- `evt_ret_morale_low` - Men grumbling, morale crisis
- Risk: Must address or face desertion

**High Loyalty (>80):**
- `evt_ret_loyalty_high` - Men devoted, would follow anywhere
- Benefit: Combat bonus, better performance

### Daily Brief Integration

Low or high loyalty appears in Daily Brief context lines:
- Low (<30): "Your men are restless. Morale among your retinue is worryingly low."
- High (>80): "Your soldiers are devoted. They would follow you into any fight."

---

## Training & Development

Retinue soldiers develop through service:

**Combat Experience:**
- Soldiers gain XP naturally through battle participation
- XP scales with enemy difficulty
- Veteran retinue members level faster

**Upgrades:**
- Troops follow their native culture's upgrade tree
- Automatic upgrades when XP thresholds met
- Elite troops emerge after multiple battles

**Manual Training:**

The `dec_ret_drill` Camp Hub decision allows active training:

**Cost:** 1-3 fatigue, 4-day cooldown  
**Options:**
- **Hard Training**: Discipline +3, TroopXP +25, Loyalty -5 (harsh)
- **Balanced Training**: Discipline +2, TroopXP +15, Loyalty ±0
- **Light Training**: Discipline +1, TroopXP +8, Loyalty +3 (gentle)

**Leadership Skill Check:**

Leadership 60+ improves training outcomes, reduces injury risk.

---

## Battle Integration

In battle, your companions and retinue form a **Unified Squad**.

**Deployment:**
- All retinue members spawn in your designated formation
- Formation matches your selected type (Infantry/Archers/Cavalry/Horse Archers)
- Companions join the same formation

**Direct Command:**
- You have direct control using F1-F6 command keys
- Order retinue separately from lord's main force
- Position and tactic orders apply to your squad

**Reinforcement Phases:**
- During battle reinforcement waves, your retinue automatically routes to your position
- Named veterans prioritized in spawn order (if implemented)

**Casualty Tracking:**

The system meticulously tracks retinue health:
1. **Pre-Battle Snapshot**: Retinue state recorded before battle
2. **Battle Participation**: Retinue members fight, may be killed/wounded
3. **Post-Battle Reconciliation**: Casualties calculated, state updated
4. **Recovery**: Wounded soldiers recover over time
5. **News Reporting**: Casualties reported in Personal Feed

---

## Casualty Tracking

### Pre-Battle State

Before each battle, the system records:
- Total retinue count
- Healthy vs wounded split
- Named veteran status

### Post-Battle Reconciliation

After battle ends:
1. Compare pre-battle vs post-battle roster
2. Calculate killed retinue soldiers
3. Calculate wounded retinue soldiers
4. Update `RetinueState.TroopCounts` to match actual roster
5. Check named veteran casualties (see [Named Veterans](#named-veterans))

### Sync Protection

The system syncs `RetinueState` with actual party roster:
- On daily tick
- Before displaying retinue menu
- After every battle

This prevents desync between tracked state and actual soldiers.

### News Reporting

Retinue casualties are reported separately from main force:

**Personal Feed:**
- "Your retinue suffered losses: N killed, M wounded."

**Daily Brief:**
- Includes retinue casualties in casualty report section
- Distinguishes retinue losses from lord's troop losses

---

## Retinue Events

10 narrative events fire for T7+ commanders with retinue, creating command decision moments.

**Trigger Requirements:**
- All: `is_enlisted`, `ai_safe`, `has_retinue`, T7-T9
- Contexts: `camp_daily` (automatic daily evaluation)
- Cooldowns: 10-25 days to prevent spam

**Event List:**

| Event ID | Title | Situation | Core Conflict |
|----------|-------|-----------|---------------|
| `evt_ret_soldier_dispute` | Trouble in the Ranks | Two soldiers fighting | Discipline vs. morale |
| `evt_ret_leave_request` | A Soldier's Plea | Soldier asks for leave | Humanity vs. readiness |
| `evt_ret_theft` | Missing Rations | Rations stolen | Trust vs. punishment |
| `evt_ret_veteran_questions` | The Old Hand | Veteran questions order | Authority vs. wisdom |
| `evt_ret_new_recruits` | Raw Meat | New recruits arrive | Harsh welcome vs. gentle |
| `evt_ret_loyalty_test` | Expendable | Lord orders risky mission | Obey vs. protect men |
| `evt_ret_morale_low` | Grumbling | Men are unhappy | Share rations vs. enforce |
| `evt_ret_desertion` | The Runner | Soldier caught fleeing | Report, stop, or let go |
| `evt_ret_promotion` | From the Ranks | Soldier distinguishes self | Promote publicly vs. private |
| `evt_ret_illness` | Fever in Camp | Sickness spreading | Quarantine vs. march on |

**Example Event: Trouble in the Ranks**

```
Setup: "Shouting draws you to the fire pit. Two of your soldiers are at 
each other's throats. Your men watch, waiting to see what their 
commander does."

Options:
1. Discipline both (harsh, order maintained, warmth lost)
2. Investigate (Leadership 50+, best outcome, men respect you)
3. Let them fight (30% injury, honor satisfied)
4. Dismiss (weak leadership, men lose respect)

Effects:
- Option 1: Loyalty -5, Discipline +1, OfficerRep +5
- Option 2: Loyalty +8, SoldierRep +5, Leadership XP +15
- Option 3: Loyalty +3, Discipline +1, 30% one wounded
- Option 4: Loyalty -8, SoldierRep -5
```

**Effect Types Used:**

- `retinueLoyalty`: Modify loyalty track directly
- `retinueGain`: Add soldiers (volunteers, transfers)
- `retinueLoss`: Remove soldiers (casualties, desertion)
- `retinueWounded`: Wound soldiers (accidents, illness)
- Standard effects: `gold`, `officerRep`, `soldierRep`, `discipline`, `skillXp`

---

## Post-Battle Incidents

6 map incidents fire after battles when player has retinue (T7+).

**Trigger:** `leaving_battle` context with `has_retinue` condition  
**Cooldowns:** 10-20 days

**Incident List:**

| Incident ID | Situation | Key Choices |
|-------------|-----------|-------------|
| `mi_ret_wounded_soldier` | One of your men is dying | Stay with him, send surgeon, move on |
| `mi_ret_first_command` | First battle as commander (one-time) | Reflect, rally men, mourn losses |
| `mi_ret_cowardice` | A soldier ran from battle | Discipline, understand (Leadership 60+), ignore |
| `mi_ret_looting` | Men looting before permission | Allow, stop, join |
| `mi_ret_prisoner_mercy` | Enemy begs for mercy | Kill, spare, let men decide (vote) |
| `mi_ret_recognition` | Lord praises your retinue (victory only) | Accept humbly, credit men, boast |

**Example Incident: Wounded Soldier**

```
Setup: "One of your men lies bleeding on the field. The surgeon's tent 
is far. The march waits for no one. Your men watch to see what you do."

Options:
1. Stay with him: +10 Loyalty, 2-hour delay, may save him (Medicine check)
2. Send surgeon: -50 gold, 60% save chance
3. Leave him: -15 Loyalty, "He'll be found"
4. Mercy kill: -5 Loyalty, "Quick end" (if critically wounded)

Effects depend on Medicine skill and choice
```

**First Command Special:**

`mi_ret_first_command` fires only once (999-day cooldown = never repeats). Marks the first time leading retinue in battle, allows reflection on new responsibility.

---

## Camp Decisions

4 commander-specific decisions available in Camp Hub (T7+ with retinue).

**Access:** Camp Hub → Retinue section

| Decision ID | Title | Cost | Cooldown | Effect |
|-------------|-------|------|----------|--------|
| `dec_ret_inspect` | Inspect Your Retinue | 1-2 fatigue | 5 days | +2 to +5 Loyalty, +2 Discipline |
| `dec_ret_drill` | Drill Your Soldiers | 1-3 fatigue | 4 days | ±Loyalty, +8 to +25 TroopXP, +1 to +3 Discipline |
| `dec_ret_share_rations` | Share Your Rations | 20-50 gold | 7 days | +5 to +10 Loyalty, +2 to +5 SoldierRep |
| `dec_ret_address_men` | Address Your Men | 1 fatigue | 10 days | +2 to +8 Loyalty, +1 to +4 Discipline, Leadership check |

**Decision Details:**

### Inspect Your Retinue

Walk through camp, check equipment, talk to men.

**Options:**
- **Thorough Inspection**: 2 fatigue, +5 Loyalty, +2 Discipline
- **Quick Check**: 1 fatigue, +2 Loyalty, +1 Discipline

### Drill Your Soldiers

Active combat training with your retinue.

**Options:**
- **Hard Training**: 3 fatigue, +25 TroopXP, +3 Discipline, -5 Loyalty (brutal)
- **Balanced Training**: 2 fatigue, +15 TroopXP, +2 Discipline, ±0 Loyalty
- **Light Training**: 1 fatigue, +8 TroopXP, +1 Discipline, +3 Loyalty (gentle)

### Share Your Rations

Buy extra food and distribute to men.

**Options:**
- **Feast**: 50 gold, +10 Loyalty, +5 SoldierRep (generous)
- **Modest Share**: 20 gold, +5 Loyalty, +2 SoldierRep

### Address Your Men

Give a speech to your retinue.

**Options:**
- **Inspire**: Leadership check, +8 Loyalty on success, +4 Discipline
- **Discipline Lecture**: +4 Discipline, +2 Loyalty, no check
- **Casual Talk**: +2 Loyalty, +1 Discipline, builds rapport

---

## Named Veterans

Individual soldiers "emerge" with names and stats after surviving multiple battles, creating personal connections.

### Emergence Criteria

**Requirements:**
- Retinue has participated in 3+ battles
- Not at max named veterans (5 max)
- Has living soldiers in retinue

**Emergence Chance:** 15% per eligible battle

**Emergence Timing:** After battle ends, during casualty reconciliation

### Veteran Data

Each named veteran tracks:

```csharp
- Id: Unique identifier (timestamp + random)
- Name: Culture-appropriate name (via NameGenerator)
- BattlesSurvived: Count (starts at 3)
- Kills: Rough count for flavor
- IsWounded: Wounded veterans skip next battle
- Trait: One of five positive traits
- EmergenceTimeInDays: Campaign time when emerged
```

**Traits:**
- **Brave**: Stands firm in danger
- **Lucky**: Survives impossible situations
- **Sharp-Eyed**: Spots threats early
- **Steady**: Reliable under pressure
- **Iron Will**: Never breaks

### Veteran Emergence Notification

When a veteran emerges:

**Notification (Cyan):**
"[Name], a soldier in your retinue, has distinguished themselves in battle."

**News Feed:**
Added to Personal Feed with priority 2.

### Veteran Death

**Death Chance:**

After each battle with casualties:
1. Calculate casualty rate: `casualties / retinue_size`
2. Named veterans have 30% survival bonus
3. Roll death chance per veteran: `casualty_rate × 0.7`
4. Wounded veterans skip death rolls (already wounded, not in battle)

**Death Notification (Red):**
"[Name], who served N battles under your command, has fallen."

**Memorial Event:**

When a veteran dies, `evt_ret_veteran_memorial` is emitted to `StoryDirector` as a non-modal candidate. It surfaces through the news/status feed instead of a popup (demoted in commit `a8d42bf`):

```
Setup: "The men gather around the fire. [Veteran Name] is gone. [Count] 
battles they fought at your side. The soldiers look to you."

Options:
1. Hold Memorial: 2-hour ceremony, +8 Loyalty, +5 SoldierRep
2. Move On: No cost, -3 Loyalty ("Commander doesn't care")
3. Harsh Response: "Death is expected", +2 Discipline, -5 Loyalty
4. Personal Tribute: Share story, +10 Loyalty, +3 SoldierRep, Leadership XP
```

### Veteran Display

**Retinue Menu:**

Shows named veterans with:
- Name
- Battles survived
- Trait
- Wounded status

**Daily Brief:**

If you have 3+ named veterans with high loyalty:
"Your veterans—[Name1], [Name2], [Name3]—are the backbone of your command."

---

## News Integration

The retinue system integrates with the News & Reporting system.

### Personal Feed

Retinue activities appear as dispatches:

**Retinue Events:**
- Automatic via `EventDeliveryManager.AddEventOutcome()`
- All 10 retinue events logged with options chosen
- Format: "[Event Title]: [Outcome]"

**Veteran Activities:**
- Emergence: "[Name] has distinguished themselves in battle."
- Death: "[Name], who served N battles, has fallen."

**Casualties:**
- "Your retinue suffered losses: N killed, M wounded."
- Reported separately from lord's casualties

### Daily Brief

Retinue context appears in multiple sections:

**Company Context Section:**

`BuildRetinueContextLine()` checks retinue status:
- **Under-strength**: "Your retinue is at X of Y. Z replacements needed."
- **Low loyalty**: "Your men are restless. Morale among your retinue is worryingly low."
- **High loyalty**: "Your soldiers are devoted. They would follow you into any fight."

**Casualty Report Section:**

Retinue casualties included in `BuildCasualtyReportLine()`:
- Distinguished from lord's troop casualties
- Shows retinue losses separately

**Skill Progress Section:**

Leadership XP from retinue events/decisions contributes to levelup hints.

### Reputation Changes

Retinue loyalty changes recorded as reputation changes:

```csharp
ReputationChangeRecord {
    Target: "Retinue",
    Delta: +5,
    NewValue: 65,
    Message: "Shared rations with men",
    DayNumber: 1247
}
```

These appear in service record ledger.

---

## Technical Details

### Data Model

**RetinueState (serialized):**

```csharp
public class RetinueState
{
    public string SelectedTypeId { get; set; }          // "infantry", "archers", etc.
    public Dictionary<string, int> TroopCounts { get; set; } // CharacterID -> count
    public int RetinueLoyalty { get; set; }              // 0-100
    public CampaignTime LastBattleTime { get; set; }     // For context trickle
    public bool LastBattleWon { get; set; }              // Victory/defeat tracking
    public List<NamedVeteran> NamedVeterans { get; set; } // Max 5
    public int BattlesParticipated { get; set; }         // Battle count
    public int MaxNamedVeterans => 5;
    
    // Computed properties
    public int TotalSoldiers => TroopCounts?.Values.Sum() ?? 0;
    public bool HasRetinue => !string.IsNullOrEmpty(SelectedTypeId);
    public bool HasTypeSelected => !string.IsNullOrEmpty(SelectedTypeId);
}
```

**NamedVeteran (serialized):**

```csharp
public class NamedVeteran
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int BattlesSurvived { get; set; }
    public int Kills { get; set; }
    public bool IsWounded { get; set; }
    public string Trait { get; set; }
    public float EmergenceTimeInDays { get; set; }
}
```

### Party Architecture

**Player's Hidden Party:**

While enlisted, player uses an invisible party:
- `MobileParty.MainParty.IsVisible = false`
- `MobileParty.MainParty.IsActive = true`
- `MobileParty.MainParty.IgnoreByOtherPartiesTill = 1 year`
- `MobileParty.MainParty.SetMoveEscortParty(lordParty)`

**Retinue Storage:**

Retinue soldiers stored in `MobileParty.MainParty.MemberRoster`:
- Separate from lord's troops (in `lordParty.MemberRoster`)
- Player controls retinue + companions in battle
- Lord controls his own troops

### Effect System

**New Effect Types:**

| Effect | JSON Key | Target | Implementation |
|--------|----------|--------|----------------|
| Retinue Gain | `retinueGain` | Player's roster + state | `ApplyRetinueGain()` |
| Retinue Loss | `retinueLoss` | Player's roster + state | `ApplyRetinueLoss()` |
| Retinue Wounded | `retinueWounded` | Player's roster + state | `ApplyRetinueWounded()` |
| Retinue Loyalty | `retinueLoyalty` | RetinueState | `ApplyRetinueLoyalty()` |

**Implementation:**

Effects are applied in `EventDeliveryManager.cs`:
- Check retinue exists and has soldiers
- Modify roster via `MobileParty.MainParty.MemberRoster`
- Update `RetinueState.TroopCounts` to match
- Display notification with count
- Log to ModLogger

### Custom Conditions

**New Requirement Conditions:**

| Condition | JSON Key | Check |
|-----------|----------|-------|
| Has Retinue | `has_retinue` | `RetinueManager.Instance?.State?.HasRetinue == true` |
| Below Capacity | `retinue_below_capacity` | `TotalSoldiers < GetTierCapacity()` |
| Last Battle Won | `last_battle_won` | `LastBattleWon && daysSinceBattle < 1` |
| Loyalty Low | `retinue_loyalty_low` | `RetinueLoyalty < 30` |
| Loyalty High | `retinue_loyalty_high` | `RetinueLoyalty >= 80` |
| Retinue Wounded | `retinue_wounded` | Has wounded soldiers in roster |

**Implementation:**

Conditions checked in `EventRequirementChecker.CheckCustomCondition()`.

### Files Modified/Created

**Core Files:**
- `src/Features/Retinue/Data/RetinueState.cs` - Added loyalty, battle tracking, veterans
- `src/Features/Retinue/Data/NamedVeteran.cs` - NEW: Veteran data class
- `src/Features/Retinue/Core/RetinueManager.cs` - Added request reinforcements, loyalty methods
- `src/Features/Retinue/Core/RetinueRecruitmentGrant.cs` - Formation selection dialog
- `src/Features/Retinue/Systems/RetinueTrickleSystem.cs` - Context-aware rates
- `src/Features/Retinue/Systems/RetinueCasualtyTracker.cs` - Veteran emergence/death
- `src/Features/Camp/CampMenuHandler.cs` - Enhanced status, relation-based pricing
- `src/Features/Content/EventDefinition.cs` - Added retinue effect properties
- `src/Features/Content/EventCatalog.cs` - Parse retinue effects
- `src/Features/Content/EventDeliveryManager.cs` - Apply retinue effects
- `src/Features/Content/EventRequirementChecker.cs` - Added retinue conditions
- `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs` - Added retinue news methods

**Content Files:**
- `ModuleData/Enlisted/Events/events_retinue.json` - 10 retinue narrative events + volunteer event
- `ModuleData/Enlisted/Events/incidents_retinue.json` - 6 post-battle map incidents
- `ModuleData/Enlisted/Decisions/decisions.json` - 4 retinue camp decisions
- `ModuleData/Languages/enlisted_strings.xml` - 100+ retinue localization strings

### Save/Load Compatibility

All retinue data persists through:
- `RetinueManager.SyncData()` - Serializes RetinueState
- `RetinueState` includes all new fields (loyalty, battle tracking, veterans)
- `NamedVeteran` list serialized as part of state
- No breaking changes to existing save format

---

**End of Document**
