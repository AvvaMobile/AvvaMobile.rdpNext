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

    [Fact]
    public void InsertPlacesBeforeGivenSibling()
    {
        var root = new Group();
        var a = new Server { Name = "a" };
        var b = new Server { Name = "b" };
        var c = new Server { Name = "c" };
        var folder = new Group { Name = "Test" };
        root.Add(a);
        root.Add(b);
        root.Add(c);
        root.Add(folder);

        root.Insert(c, a);
        Assert.Equal(["c", "a", "b", "Test"], root.Children.Select(n => n.Name));

        root.Insert(c, null);
        Assert.Equal(["a", "b", "Test", "c"], root.Children.Select(n => n.Name));

        folder.Insert(a, null);
        Assert.Same(folder, a.Parent);
        Assert.Equal(["b", "Test", "c"], root.Children.Select(n => n.Name));

        // Kendi önüne bırakmak yerinde bırakır.
        root.Insert(b, b);
        Assert.Equal(["b", "Test", "c"], root.Children.Select(n => n.Name));
    }
}
