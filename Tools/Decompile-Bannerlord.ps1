<#
.SYNOPSIS
    Decompiles a curated list of Bannerlord assemblies into C:\Dev\Enlisted\Decompile II using ilspycmd.

.DESCRIPTION
    - Requires ilspycmd (dotnet global tool). Installs it on first run if missing.
    - Scans the Bannerlord install tree (main bin + every Modules\*\bin\<Platform> folder) for each
      named assembly, then runs `ilspycmd -p -o <outDir>` to emit a C# project tree per assembly.
    - The 40 assembly names below match the folder set under C:\Dev\Enlisted\Decompile (Bannerlord v1.3.13).

.PARAMETER BannerlordRoot
    Path to the Mount & Blade II Bannerlord install. Defaults to the Steam default.

.PARAMETER OutputRoot
    Destination folder. One subfolder per assembly is created.

.PARAMETER Platform
    Platform subfolder name under each bin\. Default Win64_Shipping_Client; use Win64_Shipping_wEditor for editor builds.

.EXAMPLE
    # From an elevated or normal PowerShell prompt:
    pwsh -ExecutionPolicy Bypass -File .\Tools\Decompile-Bannerlord.ps1

.NOTES
    Target: Bannerlord v1.3.13.
    Re-running overwrites each assembly's output folder.
#>

[CmdletBinding()]
param(
    [string] $BannerlordRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string] $OutputRoot     = 'C:\Dev\Enlisted\Decompile II',
    [string] $Platform       = 'Win64_Shipping_Client'
)

$ErrorActionPreference = 'Stop'

# ---- Log every line of output to a transcript so we can diagnose headless runs ----
$LogPath = Join-Path $PSScriptRoot 'Decompile-Bannerlord.log'
try { Stop-Transcript | Out-Null } catch {}
Start-Transcript -Path $LogPath -Force | Out-Null
Write-Host "Logging to: $LogPath" -ForegroundColor DarkGray
Write-Host "PS version: $($PSVersionTable.PSVersion)" -ForegroundColor DarkGray
Write-Host "PWD:        $(Get-Location)" -ForegroundColor DarkGray

# ---- Assembly list (40 items, sorted to match C:\Dev\Enlisted\Decompile) ----
$Assemblies = @(
    'NavalDLC',
    'NavalDLC.GauntletUI',
    'NavalDLC.GauntletUI.Widgets',
    'NavalDLC.View',
    'NavalDLC.ViewModelCollection',
    'SandBox',
    'SandBox.GauntletUI',
    'SandBox.View',
    'SandBox.ViewModelCollection',
    'StoryMode',
    'StoryMode.GauntletUI',
    'StoryMode.View',
    'StoryMode.ViewModelCollection',
    'TaleWorlds.ActivitySystem',
    'TaleWorlds.CampaignSystem',
    'TaleWorlds.CampaignSystem.ViewModelCollection',
    'TaleWorlds.Core',
    'TaleWorlds.Core.ViewModelCollection',
    'TaleWorlds.Engine',
    'TaleWorlds.Engine.GauntletUI',
    'TaleWorlds.GauntletUI',
    'TaleWorlds.GauntletUI.Data',
    'TaleWorlds.GauntletUI.ExtraWidgets',
    'TaleWorlds.GauntletUI.PrefabSystem',
    'TaleWorlds.Library',
    'TaleWorlds.LinQuick',
    'TaleWorlds.Localization',
    'TaleWorlds.ModuleManager',
    'TaleWorlds.MountAndBlade',
    'TaleWorlds.MountAndBlade.GauntletUI',
    'TaleWorlds.MountAndBlade.GauntletUI.Widgets',
    'TaleWorlds.MountAndBlade.Helpers',
    'TaleWorlds.MountAndBlade.View',
    'TaleWorlds.MountAndBlade.ViewModelCollection',
    'TaleWorlds.NavigationSystem',
    'TaleWorlds.ObjectSystem',
    'TaleWorlds.SaveSystem',
    'TaleWorlds.Starter.Library',
    'TaleWorlds.TwoDimension',
    'TaleWorlds.TwoDimension.Standalone'
)

# ---- Validate inputs ----
if (-not (Test-Path -LiteralPath $BannerlordRoot)) {
    throw "Bannerlord install not found at '$BannerlordRoot'. Re-run with -BannerlordRoot <path>."
}
if (-not (Test-Path -LiteralPath $OutputRoot)) {
    New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
}

# ---- Ensure ilspycmd is on PATH (dotnet global tool) ----
function Resolve-IlspyCmd {
    $cmd = Get-Command ilspycmd -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $toolsDir = Join-Path $env:USERPROFILE '.dotnet\tools'
    $candidate = Join-Path $toolsDir 'ilspycmd.exe'
    if (Test-Path -LiteralPath $candidate) {
        $env:PATH = "$toolsDir;$env:PATH"
        return $candidate
    }
    return $null
}

