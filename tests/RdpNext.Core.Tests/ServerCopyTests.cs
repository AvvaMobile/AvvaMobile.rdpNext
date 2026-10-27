namespace RdpNext.Core.Tests;

public class ServerCopyTests
{
    [Fact]
    public void CopyKeepsFieldsWithNewId()
    {
        var group = new Group { Name = "Üretim" };
        var server = new Server
        {
            Name = "web-01", Host = "10.0.1.11", Port = 3390, Username = "ali", Domain = "AVVA",
            Color = ServerColor.Teal, Screen = ScreenMode.FullScreen, Icon = ServerIcon.Linux,
        };
        group.Add(server);

        var copy = server.CopyWithNewId("web-01 kopya");

        Assert.NotEqual(server.Id, copy.Id);
        Assert.Null(copy.Parent);
        Assert.Equal(
            ("web-01 kopya", "10.0.1.11", 3390, "ali", "AVVA", ServerColor.Teal, ScreenMode.FullScreen, ServerIcon.Linux, Protocol.Rdp),
            (copy.Name, copy.Host, copy.Port, copy.Username, copy.Domain, copy.Color, copy.Screen, copy.Icon, copy.Protocol));
    }
}
