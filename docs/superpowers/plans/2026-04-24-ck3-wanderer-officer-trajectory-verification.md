# Plan 4 — CK3 Wanderer Officer Trajectory: Verification Report

**Status:** 🟡 Code-level verification complete; in-game manual smoke pending human operator (T20 scenarios A-F below).

**Plan:** [2026-04-24-ck3-wanderer-officer-trajectory.md](2026-04-24-ck3-wanderer-officer-trajectory.md)
**Brief:** [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md)
**Branch:** `feature/plan4-officer-trajectory` (commits `f9e6ca6` → `e362110`, branched from Plan 3 ship `b222ce5`)
**Date:** 2026-04-26

---

## §1 — What shipped

### Five phase commits

| Phase | Commit | Subject | Files |
| :-: | :-- | :-- | :-: |
| 0 | `f9e6ca6` | docs(plan4): pre-execution audit + game-design decisions | 1 |
| 1 | `2f9ee34` | feat(officer): Plan 4 Phase 1 — modifier factory + bootstrap registration | 6 |
| 2 | `2561034` | feat(officer): Plan 4 Phase 2 — gear application on tier change | 7 |
| 3 | `4a799d9` | feat(officer): Plan 4 Phase 3 — Harmony patches for officer-tier passive buffs | 5 |
| 4 | `30dd3e6` | feat(officer): Plan 4 Phase 4 — officer-tier dialog branches + tokens | 2 |
| 5 | `e362110` | feat(officer): Plan 4 Phase 5 — Inspect officer's tent Camp menu | 1 |

### New files (8 C# + 1 JSON + 1 verification doc)

**Officer feature (5)**
- `src/Features/Officer/ItemModifierFactory.cs` — property-setter reflection helper for runtime `ItemModifier` + `ItemModifierGroup` registration; `GetOrCreate` idempotent across reloads
- `src/Features/Officer/OfficerGearRegistry.cs` — registers `mod_lord_gifted` group + 18 modifiers (6 cultures × 3 tier bands) at `SubModule.OnGameStart` with static StringIds (`lord_gifted_<culture>_t<tier>`)
- `src/Features/Officer/PatronWeaponModifier.cs` — looks up the pre-registered modifier and applies to `Hero.MainHero.BattleEquipment[Weapon0]` at T7-T9 transitions
- `src/Features/Officer/CapeProgression.cs` — maps (culture variant suffix, tier band) to vanilla shoulder-armor `ItemObject` IDs via `cape_progression.json`
- `src/Features/Officer/BannerProvision.cs` — flag-only banner narrative (`officer_banner_t7/t8/t9` keys) + toast notification + accessors (`GetCurrentBannerName`, `GetCurrentBannerTier`)
- `src/Features/Officer/OfficerTrajectoryBehavior.cs` — `OnTierChanged` subscriber that fans out to the gear helpers and refreshes dialog tokens

**Harmony patches (3)**
- `src/Mod.GameAdapters/Patches/OfficerTentHealingPatch.cs` — postfix on `DefaultPartyHealingModel.GetDailyHealingHpForHeroes`, +6 HP/day for T7+ enlisted MainParty
- `src/Mod.GameAdapters/Patches/OfficerSurgeryPatch.cs` — postfix on `DefaultPartyHealingModel.GetSurgeryChance`, +0.15 (15%) when MainHero leads MainParty at T7+
- `src/Mod.GameAdapters/Patches/OfficerMessMoralePatch.cs` — postfix on the public `DefaultPartyMoraleModel.GetEffectivePartyMorale`, flat +2 morale when MainParty + T7+

**Content (1)**
- `ModuleData/Enlisted/Officer/cape_progression.json` — 9 cells (3 cultures × 3 tier bands) mapping to vanilla shoulder-armor IDs

**Documentation (1)**
- `docs/superpowers/plans/2026-04-24-ck3-wanderer-officer-trajectory-verification.md` — this file

### Edits (4)

