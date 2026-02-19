namespace Aiden.TrayMonitor.Infrastructure;

public sealed class VmOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8428";
    public string OtlpEndpoint { get; set; } = "http://127.0.0.1:8428/opentelemetry";
    public int PollSeconds { get; set; } = 5;
}
