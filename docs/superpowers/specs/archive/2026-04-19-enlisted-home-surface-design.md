# Enlisted Home Surface — Design Spec

**Date:** 2026-04-19
**Status:** **Implementation complete 2026-04-20** on `development`. Full commit trail `fc93285` → `0390fdf`. Phases A (substrate, `fc93285`→`24b7e50`), B (HomeActivity + save + triggers + starter, `e4b6806`→`a48983c`), C (menu integration + smoke, `18b4021`→`19391a4`), D (50 storylets + localization, `8b96294`→`d9bcf8b`), E (debug hotkeys + validator + acceptance smoke, `1d0fb52`→`b563834`), plus three post-Phase-E production fixes (`0390fdf`): csproj now mirrors `Activities/` / `Effects/` / `Storylets/` / `Qualities/` via `AfterBuild`, `Ctrl+Shift+T` rebound to `Ctrl+Shift+Y` after decompile audit of `DebugHotKeyCategory`, and `AdvanceHomeToEvening` now calls `ActivityRuntime.ForceAdvancePhase` to cross Auto phases. Task 21 Scenarios A-F verified in-game. Drafted 2026-04-19; revised same day after adversarial review caught six integration gaps (activity-type loader, Storylet→InteractiveEvent adapter, menu slot-bank pattern, party-identity gate, OrchestratorOverride scope, effect schema corrections). Depends on Spec 0 (Storylet Backbone, landed `45b38bf`). Implementation plan: [docs/superpowers/plans/2026-04-19-enlisted-home-surface.md](../../superpowers/plans/2026-04-19-enlisted-home-surface.md).
**Scope:** The first player-experience surface built on the Spec 0 storylet backbone: what the player does when the lord's party is parked at a friendly settlement (town, castle, or village). Defines `HomeActivity`, the `PhaseDelivery.PlayerChoice` runtime, the menu integration, the engine-state trigger palette, and the 50-storylet launch shelf.

This is **Spec 1** in the 6-spec cycle. Spec 0 established the substrate (`QualityStore`, `FlagStore`, `ActivityRuntime`, `Storylet`, `ScriptedEffectRegistry`, `TriggerRegistry`). Spec 1 delivers the first consumer — the "enlisted home" surface — and in doing so ships three pieces of generic plumbing that Specs 2-5 inherit: an `ActivityTypeDefinition` JSON loader, a `Storylet → InteractiveEvent` adapter for Modal player-choice events, and the `PhaseDelivery.PlayerChoice` branch of `ActivityRuntime`.

---

## 1. Problem

Spec 0 shipped infrastructure with no content and no consumer. Three integration gaps made "just author some storylets" impossible:

1. **`ActivityRuntime._types` is empty by construction.** Spec 0's own code comment (`ActivityRuntime.cs:59`) says "*ActivityTypeDefinition JSON loading is a surface-spec concern. Spec 0 leaves _types empty.*" Without a loader, `Activity.ResolvePhasesFromType` (`Activity.cs:46`) no-ops and any new `Activity` has zero phases.

2. **`Storylet.ToCandidate` never populates `InteractiveEvent`** (`Storylet.cs:62-84`). `StoryDirector.Route` gates the Modal path on `c.InteractiveEvent != null` (`StoryDirector.cs:218`); anything without an `InteractiveEvent` goes to the news-feed dispatch path (`:246`). Without an adapter, storylet choices cannot render as Modal events with options/effects/chains.

3. **`PhaseDelivery.PlayerChoice` is declared but not implemented.** `ActivityRuntime.TryFireAutoPhaseStorylet` (`ActivityRuntime.cs:182`) only handles `PhaseDelivery.Auto`. The PlayerChoice branch needs to suppress auto-fire, surface options through the menu, and invoke a resolution callback on selection.

Meanwhile the `enlisted_status` menu (322 KB `EnlistedMenuBehavior.cs`) already owns the home-screen surface, and the native engine exposes rich, daily-ticked state the mod ignores:

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
| `Settlement.IsTown` / `IsCastle` / `Village != null` | `Settlement.cs:378`, `:390` | Settlement type |
| `Settlement.IsUnderSiege` + `LastThreatTime` | `Settlement.cs:466`, `:360` | Regional threat |
| `Town.Prosperity` / `Loyalty` / `Security` | `Town.cs:112`, `:191`, `:171` | Settlement health |
| `CampaignTime.Now.IsNightTime` + `GetSeasonOfYear` | `CampaignTime.cs` | Temporal context |

Spec 1 reads this state directly via `TriggerRegistry` predicates. The mod's `QualityStore` is reserved for genuinely invented axes (`scrutiny`, `rank_xp`, `days_in_rank`, `lord_relation` as a cached mirror). No parallel simulation.

## 2. Goals

- Deliver a felt camp experience at friendly settlements: arrival beat, ambient settle, one nightly player-choice moment, departure beat.
- Five player-facing intents at Evening: `unwind`, `train_hard`, `brood`, `commune`, `scheme`.
- Content pool gated by real engine state, not parallel mod fiction.
- Ship the three generic primitives (`ActivityTypeCatalog`, `StoryletEventAdapter`, `PhaseDelivery.PlayerChoice` runtime) once; Specs 2-5 reuse.
- Respect the established menu contract: menu options register once at `OnSessionLaunched` via `starter.AddGameMenuOption(...)` with condition callbacks gating visibility — mirror the decision-slot pattern at `EnlistedMenuBehavior.cs:1150-1158`.
- Ship 50 storylets at launch; expansion is content-only, no engine work.

## 3. Non-goals

