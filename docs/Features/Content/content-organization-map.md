# Content Organization Map

**Summary:** Visual hierarchy showing how all content types relate to each other and where to add new content. Use this as a quick reference when creating new events, orders, or decisions.

**Status:** ✅ Current
**Last Updated:** 2026-01-04 (Phase-aware scheduling, duplicate prevention)
**Related Docs:** [Content Index](content-index.md), [Content System Architecture](content-system-architecture.md)

---

## Content Hierarchy Tree

```
ENLISTED MOD CONTENT (275 pieces)
│
├── 📁 ORDERS (17 military directives)
│   │
│   ├── T1-T3: Basic Soldier (6 orders)
│   │   ├── order_guard_post
│   │   │   └── 🎭 order_events/guard_post_events.json (6 events)
│   │   ├── order_camp_patrol
│   │   │   └── 🎭 order_events/camp_patrol_events.json (5 events)
│   │   ├── order_firewood
│   │   │   └── 🎭 order_events/firewood_detail_events.json (5 events)
│   │   ├── order_equipment_check
│   │   │   └── 🎭 order_events/cleaning_events.json (3 events)
│   │   ├── order_muster
│   │   │   └── 🎭 order_events/muster_events.json (3 events)
│   │   └── order_sentry
│   │       └── 🎭 order_events/sentry_duty_events.json (5 events)
│   │
│   ├── T4-T6: Specialist (6 orders)
│   │   ├── order_scout_route
│   │   │   └── 🎭 order_events/scout_events.json (8 events)
│   │   ├── order_treat_wounded
│   │   │   └── 🎭 order_events/medical_events.json (6 events)
│   │   ├── order_repair_equipment
│   │   │   └── 🎭 order_events/repair_events.json (5 events)
│   │   ├── order_forage
│   │   │   └── 🎭 order_events/forage_events.json (6 events)
│   │   ├── order_lead_patrol
│   │   │   └── 🎭 order_events/patrol_lead_events.json (8 events)
│   │   └── order_inspect_defenses
│   │       └── 🎭 order_events/defenses_events.json (4 events)
│   │
│   └── T7-T9: Leadership (5 orders)
│       ├── order_command_squad
│       ├── order_strategic_planning
│       ├── order_coordinate_supply
│       ├── order_interrogate
│       └── order_inspect_readiness
│           └── 🎭 84 total order events across 16 event files
│
├── 📁 DECISIONS (37 player-initiated actions, 4 files)
│   │
│   ├── Core Decisions (3) - decisions.json
│   │   ├── dec_maintain_gear
│   │   ├── dec_write_letter
│   │   └── dec_gamble_high
│   │
│   ├── Camp Decisions (26) - camp_decisions.json
│   │   ├── Training (5)
│   │   │   ├── dec_training_drill
│   │   │   ├── dec_training_spar
│   │   │   ├── dec_training_formation
│   │   │   ├── dec_training_veteran
│   │   │   └── dec_training_archery
│   │   ├── Social (8)
│   │   │   ├── dec_social_stories
│   │   │   ├── dec_social_storytelling
│   │   │   ├── dec_social_singing
│   │   │   ├── dec_tavern_drink
│   │   │   ├── dec_drinking_contest
│   │   │   ├── dec_arm_wrestling
│   │   │   ├── dec_gamble_cards
│   │   │   └── dec_gamble_dice
│   │   ├── Economic (3)
│   │   │   ├── dec_forage
│   │   │   ├── dec_work_repairs
│   │   │   └── dec_trade_browse
│   │   ├── Recovery (5)
│   │   │   ├── dec_rest_sleep
│   │   │   ├── dec_rest_short
│   │   │   ├── dec_meditate
│   │   │   ├── dec_prayer
│   │   │   └── dec_help_wounded
│   │   └── Special (5)
│   │       ├── dec_officer_audience
│   │       ├── dec_mentor_recruit
│   │       ├── dec_volunteer_extra
│   │       ├── dec_night_patrol
│   │       └── dec_baggage_access
│   │
│   └── Medical Decisions (8: 4 land + 4 sea) - medical_decisions.json
│       ├── Land Versions (notAtSea: true)
│       │   ├── dec_medical_surgeon
│       │   ├── dec_medical_rest
│       │   ├── dec_medical_herbal
│       │   └── dec_medical_emergency
│       └── Sea Versions (atSea: true)
│           ├── dec_medical_surgeon_sea
│           ├── dec_medical_rest_sea
│           ├── dec_medical_grog_sea (sailor's remedy)
│           └── dec_medical_emergency_sea
│
├── 📁 CAMP OPPORTUNITIES (36 orchestrated activities)
│   │   File: ModuleData/Enlisted/Decisions/camp_opportunities.json
│   │   Pre-scheduled by ContentOrchestrator 24hrs ahead
│   │
│   ├── Training (6)
│   │   ├── opp_weapon_drill → dec_training_drill
│   │   ├── opp_sparring_match → dec_training_spar
│   │   ├── opp_formation_practice → dec_training_formation
│   │   ├── opp_veteran_spar → dec_training_veteran
│   │   ├── opp_archery_range → dec_training_archery
│   │   └── opp_equipment_maintenance → dec_maintain_gear
│   │
│   ├── Social (9)
│   │   ├── opp_card_game → dec_gamble_cards
│   │   ├── opp_dice_game → dec_gamble_dice
│   │   ├── opp_war_stories → dec_social_stories
│   │   ├── opp_storytelling_circle → dec_social_storytelling
│   │   ├── opp_tavern_visit → dec_tavern_drink
│   │   ├── opp_drinking_heavy → dec_drinking_contest
│   │   ├── opp_arm_wrestling → dec_arm_wrestling
│   │   ├── opp_campfire_song → dec_social_singing
│   │   └── opp_letter_writing → dec_write_letter
│   │
│   ├── Economic (4)
│   │   ├── opp_foraging → dec_forage
│   │   ├── opp_repair_work → dec_work_repairs
│   │   ├── opp_trade_goods → dec_trade_browse
│   │   └── opp_high_stakes_cards → dec_gamble_high
│   │
│   ├── Recovery (7)
│   │   ├── opp_rest_tent → dec_rest_sleep (land)
│   │   ├── opp_rest_shade → dec_rest_short (land)
│   │   ├── opp_rest_hammock → dec_rest_sleep (sea)
│   │   ├── opp_meditation → dec_meditate
│   │   ├── opp_prayer_service → dec_prayer
│   │   ├── opp_help_wounded → dec_help_wounded
│   │   └── opp_preventive_rest → dec_rest_medical (medical pressure)
│   │
│   ├── Special (3)
│   │   ├── opp_officer_audience → dec_officer_audience
│   │   ├── opp_mentor_recruit → dec_mentor_recruit
│   │   ├── opp_volunteer_duty → dec_volunteer_extra
│   │   ├── opp_night_patrol → dec_night_patrol
│   │   └── opp_baggage_access → dec_baggage_access
│   │
│   └── Medical (3 orchestrated)
│       ├── opp_seek_medical_care (has condition)
│       ├── opp_urgent_medical (severe condition)
│       └── opp_preventive_rest (medical pressure building)
│
│   ├── Sea Variants (3 nautical-specific)
│   │   ├── opp_below_deck_drinking → dec_tavern_drink (sea)
│   │   ├── opp_ship_maintenance → dec_work_repairs (sea)
│   │   └── opp_sea_shanty → dec_social_singing (sea)
│
├── 📁 EVENTS (57 context-triggered situations)
│   │
│   ├── Escalation Events (10)
│   │   │   File: events_escalation_thresholds.json
│   │   ├── Scrutiny Track (5 events at thresholds 2, 4, 6, 8, 10)
│   │   └── Discipline Track (5 events at thresholds 2, 4, 6, 8, 10)
│   │
│   ├── Medical Events (4)
│   │   │   File: illness_onset.json
│   │   ├── illness_onset_minor (Risk 3+)
│   │   ├── illness_onset_moderate (Risk 4+)
│   │   ├── illness_onset_severe (Risk 5+)
│   │   └── untreated_condition_worsening
│   │
│   ├── Baggage Events (1)
│   │   │   File: events_baggage_stowage.json
│   │   └── evt_baggage_stowage_first_enlistment (onboarding, one-time)
│   │
│   ├── Pay Events (12)
│   │   │   Files: events_pay_tension.json, events_pay_loyal.json,
│   │   │           events_pay_mutiny.json
│   │   ├── Pay Tension (player unpaid wages)
│   │   ├── Pay Loyalty (wage satisfaction)
│   │   └── Pay Mutiny (wage crisis)
│   │
│   ├── Promotion Events (6)
│   │   │   File: events_promotion.json
│   │   └── Tier promotion proving events (combat challenges)
│   │
│   ├── Retinue Events (17, T7+ only)
│   │   │   File: events_retinue.json
│   │   ├── Loyalty tests
│   │   ├── Veteran stories
│   │   ├── Discipline challenges
│   │   └── Named veteran development
│   │
│   └── Universal Events (7)
│       │   File: events_camp_life.json
│       └── Camp situations available to all tiers
│
└── 📁 MAP INCIDENTS (51 map-action-triggered)
    │
    ├── LeavingBattle (11 incidents)
    │   │   File: incidents_battle.json
    │   │   Trigger: After player battle ends
    │   ├── mi_loot_decision
    │   ├── mi_wounded_enemy
    │   ├── mi_first_kill
    │   ├── mi_officer_notice
    │   ├── mi_comrade_down
    │   ├── mi_battlefield_find
    │   ├── mi_triage_call
    │   ├── mi_enemy_intel
    │   ├── mi_battle_trophy
    │   ├── mi_survivors_choice
    │   └── mi_ambush_losses
    │
    ├── DuringSiege (10 incidents)
    │   │   File: incidents_siege.json
    │   │   Trigger: Hourly while besieging (10% chance)
    │   ├── mi_siege_water
    │   ├── mi_siege_boredom
    │   ├── mi_siege_deserter
    │   ├── mi_siege_sickness
    │   ├── mi_siege_assault_prep
    │   ├── mi_siege_opportunity
    │   ├── mi_siege_gambling
    │   ├── mi_siege_supply_theft
    │   ├── mi_siege_disease
    │   └── mi_siege_spoiled_food
    │
    ├── EnteringTown (8 incidents)
    │   │   File: incidents_town.json
    │   │   Trigger: Opening town/castle menu
    │   ├── mi_town_tavern
    │   ├── mi_town_merchant
    │   ├── mi_town_old_friend
    │   ├── mi_town_message
    │   ├── mi_town_criminal
    │   ├── mi_town_recruitment
    │   ├── mi_town_brawl
    │   └── mi_town_information
    │
    ├── EnteringVillage (6 incidents)
    │   │   File: incidents_village.json
    │   │   Trigger: Opening village menu
    │   ├── mi_village_gratitude
    │   ├── mi_village_resentment
    │   ├── mi_village_sick
    │   ├── mi_village_rumor
    │   ├── mi_village_theft
    │   └── mi_village_recruit
    │
    ├── LeavingSettlement (6 incidents)
    │   │   File: incidents_leaving.json
    │   │   Trigger: Leaving any settlement
    │   ├── mi_leave_hangover
    │   ├── mi_leave_stolen
    │   ├── mi_leave_farewell
    │   ├── mi_leave_intel
    │   ├── mi_leave_stowaway
    │   └── mi_leave_purchase
    │
    ├── WaitingInSettlement (4 incidents)
    │   │   File: incidents_waiting.json
    │   │   Trigger: Hourly while waiting (15% chance)
    │   ├── mi_wait_opportunity
    │   ├── mi_wait_encounter
    │   ├── mi_wait_trouble_brewing
    │   └── mi_wait_boredom
    │
    └── Retinue Incidents (6 incidents, T7+ only)
        │   File: incidents_retinue.json
        │   Trigger: After player battle ends (if has retinue)
        ├── mi_ret_casualty
        ├── mi_ret_hero
        ├── mi_ret_discipline
        ├── mi_ret_loot
        ├── mi_ret_rivalry
        └── mi_ret_veteran_moment
```

