# Enlisted Mod Configuration Guide

**For Players:** This folder contains all the configuration files and content data that control how the Enlisted mod works. You can adjust settings, tune gameplay, and understand how the mod's systems operate.

---

## Quick Navigation

**Jump to:**
- [Summary](#summary) - What this folder contains
- [Index by Theme](#index-by-theme) - Find configs by topic
- [Config Files Reference](#config-files-reference) - Detailed file descriptions
- [Content Files Reference](#content-files-reference) - Events, incidents
- [How to Customize](#how-to-customize) - Safe editing tips

---

## Summary

This folder (`Modules\Enlisted\ModuleData\Enlisted\`) contains:

1. **Configuration Files** - Control gameplay rules, pacing, and features
2. **Content Files** - Events and incidents (all in JSON)
3. **Localization** - Player-facing text in `../Languages/enlisted_strings.xml`

All files use JSON format and can be edited with any text editor. Changes take effect when you restart the game.

**Important:** Always backup files before editing. Invalid JSON will cause the mod to fail loading.

---

## Index by Theme

### XP & Progression
- **[progression_config.json](#progression_configjson)** - Tier XP requirements, rank names, formation selection
- **[enlisted_config.json](#enlisted_configjson)** → `xp_sources` - Daily XP, battle XP, kill XP
- **[events_training.json](#events-folder)** - Training events and skill rewards
- **[events_promotion.json](#events-folder)** - Promotion events and proving challenges

### Retirement & Service Terms
- **[enlisted_config.json](#enlisted_configjson)** → `retirement` section:
  - First term length (default: 252 days)
  - Renewal term length (default: 84 days)
  - Discharge bonuses and penalties
  - Re-enlistment bonuses
  - Pension rates (honorable/veteran)
  - Severance pay
  - Probation rules for re-entry

### Pay & Wages
- **[enlisted_config.json](#enlisted_configjson)** → `finance` section:
  - Payday interval (default: 12 days)
  - Wage formula (base, tier multipliers, level scaling)
  - Army bonus multiplier
- **[progression_config.json](#progression_configjson)** → `wage_system`:
  - Base daily wage
  - Duty assignment multipliers (scout, engineer, quartermaster, etc.)
  - Maximum wage caps
- **[events_pay_tension.json](#events-folder)** - Late pay events
- **[events_pay_mutiny.json](#events-folder)** - Mutiny events when pay is very late
- **[events_pay_loyal.json](#events-folder)** - Loyalty events during pay issues

### Equipment & Quartermaster
- **[equipment_pricing.json](#equipment_pricingjson)** - Quartermaster pricing and retirement costs
- **[enlisted_config.json](#enlisted_configjson)** → `quartermaster`:
  - Soldier tax (default: 1.2x market price)
  - Buyback rate (default: 0.5x)
  - Officer stock tax (default: 1.35x)
- **[enlisted_config.json](#enlisted_configjson)** → `camp_life`:
  - QM pricing modifiers based on company morale/logistics
  - Purchase/buyback rates when conditions are fine/tense/sour/predatory
- **[events_duty_quartermaster.json](#events-folder)** - Supply management events (requires Trader trait 5+)

### Camp Life & Activities
- **[enlisted_config.json](#enlisted_configjson)** → `camp_life`:
  - Enabled/disabled toggle
  - Threshold values for high/low conditions
  - QM pricing modifiers
- **[enlisted_config.json](#enlisted_configjson)** → `camp_activities`:
  - Enabled/disabled toggle
  - Activities definition file location
- **[camp_events.json](#events-folder)** - Camp context events
- **[events_general.json](#events-folder)** - General camp and march events

### Events & Pacing
- **[enlisted_config.json](#enlisted_configjson)** → `decision_events.pacing`:
  - Max events per day/week
  - Minimum hours between events
  - Event window (3-5 days)
  - Cooldown periods
  - Evaluation hours (when events fire)
  - Quiet day system

### Orders Surface
- Driven by the **storylet backbone** (Spec 0) via duty-pool storylets in `Storylets/duty_*.json` and named-order arc storylets. The legacy `Orders/` folder + `events_decisions.json` were retired.
- See `docs/Features/Content/storylet-backbone.md` for the living reference.

### Reputation & Discipline
- **[enlisted_config.json](#enlisted_configjson)** → `escalation`:
  - Scrutiny decay interval (default: 7 days)
  - Discipline decay interval (default: 14 days)
  - Soldier rep decay interval (default: 14 days)
  - Medical risk decay interval (default: 1 day)
  - Threshold event cooldown (default: 7 days)
- **[events_escalation_thresholds.json](#events-folder)** - Events triggered by high scrutiny/discipline/medical risk

### Retinue & Commander Track
- **[retinue_config.json](#retinue_configjson)** - Retinue capacity, trickle rates, economics
- **[enlisted_config.json](#enlisted_configjson)** → `gameplay.reserve_troop_threshold` - Troop threshold for retinue

### Onboarding & Discharge
- **[events_onboarding.json](#events-folder)** - New recruit introduction chain
- **[enlisted_config.json](#enlisted_configjson)** → `retirement` - Discharge rules and bonuses

### Map Incidents
- **[incidents_battle.json](#events-folder)** - After-battle events
- **[incidents_town.json](#events-folder)** - Town/settlement events
- **[incidents_village.json](#events-folder)** - Village events
- **[incidents_siege.json](#events-folder)** - Siege events
- **[incidents_leaving.json](#events-folder)** - Leaving settlement events
- **[incidents_waiting.json](#events-folder)** - Waiting/idle events

### Personas & Squad Identity
- **[enlisted_config.json](#enlisted_configjson)** → `lance_personas`:
  - Enabled/disabled toggle
  - Female character chances by role
  - Seed salt for randomization

### Player Conditions (Injury/Illness)
- **[enlisted_config.json](#enlisted_configjson)** → `player_conditions`:
  - Enabled/disabled toggle
  - Treatment multipliers (basic, thorough, herbal)
  - Exhaustion system toggle
  - Condition definitions file location

---

## Config Files Reference

### enlisted_config.json

**Purpose:** Master configuration file controlling all major systems and gameplay tuning.

**Key Sections:**

| Section | What It Controls |
|---------|------------------|
| `system_info` | Mod version, compatible game versions |
| `gameplay` | Reserve troop threshold, desertion grace period, leave duration |
| `quartermaster` | Soldier tax, buyback rate, officer stock pricing |
| `retirement` | Service terms, discharge bonuses, pensions, probation rules |
| `finance` | Payday interval, wage formula, clan tooltip |
| `camp_life` | Camp life system toggle, thresholds, QM pricing modifiers |
| `escalation` | Reputation/discipline decay rates, threshold event cooldowns |
| `lance_personas` | Squad identity system, female character chances |
| `player_conditions` | Injury/illness system, treatment multipliers |
| `camp_activities` | Camp activities system toggle |
| `decision_events` | Event system toggle, pacing rules, tier gates |

**Most Common Edits:**
- `retirement.first_term_days` - Change how long your first service term lasts
- `finance.payday_interval_days` - Change how often you get paid
- `decision_events.pacing.max_per_day` - Control event frequency
- `camp_life.enabled` - Toggle camp life simulation on/off
- `escalation.enabled` - Toggle reputation/discipline systems on/off

### progression_config.json

**Purpose:** Controls tier progression, rank names, and wage scaling.

**Key Sections:**

| Section | What It Controls |
|---------|------------------|
| `tier_progression.requirements` | XP needed for each tier (1-9), rank names, estimated duration |
| `tier_progression.formation_selection` | When you choose your formation, cooldowns, free changes |
| `culture_ranks` | Culture-specific rank names (Empire, Vlandia, Sturgia, etc.) |
| `wage_system` | Base wage formula, duty assignment multipliers, army bonuses |
| `xp_sources` | Daily XP, battle XP, XP per kill |
| `promotion_benefits` | Equipment access, officer/commander track unlocks |
| `track_definitions` | Enlisted/Officer/Commander track definitions |

**Most Common Edits:**
- `tier_progression.requirements[].xp_required` - Change XP needed for each tier
- `wage_system.assignment_multipliers` - Adjust pay for different duties
- `xp_sources.daily_base` - Change base daily XP gain
- `formation_selection.change_cooldown_days` - How often you can change formations

### equipment_pricing.json

**Purpose:** Controls quartermaster pricing and retirement equipment costs.

**Structure:** Contains pricing rules for equipment purchases and retirement gear stripping.

**Most Common Edits:**
- Adjust pricing multipliers for equipment tiers
- Change retirement equipment buyback rates

### retinue_config.json

**Purpose:** Controls retinue system for commander-tier players.

**Key Settings:**
- Retinue capacity by tier
- Troop trickle rates
- Economic costs for maintaining retinue

### strategic_context_config.json

**Purpose:** Defines war stance contexts and strategic situations.

**Structure:** War stance definitions used by the strategic context system.

### menu_config.json

**Purpose:** Game menu definitions (future use).

**Note:** Currently used for menu structure definitions.

### settings.json

**Purpose:** Logging levels and debug flags.

**Most Common Edits:**
- Adjust logging verbosity
- Enable/disable debug features

---

## Content Files Reference

### Events Folder

Contains all narrative events triggered by context, role, or player action.

**Event Files:**

| File | Content | Count |
|------|---------|-------|
| `events_general.json` | Universal camp/march events | ~25 |
| `events_training.json` | Training events and progression | ~12 |
| `events_onboarding.json` | New recruit introduction chain | ~8 |
| `events_promotion.json` | Promotion and proving events | ~8 |
| `events_retinue.json` | Commander retinue events (T7+) | ~11 |
| `events_escalation_thresholds.json` | Scrutiny/Discipline/Medical threshold events | 14 |
| `events_pay_tension.json` | Pay tension events | ~10 |
| `events_pay_mutiny.json` | Mutiny events | ~8 |
| `events_pay_loyal.json` | Loyalty events | ~6 |
| `camp_events.json` | Camp context events | ~8 |
| `muster_events.json` | Muster and recruitment events | ~6 |
| `Role/scout_events.json` | Scout role-specific events | ~12 |

**Role-Specific Content:**

Role-based events are integrated into the general event system. The Orders system (see Orders folder) delivers role-specific content through chain-of-command directives that are filtered by your skills and traits.

**Map Incidents:**

| File | Trigger |
|------|---------|
| `incidents_battle.json` | After battle ends | ~11 |
| `incidents_town.json` | Entering/leaving towns | ~8 |
| `incidents_village.json` | Entering/leaving villages | ~6 |
| `incidents_siege.json` | During sieges | ~10 |
| `incidents_leaving.json` | Leaving settlements | ~6 |
| `incidents_waiting.json` | While waiting/idle | ~4 |
| `incidents_retinue.json` | Post-battle retinue incidents (T7+) | ~6 |

### Storylets Folder

Storylet backbone (Spec 0) — duty pool storylets, named-order arc storylets, ceremony storylets, transitions, culture overlays. Drives the current Orders surface (Spec 2: `OrderActivity` + `NamedOrderState`) and the Home Surface (Spec 1: `HomeActivity`). Living reference: `docs/Features/Content/storylet-backbone.md`.

---

## How to Customize

### Safe Editing Tips

1. **Always backup first** - Copy the file before editing
2. **Use a proper text editor** - Notepad++, VS Code, or similar (NOT Microsoft Word)
3. **Validate JSON** - Use an online JSON validator to check for syntax errors
4. **Test incrementally** - Make small changes and test in-game
5. **Check logs** - If the mod fails to load, check `Modules\Enlisted\Debugging\Session-A_*.log`

### Common Customizations

**Make service terms shorter:**
```json
// In enlisted_config.json → retirement section
"first_term_days": 126,  // Changed from 252 (half the time)
"renewal_term_days": 42,  // Changed from 84 (half the time)
```

**Get paid more often:**
```json
// In enlisted_config.json → finance section
"payday_interval_days": 6,  // Changed from 12 (twice as often)
```

**Level up faster:**
```json
// In progression_config.json → xp_sources section
"daily_base": 50,  // Changed from 25 (double XP)
"battle_participation": 50,  // Changed from 25
"xp_per_kill": 4  // Changed from 2
```

**Reduce event frequency:**
```json
// In enlisted_config.json → decision_events.pacing section
"max_per_day": 1,  // Changed from 2
"max_per_week": 4,  // Changed from 8
"min_hours_between": 12  // Changed from 6
```

**Disable camp life simulation:**
```json
// In enlisted_config.json → camp_life section
"enabled": false  // Changed from true
```

**Adjust quartermaster prices:**
```json
// In enlisted_config.json → quartermaster section
"soldier_tax": 1.0,  // Changed from 1.2 (no markup)
"buyback_rate": 0.75  // Changed from 0.5 (better buyback)
```

### Event Content Editing

Events use a standardized JSON schema. Each event has:
- `event_id` - Unique identifier
- `titleId` / `title` - Event title (localized/fallback)
- `setupId` / `setup` - Event description (localized/fallback)
- `requirements` - Conditions for the event to fire
- `options` - Player choices and their effects

**Example event structure:**
```json
{
  "event_id": "evt_example",
  "titleId": "evt_example_title",
  "title": "Fallback Title",
  "setupId": "evt_example_setup",
  "setup": "Fallback description text.",
  "requirements": { "tier_min": 1, "context": ["camp"] },
  "options": [
    {
      "option_id": "opt_1",
      "textId": "evt_example_opt1",
      "text": "Option text",
      "effects": { "soldierRep": 5, "officerRep": 0 }
    }
  ]
}
```

**Effect fields you can use:**
- `soldierRep`, `officerRep`, `lordRep` - Reputation changes
- `scrutiny`, `discipline` - Escalation meter changes
- `gold` - Gold reward/penalty
- `xp` - XP reward
- `skillXp` - Skill XP (e.g., `{"Scouting": 50}`) - **REQUIRED for order events**
- `traitXp` - Trait XP (e.g., `{"Valor": 10}`)
- `companyNeeds` - Company need changes (e.g., `{"Readiness": -10}`)
- `medicalRisk` - Medical risk escalation
- `renown` - Renown reward/penalty
- `hpChange` - Player HP damage (negative values)
- `troopLoss` - Troop casualties (e.g., `{"min": 1, "max": 3}`)
- `promotes` - Grants promotion (use `true` for next tier, or specific tier number like `3`)

**Promotion Effect Details:**
The `promotes` field is used in proving events (`events_promotion.json`) to grant tier promotions:
- `"promotes": true` - Promotes player to next tier (e.g., T2→T3, T5→T6)
- `"promotes": 5` - Promotes player to specific tier (explicit tier 5)
- **Technical Note:** The value `true` is converted to `-1` internally (sentinel value) and resolved to `currentTier + 1` at runtime. This ensures promotions always advance by one tier regardless of player's current rank.

**Text placeholders you can use:**
- Player: `{PLAYER_NAME}`, `{PLAYER_RANK}`
- NCO/Officers: `{SERGEANT}`, `{SERGEANT_NAME}`, `{OFFICER_NAME}`, `{CAPTAIN_NAME}`
- Soldiers: `{SOLDIER_NAME}`, `{COMRADE_NAME}`, `{VETERAN_1_NAME}`, `{RECRUIT_NAME}`
- Lord/Faction: `{LORD_NAME}`, `{LORD_TITLE}`, `{FACTION_NAME}`, `{KINGDOM_NAME}`
- Location: `{SETTLEMENT_NAME}`, `{COMPANY_NAME}`

---

## Localization

All player-facing text is in `../Languages/enlisted_strings.xml`.

**String ID Prefixes:**
- `evt_*` - Event text
- `dec_*` - Decision text
- `ord_*` - Order text
- `inc_*` - Map incident text
- `ui_*` - UI labels

To add translations, see the [Translation Guide](../Languages/README.md).

---

## Schema & Documentation

**Event Schema:** Events use schema version 2. See `Events/schema_version.json`.

**Full Documentation:**
- Complete content catalog: `docs/Content/content-index.md`
- Event system reference: `docs/Features/Content/event-system-schemas.md`
- Content system architecture: `docs/Features/Content/content-system-architecture.md`

---

## Content Counts

| Category | Count |
|----------|-------|
| Events | 80+ |
| Map Incidents | 51 |
| Storylets | 245+ |

---

## JSON File Index

Complete inventory of all JSON configuration and content files.

### Config Files (8)
- `Config/baggage_config.json` - Baggage train system configuration
- `Config/enlisted_config.json` - Master mod configuration
- `Config/equipment_pricing.json` - Quartermaster pricing rules
- `Config/progression_config.json` - Tier progression and wage system
- `Config/retinue_config.json` - Commander retinue system
- `Config/settings.json` - Logging and debug settings
- `Config/simulation_config.json` - AI simulation configuration
- `Config/strategic_context_config.json` - War stance and strategic contexts

### Conditions (1)
- `Conditions/condition_defs.json` - Player condition definitions (injuries/illness)

### Dialogue (4)
- `Dialogue/qm_baggage.json` - Quartermaster baggage dialogue
- `Dialogue/qm_dialogue.json` - Quartermaster main dialogue
- `Dialogue/qm_gates.json` - Quartermaster gating dialogue
- `Dialogue/qm_intro.json` - Quartermaster introduction dialogue

### Events (15)
- `Events/events_bagcheck.json` - Baggage inspection events
- `Events/events_escalation_thresholds.json` - Escalation threshold events
- `Events/events_pay_loyal.json` - Pay loyalty events
- `Events/events_pay_mutiny.json` - Pay mutiny events
- `Events/events_pay_tension.json` - Pay tension events
- `Events/events_promotion.json` - Promotion events
- `Events/events_retinue.json` - Commander retinue events
- `Events/incidents_battle.json` - After-battle incidents
- `Events/incidents_leaving.json` - Leaving settlement incidents
- `Events/incidents_retinue.json` - Retinue-specific incidents
- `Events/incidents_siege.json` - Siege incidents
- `Events/incidents_town.json` - Town incidents
- `Events/incidents_village.json` - Village incidents
- `Events/incidents_waiting.json` - Waiting/idle incidents
- `Events/schema_version.json` - Event schema version

### Storylets

The current Orders surface (Spec 2) is driven by storylets in `Storylets/` — `duty_*.json` for the seven duty profiles (wandering, marching, escorting, garrisoned, raiding, besieging, imprisoned) and named-order arc storylets for tier-gated mission arcs. Living reference: `docs/Features/Content/storylet-backbone.md`.

---

## Support

**If something breaks:**
1. Check `Modules\Enlisted\Debugging\Session-A_*.log` for errors
2. Restore your backup
3. Validate your JSON syntax
4. Report issues with the error code from the log

**Common error codes:**
- `E-CONFIG-*` - Configuration file errors
- `E-EVENT-*` - Event system errors
- `E-SAVELOAD-*` - Save/load errors

---

**Last Updated:** 2026-04-25 (Decisions + Orders systems retired; Storylet backbone is authoritative)  
**Mod Version:** 0.9.0  
**For full documentation, see:** `docs/index.md`
