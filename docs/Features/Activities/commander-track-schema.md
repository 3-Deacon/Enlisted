# Commander Track Schema (T7-T9)

**Summary:** Design schema for Commander Track (T7-T9) expansion including advanced retinue management, individual soldier tracking, and relationship mechanics. The basic retinue system (soldiers at T7+) is implemented; this schema defines future enhancements.

**Status:** 📋 Specification (Partial Implementation)  
**Last Updated:** 2025-12-23  
**Related Docs:** [Retinue System](../Core/retinue-system.md), [Training System](../Combat/training-system.md)

> **Implementation Status:**
> - ✅ **Implemented:** Basic retinue (T7: 20, T8: 30, T9: 40 soldiers), formation type selection, trickle replenishment, requisition, casualty tracking
> - 📋 **Future:** Named soldiers, individual traits, morale/cohesion, soldier relationships, commander events, command style tags

---

## Table of Contents

1. [Overview](#overview)
2. [Commander State Schema](#commander-state-schema)
3. [Retinue Schema](#retinue-schema)
4. [Soldier Schema](#soldier-schema)
5. [Commander Relationships](#commander-relationships)
6. [Commander Promotions](#commander-promotions)
7. [Commander Events Framework](#commander-events-framework)
8. [Commander Tracking Stats](#commander-tracking-stats)
9. [Feature Flags](#feature-flags)
10. [Trigger Conditions](#trigger-conditions)
11. [Persistence](#persistence)

---

## Overview

The Commander Track (T7-T9) is fundamentally different from the Enlisted Track (T1-T6):

| Aspect | Enlisted (T1-T6) | Commander (T7-T9) |
|--------|------------------|-------------------|
| Role | Follow orders | Give orders |
| Unit | Part of a squad | Lead a retinue |
| Relationships | NCO, fellow soldiers | Soldiers, officers, lord |
| Progression | Events, battles survived | Battles commanded, soldier survival |
| Stakes | Personal survival | Soldiers' lives |

### Tier Summary

| Tier | Rank | Retinue Cap | XP Required | Key Unlock |
|------|------|-------------|-------------|------------|
| T7 | Captain | 20 soldiers | 30,000 | Retinue system |
| T8 | Commander | 30 soldiers | 45,000 | Command style tag |
| T9 | Marshal | 40 soldiers | 65,000 | Strategic style tag |

> **Note:** Capacities (20/30/40) are hardcoded in `RetinueManager.cs`. This spec document contains schema ideas for future expansion.

---

## Commander State Schema

Master state object for a T7+ player.

```json
{
  "schemaVersion": "1.0",
  
  "commander_state": {
    "is_commander": false,
    "commander_tier": 7,
    "days_as_commander": 0,
    "commission_date": "CampaignTime | null",
    
    "rank": {
      "tier": 7,
      "title": "Lieutenant",
      "title_key": "rank_lieutenant"
    },
    
    "tags": {
      "character_tag": "steady | fierce | supportive | null",
      "loyalty_tag": "absolute | counselor | principled | null",
      "command_style_tag": "dutiful | reluctant | protective | null",
      "strategic_style_tag": "bold | cautious | null"
    },
    
    "declined_commission": false,
    "declined_commission_date": "CampaignTime | null",
    
    "lord_id": "string",
    "faction_id": "string",
    
    "retinue": { "...": "see Retinue Schema" },
    "relationships": { "...": "see Commander Relationships" },
    "stats": { "...": "see Commander Tracking Stats" },
    "promotion_state": { "...": "see Commander Promotions" }
  }
}
```

---

## Retinue Schema

The player's personal soldiers at T7+.

```json
{
  "schemaVersion": "1.0",
  
  "retinue": {
    "enabled": true,
    "unlocked_at_tier": 7,
    
    "capacity": {
      "current_max": 20,
      "tier_7_max": 20,
      "tier_8_max": 30,
      "tier_9_max": 40
    },
    
    "companions_count_toward_cap": false,
    
    "soldiers": [
      { "...": "see Soldier Schema" }
    ],
    
    "current_count": 0,
    "available_count": 0,
    "wounded_count": 0,
    "dead_this_campaign": 0,
    
    "morale": {
      "value": 50,
      "min": 0,
      "max": 100,
      "modifiers": [
        { "source": "recent_victory", "value": 10, "expires": "CampaignTime" },
        { "source": "unpaid", "value": -15, "expires": null }
      ]
    },
    
    "cohesion": {
      "value": 50,
      "min": 0,
      "max": 100,
      "description": "How well soldiers work together"
    },
    
    "average_veterancy": 0,
    "average_tier": 0,
    
    "formation_composition": {
      "infantry": 0,
      "archer": 0,
      "cavalry": 0,
      "horse_archer": 0
    }
  }
}
```

### Retinue Capacity by Tier

> **Implementation Note:** Current implementation uses single formation type (player selects one). Composition limits below are for future expansion.

```json
{
  "retinue_capacity_config": {
    "tier_7": {
      "soldiers": 20,
      "max_cavalry_percent": 30,
      "max_horse_archer_percent": 20
    },
    "tier_8": {
      "soldiers": 30,
      "max_cavalry_percent": 40,
      "max_horse_archer_percent": 30
    },
    "tier_9": {
      "soldiers": 40,
      "max_cavalry_percent": 50,
      "max_horse_archer_percent": 40
    }
  }
}
```

---

## Soldier Schema

Individual soldier in the retinue.

```json
{
  "schemaVersion": "1.0",
  
  "soldier": {
    "id": "string (generated UUID)",
    "slot_index": 0,
    
    "identity": {
      "name_first": "string",
      "name_full": "string",
      "name_short": "string",
      "culture": "vlandia | sturgia | empire | battania | aserai | khuzait",
      "background": "peasant | townsman | soldier_son | noble_bastard | mercenary | criminal"
    },
    
    "troop": {
      "troop_id": "string (native troop type reference)",
      "troop_name": "string",
      "troop_tier": 1,
      "formation": "infantry | archer | cavalry | horse_archer"
    },
    
    "state": {
      "is_alive": true,
      "is_available": true,
      "is_wounded": false,
      "wound_severity": "none | minor | moderate | severe",
      "wound_recovery_days": 0,
      "is_sick": false,
      "sick_recovery_days": 0
    },
    
    "stats": {
      "battles_survived": 0,
      "kills": 0,
      "wounds_received": 0,
      "days_served": 0,
      "times_promoted": 0
    },
    
    "relationship": {
      "loyalty": 50,
      "respect": 50,
      "trust": 50
    },
    
    "traits": [
      {
        "id": "brave | coward | steady | reckless | loyal | ambitious | quiet | loud",
        "discovered": true,
        "discovered_at_event": "string | null"
      }
    ],
    
    "history": {
      "recruited_date": "CampaignTime",
      "recruited_source": "battlefield | camp_assignment | purchased | volunteer | transfer",
      "recruited_at_location": "string",
      "notable_events": [
        {
          "event_type": "first_battle | first_kill | wounded | saved_comrade | promoted | insubordination",
          "date": "CampaignTime",
          "description": "string"
        }
      ]
    },
    
    "equipment": {
      "is_customized": false,
      "uses_retinue_standard": true,
      "custom_items": []
    }
  }
}
```

### Soldier Recruitment Sources

```json
{
  "recruitment_sources": [
    {
      "id": "battlefield",
      "name": "Battlefield Pickup",
      "description": "Recruited from survivors after battle",
      "base_loyalty": 40,
      "base_respect": 30,
      "available_at": "after_battle",
      "cost_gold": 0,
      "chance_wounded": 50
    },
    {
      "id": "camp_assignment",
      "name": "Camp Assignment",
      "description": "Assigned from lord's troops",
      "base_loyalty": 50,
      "base_respect": 40,
      "available_at": "in_camp",
      "cost_gold": 0,
      "requires_lord_relation": 20
    },
    {
      "id": "purchased",
      "name": "Purchased",
      "description": "Hired from settlement",
      "base_loyalty": 45,
      "base_respect": 35,
      "available_at": "in_settlement",
      "cost_gold": 100,
      "cost_per_tier": 50
    },
    {
      "id": "volunteer",
      "name": "Volunteer",
      "description": "Soldier asked to join your retinue",
      "base_loyalty": 60,
      "base_respect": 50,
      "available_at": "event_triggered",
      "cost_gold": 0,
      "requires_commander_reputation": 30
    },
    {
      "id": "transfer",
      "name": "Transfer",
      "description": "Transferred from another commander",
      "base_loyalty": 35,
      "base_respect": 40,
      "available_at": "event_triggered",
      "cost_gold": 0
    }
  ]
}
```

### Soldier Traits

```json
{
  "soldier_traits": [
    {
      "id": "brave",
      "name": "Brave",
      "description": "Stands firm under fire",
      "effects": { "morale_bonus": 5, "flee_chance": -20 },
      "discovered_by": ["battle_event", "crisis_event"]
    },
    {
      "id": "coward",
      "name": "Coward",
      "description": "Breaks under pressure",
      "effects": { "morale_bonus": -5, "flee_chance": 20 },
      "discovered_by": ["battle_event", "crisis_event"],
      "hidden_until_triggered": true
    },
    {
      "id": "steady",
      "name": "Steady",
      "description": "Reliable in all situations",
      "effects": { "cohesion_bonus": 5 },
      "discovered_by": ["long_service"]
    },
    {
      "id": "reckless",
      "name": "Reckless",
      "description": "Takes unnecessary risks",
      "effects": { "kill_bonus": 10, "wound_chance": 15 },
      "discovered_by": ["battle_event"]
    },
    {
      "id": "loyal",
      "name": "Loyal",
      "description": "Devoted to the commander",
      "effects": { "loyalty_decay_reduction": 50 },
      "discovered_by": ["loyalty_event", "long_service"]
    },
    {
      "id": "ambitious",
      "name": "Ambitious",
      "description": "Wants more responsibility",
      "effects": { "xp_gain_bonus": 20, "loyalty_decay": 10 },
      "discovered_by": ["promotion_event", "dialogue"]
    },
    {
      "id": "quiet",
      "name": "Quiet",
      "description": "Keeps to themselves",
      "effects": { "cohesion_penalty": -3, "reliability_bonus": 5 },
      "discovered_by": ["social_event"]
    },
    {
      "id": "loud",
      "name": "Loud",
      "description": "Natural leader among troops",
      "effects": { "cohesion_bonus": 5, "morale_spread": 10 },
      "discovered_by": ["social_event", "battle_event"]
    }
  ]
}
```

---

## Commander Relationships

Relationship tracking for T7+ commanders.

```json
{
  "schemaVersion": "1.0",
  
  "commander_relationships": {
    "lord": {
      "id": "string",
      "name": "string",
      "relation": 0,
      "trust": 0,
      "respect": 0,
      "last_interaction": "CampaignTime",
      "standing": "new | trusted | favored | right_hand | strained | distrusted"
    },
    
    "faction_leader": {
      "id": "string",
      "name": "string",
      "relation": 0,
      "awareness": "unknown | heard_of | met | known | respected",
      "last_interaction": "CampaignTime | null"
    },
    
    "fellow_officers": [
      {
        "id": "string",
        "name": "string",
        "rank": "Lieutenant | Captain | Commander",
        "relation": 0,
        "disposition": "friendly | neutral | rival | hostile",
        "history": []
      }
    ],
    
    "retinue_overall": {
      "loyalty": 50,
      "morale": 50,
      "respect": 50,
      "trust": 50,
      "cohesion": 50
    },
    
    "notable_soldiers": [
      {
        "soldier_id": "string",
        "relationship_type": "trusted_sergeant | problem_soldier | promising_recruit | old_veteran",
        "relation": 0,
        "special_events": []
      }
    ]
  }
}
```

### Relationship Modifiers

```json
{
  "relationship_modifiers": {
    "lord_relation": {
      "sources": [
        { "id": "battle_victory", "value": 5, "max_per_month": 15 },
        { "id": "battle_defeat", "value": -3, "max_per_month": -10 },
        { "id": "low_casualties", "value": 3, "condition": "survival_rate >= 90%" },
        { "id": "high_casualties", "value": -5, "condition": "survival_rate < 50%" },
        { "id": "disobeyed_order", "value": -15 },
        { "id": "exceeded_expectations", "value": 10 },
        { "id": "event_choice", "value": "varies" },
        { "id": "time_served", "value": 1, "per_days": 30 }
      ],
      "decay": {
        "rate": -1,
        "per_days": 60,
        "decay_toward": 0,
        "min_before_decay": 30
      }
    },
    
    "soldier_loyalty": {
      "sources": [
        { "id": "paid_on_time", "value": 2, "per_payday": true },
        { "id": "pay_delayed", "value": -5, "per_days": 7 },
        { "id": "victory", "value": 3 },
        { "id": "defeat", "value": -2 },
        { "id": "comrade_died", "value": -3 },
        { "id": "commander_led_from_front", "value": 5 },
        { "id": "commander_stayed_back", "value": -2 },
        { "id": "good_supplies", "value": 1, "per_week": true },
        { "id": "poor_supplies", "value": -3, "per_week": true },
        { "id": "commander_protective", "value": 2, "per_battle": true, "condition": "command_style == protective" },
        { "id": "event_choice", "value": "varies" }
      ],
      "decay": {
        "rate": -1,
        "per_days": 14,
        "decay_toward": 50
      }
    },
    
    "soldier_respect": {
      "sources": [
        { "id": "battle_victory", "value": 3 },
        { "id": "battle_defeat", "value": -2 },
        { "id": "tactical_success", "value": 5 },
        { "id": "tactical_blunder", "value": -8 },
        { "id": "commander_wounded", "value": 5, "description": "fought alongside us" },
        { "id": "fair_discipline", "value": 2 },
        { "id": "harsh_discipline", "value": -3, "also_adds": { "fear": 5 } },
        { "id": "event_choice", "value": "varies" }
      ]
    },
    
    "retinue_cohesion": {
      "sources": [
        { "id": "time_together", "value": 1, "per_days": 7, "max": 80 },
        { "id": "battle_together", "value": 3, "max": 90 },
        { "id": "new_soldier_added", "value": -2 },
        { "id": "soldier_died", "value": -5 },
        { "id": "soldier_deserted", "value": -8 },
        { "id": "shared_hardship", "value": 5, "event_triggered": true },
        { "id": "internal_conflict", "value": -10, "event_triggered": true }
      ]
    }
  }
}
```

### Standing Thresholds

```json
{
  "lord_standing_thresholds": {
    "distrusted": { "max": -20 },
    "strained": { "min": -19, "max": -1 },
    "new": { "min": 0, "max": 19 },
    "trusted": { "min": 20, "max": 39 },
    "favored": { "min": 40, "max": 59 },
    "right_hand": { "min": 60 }
  },
  
  "retinue_standing_thresholds": {
    "mutinous": { "max": 19 },
    "disgruntled": { "min": 20, "max": 39 },
    "neutral": { "min": 40, "max": 59 },
    "loyal": { "min": 60, "max": 79 },
    "devoted": { "min": 80 }
  }
}
```

---

## Commander Promotions

Promotion requirements and state for T7-T9.

```json
{
  "schemaVersion": "1.0",
  
  "commander_promotions": {
    "t6_to_t7": {
      "promotion_id": "promotion_t6_t7",
      "from_tier": 6,
      "to_tier": 7,
      "rank_title": "Lieutenant",
      
      "eligibility": {
        "xp_required": 11000,
        "min_days_at_t6": 84,
        "min_lord_relation": 30,
        "max_discipline": 3,
        "events_completed_total": 70,
        "battles_survived_total": 40
      },
      
      "proving_event": {
        "event_id": "promotion_t6_t7_the_commission",
        "can_fail": false,
        "can_decline": true,
        "decline_cooldown_days": 30
      },
      
      "on_promote": {
        "unlocks": ["retinue_system", "commander_events"],
        "retinue_cap": 20,
        "relation_changes": {
          "lord": 10
        },
        "resets": {
          "soldier_reputation": true,
          "soldier_relationships": true
        }
      }
    },
    
    "t7_to_t8": {
      "promotion_id": "promotion_t7_t8",
      "from_tier": 7,
      "to_tier": 8,
      "rank_title": "Captain",
      
      "eligibility": {
        "xp_required": 14000,
        "min_days_at_t7": 126,
        "min_lord_relation": 40,
        "max_discipline": 3,
        "min_battles_commanded": 15,
        "min_soldier_survival_rate": 70
      },
      
      "proving_event": {
        "event_id": "promotion_t7_t8_test_of_command",
        "can_fail": true,
        "failure_cooldown_days": 60,
        "failure_followup_event": "promotion_t7_t8_failure_recovery"
      },
      
      "on_promote": {
        "unlocks": ["command_style_tag"],
        "retinue_cap": 30,
        "relation_changes": {
          "lord": 10,
          "faction_leader": 5
        },
        "sets_tag": "command_style_tag"
      }
    },
    
    "t8_to_t9": {
      "promotion_id": "promotion_t8_t9",
      "from_tier": 8,
      "to_tier": 9,
      "rank_title": "Commander",
      
      "eligibility": {
        "xp_required": 18000,
        "min_days_at_t8": 168,
        "min_lord_relation": 50,
        "min_faction_leader_relation": 20,
        "max_discipline": 2,
        "min_battles_commanded": 30,
        "min_campaign_victories": 3
      },
      
      "proving_event": {
        "event_id": "promotion_t8_t9_council_of_war",
        "can_fail": true,
        "failure_cooldown_days": 90,
        "failure_followup_event": "promotion_t8_t9_prove_worth"
      },
      
      "on_promote": {
        "unlocks": ["strategic_style_tag", "war_council_access"],
        "retinue_cap": 40,
        "relation_changes": {
          "lord": 15,
          "faction_leader": 10
        },
        "sets_tag": "strategic_style_tag"
      }
    }
  },
  
  "promotion_state": {
    "current_tier": 7,
    "days_in_current_tier": 0,
    
    "eligibility_t8": {
      "is_eligible": false,
      "blocking_reasons": [],
      "progress": {
        "xp": { "current": 11500, "required": 14000 },
        "days": { "current": 45, "required": 126 },
        "lord_relation": { "current": 35, "required": 40 },
        "battles_commanded": { "current": 8, "required": 15 },
        "survival_rate": { "current": 85, "required": 70 }
      }
    },
    
    "retry_cooldowns": {
      "t7_t8": "CampaignTime | null",
      "t8_t9": "CampaignTime | null"
    }
  }
}
```

---

## Commander Events Framework

Framework for commander-specific events (content not included).

### Event Categories

```json
{
  "commander_event_categories": [
    {
      "id": "command",
      "name": "Command Events",
      "description": "Events about leading soldiers in and out of battle",
      "delivery": "automatic",
      "tier_range": { "min": 7, "max": 9 },
      "examples": [
        "Soldier requests transfer",
        "Discipline problem in retinue",
        "Soldier distinguishes themselves",
        "Pre-battle preparation",
        "Post-battle assessment"
      ]
    },
    {
      "id": "logistics",
      "name": "Logistics Events", 
      "description": "Events about supply, equipment, pay",
      "delivery": "automatic",
      "tier_range": { "min": 7, "max": 9 },
      "examples": [
        "Supply shortage",
        "Equipment request",
        "Pay dispute",
        "Requisition denied"
      ]
    },
    {
      "id": "political",
      "name": "Political Events",
      "description": "Events about relationships with lord, officers, faction",
      "delivery": "automatic",
      "tier_range": { "min": 7, "max": 9 },
      "examples": [
        "Lord's orders conflict with soldier welfare",
        "Rival officer undermines you",
        "Faction leader notices you",
        "Noble visits camp"
      ]
    },
    {
      "id": "soldier_personal",
      "name": "Soldier Personal Events",
      "description": "Events about individual soldiers",
      "delivery": "automatic",
      "tier_range": { "min": 7, "max": 9 },
      "examples": [
        "Soldier shares background",
        "Soldier asks for advice",
        "Soldier conflict with another",
        "Soldier's family matter"
      ]
    },
    {
      "id": "retinue_social",
      "name": "Retinue Social Events",
      "description": "Events about retinue morale and cohesion",
      "delivery": "automatic | player_initiated",
      "tier_range": { "min": 7, "max": 9 },
      "examples": [
        "Campfire gathering",
        "Victory celebration",
        "Mourning fallen",
        "Retinue tradition"
      ]
    },
    {
      "id": "training_command",
      "name": "Command Training Events",
      "description": "Training and drilling your retinue",
      "delivery": "player_initiated",
      "menu": "commander_activities",
      "tier_range": { "min": 7, "max": 9 },
      "examples": [
        "Formation drill",
        "Individual training",
        "War games",
        "Night exercise"
      ]
    },
    {
      "id": "recruitment",
      "name": "Recruitment Events",
      "description": "Events about adding soldiers to retinue",
      "delivery": "automatic | player_initiated",
      "tier_range": { "min": 7, "max": 9 },
      "examples": [
        "Battlefield pickup opportunity",
        "Volunteer approaches",
        "Lord assigns soldiers",
        "Purchase from settlement"
      ]
    },
    {
      "id": "crisis",
      "name": "Crisis Events",
      "description": "High-stakes command moments",
      "delivery": "automatic",
      "priority": "high",
      "tier_range": { "min": 7, "max": 9 },
      "examples": [
        "Soldier mutiny brewing",
        "Mass desertion threat",
        "Ambush - quick decisions",
        "Lord demands sacrifice"
      ]
    }
  ]
}
```

### Commander Event Schema

```json
{
  "commander_event": {
    "id": "string",
    "category": "command | logistics | political | soldier_personal | retinue_social | training_command | recruitment | crisis",
    
    "metadata": {
      "tier_range": { "min": 7, "max": 9 },
      "requires_tag": "command_style_tag | strategic_style_tag | null",
      "tag_variants": {
        "dutiful": { "setup_override": "string", "options_override": [] },
        "protective": { "setup_override": "string", "options_override": [] }
      }
    },
    
    "delivery": {
      "method": "automatic | player_initiated",
      "menu": "commander_activities | null"
    },
    
    "triggers": {
      "all": ["is_commander", "ai_safe"],
      "any": [],
      "time_of_day": [],
      "requires_soldiers": true,
      "min_retinue_size": 0,
      "min_battles_commanded": 0,
      "soldier_conditions": {
        "has_wounded": false,
        "has_low_loyalty": false,
        "has_high_veterancy": false
      }
    },
    
    "targets": {
      "soldier_selection": "random | lowest_loyalty | highest_loyalty | newest | most_veteran | specific_trait",
      "soldier_count": 1,
      "officer_involved": false,
      "lord_involved": false
    },
    
    "timing": {
      "cooldown_days": 0,
      "priority": "normal | high | critical",
      "one_time": false
    },
    
    "content": {
      "title": "string",
      "setup": "string with placeholders",
      "options": [
        {
          "id": "string",
          "text": "string",
          "condition": "string | null",
          "effects": {
            "soldier_loyalty": 0,
            "soldier_respect": 0,
            "retinue_morale": 0,
            "retinue_cohesion": 0,
            "lord_relation": 0,
            "faction_leader_relation": 0,
            "discipline": 0,
            "gold": 0
          },
          "soldier_effects": {
            "target": "trigger_soldier | all | wounded | specific",
            "loyalty": 0,
            "respect": 0,
            "add_trait": "string | null",
            "remove_trait": "string | null",
            "wound": false,
            "kill": false,
            "desert": false
          },
          "outcome": "string"
        }
      ]
    }
  }
}
```

### Commander Placeholders

```json
{
  "commander_placeholders": [
    { "key": "{COMMANDER_RANK}", "description": "Player's commander rank title" },
    { "key": "{RETINUE_NAME}", "description": "Retinue name (if named)" },
    { "key": "{RETINUE_SIZE}", "description": "Current retinue count" },
    { "key": "{RETINUE_MAX}", "description": "Current retinue cap" },
    
    { "key": "{SOLDIER_NAME}", "description": "Target soldier's name" },
    { "key": "{SOLDIER_SHORT}", "description": "Target soldier's short name" },
    { "key": "{SOLDIER_RANK}", "description": "Target soldier's troop type" },
    { "key": "{SOLDIER_BATTLES}", "description": "Battles the soldier has survived" },
    { "key": "{SOLDIER_TRAIT}", "description": "Relevant trait for event" },
    
    { "key": "{WOUNDED_COUNT}", "description": "Number of wounded soldiers" },
    { "key": "{DEAD_COUNT}", "description": "Soldiers lost this campaign" },
    { "key": "{MORALE_LEVEL}", "description": "Retinue morale description" },
    { "key": "{COHESION_LEVEL}", "description": "Retinue cohesion description" },
    
    { "key": "{BATTLES_COMMANDED}", "description": "Total battles commanded" },
    { "key": "{SURVIVAL_RATE}", "description": "Soldier survival rate percent" },
    
    { "key": "{RIVAL_OFFICER_NAME}", "description": "Rival officer's name" },
    { "key": "{FELLOW_OFFICER_NAME}", "description": "Fellow officer's name" }
  ]
}
```

---

## Commander Tracking Stats

Statistics tracked for T7+ commanders.

```json
{
  "schemaVersion": "1.0",
  
  "commander_stats": {
    "battles": {
      "battles_commanded": 0,
      "battles_won": 0,
      "battles_lost": 0,
      "battles_retreated": 0,
      "largest_battle_commanded": 0
    },
    
    "soldiers": {
      "total_recruited": 0,
      "total_lost_killed": 0,
      "total_lost_deserted": 0,
      "total_lost_transferred": 0,
      "total_wounded": 0,
      "current_alive": 0,
      "longest_serving_days": 0,
      "most_kills_single_soldier": 0
    },
    
    "survival": {
      "lifetime_survival_rate": 100,
      "last_10_battles_survival_rate": 100,
      "current_campaign_survival_rate": 100
    },
    
    "campaigns": {
      "campaigns_participated": 0,
      "campaign_victories": 0,
      "campaign_defeats": 0,
      "sieges_participated": 0,
      "sieges_won": 0
    },
    
    "command": {
      "orders_obeyed": 0,
      "orders_disobeyed": 0,
      "soldiers_disciplined": 0,
      "soldiers_promoted": 0,
      "mutinies_prevented": 0,
      "desertions": 0
    },
    
    "reputation": {
      "commander_reputation": 0,
      "known_for": [],
      "reputation_modifiers": [
        { "source": "high_survival", "value": 10, "description": "Keeps soldiers alive" },
        { "source": "harsh_discipline", "value": -5, "description": "Hard on troops" }
      ]
    }
  }
}
```

### Stat Thresholds for Events/Triggers

```json
{
  "stat_thresholds": {
    "survival_rate": {
      "excellent": 90,
      "good": 75,
      "acceptable": 60,
      "poor": 45,
      "terrible": 30
    },
    "morale": {
      "excellent": 80,
      "good": 60,
      "neutral": 40,
      "low": 25,
      "critical": 10
    },
    "cohesion": {
      "tight_knit": 80,
      "solid": 60,
      "forming": 40,
      "fractured": 25,
      "broken": 10
    },
    "loyalty": {
      "devoted": 80,
      "loyal": 60,
      "neutral": 40,
      "disgruntled": 25,
      "mutinous": 10
    }
  }
}
```

---

## Feature Flags

```json
{
  "commander_feature_flags": {
    "commander_track": {
      "enabled": true,
      "min_tier_to_unlock": 6
    },
    
    "retinue": {
      "enabled": true,
      "soldier_permadeath": true,
      "soldier_traits": true,
      "soldier_relationships": true,
      "soldier_history": true,
      "retinue_morale": true,
      "retinue_cohesion": true
    },
    
    "commander_events": {
      "enabled": true,
      "command_events": true,
      "logistics_events": true,
      "political_events": true,
      "soldier_personal_events": true,
      "retinue_social_events": true,
      "recruitment_events": true,
      "crisis_events": true
    },
    
    "commander_promotions": {
      "enabled": true,
      "t7_proving_event": true,
      "t8_proving_event": true,
      "t9_proving_event": true,
      "can_decline_commission": true
    },
    
    "commander_relationships": {
      "enabled": true,
      "lord_relation_tracking": true,
      "officer_rivals": true,
      "faction_leader_awareness": true,
      "soldier_individual_relations": true
    },
    
    "commander_stats": {
      "enabled": true,
      "survival_rate_tracking": true,
      "campaign_tracking": true,
      "reputation_system": true
    }
  }
}
```

---

## Trigger Conditions

Commander-specific trigger conditions for events.

```json
{
  "commander_triggers": [
    { "id": "is_commander", "description": "Player is T7+" },
    { "id": "tier_is:{n}", "description": "Player is exactly tier n" },
    { "id": "tier_min:{n}", "description": "Player is tier n or higher" },
    
    { "id": "retinue_size_min:{n}", "description": "Retinue has at least n soldiers" },
    { "id": "retinue_size_max:{n}", "description": "Retinue has at most n soldiers" },
    { "id": "retinue_has_vacancy", "description": "Retinue is not full" },
    { "id": "retinue_is_full", "description": "Retinue is at capacity" },
    
    { "id": "retinue_morale_min:{n}", "description": "Retinue morale >= n" },
    { "id": "retinue_morale_max:{n}", "description": "Retinue morale <= n" },
    { "id": "retinue_cohesion_min:{n}", "description": "Retinue cohesion >= n" },
    { "id": "retinue_loyalty_min:{n}", "description": "Average soldier loyalty >= n" },
    
    { "id": "has_wounded_soldiers", "description": "At least one soldier is wounded" },
    { "id": "wounded_count_min:{n}", "description": "At least n soldiers wounded" },
    { "id": "has_veteran_soldiers", "description": "At least one soldier with 5+ battles" },
    { "id": "has_new_soldiers", "description": "At least one soldier with <3 battles" },
    
    { "id": "soldier_with_trait:{trait}", "description": "Has soldier with specific trait" },
    { "id": "soldier_loyalty_below:{n}", "description": "Has soldier with loyalty below n" },
    
    { "id": "battles_commanded_min:{n}", "description": "Commanded at least n battles" },
    { "id": "survival_rate_min:{n}", "description": "Survival rate >= n%" },
    { "id": "survival_rate_max:{n}", "description": "Survival rate <= n%" },
    
    { "id": "lord_relation_min:{n}", "description": "Lord relation >= n" },
    { "id": "lord_relation_max:{n}", "description": "Lord relation <= n" },
    { "id": "faction_leader_relation_min:{n}", "description": "Faction leader relation >= n" },
    
    { "id": "has_command_style_tag:{tag}", "description": "Has specific command style" },
    { "id": "has_strategic_style_tag:{tag}", "description": "Has specific strategic style" },
    
    { "id": "after_battle", "description": "Within 2 days of battle" },
    { "id": "after_victory", "description": "Last battle was victory" },
    { "id": "after_defeat", "description": "Last battle was defeat" },
    { "id": "soldiers_died_last_battle", "description": "Lost soldiers in last battle" },
    
    { "id": "campaign_active", "description": "Currently in a campaign" },
    { "id": "besieging", "description": "Currently besieging" },
    { "id": "being_besieged", "description": "Currently under siege" }
  ]
}
```

---

## Persistence

Safe persistence for commander state using primitives only.

```json
{
  "commander_persistence": {
    "method": "IDataStore.SyncData",
    "primitives_only": true,
    
    "state_fields": [
      { "field": "is_commander", "type": "bool" },
      { "field": "commander_tier", "type": "int" },
      { "field": "days_as_commander", "type": "int" },
      { "field": "commission_date", "type": "CampaignTime" },
      
      { "field": "command_style_tag", "type": "string" },
      { "field": "strategic_style_tag", "type": "string" },
      { "field": "declined_commission", "type": "bool" },
      
      { "field": "lord_id", "type": "string" },
      { "field": "lord_relation", "type": "int" },
      { "field": "faction_leader_relation", "type": "int" },
      
      { "field": "retinue_morale", "type": "int" },
      { "field": "retinue_cohesion", "type": "int" }
    ],
    
    "soldier_persistence": {
      "method": "flattened_arrays",
      "max_soldiers": 40,
      "fields_per_soldier": [
        "id", "name", "troop_id", "formation",
        "is_alive", "is_wounded", "wound_days",
        "battles", "kills", "days_served",
        "loyalty", "respect",
        "trait_1", "trait_2", "trait_3"
      ]
    },
    
    "stats_persistence": {
      "method": "flat_fields",
      "fields": [
        "battles_commanded", "battles_won", "battles_lost",
        "total_recruited", "total_lost",
        "campaign_victories", "campaign_defeats"
      ]
    }
  }
}
```

---

## File Organization

```
ModuleData/Enlisted/
├── commander/
│   ├── commander_config.json         # Feature flags, thresholds
│   ├── retinue_config.json           # Capacity, recruitment sources
│   ├── soldier_traits.json           # Trait definitions
│   ├── relationship_modifiers.json   # How relations change
│   └── promotions_t7_t9.json         # Promotion requirements
├── events/
│   ├── events_commander_command.json
│   ├── events_commander_logistics.json
│   ├── events_commander_political.json
│   ├── events_commander_soldier.json
│   ├── events_commander_social.json
│   ├── events_commander_recruitment.json
│   ├── events_commander_crisis.json
│   └── events_commander_promotion.json
└── ...
```

---

*Schema Version: 1.0*
*For use with: Enlisted Commander Track, Tiers 7-9*
