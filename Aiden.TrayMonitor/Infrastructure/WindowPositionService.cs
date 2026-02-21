using System.Windows;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class WindowPositionService
{
    public void PlaceNearTaskbar(Window window)
    {
        const double margin = 12;
        var workArea = SystemParameters.WorkArea;
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

        window.Left = workArea.Right - width - margin;
        window.Top = workArea.Bottom - height - margin;
    }
}
