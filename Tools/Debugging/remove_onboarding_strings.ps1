. (Join-Path $PSScriptRoot '..\\Write-Status.ps1')

# PowerShell script to remove onboarding-related localization strings
# Removes all <string id="ll_evt_*onboard*"> entries from enlisted_strings.xml

$xmlPath = "ModuleData\Languages\enlisted_strings.xml"

if (-not (Test-Path $xmlPath)) {
    Write-Error "File not found: $xmlPath"
    exit 1
}

Write-Status "Reading $xmlPath..."

# Read all lines
$lines = Get-Content $xmlPath

$outputLines = @()
$skipMode = $false
$removedCount = 0

foreach ($line in $lines) {
    # Check if this line starts an onboarding string tag
    if ($line -match '<string id="[^"]*onboard[^"]*"') {
        $skipMode = $true
        $removedCount++
        # Check if it's a self-closing or single-line tag
        if ($line -match '</string>') {
            $skipMode = $false
        }
        continue
    }
    
    # If we're in skip mode, look for closing tag
    if ($skipMode) {
        if ($line -match '</string>') {
            $skipMode = $false
        }
        continue
    }
    
    # Keep this line
    $outputLines += $line
}

Write-Status "Found and removed $removedCount onboarding strings"

if ($removedCount -eq 0) {
    Write-Status "No onboarding strings found. File is already clean."
    exit 0
}

# Write back to file with UTF-8 encoding (no BOM)
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllLines($xmlPath, $outputLines, $utf8NoBom)

Write-Status "Successfully cleaned $xmlPath"
Write-Status "Backup: Original file is in git history (git restore to undo)"
