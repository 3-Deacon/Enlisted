# Enlisted Home Surface — Design Spec

**Date:** 2026-04-19
**Status:** Drafted 2026-04-19. Depends on Spec 0 (Storylet Backbone, landed `45b38bf`). Implementation plan to follow.
**Scope:** The first player-experience surface built on the Spec 0 storylet backbone: what the player does when the lord's party is parked at a friendly settlement (town, castle, or village). Defines `HomeActivity`, the Evening-phase `PlayerChoice` runtime, the menu integration, the engine-state trigger palette, and the 50-storylet launch shelf.

This is **Spec 1** in the 6-spec cycle. Spec 0 established the substrate (`QualityStore`, `FlagStore`, `ActivityRuntime`, `Storylet`, `ScriptedEffectRegistry`, `TriggerRegistry`). Spec 1 delivers the first consumer — the "enlisted home" surface — and in doing so implements the `PhaseDelivery.PlayerChoice` runtime that Specs 2-5 will also use.

---

## 1. Problem

Spec 0 shipped infrastructure with no content and no consumer. The `enlisted_status` menu (322 KB `EnlistedMenuBehavior.cs`) already owns the home-screen accordion (headlines / orders / decisions / camp_hub), but nothing currently writes camp-time narrative into it beyond the generic `StoryDirector` headline pipeline. `OrchestratorOverride.cs` + `orchestrator_overrides.json` were scaffolded pre-Spec-0 as a need-based activity-injection system and never wired to `ActivityRuntime`. `CompanySupplyManager._totalSupply` and `CompanyNeedsState.Readiness` are mod-invented parallel numbers that do **not** read engine state (`MobileParty.Food`, `MobileParty.Morale`). The camp experience is mostly silence.

Meanwhile the native engine exposes rich, daily-ticked state the mod ignores:

| Engine API | Decompile citation | What it measures |
| :--- | :--- | :--- |
| `MobileParty.Food` + `GetNumDaysForFoodToLast()` | `MobileParty.cs:1089`, `:3231` | Days until the lord's party starves |
| `MobileParty.Morale` | `MobileParty.cs:878` | Troop morale `[0, 100]`, model-output |
| `MobileParty.DefaultBehavior` | `MobileParty.cs:710` | `AiBehavior` enum — tactical posture |
| `MobileParty.IsDisorganized` + `Speed` | `MobileParty.cs:416`, `:388` | Recent-defeat flag + map speed |
| `Hero.IsWounded` + `HitPoints` | `Hero.cs:329`, `:401` | Lord's physical state |
| `Hero.GetRelationWithPlayer()` | `Hero.cs:1995` | Lord ↔ player relation |
| `Hero.GetTraitLevel(DefaultTraits.X)` | `Hero.cs:1403` | `Mercy` / `Valor` / `Honor` / `Calculating` scores `[-2, 2]` |
| `Hero.Spouse` / `Children` / `HomeSettlement` | `Hero.cs:818`, `:843`, `:709` | Family + seat-of-the-clan |
| `Clan.IsUnderMercenaryService` | `Clan.cs:242` | Merc-vs-vassal (`SaveableProperty(75)`) |
| `Clan.FactionsAtWarWith` | `Clan.cs:402` | War context + multi-front detection |
| `Settlement.IsTown` / `IsCastle` / `IsVillage` | `Settlement.cs:378`, `:390` | Settlement type |
| `Settlement.IsUnderSiege` + `LastThreatTime` | `Settlement.cs:466`, `:360` | Regional threat |
| `Town.Prosperity` / `Loyalty` / `Security` | `Town.cs:112`, `:191`, `:171` | Settlement health |
| `CampaignTime.Now.IsNightTime` + `CurrentHourInDay` | `CampaignTime.cs` | Temporal context |

Spec 1 reads this state directly via `TriggerRegistry` predicates. The mod's `QualityStore` is reserved for genuinely invented axes (`scrutiny`, `rank_xp`, `days_in_rank`, `lord_relation` as a cached mirror). No parallel simulation.

## 2. Goals

- Deliver a felt camp experience at friendly settlements: arrival beat, ambient settle, one nightly player-choice moment, departure beat.
- Five player-facing intents at Evening: `unwind`, `train_hard`, `brood`, `commune`, `scheme`.
- Content pool gated by real engine state, not parallel mod fiction.
- Implement `PhaseDelivery.PlayerChoice` runtime once, generically, so Specs 2-5 inherit it.
- Single surgical hook into `EnlistedMenuBehavior` (already 322 KB — do not extend further than necessary).
- Ship 50 storylets at launch; expansion is content-only, no engine work.
- Delete `OrchestratorOverride` scaffolding; home-camp need-response is expressed as weighted triggers on regular storylets.

## 3. Non-goals

- **No on-road camps.** "The company makes camp on the march road" is a different context — `EnterSettlementAction` does not fire, `Settlement.CurrentSettlement` is null. A future spec covers that. Spec 1 is settlement-home only (`at_town` ∪ `at_castle` ∪ `at_village`).
- **No refactor of `CompanySupplyManager` or `CompanyNeedsState`.** They stay. New Spec 1 triggers read engine state directly; the parallel mod-fiction numbers remain for the surfaces that already depend on them (Orders, CampMenuHandler). A future cleanup may consolidate.
- **No rewrite of `EnlistedMenuBehavior`.** One hook added (~20 LOC); everything else stays.
- **No new error-output channels.** Use existing `ModLogger.Surfaced` / `Caught` / `Expected`. Logs land in `Modules/Enlisted/Debugging/` per the existing convention.
- **No content for Specs 2-5.** Only home-surface storylets.

