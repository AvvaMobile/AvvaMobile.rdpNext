using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

// SSH sekmesinin terminali. Terminalin içeriği sekmede (SshTabViewModel.Terminal) durur; görünüm yalnız gösterir.
// Kopyala/yapıştır: Mac'te Cmd+C / Cmd+V, diğerlerinde Ctrl+Shift+C / Ctrl+Shift+V. Ctrl+C sunucuya gider.
public partial class TerminalView : UserControl
{
    public TerminalView()
    {
        InitializeComponent();
        Terminal.FontFamily = new FontFamily(OperatingSystem.IsMacOS() ? "Menlo" : "Cascadia Mono, Consolas");
        AddHandler(KeyDownEvent, OnClipboardShortcut, RoutingStrategies.Tunnel);
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => Terminal.Focus(), DispatcherPriority.Background);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Terminal.Model = (DataContext as SshTabViewModel)?.Terminal;
    }

    private async void OnClipboardShortcut(object? sender, KeyEventArgs e)
    {
        var shortcut = OperatingSystem.IsMacOS()
            ? e.KeyModifiers == KeyModifiers.Meta
            : e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift);
        if (!shortcut || Terminal.Model is null)
            return;
        if (e.PhysicalKey == PhysicalKey.C)
        {
            e.Handled = true;
            if (Terminal.HasSelection)
                await Terminal.CopySelectionAsync();
        }
        else if (e.PhysicalKey == PhysicalKey.V)
        {
            e.Handled = true;
            await Terminal.PasteFromClipboardAsync();
        }
    }
}
