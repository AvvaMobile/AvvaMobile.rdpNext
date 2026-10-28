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
            _protocol = existing.Protocol;
            _os = existing.Os;
            _auth = existing.Auth;
            _keyFile = existing.KeyFile ?? "";
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
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _host = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _port = "3389";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUsernameSource))]
    private string _username = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPasswordSource))]
    private string _password = "";

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDomainSource))]
    private string _domain = "";

    // Alt sekme: Genel ya da Gelişmiş.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGeneral))]
    private bool _showAdvanced;

    public bool ShowGeneral
    {
        get => !ShowAdvanced;
        set => ShowAdvanced = !value;
    }

    [ObservableProperty]
    private FolderOption _folder;

    [ObservableProperty]
    private ServerColor _color = ServerColor.Blue;

    // Bağlantı tipi: Windows ya da Linux (ikisi de RDP) ya da SSH; formun geri kalanı buna göre değişir.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRdp), nameof(IsSsh), nameof(IsWindows), nameof(IsLinux), nameof(IsPasswordAuth), nameof(IsKeyAuth),
        nameof(PasswordRow), nameof(PasswordColumn))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private Protocol _protocol = Protocol.Rdp;

    public bool IsRdp
    {
        get => Protocol == Protocol.Rdp;
        set { if (value) Protocol = Protocol.Rdp; }
    }

    public bool IsSsh
    {
        get => Protocol == Protocol.Ssh;
        set { if (value) Protocol = Protocol.Ssh; }
    }

    // RDP sunucusunun işletim sistemi; yalnız ikonu değiştirir.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWindows), nameof(IsLinux))]
    private ServerOs _os;

    public bool IsWindows
    {
        get => IsRdp && Os == ServerOs.Windows;
        set { if (value) SelectRdp(ServerOs.Windows); }
    }

    public bool IsLinux
    {
        get => IsRdp && Os == ServerOs.Linux;
        set { if (value) SelectRdp(ServerOs.Linux); }
    }

    private void SelectRdp(ServerOs os)
    {
        Os = os;
        Protocol = Protocol.Rdp;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordAuth), nameof(IsKeyAuth), nameof(PasswordLabel), nameof(PasswordColumn))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private SshAuth _auth = SshAuth.Password;

    public bool IsPasswordAuth
    {
        get => IsRdp || Auth == SshAuth.Password;
        set { if (value) Auth = SshAuth.Password; }
    }

    public bool IsKeyAuth
    {
        get => IsSsh && Auth == SshAuth.Key;
        set { if (value) Auth = SshAuth.Key; }
    }

    // Anahtarla girişte şifre alanı anahtar parolasıdır (aynı gizli bilgi yuvası).
    public string PasswordLabel => IsKeyAuth ? Strings.Strings.KeyPassphrase : Strings.Strings.Password;

    // Formdaki şifre alanının yeri: RDP'de kullanıcının yanı, SSH şifrede alt satırın solu, SSH anahtarda anahtar dosyasının yanı.
    public int PasswordRow => IsRdp ? 0 : 2;

    public int PasswordColumn => IsSsh && !IsKeyAuth ? 0 : 2;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _keyFile = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScreenIndex))]
    private ScreenMode _screen = ScreenMode.FitToWindow;

    // Ekran seçicisindeki sıra: 0 Full Screen, 1 Windowed.
    public int ScreenIndex
    {
        get => Screen == ScreenMode.FullScreen ? 0 : 1;
        set => Screen = value == 0 ? ScreenMode.FullScreen : ScreenMode.FitToWindow;
    }

    // Gruptan gelen değer alanda soluk görünür, kaynağı ("Üretim'den") alanın sağında.
    [ObservableProperty]
    private string? _usernameHint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUsernameSource))]
    private string? _usernameSource;

    [ObservableProperty]
    private string? _passwordHint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPasswordSource))]
    private string? _passwordSource;

    [ObservableProperty]
    private string? _domainHint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDomainSource))]
    private string? _domainSource;

    public bool ShowUsernameSource => UsernameSource is not null && Username.Length == 0;

    public bool ShowPasswordSource => PasswordSource is not null && Password.Length == 0;

    public bool ShowDomainSource => DomainSource is not null && Domain.Length == 0;

    partial void OnFolderChanged(FolderOption value) => UpdateHints();

    // Port öbür tipin varsayılanındaysa yeni tipin varsayılanına geçer; elle yazılan port korunur.
    partial void OnProtocolChanged(Protocol value)
    {
        if (value == Protocol.Ssh && Port == "3389")
            Port = "22";
        else if (value == Protocol.Rdp && Port == "22")
            Port = "3389";
        if (value == Protocol.Ssh)
            ShowAdvanced = false;
        // Anahtar parolası RDP şifresine dönüşmesin; alan boşalır, kaydedince yuvadan silinir.
        if (value == Protocol.Rdp && Auth == SshAuth.Key)
            Password = "";
        OnPropertyChanged(nameof(PasswordLabel));
        UpdateHints();
    }

    // Giriş yöntemi değişince yuvadaki bilgi başka bir şey olur (şifre ya da anahtar parolası); alan boşalır.
    partial void OnAuthChanged(SshAuth value)
    {
        Password = "";
        UpdateHints();
    }

    [RelayCommand]
    private async Task ChooseKeyFile()
    {
        if (await _owner.Files.OpenKeyFileAsync() is { } path)
            KeyFile = path;
    }

    // Anahtarla girişte anahtar dosyası zorunlu; yoksa bağlanırken boş şifreyle denenirdi.
    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(Host) && int.TryParse(Port, out var port) && port is >= 1 and <= 65535
        && (!IsKeyAuth || !string.IsNullOrWhiteSpace(KeyFile));

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        Store();
        _owner.ShowToast(Strings.Strings.Saved);
    }

    private ServerNodeViewModel Store()
    {
        var tree = _owner.Tree;
        var server = Existing ?? new Server();
        server.Host = Host.Trim();
        server.Name = string.IsNullOrWhiteSpace(Name) ? server.Host : Name.Trim();
        server.Port = int.Parse(Port);
        server.Username = Blank(Username);
        server.Color = Color;
        server.Screen = Screen;
        if (server.Protocol != Protocol)
        {
            server.HostKeyFingerprint = null;
            server.HostKeyType = null;
        }
        server.Protocol = Protocol;
        server.Os = IsSsh ? ServerOs.Windows : Os;
        server.Domain = IsSsh ? null : Blank(Domain);
        server.Auth = IsSsh ? Auth : SshAuth.Password;
        server.KeyFile = IsKeyAuth ? Blank(KeyFile) : null;

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
        UsernameHint = inherited.Username?.Value;
        UsernameSource = Source(inherited.Username);
        DomainHint = inherited.Domain?.Value;
        DomainSource = Source(inherited.Domain);
        // Anahtar parolası klasörden devralınmaz.
        var password = IsKeyAuth ? null : inherited.Password;
        PasswordHint = password is null ? null : "••••••";
        PasswordSource = Source(password);
    }

    private static string? Source(InheritedValue? value) => value is null ? null : string.Format(Strings.Strings.InheritedFrom, value.FromGroup);

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
