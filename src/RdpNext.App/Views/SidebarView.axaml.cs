using Avalonia.Controls;
using Avalonia.Input;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

public partial class SidebarView : UserControl
{
    public SidebarView() => InitializeComponent();

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control { DataContext: ServerNodeViewModel server } && DataContext is MainWindowViewModel vm)
            vm.ConnectCommand.Execute(server);
    }
}
