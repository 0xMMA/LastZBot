var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL for action logs and patterns
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("lastzbot");

// LOCAL DEVELOPMENT:
// We use BlueStacks emulator (or similar) running on the host machine.
// BlueStacks exposes ADB at 127.0.0.1:5605 by default.
// Start BlueStacks and enable ADB in settings before running Aspire.
//
// CLOUD DEPLOYMENT:
// For cloud/Linux deployment, use redroid container with privileged mode.
// Uncomment the redroid section below and update VisionService config accordingly.

// Bot service - coordinates automation tasks
var botService = builder.AddProject<Projects.LastZBot_BotService>("botservice")
    .WithReference(postgres);

// Vision service - handles ADB connection and screen capture
// Connects to BlueStacks at 127.0.0.1:5605 for local dev
var visionService = builder.AddProject<Projects.LastZBot_VisionService>("visionservice")
    .WithHttpHealthCheck("/api/status")
    .WithEnvironment("Adb__Host", "127.0.0.1")
    .WithEnvironment("Adb__Port", "5605");

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

// ============================================================================
// CLOUD/REDROID CONFIGURATION (uncomment for Linux deployment)
// ============================================================================
// 
// var redroid = builder.AddContainer("redroid", "redroid/redroid", "14.0.0-latest")
//     .WithContainerName("redroid")
//     .WithContainerRuntimeArgs("--privileged")
//     .WithEndpoint(5555, 5555, name: "adb")
//     .WithBindMount("./docker/redroid/data", "/data")
//     .WithEnvironment("ANDROID_ADB_SERVER_PORT", "5555");
//
// var scrcpyWeb = builder.AddContainer("scrcpy-web", "emptysuns/scrcpy-web", "v0.1")
//     .WithContainerName("scrcpy-web")
//     .WithEndpoint(8000, 8000, name: "scrcpy")
//     .WithEnvironment("DEVICE_HOST", "redroid")
//     .WithEnvironment("DEVICE_PORT", "5555");
//
// Update visionService to connect to redroid:
// .WithEnvironment("Adb__Host", "redroid")
// .WithEnvironment("Adb__Port", "5555");
