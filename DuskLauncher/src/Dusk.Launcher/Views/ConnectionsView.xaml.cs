using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Dusk.Launcher.Auth;

namespace Dusk.Launcher.Views;

public partial class ConnectionsView : UserControl
{
    public ConnectionsView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh()
    {
        var token = DiscordAuth.LoadToken();
        DiscordStatus.Text = token is null ? "Not verified" : $"Connected · {token.GlobalName ?? token.Username}";
        DiscordStatus.Foreground = token is null
            ? System.Windows.Media.Brushes.LightCoral
            : System.Windows.Media.Brushes.LightGreen;

        var game = Process.GetProcessesByName("Gorilla Tag");
        GameStatus.Text = game.Length > 0 ? "Running" : "Not running";
        GameStatus.Foreground = game.Length > 0
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.Gray;

        string dll = new Settings().ResolveMenuDllPath();
        bool dllOk = System.IO.File.Exists(dll);
        MenuStatus.Text = dllOk ? "Ready" : "Missing DLL";
        MenuStatus.Foreground = dllOk
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.LightCoral;

        Log($"Discord: {(token is null ? "not verified" : "verified")}");
        Log($"Game: {(game.Length > 0 ? "running" : "not running")}");
        Log($"Menu DLL: {(dllOk ? "ready" : "missing")} ({dll})");
    }

    private void Log(string line)
    {
        LogList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
        while (LogList.Items.Count > 200)
            LogList.Items.RemoveAt(0);
        LogList.ScrollIntoView(LogList.Items[^1]);
    }
}