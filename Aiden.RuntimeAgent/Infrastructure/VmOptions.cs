namespace Aiden.RuntimeAgent.Infrastructure;

public sealed class VmOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8428";
    public int Port { get; set; } = 8428;
    public string HealthEndpoint { get; set; } = "http://127.0.0.1:8428/health";
}
