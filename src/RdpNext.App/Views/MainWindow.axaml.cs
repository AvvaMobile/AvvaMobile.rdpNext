using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;

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
        // Tam ekran kısayolu oturum klavyeyi tutarken de önce burada yakalanır.
        AddHandler(KeyDownEvent, OnFullScreenGesture, Avalonia.Interactivity.RoutingStrategies.Tunnel);
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
        WatchFullScreen(DataContext as MainWindowViewModel);
        if (DataContext is not MainWindowViewModel vm || Application.Current is not { } app)
            return;
        Sidebar.Width = Math.Clamp(vm.Settings.Load().SidebarWidth, Sidebar.MinWidth, Sidebar.MaxWidth);
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

    private void OnFullScreenGesture(object? sender, KeyEventArgs e)
    {
        if (!Rdp.FullScreenGesture.Matches(e) || DataContext is not MainWindowViewModel vm)
            return;
        (FocusManager?.GetFocusedElement() as SessionView)?.ReleaseKeys();
        vm.ToggleFullScreenCommand.Execute(null);
        e.Handled = true;
    }

    private MainWindowViewModel? _watched;
    private readonly List<SessionWindow> _sessionWindows = [];

    // Tam ekrandaki oturumların pencereleri.
    public IReadOnlyList<SessionWindow> SessionWindows => _sessionWindows;

    private void WatchFullScreen(MainWindowViewModel? vm)
    {
        if (_watched is not null)
            _watched.Sessions.Windows.CollectionChanged -= OnSessionWindowsChanged;
        _watched = vm;
        if (_watched is not null)
            _watched.Sessions.Windows.CollectionChanged += OnSessionWindowsChanged;
    }

    // Oturum tam ekrana geçince kendi penceresi açılır; sekmesine dönünce pencere kapanır.
    // Pencereyi kullanıcı kapatırsa yalnız o oturum kapanır.
    private void OnSessionWindowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_watched is not { } vm)
            return;
        foreach (var tab in e.NewItems?.OfType<ConnectionTabViewModel>() ?? [])
        {
            var window = new SessionWindow(vm, tab);
            window.Closed += (_, _) =>
            {
                _sessionWindows.Remove(window);
                if (vm.Sessions.Windows.Contains(tab))
                    vm.Sessions.Close(tab);
            };
            _sessionWindows.Add(window);
            window.Show();
        }
        foreach (var tab in e.OldItems?.OfType<ConnectionTabViewModel>() ?? [])
            _sessionWindows.FirstOrDefault(w => w.DataContext == tab)?.Close();
    }

    // Ayraç tutulduğunda farenin x'i ve sidebar'ın genişliği; bırakılınca null.
    private (double X, double Width)? _splitterDrag;

    private void OnSplitterPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        e.Handled = true;
        if (e.ClickCount % 2 == 0)
        {
            Sidebar.Width = AppSettings.DefaultSidebarWidth;
            SaveSidebarWidth();
            return;
        }
        _splitterDrag = (e.GetPosition(this).X, Sidebar.Bounds.Width);
        e.Pointer.Capture(SidebarSplitter);
    }

    // Genişlik sidebar'ın MinWidth ve MaxWidth'i arasında kalır.
    private void OnSplitterPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_splitterDrag is not { } drag)
            return;
        Sidebar.Width = Math.Clamp(drag.Width + e.GetPosition(this).X - drag.X, Sidebar.MinWidth, Sidebar.MaxWidth);
    }

    // Bırakınca genişlik yazılır; uygulama bir dahaki açılışta bu genişlikle gelir.
    private void OnSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_splitterDrag is not { } drag)
            return;
        _splitterDrag = null;
        e.Pointer.Capture(null);
        if (Sidebar.Width != drag.Width)
            SaveSidebarWidth();
    }

    private void SaveSidebarWidth()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        var settings = vm.Settings.Load();
        settings.SidebarWidth = Sidebar.Width;
        vm.Settings.Save(settings);
    }

    // Başlık çubuğundan pencere taşınmaya başladı.
    public event Action? MoveStarted;

    // Başlık çubuğunun boş yeri: basılı tutup sürükleyince pencere taşınır; çift tık ekranı doldurur ya da eski boyuta döner.
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || (e.Source is Visual source
                && (source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null
                    || source.FindAncestorOfType<Button>(includeSelf: true) is not null)))
            return;
        e.Handled = true;
        if (e.ClickCount % 2 == 0)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        MoveStarted?.Invoke();
        BeginMoveDrag(e);
    }
}
