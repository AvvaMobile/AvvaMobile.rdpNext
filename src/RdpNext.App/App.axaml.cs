using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RdpNext.App.ViewModels;
using RdpNext.App.Views;
using RdpNext.Core.Secrets;
using RdpNext.Core.Storage;

namespace RdpNext.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsStore(SettingsStore.DefaultPath());
            var vm = new MainWindowViewModel(new ConnectionStore(ConnectionStore.DefaultPath()), SecretStores.ForCurrentPlatform(), settings: settings);
            desktop.MainWindow = new MainWindow { DataContext = vm };
            if (OperatingSystem.IsMacOS())
                Platform.MacDockIcon.Set(new Uri("avares://rdpNext/Assets/rdpNext.png"));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
