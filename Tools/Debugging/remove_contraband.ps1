. (Join-Path $PSScriptRoot '..\\Write-Status.ps1')

# PowerShell script to remove contraband system references
$file = "src\Features\Enlistment\Behaviors\MusterMenuHandler.cs"
$content = Get-Content $file -Raw

# Step 1: Remove using statement
$content = $content -replace "using Enlisted\.Features\.Logistics;`r?`n", ""

# Step 2: Update comment from 8 to 7 stages  
$content = $content -replace "// Menu IDs for the 8 muster stages", "// Menu IDs for the 7 muster stages"

# Step 3: Remove MusterBaggageMenuId constant
$content = $content -replace "`r?`n\s+private const string MusterBaggageMenuId = `"enlisted_muster_baggage`";", ""

# Step 4: Remove baggage-related fields from MusterSessionState
$content = $content -replace "`r?`n\s+/// <summary>Item StringId from Quartermaster's Deal \(for contraband exemption\)\.</summary>`r?`n\s+public string QMDealItemId { get; set; }", ""
$content = $content -replace "`r?`n\s+// Event stage outcomes`r?`n\s+/// <summary>Baggage check outcome: `"passed`", `"confiscated`", `"bribed`", `"skipped`"`.</summary>`r?`n\s+public string BaggageOutcome { get; set; }", ""
$content = $content -replace "`r?`n\s+// Contraband \(needed for baggage stage display\)`r?`n\s+/// <summary>Contraband found during baggage check\.</summary>`r?`n\s+public bool ContrabandFound { get; set; }`r?`n`r?`n\s+/// <summary>Current quartermaster reputation\.</summary>`r?`n\s+public int QMRep { get; set; }", ""

# Step 5: Remove baggage stage from IsKeyStage check
$content = $content -replace "\s+stageId == MusterBaggageMenuId \|\|`r?`n", ""

# Write back
Set-Content $file $content -NoNewline

Write-Status "Phase 1 complete: Removed basic contraband references"
