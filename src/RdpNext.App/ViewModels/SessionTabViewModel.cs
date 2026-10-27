using RdpNext.Core.Model;

namespace RdpNext.App.ViewModels;

// Plan 2'de RDP oturumu bu sekmenin içeriği olacak.
public sealed class SessionTabViewModel : TabViewModel
{
    public SessionTabViewModel(ServerNodeViewModel server)
    {
        Server = server;
        // Sunucunun adı, rengi ya da ikonu değişince sekme de güncellenir.
        server.PropertyChanged += (_, _) =>
        {
            TitleChanged();
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(Icon));
        };
    }

    public ServerNodeViewModel Server { get; }

    public override string Title => Server.Name;

    public override ServerColor? Color => Server.Color;

    public override ServerIcon Icon => Server.Icon;

    public override void OnClosed() => Server.IsConnected = false;
}
