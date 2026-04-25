# CK3 Wanderer Mechanics — Systems Analysis for Enlisted

**Status:** Draft v6 (2026-04-24). Three-way mapping of CK3 *Roads to Power* / *Wandering Nobles* mechanics → existing Enlisted systems → TaleWorlds native APIs. **v6 commits to "ship all" via 7 numbered plans** (§8). Companion to the menu+duty unification design at [`2026-04-24-enlisted-menu-duty-unification-design.md`](2026-04-24-enlisted-menu-duty-unification-design.md).

**v6 changes (implementation roadmap):**
- **§8 NEW — Implementation roadmap.** All v3-v5 mechanics ship via 7 numbered plans, ~150 tasks total, sized 15-30 tasks each (consistent with project's plan-sizing norms). Plan 1 (Architecture Foundation) blocks all others; Plans 2-7 partly parallelize per the dependency graph.
- **§7 Recommended next steps** preserved as historical design context; the 7-plan roadmap in §8 supersedes it for execution.
- Existing §8 (Canonical rank system reference) renumbered to §9.

**v5 changes (menu + dialog deep-dive findings):**
- **`InteractiveEvent` terminology corrected** — it is NOT a vanilla TaleWorlds class (zero matches in decompile). It is the mod's `EventDefinition` carried on `StoryCandidate.InteractiveEvent`. Spec wording across §3.8 and §3.9 updated to reference concrete classes.
- **§6.8 NEW — canonical modal pipeline recipe** with the full `Storylet → BuildModal → EmitCandidate → ShowEventPopup` chain and exact file:line cites. The "Home-surface adapter/provider pattern" referred to abstractly in v4 is concretely `StoryletEventAdapter` + `StoryDirector` + `EventDeliveryManager`, with `HomeEveningMenuProvider.OnSlotSelected` as the working precedent.
- **§6.9 NEW — mod's existing menu + dialog wiring map** — Camp hub option indices (1/2/3/4/7/100, with 5/6 free for new options), Lord dialog branches table, JSON schema for `QMDialogueCatalog` (reusable for companion dialogs), staggered-index anti-collision convention.
- **§3.8 Endeavor System updated** — the `EndeavorPhaseProvider` is modeled on `HomeEveningMenuProvider` literally; phase events use `StoryletEventAdapter.BuildModal` + `StoryDirector.EmitCandidate(candidate)` with `candidate.InteractiveEvent = evt`.
- **§3.9 Rank-Ceremony Arc updated** — `CeremonyProvider` is the same shape as `HomeEveningMenuProvider`; ceremony hook on `OnTierChanged` calls `StoryletEventAdapter.BuildModal` with `ChainContinuation = true` to bypass the 5-day in-game floor and 3-day category cooldown.
- **Pacing rails verified** — `DensitySettings.ModalFloorInGameDays = 5`, `ModalFloorWallClockSeconds = 60`, `CategoryCooldownDays = 3`, `QuietStretchDays = 7`. `ChainContinuation = true` bypasses cooldowns; 60s wall-clock floor still applies.

**v4 changes (corrections from external review, verified against code):**
- **§3.3 patron lifecycle** rewritten to enlisted-only (was contradicting §0 scoping)
- **§3.8 Endeavor phase routing** corrected — phase events use Home-surface adapter/provider pattern with `InteractiveEvent`, NOT generic auto-phase storylet emissions
- **§4.1 save-offset ledger** consolidated into single concrete table — was scattered with overlapping claims (53/54/55/56 hedged across multiple mechanics). Ledger now also enumerates required container-type and enum-type registrations
- **§3.4 + §3.8 sibling-not-merge** corrected — Endeavors and Contracts are siblings to Orders sharing the Activity backbone; OrderActivity stays authoritative (it owns shipped duty-profile state, named-order arc state, reconstruction)
- **§3.7 retinue wording** corrected — retinue troops live in `MobileParty.MainParty`, not "flagged within the lord's main party." Path A framing was confused
- **§3.9 ceremony hook** rewritten — hooks `OnTierChanged` event with `_ceremonyFiredForTier[N]` dedup, NOT the dialog branch alone. Verified via `PromotionBehavior.cs:330-401`: T6→T7 has THREE paths (auto proving-event, decline-then-dialog, dialog-request) — single hook on `OnTierChanged` collapses all three to one reaction
- **§3.10 + §6.5 stay-back claim** corrected — `EnlistedFormationAssignmentBehavior.cs:188-193` has a hard T7+ gate; withhold mechanic is NOT free for T1-T6 companions. Three-line fix at line 190 enables tier-wide enforcement

**v3 changes:** (a) **Companion-system pivot** — most "spawned army NPCs" become PlayerClan companions in MainParty (not parked-in-settlement NPCs). Vanilla provides spawn customization, combat participation, withhold-from-combat UI, skill-aptitude bonuses, dialog/role-assignment, *and* trait-driven preferences for free. New §3.10. (b) **Endeavor system** (new §3.8) — text-based multi-phase player-driven activities adapting CK3 schemes + adventurer contracts; consolidates §3.1 Adventurer Decisions + §3.4 Side-contracts under one Activity-substrate model; **hybrid skill-OR-companion gating**. (c) **Rank-Ceremony Arc** (new §3.9) — eight character-defining storylets at tier transitions; choice memory; trait compounding; companion witnesses. (d) **Canonical rank system reference** (new §8) — 9-tier ladder, culture labels, XP thresholds, wage formula. (e) Native API audit expanded with §6.5 (companion combat lifecycle), §6.6 (vanilla preferences), §6.7 (CK3-scheme → endeavor mapping). (f) Decision sheet updated.

**v2 changes:** (a) added **§0 Scoping** — all CK3 mechanics operate **enlisted-only**; mod silences on retirement; Officer Trajectory becomes a 7th mechanic at upper ranks; Roll of Patrons accumulates *within enlistment* and is callable while enlisted. (b) added **§3.7 Officer Trajectory** with concrete gear/health/command/dialog deltas. (c) added **§6 Native API findings — tangible-effect audit** with file:line evidence per mechanic from a four-bucket decompile audit. (d) updated decision sheet.

---

## 0. Scoping

**The mod operates only while the player is enlisted.** Every CK3-inspired mechanic in this document — Adventurer Decisions, Personal Kit, Roll of Patrons, Side-contracts, Companion Aptitude, Lifestyle Unlocks, Officer Trajectory — runs *during enlistment* and silences on retirement / discharge / mod-exit. No persisted state needs to survive post-mod gameplay; the campaign continues but Enlisted's surface is no longer active.

**This affects three concrete design choices:**

1. **Roll of Patrons accumulates within a campaign run, callable while enlisted, not across mod boundaries.** A patron entry is created when you discharge from a Lord and remains callable for as long as you stay enlisted (with the same Lord, a new Lord, or between enlistments inside the mod's active window). When you retire from soldiering entirely, the Roll evaporates with the rest of the mod's surface. CK3 patrons persist post-retirement; we explicitly trade that long-tail payoff for a cleaner mod boundary.

2. **Officer Trajectory is the upper-rank phase of the enlistment lifecycle, not a separate post-soldier role.** When the player reaches T6/T7 (boundary TBD per Plan / playtest), they become an officer; mechanics shift accordingly. This is *the* high-level destination of a soldier career inside the mod — not a graduation out of it. Retirement remains the actual graduation, after which the mod silences.

3. **Durable-state save-definer offsets are scoped to the mod's lifetime.** Stores can be cleared on retirement without save-compat concerns. This relaxes some of the cautious lifecycle handling in §3.3 (Roll of Patrons) — discharge handlers don't need to anticipate post-retirement queries.

---

## 1. Executive summary

Ten mechanics — seven CK3 imports + Officer Trajectory + Endeavors + Rank Ceremonies — offer the highest leverage for "running around with the Lord" downtime engagement. The five-bucket native-API audit in §6 confirms most map cleanly to existing TaleWorlds APIs with two notable gaps; tangible in-game effects are documented per mechanic. **The v3 pivot puts vanilla companions in the player's clan + main party as the substrate for army NPCs (Sergeant, Field Medic, Pathfinder, Veteran, QM Officer, Junior Officer)** — they fight, can die, provide skill aptitude bonuses through `MobileParty.EffectiveX`, and natively express trait-gated preferences via the vanilla grievance system. One-paragraph fit verdict per mechanic — depth is in §3, evidence is in §6.

1. **Adventurer Decisions** — Camp-menu actions ("train alone," "drink with the lads," "mend your gear"). Pure storylet-content extension; no new substrate. Ships in days. **Verdict: ✅ Adopt.**
2. **Personal-kit "buildings"** — 3-slot footlocker (Bedroll / Sharpening Stone / Field Kit) with passive bonuses. Vanilla `ItemObject.ItemTypeEnum` has no Trinket type, so use `QualityStore` quality slots instead of fighting the item system. Small store + tick handler. **Verdict: ✅ Adopt with substrate workaround.**
3. **Roll of Patrons** — *Highest-leverage, deepest tendrils.* Long-tail relationship list of every Lord you've served; later you can call in favors (gold loan, troop loan, letter of introduction, marriage). Hero-death lifecycle, save-state across rank reset/retirement, news-feed integration, audience-system integration. **Verdict: ✅ Adopt — but big.**
4. **Side-contracts** — Third-party jobs (innkeeper / notable / fellow soldier) sitting alongside Orders. Native `IssueBase`/`IssueManager`/`Settlement.Notables` provide clean infrastructure, but there's a load-bearing architectural question: are side-contracts a **sibling** of Orders or a **sub-system**? **Verdict: ⚠️ Adopt with sibling-vs-subsystem decision.**
5. **Companion Aptitude** — Vanilla precedent (`DefaultPartyHealingModel` reads `EffectiveSurgeon.GetSkillValue(Medicine)`) is the exact pattern. Plugs into Plan E's role-claim work and the deferred Companions sub-menu (`CompanionAssignmentManager` already exists). **Verdict: ✅ Adopt as Plan E follow-up.**
6. **Lifestyle perk trees** — *Critical API gap.* `Hero.HeroDeveloper.AddPerk(perk)` enforces `PerkObject.RequiredSkillValue` internally — a mod cannot grant a perk to a hero whose skill is below the gate. The perks-version of this feature is high-risk; the **unlocks-version** (lifestyle progression as menu/contract/duty unlocks instead of numeric perks) gets ~80% of the value at ~20% of the substrate cost. **Verdict: ⚠️ Adopt as unlocks-version; defer numeric perks.**
7. **Officer Trajectory** (NEW — §3.7) — The player ranks through 9 tiers; T6/T7 is where line-soldier becomes officer. Concrete deltas across four subsystems: rank-escalating cape (visual), patron-named weapon modifier (visible), banner with formation aura (`BannerComponent`), officer's tent (+6 HP/day → 4-day vs 6-day recovery), bodyguard rear-formation spawn (~50% wound-risk reduction in auto-resolve), rank-gated dialog greetings ("Good evening, Captain") with peers and notables, and **personal retinue troops in `MobileParty.MainParty`** (T7=20 / T8=30 / T9=40 via existing `RetinueManager`; the player's hidden+active main party joins the lord's battles via `MapEventSide` — retinue is the player's own roster, NOT a flagged slice of the lord's). **Verdict: ✅ Adopt — biggest single tangible reward for rank progression.**

---

## 2. Three-system map (high-level)

| CK3 mechanic | Hosting Enlisted system | Native API touchpoints |
| :--- | :--- | :--- |
| Adventurer Decisions | Camp menu (`EnlistedMenuBehavior`) + storylet pool + `FlagStore` cooldowns | `CampaignGameStarter.AddGameMenuOption` (`CampaignGameStarter.cs:93`) |
| Personal-kit | New `PersonalKitStore` (or quality slots in `QualityStore`) + Quartermaster shop | `Hero.HeroDeveloper.AddSkillXp` (`HeroDeveloper.cs:189`); QM tick handler |
| Roll of Patrons | New `PatronRoll` + news feed + Audience flow | `Hero.GetRelation`/`SetPersonalRelation` (`Hero.cs:2025`/`2019`); `Hero.IsAlive`/`IsDead` (`Hero.cs:314`/`298`); `KillCharacterAction`+`OnHeroKilled` (`CampaignEventDispatcher.cs:612`); `BarterManager` (`BarterManager.cs:13-309`) |
| Side-contracts | New `ContractCatalog` + `ContractActivity` (Activity subclass, sibling to `OrderActivity`) | `Settlement.Notables` (`Settlement.cs:275`); `IssueBase`/`IssueManager` |
| Companion Aptitude | Extends Plan E + Companions sub-menu (`CompanionAssignmentManager`) | `Hero.GetSkillValue` (`Hero.cs:1353`); `MobileParty.EffectiveX` getters; `DefaultPartyHealingModel.cs:47` precedent |
| Lifestyle (unlocks) | New `LifestyleUnlockStore` + rank-up reward hook | `EnlistmentBehavior.SetTier` `OnTierChanged` event (already wired) |
| Officer Trajectory (§3.7) | Existing `EnlistmentBehavior` rank tiers + `RetinueManager` (already T7/T8/T9 gated) + new `ItemModifier` registrations + dialog branches at priority 110-115 + formation rear-position offset | `MBObjectManager.RegisterObject<ItemModifier>`; `BannerComponent`; `DefaultPartyHealingModel.GetDailyHealingHpForHeroes`; `MissionAgentSpawnLogic`; `DialogFlow.AddDialogLine` |

---

## 3. Mechanic-by-mechanic analysis

### 3.1 Adventurer Decisions

> **v3 NOTE:** Adventurer Decisions are now the **minor (single-phase) tier** of the Endeavor System (§3.8). The five-category framework, agent assignment, and storylet resolution machinery live there; this section's content stands as the design rationale for what the *minor* endeavor catalog should look like.

**CK3 source.** Minor decisions specific to wanderer life: forage for provisions, train alone, hunt, recruit, etc. Player-initiated, small effects, often gated by camp purpose or lifestyle focus. The CK3 wiki [Adventurer decisions](https://ck3.paradoxwikis.com/Adventurer_decisions) lists the full set.

**Mod hosting system.** Camp menu (`EnlistedMenuBehavior` — Camp hub options). Each decision is a `GameMenu` option whose `consequence` fires a storylet effect. Cooldowns live in `FlagStore` (named bool flags with expiry — already shipped, Spec 0 backbone). Outcomes route through existing `EffectExecutor.Apply` so the spec's "envelope" model is irrelevant for the unlocks-only flavor.

**Native APIs available.**
- `CampaignGameStarter.AddGameMenuOption(menuId, optionId, optionText, condition, consequence, ..., index)` at `CampaignGameStarter.cs:93` — standard registration.
- Cooldown precedent: `Companion.AskForOpinion()` in `LordConversationsCampaignBehavior` uses relation-plus-timestamp checks. Vanilla has no built-in "once per game day" API — track via custom `FlagStore` entry.
- Index parameter is positional `List.Insert` (verified earlier — `GameMenu.cs:147-156`); use unique per-decision indices to avoid Camp-menu reordering surprises.

**State + save-definer needs.**
- 0 new save-definer offsets. Reuses `FlagStore` cooldown flags (e.g. `decision.train_alone.cooldown.until`).

**Cross-system collisions / dependencies.**
- Plan 4's army-situation "Duty" storylets share the Camp menu surface. **Risk: pool-id collision.** Mitigation: prefix all decision storylet ids `decision_*` and route through a dedicated `decision.player_initiated` `StoryDirector` category so Plan 4's `duty.event` (or whatever it ends up named) doesn't compete with player-initiated decisions for the same modal slot.
- The menu+duty spec's Plan A (Camp menu shell) is a soft prerequisite — clean Camp menu makes adding Decisions ~3x easier.

**Sizing + risk.** **Small.** ~6-12 menu options (one per decision archetype), ~12-24 storylet entries (each option pulls 1-3 outcome variants), ~6 cooldown flags, no new types or save offsets. **Risk: low.**

---

### 3.2 Personal-kit buildings

**CK3 source.** [Camp Buildings](https://ck3.paradoxwikis.com/Camp) — up to 6-level buildings (Smithy, Barber's Tent, Training Yard, etc.) that produce passive bonuses scaled by level. Pavilion determines slot count. The Adventurer's Camp is the soldier's analogue.

**Mod hosting system.** The player has no Camp; they live in the Lord's army. The hosting system is a 3-slot **personal effects** model: Bedroll / Sharpening Stone / Field Kit, each upgradeable from level 0 (no kit) to level 3 (master kit). Bought at the QM (already a shop, see `QuartermasterManager` — shop architecture verified in prior audit). Bonuses applied via an hourly/daily tick handler that reads quality levels and grants the corresponding effects.

**Native APIs available.**
- `Hero.HeroDeveloper.AddSkillXp(SkillObject, float)` at `HeroDeveloper.cs:189` — for Trivial-band XP drift bonuses (e.g. Sharpening Stone level 2 → +0.5/hr One-Handed XP).
- `Hero.GetSkillValue(SkillObject)` at `Hero.cs:1353` — for reading hero state.
- `DefaultPartyHealingModel.GetSurgeryChance` at `DefaultPartyHealingModel.cs:47` — pattern for layering bonuses.

**API gap.** `ItemObject.ItemTypeEnum` (in `ItemObject.cs`) has Weapon / Armor / Goods / Book / Banner / Animal / Cape — **no Trinket / Bedroll / Footlocker type**. `Hero.SpecialItems` exists but is a generic list with no slot semantics. Vanilla has no per-tick passive-regen hook on `Hero` outside the party-healing-model context.

**Workaround (recommended): use `QualityStore` quality slots, not `ItemObject`.**
- `QualityStore` (Spec 0, shipped) supports typed numeric per-hero qualities. Add `kit.bedroll_level`, `kit.sharpening_stone_level`, `kit.field_kit_level` (each int 0-3). The QM "shop" UI for personal kits writes these values; the tick handler reads them. This avoids fighting `ItemObject`'s type enum entirely.
- The trinkets are *data*, not *items*. Author the catalog as JSON in `ModuleData/Enlisted/PersonalKit/personal_kit.json` (price per level, bonus per level, flavor text) and load it like other catalogs.

**State + save-definer needs.**
- If using `QualityStore` quality slots: 0 new save-definer offsets (extends existing store).
- If a new `PersonalKitStore` class is preferred for clarity: 1 offset → **53**.

**Cross-system collisions / dependencies.**
- Trivial-band XP drift caps from the menu+duty spec's agency v3 substrate (Plan B) — *not yet shipped*. Personal-kit XP bonuses must respect a Trivial-band cap when Plan B exists; can run uncapped before then. Document the future cap reconciliation.
- QM shop integration is clean: `QuartermasterManager.RefreshStockForDuty` (proposed in menu+duty Plan F) can also surface personal-kit upgrade options. Buying upgrades is just `QualityStore.Set("kit.bedroll_level", N)` plus a `GiveGoldAction` cost.

**Sizing + risk.** **Medium.** ~3 quality slots, ~9 catalog entries (3 items × 3 levels), 1 tick handler, QM dialog integration, ~3 menu/dialog strings. **Risk: medium** — the tick-bonus integration point needs to coexist with whatever XP/health-regen subsystems already tick. Best to brainstorm content + integration before writing the plan.

---

### 3.3 Roll of Patrons (DEEPEST SECTION)

**CK3 source.** [Adventurer — Roll of Patrons](https://ck3.paradoxwikis.com/Adventurer): family members, friends, lovers, and rulers the adventurer has completed contracts for accumulate on a callable list. Patrons can be asked for **provisions, gold, knights, a random men-at-arms regiment, an arranged marriage, or another contract**. Each request typically has a per-patron cooldown and a per-favor-type cooldown. The patron's response weighs current opinion, prior service, and the patron's own state (dead patrons are removed).

**Why this is the highest-leverage import (within enlisted scope).** Today, when the player is discharged from one Lord and enlists with another, the prior service evaporates. Renown, lord_relation quality, and rank reset — the relationship is gone. Roll of Patrons makes service *permanent across enlistments within a single career*: the more lords you've served before retiring, the bigger your safety net while you're still soldiering. A T6 veteran with three former patrons across two factions is a meaningfully different character from a fresh T1 enlistee. The mod gains *long-tail emotional payoff* without changing the moment-to-moment loop. **Per §0 scoping, the Roll evaporates with the rest of the mod surface on full retirement** — CK3-style post-retirement persistence is explicitly traded for a cleaner mod boundary.

**Mod hosting system.**
- New `PatronRoll` class (Campaign behavior, persisted) holding `List<PatronEntry>` (NOT `Dictionary` — saveable container constraint per CLAUDE.md known issue #14; see also §4.1 ledger). Runtime dedup by MBGUID.
- New `PatronEntry` save-class: hero MBGUID, days-served, max-rank-reached, last-favor-time, alive-status-cache, relation-snapshot-on-discharge, factions-served-under (snapshot at discharge).
- **Discharge flow (between-Lord transitions, NOT retirement)** creates patron entries with snapshot data. Specifically: when `IsEnlisted` flips false but `EnlistmentBehavior.IsRetiring` is also false (i.e. mid-career discharge — fired, captured, lord lost, faction-switch), the entry is added. On *full retirement* (`IsRetiring == true`), the entire Roll is cleared as part of mod-silences-on-retirement teardown.
- Audience system integration: existing `OnTalkToSelected` at `EnlistedMenuBehavior.cs:3946` finds nearby lords. Extend to query `PatronRoll` first — if the selected lord is on the roll, surface a top-level "Call in a favor" option with the available favors as sub-options.
- News-feed integration: when a patron dies, gains/loses major holdings, or has a notable battlefield event, emit a patron-source news entry. (Note: typed-dispatch enums `DispatchSourceKind` etc. don't exist yet — see §4.4 dependency on news-v2 substrate; v1 emits direct-to-news.)
- **Lifecycle teardown**: patron entries clear cleanly via the existing `EnlistmentBehavior` teardown path (the live teardown at `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3640` clears active service state and fires post-teardown events — `PatronRoll.Clear()` hooks here). No post-retirement UI surface needed; the Roll is invisible after retirement because the mod surface is gone.

**Native APIs available.**
- `Hero.GetRelation(Hero)` at `Hero.cs:2025`, `Hero.SetPersonalRelation(Hero, int)` at `Hero.cs:2019` — relation read/write with internal clamp.
- `Hero.IsAlive` / `Hero.IsDead` at `Hero.cs:314` / `298` — death-state queries.
- `Hero.Clan` at `Hero.cs:494`, `Hero.MapFaction` at `Hero.cs:558`, `Hero.IsLord` at `Hero.cs:397`.
- `KillCharacterAction.ApplyByX(...)` variants at `KillCharacterAction.cs:170-220`. Hook the `OnHeroKilled` campaign event at `CampaignEventDispatcher.cs:612` to mark patron entries dead.
- `BarterManager` at `BarterManager.cs:13-309` — vanilla negotiation/trade substrate. Extends to custom `BarterContext` initializers; existing barters cover marriage, faction-join, peace. Could host favor-grant logic, OR favor-grant can skip Barter and use a simpler `Inquiry`/popup flow. **Recommend: skip BarterManager for v1.** Faster to author, easier to localize, fewer moving parts. Revisit if v2 needs negotiation flavor.
- `MBObjectManager` for stable Hero references across saves (use `Hero.MBGUID`, not Hero reference, in serialized state).

**State design — concrete.**
```
PatronRoll : Campaign behavior, save-definer offset 54 (see §4.1 ledger)
{
  List<PatronEntry> Entries;                    // List, not Dictionary — saveable container
  void OnDischarge(Hero lord, DischargeReason reason);
  void OnHeroKilled(Hero deceased);              // updates IsDead on matching entry
  void Clear();                                  // called during retirement teardown
  IEnumerable<PatronEntry> AvailableNearby();
  bool TryGrantFavor(MBGUID patronId, FavorKind kind, out FavorOutcome outcome);
}

PatronEntry : save-class, save-definer offset 55 (see §4.1 ledger)
{
  MBGUID HeroId;
  int DaysServed;
  int MaxRankReached;
  CampaignTime DischargedOn;
  CampaignTime LastFavorRequestedOn;
  string PerFavorCooldownsCsv;                   // CSV-encoded Dict<FavorKind,CampaignTime>; rebuilt on load
  int RelationSnapshotOnDischarge;
  bool IsDead;                                    // updated by OnHeroKilled
  string FactionAtDischarge;                      // by id, not reference
}

// Enum FavorKind requires DefineEnumType registration (see §4.1 ledger)
enum FavorKind { LetterOfIntroduction, GoldLoan, TroopLoan, AudienceArrangement, MarriageFacilitation, AnotherContract }
```

**Favor catalog — initial pool (~6 favors):**
| Favor | Effect | Patron requirement | Per-patron cooldown |
| :--- | :--- | :--- | :--- |
| Letter of introduction | Small renown bump in patron's faction; storylet hook for downstream introductions | Alive, opinion ≥ 0 | 30 days |
| Gold loan | One-time gold injection; tracked debt repaid against future wages | Opinion ≥ 10, gold to lend | 60 days |
| Troop loan | One or two hand-picked troops attach to the player's escort for 7 days | Opinion ≥ 20, party space | 90 days |
| Audience arrangement | Patron arranges a meeting with a third lord; opens the audience flow on that lord | Alive, opinion ≥ 5 | 45 days |
| Marriage facilitation | Patron introduces the player to a marriage candidate from their clan or court | Opinion ≥ 30 | 180 days |
| Another contract | Loops back into Side-contracts (if shipped) — patron offers a high-priority contract | Opinion ≥ 0 | 30 days |

**Cross-system collisions / dependencies.**
- **News-v2 substrate** (`DispatchSurfaceHint`, `DispatchSourceKind`, `BuildKingdomDigestSection`) — *not yet shipped* (verified in prior audit). Patron updates can emit *direct-to-news* without typed dispatch as a v1; once news-v2 ships, retroactively type them.
- **Plan 1 Campaign Intelligence Backbone** (shipped) — already tracks Lord intelligence. Roll of Patrons is a *different store* (player-relationship-history, not campaign-state-snapshot). When the player is currently enlisted with a former patron, intelligence + patron-status both apply: intelligence drives AI-relevant facts, patron-status drives social options. No conflict; they layer.
- **Audience system** at `EnlistedMenuBehavior.cs:3946` — extend with a patron-favor branch. Confirmed to exist and accept extension.
- **Hero-death lifecycle** — `KillCharacterAction.ApplyByX` is the canonical death entry. Subscribe `OnHeroKilled` to mark entries dead, NOT to remove them. Dead patrons stay on the roll (greyed) — the player should remember who they served.
- **Save-state migration** — adding `PatronRoll` mid-campaign requires `EnsureInitialized()` on the store (per CLAUDE.md known footgun #4: deserialization skips ctor and dict-property initializers). Implement the pattern proactively.

**Sizing + risk.** **Large.** ~2 new save classes (offsets 53 + 54), ~6 favor effects (each as a storylet), Audience-system extension, news integration deferred or simplified, retirement-flow integration, hero-death event handler. **Risk: medium-high.** The risk vectors are (a) patron-death lifecycle correctness, (b) save compat across long campaigns, (c) content authoring for favor outcomes (each favor needs success/fail/partial branches with localized text).

---

### 3.4 Side-contracts (Orders sibling/subsystem decision)

> **v3 NOTE (corrected v4):** Side-contracts and player-initiated Endeavors are **siblings to OrderActivity**, sharing the Activity backbone. Each gets its own Activity subclass and save-definer offset (`ContractActivity` 56, `EndeavorActivity` 57 — see §4.1 ledger). **Orders stays authoritative** — `OrderActivity` (shipped) owns duty-profile state, named-order arc state, and reconstruction code; no migration of Orders into Endeavor is proposed. The sibling-vs-subsystem question below answers: sibling. The shared backbone means lifecycle/serialization/storylet-pool plumbing is reused; the state classes stay distinct.

**CK3 source.** [Hired forces](https://ck3.paradoxwikis.com/Hired_forces) and [Adventurer](https://ck3.paradoxwikis.com/Adventurer): Mercenary / Faction / Bodyguard / Escort / Transport contracts offered by landed rulers and notables. Bread and butter income for landless adventurers. Each contract has a contractor (the issuing NPC), a task, success conditions, payment, and consequences for failure.

**Mod hosting system — and the load-bearing decision.**

**Question: are side-contracts a SIBLING of Orders, or a SUB-SYSTEM of Orders?**

- **Orders are top-down.** The player's commander tells the army what it's doing; the player has limited agency in choosing them.
- **Side-contracts are bottom-up.** A notable, an innkeeper, or a fellow soldier has a small problem. Pay scales with risk. Player picks freely.
- Different sources, different stakes, different cadence (Orders affect the army; side-contracts are personal).

**Recommendation: SIBLING.** Two distinct activities sharing the same Activity backbone (Spec 0):
- `OrderActivity` (already exists, save-definer offset 46) — Lord-issued, top-down.
- `ContractActivity` (new, save-definer offset 55) — notable-issued, bottom-up.

Both are `Activity` subclasses. Both run via `ActivityRuntime`. Both emit storylets. They differ in **intent** (the storylet pool the activity biases toward) and **source** (Lord vs notable). Treating them as siblings preserves the architectural cleanliness and avoids overloading the Orders system with two semantic flavors.

**Native APIs available.**
- `Settlement.Notables` at `Settlement.cs:275` — read-only collection of notable heroes per settlement.
- `IssueBase` and `IssueManager` — vanilla quest-issue infrastructure. `IssueBase` is the abstract base for all notable-issued quests; `IssueManager` orchestrates surfacing them.
- `Hero.IsNotable`, `Hero.Occupation` — filter notables by kind (gang leader, headman, merchant, artisan).
- Vanilla precedent: `EscortMerchantCaravanIssueBehavior` shows the pattern for an NPC-issued escort quest.
- `MapEvent` / `MobileParty` — execution mechanics for escort/delivery.

**API gap and workaround.** Vanilla `IssueBase` is **bound to specific named NPCs** — issues are properties of a notable, not free-floating job-board entries. We have two choices:
1. **Hijack `IssueBase`** — register custom `IssueBase` subclasses that surface as our contracts. The contract "issuer" is a real notable in the player's current settlement. This is the cleanest path; vanilla quest UI already handles tracking and completion.
2. **Build a parallel "ContractCatalog"** — a JSON catalog of contract templates that procedurally pair with whatever notables are nearby at offer-time. More flexible (we own the data layout), more code (we write the surfacing UI).

**Recommend option 1** for v1. Vanilla `IssueBase` is well-understood; reusing it keeps the player's mental model consistent ("this is just another quest") and keeps the contract UI free.

**State + save-definer needs.**
- `ContractActivity` (Activity subclass): save-definer offset **55**.
- `IssueBase` subclasses serialize via vanilla quest infrastructure — no extra offset.

**Cross-system collisions / dependencies.**
- **Heavy semantic overlap with Orders.** The sibling decision above resolves this; without it, the line between "Lord told you to escort this convoy" and "the convoy's owner asked you to escort it" gets fuzzy.
- **Vanilla quest UI** — if we hijack `IssueBase`, the player sees contracts in the same UI as vanilla quests. May be desirable (consistent) or undesirable (Enlisted contracts get lost). If undesirable, surface contracts in the Camp menu instead, with `IssueBase` as the back-end accountancy only.
- **Orders system** (Plan 2, mostly shipped) — the `OrderActivity` lifecycle (`OnStart`, `OnTick`, `OnComplete`) is a known good template for `ContractActivity`. Reuse aggressively.

**Sizing + risk.** **Large.** ~10-15 contract archetypes (each a `IssueBase` subclass with success/fail conditions), `ContractActivity` Activity-subclass plumbing, completion-reward flow integration, Camp/town surfacing UI. **Risk: medium.** The Orders-sibling question is the load-bearing decision; if we get it wrong, we either build a redundant system or merge incoherently with Orders.

---

### 3.5 Companion Aptitude

> **v3 NOTE:** Companion Aptitude is **subsumed by §3.10 Companion Substrate**. The audit confirmed the "aptitude" concept IS the vanilla `MobileParty.EffectiveSurgeon` / `EffectiveScout` / `EffectiveQuartermaster` / `EffectiveEngineer` system — assigning a high-skill companion to a role provides bonuses through models that already exist (`DefaultPartyHealingModel` etc.). No separate aptitude substrate to build; it ships free with companion role assignment via vanilla dialog.

**CK3 source.** [Adventurer Officers](https://ck3.paradoxwikis.com/Adventurer): camp followers fill jobs (Steward / Marshal / Spymaster / Bodyguard) and contribute aptitude bonuses scaled by their relevant skill — a high-Stewardship companion as Steward gives a bigger camp economy bonus than a low-Stewardship one. Aptitude is *role-relative*: the same companion's aptitude is high in one role, low in another.

**Mod hosting system.** Extends the menu+duty spec's Plan E (Support Duty role-claim) and the deferred Companions sub-menu (`CompanionAssignmentManager` exists at `src/Features/Retinue/Core/`). Plan E has the *player* claiming a party role; Companion Aptitude has *companions* contributing to a role. They're complementary: player's Duty role wins for primary slot; companions provide secondary aptitude bonuses scaled by skill.

**Native APIs available.**
- `Hero.GetSkillValue(SkillObject)` at `Hero.cs:1353` — read companion's role-relevant skill.
- `Hero.GetAttributeValue(...)` at `Hero.cs:1377` — for attribute-based aptitude (less likely needed).
- `MobileParty.LeaderHero` at `MobileParty.cs:761`, `MobileParty.MemberRoster` at `MobileParty.cs:1027`, `MobileParty.EffectiveSurgeon`/`EffectiveScout`/etc. at `MobileParty.cs:777-815`.
- **Direct precedent: `DefaultPartyHealingModel.GetSurgeryChance` at `DefaultPartyHealingModel.cs:47`** reads `EffectiveSurgeon.GetSkillValue(Medicine)` and scales the survival bonus. Companion Aptitude replicates this pattern: a model override that reads `companion.GetSkillValue(Medicine)` and applies a scaled bonus on top of (or instead of) the vanilla calculation.
- `PerkObject` at `PerkObject.cs:9-80` already includes `PartyRole`, `PrimaryBonus`, `SecondaryBonus`, `EffectIncrementType` — designed for role-based perks. The same machinery can be reused for aptitude bonuses if we want them as registered `PerkObject` instances.

**State + save-definer needs.**
- 0 new save-definer offsets. Companion → role assignments live in `CompanionAssignmentManager` (already shipped). Aptitude bonus is **computed**, not persisted — read companion's skill at bonus-application time.

**Cross-system collisions / dependencies.**
- **Plan E (menu+duty Plan E)** is the prerequisite. Plan E ships first; Companion Aptitude is a follow-up.
- **Vanilla healing/scout/quartermaster/engineer models** already read role-holder skills. We're either layering on top (extra bonus) or replacing them for the player's party. The replacement path conflicts with `DutiesOfficerRolePatches` from Plan E — coordinate to ensure both don't fire and double-apply.

**Sizing + risk.** **Medium.** 1-2 companion-role mappings (Surgeon ↔ Field Medic Duty, Scout ↔ Pathfinder Duty, etc.), 1-2 model overrides (e.g. extend `DefaultPartyHealingModel`), sub-menu UI for assignment in the Companions sub-menu. **Risk: low.** Fully on existing infrastructure; vanilla precedent is direct.

---

### 3.6 Lifestyle perk trees

**CK3 source.** [Lifestyle](https://ck3.paradoxwikis.com/Lifestyle): six lifestyles (Diplomacy / Martial / Stewardship / Intrigue / Learning / Wandering) × three focuses each × three perk trees per focus. Adventurers have a separate set of perk trees from landed rulers. ~20 perk points unlock across a 75-year life.

**Critical API gap.** Vanilla `PerkObject.RequiredSkillValue` at `PerkObject.cs:9-80` enforces that a perk takes effect only when the hero's relevant skill ≥ `RequiredSkillValue`. **`Hero.HeroDeveloper.AddPerk(perk)` at `HeroDeveloper.cs:317` does not bypass this gate** — the perk is "granted" but inert until the skill threshold is reached. There's no `Campaign.Current.Models.CharacterDevelopmentModel` override that exposes a "grant perk by XP/rank" hook; the model's interface (in `CharacterDevelopmentModel.cs`) doesn't include such a method.

This means the obvious port — "rank up → spend a perk point → unlock a numeric perk that takes effect immediately" — does NOT work cleanly with vanilla `PerkObject`. Three options:

**Option 1: Mod perks with `RequiredSkillValue = 0`.** Define all lifestyle perks as `PerkObject` instances with `RequiredSkillValue = 0`, granted via `AddPerk`. Effects take immediate effect because the gate is satisfied. **Constraint:** the perk must be tied to a `Skill` field anyway (`PerkObject` requires a `Skill`); the perk's effect logic is whatever `PerkObject` natively supports (party-role bonuses, primary/secondary bonus values). Limited expressiveness.

**Option 2: Parallel `DutyPerk` system.** Build a custom perk system unrelated to vanilla `PerkObject`. New save-class `DutyPerkStore` at offsets 56-58, custom effect application, custom UI. **More flexibility, more code, more substrate.** Effects can be anything (storylet hooks, quality bonuses, FlagStore unlocks, behavior gates).

**Option 3: Lifestyle as UNLOCKS, not perks.** Lifestyle progression unlocks new gameplay options — new menu options, new contract types, new Duty subtypes, new audience flows — rather than granting numeric bonuses. Same flavor (player makes a long-term commitment to a way of life), different mechanism (no `PerkObject`, no numeric bonuses). **80% of the value at 20% of the cost.**

**Recommend option 3 for v1.** "Adopt the Forager lifestyle → unlock the 'Live off the Land' menu option, the 'Wild Provisions' decision, and 50% reduced supply consumption while traveling." Same emotional payoff as a perk tree, none of the `PerkObject` gate-bypass risk. Numeric perks can come later as a Phase 2 if option 3 doesn't deliver enough mechanical depth.

**Mod hosting system (option 3).** New `LifestyleUnlockStore` (Campaign behavior, persisted) holding a `HashSet<string>` of unlocked feature ids. Hooked into `EnlistmentBehavior.SetTier`'s `OnTierChanged` event (already wired). Player picks a lifestyle path at T2 / T4 / T6 milestones; each milestone unlocks a chunk of the chosen path. Storylet triggers, menu options, and contract-template filters consult the store via a `TriggerRegistry` predicate (e.g. `lifestyle:forager` or `unlock:forager_provisions`).

**Note on `HashSet<T>` save gotcha.** Per CLAUDE.md known issue #14: `HashSet<T>` is **NOT a saveable container** in TaleWorlds SaveSystem. If we use a HashSet, we either serialize as `List<string>` with runtime dedup, or serialize-to-CSV string. The internal field can be a `HashSet`; the SaveSystem-facing field is a `List` or string.

**Native APIs available.**
- `EnlistmentBehavior.SetTier` `OnTierChanged` event — already wired.
- `TriggerRegistry.Register` for the new `lifestyle:*` and `unlock:*` predicates.
- `GameMenu.AddOption` `condition` delegate consults the store.

**State + save-definer needs.**
- `LifestyleUnlockStore`: 1 offset → **56** (or 57 if 56 used elsewhere).

**Cross-system collisions / dependencies.**
- **Plan B agency substrate** (StateMutator, Envelope) — only relevant if perks fire effects via storylet outcomes. Option 3 (unlocks) sidesteps this entirely; option 2 (parallel DutyPerk) might want it for safety; option 1 (RequiredSkillValue=0 PerkObject) doesn't need it.
- **Trait references** — option 3 unlocks can drift traits (Mercy/Valor/Honor/Generosity/Calculating) via storylet outcomes the unlock makes available. Option 1 PerkObjects can't drift traits (perks aren't trait grants).
- **Save-class HashSet gotcha** — covered above.

**Sizing + risk.** **Medium for option 3** (~3 lifestyle paths, ~3 milestones each, ~9 unlock-feature ids, integrated into existing trigger/menu/storylet pools). **Large for option 2** (parallel perk system, custom UI, effect application). **Risk: low for option 3, high for options 1/2.**

---

### 3.7 Officer Trajectory

**Concept.** Officer Trajectory is the upper-rank phase of the enlistment lifecycle — when the player crosses T6 or T7 (boundary TBD), the texture of being a soldier shifts. The player isn't a separate role; they're a *promoted* one. CK3's wanderer system has loose analogues (the Sword-for-Hire camp purpose, fame milestones), but the cleanest mental model is "knight in someone else's army" — still subordinate to the Lord, but socially distinct from the rank-and-file. Per §0 scoping, this happens *during* enlistment; retirement still silences the mod. The decompile audit (§6) confirms every officer-trajectory mechanic below has working native APIs.

**Why dedicated section.** Officer Trajectory cuts across all four CK3 subsystems audited (equipment / health / party / dialog). Rather than splitting findings across §3.1-§3.6 and losing the cohesive picture, this section synthesizes the cross-bucket recommendation: *what changes for the player when they make officer*.

**Mod hosting system.** Existing `EnlistmentBehavior` rank tiers + existing `RetinueManager` (already T7=20 / T8=30 / T9=40 gated for personal retinue size) + new `ItemModifier` registrations + new dialog branches at priority 110-115 + new formation rear-position offset hook in `EnlistedFormationAssignmentBehavior`. **No new save-definer offsets required for the officer-trajectory machinery itself** — it rides on existing rank state. (Stores claimed by other mechanics in this doc still apply when those mechanics ship.)

**Concrete officer deltas across four subsystems.**

#### Equipment side (§6.1 evidence)

- **Patron-named weapon modifier.** Register a runtime `ItemModifier` with name *"Crassus's Fine Steel Sword"* (TextObject with patron-name substitution). Registration via `MBObjectManager.Instance.RegisterObject<ItemModifier>(mod)` is open; private fields require reflection at construction time. Apply by reassigning `hero.BattleEquipment[Weapon0] = new EquipmentElement(existingWeapon, newModifier)`. Visible in tooltip immediately. Encodes patron + rank in the modifier's `StringId` for save-roundtrip identity.
- **Rank-escalating cape.** `EquipmentIndex.Cape = 9` is a real cosmetic slot. T1-3 plain cloak / T4-6 colored sash / T7-9 fur-trimmed officer's cape. Three to five `ItemObject`s, assigned on `OnTierChanged`. Purely cosmetic; cheap.
- **Banner item with formation aura.** Vanilla `BannerComponent` (extends `WeaponComponent`) carries `BannerLevel` and `BannerEffect` for captain morale auras. Officer at T5+ carries a banner item whose `BannerLevel` scales with rank. Encodes patron in StringId. Affects formation morale/cohesion in battle.
- **Mount upgrade.** `EquipmentIndex.Horse = 10`. Officer at T4+ unlocks T5+ mounts (Vlandian Charger / Empire Steppe Charger / etc.) via QM stock unlock — already fits the Plan F shop architecture.

#### Health/survival side (§6.2 evidence)

The vanilla wound system is **binary** (`Hero.IsWounded` at HP ≤ 20, `WoundedHealthLimit` hardcoded in `DefaultCharacterStatsModel.cs:13-15`). No graded injuries. So officer survival deltas come from healing-rate multipliers and battle-positioning, NOT from wound-severity reduction.

| Officer perk | Vanilla hook | Magnitude | Player feels |
| :--- | :--- | :--- | :--- |
| **Officer's Tent** (T7+) | Patch `DefaultPartyHealingModel.GetDailyHealingHpForHeroes` (`DefaultPartyHealingModel.cs:232-286`) | +6 HP/day on top of base +11 → **17 HP/day total** | Wounds heal in ~4 days vs ~6 |
| **Personal Surgeon** (T7+) | Wrap `GetSurgeryChance` (`DefaultPartyHealingModel.cs:45-49`) | +15% additive survival roll modifier | 20% → 35% chance to cheat death |
| **Officer Mess** (T7+) | Set party food variety +2 (`DefaultPartyMoraleModel.cs:63-130`) | +2 morale (direct) | Morale floor ~48 instead of 30 — troops don't grumble at supply shortage |
| **Bodyguard rear formation** (T6+) | Hook `EnlistedFormationAssignmentBehavior.TryTeleportPlayerToFormationPosition` (already exists at `:705-892`) | Spawn offset –5m from formation median | ~50% wound-risk reduction in auto-resolve battles |
| **Field Kit L3** (T1-T6, non-officer) | Patch healing formula | +4 HP/day → **15 HP/day** | 5-day recovery vs 6 — marginal but felt |
| **Bedroll L3** (any tier) | Boost food variety +1 | +1 morale | Prevents morale collapse on poor rations |

The asymmetry is intentional: officers get **hard survival deltas** (faster heal, surgery boost, rear position). Line soldiers get **morale-and-logistics deltas** (better sleep → cohesion → fewer reckless charges).

#### Party/command side (§6.3 evidence)

- **Officer's retinue lives in `MobileParty.MainParty` (the player's hidden+active party).** The decompile audit's `RetinueManager` review confirms the existing scaffold (T7=20 / T8=30 / T9=40 troop capacity) writes to `MainParty.MemberRoster.AddToCounts(...)`. The retinue troops are the **player's own roster**, NOT "flagged within the lord's party" (that earlier framing was wrong). When the lord enters a battle, MainParty activates and joins via `MapEventSide` (per `EnlistmentBehavior.cs:1142-1164`), and the entire MainParty roster — player hero + companions + retinue troops — spawns on the lord's side. This is least invasive (no new MobileParty), save-stable (vanilla TroopRoster serialization), and matches the user's mental model of "you command your own retinue while traveling with the lord's army."
- **Companion Aptitude (§3.5) layers cleanly.** Officer's clan companions assigned to roles (Pathfinder / Field Medic / Provisioner) contribute aptitude bonuses scaled by skill, mirroring `DefaultPartyHealingModel.GetSurgeryChance`'s pattern (which reads `EffectiveSurgeon.GetSkillValue(Medicine)` and scales). No new save-definer offset; bonus is computed at apply time.
- **Patron troop loan: clean lifecycle.** When a patron loans a knight or two for 7 days, use `AddCompanionAction.Apply(playerClan, hero)` to attach + `RemoveCompanionAction.ApplyByFire(playerClan, hero)` to detach on cooldown. The detach path makes the hero a Fugitive (`MakeHeroFugitiveAction`) and fires `OnCompanionRemoved`. **No save-state corruption**; clean cleanup confirmed in audit.

#### Dialog side (§6.4 evidence)

- **Rank-gated greetings at priority 110-115** (above vanilla 100). Innkeepers, fellow soldiers, peer officers, the Lord himself — each gets new `AddDialogLine` registrations that fire when `IsOfficer()` returns true. Pattern: `starter.AddDialogLine("inn_greet_officer", "start", "inn_hub", "Good evening, {PLAYER_RANK}. Word travels — you've made captain, haven't you?", () => IsOfficer() && InSettlement(), null, 110)`.
- **`MBTextManager.SetTextVariable` for substitution.** Conditions evaluate before render; set `{PLAYER_RANK}`, `{LORD_NAME}`, `{PATRON_NAME}` in the condition body (pattern already in use at `EnlistedDialogManager.SetCommonDialogueVariables` `:1360-1386`). NPCs literally call the player by their officer rank.
- **Patron favor branches at lord `lord_pretalk` entry state.** When a former patron is the conversation target, a "Call in a favor" option appears at priority 110 above vanilla greetings. Vanilla precedent: barter system uses the same delegate-driven pattern.
- **Side-contract branches at notable `notable_pretalk`.** Notables in settlements offer contracts via the same dialog-layering pattern; vanilla `IssueBase` infrastructure handles tracking and completion.
- **Peer officer professional conversations.** New dialog state tree (e.g. `officer_chat_hub`) for officer-to-officer professional exchange — gated on `IsOfficer() && TargetIsOfficer()`. New texture: discussing tactics, troop discipline, rivalry between commanders. Pure authoring, no API gap.

**State + save-definer needs.**
- **0 new save-definer offsets** for officer-trajectory itself. Rank state is in `EnlistmentBehavior`; retinue is in existing `RetinueManager`; gear modifiers are runtime-registered to MBObjectManager (which serializes by StringId).
- New ItemModifiers registered at session-launch (or on first relevant rank-up) — saves serialize correctly via vanilla MBObjectManager round-trip.

**Cross-system collisions / dependencies.**
- **Plan E (Support Duty role-claim)** in the menu+duty spec — officer's companion-aptitude bonuses layer on top of Plan E's role-claim. Coordinate so they don't double-apply.
- **`EnlistedFormationAssignmentBehavior`** — already shipped, already partially T7+ aware (line 458-461 mentions T7 commander tier gets own formation). The rear-position offset is an extension, not a rebuild.
- **News-feed + StoryDirector** — promotion-to-officer should fire a notable news entry. Plan A in the menu+duty spec handles the news-v2 substrate; until that ships, emit direct-to-news.

**Sizing + risk.** **Medium-large.** ~5 ItemModifier registrations (rank tiers), ~5 cape ItemObjects, 1-2 banner items, 4 model patches/wraps for survival deltas, 1 formation hook, ~10-15 dialog branches across NPC archetypes. **Risk: medium.** The biggest risk vector is the rear-position formation offset — modifying agent spawn positions can interact poorly with formations of varying size; smoke-test on small / medium / large army battles before committing. Rest of the work is precedent-driven.

---

### 3.8 Endeavor System (text-based player-driven activities)

**CK3 source.** Adapts CK3's [Schemes](https://ck3.paradoxwikis.com/Schemes) (multi-phase activities with agents + power + secrecy) and Roads to Power [Adventurer Contracts](https://ck3.paradoxwikis.com/Adventurer) (categorized text-based missions tied to Camp Purpose). CK3's wanderer loop runs on Major Decisions (long arcs) → Contracts (mid-scale missions) → Minor Decisions (filler). We adapt the middle and bottom layers as one unified system; the top layer is replaced by the rank ladder + ceremony arc (§3.9).

**Concept.** Within the army, the player can pursue **Endeavors** — text-based, multi-phase, archetype-themed activities. They sit alongside Lord-issued Orders (top-down) as the player-driven activity layer (bottom-up), all running on the existing `Activity` backbone (Spec 0, shipped). The system consolidates earlier proposals: **Adventurer Decisions** (§3.1) become single-phase minor endeavors; **Side-contracts** (§3.4) become notable-issued endeavor flavor; **player-initiated endeavors** are the new addition.

**Five categories.**

| Category | Skill axis | Example endeavors | Companion synergy |
| :-- | :-- | :-- | :-- |
| **Soldier** | One-Handed / Two-Handed / Athletics / Leadership | Win the regimental drill competition; teach a recruit your trade; earn the lord's notice in skirmishes | Veteran companion +speed |
| **Rogue** | Roguery / Charm | Run dice in camp; smuggle wine past the QM; pickpocket camp followers; sell looted goods | Field Medic+Charm or Veteran+Roguery; risk: scrutiny |
| **Medical** | Medicine / Steward | Forage herbs for wounded; tend the lord's sick page; learn poultice recipes | Field Medic companion massive bonus |
| **Scouting** | Scouting / Riding / Bow | Map terrain; track an enemy patrol; report on village morale | Pathfinder companion massive bonus |
| **Social** | Charm / Roguery | Befriend the Sergeant; sway the lord's opinion; court a camp follower | Companion-as-confidant relationship gain |

**Hybrid skill-OR-companion gating (decided v3).** A category unlocks when the player has either:
- **Sufficient personal skill** (e.g. Roguery ≥ 60 unlocks Rogue endeavors), OR
- **A companion of the matching archetype** in their clan (e.g. Field Medic companion unlocks Medical endeavors regardless of player Medicine).

This gives two paths to access: the player who builds Roguery themselves OR the player who recruits a Roguery companion can both run Rogue endeavors. Recruiting becomes a real strategic choice — companions aren't just numerically optimal, they *gate* whole activity categories.

**Endeavor lifecycle (per endeavor).**

1. **Selection** — Camp menu → Endeavor sub-option. Player picks one available endeavor (filtered by gating).
2. **Setup** — Choose 1-2 companions to assign as agents (locking them out of other endeavors during the run). Each agent's relevant skill contributes to potential, phase speed, and (for Rogue category) secrecy/scrutiny mitigation.
3. **Phases** — Every 2-3 in-game days, a phase event fires as a **player-choice modal**: text-based prompt with 2-3 choices. Each choice shifts progress + risk + skill XP. Cumulative across phases.
4. **Resolution** — At duration end (3-7 in-game days typical), final outcome event fires: success / partial / failure with concrete consequences (gold, lord_relation, scrutiny, trait drift, skill XP, follow-up flag).

**IMPORTANT: phase events use the canonical modal-popup pipeline (§6.8), NOT generic auto-phase storylet emission.** `ActivityRuntime.cs:267` auto-emits phase storylets through `StoryDirector` via `storylet.ToCandidate(ctx)` — that path produces ambient news entries, not modal player choices. Modal choices require setting `candidate.InteractiveEvent = evt` (the synthetic `EventDefinition` from `StoryletEventAdapter.BuildModal`) — see §6.8 for the full pipeline. Endeavor phases therefore need a dedicated **`EndeavorPhaseProvider`** modeled literally on `HomeEveningMenuProvider.OnSlotSelected` (`src/Features/Activities/Home/HomeEveningMenuProvider.cs:37`) — same shape, same flow. The Activity backbone handles lifecycle + state + cooldowns; the provider builds the modal via `StoryletEventAdapter.BuildModal` and emits via `StoryDirector.EmitCandidate`. **This is a thin wrapper, not new substrate** — the underlying pipeline is shipped.

**What we keep from CK3 schemes.**
- Phased progression with success chance climbing over time
- Skill-driven success rolls
- Companions as agents
- Category-based filtering matching player archetype
- Risk vs reward by category — Rogue endeavors carry **scrutiny risk** (the camp's "secrecy" analogue)
- Text-based resolution, no UI minigames

**What we drop from CK3.**
- 5-agent slot system (capped at 1-2 companions per endeavor)
- Spymaster-vs-spymaster math (no spymasters in our mod; difficulty fixed per endeavor)
- Court proximity scoring (no courts; agents are clan companions)
- Separate discovery vs execution-discovery (single scrutiny-risk number)

**Mod hosting system.**
- **`EndeavorActivity`** — new `Activity` subclass at save-definer offset 57 (see §4.1 ledger). Sibling to `OrderActivity` (Lord-issued, shipped) and `ContractActivity` (notable-issued, offset 56). All three share the Activity backbone; each owns its own state.
- **`EndeavorPhaseProvider`** — new provider class modeled literally on `HomeEveningMenuProvider.OnSlotSelected` (see §6.8 canonical recipe). Builds modal via `StoryletEventAdapter.BuildModal(s, ctx, owner)`, sets `candidate.InteractiveEvent = evt` + `ProposedTier = StoryTier.Modal` + `ChainContinuation = true` (for in-progress phases firing back-to-back), emits via `StoryDirector.EmitCandidate`. The synthetic `EventDefinition` carries the option block; `EventDeliveryManager.ShowEventPopup` renders the `MultiSelectionInquiryData` modal; `OnOptionSelected` drains Spec 0 effects via `StoryletEventAdapter.DrainPendingEffects`.
- **Endeavor catalog (JSON)** — `ModuleData/Enlisted/Endeavors/*.json`. Each entry: id, category, duration, skill axis, companion archetype slot(s), phase event definitions (with option blocks), resolution event definitions.
- **Companion-agent locking** — extend `CompanionAssignmentManager` with an "assigned-to-endeavor" flag (similar to existing Fight/Stay-Back flag).
- **Camp menu → Endeavors** — new sub-menu option with available-endeavor list.
- **Per-category cooldown** — `StoryDirector` `CategoryId` (`endeavor.soldier`, `endeavor.rogue`, etc.) for cooldown isolation between categories. Cooldowns apply at modal-emission time inside the provider.

**State + save-definer needs.**
- 1-2 new offsets (`EndeavorActivity` 56; optional `EndeavorCatalogState` 57).
- Companion-agent-assignment flags ride on `CompanionAssignmentManager`.

**Cross-system collisions / dependencies.**
- **Side-contracts (§3.4) merge in here** — notable-issued endeavors share the same machinery. The sibling-vs-subsystem question resolves: same backbone, different *source* (notable vs player vs lord-as-order).
- **Adventurer Decisions (§3.1) collapse to single-phase endeavors** — "Train alone tonight" is just a 1-phase Soldier endeavor with a single resolution storylet; no separate decision system.
- **`StoryDirector` pacing** — five new categories at 12-day cooldowns each. The category cooldown is per-endeavor-pool, not global, so different categories don't compete.
- **Companion Substrate (§3.10)** — load-bearing dependency. Without companions implemented, the gating model collapses to "skill-only" (option 2), losing the recruit-as-unlock dimension.

**Sizing + risk.** **Large.** New `EndeavorActivity` class (~200 LOC including phase storylet hosting), JSON catalog (~30-50 endeavors across 5 categories for adequate coverage), Camp menu integration, gating logic, scrutiny-risk computation for Rogue category. **Risk: medium-high.** Risk vectors: (a) endeavor-vs-Order conflict (player can't be on an Endeavor while on an active Order — needs explicit gate), (b) phase storylet pacing must respect existing DensitySettings rails, (c) Rogue scrutiny mechanics need careful balancing.

---

### 3.9 Rank-Ceremony Arc

**CK3 source.** CK3's lifecycle events (childhood education → coming-of-age → marriage → coronation) deliver character-defining choices at structural life-stage transitions. We adapt this onto Bannerlord's tier-based progression: each rank transition fires a single **character-defining storylet** with 2-3 options whose effects compound across the 9-tier ladder.

**Concept.** Eight ceremony events at tier transitions T1→T2 through T8→T9. Each is a CK3-style modal popup: 2-3 options, each carrying a `preview` block (per agency v3 envelope when shipped), choices drifting traits + setting flags that future events read.

**Hook point: `OnTierChanged` event, NOT the dialog branch (corrected v4).** Verified via `PromotionBehavior.cs:330-401`: T6→T7 specifically has THREE paths to promotion — (a) auto proving-event when requirements are met (`StoryDirector.EmitCandidate` at line 370), (b) decline-then-dialog-request when player previously declined the auto-event (gated by `EscalationManager.HasDeclinedPromotion(targetTier)` at line 345), (c) explicit dialog-request via `EnlistedDialogManager.cs:347-368`. T2-T5 and T8-T9 use only path (a). **Hooking the dialog branch alone misses paths (a) and (b) entirely; hooking the proving event alone misses path (c) and duplicates with the dialog flow.** The clean architecture: hook `EnlistmentBehavior.OnTierChanged` (`EnlistmentBehavior.cs:8490, 9799`). That fires regardless of which path triggered the promotion. Add a `_ceremonyFiredForTier[N]` dedup flag in `FlagStore` to guarantee single-fire per transition (cheap save state — bitfield serialization).

**Phase pattern.** Ceremony modal uses the canonical modal-popup pipeline (§6.8) — `CeremonyProvider` is modeled literally on `HomeEveningMenuProvider.OnSlotSelected` (`src/Features/Activities/Home/HomeEveningMenuProvider.cs:37`). Calls `StoryletEventAdapter.BuildModal(ceremonyStorylet, ctx, null /* not Activity-owned */)`, sets `candidate.InteractiveEvent = evt` + `ProposedTier = StoryTier.Modal` + `ChainContinuation = true` (promotion is a hard-trigger event that bypasses the 5-day in-game and 3-day category cooldowns; 60s wall-clock floor still applies), emits via `StoryDirector.EmitCandidate`.

**The eight ceremonies.**

| Transition | The question | Drift axis | Witnesses |
| :-: | :-- | :-- | :-- |
| T1→T2 | First combat survival — *who do you credit?* | Self-reliance vs trust-the-line | Sergeant + Veteran |
| T2→T3 | First raid share — *what do you do with the gold?* | Frugal / Generous / Hedonist / Family | Sergeant + fellow recruits |
| T3→T4 | A peer mocks you publicly — *fists, words, or report?* | Valor / Calculating / Honor | Sergeant + Officer NPC |
| T4→T5 | Lord orders something questionable — *obey, question, refuse?* | Mercy / Honor / Calculating | Lord + Veteran |
| T5→T6 | Company asks YOU to lead a small task — *take it or defer?* | NCO authority style | Sergeant + peer soldiers |
| **T6→T7** | **The Commission Ceremony** — Lord grants commander rank + 20 retinue (existing dialog branch) | Identity locks in from prior 5 choices | Lord + everyone (banner ceremony) |
| T7→T8 | Junior officer questions your tactical call — *authority, debate, or compromise?* | Officer leadership style | Officer NPC + retinue |
| T8→T9 | Young soldier asks you to mentor him — *take him on, distance, or use him?* | Marshal legacy texture | Veteran + Lord |

**Coherence rules.**
1. **Choice memory** — every choice writes a `FlagStore` flag (e.g. `ceremony.t2_t3.choice = "frugal"`). Future ceremonies' option text and condition gates read these flags. The Lord's framing at T6→T7 reflects accumulated choices.
2. **Trait compounding** — all eight ceremonies drift the same five vanilla traits (Mercy / Valor / Honor / Generosity / Calculating). By T9 the player has a measurable personality.
3. **Companion witnesses** — Sergeant / Officer / Veteran (from §3.10) show up across multiple ceremonies. **Their reactions drift via `ChangeRelationAction.ApplyPlayerRelation`** based on the player's choice — and vanilla's grievance system (§6.6) auto-fires future complaints when the right trait gate matches.
4. **Cultural variants** — each ceremony has 6 culture-flavor variants (Vlandian feast / Khuzait horse-test / Sturgian oath-stone / Aserai blade-blessing / Battanian woodland trial / Imperial hastatus-rite). Same decision, different texture.

**Mod hosting system.**
- **Single hook on `EnlistmentBehavior.OnTierChanged`** — fires after any of the three promotion paths land (auto proving-event, decline-then-dialog, dialog-request). New `RankCeremonyBehavior` subscribes; on each fire, checks `FlagStore.GetBool($"ceremony.fired.t{N}")` and short-circuits if already fired for this tier transition (handles edge cases like grace-period re-enlistment that could re-trigger the event).
- **`CeremonyProvider`** — modeled on `HomeEveningMenuProvider.OnSlotSelected`; builds modal via `StoryletEventAdapter.BuildModal`, sets `candidate.InteractiveEvent = evt` + `ProposedTier = StoryTier.Modal` + `ChainContinuation = true`, emits via `StoryDirector.EmitCandidate`. Thin wrapper around the shipped pipeline (§6.8) — no new substrate.
- T6→T7 ceremony **lives in the same hook** as all others; it's not augmenting the dialog branch — the dialog grants the rank, `OnTierChanged` fires, the ceremony provider runs, modal pops. (If the proving event already grants the rank, dialog branch is gated by `HasDeclinedPromotion` and won't double-fire.)
- Choice flags persist via existing `FlagStore`. Dedup flag (`ceremony.fired.t{N}`) lives in the same store.

**State + save-definer needs.**
- 0 new offsets — uses existing `FlagStore` and trait drift store.

**Cross-system collisions / dependencies.**
- **Companion Substrate (§3.10)** — witnesses are companions. If companions aren't shipped, ceremonies still work but feel less social.
- **Officer Trajectory (§3.7)** — T6→T7 ceremony is the natural moment to apply officer gear deltas (banner / cape / weapon modifier).
- **Endeavor System (§3.8)** — companions assigned to endeavors at the time of a ceremony can deliver post-ceremony reactions in their next dialog ("That choice at the trial — I respected it").

**Sizing + risk.** **Medium.** ~24 storylets total (8 ceremonies × ~3 cultural variants for adequate coverage), choice-memory flag wiring, trait drift bookkeeping, integration with existing rank-up promotion path. **Risk: low-medium.** Risk vectors: (a) flag-state proliferation (need conventions before authoring), (b) cultural-variant content burden (defer to post-Phase-1).

---

### 3.10 Companion Substrate (the v3 pivot)

**Concept.** Most "spawned army NPCs" become **vanilla companions in the player's clan, attached to MainParty** — not parked-in-settlement NPCs (the QM pattern). The audit confirmed companions in the player's hidden+active MainParty fight in the Lord's battles, can be killed, are withholdable from combat via existing UI, provide skill-aptitude bonuses through `MobileParty.EffectiveX`, are conversable + role-assignable via vanilla dialog, *and* express trait-gated preferences via the vanilla grievance system. The QM stays parked (non-combatant by design); patrons stay separate (former Lords, not your companions).

**Concrete companion roster.**

| Companion | Spawn timing | Skills | Vanilla role | Player benefit | Can die? |
| :-- | :-- | :-- | :-- | :-- | :-: |
| **Sergeant** | Enlistment (T1) | One-Handed 80, Leadership 60, Tactics 40, Valor +1 | (none — combatant) | Battle presence; discipline/morale dialog source; complains about retreats (Valor-trait gate) | yes |
| **Field Medic** | T3 unlock (or via patron favor) | Medicine 100, Steward 60, Mercy +1 | Surgeon | +30% surgery survival; +6 daily HP regen; complains about village raids (Mercy gate) | yes |
| **Pathfinder** | T3 unlock (or via patron favor) | Scouting 80, Riding 60, Calculating +1 | Scout | Scouting perks (terrain, spotting); pragmatic kingdom voter | yes |
| **Veteran** | T5 unlock (NCO band) | Bow/TwoHanded 80, Tactics 60, Honor +1, Valor +1 | (none — combatant) | Mentorship dialog; XP bonus from sparring; complains about retreats | yes |
| **Quartermaster Officer** | T7 unlock | Steward 80, Trade 60, Charm 40, Generosity +1 | Quartermaster | -10-25% food consumption; complains about unpaid wages and starvation | yes |
| **Junior Officer** | T7 unlock | Leadership 80, Tactics 60, Polearm 60, Honor +1 | (none — peer officer) | Officer-tier peer dialog; commands part of detachment | yes |

All in PlayerClan. All in MainParty. All conversable + role-assignable via vanilla dialog. All withholdable via the existing `CompanionAssignmentManager` toggle. All gain XP, level up, can be re-equipped, can die.

**The QM stays parked-in-settlement** (non-combatant by design — shopkeeper, not soldier). **Patrons stay separate** (former Lords with their own clan/party lifecycle).

**Why this beats parked-NPC pattern.**
- **Combat presence** — companions show up in battle, take risks alongside the player.
- **Mortality stakes** — death is real (`CampaignOptions.BattleDeath` controls; tunable via `DefaultDifficultyModel.GetClanMemberDeathChanceMultiplier`).
- **Skill aptitude bonuses are free** — vanilla `EffectiveX` system layered on `DefaultPartyHealingModel` etc. bonuses flow to MainParty regardless of hidden state.
- **Withhold UI already exists at T7+; tier-wide enforcement requires a 3-line gate removal.** `CompanionAssignmentManager.ShouldCompanionFight(hero)` is enforced at agent-spawn via `EnlistedFormationAssignmentBehavior.TryRemoveStayBackCompanion()` — but with a hard T7+ early-return at line 190. Tier-wide enforcement (needed for T1 Sergeant, T3 Field Medic) requires dropping the `EnlistmentTier < CommanderTier1` check.
- **Conversation + role assignment for free** — `CompanionRolesCampaignBehavior` provides "About your position in the clan…" dialog automatically.
- **Trait-gated preferences for free** — `CompanionGrievanceBehavior` fires complaints when player actions trigger + companion has the matching trait. We just bake personality at spawn (see Sergeant Valor+1, Field Medic Mercy+1, etc.) and vanilla auto-reacts.

**State + save-definer needs.**
- 0 new offsets for the heroes themselves — TaleWorlds Hero serialization handles persistence automatically once `AddCompanionAction.Apply(playerClan, hero)` is called.
- Mod-side metadata: which hero is the Sergeant, which is the Field Medic, archetype tags. ~5 string/int fields in `EnlistmentBehavior.SyncData`.
- `CompanionAssignmentManager` (existing) extended for endeavor-agent assignment flag.

**Cross-system dependencies.**
- **Officer Trajectory (§3.7)** — Junior Officer + QM Officer companions unlock at T7 alongside the gear deltas; ceremony at T6→T7 introduces them.
- **Endeavor System (§3.8)** — companions are agents within endeavors; their archetype gates which categories the player can run.
- **Rank Ceremonies (§3.9)** — companions are witnesses; their trait-driven grievance reactions to ceremony choices give the social-fabric texture.

**Open design decisions.**
1. **Per-Lord or per-player?** Lean: **per-player Sergeant + Field Medic + Pathfinder** (your retainers — emotional continuity); **per-Lord Veteran + Officers** (the army's people). Recruit recruits per-Lord; senior NPCs follow you.
2. **Spawn-fresh or recruit-from-pool?** Lean: spawn-fresh for archetypes with specific skill profiles (Field Medic with Medicine 100 doesn't exist in vanilla wanderer pools); allow Lord's existing companions to also fill peer-officer slots.
3. **Customization at spawn — fixed or rolled?** Lean: **rolled archetype like the QM's 6 personalities** (veteran/merchant/bookkeeper/scoundrel/believer/eccentric) — gives texture without massive content authoring.
4. **Retinue cap interaction.** Existing `RetinueManager` caps T7=20 / T8=30 / T9=40 anonymous troops. Named companions (max ~6) layer on top, NOT against the retinue cap. They count as Heroes, not roster troops.
5. **Death-rate tuning.** Accept player's `CampaignOptions.BattleDeath` setting; consider an additional age-based modifier so older companions are more vulnerable (CK3-style life-cycle).

**Sizing + risk.** **Medium-large.** ~6 companion spawn recipes (each a `HeroCreator.CreateSpecialHero` call + post-spawn customization), JSON personality archetype catalog, conversation extension for endeavor-related branches (vanilla covers role-assignment), endeavor-agent assignment flag in `CompanionAssignmentManager`. **Risk: low-medium.** The audit confirmed every load-bearing question (combat participation, withhold, aptitude flow, dialog) — no API gaps remain. Risk vectors: (a) per-Lord vs per-player decision needs explicit playtest validation, (b) companion-death + grievance-system interaction needs a UX pass (don't let a dying-Field-Medic fire its last grievance on the same tick).

---

## 4. Cross-cutting concerns

### 4.1 Save-definer offset ledger (corrected v4 — concrete, no hedging)

Live registrations stop at offset 50 (`DutyCooldownStore`, per `EnlistedSaveDefiner.cs:63`). Every new offset claim below is concrete — no "or" hedging across mechanics. Container types and enum types each get explicit `DefineEnumType` / `DefineGenericClassDefinition` calls.

**Class offsets (51-70 reserved cluster — see recommendation below):**

| Offset | Owner | Mechanic | Status |
| :---: | :--- | :--- | :--- |
| 51 | `DutyActivity` | Menu+duty Plan C | Reserved by menu+duty spec |
| 52 | `ChoreThrottleStore` | Menu+duty Plan B | Reserved by menu+duty spec |
| 53 | `PersonalKitStore` | §3.2 Personal Kit (skip if folding into `QualityStore` slots — pick one and own the choice) | Optional but exclusive |
| 54 | `PatronRoll` | §3.3 Roll of Patrons | Required |
| 55 | `PatronEntry` | §3.3 Roll of Patrons | Required |
| 56 | `ContractActivity` | §3.8 Endeavor System (notable-issued sibling) | Required |
| 57 | `EndeavorActivity` | §3.8 Endeavor System (player-issued sibling) | Required |
| 58 | `LifestyleUnlockStore` | §3.6 Lifestyle (unlocks version) | Required |
| 59 | `RankCeremonyState` | §3.9 Rank-Ceremony Arc — holds dedup flags + per-tier choice records (could fold into existing `FlagStore` + `QualityStore` instead — pick one) | Optional but exclusive |
| 60-70 | RESERVED for future Activity-and-related | Specs 3-5 (Land/Sea / Promotion+Muster / Quartermaster) | Reserve range expansion |

**Enum offsets (80-89 enum range — `FavorKind` claims one):**

| Offset | Owner | Notes |
| :---: | :--- | :--- |
| 84 | `FavorKind` (§3.3) | Enum: LetterOfIntroduction / GoldLoan / TroopLoan / AudienceArrangement / MarriageFacilitation / AnotherContract |

(Spec 0 holds 82-83 per AGENTS.md; offsets 80-81 + 85+ are free.)

**Container types — required explicit registrations:**

Per CLAUDE.md known issue #14, `HashSet<T>` is NOT saveable. Use `List<T>` with runtime dedup or CSV-encoded strings. Specifically:
- `LifestyleUnlockStore` uses `List<string>` (NOT `HashSet<string>`) for unlocked feature ids; runtime dedup.
- `PatronRoll` uses `List<PatronEntry>` (NOT `Dictionary<MBGUID, PatronEntry>`); runtime dedup by MBGUID.
- `PatronEntry.PerFavorCooldownsCsv` is a CSV-encoded string (NOT `Dictionary<FavorKind, CampaignTime>`); rebuilt into a runtime dict on load.
- Where `Dictionary<,>` IS used (e.g. `CompanionAssignmentManager._companionBattleParticipation`), the dict's value-type and key-type need to be saveable primitives — string/int/bool. No nested complex types.

**Total new offsets if all mechanics adopted:** 7 class + 1 enum = 8 new registrations. Reserved cluster 51-70 holds with 11 free slots remaining for Specs 3-5.

**Recommendation:** amend AGENTS.md Rule #11 to expand the reserved Activity-and-related cluster from 51-60 to **51-70** before any plan claims an offset above 60.

### 4.2 News feed + StoryDirector load

- **Adventurer Decisions / Personal-kit / Companion Aptitude** add 0 news-feed items (player-driven, immediate-effect, no narrative emission).
- **Roll of Patrons** adds occasional news items (patron death, patron military victory/defeat, patron clan extinction) — needs a `DispatchSourceKind=Patron` if news-v2 substrate ever ships. v1 can emit direct-to-news without typed dispatch.
- **Side-contracts** surfaces in town/camp menus and (if hijacking `IssueBase`) the vanilla quest log. No news-feed entries by default; completion outcomes may emit recap items.
- **Lifestyle (unlocks)** adds rank-up notifications when a lifestyle milestone unlocks features.

**StoryDirector category load.** Each new mechanic with a Modal storylet pool wants its own `CategoryId` for cooldown isolation. Estimated additions: `decision.player_initiated` (Decisions), `patron.event` (Patrons), `contract.completion` (Contracts), `lifestyle.unlock` (Lifestyles). Combined with the menu+duty spec's `duty.event` and Plan 4's army-situation categories, total ~12 categories. `DensitySettings` rails handle this fine; the only new consideration is the `duty.event` *single-category-starvation* concern flagged in the menu+duty audit (§spec amendments) — that concern doesn't deepen here.

### 4.3 Namespace + Plan-4 collision

New types should land in:
- `Enlisted.Features.AdventurerDecisions` — Adventurer Decisions
- `Enlisted.Features.PersonalKit` — Personal-kit
- `Enlisted.Features.Patrons` — Roll of Patrons
- `Enlisted.Features.Contracts` — Side-contracts
- `Enlisted.Features.CompanionAptitude` — Companion Aptitude (or extend existing `Enlisted.Features.Retinue`)
- `Enlisted.Features.Lifestyles` — Lifestyle unlocks

None collide with Plan-4's `Enlisted.Features.Activities.Orders` or `Enlisted.Features.CampaignIntelligence.Duty`. None collide with the (TBD) Plan-C namespace for the menu+duty spec — but the menu+duty spec should pick its namespace explicitly to avoid a future collision (already flagged in that spec's amendments).

### 4.4 Dependencies on unbuilt substrate

| Dependency | Status | Affects |
| :--- | :--- | :--- |
| News-v2 (`DispatchSurfaceHint`, `DispatchSourceKind`, `BuildKingdomDigestSection`) | **Unshipped** | Roll of Patrons news integration (deferrable — emit direct-to-news in v1) |
| Plan B agency substrate (`StateMutator`, `Envelope`, `AgencyGate`) | **Unshipped** | Lifestyle perks option 1/2 (option 3 unlocks sidesteps); Personal-kit XP cap (deferrable) |
| Plan C `DutyManager` | **Unshipped** | Personal-kit shop refresh and Companion Aptitude both want "what Duty is the player on" (deferrable — fall back to equipment-derived) |
| Storylet schema extensions (`agency.role`, `preview`, `duty_id`) | **Unshipped** | Decisions, Contracts, Patron favors all use storylets; can author with bare schema in v1, retro-fit later |
| Validator phases for Plan G rails | **Unshipped (Phase 16+)** | All six mechanics' new content; v1 ships with warnings only |

**Net read:** every mechanic in this analysis can ship in a v1 form with **no upstream substrate blockers** as long as we accept some interim simplifications (direct-to-news instead of typed dispatch, bare storylet schema, no envelope safety on effects, no Phase-16 preview validation). Substrate retro-fits are tractable later.

### 4.5 Travel-event terrain awareness

Terrain-aware ambient events are content-authoring, not a separate plan. They live inside the storylet pool authoring already designed by Plan G of the menu+duty spec. New `TriggerRegistry` predicates needed: `terrain:mountain`, `terrain:forest`, `terrain:steppe`, `terrain:desert`, `terrain:plain`. Each is a wrapper over `Settlement.GatePosition.GetTerrainType` or `Campaign.Current.MapSceneWrapper.GetFaceTerrainType`. Tiny additions to `TriggerRegistry`, not a new feature.

### 4.6 Native preference integration — what's free vs what we add

The audit found vanilla provides a real but **narrow** companion-preference surface. We get the following for free, just by setting companion traits at spawn:

**Free from vanilla:**
- **Trait-gated grievance complaints** — `CompanionGrievanceBehavior` fires when an event triggers AND the companion has the matching trait: Valor+ → complains about retreats, Generosity+ → complains about starvation/unpaid wages, Mercy+ → complains about village raids. Persona traits modify dialog tone (Curt/Earnest/Ironic/Softspoken) but not logic.
- **Kingdom decision voting weights** — clan members vote with +20 per Valor (pro-war) and -10 per Mercy (anti-war) on `DeclareWarDecision`. Player-clan companions become a voting bloc.
- **Auto-leave threshold** — relation < -10 triggers `KillCharacterAction.ApplyByRemove`. Hard floor; no gradual exit.
- **Relation drift on grievance response** — accept (+10), dismiss (-2 to -5), reject (-15) — vanilla applies these without our code.

**What we add for the rank-ceremony arc and endeavors:**
- **Auto-cascading reactions** — vanilla doesn't ripple "player executed a noble" to all Mercy+ companions. Mod-side: each ceremony/endeavor outcome calls `ChangeRelationAction.ApplyPlayerRelation(companion, +/-N)` for each relevant companion based on their traits. Vanilla then fires grievances correctly afterward.
- **Ceremony-aware dialog branches** — Sergeant's post-ceremony dialog reads our flag (`ceremony.t4.choice == "obey_anyway"`) and surfaces a custom comment. Vanilla won't auto-react to ceremony outcomes; we register dialog conditions reading our state.
- **Endeavor-outcome reactions** — companion as agent on a failed Rogue endeavor → relation drift + scrutiny + flag set → next time the player talks to them, custom dialog branch fires.

Net: we plumb the *triggers* to call `ChangeRelationAction` and set flags; vanilla plumbs the *consequences* (grievance gates, voting). About 60% of the reactivity layer is already wired; we add the trigger plumbing and ceremony-aware dialog conditions.

---

## 5. Decision sheet

| Mechanic | Commit-to | Give up if skipped | Prereqs | Size | Risk |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Adventurer Decisions** (§3.1) | 6-12 menu options + 12-24 storylets + cooldown flags + `decision.*` storylet category | Camp menu stays static; no player-initiated downtime activities; downtime feels passive | Menu+duty Plan A (Camp menu shell — soft) | **S** | Low |
| **Personal-kit** (§3.2) | 3 quality slots OR new `PersonalKitStore` at offset 53; ~9 catalog entries (3 items × 3 levels); QM dialog hook; tick-bonus integration | No "spend wages between gear buys" gold sink; no compounding minor passive bonuses | QM shop (already a shop); save offset 53 (optional); brainstorm session before plan | **M** | Medium (tick-bonus integration) |
| **Roll of Patrons** (§3.3) | `PatronRoll` + `PatronEntry` at offsets 53-54 (or 54-55); 6-favor catalog (each as a storylet); Audience-system extension; news integration (deferrable); retirement-flow integration; `OnHeroKilled` handler | No long-tail relationship payoff; rank of service has no after-effects; post-retirement campaign feels disconnected; player has no social safety net across enlistments | News-v2 *desirable but not blocking*; Audience flow at line 3946 | **L** | Medium-high (death lifecycle, save compat, content authoring volume) |
| **Side-contracts** (§3.4) | `ContractActivity` at offset 55-56; ~10-15 contract archetypes (each an `IssueBase` subclass); notable-interaction surface; completion-reward flow | No bottom-up agency; player only acts on Lord's directive; settlements feel passive between enlistment beats | Activity backbone (Spec 0, shipped); `Settlement.Notables` API; **architectural decision: sibling vs subsystem of Orders** | **L** | Medium (Orders-sibling question is load-bearing) |
| **Companion Aptitude** (§3.5) | 1-2 companion-role mappings; 1-2 party-model overrides; sub-menu UI in Companions; precedent-driven (mirrors `DefaultPartyHealingModel`) | Companion sub-menu ships as a passive list; companions are name-only, no mechanical contribution | Plan E (Support Duty role-claim) | **M** | Low |
| **Lifestyle unlocks** (§3.6 option 3) | `LifestyleUnlockStore` at offset 56-57; 3 lifestyle paths × 3 milestones × ~3 unlock features each (~27 features total); `lifestyle:*` and `unlock:*` `TriggerRegistry` predicates; rank-up reward hook | No long-term character progression beyond rank+wages; T2/T4/T6 milestones lack mechanical resonance | `OnTierChanged` event (already exists); save-class `HashSet` gotcha | **M** | Low |
| Lifestyle **perks** version (option 1 or 2) | (deferred; revisit if option 3 doesn't deliver depth) | Same as unlocks-version + numeric perk bonuses | Plan B (StateMutator for safe effects) OR custom effect application; PerkObject extension | **L** | High (PerkObject API gap) |
| **Officer Trajectory** (§3.7) | 0 new save offsets; ~5 ItemModifier registrations + reflection helper; ~5 cape items; 1-2 banner items; 4 model patches/wraps (healing rate, surgery survival, food variety, formation rear-position); ~10-15 dialog branches at priority 110-115 | T6/T7 promotion is purely numeric (rank+wages+QM unlock); no visible/social/survival differentiation between line soldier and officer | Existing `EnlistmentBehavior` rank state, existing `RetinueManager`, existing `EnlistedFormationAssignmentBehavior`; Plan E (Support role) for companion-aptitude layering | **M-L** | Medium (rear-position formation hook; ItemModifier reflection construction) |
| **Endeavor System** (§3.8) | `EndeavorActivity` at offset 56 (and side-contract `ContractActivity` at 55 merges in); ~30-50 endeavor templates across 5 categories (JSON); Camp menu Endeavor sub-option; hybrid skill-OR-companion gating logic; companion-agent locking flag in `CompanionAssignmentManager`; scrutiny-risk computation for Rogue category | No bottom-up player agency; player only acts on Lord's directive; downtime is passive | Activity backbone (Spec 0, shipped); `StoryDirector` pacing rails (per-category cooldowns); `Companion Substrate` (§3.10) for the gating model | **L** | Medium-high (endeavor-vs-Order conflict gate; phase pacing tuning; Rogue scrutiny balancing) |
| **Rank-Ceremony Arc** (§3.9) | 0 new save offsets; ~24 storylets (8 ceremonies × ~3 cultural variants for adequate coverage); choice-memory flags; trait drift bookkeeping; integration with rank-up promotion path; companion witness dialog branches | Rank-up is a numeric event; no character-defining moments; 9-tier ladder is procedural progression with no narrative texture | Existing `FlagStore`, existing trait drift, existing `EnlistmentBehavior.SetTier`, `EventDeliveryManager` proving-event slot at each tier-up; `Companion Substrate` (§3.10) for witness companions | **M** | Low-medium (flag-state proliferation; cultural-variant content burden) |
| **Companion Substrate** (§3.10) | 0 new save offsets for heroes (TaleWorlds Hero serialization is automatic); ~5 metadata fields in `EnlistmentBehavior.SyncData`; ~6 companion spawn recipes (`HeroCreator.CreateSpecialHero` + post-spawn customization); JSON personality archetype catalog (mirroring QM's 6-archetype model); endeavor-agent flag in `CompanionAssignmentManager`; conversation extension for endeavor-related branches (vanilla covers role-assignment) | No army NPC density beyond Lord+QM; no skill-aptitude bonuses to player party; no trait-gated reactions to player actions; CK3-style preferences/grievances entirely absent | Existing QM precedent (`EnlistmentBehavior.GetOrCreateQuartermaster`); existing `CompanionAssignmentManager`; existing `RetinueManager`; vanilla `CompanionRolesCampaignBehavior` (role assignment); vanilla `CompanionGrievanceBehavior` (preferences); `RetinueManager` capacity gates | **M-L** | Low-medium (per-Lord vs per-player decision needs playtest validation; companion-death + grievance-system interaction needs UX pass) |

---

## 6. Native API findings — tangible-effect audit

This section is the evidence layer for §3 and §3.7. A four-bucket decompile audit answered: *for each CK3 mechanic, how can it manifest as a vivid, in-game-felt effect via existing TaleWorlds APIs?* Findings are organized by subsystem; mechanics in §3 reference these subsections.

### 6.1 Equipment subsystem — Lord/patron favor and rank as durable gear

**Load-bearing answer: `ItemModifier` registration is OPEN.** Mods can register new `ItemModifier` instances at runtime via `MBObjectManager.Instance.RegisterObject<ItemModifier>(mod)` (`MBObjectManager.cs:446`). Modifiers are added to `ItemModifierGroup`s via `AddItemModifier()` (`ItemModifierGroup.cs:51`). **Caveat:** the modifier's properties (`Name`, `Damage`, `Speed`, `Armor`, etc.) are private with only `Deserialize()` as the setter path — runtime construction needs reflection or a helper that mirrors the deserialization. Subclassing is impossible (sealed). Verdict: viable with a small reflection-based factory.

**Item provenance.** `EquipmentElement` carries `Item`, `ItemModifier`, `IsQuestItem` — no first-class "GivenBy" field. Encode provenance in the `ItemModifier.StringId` (e.g. `"lord_gifted_crassus_rank_3"`) and parse at display time. Save round-trip preserved (modifiers serialize by StringId at `EquipmentElement.cs:462-465`).

**Banners + capes.** `EquipmentIndex.Cape = 9` is a real cosmetic slot (`EquipmentIndex.cs:20`). Banner items (`ItemTypeEnum.Banner`) carry a `BannerComponent` (`BannerComponent.cs:7-48`) with `BannerLevel` and `BannerEffect.GetBannerEffectBonus()` for captain-aura formation bonuses. Both are mod-grantable by direct slot assignment.

**Crafting custom weapons.** `Crafting.CreatePreCraftedWeaponOnDeserialize()` (`ItemObject.cs:469`) is the construction path; full runtime crafting is complex (`WeaponDesign` + `CraftingTemplate` + `CraftingPiece` machinery). **Cheaper alternative:** use existing base weapons + custom `ItemModifier` to get the patron-named display ("Crassus's Fine Steel Sword") via `EquipmentElement.GetModifiedItemName` (`Equipment.cs:266`). Recommended for v1.

**Battle vs civilian equipment.** `Hero._battleEquipment` (`Hero.cs:208`) and `_civilianEquipment` (`Hero.cs:215`) are distinct sets, both saveable. Officer at T7+ can have a distinct "court dress" civilian set with insignia.

**File:line index for §6.1:**
- `ItemModifier.cs:10-177` — class structure
- `MBObjectManager.cs:446-450` — RegisterObject<T>
- `ItemModifierGroup.cs:9-86` — AddItemModifier
- `EquipmentElement.cs:9-476` — slot assignment + GetModifiedItemName at :266
- `EquipmentIndex.cs:3-26` — slot enum
- `BannerComponent.cs:7-48` — banner stats
- `Hero.cs:207-220` — Battle/Civilian equipment fields
- `Crafting.cs:10-250` — crafting pipeline (use modifiers instead)

### 6.2 Health subsystem — Sleep/diet/kit as survival deltas

**Load-bearing answer: vanilla wounds are BINARY.** `Hero.IsWounded` is a simple boolean computed from `HitPoints ≤ WoundedHealthLimit`, hardcoded at 20 HP (`DefaultCharacterStatsModel.cs:13-15`, `Hero.cs:329`). **No graded-injury system exists.** No traits like `Wounded`, `Disabled`, `Maimed`, `Scarred`. Heavy escalation goes to `MakeHeroFugitive`. Once HP recovers above 20, the hero is mechanically identical to pre-injury — **no persistent stat penalty.**

**Implication:** Field Kit cannot reduce wound *severity* in vanilla; it can only modify *healing rate* and *survival roll*. Personal-kit benefits are realized through HP-per-day multipliers and food-variety morale, not graded injury reduction.

**Healing rate hooks.** `DefaultPartyHealingModel.GetDailyHealingHpForHeroes` (`DefaultPartyHealingModel.cs:232-286`) is the canonical entry: base **+11 HP/day** for heroes (line 250), with multipliers for starvation (×0.5–0.25), morale, perks, settlement fortification (+8 HP). Patch this for Field Kit / Officer's Tent additive bonuses.

**Surgery survival precedent.** `GetSurgeryChance` (`DefaultPartyHealingModel.cs:45-49`) reads `EffectiveSurgeon.GetSkillValue(DefaultSkills.Medicine)` and scales `0.0015f * skill`. This is the exact pattern Companion Aptitude (§3.5) and Personal-kit Field Kit replicate. `GetSurvivalChance` (`DefaultPartyHealingModel.cs:61-115`) is the post-battle death-save roll.

**No hero-level Stress.** Bannerlord has only **collective party morale** (`MobileParty.Morale`, `MobileParty.cs:878`) computed via `DefaultPartyMoraleModel.GetEffectivePartyMorale` (`DefaultPartyMoraleModel.cs:215-246`). Base 50, starvation –30, no-wage –20, food variety –2 to +10. No CK3-style individual stress drift; "Bedroll level 3" expresses as +1 food variety = +1 party morale.

**Skill XP API.** `Hero.HeroDeveloper.AddSkillXp(SkillObject, float, bool isAffectedByFocusFactor = true)` (`HeroDeveloper.cs:189-213`). `SkillLevelingManager.OnCombatHit` is the per-hit XP grant; mod can layer Sharpening Stone on top. **No vanilla hourly-tick passive XP.**

**No trait-XP API.** `DefaultTraits.Mercy/Valor/Honor/Generosity/Calculating` are stored in `Hero._heroTraits` but have no leveling mechanism. Drift system is mod-owned. Use a custom store keyed by `TraitObject`; emit a custom event on change for toast/journal visibility.

**Food.** `DefaultMobilePartyFoodConsumptionModel` (`:18-97`): daily party consumption is `–(party_size / 20)`. No per-hero food penalty — only party-wide starvation (–19 HP/day to heroes if party starves). Officer "personal mess" effect must wire to retinue-only consumption modeling, not individual food.

**Battle positioning.** No vanilla rear-position spawn for heroes. `MissionAgentSpawnLogic.SpawnTroopsOfFormation` and formation-median computation determine spawn. Mod's existing `EnlistedFormationAssignmentBehavior.TryTeleportPlayerToFormationPosition` (`:705-892`) is the right hook for officer-rear-offset (`spawnPos = formation.MedianPos + (-formation.Direction * 5m)`).

**File:line index for §6.2:**
- `Hero.cs:329` — IsWounded
- `DefaultCharacterStatsModel.cs:13-15` — WoundedHealthLimit constant
- `DefaultPartyHealingModel.cs:45-49` — GetSurgeryChance
- `DefaultPartyHealingModel.cs:61-115` — GetSurvivalChance
- `DefaultPartyHealingModel.cs:232-286` — GetDailyHealingHpForHeroes
- `DefaultPartyMoraleModel.cs:215-246` — GetEffectivePartyMorale
- `DefaultMobilePartyFoodConsumptionModel.cs:18-97`
- `HeroDeveloper.cs:189-213` — AddSkillXp
- `EnlistedFormationAssignmentBehavior.cs:705-892` (mod) — TryTeleportPlayerToFormationPosition

### 6.3 Party subsystem — Personal escort lifecycle

**Load-bearing answer: hero loan cleanup is CLEAN.** `AddCompanionAction.Apply(Clan clan, Hero hero)` binds the hero to the player's clan; `RemoveCompanionAction.ApplyByFire(Clan clan, Hero companion)` detaches with guaranteed cleanup — removes from `MemberRoster`, transitions to Fugitive via `MakeHeroFugitiveAction.Apply` (`:26`), fires `OnCompanionRemoved` (`:56`). **No save-state corruption risk.** A 7-day patron loan is a daily-tick timer that calls `ApplyByFire` on expiry.

**Spawned NPC parties for escorts.** `CustomPartyComponent.CreateCustomPartyWithTroopRoster(position, spawnRadius, homeSettlement, name, clan, troopRoster, prisonerRoster, owner, ...)` (`CustomPartyComponent.cs:202`) spawns AI-controlled parties. Vanilla precedent: `EscortMerchantCaravanIssueBehavior` (`:666`, `:1281-1293`) spawns the escort-target party, locks AI with `Ai.SetDoNotMakeNewDecisions(true)`, directs via `SetPartyAiAction.GetActionForEngagingParty` and converts back to vanilla caravan on quest finalize. **No "follows player" AI out-of-the-box** — script via daily ticks.

**Troop additions to player escort.** `MobileParty.MemberRoster.AddToCounts(CharacterObject, int count, isHero, ...)`. Existing `RetinueManager` (`src/Features/Retinue/Core/RetinueManager.cs:21-93`) caps T1-T6 = 0, T7 = 20, T8 = 30, T9 = 40. Party-size cap from `DefaultPartySizeLimitModel.GetPartyMemberSizeLimit` (`:81-97`) — overrideable by mod.

**Officer sub-party verdict: PATH A.** Three options were:
- **Path A:** logical grouping of troops within main party, no second `MobileParty`. Lowest-risk, save-stable, matches the enlistment mental model.
- **Path B:** spawn a real `LordPartyComponent` second party. Invokes vanilla clan-mechanic collisions; player isn't naturally a clan leader.
- **Path C:** spawn `CustomPartyComponent` as detachment, lock AI to follow main party. Most fragile.

**Path A wins** for the officer-trajectory implementation. Extends the existing `RetinueManager` scaffold without inventing parallel infrastructure.

**Friendship/rivalry.** `Hero.SetPersonalRelation` clamps to ±100 (`Hero.cs:2019-2023`); `IsFriend` gate uses `DiplomacyModel.MaxNeutralRelationLimit` (`:2044-2047`). Companion death below relation –10 is precedent in `CompanionRolesCampaignBehavior.cs:58-64`. No auto-trigger "friendship trait"; gate behavior on `GetRelation` thresholds in conditions.

**File:line index for §6.3:**
- `AddCompanionAction.cs:5-18`
- `RemoveCompanionAction.cs:15-56`
- `MakeHeroFugitiveAction.cs:5-34`
- `CustomPartyComponent.cs:202` — CreateCustomPartyWithTroopRoster
- `EscortMerchantCaravanIssueBehavior.cs:666, 1281-1293` — spawn precedent
- `DefaultPartySizeLimitModel.cs:81-97`
- `LordPartyComponent.cs:12-80`
- `WarPartyComponent.cs:6-35`
- `Hero.cs:2019-2047` — relation API
- `RetinueManager.cs:21-93` (mod) — existing scaffold

### 6.4 Dialog subsystem — Patron and officer relationships in NPC conversation

**Load-bearing answer: dialog is the right surface for patron-favor UX.** Vanilla `DialogFlow.AddPlayerLine` / `AddDialogLine` (`DialogFlow.cs:206-287`) accept `OnConditionDelegate` (evaluated before render — set `MBTextManager.SetTextVariable` *inside* the condition for runtime substitution) and `OnConsequenceDelegate` (action on selection). Token state machine (`inputToken`, `outputToken`, `priority` default 100) is isolated; mod branches at priority 110-115 layer above vanilla without disrupting.

**Why dialog beats menu:** the lord saying *"I remember when you served under me at Onica, {PLAYER_NAME}"* lands emotionally; a menu option *"Collect Favor: +5 Relation"* does not. Authoring cost moderate (5-10 branches per NPC archetype × number of relevant patrons), but JSON parametrization (the pattern already in `EnlistedDialogManager`) keeps boilerplate tractable.

**Vanilla relation read in dialog conditions.** `Hero.OneToOneConversationHero.GetRelation(Hero.MainHero)` and `GetRelationWithPlayer()` work inside delegate bodies. `Hero.OneToOneConversationHero` is the conversation target. Mod conditions also legal: `PatronRoll.HasEntry(...)`, `IsOfficer()`, etc.

**Text variable substitution.** `MBTextManager.SetTextVariable("VARIABLE_NAME", value)` — value can be string, int, or `TextObject`. Pattern is in mod use already at `EnlistedDialogManager.SetCommonDialogueVariables` (`:1360-1386`). `ConversationManager.FindMatchingTextOrNull(id, character)` for per-culture text variants (`:979-1000`).

**Audience-system extension confirmed.** The mod's `OnTalkToSelected` flow at `EnlistedMenuBehavior.cs:3946` finds nearby lords and opens `CampaignMapConversation.OpenConversation`. Patron-favor branches register at `lord_pretalk` entry state with priority 110+.

**Side-contracts via `notable_pretalk`.** Notables (`Occupation.GangLeader/Artisan/RuralNotable/Merchant/Headman` per `NotablesCampaignBehavior.cs:42-44`) accept layered dialog branches at the same way vanilla quest hand-out works (`IssuesCampaignBehavior.cs:55-149`).

**Officer-trajectory dialog patterns.**
- Lord-greeting officer: `priority: 115`, condition `IsOfficer() && Hero.OneToOneConversationHero.IsLord`.
- Innkeeper / settlement greetings: officer-aware variants at priority 110.
- Peer-officer professional conversations: new state tree (`officer_chat_hub`), gated `IsOfficer() && TargetIsOfficer()`.

**File:line index for §6.4:**
- `DialogFlow.cs:206-287` — AddPlayerLine / AddDialogLine signatures
- `ConversationSentence.cs:23-31` — delegate signatures
- `CampaignMapConversation.cs:4-8` — OpenConversation
- `ConversationManager.cs:700-705` — CreateToken
- `ConversationManager.cs:979-1000` — FindMatchingTextOrNull
- `LordConversationsCampaignBehavior.cs:170-174` — SetTextVariable pattern
- `EnlistedMenuBehavior.cs:3946-4020` (mod) — audience flow
- `EnlistedDialogManager.cs:1360-1386` (mod) — SetCommonDialogueVariables
- `EnlistedDialogManager.cs:1571-1592` (mod) — condition+consequence pattern
- `NotablesCampaignBehavior.cs:42-44` — notable occupation enum
- `IssuesCampaignBehavior.cs:55-149` — issue hand-out flow

### 6.5 Companion subsystem — spawn customization, combat, withhold, aptitude

**Spawn customization.** `HeroCreator.CreateSpecialHero(template, settlement, clan, supporter, age)` (`HeroCreator.cs:189-210`) followed by `hero.HeroDeveloper.SetInitialSkillLevel(skill, value)` (`HeroDeveloper.cs:181-187`), `AddAttribute(attr, n, checkUnspentPoints: false)` (`HeroDeveloper.cs:346-362`), `hero.SetTraitLevel(trait, value)` (`Hero.cs:1397-1401`), `hero.BattleEquipment[slot] = new EquipmentElement(...)`, `hero.SetName(...)` (`Hero.cs:1233-1241`), `hero.SetNewOccupation(Occupation.Soldier)`, then `AddCompanionAction.Apply(playerClan, hero)` (`AddCompanionAction.cs:15-18`). `AddCompanionAction` accepts any occupation — Wanderer is NOT required (verified). Save persistence is automatic via TaleWorlds Hero serialization; mod tracks only metadata.

**LOAD-BEARING: companions in hidden+inactive MainParty fight in Lord's battles.** When the Lord's `MapEvent` fires, the mod's existing escort code activates MainParty (`IsActive = true, IsVisible = false`) and sets `main.Party.MapEventSide = targetSide` (`EnlistmentBehavior.cs:1142-1164`). `MissionAgentSpawnLogic.SpawnTroops` collects all party members regardless of visibility — companions spawn as agents on the Lord's side. Hidden ≠ excluded.

**Withhold-from-combat is partially implemented (corrected v4).** `CompanionAssignmentManager.ShouldCompanionFight(hero)` (`src/Features/Retinue/Core/CompanionAssignmentManager.cs:51-65`) is enforced at agent-spawn by `EnlistedFormationAssignmentBehavior.TryRemoveStayBackCompanion()` (lines 186-220) — BUT the enforcement has a hard early-return at line 188-193 gating it to commander tier (T7+):
```csharp
// Only process companions from player's party at Commander tier (T7+).
var enlistment = EnlistmentBehavior.Instance;
if (enlistment?.IsEnlisted != true || enlistment.EnlistmentTier < RetinueManager.CommanderTier1)
{
    return;
}
```
For the v3 design (Sergeant present from T1, Field Medic from T3, etc.), this gate must be removed — a ~3-line change at `EnlistedFormationAssignmentBehavior.cs:190` enabling tier-wide enforcement. Plus a smoke-test pass to confirm early-tier removal doesn't break the spawn loop differently than T7+ removal does (likely fine since the queue+deferred-removal logic at lines 220-229 is tier-agnostic). Toggle UI in `CampMenuHandler` already works for any tier; only the agent-spawn enforcement is gated.

**Skill aptitude flows correctly while attached.** `MainParty.EffectiveSurgeon` / `EffectiveScout` / `EffectiveQuartermaster` / `EffectiveEngineer` getters (`MobileParty.cs:777-823`) work on hidden+active party. `DefaultPartyHealingModel.GetSurvivalChance` (`:61-115`), `DefaultPartyHealingModel.GetSurgeryChance` (`:45-49`), `DefaultMobilePartyFoodConsumptionModel.cs:59-62`, `DefaultSiegeEventModel.cs:358-360` all read MainParty.EffectiveX correctly. Bonuses reach the player.

**Death is real and tunable.** Vanilla `DefaultDeathProbabilityModel` is age-only (50+ have natural death rate). Battle wounds → `KillCharacterAction.DiedInBattle`. `DefaultPartyHealingModel.GetSurvivalChance` reads Surgeon's Medicine, character tier, armor, age, with a 50× hero multiplier — young + armored ≈ 95% survival per wounding event; old + underarmored ≈ 40%. `CampaignOptions.BattleDeath == Easy` makes player immune (companions can still die at Realistic). Tunable globally via `DefaultDifficultyModel.GetClanMemberDeathChanceMultiplier`.

**File:line index for §6.5:**
- `HeroCreator.cs:189-210` — CreateSpecialHero
- `HeroDeveloper.cs:181-187` — SetInitialSkillLevel
- `HeroDeveloper.cs:346-362` — AddAttribute (with checkUnspentPoints)
- `Hero.cs:1397-1401` — SetTraitLevel
- `Hero.cs:1233-1241` — SetName
- `AddCompanionAction.cs:15-18`
- `RemoveCompanionAction.cs:59-62` — ApplyByFire
- `MakeHeroFugitiveAction.cs:5-34`
- `MobileParty.cs:777-823` — EffectiveX getters
- `EnlistmentBehavior.cs:1142-1164` (mod) — MapEventSide assignment
- `CompanionAssignmentManager.cs:51-65` (mod) — ShouldCompanionFight
- `EnlistedFormationAssignmentBehavior.cs:186-220` (mod) — TryRemoveStayBackCompanion

### 6.6 Companion preference subsystem — vanilla grievance + voting + auto-leave

**Grievance system.** `CompanionGrievanceBehavior` (`TaleWorlds.CampaignSystem.CampaignBehaviors`) auto-fires complaints when an event triggers AND companion has the matching trait (lines 375-382):
- `GrievanceType.DesertedBattle` — companion needs Valor > 0
- `GrievanceType.Starvation` — companion needs Generosity > 0
- `GrievanceType.NoWage` — companion needs Generosity > 0
- `GrievanceType.VillageRaided` — companion needs Mercy > 0

Companions with negative trait levels stay silent. Persona traits (Curt/Earnest/Ironic/Softspoken) modify dialog tone via `GetPersona()` (lines 259-270) but not the underlying logic.

**Grievance response relation deltas** (lines 288-306):
- Accept: +10 relation
- Dismiss: -2 to -5 (depends on repetition)
- Reject: -15 relation

**Kingdom decision voting weights** (`DeclareWarDecision.cs:258-271`):
- Valor: +20 per level (pro-war)
- Mercy: -10 per level (anti-war)

Similar trait-weighting pattern across `MakePeaceKingdomDecision` and other `KingdomDecision` subclasses.

**Auto-leave threshold** (`CompanionRolesCampaignBehavior.cs:58-63`):
- relation < -10 → `KillCharacterAction.ApplyByRemove(companion)`

Hard floor; no gradual exit. The auto-removal hooks `OnHeroRelationChanged`, so any negative relation drift that crosses the threshold triggers it.

**Trait-gated dialog conditions** — vanilla precedent in `BoardGameCampaignBehavior.cs:123` reads opponent's trait via `companion.GetTraitLevel(DefaultTraits.Calculating)` and modifies AI scoring. Same pattern applies to dialog conditions.

**What vanilla does NOT provide:**
- No auto-cascading drift to companions on player choices (mod must call `ChangeRelationAction.ApplyPlayerRelation` for ripples)
- No companion-initiated dialog beyond grievances
- No dynamic backstory tied to traits
- No trait-driven AI behavior decisions

**File:line index for §6.6:**
- `CompanionGrievanceBehavior.cs:156-179, 259-270, 288-306, 375-382`
- `DeclareWarDecision.cs:258-271`
- `CompanionRolesCampaignBehavior.cs:58-63`
- `Hero.cs:2019-2047` — relation API
- `ChangeRelationAction.cs` — `ApplyPlayerRelation` for cascading drift

### 6.7 CK3 schemes → Endeavor mapping (what we keep / drop / substitute)

| CK3 element | Mechanism | Enlisted adaptation | Substitute / decision |
| :-- | :-- | :-- | :-- |
| Schemes (multi-phase) | Power vs Resistance + monthly progress roll | Endeavor phases at 2-3 day intervals | Keep — phase pacing maps to in-game days |
| Hostile vs Personal | Agents+secrecy vs event-only | All endeavors are event-driven; Rogue category carries scrutiny risk | Simplify — single category model |
| Up to 5 agents | Agent slots boost potential / speed / secrecy | Cap at 1-2 companions per endeavor | Reduce — fits army NPC count |
| Spymaster math | Skill-vs-spymaster computation | Player skill + agent skills vs fixed difficulty | Drop spymaster — fixed difficulty per endeavor |
| Court proximity | Agent's court relation to target | Agents are own clan companions | Drop — no courts |
| Discovery vs execution-discovery | Two separate secrecy rolls | Single scrutiny risk number | Simplify |
| Camp Purpose drives contract types | Sword-for-Hire / Pilgrim / Scholar | Five archetype categories (Soldier/Rogue/Medical/Scouting/Social) | Adapt — Bannerlord-flavored archetypes |
| Major Decision long-arcs | Overarching goals (find religion, claim land) | Rank ladder + ceremony arc (§3.9) | Replace — rank progression IS the long arc |
| Adventurer Decisions (minor) | Forage, train, recruit | Single-phase endeavors in any category | Subsume — collapse into endeavor model |

**Net translation.** CK3's three-tier loop (Major Decisions / Contracts / Minor Decisions) collapses to two layers in Enlisted: the **rank ladder + ceremonies** are the long arc, the **endeavor system** is the mid-and-minor activity layer. Companions act as agents and as gatekeepers (the §3.8 hybrid skill-OR-companion model).

### 6.8 Canonical modal-popup pipeline (the "Home adapter/provider" pattern)

**Load-bearing finding (v5):** The "Home-surface adapter/provider pattern" referenced in v4 corrections is real, concretely shipped, and is the single canonical path for ALL player-choice modals in the mod. New mechanics (Endeavor phases, Rank Ceremonies, Decisions, patron-favor outcomes) **plug into this existing pipeline; no new modal infrastructure to design.**

**`InteractiveEvent` is NOT a vanilla TaleWorlds class.** Grep across the decompile returns zero matches. It is the mod's `EventDefinition` carried on the `StoryCandidate.InteractiveEvent` field — set non-null to mark a candidate as Modal vs ambient news.

**The full pipeline:**

```
Storylet (Spec 0 content)
   ↓ StoryletEventAdapter.BuildModal(s, ctx, owner)        [src/Features/Content/StoryletEventAdapter.cs:56]
EventDefinition (synthetic, options w/ empty Effects, Spec 0 EffectDecl stashed in _pendingEffects registry)
   ↓ candidate.InteractiveEvent = evt; candidate.ProposedTier = StoryTier.Modal
StoryCandidate
   ↓ StoryDirector.EmitCandidate(candidate)                [src/Features/Content/StoryDirector.cs:61]
   ↓ Route(c, tier) — pacing-floor check                    [src/Features/Content/StoryDirector.cs:213]
   ↓ EventDeliveryManager.QueueEvent(evt)                   [src/Features/Content/EventDeliveryManager.cs:93]
   ↓ ShowEventPopup(evt) — wraps MultiSelectionInquiryData  [src/Features/Content/EventDeliveryManager.cs:209]
Modal popup with N options (vanilla MultiSelectionInquiry under the hood)
   ↓ player picks
EventDeliveryManager.OnOptionSelected
   ↓ ApplyEffects(option.Effects) + StoryletEventAdapter.DrainPendingEffects(syntheticId, optionId)
EffectExecutor.Apply(effects, ctx) + chain parking
```

**The bridge that makes a candidate a Modal:** setting both `candidate.InteractiveEvent = evt` (non-null) AND `candidate.ProposedTier = StoryTier.Modal` in `StoryDirector.Route` (`:217`) routes to `EventDeliveryManager.QueueEvent`. Without `InteractiveEvent` set, the candidate goes to the news feed via `WriteDispatchItem`.

**Pacing rails (`DensitySettings`):** `ModalFloorInGameDays = 5`, `ModalFloorWallClockSeconds = 60`, `CategoryCooldownDays = 3`, `QuietStretchDays = 7`. Modal candidates pass through `ModalFloorsAllow(c, today)` (`StoryDirector.cs:250`); blocked candidates park in `_deferredInteractive` FIFO queue and retry on daily tick. **`candidate.ChainContinuation = true` bypasses the in-game floor and category cooldown** (still respects 60s wall-clock) — used for in-progress endeavor phase-2-3 firing back-to-back, and ceremony-after-promotion firing immediately on `OnTierChanged`.

**Working precedent — `HomeEveningMenuProvider.OnSlotSelected`** at `src/Features/Activities/Home/HomeEveningMenuProvider.cs:37`. Future provider classes (`EndeavorPhaseProvider`, `CeremonyProvider`, etc.) should be modeled on this literally — same shape, same flow.

**Canonical recipe (drop-in template):**

```csharp
var s = StoryletCatalog.GetById(storyletId);
var ctx = new StoryletContext { ActivityTypeId = "...", PhaseId = "..." };
var evt = StoryletEventAdapter.BuildModal(s, ctx, ownerActivity);
var cand = s.ToCandidate(ctx);
cand.InteractiveEvent = evt;
cand.ProposedTier = StoryTier.Modal;
cand.ChainContinuation = chainContinuation;  // true to bypass cooldowns
StoryDirector.Instance?.EmitCandidate(cand);
```

**Recommended helper to write once at architecture-commit time:**

```csharp
public static class ModalEventBuilder
{
    public static void FireCeremony(string storyletId, StoryletContext ctx);
    public static void FireEndeavorPhase(string storyletId, StoryletContext ctx, EndeavorActivity owner);
    public static void FireDecisionOutcome(string storyletId, StoryletContext ctx);
    // hides BuildModal + EmitCandidate + ChainContinuation flags + null-checks
}
```

This eliminates the ~10 lines of boilerplate at every modal-fire site. Future content authors invoke a single method call.

**Vanilla layer at the bottom:** `EventDeliveryManager.ShowEventPopup` calls `MBInformationManager.ShowMultiSelectionInquiry(MultiSelectionInquiryData, true)` — vanilla's 3+ option modal API. The mod's pipeline doesn't bypass vanilla; it wraps it with pacing + Spec 0 effect application + chain parking. For 2-button modals (e.g. simple "yes / no" patron favor responses), `InformationManager.ShowInquiry(InquiryData)` is also legal and lighter-weight; the Storylet pipeline is overkill there.

### 6.9 Mod's existing menu + dialog wiring (the integration map)

**Menu surfaces:**

| Menu ID | Type | Title | Init handler | Purpose |
| :-- | :-- | :-- | :-- | :-- |
| `enlisted_status` | Wait (no-progress) | `{PARTY_TEXT}` | `OnEnlistedStatusBackgroundInit:531` | Main enlisted hub — orders, decisions, camp, reports |
| `enlisted_camp_hub` | Wait | `{CAMP_HUB_TEXT}` | `OnCampHubInit:1485` | Camp sub — service records, companions, retinue, QM, talk-to-lord |
| `enlisted_headlines` | Game menu | `{HEADLINES_TEXT}` | `OnHeadlinesInit` | Drill-down for high-severity dispatches (7-day window) |

**Camp hub option indices (anti-collision staggered):**

| ID | Index | Condition | Consequence | LeaveType |
| :-- | --: | :-- | :-- | :-- |
| `camp_hub_service_records` | 1 | always | switches to `enlisted_service_records` | Manage |
| `camp_hub_companions` | 2 | always | switches to `enlisted_companions` | Manage |
| `camp_hub_retinue` | 3 | T7+ (Commander) | switches to `enlisted_retinue` | TroopSelection |
| `camp_hub_quartermaster` | 4 | supplies ≥ 15% | `OnQuartermasterSelected:3819` | Trade |
| (free) | 5 | — | — | — |
| (free) | 6 | — | — | — |
| `camp_hub_talk_to_lord` | 7 | nearby lords exist | `OnTalkToSelected:3946` | Conversation |
| `camp_hub_back` | 100 | always | switches to `enlisted_status` | Leave |

**Slots 5 and 6 are free** — these are where Endeavor (5) and a future sub-menu like "Decisions" or "Talk to Sergeant/Field Medic/Veteran" (6) can land without disturbing existing positioning.

**Dialog patterns — two coexisting:**

1. **C#-registered (Lord dialogues).** `EnlistedDialogManager.AddEnlistmentDialogs:161`. ~15 active branches at priority 109-112; ~20 disabled (`#if false`) scaffolding for retirement / renewal / veteran re-enlistment / early discharge. Entry token: `lord_talk_speak_diplomacy_2` → `enlisted_main_hub` → `enlisted_service_options`.

2. **JSON-driven (QM dialogue).** `QMDialogueCatalog` loads `ModuleData/Enlisted/Dialogue/qm_*.json` (4 files, ~40+ context-aware nodes). Schema supports id / speaker / textId / text / context (supply_level, archetype, tier_min, reputation_tier, formation, is_introduced) / options[] with per-option requirements + optional gate. Variants share id with different context; specificity-ranked at runtime via `RegisterJsonQuartermasterLines:1628`. **This pattern ports directly to companion dialogs** — `companion_sergeant_*.json` / `companion_field_medic_*.json` / etc. Mirror the schema; differ only in archetype + topic content.

**Menu ↔ dialog bridges:**

| From | To | Mechanism |
| :-- | :-- | :-- |
| Camp menu option | Dialog | `OnQuartermasterSelected:3819` — fetches QM hero, calls `CampaignMapConversation.OpenConversation(playerData, qmData)`. Sea variant uses `CampaignMission.OpenConversationMission` with `"conversation_scene_sea_multi_agent"` |
| Camp menu option | Dialog (with selection) | `OnTalkToSelected:3946` — finds nearby lords, shows `MultiSelectionInquiryData`, then opens conversation |
| Dialog consequence | Menu re-activate | `NextFrameDispatcher.RunNextFrame(SafeActivateEnlistedMenu)` — deferred to avoid menu-state-during-conversation bugs |
| Dialog consequence | New behavior | `EnlistmentBehavior.StartEnlist()` (post-`OnAcceptEnlistment:4119`), gold/relation actions, etc. |

**Dialog token namespace conventions** (locked-in to avoid global collision):
- `enlisted_*` — current enlistment dialogs
- `endeavor_*` — Endeavor system (§3.8)
- `ceremony_*` — Rank Ceremonies (§3.9)
- `companion_sergeant_*` / `companion_fieldmedic_*` / etc. — companion-specific (§3.10)
- `patron_*` — Roll of Patrons favor branches (§3.3)

**Vanilla token reuse** — ALWAYS layer mod player-lines on existing vanilla input tokens (`lord_pretalk`, `lord_talk_speak_diplomacy_2`, `notable_pretalk`, `hero_main_options`) for natural integration. Use mod-prefixed output tokens to keep our sub-trees isolated.

**File:line index for §6.9:**
- `EnlistedMenuBehavior.cs:915` — `enlisted_status` registration
- `EnlistedMenuBehavior.cs:1345` — `RegisterCampHubMenu`
- `EnlistedMenuBehavior.cs:1485` — `OnCampHubInit`
- `EnlistedMenuBehavior.cs:3819` — `OnQuartermasterSelected`
- `EnlistedMenuBehavior.cs:3946` — `OnTalkToSelected`
- `EnlistedDialogManager.cs:85` — `AddEnlistedDialogs` entry
- `EnlistedDialogManager.cs:161` — `AddEnlistmentDialogs` (Lord branches)
- `EnlistedDialogManager.cs:1140` — `AddQuartermasterDialogs`
- `EnlistedDialogManager.cs:1360-1386` — `SetCommonDialogueVariables` (text variable substitution)
- `EnlistedDialogManager.cs:1628` — `RegisterJsonQuartermasterLines` (JSON catalog → AddDialogLine)
- `QMDialogueCatalog.cs:38` — `LoadFromJson`
- `QMDialogueCatalog.cs:222` — `ParseNode` (schema definition)

---

## 7. Recommended next steps (not commitments)

This is a map, not a plan. Choices are yours. Five observations to inform them:

1. **Foundation first → Companion Substrate (§3.10).** Without companions, three other mechanics (Endeavor gating, Rank-Ceremony witnesses, Officer Trajectory peer dialog) are reduced. Ship companions first.
2. **Highest leverage per LOC → Rank-Ceremony Arc (§3.9).** Eight storylets compounding into 8 character-defining choices give the entire rank ladder narrative meaning. Cheap content-side; massive emotional payoff. Depends on companions for witness texture.
3. **Highest leverage at high rank → Officer Trajectory (§3.7).** T7 promotion goes from "wage tier" to "named gear + tent + tactical position + officer dialog." Companion-pivot makes Junior Officer + QM Officer companions land at this same beat for a multi-system inflection.
4. **Strongest player-agency lever → Endeavor System (§3.8).** Five categories of player-driven activities turn the army from a corridor into a sandbox. Hybrid skill-OR-companion gating gives two paths to access. Subsumes Adventurer Decisions and Side-contracts under one model — saves work elsewhere.
5. **Best long-tail emotional payoff → Roll of Patrons (§3.3).** Even with mod-silences-on-retirement scoping, patron favors during enlistment turn each Lord you've served into a permanent (within-mod) asset.

**Architectural decisions resolved as of v3:**
- ~~Side-contracts: sibling or subsystem of Orders?~~ → **Both merge into Endeavor System (§3.8)** — same Activity backbone, different source (notable / Lord / player).
- ~~Lifestyle: unlocks or perks?~~ → **Unlocks** (option 3); defer numeric perks.
- ~~Officer threshold: T6 or T7?~~ → **T7** — confirmed by rank-system audit (§8). T6→T7 is the only dialog-mediated promotion ceremony; T7 is where retinue/formation control/officer dialog/loot pool jump happens.
- ~~Endeavor gating: skill or companion?~~ → **Hybrid** (option 2 + 3) — your skill OR a hired companion unlocks the category. Recruit-as-unlock makes companion choice load-bearing for play style.

**Architectural decisions still open:**
- **Companions per-Lord or per-player?** Lean: hybrid (Sergeant/Field Medic/Pathfinder per-player; Veteran/QM Officer/Junior Officer per-Lord).
- **Companion personality: rolled archetypes (like QM) or fixed?** Lean: rolled, mirroring QM's 6-personality model.
- **Endeavor concurrency:** can the player run multiple endeavors simultaneously, or one at a time? Lean: 1 active major endeavor + N concurrent minor (single-phase) decisions.

When you pick a mechanic to actually build, *that* call invokes the brainstorming skill — this analysis is the input, not the plan.

---

## 8. Implementation roadmap (7 plans)

**v6 commits the spec to ship.** All mechanics designed across §3.1-3.10 + cross-cutting concerns ship via 7 numbered plans. AI-driven implementation timeline target: ~14 working days end-to-end. Median plan size ~22 tasks; max 30; min 15. Total ~150 tasks.

### Plan summary

| # | Plan | Scope (one-line) | Est. tasks | Depends on |
| :-: | :-- | :-- | --: | :-- |
| **1** | **Architecture Foundation** | Save-offset ledger registered (§4.1); `ModalEventBuilder` helper (§6.8 recipe); companion JSON schema; stay-back gate fix at `EnlistedFormationAssignmentBehavior.cs:190`; empty store shells; validators tightened; build + save-load clean | **15** | — |
| **2** | **Companion Substrate** | All 6 companion spawn recipes (Sergeant T1, Field Medic T3, Pathfinder T3, Veteran T5, QM Officer T7, Junior Officer T7); JSON dialog catalogs per archetype; per-player vs per-Lord wiring; Talk-to sub-menu (Camp slot 6); vanilla `CompanionRolesCampaignBehavior` integration; combat + grievance smoke (§3.10) | **25** | 1 |
| **3** | **Rank-Ceremony Arc** | `RankCeremonyBehavior` hooked to `OnTierChanged`; `CeremonyProvider` modeled on `HomeEveningMenuProvider`; dedup flags; 8 ceremony storylets × ~3 cultural variants; choice-memory flag conventions; companion witness reactions via `ChangeRelationAction` (§3.9) | **20** | 1, 2 |
| **4** | **Officer Trajectory** | `ItemModifier` reflection helper; patron-named weapon modifier; rank-escalating cape; banner item; healing model patches; rear-formation offset (smoke-gated); rank-gated dialog branches at priority 110-115; peer-officer professional conversation (§3.7) | **20** | 1, 2 |
| **5** | **Endeavor System** | `EndeavorActivity` + `ContractActivity` siblings; `EndeavorPhaseProvider`; hybrid skill-OR-companion gating; ~30 endeavor templates × 5 categories; ~60 phase storylets; Camp menu Endeavors sub-option (slot 5); companion-agent locking; scrutiny risk for Rogue (§3.8) | **30** (largest) | 1, 2 |
| **6** | **Roll of Patrons** | `PatronRoll` + `PatronEntry` save classes; `FavorKind` enum; discharge-flow hook; `OnHeroKilled` handler; audience flow extension at `lord_pretalk` priority 112; 6 favor outcome storylets; lifecycle teardown on retirement (§3.3) | **18** | 1 |
| **7** | **Personal Kit + Lifestyle Unlocks + Smoke + Tuning** | Kit catalog (Bedroll/Sharpening/Field Kit × 3 levels); QM dialog extension; lifestyle 3 paths × 3 milestones × ~3 unlocks each; rank-up milestone hook; **plus** 12 mechanic golden-path smoke scenarios + interaction matrix tests + numeric tuning pass (§3.2 + §3.6 + cross-cutting) | **22** | 1-6 |

**Total: ~150 tasks across 7 plans.**

### Dependency graph

```
Plan 1 (Architecture)
   ├── Plan 2 (Companions)
   │      ├── Plan 3 (Ceremonies)         [parallel]
   │      ├── Plan 4 (Officer Trajectory) [parallel]
   │      └── Plan 5 (Endeavors)          [parallel]
   ├── Plan 6 (Patrons)                   [parallel to Plan 2]
   └── Plan 7 (Kit + Lifestyle + Smoke)   [last; absorbs everything]
```

Plan 1 blocks everything (architecture commits are one-way). Plans 2 + 6 can run parallel after Plan 1. Plans 3 + 4 + 5 can run parallel after Plan 2. Plan 7 ships last because smoke testing exercises the full surface.

### Sizing rationale

- **Plan 5 is the largest (30 tasks)** — content authoring dominates. Could split into 5a Substrate + 5b Content if it overruns; held as one for AI-driven velocity.
- **Plan 7 absorbs three concerns** (Personal Kit + Lifestyle + Smoke/Tuning) because each is small individually and they're naturally the "polish + verify" layer. Cohesive final integration pass.
- **Plan 6 stays separate** despite being relatively small because Roll of Patrons has tendrils across audience system + discharge flow + news feed + hero-death lifecycle that benefit from one focused plan rather than scattering.
- **Plan 2 is dense (25 tasks) but cohesive** — six companion spawns share the QM precedent; one plan keeps them on the same architectural pattern.

### Why not fewer plans?

- **5 plans** (~30 tasks each) hits the project's plan-execution limit; tasks at the bottom of a 30-task plan often get rushed or deferred.
- **3 plans** (substrate / content / integration) would smear concerns and lose the "ship + smoke + iterate" rhythm.

### Why not more plans?

- **One plan per mechanic** (10+ plans) creates plan-tracking overhead without clean per-plan deliverables. Plan 4 Officer Trajectory alone isn't a meaningful playable milestone without companions; better to ship them paired.

### Plan documents

Each plan lives at `docs/superpowers/plans/2026-04-24-ck3-wanderer-<plan-name>.md`. Plan 1 (Architecture Foundation) is the first to be drafted; subsequent plans reference Plan 1's locked-in architecture brief.

---

## 9. Canonical rank system reference

The 9-tier ladder is shipped code. Universal default labels + culture-specific overrides. XP thresholds, wage formula, and per-tier unlocks all live in `progression_config.json` + `EnlistmentBehavior.cs` + `RankHelper.cs`. This is the source-of-truth table for design discussions.

| Tier | Universal label | Vlandia | Empire | Khuzait | XP req | Wage/day (T base) | Retinue | Notable unlock |
| :-: | :-- | :-- | :-- | :-- | --: | --: | --: | :-- |
| T1 | Follower | Peasant | Tiro | Outsider | 0 | 15 | 0 | Enlistment start |
| T2 | Recruit | Levy | Miles | Nomad | 800 | 20 | 0 | **Formation selection unlocks** |
| T3 | Free Sword | Footman | Immunes | Noker | 3,000 | 25 | 0 | T3 equipment |
| T4 | Veteran | Man-at-Arms | Principalis | Warrior | 6,000 | 30 | 0 | **Heroic music band**; companion reclaim |
| T5 | Blade | Sergeant | Evocatus | Veteran | 11,000 | 35 | 0 | **NCO track marker** (dialog suffix `_nco`) |
| T6 | Chosen | Knight Bachelor | Centurion | Bahadur | 19,000 | 40 | 0 | T6 equipment; **eligible to request T7** |
| **T7** | **Captain** | **Cavalier** | **Primus Pilus** | **Arban** | **30,000** | **45** | **20** | **OFFICER threshold** — see "what changes at T7" below |
| T8 | Commander | Banneret | Tribune | Zuun | 45,000 | 50 | 30 | Retinue +10; T8 muster narrative |
| T9 | Marshal | Castellan | Legate | Noyan | 65,000 | 55 | 40 | Retinue +10; T9 muster narrative; endgame |

Other cultures: Battania Woodrunner→Highland Champion→High King's Guard; Aserai Tribesman→Faris→Emir's Chosen→Sheikh→Grand Vizier; Sturgia Thrall→Drengr→Huskarl→Champion→High Warlord. Lookup at `RankHelper.cs:21-48` reads from `progression_config.json:4-167`.

**Wage formula:** `10 + heroLevel + (tier × 5) + (xp / 200)`, then ×1.2 if in active army, ×0.5 on probation, capped 24-150. T1 ≈ 15-30g/day; T9 ≈ 55-70g/day baseline.

**Three social bands** (already in code as dialog suffix at `EnlistedDialogManager.cs:2338`):
- **Enlisted (T1-T4)** — `_enlisted` suffix
- **NCO (T5-T6)** — `_nco` suffix
- **Officer (T7-T9)** — `_officer` suffix

**Promotion mechanics:**
- T1→T2 through T5→T6 and T7→T8 / T8→T9: **automatic via "proving event"** delivered by `EventDeliveryManager` once XP + days-in-rank + battles + lord-relation + scrutiny requirements are met. No dialog ceremony.
- **T6→T7: the ONLY dialog-mediated promotion** (`EnlistedDialogManager.cs:347-368`). Player at T6 with all promotion requirements met requests via dialog: *"I believe I am ready to accept the responsibilities of command"* → Lord: *"You have proven yourself worthy. The rank of commander is yours, along with twenty soldiers to train and lead."* This is the single dramatic peak in rank progression — the natural anchor for the T6→T7 ceremony storylet (§3.9) and Officer Trajectory gear deltas (§3.7).

**What ONLY changes at T7 (firm officer boundary):**
1. Retinue capacity 0 → 20 (`RetinueManager.cs:84-93`)
2. Commander formation control in battle (`EnlistedFormationAssignmentBehavior.cs:456-461`)
3. Officer dialog branches (`EnlistedDialogManager.cs:1320, 2338, 2356, 2432`)
4. Baggage storage drops 5 → 0 slots (`BaggageTrainManager.cs:354-357`)
5. QM officer stock unlocks (`QuartermasterManager.cs:2714, 2926`)
6. Loot pool 50% → 100% (`EnlistmentBehavior.cs:6192`)
7. Discharge dialog narrative gets `_officer` suffix
8. Snapshot classifier flags officer status (`SnapshotClassifier.cs:396`)

**Rank lifecycle edge cases:**
- **No demotion path** — rank cannot go down.
- **Discharge resets to T1** for fresh enlistment with a new faction. `ServiceRecordManager` tracks per-faction `HighestTier` and grants veteran-bonus starting tier (capped at T3) when re-enlisting with the same faction.
- **Grace-period re-enlistment** within 30 in-game days preserves tier+XP.

**File index for §8:**
- `EnlistmentBehavior.cs:171` — `_enlistmentTier` field
- `EnlistmentBehavior.cs:686` — `EnlistmentTier` property
- `EnlistmentBehavior.cs:5840-5861` — wage formula
- `EnlistmentBehavior.cs:9748-9805` — `SetTier()` method + `OnTierChanged` event
- `EnlistmentBehavior.cs:8490, 9799` — `OnTierChanged` event declaration + invocation
- `RankHelper.cs:21-48` — culture-aware rank label lookup
- `progression_config.json:4-167` — XP thresholds and culture rank labels
- `PromotionBehavior.cs:30-57, 307-402` — promotion requirements + auto proving event
- `EnlistedDialogManager.cs:347-368` — T6→T7 commander promotion dialog
- `EnlistedDialogManager.cs:4075-4106` — `CanRequestCommanderPromotion`

---

## Sources

- [Roads to Power — CK3 Wiki](https://ck3.paradoxwikis.com/Roads_to_Power)
- [Adventurer — CK3 Wiki](https://ck3.paradoxwikis.com/Adventurer)
- [Adventurer decisions — CK3 Wiki](https://ck3.paradoxwikis.com/Adventurer_decisions)
- [Camp — CK3 Wiki](https://ck3.paradoxwikis.com/Camp)
- [Hired forces — CK3 Wiki](https://ck3.paradoxwikis.com/Hired_forces)
- [Lifestyle — CK3 Wiki](https://ck3.paradoxwikis.com/Lifestyle)
- [Wandering Nobles — CK3 Wiki](https://ck3.paradoxwikis.com/Wandering_Nobles)
- [Travel — CK3 Wiki](https://ck3.paradoxwikis.com/Travel)
- [Activity — CK3 Wiki](https://ck3.paradoxwikis.com/Activity)
- **CK3 wiki**: [Schemes](https://ck3.paradoxwikis.com/Schemes), [Roads to Power](https://ck3.paradoxwikis.com/Roads_to_Power), [Adventurer](https://ck3.paradoxwikis.com/Adventurer), [Adventurer decisions](https://ck3.paradoxwikis.com/Adventurer_decisions), [Wandering Nobles](https://ck3.paradoxwikis.com/Wandering_Nobles)
- **CK3 forums + guides**: [Dev Diary #5 — Schemes, Secrets and Hooks](https://forum.paradoxplaza.com/forum/threads/ck3-dev-diary-5-schemes-secrets-and-hooks.1289167/), [Dev Diary #151 — Landless Adventurers Part I](https://forum.paradoxplaza.com/forum/threads/dev-diary-151-landless-adventurers-part-i.1697815/), [Dev Diary #152 — Landless Adventurers Part II](https://forum.paradoxplaza.com/forum/threads/dev-diary-152-landless-adventurers-part-ii.1698728/), [How To Plot A Successful Scheme — TheGamer](https://www.thegamer.com/crusader-kings-3-complete-guide-to-schemes/)
- **Decompile (vanilla TaleWorlds)** — `Hero.cs`, `MobileParty.cs`, `Settlement.cs`, `IssueBase.cs`, `KillCharacterAction.cs`, `BarterManager.cs`, `HeroDeveloper.cs`, `HeroCreator.cs`, `PerkObject.cs`, `DefaultPartyHealingModel.cs`, `DefaultDeathProbabilityModel.cs`, `CampaignGameStarter.cs`, `CampaignEventDispatcher.cs`, `ItemModifier.cs`, `MBObjectManager.cs`, `ItemModifierGroup.cs`, `EquipmentElement.cs`, `EquipmentIndex.cs`, `BannerComponent.cs`, `Equipment.cs`, `Crafting.cs`, `DefaultCharacterStatsModel.cs`, `DefaultPartyMoraleModel.cs`, `DefaultMobilePartyFoodConsumptionModel.cs`, `DefaultSiegeEventModel.cs`, `AddCompanionAction.cs`, `RemoveCompanionAction.cs`, `MakeHeroFugitiveAction.cs`, `ChangeRelationAction.cs`, `CustomPartyComponent.cs`, `EscortMerchantCaravanIssueBehavior.cs`, `LordPartyComponent.cs`, `WarPartyComponent.cs`, `DefaultPartySizeLimitModel.cs`, `CompanionRolesCampaignBehavior.cs`, `CompanionGrievanceBehavior.cs`, `CompanionsCampaignBehavior.cs`, `DeclareWarDecision.cs`, `MakePeaceKingdomDecision.cs`, `DialogFlow.cs`, `ConversationSentence.cs`, `ConversationManager.cs`, `CampaignMapConversation.cs`, `LordConversationsCampaignBehavior.cs`, `NotablesCampaignBehavior.cs`, `IssuesCampaignBehavior.cs`, `MissionAgentSpawnLogic.cs`, `BackstoryCampaignBehavior.cs`
- **Mod (Enlisted)** — `EnlistmentBehavior.cs` (rank system, QM spawn, MapEventSide, leave/dispatch infrastructure), `EnlistedDialogManager.cs` (lord dialogue surface, JSON catalog registration), `EnlistedFormationAssignmentBehavior.cs` (combat assignment + withhold-from-combat enforcement), `EnlistedMenuBehavior.cs` (menu state machine, audience flow, camp hub), `QuartermasterManager.cs` (shop architecture), `RetinueManager.cs` (retinue capacity gates), `CompanionAssignmentManager.cs` (Fight/Stay-Back toggle), `RankHelper.cs` (culture-aware rank labels), `progression_config.json` (XP thresholds + rank labels), `PromotionBehavior.cs` (proving event delivery), `StoryletEventAdapter.cs` (BuildModal — Storylet → EventDefinition adapter), `StoryDirector.cs` (EmitCandidate, Route, modal vs news routing, pacing floors), `EventDeliveryManager.cs` (QueueEvent, ShowEventPopup, MultiSelectionInquiry wrapper), `HomeEveningMenuProvider.cs` (canonical menu→modal precedent), `QMDialogueCatalog.cs` (JSON dialog schema + context matching), `DensitySettings.cs` (modal pacing constants)
- Companion docs — [`docs/superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md`](2026-04-24-enlisted-menu-duty-unification-design.md), [`AGENTS.md`](../../../AGENTS.md), [`CLAUDE.md`](../../../CLAUDE.md)