---

## Where to Add New Content

### "I want to add a new guard duty event"

1. **Where:** `ModuleData/Enlisted/Orders/order_events/guard_post_events.json`
2. **Parent Order:** `order_guard_post` (defined in `orders_t1_t3.json`)
3. **ID Pattern:** `guard_*` (e.g., `guard_drunk_soldier`, `guard_strange_noise`)
4. **Requirements:** Set `order_type: "order_guard_post"` in event JSON

### "I want to add a new camp decision"

1. **Where:** `ModuleData/Enlisted/Decisions/decisions.json`
2. **ID Pattern:** `dec_*` (e.g., `dec_training_drill`, `dec_gamble_cards`)
3. **Category:** Choose from: training, social, economic, recovery, special, medical
4. **Delivery:** Appears as inline menu option in Camp Hub

### "I want to add a post-battle event"

1. **Where:** `ModuleData/Enlisted/Events/incidents_battle.json`
2. **ID Pattern:** `mi_*` (e.g., `mi_loot_decision`, `mi_wounded_enemy`)
3. **Trigger:** Set `"incident_trigger": "leaving_battle"` in event JSON
4. **Delivery:** Native Bannerlord incident UI (popup after battle)

### "I want to add an escalation threshold event"

1. **Where:** `ModuleData/Enlisted/Events/events_escalation_thresholds.json`
2. **ID Pattern:** `evt_scrutiny_*` or `evt_disc_*` (e.g., `evt_scrutiny_6`, `evt_disc_8`)
3. **Trigger:** Fires automatically when Scrutiny/Discipline crosses threshold
4. **Requirements:** Set `"escalation_threshold": { "scrutiny": 6 }` in event JSON

