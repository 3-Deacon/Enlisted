# Endeavor Design Guide

**Status:** Living reference. The schema cheat-sheet is at [endeavor-catalog-schema.md](endeavor-catalog-schema.md); this file is the *design* counterpart — what to put in the fields, not what fields exist.

**Owning plan:** [Plan 5 — Endeavor System](../../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system.md).

**Read the [writing style guide](../Content/writing-style-guide.md) first** for voice, tense, and tone — this guide does not re-derive those rules.

---

## 1. What an endeavor IS (and isn't)

An endeavor is a **player-initiated, multi-day mini-arc**. The player declares "I am pursuing X" from the Camp menu; for the next 3-7 in-game days, 2-3 modal beats fire that surface choices about *how* they pursue X; a resolution modal fires that pays out (or punishes) based on the choices made.

| It IS | It is NOT |
| :-- | :-- |
| Player-driven (chosen, not pushed) | An ambient event (those go through ActivityRuntime auto-fire) |
| Multi-phase with discrete choices | A single-screen decision (those are storylets fired ad-hoc) |
| Resolved within 3-7 in-game days | A long campaign goal (those are duty profiles or path scores) |
| Bounded reward + risk profile | A free-form "do whatever you want" sandbox |
| One active major endeavor at a time (§4.4) | Stackable concurrent objectives |

If a feature wants to push something at the player without the player asking for it, it belongs in an Order (lord-issued) or a Contract (notable-issued). Endeavors are the **player's** verb.

---

## 2. The five categories — distinctive identities

Each category has a **dominant emotion** and a **dominant verb**. When authoring, lean into those rather than blurring categories.

### 2.1 Soldier — *pride / craft*

**Verbs:** drill, train, lead, master, prove, mentor.
**Tone:** earned competence, the small dignity of a kept blade.
**Skill axis:** combat skills + Athletics + Leadership.
**Player at:** any tier; default starting category.
**Companion synergy:** Veteran (mentor), Sergeant (drill partner), Junior Officer (peer challenge at T7+).

A Soldier endeavor is *about getting better at the work*. The reward is mastery, recognition, and small relation gains with combatant peers. There is **no scrutiny risk** (the company expects you to drill).

Common option-shape:
- Hard-grind option (more XP, exhausts you for downstream phase)
- Smart-work option (less XP, sets up phase 2 with a positional bonus)
- Show-off option (relation +/- with witnesses depending on culture)

### 2.2 Rogue — *transgression / nerve*

**Verbs:** steal, smuggle, gamble, bribe, lie, fence.
**Tone:** the sharp pleasure of getting away with it; the cold sweat when you almost don't.
**Skill axis:** Roguery + Charm.
**Player at:** any tier (some templates `tier_min: 3+` for scope).
**Companion synergy:** Field Medic (Charm cover) reduces scrutiny; Veteran with Roguery >50 (rare archetype roll) can be a partner-in-crime.

A Rogue endeavor is *about taking what isn't given*. Rewards are **lop-sided** — large gold or unique flag unlocks vs. proportionally large scrutiny risk. **Always non-zero** `scrutiny_risk_per_phase` (Phase 19 validator enforces).

Common option-shape:
- Brash option (high reward, high scrutiny modifier — "+0.10 to phase risk")
- Clever option (medium reward, scrutiny-mitigation modifier — "-0.05 to phase risk")
- Bail-out option (forfeit phase reward, kill the scrutiny roll for this phase)

Rogue resolutions almost always have a sharp partial-vs-failure split. Success → gold + lord-relation drift up *if not caught*; failure → lord relation hammered + scrutiny spike + downstream `flogging_storylet` chain (Plan 7 polish).

### 2.3 Medical — *care / patience*

**Verbs:** tend, forage, recipe, mix, bandage, ease.
**Tone:** the quiet of a sick-tent; the smell of crushed herbs.
**Skill axis:** Medicine + Steward.
**Player at:** Medicine 50+ self-gate, OR Field Medic companion (massive bonus).
**Companion synergy:** Field Medic is the **dominant** synergy here — `skill_bonus_amount: 50` is appropriate for a Field Medic on a Medical endeavor (the most a companion ever contributes in any category).

