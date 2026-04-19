# Steam Workshop Upload Script for Enlisted Mod
# Run from PowerShell: .\upload.ps1
#
# Prerequisites:
# 1. SteamCMD installed (set path below)
# 2. workshop_upload.vdf configured with correct paths
# 3. preview.png created

param(
    [string]$SteamCmdPath = "",
    [string]$SteamUser = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)

# Auto-detect steamcmd in workspace root if not specified
if ([string]::IsNullOrEmpty($SteamCmdPath)) {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        $SteamCmdPath = Join-Path $WorkspaceRoot "steamcmd\steamcmd.exe"
    } else {
        $SteamCmdPath = Join-Path $WorkspaceRoot "steamcmd/steamcmd.sh"
    }
}
$VdfPath = Join-Path $ScriptDir "workshop_upload.vdf"
$PreviewPath = Join-Path $ScriptDir "preview.png"

Write-Host "=== Enlisted Steam Workshop Uploader ===" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
if (-not (Test-Path $SteamCmdPath)) {
    Write-Host "ERROR: SteamCMD not found at: $SteamCmdPath" -ForegroundColor Red
    Write-Host "Expected location: steamcmd/ in workspace root"
    Write-Host "Or specify path: .\upload.ps1 -SteamCmdPath '/path/to/steamcmd'"
    exit 1
}

if (-not (Test-Path $VdfPath)) {
    Write-Host "ERROR: workshop_upload.vdf not found at: $VdfPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $PreviewPath)) {
    Write-Host "WARNING: preview.png not found. Upload may fail." -ForegroundColor Yellow
    Write-Host "Create a 512x512 or 1024x1024 PNG image as your Workshop thumbnail."
    Write-Host ""
}

# Read VDF to check for TODOs
$vdfContent = Get-Content $VdfPath -Raw
if ($vdfContent -match "TODO") {
    Write-Host "WARNING: workshop_upload.vdf contains TODO markers." -ForegroundColor Yellow
    Write-Host "Please update the following fields before uploading:"
    Write-Host "  - contentfolder: Path to your Modules\Enlisted folder"
    Write-Host "  - previewfile: Path to your preview.png"
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne "y") {
        exit 0
    }
}

# Resolve preview path and write a temp VDF with an absolute previewfile to avoid machine-specific paths
$resolvedPreviewPath = $PreviewPath
if (Test-Path $PreviewPath) {
    $resolvedPreviewPath = (Resolve-Path $PreviewPath).Path
}

$tempVdfPath = Join-Path $ScriptDir "workshop_upload.resolved.vdf"
$vdfResolved = $vdfContent -replace '"previewfile"\s+"[^"]+"', "`"previewfile`" `"$resolvedPreviewPath`""
Set-Content -Path $tempVdfPath -Value $vdfResolved -Encoding ASCII

# Prompt for Steam username if not provided
if ([string]::IsNullOrEmpty($SteamUser)) {
    $SteamUser = Read-Host "Enter your Steam username"
}

Write-Host ""
Write-Host "Uploading to Steam Workshop..." -ForegroundColor Green
Write-Host "VDF: $tempVdfPath"
Write-Host ""

# Run SteamCMD
& $SteamCmdPath +login $SteamUser +workshop_build_item $tempVdfPath +quit

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Upload completed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Check your Steam Workshop page for the new/updated item"
    Write-Host "2. If this was your first upload, note the Workshop Item ID"
    Write-Host "3. Update publishedfileid in workshop_upload.vdf for future updates"
    Write-Host "4. Change visibility to Public when ready"
} else {
    Write-Host ""
    Write-Host "Upload failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    Write-Host "Check the output above for error details."
}

