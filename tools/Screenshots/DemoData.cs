using RdpNext.Core.Model;
using RdpNext.Core.Secrets;

namespace RdpNext.Screenshots;

// Ekran görüntüleri için uydurma bağlantı listesi; adresler örnek ağlardan.
public static class DemoData
{
    public static Group Build(ISecretStore secrets)
    {
        var root = new Group();

        var production = Folder(root, "Production", ServerColor.Blue, "administrator", "CORP");
        Rdp(production, "web-01", "10.0.1.11", ServerColor.Blue);
        Rdp(production, "web-02", "10.0.1.12", ServerColor.Blue);
        Rdp(production, "sql-01", "10.0.1.20", ServerColor.Teal);
        Rdp(production, "api-gateway", "10.0.1.31", ServerColor.Orange);

        var staging = Folder(root, "Staging", ServerColor.Violet, "administrator", "CORP-STG");
        staging.IsExpanded = false;
        Rdp(staging, "web-staging", "10.0.2.11", ServerColor.Violet);
        Rdp(staging, "sql-staging", "10.0.2.20", ServerColor.Teal);

        var linux = Folder(root, "Linux", ServerColor.Green, "deploy", null);
        Ssh(linux, "ubuntu-build", "10.0.3.10", ServerColor.Green);
        Ssh(linux, "k8s-node-01", "10.0.3.21", ServerColor.Teal);
        Ssh(linux, "k8s-node-02", "10.0.3.22", ServerColor.Teal);
        var desktop = Rdp(linux, "dev-desktop", "10.0.3.40", ServerColor.Orange);
        desktop.Os = ServerOs.Linux;

        var customers = Folder(root, "Customers", ServerColor.Orange, "administrator", null);
        Rdp(customers, "acme-dc01", "rdp.acme.example.com", ServerColor.Violet);
        Rdp(customers, "globex-app", "globex.example.net", ServerColor.Black);

        var jump = Ssh(root, "jump-host", "bastion.example.com", ServerColor.Black);
        jump.Username = "ops";

        foreach (var node in root.Children)
            secrets.SetPassword(node.Id, "demo");
        foreach (var server in root.AllServers())
            secrets.SetPassword(server.Id, "demo");
        return root;
    }

    private static Group Folder(Group root, string name, ServerColor color, string? username, string? domain)
    {
        var group = new Group { Name = name, Color = color, Username = username, Domain = domain };
        root.Add(group);
        return group;
    }

    private static Server Rdp(Group parent, string name, string host, ServerColor color)
    {
        var server = new Server { Name = name, Host = host, Color = color };
        parent.Add(server);
        return server;
    }

    private static Server Ssh(Group parent, string name, string host, ServerColor color)
    {
        var server = new Server { Name = name, Host = host, Color = color, Protocol = Protocol.Ssh, Port = 22 };
        parent.Add(server);
        return server;
    }
}
