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
            // İşletim sistemi anahtar zinciri Plan 3'te gelecek.
            var vm = new MainWindowViewModel(new ConnectionStore(ConnectionStore.DefaultPath()), new InMemorySecretStore());
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
