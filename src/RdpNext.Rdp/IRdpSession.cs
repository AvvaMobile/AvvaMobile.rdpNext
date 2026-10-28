namespace RdpNext.Rdp;

public sealed record RdpConnectOptions(string Host, int Port, string Username, string? Domain, string Password, int Width, int Height);

// Buffer yalnız olay süresince geçerli; BGRA32. X, Y, W, H değişen dikdörtgen.
public readonly record struct RdpFrame(IntPtr Buffer, int Width, int Height, int Stride, int X, int Y, int W, int H);

public enum RdpEndReason
{
    UserLogoff,
    Disconnected,
    AuthFailed,
    CertificateRejected,
    ConnectFailed,
}

// Doğrulanamayan sunucu sertifikası. Changed: daha önce başka bir sertifika görülmüştü.
public sealed record RdpCertificate(string Subject, string Issuer, string Fingerprint, bool Changed);

// Sunucunun imleç şekli, BGRA32. Width 0 ise imleç gizli.
public sealed record RdpPointer(byte[] Bgra, int Width, int Height, int HotX, int HotY)
{
    public static RdpPointer Hidden { get; } = new([], 0, 0, 0, 0);
}

// FreeRDP PTR_FLAGS_* değerleri.
[Flags]
public enum RdpMouseFlags : ushort
{
    WheelNegative = 0x0100,
    Wheel = 0x0200,
    HWheel = 0x0400,
    Move = 0x0800,
    Button1 = 0x1000,
    Button2 = 0x2000,
    Button3 = 0x4000,
    Down = 0x8000,
}

// Tek bir RDP bağlantısı. Olaylar oturumun kendi iş parçacığından gelir.
public interface IRdpSession : IDisposable
{
    event Action? Connected;

    event Action<RdpFrame>? Frame;

    event Action<RdpEndReason, string?>? Ended;

    // Sunucuda kopyalanan metin; null ise sunucuda metin dışı bir şey (dosya gibi) kopyalandı.
    event Action<string?>? RemoteClipboardText;

    // Sunucuda kopyalanan dosyalar bu bilgisayara indirildi (tam yollar, en üstteki öğeler).
    event Action<IReadOnlyList<string>>? RemoteClipboardFiles;

    // Sunucudaki kopya 2 GB'tan büyük; bu bilgisayara indirilmedi.
    event Action? RemoteClipboardTooLarge;

    // null: sistemin varsayılan oku.
    event Action<RdpPointer?>? PointerChanged;

    // Sertifika doğrulanamazsa oturum iş parçacığından çağrılır ve cevap beklenir; null ise reddedilir.
    Func<RdpCertificate, Task<bool>>? VerifyCertificate { get; set; }

    // Hemen döner; bağlanma arka planda sürer.
    void Start(RdpConnectOptions options);

    void SendMouse(RdpMouseFlags flags, int x, int y);

    // Tekerlek; bir çentik 120, yukarı ve sağ artı.
    void SendWheel(int delta, bool horizontal, int x, int y);

    void SendKey(ushort code, bool extended, bool down);

    // Bu bilgisayarın panosundaki metin ve dosyalar (tam yol); sunucuda yapıştırılınca bunlar gider.
    void SetLocalClipboard(string? text, IReadOnlyList<string> files);

    // Sunucudan yeni çözünürlük ister (disp kanalı). Sunucu desteklemiyorsa görüntü aynı kalır.
    void Resize(int width, int height);

    // Klavye düzeninden bağımsız karakter (UTF-16 birimi), basılıp bırakılır.
    void SendUnicode(char c);
}
