using System.Globalization;
using System.Net.Http;
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
            var inputTask = QueryScalarAsync("sum(ai_tokens_input_total)", cancellationToken);
            var outputTask = QueryScalarAsync("sum(ai_tokens_output_total)", cancellationToken);
            var costTask = QueryScalarAsync("sum(ai_session_cost_usd_total)", cancellationToken);
            var contextTask = QueryScalarAsync("sum(ai_context_window_usage)", cancellationToken);

            await Task.WhenAll(inputTask, outputTask, costTask, contextTask);

            return new TelemetrySnapshot
            {
                InputTokens = inputTask.Result,
                OutputTokens = outputTask.Result,
                SessionCostUsd = costTask.Result,
                ContextWindowM = contextTask.Result / 1_000_000d,
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
        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/v1/query?query={Uri.EscapeDataString(query)}";

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
}
