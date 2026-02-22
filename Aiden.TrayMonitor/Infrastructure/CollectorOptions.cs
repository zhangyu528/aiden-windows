namespace Aiden.TrayMonitor.Infrastructure;

public sealed class CollectorOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1";
    public int GrpcPort { get; set; } = 4317;
    public int HttpPort { get; set; } = 4318;
    public int HealthPort { get; set; } = 13133;
    public bool EnableFileLogExport { get; set; } = true;
    public string FileLogExportPath { get; set; } = string.Empty;
    public string FileLogExportFormat { get; set; } = "json";
}
