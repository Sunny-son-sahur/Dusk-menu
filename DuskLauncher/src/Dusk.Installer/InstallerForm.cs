using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace Dusk.Installer;

/// <summary>
/// Dark first-run installer. Installs Dusk Launcher into
/// C:\Program Files\Dusk Launcher, drops a copy of the launcher on the
/// desktop, and creates a desktop shortcut.
///
/// The launcher exe comes from one of two places:
///   1. A file bundled next to the installer (produced by the CI build).
///   2. The latest GitHub release of this repo (downloaded at runtime).
/// </summary>
public sealed class InstallerForm : Form
{
    // ── Metadata / sources ────────────────────────────────────────────
    private const string InstallDir   = @"C:\Program Files\Dusk Launcher";
    private const string AppName      = "Dusk Launcher";
    private const string DesktopName  = "Dusk Launcher.exe";

    // Fill in once the repo + releases exist.
    private const string RepoOwner = "Sunny-son-sahur";
    private const string RepoName  = "Dusk-menu";
    private const string ReleasesApi = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    private const string DownloadsUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest/download/DuskLauncher.zip";

    // ── UI controls ────────────────────────────────────────────────────
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progress = null!;
    private Button _installButton = null!;
    private Button _closeButton = null!;

    public InstallerForm()
    {
        BuildUi();
    }

    private void BuildUi()
    {
        Text = AppName + " Installer";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 340);
        BackColor = Color.FromArgb(18, 18, 22);
        ForeColor = Color.White;

        _titleLabel = new Label
        {
            Text = AppName,
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 220, 228),
            AutoSize = true,
            Location = new Point(28, 28)
        };
        Controls.Add(_titleLabel);

        _subtitleLabel = new Label
        {
            Text = "Gorilla Tag mod menu — launcher and injector.",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(150, 150, 160),
            AutoSize = true,
            Location = new Point(30, 78)
        };
        Controls.Add(_subtitleLabel);

        var infoLabel = new Label
        {
            Text = $@"This will install Dusk Launcher to:
{InstallDir}

A copy of the launcher will also be placed on
your desktop so you can start it from there.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(170, 170, 180),
            Location = new Point(30, 118),
            AutoSize = true
        };
        Controls.Add(infoLabel);

        _progress = new ProgressBar
        {
            Location = new Point(30, 236),
            Size = new Size(400, 18),
            Style = ProgressBarStyle.Continuous,
            ForeColor = Color.FromArgb(0, 170, 255),
            BackColor = Color.FromArgb(30, 30, 38)
        };
        Controls.Add(_progress);

        _statusLabel = new Label
        {
            Text = "Ready",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(120, 120, 132),
            Location = new Point(30, 260),
            AutoSize = true
        };
        Controls.Add(_statusLabel);

        _installButton = new Button
        {
            Text = "Install",
            Size = new Size(110, 34),
            Location = new Point(238, 292),
            BackColor = Color.FromArgb(45, 45, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _installButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 82);
        _installButton.Click += async (_, _) => await RunInstall();
        Controls.Add(_installButton);

        _closeButton = new Button
        {
            Text = "Close",
            Size = new Size(110, 34),
            Location = new Point(354, 292),
            BackColor = Color.FromArgb(30, 30, 38),
            ForeColor = Color.FromArgb(180, 180, 190),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f),
            Cursor = Cursors.Hand
        };
        _closeButton.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 70);
        _closeButton.Click += (_, _) => Close();
        Controls.Add(_closeButton);
    }

    private async Task RunInstall()
    {
        _installButton.Enabled = false;
        try
        {
            Directory.CreateDirectory(InstallDir);
            SetStatus("Preparing...", 2);

            string launcherZip = await ObtainLauncherZip();
            SetStatus("Extracting launcher...", 40);
            ExtractZip(launcherZip, InstallDir, "DuskLauncher/");

            SetStatus("Placing desktop shortcut...", 72);
            PlaceDesktopShortcut();

            SetStatus("Done.", 100);
            MessageBox.Show(
                $"{AppName} installed to {InstallDir}.\n\n" +
                "Launch it from the shortcut on your desktop.",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            _closeButton.Text = "Finish";
        }
        catch (Exception ex)
        {
            SetStatus("Install failed.", 0);
            MessageBox.Show("Install failed:\n" + ex.Message,
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _installButton.Enabled = true;
        }
    }

    /// <summary>Returns a path to a zip containing the launcher.</summary>
    private async Task<string> ObtainLauncherZip()
    {
        // Prefer a locally bundled copy (CI copies it next to the installer).
        string? bundled = Directory
            .GetFiles(AppContext.BaseDirectory, "DuskLauncher.zip")
            .FirstOrDefault();
        if (bundled != null)
        {
            SetStatus("Using bundled launcher...", 20);
            return bundled;
        }

        // Fallback: download the latest release.
        SetStatus("Downloading launcher from GitHub...", 12);
        var tmpZip = Path.Combine(Path.GetTempPath(), "DuskLauncher.zip");
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DuskInstaller/1.0");
        using var stream = await http.GetStreamAsync(DownloadsUrl);
        using var file = File.Create(tmpZip);
        await stream.CopyToAsync(file);
        return tmpZip;
    }

    private static void ExtractZip(string zipPath, string destination, string stripPrefix)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/'))
                continue;

            string relative = entry.FullName;
            if (stripPrefix != null && relative.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
                relative = relative[stripPrefix.Length..];

            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private void PlaceDesktopShortcut()
    {
        string launcherExe = Path.Combine(InstallDir, "DuskLauncher.exe");
        if (!File.Exists(launcherExe))
            throw new FileNotFoundException("DuskLauncher.exe was not produced by the install.", launcherExe);

        // Copy to the desktop so it can be launched directly from there.
        string desktopExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DesktopName);
        File.Copy(launcherExe, desktopExe, overwrite: true);

        // Also make a .lnk shortcut to the installed copy.
        var shell = new Shell32();
        shell.CreateShortcut(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"),
            launcherExe,
            InstallDir);
    }

    private void SetStatus(string text, int percent)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(text, percent));
            return;
        }
        _statusLabel.Text = text;
        _progress.Value = Math.Clamp(percent, 0, 100);
    }
}

/// <summary>Creates a .lnk shortcut via the Windows Script Host COM object.</summary>
internal sealed class Shell32
{
    public void CreateShortcut(string lnkPath, string targetPath, string workingDir)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(lnkPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDir;
        shortcut.Save();
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
    }
}