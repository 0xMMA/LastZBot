var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL for action logs and patterns
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("lastzbot");

// Redroid container with privileged mode for Android emulation
var redroid = builder.AddContainer("redroid", "redroid/redroid", "latest")
    .WithContainerName("redroid")
    .WithContainerRuntimeArgs("--privileged")
    .WithEndpoint(5555, 5555, name: "adb", isExternal: true)
    .WithBindMount("./docker/redroid/data", "/data")
    .WithEnvironment("ANDROID_ADB_SERVER_PORT", "5555")
    .WithEnvironment("redroid.gpu.mode", "guest");

// scrcpy-web for live view in the browser
var scrcpyWeb = builder.AddContainer("scrcpy-web", "emptysuns/scrcpy-web", "v0.1")
    .WithContainerName("scrcpy-web")
    .WithHttpEndpoint(8000, 8000, name: "scrcpy")
    .WithEnvironment("DEVICE_HOST", redroid.GetEndpoint("adb").Property(EndpointProperty.Host))
    .WithEnvironment("DEVICE_PORT", "5555")
    .WithAnnotation(new CommandLineArgsCallbackAnnotation(args =>
    {
        args.Clear();
        args.Add("sh");
        args.Add("-c");
        // Ensure ADB connects to Redroid before starting scrcpy-web
        args.Add("until adb connect $DEVICE_HOST:$DEVICE_PORT; do sleep 1; done && npm start");
    }))
    .WaitFor(redroid);

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
    .WaitFor(apiService)
    .WaitFor(scrcpyWeb);

builder.Build().Run();
