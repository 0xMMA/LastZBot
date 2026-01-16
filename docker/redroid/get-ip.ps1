# Get redroid's IP address on Aspire network
Write-Host "Getting redroid IP addresses..." -ForegroundColor Green

# Check if redroid exists
$redroidExists = podman ps -a --filter name=redroid --format "{{.Names}}"
if ($redroidExists -ne "redroid") {
    Write-Host "ERROR: Redroid container not found. Run run-redroid.ps1 first." -ForegroundColor Red
    exit 1
}

# Get network information using JSON parsing
$inspectJson = podman inspect redroid --format '{{json .NetworkSettings.Networks}}' | ConvertFrom-Json

if ($inspectJson) {
    Write-Host "`nRedroid is on the following networks:" -ForegroundColor Cyan
    $aspireIP = $null
    $podmanIP = $null
    
    $inspectJson.PSObject.Properties | ForEach-Object {
        $netName = $_.Name
        $ip = $_.Value.IPAddress
        
        if ($netName -match "aspire") {
            Write-Host "  $netName : $ip (Aspire network - USE THIS)" -ForegroundColor Green
            $aspireIP = $ip
        } else {
            Write-Host "  $netName : $ip" -ForegroundColor Yellow
            if ($netName -eq "podman") {
                $podmanIP = $ip
            }
        }
    }
    
    if ($aspireIP) {
        Write-Host ""
        Write-Host "Aspire network IP: $aspireIP" -ForegroundColor Green
        Write-Host "Update AppHost.cs line 25: DEVICE_HOST = `"$aspireIP`"" -ForegroundColor Cyan
        return $aspireIP
    } else {
        Write-Host ""
        Write-Host "Could not find Aspire network IP." -ForegroundColor Yellow
        if ($podmanIP) {
            Write-Host "Redroid is only on podman network ($podmanIP)." -ForegroundColor Yellow
            Write-Host "Run: .\docker\redroid\connect-to-aspire.ps1" -ForegroundColor Cyan
            Write-Host "Then run this script again to get the Aspire network IP." -ForegroundColor Cyan
        } else {
            Write-Host "Redroid may not be connected to any network." -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "ERROR: Could not get network information from redroid container." -ForegroundColor Red
}
