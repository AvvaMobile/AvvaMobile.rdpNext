using Avalonia;
using Avalonia.Headless;
using RdpNext.App;

[assembly: AvaloniaTestApplication(typeof(RdpNext.App.Tests.TestAppBuilder))]

namespace RdpNext.App.Tests;

public static class TestAppBuilder
{
    // Testler arayüzü Türkçe bekler; makinenin diline bağlı kalmasın.
    public static AppBuilder BuildAvaloniaApp()
    {
        Languages.Apply(System.Globalization.CultureInfo.GetCultureInfo(Languages.Turkish));
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
