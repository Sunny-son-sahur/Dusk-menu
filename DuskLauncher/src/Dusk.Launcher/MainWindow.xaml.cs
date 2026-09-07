using System.Windows;
using System.Windows.Controls;
using Dusk.Launcher.Auth;

namespace Dusk.Launcher;

public partial class MainWindow : Window
{
    private readonly Settings _settings = Settings.Load();

    public MainWindow()
    {
        InitializeComponent();

        var token = DiscordAuth.LoadToken();
        AccountText.Text = token?.GlobalName ?? token?.Username ?? "Not connected";
        if (token?.AvatarUrl is string avatar)
            _ = avatar; // avatar rendering can be added later

        NavHome.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private void OnNav(object sender, RoutedEventArgs e)
    {
        string tag = (string)((FrameworkElement)sender).Tag;
        bool home = tag == "home";
        HomeView.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        FriendsView.Visibility = tag == "friends" ? Visibility.Visible : Visibility.Collapsed;
        ConnectionsView.Visibility = tag == "connections" ? Visibility.Visible : Visibility.Collapsed;

        // Keep the view models current when switching tabs.
        if (tag == "settings")
            SettingsView.Refresh(_settings);
        if (tag == "friends")
            FriendsView.Refresh();
        if (tag == "connections")
            ConnectionsView.Refresh();
    }

    private void OnDisconnect(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Disconnect your Discord account from Dusk Launcher?",
                "Disconnect", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        DiscordAuth.ClearToken();
        AccountText.Text = "Not connected";
        MessageBox.Show("Disconnected. Restart Dusk Launcher to re-verify.",
            "Dusk Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}