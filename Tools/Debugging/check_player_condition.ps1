. (Join-Path $PSScriptRoot '..\\Write-Status.ps1')

# Check Player Condition State
# This script helps diagnose why Urgent Medical Care decision is appearing
# when no sickness/injury is visible in Player Status

Write-Status "=== Enlisted Player Condition Diagnostic ===" -ForegroundColor Cyan
Write-Status ""

$saveDir = "$env:USERPROFILE\Documents\Mount and Blade II Bannerlord\Game Saves"
Write-Status "Checking save directory: $saveDir" -ForegroundColor Yellow

if (Test-Path $saveDir) {
    $saves = Get-ChildItem $saveDir -Filter "*.sav" | Sort-Object LastWriteTime -Descending | Select-Object -First 5
    
    Write-Status "Recent save files found:" -ForegroundColor Green
    foreach ($save in $saves) {
        Write-Status "  - $($save.Name) ($(Get-Date $save.LastWriteTime -Format 'yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
    }
    
    Write-Status ""
    Write-Status "To check your player condition state:" -ForegroundColor Cyan
    Write-Status "1. Look in your most recent save file for these save data keys:" -ForegroundColor White
    Write-Status "   - pc_injSeverity (0=None, 1=Minor, 2=Moderate, 3=Severe, 4=Critical)" -ForegroundColor Gray
    Write-Status "   - pc_injType (injury type name)" -ForegroundColor Gray
    Write-Status "   - pc_injDays (days remaining)" -ForegroundColor Gray
    Write-Status "   - pc_illSeverity (0=None, 1=Mild, 2=Moderate, 3=Severe, 4=Critical)" -ForegroundColor Gray
    Write-Status "   - pc_illType (illness type name)" -ForegroundColor Gray
    Write-Status "   - pc_illDays (days remaining)" -ForegroundColor Gray
    Write-Status ""
    Write-Status "2. If pc_injSeverity >= 3 or pc_illSeverity >= 3, that's triggering Urgent Medical Care" -ForegroundColor Yellow
    Write-Status ""
    Write-Status "Common causes:" -ForegroundColor Cyan
    Write-Status "  - Save data corruption (severity set but days = 0)" -ForegroundColor White
    Write-Status "  - Combat injury that wasn't properly displayed" -ForegroundColor White
    Write-Status "  - Event that applied condition but status UI didn't update" -ForegroundColor White
    Write-Status ""
} else {
    Write-Status "Save directory not found at: $saveDir" -ForegroundColor Red
}

Write-Status "=== Quick Fix ===" -ForegroundColor Cyan
Write-Status "If you want to clear any hidden conditions:" -ForegroundColor White
Write-Status "1. Take the 'Get to the surgeon. Now.' option (costs 200 gold)" -ForegroundColor Yellow
Write-Status "   This will clear the severe condition and prevent the decision from reappearing" -ForegroundColor Gray
Write-Status ""
Write-Status "2. Or wait for natural recovery (if days remaining > 0)" -ForegroundColor Yellow
Write-Status ""
Write-Status "=== Root Cause Investigation ===" -ForegroundColor Cyan
Write-Status "The bug is likely one of these:" -ForegroundColor White
Write-Status "A. Status display not checking PlayerConditionBehavior.State" -ForegroundColor Gray
Write-Status "B. Condition was applied but message was missed/not displayed" -ForegroundColor Gray
Write-Status "C. Save data has stale condition from previous session" -ForegroundColor Gray
Write-Status ""
