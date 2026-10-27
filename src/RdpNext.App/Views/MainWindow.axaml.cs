using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        // macOS'ta sol üstteki pencere düğmeleri sekmelerin üstüne binmesin.
        if (OperatingSystem.IsMacOS())
            Tabs.Margin = new Avalonia.Thickness(84, 0, 14, 0);
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

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
            _vm.FocusSearchRequested -= Sidebar.FocusSearch;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null)
            _vm.FocusSearchRequested += Sidebar.FocusSearch;
    }
}
