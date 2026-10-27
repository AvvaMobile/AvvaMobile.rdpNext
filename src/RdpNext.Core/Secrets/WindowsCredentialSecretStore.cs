using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace RdpNext.Core.Secrets;

// Windows Kimlik Bilgisi Yöneticisi: hedef adı "rdpNext:<düğüm Id>".
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialSecretStore(string prefix = "rdpNext:") : ISecretStore
{
    private const int GenericType = 1;
    private const int PersistLocalMachine = 2;
    private const int NotFound = 1168;

    public string? GetPassword(Guid nodeId)
    {
        if (!CredReadW(Target(nodeId), GenericType, 0, out var handle))
        {
            if (Marshal.GetLastWin32Error() == NotFound)
                return null;
            throw new InvalidOperationException($"Kimlik bilgisi okunamadı: {Marshal.GetLastWin32Error()}");
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(handle);
            if (credential.CredentialBlobSize == 0)
                return "";
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(handle);
        }
    }

    public void SetPassword(Guid nodeId, string password)
    {
        var bytes = Encoding.Unicode.GetBytes(password);
        var blob = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1));
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                Type = GenericType,
                TargetName = Target(nodeId),
                CredentialBlobSize = bytes.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = "rdpNext",
            };
            if (!CredWriteW(ref credential, 0))
                throw new InvalidOperationException($"Kimlik bilgisi yazılamadı: {Marshal.GetLastWin32Error()}");
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
        }
    }

    public void DeletePassword(Guid nodeId)
    {
        if (!CredDeleteW(Target(nodeId), GenericType, 0) && Marshal.GetLastWin32Error() != NotFound)
            throw new InvalidOperationException($"Kimlik bilgisi silinemedi: {Marshal.GetLastWin32Error()}");
    }

    private string Target(Guid nodeId) => prefix + nodeId;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredReadW(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW(ref Credential credential, int flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
