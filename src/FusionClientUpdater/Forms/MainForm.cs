using FusionClientUpdater.Models;
using FusionClientUpdater.Services;

namespace FusionClientUpdater.Forms;

public class MainForm : Form
{
    private readonly SettingsService _settingsService = new();
    private readonly GitHubService _gitHubService = new();
    private readonly UpdaterService _updaterService = new();

    private AppSettings _settings = new();
    private ReleaseInfo? _latestRelease;

    // Header
    private Panel _headerPanel = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;

    // Status group
    private GroupBox _statusGroup = null!;
    private Label _installedVersionLabel = null!;
    private Label _latestVersionLabel = null!;

    // Actions
    private Button _checkButton = null!;
    private Button _downloadButton = null!;
    private ProgressBar _downloadProgress = null!;
    private ProgressBar _installProgress = null!;
    private Label _statusLabel = null!;

    // Settings group (end-user only: install path + process name)
    private GroupBox _settingsGroup = null!;
    private TextBox _installPathBox = null!;
    private TextBox _processBox = null!;
    private Button _saveSettingsButton = null!;

    private Button _uploadButton = null!;

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
        ClientSize = new Size(600, 560);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        // Header
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = ColorTranslator.FromHtml("#1e3a5f"),
            Padding = new Padding(20, 10, 20, 10)
        };
        _titleLabel = new Label
        {
            Text = "Fusion Client Updater",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 10)
        };
        _subtitleLabel = new Label
        {
            Text = "Download and install the latest Fusion client",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Location = new Point(22, 42)
        };
        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);

        // Status group
        _statusGroup = new GroupBox
        {
            Text = "Current Status",
            Location = new Point(20, 85),
            Size = new Size(560, 90),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        _installedVersionLabel = new Label
        {
            Text = "Installed Version: -",
            Location = new Point(15, 28),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular)
        };
        _latestVersionLabel = new Label
        {
            Text = "Latest Version: (not checked)",
            Location = new Point(15, 55),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular)
        };
        _statusGroup.Controls.Add(_installedVersionLabel);
        _statusGroup.Controls.Add(_latestVersionLabel);

        // Buttons row
        _checkButton = new Button
        {
            Text = "Check for Update",
            Location = new Point(20, 190),
            Size = new Size(160, 36),
            Font = new Font("Segoe UI", 9F)
        };
        _checkButton.Click += async (s, e) => await CheckForUpdateAsync();

        _downloadButton = new Button
        {
            Text = "Download && Install",
            Location = new Point(195, 190),
            Size = new Size(160, 36),
            Enabled = false,
            Font = new Font("Segoe UI", 9F)
        };
        _downloadButton.Click += async (s, e) => await DownloadAndInstallAsync();

        _uploadButton = new Button
        {
            Text = "Open Upload Tool",
            Location = new Point(420, 190),
            Size = new Size(160, 36),
            Font = new Font("Segoe UI", 9F)
        };
        _uploadButton.Click += (s, e) =>
        {
            using var form = new UploadForm(_settings, _gitHubService);
            form.ShowDialog(this);
        };

        var dlLabel = new Label { Text = "Download progress:", Location = new Point(20, 240), AutoSize = true };
        _downloadProgress = new ProgressBar { Location = new Point(20, 260), Size = new Size(560, 22) };

        var instLabel = new Label { Text = "Install progress:", Location = new Point(20, 292), AutoSize = true };
        _installProgress = new ProgressBar { Location = new Point(20, 312), Size = new Size(560, 22) };

        _statusLabel = new Label
        {
            Text = "Ready.",
            Location = new Point(20, 344),
            AutoSize = true,
            ForeColor = ColorTranslator.FromHtml("#1e3a5f"),
            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
        };

        // Settings group — only install path & process name for end users
        _settingsGroup = new GroupBox
        {
            Text = "Settings",
            Location = new Point(20, 374),
            Size = new Size(560, 140),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        var rf = new Font("Segoe UI", 9F, FontStyle.Regular);
        int lx = 15, bx = 140, bw = 390;

        _settingsGroup.Controls.Add(new Label { Text = "Install Path:", Location = new Point(lx, 33), AutoSize = true, Font = rf });
        _installPathBox = new TextBox { Location = new Point(bx, 30), Size = new Size(bw, 24), Font = rf };
        _settingsGroup.Controls.Add(_installPathBox);

        _settingsGroup.Controls.Add(new Label { Text = "Process Name:", Location = new Point(lx, 65), AutoSize = true, Font = rf });
        _processBox = new TextBox { Location = new Point(bx, 62), Size = new Size(bw, 24), Font = rf };
        _settingsGroup.Controls.Add(_processBox);

        _saveSettingsButton = new Button { Text = "Save Settings", Location = new Point(bx, 97), Size = new Size(130, 30), Font = rf };
        _saveSettingsButton.Click += (s, e) => SaveSettingsFromUi();
        _settingsGroup.Controls.Add(_saveSettingsButton);

        // Add all to form
        Controls.Add(_settingsGroup);
        Controls.Add(_statusLabel);
        Controls.Add(instLabel);
        Controls.Add(_installProgress);
        Controls.Add(dlLabel);
        Controls.Add(_downloadProgress);
        Controls.Add(_uploadButton);
        Controls.Add(_downloadButton);
        Controls.Add(_checkButton);
        Controls.Add(_statusGroup);
        Controls.Add(_headerPanel);

        // Tray icon
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show", null, (s, e) => RestoreFromTray());
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

    private void LoadSettingsToUi()
    {
        _settings = _settingsService.Load();
        _installPathBox.Text = _settings.InstallPath;
        _processBox.Text = _settings.ProcessToKill;
    }

    private void SaveSettingsFromUi()
    {
        _settings.InstallPath = _installPathBox.Text.Trim();
        _settings.ProcessToKill = _processBox.Text.Trim();

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

    private static Version ParseVersion(string raw)
    {
        var s = (raw ?? "").Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
        return Version.TryParse(s, out var v) ? v : new Version(0, 0, 0);
    }

    private async Task CheckForUpdateAsync()
    {
        SetBusy(true);
        _statusLabel.Text = "Checking for updates...";
        try
        {
            _latestRelease = await _gitHubService.GetLatestRelease(
                AppConstants.GitHubOwner, AppConstants.GitHubRepo, AppConstants.AssetName);

            UpdateVersionLabels();

            var installed = ParseVersion(_settings.InstalledVersion);
            var latest = ParseVersion(_latestRelease.TagName);

            if (latest > installed)
            {
                _downloadButton.Enabled = _latestRelease.HasAsset;
                _statusLabel.Text = $"Update available: {_latestRelease.TagName}";

                if (!_latestRelease.HasAsset)
                {
                    MessageBox.Show(this,
                        $"A newer release ({_latestRelease.TagName}) was found, but the asset '{AppConstants.AssetName}' was not attached.",
                        "Asset Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
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
                _statusLabel.Text = "You are up to date.";
                MessageBox.Show(this, "You already have the latest version installed.",
                    "Up To Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Check failed.";
            MessageBox.Show(this, $"Could not check for updates:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DownloadAndInstallAsync()
    {
        if (_latestRelease == null || !_latestRelease.HasAsset)
        {
            MessageBox.Show(this, "No downloadable release found. Run 'Check for Update' first.",
                "Nothing to Install", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        _downloadProgress.Value = 0;
        _installProgress.Value = 0;

        var tempZip = Path.Combine(Path.GetTempPath(), $"fusion_{Guid.NewGuid():N}.zip");
        try
        {
            _statusLabel.Text = "Downloading...";
            var dlProgress = new Progress<int>(p => _downloadProgress.Value = Math.Clamp(p, 0, 100));
            await _gitHubService.DownloadAsset(_latestRelease.AssetUrl, tempZip, dlProgress);

            _statusLabel.Text = "Installing...";
            var instProgress = new Progress<int>(p => _installProgress.Value = Math.Clamp(p, 0, 100));
            var zip = tempZip;
            var dest = _settings.InstallPath;
            var proc = _settings.ProcessToKill;

            await Task.Run(() => _updaterService.InstallUpdate(zip, dest, proc, instProgress));

            _settings.InstalledVersion = _latestRelease.TagName;
            _settingsService.Save(_settings);
            UpdateVersionLabels();

            _statusLabel.Text = $"Installed {_latestRelease.TagName} successfully.";
            _downloadButton.Enabled = false;
            MessageBox.Show(this,
                $"Update installed successfully!\n\nVersion {_latestRelease.TagName} is now at {_settings.InstallPath}.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Install failed.";
            MessageBox.Show(this, $"The update could not be installed:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* ignore */ }
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _checkButton.Enabled = !busy;
        _uploadButton.Enabled = !busy;
        _saveSettingsButton.Enabled = !busy;
        if (busy) _downloadButton.Enabled = false;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }
}
