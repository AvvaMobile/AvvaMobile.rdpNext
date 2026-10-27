using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace RdpNext.App.Services;

// Ana pencerenin sistem dosya pencerelerini açar; yalnız .json dosyaları.
public sealed class FileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType Json = new("JSON") { Patterns = ["*.json"] };

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

    public async Task<string?> OpenAsync()
    {
        if (Owner() is not { } owner)
            return null;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [Json],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private static Window? Owner() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner } ? owner : null;
}
