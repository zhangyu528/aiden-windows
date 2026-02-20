using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aiden.TrayMonitor.Infrastructure;

namespace Aiden.TrayMonitor.ViewModels;

public partial class TrayPanelViewModel : ObservableObject
{
    private readonly TelemetryService _telemetryService;

    [ObservableProperty] private double _inputTokens;
    [ObservableProperty] private double _outputTokens;
    [ObservableProperty] private string _inputText = "N/A";
    [ObservableProperty] private string _outputText = "N/A";
    [ObservableProperty] private double _sessionCostUsd;
    [ObservableProperty] private double _contextWindowM;
    [ObservableProperty] private string _contextText = "N/A";
    [ObservableProperty] private string _currentUserEmail = "Unknown";
    [ObservableProperty] private string _userActiveAtText = "N/A";
    [ObservableProperty] private bool _isOnline;
    [ObservableProperty] private string _statusText = "Offline";
    [ObservableProperty] private string _updatedAt = "-";

    public TrayPanelViewModel(TelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
        _telemetryService.SnapshotUpdated += OnSnapshotUpdated;
    }

    [RelayCommand]
    private Task RefreshAsync() => _telemetryService.RefreshOnceAsync();

    private void OnSnapshotUpdated(TelemetrySnapshot snapshot)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            InputTokens = snapshot.InputTokens;
            OutputTokens = snapshot.OutputTokens;
            InputText = snapshot.InputText;
            OutputText = snapshot.OutputText;
            CurrentUserEmail = snapshot.CurrentUserEmail;
            UserActiveAtText = snapshot.UserActiveAtText;
            SessionCostUsd = snapshot.SessionCostUsd;
            ContextWindowM = snapshot.ContextWindowM;
            ContextText = snapshot.ContextText;
            IsOnline = snapshot.Online;
            StatusText = snapshot.Online ? "Online" : "Offline";
            UpdatedAt = snapshot.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        });
    }
}
