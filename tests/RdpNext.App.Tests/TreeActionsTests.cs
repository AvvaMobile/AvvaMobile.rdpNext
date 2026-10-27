using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using RdpNext.App.Services;
using RdpNext.App.ViewModels;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class TreeActionsTests
{
    private sealed class FakeConfirm(bool answer, string? text = null) : IConfirmService
    {
        public string? LastMessage { get; private set; }

        public Task<string?> AskTextAsync(string message, string confirmText) => Task.FromResult(text);

        public Task<bool> ConfirmAsync(string message, string confirmText)
        {
            LastMessage = message;
            return Task.FromResult(answer);
        }
    }

    private readonly string _path = TestWindows.SampleFile();

    private (MainWindowViewModel Vm, FakeConfirm Confirm) Create(bool answer = true, string? text = null)
    {
        var confirm = new FakeConfirm(answer, text);
        return (new MainWindowViewModel(new ConnectionStore(_path), new InMemorySecretStore(), confirm), confirm);
    }

    private static GroupNodeViewModel Folder(MainWindowViewModel vm, string name) => vm.Tree.Node<GroupNodeViewModel>(
        vm.Tree.Root.Children.OfType<Group>().First(g => g.Name == name).Id);

    private static ServerNodeViewModel Server(MainWindowViewModel vm, string name) => vm.Tree.Node<ServerNodeViewModel>(
        vm.Tree.Root.AllServers().First(s => s.Name == name).Id);

    private Group Reload() => new ConnectionStore(_path).Load().Root;

    [Fact]
    public async Task DeleteServerAfterConfirm()
    {
        var (vm, confirm) = Create();
        var node = Server(vm, "web-01");
        vm.Secrets.SetPassword(node.Server.Id, "pw");

        await vm.DeleteNodeCommand.ExecuteAsync(node);

        Assert.Equal("web-01 silinsin mi?", confirm.LastMessage);
        Assert.DoesNotContain(Reload().AllServers(), s => s.Name == "web-01");
        Assert.DoesNotContain(Folder(vm, "Üretim").Children, n => n.Name == "web-01");
        Assert.Null(vm.Secrets.GetPassword(node.Server.Id));
    }

    [Fact]
    public async Task DeleteCancelledKeepsServer()
    {
        var (vm, _) = Create(answer: false);

        await vm.DeleteNodeCommand.ExecuteAsync(Server(vm, "web-01"));

        Assert.Contains(Reload().AllServers(), s => s.Name == "web-01");
    }

    [Fact]
    public async Task DeleteServerClosesItsTabs()
    {
        var (vm, _) = Create();
        var node = Server(vm, "web-01");
        vm.Sessions.Open(node);
        vm.EditServer(node);
        vm.Sessions.Open(Server(vm, "sql-01"));

        await vm.DeleteNodeCommand.ExecuteAsync(node);

        Assert.Equal(["sql-01"], vm.Sessions.Tabs.Select(t => t.Title));
    }

    [Fact]
    public async Task DeleteFolderRemovesChildrenAndSecrets()
    {
        var (vm, confirm) = Create();
        var folder = Folder(vm, "Üretim");
        var web = Server(vm, "web-01");
        vm.Secrets.SetPassword(web.Server.Id, "pw");
        vm.Secrets.SetPassword(folder.Group.Id, "g");
        vm.Sessions.Open(web);

        await vm.DeleteNodeCommand.ExecuteAsync(folder);

        Assert.Equal("Üretim ve içindeki 3 sunucu silinsin mi?", confirm.LastMessage);
        Assert.DoesNotContain(Reload().Children, n => n.Name == "Üretim");
        Assert.Null(vm.Secrets.GetPassword(web.Server.Id));
        Assert.Null(vm.Secrets.GetPassword(folder.Group.Id));
        Assert.Empty(vm.Sessions.Tabs);
    }

    [Fact]
    public void CopyServerAddsCopyAndOpensEditor()
    {
        var (vm, _) = Create();
        var web = Server(vm, "web-01");
        vm.Secrets.SetPassword(web.Server.Id, "pw");

        vm.CopyServerCommand.Execute(web);

        var copy = Reload().AllServers().Single(s => s.Name == "web-01 kopya");
        Assert.Equal(("10.0.1.11", "Üretim"), (copy.Host, copy.Parent!.Name));
        Assert.Equal("pw", vm.Secrets.GetPassword(copy.Id));
        var editor = Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);
        Assert.Equal("web-01 kopya ayarları", editor.Title);
    }

    [Fact]
    public void AddFromFolderMenuUsesThatFolder()
    {
        var (vm, _) = Create();
        vm.Tree.Selected = null;

        vm.NewServerInCommand.Execute(Folder(vm, "Test"));
        var server = Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);

        Assert.Equal("Test", server.Folder.Group?.Name);
    }

    [Fact]
    public async Task NewFolderAsksOnlyNameAndAddsAtRoot()
    {
        var (vm, _) = Create(text: "  Muhasebe ");
        vm.Tree.Selected = Folder(vm, "Üretim");
        var tabsBefore = vm.Sessions.Tabs.Count;

        await vm.NewFolderCommand.ExecuteAsync(null);

        var root = Reload();
        Assert.Contains(root.Children, n => n is Group { Name: "Muhasebe" });
        Assert.DoesNotContain(root.Children.OfType<Group>().Single(g => g.Name == "Üretim").Children, n => n is Group);
        Assert.Equal(tabsBefore, vm.Sessions.Tabs.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task NewFolderCancelledOrBlankAddsNothing(string? answer)
    {
        var (vm, _) = Create(text: answer);
        vm.Tree.Selected = null;
        var before = vm.Tree.Roots.Count;

        await vm.NewFolderCommand.ExecuteAsync(null);

        Assert.Equal(before, vm.Tree.Roots.Count);
    }

    [Fact]
    public void EditorDeleteButtonHiddenForNew()
    {
        var (vm, _) = Create();
        vm.NewServer();

        Assert.True(Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected).IsNew);
    }

    [AvaloniaFact]
    public void ServerRowMenuHasApprovedItemsAndEditWorks()
    {
        var (window, vm) = TestWindows.Open(_path);
        var row = window.Text("web-01").GetVisualAncestors().OfType<Control>().First(c => c.ContextMenu is not null);
        var items = row.ContextMenu!.Items.OfType<MenuItem>().ToList();

        Assert.Equal(["Bağlan", "Düzenle", "Kopyala", "Sil"], items.Select(i => i.Header as string));
        var edit = items[1];
        row.ContextMenu.Open(row);
        edit.Command!.Execute(edit.CommandParameter);
        row.ContextMenu.Close();

        Assert.Equal("web-01 ayarları", vm.Sessions.Selected?.Title);
    }

    [AvaloniaFact]
    public void FolderRowMenuHasApprovedItems()
    {
        var (window, _) = TestWindows.Open(_path);
        var row = window.Text("Üretim").GetVisualAncestors().OfType<Control>().First(c => c.ContextMenu is not null);

        Assert.Equal(["Sunucu ekle", "Düzenle", "Sil"],
            row.ContextMenu!.Items.OfType<MenuItem>().Select(i => i.Header as string));
    }

    [AvaloniaFact]
    public void ClickingTreeRowSelectsIt()
    {
        var (window, vm) = TestWindows.Open(_path);
        var row = window.Text("sql-01");
        var point = row.TranslatePoint(new Avalonia.Point(5, 5), window)!.Value;

        window.MouseDown(point, Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, Avalonia.Input.MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("sql-01", vm.Tree.Selected?.Name);
        Assert.Empty(vm.Sessions.Tabs);
    }
}
