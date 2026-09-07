using System;
using System.IO;
using System.Text.Json;

namespace Dusk.Launcher;

/// <summary>Persistent launcher settings stored in %APPDATA%\Dusk Launcher\settings.json.</summary>
public sealed class Settings
{
    public string? GamePath { get; set; }
    public string? GamePlatform { get; set; } // "Steam" | "Meta" | "Custom"
    public string? MenuDllPath { get; set; }
    public bool AutoDetectOnLaunch { get; set; } = true;
    public bool LaunchOnStart { get; set; }
    public string? AccentHex { get; set; }

    private static readonly string Path_ = Path.Combine(App.DataDir, "settings.json");

    public void Save() =>
        File.WriteAllText(Path_, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));

    public static Settings Load()
    {
        try
        {
            if (File.Exists(Path_))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path_)) ?? new Settings();
        }
        catch { }
        return new Settings();
    }

    /// <summary>Convenience: installed menu DLL lives beside the launcher by default.</summary>
    public string ResolveMenuDllPath()
    {
        if (!string.IsNullOrEmpty(MenuDllPath) && File.Exists(MenuDllPath))
            return MenuDllPath;

        // Look next to the running exe first, then in the app data dir.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, Injection.DuskInjector.MenuDllName),
            Path.Combine(App.DataDir, Injection.DuskInjector.MenuDllName)
        };
        foreach (var c in candidates)
            if (File.Exists(c))
                return c;
        return candidates[0]; // best guess
    }
}