## 4. Vocabulary

Spec 1 inherits Spec 0's vocabulary (Storylet, Trigger, Quality, Flag, Scope, Slot, Chain, Activity, Phase, Intent) and adds:

| Term | Meaning |
| :--- | :--- |
| **HomeActivity** | Concrete `Activity` subclass. Starts on friendly-settlement entry, ends on settlement departure. Owns four phases. |
| **Intent** (home-specific) | One of `unwind` / `train_hard` / `brood` / `commune` / `scheme`. Player picks at Evening; initial value set by starter based on lord state. |
| **Phase** (home-specific) | `arrive` (auto, 1h) / `settle` (auto, 2-4h) / `evening` (PlayerChoice, 1 fire) / `break_camp` (auto, on departure beat). |
| **PhaseChoiceProvider** | Interface that supplies menu options for a `PlayerChoice` phase. `HomeEveningMenuProvider` is its first implementation. Specs 2-5 register their own. |
| **Engine-state trigger** | `TriggerRegistry` predicate that reads live engine API (not `QualityStore`). Named with `lord_` / `party_` / `settlement_` / `kingdom_` / `time_` prefixes. |

## 5. Architecture

```
┌───────────────────────────────────────────────────────────────────────────┐
│ CampaignEvents.SettlementEntered  ──►  HomeActivityStarter                │
│                                         │                                 │
│                                         ▼                                 │
│                                   ActivityRuntime.Start(HomeActivity)     │
│                                         │                                 │
│                                         ▼                                 │
│        ┌────────────────────────────────┴────────────────────┐            │
│        │                                                     │            │
│        ▼                                                     ▼            │
│   HomeActivity (Spec 1)                           ActivityRuntime core    │
│   - Intent (5 strings)                            - OnHourlyTick fan-out  │
│   - CurrentPhaseIndex (0..3)                      - Auto phase: fire 1    │
│   - Attendees (from retinue)                        storylet per tick     │
│   - PhaseQuality (reserved, unused v1)            - PlayerChoice phase:   │
│   - OnStart / OnPhaseEnter / OnPhaseExit /          suppress auto, query  │
│     OnHourlyTick / Finish                           PhaseChoiceRegistry   │
│        │                                                                  │
│        ▼                                                                  │
│   PhaseChoiceRegistry (Spec 1, generic)  ──►  HomeEveningMenuProvider     │
│        │                                                                  │
│        ▼                                                                  │
│   EnlistedMenuBehavior.enlisted_camp_hub render path                      │
│   (one hook: append active-phase choice options to accordion)             │
│                                                                           │
│ CampaignEvents.SettlementLeft  ──►  HomeActivityStarter.Finish(Completed) │
└───────────────────────────────────────────────────────────────────────────┘
```

### 5.1 New files

| File | LOC estimate | Role |
| :--- | ---: | :--- |
| `src/Features/Activities/Home/HomeActivity.cs` | 150 | `Activity` subclass, save offset **45** |
| `src/Features/Activities/Home/HomeActivityStarter.cs` | 120 | `CampaignBehaviorBase`, lifecycle detection |
| `src/Features/Activities/Home/HomeEveningMenuProvider.cs` | 80 | `IPhaseChoiceProvider` impl |
| `src/Features/Activities/Home/HomeTriggers.cs` | 200 | Engine-state predicate registration (~25 triggers) |
| `src/Features/Activities/IPhaseChoiceProvider.cs` | 20 | Generic interface for Specs 1-5 |
| `src/Features/Activities/PhaseChoiceOption.cs` | 30 | Plain DTO: `Id`, `Label`, `Tooltip`, `IsEnabled` |
| `src/Features/Activities/PhaseChoiceRegistry.cs` | 50 | Static registry, `Register(string phaseId, IPhaseChoiceProvider)` |
| **Total new C#** | **650** | |

### 5.2 Modified files

| File | Change | LOC delta |
| :--- | :--- | ---: |
| `src/Features/Activities/ActivityRuntime.cs` | Implement `PhaseDelivery.PlayerChoice` branch: on phase enter, query `PhaseChoiceRegistry`, suppress auto-fire, wait for `ResolvePlayerChoice(activity, optionId)` call from menu | +80 |
| `src/Features/Activities/Activity.cs` | Add virtual `OnPlayerChoice(string optionId, ActivityContext ctx)` | +15 |
| `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` | In `enlisted_camp_hub` render, after existing options, query `PhaseChoiceRegistry.GetActiveProvider()` and append options | +25 |
| `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` | `DefineClassType(typeof(HomeActivity))` at offset 45 | +2 |
| `Enlisted.csproj` | Register 7 new `.cs` files | +7 |

### 5.3 Deletions

| File | Reason |
| :--- | :--- |
| `src/Features/Content/OrchestratorOverride.cs` | Unwired pre-Spec-0 scaffolding; storylet weights + triggers subsume it |
| `ModuleData/Enlisted/Config/orchestrator_overrides.json` | Same |

`ContentOrchestrator` usage will be grep-audited during implementation; any referencing code is either dead (delete) or needs to be re-expressed as a storylet trigger (rewrite). Surface specs 2-5 are free to revisit this if they need the "replace entire phase" semantic.

### 5.4 New content (ModuleData)

