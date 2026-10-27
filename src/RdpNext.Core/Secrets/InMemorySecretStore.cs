namespace RdpNext.Core.Secrets;

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<Guid, string> _passwords = [];

    public string? GetPassword(Guid nodeId) => _passwords.GetValueOrDefault(nodeId);

    public void SetPassword(Guid nodeId, string password) => _passwords[nodeId] = password;

    public void DeletePassword(Guid nodeId) => _passwords.Remove(nodeId);
}
