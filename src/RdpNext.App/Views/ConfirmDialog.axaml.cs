using Avalonia.Controls;

namespace RdpNext.App.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog() : this("", "")
    {
    }

    public ConfirmDialog(string message, string confirmText)
    {
        InitializeComponent();
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        ConfirmButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
    }
}
