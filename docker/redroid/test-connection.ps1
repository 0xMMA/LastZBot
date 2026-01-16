# Test script to verify scrcpy-web can connect to redroid
Write-Host "Testing redroid connectivity..." -ForegroundColor Green

# Get redroid IP
$redroidIP = podman inspect redroid --format '{{.NetworkSettings.IPAddress}}'
Write-Host "Redroid IP: $redroidIP" -ForegroundColor Cyan

# Get Aspire network name
$aspireNetwork = podman network ls --format "{{.Name}}" | Select-String "aspire-session-network" | Select-Object -First 1

if ($aspireNetwork) {
    Write-Host "Aspire network: $aspireNetwork" -ForegroundColor Cyan
    
    # Try to connect redroid to Aspire network
    Write-Host "Attempting to connect redroid to Aspire network..." -ForegroundColor Yellow
    podman network connect $aspireNetwork redroid 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully connected!" -ForegroundColor Green
    } else {
        Write-Host "May already be connected or failed" -ForegroundColor Yellow
    }
    
    # Check if scrcpy-web can reach redroid
    Write-Host "`nTesting connectivity from scrcpy-web to redroid..." -ForegroundColor Yellow
    $scrcpyExists = podman ps -a --filter name=scrcpy-web --format "{{.Names}}"
    
    if ($scrcpyExists -eq "scrcpy-web") {
        Write-Host "Testing if scrcpy-web can reach redroid via container name..." -ForegroundColor Cyan
        podman exec scrcpy-web sh -c "nc -zv redroid 5555 2>&1 || echo 'Connection failed'"
        
        Write-Host "`nTesting if scrcpy-web can reach redroid via IP..." -ForegroundColor Cyan
        podman exec scrcpy-web sh -c "nc -zv $redroidIP 5555 2>&1 || echo 'Connection failed'"
    } else {
        Write-Host "Scrcpy-web container not found. Start Aspire first." -ForegroundColor Yellow
    }
} else {
    Write-Host "Aspire network not found. Start Aspire first." -ForegroundColor Yellow
    Write-Host "For now, scrcpy-web should use DEVICE_HOST=$redroidIP" -ForegroundColor Cyan
}

Write-Host "`nCurrent redroid status:" -ForegroundColor Green
podman ps --filter name=redroid --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
