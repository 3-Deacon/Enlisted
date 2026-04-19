# Enlisted Tools

Development tools, utilities, and diagnostics for the Enlisted mod.

---

## Quick Debug Logging Toggle

**`toggle_debug_logging.ps1`** - Easily enable/disable debug logging for specific categories without manually editing JSON.

```powershell
# Show current log levels
.\Tools\toggle_debug_logging.ps1 -ShowCurrent

# Enable Debug logging for Interface (combat log, UI issues)
.\Tools\toggle_debug_logging.ps1 -Category Interface -Level Debug

# Enable Debug logging for Battle (combat issues)
.\Tools\toggle_debug_logging.ps1 -Category Battle -Level Debug

# Reset Interface to normal Info level
.\Tools\toggle_debug_logging.ps1 -Category Interface -Level Info
```

**Available log levels:** `Off`, `Error`, `Warn`, `Info` (default), `Debug`, `Trace`

**Common categories:** `Interface`, `Battle`, `Enlistment`, `Orders`, `Content`, `Equipment`, `Orchestrator`

⚠️ **Restart the game** after changing log levels.

---

## Folder Structure

```text
Tools/
├── README.md              This file - master tools reference
├── WARP.md                Guidance for WARP terminal (warp.dev)
├── TECHNICAL-REFERENCE.md Logging, save system, code patterns
├── Validation/            Content validators, analyzers, sync tools
├── Debugging/             Reports, debug scripts, backups (safe to delete)
├── Steam/                 Workshop upload scripts and configuration
└── Research/              Native extraction, localization, Qodana analysis
```

---

## Validation Tools

Content and project validation ensures everything follows schema rules and project structure standards.

### Validation Phases

| Phase | What It Checks |
| :--- | :--- |
| 1-4 | JSON structure, localization references, logic, consistency |
| 5 | Orphan detection (unused XML strings) |
| 5.5 | **Opportunity validation** (hints, deprecated 'immediate' field) |
| 6 | Config file validation (baggage_config.json, etc.) |
| 7 | **Project structure** (.csproj completeness, file organization) |
| 8 | **Code quality** (IsCurrentlyAtSea pattern detection) |
| 9 | **C# TextObject localization** (string IDs in code → XML) |
| 9.5 | **Camp schedule descriptions** (meaningful phase descriptions) |

**Phase 5.5 (Opportunity Validation)** validates:

- Hint fields (completeness, length, style)
- Deprecated `immediate` field (removed 2026-01-04)
- Phase definitions (`validPhases` correctness)

**Phase 7 (Project Structure)** validates:

- All `.cs` files in `src/` are in `.csproj`
- All files referenced in `.csproj` actually exist
- GUI assets are properly included
- No rogue files cluttering the root directory

