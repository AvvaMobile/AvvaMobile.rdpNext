namespace RdpNext.Core.Tests;

public class GroupTests
{
    [Fact]
    public void AddRejectsMovingGroupIntoItsOwnChild()
    {
        var root = new Group();
        var parent = new Group { Name = "Üretim" };
        var child = new Group { Name = "Web" };
        root.Add(parent);
        parent.Add(child);

        Assert.Throws<InvalidOperationException>(() => child.Add(parent));
        Assert.Throws<InvalidOperationException>(() => parent.Add(parent));
        Assert.Same(root, parent.Parent);
    }
}
