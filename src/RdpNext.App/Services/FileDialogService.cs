using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace RdpNext.App.Services;

// Ana pencerenin sistem dosya pencerelerini açar; yedek için yalnız .json, anahtar için her dosya.
public sealed class FileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType Json = new("JSON") { Patterns = ["*.json"] };

    private static readonly FilePickerFileType Xml = new("mRemoteNG") { Patterns = ["*.xml"] };

    public async Task<string?> SaveAsync(string suggestedName)
    {
        if (Owner() is not { } owner)
            return null;
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            SuggestedStartLocation = await owner.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Downloads),
            DefaultExtension = "json",
            FileTypeChoices = [Json],
        });
        return file?.TryGetLocalPath();
    }

    public Task<string?> OpenAsync() => OpenAsync(Json);

    public Task<string?> OpenMRemoteNgAsync() => OpenAsync(Xml);

    private static async Task<string?> OpenAsync(FilePickerFileType type)
    {
        if (Owner() is not { } owner)
            return null;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [type],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> OpenKeyFileAsync()
    {
        if (Owner() is not { } owner)
            return null;
        var ssh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = Directory.Exists(ssh) ? await owner.StorageProvider.TryGetFolderFromPathAsync(ssh) : null,
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private static Window? Owner() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner } ? owner : null;
}
