using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;
using RdpNext.Rdp;

namespace RdpNext.App.Tests;

public class ReconnectTests
{
    private readonly List<FakeRdpSession> _sessions = [];
    private readonly MainWindowViewModel _vm;

    public ReconnectTests()
    {
        _vm = TestWindows.ViewModel(TestWindows.SampleFile(), sessions: _sessions);
        var uretim = _vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == "Üretim");
        uretim.Username = "admin";
        _vm.Secrets.SetPassword(uretim.Id, "pw");
    }

    private ServerNodeViewModel Web => _vm.Tree.Node<ServerNodeViewModel>(_vm.Tree.Root.AllServers().First(s => s.Name == "web-01").Id);

    private SessionTabViewModel Connected()
    {
        _vm.ConnectCommand.Execute(Web);
        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        return Assert.IsType<SessionTabViewModel>(_vm.Sessions.Selected);
    }

    private (Views.MainWindow Window, SessionTabViewModel Tab) ConnectedInWindow()
    {
        var window = new Views.MainWindow { DataContext = _vm };
        window.Show();
        return (window, Connected());
    }

    // Kullanıcı Windows'ta sign out yaptı: sekme kapanır (mstsc gibi).
    [AvaloniaFact]
    public void UserLogoffClosesTab()
    {
        var tab = Connected();

        _sessions[0].RaiseEnded(RdpEndReason.UserLogoff);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(tab, _vm.Sessions.Tabs);
    }

    [AvaloniaFact]
    public void DisconnectKeepsTabAndShowsReconnect()
    {
        var (window, tab) = ConnectedInWindow();

        _sessions[0].RaiseEnded(RdpEndReason.Disconnected);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(tab, _vm.Sessions.Tabs);
        var card = window.Named<Control>("DisconnectedCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains(card.Texts(), t => t.Text == "Connection lost");
        Assert.Contains(card.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Reconnect");
    }

    [AvaloniaFact]
    public void ReconnectStartsNewSessionInSameTab()
    {
        var tab = Connected();
        _sessions[0].RaiseEnded(RdpEndReason.Disconnected);
        Dispatcher.UIThread.RunJobs();

        tab.ReconnectCommand.Execute(null);

        Assert.Equal(2, _sessions.Count);
        Assert.True(_sessions[0].Disposed);
        Assert.Equal(("admin", "pw"), (_sessions[1].Starts[0].Username, _sessions[1].Starts[0].Password));
        Assert.Equal(SessionState.Connecting, tab.State);
        Assert.Same(tab, _vm.Sessions.Selected);
    }

    [AvaloniaFact]
    public void ConnectingShowsMessage()
    {
        var window = new Views.MainWindow { DataContext = _vm };
        window.Show();
        _vm.ConnectCommand.Execute(Web);
        Dispatcher.UIThread.RunJobs();

        var card = window.Named<Control>("ConnectingCard");

        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains(card.Texts(), t => t.Text == "Connecting to web-01");
    }

    [AvaloniaFact]
    public void TabMenuHasReconnectOnlyForSessions()
    {
        var tab = Connected();
        _vm.NewServer();

        Assert.True(tab.CanReconnect);
        Assert.False(_vm.Sessions.Selected!.CanReconnect);

        _vm.ReconnectTabCommand.Execute(tab);
        Assert.Equal(2, _sessions.Count);
    }

    // Ağaçta: bağlanırken halka, bağlıyken yeşil nokta, koptuğunda turuncu nokta.
    [AvaloniaFact]
    public void TreeRowShowsSessionState()
    {
        var (window, _) = TestWindows.OpenExpanded();
        var vm = (MainWindowViewModel)window.DataContext!;
        var node = vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "web-01").Id);
        var row = window.Item("web-01");

        node.State = SessionState.Connecting;
        Dispatcher.UIThread.RunJobs();
        Assert.True(row.Named<Control>("ConnectingMark").IsEffectivelyVisible);

        node.State = SessionState.Connected;
        Dispatcher.UIThread.RunJobs();
        Assert.True(row.Named<Ellipse>("ConnectedMark").IsEffectivelyVisible);
        Assert.False(row.Named<Control>("ConnectingMark").IsEffectivelyVisible);

        node.State = SessionState.Disconnected;
        Dispatcher.UIThread.RunJobs();
        Assert.True(row.Named<Ellipse>("DisconnectedMark").IsEffectivelyVisible);
        Assert.False(row.Named<Ellipse>("ConnectedMark").IsEffectivelyVisible);
    }
}
