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
    private WinForms.NotifyIcon? _notifyIcon;

    public TrayIconService(
        TrayPanelWindow panelWindow,
        WindowPositionService windowPositionService,
        TelemetryService telemetryService)
    {
        _panelWindow = panelWindow;
        _windowPositionService = windowPositionService;
        _telemetryService = telemetryService;
    }

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("刷新", null, async (_, _) => await _telemetryService.RefreshOnceAsync());
        contextMenu.Items.Add("显示/隐藏", null, (_, _) => TogglePanel());
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => System.Windows.Application.Current.Shutdown());

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
}
