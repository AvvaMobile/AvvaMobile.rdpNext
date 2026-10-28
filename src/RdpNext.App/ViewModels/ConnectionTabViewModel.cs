using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

public enum SessionState
{
    Connecting,
    Connected,
    Disconnected,
}

// Bir sunucunun bağlantı sekmesi (RDP ya da SSH): durum, kullanıcı adı/şifre sorusu, Reconnect.
// Protokole özgü bağlanma türeyen sınıftadır.
public abstract partial class ConnectionTabViewModel : TabViewModel
{
    protected ConnectionTabViewModel(ServerNodeViewModel server)
    {
        Server = server;
        server.State = SessionState.Connecting;
        server.PropertyChanged += OnServerChanged;
    }

    // Sunucunun adı, rengi ya da ikonu değişince sekme de güncellenir. Sekme kapanınca bağ çözülür.
    private void OnServerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ServerNodeViewModel.State) or nameof(ServerNodeViewModel.IsConnected)
            or nameof(ServerNodeViewModel.IsConnecting) or nameof(ServerNodeViewModel.IsDisconnected))
            return;
        TitleChanged();
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(Icon));
    }

    public ServerNodeViewModel Server { get; }

    public override string Title => Server.Name;

    public override ServerColor? Color => Server.Color;

    public override ServerIcon Icon => Server.Icon;

    public override bool CanReconnect => true;

    public string ConnectingTitle => string.Format(Strings.Strings.ConnectingTo, Title);

    // "Bağlanıyor" yazısı soru kartı açıkken gizlenir.
    public bool ShowConnecting => State == SessionState.Connecting && !IsAskingCredentials && !HasPrompt;

    public bool ShowDisconnected => State == SessionState.Disconnected && !IsAskingCredentials;

    // Protokole özgü bir soru kartı (sertifika, sunucu anahtarı, anahtar parolası) açık mı.
    protected virtual bool HasPrompt => false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnecting), nameof(ShowDisconnected))]
    private SessionState _state = SessionState.Connecting;

    partial void OnStateChanged(SessionState value) => Server.State = value;

    // Sekme kendini kapatmak istiyor (sertifika reddedildi gibi).
    public event Action? CloseRequested;

    // Sunucu kaydı değişti (güvenilen sertifika gibi); diske yazılmalı.
    public event Action? ServerChanged;

    protected void RequestClose() => CloseRequested?.Invoke();

    protected void RaiseServerChanged() => ServerChanged?.Invoke();

    // Kullanıcı adı ve şifre soruluyor (bulunamadı ya da sunucu reddetti).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnecting), nameof(ShowDisconnected))]
    private bool _isAskingCredentials;

    [ObservableProperty]
    private string _promptUsername = "";

    [ObservableProperty]
    private string _promptPassword = "";

    [ObservableProperty]
    private string _promptDomain = "";

    [ObservableProperty]
    private bool _promptSave;

    public string SignInTitle => string.Format(Strings.Strings.SignInTo, Title);

    // "Save" işaretliyken girilen kullanıcı, şifre ve domain; sunucuya yazılmalı.
    public event Action<string, string, string?>? CredentialsSaved;

    protected void AskCredentials(string? username, string? domain)
    {
        PromptUsername = username ?? "";
        PromptDomain = domain ?? "";
        PromptPassword = "";
        PromptSave = false;
        IsAskingCredentials = true;
    }

    [RelayCommand]
    private void SignIn()
    {
        if (string.IsNullOrWhiteSpace(PromptUsername) || PromptPassword.Length == 0)
            return;
        var (username, password) = (PromptUsername.Trim(), PromptPassword);
        var domain = string.IsNullOrWhiteSpace(PromptDomain) ? null : PromptDomain.Trim();
        IsAskingCredentials = false;
        PromptPassword = "";
        if (PromptSave)
            CredentialsSaved?.Invoke(username, password, domain);
        StartWith(username, password, domain);
    }

    [RelayCommand]
    private void CancelSignIn()
    {
        IsAskingCredentials = false;
        RequestClose();
    }

    // Sorudan gelen bilgilerle bağlanır.
    protected abstract void StartWith(string username, string password, string? domain);

    // Kopan oturum aynı sekmede, aynı bilgilerle yeniden açılır.
    [RelayCommand]
    private void Reconnect()
    {
        if (!IsAskingCredentials)
            ReconnectCore();
    }

    protected abstract void ReconnectCore();

    public override void OnClosed()
    {
        Server.PropertyChanged -= OnServerChanged;
        Server.State = null;
    }
}