| Path | Count | Notes |
| :--- | ---: | :--- |
| `ModuleData/Enlisted/Activities/home_activity.json` | 1 | `ActivityTypeDefinition` |
| `ModuleData/Enlisted/Storylets/home_arrive/*.json` | 8 | Auto phase |
| `ModuleData/Enlisted/Storylets/home_settle/*.json` | 12 | Auto phase |
| `ModuleData/Enlisted/Storylets/home_evening_unwind/*.json` | 5 | PlayerChoice phase, unwind pool |
| `ModuleData/Enlisted/Storylets/home_evening_train_hard/*.json` | 5 | PlayerChoice phase, train_hard pool |
| `ModuleData/Enlisted/Storylets/home_evening_brood/*.json` | 5 | PlayerChoice phase, brood pool |
| `ModuleData/Enlisted/Storylets/home_evening_commune/*.json` | 5 | PlayerChoice phase, commune pool |
| `ModuleData/Enlisted/Storylets/home_evening_scheme/*.json` | 5 | PlayerChoice phase, scheme pool (3 merc-gated + 2 universal) |
| `ModuleData/Enlisted/Storylets/home_break_camp/*.json` | 5 | Auto phase |
| `ModuleData/Enlisted/Localization/strings_home.xml` | 1 | All storylet `{=id}` entries |

## 6. `HomeActivity`

### 6.1 Class shape

```csharp
[SaveableClass(offset = 45)]
public sealed class HomeActivity : Activity
{
    [SaveableProperty(1)] public string StartedAtSettlementId { get; set; }
    [SaveableProperty(2)] public CampaignTime PhaseStartedAt { get; set; }
    [SaveableProperty(3)] public bool DigestReleased { get; set; }
    [SaveableProperty(4)] public bool EveningFired { get; set; }

    public override void OnStart(ActivityContext ctx) { /* pick Intent, enter arrive */ }
    public override void OnPhaseEnter(Phase phase) { /* beat hooks per phase */ }
    public override void OnPhaseExit(Phase phase) { }
    public override void Tick(bool hourly) { /* advance phase by beat */ }
    public override void OnPlayerChoice(string optionId, ActivityContext ctx) { /* set Intent, fire */ }
    public override void Finish(ActivityEndReason reason) { /* cleanup */ }
}
```

`HomeActivity` does **not** reimplement phase-pool selection or storylet weighting — those live in `ActivityRuntime` + `StoryletSelector` from Spec 0. It only owns the lifecycle + state the Director and menu need.

### 6.2 Phase model

Defined in `home_activity.json`:

```json
{
  "id": "home_activity",
  "phases": [
    {
      "id": "arrive",
      "duration_hours": 1,
      "delivery": "auto",
      "pool": "home_arrive/*",
      "intent_bias": { "brood": 1.2, "unwind": 1.0, "commune": 1.3, "train_hard": 0.8, "scheme": 0.9 },
      "on_enter_beats": ["SettlementEntered"]
    },
    {
      "id": "settle",
      "duration_hours": 4,
      "delivery": "auto",
      "fire_interval_hours": 2,
      "pool": "home_settle/*",
      "intent_bias": { "brood": 1.0, "unwind": 1.0, "commune": 1.0, "train_hard": 1.1, "scheme": 1.0 }
    },
    {
      "id": "evening",
      "delivery": "player_choice",
      "duration_hours": 0,
      "intents": ["unwind", "train_hard", "brood", "commune", "scheme"],
      "advance_on": "player_choice_resolved"
    },
    {
      "id": "break_camp",
      "delivery": "auto",
      "trigger_on_beat": "settlement_departure_imminent",
      "pool": "home_break_camp/*"
    }
  ]
}
```

### 6.3 Lifecycle

