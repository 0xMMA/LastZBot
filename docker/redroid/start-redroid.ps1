# Complete script to start redroid and connect to Aspire network
# 
# USAGE:
#   Option 1: Start Aspire first, then run this script
#   Option 2: Run this script, then start Aspire, then run: .\docker\redroid\connect-to-aspire.ps1

Write-Host "Starting redroid for Last Z Bot..." -ForegroundColor Green
Write-Host ""

# Step 1: Check if Aspire is running
$aspireNetwork = podman network ls --format "{{.Name}}" | Select-String "aspire-session-network" | Select-Object -First 1

if (-not $aspireNetwork) {
    Write-Host "WARNING: Aspire network not found." -ForegroundColor Yellow
    Write-Host "You can either:" -ForegroundColor Cyan
    Write-Host "  A) Start Aspire first, then run this script again" -ForegroundColor White
    Write-Host "  B) Continue now, then after starting Aspire run: .\docker\redroid\connect-to-aspire.ps1" -ForegroundColor White
    Write-Host ""
    $continue = Read-Host "Continue anyway? (Y/N)"
    if ($continue -ne "Y" -and $continue -ne "y") {
        Write-Host "Exiting. Start Aspire first, then run this script." -ForegroundColor Yellow
        exit 0
    }
    Write-Host ""
}

# Step 2: Start redroid
Write-Host "Step 1: Starting redroid container..." -ForegroundColor Cyan
& "$PSScriptRoot\run-redroid.ps1" -AutoConnectToAspire:$true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to start redroid. Exiting." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2: Getting redroid IP address..." -ForegroundColor Cyan
Start-Sleep -Seconds 2

# Try to connect to Aspire network again if it exists now
if (-not $aspireNetwork) {
    $aspireNetwork = podman network ls --format "{{.Name}}" | Select-String "aspire-session-network" | Select-Object -First 1
    if ($aspireNetwork) {
        Write-Host "Aspire network found! Connecting redroid..." -ForegroundColor Green
        & "$PSScriptRoot\connect-to-aspire.ps1"
    }
}

& "$PSScriptRoot\get-ip.ps1"

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "Next steps:" -ForegroundColor Green
Write-Host "1. Make sure Aspire is running" -ForegroundColor White
Write-Host "2. If redroid is on Aspire network (shown above), scrcpy-web should connect automatically" -ForegroundColor White
Write-Host "3. If redroid is NOT on Aspire network, run: .\docker\redroid\connect-to-aspire.ps1" -ForegroundColor White
Write-Host "4. If connection still fails, update DEVICE_HOST in AppHost.cs with the IP shown above" -ForegroundColor White
Write-Host "5. Restart Aspire to apply any changes" -ForegroundColor White
Write-Host "=========================================================" -ForegroundColor Cyan
