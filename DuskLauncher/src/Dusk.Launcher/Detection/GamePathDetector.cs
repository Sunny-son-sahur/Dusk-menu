using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Dusk.Launcher.Detection;

/// <summary>
/// Locates the Gorilla Tag install for Steam and Meta (Oculus) builds.
/// </summary>
public static class GamePathDetector
{
    private const string GameExe        = "Gorilla Tag.exe";
    private const string GameFolderName = "Gorilla Tag";

    public enum Platform
    {
        Unknown,
        Steam,
        Meta
    }

    public sealed record GameInstall(string Path, Platform Platform);

    /// <summary>Auto-detect. Returns null when nothing is found.</summary>
    public static GameInstall? Detect()
    {
        // Steam path detection (registry + library folders).
        var steam = DetectSteam();
        if (steam != null)
            return steam;

        // Meta / Oculus install detection.
        var meta = DetectMeta();
        if (meta != null)
            return meta;

        return null;
    }

    private static GameInstall? DetectSteam()
    {
        string? steamInstallPath = RegistrySteamPath();
        if (string.IsNullOrEmpty(steamInstallPath))
            steamInstallPath = @"C:\Program Files (x86)\Steam";

        // Known default path first (fast path).
        var candidates = new System.Collections.Generic.List<string>
        {
            Path.Combine(steamInstallPath, "steamapps", "common", GameFolderName)
        };

        // Parse libraryfolders.vdf for extra install locations.
        string vdfPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            // Rough but effective: grab every quoted path in the vdf.
            foreach (Match m in Regex.Matches(File.ReadAllText(vdfPath), "\"path\"\\s+\"([^\"]+)\""))
            {
                string lib = m.Groups[1].Value.Replace("\\\\", "\\");
                candidates.Add(Path.Combine(lib, "steamapps", "common", GameFolderName));
            }
        }

        foreach (var candidate in candidates)
        {
            string exe = Path.Combine(candidate, GameExe);
            if (File.Exists(exe))
                return new GameInstall(candidate, Platform.Steam);
        }

        return null;
    }

    private static string? RegistrySteamPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Valve\Steam");
            return key?.GetValue("InstallPath") as string;
        }
        catch
        {
            return null;
        }
    }

    private static GameInstall? DetectMeta()
    {
        // Common Oculus PC install locations.
        string[] candidateRoots =
        {
            @"C:\Program Files\Oculus\Software\Software\another-axiom-gorilla-tag",
            @"C:\Program Files\Oculus\Software\another-axiom-gorilla-tag",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Oculus\Software\Software\another-axiom-gorilla-tag")
        };

        foreach (var root in candidateRoots)
        {
            string exe = Path.Combine(root, GameExe);
            if (File.Exists(exe))
                return new GameInstall(root, Platform.Meta);
        }

        return null;
    }
}