using System.Net.Http.Json;

namespace LastZBot.Web;

public class VisionApiClient(HttpClient httpClient)
{
    public async Task<string?> GetBaseAddressAsync(CancellationToken cancellationToken = default)
    {
        // Make a request so Service Discovery resolves the logical "botservice" to the actual URL.
        // The RequestMessage on the response contains the resolved request URI.
        const int maxRetries = 5;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync("/api/status", cancellationToken);
                var requestUri = response.RequestMessage?.RequestUri;
                if (requestUri != null)
                {
                    var baseUri = new Uri(requestUri.GetLeftPart(UriPartial.Authority));
                    return baseUri.ToString();
                }
            }
            catch (Exception)
            {
                if (attempt == maxRetries)
                    break;
                await Task.Delay(500 * attempt, cancellationToken);
            }
        }
        return httpClient.BaseAddress?.ToString();
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

public record VisionStatus(bool Connected, string? Device, int? DeviceWidth, int? DeviceHeight);
public record ConnectResult(bool Success, string? Device);
public record ScreenshotResult(string Image);
public record SuccessResult(bool Success);
