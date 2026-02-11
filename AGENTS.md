# LastZBot - Agent Guide

This document helps AI agents and developers understand the project, fix common issues, and run the stack correctly.

## Architecture Overview

- **AppHost (Aspire)**: Orchestrates all services. Run with `dotnet run --project LastZBot.AppHost`
- **botservice**: Connects to Redroid via ADB from the **host** (run as a process, not in a container). Captures screenshots via framebuffer, exposes REST API and SignalR for live frames. Coordinates automation tasks.
- **webfrontend**: Blazor app. Uses Service Discovery to reach botservice. Shows connection status and Android view.
- **redroid**: Android-in-Docker (Redroid). Exposes ADB on port 5555. **Requires binder on host.**

## ADB Connection Flow

1. BotService runs on the host and needs to connect to Redroid at `127.0.0.1:5555`.
2. Aspire 9.5+ passes container endpoint Host as the container name ("redroid"), which host processes cannot resolve. We override `Adb__Host` to `127.0.0.1` in AppHost.
3. ADB server (port 5037) runs on the host. BotService starts it if needed.
4. `adb connect 127.0.0.1:5555` connects to the DCP proxy (dcpctrl), which forwards to the Redroid container.

## Critical Prerequisites (Linux)

### 1. ADB

```bash
sudo apt install adb
```

### 2. Binder (Required for Redroid)

Redroid crashes with "Binder driver could not be opened" if binder is not set up.

**Install kernel modules:**

```bash
sudo apt install linux-modules-extra-$(uname -r)
```

**Load and mount (run after each boot unless made permanent):**

```bash
sudo modprobe binder_linux devices="binder,hwbinder,vndbinder"
sudo mkdir -p /dev/binderfs
sudo mount -t binder binder /dev/binderfs
sudo ln -sf /dev/binderfs/{binder,hwbinder,vndbinder} /dev/
```

**Verify:**

```bash
ls -la /dev/binder /dev/hwbinder /dev/vndbinder   # Should NOT be broken symlinks
ls /dev/binderfs/                                 # Should show binder, hwbinder, vndbinder
```

**Make permanent** (see README.md): Add to `/etc/fstab` and `/etc/tmpfiles.d/binder.conf`.

### 3. Docker

Redroid runs in Docker. Ensure Docker is installed and the user can run containers.

## Common Issues

### ADB shows "offline" or "Not connected"

- **Redroid container exited**: Check `docker ps -a`. If Redroid exited, check binder setup (see above). View logs: `docker logs <redroid-container-id>`
- **Wrong host**: BotService must use `127.0.0.1`, not `redroid`. Fixed in AppHost.
- **Boot timing**: Redroid takes 30–60+ seconds to boot. BotService waits 30s and retries up to 15 times.

### Redroid exits with code 129

- **Binder not available**: dmesg shows "Binder driver could not be opened". Re-run binder setup.
- **Broken symlinks**: `ls -la /dev/binder` shows "broken symbolic link". Remount binderfs (see above).

### Port 5555 already in use

Aspire's DCP proxy binds 5555 when Redroid is part of the stack. Stop Aspire first before running Redroid manually.

### SignalR / Live stream not working

- BotService must be reachable from the browser. Service Discovery provides the URL.
- Home.razor resolves `botservice` URL from config and normalizes `https+http://` schemes.
- FrameBroadcaster only sends frames when `_adbService.IsConnected` is true.

## Configuration

| Variable       | Where      | Purpose                             |
|----------------|------------|-------------------------------------|
| Adb__Host      | AppHost    | Overridden to 127.0.0.1 for host use |
| Adb__Port      | AppHost    | 5555 (Redroid ADB port)              |
| services:botservice:http:0 | Aspire env | BotService URL for Web |

## Running the Stack

```bash
# From repo root - use local profile (HTTP, typical for local dev)
dotnet run --project LastZBot.AppHost --launch-profile local
```

Then open the Aspire dashboard (URL printed at startup) and the webfrontend from there. Refresh the Blazor page after all services are up; connection status polls `/api/status` from BotService.

## Key Files

- `LastZBot.AppHost/AppHost.cs` – Redroid and BotService config
- `LastZBot.BotService/Device/AdbService.cs` – ADB connect, screenshot, tap
- `LastZBot.BotService/Device/AdbConnectionWorker.cs` – Boot wait and connection retries
- `LastZBot.BotService/Device/FrameBroadcaster.cs` – SignalR frame broadcast
- `LastZBot.Web/Components/Pages/Home.razor` – Dashboard, status, live view
