using System;
using System.Threading;
using System.Threading.Tasks;
using KeyAsio.Gui.Utils;
using KeyAsio.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KeyAsio.Gui;

internal class StartupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly YamlAppSettings _appSettings;

    public StartupService(IServiceProvider serviceProvider, YamlAppSettings appSettings)
    {
        _serviceProvider = serviceProvider;
        _appSettings = appSettings;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_appSettings.Logging.EnableDebugConsole)
        {
            ConsoleManager.Show();
        }

        var thread = new Thread(() =>
        {
            var app = _serviceProvider.GetRequiredService<App>();
            app.InitializeComponent();
            app.Run();
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
