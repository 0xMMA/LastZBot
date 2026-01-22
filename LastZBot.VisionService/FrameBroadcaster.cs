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
                        _logger.LogInformation("Broadcasting frame {Size} bytes", frame.Length);
                        var base64 = Convert.ToBase64String(frame);
                        await _hubContext.Clients.All.SendAsync("ReceiveFrame", $"data:image/png;base64,{base64}", stoppingToken);
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