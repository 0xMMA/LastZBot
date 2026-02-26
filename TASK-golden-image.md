# TASK: Golden Redroid Image (Bake /data into Docker image)

## Goal

Create a self-contained Docker image that includes the current pre-configured Android `/data` state, so that any new container instance starts with:

- Google Play Services already registered (GSF ID: `3dd97fd57e4d28ff`)
- Google Setup Wizard permanently disabled
- Device provisioned and user setup marked complete
- Screen sleep disabled

This eliminates the one-time GSF registration dance for every new instance or machine.

## Current State (as of commit)

- Image in use: `redroid/redroid:14.0.0_mindthegapps` (local, built via ayasa520/redroid-script)
- Data directory: `LastZBot.AppHost/docker/redroid/data/` (bind-mounted to `/data` in container)
- GSF Android ID: `3dd97fd57e4d28ff` (hex) / `4456733861183564031` (decimal) — **registered** at g.co/AndroidCertifiedDevice
- Google account: **signed in** (Play Store accessible)
- Aspire config: `WithBindMount("./docker/redroid/data", "/data")` in AppHost.cs

## Approach

### Why bake `/data` into the image?

Docker's overlay filesystem gives each container its own **writable copy** of image layers. If `/data` is a layer in the image (not a bind mount), every new container instance:
- Starts from the golden pre-configured state
- Writes to its own isolated writable layer
- Does not affect other containers or the image

This is the standard "golden master" pattern for container-based automation.

### Why NOT keep the bind mount?

With a bind mount, the data directory on the host is **shared** between all containers. Running two instances simultaneously would corrupt each other's Android state. It also means the GSF registration is machine-specific (tied to the host path).

## Implementation Steps

### 1. Decide what to bake in

**Option A — Full `/data` including signed-in Google account (Recommended for single-account bots)**
- All instances start already signed into Google
- Each container runs independently (own writable layer)
- Risk: Google account tokens in multiple simultaneous containers may conflict if used concurrently with the same account

**Option B — GSF state only, no Google account**
- Bake in only the GSF-relevant state: `device_provisioned`, `user_setup_complete`, the `com.google.android.gsf` data directory
- Each instance still needs to sign into Google manually once
- Safer for multi-account scenarios

For the initial implementation, **Option A** is recommended to match current state.

### 2. Create the golden image

The `/data` directory has Android root-owned files. Use `sudo tar` to create the archive preserving permissions, then use Docker `ADD` (which extracts tars preserving ownership and modes):

```bash
# From the repo root
cd LastZBot.AppHost

# Create a tar of the current data directory (preserves Linux permissions/ownership)
sudo tar -cJf docker/redroid/data-golden.tar.xz -C docker/redroid/data .
```

Create `docker/redroid/Dockerfile.golden`:
```dockerfile
FROM redroid/redroid:14.0.0_mindthegapps
# ADD automatically extracts tar archives and preserves file permissions/ownership
ADD data-golden.tar.xz /data/
```

Build the golden image:
```bash
docker build \
  -f docker/redroid/Dockerfile.golden \
  -t redroid/redroid:14.0.0_golden \
  docker/redroid/
```

### 3. Update AppHost.cs

Replace the image tag and remove the bind mount:

```csharp
// Before
var redroid = builder.AddContainer("redroid", "redroid/redroid", "14.0.0_mindthegapps")
    .WithBindMount("./docker/redroid/data", "/data")
    ...

// After
var redroid = builder.AddContainer("redroid", "redroid/redroid", "14.0.0_golden")
    // No bind mount — /data lives in the image layer; each container gets its own writable copy
    ...
```

### 4. Verify

```bash
# Start a fresh container from the golden image (no bind mount)
docker run -d --name redroid-golden-test --privileged \
  -p 5557:5555 \
  -e "redroid.gpu.mode=software" \
  -e "redroid.gralloc.no_hw_buffer=1" \
  redroid/redroid:14.0.0_golden

sleep 30
adb connect 127.0.0.1:5557

# Confirm GSF ID matches
adb -s 127.0.0.1:5557 shell \
  'su root sqlite3 /data/data/com.google.android.gsf/databases/gservices.db \
   "select * from main where name = \"android_id\";"'
# Expected: android_id|4456733861183564031

# Confirm Play Store opens (and shows signed-in state if Option A)
adb -s 127.0.0.1:5557 shell am start -n com.android.vending/.AssetBrowserActivity
sleep 3
adb -s 127.0.0.1:5557 exec-out screencap -p > /tmp/golden-verify.png

docker stop redroid-golden-test && docker rm redroid-golden-test
```

### 5. Clean up

After the golden image is confirmed working:
- Delete `docker/redroid/data-golden.tar.xz` (large, don't commit to git)
- Add `docker/redroid/data-golden.tar.xz` to `.gitignore`
- Add `docker/redroid/data/` to `.gitignore` (already should be — it contains user credentials)
- Update `run-redroid.ps1` to use `14.0.0_golden`

## Considerations & Risks

**Google token expiry:** If Option A is used, the baked-in Google account tokens will eventually expire (typically days to weeks). After expiry, the Play Store will ask to re-authenticate. Containers are ephemeral so this may be acceptable — just rebuild the golden image after re-authenticating.

**GSF ID uniqueness:** All containers share the same GSF ID (`3dd97fd57e4d28ff`). Google treats them as the same device. This is fine for single-account usage. For multi-account parallel botting, each account would need to be running on the same "device" from Google's perspective — this may or may not cause issues depending on the game's anti-cheat.

**Image size:** The golden image will be larger than `14.0.0_mindthegapps` (~3.7GB) because it includes the full `/data` partition. Typical Android `/data` after GApps first boot is 500MB–1.5GB.

**Rebuilding the image:** If the Google account gets signed out or tokens expire, re-do from step 2. Keep the data directory around until the golden image is tested.

**`docker/redroid/data/` in git:** This directory MUST NOT be committed — it contains Google account tokens and sensitive Android state. Verify it is in `.gitignore`.

## Rebuild the Base Image First?

The `redroid/redroid:14.0.0_mindthegapps` base image was built locally. If the machine is wiped or Docker is pruned, rebuild it first (see README step 4) before building the golden image.

## Related Files

- `LastZBot.AppHost/AppHost.cs` — where image tag and bind mount are configured
- `docker/redroid/run-redroid.ps1` — manual fallback run script
- `LastZBot.BotService/Device/AdbConnectionWorker.cs` — PrepareDeviceAsync (still useful even with golden image, in case state drifts)
