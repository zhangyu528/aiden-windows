using System.Net;
using System.Net.Http;
using System.Text;
using Aiden.RuntimeAgent.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Aiden.IntegrationTests;

public sealed class RuntimeSupervisorEndpointTests
{
    [Fact]
    public async Task StatusAndRestartEndpoints_AreReachable()
    {
        var statusPort = FindFreeTcpPort();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var vmOptions = Options.Create(new VmOptions
        {
            BaseUrl = "http://127.0.0.1",
            Port = 8428,
            HealthEndpoint = "/health"
        });
        var collectorOptions = Options.Create(new CollectorOptions
        {
            BaseUrl = "http://127.0.0.1",
            GrpcPort = 4317,
            HealthPort = 13133
        });
        var agentOptions = Options.Create(new AgentOptions
        {
            Enabled = true,
            AutoStartOnLogin = false,
            StatusPort = statusPort,
            HealthCheckSeconds = 1,
            BackoffMinSeconds = 1,
            BackoffMaxSeconds = 2
        });

        var vmService = new VmProcessService(vmOptions, new DefaultHttpClientFactory());
        var collectorService = new CollectorProcessService(collectorOptions, vmOptions);
        await using var supervisor = new RuntimeSupervisor(vmService, collectorService, agentOptions);

        var runTask = supervisor.RunAsync(cts.Token);

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var statusResponse = await WaitForHealthyResponse(client, $"http://127.0.0.1:{statusPort}/status", cts.Token);
            statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var statusBody = await statusResponse.Content.ReadAsStringAsync(cts.Token);
            statusBody.Should().Contain("vmHealthy");
            statusBody.Should().Contain("collectorHealthy");

            var restartResponse = await client.PostAsync(
                $"http://127.0.0.1:{statusPort}/restart",
                new StringContent("{}", Encoding.UTF8, "application/json"),
                cts.Token);

            restartResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var restartBody = await restartResponse.Content.ReadAsStringAsync(cts.Token);
            restartBody.Should().Contain("restart requested");
        }
        finally
        {
            cts.Cancel();
            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }
    }

    private static async Task<HttpResponseMessage> WaitForHealthyResponse(HttpClient client, string url, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 30; i++)
        {
            try
            {
                var response = await client.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
            }
            catch
            {
                // retry
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new TimeoutException($"Endpoint did not become available: {url}");
    }

    private static int FindFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class DefaultHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name = "")
        {
            return new HttpClient();
        }
    }
}
