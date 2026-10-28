using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using RdpNext.Core.Model;

namespace RdpNext.App.Views;

public static class Converters
{
    private static readonly Dictionary<ServerColor, IBrush> ServerBrushes = new()
    {
        [ServerColor.Blue] = new ImmutableSolidColorBrush(Color.Parse("#2d6cdf")),
        [ServerColor.Teal] = new ImmutableSolidColorBrush(Color.Parse("#12a594")),
        [ServerColor.Orange] = new ImmutableSolidColorBrush(Color.Parse("#e5673a")),
        [ServerColor.Violet] = new ImmutableSolidColorBrush(Color.Parse("#7b5cf0")),
        [ServerColor.Green] = new ImmutableSolidColorBrush(Color.Parse("#2fbf71")),
        [ServerColor.Black] = new ImmutableSolidColorBrush(Color.Parse("#111318")),
    };

    private static readonly IBrush EditorTile = new ImmutableSolidColorBrush(Color.Parse("#8080a0"), 0.18);

    public static readonly IValueConverter TabTileBrush =
        new FuncValueConverter<ServerColor?, IBrush>(color => color is { } c ? ServerBrushes[c] : EditorTile);

    public static readonly IValueConverter ServerColorBrush =
        new FuncValueConverter<ServerColor, IBrush>(color => ServerBrushes[color]);
}
