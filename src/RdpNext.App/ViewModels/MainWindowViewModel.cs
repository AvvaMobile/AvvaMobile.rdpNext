using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.App.Services;
using RdpNext.Core.Model;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(ConnectionStore store, ISecretStore secrets, IConfirmService? confirm = null)
    {
        Store = store;
        Secrets = secrets;
        Confirm = confirm ?? new DialogConfirmService();
        var loaded = store.Load();
        Tree = new TreeViewModel(loaded.Root);
        Notice = loaded.CorruptBackupPath is { } backup
            ? string.Format(Strings.Strings.CorruptFileNotice, backup)
            : null;
    }

    public ConnectionStore Store { get; }

    public ISecretStore Secrets { get; }

    public IConfirmService Confirm { get; }

    public TreeViewModel Tree { get; }

    public string? Notice { get; }

    public SessionsViewModel Sessions { get; } = new();

    public void Persist() => Store.Save(Tree.Root);

    // Ağaçta seçili klasör; sunucu seçiliyse onun klasörü; hiçbiri yoksa en üst (null).
    public Group? SelectedFolder() => Tree.Selected switch
    {
        GroupNodeViewModel group => group.Group,
        ServerNodeViewModel server when server.Server.Parent != Tree.Root => server.Server.Parent,
        _ => null,
    };

    public void EditServer(ServerNodeViewModel node) => Sessions.OpenTab(
        Sessions.Find<ServerEditorViewModel>(e => e.Existing == node.Server)
        ?? new ServerEditorViewModel(this, node.Server, null));

    public void EditFolder(GroupNodeViewModel node) => Sessions.OpenTab(
        Sessions.Find<FolderEditorViewModel>(e => e.Existing == node.Group)
        ?? new FolderEditorViewModel(this, node.Group, null));

    [RelayCommand]
    public void NewFolder() => Sessions.OpenTab(new FolderEditorViewModel(this, null, SelectedFolder()));

    [RelayCommand]
    public void NewServer() => Sessions.OpenTab(new ServerEditorViewModel(this, null, SelectedFolder()));

    [RelayCommand]
    private void Connect(ServerNodeViewModel server) => Sessions.Open(server);

    [RelayCommand]
    private void ConnectFirstMatch()
    {
        if (Tree.FirstMatch is { } server)
            Sessions.Open(server);
    }

    [RelayCommand]
    private void CloseTab(TabViewModel tab) => Sessions.Close(tab);

    [RelayCommand]
    private void CloseOtherTabs(TabViewModel tab) => Sessions.CloseOthers(tab);

    [RelayCommand]
    private void Edit(TreeNodeViewModel node)
    {
        if (node is ServerNodeViewModel server)
            EditServer(server);
        else if (node is GroupNodeViewModel group)
            EditFolder(group);
    }

    [RelayCommand]
    private void NewServerIn(GroupNodeViewModel folder)
    {
        Tree.Selected = folder;
        NewServer();
    }

    [RelayCommand]
    private void NewFolderIn(GroupNodeViewModel folder)
    {
        Tree.Selected = folder;
        NewFolder();
    }

    [RelayCommand]
    private void CopyServer(ServerNodeViewModel node)
    {
        var source = node.Server;
        var copy = source.CopyWithNewId(string.Format(Strings.Strings.CopyName, source.Name));
        var copyNode = Tree.AddServer(copy, source.Parent == Tree.Root ? null : source.Parent);
        if (Secrets.GetPassword(source.Id) is { } password)
            Secrets.SetPassword(copy.Id, password);
        Persist();
        EditServer(copyNode);
    }

    [RelayCommand]
    private async Task DeleteNode(TreeNodeViewModel node)
    {
        var message = node is GroupNodeViewModel group
            ? string.Format(Strings.Strings.DeleteFolderConfirm, group.Name, group.Group.AllServers().Count())
            : string.Format(Strings.Strings.DeleteConfirm, node.Name);
        if (!await Confirm.ConfirmAsync(message, Strings.Strings.Delete))
            return;

        var removed = Subtree(node.Model).ToHashSet();
        foreach (var tab in Sessions.Tabs.Where(t => TabNode(t) is { } n && removed.Contains(n)).ToList())
            Sessions.Close(tab);
        foreach (var model in removed)
            Secrets.DeletePassword(model.Id);
        Tree.Remove(node);
        Persist();
    }

    private static IEnumerable<Node> Subtree(Node node) =>
        node is Group group ? group.Children.SelectMany(Subtree).Prepend(node) : [node];

    private static Node? TabNode(TabViewModel tab) => tab switch
    {
        SessionTabViewModel session => session.Server.Server,
        ServerEditorViewModel editor => editor.Existing,
        FolderEditorViewModel editor => editor.Existing,
        _ => null,
    };
}
