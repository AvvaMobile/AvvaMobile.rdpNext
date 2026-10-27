namespace RdpNext.App.Services;

public interface IConfirmService
{
    Task<bool> ConfirmAsync(string message, string confirmText);

    // Tek satırlık metin sorar; vazgeçilirse null.
    Task<string?> AskTextAsync(string message, string confirmText);
}
