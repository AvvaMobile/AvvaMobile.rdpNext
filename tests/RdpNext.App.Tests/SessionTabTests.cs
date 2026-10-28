using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Time.Testing;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Rdp;

namespace RdpNext.App.Tests;

public class SessionTabTests
{
    private readonly List<FakeRdpSession> _sessions = [];
    private readonly FakeTimeProvider _time = new();
    private readonly MainWindowViewModel _vm;

    public SessionTabTests() => _vm = TestWindows.ViewModel(sessions: _sessions, time: _time);

    private SessionTabViewModel Connected(string name)
    {
        GiveUretimCredentials();
        var tab = Connect(name);
        _sessions[^1].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        return tab;
    }

    // Pencere boyutu değişirken sunucuya son değişiklikten 300 ms sonra tek istek gider.
    [AvaloniaFact]
    public void ResizeIsDebounced()
    {
        var tab = Connected("web-01");

        tab.ViewSizeChanged(1000, 700);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        tab.ViewSizeChanged(1100, 700);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        tab.ViewSizeChanged(1200, 700);
        _time.Advance(TimeSpan.FromMilliseconds(299));
        Assert.Empty(_sessions[0].Resizes);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal([(1200, 700)], _sessions[0].Resizes);
    }

    // RDP: genişlik 200..8192 ve çift, yükseklik 200..8192.
    [AvaloniaTheory]
    [InlineData(1001, 150, 1000, 200)]
    [InlineData(9000, 9000, 8192, 8192)]
    [InlineData(0, 0, 200, 200)]
    public void ResizeIsClampedAndEven(double width, double height, int expectedWidth, int expectedHeight)
    {
        var tab = Connected("web-01");

        tab.ViewSizeChanged(width, height);
        _time.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Equal([(expectedWidth, expectedHeight)], _sessions[0].Resizes);
    }

    // Bağlanırken görünüm boyutu değiştiyse, bağlanınca sunucudan bu boyut istenir.
    [AvaloniaFact]
    public void SizeChangedWhileConnectingIsSentAfterConnect()
    {
        GiveUretimCredentials();
        var tab = Connect("web-01");
        tab.ViewSizeChanged(1374, 996);

        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        _time.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Equal([(1374, 996)], _sessions[0].Resizes);
    }

    [AvaloniaFact]
    public void ResizeBeforeConnectedIsIgnored()
    {
        GiveUretimCredentials();
        var tab = Connect("web-01");

        tab.ViewSizeChanged(1000, 700);
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(_sessions[0].Resizes);
    }

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
        var uretim = vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == "Üretim");
        uretim.Username = "admin";
        vm.Secrets.SetPassword(uretim.Id, "pw");
        vm.ConnectCommand.Execute(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "sql-01").Id));

        await vm.DeleteNodeCommand.ExecuteAsync(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "sql-01").Id));

        Assert.True(_sessions[1].Disposed);
    }

    // Kapanan sekme sunucuya bağlı kalmaz ve görüntüsünü bırakır; yoksa her aç-kapa belleği büyütür.
    [AvaloniaFact]
    public void ClosedTabReleasesServerAndBitmap()
    {
        GiveUretimCredentials();
        var tab = Connect("web-01");
        _sessions[0].RaiseFrame(new byte[2 * 2 * 4], 2, 2);
        Dispatcher.UIThread.RunJobs();
        var changes = 0;
        tab.PropertyChanged += (_, _) => changes++;

        _vm.CloseTabCommand.Execute(tab);
        Dispatcher.UIThread.RunJobs();
        Server("web-01").Refresh();

        Assert.Equal(0, changes);
        Assert.Null(tab.Screen.Bitmap);
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
