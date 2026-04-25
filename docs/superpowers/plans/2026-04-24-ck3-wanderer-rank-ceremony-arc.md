# Plan 3 — CK3 Wanderer Mechanics: Rank-Ceremony Arc

**Status:** Draft v2 (2026-04-25). Third of seven plans implementing the [CK3 Wanderer Mechanics Systems Analysis (v6)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md). See spec §8 for the full plan structure.

**v2 changes from v1:** flag-key naming aligned with architecture brief §4 rule 6 (flat underscore, not dotted) AND with the docstring already shipped in `RankCeremonyBehavior.cs:8-15` (`ceremony_fired_t{N}` + `ceremony_choice_t{N}` + `ceremony_witness_<archetype>_reaction_t{N}` + `ceremony_culture_variant_t{N}` — "system_scope_id" pattern with the tier suffix at the end); scripted-effects schema corrected to match the actual `{ "effects": { "<id>": [<primitives>] } }` shape with `apply` keys; explicit Plan 2 hand-off surface listed; dialog token interpolation contract called out for ceremony storylet authoring (per AGENTS.md pitfall #23, established by Plan 2 Phase 5++); Phase 20 validator reframed as "created from scratch" since Plan 1 verification §5 confirms no Phase 20 stub shipped (Plan 1 deferred validator phases 18-20 to the plans that need them); EnlistmentBehavior.cs line-number references updated to post-Plan-2 values (OnTierChanged event at ~8570, SetTier at ~9828, OnTierChanged.Invoke at ~9882) with a grep-the-symbol fallback note since plans drift per AGENTS.md pitfall #22.

**Scope:** Eight character-defining ceremony storylets at tier transitions (T1→T2 through T8→T9). Each fires as a modal popup via the canonical pipeline (`StoryletEventAdapter.BuildModal` + `StoryDirector.EmitCandidate`). Choice memory persists across the career via `FlagStore`; trait drift applies vanilla `DefaultTraits` (Mercy/Valor/Honor/Generosity/Calculating); companion witnesses (from Plan 2) react via `ChangeRelationAction.ApplyPlayerRelation`. **NO endeavors, NO officer equipment, NO patron favors** — those are Plans 4, 5, 6.

**Estimated tasks:** 20. **Estimated effort:** 3-4 days with AI-driven implementation.

**Dependencies:** Plan 1 + Plan 2 must be complete and their verification reports shipped before Plan 3 begins.

---

## §0 — Read these first (mandatory orientation for fresh agent chats)

### Required prior plan documentation

1. **[Plan 1 — Architecture Foundation](2026-04-24-ck3-wanderer-architecture-foundation.md)** + verification report.
2. **[Plan 2 — Companion Substrate](2026-04-24-ck3-wanderer-companion-substrate.md)** + verification report. Plan 3 uses `CompanionLifecycleHandler.GetSpawnedCompanions()` to query witnesses.
3. **[Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)** — locked decisions Plan 3 inherits.

### Required spec reading

4. **[Spec v6 §3.9 Rank-Ceremony Arc](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — design source. The eight ceremonies table, choice memory pattern, witness reactions, cultural variants.
5. **[Spec v6 §6.8 Canonical modal-popup pipeline](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — load-bearing. `StoryletEventAdapter.BuildModal` + `StoryDirector.EmitCandidate` with `ChainContinuation = true` for ceremony modals.
6. **[Spec v6 §6.6 Vanilla preferences (relation drift API)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — `ChangeRelationAction.ApplyPlayerRelation` is the cascading-drift entry point. Vanilla `CompanionGrievanceBehavior` reacts downstream via trait gates.
7. **[Spec v6 §9 Canonical rank system reference](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — **CRITICAL.** T6→T7 has THREE paths to promotion (auto proving-event, decline-then-dialog, dialog-request). Plan 3 hooks `OnTierChanged` to handle all three with one ceremony fire.

### Required project guidance

8. **[AGENTS.md](../../../AGENTS.md)** — Critical Rule #10 (StoryDirector routing), Pitfall #14 (don't author scripted-effect IDs without registering), Pitfall #16 (no cyclic scripted effects, depth limit 8).
9. **[CLAUDE.md](../../../CLAUDE.md)** — Pitfall #13 (don't write to read-only QualityStore qualities), regenerate error-codes registry after line-shifting C# edits.

### Required existing-code orientation

10. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` — `OnTierChanged` event declaration (currently line 8570) + invocation (currently line 9882).** Plan 3's main hook. **Line numbers shifted +80 after Plan 2's 24 SyncKey-field cluster + 6 GetOrCreate methods landed; treat all line-number references as approximate and grep for the symbol if line lookup misses.**
11. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` — `SetTier(int tier)` (currently line 9828).** Tier transition entry. Both proving-event path and dialog path call this. SetTier emits `OnTierChanged?.Invoke(previousTier, tier)` near its end.
12. **`src/Features/Ranks/Behaviors/PromotionBehavior.cs` — `CheckForPromotion()` (currently line 307).** Emits proving events via `StoryDirector.EmitCandidate` for ALL tier transitions including T6→T7. Plan 3's `OnTierChanged` hook fires AFTER this completes; ceremony storylet runs as a follow-up modal with `ChainContinuation = true`.
13. **`src/Features/Ranks/Behaviors/PromotionBehavior.cs` — `TriggerPromotionNotificationPublic(int newTier)` (currently line 413).** Called by `EventDeliveryManager` after a proving event grants promotion. Plan 3 ceremony fires from `OnTierChanged`, not from this — but verify they don't double-fire by tracing the call chain in T19 smoke.
14. **`src/Features/Ceremonies/RankCeremonyBehavior.cs`** — created in Plan 1 T11 as a log-only stub subscribed to `OnTierChanged`. The shipped file is short (~33 lines) with `OnTierChanged` invoking `ModLogger.Expected("CEREMONY", "tier_changed", ...)`. Plan 3 T4 replaces the stub body with the real ceremony fire logic. The shipped docstring (lines 7-15) already documents the flag-key conventions Plan 3 §4.2 uses (`ceremony_fired_t{N}`, `ceremony_choice_t{N}`).
15. **`src/Mod.Core/Helpers/ModalEventBuilder.cs`** — created in Plan 1 T5. Public API: `FireSimpleModal(storyletId, ctx, chainContinuation)`, `FireCeremony(storyletId, ctx)` (sets `chainContinuation: true`), `FireEndeavorPhase(storyletId, ctx, EndeavorActivity owner)`, `FireDecisionOutcome(storyletId, ctx)`. Plan 3 calls `ModalEventBuilder.FireCeremony(storyletId, ctx)` from the `OnTierChanged` handler. The `Activity owner` parameter is for endeavor phases (Plan 5); ceremonies pass null implicitly.
16. **`src/Features/Companions/CompanionLifecycleHandler.cs`** — created in Plan 2 T11. Plan 3 queries `GetSpawnedCompanions()` for witness selection.
17. **`src/Features/Activities/Home/HomeEveningMenuProvider.cs:37-72`** — canonical menu→modal precedent. Plan 3's `CeremonyProvider` mirrors this exact shape.
18. **`src/Features/Content/StoryletCatalog.cs`** — content loader. Plan 3 ceremony storylets land in `ModuleData/Enlisted/Storylets/ceremony_*.json` and load via the existing catalog infrastructure.
19. **`src/Features/Content/StoryletEventAdapter.cs:56` `BuildModal`** — converts Storylet → EventDefinition. `ModalEventBuilder.FireCeremony` wraps this.
20. **`src/Features/Content/StoryDirector.cs:213` `Route`** — pacing + modal vs news routing. `ChainContinuation = true` bypasses the 5-day in-game floor and 3-day category cooldown.
21. **`src/Features/Content/EffectExecutor.cs`** — applies storylet effects to game state. Plan 3 ceremony choices use existing effect primitives (e.g. `quality_add`, `set_flag`, `relation_change`) plus new helpers if needed.
22. **`src/Features/Qualities/QualityStore.cs`** — trait drift target. Plan 3 may use QualityStore for trait XP accumulation; alternative is direct `Hero.SetTraitLevel` per CLAUDE.md primitives.
23. **`src/Features/Flags/FlagStore.cs`** — choice memory persistence. Plan 3 keys: `ceremony.fired.t{N}` (dedup) + `ceremony.t{N}.choice = "<choice_id>"` (memory).
24. **`ModuleData/Enlisted/Effects/scripted_effects.json`** — existing scripted effects catalog. Plan 3 may add new entries (e.g. `ceremony_trait_drift_valor`, `ceremony_relation_witness_approve`) per spec §3.9 trait compounding rule.
25. **`ModuleData/Enlisted/Storylets/`** — existing storylet content directory. Plan 3 adds 8 ceremony storylets here (with cultural variants).

### Required decompile orientation

26. **`Decompile/TaleWorlds.CampaignSystem.Actions/ChangeRelationAction.cs`** — `ApplyPlayerRelation(Hero target, int change, bool showQuickNotification = true)`. Used to drift companion relations after ceremony choices that the companion approves/disapproves of.
27. **`Decompile/TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultTraits.cs:73-81`** — vanilla traits Mercy / Valor / Honor / Generosity / Calculating. Plan 3 ceremony choices apply these via `Hero.SetTraitLevel` or scripted effects.
28. **`Decompile/TaleWorlds.CampaignSystem/Hero.cs:1397-1401` `SetTraitLevel`** — direct setter, clamps to trait min/max. Plan 3 trait drift uses this.
29. **`Decompile/TaleWorlds.CampaignSystem/Hero.cs:2019-2025`** — `SetPersonalRelation` + `GetRelation`. Used by ChangeRelationAction internally.

---

## §1 — What this plan delivers

After Plan 3 ships, the codebase is in a state where:

- **Eight ceremony modals fire automatically at tier transitions.** When the player ranks up from T1→T2, T2→T3, etc., a modal popup appears with 2-3 character-defining choices. Choices apply trait drift, set memory flags, and trigger companion witness reactions.
- **Choice memory persists across the entire career.** `FlagStore` keys like `ceremony.t3.choice = "frugal"` are queryable by any future system. Plans 5 (Endeavors) and 6 (Patrons) can read these flags to gate or flavor their content.
- **Trait drift compounds across the 9-tier ladder.** All 8 ceremonies drift the same 5 vanilla traits (Mercy/Valor/Honor/Generosity/Calculating). By T9 the player has a measurable personality made of 8 character-defining choices.
- **Companion witnesses react to choices.** When the Sergeant disapproves of a T4 obedience-to-questionable-order choice, his relation drifts down via `ChangeRelationAction.ApplyPlayerRelation`. Vanilla `CompanionGrievanceBehavior` then auto-fires future complaints based on the new relation + trait state.
- **Cultural variants land for adequate coverage.** Each ceremony has 3 cultural variants (Vlandian feast / Sturgian oath-stone / Imperial formal commendation as representative samples). Same choices, different texture per the player's enlisted faction culture.
- **T6→T7 commission ceremony is the dramatic peak.** Hooks `OnTierChanged` (which fires regardless of which path triggered the promotion — auto proving-event, decline-then-dialog, or dialog-request), uses dedup flag `ceremony.fired.t7` to ensure exactly one ceremony per transition.
- **No double-fire across promotion paths.** T19 smoke verifies all three T6→T7 promotion paths (auto / decline / dialog) result in exactly one ceremony modal.

**No new mechanic surfaces beyond ceremonies firing on rank-up.** Companion witness reactions piggyback on Plan 2's spawned companions; no new companion lifecycle. Trait drift uses existing primitives or new scripted effects.

---

## §2 — Subsystems explored (audits informing Plan 3)

| Audit topic | Key finding informing Plan 3 | Spec section |
| :-- | :-- | :-- |
| Rank system end-to-end | `OnTierChanged` fires for ALL tier transitions; T6→T7 has 3 paths but `OnTierChanged` is single hook | §9 |
| Promotion path mechanics | `PromotionBehavior.CheckForPromotion` emits proving events for T2-T9 (NOT just T8/T9 as initially audited); T6→T7 has the additional dialog-request path | §9 (corrected v4) |
| Modal pipeline | `StoryletEventAdapter.BuildModal` + `StoryDirector.EmitCandidate` with `ChainContinuation = true` bypasses pacing for hard-trigger events like ceremonies | §6.8 |
| Choice memory | `FlagStore` is the existing flag-bool store with expiry; `QualityStore` is for typed numeric. Plan 3 uses FlagStore for choice flags + QualityStore (or direct Hero.SetTraitLevel) for trait drift | §6.4 |
| Companion witnesses | Plan 2 ships `CompanionLifecycleHandler.GetSpawnedCompanions()`; Plan 3 reads from this. Witness count per ceremony depends on tier (T1→T2 has only Sergeant; T6→T7 has all 6 + Lord) | §3.9 + §3.10 |
| Vanilla relation drift | `ChangeRelationAction.ApplyPlayerRelation(Hero, int, showQuickNotification)` is the cascading entry; vanilla auto-fires grievances downstream | §6.6 |
| Cultural variants | Player's enlisted faction culture (`EnlistmentBehavior._enlistedLord.Culture`) determines which variant fires; storylet `culture_filter` field gates selection | §3.9 |

---

## §3 — Subsystems Plan 3 touches

### Files modified (existing)

| File | Change | Tasks |
| :-- | :-- | :-- |
| `src/Features/Ceremonies/RankCeremonyBehavior.cs` (created in Plan 1 T11 as stub) | Populate `OnTierChanged` handler with real ceremony fire logic + dedup flag check | T4 |
| `src/Features/Content/StoryletCatalog.cs` | No code change; Plan 3 just adds storylet JSON files. Catalog auto-loads them. | T9-T16 (content) |
| `Tools/Validation/validate_content.py` | Phase 20 (ceremony storylet completeness) populated from Plan 1 T14 stub | T17 |
| `ModuleData/Enlisted/Effects/scripted_effects.json` | Add new entries for ceremony-specific effects (e.g. `ceremony_trait_drift_valor`, `ceremony_relation_witness_approve`) | T6, T7 |

### Files created (new)

| File | Purpose | Tasks |
| :-- | :-- | :-- |
| `docs/Features/Ceremonies/ceremony-storylet-schema.md` | Schema reference for ceremony storylets (extends base storylet schema with ceremony-specific fields) | T1 |
| `docs/Features/Ceremonies/ceremony-flag-conventions.md` | Choice memory flag conventions (`ceremony.fired.t{N}`, `ceremony.t{N}.choice`, `ceremony.t{N}.witness.<name>.reaction`) | T2 |
| `src/Features/Ceremonies/CeremonyProvider.cs` | Modeled on `HomeEveningMenuProvider.OnSlotSelected`; builds ceremony modal via `StoryletEventAdapter.BuildModal` + emits via `StoryDirector.EmitCandidate` | T3 |
| `src/Features/Ceremonies/CeremonyWitnessSelector.cs` | Selects companion witnesses for a given ceremony based on tier + spawned-companions list | T5 |
| `src/Features/Ceremonies/CeremonyTraitDriftApplier.cs` | Applies vanilla trait drift (Mercy/Valor/Honor/Generosity/Calculating) per ceremony choice | T6 |
| `src/Features/Ceremonies/CeremonyWitnessReactor.cs` | Drifts companion witness relations via `ChangeRelationAction.ApplyPlayerRelation` based on choice + companion's trait alignment | T7 |
| `src/Features/Ceremonies/CeremonyCultureSelector.cs` | Maps player's enlisted faction culture to cultural variant storylet ID | T8 |
| `ModuleData/Enlisted/Storylets/ceremony_t1_to_t2.json` | T1→T2 ceremony (3 cultural variants + shared options/consequences) | T9 |
| `ModuleData/Enlisted/Storylets/ceremony_t2_to_t3.json` | T2→T3 ceremony | T10 |
| `ModuleData/Enlisted/Storylets/ceremony_t3_to_t4.json` | T3→T4 ceremony | T11 |
| `ModuleData/Enlisted/Storylets/ceremony_t4_to_t5.json` | T4→T5 ceremony | T12 |
| `ModuleData/Enlisted/Storylets/ceremony_t5_to_t6.json` | T5→T6 ceremony (NCO transition — Sergeant + Veteran witnesses) | T13 |
| `ModuleData/Enlisted/Storylets/ceremony_t6_to_t7.json` | T6→T7 ceremony — THE COMMISSION (Lord + all 6 companions + soldiers as witnesses) | T14 |
| `ModuleData/Enlisted/Storylets/ceremony_t7_to_t8.json` | T7→T8 ceremony | T15 |
| `ModuleData/Enlisted/Storylets/ceremony_t8_to_t9.json` | T8→T9 ceremony | T16 |
| `docs/superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc-verification.md` | Plan 3 verification report | T20 |

### Subsystems Plan 3 does NOT touch

- Companion spawning (Plan 2 — Plan 3 just queries `CompanionLifecycleHandler`)
- Officer Trajectory equipment (Plan 4 — T7 commission ceremony narratively references the banner/cape/weapon-modifier but Plan 3 doesn't apply them)
- Endeavor system (Plan 5)
- Patron favors (Plan 6)
- Personal Kit (Plan 7)
- New tier mechanics — Plan 3 only fires modals at existing tier-change events; doesn't add new tiers or change existing rank-up flow

---

## §4 — Locked design decisions

### §4.1 Ceremony hook strategy (LOCKED — corrected v4)

**Hook `EnlistmentBehavior.OnTierChanged` event ONLY.** Do not hook the dialog branch at `EnlistedDialogManager.cs:347-368` directly. Do not hook `PromotionBehavior.CheckForPromotion`. The rationale (verified via spec §9 audit):

- T2-T5 + T7-T9: only the auto proving-event path fires `SetTier(target)`.
- T6→T7: three paths can fire `SetTier(7)`:
  1. Auto proving-event (when XP + days + battles + relation requirements met)
  2. Decline-then-dialog (player previously declined the proving event; later requests via dialog)
  3. Direct dialog-request (player initiates "I believe I am ready..." at any time when conditions met)

`SetTier(target)` always fires `OnTierChanged(prev, curr)`. **Hooking only `OnTierChanged` covers all three T6→T7 paths with one handler.** The `ceremony.fired.t{N}` dedup flag ensures exactly one ceremony per transition (handles edge cases like grace-period re-enlistment that could theoretically re-trigger the event).

### §4.2 Choice memory flag conventions (LOCKED)

| Flag key pattern | Type | Purpose |
| :-- | :-- | :-- |
| `ceremony_fired_t{N}` | bool | Dedup. Set to `true` after ceremony fires for tier-up to N. Subsequent `OnTierChanged(N-1, N)` events check this flag and short-circuit if set. |
| `ceremony_choice_t{N}` | string | Player's choice ID at ceremony N. E.g. `ceremony_choice_t3 = "frugal"`. |
| `ceremony_witness_<archetype>_reaction_t{N}` | string | Witness reaction at ceremony N. E.g. `ceremony_witness_sergeant_reaction_t4 = "approve"`. |
| `ceremony_culture_variant_t{N}` | string | Which cultural variant fired (Vlandian / Sturgian / etc.). For replay analysis only; no game logic reads this. |

All flags persist via `FlagStore`'s existing serialization. No new save-offset needed.

**Naming convention enforced.** The architecture brief §4 rule 6 (and Plan 1 verification §5) lock all flag and quality keys to flat underscore namespace (`<system>_<scope>_<id>`). Earlier draft v1 of this plan used dotted notation (`ceremony.fired.t{N}`); v2 corrects to match the brief, the existing `FlagStore` precedent, AND the docstring already shipped with `RankCeremonyBehavior.cs:8-15` which documents `ceremony_fired_t{N}` (dedup) + `ceremony_choice_t{N}` (selected option). Plan 3 v2 conforms to the shipped docstring rather than inventing new patterns.

### §4.3 Cultural variant strategy (LOCKED)

**Three representative cultures per ceremony for adequate coverage.** Each ceremony storylet file (`ceremony_t{N}_to_t{N+1}.json`) contains:
- 1 base setup with all options + consequences (the structural ceremony)
- 3 cultural variants: Vlandian / Sturgian / Imperial (the texture per culture)

The `CeremonyCultureSelector` (T8) maps the player's enlisted-faction culture to one of:
- Vlandia → Vlandian variant
- Sturgia → Sturgian variant  
- Empire → Imperial variant
- Battania, Aserai, Khuzait → fall back to closest cultural cousin (Battania→Sturgian, Aserai→Imperial, Khuzait→Sturgian as steppe-warrior variant) for v1; Plan 7 polish pass adds full per-culture variants if playtest demands.

This keeps content authoring tractable: 8 ceremonies × 4 storylet objects each (base + 3 variants) = ~32 storylet entries. Alternative full coverage (8 × 6) = 48 entries; deferred to polish.

### §4.4 Eight ceremonies — locked content shape (mirrors spec §3.9)

| Tier transition | The question | Drift axis | Witnesses (Plan 2 spawned) |
| :-: | :-- | :-- | :-- |
| T1→T2 | First combat survival — *who do you credit?* | Self-reliance vs trust-the-line | Sergeant only (Field Medic / Pathfinder not yet spawned at T2) |
| T2→T3 | First raid share — *what do you do with the gold?* | Frugal / Generous / Hedonist / Family | Sergeant, fellow recruits (NPCs not Plan 2 companions) |
| T3→T4 | A peer mocks you publicly — *fists, words, or report?* | Valor / Calculating / Honor | Sergeant, Field Medic, Pathfinder (now spawned at T3) |
| T4→T5 | Lord orders something questionable — *obey, question, refuse?* | Mercy / Honor / Calculating | Lord, Sergeant, Field Medic |
| T5→T6 | Company asks YOU to lead a small task — *take it or defer?* | NCO authority style | Sergeant, Field Medic, Pathfinder, Veteran (Veteran spawns at T5) |
| **T6→T7** | **The Commission Ceremony** — Lord grants commander rank | Identity locks in from prior 5 choices | Lord + all 4 companions + soldiers (banner ceremony moment) |
| T7→T8 | Junior officer questions your tactical call — *authority, debate, or compromise?* | Officer leadership style | Junior Officer, QM Officer (spawn at T7), Veteran |
| T8→T9 | Young soldier asks you to mentor him — *take him on, distance, or use him?* | Marshal legacy texture | Veteran, Lord |

**Each ceremony is single-phase.** Ceremony fires → modal opens → player picks → effects apply → ceremony done. No multi-phase ceremonies in v1; Plan 7 polish pass may add multi-phase variants.

### §4.5 Trait drift mechanism (LOCKED)

Choice options apply trait drift via direct `Hero.SetTraitLevel(trait, currentLevel + delta)` on Hero.MainHero. Magnitudes: ±1 per ceremony (compounds across 8 ceremonies = ±8 max range, within vanilla trait clamp).

**Implementation via scripted effects** (added to `ModuleData/Enlisted/Effects/scripted_effects.json` in T6).

The actual `scripted_effects.json` shape is `{ "effects": { "<id>": [ <list of primitives> ] } }`. Each primitive uses `apply` plus its own args. New ceremony entries land in this shape:

```json
{
  "effects": {
    "ceremony_trait_drift_valor_up": [
      { "apply": "trait_drift", "trait": "Valor", "delta": 1 }
    ],
    "ceremony_trait_drift_mercy_down": [
      { "apply": "trait_drift", "trait": "Mercy", "delta": -1 }
    ]
  }
}
```

Storylet option references the effect by ID:

```json
{
  "id": "fight_him",
  "text": "Step up and answer him — fists, not steel",
  "effects": [
    { "apply": "ceremony_trait_drift_valor_up" },
    { "apply": "ceremony_witness_reaction_sergeant_approve" },
    { "apply": "set_flag", "name": "ceremony_choice_t3", "value": "fight" }
  ]
}
```

`EffectExecutor` (existing) handles dispatch. Plan 3 adds the ~10 ceremony-specific scripted effects (one per trait × direction); the new `trait_drift` primitive needs an `EffectExecutor` handler in T6 (calls `Hero.MainHero.SetTraitLevel(trait, currentLevel + delta)`). Existing primitives (`set_flag`, `quality_add`, etc.) are reused. **The `kind:` / `primitive:` field shapes from earlier draft v1 of this plan don't match the actual schema; v2 corrects to `apply:` inside an array per id.**

**Storylet text token usage.** Per AGENTS.md pitfall #23 + architecture brief §3 text-variable interpolation contract, ceremony storylet `setup` and `options[].text` strings should reference `{PLAYER_NAME}`, `{PLAYER_RANK}` (culture-aware via `RankHelper.GetCurrentRank`), `{LORD_NAME}`, `{PLAYER_TIER}` rather than hard-coding "soldier" / "the lord" / etc. Token resolution requires the conversation-opener to populate the variables; ceremony modals fired through `ModalEventBuilder.FireCeremony` inherit whatever variables the wider session has set, so most cases work automatically — but if a ceremony fires before the QM flow has set the tokens (early enlistment), `RankCeremonyBehavior.OnTierChanged` may need to call its own SetTextVariable batch. T18 smoke verifies token resolution at T1→T2 (the earliest ceremony).

### §4.6 Witness reaction mechanism (LOCKED)

Each ceremony storylet defines per-choice witness reactions. Format in storylet JSON:

```json
{
  "id": "fight_him",
  "witness_reactions": {
    "sergeant": "approve",   // gruff sergeant likes Valor
    "field_medic": "neutral", // doesn't care about peer-mocking dispute
    "pathfinder": "neutral"
  }
}
```

`CeremonyWitnessReactor` (T7) reads the witness_reactions block, queries `CompanionLifecycleHandler.GetSpawnedCompanions()` for currently-present witnesses, and applies relation drift:
- `approve`: `ChangeRelationAction.ApplyPlayerRelation(witness, +5, showQuickNotification: true)`
- `neutral`: no action
- `disapprove`: `ChangeRelationAction.ApplyPlayerRelation(witness, -5, showQuickNotification: true)`
- `strong_approve`: +10
- `strong_disapprove`: -10

Vanilla `CompanionGrievanceBehavior` then auto-fires future complaints based on the drifted relation + companion's trait state. This is the "free reactivity" finding from spec §6.6.

---

## §5 — Tooling and safeguards

Inherits all from Plan 1 §5 + Plan 2 §5. Additional Plan 3-specific guidance:

### Ceremony fire smoke recipe

For each ceremony task (T9-T16):

1. Build clean.
2. Use Debug Tools to force-set `EnlistmentBehavior.SetTier(N+1)` (where N is the ceremony's source tier).
3. Confirm modal popup appears within 1-2 seconds (modal pacing rails respect `ModalFloorWallClockSeconds = 60` even with `ChainContinuation = true`, but the in-game-day floor is bypassed).
4. Confirm modal title + setup match the ceremony's culture variant for the player's enlisted faction.
5. Confirm 2-3 options visible; each option's preview block shows expected effects.
6. Pick one option. Confirm:
   - Modal closes.
   - `FlagStore.GetString($"ceremony.t{N+1}.choice")` returns the picked option's ID.
   - `FlagStore.GetBool($"ceremony.fired.t{N+1}")` is true.
   - Trait drift applied (verify via Hero.MainHero trait inspection).
   - Witness reactions applied (verify via Hero.GetRelation for each spawned companion).
7. Force-set tier again to same value. Confirm ceremony does NOT re-fire (dedup).

### T6→T7 three-path smoke recipe (T19)

Critical regression check. Run all three paths separately:

**Path A — Auto proving-event:**
1. Set `EnlistmentTier = 6`. Use Debug Tools to grant the XP/days/battles/relation requirements for T7.
2. Wait for `PromotionBehavior.CheckForPromotion` to trigger (or force-tick).
3. Confirm proving-event modal fires (vanilla mechanic, separate from ceremony).
4. Player accepts the proving event → `SetTier(7)` fires.
5. **Confirm ceremony modal fires immediately after** (via `OnTierChanged` hook + `ChainContinuation = true`).
6. Player picks ceremony choice → ceremony.fired.t7 set.

**Path B — Decline-then-dialog:**
1. Same setup as A.
2. When proving-event modal appears, player declines.
3. `EscalationManager.HasDeclinedPromotion(7)` returns true.
4. Player initiates Lord conversation, asks for promotion via dialog branch (`enlisted_request_promotion_t7`).
5. Lord grants → `SetTier(7)` fires.
6. **Confirm ceremony modal fires immediately after** (via same hook).
7. Confirm dedup flag prevents proving-event-side ceremony from firing later.

**Path C — Direct dialog-request:**
1. Set `EnlistmentTier = 6`. Grant requirements but DON'T trigger proving-event.
2. Player initiates Lord conversation, asks for promotion.
3. Lord grants → `SetTier(7)` fires.
4. **Confirm ceremony modal fires once.**

All three paths must result in exactly ONE ceremony modal at T7. T19 documents results.

---

## §6 — Tasks (sequential — must complete in order)

### T1 — Ceremony storylet schema documented

**Goal:** Document the ceremony storylet schema at `docs/Features/Ceremonies/ceremony-storylet-schema.md`. Extends base storylet schema (existing) with ceremony-specific fields.

**New fields beyond base storylet:**
- `culture_variant` (string) — one of "vlandian" / "sturgian" / "imperial" / "base" (catch-all)
- `tier_transition` (string) — e.g. "t3_to_t4"
- `witness_reactions` (object) — per-choice map of `<archetype>: "approve"|"neutral"|"disapprove"|"strong_approve"|"strong_disapprove"`

**Files:** New `docs/Features/Ceremonies/ceremony-storylet-schema.md`

**Verification:** Document review.

---

### T2 — Choice memory flag conventions documented

**Goal:** Document FlagStore key conventions per §4.2.

**Files:** New `docs/Features/Ceremonies/ceremony-flag-conventions.md`

**Content:** Reproduce §4.2 table verbatim + give 2-3 example flag-set sequences for full ceremony arc playthroughs.

**Verification:** Document review.

---

### T3 — `CeremonyProvider` class

**Goal:** Centralize ceremony-modal firing. Modeled exactly on `HomeEveningMenuProvider.OnSlotSelected:37`.

**Files:** New `src/Features/Ceremonies/CeremonyProvider.cs`. Edit `Enlisted.csproj`.

**Concrete API:**

```csharp
namespace Enlisted.Features.Ceremonies
{
    public static class CeremonyProvider
    {
        public static void FireCeremonyForTier(int newTier)
        {
            var dedupKey = $"ceremony.fired.t{newTier}";
            if (FlagStore.Instance.GetBool(dedupKey))
            {
                ModLogger.Expected("CEREMONY", "already_fired",
                    $"Ceremony for T{newTier} already fired", new { newTier });
                return;
            }
            
            var storyletId = SelectCulturalVariant(newTier);
            if (storyletId == null) { /* expected log; return */ }
            
            var ctx = BuildCeremonyContext(newTier);
            ModalEventBuilder.FireCeremony(storyletId, ctx);
            
            // Note: dedup flag is set by the ceremony's resolution storylet effect,
            // NOT here. This ensures a player who closes the modal mid-air without
            // picking gets the ceremony retried later (vs locked out).
            // If we want strict "fire-and-forget" semantics, set the flag here instead.
        }
        
        private static string SelectCulturalVariant(int newTier);
        private static StoryletContext BuildCeremonyContext(int newTier);
    }
}
```

**Verification:** Build clean. Smoke: `CeremonyProvider.FireCeremonyForTier(2)` from Debug Tools fires T1→T2 ceremony correctly (after T9 ships content).

---

### T4 — `RankCeremonyBehavior` populated

**Goal:** Replace Plan 1 T11's log-only stub with the real `OnTierChanged` handler that calls `CeremonyProvider.FireCeremonyForTier`.

**Files:** Edit `src/Features/Ceremonies/RankCeremonyBehavior.cs`

**Concrete change:**

```csharp
private void OnTierChanged(int previousTier, int newTier)
{
    // Plan 3 T4: fire ceremony storylet via CeremonyProvider.
    // Single hook covers all 3 T6→T7 paths (auto proving-event, decline-then-dialog, dialog-request).
    
    if (newTier <= previousTier) return;  // Only on rank-up, not demotion (vanilla doesn't demote)
    if (newTier < 2 || newTier > 9) return;  // Only valid tier range
    
    ModLogger.Expected("CEREMONY", "tier_up_detected",
        $"Tier {previousTier} -> {newTier}; firing ceremony", new { previousTier, newTier });
    
    CeremonyProvider.FireCeremonyForTier(newTier);
}
```

**Verification:** Force-set tier; confirm `CEREMONY tier_up_detected` log line. After T9 ships content, full smoke recipe (§5) passes.

---

### T5 — Witness selection logic

**Goal:** `CeremonyWitnessSelector` queries `CompanionLifecycleHandler.GetSpawnedCompanions()` and filters per ceremony tier per §4.4 table.

**Files:** New `src/Features/Ceremonies/CeremonyWitnessSelector.cs`

**Concrete API:**

```csharp
public static class CeremonyWitnessSelector
{
    public static List<Hero> GetWitnessesForCeremony(int newTier)
    {
        var spawned = CompanionLifecycleHandler.Instance?.GetSpawnedCompanions() ?? new List<Hero>();
        // Per §4.4, witnesses depend on tier + which companions exist
        // T1→T2: Sergeant only
        // T3→T4: Sergeant + Field Medic + Pathfinder
        // T5→T6: + Veteran
        // T6→T7: + all + Lord
        // T7→T8: + QM Officer + Junior Officer
        // T8→T9: Veteran + Lord
        // Return appropriate subset
    }
}
```

**Verification:** Unit smoke: at T6→T7 with all 6 companions spawned, witness list returns 6 + Lord. At T1→T2 with only Sergeant spawned, witness list returns just Sergeant.

---

### T6 — Trait drift apply mechanism

**Goal:** `CeremonyTraitDriftApplier` applies trait deltas via `Hero.SetTraitLevel`. Implemented as scripted effects in `scripted_effects.json` so storylet content references them by ID.

**Files:**
- New `src/Features/Ceremonies/CeremonyTraitDriftApplier.cs` (helper class for direct calls)
- Edit `ModuleData/Enlisted/Effects/scripted_effects.json` — add 10 entries:

```json
{ "id": "ceremony_trait_drift_mercy_up", "primitive": "trait_drift", "trait": "Mercy", "delta": 1 },
{ "id": "ceremony_trait_drift_mercy_down", "primitive": "trait_drift", "trait": "Mercy", "delta": -1 },
{ "id": "ceremony_trait_drift_valor_up", ... },  // etc. for all 5 traits × 2 directions
```

- Edit `EffectExecutor.cs` — add `trait_drift` primitive handler (calls `Hero.MainHero.SetTraitLevel(trait, currentLevel + delta)`)

**Verification:** Smoke: storylet option with `{ "apply": "ceremony_trait_drift_valor_up" }` increases MainHero's Valor by 1. `validate_content.py` Phase 12 confirms scripted effect IDs resolve.

**Footgun:** Per CLAUDE.md Pitfall #14, every `apply` value must be in `scripted_effects.json`. Phase 12 validator blocks unknowns.

---

### T7 — Companion witness reaction wiring

**Goal:** `CeremonyWitnessReactor` reads storylet `witness_reactions` block + applies relation drift via `ChangeRelationAction`. Implemented as scripted effects.

**Files:**
- New `src/Features/Ceremonies/CeremonyWitnessReactor.cs` (helper)
- Edit `scripted_effects.json` — add per-archetype reaction effects in the canonical shape:

```json
{
  "effects": {
    "ceremony_witness_reaction_sergeant_approve": [
      { "apply": "companion_relation", "archetype": "sergeant", "delta": 5 }
    ],
    "ceremony_witness_reaction_sergeant_disapprove": [
      { "apply": "companion_relation", "archetype": "sergeant", "delta": -5 }
    ]
  }
}
```

6 archetypes × 2 directions × 2 magnitudes (5/10) = ~24 entries.

- Edit `EffectExecutor.cs` — add `companion_relation` primitive handler (resolves archetype to spawned hero via `EnlistmentBehavior.Instance.GetSpawnedCompanions()` filtered by `EnlistmentBehavior.Instance.GetCompanionTypeId(hero)` matching the archetype string; calls `ChangeRelationAction.ApplyPlayerRelation`).

The Plan 2 hand-off surface for archetype → hero lookup:

```csharp
var enlistment = EnlistmentBehavior.Instance;
var hero = enlistment?.GetSpawnedCompanions()
    .FirstOrDefault(h => enlistment.GetCompanionTypeId(h) == archetype);
if (hero == null) {
    ModLogger.Expected("CEREMONY", "witness_not_spawned",
        $"Witness archetype '{archetype}' not currently spawned; skipping reaction");
    return;
}
ChangeRelationAction.ApplyPlayerRelation(hero, delta, showQuickNotification: true);
```

`CompanionLifecycleHandler.Instance.GetSpawnedCompanions()` is an equivalent accessor (delegates to EnlistmentBehavior). Either is fine.

**Verification:** Smoke: storylet option with `{ "apply": "ceremony_witness_reaction_sergeant_approve" }` increases Sergeant's relation with MainHero by 5. Vanilla notification ("Sergeant approves of your decision") fires automatically.

**Footgun:** Companion may not be spawned (e.g. Veteran at T3→T4). Effect must Expected-log and skip gracefully if archetype's hero is null.

---

### T8 — Cultural variant matching

**Goal:** `CeremonyCultureSelector` maps player's enlisted faction culture to one of "vlandian"/"sturgian"/"imperial"/"base" per §4.3.

**Files:** New `src/Features/Ceremonies/CeremonyCultureSelector.cs`

**Concrete logic:**

```csharp
public static string SelectVariantSuffix()
{
    var lord = EnlistmentBehavior.Instance?.EnlistedLord;
    var cultureId = lord?.Culture?.StringId;
    return cultureId switch
    {
        "vlandia" or "battania" => "vlandian",  // Battania falls back to Vlandian for v1
        "sturgia" or "khuzait"  => "sturgian",  // Khuzait falls back to Sturgian (steppe-warrior)
        "empire" or "aserai"    => "imperial",  // Aserai falls back to Imperial (formal commendation)
        _                       => "base"        // catch-all
    };
}

public static string ResolveStoryletId(int newTier)
{
    var suffix = SelectVariantSuffix();
    return $"ceremony.t{newTier-1}_to_t{newTier}.{suffix}";
}
```

**Verification:** Smoke: enlisted with Vlandian lord → `ResolveStoryletId(2)` returns `"ceremony.t1_to_t2.vlandian"`. With Sturgian lord → `"ceremony.t1_to_t2.sturgian"`.

---

### T9 — Ceremony storylet T1→T2 authored (3 cultural variants)

**Goal:** Author `ModuleData/Enlisted/Storylets/ceremony_t1_to_t2.json`. Contains 4 storylets: 1 base + 3 cultural variants.

**Question:** *First combat survival — who do you credit?* Self-reliance vs trust-the-line.

**Witnesses:** Sergeant only.

**Options (shared across cultural variants):**
1. *"My own steel."* — +Valor; Sergeant disapprove (gruff: "We're a unit, soldier, not a list of duelists.")
2. *"My sergeant kept me alive."* — +Honor; Sergeant strong_approve.
3. *"Luck, mostly."* — +Calculating; Sergeant neutral.

**Cultural variants (setup texture):**
- Vlandian: After-action feast in the warband mess. The lord's wine is being passed around.
- Sturgian: Around the fire after the action. The men are drinking from the captured stores.
- Imperial: At the muster the next morning. The centurion is calling roll.

**Files:** New `ModuleData/Enlisted/Storylets/ceremony_t1_to_t2.json`

**Verification:** `validate_content.py` Phase 12 + Phase 20 (ceremony completeness) pass. Smoke per §5 ceremony fire recipe.

---

### T10-T16 — Ceremony storylets T2→T3 through T8→T9

**Each follows T9's pattern** — 1 base + 3 cultural variants per ceremony. Content per §4.4 table.

Brief sketches:

**T10 (T2→T3):** First raid share — what do you do with the gold? Options: Frugal (bury it) / Generous (drinks for the company) / Family (send home) / Hedonist (spend on cards and women). Witnesses: Sergeant + fellow recruits (NPCs).

**T11 (T3→T4):** A peer mocks you — fists / words / report? Witnesses: Sergeant + Field Medic + Pathfinder. Field Medic disapproves of fists; Sergeant approves of fists.

**T12 (T4→T5):** Lord orders something questionable — obey / question / refuse? Witnesses: Lord (CRITICAL — the Lord is watching) + Sergeant + Field Medic. Field Medic strong_approves refuse if order targets civilians.

**T13 (T5→T6):** Company asks YOU to lead a small task — take it / defer? NCO transition. Witnesses: Sergeant, Field Medic, Pathfinder, Veteran (Veteran spawns at T5).

**T14 (T6→T7):** THE COMMISSION. Witnesses: Lord + all 4 spawned companions + the army. Banner ceremony moment (Plan 4 applies actual gear; Plan 3 narratively references). Options: Accept with humility (+Honor +Generosity) / Accept with pride (+Valor +Calculating) / Try to refuse (+Mercy, -Renown).

**T15 (T7→T8):** Junior officer questions your tactical call. Witnesses: Junior Officer + QM Officer + Veteran.

**T16 (T8→T9):** Young soldier asks for mentorship. Witnesses: Veteran + Lord.

**Files:** New ceremony JSONs per task.

**Verification:** Each smoke per §5. All 8 ceremonies fire correctly at their tier triggers.

---

### T17 — Phase 20 validator created

**Goal:** Create Phase 20 in `validate_content.py` from scratch (Plan 1 verification §5 confirms no Phase 20 stub shipped — Plan 1 deferred validator phases 18-20 to the plans that need them; Plan 2 created Phase 18 from scratch using the same pattern). Phase 20 confirms all 8 tier transitions have at least one ceremony storylet authored, with at least the "base" cultural variant.

**Files:** Edit `Tools/Validation/validate_content.py`

**Concrete check:**

```python
def phase20_ceremony_storylets(storylet_entries, ctx):
    """Plan 3 §6 T17. Confirm all 8 tier transitions have ceremony storylets authored."""
    print("[Phase 20] Validating ceremony storylet completeness...")
    required_tiers = [(1,2), (2,3), (3,4), (4,5), (5,6), (6,7), (7,8), (8,9)]
    # For each (prev, curr) tier pair, verify ceremony_t{prev}_to_t{curr}_base
    # exists; the 3 cultural variants are recommended but not strictly required
    # for ship (covered by Plan 7 polish pass).
```

Pattern mirrors `phase18_companion_dialogue` (Plan 2) — register the function before `main()`, call from `main()` after Phase 19 (or after Phase 17 if Phase 19 doesn't exist yet — check the latest validator state).

**Verification:** `validate_content.py` Phase 20 passes after T9-T16 all ship.

---

### T18 — End-to-end smoke: T1→T2 single ceremony

**Goal:** Validate the full pipeline end-to-end with the simplest case (T1→T2, only Sergeant witness).

**Test recipe:**
1. Build clean.
2. Launch game; enlist with Vlandian lord (Sergeant spawns at T1 per Plan 2).
3. Use Debug Tools to grant T2 XP requirements.
4. Wait for proving event (or Debug-Tools fast-trigger).
5. Player accepts → `SetTier(2)` fires → `OnTierChanged(1, 2)` → `RankCeremonyBehavior.OnTierChanged` → `CeremonyProvider.FireCeremonyForTier(2)` → modal pops up.
6. Modal title + setup match Vlandian variant of T1→T2 ceremony.
7. Player picks "My sergeant kept me alive" option.
8. Modal closes. Confirm:
   - `FlagStore.GetBool("ceremony.fired.t2")` == true
   - `FlagStore.GetString("ceremony.t2.choice")` == "trust_sergeant" (or whatever ID)
   - Hero.MainHero Honor trait increased by 1
   - Sergeant's relation with MainHero increased by 10 (strong_approve)
   - In-game notification "Sergeant approves of your decision"

**Verification:** Manual smoke per recipe. Document in T20 report.

---

### T19 — End-to-end smoke: T6→T7 three-path verification

**Goal:** Critical regression check that `OnTierChanged` hook handles all three T6→T7 paths correctly with no double-fires.

**Test recipe:** Per §5 T6→T7 three-path smoke recipe. Run all three paths separately on three save files (or reload). All three must result in exactly ONE ceremony modal.

**Verification:** Manual smoke. Document each path's result in T20 report. If any path double-fires or fails to fire, halt and debug before declaring Plan 3 done.

---

### T20 — Plan 3 verification report

**Goal:** Document all smoke results, flag any deviations, sign-off.

**Files:** New `docs/superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc-verification.md`

**Content:**
- Build + validator pass confirmation
- Each ceremony's smoke result (T9-T16)
- T18 end-to-end smoke result
- T19 three-path smoke result (CRITICAL)
- Any deviations + resolutions
- Sign-off: Plan 3 ✅ complete; Plans 4-7 unblocked.

---

## §7 — Risks

### Risk H1 — T6→T7 double-fire (HIGH)

**Vector:** Three promotion paths, each could fire `OnTierChanged`. If dedup flag isn't set correctly or is checked after modal renders, player could see two ceremony modals stacked.

**Mitigation:**
- T19 explicit smoke for all three paths.
- Dedup flag check at top of `CeremonyProvider.FireCeremonyForTier` (T3).
- If still double-fires, set the dedup flag BEFORE calling `ModalEventBuilder.FireCeremony` (move flag-set out of effect chain).

### Risk M1 — Modal fires too early (MEDIUM)

**Vector:** `OnTierChanged` may fire BEFORE the proving-event modal closes, causing the ceremony modal to stack on top of the proving-event modal. Bad UX.

**Mitigation:**
- `EventDeliveryManager` queues modals; ceremony modal queues behind proving-event modal naturally if both emit at the same tick.
- T19 verifies the visual order (proving event → close → ceremony fires).
- If stacking issues, defer ceremony fire by one tick using `NextFrameDispatcher.RunNextFrame`.

### Risk M2 — Companion witness reactions don't fire if companion not spawned (MEDIUM)

**Vector:** Storylet specifies witness reaction for "veteran" but Veteran isn't spawned yet (e.g. T3→T4 ceremony references Veteran as if witness). `companion_relation` effect with archetype=veteran returns null hero.

**Mitigation:**
- T7 `companion_relation` primitive Expected-logs and skips if archetype hero is null.
- Storylet `witness_reactions` blocks should only reference companions per §4.4 table.
- T7 includes a defensive null-check; never crashes.

### Risk M3 — Cultural variant fallbacks feel wrong (MEDIUM)

**Vector:** Battania → Vlandian variant might feel narratively off (Vlandian feast vs Battanian woodland trial). Some playtest dissonance possible.

**Mitigation:**
- Document the fallback explicitly in §4.3 — players see a Vlandian-flavored ceremony when enlisted with Battanian lord. Acceptable for v1.
- Plan 7 polish pass authors full per-culture variants (Battanian / Aserai / Khuzait) if playtest demands.

### Risk L1 — Trait drift overflow (LOW)

**Vector:** Vanilla traits clamp ±N (typically ±2 or ±3 per `DefaultTraits.MaxValue`). 8 ceremonies × ±1 = ±8 max if all in same direction; clamps to vanilla max.

**Mitigation:**
- `Hero.SetTraitLevel` clamps internally per `Decompile/Hero.cs:1397-1401`. No overflow risk.
- Player can reach max trait value via ceremony arc; Plan 3 design explicitly accepts this (compounding personality).

### Risk L2 — Storylet content tone inconsistency (LOW)

**Vector:** AI-generated cultural variants may shift tone unevenly between Vlandian / Sturgian / Imperial.

**Mitigation:**
- T9 (T1→T2) authored first as the tone-reference. Playtest reaction informs T10-T16.
- Plan 7 polish pass available.

---

## §8 — Verification gates

- [ ] `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` passes
- [ ] `python Tools/Validation/validate_content.py` passes (Phases 12, 13, 20 in particular)
- [ ] `Tools/Validation/lint_repo.ps1` passes for Plan 3's NEW files (`dotnet format --include 'src/Features/Ceremonies/**'`). **Note:** The full repo-wide lint stack currently fails on pre-existing CHARSET pollution across Plan 1 substrate files + Career-loop family files (`normalize_crlf.ps1` unconditionally prepends a UTF-8 BOM; `.editorconfig charset = utf-8` rejects BOMs). Plan 2 verification §2 documents this as out-of-scope tracked separately. Plan 3 should strip the BOM from its new C# files (use the PowerShell snippet from Plan 2 Phase 5+) and verify lint passes for `src/Features/Ceremonies/**` specifically — it should NOT block on the pre-existing pollution.
- [ ] `RankCeremonyBehavior.OnTierChanged` populated (no longer log-only stub)
- [ ] All 8 ceremony storylets authored (32+ storylet entries with cultural variants)
- [ ] `CeremonyProvider`, `CeremonyWitnessSelector`, `CeremonyTraitDriftApplier`, `CeremonyWitnessReactor`, `CeremonyCultureSelector` all build clean and unit-smoke clean
- [ ] T18 end-to-end smoke (T1→T2) passes
- [ ] T19 T6→T7 three-path smoke passes (NO double-fires; ONE modal per transition)
- [ ] Choice memory flags persist across save/reload (verified via T18 smoke)
- [ ] Trait drift compounds correctly (verified by running 5 ceremonies in sequence and inspecting trait values)
- [ ] Witness reactions fire correctly (verified via Hero.GetRelation diff before/after each ceremony)
- [ ] Cultural variant selection matches player's enlisted faction culture (verified across 3+ faction enlistments)
- [ ] Phase 20 validator populated and passes
- [ ] Verification report committed at `docs/superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc-verification.md`

---

## §9 — Definition of done

Plan 3 is complete when:

1. All 20 tasks marked ✅ done with their per-task verifications passed.
2. All §8 verification checkboxes pass.
3. Verification report committed.
4. CLAUDE.md current-status block updated to reference Plan 3 shipped.
5. Plans 4-7 can begin parallel implementation.

---

## §10 — Hand-off to Plans 4-7

After Plan 3 ships, downstream plans inherit:

### For Plan 4 (Officer Trajectory)
- T6→T7 ceremony (T14) is the natural moment for Plan 4's gear-delta application. Plan 4 can subscribe to `OnTierChanged` independently OR add an effect ID to ceremony T14's "accept" options that applies banner/cape/weapon-modifier.
- Officer dialog gating: Plan 4 reads `FlagStore.GetString("ceremony.t7.choice")` to flavor officer-tier dialog (e.g. "humble accept" choice → Lord addresses player as "{=t7_humble_address}Steady captain" vs "proud accept" → "{=t7_proud_address}Bold captain").

### For Plan 5 (Endeavor System)
- Endeavor categories may gate or flavor on ceremony choice flags. E.g. T2→T3 "frugal" choice unlocks a Rogue endeavor "Run a tight dice game" with bonus.
- Endeavor witness drift: high-relation companions provide bonus to endeavor success rolls.

### For Plan 6 (Roll of Patrons)
- Patron relation snapshots include ceremony-driven trait/relation drift accumulated during prior service.
- Patron favor outcomes may flavor on ceremony choice flags ("you've always been calculating, so I'm not surprised you're asking for a loan").

### For Plan 7 (Personal Kit + Lifestyle + Smoke)
- Lifestyle unlock pre-conditions may include ceremony choice flags (e.g. Forager lifestyle unlocks via T2→T3 "frugal" choice).
- Smoke testing matrix includes ceremony × companion × endeavor × patron interaction scenarios.

---

## §11 — Out of scope

- Officer Trajectory equipment / dialog (Plan 4)
- Endeavor System (Plan 5)
- Patron favors (Plan 6)
- Personal Kit (Plan 7)
- Lifestyle Unlocks (Plan 7)
- Multi-phase ceremonies (v1 ships single-phase; multi-phase deferred)
- Full per-culture variants (Battanian / Aserai / Khuzait specific) — fallback per §4.3 covers v1
- Save migration of ceremony flags from older saves (FlagStore handles gracefully; no migration code needed)
- News-feed integration of ceremony events (separate news-v2 substrate)

---

## §12 — References

- [Plan 1 — Architecture Foundation](2026-04-24-ck3-wanderer-architecture-foundation.md)
- [Plan 2 — Companion Substrate](2026-04-24-ck3-wanderer-companion-substrate.md)
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)
- [Spec v6](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)
- [AGENTS.md](../../../AGENTS.md)
- [CLAUDE.md](../../../CLAUDE.md)
- Existing `EnlistmentBehavior.OnTierChanged` event + `SetTier` method
- Existing `PromotionBehavior.CheckForPromotion` (proving-event path)
- Existing `EnlistedDialogManager` T6→T7 dialog branch
- Existing `RankCeremonyBehavior` (Plan 1 T11 stub, Plan 3 populates)
- Existing `ModalEventBuilder.FireCeremony` (Plan 1 T5)
- Existing `HomeEveningMenuProvider.OnSlotSelected` (canonical menu→modal precedent)
- Existing `StoryletCatalog`, `StoryletEventAdapter`, `StoryDirector`, `EventDeliveryManager`
- Existing `EffectExecutor`, `FlagStore`, `QualityStore`
- Decompile: `ChangeRelationAction`, `DefaultTraits`, `Hero.SetTraitLevel`
