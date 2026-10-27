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
    private sealed class FakeConfirm(bool answer) : IConfirmService
    {
        public string? LastMessage { get; private set; }

        public Task<bool> ConfirmAsync(string message, string confirmText)
        {
            LastMessage = message;
            return Task.FromResult(answer);
        }
    }

    private readonly string _path = TestWindows.SampleFile();

    private (MainWindowViewModel Vm, FakeConfirm Confirm) Create(bool answer = true)
    {
        var confirm = new FakeConfirm(answer);
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

        Assert.Equal("Delete web-01?", confirm.LastMessage);
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

        Assert.Equal("Delete Üretim and the 3 servers in it?", confirm.LastMessage);
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

        var copy = Reload().AllServers().Single(s => s.Name == "web-01 copy");
        Assert.Equal(("10.0.1.11", "Üretim"), (copy.Host, copy.Parent!.Name));
        Assert.Equal("pw", vm.Secrets.GetPassword(copy.Id));
        var editor = Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);
        Assert.Equal("web-01 copy settings", editor.Title);
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
    public void NewFolderOpensEditorTabAndAddsAtRoot()
    {
        var (vm, _) = Create();
        vm.Tree.Selected = Folder(vm, "Üretim");

        vm.NewFolderCommand.Execute(null);
        var editor = Assert.IsType<FolderEditorViewModel>(vm.Sessions.Selected);
        Assert.True(editor.IsNew);
        Assert.False(editor.SaveCommand.CanExecute(null));

        editor.Name = "  Muhasebe ";
        editor.SaveCommand.Execute(null);

        var root = Reload();
        Assert.Contains(root.Children, n => n is Group { Name: "Muhasebe" });
        Assert.DoesNotContain(root.Children.OfType<Group>().Single(g => g.Name == "Üretim").Children, n => n is Group);
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
        var (window, vm) = TestWindows.OpenExpanded(_path);
        var row = window.Text("web-01").GetVisualAncestors().OfType<Control>().First(c => c.ContextMenu is not null);
        var items = row.ContextMenu!.Items.OfType<MenuItem>().ToList();

        Assert.Equal(["Connect", "Edit", "Duplicate", "Delete"], items.Select(i => i.Header as string));
        var edit = items[1];
        row.ContextMenu.Open(row);
        edit.Command!.Execute(edit.CommandParameter);
        row.ContextMenu.Close();

        Assert.Equal("web-01 settings", vm.Sessions.Selected?.Title);
    }

    [AvaloniaFact]
    public void FolderRowMenuHasApprovedItems()
    {
        var (window, _) = TestWindows.Open(_path);
        var row = window.Text("Üretim").GetVisualAncestors().OfType<Control>().First(c => c.ContextMenu is not null);

        Assert.Equal(["Add server", "Edit", "Delete"],
            row.ContextMenu!.Items.OfType<MenuItem>().Select(i => i.Header as string));
    }

    [AvaloniaFact]
    public void ClickingTreeRowSelectsIt()
    {
        var (window, vm) = TestWindows.OpenExpanded(_path);
        var row = window.Text("sql-01");
        var point = row.TranslatePoint(new Avalonia.Point(5, 5), window)!.Value;

        window.MouseDown(point, Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, Avalonia.Input.MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("sql-01", vm.Tree.Selected?.Name);
        Assert.Empty(vm.Sessions.Tabs);
    }
}
