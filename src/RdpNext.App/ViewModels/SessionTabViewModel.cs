using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Credentials;
using RdpNext.Core.Model;
using RdpNext.Rdp;

namespace RdpNext.App.ViewModels;

// Bir sunucunun RDP oturumu. Oturumdan gelen olaylar arayüz iş parçacığına aktarılır;
// eski bir oturumdan geç gelen olay yok sayılır.
public sealed partial class SessionTabViewModel : ConnectionTabViewModel
{
    private static readonly TimeSpan ResizeDelay = TimeSpan.FromMilliseconds(300);
    private readonly TimeProvider _time;
    private readonly object _resizeGate = new();
    private ITimer? _resizeTimer;
    private (int Width, int Height) _pendingSize;

    public SessionTabViewModel(ServerNodeViewModel server, TimeProvider? time = null) : base(server)
    {
        _time = time ?? TimeProvider.System;
    }

    public SessionScreen Screen { get; } = new();

    public IRdpSession? Session { get; private set; }

    // Doğrulanamayan sertifika soruluyor; cevap gelene kadar bağlanma bekler.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnecting))]
    private RdpCertificate? _certificatePrompt;

    protected override Protocol Protocol => Protocol.Rdp;

    protected override bool HasPrompt => CertificatePrompt is not null;

    private TaskCompletionSource<bool>? _certificateAnswer;

    // İki tarafın da bildiği son pano metni (sunucuya duyurulan ya da sunucudan gelen). Mac panosu bundan farklıysa
    // sunucuya duyurulur; aynısıysa duyurulmaz, yoksa sunucuda yapılan kopyalamanın üstüne yazılır.
    public string? SharedClipboardText { get; set; }

    // Sunucuda kopyalanan metin; görünüm bunu bu bilgisayarın panosuna yazar.
    public event Action<string?>? RemoteClipboardText;

    // Sunucuda kopyalanıp bu bilgisayara indirilen dosyalar; görünüm Mac panosuna koyar.
    public event Action<IReadOnlyList<string>>? RemoteClipboardFiles;

    // Sunucudaki kopya Mac'e indirilemeyecek kadar büyük; kullanıcıya bildirilir.
    public event Action? ClipboardTooLarge;

    // Sunucunun imleç şekli; null ise sistemin oku.
    [ObservableProperty]
    private RdpPointer? _pointer;

    private Func<IRdpSession>? _newSession;
    private Size _size;
    private (int Width, int Height) _startedSize;
    // Son bağlanılan bilgiler; Reconnect aynı bilgilerle bağlanır (şifre yalnız bellekte).
    private (string Username, string Password, string? Domain) _lastSignIn;

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

    protected override void StartWith(string username, string password, string? domain) => Start(username, password, domain);

    private void Start(string username, string password, string? domain)
    {
        Session?.Dispose();
        var session = _newSession!();
        Session = session;
        State = SessionState.Connecting;
        _lastSignIn = (username, password, domain);
        session.VerifyCertificate = certificate => AskCertificate(session, certificate);
        session.Connected += () => OnUi(session, OnConnected);
        session.Ended += (reason, _) => OnUi(session, () => Ended(reason));
        session.Frame += Screen.CopyFrame;
        session.PointerChanged += pointer => OnUi(session, () => Pointer = pointer);
        session.RemoteClipboardText += text => OnUi(session, () => RemoteClipboardText?.Invoke(text));
        session.RemoteClipboardFiles += files => OnUi(session, () => RemoteClipboardFiles?.Invoke(files));
        session.RemoteClipboardTooLarge += () => OnUi(session, () => ClipboardTooLarge?.Invoke());
        var (width, height) = _startedSize = RemoteSize(_size.Width, _size.Height);
        session.Start(new RdpConnectOptions(Server.Host, Server.Server.Port, username, domain, password, width, height));
    }

    // Bağlanırken görünüm boyutu değiştiyse sunucudan şimdiki boyut istenir.
    private void OnConnected()
    {
        State = SessionState.Connected;
        if (RemoteSize(_size.Width, _size.Height) != _startedSize)
            ViewSizeChanged(_size.Width, _size.Height);
    }

    private void Ended(RdpEndReason reason)
    {
        if (reason == RdpEndReason.CertificateRejected)
            RequestClose();
        else if (reason == RdpEndReason.AuthFailed)
            AskCredentials(_lastSignIn.Username, _lastSignIn.Domain);
        else if (reason == RdpEndReason.UserLogoff)
            RequestClose();
        else
            ShowEnded(reason == RdpEndReason.ConnectFailed);
    }

    protected override void ReconnectCore()
    {
        if (_newSession is null)
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
        RaiseServerChanged();
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
        base.OnClosed();
        Screen.Clear();
    }
}
