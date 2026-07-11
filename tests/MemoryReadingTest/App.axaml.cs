using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KeyAsio.Configuration;
using KeyAsio.Application.Models;
using KeyAsio.Sync.Sources;
using KeyAsio.Sync;
using KeyAsio.Sync.Services;
using Microsoft.Extensions.DependencyInjection;
using NLog.Extensions.Logging;

namespace MemoryReadingTest
{
    public partial class App : Application
    {
        private SyncSessionContext _syncSessionContext = null!;

        public MemoryScan MemoryScan { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var provider = AppBootstrapper.InitServices();
            _syncSessionContext = provider.GetRequiredService<SyncSessionContext>();
            MemoryScan = provider.GetRequiredService<MemoryScan>();

            AppBootstrapper.ConfigureMemoryScan(provider);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
