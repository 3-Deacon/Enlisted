# Enlisted Home Surface — Reference

**Status:** Live. This is the authoritative day-to-day reference for the shipped "enlisted home" surface (Spec 1, Phases A–D complete). Phase E (debug hotkeys + validator extension + acceptance smoke) is the only outstanding piece.

**For design rationale / problem framing:** see the frozen design spec at [`../../superpowers/specs/2026-04-19-enlisted-home-surface-design.md`](../../superpowers/specs/2026-04-19-enlisted-home-surface-design.md).

**For implementation history:** see the frozen plan at [`../../superpowers/plans/2026-04-19-enlisted-home-surface.md`](../../superpowers/plans/2026-04-19-enlisted-home-surface.md).

The Home Surface is the first player-experience surface built on the [storylet backbone](storylet-backbone.md) (Spec 0). It answers: "what does the player do when the lord's party is parked at a friendly settlement?"

---

## What HomeActivity does

`HomeActivity` runs whenever the enlisted lord's party enters a friendly town, castle, or village. It emits ambient storylets across three auto-fire phases, surfaces five player-choice intents in the evening phase (`enlisted_status` menu), and exits when the party leaves.

Save-definer offset: **45** (class). Owning spec: 1. Registered in [`src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`](../../../src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs).

TypeId: `home_activity` — matches `ModuleData/Enlisted/Activities/home_activity.json`.

---

## Lifecycle (`HomeActivityStarter`)

File: [`src/Features/Activities/Home/HomeActivityStarter.cs`](../../../src/Features/Activities/Home/HomeActivityStarter.cs).

```
CampaignEvents.SettlementEntered
  ↓ filter: party == EnlistmentBehavior.Instance.EnlistedLord.PartyBelongedTo   (party-identity gate)
  ↓ filter: !settlement.IsHideout && lord & settlement faction not at war       (friendly-fief gate)
  ↓ filter: no HomeActivity already active                                       (no-duplicate gate)
  → ActivityRuntime.Start(new HomeActivity { Intent = pickInitialIntent(...) })
```

On departure:

```
CampaignEvents.OnSettlementLeftEvent
  ↓ same party-identity gate
  → home.OnBeat("departure_imminent", ctx)
  → HomeActivity jumps CurrentPhaseIndex to break_camp - 1, lets the runtime advance normally
```

**Party-identity gate is critical.** Other parties entering the same settlement do NOT start a HomeActivity — only the lord's own party does. This prevents garrison NPCs, merchants, and unrelated lords from polluting the player's narrative thread.

---

## Phase flow

Authored in [`ModuleData/Enlisted/Activities/home_activity.json`](../../../ModuleData/Enlisted/Activities/home_activity.json). Schema parser at [`src/Features/Activities/ActivityTypeCatalog.cs`](../../../src/Features/Activities/ActivityTypeCatalog.cs).

| # | Phase | Duration | Fire Interval | Delivery | Pool | Intent bias |
| :-: | :--- | :---: | :---: | :--- | :---: | :--- |
| 0 | `arrive` | 1h | 0 (once) | auto | 8 | brood 1.2, commune 1.3, unwind 1.0, train_hard 0.8, scheme 0.9 |
| 1 | `settle` | 4h | 2h | auto | 12 | flat (1.0 each; train_hard 1.1) |
| 2 | `evening` | 0 | 0 | **player_choice** | 25 | (not used; intent comes from menu click) |
| 3 | `break_camp` | 1h | 0 (once) | auto | 5 | n/a |

Total storylet corpus: **50**.

**`arrive` and `break_camp`** fire exactly once per home visit (duration 1h, fire_interval 0). **`settle`** auto-fires up to twice in its 4-hour window (fire_interval 2h). **`evening`** waits for player input — see next section.

---

## The 5-intent Evening menu

File: [`src/Features/Activities/Home/HomeEveningMenuProvider.cs`](../../../src/Features/Activities/Home/HomeEveningMenuProvider.cs).

Consumed by `EnlistedMenuBehavior`'s 5-slot Evening bank (priority 9, above the camp_hub at priority 10). The provider is called per slot during menu refresh.

**Intent order (fixed):** `[ "unwind", "train_hard", "brood", "commune", "scheme" ]` — slot 0 → unwind, slot 4 → scheme.

```
IsSlotActive(i)
  → runtime.GetActiveChoicePhase() must return (HomeActivity, Phase "evening")
  → AnyEligibleForIntent(home, intents[i])
      iterates home.CurrentPhase.Pool, filters by prefix "home_evening_<intent>_"
      short-circuits on first IsEligible storylet
  → label: "{=home_evening_<intent>_label}<DefaultLabel>"
  → tooltip: intent-specific; scheme shows a scrutiny warning under non-mercenary lords

OnSlotSelected(i)
  → home.OnPlayerChoice(intent, ctx)        — sets Intent, flips EveningFired
  → PickStorylet: weighted pick over the same prefix-filtered pool (weight = Storylet.WeightFor(ctx))
  → EffectExecutor.Apply(picked.Immediate, ctx)
  → StoryDirector.EmitCandidate(candidate with ChainContinuation=true)
  → runtime.ResolvePlayerChoice(home)       — advances phase
```

