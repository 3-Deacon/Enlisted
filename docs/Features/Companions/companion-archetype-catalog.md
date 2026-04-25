# Companion Archetype Catalog — Schema Reference

**File:** `ModuleData/Enlisted/Companions/archetype_catalog.json`

**Loader:** `Enlisted.Features.Companions.CompanionSpawnFactory`

**Owning plan:** [Plan 2 — Companion Substrate](../../superpowers/plans/2026-04-24-ck3-wanderer-companion-substrate.md) (CK3 wanderer mechanics cluster, plan 2 of 7).

**Status:** Living reference. Schema is `schemaVersion: 1`. Future bumps require coordinated migration of every `companionTypes[]` entry.

---

## 1. Purpose

The archetype catalog defines *what to spawn* when a companion-unlock trigger fires. `CompanionSpawnFactory.SpawnCompanion(companionTypeId, owningClan, bornSettlement, out archetype)` reads this file at session launch and uses one entry per companion type to drive `HeroCreator.CreateSpecialHero` and the post-spawn customization recipe (skills, attributes, traits, equipment, name).

The catalog covers the six wanderer-mechanics companions: `sergeant`, `field_medic`, `pathfinder`, `veteran`, `qm_officer`, `junior_officer`. Each companion type has a fixed base skill/attribute/trait set plus three personality archetypes; one archetype is rolled at spawn time and persists with the hero (stored as a string field on `EnlistmentBehavior`).

This file is **not** for Plan 6's patron troop loans, the existing quartermaster (`Hero.GetOrCreateQuartermaster` in `EnlistmentBehavior`), or any other temporary or pre-existing companion. Plan 2's six archetypes only.

---

## 2. Top-level schema

```json
{
  "schemaVersion": 1,
  "companionTypes": [
    { "id": "sergeant", "...": "..." },
    { "id": "field_medic", "...": "..." },
    { "id": "pathfinder", "...": "..." },
    { "id": "veteran", "...": "..." },
    { "id": "qm_officer", "...": "..." },
    { "id": "junior_officer", "...": "..." }
  ]
}
```

| Field | Type | Required | Notes |
| :-- | :-- | :--: | :-- |
| `schemaVersion` | int | yes | `1`. Loader rejects other values with `ModLogger.Surfaced("COMPANION", "schema_version_mismatch", ...)`. |
| `companionTypes` | array | yes | One entry per companion type. Loader keys by `id`; duplicate ids fail validation. |

---

## 3. Per `companionType` schema

```json
{
  "id": "sergeant",
  "displayName": "Sergeant",
  "lifecycle": "per_player",
  "unlockTier": 1,
  "ageRange": [35, 55],
  "baseSkills": {
    "OneHanded": 80,
    "Leadership": 60,
    "Tactics": 40
  },
  "baseAttributes": {
    "Vigor": 1
  },
  "baseTraits": {
    "Valor": 1
  },
  "troopTemplate": {
    "tierMin": 3,
    "tierMax": 4,
    "formationClass": "Infantry"
  },
  "civilianStyle": "soldier",
  "archetypes": [
    {
      "id": "gruff_veteran",
      "extraTraits": { "Honor": 1 },
      "namePool": ["Garreth", "Ulric", "Brennus", "Hakon"],
      "dialogCatalogPrefix": "companion_sergeant_gruff_veteran"
    },
    {
      "id": "by_the_book",
      "extraTraits": { "Calculating": 1 },
      "namePool": ["Marcus", "Aldric", "Rolf", "Tarek"],
      "dialogCatalogPrefix": "companion_sergeant_by_the_book"
    },
    {
      "id": "cynical",
      "extraTraits": { "Mercy": 1 },
      "namePool": ["Davos", "Lyle", "Ferran", "Raol"],
      "dialogCatalogPrefix": "companion_sergeant_cynical"
    }
  ]
}
```

### 3.1 Required fields

