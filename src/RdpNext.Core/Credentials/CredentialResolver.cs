using RdpNext.Core.Model;
using RdpNext.Core.Secrets;

namespace RdpNext.Core.Credentials;

public sealed record ResolvedCredentials(string? Username, string? Password, string? Domain);

public sealed record InheritedValue(string Value, string FromGroup);

public sealed record InheritedCredentials(InheritedValue? Username, InheritedValue? Password, InheritedValue? Domain);

public static class CredentialResolver
{
    // Her alan ayrı ayrı aranır: önce sunucu, sonra en yakın üst gruptan köke doğru.
    public static ResolvedCredentials Resolve(Server server, ISecretStore secrets) => new(
        FirstValue(server, n => n switch { Server s => s.Username, Group g => g.Username, _ => null }),
        FirstValue(server, n => secrets.GetPassword(n.Id)),
        FirstValue(server, n => n switch { Server s => s.Domain, Group g => g.Domain, _ => null }));

    // Bir klasörden köke doğru, her alan için ilk dolu değer ve geldiği klasör.
    public static InheritedCredentials Inherited(Group? start, ISecretStore secrets) => new(
        FirstFromGroups(start, g => g.Username),
        FirstFromGroups(start, g => secrets.GetPassword(g.Id)),
        FirstFromGroups(start, g => g.Domain));

    private static InheritedValue? FirstFromGroups(Group? start, Func<Group, string?> read)
    {
        for (var group = start; group is not null; group = group.Parent)
        {
            var value = read(group);
            if (!string.IsNullOrWhiteSpace(value))
                return new InheritedValue(value, group.Name);
        }
        return null;
    }

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
