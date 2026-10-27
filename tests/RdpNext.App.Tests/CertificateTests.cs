using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;
using RdpNext.Rdp;

namespace RdpNext.App.Tests;

public class CertificateTests
{
    private static readonly RdpCertificate Cert = new("CN=WEB", "CN=WEB", "sha256 4F:1A", Changed: false);
    private readonly List<FakeRdpSession> _sessions = [];
    private readonly string _path = TestWindows.SampleFile();
    private readonly MainWindowViewModel _vm;

    public CertificateTests()
    {
        _vm = TestWindows.ViewModel(_path, sessions: _sessions);
        var uretim = _vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == "Üretim");
        uretim.Username = "admin";
        _vm.Secrets.SetPassword(uretim.Id, "pw");
    }

    private ServerNodeViewModel Web => _vm.Tree.Node<ServerNodeViewModel>(_vm.Tree.Root.AllServers().First(s => s.Name == "web-01").Id);

    private (SessionTabViewModel Tab, Task<bool> Answer) ConnectAndAsk()
    {
        _vm.ConnectCommand.Execute(Web);
        var tab = Assert.IsType<SessionTabViewModel>(_vm.Sessions.Selected);
        var answer = _sessions[0].VerifyCertificate!(Cert);
        Dispatcher.UIThread.RunJobs();
        return (tab, answer);
    }

    [AvaloniaFact]
    public void UnknownCertificateAsks()
    {
        var (tab, answer) = ConnectAndAsk();

        Assert.Equal(Cert, tab.CertificatePrompt);
        Assert.False(answer.IsCompleted);
    }

    [AvaloniaFact]
    public void TrustedCertificateDoesNotAsk()
    {
        Web.Server.CertificateFingerprint = Cert.Fingerprint;

        var (tab, answer) = ConnectAndAsk();

        Assert.Null(tab.CertificatePrompt);
        Assert.True(answer.IsCompletedSuccessfully && answer.Result);
    }

    [AvaloniaFact]
    public void ChangedCertificateAsksAgain()
    {
        Web.Server.CertificateFingerprint = "sha256 00:00";

        var (tab, _) = ConnectAndAsk();

        Assert.NotNull(tab.CertificatePrompt);
    }

    [AvaloniaFact]
    public void TrustSavesFingerprint()
    {
        var (tab, answer) = ConnectAndAsk();

        tab.TrustCertificateCommand.Execute(null);

        Assert.True(answer.Result);
        Assert.Null(tab.CertificatePrompt);
        Assert.Equal(Cert.Fingerprint, new ConnectionStore(_path).Load().Root.AllServers().First(s => s.Name == "web-01").CertificateFingerprint);
    }

    [AvaloniaFact]
    public void CancelClosesTab()
    {
        var (tab, answer) = ConnectAndAsk();

        tab.CancelCertificateCommand.Execute(null);
        Assert.False(answer.Result);
        _sessions[0].RaiseEnded(RdpEndReason.CertificateRejected);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(tab, _vm.Sessions.Tabs);
        Assert.True(_sessions[0].Disposed);
    }

    [AvaloniaFact]
    public void CardShowsCertificate()
    {
        var window = new Views.MainWindow { DataContext = _vm };
        window.Show();
        ConnectAndAsk();

        var card = window.Named<Control>("CertificateCard");

        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains(card.Texts(), t => t.Text == "Untrusted certificate");
        Assert.Contains(card.Texts(), t => t.Text == "sha256 4F:1A");
    }
}
