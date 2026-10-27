using System.Runtime.InteropServices;

namespace RdpNext.Rdp;

// native/rdpnext_shim/rdpnext_shim.h ile birebir; alan sırası değişmez.
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct RnCallbacks
{
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, int, int, int, int, int, void> Frame;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int, int> VerifyCertificate;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, int, int, void> Pointer;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> ClipboardText;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RnOptions
{
    public IntPtr Host;
    public int Port;
    public IntPtr Username;
    public IntPtr Domain;
    public IntPtr Password;
    public int Width;
    public int Height;
}

internal static unsafe partial class NativeMethods
{
    public const string Library = "rdpnext_shim";

    [LibraryImport(Library, EntryPoint = "rn_create")]
    public static partial IntPtr Create(RnCallbacks* callbacks, IntPtr user);

    [LibraryImport(Library, EntryPoint = "rn_connect")]
    public static partial uint Connect(IntPtr ctx, RnOptions* options);

    [LibraryImport(Library, EntryPoint = "rn_pump")]
    public static partial int Pump(IntPtr ctx, int timeoutMs);

    [LibraryImport(Library, EntryPoint = "rn_end_reason")]
    public static partial uint EndReason(IntPtr ctx);

    [LibraryImport(Library, EntryPoint = "rn_last_error")]
    public static partial uint LastError(IntPtr ctx);

    [LibraryImport(Library, EntryPoint = "rn_abort")]
    public static partial void Abort(IntPtr ctx);

    [LibraryImport(Library, EntryPoint = "rn_mouse")]
    public static partial void Mouse(IntPtr ctx, ushort flags, ushort x, ushort y);

    [LibraryImport(Library, EntryPoint = "rn_key")]
    public static partial void Key(IntPtr ctx, ushort code, int extended, int down);

    [LibraryImport(Library, EntryPoint = "rn_unicode")]
    public static partial void Unicode(IntPtr ctx, ushort code, int down);

    [LibraryImport(Library, EntryPoint = "rn_resize")]
    public static partial int Resize(IntPtr ctx, int width, int height);

    [LibraryImport(Library, EntryPoint = "rn_free")]
    public static partial void Free(IntPtr ctx);
}
