# Storylet Backbone — Design Spec

**Date:** 2026-04-19
**Status:** Draft — awaiting designer review.
**Scope:** Establish the **content layer** beneath `StoryDirector`: storylets, qualities, flags, activities, scopes, chains, scripted effects. Ground the mod's narrative vocabulary in established industry patterns (Failbetter QBN, CK3 events + activities, Banner Kings code-first hybrid, Viking Conquest Freelancer context segregation, Disco Elysium skill voices, Wildermyth slot-filling). **No migration.** Every surface refactor that follows (Enlisted Home, Orders, Land/Sea, Promotion+Muster, Quartermaster) deletes its legacy JSON/C# and ships clean against this spec.

This is **Spec 0** in a 6-spec cycle:

| Spec | Title | Depends on |
| :--- | :--- | :--- |
| 0 | Storylet Backbone (this doc) | — |
| 1 | Enlisted Home Surface (Camp Activity + Menu, unified) | 0 |
| 2 | Orders as Activities | 0, 1 |
| 3 | Land/Sea Incident Pools | 0 |
| 4 | Promotion + Muster (Rank Cycle) | 0, 1 |
| 5 | Quartermaster Activity | 0, 1 |

Spec 0 defines *what things are called, what they look like, and how they connect.* It ships runtime plumbing (`QualityStore`, `FlagStore`, `ActivityRuntime`, `Storylet` base, `ScriptedEffectRegistry`) but **no content** — every storylet, every quality definition, every scripted-effect body lives in its surface spec.

---

## 1. Problem

The mod's narrative plumbing works but its vocabulary doesn't match the industry patterns it implements. Six invented terms collide with six canonical ones (see §3). The cost shows up in three places:

1. **Authoring drift.** A writer reading `EventDefinition.TriggersAll` + `Requirements.Role` + `EscalationState.ActiveFlags` + `CampOpportunity.Type` + `ScheduledPhase.Slot1Category` has to internalise four overlapping gating systems that all do the same thing under different names. Nothing tells them these are one concept (a *trigger*) with different call sites.
2. **Feature synchronization.** Camp, Orders, Land/Sea incidents, Muster, Promotion, Quartermaster each grew their own schedulers, their own state bags, their own effect shapes. They cannot share content because their data models diverge. The pacing spec (2026-04-18) unified *delivery*; this spec unifies *content*.
3. **Missing primitives.** Three industry patterns have no equivalent in the mod today: scoped events with slot-filling (Wildermyth), scripted-effect registries (CK3), and intent-biased activities with phase pools (CK3). The code gets by without them, but only because content authors paper over the gaps with duplicated JSON.

The mod is ~85% structurally aligned with the target patterns already. The remaining 15% is (a) renames to canonical terms, (b) unification of ad-hoc state into `QualityStore` + `FlagStore`, (c) the three missing primitives.

## 2. Goals

- One vocabulary. Every narrative surface uses the same words for the same concepts.
- One content schema. A single JSON shape (CK3-structure adapted to JSON) describes every storylet, regardless of surface.
- One quality home. `QualityStore` is the sole owner of Scrutiny, Lord Rep, Medical Risk, Fatigue, Discipline, Supply Days, Morale, Loyalty, Rank, Days Enlisted. No duplicate fields on legacy classes.
- One flag home. `FlagStore` owns global flags and hero-scoped flags with expiry.
- One activity runtime. `ActivityRuntime` drives stateful, phased, intent-biased, attendee-aware activities (Banner Kings `Feast` pattern generalised).
- One scoping model. Storylets declare `scope: { role: "veteran", target: "lord" }` and the `SlotFiller` resolves placeholders at fire time.
- One effects library. `ScriptedEffectRegistry` resolves named effects (`lord_rep_up_small`) at runtime. JSON-declared storylets call them by string ID.
- One chain model. `trigger_event { id, days }` is the only chain primitive. Extends the existing `ChainContinuation` pacing bypass.
- Zero migration. Surface specs delete the legacy files/classes they replace.

## 3. Non-goals

- No content authoring. No new storylets, no new qualities beyond those already implied, no new activities. Surface specs author content.
- No changes to `StoryDirector`'s pacing logic (§6.2, §7, §10 of the 2026-04-18 pacing spec). The director stays.
- No new Gauntlet UI. Modal = `InquiryData`, accordion = `enlisted_status` entries. Renderers are unchanged.
- No replacement for `IssueBase`. Issue-driven walk-up missions remain a separate system; see §13.
- No MCM UI for Quality inspection. Developers read values through the existing debug surface.

## 4. Core principles

1. **Code-first triggers, data-first text.** Trigger predicates and effect bodies are C# (registry-resolved by string ID). Titles, setups, option text, tooltips, and effect *parameters* are JSON. The hybrid avoids the JSON-trigger-DSL trap that every Bannerlord narrative mod has fallen into (per the external mod survey — Banner Kings, MnBRandomEvents, AdditionalQuests all tried and walked back).
2. **Storylets compile to candidates.** A `Storylet` is content. A `StoryCandidate` is an emission. The bridge is `Storylet.ToCandidate(context)`. The Director already owns candidates; this spec adds the thing upstream of them.
3. **Qualities gate and reward.** Every quality is both readable (gates a storylet) and writable (a storylet rewards it). Uniform type, uniform storage, uniform decay, uniform save.
4. **Flags are boolean memory.** A flag's only purpose is to say *this happened once* or *this is true for N days.* All identity tags, lord memory markers, arc progress markers, one-time-event tripwires, and chain links use the same primitive.
5. **Activities are stateful.** A CK3 activity is not a bundle of events; it is an object that ticks, accumulates state, selects from phase pools, and resolves with rewards. We generalise Banner Kings' `Feast` pattern: `Activity` is a saveable object with `Tick(hourly)`, `Phase`, `Attendees`, `Intent`, `PhaseQuality` composite scalars, and `Finish(reason)`.
6. **Scopes are first-class.** Storylets declare who the event is *about* (`scope:lord`, `scope:veteran`, `scope:chaplain`, `scope:land`, `scope:sea`). The `SlotFiller` resolves named slots at fire time. This is how Viking Conquest Freelancer's context segregation becomes a property of the content, not a fork in the code.
7. **Scripted effects are named and reusable.** `{ "apply": "lord_rep_up_small" }` is the normal form. Inline effect blocks are allowed for one-offs but the normal form pressures authors toward a shared vocabulary of outcomes.

## 5. Vocabulary and mapping

