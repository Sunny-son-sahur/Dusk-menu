using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace Dusk.Installer;

/// <summary>
/// Wizard-style installer for Dusk Launcher.
///
/// Step 1 — Welcome: shows what will be installed, Install / Cancel.
/// Step 2 — Installing: progress bar, live status.
/// Step 3 — Done: "Launch Dusk Launcher" checkbox, Finish.
///
/// The launcher exe comes from one of two places:
///   1. A file bundled next to the installer (produced by the CI build).
///   2. The latest GitHub release of this repo (downloaded at runtime).
///
/// On Finish the installer schedules its own deletion (self-destruct).
/// </summary>
public sealed class InstallerForm : Form
{
    // ── Metadata / sources ────────────────────────────────────────────
    private const string InstallDir  = @"C:\Program Files\Dusk Launcher";
    private const string AppName     = "Dusk Launcher";
    private const string DesktopName = "Dusk Launcher.exe";
    private const string ExeName     = "DuskLauncher.exe";

    private const string RepoOwner    = "Sunny-son-sahur";
    private const string RepoName     = "Dusk-menu";
    private const string ReleasesApi  = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    private const string DownloadsUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest/download/DuskLauncher.zip";

    // ── Shared controls ───────────────────────────────────────────────
    private readonly Panel _welcomePanel;
    private readonly Panel _installingPanel;
    private readonly Panel _completePanel;

    // Welcome panel
    private readonly Label _welcomeTitle;
    private readonly Label _welcomeInfo;
    private readonly Button _installBtn;
    private readonly Button _cancelBtn;

    // Installing panel
    private readonly Label _installingTitle;
    private readonly Label _installingStatus;
    private readonly ProgressBar _progress;

    // Complete panel
    private readonly Label _completeTitle;
    private readonly Label _completeInfo;
    private readonly CheckBox _launchCheck;
    private readonly Button _finishBtn;

    private bool _launchOnClose;

    public InstallerForm()
    {
        // ── Form ──────────────────────────────────────────────────────
        Text = $"{AppName} Installer";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 340);
        BackColor = Color.FromArgb(18, 18, 22);
        ForeColor = Color.White;
        ShowInTaskbar = false;

        // ── Welcome panel ─────────────────────────────────────────────
        _welcomePanel = new Panel { Dock = DockStyle.Fill, Visible = true };

