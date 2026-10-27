using System.Collections;
using System.Globalization;
using System.Resources;
using Avalonia.Headless.XUnit;
using RdpNext.App.ViewModels;
using RdpNext.Core.Storage;

namespace RdpNext.App.Tests;

public class LanguageTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Theory]
    [InlineData("tr", "en-US", "tr")]
    [InlineData("en", "tr-TR", "en")]
    [InlineData(null, "tr-TR", "tr")]
    [InlineData(null, "en-GB", "en")]
    [InlineData(null, "de-DE", "en")]
    [InlineData("fr", "tr-TR", "tr")]
    public void ResolvePrefersSavedThenSystem(string? saved, string system, string expected) =>
        Assert.Equal(expected, Languages.Resolve(saved, CultureInfo.GetCultureInfo(system)).TwoLetterISOLanguageName);

    [Fact]
    public void EnglishHasEveryString()
    {
        var neutral = Keys(CultureInfo.InvariantCulture);
        var english = Keys(CultureInfo.GetCultureInfo("en"));

        Assert.Empty(neutral.Except(english));
    }

    private static HashSet<string> Keys(CultureInfo culture)
    {
        var set = Strings.Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!;
        return set.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToHashSet();
    }

    [Fact]
    public void SourceUsesSuffixInTurkishAndFromInEnglish()
    {
        Assert.Equal("Üretim'den", Languages.From("Üretim", CultureInfo.GetCultureInfo("tr")));
        Assert.Equal("from Üretim", Languages.From("Üretim", CultureInfo.GetCultureInfo("en")));
    }

    [AvaloniaFact]
    public void LanguageIsSavedOnlyWithSaveButton()
    {
        var path = TestWindows.SampleFile();
        var vm = TestWindows.ViewModel(path);
        var store = new SettingsStore(SettingsStore.Beside(path));
        vm.OpenSettingsCommand.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(vm.Sessions.Selected);
        Assert.Equal(0, settings.LanguageIndex);
        Assert.False(settings.SaveGeneralCommand.CanExecute(null));

        settings.LanguageIndex = 1;
        Assert.Null(store.Load().Language);
        Assert.True(settings.SaveGeneralCommand.CanExecute(null));

        settings.SaveGeneralCommand.Execute(null);
        Assert.Equal("en", store.Load().Language);
        Assert.False(settings.SaveGeneralCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void RestartNoteShowsAfterSavingOtherLanguage()
    {
        var (window, vm) = TestWindows.Open();
        vm.OpenSettingsCommand.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(vm.Sessions.Selected);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var note = window.Named<Avalonia.Controls.TextBlock>("RestartNote");
        Assert.True(window.Named<Avalonia.Controls.Button>("SaveGeneralButton").IsEffectivelyVisible);
        Assert.False(note.IsEffectivelyVisible);

        settings.LanguageIndex = 1;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(note.IsEffectivelyVisible);

        settings.SaveGeneralCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(note.IsEffectivelyVisible);

        settings.LanguageIndex = 0;
        settings.SaveGeneralCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(note.IsEffectivelyVisible);
    }
}
