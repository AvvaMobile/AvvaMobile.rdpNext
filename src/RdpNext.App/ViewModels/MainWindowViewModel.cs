using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using RdpNext.App.Services;
using RdpNext.Core.Credentials;
using RdpNext.Core.Model;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;
using RdpNext.Rdp;

namespace RdpNext.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(ConnectionStore store, ISecretStore secrets, IConfirmService? confirm = null, IFileDialogService? files = null, SettingsStore? settings = null,
        Func<IRdpSession>? rdp = null, TimeProvider? time = null)
    {
        _rdp = rdp ?? (() => new RdpSession());
        _time = time ?? TimeProvider.System;
        Sessions = new SessionsViewModel(time);
        Store = store;
        Settings = settings ?? new SettingsStore(SettingsStore.Beside(store.FilePath));
        Secrets = new GuardedSecretStore(secrets, message => Notice = message);
        Confirm = confirm ?? new DialogConfirmService();
        Files = files ?? new FileDialogService();
        var loaded = store.Load();
        // Uygulama açılınca klasörler kapalı gelir; oturum içinde açılıp kapananlar arama sonrası korunur.
        Collapse(loaded.Root);
        Tree = new TreeViewModel(loaded.Root);
        _notice = loaded.CorruptBackupPath is { } backup
            ? string.Format(Strings.Strings.CorruptFileNotice, backup)
            : null;
    }

    public ConnectionStore Store { get; }

    private readonly Func<IRdpSession> _rdp;
    private readonly TimeProvider _time;
    private ITimer? _toastTimer;

    // Sağ üstte kısa süre görünen bildirim ("Saved" gibi); null ise gizli.
    [ObservableProperty]
    private string? _toastMessage;

    public void ShowToast(string message)
    {
        ToastMessage = message;
        _toastTimer?.Dispose();
        _toastTimer = _time.CreateTimer(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() => ToastMessage = null),
            null, TimeSpan.FromSeconds(2.5), Timeout.InfiniteTimeSpan);
    }

    // Oturum alanının son boyutu; yeni oturum bu çözünürlükle açılır.
    public Size SessionAreaSize { get; set; } = new(1280, 800);

    private static void Collapse(Group group)
    {
        foreach (var child in group.Children.OfType<Group>())
        {
            child.IsExpanded = false;
            Collapse(child);
        }
    }

    public SettingsStore Settings { get; }

    public ISecretStore Secrets { get; }

    public IConfirmService Confirm { get; }

    public IFileDialogService Files { get; }

    public TreeViewModel Tree { get; }

    [ObservableProperty]
    private string? _notice;

    public SessionsViewModel Sessions { get; }

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    // Oturum kendi tam ekran penceresine geçer ya da oradan sekmesine döner. Yalnız oturum sekmesi tam ekran olur.
    [RelayCommand]
    private void ToggleFullScreen(SessionTabViewModel? tab)
    {
        tab ??= Sessions.Selected as SessionTabViewModel;
        if (tab is null)
            return;
        if (Sessions.Windows.Contains(tab))
            Sessions.Attach(tab);
        else
            Sessions.Detach(tab);
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarVisible = !IsSidebarVisible;

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

    // Yeni klasör, sunucu gibi sekmede açılır; klasörler hep en üste eklenir, iç içe klasör yok.
    [RelayCommand]
    private void NewFolder() => Sessions.OpenTab(new FolderEditorViewModel(this, null, null));

    [RelayCommand]
    private void OpenSettings() => Sessions.OpenTab(Sessions.Find<SettingsViewModel>(_ => true) ?? new SettingsViewModel(this));

    // İçe aktarma, eskileri silerek: liste dosyadakiyle değişir, eski düğümlere bağlı sekmeler ve
    // artık listede olmayan kayıtların şifreleri silinir.
    public void ReplaceTree(Group root, TabViewModel keep)
    {
        var kept = root.Children.SelectMany(Subtree).Select(n => n.Id).ToHashSet();
        var removed = Tree.Root.Children.SelectMany(Subtree).Where(n => !kept.Contains(n.Id)).ToList();
        Sessions.CloseOthers(keep);
        Tree.Replace(root);
        foreach (var node in removed)
            Secrets.DeletePassword(node.Id);
        Persist();
    }

    // İçe aktarma, ekleyerek; eklenen sunucu sayısını döner.
    public int MergeTree(Group root)
    {
        var added = Tree.Merge(root);
        Persist();
        return added;
    }

    [RelayCommand]
    public void NewServer() => Sessions.OpenTab(new ServerEditorViewModel(this, null, SelectedFolder()));

    // Ağaçta sürükle-bırak; kurala uymayan bırakışta false döner, hiçbir şey değişmez.
    public bool Drop(TreeNodeViewModel dragged, TreeNodeViewModel target, DropPlace place)
    {
        if (Tree.ResolveDrop(dragged, target, place) is not { } drop)
            return false;
        Tree.MoveTo(dragged, drop.Parent, drop.Before);
        Persist();
        return true;
    }

    [RelayCommand]
    private void Connect(ServerNodeViewModel server)
    {
        var existing = Sessions.Find<SessionTabViewModel>(t => t.Server == server);
        var tab = Sessions.Open(server);
        if (existing is not null)
            return;
        tab.CloseRequested += () => Sessions.Close(tab);
        tab.ServerChanged += Persist;
        tab.CredentialsSaved += (username, password, domain) => SaveCredentials(server, username, password, domain);
        tab.Connect(_rdp, CredentialResolver.Resolve(server.Server, Secrets), SessionAreaSize);
        if (server.Server.Screen == ScreenMode.FullScreen)
            Sessions.Detach(tab);
    }

    // Şifre sorusunda "Save" işaretliyse: kullanıcı ve domain sunucuya, şifre anahtar zincirine.
    private void SaveCredentials(ServerNodeViewModel server, string username, string password, string? domain)
    {
        server.Server.Username = username;
        server.Server.Domain = domain;
        Secrets.SetPassword(server.Server.Id, password);
        server.Refresh();
        Persist();
    }

    // Uygulama kapanırken açık oturumların hepsi kapatılır.
    public void Shutdown()
    {
        foreach (var tab in Sessions.Tabs.OfType<SessionTabViewModel>().Concat(Sessions.Windows).ToList())
            Sessions.Close(tab);
    }

    [RelayCommand]
    private void ConnectFirstMatch()
    {
        if (Tree.FirstMatch is { } server)
            Connect(server);
    }

    [RelayCommand]
    private void CloseTab(TabViewModel tab) => Sessions.Close(tab);

    [RelayCommand]
    private void ReconnectTab(TabViewModel tab) => (tab as SessionTabViewModel)?.ReconnectCommand.Execute(null);

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
        foreach (var tab in Sessions.Tabs.Concat(Sessions.Windows).Where(t => TabNode(t) is { } n && removed.Contains(n)).ToList())
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
