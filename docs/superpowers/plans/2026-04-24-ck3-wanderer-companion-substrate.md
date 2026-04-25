# Plan 2 — CK3 Wanderer Mechanics: Companion Substrate

**Status:** Draft v1 (2026-04-24). Second of seven plans implementing the [CK3 Wanderer Mechanics Systems Analysis (v6)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md). See spec §8 for the full plan structure.

**Scope:** Spawn six companion archetypes that travel with the player through the army, contribute skill aptitude bonuses via vanilla `MobileParty.EffectiveX` getters, fight in the lord's battles, can be killed in combat, can be withheld from combat, and surface in dialog via vanilla `CompanionRolesCampaignBehavior` plus mod-authored archetype-flavored JSON catalogs. **NO ceremonies, NO endeavors, NO officer-trajectory equipment** — those are Plans 3, 4, 5.

**Estimated tasks:** 25. **Estimated effort:** 4-5 days with AI-driven implementation.

**Dependencies:** Plan 1 (Architecture Foundation) must be complete and its verification report shipped before Plan 2 begins.

---

## §0 — Read these first (mandatory orientation for fresh agent chats)

This plan is self-contained but assumes the executor has read these files in order before touching any code:

### Required prior plan documentation

1. **[Plan 1 — Architecture Foundation](2026-04-24-ck3-wanderer-architecture-foundation.md)** — the foundation. Pay special attention to Plan 1 §4 (locked architecture decisions) and Plan 1 §10 (hand-off to Plan 2).
2. **[Plan 1 verification report](2026-04-24-ck3-wanderer-architecture-foundation-verification.md)** — must be ✅ complete before starting Plan 2. If missing or showing failures, halt.
3. **[Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)** — committed by Plan 1 T1. Locked save-offset ledger, namespace conventions, dialog token prefixes, schema rules, "do not" list, modal pipeline recipe.

### Required spec reading

