using RdpNext.Core.Secrets;

namespace RdpNext.App.Services;

// Anahtar zinciri erişimi reddedilir ya da kilitliyse uygulama çökmez; kullanıcıya tek satır gösterilir.
public sealed class GuardedSecretStore(ISecretStore inner, Action<string> onError) : ISecretStore
{
    public string? GetPassword(Guid nodeId)
    {
        try
        {
            return inner.GetPassword(nodeId);
        }
        catch (InvalidOperationException)
        {
            onError(Strings.Strings.KeychainError);
            return null;
        }
    }

    public void SetPassword(Guid nodeId, string password) => Guard(() => inner.SetPassword(nodeId, password));

    public void DeletePassword(Guid nodeId) => Guard(() => inner.DeletePassword(nodeId));

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            onError(Strings.Strings.KeychainError);
        }
    }
}