$ilspycmd = Resolve-IlspyCmd
if (-not $ilspycmd) {
    # The latest ilspycmd on NuGet (9.x) is known broken — missing DotnetToolSettings.xml.
    # Try a list of known-good versions in order. Older stable releases work fine.
    $candidateVersions = @('8.2.0.7535', '8.1.0.7464', '8.0.0.7344', '7.2.1.6856')
    # First uninstall any broken prior install so --version takes effect.
    & dotnet tool uninstall --global ilspycmd 2>&1 | Out-Null

    foreach ($ver in $candidateVersions) {
        Write-Host ("Trying ilspycmd {0} ..." -f $ver) -ForegroundColor Yellow
        & dotnet tool install --global ilspycmd --version $ver 2>&1 | Out-Host
        if ($LASTEXITCODE -eq 0) { break }
    }
    $ilspycmd = Resolve-IlspyCmd
}
if (-not $ilspycmd) {
    throw "ilspycmd still not available after trying versions: $($candidateVersions -join ', '). Run ``dotnet --info`` and confirm the .NET SDK is installed, then re-run."
}

Write-Host ''
Write-Host "ilspycmd:   $ilspycmd"       -ForegroundColor Cyan
Write-Host "Bannerlord: $BannerlordRoot" -ForegroundColor Cyan
Write-Host "Output:     $OutputRoot"     -ForegroundColor Cyan
Write-Host "Platform:   $Platform"       -ForegroundColor Cyan
Write-Host ''

# ---- Index every DLL across bin folders once (prefers main bin over module bins) ----
$mainBin = Join-Path $BannerlordRoot "bin\$Platform"
$modulesBins = @()
$modulesRoot = Join-Path $BannerlordRoot 'Modules'
if (Test-Path -LiteralPath $modulesRoot) {
    $modulesBins = Get-ChildItem -LiteralPath $modulesRoot -Directory -ErrorAction SilentlyContinue |
                   ForEach-Object { Join-Path $_.FullName "bin\$Platform" } |
                   Where-Object   { Test-Path -LiteralPath $_ }
}
$searchRoots = @($mainBin) + $modulesBins | Where-Object { Test-Path -LiteralPath $_ }

$dllIndex = @{}
foreach ($root in $searchRoots) {
    Get-ChildItem -LiteralPath $root -Filter *.dll -ErrorAction SilentlyContinue | ForEach-Object {
        $key = [IO.Path]::GetFileNameWithoutExtension($_.Name)
        if (-not $dllIndex.ContainsKey($key)) { $dllIndex[$key] = $_.FullName }
    }
}

Write-Host ("Indexed {0} unique DLLs across {1} bin folders." -f $dllIndex.Count, $searchRoots.Count) -ForegroundColor Cyan
Write-Host ''

# ---- Decompile each assembly ----
$results = New-Object System.Collections.Generic.List[object]
$index = 0
foreach ($name in $Assemblies) {
    $index++
    $tag  = '[{0,2}/{1}]' -f $index, $Assemblies.Count
    $outDir = Join-Path $OutputRoot $name

    if (-not $dllIndex.ContainsKey($name)) {
        Write-Host ("{0} MISSING  {1} (no DLL found under {2})" -f $tag, $name, $Platform) -ForegroundColor Red
        $results.Add([pscustomobject]@{ Assembly = $name; Status = 'MissingDll'; Path = ''; Output = $outDir })
        continue
    }

    $dllPath = $dllIndex[$name]
    Write-Host ("{0} EXPORT   {1,-50} <- {2}" -f $tag, $name, $dllPath) -ForegroundColor Green
    if (Test-Path -LiteralPath $outDir) { Remove-Item -LiteralPath $outDir -Recurse -Force }
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    try {
        & $ilspycmd -p -o $outDir $dllPath 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "ilspycmd exited with code $LASTEXITCODE" }
        $results.Add([pscustomobject]@{ Assembly = $name; Status = 'OK'; Path = $dllPath; Output = $outDir })
    } catch {
        Write-Host ("{0} FAIL     {1}: {2}" -f $tag, $name, $_.Exception.Message) -ForegroundColor Red
        $results.Add([pscustomobject]@{ Assembly = $name; Status = 'Error'; Path = $dllPath; Output = $outDir })
    }
}

# ---- Summary ----
Write-Host ''
Write-Host '=== Summary ===' -ForegroundColor Cyan
$results | Format-Table Assembly, Status, Path -AutoSize | Out-Host

$ok      = ($results | Where-Object { $_.Status -eq 'OK' }).Count
$missing = ($results | Where-Object { $_.Status -eq 'MissingDll' }).Count
$errors  = ($results | Where-Object { $_.Status -eq 'Error' }).Count
Write-Host ("{0} OK   {1} Missing   {2} Errors   (out of {3})" -f $ok, $missing, $errors, $Assemblies.Count) -ForegroundColor Cyan

if ($missing -gt 0) {
    Write-Host ''
    Write-Host 'Tip: If any assemblies are MISSING, the DLL name in Bannerlord may differ from the folder name in the old Decompile set.' -ForegroundColor Yellow
    Write-Host 'Pass -Platform Win64_Shipping_wEditor to search the editor build instead.' -ForegroundColor Yellow
}

try { Stop-Transcript | Out-Null } catch {}
