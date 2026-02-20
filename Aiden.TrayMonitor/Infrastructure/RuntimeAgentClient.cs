using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class RuntimeAgentClient
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyName = "AidenRuntimeAgent";

    private readonly AgentOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public RuntimeAgentClient(IOptions<AgentOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (_options.AutoStartOnLogin)
        {
            EnsureRunKey();
        }

        if (await IsHealthyAsync(cancellationToken))
        {
            return;
        }

        StartAgentProcess();
        await WaitForHealthyAsync(cancellationToken);
    }

    public async Task<string> GetStatusTextAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            var url = BuildLocalUrl("/status");
            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            var vmHealthy = root.TryGetProperty("vmHealthy", out var vm) && vm.GetBoolean();
            var collectorHealthy = root.TryGetProperty("collectorHealthy", out var collector) && collector.GetBoolean();
            var lastError = root.TryGetProperty("lastError", out var error) ? error.GetString() : string.Empty;
            var status = $"VM={(vmHealthy ? "OK" : "DOWN")}, Collector={(collectorHealthy ? "OK" : "DOWN")}";
            if (!string.IsNullOrWhiteSpace(lastError))
            {
                status = $"{status}, Error={lastError}";
            }

            return status;
        }
        catch (Exception ex)
        {
            return $"Agent unavailable: {ex.Message}";
        }
    }

    public async Task<bool> RestartRuntimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync(BuildLocalUrl("/restart"), content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            var response = await client.GetAsync(BuildLocalUrl("/healthz"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task WaitForHealthyAsync(CancellationToken cancellationToken)
    {
        var maxAttempts = 12;
        for (var i = 0; i < maxAttempts; i++)
        {
            if (await IsHealthyAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }
    }

    private void EnsureRunKey()
    {
        var exePath = FindAgentExecutablePath();
        if (exePath is null)
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key?.SetValue(RunKeyName, $"\"{exePath}\"");
    }

    private void StartAgentProcess()
    {
        var exePath = FindAgentExecutablePath();
        if (exePath is null)
        {
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(exePath);
        if (Process.GetProcessesByName(processName).Any())
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory
        };

        Process.Start(startInfo);
    }

    private string? FindAgentExecutablePath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "Aiden.RuntimeAgent.exe");
        if (File.Exists(local))
        {
            return local;
        }

        var roots = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var root in roots)
        {
            var dir = new DirectoryInfo(root);
            for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
            {
                var candidates = new[]
                {
                    Path.Combine(dir.FullName, "Aiden.RuntimeAgent", "bin", "Debug", "net8.0-windows", "Aiden.RuntimeAgent.exe"),
                    Path.Combine(dir.FullName, "Aiden.RuntimeAgent", "bin", "Release", "net8.0-windows", "Aiden.RuntimeAgent.exe")
                };

                var found = candidates.FirstOrDefault(File.Exists);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private string BuildLocalUrl(string path)
    {
        var port = _options.StatusPort > 0 ? _options.StatusPort : 18731;
        return $"http://127.0.0.1:{port}{path}";
    }
}
