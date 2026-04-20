# Orders Surface — Design Spec

**Date:** 2026-04-20
**Status:** Design draft, revised once after adversarial review (2026-04-20). Awaiting user re-review.
**Scope:** Spec 2 in the 6-spec cycle. Replaces the existing 11-file `src/Features/Orders/` subsystem with a single `OrderActivity` running on the Spec 0 storylet backbone. Reframes "Orders" as the **career-arc text RPG** that hums in the background while Bannerlord's foreground campaign plays out — making enlistment a replayable rise from peasant to commander, with real Bannerlord-native progression rewards (skill XP, focus points, attribute points, traits, renown, relations, native-issue loot from the lord's faction troop tree).

Depends on Spec 0 (Storylet Backbone, landed `45b38bf`) and Spec 1 (Enlisted Home Surface, landed `0390fdf`). Spec 1's `ActivityTypeCatalog`, `Storylet → InteractiveEvent` adapter, and `PhaseDelivery.PlayerChoice` runtime are inherited unchanged. Spec 2 adds **twelve scripted-effect primitives** (eight character-development reward currencies plus four runtime-control), a duty-profile sampling subsystem, an arc-storylet variant with explicit save-load reconstruction, a path-progression accumulator, a faction-troop-tree-aware loot resolver, **a thin `EnlistmentBehavior.OnTierChanged` event**, and **migration of eight consumer call sites** that today read `OrderManager.Instance` directly.

---

## 1. Problem

Today's Orders subsystem (`src/Features/Orders/`, ~1.5K lines across 11 files; 17 orders, 84 hand-authored events, 16 event pools) does three things and does each poorly:

1. **It's an island.** `OrderManager.ApplyOutcome` (`OrderManager.cs:762-784`) awards skill XP, gold, and an in-house "officer reputation" number — but nothing else. The full Bannerlord progression vocabulary (focus points, attribute points, unspent points, trait levels, renown, relation deltas on real heroes, item drops into the player's inventory) is untouched. Skill XP without focus is capped progression — a T3 player with 0 focus on Bow can't pass ~50 Bow no matter how many "guard duty" rolls they take. Today's Orders are *literally incapable* of pushing the player past Bannerlord's natural caps, because they never grant the currencies that raise those caps.

2. **It's static.** Outcomes are JSON-authored as fixed Success / Failure / Decline blobs (`OrderConsequence.cs:7-32`). What actually happened during the duty arc — which event prompts fired, what the player chose — has zero effect on what's awarded at completion. The player who slacked through Guard Duty and the player who diligently caught a thief receive the same "success" payload.

3. **It's blind to the lord.** Order availability is gated by `tier_min` / `tier_max` and `not_at_sea`. Whether the lord is besieging a castle, garrisoned in their seat, raiding a village, marching to war, or wandering the countryside has no bearing on what duties the sergeant offers. A T3 player gets "Latrine Detail" pulled from the same pool whether they're at peace in a Vlandian town or knee-deep in a Khuzait raid.

Meanwhile the Spec 0 substrate — `QualityStore`, `FlagStore`, `ActivityRuntime`, `Storylet`, `ScriptedEffectRegistry`, `TriggerRegistry`, scripted-effect catalog — was designed precisely to express the things today's Orders cannot. Spec 1 proved the substrate works at the surface level (`HomeActivity`, save offset 45, 50 storylets, all five phases shipped). Spec 2 retires the hand-rolled Orders subsystem in favor of an Activity that *uses* the substrate and integrates honestly with Bannerlord.

A separate investigation against the v1.3.13 decompile (`AiPartyThinkBehavior.cs:47-214`, `AiMilitaryBehavior.cs:471-549`, `Army.cs:31-473`, `MobilePartyAi.cs:17-290`) confirmed lord AI is **probabilistically unstable**: lords re-think every 1–6 hours with a 3–10% per-tick flip probability. A solo lord besieging a castle has ~10-hour median persistence; an army leader has ~30 hours. Five documented flip-flop modes (siege-abandonment-mid-assault, raid-to-besiege pivot, army-disband-after-48h-inactivity, settlement-changed-hands forced re-think, food-shortage cascade) cause *immediate* behavior change with no grace period. Any duty-profile system must tolerate this — or better, treat it as content rather than a failure mode.

## 2. Goals

- Retire `src/Features/Orders/` entirely; replace with `src/Features/Activities/Orders/OrderActivity.cs` (Activity subclass, save offset 46). **Migrate the eight existing consumer call sites** (`EnlistedMenuBehavior.cs:956,2167,2408`; `EnlistedNewsBehavior.cs:768,4204`; `ForecastGenerator.cs:66`; `CampOpportunityGenerator.cs:1598`; `ContentOrchestrator.cs:571`) to read from `OrderActivity` instead of `OrderManager.Instance` *before* deleting the old subsystem.
- Ship **twelve** new scripted-effect primitives — eight character-development reward currencies (`grant_skill_level`, `grant_focus_point`, `grant_unspent_focus`, `grant_attribute_level`, `grant_unspent_attribute`, `set_trait_level`, `grant_renown`, `relation_change`) and four runtime-control primitives (`grant_item`, `grant_random_item_from_pool`, `clear_active_named_order`, `start_arc`).
- Sample the lord's party state hourly with 2-hour hysteresis to classify a stable **duty profile** (one of six: `besieging` / `raiding` / `escorting` / `garrisoned` / `marching` / `wandering`). Treat profile transitions as content via dedicated transition storylets.
- Replace the 17 hand-authored orders with **named-order storylets** (storylets with an `arc` block); replace the 84 hand-authored order events with a richer corpus of profile-pool ambient storylets, intent-biased mid-arc storylets, transition storylets, path crossroads, and progression-floor storylets. Target initial corpus: ~260 storylets.
- Make every duty profile cover **all 18 Bannerlord skill axes** at varying density. A 30-day garrison stint progresses every skill at some rate; combat skills slow but never zero, social/support skills accelerate.
- Layer a **path-specialization system** over emergent skill+intent accumulation: Ranger / Enforcer / Support / Diplomat / Rogue. Three explicit crossroads moments (T3 / T5 / T7) let the player commit, resist, or drift.
- Resolve loot drops through `culture.BasicTroop` + `culture.EliteBasicTroop` + `UpgradeTargets` walking — the **same source of truth Quartermaster uses** (`TroopSelectionManager.BuildCultureTroopTree`, `TroopSelectionManager.cs:497-561`). One faction-tier-aware item economy, two consumers.
- Reuse `RankHelper.GetRankTitle(tier, cultureId)` (`RankHelper.cs:21`) for culture-specific rank labels in storylet text. No new rank infrastructure.
- Detect player combat class (Infantry / Ranged / Cavalry / HorseArcher) from current battle equipment via `CharacterObject.IsRanged` + `IsMounted`, refactored out of `EnlistedFormationAssignmentBehavior.DetectFormationFromEquipment` (`EnlistedFormationAssignmentBehavior.cs:1343-1385`) into a shared `CombatClassResolver`.
- Persist `prior_service_*` flags on the player hero on discharge, enabling cross-enlistment veteran-flavored content.
- Deliver in four playtestable phases (A → D) over ~5 calendar weeks, with the old `OrderManager` running in parallel through Phase A and retired at end of Phase B.

## 3. Non-goals