The table below is authoritative. When surface specs land, every reference uses the right-column term. Legacy class names are listed for clarity; they will be renamed or deleted per the surface spec that owns them.

| Canonical term | What it is | Today's artifact |
| :--- | :--- | :--- |
| **Storylet** | Self-contained authored narrative unit with title, setup, trigger, options, effects | `EventDefinition` |
| **Choice** | One option inside a storylet | `EventOption` |
| **Trigger** | Named C# predicate, resolved by string ID. Storylets declare `trigger: ["is_enlisted", "scrutiny_gte_7"]` | `TriggersAll` / `Requirements` (fragmented) |
| **Immediate** | Effect list applied on storylet fire, before player sees options | (implicit in effect ordering today) |
| **Effect** | Unit of outcome. Either inline parameters or a reference to a scripted effect by ID | `EventEffects` (inline only) |
| **Scripted effect** | Reusable named effect body in `scripted_effects.json` | (missing) |
| **Quality** | Typed numeric state with min/max, decay rule, optional hero-scoped | `Scrutiny`, `LordReputation`, `MedicalRisk`, `Fatigue`, `Discipline`, `SupplyDays`, `Morale`, `Loyalty`, `Rank`, `DaysEnlisted` — scattered |
| **Flag** | Named boolean with optional expiry; global or hero-scoped | `EscalationState.ActiveFlags` + informal identity tags + arc markers |
| **Scope** | Declared context of a storylet (which lord, which veteran, land or sea) | `RelevanceKey` (partial) |
| **Slot** | Placeholder in storylet text or trigger, filled at fire time (`{lord}`, `{veteran}`) | (missing) |
| **Chain** | Named continuation: `trigger_event { id, days }` | `ChainsTo` + `ChainDelayHours` |
| **Activity** | Stateful, phased, intent-biased, time-bounded player engagement | `Order` + `ScheduledPhase` + `CampOpportunity` (three disjoint systems) |
| **Intent** | Player's declared purpose at activity start; biases phase-pool weights | (missing) |
| **Phase** | Time window inside an activity with its own storylet pool | `ScheduledPhase` (camp only) |
| **Pool** | The set of storylets eligible in a phase | (implicit in catalog filtering) |
| **Emission** | A candidate that a source hands to the Director | `StoryCandidate` |
| **Director** | Pacing gate + tier selector + renderer dispatcher | `StoryDirector` |
| **Scope filter** | Pre-Director relevance gate | `RelevanceFilter` |

## 6. Storylet schema

### 6.1 JSON shape

```json
{
  "id": "slt_veteran_torgrim_sea_homesick",
  "title": "{=slt_torg_sea_home.t}Torgrim Stares At The Horizon",
  "setup": "{=slt_torg_sea_home.s}The deck sways. {veteran.name} hasn't touched his rations in two days.",
  "theme": "somber",
  "scope": {
    "context": "sea",
    "slots": {
      "veteran": { "role": "squadmate", "tag": "homesick" }
    }
  },
  "trigger": [
    "is_at_sea",
    "veteran_has_flag:homesick",
    "days_since:torgrim_homesick >= 3"
  ],
  "weight": {
    "base": 1.0,
    "modifiers": [
      { "if": "player_has_trait:mercy_positive", "factor": 1.5 }
    ]
  },
  "immediate": [
    { "apply": "set_flag", "name": "torgrim_in_sight" }
  ],
  "options": [
    {
      "id": "offer_hope",
      "text": "{=slt_torg_sea_home.o1}Sit with him. Talk about home.",
      "tooltip": "{=slt_torg_sea_home.t1}+1 Loyalty, -2 Fatigue. 7-day cooldown.",
      "trigger": [],
      "effects": [
        { "apply": "quality_add", "quality": "loyalty", "amount": 1 },
        { "apply": "quality_add", "quality": "fatigue", "amount": -2 },
        { "apply": "lord_rep_up_small" }
      ],
      "chain": { "id": "slt_veteran_torgrim_sea_letter", "days": 7 }
    },
    {
      "id": "stay_professional",
      "text": "{=slt_torg_sea_home.o2}Leave him to it.",
      "tooltip": "{=slt_torg_sea_home.t2}No effect. Men are men.",
      "effects": []
    }
  ],
  "cooldown_days": 10,
  "one_time": false
}
```

Key points:

- **Localization pattern preserved.** `"title": "{=id}Fallback"` is the existing convention (AGENTS.md §6). Unchanged.
- **`scope.context`** is the VC Freelancer segregation dial. Values: `"land"`, `"sea"`, `"siege"`, `"settlement"`, `"any"`. Maps to a trigger under the hood but is surfaced as a first-class field because every surface reads it.
- **`scope.slots`** declares Wildermyth-style slots. `{ "veteran": { "role": "squadmate", "tag": "homesick" } }` means "pick a squadmate with the `homesick` flag and bind them to `{veteran}` in text and triggers." `SlotFiller` resolves at fire time.
- **`trigger`** is a flat list of named predicates. Conjunction by default. CK3 `AND/OR/NOT` nesting lives in C# via composed predicates — authors call `"any_of:a|b|c"` or `"not:x"` prefixes if needed, but most content never nests.
- **`weight`** separates firing probability from firing eligibility. Base × modifiers.
- **`immediate`** fires before options render.
- **`effects`** inside an option fires on select. Each entry is `{ "apply": "<registered_effect_id>", ...params }` for scripted, or `{ "apply": "<primitive>", ...params }` for inline (e.g., `quality_add`, `set_flag`, `trait_xp`).
- **`chain`** is the only continuation mechanism. Sets `ChainContinuation=true` on the resulting `StoryCandidate` so the in-game floor + category cooldown don't defer it.
- **`cooldown_days`** and **`one_time`** are authored at storylet level — `Director` already persists these in `EventLastFired` / `OneTimeEventsFired`.

### 6.2 C# shape

