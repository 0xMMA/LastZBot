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
                await _adbService.ConnectAsync();
            }

            await Task.Delay(30000, stoppingToken); // Check every 30 seconds
        }
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
