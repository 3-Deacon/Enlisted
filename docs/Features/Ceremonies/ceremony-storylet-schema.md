# Ceremony Storylet — Schema Reference

**Files:** `ModuleData/Enlisted/Storylets/ceremony_t{prev}_to_t{curr}.json`

**Loader:** `Enlisted.Features.Content.StoryletCatalog` (the same loader that handles every other storylet — ceremonies are a content convention, not a separate substrate).

**Owning plan:** [Plan 3 — Rank-Ceremony Arc](../../superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc.md) (CK3 wanderer mechanics cluster, plan 3 of 7).

**Status:** Living reference. Schema is `schemaVersion: 1` for the storylet array shape (every storylet file).

---

## 1. Purpose

A ceremony storylet is an ordinary storylet that fires as a Modal at a tier transition via `RankCeremonyBehavior.OnTierChanged → CeremonyProvider.FireCeremonyForTier`. Each tier transition that ships a ceremony has one JSON file containing **four storylet entries**: a `base` variant (catch-all) and three cultural variants (`vlandian`, `sturgian`, `imperial`).

Ceremonies are not a new substrate. They reuse:

- the existing storylet schema (`Storylet.cs`, `Choice.cs`, `EffectDecl.cs`),
- `ModalEventBuilder.FireCeremony` (Plan 1 helper) for the modal pipeline,
- existing primitives `trait_xp`, `relation_change`, `set_flag`, `grant_renown` (no new primitives needed),
- `FlagStore` for choice memory (one bool flag per option, not a string store).

This doc is the cheat-sheet for *what fields a ceremony storylet uses* and *what conventions matter for the runtime to wire correctly*.

---

## 2. Five tier transitions ship ceremonies

| Tier transition | newTier | File | Witnesses (Plan 2 spawned) |
| :-: | :-: | :-- | :-- |
| T1 → T2 | 2 | `ceremony_t1_to_t2.json` | Sergeant only |
| T2 → T3 | 3 | `ceremony_t2_to_t3.json` | Sergeant (+ ambient NPC fellow recruits) |
| T4 → T5 | 5 | `ceremony_t4_to_t5.json` | Lord + Sergeant + Field Medic |
| **T6 → T7** | 7 | `ceremony_t6_to_t7.json` | **Lord + all 4 spawned companions + soldiers (THE COMMISSION)** |
| T7 → T8 | 8 | `ceremony_t7_to_t8.json` | Junior Officer + QM Officer + Veteran |

**Three transitions intentionally skip** because `PathCrossroadsBehavior` already fires a Modal at those tiers: `newTier = 4` (T3→T4), `newTier = 6` (T5→T6), `newTier = 9` (T8→T9). See Plan 3 §4.7 lock decisions.

---

## 3. Top-level file shape

Every ceremony JSON file is a regular storylet collection:

```json
{
  "storylets": [
    { "id": "ceremony_t1_to_t2_base",     "...": "..." },
    { "id": "ceremony_t1_to_t2_vlandian", "...": "..." },
    { "id": "ceremony_t1_to_t2_sturgian", "...": "..." },
    { "id": "ceremony_t1_to_t2_imperial", "...": "..." }
  ]
}
```

Storylet IDs follow the locked pattern `ceremony_t{prev}_to_t{curr}_{suffix}` where `suffix ∈ { base, vlandian, sturgian, imperial }`. `CeremonyCultureSelector.ResolveStoryletId(newTier)` returns this exact ID at fire time.

---

## 4. Required per-storylet fields

Ceremonies use the standard storylet schema (see [storylet-backbone.md](../Content/storylet-backbone.md) §"Storylet schema"). The fields below are the ceremony-relevant subset:

| Field | Type | Required | Notes |
| :-- | :-- | :--: | :-- |
| `id` | string | yes | `ceremony_t{prev}_to_t{curr}_{suffix}` per §3 |
| `category` | string | yes | `"ceremony"` |
| `trigger` | string[] | yes | `["is_enlisted"]` — ceremonies are gated by `RankCeremonyBehavior` and `CeremonyProvider` dedup, not by storylet trigger predicates |
| `scope` | object | yes | `{ "context": "any" }` — ceremonies fire from any context (settlement / camp / overland) |
| `title` | string | yes | Inline loc-key + fallback: `"{=ceremony_t1_to_t2_base_title}First Blood"` |
| `setup` | string | yes | Inline loc-key + fallback. **Must reference `{PLAYER_NAME}`, `{PLAYER_RANK}`, `{LORD_NAME}` tokens** rather than literal "soldier" / "Sergeant" / "the lord" — see §6 below |
| `options` | array | yes | 2-4 player choices (the character-defining decisions) |

