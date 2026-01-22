using Microsoft.AspNetCore.SignalR;

namespace LastZBot.VisionService;

public class FrameHub : Hub
{
    private readonly ILogger<FrameHub> _logger;

    public FrameHub(ILogger<FrameHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to FrameHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from FrameHub: {ConnectionId}. Error: {Error}", 
            Context.ConnectionId, exception?.Message ?? "None");
        await base.OnDisconnectedAsync(exception);
    }
}