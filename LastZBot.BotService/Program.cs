using LastZBot.BotService;
using LastZBot.BotService.Data;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Add PostgreSQL database context
builder.AddNpgsqlDbContext<LastZBotDbContext>("lastzbot");

// Ensure database is created
builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
