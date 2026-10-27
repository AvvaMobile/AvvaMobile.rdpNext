namespace RdpNext.App.Services;

public interface IConfirmService
{
    Task<bool> ConfirmAsync(string message, string confirmText);
}
