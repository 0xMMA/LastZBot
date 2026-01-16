namespace LastZBot.BotService.Data;

public class ActionLog
{
    public int Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string MethodUsed { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int DurationMs { get; set; }
    public decimal CostUsd { get; set; }
    public string? UiSignature { get; set; }
    public string? ErrorType { get; set; }
    public DateTime CreatedAt { get; set; }
}
