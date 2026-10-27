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
    public void PlusTabFocusesSearch()
    {
        var (window, _) = TestWindows.Open();
        var plus = window.Named<Button>("NewTabButton");
        var point = plus.TranslatePoint(new Point(plus.Bounds.Width / 2, plus.Bounds.Height / 2), window)!.Value;
        Assert.False(window.Named<TextBox>("SearchBox").IsFocused);

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.Named<TextBox>("SearchBox").IsFocused);
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
    public void ManyTabsKeepPlusButtonInsideWindow()
    {
        var root = new Group();
        root.Add(new Server { Name = new string('u', 120) });
        for (var i = 0; i < 12; i++)
            root.Add(new Server { Name = $"sunucu-{i:00}" });
        var (window, vm) = TestWindows.Open(TestWindows.FileWith(root));
        foreach (var server in vm.Tree.Roots.OfType<ServerNodeViewModel>())
            vm.ConnectCommand.Execute(server);
        Dispatcher.UIThread.RunJobs();

        var plus = window.Named<Button>("NewTabButton");
        var right = plus.TranslatePoint(new Point(plus.Bounds.Width, 0), window)!.Value.X;
        Assert.True(right <= window.Bounds.Width, $"{right} > {window.Bounds.Width}");
        Assert.True(window.Named<Control>("TabStrip").Texts().First().Bounds.Width <= 200);
    }
}