```csharp
public class Storylet
{
    public string Id { get; set; }
    public string Title { get; set; }              // "{=id}Fallback" form
    public string Setup { get; set; }
    public string Theme { get; set; }
    public ScopeDecl Scope { get; set; } = new();
    public List<string> Trigger { get; set; } = [];
    public WeightDecl Weight { get; set; } = new();
    public List<EffectDecl> Immediate { get; set; } = [];
    public List<Choice> Options { get; set; } = [];
    public int CooldownDays { get; set; }
    public bool OneTime { get; set; }

    // Runtime hooks a subclass MAY override (Banner Kings pattern).
    // Default implementation reads Trigger[] and delegates to TriggerRegistry.
    public virtual bool IsEligible(StoryletContext ctx) { /* default impl */ }
    public virtual float WeightFor(StoryletContext ctx)  { /* default impl */ }
    public virtual StoryCandidate ToCandidate(StoryletContext ctx) { /* default impl */ }
}

public class Choice
{
    public string Id { get; set; }
    public string Text { get; set; }
    public string Tooltip { get; set; }
    public List<string> Trigger { get; set; } = [];   // option-level gate
    public List<EffectDecl> Effects { get; set; } = [];
    public ChainDecl Chain { get; set; }              // optional
}

public sealed class EffectDecl
{
    public string Apply { get; set; }           // e.g. "quality_add" or "lord_rep_up_small"
    public Dictionary<string, object> Params { get; set; } = new();
}

public sealed class ChainDecl
{
    public string Id { get; set; }
    public int Days { get; set; }
}

public sealed class ScopeDecl
{
    public string Context { get; set; } = "any";   // "land" | "sea" | "siege" | "settlement" | "any"
    public Dictionary<string, SlotDecl> Slots { get; set; } = new();
}

public sealed class SlotDecl
{
    public string Role { get; set; }       // "lord", "squadmate", "chaplain", "quartermaster", etc.
    public string Tag { get; set; }        // optional flag the filler must have
}
```

The base class is concrete enough that 95% of storylets never need a subclass. The 5% that do (complex promotion branches, chain coordinators, slot-fillers with bespoke rules) inherit and override — Banner Kings' pattern, validated by their survey.

### 6.3 Observational storylets (news)

A news item is just a storylet with `observational: true`, empty `options`, and default Log tier. The existing `EnlistedNewsBehavior` becomes a *source* (not a writer): it observes world events (war declared, siege begun, hero died) and emits templated news storylets with slots filled from the observed event.

```json
{
  "id": "slt_news_war_declared",
  "observational": true,
  "title": "{=slt_news_war.t}{attacker.name} declares war on {defender.name}",
  "setup": "{=slt_news_war.s}Word reaches camp: the {attacker.name} have broken peace with the {defender.name}. Heralds ride for every village.",
  "scope": {
    "slots": {
      "attacker": { "role": "kingdom" },
      "defender": { "role": "kingdom" }
    }
  },
  "trigger": ["beat:WarDeclared"],
  "options": [],
  "cooldown_days": 0
}
```

Observational storylets route through `Storylet.ToCandidate` like any other; the resulting `StoryCandidate` has `HasObservationalRender = true` and its `Payload` delegate is a no-op (no effects fire on tier grant — observational content has no mechanical outcomes). News appears in the Log accordion or, on a Headline/Pertinent beat, as an accordion headline.

One schema, two modes. Authors write both the same way.

## 7. Qualities

### 7.1 Storage

One store. One save surface. Every quality flows through it.

```csharp
public sealed class QualityStore
{
    private readonly Dictionary<string, QualityValue> _global = new();
    private readonly Dictionary<MBGUID, Dictionary<string, QualityValue>> _hero = new();

    public int Get(string qualityId);
    public int GetForHero(Hero hero, string qualityId);
    public void Set(string qualityId, int value, string reason);
    public void Add(string qualityId, int delta, string reason);
    public void SetForHero(Hero hero, string qualityId, int value, string reason);
    public void AddForHero(Hero hero, string qualityId, int delta, string reason);
    public IEnumerable<QualityDefinition> AllDefs { get; }
}

public sealed class QualityValue
{
    public int Current;
    public CampaignTime LastChanged;
    public CampaignTime LastDecayed;
}

public sealed class QualityDefinition
{
    public string Id { get; set; }
    public string DisplayName { get; set; }      // localization key
    public int Min { get; set; }
    public int Max { get; set; }
    public int Default { get; set; }
    public QualityScope Scope { get; set; }       // Global or PerHero
    public DecayRule Decay { get; set; }          // amount, interval, requireIdle
}
```

### 7.2 Definitions

Quality definitions live in `ModuleData/Enlisted/Qualities/quality_defs.json`. The seed set (derived from the existing scattered state):

| Id | Range | Scope | Kind | Notes |
| :--- | :--- | :--- | :--- | :--- |
| `scrutiny` | 0–100 | Global | Stored | From deleted `EscalationState.Scrutiny`. Gates promotion (`max_scrutiny` per tier) |
| `medical_risk` | 0–5 | Global | Stored | From deleted `EscalationState.MedicalRisk` |
| `fatigue` | 0–24 | Global | Stored | From `PlayerFatigueBehavior` |
| `discipline` | 0–100 | Global | Stored | From deleted `EscalationState.Discipline` |
| `supply_days` | -7–30 | Global | Stored | From `CompanySimulationBehavior` |
| `morale` | 0–100 | Global | Stored | From `CompanySimulationBehavior` |
| `loyalty` | 0–100 | Global | Stored | New — previously implicit in rep + flags |
| `rank_xp` | 0–∞ | Global | Read-through | Reads `EnlistmentBehavior.EnlistmentXP`. Gates promotion (`xp_threshold` from `progression_config.json`) |
| `days_in_rank` | 0–∞ | Global | Read-through | Reads `EnlistmentBehavior.DaysInRank`. Gates promotion (14–112 per tier per `PromotionRequirements.GetForTier`) |
| `battles_survived` | 0–∞ | Global | Read-through | Reads `EnlistmentBehavior.BattlesSurvived`. Gates promotion (2–60 per tier) |
| `rank` | 1–9 | Global | Read-through | Reads `EnlistmentBehavior.EnlistmentTier`. Display + trigger gate only — cannot be written via storylet (rank changes route through `EnlistmentBehavior.SetTier`) |
| `days_enlisted` | 0–∞ | Global | Read-through | From `EnlistmentBehavior` |
| `lord_relation` | -100–+100 | Global | Read-through | **Native proxy** — reads `EnlistedLord.GetRelationWithPlayer()` (TaleWorlds `Hero.cs`). Not a stored track. Gates promotion (`min_leader_relation` 0–30 per tier). Writes route through `ChangeRelationAction.ApplyPlayerRelation` |
| `hero_rep_<subject>` | -100–+100 | PerHero | Read-through | Optional — for named QM, NCO, veteran. Same native proxy pattern |

Surface specs extend this list. Spec 0 only ships the *mechanism*; the seed list is for reference.

### 7.3 Decay

```json
{
  "id": "scrutiny",
  "decay": { "amount": -1, "intervalDays": 5, "requireIdle": true, "floor": 0 }
}
```

