using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class DragDropTests
{
    private readonly string _path = TestWindows.SampleFile();
    private readonly MainWindowViewModel _vm;

    public DragDropTests() => _vm = TestWindows.ViewModel(_path);

    private TreeNodeViewModel Node(string name) => Flatten(_vm.Tree.Roots).First(n => n.Name == name);

    private static IEnumerable<TreeNodeViewModel> Flatten(IEnumerable<TreeNodeViewModel> nodes) =>
        nodes.SelectMany(n => n is GroupNodeViewModel g ? Flatten(g.Children).Prepend(n) : [n]);

    private IEnumerable<string> Names(string folder) => ((GroupNodeViewModel)Node(folder)).Children.Select(n => n.Name);

    private void Drop(string dragged, string target, DropPlace place) =>
        Assert.True(_vm.Drop(Node(dragged), Node(target), place));

    [Fact]
    public void ServerReordersInsideFolder()
    {
        Drop("api-01", "web-01", DropPlace.Above);
        Assert.Equal(["api-01", "web-01", "sql-01"], Names("Üretim"));

        Drop("api-01", "sql-01", DropPlace.Below);
        Assert.Equal(["web-01", "sql-01", "api-01"], Names("Üretim"));
    }

    [Fact]
    public void ServerDroppedOnFolderMovesToItsTop()
    {
        Drop("web-01", "Test", DropPlace.Into);

        Assert.Equal("web-01", Names("Test").First());
        Assert.DoesNotContain("web-01", Names("Üretim"));
    }

    [Fact]
    public void ServerMovesNextToServerInOtherFolder()
    {
        Drop("web-01", "test-02", DropPlace.Above);

        Assert.Equal(["web-test", "web-01", "test-02"], Names("Test").Take(3));
    }

    [Fact]
    public void ServerBelowExpandedFolderGoesToItsTop()
    {
        Drop("web-test", "Üretim", DropPlace.Below);

        Assert.Equal("web-test", Names("Üretim").First());
    }

    [Fact]
    public void ServerAboveRootFolderGoesToRoot()
    {
        Drop("web-01", "Test", DropPlace.Above);

        Assert.Equal(["Üretim", "web-01", "Test", "Müşteriler"], _vm.Tree.Roots.Select(n => n.Name));
    }

    [Fact]
    public void FolderReordersAtRoot()
    {
        Drop("Müşteriler", "Üretim", DropPlace.Above);
        Assert.Equal(["Müşteriler", "Üretim", "Test"], _vm.Tree.Roots.Select(n => n.Name));

        Drop("Müşteriler", "Test", DropPlace.Below);
        Assert.Equal(["Üretim", "Test", "Müşteriler"], _vm.Tree.Roots.Select(n => n.Name));
    }

    [Fact]
    public void FolderNeverGoesInsideAnotherFolder()
    {
        Assert.False(_vm.Drop(Node("Test"), Node("Üretim"), DropPlace.Into));
        Assert.False(_vm.Drop(Node("Test"), Node("web-01"), DropPlace.Above));
        Assert.Equal(["Üretim", "Test", "Müşteriler"], _vm.Tree.Roots.Select(n => n.Name));
        Assert.DoesNotContain("Test", Names("Üretim"));
    }

    [Fact]
    public void DropOnItselfDoesNothing()
    {
        Assert.False(_vm.Drop(Node("web-01"), Node("web-01"), DropPlace.Above));
        Assert.Equal(["web-01", "sql-01", "api-01"], Names("Üretim"));
    }

    [Fact]
    public void DropIsSaved()
    {
        Drop("api-01", "Test", DropPlace.Into);
        Drop("Müşteriler", "Üretim", DropPlace.Above);

        var root = new ConnectionStore(_path).Load().Root;
        Assert.Equal(["Müşteriler", "Üretim", "Test"], root.Children.Select(n => n.Name));
        Assert.Equal("api-01", ((Group)root.Children[2]).Children[0].Name);
    }

    // Gerçek fareyle: api-01'i tutup web-01'in üst kenarına sürükle; çizgi görünür, bırakınca yer değişir.
    [AvaloniaFact]
    public void MouseDragShowsLineAndReorders()
    {
        var (window, vm) = TestWindows.Open(_path);
        Point At(string name, double y) =>
            window.Text(name).TranslatePoint(new Point(5, 0), window)!.Value + new Point(0, y);
        var line = window.Named<Border>("DropLine");

        window.MouseDown(At("api-01", 5), MouseButton.Left);
        window.MouseMove(At("api-01", -10), RawInputModifiers.LeftMouseButton);
        window.MouseMove(At("web-01", -6), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        Assert.True(line.IsVisible);

        window.MouseUp(At("web-01", -6), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(line.IsVisible);
        var uretim = (GroupNodeViewModel)vm.Tree.Roots[0];
        Assert.Equal(["api-01", "web-01", "sql-01"], uretim.Children.Select(n => n.Name));
    }

    [AvaloniaFact]
    public void MouseDragOnFolderHighlightsIt()
    {
        var (window, vm) = TestWindows.Open(_path);
        Point At(string name, double y) =>
            window.Text(name).TranslatePoint(new Point(5, 0), window)!.Value + new Point(0, y);
        var mid = window.Text("Test").Bounds.Height / 2;

        window.MouseDown(At("web-01", 5), MouseButton.Left);
        window.MouseMove(At("web-01", 20), RawInputModifiers.LeftMouseButton);
        window.MouseMove(At("Test", mid), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.Named<Border>("DropHighlight").IsVisible);

        window.MouseUp(At("Test", mid), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("web-01", ((GroupNodeViewModel)vm.Tree.Roots[1]).Children.First().Name);
    }
}
