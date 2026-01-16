namespace LastZBot.VisionService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly AdbService _adbService;

    public Worker(ILogger<Worker> logger, AdbService adbService)
    {
        _logger = logger;
        _adbService = adbService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VisionService is starting...");

        // Connect to ADB device
        var connected = await _adbService.ConnectAsync();
        if (!connected)
        {
            _logger.LogWarning("Failed to connect to ADB device. Retrying in 10 seconds...");
            await Task.Delay(10000, stoppingToken);
            connected = await _adbService.ConnectAsync();
        }

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
}
