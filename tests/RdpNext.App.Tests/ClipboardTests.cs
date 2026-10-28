using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Tests;

public class ClipboardTests
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
        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        return (window, window.Named<Views.SessionView>("SessionViewRoot"));
    }

    [AvaloniaFact]
    public async Task RemoteTextGoesToLocalClipboard()
    {
        var (window, _) = OpenSession();

        _sessions[0].RaiseClipboardText("şöyle böyle");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("şöyle böyle", await window.Clipboard!.TryGetTextAsync());
    }

    [AvaloniaFact]
    public async Task FocusSendsLocalText()
    {
        var (window, view) = OpenSession();
        await window.Clipboard!.SetTextAsync("merhaba");
        window.Named<TextBox>("SearchBox").Focus();

        view.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("merhaba", _sessions[0].LocalClipboard);
    }

    // Başka uygulamada kopyalayıp pencereye dönünce (oturum zaten odaktayken) yeni metin sunucuya gider.
    [AvaloniaFact]
    public async Task ReturningToWindowSendsNewLocalText()
    {
        var (window, view) = OpenSession();
        view.Focus();
        Dispatcher.UIThread.RunJobs();
        await window.Clipboard!.SetTextAsync("yeni metin");

        var other = new Window();
        other.Show();
        other.Activate();
        Dispatcher.UIThread.RunJobs();
        window.Activate();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("yeni metin", _sessions[0].LocalClipboard);
    }

    // Mac panosu değişmediyse sunucuya yeniden duyurulmaz; yoksa Windows içindeki kopyalamanın üstüne yazar.
    [AvaloniaFact]
    public async Task UnchangedLocalTextIsNotSentAgain()
    {
        var (window, view) = OpenSession();
        await window.Clipboard!.SetTextAsync("aynı");
        window.Named<TextBox>("SearchBox").Focus();
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        window.Named<TextBox>("SearchBox").Focus();
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, _sessions[0].LocalClipboardSets);
    }

    // Sunucudan gelip Mac panosuna yazılan metin sunucuya geri duyurulmaz.
    [AvaloniaFact]
    public void RemoteTextIsNotSentBack()
    {
        var (window, view) = OpenSession();
        _sessions[0].RaiseClipboardText("sunucudan");
        Dispatcher.UIThread.RunJobs();
        var before = _sessions[0].LocalClipboardSets;

        window.Named<TextBox>("SearchBox").Focus();
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, _sessions[0].LocalClipboardSets);
    }

    // Sunucuda metin dışı bir şey (dosya) kopyalandı: Mac'teki eski metin sunucuya duyurulup onun üstüne yazmaz.
    [AvaloniaFact]
    public async Task ServerFileCopyIsNotOverwritten()
    {
        var (window, view) = OpenSession();
        await window.Clipboard!.SetTextAsync("eski");
        view.Focus();
        Dispatcher.UIThread.RunJobs();
        var before = _sessions[0].LocalClipboardSets;

        _sessions[0].RaiseClipboardText(null);
        Dispatcher.UIThread.RunJobs();
        window.Named<TextBox>("SearchBox").Focus();
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, _sessions[0].LocalClipboardSets);
    }

    // Finder'da kopyalanan dosya, oturuma dönünce sunucuya dosya olarak duyurulur.
    [AvaloniaFact]
    public async Task FocusSendsLocalFiles()
    {
        var (window, view) = OpenSession();
        var path = Path.Combine(Path.GetTempPath(), "rdpnext-pano-" + Guid.NewGuid() + ".txt");
        await File.WriteAllTextAsync(path, "x", TestContext.Current.CancellationToken);
        var file = await window.StorageProvider.TryGetFileFromPathAsync(new Uri(path));
        Assert.SkipWhen(file is null, "Başsız depolama dosya veremiyor");
        var data = new Avalonia.Input.DataTransfer();
        data.Add(Avalonia.Input.DataTransferItem.CreateFile(file!));
        await window.Clipboard!.SetDataAsync(data);
        window.Named<TextBox>("SearchBox").Focus();

        view.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([path], _sessions[0].LocalFiles);
    }
}
