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

    // null: sistemin varsayılan oku.
    event Action<RdpPointer?>? PointerChanged;

    // Hemen döner; bağlanma arka planda sürer.
    void Start(RdpConnectOptions options);

    void SendMouse(RdpMouseFlags flags, int x, int y);

    // Tekerlek; bir çentik 120, yukarı ve sağ artı.
    void SendWheel(int delta, bool horizontal, int x, int y);

    void SendKey(ushort code, bool extended, bool down);

    // Klavye düzeninden bağımsız karakter (UTF-16 birimi), basılıp bırakılır.
    void SendUnicode(char c);
}
