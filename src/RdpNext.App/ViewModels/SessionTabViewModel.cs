using CommunityToolkit.Mvvm.ComponentModel;
using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

// Plan 2'de RDP oturumu bu sekmenin içeriği olacak.
public sealed partial class SessionTabViewModel(ServerNodeViewModel server) : ObservableObject
{
    public ServerNodeViewModel Server { get; } = server;

    public string Title => Server.Name;

    public ServerColor Color => Server.Color;
}
