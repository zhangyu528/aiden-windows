using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aiden.TrayMonitor.Infrastructure;
using Aiden.TrayMonitor.Tray;
using Aiden.TrayMonitor.ViewModels;
using Aiden.TrayMonitor.Views;

namespace Aiden.TrayMonitor;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.SetBasePath(AppContext.BaseDirectory);
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<VmOptions>(ctx.Configuration.GetSection("Vm"));
                services.AddHttpClient();

                services.AddSingleton<VmClient>();
                services.AddSingleton<TelemetryService>();
                services.AddSingleton<WindowPositionService>();
                services.AddSingleton<TrayPanelViewModel>();
                services.AddSingleton<TrayPanelWindow>();
                services.AddSingleton<TrayIconService>();
            })
            .Build();

        await _host.StartAsync();

        var telemetry = _host.Services.GetRequiredService<TelemetryService>();
        telemetry.Start();

        var tray = _host.Services.GetRequiredService<TrayIconService>();
        tray.Initialize();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var telemetry = _host.Services.GetService<TelemetryService>();
            telemetry?.Stop();

            var tray = _host.Services.GetService<TrayIconService>();
            tray?.Dispose();

            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