---

## Content Flow by Player Experience

### How content fires during typical gameplay:

```
NEW PLAYER (T1, Day 1)
├── Enlistment → order_guard_post assigned
├── Guard duty begins → 8 phases over 2 days
│   ├── Phase 1 (Dawn): Status message only
│   ├── Phase 2 (Midday): Status message only
│   ├── Phase 3 (Dusk): 15% chance → guard_drunk_soldier event fires
│   ├── Phase 4 (Night): 35% chance → guard_strange_noise event fires
│   ├── Phase 5-7: Continue...
│   └── Phase 8 (Resolve): Order completes, rewards applied
└── Player opens Camp Hub → sees dec_rest_sleep, dec_training_drill, etc.

EXPERIENCED SOLDIER (T4, Day 150)
├── Lord's party enters town → mi_town_tavern incident fires
├── Player selects options → effects applied
├── 3 days pass → order_scout_route assigned (specialist order)
├── Scout order begins → 12 phases over 3 days
│   └── Phase 6 (Night slot!): scout_enemy_patrol event fires
├── Scrutiny reaches 6 → evt_scrutiny_6 fires immediately
└── Player opens Camp Hub → medical decisions appear (has injury)

COMMANDER (T7+, Day 500)
├── Battle ends → mi_ret_casualty retinue incident fires
├── 5 days pass → order_command_squad assigned (leadership order)
├── Camp phase → retinue narrative event fires (loyalty challenge)
└── Muster day arrives → retinue muster stage in muster menu
```

