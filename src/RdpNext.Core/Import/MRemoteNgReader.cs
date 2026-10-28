using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using RdpNext.Core.Model;

namespace RdpNext.Core.Import;

public enum MRemoteNgError
{
    NotMRemoteNg,
    PasswordNeeded,
    WrongPassword,
}

public sealed class MRemoteNgException(MRemoteNgError error) : Exception(error.ToString())
{
    public MRemoteNgError Error { get; } = error;
}

// Root'un tek çocuğu "mRemoteNG" klasörüdür. Şifreler düğüm kimliğine göre; anahtar zincirine yazılmalı.
public sealed record MRemoteNgImport(Group Root, IReadOnlyDictionary<Guid, string> Passwords, int Skipped);

// mRemoteNG'nin confCons.xml dosyasını okur: klasörler, RDP ve SSH bağlantıları, şifreler.
// Diğer bağlantı türleri atlanıp sayılır.
public static class MRemoteNgReader
{
    // İçe aktarılanların üst klasörü; aynı dosya tekrar alınınca aynı klasöre eklenir.
    public static readonly Guid FolderId = new("8d0d9a52-6a3e-4c55-9a51-6d52656d6f74");

    // Ana parola konmamış dosyalarda mRemoteNG'nin kullandığı parola.
    private const string DefaultPassword = "mR3m";

    public static MRemoteNgImport Read(string path, string? masterPassword)
    {
        XElement root;
        try
        {
            root = XDocument.Load(path).Root!;
        }
        catch (XmlException)
        {
            throw new MRemoteNgException(MRemoteNgError.NotMRemoteNg);
        }
        if (root.Name.LocalName != "Connections")
            throw new MRemoteNgException(MRemoteNgError.NotMRemoteNg);

        var version = double.TryParse(((string?)root.Attribute("ConfVersion"))?.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var v) ? v : 0;
        var decrypt = Decryptor(root, version);
        var password = Password(root, decrypt, masterPassword);

        var nodes = root.Elements().Where(e => e.Name.LocalName == "Node");
        if (version >= 2.6 && IsTrue(root, "FullFileEncryption"))
            nodes = XElement.Parse("<r>" + Decrypt(decrypt, root.Value, password) + "</r>").Elements();

        var reader = new Reader(text => text.Length == 0 ? "" : Decrypt(decrypt, text, password));
        var folder = new Group { Id = FolderId, Name = "mRemoteNG" };
        reader.Add(nodes, folder, "RDP", null);
        var top = new Group();
        top.Add(folder);
        return new MRemoteNgImport(top, reader.Passwords, reader.Skipped);
    }

    // 2.6 ve sonrası AES-GCM, öncesi AES-CBC; başka şifreleme seçenekleri desteklenmez.
    private static Func<string, string, string> Decryptor(XElement root, double version)
    {
        if (version < 2.6)
            return DecryptCbc;
        var engine = (string?)root.Attribute("EncryptionEngine") ?? "AES";
        var mode = (string?)root.Attribute("BlockCipherMode") ?? "GCM";
        if (!engine.Equals("AES", StringComparison.OrdinalIgnoreCase) || !mode.Equals("GCM", StringComparison.OrdinalIgnoreCase))
            throw new MRemoteNgException(MRemoteNgError.NotMRemoteNg);
        var iterations = int.TryParse((string?)root.Attribute("KdfIterations"), out var i) && i > 0 ? i : 1000;
        return (text, password) => DecryptGcm(text, password, iterations);
    }

    // "Protected" alanı varsayılan parolayla açılıyorsa dosya korumasızdır; değilse ana parola gerekir.
    private static string Password(XElement root, Func<string, string, string> decrypt, string? masterPassword)
    {
        if ((string?)root.Attribute("Protected") is not { Length: > 0 } check)
            return DefaultPassword;
        if (TryDecrypt(decrypt, check, DefaultPassword) == "ThisIsNotProtected")
            return DefaultPassword;
        if (string.IsNullOrEmpty(masterPassword))
            throw new MRemoteNgException(MRemoteNgError.PasswordNeeded);
        if (TryDecrypt(decrypt, check, masterPassword) != "ThisIsProtected")
            throw new MRemoteNgException(MRemoteNgError.WrongPassword);
        return masterPassword;
    }

    private static string? TryDecrypt(Func<string, string, string> decrypt, string text, string password)
    {
        try
        {
            return decrypt(text, password);
        }
        catch (MRemoteNgException)
        {
            return null;
        }
    }

    private static string Decrypt(Func<string, string, string> decrypt, string text, string password) =>
        TryDecrypt(decrypt, text.Trim(), password) ?? throw new MRemoteNgException(MRemoteNgError.NotMRemoteNg);

