using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Credentials;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

// Sunucu ekleme ve düzenleme sekmesi (DbGate düzeni).
public sealed partial class ServerEditorViewModel : TabViewModel
{
    private readonly MainWindowViewModel _owner;

    public ServerEditorViewModel(MainWindowViewModel owner, Server? existing, Group? folder)
    {
        _owner = owner;
        Existing = existing;
        _folders = owner.Tree.FolderOptions();
        owner.Tree.StructureChanged += RebuildFolders;
        if (existing is not null)
        {
            _name = existing.Name;
            _host = existing.Host;
            _port = existing.Port.ToString();
            _username = existing.Username ?? "";
            _domain = existing.Domain ?? "";
            _password = _savedPassword = owner.Secrets.GetPassword(existing.Id) ?? "";
            _color = existing.Color;
            _screen = existing.Screen;
            folder = existing.Parent;
        }
        _folder = _folders.FirstOrDefault(f => f.Group == (folder == owner.Tree.Root ? null : folder)) ?? _folders[0];
        UpdateHints();
    }

    public Server? Existing { get; private set; }

    public bool IsNew => Existing is null;

    public override string Title => Existing is null
        ? Strings.Strings.NewServer
        : string.Format(Strings.Strings.SettingsTitle, Existing.Name);

    // Anahtar zincirine yalnız şifre değiştiyse yazılır.
    private string _savedPassword = "";

    [ObservableProperty]
    private IReadOnlyList<FolderOption> _folders;

    public IReadOnlyList<ServerColor> Colors { get; } = Enum.GetValues<ServerColor>();

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ConnectCommand))]
    private string _host = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ConnectCommand))]
    private string _port = "3389";

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private string _domain = "";

    [ObservableProperty]
    private FolderOption _folder;

    [ObservableProperty]
    private ServerColor _color = ServerColor.Blue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScreenIndex))]
    private ScreenMode _screen = ScreenMode.FitToWindow;

    // Ekran seçicisindeki sıra: 0 pencereye sığdır, 1 tam ekran.
    public int ScreenIndex
    {
        get => (int)Screen;
        set => Screen = (ScreenMode)value;
    }

    [ObservableProperty]
    private string? _usernameHint;

    [ObservableProperty]
    private string? _passwordHint;

    [ObservableProperty]
    private string? _domainHint;

    partial void OnFolderChanged(FolderOption value) => UpdateHints();

    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(Host) && int.TryParse(Port, out var port) && port is >= 1 and <= 65535;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => Store();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Connect() => _owner.Sessions.Open(Store());

    [RelayCommand]
    private Task Delete() => Existing is null
        ? Task.CompletedTask
        : _owner.DeleteNodeCommand.ExecuteAsync(_owner.Tree.Node<ServerNodeViewModel>(Existing.Id));

    private ServerNodeViewModel Store()
    {
        var tree = _owner.Tree;
        var server = Existing ?? new Server();
        server.Host = Host.Trim();
        server.Name = string.IsNullOrWhiteSpace(Name) ? server.Host : Name.Trim();
        server.Port = int.Parse(Port);
        server.Username = Blank(Username);
        server.Domain = Blank(Domain);
        server.Color = Color;
        server.Screen = Screen;

        ServerNodeViewModel node;
        if (Existing is null)
        {
            node = tree.AddServer(server, Folder.Group);
            Existing = server;
            OnPropertyChanged(nameof(IsNew));
        }
        else
        {
            node = tree.Node<ServerNodeViewModel>(server.Id);
            if (server.Parent != (Folder.Group ?? tree.Root))
                tree.Move(node, Folder.Group);
            node.Refresh();
        }

        _owner.Persist();
        if (Password != _savedPassword)
        {
            if (string.IsNullOrEmpty(Password))
                _owner.Secrets.DeletePassword(server.Id);
            else
                _owner.Secrets.SetPassword(server.Id, Password);
            _savedPassword = Password;
        }

        Name = server.Name;
        TitleChanged();
        return node;
    }

    // Başka sekmede klasör taşınır ya da silinirse liste yenilenir; seçili klasör gittiyse en üste döner.
    private void RebuildFolders()
    {
        var selected = Folder.Group;
        Folders = _owner.Tree.FolderOptions();
        Folder = Folders.FirstOrDefault(f => f.Group == selected) ?? Folders[0];
    }

    public override void OnClosed() => _owner.Tree.StructureChanged -= RebuildFolders;

    private void UpdateHints()
    {
        var inherited = CredentialResolver.Inherited(Folder.Group ?? _owner.Tree.Root, _owner.Secrets);
        UsernameHint = Hint(inherited.Username?.Value, inherited.Username);
        DomainHint = Hint(inherited.Domain?.Value, inherited.Domain);
        PasswordHint = Hint("••••••", inherited.Password);
    }

    private static string? Hint(string? shown, InheritedValue? value) =>
        value is null ? null : $"{shown} ({TurkishText.From(value.FromGroup)})";

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
