using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using System.Net;
using System.Linq;

namespace LastZBot.BotService.Device;

public class AdbService
{
    private readonly ILogger<AdbService> _logger;
    private readonly IHostEnvironment _environment;
    private AdbClient? _adbClient;
    private DeviceData? _device;
    private string _host = "127.0.0.1";
    private int _port = 5555; // Redroid default port
    private string? _debugSavePath;

    public AdbService(ILogger<AdbService> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public void ConfigureConnection(string host, int port)
    {
        _host = host;
        _port = port;
        _logger.LogInformation("ADB connection configured for {Host}:{Port}", host, port);
    }

    public async Task<bool> ConnectAsync(string? host = null, int? port = null)
    {
        var connectHost = host ?? _host;
        var connectPort = port ?? _port;

        // BotService runs on the host. If Aspire passes container name "redroid", host cannot resolve it.
        // Fallback to localhost for host-side ADB connections.
        if (string.Equals(connectHost, "redroid", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Host 'redroid' cannot be resolved from host process. Using 127.0.0.1");
            connectHost = "127.0.0.1";
        }

        try
        {
            // Start ADB server if not running
            if (!AdbServer.Instance.GetStatus().IsRunning)
            {
                _logger.LogInformation("ADB server not running. Attempting to start...");

                var adbPaths = new List<string>();

                // Only use BlueStacks ADB in Development (Windows local dev)
                if (_environment.IsDevelopment() && OperatingSystem.IsWindows())
                {
                    adbPaths.Add(@"C:\Program Files\BlueStacks_nxt\HD-Adb.exe");
                    adbPaths.Add(@"C:\Program Files (x86)\BlueStacks_nxt\HD-Adb.exe");
                }

                // Always try system ADB as fallback
                adbPaths.Add("adb");

                // Common Linux paths
                adbPaths.Add("/usr/bin/adb");
                adbPaths.Add("/usr/local/bin/adb");

                var started = false;
                foreach (var adbPath in adbPaths)
                {
                    try
                    {
                        // Check if file exists or it's just "adb" (to be found in PATH)
                        if (adbPath == "adb" || File.Exists(adbPath))
                        {
                            _logger.LogInformation("Trying ADB at: {Path}", adbPath);
                            await AdbServer.Instance.StartServerAsync(adbPath, false, CancellationToken.None);
                            _logger.LogInformation("ADB server started successfully using: {Path}", adbPath);
                            started = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to start ADB with {Path}", adbPath);
                    }
                }

                if (!started)
                {
                    _logger.LogWarning("Could not start ADB server automatically. This service requires an ADB binary to be installed on the host or available in the PATH.");
                    _logger.LogWarning("Please install ADB: 'sudo apt install adb' on Ubuntu/Debian.");
                }
            }
            else
            {
                _logger.LogInformation("ADB server already running");
            }

            // Create ADB client if not exists
            if (_adbClient == null)
            {
                // We use 127.0.0.1:5037 as the default ADB server endpoint.
                _adbClient = new AdbClient(new IPEndPoint(IPAddress.Loopback, 5037));
            }

            // Connect to the device (BlueStacks/emulator/redroid)
            var endpoint = new DnsEndPoint(connectHost, connectPort);
            _logger.LogInformation("Connecting to device at {Host}:{Port}...", connectHost, connectPort);

            DeviceData? targetDevice = null;
            var retryCount = 0;
            const int maxRetries = 10;

            while (retryCount < maxRetries)
            {
                try
                {
                    var connectResult = await _adbClient.ConnectAsync(endpoint);
                    _logger.LogDebug("Connection attempt {Retry}: {Result}", retryCount, connectResult);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("ConnectAsync failed: {Message}", ex.Message);
                }

                // Wait for device to register and update state
                await Task.Delay(2000);

                var devices = await _adbClient.GetDevicesAsync();
                var deviceList = devices.ToList();

                // 1. Look for our specific device that is Online
                targetDevice = deviceList.FirstOrDefault(d =>
                    (d.Serial.Contains(connectHost) || d.Serial.Contains($"{connectPort}")) &&
                    d.State == DeviceState.Online);

                // 2. Fallback: any online device (if we only expect one)
                if (targetDevice == null)
                {
                    targetDevice = deviceList.FirstOrDefault(d => d.State == DeviceState.Online);
                    if (targetDevice != null)
                    {
                        _logger.LogInformation("Selected alternative online device: {Serial}", targetDevice.Serial);
                    }
                }

                if (targetDevice != null) break;

                // If we found the device but it's offline, log it
                var offlineDevice = deviceList.FirstOrDefault(d =>
                    d.Serial.Contains(connectHost) || d.Serial.Contains($"{connectPort}"));

                if (offlineDevice != null)
                {
                    _logger.LogInformation("Device {Serial} found but State is {State}. Retrying ({Retry}/{MaxRetries})...",
                        offlineDevice.Serial, offlineDevice.State, retryCount + 1, maxRetries);

                    // Sometimes disconnecting and reconnecting helps when stuck in Offline
                    if (retryCount > 0 && retryCount % 3 == 0)
                    {
                        _logger.LogInformation("Attempting to wake up offline device...");
                        // We don't have a reliable Disconnect here, but calling Connect again often works
                    }
                }
                else
                {
                    _logger.LogInformation("Device {Host}:{Port} not found in device list. Retrying ({Retry}/{MaxRetries})...",
                        connectHost, connectPort, retryCount + 1, maxRetries);
                }

                retryCount++;
            }

            _device = targetDevice;

            if (_device == null)
            {
                // Last ditch effort: pick the first device if any exists, even if offline
                var devices = await _adbClient.GetDevicesAsync();
                _device = devices.FirstOrDefault();
            }

            if (_device != null)
            {
                _logger.LogInformation("Final device selection: {Serial} (State: {State})",
                    _device.Serial, _device.State);
            }
            else
            {
                _logger.LogWarning("No devices found after all retries.");
                return false;
            }

            return _device.State == DeviceState.Online;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to ADB at {Host}:{Port}", connectHost, connectPort);
            return false;
        }
    }

    public async Task<byte[]?> CaptureScreenshotAsync()
    {
        if (_adbClient == null || _device == null)
        {
            _logger.LogWarning("ADB not connected. Call ConnectAsync first.");
            return null;
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var framebuffer = await _adbClient.GetFrameBufferAsync(_device, CancellationToken.None);
            var fetchTime = sw.ElapsedMilliseconds;

            if (framebuffer == null)
            {
                _logger.LogWarning("GetFrameBufferAsync returned null");
                return null;
            }

            if (framebuffer.Header.Width == 0 || framebuffer.Header.Height == 0)
            {
                _logger.LogWarning("Framebuffer has invalid dimensions: {Width}x{Height}",
                    framebuffer.Header.Width, framebuffer.Header.Height);
            }

            _logger.LogDebug("Framebuffer received: {Width}x{Height}, Bpp: {Bpp}, Size: {Size}",
                framebuffer.Header.Width, framebuffer.Header.Height, framebuffer.Header.Bpp,
                framebuffer.Data?.Length ?? 0);

            using var imageSharp = ToImageSharp(framebuffer);
            var convertTime = sw.ElapsedMilliseconds - fetchTime;

            if (imageSharp == null)
            {
                _logger.LogWarning("Failed to convert FrameBuffer to ImageSharp image. Header: {Width}x{Height}, Bpp: {Bpp}, DataLength: {DataLength}",
                    framebuffer.Header.Width, framebuffer.Header.Height, framebuffer.Header.Bpp, framebuffer.Data?.Length ?? 0);
                return null;
            }

            using var ms = new MemoryStream();
            await imageSharp.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 75 });
            var saveTime = sw.ElapsedMilliseconds - fetchTime - convertTime;

            var data = ms.ToArray();
            _logger.LogDebug("Screenshot captured via FrameBuffer: {Size} bytes. Fetch: {Fetch}ms, Convert: {Convert}ms, Save: {Save}ms",
                data.Length, fetchTime, convertTime, saveTime);

            if (_debugSavePath != null)
            {
                try
                {
                    var path = _debugSavePath;
                    _debugSavePath = null; // Clear after use
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(path, data);
                    _logger.LogInformation("Debug screenshot saved to {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save debug screenshot");
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing screenshot via FrameBuffer");
            return null;
        }
    }

    public Image<Rgba32>? ToImageSharp(Framebuffer framebuffer)
    {
        // Framebuffer holen (raw bytes)
        var buffer = framebuffer.Data;
        var width = (int)framebuffer.Header.Width;
        var height = (int)framebuffer.Header.Height;
        var bpp = (int)framebuffer.Header.Bpp;

        if (width <= 0 || height <= 0 || buffer == null || buffer.Length == 0)
        {
            _logger.LogWarning("Invalid framebuffer data: Width={Width}, Height={Height}, BufferLength={BufferLength}",
                width, height, buffer?.Length ?? 0);
            return null;
        }

        var expectedLength = width * height * (bpp / 8);
        if (buffer.Length < expectedLength)
        {
            _logger.LogWarning("Framebuffer data too small: Expected at least {Expected}, but got {Actual}",
                expectedLength, buffer.Length);
            return null;
        }

        // Neues Image erstellen
        var image = new Image<Rgba32>(width, height);

        _logger.LogDebug("Converting framebuffer to ImageSharp. Bpp: {Bpp}, BufferLength: {Length}", bpp, buffer.Length);

        // Pixel Format von Android ist meistens RGBA oder BGRA
        image.ProcessPixelRows(accessor =>
        {
            var bytesPerPixel = bpp / 8;
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var offset = (y * width + x) * bytesPerPixel;

                    if (offset + 3 < buffer.Length)
                    {
                        // Android ist meistens RGBA oder BGRA.
                        // Wir probieren RGBA, aber loggen die ersten paar Bytes falls nötig.
                        if (y == 0 && x == 0)
                        {
                            _logger.LogInformation("First pixel bytes: {B1}, {B2}, {B3}, {B4}",
                                buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);
                        }

                        // Detect BGRA vs RGBA (heuristic: if it's all 0 or 255 it's hard, but redroid is usually RGBA)
                        // Redroid usually uses RGBA (R at offset 0). BlueStacks often uses BGRA (B at offset 0).
                        // If the first pixel seems to have R=0 and B>0 when we expect blue, or vice versa, we might need to swap.
                        // For now, let's stick to RGBA as default.
                        row[x] = new Rgba32(
                            buffer[offset],     // R
                            buffer[offset + 1], // G
                            buffer[offset + 2], // B
                            buffer[offset + 3]  // A
                        );
                    }
                }
            }
        });

        return image;
    }

    public async Task<bool> TapAsync(int x, int y)
    {
        if (_adbClient == null || _device == null)
        {
            _logger.LogWarning("ADB not connected. Call ConnectAsync first.");
            return false;
        }

        try
        {
            var receiver = new ConsoleOutputReceiver();
            await _adbClient.ExecuteRemoteCommandAsync($"input tap {x} {y}", _device, receiver);

            _logger.LogDebug("Tap executed at ({X}, {Y})", x, y);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tap at ({X}, {Y})", x, y);
            return false;
        }
    }

    public async Task<bool> SwipeAsync(int startX, int startY, int endX, int endY, int durationMs = 300)
    {
        if (_adbClient == null || _device == null)
        {
            _logger.LogWarning("ADB not connected. Call ConnectAsync first.");
            return false;
        }

        try
        {
            var receiver = new ConsoleOutputReceiver();
            await _adbClient.ExecuteRemoteCommandAsync(
                $"input swipe {startX} {startY} {endX} {endY} {durationMs}",
                _device, receiver);

            _logger.LogDebug("Swipe executed from ({StartX}, {StartY}) to ({EndX}, {EndY})",
                startX, startY, endX, endY);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing swipe");
            return false;
        }
    }

    public async Task<bool> SendTextAsync(string text)
    {
        if (_adbClient == null || _device == null)
        {
            _logger.LogWarning("ADB not connected. Call ConnectAsync first.");
            return false;
        }

        try
        {
            // Escape special characters for shell
            var escapedText = text.Replace(" ", "%s").Replace("'", "\\'");
            var receiver = new ConsoleOutputReceiver();
            await _adbClient.ExecuteRemoteCommandAsync($"input text '{escapedText}'", _device, receiver);

            _logger.LogDebug("Text sent: {Text}", text);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text");
            return false;
        }
    }

    public async Task<bool> PressKeyAsync(string keycode)
    {
        if (_adbClient == null || _device == null)
        {
            _logger.LogWarning("ADB not connected. Call ConnectAsync first.");
            return false;
        }

        try
        {
            var receiver = new ConsoleOutputReceiver();
            await _adbClient.ExecuteRemoteCommandAsync($"input keyevent {keycode}", _device, receiver);

            _logger.LogDebug("Key pressed: {Keycode}", keycode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pressing key {Keycode}", keycode);
            return false;
        }
    }

    public async Task<string?> ExecuteShellCommandAsync(string command)
    {
        if (_adbClient == null || _device == null)
        {
            _logger.LogWarning("ADB not connected. Call ConnectAsync first.");
            return null;
        }

        try
        {
            var receiver = new ConsoleOutputReceiver();
            await _adbClient.ExecuteRemoteCommandAsync(command, _device, receiver);
            return receiver.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing shell command: {Command}", command);
            return null;
        }
    }

    public async Task<byte[]?> GetFrameAsync()
    {
        if (_adbClient == null || _device == null)
        {
            return null;
        }

        try
        {
            // Use GetFrameBufferAsync and convert to PNG to avoid slow file operations
            using var framebuffer = await _adbClient.GetFrameBufferAsync(_device, CancellationToken.None);
            if (framebuffer == null) return null;

            // If we can't easily convert to PNG here, we might stick to CaptureScreenshotAsync
            // but let's try to optimize it.
            return framebuffer.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting framebuffer");
            return null;
        }
    }

    public void RequestDebugScreenshot(string path)
    {
        _debugSavePath = path;
    }

    public bool IsConnected => _device != null && _device.State == DeviceState.Online;

    public string? DeviceSerial => _device?.Serial;
}
