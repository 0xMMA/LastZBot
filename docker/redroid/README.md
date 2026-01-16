# Redroid Setup for Last Z Bot

## Quick Start

1. **Start Aspire first** - This creates the network that redroid needs to join
2. **Run redroid** - Use the script to start redroid and connect it to Aspire's network:
   ```powershell
   .\docker\redroid\run-redroid.ps1
   .\docker\redroid\connect-to-aspire.ps1
   ```
3. **Verify connection** - Test that scrcpy-web can reach redroid:
   ```powershell
   .\docker\redroid\test-connection.ps1
   ```

## Troubleshooting

### "Device Tracker" shows in live view

This means scrcpy-web can't connect to redroid. Try:

1. **Check if redroid is running:**
   ```powershell
   podman ps | Select-String redroid
   ```

2. **Get redroid's IP and update AppHost.cs:**
   ```powershell
   podman inspect redroid --format '{{.NetworkSettings.IPAddress}}'
   ```
   Then update `DEVICE_HOST` in `AppHost.cs` with the IP address.

3. **Connect redroid to Aspire network:**
   ```powershell
   .\docker\redroid\connect-to-aspire.ps1
   ```
   Then use `DEVICE_HOST="redroid"` (container name) in `AppHost.cs`.

4. **Restart Aspire** after changing `DEVICE_HOST` so scrcpy-web picks up the new configuration.

### Connection Refused

If ADB connection is refused:
- Redroid's ADB might only be listening on localhost
- Try restarting redroid: `podman restart redroid`
- Check redroid logs: `podman logs redroid`

### Network Issues

If containers are on different networks:
- Redroid should be on both `podman` network (default) and Aspire's network
- Use `podman network ls` to see networks
- Use `podman inspect redroid` to see which networks it's on
