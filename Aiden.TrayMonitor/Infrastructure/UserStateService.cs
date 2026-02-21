using System.Text.Json;
using System.IO;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class UserStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _stateFilePath;

    public UserStateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _stateFilePath = Path.Combine(appData, "AidenTrayMonitor", "user-state.json");
    }

    public async Task<bool> IsOnboardingCompletedAsync(CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(cancellationToken);
        return state.OnboardingCompleted;
    }

    public async Task MarkOnboardingCompletedAsync(CancellationToken cancellationToken)
    {
        var state = await ReadStateAsync(cancellationToken);
        state.OnboardingCompleted = true;
        await WriteStateAsync(state, cancellationToken);
    }

    private async Task<UserState> ReadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_stateFilePath))
        {
            return new UserState();
        }

        try
        {
            await using var stream = File.OpenRead(_stateFilePath);
            var state = await JsonSerializer.DeserializeAsync<UserState>(stream, cancellationToken: cancellationToken);
            return state ?? new UserState();
        }
        catch
        {
            return new UserState();
        }
    }

    private async Task WriteStateAsync(UserState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
        await using var stream = File.Create(_stateFilePath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
    }

    private sealed class UserState
    {
        public bool OnboardingCompleted { get; set; }
    }
}
