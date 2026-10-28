using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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

    private static Control Overlay(Window window) =>
        window.GetVisualDescendants().OfType<Control>().Single(b => b.Name == "InfoOverlay");

    private static Button CloseButton(Window window) =>
        Overlay(window).GetVisualDescendants().OfType<Button>().Single();

    // × düğmesinin yazısı hariç etiket satırları.
    private static List<TextBlock> Lines(Control overlay) =>
        overlay.GetVisualDescendants().OfType<TextBlock>().Where(t => t.FindAncestorOfType<Button>() is null).ToList();

    private static string Text(Control overlay) => string.Join(" ", Lines(overlay).Select(t => t.Text));

    [AvaloniaFact]
    public void ShowsNameAndHostAtTopCenter()
    {
        var (window, _, _) = Open(overlay: true);

        var overlay = Overlay(window);
        Assert.True(overlay.IsEffectivelyVisible);
        Assert.Equal("web-01 10.0.1.11", Text(overlay));
        Assert.Equal((Avalonia.Layout.HorizontalAlignment.Center, Avalonia.Layout.VerticalAlignment.Top),
            (overlay.HorizontalAlignment, overlay.VerticalAlignment));
    }

    // Yazının üstüne tıklama oturuma geçer; yalnız × düğmesi tıklanır.
    [AvaloniaFact]
    public void OnlyCloseButtonTakesClicks()
    {
        var (window, _, _) = Open(overlay: true);

        var name = Overlay(window).GetVisualDescendants().OfType<TextBlock>().First();
        var onName = window.InputHitTest(name.TranslatePoint(new Avalonia.Point(name.Bounds.Width / 2, name.Bounds.Height / 2), window)!.Value);
        Assert.DoesNotContain(onName as Visual, Overlay(window).GetVisualDescendants());
        var close = CloseButton(window);
        var onClose = window.InputHitTest(close.TranslatePoint(new Avalonia.Point(close.Bounds.Width / 2, close.Bounds.Height / 2), window)!.Value);
        Assert.True(onClose == close || close.IsVisualAncestorOf((Visual)onClose!));
    }

    // × yalnız bu oturumdaki etiketi kapatır; sunucu ayarı kalır, yeniden bağlanınca etiket gelir.
    [AvaloniaFact]
    public void CloseHidesOverlayForThisSessionOnly()
    {
        var (window, vm, server) = Open(overlay: true);

        CloseButton(window).Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Overlay(window).IsEffectivelyVisible);
        Assert.True(server.Server.ShowInfoOverlay);

        vm.CloseTabCommand.Execute(vm.Sessions.Selected!);
        vm.ConnectCommand.Execute(server);
        _sessions[^1].RaiseConnected();
        Dispatcher.UIThread.RunJobs();
        Assert.True(Overlay(window).IsEffectivelyVisible);
    }

    // İki satır: üstte ad, altta adres.
    [AvaloniaFact]
    public void NameAboveHost()
    {
        var (window, _, _) = Open(overlay: true);

        var lines = Lines(Overlay(window));
        Assert.Equal(["web-01", "10.0.1.11"], lines.Select(t => t.Text));
        Assert.True(lines[1].Bounds.Top >= lines[0].Bounds.Bottom);
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