1. **Start.** `HomeActivityStarter` (§7) hooks `CampaignEvents.SettlementEntered` (citation: `CampaignEvents.cs` — the event fires after `EnterSettlementAction.ApplyForParty` at `EnterSettlementAction.cs:71`). Starter checks:
   - `EnlistmentBehavior.Instance?.IsEnlisted == true`
   - `settlement.MapFaction == EnlistmentBehavior.Instance.EnlistedLord.MapFaction` (friendly to lord's kingdom)
   - Party's `CurrentSettlement` matches
   - No active `HomeActivity` already
   - Settlement is town/castle/village (not hideout)

   On pass, it calls `ActivityRuntime.Start(new HomeActivity { Intent = PickInitialIntent(ctx) }, ctx)`.

2. **Initial Intent selection.** The starter reads lord state and picks one of the five. Rules:
   - `Hero.IsWounded == true` → `brood` (weight 3)
   - `MobileParty.Morale < 40` → `brood` (weight 2) or `unwind` (weight 2)
   - `Hero.GetTraitLevel(DefaultTraits.Valor) >= 1` → `train_hard` (weight 2)
   - `Hero.GetTraitLevel(DefaultTraits.Calculating) >= 1 AND clan.IsUnderMercenaryService` → `scheme` (weight 2)
   - `lord_at_home_settlement AND Hero.Spouse != null` → `commune` (weight 3)
   - Default → `unwind` (weight 1)

   Weighted pick; player's Evening choice overrides. The initial Intent matters for **Arrive + Settle** phase biasing; by Evening the player takes the wheel.

3. **Arrive phase.** `OnPhaseEnter(arrive)` releases the news-from-the-road digest if applicable (per pacing spec §4.3 — two in-game days accumulated). Then the phase auto-fires one storylet from `home_arrive/*` pool, weighted by `intent_bias[Intent]` + each storylet's own triggers + base weight.

4. **Settle phase.** `OnPhaseEnter(settle)` starts a 4-hour auto-phase window firing one storylet every 2 in-game hours (up to 2 fires total). Content is ambient / observational; player does not interact.

5. **Evening phase.** `OnPhaseEnter(evening)`:
   - Suppresses auto-fire.
   - Calls `PhaseChoiceRegistry.GetProvider("home_evening")` → `HomeEveningMenuProvider`.
   - Provider scans each intent's pool for at least one eligible storylet; intents with zero eligible storylets render disabled (grey tooltip: "nothing here right now").
   - Five menu lines appear in `enlisted_camp_hub`.
   - Player clicks one. `HomeActivity.OnPlayerChoice(intentId, ctx)` runs:
     - Sets `Intent = intentId` (overrides starter's initial pick).
     - Sets `EveningFired = true`.
     - Calls `StoryletSelector.Pick(pool = home_evening_{intentId}, bias = 1.0, ctx)` → one storylet.
     - Storylet fires as `StoryTier.Modal` via `StoryDirector.EmitCandidate(...)` with `ChainContinuation = true` (player-opted → bypass in-game floor).
   - Modal resolves → player option selection → `EffectExecutor` runs outcomes → phase advances.

6. **Break Camp phase.** Triggered not by time but by beat: `SettlementLeft` imminent (the mod already uses a "party about to march" detection in `EnlistmentBehavior`; `HomeActivityStarter` listens to `CampaignEvents.OnSettlementLeftEvent`). Auto-fires one `home_break_camp/*` storylet. Then `HomeActivity.Finish(ActivityEndReason.Completed)`.

7. **Multi-day stays.** If the lord lingers beyond one night, the activity does **not** auto-re-fire Evening. Instead, `Tick(hourly)` checks: if 24 in-game hours have elapsed since `EveningFired` was set, clear `EveningFired` and re-enter Evening phase. So a three-night stay produces three Evening choices, one per night.

8. **Short stops.** If `SettlementLeft` fires < 3 in-game hours after `SettlementEntered`, Arrive + Settle may have run (or may not), and Break Camp fires but Evening does not. This is intended — a pit stop isn't a camp night.

9. **Interruption.** If the activity is forcibly ended (lord captured mid-stay, settlement falls to siege, player retires), `Finish(ActivityEndReason.Interrupted)` runs; no Break Camp storylet.

### 6.4 SaveableProperty registration

```csharp
// src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs — add at offset 45:
DefineClassType(typeof(HomeActivity));  // offset 45 — Spec 1
```

No new enum types. `Intent` remains a `string` field on `Activity` (Spec 0 choice); validation happens at `ActivityTypeDefinition` load time against the declared `intents` array.

## 7. `HomeActivityStarter`

```csharp
public sealed class HomeActivityStarter : CampaignBehaviorBase
{
    public override void RegisterEvents()
    {
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
    }

    public override void SyncData(IDataStore store) { /* no instance state */ }

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (!ShouldStart(party, settlement)) { return; }
        var ctx = ActivityContext.FromCurrent();
        var activity = new HomeActivity { Intent = PickInitialIntent(ctx) };
        ActivityRuntime.Instance?.Start(activity, ctx);
    }

    private void OnSettlementLeft(MobileParty party, Settlement settlement)
    {
        var home = ActivityRuntime.Instance?.FindActive<HomeActivity>();
        if (home == null) { return; }
        home.Finish(ActivityEndReason.Completed);
    }

    private bool ShouldStart(MobileParty party, Settlement settlement) { /* §6.3 rules */ }
    private string PickInitialIntent(ActivityContext ctx) { /* §6.3 weighted pick */ }
}
```

Registration: `EnlistedCampaignStarter` adds `HomeActivityStarter` alongside existing behaviors. No new entry point; the mod's `SubModule.OnCampaignStart` already iterates `CampaignBehaviorBase` types.

Surfaced errors use `ModLogger.Surfaced("HOME-ACTIVITY", ...)` for genuine failures (e.g. lord null at fire time); `ModLogger.Caught` wraps Harmony-adjacent code; `ModLogger.Expected("HOME-ACTIVITY", key, ...)` flags known early exits (not enlisted, not friendly).

## 8. `PhaseDelivery.PlayerChoice` runtime

Spec 0 declared the enum value; Spec 1 implements it.

### 8.1 Interface

```csharp
public interface IPhaseChoiceProvider
{
    /// Called when the phase becomes active. Return the options to surface.
    /// Empty list is valid (phase auto-advances after a grace tick).
    IReadOnlyList<PhaseChoiceOption> GetOptionsFor(Activity activity, Phase phase);

    /// Called when the player selects an option.
    void OnChoiceSelected(Activity activity, Phase phase, string optionId);
}

public sealed class PhaseChoiceOption
{
    public string Id { get; set; }           // "unwind", "train_hard", ...
    public TextObject Label { get; set; }    // localized
    public TextObject Tooltip { get; set; }  // localized, <80 chars
    public bool IsEnabled { get; set; }
    public TextObject DisabledReason { get; set; }
}
```

### 8.2 Registry

```csharp
public static class PhaseChoiceRegistry
{
    public static void Register(string phaseId, IPhaseChoiceProvider provider);
    public static IPhaseChoiceProvider GetProvider(string phaseId);
    public static IPhaseChoiceProvider GetActiveProvider(); // the current active PlayerChoice phase, if any
}
```

Registration at boot: `HomeEveningMenuProvider.Register()` runs from `SubModule.OnCampaignStart` after Spec 0 initialization.

### 8.3 `ActivityRuntime` changes

```csharp
// ActivityRuntime.cs — in OnPhaseEnter switch:
switch (phase.Delivery)
{
    case PhaseDelivery.Auto:
        TryFireAutoPhaseStorylet(activity, phase, ctx);
        break;

    case PhaseDelivery.PlayerChoice:
        // Do NOT auto-fire. Register this as the active-choice phase.
        _activeChoicePhase = (activity, phase);
        var provider = PhaseChoiceRegistry.GetProvider(phase.Id);
        if (provider == null)
        {
            ModLogger.Surfaced("ACTIVITY", "no_choice_provider",
                new Exception($"No IPhaseChoiceProvider registered for phase {phase.Id}"));
            AdvancePhase(activity);  // safety fallback
            return;
        }
        // Provider's GetOptionsFor is called lazily by the menu renderer.
        break;
}

// New method — invoked by HomeEveningMenuProvider.OnChoiceSelected:
public void ResolvePlayerChoice(Activity activity, Phase phase, string optionId, ActivityContext ctx)
{
    activity.OnPlayerChoice(optionId, ctx);
    _activeChoicePhase = null;
    AdvancePhase(activity);
}
```

### 8.4 Menu renderer hook

In `EnlistedMenuBehavior`, inside the `enlisted_camp_hub` render path:

```csharp
// EnlistedMenuBehavior.cs — one surgical insertion in the camp_hub accordion builder:
var activeProvider = PhaseChoiceRegistry.GetActiveProvider();
if (activeProvider != null)
{
    var (activity, phase) = ActivityRuntime.Instance.GetActiveChoicePhase();
    foreach (var opt in activeProvider.GetOptionsFor(activity, phase))
    {
        AddGameMenuOption(
            menu: "enlisted_status",
            id: $"enlisted_camp_hub_choice_{opt.Id}",
            text: opt.Label,
            condition: (args) => { args.Tooltip = opt.Tooltip; args.IsEnabled = opt.IsEnabled; return true; },
            consequence: (args) => activeProvider.OnChoiceSelected(activity, phase, opt.Id)
        );
    }
}
```

This is the **one and only** extension to `EnlistedMenuBehavior` in Spec 1. The menu otherwise stays untouched. Options append *after* existing camp_hub contents so the accordion grows at the bottom.

## 9. `HomeEveningMenuProvider`

```csharp
public sealed class HomeEveningMenuProvider : IPhaseChoiceProvider
{
    private static readonly string[] Intents =
        { "unwind", "train_hard", "brood", "commune", "scheme" };

    public IReadOnlyList<PhaseChoiceOption> GetOptionsFor(Activity activity, Phase phase)
    {
        var options = new List<PhaseChoiceOption>(5);
        foreach (var intent in Intents)
        {
            var pool = LoadPool($"home_evening_{intent}");
            var eligible = pool.Where(s => TriggerRegistry.Eval(s.Trigger, activity.Context)).ToList();
            options.Add(new PhaseChoiceOption
            {
                Id = intent,
                Label = new TextObject($"{{=home_evening_{intent}_label}}{IntentDefaultLabel(intent)}"),
                Tooltip = BuildTooltip(intent, eligible.Count),
                IsEnabled = eligible.Count > 0,
                DisabledReason = eligible.Count == 0
                    ? new TextObject("{=home_evening_nothing}There's nothing here tonight.")
                    : TextObject.Empty
            });
        }
        return options;
    }

    public void OnChoiceSelected(Activity activity, Phase phase, string optionId)
    {
        activity.Intent = optionId;
        var storylet = StoryletSelector.Pick($"home_evening_{optionId}", activity.Context);
        if (storylet == null)
        {
            ModLogger.Surfaced("HOME-EVENING", "no_eligible_storylet",
                new Exception($"Intent {optionId} reported eligible at render, none at fire"));
            ActivityRuntime.Instance.ResolvePlayerChoice(activity, phase, optionId, activity.Context);
            return;
        }
        // Fire as Modal, ChainContinuation=true (player-opted → bypass in-game floor per pacing §6.2).
        StoryDirector.Instance?.EmitCandidate(storylet.ToCandidate(activity.Context,
            chainContinuation: true, proposedTier: StoryTier.Modal));
        ActivityRuntime.Instance.ResolvePlayerChoice(activity, phase, optionId, activity.Context);
    }
}
```

The `scheme` intent's tooltip carries a warning for vassal-path players:

```csharp
private TextObject BuildTooltip(string intent, int eligibleCount)
{
    if (intent == "scheme"
        && EnlistmentBehavior.Instance?.EnlistedLord?.Clan?.IsUnderMercenaryService != true)
    {
        return new TextObject("{=home_scheme_tooltip_vassal}Risky under an honour-bound lord. Scrutiny may rise.");
    }
    // ... other intent tooltips ...
}
```

## 10. Engine-state triggers (`HomeTriggers`)

Registered at boot via `TriggerRegistry.Register(name, predicate)`. Each predicate takes an `ActivityContext` and returns `bool`. All read live engine state, none cache to `QualityStore`.

| Trigger | Reads | Engine citation |
| :--- | :--- | :--- |
| `party_food_lte_3_days` | `MobileParty.GetNumDaysForFoodToLast()` ≤ 3 | `MobileParty.cs:3231` |
| `party_food_lte_5_days` | Same, ≤ 5 | Same |
| `party_food_starving` | `MobileParty.Food < 1` | `MobileParty.cs:1089` |
| `lord_morale_lte_30` | `MobileParty.Morale` ≤ 30 | `MobileParty.cs:878` |
| `lord_morale_lte_50` | ≤ 50 | Same |
| `lord_morale_lte_70` | ≤ 70 | Same |
| `lord_morale_gte_80` | ≥ 80 | Same |
| `lord_is_wounded` | `Hero.IsWounded` | `Hero.cs:329` |
| `lord_is_disorganized` | `MobileParty.IsDisorganized` | `MobileParty.cs:416` |
| `lord_at_home_settlement` | `Hero.HomeSettlement == party.CurrentSettlement` | `Hero.cs:709` |
| `lord_has_spouse` | `Hero.Spouse != null` | `Hero.cs:818` |
| `lord_has_children` | `Hero.Children.Count > 0` | `Hero.cs:843` |
| `lord_trait_mercy_positive` | `GetTraitLevel(DefaultTraits.Mercy) >= 1` | `Hero.cs:1403` |
| `lord_trait_mercy_negative` | `GetTraitLevel(DefaultTraits.Mercy) <= -1` | Same |
| `lord_trait_valor_positive` | Same for Valor | Same |
| `lord_trait_valor_negative` | Same for Valor | Same |
| `lord_trait_honor_positive` | Same for Honor | Same |
| `lord_trait_honor_negative` | Same for Honor | Same |
| `lord_trait_calculating_positive` | Same for Calculating | Same |
| `lord_trait_calculating_negative` | Same for Calculating | Same |
| `enlisted_lord_is_mercenary` | `clan.IsUnderMercenaryService` | `Clan.cs:242` |
| `kingdom_at_war` | `clan.FactionsAtWarWith.Count > 0` | `Clan.cs:402` |
| `kingdom_multi_front_war` | Same, `> 1` | Same |
| `at_town` | `settlement.IsTown` | `Settlement.cs:378` |
| `at_castle` | `settlement.IsCastle` | `Settlement.cs:390` |
| `at_village` | `settlement.Village != null` | `Settlement.cs` |
| `settlement_under_siege` | `settlement.IsUnderSiege` | `Settlement.cs:466` |
| `settlement_recent_threat` | `(CampaignTime.Now - settlement.LastThreatTime).ToDays < 7` | `Settlement.cs:360` |
| `settlement_low_loyalty` | `town.Loyalty < 40` | `Town.cs:191` |
| `is_night` | `CampaignTime.Now.IsNightTime` | `CampaignTime.cs` |
| `is_autumn` | `CampaignTime.Now.GetSeasonOfYear == 2` | `CampaignTime.cs` |
| `is_winter` | `GetSeasonOfYear == 3` | Same |
| `days_enlisted_gte_30` | `EnlistmentBehavior.DaysEnlisted >= 30` | mod state |
| `days_enlisted_gte_90` | `>= 90` | mod state |
| `recent_company_casualty` | `EnlistmentBehavior.RecentDeaths(days: 3) > 0` | mod state (verify API exists; add if missing) |
| `rank_gte_3` | `EnlistmentBehavior.CurrentTier >= 3` | mod state |

Implementation pattern:

```csharp
public static class HomeTriggers
{
    public static void Register()
    {
        var reg = TriggerRegistry.Instance;
        reg.Register("party_food_lte_3_days", ctx =>
        {
            var party = EnlistmentBehavior.Instance?.EnlistedLord?.PartyBelongedTo;
            if (party == null) { return false; }
            return party.GetNumDaysForFoodToLast() <= 3;
        });
        reg.Register("lord_is_wounded", ctx =>
            EnlistmentBehavior.Instance?.EnlistedLord?.IsWounded == true);
        reg.Register("enlisted_lord_is_mercenary", ctx =>
            EnlistmentBehavior.Instance?.EnlistedLord?.Clan?.IsUnderMercenaryService == true);
        // ... 30+ more
    }
}
```

All predicates are null-safe (follow Critical Rule #5). None throw; boundary failures log via `ModLogger.Expected("HOME-TRIG", name, ...)`.

## 11. `QualityStore` usage

Spec 1 writes to existing qualities only:
- `rank_xp` — Evening storylet outcomes add points.
- `lord_relation` (cached mirror) — some outcomes nudge it.
- `scrutiny` — scheme outcomes and caught-doing-X outcomes add to it.

Spec 1 does **not** introduce new qualities. No `home_readiness`, no `home_supplies`. Engine state covers those axes.

## 12. Storylet authoring

### 12.1 Shape (inherits Spec 0)

Example — `home_evening_brood_dead_friend.json`:

```json
{
  "id": "home_evening_brood_dead_friend",
  "pool": "home_evening_brood",
  "trigger": [
    "recent_company_casualty",
    "days_enlisted_gte_30"
  ],
  "weight": {
    "base": 2.0,
    "modifiers": [
      { "if": "lord_trait_honor_positive", "factor": 1.3 },
      { "if": "is_autumn", "factor": 1.2 }
    ]
  },
  "scope": { "context": "home", "slots": { "veteran": { "role": "dead_squadmate" } } },
  "titleId": "home_brood_empty_cot_title",
  "title": "In the empty cot",
  "setupId": "home_brood_empty_cot_setup",
  "setup": "Seoras had the cot next to yours since Pravend. His kit is still folded at the foot, the way he liked it — the boots turned just so. You sit with it in the dark a while.",
  "options": [
    {
      "id": "take_boots",
      "textId": "home_brood_empty_cot_opt_take",
      "text": "Take the boots. He'd want someone to use them.",
      "tooltipId": "home_brood_empty_cot_opt_take_tt",
      "tooltip": "+1 Roguery. Scrutiny +2 if caught.",
      "effects": [
        { "apply": "skill_xp_minor", "skill": "Roguery", "amount": 20 },
        { "apply": "set_flag", "flag": "took_dead_friend_gear" },
        { "apply": "quality_add", "quality": "scrutiny", "amount": 2, "if": "random_lt:0.3" }
      ]
    },
    {
      "id": "keep_kit",
      "textId": "home_brood_empty_cot_opt_keep",
      "text": "Keep the kit as it is. Someone should know him still.",
      "tooltipId": "home_brood_empty_cot_opt_keep_tt",
      "tooltip": "+2 lord relation if seen. Flag: kept_dead_friend_kit.",
      "effects": [
        { "apply": "lord_relation_up_small" },
        { "apply": "set_flag", "flag": "kept_dead_friend_kit" }
      ]
    },
    {
      "id": "write_ledger",
      "textId": "home_brood_empty_cot_opt_write",
      "text": "Write his name in the book no one asked you to keep.",
      "tooltipId": "home_brood_empty_cot_opt_write_tt",
      "tooltip": "+3 rank_xp. Unlocks a ledger chain.",
      "effects": [
        { "apply": "rank_xp_minor" },
        { "apply": "set_flag", "flag": "keeps_death_ledger" },
        { "apply": "trigger_event", "id": "home_brood_death_ledger_rumored", "days": 14 }
      ]
    }
  ]
}
```

### 12.2 50-storylet breakdown

| Pool | Count | Trigger focus |
| :--- | ---: | :--- |
| `home_arrive` | 8 | Faction-flavored (Empire / Sturgia / Battania / Vlandia / Aserai / Khuzait), home-fief arrival, wounded-lord arrival, post-defeat arrival |
| `home_settle` | 12 | Food-pressure beats (3), wounded-tending (2), stores-anxiety (2), weather/season (2), settlement-state (2 — low loyalty, recent threat), lord-observations (1) |
| `home_evening_unwind` | 5 | Dice with squad, tavern song, drinks with sergeant, firelit story, stars-and-wine |
| `home_evening_train_hard` | 5 | Sparring partner, drill-until-bleeding, form practice with a master-at-arms notable, armor conditioning, technique from a foreign-faction squadmate |
| `home_evening_brood` | 5 | Dead friend's kit (above), lord-like-father reflection, letter home you won't send, cold-comfort of rank, drift (fallback) |
| `home_evening_commune` | 5 | Lord at the hearth, squadmate confession, shared meal with family (if at home-fief), chaplain conversation, old soldier's advice |
| `home_evening_scheme` | 5 | Skim the stores (merc-gated), eavesdrop outside lord's hall (merc-gated), bribe a gatekeeper (merc-gated), trade rumors at the tavern (universal, low-scrutiny), ghost a sentry shift (universal, moderate-scrutiny) |
| `home_break_camp` | 5 | Parting at the gate, lord's mood after a night at home, supply-trains forming, sword-edge-sharpened, rain-on-march (weather dependent) |

Localization: all storylet titles / setups / option texts / tooltips are localized via `{=id}Fallback` pattern and synced by `Tools/Validation/sync_event_strings.py`. Target file: `ModuleData/Enlisted/Localization/strings_home.xml`.

### 12.3 Chains

Four of the 50 storylets park continuation chains:

- `home_brood_dead_friend_take_boots` → `home_chain_boots_rumor_14d` (14 days)
- `home_brood_write_ledger` → `home_chain_ledger_rumored_14d` (14 days)
- `home_commune_lord_at_hearth_speak_plainly` → `home_chain_lord_remembered_30d` (30 days)
- `home_scheme_skim_stores_caught_later` → `home_chain_stores_audited_7d` (7 days)

Chains fire through `StoryDirector.EmitCandidate(chainContinuation: true, ...)` so the 5-day in-game floor does not defer them.

## 13. Testing & verification

### 13.1 Smoke-test debug bindings

Following the Spec 0 pattern (`DebugToolsBehavior.cs`), add hotkeys:

| Key | Action |
| :--- | :--- |
| `Ctrl+Shift+H` | Force-start a `HomeActivity` at the current settlement (ignoring eligibility gates) |
| `Ctrl+Shift+E` | Advance to Evening phase immediately |
| `Ctrl+Shift+B` | Advance to Break Camp and finish |
| `Ctrl+Shift+T` | Dump trigger evaluation table to session log |

These live in the existing `src/Debugging/Behaviors/DebugToolsBehavior.cs` — no new debug entry points.

### 13.2 `validate_content.py` phase

Extend Phase 12 (storylet references) with:
- All `home_*` storylet `pool` fields must exist in `home_activity.json`.
- All triggers referenced must exist in `HomeTriggers` registration.
- All scripted effects referenced must exist in `scripted_effects.json`.
- All `{=id}` tokens must have entries in `strings_home.xml` (sync step).

### 13.3 Acceptance checklist

- [ ] `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds.
- [ ] `python Tools/Validation/validate_content.py` passes with home_* storylets registered.
- [ ] Force-start hotkey enters Arrive phase and fires one storylet in the accordion.
- [ ] Settle phase fires 1-2 storylets over 4 in-game hours.
- [ ] Evening phase surfaces 5 intent options in `enlisted_camp_hub`; disabled options grey-labeled.
- [ ] Selecting an intent fires a modal event via `StoryDirector`.
- [ ] Modal resolution advances to Break Camp; Break Camp storylet fires on departure.
- [ ] Save/load mid-activity restores `CurrentPhaseIndex`, `Intent`, `EveningFired`, `DigestReleased`.
- [ ] No `ModLogger.Error` call appears anywhere in new code (Phase 11 guard).
- [ ] `OrchestratorOverride.cs` + `orchestrator_overrides.json` deleted; `ContentOrchestrator` references resolved or removed.

### 13.4 Regression risk

- **322 KB `EnlistedMenuBehavior`.** The single hook is inserted in the `enlisted_camp_hub` render block. Risk: menu builds from multiple call sites; ensure the hook lives in the *accordion expansion* path, not the top-level refresh. Review against `_ordersCollapsed` / `_decisionsMainMenuCollapsed` patterns before landing.
- **`CompanySupplyManager` coexistence.** The mod's supply-fiction still ticks during HomeActivity. A supply-fiction storylet (`home_settle_stores_short`) and an engine-state-driven food storylet (`home_settle_food_anxiety`) could fire in the same session with contradictory framing. Mitigation: author the engine-state variants with stronger narrative cues so they feel canonical; phase out `CompanySupplyManager`-gated content over subsequent specs.
- **`StoryDirector` pacing floor interactions.** Evening-phase Modals use `ChainContinuation = true` to bypass the in-game 5-day floor (already legitimate per pacing spec §6.2). Arrive + Settle phase auto-fires use normal `ChainContinuation = false`; with the aggregation buffer they should rarely trip the pacing gate because most are accordion-tier, not Modal.

## 14. Migration / rollback

No migration. New code path, new content. If the system needs to be rolled back pre-merge:

1. Revert the feature commits; `HomeActivity` is gone, `PhaseChoiceRegistry` is gone.
2. `OrchestratorOverride.cs` + `orchestrator_overrides.json` deletion is in the same revert; they come back.
3. Save offset 45 returns to unclaimed; saves created during Spec 1 testing will fail to load (acceptable — Spec 1 is unreleased).

Post-merge rollback is handled by the existing save-version guard in `EnlistedSaveDefiner`.

## 15. Hooks for Specs 2-5

Spec 1 establishes three reusable primitives:

1. **`IPhaseChoiceProvider` + `PhaseChoiceRegistry`.** Spec 2 (Orders as Activities) will register `OrderPhaseChoiceProvider` for the march-day decision phases. Spec 4 (Promotion + Muster) will register a muster-choice provider. Same interface, same registry.
2. **Engine-state `TriggerRegistry` pattern.** Spec 2-5 add their own triggers using the same null-safe, boundary-guarded pattern shown in `HomeTriggers`. Triggers are namespaced by prefix (`order_`, `muster_`, `sea_`) to avoid collision.
3. **Activity-as-surface pattern.** `HomeActivity` is the template. Spec 2's `OrderActivity`, Spec 3's `LandActivity` / `SeaActivity`, Spec 4's `MusterActivity`, Spec 5's `QuartermasterActivity` all follow the same lifecycle (starter behavior → `OnStart` → phase progression → `Finish`) and claim offsets 46-50.

## 16. Open questions

- **`EnlistmentBehavior.RecentDeaths(days)`.** The `recent_company_casualty` trigger assumes this API exists. If the mod tracks deaths via another mechanism (retinue system's `LifetimeServiceRecord`?), adapt the predicate at implementation time. If not tracked at all, add a narrow `RecentDeaths` tracker to `EnlistmentBehavior` before home_brood storylets can reference it.
- **Tooltip length.** Critical Rule #7 caps tooltips at <80 chars. The scheme intent's dynamic tooltip ("Risky under an honour-bound lord. Scrutiny may rise.") is 48 chars — fine. Per-storylet option tooltips must be audited during authoring; validator Phase 8 already enforces this.
- **`ContentOrchestrator` dead-code audit.** Deleting `OrchestratorOverride.cs` may leave dead references in `ContentOrchestrator`. Implementation plan must grep for usages and either delete or rewrite as storylet triggers.

## 17. Summary

Spec 1 ships:

- One new `Activity` subclass (`HomeActivity`, offset 45).
- One new generic runtime facility (`IPhaseChoiceProvider` + `PhaseChoiceRegistry` + `ActivityRuntime.ResolvePlayerChoice`).
- One new menu-hook in `EnlistedMenuBehavior` (~25 LOC).
- One new trigger module (`HomeTriggers`, ~30 engine-state predicates).
- 50 home-camp storylets across 4 phases × 5 intents.
- Deletion of `OrchestratorOverride` scaffolding.

Total new C# is ~650 LOC + ~105 LOC of edits. Content authoring is ~12-17 hours. The architecture grounds every mechanical decision in the Bannerlord v1.3.13 API (cited inline), treats the storylet backbone as the canonical authoring substrate, and leaves clean extension points for Specs 2-5.
