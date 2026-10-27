using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Credentials;
using RdpNext.Core.Model;
using RdpNext.Rdp;

namespace RdpNext.App.ViewModels;

public enum SessionState
{
    Connecting,
    Connected,
    Disconnected,
}

// Bir sunucunun RDP oturumu. Oturumdan gelen olaylar arayüz iş parçacığına aktarılır;
// eski bir oturumdan geç gelen olay yok sayılır.
public sealed partial class SessionTabViewModel : TabViewModel
{
    public SessionTabViewModel(ServerNodeViewModel server)
    {
        Server = server;
        server.State = SessionState.Connecting;
        // Sunucunun adı, rengi ya da ikonu değişince sekme de güncellenir.
        server.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ServerNodeViewModel.State) or nameof(ServerNodeViewModel.IsConnected))
                return;
            TitleChanged();
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(Icon));
        };
    }

    public ServerNodeViewModel Server { get; }

    public override string Title => Server.Name;

    public override ServerColor? Color => Server.Color;

    public override ServerIcon Icon => Server.Icon;

    public SessionScreen Screen { get; } = new();

    public IRdpSession? Session { get; private set; }

    [ObservableProperty]
    private SessionState _state = SessionState.Connecting;

    partial void OnStateChanged(SessionState value) => Server.State = value;

    // Kullanıcı ya da şifre bulunamazsa bağlanmadan kopmuş sayılır.
    public void Connect(IRdpSession session, ResolvedCredentials credentials, Size size)
    {
        Session?.Dispose();
        Session = session;
        State = SessionState.Connecting;
        if (credentials.Username is null || credentials.Password is null)
        {
            State = SessionState.Disconnected;
            return;
        }
        session.Connected += () => OnUi(session, () => State = SessionState.Connected);
        session.Ended += (_, _) => OnUi(session, () => State = SessionState.Disconnected);
        session.Frame += Screen.CopyFrame;
        session.Start(new RdpConnectOptions(Server.Host, Server.Server.Port, credentials.Username, credentials.Domain,
            credentials.Password, (int)size.Width, (int)size.Height));
    }

    private void OnUi(IRdpSession from, Action action) => Dispatcher.UIThread.Post(() =>
    {
        if (Session == from)
            action();
    });

    public override void OnClosed()
    {
        Session?.Dispose();
        Session = null;
        Server.State = null;
    }
}
