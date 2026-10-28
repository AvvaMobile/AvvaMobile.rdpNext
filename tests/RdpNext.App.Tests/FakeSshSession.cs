using RdpNext.Ssh;

namespace RdpNext.App.Tests;

// Gerçek bağlantı açmayan SSH oturumu: çağrıları kaydeder, olayları testin istediği an tetikler.
public sealed class FakeSshSession : ISshSession
{
    public List<SshConnectOptions> Starts { get; } = [];

    public List<byte[]> Sent { get; } = [];

    public List<(int Columns, int Rows)> Resizes { get; } = [];

    public bool Disposed { get; private set; }

    public Func<SshHostKey, Task<bool>>? VerifyHostKey { get; set; }

    public event Action? Connected;

    public event Action<byte[]>? Output;

    public event Action<SshEndReason, string?>? Ended;

    public void Start(SshConnectOptions options) => Starts.Add(options);

    public void Send(byte[] data) => Sent.Add(data);

    public void Resize(int columns, int rows) => Resizes.Add((columns, rows));

    public void RaiseConnected() => Connected?.Invoke();

    public void RaiseOutput(byte[] data) => Output?.Invoke(data);

    public void RaiseEnded(SshEndReason reason, string? message = null) => Ended?.Invoke(reason, message);

    public void Dispose() => Disposed = true;
}
