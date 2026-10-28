using Avalonia;
using Avalonia.Controls;
using RdpNext.Core.Model;

namespace RdpNext.App.Views;

// Sunucu kutucuğundaki beyaz ikon: Windows logosu ya da terminal.
public partial class ServerIconView : UserControl
{
    public static readonly StyledProperty<ServerIcon> IconProperty =
        AvaloniaProperty.Register<ServerIconView, ServerIcon>(nameof(Icon));

    public ServerIconView()
    {
        InitializeComponent();
    }

    public ServerIcon Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IconProperty)
            return;
        WindowsGlyph.IsVisible = Icon == ServerIcon.Windows;
        SshGlyph.IsVisible = Icon == ServerIcon.Ssh;
    }
}
