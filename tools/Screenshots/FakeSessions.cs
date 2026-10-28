using RdpNext.Rdp;
using RdpNext.Ssh;

namespace RdpNext.Screenshots;

// Gerçek bağlantı açmayan RDP oturumu: bağlandı der, verilen görüntüyü ekrana basar.
public sealed class DemoRdpSession : IRdpSession
{
    public RdpConnectOptions? Options { get; private set; }

    public Func<RdpCertificate, Task<bool>>? VerifyCertificate { get; set; }

    public event Action? Connected;

    public event Action<RdpFrame>? Frame;

    public event Action<RdpEndReason, string?>? Ended { add { } remove { } }

    public event Action<string?>? RemoteClipboardText { add { } remove { } }

    public event Action<IReadOnlyList<string>>? RemoteClipboardFiles { add { } remove { } }

    public event Action? RemoteClipboardTooLarge { add { } remove { } }

    public event Action<RdpPointer?>? PointerChanged { add { } remove { } }

    public void Start(RdpConnectOptions options) => Options = options;

    public void RaiseConnected() => Connected?.Invoke();

    public unsafe void RaiseFrame(byte[] bgra, int width, int height)
    {
        fixed (byte* p = bgra)
            Frame?.Invoke(new RdpFrame((IntPtr)p, width, height, width * 4, 0, 0, width, height));
    }

    public void SendMouse(RdpMouseFlags flags, int x, int y) { }

    public void SendWheel(int delta, bool horizontal, int x, int y) { }

    public void SendKey(ushort code, bool extended, bool down) { }

    public void SetLocalClipboard(string? text, IReadOnlyList<string> files) { }

    public void Resize(int width, int height) { }

    public void SendUnicode(char c) { }

    public void Dispose() { }
}

// Gerçek bağlantı açmayan SSH oturumu: bağlandı der, verilen çıktıyı terminale yazar.
public sealed class DemoSshSession : ISshSession
{
    public Func<SshHostKey, Task<bool>>? VerifyHostKey { get; set; }

    public event Action? Connected;

    public event Action<byte[]>? Output;

    public event Action<SshEndReason, string?>? Ended { add { } remove { } }

    public void Start(SshConnectOptions options) { }

    public void RaiseConnected() => Connected?.Invoke();

    public void RaiseOutput(byte[] data) => Output?.Invoke(data);

    public void Send(byte[] data) { }

    public void Resize(int columns, int rows) { }

    public void Dispose() { }
}
