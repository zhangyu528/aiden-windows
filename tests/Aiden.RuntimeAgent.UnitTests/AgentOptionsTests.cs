using Aiden.RuntimeAgent.Infrastructure;
using FluentAssertions;

namespace Aiden.RuntimeAgent.UnitTests;

public sealed class AgentOptionsTests
{
    [Fact]
    public void Defaults_AreSafeAndExpected()
    {
        var options = new AgentOptions();

        options.Enabled.Should().BeTrue();
        options.AutoStartOnLogin.Should().BeTrue();
        options.HealthCheckSeconds.Should().Be(5);
        options.BackoffMinSeconds.Should().Be(2);
        options.BackoffMaxSeconds.Should().Be(60);
        options.StatusPort.Should().Be(18731);
    }
}
