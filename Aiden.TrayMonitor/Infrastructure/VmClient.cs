using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class VmClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VmOptions _options;

    public VmClient(IHttpClientFactory httpClientFactory, IOptions<VmOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<TelemetrySnapshot> QuerySnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var inputTask = QueryScalarAsync("sum(gen_ai.client.token.usage_sum{gen_ai.token.type=\"input\"})", cancellationToken);
            var outputTask = QueryScalarAsync("sum(gen_ai.client.token.usage_sum{gen_ai.token.type=\"output\"})", cancellationToken);

            await Task.WhenAll(inputTask, outputTask);

            return new TelemetrySnapshot
            {
                InputTokens = inputTask.Result,
                OutputTokens = outputTask.Result,
                SessionCostUsd = 0,
                ContextWindowM = 0,
                Online = true,
                UpdatedAt = DateTimeOffset.Now
            };
        }
        catch
        {
            return new TelemetrySnapshot { Online = false, UpdatedAt = DateTimeOffset.Now };
        }
    }

    private async Task<double> QueryScalarAsync(string query, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        var queryBase = ResolveUrl(_options.BaseUrl, _options.QueryEndpoint, "/api/v1/query").TrimEnd('/');
        var url = $"{queryBase}?query={Uri.EscapeDataString(query)}";

        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        var status = root.GetProperty("status").GetString();
        if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var result = root.GetProperty("data").GetProperty("result");
        if (result.GetArrayLength() == 0)
        {
            return 0;
        }

        var valueArray = result[0].GetProperty("value");
        var valueString = valueArray[1].GetString() ?? "0";

        return double.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
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
}
