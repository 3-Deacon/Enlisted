# enlisted (military service mod)

serve as a soldier in any lord's warband. follow orders, earn wages, climb ranks, and leave service when you're done.

## index

- [requirements](#requirements)
- [install](#install)
- [update](#update)
- [quick start (first time)](#quick-start-first-time)
- [features overview](#features-overview)
  - [core military career](#core-military-career)
  - [equipment & logistics](#equipment--logistics)
  - [combat & training](#combat--training)
  - [camp life & activities](#camp-life--activities)
  - [story & narrative](#story--narrative)
  - [progression & identity](#progression--identity)
  - [ui & interface](#ui--interface)
  - [optional systems](#optional-systems)
- [what to expect (important rules)](#what-to-expect-important-rules)
- [settings / config](#settings--config)
- [translations](#translations)
- [logs + troubleshooting](#logs--troubleshooting)
- [more docs](#more-docs)

## requirements

- mount & blade ii: bannerlord (supported versions depend on the release you installed)
- `bannerlord.harmony` (required)

## install

1. download the mod release (zip).
2. extract the `Enlisted` folder into:
   - `<bannerlord>\Modules\`
3. in the launcher, enable:
   - `bannerlord.harmony`
   - `enlisted`
4. recommended load order:
   - harmony -> native -> sandboxcore -> sandbox -> storymode -> custombattle -> enlisted

## update

- overwrite your existing `Modules\Enlisted\` folder with the new one.
- if you use other mods that patch menus/encounters/party state, re-check load order after updating.

## quick start (first time)

1. talk to a lord -> choose the enlistment dialog option.
2. after you enlist, use the **enlisted status** menu as your hub:
   - quartermaster, camp, duty selection, leave, etc.
   - to leave service properly: **camp -> request discharge** (resolves at next pay muster: **final muster**)
3. at the t1->t2 proving event, choose your formation (infantry/archer/cavalry/horse archer when available).

## features overview

enlisted transforms bannerlord into a deep soldier career simulator with **245 narrative content pieces** across 9 military ranks spanning your entire career from fresh recruit to senior commander.

### core military career

#### enlistment & service

- enlist with any lord and live inside their campaign loop (movement, battles, camp)
- safe encounter handling for battles, captivity, and sieges prevents stuck states
- temporary leave system (visit family, conduct trade, rest and recover)
- discharge process with final pay settlement, equipment reclamation, and retirement options
- 90-day re-enlistment cooldown after discharge

#### rank progression (t1-t9)

- 9 tiers spanning recruit to senior commander with culture-specific rank titles
- multi-factor promotion requirements: xp, service time, battles fought, reputation, discipline
- narrative "proving events" at key ranks (including formation choice at t1→t2)
- equipment tier unlocks and officer privileges at higher ranks
- promotion recap display during muster ceremonies

#### pay system (muster ledger)

- 12-day muster cycle with ceremonial pay day events
- wages accrue daily in a ledger, paid out at muster
- rank-based pay scales with performance modifiers
- pay tension system: when pay is late, tension rises and unlocks special choices
- deductions for fines, missing gear, or disciplinary actions
- iou/promissory options when treasury is disrupted

#### muster ceremonies (pay day events)

- 8-stage immersive ceremony replacing simple pay popups
- stages: intro → pay line → baggage check → inspection → recruit → promotion recap → retinue → complete
- 12-day period summary with combat/training/orders/xp breakdown
- integrated baggage checks, equipment inspections, recruitment
- direct quartermaster access and ration distribution
- configurable time pause behavior

#### orders system (chain of command)

- 16 military orders from sergeants, lieutenants, and your lord
- basic duties (t1-t3): auto-assigned mandatory tasks (guard duty, camp patrol, firewood detail, equipment checks, muster, sentry)
- advancement orders (t4+): optional missions you can accept or decline (lead patrol, scout route, train recruits, escort duty)
- multi-day execution: orders progress through forecast → scheduled → pending → active → complete
- 85 order events that can fire during execution based on world state (15-35% chance per duty period)
- 4 phases per day: dawn, midday, dusk, night
- success/failure impacts reputation, company needs, and grants skill/trait xp
- repeatedly declining optional orders (5+) risks dishonorable discharge

#### commander's retinue (t7+ officers)

- personal military force at officer ranks (t7-t9)
- player-chosen formation determines retinue troop types
- context-aware trickle reinforcements (1-3 troops per battle)
- relation-based reinforcement system
- loyalty tracking with consequences for poor treatment
- 11 narrative events for character development
- 6 post-battle incidents (heroism, casualties, desertion)
- 4 camp decisions (discipline, rewards, training)
- named veterans as persistent characters

#### companions

- companions stay with you during service
- toggle who fights vs who stays back (safer option)
- special companion interactions and events
- companion role assignments

### equipment & logistics

#### quartermaster system (10+ subsystems)

- equipment purchasing: browse by category, reputation-based discounts (0-30%)
- 6 quality tiers affecting stats and prices: shoddy, standard, serviceable, fine, masterwork, legendary
- upgrade system: sequential quality improvements with real stat bonuses using native itemmodifier
- gauntlet grid ui for equipment browsing and upgrades
- buyback service: sell qm-issued gear back at 30-65% value
- tier gates: rank-based access control to better equipment
- supply integration: equipment blocked when company supply <30%
- officers armory (t7+): elite gear catalog for commanders
- first-meeting introduction with your quartermaster
- contextual dialogue: 150+ dynamic responses based on supply, reputation, and context

#### provisions & rations

- t1-t6: issued rations every 12 days (reclaimed at muster)
- ration quality scales with soldier reputation
- t7+ officers: premium provisions shop with gauntlet grid ui
- premium pricing (2.0-3.2x town markets)
- stock levels affected by company supply
- provision bundles with morale/fatigue boosts
- rank-based button gating in shop interface

#### baggage train & logistics

- world-state-aware simulation: baggage delays/raids occur more during intense combat, rarely during peaceful garrison
- dynamic decision system: "access baggage train" appears in decisions only when wagons are accessible
- accessibility responds to campaign situation (march state, battles, sieges, settlements)
- 5 baggage events: wagons arrive, delays (weather/terrain), raids, theft
- rank-based access: higher ranks can request emergency access or halt column
- baggage checks during muster ceremonies
- early enlistment inventory handling (prevents "walk in with endgame kit")
- reclamation of personal gear upon discharge

#### company supply tracking

- 0-100% supply scale tracking rations, ammo, repairs, camp supplies
- supply effects gate quartermaster access and ration availability
- supply-based messaging affects qm greeting tone
- stock levels reflect current supply status

### combat & training

#### training & skill progression

- formation training: daily skill xp baseline for your chosen formation (infantry/archer/cavalry/horse archer)
- camp training actions: weapon drills, fitness training, sparring
- skill progression with xp rates and skill caps by rank
- training events with success/injury/fatigue outcomes
- cooldown system prevents training spam

#### battle participation

- formation assignment (t1-t6): auto-assigned based on equipped weapons
  - bow → ranged formation
  - horse → cavalry formation  
  - bow + horse → horse archer formation
  - melee → infantry formation
  - teleported to formation position at battle start
- t7+ commanders control their own party, no auto-assignment
- kill tracking and post-battle reports
- encounter safety systems prevent stuck states

#### battle ai (optional submodule)

- can be disabled via bannerlord launcher checkbox (no performance cost when disabled)
- enlisted-only activation: only runs when player is enlisted
- field battles only (siege and naval use native ai)
- ai improvements through better decision-making, not stat cheating
- performance-conscious design with appropriate update intervals

### camp life & activities

#### camp simulation (two-layer system)

- background simulation: autonomous company life runs automatically
  - soldiers get sick, injured, or desert
  - equipment breaks, gets stolen, or wears out
  - morale shifts based on conditions
  - small incidents occur constantly
- camp opportunities: 29 contextual player activities
  - training, social, recovery, economic, information gathering
  - fitness scoring based on context and player needs
  - learning system adapts to your preferences and history

#### camp activities & decisions

- 33 player-initiated camp hub decisions organized by category:
  - training (8): weapon drills, sparring, formation practice, archery, veteran lessons
  - social (11): join the men, write letters, socialize, war stories, card games, dice
  - economic (6): gambling, side work, loans, foraging, repairs, merchant caravans
  - recovery (8): rest options, sleep, meditation, prayer, help wounded
  - medical care (4): surgeon treatment, herbal remedies, rest recovery, emergency care
  - career (5): request audience, volunteer for duty, mentor recruits, extra duty, night patrol
  - information (3): listen to rumors, scout area
  - equipment (3): maintain gear, visit quartermaster
  - risk-taking (4): dangerous wagers, prove courage, high-stakes gambling
  - special (12): personal time, officer audiences, baggage access, retinue management
- fatigue system: actions accumulate fatigue, rest recovers it
- fatigue effects on performance and event outcomes
- dynamic skill checks: success chances modified by relevant skills (Medicine, Scouting, Athletics, etc.)
- illness restrictions: severe conditions block strenuous activities until treated

#### camp routine schedule

- baseline daily routine: dawn formations, midday work, dusk social, night rest
- world state deviations (march, battle, siege, rest, etc.)
- schedule forecast ui shows what's happening
- routine flavor text for immersion

**company needs (4 metrics)**

- readiness: combat effectiveness and preparation
- morale: the unit's will to fight
- supplies: food, ammunition, maintenance materials, and logistics
- rest: recovery from fatigue
- all metrics (0-100%) affect available options and event outcomes
- transparent system with clear cause and effect

**medical care & injuries**

- percentage-based hp loss (15-55%) for narrative injuries
- 15 injury types with contextual descriptions
- severity levels: minor, moderate, significant, serious, critical
- 4 medical care decisions accessible through the decisions menu:
  - surgeon treatment (skill check, 100 gold): professional care with better success rates
  - herbal remedies (skill check, 30 gold): traditional medicine with moderate effectiveness
  - rest recovery (free): natural healing through camp rest
  - emergency care (200 gold): immediate treatment for severe/critical conditions
- illness severity blocks strenuous activities (training, labor) until partially recovered
- hp reduction based on illness severity with protective 30% hp floor
- daily condition status messages in combat log
- rp-appropriate medieval military medical flavor

### story & narrative

#### 245 narrative content pieces

- 16 military orders across all tiers
- 85 order events that fire during order execution
- 33 player-initiated camp decisions (Phase 6G: deleted 35 old, kept 3, added 30 new)
- 68 context-triggered events
- 51 map incidents (battle, siege, settlement-triggered)
- 23 retinue-specific content pieces (t7+ commanders)

#### world-state-driven content

- content orchestrator analyzes world state and generates contextual opportunities
- activity level system responds to march state, battles, supplies, morale
- simulation pressure tracking influences event frequency
- player behavior tracking adapts content to your choices
- baggage train simulation responds to campaign conditions (intense siege vs peaceful garrison)
- event probabilities adapt dynamically to lord situation, war stance, and terrain

#### event types

- escalation events: reputation/discipline consequences (14 events)
- crisis events: major turning points (5 events)
- role-based events: tied to your emerging specialization
- universal events: available to all soldiers
- proving events: rank-up challenges at key promotions

#### emergent storytelling

- your choices shape your reputation and identity
- multiple branching paths and consequences
- faction relationship effects
- long-term career narrative arcs

### progression & identity

#### emergent identity (traits + reputation)

- role determination from native bannerlord traits, developed through choices:
  - officer (commander 10+): leadership, tactical command
  - scout (scoutskills 10+): reconnaissance, intelligence gathering
  - medic (surgery 10+): medical treatment, triage
  - engineer (siegecraft 10+): fortifications, siege operations
  - operative (rogueskills 10+): covert operations, black market
  - nco (sergeantcommandskills 8+): squad leadership, training
  - soldier (default): general combat duties
- role-specific content filtering for orders and events
- trait xp from choices gradually develops specializations
- multiple roles can develop simultaneously

#### reputation (three distinct tracks)

- lord reputation (-50 to +100): standing with your commanding lord
- officer reputation (-50 to +100): respect from ncos and officers
- soldier reputation (-50 to +100): popularity among rank-and-file troops
- each reputation affects different event outcomes and available options
- reputation-based quartermaster discounts (0-30%)
- promotion eligibility gated by reputation thresholds

#### discipline & escalation

- discipline score affects officer trust and consequences
- escalation tracks for serious infractions
- pressure-valve events at reputation thresholds
- scrutiny system for officer attention

### ui & interface

#### native game menu integration

- all interactions through native bannerlord game menus
- fast, keyboard-friendly, seamlessly integrated
- enlisted status hub: rank, orders, reports, decisions
- camp submenu: activities and rest
- reports submenu: daily brief, service record, company status
- decisions submenu: pending choices and opportunities

#### custom combat log

- native-styled scrollable feed (right side of screen)
- 5-minute persistence with 50-message history
- smart auto-scroll (pauses on manual scroll)
- inactivity fade (35% opacity after 10 seconds)
- clickable encyclopedia links with faction-specific colors
- suppresses native log while enlisted
- color-coded messages with shadows for readability

#### news & reporting

- daily brief: immersive narrative summary of company status
  - company situation, casualties, supply status
  - recent events, player condition, kingdom news
  - flowing paragraph format for immersion
- company status report: detailed breakdown of 5 company needs
- period recaps during muster ceremonies (12-day summaries)
- combat summaries and battle reports

#### gauntlet interfaces

- equipment browsing grid with category filtering
- upgrade system interface with stat comparison
- provisions shop (t7+) with rank-based gating
- professional color scheme across all ui elements

#### optional systems

#### battle ai submodule

- completely optional: disable via launcher checkbox
- zero performance cost when disabled
- enhanced ai for field battles when enlisted
- modular architecture allows easy toggle

#### dlc integration

- warsails/naval dlc feature detection
- naval-only features gracefully gated if dlc not present
- no errors or warnings for missing dlc

#### configuration & modding

- extensive json configuration for pacing, balance, features
- 11 config files in `ModuleData/Enlisted/Config/`
- event content easily moddable via json
- full localization support with template system
- 1,822 localizable strings

### what to expect (important rules)

### you serve; you don't command

- you manage your own small scope: your duty, your reputation, camp activities
- you don't control the lord's strategic decisions or army movements
- at t7+ you lead a retinue, but still follow the lord's campaign

### orders & duties

- t1-t3: basic duties are auto-assigned (mandatory, no choice)
- t4+: advancement orders are optional (can accept or decline)
- repeatedly declining optional orders (5+) risks dishonorable discharge
- orders progress over multiple days with event chances during execution
- success/failure affects reputation, company needs, and xp

### loot & rewards

- low tiers (t1-t6): limited loot access, compensation via pay
- higher tiers (t7+): regain native loot access as officers
- wages accrue daily in muster ledger, paid every 12 days
- performance bonuses, reputation modifiers, and deductions apply

### pay & muster

- muster ceremonies occur every 12 days (8-stage immersive event)
- wages paid at muster along with ration distribution
- when pay is late, pay tension rises and unlocks special options
- can leave without penalty at high pay tension

### leave & discharge

- **temporary leave**: time-limited, must return or go awol
  - rank-based approval requirements
  - leave activities: visit family, trade, rest
  - awol consequences if you don't return
- **discharge**: permanent separation from service
  - request at camp hub → resolves at next muster (final muster stage)
  - final pay settlement and equipment reclamation
  - 90-day re-enlistment cooldown with same faction

### reputation matters

- three separate tracks: lord, officer, soldier
- affects event options, promotion eligibility, and outcomes
- help comrades → better options, they cover for you, promotion paths open
- betray or ignore them → isolation, blocked promotions, sabotage
- officer rep affects discipline consequences
- soldier rep affects quartermaster discounts (0-30%)

### your identity emerges from choices

- no "pick your class" menus
- your role develops through native bannerlord traits
- event choices grant trait xp toward specializations
- roles: officer, scout, medic, engineer, operative, nco, soldier
- role affects which orders and events you see
- multiple specializations can develop simultaneously

### company life is autonomous

- background simulation runs automatically
- soldiers get sick, equipment breaks, morale shifts
- you observe through news feed and company reports
- camp opportunities appear contextually based on situation
- things happen whether you engage or not

### fatigue & medical

- actions accumulate fatigue (marching, fighting, training)
- fatigue affects performance and event outcomes
- rest actions in camp recover fatigue
- injuries are percentage-based hp loss (15-55%)
- medical treatment through 4 decision options (surgeon, herbal, rest, emergency)
- skill checks affect treatment success (Medicine skill improves outcomes)
- illness severity restricts activities until partially recovered
- severe conditions require treatment before resuming strenuous duties

### settings / config

configs live in:

- `Modules\Enlisted\ModuleData\Enlisted\`

common files:

- `enlisted_config.json` (main toggles and pacing)
- `progression_config.json` (tiers/ranks)
- `Events\*.json` (event content packs)
- `Orders\*.json` (military orders by tier)

### translations

the mod is fully translatable and supports multiple languages. to add a translation:

- see **[Translation Guide](ModuleData/Languages/README.md)** for complete instructions
- copy the `_TEMPLATE` folder in `ModuleData/Languages/`
- translate 1,822 strings to your language
- submit via pull request or share with the community

current languages: **english** (default)

### logs + troubleshooting

### where logs are

**PRIMARY LOCATION (ENLISTED MOD LOGS):**

`<bannerlord>\Modules\Enlisted\Debugging\`

**Example full path:**

`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\`

**IMPORTANT:** The mod writes all logs directly to the `Debugging` subfolder inside the Enlisted module directory. This is NOT the game's crash logs and NOT your Documents folder.

### installation locations

**Where the mod is installed depends on how you got it:**

**Steam Workshop:**

- `C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\3621116083\`

**Manual/Nexus Install:**

- `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\`

**NOTE:** If you have both installed, you'll see duplicates in the Bannerlord launcher. Unsubscribe from the Workshop version if you're installing manually to avoid conflicts.

### automatic mod conflict detection

The mod automatically detects conflicts with other mods at startup. Check `Conflicts-A_{timestamp}.log` in the Debugging folder for:

- Harmony patch conflicts with other mods
- Module health check (missing/corrupt JSON/XML files)
- Runtime catalog status (content loading verification)
- Patch application status (total methods patched)
- Installation path information (manual vs Steam Workshop)

This comprehensive diagnostic log helps quickly identify whether issues are due to mod conflicts, corrupted installations, or genuine bugs.

- `Session-A_{timestamp}.log` (latest session)
- `Conflicts-A_{timestamp}.log` (latest conflicts report)
- `Current_Session_README.txt` (points to the current files and explains what to share)

**fallback location (only if the mod can't write to the module folder):**

- `%userprofile%\Documents\Mount and Blade II Bannerlord\Logs\Enlisted\`

**bannerlord crash dumps (separate - when the game itself hard-crashes):**

- `C:\ProgramData\Mount and Blade II Bannerlord\crashes\`

### error codes (important)

when something breaks, enlisted will often log a **stable error code** like:

- `[E-CAMPUI-a3f2] Failed to display Camp Area screen`

these codes are meant to be searchable and consistent across versions.
when reporting an issue, please include:

- the **error code(s)** you see (copy/paste the lines)
- `Session-A_*.log`

note: exceptions include **full stack traces**, but they’re **de-duplicated** (the first occurrence per unique exception in a session) to avoid log spam.

also note: some issues are intentionally logged **once per session** (look for codes beginning with `E-` in a single line), especially for:

- **dlc missing / feature gating** (previously emitted as `W-DLC-*` in pre-redesign logs; current builds emit DLC fallbacks as throttled INFO entries)
- **reflection / api drift** where a patch can’t apply (previously emitted as `W-REFLECT-*` in pre-redesign logs; current builds use throttled INFO entries)
- **ui fallback paths** (example: `E-QM-<4hex>`)
- **save/load wrappers + migrations** (example: `E-SAVELOAD-<4hex>`)

**Code format:** `E-<CATEGORY>-<4hex>`. The 4-hex suffix is a deterministic
SHA-256 hash of the error summary string, so the same error produces the
same code across runs and builds. Codes shown in the log and on-screen
toast match the entries in [docs/error-codes.md](docs/error-codes.md),
which is auto-generated from the source and may have been updated after
your installed build — when in doubt, match on the summary text, not the
exact suffix.

### common issues

- **mod menu options don’t show**
  - confirm `bannerlord.harmony` is enabled and loaded before enlisted.
- **war sails / naval features don’t work**
  - if you don’t have the War Sails (NavalDLC) expansion enabled, naval-only features will be gated.
  - check the session log for a `W-DLC-*` warning.
- **events don’t fire**
  - confirm the event systems are enabled in `enlisted_config.json`.
- **something feels “stuck” after leaving a menu/encounter**
  - send `Session-A_*.log` + your mod list/load order.
- **save won’t load / loads into broken state**
  - check for `E-SAVELOAD-*` in `Session-A_*.log`, then share the session log + conflicts log (and the save file if possible).

### more docs

full documentation entry point:

- `docs/index.md`
