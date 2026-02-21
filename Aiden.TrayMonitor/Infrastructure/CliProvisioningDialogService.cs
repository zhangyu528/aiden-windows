using System.Windows;
using Aiden.TrayMonitor.Views;

namespace Aiden.TrayMonitor.Infrastructure;

public sealed class CliProvisioningDialogService
{
    private readonly CliProvisioningService _provisioningService;

    public CliProvisioningDialogService(CliProvisioningService provisioningService)
    {
        _provisioningService = provisioningService;
    }

    public bool ShowOnboardingDialog()
    {
        var window = new CliProvisioningWindow(_provisioningService, requireAtLeastOneEnabled: true);
        var result = window.ShowDialog();
        return result == true;
    }

    public void ShowSettingsDialog()
    {
        var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var window = new CliProvisioningWindow(_provisioningService, requireAtLeastOneEnabled: false);
        if (owner is not null)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
