# set-env.ps1 - Set Azure OpenAI environment variables for the workshop

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " Azure Agent Framework Workshop - Set Env" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

$requiredVars = @(
    "AZURE_OPENAI_ENDPOINT",
    "AZURE_OPENAI_API_KEY",
    "AZURE_OPENAI_DEPLOYMENT"
)

$optionalVars = @(
    "AZURE_OPENAI_API_VERSION"
)

$missing = @()

foreach ($var in $requiredVars) {
    if (-not [Environment]::GetEnvironmentVariable($var)) {
        $missing += $var
    }
}

if ($missing.Count -eq 0) {
    Write-Host "[OK] All required environment variables are set." -ForegroundColor Green
    Write-Host ""
    Write-Host "Current values:"
    Write-Host "  AZURE_OPENAI_ENDPOINT    = $env:AZURE_OPENAI_ENDPOINT"
    Write-Host '  AZURE_OPENAI_API_KEY     = (set, hidden)'
    Write-Host "  AZURE_OPENAI_DEPLOYMENT  = $env:AZURE_OPENAI_DEPLOYMENT"
    if ($env:AZURE_OPENAI_API_VERSION) {
        Write-Host "  AZURE_OPENAI_API_VERSION = $env:AZURE_OPENAI_API_VERSION"
    }
    exit 0
}

Write-Host "[ERROR] Missing required environment variables:" -ForegroundColor Red
foreach ($var in $missing) {
    Write-Host "   - $var" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Please set them before running any module:"
Write-Host ""
Write-Host "  `$env:AZURE_OPENAI_ENDPOINT   = 'https://<your-resource>.openai.azure.com/'"
Write-Host "  `$env:AZURE_OPENAI_API_KEY    = '<your-api-key>'"
Write-Host "  `$env:AZURE_OPENAI_DEPLOYMENT = '<your-deployment-name>'"
Write-Host "  `$env:AZURE_OPENAI_API_VERSION = '2025-01-01-preview'  # optional"
Write-Host ""
Write-Host "Tip: Add these to your PowerShell profile (`$PROFILE) for persistence." -ForegroundColor Cyan
exit 1
