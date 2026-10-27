using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using RdpNext.Core.Model;

namespace RdpNext.App.Views;

public static class Converters
{
    private static readonly Dictionary<ServerColor, IBrush> ServerBrushes = new()
    {
        [ServerColor.Blue] = new SolidColorBrush(Color.Parse("#2d6cdf")),
        [ServerColor.Teal] = new SolidColorBrush(Color.Parse("#12a594")),
        [ServerColor.Orange] = new SolidColorBrush(Color.Parse("#e5673a")),
        [ServerColor.Violet] = new SolidColorBrush(Color.Parse("#7b5cf0")),
        [ServerColor.Green] = new SolidColorBrush(Color.Parse("#2fbf71")),
    };

    private static readonly IBrush EditorTile = new SolidColorBrush(Color.Parse("#8080a0"), 0.18);

    public static readonly IValueConverter TabTileBrush =
        new FuncValueConverter<ServerColor?, IBrush>(color => color is { } c ? ServerBrushes[c] : EditorTile);

    // Tokens.axaml'daki şekil: Windows logosu, penguen ya da terminal.
    public static readonly IValueConverter ServerIconGeometry =
        new FuncValueConverter<ServerIcon, Geometry?>(icon => Application.Current!.FindResource(icon switch
        {
            ServerIcon.Linux => "LinuxLogo",
            ServerIcon.Ssh => "TerminalIcon",
            _ => "WindowsLogo",
        }) as Geometry);

    public static readonly IValueConverter ServerColorBrush =
        new FuncValueConverter<ServerColor, IBrush>(color => ServerBrushes[color]);
}