- **No `ActivityRuntime` / `Phase` schema changes.** `ActivityRuntime`, `ActivityTypeCatalog`, and `PhaseDelivery.PlayerChoice` are inherited from Spec 0+1 unchanged. **However**, the named-order arc splice IS new runtime state that must survive save/load; `OrderActivity.OnGameLoaded` is responsible for re-resolving the arc from `ActiveNamedOrder.OrderStoryletId` against the storylet catalog (no new serialization fields on `Activity` itself; reconstruction is local to `OrderActivity`). See §5 + §10.
- **No rewrite of `EnlistedMenuBehavior`.** Spec 2 may add 1–2 menu options for surfacing accept-candidate named orders, and **must replace the three internal `OrderManager.Instance` reads** at `:956`, `:2167`, `:2408` with `OrderActivity` queries. The menu architecture stays.
- **No promotion-storylet authoring.** Spec 2 ships the twelve scripted-effect primitives that promotions will consume; **Spec 4 (Promotion+Muster)** owns the actual rank-up flow and authors the per-tier promotion storylets. Spec 2 stops at the primitive layer.
- **No quartermaster behavior changes.** Spec 5 (Quartermaster) and Spec 2's loot system both consume `BuildCultureTroopTree`. The method is currently `private` inside `TroopSelectionManager` (`TroopSelectionManager.cs:494`); **Spec 2 owns extracting it into a shared `CultureTroopTreeHelper`** under `src/Features/Equipment/CultureTroopTreeHelper.cs` and rewires `TroopSelectionManager` to call the helper. This is a pure refactor — no QM behavior change, no storage change, no UX change.
- **No new error-output channels.** Use `ModLogger.Surfaced` / `Caught` / `Expected` per AGENTS.md.
- **No naval-specific duty profile.** Naval contexts route through the existing sea/land filter pattern at the storylet level (`requires_at_sea` / `excludes_at_sea` predicates). A future spec may add `seafaring` as a seventh profile if naval enlistment lands.
- **No backwards-compatibility with old order JSON.** Saves containing references to deleted order ids (e.g. `order_guard_duty`) will lose that state on load — acceptable per Spec 0's "saves lose deleted-quality data" precedent (Spec 0, §15).
- **No live tuning of `class_density` matrices.** Per Revision 1 of brainstorming, density is encoded **on storylets** (`class_affinity`, `intent_affinity`, `profile_requires`), not in a parallel matrix file. Authors edit storylets directly; weight changes are local.

## 4. Vocabulary

Spec 2 inherits Spec 0's vocabulary plus Spec 1's (Activity, Phase, Pool, PlayerChoice slot bank) and adds:

| Term | Meaning |
| :--- | :--- |
| **OrderActivity** | Concrete `Activity` subclass. Save offset **46**. Starts on `EnlistmentBehavior.IsEnlisted == true`, runs continuously until discharge. Holds duty profile, active named order, combat class cache, hysteresis counters. |
| **Duty profile** | Auto-classified state derived from the enlisted lord's `MobileParty` state. One of six values; resolved by `DutyProfileSelector.Resolve(MobileParty)` and committed by `DutyProfileBehavior` after 2-hour hysteresis. |
| **Named order** | A storylet with an `arc` block. The setup is the lord/sergeant's offer; option picks set the player's **intent** (and constitute accept). Accepting splices the arc's phases onto `OrderActivity` for the arc's duration. |
| **Intent** (order-specific) | One of `diligent` / `slack` / `scheme` / `sociable` / `train_hard`. Picked at named-order accept; persists for the arc; biases mid-arc storylet eligibility via `intent_affinity`. |
| **Combat class** | One of `Infantry` / `Ranged` / `Cavalry` / `HorseArcher`. Resolved from current battle equipment via `CombatClassResolver.Resolve(Hero)`; resampled hourly. Drives storylet `class_affinity` weighting and named-order `requires_combat_class` gating. |
| **Path** | One of `ranger` / `enforcer` / `support` / `diplomat` / `rogue`. Each has a 0–100 score quality (`path_<name>_score`) accumulated from intent picks and skill gains. The `committed_path` quality holds a single string id once a crossroads commit fires. |
| **Crossroads** | A storylet that fires on rank promotion at T3 / T5 / T7 if path scores indicate a clear leader. Three options: commit / resist / drift. Commit sets `committed_path`. |
| **Profile pool** | Ambient storylets keyed `duty_<profile>_<tag>_<n>.json`. ~20 per profile. |
| **Transition pool** | Storylets keyed `transition_<old_profile>_to_<new_profile>_<n>.json`. Fired by `DutyProfileBehavior` on a committed transition while a named order is active. |
| **Floor storylet** | Tiny narrative news-feed storylet keyed `floor_<profile>_<n>.json` that bundles the daily skill-XP drift with one-line texture ("You drill for an hour. Polearm improves."). |
| **Faction troop tree** | The result of `TroopSelectionManager.BuildCultureTroopTree(culture)` — every troop reachable from `culture.BasicTroop` + `culture.EliteBasicTroop` via `UpgradeTargets`. The lord's faction's tree, capped at the player's tier, defines what loot drops resolve to. |

## 5. Architecture

```
                    ┌─────────────────────────────────────────────────────────┐
                    │  EnlistmentBehavior.IsEnlisted == true                  │
                    │    └─► OrderActivity (offset 46) registered              │
                    │         persists across HomeActivity start/stop          │
                    └─────────────────────────────────────────────────────────┘
                                            │
        ┌───────────────────────────────────┼───────────────────────────────────┐
        ▼                                   ▼                                   ▼
┌───────────────┐                ┌───────────────────┐                ┌──────────────────┐
│ DutyProfile   │                │ NamedOrderArc     │                │ DailyDrift       │
│ Behavior      │                │ Runtime           │                │ Applicator       │
│               │                │                   │                │                  │
│ HourlyTick:   │                │ Listens for       │                │ DailyTick:       │
│ resolve →     │                │ accept on arc-    │                │ pick 2-3 skills  │
│ hysteresis →  │                │ tagged storylet → │                │ per profile +    │
│ commit →      │                │ splices phases    │                │ class affinity → │
│ profile_      │                │ onto Order-       │                │ award 5-15 XP    │
│ changed beat  │                │ Activity →        │                │ each via floor   │
│               │                │ runs to resolve → │                │ storylet         │
│               │                │ applies outcome   │                │                  │
└───────┬───────┘                └─────────┬─────────┘                └─────────┬────────┘
        │                                  │                                    │
        │      profile_changed              │      arc completed                │ news-feed
        │      (with active named order)    │                                   │ entry
        ▼                                  ▼                                    ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│ StoryDirector.EmitCandidate(...)                                              │
│   transition pool       resolve scripted effects     ambient pool             │
│         │                       │                          │                  │
│         ▼                       ▼                          ▼                  │
│   StoryletEventAdapter        EffectExecutor          news-feed accordion    │
│   → Modal InteractiveEvent    → 8 new primitives                              │
└──────────────────────────────────────────────────────────────────────────────┘
                                            │
                            ┌───────────────┴───────────────┐
                            ▼                               ▼
                   ┌─────────────────┐            ┌──────────────────┐
                   │ PathScorer      │            │ LordStateListener│
                   │                 │            │                  │
                   │ on intent pick  │            │ captured →       │
                   │ on skill XP gain│            │   imprisoned     │
                   │ → increments    │            │   profile        │
                   │ path_<n>_score  │            │ died → tear down │
                   │ → fires cross-  │            │ kingdom changed →│
                   │ roads on rank-  │            │   variant cache  │
                   │ up at T3/T5/T7  │            │   invalidate     │
                   └─────────────────┘            └──────────────────┘
```

Eight new C# files (six under `src/Features/Activities/Orders/`, one under `src/Features/Equipment/`, plus a thin event addition to `EnlistmentBehavior`):

