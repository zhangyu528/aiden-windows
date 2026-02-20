using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Aiden.RuntimeAgent.Infrastructure;

public sealed class RuntimeAgentWorker : BackgroundService
{
    private readonly RuntimeSupervisor _supervisor;
    private readonly AgentOptions _agentOptions;

    public RuntimeAgentWorker(RuntimeSupervisor supervisor, IOptions<AgentOptions> agentOptions)
    {
        _supervisor = supervisor;
        _agentOptions = agentOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_agentOptions.Enabled)
        {
            return;
        }

        await _supervisor.RunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _supervisor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