`category: "ceremony"` opts the storylet into the StoryDirector category-cooldown bucket — but `ChainContinuation = true` (set by `ModalEventBuilder.FireCeremony`) bypasses the cooldown anyway. Keeping `ceremony` as the category lets future surfaces filter on it (news feed, debug tooling).

---

## 5. Options block — ceremony-specific conventions

Each `options[]` entry is a regular `Choice` with three convention layers:

```json
{
  "id": "trust_sergeant",
  "text": "{=ceremony_t1_to_t2_base_trust_sergeant}My sergeant kept me alive.",
  "tooltip": "{=ceremony_t1_to_t2_base_trust_sergeant_tt}+1 Honor. Sergeant strongly approves.",
  "effects": [
    { "apply": "ceremony_trait_drift_honor_up" },
    { "apply": "ceremony_witness_reaction_sergeant_strong_approve" },
    { "apply": "set_flag", "name": "ceremony_choice_t2_trust_sergeant" },
    { "apply": "set_flag", "name": "ceremony_fired_t2" }
  ]
}
```

### 5.1 Trait drift effect

Every option must apply exactly one trait drift via the `ceremony_trait_drift_<trait>_<direction>` scripted-effect family:

| Trait id | Up | Down |
| :-- | :-- | :-- |
| `Mercy` | `ceremony_trait_drift_mercy_up` | `ceremony_trait_drift_mercy_down` |
| `Valor` | `ceremony_trait_drift_valor_up` | `ceremony_trait_drift_valor_down` |
| `Honor` | `ceremony_trait_drift_honor_up` | `ceremony_trait_drift_honor_down` |
| `Generosity` | `ceremony_trait_drift_generosity_up` | `ceremony_trait_drift_generosity_down` |
| `Calculating` | `ceremony_trait_drift_calculating_up` | `ceremony_trait_drift_calculating_down` |

These resolve to the existing `trait_xp` primitive (which steps ±1 per call via `Math.Sign(amount)`). Authoring referenced these scripted-effect IDs **must register them in `ModuleData/Enlisted/Effects/scripted_effects.json`** — Phase 12 validator rejects unknown `apply` values.

### 5.2 Witness reaction effects

For each on-tier witness companion that should react, append one scripted-effect call per reaction strength:

| Reaction | Scripted effect ID pattern | Relation delta |
| :-- | :-- | :-: |
| `approve` | `ceremony_witness_reaction_<archetype>_approve` | +5 |
| `disapprove` | `ceremony_witness_reaction_<archetype>_disapprove` | −5 |
| `strong_approve` | `ceremony_witness_reaction_<archetype>_strong_approve` | +10 |
| `strong_disapprove` | `ceremony_witness_reaction_<archetype>_strong_disapprove` | −10 |
| `neutral` | (no effect — omit) | 0 |

Archetype IDs match Plan 2's spawned companions: `sergeant`, `field_medic`, `pathfinder`, `veteran`, `qm_officer`, `junior_officer`. The lord witness (T4→T5 + T6→T7 only) uses archetype `lord`.

These resolve to the existing `relation_change` primitive with `target_slot: witness_<archetype>`. `CeremonyProvider` populates `ctx.ResolvedSlots["witness_<archetype>"] = hero` before firing the modal, so the slot lookup at effect-apply time always resolves to the correct hero.

If the witness archetype isn't currently spawned (e.g. Veteran at T4→T5 — only spawns at T5), the `relation_change` primitive Expected-logs and skips gracefully. Authoring should still include the reaction line; the runtime handles "not present" cleanly.

### 5.3 Choice memory flags

Every option must set its own choice flag via `set_flag`:

```json
{ "apply": "set_flag", "name": "ceremony_choice_t{N}_<choice_id>" }
```

E.g. picking the `trust_sergeant` option at T2 sets `ceremony_choice_t2_trust_sergeant = true`. **One bool flag per option** — `FlagStore` does not store strings, so the choice identity is encoded in the flag name itself.

