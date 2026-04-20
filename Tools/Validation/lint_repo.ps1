[CmdletBinding()]
param(
    [switch]$SkipCSharp,
    [switch]$SkipContent,
    [switch]$SkipPython,
    [switch]$SkipPowerShell
)

. (Join-Path $PSScriptRoot '..\\Write-Status.ps1')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Output ""
    Write-Output "==> $Name"
    & $Action
}

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter()]
        [string[]]$Arguments = @()
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath exited with code $LASTEXITCODE."
    }
}

$workspaceRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Push-Location $workspaceRoot
try {
    if (-not $SkipCSharp) {
        Invoke-Step -Name 'C# whitespace formatting' -Action {
            Invoke-Native -FilePath 'dotnet' -Arguments @(
                'format',
                'Enlisted.sln',
                'whitespace',
                '--verify-no-changes',
                '--no-restore',
                '--verbosity',
                'minimal'
            )
        }

        Invoke-Step -Name 'C# code style' -Action {
            Invoke-Native -FilePath 'dotnet' -Arguments @(
                'format',
                'Enlisted.sln',
                'style',
                '--verify-no-changes',
                '--severity',
                'warn',
                '--no-restore',
                '--verbosity',
                'minimal'
            )
        }
    }

    if (-not $SkipContent) {
        Invoke-Step -Name 'Content validation' -Action {
            Invoke-Native -FilePath 'python' -Arguments @(
                'Tools/Validation/validate_content.py'
            )
        }
    }

    if (-not $SkipPython) {
        Invoke-Step -Name 'Ruff lint' -Action {
            Invoke-Native -FilePath 'python' -Arguments @(
                '-m',
                'ruff',
                'check',
                'Tools',
                '--config',
                'ruff.toml'
            )
        }

        Invoke-Step -Name 'Ruff format check' -Action {
            Invoke-Native -FilePath 'python' -Arguments @(
                '-m',
                'ruff',
                'format',
                'Tools',
                '--check',
                '--config',
                'ruff.toml'
            )
        }
    }

    if (-not $SkipPowerShell) {
        Import-Module PSScriptAnalyzer -ErrorAction Stop

        Invoke-Step -Name 'PSScriptAnalyzer' -Action {
            $results = @()
            $psFiles = Get-ChildItem -Path 'Tools' -Recurse -Filter '*.ps1' | Sort-Object FullName
            foreach ($psFile in $psFiles) {
                $fileResults = Invoke-ScriptAnalyzer -Path $psFile.FullName -Settings 'PSScriptAnalyzerSettings.psd1'
                if ($fileResults) {
                    $results += $fileResults
                }
            }

            if ($results) {
                $results |
                    Select-Object RuleName, Severity, ScriptName, Line, Message |
                    Format-Table -AutoSize |
                    Out-String |
                    Write-Output

                throw "PSScriptAnalyzer found $($results.Count) issue(s)."
            }
        }
    }
} finally {
    Pop-Location
}
