var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL for action logs and patterns
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("lastzbot");

// Redroid container with privileged mode for Android emulation
var redroid = builder.AddContainer("redroid", "redroid/redroid", "14.0.0-latest")
    // .WithContainerName("redroid") // Commented out to avoid name conflicts if the container wasn't cleaned up properly. Re-evaluate if fixed name is needed.
    .WithContainerRuntimeArgs("--privileged")
    .WithEndpoint(5555, 5555, name: "adb", isExternal: true)
    .WithBindMount("./docker/redroid/data", "/data")
    .WithEnvironment("redroid.gpu.mode", "software")
    .WithEnvironment("redroid.gralloc.no_hw_buffer", "1")
    .WithEnvironment("redroid.fps", "30")
    .WithEnvironment("redroid.width", "720")
    .WithEnvironment("redroid.height", "1280")
    .WithEnvironment("redroid.dpi", "320");

// Bot service - coordinates automation tasks, device gateway (ADB, screenshot, tap, live stream)
// BotService runs on the host (not in container). Aspire 9.5+ resolves container endpoint Host to
// the container name ("redroid"), which host processes cannot resolve. Use 127.0.0.1 and the
// published port (5555) so the host ADB client can connect to the Redroid container.
var botService = builder.AddProject<Projects.LastZBot_BotService>("botservice")
    .WithReference(postgres)
    .WithHttpHealthCheck("/api/status")
    .WithEnvironment("Adb__Host", "127.0.0.1")
    .WithEnvironment("Adb__Port", "5555")
    .WithEnvironment("Adb__DeviceWidth", "720")
    .WithEnvironment("Adb__DeviceHeight", "1280")
    .WaitFor(redroid);

// API service for external integrations
var apiService = builder.AddProject<Projects.LastZBot_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(postgres);

// Blazor web frontend - dashboard and controls
var web = builder.AddProject<Projects.LastZBot_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithReference(botService)
    .WaitFor(apiService);

builder.Build().Run();
