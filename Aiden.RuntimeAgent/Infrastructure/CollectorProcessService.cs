using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Aiden.RuntimeAgent.Infrastructure;

public sealed class CollectorProcessService
{
    private readonly CollectorOptions _options;
    private readonly VmOptions _vmOptions;
    private Process? _ownedProcess;

    public CollectorProcessService(IOptions<CollectorOptions> options, IOptions<VmOptions> vmOptions)
    {
        _options = options.Value;
        _vmOptions = vmOptions.Value;
    }

    public async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (await IsCollectorHealthyAsync(cancellationToken))
        {
            return;
        }

        var exePath = FindCollectorExecutablePath();
        if (exePath is null)
        {
            return;
        }

        var configPath = BuildCollectorConfig(exePath);
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--config \"{configPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath)!
        };

        _ownedProcess = Process.Start(startInfo);
    }

    public void StopIfOwned()
    {
        try
        {
            if (_ownedProcess is { HasExited: false })
            {
                _ownedProcess.Kill(entireProcessTree: true);
                _ownedProcess.WaitForExit(2000);
            }
        }
        catch
        {
            // Ignore shutdown errors.
        }
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken) => IsCollectorHealthyAsync(cancellationToken);

    private async Task<bool> IsCollectorHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
            var healthUrl = ResolveUrl(_options.BaseUrl, "/", "/", _options.HealthPort);
            using var response = await client.GetAsync(healthUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
        }
        catch
        {
            // Ignore and fallback.
        }

        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(ExtractHost(_options.BaseUrl), _options.GrpcPort);
            var completed = await Task.WhenAny(connectTask, Task.Delay(1000, cancellationToken));
            return completed == connectTask && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    private string BuildCollectorConfig(string exePath)
    {
        var collectorDir = Path.GetDirectoryName(exePath)!;
        var configDir = Path.Combine(collectorDir, "config");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "otelcol-vm.yaml");
        var vmMetricsEndpoint = ResolveUrl(_vmOptions.BaseUrl, "/opentelemetry", "/opentelemetry", _vmOptions.Port);
        var collectorListenHost = FormatListenHost(ExtractHost(_options.BaseUrl));

        var yaml = $"""
        receivers:
          otlp:
            protocols:
              grpc:
                endpoint: {collectorListenHost}:{_options.GrpcPort}
              http:
                endpoint: {collectorListenHost}:{_options.HttpPort}

        processors:
          batch:

        exporters:
          otlphttp/vm:
            endpoint: {vmMetricsEndpoint}
          debug:
            verbosity: basic

        extensions:
          health_check:
            endpoint: {collectorListenHost}:{_options.HealthPort}

        service:
          extensions: [health_check]
          pipelines:
            metrics:
              receivers: [otlp]
              processors: [batch]
              exporters: [otlphttp/vm]
            logs:
              receivers: [otlp]
              processors: [batch]
              exporters: [debug]
            traces:
              receivers: [otlp]
              processors: [batch]
              exporters: [debug]
        """;

        File.WriteAllText(configPath, yaml);
        return configPath;
    }

    private static string ResolveUrl(string baseUrl, string? endpointOrPath, string defaultPath, int? port)
    {
        var normalizedBase = NormalizeBaseUrl(baseUrl, port);
        if (!string.IsNullOrWhiteSpace(endpointOrPath) && endpointOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return endpointOrPath;
        }

        var path = string.IsNullOrWhiteSpace(endpointOrPath) ? defaultPath : endpointOrPath;
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return $"{normalizedBase}{path}";
    }

    private static string NormalizeBaseUrl(string baseUrl, int? port)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1" : baseUrl.Trim();
        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "http://" + url;
        }

        var builder = new UriBuilder(url);
        var authority = builder.Uri.GetLeftPart(UriPartial.Authority);
        var hasExplicitPort = Regex.IsMatch(authority, @":\d+$");
        if (!hasExplicitPort && port is > 0)
        {
            builder.Port = port.Value;
        }

        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string ExtractHost(string baseUrl)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1" : baseUrl.Trim();
        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "http://" + url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return "127.0.0.1";
    }

    private static string FormatListenHost(string host)
    {
        return host.Contains(':', StringComparison.Ordinal) && !host.StartsWith("[", StringComparison.Ordinal)
            ? $"[{host}]"
            : host;
    }

    private static string? FindCollectorExecutablePath()
    {
        var roots = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var root in roots)
        {
            var dir = new DirectoryInfo(root);
            for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            {
                var runtimeCollector = Path.Combine(dir.FullName, "runtime", "collector");
                if (!Directory.Exists(runtimeCollector))
                {
                    continue;
                }

                var selected = Directory.GetFiles(runtimeCollector, "otelcol*.exe", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (selected is not null)
                {
                    return selected.FullName;
                }
            }
        }

        return null;
    }
}
