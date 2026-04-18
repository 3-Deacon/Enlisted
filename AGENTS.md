# Enlisted - Bannerlord v1.3.13 Mod

C# mod transforming Mount & Blade II: Bannerlord into a soldier career simulator. Player enlists with a lord, follows orders, earns wages, progresses through 9 ranks. 245+ narrative content pieces, data-driven via JSON + XML.

This file is the shared source of truth for AI coding agents (Claude Code, Codex, Cursor, Copilot, Aider, etc.). Tool-specific extras live alongside: `CLAUDE.md` imports this file.

---

## Quick Commands

```bash
# Build (always use this exact configuration)
dotnet build -c "Enlisted RETAIL" /p:Platform=x64

# Validate content (ALWAYS before commit)
python Tools/Validation/validate_content.py

# Sync localization strings
python Tools/Validation/sync_event_strings.py

# Upload to Steam Workshop
./Tools/Steam/upload.ps1
```

---

## Critical Rules (Will Break Mod)

### 1. Target Bannerlord v1.3.13

- NEVER assume APIs from later versions
- ALWAYS verify APIs against local `Decompile/` directory (NOT online docs)

### 2. New C# Files Must Be Registered in .csproj

```xml
<Compile Include="src\Features\MyFeature\MyNewClass.cs"/>
```

### 3. Gold Transactions — use GiveGoldAction

```csharp
// CORRECT - visible in UI
GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount);
// WRONG - not visible, breaks UI feedback
Hero.MainHero.ChangeHeroGold(amount);
```

### 4. Equipment Iteration — numeric loop only

```csharp
// CORRECT
for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
// WRONG - crashes (Enum.GetValues includes count values)
foreach (EquipmentIndex slot in Enum.GetValues(typeof(EquipmentIndex)))
```

### 5. Hero Safety — null-safe, IsAlive checks

```csharp
var hero = CampaignSafetyGuard.SafeMainHero;
if (hero == null) return;
if (hero.IsAlive) VisualTrackerManager.RegisterObject(hero);
```

### 6. JSON Field Order — fallback immediately after ID

```json
{ "titleId": "key", "title": "Fallback", "setupId": "key2", "setup": "Text" }
```

### 7. Event Tooltips Required

All options need tooltips (<80 chars). Format: action + effects + cooldown.

### 8. Save System Registration

In `EnlistedSaveDefiner` — missing = "Cannot Create Save" error:

```csharp
DefineEnumType(typeof(MyNewEnum));
DefineClassType(typeof(MyNewClass));
```

Persist in-progress flags in `SyncData()` too — otherwise state is lost on reload.

### 9. Windows/WSL Portability

This repo is developed on both Windows and Linux (WSL). For C# code that builds paths:

```csharp
// CORRECT - cross-platform
Path.Combine(basePath, "Prompts", "order_prompts.json");
ModulePaths.GetContentPath("Prompts"); // preferred for mod content

// WRONG - breaks on Linux
basePath + "\\Prompts\\order_prompts.json"
```

Line endings are enforced by `.gitattributes` (`.cs` / `.csproj` / `.sln` / `.ps1` = CRLF; everything else `text=auto`). Don't override locally.

---

## Code Standards

- Braces required on all control statements (no single-line `if`)
- `ModLogger.Log()` with error codes: `E-SYSTEM-###`
- Localized strings: `new TextObject("{=id}Fallback")`
- Private fields: `_camelCase`
- Comments describe current behavior — never changelogs, PR references, or "added for X"

### Safe Patterns

```csharp
// Deferred menu activation
NextFrameDispatcher.RunNextFrame(() => GameMenu.ActivateGameMenu("menu_id"));
// Item comparison by StringId (not reference equality)
if (element.Item.StringId == targetItem.StringId)
// Settlement safety check
if (!PlayerEncounter.InsideSettlement) PlayerEncounter.Finish();
// Centralized manager for reputation/needs changes
EscalationManager.Instance.ModifyReputation(ReputationType.Soldier, 5, "reason");
```

---

## Project Structure

```
src/Features/          C# gameplay features
ModuleData/Enlisted/   JSON events, orders, decisions
ModuleData/Languages/  enlisted_strings.xml (localization)
docs/                  All documentation (see docs/INDEX.md)
Tools/Validation/      Validators (run before commit)
Decompile/             Bannerlord v1.3.13 API — AUTHORITATIVE
```

### Key Feature Folders

- `Enlistment/` — Service state, retirement
- `Orders/` — Mission directives
- `Content/` — Events, decisions, narrative
- `Escalation/` — Reputation, scrutiny/discipline
- `Company/` — Readiness, supply needs
- `Equipment/` — Quartermaster, gear

---

## Pre-Commit Checklist

- [ ] APIs verified against `Decompile/` (v1.3.13)
- [ ] New C# files added to `Enlisted.csproj`
- [ ] JSON field order correct (fallback after ID)
- [ ] Tooltips on all event options (<80 chars)
- [ ] `python Tools/Validation/validate_content.py` passes
- [ ] `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` succeeds

---

## Key Documentation

Link, don't duplicate — open these for depth:

| Topic | File |
| :--- | :--- |
| Architecture & standards | [docs/BLUEPRINT.md](docs/BLUEPRINT.md) |
| Master documentation catalog | [docs/INDEX.md](docs/INDEX.md) |
| Build setup, developer onboarding | [docs/DEVELOPER-GUIDE.md](docs/DEVELOPER-GUIDE.md) |
| Logging, saves, dialogue, menu patterns | [Tools/TECHNICAL-REFERENCE.md](Tools/TECHNICAL-REFERENCE.md) |
| Validation tool reference | [Tools/README.md](Tools/README.md) |
| Writing style (voice, tone) | [docs/Features/Content/writing-style-guide.md](docs/Features/Content/writing-style-guide.md) |

---

## Common Pitfalls

1. `ChangeHeroGold` instead of `GiveGoldAction`
2. `Enum.GetValues` for equipment iteration
3. Tracking a hero without checking `IsAlive`
4. `PlayerEncounter.Finish()` while inside a settlement
5. Forgetting to add new files to `.csproj`
6. Missing tooltips on event options
7. Wrong JSON field order (ID and fallback not adjacent)
8. Not persisting in-progress flags in `SyncData()`
9. Missing `SaveableTypeDefiner` registration
10. Relying on external API docs (wrong version)

Full pitfalls list with solutions: [docs/BLUEPRINT.md](docs/BLUEPRINT.md).

---

## Deprecated Systems

- **Morale System** — Removed 2026-01-11, save-load only
- **Company Rest** — Removed 2026-01-11, save-load only
- Player Fatigue (0-24 budget) remains functional

---

## External Resources

- Steam Workshop: <https://steamcommunity.com/sharedfiles/filedetails/?id=3621116083>
- Requires: Harmony for Bannerlord

---

**When in doubt, check `Decompile/`. Never hallucinate APIs.**
