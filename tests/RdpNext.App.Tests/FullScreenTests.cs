using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Tests;

// Tam ekrana alınan her oturum kendi penceresinde açılır; ana pencere olduğu gibi kalır.
// O pencere kapanınca yalnız o oturum kapanır; kısayolla çıkınca oturum sekmesine döner.
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

    private static SessionTabViewModel Connect(MainWindowViewModel vm, string name)
    {
        var server = vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == name).Id);
        vm.ConnectCommand.Execute(server);
        Dispatcher.UIThread.RunJobs();
        return vm.Sessions.Find<SessionTabViewModel>(t => t.Server == server)!;
    }

    [AvaloniaFact]
    public void ToggleMovesSessionToOwnFullScreenWindow()
    {
        var (window, vm) = Open();
        var tab = Connect(vm, "web-01");
        var state = window.WindowState;

        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var own = Assert.Single(window.SessionWindows);
        Assert.Same(tab, own.DataContext);
        Assert.Equal(WindowState.FullScreen, own.WindowState);
        Assert.True(own.IsVisible);
        Assert.DoesNotContain(tab, vm.Sessions.Tabs);
        Assert.Equal(state, window.WindowState);
        Assert.False(_sessions[0].Disposed);
    }

    [AvaloniaFact]
    public void ToggleBackReturnsSessionToTab()
    {
        var (window, vm) = Open();
        var tab = Connect(vm, "web-01");
        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        vm.ToggleFullScreenCommand.Execute(tab);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(window.SessionWindows);
        Assert.Same(tab, vm.Sessions.Selected);
        Assert.False(_sessions[0].Disposed);
    }

    [AvaloniaFact]
    public void ClosingFullScreenWindowClosesOnlyThatSession()
    {
        var (window, vm) = Open();
        var sql = Connect(vm, "sql-01");
        Connect(vm, "web-01");
        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        window.SessionWindows.Single().Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(_sessions[1].Disposed);
        Assert.False(_sessions[0].Disposed);
        Assert.True(window.IsVisible);
        Assert.Equal([sql], vm.Sessions.Tabs);
        Assert.Null(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "web-01").Id).State);
    }

    [AvaloniaFact]
    public void EachFullScreenSessionHasOwnWindow()
    {
        var (window, vm) = Open();
        Connect(vm, "web-01");
        vm.ToggleFullScreenCommand.Execute(null);
        Connect(vm, "sql-01");
        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, window.SessionWindows.Count);
    }

    [AvaloniaFact]
    public void FullScreenServerOpensOwnWindow()
    {
        var (window, vm) = Open(uretim => uretim.Children.OfType<Server>().First(s => s.Name == "web-01").Screen = ScreenMode.FullScreen);

        Connect(vm, "web-01");

        Assert.Single(window.SessionWindows);
        Assert.NotEqual(WindowState.FullScreen, window.WindowState);
    }

    // Yalnız oturum sekmesi tam ekran olur.
    [AvaloniaFact]
    public void ToggleOnEditorDoesNothing()
    {
        var (window, vm) = Open();
        vm.NewServer();

        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(window.SessionWindows);
    }

    [AvaloniaFact]
    public void ShutdownClosesFullScreenWindows()
    {
        var (window, vm) = Open();
        Connect(vm, "web-01");
        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        vm.Shutdown();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(window.SessionWindows);
        Assert.True(_sessions[0].Disposed);
    }

    // Kısayol oturum klavyeyi tutarken de çalışır; basılı kalan Ctrl sunucuda bırakılır.
    [AvaloniaFact]
    public void ShortcutTogglesBothWaysAndReleasesKeys()
    {
        var (window, vm) = Open();
        var tab = Connect(vm, "web-01");
        window.Named<Views.SessionView>("SessionViewRoot").Focus();
        var (key, mods, physical) = OperatingSystem.IsMacOS()
            ? (Key.F, RawInputModifiers.Control | RawInputModifiers.Meta, PhysicalKey.F)
            : (Key.Enter, RawInputModifiers.Control | RawInputModifiers.Alt, PhysicalKey.Enter);

        window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft, null);
        window.KeyPress(key, mods, physical, null);
        Dispatcher.UIThread.RunJobs();
        var own = Assert.Single(window.SessionWindows);
        Assert.Equal(((ushort)0x1D, false, false), _sessions[0].Keys[^1]);

        own.KeyPress(key, mods, physical, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(window.SessionWindows);
        Assert.Same(tab, vm.Sessions.Selected);
    }
}
