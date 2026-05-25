# AGENTS.md — Tools

> Parent: [../AGENTS.md](../AGENTS.md)
> Handbook: [../docs/INDEX.md](../docs/INDEX.md) (no dedicated mirror)

This file owns Tools/ rules: build configurations, deploy scripts, and the workshop upload flow. The Validation subdir has its own AGENTS.md at [Validation/AGENTS.md](Validation/AGENTS.md).

---

## Build configurations

Single build configuration: `Enlisted RETAIL` (Platform `x64`). Produces one mod from one build invocation. Output: `Modules\Enlisted\` (Core + optional Battle AI SubModule).

```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

**DLL mirror:** csproj `AfterBuild` target mirrors `Enlisted.dll` + `Enlisted.pdb` from `bin\Win64_Shipping_Client\` into `bin\Win64_Shipping_wEditor\`, so testing in either game mode uses the same compiled binary.

**Build failure footgun:** Close `BannerlordLauncher.exe` before building. It holds the DLL open and the AfterBuild mirror fails with `MSB3021` (cannot copy file in use).

**Optional Battle AI SubModule:** Users toggle via Bannerlord launcher checkbox. `SubModule.xml` declares two entries:

```xml
<SubModuleClassType value="Enlisted.Mod.Entry.SubModule"/>            <!-- required -->
<SubModuleClassType value="Enlisted.Features.Combat.BattleAISubModule"/> <!-- optional -->
```

Both compile into the same DLL; the launcher decides which class instantiates at runtime.

**Adding Battle AI files:** new `.cs` under `src/Features/Combat/BattleAI/` needs an explicit `<Compile Include>` in `Enlisted.csproj` (wildcards are non-recursive — see [../ModuleData/Enlisted/AGENTS.md](../ModuleData/Enlisted/AGENTS.md) wildcards quirk).

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
