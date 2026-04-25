. (Join-Path $PSScriptRoot '..\\Write-Status.ps1')

# Content Localization Verification Script
# Verifies that all content IDs from content-index.md have corresponding XML localization strings

$ErrorActionPreference = "Continue"
$projectRoot = "C:\Dev\Enlisted\Enlisted"
$xmlFile = "$projectRoot\ModuleData\Languages\enlisted_strings.xml"
$outputFile = "$projectRoot\Debugging\localization_verification_report.txt"

Write-Status "Content Localization Verification" -ForegroundColor Cyan
Write-Status "==================================`n" -ForegroundColor Cyan

# Define all content IDs that need localization based on content-index.md
$orderIds = @(
    "order_guard_duty", "order_camp_patrol", "order_firewood", "order_equipment_check",
    "order_muster", "order_sentry", "order_scout_route", "order_treat_wounded",
    "order_repair_equipment", "order_forage", "order_lead_patrol", "order_inspect_defenses",
    "order_command_squad", "order_strategic_planning", "order_coordinate_supply",
    "order_interrogate", "order_inspect_readiness"
)

$decisionIds = @(
    "dec_rest", "dec_rest_extended", "dec_seek_treatment",
    "dec_weapon_drill", "dec_spar", "dec_endurance", "dec_study_tactics",
    "dec_practice_medicine", "dec_train_troops", "dec_combat_drill",
    "dec_weapon_specialization", "dec_lead_drill",
    "dec_join_men", "dec_join_drinking", "dec_seek_officers", "dec_keep_to_self",
    "dec_write_letter", "dec_confront_rival",
    "dec_gamble_low", "dec_gamble_high", "dec_side_work", "dec_shady_deal", "dec_visit_market",
    "dec_request_leave", "dec_request_audience", "dec_seek_promotion",
    "dec_gather_intel", "dec_ask_around",
    "dec_inspect_equipment", "dec_organize_kit",
    "dec_contraband_run", "dec_cover_deserter", "dec_report_theft"
)

$mapIncidentIds = @(
    "mi_battle_victory_decisive", "mi_battle_victory_close", "mi_battle_defeat_close",
    "mi_battle_defeat_rout", "mi_battle_friendly_fire", "mi_battle_heroic_stand",
    "mi_battle_cowardice_witnessed", "mi_battle_looting_opportunity",
    "mi_siege_assault_success", "mi_siege_assault_failure", "mi_siege_sortie",
    "mi_siege_disease_outbreak", "mi_siege_supply_crisis", "mi_siege_desertion_wave",
    "mi_siege_bombardment_injury", "mi_siege_mining_collapse",
    "mi_town_market_opportunity", "mi_town_recruitment_offer", "mi_town_old_comrade",
    "mi_town_suspicious_activity", "mi_town_tavern_brawl", "mi_town_noble_encounter",
    "mi_town_black_market", "mi_town_gambling_den",
    "mi_village_requisition_harsh", "mi_village_requisition_fair", "mi_village_local_conflict",
    "mi_village_refugee_family", "mi_village_deserter_hiding", "mi_village_bandit_threat",
    "mi_village_harvest_festival", "mi_village_shrine_blessing",
    "mi_leave_settlement_ambush", "mi_leave_settlement_patrol_encounter", "mi_leave_settlement_merchant_caravan",
    "mi_leave_settlement_deserter_temptation", "mi_leave_settlement_refugee_plea",
    "mi_wait_settlement_inspection", "mi_wait_settlement_pay_dispute", "mi_wait_settlement_equipment_issue",
    "mi_wait_settlement_rumor_mill", "mi_wait_settlement_officer_summons", "mi_wait_settlement_local_trouble",
    "mi_wait_settlement_rest_interrupted", "mi_wait_settlement_training_opportunity"
)

# Read XML content
Write-Status "Reading XML file..." -ForegroundColor Yellow
$xmlContent = Get-Content $xmlFile -Raw

