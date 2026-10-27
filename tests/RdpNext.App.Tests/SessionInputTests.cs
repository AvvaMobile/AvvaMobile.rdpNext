using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Rdp;

namespace RdpNext.App.Tests;

public class SessionInputTests
{
    private readonly List<FakeRdpSession> _sessions = [];

    private (Window Window, Views.SessionView View) OpenSession()
    {
        var vm = TestWindows.ViewModel(sessions: _sessions);
        var uretim = vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == "Üretim");
        uretim.Username = "admin";
        vm.Secrets.SetPassword(uretim.Id, "pw");
        var window = new Views.MainWindow { DataContext = vm };
        window.Show();
        vm.ConnectCommand.Execute(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "web-01").Id));
        Dispatcher.UIThread.RunJobs();
        var view = window.Named<Views.SessionView>("SessionViewRoot");
        view.Focus();
        return (window, view);
    }

    [AvaloniaFact]
    public void KeyDownSendsScancodeToSession()
    {
        var (window, _) = OpenSession();

        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        window.KeyRelease(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);

        Assert.Equal([((ushort)0x4B, true, true), ((ushort)0x4B, true, false)], _sessions[0].Keys);
    }

    [AvaloniaFact]
    public void RightClickSendsButton2()
    {
        var (window, view) = OpenSession();
        var point = view.TranslatePoint(new Point(10, 20), window)!.Value;

        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);

        Assert.Contains((RdpMouseFlags.Down | RdpMouseFlags.Button2, 10, 20), _sessions[0].Mouse);
        Assert.Contains((RdpMouseFlags.Button2, 10, 20), _sessions[0].Mouse);
    }
}
