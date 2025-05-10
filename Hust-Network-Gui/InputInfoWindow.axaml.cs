using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HustNetworkGui;

public partial class InputInfoWindow : Window
{
    public InputInfoWindow() : this(null, null, null)
    {
    }

    public InputInfoWindow(string? username, string? password, string? msg = null)
    {
        InitializeComponent();
        UsernameTextBlock.Text = username;
        PasswordTextBlock.Text = password;
        Message.Text = msg;
    }

    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (UsernameTextBlock.Text is not null && PasswordTextBlock.Text is not null)
        {
            Close((UsernameTextBlock.Text, PasswordTextBlock.Text));
        }
    }
}