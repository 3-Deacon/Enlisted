$ErrorActionPreference = 'SilentlyContinue'

$raw = [Console]::In.ReadToEnd()
try {
    $d = $raw | ConvertFrom-Json
} catch {
    exit 0
}

$filePath = $d.tool_input.file_path
if (-not $filePath) { exit 0 }

$projectDir = $d.cwd
if (-not $projectDir) { $projectDir = $env:CLAUDE_PROJECT_DIR }
if (-not $projectDir) { exit 0 }

$norm = $filePath -replace '\\', '/'
$root = $projectDir -replace '\\', '/'
if ($norm -notlike "$root/src/*" -or $norm -notlike '*.cs') {
    exit 0
}
if (-not (Test-Path $filePath)) { exit 0 }

$content = Get-Content -LiteralPath $filePath -Raw -ErrorAction SilentlyContinue
if (-not $content) { exit 0 }
if ($content -notmatch 'ModLogger\.Surfaced\s*\(') { exit 0 }

$registry = Join-Path $projectDir 'docs\error-codes.md'
$beforeHash = $null
if (Test-Path $registry) {
    $beforeHash = (Get-FileHash -LiteralPath $registry -Algorithm SHA256).Hash
}

$python = 'C:\Python313\python.exe'
$gen    = Join-Path $projectDir 'Tools\Validation\generate_error_codes.py'
if (-not (Test-Path $python) -or -not (Test-Path $gen)) { exit 0 }

Push-Location $projectDir
try {
    & $python $gen *> $null
} finally {
    Pop-Location
}

$afterHash = $null
if (Test-Path $registry) {
    $afterHash = (Get-FileHash -LiteralPath $registry -Algorithm SHA256).Hash
}

if ($beforeHash -ne $afterHash) {
    Write-Output "[post-edit-cs] docs/error-codes.md regenerated (triggered by $(Split-Path $filePath -Leaf))"
}

exit 0
