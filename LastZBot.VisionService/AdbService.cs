using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using System.Net;

namespace LastZBot.VisionService;

public class AdbService
{
    private readonly ILogger<AdbService> _logger;
    private readonly IHostEnvironment _environment;
    private AdbClient? _adbClient;
    private DeviceData? _device;
    private string _host = "127.0.0.1";
    private int _port = 5555; // Redroid default port

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

            // Create ADB client
            // We use 127.0.0.1:5037 as the default ADB server endpoint.
            // Even when connecting to a remote device, AdbClient still expects to talk to a local ADB server.
            _adbClient = new AdbClient(new IPEndPoint(IPAddress.Loopback, 5037));

            // Connect to the device (BlueStacks/emulator/redroid)
            var endpoint = new DnsEndPoint(connectHost, connectPort);
            _logger.LogInformation("Connecting to device at {Host}:{Port}...", connectHost, connectPort);
            
            try 
            {
                var connectResult = await _adbClient.ConnectAsync(endpoint);
                _logger.LogInformation("Connection result: {Result}", connectResult);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ConnectAsync failed, but continuing as it might already be connected or ADB server might handle it.");
            }

            // Wait a moment for device to register
            await Task.Delay(1000);

            // Get connected devices
            var devices = await _adbClient.GetDevicesAsync();
            if (!devices.Any())
            {
                _logger.LogWarning("No devices found after connection. Attempting to force connect again...");
                try 
                {
                    await _adbClient.ConnectAsync(endpoint);
                    await Task.Delay(2000);
                    devices = await _adbClient.GetDevicesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Force connect failed");
                }
            }

            if (!devices.Any())
            {
                _logger.LogWarning("Still no devices found after retry.");
                return false;
            }

            // Find our device
            _device = devices.FirstOrDefault(d => 
                d.Serial.Contains(connectHost) || 
                d.Serial.Contains($"{connectPort}") ||
                d.State == DeviceState.Online);

            if (_device == null)
            {
                _device = devices.First();
            }

            _logger.LogInformation("Connected to device: {Serial} (State: {State})", 
                _device.Serial, _device.State);
            
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
            // Use screencap to save PNG to device, then pull the file
            const string remotePath = "/sdcard/screenshot_temp.png";
            
            // Capture screenshot to file on device
            var receiver = new ConsoleOutputReceiver();
            await _adbClient.ExecuteRemoteCommandAsync(
                $"screencap -p {remotePath}", 
                _device, 
                receiver,
                CancellationToken.None);
            
            // Pull the file from device
            using var ms = new MemoryStream();
            using var syncService = new SyncService(_device);
            
            await syncService.PullAsync(remotePath, ms, null, CancellationToken.None);
            
            // Delete temp file on device
            await _adbClient.ExecuteRemoteCommandAsync(
                $"rm {remotePath}", 
                _device, 
                receiver,
                CancellationToken.None);
            
            var data = ms.ToArray();
            
            if (data.Length == 0)
            {
                _logger.LogWarning("Screenshot data is empty");
                return null;
            }
            
            _logger.LogDebug("Screenshot captured: {Size} bytes", data.Length);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing screenshot");
            return null;
        }
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

    public bool IsConnected => _device != null && _device.State == DeviceState.Online;
    
    public string? DeviceSerial => _device?.Serial;
}
