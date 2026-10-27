using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace RdpNext.Rdp;

// FreeRDP oturumu: bağlanma ve olay döngüsü kendi iş parçacığında döner. Fare, klavye gibi istekler
// kuyruğa girer ve aynı iş parçacığında gönderilir; FreeRDP nesnesine iki iş parçacığı aynı anda dokunmaz.
public sealed unsafe class RdpSession : IRdpSession
{
    private const int PumpTimeoutMs = 100;
    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly ConcurrentQueue<Action<IntPtr>> _requests = new();
    private GCHandle _self;
    private Thread? _thread;
    private IntPtr _ctx;
    private volatile bool _stopping;

    public event Action? Connected;

    public event Action<RdpFrame>? Frame;

    public event Action<RdpEndReason, string?>? Ended;

    public event Action<RdpPointer?>? PointerChanged;

    public event Action<string>? RemoteClipboardText;

    // C katmanı ve FreeRDP bu makinede yüklenebiliyor mu.
    public static bool IsAvailable { get; } =
        NativeLibrary.TryLoad(NativeMethods.Library, typeof(RdpSession).Assembly, null, out _);

    public void Start(RdpConnectOptions options)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            if (_thread is not null)
                throw new InvalidOperationException("Session already started.");
            _self = GCHandle.Alloc(this);
            _thread = new Thread(() => Run(options)) { IsBackground = true, Name = "rdp " + options.Host };
            _thread.Start();
        }
    }

    public void SendMouse(RdpMouseFlags flags, int x, int y) =>
        _requests.Enqueue(ctx => NativeMethods.Mouse(ctx, (ushort)flags, (ushort)Math.Max(0, x), (ushort)Math.Max(0, y)));

    public void SendWheel(int delta, bool horizontal, int x, int y)
    {
        var flags = WheelFlags(delta, horizontal);
        _requests.Enqueue(ctx => NativeMethods.Mouse(ctx, (ushort)flags, (ushort)Math.Max(0, x), (ushort)Math.Max(0, y)));
    }

    // RDP tekerlek değeri 9 bitlik ikiye tümleyen; en fazla 255.
    internal static RdpMouseFlags WheelFlags(int delta, bool horizontal)
    {
        var value = Math.Clamp(delta, -255, 255);
        return (horizontal ? RdpMouseFlags.HWheel : RdpMouseFlags.Wheel) | (RdpMouseFlags)(value & 0x1FF);
    }

    public void SetLocalClipboardText(string text) => _requests.Enqueue(ctx => NativeMethods.SetClipboardText(ctx, text));

    public void Resize(int width, int height) => _requests.Enqueue(ctx => NativeMethods.Resize(ctx, width, height));

    public void SendKey(ushort code, bool extended, bool down) =>
        _requests.Enqueue(ctx => NativeMethods.Key(ctx, code, extended ? 1 : 0, down ? 1 : 0));

    public void SendUnicode(char c) => _requests.Enqueue(ctx =>
    {
        NativeMethods.Unicode(ctx, c, 1);
        NativeMethods.Unicode(ctx, c, 0);
    });

    public void Dispose()
    {
        Thread? thread;
        lock (_gate)
        {
            if (_stopping)
                return;
            _stopping = true;
            if (_ctx != IntPtr.Zero)
                NativeMethods.Abort(_ctx);
            thread = _thread;
        }
        if (thread is not null && thread != Thread.CurrentThread)
            thread.Join(DisposeWait);
    }

    private void Run(RdpConnectOptions options)
    {
        var callbacks = new RnCallbacks { Frame = &OnFrame, Pointer = &OnPointer, ClipboardText = &OnClipboardText };
        var ctx = NativeMethods.Create(&callbacks, GCHandle.ToIntPtr(_self));
        lock (_gate)
        {
            if (ctx == IntPtr.Zero || _stopping)
            {
                Release(ctx);
                if (ctx == IntPtr.Zero)
                    Ended?.Invoke(RdpEndReason.ConnectFailed, null);
                return;
            }
            _ctx = ctx;
        }

        var error = Connect(ctx, options);
        if (error != 0)
        {
            var reason = RdpErrors.Classify(error, NativeMethods.EndReason(ctx), wasConnected: false);
            Finish(ctx, reason);
            return;
        }

        Connected?.Invoke();
        while (!_stopping)
        {
            while (_requests.TryDequeue(out var request))
                request(ctx);
            if (NativeMethods.Pump(ctx, PumpTimeoutMs) == 0)
                break;
        }
        Finish(ctx, RdpErrors.Classify(NativeMethods.LastError(ctx), NativeMethods.EndReason(ctx), wasConnected: true));
    }

    private static uint Connect(IntPtr ctx, RdpConnectOptions o)
    {
        var host = Marshal.StringToCoTaskMemUTF8(o.Host);
        var user = Marshal.StringToCoTaskMemUTF8(o.Username);
        var domain = o.Domain is null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(o.Domain);
        var password = Marshal.StringToCoTaskMemUTF8(o.Password);
        try
        {
            var native = new RnOptions
            {
                Host = host, Port = o.Port, Username = user, Domain = domain, Password = password,
                Width = o.Width, Height = o.Height,
            };
            return NativeMethods.Connect(ctx, &native);
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUTF8(password);
            Marshal.FreeCoTaskMem(host);
            Marshal.FreeCoTaskMem(user);
            Marshal.FreeCoTaskMem(domain);
        }
    }

    // Kapatılırken (Dispose) sebep bildirilmez; sekme zaten gidiyor.
    private void Finish(IntPtr ctx, RdpEndReason reason)
    {
        bool stopping;
        lock (_gate)
        {
            stopping = _stopping;
            _ctx = IntPtr.Zero;
            Release(ctx);
        }
        if (!stopping)
            Ended?.Invoke(reason, null);
    }

    private void Release(IntPtr ctx)
    {
        if (ctx != IntPtr.Zero)
            NativeMethods.Free(ctx);
        if (_self.IsAllocated)
            _self.Free();
    }

    private static RdpSession? From(IntPtr user) => GCHandle.FromIntPtr(user).Target as RdpSession;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnFrame(IntPtr user, IntPtr buffer, int width, int height, int stride, int x, int y, int w, int h) =>
        From(user)?.Frame?.Invoke(new RdpFrame(buffer, width, height, stride, x, y, w, h));

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnClipboardText(IntPtr user, IntPtr text)
    {
        if (Marshal.PtrToStringUTF8(text) is { } value)
            From(user)?.RemoteClipboardText?.Invoke(value);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnPointer(IntPtr user, IntPtr bgra, int width, int height, int hotX, int hotY)
    {
        RdpPointer? pointer = bgra == IntPtr.Zero
            ? width < 0 ? null : RdpPointer.Hidden
            : new RdpPointer(new Span<byte>((void*)bgra, width * height * 4).ToArray(), width, height, hotX, hotY);
        From(user)?.PointerChanged?.Invoke(pointer);
    }
}
