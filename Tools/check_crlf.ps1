param([string]$Path)

. (Join-Path $PSScriptRoot 'Write-Status.ps1')

$bytes = [System.IO.File]::ReadAllBytes($Path)
$hasCRLF = $false
$hasLFOnly = $false
for ($i = 0; $i -lt $bytes.Length - 1; $i++) {
    if ($bytes[$i] -eq 13 -and $bytes[$i + 1] -eq 10) { $hasCRLF = $true }
}
for ($i = 0; $i -lt $bytes.Length; $i++) {
    if ($bytes[$i] -eq 10 -and ($i -eq 0 -or $bytes[$i - 1] -ne 13)) { $hasLFOnly = $true }
}
Write-Status "CRLF: $hasCRLF, LF-only: $hasLFOnly"