Medical endeavors are *about service to the company*, not personal advancement. Rewards are diffuse — `quality_add: readiness +5`, `quality_add: medical_risk -1`, lord relation small drift up. Almost no scrutiny. They feel like the Medical Tent in Camp life from a real soldier's diary: not dramatic, but the difference between a company that survives the winter and one that doesn't.

Common option-shape:
- Heavy-effort option (more readiness/medical_risk delta, exhausts player Fatigue)
- Delegate-to-Field-Medic option (smaller delta, no Fatigue, only available with Field Medic assigned)
- Cut-corners option (small delta, scrutiny +5 risk one-time — the company doctor noticed)

### 2.4 Scouting — *patience / risk*

**Verbs:** map, track, recon, watch, mark, sketch.
**Tone:** the cold of pre-dawn; the held breath when the patrol passes ten yards out.
**Skill axis:** Scouting + Riding + Bow.
**Player at:** Scouting 50+ self-gate, OR Pathfinder companion.
**Companion synergy:** Pathfinder (Scouting 80) is the natural agent.

Scouting endeavors are *about information*, paid out as `lord_relation_up_*` (intel reports impressing the lord), unique `flag_set` unlocks for downstream content (e.g. `enlisted_knows_enemy_size = true` flavors next battle's pre-modal), or rare gold (the lord pays for actionable intel).

**Mild scrutiny** for `track_patrol` and similar (you're sneaking around enemy positions; if you're seen by their pickets it counts as fraternization at best, desertion at worst). Other Scouting endeavors are scrutiny-free.

Common option-shape:
- Aggressive option (deeper recon, larger reward, higher per-phase scrutiny)
- Conservative option (smaller reward, no scrutiny)
- Delegate-to-Pathfinder option (Pathfinder slot only — small reward, near-zero risk)

### 2.5 Social — *bond / leverage*

**Verbs:** befriend, court, sway, deepen, ally, confide.
**Tone:** what's left unsaid; the long pause before a yes.
**Skill axis:** Charm + Leadership.
**Player at:** Charm 50+ OR Leadership 60+ self-gate, OR any companion-as-confidant (any high-relation Plan 2 companion satisfies the gate).
**Companion synergy:** all six Plan 2 companions work here — the role is "confidant" rather than skill provider. Companion relation gains are **the** primary reward.

Social endeavors are *about deepening one specific relationship*. The reward profile is narrow: a single hero's relation moves significantly (+10, +20, sometimes +30 for a multi-phase social arc). Rare side-rewards include `flag_set` unlocks for follow-up dialog branches with that hero (Plan 2 catalog hooks).

No scrutiny. Modest gold. Lord-relation effects only on `peer_officer_alliance` and `sway_lord` (the explicitly officer-tier templates).

Common option-shape:
- Honest-vulnerability option (large relation gain, sets a "you opened up" flag for downstream)
- Strategic-flattery option (moderate relation gain, no flag)
- Self-protective option (small or zero relation gain, +/- player_trust depending on companion archetype)

---

## 3. Hybrid gating — when to set the threshold

Per Plan 5 §4.2, every endeavor has a self-gate skill threshold AND optional companion slots. The **OR** between them is the gating philosophy: an experienced player should be able to do every category solo at high skill; a new player should be able to access every category by recruiting the matching companion.

| Threshold | Player skill needed | Read | Examples |
| :-- | :--: | :-- | :-- |
| 30 | Any half-trained character | "trivial gate — you've held a sword" | Soldier intro endeavors |
| 50 | A focused-build character | "you've put real time into this" | Most Medical / Scouting / Social standard |
| 60-70 | A specialist character | "you've made this your career path" | Standard Rogue + advanced Soldier |
| 80+ | A dedicated master | "you're better than the company expert" | Stretch endeavors with unique rewards |

**Rule of thumb:** if the matching companion archetype is on the spawn list (Plan 2) and the endeavor is a category-defining loop activity, use threshold `50` and let the companion slot exist as an alternative. If the endeavor is a stretch / officer-only / late-game piece, use `60-70` and make the companion slot **required** (`required: true`).

---

## 4. Scrutiny tuning — Rogue-only

Per [Lock 2](../../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system.md#-locked-2026-04-26--readiness-amendments-pre-execution) and §4.6, only Rogue endeavors have non-zero `scrutiny_risk_per_phase`. The number is the **base** discovery probability per phase before player-and-companion Charm contributions reduce it.

| `scrutiny_risk_per_phase` | Reads as | Applies to |
| :--: | :-- | :-- |
| `0.05` | "low risk — you're being careful" | `endeavor.rogue.dice_game` (camp-internal, low stakes) |
| `0.10` | "moderate risk — there's exposure" | `endeavor.rogue.smuggle_wine`, `endeavor.rogue.black_market` |
| `0.15` | "high risk — you're being seen" | `endeavor.rogue.pickpocket`, `endeavor.rogue.gambling_ring` |
| `0.20` | "very high risk — one slip and you're caught" | `endeavor.rogue.bribe_officer` (T5+ only — officer corruption is a hanging offense) |

**Per-phase scaling.** A 4-phase Rogue endeavor at `0.15` has cumulative ~50% chance of at least one discovery if no Charm contribution. Authoring should keep average campaign-impact survivable: a discovered phase typically adds `+5` scrutiny via `endeavor_scrutiny_drift_minor`, occasionally `+15` via `endeavor_scrutiny_drift_major` for the most brazen options.

**Mitigation arithmetic.** Per Plan 5 §4.6, effective risk = `baseRisk - 0.001 * (player.Charm + 0.5 * companionCharm)`. So Charm 60 player + Charm 60 Field Medic companion drops `0.15` to `0.06`. Don't write storylets that assume a particular reduction — the math is recomputed each fire.

---

## 5. Companion synergy patterns

| Archetype | Categories where they shine | Skill bonus axis / amount |
| :-- | :-- | :-- |
| `sergeant` | Soldier (mentor), Social (high-relation drill peer) | `leadership +20`, `one_handed +15` |
| `field_medic` | **Medical (50)**, Rogue (Charm cover, 30) | `medicine +50` (Medical), `charm +30` (Rogue) |
| `pathfinder` | Scouting (40) | `scouting +40` |
| `veteran` | Soldier (30), Rogue if high-Roguery roll (20-30) | `one_handed/two_handed +30` |
| `qm_officer` | Social (T7+), Soldier (Steward-flavored leadership) | `steward +25` |
| `junior_officer` | Social (T7+ peer), Soldier (T7+) | `leadership +25`, `tactics +15` |

**Role label conventions.** The `role` string surfaces in the assign-companion inquiry — write it as a short noun describing the companion's function in the endeavor:

- `mentor` — they teach you something
- `lookout` — they extend your senses
- `partner` — peer-level participation, splits credit
- `agent` — they do the work; you direct
- `confidant` — they listen; the work is interpersonal
- `cover` — they distract / charm so you can act

---

## 6. Phase pacing — what belongs where

Most endeavors should run **2-3 phases**. 4-phase templates are reserved for the largest stretch endeavors (`endeavor.rogue.gambling_ring`, `endeavor.social.peer_officer_alliance`, `endeavor.social.befriend_sergeant`).

| Phase | What the modal asks the player | Typical effects |
| :--: | :-- | :-- |
| **1** | "How are you starting? What's your initial approach?" | Sets up choice flag for phase 2 to flavor; small XP / quality drift; one `endeavor_set_score` ±1 |
| **2** | "Something has happened mid-arc. How do you respond?" | Reads phase-1 flag for tone/options; medium XP / quality drift; one `endeavor_set_score` ±1; first scrutiny roll for Rogue |
| **3** | (4-phase only) "The stakes have escalated. Commit or pull back?" | Reads accumulated state; `endeavor_set_score` ±1 to ±2; second scrutiny roll for Rogue |
| **resolution** | (separate storylet, not in `phase_pool`) "Here is what your choices produced." | Major payoffs: gold, multi-point relation drift, trait XP, milestone flag-sets, follow-up chain triggers |

**Symmetry rule.** Each phase should give the player **at least one option that doesn't punish them for previous picks**. A `failure` resolution should be reachable but not the inevitable consequence of a single early miss-step.

**Cooldown for category re-entry.** `EndeavorRunner` enforces a 3-day cooldown per category after a resolution fires (matches `DensitySettings.CategoryCooldownDays`). Don't author storylets assuming the player can spam the same category.

---

## 7. Resolution thresholds — when to use the default ±2

The default `success_min_score: +2`, `failure_max_score: -2` works for 2-3 phase endeavors where each phase's options offer ±1 score. The player has to make **mostly successful choices** to hit success; **mostly poor choices** to hit failure; mixed → partial.

Override the defaults when:

- **Resolution should be more forgiving** (Social endeavors building a slow bond): set `failure_max_score: -3` so a single bad pick mid-arc doesn't torpedo the whole thing.
- **Resolution should be sharper** (high-stakes Rogue): set `success_min_score: +3` so the player has to be consistently sharp; a single brash pick relegates them to partial.
- **Endeavor is binary by design** (`endeavor.rogue.bribe_officer` — you either pull it off or you're flogged): set `success_min_score: +1`, `failure_max_score: 0` so partial doesn't fire — every run resolves clean.

---

## 8. Token interpolation

Per AGENTS.md pitfall #23 + architecture brief §3, every authored storylet `setup` and `options[].text` should reference these tokens rather than hardcoding:

- `{PLAYER_NAME}` (the soldier's name)
- `{PLAYER_RANK}` (culture-aware — reads `progression_config.json`)
- `{LORD_NAME}` (the enlisted lord)
- For companion-assigned endeavors: `{COMPANION_NAME}` / `{COMPANION_FIRST_NAME}`

`EndeavorPhaseProvider` (T9 deliverable) populates these via `MBTextManager.SetTextVariable` before opening each phase modal — modeled on `EnlistedDialogManager.SetCommonDialogueVariables`. Companion-context variables come from the assigned-companion lookup.

---

## 9. Tag conventions (for filtering / debug)

`tags` is free-form — never gate from C# on tag values. Conventions:

- `t<N>_plus` — implies `tier_min: N` is set; redundant but useful for log skim
- `officer_only` — same as `tier_min: 7`
- `stretch_goal` — Plan 7 polish targets
- `requires_<archetype>` — implies `companion_slots[].required: true`
- `<category>_intro` — entry-level for that category
- `<category>_capstone` — late-game tone-setter for that category

---

## 10. Worked example — `endeavor.medical.tend_sick`

Walking the design through the schema:

```json
{
  "id": "endeavor.medical.tend_sick",
  "category": "medical",
  "title_id": "endeavor_medical_tend_sick_title",
  "title": "Tend a sick officer",
  "description_id": "endeavor_medical_tend_sick_desc",
  "description": "An officer is bedridden with fever. The Field Medic wants help; you decide how much.",
  "duration_days": 2,
  "phase_pool": [
    "endeavor_medical_tend_sick_phase1",
    "endeavor_medical_tend_sick_phase2"
  ],
  "resolution_storylets": {
    "success": "endeavor_medical_tend_sick_resolution_success",
    "partial": "endeavor_medical_tend_sick_resolution_partial",
    "failure": "endeavor_medical_tend_sick_resolution_failure"
  },
  "skill_axis": ["medicine", "steward"],
  "self_gate_skill_threshold": 40,
  "companion_slots": [
    {
      "archetype": "field_medic",
      "role": "mentor",
      "required": false,
      "skill_bonus_axis": "medicine",
      "skill_bonus_amount": 50
    }
  ],
  "scrutiny_risk_per_phase": 0.0,
  "tier_min": 1,
  "tags": ["medical_intro"]
}
```

**Why these numbers:**
- `duration_days: 2` — short, intimate. A sickbed isn't a campaign.
- `self_gate_skill_threshold: 40` — entry-level. New medics can take this.
- `field_medic` slot **not required** (the player can muddle through alone) but `skill_bonus_amount: 50` makes the Field Medic transformatively helpful.
- `scrutiny_risk_per_phase: 0.0` — Medical category is scrutiny-free per §4.1.
- 2-phase pacing — phase 1 "what's your bedside manner" + phase 2 "the fever broke at midnight, how do you respond" + resolution "the officer survives / barely / not."

---

## 11. References

- [Endeavor catalog schema](endeavor-catalog-schema.md) — what each field MEANS at the JSON level
- [Plan 5](../../superpowers/plans/2026-04-24-ck3-wanderer-endeavor-system.md) — locked design decisions §4.1-§4.8 + execution-shape locks
- [Writing style guide](../Content/writing-style-guide.md) — voice / tense / vocabulary
- [Companion archetype catalog](../Companions/companion-archetype-catalog.md) — Plan 2 archetypes, when they spawn, their existing dialog tones
- [Storylet backbone reference](../Content/storylet-backbone.md) — quality + flag conventions
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md) — namespace + offset contract
