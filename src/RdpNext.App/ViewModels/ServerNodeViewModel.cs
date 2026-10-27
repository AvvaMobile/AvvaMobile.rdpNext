using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

public sealed partial class ServerNodeViewModel(Server server) : TreeNodeViewModel(server)
{
    public Server Server { get; } = server;

    public string Host => Server.Host;

    public ServerColor Color => Server.Color;

    public ServerIcon Icon => Server.Icon;

    // Açık sekmesi yoksa null.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(IsConnecting), nameof(IsDisconnected))]
    private SessionState? _state;

    public bool IsConnected => State == SessionState.Connected;

    public bool IsConnecting => State == SessionState.Connecting;

    public bool IsDisconnected => State == SessionState.Disconnected;
}
