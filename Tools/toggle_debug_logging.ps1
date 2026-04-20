#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Toggle debug logging for specific categories in the Enlisted mod.

.DESCRIPTION
    This script makes it easy to enable/disable Debug level logging for specific
    categories without manually editing the settings JSON file.

.PARAMETER Category
    The log category to modify (e.g., Interface, Battle, Enlistment, etc.)

.PARAMETER Level
    The log level to set (Off, Error, Warn, Info, Debug, Trace)

.PARAMETER ShowCurrent
    Display current log levels for all categories

.EXAMPLE
    .\toggle_debug_logging.ps1 -Category Interface -Level Debug
    Enable Debug logging for Interface category

.EXAMPLE
    .\toggle_debug_logging.ps1 -Category Interface -Level Info
    Reset Interface logging to Info level

.EXAMPLE
    .\toggle_debug_logging.ps1 -ShowCurrent
    Show current log levels
#>

param(
    [string]$Category,
    [ValidateSet("Off", "Error", "Warn", "Info", "Debug", "Trace")]
    [string]$Level,
    [switch]$ShowCurrent
)

. (Join-Path $PSScriptRoot 'Write-Status.ps1')

$settingsPath = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\ModuleData\Enlisted\Config\settings.json"

# Check if settings file exists
if (-not (Test-Path $settingsPath)) {
    Write-Status "Settings file not found at: $settingsPath" -ForegroundColor Red
    Write-Status "The file will be created when you first run the game." -ForegroundColor Yellow
    exit 1
}

# Load settings
try {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
} catch {
    Write-Status "Failed to parse settings file: $_" -ForegroundColor Red
    exit 1
}

# Show current levels if requested
if ($ShowCurrent) {
    Write-Status "`n=== Current Log Levels ===" -ForegroundColor Cyan
    Write-Status "Default: $($settings.LogLevels.Default)" -ForegroundColor Gray
    Write-Status ""
    
    $settings.LogLevels.PSObject.Properties | Where-Object { $_.Name -ne "Default" } | Sort-Object Name | ForEach-Object {
        $color = switch ($_.Value) {
            "Debug" { "Green" }
            "Trace" { "Magenta" }
            "Info" { "White" }
            "Warn" { "Yellow" }
            "Error" { "Red" }
            "Off" { "DarkGray" }
            default { "White" }
        }
        Write-Status "$($_.Name.PadRight(25)): $($_.Value)" -ForegroundColor $color
    }
    Write-Status ""
    exit 0
}

# Validate parameters
if (-not $Category -or -not $Level) {
    Write-Status "Usage: .\toggle_debug_logging.ps1 -Category <CategoryName> -Level <LogLevel>" -ForegroundColor Yellow
    Write-Status "   Or: .\toggle_debug_logging.ps1 -ShowCurrent" -ForegroundColor Yellow
    Write-Status ""
    Write-Status "Common categories:" -ForegroundColor Cyan
    Write-Status "  Interface, Battle, Enlistment, Orders, Content, Equipment" -ForegroundColor Gray
    Write-Status ""
    Write-Status "Log levels: Off, Error, Warn, Info, Debug, Trace" -ForegroundColor Cyan
    exit 1
}

# Check if category exists
$categoryProperty = $settings.LogLevels.PSObject.Properties | Where-Object { $_.Name -eq $Category }
if (-not $categoryProperty) {
    Write-Status "Category '$Category' not found in settings." -ForegroundColor Yellow
    Write-Status "Adding new category..." -ForegroundColor Gray
}

# Update the category
$oldValue = if ($categoryProperty) { $categoryProperty.Value } else { "(new)" }
$settings.LogLevels | Add-Member -MemberType NoteProperty -Name $Category -Value $Level -Force

# Save settings
try {
    $settings | ConvertTo-Json -Depth 10 | Out-File -FilePath $settingsPath -Encoding UTF8
    Write-Status ""
    Write-Status "Updated $Category logging: $oldValue -> $Level" -ForegroundColor Green
    Write-Status ""
    Write-Status "Restart the game for changes to take effect." -ForegroundColor Yellow
    Write-Status ""
} catch {
    $errorMsg = $_.Exception.Message
    Write-Status "Failed to save settings: $errorMsg" -ForegroundColor Red
    exit 1
}
