using System.Windows;

namespace Aiden.TrayMonitor.Views;

public partial class StartupErrorWindow : Window
{
    public string ErrorText
    {
        get => _errorText;
        set
        {
            _errorText = value;
            DataContext = this;
        }
    }

    private string _errorText = "Runtime health check did not respond in time.";

    public StartupErrorWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
