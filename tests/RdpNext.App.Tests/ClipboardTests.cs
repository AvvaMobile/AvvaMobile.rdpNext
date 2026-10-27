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
}
