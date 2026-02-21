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
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.SetBasePath(AppContext.BaseDirectory);
                cfg.AddJsonFile("runtime.shared.json", optional: true, reloadOnChange: true);
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<VmOptions>(ctx.Configuration.GetSection("Vm"));
                services.Configure<CollectorOptions>(ctx.Configuration.GetSection("Collector"));
                services.Configure<AgentOptions>(ctx.Configuration.GetSection("Agent"));
                services.Configure<PricingOptions>(ctx.Configuration.GetSection("Pricing"));
                services.Configure<ModelCapabilityOptions>(ctx.Configuration.GetSection("ModelCapability"));
                services.AddHttpClient();

                services.AddSingleton<UserStateService>();
                services.AddSingleton<VmClient>();
                services.AddSingleton<RuntimeAgentClient>();
                services.AddSingleton<CliProvisioningService>();
                services.AddSingleton<CliProvisioningDialogService>();
                services.AddSingleton<TelemetryService>();
                services.AddSingleton<WindowPositionService>();
                services.AddSingleton<TrayPanelViewModel>();
                services.AddSingleton<TrayPanelWindow>();
                services.AddSingleton<TrayIconService>();
            })
            .Build();

        await _host.StartAsync();

        var runtimeAgent = _host.Services.GetRequiredService<RuntimeAgentClient>();
        await runtimeAgent.EnsureReadyAsync(CancellationToken.None);

        var userState = _host.Services.GetRequiredService<UserStateService>();
        var provisioningService = _host.Services.GetRequiredService<CliProvisioningService>();
        var onboardingCompleted = await userState.IsOnboardingCompletedAsync(CancellationToken.None);

        if (!onboardingCompleted)
        {
            var states = await provisioningService.GetStatesAsync(CancellationToken.None);
            var allEnabled = states.Count > 0 && states.All(s => s.IsEnabled);
            if (allEnabled)
            {
                await userState.MarkOnboardingCompletedAsync(CancellationToken.None);
                onboardingCompleted = true;
            }
        }

        if (!onboardingCompleted)
        {
            var provisioningDialog = _host.Services.GetRequiredService<CliProvisioningDialogService>();
            var ok = provisioningDialog.ShowOnboardingDialog();
            if (!ok)
            {
                Shutdown();
                return;
            }

            await userState.MarkOnboardingCompletedAsync(CancellationToken.None);
        }

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