- **No on-road camps.** "The company makes camp on the march road" is a different context — `EnterSettlementAction` does not fire, `Settlement.CurrentSettlement` is null. A future spec covers that. Spec 1 is settlement-home only (`at_town` ∪ `at_castle` ∪ `at_village`).
- **No touching `OrchestratorOverride` / `ContentOrchestrator` / `CampScheduleManager` / `CampRoutineProcessor`.** The initial draft proposed deleting this subsystem as "unwired scaffolding"; adversarial review confirmed it is live and load-bearing (`ContentOrchestrator.cs:1763` → `CampScheduleManager.cs:87` → `CampRoutineProcessor.cs:60`). Spec 1 does not modify it. A future consolidation may replace it with storylet-based expression after Spec 2 ships.
- **No refactor of `CompanySupplyManager` or `CompanyNeedsState`.** Engine-state triggers in Spec 1 bypass them entirely; the mod-fiction numbers remain for surfaces that depend on them (Orders, CampMenuHandler).
- **No rewrite of `EnlistedMenuBehavior`.** Spec 1 adds a preallocated 5-slot option bank (same shape as `enlisted_decision_slot_*`); everything else stays.
- **No new error-output channels.** Use `ModLogger.Surfaced` / `Caught` / `Expected`.
- **No `recent_company_casualty` trigger at launch.** `EnlistmentBehavior` does not currently expose a "deaths in last N days" API. Storylets that need this concept are out of Spec 1 scope; they enter a backlog for a future content pass after the tracker is built.
- **No subdirectory layout under `Storylets/`.** `StoryletCatalog.LoadAll` (`StoryletCatalog.cs:30`) is non-recursive and expects `*.json` files at the top level, each containing a `storylets: [...]` array. Spec 1 respects this: all home content lives in `ModuleData/Enlisted/Storylets/home.json` (or split into `home_arrive.json`, `home_evening.json`, etc., all top-level) as array files.

## 4. Vocabulary

Spec 1 inherits Spec 0's vocabulary and adds:

| Term | Meaning |
| :--- | :--- |
| **HomeActivity** | Concrete `Activity` subclass. Starts on friendly-settlement entry by the enlisted lord's party, ends on the same party's departure. Owns four phases. Save offset **45**. |
| **Intent** (home-specific) | One of `unwind` / `train_hard` / `brood` / `commune` / `scheme`. Player picks at Evening; initial value set by starter based on lord state. |
| **Phase** (home-specific) | `arrive` (auto, 1h) / `settle` (auto, 2h fire interval) / `evening` (PlayerChoice) / `break_camp` (auto, fires on departure). |
| **Evening slot bank** | Five preallocated `AddGameMenuOption` entries on `enlisted_status`, ids `enlisted_evening_intent_slot_0..4`. Condition callbacks query `HomeEveningMenuProvider`; visible only when `HomeActivity` is in the `evening` phase. |
| **Engine-state trigger** | `TriggerRegistry` predicate that reads live engine API. Named with `lord_` / `party_` / `settlement_` / `kingdom_` / `time_` / `at_` prefixes. |

## 5. Architecture

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ CampaignEvents.SettlementEntered (party, settlement, hero)                   │
│   └─► HomeActivityStarter.OnSettlementEntered                                │
│         filters: party == EnlistedLord.PartyBelongedTo,                      │
│                  settlement.MapFaction == EnlistedLord.MapFaction,           │
│                  no active HomeActivity, settlement type in {town,castle,    │
│                  village}                                                    │
│         └─► ActivityRuntime.Start(new HomeActivity { ... }, ctx)             │
│                                                                              │
│ ActivityRuntime                                                              │
│   OnSessionLaunched: ActivityTypeCatalog.LoadAll() → RegisterType(...)       │
│   OnHourlyTick: Activity.Tick(hourly) + phase Auto fire + advance            │
│   OnPhaseEnter(PhaseDelivery.PlayerChoice):                                  │
│     - set _activeChoicePhase = (activity, phase)                             │
│     - suppress auto-fire                                                     │
│     - menu-side slot-bank conditions now return true for this activity       │
│                                                                              │
│ EnlistedMenuBehavior (session launch)                                        │
│   Preallocated slots enlisted_evening_intent_slot_0..4, each:                │
│     condition: HomeEveningMenuProvider.IsSlotActive(i, out label, tooltip)   │
│                (returns false unless Evening phase of a live HomeActivity)   │
│     consequence: HomeEveningMenuProvider.OnSlotSelected(i)                   │
│                                                                              │
│ HomeEveningMenuProvider.OnSlotSelected(i)                                    │
│   - activity.Intent = intent(i)                                              │
│   - Storylet s = StoryletSelector.Pick(pool = home_evening_{intent}, ctx)    │
│   - InteractiveEvent evt = StoryletEventAdapter.Build(s, ctx)                │
│   - candidate.InteractiveEvent = evt, ChainContinuation = true               │
│   - StoryDirector.EmitCandidate(candidate, tier = Modal)                     │
│   - ActivityRuntime.ResolvePlayerChoice(activity) → advance phase            │
│                                                                              │
│ CampaignEvents.OnSettlementLeftEvent (party, settlement)                     │
│   └─► HomeActivityStarter.OnSettlementLeft                                   │
│         filter: party == EnlistedLord.PartyBelongedTo                        │
│         └─► activity.OnBeat("departure_imminent") → fire break_camp storylet │
│         └─► activity.Finish(ActivityEndReason.Completed)                     │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 5.1 New files

| File | LOC est. | Role |
| :--- | ---: | :--- |
| `src/Features/Activities/ActivityTypeCatalog.cs` | 80 | **Generic.** Loads `ModuleData/Enlisted/Activities/*.json`, calls `ActivityRuntime.RegisterType`. Used by Specs 2-5 too. |
| `src/Features/Content/StoryletEventAdapter.cs` | 150 | **Generic.** Builds `InteractiveEvent` from a `Storylet` + `StoryletContext` for Modal PlayerChoice routing. Used by Specs 2-5 too. |
| `src/Features/Activities/Home/HomeActivity.cs` | 180 | `Activity` subclass, save offset **45** |
| `src/Features/Activities/Home/HomeActivityStarter.cs` | 120 | `CampaignBehaviorBase`, lifecycle detection |
| `src/Features/Activities/Home/HomeEveningMenuProvider.cs` | 100 | Queries pool eligibility, binds slot-bank labels, invokes adapter on select |
| `src/Features/Activities/Home/HomeTriggers.cs` | 180 | Engine-state predicate registration (~30 triggers) |
| **Total new C#** | **810** | |

### 5.2 Modified files

