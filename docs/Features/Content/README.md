# Content System Documentation

**Summary:** Narrative content authoring for the Enlisted mod. As of 2026-04-19, all new content targets the **storylet backbone** (Spec 0). Pre-storylet `EventDefinition` JSON under `ModuleData/Enlisted/Events/` and `Decisions/` still runs at runtime through `StoryDirector`; the old Orders event pools were retired with the legacy Orders subsystem and are historical reference only.

---

## Canonical references

| Document | Purpose | Status |
|---|---|---|
| [storylet-backbone.md](storylet-backbone.md) | Spec 0 living reference: vocabulary, quality/flag registries, trigger + slot + primitive catalogs, scripted-effect seed list, save-definer offsets, chain semantics, pitfalls. **Start here.** | Live |
| [writing-style-guide.md](writing-style-guide.md) | Writing standards: military voice/tone, vocabulary, text structure, dynamic tokens, tooltip formatting | Live |

## Still-live legacy content (not yet replaced)

| Document | Purpose | Notes |
|---|---|---|
| [injury-system.md](injury-system.md) | Unified medical condition system (injuries, illnesses, context-aware treatment) | Implementation: `PlayerConditionBehavior`, `EventDeliveryManager`, `condition_defs.json` |

## Authoring new content

1. Read [storylet-backbone.md](storylet-backbone.md) for vocabulary + substrate.
2. Read [writing-style-guide.md](writing-style-guide.md) for voice.
3. Author storylets under `ModuleData/Enlisted/Storylets/*.json`; effects in `ModuleData/Enlisted/Effects/scripted_effects.json`; new triggers registered in `src/Features/Content/TriggerRegistry.cs` or surface-specific trigger files (e.g. `src/Features/Activities/Home/HomeTriggers.cs`).
4. Run `python Tools/Validation/validate_content.py` (Phase 12 catches unknown trigger / scripted-effect references).
5. Run `python Tools/Validation/sync_event_strings.py` if you added `{=key}Fallback` loc-keys.

## Legacy content runtime (reference only)

`ModuleData/Enlisted/Events/*.json` and `ModuleData/Enlisted/Decisions/*.json` are still loaded by `EventCatalog` + `EventDeliveryManager` and still fire at runtime through `StoryDirector`. The legacy `ModuleData/Enlisted/Orders/` directory was deleted on 2026-04-21 with the retired Orders subsystem; the current Orders surface (Spec 2) is driven by `OrderActivity` + `NamedOrderState` save-classes plus storylet pools at `ModuleData/Enlisted/Storylets/duty_*.json`.
