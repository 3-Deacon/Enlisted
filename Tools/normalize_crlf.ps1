param([string]$Path)

. (Join-Path $PSScriptRoot 'Write-Status.ps1')

$content = [System.IO.File]::ReadAllBytes($Path)
$text = [System.Text.Encoding]::UTF8.GetString($content)
# Normalize to CRLF: first collapse any existing CRLF to LF, then expand LF to CRLF
$text = $text.Replace("`r`n", "`n").Replace("`n", "`r`n")
[System.IO.File]::WriteAllText($Path, $text, [System.Text.Encoding]::UTF8)
Write-Status "Normalized: $Path"
