# AGENTS.md — Tools

> Parent: [../AGENTS.md](../AGENTS.md)
> Handbook: [../docs/INDEX.md](../docs/INDEX.md) (no dedicated mirror)

This file owns Tools/ rules: build configurations, deploy scripts, and the workshop upload flow. The Validation subdir has its own AGENTS.md at [Validation/AGENTS.md](Validation/AGENTS.md).

---

## Build configurations

The full build-configuration depth currently lives in [../docs/BUILD-CONFIGURATIONS.md](../docs/BUILD-CONFIGURATIONS.md). Phase G of the 2026-05-24 docs restructure folds the worth-keeping bits into THIS file and archives BUILD-CONFIGURATIONS.md.

Until Phase G ships, refer to that file for the depth. The top-level build command lives in root [../AGENTS.md](../AGENTS.md) Quick Commands.

---

## Deploy scripts

- [Steam/upload.ps1](Steam/upload.ps1) — Steam Workshop upload. Requires `STEAM_USERNAME` / `STEAM_PASSWORD` env vars (or interactive login). Uses the Workshop app ID at Steam Workshop page.
- [Decompile-Bannerlord.bat](Decompile-Bannerlord.bat) — Windows: regenerate the TaleWorlds decompile reference. See root [../AGENTS.md](../AGENTS.md) Quick Commands for the WSL equivalent (`ilspycmd` flow).
- [Decompile-Bannerlord.ps1](Decompile-Bannerlord.ps1) — PowerShell variant; cross-platform with `pwsh` if available (note: Windows `.exe` interop is broken from WSL — see root AGENTS.md Platform notes).

---

## Workshop publish flow

1. Build clean: `dotnet build -c "Enlisted RETAIL" /p:Platform=x64`
2. Validate: `python3 Tools/Validation/validate_content.py` passes
3. Upload: `./Tools/Steam/upload.ps1`

Workshop page: [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3621116083).

---

## See also

- [Validation/AGENTS.md](Validation/AGENTS.md) — validator + error-codes + lint stack
- [README.md](README.md) — Tools/ catalog
- [TECHNICAL-REFERENCE.md](TECHNICAL-REFERENCE.md) — logging, saves, dialogue, menu patterns reference
