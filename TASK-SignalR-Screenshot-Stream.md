# Task: Fix SignalR Screenshot / Live Stream (Backend → Frontend)

## Ziel
Der Live-Stream soll die Android-Screenshots (Redroid) per SignalR vom BotService ins Blazor-Frontend transportieren und anzeigen. Wenn ADB verbunden ist und Redroid läuft, soll im Dashboard unter "Live View" ein flüssiger Stream der aktuellen Android-Ansicht erscheinen.

## Architektur

```
Redroid (ADB:5555) → BotService (AdbService.CaptureScreenshotAsync)
                            ↓
                     FrameBroadcaster (BackgroundService)
                            ↓
                     SignalR Hub "/hubs/frame"
                            ↓
                     ReceiveFrame(string base64DataUrl)
                            ↓
                     Blazor Home.razor → <img src="@liveFrameData" />
```

## Was bereits existiert

- **BotService**: `FrameHub`, `FrameBroadcaster`, `/hubs/frame` Endpoint
- **FrameBroadcaster**: Läuft in einer Schleife (100ms), holt Screenshots via ADB, sendet Base64-JPEG als `ReceiveFrame`
- **Blazor (Home.razor)**: HubConnection zu botservice, `ReceiveFrame` Handler setzt `liveFrameData`, `<img src="@liveFrameData">` zeigt das Bild
- **VisionApiClient.GetBaseAddressAsync**: Holt die BotService-URL für SignalR (Service Discovery / Aspire)
- Home.razor hat komplexe URL-Auflösung (configUrl, https+http:// Normalisierung, Fallback localhost:5031)

## Mögliche Probleme / zu prüfen

1. **SignalR-Verbindung**: Konnte sich der Blazor-Client überhaupt mit dem Hub verbinden? (hubConnection.State, Closed-Handler)
2. **URL**: BotService wird über Aspire Service Discovery unter `https+http://botservice` angeboten. Blazor muss die echte URL (z.B. `http://localhost:xxxx`) bekommen, weil SignalR vom Browser aus oder vom Blazor-Server aus aufgerufen wird – je nach Rendering-Mode.
3. **Blazor Server vs Browser**: Blazor Server rendert auf dem Server. Der SignalR-Client in Home.razor läuft also im Server-Prozess. Die Verbindung zu BotService geht von Webfrontend → BotService. Service Discovery sollte von dort aus funktionieren.
4. **CORS**: BotService hat CORS mit AllowAnyOrigin. Sollte passen.
5. **Message Size**: Hub sendet große Base64-Strings (~100KB–500KB+ pro Frame). Server: `MaximumReceiveMessageSize = 10MB`. Client: `((dynamic)hubConnection).MaximumReceiveMessageSize = 10MB`.
6. **Framerate**: 100ms Delay ≈ 10 FPS. Kann angepasst werden.

## Prerequisites

- Aspire mit `dotnet run --project LastZBot.AppHost --launch-profile local`
- Binder für Redroid (siehe README)
- ADB verbunden: Im Dashboard "Connection Status" sollte "ADB: Connected" stehen
- BotService muss erreichbar sein (Health Check `/api/status`)

## Erfolgskriterium

- Im Dashboard unter "Live View" erscheint eine nahezu Live-Ansicht des Android-Bildschirms
- Keine Fehlermeldung "Failed to connect to live stream"
- Kein permanenter Spinner "Waiting for live stream..."
- Console / Logs: Keine SignalR-Verbindungsfehler

## Relevante Dateien

| Datei | Rolle |
|-------|--------|
| `LastZBot.BotService/Device/FrameBroadcaster.cs` | Sendet Frames an alle Clients |
| `LastZBot.BotService/Device/FrameHub.cs` | SignalR Hub |
| `LastZBot.BotService/Program.cs` | MapHub, CORS, SignalR-Options |
| `LastZBot.Web/Components/Pages/Home.razor` | HubConnection, ReceiveFrame, Live-View UI |
| `LastZBot.Web/VisionApiClient.cs` | GetBaseAddressAsync für BotService-URL |
| `LastZBot.Web/Program.cs` | HttpClient BaseAddress für VisionApiClient |

## Debugging

- BotService-Logs: `_logger.LogInformation("Broadcasting frame {Size} bytes", ...)` alle 10 Frames
- FrameHub: `OnConnectedAsync` / `OnDisconnectedAsync` zeigen Client-Verbindungen
- Wenn `liveFrameData` nie gesetzt wird: Entweder kein Frame-Empfang oder SignalR nicht verbunden
- Browser DevTools: Network → WS (WebSocket) für die SignalR-Verbindung prüfen
