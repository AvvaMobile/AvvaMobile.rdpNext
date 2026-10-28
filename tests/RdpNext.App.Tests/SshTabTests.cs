using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;
using RdpNext.Ssh;

namespace RdpNext.App.Tests;

public class SshTabTests
{
    private static readonly SshHostKey HostKey = new("ssh-ed25519", "SHA256:yeni");
    private readonly List<FakeSshSession> _sessions = [];
    private readonly string _path;
    private readonly MainWindowViewModel _vm;
    private readonly Group _uretim;

    public SshTabTests()
    {
        var root = new Group();
        _uretim = new Group { Name = "Üretim", Username = "root" };
        root.Add(_uretim);
        _uretim.Add(new Server { Name = "linux-01", Host = "10.0.2.5", Port = 22, Protocol = Protocol.Ssh });
        _uretim.Add(new Server
        {
            Name = "linux-key", Host = "10.0.2.6", Port = 2200, Protocol = Protocol.Ssh, Auth = SshAuth.Key, KeyFile = "/k/id_ed25519",
        });
        _path = TestWindows.FileWith(root);
        _vm = TestWindows.ViewModel(_path, sshSessions: _sessions);
        _vm.Secrets.SetPassword(_uretim.Id, "klasor");
    }

    private ServerNodeViewModel Node(string name) =>
        _vm.Tree.Node<ServerNodeViewModel>(_vm.Tree.Root.AllServers().First(s => s.Name == name).Id);

    private SshTabViewModel Connect(string name = "linux-01")
    {
        _vm.ConnectCommand.Execute(Node(name));
        return Assert.IsType<SshTabViewModel>(_vm.Sessions.Selected);
    }

    private SshTabViewModel Connected(string name = "linux-01")
    {
        var tab = Connect(name);
        _sessions[^1].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        return tab;
    }

    private Server Reload(string name) => new ConnectionStore(_path).Load().Root.AllServers().First(s => s.Name == name);

    [AvaloniaFact]
    public void PasswordServerConnectsWithResolvedCredentials()
    {
        var tab = Connect();

        var options = Assert.Single(_sessions[0].Starts);
        Assert.Equal(("10.0.2.5", 22, "root", "klasor", (string?)null), (options.Host, options.Port, options.Username, options.Password, options.KeyFile));
        Assert.Equal(SessionState.Connecting, tab.State);
        Assert.Equal(ServerIcon.Ssh, tab.Icon);
    }

    [AvaloniaFact]
    public void MissingPasswordAsksCredentials()
    {
        _vm.Secrets.DeletePassword(_uretim.Id);

        var tab = Connect();

        Assert.Empty(_sessions);
        Assert.True(tab.IsAskingCredentials);
        Assert.Equal("root", tab.PromptUsername);
        Assert.False(tab.PromptShowsDomain);
        tab.PromptPassword = "yeni";
        tab.SignInCommand.Execute(null);
        Assert.Equal("yeni", Assert.Single(_sessions[0].Starts).Password);
    }

    [AvaloniaFact]
    public void AuthFailedAsksAgain()
    {
        var tab = Connect();

        _sessions[0].RaiseEnded(SshEndReason.AuthFailed);
        Dispatcher.UIThread.RunJobs();

        Assert.True(tab.IsAskingCredentials);
        Assert.Equal("root", tab.PromptUsername);
    }

    [AvaloniaFact]
    public void KeyServerPassesKeyFileAndPassphrase()
    {
        var key = Node("linux-key").Server;
        _vm.Secrets.SetPassword(key.Id, "anahtar123");

        Connect("linux-key");

        var options = Assert.Single(_sessions[0].Starts);
        Assert.Equal(("10.0.2.6", 2200, "root", (string?)null, "/k/id_ed25519", "anahtar123"),
            (options.Host, options.Port, options.Username, options.Password, options.KeyFile, options.KeyPassphrase));
    }

