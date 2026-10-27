using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Platform;

namespace RdpNext.App.Platform;

// macOS paketlenmemiş uygulamada pencere ikonunu Dock'a taşımaz ("exec" görünür); Dock ikonu NSApp'e elle verilir.
[SupportedOSPlatform("macos")]
public static class MacDockIcon
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    public static void Set(Uri png)
    {
        using var stream = AssetLoader.Open(png);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();

        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var data = Send(objc_getClass("NSData"), Sel("dataWithBytes:length:"), pinned.AddrOfPinnedObject(), (ulong)bytes.Length);
            var image = Send(Send(objc_getClass("NSImage"), Sel("alloc")), Sel("initWithData:"), data);
            if (image == IntPtr.Zero)
                return;
            var app = Send(objc_getClass("NSApplication"), Sel("sharedApplication"));
            Send(app, Sel("setApplicationIconImage:"), image);
            Send(image, Sel("release"));
        }
        finally
        {
            pinned.Free();
        }
    }

    private static IntPtr Sel(string name) => sel_registerName(name);

    [DllImport(ObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr bytes, ulong length);
}
