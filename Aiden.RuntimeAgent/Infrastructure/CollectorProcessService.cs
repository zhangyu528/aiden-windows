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
        var fileExportPath = ResolveFileLogExportPath(collectorDir);
        var fileExportDirectory = Path.GetDirectoryName(fileExportPath);
        if (!string.IsNullOrWhiteSpace(fileExportDirectory))
        {
            Directory.CreateDirectory(fileExportDirectory);
        }

        var configPath = Path.Combine(configDir, "otelcol-vm.yaml");
        var vmMetricsEndpoint = ResolveUrl(_vmOptions.BaseUrl, _vmOptions.OtlpEndpoint, "/opentelemetry", _vmOptions.Port);
        var collectorListenHost = FormatListenHost(ExtractHost(_options.BaseUrl));
        var yaml = BuildLegacyConfig(collectorListenHost, vmMetricsEndpoint, fileExportPath);

        File.WriteAllText(configPath, yaml);
        return configPath;
    }

    private string BuildLegacyConfig(string collectorListenHost, string vmMetricsEndpoint, string fileLogExportPath)
    {
        var fileLogExportFormat = ResolveFileLogExportFormat();
        var logsExporters = BuildLogsExportersList();

        return $"""
        receivers:
          otlp:
            protocols:
              grpc:
                endpoint: {collectorListenHost}:{_options.GrpcPort}
              http:
                endpoint: {collectorListenHost}:{_options.HttpPort}

        processors:
          filter/codex_completed:
            error_mode: ignore
            logs:
              log_record:
                - log.attributes["event.kind"] != "response.completed" or log.attributes["input_token_count"] == nil or log.attributes["output_token_count"] == nil
          attributes/codex_enrich:
            actions:
              - key: gen_ai.request.model
                from_attribute: slug
                action: upsert
              - key: gen_ai.request.model
                from_attribute: model
                action: upsert
              - key: session.id
                from_attribute: conversation.id
                action: upsert
              - key: input_token_count
                action: convert
                converted_type: int
              - key: output_token_count
                action: convert
                converted_type: int
          transform/codex_resource_dims:
            error_mode: ignore
            log_statements:
              - context: log
                statements:
                  - set(resource.attributes["service.name"], "codex-cli")
                  - set(resource.attributes["user.email"], log.attributes["user.email"]) where log.attributes["user.email"] != nil
                  - set(resource.attributes["session.id"], log.attributes["session.id"]) where log.attributes["session.id"] != nil
                  - set(resource.attributes["gen_ai.request.model"], log.attributes["gen_ai.request.model"]) where log.attributes["gen_ai.request.model"] != nil
          deltatocumulative:
          metricstarttime:
          batch:

        connectors:
          sum/codex_input:
            logs:
              gen_ai.client.token.usage_sum:
                source_attribute: input_token_count
                attributes:
                  - key: gen_ai.token.type
                    default_value: input
          sum/codex_output:
            logs:
              gen_ai.client.token.usage_sum:
                source_attribute: output_token_count
                attributes:
                  - key: gen_ai.token.type
                    default_value: output
        exporters:
          otlphttp/vm:
            endpoint: {vmMetricsEndpoint}
          debug:
            verbosity: basic
          file/otlp_logs:
            path: '{EscapeYamlSingleQuoted(fileLogExportPath)}'
            format: {fileLogExportFormat}

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
            metrics/codex_input:
              receivers: [sum/codex_input]
              processors: [deltatocumulative, metricstarttime, batch]
              exporters: [otlphttp/vm]
            metrics/codex_output:
              receivers: [sum/codex_output]
              processors: [deltatocumulative, metricstarttime, batch]
              exporters: [otlphttp/vm]
            logs:
              receivers: [otlp]
              processors: [batch]
              exporters: [{logsExporters}]
            logs/codex_token_convert:
              receivers: [otlp]
              processors: [filter/codex_completed, attributes/codex_enrich, transform/codex_resource_dims, batch]
              exporters: [sum/codex_input, sum/codex_output]
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
                var candidates = new[]
                {
                    runtimeCollector,
                    Path.Combine(dir.FullName, "Aiden.TrayMonitor", "runtime", "collector")
                };

                foreach (var candidate in candidates)
                {
                    if (!Directory.Exists(candidate))
                    {
                        continue;
                    }

                    var selected = Directory.GetFiles(candidate, "otelcol-contrib*.exe", SearchOption.AllDirectories)
                        .Select(path => new FileInfo(path))
                        .Where(f => f.Name.Equals("otelcol-contrib.exe", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .FirstOrDefault();
                    if (selected is not null)
                    {
                        return selected.FullName;
                    }
                }
            }
        }

        return null;
    }

    private string ResolveFileLogExportPath(string collectorDir)
    {
        if (!string.IsNullOrWhiteSpace(_options.FileLogExportPath))
        {
            return _options.FileLogExportPath.Trim();
        }

        return Path.Combine(collectorDir, "logs", "otlp-logs.jsonl");
    }

    private string ResolveFileLogExportFormat()
    {
        var configured = _options.FileLogExportFormat?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            return "json";
        }

        return string.Equals(configured, "proto", StringComparison.OrdinalIgnoreCase)
            ? "proto"
            : "json";
    }

    private string BuildLogsExportersList()
    {
        if (!_options.EnableFileLogExport)
        {
            return "debug";
        }

        return "debug, file/otlp_logs";
    }

    private static string EscapeYamlSingleQuoted(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
