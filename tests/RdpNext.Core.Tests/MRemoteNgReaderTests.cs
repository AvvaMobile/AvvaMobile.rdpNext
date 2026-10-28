using RdpNext.Core.Import;

namespace RdpNext.Core.Tests;

// Test dosyaları Fixtures/mRemoteNG/make.mjs ile Node.js'te, bu koddan bağımsız şifrelendi.
public class MRemoteNgReaderTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "mRemoteNG", name);

    private static Group Folder(MRemoteNgImport import) => Assert.IsType<Group>(Assert.Single(import.Root.Children));

    private static Node Child(Group group, string name) => group.Children.Single(c => c.Name == name);

    [Fact]
    public void FoldersAndServersArriveUnderMRemoteNgFolder()
    {
        var import = MRemoteNgReader.Read(Fixture("confCons.xml"), null);

        var folder = Folder(import);
        Assert.Equal(("mRemoteNG", MRemoteNgReader.FolderId), (folder.Name, folder.Id));
        Assert.Equal(["Üretim", "Test"], folder.Children.Select(c => c.Name));
        var uretim = Assert.IsType<Group>(Child(folder, "Üretim"));
        Assert.Equal(new Guid("6f1c2a10-0000-4000-8000-000000000001"), uretim.Id);
        Assert.Equal(["web-01", "web-02", "linux-01"], uretim.Children.Select(c => c.Name));
        Assert.True(uretim.IsExpanded);
        Assert.False(Assert.IsType<Group>(Child(folder, "Test")).IsExpanded);
    }

    [Fact]
    public void OnlyRdpAndSshAreImportedOthersCounted()
    {
        var import = MRemoteNgReader.Read(Fixture("confCons.xml"), null);

        Assert.Equal(2, import.Skipped);
        Assert.Equal(4, import.Root.AllServers().Count());
    }

    [Fact]
    public void ServerFieldsAndProtocols()
    {
        var import = MRemoteNgReader.Read(Fixture("confCons.xml"), null);
        var uretim = (Group)Child(Folder(import), "Üretim");

        var web = Assert.IsType<Server>(Child(uretim, "web-01"));
        Assert.Equal(("10.0.1.11", 3390, Protocol.Rdp), (web.Host, web.Port, web.Protocol));
        Assert.Equal(new Guid("6f1c2a10-0000-4000-8000-000000000011"), web.Id);
        var linux = Assert.IsType<Server>(Child(uretim, "linux-01"));
        Assert.Equal(("10.0.2.5", 22, Protocol.Ssh, "root", (string?)null), (linux.Host, linux.Port, linux.Protocol, linux.Username, linux.Domain));
    }

    // mRemoteNG'de "devral" işaretli alan bizde boş kalır, böylece klasörden devralınır.
    [Fact]
    public void InheritanceAndPasswordsAreConverted()
    {
        var import = MRemoteNgReader.Read(Fixture("confCons.xml"), null);
        var uretim = (Group)Child(Folder(import), "Üretim");
        var web01 = (Server)Child(uretim, "web-01");
        var web02 = (Server)Child(uretim, "web-02");

        Assert.Equal(("admin", "AVVA"), (uretim.Username, uretim.Domain));
        Assert.Equal("klasor-sifre", import.Passwords[uretim.Id]);
        Assert.Equal(((string?)null, (string?)null), (web01.Username, web01.Domain));
        Assert.False(import.Passwords.ContainsKey(web01.Id));
        Assert.Equal(("ali", (string?)null), (web02.Username, web02.Domain));
        Assert.Equal("ali-ğüş", import.Passwords[web02.Id]);
    }

    [Fact]
    public void InheritedProtocolAndPortComeFromFolder()
    {
        var import = MRemoteNgReader.Read(Fixture("confCons.xml"), null);
        var test = (Group)Child(Folder(import), "Test");

        var server = Assert.IsType<Server>(Assert.Single(test.Children));
        Assert.Equal(("test.local", "test.local", Protocol.Ssh, 2222), (server.Name, server.Host, server.Protocol, server.Port));
        Assert.Equal(server.Id, MRemoteNgReader.Read(Fixture("confCons.xml"), null).Root.AllServers().Single(s => s.Host == "test.local").Id);
    }

    [Fact]
    public void MasterPasswordFileNeedsPassword()
    {
        var error = Assert.Throws<MRemoteNgException>(() => MRemoteNgReader.Read(Fixture("confCons-master.xml"), null));
        Assert.Equal(MRemoteNgError.PasswordNeeded, error.Error);

        error = Assert.Throws<MRemoteNgException>(() => MRemoteNgReader.Read(Fixture("confCons-master.xml"), "yanlis"));
        Assert.Equal(MRemoteNgError.WrongPassword, error.Error);

        var import = MRemoteNgReader.Read(Fixture("confCons-master.xml"), "ana-parola");
        Assert.Contains("ali-ğüş", import.Passwords.Values);
    }

    [Fact]
    public void MasterPasswordIgnoredForUnprotectedFile()
    {
        var import = MRemoteNgReader.Read(Fixture("confCons.xml"), "gereksiz");

        Assert.Contains("klasor-sifre", import.Passwords.Values);
    }

    [Theory]
    [InlineData("confCons-fullfile.xml")]
    [InlineData("confCons-legacy.xml")]
    public void OtherEncryptionFormatsRead(string name)
    {
        var import = MRemoteNgReader.Read(Fixture(name), null);

        Assert.Equal(4, import.Root.AllServers().Count());
        Assert.Contains("ali-ğüş", import.Passwords.Values);
    }

    [Fact]
    public void NonMRemoteNgFileIsRejected()
    {
        var error = Assert.Throws<MRemoteNgException>(() => MRemoteNgReader.Read(Fixture("not-mremoteng.xml"), null));
        Assert.Equal(MRemoteNgError.NotMRemoteNg, error.Error);
    }
}
