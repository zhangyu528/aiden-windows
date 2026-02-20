namespace Aiden.TrayMonitor.Infrastructure;

public sealed class PricingOptions
{
    public double DefaultInputPerMillionUsd { get; set; } = 0.075;
    public double DefaultOutputPerMillionUsd { get; set; } = 0.30;
    public Dictionary<string, ModelRate> ModelRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ModelRate
{
    public double InputPerMillionUsd { get; set; } = 0.075;
    public double OutputPerMillionUsd { get; set; } = 0.30;
}
