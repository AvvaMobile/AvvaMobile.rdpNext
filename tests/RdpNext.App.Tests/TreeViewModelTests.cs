using RdpNext.App.ViewModels;

namespace RdpNext.App.Tests;

public class TreeViewModelTests
{
    private readonly TreeViewModel _tree = TestWindows.ViewModel().Tree;

    private TreeNodeViewModel Node(string name) => Flatten(_tree.Roots).First(n => n.Name == name);

    private static IEnumerable<TreeNodeViewModel> Flatten(IEnumerable<TreeNodeViewModel> nodes) =>
        nodes.SelectMany(n => n is GroupNodeViewModel g ? Flatten(g.Children).Prepend(n) : [n]);

    [Fact]
    public void QueryHidesNonMatchingServers()
    {
        _tree.Query = "sql";

        Assert.True(Node("sql-01").IsVisible);
        Assert.False(Node("web-01").IsVisible);
        Assert.False(Node("Test").IsVisible);
    }

    [Fact]
    public void QueryExpandsVisibleGroups()
    {
        Assert.False(Node("Test").IsExpanded);

        _tree.Query = "web-test";

        Assert.True(Node("Test").IsExpanded);
    }

    [Fact]
    public void ClearingQueryRestoresModelExpansion()
    {
        _tree.Query = "web-test";
        _tree.Query = "";

        Assert.False(Node("Test").IsExpanded);
        Assert.False(((GroupNodeViewModel)Node("Test")).Group.IsExpanded);
        Assert.All(Flatten(_tree.Roots), n => Assert.True(n.IsVisible));
    }

    [Fact]
    public void FirstMatchFollowsQuery()
    {
        _tree.Query = "web";
        Assert.Equal("web-01", _tree.FirstMatch?.Name);

        _tree.Query = "yok-boyle";
        Assert.Null(_tree.FirstMatch);
    }
}
