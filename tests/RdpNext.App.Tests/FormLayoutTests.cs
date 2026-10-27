using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Tests;

// Tek başına duran alanlar her ekranda aynı genişlikte: form ızgarasının yarısı (klasör adı gibi).
public class FormLayoutTests
{
    [AvaloniaFact]
    public void SingleFieldsShareFolderNameWidth()
    {
        var (window, vm) = TestWindows.Open();

        vm.NewFolderCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var folderName = window.Named<TextBox>("FolderNameBox").Bounds.Width;

        vm.OpenSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var language = window.Named<ComboBox>("LanguageBox").Bounds.Width;

        vm.NewServer();
        Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected).ShowAdvanced = true;
        Dispatcher.UIThread.RunJobs();
        var screen = window.Named<ComboBox>("ScreenBox").Bounds.Width;

        Assert.True(folderName > 300, $"{folderName}");
        Assert.Equal(folderName, language, 0.5);
        Assert.Equal(folderName, screen, 0.5);
    }
}
