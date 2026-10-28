using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;
using RdpNext.App.Views;

namespace RdpNext.App.Tests;

// Sunucuda açıksa oturumun üst ortasında sunucu adı ve adresi görünür; tam ekranda da.
public class InfoOverlayTests
{
    private readonly List<FakeRdpSession> _sessions = [];

    private (MainWindow Window, MainWindowViewModel Vm, ServerNodeViewModel Server) Open(bool overlay)
    {
        var vm = TestWindows.ViewModel(sessions: _sessions);
        var uretim = vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == "Üretim");
        uretim.Username = "admin";
        vm.Secrets.SetPassword(uretim.Id, "pw");
        var server = vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "web-01").Id);
        server.Server.ShowInfoOverlay = overlay;
        var window = new MainWindow { DataContext = vm };
        window.Show();
        vm.ConnectCommand.Execute(server);
        _sessions[0].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        return (window, vm, server);
    }

    private static Border Overlay(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "InfoOverlay");

    private static string Text(Border overlay) =>
        string.Join(" ", overlay.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

    [AvaloniaFact]
    public void ShowsNameAndHostAtTopCenter()
    {
        var (window, _, _) = Open(overlay: true);

        var overlay = Overlay(window);
        Assert.True(overlay.IsEffectivelyVisible);
        Assert.Equal("web-01 10.0.1.11", Text(overlay));
        Assert.Equal((Avalonia.Layout.HorizontalAlignment.Center, Avalonia.Layout.VerticalAlignment.Top),
            (overlay.HorizontalAlignment, overlay.VerticalAlignment));
        Assert.False(overlay.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void HiddenByDefault()
    {
        var (window, _, _) = Open(overlay: false);

        Assert.False(Overlay(window).IsEffectivelyVisible);
    }

    // Açık oturumda ayar kaydedilince etiket hemen gelir.
    [AvaloniaFact]
    public void SavingSettingTurnsOverlayOnInOpenSession()
    {
        var (window, vm, server) = Open(overlay: false);
        var tab = vm.Sessions.Selected!;

        vm.EditServer(server);
        var editor = Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);
        editor.ShowInfoOverlay = true;
        editor.SaveCommand.Execute(null);
        vm.Sessions.Selected = tab;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Overlay(window).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void ShownInFullScreenWindow()
    {
        var (window, vm, _) = Open(overlay: true);

        vm.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var own = Assert.Single(window.SessionWindows);
        Assert.True(Overlay(own).IsEffectivelyVisible);
    }
}