**Intent → storylet-id convention:** a storylet belongs to intent X iff its id matches `home_evening_<X>_*`. The provider uses this prefix match — there is no separate `intent` field on storylets.

**Default labels** (from `DefaultLabel(intent)`):

| Intent | Label |
| :--- | :--- |
| unwind | Drink with the squad |
| train_hard | Drill until blisters |
| brood | Sit alone with your thoughts |
| commune | Seek out the lord |
| scheme | Slip away quietly |

---

## Trigger palette (`HomeTriggers`)

File: [`src/Features/Activities/Home/HomeTriggers.cs`](../../../src/Features/Activities/Home/HomeTriggers.cs). Registered by `SubModule.OnGameStart` before any behavior tick.

All 34 predicates read **live engine state** via `EnlistmentBehavior.Instance.EnlistedLord` → `Lord.PartyBelongedTo` → `Party.CurrentSettlement`. None read mirrored values from `QualityStore`.

| Domain | Triggers |
| :--- | :--- |
| Party / food | `party_food_lte_3_days`, `party_food_lte_5_days`, `party_food_starving` |
| Lord morale | `lord_morale_lte_30`, `lord_morale_lte_50`, `lord_morale_lte_70`, `lord_morale_gte_80` |
| Lord condition | `lord_is_wounded`, `lord_is_disorganized`, `lord_at_home_settlement`, `lord_has_spouse`, `lord_has_children` |
| Lord traits (× ±) | `lord_trait_mercy_positive`, `lord_trait_mercy_negative`, `lord_trait_valor_{positive,negative}`, `lord_trait_honor_{positive,negative}`, `lord_trait_calculating_{positive,negative}` |
| Clan / kingdom | `enlisted_lord_is_mercenary`, `kingdom_at_war`, `kingdom_multi_front_war` |
| Settlement type | `at_town`, `at_castle`, `at_village` |
| Settlement state | `settlement_under_siege`, `settlement_recent_threat` (7d), `settlement_low_loyalty` (town.Loyalty < 40) |
| Time | `is_night`, `is_autumn`, `is_winter` |
| Mod state | `days_enlisted_gte_30`, `days_enlisted_gte_90` |

**Important: trait predicates use `Func<TraitObject>` providers, not eager `DefaultTraits.X` arguments** — `Campaign.Current.DefaultTraits` is null at `OnGameStart` and direct references NRE before mod boot completes. See `RegisterTrait` helper.

---

## Storylet corpus

All files under [`ModuleData/Enlisted/Storylets/`](../../../ModuleData/Enlisted/Storylets/). Loaded by `StoryletCatalog.LoadAll` (non-recursive — files must live at the top level of this directory).

| File | Count | Delivery | Shape |
| :--- | :---: | :--- | :--- |
| `home_arrive.json` | 8 | auto, observational | Settlement-arrival variety: home-fief, wounded-returning, grim-mood, post-defeat, 4 faction flavors |
| `home_settle.json` | 12 | auto, observational | Reactive to engine state: food/weather/wounded/siege/lord-trait observations |
| `home_evening.json` | 25 | **player_choice** | 5 per intent (unwind / train_hard / brood / commune / scheme), options with effects |
| `home_break_camp.json` | 5 | auto, observational | Departure beats |

Observational storylets have no options (or a single "continue"); evening storylets carry 2-3 options with effects. Effects use the Spec 0 seed catalog (`lord_relation_up_small`, `rank_xp_minor`, `scrutiny_up_small`, etc.) plus primitives (`give_gold`, `skill_xp`, `quality_add`, `set_flag`).

**Continuation chains parked by evening storylets:** `home_chain_ledger_rumored` (from `brood_dead_friend`), `home_chain_lord_remembered` (from `commune_lord_at_hearth`). The chain storylets themselves are not yet authored — first fire will no-op gracefully.

---

## Save state

`HomeActivity` persists four fields (`SaveableProperty` 1–4):

| Field | Type | Purpose |
| :--- | :--- | :--- |
| `StartedAtSettlementId` | string | Which settlement initiated this visit; used for post-load sanity checks |
| `PhaseStartedAt` | `CampaignTime` | Drives fire-interval enforcement |
| `DigestReleased` | bool | Ensures the arrive-phase digest signal fires once |
| `EveningFired` | bool | Prevents duplicate evening slots per visit |

Base `Activity` fields (`TypeId`, `CurrentPhaseIndex`, `Intent`, `LastAutoFireHour`) are defined on the abstract base at offset 44.

**Load path:** `ActivityRuntime.RestoreFromSave` re-resolves `Phases` from `TypeId` via `ActivityTypeCatalog` — phases are NOT saved directly. Changing `home_activity.json` between runs changes the behavior of loaded saves.

---

## Localization

Storylet loc-keys use the **inline `{=key}Fallback`** pattern (in `title` / `setup` / `options[].text` / `options[].tooltip`), not the legacy Event schema's separate `titleId`+`title` pairs. See [`../../../CLAUDE.md`](../../../CLAUDE.md) → Project conventions.

