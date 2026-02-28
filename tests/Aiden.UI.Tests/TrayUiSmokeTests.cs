using FluentAssertions;

namespace Aiden.UI.Tests;

public sealed class TrayUiSmokeTests
{
    [Fact]
    public void TrayExecutablePath_ShouldBeResolvableForUiRuns()
    {
        var path = ResolveExecutablePath();
        path.Should().NotBeNullOrWhiteSpace();
        File.Exists(path).Should().BeTrue();
    }

    [Fact(Skip = "Requires interactive desktop session and stable UI automation IDs.")]
    public void LaunchAndAttach_Smoke()
    {
    }

    private static string ResolveExecutablePath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("AIDEN_UI_APP_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var candidate = Path.Combine(repoRoot, "Aiden.TrayMonitor", "bin", "Release", "net8.0-windows", "Aiden.TrayMonitor.exe");
        return candidate;
    }
}
