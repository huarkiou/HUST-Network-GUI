using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HustNetworkGui;

public partial class InputInfoWindow : Window
{
    public InputInfoWindow()
    {
        InitializeComponent();
    }

    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (UsernameTextBlock.Text is not null && PasswordTextBlock.Text is not null)
        {
            Close((UsernameTextBlock.Text, PasswordTextBlock.Text));
        }
    }
}