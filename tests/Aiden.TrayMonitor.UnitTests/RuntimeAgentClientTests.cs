using System.Net;
using System.Net.Http;
using System.Text;
using Aiden.TrayMonitor.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Aiden.TrayMonitor.UnitTests;

public sealed class RuntimeAgentClientTests
{
    [Fact]
    public async Task GetStatusTextAsync_WhenStatusEndpointReturnsHealthyPayload_ShouldFormatStatus()
    {
        var handler = new DelegateHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"ok":true,"vmHealthy":true,"collectorHealthy":false,"lastError":"collector not ready"}""",
                    Encoding.UTF8,
                    "application/json")
            });

        var client = CreateClient(handler);
        var status = await client.GetStatusTextAsync(CancellationToken.None);

        status.Should().Contain("VM=OK");
        status.Should().Contain("Collector=DOWN");
        status.Should().Contain("collector not ready");
    }

    [Fact]
    public async Task RestartRuntimeAsync_WhenRequestFails_ShouldReturnFalse()
    {
        var handler = new DelegateHttpMessageHandler((_, _) => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);

        var restarted = await client.RestartRuntimeAsync(CancellationToken.None);

        restarted.Should().BeFalse();
    }

    private static RuntimeAgentClient CreateClient(HttpMessageHandler handler)
    {
        var options = Options.Create(new AgentOptions
        {
            Enabled = true,
            AutoStartOnLogin = false,
            StatusPort = 18731
        });

        return new RuntimeAgentClient(options, new SingleHttpClientFactory(new HttpClient(handler)));
    }

    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name = "")
        {
            return _client;
        }
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request, cancellationToken));
        }
    }
}
