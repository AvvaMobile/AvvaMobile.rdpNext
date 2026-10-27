namespace RdpNext.Core.Model;

public sealed class Server : Node
{
    public Protocol Protocol { get; set; } = Protocol.Rdp;

    public string Host { get; set; } = "";

    public int Port { get; set; } = 3389;

    public string? Username { get; set; }

    public string? Domain { get; set; }

    public ServerColor Color { get; set; } = ServerColor.Blue;

    public ScreenMode Screen { get; set; } = ScreenMode.FitToWindow;
}
