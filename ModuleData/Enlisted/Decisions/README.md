# Decisions/

Player-initiated actions from the Camp Hub menu.

**Spec:** `docs/Content/content-index.md` §Decisions

---

## How It Works

**Delivery Method:** Player-initiated from Camp Hub menu  
**Trigger:** Player presses 'C' → navigates to category → selects action  
**System:** Loaded by `EventCatalog` → filtered by `DecisionCatalog` → displayed in Camp Hub menu

**Decision Flow:**
```
1. Player opens Camp Hub (C key)
2. Navigates to category (Training, Social, Economic, etc.)
3. Sees available decisions (filtered by tier, cooldown, requirements)
4. Selects decision → sees setup text → chooses option → gets result
5. Cooldown applied, effects processed
```

**Recognition:** Decisions use `dec_` ID prefix (e.g., `dec_rest`, `dec_spar`)

---

## File

| File | Content | Count |
|------|---------|-------|
| `decisions.json` | All player decisions | 34 |

## Categories

| Category | Count | Decisions |
|----------|-------|-----------|
| Self-Care | 3 | Rest, Extended Rest, Seek Treatment |
| Training | 9 | Weapon Drill, Spar, Endurance, Study Tactics, Practice Medicine, Train Troops, Combat Drill, Weapon Specialization, Lead Drill |
| Social | 6 | Join the Men, Join Drinking, Seek Officers, Keep to Self, Write Letter, Confront Rival |
| Economic | 5 | Gamble Low, Gamble High, Side Work, Shady Deal, Visit Market |
| Career | 3 | Request Audience, Volunteer for Duty, Request Leave |
| Information | 3 | Listen to Rumors, Scout Area, Check Supplies |
| Equipment | 2 | Maintain Gear, Visit Quartermaster |
| Risk-Taking | 3 | Dangerous Wager, Prove Courage, Challenge |

## Not in This Folder

The following are **triggered events** (game-initiated based on conditions), not Camp Hub decisions. They remain in `Events/`:

### Automatic Decision Events (`events_decisions.json`)
**Delivery:** Game triggers as popup inquiry based on conditions  
**Examples:** Lord's hunt invitation, training offers from NCO, quartermaster deals  
**Recognition:** `decision_*` prefix

### Player-Initiated Event Popups (`events_player_decisions.json`)
**Delivery:** Player initiates from menu, but delivered as popup inquiry  
**Examples:** Organize dice game, request extra training, visit wounded  
**Recognition:** `player_*` prefix  
**Difference from Camp Hub decisions:** These open as popup dialogs rather than inline menu selections

## Summary

| Type | File | Prefix | Delivery | Player Control |
|------|------|--------|----------|----------------|
| **Camp Hub Decisions** | `Decisions/decisions.json` | `dec_` | Inline menu selection | Full (when to trigger) |
| **Player-Initiated Event Popups** | `Events/events_player_decisions.json` | `player_` | Popup inquiry | Full (when to trigger) |
| **Automatic Decision Events** | `Events/events_decisions.json` | `decision_` | Popup inquiry | Partial (only response) |