Plans 4-7 read these flags via `FlagStore.Instance.Has("ceremony_choice_t{N}_<choice_id>")` to gate or flavor downstream content.

### 5.4 Dedup flag

Every option must also set the dedup flag:

```json
{ "apply": "set_flag", "name": "ceremony_fired_t{N}" }
```

`CeremonyProvider.FireCeremonyForTier(N)` checks this flag at the top and short-circuits if set. Setting the flag from the option (not from the Provider) means a player who closes the modal mid-air without picking gets the ceremony retried later — the dedup engages only on actual choice resolution.

---

## 6. Token interpolation contract

Per architecture brief §3 + AGENTS.md pitfall #23, ceremony storylet `setup` and `options[].text` strings should reference these tokens rather than hard-coding literals:

| Token | Source | Notes |
| :-- | :-- | :-- |
| `{PLAYER_NAME}` | `Hero.MainHero.Name` | Hard-coding "soldier" or "lad" strips the player's chosen name |
| `{PLAYER_RANK}` | `RankHelper.GetCurrentRank(...)` | **Culture-aware** — reads `progression_config.json` for per-kingdom rank titles. Hard-coding "Sergeant" silently breaks Sturgian / Khuzait / Aserai / etc. native rank work |
| `{LORD_NAME}` | `EnlistmentBehavior.Instance.EnlistedLord.Name` | Use instead of "the lord" / "him" |

Token resolution requires the firing path to populate the variables via `MBTextManager.SetTextVariable` before opening the modal. `CeremonyProvider.FireCeremonyForTier` does this in `BuildCeremonyContext` before calling `ModalEventBuilder.FireCeremony`.

The QM flow (`EnlistedDialogManager.SetCommonDialogueVariables`) and Plan 2 companion flow (`EnlistedMenuBehavior.SetCompanionConversationTokens`) populate these tokens for their conversations — those tokens persist across the global `MBTextManager` bag, so a ceremony fired after a QM or companion conversation already has them set. But a ceremony fired from a fresh game state (e.g. T1→T2 immediately after enlistment, before the player has opened the QM dialog) must populate its own tokens. `CeremonyProvider` does the SetTextVariable batch unconditionally on every fire so ordering doesn't matter.

---

## 7. Cultural variants

The four storylet entries per file (`base` + 3 culture variants) share **the same options array and effect lists**. Only the `setup` text differs to reflect the cultural texture (Vlandian feast vs Sturgian fire-circle vs Imperial morning muster vs generic). The base variant is the catch-all when the player's enlisted lord's culture doesn't map to one of the three (Battanian / Aserai / Khuzait fall back per `CeremonyCultureSelector` §4.3 of the plan).

`title` text usually stays consistent across variants (the question is the same, only the setting changes), but cultural variants may flavor the title too if it improves immersion.

---

## 8. Validation

`Tools/Validation/validate_content.py` Phase 20 (added in Plan 3 T17) checks each retained tier transition has at least the `_base` storylet authored. The 3 cultural variants are recommended but not strictly required for ship — Plan 7 polish pass adds full per-culture coverage if needed.

Phase 12 (existing) rejects:
- Unknown `apply` values in effects (every `ceremony_trait_drift_*` and `ceremony_witness_reaction_*` ID must exist in `scripted_effects.json`).
- Writes to read-only qualities (`rank`, `days_in_rank`, `days_enlisted` — never write these from a ceremony effect; tier advancement routes through `EnlistmentBehavior.SetTier`).

Phase 11 separately fails the build if any `ModLogger.Error(...)` call appears in `src/`.

---

## 9. References

- [Plan 3 — Rank-Ceremony Arc](../../superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc.md) — owning plan
- [Storylet backbone reference](../Content/storylet-backbone.md) — base schema + primitive list
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md) — locked decisions Plan 3 inherits
- [Companion archetype catalog](../Companions/companion-archetype-catalog.md) — Plan 2 archetype IDs the witness reactions reference
- `src/Features/Content/EffectExecutor.cs` — primitive dispatch (`trait_xp`, `relation_change`, `set_flag`)
- `src/Features/Content/StoryletEventAdapter.cs` — `BuildModal` + pending-effects registry
- `src/Mod.Core/Helpers/ModalEventBuilder.cs` — `FireCeremony` entry point
- `ModuleData/Enlisted/Effects/scripted_effects.json` — scripted-effect catalog
