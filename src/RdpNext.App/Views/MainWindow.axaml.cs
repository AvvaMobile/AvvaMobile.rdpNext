using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // macOS'ta sol üstteki pencere düğmeleri sidebar düğmesinin ve sekmelerin üstüne binmesin.
        if (OperatingSystem.IsMacOS())
        {
            TitleContent.Margin = new Avalonia.Thickness(92, 0, 14, 0);
            Opened += (_, _) =>
            {
                if (OperatingSystem.IsMacOS())
                    Platform.MacTitleBar.CenterTrafficLights(this);
            };
        }
    }

    // macOS üst menü çubuğundaki File menüsü; diğer sistemlerde görünmez.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainWindowViewModel vm)
            return;
        NativeMenu.SetMenu(this, new NativeMenu
        {
            new NativeMenuItem(Strings.Strings.File)
            {
                Menu = new NativeMenu
                {
                    new NativeMenuItem(Strings.Strings.AddServer) { Command = vm.NewServerCommand },
                    new NativeMenuItem(Strings.Strings.AddFolder) { Command = vm.NewFolderCommand },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem(Strings.Strings.Settings) { Command = vm.OpenSettingsCommand },
                },
            },
        });
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