- `Enlisted.csproj` — 9 new `<Compile Include>` entries (4 Officer + 3 Patches + 2 Phase 1 helpers); new `OfficerData` ItemGroup + matching `MakeDir` and `Copy` entries in the AfterBuild target
- `src/Mod.Entry/SubModule.cs` — `Features.Officer.OfficerGearRegistry.Initialize()` call at the top of the `campaignStarter` block; `OfficerTrajectoryBehavior` registered immediately after `RankCeremonyBehavior` (Lock 5 subscriber order); three `_ = typeof(...)` static refs for the new patches
- `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs` — extended `SetCommonDialogueVariables` with 4 new tokens (`IS_OFFICER`, `PLAYER_RANK_TITLE`, `BANNER_NAME`, `PATRON_NAME`); new public `RefreshOfficerTokens()`; 5 helpers (`IsOfficer`, `IsCurrentEnlistedLord`, `IsPeerOfficerInArmy`, `InSettlement`, plus inline ceremony-flag checks); 8-line officer dialog block authored in `AddOfficerDialogs(starter)` called from `OnSessionLaunched`
- `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` — Camp menu option `camp_hub_inspect_tent` at slot 8 (T7+ visibility gate); `OnInspectOfficerTentSelected` renders an `InformationManager.ShowInquiry` trophy view; `AppendCeremonyChoices` iterates `FlagStore.GlobalFlags` for resolved-tier ceremony picks

### Save-definer offsets

Plan 4 consumes **zero new save-definer offsets**. State lives in:
- Vanilla `Hero.BattleEquipment` (cape slot, weapon-modifier attachment via `EquipmentElement.ItemModifier`)
- Existing `FlagStore` (`officer_banner_t7/t8/t9` keys)
- `MBObjectManager`'s registry (the 18 static `ItemModifier`s + the `mod_lord_gifted` group, registered at `OnGameStart`)

The 18 `lord_gifted_<culture>_t<tier>` modifier StringIds are stable across reloads — the save's `EquipmentElement.ItemModifier` reference resolves correctly because `OfficerGearRegistry.Initialize` runs at `OnGameStart` BEFORE save-deserialize.

### Error-codes registry

`docs/error-codes.md` regenerated. The OFFICER category accumulates 13 codes from new `Surfaced` / `Caught` / `Expected` call sites across the 5 Officer C# files + 3 patches.

---

## §2 — Verification gates passed

- ✅ `dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64` — clean (0 warnings, 0 errors) after every phase commit.
- ✅ `python Tools/Validation/validate_content.py` — passes with 0 errors. Warning count rose from 37 (Plan 3 baseline) to 53 (+16): all are `warning_reference` from new inline `{=key}Fallback` loc-keys in C# that haven't been integrated into `enlisted_strings.xml`. Per project convention, missing keys fall back to the inline English text — zero runtime impact, only translators see English.
- ✅ All new C# files CRLF-normalized via `Tools/normalize_crlf.ps1` (each file run exactly once per the script's known double-BOM bug).
- ✅ `OfficerTrajectoryBehavior` registered AFTER `RankCeremonyBehavior` in `SubModule.cs` — Lock 5 subscriber-order requirement satisfied. Verified at `SubModule.cs:388-391`.
- ✅ Static modifier StringIds confirmed unique vs vanilla: grep of `Modules/Native/ModuleData/item_modifiers.xml` and `Modules/SandBox/ModuleData/item_modifiers.xml` shows zero `lord_gifted_*` collisions. (Lock 14 satisfied without authoring a Phase 21 validator — the manual check covers the v1 ship.)
- ✅ `OfficerGearRegistry.Initialize` is idempotent: `GetOrCreate` short-circuits on existing StringId via `MBObjectManager.GetObject<T>` lookup. Verified at code-read time; safe across reloads.
- ✅ `MBObjectBase.StringId { get; set; }` is publicly settable (decompile `TaleWorlds.ObjectSystem/MBObjectBase.cs:12`); reflection only needed for `ItemModifier`'s auto-properties (`Damage`, `Speed`, etc.).
- ✅ `ItemModifierGroup(string id)` public constructor exists (decompile `TaleWorlds.Core/ItemModifierGroup.cs:38`). `AddItemModifier` is public.
- ✅ Patch target signatures verified at decompile:
  - `GetDailyHealingHpForHeroes(PartyBase party, bool isPrisoners, bool includeDescriptions = false) → ExplainedNumber` ✓
  - `GetSurgeryChance(PartyBase party) → float` ✓ (Lock 9 — NOT ExplainedNumber)
  - `GetEffectivePartyMorale(MobileParty mobileParty, bool includeDescription = false) → ExplainedNumber` ✓ (Lock 10 — public, not the private `CalculateFoodVarietyMoraleBonus`)
