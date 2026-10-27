using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;
using RdpNext.App.Views;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

// Testler için geçici klasörde bağlantı dosyası hazırlayıp pencereyi açar.
public static class TestWindows
{
    public static string SampleFile(string? content = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "rdpnext-app-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "connections.json");
        if (content is null)
            File.Copy(Path.Combine(AppContext.BaseDirectory, "connections.sample.json"), path);
        else
            File.WriteAllText(path, content);
        return path;
    }

    public static string FileWith(Group root)
    {
        var path = SampleFile("");
        new ConnectionStore(path).Save(root);
        return path;
    }

    public static MainWindowViewModel ViewModel(string? path = null) =>
        new(new ConnectionStore(path ?? SampleFile()), new InMemorySecretStore());

    public static (MainWindow Window, MainWindowViewModel Vm) Open(string? path = null)
    {
        var vm = ViewModel(path);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    // Klasörler açılışta kapalı gelir; sunucu satırlarıyla çalışan testler hepsini açar.
    public static (MainWindow Window, MainWindowViewModel Vm) OpenExpanded(string? path = null)
    {
        var (window, vm) = Open(path);
        Expand(vm.Tree.Roots);
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    private static void Expand(IEnumerable<TreeNodeViewModel> nodes)
    {
        foreach (var group in nodes.OfType<GroupNodeViewModel>())
        {
            group.IsExpanded = true;
            Expand(group.Children);
        }
    }

    public static IEnumerable<TextBlock> Texts(this Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible);

    public static TextBlock Text(this Visual root, string text) =>
        root.Texts().First(t => t.Text == text);

    public static T Named<T>(this Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    public static TreeViewItem Item(this Visual root, string name) =>
        root.GetVisualDescendants().OfType<TreeViewItem>()
            .First(i => i.DataContext is TreeNodeViewModel n && n.Name == name);
}