| Field | Type | Notes |
| :-- | :-- | :-- |
| `id` | string | Stable identifier. C# code references this in `GetOrCreate<Companion>()` calls and `CompanionLifecycleHandler.GetSpawnedCompanions`. Must be unique within the file. Snake_case, lowercase, ASCII. Allowed: `[a-z][a-z0-9_]*`. |
| `displayName` | string | Surfaced in talk-to inquiry list and log lines. Not localized at this layer (use a `{=key}` token in player-facing strings if needed downstream). |
| `lifecycle` | enum string | `per_player` or `per_lord`. `per_player` companions persist across lord switches; `per_lord` companions are released via `RemoveCompanionAction.ApplyByFire` on discharge. **All six companions live in `Clan.PlayerClan` regardless of lifecycle.** Per plan §4.5 lock decision: lifecycle controls *when the hero is released*, not *which clan owns them*. PlayerClan is required for vanilla `CompanionRolesCampaignBehavior` "About your position in the clan…" dialog to fire. |
| `unlockTier` | int 1-9 | Tier at which `GetOrCreate<Companion>()` first spawns the hero. Sergeant = 1 (T1 enlistment), Field Medic + Pathfinder = 3, Veteran = 5, QM Officer + Junior Officer = 7. |
| `ageRange` | `[int, int]` | `[min, max]` inclusive. Passed to `HeroCreator.CreateSpecialHero(template, settlement, clan, supporter, age)` via `MBRandom.RandomInt(min, max)`. Sergeant uses `[35, 55]` (experienced NCO); Junior Officer uses `[28, 42]` (younger peer); Veteran uses `[45, 65]`. |
| `baseSkills` | dict<string, int> | Skill StringId → exact level. Applied via `HeroDeveloper.SetInitialSkillLevel(skill, value)`. Keys must match `DefaultSkills.<Name>.StringId`; the loader validates against `Skills.All` at session launch and fails fast on unknown skills. See §4.1. |
| `baseAttributes` | dict<string, int> | Attribute StringId → bonus added to base. Applied via `HeroDeveloper.AddAttribute(attr, value, checkUnspentPoints: false)`. Valid keys: `Vigor`, `Control`, `Endurance`, `Cunning`, `Social`, `Intelligence`. |
| `baseTraits` | dict<string, int> | Trait StringId → exact value. Applied via `Hero.SetTraitLevel(trait, value)`. Valid keys: `Valor`, `Mercy`, `Honor`, `Calculating`, `Generosity`. The five `DefaultTraits` constants (excluding the meta-trait family). |
| `troopTemplate` | object | Predicate that drives runtime troop-template selection. See §3.3. Mirrors `EnlistmentBehavior.GetSergeantTierTroopTemplate` precedent — predicate-based to survive game patches and modded factions, not a hard-coded StringId map. |
| `civilianStyle` | enum string | `soldier` (use the troop template's default civilian equipment) or `wealthy` (overlay civilian equipment from a culture-matched `Merchant` / `Artisan` / `Preacher` / `Gangster` template, mirroring `EnlistmentBehavior.TryApplyQuartermasterWealthyCulturalAttire`). Plan 2 ships only these two styles; future plans add `peasant`, `noble`, etc. as needed. |
| `archetypes` | array | Exactly three entries. One is rolled at spawn time. See §3.2. |

### 3.3 `troopTemplate` predicate

Spawn-time selection runs `CharacterObject.All.Where(...)` against the lord's culture filtered by these fields, fall-back chain identical to `GetSergeantTierTroopTemplate`:

| Field | Type | Notes |
| :-- | :-- | :-- |
| `tierMin` | int | Inclusive lower bound on `CharacterObject.Tier`. |
| `tierMax` | int | Inclusive upper bound on `CharacterObject.Tier`. Must be ≥ `tierMin`. |
| `formationClass` | enum string | `Infantry`, `Ranged`, `Cavalry`, `HorseArcher`, or `Any`. Maps to `FormationClass` at runtime. `Any` skips the formation-class filter. |
| `roll` | object (optional) | Per-spawn random sub-selection (used by Veteran's `Bow 80 OR TwoHanded 80` choice). When present, the factory picks one entry uniformly at random from `roll.options[]` and overlays its skills + formation onto the base. See §3.4. |

Fallback chain on empty result: drop the formation filter, then drop the tier filter, then `culture.BasicTroop` as a last resort. All three fallback steps log `ModLogger.Expected("COMPANION", "template_fallback_<level>", …)`.

### 3.4 `troopTemplate.roll` (used by Veteran)

```json
"troopTemplate": {
  "tierMin": 4,
  "tierMax": 5,
  "formationClass": "Any",
  "roll": {
    "options": [
      { "skillOverride": { "Bow": 80 }, "formationClass": "Ranged" },
      { "skillOverride": { "TwoHanded": 80 }, "formationClass": "Infantry" }
    ]
  }
}
```

Each `roll.options[]` entry has:
- `skillOverride` — dict<string, int> applied **after** `baseSkills` (so the override wins on collisions)
- `formationClass` — overrides the parent `formationClass` when this option is rolled

The factory picks one option uniformly at random per spawn. Plan 2 only uses `roll` on the Veteran. Other companions omit the field entirely.

### 3.5 Per `archetype` schema

| Field | Type | Notes |
| :-- | :-- | :-- |
| `id` | string | Snake_case. Stored on `EnlistmentBehavior._<companion>Archetype` as the rolled value. Used by `CompanionDialogueCatalog` for archetype-conditional node selection. Each id is unique within its companion type's `archetypes` array. |
| `extraTraits` | dict<string, int> | Trait deltas added on top of `baseTraits`. Schema same as `baseTraits`. Per spec §4.3: each archetype contributes one extra trait that flavors personality (e.g. `gruff_veteran` adds `Honor +1`, `cynical` adds `Mercy +1`). |
| `namePool` | string array | First names sampled at random by `CompanionSpawnFactory`. Family names come from the troop-template culture's name lists via vanilla generation. Names are passed through `Hero.SetName(new TextObject(name), new TextObject(name))`. |
| `dialogCatalogPrefix` | string | File-name prefix used by `CompanionDialogueCatalog` to scope dialog content to this archetype. Convention: `companion_<companionTypeId>_<archetypeId>`. Files matching `<prefix>_*.json` in `ModuleData/Enlisted/Dialogue/` are loaded with `dialogueType: "companion"` and the catalog filters on `context.archetype` at runtime. |

---

## 4. Reference data

### 4.1 Valid skill StringIds

Match `DefaultSkills.<Name>.StringId` (camelCase, no spaces):

`OneHanded`, `TwoHanded`, `Polearm`, `Bow`, `Crossbow`, `Throwing`, `Riding`, `Athletics`, `Smithing`, `Scouting`, `Tactics`, `Roguery`, `Charm`, `Leadership`, `Trade`, `Steward`, `Medicine`, `Engineering`.

The loader validates skill keys against `Skills.All` at session launch. Unknown keys fail fast.

### 4.2 Valid attribute StringIds

`Vigor`, `Control`, `Endurance`, `Cunning`, `Social`, `Intelligence`.

These match `CharacterAttribute.<Name>.StringId` and the `AttributeKind` enum.

### 4.3 Valid trait StringIds

`Valor`, `Mercy`, `Honor`, `Calculating`, `Generosity`.

These are the five `DefaultTraits` accessible via `DefaultTraits.<Name>` and gate vanilla `CompanionGrievanceBehavior` complaints. Plan 2 deliberately limits trait setting to this list — anything else (`Egalitarian`, etc.) is out of scope.

### 4.4 Valid `formationClass` values

`Infantry`, `Ranged`, `Cavalry`, `HorseArcher`, `Any`. Map to the `FormationClass` enum at runtime. `Any` skips the formation filter (used by Veteran's `roll`-based selection where formation is decided per option, and by officer-tier companions where infantry/cavalry are both acceptable).

### 4.5 Locked spawn parameters per companion (plan §4.2 + §4.3)

| Companion | Lifecycle | Unlock | Base skills | Base attributes | Base traits | Archetypes (extra trait) | Troop predicate | Civilian style |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| `sergeant` | per_player | T1 | OneHanded 80, Leadership 60, Tactics 40 | Vigor +1 | Valor +1 | `gruff_veteran` (Honor) / `by_the_book` (Calculating) / `cynical` (Mercy) | T3-4 Infantry | soldier |
| `field_medic` | per_player | T3 | Medicine 100, Steward 60, Athletics 30 | Endurance +1 | Mercy +1 | `compassionate` (Mercy) / `pragmatic` (Calculating) / `pious` (Honor) | T2-3 Infantry | wealthy |
| `pathfinder` | per_player | T3 | Scouting 80, Riding 60, Bow 40 | Cunning +1 | Calculating +1 | `lone_wolf` (Calculating) / `talkative` (Generosity) / `superstitious` (Mercy) | T3-4 HorseArcher | soldier |
| `veteran` | per_lord | T5 | Bow 80 OR TwoHanded 80, Tactics 60 | Endurance +1 | Honor +1, Valor +1 | `war_weary` (Mercy) / `proud` (Valor) / `philosophical` (Honor) | T4-5 (rolled: Ranged or Infantry) | soldier |
| `qm_officer` | per_lord | T7 | Steward 80, Trade 60, Charm 40 | Social +1 | Generosity +1 | `by_the_book` (Honor) / `corner_cutter` (Calculating) / `weary` (Mercy) | T4-5 Infantry | wealthy |
| `junior_officer` | per_lord | T7 | Leadership 80, Tactics 60, Polearm 60 | Vigor +1 | Honor +1 | `cocky` (Valor) / `serious` (Honor) / `political` (Calculating) | T5-6 Cavalry | soldier |

**Veteran note.** The `Bow 80 OR TwoHanded 80` choice is per-spawn random (50/50) via `troopTemplate.roll.options[]` — see §3.4. The unselected skill defaults to the troop template baseline.

**Field Medic + QM Officer note.** These two are non-combatant-styled but ship with combat-capable battle equipment (so they don't die instantly when forced into battle). `civilianStyle: wealthy` overlays a culture-matched merchant/artisan/preacher civilian outfit on top of the battle-equipment baseline, mirroring the QM's `TryApplyQuartermasterWealthyCulturalAttire` precedent.

---

## 5. Loader behavior

### 5.1 Load order

1. Session launch → `CompanionSpawnFactory.LoadCatalog()` reads the JSON.
2. Validates `schemaVersion == 1`, exactly six `companionTypes` entries with the expected ids, each with exactly three `archetypes`.
3. Validates skill / attribute / trait / culture keys against the runtime tables (`Skills.All`, `Attributes`, `DefaultTraits`, `Cultures.All`).
4. On any validation failure: `ModLogger.Surfaced("COMPANION", "<reason>", ex, ctx)` and the factory returns `null` from every subsequent `SpawnCompanion(...)` call (graceful degradation — companion-related features stay silent rather than NRE).

### 5.2 Spawn-time roll

Per spawn:
1. `CompanionSpawnFactory.SpawnCompanion(typeId, owningClan, settlement, out archetypeId)` looks up the entry by `typeId`.
2. Picks one entry from `archetypes[]` uniformly at random, returns the `id` via `out archetypeId`.
3. Picks one name from that archetype's `namePool` uniformly at random.
4. Picks the troop template by running the §3.3 predicate against `CharacterObject.All` filtered to `settlement.Culture` (with the documented fallback chain). When `troopTemplate.roll` is present, also rolls one option from `roll.options[]` and merges its `skillOverride` + `formationClass`.
5. Calls `HeroCreator.CreateSpecialHero(template, settlement, owningClan, null, age)` with `age = MBRandom.RandomInt(ageRange[0], ageRange[1])`.
6. Applies skill / attribute / trait sets from `baseSkills + baseAttributes + baseTraits + archetype.extraTraits`.
7. Sets occupation to `Soldier`, name from the rolled pool, `HiddenInEncyclopedia = true`, `IsKnownToPlayer = true`.
8. Applies equipment by overlaying the troop-template loadout (battle equipment from template, civilian equipment via the same fallback chain `EnlistmentBehavior.TryApplyQuartermasterWealthyCulturalAttire` uses).
9. `AddCompanionAction.Apply(owningClan, hero)` plus `MobileParty.MainParty.MemberRoster.AddToCounts(hero.CharacterObject, 1, isHero: true)`.

### 5.3 Re-spawn vs persistence

| Lifecycle | Trigger | Outcome |
| :-- | :-- | :-- |
| `per_player` | First time the unlock condition holds AND the corresponding `_<companion>Hero` field is null OR `IsDead` | Spawn into PlayerClan, MainParty roster. **Death is permanent for per-player heroes** — `_<companion>Hero` is nulled in `OnHeroKilled`, but the spawn factory does not re-fire the trigger (player permanently loses that companion). |
| `per_lord` | Spawn fires per-discharge, conditioned on the lord-switch + tier still ≥ unlock | `RemoveCompanionAction.ApplyByFire(playerClan, hero)` releases the hero on discharge; `_<companion>Hero` cleared. Next enlistment re-rolls a fresh archetype + name pool entry. **Death is permanent for the current lord's enlistment** but does not block re-spawn on the next lord. |

---

## 6. Pitfalls

1. **Trait keys are the five `DefaultTraits` only.** Authoring `"Egalitarian": 1` (a meta-trait) silently mis-applies. Loader validates against the five-trait whitelist.
2. **Skill values are exact, not deltas.** `"OneHanded": 80` sets the skill to 80 regardless of the troop template's baseline. Use `0` to clear a template skill (rare).
3. **Attribute deltas are bounded by `Hero` cap.** Vanilla `AddAttribute` clamps to the attribute cap; setting `Vigor +5` on a hero with `Vigor 5` already is a no-op. Stay at +1 for archetypes per spec §4.2.
4. **`archetypes[]` MUST have exactly three entries.** Plan 2's dialog content authors three archetype variants per companion. Adding a fourth would orphan dialog files; removing one would orphan the third archetype's authoring. Loader fails on any other count.
5. **The `troopTemplate` predicate must produce a non-empty result for every vanilla culture.** Spawn into a culture where the predicate yields nothing falls through the formation → tier → `BasicTroop` fallback chain and may produce a culture-mismatched or generic hero. Validate at session launch by running the predicate against each culture in `Cultures.All` and surfacing a warning per culture-companion pair that hits the `BasicTroop` last resort.
6. **`namePool` collisions across archetypes are allowed but not recommended.** Two archetypes both listing `"Garreth"` is legal — each spawn rolls independently. But it dilutes the "you can tell archetypes apart by their feel" effect.
7. **Equipment assignment runs after `HeroCreator.CreateSpecialHero` overlays the template's default equipment.** Archetype catalog does NOT define equipment slots directly; equipment comes from the troop template + `TryApplyQuartermasterWealthyCulturalAttire`-style culture-appropriate civilian overlay. Plan 2 does not author item-level overrides.

---

## 7. References

- [Plan 2 — Companion Substrate](../../superpowers/plans/2026-04-24-ck3-wanderer-companion-substrate.md) — owning plan
- [Spec v6 §3.10](../../superpowers/specs/2026-04-24-ck3-wanderer-systems-analysis.md) — design source for the six-companion roster
- [Architecture brief §4](../../architecture/ck3-wanderer-architecture-brief.md) — schema rules and dialog token prefixes
- `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9815-9927` — `GetOrCreateQuartermaster` / `CreateQuartermasterForLord`, the canonical companion-spawn precedent
- `src/Features/Conversations/Data/QMDialogueCatalog.cs` — JSON dialog schema precedent
- `Decompile/TaleWorlds.CampaignSystem/HeroCreator.cs:189-210` — `CreateSpecialHero` signature
- `Decompile/TaleWorlds.CampaignSystem/HeroDeveloper.cs:181-187` — `SetInitialSkillLevel`
- `Decompile/TaleWorlds.CampaignSystem/HeroDeveloper.cs:346-362` — `AddAttribute`
- `Decompile/TaleWorlds.CampaignSystem/Hero.cs:1397-1401` — `SetTraitLevel`
- `Decompile/TaleWorlds.CampaignSystem/Hero.cs:1263-1268` — `SetNewOccupation` (must be `Occupation.Soldier`)
