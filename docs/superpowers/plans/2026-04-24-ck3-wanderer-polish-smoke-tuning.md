# Plan 7 — CK3 Wanderer Mechanics: Personal Kit + Lifestyle Unlocks + Smoke + Tuning

**Status:** Draft v1 (2026-04-24). Seventh and final plan implementing the [CK3 Wanderer Mechanics Systems Analysis (v6)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md). See spec §8 for the full plan structure.

**Scope:** Three concerns absorbed into one plan because each is small individually and they're naturally the polish + verify layer:
1. **Personal Kit content** — Bedroll/Sharpening Stone/Field Kit catalog (3 items × 3 levels = 9 catalog entries) + tick-handler bonuses (Plan 1 T13 shipped a no-op handler; Plan 7 populates).
2. **Lifestyle Unlocks** — 3 lifestyle paths × 3 milestones × ~3 unlock features each (~27 features total) + rank-up milestone hook + integration with menu/dialog/storylet conditions.
3. **Smoke + Tuning** — 12 mechanic golden-path scenarios + cross-mechanic interaction matrix + numeric tuning passes from playtest data.

This is the final integration pass. Plan 7's verification report closes out the entire 7-plan family.

**Estimated tasks:** 22. **Estimated effort:** 4-6 days with AI-driven implementation. Tuning portion is human-bound (playtest required).

**Dependencies:** Plans 1-6 must all be complete and verified before Plan 7 begins.

---

## §0 — Read these first

### Required prior plan documentation (ALL of them)
1. **[Plan 1](2026-04-24-ck3-wanderer-architecture-foundation.md)** + verification — `PersonalKitTickHandler.cs` no-op stub from T13, `LifestyleUnlockStore.cs` from T12 with `IsUnlocked`/`Unlock` ready for content keys.
2. **[Plan 2](2026-04-24-ck3-wanderer-companion-substrate.md)** + verification.
3. **[Plan 3](2026-04-24-ck3-wanderer-rank-ceremony-arc.md)** + verification.
4. **[Plan 4](2026-04-24-ck3-wanderer-officer-trajectory.md)** + verification.
5. **[Plan 5](2026-04-24-ck3-wanderer-endeavor-system.md)** + verification.
6. **[Plan 6](2026-04-24-ck3-wanderer-roll-of-patrons.md)** + verification.
7. **[Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)**.

