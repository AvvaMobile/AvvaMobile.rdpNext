namespace RdpNext.Core.Secrets;

public static class SecretStores
{
    public static ISecretStore ForCurrentPlatform()
    {
        if (OperatingSystem.IsMacOS())
            return new MacKeychainSecretStore();
        if (OperatingSystem.IsWindows())
            return new WindowsCredentialSecretStore();
        return new InMemorySecretStore();
    }
}
