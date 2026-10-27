using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace RdpNext.App.Platform;

// macOS'ta pencereye boş, "unified" bir araç çubuğu ekler; sistem kırmızı/sarı/yeşil düğmeleri
// 52 px'lik başlık şeridinin dikey ortasına alır. Avalonia 12'de bunun için hazır ayar yok.
[SupportedOSPlatform("macos")]
public static class MacTitleBar
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const long ToolbarStyleUnified = 3;
    private const long TitleHidden = 1;

    public static void CenterTrafficLights(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero)
            return;
        var nsWindow = handle.HandleDescriptor == "NSWindow" ? handle.Handle : Send(handle.Handle, Sel("window"));
        if (nsWindow == IntPtr.Zero)
            return;

        var toolbar = Send(Send(objc_getClass("NSToolbar"), Sel("alloc")), Sel("init"));
        Send(nsWindow, Sel("setToolbar:"), toolbar);
        Send(toolbar, Sel("release"));
        Send(nsWindow, Sel("setToolbarStyle:"), ToolbarStyleUnified);
        Send(nsWindow, Sel("setTitleVisibility:"), TitleHidden);
    }

    private static IntPtr Sel(string name) => sel_registerName(name);

    [DllImport(ObjC)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void Send(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void Send(IntPtr receiver, IntPtr selector, long argument);
}
