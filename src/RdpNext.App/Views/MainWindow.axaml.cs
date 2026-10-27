using Avalonia.Controls;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        // macOS'ta sol üstteki pencere düğmeleri sekmelerin üstüne binmesin.
        if (OperatingSystem.IsMacOS())
            Tabs.Margin = new Avalonia.Thickness(84, 0, 14, 0);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
            _vm.FocusSearchRequested -= Sidebar.FocusSearch;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null)
            _vm.FocusSearchRequested += Sidebar.FocusSearch;
    }
}
