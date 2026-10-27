using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Yeni oturum, oturum alanının o anki boyutuyla açılır; pencere kapanınca oturumlar kapatılır.
        SessionArea.SizeChanged += (_, e) =>
        {
            if (DataContext is MainWindowViewModel vm && e.NewSize.Width > 0 && e.NewSize.Height > 0)
                vm.SessionAreaSize = e.NewSize;
        };
        Closing += (_, _) => (DataContext as MainWindowViewModel)?.Shutdown();
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

    // macOS'ta uygulama adına tıklanınca açılan menü; Avalonia'nın varsayılan "About Avalonia"sının yerine geçer.
    // Avalonia bu menüyü açılışta kurar ve yenisini görmez; o yüzden var olan menünün içi değiştirilir,
    // Services, Gizle ve Çık maddeleri yerinde kalır.
    // Diğer sistemlerde görünmez.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainWindowViewModel vm || Application.Current is not { } app)
            return;
        var menu = NativeMenu.GetMenu(app);
        if (menu is null)
            NativeMenu.SetMenu(app, menu = new NativeMenu());
        if (menu.Items.FirstOrDefault() is NativeMenuItem { Header: "About Avalonia" } avalonia)
            menu.Items.Remove(avalonia);
        NativeMenuItemBase[] items =
        [
            new NativeMenuItem(string.Format(Strings.Strings.About, "rdpNext")) { Command = new RelayCommand(ShowAbout) },
            new NativeMenuItemSeparator(),
            new NativeMenuItem(Strings.Strings.AddServer) { Command = vm.NewServerCommand },
            new NativeMenuItem(Strings.Strings.AddFolder) { Command = vm.NewFolderCommand },
            new NativeMenuItemSeparator(),
            new NativeMenuItem(Strings.Strings.Settings) { Command = vm.OpenSettingsCommand },
        ];
        for (var i = 0; i < items.Length; i++)
            menu.Items.Insert(i, items[i]);
    }

    private void ShowAbout() => new AboutWindow().ShowDialog(this);

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
