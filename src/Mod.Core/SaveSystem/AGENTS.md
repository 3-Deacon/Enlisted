# AGENTS.md — Mod.Core/SaveSystem

> Parent: [../../../AGENTS.md](../../../AGENTS.md)
> Handbook: [../../../docs/architecture/](../../../docs/architecture/) (cross-cutting briefs)

This file owns save-system rules: the full save-definer offset table, class-vs-enum disjointness, EnsureInitialized pattern, and the `Campaign.Current.X`-null-at-OnGameStart family. Source-of-truth for `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` and any `[Serializable]` class registered there.

---

## Critical Rule: Save System Registration

In `EnlistedSaveDefiner` — missing = "Cannot Create Save" error:

```csharp
DefineEnumType(typeof(MyNewEnum));
DefineClassType(typeof(MyNewClass));
```

Persist in-progress flags in `SyncData()` too — otherwise state is lost on reload.

---

## Save-definer offset convention

**Save-definer offset convention.** Spec 0 owns class offsets 40-44 and enum offsets 82-83 in `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`. Class offsets **45-70** are reserved for concrete `Activity` subclasses from surface specs (Home / Orders / Land-Sea / Promotion+Muster / Quartermaster) AND closely-related surface-spec persistent state that surface specs own (snapshots, accessors, compact POCOs serving as sources of truth). Intelligence backbone's `EnlistedLordIntelligenceSnapshot` holds offset 48 under this broadening; Spec 2's `OrderActivity` + `NamedOrderState` hold 46/47, Plan 3's `SignalEmissionRecord` holds 49 under this broadening, Plan 4's `DutyCooldownStore` holds 50. Offsets **51-70** are shared between three sources: the menu+duty unification spec (offsets 51-52: `DutyActivity`, `ChoreThrottleStore` — see [docs/superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md](../../../docs/superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md)); the CK3 wanderer mechanics cluster Plans 1-7 (offsets 54-58 + enum 84 — contract at [docs/architecture/ck3-wanderer-architecture-brief.md](../../../docs/architecture/ck3-wanderer-architecture-brief.md)); and future surface specs 3-5 (offsets 60-70). Grep the definer before claiming an offset — collisions corrupt saves silently. Offsets 10-14 were held by the legacy Orders subsystem (retired 2026-04-21, commit `a8719bb`) and remain reserved; do not reuse without audit.

---

## Enum offsets must be disjoint from class offsets

**Enum offsets MUST stay disjoint from the class-offset numeric range.** TaleWorlds' `DefinitionContext.AddClassDefinition` and `AddEnumDefinition` both add to a shared `_allTypeDefinitionsWithId` dictionary keyed by `TypeSaveId` (which equals `BaseId + offset` as a plain integer — kind discriminator is NOT part of the key). A class at offset N and an enum at offset N produce the same key and crash `Module.Initialize()` with `Dictionary.Insert` `ArgumentException`, before any mod logging. Decompile reference: `Decompile/TaleWorlds.SaveSystem/TaleWorlds.SaveSystem.Definition/DefinitionContext.cs:118-124` (4 dict.Add calls including the shared `_allTypeDefinitionsWithId`). Enums in the mod live at offsets **80+** (currently 80-103 + 110-117). Class offsets 1-70 are reserved for class registrations only — never reuse those numbers for an enum. The retinue + logistics enums originally at 50-52 and Content Orchestrator + Camp Life Simulation enums originally at 60-71 were relocated to 110-119 on 2026-04-25 after this collision surfaced live. The Camp Life Simulation enums `OpportunityType` (118) + `CampMood` (119) were freed later that day when the legacy decisions / orchestrator / Camp* cluster retired. Free enum offsets going forward: 104-109, 118-119, and 120+.

---

## Pitfall: HashSet not saveable

**`HashSet<T>` is not a saveable container in the TaleWorlds SaveSystem.** `TaleWorlds.SaveSystem.ContainerType` only knows `List / Queue / Dictionary / Array / CustomList / CustomReadOnlyList` — `IsContainer` falls through for `HashSet<T>`, leaving both `SaveId`s null and crashing `ContainerSaveId.CalculateStringId` during `Module.Initialize()` (before mod logs exist). Use `List<T>` with runtime dedup, or serialize-to-CSV + rebuild on load (see `CompanySimulationBehavior._activeFlags`). If the game crashes before the session log writes a single line, check the native stack in `C:\ProgramData\Mount and Blade II Bannerlord\logs\watchdog_log_<pid>.txt`.

---

## Pitfall: Serializable stores deserialize with null Dictionary/List

