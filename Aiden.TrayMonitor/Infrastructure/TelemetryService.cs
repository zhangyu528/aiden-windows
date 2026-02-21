namespace Aiden.TrayMonitor.Infrastructure;

public sealed class TelemetryService
{
    private readonly VmClient _vmClient;
    private readonly VmOptions _options;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _runner;

    public event Action<TelemetrySnapshot>? SnapshotUpdated;

    public TelemetryService(VmClient vmClient, Microsoft.Extensions.Options.IOptions<VmOptions> options)
    {
        _vmClient = vmClient;
        _options = options.Value;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_runner is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _runner = RunLoopAsync(_cts.Token);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _runner = null;
        }
    }

    public async Task RefreshOnceAsync()
    {
        var snapshot = await _vmClient.QuerySnapshotAsync(CancellationToken.None);
        SnapshotUpdated?.Invoke(snapshot);
    }

    public void SetServiceNameFilter(string? serviceNameFilter)
    {
        _vmClient.SetServiceNameFilterOverride(serviceNameFilter);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds)));

        await PublishAsync(cancellationToken);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await PublishAsync(cancellationToken);
        }
    }

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _vmClient.QuerySnapshotAsync(cancellationToken);
        SnapshotUpdated?.Invoke(snapshot);
    }
}