`requireIdle` means "only decay while no storylet has raised this quality in the last `intervalDays`." Matches the current `EscalationManager.LastScrutinyRaisedTime` pattern generalised.

### 7.4 Save surface

`QualityStore` registers with `EnlistedSaveDefiner`. One new class (`QualityStore`), one new enum (`QualityScope`), one container (`Dictionary<string, QualityValue>`). Existing `EscalationState` fields (Scrutiny / LordReputation / MedicalRisk) are **deleted** — migration rule: surface specs that read them rewrite to `QualityStore.Get("scrutiny")`. Saves from before the refactor **do not migrate.** This is by designer decision (no migration).

### 7.5 Read-through qualities (native TaleWorlds proxies)

Not every quality stores its own data. A `Kind: Read-through` quality registers a reader delegate and optionally a writer delegate; `QualityStore.Get("rank_xp")` calls the reader, `QualityStore.Add("lord_relation", +5)` calls the writer. This is how the storylet layer reads from and writes to native TaleWorlds state without duplicating it.

```csharp
// During QualityStore.RegisterAll():
Register(new QualityDefinition
{
    Id = "lord_relation",
    Min = -100, Max = 100,
    Kind = QualityKind.ReadThrough,
    Reader = () => EnlistmentBehavior.Instance?.EnlistedLord?.GetRelationWithPlayer() ?? 0,
    Writer = (delta, reason) =>
    {
        var lord = EnlistmentBehavior.Instance?.EnlistedLord;
        if (lord != null)
            ChangeRelationAction.ApplyPlayerRelation(lord, delta, affectRelatives: false, showQuickNotification: false);
    }
});
```

Read-through qualities are not persisted (their source is). They still participate in triggers (`quality_gte:lord_relation:25`) and effects (`{ "apply": "quality_add", "quality": "lord_relation", "amount": 5 }`) identically. Authors see no difference.

The promotion gate table (`PromotionRequirements.GetForTier`) becomes data: `ModuleData/Enlisted/Promotion/promotion_requirements.json` mapping `targetTier → { xp_threshold, days_in_rank, battles_survived, min_lord_relation, max_scrutiny }`. The `can_promote_to_tier:N` trigger is a composite that reads `rank_xp`, `days_in_rank`, `battles_survived`, `lord_relation`, and `scrutiny` against the target row.

## 8. Flags

### 8.1 Storage

```csharp
public sealed class FlagStore
{
    private readonly Dictionary<string, CampaignTime> _global = new();
    private readonly Dictionary<MBGUID, Dictionary<string, CampaignTime>> _hero = new();

    public bool Has(string flag);
    public bool HasForHero(Hero hero, string flag);
    public void Set(string flag, CampaignTime expiry);            // CampaignTime.Never = permanent
    public void SetForHero(Hero hero, string flag, CampaignTime expiry);
    public void Clear(string flag);
    public void ClearForHero(Hero hero, string flag);
    public int DaysSinceSet(string flag);                         // for "days_since:" triggers
}
```

Replaces `EscalationState.ActiveFlags`, replaces informal identity-tag dictionaries, replaces arc progress markers. One API.

### 8.2 Conventions

- Flag names are snake_case strings. No enum — authors can mint new flags without touching C#.
- Expiry defaults to `CampaignTime.Never` (permanent). Time-boxed flags pass a `CampaignTime.DaysFromNow(n)` value.
- Hero-scoped flags (e.g., `homesick` on a squadmate) persist with the hero's GUID — survive squad churn if the hero is retained, drop with the hero if dismissed.
- Purge on hero-death is handled by a save-load post-hook in `FlagStore.OnGameLoaded`.

### 8.3 Save surface

`FlagStore` registers with `EnlistedSaveDefiner`. `EscalationState.ActiveFlags` is deleted.

## 9. Activities

### 9.1 Model

An Activity is a saveable, ticking, stateful object with phases.

```csharp
public abstract class Activity
{
    public string Id { get; protected set; }           // instance id, e.g. "camp_20260419_0300"
    public string TypeId { get; protected set; }       // template id, e.g. "activity_camp"
    public string Intent { get; protected set; }       // "unwind" | "train_hard" | "brood" | "commune"
    public List<Phase> Phases { get; protected set; } = [];
    public int CurrentPhaseIndex { get; protected set; }
    public List<Hero> Attendees { get; protected set; } = [];
    public CampaignTime StartedAt { get; protected set; }
    public CampaignTime EndsAt { get; protected set; }
    public Dictionary<string, int> PhaseQuality { get; protected set; } = new();  // composite scalars

    public abstract void OnStart(ActivityContext ctx);
    public abstract void Tick(bool hourly);
    public abstract void OnPhaseEnter(Phase phase);
    public abstract void OnPhaseExit(Phase phase);
    public abstract void Finish(ActivityEndReason reason);
}

public sealed class Phase
{
    public string Id { get; set; }
    public int DurationHours { get; set; }
    public List<string> Pool { get; set; } = [];       // storylet ids eligible this phase
    public Dictionary<string, float> IntentBias { get; set; } = new();  // intent -> weight multiplier
}
```

### 9.2 Intent

Intent is declared at activity start and biases phase-pool weights throughout. It is a quality-like field but scoped to the activity instance (not persisted in `QualityStore` — it's part of the activity's own state).

For the Camp activity (Spec 1 will finalise), the intents are `unwind`, `train_hard`, `brood`, `commune`. Each intent attaches a bias dictionary to storylet selection:

```json
"intent_bias": {
  "unwind":      { "campfire_social":  1.5, "training_drill": 0.3 },
  "train_hard":  { "campfire_social":  0.5, "training_drill": 2.0 },
  "brood":       { "introspection":    2.0, "campfire_social": 0.5 },
  "commune":     { "chaplain_talk":    2.0, "training_drill": 0.5 }
}
```

### 9.3 Runtime

`ActivityRuntime` is the singleton that:

- Instantiates activities from `ActivityTypeDefinition` JSON in `ModuleData/Enlisted/Activities/`
- Ticks active activities on `CampaignEvents.HourlyTickEvent`
- Selects a phase-pool storylet each tick that qualifies, weighted by intent bias
- Routes the selected storylet through `Storylet.ToCandidate(ctx)` → `StoryDirector.EmitCandidate`
- Drives phase transitions (via `Phase.DurationHours`)
- Calls `Finish` on completion / exit / interruption
- Persists active activities to save (one container: `List<Activity>`)

