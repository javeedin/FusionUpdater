using FusionClientUpdater.Models;
using FusionClientUpdater.Services;

namespace FusionClientUpdater.Forms;

public class MainForm : Form
{
    private const string UploadPassword = "Gitupload";

    private readonly SettingsService _settingsService = new();
    private readonly GitHubService _gitHubService = new();
    private readonly UpdaterService _updaterService = new();

    private AppSettings _settings = new();
    private ReleaseInfo? _latestRelease;

    // Header
    private Panel _headerPanel = null!;

    // Top toolbar
    private Panel _toolbarPanel = null!;

    // Status group
    private GroupBox _statusGroup = null!;
    private Label _installedVersionLabel = null!;
    private Label _latestVersionLabel = null!;

    // Update actions
    private Button _checkButton = null!;
    private Button _downloadOnlyButton = null!;
    private Button _downloadButton = null!;
    private ProgressBar _downloadProgress = null!;
    private ProgressBar _installProgress = null!;
    private Label _statusLabel = null!;

    // Settings
    private GroupBox _settingsGroup = null!;
    private TextBox _installPathBox = null!;
    private TextBox _processBox = null!;
    private TextBox _appExeBox = null!;
    private Button _saveSettingsButton = null!;

    private RichTextBox _logBox = null!;

    // Tray
    private NotifyIcon _notifyIcon = null!;
    private bool _reallyExit;

    public MainForm()
    {
        InitializeComponentManual();
        LoadSettingsToUi();
        UpdateVersionLabels();
    }

