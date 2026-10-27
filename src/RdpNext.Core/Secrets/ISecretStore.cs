namespace RdpNext.Core.Secrets;

public interface ISecretStore
{
    string? GetPassword(Guid nodeId);

    void SetPassword(Guid nodeId, string password);

    void DeletePassword(Guid nodeId);
}
