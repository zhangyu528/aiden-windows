using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Aiden.TrayMonitor.Infrastructure;

namespace Aiden.TrayMonitor.Views;

public partial class CliProvisioningWindow : Window
{
    private readonly CliProvisioningService _provisioningService;
    private readonly bool _requireAtLeastOneEnabled;
    private readonly CliProvisioningWindowModel _model;

    public CliProvisioningWindow(CliProvisioningService provisioningService, bool requireAtLeastOneEnabled)
    {
        _provisioningService = provisioningService;
        _requireAtLeastOneEnabled = requireAtLeastOneEnabled;

        _model = new CliProvisioningWindowModel
        {
            TitleText = requireAtLeastOneEnabled ? "Welcome - CLI Setup" : "CLI Settings",
            SubtitleText = requireAtLeastOneEnabled
                ? "Enable at least one installed CLI to start telemetry collection."
                : "Manage Gemini/Codex/Claude telemetry switches. Uninstalled clients show install hints.",
            ContinueButtonText = requireAtLeastOneEnabled ? "Continue" : "Close"
        };

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
            _model.Items.Add(new CliProvisioningRow
            {
                Provider = state.Provider,
                DisplayName = state.DisplayName,
                IsInstalled = state.IsInstalled,
                IsEnabled = state.IsEnabled,
                InstallHint = state.InstallHint,
                ConfigPath = state.ConfigPath
            });
        }

        UpdateContinueState();
    }

    private async void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox checkBox ||
            checkBox.DataContext is not CliProvisioningRow row ||
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
                "CLI Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        row.IsEnabled = targetEnabled;
        UpdateContinueState();
    }

    private void UpdateContinueState()
    {
        var hasEnabled = _model.Items.Any(i => i.IsInstalled && i.IsEnabled);
        _model.CanContinue = !_requireAtLeastOneEnabled || hasEnabled;
        _model.NoticeText = _model.CanContinue
            ? string.Empty
            : "You must enable at least one installed CLI to continue.";
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

    private sealed partial class CliProvisioningWindowModel : ObservableObject
    {
        [ObservableProperty] private string _titleText = string.Empty;
        [ObservableProperty] private string _subtitleText = string.Empty;
        [ObservableProperty] private string _continueButtonText = "Close";
        [ObservableProperty] private bool _canContinue = true;
        [ObservableProperty] private string _noticeText = string.Empty;
        public ObservableCollection<CliProvisioningRow> Items { get; } = [];
    }

    private sealed partial class CliProvisioningRow : ObservableObject
    {
        public CliProviderType Provider { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public bool IsInstalled { get; init; }
        public string InstallStatusText => IsInstalled ? "Installed" : "Not Installed";
        [ObservableProperty] private bool _isEnabled;
        public string InstallHint { get; init; } = string.Empty;
        public string ConfigPath { get; init; } = string.Empty;
    }
}
