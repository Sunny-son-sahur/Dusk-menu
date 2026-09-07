using System;
using System.Windows;
using System.Windows.Controls;
using Dusk.Launcher.Detection;
using Dusk.Launcher.Injection;
using Microsoft.Win32;

namespace Dusk.Launcher.Views;

public partial class HomeView : UserControl
{
    private readonly Settings _settings = Settings.Load();
    private bool _suppressPathChange;

    public HomeView()
    {
        InitializeComponent();
        ApplySettings(_settings);
        RefreshMenuDllStatus();
    }

    private void ApplySettings(Settings s)
    {
        _suppressPathChange = true;
        if (s.GamePath is string p)
            PathBox.Text = p;

        switch (s.GamePlatform)
        {
            case "Meta":   PlatformMeta.IsChecked = true;   break;
            case "Custom": PlatformCustom.IsChecked = true; break;
            default:       PlatformSteam.IsChecked = true;  break;
        }
        _suppressPathChange = false;

        RefreshMenuDllStatus();
    }

    private void OnPlatformChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressPathChange) return;
        SaveSettings();
        // If a platform change happens and the existing path is empty, try detect.
        if (string.IsNullOrWhiteSpace(PathBox.Text))
            TryAutoDetect();
    }

    private void OnPathChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressPathChange) return;
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settings.GamePath = PathBox.Text;
        _settings.GamePlatform = PlatformMeta.IsChecked == true ? "Meta"
            : PlatformCustom.IsChecked == true ? "Custom" : "Steam";
        _settings.Save();
    }

    private void OnAutoDetect(object sender, RoutedEventArgs e) => TryAutoDetect();

    private void TryAutoDetect()
    {
        StatusText.Text = "Detecting Gorilla Tag install...";
        var detected = GamePathDetector.Detect();
        if (detected != null)
        {
            _suppressPathChange = true;
            PathBox.Text = detected.Path;
            if (detected.Platform == GamePathDetector.Platform.Steam)
                PlatformSteam.IsChecked = true;
            else if (detected.Platform == GamePathDetector.Platform.Meta)
                PlatformMeta.IsChecked = true;
            _suppressPathChange = false;

            StatusText.Text = $"Found: {detected.Path} ({detected.Platform})";
            DetectStatus.Text = $"Detected: {(detected.Platform == GamePathDetector.Platform.Steam ? "Steam" : "Meta")} install";
        }
        else
        {
            StatusText.Text = "Could not auto-detect. Pick the path manually.";
            DetectStatus.Text = "No install found in the usual locations.";
        }
        SaveSettings();
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Gorilla Tag.exe",
            Filter = "Gorilla Tag|Gorilla Tag.exe|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            PathBox.Text = System.IO.Path.GetDirectoryName(dialog.FileName) ?? dialog.FileName;
            SaveSettings();
            StatusText.Text = $"Path set: {PathBox.Text}";
        }
    }

    private void RefreshMenuDllStatus()
    {
        string dll = _settings.ResolveMenuDllPath();
        bool exists = System.IO.File.Exists(dll);
        MenuDllStatus.Text = exists
            ? $"Menu DLL ready: {dll}"
            : $"Menu DLL not found: {dll}\nPlace SentinelMenu.dll here, or pick it in Settings.";
    }

    private async void OnLaunchAndLoad(object sender, RoutedEventArgs e)
    {
        if (!ValidatePath()) return;
        LaunchLoadButton.IsEnabled = false;
        StatusText.Text = "Launching Gorilla Tag...";

        var result = await Injection.DuskInjector.LaunchAndLoadAsync(_settings.ResolveMenuDllPath());
        StatusText.Text = result.Message;
        StatusText.Foreground = result.Success
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.LightCoral;
        LaunchLoadButton.IsEnabled = true;
    }

    private void OnLoad(object sender, RoutedEventArgs e)
    {
        if (!ValidatePath()) return;
        StatusText.Text = "Injecting into running Gorilla Tag...";
        var result = Injection.DuskInjector.Load(_settings.ResolveMenuDllPath());
        StatusText.Text = result.Message;
        StatusText.Foreground = result.Success
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.LightCoral;
    }

    private void OnOpenMenuDir(object sender, RoutedEventArgs e)
    {
        string dir = System.IO.Path.GetDirectoryName(_settings.ResolveMenuDllPath()) ?? App.DataDir;
        System.IO.Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", dir)
        {
            UseShellExecute = true
        });
    }

    private bool ValidatePath()
    {
        if (System.IO.Directory.Exists(PathBox.Text) ||
            System.IO.File.Exists(System.IO.Path.Combine(PathBox.Text, "Gorilla Tag.exe")))
            return true;

        StatusText.Text = "Pick a valid Gorilla Tag path first (Auto Detect or Browse).";
        StatusText.Foreground = System.Windows.Media.Brushes.LightCoral;
        return false;
    }
}