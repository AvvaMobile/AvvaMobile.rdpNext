using Avalonia;
using Avalonia.Styling;
using RdpNext.Core.Storage;

namespace RdpNext.App;

// Ayarlardaki tema seçimi; System işletim sisteminin açık/koyu modunu izler.
public static class Themes
{
    public static void Apply(AppTheme theme) => Application.Current!.RequestedThemeVariant = theme switch
    {
        AppTheme.Dark => ThemeVariant.Dark,
        AppTheme.Light => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };
}
