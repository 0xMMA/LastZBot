# LastZBot

> **Important:** On Linux, the binder setup (step 3 below) must be run **after every reboot** unless you make it permanent. Redroid will not start without it.

## Prerequisites for Linux Development

This project uses **Redroid** (Android-in-Docker) with **real Google Play Services** (MindTheGapps).
The `BotService` runs on the host and connects to the Android container via ADB.

### 1. Install .NET SDK

```bash
sudo apt update
sudo apt install -y dotnet-sdk-10.0
```

### 2. Install ADB

```bash
sudo apt update
sudo apt install adb
```

### 3. Android Binder Setup (Required for Redroid)

On modern kernels (6.x+), you must install the binder module, mount `binderfs`, and create symlinks.

> **Important:** Steps 2 (load and mount) must be run **after every reboot** unless you complete step 3 (make it permanent). Redroid will not start without binder.

**Symptoms of incorrect setup:** Redroid container exits with code 129. `dmesg` shows "Binder driver could not be opened." ADB reports devices as `offline`.

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

### 4. Build the Redroid image with Google Play Services

The Aspire stack uses a **locally-built** Docker image: `redroid/redroid:14.0.0_mindthegapps`.
This image is **not on Docker Hub** — you must build it once before running:

```bash
# Clone the build tool
cd /tmp && git clone https://github.com/ayasa520/redroid-script.git
cd redroid-script

# Install Python deps
python3 -m pip install tqdm requests --break-system-packages

# Build the image (downloads ~200MB MindTheGapps package)
python3 redroid.py -a 14.0.0 -mtg -c docker
```

This produces `redroid/redroid:14.0.0_mindthegapps` locally (~3.7GB). Only needs to be done once per machine.

> **Note:** `libndk` ARM translation does not work on Android 14. The image uses x86_64 native execution. ARM-only apps will not install.

### 5. Google Play Protect Registration (One-time per data directory)

On first boot with a fresh `/data` directory, the device will not be Play Protect certified. Google Play will refuse sign-in with "This device isn't Play Protect certified."

**Fix — register the GSF Android ID with Google:**

1. Get the device's GSF ID after first boot:
   ```bash
   # Find the redroid port from: docker ps | grep redroid
   adb connect 127.0.0.1:<PORT>
   adb shell 'su root sqlite3 /data/data/com.google.android.gsf/databases/gservices.db \
     "select * from main where name = \"android_id\";"'
   # Returns decimal. Convert to hex:
   python3 -c "print('{:016x}'.format(DECIMAL_VALUE))"
   ```

2. On a different browser/device, sign into the Google account you want to use, then visit:
   **https://www.google.com/android/uncertified/**
   Enter the 16-character hex GSF ID and submit.

3. Wait ~15 minutes, then clear Play Services state on the device:
   ```bash
   adb shell pm clear com.google.android.gms
   adb shell pm clear com.android.vending
   ```

4. Reopen Play Store and sign in.

> **Important:** The GSF ID is stored in `LastZBot.AppHost/docker/redroid/data/`. **Do not delete this directory** or you will need to register a new ID. See `TASK-golden-image.md` for the planned work to bake this state into the image permanently.

## Running the Stack

```bash
dotnet run --project LastZBot.AppHost --launch-profile local
```

The Aspire dashboard opens automatically (no login token required). All services start in order: Postgres → Redroid → BotService → Web.

After startup, the BotService will automatically:
- Wait for Redroid to boot (~30–60 seconds)
- Connect via ADB
- Wake the screen, disable screen timeout, bypass the Google Setup Wizard, and navigate to the launcher

**Troubleshooting:** See [AGENTS.md](AGENTS.md) for common issues (ADB offline, Redroid exit 129, binder setup, black screen).
