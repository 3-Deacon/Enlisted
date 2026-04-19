# Build Configuration & Optional Battle AI SubModule

**Summary:** Quick guide for building the Enlisted mod with optional Battle AI SubModule that users can toggle in the Bannerlord launcher.

**Status:** ✅ Current  
**Last Updated:** 2026-04-18  
**Related Docs:** [BLUEPRINT.md](BLUEPRINT.md), [BATTLE-AI-IMPLEMENTATION-SPEC.md](Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md)

---

## Single Build with Optional Features

The project produces **one mod** from a single build configuration. Users can enable/disable Battle AI via checkbox in the Bannerlord launcher without redownloading or rebuilding.

| Configuration | Module ID | Output Path | Features |
|--------------|-----------|-------------|----------|
| `Enlisted RETAIL` | `Enlisted` | `Modules\Enlisted\` | Core mod + Optional Battle AI SubModule |

A single build invocation produces the assembly in both `bin\Win64_Shipping_Client\` (the default game build — hard-crashes on errors) and `bin\Win64_Shipping_wEditor\` (the editor build — surfaces errors more gracefully). The csproj `AfterBuild` target mirrors the DLL + PDB from Shipping_Client into Shipping_wEditor so testing in either mode uses the same compiled code.

**User Control:**
- ☑️ **Enlisted Core** (required)
- ☑️ **Enlisted Battle AI** (optional, can uncheck in launcher)

---

## Build Commands

### PowerShell
```powershell
cd C:\Dev\Enlisted\Enlisted
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

### Visual Studio/Rider
1. Select configuration dropdown: **"Enlisted RETAIL"**
2. Set platform: **x64**
3. Build

**Output Locations** (one build, both populated):
```
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\bin\Win64_Shipping_Client\
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\bin\Win64_Shipping_wEditor\
```

If the launcher (`BannerlordLauncher`, `Watchdog`) is running, it holds `Enlisted.dll` open and the copy step fails with `MSB3021`. Close the launcher before building.

---

## How Optional SubModule Works

### Single Build, Always Includes Battle AI

The `BATTLE_AI` preprocessor constant is **always defined**, so Battle AI code is always compiled into the DLL.

```csharp
#if BATTLE_AI
using TaleWorlds.MountAndBlade;

namespace Enlisted.Features.Combat.BattleAI
{
    public class BattleOrchestrator
    {
        // Always compiled, but only runs if SubModule is enabled
    }
}
#endif
```

### Build Configuration

**`Enlisted RETAIL` Build:**
- DefineConstants: `TRACE;BATTLE_AI`
- Output: `Modules\Enlisted\`
- Uses: `SubModule.xml` (contains both Core and Battle AI SubModules)

### SubModule Structure

**SubModule.xml contains TWO SubModule entries:**
```xml
<Module>
    <Name value="Enlisted"/>
    <Id value="Enlisted"/>
    <SubModules>
        <!-- Core SubModule (Required) -->
        <SubModule>
            <Name value="Enlisted Core"/>
            <DLLName value="Enlisted.dll"/>
            <SubModuleClassType value="Enlisted.Mod.Entry.SubModule"/>
        </SubModule>
        
        <!-- Battle AI SubModule (Optional) -->
        <SubModule>
            <Name value="Enlisted Battle AI"/>
            <DLLName value="Enlisted.dll"/>
            <SubModuleClassType value="Enlisted.Features.Combat.BattleAI.BattleAISubModule"/>
        </SubModule>
    </SubModules>
</Module>
```

**In Bannerlord Launcher, users see:**
- ☑️ Enlisted Core (keep enabled)
- ☑️ Enlisted Battle AI (can uncheck to disable)

When "Enlisted Battle AI" is unchecked, `BattleAISubModule` never initializes = **zero performance cost**.

---

## File Organization

### Battle AI Source Code

All Battle AI files go in:
```
src/Features/Combat/BattleAI/
├── BattleAISubModule.cs              # SubModule entry point
├── Behaviors/
│   └── EnlistedBattleAIBehavior.cs
├── Orchestration/
│   ├── BattleOrchestrator.cs
│   └── BattleContext.cs
├── Formation/
│   └── FormationController.cs
└── Agent/
    └── AgentCombatEnhancer.cs