| File | Change | LOC delta |
| :--- | :--- | ---: |
| `src/Features/Activities/ActivityRuntime.cs` | Call `ActivityTypeCatalog.LoadAll()` from `OnSessionLaunched`; implement `PhaseDelivery.PlayerChoice` branch in `OnPhaseEnter` (suppress auto-fire, set `_activeChoicePhase`); add `ResolvePlayerChoice(activity)` + `FindActive<T>()` + `GetActiveChoicePhase()` | +75 |
| `src/Features/Activities/Activity.cs` | Add `virtual void OnPlayerChoice(string optionId, ActivityContext ctx) { }`; add `virtual void OnBeat(string beatId, ActivityContext ctx) { }` (empty defaults) | +15 |
| `src/Features/Activities/Phase.cs` | Add `int FireIntervalHours { get; set; }` (0 = once per phase, N = every N hours during phase) | +3 |
| `src/Features/Content/ActivityRuntime.cs` auto-fire branch | Honour `FireIntervalHours` (track `LastFireHour` per activity; skip tick if too recent) | +20 |
| `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` | **Register 5 preallocated evening-slot options at `OnSessionLaunched`** (pattern: `EnlistedMenuBehavior.cs:1150-1158`). Each slot's condition callback queries `HomeEveningMenuProvider.IsSlotActive(i, out label, out tooltip)`; consequence callback invokes `HomeEveningMenuProvider.OnSlotSelected(i)` | +75 |
| `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` | `DefineClassType(typeof(HomeActivity))` at offset 45 | +2 |
| `Enlisted.csproj` | Register 6 new `.cs` files | +6 |

### 5.3 New content (ModuleData)

| Path | Count | Notes |
| :--- | ---: | :--- |
| `ModuleData/Enlisted/Activities/home_activity.json` | 1 | `ActivityTypeDefinition` with 4 phases |
| `ModuleData/Enlisted/Storylets/home_arrive.json` | 1 file, 8 storylets | Array of storylet objects |
| `ModuleData/Enlisted/Storylets/home_settle.json` | 1 file, 12 storylets | |
| `ModuleData/Enlisted/Storylets/home_evening.json` | 1 file, 25 storylets | All five intents pooled in one file |
| `ModuleData/Enlisted/Storylets/home_break_camp.json` | 1 file, 5 storylets | |
| `ModuleData/Enlisted/Localization/strings_home.xml` | 1 | All `{=id}` tokens |

All storylets register to the global `StoryletCatalog` by `id`; phase pools are explicit lists of `id` strings declared in `home_activity.json`.

### 5.4 Deletions

