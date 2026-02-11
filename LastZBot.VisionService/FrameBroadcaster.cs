using Microsoft.AspNetCore.SignalR;

namespace LastZBot.VisionService;

public class FrameBroadcaster : BackgroundService
{
    private readonly ILogger<FrameBroadcaster> _logger;
    private readonly AdbService _adbService;
    private readonly IHubContext<FrameHub> _hubContext;

    public FrameBroadcaster(
        ILogger<FrameBroadcaster> logger,
        AdbService adbService,
        IHubContext<FrameHub> hubContext)
    {
        _logger = logger;
        _adbService = adbService;
        _hubContext = hubContext;
    }

    private int _logCounter = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FrameBroadcaster is starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_adbService.IsConnected)
            {
                try
                {
                    _logger.LogDebug("Capturing frame...");
                    // Use GetFrameAsync which might be faster, but it's raw data.
                    // For now, let's stick to CaptureScreenshotAsync but add more logging.
                    var frame = await _adbService.CaptureScreenshotAsync(); 
                    if (frame != null)
                    {
                        if (_logCounter++ % 10 == 0)
                        {
                            _logger.LogInformation("Broadcasting frame {Size} bytes", frame.Length);
                        }
                        var base64 = Convert.ToBase64String(frame);
                        var frameData = $"data:image/jpeg;base64,{base64}";
                        await _hubContext.Clients.All.SendAsync("ReceiveFrame", frameData, stoppingToken);
                    }
                    else
                    {
                        _logger.LogWarning("Captured frame is null");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting frame");
                }
            }
            else
            {
                _logger.LogTrace("ADB not connected, skipping frame capture");
            }

            // Aim for ~5-10 FPS for MVP
            await Task.Delay(100, stoppingToken);
        }
    }
}