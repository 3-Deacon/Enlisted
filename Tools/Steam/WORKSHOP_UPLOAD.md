# Steam Workshop Upload Guide for Enlisted

This guide covers how to publish the Enlisted mod to the Steam Workshop for Mount & Blade II: Bannerlord.

## Quick Start - How to Upload

**THE SIMPLE WAY (Do this first):**

Open PowerShell and run:
```powershell
cd C:\Dev\Enlisted\Enlisted
.\Tools\Steam\upload.ps1
```

Or from the project root, just:
```powershell
.\Tools\Steam\upload.ps1
```

**If that doesn't work (script exits without prompting):**

The script needs to run in an interactive PowerShell window. Use this command to launch it:
```powershell
Start-Process powershell.exe -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-File", "C:\Dev\Enlisted\Enlisted\Tools\Steam\upload.ps1"
```

**What the script does:**
1. Prompts for your Steam username
2. Asks for your Steam password
3. Requests your Steam Guard code
4. Uploads to Workshop ID 3621116083

**That's it.** The rest of this document is for troubleshooting and understanding the process.

---

## IMPORTANT: VDF Character Limits

**CRITICAL:** SteamCMD's VDF parser has strict character limits:
- **Description field:** ~2000 characters max (with BBCode)
- **Changenote field:** ~500 characters max
- If you exceed these, you'll get: `CKeyValuesSystem::AddStringToPool: key name too long`

**Solution:** Keep descriptions concise. Use BBCode efficiently. Test with short changenotes.

## Prerequisites

1. **Steam Account** with Bannerlord (AppID: 261550)
2. **SteamCMD** - Already included in `steamcmd/` folder (workspace root)
3. **Built mod** in your Bannerlord Modules folder
4. **Preview image** (512x512 or 1024x1024 PNG, under 1MB)

## First-Time Setup

### 1. SteamCMD Location

SteamCMD is included in the workspace at `steamcmd/` (cross-platform compatible).

**Windows:** Use `steamcmd\steamcmd.exe`
**Linux:** Use `steamcmd/steamcmd.sh` (may need to make executable: `chmod +x steamcmd/steamcmd.sh`)

### 2. Prepare Your Mod

Make sure your mod is built and working:

