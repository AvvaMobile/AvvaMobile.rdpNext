using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace RdpNext.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // macOS'ta sol üstteki pencere düğmeleri sekmelerin üstüne binmesin.
        if (OperatingSystem.IsMacOS())
        {
            Tabs.Margin = new Avalonia.Thickness(84, 0, 14, 0);
            Opened += (_, _) =>
            {
                if (OperatingSystem.IsMacOS())
                    Platform.MacTitleBar.CenterTrafficLights(this);
            };
        }
    }

    // Başlık çubuğunun boş yerine çift tık: ekranı doldur ya da eski boyuta dön.
    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source
            && (source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null
                || source.FindAncestorOfType<Button>(includeSelf: true) is not null))
            return;
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
