using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [RelayCommand]
    private void Connect(ServerNodeViewModel server) => Sessions.Open(server);

    [RelayCommand]
    private void ConnectFirstMatch()
    {
        if (Tree.FirstMatch is { } server)
            Sessions.Open(server);
    }

    [RelayCommand]
    private void CloseTab(SessionTabViewModel tab) => Sessions.Close(tab);

    [RelayCommand]
    private void CloseOtherTabs(SessionTabViewModel tab) => Sessions.CloseOthers(tab);
}
