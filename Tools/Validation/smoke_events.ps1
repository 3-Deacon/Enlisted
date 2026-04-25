. (Join-Path $PSScriptRoot '..\\Write-Status.ps1')

$ErrorActionPreference = "Stop"

# Offline smoke test for Lance Life events:
# - Regenerates JSON packs from markdown
# - Validates basic schema invariants (unique IDs, 2–4 options)
# - Builds the mod
#
# This does NOT simulate Bannerlord runtime (campaign state, menus, inquiry popups).

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

Write-Status "[smoke] Regenerating event packs from docs..." -ForegroundColor Cyan
python tools/events/convert_lance_life_events.py --write
if ($LASTEXITCODE -ne 0) { throw "Event conversion failed (exit=$LASTEXITCODE)" }

Write-Status "[smoke] Validating event packs..." -ForegroundColor Cyan
python tools/events/validate_events.py
if ($LASTEXITCODE -ne 0) { throw "Event validation failed (exit=$LASTEXITCODE)" }

Write-Status "[smoke] Building Enlisted..." -ForegroundColor Cyan
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit=$LASTEXITCODE)" }

Write-Status "[smoke] OK" -ForegroundColor Green


