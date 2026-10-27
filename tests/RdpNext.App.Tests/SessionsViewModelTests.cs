using RdpNext.App.ViewModels;

namespace RdpNext.App.Tests;

public class SessionsViewModelTests
{
    private readonly SessionsViewModel _sessions = new();
    private readonly ServerNodeViewModel _a = Server("a");
    private readonly ServerNodeViewModel _b = Server("b");
    private readonly ServerNodeViewModel _c = Server("c");

    private static ServerNodeViewModel Server(string name) => new(new Server { Name = name, Color = ServerColor.Teal });

    [Fact]
    public void OpenAddsTabSelectsItAndMarksConnected()
    {
        var tab = _sessions.Open(_a);

        Assert.Same(tab, Assert.Single(_sessions.Tabs));
        Assert.Same(tab, _sessions.Selected);
        Assert.Equal(("a", ServerColor.Teal), (tab.Title, tab.Color));
        Assert.True(_a.IsConnected);
    }

    [Fact]
    public void OpeningSameServerTwiceSelectsExistingTab()
    {
        var first = _sessions.Open(_a);
        _sessions.Open(_b);

        var again = _sessions.Open(_a);

        Assert.Same(first, again);
        Assert.Equal(2, _sessions.Tabs.Count);
        Assert.Same(first, _sessions.Selected);
    }

    [Fact]
    public void CloseSelectsLeftNeighbourOrFirst()
    {
        var a = _sessions.Open(_a);
        var b = _sessions.Open(_b);
        _sessions.Open(_c);
        _sessions.Selected = b;

        _sessions.Close(b);
        Assert.Same(a, _sessions.Selected);

        _sessions.Close(a);
        Assert.Equal("c", _sessions.Selected?.Title);

        _sessions.Close(_sessions.Selected!);
        Assert.Null(_sessions.Selected);
        Assert.Empty(_sessions.Tabs);
    }

    [Fact]
    public void ClosingUnselectedTabKeepsSelection()
    {
        var a = _sessions.Open(_a);
        var b = _sessions.Open(_b);

        _sessions.Close(a);

        Assert.Same(b, _sessions.Selected);
    }

    [Fact]
    public void CloseMarksServerDisconnected()
    {
        var tab = _sessions.Open(_a);

        _sessions.Close(tab);

        Assert.False(_a.IsConnected);
    }

    [Fact]
    public void CloseOthersKeepsOnlyGivenTab()
    {
        _sessions.Open(_a);
        var b = _sessions.Open(_b);
        _sessions.Open(_c);

        _sessions.CloseOthers(b);

        Assert.Same(b, Assert.Single(_sessions.Tabs));
        Assert.Same(b, _sessions.Selected);
        Assert.False(_a.IsConnected);
        Assert.True(_b.IsConnected);
        Assert.False(_c.IsConnected);
    }

    private sealed class FakeTab(string title) : TabViewModel
    {
        public override string Title => title;
    }

    [Fact]
    public void OpenTabAddsAndSelectsAnyTab()
    {
        _sessions.Open(_a);
        var tab = new FakeTab("ayarlar");

        _sessions.OpenTab(tab);

        Assert.Equal(2, _sessions.Tabs.Count);
        Assert.Same(tab, _sessions.Selected);
        Assert.Same(tab, _sessions.Find<FakeTab>(t => t.Title == "ayarlar"));
    }

    [Fact]
    public void MoveTabReordersAndKeepsSelection()
    {
        var a = _sessions.Open(_a);
        var b = _sessions.Open(_b);
        var c = _sessions.Open(_c);
        _sessions.Selected = b;

        _sessions.MoveTab(c, 0);
        Assert.Equal([c, a, b], _sessions.Tabs);

        _sessions.MoveTab(c, 5);
        Assert.Equal([a, b, c], _sessions.Tabs);
        Assert.Same(b, _sessions.Selected);
    }
}
