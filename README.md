# LastZBot

## Prerequisites for Linux Development
This project uses **Redroid** (Android in Docker) and **scrcpy-web**. 
While the Android device runs in a container, the `VisionService` (running on your host) requires a local ADB client to bridge the connection.

Please install ADB on your host machine:
```bash
sudo apt update
sudo apt install adb
```

The Aspire AppHost will automatically configure the connection to Redroid once the ADB server is available on your host.
