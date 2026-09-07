using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Dusk.Launcher.Views;

public partial class SettingsView : UserControl
{
    private readonly Settings _settings = Settings.Load();

    public SettingsView()
    {
        InitializeComponent();
        Refresh(_settings);
    }

    public void Refresh(Settings? s = null)
    {
        var current = s ?? _settings;
        MenuDllBox.Text = current.MenuDllPath ?? current.ResolveMenuDllPath();
        AutoDetectCheck.IsChecked = current.AutoDetectOnLaunch;
        LaunchOnStartCheck.IsChecked = current.LaunchOnStart;
    }

    private void OnDllChanged(object sender, TextChangedEventArgs e)
    {
        _settings.MenuDllPath = MenuDllBox.Text;
        _settings.Save();
    }

    private void OnBrowseDll(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select SentinelMenu.dll",
            Filter = "Menu DLL (SentinelMenu.dll)|*.dll|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            MenuDllBox.Text = dialog.FileName;
            _settings.MenuDllPath = dialog.FileName;
            _settings.Save();
        }
    }

    private void OnAccent(object sender, RoutedEventArgs e)
    {
        var hex = (string)((FrameworkElement)sender).Tag;
        var color = (Color)ColorConverter.ConvertFromString(hex);
        ApplyAccent(color);
        _settings.AccentHex = hex;
        _settings.Save();
    }

    public static void ApplyAccent(Color color)
    {
        if (Application.Current.Resources["AccentBrush"] is SolidColorBrush brush)
            brush.Color = color;
    }

    private void OnBehaviorChanged(object sender, RoutedEventArgs e)
    {
        _settings.AutoDetectOnLaunch = AutoDetectCheck.IsChecked == true;
        _settings.LaunchOnStart = LaunchOnStartCheck.IsChecked == true;
        _settings.Save();
    }
}