using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Storage;

namespace RdpNext.App.ViewModels;

// Ayarlar sekmesi; şimdilik yalnız yedek: listeyi dosyaya ver, dosyadan geri yükle.
public sealed partial class SettingsViewModel(MainWindowViewModel owner) : TabViewModel
{
    public override string Title => Strings.Strings.Settings;

    // Son işlemin sonucu ya da hatası; ekranda düğmelerin altında.
    [ObservableProperty]
    private string? _status;

    // İçe aktarırken mevcut liste silinsin mi; işaretsizse dosyadakiler listeye eklenir.
    [ObservableProperty]
    private bool _deleteOld;

    [RelayCommand]
    private async Task Export()
    {
        var path = await owner.Files.SaveAsync($"rdpNext-yedek-{DateTime.Now:yyyy-MM-dd}.json");
        if (path is null)
            return;
        try
        {
            new ConnectionStore(path).Save(owner.Tree.Root);
            Status = string.Format(Strings.Strings.ExportDone, Path.GetFileName(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = string.Format(Strings.Strings.ExportFailed, Path.GetFileName(path));
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        var path = await owner.Files.OpenAsync();
        if (path is null)
            return;
        if (ConnectionStore.Read(path) is not { } root)
        {
            Status = string.Format(Strings.Strings.ImportFailed, Path.GetFileName(path));
            return;
        }
        if (!DeleteOld)
        {
            Status = string.Format(Strings.Strings.ImportAdded, owner.MergeTree(root));
            return;
        }
        if (!await owner.Confirm.ConfirmAsync(Strings.Strings.ImportDeleteConfirm, Strings.Strings.DeleteAndImport))
            return;
        owner.ReplaceTree(root, keep: this);
        Status = string.Format(Strings.Strings.ImportDone, root.AllServers().Count());
    }
}
