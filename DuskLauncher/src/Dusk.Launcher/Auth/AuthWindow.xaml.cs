using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Dusk.Launcher.Auth;

public partial class AuthWindow : Window
{
    private bool _completed;

    public AuthWindow()
    {
        InitializeComponent();
    }

    private async void OnAuthorize(object sender, RoutedEventArgs e)
    {
        AuthorizeButton.IsEnabled = false;
        StatusText.Text = "Opening Discord...";
        var token = await DiscordAuth.AuthorizeAsync();

        if (token is null)
        {
            StatusText.Text = "Authorization failed. Try again.";
            AuthorizeButton.IsEnabled = true;
            return;
        }

        _completed = true;
        StatusText.Text = $"Verified as {token.GlobalName ?? token.Username ?? "Discord User"}.";
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = _completed;
        Close();
    }
}