namespace RdpNext.App.Services;

// Dosya kaydet/aç pencereleri; vazgeçilirse null.
public interface IFileDialogService
{
    Task<string?> SaveAsync(string suggestedName);

    Task<string?> OpenAsync();
}