**`[Serializable]` save stores deserialize with null `Dictionary`/`List` properties.** TaleWorlds `IDataStore.SyncData` uses a deserialization path that skips the ctor, so `public Dictionary<...> Foo { get; set; } = new(...)` field initializers don't run when loading a save that predates the field. Any `foreach (... in Foo.Keys)` in `OnGameLoaded` or on an hourly/daily tick NREs and crosses into native, killing the process (no managed log line — only a crash dump). Add an `EnsureInitialized()` method on the store that reseats null dict/list fields with empty instances, and call it from `SyncData` (after the `dataStore.SyncData(...)` line), `OnSessionLaunched`, and `OnGameLoaded`. See `FlagStore.EnsureInitialized` / `QualityStore.EnsureInitialized` for the pattern.

---

## Pitfall: Campaign.Current.X statics null at OnGameStart

**`Campaign.Current.X`-backed statics are null at `OnGameStart`.** `DefaultTraits.Mercy/Valor/Honor/Calculating`, `DefaultSkills.*`, `DefaultPerks.*` all dereference `Campaign.Current.DefaultXxx` internally. Touching them eagerly at registration (e.g. `RegisterTrait("mercy", DefaultTraits.Mercy)`) NREs before `OnGameStart` finishes and aborts the rest of bootstrap (menu regs, deferred patches, enlisted activation). Pass providers (`Func<TraitObject>`) or resolve inside the handler body — lookup must happen after `OnSessionLaunched`.

---

## Pitfall: Claiming an offset without checking the registry

Claiming a `SaveableTypeDefiner` offset without grepping
`src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs` first. Offsets 40-44
(classes) and 82-83 (enums) are Spec 0. Offsets **45-70** are reserved
for concrete `Activity` subclasses AND closely-related surface-spec
persistent state — see the save-definer offset convention above
for the full table (Career-loop family at 48-50, menu+duty
unification at 51-52, CK3 wanderer at 54-58, surface specs 3-5 at
60-70). Offsets 10-14 (legacy Orders) reserved; offsets 118-119 freed
2026-04-25 (Camp* retirement). Do not reuse any reserved range
without audit.

---

## Hero/Skill XP gotchas

- **`hero.HeroDeveloper.AddSkillXp(skill, xp)` — 2-arg form (isAffectedByFocusFactor defaults to true):** calls `GainRawXp` internally, which (a) increments `_totalXp` and contributes to overall hero character-level advancement, and (b) multiplies the per-skill XP award by the hero's invested focus factor — heroes with focus in a skill earn more XP per award. This is vanilla behavior and fires level-up events. NOT quiet.

- **`hero.HeroDeveloper.AddSkillXp(skill, xp, isAffectedByFocusFactor: false)` — 3-arg form:** skips `GainRawXp` (no character-level contribution) AND ignores focus factor (raw XP regardless of investment). This is what storylet/event/scripted-effect XP uses — intentional, but two designer consequences to know: (1) event XP does NOT contribute to the hero's overall character level, and (2) focus point investment does NOT scale these awards. If designers want event XP to contribute to character level (vanilla behavior), use the 2-arg form — but that brings the level-up notification and character-level side effect. Decompile reference: `HeroDeveloper.cs:198-218`.

- For background XP awards (events, scripted effects, daily drift) use the 3-arg `isAffectedByFocusFactor: false` form.

- **`TraitLevelingHelper.AddTraitXp` / `OnIncidentResolved`** are `private`/internal and route through `Campaign.Current.PlayerTraitDeveloper` — MainHero only. For non-player heroes use `Hero.SetTraitLevel(trait, level)` directly.

- **`DefaultTraits.Tracking`** is catalogued in `DefaultTraits` but has no public static accessor in v1.3.13. Use `MBObjectManager.Instance.GetObject<TraitObject>("Tracking")` to resolve it at runtime. The same applies to any trait not exposed as a static property — confirm against `Decompile/TaleWorlds.CampaignSystem/DefaultTraits.cs` before assuming a static exists.

---

## See also

- [EnlistedSaveDefiner.cs](EnlistedSaveDefiner.cs) — the live offset registry
- [../../Features/Content/AGENTS.md](../../Features/Content/AGENTS.md) — Content classes that claim offsets 40-44
- [../../../docs/superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md](../../../docs/superpowers/specs/2026-04-24-enlisted-menu-duty-unification-design.md) — menu+duty offsets 51-52
- [../../../docs/architecture/ck3-wanderer-architecture-brief.md](../../../docs/architecture/ck3-wanderer-architecture-brief.md) — CK3 wanderer offsets 54-58