Orders, Camp, Muster, Quartermaster, and (eventually) Promotion each ship concrete `Activity` subclasses in their surface specs. Spec 0 only ships the base class and the runtime.

### 9.4 Save surface

`ActivityRuntime` owns a `List<Activity>` container in save. Each concrete subclass registers with `EnlistedSaveDefiner` in its surface spec.

## 10. Scope and slot-filling

### 10.1 Scope primitives

A storylet declares:

```json
"scope": {
  "context": "sea",
  "slots": {
    "lord": { "role": "enlisted_lord" },
    "veteran": { "role": "squadmate", "tag": "homesick" },
    "chaplain": { "role": "chaplain" }
  }
}
```

`SlotFiller.Resolve(scopeDecl, worldContext) → Dictionary<string, Hero>` runs before `IsEligible` and again before text rendering. If any required slot cannot be filled, the storylet is ineligible — dropped before weighting.

### 10.2 Slot roles

Seed role list (surface specs extend):

| Role | Source |
| :--- | :--- |
| `enlisted_lord` | `EnlistmentBehavior.CurrentLord` |
| `squadmate` | `RetinueManager.ActiveRetinue` (any named veteran) |
| `chaplain` | Mod-spawned `Occupation.Soldier` hero tagged `chaplain` |
| `quartermaster` | `QuartermasterManager.QuartermasterHero` |
| `enemy_commander` | `MapEvent.OtherSide.LeaderParty.LeaderHero` (if in battle) |
| `settlement_notable` | Nearest visited settlement's `Notables` |

### 10.3 Context is a first-class runtime dimension

`scope.context` is not just authoring metadata — it is **derived every tick from game state** and used by the Director as a hard filter. Authored values and their derivation source:

| Context | Active when | Precedence |
| :--- | :--- | :--- |
| `siege` | `MobileParty.MainParty.BesiegerCamp != null` OR `MobileParty.MainParty.CurrentSettlement?.SiegeEvent != null` | highest |
| `settlement` | `MobileParty.MainParty.CurrentSettlement != null` (and no siege) | |
| `sea` | `MobileParty.MainParty.IsCurrentlyAtSea == true` (and none of the above) | |
| `land` | none of the above | lowest (default) |
| `any` | matches any runtime context | — |

