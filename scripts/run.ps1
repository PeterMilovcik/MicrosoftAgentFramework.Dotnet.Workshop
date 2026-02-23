# run.ps1 – Run a workshop module by number or name
#
# Usage:
#   ./scripts/run.ps1 00              # runs 00_ConnectivityCheck
#   ./scripts/run.ps1 02              # runs 02_Tools_FunctionCalling
#   ./scripts/run.ps1 00_ConnectivityCheck  # also works

param(
    [Parameter(Position = 0)]
    [string]$Module = "",

    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ExtraArgs = @()
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$ModulesDir = Join-Path $RepoRoot "modules"

if (-not $Module) {
    Write-Host "Usage: ./scripts/run.ps1 <module-number-or-name>" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Available modules:"
    Get-ChildItem -Directory $ModulesDir | ForEach-Object { Write-Host "  $($_.Name)" }
    exit 1
}

# Find matching module directory
$Match = $null
Get-ChildItem -Directory $ModulesDir | ForEach-Object {
    $name = $_.Name
    $prefix = ($name -split "_")[0]
    if ($name -eq $Module -or $prefix -eq $Module) {
        $Match = $_
    }
}

if (-not $Match) {
    Write-Host "❌ No module found matching: $Module" -ForegroundColor Red
    Write-Host ""
    Write-Host "Available modules:"
    Get-ChildItem -Directory $ModulesDir | ForEach-Object { Write-Host "  $($_.Name)" }
    exit 1
}

$ModName = $Match.Name
$CsProjPath = Join-Path $Match.FullName "$ModName.csproj"

Write-Host "🚀 Running module: $ModName" -ForegroundColor Cyan
Write-Host "   Project: $CsProjPath"
Write-Host "-------------------------------------------"

Push-Location $RepoRoot
try {
    dotnet run --project $CsProjPath @ExtraArgs
} finally {
    Pop-Location
}
