using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class VmClient
{
    private sealed record LatestUserInfo(string Email, DateTimeOffset? ActiveAt);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VmOptions _options;
    private readonly PricingOptions _pricing;
    private readonly ModelCapabilityOptions _modelCapability;
    private readonly object _serviceNameLock = new();
    private string? _serviceNameFilterOverride;

    public VmClient(
        IHttpClientFactory httpClientFactory,
        IOptions<VmOptions> options,
        IOptions<PricingOptions> pricingOptions,
        IOptions<ModelCapabilityOptions> modelCapabilityOptions)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _pricing = pricingOptions.Value;
        _modelCapability = modelCapabilityOptions.Value;
    }

    public async Task<TelemetrySnapshot> QuerySnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var latestUser = await QueryLatestUserAsync(cancellationToken);
            var lookbackDays = ResolveLookbackDays(latestUser.ActiveAt);

            var inputTask = QueryTokenWithFallbackAsync("input", lookbackDays, cancellationToken);
            var outputTask = QueryTokenWithFallbackAsync("output", lookbackDays, cancellationToken);
            var costTask = QueryCostWithFallbackAsync(lookbackDays, cancellationToken);
            await Task.WhenAll(inputTask, outputTask, costTask);

            var context = await QueryContextForUserAsync(latestUser.Email, lookbackDays, cancellationToken);
            var isKnownUser = !string.Equals(latestUser.Email, "Unknown", StringComparison.OrdinalIgnoreCase);
            var userActiveAtText = BuildUserActiveDaysText(isKnownUser, latestUser.ActiveAt);

            return new TelemetrySnapshot
            {
                InputTokens = inputTask.Result,
                OutputTokens = outputTask.Result,
                InputText = isKnownUser ? FormatCompactTokenValue(inputTask.Result) : "N/A",
                OutputText = isKnownUser ? FormatCompactTokenValue(outputTask.Result) : "N/A",
                CurrentUserEmail = latestUser.Email,
                UserActiveAtText = userActiveAtText,
                SessionCostUsd = costTask.Result,
                ContextWindowM = context.ContextWindowM,
                ContextPercent = context.ContextPercent,
                ContextText = context.ContextText,
                Online = true,
                UpdatedAt = DateTimeOffset.Now
            };
        }
        catch
        {
            return new TelemetrySnapshot { Online = false, UpdatedAt = DateTimeOffset.Now };
        }
    }

    public void SetServiceNameFilterOverride(string? serviceNameFilter)
    {
        lock (_serviceNameLock)
        {
            _serviceNameFilterOverride = string.IsNullOrWhiteSpace(serviceNameFilter)
                ? null
                : serviceNameFilter.Trim();
        }
    }

    private async Task<double> QueryTokenWithFallbackAsync(string tokenType, int fallbackDays, CancellationToken cancellationToken)
    {
        var instant = await QueryScalarOrEmptyAsync(BuildTokenQuery(tokenType), cancellationToken);
        if (instant.HasValue)
        {
            return instant.Value;
        }

        var fallback = await QueryScalarOrEmptyAsync(BuildTokenFallbackQuery(tokenType, fallbackDays), cancellationToken);
        return fallback.HasValue ? fallback.Value : 0;
    }

    private async Task<(bool HasValue, double Value)> QueryScalarOrEmptyAsync(string query, CancellationToken cancellationToken)
    {
        var result = await QueryResultAsync(query, cancellationToken);
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            return (false, 0);
        }

        var valueArray = result[0].GetProperty("value");
        var valueString = valueArray[1].GetString() ?? "0";

        var parsed = double.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
        return (true, parsed);
    }

    private string BuildTokenQuery(string tokenType)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        return $"sum(gen_ai.client.token.usage_sum{{gen_ai.token.type=\"{tokenType}\",service.name=\"{escapedServiceName}\"}})";
    }

    private string BuildTokenFallbackQuery(string tokenType, int fallbackDays)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var days = Math.Max(1, fallbackDays);
        return $"sum(last_over_time(gen_ai.client.token.usage_sum{{gen_ai.token.type=\"{tokenType}\",service.name=\"{escapedServiceName}\"}}[{days}d]))";
    }

    private async Task<double> QueryCostWithFallbackAsync(int fallbackDays, CancellationToken cancellationToken)
    {
        var instantResult = await QueryResultAsync(BuildModelTokenQuery(), cancellationToken);
        if (instantResult.ValueKind == JsonValueKind.Array && instantResult.GetArrayLength() > 0)
        {
            return ComputeCostUsd(instantResult);
        }

        var fallbackResult = await QueryResultAsync(BuildModelTokenFallbackQuery(fallbackDays), cancellationToken);
        if (fallbackResult.ValueKind == JsonValueKind.Array && fallbackResult.GetArrayLength() > 0)
        {
            return ComputeCostUsd(fallbackResult);
        }

        return 0;
    }

    private string BuildModelTokenQuery()
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        return $"sum by (gen_ai.request.model, gen_ai.token.type) (gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\"}})";
    }

    private string BuildModelTokenFallbackQuery(int fallbackDays)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var days = Math.Max(1, fallbackDays);
        return $"sum by (gen_ai.request.model, gen_ai.token.type) (last_over_time(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\"}}[{days}d]))";
    }

    private double ComputeCostUsd(JsonElement result)
    {
        var total = 0d;
        foreach (var item in result.EnumerateArray())
        {
            if (!item.TryGetProperty("metric", out var metric))
            {
                continue;
            }

            var model = metric.TryGetProperty("gen_ai.request.model", out var modelValue)
                ? modelValue.GetString()
                : null;
            var tokenType = metric.TryGetProperty("gen_ai.token.type", out var tokenTypeValue)
                ? tokenTypeValue.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(tokenType))
            {
                continue;
            }

            if (!TryParseVectorValue(item, out var tokenCount))
            {
                continue;
            }

            var (inputPerMillion, outputPerMillion) = ResolveModelRates(model);
            var perMillion = string.Equals(tokenType, "input", StringComparison.OrdinalIgnoreCase)
                ? inputPerMillion
                : string.Equals(tokenType, "output", StringComparison.OrdinalIgnoreCase)
                    ? outputPerMillion
                    : 0;

            total += (tokenCount / 1_000_000d) * perMillion;
        }

        return total;
    }

    private (double InputPerMillion, double OutputPerMillion) ResolveModelRates(string? model)
    {
        if (!string.IsNullOrWhiteSpace(model) && _pricing.ModelRates.TryGetValue(model, out var modelRate))
        {
            return (modelRate.InputPerMillionUsd, modelRate.OutputPerMillionUsd);
        }

        return (_pricing.DefaultInputPerMillionUsd, _pricing.DefaultOutputPerMillionUsd);
    }

    private static bool TryParseVectorValue(JsonElement item, out double value)
    {
        value = 0;
        if (!item.TryGetProperty("value", out var valueArray) || valueArray.ValueKind != JsonValueKind.Array || valueArray.GetArrayLength() < 2)
        {
            return false;
        }

        var valueString = valueArray[1].GetString();
        if (string.IsNullOrWhiteSpace(valueString))
        {
            return false;
        }

        return double.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private async Task<LatestUserInfo> QueryLatestUserAsync(CancellationToken cancellationToken)
    {
        var result = await QueryResultAsync(BuildLatestUserQuery(), cancellationToken);
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0)
        {
            var instantUser = ExtractLatestUserInfoOrUnknown(result);
            if (!string.Equals(instantUser.Email, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return instantUser;
            }
        }

        result = await QueryResultAsync(BuildLatestUserFallbackQuery(ResolveMaxHistoryDays()), cancellationToken);
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0)
        {
            return ExtractLatestUserInfoOrUnknown(result);
        }

        return new LatestUserInfo("Unknown", null);
    }

    private string BuildLatestUserQuery()
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        return $"topk(1, max by (user.email) (timestamp(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\",user.email!=\"\"}})))";
    }

    private string BuildLatestUserFallbackQuery(int fallbackDays)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var days = Math.Max(1, fallbackDays);
        return $"topk(1, max by (user.email) (timestamp(last_over_time(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\",user.email!=\"\"}}[{days}d]))))";
    }

    private static LatestUserInfo ExtractLatestUserInfoOrUnknown(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            return new LatestUserInfo("Unknown", null);
        }

        var first = result[0];
        if (!first.TryGetProperty("metric", out var metric))
        {
            return new LatestUserInfo("Unknown", null);
        }

        if (!metric.TryGetProperty("user.email", out var emailElement))
        {
            return new LatestUserInfo("Unknown", null);
        }

        var email = emailElement.GetString();
        if (string.IsNullOrWhiteSpace(email))
        {
            return new LatestUserInfo("Unknown", null);
        }

        return new LatestUserInfo(email, ExtractTimestampOrNull(first));
    }

    private static DateTimeOffset? ExtractTimestampOrNull(JsonElement item)
    {
        if (!item.TryGetProperty("value", out var valueArray) || valueArray.ValueKind != JsonValueKind.Array || valueArray.GetArrayLength() < 2)
        {
            return null;
        }

        var tsElement = valueArray[0];
        double seconds;
        if (tsElement.ValueKind == JsonValueKind.Number)
        {
            seconds = tsElement.GetDouble();
        }
        else if (tsElement.ValueKind == JsonValueKind.String)
        {
            var tsRaw = tsElement.GetString();
            if (string.IsNullOrWhiteSpace(tsRaw) ||
                !double.TryParse(tsRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out seconds))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        var unixSeconds = (long)Math.Floor(seconds);
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    private static string BuildUserActiveDaysText(bool isKnownUser, DateTimeOffset? activeAt)
    {
        if (!isKnownUser || !activeAt.HasValue)
        {
            return "N/A";
        }

        var elapsed = DateTimeOffset.Now - activeAt.Value;
        var days = (int)Math.Floor(elapsed.TotalDays);
        if (days < 0)
        {
            days = 0;
        }

        return $"{days} days";
    }

    private async Task<(double ContextWindowM, string ContextText, double ContextPercent)> QueryContextForUserAsync(string userEmail, int fallbackDays, CancellationToken cancellationToken)
    {
        if (string.Equals(userEmail, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return (0, "N/A", 0);
        }

        var sessionId = await QueryActiveSessionIdForUserAsync(userEmail, fallbackDays, cancellationToken);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return (0, "N/A", 0);
        }

        var model = await QueryLatestModelForSessionAsync(userEmail, sessionId, fallbackDays, cancellationToken);
        if (string.IsNullOrWhiteSpace(model))
        {
            return (0, "N/A", 0);
        }

        if (!_modelCapability.ModelContextWindowTokens.TryGetValue(model, out var windowTokens) || windowTokens <= 0)
        {
            return (0, "N/A", 0);
        }

        var instant = await QueryScalarOrEmptyAsync(BuildSessionContextUsageQuery(userEmail, sessionId), cancellationToken);
        var usedTokens = instant.HasValue ? instant.Value : 0;
        if (!instant.HasValue)
        {
            var fallback = await QueryScalarOrEmptyAsync(BuildSessionContextUsageFallbackQuery(userEmail, sessionId, fallbackDays), cancellationToken);
            if (!fallback.HasValue)
            {
                return (0, "N/A", 0);
            }

            usedTokens = fallback.Value;
        }

        var percent = (usedTokens / windowTokens) * 100d;
        return (usedTokens / 1_000_000d, FormatCompactTokenValue(usedTokens), Math.Clamp(percent, 0d, 100d));
    }

    private async Task<string?> QueryActiveSessionIdForUserAsync(string userEmail, int fallbackDays, CancellationToken cancellationToken)
    {
        var result = await QueryResultAsync(BuildActiveSessionQuery(userEmail), cancellationToken);
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0)
        {
            var session = ExtractPreferredSessionIdOrNull(result);
            if (!string.IsNullOrWhiteSpace(session))
            {
                return session;
            }
        }

        result = await QueryResultAsync(BuildActiveSessionFallbackQuery(userEmail, fallbackDays), cancellationToken);
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0)
        {
            return ExtractPreferredSessionIdOrNull(result);
        }

        return null;
    }

    private string BuildActiveSessionQuery(string userEmail)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var escapedUser = EscapePromQlLabelValue(userEmail);
        return $"max by (session.id) (timestamp(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\",user.email=\"{escapedUser}\",session.id!=\"\"}}))";
    }

    private string BuildActiveSessionFallbackQuery(string userEmail, int fallbackDays)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var escapedUser = EscapePromQlLabelValue(userEmail);
        var days = Math.Max(1, fallbackDays);
        return $"max by (session.id) (timestamp(last_over_time(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\",user.email=\"{escapedUser}\",session.id!=\"\"}}[{days}d])))";
    }

    private string BuildSessionContextUsageQuery(string userEmail, string sessionId)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var escapedUser = EscapePromQlLabelValue(userEmail);
        var escapedSession = EscapePromQlLabelValue(sessionId);
        return $"sum(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\",user.email=\"{escapedUser}\",session.id=\"{escapedSession}\",gen_ai.token.type=\"input\"}})";
    }

    private string BuildSessionContextUsageFallbackQuery(string userEmail, string sessionId, int fallbackDays)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var escapedUser = EscapePromQlLabelValue(userEmail);
        var escapedSession = EscapePromQlLabelValue(sessionId);
        var days = Math.Max(1, fallbackDays);
        return $"sum(last_over_time(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\",user.email=\"{escapedUser}\",session.id=\"{escapedSession}\",gen_ai.token.type=\"input\"}}[{days}d]))";
    }

    private async Task<string?> QueryLatestModelForSessionAsync(string userEmail, string sessionId, int fallbackDays, CancellationToken cancellationToken)
    {
        var result = await QueryResultAsync(BuildLatestModelForSessionQuery(userEmail, sessionId), cancellationToken);
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0)
        {
            var model = ExtractModelOrNull(result);
            if (!string.IsNullOrWhiteSpace(model))
            {
                return model;
            }
        }

        result = await QueryResultAsync(BuildLatestModelForSessionFallbackQuery(userEmail, sessionId, fallbackDays), cancellationToken);
        if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0)
        {
            return ExtractModelOrNull(result);
        }

        return null;
    }

    private string BuildLatestModelForSessionQuery(string userEmail, string sessionId)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var escapedUser = EscapePromQlLabelValue(userEmail);
        var escapedSession = EscapePromQlLabelValue(sessionId);
        return $"topk(1, max by (gen_ai.request.model) (timestamp(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\",user.email=\"{escapedUser}\",session.id=\"{escapedSession}\",gen_ai.request.model!=\"\"}})))";
    }

    private string BuildLatestModelForSessionFallbackQuery(string userEmail, string sessionId, int fallbackDays)
    {
        var escapedServiceName = EscapePromQlLabelValue(GetServiceNameFilter());
        var escapedUser = EscapePromQlLabelValue(userEmail);
        var escapedSession = EscapePromQlLabelValue(sessionId);
        var days = Math.Max(1, fallbackDays);
        return $"topk(1, max by (gen_ai.request.model) (timestamp(last_over_time(gen_ai.client.token.usage_sum{{service.name=\"{escapedServiceName}\",user.email=\"{escapedUser}\",session.id=\"{escapedSession}\",gen_ai.request.model!=\"\"}}[{days}d]))))";
    }

    private int ResolveLookbackDays(DateTimeOffset? activeAt)
    {
        var maxHistoryDays = ResolveMaxHistoryDays();
        if (!activeAt.HasValue)
        {
            return maxHistoryDays;
        }

        var elapsedDays = (int)Math.Ceiling((DateTimeOffset.Now - activeAt.Value).TotalDays) + 1;
        if (elapsedDays < 1)
        {
            elapsedDays = 1;
        }

        return Math.Min(maxHistoryDays, elapsedDays);
    }

    private int ResolveMaxHistoryDays()
    {
        return _options.MaxHistoryDays > 0 ? _options.MaxHistoryDays : 365;
    }

    private static string? ExtractPreferredSessionIdOrNull(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            return null;
        }

        string? selectedSession = null;
        double selectedTs = double.MinValue;
        foreach (var item in result.EnumerateArray())
        {
            if (!item.TryGetProperty("metric", out var metric))
            {
                continue;
            }

            if (!metric.TryGetProperty("session.id", out var sessionElement))
            {
                continue;
            }

            var session = sessionElement.GetString();
            if (string.IsNullOrWhiteSpace(session))
            {
                continue;
            }

            if (!TryParseVectorValue(item, out var ts))
            {
                continue;
            }

            // Stable pick: higher timestamp first; if tied, lexical session id.
            var shouldSelect = ts > selectedTs
                || (Math.Abs(ts - selectedTs) < 0.0001d
                    && selectedSession is not null
                    && string.CompareOrdinal(session, selectedSession) > 0);

            if (!shouldSelect && selectedSession is not null)
            {
                continue;
            }

            selectedTs = ts;
            selectedSession = session;
        }

        return selectedSession;
    }

    private static string? ExtractModelOrNull(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            return null;
        }

        if (!result[0].TryGetProperty("metric", out var metric))
        {
            return null;
        }

        if (!metric.TryGetProperty("gen_ai.request.model", out var modelElement))
        {
            return null;
        }

        var model = modelElement.GetString();
        return string.IsNullOrWhiteSpace(model) ? null : model;
    }

    private string GetServiceNameFilter()
    {
        lock (_serviceNameLock)
        {
            if (!string.IsNullOrWhiteSpace(_serviceNameFilterOverride))
            {
                return _serviceNameFilterOverride;
            }
        }

        return string.IsNullOrWhiteSpace(_options.ServiceNameFilter)
            ? "gemini-cli"
            : _options.ServiceNameFilter.Trim();
    }

    private static string EscapePromQlLabelValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string FormatCompactTokenValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            return "0";
        }

        if (value >= 1_000_000d)
        {
            var m = value / 1_000_000d;
            return m >= 10d
                ? $"{m:0}M"
                : $"{m:0.#}M";
        }

        if (value >= 1_000d)
        {
            var k = value / 1_000d;
            return k >= 100d
                ? $"{k:0}K"
                : $"{k:0.#}K";
        }

        return $"{value:0}";
    }

    private async Task<JsonElement> QueryResultAsync(string query, CancellationToken cancellationToken)
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
            return default;
        }

        return root.GetProperty("data").GetProperty("result").Clone();
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
