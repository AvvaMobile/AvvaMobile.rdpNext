using System.Text.Json;
using System.Text.Json.Serialization;
using RdpNext.Core.Model;

namespace RdpNext.Core.Storage;

public sealed class ConnectionStore(string filePath)
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string FilePath => filePath;

    private sealed record ConnectionFile(int Version, Group Root);

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rdpNext", "connections.json");

    public LoadResult Load()
    {
        if (!File.Exists(filePath))
            return new LoadResult(new Group(), null);
        if (Read(filePath) is { } root)
            return new LoadResult(root, null);

        // Okunamayan dosyanın üstüne asla yazılmaz; kenara alınır.
        var backup = BackupPath();
        File.Move(filePath, backup);
        return new LoadResult(new Group(), backup);
    }

    // Dosyayı okur; yoksa ya da okunamıyorsa null. Dosyaya dokunmaz (içe aktarmada kullanılır).
    public static Group? Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<ConnectionFile>(File.ReadAllText(path), Options)?.Root;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(Group root)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temp, JsonSerializer.Serialize(new ConnectionFile(CurrentVersion, root), Options));
        File.Move(temp, filePath, overwrite: true);
    }

    private string BackupPath()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var stem = $"{Path.GetFileNameWithoutExtension(filePath)}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        var path = Path.Combine(directory, stem + ".json");
        for (var i = 1; File.Exists(path); i++)
            path = Path.Combine(directory, $"{stem}-{i}.json");
        return path;
    }
}
