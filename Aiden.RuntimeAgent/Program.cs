using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aiden.RuntimeAgent.Infrastructure;

var singleInstanceMutex = new Mutex(initiallyOwned: true, name: @"Local\AidenRuntimeAgentMutex", out var isSingleInstance);
if (!isSingleInstance)
{
    return;
}

try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration(cfg =>
        {
            cfg.SetBasePath(AppContext.BaseDirectory);
            cfg.AddJsonFile("runtime.shared.json", optional: true, reloadOnChange: true);
            cfg.AddJsonFile("agentsettings.json", optional: false, reloadOnChange: true);
        })
        .ConfigureServices((ctx, services) =>
        {
            services.Configure<VmOptions>(ctx.Configuration.GetSection("Vm"));
            services.Configure<CollectorOptions>(ctx.Configuration.GetSection("Collector"));
            services.Configure<AgentOptions>(ctx.Configuration.GetSection("Agent"));
            services.AddHttpClient();

            services.AddSingleton<VmProcessService>();
            services.AddSingleton<CollectorProcessService>();
            services.AddSingleton<RuntimeSupervisor>();
            services.AddHostedService<RuntimeAgentWorker>();
        })
        .Build();

    await host.RunAsync();
}
finally
{
    singleInstanceMutex.ReleaseMutex();
    singleInstanceMutex.Dispose();
}