    // Anahtarla girişte parola yoksa yine de denenir; anahtar parolasızsa bağlanır.
    [AvaloniaFact]
    public void KeyServerWithoutPassphraseStillTries()
    {
        Connect("linux-key");

        Assert.Null(Assert.Single(_sessions[0].Starts).KeyPassphrase);
    }

    // Anahtar yolu boş kalmış eski kayıt: şifreyle denenmez, anahtar modunda kalır.
    [AvaloniaFact]
    public void KeyServerWithoutKeyFileStaysInKeyMode()
    {
        Node("linux-key").Server.KeyFile = null;

        Connect("linux-key");

        var options = Assert.Single(_sessions[0].Starts);
        Assert.Equal(("", (string?)null), (options.KeyFile, options.Password));
    }

    // Kullanıcı parmak izine bakarken bağlantı zaman aşımına uğrarsa, "Trust" sonrası kendiliğinden yeniden bağlanılır.
    [AvaloniaFact]
    public void TrustAfterTimeoutReconnects()
    {
        var tab = Connect();
        _ = _sessions[0].VerifyHostKey!(HostKey);
        Dispatcher.UIThread.RunJobs();
        tab.TrustHostKeyCommand.Execute(null);

        _sessions[0].RaiseEnded(SshEndReason.ConnectFailed);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, _sessions.Count);
        Assert.Equal(SessionState.Connecting, tab.State);
        Assert.False(tab.ShowDisconnected);

