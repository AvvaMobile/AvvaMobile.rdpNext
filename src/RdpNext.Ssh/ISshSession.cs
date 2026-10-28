namespace RdpNext.Ssh;

// KeyFile doluysa yalnız anahtarla, boşsa yalnız şifreyle girilir. KeyPassphrase anahtar şifreliyse gerekir.
public sealed record SshConnectOptions(string Host, int Port, string Username, string? Password,
    string? KeyFile, string? KeyPassphrase, int Columns, int Rows);

// Sunucu anahtarı; Fingerprint OpenSSH biçiminde ("SHA256:...").
public sealed record SshHostKey(string Algorithm, string Fingerprint);

public enum SshEndReason
{
    // Sunucuda kabuk kapandı (exit).
    Exited,
    Disconnected,
    AuthFailed,
    // Anahtar parolalı; parola yok ya da yanlış.
    PassphraseNeeded,
    // Anahtar dosyası yok, okunamıyor ya da sunucu anahtarı kabul etmedi.
    KeyUnusable,
    HostKeyRejected,
    ConnectFailed,
}

// Tek bir SSH terminal bağlantısı. Olaylar oturumun kendi iş parçacığından gelir.
public interface ISshSession : IDisposable
{
    event Action? Connected;

    // Sunucudan gelen ham terminal çıktısı (UTF-8).
    event Action<byte[]>? Output;

    event Action<SshEndReason, string?>? Ended;

    // Sunucu anahtarı oturum iş parçacığından sorulur ve cevap beklenir; null ise reddedilir.
    Func<SshHostKey, Task<bool>>? VerifyHostKey { get; set; }

    // Hemen döner; bağlanma arka planda sürer.
    void Start(SshConnectOptions options);

    void Send(byte[] data);

    void Resize(int columns, int rows);
}