```

### Module Configuration

Single `SubModule.xml` with two SubModule entries:
- **Core SubModule**: `Enlisted.Mod.Entry.SubModule` (required)
- **Battle AI SubModule**: `Enlisted.Features.Combat.BattleAI.BattleAISubModule` (optional)

---

## Adding Battle AI Files

When creating new Battle AI files:

1. **Create file** in `src/Features/Combat/BattleAI/`
2. **Wrap in conditional**:
   ```csharp
   #if BATTLE_AI
   // ... your code ...
   #endif
   ```
3. **Add to .csproj**:
   ```xml
   <Compile Include="src\Features\Combat\BattleAI\YourNewFile.cs"/>
   ```
4. **Build both versions** to verify

---

## Verification

After building, verify the module configuration:

```powershell
# Check that SubModule.xml has both SubModule entries
type "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\SubModule.xml" | findstr /C:"<Name value"

# Should show:
# <Name value="Enlisted"/>
# <Name value="Enlisted Core"/>
# <Name value="Enlisted Battle AI"/>
```

---

## Common Issues

### Battle AI SubModule not appearing in launcher
**Problem:** SubModule.xml doesn't have Battle AI SubModule entry  
**Solution:** Verify SubModule.xml has both `Enlisted Core` and `Enlisted Battle AI` SubModule entries

### BattleAISubModule class not found error
**Problem:** Class doesn't exist or not added to .csproj  
**Solution:** Create `src/Features/Combat/BattleAI/BattleAISubModule.cs` and add to .csproj

### Battle AI code not running even when enabled
**Problem:** Missing `#if BATTLE_AI` wrapper or activation gate failing  
**Solution:** All Battle AI files must use `#if BATTLE_AI` and check `EnlistmentState.IsEnlisted`

---

## Quick Status Check

To verify your build:

```powershell
# Check DLL last-modified time in BOTH platform folders (should match)
Get-ChildItem "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\bin\Win64_Shipping_Client\Enlisted.dll" | Select LastWriteTime
Get-ChildItem "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\bin\Win64_Shipping_wEditor\Enlisted.dll" | Select LastWriteTime

# Verify SubModule.xml has both SubModules
type "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\SubModule.xml" | findstr /C:"SubModuleClassType"
# Should show:
#   <SubModuleClassType value="Enlisted.Mod.Entry.SubModule"/>
#   <SubModuleClassType value="Enlisted.Features.Combat.BattleAI.BattleAISubModule"/>
```

---

## Integration Points

### Battle AI SubModule Entry Point

```csharp
#if BATTLE_AI
using TaleWorlds.MountAndBlade;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Combat.BattleAI
{
    /// <summary>
    /// Optional SubModule for Battle AI. Users can disable in Bannerlord launcher.
    /// </summary>
    public class BattleAISubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLogger.Info("BattleAI", "Battle AI SubModule loaded");
        }
        
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            // Register mission behavior callbacks
        }
    }
}
#endif
```

### Battle AI Activation Gate

```csharp
#if BATTLE_AI
// In BattleAISubModule or behavior registration
private void RegisterBattleAIForMission(Mission mission)
{
    // Only field battles
    if (mission.Scene.GetName().Contains("siege") || 
        mission.Scene.GetName().Contains("naval"))
    {
        return;
    }
    
    // Only when player is enlisted
    if (!EnlistmentState.IsEnlisted)
    {
        return;
    }
    
    // Add Battle AI behaviors
    mission.AddMissionBehavior(new EnlistedBattleAIBehavior());
}
#endif
```

---

## See Also

- [BLUEPRINT.md - Build & Deployment](BLUEPRINT.md#build--deployment) - Complete dual-build documentation
- [BATTLE-AI-IMPLEMENTATION-SPEC.md](Features/Combat/BATTLE-AI-IMPLEMENTATION-SPEC.md) - Battle AI implementation guide
- [Enlisted.csproj](../Enlisted.csproj) - Project file with detailed comments
