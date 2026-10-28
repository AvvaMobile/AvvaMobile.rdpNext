using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Credentials;
using RdpNext.Core.Model;
using RdpNext.Ssh;
using SvcSystems.UI.Terminal;

namespace RdpNext.App.ViewModels;

// Bir sunucunun SSH terminal oturumu. Oturumdan gelen olaylar arayüz iş parçacığına aktarılır;
// eski bir oturumdan geç gelen olay yok sayılır.
public sealed partial class SshTabViewModel : ConnectionTabViewModel
{
    public SshTabViewModel(ServerNodeViewModel server) : base(server)
    {
        Terminal.UserInput += (_, e) => Send(e.Data.ToArray());
        Terminal.SizeChanged += (_, e) => TerminalSizeChanged(e.Cols, e.Rows);
    }

    // Terminalin ekranı ve geçmişi; sekme değişince görünüm yeniden kurulduğu için burada durur.
    public TerminalControlModel Terminal { get; } = new(new TerminalOptions { TermName = "xterm-256color" });

    public ISshSession? Session { get; private set; }

    // Sunucudan gelen terminal çıktısı; görünüm terminale besler.
    public event Action<byte[]>? Output;

    // "Save" işaretliyken girilen anahtar parolası; sunucunun gizli bilgi yuvasına yazılmalı.
    public event Action<string>? PassphraseSaved;

    private bool IsKeyAuth => Server.Server.Auth == SshAuth.Key;

    protected override Protocol Protocol => Protocol.Ssh;

    public override bool PromptShowsDomain => false;

    // Anahtarla girişte soru kartı yalnız kullanıcı adını ister.
    public override bool PromptNeedsPassword => !IsKeyAuth;

    // Doğrulanacak sunucu anahtarı; cevap gelene kadar bağlanma bekler.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnecting), nameof(IsHostKeyChanged))]
    private SshHostKey? _hostKeyPrompt;

    // Daha önce güvenilen ve şimdi farklı gelen anahtarın parmak izi; ilk bağlantıda null.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHostKeyChanged))]
    private string? _previousHostKey;

    public bool IsHostKeyChanged => HostKeyPrompt is not null && PreviousHostKey is not null;

    private TaskCompletionSource<bool>? _hostKeyAnswer;

    // Bu oturumda kullanıcı az önce "Trust" dedi.
    private bool _retryAfterTrust;

    // Anahtar parolası soruluyor (anahtar parolalı, kayıtlı parola yok ya da yanlış).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConnecting), nameof(ShowDisconnected))]
    private bool _isAskingPassphrase;

    [ObservableProperty]
    private string _promptPassphrase = "";

    protected override bool HasPrompt => HostKeyPrompt is not null || IsAskingPassphrase;

    private Func<ISshSession>? _newSession;
    private (int Columns, int Rows) _size = (80, 24);
    private (int Columns, int Rows) _startedSize;
    // Son bağlanılan bilgiler; Reconnect aynı bilgilerle bağlanır (şifre yalnız bellekte).
    private (string? Username, string? Secret) _lastSignIn;

    // Kullanıcı adı ya da (şifreyle girişte) şifre yoksa bağlanmadan önce sorulur.
    public void Connect(Func<ISshSession> newSession, ResolvedCredentials credentials)
    {
        _newSession = newSession;
        if (credentials.Username is null || (!IsKeyAuth && credentials.Password is null))
        {
            // Anahtar parolası soruda istenmez; kayıtlıysa sonra kullanılmak üzere saklanır.
            _lastSignIn = (credentials.Username, IsKeyAuth ? credentials.Password : null);
            AskCredentials(credentials.Username, null);
        }
        else
            Start(credentials.Username, credentials.Password);
    }

    protected override void StartWith(string username, string password, string? domain) =>
        Start(username, IsKeyAuth ? _lastSignIn.Secret : password);

    private void Start(string username, string? secret)
    {
        Session?.Dispose();
        var session = _newSession!();
        Session = session;
        State = SessionState.Connecting;
        _lastSignIn = (username, secret);
        session.VerifyHostKey = key => AskHostKey(session, key);
        session.Connected += () => OnUi(session, OnConnected);
        session.Ended += (reason, _) => OnUi(session, () => Ended(reason));
        // Arayüz işlemeden önce gelen parçalar biriktirilir, terminale tek seferde verilir.
        var pending = new MemoryStream();
        var scheduled = false;
        session.Output += bytes =>
        {
            lock (pending)
            {
                pending.Write(bytes);
                if (scheduled)
                    return;
                scheduled = true;
            }
            OnUi(session, () =>
            {
                byte[] chunk;
                lock (pending)
                {
                    chunk = pending.ToArray();
                    pending.SetLength(0);
                    scheduled = false;
                }
                Terminal.Feed(chunk, chunk.Length);
                Output?.Invoke(chunk);
            });
        };
        var server = Server.Server;
        _startedSize = _size;
        // Anahtar modunda yol boş olsa da "" gider; oturum şifreye düşmez, anahtar kullanılamadı der.
        session.Start(new SshConnectOptions(server.Host, server.Port, username,
            IsKeyAuth ? null : secret, IsKeyAuth ? server.KeyFile ?? "" : null, IsKeyAuth ? secret : null, _size.Columns, _size.Rows,
            server.HostKeyType));
    }