---

## File Organization Quick Reference

```
ModuleData/Enlisted/
│
├── Orders/                         (17 orders, 3 files + 84 events in 16 files)
│   ├── orders_t1_t3.json           ← T1-T3 basic orders (6)
│   ├── orders_t4_t6.json           ← T4-T6 specialist orders (6)
│   ├── orders_t7_t9.json           ← T7-T9 leadership orders (5)
│   └── order_events/               (84 order events, 16 files)
│       ├── guard_post_events.json          ← order_guard_post events
│       ├── camp_patrol_events.json         ← order_camp_patrol events
│       ├── firewood_detail_events.json     ← order_firewood events
│       ├── sentry_duty_events.json         ← order_sentry events
│       ├── scout_events.json               ← order_scout_route events
│       ├── medical_events.json             ← order_treat_wounded events
│       ├── repair_events.json              ← order_repair_equipment events
│       ├── forage_events.json              ← order_forage events
│       ├── patrol_lead_events.json         ← order_lead_patrol events
│       ├── defenses_events.json            ← order_inspect_defenses events
│       └── ... (6 more event files)
│
├── Decisions/                      (37 decisions + 36 opportunities, 4 files)
│   ├── decisions.json              ← 3 core decisions
│   ├── camp_decisions.json         ← 26 camp life decisions
│   ├── medical_decisions.json      ← 8 medical decisions (4 land + 4 sea)
│   └── camp_opportunities.json     ← 36 orchestrated camp opportunities
│
└── Events/                         (57 events, 16 files)
    ├── events_escalation_thresholds.json   ← Scrutiny/Discipline events (10)
    ├── illness_onset.json                  ← Medical Risk events (4)
    ├── events_baggage_stowage.json         ← Baggage onboarding event (1)
    ├── events_pay_tension.json             ← Pay system events (12 total)
    ├── events_pay_loyal.json
    ├── events_pay_mutiny.json
    ├── events_promotion.json               ← Promotion proving events (6)
    ├── events_retinue.json                 ← T7+ retinue events (17)
    ├── events_camp_life.json               ← Universal camp events (7)
    ├── incidents_battle.json               ← Post-battle incidents (11)
    ├── incidents_siege.json                ← Siege incidents (10)
    ├── incidents_town.json                 ← Town incidents (8)
    ├── incidents_village.json              ← Village incidents (6)
    ├── incidents_leaving.json              ← Leaving settlement incidents (6)
    ├── incidents_waiting.json              ← Waiting incidents (4)
    └── incidents_retinue.json              ← T7+ retinue incidents (6)
```