- `src/Features/Activities/Orders/OrderActivity.cs` — Activity subclass; holds runtime state listed in §10. **Owns arc reconstruction in `OnGameLoaded`** — re-resolves `ActiveNamedOrder.OrderStoryletId` against `StoryletCatalog`, re-extracts the `arc` block, re-builds the spliced `Phase` list, and re-seats it via `Activity.Phases` setter. Without this, a save/load mid-arc loses the arc's phase list (the `[NonSerialized]` `_cachedPhases` field on the base, `Activity.cs:33`).
- `src/Features/Activities/Orders/DutyProfileSelector.cs` — pure function `Resolve(MobileParty) → DutyProfileId`. No state.
- `src/Features/Activities/Orders/DutyProfileBehavior.cs` — `CampaignBehaviorBase`. Owns `CampaignEvents.HourlyTickEvent` subscription, hysteresis counters, profile commit, `profile_changed` beat emission, transition-storylet dispatch.
- `src/Features/Activities/Orders/CombatClassResolver.cs` — shared helper extracted from `EnlistedFormationAssignmentBehavior.DetectFormationFromEquipment` (`EnlistedFormationAssignmentBehavior.cs:1343-1385`). The old behavior becomes a thin wrapper that calls the new resolver. **No duplicate logic.**
- `src/Features/Activities/Orders/NamedOrderArcRuntime.cs` — Listens for option-effect dispatches with `apply: "start_arc"`. Splices the arc's phases onto `OrderActivity` (mutates `OrderActivity._cachedPhases` via the `Phases` setter; persists `ActiveNamedOrder.OrderStoryletId` so `OnGameLoaded` can reconstruct). Tracks phase progress via `ActivityRuntime`'s normal advancement. On final phase, fires the resolve storylet. Handles abort on `profile_changed` while active.
- `src/Features/Activities/Orders/DailyDriftApplicator.cs` — `CampaignBehaviorBase`. `CampaignEvents.DailyTickEvent`. Selects 2–3 skills weighted by current profile + combat class, fires a `floor_<profile>_<n>` storylet that bundles 5–15 XP per skill with one-line news-feed texture.
- `src/Features/Activities/Orders/PathScorer.cs` — `CampaignBehaviorBase`. Subscribes to intent-pick (via `EffectExecutor` callback after `start_arc`) and skill-XP-gain (via `EnlistmentBehavior.OnXPGained`, `EnlistmentBehavior.cs:8451`). Increments `path_<name>_score` qualities. On rank promotion at T3 / T5 / T7 (via the new `EnlistmentBehavior.OnTierChanged` event Spec 2 introduces — see below), queries scores and fires the appropriate crossroads storylet.
- `src/Features/Activities/Orders/LordStateListener.cs` — `CampaignBehaviorBase`. Subscribes to **`CampaignEvents.HeroKilledEvent`** (`CampaignEvents.cs:707`), **`CampaignEvents.HeroPrisonerTaken`** (`:723`), **`CampaignEvents.HeroPrisonerReleased`** (`:725`), and `CampaignEvents.OnClanChangedKingdomEvent`. Sets / clears `lord_imprisoned`, invalidates culture/trait variant caches, fires teardown handshake on death (via `EnlistmentBehavior.StopEnlist(reason, isHonorableDischarge: true)`, `EnlistmentBehavior.cs:3333`).
- `src/Features/Equipment/CultureTroopTreeHelper.cs` — extracted from the currently-private `TroopSelectionManager.BuildCultureTroopTree` (`TroopSelectionManager.cs:494`). Public static method `BuildCultureTroopTree(CultureObject culture) → List<CharacterObject>`. `TroopSelectionManager` becomes a thin caller. Spec 2's loot system AND Spec 5's QM use it.

Plus extensions to existing infrastructure:

