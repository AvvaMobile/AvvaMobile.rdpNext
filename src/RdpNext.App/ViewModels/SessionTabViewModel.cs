using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

// Plan 2'de RDP oturumu bu sekmenin içeriği olacak.
public sealed class SessionTabViewModel(ServerNodeViewModel server) : TabViewModel
{
    public ServerNodeViewModel Server { get; } = server;

    public override string Title => Server.Name;

    public override ServerColor? Color => Server.Color;

    public override void OnClosed() => Server.IsConnected = false;
}
