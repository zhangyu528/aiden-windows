using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class VmProcessService
{
    private readonly VmOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private Process? _ownedProcess;

    public VmProcessService(IOptions<VmOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (await IsVmHealthyAsync(cancellationToken))
        {
            return;
        }

        var exePath = FindVmExecutablePath();
        if (exePath is null)
        {
            return;
        }

        var dataPath = Path.Combine(Path.GetDirectoryName(exePath)!, "..", "..", "data", "vm-data");
        dataPath = Path.GetFullPath(dataPath);
        Directory.CreateDirectory(dataPath);

        var port = _options.Port > 0 ? _options.Port : ParsePortOrDefault(_options.BaseUrl, 8428);

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"-httpListenAddr=:{port} -storageDataPath=\"{dataPath}\"",
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

    private async Task<bool> IsVmHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            var healthUrl = ResolveUrl(_options.BaseUrl, _options.HealthEndpoint, "/health");
            using var response = await client.GetAsync(healthUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static int ParsePortOrDefault(string url, int defaultPort)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Port : defaultPort;
    }

    private string ResolveUrl(string baseUrl, string? endpointOrPath, string defaultPath)
    {
        var normalizedBase = NormalizeBaseUrl(baseUrl, _options.Port);

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

    private string NormalizeBaseUrl(string baseUrl, int? overridePort)
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

        var targetPort = overridePort ?? _options.Port;
        if (!hasExplicitPort && targetPort > 0)
        {
            builder.Port = targetPort;
        }

        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string? FindVmExecutablePath()
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
                var runtimeVm = Path.Combine(dir.FullName, "runtime", "vm");
                if (!Directory.Exists(runtimeVm))
                {
                    continue;
                }

                var exes = Directory.GetFiles(runtimeVm, "victoria-metrics.exe", SearchOption.AllDirectories);
                var selected = exes
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
