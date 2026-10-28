using RdpNext.Core.Credentials;
using RdpNext.Core.Secrets;

namespace RdpNext.Core.Tests;

public class CredentialResolverTests
{
    private readonly InMemorySecretStore _secrets = new();

    [Fact]
    public void ServerOwnValuesWin()
    {
        var group = new Group { Name = "Üretim", Username = "admin", Domain = "GRUP" };
        _secrets.SetPassword(group.Id, "grup-sifre");
        var server = new Server { Name = "web-01", Username = "ali", Domain = "AVVA" };
        _secrets.SetPassword(server.Id, "pw1");
        group.Add(server);

        var result = CredentialResolver.Resolve(server, _secrets);

        Assert.Equal(new ResolvedCredentials("ali", "pw1", "AVVA"), result);
    }

    [Fact]
    public void EmptyFieldsInheritFromNearestGroup()
    {
        var root = new Group { Name = "kök", Username = "kok-kullanici", Domain = "KOK" };
        var uretim = new Group { Name = "Üretim", Username = "admin", Domain = "AVVA" };
        _secrets.SetPassword(uretim.Id, "g");
        var web = new Group { Name = "Web" };
        var server = new Server { Name = "web-01" };
        root.Add(uretim);
        uretim.Add(web);
        web.Add(server);

        var result = CredentialResolver.Resolve(server, _secrets);

        Assert.Equal(new ResolvedCredentials("admin", "g", "AVVA"), result);
    }

    [Fact]
    public void FieldsInheritIndependently()
    {
        var group = new Group { Name = "Üretim", Username = "admin", Domain = "AVVA" };
        _secrets.SetPassword(group.Id, "g");
        var server = new Server { Name = "web-01", Username = "ali" };
        group.Add(server);

        var result = CredentialResolver.Resolve(server, _secrets);

        Assert.Equal(new ResolvedCredentials("ali", "g", "AVVA"), result);
    }

    [Fact]
    public void WhitespaceCountsAsEmpty()
    {
        var group = new Group { Name = "Üretim", Username = "admin" };
        var server = new Server { Name = "web-01", Username = "  " };
        group.Add(server);

        var result = CredentialResolver.Resolve(server, _secrets);

        Assert.Equal("admin", result.Username);
    }

    [Fact]
    public void NothingAnywhereGivesNulls()
    {
        var group = new Group { Name = "Üretim" };
        var server = new Server { Name = "web-01" };
        group.Add(server);

        var result = CredentialResolver.Resolve(server, _secrets);

        Assert.Equal(new ResolvedCredentials(null, null, null), result);
    }

    [Fact]
    public void AddMovesChildBetweenGroups()
    {
        var a = new Group { Name = "A" };
        var b = new Group { Name = "B" };
        var server = new Server { Name = "web-01" };

        a.Add(server);
        b.Add(server);

        Assert.Same(b, server.Parent);
        Assert.Empty(a.Children);
        Assert.Single(b.Children);
    }

    [Fact]
    public void InheritedReportsSourceGroup()
    {
        var uretim = new Group { Name = "Üretim", Username = "admin" };
        _secrets.SetPassword(uretim.Id, "g");
        var web = new Group { Name = "Web", Domain = "AVVA" };
        uretim.Add(web);

        var result = CredentialResolver.Inherited(web, _secrets);

        Assert.Equal(new InheritedValue("admin", "Üretim"), result.Username);
        Assert.Equal(new InheritedValue("g", "Üretim"), result.Password);
        Assert.Equal(new InheritedValue("AVVA", "Web"), result.Domain);
    }

    [Fact]
    public void InheritedNullStartGivesNulls() =>
        Assert.Equal(new InheritedCredentials(null, null, null), CredentialResolver.Inherited(null, _secrets));

    [Fact]
    public void SshKeyAuthDoesNotInheritPassword()
    {
        var group = new Group { Name = "Üretim", Username = "root" };
        _secrets.SetPassword(group.Id, "klasor");
        var server = new Server { Protocol = Protocol.Ssh, Auth = SshAuth.Key, KeyFile = "/k/id" };
        group.Add(server);

        Assert.Equal(new ResolvedCredentials("root", null, null), CredentialResolver.Resolve(server, _secrets));
        _secrets.SetPassword(server.Id, "anahtar123");
        Assert.Equal(new ResolvedCredentials("root", "anahtar123", null), CredentialResolver.Resolve(server, _secrets));
    }

    [Fact]
    public void SshIgnoresDomain()
    {
        var group = new Group { Name = "Üretim", Username = "root", Domain = "AVVA" };
        _secrets.SetPassword(group.Id, "klasor");
        var server = new Server { Protocol = Protocol.Ssh, Domain = "ESKI" };
        group.Add(server);

        Assert.Equal(new ResolvedCredentials("root", "klasor", null), CredentialResolver.Resolve(server, _secrets));
    }
}
