using Avalonia;
using KeyAsio.Configuration;
using KeyAsio.Utils;
using Microsoft.Extensions.Hosting;

namespace KeyAsio.Services;

internal class GuiStartupService : IHostedService
{
    private readonly AppSettings _appSettings;

    public GuiStartupService(AppSettings appSettings)
    {
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
            Program.BuildAvaloniaApp()
                   .StartWithClassicDesktopLifetime([]);
        })
        {
            IsBackground = true
        };
        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }

        thread.Start();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
