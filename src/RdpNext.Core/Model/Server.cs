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

    public ServerIcon Icon { get; set; } = ServerIcon.Windows;

    // Kullanıcının "güven" dediği sertifikanın parmak izi; aynısı gelirse sorulmaz.
    public string? CertificateFingerprint { get; set; }

    public Server CopyWithNewId(string name) => new()
    {
        Name = name,
        Protocol = Protocol,
        Host = Host,
        Port = Port,
        Username = Username,
        Domain = Domain,
        Color = Color,
        Screen = Screen,
        Icon = Icon,
    };
}
