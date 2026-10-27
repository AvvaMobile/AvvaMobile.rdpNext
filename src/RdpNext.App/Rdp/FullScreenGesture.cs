using Avalonia.Input;

namespace RdpNext.App.Rdp;

// Tam ekran kısayolu: macOS Ctrl+Cmd+F, Windows Ctrl+Alt+Enter.
public static class FullScreenGesture
{
    public static bool Matches(KeyEventArgs e) => OperatingSystem.IsMacOS()
        ? e.PhysicalKey == PhysicalKey.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Meta)
        : e.PhysicalKey is PhysicalKey.Enter or PhysicalKey.NumPadEnter
          && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Alt);
}