    private void InitializeComponentManual()
    {
        Text = "Fusion Client Updater";
        ClientSize = new Size(600, 680);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        // ── Header ──────────────────────────────────────────────
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = ColorTranslator.FromHtml("#1e3a5f")
        };
        _headerPanel.Controls.Add(new Label
        {
            Text = "Fusion Client Updater",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 10)
        });
        _headerPanel.Controls.Add(new Label
        {
            Text = "Download and install the latest Fusion client",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Location = new Point(22, 42)
        });

        // ── Toolbar (Launch + Upload) ────────────────────────────
        _toolbarPanel = new Panel
        {
            Location = new Point(0, 70),
            Size = new Size(600, 48),
            BackColor = ColorTranslator.FromHtml("#f0f4f8")
        };

        var launchBtn = new Button
        {
            Text = "▶  Launch Application",
            Location = new Point(12, 8),
            Size = new Size(180, 32),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = ColorTranslator.FromHtml("#1e3a5f"),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        launchBtn.FlatAppearance.BorderSize = 0;
        launchBtn.Click += (s, e) => LaunchApplication();

        var uploadBtn = new Button
        {
            Text = "⬆  Open Upload Tool",
            Location = new Point(202, 8),
            Size = new Size(170, 32),
            Font = new Font("Segoe UI", 9F),
            FlatStyle = FlatStyle.Flat,
            BackColor = ColorTranslator.FromHtml("#e8eef5")
        };
        uploadBtn.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#1e3a5f");
        uploadBtn.Click += (s, e) => OpenUploadTool();

        _toolbarPanel.Controls.Add(launchBtn);
        _toolbarPanel.Controls.Add(uploadBtn);

        // ── Current Status ───────────────────────────────────────
        _statusGroup = new GroupBox
        {
            Text = "Current Status",
            Location = new Point(20, 130),
            Size = new Size(560, 80),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        _installedVersionLabel = new Label
        {
            Text = "Installed Version: -",
            Location = new Point(15, 24),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F)
        };
        _latestVersionLabel = new Label
        {
            Text = "Latest Version: (not checked)",
            Location = new Point(15, 50),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F)
        };
        _statusGroup.Controls.Add(_installedVersionLabel);
        _statusGroup.Controls.Add(_latestVersionLabel);

        // ── Update Buttons ───────────────────────────────────────
        _checkButton = new Button
        {
            Text = "Check for Update",
            Location = new Point(20, 225),
            Size = new Size(155, 34),
            Font = new Font("Segoe UI", 9F)
        };
        _checkButton.Click += async (s, e) => await CheckForUpdateAsync();

        _downloadOnlyButton = new Button
        {
            Text = "Download Only",
            Location = new Point(185, 225),
            Size = new Size(130, 34),
            Enabled = false,
            Font = new Font("Segoe UI", 9F)
        };
        _downloadOnlyButton.Click += async (s, e) => await DownloadOnlyAsync();

        _downloadButton = new Button
        {
            Text = "Download && Install",
            Location = new Point(325, 225),
            Size = new Size(155, 34),
            Enabled = false,
            Font = new Font("Segoe UI", 9F)
        };
        _downloadButton.Click += async (s, e) => await DownloadAndInstallAsync();

        // ── Progress ─────────────────────────────────────────────
        var dlLabel = new Label { Text = "Download progress:", Location = new Point(20, 272), AutoSize = true };
        _downloadProgress = new ProgressBar { Location = new Point(20, 292), Size = new Size(560, 20) };

        var instLabel = new Label { Text = "Install progress:", Location = new Point(20, 320), AutoSize = true };
        _installProgress = new ProgressBar { Location = new Point(20, 340), Size = new Size(560, 20) };

        _statusLabel = new Label
        {
            Text = "Ready.",
            Location = new Point(20, 368),
            AutoSize = true,
            ForeColor = ColorTranslator.FromHtml("#1e3a5f"),
            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
        };

        // ── Settings ─────────────────────────────────────────────
        _settingsGroup = new GroupBox
        {
            Text = "Settings",
            Location = new Point(20, 392),
            Size = new Size(560, 165),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        var rf = new Font("Segoe UI", 9F, FontStyle.Regular);
        int lx = 15, bx = 135, bw = 400;

        _settingsGroup.Controls.Add(new Label { Text = "Install Path:", Location = new Point(lx, 30), AutoSize = true, Font = rf });
        _installPathBox = new TextBox { Location = new Point(bx, 27), Size = new Size(bw, 24), Font = rf };
        _settingsGroup.Controls.Add(_installPathBox);

        _settingsGroup.Controls.Add(new Label { Text = "Process Name:", Location = new Point(lx, 62), AutoSize = true, Font = rf });
        _processBox = new TextBox { Location = new Point(bx, 59), Size = new Size(bw, 24), Font = rf };
        _settingsGroup.Controls.Add(_processBox);

        _settingsGroup.Controls.Add(new Label { Text = "App EXE Path:", Location = new Point(lx, 94), AutoSize = true, Font = rf });
        _appExeBox = new TextBox { Location = new Point(bx, 91), Size = new Size(bw, 24), Font = rf };
        _settingsGroup.Controls.Add(_appExeBox);

        _settingsGroup.Controls.Add(new Label
        {
            Text = "Relative to Install Path, e.g. fusionclientweb\\graysWMSwebviewnew\\dist\\GraysWMS.exe",
            Location = new Point(bx, 117),
            AutoSize = true,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Italic),
            ForeColor = Color.Gray
        });

        _saveSettingsButton = new Button { Text = "Save Settings", Location = new Point(bx, 128), Size = new Size(120, 28), Font = rf };
        _saveSettingsButton.Click += (s, e) => SaveSettingsFromUi();
        _settingsGroup.Controls.Add(_saveSettingsButton);

        // ── Log ──────────────────────────────────────────────────
        var logLabel = new Label { Text = "Log:", Location = new Point(20, 566), AutoSize = true, Font = rf };
        _logBox = new RichTextBox
        {
            Location = new Point(20, 584),
            Size = new Size(560, 82),
            ReadOnly = true,
            BackColor = Color.WhiteSmoke,
            Font = new Font("Consolas", 8F),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        // ── Add all controls ─────────────────────────────────────
        Controls.Add(_logBox);
        Controls.Add(logLabel);
        Controls.Add(_settingsGroup);
        Controls.Add(_statusLabel);
        Controls.Add(instLabel);
        Controls.Add(_installProgress);
        Controls.Add(dlLabel);
        Controls.Add(_downloadProgress);
        Controls.Add(_downloadButton);
        Controls.Add(_downloadOnlyButton);
        Controls.Add(_checkButton);
        Controls.Add(_statusGroup);
        Controls.Add(_toolbarPanel);
        Controls.Add(_headerPanel);

        // ── Tray ─────────────────────────────────────────────────
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show", null, (s, e) => RestoreFromTray());
        trayMenu.Items.Add("Launch Application", null, (s, e) => LaunchApplication());
        trayMenu.Items.Add("Check for Update", null, async (s, e) => { RestoreFromTray(); await CheckForUpdateAsync(); });
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (s, e) => { _reallyExit = true; Close(); });

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Fusion Client Updater",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();

        Resize += MainForm_Resize;
        FormClosing += MainForm_FormClosing;
    }

    // ── Upload Tool with password ────────────────────────────────
    private void OpenUploadTool()
    {
        if (!_settings.UploadPasswordSaved)
        {
            using var pwdForm = new PasswordDialog();
            if (pwdForm.ShowDialog(this) != DialogResult.OK) return;

            if (pwdForm.EnteredPassword != UploadPassword)
            {
                MessageBox.Show(this, "Incorrect password.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _settings.UploadPasswordSaved = true;
            _settingsService.Save(_settings);
        }

        using var form = new UploadForm(_settings, _gitHubService);
        form.ShowDialog(this);
    }

    // ── Tray / Window ────────────────────────────────────────────
    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
            _notifyIcon.ShowBalloonTip(1000, "Fusion Client Updater",
                "Still running in the system tray.", ToolTipIcon.Info);
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            return;
        }
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    // ── Settings ─────────────────────────────────────────────────
    private void LoadSettingsToUi()
    {
        _settings = _settingsService.Load();
        _installPathBox.Text = _settings.InstallPath;
        _processBox.Text = _settings.ProcessToKill;
        _appExeBox.Text = _settings.AppExePath;
    }

    private void SaveSettingsFromUi()
    {
        _settings.InstallPath = _installPathBox.Text.Trim();
        _settings.ProcessToKill = _processBox.Text.Trim();
        _settings.AppExePath = _appExeBox.Text.Trim();

        try
        {
            _settingsService.Save(_settings);
            MessageBox.Show(this, "Settings saved.", "Fusion Client Updater",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save settings:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateVersionLabels()
    {
        _installedVersionLabel.Text = $"Installed Version: {_settings.InstalledVersion}";
        _latestVersionLabel.Text = _latestRelease != null
            ? $"Latest Version: {_latestRelease.TagName}"
            : "Latest Version: (not checked)";
    }

    // ── Launch ───────────────────────────────────────────────────
    private void LaunchApplication()
    {
        var exePath = Path.IsPathRooted(_settings.AppExePath)
            ? _settings.AppExePath
            : Path.Combine(_settings.InstallPath, _settings.AppExePath);

        if (!File.Exists(exePath))
        {
            MessageBox.Show(this,
                $"Application not found at:\n{exePath}\n\nPlease check the App EXE Path in Settings.",
                "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            });
            Log($"Launched: {exePath}");
        }
        catch (Exception ex)
        {
            Log($"ERROR launching: {ex.Message}");
            MessageBox.Show(this, $"Could not launch the application:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Log ──────────────────────────────────────────────────────
    private void Log(string message)
    {
        if (_logBox.InvokeRequired) { _logBox.Invoke(new Action(() => Log(message))); return; }
        _logBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        _logBox.ScrollToCaret();
    }

    // ── Version parsing ──────────────────────────────────────────
    private static Version ParseVersion(string raw)
    {
        var s = (raw ?? "").Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
        if (s.StartsWith(".")) s = s[1..];
        return Version.TryParse(s, out var v) ? v : new Version(0, 0, 0);
    }

    // ── Check for update ─────────────────────────────────────────
    private async Task CheckForUpdateAsync()
    {
        SetBusy(true);
        _statusLabel.Text = "Checking for updates...";
        Log($"Checking {AppConstants.GitHubOwner}/{AppConstants.GitHubRepo}...");
        try
        {
            _latestRelease = await _gitHubService.GetLatestRelease(
                AppConstants.GitHubOwner, AppConstants.GitHubRepo, AppConstants.AssetName);

            UpdateVersionLabels();
            Log($"Latest: {_latestRelease.TagName} | Asset: {_latestRelease.HasAsset}");

            var installed = ParseVersion(_settings.InstalledVersion);
            var latest = ParseVersion(_latestRelease.TagName);

            if (latest > installed)
            {
                _downloadButton.Enabled = _latestRelease.HasAsset;
                _downloadOnlyButton.Enabled = _latestRelease.HasAsset;
                _statusLabel.Text = $"Update available: {_latestRelease.TagName}";

                if (_latestRelease.HasAsset)
                {
                    var result = MessageBox.Show(this,
                        $"A newer version is available!\n\nInstalled: {_settings.InstalledVersion}\n" +
                        $"Latest: {_latestRelease.TagName}\n\nDownload and install now?",
                        "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        SetBusy(false);
                        await DownloadAndInstallAsync();
                        return;
                    }
                }
            }
            else
            {
                _downloadButton.Enabled = false;
                _downloadOnlyButton.Enabled = false;
                _statusLabel.Text = "You are up to date.";
                MessageBox.Show(this, "You already have the latest version installed.",
                    "Up To Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Check failed.";
            Log($"ERROR: {ex.GetType().Name}: {ex.Message}");
            MessageBox.Show(this, $"Could not check for updates:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); }
    }

    // ── Download only ────────────────────────────────────────────
    private async Task DownloadOnlyAsync()
    {
        if (_latestRelease == null || !_latestRelease.HasAsset) return;

        using var save = new SaveFileDialog
        {
            Title = "Save downloaded file",
            FileName = AppConstants.AssetName,
            Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*"
        };
        if (save.ShowDialog(this) != DialogResult.OK) return;

        SetBusy(true);
        _downloadProgress.Value = 0;
        try
        {
            _statusLabel.Text = "Downloading...";
            Log($"Downloading to {save.FileName}...");
            var prog = new Progress<int>(p => _downloadProgress.Value = Math.Clamp(p, 0, 100));
            await _gitHubService.DownloadAsset(_latestRelease.AssetUrl, save.FileName, prog);
            _statusLabel.Text = "Download complete.";
            Log($"Saved to {save.FileName}");
            MessageBox.Show(this, $"File saved to:\n{save.FileName}", "Download Complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Download failed.";
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(this, $"Download failed:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); }
    }

    // ── Download & Install ───────────────────────────────────────
    private async Task DownloadAndInstallAsync()
    {
        if (_latestRelease == null || !_latestRelease.HasAsset) return;

        SetBusy(true);
        _downloadProgress.Value = 0;
        _installProgress.Value = 0;

        var tempZip = Path.Combine(Path.GetTempPath(), $"fusion_{Guid.NewGuid():N}.zip");
        try
        {
            _statusLabel.Text = "Downloading...";
            var dlProg = new Progress<int>(p => _downloadProgress.Value = Math.Clamp(p, 0, 100));
            await _gitHubService.DownloadAsset(_latestRelease.AssetUrl, tempZip, dlProg);

            _statusLabel.Text = "Installing...";
            var instProg = new Progress<int>(p => _installProgress.Value = Math.Clamp(p, 0, 100));
            var zip = tempZip;
            var dest = Path.Combine(_settings.InstallPath, AppConstants.ExtractSubfolder);
            var proc = _settings.ProcessToKill;
            await Task.Run(() => _updaterService.InstallUpdate(zip, dest, proc, instProg));

            _settings.InstalledVersion = _latestRelease.TagName;
            _settingsService.Save(_settings);
            UpdateVersionLabels();

            _statusLabel.Text = $"Installed {_latestRelease.TagName} successfully.";
            _downloadButton.Enabled = false;
            _downloadOnlyButton.Enabled = false;
            MessageBox.Show(this,
                $"Update installed successfully!\n\nVersion {_latestRelease.TagName} is now at {_settings.InstallPath}.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Install failed.";
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(this, $"Install failed:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _checkButton.Enabled = !busy;
        _saveSettingsButton.Enabled = !busy;
        if (busy) { _downloadButton.Enabled = false; _downloadOnlyButton.Enabled = false; }
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }
}
