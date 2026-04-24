# Writing Style Guide

**Summary:** Authoritative guide for writing Bannerlord-flavored roleplay text. Covers voice, tone, vocabulary, and structure for all narrative content (events, decisions, orders, dialogue). Follow this guide when creating or reviewing content.

**Status:** ✅ Current  
**Last Updated:** 2026-01-03 (Added Status Forecasts section for meaningful player status text)  
**Related Docs:** [Storylet Backbone](storylet-backbone.md), [Content Reference](README.md)

---

## Index

1. [Core Voice](#core-voice)
2. [Tense & Perspective](#tense--perspective)
3. [Sentence Structure](#sentence-structure)
4. [Vocabulary](#vocabulary)
5. [Setup Text](#setup-text)
6. [Option Text](#option-text)
7. [Result Text](#result-text)
8. [Tooltips](#tooltips)
9. [Opportunity Hints](#opportunity-hints)
10. [Status Forecasts](#status-forecasts)
11. [Dynamic Tokens](#dynamic-tokens)
12. [Dialogue](#dialogue)
13. [Common Mistakes](#common-mistakes)
14. [Examples](#examples)
15. [Technical Reference](#technical-reference)
16. [Quick Reference](#quick-reference)

---

## Core Voice

The Enlisted writing voice is **terse military prose**. We write like a soldier thinks—direct, physical, unsentimental. No purple prose. No modern psychology. Show the mud, steel, and consequences.

### Principles

| Principle | Description |
|-----------|-------------|
| **Direct** | Say it plainly. No flourishes or explanations. |
| **Physical** | Describe what the body feels, not abstract emotions. |
| **Ambiguous morality** | Show choices and consequences, not judgments. |
| **Grounded** | Medieval military reality. Dirty, hungry, tired. |
| **Laconic** | Soldiers don't talk much. Neither should the text. |

### Tone by Context

| Context | Tone | Example |
|---------|------|---------|
| **Routine** | Dry, matter-of-fact | "Another day of drills. Your arms ache." |
| **Danger** | Terse, focused | "Movement in the treeline. Steel out." |
| **Aftermath** | Weary, reflective | "The blood isn't yours. You're not sure if that helps." |
| **Social** | Guarded, watchful | "The veterans fall silent when you approach." |
| **Authority** | Formal, clipped | "The captain's eyes don't leave yours." |

---

## Tense & Perspective

### Rules

| Element | Rule | Example |
|---------|------|---------|
| **Perspective** | Second person ("you") | "You see...", "Your blade..." |
| **Setup text** | Present tense | "A wounded soldier lies in the mud." |
| **Result text** | Past tense for action, present for consequence | "You took the gold. It weighs heavy." |
| **Ongoing state** | Present tense | "The camp is quiet now." |

### Correct Examples

```
Setup (present):
"The battle is over. Bodies scatter the field. You notice a glint of gold."

Result (past action → present state):
"You pocketed the coins. No one saw. The weight feels heavier than it should."
```

### Incorrect Examples

```
❌ "You will see a wounded soldier lying in the mud."  (future tense)
❌ "The player notices gold on a corpse."  (third person)
❌ "You had taken the coins and felt guilty."  (past perfect, too distant)
```

---

## Sentence Structure

### Keep It Short

- **Average sentence:** 8-12 words
- **Maximum sentence:** 20 words (rare)
- **Fragments are good:** "Steel out. Eyes forward."

### Rhythm Pattern

Vary between statement → short punch → statement:

```
✅ "The sergeant approaches. His face says nothing. You know what this means."

❌ "The sergeant approaches with a stern expression on his weathered face that tells you immediately that something is wrong and you're probably in trouble."
```

### Punctuation

| Use | For |
|-----|-----|
| **Period** | Most endings. Keep it clipped. |
| **Em dash** | Interruption, internal thought, or list. "Mercy or cruelty—you're not sure which." |
| **Ellipsis** | Trailing off (rare). "You could refuse, but..." |
| **Comma** | Only when grammatically required. Fewer is better. |

### Avoid

- Semicolons (too literary)
- Exclamation marks (soldiers don't exclaim)
- Question marks in narration (save for dialogue)
- Parenthetical asides

---

## Vocabulary

### Medieval Military Register

Use words a medieval soldier would know. Avoid modern terms.

| ✅ Use | ❌ Avoid |
|--------|----------|
| camp, garrison, formation | base, facility, unit |
| gold, coin, denars | money, cash, funds |
| blade, steel, weapon | equipment, armament |
| sergeant, captain, lord | officer, commander, leadership |
| march, ride, advance | travel, proceed, relocate |
| rations, provisions | food supplies, consumables |
| wounded, bloodied | injured, harmed |
| mud, dust, cold, hunger | discomfort, adverse conditions |

### Sensory Over Abstract

| ✅ Physical | ❌ Abstract |
|-------------|-------------|
| "Your legs ache" | "You feel tired" |
| "The blood dries on your hands" | "You process what happened" |
| "His stare follows you" | "He seems disapproving" |
| "Steel scrapes leather" | "You draw your weapon" |
| "Smoke stings your eyes" | "The fire is burning" |

### Emotion Through Action

Never name emotions. Show them through physical reactions.

| ❌ Telling | ✅ Showing |
|-----------|-----------|
| "You feel guilty" | "The weight feels heavier than it should" |
| "He's angry" | "His jaw tightens" |
| "You're scared" | "Your hands won't stop shaking" |
| "She's grateful" | "She nods once, says nothing" |
| "They're suspicious" | "Eyes follow you as you pass" |

### Bannerlord-Specific Terms

| Term | When to Use |
|------|-------------|
| **Lord/Lady** | The player's employer (your lord) |
| **Captain** | Company commander, officer |
| **Sergeant** | NCO, direct supervisor |
| **Quartermaster** | Supplies, equipment |
| **Company** | The player's unit |
| **Camp** | Where the army rests |
| **March** | Army movement |
| **Garrison** | Stationed in settlement |
| **Muster** | Regular inspection/pay |

### Cultural Faction Notes

Factions have distinct flavors, but don't overdo accent or dialect:

| Faction | Flavor Notes |
|---------|--------------|
| **Empire** | Formal, bureaucratic, references to law and order |
| **Vlandia** | Feudal honor, knightly codes, siege warfare |
| **Sturgians** | Harsh, direct, cold-weather references |
| **Aserai** | Trade references, desert/heat, hospitality codes |
| **Khuzait** | Horse culture, mobility, steppe references |
| **Battania** | Forest/hill terrain, clan loyalty, raiding tradition |

Use faction flavor sparingly. Most events work across all cultures.

---

## Setup Text

The setup text introduces the situation. It's the "what do you see" moment.

### Structure

1. **Concrete opening** - What's happening right now
2. **Context** - Why it matters
3. **Implicit question** - What will you do? (never ask directly)

### Length

- **Target:** 2-4 sentences
- **Maximum:** 5 sentences
- **Minimum:** 1 sentence (for simple events)

### Good Setup Patterns

```
[Concrete detail] + [Stakes] + [Choice implied]

"The battle is over. Bodies are scattered across the field. You notice a glint of gold on a corpse near your feet—someone's purse, spilled open. Officers aren't looking this way."
```

```
[Someone acts] + [Your position] + [What's at stake]

"A drunk soldier stumbles toward your post, demanding to pass. {SERGEANT} told you no one passes without authorization."
```

```
[Physical state] + [Internal moment]

"You stare at your hands, still trembling. The blood isn't yours. If this was your first kill, it's hitting you now."
```

### Bad Setup Patterns

```
❌ Too long, too explanatory:
"You find yourself in a difficult situation. After the battle, which was hard-fought, you're walking through the aftermath and you see something interesting that presents you with a moral dilemma about whether or not to take it..."

❌ Too modern/psychological:
"You're processing complex emotions about what just happened. Your trauma response is kicking in and you need to decide how to cope with the stress of combat."

❌ Breaks perspective:
"The player notices that there's a wounded enemy nearby. They must decide what to do about this interesting narrative opportunity."
```

---

## Option Text

Options are what the player can choose. Keep them **short and decisive**.

### Rules

- **Maximum:** 8 words (aim for 4-6)
- **Start with a verb** when possible
- **No explanations** (that's what tooltips are for)
- **No hedging** ("Maybe try to..." ❌)

### Good Options

```
"Take the gold"
"Leave it be"
"Report the find to an officer"
"End his suffering"
"Walk away"
"Rally the men"
"Hold your post and watch"
```

### Bad Options

```
❌ "Perhaps you should consider taking the gold if you think it's worth the risk"
❌ "You could potentially try to help the wounded person"
❌ "The choice to report this to an officer might be wise"
❌ "Click here to select the brave option"
```

---

## Result Text

Result text shows what happened after the choice. **Past tense for action, present for state.**

### Structure

1. **What you did** (past tense, brief)
2. **Immediate consequence** (what changed)
3. **Lingering thought** (optional, present tense)

### Length

- **Target:** 2-3 sentences
- **Maximum:** 4 sentences
- **Minimum:** 1 sentence

### Good Result Patterns

```
[Action] + [Consequence] + [Lingering state]

"You pocket the coins quickly, glancing around. No one saw. The weight feels heavier than it should."
```

```
[Action with detail] + [Someone's reaction]

"You grab the nearest soldiers and push toward the main body. It's ugly, brutal, and some don't make it—but you survive."
```

```
[What you did] + [Ambiguous reflection]

"You draw your blade and finish it quickly. Mercy or cruelty—you're not sure which. The field is quieter now."
```

### Bad Result Patterns

```
❌ Too explanatory:
"By taking the gold, you have made a morally questionable decision that may haunt you later. This represents your character's willingness to prioritize personal gain over ethical considerations."

❌ Too rewarding/judging:
"Great choice! You did the right thing and everyone respects you now. You should feel proud of yourself!"

❌ Future promises:
"This will definitely come back to help you later in the game when you need gold for important purchases."
```

---

## Tooltips

Tooltips explain mechanics. They are **NOT narrative text**.

### Rules from BLUEPRINT

- One sentence, under 80 characters
- Factual description of what happens
- Format: action + side effects + cooldown

### Structure

```
[What happens]. [Side effects]. [Timing if relevant].
```

### Good Tooltips

```
"Trains equipped weapon. Causes fatigue. 3 day cooldown."
"Risky choice. Adds 2 scrutiny. 25 gold reward."
"+5 Officer rep, -3 Soldier rep."
"Requires Medicine 20. +2 Officer rep. Mercy trait XP."
"45% to win 150 gold. +3 Soldier rep. Costs 50 gold."
"Safe choice. Honor trait XP."
```

### Bad Tooltips

```
❌ "This is a risky option that might get you in trouble with the officers if you're caught, but could pay off handsomely"
❌ "Take a chance!"
❌ "" (empty - never allowed)
❌ null (never allowed)
```

---

## Opportunity Hints

Hints are short narrative snippets that foreshadow upcoming camp activities. They appear in the Daily Brief before the opportunity becomes available.

### Purpose

Hints create immersion by making the camp feel alive with gossip and activity. The player sees "Torgan mentioned a card game tonight" before the Card Game opportunity appears in the menu.

### Hint Categories

Hints are auto-categorized for display:

| Category | Section | Styling | Pattern |
|----------|---------|---------|---------|
| **Camp Rumors** | Company Reports | Rumor (lavender) | What others are doing ("Hrolf is running dice tonight") |
| **Personal Hints** | Your Status | Default | Player needs ("Your condition needs attention") |

**Categorization Logic:**
- Hints starting with "Your" or "You" → Personal
- Hints containing medical terms (condition, wound, injury) → Personal
- Everything else → Camp Rumor

### Rules

| Rule | Description |
|------|-------------|
| **Length** | 1 sentence, max 10 words |
| **Tone** | Camp gossip, observation, not UI text |
| **Tokens** | Always use placeholders for names (`{SOLDIER_NAME}`) |
| **Phase-appropriate** | Match hint to opportunity's valid phases |
| **Optional** | Opportunities work without hints (no foreshadowing) |

### Good Hints

```
Camp Rumors (social activities):
"{VETERAN_1_NAME} mentioned morning drill."
"{SOLDIER_NAME} is running a card game tonight."
"{COMRADE_NAME} is looking for a sparring partner."
"The veterans are gathering for dice."
"{SERGEANT} is forming a foraging party."

Personal Hints (player needs):
"Your condition needs attention."
"Your hammock awaits."
"You're pushing yourself too hard."
"Your wound is worsening."
```

### Bad Hints

```
❌ "A card game opportunity is available at dusk."  (UI text, not narrative)
❌ "Someone might be having a card game tonight maybe."  (too vague, hedging)
❌ "The veterans are going to play cards by the fire this evening."  (too long)
❌ "Card game forming."  (too terse, missing personality)
❌ "The sergeant mentioned drill."  (hardcoded, should use {SERGEANT})
```

### JSON Structure

```json
{
  "id": "opp_card_game",
  "hintId": "opp_card_game_hint",
  "hint": "{SOLDIER_NAME} mentioned a card game tonight.",
  "validPhases": ["Dusk", "Night"]
}
```

**Note:** The `hint` field uses the same placeholders as other narrative text. See [Dynamic Tokens](#dynamic-tokens) for the complete list.

---

## Status Forecasts

Status forecasts appear in the player status section of the main menu. They tell the player what's actually happening—scheduled activities, deviations from routine, and upcoming commitments.

### Purpose

Status forecasts ground the player in the camp's daily rhythm. They answer: "What's going on right now?" and "What comes next?" Unlike motivational filler, forecasts convey **real information** from the game state.

### Principles

| Principle | Description |
|-----------|-------------|
| **Factual** | Describe actual scheduled activities, not generic encouragement |
| **Terse** | Short declarative statements. "Morning formation. Early drill." |
| **Meaningful** | Every word conveys information the player can act on |
| **State-aware** | Deviations and commitments override normal schedule |

### Text Sources

Forecasts are built from `ScheduledPhase` data:

| Source | When Used | Example |
|--------|-----------|---------|
| `Slot1Description` + `Slot2Description` | Normal routine | "Morning formation. Early drill." |
| `DeviationReason` | Schedule override | "Foraging detail. Supplies running low." |
| `PlayerCommitmentTitle` | Player scheduled something | "Card game scheduled." |
| Fallback | Both slots skipped | "Light duty. Nothing scheduled." |

### Good Forecasts

```
Normal schedule:
"Morning formation. Early drill."
"Combat training. Work details."
"Evening leisure. Trading and gambling."
"Rest and sleep."

With deviation:
"Foraging detail. Supplies running low."
"Extended rest. Company exhausted."
"Emergency drill. Readiness critical."

With player commitment:
"Sparring match scheduled."
"Card game scheduled."

Activity levels (non-routine):
"The captain's tent is busy. Expect orders soon."  (Intense)
"The company is on the move. Stay sharp."  (Active)
"Garrison duty. Nothing pressing."  (Quiet)
```

### Bad Forecasts

```
❌ "One task at a time, and the duty will be done."  (empty motivation, no information)
❌ "Afternoon stretches ahead. Make use of it."  (generic time-of-day filler)
❌ "Today might be interesting!"  (vague, no content)
❌ "Keep your spirits up, soldier."  (motivational, not factual)
❌ "The schedule shows training and then some other activities later."  (wordy, hedging)
```

### Formatting Rules

- **Fragments are good:** "Morning formation. Early drill." (not complete sentences)
- **Period separators:** Join slot descriptions with ". " 
- **No flourishes:** No "You will..." or "There may be..."
- **Deviation priority:** Deviation reason overrides normal schedule display
- **Commitment priority:** Player commitments override schedule display

### Color Styling

| Condition | Style | Example |
|-----------|-------|---------|
| Normal schedule | Default (gray) | "Morning formation. Early drill." |
| Deviation/override | Warning (yellow) | "Foraging detail. Supplies running low." |
| Variety assignment | Link (cyan) | "Assigned to patrol duty this morning." |
| Player commitment | Link (cyan) | "Card game scheduled." |
| Intense activity | Warning (yellow) | "The captain's tent is busy." |
| Active activity | Link (cyan) | "The company is on the move." |

**Note:** Variety assignments (from `orchestrator_overrides.json`) display their `activationText` directly in Player Status. Write `activationText` as complete immersive sentences that stand alone:

```json
✅ Good:
"activationText": "Assigned to patrol duty this morning."
"activationText": "Selected for a scouting mission."
"activationText": "Full equipment inspection ordered."

❌ Bad:
"activationText": "Patrol duty assigned"  (sounds mechanical)
"activationText": "Variety assignment"  (internal terminology)
```

---

## Dynamic Tokens

All text fields (`title`, `setup`, `text`, `resultText`, etc.) support placeholder variables that are replaced at runtime with actual game data. **Always use tokens for NPC names** to maintain immersion across different game states.

### Token Categories

#### Player Tokens

| Token | Description | Fallback |
|-------|-------------|----------|
| `{PLAYER_NAME}` | Player hero's first name | "Soldier" |
| `{PLAYER_RANK}` | Current military rank title | "soldier" |

#### Chain of Command Tokens

| Token | Description | Fallback |
|-------|-------------|----------|
| `{SERGEANT}` | Current NCO (short form) | "the Sergeant" |
| `{SERGEANT_NAME}` | NCO's full name | "the Sergeant" |
| `{NCO_NAME}` | Alias for sergeant | "the Sergeant" |
| `{NCO_TITLE}` | NCO rank title only | "Sergeant" |
| `{OFFICER_NAME}` | Random officer in company | "the Captain" |
| `{CAPTAIN_NAME}` | Company commander | "the Captain" |

#### Lord & Faction Tokens

| Token | Description | Fallback |
|-------|-------------|----------|
| `{LORD_NAME}` | Enlisted lord's name | "the Lord" |
| `{LORD_TITLE}` | Lord's title (e.g., "Count") | "Lord" |
| `{FACTION_NAME}` | Lord's clan name | "the warband" |
| `{KINGDOM_NAME}` | Kingdom name | "the realm" |

#### Comrade Tokens

| Token | Description | Fallback |
|-------|-------------|----------|
| `{SOLDIER_NAME}` | Random soldier | "a soldier" |
| `{COMRADE_NAME}` | Fellow soldier (familiar) | "your comrade" |
| `{VETERAN_1_NAME}` | Veteran soldier #1 | "an old soldier" |
| `{VETERAN_2_NAME}` | Veteran soldier #2 | "another veteran" |
| `{RECRUIT_NAME}` | New recruit | "the new recruit" |

#### Location Tokens

| Token | Description | Fallback |
|-------|-------------|----------|
| `{SETTLEMENT_NAME}` | Nearest settlement | "the settlement" |
| `{COMPANY_NAME}` | Party/company name | "the company" |
| `{TROOP_COUNT}` | Total soldiers in company | "20" |

#### Rank Progression Tokens

| Token | Description | Fallback |
|-------|-------------|----------|
| `{NEXT_RANK}` | Next promotion rank | "the next rank" |
| `{SECOND_RANK}` | Second-tier rank name | "Soldier" |

#### Order & Duty Tokens

| Token | Description | Fallback |
|-------|-------------|----------|
| `{ORDER_NAME}` | Current order name | "duty" |
| `{DAY}` | Current day of order | "1" |
| `{TOTAL}` | Total days for order | "3" |
| `{HOURS}` | Hours until next event | "4" |
| `{PHASE}` | Current phase description | "in progress" |
| `{DAYS}` | Generic day count | varies |

#### Skill Check Tokens (tooltipTemplate only)

| Token | Description | Example |
|-------|-------------|---------|
| `{CHANCE}` | Calculated success % | "73" |
| `{SKILL}` | Player's skill value | "45" |
| `{SKILL_NAME}` | Skill being checked | "Scouting" |

#### Specialized Tokens

**Medical Events:**
- `{CONDITION_TYPE}`, `{CONDITION_LOCATION}`, `{COMPLICATION_NAME}`, `{REMEDY_NAME}`

**Naval Events (Warsails DLC):**
- `{SHIP_NAME}`, `{BOATSWAIN_NAME}`, `{NAVIGATOR_NAME}`, `{DESTINATION_PORT}`, `{DAYS_AT_SEA}`

**Context-Specific:**
- `{PREVIOUS_LORD}`, `{ALLIED_LORD}`, `{ENEMY_FACTION_ADJECTIVE}` (used in specific event chains)

### Token Usage Examples

**In setup text:**
```json
"setup": "{SERGEANT} pulls you aside. 'Listen, {PLAYER_NAME}, we need someone to scout ahead. {LORD_NAME} wants intel on {SETTLEMENT_NAME} before we march.'"
```

**In option text:**
```json
"text": "Tell {SERGEANT_NAME} you'll do it."
```

**In result text:**
```json
"resultText": "You report back to {SERGEANT}. {LORD_NAME} nods when he hears the news."
```

**In tooltipTemplate (skill checks):**
```json
"tooltipTemplate": "{CHANCE}% ({SKILL_NAME} {SKILL}). +15 Scouting XP. Fail: -10 HP."
```

### Token Resolution

Tokens are resolved at runtime by multiple systems:

1. **EventDeliveryManager.SetEventTextVariables()** - Main event/decision tokens (NPC names, player info, lord info)
2. **EventDeliveryManager.GenerateDynamicTooltip()** - Skill check tooltips (CHANCE, SKILL, SKILL_NAME)
3. **ForecastGenerator / MenuHandlers** - Order tracking tokens (ORDER_NAME, DAY, HOURS, PHASE)
4. **Priority order:** Specific > Generic (e.g., `{SERGEANT_NAME}` before `{NCO_NAME}`)
5. **Culture-aware:** Names match enlisted lord's faction culture
6. **Fallback guaranteed:** Every token has a fallback if data unavailable
7. **Case-sensitive:** Must use exact casing `{LORD_NAME}` not `{lord_name}`

### When to Use Tokens

| Situation | Use Token | Example |
|-----------|-----------|---------|
| Referring to any NPC | ✅ Always | `{SERGEANT}`, `{SOLDIER_NAME}` |
| Player's name in dialogue | ✅ Yes | `{PLAYER_NAME}` |
| Current location | ✅ Yes | `{SETTLEMENT_NAME}` |
| Generic objects | ❌ No | "your blade", "the wagon" |
| Abstract concepts | ❌ No | "duty", "honor" |

### Token Anti-Patterns

```
❌ "The sergeant told you..."          (hardcoded, should use {SERGEANT})
❌ "{sergeant}"                         (wrong case)
❌ "{{SERGEANT}}"                       (double braces)
❌ "{SERGEANT} {SERGEANT} {SERGEANT}"  (token spam - varies once)
```

**See:** [Dynamic Tokens](#dynamic-tokens) for placeholder guidance.

**Implementation:** Tokens use Bannerlord's native `TextObject.SetTextVariable(key, value)` API with null-safe fallbacks (`?.ToString() ?? "fallback"`).

---

## Dialogue

Dialogue follows special rules for immersion.

### NPC Speech Patterns

| Rank | Pattern | Example |
|------|---------|---------|
| **Sergeant** | Blunt, imperative, nicknames | "Move it, soldier. Lord's waiting." |
| **Officer** | Formal, measured, uses titles | "You fought well today, soldier." |
| **Lord** | Distant, brief, expects obedience | "See it done." |
| **Veteran** | Laconic, world-weary, advice | "First one's the hardest. Gets easier." |
| **Recruit** | Nervous, questions, eager | "Is it always like this?" |
| **Quartermaster** | Transactional, gruff, precise | "That'll be thirty denars." |

### Dialogue Formatting

- Use double quotes for speech
- Keep dialogue short (under 20 words)
- No dialect spelling ("Ya gotta..." ❌)
- Actions in narration, not speech tags

```
✅ "'Just doing my duty, sir.' The captain nods."
✅ "'They surrendered. That means prisoners, not targets.' Your voice carries weight."

❌ "'Well,' said the captain thoughtfully, 'I suppose that you did fight quite well today.'"
❌ "'Oi there mate, ye did a right proper job o' fightin' today, ye did!'"
```

---

## Common Mistakes

### 1. Modern Psychology

```
❌ "You process your trauma and decide to seek closure through helping others."
✅ "The shaking stops. You find work to do."
```

### 2. Explaining the Stakes

```
❌ "This is a moral dilemma about whether to steal."
✅ "Officers aren't looking this way. No one would know."
```

### 3. Being Too Flowery

```
❌ "The crimson lifeblood of your vanquished foe spreads across the once-verdant battlefield like a tapestry of mortality."
✅ "Blood pools in the mud. His eyes stop moving."
```

### 4. Anachronistic Language

```
❌ "You feel stressed about your upcoming performance review with the sergeant."
✅ "The sergeant's been watching you. That's never good."
```

### 5. Breaking Immersion

```
❌ "Choose this option to increase your soldier reputation stat."
✅ "The soldiers nod with approval."
```

### 6. Too Much Hedging

```
❌ "Perhaps you might consider possibly helping if you want to."
✅ "Help with the burial."
```

### 7. Telling Emotions

```
❌ "You feel incredibly proud and happy about your accomplishment."
✅ "Something settles in your chest. You stand a little straighter."
```

---

## Examples

### Complete Event Example

```json
{
  "id": "mi_wounded_enemy",
  "title": "The Wounded Enemy",
  "setup": "A wounded enemy soldier lies in the mud, blood pooling beneath him. He's watching you, eyes wide with fear and pain. He's not dead yet. No one else is around.",
  "options": [
    {
      "id": "kill",
      "text": "End his suffering",
      "resultText": "You draw your blade and finish it quickly. Mercy or cruelty—you're not sure which. The field is quieter now.",
      "tooltip": "Safe choice. Valor and Mercy trait XP."
    },
    {
      "id": "spare",
      "text": "Leave him to his fate",
      "resultText": "You walk away. His labored breathing fades behind you. He might live, he might not. That's not your decision to make.",
      "tooltip": "Safe choice. Mercy trait XP."
    },
    {
      "id": "help",
      "text": "Call for a medic",
      "resultText": "You signal for the surgeon's assistant. He looks at you oddly but comes over. 'Strange to waste bandages on them,' he says, but he does it anyway.",
      "tooltip": "Requires Medicine 20. +2 Officer rep, -3 Soldier rep. Mercy trait XP."
    }
  ]
}
```

**Why this works:**
- Setup: Concrete image, stakes implied, no question asked
- Options: Short, verb-first, no explanations
- Results: Action → consequence → lingering state
- Tooltips: Factual, brief, mechanical

### Before/After Comparison

**Before (bad):**
```
Setup: "You find yourself in a challenging situation where you must make a difficult moral choice. There is a wounded enemy soldier nearby who needs help, and you are feeling conflicted about whether to assist them given that they were trying to kill you moments ago."

Option: "Perhaps consider helping the enemy soldier who is wounded"

Result: "You made the compassionate choice to help the enemy! You should feel proud of yourself for showing mercy. This kind action will definitely be remembered positively."
```

**After (good):**
```
Setup: "A wounded enemy lies in the mud, blood pooling beneath him. Eyes wide with fear and pain. He's not dead yet. No one else is around."

Option: "Call for a medic"

Result: "You signal for the surgeon's assistant. He looks at you oddly but comes over. 'Strange to waste bandages on them,' he says, but he does it anyway."
```

---

## Technical Reference

This section provides technical context for anyone generating or reviewing content.

### Content Pipeline Overview

```
JSON Definition → XML Localization → Runtime Resolution → Player Display
     ↓                  ↓                    ↓                  ↓
  events/*.json   enlisted_strings.xml  TextVariableResolver   InquiryPopup
  decisions/*.json                      TokenResolver          GameMenu
```

### JSON Structure Requirements

Every event/decision requires specific fields. **Fallback text must immediately follow its ID field.**

```json
{
  "id": "evt_example",              // Unique ID (evt_*, dec_*, mi_*, player_*)
  "category": "map_incident",       // Category determines delivery mechanism
  "titleId": "evt_example_title",   // XML localization key for title
  "title": "Example Title",         // Fallback if XML missing
  "setupId": "evt_example_setup",   // XML localization key for setup
  "setup": "Setup text here...",    // Fallback if XML missing
  "requirements": { ... },          // When event can fire
  "timing": { ... },                // Cooldowns, priority
  "options": [ ... ]                // Player choices
}
```

**ID Prefix → Delivery Mechanism:**

| Prefix | Delivery | Where Displayed |
|--------|----------|-----------------|
| `dec_*` | Player-initiated | Camp Hub menu (inline) |
| `player_*` | Player-initiated | Popup inquiry |
| `decision_*` | Game-triggered | Popup inquiry |
| `evt_*` | Automatic | Popup inquiry |
| `mi_*` | Map incident | Popup inquiry |

### Localization ID Patterns

XML string IDs follow consistent naming:

| Field | ID Pattern | Example |
|-------|------------|---------|
| Title | `{event_id}_title` | `mi_loot_title` |
| Setup | `{event_id}_setup` | `mi_loot_setup` |
| Option text | `{event_id}_{option_id}` | `mi_loot_take` |
| Result text | `{event_id}_{option_id}_result` | `mi_loot_take_result` |
| Fail result | `{event_id}_{option_id}_fail` | `mi_loot_take_fail` |

**XML Format:**
```xml
<string id="mi_loot_title" text="Dead Man's Purse" />
<string id="mi_loot_setup" text="The battle is over. Bodies scatter the field..." />
<string id="mi_loot_take" text="Take the gold" />
<string id="mi_loot_take_result" text="You pocket the coins quickly..." />
```

**XML Escaping Rules:**
- Newlines: `&#xA;`
- Ampersand: `&amp;`
- Apostrophe: `&apos;`
- Quote: `&quot;`

### Option Structure

Each option requires specific fields:

```json
{
  "id": "take",                           // Unique within event
  "textId": "mi_loot_take",               // XML key
  "text": "Take the gold",                // Fallback
  "resultTextId": "mi_loot_take_result",  // XML key for result
  "resultText": "You pocket the coins...", // Fallback
  "tooltip": "Adds 2 scrutiny. 25 gold.",  // REQUIRED, never null
  "risk": "moderate",                      // safe/moderate/risky/dangerous
  "effects": {                             // Game state changes
    "gold": 25,
    "scrutiny": 2
  }
}
```

**Skill Check Options:**
```json
{
  "id": "investigate",
  "skillCheck": {
    "skill": "Scouting",
    "difficulty": 35
  },
  "tooltipTemplate": "{CHANCE}% ({SKILL_NAME} {SKILL}). +15 XP. Fail: -5 HP.",
  "effects": { ... },
  "failEffects": { ... },
  "failResultTextId": "...",
  "failResultText": "..."
}
```

### Effect Types Reference

| Category | Fields | Example Values |
|----------|--------|----------------|
| **Resources** | `gold`, `fatigue`, `hpChange` | 50, -2, -10 |
| **Reputation** | `soldierRep`, `officerRep`, `lordRep` | 5, -3, 10 |
| **Escalation** | `scrutiny`, `discipline`, `medicalRisk` | 2, 1, 1 |
| **Skills** | `skillXp: { "Scouting": 15 }` | Any skill name |
| **Traits** | `traitXp: { "Valor": 5, "Mercy": -3 }` | Native trait names |
| **Company** | `companyNeeds: { "Morale": 3 }` | Morale, Readiness, etc. |
| **Combat** | `troopLoss`, `woundTroops` | 2, 3 |

### Validation Requirements

Before content is accepted, it must pass validation:

```powershell
python Tools/Validation/validate_content.py
```

**Validation checks:**
- [ ] All `titleId`/`setupId` have matching XML strings
- [ ] All options have non-null `tooltip`
- [ ] Fallback text immediately follows ID fields
- [ ] Order events include `skillXp` in effects
- [ ] Risk options have `effects_failure` or `failEffects`
- [ ] No duplicate event IDs across files

### Context Requirements

Events fire based on `requirements.context`:

| Context | When Active |
|---------|-------------|
| `"camp"` | Resting in camp |
| `"march"` | Party moving |
| `"leaving_battle"` | After battle ends |
| `"during_siege"` | While besieging |
| `"entering_town"` | Opening town menu |
| `"entering_village"` | Opening village menu |
| `"leaving_settlement"` | Leaving any settlement |
| `"waiting_in_settlement"` | Hourly in garrison |

### World State Requirements

Order events use `world_state` for context filtering:

| World State | When Active |
|-------------|-------------|
| `peacetime_garrison` | At peace, in settlement |
| `peacetime_marching` | At peace, traveling |
| `war_marching` | At war, traveling |
| `war_active_campaign` | At war, active operations |
| `siege_attacking` | Attacking a siege |
| `siege_defending` | Defending a siege |

### Cross-Reference Documents

| Topic | Document | Key Sections |
|-------|----------|--------------|
| Storylet schema and runtime | [storylet-backbone.md](storylet-backbone.md) | Current content fields, triggers, effects, and save offsets |
| All placeholder tokens | This document (see Dynamic Tokens section above) | Complete variable list |
| Content entry point | [README.md](README.md) | Pipeline overview |
| Order event structure | [orders-content.md](orders-content.md) | Order-specific rules |
| Validation tools | [../../Tools/README.md](../../Tools/README.md) | How to validate |

### Content Generation Checklist

When generating new content, verify:

1. **Structure**
   - [ ] ID follows naming convention (`evt_*`, `dec_*`, `mi_*`)
   - [ ] Fallback text follows ID fields
   - [ ] All required fields present

2. **Writing Style**
   - [ ] Setup: present tense, 2-4 sentences, concrete
   - [ ] Options: under 8 words, verb-first
   - [ ] Results: past tense action, present state
   - [ ] No modern psychology, no praise/judgment

3. **Tokens**
   - [ ] NPC names use tokens (`{SERGEANT}` not "the sergeant")
   - [ ] Tokens are uppercase with braces
   - [ ] Fallbacks exist for all tokens used

4. **Mechanics**
   - [ ] Every option has non-null tooltip
   - [ ] Tooltips under 80 characters
   - [ ] Effects use correct field names
   - [ ] Skill checks have fail paths
   - [ ] Order events with land-specific content use `notAtSea: true`

5. **Hints** (for opportunities)
   - [ ] Under 10 words
   - [ ] Uses placeholders (`{SOLDIER_NAME}`, not "a soldier")
   - [ ] Camp gossip tone, not UI description
   - [ ] Personal hints start with "Your"

6. **Localization**
   - [ ] All text IDs follow pattern `{event_id}_{field}`
   - [ ] XML escaping applied where needed
   - [ ] Sync tool can generate entries

---

## Quick Reference

### Setup Checklist
- [ ] Present tense
- [ ] 2-4 sentences
- [ ] Concrete opening
- [ ] Stakes implied (not stated)
- [ ] No direct questions to player

### Option Checklist
- [ ] Under 8 words
- [ ] Starts with verb (usually)
- [ ] No explanations
- [ ] Uses placeholders for NPC names

### Result Checklist
- [ ] Past tense for action
- [ ] 2-3 sentences
- [ ] Shows consequence
- [ ] Ambiguous moral framing
- [ ] No "good job!" praise

### Tooltip Checklist
- [ ] Under 80 characters
- [ ] One sentence
- [ ] Mechanical effects only
- [ ] Not null or empty

### Hint Checklist
- [ ] Under 10 words
- [ ] Uses placeholders for names (`{SOLDIER_NAME}`)
- [ ] Camp gossip tone, not UI text
- [ ] Phase-appropriate (dawn drill, evening cards)
- [ ] Personal hints start with "Your"

### Voice Checklist
- [ ] No modern psychology terms
- [ ] No anachronistic language
- [ ] Physical over abstract
- [ ] Short sentences
- [ ] No exclamation marks

---

**End of Document**
