# LastZBot

## Prerequisites for Linux Development
This project uses **Redroid** (Android in Docker). 
While the Android device runs in a container, the `VisionService` (running on your host) requires a local ADB client to bridge the connection.

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
On modern kernels (6.x+), simply loading modules isn't enough. You must mount `binderfs` and create symlinks.

**Symptoms of incorrect setup:** ADB becomes flaky, reporting devices as `offline` or saying they are `already connected` when they aren't.

#### Step-by-Step Setup:
1. **Install modules & Load:**
   ```bash
   sudo apt install linux-modules-extra-`uname -r`
   sudo modprobe binder_linux devices="binder,hwbinder,vndbinder"
   ```

2. **Mount binderfs & Create Symlinks (Kernel 6.x+):**
   ```bash
   sudo mkdir -p /dev/binderfs
   sudo mount -t binder binder /dev/binderfs
   sudo ln -sf /dev/binderfs/{binder,hwbinder,vndbinder} /dev/
   ```

3. **Make it permanent:**
   - Add to `/etc/fstab`:
     `binder /dev/binderfs binder nofail 0 0`
   - Create `/etc/tmpfiles.d/binder.conf`:
     `L+ /dev/binder - - - - /dev/binderfs/binder`
     `L+ /dev/hwbinder - - - - /dev/binderfs/hwbinder`
     `L+ /dev/vndbinder - - - - /dev/binderfs/vndbinder`

#### Verification:
- **Module loaded?** `lsmod | grep binder` (should show `binder_linux`)
- **Devices exist?** `ls -l /dev/{binder,hwbinder,vndbinder}`
- **Errors?** `sudo dmesg | grep -i binder | tail`

The Aspire AppHost will automatically configure the connection to Redroid once the ADB server is available on your host.
