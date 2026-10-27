using Avalonia.Controls;

namespace RdpNext.App.Views;

// Uygulama menüsündeki "rdpNext Hakkında"; yalnız ikon, ad, sürüm ve telif.
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = string.Format(Strings.Strings.Version, typeof(AboutWindow).Assembly.GetName().Version?.ToString(3));
    }
}