### Required spec reading
8. **[Spec v6 §3.2 Personal-kit "buildings"](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — design source. 3-slot footlocker (Bedroll/Sharpening Stone/Field Kit) with passive bonuses.
9. **[Spec v6 §3.6 Lifestyle perk trees](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — option 3 unlocks-version locked (NOT perks-version per spec §4.5 do-not list).
10. **[Spec v6 §6.2 Health subsystem](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — vanilla wound model is binary; Personal Kit bonuses use healing-rate multiplier + food-variety patches.
11. **[Spec v6 §6.7 + §6.8](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — pacing rails for any lifestyle-triggered modal popups.

### Required project guidance
12. **[AGENTS.md](../../../AGENTS.md)** — Critical Rule #3 (gold transactions for kit purchases via `GiveGoldAction`).
13. **[CLAUDE.md](../../../CLAUDE.md)** — Pitfall #14 (List<string> not HashSet<string> for LifestyleUnlockStore — already locked in Plan 1 T12).

### Required existing-code orientation
14. **`src/Features/PersonalKit/PersonalKitTickHandler.cs`** — no-op stub from Plan 1 T13. Plan 7 populates with bonus application logic.
15. **`src/Features/Lifestyles/LifestyleUnlockStore.cs`** — Campaign behavior from Plan 1 T12 with `IsUnlocked`/`Unlock`/`EnsureInitialized`. Plan 7 populates content + hooks rank-up milestones.
16. **`src/Features/Qualities/QualityStore.cs`** — Personal Kit uses quality slots `kit.bedroll_level`, `kit.sharpening_stone_level`, `kit.field_kit_level` per Plan 1 T13 decision (option B).
17. **`src/Features/Equipment/Behaviors/QuartermasterManager.cs`** — kit purchases extend QM dialog. Existing JSON catalog (`QMDialogueCatalog`) handles dialog flow.
18. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:9748` `OnTierChanged`** — lifestyle milestone hook (T2/T4/T6 thresholds per spec §3.6).
19. **`src/Mod.GameAdapters/Patches/OfficerHealingPatch.cs` (Plan 4 T9)** — pattern for Plan 7's Personal Kit healing patch.

### Required decompile orientation
20. **`Decompile/TaleWorlds.CampaignSystem.GameComponents/DefaultPartyHealingModel.cs:232-286`** — patch target for Field Kit healing-rate boost (similar to Plan 4 Officer Tent).
21. **`Decompile/TaleWorlds.CampaignSystem.GameComponents/DefaultPartyMoraleModel.cs:63-130`** — Bedroll food-variety patch target.
22. **`Decompile/TaleWorlds.CampaignSystem/HeroDeveloper.cs:189` `AddSkillXp`** — Sharpening Stone passive XP target.

---

## §1 — What this plan delivers

After Plan 7 ships:

### Personal Kit (substrate from Plan 1, content + bonuses here)
- **Camp menu Quartermaster sub-flow extended** with personal-kit upgrade options (catalog entries for Bedroll/Sharpening/Field Kit at levels 1-3).
- **Tick handler populated** — every hourly/daily tick reads `QualityStore` kit levels and applies bonuses:
  - **Bedroll L1-L3:** +1/+2/+3 to party food variety (via Harmony patch)
  - **Sharpening Stone L1-L3:** +0.2/+0.5/+1.0 passive One-Handed/Two-Handed XP per hour
  - **Field Kit L1-L3:** +2/+4/+6 daily HP regen for player (additive to Officer Tent if T7+)

### Lifestyle Unlocks
- **3 lifestyle paths** authored: **Forager** (survival/wilderness), **Tactician** (command/strategy), **Diplomat** (social/connections). Player picks one path on first reaching T2 (or T3 if T2 deferred).
- **3 milestones per path** (unlock at T2, T4, T6) — player must commit to a path; milestones cumulative within chosen path.
- **~3 unlock features per milestone** — total ~27 unlock IDs. Examples: `forager.wild_provisions` (Camp menu Forage option), `tactician.battle_briefing` (pre-battle dialog with lord), `diplomat.lord_courtesy` (audience tone improves with non-enlisted lords).
- **Lifestyle picker dialog** — first time player reaches T2 with no path picked, dialog offers 3 lifestyle paths. Choice locked for the career (changeable only via retirement).
- **Cross-system integration** — endeavors/ceremonies/patron favors/menu options consult `LifestyleUnlockStore.IsUnlocked(featureId)` for gating + flavor.

### Smoke + Tuning
- **12 mechanic golden-path scenarios** documented + executed (one per major mechanic across Plans 1-6).
- **Cross-mechanic interaction matrix tests** — companion + ceremony + endeavor + grievance + patron interactions.
- **Numeric tuning pass** — XP magnitudes, scrutiny risk percentages, death rates, cooldowns, gold amounts all reviewed against playtest data.
- **Final verification report** closes the 7-plan family. CLAUDE.md current-status block updated to ✅ shipped.

---

## §2 — Subsystems explored (Plan 7 specifically)

| Audit | Finding | Spec |
| :-- | :-- | :-- |
| Personal Kit | Item slot via `QualityStore` kit qualities (Plan 1 T13 decision); tick-bonuses via Harmony patches mirroring Plan 4 patterns | §3.2 + §6.2 |
| Lifestyle (option 3 unlocks) | `List<string> UnlockedFeatures` simple store; rank-up milestone hook via `OnTierChanged` | §3.6 |
| PerkObject API gap | `Hero.HeroDeveloper.AddPerk` enforces `RequiredSkillValue`; option 3 sidesteps entirely. **Plan 7 ships unlocks-only; no PerkObject path** | §3.6 + §4.5 do-not |
| Cross-system gating | LifestyleUnlockStore.IsUnlocked queryable from any storylet condition + menu condition + dialog condition | spec wide |

---

## §3 — Subsystems Plan 7 touches

### Files modified

| File | Change | Tasks |
| :-- | :-- | :-- |
| `src/Features/PersonalKit/PersonalKitTickHandler.cs` (Plan 1 stub) | Populate hourly+daily tick logic to apply kit bonuses | T2, T3 |
| `src/Features/Lifestyles/LifestyleUnlockStore.cs` (Plan 1 stub) | Add helper methods: `GetCurrentPath()`, `GetMilestoneCount(path)`, etc. | T8 |
| `src/Features/Equipment/Behaviors/QuartermasterManager.cs` | Add kit purchase flow (existing JSON QM catalog gets new "Browse personal effects" branch) | T5 |
| `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:OnTierChanged` | Hook for lifestyle milestone unlock + lifestyle picker (first time at T2) | T9, T11 |
| `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs` | Add lifestyle picker dialog branches | T11 |
| `Tools/Validation/validate_content.py` | Phase 18 (companion JSON), Phase 19 (endeavor catalog), Phase 20 (ceremony completeness) — already populated. Plan 7 adds Phase 21 (lifestyle unlock catalog) | T18 |

### Files created

| File | Purpose | Tasks |
| :-- | :-- | :-- |
| `docs/Features/PersonalKit/personal-kit-catalog.md` | Schema reference for kit JSON | T1 |
| `ModuleData/Enlisted/PersonalKit/kit_catalog.json` | 9 catalog entries (3 items × 3 levels) | T4 |
| `src/Mod.GameAdapters/Patches/PersonalKitMoralePatch.cs` | Harmony patch for Bedroll food-variety bonus | T2 |
| `src/Mod.GameAdapters/Patches/PersonalKitHealingPatch.cs` | Harmony patch for Field Kit healing-rate bonus | T3 |
| `src/Features/PersonalKit/SharpeningStoneXpHandler.cs` | Hourly XP-drift via `Hero.HeroDeveloper.AddSkillXp` | T3 |
| `docs/Features/Lifestyles/lifestyle-catalog.md` | Schema reference for lifestyle catalog | T6 |
| `ModuleData/Enlisted/Lifestyles/lifestyle_catalog.json` | 3 paths × 3 milestones × ~3 unlocks definition | T7 |
| `src/Features/Lifestyles/LifestylePicker.cs` | Lifestyle picker dialog + first-time T2 trigger | T10 |
| `src/Features/Lifestyles/LifestyleMilestoneHook.cs` | Hooks `OnTierChanged` for milestone unlocks | T11 |
| `docs/superpowers/plans/2026-04-24-ck3-wanderer-polish-smoke-tuning-verification.md` | Plan 7 final verification report (closes 7-plan family) | T22 |

### Subsystems Plan 7 does NOT touch

- Companion spawning (Plan 2)
- Ceremony storylets (Plan 3)
- Officer equipment (Plan 4)
- Endeavor catalog (Plan 5)
- Patron favors (Plan 6)
- News-feed substrate (separate spec)
- PerkObject (excluded per "do not" list)

---

## §4 — Locked design decisions

### §4.1 Personal Kit catalog (LOCKED)

| Item | L1 cost | L2 cost | L3 cost | L1 bonus | L2 bonus | L3 bonus |
| :-- | --: | --: | --: | :-- | :-- | :-- |
| Bedroll | 60 g | 180 g | 360 g | +1 food variety | +2 food variety | +3 food variety |
| Sharpening Stone | 60 g | 180 g | 360 g | +0.2 XP/hr One-Handed | +0.5 XP/hr | +1.0 XP/hr |
| Field Kit | 60 g | 180 g | 360 g | +2 HP/day regen | +4 HP/day | +6 HP/day |

Levels are cumulative (L3 means already paid for L1 + L2). Total full-kit cost: 3 × (60 + 180 + 360) = **1,800 gold**.

QM dialog adds new "Browse personal effects" branch surfacing these as purchasable. Player gold deducted via `GiveGoldAction.ApplyBetweenCharacters(hero, null, cost)` per AGENTS.md Rule #3.

### §4.2 Lifestyle paths (LOCKED — 3 paths)

#### Forager Path
- **Theme:** Wilderness self-reliance, scouting, survival
- **Milestones:**
  - **T2 (level 1):** Unlocks 3 features
    - `forager.wild_provisions` — Camp menu Forage option (single-phase Endeavor — gain food)
    - `forager.bedroll_skill` — Bedroll bonus +1 effective level (e.g. L1 acts as L2)
    - `forager.terrain_familiarity` — Scouting endeavor success rate +10%
  - **T4 (level 2):** Unlocks 3 features
    - `forager.live_off_land` — Reduced supply consumption -25%
    - `forager.herbalist_apprentice` — Field Medic companion (if spawned) gives +20% to medical endeavor success
    - `forager.silent_step` — Stealth-related Rogue endeavors get -10% scrutiny risk
  - **T6 (level 3):** Unlocks 3 features
    - `forager.master_woodsman` — Reduced supply consumption -50%
    - `forager.deep_recon` — Plan 5 endeavor `endeavor.scouting.deep_recon` available regardless of skill
    - `forager.wilderness_kingdom` — All Scouting endeavors get +20% success rate

#### Tactician Path
- **Theme:** Command, strategy, formation
- **Milestones:**
  - **T2:**
    - `tactician.formation_drill` — Soldier endeavor `drill_competition` -1 day duration
    - `tactician.battle_brief` — Pre-battle dialog with lord (asks for tactical input; affects auto-resolve outcome slightly)
    - `tactician.troop_eye` — Veteran companion (if spawned) gives +30% to soldier endeavor success
  - **T4:**
    - `tactician.junior_command` — Officer endeavor unlocks at T5 (one rank early — vs T7 default)
    - `tactician.morale_speaker` — Party morale +5 baseline
    - `tactician.flank_reader` — Soldier endeavors that involve battles get +XP
  - **T6:**
    - `tactician.fieldcraft_mastery` — Officer Tent (Plan 4) bonus +2 HP/day additional
    - `tactician.command_presence` — Junior Officer companion (T7+) gives +50% to all soldier endeavors
    - `tactician.tactical_genius` — Player +10% in auto-resolve battle outcome

#### Diplomat Path
- **Theme:** Social, connections, court
- **Milestones:**
  - **T2:**
    - `diplomat.courtesy` — Notable interactions in towns gain +5 relation per encounter (passive)
    - `diplomat.witness_charm` — Social endeavors get +20% success rate
    - `diplomat.audience_grace` — Lord conversation cooldowns -25%
  - **T4:**
    - `diplomat.peer_alliance` — Peer-officer professional conversation (Plan 4) unlocks at T5 (one rank early)
    - `diplomat.gentleman_thief` — Rogue endeavors involving Charm get -10% scrutiny risk
    - `diplomat.fluent_speaker` — Foreign-faction lord interactions reduced relation penalty
  - **T6:**
    - `diplomat.kingsmaker` — Patron favors (Plan 6) cooldowns -50%
    - `diplomat.matchmaker` — `MarriageFacilitation` favor (Plan 6) cooldown -75%
    - `diplomat.silver_tongue` — All Social + Roguery skill XP gains +25%

### §4.3 Lifestyle picker mechanism (LOCKED)

- First time `OnTierChanged` reaches tier ≥ 2 AND `LifestyleUnlockStore.GetCurrentPath() == null`, fire lifestyle picker dialog (via `LifestylePicker.OpenPicker()`).
- Picker is a `MultiSelectionInquiryData` with 3 options (Forager / Tactician / Diplomat) + descriptions.
- Player must pick one (no exit). Choice locks via `FlagStore.SetString("lifestyle.path", "forager")`.
- T2 milestone unlocks fire automatically after picker (3 features for chosen path).
- T4 + T6 milestones fire automatically when those tiers reach (no further player choice — milestone within chosen path).
- **No path-switching** in v1 (CK3 also locks lifestyle commitments).

### §4.4 Cross-system gating (LOCKED)

Lifestyle unlock IDs are queryable from any storylet/menu/dialog condition via `LifestyleUnlockStore.Instance?.IsUnlocked(featureId)`.

Examples:
- Endeavor template includes `requires_lifestyle: ["forager.deep_recon"]` — endeavor available only if unlocked.
- Camp menu option condition checks `IsUnlocked("forager.wild_provisions")` — option visible only if unlocked.
- Dialog branch condition checks `IsUnlocked("diplomat.courtesy")` — branch surfaces only if unlocked.

### §4.5 Cumulative milestone semantics (LOCKED)

T2 milestone unlocks 3 features for chosen path; T4 unlocks ANOTHER 3 features (cumulative — T2 features remain unlocked); T6 unlocks ANOTHER 3 features (T2 + T4 features remain).

A T6+ Forager has all 9 Forager features unlocked. A T4 Tactician has 6 Tactician features unlocked. Etc.

---

## §5 — Tooling and safeguards

Inherits Plans 1-6. Plan 7-specific:

### Personal Kit smoke recipe

For T2-T5:
1. Build clean.
2. Use Debug Tools to give player 1,800 gold.
3. Camp → Quartermaster → Browse personal effects → buy Bedroll L1.
4. Verify gold deducted (60 g).
5. Verify `QualityStore.GetInt("kit.bedroll_level") == 1`.
6. Inspect party morale; verify food variety +1 contribution.
7. Save → reload → verify kit level + morale bonus persist.

### Lifestyle picker smoke recipe

For T9-T11:
1. Build clean. Start fresh save.
2. Enlist with Vlandian lord.
3. Use Debug Tools to grant T2 XP requirements; trigger promotion (proving event or dialog).
4. Verify lifestyle picker dialog fires AFTER ceremony (T3 ceremony fires; then picker if not yet picked).
5. Pick "Forager" path.
6. Verify `LifestyleUnlockStore.GetCurrentPath() == "forager"`.
7. Verify 3 T2 features unlocked (`IsUnlocked("forager.wild_provisions")`, etc.).
8. Save → reload → verify path + unlocks persist.
9. Force tier to 4; verify T4 milestone fires (3 more features unlocked, total 6).
10. Force tier to 6; verify T6 milestone fires (3 more, total 9).

### Cross-mechanic interaction matrix smoke (T19)

Run the following scenarios end-to-end:
1. **Companion + Ceremony:** T1 enlist → Sergeant spawns → T1→T2 ceremony fires → choice "trust the line" → Sergeant relation +10 → vanilla grievance behavior fires future complaints based on relation.
2. **Companion + Endeavor:** T3 enlist + Field Medic spawned → Medical endeavor unlocked via companion gating → endeavor runs → resolution applies skill XP.
3. **Ceremony + Endeavor:** T2→T3 ceremony "frugal" choice → unlocks `endeavor.rogue.dice_game` (if hooked).
4. **Lifestyle + Endeavor:** Forager path T4 → `forager.silent_step` unlocked → Rogue endeavor scrutiny -10%.
5. **Patron + Endeavor:** Mid-career discharge → patron entry created → call in `AnotherContract` favor → spawns ContractActivity (Plan 5).
6. **Officer + Companion:** T7 promotion → Junior Officer + QM Officer spawn → ceremony T6→T7 fires → gear deltas apply (banner, cape, weapon modifier) → peer-officer dialog branches surface.
7. **Personal Kit + Officer Tent:** T7 + Field Kit L3 → daily HP regen = base 11 + Officer Tent 6 + Field Kit 6 = 23 HP/day (verify ExplainedNumber breakdown).
8. **Lifestyle + Patron:** Diplomat path T6 → `diplomat.kingsmaker` unlocked → patron favor cooldowns halved.

### Numeric tuning pass smoke (T20)

Review numeric values across Plans 2-6:
- Companion trait magnitudes (default ±1)
- Ceremony choice trait drift (default ±1)
- Endeavor skill XP magnitudes (default 20-100 per resolution)
- Endeavor scrutiny risk per phase (default 0.15 baseline)
- Patron favor gold loan amount (default 5000)
- Officer Tent HP/day (default +6)
- Personal Kit prices (60/180/360)

Document any values that feel "off" in playtest. Tune in T20.

---

## §6 — Tasks (sequential)

### T1 — Personal Kit catalog schema doc

**Files:** New `docs/Features/PersonalKit/personal-kit-catalog.md`. Schema for `kit_catalog.json`.

### T2 — Personal Kit Bedroll morale patch

**Goal:** Harmony patch for `DefaultPartyMoraleModel` food-variety boost. Reads `kit.bedroll_level`, adds +N to food variety contribution.

**Files:** New `src/Mod.GameAdapters/Patches/PersonalKitMoralePatch.cs`.

### T3 — Personal Kit Field Kit healing patch + Sharpening Stone XP handler

**Files:**
- New `src/Mod.GameAdapters/Patches/PersonalKitHealingPatch.cs` (Field Kit additive to `GetDailyHealingHpForHeroes`)
- New `src/Features/PersonalKit/SharpeningStoneXpHandler.cs` (hourly tick reads `kit.sharpening_stone_level`, calls `Hero.HeroDeveloper.AddSkillXp(DefaultSkills.OneHanded, levelMagnitude)`)

### T4 — `kit_catalog.json` authored

**Files:** New `ModuleData/Enlisted/PersonalKit/kit_catalog.json`. 9 entries per §4.1 table.

Plus csproj ItemGroup + MakeDir + Copy in AfterBuild.

### T5 — QM dialog extension for kit purchases

**Goal:** Add nodes to existing `QMDialogueCatalog` JSON for "Browse personal effects" branch.

**Files:** New `ModuleData/Enlisted/Dialogue/qm_personal_kit.json`.

QM dialog flow: player asks "Show me your personal effects" → QM presents catalog → player picks an item + level → confirm purchase → gold deduct + `QualityStore.SetInt(quality, level)`.

### T6 — Lifestyle catalog schema doc

**Files:** New `docs/Features/Lifestyles/lifestyle-catalog.md`. Schema for `lifestyle_catalog.json`.

### T7 — `lifestyle_catalog.json` authored

**Files:** New `ModuleData/Enlisted/Lifestyles/lifestyle_catalog.json`. 3 paths × 3 milestones × ~3 features per §4.2.

### T8 — `LifestyleUnlockStore` helper methods

**Files:** Edit `src/Features/Lifestyles/LifestyleUnlockStore.cs`.

**Concrete additions:**

```csharp
public string GetCurrentPath()
{
    return FlagStore.Instance?.GetString("lifestyle.path") ?? "";
}

public int GetMilestoneCount(string path)
{
    if (string.IsNullOrEmpty(path)) return 0;
    int count = 0;
    foreach (var feature in _unlockedFeatures)
    {
        if (feature.StartsWith($"{path}."))
            count++;
    }
    return count;
}

public void UnlockMilestoneFeatures(string path, int milestoneLevel)
{
    // Reads lifestyle_catalog.json, finds path's milestone-N features, calls Unlock on each
}
```

### T9 — `OnTierChanged` lifestyle milestone hook

**Goal:** When player reaches T2 / T4 / T6 with chosen path, unlock corresponding milestone's features automatically.

**Files:** New `src/Features/Lifestyles/LifestyleMilestoneHook.cs`.

**Concrete:**

```csharp
private void OnTierChanged(int prev, int curr)
{
    var path = LifestyleUnlockStore.Instance?.GetCurrentPath();
    if (string.IsNullOrEmpty(path)) return;  // No path picked yet
    
    if (prev < 2 && curr >= 2) LifestyleUnlockStore.Instance.UnlockMilestoneFeatures(path, 1);
    if (prev < 4 && curr >= 4) LifestyleUnlockStore.Instance.UnlockMilestoneFeatures(path, 2);
    if (prev < 6 && curr >= 6) LifestyleUnlockStore.Instance.UnlockMilestoneFeatures(path, 3);
}
```

### T10 — `LifestylePicker` dialog

**Files:** New `src/Features/Lifestyles/LifestylePicker.cs`.

**Concrete:** When `OnTierChanged` reaches T2 with no path picked, fire `MultiSelectionInquiryData`:

```csharp
public static void OpenPicker()
{
    var elements = new List<InquiryElement>
    {
        new InquiryElement("forager", "Forager — wilderness self-reliance, scouting, survival", null),
        new InquiryElement("tactician", "Tactician — command, strategy, formation", null),
        new InquiryElement("diplomat", "Diplomat — social, connections, court", null)
    };
    
    var inquiry = new MultiSelectionInquiryData(
        "Choose Your Path",
        "You've reached a turning point. The path you choose now will shape the rest of your career.",
        elements,
        isExitShown: false,  // Force a choice
        minSelectableOptionCount: 1,
        maxSelectableOptionCount: 1,
        affirmativeText: "Commit",
        negativeText: "",
        affirmativeAction: list =>
        {
            var path = list[0].Identifier as string;
            FlagStore.Instance?.SetString("lifestyle.path", path);
            LifestyleUnlockStore.Instance?.UnlockMilestoneFeatures(path, 1);  // T2 milestone immediately
        },
        negativeAction: null);
    
    MBInformationManager.ShowMultiSelectionInquiry(inquiry, true);
}
```

### T11 — Wire picker into OnTierChanged hook

**Goal:** When OnTierChanged reaches T2 with no path picked, fire picker BEFORE milestone unlock attempt.

**Files:** Edit `LifestyleMilestoneHook.cs` to call `LifestylePicker.OpenPicker` if no path; otherwise unlock milestones.

### T12 — Cross-system gating audit + integration

**Goal:** Audit existing storylets, menu options, dialog branches across Plans 2-6 for opportunities to add lifestyle unlock gating.

**Examples to add:**
- Endeavor template `endeavor.scouting.deep_recon` adds `requires_lifestyle: ["forager.deep_recon"]`.
- Camp menu Forage option (new in T13 below) condition: `LifestyleUnlockStore.IsUnlocked("forager.wild_provisions")`.
- Patron favor cooldown calculation reads `IsUnlocked("diplomat.kingsmaker")` and applies multiplier.

### T13 — Add Camp menu Forage option (Forager unlock)

**Goal:** When `forager.wild_provisions` is unlocked, add a Camp menu option "Forage in the wilderness" — triggers a single-phase endeavor.

**Files:** Edit `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` Camp hub registration.

### T14 — Add Battle Briefing dialog (Tactician unlock)

**Goal:** When `tactician.battle_brief` unlocked, dialog with lord before any battle adds tactical-input branch.

### T15 — Add Lord Courtesy passive (Diplomat unlock)

**Goal:** When `diplomat.courtesy` unlocked, +5 relation per notable interaction (passive). Hook notable-interaction events.

### T16 — Twelve mechanic golden-path scenarios documented

**Files:** Create `docs/Features/SmokeTests/golden-paths.md` with 12 scenarios:

1. T1 enlist → Sergeant spawns → talk to Sergeant → confirm dialog
2. T1→T2 ceremony fires correctly
3. T3 promotion → Field Medic + Pathfinder spawn → assign Field Medic as Surgeon → verify aptitude flows
4. T6→T7 ceremony three-path (Plan 3 T19 already smoke; reverify)
5. Officer gear application at T7 (cape, banner, weapon modifier)
6. Officer Tent +6 HP/day verified
7. Endeavor (Soldier drill competition) end-to-end (Plan 5 T29 reverify)
8. Endeavor (Rogue dice game) — scrutiny accumulates
9. Patron audience flow — call in gold loan favor
10. Lifestyle picker fires at T2; pick Forager
11. Forager T4 milestone unlocks; verify endeavor scrutiny reduction
12. Personal Kit Bedroll L3 + Field Kit L3 + Sharpening Stone L3 — verify all three bonuses simultaneously active

Each scenario: setup, steps, expected outcome, pass/fail criteria.

### T17 — Cross-mechanic interaction matrix executed

**Goal:** Run the 8 interaction scenarios from §5. Document results.

### T18 — Phase 21 validator (lifestyle catalog)

**Goal:** Add Phase 21 to `validate_content.py` validating lifestyle catalog schema + cross-references.

### T19 — Numeric tuning pass

**Goal:** Review numeric values per §5 numeric tuning smoke recipe. Document any "off" values + tune.

### T20 — Stretch: cultural variants for endeavors (deferred from Plan 5)

**Goal:** If Plan 5 deferred cultural variants for endeavors per its §10 hand-off, Plan 7 polish authors them now. Otherwise skip.

### T21 — Stretch: news-feed integration

**Goal:** If news-v2 substrate has shipped (separate spec) by Plan 7 timing, integrate patron deaths + ceremony outcomes + endeavor resolutions into news feed. If not, leave as direct-to-news fallback.

### T22 — Plan 7 final verification report (closes 7-plan family)

**Goal:** Single comprehensive report covering:
- Plan 7 task completion (T1-T21)
- All 12 golden-path scenarios pass/fail
- All 8 interaction matrix scenarios pass/fail
- Numeric tuning notes
- Outstanding follow-ups for future polish
- Sign-off on the entire 7-plan family

**Files:** New `docs/superpowers/plans/2026-04-24-ck3-wanderer-polish-smoke-tuning-verification.md`.

**Final action:** Update CLAUDE.md current-status block with completion record for all 7 plans.

---

## §7 — Risks

### Risk M1 — Lifestyle picker timing conflict with ceremony (MEDIUM)

**Vector:** T1→T2 ceremony fires + lifestyle picker would also fire at T2. Modal stack.

**Mitigation:**
- T11 explicitly orders: ceremony fires first (Plan 3), THEN lifestyle picker (after ceremony modal closes).
- Use `NextFrameDispatcher.RunNextFrame` to defer picker until after ceremony.

### Risk M2 — Numeric tuning depends on playtest data (MEDIUM)

**Vector:** AI can't tune what hasn't been playtested. T19 may surface that a value is wrong but lacks data to fix.

**Mitigation:**
- T19 documents the issue; defers actual tuning to follow-up if needed.
- Per spec §0, we ship with tunable defaults; tuning is iterative.

### Risk L1 — Cross-system gating combinatorial explosion (LOW)

**Vector:** T12 audit may surface dozens of small lifestyle-unlock integration points across Plans 2-6.

**Mitigation:**
- Prioritize the obvious ones (those listed in §4.2 features); defer the rest to future polish.

---

## §8 — Verification gates

### Personal Kit
- [ ] All 9 catalog entries load
- [ ] Bedroll bonuses (food variety) verified at L1/L2/L3
- [ ] Field Kit bonuses (HP/day) verified
- [ ] Sharpening Stone passive XP verified
- [ ] QM purchase flow works
- [ ] Save-load round-trip preserves levels

### Lifestyle Unlocks
- [ ] All 3 paths × 9 features (27 total) load
- [ ] Lifestyle picker fires at T2
- [ ] T2 milestone unlocks 3 features
- [ ] T4 + T6 milestones unlock 3 + 3 features cumulatively
- [ ] Path locked after picking
- [ ] Cross-system gating works (endeavor + menu + dialog conditions)

### Smoke + Tuning
- [ ] All 12 golden-path scenarios pass
- [ ] All 8 interaction matrix scenarios pass
- [ ] Numeric tuning notes documented (T19)
- [ ] Phase 21 validator populated and passes
- [ ] All 7 plans' verification reports referenced and confirmed

### Integration
- [ ] Build clean
- [ ] All validators (Phases 1-21) pass
- [ ] `Tools/Validation/lint_repo.ps1` passes
- [ ] Save-load round-trip 4x with no errors
- [ ] Final verification report committed
- [ ] CLAUDE.md current-status block updated to ✅ all 7 plans shipped

---

## §9 — Definition of done

Plan 7 complete (and the entire 7-plan family complete) when:
1. All 22 Plan 7 tasks ✅
2. All §8 verification gates pass
3. Final verification report committed
4. CLAUDE.md updated
5. The mod surface is the full v6 spec realized

---

## §10 — Hand-off (post-7-plan)

The 7-plan family is the last formal effort for this spec. Future work:
- Continued numeric tuning based on playtest feedback (small Edits, no new plans needed)
- Additional cultural variants per ceremony / endeavor / lifestyle (content-only)
- Custom 3D mesh assets if budget allows
- News-feed integration when news-v2 substrate ships (separate spec)
- Cross-spec integration with menu+duty unification (separate spec, separate plan family)

No more wanderer-spec plans expected. If significant new design emerges, draft a new spec + new plan family.

---

## §11 — Out of scope (explicitly NOT in Plan 7 — final)

- Lifestyle Perks (PerkObject path) — locked excluded per spec §4.5 do-not list
- Order migration (sibling not merge — locked)
- News-v2 substrate (separate spec)
- Menu+duty unification design (separate spec)
- Post-retirement gameplay (mod silences per §0 scoping)

---

## §12 — References

- Plans 1-6 + verification reports
- Spec v6 §3.2 + §3.6 + §0 + §4.5
- AGENTS.md / CLAUDE.md (in particular known issues #4, #14)
- Existing `PersonalKitTickHandler`, `LifestyleUnlockStore`, `QualityStore`, `FlagStore`, `EnlistmentBehavior`, `EnlistedMenuBehavior`, `EnlistedDialogManager`
- Decompile: `DefaultPartyHealingModel`, `DefaultPartyMoraleModel`, `HeroDeveloper`, `MultiSelectionInquiryData`
