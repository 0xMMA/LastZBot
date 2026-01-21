var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL for action logs and patterns
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("lastzbot");

// Redroid container with privileged mode for Android emulation
var redroid = builder.AddContainer("redroid", "redroid/redroid", "11.0.0-latest")
    .WithContainerName("redroid")
    .WithContainerRuntimeArgs("--privileged")
    .WithEndpoint(5555, 5555, name: "adb", isExternal: true)
    .WithBindMount("./docker/redroid/data", "/data")
    .WithEnvironment("redroid.gpu.mode", "software")
    .WithEnvironment("redroid.gralloc.no_hw_buffer", "1")
    .WithEnvironment("redroid.fps", "30")
    .WithEnvironment("redroid.width", "720")
    .WithEnvironment("redroid.height", "1280")
    .WithEnvironment("redroid.dpi", "320");

// Bot service - coordinates automation tasks
var botService = builder.AddProject<Projects.LastZBot_BotService>("botservice")
    .WithReference(postgres);

// Vision service - handles ADB connection and screen capture
var visionService = builder.AddProject<Projects.LastZBot_VisionService>("visionservice")
    .WithHttpHealthCheck("/api/status")
    .WithEnvironment("Adb__Host", redroid.GetEndpoint("adb").Property(EndpointProperty.Host))
    .WithEnvironment("Adb__Port", redroid.GetEndpoint("adb").Property(EndpointProperty.Port))
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
    .WithReference(visionService)
    .WaitFor(apiService);

builder.Build().Run();
