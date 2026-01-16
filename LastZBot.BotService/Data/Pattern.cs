using System.Text.Json;

namespace LastZBot.BotService.Data;

public class Pattern
{
    public int Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public JsonDocument PatternData { get; set; } = null!;
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastSuccess { get; set; }
    public string? UiSignature { get; set; }
}
