using RdpNext.Rdp;

namespace RdpNext.App.Tests;

// Gerçek bağlantı açmayan oturum: çağrıları kaydeder, olayları testin istediği an tetikler.
public sealed class FakeRdpSession : IRdpSession
{
    public List<RdpConnectOptions> Starts { get; } = [];

    public List<(RdpMouseFlags Flags, int X, int Y)> Mouse { get; } = [];

    public bool Disposed { get; private set; }

    public event Action? Connected;

    public event Action<RdpFrame>? Frame;

    public event Action<RdpEndReason, string?>? Ended;

    public void Start(RdpConnectOptions options) => Starts.Add(options);

    public void SendMouse(RdpMouseFlags flags, int x, int y) => Mouse.Add((flags, x, y));

    public void Dispose() => Disposed = true;

    public void RaiseConnected() => Connected?.Invoke();

    public void RaiseEnded(RdpEndReason reason) => Ended?.Invoke(reason, null);

    public unsafe void RaiseFrame(byte[] bgra, int width, int height)
    {
        fixed (byte* p = bgra)
            Frame?.Invoke(new RdpFrame((IntPtr)p, width, height, width * 4, 0, 0, width, height));
    }
}
