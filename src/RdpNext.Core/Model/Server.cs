using System.Text.Json.Serialization;

namespace RdpNext.Core.Model;

public sealed class Server : Node
{
    public Protocol Protocol { get; set; } = Protocol.Rdp;

    private string _host = "";

    public string Host
    {
        get => _host;
        set => _host = value ?? "";
    }

    public int Port { get; set; } = 3389;

    public string? Username { get; set; }

    public string? Domain { get; set; }

    public ServerColor Color { get; set; } = ServerColor.Blue;

    public ScreenMode Screen { get; set; } = ScreenMode.FitToWindow;

    // Oturumun üst ortasında sunucu adı ve adresi görünür.
    public bool ShowInfoOverlay { get; set; }

    // Linux sunucusu da RDP ile bağlanır (xrdp); eski dosyalarda alan yoksa Windows.
    public ServerOs Os { get; set; } = ServerOs.Windows;

    // Eski dosyalardaki "icon" alanı okunmaz; ikon protokolden ve işletim sisteminden gelir.
    [JsonIgnore]
    public ServerIcon Icon => Protocol == Protocol.Ssh ? ServerIcon.Ssh : Os == ServerOs.Linux ? ServerIcon.Linux : ServerIcon.Windows;

    // Kullanıcının "güven" dediği sertifikanın parmak izi; aynısı gelirse sorulmaz.
    public string? CertificateFingerprint { get; set; }

    public SshAuth Auth { get; set; } = SshAuth.Password;

    // Özel anahtar dosyasının yolu; dosyanın kendisi kopyalanmaz.
    public string? KeyFile { get; set; }

    // Kullanıcının "güven" dediği SSH sunucu anahtarı ("SHA256:..."); aynısı gelirse sorulmaz.
    public string? HostKeyFingerprint { get; set; }

    // Güvenilen anahtarın türü ("ssh-ed25519"); sonraki bağlantıda sunucudan aynı tür istenir.
    public string? HostKeyType { get; set; }

    public Server CopyWithNewId(string name) => new()
    {
        Name = name,
        Protocol = Protocol,
        Os = Os,
        Host = Host,
        Port = Port,
        Username = Username,
        Domain = Domain,
        Color = Color,
        Screen = Screen,
        ShowInfoOverlay = ShowInfoOverlay,
        Auth = Auth,
        KeyFile = KeyFile,
    };
}