All 206 home-surface keys are synced into [`ModuleData/Languages/enlisted_strings.xml`](../../../ModuleData/Languages/enlisted_strings.xml); run `Tools/Validation/sync_event_strings.py` after authoring new storylets to pick up additions.

---

## Integration points (plugs into storylet backbone)

| Backbone piece | How Home uses it |
| :--- | :--- |
| `ActivityRuntime` | `Start` / `FindActive<HomeActivity>` / `ResolvePlayerChoice` / `GetActiveChoicePhase` |
| `ActivityTypeCatalog` | Loads `home_activity.json` → registers the four phases |
| `StoryletCatalog` | Loads all `home_*.json` storylet files |
| `TriggerRegistry` | 34 `HomeTriggers` predicates registered at OnGameStart |
| `StoryletContext` | Populated by the Evening provider with `ActivityTypeId="home_activity"`, `PhaseId="evening"`, `CurrentContext="settlement"` |
| `StoryletEventAdapter.BuildModal` | Wraps a chosen storylet into an `InteractiveEvent` for `StoryDirector` |
| `StoryDirector.EmitCandidate` | Routes the chosen evening storylet as a Modal (with `ChainContinuation=true`) |
| `EffectExecutor` | Applies immediate + chosen-option effects |
| `ScriptedEffectRegistry` | Resolves seed-catalog effect ids from option JSON |

---

## File map

Core C# (all under `src/Features/Activities/Home/`):

| File | Purpose |
| :--- | :--- |
| `HomeActivity.cs` | Concrete activity; 4 SaveableProperty fields; phase-beat handler |
| `HomeActivityStarter.cs` | `CampaignBehaviorBase` that gates start/stop by party identity |
| `HomeEveningMenuProvider.cs` | 5-intent slot-bank provider for `EnlistedMenuBehavior` |
| `HomeTriggers.cs` | 34 engine-state predicates |

Content (all under `ModuleData/Enlisted/`):

| File | Purpose |
| :--- | :--- |
| `Activities/home_activity.json` | ActivityTypeDefinition — 4 phases, pools, delivery, intent biases |
| `Storylets/home_arrive.json` | 8 storylets |
| `Storylets/home_settle.json` | 12 storylets |
| `Storylets/home_evening.json` | 25 storylets (player-choice) |
| `Storylets/home_break_camp.json` | 5 storylets |

Menu bank:

| File | Relevant region |
| :--- | :--- |
| `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` | 5-slot evening bank at L1160–1187 (priority 9); mirrors decision-slot pattern at L1149–1158 |

Save registration:

| File | Offset |
| :--- | :---: |
| `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` | Class 45 (HomeActivity); claims 45–60 are reserved for concrete `Activity` subclasses per [storylet-backbone.md](storylet-backbone.md) |

---

## Gotchas

1. **`HomeActivity` `TypeId` must be set in `OnStart`** — otherwise `Activity.ResolvePhasesFromType` no-ops on load and the activity has zero phases. Current code: `TypeId = TypeIdConst` at line 26.
2. **`DefaultTraits.X` is null at `OnGameStart`.** `HomeTriggers.RegisterTrait` wraps the trait lookup in `Func<TraitObject>` — do NOT inline `DefaultTraits.Mercy` as an eager argument at registration time. NRE'd during an earlier boot, aborting the rest of mod init.
3. **`HashSet<StoryBeat>` is not a saveable container.** An earlier draft tried to register one; `ContainerType` enum closes at `List/Queue/Dictionary/Array/CustomList/CustomReadOnlyList`. Home state uses only primitive fields.
4. **Intent is set by `HomeActivityStarter`, not overwritten by `OnStart`.** Don't copy-paste `OnStart` logic from another activity that clears `Intent` — the Evening provider uses it.
5. **`ActivityTypeCatalog` parses `home_activity.json` non-recursively.** If you add another activity file, it lives at the top level of `ModuleData/Enlisted/Activities/`, not in a subfolder.
6. **Storylet pool IDs must exactly match storylet file IDs.** `validate_content.py` Phase 12 catches typos; missing pool entries silently produce empty phases. Grep before committing pool changes.
7. **The `home_*` prefix is load-bearing for Evening.** `HomeEveningMenuProvider.AnyEligibleForIntent` filters by `StartsWith("home_evening_<intent>_")`. Renaming a storylet to drop that prefix removes it from the intent pool even if the pool list still lists it.

---

## What Phase E will add

- **Debug hotkeys** in `DebugToolsBehavior.cs` — `Ctrl+Shift+H` force-start, `Ctrl+Shift+E` advance-to-evening, `Ctrl+Shift+B` advance-to-break-camp, `Ctrl+Shift+T` dump trigger eval table.
- **`validate_content.py` Phase 12 extensions** — activity pool-reference check, `chain.when` parse check.
- **Acceptance smoke** — 8 in-game scenarios (arrive/settle fire, evening slots + Modal, party-identity gate, save/load, chain park, no OrchestratorOverride regression).

See [`../../superpowers/HANDOFF-2026-04-20-spec1-phase-e.md`](../../superpowers/HANDOFF-2026-04-20-spec1-phase-e.md) for scope + task text.
