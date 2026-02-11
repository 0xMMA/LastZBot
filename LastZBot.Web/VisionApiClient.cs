using System.Net.Http.Json;

namespace LastZBot.Web;

public class VisionApiClient(HttpClient httpClient)
{
    public async Task<string?> GetBaseAddressAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use GetStringAsync on a known endpoint to force resolution if needed, 
            // but HttpClient should already have it.
            // However, httpClient.BaseAddress is what we set in Program.cs.
            // Aspire's Service Discovery usually handles resolution during the request.
            
            // If we are using logical names, we can try to "ping" the service to ensure discovery is active.
            await httpClient.GetAsync("/api/status", cancellationToken);
            return httpClient.BaseAddress?.ToString();
        }
        catch (Exception)
        {
            return httpClient.BaseAddress?.ToString();
        }
    }

    public async Task<VisionStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<VisionStatus>("/api/status", cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsync("/api/connect", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ConnectResult>(cancellationToken);
                return result?.Success ?? false;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<string?> CaptureScreenshotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<ScreenshotResult>("/api/screenshot", cancellationToken);
            return result?.Image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> TapAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/tap", new { X = x, Y = y }, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SuccessResult>(cancellationToken);
                return result?.Success ?? false;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> SwipeAsync(int startX, int startY, int endX, int endY, int durationMs = 300, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/swipe", new 
            { 
                StartX = startX, 
                StartY = startY, 
                EndX = endX, 
                EndY = endY, 
                DurationMs = durationMs 
            }, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SuccessResult>(cancellationToken);
                return result?.Success ?? false;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public record VisionStatus(bool Connected, string? Device);
public record ConnectResult(bool Success, string? Device);
public record ScreenshotResult(string Image);
public record SuccessResult(bool Success);
