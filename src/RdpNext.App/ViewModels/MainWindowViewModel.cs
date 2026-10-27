using CommunityToolkit.Mvvm.ComponentModel;
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
}
