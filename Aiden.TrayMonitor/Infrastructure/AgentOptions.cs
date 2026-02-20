namespace Aiden.TrayMonitor.Infrastructure;

public sealed class AgentOptions
{
    public bool Enabled { get; set; } = true;
    public bool AutoStartOnLogin { get; set; } = true;
    public int HealthCheckSeconds { get; set; } = 5;
    public int BackoffMinSeconds { get; set; } = 2;
    public int BackoffMaxSeconds { get; set; } = 60;
    public int StatusPort { get; set; } = 18731;
}
