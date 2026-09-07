using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpMonoInjector;

namespace Dusk.Launcher.Injection;

/// <summary>
/// Injects the SentinelMenu assembly into the Gorilla Tag Unity (mono)
/// runtime using SharpMonoInjector, and launches the game via Steam where
/// needed.
/// </summary>
public static class DuskInjector
{
    public const string MenuDllName  = "SentinelMenu.dll";
    public const string MenuNamespace = "SentinelMenu";
    public const string MenuClass    = "Plugin";
    public const string MenuMethod   = "Inject";

    public sealed record InjectResult(bool Success, string Message);

    /// <summary>Launch Gorilla Tag through Steam (blocking until process exits is NOT done — returns immediately after start).</summary>
    public static void LaunchViaSteam()
    {
        // steam://rungameid/1533390 is Gorilla Tag's app id.
        Process.Start(new ProcessStartInfo("steam://rungameid/1533390")
        {
            UseShellExecute = true
        });
    }

    /// <summary>Start the game, then inject the menu when the process appears and is ready.</summary>
    public static async Task<InjectResult> LaunchAndLoadAsync(string menuDllPath, int timeoutSeconds = 120)
    {
        LaunchViaSteam();

        var sw = Stopwatch.StartNew();
        Process? game = null;
        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            game = FindGorillaTag();
            if (game != null)
                break;
            await Task.Delay(1000);
        }

        if (game == null)
            return new InjectResult(false, "Gorilla Tag did not start within the timeout.");

        // Give the mono runtime a moment to initialize.
        await Task.Delay(4000);
        return Inject(game, menuDllPath);
    }

    /// <summary>Inject into an already-running Gorilla Tag process.</summary>
    public static InjectResult Load(string menuDllPath)
    {
        var game = FindGorillaTag();
        if (game == null)
            return new InjectResult(false, "Gorilla Tag is not running. Start it first, or use Launch & Load.");
        return Inject(game, menuDllPath);
    }

    private static Process? FindGorillaTag() =>
        Process.GetProcessesByName("Gorilla Tag")
            .FirstOrDefault(p => !p.HasExited);

    private static InjectResult Inject(Process game, string menuDllPath)
    {
        if (!File.Exists(menuDllPath))
            return new InjectResult(false, $"Menu DLL not found at:\n{menuDllPath}");

        try
        {
            using var injector = new Injector(game.Id);
            byte[] assembly = File.ReadAllBytes(menuDllPath);
            injector.Inject(assembly, MenuNamespace, MenuClass, MenuMethod);
            return new InjectResult(true, "Injected. Menu is in-game — press Y to toggle.");
        }
        catch (Exception ex)
        {
            return new InjectResult(false,
                "Injection failed: " + ex.Message +
                "\n\nRun Dusk Launcher as administrator and make sure Gorilla Tag is running (Steam or Meta).");
        }
    }

    /// <summary>Elevates the current process so OpenProcess can grab the game.</summary>
    public static void RequireAdminOrPrompt()
    {
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        if (principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            return;

        var startInfo = new ProcessStartInfo
        {
            FileName = Process.GetCurrentProcess().MainModule?.FileName ?? "DuskLauncher.exe",
            Verb = "runas",
            UseShellExecute = true
        };
        try
        {
            Process.Start(startInfo);
            Environment.Exit(0);
        }
        catch
        {
            // User declined elevation; injections will fail gracefully with a hint.
        }
    }
}