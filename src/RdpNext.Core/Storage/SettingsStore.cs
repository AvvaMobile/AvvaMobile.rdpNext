using System.Text.Json;

namespace RdpNext.Core.Storage;

public sealed class AppSettings
{
    // "tr" ya da "en"; null ise sistem dili kullanılır.
    public string? Language { get; set; }
}

// Uygulama ayarları; bağlantı dosyasının yanında settings.json.
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string DefaultPath() => Beside(ConnectionStore.DefaultPath());

    public static string Beside(string connectionsPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(connectionsPath))!, "settings.json");

    // Dosya yoksa ya da okunamıyorsa varsayılanlar; ayar bozulunca uygulama açılmaya devam etmeli.
    public AppSettings Load()
    {
        try
        {
            return File.Exists(filePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, filePath, overwrite: true);
    }
}
