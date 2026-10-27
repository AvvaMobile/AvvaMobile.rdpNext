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
            if (e.PropertyName is nameof(ServerNodeViewModel.State) or nameof(ServerNodeViewModel.IsConnected))
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

    public IRdpSession? Session { get; private set; }

    [ObservableProperty]
    private SessionState _state = SessionState.Connecting;

    partial void OnStateChanged(SessionState value) => Server.State = value;

    // Sekme kendini kapatmak istiyor (sertifika reddedildi gibi).
    public event Action? CloseRequested;

    // Sunucu kaydı değişti (güvenilen sertifika gibi); diske yazılmalı.
    public event Action? ServerChanged;

    // Doğrulanamayan sertifika soruluyor; cevap gelene kadar bağlanma bekler.
    [ObservableProperty]
    private RdpCertificate? _certificatePrompt;

    private TaskCompletionSource<bool>? _certificateAnswer;

    // Sunucuda kopyalanan metin; görünüm bunu bu bilgisayarın panosuna yazar.
    public event Action<string>? RemoteClipboardText;

    // Sunucunun imleç şekli; null ise sistemin oku.
    [ObservableProperty]
    private RdpPointer? _pointer;

    // Kullanıcı ya da şifre bulunamazsa bağlanmadan kopmuş sayılır.
    public void Connect(IRdpSession session, ResolvedCredentials credentials, Size size)
    {
        Session?.Dispose();
        Session = session;
        State = SessionState.Connecting;
        if (credentials.Username is null || credentials.Password is null)
        {
            State = SessionState.Disconnected;
            return;
        }
        session.VerifyCertificate = certificate => AskCertificate(session, certificate);
        session.Connected += () => OnUi(session, () => State = SessionState.Connected);
        session.Ended += (reason, _) => OnUi(session, () => Ended(reason));
        session.Frame += Screen.CopyFrame;
        session.PointerChanged += pointer => OnUi(session, () => Pointer = pointer);
        session.RemoteClipboardText += text => OnUi(session, () => RemoteClipboardText?.Invoke(text));
        session.Start(new RdpConnectOptions(Server.Host, Server.Server.Port, credentials.Username, credentials.Domain,
            credentials.Password, (int)size.Width, (int)size.Height));
    }

    private void Ended(RdpEndReason reason)
    {
        if (reason == RdpEndReason.CertificateRejected)
            CloseRequested?.Invoke();
        else
            State = SessionState.Disconnected;
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
