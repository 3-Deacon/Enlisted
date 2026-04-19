# Enlistment System

**Summary:** The enlistment system manages how players join a lord's service, progress through 9 military ranks (T1-T9), and leave through discharge or desertion. Covers deep technical details of enlistment mechanics, invisible party management, XP progression, wage calculation, baggage handling, grace periods, and service records for re-enlistment.

**Status:** ✅ Current  
**Last Updated:** 2026-01-04 (Bug Fix: Grace period crash when lord dies - visual tracker now checks IsAlive before registration)
**Related Docs:** [Core Gameplay](core-gameplay.md), [Onboarding & Discharge](onboarding-discharge-system.md), [Pay System](pay-system.md), [Promotion System](promotion-system.md)

> **Note:** For high-level gameplay overview, see `core-gameplay.md`. This document provides technical implementation details.

---

## Index

- [Overview](#overview)
- [Purpose](#purpose)
- [Inputs/Outputs](#inputsoutputs)
- [Behavior](#behavior)
- [Discharge & Final Muster](#discharge--final-muster)
- [Re-entry & Reservist System](#re-entry--reservist-system)
- [Experience Tracks & Starting Tier](#experience-tracks--starting-tier)
- [Technical Implementation](#technical-implementation)
- [Edge Cases](#edge-cases)
- [Acceptance Criteria](#acceptance-criteria)

---

## Overview
Core military service functionality that lets players enlist with any lord, follow their armies, participate in military life, earn XP and wages, and advance through a 9-tier rank progression system (T1-T9). The player's party becomes invisible on the map while escorting the lord's party. Military progression is driven by orders from the chain of command, battle participation, and reputation with Lord, Officer, and Soldier factions.

## Purpose
Provide the foundation for military service: enlist with a lord, follow their movements automatically, participate in battles, progress through 9 tiers, and earn wages paid every ~12 days at muster. Promotion is gated by multi-factor requirements (XP, days in rank, battles, reputation, discipline). Re-enlistment benefits depend on discharge type (veteran/honorable/washout/dishonorable/deserter/grace).

## Inputs/Outputs

**Inputs:**
- Player dialog choice to enlist with a lord (kingdom or minor faction)
- Lord availability and relationship status
- Current campaign state (peace/war, lord location, etc.)
- Real-time monitoring of lord and army status

**Outputs:**
- Player joins lord's kingdom as a soldier.
- Player party hidden from map (`IsVisible = false`, Nameplate hidden)
- Player follows enlisted lord's movements (including naval travel)
- Daily wage accrual into muster ledger; periodic pay muster incident (inquiry fallback) handles payouts.
- XP progression and Rank advancement (T1-T9).
- Participation in lord's battles and army activities.
- Reputation tracking (Lord, Officer, Soldier).
- Company Needs management (Readiness, Supply) - Note: Rest removed 2026-01-11.
- Managed discharge or desertion.

Related systems (shipping):
- Discipline can temporarily block promotion until the player recovers (clean service / decay). (Config: `escalation.enabled`, enabled by default.)
- The enlisted status UI (Native Game Menu) shows standing, conditions, and active orders.

## Behavior

**Enlistment Process:**
1. Talk to lord -> Express interest in military service (kingdom flow or minor-faction mercenary flow)
2. Lord evaluates player (relationship, faction status)
3. **Army Leader Restriction**: If player is leading their own army, lord will refuse with roleplay dialog explaining they must disband their army first
4. Player confirms -> Immediate enlistment with safety measures
5. Player party becomes invisible (`IsVisible = false`) and Nameplate removed via Patch
6. Begin following lord and receiving military benefits
7. **Initial Ration Issued** (T1-T6 only): New recruits receive their first ration immediately at enlistment. Quality based on QM reputation (grain/butter/cheese/meat). Commanders (T7+) don't receive issued rations.
8. **First-Enlistment Bag Check** (T1-T6 only, fires once per career):
   - **Equipment Transfer Flow**: When military equipment is issued at enlistment, all civilian equipment (worn and civilian outfit) is automatically moved to party inventory (quest items excluded). This ensures pre-enlistment gear is available for the bag check.
   - Deferred **1 hour** after enlistment; fires as narrative event (`evt_baggage_stowage_first_enlistment`).
   - **Stow it all (200g + 5% value)**: Stashes pre-enlistment civilian gear from inventory into baggage train. Costs 200g base + 5% of total inventory value. **Protected:** Quest items stay with player.
   - **Sell it all (60%)**: Liquidates pre-enlistment civilian gear at 60% value. **Protected:** Quest items stay with player.
   - **Smuggle (Roguery 30+)**: Hard skill check to stow everything without paying the fee. If caught: pay full fee + 1 Scrutiny.
   - **Abort enlistment**: Changes mind and leaves service before it starts. -10 Lord reputation. Party restored to normal state. Lord will not accept the player for 7 days. No other penalties.
   - Baggage is tagged with current faction ID for cross-faction tracking.
   - Skipped at T7+ (commanders have baggage authority).
   - **Abort cooldown**: If the player aborts during bag check, the lord remembers and requires 7 days before accepting them again. Lord gives personalized dialogue about needing to sort out affairs.
9. **Cross-Faction Baggage Transfer**: If player has baggage stored with a different faction, prompted before enlistment:
   - **Send a courier (50g + 5% of value)**: Items arrive in 3 days, posted to personal news feed.
   - **Sell remotely (40%)**: Immediate gold at reduced rate (worse than in-person sale).
   - **Abandon**: Items lost forever.
   - **Grace period exception**: Re-enlisting within same kingdom during grace period skips this prompt.
10. **Minor factions only:** Mirror the lord faction's current wars to the player clan.
11. **Onboarding Events**: New enlistees receive introductory events based on their experience track (green/seasoned/veteran). Events fire in stages (1→2→3→complete) and certain options advance the stage.

**Daily Service:**
- Follow enlisted lord's party movements automatically (invisible escort AI).
- **Strategic Context Awareness**: Experience changes based on faction position (Desperate to Offensive) and 8 distinct strategic contexts (e.g., Grand Campaign, Winter Camp).
- Receive **Orders** every 3-5 days from the chain of command (configurable: `event_window_min_days: 3`, `event_window_max_days: 5`), filtered by strategic tags.
- Manage **Company Needs** (Readiness, Supply) through choices and activities - Note: Rest removed 2026-01-11.
- Participate in battles when lord fights; battles increment the promotion requirement counter.
- Wages accrue daily into muster ledger; paid every ~12 days.
- **XP Sources**: Earned exclusively through gameplay - order completion, order events (duty execution, battle phases), and combat performance (skill XP converted to enlistment XP via SkillSuppressionPatch). No automatic daily/battle XP grants.

**Wage Breakdown (muster ledger):**

Wages accrue daily and are paid every ~12 days (configurable: `payday_interval_days: 12`).

**Formula** (from `enlisted_config.json` → `wage_formula`):
```
Base = 10
+ (Player Level × 1)
+ (Tier × 5)
+ (Total XP ÷ 200)
× Army Bonus (1.2 if lord is in an army)
× Probation Multiplier (0.5 if on probation)
```

**Example**: T3 soldier (level 15, 4000 XP) in an army = (10 + 15 + 15 + 20) × 1.2 = 72 gold/day

Wages accrue daily; paid at muster. If pay is late, Pay Tension rises and triggers corruption/desertion events.

**Tier Progression (T1-T9):**
Rank progression is gated by multi-factor requirements: XP threshold, days in rank, battles survived, reputation levels, and discipline score.

| Tier | XP Required | Generic Title | Empire Title | Authority & Benefits |
|------|-------------|---------------|--------------|----------------------|
| T1 | 0 | Follower | Tiro | Raw recruit; issued T1 gear |
| T2 | 800 | Recruit | Miles | Formation selection unlock |
| T3 | 3,000 | Free Sword | Immunes | Specialist roles; T3 gear |
| T4 | 6,000 | Veteran | Principalis | Squad leadership |
| T5 | 11,000 | Blade | Evocatus | Officer track; T5 gear |
| T6 | 19,000 | Chosen | Centurion | Senior NCO; T6 gear |
| T7 | 30,000 | Captain | Primus Pilus | Commander track; Retinue (20 troops) |
| T8 | 45,000 | Commander | Tribune | Mid commander; Retinue (30 troops) |
| T9 | 65,000 | Marshal | Legate | Senior commander; Retinue (40 troops) |

**Culture-Specific Ranks:**
Rank names are determined by the enlisted lord's culture (all 9 tiers):
- **Empire**: Tiro -> Miles -> Immunes -> Principalis -> Evocatus -> Centurion -> Primus Pilus -> Tribune -> Legate
- **Vlandia**: Peasant -> Levy -> Footman -> Man-at-Arms -> Sergeant -> Knight Bachelor -> Cavalier -> Banneret -> Castellan
- **Sturgia**: Thrall -> Ceorl -> Fyrdman -> Drengr -> Huskarl -> Varangian -> Champion -> Thane -> High Warlord
- **Khuzait**: Outsider -> Nomad -> Noker -> Warrior -> Veteran -> Bahadur -> Arban -> Zuun -> Noyan
- **Battania**: Woodrunner -> Clan Warrior -> Skirmisher -> Raider -> Oathsworn -> Fian -> Highland Champion -> Clan Chief -> High King's Guard
- **Aserai**: Tribesman -> Skirmisher -> Footman -> Veteran -> Guard -> Faris -> Emir's Chosen -> Sheikh -> Grand Vizier
- **Mercenary/Generic**: Follower -> Recruit -> Free Sword -> Veteran -> Blade -> Chosen -> Captain -> Commander -> Marshal

(Source: `progression_config.json` → `culture_ranks` section)

**Reputation System:**
Identity is tracked via three reputation scales:
- **Lord Reputation (0-100)**: Impacted by strategic success and loyalty.
- **Officer Reputation (0-100)**: Impacted by order completion and competence.
- **Soldier Reputation (-50 to +50)**: Impacted by camp activities and shared hardship.

**Orders System:**
Instead of passive assignments, players receive explicit orders from the chain of command:
- **T1-T3**: Basic tasks (guard watch, patrol, firewood collection)
- **T4-T6**: Specialist missions (scouting, medical aid, leading patrols)
- **T7-T9**: Strategic directives (command squads, strategic planning)
Success improves reputation and company needs; failure or declining orders carries heavy penalties.

**See:** [Order Progression System](order-progression-system.md) for complete documentation.

**Promotion Requirements:**

Each promotion requires meeting ALL of the following:

| Promotion | XP Req | Days in Rank | Battles | Soldier Rep | Leader Rel | Max Discipline |
|-----------|--------|--------------|---------|-------------|------------|----------------|
| T1→T2 | 800 | 14 | 2 | ≥0 | ≥0 | <8 |
| T2→T3 | 3,000 | 35 | 6 | ≥10 | ≥10 | <7 |
| T3→T4 | 6,000 | 56 | 12 | ≥20 | ≥20 | <6 |
| T4→T5 | 11,000 | 56 | 20 | ≥30 | ≥30 | <5 |
| T5→T6 | 19,000 | 56 | 30 | ≥40 | ≥15 | <4 |
| T6→T7 | 30,000 | 70 | 40 | ≥50 | ≥20 | <3 |
| T7→T8 | 45,000 | 84 | 50 | ≥60 | ≥25 | <2 |
| T8→T9 | 65,000 | 112 | 60 | ≥70 | ≥30 | <1 |

**Note:** If you decline a promotion when offered, you must request it manually from your NCO via dialog.

**Service Monitoring:**
- Continuous checking of lord status (alive, army membership, etc.)
- 14-day grace period if lord dies, is captured, or army defeated.
- The player clan stays inside the kingdom throughout the grace window.

**Grace Period (Lord Death/Capture):**

When the enlisted lord dies or is captured, a **14-day grace period** begins (configurable: `desertion_grace_period_days: 14`).

**Triggers:**
- Lord killed in battle
- Lord captured by enemy
- Army defeated and lord taken prisoner

**During Grace Period:**
- Player leaves kingdom but all progression is preserved (tier, XP, reputation, troop selection)
- Can re-enlist with any lord in the same kingdom to resume service seamlessly
- No relationship penalties
- Kingdom membership is NOT lost during the 14 days

**Grace Period Options:**
1. **Transfer Service**: Talk to another lord in same kingdom → full tier/XP/reputation preservation, no discharge recorded
2. **Leave Voluntarily**: Request discharge → "Grace" band (100% rep restoration on return, no cooldown)
3. **Wait Out Grace**: If 14 days expire without re-enlisting → automatic deserter discharge (-30 relation, +30 crime, 90-day block)

**Grace Period Transfer vs. Normal Transfer:**
- **Grace Transfer**: Preserves exact tier, XP, and reputation (not player's fault lord died)
- **Leave Transfer**: Standard leave mechanics, must manually request from Camp menu

## Discharge & Final Muster

Managed discharge is requested from the Muster Complete menu during muster. When requesting discharge, a confirmation popup appears showing exactly what the player will receive based on their service record (severance pay, pension, relation changes, gear handling). The player must confirm before discharge processes.

Six discharge bands determine re-enlistment outcomes:

| Discharge Band | Trigger | Cooldown | Relation Changes | Re-Entry Effects |
|----------------|---------|----------|------------------|------------------|
| **Veteran** | 200+ days, T4+ | 0 days | +30 lord, +15 faction | Start T4, +1000 XP, 75% rep restore |
| **Honorable** | 100-199 days, neutral+ | 0 days | +10 lord, +5 faction | Start T3, +500 XP, 50% rep restore |
| **Washout** | <100 days OR negative rep | 30 days | -10 all | Start T1, probation |
| **Dishonorable** | Discipline = 10 (kicked out) | 90 days | -20 all | Start T1, probation, scrutiny |
| **Deserter** | Abandoned service | 90 days | -30 all, +30 crime | Start T1, probation, criminal |
| **Grace** | Lord died/captured | 0 days | None | Full restoration (100% rep) |

**Severance & Pension** (from `enlisted_config.json` → `retirement`):
- **Honorable**: 3,000g severance + 50g/day pension
- **Veteran**: 10,000g severance + 100g/day pension
- Pension continues until lord relation drops below 0

## Re-entry & Reservist System

**Re-Enlistment Blocks:**
- **Washout**: 30-day block
- **Dishonorable/Deserter**: 90-day block
- **Honorable/Veteran/Grace**: No block (immediate re-entry)

**Reservist Benefits** (only applies when re-enlisting with same faction):

| Prior Discharge | Starting Tier | Bonus XP | Reputation Restore |
|----------------|---------------|----------|-------------------|
| Veteran | T4 | +1,000 | 75% (3/4 of saved) |
| Honorable | T3 | +500 | 50% (1/2 of saved) |
| Grace | Preserved tier | Half XP | 100% (full restore) |
| Washout/Dishonorable/Deserter | T1 | 0 | 0% |

**Probation Status** (bad discharges):
- Duration: 12 days (configurable: `probation_days`)
- Wage: 50% penalty (`probation_wage_multiplier: 0.5`)
- Fatigue Cap: Reduced to 18/24 (`probation_fatigue_cap: 18`)

## Experience Tracks & Starting Tier

The starting tier for a new enlistment is determined by player level (experience track) and prior service history:

**Experience Tracks** (from `ExperienceTrackHelper.cs`):

| Track | Player Level | Base Tier | Training XP Modifier |
|-------|--------------|-----------|---------------------|
| Green | 1-9 | T1 | +20% (learns quickly) |
| Seasoned | 10-20 | T2 | Normal (1.0×) |
| Veteran | 21+ | T3 | -10% (diminishing returns) |

**Starting Tier Calculation:**
1. **Base Tier**: Determined by experience track (T1-T3)
2. **Faction History Bonus**: If returning to same faction with good service → `HighestTier - 2` (capped at T3)
3. **Reservist Bonus**: If veteran/honorable discharge → T3/T4 (overrides faction bonus)
4. **Bad Discharge Penalty**: Washout/dishonorable/deserter → T1 (probation), no faction bonus

**Example Calculations:**
```
Level 25 Player, First Enlistment:
  Track: Veteran → Base: T3 → Final: T3

Level 15 Player, Returning (Reached T5, Honorable Discharge):
  Track: Seasoned → Base: T2 → Reservist: T3 → Final: T3

Level 30 Player, Returning (Reached T6, Veteran Discharge):
  Track: Veteran → Base: T3 → Reservist: T4 → Final: T4

Level 10 Player, Returning (Deserter):
  Track: Seasoned → Base: T2 → Bad Discharge: T1 → Final: T1 (probation)
```

## Technical Implementation

**Core Files:**
- `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`: Core service state, lord tracking, party following, bag check, grace period, wage calculation
- `src/Features/Ranks/Behaviors/PromotionBehavior.cs`: Multi-factor promotion requirements (T1-T9), proving events
- `src/Features/Orders/Behaviors/OrderManager.cs`: Chain of command orders, pacing, rewards
- `src/Features/Escalation/EscalationManager.cs`: Triple reputation system (Lord, Officer, Soldier), discipline, scrutiny
- `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`: Native game menu implementation (Camp Hub, Reports)
- `src/Features/Company/CompanyNeedsManager.cs`: Company needs simulation (Readiness, Supply) - Note: Rest removed 2026-01-11
- `src/Features/Retinue/Core/ServiceRecordManager.cs`: Per-faction service records, discharge bands, reservist bonuses
- `src/Features/Content/ExperienceTrackHelper.cs`: Experience track calculation, starting tier logic (T1-T3 base)

**Configuration Files:**
- `ModuleData/Enlisted/enlisted_config.json`: Core gameplay config (grace period, wages, pacing, probation)
- `ModuleData/Enlisted/progression_config.json`: Tier XP thresholds, culture-specific ranks, promotion benefits
  - **Note**: `xp_sources` section (daily_base, battle_participation, xp_per_kill) is legacy; XP is now awarded through order system and combat skill conversion
- `ModuleData/Enlisted/Orders/*.json`: Order definitions (17 total orders across T1-T9)
- `ModuleData/Languages/enlisted_strings.xml`: All localized strings

## Edge Cases

**Army Leadership:**
- Player leading an army cannot enlist (dialog blocks with explanation)
- Must disband army first via kingdom menu

**Cross-Faction Enlistment:**
- Switching factions triggers baggage transfer prompt (unless grace period same-kingdom)
- Bad discharge blocks from same faction for 30-90 days

**Grace Period Edge Cases:**
- Courier baggage arrival during grace period: deferred until re-enlistment
- Player captured during grace period: grace timer continues
- Grace expires during player captivity: deserter penalties applied on release
- **Lord dies (not captured)**: Visual tracker only registers lord if alive, preventing crash when attempting to re-enlist

**Promotion Edge Cases:**
- Declining a promotion requires manual request from NCO to receive it later
- High discipline (≥8) temporarily blocks promotion until decay reduces it
- Missing promotion requirements: tooltip shows exact blockers

**Baggage Edge Cases:**
- Bag check fires 1 hour after enlistment (never blocks enlistment flow)
- T7+ commanders skip bag check entirely (assumed baggage authority)
- Smuggle attempt failed: item confiscated, no reputation penalty (expected risk)
- **Save/Load During Bag Check:** If player saves while the bag check popup is visible, the event is properly restored on load (prevents duplicate events)
- **One-Time Trigger:** Baggage check only fires on first enlistment or after retirement/desertion; normal re-enlistments (lord defeated, etc.) skip it
- **Bug Fix (2026-01-03):** Fixed issue where baggage dialog was incorrectly firing multiple times during active service due to (1) improper flag resets in EnlistmentBehavior and (2) EventSelector picking onboarding events as regular camp events

**Mercenary Faction Sync:**
- **Lord's Clan Joins Kingdom:** Player automatically joins as mercenary (maintains battle eligibility)
- **Lord's Clan Leaves Kingdom:** Player automatically leaves kingdom (follows lord, not desertion)
- **Lord's Clan Switches Kingdoms:** Player leaves old kingdom and joins new kingdom (seamless transfer)
- **On Leave:** Faction sync applies even while on leave (commitment to lord's service persists)
- **During Captivity:** Faction sync deferred until release (prevents captivity system conflicts)
- **Mid-Battle:** Faction change logged with warning but attempted (participation already determined)
- This mirrors native Bannerlord behavior where soldiers follow their employer's allegiances

## Acceptance Criteria

**Enlistment:**
- [x] Player can enlist with any lord (kingdom or minor faction)
- [x] Player party becomes invisible and follows lord automatically
- [x] Bag check fires 1 hour after enlistment (T1-T6 only)
- [x] Cross-faction baggage transfer prompt works correctly
- [x] Army leaders blocked from enlisting until army disbanded

**Progression:**
- [x] XP awarded through gameplay: order completion, order events, and combat skill conversion
- [x] Promotion requirements enforced (XP, days, battles, rep, discipline)
- [x] All 9 tiers (T1-T9) achievable with correct XP thresholds
- [x] Culture-specific rank names display correctly (all 6 cultures + mercenary)

**Wages:**
- [x] Wages accrue daily using correct formula (base + level + tier + XP/200 × modifiers)
- [x] Payday occurs every ~12 days
- [x] Probation reduces wage by 50%

**Discharge:**
- [x] All 6 discharge bands function correctly (veteran/honorable/washout/dishonorable/deserter/grace)
- [x] Re-enlistment blocks enforced (30/90 days for bad discharges)
- [x] Severance and pension paid for veteran/honorable
- [x] Reputation snapshots saved and restored correctly

**Grace Period:**
- [x] 14-day grace period triggers on lord death/capture
- [x] Player can transfer within same kingdom without penalties
- [x] Grace expiration triggers deserter discharge
- [x] 100% reputation restoration on grace discharge

**Service Records:**
- [x] Per-faction records track days, battles, kills, tiers
- [x] Reservist bonuses apply correctly on re-enlistment
- [x] Experience tracks determine starting tier (green/seasoned/veteran)