None in Spec 1. `OrchestratorOverride.cs` and `orchestrator_overrides.json` remain — see §3.

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
    [SaveableProperty(5)] public int LastAutoFireHour { get; set; }  // for FireIntervalHours pacing

    public override void OnStart(ActivityContext ctx)
    {
        TypeId = "home_activity";   // critical — empty TypeId leaves Phases unresolved
        StartedAtSettlementId = MobileParty.MainParty?.CurrentSettlement?.StringId ?? "";
        Intent = PickInitialIntent(ctx);  // from starter; can be overwritten at Evening
        CurrentPhaseIndex = 0;            // arrive phase
        PhaseStartedAt = CampaignTime.Now;
    }

    public override void OnPhaseEnter(Phase phase) { /* beat hooks per phase, §6.3 */ }
    public override void OnPhaseExit(Phase phase) { }
    public override void Tick(bool hourly) { /* advance phase by clock + beats, §6.3 */ }
    public override void OnPlayerChoice(string optionId, ActivityContext ctx) { Intent = optionId; EveningFired = true; }
    public override void OnBeat(string beatId, ActivityContext ctx) { /* "departure_imminent" → enter break_camp */ }
    public override void Finish(ActivityEndReason reason) { /* cleanup */ }
}
```

`HomeActivity` must set `TypeId = "home_activity"` in `OnStart` — otherwise `ResolvePhasesFromType` (`Activity.cs:46`) no-ops on load and the activity has zero phases.

### 6.2 Phase model — what lives in JSON vs C#

The Phase class (`Phase.cs:10-18`) supports `Id`, `DurationHours`, `Pool` (List<string> of storylet ids), `IntentBias`, `Delivery`, `PlayerChoiceCount`. Spec 1 adds one field: `FireIntervalHours` (default 0 = fire once per phase; N = fire no more than once per N in-game hours during the phase window).

Everything else — beat hooks, departure-imminent detection, digest release, player-choice resolution — lives in `HomeActivity` C# methods, not in JSON. The JSON is a static template; runtime behavior is code.

**Example — `ModuleData/Enlisted/Activities/home_activity.json`:**

```json
{
  "id": "home_activity",
  "phases": [
    {
      "id": "arrive",
      "duration_hours": 1,
      "fire_interval_hours": 0,
      "delivery": "auto",
      "pool": [
        "home_arrive_lord_wounded_returning",
        "home_arrive_home_fief",
        "home_arrive_grim_mood",
        "home_arrive_faction_empire",
        "home_arrive_faction_sturgia",
        "home_arrive_faction_battania",
        "home_arrive_faction_vlandia",
        "home_arrive_post_defeat"
      ],
      "intent_bias": { "brood": 1.2, "unwind": 1.0, "commune": 1.3, "train_hard": 0.8, "scheme": 0.9 }
    },
    {
      "id": "settle",
      "duration_hours": 4,
      "fire_interval_hours": 2,
      "delivery": "auto",
      "pool": [
        "home_settle_food_anxiety_3d",
        "home_settle_food_anxiety_5d",
        "home_settle_wounded_tend",
        "home_settle_stores_short",
        "home_settle_weather_autumn",
        "home_settle_weather_winter",
        "home_settle_low_loyalty_town",
        "home_settle_recent_threat",
        "home_settle_lord_observation_mercy",
        "home_settle_lord_observation_valor",
        "home_settle_chaplain_passes",
        "home_settle_siege_preparations"
      ],
      "intent_bias": { "brood": 1.0, "unwind": 1.0, "commune": 1.0, "train_hard": 1.1, "scheme": 1.0 }
    },
    {
      "id": "evening",
      "duration_hours": 0,
      "fire_interval_hours": 0,
      "delivery": "player_choice",
      "pool": [
        "home_evening_unwind_dice", "home_evening_unwind_song", "home_evening_unwind_sergeant", "home_evening_unwind_firelit", "home_evening_unwind_stars",
        "home_evening_train_hard_sparring", "home_evening_train_hard_drill", "home_evening_train_hard_master", "home_evening_train_hard_armor", "home_evening_train_hard_foreign_form",
        "home_evening_brood_dead_friend", "home_evening_brood_lord_like_father", "home_evening_brood_letter_home", "home_evening_brood_cold_comfort", "home_evening_brood_drift",
        "home_evening_commune_lord_at_hearth", "home_evening_commune_squadmate_confession", "home_evening_commune_family_meal", "home_evening_commune_chaplain", "home_evening_commune_old_soldier",
        "home_evening_scheme_skim_stores", "home_evening_scheme_eavesdrop_hall", "home_evening_scheme_bribe_gate", "home_evening_scheme_tavern_rumors", "home_evening_scheme_ghost_sentry"
      ]
    },
    {
      "id": "break_camp",
      "duration_hours": 1,
      "fire_interval_hours": 0,
      "delivery": "auto",
      "pool": [
        "home_break_camp_parting_gate",
        "home_break_camp_lord_mood_after_home",
        "home_break_camp_supply_trains",
        "home_break_camp_sword_edge",
        "home_break_camp_rain_on_march"
      ]
    }
  ]
}
```

Note: the Evening pool contains all 25 intent-specific storylets. `HomeEveningMenuProvider` filters them down to the 5 per intent at selection time by storylet-id prefix match (`home_evening_{intent}_*`).

### 6.3 Lifecycle details

1. **Start detection** (`HomeActivityStarter.OnSettlementEntered`, subscribed to `CampaignEvents.SettlementEntered`):
   ```csharp
   private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
   {
       var lord = EnlistmentBehavior.Instance?.EnlistedLord;
       if (lord?.PartyBelongedTo == null) { return; }
       if (party != lord.PartyBelongedTo) { return; }           // identity gate (§4-finding fix)
       if (settlement.MapFaction != lord.MapFaction) { return; } // friendly gate
       if (settlement.IsHideout) { return; }
       if (!(settlement.IsTown || settlement.IsCastle || settlement.Village != null)) { return; }
       if (ActivityRuntime.Instance?.FindActive<HomeActivity>() != null) { return; }
       // proceed to start
   }
   ```

2. **Initial Intent selection.** Weighted-random from five candidates, read from engine state:
   - `lord.IsWounded` → weight `brood +3`
   - `party.Morale < 40` → weight `brood +2`, `unwind +2`
   - `lord.GetTraitLevel(DefaultTraits.Valor) >= 1` → weight `train_hard +2`
   - `lord.GetTraitLevel(DefaultTraits.Calculating) >= 1 AND lord.Clan.IsUnderMercenaryService` → weight `scheme +2`
   - `lord.HomeSettlement == settlement AND lord.Spouse != null` → weight `commune +3`
   - Default `unwind +1`

   Player's Evening choice overrides; initial Intent biases Arrive + Settle only.

3. **Arrive phase.** Auto-fires one storylet per phase (FireIntervalHours=0 + DurationHours=1). On `OnPhaseEnter`:
   - If `PacingBuffer.DaysOnRoad >= 2`, release the aggregated digest via `StoryDirector.ReleaseDigest(beat: "SettlementEntered")` per pacing spec §4.3. Set `DigestReleased = true`.
   - `ActivityRuntime.TryFireAutoPhaseStorylet` runs weighted pick on next hourly tick.

4. **Settle phase.** `FireIntervalHours = 2`, `DurationHours = 4` → up to 2 auto-fires. No player interaction.

5. **Evening phase (`PhaseDelivery.PlayerChoice`).** On `OnPhaseEnter`:
   - `ActivityRuntime` sets `_activeChoicePhase = (activity, phase)` and skips `TryFireAutoPhaseStorylet`.
   - `HomeEveningMenuProvider.SnapshotEligibility(ctx)` scans each intent's 5 storylets for at least one whose triggers pass. Result cached on the activity for the duration of this Evening so menu re-renders are stable.
   - Menu slot-bank conditions (§8.4) return true for slots whose intent has ≥1 eligible storylet; false otherwise (grey-disabled with a tooltip reason).
   - Player selects a slot. `HomeEveningMenuProvider.OnSlotSelected(i)`:
     - Sets `activity.Intent = intent(i)` and `activity.EveningFired = true`.
     - Calls `StoryletSelector.Pick` restricted to `home_evening_{intent}_*` storylets.
     - Builds `InteractiveEvent` via `StoryletEventAdapter` (§7).
     - Builds `StoryCandidate`, sets `InteractiveEvent`, `ProposedTier = Modal`, `ChainContinuation = true` (player-opted bypass per pacing spec §6.2).
     - `StoryDirector.Instance?.EmitCandidate(candidate)`.
     - `ActivityRuntime.Instance.ResolvePlayerChoice(activity)` → advances to `break_camp` phase pending departure beat.

6. **Break Camp phase.** Triggered by `HomeActivityStarter.OnSettlementLeft` (party-identity filtered):
   ```csharp
   private void OnSettlementLeft(MobileParty party, Settlement settlement)
   {
       var lord = EnlistmentBehavior.Instance?.EnlistedLord;
       if (party != lord?.PartyBelongedTo) { return; }
       var home = ActivityRuntime.Instance?.FindActive<HomeActivity>();
       if (home == null) { return; }
       home.OnBeat("departure_imminent", ActivityContext.FromCurrent());
       home.Finish(ActivityEndReason.Completed);
   }
   ```
   `HomeActivity.OnBeat("departure_imminent", ctx)` enters the `break_camp` phase, which auto-fires one storylet.

7. **Multi-day stays.** `HomeActivity.Tick(hourly: true)` checks: if phase is `evening` AND `EveningFired == true` AND current in-game hour is past next-night dawn, clear `EveningFired` and re-enter Evening phase. Three-night stay → three Evening choices.

8. **Short stops.** If `SettlementLeft` fires < 3 in-game hours after entry, the runtime jumps directly to `break_camp` (bypassing any unfired Evening). Pit stops don't get a camp night.

9. **Interruption.** Lord captured mid-stay / settlement falls to siege / player retires → `Finish(ActivityEndReason.Interrupted)`; no break-camp storylet.

## 7. `StoryletEventAdapter` (generic)

### 7.1 Problem

`StoryDirector.Route` (`StoryDirector.cs:218`) requires `c.InteractiveEvent != null` to take the Modal path; otherwise writes a dispatch item to the news feed (`:246`). `Storylet.ToCandidate` (`Storylet.cs:62-84`) does not populate `InteractiveEvent`. Without an adapter, player-choice storylets cannot render as Modal events.

### 7.2 Shape

```csharp
public static class StoryletEventAdapter
{
    /// Builds an InteractiveEvent backed by InquiryData with one button per eligible Choice.
    /// Applies Choice.Effects on selection; parks Choice.Chain via ActivityRuntime.ParkChain.
    public static InteractiveEvent BuildModal(Storylet s, StoryletContext ctx, Activity owner)
    {
        var inquiry = new MultiSelectionInquiryData(
            titleText: s.Title,         // "{=id}Fallback" form, resolved by MBTextManager
            descriptionText: s.Setup,
            inquiryElements: s.Options
                .Where(o => TriggerRegistry.Evaluate(o.Trigger, ctx))  // option-level eligibility
                .Select(o => new InquiryElement(o, o.Text, null, true, o.Tooltip))
                .ToList(),
            isExitShown: false, exitString: "",
            minSelectableOptionCount: 1, maxSelectableOptionCount: 1,
            affirmativeAction: selected =>
            {
                var choice = (Choice)selected[0].Identifier;
                EffectExecutor.Apply(choice.Effects, ctx);
                if (choice.Chain != null && !string.IsNullOrEmpty(choice.Chain.Id))
                {
                    var (typeId, phaseId) = ParseWhen(choice.Chain.When, fallbackTypeId: owner.TypeId, fallbackPhaseId: "evening");
                    ActivityRuntime.Instance?.ParkChain(typeId, phaseId, choice.Chain.Id);
                }
            },
            negativeAction: null
        );
        return new InteractiveEvent(inquiry, s.Title, s.Setup);  // mod's InteractiveEvent wrapper
    }

