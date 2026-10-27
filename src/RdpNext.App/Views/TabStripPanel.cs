using Avalonia;
using Avalonia.Controls;

namespace RdpNext.App.Views;

// İlk çocuk (sekmeler) sığdığı kadar yer alır, ikinci çocuk ("+") hemen ardına gelir ve hep görünür kalır.
public sealed class TabStripPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var (tabs, plus) = (Children[0], Children[1]);
        plus.Measure(availableSize);
        tabs.Measure(availableSize.WithWidth(Math.Max(0, availableSize.Width - plus.DesiredSize.Width)));
        return new Size(
            tabs.DesiredSize.Width + plus.DesiredSize.Width,
            Math.Max(tabs.DesiredSize.Height, plus.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (tabs, plus) = (Children[0], Children[1]);
        var plusWidth = plus.DesiredSize.Width;
        var tabsWidth = Math.Min(tabs.DesiredSize.Width, Math.Max(0, finalSize.Width - plusWidth));
        tabs.Arrange(new Rect(0, 0, tabsWidth, finalSize.Height));
        plus.Arrange(new Rect(tabsWidth, 0, plusWidth, finalSize.Height));
        return finalSize;
    }
}