---

## Content Selection Rules

### How the system picks which content to show:

#### Orders
1. **Frequency:** Every 3-5 days (config: `event_window_min/max_days`)
2. **Filter by tier:** Player must be in order's tier range
3. **Filter by skill:** T4+ orders require minimum skill level
4. **Filter by context:** Some orders blocked at sea (`not_at_sea: true`)
5. **Selection:** Random eligible order from player's tier range

#### Order Events
1. **Phase type determines chance:**
   - `routine`: 0% (status message only)
   - `slot`: 15% base × activity modifier
   - `slot!`: 35% base × activity modifier
2. **Activity modifiers:**
   - Quiet garrison: ×0.3
   - Routine operations: ×0.6
   - Active campaign: ×1.0
   - Intense siege: ×1.5
3. **Filter by requirements:** `world_state`, `notAtSea`, `atSea`
4. **Selection:** Random weighted event from order's event pool

#### Decisions
1. **Always available** (no pacing limits)
2. **Filter by requirements:** tier, cooldowns, state (rest, medical, etc.)
3. **Player initiates** from Camp Hub menu
4. **Category-based organization** in UI

#### Events
1. **Frequency:** 0-1 per day (config: `max_per_day`)
2. **Filter by context:** camp, march, siege, etc.
3. **Weighted selection:**
   - +2 if matches player role
   - +1 if matches current context
   - -3 if same category as last event
   - -2 if seen in last 30 days
4. **Escalation events:** 100% fire when threshold crossed (bypass pacing)

#### Map Incidents
1. **Trigger-based:** Fire immediately on map action
2. **Cooldowns:** Individual per incident (1-12 hours)
3. **Probability:** Some have % chance (10-15%) on trigger
4. **Filter by tier:** Player must meet incident's tier range
5. **Bypass evaluation hours:** Fire any time trigger occurs

---

## Adding Content: Step-by-Step Workflows

### Workflow 1: Add a New Order Event

**Goal:** Add "Guard spots infiltrator" event to Guard Duty order