**Phase 9 (C# TextObject)** validates:

- All `TextObject("{=string_id}...")` patterns in C# code
- Verifies string IDs exist in `enlisted_strings.xml`
- Catches missing localization strings referenced from code
- Whitelists: `dbg_`, `test_`, `debug_` prefixes, `DebugToolsBehavior.cs`

### Quick Start

```powershell
# 1. Validate all content
python Tools/Validation/validate_content.py > Tools/Debugging/validation_report.txt

# 2. Compare to baseline (236 events, 0 errors, 299 warnings expected)
# See: Tools/Validation/VALIDATION_BASELINE.md (268 are C# TextObject strings to fix over time)

# 3. Get actionable summary
python Tools/Validation/analyze_validation.py

# 4. Fix issues based on priority

# 5. Re-validate
python Tools/Validation/validate_content.py > Tools/Debugging/validation_report.txt
```

### Tools

| Script | Purpose |
| :--- | :--- |
| `validate_content.py` | Comprehensive validator (content, project structure, .csproj, C# TextObject refs) |
| `analyze_validation.py` | Parse validation reports into prioritized, actionable summaries |
| `sync_event_strings.py` | Extract string IDs from JSON and sync to XML localization |
| **`VALIDATION_BASELINE.md`** | **Expected validation state - 299 warnings (31 acceptable + 268 C# strings to fix)** |
| `validate_events.py` | Legacy event validator (use `validate_content.py` instead) |
| `migrate_schema_v1_to_v2.py` | Convert old schema v1 events to current v2 format |
| `convert_lance_life_events.py` | Convert Lance life events to Enlisted format |
| `inject_fallback_text.py` | Add fallback text to JSON events |
| `generate_tooltips.py` | Generate tooltip content for events |
| `update_tooltips.py` | Update existing tooltips |
| `smoke_events.ps1` | Quick smoke test for event loading |

### Validator Options

```powershell
# Basic validation
python Tools/Validation/validate_content.py

# Strict mode (warnings become errors)
python Tools/Validation/validate_content.py --strict

# Check for orphaned XML strings
python Tools/Validation/validate_content.py --check-orphans

# Generate missing string stubs
python Tools/Validation/validate_content.py --fix-refs
```

### Issue Priority

| Level | Description | Action |
| :--- | :--- | :--- |
| **[CRITICAL]** | Invalid option counts, missing required fields | Fix immediately |
| **[HIGH]** | Missing order XP, long tooltips, invalid skills | Fix before commit |
| **[MEDIUM]** | Schema v1 files, tier/role mismatches | Fix when convenient |
| **[LOW]** | Missing localization (WIP content) | Fix as content completes |

### Safety Features

- **Read-only** - Validators never modify JSON files
- **Unknown fields** - Reported as INFO, not errors
- **Custom skills** - Add to `ModuleData/Enlisted/Config/validation_extensions.json`
- **Schema versioning** - Detects v1 files, suggests migration
- **Fuzzy matching** - Suggests corrections for typos

---

## Debugging Folder

Temporary reports, diagnostic scripts, and backups. **All files safe to delete.**

### Contents

| Type | Files | Description |
| :--- | :--- | :--- |
| **Validation Reports** | `validation_report*.txt` | Content validation output (regenerate anytime) |
| **Qodana Reports** | `qodana_*.txt`, `redundant_qualifiers.txt` | Static analysis output (regenerate with `qodana scan`) |
| **Backups** | `enlisted_strings_backup_*.xml` | XML snapshots before major changes |
| **Backups** | `fallback_backups/*.json` | Event JSON snapshots |
| **Debug Scripts** | `*.ps1` | One-off PowerShell diagnostics |
| **Bug Reports** | `*.md` | Issue investigation notes |

### Cleanup

```powershell
# Delete all validation reports
Remove-Item Tools/Debugging/validation_report*.txt

# Or just delete the whole folder contents (backups are in git history)
```

---

## Steam Workshop

Scripts and configuration for Steam Workshop deployment.

### Upload to Workshop

```powershell
.\Tools\Steam\upload.ps1
```

### Files

| File | Purpose |
| :--- | :--- |
| `upload.ps1` | Main upload script |
| `WORKSHOP_UPLOAD.md` | Full deployment guide |
| `workshop_upload.vdf` | SteamCMD configuration |
| `workshop_upload.resolved.vdf` | Resolved paths for current system |
| `WorkshopUpdate.xml` | Bannerlord workshop manifest |
| `preview.png` | Workshop preview image |

---

## Research Tools

Utilities for analyzing the native game, localization, and codebase.

| Script | Purpose |
| :--- | :--- |
| `extract_native_map_incidents.py` | Extract incident data from game files |
| `generate_language_template.py` | Create XML templates for new language translations |
| `find_articles*.py` | Search and analyze documentation |
| `list_messages.py` | List game messages |
| `parse_qodana.py` | Parse Qodana static analysis reports |
| `extract_issues.py` | Extract issues from reports |

---

## Common Workflows

### Before Committing

```powershell
# Validate content
python Tools/Validation/validate_content.py

# Fix any [ERROR] issues
# Review [WARNING] issues
# Commit
```

### Adding New Events

```powershell
# 1. Read docs/Features/Content/writing-style-guide.md for voice/tone standards
# 2. Create JSON event files
# 3. Validate
python Tools/Validation/validate_content.py > Tools/Debugging/validation_report.txt

# 3. Analyze issues
python Tools/Validation/analyze_validation.py

# 4. Fix critical issues
# 5. Generate missing XML strings
python Tools/Validation/sync_event_strings.py

# 6. Re-validate
python Tools/Validation/validate_content.py
```

### Custom Skills/Roles

To add custom skills without false validation warnings:

```powershell
# Create extension config
cp ModuleData/Enlisted/Config/validation_extensions.json.example validation_extensions.json

# Edit to add your skills
{
  "valid_skills": ["MyCustomSkill"]
}

# Validate - no more false positives
python Tools/Validation/validate_content.py
```

---

## Troubleshooting

### "Validator flags my custom skill as invalid"

Add to `validation_extensions.json`:

```json
{"valid_skills": ["MySkill"]}
```

### "Report file not found"

Run validation first to generate the report:

```powershell
python Tools/Validation/validate_content.py > Tools/Debugging/validation_report.txt
```

### "Analyzer shows 0 issues but validator found many"

Analyzer only shows recognized patterns. Check the raw validation report for full list.

### "Validation passes but content doesn't work in-game"

Validator checks structure, not gameplay logic. Test in-game!

---

## Related Documentation

| Document | Description |
| :--- | :--- |
| [BLUEPRINT.md](../docs/BLUEPRINT.md) | Master project guide, coding standards, architecture |
| [writing-style-guide.md](../docs/Features/Content/writing-style-guide.md) | Writing standards for RP text (voice, tone, tokens) |
| [event-system-schemas.md](../docs/Features/Content/event-system-schemas.md) | JSON schema reference (source of truth) |
| [TECHNICAL-REFERENCE.md](TECHNICAL-REFERENCE.md) | Logging, save system, code patterns |
| [Steam/WORKSHOP_UPLOAD.md](Steam/WORKSHOP_UPLOAD.md) | Steam Workshop deployment guide |

---

## Files Excluded from Git

These files are in `.gitignore`:

```text
# Validation reports (anywhere)
Tools/Debugging/validation*.txt
validation*.txt

# Qodana reports (anywhere)
Tools/Debugging/qodana_*.txt
qodana_*.txt
redundant_qualifiers.txt
```

**Note:** If you see these files in the root directory, they're gitignored but haven't been deleted. Move them to `Tools/Debugging/` or delete them - they can be regenerated anytime.
