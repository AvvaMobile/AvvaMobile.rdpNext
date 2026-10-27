using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Credentials;
using RdpNext.Core.Model;
using RdpNext.Rdp;

namespace RdpNext.App.ViewModels;

public enum SessionState
{
    Connecting,
    Connected,
    Disconnected,
}

// Bir sunucunun RDP oturumu. Oturumdan gelen olaylar arayüz iş parçacığına aktarılır;
// eski bir oturumdan geç gelen olay yok sayılır.
public sealed partial class SessionTabViewModel : TabViewModel
{
    private static readonly TimeSpan ResizeDelay = TimeSpan.FromMilliseconds(300);
    private readonly TimeProvider _time;
    private readonly object _resizeGate = new();
    private ITimer? _resizeTimer;
    private (int Width, int Height) _pendingSize;

    public SessionTabViewModel(ServerNodeViewModel server, TimeProvider? time = null)
    {
        Server = server;
        _time = time ?? TimeProvider.System;
        server.State = SessionState.Connecting;
        // Sunucunun adı, rengi ya da ikonu değişince sekme de güncellenir.
        server.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ServerNodeViewModel.State) or nameof(ServerNodeViewModel.IsConnected)
                or nameof(ServerNodeViewModel.IsConnecting) or nameof(ServerNodeViewModel.IsDisconnected))
                return;
            TitleChanged();
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(Icon));
        };
    }

    public ServerNodeViewModel Server { get; }

    public override string Title => Server.Name;

    public override ServerColor? Color => Server.Color;

    public override ServerIcon Icon => Server.Icon;

    public SessionScreen Screen { get; } = new();

    public override bool CanReconnect => true;

    public string ConnectingTitle => string.Format(Strings.Strings.ConnectingTo, Title);

    // "Bağlanıyor" yazısı soru kartı açıkken gizlenir.
    public bool ShowConnecting => State == SessionState.Connecting && !IsAskingCredentials && CertificatePrompt is null;

    public bool ShowDisconnected => State == SessionState.Disconnected && !IsAskingCredentials;

    public IRdpSession? Session { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnecting), nameof(ShowDisconnected))]
    private SessionState _state = SessionState.Connecting;

    partial void OnStateChanged(SessionState value) => Server.State = value;

    // Sekme kendini kapatmak istiyor (sertifika reddedildi gibi).
    public event Action? CloseRequested;

    // Sunucu kaydı değişti (güvenilen sertifika gibi); diske yazılmalı.
    public event Action? ServerChanged;

    // Doğrulanamayan sertifika soruluyor; cevap gelene kadar bağlanma bekler.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnecting))]
    private RdpCertificate? _certificatePrompt;

    private TaskCompletionSource<bool>? _certificateAnswer;

    // Sunucuda kopyalanan metin; görünüm bunu bu bilgisayarın panosuna yazar.
    public event Action<string>? RemoteClipboardText;

    // Sunucunun imleç şekli; null ise sistemin oku.
    [ObservableProperty]
    private RdpPointer? _pointer;

    private Func<IRdpSession>? _newSession;
    private Size _size;
    // Son bağlanılan bilgiler; Reconnect aynı bilgilerle bağlanır (şifre yalnız bellekte).
    private (string Username, string Password, string? Domain) _lastSignIn;

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

    // Kullanıcı ya da şifre bulunamazsa bağlanmadan önce sorulur.
    public void Connect(Func<IRdpSession> newSession, ResolvedCredentials credentials, Size size)
    {
        _newSession = newSession;
        _size = size;
        if (credentials.Username is null || credentials.Password is null)
            AskCredentials(credentials.Username, credentials.Domain);
        else
            Start(credentials.Username, credentials.Password, credentials.Domain);
    }

    private void Start(string username, string password, string? domain)
    {
        Session?.Dispose();
        var session = _newSession!();
        Session = session;
        State = SessionState.Connecting;
        _lastSignIn = (username, password, domain);
        session.VerifyCertificate = certificate => AskCertificate(session, certificate);
        session.Connected += () => OnUi(session, () => State = SessionState.Connected);
        session.Ended += (reason, _) => OnUi(session, () => Ended(reason));
        session.Frame += Screen.CopyFrame;
        session.PointerChanged += pointer => OnUi(session, () => Pointer = pointer);
        session.RemoteClipboardText += text => OnUi(session, () => RemoteClipboardText?.Invoke(text));
        var (width, height) = RemoteSize(_size.Width, _size.Height);
        session.Start(new RdpConnectOptions(Server.Host, Server.Server.Port, username, domain, password, width, height));
    }

    private void AskCredentials(string? username, string? domain)
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
        Start(username, password, domain);
    }

    [RelayCommand]
    private void CancelSignIn()
    {
        IsAskingCredentials = false;
        CloseRequested?.Invoke();
    }

    private void Ended(RdpEndReason reason)
    {
        if (reason == RdpEndReason.CertificateRejected)
            CloseRequested?.Invoke();
        else if (reason == RdpEndReason.AuthFailed)
            AskCredentials(_lastSignIn.Username, _lastSignIn.Domain);
        else if (reason == RdpEndReason.UserLogoff)
            CloseRequested?.Invoke();
        else
            State = SessionState.Disconnected;
    }

    // Kopan oturum aynı sekmede, aynı bilgilerle yeniden açılır.
    [RelayCommand]
    private void Reconnect()
    {
        if (_newSession is null || IsAskingCredentials)
            return;
        if (_lastSignIn.Username is null)
            AskCredentials(null, null);
        else
            Start(_lastSignIn.Username, _lastSignIn.Password, _lastSignIn.Domain);
    }

    // Güvenilen parmak izi aynıysa sormadan kabul; değilse kullanıcıya sorulur.
    private Task<bool> AskCertificate(IRdpSession from, RdpCertificate certificate)
    {
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            if (Session != from)
                answer.TrySetResult(false);
            else if (Server.Server.CertificateFingerprint == certificate.Fingerprint)
                answer.TrySetResult(true);
            else
            {
                _certificateAnswer?.TrySetResult(false);
                _certificateAnswer = answer;
                CertificatePrompt = certificate;
            }
        });
        return answer.Task;
    }

    [RelayCommand]
    private void TrustCertificate()
    {
        if (CertificatePrompt is not { } certificate)
            return;
        Server.Server.CertificateFingerprint = certificate.Fingerprint;
        ServerChanged?.Invoke();
        AnswerCertificate(true);
    }

    [RelayCommand]
    private void CancelCertificate() => AnswerCertificate(false);

    private void AnswerCertificate(bool trust)
    {
        CertificatePrompt = null;
        _certificateAnswer?.TrySetResult(trust);
        _certificateAnswer = null;
    }

    // Oturum alanı boyut değiştirdi: son değişiklikten 300 ms sonra sunucudan bu çözünürlük istenir.
    public void ViewSizeChanged(double width, double height)
    {
        _size = new Size(width, height);
        if (State != SessionState.Connected || Session is not { } session)
            return;
        lock (_resizeGate)
        {
            _pendingSize = RemoteSize(width, height);
            _resizeTimer?.Dispose();
            _resizeTimer = _time.CreateTimer(_ => SendResize(session), null, ResizeDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void SendResize(IRdpSession session)
    {
        (int Width, int Height) size;
        lock (_resizeGate)
            size = _pendingSize;
        if (Session == session)
            session.Resize(size.Width, size.Height);
    }

    // RDP'nin kabul ettiği çözünürlük: genişlik çift, ikisi de 200..8192.
    private static (int Width, int Height) RemoteSize(double width, double height) =>
        (Math.Clamp((int)width & ~1, 200, 8192), Math.Clamp((int)height, 200, 8192));

    private void OnUi(IRdpSession from, Action action) => Dispatcher.UIThread.Post(() =>
    {
        if (Session == from)
            action();
    });

    public override void OnClosed()
    {
        AnswerCertificate(false);
        lock (_resizeGate)
            _resizeTimer?.Dispose();
        Session?.Dispose();
        Session = null;
        Server.State = null;
    }
}