    // Bağlanırken terminal boyutu değiştiyse sunucuya şimdiki boyut bildirilir.
    private void OnConnected()
    {
        _retryAfterTrust = false;
        State = SessionState.Connected;
        if (_size != _startedSize)
            Session?.Resize(_size.Columns, _size.Rows);
    }

    private void Ended(SshEndReason reason)
    {
        // Parmak izi incelenirken bağlanma zaman aşımına uğramış olabilir; "Trust" sonrası bir kez yeniden denenir.
        var retry = _retryAfterTrust;
        _retryAfterTrust = false;
        if (retry && reason == SshEndReason.ConnectFailed)
        {
            Start(_lastSignIn.Username!, _lastSignIn.Secret);
            return;
        }
        switch (reason)
        {
            case SshEndReason.Exited or SshEndReason.HostKeyRejected:
                RequestClose();
                break;
            case SshEndReason.AuthFailed:
                AskCredentials(_lastSignIn.Username, null);
                break;
            case SshEndReason.PassphraseNeeded:
                PromptPassphrase = "";
                PromptSave = false;
                IsAskingPassphrase = true;
                break;
            case SshEndReason.KeyUnusable:
                DisconnectedTitle = Strings.Strings.KeyUnusable;
                State = SessionState.Disconnected;
                break;
            default:
                ShowEnded(reason == SshEndReason.ConnectFailed);
                break;
        }
    }

    [RelayCommand]
    private void SubmitPassphrase()
    {
        if (PromptPassphrase.Length == 0)
            return;
        var passphrase = PromptPassphrase;
        IsAskingPassphrase = false;
        PromptPassphrase = "";
        if (PromptSave)
            PassphraseSaved?.Invoke(passphrase);
        Start(_lastSignIn.Username!, passphrase);
    }

    [RelayCommand]
    private void CancelPassphrase()
    {
        IsAskingPassphrase = false;
        RequestClose();
    }

    protected override void ReconnectCore()
    {
        if (_newSession is null)
            return;
        if (_lastSignIn.Username is null)
            AskCredentials(null, null);
        else
            Start(_lastSignIn.Username, _lastSignIn.Secret);
    }

    // Güvenilen parmak izi aynıysa sormadan kabul; değilse kullanıcıya sorulur.
    private Task<bool> AskHostKey(ISshSession from, SshHostKey key)
    {
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            if (Session != from)
                answer.TrySetResult(false);
            else if (Server.Server.HostKeyFingerprint == key.Fingerprint)
                answer.TrySetResult(true);
            else
            {
                _hostKeyAnswer?.TrySetResult(false);
                _hostKeyAnswer = answer;
                PreviousHostKey = Server.Server.HostKeyFingerprint;
                HostKeyPrompt = key;
            }
        });
        return answer.Task;
    }

    [RelayCommand]
    private void TrustHostKey()
    {
        if (HostKeyPrompt is not { } key)
            return;
        Server.Server.HostKeyFingerprint = key.Fingerprint;
        Server.Server.HostKeyType = key.Algorithm;
        RaiseServerChanged();
        _retryAfterTrust = true;
        AnswerHostKey(true);
    }

    [RelayCommand]
    private void CancelHostKey() => AnswerHostKey(false);

    private void AnswerHostKey(bool trust)
    {
        HostKeyPrompt = null;
        PreviousHostKey = null;
        _hostKeyAnswer?.TrySetResult(trust);
        _hostKeyAnswer = null;
    }

    // Terminale yazılan; yalnız bağlıyken sunucuya gider.
    public void Send(byte[] data)
    {
        if (State == SessionState.Connected)
            Session?.Send(data);
    }

    // Terminalin satır/sütun sayısı değişti; bağlıysa sunucuya bildirilir.
    public void TerminalSizeChanged(int columns, int rows)
    {
        if (columns <= 0 || rows <= 0 || (columns, rows) == _size)
            return;
        _size = (columns, rows);
        if (State == SessionState.Connected)
            Session?.Resize(columns, rows);
    }

    private void OnUi(ISshSession from, Action action) => Dispatcher.UIThread.Post(() =>
    {
        if (Session == from)
            action();
    });

    public override void OnClosed()
    {
        AnswerHostKey(false);
        Session?.Dispose();
        Session = null;
        base.OnClosed();
        Terminal.Dispose();
    }
}
