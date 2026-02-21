using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aiden.TrayMonitor.Infrastructure;

namespace Aiden.TrayMonitor.ViewModels;

public partial class TrayPanelViewModel : ObservableObject
{
    private const string GeminiServiceName = "gemini-cli";
    private const string CodexServiceName = "codex-cli";
    private const string ClaudeServiceName = "claude-code";

    private readonly TelemetryService _telemetryService;
    private readonly CliProvisioningDialogService _cliProvisioningDialogService;
    private readonly CliProvisioningService _cliProvisioningService;

    [ObservableProperty] private double _inputTokens;
    [ObservableProperty] private double _outputTokens;
    [ObservableProperty] private string _inputText = "N/A";
    [ObservableProperty] private string _outputText = "N/A";
    [ObservableProperty] private double _sessionCostUsd;
    [ObservableProperty] private double _contextWindowM;
    [ObservableProperty] private double _contextPercent;
    [ObservableProperty] private string _contextText = "N/A";
    [ObservableProperty] private string _currentUserEmail = "Unknown";
    [ObservableProperty] private string _userActiveAtText = "N/A";
    [ObservableProperty] private bool _isOnline;
    [ObservableProperty] private string _statusText = "Offline";
    [ObservableProperty] private string _updatedAt = "-";
    [ObservableProperty] private CliProviderType _selectedProvider = CliProviderType.Gemini;
    [ObservableProperty] private bool _isGeminiSelected = true;
    [ObservableProperty] private bool _isCodexSelected;
    [ObservableProperty] private bool _isClaudeSelected;
    [ObservableProperty] private bool _isGeminiEnabled = true;
    [ObservableProperty] private bool _isCodexEnabled = true;
    [ObservableProperty] private bool _isClaudeEnabled = true;

    public TrayPanelViewModel(
        TelemetryService telemetryService,
        CliProvisioningDialogService cliProvisioningDialogService,
        CliProvisioningService cliProvisioningService)
    {
        _telemetryService = telemetryService;
        _cliProvisioningDialogService = cliProvisioningDialogService;
        _cliProvisioningService = cliProvisioningService;
        _telemetryService.SnapshotUpdated += OnSnapshotUpdated;
        ApplyProviderFilterAndRefresh(CliProviderType.Gemini);
        _ = ReloadProviderStatesAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => _telemetryService.RefreshOnceAsync();

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        _cliProvisioningDialogService.ShowSettingsDialog();
        await ReloadProviderStatesAsync();
    }

    [RelayCommand]
    private void SelectGemini() => SetProvider(CliProviderType.Gemini);

    [RelayCommand]
    private void SelectCodex() => SetProvider(CliProviderType.Codex);

    [RelayCommand]
    private void SelectClaude() => SetProvider(CliProviderType.Claude);

    partial void OnSelectedProviderChanged(CliProviderType value)
    {
        IsGeminiSelected = value == CliProviderType.Gemini;
        IsCodexSelected = value == CliProviderType.Codex;
        IsClaudeSelected = value == CliProviderType.Claude;
    }

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
            ContextPercent = snapshot.ContextPercent;
            ContextText = snapshot.ContextText;
            IsOnline = snapshot.Online;
            StatusText = snapshot.Online ? "ONLINE" : "OFFLINE";
            UpdatedAt = snapshot.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        });
    }

    private void SetProvider(CliProviderType provider)
    {
        if (!IsProviderEnabled(provider))
        {
            return;
        }

        if (SelectedProvider == provider)
        {
            return;
        }

        SelectedProvider = provider;
        ApplyProviderFilterAndRefresh(provider);
    }

    private void ApplyProviderFilterAndRefresh(CliProviderType provider)
    {
        _telemetryService.SetServiceNameFilter(MapServiceName(provider));
        _ = _telemetryService.RefreshOnceAsync();
    }

    private static string MapServiceName(CliProviderType provider)
    {
        return provider switch
        {
            CliProviderType.Gemini => GeminiServiceName,
            CliProviderType.Codex => CodexServiceName,
            CliProviderType.Claude => ClaudeServiceName,
            _ => GeminiServiceName
        };
    }

    private bool IsProviderEnabled(CliProviderType provider)
    {
        return provider switch
        {
            CliProviderType.Gemini => IsGeminiEnabled,
            CliProviderType.Codex => IsCodexEnabled,
            CliProviderType.Claude => IsClaudeEnabled,
            _ => false
        };
    }

    private async Task ReloadProviderStatesAsync()
    {
        try
        {
            var states = await _cliProvisioningService.GetStatesAsync(CancellationToken.None);
            var enabledByProvider = states.ToDictionary(s => s.Provider, s => s.IsInstalled && s.IsEnabled);

            var geminiEnabled = enabledByProvider.GetValueOrDefault(CliProviderType.Gemini);
            var codexEnabled = enabledByProvider.GetValueOrDefault(CliProviderType.Codex);
            var claudeEnabled = enabledByProvider.GetValueOrDefault(CliProviderType.Claude);

            App.Current.Dispatcher.Invoke(() =>
            {
                IsGeminiEnabled = geminiEnabled;
                IsCodexEnabled = codexEnabled;
                IsClaudeEnabled = claudeEnabled;

                if (IsProviderEnabled(SelectedProvider))
                {
                    return;
                }

                if (IsGeminiEnabled)
                {
                    SetProvider(CliProviderType.Gemini);
                }
                else if (IsCodexEnabled)
                {
                    SetProvider(CliProviderType.Codex);
                }
                else if (IsClaudeEnabled)
                {
                    SetProvider(CliProviderType.Claude);
                }
            });
        }
        catch
        {
            // Keep current tab enabled state on transient provisioning read errors.
        }
    }
}
