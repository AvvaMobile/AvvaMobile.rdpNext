namespace RdpNext.App.Services;

// Dosya kaydet/aç pencereleri; vazgeçilirse null.
public interface IFileDialogService
{
    Task<string?> SaveAsync(string suggestedName);

    Task<string?> OpenAsync();

    // SSH özel anahtarı; filtre yok, ~/.ssh'tan başlar.
    Task<string?> OpenKeyFileAsync();

    // mRemoteNG bağlantı dosyası (.xml).
    Task<string?> OpenMRemoteNgAsync();
}
