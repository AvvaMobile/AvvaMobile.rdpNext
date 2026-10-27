using Avalonia.Controls;

namespace RdpNext.App.Views;

public partial class TextPromptDialog : Window
{
    public TextPromptDialog() : this("", "")
    {
    }

    public TextPromptDialog(string message, string confirmText)
    {
        InitializeComponent();
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        ConfirmButton.Click += (_, _) => Close(InputBox.Text);
        CancelButton.Click += (_, _) => Close(null);
        Opened += (_, _) => InputBox.Focus();
    }
}
