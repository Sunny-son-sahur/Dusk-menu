using System.Windows;
using System.Windows.Controls;
using Dusk.Launcher.Auth;

namespace Dusk.Launcher;

public partial class MainWindow : Window
{
    private readonly Settings _settings = Settings.Load();
    private bool _gateAllowed;

    public MainWindow()
    {
        InitializeComponent();

        var token = DiscordAuth.LoadToken();
        AccountText.Text = token?.GlobalName ?? token?.Username ?? "Not connected";

        // Access gate: server membership + role must pass before unlock.
        Loaded += async (_, _) => await RunAccessCheckAsync();
    }

    private async Task RunAccessCheckAsync()
    {
        ShowGate("Checking access…", "Verifying your Discord server access.", null);
        var result = await AccessGate.CheckAsync();

        switch (result.Status)
        {
            case AccessGate.Status.Allowed:
                _gateAllowed = true;
                GatePanel.Visibility = Visibility.Collapsed;
                NavHome.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                break;

            case AccessGate.Status.NotInServer:
                ShowGate(
                    "You're not in the Discord server",
                    "Dusk Menu is invite-only. Join the server first, then check again:",
                    "Open Discord invite");
                break;

            case AccessGate.Status.MissingRole:
                ShowGate(
                    "Role required",
                    $"You're in the server, but you don't have the {(result.RoleName is null ? "required" : $"\"{result.RoleName}\"")} role.\n" +
                    "Ask in the server to get the role, then check again.",
                    "Open Discord invite");
                break;

            case AccessGate.Status.ServiceError:
                ShowGate(
                    "Verification service offline",
                    "Couldn't reach the role verification service. Try again in a moment.",
                    null);
                break;

            default:
                ShowGate(
                    "Not logged in",
                    "Your Discord login isn't valid anymore. Restart the launcher to re-authorize.",
                    null);
                break;
        }
    }

    private void ShowGate(string title, string message, string? actionLabel)
    {
        _gateAllowed = false;
        GateTitle.Text = title;
        GateMessage.Text = message;

        GateActionButton.Content = actionLabel ?? "";
        GateActionButton.Visibility = actionLabel is null ? Visibility.Collapsed : Visibility.Visible;

        GateRetryButton.Visibility = Visibility.Visible;
        GateStatusText.Text = AccessGate.IsConfigured()
            ? ""
            : "Setup note: access gate values are still placeholders (see Auth/AccessGate.cs).";

        GatePanel.Visibility = Visibility.Visible;
    }

    private void OnGateAction(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AccessGate.InviteUrl)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // browser couldn't open; the message already shows the link context
        }
    }

    private async void OnGateRetry(object sender, RoutedEventArgs e)
        => await RunAccessCheckAsync();

    private void OnNav(object sender, RoutedEventArgs e)
    {
        if (!_gateAllowed)
            return;

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