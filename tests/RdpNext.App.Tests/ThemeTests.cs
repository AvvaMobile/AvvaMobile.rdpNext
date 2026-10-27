using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class ThemeTests
{
    // Seçicideki sıra: 0 Dark, 1 Light, 2 System. Kaydet'e basınca yazılır ve hemen uygulanır.
    [AvaloniaFact]
    public void ThemeIsSavedAndAppliedOnlyWithSaveButton()
    {
        var path = TestWindows.SampleFile();
        var vm = TestWindows.ViewModel(path);
        var store = new SettingsStore(SettingsStore.Beside(path));
        vm.OpenSettingsCommand.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(vm.Sessions.Selected);
        Assert.Equal(2, settings.ThemeIndex);
        Assert.False(settings.SaveGeneralCommand.CanExecute(null));

        try
        {
            settings.ThemeIndex = 0;
            Assert.Equal(AppTheme.System, store.Load().Theme);
            Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
            Assert.True(settings.SaveGeneralCommand.CanExecute(null));

            settings.SaveGeneralCommand.Execute(null);
            Assert.Equal(AppTheme.Dark, store.Load().Theme);
            Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
            Assert.False(settings.SaveGeneralCommand.CanExecute(null));

            settings.ThemeIndex = 1;
            settings.SaveGeneralCommand.Execute(null);
            Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
        }
        finally
        {
            Themes.Apply(AppTheme.System);
        }
    }

    [AvaloniaFact]
    public void ThemeBoxListsDarkLightSystem()
    {
        var (window, vm) = TestWindows.Open();
        vm.OpenSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var box = window.Named<ComboBox>("ThemeBox");
        Assert.Equal(["Dark", "Light", "System"], box.Items.Cast<ComboBoxItem>().Select(i => i.Content as string));
        Assert.True(window.Named<Button>("SaveGeneralButton").IsEffectivelyVisible);
    }
}
