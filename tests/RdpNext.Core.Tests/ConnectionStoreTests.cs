using System.Text.Json.Nodes;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.Core.Tests;

public sealed class ConnectionStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rdpnext-tests-" + Guid.NewGuid());
    private readonly string _path;

    public ConnectionStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "connections.json");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void RoundTripKeepsEverything()
    {
        var root = new Group();
        var uretim = new Group { Name = "Üretim", Username = "admin", Domain = "AVVA", Color = ServerColor.Orange };
        var web = new Group { Name = "Web", IsExpanded = false };
        var server = new Server
        {
            Name = "web-01", Host = "10.0.1.11", Port = 3390, Color = ServerColor.Violet,
            Screen = ScreenMode.FullScreen, Username = "ali", Domain = "AVVA2",
            CertificateFingerprint = "sha256 AA:BB",
        };
        root.Add(uretim);
        uretim.Add(web);
        web.Add(server);

        new ConnectionStore(_path).Save(root);
        var loaded = new ConnectionStore(_path).Load().Root;

        var lUretim = Assert.IsType<Group>(Assert.Single(loaded.Children));
        Assert.Equal((uretim.Id, "Üretim", "admin", "AVVA", ServerColor.Orange), (lUretim.Id, lUretim.Name, lUretim.Username, lUretim.Domain, lUretim.Color));
        var lWeb = Assert.IsType<Group>(Assert.Single(lUretim.Children));
        Assert.False(lWeb.IsExpanded);
        var lServer = Assert.IsType<Server>(Assert.Single(lWeb.Children));
        Assert.Equal(
            (server.Id, "web-01", "10.0.1.11", 3390, ServerColor.Violet, ScreenMode.FullScreen, "ali", "AVVA2", Protocol.Rdp),
            (lServer.Id, lServer.Name, lServer.Host, lServer.Port, lServer.Color, lServer.Screen, lServer.Username, lServer.Domain, lServer.Protocol));
        Assert.Equal("sha256 AA:BB", lServer.CertificateFingerprint);
        Assert.Same(lWeb, lServer.Parent);
        Assert.Same(lUretim, lWeb.Parent);
        Assert.Same(loaded, lUretim.Parent);
    }

    [Fact]
    public void PasswordsNeverWritten()
    {
        var secrets = new InMemorySecretStore();
        var root = new Group();
        var server = new Server { Name = "web-01", Host = "10.0.1.11" };
        secrets.SetPassword(server.Id, "Gizli123!");
        root.Add(server);

        new ConnectionStore(_path).Save(root);

        Assert.DoesNotContain("Gizli123!", File.ReadAllText(_path));
    }

    [Fact]
    public void FileHasVersionAndKinds()
    {
        var root = new Group();
        var group = new Group { Name = "Üretim" };
        root.Add(group);
        group.Add(new Server { Name = "web-01" });

        new ConnectionStore(_path).Save(root);

        var json = JsonNode.Parse(File.ReadAllText(_path))!;
        Assert.Equal(1, json["version"]!.GetValue<int>());
        var jGroup = json["root"]!["children"]![0]!;
        Assert.Equal("group", jGroup["kind"]!.GetValue<string>());
        Assert.Equal("blue", jGroup["color"]!.GetValue<string>());
        var jServer = jGroup["children"]![0]!;
        Assert.Equal("server", jServer["kind"]!.GetValue<string>());
        Assert.Equal("rdp", jServer["protocol"]!.GetValue<string>());
        Assert.Equal("fitToWindow", jServer["screen"]!.GetValue<string>());
        Assert.Equal("blue", jServer["color"]!.GetValue<string>());
    }

    // İçe aktarma: okunamayan dosya kenara alınmaz, yerinde kalır.
    [Theory]
    [InlineData("{ yarım")]
    [InlineData("""{ "version": 1, "root": { "kind": "group", "children": [ { "kind": "ssh", "name": "x" } ] } }""")]
    public void ReadReturnsNullAndLeavesBadFile(string content)
    {
        File.WriteAllText(_path, content);

        Assert.Null(ConnectionStore.Read(_path));
        Assert.Equal(content, File.ReadAllText(_path));
    }

    [Fact]
    public void ReadGivesSavedTree()
    {
        var root = new Group();
        root.Add(new Group { Name = "Üretim" });
        new ConnectionStore(_path).Save(root);

        Assert.Equal("Üretim", Assert.Single(ConnectionStore.Read(_path)!.Children).Name);
        Assert.Null(ConnectionStore.Read(Path.Combine(_dir, "yok.json")));
    }

    [Fact]
    public void MissingFileGivesEmptyRoot()
    {
        var result = new ConnectionStore(_path).Load();

        Assert.Empty(result.Root.Children);
        Assert.Null(result.CorruptBackupPath);
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void CorruptFileIsBackedUpNotOverwritten()
    {
        File.WriteAllText(_path, "{ yarım");

        var result = new ConnectionStore(_path).Load();

        Assert.Empty(result.Root.Children);
        Assert.NotNull(result.CorruptBackupPath);
        Assert.Matches(@"connections\.corrupt-\d{8}-\d{6}\.json$", result.CorruptBackupPath);
        Assert.Equal("{ yarım", File.ReadAllText(result.CorruptBackupPath));
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void UnknownNodeKindIsBackedUpNotCrash()
    {
        File.WriteAllText(_path, """{ "version": 1, "root": { "kind": "group", "children": [ { "kind": "ssh", "name": "x" } ] } }""");

        var result = new ConnectionStore(_path).Load();

        Assert.Empty(result.Root.Children);
        Assert.NotNull(result.CorruptBackupPath);
    }

    [Theory]
    [InlineData("""{ "version": 1, "root": { "kind": "group", "children": [ { "name": "türsüz" } ] } }""")]
    [InlineData("""{ "version": 1, "root": { "kind": "group", "children": null } }""")]
    [InlineData("""{ "version": 1, "root": { "kind": "group", "children": [ null ] } }""")]
    public void MalformedFileIsBackedUpNotCrash(string content)
    {
        File.WriteAllText(_path, content);

        var result = new ConnectionStore(_path).Load();

        Assert.Empty(result.Root.Children);
        Assert.NotNull(result.CorruptBackupPath);
    }

    [Fact]
    public void NullNameAndHostLoadAsEmpty()
    {
        File.WriteAllText(_path, """{ "version": 1, "root": { "kind": "group", "children": [ { "kind": "server", "name": null, "host": null } ] } }""");

        var server = Assert.IsType<Server>(Assert.Single(new ConnectionStore(_path).Load().Root.Children));

        Assert.Equal(("", ""), (server.Name, server.Host));
    }

    // İkon alanından önce kaydedilmiş dosyalardaki sunucular Windows ikonuyla açılır.
    [Fact]
    public void MissingIconLoadsAsWindows()
    {
        File.WriteAllText(_path, """{ "version": 1, "root": { "kind": "group", "children": [ { "kind": "server", "name": "web-01", "host": "h" } ] } }""");

        var server = Assert.IsType<Server>(Assert.Single(new ConnectionStore(_path).Load().Root.Children));

        Assert.Equal(ServerIcon.Windows, server.Icon);
    }

    // İkon seçicisi varken Linux ya da SSH ikonu seçilmiş RDP sunucuları yeni sürümde RDP olarak açılır.
    [Theory]
    [InlineData("linux")]
    [InlineData("ssh")]
    public void OldFileWithLinuxIconLoadsAsRdp(string icon)
    {
        File.WriteAllText(_path, $$"""{ "version": 1, "root": { "kind": "group", "children": [ { "kind": "server", "name": "web-01", "host": "h", "icon": "{{icon}}" } ] } }""");

        var result = new ConnectionStore(_path).Load();

        Assert.Null(result.CorruptBackupPath);
        var server = Assert.IsType<Server>(Assert.Single(result.Root.Children));
        Assert.Equal((Protocol.Rdp, ServerIcon.Windows), (server.Protocol, server.Icon));
    }

    [Fact]
    public void LinuxServerRoundTripsAndOldFilesAreWindows()
    {
        var root = new Group();
        root.Add(new Server { Name = "linux-rdp", Host = "10.0.4.1", Os = ServerOs.Linux });
        root.Add(new Server { Name = "win", Host = "10.0.4.2" });

        new ConnectionStore(_path).Save(root);
        var loaded = new ConnectionStore(_path).Load().Root.Children.Cast<Server>().ToList();

        Assert.Equal([ServerOs.Linux, ServerOs.Windows], loaded.Select(s => s.Os));
        Assert.Equal((Protocol.Rdp, ServerIcon.Linux), (loaded[0].Protocol, loaded[0].Icon));
    }

    [Fact]
    public void SshServerRoundTrips()
    {
        var root = new Group();
        var server = new Server
        {
            Name = "linux-01", Host = "10.0.2.5", Port = 22, Protocol = Protocol.Ssh, Auth = SshAuth.Key,
            KeyFile = "~/.ssh/id_ed25519", HostKeyFingerprint = "SHA256:abc", HostKeyType = "ssh-ed25519",
        };
        root.Add(server);

        new ConnectionStore(_path).Save(root);
        var loaded = Assert.IsType<Server>(Assert.Single(new ConnectionStore(_path).Load().Root.Children));

        Assert.Equal((Protocol.Ssh, 22, SshAuth.Key, "~/.ssh/id_ed25519", "SHA256:abc", "ssh-ed25519"),
            (loaded.Protocol, loaded.Port, loaded.Auth, loaded.KeyFile, loaded.HostKeyFingerprint, loaded.HostKeyType));
        Assert.DoesNotContain("\"icon\"", File.ReadAllText(_path));
    }

    [Fact]
    public void SaveIsAtomic()
    {
        var root = new Group();
        root.Add(new Server { Name = "web-01" });

        new ConnectionStore(_path).Save(root);

        Assert.Equal(["connections.json"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public void SampleFileLoads()
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "connections.sample.json");

        var root = new ConnectionStore(sample).Load().Root;

        Assert.Equal(3, root.Children.Count);
        var uretim = Assert.IsType<Group>(root.Children[0]);
        Assert.Equal(3, uretim.Children.Count);
    }
}