    private static (string typeId, string phaseId) ParseWhen(string when, string fallbackTypeId, string fallbackPhaseId)
    {
        // "home_activity.evening" → ("home_activity", "evening"); empty → fallbacks
        if (string.IsNullOrEmpty(when)) { return (fallbackTypeId, fallbackPhaseId); }
        var parts = when.Split('.');
        return parts.Length == 2 ? (parts[0], parts[1]) : (fallbackTypeId, fallbackPhaseId);
    }
}
```

Chain delay semantics: `Choice.Chain.Days` is **not** honored by Spec 1. `ActivityRuntime.ParkChain` fires parked storylets on next phase-entry match, not on a real-time timer. Chains fire the next time the target activity enters the target phase — hours later for a multi-day stay, days later for a new campaign stop. Authors express elapsed-time framing in chain-storylet prose ("some time later…"), not via day counts. A future spec may add real time-delayed chains; Spec 1 uses the existing `ParkChain` semantics unchanged.

### 7.3 `InteractiveEvent` wrapper

The mod already has an `InteractiveEvent` wrapper used by `EventDeliveryManager.QueueEvent(...)`. The adapter reuses that wrapper; no new Gauntlet UI. Audit during implementation to confirm the wrapper's constructor signature matches.

## 8. `PhaseDelivery.PlayerChoice` runtime + menu slot-bank

### 8.1 `ActivityRuntime` changes

```csharp
// ActivityRuntime.cs — track the single active choice phase:
private (Activity activity, Phase phase)? _activeChoicePhase;

public (Activity activity, Phase phase)? GetActiveChoicePhase() => _activeChoicePhase;

public T FindActive<T>() where T : Activity =>
    _active.OfType<T>().FirstOrDefault();

// In TryFireAutoPhaseStorylet, add an early return for PlayerChoice phases:
if (phase.Delivery != PhaseDelivery.Auto) { return; }

// New: called by HomeEveningMenuProvider after an option is picked:
public void ResolvePlayerChoice(Activity activity)
{
    if (_activeChoicePhase?.activity != activity) { return; }
    _activeChoicePhase = null;
    AdvancePhase(activity);
}
```

### 8.2 `OnPhaseEnter` hook for PlayerChoice

```csharp
// In ActivityRuntime.Start and phase-advance paths, after OnPhaseEnter:
if (activity.CurrentPhase?.Delivery == PhaseDelivery.PlayerChoice)
{
    _activeChoicePhase = (activity, activity.CurrentPhase);
}
```

### 8.3 `HomeEveningMenuProvider`

```csharp
public static class HomeEveningMenuProvider
{
    private static readonly string[] Intents = { "unwind", "train_hard", "brood", "commune", "scheme" };

    /// Called by slot condition callbacks. Returns true if slot i should be visible.
    public static bool IsSlotActive(int i, out TextObject label, out TextObject tooltip)
    {
        label = TextObject.Empty; tooltip = TextObject.Empty;
        var runtime = ActivityRuntime.Instance;
        var active = runtime?.GetActiveChoicePhase();
        if (active == null || !(active.Value.activity is HomeActivity home)) { return false; }
        if (active.Value.phase.Id != "evening") { return false; }
        if (i < 0 || i >= Intents.Length) { return false; }

        var intent = Intents[i];
        var pool = home.CurrentPhase.Pool.Where(id => id.StartsWith($"home_evening_{intent}_")).ToList();
        var ctx = new StoryletContext { ActivityTypeId = home.TypeId, PhaseId = "evening", CurrentContext = "settlement" };
        var anyEligible = pool.Any(id =>
            StoryletCatalog.GetById(id) is { } s && s.IsEligible(ctx));

        label = new TextObject($"{{=home_evening_{intent}_label}}{DefaultLabel(intent)}");
        tooltip = BuildTooltip(intent, anyEligible);
        return anyEligible;  // slot visible only when at least one storylet is eligible
    }

    public static void OnSlotSelected(int i)
    {
        var runtime = ActivityRuntime.Instance;
        var active = runtime?.GetActiveChoicePhase();
        if (active == null || !(active.Value.activity is HomeActivity home)) { return; }
        var intent = Intents[i];
        home.OnPlayerChoice(intent, ActivityContext.FromCurrent());

        var ctx = new StoryletContext { ActivityTypeId = home.TypeId, PhaseId = "evening", CurrentContext = "settlement" };
        var pool = home.CurrentPhase.Pool.Where(id => id.StartsWith($"home_evening_{intent}_")).ToList();
        var picked = StoryletSelector.Pick(pool, ctx);
        if (picked == null)
        {
            ModLogger.Surfaced("HOME-EVENING", "no_eligible_storylet_at_fire",
                new InvalidOperationException($"intent={intent}"));
            runtime.ResolvePlayerChoice(home);
            return;
        }
        EffectExecutor.Apply(picked.Immediate, ctx);
        var evt = StoryletEventAdapter.BuildModal(picked, ctx, home);
        var candidate = picked.ToCandidate(ctx);
        candidate.InteractiveEvent = evt;
        candidate.ProposedTier = StoryTier.Modal;
        candidate.ChainContinuation = true;
        StoryDirector.Instance?.EmitCandidate(candidate);
        runtime.ResolvePlayerChoice(home);
    }

