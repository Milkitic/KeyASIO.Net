using Avalonia;
using Avalonia.Headless;
using KeyAsio.UnitTests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace KeyAsio.UnitTests;

public class TestApp : Application
{
}

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}