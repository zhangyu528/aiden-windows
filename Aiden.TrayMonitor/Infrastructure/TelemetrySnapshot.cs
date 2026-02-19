namespace Aiden.TrayMonitor.Infrastructure;

public sealed class TelemetrySnapshot
{
    public double InputTokens { get; init; }
    public double OutputTokens { get; init; }
    public double SessionCostUsd { get; init; }
    public double ContextWindowM { get; init; }
    public bool Online { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}
