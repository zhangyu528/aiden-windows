using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Aiden.TrayMonitor.Infrastructure;

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

    private async Task<bool> IsCollectorHealthyAsync(CancellationToken cancellationToken)
    {
        // First, try health_check extension endpoint.
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
            // Ignore and fallback to port check.
        }

        // Fallback: check gRPC listening port.
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
        var vmMetricsEndpoint = ResolveUrl(_vmOptions.BaseUrl, _vmOptions.OtlpEndpoint, "/opentelemetry", _vmOptions.Port);
        var collectorListenHost = FormatListenHost(ExtractHost(_options.BaseUrl));
        var yaml = BuildLegacyConfig(collectorListenHost, vmMetricsEndpoint);

        File.WriteAllText(configPath, yaml);
        return configPath;
    }

    private string BuildLegacyConfig(string collectorListenHost, string vmMetricsEndpoint)
    {
        return $"""
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
    }

    private string BuildCodexSpanMetricsConfig(string collectorListenHost, string vmMetricsEndpoint)
    {
        return $"""
        receivers:
          otlp:
            protocols:
              grpc:
                endpoint: {collectorListenHost}:{_options.GrpcPort}
              http:
                endpoint: {collectorListenHost}:{_options.HttpPort}

        processors:
          filter/codex:
            error_mode: ignore
            traces:
              span:
                - resource.attributes["service.name"] == "gemini-cli"
          batch:

        connectors:
          spanmetrics/codex:
            namespace: codex
            metrics_flush_interval: 15s
            dimensions:
              - name: user.email
              - name: session.id
              - name: gen_ai.request.model
            resource_metrics_key_attributes:
              - service.name
              - telemetry.sdk.language
              - telemetry.sdk.name
            aggregation_temporality: AGGREGATION_TEMPORALITY_CUMULATIVE

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
            metrics/codex_converted:
              receivers: [spanmetrics/codex]
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
            traces/codex_convert:
              receivers: [otlp]
              processors: [filter/codex, batch]
              exporters: [spanmetrics/codex]
        """;
    }

    private string BuildCodexSpanAndLogMetricsConfig(string collectorListenHost, string vmMetricsEndpoint)
    {
        return $"""
        receivers:
          otlp:
            protocols:
              grpc:
                endpoint: {collectorListenHost}:{_options.GrpcPort}
              http:
                endpoint: {collectorListenHost}:{_options.HttpPort}

        processors:
          filter/codex:
            error_mode: ignore
            traces:
              span:
                - resource.attributes["service.name"] == "gemini-cli"
          filter/codex_logs:
            error_mode: ignore
            logs:
              log_record:
                - resource.attributes["service.name"] == "gemini-cli"
          batch:

        connectors:
          spanmetrics/codex:
            namespace: codex
            metrics_flush_interval: 15s
            dimensions:
              - name: user.email
              - name: session.id
              - name: gen_ai.request.model
            resource_metrics_key_attributes:
              - service.name
              - telemetry.sdk.language
              - telemetry.sdk.name
            aggregation_temporality: AGGREGATION_TEMPORALITY_CUMULATIVE
          count/codex_logs:
            logs:
              codex_log_records_total:
                description: Count of Codex log records
                attributes:
                  - key: service.name
                  - key: user.email
                  - key: session.id
                  - key: gen_ai.request.model

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
            metrics/codex_span_converted:
              receivers: [spanmetrics/codex]
              processors: [batch]
              exporters: [otlphttp/vm]
            metrics/codex_logs_converted:
              receivers: [count/codex_logs]
              processors: [batch]
              exporters: [otlphttp/vm]
            logs:
              receivers: [otlp]
              processors: [batch]
              exporters: [debug]
            logs/codex_convert:
              receivers: [otlp]
              processors: [filter/codex_logs, batch]
              exporters: [count/codex_logs]
            traces:
              receivers: [otlp]
              processors: [batch]
              exporters: [debug]
            traces/codex_convert:
              receivers: [otlp]
              processors: [filter/codex, batch]
              exporters: [spanmetrics/codex]
        """;
    }

    private string BuildCodexCountConfig(string collectorListenHost, string vmMetricsEndpoint)
    {
        return $"""
        receivers:
          otlp:
            protocols:
              grpc:
                endpoint: {collectorListenHost}:{_options.GrpcPort}
              http:
                endpoint: {collectorListenHost}:{_options.HttpPort}

        processors:
          batch:

        connectors:
          count/codex:
            spans:
              codex_span_records_total:
                description: Count of Codex spans
                attributes:
                  - key: service.name
                  - key: user.email
                  - key: session.id
                  - key: gen_ai.request.model
            logs:
              codex_log_records_total:
                description: Count of Codex log records
                attributes:
                  - key: service.name
                  - key: user.email
                  - key: session.id
                  - key: gen_ai.request.model

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
            logs/codex_convert:
              receivers: [otlp]
              processors: [batch]
              exporters: [count/codex]
            metrics/codex_converted:
              receivers: [count/codex]
              processors: [batch]
              exporters: [otlphttp/vm]
            traces:
              receivers: [otlp]
              processors: [batch]
              exporters: [debug, count/codex]
        """;
    }

    private static bool SupportsConnector(string exePath, string connectorName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "components",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return output.IndexOf($"- name: {connectorName}", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
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
        var hasScheme = url.Contains("://", StringComparison.Ordinal);
        if (!hasScheme)
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
        var roots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

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

                var exes = Directory.GetFiles(runtimeCollector, "otelcol*.exe", SearchOption.AllDirectories);
                var selected = exes
                    .Select(path => new FileInfo(path))
                    .Where(f => f.Name.Equals("otelcol.exe", StringComparison.OrdinalIgnoreCase))
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