1. **Open file:** `ModuleData/Enlisted/Orders/order_events/guard_post_events.json`
2. **Add event JSON:**
   ```json
   {
     "id": "guard_infiltrator",
     "order_type": "order_guard_post",
     "weight": 1.0,
     "requirements": {
       "world_state": ["siege_defending"],
       "notAtSea": true
     },
     "setup": "Movement in the shadows. Someone's trying to get past your post.",
     "setupId": "guard_infiltrator_setup",
     "options": [
       {
         "id": "challenge",
         "text": "Challenge them loudly",
         "skill_check": { "Perception": 40 },
         "effects": {
           "officer_rep": 10,
           "scrutiny": -2
         },
         "resultText": "You stop the infiltrator. The sergeant is impressed."
       },
       {
         "id": "pursue",
         "text": "Pursue quietly",
         "skill_check": { "Athletics": 50 },
         "effects": {
           "officer_rep": 5,
           "soldier_rep": 5
         },
         "resultText": "You catch them alone. Quick work."
       }
     ]
   }
   ```
3. **Add XML strings:** `ModuleData/Languages/enlisted_strings.xml`
   ```xml
   <string id="guard_infiltrator_setup" text="Movement in the shadows..." />
   <!-- Add resultText IDs too -->
   ```
4. **Test:** Run order, watch for event during slot phases

### Workflow 2: Add a New Camp Decision

**Goal:** Add "Study battle tactics" training decision

1. **Open file:** `ModuleData/Enlisted/Decisions/camp_decisions.json`
2. **Add decision JSON:**
   ```json
   {
     "id": "dec_study_tactics",
     "nameId": "dec_study_tactics_name",
     "name": "Study Battle Tactics",
     "descriptionId": "dec_study_tactics_desc",
     "description": "Review maps and formations. Learn from past battles.",
     "category": "training",
     "cost": {
       "time_hours": 3,
       "fatigue": 1
     },
     "requirements": {
       "tier_min": 3,
       "maxIllness": "Moderate"
     },
     "cooldown_days": 1,
     "sub_choices": [
       {
         "id": "study",
         "textId": "dec_study_tactics_action",
         "text": "Study maps and formations",
         "reward_choices": [
           {
             "Tactics": 25,
             "Leadership": 10
           }
         ]
       }
     ]
   }
   ```
3. **Add XML strings**
4. **Test:** Open Camp Hub, look for decision in Training category

### Workflow 3: Add a New Map Incident

**Goal:** Add "Wounded enemy begs for mercy" post-battle incident

1. **Open file:** `ModuleData/Enlisted/Events/incidents_battle.json`
2. **Add incident JSON:**
   ```json
   {
     "id": "mi_enemy_mercy",
     "category": "map_incident",
     "delivery": {
       "method": "automatic",
       "channel": "inquiry",
       "incident_trigger": "leaving_battle"
     },
     "requirements": {
       "tier": { "min": 1, "max": 9 }
     },
     "timing": {
       "cooldown_days": 7,
       "priority": "normal"
     },
     "content": {
       "titleId": "mi_enemy_mercy_title",
       "setupId": "mi_enemy_mercy_setup",
       "options": [
         {
           "id": "mercy",
           "textId": "mi_enemy_mercy_spare",
           "effects": {
             "trait_xp": { "Mercy": 20 }
           }
         },
         {
           "id": "execute",
           "textId": "mi_enemy_mercy_kill",
           "effects": {
             "trait_xp": { "Valor": 15 },
             "soldier_reputation": 5
           }
         }
       ]
     }
   }
   ```
3. **Add XML strings**
4. **Test:** Fight battle, watch for incident on battle end screen

---

## Systems Integration Reference

### Which systems does each content type affect?

