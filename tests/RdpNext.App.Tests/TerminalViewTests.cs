using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;
using RdpNext.Ssh;
using SvcSystems.UI.Terminal;

namespace RdpNext.App.Tests;

// SSH sekmesinin terminal ekranı: çıktı terminale, yazılan sunucuya gider; kartlar RDP ile aynı yerde.
public class TerminalViewTests
{
    private readonly List<FakeSshSession> _sessions = [];

    private (Views.MainWindow Window, MainWindowViewModel Vm, SshTabViewModel Tab) Open(Action<Server>? change = null)
    {
        var root = new Group();
        var uretim = new Group { Name = "Üretim", Username = "root" };
        root.Add(uretim);
        var server = new Server { Name = "linux-01", Host = "10.0.2.5", Port = 22, Protocol = Protocol.Ssh };
        change?.Invoke(server);
        uretim.Add(server);
        var vm = TestWindows.ViewModel(TestWindows.FileWith(root), sshSessions: _sessions);
        vm.Secrets.SetPassword(uretim.Id, "pw");
        var window = new Views.MainWindow { DataContext = vm, Width = 1200, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        vm.ConnectCommand.Execute(vm.Tree.Node<ServerNodeViewModel>(server.Id));
        Dispatcher.UIThread.RunJobs();
        return (window, vm, Assert.IsType<SshTabViewModel>(vm.Sessions.Selected));
    }

    private (Views.MainWindow Window, MainWindowViewModel Vm, SshTabViewModel Tab) Connected()
    {
        var opened = Open();
        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        return opened;
    }

    private static TerminalControl Terminal(Window window) => window.GetVisualDescendants().OfType<TerminalControl>().Single();

    private static string Screen(SshTabViewModel tab) => string.Join("\n", tab.Terminal.Terminal.Engine.GetVisibleLines());

    private string Sent() => string.Concat(_sessions[0].Sent.Select(b => Encoding.UTF8.GetString(b)));

    [AvaloniaFact]
    public void OutputReachesTerminal()
    {
        var (window, _, tab) = Connected();

        _sessions[0].RaiseOutput(Encoding.UTF8.GetBytes("merhaba ğüşİ\r\n"));
        Dispatcher.UIThread.RunJobs();

        Assert.Same(tab.Terminal, Terminal(window).Model);
        Assert.Contains("merhaba ğüşİ", Screen(tab));
    }

    [AvaloniaFact]
    public void TypingSendsBytes()
    {
        var (window, _, _) = Connected();
        Terminal(window).Focus();

        window.KeyTextInput("ls");
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("ls\r", Sent());
    }

    [AvaloniaFact]
    public void ResizeSendsColumnsAndRows()
    {
        var (window, _, _) = Connected();
        _sessions[0].Resizes.Clear();

        window.Width = 700;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();

        var (columns, rows) = Assert.Single(_sessions[0].Resizes.TakeLast(1));
        Assert.InRange(columns, 20, 200);
        Assert.InRange(rows, 5, 100);
    }

    private static (RawInputModifiers Modifiers, Action<Window, PhysicalKey, Key> Press) Shortcut() => OperatingSystem.IsMacOS()
        ? (RawInputModifiers.Meta, (w, physical, key) => w.KeyPress(key, RawInputModifiers.Meta, physical, null))
        : (RawInputModifiers.Control | RawInputModifiers.Shift,
            (w, physical, key) => w.KeyPress(key, RawInputModifiers.Control | RawInputModifiers.Shift, physical, null));

    [AvaloniaFact]
    public async Task PasteShortcutSendsClipboardOnce()
    {
        var (window, _, _) = Connected();
        await window.Clipboard!.SetTextAsync("echo ğ");
        Terminal(window).Focus();

        Shortcut().Press(window, PhysicalKey.V, Key.V);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("echo ğ", Sent());
    }

    [AvaloniaFact]
    public async Task CopyShortcutCopiesSelection()
    {
        var (window, _, tab) = Connected();
        _sessions[0].RaiseOutput("kopyala-beni"u8.ToArray());
        Dispatcher.UIThread.RunJobs();
        Terminal(window).Focus();
        tab.Terminal.SelectAll();

        Shortcut().Press(window, PhysicalKey.C, Key.C);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("kopyala-beni", await window.Clipboard!.TryGetTextAsync());
        Assert.Empty(_sessions[0].Sent);
    }

    [AvaloniaFact]
    public void HostKeyCardShowsOldAndNewFingerprint()
    {
        var (window, _, tab) = Open(s => s.HostKeyFingerprint = "SHA256:eski");

        _ = _sessions[0].VerifyHostKey!(new SshHostKey("ssh-ed25519", "SHA256:yeni"));
        Dispatcher.UIThread.RunJobs();

        var card = window.Named<Control>("HostKeyCard");
        Assert.True(card.IsEffectivelyVisible);
        var texts = card.Texts().Select(t => t.Text).ToList();
        Assert.Contains("Host key changed", texts);
        Assert.Contains("SHA256:eski", texts);
        Assert.Contains("SHA256:yeni", texts);
        Assert.Contains(card.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Trust new key");
        Assert.False(window.Named<Control>("ConnectingCard").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void FirstHostKeyCardHasNoPrevious()
    {
        var (window, _, _) = Open();

        _ = _sessions[0].VerifyHostKey!(new SshHostKey("ssh-ed25519", "SHA256:yeni"));
        Dispatcher.UIThread.RunJobs();

        var card = window.Named<Control>("HostKeyCard");
        Assert.Contains(card.Texts(), t => t.Text == "Unknown host key");
        Assert.DoesNotContain(card.Texts(), t => t.Text == "Previous");
        Assert.Contains(card.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Trust");
    }

    [AvaloniaFact]
    public void PassphraseCardHasSingleField()
    {
        var (window, _, _) = Open(s =>
        {
            s.Auth = SshAuth.Key;
            s.KeyFile = "/k/id";
        });

        _sessions[0].RaiseEnded(SshEndReason.PassphraseNeeded);
        Dispatcher.UIThread.RunJobs();

        var card = window.Named<Control>("PassphraseCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Single(card.GetVisualDescendants().OfType<TextBox>());
        Assert.Contains(card.Texts(), t => t.Text == "Key passphrase");
    }

    [AvaloniaFact]
    public void SignInCardHidesDomainForSsh()
    {
        var (window, vm, _) = Open();

        _sessions[0].RaiseEnded(SshEndReason.AuthFailed);
        Dispatcher.UIThread.RunJobs();

        var card = window.Named<Control>("CredentialsCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.DoesNotContain(card.Texts(), t => t.Text == "Domain");
        Assert.Contains(card.Texts(), t => t.Text == "Password");
    }

    [AvaloniaFact]
    public void KeyUnusableShowsMessageAndReconnect()
    {
        var (window, _, _) = Open(s =>
        {
            s.Auth = SshAuth.Key;
            s.KeyFile = "/k/yok";
        });

        _sessions[0].RaiseEnded(SshEndReason.KeyUnusable, "yok");
        Dispatcher.UIThread.RunJobs();

        var card = window.Named<Control>("DisconnectedCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains(card.Texts(), t => t.Text == "The key file could not be used.");
    }

    [AvaloniaFact]
    public void FullScreenOpensTerminalInOwnWindow()
    {
        var (window, vm, tab) = Connected();

        vm.ToggleFullScreenCommand.Execute(tab);
        Dispatcher.UIThread.RunJobs();

        var own = Assert.Single(window.SessionWindows);
        Assert.Same(tab.Terminal, own.GetVisualDescendants().OfType<TerminalControl>().Single().Model);

        vm.ToggleFullScreenCommand.Execute(tab);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(window.SessionWindows);
        Assert.Same(tab, vm.Sessions.Selected);
    }

    // RDP oturumunun kartları ortak parçaya taşındı; RDP sekmesinde domain hâlâ sorulur.
    [AvaloniaFact]
    public void RdpSignInCardStillShowsDomain()
    {
        var vm = TestWindows.ViewModel();
        var window = new Views.MainWindow { DataContext = vm };
        window.Show();
        vm.ConnectCommand.Execute(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First().Id));
        Dispatcher.UIThread.RunJobs();

        var card = window.Named<Control>("CredentialsCard");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains(card.Texts(), t => t.Text == "Domain");
    }
}
