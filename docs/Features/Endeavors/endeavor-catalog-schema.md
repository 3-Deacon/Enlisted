# Endeavor Catalog — Schema Reference

**File:** `ModuleData/Enlisted/Endeavors/endeavor_catalog.json` (one file holds all endeavor templates).
**Sibling file:** `ModuleData/Enlisted/Endeavors/contract_archetypes.json` (notable-issued contracts; same schema family with three field swaps — see §8).

**Loader:** `Enlisted.Features.Endeavors.EndeavorCatalog` (`LoadAll` runs from the host behavior's `OnSessionLaunchedEvent`).

**Owning plan:** [Plan 5 — Endeavor System](../../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system.md) (CK3 wanderer mechanics cluster, plan 5 of 7).

**Status:** Living reference. Schema is `schemaVersion: 1`.

---

## 1. Purpose

An *endeavor template* declares one player-initiated, multi-phase activity the player can pick from the Camp → Endeavors sub-menu. Each template:

- Belongs to one of five categories (Soldier / Rogue / Medical / Scouting / Social — see Plan 5 §4.1).
- Carries an ordered list of **phase storylet IDs** (`phase_pool`) that fire one-per-phase as Modal events.
- Carries up to three **resolution storylet IDs** (success / partial / failure) keyed by accumulated outcome.
- Declares its **gating axis** (one or more `skill_axis` ids) and optional **companion slots**.
- Declares **scrutiny risk** (Rogue-category endeavors only — non-zero per-phase discovery roll).
- Is selected from the available list when its hybrid gate (player skill ≥ threshold OR matching companion present) passes.

The endeavor templates do **not** define their own modal text or option effects — those live in the referenced phase storylets, which follow the standard [storylet schema](../Content/storylet-backbone.md#storylet-schema). The endeavor catalog is purely the *index*.

---

## 2. Phase model — driven by `EndeavorRunner`, NOT the Activity backbone

Per the plan's [Lock 9](../../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system.md#-locked-2026-04-26--readiness-amendments-pre-execution), endeavor phases are **NOT** advanced by `ActivityRuntime.TryAdvancePhase` (the backbone's automatic per-tick advancement that uses `Phase.DurationHours`). Instead:

- `EndeavorActivity` registers under a thin `activity_endeavor.json` `ActivityTypeDefinition` with one long-duration "running" phase (Auto delivery, empty `Pool`). This keeps `ActivityRuntime` quiet (no false-positive `Surfaced` log on `Start`) and preserves save-load polymorphism via `List<Activity>` at offset 57.
- `EndeavorRunner` (a `CampaignBehaviorBase`) subscribes to `OnHourlyTick`, walks active endeavors, and advances each one's phase index when its phase-duration has elapsed. The **current phase index is a reserved key** in `EndeavorActivity.AccumulatedOutcomes` (`__phase_index__`), not a separate field.
- When `EndeavorRunner` advances the phase, it picks `template.phase_pool[index]`, builds context, and fires the storylet via `ModalEventBuilder.FireEndeavorPhase(storyletId, ctx, activity)`. After the player picks an option, the choice is persisted as `phase_<N>_choice = <option_id_hash>` in `AccumulatedOutcomes` and the runner waits for the next phase's duration.
- When all phases have fired, the runner picks the resolution storylet (success / partial / failure based on a tally of `phase_<N>_choice` outcomes vs `template.resolution_thresholds`), fires it, then `Stop()`s the activity through `ActivityRuntime`.

**Authoring takeaway:** `phase_pool` is an *ordered* list. The N-th entry is the storylet that fires for phase N. No weighted bag, no intent bias — just one storylet per phase.

---

## 3. Top-level file shape

```json
{
  "schemaVersion": 1,
  "endeavors": [
    { "id": "endeavor.soldier.drill_competition", "...": "..." },
    { "id": "endeavor.rogue.dice_game",            "...": "..." }
  ]
}
```

The loader reads the `endeavors` array and registers each entry by its `id` string. Last write wins on duplicate IDs (same as `StoryletCatalog`).

---

## 4. Endeavor template fields

| Field | Type | Required | Notes |
| :-- | :-- | :--: | :-- |
| `id` | string | yes | `endeavor.<category>.<short_name>` — e.g. `endeavor.soldier.drill_competition`. Stable identifier across saves |
| `category` | string | yes | One of `soldier`, `rogue`, `medical`, `scouting`, `social` |
| `title_id` / `title` | string / string | yes | Inline loc-key + fallback; surfaces in the sub-menu list and as the storylet title at fire time |
| `description_id` / `description` | string / string | yes | Sub-menu hover description (≤ 200 chars). Plain-prose, not Markdown |
| `duration_days` | int | yes | Total in-game days from start to resolution. Range: `1..14`. Phase 1 fires `phase_1_offset_hours` after start; subsequent phases fire `phase_pool.length / duration_days` apart unless `phase_offsets_hours` is provided |
| `phase_offsets_hours` | int[] | no | Per-phase fire offset from `StartedOn` in hours. Length must equal `phase_pool.length`. If omitted, `EndeavorRunner` computes evenly-spaced offsets across `duration_days * 24` |
| `phase_pool` | string[] | yes | Ordered storylet IDs, one per phase. Range: `1..4` entries. Each ID must resolve in `StoryletCatalog` (Phase 19 validator enforces) |
| `resolution_storylets` | object | yes | `{ "success": "...", "partial": "...", "failure": "..." }` — three storylet IDs. All three must resolve in `StoryletCatalog`. `partial` is optional (`null` allowed); `success` + `failure` are required |
| `resolution_thresholds` | object | no | `{ "success_min_score": int, "failure_max_score": int }` — score bounds for which resolution fires. Defaults: success ≥ +2, failure ≤ -2, otherwise partial. Score is the sum of per-option `score_delta` effects across all phase choices (see §6) |
| `skill_axis` | string[] | yes | One or more skill IDs the endeavor leans on (used for hybrid gating + skill-XP effects). Allowed values: `one_handed`, `two_handed`, `polearm`, `bow`, `crossbow`, `throwing`, `riding`, `athletics`, `roguery`, `charm`, `leadership`, `medicine`, `tactics`, `scouting`, `steward`, `engineering`. Phase 19 validator checks against `Decompile/.../DefaultSkills.cs` enum names |
| `self_gate_skill_threshold` | int | yes | Minimum skill value (0-300) the player must have in **at least one** of the `skill_axis` skills to unlock the endeavor without a matching companion. Range: `0..300` |
| `companion_slots` | array | no | 0-2 companion slot definitions (see §5). Empty / missing means "no companion can join" — purely solo endeavor |
| `scrutiny_risk_per_phase` | float | yes | Probability (0.0-1.0) of a discovery roll firing each phase. `0.0` for non-Rogue categories (Phase 19 validator enforces); typical Rogue value 0.05-0.20. Companion Charm + player Charm reduce the actual roll at fire time — see Plan 5 §4.6 |
| `tier_min` | int | no | Minimum `EnlistmentTier` the player must hold to see the endeavor. Default 1. Range: `1..9` |
| `flag_gates` | string[] | no | List of `FlagStore` boolean keys that must ALL be set (`Has` returns true) for the endeavor to be available. Used for cross-system gating — e.g. `["ceremony_choice_t2_frugal"]` requires the player picked the `frugal` option at the T2 ceremony |
| `flag_blockers` | string[] | no | List of `FlagStore` boolean keys that must ALL be UNSET. Inverse of `flag_gates` |
| `tags` | string[] | no | Free-form labels for filtering / debug (e.g. `["t5_plus", "officer_only", "stretch_goal"]`) — never gate on tags from C# |

---

## 5. `companion_slots` shape

A companion slot declares "this endeavor accepts a companion in role X". The sub-menu's setup inquiry presents the player with the list of currently-spawned companions matching the slot archetype; player can assign one. Assigning sets `CompanionAssignmentManager.SetAssignedToEndeavor(hero, true)` for the duration.

```json
"companion_slots": [
  { "archetype": "veteran",     "role": "mentor",   "required": false, "skill_bonus_axis": "one_handed", "skill_bonus_amount": 30 },
  { "archetype": "field_medic", "role": "advisor",  "required": false, "skill_bonus_axis": "charm",      "skill_bonus_amount": 20 }
]
```

| Field | Type | Required | Notes |
| :-- | :-- | :--: | :-- |
| `archetype` | string | yes | One of Plan 2's six archetypes: `sergeant`, `field_medic`, `pathfinder`, `veteran`, `qm_officer`, `junior_officer`. Phase 19 validator enforces |
| `role` | string | yes | Free-form label surfaced in the assign-companion inquiry text (e.g. `mentor`, `advisor`, `lookout`). Not a behavior switch — purely flavor for the player |
| `required` | bool | no | If `true`, the endeavor is unavailable when no companion of this archetype is currently spawned (overrides hybrid gating's "skill OR companion" — `required: true` makes companion mandatory). Default `false` |
| `skill_bonus_axis` | string | no | Skill ID the companion contributes to during phase resolution rolls. Effective skill at roll time = `player.GetSkillValue(axis) + (companionAssigned ? skill_bonus_amount : 0)` |
| `skill_bonus_amount` | int | no | Magnitude of the companion's skill contribution. Range: `0..50`. Defaults to `0` if `skill_bonus_axis` omitted |

**Cap.** Maximum 2 slots per template (Plan 5 §4.5). Phase 19 validator enforces.

**Stay-back independence.** A companion assigned to an endeavor is locked from joining a *different* endeavor for the duration, but can still be set Fight / Stay-Back for combat (Plan 1 hand-off note). The two flags are separate booleans on `CompanionAssignmentManager`.

---

## 6. Choice memory + score accumulation

Phase storylets are regular storylets (see [storylet-backbone.md](../Content/storylet-backbone.md#storylet-schema)) with one ceremony-style convention layer: each option's `effects` array should set a per-phase choice flag and a per-phase score delta.

```json
{
  "id": "drill_phase1_drill_hard",
  "text": "{=endeavor_soldier_drill_phase1_drill_hard}Drill until your arms ache.",
  "tooltip": "{=endeavor_soldier_drill_phase1_drill_hard_tt}+50 XP One-Handed. Sets up phase 2 with a tired-arms penalty.",
  "effects": [
    { "apply": "endeavor_skill_xp_one_handed_major" },
    { "apply": "endeavor_set_choice_flag", "name": "endeavor_choice_endeavor.soldier.drill_competition_1_drill_hard" },
    { "apply": "endeavor_set_score", "name": "endeavor_score_endeavor.soldier.drill_competition", "delta": 1 }
  ]
}
```

**Choice flag naming.** Per [Lock 2](../../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system.md#-locked-2026-04-26--readiness-amendments-pre-execution), choice flags use flat-underscore naming: `endeavor_choice_<endeavor_id>_<phase_index>_<option_id>`. The N-th phase's options should set the matching N. `EndeavorRunner` reads these to:

1. Determine "did this option fire" downstream (e.g. phase 2 storylet may flavor based on phase 1's pick).
2. Tally the `endeavor_score_<endeavor_id>` accumulator (via the `endeavor_set_score` scripted-effect's `delta` argument) which gates which `resolution_storylets` entry fires.

**Score primitive.** Score writes go through the `endeavor_score_plus_1` / `endeavor_score_minus_1` / `_plus_2` / `_minus_2` scripted-effect family (T6 catalog). Each is a closed wrapper around `quality_add` against the single global quality `endeavor_active_score` (range -20..+20, default 0). `EndeavorRunner` clears the quality to 0 on endeavor start AND finish so the score doesn't leak between endeavors.

---

## 7. Resolution selection

When all `phase_pool` entries have fired and the player has picked an option for each, `EndeavorRunner` reads the accumulated `endeavor_score_<endeavor_id>` quality and selects:

| Score | Resolution storylet |
| :--: | :-- |
| `>= success_min_score` (default `+2`) | `success` |
| `<= failure_max_score` (default `-2`) | `failure` |
| otherwise | `partial` (or `success` if `partial` is null and score ≥ 0; else `failure`) |

The chosen storylet fires as a Modal via `ModalEventBuilder.FireEndeavorPhase` with `ChainContinuation = true`. Once the player picks an option in the resolution modal, `EndeavorRunner` calls `ActivityRuntime.Stop(activity, ActivityEndReason.Completed)` and clears all `CompanionAssignmentManager.SetAssignedToEndeavor(hero, false)` flags for the assigned agents.

---

## 8. Contract templates — same schema with three swaps

`contract_archetypes.json` uses the **same schema** as `endeavor_catalog.json` with three field replacements:

| Endeavor field | Contract replacement | Notes |
| :-- | :-- | :-- |
| `category` | `notable_type` | `headman`, `merchant`, `gangleader`, `artisan`, `rural_notable`, `urban_notable` (matches `Hero.IsNotable` types in vanilla) |
| (n/a) | `payment_denars` | Base reward in denars (`int`) — applied via `give_gold` in the resolution `success` storylet |
| `tier_min` | `notable_relation_min` | Relation gate against the issuing notable (default `0` — any non-hostile notable can offer) |

Top-level array is `contracts` instead of `endeavors`. Resolution thresholds, phase pool, scrutiny, companion slots all behave identically. A contract's `id` follows `contract.<notable_type>.<short_name>` (e.g. `contract.headman.find_runaway_apprentice`).

---

## 9. Validation (Phase 19, added in Plan 5 T28)

`Tools/Validation/validate_content.py` Phase 19 fail-closes on:

- **Schema:** missing required fields (`id`, `category`, `phase_pool`, `resolution_storylets`, `skill_axis`, `self_gate_skill_threshold`, `scrutiny_risk_per_phase`).
- **Reference integrity:** every `phase_pool` entry resolves in `StoryletCatalog`; `resolution_storylets.success` + `.failure` resolve; `.partial` resolves if non-null; every `companion_slots[].archetype` matches a Plan 2 archetype ID; every `flag_gates`/`flag_blockers` entry is a valid flat-underscore flag name.
- **Bounds:** `duration_days` in `[1, 14]`; `scrutiny_risk_per_phase` in `[0.0, 1.0]`; `tier_min` in `[1, 9]`; `companion_slots.length` in `[0, 2]`; `phase_pool.length` in `[1, 4]`; `skill_axis` entries are valid skill IDs.
- **Category-scrutiny coherence:** `category != "rogue"` MUST have `scrutiny_risk_per_phase == 0.0`. Non-rogue templates with non-zero scrutiny are a build error.
- **Effect references:** Phase 12 (existing) catches unknown `apply` IDs in the *referenced storylets*' option effects — Phase 19 does not re-walk option effects. Authoring should still cross-check that custom `endeavor_*` scripted-effect IDs are registered in `scripted_effects.json` (T6 lands the seed catalog).

---

## 10. Schema-locked example (full)

```json
{
  "schemaVersion": 1,
  "endeavors": [
    {
      "id": "endeavor.soldier.drill_competition",
      "category": "soldier",
      "title_id": "endeavor_soldier_drill_competition_title",
      "title": "Win the regimental drill competition",
      "description_id": "endeavor_soldier_drill_competition_desc",
      "description": "Outperform your peers in the company's monthly drill. Recognition or shame, all in five days.",
      "duration_days": 5,
      "phase_pool": [
        "endeavor_soldier_drill_phase1",
        "endeavor_soldier_drill_phase2"
      ],
      "resolution_storylets": {
        "success": "endeavor_soldier_drill_resolution_success",
        "partial": "endeavor_soldier_drill_resolution_partial",
        "failure": "endeavor_soldier_drill_resolution_failure"
      },
      "skill_axis": ["one_handed", "athletics"],
      "self_gate_skill_threshold": 50,
      "companion_slots": [
        {
          "archetype": "veteran",
          "role": "mentor",
          "required": false,
          "skill_bonus_axis": "one_handed",
          "skill_bonus_amount": 30
        }
      ],
      "scrutiny_risk_per_phase": 0.0,
      "tier_min": 1,
      "tags": ["soldier_intro"]
    }
  ]
}
```

---

## 11. References

- [Plan 5 — Endeavor System](../../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system.md) — owning plan, locked design decisions
- [Endeavor design guide](endeavor-design-guide.md) — category boundaries, scrutiny tuning, companion synergy patterns, phase pacing (T2 deliverable)
- [Storylet backbone reference](../Content/storylet-backbone.md) — base schema for the phase + resolution storylets the endeavor references
- [Companion archetype catalog](../Companions/companion-archetype-catalog.md) — Plan 2 archetype IDs (`sergeant`, `field_medic`, etc.)
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md) — locked decisions Plan 5 inherits (offset 57, namespace `Enlisted.Features.Endeavors`)
- `src/Features/Endeavors/EndeavorActivity.cs` — save-class POCO at offset 57
- `src/Features/Endeavors/EndeavorCatalog.cs` — JSON loader (T10 deliverable)
- `src/Features/Endeavors/EndeavorRunner.cs` — phase advancement + resolution dispatch (T12 deliverable)
- `src/Features/Endeavors/EndeavorPhaseProvider.cs` — modal-firing helper modeled on `HomeEveningMenuProvider` (T9 deliverable)
- `src/Mod.Core/Helpers/ModalEventBuilder.cs` — `FireEndeavorPhase` entry point
- `ModuleData/Enlisted/Effects/scripted_effects.json` — endeavor-specific scripted effects (T6 deliverable)
