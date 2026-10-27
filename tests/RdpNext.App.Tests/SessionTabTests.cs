using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Rdp;

namespace RdpNext.App.Tests;

public class SessionTabTests
{
    private readonly List<FakeRdpSession> _sessions = [];
    private readonly MainWindowViewModel _vm;

    public SessionTabTests() => _vm = TestWindows.ViewModel(sessions: _sessions);

    private ServerNodeViewModel Server(string name) => _vm.Tree.Node<ServerNodeViewModel>(
        _vm.Tree.Root.AllServers().First(s => s.Name == name).Id);

    private SessionTabViewModel Connect(string name)
    {
        _vm.ConnectCommand.Execute(Server(name));
        Dispatcher.UIThread.RunJobs();
        return Assert.IsType<SessionTabViewModel>(_vm.Sessions.Selected);
    }

    // web-01'in kendi kullanıcısı yok; Üretim'den devralır.
    private void GiveUretimCredentials()
    {
        var uretim = _vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == "Üretim");
        uretim.Username = "admin";
        uretim.Domain = "AVVA";
        _vm.Secrets.SetPassword(uretim.Id, "pw");
    }

    [AvaloniaFact]
    public void ConnectStartsSessionWithResolvedCredentials()
    {
        GiveUretimCredentials();

        Connect("web-01");

        var options = Assert.Single(Assert.Single(_sessions).Starts);
        Assert.Equal(("10.0.1.11", 3389, "admin", "AVVA", "pw"), (options.Host, options.Port, options.Username, options.Domain, options.Password));
        Assert.True(options.Width > 0 && options.Height > 0);
    }

    [AvaloniaFact]
    public void StateFollowsSession()
    {
        GiveUretimCredentials();
        var tab = Connect("web-01");
        Assert.Equal(SessionState.Connecting, tab.State);
        Assert.Equal(SessionState.Connecting, Server("web-01").State);

        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(SessionState.Connected, tab.State);
        Assert.True(Server("web-01").IsConnected);

        _sessions[0].RaiseEnded(RdpEndReason.Disconnected);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(SessionState.Disconnected, tab.State);
        Assert.Equal(SessionState.Disconnected, Server("web-01").State);
    }

    [AvaloniaFact]
    public void ClosingTabDisposesSession()
    {
        GiveUretimCredentials();
        var tab = Connect("web-01");

        _vm.CloseTabCommand.Execute(tab);

        Assert.True(_sessions[0].Disposed);
        Assert.Null(Server("web-01").State);
    }

    [AvaloniaFact]
    public async Task DeletingServerDisposesSession()
    {
        GiveUretimCredentials();
        Connect("web-01");
        var vm = TestWindows.ViewModel(sessions: _sessions, confirm: true);
        vm.ConnectCommand.Execute(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "sql-01").Id));

        await vm.DeleteNodeCommand.ExecuteAsync(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "sql-01").Id));

        Assert.True(_sessions[1].Disposed);
    }

    [AvaloniaFact]
    public void ShutdownDisposesAllSessions()
    {
        GiveUretimCredentials();
        Connect("web-01");
        Connect("sql-01");

        _vm.Shutdown();

        Assert.All(_sessions, s => Assert.True(s.Disposed));
        Assert.Empty(_vm.Sessions.Tabs.OfType<SessionTabViewModel>());
    }

    [AvaloniaFact]
    public void DoubleConnectReusesSession()
    {
        GiveUretimCredentials();
        var first = Connect("web-01");

        var second = Connect("web-01");

        Assert.Same(first, second);
        Assert.Single(Assert.Single(_sessions).Starts);
    }

    [AvaloniaFact]
    public void FrameUpdatesScreen()
    {
        GiveUretimCredentials();
        var tab = Connect("web-01");

        _sessions[0].RaiseFrame(new byte[2 * 2 * 4], 2, 2);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new Avalonia.PixelSize(2, 2), tab.Screen.Bitmap?.PixelSize);
    }
}
