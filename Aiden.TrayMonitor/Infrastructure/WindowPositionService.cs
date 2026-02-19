using System.Windows;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class WindowPositionService
{
    public void PlaceNearTaskbar(Window window)
    {
        const double margin = 12;
        var workArea = SystemParameters.WorkArea;

        window.Left = workArea.Right - window.Width - margin;
        window.Top = workArea.Bottom - window.Height - margin;
    }
}
