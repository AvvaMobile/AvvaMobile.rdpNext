using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RdpNext.Core.Storage;

namespace RdpNext.App.ViewModels;

// Ayarlar sekmesi: Genel (dil), Dışa aktar (listeyi dosyaya ver) ve İçe aktar (dosyadan yükle).
public sealed partial class SettingsViewModel(MainWindowViewModel owner) : TabViewModel
{
    public override string Title => Strings.Strings.Settings;

    // Son işlemin sonucu ya da hatası; ekranda düğmelerin altında.
    [ObservableProperty]
    private string? _status;

    public enum SettingsPage
    {
        General,
        Export,
        Import,
    }

    // Alt sekme; Ayarlar Genel ile açılır.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGeneral), nameof(ShowExport), nameof(ShowImport))]
    private SettingsPage _page;

    // Sekme düğmeleri için; işaretlenen sekme açılır, işaret kalkması yok sayılır.
    public bool ShowGeneral
    {
        get => Page == SettingsPage.General;
        set => Select(SettingsPage.General, value);
    }

    public bool ShowExport
    {
        get => Page == SettingsPage.Export;
        set => Select(SettingsPage.Export, value);
    }

    public bool ShowImport
    {
        get => Page == SettingsPage.Import;
        set => Select(SettingsPage.Import, value);
    }

    private void Select(SettingsPage page, bool selected)
    {
        if (selected)
            Page = page;
    }

    // Genel sekmesindeki ayarlar Kaydet'e basınca yazılır. Dil seçicisindeki sıra: 0 Türkçe, 1 English.
    private static int SavedLanguageIndex(MainWindowViewModel owner) =>
        (owner.Settings.Load().Language ?? Languages.Current.TwoLetterISOLanguageName) == Languages.English ? 1 : 0;

    private static string LanguageAt(int index) => index == 1 ? Languages.English : Languages.Turkish;

    private int _savedLanguageIndex = SavedLanguageIndex(owner);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveGeneralCommand))]
    private int _languageIndex = SavedLanguageIndex(owner);

    // "Yeniden başlatınca uygulanır" yalnız kaydedilen dil açık olandan farklıysa görünür.
    public bool ShowRestartNote => LanguageAt(_savedLanguageIndex) != Languages.Current.TwoLetterISOLanguageName;

    private bool CanSaveGeneral() => LanguageIndex != _savedLanguageIndex;

    [RelayCommand(CanExecute = nameof(CanSaveGeneral))]
    private void SaveGeneral()
    {
        var settings = owner.Settings.Load();
        settings.Language = LanguageAt(LanguageIndex);
        owner.Settings.Save(settings);
        _savedLanguageIndex = LanguageIndex;
        SaveGeneralCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowRestartNote));
    }

    // İçe aktarırken mevcut liste silinsin mi; işaretsizse dosyadakiler listeye eklenir.
    [ObservableProperty]
    private bool _deleteOld;

    [RelayCommand]
    private async Task Export()
    {
        var path = await owner.Files.SaveAsync(string.Format(Strings.Strings.BackupFileName, DateTime.Now.ToString("yyyy-MM-dd")));
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
