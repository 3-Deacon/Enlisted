# Content System Documentation

**Summary:** Narrative content authoring for the Enlisted mod. All new content targets the **storylet backbone**: storylets, activities, scripted effects, triggers, qualities, and flags.

---

## Canonical references

| Document | Purpose | Status |
|---|---|---|
| [storylet-backbone.md](storylet-backbone.md) | Spec 0 living reference: vocabulary, quality/flag registries, trigger + slot + primitive catalogs, scripted-effect seed list, save-definer offsets, chain semantics, pitfalls. **Start here.** | Live |
| [writing-style-guide.md](writing-style-guide.md) | Writing standards: military voice/tone, vocabulary, text structure, dynamic tokens, tooltip formatting | Live |

## Current content references

| Document | Purpose | Notes |
|---|---|---|
| [storylet-backbone.md](storylet-backbone.md) | Storylet and activity authoring substrate | Use this for order arcs, camp opportunities, and narrative events. |
| [injury-system.md](injury-system.md) | Unified medical condition system (injuries, illnesses, context-aware treatment) | Implementation: `PlayerConditionBehavior`, `EventDeliveryManager`, `condition_defs.json` |

## Authoring new content

1. Read [storylet-backbone.md](storylet-backbone.md) for vocabulary + substrate.
2. Read [writing-style-guide.md](writing-style-guide.md) for voice.
3. Author storylets under `ModuleData/Enlisted/Storylets/*.json`; effects in `ModuleData/Enlisted/Effects/scripted_effects.json`; new triggers registered in `src/Features/Content/TriggerRegistry.cs` or surface-specific trigger files (e.g. `src/Features/Activities/Home/HomeTriggers.cs`).
4. Run `python Tools/Validation/validate_content.py` (Phase 12 catches unknown trigger / scripted-effect references).
5. Run `python Tools/Validation/sync_event_strings.py` if you added `{=key}Fallback` loc-keys.

## Runtime catalogs

`ModuleData/Enlisted/Events/*.json` and `ModuleData/Enlisted/Decisions/*.json` are still loaded by `EventCatalog` + `EventDeliveryManager` and fire through `StoryDirector`. New authoring should target storylets and activities per above.
