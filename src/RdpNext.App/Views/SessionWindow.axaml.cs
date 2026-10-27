using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    public SessionWindow(MainWindowViewModel owner, SessionTabViewModel tab) : this()
    {
        DataContext = tab;
        Opened += (_, _) =>
        {
            WindowState = WindowState.FullScreen;
            SessionViewRoot.Focus();
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!Rdp.FullScreenGesture.Matches(e))
                return;
            SessionViewRoot.ReleaseKeys();
            owner.ToggleFullScreenCommand.Execute(tab);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }
}
