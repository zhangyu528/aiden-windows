namespace Aiden.TrayMonitor.Infrastructure;

public sealed class TelemetrySnapshot
{
    public double InputTokens { get; init; }
    public double OutputTokens { get; init; }
    public string InputText { get; init; } = "N/A";
    public string OutputText { get; init; } = "N/A";
    public string CurrentUserEmail { get; init; } = "Unknown";
    public string UserActiveAtText { get; init; } = "N/A";
    public double SessionCostUsd { get; init; }
    public double ContextWindowM { get; init; }
    public double ContextPercent { get; init; }
    public string ContextText { get; init; } = "N/A";
    public bool Online { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}