        // Yeniden denemede de olmazsa döngüye girmez, kopuk gösterilir.
        _sessions[1].RaiseEnded(SshEndReason.ConnectFailed);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, _sessions.Count);
        Assert.True(tab.ShowDisconnected);
    }

    [AvaloniaFact]
    public void KeyServerWithoutUserAsksOnlyUser()
    {
        _vm.Tree.Root.Children.OfType<Group>().Single().Username = null;

        var tab = Connect("linux-key");

        Assert.True(tab.IsAskingCredentials);
        Assert.False(tab.PromptNeedsPassword);
        tab.PromptUsername = "deneme";
        tab.SignInCommand.Execute(null);
        Assert.Equal("deneme", Assert.Single(_sessions[0].Starts).Username);
    }

    [AvaloniaFact]
    public void PassphraseNeededAsksPassphraseAndSaves()
    {
        var tab = Connect("linux-key");

        _sessions[0].RaiseEnded(SshEndReason.PassphraseNeeded);
        Dispatcher.UIThread.RunJobs();

        Assert.True(tab.IsAskingPassphrase);
        Assert.False(tab.ShowConnecting);
        Assert.False(tab.ShowDisconnected);
        tab.PromptPassphrase = "anahtar123";
        tab.PromptSave = true;
        tab.SubmitPassphraseCommand.Execute(null);

        Assert.False(tab.IsAskingPassphrase);
        Assert.Equal("anahtar123", _sessions[1].Starts.Single().KeyPassphrase);
        Assert.Equal("anahtar123", _vm.Secrets.GetPassword(Node("linux-key").Server.Id));
    }

    [AvaloniaFact]
    public void KeyUnusableShowsDisconnectedWithoutPasswordFallback()
    {
        var tab = Connect("linux-key");

        _sessions[0].RaiseEnded(SshEndReason.KeyUnusable, "dosya yok");
        Dispatcher.UIThread.RunJobs();

        Assert.True(tab.ShowDisconnected);
        Assert.Equal("The key file could not be used.", tab.DisconnectedTitle);
        Assert.False(tab.IsAskingCredentials);
        Assert.Single(_sessions);
    }

    [AvaloniaFact]
    public void FirstHostKeyAsksAndTrustSaves()
    {
        var tab = Connect();
        var answer = _sessions[0].VerifyHostKey!(HostKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(HostKey, tab.HostKeyPrompt);
        Assert.Null(tab.PreviousHostKey);
        Assert.False(tab.ShowConnecting);
        Assert.False(answer.IsCompleted);

        tab.TrustHostKeyCommand.Execute(null);

        Assert.True(answer.IsCompletedSuccessfully && answer.Result);
        Assert.Null(tab.HostKeyPrompt);
        Assert.Equal("SHA256:yeni", Reload("linux-01").HostKeyFingerprint);
    }

    [AvaloniaFact]
    public void SameHostKeyDoesNotAsk()
    {
        Node("linux-01").Server.HostKeyFingerprint = HostKey.Fingerprint;
        var tab = Connect();

        var answer = _sessions[0].VerifyHostKey!(HostKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(tab.HostKeyPrompt);
        Assert.True(answer.IsCompletedSuccessfully && answer.Result);
    }

    [AvaloniaFact]
    public void ChangedHostKeyAsksAndDoesNotConnect()
    {
        Node("linux-01").Server.HostKeyFingerprint = "SHA256:eski";
        var tab = Connect();

        var answer = _sessions[0].VerifyHostKey!(HostKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(HostKey, tab.HostKeyPrompt);
        Assert.Equal("SHA256:eski", tab.PreviousHostKey);
        Assert.False(answer.IsCompleted);
        Assert.Equal(SessionState.Connecting, tab.State);

        tab.CancelHostKeyCommand.Execute(null);
        Assert.True(answer.IsCompletedSuccessfully && !answer.Result);
        Assert.Equal("SHA256:eski", Node("linux-01").Server.HostKeyFingerprint);
    }

    [AvaloniaFact]
    public void ExitClosesTab()
    {
        var tab = Connected();

        _sessions[0].RaiseEnded(SshEndReason.Exited);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(tab, _vm.Sessions.Tabs);
        Assert.True(_sessions[0].Disposed);
        Assert.Null(Node("linux-01").State);
    }

    [AvaloniaFact]
    public void HostKeyRejectedClosesTab()
    {
        var tab = Connect();

        _sessions[0].RaiseEnded(SshEndReason.HostKeyRejected);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(tab, _vm.Sessions.Tabs);
    }

    [AvaloniaFact]
    public void DropShowsReconnectAndReconnectReuses()
    {
        var tab = Connected();

        _sessions[0].RaiseEnded(SshEndReason.Disconnected);
        Dispatcher.UIThread.RunJobs();
        Assert.True(tab.ShowDisconnected);
        Assert.Equal("Connection lost", tab.DisconnectedTitle);

        tab.ReconnectCommand.Execute(null);

        Assert.Equal(2, _sessions.Count);
        Assert.Equal(("root", "klasor"), (_sessions[1].Starts.Single().Username, _sessions[1].Starts.Single().Password));
        Assert.True(_sessions[0].Disposed);
        Assert.Equal(SessionState.Connecting, tab.State);
    }

    [AvaloniaFact]
    public void TreeDotFollowsState()
    {
        var node = Node("linux-01");
        Connect();
        Assert.True(node.IsConnecting);

        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        Assert.True(node.IsConnected);

        _sessions[0].RaiseEnded(SshEndReason.Disconnected);
        Dispatcher.UIThread.RunJobs();
        Assert.True(node.IsDisconnected);
    }

    [AvaloniaFact]
    public void OutputReachesTabAndOldSessionIgnored()
    {
        var tab = Connected();
        var received = new List<string>();
        tab.Output += bytes => received.Add(Encoding.UTF8.GetString(bytes));
        _sessions[0].RaiseEnded(SshEndReason.Disconnected);
        Dispatcher.UIThread.RunJobs();
        tab.ReconnectCommand.Execute(null);

        _sessions[0].RaiseOutput("eski"u8.ToArray());
        _sessions[1].RaiseOutput("yeni"u8.ToArray());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["yeni"], received);
    }

    // Arayüz meşgulken gelen parçalar tek seferde terminale verilir.
    [AvaloniaFact]
    public void OutputBurstFedOnce()
    {
        var tab = Connected();
        var received = new List<string>();
        tab.Output += bytes => received.Add(Encoding.UTF8.GetString(bytes));

        _sessions[0].RaiseOutput("bir "u8.ToArray());
        _sessions[0].RaiseOutput("iki "u8.ToArray());
        _sessions[0].RaiseOutput("üç"u8.ToArray());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["bir iki üç"], received);
        Assert.Contains("bir iki üç", string.Join("\n", tab.Terminal.Terminal.Engine.GetVisibleLines()));
    }

    [AvaloniaFact]
    public void SendGoesToSessionOnlyWhenConnected()
    {
        var tab = Connect();
        tab.Send("a"u8.ToArray());
        Assert.Empty(_sessions[0].Sent);

        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        tab.Send("ls\r"u8.ToArray());

        Assert.Equal("ls\r", Encoding.UTF8.GetString(Assert.Single(_sessions[0].Sent)));
    }

    [AvaloniaFact]
    public void TerminalSizeUsedForStartAndResize()
    {
        var node = Node("linux-01");
        _vm.ConnectCommand.Execute(node);
        var tab = Assert.IsType<SshTabViewModel>(_vm.Sessions.Selected);
        Assert.Equal((80, 24), (_sessions[0].Starts[0].Columns, _sessions[0].Starts[0].Rows));

        tab.TerminalSizeChanged(120, 40);
        Assert.Empty(_sessions[0].Resizes);
        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([(120, 40)], _sessions[0].Resizes);

        tab.TerminalSizeChanged(100, 30);
        Assert.Equal((100, 30), _sessions[0].Resizes[^1]);
    }

    [AvaloniaFact]
    public void ReconnectIgnoredWhileHostKeyCardOpen()
    {
        var tab = Connect();
        _ = _sessions[0].VerifyHostKey!(HostKey);
        Dispatcher.UIThread.RunJobs();

        tab.ReconnectCommand.Execute(null);

        Assert.Single(_sessions);
        Assert.Equal(HostKey, tab.HostKeyPrompt);
    }

    [AvaloniaFact]
    public void ConnectFailedSaysCouldNotConnectThenDropSaysLost()
    {
        var tab = Connect();

        _sessions[0].RaiseEnded(SshEndReason.ConnectFailed);
        Dispatcher.UIThread.RunJobs();
        Assert.True(tab.ShowDisconnected);
        Assert.Equal("Could not connect", tab.DisconnectedTitle);

        tab.ReconnectCommand.Execute(null);
        _sessions[1].RaiseConnected();
        _sessions[1].RaiseEnded(SshEndReason.Disconnected);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Connection lost", tab.DisconnectedTitle);
    }

    // Sekme açıkken sunucu RDP'ye çevrildi: bağlanınca eski SSH sekmesi kapanır, RDP sekmesi açılır.
    [AvaloniaFact]
    public void ProtocolChangeReplacesOpenTab()
    {
        var old = Connected();
        Node("linux-01").Server.Protocol = Protocol.Rdp;

        _vm.ConnectCommand.Execute(Node("linux-01"));

        Assert.IsType<SessionTabViewModel>(_vm.Sessions.Selected);
        Assert.DoesNotContain(old, _vm.Sessions.Tabs);
        Assert.True(_sessions[0].Disposed);
    }

    [AvaloniaFact]
    public void ReconnectAfterProtocolChangeOpensNewTab()
    {
        var old = Connected();
        _sessions[0].RaiseEnded(SshEndReason.Disconnected);
        Dispatcher.UIThread.RunJobs();
        Node("linux-01").Server.Protocol = Protocol.Rdp;

        old.ReconnectCommand.Execute(null);

        Assert.IsType<SessionTabViewModel>(_vm.Sessions.Selected);
        Assert.DoesNotContain(old, _vm.Sessions.Tabs);
        Assert.Single(_sessions);
    }

    [AvaloniaFact]
    public void RdpServerStillOpensRdpTab()
    {
        _vm.Tree.AddServer(new Server { Name = "web-01", Host = "10.0.1.11" }, null);

        _vm.ConnectCommand.Execute(Node("web-01"));

        Assert.IsType<SessionTabViewModel>(_vm.Sessions.Selected);
        Assert.Empty(_sessions);
    }
}