    // base64(salt 16 | nonce 16 | şifreli | etiket 16); anahtar PBKDF2-HMAC-SHA1, ek veri salt.
    private static string DecryptGcm(string text, string password, int iterations)
    {
        try
        {
            var data = Convert.FromBase64String(text);
            var salt = data[..16];
            var nonce = data[16..32];
            var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA1, 32);
            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(false, new AeadParameters(new KeyParameter(key), 128, nonce, salt));
            var output = new byte[cipher.GetOutputSize(data.Length - 32)];
            var length = cipher.ProcessBytes(data, 32, data.Length - 32, output, 0);
            length += cipher.DoFinal(output, length);
            return Encoding.UTF8.GetString(output, 0, length);
        }
        catch (Exception e) when (e is FormatException or ArgumentException or InvalidCipherTextException or DataLengthException)
        {
            throw new MRemoteNgException(MRemoteNgError.NotMRemoteNg);
        }
    }

    // Eski biçim: base64(iv 16 | şifreli), anahtar MD5(parola), AES-CBC.
    private static string DecryptCbc(string text, string password)
    {
        try
        {
            var data = Convert.FromBase64String(text);
            using var aes = Aes.Create();
            aes.Key = MD5.HashData(Encoding.UTF8.GetBytes(password));
            return Encoding.UTF8.GetString(aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16)));
        }
        catch (Exception e) when (e is FormatException or ArgumentException or CryptographicException)
        {
            throw new MRemoteNgException(MRemoteNgError.NotMRemoteNg);
        }
    }

    private static bool IsTrue(XElement element, string name) =>
        string.Equals((string?)element.Attribute(name), "true", StringComparison.OrdinalIgnoreCase);

    private sealed class Reader(Func<string, string> decrypt)
    {
        public Dictionary<Guid, string> Passwords { get; } = [];

        public int Skipped { get; private set; }

        // Protokol ve port "devral" işaretliyse üst klasörün değeri kullanılır.
        public void Add(IEnumerable<XElement> nodes, Group parent, string parentProtocol, string? parentPort)
        {
            foreach (var node in nodes.Where(n => n.Name.LocalName == "Node"))
            {
                var protocol = IsTrue(node, "InheritProtocol") ? parentProtocol : (string?)node.Attribute("Protocol") ?? parentProtocol;
                var port = IsTrue(node, "InheritPort") ? parentPort : (string?)node.Attribute("Port") ?? parentPort;
                if (string.Equals((string?)node.Attribute("Type"), "Container", StringComparison.OrdinalIgnoreCase))
                {
                    var group = new Group
                    {
                        Id = IdOf(node, parent), Name = Text(node, "Name") ?? "", Username = Own(node, "Username"), Domain = Own(node, "Domain"),
                        IsExpanded = node.Attribute("Expanded") is null || IsTrue(node, "Expanded"),
                    };
                    parent.Add(group);
                    SavePassword(node, group.Id);
                    Add(node.Elements(), group, protocol, port);
                    continue;
                }
                Protocol? kind = protocol.ToUpperInvariant() switch { "RDP" => Protocol.Rdp, "SSH2" => Protocol.Ssh, _ => null };
                if (kind is not { } type)
                {
                    Skipped++;
                    continue;
                }
                var host = Text(node, "Hostname") ?? "";
                var server = new Server
                {
                    Id = IdOf(node, parent), Name = Text(node, "Name") ?? host, Host = host, Protocol = type,
                    Port = int.TryParse(port, out var number) && number is > 0 and <= 65535 ? number : type == Protocol.Ssh ? 22 : 3389,
                    Username = Own(node, "Username"), Domain = type == Protocol.Ssh ? null : Own(node, "Domain"),
                };
                parent.Add(server);
                SavePassword(node, server.Id);
            }
        }

        private void SavePassword(XElement node, Guid id)
        {
            if (!IsTrue(node, "InheritPassword") && Text(node, "Password") is { } secret && decrypt(secret) is { Length: > 0 } password)
                Passwords[id] = password;
        }

        // "Devral" işaretli alan boş bırakılır; bizde boş alan klasörden devralınır.
        private static string? Own(XElement node, string name) => IsTrue(node, "Inherit" + name) ? null : Text(node, name);

        private static string? Text(XElement node, string name) =>
            (string?)node.Attribute(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        // mRemoteNG kimliği GUID değilse klasör ve addan sabit bir kimlik türetilir; tekrar alınınca aynısı çıkar.
        private static Guid IdOf(XElement node, Group parent) =>
            Guid.TryParse((string?)node.Attribute("Id"), out var id)
                ? id
                : new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"{parent.Id}/{node.Attribute("Name")?.Value}/{node.Attribute("Hostname")?.Value}")));
    }
}
