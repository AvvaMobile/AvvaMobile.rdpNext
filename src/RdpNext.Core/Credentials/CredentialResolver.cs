using RdpNext.Core.Model;
using RdpNext.Core.Secrets;

namespace RdpNext.Core.Credentials;

public sealed record ResolvedCredentials(string? Username, string? Password, string? Domain);

public static class CredentialResolver
{
    // Her alan ayrı ayrı aranır: önce sunucu, sonra en yakın üst gruptan köke doğru.
    public static ResolvedCredentials Resolve(Server server, ISecretStore secrets) => new(
        FirstValue(server, n => n switch { Server s => s.Username, Group g => g.Username, _ => null }),
        FirstValue(server, n => secrets.GetPassword(n.Id)),
        FirstValue(server, n => n switch { Server s => s.Domain, Group g => g.Domain, _ => null }));

    private static string? FirstValue(Server server, Func<Node, string?> read)
    {
        for (Node? node = server; node is not null; node = node.Parent)
        {
            var value = read(node);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }
}
