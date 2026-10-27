using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace RdpNext.Core.Secrets;

// macOS giriş anahtar zinciri: servis adı + hesap olarak düğüm Id'si.
[SupportedOSPlatform("macos")]
public sealed class MacKeychainSecretStore(string service = "rdpNext") : ISecretStore
{
    private const string Security = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int ItemNotFound = -25300;

    private readonly byte[] _service = Encoding.UTF8.GetBytes(service);

    public string? GetPassword(Guid nodeId)
    {
        var account = Account(nodeId);
        var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)_service.Length, _service,
            (uint)account.Length, account, out var length, out var data, out var item);
        if (status == ItemNotFound)
            return null;
        Check(status);
        try
        {
            var bytes = new byte[length];
            Marshal.Copy(data, bytes, 0, (int)length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            SecKeychainItemFreeContent(IntPtr.Zero, data);
            CFRelease(item);
        }
    }

    public void SetPassword(Guid nodeId, string password)
    {
        var account = Account(nodeId);
        var bytes = Encoding.UTF8.GetBytes(password);
        var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)_service.Length, _service,
            (uint)account.Length, account, out _, out var data, out var item);
        if (status == ItemNotFound)
        {
            Check(SecKeychainAddGenericPassword(IntPtr.Zero, (uint)_service.Length, _service,
                (uint)account.Length, account, (uint)bytes.Length, bytes, IntPtr.Zero));
            return;
        }
        Check(status);
        try
        {
            Check(SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)bytes.Length, bytes));
        }
        finally
        {
            SecKeychainItemFreeContent(IntPtr.Zero, data);
            CFRelease(item);
        }
    }

    public void DeletePassword(Guid nodeId)
    {
        var account = Account(nodeId);
        var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)_service.Length, _service,
            (uint)account.Length, account, out _, out var data, out var item);
        if (status == ItemNotFound)
            return;
        Check(status);
        try
        {
            Check(SecKeychainItemDelete(item));
        }
        finally
        {
            SecKeychainItemFreeContent(IntPtr.Zero, data);
            CFRelease(item);
        }
    }

    private static byte[] Account(Guid nodeId) => Encoding.UTF8.GetBytes(nodeId.ToString());

    private static void Check(int status)
    {
        if (status != 0)
            throw new InvalidOperationException($"Anahtar zinciri hatası: {status}");
    }

    [DllImport(Security)]
    private static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceNameLength, byte[] serviceName,
        uint accountNameLength, byte[] accountName, uint passwordLength, byte[] passwordData, IntPtr itemRef);

    [DllImport(Security)]
    private static extern int SecKeychainFindGenericPassword(IntPtr keychainOrArray, uint serviceNameLength, byte[] serviceName,
        uint accountNameLength, byte[] accountName, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);

    [DllImport(Security)]
    private static extern int SecKeychainItemModifyAttributesAndData(IntPtr itemRef, IntPtr attrList, uint length, byte[] data);

    [DllImport(Security)]
    private static extern int SecKeychainItemDelete(IntPtr itemRef);

    [DllImport(Security)]
    private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr cf);
}
