# CK3 Wanderer Mechanics — Architecture Brief

**Status:** Locked. This brief is the contract Plans 2-7 of the CK3 wanderer plan family reference. All decisions below are final; downstream plans do not relitigate.

**Source spec:** [CK3 Wanderer Mechanics Systems Analysis (v6)](../superpowers/specs/2026-04-24-ck3-wanderer-systems-analysis.md).
**Owning plan:** [Plan 1 — Architecture Foundation](../superpowers/plans/2026-04-24-ck3-wanderer-architecture-foundation.md).

**Plan family progress (2026-04-26):**

| Plan | Status | Verification |
| :-- | :-- | :-- |
| Plan 1 — Architecture Foundation | 🟡 code shipped on `development`, in-game smoke pending | [verification](../superpowers/plans/2026-04-24-ck3-wanderer-architecture-foundation-verification.md) |
| Plan 2 — Companion Substrate | 🟡 code shipped on `development`, in-game smoke pending | [verification](../superpowers/plans/2026-04-24-ck3-wanderer-companion-substrate-verification.md) |
| Plan 3 — Rank-Ceremony Arc | 🟡 code shipped on `development`, in-game smoke pending | [verification](../superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc-verification.md) |
| Plan 5 — Endeavor System | 🟡 Phase A + B (substrate + wiring) shipped on `feature/plan5-endeavor-system` worktree (NOT yet on `development`); 13 of 30 tasks complete; content authoring + smoke pending | [verification (PARTIAL)](../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system-verification.md) |
| Plans 4 + 6-7 | not started | — |

**Plan 2 hand-off surface (Plans 3-7 may use):**
- `Enlisted.Features.Companions.CompanionLifecycleHandler.Instance.GetSpawnedCompanions()` — `List<Hero>` of currently-spawned, alive companions. Plan 3 ceremony witness selection reads this.
- `Enlisted.Features.Enlistment.Behaviors.EnlistmentBehavior.Instance.GetSpawnedCompanions()` — same data, accessed without going through the lifecycle handler.
- `Enlisted.Features.Enlistment.Behaviors.EnlistmentBehavior.Instance.GetCompanionTypeId(Hero)` — returns "sergeant" / "field_medic" / "pathfinder" / "veteran" / "qm_officer" / "junior_officer" / null.
- `Enlisted.Features.Enlistment.Behaviors.EnlistmentBehavior.Instance.ClearCompanionSlot(Hero)` — null the slot matching a hero. Plan 6 patron loaned-knight cleanup uses its own pathway, but the helper is available for plans that need to remove a Plan-2 companion outside the death/discharge defaults.
- `Enlisted.Features.Companions.Data.CompanionDialogueCatalog.Instance.GetNode(nodeId, ctx)` — specificity-ranked variant lookup. Plans 3-7 hooking conversation rendering against the catalog use this directly.
- Six archetype catalogs at `ModuleData/Enlisted/Dialogue/companion_<id>.json` with stable node-id prefixes (`companion_<id>_intro_greeting`, `_root`, `_topic_*`, `_goodbye`). Plans 3-7 may add new `companion_*.json` files; the loader picks them all up.