```
ORDERS
├── Reputation: Officer Rep, Lord Rep (T4+)
├── Company Needs: Readiness, Morale, Supplies
├── Skills: Primary and secondary skills
├── Gold: T4+ orders reward denars
├── Renown: T7+ orders grant renown
└── Retinue: T7+ orders may affect retinue loyalty

DECISIONS
├── Reputation: All three tracks (Soldier/Officer/Lord)
├── Escalation: Scrutiny, Discipline, Medical Risk
├── Skills: Targeted skill training
├── HP: Injury/recovery
├── Fatigue: Rest/Rest drain
├── Gold: Costs and gambling rewards
└── Time: Hours pass (miss other opportunities)

EVENTS
├── Reputation: Complex trade-offs between tracks
├── Escalation: Threshold events at specific values
├── Skills: Passive XP from choices
├── HP: Injuries from risky choices
├── Traits: Valor, Honor, Mercy, etc.
├── Gold: Rewards and costs
└── Party: Rare troop/food loss in crisis events

MAP INCIDENTS
├── Reputation: Situation-based rep changes
├── Skills: Context-specific skill checks
├── Traits: Personality development
├── HP: Battle aftermath injuries
├── Gold: Loot and spending opportunities
└── Intel: Learn about world state
```

---

## Content Statistics

### Current Content Distribution

```
COMPLETE CONTENT CATALOG:
- Orders:           17
- Order Events:     84
- Decisions:        37 (core:3 + camp:26 + medical:8)
- Opportunities:    36 (orchestrated)
- Context Events:   57 (escalation, medical, pay, promotion, baggage, retinue, universal)
- Map Incidents:    51
                   ---
TOTAL:             275 pieces

BY TIER:
T1-T3:  6 orders + 27 order events + 37 decisions + 36 opportunities + 45 map incidents = 151 pieces
T4-T6:  6 orders + 37 order events + 37 decisions + 36 opportunities + 45 map incidents = 161 pieces
T7-T9:  5 orders + 20 order events + 37 decisions + 36 opportunities + 51 map incidents = 149 pieces
All:    57 context events (escalation, medical, pay, promotion, baggage, retinue, universal)

BY TRIGGER TYPE:
- System-assigned:  17 orders
- Duty-based:       84 order events
- Player-initiated: 37 decisions (direct choice)
- Orchestrated:     36 opportunities (pre-scheduled 24hrs ahead)
- State-triggered:  57 context events
- Map-triggered:    51 map incidents

BY DELIVERY METHOD:
- Orders (popup Accept/Decline):       17
- Order Events (during duty):          84
- Decisions (Camp Hub inline menu):    37
- Opportunities (DECISIONS accordion): 36
- Events (automatic popups):           57
- Map Incidents (native UI):           51

BY CONTENT FILE:
- orders_*.json (3 files):                    17 orders
- order_events/*.json (16 files):             84 order events
- decisions.json:                              3 core decisions
- camp_decisions.json:                        26 camp decisions
- medical_decisions.json:                      8 medical decisions (4+4 sea)
- camp_opportunities.json:                    36 opportunities
- events_*.json (8 files):                    57 context events
- incidents_*.json (6 files):                 51 map incidents
```

---

## Quick Reference: ID Prefixes

```
order_*          = Order definition (17)
guard_*          = Guard duty order events
patrol_*         = Patrol order events
scout_*          = Scout order events
forage_*         = Forage order events
(etc...)

dec_*            = Camp Hub decision (37)
opp_*            = Camp opportunity (36)
evt_*            = Context event (57)
mi_*             = Map incident (51)

evt_scrutiny_*   = Scrutiny escalation event
evt_disc_*       = Discipline escalation event
illness_onset_*  = Medical escalation event
evt_baggage_*    = Baggage onboarding event
evt_pay_*        = Pay system event
evt_ret_*        = Retinue event (T7+)
mi_ret_*         = Retinue incident (T7+)
```

---

## Summary

**YES, your content is organized and tied to parent systems:**

✅ Orders → Order Events (explicit event pools)  
✅ Events → Trigger Contexts (leaving_battle, during_siege, etc.)  
✅ Content → Tier Ranges (T1-T3, T4-T6, T7-T9)  
✅ Content → Requirements (skills, state, escalation)  
✅ Documentation → Multiple indexes and catalogs  

**Use this map when:**
- Adding new content (find the right file)
- Understanding how content flows (see trigger paths)
- Debugging missing content (check requirements and filters)
- Planning new features (see where content gaps exist)

---

**End of Document**
