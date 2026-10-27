using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;
using RdpNext.Rdp;

namespace RdpNext.App.Tests;

public class CredentialsPromptTests
{
    private readonly List<FakeRdpSession> _sessions = [];
    private readonly string _path = TestWindows.SampleFile();
    private readonly MainWindowViewModel _vm;

    public CredentialsPromptTests() => _vm = TestWindows.ViewModel(_path, sessions: _sessions);

    private ServerNodeViewModel Web => _vm.Tree.Node<ServerNodeViewModel>(_vm.Tree.Root.AllServers().First(s => s.Name == "web-01").Id);

    private SessionTabViewModel Connect()
    {
        _vm.ConnectCommand.Execute(Web);
        Dispatcher.UIThread.RunJobs();
        return Assert.IsType<SessionTabViewModel>(_vm.Sessions.Selected);
    }

    private void GiveUretimCredentials()
    {
        var uretim = _vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == "Üretim");
        uretim.Username = "admin";
        _vm.Secrets.SetPassword(uretim.Id, "pw");
    }

    [AvaloniaFact]
    public void MissingPasswordAsksBeforeConnect()
    {
        var tab = Connect();

        Assert.True(tab.IsAskingCredentials);
        Assert.All(_sessions, s => Assert.Empty(s.Starts));
    }

    [AvaloniaFact]
    public void AuthFailedAsksAndReconnects()
    {
        GiveUretimCredentials();
        var tab = Connect();

        _sessions[0].RaiseEnded(RdpEndReason.AuthFailed);
        Dispatcher.UIThread.RunJobs();
        Assert.True(tab.IsAskingCredentials);
        Assert.Equal("admin", tab.PromptUsername);

        tab.PromptPassword = "new";
        tab.SignInCommand.Execute(null);

        var options = Assert.Single(_sessions[^1].Starts);
        Assert.Equal(("admin", "new"), (options.Username, options.Password));
        Assert.True(_sessions[0].Disposed);
        Assert.False(tab.IsAskingCredentials);
        Assert.Equal(SessionState.Connecting, tab.State);
    }

    [AvaloniaFact]
    public void SaveWritesServerAndSecret()
    {
        var tab = Connect();
        (tab.PromptUsername, tab.PromptPassword, tab.PromptDomain, tab.PromptSave) = ("bob", "x", "D", true);

        tab.SignInCommand.Execute(null);

        var saved = new ConnectionStore(_path).Load().Root.AllServers().First(s => s.Name == "web-01");
        Assert.Equal(("bob", "D"), (saved.Username, saved.Domain));
        Assert.Equal("x", _vm.Secrets.GetPassword(saved.Id));
        Assert.Equal(("bob", "x", "D"), (_sessions[^1].Starts[0].Username, _sessions[^1].Starts[0].Password, _sessions[^1].Starts[0].Domain));
    }

    [AvaloniaFact]
    public void NoSaveLeavesServerUnchanged()
    {
        var tab = Connect();
        (tab.PromptUsername, tab.PromptPassword) = ("bob", "x");

        tab.SignInCommand.Execute(null);

        var saved = new ConnectionStore(_path).Load().Root.AllServers().First(s => s.Name == "web-01");
        Assert.Null(saved.Username);
        Assert.Null(_vm.Secrets.GetPassword(saved.Id));
        Assert.Single(_sessions[^1].Starts);
    }

    [AvaloniaFact]
    public void CancelClosesTab()
    {
        var tab = Connect();

        tab.CancelSignInCommand.Execute(null);

        Assert.DoesNotContain(tab, _vm.Sessions.Tabs);
    }

    [AvaloniaFact]
    public void CardShowsSignIn()
    {
        var window = new Views.MainWindow { DataContext = _vm };
        window.Show();
        Connect();

        var card = window.Named<Control>("CredentialsCard");

        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains(card.Texts(), t => t.Text == "Sign in to web-01");
    }
}
