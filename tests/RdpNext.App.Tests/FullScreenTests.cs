using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Tests;

public class FullScreenTests
{
    private readonly List<FakeRdpSession> _sessions = [];

    private (Views.MainWindow Window, MainWindowViewModel Vm) Open(Action<Group>? change = null)
    {
        var vm = TestWindows.ViewModel(sessions: _sessions);
        var uretim = vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == "Üretim");
        uretim.Username = "admin";
        vm.Secrets.SetPassword(uretim.Id, "pw");
        change?.Invoke(uretim);
        var window = new Views.MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    private static void Connect(MainWindowViewModel vm, string name)
    {
        vm.ConnectCommand.Execute(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == name).Id));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ToggleHidesSidebarAndTabs()
    {
        var (window, vm) = Open();
        Connect(vm, "web-01");

        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.FullScreen, window.WindowState);
        Assert.False(window.Named<Control>("Sidebar").IsVisible);
        Assert.False(window.Named<Control>("TitleBar").IsVisible);
        Assert.True(window.Named<Control>("SessionViewRoot").IsEffectivelyVisible);
        // Tam ekranda oturum kenardan kenara: çerçevenin boşluğu, köşesi ve kenarlığı yok.
        var frame = window.Named<Border>("ContentFrame");
        Assert.Equal((new Avalonia.Thickness(0), new Avalonia.CornerRadius(0), new Avalonia.Thickness(0)),
            (frame.Margin, frame.CornerRadius, frame.BorderThickness));
    }

    [AvaloniaFact]
    public void ToggleTwiceRestoresWindowState()
    {
        var (window, vm) = Open();
        Connect(vm, "web-01");
        window.WindowState = WindowState.Maximized;

        vm.ToggleFullScreenCommand.Execute(null);
        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.True(window.Named<Control>("Sidebar").IsVisible);
        Assert.True(window.Named<Control>("TitleBar").IsVisible);
    }

    // Yalnız oturum sekmesi tam ekran olur.
    [AvaloniaFact]
    public void ToggleOnEditorDoesNothing()
    {
        var (window, vm) = Open();
        vm.NewServer();

        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(vm.FullScreenSession);
        Assert.NotEqual(WindowState.FullScreen, window.WindowState);
    }

    [AvaloniaFact]
    public void FullScreenServerOpensFullScreen()
    {
        var (window, vm) = Open(uretim => uretim.Children.OfType<Server>().First(s => s.Name == "web-01").Screen = ScreenMode.FullScreen);

        Connect(vm, "web-01");

        Assert.NotNull(vm.FullScreenSession);
        Assert.Equal(WindowState.FullScreen, window.WindowState);
    }

    [AvaloniaFact]
    public void ClosingFullScreenSessionLeavesFullScreen()
    {
        var (window, vm) = Open();
        Connect(vm, "web-01");
        vm.ToggleFullScreenCommand.Execute(null);

        vm.CloseTabCommand.Execute(vm.FullScreenSession!);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(vm.FullScreenSession);
        Assert.NotEqual(WindowState.FullScreen, window.WindowState);
        Assert.True(window.Named<Control>("TitleBar").IsVisible);
    }

    // Tam ekran kısayolu oturum klavyeyi tutarken de çalışır; basılı kalan Ctrl sunucuda bırakılır.
    [AvaloniaFact]
    public void ShortcutTogglesFromSessionAndReleasesKeys()
    {
        var (window, vm) = Open();
        Connect(vm, "web-01");
        window.Named<Views.SessionView>("SessionViewRoot").Focus();
        var (key, mods, physical) = OperatingSystem.IsMacOS()
            ? (Avalonia.Input.Key.F, Avalonia.Input.RawInputModifiers.Control | Avalonia.Input.RawInputModifiers.Meta, Avalonia.Input.PhysicalKey.F)
            : (Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.Control | Avalonia.Input.RawInputModifiers.Alt, Avalonia.Input.PhysicalKey.Enter);

        Avalonia.Headless.HeadlessWindowExtensions.KeyPress(window, Avalonia.Input.Key.LeftCtrl, Avalonia.Input.RawInputModifiers.Control,
            Avalonia.Input.PhysicalKey.ControlLeft, null);
        Avalonia.Headless.HeadlessWindowExtensions.KeyPress(window, key, mods, physical, null);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(vm.FullScreenSession);
        Assert.Equal(((ushort)0x1D, false, false), _sessions[0].Keys[^1]);
    }
}