# Check each content type
$report = @"
================================================================================
CONTENT LOCALIZATION VERIFICATION REPORT
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
================================================================================

"@

$missingStrings = @()
$foundStrings = @()

function Test-LocalizationExists {
    param($contentId, $category)
    
    # Check for various string patterns that might be used
    $patterns = @(
        "id=`"$contentId`"",
        "id=`"${contentId}_title`"",
        "id=`"${contentId}_body`"",
        "id=`"${contentId}_setup`"",
        "id=`"${contentId}_desc`"",
        "id=`"${contentId}_name`""
    )
    
    $found = $false
    foreach ($pattern in $patterns) {
        if ($xmlContent -match [regex]::Escape($pattern)) {
            $found = $true
            break
        }
    }
    
    if ($found) {
        $script:foundStrings += "$category : $contentId"
    } else {
        $script:missingStrings += "$category : $contentId"
    }
    
    return $found
}

# Check Orders
Write-Status "Checking Orders ($($orderIds.Count) total)..." -ForegroundColor Yellow
$ordersMissing = 0
foreach ($id in $orderIds) {
    if (-not (Test-LocalizationExists $id "Order")) {
        $ordersMissing++
    }
}

# Check Decisions
Write-Status "Checking Decisions ($($decisionIds.Count) total)..." -ForegroundColor Yellow
$decisionsMissing = 0
foreach ($id in $decisionIds) {
    if (-not (Test-LocalizationExists $id "Decision")) {
        $decisionsMissing++
    }
}

# Check Map Incidents
Write-Status "Checking Map Incidents ($($mapIncidentIds.Count) total)..." -ForegroundColor Yellow
$incidentsMissing = 0
foreach ($id in $mapIncidentIds) {
    if (-not (Test-LocalizationExists $id "MapIncident")) {
        $incidentsMissing++
    }
}

$report += @"

SUMMARY
-------
Orders:        $($orderIds.Count - $ordersMissing) / $($orderIds.Count) localized ($ordersMissing missing)
Decisions:     $($decisionIds.Count - $decisionsMissing) / $($decisionIds.Count) localized ($decisionsMissing missing)
Map Incidents: $($mapIncidentIds.Count - $incidentsMissing) / $($mapIncidentIds.Count) localized ($incidentsMissing missing)

Total Missing: $($missingStrings.Count)
Total Found:   $($foundStrings.Count)

"@

if ($missingStrings.Count -gt 0) {
    $report += @"
================================================================================
MISSING LOCALIZATION STRINGS
================================================================================

The following content IDs do not have localization strings in enlisted_strings.xml:

"@
    foreach ($missing in $missingStrings | Sort-Object) {
        $report += "  - $missing`n"
    }
} else {
    $report += "✅ All content IDs have localization strings!`n"
}

$report += @"

================================================================================
FOUND LOCALIZATION STRINGS
================================================================================

"@
foreach ($found in $foundStrings | Sort-Object) {
    $report += "  [OK] $found`n"
}

# Save report
$report | Out-File -FilePath $outputFile -Encoding UTF8

Write-Status "`nReport saved to: $outputFile" -ForegroundColor Green
Write-Status "`nSUMMARY:" -ForegroundColor Cyan
Write-Status "  Orders:        $($orderIds.Count - $ordersMissing) / $($orderIds.Count)" -ForegroundColor $(if ($ordersMissing -eq 0) { "Green" } else { "Yellow" })
Write-Status "  Decisions:     $($decisionIds.Count - $decisionsMissing) / $($decisionIds.Count)" -ForegroundColor $(if ($decisionsMissing -eq 0) { "Green" } else { "Yellow" })
Write-Status "  Map Incidents: $($mapIncidentIds.Count - $incidentsMissing) / $($mapIncidentIds.Count)" -ForegroundColor $(if ($incidentsMissing -eq 0) { "Green" } else { "Yellow" })
Write-Status "  Missing:       $($missingStrings.Count)" -ForegroundColor $(if ($missingStrings.Count -eq 0) { "Green" } else { "Red" })

