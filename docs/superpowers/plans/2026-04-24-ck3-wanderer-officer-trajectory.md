# Plan 4 — CK3 Wanderer Mechanics: Officer Trajectory

**Status:** Draft v1 (2026-04-24). Fourth of seven plans implementing the [CK3 Wanderer Mechanics Systems Analysis (v6)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md). See spec §8 for the full plan structure.

**Scope:** When the player promotes from T6 to T7 (Captain), the texture of being a soldier shifts hard. Plan 4 ships: patron-named weapon modifier, rank-escalating cape, banner item with `BannerComponent`, officer's tent healing model patch (+6 HP/day), Personal Surgeon survival modifier, food-variety bonus from officer mess, bodyguard rear-formation offset (smoke-gated as the riskiest piece), and rank-gated dialog branches at priority 110-115. **NO ceremonies (Plan 3 ships those), NO retinue troops (existing `RetinueManager` handles), NO endeavors / patrons / kit (later plans).**

**Estimated tasks:** 20. **Estimated effort:** 3-4 days with AI-driven implementation.

**Dependencies:** Plan 1 (architecture), Plan 2 (companions — Junior Officer + QM Officer spawn at T7), Plan 3 (T6→T7 ceremony storylet provides the narrative moment Plan 4's gear deltas bind to). All three must have verification reports shipped before Plan 4 begins.

---

## 🔒 LOCKED 2026-04-26 — readiness amendments (pre-execution)

This block consolidates the pre-execution readiness audit (Plan 4 was authored 2026-04-24 before Plans 2+3 shipped — drift accumulated) plus deeper research findings against the decompile and existing codebase, plus four game-design decisions locked after the synthesis. **Locks override the body of the plan where they conflict.** Same pattern as Plan 3.

### Audit-correction locks (1-7)

**Lock 1 — `ItemModifier` reflection target (BLOCKING for §T2).** The §T2 example uses field names `_damage`, `_speed`, `_armor`, `_priceMultiplier`, `_itemQuality`, `_name`, `_stringId`. **No such fields exist.** Decompile `TaleWorlds.Core/ItemModifier.cs:13-41` shows auto-properties with private setters (`public int Damage { get; private set; }`). Compiler-generated backing fields are named `<Damage>k__BackingField`-style; reflection on `_damage` finds nothing.

Use **property-setter reflection** (recommended): `typeof(ItemModifier).GetProperty(nameof(ItemModifier.Damage)).GetSetMethod(nonPublic: true).Invoke(mod, new object[] { value })`. Survives Bannerlord patches better than backing-field names; uses the same setter path as XML `Deserialize` (decompile lines 60-77). `MBObjectBase.StringId` is publicly settable (`{ get; set; }`) — assign directly, no reflection needed for it. See Lock 8 for the registration timing this binds to.

**Lock 2 — Ceremony flag references (BLOCKING for §T17 `lord_address_humble_or_proud`).** §4.7 says the row reads `ceremony.t7.choice` flag with dotted notation and string semantics. Wrong on three counts:
1. Notation is flat underscore (architecture brief §4 rule 6): `ceremony_choice_t7_*`, never `ceremony.t7.choice`.
2. `FlagStore` is bool-only (verified `src/Features/Flags/FlagStore.cs:44-106` — `Has`/`Set`/`Clear` only, no `GetString`).
3. Schema is one bool flag per option ID. Plan 3 ships these flags at T7:
   - `ceremony_choice_t7_humble_accept`
   - `ceremony_choice_t7_proud_accept`
   - `ceremony_choice_t7_try_to_refuse`
   - Plus the dedup `ceremony_fired_t7`.

§T17 `lord_address_humble_or_proud` becomes **three separate `AddDialogLine` calls** — one per ceremony flag — at priority 112. Cleaner than one branched condition; avoids the "which flag is set?" ambiguity.

**Lock 3 — `EnlistmentBehavior.cs` line references shifted (advisory).** §0 reference 17 says `AssignInitialEquipment` is at lines 11223-11248. Actual location after Plans 2+3 shipped: **line 11512** (Plan 2 added 24 SyncKey fields + 6 GetOrCreate methods around lines 8500-8700). Other line refs:
- `EnlistedFormationAssignmentBehavior.cs:705-892 TryTeleportPlayerToFormationPosition` → actual 709 ✓
- `EnlistedDialogManager.cs:1360-1386 SetCommonDialogueVariables` → exact match ✓
- `EnlistedDialogManager.cs:347-368` → exact match ✓

Implementer must grep-the-symbol per AGENTS.md pitfall #22.

**Lock 4 — Cape culture coverage: 3 cultures + fallback.** §T4 says "6 cultures × 3 tier bands = 18 ItemObjects." §T5-T6 banners say "3 cultures × 3 tiers = 9 banner ItemObjects" using Plan 3's fallback (Battania→Vlandian, Khuzait→Sturgian, Aserai→Imperial). **Lock: align capes to the banner approach — 3 cultures × 3 bands = 9 cape ItemObjects.** Reduces authoring 50% and keeps the cluster's culture-fallback strategy uniform across Plans 3-4. Update §T4 file count + the cape lookup helper to read culture via `CeremonyCultureSelector.SelectVariantSuffix()` (Plan 3's helper, free to reuse — listed in Plan 3 hand-off surface in the architecture brief).

**Lock 5 — §T12 visual-order timing claim is wrong (advisory).** §T12 reasons: "Since ceremony modal is a `MultiSelectionInquiryData` that pauses the game, the next frame doesn't fire until the player picks an option." Two issues:
1. Ceremony modals route through `EventDeliveryManager.QueueEvent` (per Plan 3 + Critical Rule #10), which does NOT pause the game — it queues an event for next-tick rendering through the `EventPopupViewModel` Gauntlet layer. Campaign tick continues; only the player's input is captured by the popup.
2. `InformationManager.DisplayMessage` (used for gear toasts) writes to the message log scroll, which renders behind/over Gauntlet popups regardless of modal state.

In practice: ceremony modal opens, gear toasts appear in the message log scroll behind it, player picks, modal closes, player sees the toasts. **This is acceptable UX** — modal owns attention, toasts wait peripherally. Implementation guidance:
- Register `OfficerTrajectoryBehavior` AFTER `RankCeremonyBehavior` in `SubModule.cs` so subscriber order puts ceremony emit before gear apply (defensive; effect is small).
- **Drop the `NextFrameDispatcher.RunNextFrame` complexity in §T12** — not needed.
- If playtest shows a real UX problem, the cleaner fix is to gate gear application on the `ceremony_fired_t7` flag being set (apply on next campaign tick after the player resolves the ceremony) — but ship without this and only add if smoke fails.

**Lock 6 — Banner items are inventory items in v1, not equipped (clarification + §1 correction).** §T5-T6 already states "Banners are NOT a standard `EquipmentIndex` slot... Plan 4 v1 ships banners as inventory items." Worth flagging that the §1 deliverable line "T7+: banner item with `BannerComponent` formation aura" overstates v1 — `BannerComponent.GetBannerEffectBonus` only fires on equipped banners (decompile confirms it reads from a Hero's equipped banner slot). Inventory-only banners give NO formation aura in v1.

**§1 correction:** the banner deliverable reads "T7+: banner item granted to inventory (cosmetic narrative; aura deferred to Plan 7 polish)". v1 ships narrative flavor ("Lord Crassus has granted you the Cavalier's Standard") without combat effect. The aura ships properly in Plan 7 polish if a real banner equip slot is implemented. **See Lock 13 for the dialog hooks that anchor the banner narratively in v1.**

**Lock 7 — `PatronRoll.Has(MBGUID)` exists from Plan 1 substrate (verification).** §T16 calls `PatronRoll.Instance?.Has(conversationTarget.Id)`. Verified at `src/Features/Patrons/PatronRoll.cs:37`. Plan 1 shipped the stub accessor. Returns false for all heroes until Plan 6 populates the roll, so `PATRON_NAME` is always empty until Plan 6 ships — that's expected. **No code change needed; this lock just records that the dependency is genuinely available even though Plan 6 isn't written yet.**

### Research-derived locks (8-12)

**Lock 8 — Save-load timing for runtime ItemModifier registration (BLOCKING, supersedes Plan §T2 footgun).** Saved `Hero.BattleEquipment` references ItemModifier StringIds. SaveSystem deserializes Hero state BEFORE `Behavior.OnSessionLaunched` fires. If modifiers register at OnSessionLaunched, the save-loaded equipment already has null modifier refs, and `MBObjectManager.RegisterObject` (decompile lines 213-243) suffixes a numeric on collision (`obj.StringId = text + num`) — re-registering session 2 with a different instance corrupts the saved reference.

**Fix:**
- Register modifiers at `SubModule.OnGameStart` (or earlier, e.g. `OnSubModuleLoad`) using a `GetOrCreate` pattern: `MBObjectManager.GetObject<ItemModifier>(stringId) ?? CreateAndRegister(...)`. Idempotent across reloads.
- Use **static StringIds**: `lord_gifted_<culture>_t<tier>` (e.g. `lord_gifted_vlandian_t7`). NO per-hero suffix — the save's reference is to an ID that always exists post-registration.
- Personalization (lord name) flows through the `Name` TextObject's `{LORD_NAME}` variable resolved at display time, not the StringId.
- 18 modifiers max: 6 cultures × 3 tier bands. (Lock 4 reduces capes to 9; modifiers stay 18 because the StringId encodes per-culture-per-tier and there's no fallback at registration time — the registry just exists, lookup fallback can happen via culture-mapping in `PatronWeaponModifier`.)
- The `mod_lord_gifted` `ItemModifierGroup` is created similarly: `MBObjectManager.GetObject<ItemModifierGroup>("mod_lord_gifted") ?? new ItemModifierGroup(...) → RegisterObject`.

**§T2 + §T7 restructure:**
- §T2 `ItemModifierFactory` ships the helper class but no caller-site registration.
- §T2.5 (new sub-task) ships the bootstrap registration in `SubModule.OnGameStart`: registers the group + 18 modifiers idempotently.
- §T7 `OfficerTrajectoryBehavior.OnTierChanged` does ONLY lookup-and-apply. No registration.

**Lock 9 — `GetSurgeryChance` returns `float`, not `ExplainedNumber` (BLOCKING for §T10).** Plan §T9-T11 examples show `ref ExplainedNumber __result` for all three patches. Decompile `Decompile/TaleWorlds.CampaignSystem.GameComponents/DefaultPartyHealingModel.cs:45-49` confirms: `public override float GetSurgeryChance(PartyBase party)`. T10 patch must use `ref float __result` and `__result += 0.15f`. Cannot pass a TextObject reason via `ExplainedNumber.Add` here; the surgery bonus won't appear in any tooltip — that's fine, surgery doesn't have a UI breakdown like ExplainedNumber-backed stats do.

**Lock 10 — T11 patches public `GetEffectivePartyMorale`, NOT private `CalculateFoodVarietyMoraleBonus` (BLOCKING for §T11).** The food-variety calc method is `private void CalculateFoodVarietyMoraleBonus(MobileParty party, ref ExplainedNumber result)` (decompile lines 63-130 of `DefaultPartyMoraleModel.cs`). Patching a private method requires string-name targeting (no `nameof`), more brittle. The public `GetEffectivePartyMorale(MobileParty mobileParty, bool includeDescription = false)` returns the final ExplainedNumber after the food-variety calc has been added; postfix on this is cleaner.

**Game-design decision (locked):** flat +2 morale via `__result.Add(2f, new TextObject("{=officer_mess_bonus}Officer's Mess"))` regardless of food state. Reading (a) per design verdict — legible tooltip ("+2 from Officer's Mess"), steady felt bonus. Reading (b) "+2 to variety count" produces inconsistent morale gains depending on current food state, which is invisible to the player.

**Lock 11 — T13 rear-formation offset DROPPED for v1 (BLOCKING design decision); replaced with "Inspect officer's tent" Camp menu.** Verified at `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:704` doc comment ("Only applies to T1-T6 soldiers. T7+ commanders control their own party and spawn position") and lines 727-731 explicit early-return for `EnlistmentTier >= RetinueManager.CommanderTier1`. Sub-T7 already gets `var behindOffset = -formationDirection.ToVec3() * 5f` at line 778. Plan §4.6 wants T7+ to also receive rear offset, but this conflicts with established commander-tier design AND brings real risk (off-map spawns, AI pathing breaks on small formations) for marginal payoff (officer survivability is already covered by Lock 9-10 patches).

**T13 (rear-formation) is a no-op task.** T14 + T15 (reserved for T13 follow-up) are dropped. T20 verification report documents: "rear-formation offset verified for T1-T6 enlisted soldiers via line 778 (pre-Plan-4); T7+ retains commander-tier spawn behavior; future Plan 7 polish may reassign T7+ players to a rear formation slot via `FormationArrangement` APIs if playtest demand surfaces."

**Replacement for the T13 slot (game-design scope-add): "Inspect your officer's tent" Camp menu line.** One Camp menu option visible only to T7+ officers. Selecting it shows `InformationManager.ShowInquiry` (or equivalent text-only popup) listing accumulated officer story:
- Current banner StringId + display name (looked up from `MobileParty.MainParty.ItemRoster`)
- Sword modifier display name (read from `Hero.MainHero.BattleEquipment[Weapon0].ItemModifier?.Name`)
- Current `Hero.MainHero.GetRelation(EnlistedLord)`
- Ceremony choices made — one line per resolved tier (read `ceremony_choice_t{N}_*` flags via `FlagStore.Instance.Has(...)` for tiers 2/3/5/7/8 if `ceremony_fired_t{N}` is set)

Zero gameplay effect; pure trophy/character-sheet view. ~50 lines of C# in `EnlistedMenuBehavior` (Camp section). Uses the existing native-menu pattern (per AGENTS.md "native menus over Gauntlet"). Reuses the dropped T13 task slot to keep the plan-task count balanced. Player-facing payoff: the T7+ promotion has a visible place where accumulated story lives, which is what makes a CK3-style trinket feel meaningful.

**Lock 12 — `SetCommonDialogueVariables` timing requires conversation-activation hook for §T16 (BLOCKING for §T17 `inn_greet_officer`).** `SetCommonDialogueVariables` currently fires only inside `LoadDialogueFromJson` parsing (lines 1494, 1533, 1574, 1672 of `EnlistedDialogManager.cs`) — i.e., inside QM-flow JSON processing. The vanilla `start` token used by `inn_greet_officer` fires BEFORE this. Without intervention, `{IS_OFFICER}` / `{PLAYER_RANK_TITLE}` / `{PATRON_NAME}` won't be populated when the innkeeper's officer-greeting line evaluates.

**Fix:** extend `EnlistedDialogManager` so `SetCommonDialogueVariables` runs on every conversation activation, not just inside QM-flow. Two options:
- **(a) Override `OnConversationActivated`** if `CampaignBehaviorBase` exposes that virtual.
- **(b) Subscribe to `CampaignEvents.ConversationStarted`** in `RegisterEvents` (canonical pattern; verify the event exists at decompile `TaleWorlds.CampaignSystem/CampaignEvents.cs`).

Implementer must verify which hook actually fires. **§T16 task expands to include this wiring.** Testing: enlisted at T7 enters an inn → tooltip-render of `{IS_OFFICER}` resolves to `"1"` and `{PLAYER_RANK_TITLE}` to `"Cavalier"` (or equivalent culture-rank).

### Game-design decision locks (13-14)

**Lock 13 — Banner narrative dialog hooks (game-design scope-add for §T17/T18).** Lock 6 ships banners as inventory items with no combat effect. To anchor the banner narratively without code changes elsewhere, T17 + T18 add three NPC references to the player's banner so it becomes a referenced token of identity rather than a forgotten inventory entry:

| Speaker | Input | Condition | Line (paraphrased; final wording in T17/T18) |
| :-- | :-- | :-- | :-- |
| Enlisted lord | `lord_pretalk` | `IsOfficer() && IsCurrentEnlistedLord(Hero.OneToOneConversationHero)` | "How fares the standard, captain? I trust you keep it close." |
| Peer officer | `officer_chat_hub` | `IsOfficer() && IsPeerOfficerInArmy(Hero.OneToOneConversationHero)` | "Banners count for something. Yours has been seen." |
| Notable in enlisted lord's culture | `notable_pretalk` | `IsOfficer() && Hero.OneToOneConversationHero?.IsNotable == true && Hero.OneToOneConversationHero?.Culture == EnlistedLord.Culture` | "I've seen your standard pass through. Good men under it." |

Cost: 3 dialog lines + 3 loc-keys synced via `sync_event_strings.py`. Optional `BANNER_NAME` token added to `SetCommonDialogueVariables` (T16) — populated from `MobileParty.MainParty.ItemRoster.FirstOrDefault(e => e.EquipmentElement.Item?.StringId?.StartsWith("banner_") == true)?.EquipmentElement.Item?.Name?.ToString() ?? "your standard"`.

**Lock 14 — T19 validator phase rescoped (advisory).** Plan §T19 says "extend Phase 12 to verify ItemModifier StringIds don't shadow vanilla." Phase 12 scans JSON `apply` IDs in storylet/effects content; runtime-registered ItemModifiers are NOT in JSON. T19 is rescoped or dropped:
- **(rescope)** Add a `Tools/Validation/check_modifier_collisions.py` (or Phase 21 in `validate_content.py`) that loads vanilla `Modules/Native/ModuleData/item_modifiers.xml` IDs and warns if any `lord_gifted_*_t*` collides with a vanilla one.
- **(drop)** Rely on `MBObjectManager`'s deduplicate-with-suffix behavior (decompile lines 222-234) — first-registration wins on idempotency. Document in T20 verification report that no static IDs collide with vanilla XML `lord_gifted_*` (none exist in vanilla — verified).

**Recommendation: drop T19** as the cheaper path; first-time Plan 4 ships, the implementer greps vanilla `item_modifiers.xml` once, confirms zero collision, and writes the result in T20.

### Revised task count

20 tasks → 17 effective:
- T2 split into T2 (factory) + T2.5 (bootstrap registration) — net +1
- T13 (rear-formation) → no-op; replaced by "Inspect officer's tent" Camp menu (still T13)
- T14, T15 dropped (were reserved for T13 follow-up)
- T19 dropped (or rescoped to one-line grep, ship-time only)

---

## §0 — Read these first

### Required prior plan documentation
1. **[Plan 1 — Architecture Foundation](2026-04-24-ck3-wanderer-architecture-foundation.md)** + verification report.
2. **[Plan 2 — Companion Substrate](2026-04-24-ck3-wanderer-companion-substrate.md)** + verification report. Plan 4 references Junior Officer + QM Officer spawned at T7.
3. **[Plan 3 — Rank-Ceremony Arc](2026-04-24-ck3-wanderer-rank-ceremony-arc.md)** + verification report. Plan 4 hooks `OnTierChanged` for gear application; T6→T7 ceremony narratively introduces the new banner/cape/sword.
4. **[Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)**.

### Required spec reading
5. **[Spec v6 §3.7 Officer Trajectory](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — design source. Concrete deltas across four subsystems (gear / health / command / dialog).
6. **[Spec v6 §6.1 Equipment subsystem](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — `MBObjectManager.RegisterObject<ItemModifier>` is open at runtime; reflection helper needed for private fields. Banner items + cape slot.
7. **[Spec v6 §6.2 Health subsystem](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — vanilla wounds binary; healing rate hooks via `DefaultPartyHealingModel.GetDailyHealingHpForHeroes`; surgery survival via `GetSurgeryChance`; food variety drives morale via `DefaultPartyMoraleModel`; no rear-position spawn — must extend `EnlistedFormationAssignmentBehavior`.
8. **[Spec v6 §9 Canonical rank system reference](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — T7 is the firm officer threshold (8 confirmed code-level changes at T7).

### Required project guidance
9. **[AGENTS.md](../../../AGENTS.md)** — Critical Rule #4 (Equipment iteration — numeric loop only, no `Enum.GetValues`).
10. **[CLAUDE.md](../../../CLAUDE.md)** — known footguns inline below.

### Required existing-code orientation
11. **`src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:705-892` `TryTeleportPlayerToFormationPosition`** — formation-position hook. Plan 4 T13 extends with rear-position offset (riskiest task).
12. **`src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:456-461`** — T7+ commander tier handling already present. Plan 4 layers rear-position on top.
13. **`src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:1360-1386` `SetCommonDialogueVariables`** — text variable setup. Plan 4 T16 extends with `IS_OFFICER`, `PLAYER_RANK_TITLE`, `PATRON_NAME`.
14. **`src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:347-368`** — T6→T7 promotion dialog. Plan 4 doesn't modify this (Plan 3 owns ceremony fire); but Plan 4 adds parallel officer-tier dialog branches at priority 110-115.
15. **`src/Features/Equipment/Behaviors/QuartermasterManager.cs:2714, 2926`** — officer stock unlock. Existing T7+ behavior; Plan 4 doesn't modify but verifies stock contains banner/cape items added in T2-T4.
16. **`src/Features/Ranks/Behaviors/PromotionBehavior.cs`** — fires `OnTierChanged`. Plan 4 hooks for gear application same as Plan 3 hooks for ceremony.
17. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:11223-11248` `AssignInitialEquipment`** — equipment assignment precedent (used at enlistment for starter kit). Plan 4 reuses pattern for officer gear application at T7+.

### Required decompile orientation
18. **`Decompile/TaleWorlds.Core/ItemModifier.cs:10-177`** — class structure. All properties private; runtime construction needs reflection.
19. **`Decompile/TaleWorlds.Core/ItemModifierGroup.cs:9-86`** — `AddItemModifier` is callable at runtime.
20. **`Decompile/TaleWorlds.ObjectSystem/MBObjectManager.cs:446-450` `RegisterObject<T>`** — open registration path.
21. **`Decompile/TaleWorlds.Core/EquipmentElement.cs:9-476`** — slot assignment + `GetModifiedItemName` at line 266 (renders modifier prefix on weapon).
22. **`Decompile/TaleWorlds.Core/EquipmentIndex.cs:3-26`** — `Cape = 9`, `Horse = 10`, `HorseHarness = 11`. Cape is a real cosmetic slot.
23. **`Decompile/TaleWorlds.Core/BannerComponent.cs:7-48`** — `BannerLevel` + `BannerEffect.GetBannerEffectBonus()` for captain auras.
24. **`Decompile/TaleWorlds.Core/ItemObject.cs:24-52`** — `ItemTypeEnum`. Banner is a distinct type; no Trinket/Footlocker types.
25. **`Decompile/TaleWorlds.CampaignSystem.GameComponents/DefaultPartyHealingModel.cs:232-286` `GetDailyHealingHpForHeroes`** — patch target. Base +11 HP/day for heroes; Plan 4 layers Officer Tent (+6 at T7+).
26. **`Decompile/TaleWorlds.CampaignSystem.GameComponents/DefaultPartyHealingModel.cs:45-49` `GetSurgeryChance`** — `0.0015f * Surgeon Medicine`. Plan 4 layers Personal Surgeon (+0.15 = 15%) at T7+.
27. **`Decompile/TaleWorlds.CampaignSystem.GameComponents/DefaultPartyMoraleModel.cs:63-130`** — food variety table. Plan 4 boosts variety +2 at T7+ via Officer Mess.

---

## §1 — What this plan delivers

After Plan 4 ships:

- **At T7 promotion (regardless of which path triggered it):** patron-named weapon modifier registered + applied; rank-escalating cape (T1-3 plain → T4-6 sash → T7-9 fur-trimmed officer cape); banner item with `BannerComponent` formation aura; officer's-tent healing bonus active (+6 HP/day); Personal Surgeon survival roll modifier (+15%); Officer Mess food variety (+2 morale).
- **At T6+ in battle (smoke-gated):** rear-formation offset puts the player ~5m back from formation median, reducing wound chance ~50% in auto-resolve. **Riskiest piece — feature-flagged so it can be disabled if smoke fails.**
- **In dialog:** rank-gated greetings at priority 110-115. Innkeepers say "Good evening, Captain"; peer officers initiate professional conversation. `SetCommonDialogueVariables` exposes `IS_OFFICER`, `PLAYER_RANK_TITLE`, `PATRON_NAME` for content authoring.
- **Visible in inventory:** sword tooltip shows *"Crassus's Fine Steel Longsword"* (or whatever lord-name the player served under). Banner appears in inventory; cape visible on character model.
- **At T8 + T9:** banner level scales (T8 BannerLevel=2; T9 BannerLevel=3). Cape variant doesn't change (officer cape ships at T7+).

**No new mechanic surfaces beyond gear + dialog deltas.** No new menu options (Camp menu unchanged). No new save-classes (gear stored in vanilla `Hero.BattleEquipment`).

---

## §2 — Subsystems explored

| Audit | Finding | Spec |
| :-- | :-- | :-- |
| Equipment subsystem | `MBObjectManager.RegisterObject<ItemModifier>` is open; private fields require reflection | §6.1 |
| Item provenance | No "GivenBy" field on EquipmentElement; encode in ItemModifier StringId (`lord_gifted_crassus_t7`) | §6.1 |
| Banner items | `BannerComponent` extends `WeaponComponent` with `BannerLevel` + `BannerEffect`; stat-bearing item | §6.1 |
| Cape slot | `EquipmentIndex.Cape = 9` is real cosmetic slot; vanilla supports any culture | §6.1 |
| Wound model | Vanilla binary (`IsWounded` at HP ≤ 20); no graded injury system | §6.2 |
| Healing rate hooks | `DefaultPartyHealingModel.GetDailyHealingHpForHeroes` patchable for additive officer-tent bonus | §6.2 |
| Surgery survival | `GetSurgeryChance` reads Surgeon Medicine; Plan 4 wraps with +15% officer modifier | §6.2 |
| Food variety | `DefaultPartyMoraleModel` table-based; +2 entries = +2 morale | §6.2 |
| Battle positioning | No vanilla rear-position spawn for heroes; mod must extend `EnlistedFormationAssignmentBehavior.TryTeleportPlayerToFormationPosition` | §6.2 |

---

## §3 — Subsystems Plan 4 touches

### Files modified

| File | Change | Tasks |
| :-- | :-- | :-- |
| `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs:705-892` | Add rear-position offset for T7+ via feature-flagged extension | T13 |
| `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs:1360-1386` | Extend `SetCommonDialogueVariables` with `IS_OFFICER`, `PLAYER_RANK_TITLE`, `PATRON_NAME` | T16 |
| `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs` (new section) | Add ~10 rank-gated dialog branches at priority 110-115 | T17, T18 |
| `src/Mod.GameAdapters/Patches/...` | Harmony patches for `DefaultPartyHealingModel.GetDailyHealingHpForHeroes` + `GetSurgeryChance` + `DefaultPartyMoraleModel` food variety | T9, T10, T11 |
| `Tools/Validation/validate_content.py` | No new phase needed (Plan 4 doesn't add catalog content); existing phases verify ItemModifier registrations don't collide | — |

### Files created

| File | Purpose | Tasks |
| :-- | :-- | :-- |
| `src/Features/Officer/ItemModifierFactory.cs` | Reflection helper for runtime `ItemModifier` construction | T2 |
| `src/Features/Officer/PatronWeaponModifier.cs` | Logic for naming + applying patron-named weapon modifier on T7 promotion | T3 |
| `src/Features/Officer/CapeProgression.cs` | T1-3 / T4-6 / T7-9 cape ItemObject lookup + slot assignment | T4 |
| `src/Features/Officer/BannerProvision.cs` | Banner item creation + slot assignment | T5, T6 |
| `src/Features/Officer/OfficerTrajectoryBehavior.cs` | Hooks `OnTierChanged`; coordinates gear application + survival modifiers | T7, T12 |
| `src/Mod.GameAdapters/Patches/OfficerHealingPatch.cs` | Harmony postfix on `GetDailyHealingHpForHeroes` | T9 |
| `src/Mod.GameAdapters/Patches/OfficerSurgeryPatch.cs` | Harmony postfix on `GetSurgeryChance` | T10 |
| `src/Mod.GameAdapters/Patches/OfficerMessPatch.cs` | Harmony postfix on food-variety calculation | T11 |
| `ModuleData/Enlisted/Officer/cape_progression.json` | Cape ItemObject IDs per tier band per culture | T4 |
| `ModuleData/Enlisted/Officer/banner_definitions.json` | Banner ItemObject definitions (per culture, per officer rank) | T5 |
| `docs/Features/Officer/officer-trajectory-design.md` | Per-system delta reference doc | T1 |
| `docs/superpowers/plans/2026-04-24-ck3-wanderer-officer-trajectory-verification.md` | Plan 4 verification report | T20 |

### Subsystems Plan 4 does NOT touch

- Companion spawning (Plan 2)
- Ceremony storylets (Plan 3 — T7 ceremony content already authored; Plan 4's gear application happens in parallel via separate `OnTierChanged` hook)
- Endeavor System (Plan 5)
- Patron favors (Plan 6)
- Personal Kit (Plan 7)
- Officer-tier menu options — none added; existing Camp + Status menus unchanged

---

## §4 — Locked design decisions

### §4.1 Officer threshold (LOCKED — T7)

T7 is the firm officer boundary per spec §9. Plan 4's gear/health/dialog deltas all gate on `EnlistmentBehavior.Instance?.EnlistmentTier >= 7`. T8 + T9 layer additional incremental deltas (banner level scales) but the qualitative shift is at T7.

### §4.2 Patron-named weapon modifier convention

**Naming pattern:** `lord_gifted_<culture>_t<tier>` (StringId) → display name *"<Lord's Name>'s Fine Steel <WeaponName>"*.

E.g.:
- StringId: `lord_gifted_vlandian_t7`
- Display: `"Crassus's Fine Steel Longsword"` (lord name + base weapon name)

**Stat bonuses per tier band** (locked numbers; tunable in Plan 7 polish):
- T7: +3 damage, +1 swing speed, +5% price multiplier
- T8: +5 damage, +2 swing speed, +10% price
- T9: +8 damage, +3 swing speed, +20% price

ModifierGroup: `mod_lord_gifted` (new group). Modifiers added via `MBObjectManager.RegisterObject<ItemModifier>` at `OnSessionLaunched` (per CLAUDE.md issue #17 — not OnGameStart).

### §4.3 Cape progression (LOCKED)

| Tier band | ItemObject ID pattern | Style |
| :-- | :-- | :-- |
| T1-T3 | `cape_recruit_<culture>` | Plain woolen cloak |
| T4-T6 | `cape_nco_<culture>` | Colored sash + plain cloak (NCO rank) |
| T7-T9 | `cape_officer_<culture>` | Fur-trimmed officer cape with insignia |

Six cultures × 3 tier bands = 18 ItemObjects. Authored in `ModuleData/Enlisted/Officer/cape_progression.json` (T4). Each entry is a vanilla-format `<Item>` XML embedded in JSON with mesh + material refs.

**Note:** Plan 4 v1 may ship with stub cosmetic-only cloaks (no unique 3D meshes) using existing vanilla cape art. Polish pass in Plan 7 swaps in custom assets if budget allows.

### §4.4 Banner items (LOCKED)

`BannerComponent`-bearing items with escalating `BannerLevel`:
- T7: BannerLevel = 1, generic captain banner
- T8: BannerLevel = 2, commander banner
- T9: BannerLevel = 3, marshal banner

Three culture-flavored variants per tier (Vlandian, Sturgian, Imperial — same cultural-fallback strategy as Plan 3 §4.3): 3 cultures × 3 tiers = 9 banner ItemObjects.

Each banner StringId encodes patron + rank: `banner_<culture>_t{N}` (or `banner_<culture>_<patron-clan>_t{N}` for fully-personalized; latter requires per-promotion runtime registration). **Plan 4 v1 ships generic culture-keyed banners; per-patron banners deferred to Plan 7 polish.**

### §4.5 Healing model deltas (LOCKED)

| Effect | Trigger | Magnitude | Implementation |
| :-- | :-- | :-- | :-- |
| Officer Tent | T7+ + in camp (any settlement, or `IsCampMode`) | +6 HP/day to Hero.MainHero | Harmony postfix on `DefaultPartyHealingModel.GetDailyHealingHpForHeroes` |
| Personal Surgeon | T7+ + battle outcome involves player wound | +15% additive to `GetSurgeryChance` for Hero.MainHero | Harmony postfix on `DefaultPartyHealingModel.GetSurgeryChance` |
| Officer Mess | T7+ + party feeding | +2 to food variety contribution | Harmony postfix on food-variety calculation in `DefaultPartyMoraleModel` |

All three are postfix patches; no prefix replacement (which would replace vanilla logic). Patches use the standard Harmony pattern from `src/Mod.GameAdapters/Patches/` (existing).

### §4.6 Rear-formation offset (LOCKED — feature-flagged)

**Behavior:** When `EnlistedFormationAssignmentBehavior.TryTeleportPlayerToFormationPosition` runs at battle start AND `EnlistmentTier >= 7`, the player's spawn position is offset `-5m * formation.Direction` (5 meters back from formation median).

**Feature flag:** `enlisted_config.json` field `officer_rear_formation_enabled` (default `true`). If smoke testing reveals issues (e.g. small formations spawning the player off-map, AI pathing breakdown), set to `false` and ship without this delta. The other gear/health deltas are unaffected.

**Effect estimate:** ~50% wound-risk reduction in auto-resolve battles where rear vs front position matters. Manual battles less affected (player chooses position).

### §4.7 Dialog branches (LOCKED — priority 110-115)

Plan 4 ships ~10 rank-gated dialog branches:

| ID | State token (input) | Output | Condition | Priority |
| :-- | :-- | :-- | :-- | :-- |
| `inn_greet_officer` | `start` (settlement inn) | `inn_hub` | `IsOfficer() && InSettlement()` | 115 |
| `notable_greet_officer` | `notable_pretalk` | `notable_hub_officer` | `IsOfficer() && Hero.OneToOneConversationHero.IsNotable` | 113 |
| `lord_greet_officer` | `lord_pretalk` | `lord_talk_officer` | `IsOfficer() && Hero.OneToOneConversationHero.IsLord && IsCurrentEnlistedLord(Hero.OneToOneConversationHero)` | 112 |
| `peer_officer_chat_init` | `lord_pretalk` | `officer_chat_hub` | `IsOfficer() && IsPeerOfficerInArmy(Hero.OneToOneConversationHero)` | 111 |
| `peer_officer_tactical` | `officer_chat_hub` | `officer_tactical_response` | always (after init) | 110 |
| `peer_officer_morale` | `officer_chat_hub` | `officer_morale_response` | always | 110 |
| `peer_officer_political` | `officer_chat_hub` | `officer_political_response` | `IsOfficer() && SamefactionLord()` | 110 |
| `peer_officer_back` | `officer_chat_hub` | `lord_pretalk` | always | 110 |
| `lord_address_captain` | `lord_pretalk` | `lord_main_hub_officer` | `IsOfficer()` (NPC line, lord speaks) | 113 |
| `lord_address_humble_or_proud` | `lord_main_hub_officer` | varies | reads `ceremony.t7.choice` flag | 112 |

Total: ~10 branches authored in C# in `EnlistedDialogManager`. Plus text-variable substitution per T16 expansion.

---

## §5 — Tooling and safeguards

Inherits Plan 1 §5 + Plan 2 §5 + Plan 3 §5. Plan 4-specific:

### Officer gear smoke recipe

For each officer-gear task (T2-T6):

1. Build clean.
2. Launch game; enlist with Vlandian lord; force-set tier to 7 via Debug Tools.
3. Verify on T7 promotion:
   - Sword in BattleEquipment slot 0 has modifier; tooltip shows "Crassus's Fine Steel Longsword" (or appropriate)
   - Cape slot 9 contains `cape_officer_vlandian` ItemObject
   - Banner item appears (in inventory or equipped per T6 decision)
   - Inspect via Debug Tools "Inspect Hero Equipment"
4. Force-set tier to 8 → confirm banner level updates to 2.
5. Force-set tier to 9 → confirm banner level updates to 3.
6. Save → reload → all gear preserved.

### Officer health smoke recipe

For T9-T11:

1. Force tier to 7. Wound Hero.MainHero to 50/100 HP.
2. Wait 1 in-game day. Confirm HP restored to ~67 (50 + 11 base + 6 officer tent = 67).
3. Force pre-Plan-4 baseline (rollback or comparison save): same scenario, expect 50 + 11 = 61.
4. Diff = +6 HP/day confirmed.

For surgery: trigger battle wound severe enough to roll for survival; vanilla rolls 0.0015 * Medicine; Plan 4 patch adds +0.15. Run 20 trials; confirm survival rate increase.

For food variety: stock party with 5 food types. Open party UI; confirm morale contribution shows +2 over baseline.

### Rear-formation smoke recipe (T13 — riskiest)

1. Force tier to 7. Enter battle as enlisted soldier (use Debug Tools or natural encounter).
2. Confirm at battle start: player's agent spawns ~5m back from formation median.
3. Test on small formation (10 troops), medium (30), large (100). Confirm spawn position doesn't push player off-map or into terrain.
4. Test on each formation class (Infantry, Ranged, Cavalry, HorseArcher). Confirm offset behaves consistently.
5. If ANY case fails, set `officer_rear_formation_enabled = false` and ship without this delta. Document failure mode in T20 verification report.

---

## §6 — Tasks (sequential)

### T1 — Officer trajectory design doc

**Goal:** Document the per-system delta reference at `docs/Features/Officer/officer-trajectory-design.md`.

**Content:** §4.1-§4.7 reproduced + flow diagrams showing OnTierChanged → ItemModifierFactory → PatronWeaponModifier + CapeProgression + BannerProvision triggered.

**Verification:** Document review.

---

### T2 — `ItemModifierFactory` reflection helper

**Goal:** Centralize runtime `ItemModifier` construction. Per spec §6.1, `ItemModifier` private fields require reflection.

**Files:** New `src/Features/Officer/ItemModifierFactory.cs`. Edit `Enlisted.csproj`.

**Concrete API:**

```csharp
public static class ItemModifierFactory
{
    public static ItemModifier CreateAndRegister(
        string stringId,
        TextObject name,
        int damageBonus,
        int speedBonus,
        int armorBonus,
        float priceMultiplier,
        ItemQuality quality,
        ItemModifierGroup group)
    {
        var mod = new ItemModifier();
        // Use reflection to set private fields:
        // _stringId, _name, _damage, _speed, _armor, _priceMultiplier, _itemQuality
        // (read field names from Decompile/TaleWorlds.Core/ItemModifier.cs)
        SetPrivateField(mod, "_stringId", stringId);
        SetPrivateField(mod, "_name", name);
        SetPrivateField(mod, "_damage", damageBonus);
        SetPrivateField(mod, "_speed", speedBonus);
        SetPrivateField(mod, "_armor", armorBonus);
        SetPrivateField(mod, "_priceMultiplier", priceMultiplier);
        SetPrivateField(mod, "_itemQuality", quality);
        
        MBObjectManager.Instance.RegisterObject<ItemModifier>(mod);
        group.AddItemModifier(mod);
        
        return mod;
    }
    
    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            ModLogger.Surfaced("OFFICER", "modifier_field_missing",
                new InvalidOperationException($"Field {fieldName} not found on ItemModifier"));
            return;
        }
        field.SetValue(instance, value);
    }
}
```

**Verification:** Smoke: call `CreateAndRegister("test_mod", new TextObject("Test Modifier"), 5, 2, 0, 1.5f, ItemQuality.Fine, someGroup)`. Verify modifier appears in `MBObjectManager.GetObject<ItemModifier>("test_mod")` after registration.

**Footgun:** Per CLAUDE.md issue #17, run `RegisterObject` in `OnSessionLaunched`, NOT `OnGameStart` (because `MBObjectManager` may not be ready).

---

### T3 — `PatronWeaponModifier` logic

**Goal:** On T7 promotion, register a patron-named modifier and apply to player's weapon slot 0.

**Files:** New `src/Features/Officer/PatronWeaponModifier.cs`.

**Concrete logic:**

```csharp
public static class PatronWeaponModifier
{
    public static void ApplyAtPromotion(int newTier)
    {
        if (newTier != 7) return;  // Only at T7 commission
        
        var lord = EnlistmentBehavior.Instance?.EnlistedLord;
        if (lord == null) return;
        
        var stringId = $"lord_gifted_{lord.Culture.StringId}_t7_{lord.StringId}";
        var name = new TextObject($"{{=officer_lord_gifted_modifier}}{{LORD_NAME}}'s Fine Steel {{ITEMNAME}}");
        name.SetTextVariable("LORD_NAME", lord.Name);
        
        var group = MBObjectManager.Instance.GetObject<ItemModifierGroup>("mod_lord_gifted");
        var mod = ItemModifierFactory.CreateAndRegister(
            stringId, name,
            damageBonus: 3, speedBonus: 1, armorBonus: 0,
            priceMultiplier: 1.05f,
            quality: ItemQuality.Fine,
            group);
        
        // Apply to MainHero's primary weapon
        var hero = Hero.MainHero;
        var weapon = hero.BattleEquipment[EquipmentIndex.Weapon0];
        if (weapon.Item != null)
        {
            hero.BattleEquipment[EquipmentIndex.Weapon0] = new EquipmentElement(weapon.Item, mod);
        }
    }
}
```

**Verification:** Smoke per officer gear recipe T2-step-3.

---

### T4 — `CapeProgression` lookup + apply

**Goal:** Map (tier-band, culture) → cape ItemObject ID; apply on tier change.

**Files:**
- New `ModuleData/Enlisted/Officer/cape_progression.json` (18 ItemObjects: 6 cultures × 3 tier bands)
- New `src/Features/Officer/CapeProgression.cs`
- Edit `Enlisted.csproj` — add `<OfficerData Include="ModuleData\Enlisted\Officer\*.json"/>` ItemGroup + MakeDir + Copy in AfterBuild

**Concrete logic:**

```csharp
public static class CapeProgression
{
    public static void ApplyAtTierChange(int newTier)
    {
        var lord = EnlistmentBehavior.Instance?.EnlistedLord;
        var cultureId = lord?.Culture?.StringId ?? "vlandia";
        
        string capeId;
        if (newTier <= 3) capeId = $"cape_recruit_{cultureId}";
        else if (newTier <= 6) capeId = $"cape_nco_{cultureId}";
        else capeId = $"cape_officer_{cultureId}";
        
        var cape = MBObjectManager.Instance.GetObject<ItemObject>(capeId);
        if (cape == null) { /* expected log; return */ }
        
        Hero.MainHero.BattleEquipment[EquipmentIndex.Cape] = new EquipmentElement(cape, null);
    }
}
```

**Verification:** Force tier 1, 4, 7; verify cape slot updates to recruit, nco, officer cape respectively.

---

### T5-T6 — Banner provision

**Goal:** T5 authors banner ItemObjects; T6 implements assignment logic.

**Files:**
- T5: `ModuleData/Enlisted/Officer/banner_definitions.json` (9 banner items: 3 cultures × 3 levels)
- T6: New `src/Features/Officer/BannerProvision.cs`

**Banner equipping decision:** Banners are NOT a standard `EquipmentIndex` slot. Vanilla treats banners as items the player carries (via party inventory or a special "banner" slot). Plan 4 v1 ships banners as **inventory items** (`Hero.MainHero.PartyBelongedTo.ItemRoster.AddToCounts`) with a player-facing notification "Lord Crassus has granted you the Cavalier's Standard." Polish pass in Plan 7 may upgrade to a special equipped slot if the audit reveals one.

**Concrete logic:**

```csharp
public static class BannerProvision
{
    public static void ApplyAtTierChange(int newTier)
    {
        if (newTier < 7) return;  // Only T7+
        
        var lord = EnlistmentBehavior.Instance?.EnlistedLord;
        var cultureId = lord?.Culture?.StringId ?? "vlandia";
        
        var bannerId = $"banner_{cultureId}_t{newTier}";
        var banner = MBObjectManager.Instance.GetObject<ItemObject>(bannerId);
        if (banner == null) { /* expected log; return */ }
        
        // Add to party inventory (or remove old + add new on tier-up)
        var roster = MobileParty.MainParty.ItemRoster;
        // Remove prior officer banner if present
        for (int i = 0; i < roster.Count; i++)
        {
            var item = roster.GetElementCopyAtIndex(i).EquipmentElement.Item;
            if (item?.StringId?.StartsWith($"banner_{cultureId}_t") == true)
            {
                roster.AddToCounts(item, -1);
            }
        }
        roster.AddToCounts(banner, 1);
        
        InformationManager.DisplayMessage(new InformationMessage(
            new TextObject("{=officer_banner_granted}{LORD_NAME} has granted you the {BANNER_NAME}.")
                .SetTextVariable("LORD_NAME", lord.Name)
                .SetTextVariable("BANNER_NAME", banner.Name)
                .ToString()));
    }
}
```

**Verification:** Smoke: T7 promotion adds T7 banner to party roster; T8 promotion replaces with T8 banner.

---

### T7 — `OfficerTrajectoryBehavior` orchestrator

**Goal:** Single Campaign behavior subscribed to `OnTierChanged`; calls all gear-application helpers.

**Files:** New `src/Features/Officer/OfficerTrajectoryBehavior.cs`.

**Concrete logic:**

```csharp
public sealed class OfficerTrajectoryBehavior : CampaignBehaviorBase
{
    public override void RegisterEvents()
    {
        EnlistmentBehavior.OnTierChanged += OnTierChanged;
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }
    
    public override void SyncData(IDataStore dataStore) { }
    
    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Register the mod_lord_gifted ItemModifierGroup if not already present
        EnsureLordGiftedGroup();
    }
    
    private void OnTierChanged(int prev, int curr)
    {
        // Apply gear deltas at every tier change
        CapeProgression.ApplyAtTierChange(curr);
        BannerProvision.ApplyAtTierChange(curr);
        if (prev < 7 && curr >= 7) PatronWeaponModifier.ApplyAtPromotion(curr);
        // Health/morale patches are global (Harmony) — no per-tier-change action needed
    }
    
    private void EnsureLordGiftedGroup() { /* check/create ItemModifierGroup */ }
}
```

**Verification:** Build clean. Force tier sequence 1→2→3→...→9; verify all gear applies correctly at each transition.

---

### T8 — Officer-tier dialog gating helpers

**Goal:** Helpers used by T17-T18 dialog conditions: `IsOfficer()`, `IsPeerOfficerInArmy(Hero)`, `SameFactionLord()`, etc.

**Files:** Extend `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs` with helper methods.

**Concrete:**

```csharp
private static bool IsOfficer()
    => EnlistmentBehavior.Instance?.IsEnlisted == true
    && EnlistmentBehavior.Instance.EnlistmentTier >= 7;

private static bool IsPeerOfficerInArmy(Hero candidate)
{
    if (!IsOfficer()) return false;
    if (candidate == null || !candidate.IsLord) return false;
    var enlistedLord = EnlistmentBehavior.Instance?.EnlistedLord;
    if (enlistedLord == null || candidate == enlistedLord) return false;
    // Same army?
    return candidate.PartyBelongedTo?.Army == enlistedLord.PartyBelongedTo?.Army
        && enlistedLord.PartyBelongedTo?.Army != null;
}

private static bool SameFactionLord()
{
    var converse = Hero.OneToOneConversationHero;
    var enlisted = EnlistmentBehavior.Instance?.EnlistedLord;
    return converse?.MapFaction == enlisted?.MapFaction;
}
```

**Verification:** Unit smoke each helper.

---

### T9-T11 — Healing model patches (Officer Tent / Personal Surgeon / Officer Mess)

**Goal:** Three Harmony postfix patches.

**T9 — Officer Tent:** patch `DefaultPartyHealingModel.GetDailyHealingHpForHeroes`. If Hero == MainHero AND IsOfficer, add +6 to result.

**T10 — Personal Surgeon:** patch `DefaultPartyHealingModel.GetSurgeryChance`. If party.MobileParty.LeaderHero == MainHero AND IsOfficer, add +0.15 to result.

**T11 — Officer Mess:** patch food variety calculation in `DefaultPartyMoraleModel`. If MainParty AND IsOfficer, add +2 to variety count.

**Files:** Three new patch files in `src/Mod.GameAdapters/Patches/`.

**Concrete (T9 example):**

```csharp
[HarmonyPatch(typeof(DefaultPartyHealingModel), nameof(DefaultPartyHealingModel.GetDailyHealingHpForHeroes))]
public static class OfficerHealingPatch
{
    public static void Postfix(PartyBase party, ref ExplainedNumber __result)
    {
        if (party?.MobileParty != MobileParty.MainParty) return;
        if (EnlistmentBehavior.Instance?.IsEnlisted != true) return;
        if (EnlistmentBehavior.Instance.EnlistmentTier < 7) return;
        
        __result.Add(6f, new TextObject("{=officer_tent_bonus}Officer's Tent"));
    }
}
```

**Verification:** Per officer health smoke recipe.

**Footgun:** `ExplainedNumber` (vanilla type for stat tooltips) — passing the `TextObject` reason gives the player a visible breakdown ("+6 from Officer's Tent"). Don't use `__result.ResultNumber += 6` directly.

---

### T12 — Promotion gear application orchestration

**Goal:** Wire `OfficerTrajectoryBehavior.OnTierChanged` to fire AFTER ceremony provider (Plan 3) so the gear application visually follows the ceremony narrative.

**Decision:** Both Plan 3 (RankCeremonyBehavior) and Plan 4 (OfficerTrajectoryBehavior) hook `OnTierChanged`. Subscriber order matters for visual sequencing:
- Ceremony fires modal (player reads narrative)
- Player picks choice (modal closes)
- THEN gear applies (toast notifications fire — "Lord Crassus has granted you a Cavalier's banner.")

If gear applies BEFORE ceremony fires, visual order is wrong (toasts before narrative).

**Mitigation:** OfficerTrajectoryBehavior wraps gear application in `NextFrameDispatcher.RunNextFrame` to defer 1 frame. Since ceremony modal is a `MultiSelectionInquiryData` that pauses the game, the next frame doesn't fire until the player picks an option. By then ceremony narrative has been consumed.

**Files:** Edit `src/Features/Officer/OfficerTrajectoryBehavior.cs:OnTierChanged`.

**Verification:** Smoke T6→T7 promotion. Confirm visual order: ceremony modal opens → player reads → picks → modal closes → gear toasts fire ("Cape upgraded", "Banner granted", "Sword renamed").

---

### T13 — Rear-formation offset (RISKIEST)

**Goal:** Extend `EnlistedFormationAssignmentBehavior.TryTeleportPlayerToFormationPosition:705-892` with rear offset for T7+.

**Files:** Edit `src/Features/Combat/Behaviors/EnlistedFormationAssignmentBehavior.cs`.

**Concrete change:** After existing position calculation, if `IsOfficer()` AND `EnlistedConfig.OfficerRearFormationEnabled`, offset position by `-5f * formation.Direction.AsVec2`.

**Smoke gate:** Run §5 rear-formation recipe. If ANY scenario fails (off-map spawn, AI breakdown), set `officer_rear_formation_enabled = false` in `enlisted_config.json` defaults and document deferred-shipping in T20.

**Verification:** §5 recipe.

**Risk:** HIGH. This is the riskiest task in Plan 4. Feature-flagged so failure doesn't block ship.

---

### T14-T15 — (RESERVED for T13 follow-up)

If T13 surfaces issues requiring deeper fixes, T14-T15 hold mitigation work. Otherwise no-op.

---

### T16 — `SetCommonDialogueVariables` extension

**Goal:** Extend `EnlistedDialogManager.cs:1360-1386` with `IS_OFFICER`, `PLAYER_RANK_TITLE`, `PATRON_NAME` text variables for Plan 4 dialog branches.

**Files:** Edit `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs`.

**Concrete addition:**

```csharp
private void SetCommonDialogueVariables()
{
    // ... existing PLAYER_NAME, PLAYER_RANK, LORD_NAME ...
    
    var enlistment = EnlistmentBehavior.Instance;
    var isOfficer = enlistment?.IsEnlisted == true && enlistment.EnlistmentTier >= 7;
    MBTextManager.SetTextVariable("IS_OFFICER", isOfficer ? "1" : "0");
    
    var rankTitle = enlistment != null ? RankHelper.GetCurrentRank(enlistment) : "Soldier";
    MBTextManager.SetTextVariable("PLAYER_RANK_TITLE", rankTitle);
    
    // Patron name (if conversation target is on Roll of Patrons — Plan 6 populates)
    var conversationTarget = Hero.OneToOneConversationHero;
    var patronName = "";
    if (conversationTarget != null && PatronRoll.Instance?.Has(conversationTarget.Id) == true)
    {
        patronName = conversationTarget.Name?.ToString() ?? "";
    }
    MBTextManager.SetTextVariable("PATRON_NAME", patronName);
}
```

**Verification:** Build clean. Smoke: enlisted with Vlandian lord at T7; open dialog with the lord; confirm `{IS_OFFICER}` substitutes to "1" and `{PLAYER_RANK_TITLE}` substitutes to "Cavalier".

---

### T17-T18 — Rank-gated dialog branches

**Goal:** Author the ~10 branches per §4.7 table.

**Files:** Edit `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs`. Add new `AddOfficerDialogs(CampaignGameStarter starter)` method called from `OnSessionLaunched`.

**Concrete (T17 — innkeeper greeting example):**

```csharp
starter.AddDialogLine(
    "inn_greet_officer",
    "start",
    "inn_hub",
    "{=officer_inn_greeting}Good evening, {PLAYER_RANK_TITLE}. Word travels — you've made captain, haven't you?",
    () => IsOfficer() && InSettlement(),
    null,
    115);
```

T17 covers innkeepers, notables, lord greetings (~5 branches). T18 covers peer-officer professional conversation tree (~5 branches).

**Verification:** Smoke: enlisted at T7, enter inn → confirm officer greeting fires (NOT generic recruit greeting). Talk to peer-officer in army → confirm professional dialog branch surfaces.

**Footgun:** Per CLAUDE.md, run `Tools/Validation/generate_error_codes.py` after dialog additions if any line shifts cause ModLogger calls to move.

---

### T19 — Validators tightened + plan-end smoke

**Goal:** Verify all 7 ItemModifier registrations don't collide with vanilla; run full smoke recipe.

**Files:** Edit `validate_content.py` (extend Phase 12 storylet ref check to also verify ItemModifier StringIds don't shadow vanilla ones).

**Verification:** Full §5 + §6 smoke recipe. Document any deviations.

---

### T20 — Plan 4 verification report

**Goal:** Document smoke results, rear-formation feature-flag decision, sign-off.

**Files:** New `docs/superpowers/plans/2026-04-24-ck3-wanderer-officer-trajectory-verification.md`.

**Content:** Build/validator pass; gear smoke per task; health smoke; rear-formation smoke result + feature-flag setting; dialog branch smoke; sign-off.

---

## §7 — Risks

### Risk H1 — Rear-formation offset breaks battle pathing (HIGH)

**Mitigation:** Feature-flagged per §4.6. Smoke-tested across formation classes + sizes per §5 recipe. If failure, set flag false; ship without this delta. Document in T20.

### Risk M1 — `ItemModifier` reflection breaks across patches (MEDIUM)

**Vector:** `ItemModifier` private field names may change between Bannerlord patches. Reflection breaks silently.

**Mitigation:** `ItemModifierFactory.SetPrivateField` Surfaced-logs if a field is missing. Also: keep CLAUDE.md and decompile up-to-date; verify field names against current decompile before each Plan 4 build.

### Risk M2 — Gear application happens before ceremony (MEDIUM)

**Vector:** Subscription order to `OnTierChanged` may put OfficerTrajectoryBehavior before RankCeremonyBehavior. Toast notifications fire before player sees ceremony narrative.

**Mitigation:** T12 wraps gear application in `NextFrameDispatcher.RunNextFrame`. Since modal pauses game, deferred frame waits until player picks.

### Risk L1 — Banner stat bonuses unbalance battles (LOW)

**Vector:** `BannerComponent.GetBannerEffectBonus` at level 3 may give too much formation morale.

**Mitigation:** Plan 7 polish pass tunes bonus values based on playtest data.

---

## §8 — Verification gates

- [ ] Build clean
- [ ] Validators pass
- [ ] Patron-named weapon modifier registers + applies at T7 (T2-T3 smoke)
- [ ] Cape progression works at T1, T4, T7 (T4 smoke)
- [ ] Banner ItemObject grants at T7, scales at T8/T9 (T5-T6 smoke)
- [ ] Officer Tent +6 HP/day verified (T9 smoke)
- [ ] Personal Surgeon +15% verified (T10 smoke)
- [ ] Officer Mess +2 morale verified (T11 smoke)
- [ ] Rear-formation offset works OR feature-flagged off (T13)
- [ ] All 10 dialog branches at priority 110-115 surface correctly (T17-T18)
- [ ] `IS_OFFICER`, `PLAYER_RANK_TITLE`, `PATRON_NAME` text variables substitute (T16)
- [ ] T7 promotion shows correct visual order (ceremony → choice → gear toasts)
- [ ] Save-load preserves all gear (T2-T6)
- [ ] Verification report committed

---

## §9 — Definition of done

Plan 4 complete when 20 tasks ✅, §8 gates pass, report committed, CLAUDE.md updated.

---

## §10 — Hand-off to Plans 5-7

### For Plan 5 (Endeavor System)
- Officer-tier endeavor categories (e.g. "tactical command" subcategory) gate on `IsOfficer()` helper from T8.

### For Plan 6 (Roll of Patrons)
- `PATRON_NAME` text variable available for patron-favor dialog branches.
- Patron-named weapon modifier StringId pattern (`lord_gifted_<culture>_t7_<patron-clan>`) — Plan 6 may add per-patron ceremonial gifts using same factory.

### For Plan 7 (Polish)
- Custom 3D mesh assets for capes if desired.
- Per-patron banners (vs generic culture banners).
- Tuning of healing/surgery/morale magnitudes.

---

## §11 — Out of scope

- Companion spawning (Plan 2)
- Ceremony storylets (Plan 3)
- Endeavor System (Plan 5)
- Patron favors (Plan 6)
- Personal Kit (Plan 7)
- Lifestyle Unlocks (Plan 7)
- Custom 3D mesh assets (deferred to Plan 7 or beyond)
- News-feed integration

---

## §12 — References

- Plans 1-3 + verification reports
- Spec v6
- AGENTS.md / CLAUDE.md
- Existing `EnlistedFormationAssignmentBehavior`, `EnlistedDialogManager`, `EnlistmentBehavior`, `RankHelper`, `PromotionBehavior`, `RankCeremonyBehavior`
- Decompile: `ItemModifier`, `ItemModifierGroup`, `MBObjectManager`, `EquipmentElement`, `EquipmentIndex`, `BannerComponent`, `ItemObject`, `DefaultPartyHealingModel`, `DefaultPartyMoraleModel`
