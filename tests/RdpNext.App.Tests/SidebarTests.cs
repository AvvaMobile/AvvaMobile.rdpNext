using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using RdpNext.App.Strings;
using RdpNext.App.ViewModels;

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
        var (window, _) = TestWindows.OpenExpanded();

        Assert.Contains(window.Texts(), t => t.Text == "web-01");
        Assert.Contains(window.Texts(), t => t.Text == "10.0.1.11");
    }

    [AvaloniaFact]
    public void FontSizesMeetMinimums()
    {
        var (window, _) = TestWindows.OpenExpanded();

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

        var (window, _) = TestWindows.OpenExpanded(TestWindows.FileWith(root));

        Assert.Equal(TextTrimming.CharacterEllipsis, window.Text(longName).TextTrimming);
        Assert.Equal(TextTrimming.CharacterEllipsis, window.Text(longHost).TextTrimming);
        Assert.True(window.Text(longName).Bounds.Width < 310, $"{window.Text(longName).Bounds.Width}");
        Assert.True(window.Text(longHost).Bounds.Width < 310, $"{window.Text(longHost).Bounds.Width}");
    }

    [AvaloniaFact]
    public void AddButtonsAreIconsRightOfSearch()
    {
        var (window, _) = TestWindows.Open();
        var search = window.Named<TextBox>("SearchBox");
        var server = window.Named<Button>("AddServerButton");
        var folder = window.Named<Button>("AddFolderButton");

        Assert.Equal(("Add server", "Add folder"), (ToolTip.GetTip(server), ToolTip.GetTip(folder)));
        Assert.Empty(server.Texts());
        Assert.NotEmpty(server.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        Assert.NotEmpty(folder.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        Point Top(Control c) => c.TranslatePoint(new Point(0, 0), window)!.Value;
        Assert.Equal(Top(search).Y, Top(server).Y, 0.5);
        Assert.Equal(Top(search).Y, Top(folder).Y, 0.5);
        Assert.True(Top(server).X > Top(search).X + search.Bounds.Width);
        Assert.True(Top(folder).X > Top(server).X + server.Bounds.Width);
    }

    [AvaloniaFact]
    public void CorruptFileShowsNotice()
    {
        var (broken, brokenVm) = TestWindows.Open(TestWindows.SampleFile("{ yarım"));
        Assert.NotNull(brokenVm.Notice);
        Assert.Contains(broken.Texts(), t => t.Text == brokenVm.Notice && t.Text!.StartsWith("The connections file could not be read"));

        var (normal, normalVm) = TestWindows.Open();
        Assert.Null(normalVm.Notice);
        Assert.DoesNotContain(normal.Texts(), t => t.Text?.StartsWith("The connections file could not be read") == true);
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

    // Klasörler uygulama kapanırken nasılsa öyle açılır (örnek dosyada Üretim açık, Test ve Müşteriler kapalı).
    [AvaloniaFact]
    public void FoldersStartAsSaved()
    {
        var (window, _) = TestWindows.Open();

        Assert.True(window.Item("Üretim").IsExpanded);
        Assert.False(window.Item("Test").IsExpanded);
        Assert.False(window.Item("Müşteriler").IsExpanded);
    }

    [AvaloniaFact]
    public void ToggledFolderStateIsSaved()
    {
        var path = TestWindows.SampleFile();
        var (_, vm) = TestWindows.Open(path);
        var test = vm.Tree.Roots.OfType<GroupNodeViewModel>().First(g => g.Name == "Test");

        test.IsExpanded = true;

        Assert.True(new Core.Storage.ConnectionStore(path).Load().Root.Children.OfType<Group>().First(g => g.Name == "Test").IsExpanded);
    }

    // Aramanın açtığı klasörler kaydedilmez.
    [AvaloniaFact]
    public void SearchExpansionIsNotSaved()
    {
        var path = TestWindows.SampleFile();
        var (_, vm) = TestWindows.Open(path);

        vm.Tree.Query = "a";

        Assert.False(new Core.Storage.ConnectionStore(path).Load().Root.Children.OfType<Group>().First(g => g.Name == "Test").IsExpanded);
    }

    // Klasör satırına tek tık: açar, bir daha tık: kapatır.
    [AvaloniaFact]
    public void SingleClickTogglesFolder()
    {
        var (window, _) = TestWindows.Open();
        var item = window.Item("Test");
        var text = window.Text("Test");
        var point = text.TranslatePoint(new Point(5, text.Bounds.Height / 2), window)!.Value;

        window.MouseDown(point, Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, Avalonia.Input.MouseButton.Left);
        Assert.True(item.IsExpanded);

        window.MouseDown(point, Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, Avalonia.Input.MouseButton.Left);
        Assert.False(item.IsExpanded);
    }
}
