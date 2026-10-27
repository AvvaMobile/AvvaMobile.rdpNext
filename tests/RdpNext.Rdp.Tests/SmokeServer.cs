using RdpNext.Core.Secrets;

namespace RdpNext.Rdp.Tests;

// Gerçek sunucuya bağlanan duman testleri yalnız RDPNEXT_SMOKE ayarlıyken koşar:
// RDPNEXT_SMOKE="host:port:kullanıcı:anahtar zincirindeki düğüm Id'si". Şifre uygulamanın anahtar zincirinden okunur.
internal static class SmokeServer
{
    public static RdpConnectOptions? Options(int width = 1280, int height = 800)
    {
        var value = Environment.GetEnvironmentVariable("RDPNEXT_SMOKE");
        if (string.IsNullOrEmpty(value) || !RdpSession.IsAvailable || !OperatingSystem.IsMacOS())
            return null;
        var parts = value.Split(':');
        var password = new MacKeychainSecretStore().GetPassword(Guid.Parse(parts[3]))
                       ?? throw new InvalidOperationException("Password not in keychain.");
        return new RdpConnectOptions(parts[0], int.Parse(parts[1]), parts[2], null, password, width, height);
    }
}
