using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Model;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(ConnectionStore store, ISecretStore secrets)
    {
        Store = store;
        Secrets = secrets;
        var loaded = store.Load();
        Tree = new TreeViewModel(loaded.Root);
        Notice = loaded.CorruptBackupPath is { } backup
            ? string.Format(Strings.Strings.CorruptFileNotice, backup)
            : null;
    }

    public ConnectionStore Store { get; }

    public ISecretStore Secrets { get; }

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
}
