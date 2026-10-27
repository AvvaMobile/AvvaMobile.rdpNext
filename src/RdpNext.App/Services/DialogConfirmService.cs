using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using RdpNext.App.Views;

namespace RdpNext.App.Services;

// Ana pencerenin üstünde küçük bir onay penceresi açar.
public sealed class DialogConfirmService : IConfirmService
{
    public async Task<bool> ConfirmAsync(string message, string confirmText)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            return false;
        return await new ConfirmDialog(message, confirmText).ShowDialog<bool>(owner);
    }

    public async Task<string?> AskTextAsync(string message, string confirmText)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            return null;
        return await new TextPromptDialog(message, confirmText).ShowDialog<string?>(owner);
    }
}
