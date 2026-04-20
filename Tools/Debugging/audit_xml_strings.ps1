. (Join-Path $PSScriptRoot '..\\Write-Status.ps1')

# XML String Audit Script
# This script analyzes all XML string IDs and checks their usage in the codebase

$ErrorActionPreference = "Continue"
$projectRoot = "C:\Dev\Enlisted\Enlisted"
$outputFile = "$projectRoot\Debugging\xml_audit_report.txt"
$stringIdsFile = "$projectRoot\Debugging\xml_string_ids.txt"

Write-Status "Starting XML String Audit..." -ForegroundColor Cyan
Write-Status "Project Root: $projectRoot" -ForegroundColor Gray
Write-Status ""

# Load all string IDs
$stringIds = Get-Content $stringIdsFile
Write-Status "Total XML String IDs: $($stringIds.Count)" -ForegroundColor Yellow
Write-Status ""

# Get all C# source files
$csFiles = Get-ChildItem -Path $projectRoot -Recurse -Include *.cs | 
    Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

Write-Status "Scanning $($csFiles.Count) C# source files..." -ForegroundColor Yellow
Write-Status ""

# Initialize results
$usedStrings = @{}
$unusedStrings = @()
$totalReferences = 0

# Progress tracking
$progress = 0
$totalStrings = $stringIds.Count

foreach ($stringId in $stringIds) {
    $progress++
    if ($progress % 100 -eq 0) {
        Write-Status "Progress: $progress / $totalStrings ($([math]::Round($progress/$totalStrings*100, 1))%)" -ForegroundColor Gray
    }
    
    # Search for this string ID in all C# files
    $found = $false
    $locations = @()
    
    foreach ($file in $csFiles) {
        try {
            $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
            if ($content -match [regex]::Escape($stringId)) {
                $found = $true
                $relativePath = $file.FullName.Replace($projectRoot, "").TrimStart('\')
                $locations += $relativePath
                $totalReferences++
            }
        } catch {
            Write-Verbose "Skipping unreadable file: $($file.FullName)"
        }
    }
    
    if ($found) {
        $usedStrings[$stringId] = $locations
    } else {
        $unusedStrings += $stringId
    }
}

Write-Status ""
Write-Status "Analysis Complete!" -ForegroundColor Green
Write-Status ""

# Generate report
$report = @"
================================================================================
XML STRING AUDIT REPORT
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
================================================================================

SUMMARY
-------
Total XML String IDs:     $($stringIds.Count)
Used Strings:             $($usedStrings.Count)
Unused Strings:           $($unusedStrings.Count)
Total Code References:    $totalReferences
Usage Rate:               $([math]::Round($usedStrings.Count / $stringIds.Count * 100, 2))%

================================================================================
UNUSED STRINGS (Potential Dead Code)
================================================================================

"@

if ($unusedStrings.Count -gt 0) {
    $report += "The following $($unusedStrings.Count) string IDs were not found in any C# source files:`n`n"
    foreach ($unused in $unusedStrings | Sort-Object) {
        $report += "  - $unused`n"
    }
} else {
    $report += "All strings are being used! No dead strings found.`n"
}

$report += @"

================================================================================
USED STRINGS BY CATEGORY
================================================================================

"@

# Group used strings by prefix
$categories = @{}
foreach ($key in $usedStrings.Keys) {
    $prefix = $key.Split('_')[0]
    if (-not $categories.ContainsKey($prefix)) {
        $categories[$prefix] = @()
    }
    $categories[$prefix] += $key
}

foreach ($category in $categories.Keys | Sort-Object) {
    $count = $categories[$category].Count
    $report += "`n$category`_* : $count strings`n"
}

$report += @"

================================================================================
DETAILED USAGE REPORT
================================================================================

"@

foreach ($stringId in $usedStrings.Keys | Sort-Object) {
    $locations = $usedStrings[$stringId]
    $report += "`n$stringId`n"
    $report += "  Used in $($locations.Count) file(s):`n"
    foreach ($loc in $locations) {
        $report += "    - $loc`n"
    }
}

# Save report
$report | Out-File -FilePath $outputFile -Encoding UTF8

Write-Status "Report saved to: $outputFile" -ForegroundColor Green
Write-Status ""
Write-Status "SUMMARY:" -ForegroundColor Cyan
Write-Status "  Used:   $($usedStrings.Count) strings" -ForegroundColor Green
Write-Status "  Unused: $($unusedStrings.Count) strings" -ForegroundColor $(if ($unusedStrings.Count -gt 0) { "Yellow" } else { "Green" })
Write-Status "  Usage:  $([math]::Round($usedStrings.Count / $stringIds.Count * 100, 2))%" -ForegroundColor Cyan

