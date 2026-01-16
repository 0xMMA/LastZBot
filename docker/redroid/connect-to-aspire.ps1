# Script to connect existing redroid container to Aspire's network
# Run this after Aspire has started to connect redroid to the same network as scrcpy-web

Write-Host "Finding Aspire network..." -ForegroundColor Green

# Find Aspire network (usually starts with "aspire-session-network")
$aspireNetwork = podman network ls --format "{{.Name}}" | Select-String "aspire-session-network" | Select-Object -First 1

if (-not $aspireNetwork) {
    Write-Host "ERROR: Could not find Aspire network. Make sure Aspire is running." -ForegroundColor Red
    exit 1
}

Write-Host "Found Aspire network: $aspireNetwork" -ForegroundColor Cyan

# Check if redroid exists
$redroidExists = podman ps -a --filter name=redroid --format "{{.Names}}"
if ($redroidExists -ne "redroid") {
    Write-Host "ERROR: Redroid container not found. Run run-redroid.ps1 first." -ForegroundColor Red
    exit 1
}

# Connect redroid to Aspire network
Write-Host "Connecting redroid to Aspire network..." -ForegroundColor Yellow
podman network connect $aspireNetwork redroid

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully connected redroid to Aspire network!" -ForegroundColor Green
    Write-Host "Scrcpy-web should now be able to connect to redroid." -ForegroundColor Cyan
} else {
    Write-Host "Failed to connect redroid to network. It may already be connected." -ForegroundColor Yellow
}
