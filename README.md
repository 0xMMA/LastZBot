# LastZBot

> **Important:** On Linux, the binder setup (step 3 below) must be run **after every reboot** unless you make it permanent. Redroid will not start without it.

## Prerequisites for Linux Development
This project uses **Redroid** (Android in Docker). 
While the Android device runs in a container, the `BotService` (running on your host) requires a local ADB client to bridge the connection.

### 1. Install .NET SDK
As we use Ubuntu Desktop for development, follow these steps to install the .NET SDK:

```bash
sudo apt update
sudo apt install -y dotnet-sdk-10.0
```

### 2. Install ADB
Please install ADB on your host machine:
```bash
sudo apt update
sudo apt install adb
```

### 3. Android Binder Setup (Required for Redroid)
On modern kernels (6.x+), you must install the binder module, mount `binderfs`, and create symlinks.

> **Important:** Steps 2 (load and mount) must be run **after every reboot** unless you complete step 3 (make it permanent). Redroid will not start without binder.

**Symptoms of incorrect setup:** Redroid container exits with code 129. `dmesg` shows "Binder driver could not be opened. Terminating." ADB reports devices as `offline`.

#### Step-by-Step Setup:
1. **Install kernel modules (includes binder_linux):**
   ```bash
   sudo apt install linux-modules-extra-$(uname -r)
   ```

2. **Load and mount binderfs:**
   ```bash
   sudo modprobe binder_linux devices="binder,hwbinder,vndbinder"
   sudo mkdir -p /dev/binderfs
   sudo mount -t binder binder /dev/binderfs
   sudo ln -sf /dev/binderfs/{binder,hwbinder,vndbinder} /dev/
   ```

3. **Make it permanent** (survives reboot):
   - Add to `/etc/fstab`:
     `binder /dev/binderfs binder nofail 0 0`
   - Create `/etc/tmpfiles.d/binder.conf`:
     `L+ /dev/binder - - - - /dev/binderfs/binder`
     `L+ /dev/hwbinder - - - - /dev/binderfs/hwbinder`
     `L+ /dev/vndbinder - - - - /dev/binderfs/vndbinder`

#### Verification:
- **Module loaded?** `lsmod | grep binder` (should show `binder_linux`)
- **Devices exist?** `ls -l /dev/{binder,hwbinder,vndbinder}` (must NOT be broken symlinks)
- **binderfs populated?** `ls /dev/binderfs/` (should show binder, hwbinder, vndbinder)
- **Errors?** `sudo dmesg | grep -i binder | tail`

The Aspire AppHost will automatically configure the connection to Redroid once the ADB server is available on your host.

**Run locally:**
```bash
dotnet run --project LastZBot.AppHost --launch-profile local
```

**Troubleshooting:** See [AGENTS.md](AGENTS.md) for common issues (ADB offline, Redroid exit 129, binder setup).
