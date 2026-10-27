using Avalonia.Controls;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

// Oturumun görüntüsü. Sekme değişince görünüm yeniden kurulur; görüntü SessionScreen'de durduğu için kaybolmaz.
public partial class SessionView : UserControl
{
    private SessionScreen? _screen;

    public SessionView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => Watch(null);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Watch((DataContext as SessionTabViewModel)?.Screen);
    }

    private void Watch(SessionScreen? screen)
    {
        if (_screen is not null)
            _screen.Invalidated -= Redraw;
        _screen = screen;
        if (_screen is not null)
            _screen.Invalidated += Redraw;
    }

    private void Redraw() => Screen.InvalidateVisual();
}
