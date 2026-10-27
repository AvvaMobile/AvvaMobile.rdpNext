using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Tests;

public class MainWindowTests
{
    private static void TypeInSearch(Window window, string text)
    {
        window.Named<TextBox>("SearchBox").Focus();
        window.KeyTextInput(text);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<string?> TabTitles(Window window) =>
        window.Named<Control>("TabStrip").Texts().Select(t => t.Text);

    [AvaloniaFact]
    public void DoubleClickOnServerOpensTab()
    {
        var (window, vm) = TestWindows.Open();
        var row = window.Text("web-01");
        var point = row.TranslatePoint(new Point(5, 5), window)!.Value;

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("web-01", vm.Sessions.Selected?.Title);
        Assert.Contains("web-01", TabTitles(window));
    }

    [AvaloniaFact]
    public void EnterInSearchOpensFirstMatch()
    {
        var (window, vm) = TestWindows.Open();

        TypeInSearch(window, "sql");

        Assert.Equal("sql-01", vm.Sessions.Selected?.Title);
        Assert.Contains("sql-01", TabTitles(window));
    }

    [AvaloniaFact]
    public void EnterWithNoMatchDoesNothing()
    {
        var (window, vm) = TestWindows.Open();

        TypeInSearch(window, "yok-boyle");

        Assert.Empty(vm.Sessions.Tabs);
    }

    [AvaloniaFact]
    public void TitleBarHasNoPlusButton()
    {
        var (window, _) = TestWindows.Open();

        Assert.DoesNotContain(window.Named<Control>("TitleBar").GetVisualDescendants().OfType<Button>(), b => b.Content as string == "+");
    }

    [AvaloniaFact]
    public void EmptySessionAreaHasNoText()
    {
        var (window, vm) = TestWindows.Open();
        var session = window.Named<Control>("SessionArea");
        Assert.Empty(session.Texts());

        vm.ConnectFirstMatchCommand.Execute(null);
        vm.Tree.Query = "web";
        vm.ConnectFirstMatchCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(session.Texts());
        Assert.NotEmpty(session.GetVisualDescendants().OfType<Views.SessionPlaceholderView>());
    }

    [AvaloniaFact]
    public void ClosingSelectedTabInWindowSelectsLeftNeighbour()
    {
        var (window, vm) = TestWindows.Open();
        foreach (var name in new[] { "web-01", "sql-01", "api-01" })
        {
            vm.Tree.Query = name;
            vm.ConnectFirstMatchCommand.Execute(null);
        }
        Dispatcher.UIThread.RunJobs();

        vm.CloseTabCommand.Execute(vm.Sessions.Selected);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("sql-01", vm.Sessions.Selected?.Title);
    }

    [AvaloniaFact]
    public void ManyTabsStayInsideWindow()
    {
        var root = new Group();
        root.Add(new Server { Name = new string('u', 120) });
        for (var i = 0; i < 12; i++)
            root.Add(new Server { Name = $"sunucu-{i:00}" });
        var (window, vm) = TestWindows.Open(TestWindows.FileWith(root));
        foreach (var server in vm.Tree.Roots.OfType<ServerNodeViewModel>())
            vm.ConnectCommand.Execute(server);
        Dispatcher.UIThread.RunJobs();

        var strip = window.Named<Control>("TabStrip");
        var right = strip.TranslatePoint(new Point(strip.Bounds.Width, 0), window)!.Value.X;
        Assert.True(right <= window.Bounds.Width, $"{right} > {window.Bounds.Width}");
        Assert.True(window.Named<Control>("TabStrip").Texts().First().Bounds.Width <= 200);
    }

    private static void DoubleClick(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void DoubleClickOnEmptyTitleBarTogglesMaximized()
    {
        var (window, _) = TestWindows.Open();
        var empty = new Point(window.Bounds.Width - 200, 26);

        DoubleClick(window, empty);
        Assert.Equal(WindowState.Maximized, window.WindowState);

        DoubleClick(window, new Point(window.Bounds.Width - 200, 26));
        Assert.Equal(WindowState.Normal, window.WindowState);
    }

    [AvaloniaFact]
    public void DoubleClickOnTabDoesNotMaximize()
    {
        var (window, vm) = TestWindows.Open();
        vm.Tree.Query = "web";
        vm.ConnectFirstMatchCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var tab = window.Named<Control>("TabStrip").Texts().First();

        DoubleClick(window, tab.TranslatePoint(new Point(5, 5), window)!.Value);

        Assert.Equal(WindowState.Normal, window.WindowState);
    }

    [AvaloniaFact]
    public void EditorShowsFieldsInApprovedOrder()
    {
        var (window, vm) = TestWindows.Open();
        vm.NewServer();
        Dispatcher.UIThread.RunJobs();
        var editor = window.Named<Control>("SessionArea").GetVisualDescendants().OfType<Views.ServerEditorView>().Single();

        var labels = editor.Texts().Where(t => t.Classes.Contains("label")).Select(t => t.Text).ToList();

        Assert.Equal(["Görünen ad", "Klasör", "Renk", "Host/IP", "Port", "Kullanıcı", "Şifre", "Domain"], labels);
        Assert.Contains(editor.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Bağlan");
        Assert.Contains(editor.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "Kaydet");
        Assert.Contains(window.Named<Control>("TabStrip").Texts(), t => t.Text == "Yeni sunucu");
    }

    [AvaloniaFact]
    public void TypingInEditorAndSavingAddsServerToTree()
    {
        var (window, vm) = TestWindows.Open();
        vm.NewServer();
        Dispatcher.UIThread.RunJobs();
        window.Named<TextBox>("HostBox").Focus();
        window.KeyTextInput("10.0.5.5");
        window.Named<Button>("SaveButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(window.Named<Control>("Tree").Texts(), t => t.Text == "10.0.5.5");
    }

    [AvaloniaFact]
    public void SessionAndEditorOpenInTheSameFrame()
    {
        var (window, vm) = TestWindows.Open();
        var frame = window.Named<Border>("ContentFrame");
        Assert.Equal(new CornerRadius(12), frame.CornerRadius);
        Assert.True(frame.ClipToBounds);

        vm.Tree.Query = "web-01";
        vm.ConnectFirstMatchCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var session = frame.GetVisualDescendants().OfType<Views.SessionPlaceholderView>().Single().Bounds;

        vm.NewServer();
        Dispatcher.UIThread.RunJobs();
        var editor = frame.GetVisualDescendants().OfType<Views.ServerEditorView>().Single().Bounds;

        Assert.Equal(session, editor);
    }
}