API references (confirmed against `C:\Dev\Enlisted\Decompile\` for v1.3.13):

- `MobileParty.IsCurrentlyAtSea` — `TaleWorlds.CampaignSystem.Party/MobileParty.cs:483` (read/write bool)
- `MobileParty.CurrentSettlement` — `TaleWorlds.CampaignSystem.Party/MobileParty.cs:566`
- `MobileParty.BesiegerCamp` — `TaleWorlds.CampaignSystem.Party/MobileParty.cs:679`
- `MobileParty.SiegeEvent` — `TaleWorlds.CampaignSystem.Party/MobileParty.cs:1087` (returns `BesiegerCamp?.SiegeEvent` — null when not actively besieging)
- `Settlement.SiegeEvent` — `TaleWorlds.CampaignSystem.Settlements/Settlement.cs:111` (non-null when the settlement is under siege, useful for the "player is in a besieged settlement" branch)
- `MapEvent.IsNavalMapEvent` — `TaleWorlds.CampaignSystem.MapEvents/MapEvent.cs:334` (derived from `!Position.IsOnLand`)
- `MobileParty.HasNavalNavigationCapability` — `TaleWorlds.CampaignSystem.Party/MobileParty.cs:313`

Precedence matches the existing mod bugfix at `src/Features/Camp/CampOpportunityGenerator.cs:969`: a party in a settlement or besieging is **not** at sea for storylet-selection purposes, regardless of `IsCurrentlyAtSea`. Siege overrides settlement.

**Director filtering.** Before severity classification, the Director drops any candidate whose source storylet's `scope.context` is neither `any` nor the current runtime context. Applies uniformly to interactive and observational storylets — land news is ineligible at sea, sea news is ineligible on land, siege storylets are ineligible outside a siege. Context segregation is therefore automatic; authors never write `"trigger": ["is_at_sea"]` defensively.

**Context transition beats.** Two new entries in `StoryBeat`, monitored by `ActivityRuntime` on hourly tick (no direct TaleWorlds event exists for sea/land transitions — verified against the decompile; the engine exposes `CampaignEvents.OnSiegeEventStartedEvent` for siege but nothing analogous for naval transitions, so we detect state change by comparing against the previous tick's `IsCurrentlyAtSea` value):

| Beat | Fires when |
| :--- | :--- |
| `BoardedShip` | player transitions from `!IsCurrentlyAtSea` to `IsCurrentlyAtSea` |
| `DisembarkedShip` | player transitions from `IsCurrentlyAtSea` to `!IsCurrentlyAtSea` |

Storylets that want to narrate boarding or landing author `trigger: ["beat:BoardedShip"]`. The existing pacing-spec beats (`SettlementEntered`, `SettlementLeft`, `SiegeBegin`, `SiegeEnd`) already cover the other two transitions and route through the native `CampaignEvents.OnSiegeEventStartedEvent` / `OnSettlementEnteredEvent` hooks.

**Aggregation at transition.** When a context transition fires, the Director flushes any deferred observational storylets from the prior context as a "news from shore" digest (if transitioning to sea) or "news from the voyage" digest (if transitioning to land). Implementation reuses the existing `Pertinent`-tier digest mechanism from the pacing spec §10.3.

### 10.5 Text substitution

Authored syntax is `{slot.name}`, `{slot.occupation}`, `{slot.skill:melee}` in `Title`, `Setup`, `Option.Text`, `Option.Tooltip`. `SlotFiller` flattens these to TaleWorlds-compatible flat keys at fire time — `{veteran.name}` becomes `TextObject.SetTextVariable("VETERAN_NAME", hero.Name)`, `{lord.occupation}` becomes `SetTextVariable("LORD_OCCUPATION", ...)`. The nested authoring syntax is ours; the runtime string passed to `MBTextManager` is flat (TaleWorlds' `TextObject` does not parse dotted paths).

The authored `TextObject` template uses the flat form: `{=slt_id}The {VETERAN_NAME} stares at the horizon.` Authors don't write the flat form by hand — the storylet loader pre-rewrites dotted slot references during JSON → `Storylet` construction. One source of truth for the dotted syntax, one flat key for the engine.

## 11. Scripted effects

### 11.1 Registry

Named effects live in one JSON file with corresponding C# entries:

```json
// ModuleData/Enlisted/Effects/scripted_effects.json
{
  "effects": {
    "lord_rep_up_small": [
      { "apply": "quality_add", "quality": "lord_reputation", "amount": 2 },
      { "apply": "quality_add", "quality": "loyalty", "amount": 1 }
    ],
    "scrutiny_up_major": [
      { "apply": "quality_add", "quality": "scrutiny", "amount": 10 },
      { "apply": "set_flag", "name": "under_investigation", "days": 5 }
    ]
  }
}
```

`ScriptedEffectRegistry` resolves an effect ID to its body at load time and caches it. Storylets referencing an unknown ID fail validation at startup (`validate_content.py` Phase 12 — new).

### 11.2 Primitive effects

The C# side ships a small set of primitive effects that scripted effects compose from:

| Primitive | Parameters |
| :--- | :--- |
| `quality_add` | `quality`, `amount` |
| `quality_set` | `quality`, `value` |
| `set_flag` | `name`, `days?` (default permanent) |
| `clear_flag` | `name` |
| `set_flag_on_hero` | `slot`, `name`, `days?` |
| `trait_xp` | `trait`, `amount` |
| `skill_xp` | `skill`, `amount` |
| `give_gold` | `amount` (routes through `GiveGoldAction` per CLAUDE.md §3) |
| `give_item` | `item_id`, `count` |

Primitives dispatch to `EffectExecutor`, which is the thin C# layer that does the actual game-state mutation.

## 12. Chain semantics

### 12.1 Authoring

Two chain forms:

```json
"chain": { "id": "slt_followup", "days": 7 }
```

```json
"chain": { "id": "slt_promoted", "when": "next_muster.promotion_phase" }
```

The first form fires after an in-game delay. The second form parks until a named phase of a named activity runs and then fires during that phase. The second form is how promotion chains and escalation-deferred storylets land in the Muster Activity (see §15) — it's the mechanism that makes the muster menu the legibility layer for promotions and big news.

### 12.2 Runtime

On option-select, if `chain` is non-null, the executor schedules a deferred `StoryCandidate`:

1. **Time-delay form** (`days: N`) → `StoryDirector.ScheduleChain(storyletId, fireAt)` — reuses the existing `PendingChainEvents` persistence.
2. **Phase-target form** (`when: "activity.phase"`) → `ActivityRuntime.ParkChain(storyletId, activityTypeId, phaseId)` — persisted on the runtime, consumed when the matching activity's matching phase runs.

Either way, the candidate sets `ChainContinuation = true` on fire so the Director's in-game floor + category cooldown do not defer it (60s wall-clock guard still applies — from the 2026-04-18 pacing spec).

### 12.3 Multi-step arcs

Multi-storylet arcs express as: each storylet's chain chains to the next. The `arc_*` flag convention marks progression (`flag:arc_torgrim_homecoming_step_2`). Arc completion is declarative — when the final storylet fires, it sets `flag:arc_torgrim_homecoming_complete`.

### 12.4 Deferred-to-activity queue

The `when:` chain form generalises the existing `QueueEscalationEventForAfterMuster` pattern. Any storylet can target any activity phase; not just muster. Typical targets:

- `next_muster.pay_phase` — pay-related surprises land during the pay stage
- `next_muster.promotion_phase` — rank advancement and promotion-chain resolutions
- `next_muster.retinue_phase` — squad news, casualty reports, morale moments
- `next_camp.arrive_phase` — things that want to greet the player when they enter camp

This is the single mechanism behind "muster is the legibility layer." Promotions, escalation outcomes, retinue drama, and pay consequences all chain themselves to the appropriate muster phase rather than interrupting mid-duty.

## 13. Director integration

### 13.1 Storylet → Candidate

`Storylet.ToCandidate(StoryletContext)` builds a `StoryCandidate` by:

- Resolving slots via `SlotFiller`
- Rendering `Title` + `Setup` with slot substitution → `RenderedTitle`, `RenderedBody`
- Attaching the storylet's `Beats`, `Tier`, and `SeverityHint` (read from `Storylet.Weight` + authored `severity` field)
- Producing a `Payload` delegate that, when the Director greenlights, applies `Immediate` effects and hands options to `EventDeliveryManager` (for Modal tier) or writes an accordion entry (for Headline/Pertinent tiers)

The `StoryCandidate` schema is unchanged — Spec 0 adds no fields.

**Pause contract.** `InformationManager.ShowInquiry(data, pauseGameActiveState, prioritize)` does **not** auto-pause the campaign. Modal-tier payloads must pass `pauseGameActiveState: true` explicitly. The existing pacing-spec doctrine (§4.4, "menu-open is the consumption surface") is enforced at the call site — not by the engine.

### 13.2 IssueBase (explicit non-adoption)

Per the external mod survey (Banner Kings, AdditionalQuests), `IssueBase` is the correct vehicle for *walk-up notable-at-settlement side jobs*, not for storylets. Spec 5 (Quartermaster Activity) may adopt `IssueBase` for a specific category of QM side-jobs (errand board pattern). Spec 0 does not adopt it for the storylet backbone.

## 14. Save surface

New registrations in `EnlistedSaveDefiner`:

```csharp
// Classes
AddClassDefinition(typeof(QualityStore), 40);
AddClassDefinition(typeof(QualityValue), 41);
AddClassDefinition(typeof(QualityDefinition), 42);
AddClassDefinition(typeof(FlagStore), 43);
AddClassDefinition(typeof(Activity), 44);             // abstract base — see note below

// Enums
AddEnumDefinition(typeof(QualityScope), 82);
AddEnumDefinition(typeof(ActivityEndReason), 83);

// Containers
ConstructContainerDefinition(typeof(Dictionary<string, QualityValue>));
ConstructContainerDefinition(typeof(Dictionary<MBGUID, Dictionary<string, QualityValue>>));
ConstructContainerDefinition(typeof(Dictionary<string, CampaignTime>));   // FlagStore._global
ConstructContainerDefinition(typeof(Dictionary<MBGUID, Dictionary<string, CampaignTime>>));
ConstructContainerDefinition(typeof(List<Activity>));
```

`EscalationState`'s Scrutiny / LordReputation / MedicalRisk / ActiveFlags fields are **deleted**. Existing saves will lose this data on load. Non-negotiable per designer decision.

**Abstract `Activity` + concrete subclasses.** `SaveableTypeDefiner.AddClassDefinition` works for abstract bases — Spec 0 registers `Activity` at offset 44 for the polymorphic container (`List<Activity>`). Each concrete subclass takes its own offset in its owning surface spec: `MusterActivity` in Spec 4, `CampActivity` in Spec 1, `OrderActivity` in Spec 2, `QuartermasterActivity` in Spec 5. Offsets 45–60 are reserved for these subclasses; surface specs claim from that range.

## 15. Deletion list

Spec 0 ships plumbing only — no deletions yet. The table below is the **authoritative deletion list for the surface specs** that follow, so we lock in what disappears upfront and every surface spec just confirms "yes, I handled my row":

| Deleted by Spec | File / class / field | Replaced by |
| :--- | :--- | :--- |
| 1 (Home Surface) | `Camp/CampMenuHandler.cs` | `Activity` subclass `CampActivity` + `EnlistedHomeMenuHandler` |
| 1 | `Content/ContentOrchestrator.cs` (pre-scheduling logic) | `ActivityRuntime` (generic) |
| 1 | `Content/Models/CampOpportunity.cs`, `ScheduledOpportunity`, `ScheduledPhase` | `Phase` + `Pool` |
| 2 (Orders) | `Orders/Behaviors/OrderManager.cs`, `OrderProgressionBehavior.cs` | `Activity` subclass `OrderActivity` |
| 2 | `Orders/Models/Order.cs`, `OrderPrompt.cs`, `OrderConsequence.cs`, `OrderOutcome.cs`, `PhaseRecap.cs`, `PromptOutcome.cs`, `PromptOutcomeData.cs` | `Activity` + `Storylet` + `ScriptedEffect` |
| 2 | `Orders/OrderCatalog.cs`, `PromptCatalog.cs` | `StoryletCatalog` (unified) |
| 2 | `ModuleData/Enlisted/Orders/*.json`, `order_events/*.json` | Rewritten under new schema in `ModuleData/Enlisted/Activities/` + `ModuleData/Enlisted/Storylets/` |
| 3 (Land/Sea) | `Content/MapIncidentManager.cs`, `IncidentEffectTranslator.cs` | `StoryletCatalog` + Director (incidents become storylets) |
| 3 | `ModuleData/Enlisted/Events/incidents_*.json` | Rewritten as scoped storylets |
| 3 | `CampOpportunity.AtSea` / `NotAtSea` bool gating fields (see `CampOpportunityGenerator.cs:975`, `:981`) | `scope.context: "sea" \| "land"` on the storylet itself |
| 4 (Promotion) | `src/Features/Ranks/Behaviors/PromotionBehavior.cs` (553 lines) — `CanPromote()` logic, hourly-tick eligibility check, pending-tier spam guard, `TriggerPromotionNotification` ceremony popup, intentional Director-bypass `InquiryData` at L460-466 | Chain storylet family per rank transition (T2–T9, 8 chains total, no activity wrapper). `CanPromote` → composite trigger `can_promote_to_tier:N`. Pending-tier spam guard → storylet `one_time: false` + cooldown + flag `pending_promotion_tN`. Ceremony popup → `chain: { id: "slt_promo_celebrated_tN", days: 0 }` from the interview-accept option (synchronous chain replaces the intentional bypass). Storylets chain via `when: "next_muster.promotion_phase"` for the interview step so promotions land in the muster ceremony |
| 4 (Promotion) | `PromotionRequirements.GetForTier` hardcoded switch table in `PromotionBehavior.cs:30-56` | `ModuleData/Enlisted/Promotion/promotion_requirements.json` — data-driven; read by `can_promote_to_tier:N` trigger |
| 4 (Promotion) | `ModuleData/Enlisted/Events/events_promotion.json` (existing storylet-like JSON for proving events `promotion_t2_t3_sergeants_test`, `promotion_t3_t4_crisis_of_command`, etc.) | Rewritten under new storylet schema with identical IDs — one proving-step storylet per transition plus its celebrated-step chain |
| 4 (Promotion) | `EscalationManager.HasDeclinedPromotion`, `ClearAllDeclinedPromotions` methods + backing flag | `FlagStore.Has("promotion_declined_tN")` — per-tier flag, expires on new enlistment (handled by `OnEnlisted` clearing pattern) |
| 4 (Muster) | `Enlistment/Behaviors/MusterMenuHandler.cs` (3,751 lines) | `MusterActivity` subclass: 5 phases (Intro / Pay / Promotion / Retinue / Complete), per-phase storylet pools, `MusterOutcomeRecord` → `PhaseQuality` composite state, intent derived from state (normal / high-scrutiny / discharge-threshold / first-muster), `fire_at_next_muster` deferred-queue via `when:` chain targets |
| 5 (Quartermaster) | `QuartermasterManager` dialog-event hybrid | `QuartermasterActivity` + QM storylets + optional `IssueBase` shim for side-jobs |
| All | `Content/EventDefinition.cs`, `EventOption.cs`, `EventCatalog.cs`, `EventSelector.cs`, `EventRequirementChecker.cs`, `DecisionCatalog.cs`, `DecisionManager.cs` | `Storylet` + `StoryletCatalog` + `TriggerRegistry` + `SlotFiller` |
| All | `Escalation/EscalationState.cs` fields (Scrutiny, LordReputation, MedicalRisk, ActiveFlags) | `QualityStore` + `FlagStore` |

The deletion list is intentionally held in Spec 0 (not dispersed across surface specs) so that no surface spec misses its row and leaves a zombie subsystem running.

## 16. What Spec 0 ships (concrete file list)

New files created **in this spec's implementation**:

```
src/Features/Content/Storylet.cs                  // base class, default impls
src/Features/Content/Choice.cs
src/Features/Content/EffectDecl.cs
src/Features/Content/ScopeDecl.cs
src/Features/Content/ChainDecl.cs
src/Features/Content/StoryletContext.cs
src/Features/Content/StoryletCatalog.cs           // loader, replaces EventCatalog
src/Features/Content/TriggerRegistry.cs           // named C# predicates
src/Features/Content/EffectExecutor.cs            // primitive effect dispatch
src/Features/Content/ScriptedEffectRegistry.cs
src/Features/Content/SlotFiller.cs

src/Features/Qualities/QualityStore.cs
src/Features/Qualities/QualityValue.cs
src/Features/Qualities/QualityDefinition.cs
src/Features/Qualities/QualityScope.cs
src/Features/Qualities/QualityCatalog.cs          // loader
src/Features/Qualities/QualityBehavior.cs         // decay tick hook

src/Features/Flags/FlagStore.cs
src/Features/Flags/FlagBehavior.cs                // expiry sweep hook

src/Features/Activities/Activity.cs               // abstract base
src/Features/Activities/ActivityRuntime.cs
src/Features/Activities/ActivityContext.cs
src/Features/Activities/ActivityEndReason.cs
src/Features/Activities/Phase.cs
src/Features/Activities/ActivityTypeDefinition.cs

ModuleData/Enlisted/Qualities/quality_defs.json   // seed definitions (10 qualities from §7.2)
ModuleData/Enlisted/Effects/scripted_effects.json // empty; surface specs populate
```

`Enlisted.csproj` registers every new `.cs` file (AGENTS.md §2).
`EnlistedSaveDefiner` registers every new saveable class (§14).
`validate_content.py` grows Phase 12 — validates scripted-effect references and quality IDs.
`Tools/Validation/generate_error_codes.py` is unchanged.

## 17. Testing

### 17.1 Unit tests

- `QualityStore.Add` clamps to `[min, max]`
- `QualityStore` decay tick respects `requireIdle` + `floor`
- `FlagStore.Has` returns false after expiry
- `FlagStore` hero-scoped flags drop when hero dies
- `SlotFiller.Resolve` returns null when required slot has no valid hero
- `SlotFiller.Resolve` filters candidates by `tag` when declared
- `TriggerRegistry` resolves `quality_gte:scrutiny:7` → reads `QualityStore`
- `ScriptedEffectRegistry` validates at load — unknown apply IDs fail fast
- `Storylet.ToCandidate` builds a candidate with substituted text and chain flag
- `ActivityRuntime` ticks one phase's pool, selects weighted by intent bias
- `ActivityRuntime.Finish` calls concrete subclass's `Finish` with correct reason

### 17.2 Integration scenarios

- Seed quality + flag + storylet + scripted effect. Fire storylet. Assert quality changed, flag set, chain scheduled.
- Activity with 4 phases runs for simulated hours; assert phase transitions, intent-biased pool selection, `Finish` called with `Completed` at end.
- Save mid-activity, load, assert activity resumes at correct phase with attendees.
- Unknown scripted-effect reference in JSON fails `validate_content.py` — build-blocking.

## 18. Risks and mitigations

| Risk | Mitigation |
| :--- | :--- |
| Storylet-to-candidate path regresses Director pacing | No changes to `StoryDirector`; Spec 0 only adds upstream. Integration tests for each tier. |
| Trigger registry becomes a dumping ground | Keep it flat (no classes, just static delegates); cap one file per surface (e.g., `OrderTriggers.cs`) so ownership is obvious. |
| Hero-scoped qualities explode save size | Hero-scoped qualities are opt-in per definition; seed list (§7.2) uses `PerHero` only for `hero_rep_<subject>`. |
| Slot-filler is O(n·squad) per tick | `SlotFiller` caches per-tick results by scope hash. Retinue is ≤12 heroes — cheap. |
| Activities leak state on crash | `ActivityRuntime.OnGameLoad` validates each active activity's invariants; invalid ones call `Finish(reason=InvalidStateAfterLoad)` so they clean up. |
| Deleting `EscalationState` fields corrupts existing saves | Accepted — designer explicitly chose zero-migration. Release notes flag save break. |
| Validation tool drift | `validate_content.py` Phase 12 is build-blocking; adding storylets without corresponding quality defs or effect refs fails CI. |

## 19. Out of scope (explicit)

- Any storylet content. All surface specs author their own.
- Any activity template. Spec 0 ships only the abstract `Activity` base.
- Any new Gauntlet UI. Renderers unchanged from 2026-04-18 pacing spec.
- Any MCM settings. Developer-only for now.
- Any IssueBase adoption. Deferred to Spec 5 (Quartermaster) if at all.
- Any changes to conversation trees. Dialogue stays in its own system (`qm_dialogue.json` etc.).
- Any changes to `Enlistment/EncounterGuard` or the escort-AI attachment trick. Load-bearing, untouched.
- Any rename of existing class names that *don't* align — e.g., we don't rename `StoryDirector` to `NarrativeDirector`, we don't rename `StoryCandidate` to `Emission` (though the table in §5 lists the mapping for documentation). The class names that are *close enough* stay; only the ones that break vocabulary (`EventDefinition`, `EscalationState`, `CampOpportunity`) get replaced.

## 20. Open questions

- **Quality mutation audit log.** Do we persist the `reason` parameter on `QualityStore.Add` for debugging? *Recommend: in-memory ring buffer of last 200 changes, not persisted.*
- **Flag namespacing.** Do hero-scoped flags reuse the same namespace as global flags, or separate? *Recommend: same namespace; hero-scope is storage-level, not semantic.*
- **Storylet versioning.** If a storylet's schema changes mid-save, how do we handle the deferred chain already scheduled? *Recommend: deferred candidates store a schema hash; on mismatch, silently drop with `ModLogger.Caught`.*
- **Intent as a quality?** Should `Activity.Intent` actually be a per-activity quality on `QualityStore` for consistency? *Recommend: no — intents are activity-instance state and don't decay. Keeping them inside `Activity` is cleaner.*

## 21. References

- [2026-04-18 Event Pacing Redesign](2026-04-18-event-pacing-design.md) — the delivery half; Spec 0 is the content half
- [Emily Short on Storylets (emshort.blog, 2016)](https://emshort.blog/2016/04/12/beyond-branching-quality-based-and-salience-based-narrative-structures/) — canonical QBN treatment
- [CK3 Event Modding wiki](https://ck3.paradoxwikis.com/Event_modding) — CK3 event schema
- [CK3 Activity modding wiki](https://ck3.paradoxwikis.com/Activity_modding) — intent / phase / attendee model
- [Banner Kings — Feast.cs](https://github.com/R-Vaccari/bannerlord-banner-kings/blob/main/BannerKings/Behaviours/Feasts/Feast.cs) — stateful activity pattern
- [Banner Kings — BannerKingsEvent.cs](https://github.com/R-Vaccari/bannerlord-banner-kings/blob/main/BannerKings/Behaviours/Events/BannerKingsEvent.cs) — option/resolution primitive
- [AdditionalQuests](https://github.com/hamzah-hayat/AdditionalQuests) — IssueBase reference pattern
- [Viking Conquest Freelancer (binary-only, ModDB)](https://www.moddb.com/mods/viking-conquest-with-freelancer) — context-segregation pattern (land/sea)

---

**Next step:** On designer approval, hand off to `superpowers:writing-plans` to produce the implementation plan (step-wise delivery of §16's file list, with review checkpoints per subsystem).
