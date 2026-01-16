using LastZBot.BotService.Data;
using Microsoft.EntityFrameworkCore;

namespace LastZBot.BotService;

public class DatabaseInitializer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LastZBotDbContext>();
            
            _logger.LogInformation("Initializing database...");
            await dbContext.Database.EnsureCreatedAsync(stoppingToken);
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
        }
    }
}
