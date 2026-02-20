using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Aiden.RuntimeAgent.Infrastructure;

public sealed class RuntimeSupervisor : IAsyncDisposable
{
    private readonly VmProcessService _vmProcessService;
    private readonly CollectorProcessService _collectorProcessService;
    private readonly AgentOptions _agentOptions;
    private readonly object _stateLock = new();
    private readonly HttpListener _listener = new();
    private bool _restartRequested;
    private string _lastError = string.Empty;
    private DateTimeOffset _lastCheckedAt = DateTimeOffset.MinValue;
    private bool _vmHealthy;
    private bool _collectorHealthy;

    public RuntimeSupervisor(
        VmProcessService vmProcessService,
        CollectorProcessService collectorProcessService,
        IOptions<AgentOptions> agentOptions)
    {
        _vmProcessService = vmProcessService;
        _collectorProcessService = collectorProcessService;
        _agentOptions = agentOptions.Value;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var backoffSeconds = Math.Max(1, _agentOptions.BackoffMinSeconds);
        var maxBackoffSeconds = Math.Max(backoffSeconds, _agentOptions.BackoffMaxSeconds);
        StartControlEndpoint();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var forceRestart = ConsumeRestartRequest();
                if (forceRestart)
                {
                    _collectorProcessService.StopIfOwned();
                    _vmProcessService.StopIfOwned();
                }

                await _vmProcessService.EnsureStartedAsync(cancellationToken);
                await _collectorProcessService.EnsureStartedAsync(cancellationToken);

                var vmHealthy = await _vmProcessService.IsHealthyAsync(cancellationToken);
                var collectorHealthy = await _collectorProcessService.IsHealthyAsync(cancellationToken);
                UpdateStatus(vmHealthy, collectorHealthy, string.Empty);

                if (vmHealthy && collectorHealthy)
                {
                    backoffSeconds = Math.Max(1, _agentOptions.BackoffMinSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _agentOptions.HealthCheckSeconds)), cancellationToken);
                    continue;
                }

                UpdateStatus(vmHealthy, collectorHealthy, "runtime unhealthy, retrying");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                UpdateStatus(false, false, ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken);
            backoffSeconds = Math.Min(maxBackoffSeconds, backoffSeconds * 2);
        }
    }

    private void StartControlEndpoint()
    {
        if (_listener.IsListening)
        {
            return;
        }

        var port = _agentOptions.StatusPort > 0 ? _agentOptions.StatusPort : 18731;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();

        _ = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                HttpListenerContext? ctx = null;
                try
                {
                    ctx = await _listener.GetContextAsync();
                    await HandleRequestAsync(ctx);
                }
                catch
                {
                    if (ctx is not null)
                    {
                        TryClose(ctx.Response);
                    }
                }
            }
        });
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "/";
        if (path.Length == 0)
        {
            path = "/";
        }

        if (path.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.OK, BuildStatusDto());
            return;
        }

        if (path.Equals("/status", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context.Response, HttpStatusCode.OK, BuildStatusDto());
            return;
        }

        if (path.Equals("/restart", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            lock (_stateLock)
            {
                _restartRequested = true;
            }

            await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { ok = true, message = "restart requested" });
            return;
        }

        await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { ok = false, message = "not found" });
    }

    private object BuildStatusDto()
    {
        lock (_stateLock)
        {
            return new
            {
                ok = true,
                vmHealthy = _vmHealthy,
                collectorHealthy = _collectorHealthy,
                lastError = _lastError,
                lastCheckedAt = _lastCheckedAt
            };
        }
    }

    private void UpdateStatus(bool vmHealthy, bool collectorHealthy, string error)
    {
        lock (_stateLock)
        {
            _vmHealthy = vmHealthy;
            _collectorHealthy = collectorHealthy;
            _lastCheckedAt = DateTimeOffset.Now;
            _lastError = error;
        }
    }

    private bool ConsumeRestartRequest()
    {
        lock (_stateLock)
        {
            if (!_restartRequested)
            {
                return false;
            }

            _restartRequested = false;
            return true;
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode code, object payload)
    {
        response.StatusCode = (int)code;
        response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        TryClose(response);
    }

    private static void TryClose(HttpListenerResponse response)
    {
        try
        {
            response.OutputStream.Close();
            response.Close();
        }
        catch
        {
            // ignore
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Close();
        }
        catch
        {
            // ignore
        }

        return ValueTask.CompletedTask;
    }
}
