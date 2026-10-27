using Avalonia;
using Avalonia.Headless;
using RdpNext.App;

[assembly: AvaloniaTestApplication(typeof(RdpNext.App.Tests.TestAppBuilder))]

namespace RdpNext.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