- `src/Features/Content/EffectExecutor.cs` — twelve new scripted-effect primitives (§7).
- Storylet schema parser — three new fields (`arc`, `class_affinity`, `intent_affinity`) plus four already-supported fields used at higher density (`profile_requires`, `requires_combat_class`, `requires_culture`/`excludes_culture`, `requires_lord_trait`/`excludes_lord_trait`).
- **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`** — add a new `public static event Action<int, int> OnTierChanged` (params `oldTier`, `newTier`), fired from the same code path that currently increments `EnlistmentTier`. Targeted addition; no rewrite. Spec 2's `PathScorer` is the first consumer; Spec 4 will be the second.

## 6. Duty profile system

Six profiles, **first match wins** in this resolution order:

| Priority | Profile | Predicate (against `lordParty: MobileParty`) | Pool prefix | Narrative feel |
| :-: | :--- | :--- | :--- | :--- |
| 1 | `besieging` | `lordParty.BesiegedSettlement != null` | `duty_besieging_*` | engineering, archery, waiting for the breach |
| 2 | `raiding` | `lordParty.DefaultBehavior == AiBehavior.RaidSettlement` ∪ active raid `MapEvent` | `duty_raiding_*` | looting, prisoners, fire and ethics |
| 3 | `escorting` | `lordParty.Army != null && lordParty.Army.LeaderParty != lordParty` | `duty_escorting_*` | subordinate in army; marching to someone else's plan |
| 4 | `garrisoned` | `lordParty.CurrentSettlement != null && !lordParty.IsMoving && friendly` | `duty_garrisoned_*` | drill yard, mess hall, social politics |
| 5 | `marching` | `lordParty.IsMoving && lordParty.DefaultBehavior != AiBehavior.Hold` | `duty_marching_*` | road, tents at dusk, campfire chatter |
| 6 | `wandering` | fallback (lord standing around with no clear objective) | `duty_wandering_*` | mercenary-band feel, looking for work |

**Sampling contract.** `DutyProfileBehavior` subscribes to `CampaignEvents.HourlyTickEvent`. Each tick:

1. Compute observed profile via `DutyProfileSelector.Resolve(lordParty)`.
2. If observed == `_committedProfile`: clear `_pendingMatches`, return.
3. Else: increment `_pendingMatches[observed]`. If counter ≥ 2, commit. On commit, clear all pending, set `_committedProfile`, fire `profile_changed` beat with `(old, new)` in ctx.

**Hard-trigger bypass.** If `_committedProfile` is `besieging` and `lordParty.BesiegedSettlement` is now null, OR `_committedProfile` is `escorting` and `lordParty.Army` is now null, OR `lordParty.Ai.RethinkAtNextHourlyTick == true` was set last tick: bypass hysteresis, recompute and commit on next tick. These are the cases where the lord's state changed in a way that *can't* be explained by hysteresis lag.

**Transition handling.** When `profile_changed` fires AND `OrderActivity.ActiveNamedOrder != null`, `DutyProfileBehavior` queries the transition pool keyed `(old_profile → new_profile, active_order_id)`. If a matching transition storylet exists, fire it as a Modal `InteractiveEvent` via `StoryDirector.EmitCandidate` with `ChainContinuation = true` (per Critical Rule #10). The transition storylet is responsible for:

- Applying any partial-outcome scripted effects (e.g. half the named order's intended XP)
- Calling `clear_active_named_order` (a new storylet effect primitive — see §7)
- Surfacing the moment narratively ("The retreat horn sounds. You abandon your post.")

If no matching transition storylet exists, fall back to a generic `transition_generic_interrupted` storylet (one of the ~15 transition storylets authored). The active named order is closed silently with zero outcome.

## 7. Scripted-effect primitives

Spec 2 extends `EffectExecutor` with **twelve new primitives**, split into two categories.

### 7.1 Eight character-development reward currencies

These mirror Bannerlord's own character-creation reward vocabulary (`NarrativeMenuOptionArgs` from `TaleWorlds.CampaignSystem.CharacterCreationContent`) plus the relation pipeline:

| Primitive | Schema | Bannerlord API | Use case |
| :--- | :--- | :--- | :--- |
| `grant_skill_level` | `{ skill: "Bow", amount: 2 }` | `Hero.MainHero.HeroDeveloper.ChangeSkillLevel(skill, amount)` | rare large bumps (rank promotions; not for daily content) |
| `grant_focus_point` | `{ skill: "Polearm", amount: 1 }` | `HeroDeveloper.AddFocus(skill, amount)` | named-order completions; crossroads commits |
| `grant_unspent_focus` | `{ amount: 1 }` | `HeroDeveloper.UnspentFocusPoints += amount` | rank promotions when path is unclear |
| `grant_attribute_level` | `{ attribute: "Vigor", amount: 1 }` | `HeroDeveloper.AddAttribute(attr, amount)` | rare; rank promotions, path crossroads |
| `grant_unspent_attribute` | `{ amount: 1 }` | `HeroDeveloper.UnspentAttributePoints += amount` | rank promotions when player should choose later |
| `set_trait_level` | `{ trait: "Valor", level: 1 }` | `Hero.SetTraitLevel(trait, level)` | crossroads commits; major narrative moments |
| `grant_renown` | `{ amount: 2 }` | `GainRenownAction.Apply(Hero.MainHero, amount)` | named-order completions; battle aftermath |
| `relation_change` | `{ target_slot: "<slot_name>", delta: -1 \| 1 \| 2 }` | resolves `Hero` from `StoryletContext.ResolvedSlots[target_slot]` → `ChangeRelationAction.ApplyPlayerRelation(hero, delta)` | core feedback loop; common |

**`relation_change` resolver contract.** The `target_slot` references a slot **declared in the storylet's `scope.slots` table** (`ScopeDecl` / `SlotDecl` model, `src/Features/Content/ScopeDecl.cs:6` / `SlotDecl.cs:3`). Valid `SlotDecl.Role` values per the existing model: `enlisted_lord`, `squadmate`, `named_veteran`, `chaplain`, `quartermaster`, `enemy_commander`, `settlement_notable`, `kingdom`. Authors declare the role they want filled, give it a slot name, and pass that slot name to `relation_change.target_slot`. The slot filler (existing infrastructure) resolves the role to a concrete hero at storylet evaluation time and populates `StoryletContext.ResolvedSlots`. If the slot is unresolved at apply time (filler returned null), `relation_change` no-ops with `Expected("EFFECT", "relation_target_unresolved", ctx)`. **No pseudo-selectors like `companion_in_party` or `officer_present` — the slot filler is the only resolver.**

Example storylet snippet:

```jsonc
{
  "scope": {
    "slots": {
      "lord": { "role": "enlisted_lord" },
      "buddy": { "role": "squadmate", "tag": "companion_present" }
    }
  },
  "options": [
    {
      "id": "thank_lord",
      "effects": [
        { "relation_change": { "target_slot": "lord", "delta": 2 } },
        { "relation_change": { "target_slot": "buddy", "delta": 1 } }
      ]
    }
  ]
}
```

If a needed slot role isn't covered by the existing `SlotDecl.Role` enumeration (e.g. "the officer who ordered this duty"), the spec extends `SlotDecl.Role` with explicit new values; it does not invent ad-hoc selector strings.

### 7.2 Four runtime-control primitives

These manipulate Spec 2's runtime state, not character development:

| Primitive | Schema | Behavior | Use case |
| :--- | :--- | :--- | :--- |
| `grant_item` | `{ item_id: "vlandian_sword_t3", count: 1 }` | resolve `ItemObject` by stringId → `PartyBase.MainParty.ItemRoster.AddToCounts(item, count)` | rare; specific narrative items only (heirloom blade) |
| `grant_random_item_from_pool` | `{ pool_id: "issue_weapon_melee" }` | resolve via §8 loot pipeline; same `AddToCounts` call | common; battle spoils, scavenged finds |
| `clear_active_named_order` | `{}` | sets `OrderActivity.ActiveNamedOrder = null`; calls `NamedOrderArcRuntime.UnspliceArc()` to truncate `Phases` back to the base profile phase | transition storylets only |
| `start_arc` | `{}` | reads parent storylet's `arc` block, calls `NamedOrderArcRuntime.SpliceArc(storylet)`, sets `OrderActivity.ActiveNamedOrder` with `OrderStoryletId` for save-load reconstruction | named-order accept options only |

`GiveGoldAction` (existing scripted effect `give_gold`) and the existing Spec 0 primitives (`quality_add`, `set_flag`, `clear_flag`, `apply_scripted`) remain unchanged.

**Per-storylet rate caps** (enforced at validation time, not runtime — keep authoring honest):

- `grant_attribute_level`: ≤ 1 per storylet, reserved for `crossroads_*` and `rank_promotion_*` storylets. (Phase 17 validator.)
- `grant_focus_point`: ≤ 1 per storylet (any named-order resolve or crossroads).
- `grant_skill_level`: ≤ 3 per storylet (rank promotions only — not for ambient content).
- `grant_renown`: ≤ 3 per storylet (named-order completion or major event).
- `relation_change`: |delta| ≤ 5 per storylet.
- `grant_item` / `grant_random_item_from_pool`: ≤ 1 of each per storylet; named-order completions or rare ambient.

These caps keep the daily skill-XP drift cheap and meaningful named-order / promotion / crossroads moments load-bearing — not a flood of permanent-currency rewards everywhere.

## 8. Loot system

Loot pools live in `ModuleData/Enlisted/Loot/loot_pools.json` (single file). A pool entry:

```jsonc
{
  "id": "issue_weapon_melee",
  "source": "faction_troop_tree",         // or "global_catalog"
  "filter": {
    "categories": ["OneHandedWeapon", "TwoHandedWeapon", "Polearm"],
    "max_troop_tier": "player_tier"        // or an integer
  }
}
```

**`source: faction_troop_tree`** (the default for issue-gear pools) calls `TroopSelectionManager.BuildCultureTroopTree(EnlistmentBehavior.Instance.EnlistedLord.Culture)`, walks each returned troop's `BattleEquipments` slot rosters, collects every distinct `ItemObject` matching the category filter at or below the tier cap, and picks one weighted by item tier (low tier weight slightly higher to avoid every drop being top-shelf).

**`source: global_catalog`** (for outliers — coins, trade goods, narrative trinkets) queries `MBObjectManager.Instance.GetObjectTypeList<ItemObject>()` directly, filtered by category and `tier_range`. Used sparingly.

Initial pool catalog (~12 pools):

- Issue: `issue_helmet`, `issue_body`, `issue_arm`, `issue_leg`, `issue_weapon_melee`, `issue_weapon_ranged`, `issue_horse`, `issue_horse_harness`
- Outlier: `trade_goods_common`, `narrative_trinkets`, `foreign_curios`, `courier_documents` (a custom item set if needed; reuses item ids for parchment, etc.)

The pool catalog is **culture-agnostic** at authoring time — culture comes from the lord at resolve time. So `issue_weapon_melee` produces Vlandian weapons under a Vlandian lord, Khuzait under a Khuzait. Cross-enlistment continuity is automatic.

## 9. Path system + crossroads

Five paths, each with a 0–100 score quality:

| Path | Score quality | Accumulator triggers |
| :--- | :--- | :--- |
| Ranger | `path_ranger_score` | `train_hard` ∪ `diligent` intent in `marching` ∪ `wandering` profile; Bow / Scouting / Riding XP gains |
| Enforcer | `path_enforcer_score` | `diligent` ∪ `train_hard` intent in `besieging` ∪ `garrisoned`; OneHanded / Polearm / Athletics / Leadership XP gains |
| Support | `path_support_score` | `diligent` intent in any profile + Medicine / Engineering / Trade / Steward XP gains |
| Diplomat | `path_diplomat_score` | `sociable` intent; Charm / Leadership / Steward XP gains |
| Rogue | `path_rogue_score` | `slack` ∪ `scheme` intent; Roguery / Trade XP gains; tolerates scrutiny |

**Increment rates** (per `PathScorer` event subscription):

- Intent pick during named-order accept: +3 to the matching path's score.
- Skill XP gain ≥ 50 in a single event: +1 to the matching path's score.
- Daily drift skill XP: no path increment (too noisy).

Score caps at 100; once at cap, further increments noop (diminishing returns built in by the cap).

**Crossroads firing.** `PathScorer` subscribes to `EnlistmentBehavior.OnTierChanged`. On tier transitions to T4 (from T3), T6 (from T5), or T8 (from T7):

1. Find the highest-scoring path. If no path is ≥ 30, no crossroads fires (player is too undefined).
2. If a clear leader exists (highest score is ≥ 10 ahead of second), fire `crossroads_<path>_<tier_band>` storylet (e.g. `crossroads_ranger_t3_to_t4`).
3. The storylet shows the player which way they're leaning, then offers three options:
   - **Commit** → sets `committed_path` quality to `<path>`. Locks. Unlocks T7+ named-order variants gated on this path. Applies a path-defining permanent reward (e.g. Ranger commit at T3 → +1 focus on Bow, +1 focus on Scouting, +renown, sets `Trait.Calculating` +1).
   - **Resist** → sets `path_resisted_<path>` flag. Future score increments on this path are halved. Other paths' scores re-normalize.
   - **Drift** → noop. Scores continue to evolve. Crossroads can re-fire at next band.

Crossroads storylet count: 5 paths × 3 tier-band variants = 15 base + culture-flavored variants on hot ones = ~25–30 total.

## 10. Save surface

`OrderActivity` (class offset **46**) persists:

```csharp
public sealed class OrderActivity : Activity
{
    public string CurrentDutyProfile { get; set; } = "wandering";
    public Dictionary<string, int> PendingProfileMatches { get; set; } = new();
    public int LastProfileSampleHourTick { get; set; }
    public NamedOrderState ActiveNamedOrder { get; set; }   // nullable struct
    public CombatClass CachedCombatClass { get; set; } = CombatClass.Infantry;
    public int CombatClassResampleHourTick { get; set; }
}
```

`NamedOrderState` (class offset **47**, nullable):

```csharp
public sealed class NamedOrderState
{
    public string OrderStoryletId { get; set; }
    public CampaignTime StartedAt { get; set; }
    public int CurrentPhaseIndex { get; set; }
    public string Intent { get; set; }
    public Dictionary<string, float> AccumulatedOutcomes { get; set; } = new();
}
```

Enum offsets: `CombatClass` at **84**, `DutyProfileId` at **85**.

**Mandatory `EnsureInitialized()` per Critical Rule (CLAUDE.md "[Serializable] save stores deserialize with null Dictionary/List properties").** `OrderActivity.SyncData(IDataStore)` calls `EnsureInitialized()` after `dataStore.SyncData(...)`; the same method runs on `OnSessionLaunched` and `OnGameLoaded`. Method body reseats `PendingProfileMatches` if null, ensures `ActiveNamedOrder.AccumulatedOutcomes` if `ActiveNamedOrder != null`.

**Mandatory arc reconstruction in `OrderActivity.OnGameLoaded`.** The base `Activity._cachedPhases` field is `[NonSerialized]` (`src/Features/Activities/Activity.cs:33`); only `CurrentPhaseIndex` survives. The base class's `ResolvePhasesFromType` (`Activity.cs:60-65`) restores phases from the static `ActivityTypeDefinition`. For an `OrderActivity` whose `ActiveNamedOrder != null` at load time, the static type definition does NOT contain the spliced arc phases — those came from the named-order storylet's `arc` block at accept time. So `OrderActivity` overrides `OnGameLoaded` (or hooks `ActivityRuntime.OnGameLoaded`) to:

1. Let the base class call `ResolvePhasesFromType` (restores the base `on_duty` phase).
2. If `ActiveNamedOrder != null`, look up the storylet by `ActiveNamedOrder.OrderStoryletId` via `StoryletCatalog`, extract the `arc` block, build the spliced `Phase` list, and re-seat via `Activity.Phases` setter so `CurrentPhaseIndex` resolves to the correct spliced phase.
3. If the storylet id no longer exists (content was deleted between save and load), fail gracefully: clear `ActiveNamedOrder`, fire `Caught("ARC", "stale_arc_id_on_load", ctx)`, leave the player on the base `on_duty` phase.

Save-load test scenario added to Phase D playtest list: accept a named order, advance to mid-arc phase, save, exit to main menu, reload, verify the named order's mid-arc phase is restored and ticks normally to resolve.

**Quality registrations** (added to Spec 0's registry):

- `path_ranger_score`, `path_enforcer_score`, `path_support_score`, `path_diplomat_score`, `path_rogue_score` — read-write, range [0, 100], decay none.
- `committed_path` — read-only after first set, string-valued (typed as `int` mapping to enum). Phase 17 validator gates writes.
- `days_at_current_profile` — read-write, incremented by `DutyProfileBehavior.OnDailyTick`, reset on profile commit.

**Flag registrations:**

- `prior_service_culture_<id>` (one per culture string id) — set on discharge.
- `prior_service_rank_<n>` (one per tier 1–9) — set to highest tier reached.
- `prior_service_path_<id>` — set on discharge if `committed_path` was set.
- `lord_imprisoned` — set/cleared by `LordStateListener`.
- `path_resisted_<path>` (one per path) — set by crossroads "resist" option.

**Save-definer registrations** in `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` (grep before claiming offsets — see CLAUDE.md):

```csharp
// Spec 2 — Orders Surface
DefineClassType(typeof(OrderActivity));         // offset 46
DefineClassType(typeof(NamedOrderState));       // offset 47
DefineEnumType(typeof(CombatClass));            // offset 84
DefineEnumType(typeof(DutyProfileId));          // offset 85
```

Offsets 48–60 (classes) and 86+ (enums) remain reserved for Specs 3–5.

## 11. Lord-state contract

`LordStateListener` handles three cases. Hook names verified against `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs`:

**Lord captured** (`CampaignEvents.HeroPrisonerTaken`, `CampaignEvents.cs:723`):
- Subscribe via `CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken)` in `RegisterEvents`.
- Filter: `prisoner == EnlistmentBehavior.Instance.EnlistedLord`.
- Set `lord_imprisoned` flag.
- Force `_committedProfile` to `imprisoned` (a special profile id; pool prefix `duty_imprisoned_*`; ~10 storylets). The DutyProfileSelector resolution order at §6 doesn't return `imprisoned` — it's only set externally here.
- Suppress named-order arc emission while flag is set (named-order storylets gated by `excludes_flag: ["lord_imprisoned"]`).

**Lord released** (`CampaignEvents.HeroPrisonerReleased`, `CampaignEvents.cs:725`):
- Subscribe via `CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased)`. Signature: `(Hero, PartyBase, IFaction, EndCaptivityDetail, bool)`.
- Filter: `hero == EnlistmentBehavior.Instance.EnlistedLord`.
- Clear `lord_imprisoned` flag, clear `_committedProfile = "wandering"` (recomputed on next sample tick), reset `_pendingMatches`.

**Lord killed** (`CampaignEvents.HeroKilledEvent`, `CampaignEvents.cs:707`):
- Subscribe via `CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled)`. Signature: `(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)`.
- Filter: `victim == EnlistmentBehavior.Instance.EnlistedLord`.
- Fire one farewell storylet (`lord_killed_farewell`) as Modal via `StoryDirector.EmitCandidate` with `ChainContinuation = true`.
- Trigger `EnlistmentBehavior.StopEnlist(reason: "lord_killed", isHonorableDischarge: true)` (`EnlistmentBehavior.cs:3333`) on the next campaign tick — `StopEnlist` is the actual public discharge entry point; `Discharge` does not exist as a method.
- `OrderActivity` torn down by `StopEnlist` path's existing tear-down hooks; Spec 2 wires `ActivityRuntime.Stop(orderActivity)` as a discharge listener (see Discharge handler below).

**Kingdom changed** (`CampaignEvents.OnClanChangedKingdomEvent`):
- Filter: `clan == EnlistmentBehavior.Instance.EnlistedLord.Clan`.
- Fire `lord_kingdom_changed` storylet (Modal).
- Invalidate culture/trait variant caches if implemented (otherwise no-op — variant resolution is per-storylet at fire time).
- Reset `_pendingMatches` to force re-sampling.

**Discharge handler.** `EnlistmentBehavior.StopEnlist` (`EnlistmentBehavior.cs:3333`) is the existing discharge entry point. `EnlistmentBehavior` does NOT currently expose a public discharge event for external behaviors to subscribe to. Spec 2 adds one:

- Add `public static event Action<string, bool> OnEnlistmentEnded` (params `reason`, `isHonorableDischarge`) to `EnlistmentBehavior`. Fired from `StopEnlist` immediately before its existing tear-down work runs (so subscribers see lord/tier state before reset).
- `LordStateListener` subscribes to `OnEnlistmentEnded` and:
  - Captures `prior_service_culture_<lord.Culture>`, `prior_service_rank_<currentTier>`, `prior_service_path_<committed_path>` flags if set.
  - Calls `ActivityRuntime.Instance.Stop(orderActivity, ActivityEndReason.Discharged)`.

This is a second targeted addition to `EnlistmentBehavior` (beyond `OnTierChanged`), but both are pure event additions — no logic change. Phase A introduces both events.

## 12. Authoring schema

**Storylet schema extensions** (added to Spec 0's existing schema):

```jsonc
{
  "id": "duty_garrisoned_archery_drill",
  "title": "{=}Archery yard.",
  "setup": "{=}You shoulder the practice bow at the butts.",
  "options": [ /* ... */ ],

  // existing Spec 0 fields: triggers, slots, weight, requires_*, excludes_*, effects, ...

  // NEW in Spec 2:
  "profile_requires": ["garrisoned"],          // duty profile gate; missing = any
  "class_affinity": {                           // multipliers on selection weight
    "Ranged": 1.4, "HorseArcher": 1.2          // missing classes default to 1.0
  },
  "intent_affinity": {                          // multipliers on selection weight
    "train_hard": 1.3, "diligent": 1.1
  },
  "requires_combat_class": null,                // hard gate; null = no gate
  "requires_culture": null,                     // hard gate ; "vlandia" or array
  "excludes_culture": null,
  "requires_lord_trait": null,                  // hard gate; "Cruel" or array
  "excludes_lord_trait": null,
  "arc": null                                   // present only on named-order storylets
}
```

**Arc block** (only on named-order storylets):

```jsonc
"arc": {
  "duration_hours": 12,
  "phases": [
    {
      "id": "mid",
      "duration_hours": 10,
      "pool_prefix": "order_guard_post_mid_",
      "fire_interval_hours": 3,
      "delivery": "auto"
    },
    {
      "id": "resolve",
      "duration_hours": 2,
      "pool_prefix": "order_guard_post_resolve_",
      "fire_interval_hours": 0,
      "delivery": "auto"
    }
  ],
  "on_transition_interrupt_storylet": "transition_guard_interrupted"
}
```

**Duty profiles file** (`ModuleData/Enlisted/Activities/duty_profiles.json`, six entries, lightweight):

```jsonc
{
  "profiles": [
    { "id": "garrisoned",  "pool_prefix": "duty_garrisoned_",  "daily_drift_skill_count": 2, "daily_drift_xp_range": [5, 15] },
    { "id": "marching",    "pool_prefix": "duty_marching_",    "daily_drift_skill_count": 2, "daily_drift_xp_range": [5, 15] },
    { "id": "besieging",   "pool_prefix": "duty_besieging_",   "daily_drift_skill_count": 3, "daily_drift_xp_range": [8, 20] },
    { "id": "raiding",     "pool_prefix": "duty_raiding_",     "daily_drift_skill_count": 3, "daily_drift_xp_range": [8, 20] },
    { "id": "escorting",   "pool_prefix": "duty_escorting_",   "daily_drift_skill_count": 2, "daily_drift_xp_range": [5, 12] },
    { "id": "wandering",   "pool_prefix": "duty_wandering_",   "daily_drift_skill_count": 2, "daily_drift_xp_range": [4, 10] },
    { "id": "imprisoned",  "pool_prefix": "duty_imprisoned_",  "daily_drift_skill_count": 1, "daily_drift_xp_range": [2, 6] }
  ]
}
```

(Imprisoned listed here because `DutyProfileBehavior` may force it via `LordStateListener`.)

**Storylet file naming convention** (per Spec 1's `Storylets/` non-recursive contract):

- `duty_<profile>.json` — array of ambient profile storylets (or split: `duty_<profile>_<tag>.json`)
- `order_<order_id>.json` — single named-order storylet with `arc` block
- `order_<order_id>_mid.json` — array of mid-arc storylets keyed off the `pool_prefix`
- `order_<order_id>_resolve.json` — array of resolve storylets keyed off the `pool_prefix`
- `transition.json` — array of transition storylets (or split per profile pair)
- `crossroads.json` — array of all 25–30 crossroads storylets
- `floor.json` — array of all ~50 floor storylets
- `lord_state.json` — array (`lord_imprisoned`, `lord_killed_farewell`, `lord_kingdom_changed`)

All files at top level of `ModuleData/Enlisted/Storylets/` per Spec 1's `StoryletCatalog.LoadAll` non-recursive constraint.

## 13. Migration plan

The migration has THREE explicit steps that must run in order; skipping the consumer-migration step (the middle one) breaks the build.

### 13.1 Migrate the eight existing consumer call sites (Phase B, before deletion)

Today's `OrderManager.Instance` is read directly from these 8 sites across 5 files. Each must be replaced with an equivalent read against `OrderActivity.Instance` (or a published event). Below is the contract per consumer:

| Call site | What it reads today | What it reads after migration |
| :--- | :--- | :--- |
| `EnlistedMenuBehavior.cs:956` ("Orders accordion header") | `OrderManager.Instance.GetCurrentOrder()` (returns `Order` or null) | `OrderActivity.Instance?.ActiveNamedOrder` (returns `NamedOrderState` or null); rendered text composed from the order's source storylet's `title` |
| `EnlistedMenuBehavior.cs:2167` (headlines / news) | order list iteration | `OrderActivity.Instance?.ActiveNamedOrder` + recent `profile_changed` log |
| `EnlistedMenuBehavior.cs:2408` (status text) | current-order display | same as `:956` |
| `EnlistedNewsBehavior.cs:768` (news feed) | order events log | StoryDirector news-feed accordion (already populated by ambient + transition storylets) |
| `EnlistedNewsBehavior.cs:4204` (daily report) | order completion outcomes | scripted-effect log entries written by `EffectExecutor` (same news-feed source) |
| `ForecastGenerator.cs:66` (forecast text) | "On duty: Guard Post (day 2 of 3)" | `OrderActivity.Instance?.ActiveNamedOrder?.OrderStoryletId` → resolve title via storylet catalog → "On duty: {title}" |
| `CampOpportunityGenerator.cs:1598` (camp-opportunity gating) | order-active suppression of camp opportunities | check `OrderActivity.Instance?.ActiveNamedOrder != null` |
| `ContentOrchestrator.cs:571` ("on-duty" gate for ambient content) | `OrderManager.Instance.IsOrderActive` | `OrderActivity.Instance?.ActiveNamedOrder != null` (same semantic) |

`OrderActivity.Instance` is a singleton accessor (analogous to `EnlistmentBehavior.Instance`). Public read-only properties: `CurrentDutyProfile`, `ActiveNamedOrder`, `CachedCombatClass`. Mutations are internal — callers read; they do not call `Accept`/`Decline`/`Apply` on Spec 2.

Phase B's task list (§15) explicitly enumerates each migration as its own task. Validation: after migration, `grep -r "OrderManager.Instance" src/` returns zero hits before old code is deleted.

### 13.2 Delete (per Spec 0 deletion list, Spec 2 rows; AFTER §13.1 completes)

C# files:
- `src/Features/Orders/Behaviors/OrderManager.cs`
- `src/Features/Orders/Behaviors/OrderProgressionBehavior.cs`
- `src/Features/Orders/OrderCatalog.cs`
- `src/Features/Orders/PromptCatalog.cs`
- `src/Features/Orders/Models/Order.cs`, `OrderPrompt.cs`, `OrderConsequence.cs`, `OrderOutcome.cs`, `OrderRequirement.cs`, `PhaseRecap.cs`, `PromptOutcome.cs`, `PromptOutcomeData.cs`

JSON files:
- `ModuleData/Enlisted/Orders/orders_t1_t3.json`, `orders_t4_t6.json`, `orders_t7_t9.json`
- `ModuleData/Enlisted/Orders/order_events/*.json` (16 files; ~84 events)

`Enlisted.csproj`: remove all `<Compile Include="src\Features\Orders\..."/>` entries (these are individually listed; they were never under a wildcard).

### 13.3 Refactor (touches Quartermaster & EnlistmentBehavior with zero behavior change)

- Extract `TroopSelectionManager.BuildCultureTroopTree` (currently `private`, `TroopSelectionManager.cs:494`) into a new file `src/Features/Equipment/CultureTroopTreeHelper.cs`. Promote signature to `public static List<CharacterObject> BuildCultureTroopTree(CultureObject culture)`. Rewire `TroopSelectionManager` to call the helper. **Pure refactor — no QM behavior change.**
- Add `public static event Action<int, int> OnTierChanged` to `EnlistmentBehavior` (params `oldTier`, `newTier`). Fire from the existing tier-increment code path. **Pure addition — no logic change.**
- Add `public static event Action<string, bool> OnEnlistmentEnded` to `EnlistmentBehavior` (params `reason`, `isHonorableDischarge`). Fire from `StopEnlist` (`EnlistmentBehavior.cs:3333`) immediately before the tear-down work runs. **Pure addition — no logic change.**

**Salvage** (rewrite under new schema, do not delete content):

The 84 hand-authored order events have prose worth keeping. Categorize:
- ~40-50 events → mid-arc storylets in named orders (most "guard duty" / "patrol" events)
- ~30-40 events → ambient profile storylets (most "drill" / "train recruits" events)
- ~4-5 events → cut as duplicative or outdated

Salvaged prose is rewritten under the new storylet schema. Storylet ids change (prefix `order_guard_post_mid_` instead of `guard_drunk_soldier`); old save references to old order ids become meaningless on load. Acceptable per Spec 0 §15 ("saves lose deleted-quality data").

**Add (per §5 architecture, eight files total):**

Six under `src/Features/Activities/Orders/` (`OrderActivity.cs`, `DutyProfileSelector.cs`, `DutyProfileBehavior.cs`, `CombatClassResolver.cs`, `NamedOrderArcRuntime.cs`, `DailyDriftApplicator.cs`, `PathScorer.cs`, `LordStateListener.cs` — actually seven under Orders), plus `src/Features/Equipment/CultureTroopTreeHelper.cs` (extracted per §13.3). Each gets an explicit `<Compile Include>` in `Enlisted.csproj` (per CLAUDE.md "non-recursive wildcard" warning — `Activities\*.cs` does NOT match `Activities\Orders\*.cs`, and `Equipment\*.cs` does NOT match nested directories either; verify with `grep` against the existing wildcard pattern before authoring the new entries).

**Add (`ModuleData/Enlisted/`):**

- New `Loot/` directory with `loot_pools.json`. Requires three `Enlisted.csproj` additions per CLAUDE.md "AfterBuild target needs explicit entries" warning:
  - `<LootData Include="ModuleData\Enlisted\Loot\*.json"/>` ItemGroup
  - `<MakeDir Directories="$(OutputPath)..\..\ModuleData\Enlisted\Loot"/>` inside `<AfterBuild>`
  - `<Copy SourceFiles="@(LootData)" DestinationFolder="$(OutputPath)..\..\ModuleData\Enlisted\Loot\"/>` inside `<AfterBuild>`
- New `Activities/duty_profiles.json` (mirrors existing Activities deploy pipeline).
- ~260 new storylets across `Storylets/` (existing Spec 1 deploy pipeline already handles this directory).

**Save-definer additions** in `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` — grep first to confirm 46/47/84/85 are unclaimed (Spec 1 took 45/82/83).

**Phase A dual-run.** Until end of Phase A, `OrderManager` and `OrderProgressionBehavior` continue to run alongside the new system. The new system runs in **shadow mode** — `DutyProfileBehavior` samples and commits, `DailyDriftApplicator` is disabled, no storylet content authored yet. Logs duty profile transitions and what *would* fire to `Debugging/`. Phase B authoring switches the system on; Phase B exit deletes the old code.

## 14. Validation

Extend `Tools/Validation/validate_content.py` with five new phases (Spec 0's Phases 1-12 + Spec 1's existing phases unchanged):

**Phase 13 — Storylet pool coverage.** For each duty profile, assert ≥ 5 storylets in the profile pool reference each of the 18 Bannerlord skills via `skill_xp` or `grant_focus_point` effects. Cumulative across the profile pool, not per-storylet. Build fails if any (profile, skill) cell has < 5 covering storylets. Surfaces the "I never get Bow on garrison runs" gap at build time.

**Phase 14 — Named-order arc integrity.** Every storylet with an `arc` block must:
- Declare `arc.duration_hours > 0` and `arc.phases.length >= 1`
- Each phase must declare `pool_prefix` matching ≥ 1 storylet authored elsewhere
- `arc.on_transition_interrupt_storylet`, if specified, must reference a real transition storylet id
- `start_arc` effect appears only on options of arc-tagged storylets
- The named order's id must match the convention `order_<arch>_<n>` (lint-only check)

**Phase 15 — Path crossroads completeness.** For each (path, tier_band) ∈ {ranger, enforcer, support, diplomat, rogue} × {t3_to_t4, t5_to_t6, t7_to_t8}, assert ≥ 1 crossroads storylet exists with id `crossroads_<path>_<band>`. Build fails if any cell missing.

**Phase 16 — Loot-pool resolution sanity.** Every pool with `source: faction_troop_tree` must declare `filter.categories`. Every `source: global_catalog` pool must declare `filter.categories` AND `filter.tier_range`. At build time, dry-run resolve each pool against the v1.3.13 item catalog (loaded as a fixture) — fail if any pool resolves to zero items for any of the 6 cultures × 9 tiers = 54 (culture, tier) keys.

**Phase 17 — Read-only quality & rate-cap enforcement** (extends Spec 0's Phase 12). Read-only qualities now include `committed_path`. Per-storylet rate caps (§7) enforced by scanning effect blocks.

`Tools/Validation/generate_error_codes.py` re-run after Spec 2 to capture new `ModLogger.Surfaced/Caught/Expected` call sites. Updates `docs/error-codes.md` per the auto-gen contract.

## 15. Phasing

Four phases, mirroring Spec 1's pattern. Each ends in a playtestable build with no broken intermediate state.

| Phase | Scope | Estimate |
| :--- | :--- | :--- |
| **A — Backbone, refactors & dual-run** | `OrderActivity` + `DutyProfileBehavior` + `DutyProfileSelector` + `CombatClassResolver` (extracted from `EnlistedFormationAssignmentBehavior`) + `LordStateListener` + `DailyDriftApplicator` (registered, disabled) + `PathScorer` (registered, no crossroads firing yet) + `NamedOrderArcRuntime` (no arcs to splice yet). All twelve scripted-effect primitives in `EffectExecutor`. Save offsets registered (46/47/84/85). Storylet schema extensions parsed. `Enlisted.csproj` additions. **Three refactors per §13.3:** extract `CultureTroopTreeHelper`, add `EnlistmentBehavior.OnTierChanged`, add `EnlistmentBehavior.OnEnlistmentEnded`. Old `OrderManager` + `OrderProgressionBehavior` left running in parallel (shadow mode for new system; no consumer migration yet). Smoke playtest: enlist, observe duty profile transitions in session log, verify no regression of existing Orders surface, verify QM gear lists unchanged after `BuildCultureTroopTree` extraction. | ~6 days |
| **B — Content migration + consumer rewire** | Author 6 profile pools (~120 ambient storylets across all skill axes). Author 10 named-order storylets with arc blocks + their mid-arc + resolve pools (~100 storylets). Author 15-20 transition storylets. Salvage 80 of the 84 old order events into the new corpus. Ship `loot_pools.json`. Implement Phase 13-17 validators. **Migrate the eight `OrderManager.Instance` consumer call sites per §13.1** (own task per call site). After migration: `grep -r "OrderManager.Instance" src/` returns zero. **End of phase: retire `OrderManager` + `OrderProgressionBehavior`; delete all files per §13.2.** Smoke playtest: enlist for 14+ in-game days, verify daily drift, 1+ named-order accept-and-complete cycle, 1+ transition storylet on profile change, all 18 skills receive XP, accordion / forecast / news-feed / camp-opportunity gating all read from `OrderActivity`. | ~12 days |
| **C — Path system + crossroads** | `PathScorer` event subscriptions wired: intent picks (via `EffectExecutor` post-`start_arc` callback) + skill XP gains (via `EnlistmentBehavior.OnXPGained`, `EnlistmentBehavior.cs:8451`) + tier-up (via the new `EnlistmentBehavior.OnTierChanged`). Author ~25-30 crossroads storylets across (5 paths × 3 bands) + culture variants on hot ones. `committed_path` quality + Phase-15/17 validators. T7+ named-order variants gated on committed path. `prior_service_*` flag emission via `EnlistmentBehavior.OnEnlistmentEnded` listener. Smoke playtest: enlist, level to T4 / T6 / T8, verify crossroads fire correctly with score-based path leader detection; commit one and verify lockout + path-gated content unlocks. | ~5 days |
| **D — Polish, save-load & playtest** | Floor-storylet authoring (~50 across profiles). Lord-state edge cases authored (~10 imprisoned-profile storylets, lord-killed farewell, lord-kingdom-changed). Replayability sweep: `culture_variants` overlays on ~15 hot-path storylets across all 6 cultures; `requires_lord_trait` / `excludes_lord_trait` gates on ~15 storylets where personality matters. **Save-load arc reconstruction test scenario added** (accept named order → mid-arc → save → reload → verify phase restored). Verification doc following Spec 1's plan-verification template. Final smoke playtest scenarios A-G covering each profile + each named-order archetype + each path commit + a mid-arc save/reload cycle. | ~5 days |

**Total: ~28 working days = ~5.5 calendar weeks** at the cadence Spec 1 ran (revised up from 25 days after adding the consumer-migration step in Phase B and the refactor work in Phase A). Larger than Spec 1 (which ran ~3 weeks for 50 storylets) primarily because the corpus is 5× the size and the consumer-migration tax is real; per-phase the engineering work is comparable.

## 16. End state — what the player gets

After Spec 2 ships:

- **Real Bannerlord progression while enlisted.** Skill XP at native rates, focus points (the missing currency that breaks today's progression cap), attribute points, trait shifts, renown, relation deltas on real heroes (lord, companions in the party, officers present in scenes), native-issue loot from the lord's faction troop tree resolved through the same `BuildCultureTroopTree` walk QM uses.
- **Replayable career arcs.** 5 paths × 6 cultures × 4 combat classes = 120 distinct trajectory profiles. Three explicit crossroads moments (T3 / T5 / T7) let the player commit, resist, or drift. Cross-enlistment continuity via `prior_service_*` flags enables veteran-flavored content on subsequent enlistments under different lords.
- **Lord chaos as content.** The 5 documented AI flip-flop modes (siege-abandonment-mid-assault, raid-to-besiege pivot, army-disband, settlement-changed-hands forced re-think, food-shortage cascade) all surface as transition storylets. Lord disorganization becomes the most interesting narrative vector in the surface — not a failure mode.
- **CK3-style background sim density.** Ambient news-feed entries every game day. Modal interrupts ~1 per 3-5 days at the spontaneous floor; more when the player picks an active intent. Named-order accept ~1 per week. Path crossroads ~1 per month. Rank promotions ~1 per few months (Spec 4 owns the per-promotion content). The base rate respects "Bannerlord is the foreground" — passive players aren't bombarded.
- **Coverage of all 18 skill axes from any duty profile.** A 30-day garrison stint progresses every skill at some rate. A 60-day siege campaign progresses every skill, with combat skills heavy and social skills present-but-thinner. No "I never get Bow because I never see a siege" gap.

Beyond the player surface, Spec 2 leaves Spec 4 (Promotion+Muster) with a ready vocabulary — the eight new scripted-effect primitives are exactly what promotion storylets need to deliver character-creation-style "what was decisive in earning this rank?" moments at every rank-up.

## 17. Risks and open questions

**R1 — Authoring fatigue.** ~260 storylets is 5× Spec 1's load. Phase B is the long phase (10 days) and the bulk is content authoring. Mitigation: phase the corpus so Phase B ships with a working *minimum viable* corpus (~150 storylets — enough that every profile has 15 storylets and every named order has 8) and a backlog. Polish phase (D) closes the gap to ~260.

**R2 — Daily drift balance.** 5–15 XP per skill per day across 2–3 skills = ~30 XP/day = ~210 XP/week per skill. Over a 90-day enlistment that's ~2,700 XP per drifting skill — meaningful but not break-the-game. Confirmed against Bannerlord native XP rates (`DefaultSkillEffects` and the focus/attribute XP-multiplier formula). Risk: feels boring if always the same XP. Mitigated by Revision 6 — drift is wrapped in a 1-line news-feed storylet for texture.

**R3 — Lord flip-flop spam.** If the lord flips behavior every 2 hours all day (worst-case probability), `DutyProfileBehavior` could emit a `profile_changed` beat every 2-4 hours, each triggering a transition storylet. Mitigation: the 2-hour hysteresis already cuts most flips. Additional rate-limit: `DutyProfileBehavior` enforces ≥ 4-hour gap between profile_changed beats; flips faster than that get coalesced (the system commits to the *latest* observed profile after the gap, suppressing intermediate beats).

**R4 — Combat-class drift mid-arc.** Player swaps loadout from sword to bow during a Guard Post named order. Combat class re-resamples, mid-arc storylets suddenly weight differently. Mitigation: combat class is cached on the named order at accept time (`NamedOrderState.Intent` already persists; add `NamedOrderState.CombatClassAtAccept`). Mid-arc storylet weighting uses the at-accept class, not the live class, for arc duration.

**R5 — Path scoring noise.** Player's intent picks across many named orders may not cluster cleanly. By T3 (first crossroads), the player may have only seen 3-5 named orders → 9-15 path increments → no clear leader (no path ≥ 30). Crossroads silently no-ops. Mitigation: this is the desired behavior; the player is undefined and should remain so. Crossroads can re-fire at T5 with more data. If at T7 still no leader, the player is genuinely a generalist — surface a "you remain undefined" floor storylet at T8 acknowledging this.

**R6 — Save-offset collisions with Specs 3-5.** If Spec 3 or later proceeds in parallel and claims 46/47/84/85 first, collision corrupts saves silently (per CLAUDE.md "claim a `SaveableTypeDefiner` offset without grepping" warning). Mitigation: implementation Phase A claims offsets first thing, before any other Spec 2 work; commit the definer change immediately.

**R7 — Loot pool resolves to nothing for a (culture, tier) pair.** Some cultures may not have e.g. `Polearm` items in their tree at low tiers. Phase 16 validator catches this at build, but resolution can also fail at runtime if items are unavailable due to mods or data corruption. Mitigation: `grant_random_item_from_pool` logs `Expected("LOOT", "pool_no_match", ctx)` and skips the drop silently — never surfaces.

**R8 — Consumer-migration tax bigger than estimated.** §13.1 enumerates 8 known call sites; an undocumented additional consumer of `OrderManager.Instance` could appear during Phase B and add scope. Mitigation: the first task of Phase B is `grep -r "OrderManager" src/` to enumerate the actual consumer count before authoring task slots. Any new consumers found get their own migration task before the storylet-authoring work begins.

**R9 — Save-load arc reconstruction has a gap.** If the user saves mid-arc, deletes the named-order storylet from content, then loads, the arc cannot be reconstructed. Mitigation: §10's "stale_arc_id_on_load" path logs `Caught` and clears the arc; the player loses the in-progress duty silently but the activity continues from the base `on_duty` phase. Acceptable degradation — the alternative is hard-locking the save against content edits, which is worse.

**Open question 1 — Should `escorting` collapse into `marching + is_subordinate flag`?** Recommended in brainstorming Revision 4, held during finalization. The decision was to keep 6 profiles with `escorting` deliberately thin (~10 storylets) since those runs are rare. Revisit during Phase D if `escorting`-pool authoring feels disproportionate to player exposure.

**Open question 2 — Crossroads re-fire policy.** If a player gets `crossroads_ranger_t3_to_t4`, picks "drift," and at T5 the leading path is still Ranger, do we fire `crossroads_ranger_t5_to_t6`? Current spec says yes. Alternative: only fire crossroads_<path>_<band> if path's score has *grown* since the last crossroads of any path. Defer this decision to Phase C playtest.

**Open question 3 — Floor storylet authoring scope.** ~50 floor storylets (10 per profile × 5 profiles) covers the four primary profiles + one shared. Imprisoned and one of the rarer profiles share a single set. Confirm during Phase D that 10 per profile is enough for non-repetition over a 60-day enlistment (player sees ~60 floor storylets total, so a pool of 10 means each appears ~6 times — borderline acceptable).

---

## Implementation plan

To be drafted via `superpowers:writing-plans` after this design is approved. Estimated 4 phases (A–D), 25 working days, mirroring Spec 1's plan structure.
