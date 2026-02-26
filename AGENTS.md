# LastZBot - Agent Guide

This document helps AI agents and developers understand the project, fix common issues, and run the stack correctly.

## Architecture Overview

- **AppHost (Aspire)**: Orchestrates all services. Run with `dotnet run --project LastZBot.AppHost --launch-profile local`
- **BotService**: Connects to Redroid via ADB from the **host** (not containerised). Captures screenshots via framebuffer, exposes REST API and SignalR for live frames. Coordinates automation tasks.
- **WebFrontend**: Blazor app. Uses Service Discovery to reach BotService. Shows connection status and Android live view.
- **Redroid**: Android 14 in Docker (`redroid/redroid:14.0.0_mindthegapps`). Exposes ADB on port 5555. **Requires binder on host.**

## Redroid Image

The project uses a **locally-built** image `redroid/redroid:14.0.0_mindthegapps` that is **not on Docker Hub**.

Built with [`ayasa520/redroid-script`](https://github.com/ayasa520/redroid-script):
```bash
cd /tmp && git clone https://github.com/ayasa520/redroid-script.git && cd redroid-script
python3 -m pip install tqdm requests --break-system-packages
python3 redroid.py -a 14.0.0 -mtg -c docker
```

Contains: Google Play Services (`com.google.android.gms`), Play Store (`com.android.vending`), Google Services Framework (`com.google.android.gsf`). APKs are in `/system/product/priv-app/`.

`libndk` ARM translation does **not** work on Android 14 — only x86_64 apps will run.

## ADB Connection Flow

1. BotService runs on the host and connects to Redroid at `127.0.0.1:5555`.
2. Aspire 9.5+ passes container endpoint Host as the container name ("redroid"), which host processes cannot resolve. `Adb__Host` is overridden to `127.0.0.1` in AppHost.
3. ADB server (port 5037) runs on the host. BotService starts it if needed.
4. `AdbConnectionWorker` waits 30s for Redroid to boot, then retries connection up to 15 times.
5. After a successful connect, `PrepareDeviceAsync()` runs (see below).

## Device Preparation (PrepareDeviceAsync)

After every successful ADB connect, `AdbConnectionWorker.PrepareDeviceAsync()` runs these commands:

| Command | Purpose |
|---|---|
| `settings put system screen_off_timeout 2147483647` | Disable screen sleep (~24 day timeout) |
| `settings put global stay_on_while_plugged_in 7` | Keep screen on (all power sources) |
| `settings put global device_provisioned 1` | Mark device as provisioned |
| `settings put secure user_setup_complete 1` | Mark user setup as complete |
| `pm disable-user --user 0 com.google.android.setupwizard` | Disable GMS Setup Wizard (see below) |
| `input keyevent KEYCODE_WAKEUP` | Wake the screen |
| `am start -a android.intent.action.MAIN -c android.intent.category.HOME` | Navigate to launcher |

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

Redroid runs in Docker (not Podman). `docker` must be callable without sudo.

### 4. Redroid Image

Build `redroid/redroid:14.0.0_mindthegapps` locally. See README.md step 4.

## Common Issues

### Black screen in web viewer (FPS counter active but image is black)

**Cause A — Screen asleep:** `mWakefulness=Asleep`. Framebuffer returns solid black when the display is off.
- Fix: `adb shell input keyevent KEYCODE_WAKEUP`
- Permanent: `PrepareDeviceAsync()` runs this automatically on connect.

**Cause B — Google Setup Wizard blocking:** `mFocusedApp` shows `com.google.android.setupwizard`. The GMS wizard renders black with software rendering and relaunches itself even after force-stop.
- Fix: `adb shell pm disable-user --user 0 com.google.android.setupwizard`
- Permanent: `PrepareDeviceAsync()` runs this automatically. The `ro.setupwizard.mode=DISABLED` env var only disables the AOSP wizard, not the GMS one.

### Play Store: "This device isn't Play Protect certified"

The device needs to be registered with Google once per data directory.
1. Get GSF Android ID: `adb shell 'su root sqlite3 /data/data/com.google.android.gsf/databases/gservices.db "select * from main where name = \"android_id\";"'` → convert decimal to hex
2. Register at **https://www.google.com/android/uncertified/** (signed in as the target Google account)
3. Wait ~15 min, then: `adb shell pm clear com.google.android.gms && adb shell pm clear com.android.vending`
4. Reopen Play Store and sign in.

The registered GSF ID lives in `LastZBot.AppHost/docker/redroid/data/`. Do not delete this directory. See `TASK-golden-image.md` for the plan to bake this into the image.

### ADB shows "offline" or "Not connected"

- **Redroid container exited**: `docker ps -a` — check binder setup. View logs: `docker logs <id>`
- **Wrong host**: BotService must use `127.0.0.1`, not the container name. Fixed in AppHost.
- **Boot timing**: Redroid takes 30–60+ seconds to boot. Worker waits and retries.
- **Two Redroid containers running**: Aspire may have stale containers. `docker ps | grep redroid` — stop extras.

### Redroid exits with code 129

- **Binder not available**: `dmesg` shows "Binder driver could not be opened". Re-run binder setup.
- **Broken symlinks**: `ls -la /dev/binder` shows "broken symbolic link". Remount binderfs.

### Port 5555 already in use

Aspire's DCP proxy binds 5555. Stop Aspire before running Redroid manually.

### Aspire dashboard asks for a login token

Should not happen — auth is disabled via `builder.Configuration["Dashboard:Frontend:AuthMode"] = "Unsecured"` in `AppHost.cs` and mirrored in `appsettings.Development.json`. If it reappears after a framework update, re-add both.

### SignalR / Live stream not working

- BotService must be reachable from the browser. Service Discovery provides the URL.
- `FrameBroadcaster` only sends frames when `_adbService.IsConnected` is true.
- If screen is black but FPS counter shows activity: screen is asleep or setup wizard is blocking (see above).

## Configuration Reference

| Variable | Where | Purpose |
|---|---|---|
| `Adb__Host` | AppHost env | `127.0.0.1` (overrides container name resolution) |
| `Adb__Port` | AppHost env | `5555` (Redroid ADB port) |
| `Adb__DeviceWidth/Height` | AppHost env | `720x1280` |
| `redroid.gpu.mode` | AppHost env | `software` (no GPU passthrough) |
| `ro.setupwizard.mode` | AppHost env | `DISABLED` (AOSP wizard only) |
| `Dashboard:Frontend:AuthMode` | AppHost.cs config | `Unsecured` (no login token) |

## Running the Stack

```bash
# From repo root
dotnet run --project LastZBot.AppHost --launch-profile local
```

The Aspire dashboard opens automatically (no token). BotService auto-prepares the device after boot.

## Key Files

| File | Purpose |
|---|---|
| `LastZBot.AppHost/AppHost.cs` | Service orchestration, Redroid + BotService config |
| `LastZBot.BotService/Device/AdbConnectionWorker.cs` | Boot wait, retries, `PrepareDeviceAsync` |
| `LastZBot.BotService/Device/AdbService.cs` | ADB connect, screenshot (framebuffer), tap, swipe |
| `LastZBot.BotService/Device/FrameBroadcaster.cs` | SignalR frame broadcast loop |
| `LastZBot.Web/Components/Pages/Home.razor` | Dashboard, status, live view |
| `docker/redroid/run-redroid.ps1` | Manual Redroid start (Windows fallback) |
| `TASK-golden-image.md` | Next task: bake current /data into Docker image |
