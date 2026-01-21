using LastZBot.VisionService;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Configure ADB connection settings
// Default to Redroid on localhost if not overridden via Adb__Host and Adb__Port env vars
var adbHost = builder.Configuration["Adb:Host"] ?? "127.0.0.1";
var adbPort = builder.Configuration.GetValue<int>("Adb:Port", 5555);

// Register AdbService as singleton
builder.Services.AddSingleton<AdbService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<AdbService>>();
    var environment = sp.GetRequiredService<IHostEnvironment>();
    var service = new AdbService(logger, environment);
    service.ConfigureConnection(adbHost, adbPort);
    return service;
});

// Add background worker for connection management
builder.Services.AddHostedService<Worker>();

// Add SignalR
builder.Services.AddSignalR();
builder.Services.AddHostedService<FrameBroadcaster>();

// Add CORS for web frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Allow any origin for SignalR
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

app.UseCors();
app.MapDefaultEndpoints();
app.MapHub<FrameHub>("/hubs/frame");

// API Endpoints

// Health/status endpoint
app.MapGet("/", (AdbService adb) => new
{
    Service = "VisionService",
    Connected = adb.IsConnected,
    Device = adb.DeviceSerial
});

// Get connection status
app.MapGet("/api/status", (AdbService adb) => new
{
    Connected = adb.IsConnected,
    Device = adb.DeviceSerial
});

// Connect to device
app.MapPost("/api/connect", async (AdbService adb) =>
{
    var result = await adb.ConnectAsync();
    return new { Success = result, Device = adb.DeviceSerial };
});

// Capture screenshot - returns base64 PNG
app.MapGet("/api/screenshot", async (AdbService adb) =>
{
    if (!adb.IsConnected)
    {
        return Results.BadRequest(new { Error = "ADB not connected" });
    }

    var screenshot = await adb.CaptureScreenshotAsync();
    if (screenshot == null)
    {
        return Results.Problem("Failed to capture screenshot");
    }

    var base64 = Convert.ToBase64String(screenshot);
    return Results.Ok(new { Image = $"data:image/png;base64,{base64}" });
});

// Capture screenshot - returns raw PNG image
app.MapGet("/api/screenshot/raw", async (AdbService adb) =>
{
    if (!adb.IsConnected)
    {
        return Results.BadRequest("ADB not connected");
    }

    var screenshot = await adb.CaptureScreenshotAsync();
    if (screenshot == null)
    {
        return Results.Problem("Failed to capture screenshot");
    }

    return Results.File(screenshot, "image/png", "screenshot.png");
});

// Tap at coordinates
app.MapPost("/api/tap", async (TapRequest request, AdbService adb) =>
{
    if (!adb.IsConnected)
    {
        return Results.BadRequest(new { Error = "ADB not connected" });
    }

    var result = await adb.TapAsync(request.X, request.Y);
    return Results.Ok(new { Success = result });
});

// Swipe gesture
app.MapPost("/api/swipe", async (SwipeRequest request, AdbService adb) =>
{
    if (!adb.IsConnected)
    {
        return Results.BadRequest(new { Error = "ADB not connected" });
    }

    var result = await adb.SwipeAsync(
        request.StartX, request.StartY,
        request.EndX, request.EndY,
        request.DurationMs);
    return Results.Ok(new { Success = result });
});

// Send text input
app.MapPost("/api/text", async (TextRequest request, AdbService adb) =>
{
    if (!adb.IsConnected)
    {
        return Results.BadRequest(new { Error = "ADB not connected" });
    }

    var result = await adb.SendTextAsync(request.Text);
    return Results.Ok(new { Success = result });
});

// Press key
app.MapPost("/api/key", async (KeyRequest request, AdbService adb) =>
{
    if (!adb.IsConnected)
    {
        return Results.BadRequest(new { Error = "ADB not connected" });
    }

    var result = await adb.PressKeyAsync(request.Keycode);
    return Results.Ok(new { Success = result });
});

// Execute shell command
app.MapPost("/api/shell", async (ShellRequest request, AdbService adb) =>
{
    if (!adb.IsConnected)
    {
        return Results.BadRequest(new { Error = "ADB not connected" });
    }

    var result = await adb.ExecuteShellCommandAsync(request.Command);
    return Results.Ok(new { Output = result });
});

app.Run();

// Request DTOs
record TapRequest(int X, int Y);
record SwipeRequest(int StartX, int StartY, int EndX, int EndY, int DurationMs = 300);
record TextRequest(string Text);
record KeyRequest(string Keycode);
record ShellRequest(string Command);
