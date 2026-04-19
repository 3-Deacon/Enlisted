# Content System Documentation

**Summary:** Complete documentation for the mod's unified content system including technical architecture, JSON schemas, and a full catalog of all 275 narrative content pieces (orders, order events, decisions, camp opportunities, context events, and map incidents).

**Total Content:** 282 pieces including 17 orders, 84 order events, 37 decisions, 36 camp opportunities, 57 context events, 51 map incidents, and 23 retinue events (subset).

---

## Index

### Content Catalog (What content exists)

| Document | Purpose | Content Count |
|----------|---------|---------------|
| [content-index.md](content-index.md) | Master catalog of all content with IDs, titles, descriptions, requirements, effects, skill checks organized by category | 275 pieces |
| [content-organization-map.md](content-organization-map.md) | Visual hierarchy showing parent-child relationships, file locations, workflows for adding new content | 275 pieces |
| [orders-content.md](orders-content.md) | Complete order specifications with JSON schemas, phases, event pools, and implementation details | 17 orders + 84 events |

### Technical Implementation (How the system works)

| Document | Topic | Status |
|----------|-------|--------|
| [content-system-architecture.md](content-system-architecture.md) | World-state driven orchestration, ContentOrchestrator pipeline, JSON/XML architecture, native Bannerlord integration, **error codes & diagnostics** | ✅ Implemented |
| [event-system-schemas.md](event-system-schemas.md) | Authoritative JSON schemas for events, decisions, orders, camp routines with parsing rules and validation | ✅ Current |
| [writing-style-guide.md](writing-style-guide.md) | Writing standards: military voice/tone, vocabulary, text structure, dynamic tokens, tooltip formatting | ✅ Current |
| [injury-system.md](injury-system.md) | Unified medical condition system: injuries, illnesses, medical risk escalation, context-aware treatment, recovery | ✅ Implemented |

---

## Quick Navigation

**Finding specific content:**
1. Use [content-organization-map.md](content-organization-map.md) for visual hierarchy and file locations
2. Use [content-index.md](content-index.md) for complete listings by content type with detailed tables

**Adding new content:**
- Start with [content-organization-map.md](content-organization-map.md) to find where content goes
- Read [writing-style-guide.md](writing-style-guide.md) for voice, tone, and vocabulary standards
- Read [event-system-schemas.md](event-system-schemas.md) for JSON structure and validation rules
- See [content-system-architecture.md](content-system-architecture.md) for orchestrator integration

**Understanding the system:**
- Start with [content-system-architecture.md](content-system-architecture.md) for the full technical picture
- See [event-system-schemas.md](event-system-schemas.md) for JSON schemas and validation

**Native game reference:**
- [native-map-incidents.md](../../Reference/native-map-incidents.md) - Vanilla Bannerlord map incidents
- [native-skill-xp.md](../../Reference/native-skill-xp.md) - Native skill progression rates
- [map-incidents-warsails.md](../../Reference/map-incidents-warsails.md) - Warsails/Naval DLC incidents

---

## Content Summary

| Category | Count | Description |
|----------|-------|-------------|
| **Orders** | 17 | Military directives from chain of command |
| **Order Events** | 84 | Events that fire during order execution (defined in `ModuleData/Enlisted/Orders/order_events/*.json`) |
| **Decisions** | 37 | Player-initiated Camp Hub actions (3 core + 26 camp + 8 medical with sea variants) |
| **Camp Opportunities** | 36 | Orchestrated activities pre-scheduled 24hrs ahead by ContentOrchestrator |
| **Context Events** | 57 | Context-triggered situations (escalation, medical, pay, promotion, baggage, retinue, universal) |
| **Map Incidents** | 51 | Battle, siege, and settlement-triggered encounters (45 general + 6 retinue) |
| **Retinue Content** | 23 | T7+ commander content (17 events + 6 incidents, subset of above) |
| **Total** | **275** | Complete content catalog |

---
