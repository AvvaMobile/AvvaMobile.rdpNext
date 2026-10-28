using System.Text.Json;
using System.Text.Json.Serialization;

namespace RdpNext.Core.Storage;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public sealed class AppSettings
{
    public AppTheme Theme { get; set; }

    public const double DefaultSidebarWidth = 310;

    public double SidebarWidth { get; set; } = DefaultSidebarWidth;
}

// Uygulama ayarları; bağlantı dosyasının yanında settings.json.
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
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
