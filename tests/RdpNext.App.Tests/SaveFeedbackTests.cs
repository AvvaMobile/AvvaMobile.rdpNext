using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Tests;

// Ayar ekranlarında tek Save; kaydedince sağ üstte kısa bir "Saved" bildirimi.
public class SaveFeedbackTests
{
    private readonly FakeTimeProvider _time = new();

    private (Views.MainWindow Window, MainWindowViewModel Vm) Open()
    {
        var vm = TestWindows.ViewModel(time: _time);
        var window = new Views.MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    private static ServerEditorViewModel EditWeb(MainWindowViewModel vm)
    {
        vm.EditCommand.Execute(vm.Tree.Node<ServerNodeViewModel>(vm.Tree.Root.AllServers().First(s => s.Name == "web-01").Id));
        Dispatcher.UIThread.RunJobs();
        return Assert.IsType<ServerEditorViewModel>(vm.Sessions.Selected);
    }

    private static List<Button> SaveButtons(Window window) => window.Named<Control>("SessionArea").GetVisualDescendants()
        .OfType<Button>().Where(b => b.Content as string == "Save").ToList();

    [AvaloniaFact]
    public void EditorHasOneSaveOnBothTabs()
    {
        var (window, vm) = Open();
        var editor = EditWeb(vm);

        Assert.True(Assert.Single(SaveButtons(window)).IsEffectivelyVisible);
        editor.ShowAdvanced = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(Assert.Single(SaveButtons(window)).IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void ScreenOptionsAreFullScreenAndWindowed()
    {
        var (window, vm) = Open();
        var editor = EditWeb(vm);
        editor.ShowAdvanced = true;
        Dispatcher.UIThread.RunJobs();

        var box = window.Named<ComboBox>("ScreenBox");
        Assert.Equal(["Full Screen", "Windowed"], box.Items.Cast<ComboBoxItem>().Select(i => i.Content as string));
        Assert.Equal(1, editor.ScreenIndex);
        editor.ScreenIndex = 0;
        Assert.Equal(ScreenMode.FullScreen, editor.Screen);
    }

    [AvaloniaFact]
    public void SavingServerShowsToastTopRight()
    {
        var (window, vm) = Open();
        var editor = EditWeb(vm);

        editor.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var toast = window.Named<Control>("Toast");
        Assert.True(toast.IsEffectivelyVisible);
        Assert.Contains(toast.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Saved");
        Assert.Equal((HorizontalAlignment.Right, VerticalAlignment.Top), (toast.HorizontalAlignment, toast.VerticalAlignment));

        _time.Advance(TimeSpan.FromSeconds(3));
        Dispatcher.UIThread.RunJobs();
        Assert.False(toast.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void SavingFolderAndSettingsShowsToast()
    {
        var (_, vm) = Open();
        vm.EditCommand.Execute(vm.Tree.Node<GroupNodeViewModel>(vm.Tree.Root.Children.OfType<Group>().First().Id));
        Assert.IsType<FolderEditorViewModel>(vm.Sessions.Selected).SaveCommand.Execute(null);
        Assert.Equal("Saved", vm.ToastMessage);

        _time.Advance(TimeSpan.FromSeconds(3));
        Dispatcher.UIThread.RunJobs();
        vm.OpenSettingsCommand.Execute(null);
        var settings = Assert.IsType<SettingsViewModel>(vm.Sessions.Selected);
        settings.ThemeIndex = 0;
        settings.SaveGeneralCommand.Execute(null);
        Themes.Apply(Core.Storage.AppTheme.System);
        Assert.Equal("Saved", vm.ToastMessage);
    }
}