```powershell
cd C:\Dev\Enlisted\Enlisted
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

Your mod files should be in the Bannerlord Modules folder:
```
<Bannerlord Install>\Modules\Enlisted\
├── SubModule.xml
├── bin\Win64_Shipping_Client\Enlisted.dll
├── GUI\
├── ModuleData\
└── ...
```

### 3. Create Preview Image

Replace `preview.png.placeholder` with an actual `preview.png` image:
- Dimensions: 512x512 or 1024x1024 pixels (square)
- Format: PNG
- Size: Under 1MB

### 4. Configure workshop_upload.vdf

Edit `workshop_upload.vdf` and update these paths:

```
"contentfolder" "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted"
"previewfile" "C:\Dev\Enlisted\Enlisted\tools\workshop\preview.png"
```

## Uploading to Steam Workshop

### Using the Upload Script (Recommended)

**From project root, run:**
```powershell
.\Tools\Steam\upload.ps1
```

This will:
1. Prompt for your Steam username
2. Prompt for your Steam password
3. Request your Steam Guard code
4. Generate resolved VDF with absolute paths
5. Upload to Workshop ID 3621116083

### Manual Upload via SteamCMD

If the script fails, you can upload manually:

1. Open terminal/PowerShell in interactive mode (NOT through automation)
2. Navigate to workspace steamcmd folder:
   ```bash
   # Windows PowerShell
   cd steamcmd
   
   # Linux
   cd steamcmd
   ```

3. Login and upload:
   ```bash
   # Windows
   .\steamcmd.exe +login YOUR_STEAM_USERNAME +workshop_build_item "..\Tools\Steam\workshop_upload.vdf" +quit
   
   # Linux
   ./steamcmd.sh +login YOUR_STEAM_USERNAME +workshop_build_item "../Tools/Steam/workshop_upload.vdf" +quit
   ```
   
   You'll be prompted for your password and Steam Guard code.

### First Upload Only (Creating New Workshop Item)

**Skip this if workshop item already exists (ID: 3621116083)**

After first successful upload, Steam assigns a Workshop Item ID. Update `workshop_upload.vdf`:
```
"publishedfileid" "YOUR_NEW_ID"
```

## VDF File Structure

**Key Fields:**
- `description` - Main Workshop page body (USE BBCode: [h1], [h2], [list], [b], [i], [url])
- `changenote` - Update notes (plain text or BBCode, keep SHORT)
- `visibility` - 0=Public, 1=Friends, 2=Hidden

**Current Configuration:**
- Visibility: Public (0)
- Workshop ID: 3621116083
- Description: Full feature list + troubleshooting
- Changenote: Version-specific updates

## Workshop Folder Paths

**IMPORTANT:** For Workshop subscribers, logs go to the WORKSHOP folder, not Modules!

**Workshop Install Path:**
```
C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\3621116083\
```

**Debugging Logs Location:**
```
C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\3621116083\Debugging\
```

**Manual Install Path (for reference):**
```
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\
```

When writing user-facing documentation, ALWAYS use the workshop path for Steam Workshop users.

## Verification Checklist

Before making public, verify:

- [ ] Mod loads correctly when subscribed via Workshop
- [ ] SubModule.xml is present and valid
- [ ] All required DLLs are included
- [ ] Preview image displays correctly
- [ ] Description renders properly (check Steam formatting)
- [ ] Tags are appropriate
- [ ] Dependencies are listed (Harmony requirement mentioned)

After publishing:

- [ ] Subscribe to your own mod
- [ ] Launch Bannerlord with only Workshop version enabled
- [ ] Test basic functionality (enlist, battle, duties, etc.)
- [ ] Check for any missing files or errors

## Troubleshooting

### Workshop vs Manual Installation Locations

**Steam Workshop subscribers:** The mod installs to:
```
C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\3621116083\
```

**Manual/Nexus users:** The mod should be in:
```
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\
```

**IMPORTANT:** If you have both the Workshop version AND a manual install, you'll see duplicate entries in the Bannerlord launcher. This can cause conflicts. Unsubscribe from Workshop if you're testing a manual build.

### Mod Conflict Detection

The mod automatically detects conflicts with other mods at startup. After launching the game with Enlisted enabled, check:
```
<Bannerlord>\Modules\Enlisted\Debugging\Conflicts-A_{timestamp}.log
```

This log shows if other mods are patching the same game methods as Enlisted, which helps diagnose incompatibilities.

### "Login Failed"
- Verify Steam credentials
- Check Steam Guard code
- Ensure your account owns Bannerlord

### "Item Update Failed"
- Verify contentfolder path exists
- Check previewfile path is correct
- Ensure preview image is under 1MB
- Verify AppID is 261550

### "Missing Content"
- Make sure SubModule.xml is in the root of contentfolder
- Verify bin folder contains the compiled DLL
- Check file permissions

## File Structure Reference

```
tools/workshop/
├── WORKSHOP_UPLOAD.md      (this file)
├── workshop_upload.vdf      (upload configuration)
├── preview.png              (your workshop thumbnail)
└── preview.png.placeholder  (delete after adding real image)
```

## Common Issues & Solutions

### "key name too long" Error

**Problem:** VDF parser fails with `CKeyValuesSystem::AddStringToPool: key name too long`

**Cause:** Description or changenote exceeds character limits

**Solution:**
1. Shorten description (aim for ~2000 chars with BBCode)
2. Shorten changenote (aim for ~500 chars)
3. Delete `workshop_upload.resolved.vdf` and retry

### Upload Script Doesn't Prompt for Username

**Problem:** Script runs but exits without prompting

**Cause:** Running in non-interactive PowerShell session

**Solution:** Open script in NEW PowerShell window:
```powershell
Start-Process powershell.exe -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-File", "C:\Dev\Enlisted\Enlisted\Tools\Steam\upload.ps1"
```

### Changes Not Appearing on Workshop

**Problem:** Uploaded but description unchanged

**Cause:** Cached resolved.vdf file with old content

**Solution:**
1. Delete `Tools\Steam\workshop_upload.resolved.vdf`
2. Re-run upload script

## Quick Reference

| Field | Value |
|-------|-------|
| AppID | 261550 |
| Workshop ID | 3621116083 |
| Mod Name | Enlisted |
| Current Version | v0.9.1.4 |
| Target Game | Bannerlord v1.3.13 |
| Visibility | 0 (Public) |
| Upload Script | `.\Tools\Steam\upload.ps1` |

