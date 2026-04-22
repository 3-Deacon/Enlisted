# Content System Documentation

**Summary:** Narrative content authoring for the Enlisted mod. As of 2026-04-19, all new content targets the **storylet backbone** (Spec 0). Pre-storylet content (legacy `EventDefinition` JSON under `ModuleData/Enlisted/Events/`, `Decisions/`, and the Orders event pools) still runs at runtime but is not the target for new authoring.

---

## Canonical references

| Document | Purpose | Status |
|---|---|---|
| [storylet-backbone.md](storylet-backbone.md) | Spec 0 living reference: vocabulary, quality/flag registries, trigger + slot + primitive catalogs, scripted-effect seed list, save-definer offsets, chain semantics, pitfalls. **Start here.** | Live |
| [writing-style-guide.md](writing-style-guide.md) | Writing standards: military voice/tone, vocabulary, text structure, dynamic tokens, tooltip formatting | Live |

## Still-live legacy content (not yet replaced)

| Document | Purpose | Notes |
|---|---|---|
| [orders-content.md](orders-content.md) | 17 orders + 84 order events across 16 event pools | **Retired 2026-04-21.** Legacy content deleted in `a8719bb`; `src/Features/Orders/` + `ModuleData/Enlisted/Orders/` are gone. Replacement is `OrderActivity` + storylet pools, now owned by the five-plan integration roadmap ([integration spec](../../superpowers/specs/2026-04-21-plans-integration-design.md)). This document kept only for historical reference; do not author against it. |
| [injury-system.md](injury-system.md) | Unified medical condition system (injuries, illnesses, context-aware treatment) | Implementation: `PlayerConditionBehavior`, `EventDeliveryManager`, `condition_defs.json` |

## Authoring new content

1. Read [storylet-backbone.md](storylet-backbone.md) for vocabulary + substrate.
2. Read [writing-style-guide.md](writing-style-guide.md) for voice.
3. Author storylets under `ModuleData/Enlisted/Storylets/*.json`; effects in `ModuleData/Enlisted/Effects/scripted_effects.json`; new triggers registered in `src/Features/Content/TriggerRegistry.cs` or surface-specific trigger files (e.g. `src/Features/Activities/Home/HomeTriggers.cs`).
4. Run `python Tools/Validation/validate_content.py` (Phase 12 catches unknown trigger / scripted-effect references).
5. Run `python Tools/Validation/sync_event_strings.py` if you added `{=key}Fallback` loc-keys.

## Legacy content runtime (reference only)

`ModuleData/Enlisted/Events/*.json`, `ModuleData/Enlisted/Decisions/*.json`, and `ModuleData/Enlisted/Orders/order_events/*.json` are still loaded by `EventCatalog` + `EventDeliveryManager` and still fire at runtime through `StoryDirector`. New authoring should target storylets per above, not these.
