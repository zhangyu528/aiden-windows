using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class CliProvisioningService
{
    private readonly CollectorOptions _collectorOptions;

    public CliProvisioningService(IOptions<CollectorOptions> collectorOptions)
    {
        _collectorOptions = collectorOptions.Value;
    }

    public async Task<IReadOnlyList<CliProvisioningState>> GetStatesAsync(CancellationToken cancellationToken)
    {
        var result = new List<CliProvisioningState>(3);
        foreach (var provider in Enum.GetValues<CliProviderType>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetConfigPath(provider);
            var commandAvailable = await CheckCommandAvailableAsync(provider, cancellationToken);
            var configFileExists = File.Exists(path);
            var installed = commandAvailable;
            var enabled = installed && configFileExists && await ReadEnabledAsync(provider, path, cancellationToken);

            result.Add(new CliProvisioningState
            {
                Provider = provider,
                DisplayName = GetDisplayName(provider),
                IsInstalled = installed,
                IsEnabled = enabled,
                InstallHint = GetInstallHint(provider),
                ConfigPath = path
            });
        }

        return result;
    }

    public async Task<bool> SetEnabledAsync(CliProviderType provider, bool enabled, CancellationToken cancellationToken)
    {
        if (!await CheckCommandAvailableAsync(provider, cancellationToken))
        {
            return false;
        }

        var path = GetConfigPath(provider);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return provider switch
        {
            CliProviderType.Gemini => await WriteGeminiSettingsAsync(path, enabled, cancellationToken),
            CliProviderType.Claude => await WriteClaudeSettingsAsync(path, enabled, cancellationToken),
            CliProviderType.Codex => await WriteCodexTomlAsync(path, enabled, cancellationToken),
            _ => false
        };
    }

    public async Task<bool> HasAnyEnabledAsync(CancellationToken cancellationToken)
    {
        var states = await GetStatesAsync(cancellationToken);
        return states.Any(s => s.IsInstalled && s.IsEnabled);
    }

    private static string GetDisplayName(CliProviderType provider) => provider switch
    {
        CliProviderType.Gemini => "Gemini CLI",
        CliProviderType.Codex => "Codex CLI",
        CliProviderType.Claude => "Claude Code CLI",
        _ => provider.ToString()
    };

    private static string GetConfigPath(CliProviderType provider)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return provider switch
        {
            CliProviderType.Gemini => Path.Combine(home, ".gemini", "settings.json"),
            CliProviderType.Codex => Path.Combine(home, ".codex", "config.toml"),
            CliProviderType.Claude => Path.Combine(home, ".claude", "settings.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    private static string GetInstallHint(CliProviderType provider) => provider switch
    {
        CliProviderType.Gemini => "Install: npm install -g @google/gemini-cli",
        CliProviderType.Codex => "Install: npm install -g @openai/codex",
        CliProviderType.Claude => "Install: npm install -g @anthropic-ai/claude-code",
        _ => string.Empty
    };

    private static async Task<bool> CheckCommandAvailableAsync(CliProviderType provider, CancellationToken cancellationToken)
    {
        var command = provider switch
        {
            CliProviderType.Gemini => "gemini",
            CliProviderType.Codex => "codex",
            CliProviderType.Claude => "claude",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = "where.exe",
            Arguments = command,
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

        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0;
    }

    private static async Task<bool> ReadEnabledAsync(CliProviderType provider, string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return provider switch
        {
            CliProviderType.Gemini => await ReadJsonEnabledAsync(path, cancellationToken),
            CliProviderType.Claude => await ReadClaudeEnabledAsync(path, cancellationToken),
            CliProviderType.Codex => await ReadCodexEnabledAsync(path, cancellationToken),
            _ => false
        };
    }

    private static async Task<bool> ReadJsonEnabledAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
            return node?["telemetry"]?["enabled"]?.GetValue<bool>() == true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ReadClaudeEnabledAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
            var value = node?["env"]?["CLAUDE_CODE_ENABLE_TELEMETRY"]?.GetValue<string>();
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ReadCodexEnabledAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            var section = GetTomlSection(text, "otel");
            if (string.IsNullOrWhiteSpace(section))
            {
                return false;
            }

            var exporterValue = TryReadTomlKeyInSection(section, "exporter");
            var traceExporterValue = TryReadTomlKeyInSection(section, "trace_exporter");

            static bool IsEnabledValue(string? value) =>
                !string.IsNullOrWhiteSpace(value) &&
                !value.Contains("\"none\"", StringComparison.OrdinalIgnoreCase) &&
                !value.Equals("none", StringComparison.OrdinalIgnoreCase);

            return IsEnabledValue(exporterValue) ||
                   IsEnabledValue(traceExporterValue);
        }
        catch
        {
            // ignored
        }

        return false;
    }

    private async Task<bool> WriteGeminiSettingsAsync(
        string path,
        bool enabled,
        CancellationToken cancellationToken)
    {
        JsonNode root;
        if (File.Exists(path))
        {
            try
            {
                await using var input = File.OpenRead(path);
                root = await JsonNode.ParseAsync(input, cancellationToken: cancellationToken) ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        var telemetry = root["telemetry"] as JsonObject ?? new JsonObject();
        telemetry["enabled"] = enabled;
        telemetry["target"] = "local";
        telemetry["useCollector"] = true;
        telemetry["otlpProtocol"] = "grpc";
        telemetry["otlpEndpoint"] = BuildCollectorGrpcEndpoint();
        telemetry["logPrompts"] = false;
        root["telemetry"] = telemetry;

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(path, root.ToJsonString(options), cancellationToken);
        return true;
    }

    private async Task<bool> WriteCodexTomlAsync(string path, bool enabled, CancellationToken cancellationToken)
    {
        var content = File.Exists(path)
            ? await File.ReadAllTextAsync(path, cancellationToken)
            : string.Empty;

        content = UpsertTomlKeyInSection(content, "otel", "environment", "\"dev\"");
        content = UpsertTomlKeyInSection(content, "otel", "log_user_prompt", "false");
        if (enabled)
        {
            var endpoint = BuildCollectorGrpcEndpoint();
            content = UpsertTomlKeyInSection(content, "otel", "exporter", $"{{ otlp-grpc = {{ endpoint = \"{endpoint}\" }} }}");
            content = UpsertTomlKeyInSection(content, "otel", "trace_exporter", $"{{ otlp-grpc = {{ endpoint = \"{endpoint}\" }} }}");
        }
        else
        {
            content = UpsertTomlKeyInSection(content, "otel", "exporter", "\"none\"");
            content = UpsertTomlKeyInSection(content, "otel", "trace_exporter", "\"none\"");
        }

        await File.WriteAllTextAsync(path, content.TrimEnd() + Environment.NewLine, new UTF8Encoding(false), cancellationToken);
        return true;
    }

    private async Task<bool> WriteClaudeSettingsAsync(string path, bool enabled, CancellationToken cancellationToken)
    {
        JsonNode root;
        if (File.Exists(path))
        {
            try
            {
                await using var input = File.OpenRead(path);
                root = await JsonNode.ParseAsync(input, cancellationToken: cancellationToken) ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        var env = root["env"] as JsonObject ?? new JsonObject();
        env["CLAUDE_CODE_ENABLE_TELEMETRY"] = enabled ? "1" : "0";
        env["OTEL_METRICS_EXPORTER"] = enabled ? "otlp" : "none";
        env["OTEL_LOGS_EXPORTER"] = enabled ? "otlp" : "none";
        env["OTEL_TRACES_EXPORTER"] = enabled ? "otlp" : "none";
        env["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc";
        env["OTEL_EXPORTER_OTLP_ENDPOINT"] = BuildCollectorGrpcEndpoint();
        root["env"] = env;

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(path, root.ToJsonString(options), cancellationToken);
        return true;
    }

    private string BuildCollectorGrpcEndpoint()
    {
        var url = string.IsNullOrWhiteSpace(_collectorOptions.BaseUrl)
            ? "http://127.0.0.1"
            : _collectorOptions.BaseUrl.Trim();

        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "http://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"http://127.0.0.1:{_collectorOptions.GrpcPort}";
        }

        var builder = new UriBuilder(uri);
        var authority = uri.GetLeftPart(UriPartial.Authority);
        var hasExplicitPort = Regex.IsMatch(authority, @":\d+$");
        if (!hasExplicitPort)
        {
            builder.Port = _collectorOptions.GrpcPort;
        }

        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string GetTomlSection(string source, string sectionName)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var targetHeader = $"[{sectionName}]";
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals(targetHeader, StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }

        if (start < 0)
        {
            return string.Empty;
        }

        var end = lines.Length;
        for (var i = start; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                end = i;
                break;
            }
        }

        return string.Join('\n', lines[start..end]);
    }

    private static string? TryReadTomlKeyInSection(string sectionBody, string key)
    {
        var lines = sectionBody.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('='))
            {
                continue;
            }

            var idx = trimmed.IndexOf('=');
            var left = trimmed[..idx].Trim();
            if (!left.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return trimmed[(idx + 1)..].Trim();
        }

        return null;
    }

    private static string UpsertTomlKeyInSection(string source, string sectionName, string key, string value)
    {
        var lines = source.Length == 0
            ? new List<string>()
            : source.Replace("\r\n", "\n").Split('\n').ToList();

        var header = $"[{sectionName}]";
        var sectionStart = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim().Equals(header, StringComparison.OrdinalIgnoreCase))
            {
                sectionStart = i;
                break;
            }
        }

        if (sectionStart < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add(header);
            lines.Add($"{key} = {value}");
            return string.Join(Environment.NewLine, lines);
        }

        var sectionEnd = lines.Count;
        for (var i = sectionStart + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                sectionEnd = i;
                break;
            }
        }

        for (var i = sectionStart + 1; i < sectionEnd; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('#') || !trimmed.Contains('='))
            {
                continue;
            }

            var left = trimmed[..trimmed.IndexOf('=')].Trim();
            if (!left.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lines[i] = $"{key} = {value}";
            return string.Join(Environment.NewLine, lines);
        }

        lines.Insert(sectionEnd, $"{key} = {value}");
        return string.Join(Environment.NewLine, lines);
    }
}
