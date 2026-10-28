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
            Color = ServerColor.Teal, Screen = ScreenMode.FullScreen,
        };
        group.Add(server);

        var copy = server.CopyWithNewId("web-01 kopya");

        Assert.NotEqual(server.Id, copy.Id);
        Assert.Null(copy.Parent);
        Assert.Equal(
            ("web-01 kopya", "10.0.1.11", 3390, "ali", "AVVA", ServerColor.Teal, ScreenMode.FullScreen, Protocol.Rdp),
            (copy.Name, copy.Host, copy.Port, copy.Username, copy.Domain, copy.Color, copy.Screen, copy.Protocol));
    }

    // Kopya başka bir makineye işaret edebilir; güvenilen sertifika kopyalanmaz.
    [Fact]
    public void CopyDoesNotCopyFingerprint() =>
        Assert.Null(new Server { CertificateFingerprint = "sha256 AA" }.CopyWithNewId("x").CertificateFingerprint);

    [Fact]
    public void CopyKeepsSshFieldsButNotHostKey()
    {
        var server = new Server
        {
            Protocol = Protocol.Ssh, Port = 22, Auth = SshAuth.Key, KeyFile = "~/.ssh/id_ed25519", HostKeyFingerprint = "SHA256:abc",
            HostKeyType = "ssh-ed25519",
        };

        var copy = server.CopyWithNewId("kopya");

        Assert.Equal((Protocol.Ssh, 22, SshAuth.Key, "~/.ssh/id_ed25519"), (copy.Protocol, copy.Port, copy.Auth, copy.KeyFile));
        Assert.Null(copy.HostKeyFingerprint);
        Assert.Null(copy.HostKeyType);
    }

    [Fact]
    public void IconFollowsProtocol()
    {
        Assert.Equal(ServerIcon.Windows, new Server().Icon);
        Assert.Equal(ServerIcon.Ssh, new Server { Protocol = Protocol.Ssh }.Icon);
        Assert.Equal(ServerIcon.Linux, new Server { Os = ServerOs.Linux }.Icon);
        Assert.Equal(ServerIcon.Ssh, new Server { Protocol = Protocol.Ssh, Os = ServerOs.Linux }.Icon);
    }

    [Fact]
    public void CopyKeepsLinux()
    {
        Assert.Equal(ServerOs.Linux, new Server { Os = ServerOs.Linux }.CopyWithNewId("kopya").Os);
    }

    [Fact]
    public void CopyKeepsInfoOverlay()
    {
        Assert.True(new Server { ShowInfoOverlay = true }.CopyWithNewId("kopya").ShowInfoOverlay);
    }
}
