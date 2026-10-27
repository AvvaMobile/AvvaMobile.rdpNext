using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

public sealed partial class ServerNodeViewModel(Server server) : TreeNodeViewModel(server)
{
    public Server Server { get; } = server;

    public string Host => Server.Host;

    public ServerColor Color => Server.Color;

    public ServerIcon Icon => Server.Icon;

    [ObservableProperty]
    private bool _isConnected;
}
