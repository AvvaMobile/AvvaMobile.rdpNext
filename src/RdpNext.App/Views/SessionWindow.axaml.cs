using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

// Tam ekrandaki bir oturumun kendi penceresi. Kapatılınca yalnız bu oturum kapanır;
// tam ekran kısayolu oturumu ana penceredeki sekmesine geri götürür.
public partial class SessionWindow : Window
{
    public SessionWindow()
    {
        InitializeComponent();
    }

    public SessionWindow(MainWindowViewModel owner, ConnectionTabViewModel tab) : this()
    {
        DataContext = tab;
        Opened += (_, _) =>
        {
            WindowState = WindowState.FullScreen;
            (View as Control)?.Focus();
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!Rdp.FullScreenGesture.Matches(e))
                return;
            (View as SessionView)?.ReleaseKeys();
            owner.ToggleFullScreenCommand.Execute(tab);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }

    // İçerikteki oturum görünümü (SessionView ya da TerminalView).
    private Visual? View => SessionContent.GetVisualDescendants().FirstOrDefault(v => v is SessionView or TerminalView);
}
