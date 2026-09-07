using System;
using System.IO;
using System.Windows;

namespace Dusk.Launcher;

public partial class App : Application
{
    /// <summary>Root storage for settings, auth token, etc.</summary>
    public static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Dusk Launcher");

    protected override void OnStartup(StartupEventArgs e)
    {
        Directory.CreateDirectory(DataDir);

        // First run (or missing auth) -> show the Discord auth gate first.
        if (Auth.DiscordAuth.LoadToken() is null)
        {
            var gate = new Auth.AuthWindow();
            if (!gate.ShowDialog()!.Value)
            {
                Shutdown(0);
                return;
            }
        }

        var main = new MainWindow();
        main.Show();
    }
}