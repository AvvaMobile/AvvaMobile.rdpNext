using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using RdpNext.App.Strings;

namespace RdpNext.App.Tests;

public class SidebarTests
{
    [AvaloniaFact]
    public void GroupRowsShowNoServerCount()
    {
        var (window, _) = TestWindows.Open();

        Assert.DoesNotContain(window.Item("Müşteriler").Texts(), t => t.Text == "23");
        Assert.DoesNotContain(window.Item("Test").Texts(), t => t.Text == "7");
    }

    [AvaloniaFact]
    public void ShowsServerNameAndHost()
    {
        var (window, _) = TestWindows.Open();

        Assert.Contains(window.Texts(), t => t.Text == "web-01");
        Assert.Contains(window.Texts(), t => t.Text == "10.0.1.11");
    }

    [AvaloniaFact]
    public void FontSizesMeetMinimums()
    {
        var (window, _) = TestWindows.Open();

        Assert.True(window.Text("web-01").FontSize >= 15);
        Assert.True(window.Text("10.0.1.11").FontSize >= 14);
        Assert.True(window.Text("Üretim").FontSize >= 15);
    }

    [AvaloniaFact]
    public void LongNamesAreTrimmed()
    {
        var root = new Group();
        var group = new Group { Name = "Uzun" };
        root.Add(group);
        var longName = new string('a', 120);
        var longHost = new string('b', 120) + ".avva.local";
        group.Add(new Server { Name = longName, Host = longHost });

        var (window, _) = TestWindows.Open(TestWindows.FileWith(root));

        Assert.Equal(TextTrimming.CharacterEllipsis, window.Text(longName).TextTrimming);
        Assert.Equal(TextTrimming.CharacterEllipsis, window.Text(longHost).TextTrimming);
        Assert.True(window.Text(longName).Bounds.Width < 310, $"{window.Text(longName).Bounds.Width}");
        Assert.True(window.Text(longHost).Bounds.Width < 310, $"{window.Text(longHost).Bounds.Width}");
    }

    [AvaloniaFact]
    public void AddServerAndFolderButtonsSideBySide()
    {
        var (window, _) = TestWindows.Open();
        var server = window.Named<Button>("AddServerButton");
        var folder = window.Named<Button>("AddFolderButton");

        Assert.Equal(("Sunucu ekle", "Klasör ekle"), (Strings.Strings.AddServer, Strings.Strings.AddFolder));
        Assert.Contains(server.Texts(), t => t.Text == "Sunucu ekle");
        Assert.Contains(folder.Texts(), t => t.Text == "Klasör ekle");
        Assert.NotEmpty(server.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        Assert.NotEmpty(folder.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        Assert.Equal(server.Bounds.Y, folder.Bounds.Y);
        Assert.True(folder.Bounds.X > server.Bounds.Right);
        Assert.True(server.Bounds.Height <= 40, $"{server.Bounds.Height}");
    }

    [AvaloniaFact]
    public void CorruptFileShowsNotice()
    {
        var (broken, brokenVm) = TestWindows.Open(TestWindows.SampleFile("{ yarım"));
        Assert.NotNull(brokenVm.Notice);
        Assert.Contains(broken.Texts(), t => t.Text == brokenVm.Notice && t.Text!.StartsWith("Bağlantı dosyası okunamadı"));

        var (normal, normalVm) = TestWindows.Open();
        Assert.Null(normalVm.Notice);
        Assert.DoesNotContain(normal.Texts(), t => t.Text?.StartsWith("Bağlantı dosyası okunamadı") == true);
    }

    [AvaloniaFact]
    public void TypingInSearchBoxFilters()
    {
        var (window, _) = TestWindows.Open();
        window.Named<TextBox>("SearchBox").Focus();

        window.KeyTextInput("musteri");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(window.Item("Müşteriler").IsVisible);
        Assert.False(window.Item("Üretim").IsVisible);
        Assert.False(window.Item("Test").IsVisible);
    }

    [AvaloniaFact]
    public void ExpandStateComesFromModel()
    {
        var (window, _) = TestWindows.Open();

        Assert.True(window.Item("Üretim").IsExpanded);
        Assert.False(window.Item("Test").IsExpanded);
    }
}
