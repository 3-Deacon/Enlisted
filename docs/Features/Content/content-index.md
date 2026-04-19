# Enlisted Content Index

**Summary:** Master list of all narrative content with IDs, titles, descriptions, requirements, effects, and skill checks. This index provides quick reference for all events, decisions, orders, map incidents, and orchestrated opportunities in the mod.

**Status:** ✅ Current  
**Last Updated:** 2026-01-06 (Added supply pressure arc events)
**Related Docs:** [Content Organization Map](content-organization-map.md), [Content System Architecture](content-system-architecture.md), [Orders Content](orders-content.md), [Event System Schemas](event-system-schemas.md), [Orchestrator Spec](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md)

**Diagnostics:** All content systems now include searchable error codes for user support. See [Content System Architecture - Error Codes](content-system-architecture.md#error-codes--diagnostics) for complete code reference and logging features.

---

**Skill Check Column Key:**
- **Bold skill** = Required check, affects outcome
- Skill (action) = Optional check, unlocks better option
- Skill X+ = Minimum skill threshold required
- — = No skill check

---

## Summary

| Category | Count | Description |
|----------|-------|-------------|
| **Orders** | 17 | Military directives from chain of command (see [Orders Content](orders-content.md)) |
| **Order Events** | 84 | Events that fire during order execution (17 order types in `ModuleData/Enlisted/Orders/order_events/`) |
| **Decisions** | 37 | Player-initiated choices from Camp Hub (3 core + 26 camp + 8 medical with sea variants) |
| **Context Events** | 65 | Context-triggered situations (escalation, pay, promotion, retinue, illness, baggage, pressure arcs) |
| **Map Incidents** | 51 | Triggered by map actions (battle, siege, settlement entry/exit, waiting) - includes retinue incidents |
| **Total** | **254** | Full content catalog including all systems (verified by Tools/count_content.py) |

**Training Decisions**: 3 weapon-aware training decisions using dynamic skill XP. Uses `reward_choices` and `dynamic_skill_xp` for equipped weapon training. See [Training System](../Features/Combat/training-system.md) for implementation details.

**Selection Algorithm**: Intelligent selection algorithm implemented. Events fire automatically every 3-5 days based on player role (2× weight for matching), context (1.5× weight for matching), and priority. Global pacing limits prevent spam: max 2 events per day, 8 per week, minimum 6 hours between any automatic events. Limits are config-driven (see `enlisted_config.json` → `decision_events.pacing`). See `EventSelector.cs`, `EventPacingManager.cs`, `MapIncidentManager.cs`, and `GlobalEventPacer.cs`.

---

## Index

| Section | Contents |
|---------|----------|
| [Orders](#orders-17-total) | T1-T3 Basic, T4-T6 Specialist, T7-T9 Leadership |
| [Decisions](#decisions-37-total) | Core, Training, Social, Economic, Recovery, Special, Medical (Land + Sea) |
| [Events](#events-56-total) | Escalation, Pay, Promotion, Retinue, Illness, Baggage |
| [Map Incidents](#map-incidents-51-total) | LeavingBattle, DuringSiege, EnteringTown, EnteringVillage, LeavingSettlement, WaitingInSettlement (includes retinue incidents) |
| [Coverage Summary](#content-coverage-summary) | Complete counts and file organization |

---

## Orders (17 total)

### T1-T3: Basic Soldier

| ID | Name | Premise | Systems |
|----|------|---------|---------|
| `order_guard_duty` | Guard Duty | Stand watch through the night. | Athletics, Officer Rep, Readiness |
| `order_camp_patrol` | Camp Patrol | Walk the perimeter, keep eyes open. | Athletics, Scouting, Officer Rep, Soldier Rep |
| `order_firewood` | Firewood Collection | The camp needs fuel. Grab an axe. | Athletics, Soldier Rep, Morale |
| `order_equipment_check` | Equipment Inspection | Help the quartermaster check gear. | Crafting, Officer Rep, Equipment |
| `order_muster` | Muster Inspection | Stand for inspection before the lord. | Officer Rep, Lord Rep |
| `order_sentry` | Sentry Post | Watch the road. Report anything strange. | Athletics, Perception, Readiness |

### T4-T6: Specialist

| ID | Name | Skill Req | Premise | Systems |
|----|------|-----------|---------|---------|
| `order_scout_route` | Scout the Route | Scouting 40 | Ride ahead, map the terrain, spot threats. | Scouting, Athletics, Tactics, Lord Rep, Gold |
| `order_treat_wounded` | Treat the Wounded | Medicine 40 | The surgeon needs hands. Help with the injured. | Medicine, Athletics, Officer Rep, Soldier Rep, Morale |
| `order_repair_equipment` | Equipment Repair | Crafting 50 | Fix what's broken before the next fight. | Crafting, Engineering, Equipment |
| `order_forage` | Forage Supplies | Scouting 30 | Find food and useful material in the countryside. | Scouting, Athletics, Supplies |
| `order_lead_patrol` | Lead a Patrol | Tactics 50 | Take three men, sweep the eastern woods. | Tactics, Leadership, Scouting, Lord Rep, Gold |
| `order_inspect_defenses` | Inspect Defenses | Engineering 40 | Check the walls, find weaknesses before they do. | Engineering, Tactics, Lord Rep, Readiness |

### T7-T9: Leadership

| ID | Name | Skill Req | Premise | Systems |
|----|------|-----------|---------|---------|
| `order_command_squad` | Command a Squad | Leadership 80, Tactics 70 | You're responsible for twenty men today. | Leadership, Tactics, All Reps, Gold, Renown |
| `order_strategic_planning` | Strategic Planning | Tactics 100 | The lord wants your assessment of the enemy position. | Tactics, Leadership, Lord Rep, Gold, Renown |
| `order_coordinate_supply` | Coordinate Supply | Steward 80 | Organize the baggage train. Keep us fed. | Steward, Leadership, Lord Rep, Supplies, Gold |
| `order_interrogate` | Interrogate Prisoner | Charm 60 | We took a scout. Find out what he knows. | Charm, Roguery, Lord Rep, Gold |
| `order_inspect_readiness` | Inspect Company Readiness | Leadership 100 | Walk the camp. Tell me if we're ready for battle. | Leadership, Tactics, All Reps, Readiness, Morale |

### Order Failure Consequences

Some orders have severe failure penalties beyond reputation loss:

| Order | On Critical Failure |
|-------|---------------------|
| `order_scout_route` | Ambush: **Troop Loss 1-2**, player HP loss |
| `order_lead_patrol` | Patrol lost: **Troop Loss 2-4**, Morale crash |
| `order_forage` | Spoiled food brought back: **Food Loss**, Medical Risk |
| `order_command_squad` | Squad routed: **Troop Loss 3-6**, Lord Rep crash |
| `order_coordinate_supply` | Supply theft: **Food Loss**, Scrutiny on player |

---

## Decisions (37 total)

**Files:**
- `ModuleData/Enlisted/Decisions/decisions.json` (3 core decisions)
- `ModuleData/Enlisted/Decisions/camp_decisions.json` (26 camp decisions)
- `ModuleData/Enlisted/Decisions/medical_decisions.json` (8 medical decisions: 4 land + 4 sea variants)

**Delivery:** Player-initiated from Camp Hub menu (inline selection, not popup)  
**ID Prefix:** `dec_*` (e.g., `dec_training_drill`, `dec_medical_surgeon`, `dec_gamble_high`)  
**System:** Loaded by EventCatalog → filtered by DecisionCatalog → displayed in Camp Hub

**Phase 6G Changes (2026-01-01):**
- **DELETED:** 35 old static decisions (pre-orchestrator)
- **KEPT:** 3 essential decisions (dec_maintain_gear, dec_write_letter, dec_gamble_high)
- **ADDED:** 30 new decisions (26 camp + 4 medical)
- **NET CHANGE:** 38 → 37 decisions (net -1 after Phase 6G cleanup and additions)
- **Features:** Dynamic skill checks (success modified by player skill), illness restrictions (`maxIllness`), Medical Tent menu system replaced

### Core Decisions (3 - Kept from original 38)

| ID | Name | Premise | Systems |
|----|------|---------|---------|
| `dec_maintain_gear` | Maintain Your Gear | Oil, polish, sharpen. Keep it battle-ready. | Equipment, Fatigue, maxIllness: Moderate |
| `dec_write_letter` | Write a Letter Home | Ink, paper, and memories of another life. | Fatigue, Gold |
| `dec_gamble_high` | High Stakes Game | Real money. Real risk. Fortunes change hands. | 45% win 150 gold, +3 rep, +2 Scrutiny. Costs 50 gold. Requires T2+. |

### Training (5)

| ID | Name | Premise | Skill Check | Outcomes | Illness Limit |
|----|------|---------|-------------|----------|---------------|
| `dec_training_drill` | Weapon Drill | The sergeant calls for formation. Men line up with their weapons. | Equipped weapon | +8 XP to equipped weapon. Costs 2 fatigue. | Mild |
| `dec_training_spar` | Sparring Match | A soldier tosses you a practice sword. The crowd forms a circle. | Athletics | 55% success: +3 rep, +6 Athletics XP. Fail: -5 HP, +3 XP. Costs 3 fatigue. | Mild |
| `dec_training_formation` | Formation Practice | Sergeants putting men through shield walls and maneuvers. | Tactics, Polearm | +4 Tactics XP, +3 Polearm XP. Costs 2 fatigue. | Mild |
| `dec_training_veteran` | Learn from Veterans | Old soldiers sharing hard-won knowledge. | — | +5 XP to chosen skill. Costs 1 fatigue. | Mild |
| `dec_training_archery` | Archery Practice | Target practice with bow or crossbow. | Bow/Crossbow | +6 ranged XP. Costs 2 fatigue. | Mild |

### Social (11)

| ID | Name | Premise | Risk | Outcomes | Illness Limit |
|----|------|---------|------|----------|---------------|
| `dec_social_stories` | War Stories | Veterans sharing tales of old campaigns. | — | +2 Soldier rep. -1 fatigue. | Severe |
| `dec_social_storytelling` | Tell a Story | Share your own experiences around the fire. | — | +1 Soldier rep. -1 fatigue. | Severe |
| `dec_social_singing` | Join the Singing | Men singing old songs. Join in or just listen. | — | +1 Morale. -1 fatigue. | Severe |
| `dec_tavern_drink` | Visit the Tavern | Ale, dice, and conversation. | — | +2 Morale. Costs 15 gold, 1 fatigue. | Severe |
| `dec_drinking_contest` | Drinking Contest | Serious drinking. Winner takes the pot. | 35% | Win: 80 gold, +4 rep. Lose: embarrassment. Costs 20 gold, 2 fatigue, +1 Scrutiny. | Mild |
| `dec_arm_wrestling` | Arm Wrestling Match | Test your strength against another soldier. | 45% | Win: 40 gold, +2 rep. Lose: try again later. Costs 15 gold, 1 fatigue. | Mild |
| `dec_gamble_cards` | Card Game | Friendly stakes, good company. | 50% | Win: 25 gold, +1 rep. Lose: -10 gold. Costs 10 gold. | Severe |
| `dec_gamble_dice` | Dice Game | Behind the supply wagons. Higher stakes. | 40% | Win: 75 gold. Lose: -25 gold. Costs 25 gold, +1 Scrutiny. | Severe |

### Economic (6)

| ID | Name | Premise | Skill Check | Outcomes | Illness Limit |
|----|------|---------|-------------|----------|---------------|
| `dec_forage` | Forage for Supplies | Search the area for food and useful materials. | **Scouting** | 60% base: +15 Supplies. Fail: -5 Supplies. Skill-modified. Costs 2 fatigue. | Mild |
| `dec_work_repairs` | Help with Repairs | Wagon wheels, tent patches, harness mending. | — | Earn 30 gold. +1 Soldier rep. Costs 2 fatigue. | Mild |
| `dec_trade_browse` | Browse Merchant Caravan | Traveling merchants with the army. | — | Shop interface. Costs time. | Severe |

### Recovery (5)

| ID | Name | Premise | Outcomes | Illness Limit |
|----|------|---------|----------|---------------|
| `dec_rest_sleep` | Get Some Sleep | Find a quiet spot and close your eyes. | +3 Rest. -3 fatigue. | None |
| `dec_rest_short` | Short Rest | Quick break between duties. | +1 Rest. -1 fatigue. | None |
| `dec_meditate` | Meditate | Clear your mind. Find stillness. | +2 Rest. -2 fatigue. | None |
| `dec_prayer` | Pray | Words of the old prayers. Familiar rhythm. | +1 Rest. -1 fatigue. | None |
| `dec_help_wounded` | Help the Wounded | Clean bandages, water, simple care. | +2 Soldier rep. +3 Medicine XP. -1 Medical Risk. Costs 1 fatigue. | None |

### Special (5)

| ID | Name | Premise | Outcomes | Illness Limit |
|----|------|---------|----------|---------------|
| `dec_officer_audience` | Request Officer Audience | Seek an audience with the captain. | +3 Officer rep. | Severe |
| `dec_mentor_recruit` | Mentor a Recruit | Show a struggling recruit how it's done. | +2 Soldier rep. +1 Officer rep. +5 Leadership XP. Costs 1 fatigue. Requires T3+. | Severe |
| `dec_volunteer_extra` | Volunteer for Extra Duty | Step forward for additional work. | +2 Officer rep. -2 Discipline. Costs 2 fatigue. | Severe |
| `dec_night_patrol` | Night Patrol | Join the perimeter check. | 30% to find 25 gold. +5 Scouting XP. +1 rep. Costs 2 fatigue. | Severe |
| `dec_baggage_access` | Access Baggage Train | Routes to baggage stash OR QM dialogue based on access state. | Direct routing (no popup). | None |

### Medical (4) **NEW - Phase 6G**

**Replaces:** Medical Tent menu system (535 lines deleted from `EnlistedMedicalMenuBehavior.cs`)  
**Requirements:** `hasAnyCondition: true` (appears only when injured/ill/exhausted)  
**Features:** Dynamic skill checks, skill-modified success chances, illness severity restrictions

| ID | Name | Premise | Skill Check | Outcomes | Requirements |
|----|------|---------|-------------|----------|--------------|
| `dec_medical_surgeon` | Surgeon's Treatment | Professional care from the camp surgeon. | **Medicine** | 70% base (+skill mod): +20 HP, -5 Medical Risk. Fail: +5 HP, -2 Risk. Costs 100 gold. | Has any condition |
| `dec_medical_rest` | Rest and Recover | Sometimes the body just needs time. | — | +10 HP. -3 fatigue. -1 Medical Risk. Free but slow. | Has any condition |
| `dec_medical_herbal` | Herbal Remedy | Cheaper than surgeon, if you know what you're doing. | **Medicine** | 50% base (+skill mod): +15 HP, -2 Risk. Fail: +5 HP. Costs 30 gold. | Has any condition |
| `dec_medical_emergency` | Emergency Care | Critical condition requires immediate treatment. | — | Pay: +30 HP, -10 Risk (200 gold). Tough it out: 40% fail = -10 HP, +2 Risk. | Severe/Critical condition |

---

**Note:** Retinue decisions were removed. The 4 retinue-specific decisions (`dec_ret_*`) listed in previous versions no longer exist.

| ID | Name | Premise | Systems |
|----|------|---------|---------|
| `dec_ret_inspect` | Inspect Your Retinue | Walk the line, check gear and morale. | Retinue Loyalty, Discipline |
| `dec_ret_drill` | Drill Your Soldiers | Run combat drills - hard, balanced, or light. | Troop XP, Retinue Loyalty, Discipline |
| `dec_ret_share_rations` | Share Your Rations | Share personal food stores with your men. | Retinue Loyalty, Soldier Rep, Gold |
| `dec_ret_address_men` | Address Your Men | Speak to your retinue - inspire, discipline, or keep it brief. | Retinue Loyalty, Soldier Rep, Discipline, Valor |

---

## Camp Opportunities (36 total)

**File:** `ModuleData/Enlisted/Decisions/camp_opportunities.json`  
**System:** Pre-scheduled by ContentOrchestrator 24 hours ahead  
**Delivery:** All today's opportunities visible in menu (commitment model)  
**Integration:** Each opportunity links to a decision via `targetDecision` field

**How It Works (Phase-Aware Scheduling & Commitment Model - 2026-01-04):**
1. **Daily at 6am:** ContentOrchestrator analyzes world state and pre-schedules opportunities
2. **Phase-aware generation:** For each phase (Dawn/Midday/Dusk/Night):
   - Calls `CampOpportunityGenerator.GenerateCandidatesForPhase(phase)` 
   - Generator overrides context to target phase (not current phase)
   - Ensures candidates are filtered by their target phase's `validPhases`
   - Tracks `alreadyScheduledIds` to prevent same opportunity in multiple phases
3. **Fitness scoring:** All opportunities compete on fitness (context, player needs, history, schedule awareness)
4. **Budget selection:** Top N opportunities per phase selected based on activity level budget
5. **Duplicate prevention:** Each opportunity appears only ONCE per day in its first valid phase
6. **Schedule locking:** Locked once generated (won't change when context shifts)
7. **Menu displays ALL phases:** `GetAllTodaysOpportunities()` shows opportunities for entire day
8. **Player commitment:**
   - **Future-phase opportunities:** Click to schedule → greys out with "[SCHEDULED - {Phase}]"
   - **Current/past-phase opportunities:** Click to fire immediately (no commit for past phases)
   - **Phase transition:** Auto-fires all committed opportunities as popups
   - **Missed opportunities:** Uncommitted opportunities disappear when phase passes
9. **Baggage filtering:** Only shows baggage access when player actually has access (prevents "access denied" frustration)

**Key Benefits:**
- ✅ Players can see and plan for entire day's opportunities
- ✅ Click future opportunities to schedule them (commit)
- ✅ Committed opportunities auto-fire at designated phase
- ✅ Visual feedback (grey out) shows scheduled state
- ✅ Missed opportunities just disappear (no popup spam)
- ✅ Baggage access only appears when accessible
- ✅ Opportunities persist when lord leaves or context changes

**See:** [ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md](../../ORCHESTRATOR-OPPORTUNITY-UNIFICATION.md) for complete architecture and commitment model details.

### Training Opportunities (6)

| ID | Target Decision | Description | Phase | Order Compatibility |
|----|-----------------|-------------|-------|---------------------|
| `opp_weapon_drill` | `dec_training_drill` | Sergeant running drills | Dawn, Midday | Risky on guard, blocked fatigue duty |
| `opp_sparring_match` | `dec_training_spar` | Practice swords, crowd gathering | Dawn, Midday | Risky on guard, blocked fatigue duty |
| `opp_formation_practice` | `dec_training_formation` | Formation drills, shield walls | Dawn, Midday | Blocked on guard/fatigue duty |
| `opp_veteran_spar` | `dec_training_veteran` | Veteran offering harsh lessons | Dawn, Midday | Risky on guard, blocked fatigue duty |
| `opp_archery_range` | `dec_training_archery` | Straw targets, archery practice | Dawn, Midday | Risky on guard |
| `opp_equipment_maintenance` | `dec_maintain_gear` | Oil blade, check gear | Dawn, Midday, Dusk | Available on all orders |

### Social Opportunities (9)

| ID | Target Decision | Description | Phase | Order Compatibility |
|----|-----------------|-------------|-------|---------------------|
| `opp_card_game` | `dec_gamble_cards` | Modest card game near fires | Dusk, Night | Risky on guard/patrol |
| `opp_dice_game` | `dec_gamble_dice` | Higher stakes behind wagons | Dusk, Night | Risky on guard, blocked patrol |
| `opp_war_stories` | `dec_social_stories` | Veterans sharing battle tales | Dusk, Night | Risky on guard |
| `opp_storytelling_circle` | `dec_social_storytelling` | Tale-teller with audience | Dusk, Night | Risky on guard |
| `opp_tavern_visit` | `dec_tavern_drink` | Sutler's tent busy | Dusk, Night | Risky on guard, blocked patrol |
| `opp_drinking_heavy` | `dec_drinking_contest` | Serious drinking contest | Night | Blocked on guard/patrol/fatigue duty |
| `opp_arm_wrestling` | `dec_arm_wrestling` | Strength test, coins wagered | Midday, Dusk | Risky on guard |
| `opp_campfire_song` | `dec_social_singing` | Lute found, singing | Dusk, Night | Risky on guard |
| `opp_letter_writing` | `dec_write_letter` | Scribe offering letters | Midday, Dusk | Available on all orders |

### Economic Opportunities (4)

| ID | Target Decision | Description | Phase | Order Compatibility |
|----|-----------------|-------------|-------|---------------------|
| `opp_foraging` | `dec_forage` | Party supplementing rations | Dawn, Midday | Blocked on guard/patrol/fatigue |
| `opp_repair_work` | `dec_work_repairs` | QM needs paid hands | Dawn, Midday, Dusk | Available on fatigue duty |
| `opp_trade_goods` | `dec_trade_browse` | Merchant caravan arrived | Dawn, Midday, Dusk | Risky on guard |
| `opp_high_stakes_cards` | `dec_gamble_high` | Well-dressed merchants, serious money | Dusk, Night | Blocked on guard/patrol (T3+) |

### Recovery Opportunities (7)

| ID | Target Decision | Description | Phase | Context |
|----|-----------------|-------------|-------|---------|
| `opp_rest_tent` | `dec_rest_sleep` | Bedroll inviting | Night | Land |
| `opp_rest_shade` | `dec_rest_short` | Oak shade | Midday | Land |
| `opp_rest_hammock` | `dec_rest_sleep` | Hammock sways | Night | Sea |
| `opp_meditation` | `dec_meditate` | Quiet spot for reflection | Dawn, Dusk | Any |
| `opp_prayer_service` | `dec_prayer` | Priest holding service | Dawn, Dusk | Any |
| `opp_help_wounded` | `dec_help_wounded` | Surgeon needs hands | Dawn, Midday, Dusk | Any |
| `opp_preventive_rest` | `dec_rest_medical` | Medical pressure building | Dusk, Night | Any (requires medical pressure) |

### Special Opportunities (6)

| ID | Target Decision | Description | Phase | Tier |
|----|-----------------|-------------|-------|------|
| `opp_officer_audience` | `dec_officer_audience` | Captain receiving requests | Midday | 2+ |
| `opp_mentor_recruit` | `dec_mentor_recruit` | Recruit struggling with drills | Dawn, Midday | 3+ |
| `opp_volunteer_duty` | `dec_volunteer_extra` | Extra watch volunteers needed | Dawn, Dusk | 1+ |
| `opp_night_patrol` | `dec_night_patrol` | Unofficial perimeter check | Night | 2+ |
| `opp_baggage_access` | `dec_baggage_access` | Baggage train accessible | Dawn, Midday, Dusk | 1+ |
| `opp_seek_medical_care` | Medical decisions | Surgeon available (has condition) | Dawn, Midday, Dusk | 1+ |
| `opp_urgent_medical` | Medical decisions | Immediate care (severe condition) | All phases | 1+ |

### Sea Variant Opportunities (3)

| ID | Target Decision | Description | Phase | Context |
|----|-----------------|-------------|-------|---------|
| `opp_below_deck_drinking` | `dec_tavern_drink` | Crew passing bottles below deck | Dusk, Night | At sea |
| `opp_ship_maintenance` | `dec_work_repairs` | Rope/sail work needed | Dawn, Midday, Dusk | At sea |
| `opp_sea_shanty` | `dec_social_singing` | Sailors singing on deck | Dusk, Night | At sea |

**Order Compatibility:**
- **Available:** Can do while on this order
- **Risky:** Can attempt, risk getting caught (Officer rep loss, +Discipline, order failure chance)
- **Blocked:** Cannot do while on duty

**Detection System (for risky opportunities):**
- Base chance 15-40% depending on activity
- Night modifier reduces detection (-10 to -20%)
- High reputation reduces detection (-5 to -15%)
- Consequences vary: -5 to -20 Officer rep, +1-3 Discipline, 5-30% order failure risk

**See:** [Content Organization Map](content-organization-map.md) for complete opportunity details.

---

## Events (65 total)

**⚠️ WARNING:** The detailed event listings below may contain outdated IDs and descriptions. This section needs reconciliation with actual JSON files in `ModuleData/Enlisted/Events/`. Use `python Tools/count_content.py` for accurate counts.

**Actual Event Files (66 events total):**
- `events_escalation_thresholds.json` - Scrutiny (5) + Discipline (5) threshold events = 10
- `events_pay_tension.json` - Pay complaints and tension events
- `events_pay_loyal.json` - Pay loyalty and wage satisfaction events
- `events_pay_mutiny.json` - Pay crisis and mutiny events
- (Pay events total: 12 across 3 files)
- `events_promotion.json` - Tier promotion proving events (6)
- `events_retinue.json` - Retinue narrative events for T7-T9 commanders (17)
- `events_baggage_stowage.json` - Baggage onboarding event (1)
- `illness_onset.json` - Medical system illness onset events (4)
- `events_camp_life.json` - Universal camp events (7)
- `pressure_arc_events.json` - Supply pressure narrative arc events (9) **NEW**
- `incidents_*.json` - Map-triggered incidents (6 files, counted separately as Map Incidents below)

**For detailed event listings with actual IDs and content, run:** `python Tools/list_events.py`

**This section previously contained outdated event listings. The actual events are in the JSON files.**

| ID | Threshold | Name | Premise | Options |
|----|-----------|------|---------|---------|
| `evt_scrutiny_2` | 2 | Watchful Eyes | Someone's paying attention to you lately. | Lay low / Act normal / Deflect suspicion |
| `evt_scrutiny_4` | 4 | Questions Asked | An officer pulls you aside. Casual questions, but pointed. | Answer honestly / Lie / Evade |
| `evt_scrutiny_6` | 6 | Under Investigation | They're asking around about you. This is formal now. | Cooperate / Obstruct / Find a scapegoat |
| `evt_scrutiny_8` | 8 | Evidence Found | Someone found something. A witness, a stolen item, a note. | Confess / Deny / Flee |
| `evt_scrutiny_10` | 10 | The Arrest | Soldiers approach with purpose. "Come with us." | Surrender / Fight / Bargain |

### Escalation: Discipline (5)

| ID | Threshold | Name | Premise | Options |
|----|-----------|------|---------|---------|
| `evt_disc_2` | 2 | Verbal Warning | The sergeant pulls you aside. "Don't make this a habit." | Accept / Argue / Shrug it off |
| `evt_disc_4` | 4 | Formal Reprimand | Your name's on a list now. The captain knows. | Apologize / Stay silent / Blame others |
| `evt_disc_6` | 6 | Restricted to Camp | No leaving the perimeter. You're being watched. | Comply / Sneak out anyway / Appeal |
| `evt_disc_8` | 8 | Disciplinary Hearing | Stand before the captain. Explain yourself. | Honest defense / Eloquent excuse / Accept punishment |
| `evt_disc_10` | 10 | Court Martial | This is a trial. The lord himself presides. | Plead mercy / Defend your actions / Demand trial by combat |

### Onboarding: Baggage Stowage (1)

**File:** `ModuleData/Enlisted/Events/events_baggage_stowage.json`  
**Trigger:** First enlistment only (one-time)  
**Requirements:** T1-T6 (not offered to T7+ who keep their gear)

|| ID | Name | Premise | Options |
||----|------|---------|---------|
|| `evt_baggage_stowage_first_enlistment` | Stowing Your Belongings | QM clerk demands storage fee or liquidation. | Pay standard fee (200g + 5%) / Charm waiver (Charm 50%: free, +2 QM rep) / Negotiate price (Trade 50%: 100g flat) / Smuggle items (Roguery 50%: free or 250g + +2 Scrutiny) / Sell all (60% value, clean break) / Abort enlistment (-10 Lord rep) |

**Purpose:** Establishes baggage system, QM relationship, and player agency at enlistment.

### Medical Orchestration: Illness Onset (4) **Phase 6H**

Triggered by `ContentOrchestrator` based on Medical Risk escalation track. Probability: 5% per Medical Risk level + modifiers (fatigue +10%, siege +12%, consecutive high pressure days +5% each). 7-day cooldown, blocked if player already has condition.

| ID | Medical Risk | Name | Premise | Options |
|----|--------------|------|---------|---------|
| `illness_onset_minor` | 3+ | Feeling Unwell | You wake feeling wrong. Head thick, limbs heavy. Could be the food, the cold, the camp fever going around. | "I should take it easy today." (reduces fatigue, -1 Med Risk) / "I'm fine. It's nothing." (50% → Minor illness) / "I should see the surgeon." (10 gold, prevents illness) |
| `illness_onset_moderate` | 4+ | Fever Sets In | You wake drenched in sweat, then shivering, then sweating again. Your tent-mates look worried. | "Go to the surgeon immediately." (15 gold, prevents) / "I've had worse. I'll be fine." (70% → Moderate illness) / "Herbal remedy - willow bark and rest." (50% → Minor illness or prevents) |
| `illness_onset_severe` | 5+ | Collapse | You don't remember falling. One moment standing at muster, the next on the ground. "Get the surgeon!" someone shouts. | "Submit to the surgeon's care." (20 gold, Moderate illness + treatment begins) / "I just need... to rest. I'll be fine." (Severe illness, no treatment, high risk) |
| `untreated_condition_worsening` | 3+ days untreated | Condition Worsening | The pain is getting worse. What started manageable is now constant, stealing sleep and strength. | "You're right. I'll go to the surgeon." (25 gold, begins treatment) / "Just a bit longer. I'll manage." (60% → condition worsens by one level) |

**File:** `ModuleData/Enlisted/Events/illness_onset.json`
**See:** [Injury & Illness System](injury-system.md) for full medical system specification

### Muster Events (6)

Triggered during the 12-day muster cycle. See `quartermaster-dialogue-implementation.md` for dialogue.

| ID | Trigger | Name | Skill Check | Outcome |
|----|---------|------|-------------|---------|
| `evt_muster_ration_issued` | Muster + T1-T6 | Ration Issue | — | +1 Food (quality by QM Rep) |
| `evt_muster_ration_denied` | Muster + Supply <50% | No Ration | — | Player keeps old ration, warning |
| `evt_baggage_clear` | Muster + 30% chance | Baggage Clear | — | Pass inspection |

### Party Crisis Events (Rare)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `evt_desertion_wave` | Desertion | Men are slipping away in the night. Morale has broken. | Stop them / Let them go / Report | **Troop Loss 3-8**, Morale, Discipline |
| `evt_food_theft` | Stolen Rations | Someone took the company's food stores. | Hunt thief / Ration / Forage | **Food Loss**, Supplies, Scrutiny |
| `evt_ambush_patrol` | Patrol Ambushed | The patrol you sent didn't all come back. | Search / Mourn / Avenge | **Troop Loss 1-4**, Morale, Soldier Rep |
| `evt_disease_outbreak` | Camp Fever | Fever sweeps the camp. The surgeon is overwhelmed. | Quarantine / Pray / Move camp | **Troop Loss 2-6**, **Troop Wounded**, Medical Risk |
| `evt_supply_spoilage` | Rotten Supplies | The stores are bad. Worms, mold, sickness waiting. | Destroy / Use carefully / Blame quartermaster | **Food Loss**, Supplies, Medical Risk |

### Supply Pressure Arc (9) **NEW**

**File:** `ModuleData/Enlisted/Events/pressure_arc_events.json`  
**Trigger:** Fires automatically at Day 3, 5, 7 of low supplies (tracked by `CompanySimulationBehavior`)  
**Tier Variants:** Each stage has 3 variants (grunt T1-4, NCO T5-6, commander T7+)

| Stage | Day | Grunt (T1-4) | NCO (T5-6) | Commander (T7+) |
|-------|-----|--------------|------------|------------------|
| Stage 1 | 3 | `supply_pressure_stage_1_grunt` - Thin Rations | `supply_pressure_stage_1_nco` - Your Squad Grumbles | `supply_pressure_stage_1_cmd` - Supply Officer's Report |
| Stage 2 | 5 | `supply_pressure_stage_2_grunt` - Fight at the Cook Fire | `supply_pressure_stage_2_nco` - Your Men Are Fighting | `supply_pressure_stage_2_cmd` - Discipline Breaking Down |
| Crisis | 7 | `supply_crisis_grunt` - Whispers of Desertion | `supply_crisis_nco` - One of Yours Is Leaving | `supply_crisis_cmd` - Desertions Imminent |

**Effects:** Soldier/Officer/Lord Rep, Discipline, Morale, HP, Gold (varies by tier and choice)  
**See:** [Company Events - Pressure Arcs](../../Core/company-events.md#pressure-arc-events)

### Role: Scout (6)

| ID | Name | Premise | Skill Check | Options | Systems |
|----|------|---------|-------------|---------|---------|
| `evt_scout_tracks` | Strange Tracks | Tracks that don't match our patrol routes. | **Scouting** (identify) | Report / Investigate / Set ambush | Scouting XP, Scouting 60+: identify enemy type |
| `evt_scout_enemy_camp` | Enemy Campfire | Smoke on the horizon. | **Scouting** (approach) | Scout closer / Report / Ignore | Scouting 90+: detailed intel without risk |
| `evt_scout_shortcut` | The Shortcut | Faster route, risky terrain. | **Scouting** (navigate) | Take risk / Play safe / Suggest | Scouting 60+: safe shortcut |
| `evt_scout_lost_patrol` | The Lost Patrol | Three men didn't return. | **Scouting** (track) | Search / Report / Wait | Scouting 40+: find them alive |
| `evt_scout_ambush_site` | Perfect Ambush Ground | Ideal terrain. Theirs or ours? | **Tactics** (assess) | Set ambush / Avoid / Report | Tactics 60+: successful ambush |
| `evt_scout_deserter` | The Deserter | Company man hiding in the woods. | Charm (persuade) | Turn in / Let go / Help escape | Honor, Charm 40+: talk him back |

### Role: Medic (6)

| ID | Name | Premise | Skill Check | Options | Systems |
|----|------|---------|-------------|---------|---------|
| `evt_med_triage` | Triage | Too many wounded, not enough hands. | **Medicine** (prioritize) | Officers / Worst wounds / Random | Med 60+: save more lives |
| `evt_med_infection` | The Infection | Wound turning bad. Amputation? | **Medicine** (decide) | Amputate / Save limb / Let him decide | Med 80+: save limb successfully |
| `evt_med_shortage` | Medicine Shortage | Running low on bandages and herbs. | Scouting (find) | Ration / Find alternatives / Steal | Scouting 40+: find herbs |
| `evt_med_dying_man` | The Dying Man | He's not going to make it. | **Medicine** (comfort) | Stay / Give peace / Move on | Med 90+: miracle save possible |
| `evt_med_malingerer` | The Malingerer | Wounds don't match complaints. | **Medicine** (diagnose) | Call out / Let slide / Private warning | Med 40+: certain diagnosis |
| `evt_med_plague_fear` | Plague Whispers | Men whispering about sickness. Panic coming. | **Medicine** (investigate) | Investigate / Calm / Quarantine | Med 60+: identify real vs fear |

### Role: Engineer (5)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `evt_eng_weak_wall` | The Weak Wall | This section of fortification won't hold. | Report / Fix it yourself / Exploit it later | Engineering, Officer Rep |
| `evt_eng_siege_work` | Siege Assignment | They want you supervising construction of siege equipment. | Accept proudly / Delegate / Refuse | Engineering, Leadership, Officer Rep |
| `evt_eng_sabotage` | Sabotage Opportunity | You could weaken their defenses from within. Risky. | Do it / Too dangerous / Report the chance | Engineering, Scrutiny, Lord Rep |
| `evt_eng_bridge` | The Bridge Problem | We need to cross, but the bridge looks unsafe. | Test it / Find another way / Strengthen it first | Engineering, Tactics |
| `evt_eng_supply_cache` | Hidden Cache | You notice the quartermaster hiding supplies. Personal stash? | Report / Blackmail / Ignore | Honor, Scrutiny, Officer Rep |

### Role: Officer (6)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `evt_off_insubordination` | Insubordination | A soldier refuses a direct order. Others are watching. | Punish publicly / Private discipline / Let it go | Leadership, Discipline, Soldier Rep, Officer Rep |
| `evt_off_competing_orders` | Conflicting Orders | The captain said one thing, the lord implies another. | Follow captain / Follow lord / Find middle ground | Leadership, Lord Rep, Officer Rep |
| `evt_off_promotion_rival` | Rival for Promotion | Another soldier is being considered for your advancement. | Compete fairly / Undermine them / Support them | Honor, Officer Rep, Lord Rep |
| `evt_off_morale_crisis` | Morale Collapse | The men are losing heart. Something needs to be done. | Inspiring speech / Extra rations / Punish complainers | Leadership, Morale, Soldier Rep |
| `evt_off_desertion_plot` | Desertion Plot | You overhear men planning to leave tonight. | Report them / Confront privately / Join them | Honor, Discipline, Soldier Rep |
| `evt_off_lords_favor` | The Lord's Favor | The lord singles you out for praise. Others notice. | Accept humbly / Accept proudly / Deflect to others | Lord Rep, Officer Rep, Soldier Rep |

### Role: Operative (5)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `evt_op_black_market` | Black Market Contact | A man offers goods that shouldn't be available. | Buy / Report / Become a regular | Gold, Scrutiny, Roguery |
| `evt_op_bribe` | The Bribe | Someone wants you to look the other way. Good money. | Take it / Refuse / Take it and report | Gold, Honor, Scrutiny |
| `evt_op_information` | Valuable Information | You learn something important. Selling it would be profitable. | Sell / Report / Keep for leverage | Gold, Scrutiny, Lord Rep |
| `evt_op_frame` | Frame Job | You could make someone else take the blame for your problems. | Do it / Too risky / Find another way | Scrutiny, Discipline, Honor |
| `evt_op_double_deal` | Playing Both Sides | Two factions want your loyalty. Both are paying. | Pick one / String both along / Expose them | Gold, Scrutiny, multiple Reps |

### Role: NCO (5 + Training Chain)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `evt_nco_new_recruit` | The New Recruit | Fresh from his village, terrified. Your problem now. | Tough love / Gentle guidance / Assign a mentor | Leadership, Soldier Rep |
| `evt_nco_squad_dispute` | Squad Dispute | Two of your men are at each other's throats. | Let them fight / Mediate / Punish both | Leadership, Discipline, Soldier Rep |
| `evt_nco_equipment_theft` | Missing Equipment | Gear is disappearing. Someone in your squad is responsible. | Investigate / Collective punishment / Cover it up | Scrutiny, Discipline, Soldier Rep |
| `evt_nco_veteran_advice` | The Old Soldier | A veteran offers advice. It contradicts your orders. | Listen / Dismiss / Report | Leadership, Officer Rep, Soldier Rep |
| `evt_nco_initiative` | Taking Initiative | Opportunity to act without orders. Could be great or disastrous. | Act boldly / Wait for orders / Consult quietly | Leadership, Officer Rep, Lord Rep |

### NCO Training Chain (T4-T6 only)

Player can train the lord's T1-T3 troops, giving them XP. Risk of injury to player or trainees.

**Initiation (Decision)**

| ID | Name | Cost | Gate | Premise |
|----|------|------|------|---------|
| `dec_train_troops` | Train the Men | -10 Rest, 3 hrs | Tier 4+, Leadership 40+ | Take a squad of recruits through drills. They'll learn. You'll sweat. |

**Training Outcomes (Weighted Random)**

| ID | Chance | Name | Outcome | Systems |
|----|--------|------|---------|---------|
| `evt_train_success` | 40% | Good Progress | The men improve. You feel like a proper sergeant. | **Troop XP +15**, Leadership XP, Officer Rep +5 |
| `evt_train_excellent` | 15% | Exceptional Session | Something clicks. They're actually getting it. | **Troop XP +30**, Leadership XP, Officer Rep +8, Soldier Rep +5 |
| `evt_train_mediocre` | 25% | Slow Going | They're trying. Results are mixed. | **Troop XP +8**, Leadership XP |
| `evt_train_accident_minor` | 10% | Training Accident | A recruit takes a bad fall. Bruised, not broken. | Troop XP +5, **1 Troop Wounded** |
| `evt_train_accident_player` | 5% | You Go Down | Demonstrating a move, you twist something wrong. | **Player -15 HP**, Leadership XP, Soldier Rep +3 (respect) |
| `evt_train_accident_serious` | 4% | Serious Injury | Blood. Someone's hurt badly. Your fault or not, it happened. | **1 Troop Wounded**, Officer Rep -5, Soldier Rep -3 |
| `evt_train_death` | 1% | Training Death | A practice blade, a bad angle, a life ended. | **1 Troop Loss**, Officer Rep -10, Discipline +2, Morale -5 |

**Chain Events (Triggered by Outcomes)**

| ID | Trigger | Name | Premise | Options |
|----|---------|------|---------|---------|
| `evt_train_officer_notice` | After 3+ successes | Captain's Attention | "I've seen what you're doing with those recruits." | Accept praise / Deflect / Request more responsibility |
| `evt_train_resentment` | After accident | Resentful Trainee | One of the men you hurt is holding a grudge. | Apologize / Ignore / Confront |
| `evt_train_protege` | After 5+ successes | Your Protégé | One recruit follows you like a shadow. Eager to learn. | Mentor him / Push him away / Recommend to officers |
| `evt_train_investigation` | After death | The Investigation | The captain wants answers about what happened. | Truth / Blame equipment / Blame the recruit |

**Skill Modifiers**

| Player Skill | Effect |
|--------------|--------|
| Leadership 60+ | +10% excellent, -3% accident |
| Leadership 80+ | +15% excellent, -5% accident, death → serious |
| Medicine 40+ | Serious → Minor on successful check |
| Athletics 60+ | -2% player injury |

### Universal (8)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `evt_camp_theft` | Theft in Camp | Someone stole from you. You have suspicions. | Accuse / Investigate / Let it go | Soldier Rep, Scrutiny, Discipline |
| `evt_camp_brawl` | The Brawl | Fists are flying. You could intervene or stay clear. | Break it up / Join in / Watch | Soldier Rep, HP, Discipline |
| `evt_camp_rumor` | Rumors About You | People are talking. Some of it's true. | Confront / Ignore / Start counter-rumors | Rep tracks, Discipline |
| `evt_camp_favor` | A Soldier's Favor | Someone asks you to cover for them. Risk, but builds trust. | Help / Refuse / Demand payment | Soldier Rep, Scrutiny |
| `evt_camp_letter` | Letter from Home | News from your old life. Could be good or bad. | Read privately / Share / Burn it unread | Flavor, Morale |
| `evt_camp_confession` | The Confession | Someone tells you something they shouldn't have. | Keep secret / Report / Use it | Honor, Scrutiny, multiple Reps |
| `evt_camp_gamble_invite` | Gambling Invitation | "We've got a game going. Good money if you're skilled." | Join / Decline / Watch and learn | Gold, Soldier Rep, Scrutiny |
| `evt_camp_promotion_offer` | Advancement Opportunity | The captain asks if you're ready for more responsibility. | Accept / Decline / Ask for time | Officer Rep, Lord Rep |

### Food & Supply Events (8)

Events triggered by personal food inventory or company supply status. See `player-food-ration-system.md` for full details.

**Loss Events** (Trigger: Player has 2+ personal food items)

| ID | Name | Premise | Skill Check | Options | Systems |
|----|------|---------|-------------|---------|---------|
| `evt_food_spoilage` | Food Spoilage | Summer heat ruined your personal stores. | — | Accept loss | **-1 Personal Food** |
| `evt_food_rats` | Rats in Camp | Vermin got into your supplies. | **Cunning** (hunt) | Accept / Hunt rats / Set traps | -1 Food, Cunning 50+: no loss |
| `evt_food_theft` | Missing Provisions | A comrade took from your pack. | **Charm** (confront) | Confront / Let slide / Report | -1 Food, Soldier Rep, Discipline |
| `evt_food_battle_damage` | Battle Damage | Your pack was hit. Provisions scattered. | — | Accept loss | -1 to -2 Food (based on intensity) |
| `evt_food_refugee` | Desperate Refugee | A starving family begs for food. | — | Give / Refuse / Sell | -1 Food, Honor, Mercy, Charm |
| `evt_food_checkpoint` | Checkpoint Shakedown | Guards demand tribute. They eye your provisions. | **Roguery** (bluff) | Pay gold / Give food / Refuse | -30g or -1 Food, Roguery 40+: no loss |

**Shortage Events** (Trigger: Player has <3 days food)

| ID | Name | Premise | Skill Check | Options | Systems |
|----|------|---------|-------------|---------|---------|
| `evt_food_starving` | Starving | No food. Your stomach aches. | Various | Buy / Forage / Beg QM / Endure | -10 HP, -5 Morale, or buy food |
| `evt_food_low_warning` | Running Low | Only 2 days of food left. | — | (Info only) | Warning message |

**Supply Crisis Events** (Trigger: Company Supply <30%)

| ID | Name | Premise | Skill Check | Options | Systems |
|----|------|---------|-------------|---------|---------|
| `evt_supply_critical` | Starvation in Camp | The company is out of food. Desperate. | **Scouting** (forage) | Emergency forage / Raid village / Cut rations / Desert | +10% or -30 relation, Morale, Scrutiny |
| `evt_supply_catastrophic` | Supply Collapse | 10% supply. Men are deserting. | — | (Automatic effects) | 1d6 desertions/day, +5 Scrutiny/day |

---

## Map Incidents (51 total) [Already Listed Above - Delete This Duplicate]

**Implementation Status**: ✅ **Content Complete** - MapIncidentManager delivers incidents during battle end, settlement entry/exit, siege operations, and waiting in settlement. All 45 incidents implemented across 6 files matching spec exactly.

**Files**:
- `ModuleData/Enlisted/Events/incidents_battle.json` - 11 LeavingBattle incidents
- `ModuleData/Enlisted/Events/incidents_siege.json` - 10 DuringSiege incidents
- `ModuleData/Enlisted/Events/incidents_town.json` - 8 EnteringTown incidents
- `ModuleData/Enlisted/Events/incidents_village.json` - 6 EnteringVillage incidents
- `ModuleData/Enlisted/Events/incidents_leaving.json` - 6 LeavingSettlement incidents
- `ModuleData/Enlisted/Events/incidents_waiting.json` - 4 WaitingInSettlement incidents

### LeavingBattle (11)

| ID | Name | Premise | Skill Check | Options | Systems |
|----|------|---------|-------------|---------|---------|
| `mi_loot_decision` | Dead Man's Purse | Gold on a corpse. Officers aren't looking. | Roguery (hide) | Take / Leave / Report | Scrutiny, Gold, Honor |
| `mi_wounded_enemy` | The Wounded Enemy | He's not dead yet. He's looking at you. | Medicine (save) | Kill / Spare / Call for aid | Mercy, Valor, Soldier Rep |
| `mi_first_kill` | The First Kill | If this was your first, it's hitting you now. | — | Process / Push down / Seek company | Valor, Morale |
| `mi_officer_notice` | The Captain's Eye | "You fought well today." Public recognition. | Charm (deflect) | Accept humbly / Proudly / Deflect | Officer Rep, Soldier Rep |
| `mi_comrade_down` | Fallen Comrade | Someone you knew is dead. Burial detail needs help. | — | Help / Pay respects / Move on | Soldier Rep, Morale |
| `mi_battlefield_find` | Battlefield Discovery | Something valuable in the mud. | Scouting (assess) | Keep / Turn in / Sell later | Scrutiny, Gold, Officer Rep |
| `mi_triage_call` | Call for Medics | Wounded are screaming. | **Medicine 40+** | Help (save lives) / Carry / Move on | Medicine XP, Soldier Rep, Mercy |
| `mi_enemy_intel` | Enemy Documents | Papers on a dead officer. | **Scouting 30+** | Report / Read first (intel) / Keep quiet | Scouting XP, Lord Rep, Scrutiny |
| `mi_battle_trophy` | Battle Trophy | You could take something to remember this. | — | Take / Leave / Give to comrade | Soldier Rep, Valor |
| `mi_survivors_choice` | The Survivors | Enemy wounded who surrendered. | Leadership (control) | Protect / Ignore / Ensure silence | Mercy, Honor, Discipline |
| `mi_ambush_losses` | Ambush! | Enemy survivors rallied. Men went down. | **Tactics** (rally) | Rally / Retreat / Hold | **Troop Loss 1-3**, HP, Morale |

### DuringSiege (10)

| ID | Name | Premise | Skill Check | Options | Systems |
|----|------|---------|-------------|---------|---------|
| `mi_siege_water` | Water Rations | Wells are low. Men are thirsty. | Scouting (find source) | Share / Hoard / Find source | Soldier Rep, Supplies, Mercy |
| `mi_siege_boredom` | Siege Tedium | Weeks of waiting. Men getting restless. | Leadership (organize) | Organize games / Discipline / Let be | Morale, Discipline |
| `mi_siege_deserter` | Watching the Walls | Someone's going over tonight. | Charm (confront) | Report / Confront / Help | Honor, Discipline, Soldier Rep |
| `mi_siege_sickness` | Camp Fever | Sickness spreading. Latrines? Water? | **Medicine** (investigate) | Investigate / Quarantine / Ignore | Medicine XP, Medical Risk, Morale |
| `mi_siege_assault_prep` | Before the Assault | Tomorrow, the walls. Tonight, prayers. | Leadership (rally) | Prepare / Rally men / Drink | Morale, Valor |
| `mi_siege_opportunity` | Wall Weakness | Weak point in their defenses. | **Engineering 40+** | Report (intel) / Exploit / Keep secret | Engineering XP, Lord Rep, Scrutiny |
| `mi_siege_gambling` | Siege Gambling | Stakes getting higher. | Roguery (win) | Join / Break up / Report | Gold, Discipline, Scrutiny |
| `mi_siege_supply_theft` | Missing Supplies | Food disappearing. Someone's hoarding. | Scouting (investigate) | Investigate / Report / Get cut | Scrutiny, Supplies, Discipline |
| `mi_siege_disease` | The Plague | Men dying in their tents. | **Medicine** (save lives) | Quarantine / Pray / Flee | **Troop Loss 2-5**, Med 60+: reduce loss |
| `mi_siege_spoiled_food` | Spoiled Rations | Half the stores are ruined. | Steward (salvage) | Ration / Forage / Inform officers | **Food Loss**, Steward 40+: reduce loss |

### EnteringTown (8)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `mi_town_tavern` | The Tavern | Off-duty. A drink, a bed, maybe trouble. | Drink moderately / Drink heavily / Stay sober | Soldier Rep, Scrutiny, Gold |
| `mi_town_merchant` | Merchant's Offer | A trader has goods at good prices. Maybe too good. | Buy / Haggle / Walk away | Gold, Trade |
| `mi_town_old_friend` | Familiar Face | Someone from your past is here. This could be good or bad. | Approach / Avoid / Observe first | Flavor, callback |
| `mi_town_message` | Message from Home | A letter waiting at the inn. Addressed to you. | Read now / Read later / Ignore | Flavor, Morale |
| `mi_town_criminal` | Criminal Contact | Someone in the shadows knows your face. They have an offer. | Listen / Refuse / Report | Scrutiny, Gold, Roguery |
| `mi_town_recruitment` | Recruitment Pitch | A merchant company is hiring. Better pay, they say. | Consider / Refuse firmly / Hear them out | Lord Rep, Gold |
| `mi_town_brawl` | Tavern Trouble | Local men don't like soldiers. Words become fists. | Fight / Defuse / Flee | HP, Scrutiny, Soldier Rep |
| `mi_town_information` | Town Gossip | The locals are talking about the war. Some of it's useful. | Listen carefully / Join conversation / Spread disinformation | Scouting, Charm |

### EnteringVillage (6)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `mi_village_gratitude` | Village Welcome | They're grateful for protection. Food, drink, smiles. | Accept graciously / Decline / Take advantage | Soldier Rep, Morale |
| `mi_village_resentment` | Cold Reception | These people don't want soldiers here. Understandable. | Be polite / Demand respect / Leave quickly | Charm, Soldier Rep |
| `mi_village_sick` | The Sick Child | A villager begs for help. Their child is fevered. | Help (Medicine) / Give supplies / Can't help | Medicine, Mercy, Supplies |
| `mi_village_rumor` | Village Rumors | Old women talk. They've seen troops moving to the east. | Listen / Dismiss / Investigate | Scouting |
| `mi_village_theft` | Caught Stealing | A soldier is taking from villagers. You see it happen. | Intervene / Report / Ignore | Honor, Discipline, Soldier Rep |
| `mi_village_recruit` | Eager Youth | A young villager wants to join the company. | Encourage / Discourage / Let him decide | Soldier Rep |

### LeavingSettlement (6)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `mi_leave_hangover` | Morning After | Too much last night. Your head is pounding. | Push through / Rest first / Seek remedy | Rest, Medical Risk |
| `mi_leave_stolen` | Something's Missing | Your coin purse is lighter than it should be. | Accept loss / Investigate / Accuse | Gold, Discipline |
| `mi_leave_farewell` | The Farewell | Someone you met doesn't want you to go. | Linger / Quick goodbye / Silent departure | Flavor, callback |
| `mi_leave_intel` | Road Ahead | Information about what's between here and there. | Pay for details / Scout yourself / Trust luck | Gold, Scouting |
| `mi_leave_stowaway` | The Stowaway | Someone's trying to leave town with the company. | Turn them in / Help hide / Ignore | Scrutiny, Mercy |
| `mi_leave_purchase` | Last-Minute Purchase | A merchant catches you before you leave. Good deal. | Buy / Haggle / Walk on | Gold |

### WaitingInSettlement (4)

| ID | Name | Premise | Options | Systems |
|----|------|---------|---------|---------|
| `mi_wait_opportunity` | Passing Opportunity | A chance to make money, learn something, or cause trouble. | Take it / Pass / Investigate first | Various |
| `mi_wait_encounter` | Chance Encounter | Someone interesting crosses your path. | Engage / Observe / Avoid | Flavor, callback |
| `mi_wait_trouble_brewing` | Trouble Brewing | You sense something about to go wrong. | Prevent / Prepare / Ignore | Various |
| `mi_wait_boredom` | Garrison Tedium | Another day of nothing. The men are getting antsy. | Find activity / Maintain discipline / Let it be | Morale, Discipline |

---

## Retinue Content (T7+ only, 23 total)

### Retinue Narrative Events (17)

**File:** `ModuleData/Enlisted/Events/events_retinue.json`  
**Requirements:** Tier 7+, must have active retinue  
**Topics:** Loyalty tests, veteran stories, discipline challenges, morale management, named veteran development

Retinue events focus on managing your personal command at T7+. Events cover loyalty challenges, veteran interactions, discipline issues, and opportunities to build your retinue's effectiveness. Several events feature named veterans who gain personality and history through repeated interactions.

### Retinue Map Incidents (6)

**File:** `ModuleData/Enlisted/Events/incidents_retinue.json`  
**Trigger:** Post-battle (leaving_battle context)  
**Requirements:** Tier 7+, must have active retinue

| ID | Name | Premise | Systems |
|----|------|---------|---------|
| `mi_ret_casualty` | Retinue Casualty | One of your men went down in the fight. | Retinue Loyalty, Morale |
| `mi_ret_hero` | Battlefield Hero | One of your soldiers distinguished themselves. | Retinue Loyalty, Morale |
| `mi_ret_discipline` | Discipline Issue | Retinue soldier broke formation or disobeyed. | Retinue Loyalty, Discipline |
| `mi_ret_loot` | Retinue Loot | Your men found valuable items on the field. | Gold, Retinue Loyalty |
| `mi_ret_rivalry` | Retinue Rivalry | Two of your soldiers have a grudge forming. | Retinue Loyalty |
| `mi_ret_veteran_moment` | Veteran's Wisdom | A named veteran shares battlefield experience. | Retinue Loyalty, Skill XP |

---

## Content Coverage Summary

### By Category

| Category | Count | JSON File | Status |
|----------|-------|-----------|--------|
| Orders | 17 | `orders_t1_t3.json`, `orders_t4_t6.json`, `orders_t7_t9.json` | ✅ Implemented |
| **Order Events** | 84 | `order_events/*.json` (16 files) | ✅ Implemented |
| **Core Decisions** | 3 | `decisions.json` | ✅ Implemented |
| **Camp Decisions** | 26 | `camp_decisions.json` | ✅ Implemented |
| **Medical Decisions** | 8 | `medical_decisions.json` (4 land + 4 sea) | ✅ Implemented |
| **Camp Opportunities** | 36 | `camp_opportunities.json` | ✅ Implemented |
| **Escalation Events** | 10 | `events_escalation_thresholds.json` | ✅ Implemented |
| Events - Universal | 8 | `events_camp_life.json` | Indexed |
| Events - Food/Supply | 10 | `events_food_supply.json` | Indexed |
| Events - Muster | 6 | `events_muster.json` | Indexed |
| Map Incidents | 45 | `incidents_battle/siege/town/village/leaving/waiting.json` | ✅ Implemented |
| **Retinue Events** | 17 | `events_retinue.json` | ✅ Implemented |
| **Retinue Incidents** | 6 | `incidents_retinue.json` | ✅ Implemented |
| **TOTAL** | **282** | **38 JSON files** | ✅ Complete |

### By Trigger

| Trigger | Count | When |
|---------|-------|------|
| System-assigned | 17 | Orders from chain of command |
| Duty-based | 84 | Order events during duty execution |
| Player-initiated | 37 | Camp Hub menu decisions (direct choice) |
| Orchestrated | 36 | Camp opportunities (pre-scheduled 24hrs ahead) |
| State threshold | 10 | Escalation crossed (Scrutiny/Discipline) |
| Medical pressure | 4 | Illness onset triggers |
| Onboarding | 1 | Baggage stowage (first enlistment) |
| Pay/Promotion/Retinue | 35 | Pay events (12), promotion (6), retinue (17) |
| Universal | 7 | Camp life events available to all |
| LeavingBattle | 11 + 6 retinue | After field battle (17 total) |
| DuringSiege | 10 | Hourly while besieging |
| EnteringTown | 8 | Opening town menu |
| EnteringVillage | 6 | Opening village menu |
| LeavingSettlement | 6 | Selecting leave |
| WaitingInSettlement | 4 | Hourly while waiting |

### Systems Touched

| System | Content Pieces | Notes |
|--------|----------------|-------|
| Soldier Rep | 95+ | Social decisions, camp opportunities, order outcomes, interactions |
| Officer Rep | 75+ | Orders, duty performance, volunteer opportunities, discipline |
| Lord Rep | 45+ | High-tier orders, strategic decisions, loyalty events |
| **Retinue Loyalty** | 27 | T7+ content only (17 events + 6 incidents + opportunities) |
| Gold | 60+ | Gambling opportunities, work, rewards, costs, risky actions |
| Scrutiny | 35+ | Escalation track, risky decisions, smuggling, baggage event |
| Discipline | 30+ | Escalation track, rule-breaking, detection, order failures |
| Medical Risk | 25+ | Injury/illness system, treatment decisions, medical events |
| HP/Injury | 45+ | Combat, sparring, medical events, risky actions, treatment |
| Skills (various) | 180+ | Training opportunities, skill checks, order events, XP rewards |
| Fatigue | 100+ | Activity costs, rest opportunities, recovery decisions |
| **Troop Loss** | 10 | Order critical failures, crisis events (rare, dramatic) |
| **Troop XP** | 15 | NCO training, mentoring opportunities |
| **Food Loss** | 12 | Spoilage, theft, crisis events |
| **QM Rep** | 12+ | Baggage stowage, quartermaster interactions, equipment access |

### Implementation Status

**Current Content Systems:**
- **Advanced Content Features**: Flags, chain events, and reward choices implemented.
- **Map Incidents**: Dynamic delivery during map actions.
- **Ration Exchange**: Automated muster-based logistics.
- **Baggage Check**: Baggage access gating system.
- **News Integration**: Event outcomes reflected in company reports.

| Component | File | Status |
|-----------|------|--------|
| **Opportunity Scheduling** | `ContentOrchestrator.cs` | ✅ Pre-schedules 24h ahead, locks schedule, provides hints |
| **Menu Integration** | `EnlistedMenuBehavior.cs` | ✅ Queries Orchestrator directly, no cache, stable opportunities |
| Event Catalog | `EventCatalog.cs` | ✅ Loads events from JSON, context filtering |
| Event Delivery | `EventDeliveryManager.cs` | ✅ UI popups, sub-choice popups, effect application |
| Requirement Checking | `EventRequirementChecker.cs` | ✅ Tier/role/context/skills/traits/escalation/flags |
| Weighted Selection | `EventSelector.cs` | ✅ Role 2×, context 1.5×, priority modifiers |
| Pacing System | `EventPacingManager.cs` | ✅ 3-5 day windows for narrative events, chain event scheduling |
| Map Incidents | `MapIncidentManager.cs` | ✅ Battle/settlement/siege context events |
| Global Pacing | `GlobalEventPacer.cs` | ✅ Config-driven: max/day, max/week, min hours, eval hours, quiet days, category cooldowns |
| Cooldown Tracking | `EscalationState.cs` | ✅ Per-event + one-time event persistence + flags + global counts |
| Flag System | `EscalationState.cs` | ✅ ActiveFlags with duration, flag conditions |
| Chain Events | `EscalationState.cs` | ✅ PendingChainEvents with delay scheduling |
| Reward Choices | `EventDefinition.cs` | ✅ RewardChoices class, sub-option parsing |

### Next Steps

1. Add more content events to catalog
2. Implement weapon-aware training (see `phase10-combat-xp-training.md`)
3. Implement robust troop training
4. Implement XP feedback in news
5. Implement experience track modifiers

