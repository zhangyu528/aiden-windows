using System.Windows;
using Aiden.TrayMonitor.ViewModels;

namespace Aiden.TrayMonitor.Views;

public partial class TrayPanelWindow : Window
{
    public TrayPanelWindow(TrayPanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Deactivated += (_, _) => Hide();
    }
}