- ✅ No conflict with existing patches: 37 patches in `src/Mod.GameAdapters/Patches/`, none target the three vanilla model methods Plan 4 hooks. Multiple Harmony patches on the same method are allowed (sequential application order); v1 ships first-mover for these methods.
- ✅ Dialog conditions consume `FlagStore.Instance.Has("ceremony_choice_t7_*")` per Lock 2 — flat underscore, bool-only flags. No `GetString` call (which doesn't exist on `FlagStore`).
- ✅ Camp menu slot index 8 unique vs Plan 2 slot 6 ("Talk to companion"), slot 7 ("My Lord"), slot 100 (Back) — verified at `EnlistedMenuBehavior.cs:1394, 1413, 1417`.
- ✅ All 8 cape IDs in `cape_progression.json` confirmed `Type="Cape"` in vanilla `Modules/SandBoxCore/ModuleData/items/shoulder_armors.xml`: `southern_shawl`, `battania_cloak`, `battania_cloak_furr`, `a_khuzait_leather_shoulder_b_a`, `a_khuzait_leather_shoulder_b_b`, `a_empire_plated_shoulder_a`, `a_empire_plated_shoulder_b`, `leopard_pelt`. The slot assignment to `EquipmentIndex.Cape` is type-compatible for every mapped item.

---

## §3 — Pending: in-game manual smoke

Build + validator gates can't cover the runtime modifier-render pipeline, save-load round-trips, dialog-line eligibility evaluation, or Camp menu rendering. A human operator must run the scenarios below.

> **⚠️ Run Scenario F (save-load) FIRST.** It tests the most foundational assumption in Plan 4: that `OfficerGearRegistry.Initialize()` running at `SubModule.OnGameStart` fires BEFORE save-deserialize, so saved `Hero.BattleEquipment.ItemModifier` references resolve. The timing was reasoned from the `OnGameStart` doc comment ("called when a new game starts or when loading a save game"); empirical confirmation is pending. If Scenario F fails (loaded weapon shows null modifier despite save-time having one), every other scenario builds on a broken foundation — the registration call must move to `OnSubModuleLoad` or into a marker behavior that runs before any other behavior's `SyncData(load)`.

### Known cross-cutting issue (out of Plan 4 scope)

`ceremony_choice_t7_humble_accept` (and the proud / try-to-refuse variants) persist in `FlagStore.GlobalFlags` across discharge and re-enlist. A player who picks "humble" with Lord A, discharges, re-enlists with Lord B, and gets promoted to T7 with Lord B's ceremony will see Lord B's officer-tier dialog still colored by Lord A's prior commission choice. Plan 3's T7 ceremony probably re-sets the flag at the new commission, so the flags compound rather than cleanly replace. This is a real lore inconsistency but it's outside Plan 4's scope — a future plan owning the enlistment-lifecycle policy (Plan 7 polish or a dedicated cross-cutting fix) should clear `ceremony_choice_*` flags on discharge.

### Scenario A — Phase 1+2 gear application at T7 commission

1. Build clean. Launch game.
2. Enlist with a **Vlandian** lord (so culture variant selection produces `lord_gifted_vlandian_t7` and `cape_progression.json` resolves to the Vlandian-bucket items).
3. Wait or use Debug Tools to bring the player to T7 (Plan 3 ceremony fires; player accepts).
4. Verify after ceremony resolves:
   - **Toast 1**: "Lord <name> has gifted you a finer blade."
   - **Toast 2**: "Lord <name> has granted you the Cavalier's Standard."
   - Open inventory: `Hero.MainHero.BattleEquipment[Weapon0]` shows the modifier — tooltip reads `"<Lord-Name>'s Fine Steel <weapon-name>"`. **Confirm the `{LORD_NAME}` token resolved to the actual lord's name** (not literal `{LORD_NAME}`).
   - Cape slot (`EquipmentIndex.Cape = 9`) holds the Vlandian-bucket officer cape (`battania_cloak_furr` per current placeholder mapping; v1 cosmetic stub per Lock 4).
   - `FlagStore.Instance.Has("officer_banner_t7")` is true.

### Scenario B — Tier 8/9 progression scaling

1. Continue from Scenario A. Force tier 8.
2. Verify: weapon modifier replaced with `lord_gifted_vlandian_t8` (tooltip stat-line shows +5 damage, +2 speed). Banner flag flipped to `officer_banner_t8`, toast "Commander's Standard". Cape unchanged (T8 is still in the "officer" tier band).
3. Force tier 9. Verify: modifier is now `lord_gifted_vlandian_t9` (+8 damage, +3 speed). Banner is `officer_banner_t9`, "Marshal's Standard".

### Scenario C — Phase 3 passive buffs (T7+ Harmony patches)

1. At T7+, wound `Hero.MainHero` to 50/100 HP.
2. Wait 1 in-game day. Confirm HP healing rate breakdown shows "+6 from Officer's Tent" (open the party-hero tooltip).
3. Trigger a battle severe enough to roll surgery. Compare survival rate to a sub-T7 baseline save: T7+ should be 0.15 above baseline.
4. Open the morale-summary tooltip on the main party. Confirm "+2 from Officer's Mess" appears in the breakdown.

### Scenario D — Phase 4 dialog branches

1. At T7+ enlistment with Vlandian lord, ceremony_choice_t7_humble_accept set:
   - Initiate conversation with the enlisted lord. Expected greeting: "A humble officer — the men follow you for it." (`officer_lord_humble_t7` at priority 113 wins over `officer_lord_greet` at 112.)
2. Reload save where ceremony_choice_t7_proud_accept set instead: greeting changes to "You earned the rank, <name>. I see that."
3. Reload save where ceremony_choice_t7_try_to_refuse set: greeting changes to "You doubted yourself, <name>. Yet here you are."
4. Visit a town. Talk to the innkeeper: greeting reads "Good evening, <rank-title>. The night's quiet — sit, eat." (priority 115 at "start" input).
5. Talk to a notable in a Vlandian (= enlisted lord's culture) settlement: banner-hook line "I've seen your standard pass through. Good men under it." (priority 114 beats the generic notable greeting at 113).
6. Find a peer lord in the same army as your enlisted lord. Open conversation: "Captain. Word in the army says you're holding the line." (priority 111).

### Scenario E — Phase 5 Inspect officer's tent Camp menu

1. At sub-T7, open Camp menu: confirm "Inspect your officer's tent" option is **NOT visible** (option's condition returns `false` for sub-officers, hiding the entry per the existing pattern).
2. At T7+, open Camp menu: option is visible at slot 8 (between "My Lord..." at 7 and "Back" at 100).
3. Select the option. Verify the popup shows:
   - Rank line: "You bear the title of <rank>, sworn to <lord-name>."
   - Banner line: "Your standard: Cavalier's Standard." (or T8/T9 equivalent if tier higher)
   - Blade line: "A blade marked: <Lord-Name>'s Fine Steel."
   - Relation line: "Standing with <lord-name>: <number>."
   - Ceremony list: one bullet per resolved tier, e.g. "• Tier 2: trust sergeant", "• Tier 7: humble accept".
4. Click Close. Menu returns to camp hub without side effects.

### Scenario F — Save-load round-trip

1. At T7+ with all gear applied, save game.
2. Quit to main menu. Reload save.
3. Verify:
   - `Hero.MainHero.BattleEquipment[Weapon0].ItemModifier` is non-null and has the expected StringId (`lord_gifted_<culture>_t7`). **Critical: verifies the static-StringId-at-OnGameStart timing fix (Lock 8) actually prevents null-modifier corruption.**
   - Cape slot still holds the assigned cape ItemObject.
   - `FlagStore.Instance.Has("officer_banner_t7")` is still true.
   - The 3 Harmony patches still apply on the next daily tick.
   - Camp menu Inspect tent option visible; trophy view renders correctly.

---

## §4 — Deviations from plan as written

The plan v1 prescribes 20 sequential tasks. Effective task count after locks + execution-time scope adjustments: **17** (T1 dropped, T13 swapped, T14/T15/T19 dropped, T2 split into T2 + T2.5).

| Plan task | Status | Note |
| :-- | :-- | :-- |
| T1 — design doc | ❌ **DROPPED** | Locks block at the top of the plan documents per-system deltas exhaustively; authoring a separate `officer-trajectory-design.md` before code shipped would have been a stub of the locks, while authoring at T20 time describes what actually shipped. Decision per advisor — fold any living-reference content into this verification doc (§1 covers the system breakdown). |
| T2 — `ItemModifierFactory` | shipped | Property-setter reflection per Lock 1 (auto-properties confirmed at decompile). |
| T2.5 — bootstrap registration | shipped | New sub-task split out per Lock 8 — registration must happen at `SubModule.OnGameStart`, not `OnSessionLaunched`, so `Hero.BattleEquipment` ItemModifier StringIds resolve during save-deserialize. |
| T3 — `PatronWeaponModifier` | shipped | Static StringId lookup; `{LORD_NAME}` global token via `MBTextManager.SetTextVariable`. T7-T9 all-tier scaling (plan §T3 wrote "newTier != 7"; Phase 2 ships scaling per §4.2 modifier table). |
| T4 — `CapeProgression` | shipped | 9 cells (3 cultures × 3 bands) per Lock 4, mapping to **existing vanilla shoulder-armor IDs** — no new ItemObject XML authoring. Vlandian bucket uses Battanian items as v1 placeholder (vanilla `shoulder_armors.xml` has no Vlandia or Sturgia variants); polish in Plan 7 if desired. |
| T5-T6 — Banner provision | shipped, **simplified** | Lock 6 said "inventory items"; execution-time decision shipped flag-only narrative (`officer_banner_t7/t8/t9`) instead. Vanilla has no banner ItemObjects to reuse, and authoring new banner items with `BannerComponent` is full asset authoring deferred to Plan 7. Flag + toast + dialog-hook BANNER_NAME token deliver the narrative payoff. |
| T7 — `OfficerTrajectoryBehavior` | shipped | Subscribes `OnTierChanged` with `-=`/`+=` save-load duplicate-guard pattern. Per-helper try/catch so one failure doesn't cascade. Calls `EnlistedDialogManager.RefreshOfficerTokens` at end. |
| T8 — Dialog gating helpers | shipped | All as `private static` in `EnlistedDialogManager` (per advisor recommendation 2 — keep tightly coupled to dialog logic; promote later if Plans 5-7 need broader access). Five helpers: `IsOfficer`, `IsCurrentEnlistedLord`, `IsPeerOfficerInArmy`, `SameFactionLord` (inline at use site), `InSettlement`. |
| T9 — `OfficerTentHealingPatch` | shipped | `ref ExplainedNumber __result`, `__result.Add(6f, ...)` — Add-line shows in the tooltip breakdown. |
| T10 — `OfficerSurgeryPatch` | shipped | **`ref float __result`** per Lock 9 (plan body example wrong — would not compile). `__result += 0.15f`. No tooltip surface. |
| T11 — `OfficerMessMoralePatch` | shipped | Postfix on **public** `GetEffectivePartyMorale` per Lock 10 (not the private `CalculateFoodVarietyMoraleBonus` — string-name targeting fragile). Flat +2 per design call (a). |
| T12 — Subscriber order | shipped | `OfficerTrajectoryBehavior` registered AFTER `RankCeremonyBehavior` in `SubModule.OnGameStart`. NextFrameDispatcher dropped per Lock 5. |
| **T13 — Rear-formation offset** | ❌ **DROPPED** | Lock 11. Verified at `EnlistedFormationAssignmentBehavior.cs:704` doc comment "Only applies to T1-T6 soldiers. T7+ commanders control their own party and spawn position" and lines 727-731 explicit early-return. Sub-T7 already gets `var behindOffset = -formationDirection.ToVec3() * 5f` at line 778. Forcing T7+ rearward conflicted with established commander-tier design. |
| **T13 (NEW) — Inspect officer's tent** | shipped | Game-design scope-add replacing the dropped rear-formation T13. One Camp menu line at slot 8 (T7+ only) opens an `InformationManager.ShowInquiry` listing rank, banner, gifted blade, lord relation, and resolved ceremony choices. Pure narrative; no gameplay effect. |
| **T14, T15** | ❌ **DROPPED** | Were reserved for T13 follow-up work. |
| T16 — `SetCommonDialogueVariables` extension | shipped, **timing revised** | Lock 12 proposed hooking `CampaignEvents.ConversationStarted`. That event does **not** exist in TaleWorlds (only `ConversationEnded` does — verified). Revised at execution time: state-driven token refresh. Tokens seeded once at `OnSessionLaunched`; refreshed via `RefreshOfficerTokens` from `OfficerTrajectoryBehavior.OnTierChanged`. PATRON_NAME left empty until Plan 6 ships. |
| T17 — Rank-gated dialog branches | shipped | 4 lines of the original §4.7 table: `officer_inn_greet` (115), `officer_notable_greet` (113), `officer_banner_notable_hook` (114, Lock 13), `officer_lord_greet` (112) + 3 ceremony-flag variants (Lock 2). |
| T18 — Peer-officer dialog tree | shipped, **simplified** | Plan §T18 prescribed 5+ lines for tactical/morale/political/back/init multi-stage tree. v1 ships **one** flavor recognition line `officer_peer_chat` (priority 111). Multi-stage tree deferred to Plan 7 polish — limited narrative payoff per authoring cost for v1. |
| **T19 — Validator phase** | ❌ **DROPPED** | Lock 14. Phase 12 of `validate_content.py` scans JSON `apply` IDs in storylet/effects content. Runtime-registered `ItemModifier`s are not in JSON. Manual grep of vanilla `item_modifiers.xml` confirmed zero `lord_gifted_*` collisions; sufficient for ship without authoring a Phase 21. |
| T20 — Verification report | this file | 🟡 mirroring Plan 3's verification format. Closes to ✅ when human operator signs off on Scenarios A-F. |

---

## §5 — Architecture brief compliance

Cross-checked against [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md):

- **§1 (save-definer offsets)** — Plan 4 adds **zero new offsets**. Modifier registry persists in `MBObjectManager`, banner state in `FlagStore`, gear in vanilla `Hero.BattleEquipment`. Brief reservation note "Plan 4's `DutyCooldownStore` holds 50" refers to the **Career Loop family**, not this plan; no collision.
- **§2 (namespace conventions)** — Plan 4 introduces `Enlisted.Features.Officer` (8 new types). The brief's table did not list this namespace explicitly (officer trajectory was implied under `Enlisted.Features.Ceremonies` in early drafts); the hand-off-surface table below lists the new types. No collision with existing namespaces.
- **§3 (dialog token prefixes + interpolation contract)** — All 8 new dialog lines reference `{PLAYER_NAME}`, `{PLAYER_RANK_TITLE}`, `{LORD_NAME}`, `{BANNER_NAME}` per the locked contract. Tokens populated by `SetCommonDialogueVariables` at `OnSessionLaunched` and refreshed via `RefreshOfficerTokens` on tier transition. No literal "soldier" / "captain" / "the lord" hardcoded.
- **§4 (schema rules)** — `cape_progression.json` uses `schemaVersion: 1`. No `HashSet<T>` or read-only quality writes. Flag keys (`officer_banner_t7/t8/t9`, `ceremony_choice_t7_*`) follow flat-underscore convention.
- **§5 ("do not" list)** — No vanilla TaleWorlds type re-registered; no `Occupation.Wanderer` heroes; no `Campaign.Current.X` deref at registration; no `HashSet<T>` save state; no read-only QualityStore writes; no `int.MinValue` sentinels; no `EventDeliveryManager` direct calls (no modal pipeline use in Plan 4); no `Hero.HeroDeveloper.AddPerk`; no `OrderActivity` migration.
- **§6 (modal pipeline + OnTierChanged consumers)** — Plan 4's `OfficerTrajectoryBehavior` is the **fourth** subscriber to `OnTierChanged` (after `PathScorer`, `PathCrossroadsBehavior`, `RankCeremonyBehavior`). Subscribed AFTER ceremony per Lock 5. No modal storylet emissions from Plan 4 (gear application is non-modal toast notifications via `InformationManager.DisplayMessage`).

---

## §6 — Hand-off to Plans 5-7

Plan 4 ships the following stable surface for downstream plans:

### Public APIs

| Symbol | Purpose | Use case |
| :-- | :-- | :-- |
| `OfficerGearRegistry.GetModifierStringId(string cultureId, int tier)` | Static lookup helper returning `lord_gifted_<culture>_t<tier>` for tier 7-9 | Plan 6 patron favors may grant additional weapon modifiers; reuse the StringId pattern |
| `BannerProvision.GetCurrentBannerName()` / `GetCurrentBannerTier()` | Reads `officer_banner_t7/t8/t9` flag state | Plans 5-7 may flavor endeavors / favors / lifestyle unlocks on banner rank |
| `EnlistedDialogManager.Instance.RefreshOfficerTokens()` | Re-seeds IS_OFFICER / PLAYER_RANK_TITLE / BANNER_NAME / PATRON_NAME tokens | Plans 5-7 firing modal dialogs after state changes can refresh tokens before opening a conversation |
| `FlagStore.Has("officer_banner_t<N>")` | Direct flag check (3 keys) | Same use cases as above; flag-level read for content gating |

### Stable conventions

- 18 static `ItemModifier` StringIds (`lord_gifted_<culture>_t<tier>`, cultures = vlandia/sturgia/empire/aserai/battania/khuzait, tiers = 7/8/9) registered at `SubModule.OnGameStart` — survives save-load
- `mod_lord_gifted` `ItemModifierGroup` registered alongside; reusable by Plan 6 / Plan 7 if they want to add patron-favor modifiers in the same group
- 3 ceremony-choice flags read by Plan 4 dialogs: `ceremony_choice_t7_humble_accept`, `_proud_accept`, `_try_to_refuse` — Plans 5-7 may also read for additional flavor

### For Plan 5 (Endeavor System)

- Officer-tier endeavor categories may gate on a public `IsOfficer()` helper. Currently `private static` in `EnlistedDialogManager`; if Plan 5 needs broader access, promote to `Enlisted.Features.Officer.OfficerHelper.IsOfficer()` at that time.
- Endeavor success-rolls reading `Hero.GetRelation(EnlistedLord)` already see the patron-weapon-modifier `LORD_NAME` token-flow as flavor; nothing additional needed in Plan 4.

### For Plan 6 (Roll of Patrons)

- `PatronRoll.Has(MBGUID)` confirmed exists and stable (Lock 7); Plan 4 reads it but never sets it.
- `PATRON_NAME` text variable exposed via `SetCommonDialogueVariables` (currently empty placeholder). Plan 6 wires patron data into this when the Roll populates.
- Plan 4's Inspect-tent trophy view does NOT yet show patron-favor history; Plan 6 polish can extend `OnInspectOfficerTentSelected` / `AppendCeremonyChoices` with a parallel "Favors granted by {patron}: ..." section.

### For Plan 7 (Polish + Personal Kit)

- Cape progression mapping uses **placeholder vanilla items**. Polish pass should swap in per-culture authored items if a custom asset budget appears. Mapping lives in `cape_progression.json` so JSON-only polish is possible (no code change needed).
- Banner shipped as flag-only. Plan 7 may upgrade to a real `BannerComponent` ItemObject + equip slot, granting actual `BannerEffect` aura on the field. The `BannerProvision.GetCurrentBannerTier()` accessor is already shape-compatible with reading off an equipped banner instead of a flag.
- Multi-stage peer-officer dialog tree (5+ lines) authored as polish if playtest validates v1's one-line peer recognition leaves narrative gap.
- 8 inline loc-keys not yet integrated to `enlisted_strings.xml` — translator polish task. Game uses inline English fallback today.

---

## §7 — Sign-off

Plan 4 is ✅ once a human operator runs:

- [ ] Scenario A — Phase 1+2 gear application at T7 commission (Vlandian path)
- [ ] Scenario B — Tier 8/9 progression scaling (modifier replace, banner flag flip, cape unchanged)
- [ ] Scenario C — Phase 3 passive buffs (HP/day, surgery survival, morale tooltip)
- [ ] Scenario D — Phase 4 dialog branches (3 ceremony-flavor variants × innkeeper × notable banner-hook × peer-officer recognition)
- [ ] Scenario E — Phase 5 Inspect officer's tent Camp menu (visibility, content, no-op close)
- [ ] Scenario F — Save-load round-trip preserving gear state
- [ ] Repeat Scenarios A+D for each non-Vlandian culture (Sturgian, Imperial, Aserai, Battanian, Khuzait — verifies the culture-fallback table in `cape_progression.json` and `OfficerGearRegistry.GetModifierStringId`)

Until then, status remains 🟡. Plans 5-7 may begin against this substrate — the public API surface is stable.

---

## §8 — References

- [Plan 4 — Officer Trajectory](2026-04-24-ck3-wanderer-officer-trajectory.md) — owning plan (locks 1-14 at top)
- [Plan 1 verification](2026-04-24-ck3-wanderer-architecture-foundation-verification.md)
- [Plan 2 verification](2026-04-24-ck3-wanderer-companion-substrate-verification.md)
- [Plan 3 verification](2026-04-24-ck3-wanderer-rank-ceremony-arc-verification.md)
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)
- [Spec v6 §3.7 + §6.1 + §6.2](../specs/2026-04-24-ck3-wanderer-systems-analysis.md) — design source
- [AGENTS.md](../../../AGENTS.md)
- [CLAUDE.md](../../../CLAUDE.md) (Plan 4 status entry pending update under Current project status)
