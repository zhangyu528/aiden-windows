namespace Aiden.TrayMonitor.Infrastructure;

public sealed class CliProvisioningState
{
    public required CliProviderType Provider { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsInstalled { get; init; }
    public required bool IsEnabled { get; init; }
    public required string InstallHint { get; init; }
    public required string ConfigPath { get; init; }
}
