using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Aiden.TrayMonitor.Infrastructure;

namespace Aiden.TrayMonitor.Views;

public partial class OnboardingProvisioningWindow : Window
{
    private readonly CliProvisioningService _provisioningService;
    private readonly OnboardingProvisioningWindowModel _model;

    public OnboardingProvisioningWindow(CliProvisioningService provisioningService)
    {
        _provisioningService = provisioningService;
        _model = new OnboardingProvisioningWindowModel();

        InitializeComponent();
        DataContext = _model;
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var states = await _provisioningService.GetStatesAsync(CancellationToken.None);
        _model.Items.Clear();
        foreach (var state in states)
        {
            _model.Items.Add(new OnboardingProvisioningRow
            {
                Provider = state.Provider,
                DisplayName = state.DisplayName,
                IsInstalled = state.IsInstalled,
                IsEnabled = state.IsEnabled,
                StateIconGlyph = GetStateIconGlyph(state.IsInstalled, state.IsEnabled),
                StateLabel = GetStateLabel(state.IsInstalled, state.IsEnabled),
                Description = GetDescription(state.Provider, state.IsInstalled, state.IsEnabled),
                InstallHint = state.InstallHint,
                ConfigPath = state.ConfigPath,
                InstallCommandVisibility = state.IsInstalled ? Visibility.Collapsed : Visibility.Visible,
                CopyFeedbackVisibility = Visibility.Collapsed
            });
        }

        UpdateContinueState();
    }

    private async void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox checkBox ||
            checkBox.DataContext is not OnboardingProvisioningRow row ||
            checkBox.IsChecked is null)
        {
            return;
        }

        var targetEnabled = checkBox.IsChecked.Value;
        var ok = await _provisioningService.SetEnabledAsync(row.Provider, targetEnabled, CancellationToken.None);
        if (!ok)
        {
            row.IsEnabled = !targetEnabled;
            System.Windows.MessageBox.Show(
                "Failed to update configuration. Please verify CLI install state and file permissions.",
                "CLI Onboarding",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        row.IsEnabled = targetEnabled;
        row.StateIconGlyph = GetStateIconGlyph(row.IsInstalled, row.IsEnabled);
        row.StateLabel = GetStateLabel(row.IsInstalled, row.IsEnabled);
        row.Description = GetDescription(row.Provider, row.IsInstalled, row.IsEnabled);
        UpdateContinueState();
    }

    private void UpdateContinueState()
    {
        var hasEnabled = _model.Items.Any(i => i.IsInstalled && i.IsEnabled);
        _model.CanContinue = hasEnabled;
        _model.NoticeText = hasEnabled ? string.Empty : "Enable at least one installed CLI to continue.";
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        if (!_model.CanContinue)
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void OnCopyInstallCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: OnboardingProvisioningRow row } ||
            string.IsNullOrWhiteSpace(row.InstallHint))
        {
            return;
        }

        System.Windows.Clipboard.SetText(row.InstallHint);
        row.CopyFeedbackVisibility = Visibility.Visible;
        await Task.Delay(1800);
        row.CopyFeedbackVisibility = Visibility.Collapsed;
    }

    private static string GetStateIconGlyph(bool isInstalled, bool isEnabled)
    {
        if (!isInstalled)
        {
            return "\uF083";
        }

        return isEnabled ? "\uF0BE" : "\uEB8E";
    }

    private static string GetStateLabel(bool isInstalled, bool isEnabled)
    {
        if (!isInstalled)
        {
            return "NOT DETECTED";
        }

        return isEnabled ? "DETECTED" : "INSTALLED";
    }

    private static string GetDescription(CliProviderType provider, bool isInstalled, bool isEnabled)
    {
        if (!isInstalled)
        {
            return "Install the CLI to enable telemetry monitoring.";
        }

        return provider switch
        {
            CliProviderType.Gemini => "Monitor latency and token usage for Google Gemini integration.",
            CliProviderType.Codex => "Track code generation metrics and local model health.",
            CliProviderType.Claude => isEnabled
                ? "Track assistant coding activity and performance metrics."
                : "Enable telemetry to monitor Claude Code usage.",
            _ => "Enable telemetry to start monitoring."
        };
    }

    private sealed partial class OnboardingProvisioningWindowModel : ObservableObject
    {
        [ObservableProperty] private bool _canContinue;
        [ObservableProperty] private string _noticeText = string.Empty;
        public ObservableCollection<OnboardingProvisioningRow> Items { get; } = [];
    }

    private sealed partial class OnboardingProvisioningRow : ObservableObject
    {
        public CliProviderType Provider { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public bool IsInstalled { get; init; }
        [ObservableProperty] private string _stateIconGlyph = "\uEB8E";
        [ObservableProperty] private string _stateLabel = "Installed";
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private bool _isEnabled;
        public string InstallHint { get; init; } = string.Empty;
        public string ConfigPath { get; init; } = string.Empty;
        [ObservableProperty] private Visibility _installCommandVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility _copyFeedbackVisibility = Visibility.Collapsed;
    }
}