4. **[Spec v6 §3.10 Companion Substrate](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — design source for the six-companion roster, skills, traits, role mappings.
5. **[Spec v6 §6.5 Companion combat lifecycle](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — load-bearing finding: companions in MainParty fight in the lord's battles via `MapEventSide`. Withhold-from-combat enforcement, death model, skill aptitude flow.
6. **[Spec v6 §6.6 Vanilla companion preferences](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — `CompanionGrievanceBehavior` auto-fires trait-gated complaints (Valor+ → retreat, Mercy+ → raiding, Generosity+ → starvation/wages); `DeclareWarDecision` voting weights.
7. **[Spec v6 §6.9 Mod's existing menu + dialog wiring](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — existing Camp hub structure; **slot 6 is free** for the new Talk-to sub-menu.
8. **[Spec v6 §9 Canonical rank system reference](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — the 9-tier ladder. Companion unlocks gate on tier (T1 / T3 / T5 / T7).

### Required project guidance

9. **[AGENTS.md](../../../AGENTS.md)** — universal critical rules. Especially: §1 (verify TaleWorlds APIs against decompile), §2 (csproj registration for new .cs files), §8 (save system registration), §9 (Windows/WSL portability), Pitfall #11 (mod-spawned heroes need `Occupation.Soldier`, not `Wanderer`).
10. **[CLAUDE.md](../../../CLAUDE.md)** — Claude-specific guidance. Especially: known footguns 4 (deserialization skips ctor), 11 (Occupation.Soldier), 14 (HashSet not saveable), 16 (vanilla type re-registration), 17 (Campaign.Current at OnGameStart). Each referenced inline below.

### Required existing-code orientation (read before touching the file)

11. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9748-9805` `SetTier`** — fires `OnTierChanged` event; Plan 2's tier-gated companion spawns hook here.
12. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9815-9927` `GetOrCreateQuartermaster` + `CreateQuartermasterForLord`** — **the canonical companion-spawn precedent**. Plan 2 mirrors this pattern verbatim for all six new archetypes. Read this end-to-end before T3.
13. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:1332-1336`** — QM SyncData pattern (`_quartermasterHero` MBGUID + `_quartermasterArchetype` string + relationship int + bool flag). Plan 2 adds 24 parallel fields (4 per companion × 6 companions) following this exact shape.
14. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:11223-11248` `AssignInitialEquipment`** — equipment assignment precedent. Plan 2 reuses for spawn-time equipment override.
15. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:1142-1164`** — the MapEventSide assignment that puts MainParty into the lord's battles. **Verify Plan 1's stay-back gate fix is shipped** by reading `EnlistedFormationAssignmentBehavior.cs:188-200` before T22.
16. **`src/Features/Retinue/Core/CompanionAssignmentManager.cs`** — Fight/Stay-Back toggle. Plan 1 T8 added `IsAssignedToEndeavor` parallel field. Plan 2 verifies these work for newly spawned companions.
17. **`src/Features/Conversations/Data/QMDialogueCatalog.cs`** — JSON dialog schema precedent. Plan 2's 6 companion catalogs mirror this schema exactly.
18. **`src/Features/Conversations/Data/CompanionDialogueCatalog.cs`** — created by Plan 1 T7 with stub `companion_test.json`. Plan 2 extends with all 6 archetype catalogs.
19. **`src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3946-4020` `OnTalkToSelected`** — talk-to-lord precedent. Plan 2's Camp slot 6 "Talk to companion" mirrors the `MultiSelectionInquiryData` pattern from `ShowLordSelectionInquiry`.
20. **`src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:1345-1482` `RegisterCampHubMenu`** — Camp menu registration. Plan 2 adds slot 6 here.
21. **`ModuleData/Enlisted/Dialogue/qm_*.json`** — QM dialog content as authoring reference. Plan 2's companion catalogs should match this style for context conditions, options, action verbs.

### Required decompile orientation

22. **`Decompile/TaleWorlds.CampaignSystem/HeroCreator.cs:189-210` `CreateSpecialHero`** — primary spawn API. Read full signature before T3.
23. **`Decompile/TaleWorlds.CampaignSystem/HeroDeveloper.cs:181-187` `SetInitialSkillLevel`** — clean exact-level skill set with proper XP threshold.
24. **`Decompile/TaleWorlds.CampaignSystem/HeroDeveloper.cs:346-362` `AddAttribute`** — pass `checkUnspentPoints: false` for post-spawn attribute set.
25. **`Decompile/TaleWorlds.CampaignSystem/Hero.cs:1397-1401` `SetTraitLevel`** — direct trait setter, clamps to min/max.
26. **`Decompile/TaleWorlds.CampaignSystem/Hero.cs:1233-1241` `SetName`** — TextObject-based naming.
27. **`Decompile/TaleWorlds.CampaignSystem/Hero.cs:1263-1268` `SetNewOccupation`** — must pass `Occupation.Soldier` per CLAUDE.md issue #11.
28. **`Decompile/TaleWorlds.Core/EquipmentElement.cs`** + **`Equipment.cs`** — equipment slot manipulation.
29. **`Decompile/TaleWorlds.CampaignSystem.Actions/AddCompanionAction.cs:5-18` `Apply`** — clan-attachment action.
30. **`Decompile/TaleWorlds.CampaignSystem.Actions/RemoveCompanionAction.cs:59-62` `ApplyByFire`** — clean cleanup; transitions hero to Fugitive via `MakeHeroFugitiveAction`.
31. **`Decompile/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionRolesCampaignBehavior.cs:71-142`** — vanilla "About your position in the clan..." dialog. Plan 2 verifies this fires for spawned companions; **no new dialog registration needed for role assignment**.
32. **`Decompile/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionGrievanceBehavior.cs:156-179, 375-382`** — vanilla trait-gated grievances. Plan 2 verifies these fire correctly given Plan 2's spawn-time trait set.
33. **`Decompile/TaleWorlds.CampaignSystem.Party/MobileParty.cs:777-823`** — `EffectiveSurgeon/Scout/Quartermaster/Engineer` getters. Verify role-claim flows correctly when player assigns Field Medic as Surgeon via vanilla dialog.
34. **`Decompile/TaleWorlds.CampaignSystem.GameComponents/DefaultPartyHealingModel.cs:45-49` `GetSurgeryChance`** — vanilla skill-aptitude precedent (reads `EffectiveSurgeon.GetSkillValue(Medicine)`). Plan 2's Field Medic with Medicine 100 should drive this.

---

## §1 — What this plan delivers

After Plan 2 ships, the codebase is in a state where:

- **Six companion archetypes exist in the player's clan** at the right tier unlocks: Sergeant (T1 enlistment), Field Medic + Pathfinder (T3 unlock), Veteran (T5 unlock), QM Officer + Junior Officer (T7 promotion).
- **Per-player vs per-Lord lifecycle is wired:** Sergeant + Field Medic + Pathfinder follow the player across enlistments; Veteran + QM Officer + Junior Officer are released on discharge and re-spawned with the new lord.
- **Companions live in `MobileParty.MainParty.MemberRoster`**, fight in the lord's battles via the existing `MapEventSide` mechanism, can be killed by vanilla death model, and are withheld from combat via the existing (Plan 1 T6 fixed) `CompanionAssignmentManager.ShouldCompanionFight` toggle at all tiers.
- **Companion skill aptitude flows to the player's MainParty** via `MobileParty.EffectiveSurgeon` (Field Medic), `EffectiveScout` (Pathfinder), `EffectiveQuartermaster` (QM Officer), `EffectiveEngineer` (no companion mapped — vanilla fallback). The player assigns roles via vanilla `CompanionRolesCampaignBehavior` "About your position in the clan..." dialog; **no new role-assignment code is needed**.
- **Companion trait-gated grievances auto-fire from vanilla `CompanionGrievanceBehavior`** based on the trait sets baked at spawn (Sergeant Valor+1 complains about retreats; Field Medic Mercy+1 complains about village raids; QM Officer Generosity+1 complains about unpaid wages and starvation).
- **A "Talk to..." sub-menu in Camp slot 6** lists currently-spawned companions and opens a conversation when one is selected, surfacing both vanilla dialog branches (role assignment, opinion drift, fire) and mod-authored archetype-flavored JSON catalogs (~25 nodes per companion type).
- **Each companion rolls one of 3 personality archetypes at spawn**, baked into the trait set + dialog catalog context for archetype-flavored variation.

**Player-visible deltas:**
- T1 enlistment now spawns Sergeant Garreth (or whatever the rolled name + culture provides) into the player's party. Player can talk to him in camp.
- T3 promotion spawns Field Medic + Pathfinder.
- T5 promotion spawns Veteran (per-Lord — replaced if player switches).
- T7 promotion spawns QM Officer + Junior Officer (per-Lord).
- Companions appear in battle on the player's side; can die; can be told to stay back via the existing toggle.
- Companions complain when triggered: Sergeant disapproves of retreats, Field Medic disapproves of cruelty, QM Officer disapproves of unpaid wages.

**No content surface beyond the above.** No ceremonies (Plan 3), no officer equipment (Plan 4), no endeavors (Plan 5), no patron favors (Plan 6), no kit/lifestyle (Plan 7).

---

## §2 — Subsystems explored (audits that informed this plan)

| Audit topic | Key finding informing Plan 2 | Spec section |
| :-- | :-- | :-- |
| Hero spawning lifecycle | `HeroCreator.CreateSpecialHero(template, settlement, clan, supporter, age)` works for any occupation; QM precedent in `EnlistmentBehavior.GetOrCreateQuartermaster` is the canonical recipe to mirror | §6.5 |
| Spawn customization | Post-spawn `HeroDeveloper.SetInitialSkillLevel`, `AddAttribute(checkUnspentPoints: false)`, `Hero.SetTraitLevel`, `Hero.SetName`, equipment slot assignment all work cleanly | §6.5 |
| Combat participation | Companions in MainParty fight in lord's battles via `MapEventSide` — verified at `EnlistmentBehavior.cs:1142-1164` (existing mod code, no new combat plumbing required) | §6.5 |
| Withhold-from-combat | `CompanionAssignmentManager.ShouldCompanionFight` enforced at agent-spawn by `EnlistedFormationAssignmentBehavior.TryRemoveStayBackCompanion()`. Plan 1 T6 removed the T7+ gate; now applies at all tiers | §6.5 |
| Skill aptitude flow | `MobileParty.EffectiveX` getters work on hidden+active MainParty; `DefaultPartyHealingModel.GetSurgeryChance` reads MainParty.EffectiveSurgeon's Medicine; bonuses reach the player while attached to the lord | §6.5 |
| Vanilla preferences | `CompanionGrievanceBehavior` auto-fires complaints (Valor+/Mercy+/Generosity+ with corresponding triggers); persona traits modify dialog flavor only; relation < -10 auto-removes companion | §6.6 |
| Vanilla role assignment | `CompanionRolesCampaignBehavior` provides "About your position in the clan..." dialog for any clan-companion in MainParty — no new dialog registration needed for Surgeon/Scout/Quartermaster/Engineer assignment | §6.4 / §6.6 |
| Companion lifecycle | `AddCompanionAction.Apply(playerClan, hero)` + `RemoveCompanionAction.ApplyByFire(playerClan, hero)` give clean attach/detach with `MakeHeroFugitiveAction` cleanup | §6.5 |
| Companion death | Vanilla `DefaultDeathProbabilityModel` + battle wounds → `KillCharacterAction.DiedInBattle`; survival rate ~95% young+armored, ~40% old+underarmored; tunable via `CampaignOptions.BattleDeath` | §6.5 |
| QM precedent (existing) | `EnlistmentBehavior.GetOrCreateQuartermaster` + `CreateQuartermasterForLord` pattern uses lazy creation, sergeant-tier troop template, lord's-clan affiliation, settlement parking, archetype-rolling. **Plan 2 mirrors this for combat-participating companions** (key difference: Plan 2 companions go into MainParty.MemberRoster, not parked in settlement) | §3.10 |

---

## §3 — Subsystems Plan 2 touches

### Files modified (existing)

| File | Change | Tasks |
| :-- | :-- | :-- |
| `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` | Add 24 SyncData fields (4 per companion × 6 companions: hero MBGUID, archetype string, relationship int, has-met bool); add `GetOrCreate<Companion>()` methods; hook spawn triggers on `IsEnlisted` + `OnTierChanged` + discharge; `OnHeroKilled` handler for companion death cleanup | T4-T11 |
| `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` | Add Camp menu slot 6 "Talk to..." sub-option; `OnTalkToCompanionSelected` callback shows `MultiSelectionInquiryData`; opens conversation with selected companion via `CampaignMapConversation.OpenConversation` | T19-T20 |
| `src/Features/Conversations/Data/CompanionDialogueCatalog.cs` (created in Plan 1 T7) | Extend to load all 6 archetype catalogs (sergeant, field_medic, pathfinder, veteran, qm_officer, junior_officer) | T18 |
| `Tools/Validation/validate_content.py` | Phase 18 (companion JSON schema validation, stub from Plan 1 T14) populated with real validation logic | T18 |
| `Enlisted.csproj` | `<Compile Include>` for new `.cs` files; existing `<CompanionDialogData Include>` from Plan 1 T7 covers the new catalogs | T2-T9 |

### Files created (new)

| File | Purpose | Tasks |
| :-- | :-- | :-- |
| `docs/Features/Companions/companion-archetype-catalog.md` | Schema reference for the archetype data file | T1 |
| `ModuleData/Enlisted/Companions/archetype_catalog.json` | Defines spawn parameters per archetype (name pools, skill values, attribute bonuses, traits, equipment templates, dialog catalog references) | T2 |
| `src/Features/Companions/CompanionSpawnFactory.cs` | Utility class implementing the spawn recipe (HeroCreator + post-spawn customization). Used by all 6 spawn methods. | T3 |
| `src/Features/Companions/CompanionLifecycleHandler.cs` | Campaign behavior hooking spawn triggers + discharge cleanup + `OnHeroKilled` handler | T11 |
| `ModuleData/Enlisted/Dialogue/companion_sergeant_*.json` | Sergeant archetype catalog (~25 nodes) | T12 |
| `ModuleData/Enlisted/Dialogue/companion_field_medic_*.json` | Field Medic archetype catalog (~25 nodes) | T13 |
| `ModuleData/Enlisted/Dialogue/companion_pathfinder_*.json` | Pathfinder archetype catalog (~25 nodes) | T14 |
| `ModuleData/Enlisted/Dialogue/companion_veteran_*.json` | Veteran archetype catalog (~25 nodes) | T15 |
| `ModuleData/Enlisted/Dialogue/companion_qm_officer_*.json` | QM Officer archetype catalog (~25 nodes) | T16 |
| `ModuleData/Enlisted/Dialogue/companion_junior_officer_*.json` | Junior Officer archetype catalog (~25 nodes) | T17 |
| `Enlisted.csproj` ItemGroup additions | `<CompanionsData Include="ModuleData\Enlisted\Companions\*.json"/>` + MakeDir + Copy in AfterBuild target | T2 |
| `docs/superpowers/plans/2026-04-24-ck3-wanderer-companion-substrate-verification.md` | Plan 2 verification report | T25 |

### Subsystems Plan 2 does NOT touch

- Combat plumbing (Plan 1 T6 already shipped the stay-back gate fix; existing `MapEventSide` flow already works)
- Officer equipment registrations (Plan 4)
- Endeavor companion-agent locking (`IsAssignedToEndeavor` was scaffolded in Plan 1 T8; Plan 5 populates)
- Ceremony storylets featuring companion witnesses (Plan 3)
- Patron favor outcomes including troop-loan companions (Plan 6 — those are temporary borrowed-knight heroes, separate from Plan 2's persistent archetypes)

---

## §4 — Locked design decisions (Plan 2 commits, Plans 3-7 inherit)

### §4.1 Per-player vs per-Lord lifecycle (LOCKED)

| Companion | Lifecycle | Rationale |
| :-- | :-- | :-- |
| Sergeant | **per-player** | Your personal minder, follows you across lord switches. Emotional continuity. |
| Field Medic | **per-player** | Your healer; relationship matters across enlistments. |
| Pathfinder | **per-player** | Your scout; same continuity argument. |
| Veteran | **per-Lord** | The army's old hand. Different lord = different veteran. |
| QM Officer | **per-Lord** | Officer of the lord's quartermaster corps. |
| Junior Officer | **per-Lord** | Lord's peer; tied to the army. |

**Discharge handling:**
- On `EnlistmentBehavior.IsEnlisted` flips false:
   - Per-player companions remain in PlayerClan + MainParty (no action).
   - Per-Lord companions: `RemoveCompanionAction.ApplyByFire(playerClan, hero)` releases each; clears the corresponding MBGUID field in EnlistmentBehavior.
- On full retirement (player chooses to stop soldiering entirely): all 6 companions remain in PlayerClan as inert vanilla heroes (mod surface silences but heroes persist in vanilla campaign).

### §4.2 Spawn parameters per companion (LOCKED)

Mirrors spec §3.10 verbatim. Authored into `archetype_catalog.json` (T2):

| Companion | Skills | Attributes | Traits | Equipment template |
| :-- | :-- | :-- | :-- | :-- |
| Sergeant | One-Handed 80, Leadership 60, Tactics 40 | Vigor +1 | Valor +1, archetype-trait | Tier-3 culture infantry |
| Field Medic | Medicine 100, Steward 60, Athletics 30 | Endurance +1 | Mercy +1, archetype-trait | Civilian medic (apron, herbalist's bag) |
| Pathfinder | Scouting 80, Riding 60, Bow 40 | Cunning +1 | Calculating +1, archetype-trait | Tier-3 culture mounted scout |
| Veteran | Bow 80 OR TwoHanded 80, Tactics 60 | Endurance +1 | Honor +1, Valor +1 | Tier-4 culture infantry |
| QM Officer | Steward 80, Trade 60, Charm 40 | Social +1 | Generosity +1, archetype-trait | Officer-tier civilian dress |
| Junior Officer | Leadership 80, Tactics 60, Polearm 60 | Vigor +1 | Honor +1, archetype-trait | Tier-5 culture cavalry/infantry |

**Archetype-trait** is rolled per spawn from one of 3 archetypes per companion type (12 total archetypes; see §4.3).

### §4.3 Archetype model (LOCKED, mirrors QM 6-archetype precedent)

Each companion type has 3 archetypes. One is rolled at spawn time, persisted as a string field, and used by dialog catalog context matching.

| Companion | Archetypes | Archetype-trait flavor |
| :-- | :-- | :-- |
| Sergeant | `gruff_veteran`, `by_the_book`, `cynical` | Each adds +1 to a flavor trait (Honor / Calculating / Mercy respectively) |
| Field Medic | `compassionate`, `pragmatic`, `pious` | Mercy / Calculating / Honor |
| Pathfinder | `lone_wolf`, `talkative`, `superstitious` | Calculating / Generosity / Mercy |
| Veteran | `war_weary`, `proud`, `philosophical` | Mercy / Valor / Honor |
| QM Officer | `by_the_book`, `corner_cutter`, `weary` | Honor / Calculating / Mercy |
| Junior Officer | `cocky`, `serious`, `political` | Valor / Honor / Calculating |

Archetype affects:
- Trait set (one extra trait beyond the base set in §4.2)
- Dialog catalog node selection (catalog has variants per archetype)
- Persona naming (each archetype seeds a name pool flavor; e.g. gruff sergeant gets a culture-appropriate sturdy name)

### §4.4 Tier unlock gates (LOCKED)

| Companion | Spawn trigger | Re-spawn condition |
| :-- | :-- | :-- |
| Sergeant | First time `IsEnlisted` flips true (T1 enlistment) | Persists across lord changes; only re-spawns if the existing hero dies |
| Field Medic | First `OnTierChanged` reaching tier ≥ 3 | Persists across lord changes |
| Pathfinder | First `OnTierChanged` reaching tier ≥ 3 | Persists across lord changes |
| Veteran | First `OnTierChanged` reaching tier ≥ 5 | Released on discharge; re-spawned on next enlistment if tier ≥ 5 |
| QM Officer | First `OnTierChanged` reaching tier ≥ 7 (commission ceremony) | Released on discharge; re-spawned on next enlistment if tier ≥ 7 |
| Junior Officer | First `OnTierChanged` reaching tier ≥ 7 | Released on discharge; re-spawned on next enlistment if tier ≥ 7 |

### §4.5 Vanilla integration points (LOCKED)

- **Role assignment:** vanilla `CompanionRolesCampaignBehavior` provides "About your position in the clan..." dialog at `hero_main_options` for any clan-companion in MainParty. Plan 2 does NOT register any role-assignment dialog; vanilla covers it. Verification at T21.
- **Trait grievances:** vanilla `CompanionGrievanceBehavior` auto-fires complaints based on companion's trait set. Plan 2's spawn-time trait values determine which complaints fire. Verification at T23.
- **Companion combat:** vanilla `MissionAgentSpawnLogic` collects all MainParty roster members when MapEventSide is set to lord's side. Companion participation is automatic. Stay-back enforcement via Plan 1 T6's gate-fixed `EnlistedFormationAssignmentBehavior.TryRemoveStayBackCompanion`. Verification at T22.
- **Companion death:** vanilla `DefaultDeathProbabilityModel.GetSurvivalChance` rolls survival; on failure `KillCharacterAction.DiedInBattle` fires. Plan 2's `OnHeroKilled` handler clears the corresponding MBGUID field in EnlistmentBehavior so the spawn factory can re-spawn (per-Lord) or leave the slot permanently empty (per-player; player loses the companion). Verification at T24.

### §4.6 Talk-to sub-menu (LOCKED)

- **Camp menu slot 6** (free per Plan 1 §6.9) hosts new option `camp_hub_talk_to_companion`.
- **`OnTalkToCompanionSelected`** callback queries `CompanionLifecycleHandler` for currently-spawned companions, builds `MultiSelectionInquiryData` with one element per companion, opens conversation with selected.
- **Conversation entry** uses `CampaignMapConversation.OpenConversation(playerData, companionData)` — same pattern as `OnQuartermasterSelected:3819` and `OnTalkToSelected:3946`. Sea variant uses `CampaignMission.OpenConversationMission` with `"conversation_scene_sea_multi_agent"`.
- **Dialog tree:** vanilla `hero_main_options` (entry token for any clan-companion conversation) plus mod-authored archetype-flavored branches loaded by `CompanionDialogueCatalog` from `companion_<archetype>_*.json` files.

---

## §5 — Tooling and safeguards (additions to Plan 1 §5)

Plan 1 §5 covers all the build commands, validator commands, save-load round-trip recipe, log locations, and common failure modes. Plan 2 inherits all of those. Additional Plan 2-specific guidance:

### Companion spawn smoke test recipe

For each companion-spawn task (T4-T9):

1. Build clean.
2. Launch game with mod loaded.
3. Use Debug Tools to force-trigger the spawn condition (e.g. force-set `EnlistmentBehavior.SetTier(3)` to spawn Field Medic + Pathfinder).
4. Open Camp menu → Talk to... → confirm new companion appears in the inquiry list.
5. Select the companion, confirm conversation opens with the spawned hero.
6. Confirm the hero's properties via mod debug log:
   - `Hero.Occupation == Soldier` (NOT Wanderer per CLAUDE.md issue #11)
   - `Hero.Clan == Clan.PlayerClan` (or per-Lord owning clan as applicable)
   - `Hero.PartyBelongedTo == MobileParty.MainParty`
   - Skills match the §4.2 spawn parameters
   - Traits match the §4.2 + §4.3 spawn parameters
   - Equipment matches the archetype template
7. Save → reload → confirm hero persists, all properties preserved.

### Companion combat smoke test recipe

For T22:

1. Spawn a test companion at T1 via Debug Tools.
2. Trigger a battle with bandits (use the `Engage Bandits` debug option or wait for a hostile encounter).
3. Confirm in deployment screen that the companion appears among the player's troops.
4. Confirm in mission that the companion spawns as an agent on the player's side.
5. Set companion `ShouldCompanionFight = false` via Camp menu → Companions → toggle.
6. Trigger a new battle. Confirm the companion does NOT spawn this time (Plan 1 T6 enforcement).
7. Set toggle back to true; confirm spawn resumes.

### Companion grievance smoke test recipe

For T23:

1. Spawn Sergeant (Valor +1) at T1.
2. Have the player retreat from a battle (use Debug Tools or actual encounter).
3. Wait 1-2 in-game days. Confirm the Sergeant approaches via `CompanionGrievanceBehavior` and a "complaint" dialog fires.
4. Repeat for Field Medic + raid action (Mercy gate).
5. Repeat for QM Officer + skip wages action (Generosity gate).

### Companion death smoke test recipe

For T24:

1. Spawn Veteran (per-Lord) at T5.
2. Use Debug Tools to severely wound the Veteran in battle (or use `KillCharacterAction.ApplyByBattle` directly).
3. Confirm `OnHeroKilled` event fires.
4. Confirm `EnlistmentBehavior._veteranHero` is nulled out.
5. Confirm the Veteran does NOT respawn until next enlistment + tier ≥ 5 (per-Lord lifecycle).

---

## §6 — Tasks (sequential — must complete in order)

### T1 — Companion archetype catalog schema documented

**Goal:** Document the schema for `archetype_catalog.json` at `docs/Features/Companions/companion-archetype-catalog.md`.

**Files:** New `docs/Features/Companions/companion-archetype-catalog.md`

**Schema fields:**
- Top-level: `schemaVersion`, `companionTypes[]`
- Per companion-type: `id`, `displayName`, `lifecycle` (`per_player` | `per_lord`), `unlockTier`, `archetypes[]`
- Per archetype: `id`, `traits[]`, `namePool[]`, `dialogCatalogPrefix`
- Per companion-type also: `baseSkills{}`, `baseAttributes{}`, `equipmentTemplate`

**Verification:** Document review.

---

### T2 — Companion archetype data file authored

**Goal:** Author `ModuleData/Enlisted/Companions/archetype_catalog.json` per the §4.2 + §4.3 + §4.4 locked tables. csproj AfterBuild updated to deploy the new directory.

**Files:**
- New: `ModuleData/Enlisted/Companions/archetype_catalog.json`
- Edit: `Enlisted.csproj` — add `<CompanionsData Include="ModuleData\Enlisted\Companions\*.json"/>` ItemGroup + MakeDir + Copy step in AfterBuild target (per CLAUDE.md known issue: csproj wildcards are non-recursive; new content directories need three additions).

**Concrete change in csproj:**

```xml
<!-- Add to existing data ItemGroups: -->
<ItemGroup>
  <CompanionsData Include="ModuleData\Enlisted\Companions\*.json" />
</ItemGroup>

<!-- Inside <Target Name="AfterBuild">, add: -->
<MakeDir Directories="$(OutputPath)..\..\ModuleData\Enlisted\Companions" />
<Copy SourceFiles="@(CompanionsData)"
      DestinationFolder="$(OutputPath)..\..\ModuleData\Enlisted\Companions\" />
```

**Verification:** Build clean. JSON file deployed to `Modules\Enlisted\ModuleData\Enlisted\Companions\archetype_catalog.json` (verify file exists in deployed location).

---

### T3 — `CompanionSpawnFactory` utility class

**Goal:** Centralize the spawn recipe. Used by all 6 companion-spawn tasks (T4-T9).

**Files:** New `src/Features/Companions/CompanionSpawnFactory.cs`. Edit `Enlisted.csproj` (add `<Compile Include>`).

**Concrete API:**

```csharp
namespace Enlisted.Features.Companions
{
    public sealed class CompanionSpawnFactory
    {
        public static Hero SpawnCompanion(
            string companionTypeId,    // "sergeant", "field_medic", etc.
            Clan owningClan,           // Clan.PlayerClan or lord's clan per lifecycle
            Settlement bornSettlement, // typically lord's HomeSettlement
            out string archetype       // rolled archetype id
        );
        
        // Internal helpers:
        // - PickArchetype(companionTypeId) — random roll from catalog
        // - SelectTroopTemplate(companionTypeId, culture) — culture-appropriate base
        // - ApplySkills, ApplyAttributes, ApplyTraits, ApplyEquipment, ApplyName
    }
}
```

**Spawn recipe (mirrors `CreateQuartermasterForLord:9841-9927`):**

```csharp
// 1. Resolve template + birth settlement + culture (read from archetype_catalog.json)
// 2. var hero = HeroCreator.CreateSpecialHero(template, bornSettlement, owningClan, null, age);
// 3. hero.SetNewOccupation(Occupation.Soldier);  // CLAUDE.md issue #11
// 4. hero.HiddenInEncyclopedia = true; hero.IsKnownToPlayer = true;
// 5. Apply skills via hero.HeroDeveloper.SetInitialSkillLevel(skill, value)
// 6. Apply attributes via hero.HeroDeveloper.AddAttribute(attr, +1, checkUnspentPoints: false)
// 7. Apply traits via hero.SetTraitLevel(trait, value)
// 8. Apply equipment via hero.BattleEquipment[slot] = new EquipmentElement(item, modifier)
// 9. Apply name via hero.SetName(fullName, firstName) using archetype name pool
// 10. AddCompanionAction.Apply(owningClan, hero)
// 11. MobileParty.MainParty.MemberRoster.AddToCounts(hero.CharacterObject, 1, isHero: true)
```

**Verification:** Build clean. Unit smoke: call `SpawnCompanion("sergeant", Clan.PlayerClan, settlement, out arche)`; verify hero properties match expectations (Occupation.Soldier, in PlayerClan, in MainParty roster).

**Footgun:** Per CLAUDE.md issue #11, use `Occupation.Soldier` not `Wanderer`. Per CLAUDE.md issue #4, every save-class touched needs `EnsureInitialized()` (no save-classes here yet but the pattern applies once T10 lands fields on EnlistmentBehavior).

---

### T4 — Sergeant spawn at T1 enlistment (per-player)

**Goal:** Spawn Sergeant the first time `IsEnlisted` flips true. Per-player lifecycle: persists across lord changes.

**Files:** Edit `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`

**Concrete change:**

1. Add private field: `[SaveableField(N+1)] private Hero _sergeantHero; [SaveableField(N+2)] private string _sergeantArchetype = ""; [SaveableField(N+3)] private int _sergeantRelationship; [SaveableField(N+4)] private bool _hasMetSergeant;` (use next available SaveableField IDs in the class).
2. Add SyncData entries (`dataStore.SyncData("_sergeantHero", ref _sergeantHero);` etc.) — mirrors QM SyncData pattern at lines 1332-1336.
3. Add `EnsureInitialized()` (or extend existing) to reseat null-string fields on load.
4. Add public `GetOrCreateSergeant()` method mirroring `GetOrCreateQuartermaster:9815`. Returns `_sergeantHero`; if null/dead, calls `CompanionSpawnFactory.SpawnCompanion("sergeant", Clan.PlayerClan, _enlistedLord.HomeSettlement, out _sergeantArchetype)` and stores the result.
5. Hook `ContinueStartEnlistInternal:2938` (or wherever `IsEnlisted` flips true) to call `GetOrCreateSergeant()`.

**Verification:** Smoke per §5 companion spawn recipe. Confirm hero spawns at T1 enlistment, has correct skills/traits/equipment, persists across save/reload, persists when player switches lords.

**Footgun:** Per-player lifecycle means `_sergeantHero` should NOT be cleared on discharge. Verify discharge flow at `EnlistmentBehavior.cs` discharge handler does not null this field.

---

### T5 — Field Medic spawn at T3 unlock (per-player)

**Goal:** Spawn Field Medic the first time `OnTierChanged` reaches tier ≥ 3.

**Files:** Edit `EnlistmentBehavior.cs`

**Concrete change:** Same pattern as T4 with these differences:
- 4 new SaveableFields (`_fieldMedicHero`, `_fieldMedicArchetype`, `_fieldMedicRelationship`, `_hasMetFieldMedic`)
- `GetOrCreateFieldMedic()` method
- Hook on `OnTierChanged += (prev, curr) => { if (curr >= 3 && _fieldMedicHero == null) GetOrCreateFieldMedic(); }`

**Verification:** Force-set tier to 3 via Debug Tools; confirm Field Medic spawns with Medicine 100, Steward 60, Mercy +1; persists across save/reload.

---

### T6 — Pathfinder spawn at T3 unlock (per-player)

**Goal:** Spawn Pathfinder same trigger as T5.

**Files:** Edit `EnlistmentBehavior.cs`. Same pattern as T5; different companion ID, different SaveableFields, different skills/traits per §4.2.

**Verification:** Same as T5.

---

### T7 — Veteran spawn at T5 unlock (per-Lord)

**Goal:** Spawn Veteran the first time `OnTierChanged` reaches tier ≥ 5 with the current lord. Per-Lord lifecycle.

**Files:** Edit `EnlistmentBehavior.cs`

**Concrete change:** Same field/method pattern as T4-T6 but:
- Lifecycle is per-Lord, so the hero is owned by the lord's clan, not PlayerClan.
- Spawn factory call: `CompanionSpawnFactory.SpawnCompanion("veteran", _enlistedLord.Clan, _enlistedLord.HomeSettlement, out archetype)` (note `_enlistedLord.Clan` not `Clan.PlayerClan`).

Wait — should per-Lord companions go in PlayerClan or LordClan? Trade-offs:
- **PlayerClan:** Vanilla `CompanionRolesCampaignBehavior` "About your position in the clan..." dialog gates on `Hero.OneToOneConversationHero.Clan == Clan.PlayerClan` — so role assignment only works if companion is in PlayerClan.
- **LordClan:** narratively cleaner (the Veteran is the army's, not yours), but vanilla role-assignment dialog won't fire.

**Decision (Plan 2 LOCKS):** ALL six companions (per-player and per-Lord) go in **PlayerClan**. The "per-Lord" distinction is about *lifecycle* (released on discharge), not clan ownership. Narrative framing in dialog content makes the distinction.

**Update T7 concrete change:** `owningClan = Clan.PlayerClan` for all six. Per-Lord companions are released via `RemoveCompanionAction.ApplyByFire(playerClan, hero)` on discharge (T11).

**Verification:** Force-set tier to 5; confirm Veteran spawns, in PlayerClan, with Bow 80 + Tactics 60 + Honor +1 + Valor +1.

---

### T8 — QM Officer spawn at T7 unlock (per-Lord)

**Goal:** Spawn QM Officer the first time `OnTierChanged` reaches tier 7.

**Files:** Edit `EnlistmentBehavior.cs`. Same pattern as T7; per-Lord lifecycle (clan = PlayerClan; released on discharge).

**Verification:** Force-set tier to 7; confirm QM Officer spawns with Steward 80, Trade 60, Charm 40, Generosity +1.

---

### T9 — Junior Officer spawn at T7 unlock (per-Lord)

**Goal:** Spawn Junior Officer at tier 7.

**Files:** Edit `EnlistmentBehavior.cs`. Same pattern as T8; per-Lord lifecycle.

**Verification:** Force-set tier to 7; confirm Junior Officer spawns with Leadership 80, Tactics 60, Polearm 60, Honor +1.

---

### T10 — `EnlistmentBehavior` SyncData consolidation + EnsureInitialized

**Goal:** Confirm all 24 new SaveableFields (4 per companion × 6) persist across save/reload. Ensure `EnsureInitialized` reseats string fields to empty string on load (CLAUDE.md issue #4).

**Files:** Edit `EnlistmentBehavior.cs`

**Concrete change:** Audit the SaveableField IDs to confirm no collision with existing fields. Add an `EnsureCompanionFieldsInitialized()` method called from SyncData and OnSessionLaunched:

```csharp
private void EnsureCompanionFieldsInitialized()
{
    if (_sergeantArchetype == null) _sergeantArchetype = "";
    if (_fieldMedicArchetype == null) _fieldMedicArchetype = "";
    if (_pathfinderArchetype == null) _pathfinderArchetype = "";
    if (_veteranArchetype == null) _veteranArchetype = "";
    if (_qmOfficerArchetype == null) _qmOfficerArchetype = "";
    if (_juniorOfficerArchetype == null) _juniorOfficerArchetype = "";
    // Hero MBGUID fields can be null safely; bool/int fields default OK
}
```

**Verification:** Save-load round-trip across all 6 companions (use Debug Tools to spawn all 6 then save/reload). All 24 fields preserved. No NREs on load from a pre-Plan-2 save.

**Footgun:** CLAUDE.md issue #4 — load of a save predating Plan 2 returns null for string fields (`_sergeantArchetype` etc.). EnsureInitialized must reseat. Do NOT use string interpolation (`$"sergeant: {_sergeantArchetype}"`) before EnsureInitialized runs.

---

### T11 — Companion lifecycle handler

**Goal:** Centralize spawn-trigger subscriptions, discharge cleanup, and `OnHeroKilled` handling.

**Files:** New `src/Features/Companions/CompanionLifecycleHandler.cs`. Edit `Enlisted.csproj` (add `<Compile Include>`).

**Concrete API:**

```csharp
namespace Enlisted.Features.Companions
{
    public sealed class CompanionLifecycleHandler : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            // Hook discharge — exact event name varies; check EnlistmentBehavior for OnDischarge or similar
        }
        
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // If a session loads with IsEnlisted true, ensure spawned companions are present in MainParty
            // (not parked elsewhere from older save state).
        }
        
        private void OnTierChanged(int prev, int curr)
        {
            // Spawn tier-unlock companions
            if (curr >= 3) { /* try GetOrCreateFieldMedic + GetOrCreatePathfinder */ }
            if (curr >= 5) { /* try GetOrCreateVeteran */ }
            if (curr >= 7) { /* try GetOrCreateQMOfficer + GetOrCreateJuniorOfficer */ }
        }
        
        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            // If victim matches any of the 6 companion MBGUIDs, null the field
            // and emit a Surfaced log so player sees a toast
        }
        
        public List<Hero> GetSpawnedCompanions()
        {
            // Returns list of currently-spawned companion heroes (filtered for non-null + IsAlive)
            // Used by Talk-to sub-menu (T19-T20)
        }
        
        public void OnDischarge()
        {
            // Release per-Lord companions: Veteran, QM Officer, Junior Officer
            // RemoveCompanionAction.ApplyByFire(Clan.PlayerClan, hero) for each
            // Clear corresponding MBGUID fields in EnlistmentBehavior
        }
    }
}
```

**Verification:** Build clean. Smoke: trigger discharge after spawning all 6 companions; confirm Veteran + QM Officer + Junior Officer transition to Fugitive state via `MakeHeroFugitiveAction` (logged); confirm corresponding MBGUID fields nulled. Confirm Sergeant + Field Medic + Pathfinder remain.

**Footgun:** Hooking the right discharge event matters. Read EnlistmentBehavior end-to-end to find the correct event/method to subscribe.

---

### T12 — Sergeant dialog catalog (~25 nodes, 3 archetypes)

**Goal:** Author `ModuleData/Enlisted/Dialogue/companion_sergeant_*.json` with archetype-flavored dialog nodes.

**Files:** New `ModuleData/Enlisted/Dialogue/companion_sergeant_intro.json`, `companion_sergeant_dialogue.json`, `companion_sergeant_advice.json` (3 files, ~25 total nodes).

**Schema mirrors QM (per Plan 1 T7 schema doc).** Topics for Sergeant:
- Introduction (3 archetype variants)
- Discipline (gruff_veteran flavor: complaints about laxity; by_the_book: rules-and-regs lecture; cynical: dark humor)
- Combat readiness (each archetype's view on the company's preparation)
- Recent battle commentary (player's performance feedback)
- Advice (mentorship variants)
- Goodbye

**Authoring style guide:** Match QM's tone density and length. Each node is 1-2 sentences. Options 2-3 per node. See `qm_intro.json` for length target.

**Verification:** `python Tools/Validation/validate_content.py` Phase 18 (companion JSON schema validation) passes. Sergeant catalog loads at session launch (verified via mod log: "CompanionDialogueCatalog: loaded N nodes from companion_sergeant_*.json").

---

### T13 — Field Medic dialog catalog (~25 nodes, 3 archetypes)

**Goal:** Same as T12 for Field Medic.

**Topics:** Introduction, healing advice, religious/philosophical reflection (pious archetype), pragmatic field-medicine tips (pragmatic), war horror commentary (compassionate), recent wounded report.

**Verification:** Same as T12.

---

### T14 — Pathfinder dialog catalog (~25 nodes, 3 archetypes)

**Goal:** Same as T12 for Pathfinder.

**Topics:** Introduction, terrain/weather assessment, scouting reports, lone_wolf vs talkative vs superstitious flavor variations.

---

### T15 — Veteran dialog catalog (~25 nodes, 3 archetypes)

**Topics:** Introduction, war stories (per-Lord — no continuity across enlistments matters less here), mentorship, philosophical/proud/war-weary flavor.

---

### T16 — QM Officer dialog catalog (~25 nodes, 3 archetypes)

**Topics:** Introduction, supply concerns, complaints about wages (Generosity-trait gate fires automatically via vanilla grievance system; T16 just provides flavor dialog when player initiates).

---

### T17 — Junior Officer dialog catalog (~25 nodes, 3 archetypes)

**Topics:** Introduction, peer-officer professional tactical chat, ambition/loyalty/political flavor.

---

### T18 — `CompanionDialogueCatalog` registration of all 6 catalogs + Phase 18 validator

**Goal:** Extend Plan 1 T7's `CompanionDialogueCatalog` to register all 6 archetype catalogs (replacing the stub). Populate validator Phase 18 with real schema validation.

**Files:**
- Edit `src/Features/Conversations/Data/CompanionDialogueCatalog.cs` — load all `companion_*.json` files in `ModuleData/Enlisted/Dialogue/` matching companion-prefix patterns.
- Edit `Tools/Validation/validate_content.py` — Phase 18 validates: schemaVersion = 1, dialogueType = "companion", node ids match archetype prefix conventions, all `next_node` references resolve, archetype context fields use known values.

**Verification:**
1. Build clean.
2. `python Tools/Validation/validate_content.py` Phase 18 passes for all 6 catalogs.
3. Session launch logs show all 6 catalogs loaded with expected node counts.

---

### T19 — Camp menu slot 6 "Talk to companion" option

**Goal:** Add new Camp menu option at index 6 for the Talk-to sub-menu entry.

**Files:** Edit `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:1345-1482` `RegisterCampHubMenu` method.

**Concrete change:**

```csharp
starter.AddGameMenuOption(CampHubMenuId, "camp_hub_talk_to_companion",
    "{=companion_talk_to}Talk to...",
    args =>
    {
        var spawned = CompanionLifecycleHandler.Instance?.GetSpawnedCompanions();
        if (spawned == null || spawned.Count == 0)
        {
            args.IsEnabled = false;
            args.Tooltip = new TextObject("{=companion_talk_to_none}You have no companions to speak with.");
            return false;
        }
        args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
        return true;
    },
    OnTalkToCompanionSelected,
    false, 6);  // Index 6 — confirmed free per Plan 1 §6.9
```

**Verification:** Build clean. Camp menu shows "Talk to..." option at slot 6 when at least one companion is spawned; option is greyed (with tooltip) otherwise.

---

### T20 — `OnTalkToCompanionSelected` callback opens conversation

**Goal:** Selecting "Talk to..." shows a `MultiSelectionInquiryData` listing spawned companions; selecting one opens conversation.

**Files:** Edit `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs`. Mirror `OnTalkToSelected:3946` and `ShowLordSelectionInquiry:3971` patterns.

**Concrete change:**

```csharp
private void OnTalkToCompanionSelected(MenuCallbackArgs args)
{
    var spawned = CompanionLifecycleHandler.Instance?.GetSpawnedCompanions();
    if (spawned == null || spawned.Count == 0) return;
    
    var elements = new List<InquiryElement>();
    foreach (var hero in spawned)
    {
        var label = $"{hero.Name} ({CompanionTypeLabel(hero)})";
        elements.Add(new InquiryElement(hero, label, new ImageIdentifier(CharacterCode.CreateFrom(hero.CharacterObject))));
    }
    
    var inquiry = new MultiSelectionInquiryData(
        new TextObject("{=companion_talk_to_title}Speak with...").ToString(),
        new TextObject("{=companion_talk_to_desc}Choose who you want to talk to.").ToString(),
        elements,
        isExitShown: true,
        minSelectableOptionCount: 1,
        maxSelectableOptionCount: 1,
        affirmativeText: new TextObject("{=companion_talk_to_speak}Speak").ToString(),
        negativeText: new TextObject("{=companion_talk_to_back}Back").ToString(),
        affirmativeAction: list =>
        {
            if (list.Count == 0) return;
            var hero = list[0].Identifier as Hero;
            if (hero == null) return;
            StartConversationWithCompanion(hero);
        },
        negativeAction: null);
    
    MBInformationManager.ShowMultiSelectionInquiry(inquiry, true);
}

private void StartConversationWithCompanion(Hero companion)
{
    var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
    var companionData = new ConversationCharacterData(companion.CharacterObject, MobileParty.MainParty?.Party);
    
    if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
    {
        CampaignMission.OpenConversationMission(playerData, companionData, "conversation_scene_sea_multi_agent");
    }
    else
    {
        CampaignMapConversation.OpenConversation(playerData, companionData);
    }
}
```

**Verification:** Smoke: spawn 3 companions; open Talk-to inquiry; select Field Medic; confirm conversation opens with vanilla "About your position in the clan..." entry plus mod-authored archetype branches.

---

### T21 — Vanilla `CompanionRolesCampaignBehavior` integration verified

**Goal:** Verify role-assignment dialog fires correctly for spawned companions. NO new code; just smoke testing.

**Test recipe:**
1. Spawn Field Medic at T3.
2. Open Camp → Talk to... → Field Medic.
3. Confirm vanilla "About your position in the clan..." option appears in dialog.
4. Select it; confirm role choices appear (Surgeon / Engineer / Quartermaster / Scout).
5. Assign Field Medic as Surgeon.
6. Close conversation.
7. Confirm `MobileParty.MainParty.EffectiveSurgeon == fieldMedicHero`.
8. Open Camp → Quartermaster → Browse equipment (or any health-related context).
9. Confirm Surgery survival bonus reflects Field Medic's Medicine 100 (compare to lord's Surgeon for delta).

**Verification:** Manual smoke test passes. Document results in T25 verification report.

**Footgun:** `CompanionRolesCampaignBehavior.companion_role_discuss_on_condition` requires `Hero.OneToOneConversationHero.Clan == Clan.PlayerClan` and `PartyBelongedTo == MainParty`. Both must be true for Plan 2 spawned companions; verify.

---

### T22 — Companion combat verification smoke

**Goal:** Verify all 6 companion types fight in lord's battles when stay-back is OFF, and don't when stay-back is ON.

**Test recipe per §5 companion combat smoke test recipe.** Run for each of the 6 companions; document results in T25 report.

**Verification:** All 6 companions confirmed to spawn as agents in lord's battles. All 6 confirmed to NOT spawn when withhold toggle is ON. Plan 1 T6 stay-back gate fix verified active.

---

### T23 — Companion grievance verification smoke

**Goal:** Verify vanilla `CompanionGrievanceBehavior` auto-fires complaints based on companion trait sets. NO new code.

**Test recipe per §5 companion grievance smoke test recipe.** Verify:
- Sergeant (Valor +1) complains about retreating
- Field Medic (Mercy +1) complains about village raiding
- QM Officer (Generosity +1) complains about unpaid wages and starvation
- Veteran (Valor +1, Honor +1) complains about retreats AND honor-violations (if game supports)
- Pathfinder (Calculating +1) — silent (no Valor/Mercy/Generosity gate; intentional)
- Junior Officer (Honor +1) — silent (no grievance gate matches Honor in vanilla)

**Verification:** Manual smoke test. Document which complaints fire and which don't (matches CLAUDE.md design).

**Footgun:** Grievances may take 1-3 in-game days to surface after the trigger event. Use Debug Tools to advance time if needed.

---

### T24 — Companion-death lifecycle smoke

**Goal:** Verify `OnHeroKilled` handler nulls the corresponding MBGUID field; per-Lord respawn vs per-player permanent loss.

**Test recipe per §5 companion death smoke test recipe.**

Test cases:
- Kill Veteran in battle (per-Lord). Confirm `_veteranHero` nulled. Confirm Veteran does NOT respawn until next enlistment.
- Kill Sergeant in battle (per-player). Confirm `_sergeantHero` nulled. Sergeant does NOT respawn even on next enlistment (player permanently loses Sergeant).

**Verification:** Both test cases pass. Mod session log shows `OnHeroKilled` fired with correct hero identification.

---

### T25 — Plan 2 verification report

**Goal:** Document all smoke results, any deviations from expected behavior, sign-off.

**Files:** New `docs/superpowers/plans/2026-04-24-ck3-wanderer-companion-substrate-verification.md`

**Content outline:**
- Build clean confirmation (`dotnet build` output)
- Validator passes (`validate_content.py` all 20 phases)
- Save-load round-trip results across all 6 companions
- Companion-spawn smoke results (T4-T9 each)
- Combat verification smoke (T22)
- Grievance verification smoke (T23)
- Death lifecycle smoke (T24)
- Vanilla role-assignment smoke (T21)
- Sign-off: Plan 2 ✅ complete; Plans 3-7 unblocked.

**Verification:** Report committed; CLAUDE.md current-status block updated.

---

## §7 — Risks (HIGH/MEDIUM/LOW with mitigations)

### Risk H1 — Companion combat regression (HIGH)

**Vector:** Plan 1 T6's stay-back gate fix exposed companion-removal logic to all tiers. Plan 2 spawns companions at all tiers. If T6's fix has edge cases that didn't surface in Plan 1's smoke, Plan 2 will hit them.

**Mitigation:**
- T22 runs the combat smoke test recipe for all 6 companion types in 3 battle scenarios (small bandit camp, medium battle, large army battle) — covers spawn-cap and removal-loop edge cases.
- If combat regression appears, escalate to a focused investigation and possibly restore the T7+ gate behind a feature flag.

### Risk M1 — Save-state migration from Plan-1-only saves (MEDIUM)

**Vector:** A save created during Plan 1 development has all the empty store shells but no companion fields. Plan 2's 24 new SaveableFields must default cleanly when loading such a save.

**Mitigation:**
- T10 explicitly tests load of a pre-Plan-2 save; `EnsureCompanionFieldsInitialized` handles null string fields.
- Hero MBGUID fields default to null safely (TaleWorlds save system handles this).

### Risk M2 — Vanilla role-assignment gate unmet (MEDIUM)

**Vector:** `CompanionRolesCampaignBehavior.companion_role_discuss_on_condition` requires `Hero.OneToOneConversationHero.Clan == Clan.PlayerClan` AND `PartyBelongedTo == MainParty`. If T7 lock decision (all companions in PlayerClan) is implemented incorrectly, role assignment dialog won't fire.

**Mitigation:**
- T21 explicitly verifies role assignment dialog appears.
- All 6 spawn factories confirmed to use `Clan.PlayerClan` per §4.5 lock decision.

### Risk M3 — Companion grievance not firing as expected (MEDIUM)

**Vector:** Vanilla `CompanionGrievanceBehavior` may have hidden gates beyond the trait check (e.g. relation threshold, time since last grievance). If grievances don't fire in T23 smoke, design assumption that "personality is free from spawn-time traits" is weaker.

**Mitigation:**
- T23 documents which complaints fire in actual gameplay.
- If a gate prevents expected complaint, document as deviation and consider mod-side override (e.g. patch the gate or add custom grievance).

### Risk L1 — Companion JSON content authoring quality (LOW)

**Vector:** AI-generated dialog content can be technically schema-valid but emotionally flat or repetitive across archetypes.

**Mitigation:**
- Tone reference: match `qm_intro.json` density and personality variation.
- Optional: human review pass on Sergeant catalog (T12) before authoring T13-T17 to lock in quality bar.
- Polish pass available in Plan 7's tuning phase.

### Risk L2 — Equipment template selection ambiguity (LOW)

**Vector:** "Tier-3 culture infantry" is not a single concrete template. Spawn factory must pick one consistent template per (companion type, culture).

**Mitigation:**
- T2 (archetype catalog) bakes specific template ID per (companion type × culture × archetype). Spawn factory just looks up.

---

## §8 — Verification gates (must pass before Plan 2 complete)

- [ ] `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` passes
- [ ] `python Tools/Validation/validate_content.py` passes (all 20 phases including populated Phase 18)
- [ ] `Tools/Validation/lint_repo.ps1` passes
- [ ] All 6 companions spawn at correct tier triggers (T4-T9 per-task smoke)
- [ ] All 24 SaveableFields persist across save/reload (T10 smoke)
- [ ] Per-Lord lifecycle: Veteran + QM Officer + Junior Officer released on discharge; per-player Sergeant + Field Medic + Pathfinder persist (T11 smoke)
- [ ] All 6 dialog catalogs load at session launch (T12-T18)
- [ ] Camp menu slot 6 "Talk to..." surfaces (T19)
- [ ] Talk-to inquiry lists spawned companions; selecting opens conversation (T20)
- [ ] Vanilla role-assignment dialog fires for spawned companions (T21 smoke)
- [ ] All 6 companion types fight in lord's battles when stay-back OFF (T22 smoke)
- [ ] Stay-back toggle works at all tiers (T22 smoke)
- [ ] Trait-gated grievances fire correctly (T23 smoke)
- [ ] `OnHeroKilled` clears MBGUID fields; per-Lord respawn vs per-player permanent loss (T24 smoke)
- [ ] Verification report committed at `docs/superpowers/plans/2026-04-24-ck3-wanderer-companion-substrate-verification.md`

---

## §9 — Definition of done

Plan 2 is complete when:

1. All 25 tasks marked ✅ done with their per-task verifications passed.
2. All §8 verification checkboxes pass.
3. Verification report committed.
4. CLAUDE.md current-status block updated to reference Plan 2 shipped.
5. Plans 3-7 can begin parallel implementation against the spawned-companion substrate.

---

## §10 — Hand-off to Plans 3-7

After Plan 2 ships, downstream plans inherit:

### For Plan 3 (Rank-Ceremony Arc)
- Companions are spawned and present in conversation context. Plan 3's ceremony storylets can name witnesses (Sergeant Garreth, Sister Aleyne, etc.).
- Plan 3's `CeremonyProvider` references companions by querying `CompanionLifecycleHandler.GetSpawnedCompanions()` for witness selection.

### For Plan 4 (Officer Trajectory)
- QM Officer + Junior Officer are spawned at T7. Plan 4's officer-tier dialog branches can target peer-officer conversation via these companions.
- Officer cape / banner / weapon-modifier registrations don't depend on Plan 2; independent track.

### For Plan 5 (Endeavor System)
- Endeavor companion-agent gating: Plan 5 reads `CompanionLifecycleHandler.GetSpawnedCompanions()` to determine which endeavor categories unlock via companion-presence (hybrid skill-OR-companion gating per spec §3.8).
- `IsAssignedToEndeavor` flag (Plan 1 T8) is populated by Plan 5; Plan 2 leaves it at default (false).

### For Plan 6 (Roll of Patrons)
- Patron troop loans (knights borrowed from former lords for 7 days) are TEMPORARY companions distinct from Plan 2's persistent archetypes. Plan 6 uses `AddCompanionAction.Apply` + scheduled `RemoveCompanionAction.ApplyByFire` on its own; Plan 2's `CompanionLifecycleHandler` does not interfere.

### For Plan 7 (Personal Kit + Lifestyle + Smoke)
- Smoke testing includes companion-related interaction matrix (companion + ceremony + endeavor + grievance combinations).

---

## §11 — Out of scope (explicitly NOT in Plan 2)

- Ceremony storylets (Plan 3)
- Ceremony witness reactions (Plan 3 — Plan 2 just makes companions queryable)
- Officer equipment registrations (Plan 4)
- Officer dialog branches at priority 110-115 (Plan 4)
- Officer healing model patches (Plan 4)
- Endeavor catalog content (Plan 5)
- Endeavor companion-agent assignment logic (Plan 5)
- Patron favor outcomes (Plan 6)
- Patron-loaned-knight companions (Plan 6 — distinct lifecycle from Plan 2)
- Personal Kit catalog (Plan 7)
- Lifestyle Unlocks catalog (Plan 7)
- Trait drift system for ceremony choices (Plan 3)
- News-feed integration of companion deaths (separate news-v2 substrate)
- Custom non-vanilla grievance triggers (vanilla only — mod doesn't add new triggers in Plan 2)

---

## §12 — References

- [Plan 1 — Architecture Foundation](2026-04-24-ck3-wanderer-architecture-foundation.md)
- [Plan 1 verification report](2026-04-24-ck3-wanderer-architecture-foundation-verification.md)
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)
- [Spec v6](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)
- [AGENTS.md](../../../AGENTS.md)
- [CLAUDE.md](../../../CLAUDE.md)
- Existing `EnlistmentBehavior.GetOrCreateQuartermaster` (the canonical companion-spawn precedent)
- Existing `EnlistmentBehavior.AssignInitialEquipment` (equipment assignment precedent)
- Existing `EnlistedMenuBehavior.OnTalkToSelected` (talk-to inquiry precedent)
- Existing `QMDialogueCatalog` (JSON dialog schema precedent)
- Existing `CompanionAssignmentManager` (Fight/Stay-Back toggle, extended in Plan 1 T8)
- Decompile: `HeroCreator`, `HeroDeveloper`, `Hero`, `AddCompanionAction`, `RemoveCompanionAction`, `MakeHeroFugitiveAction`, `CompanionRolesCampaignBehavior`, `CompanionGrievanceBehavior`, `MobileParty.EffectiveX`, `DefaultPartyHealingModel`, `DefaultDeathProbabilityModel`, `KillCharacterAction`, `MultiSelectionInquiryData`, `CampaignMapConversation`