        _welcomeTitle = new Label
        {
            Text = AppName,
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 220, 228),
            AutoSize = true,
            Location = new Point(30, 24)
        };
        _welcomePanel.Controls.Add(_welcomeTitle);

        _welcomeInfo = new Label
        {
            Text = $"Setup will install {AppName} to:\n\n" +
                   $"    {InstallDir}\n\n" +
                   $"A shortcut will be placed on your desktop.\n\n" +
                   $"Click Install to continue.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(160, 160, 170),
            Location = new Point(30, 76),
            Size = new Size(420, 160)
        };
        _welcomePanel.Controls.Add(_welcomeInfo);

        _installBtn = MakeButton("Install", 220, true);
        _installBtn.Click += async (_, _) => await StartInstall();
        _welcomePanel.Controls.Add(_installBtn);

        _cancelBtn = MakeButton("Cancel", 340, false);
        _cancelBtn.Click += (_, _) => Close();
        _welcomePanel.Controls.Add(_cancelBtn);

        // ── Installing panel ──────────────────────────────────────────
        _installingPanel = new Panel { Dock = DockStyle.Fill, Visible = false };

        _installingTitle = new Label
        {
            Text = "Installing…",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 220, 228),
            AutoSize = true,
            Location = new Point(30, 24)
        };
        _installingPanel.Controls.Add(_installingTitle);

        _progress = new ProgressBar
        {
            Location = new Point(30, 90),
            Size = new Size(420, 22),
            Style = ProgressBarStyle.Continuous,
            ForeColor = Color.FromArgb(0, 170, 255),
            BackColor = Color.FromArgb(30, 30, 38)
        };
        _installingPanel.Controls.Add(_progress);

        _installingStatus = new Label
        {
            Text = "Preparing…",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(120, 120, 132),
            Location = new Point(30, 120),
            AutoSize = true
        };
        _installingPanel.Controls.Add(_installingStatus);

        // ── Complete panel ────────────────────────────────────────────
        _completePanel = new Panel { Dock = DockStyle.Fill, Visible = false };

        _completeTitle = new Label
        {
            Text = "Setup Complete",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 200, 100),
            AutoSize = true,
            Location = new Point(30, 24)
        };
        _completePanel.Controls.Add(_completeTitle);

        _completeInfo = new Label
        {
            Text = $"{AppName} has been installed to:\n\n" +
                   $"    {InstallDir}\n\n" +
                   $"A shortcut has been placed on your desktop.",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(160, 160, 170),
            Location = new Point(30, 72),
            Size = new Size(420, 100)
        };
        _completePanel.Controls.Add(_completeInfo);

        _launchCheck = new CheckBox
        {
            Text = $"Launch {AppName}",
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(200, 200, 210),
            Checked = true,
            Location = new Point(30, 180),
            AutoSize = true
        };
        _completePanel.Controls.Add(_launchCheck);

        _finishBtn = MakeButton("Finish", 340, true);
        _finishBtn.Text = "Finish";
        _finishBtn.Click += (_, _) => FinishAndClose();
        _completePanel.Controls.Add(_finishBtn);

        // ── Add panels ────────────────────────────────────────────────
        Controls.Add(_completePanel);
        Controls.Add(_installingPanel);
        Controls.Add(_welcomePanel);
    }

    // ── Install flow ──────────────────────────────────────────────────

    private async Task StartInstall()
    {
        _installBtn.Enabled = false;
        _cancelBtn.Enabled = false;

        ShowPanel(_installingPanel);

        try
        {
            Directory.CreateDirectory(InstallDir);
            SetStatus("Preparing…", 5);

            string launcherZip = await ObtainLauncherZip();
            SetStatus("Extracting launcher…", 45);
            ExtractZip(launcherZip, InstallDir, "DuskLauncher/");

            SetStatus("Creating desktop shortcut…", 75);
            PlaceDesktopShortcut();

            SetStatus("Done", 100);
            await Task.Delay(400); // let the progress bar sit at 100 briefly

            ShowPanel(_completePanel);
        }
        catch (Exception ex)
        {
            SetStatus("Install failed.", 0);
            MessageBox.Show("Install failed:\n" + ex.Message,
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowPanel(_welcomePanel);
            _installBtn.Enabled = true;
            _cancelBtn.Enabled = true;
        }
    }

    private void FinishAndClose()
    {
        _launchOnClose = _launchCheck.Checked;

        // If the user wants to launch, start it before we delete ourselves.
        if (_launchOnClose)
        {
            string launcherExe = Path.Combine(InstallDir, ExeName);
            if (File.Exists(launcherExe))
            {
                Process.Start(new ProcessStartInfo(launcherExe)
                {
                    UseShellExecute = true
                });
            }
        }

        // Schedule self-deletion then exit.
        ScheduleSelfDelete();
        Close();
    }

    // ── Obtain the zip ────────────────────────────────────────────────

    private async Task<string> ObtainLauncherZip()
    {
        // 1. Bundled copy (CI puts it next to the installer).
        string? bundled = Directory
            .GetFiles(AppContext.BaseDirectory, "DuskLauncher.zip")
            .FirstOrDefault();
        if (bundled != null)
        {
            SetStatus("Using bundled launcher…", 20);
            return bundled;
        }

        // 2. Download from latest release.
        SetStatus("Downloading from GitHub…", 15);
        var tmpZip = Path.Combine(Path.GetTempPath(), "DuskLauncher.zip");
        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DuskInstaller/2.0");
            using var stream = await http.GetStreamAsync(DownloadsUrl);
            using var file = File.Create(tmpZip);
            await stream.CopyToAsync(file);
        }
        return tmpZip;
    }

    // ── Extract ───────────────────────────────────────────────────────

    private static void ExtractZip(string zipPath, string destination, string? stripPrefix)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/'))
                continue;

            string relative = entry.FullName;
            if (stripPrefix != null &&
                relative.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
                relative = relative[stripPrefix.Length..];

            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    // ── Desktop shortcut ──────────────────────────────────────────────

    private void PlaceDesktopShortcut()
    {
        string launcherExe = Path.Combine(InstallDir, ExeName);
        if (!File.Exists(launcherExe))
            throw new FileNotFoundException($"{ExeName} was not produced by the install.", launcherExe);

        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        // Copy exe to desktop.
        string desktopExe = Path.Combine(desktopDir, DesktopName);
        File.Copy(launcherExe, desktopExe, overwrite: true);

        // Create .lnk shortcut.
        string lnkPath = Path.Combine(desktopDir, AppName + ".lnk");
        CreateShortcut(lnkPath, launcherExe, InstallDir);
    }

    private static void CreateShortcut(string lnkPath, string targetPath, string workingDir)
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

    // ── Self-delete ───────────────────────────────────────────────────

    private static void ScheduleSelfDelete()
    {
        string exePath = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return;

        // Spawn a cmd process that waits 2 seconds then deletes this exe.
        // The short delay ensures the file handle is released after exit.
        string escaped = exePath.Replace("\"", "\\\"");
        string args = $"/c timeout /t 2 /nobreak >nul & del \"{escaped}\" & rmdir \"{Path.GetDirectoryName(exePath)}\" 2>nul & exit";

        Process.Start(new ProcessStartInfo("cmd", args)
        {
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void ShowPanel(Panel target)
    {
        _welcomePanel.Visible = target == _welcomePanel;
        _installingPanel.Visible = target == _installingPanel;
        _completePanel.Visible = target == _completePanel;
    }

    private void SetStatus(string text, int percent)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(text, percent));
            return;
        }
        _installingStatus.Text = text;
        _progress.Value = Math.Clamp(percent, 0, 100);
    }

    private Button MakeButton(string label, int x, bool primary)
    {
        return new Button
        {
            Text = label,
            Size = new Size(110, 34),
            Location = new Point(x, 290),
            BackColor = primary ? Color.FromArgb(45, 45, 54) : Color.FromArgb(30, 30, 38),
            ForeColor = primary ? Color.White : Color.FromArgb(180, 180, 190),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, primary ? FontStyle.Bold : FontStyle.Regular),
            Cursor = Cursors.Hand,
            FlatAppearance =
            {
                BorderColor = primary ? Color.FromArgb(0, 170, 255) : Color.FromArgb(60, 60, 70)
            }
        };
    }
}