**Plan 5 Phase A + B hand-off surface (Plans 6-7 + ongoing maintenance may use; full detail in [verification doc §6](../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system-verification.md#6--hand-off-surface-plans-6-7-and-ongoing-maintenance-may-use)):**
- `Enlisted.Features.Endeavors.EndeavorActivity.Instance` / `Enlisted.Features.Contracts.ContractActivity.Instance` — singletons returning the active player-issued / notable-issued endeavor or `null`. Phase index + total + score in `AccumulatedOutcomes` under reserved `__-prefixed` keys; per-phase choice memory in `FlagStore` under `endeavor_choice_<endeavor_id>_<phase>_<option_id>`.
- `Enlisted.Features.Endeavors.EndeavorRunner.Instance` — public surface includes `StartEndeavor(template, companions)` / `CancelEndeavor(reason)` / `CanStartCategory(category)` / `GetCategoryCooldownEnd(category)` / `GetSpawnedCompanions()`. Owns the per-category 3-day cooldown dictionary (`Dictionary<string, CampaignTime> _categoryLastResolved`).
- `Enlisted.Features.Endeavors.EndeavorCatalog.GetById(id)` / `GetByCategory(category)` / `All` / `Count` — JSON-loaded template catalog. Loaded by `EndeavorRunner.OnSessionLaunched`.
- `Enlisted.Features.Endeavors.EndeavorGatingResolver.IsAvailable(template, player, spawnedCompanions)` / `Resolve(template, player, spawnedCompanions)` — hybrid gating. `Resolution.LockReason` enum surfaces tooltip routing reasons (`BelowTier` / `FlagGateMissing` / `FlagBlockerSet` / `SkillAndCompanionMissing` / `RequiredCompanionMissing`).
- `Enlisted.Features.Endeavors.ScrutinyRiskCalculator.ComputeEffectiveRisk(baseRisk, player, agents)` / `RollDiscovery(...)` — pure risk math + roll. Charm coefficients are constants in the file.
- `Enlisted.Features.Endeavors.EndeavorPhaseProvider.FirePhase(activity)` / `FireResolution(activity)` / `PickResolutionStoryletId(template, score)` — modal-fire helpers, route through `ModalEventBuilder.FireEndeavorPhase`.
- `endeavor_active_score` quality (range −20..+20, default 0, no decay) — single global accumulator. **Reserved for `EndeavorRunner`** (resets on every endeavor start + finish); downstream plans should not write to it.
- 31 endeavor scripted effects in `ModuleData/Enlisted/Effects/scripted_effects.json` with prefixes `endeavor_score_*`, `endeavor_skill_xp_*_<axis>`, `endeavor_lord_relation_*`, `endeavor_scrutiny_drift_*`, `endeavor_gold_reward_*`, `endeavor_readiness_*`, `endeavor_medical_risk_*` — reusable by any future endeavor / contract storylet.
- Camp menu slot 5 "Endeavors" + sub-menu `enlisted_endeavors` (status text + Browse + Cancel + Back). Browse opens single `MultiSelectionInquiryData` with `[Category]` prefix.

**Plan 3 hand-off surface (Plans 4-7 may use):**
- `FlagStore.Instance.Has("ceremony_fired_t{N}")` — check whether the player has resolved their ceremony at tier N. Set when a ceremony option is picked.
- `FlagStore.Instance.Has("ceremony_choice_t{N}_<choice_id>")` — check whether the player picked a specific option. One bool per option (`FlagStore` is bool-only). Plan 4 reads `ceremony_choice_t7_humble_accept` / `_proud_accept` / `_try_to_refuse` to flavor officer-tier dialog. Plan 5 may gate endeavors on T2/T3/T5 ceremony picks. Plan 6 may flavor patron favor outcomes on prior ceremony choices.
- `Enlisted.Features.Ceremonies.CeremonyProvider.FireCeremonyForTier(int newTier)` — public accessor; downstream plans can fire a ceremony manually if needed (testing, debug). Dedup gate ensures idempotency.
- `Enlisted.Features.Ceremonies.CeremonyWitnessSelector.GetWitnessesForCeremony(int newTier)` — returns `Dictionary<string, Hero>` keyed by `witness_<archetype>` slot name. Plans 4-7 may reuse this helper for any ceremony-style modal that needs the same witness composition.
- `Enlisted.Features.Ceremonies.CeremonyCultureSelector.SelectVariantSuffix()` — returns `vlandian` / `sturgian` / `imperial` / `base`. Reusable by any culture-flavored content plan.
- 5 ceremony storylet files at `ModuleData/Enlisted/Storylets/ceremony_t{prev}_to_t{curr}.json` (T1→T2, T2→T3, T4→T5, T6→T7, T7→T8) — additional cultural variants may be added in Plan 7 polish pass; the loader picks up any storylet matching the ID prefix.
- 44 ceremony scripted effects in `ModuleData/Enlisted/Effects/scripted_effects.json` (10 trait drift + 34 witness reactions × 7 archetypes including `lord`) — reusable by any future modal that wants the same drift/reaction shapes.

**PathCrossroads collision avoidance — locked:** ceremonies do NOT fire at `newTier ∈ {4, 6, 9}` (Plan 3 Lock 1). `PathCrossroadsBehavior` already fires Modal storylets at those tiers; Plan 3's `RankCeremonyBehavior` skips them via `CeremonyProvider.CeremonyTiers = { 2, 3, 5, 7, 8 }`. Plans 4-7 hooking `OnTierChanged` should plan around this constraint — three subscribers exist on the event today (`PathScorer`, `PathCrossroadsBehavior`, `RankCeremonyBehavior`).

---

## 1. Save-definer offset ledger

Live registrations in `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` stop at offset 50 (`DutyCooldownStore`, owned by Career Loop Plan 4). The CK3 wanderer plan family adds the entries below.

### Class offsets

| Offset | Owner | Mechanic |
| :---: | :--- | :--- |
| 51 | `DutyActivity` | Menu+duty unification spec — NOT this brief's plan family. See [`docs/superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md`](../superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md). |
| 52 | `ChoreThrottleStore` | Menu+duty unification spec — NOT this brief's plan family (same source). |
| 53 | reserved | Personal Kit state lives in `QualityStore` — no offset claimed |
| 54 | `PatronRoll` | Roll of Patrons (this brief's Plan 6) |
| 55 | `PatronEntry` | Roll of Patrons (this brief's Plan 6) |
| 56 | `ContractActivity` | Endeavor System / notable-issued (this brief's Plan 5) |
| 57 | `EndeavorActivity` | Endeavor System / player-issued (this brief's Plan 5) |
| 58 | `LifestyleUnlockStore` | Lifestyle Unlocks (this brief's Plan 7) |
| 59 | reserved | Rank Ceremony state lives in `FlagStore` — no offset claimed |
| 60-70 | reserved | Future Activity-and-related (Specs 3-5) |

Offsets 51-52 are reserved by the menu+duty unification spec, which is **a parallel plan family separate from the seven CK3 wanderer plans this brief covers**. Both families share the 51-70 offset cluster announced in `AGENTS.md` Rule #11; coordinate before claiming offsets in the 60-70 range. Plan 1 of this brief does not register classes at offsets 51-52.

### Enum offsets

| Offset | Owner | Notes |
| :---: | :--- | :--- |
| 84 | `FavorKind` | Stub `None = 0` only at registration; full member list (`LetterOfIntroduction` / `GoldLoan` / `TroopLoan` / `AudienceArrangement` / `MarriageFacilitation` / `AnotherContract`) populated in Plan 6. |

**Enum offsets live in 80+ to keep them disjoint from the 1-70 class range.** TaleWorlds' `DefinitionContext` keys `_allTypeDefinitionsWithId` by `TypeSaveId` (which is just `BaseId + offset` as a number — class kind is not part of the key), so a class at offset N and an enum at offset N collide on module init with `Dictionary.Insert` `ArgumentException`. This is the actual rule the convention enforces.

**Taken:** 80-103 (`StoryTier`, `StoryBeat`, `QualityScope`, `ActivityEndReason`, `DutyProfileId`, Campaign Intelligence + Signal Projection enums); 84 = `FavorKind` (Plan 6); 110-112 (Retinue + Logistics enums, originally at 50-52, relocated 2026-04-25 after the SaveId collision with `DutyCooldownStore` class offset 50 surfaced); 113-119 (Content Orchestrator + Camp Life Simulation enums, originally at 60-64 + 70-71, relocated for the same reason).

**Free for future enum claims:** 104-109, 120+.

### Required generic-container registrations

```csharp
ConstructContainerDefinition(typeof(List<Features.Patrons.PatronEntry>));
```

`List<string>` (used by `LifestyleUnlockStore`) is already registered globally — no explicit registration needed.

### Saveable-container constraint

`HashSet<T>` is **not** a saveable container in the TaleWorlds SaveSystem. `ContainerType` only knows `List` / `Queue` / `Dictionary` / `Array` / `CustomList` / `CustomReadOnlyList`. Any save state requiring set semantics ships as `List<T>` with runtime dedup, or serialize-to-CSV + rebuild on load. Pattern: `FlagStore.EnsureInitialized` / `QualityStore.EnsureInitialized`.

---

## 2. Namespace conventions

| Namespace | Purpose | Mechanic |
| :-- | :-- | :-- |
| `Enlisted.Features.Endeavors` | `EndeavorActivity`, `EndeavorPhaseProvider`, catalog | §3.8 |
| `Enlisted.Features.Contracts` | `ContractActivity` (notable-issued sibling) | §3.8 |
| `Enlisted.Features.Patrons` | `PatronRoll`, `PatronEntry`, `FavorKind` | §3.3 |
| `Enlisted.Features.Lifestyles` | `LifestyleUnlockStore` | §3.6 |
| `Enlisted.Features.Ceremonies` | `RankCeremonyBehavior`, `CeremonyProvider` | §3.9 |
| `Enlisted.Features.PersonalKit` | `PersonalKitTickHandler` | §3.2 |
| `Enlisted.Features.CompanionAptitude` | (placeholder for Plan 2 — no Plan 1 types) | §3.5 |
| `Enlisted.Mod.Core.Helpers` | `ModalEventBuilder` | Plan 1 T5 |

None of these collide with menu+duty unification's `Enlisted.Features.Activities.Orders` or `Enlisted.Features.CampaignIntelligence.Duty`. Plans 2-7 must not introduce new namespaces without amending this brief.

---

## 3. Dialog token prefixes

Dialog token namespace is global per session. Tokens use these prefixes to avoid vanilla and intra-mod collision:

| Prefix | Purpose | Mechanic |
| :-- | :-- | :-- |
| `enlisted_*` | Existing enlistment dialogs (shipped) | — |
| `endeavor_*` | Endeavor System | §3.8 |
| `ceremony_*` | Rank Ceremonies | §3.9 |
| `companion_<archetype>_*` | Companion-specific (e.g. `companion_sergeant_intro`, `companion_field_medic_advice`) | §3.10 |
| `patron_*` | Roll of Patrons favor branches | §3.3 |
| `lifestyle_*` | Lifestyle unlock branches | §3.6 |

**Vanilla token reuse rule.** Always layer mod player-lines on existing vanilla input tokens (`lord_pretalk`, `lord_talk_speak_diplomacy_2`, `notable_pretalk`, `hero_main_options`, `companion_role_pretalk`). Use mod-prefixed *output* tokens to keep our sub-trees isolated.

**Text-variable interpolation contract (Plan 2 established, Plans 3-7 inherit).** Every dialog catalog node's `text` and `options[].text` may reference these tokens; the wiring code populates them via `MBTextManager.SetTextVariable` before opening the conversation:

| Token | Source | Notes |
| :-- | :-- | :-- |
| `{PLAYER_NAME}` | `Hero.MainHero.Name` | Must use the dialog token, not literal "soldier" / "lad" / etc. |
| `{PLAYER_RANK}` | `RankHelper.GetCurrentRank(EnlistmentBehavior.Instance)` | **Culture-aware** — reads `progression_config.json` for per-kingdom rank titles. Hard-coding "Sergeant" silently strips the mod's culture-rank work. |
| `{LORD_NAME}` | `EnlistmentBehavior.Instance.EnlistedLord.Name` | Use instead of "the lord" / "the commander" / "him" when the speaker is referencing the enlisted lord. |
| `{PLAYER_TIER}` | `EnlistmentBehavior.Instance.EnlistmentTier` (numeric 1-9) | Rarely surfaces inline; useful for context conditions in storylet schemas. |
| `{COMPANION_NAME}` / `{COMPANION_FIRST_NAME}` | The currently-spoken-to companion (Plan 2 surface) | `COMPANION_NAME` preserves title prefixes ("Brother Eadric"); `COMPANION_FIRST_NAME` strips them ("Eadric"). |

Wiring precedents: `EnlistedDialogManager.SetCommonDialogueVariables` for the QM flow; `EnlistedMenuBehavior.SetCompanionConversationTokens` for the Plan 2 Talk-to flow. Plans 3-7 firing modal storylets via `ModalEventBuilder` automatically inherit the QM-set variables (the global `MBTextManager` bag persists across calls); ceremony / endeavor / patron flows that open NEW conversations need their own SetCompanionConversationTokens-equivalent helper. Plan 2 Phase 5++ (commit `4dfe719`) added the AGENTS.md pitfall #23 codifying this rule.

---

## 4. Schema rules

1. **`List<T>` not `HashSet<T>`** in any save state. Runtime dedup if uniqueness needed.
2. **CSV-encoded dictionaries** in save state where dictionary semantics needed (e.g. `string PerFavorCooldownsCsv` rebuilt to `Dictionary<FavorKind, CampaignTime>` on load).
3. **`EnsureInitialized()` on every save-class** that has dict/list-typed properties. Reseats null fields with empty instances. Called from `SyncData` (after `dataStore.SyncData(...)`), `OnSessionLaunched`, and `OnGameLoaded`. Pattern: `FlagStore.EnsureInitialized` / `QualityStore.EnsureInitialized`.
4. **JSON dialog schema follows the `QMDialogueCatalog` node shape** (`id`, `speaker`, `textId`, `text`, `context`, `options[]`). Variants share id; specificity-ranked at runtime. Plan 2 ships the companion-dialog loader (`CompanionDialogueCatalog`) discriminating on `dialogueType: "companion"` and defines the companion-specific context-field set (archetype, relation, tier, active-endeavor flags). Plan 1 reserves the file-name convention `companion_<archetype>_*.json` in `ModuleData/Enlisted/Dialogue/`; the existing csproj `<DialogueData>` glob already deploys files matching that pattern, so no csproj changes needed.
5. **`schemaVersion: 1`** in every JSON catalog file. `dialogueType` field discriminates loader (e.g. `"quartermaster"`, `"companion"`).
6. **Flag and quality keys use flat underscore namespace.** `<system>_<scope>_<id>` style (e.g. `ceremony_fired_t6`, `prior_service_culture_vlandia`, `path_resisted_ranger`). No dotted notation. Matches existing `FlagStore` + `QualityStore` precedent.

---

## 5. "Do not" list

Plans 2-7 must not do any of the following:

1. **Re-register a vanilla TaleWorlds type in `EnlistedSaveDefiner`.** Crashes module init silently. Cross-check against `../Decompile/TaleWorlds.Core/SaveableCoreTypeDefiner.cs` and `../Decompile/TaleWorlds.CampaignSystem/SaveableCampaignTypeDefiner.cs` before adding any registration.
2. **Use `Occupation.Wanderer` for spawned heroes.** Triggers vanilla wanderer-introduction dialogue. Use `Occupation.Soldier`. Confirmed via `../Decompile/TaleWorlds.CampaignSystem/.../LordConversationsCampaignBehavior.cs:1274`.
3. **Dereference `Campaign.Current.X` at `OnGameStart`.** `DefaultTraits.Mercy/Valor/Honor/Calculating`, `DefaultSkills.*`, `DefaultPerks.*` all NRE at registration time. Pass providers (`Func<TraitObject>`) or resolve inside `OnSessionLaunched`.
4. **Use `HashSet<T>` in save state.** See schema rule 1.
5. **Write to a read-only `QualityStore` quality** from a storylet effect (`rank`, `days_in_rank`, `days_enlisted`). Validator Phase 12 blocks at build.
6. **Author scripted-effect ids without registering them** in `ModuleData/Enlisted/Effects/scripted_effects.json`. Validator Phase 12 rejects unknown `apply` values. Reuse seed catalog (`rank_xp_minor`, `lord_relation_up_*`, `scrutiny_down_*`, etc.) where possible.
7. **Use `int.MinValue` as a "never fired" sentinel** for throttle fields. Subtraction overflow trips `diff >= interval` checks backwards. Use `int.MinValue / 2`.
8. **Use `EventDeliveryManager.Instance.QueueEvent(evt)` directly** for new mechanic emissions. Bypasses `StoryDirector` pacing (no floor, no cooldown, no deferral). Use `StoryDirector.Instance?.EmitCandidate(...)` via `ModalEventBuilder` helper.
9. **Implement Lifestyle Perks** via `Hero.HeroDeveloper.AddPerk` (`PerkObject` path). API gap: `RequiredSkillValue` enforced internally; perks inert below skill threshold. Use the unlocks-version (option 3) only — `LifestyleUnlockStore` with feature-id strings.
10. **Migrate `OrderActivity` into `EndeavorActivity`.** Sibling not merge. `OrderActivity` is shipped with duty-profile state, named-order arc state, reconstruction code; migration risk is unjustified.

---

## 6. Canonical modal pipeline recipe

For any player-choice modal (ceremony, endeavor phase, decision outcome, patron favor outcome), use the helper at `src/Mod.Core/Helpers/ModalEventBuilder.cs`:

```csharp
ModalEventBuilder.FireCeremony(storyletId, ctx);
ModalEventBuilder.FireEndeavorPhase(storyletId, ctx, ownerActivity);
ModalEventBuilder.FireDecisionOutcome(storyletId, ctx);
ModalEventBuilder.FireSimpleModal(storyletId, ctx, chainContinuation);
```

Implementation:

```csharp
public static class ModalEventBuilder
{
    public static void FireSimpleModal(string storyletId, StoryletContext ctx, bool chainContinuation)
    {
        var s = StoryletCatalog.Instance?.GetById(storyletId);
        if (s == null)
        {
            ModLogger.Expected("MODAL", "storylet_not_found", "storylet missing for modal fire");
            return;
        }
        var evt = StoryletEventAdapter.BuildModal(s, ctx, owner: null);
        var cand = s.ToCandidate(ctx);
        cand.InteractiveEvent = evt;
        cand.ProposedTier = StoryTier.Modal;
        cand.ChainContinuation = chainContinuation;
        StoryDirector.Instance?.EmitCandidate(cand);
    }

    public static void FireCeremony(string storyletId, StoryletContext ctx)
        => FireSimpleModal(storyletId, ctx, chainContinuation: true);

    public static void FireEndeavorPhase(string storyletId, StoryletContext ctx, EndeavorActivity owner)
    {
        var s = StoryletCatalog.Instance?.GetById(storyletId);
        if (s == null)
        {
            ModLogger.Expected("MODAL", "storylet_not_found", "storylet missing for modal fire");
            return;
        }
        var evt = StoryletEventAdapter.BuildModal(s, ctx, owner);
        var cand = s.ToCandidate(ctx);
        cand.InteractiveEvent = evt;
        cand.ProposedTier = StoryTier.Modal;
        cand.ChainContinuation = true;
        StoryDirector.Instance?.EmitCandidate(cand);
    }

    public static void FireDecisionOutcome(string storyletId, StoryletContext ctx)
        => FireSimpleModal(storyletId, ctx, chainContinuation: false);
}
```

**Pacing rails (`DensitySettings`):** `ModalFloorInGameDays = 5`, `ModalFloorWallClockSeconds = 60`, `CategoryCooldownDays = 3`, `QuietStretchDays = 7`. Modal candidates pass through `ModalFloorsAllow(c, today)`; blocked candidates park in `_deferredInteractive` FIFO queue and retry on daily tick. **`ChainContinuation = true` bypasses the in-game floor and category cooldown** (still respects 60s wall-clock). Use for: ceremony-after-promotion, in-progress endeavor phase-2-3.

**Existing `OnTierChanged` consumers — Plan 3 must coordinate.** Two shipped behaviors already subscribe to `EnlistmentBehavior.OnTierChanged`:

- `src/Features/Activities/Orders/PathScorer.cs` — accumulates path-specialization scores at every tier (does not emit modals; safe to compose).
- `src/Features/CampaignIntelligence/Career/PathCrossroadsBehavior.cs` — fires a Modal `path_crossroads_{path}_t{tier}` storylet at **T4 / T6 / T9** where the player commits / resists / defers a career direction.

Plan 1 adds `RankCeremonyBehavior` as a third subscriber, registered after the two above, so its handler fires last. Plan 3 (Rank-Ceremony Arc) must decide how to coordinate at the three tiers where PathCrossroads already fires a Modal:

- **Option A — skip the ceremony storylet at T4 / T6 / T9** and let the existing crossroads cover those tiers; ceremonies fire only at T2/T3, T5, T7/T8 transitions.
- **Option B — chain the ceremony as the follow-up to the crossroads commit/resist outcome** (single sequenced experience).
- **Option C — emit a non-Modal news entry instead of a Modal at the colliding tiers**, preserving narrative without two back-to-back popups.

Plan 3's brainstorm session locks the choice. Plan 1 ships the seam wired log-only so the conflict is visible without a content collision.

---

## 7. AGENTS.md Rule #11 amendment

The following amendment to AGENTS.md Rule #11 ("Save-definer offset convention") ships with Plan 1 T1:

> Class offsets **45-70** are reserved for concrete `Activity` subclasses AND closely-related surface-spec persistent state... Offsets **51-70** specifically reserved for the CK3 wanderer mechanics cluster (Plans 1-7 of the wanderer spec) plus future surface specs (Specs 3-5).

(Previously: 45-60.)

---

## 8. References

- [CK3 Wanderer Mechanics Systems Analysis (v6)](../superpowers/specs/2026-04-24-ck3-wanderer-systems-analysis.md) — design source
- [Plan 1 — Architecture Foundation](../superpowers/plans/2026-04-24-ck3-wanderer-architecture-foundation.md) — owning plan
- [AGENTS.md](../../AGENTS.md) — universal rules
- [CLAUDE.md](../../CLAUDE.md) — Claude-specific session guidance + known footguns
- `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` — current offset registrations
- `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:188-193` — stay-back enforcement (T6 removes the T7+ gate)
- `src/Features/Retinue/Core/CompanionAssignmentManager.cs` — Fight/Stay-Back toggle (extension pattern; UI in `src/Features/Camp/CampMenuHandler.cs:2210-2294`)
- `src/Features/Activities/Home/HomeEveningMenuProvider.cs:37` — modal pipeline precedent
- `src/Features/Content/StoryletEventAdapter.cs:56` — `BuildModal`
- `src/Features/Content/StoryDirector.cs:61, 213` — `EmitCandidate` + `Route`
- `src/Features/Content/EventDeliveryManager.cs:93, 209` — `QueueEvent` + `ShowEventPopup`
- `src/Features/Conversations/Data/QMDialogueCatalog.cs` — JSON dialog schema precedent
- `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:8490` — `OnTierChanged` static event
