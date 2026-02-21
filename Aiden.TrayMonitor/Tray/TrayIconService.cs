using System.Drawing;
using System.Windows;
using Aiden.TrayMonitor.Infrastructure;
using Aiden.TrayMonitor.Views;
using WinForms = System.Windows.Forms;

namespace Aiden.TrayMonitor.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly TrayPanelWindow _panelWindow;
    private readonly WindowPositionService _windowPositionService;
    private readonly TelemetryService _telemetryService;
    private readonly RuntimeAgentClient _runtimeAgentClient;
    private readonly CliProvisioningDialogService _cliProvisioningDialogService;
    private WinForms.NotifyIcon? _notifyIcon;

    public TrayIconService(
        TrayPanelWindow panelWindow,
        WindowPositionService windowPositionService,
        TelemetryService telemetryService,
        RuntimeAgentClient runtimeAgentClient,
        CliProvisioningDialogService cliProvisioningDialogService)
    {
        _panelWindow = panelWindow;
        _windowPositionService = windowPositionService;
        _telemetryService = telemetryService;
        _runtimeAgentClient = runtimeAgentClient;
        _cliProvisioningDialogService = cliProvisioningDialogService;
    }

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Refresh", null, async (_, _) => await _telemetryService.RefreshOnceAsync());
        contextMenu.Items.Add("Show/Hide", null, (_, _) => TogglePanel());
        contextMenu.Items.Add("CLI Settings", null, (_, _) => _cliProvisioningDialogService.ShowSettingsDialog());
        contextMenu.Items.Add("Runtime Status", null, async (_, _) => await ShowRuntimeStatusAsync());
        contextMenu.Items.Add("Restart Runtime", null, async (_, _) => await RestartRuntimeAsync());
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Aiden Tray Monitor",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                TogglePanel();
            }
        };
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private void TogglePanel()
    {
        if (_panelWindow.IsVisible)
        {
            _panelWindow.Hide();
            return;
        }

        _windowPositionService.PlaceNearTaskbar(_panelWindow);
        _panelWindow.Show();
        _panelWindow.Activate();
    }

    private async Task ShowRuntimeStatusAsync()
    {
        var status = await _runtimeAgentClient.GetStatusTextAsync(CancellationToken.None);
        System.Windows.MessageBox.Show(status, "Runtime Status", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task RestartRuntimeAsync()
    {
        var ok = await _runtimeAgentClient.RestartRuntimeAsync(CancellationToken.None);
        var message = ok ? "Runtime restart requested." : "Failed to request runtime restart.";
        var icon = ok ? MessageBoxImage.Information : MessageBoxImage.Warning;
        System.Windows.MessageBox.Show(message, "Runtime", MessageBoxButton.OK, icon);
    }
}
