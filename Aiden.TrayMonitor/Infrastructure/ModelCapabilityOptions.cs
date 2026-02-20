namespace Aiden.TrayMonitor.Infrastructure;

public sealed class ModelCapabilityOptions
{
    public Dictionary<string, int> ModelContextWindowTokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