    private static TextObject BuildTooltip(string intent, bool eligible)
    {
        if (!eligible) { return new TextObject("{=home_evening_nothing}There's nothing here tonight."); }
        if (intent == "scheme"
            && EnlistmentBehavior.Instance?.EnlistedLord?.Clan?.IsUnderMercenaryService != true)
        {
            return new TextObject("{=home_scheme_tooltip_vassal}Risky under an honour-bound lord. Scrutiny may rise.");
        }
        return new TextObject($"{{=home_evening_{intent}_tooltip}}…");
    }

    private static string DefaultLabel(string intent) => intent switch
    {
        "unwind"     => "Drink with the squad",
        "train_hard" => "Drill until blisters",
        "brood"      => "Sit alone with your thoughts",
        "commune"    => "Seek out the lord",
        "scheme"     => "Slip away quietly",
        _            => "…"
    };
}
```

### 8.4 Menu slot-bank registration

Matches the decisions-slot pattern at `EnlistedMenuBehavior.cs:1150-1158`. Added inside `EnlistedMenuBehavior.OnSessionLaunched`:

```csharp
// Evening intent slots (visible only when HomeActivity is in evening phase).
// Priority 9 — appears in enlisted_status above the camp_hub (priority 10) so Evening
// is the focal point during its phase.
for (var i = 0; i < 5; i++)
{
    var slotIndex = i;
    starter.AddGameMenuOption(
        menuId: "enlisted_status",
        optionId: $"enlisted_evening_intent_slot_{i}",
        optionText: $"{{EVENING_SLOT_{i}_TEXT}}",
        condition: args =>
        {
            if (!HomeEveningMenuProvider.IsSlotActive(slotIndex, out var label, out var tooltip))
            {
                return false;
            }
            MBTextManager.SetTextVariable($"EVENING_SLOT_{slotIndex}_TEXT", label.ToString(), false);
            args.Tooltip = tooltip;
            args.IsEnabled = true;
            return true;
        },
        consequence: _ => HomeEveningMenuProvider.OnSlotSelected(slotIndex),
        isLeave: false,
        index: 9);
}
```

Text-variable binding via `MBTextManager.SetTextVariable` matches the decision-slot pattern at `EnlistedMenuBehavior.cs:1143`. Slots that fail the condition are simply hidden — no dynamic append.

## 9. Engine-state triggers (`HomeTriggers`)

Registered at boot via `TriggerRegistry.Register(name, predicate)`. Each predicate takes a `StoryletContext` and returns `bool`. All read live engine state. All null-safe.

| Trigger | Reads | Engine citation |
| :--- | :--- | :--- |
| `party_food_lte_3_days` | `lord.PartyBelongedTo.GetNumDaysForFoodToLast() <= 3` | `MobileParty.cs:3231` |
| `party_food_lte_5_days` | ≤ 5 | Same |
| `party_food_starving` | `party.Food < 1` | `MobileParty.cs:1089` |
| `lord_morale_lte_30` | `party.Morale <= 30` | `MobileParty.cs:878` |
| `lord_morale_lte_50` | ≤ 50 | Same |
| `lord_morale_lte_70` | ≤ 70 | Same |
| `lord_morale_gte_80` | ≥ 80 | Same |
| `lord_is_wounded` | `lord.IsWounded` | `Hero.cs:329` |
| `lord_is_disorganized` | `party.IsDisorganized` | `MobileParty.cs:416` |
| `lord_at_home_settlement` | `lord.HomeSettlement == party.CurrentSettlement` | `Hero.cs:709` |
| `lord_has_spouse` | `lord.Spouse != null` | `Hero.cs:818` |
| `lord_has_children` | `lord.Children.Count > 0` | `Hero.cs:843` |
| `lord_trait_mercy_positive` | `lord.GetTraitLevel(DefaultTraits.Mercy) >= 1` | `Hero.cs:1403` |
| `lord_trait_mercy_negative` | `<= -1` | Same |
| `lord_trait_valor_positive` / `_negative` | Same for `Valor` | Same |
| `lord_trait_honor_positive` / `_negative` | Same for `Honor` | Same |
| `lord_trait_calculating_positive` / `_negative` | Same for `Calculating` | Same |
| `enlisted_lord_is_mercenary` | `lord.Clan.IsUnderMercenaryService` | `Clan.cs:242` |
| `kingdom_at_war` | `lord.Clan.FactionsAtWarWith.Count > 0` | `Clan.cs:402` |
| `kingdom_multi_front_war` | `> 1` | Same |
| `at_town` | `settlement.IsTown` | `Settlement.cs:378` |
| `at_castle` | `settlement.IsCastle` | `Settlement.cs:390` |
| `at_village` | `settlement.Village != null` | `Settlement.cs` |
| `settlement_under_siege` | `settlement.IsUnderSiege` | `Settlement.cs:466` |
| `settlement_recent_threat` | `(Now - settlement.LastThreatTime).ToDays < 7` | `Settlement.cs:360` |
| `settlement_low_loyalty` | `town.Loyalty < 40` | `Town.cs:191` |
| `is_night` | `CampaignTime.Now.IsNightTime` | `CampaignTime.cs` |
| `is_autumn` | `CampaignTime.Now.GetSeasonOfYear == 2` | `CampaignTime.cs` |
| `is_winter` | `GetSeasonOfYear == 3` | Same |
| `days_enlisted_gte_30` | `EnlistmentBehavior.Instance.DaysEnlisted >= 30` | mod state (verify or add accessor) |
| `days_enlisted_gte_90` | `>= 90` | mod state |
| `rank_gte_3` | `EnlistmentBehavior.Instance.CurrentTier >= 3` | mod state |

Dropped from Spec 1: `recent_company_casualty` (no source API; deferred). If `DaysEnlisted` / `CurrentTier` accessors aren't public, implementation plan adds them as narrow properties — not a new subsystem.

## 10. Storylet authoring — corrected schema

Storylet JSON respects the actual parser in `StoryletCatalog.cs:66-225`:

- No separate `titleId` / `setupId` / `textId` / `tooltipId` fields. The text field embeds `{=id}Fallback`.
- Chains are declared on `Choice.Chain`, not as effects.
- Effect primitives match `EffectExecutor.cs:73-108`: `quality_add`, `quality_add_for_hero`, `quality_set`, `set_flag` (parameter `name`), `clear_flag`, `set_flag_on_hero`, `trait_xp`, `skill_xp`, `give_gold`, `give_item`. **No `trigger_event`.**
- Storylets live in top-level `ModuleData/Enlisted/Storylets/*.json` files as arrays (`{ "storylets": [ ... ] }`).

**Corrected example — `ModuleData/Enlisted/Storylets/home_evening.json` excerpt:**

```json
{
  "storylets": [
    {
      "id": "home_evening_brood_dead_friend",
      "category": "home_evening_brood",
      "trigger": ["days_enlisted_gte_30"],
      "weight": {
        "base": 2.0,
        "modifiers": [
          { "if": "lord_trait_honor_positive", "factor": 1.3 },
          { "if": "is_autumn", "factor": 1.2 }
        ]
      },
      "scope": { "context": "settlement" },
      "title": "{=home_brood_empty_cot_title}In the empty cot",
      "setup": "{=home_brood_empty_cot_setup}Seoras had the cot next to yours since Pravend. His kit is still folded at the foot, the way he liked it — the boots turned just so. You sit with it in the dark a while.",
      "options": [
        {
          "id": "take_boots",
          "text": "{=home_brood_empty_cot_opt_take}Take the boots. He'd want someone to use them.",
          "tooltip": "{=home_brood_empty_cot_opt_take_tt}+Roguery. Scrutiny up if caught.",
          "effects": [
            { "apply": "skill_xp", "skill": "Roguery", "amount": 20 },
            { "apply": "set_flag", "name": "took_dead_friend_gear" },
            { "apply": "quality_add", "quality": "scrutiny", "amount": 2 }
          ]
        },
        {
          "id": "keep_kit",
          "text": "{=home_brood_empty_cot_opt_keep}Keep the kit as it is. Someone should know him still.",
          "tooltip": "{=home_brood_empty_cot_opt_keep_tt}+lord relation.",
          "effects": [
            { "apply": "lord_relation_up_small" },
            { "apply": "set_flag", "name": "kept_dead_friend_kit" }
          ]
        },
        {
          "id": "write_ledger",
          "text": "{=home_brood_empty_cot_opt_write}Write his name in the book no one asked you to keep.",
          "tooltip": "{=home_brood_empty_cot_opt_write_tt}+rank_xp. Parks a ledger chain.",
          "effects": [
            { "apply": "rank_xp_minor" },
            { "apply": "set_flag", "name": "keeps_death_ledger" }
          ],
          "chain": {
            "id": "home_chain_ledger_rumored",
            "when": "home_activity.evening"
          }
        }
      ]
    }
  ]
}
```

`chain.when` drives `ActivityRuntime.ParkChain(typeId, phaseId, storyletId)` — the chain storylet fires next time `HomeActivity` enters the target phase (a new settlement stop, or the next night on a multi-day stay). Chain storylets open with "some time later…" framing; Spec 1 does not wire an N-days timer.

`lord_relation_up_small` and `rank_xp_minor` are scripted effects from Spec 0's seed catalog (`ModuleData/Enlisted/Effects/scripted_effects.json`); author-facing content references them by name per the storylet-backbone pattern.

### 10.1 50-storylet breakdown

| Pool | Count | Trigger focus |
| :--- | ---: | :--- |
| `home_arrive` | 8 | Faction-flavored (Empire/Sturgia/Battania/Vlandia/Aserai/Khuzait), home-fief arrival, wounded-lord arrival, post-defeat arrival |
| `home_settle` | 12 | Food-pressure (2), wounded-tending (1), stores (1), weather/season (2), settlement-state (2), lord-observations (2), chaplain (1), siege prep (1) |
| `home_evening_unwind` | 5 | Dice, song, sergeant, firelit, stars |
| `home_evening_train_hard` | 5 | Sparring, drill, master, armor, foreign-form |
| `home_evening_brood` | 5 | Dead-friend kit, lord-like-father, letter home, cold comfort, drift (fallback) |
| `home_evening_commune` | 5 | Lord at hearth, squadmate confession, family meal (home-fief gated), chaplain, old-soldier advice |
| `home_evening_scheme` | 5 | Skim stores (merc-gated), eavesdrop (merc-gated), bribe (merc-gated), tavern rumors (universal low-scrutiny), ghost sentry (universal mid-scrutiny) |
| `home_break_camp` | 5 | Gate parting, lord's mood after home, supply trains, sword-edge sharpened, rain-on-march (weather) |

All titles, setups, option text, tooltips localized via `{=id}Fallback` embedded in the text field. Sync pass: `Tools/Validation/sync_event_strings.py` into `ModuleData/Enlisted/Localization/strings_home.xml`.

### 10.2 Chains

Four Spec 1 storylets park continuations:

- `home_evening_brood_dead_friend` option `take_boots` → `home_chain_boots_rumor` (when: `home_activity.evening`)
- `home_evening_brood_dead_friend` option `write_ledger` → `home_chain_ledger_rumored` (when: `home_activity.evening`)
- `home_evening_commune_lord_at_hearth` option `speak_plainly` → `home_chain_lord_remembered` (when: `home_activity.evening`)
- `home_evening_scheme_skim_stores` → `home_chain_stores_audited` (when: `home_activity.settle`)

Chains fire on the next phase-entry match (no real-time timer). Author narrative prose accommodates the "some time later…" elasticity.

## 11. `QualityStore` usage

Spec 1 writes to existing qualities only: `rank_xp`, `lord_relation` (cached mirror), `scrutiny`. No new qualities.

## 12. Testing & verification

### 12.1 Smoke-test debug bindings

Add hotkeys in `src/Debugging/Behaviors/DebugToolsBehavior.cs`:

| Key | Action |
| :---: | :--- |
| `Ctrl+Shift+H` | Force-start a `HomeActivity` at the current settlement (bypass eligibility gates) |
| `Ctrl+Shift+E` | Advance to Evening phase immediately |
| `Ctrl+Shift+B` | Advance to Break Camp and finish |
| `Ctrl+Shift+Y` | Dump trigger evaluation table (intent slots + arrive/settle pools) to session log. Rebound from `T` after an observed in-game overlay collision; decompile audit of `DebugHotKeyCategory.cs:560-749` confirms `Y` is clean across every modifier combo. |

### 12.2 `validate_content.py` extensions

Extend Phase 12 with:
- All `home_*` storylet `pool` refs must resolve to storylet ids.
- All triggers referenced must exist in `HomeTriggers.Register()`.
- All scripted effects referenced must exist in `scripted_effects.json`.
- All `{=id}` tokens must have entries in `strings_home.xml`.
- Spec 1 does **not** alter the effect-primitive allowlist (already matches `EffectExecutor` surface at `validate_content.py:1641`).
- `ActivityTypeDefinition` JSONs: `phases[].pool` entries must exist in `StoryletCatalog`; `delivery` values in `{auto, player_choice}`.

### 12.3 Acceptance checklist

- [ ] `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds.
- [ ] `python Tools/Validation/validate_content.py` passes with home_* storylets.
- [ ] Force-start hotkey enters Arrive phase; one storylet appears in headlines accordion.
- [ ] Settle phase fires 1-2 storylets over 4 in-game hours (FireIntervalHours = 2).
- [ ] Evening phase surfaces **visible slot-bank options** — only the intents with ≥1 eligible storylet render; others hidden.
- [ ] Selecting an intent fires a Modal via `StoryDirector` with 2-3 option buttons and effects that actually apply.
- [ ] Modal resolution advances to Break Camp; Break Camp storylet fires on `SettlementLeft`.
- [ ] Party-identity gate: entering a settlement **with a different party** (e.g. a passing lord) does NOT start a HomeActivity.
- [ ] Save/load mid-activity restores `CurrentPhaseIndex`, `Intent`, `EveningFired`, `DigestReleased`, `LastAutoFireHour`.
- [ ] Chain park: selecting `write_ledger` sets the flag AND parks `home_chain_ledger_rumored`; next Evening phase entry consumes the parked chain.
- [ ] No `ModLogger.Error` call in new code (validator Phase 11).
- [ ] `OrchestratorOverride.cs` / `orchestrator_overrides.json` **unchanged** (Non-goal §3).

### 12.4 Regression risk

- **Menu slot-bank priority.** Five new menu entries at `index: 9` push into the range between decision slots (`4-8`) and camp_hub (`10`). Verify rendered order in-game; adjust priorities if overlap is ugly.
- **Chain `when:` format.** Chains target `{typeId}.{phaseId}`. A typo silently drops the chain. Validator Phase 12 extension checks every `when:` parses to a known activity type + phase id.
- **`Storylet.ToCandidate` + adapter composition.** The adapter mutates `candidate.InteractiveEvent` after `ToCandidate` returns. Ensure no call site assumes `ToCandidate`'s output is immutable (grep during implementation).
- **`StoryletEventAdapter.BuildModal` + InquiryData.** TaleWorlds' `MultiSelectionInquiryData` affirmative-action receives a list of selected elements; Spec 1 always configures `minSelectableOptionCount = maxSelectableOptionCount = 1`, so selection is exactly one. Implementation plan includes a unit-test-shaped smoke that picks each option variant end-to-end.

## 13. Migration / rollback

No migration. New code path, new content. Rollback is a revert of the feature commits; `HomeActivity` gone, no residual save-offset risk for main branch because offset 45 is unclaimed pre-Spec-1. Saves created during Spec 1 testing fail to load after rollback — acceptable (Spec 1 is unreleased).

## 14. Hooks for Specs 2-5

Spec 1 establishes four reusable primitives:

1. **`ActivityTypeCatalog`.** Specs 2-5 drop their own `ModuleData/Enlisted/Activities/*.json` files.
2. **`StoryletEventAdapter`.** Any spec that wants Modal storylet choices uses it.
3. **`PhaseDelivery.PlayerChoice` runtime.** Specs 2-4 have player-choice phases; they register their own menu-slot banks and providers.
4. **Engine-state `TriggerRegistry` pattern.** Specs 2-5 add their own triggers using `HomeTriggers` as the template. Namespaced by prefix (`order_`, `muster_`, `sea_`).

Activity subclasses for Specs 2-5 claim class offsets 46-50.

## 15. Open questions (remaining after review)

- **`EnlistmentBehavior.DaysEnlisted` / `CurrentTier` accessor visibility.** If not already public, implementation plan exposes as read-only properties (no new subsystem).
- **`InteractiveEvent` wrapper constructor signature.** Verify during implementation; the adapter's `new InteractiveEvent(inquiry, title, setup)` call shape matches the existing wrapper or is corrected.
- **`StoryletSelector.Pick` availability.** Referenced throughout; confirm it exists in Spec 0 as a public static. If it's inline in `ActivityRuntime.TryFireAutoPhaseStorylet`, implementation plan extracts it as a reusable static.
- **Tooltip length.** Critical Rule #7 caps tooltips at <80 chars. Audited during authoring.

## 16. Summary

Spec 1 ships:

- **Two generic primitives** (`ActivityTypeCatalog`, `StoryletEventAdapter`) that unblock Specs 2-5.
- **The `PhaseDelivery.PlayerChoice` runtime branch** in `ActivityRuntime`.
- **One new `Activity` subclass** (`HomeActivity`, offset 45).
- **One menu slot-bank** (5 preallocated options in `enlisted_status`, decision-slot pattern).
- **~30 engine-state trigger predicates** in `HomeTriggers`.
- **50 home-camp storylets** across 4 phases × 5 intents.

Total new C#: ~810 LOC + ~196 LOC of edits. Content authoring: ~12-17 hours. The architecture grounds every mechanical decision in the Bannerlord v1.3.13 API (cited inline) AND in the actual substrate of Spec 0 + `EnlistedMenuBehavior` as verified by adversarial review.
