# Project Resources

**Summary:** Documentation for workspace resources (Decompile, steamcmd, SQLite3) that support development but are not part of the mod build.

**Status:** ✅ Current
**Last Updated:** 2026-01-16
**Related Docs:** [BLUEPRINT.md](BLUEPRINT.md), [DEVELOPER-GUIDE.md](DEVELOPER-GUIDE.md)

---

## Overview

The project uses external reference resources that are **not tracked in Git**:

```
C:\Dev\Enlisted\
├── Enlisted\          ← Your workspace (Git repo)
└── Decompile\         ← Bannerlord v1.3.13 decompiled source (reference only, not in Git)
```

These resources are **installed separately per-platform** and are **not part of the mod build**.

---

## Decompile (Bannerlord Source Reference)

**Purpose:** Bannerlord v1.3.13 decompiled source code for API verification

**Location:** `C:\Dev\Enlisted\Decompile\` (sibling to workspace)

**Setup:**
1. Decompile Bannerlord v1.3.13 using your preferred tool (ILSpy, dotPeek, etc.)
2. Extract to `C:\Dev\Enlisted\Decompile\`
3. AI tools will auto-detect it (checks multiple locations)

**Why Outside Git:**
- ✅ Keeps repo clean (77.5 MB, 6,306 files)
- ✅ No search pollution in GitHub
- ✅ Faster clones
- ✅ Clear separation: your code vs reference

**Usage:**
- Always verify APIs against this decompile first (not online docs)
- Check method signatures, property types, enum values
- Understand native behavior before patching with Harmony
- Reference for feature specifications

**Key Assemblies:**
- `TaleWorlds.CampaignSystem` - Party, Settlement, Campaign behaviors
- `TaleWorlds.Core` - CharacterObject, ItemObject
- `TaleWorlds.Library` - Vec2, MBList, utility classes
- `TaleWorlds.MountAndBlade` - Mission, Agent, combat
- `SandBox.View` - Menu views, map handlers

**Alternative Locations:**
- Set `BANNERLORD_DECOMPILE_PATH` environment variable
- Or place in workspace at `Decompile/` (not recommended for Git)

**Example:**
```bash
# Search for API usage
grep -r "ChangeHeroGold" C:\Dev\Enlisted\Decompile\TaleWorlds.CampaignSystem\
```

---

## SteamCMD (Installed Separately)

**Purpose:** Command-line tool for uploading to Steam Workshop

**Installation:**

**Windows:**
- Download from: https://developer.valvesoftware.com/wiki/SteamCMD
- Extract to any location (e.g., `C:\Dev\steamcmd\`)
- Or the upload script will auto-download on first use

**Linux:**
```bash
# Ubuntu/Debian
sudo apt install steamcmd

# Or manual install
mkdir ~/steamcmd && cd ~/steamcmd
curl -sqL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" | tar zxvf -
```

**Usage:**

The `Tools/Steam/upload.ps1` script will:
1. Check PATH for steamcmd
2. Check common install locations
3. Prompt to download if not found

**See Also:** [Tools/Steam/WORKSHOP_UPLOAD.md](../Tools/Steam/WORKSHOP_UPLOAD.md)

---

## Build Exclusion

These folders do not interfere with builds:

1. **Not in .csproj** - C# project doesn't reference them
2. **Not copied to output** - Build targets ignore these folders
3. **Git tracked** - Available on all platforms for development
4. **No .gitignore exclusion** - Kept in repo for cross-platform work

---

## Cross-Platform Development

**Why in Git:**
- Work seamlessly between Windows and Linux
- Ensure consistent API reference across platforms
- Share tools and databases with collaborators
- Single source of truth for decompiled code

**Path References:**
- All documentation uses **workspace-relative paths**: `Decompile/`, `steamcmd/`, `SQLite3/`
- No hardcoded Windows paths like `C:\Dev\Enlisted\Decompile\`
- Scripts auto-detect platform and adjust paths accordingly

---

## Adding New Resources

If you add new development resources:

1. Place in workspace root (not in `src/`, `docs/`, or `Tools/`)
2. Document in this file
3. Update [BLUEPRINT.md](BLUEPRINT.md) if it's a critical reference
4. Use relative paths in all documentation
5. Add platform-specific handling if needed

---

## Quick Reference

| Resource | Purpose | Location | In Git? |
|----------|---------|----------|---------|
| `Decompile/` | Bannerlord v1.3.13 API reference | `C:\Dev\Enlisted\Decompile\` (sibling to workspace) | ❌ No |
| `steamcmd` | Workshop upload tool | System install | ❌ No |

---

**Last updated:** 2026-01-16 (Removed CrewAI references)
