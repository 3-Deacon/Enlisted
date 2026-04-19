# Core Gameplay

**Summary:** Comprehensive overview of the Enlisted mod's core gameplay loop covering enlistment, orders from chain of command, emergent identity through traits and reputation, and the native game menu interface. This is the main entry point for understanding how all systems fit together in the enlisted soldier experience.

**Status:** ✅ Current  
**Last Updated:** 2025-12-23  
**Related Docs:** [Enlistment](enlistment.md), [Order Progression System](order-progression-system.md), [Identity System](../Identity/identity-system.md), [Camp Simulation System](../Campaign/camp-simulation-system.md)

---

## Index

- [The Three Pillars](#the-three-pillars)
- [System Map (Code + Data)](#system-map-code--data)
- [Enlistment](#enlistment)
- [Orders System (Chain of Command)](#orders-system-chain-of-command)
- [Emergent Identity (Traits + Reputation)](#emergent-identity-traits--reputation)
- [Native Game Menu (Interface)](#native-game-menu-interface)
- [Company Needs](#company-needs)
- [Pay System (Muster Ledger)](#pay-system-muster-ledger)
- [Companions + Retinue](#companions--retinue)
- [Fatigue + Conditions](#fatigue--conditions)
- [Town Access + Temporary Leave](#town-access--temporary-leave)
- [Content Authoring (Quick Rules)](#content-authoring-quick-rules)

---

## The Three Pillars

The mod is built on three core transformations introduced in v2.0:

1.  **Emergent Identity**: Identity emerges from choices, traits, and reputation rather than prescribed systems. Your specialization (Scout, Medic, Officer) is determined by your actions and native Bannerlord traits.
2.  **Orders from Chain of Command**: Passive duties are replaced by explicit directives from your superiors. You receive orders from Sergeants, Lieutenants, or your Lord, with meaningful stakes for success or failure.
3.  **Native Game Menu Interface**: All custom UI (ViewModels/XML) has been replaced by the native Bannerlord Game Menu system. It is fast, keyboard-friendly, and integrates seamlessly with the base game.

---

## System Map (Code + Data)

### Core Code Areas

- **Enlistment + State**: `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`
- **Orders System**: `src/Features/Orders/Behaviors/OrderManager.cs`
- **Identity & Traits**: `src/Features/Identity/EnlistedStatusManager.cs`
- **Reputation & Escalation**: `src/Features/Escalation/EscalationManager.cs`
- **Native Menus**: `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`
- **Company Needs**: `src/Features/Company/CompanyNeedsManager.cs`
- **Strategic Context**: `src/Features/Context/ArmyContextAnalyzer.cs`
- **News & Reports**: `src/Features/Interface/Behaviors/EnlistedNewsBehavior.cs`
- **Quartermaster**: `src/Features/Equipment/Behaviors/QuartermasterManager.cs`
- **Medical Care**: Integrated into decision system (see `ModuleData/Enlisted/Decisions/medical_decisions.json`)

### Core Data Sources (ModuleData)

- **Config**: `ModuleData/Enlisted/enlisted_config.json`
- **Progression**: `ModuleData/Enlisted/progression_config.json`
- **Orders Catalog**: `ModuleData/Enlisted/Orders/*.json`
- **Events Catalog**: `ModuleData/Enlisted/Events/*.json` (Role-based & Contextual)
- **Rank Titles**: `ModuleData/Enlisted/progression_config.json` (Culture-specific)

---

## Enlistment

Enlisted turns Bannerlord into a “soldier career” loop:

1.  **Enlist** with a lord. Your party is hidden, and you follow the lord's army.
2.  **Live the Routine**: Receive **Orders**, manage **Company Needs**, and navigate **Events**.
3.  **Fight as a Unit**: Participate in the lord's battles. At higher tiers, you lead a **Retinue**.
4.  **Get Paid**: Wages accrue in the **Muster Ledger** and are paid during Pay Muster.
5.  **Progress**: Advance from T1 to T9. Rank gates authority and the scale of orders you receive.
6.  **Leave**: Discharge honorably, transfer, or desert.

---

## Orders System (Chain of Command)

Replaces the legacy passive duties system with explicit, mission-driven tasks.

-   **Frequency**: Orders are issued every 3-5 days by default (config-driven via `enlisted_config.json` → `decision_events.pacing.event_window_min_days` and `event_window_max_days`).
-   **Chain of Command**: Your rank determines who issues the order (e.g., T1-T2 get orders from a Sergeant; T7+ receive strategic orders from the Lord).
-   **Mandatory vs Optional**: 
    - **T1-T3 Basic Duties**: Automatically assigned (no player choice). Guard duty, camp patrol, firewood detail, equipment checks, muster, sentry. Shown as `[ASSIGNED]` (greyed out, not clickable) in Orders menu.
    - **T4+ Advancement**: Optional orders. Player clicks order row in Orders menu → popup shows order details with [Accept Order] / [Decline Order] buttons. Lead patrol, scout route, train recruits, escort duty. Shown as `[NEW]` (clickable) in Orders menu.
-   **Requirements**: Orders are filtered by your current Rank, Skills, and Traits.
-   **Progression**: Orders auto-progress through phases (4/day: Dawn, Midday, Dusk, Night). Events may fire during execution based on world state.
-   **Outcomes**: Success or failure impacts **Reputation**, **Company Needs**, and grants **Skill/Trait XP**.
-   **Discharge Risk**: Repeatedly declining optional orders (5+ times) triggers a risk of dishonorable discharge.
-   **Camp Life Continues**: Being on duty doesn't block gameplay. Camp decisions remain available, with some flagged as risky (detection chance) or blocked while on duty.

**Note:** Orders follow the same pacing system as narrative events. See [Event System Schemas](../Content/event-system-schemas.md#global-event-pacing-enlisted_configjson) for full pacing config details.

**See:** [Order Progression System](order-progression-system.md) for complete technical details.

---

## Emergent Identity (Traits + Reputation)

### Traits & Roles

Your role in the company emerges from your native Bannerlord traits, which develop through event choices and gameplay. The system checks traits in priority order:

**Role Determination** (`EnlistedStatusManager.GetPrimaryRole()`):
1. **Officer** - Commander trait 10+ (leadership, tactical command)
2. **Scout** - ScoutSkills trait 10+ (reconnaissance, intelligence gathering)
3. **Medic** - Surgery trait 10+ (medical treatment, triage)
4. **Engineer** - Siegecraft trait 10+ (fortifications, siege operations)
5. **Operative** - RogueSkills trait 10+ (covert operations, black market)
6. **NCO** - SergeantCommandSkills trait 8+ (squad leadership, training)
7. **Soldier** - Default role (general combat duties)

**Role-Specific Content:**
- Orders from the chain of command are filtered by your skills and traits
- Your choices in orders and events grant trait XP, gradually developing your specialization
- Multiple specializations can develop simultaneously (e.g., Scout + NCO)

### Reputation
Reputation with your lord is tracked using native Bannerlord's relation system (`Hero.GetRelation()`). This integrates with vanilla mechanics and affects all interactions with your commanding officer. Positive relations improve opportunities, while negative relations can block progression.

---

## Native Game Menu (Interface)

All interactions occur through the **Enlisted Status** menu hub:
-   **Enlisted Status**: View rank, active orders, and immersive narrative reports (Kingdom Reports, Company Reports, Player Status).
-   **Camp Hub**: Access Service Records, Quartermaster, and Personal Retinue management.
-   **Decisions Accordion**: Handle pending event choices, camp opportunities, and medical care (appears when content is available).

### Daily Brief & Company Reports
The main **Enlisted Status** menu displays three narrative sections:

1. **Kingdom Reports** - Macro-level: realm politics, wars, recent battles
2. **Company Reports** - Local: camp atmosphere, troop status, supplies, baggage train, lord's situation
3. **Player Status** - Personal: duty status, health, fatigue, notable conditions

The **Camp Hub** provides a detailed "COMPANY STATUS" summary with troop composition, needs analysis, recent activity, and upcoming events.

All reports use immersive narrative style with world-state-aware generation (activity levels, lord situation, campaign context).

**For complete details**, see: **[News & Reporting System](../UI/news-reporting-system.md)**

---

## Company Needs

The company's effectiveness is tracked via two core needs:
-   **Readiness**: Combat effectiveness and preparation.
-   **Supply**: Food and basic consumables.

**Note:** Company Rest was removed (2026-01-11) as redundant with the Player Fatigue system (0-24 budget). Player fatigue still gates camp decisions and has health penalties - only the company-wide Rest metric was removed.

### Status Reporting
Company Needs are displayed in the **Reports → Company Status** menu with immersive, context-aware descriptions that explain what's affecting each stat:
- **Descriptive states** instead of raw percentages (e.g., "The company is battle-ready, formations tight and weapons sharp" vs "85%")
- **Contextual factors** explaining why stats are changing (long marches draining rest, supply shortages affecting readiness)
- **5 severity levels** per need: Excellent → Good → Fair → Poor → Critical

### Needs Prediction
The mod forecasts upcoming needs based on the current **Strategic Context**. For example, a "Grand Campaign" (coordinated offensive) predicts high Readiness and Supply requirements, while a "Winter Camp" prioritizes Rest. This allows players to prepare for upcoming operations.

---

## Strategic Context & War Stance

The mod leverages Bannerlord's strategic AI data to ensure the soldier's experience reflects the lord's actual plans.

### War Stance
The mod calculates a faction's overall strength (territory, military, economy) to determine its **War Stance**:
-   **Desperate**: Losing badly; priorities shift to survival.
-   **Defensive**: Unfavorable position; focused on holding territory.
-   **Balanced**: Even footing; standard operations.
-   **Offensive**: Winning; focused on expansion and aggression.

### Strategic Contexts
The unit operates within one of 8 distinct contexts that filter which orders and events are available:
1.  **Grand Campaign** (coordinated offensive)
2.  **Last Stand** (desperate defense)
3.  **Harrying the Lands** (raid operation)
4.  **Siege Works** (active siege)
5.  **Riding the Marches** (peacetime patrol)
6.  **Garrison Duty** (settlement defense)
7.  **Mustering for War** (recruitment drive)
8.  **Winter Camp** (seasonal rest and training)

These contexts ensure you won't receive leisure orders during a desperate defense or siege operations while the realm is at peace.

---

## Pay System (Muster Ledger)

Wages accrue into a **Muster Ledger** based on your rank and performance. They are paid out periodically at **Pay Muster** events.
-   **Pay Tension**: Rises when pay is late or disrupted, leading to potential corruption or desertion.

---

## Companions + Retinue

### Companions (All Tiers)
Stay with you during enlistment and fight alongside you in your formation. Available from T1. Companions are unaffected by most enlisted mechanics and serve as your permanent squad.

### Retinue (T7+ Commander Track)
At Commander rank (T7+), you gain command over your own soldiers, transforming from individual combatant to leader of men.

**Key Features:**
- **Formation Selection**: Choose infantry, archers, cavalry, or horse archers at T7
- **Scaling Force**: T7 (20 soldiers), T8 (30), T9 (40)
- **Context-Aware Reinforcements**: Faster after victories, slower after defeats
- **Loyalty System**: Track your soldiers' morale (0-100) through choices
- **Named Veterans**: Individual soldiers emerge with names and battle histories
- **Command Content**: 11 narrative events, 6 post-battle incidents, 4 camp decisions
- **Relation-Based Requests**: Request reinforcements from your lord with pricing based on relationship

**Reinforcement System:**
- **Automatic Trickle**: Soldiers arrive based on context (1 per 2-5 days)
  - Victory (3 days): 1 per 2 days
  - Defeat (5 days): Blocked
  - Friendly Territory: 1 per 3 days
  - Peace: 1 per 2 days
  - Campaign: 1 per 4 days
- **Manual Requests**: Ask your lord for reinforcements
  - High relation (50+): 25% discount, 7-day cooldown
  - Neutral (20-49): Full cost, 14-day cooldown
  - Low (<20): Blocked

**See:** [Retinue System](retinue-system.md) for complete mechanics.

---

## Fatigue + Conditions

-   **Fatigue**: A budget consumed by orders and camp activities. Restores while resting in camp.
-   **Conditions**: Tracks injuries, illnesses, and exhaustion. These are managed through medical decisions (dec_medical_surgeon, dec_medical_rest, dec_medical_herbal, dec_medical_emergency) that appear in the Decisions menu when needed.

---

## Town Access + Temporary Leave

-   **Town Access**: Safely explore settlements while your party is hidden.
-   **Temporary Leave**: Suspend service for a limited time (e.g., to handle clan business). Expiration without return is treated as desertion.

---

## Content Authoring (Quick Rules)

-   **Events**: Tag with `role` and `context` (e.g., `scout`, `war`, `camp`).
-   **Orders**: Defined in JSON with specific requirements and tiered consequences.
-   **Strings**: All text must be localized in `enlisted_strings.xml`.
