. (Join-Path $PSScriptRoot '..\\Write-Status.ps1')

# Extract Dialog Strings and Add to XML
# Extracts all {=string_id}text format strings from dialog code and adds them to enlisted_strings.xml

$ErrorActionPreference = "Stop"
$dialogFile = "C:\Dev\Enlisted\Enlisted\src\Features\Conversations\Behaviors\EnlistedDialogManager.cs"
$xmlFile = "C:\Dev\Enlisted\Enlisted\ModuleData\Languages\enlisted_strings.xml"
$backupFile = "C:\Dev\Enlisted\Enlisted\Debugging\enlisted_strings_backup_before_dialog_fix.xml"

Write-Status "=== Extracting Dialog Strings ===" -ForegroundColor Cyan
Write-Status ""

# Create backup
Copy-Item $xmlFile $backupFile -Force
Write-Status "Backup created: $backupFile" -ForegroundColor Green
Write-Status ""

# Read dialog code
$dialogContent = Get-Content $dialogFile -Raw

# Extract all {=string_id}text patterns
$pattern = '\{=([a-z_]+)\}([^"]+)'
$matches = [regex]::Matches($dialogContent, $pattern)

$dialogStrings = @{}
foreach ($match in $matches) {
    $id = $match.Groups[1].Value
    $text = $match.Groups[2].Value.Trim()
    
    # Clean up text (remove quotes, escape XML entities)
    $text = $text -replace '^["\s]+', '' -replace '["\s]+$', ''
    $text = $text -replace '&', '&amp;'
    $text = $text -replace '<', '&lt;'
    $text = $text -replace '>', '&gt;'
    $text = $text -replace '"', '&quot;'
    
    if ($text -and -not $dialogStrings.ContainsKey($id)) {
        $dialogStrings[$id] = $text
    }
}

Write-Status "Extracted $($dialogStrings.Count) dialog strings" -ForegroundColor Yellow
Write-Status ""

# Read XML
$xmlContent = Get-Content $xmlFile -Raw

# Find missing strings
$missingStrings = @{}
foreach ($id in $dialogStrings.Keys | Sort-Object) {
    if ($xmlContent -notmatch "id=`"$id`"") {
        $missingStrings[$id] = $dialogStrings[$id]
    }
}

Write-Status "Found $($missingStrings.Count) missing strings in XML" -ForegroundColor Red
Write-Status ""

if ($missingStrings.Count -eq 0) {
    Write-Status "No missing strings! XML is complete." -ForegroundColor Green
    exit 0
}

# Create XML entries
$newEntries = @"


    <!-- Dialog Strings - Enlistment Conversations -->
"@

foreach ($id in $missingStrings.Keys | Sort-Object) {
    $text = $missingStrings[$id]
    $newEntries += "`n    <string id=`"$id`" text=`"$text`" />"
}

# Find insertion point (before </strings> closing tag)
$insertionPoint = $xmlContent.LastIndexOf("</strings>")
if ($insertionPoint -eq -1) {
    Write-Status "ERROR: Could not find </strings> tag in XML file!" -ForegroundColor Red
    exit 1
}

# Insert new entries
$updatedXml = $xmlContent.Insert($insertionPoint, $newEntries + "`n`n  ")

# Save updated XML
$updatedXml | Out-File -FilePath $xmlFile -Encoding UTF8 -NoNewline

Write-Status "Added $($missingStrings.Count) dialog strings to XML" -ForegroundColor Green
Write-Status ""
Write-Status "Missing strings added:" -ForegroundColor Cyan
foreach ($id in $missingStrings.Keys | Sort-Object) {
    Write-Status "  [+] $id" -ForegroundColor Gray
}

Write-Status ""
Write-Status "XML file updated successfully!" -ForegroundColor Green
Write-Status "Backup: $backupFile" -ForegroundColor Yellow

