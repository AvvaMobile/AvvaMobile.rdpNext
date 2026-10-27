using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RdpNext.App.Services;
using RdpNext.App.ViewModels;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class SettingsTests
{
    private sealed class FakeConfirm(bool answer) : IConfirmService
    {
        public string? LastMessage { get; private set; }

        public Task<bool> ConfirmAsync(string message, string confirmText)
        {
            LastMessage = message;
            return Task.FromResult(answer);
        }

        public Task<string?> AskTextAsync(string message, string confirmText) => Task.FromResult<string?>(null);
    }

    private sealed class FakeFiles(string? path) : IFileDialogService
    {
        public string? SuggestedName { get; private set; }

        public Task<string?> SaveAsync(string suggestedName)
        {
            SuggestedName = suggestedName;
            return Task.FromResult(path);
        }

        public Task<string?> OpenAsync() => Task.FromResult(path);
    }

    private readonly string _path = TestWindows.SampleFile();
    private readonly string _backup = Path.Combine(Path.GetTempPath(), "rdpnext-backup-" + Guid.NewGuid() + ".json");

    private MainWindowViewModel Create(string? picked, bool confirm = true) =>
        new(new ConnectionStore(_path), new InMemorySecretStore(), new FakeConfirm(confirm), new FakeFiles(picked));

    private static SettingsViewModel OpenSettings(MainWindowViewModel vm)
    {
        vm.OpenSettingsCommand.Execute(null);
        return Assert.IsType<SettingsViewModel>(vm.Sessions.Selected);
    }

    [Fact]
    public async Task ExportWritesWholeList()
    {
        var vm = Create(_backup);

        await OpenSettings(vm).ExportCommand.ExecuteAsync(null);

        var root = ConnectionStore.Read(_backup)!;
        Assert.Equal(["Üretim", "Test", "Müşteriler"], root.Children.Select(n => n.Name));
        Assert.Equal(vm.Tree.Root.AllServers().Count(), root.AllServers().Count());
    }

    [Fact]
    public async Task ExportCancelledWritesNothing()
    {
        var vm = Create(null);

        await OpenSettings(vm).ExportCommand.ExecuteAsync(null);

        Assert.False(File.Exists(_backup));
    }

    [Fact]
    public async Task ImportReplacesListKeepsSettingsTabAndSaves()
    {
        var backup = new Group();
        var folder = new Group { Name = "Yedek klasör" };
        folder.Add(new Server { Name = "yedek-01", Host = "10.9.9.9" });
        backup.Add(folder);
        new ConnectionStore(_backup).Save(backup);
        var vm = Create(_backup);
        vm.ConnectCommand.Execute(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First().Id));
        var settings = OpenSettings(vm);

        await settings.ImportCommand.ExecuteAsync(null);

        Assert.Equal(["Yedek klasör"], vm.Tree.Roots.Select(n => n.Name));
        Assert.Equal("yedek-01", ((GroupNodeViewModel)vm.Tree.Roots[0]).Children[0].Name);
        Assert.Equal([settings], vm.Sessions.Tabs);
        Assert.Equal(["Yedek klasör"], new ConnectionStore(_path).Load().Root.Children.Select(n => n.Name));
    }

    [Fact]
    public async Task ImportNeedsConfirm()
    {
        new ConnectionStore(_backup).Save(new Group());
        var vm = Create(_backup, confirm: false);

        await OpenSettings(vm).ImportCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Tree.Roots.Count);
    }

    [Fact]
    public async Task ImportBadFileKeepsListAndFile()
    {
        File.WriteAllText(_backup, "{ yarım");
        var vm = Create(_backup);
        var settings = OpenSettings(vm);

        await settings.ImportCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Tree.Roots.Count);
        Assert.Equal("{ yarım", File.ReadAllText(_backup));
        Assert.NotNull(settings.Status);
    }

    [AvaloniaFact]
    public void SidebarButtonOpensSettingsWithBackupButtons()
    {
        var (window, vm) = TestWindows.Open();

        window.Named<Button>("SettingsButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ayarlar", vm.Sessions.Selected?.Title);
        var view = window.GetVisualDescendants().OfType<Views.SettingsView>().Single();
        Assert.NotNull(view.Named<Button>("ExportButton").Command);
        Assert.NotNull(view.Named<Button>("ImportButton").Command);
    }
}
