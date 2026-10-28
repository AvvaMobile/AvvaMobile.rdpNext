using System.Net.Sockets;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace RdpNext.Ssh;

// SSH.NET üzerinde tek bir terminal bağlantısı. Bağlanma ve okuma kendi arka plan işinde döner;
// şifre ve anahtar parolası hiçbir mesaja yazılmaz.
public sealed class SshSession : ISshSession
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private SshClient? _client;
    private volatile ShellStream? _shell;
    private PrivateKeyFile? _key;
    private bool _hostKeyRejected;
    private volatile bool _disposed;
    // Connect sürüyor; Dispose istemciyi kapatmaz, bağlanma bitince arka plan işi kapatır.
    private bool _connecting;
    private int _ended;

    public event Action? Connected;

    public event Action<byte[]>? Output;

    public event Action<SshEndReason, string?>? Ended;

    public Func<SshHostKey, Task<bool>>? VerifyHostKey { get; set; }

    public void Start(SshConnectOptions options)
    {
        var thread = new Thread(() => Run(options)) { IsBackground = true, Name = "ssh-session" };
        thread.Start();
    }

    public void Send(byte[] data)
    {
        var shell = _shell;
        if (shell is null)
            return;
        try
        {
            shell.Write(data, 0, data.Length);
            shell.Flush();
        }
        catch (Exception e) when (e is ObjectDisposedException or SshException or IOException or SocketException)
        {
            // Bağlantı kapanıyor; sonucu okuma döngüsü bildirir.
        }
    }

    public void Resize(int columns, int rows)
    {
        try
        {
            _shell?.ChangeWindowSize((uint)columns, (uint)rows, 0, 0);
        }
        catch (Exception e) when (e is ObjectDisposedException or SshException or IOException or SocketException)
        {
        }
    }

    private void Run(SshConnectOptions options)
    {
        // KeyFile null değilse (boş olsa bile) yalnız anahtarla girilir; şifreye düşülmez.
        var keyMode = options.KeyFile is not null;
        AuthenticationMethod[] methods;
        if (keyMode)
        {
            if (LoadKey(options) is not { } key)
                return;
            methods = [new PrivateKeyAuthenticationMethod(options.Username, key)];
        }
        else
        {
            // Şifre önce "password", sunucu onu kapattıysa "keyboard-interactive" yöntemiyle sunulur.
            var password = options.Password ?? "";
            var interactive = new KeyboardInteractiveAuthenticationMethod(options.Username);
            interactive.AuthenticationPrompt += (_, e) =>
            {
                foreach (var prompt in e.Prompts)
                    prompt.Response = password;
            };
            methods = [new PasswordAuthenticationMethod(options.Username, password), interactive];
        }

        var connection = new ConnectionInfo(options.Host, options.Port, options.Username, methods) { Timeout = ConnectTimeout };
        var client = new SshClient(connection) { KeepAliveInterval = KeepAlive };
        // Bağlantı koparsa ShellStream.Read kendiliğinden dönmez; kabuk kapatılıp okuma döngüsü uyandırılır.
        // Bağlanırken gelen hatalar Connect'in fırlattığı istisnadan sınıflanır.
        client.ErrorOccurred += (_, e) =>
        {
            if (_shell is not { } open)
                return;
            End(SshEndReason.Disconnected, e.Exception.Message);
            open.Dispose();
        };
        client.HostKeyReceived += (_, e) =>
        {
            var key = new SshHostKey(e.HostKeyName, "SHA256:" + e.FingerPrintSHA256);
            var trusted = VerifyHostKey is { } verify && verify(key).GetAwaiter().GetResult();
            _hostKeyRejected = !trusted;
            e.CanTrust = trusted;
        };
        lock (_gate)
        {
            if (_disposed)
            {
                client.Dispose();
                return;
            }
            _client = client;
            _connecting = true;
        }

        // Bağlanırken Dispose gelirse istemci burada kapatılır: SSH.NET bağlanma sürerken yapılan Dispose'u
        // yok sayıp oturumu sonradan kurabiliyor; ikinci Dispose da etkisiz kalıyor.
        ShellStream? shell = null;
        Exception? error = null;
        try
        {
            client.Connect();
            shell = client.CreateShellStream("xterm-256color", (uint)options.Columns, (uint)options.Rows, 0, 0, 4096);
        }
        catch (Exception e)
        {
            error = e;
        }
        lock (_gate)
        {
            _connecting = false;
            if (_disposed)
            {
                shell?.Dispose();
                client.Dispose();
                _key?.Dispose();
                return;
            }
            _shell = shell;
        }
        if (error is not null)
        {
            End(Classify(error, keyMode), error.Message);
            return;
        }

        Connected?.Invoke();
        Read(client, shell!);
    }

    private PrivateKeyFile? LoadKey(SshConnectOptions options)
    {
        var path = ExpandHome(options.KeyFile!);
        if (string.IsNullOrWhiteSpace(path))
        {
            End(SshEndReason.KeyUnusable, null);
            return null;
        }
        try
        {
            _key = string.IsNullOrEmpty(options.KeyPassphrase) ? new PrivateKeyFile(path) : new PrivateKeyFile(path, options.KeyPassphrase);
        }
        catch (SshPassPhraseNullOrEmptyException)
        {
            End(SshEndReason.PassphraseNeeded, null);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            End(SshEndReason.KeyUnusable, e.Message);
            return null;
        }
        catch (Exception e) when (e is SshException or ArgumentException or InvalidOperationException or FormatException)
        {
            // Parolalı anahtar yanlış parolayla açılınca çözülen veri bozuk çıkar.
            End(string.IsNullOrEmpty(options.KeyPassphrase) ? SshEndReason.KeyUnusable : SshEndReason.PassphraseNeeded, e.Message);
            return null;
        }
        return _key;
    }

    private void Read(SshClient client, ShellStream shell)
    {
        var buffer = new byte[16384];
        try
        {
            int count;
            while ((count = shell.Read(buffer, 0, buffer.Length)) > 0)
                Output?.Invoke(buffer[..count]);
        }
        catch (Exception e) when (e is ObjectDisposedException or SshException or IOException or SocketException)
        {
            End(SshEndReason.Disconnected, e.Message);
            return;
        }
        // Kabuk kapandı; bağlantı da gittiyse kopmadır.
        End(client.IsConnected ? SshEndReason.Exited : SshEndReason.Disconnected, null);
    }

    private SshEndReason Classify(Exception e, bool keyMode) => e switch
    {
        _ when _hostKeyRejected => SshEndReason.HostKeyRejected,
        SshAuthenticationException => keyMode ? SshEndReason.KeyUnusable : SshEndReason.AuthFailed,
        _ => SshEndReason.ConnectFailed,
    };

    private static string ExpandHome(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Length > 2 ? path[2..] : "")
            : path;

    private void End(SshEndReason reason, string? message)
    {
        if (Interlocked.Exchange(ref _ended, 1) == 0 && !_disposed)
            Ended?.Invoke(reason, message);
    }

    public void Dispose()
    {
        SshClient? client;
        bool connecting;
        lock (_gate)
        {
            _disposed = true;
            connecting = _connecting;
            client = connecting ? null : _client;
            _client = null;
        }
        _shell?.Dispose();
        client?.Dispose();
        if (!connecting)
            _key?.Dispose();
    }
}
