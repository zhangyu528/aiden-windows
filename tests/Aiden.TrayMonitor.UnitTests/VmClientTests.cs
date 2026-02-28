using System.Net;
using System.Net.Http;
using System.Text;
using Aiden.TrayMonitor.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Aiden.TrayMonitor.UnitTests;

public sealed class VmClientTests
{
    [Fact]
    public async Task QuerySnapshotAsync_WhenHttpFails_ReturnsOfflineSnapshot()
    {
        var handler = new DelegateHttpMessageHandler((_, _) => throw new HttpRequestException("boom"));
        var client = CreateVmClient(handler);

        var snapshot = await client.QuerySnapshotAsync(CancellationToken.None);

        snapshot.Online.Should().BeFalse();
        snapshot.CurrentUserEmail.Should().Be("Unknown");
    }

    [Fact]
    public async Task QuerySnapshotAsync_UsesFallbackAndServiceNameOverride()
    {
        var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            var query = ExtractQuery(request.RequestUri);

            if (query.Contains("max by (user.email)", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return VmResult("""
                {"metric":{"user.email":"dev@example.com"},"value":[1700000000,"1700000000"]}
                """);
            }

            if (query.Contains("token.type=\"input\"", StringComparison.Ordinal) &&
                query.Contains("sum(", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return EmptyResult();
            }

            if (query.Contains("token.type=\"input\"", StringComparison.Ordinal) &&
                query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return ScalarResult("1200");
            }

            if (query.Contains("token.type=\"output\"", StringComparison.Ordinal) &&
                query.Contains("sum(", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return ScalarResult("800");
            }

            return EmptyResult();
        });

        var vmClient = CreateVmClient(handler);
        vmClient.SetServiceNameFilterOverride("codex-cli");

        var snapshot = await vmClient.QuerySnapshotAsync(CancellationToken.None);

        snapshot.Online.Should().BeTrue();
        snapshot.InputTokens.Should().Be(1200);
        snapshot.OutputTokens.Should().Be(800);
        handler.Queries.Should().Contain(q => q.Contains("last_over_time", StringComparison.Ordinal) && q.Contains("token.type=\"input\"", StringComparison.Ordinal));
        handler.Queries.Should().Contain(q => q.Contains("service.name=\"codex-cli\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QuerySnapshotAsync_ComputesCostAndContextWithKnownModelRates()
    {
        var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            var query = ExtractQuery(request.RequestUri);

            if (query.Contains("max by (user.email)", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return VmResult("""
                {"metric":{"user.email":"dev@example.com"},"value":[1700000000,"1700000000"]}
                """);
            }

            if (query.Contains("token.type=\"input\"", StringComparison.Ordinal) &&
                query.Contains("sum(", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal) &&
                !query.Contains("idelta", StringComparison.Ordinal))
            {
                return ScalarResult("2000000");
            }

            if (query.Contains("token.type=\"output\"", StringComparison.Ordinal) &&
                query.Contains("sum(", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return ScalarResult("1000000");
            }

            if (query.Contains("sum by (gen_ai.request.model, gen_ai.token.type)", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return VmResult(
                    """{"metric":{"gen_ai.request.model":"model-x","gen_ai.token.type":"input"},"value":[1700000000,"2000000"]}""",
                    """{"metric":{"gen_ai.request.model":"model-x","gen_ai.token.type":"output"},"value":[1700000000,"1000000"]}"""
                );
            }

            if (query.Contains("max by (session.id)", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return VmResult("""{"metric":{"session.id":"s1"},"value":[1700000000,"1700000000"]}""");
            }

            if (query.Contains("max by (gen_ai.request.model)", StringComparison.Ordinal) &&
                !query.Contains("last_over_time", StringComparison.Ordinal))
            {
                return VmResult("""{"metric":{"gen_ai.request.model":"model-x"},"value":[1700000000,"1700000000"]}""");
            }

            if (query.Contains("idelta", StringComparison.Ordinal) &&
                query.Contains("gen_ai.token.type=\"input\"", StringComparison.Ordinal))
            {
                return ScalarResult("600000");
            }

            if (query.Contains("idelta", StringComparison.Ordinal) &&
                query.Contains("gen_ai.token.type=\"cached\"", StringComparison.Ordinal))
            {
                return ScalarResult("100000");
            }

            return EmptyResult();
        });

        var pricing = new PricingOptions
        {
            DefaultInputPerMillionUsd = 1.0,
            DefaultOutputPerMillionUsd = 1.0,
            ModelRates = new Dictionary<string, ModelRate>(StringComparer.OrdinalIgnoreCase)
            {
                ["model-x"] = new() { InputPerMillionUsd = 0.1, OutputPerMillionUsd = 0.2 }
            }
        };

        var modelCapability = new ModelCapabilityOptions
        {
            ModelContextWindowTokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["model-x"] = 1_000_000
            }
        };

        var vmClient = CreateVmClient(handler, pricing, modelCapability);

        var snapshot = await vmClient.QuerySnapshotAsync(CancellationToken.None);

        snapshot.Online.Should().BeTrue();
        snapshot.SessionCostUsd.Should().BeApproximately(0.4, 0.0001);
        snapshot.ContextWindowM.Should().BeApproximately(0.5, 0.0001);
        snapshot.ContextPercent.Should().BeApproximately(50.0, 0.0001);
        snapshot.ContextText.Should().Be("500K");
    }

    private static VmClient CreateVmClient(
        HttpMessageHandler handler,
        PricingOptions? pricing = null,
        ModelCapabilityOptions? modelCapability = null)
    {
        var options = Options.Create(new VmOptions
        {
            BaseUrl = "http://127.0.0.1:8428",
            QueryEndpoint = "/api/v1/query",
            ServiceNameFilter = "gemini-cli",
            MaxHistoryDays = 365
        });

        return new VmClient(
            new SingleHttpClientFactory(handler),
            options,
            Options.Create(pricing ?? new PricingOptions()),
            Options.Create(modelCapability ?? new ModelCapabilityOptions()));
    }

    private static string ExtractQuery(Uri? uri)
    {
        if (uri is null)
        {
            return string.Empty;
        }

        var raw = uri.Query.TrimStart('?');
        foreach (var part in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!part.StartsWith("query=", StringComparison.Ordinal))
            {
                continue;
            }

            return Uri.UnescapeDataString(part[6..]);
        }

        return string.Empty;
    }

    private static HttpResponseMessage ScalarResult(string value)
    {
        return VmResult($"{{\"metric\":{{}},\"value\":[1700000000,\"{value}\"]}}");
    }

    private static HttpResponseMessage EmptyResult() => SuccessResponse("""{"status":"success","data":{"result":[]}}""");

    private static HttpResponseMessage VmResult(params string[] vectors)
    {
        var joined = string.Join(",", vectors);
        return SuccessResponse("{\"status\":\"success\",\"data\":{\"result\":[" + joined + "]}}");
    }

    private static HttpResponseMessage SuccessResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public SingleHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name = "")
        {
            return new HttpClient(_handler, disposeHandler: false);
        }
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;
        public List<string> Queries { get; } = new();

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Queries.Add(ExtractQuery(request.RequestUri));
            return Task.FromResult(_handler(request, cancellationToken));
        }
    }
}
