# Script to run redroid container manually with privileged mode
# This is required because Aspire doesn't support privileged mode directly
# 
# USAGE:
#   1. Start Aspire first (to create the network)
#   2. Run this script: .\docker\redroid\run-redroid.ps1
#   3. The script will automatically connect redroid to Aspire's network

param(
    [switch]$AutoConnectToAspire = $true
)

Write-Host "Starting redroid container with privileged mode..." -ForegroundColor Green

$dataPath = Join-Path $PSScriptRoot "data"
if (-not (Test-Path $dataPath)) {
    New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
}

# Check if redroid already exists
$existing = podman ps -a --filter name=redroid --format "{{.Names}}"
if ($existing -eq "redroid") {
    Write-Host "Removing existing redroid container..." -ForegroundColor Yellow
    podman rm -f redroid
}

podman run -d `
    --name redroid `
    --privileged `
    --restart unless-stopped `
    -p 5555:5555 `
    -v "${dataPath}:/data" `
    -e ANDROID_ADB_SERVER_PORT=5555 `
    -e "ro.setupwizard.mode=DISABLED" `
    redroid/redroid:14.0.0_mindthegapps `
    androidboot.redroid_gpu_mode=guest

if ($LASTEXITCODE -eq 0) {
    Write-Host "Redroid container started successfully!" -ForegroundColor Green
    Write-Host "Container name: redroid" -ForegroundColor Cyan
    Write-Host "ADB port: localhost:5555" -ForegroundColor Cyan
    
    if ($AutoConnectToAspire) {
        Write-Host ""
        Write-Host "Attempting to connect to Aspire network..." -ForegroundColor Yellow
        Start-Sleep -Seconds 3  # Give Aspire time to create network if just started
        
        $aspireNetwork = podman network ls --format "{{.Name}}" | Select-String "aspire-session-network" | Select-Object -First 1
        if ($aspireNetwork) {
            Write-Host "Found Aspire network: $aspireNetwork" -ForegroundColor Cyan
            $connectOutput = podman network connect $aspireNetwork redroid 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Successfully connected redroid to Aspire network!" -ForegroundColor Green
                Write-Host "Scrcpy-web should now be able to connect using container name 'redroid'" -ForegroundColor Cyan
            } else {
                if ($connectOutput -match "already exists" -or $connectOutput -match "already connected") {
                    Write-Host "Redroid is already connected to Aspire network" -ForegroundColor Green
                } else {
                    Write-Host "Note: Connection may have failed. Error: $connectOutput" -ForegroundColor Yellow
                    Write-Host "Run .\docker\redroid\connect-to-aspire.ps1 manually if needed." -ForegroundColor Yellow
                }
            }
        } else {
            Write-Host "Aspire network not found yet." -ForegroundColor Yellow
            Write-Host "Make sure Aspire is running, then run:" -ForegroundColor Yellow
            Write-Host "  .\docker\redroid\connect-to-aspire.ps1" -ForegroundColor Cyan
        }
    }
    
    Write-Host ""
    Write-Host "To stop the container, run: podman stop redroid" -ForegroundColor Yellow
    Write-Host "To remove the container, run: podman rm redroid" -ForegroundColor Yellow
} else {
    Write-Host "Failed to start redroid container. Check Podman logs." -ForegroundColor Red
    exit 1
}
