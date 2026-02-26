namespace LastZBot.BotService.Device;

public class AdbConnectionWorker : BackgroundService
{
    private readonly ILogger<AdbConnectionWorker> _logger;
    private readonly AdbService _adbService;

    public AdbConnectionWorker(ILogger<AdbConnectionWorker> logger, AdbService adbService)
    {
        _logger = logger;
        _adbService = adbService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AdbConnectionWorker is starting...");

        // Redroid (Android in Docker) takes 30-60+ seconds to boot and expose ADB.
        // Wait before first connect attempt.
        _logger.LogInformation("Waiting 30 seconds for Redroid to boot and expose ADB...");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        // Connect with retries
        var connected = await ConnectWithRetriesAsync(stoppingToken);

        if (connected)
        {
            _logger.LogInformation("ADB connection established successfully to {Device}", _adbService.DeviceSerial);
            await PrepareDeviceAsync();
        }
        else
        {
            _logger.LogWarning("Could not connect to ADB device. Screenshots will not be available until connected.");
        }

        // Connection monitoring loop
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_adbService.IsConnected)
            {
                _logger.LogWarning("ADB disconnected. Attempting to reconnect...");
                var reconnected = await _adbService.ConnectAsync();
                if (reconnected)
                    await PrepareDeviceAsync();
            }

            await Task.Delay(30000, stoppingToken); // Check every 30 seconds
        }
    }

    private async Task PrepareDeviceAsync()
    {
        _logger.LogInformation("Preparing device: disabling sleep, bypassing setup wizard...");

        // Disable screen timeout (2147483647 ms ≈ 24 days) and keep screen on
        await _adbService.ExecuteShellCommandAsync("settings put system screen_off_timeout 2147483647");
        await _adbService.ExecuteShellCommandAsync("settings put global stay_on_while_plugged_in 7");

        // Mark device as provisioned so the Google Setup Wizard does not block the UI.
        // ro.setupwizard.mode=DISABLED skips the AOSP wizard but GMS ships its own
        // WelcomeActivity (com.google.android.setupwizard) which only checks these flags.
        await _adbService.ExecuteShellCommandAsync("settings put global device_provisioned 1");
        await _adbService.ExecuteShellCommandAsync("settings put secure user_setup_complete 1");

        // Permanently disable the Google Setup Wizard for this user session.
        // The GMS wizard (com.google.android.setupwizard) ignores ro.setupwizard.mode=DISABLED
        // and will keep relaunching itself until its package is disabled.
        await _adbService.ExecuteShellCommandAsync("pm disable-user --user 0 com.google.android.setupwizard");

        // Wake screen and navigate to the launcher
        await _adbService.ExecuteShellCommandAsync("input keyevent KEYCODE_WAKEUP");
        await _adbService.ExecuteShellCommandAsync("am start -a android.intent.action.MAIN -c android.intent.category.HOME");

        _logger.LogInformation("Device prepared: screen awake, setup wizard disabled, launcher visible.");
    }

    private async Task<bool> ConnectWithRetriesAsync(CancellationToken stoppingToken)
    {
        const int maxAttempts = 15;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            _logger.LogInformation("ADB connect attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
            var connected = await _adbService.ConnectAsync();
            if (connected)
                return true;

            if (attempt < maxAttempts)
            {
                _logger.LogWarning("Connect failed. Retrying in 10 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        return false;
    }
}
