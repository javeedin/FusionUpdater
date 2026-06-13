using FusionClientUpdater.Models;
using FusionClientUpdater.Services;

namespace FusionClientUpdater.Forms;

public class UploadForm : Form
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly GitHubService _gitHubService;

    private Panel _headerPanel = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;

    private TextBox _tokenBox = null!;
    private CheckBox _rememberTokenCheck = null!;
    private TextBox _tagBox = null!;
    private TextBox _nameBox = null!;
    private RichTextBox _notesBox = null!;

    private Button _browseButton = null!;
    private Label _filePathLabel = null!;
    private string _selectedFile = "";

    private Button _createButton = null!;
    private ProgressBar _uploadProgress = null!;
    private RichTextBox _logBox = null!;

    public UploadForm(AppSettings settings, GitHubService gitHubService)
    {
        _settings = settings;
        _settingsService = new SettingsService();
        _gitHubService = gitHubService;
        InitializeComponentManual();
        _tokenBox.Text = _settings.GitHubToken;
        _rememberTokenCheck.Checked = !string.IsNullOrEmpty(_settings.GitHubToken);
    }

    private void InitializeComponentManual()
    {
        Text = "Upload New Release";
        ClientSize = new Size(560, 670);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = ColorTranslator.FromHtml("#1e3a5f")
        };
        _titleLabel = new Label
        {
            Text = "Upload New Release",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 10)
        };
        _subtitleLabel = new Label
        {
            Text = $"Uploading to: {AppConstants.GitHubOwner}/{AppConstants.GitHubRepo}",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Location = new Point(22, 42)
        };
        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);

        var rf = new Font("Segoe UI", 9F, FontStyle.Regular);
        int lx = 20, bx = 20, bw = 520;

        // Token (admin only field)
        var tokenLabel = new Label { Text = "GitHub Token (admin only):", Location = new Point(lx, 85), AutoSize = true, Font = rf };
        _tokenBox = new TextBox { Location = new Point(bx, 107), Size = new Size(bw, 24), Font = rf, UseSystemPasswordChar = true };
        _rememberTokenCheck = new CheckBox
        {
            Text = "Remember token",
            Location = new Point(bx, 134),
            AutoSize = true,
            Font = rf
        };

        var tokenHint = new Label
        {
            Text = "Requires a token with 'repo' scope to create releases.",
            Location = new Point(lx, 156),
            AutoSize = true,
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            ForeColor = Color.Gray
        };

        var tagLabel = new Label { Text = "Tag / Version (e.g. v1.2.0):", Location = new Point(lx, 178), AutoSize = true, Font = rf };
        _tagBox = new TextBox { Location = new Point(bx, 180), Size = new Size(bw, 24), Font = rf };

        var nameLabel = new Label { Text = "Release Name:", Location = new Point(lx, 233), AutoSize = true, Font = rf };
        _nameBox = new TextBox { Location = new Point(bx, 255), Size = new Size(bw, 24), Font = rf };

        var notesLabel = new Label { Text = "Release Notes:", Location = new Point(lx, 288), AutoSize = true, Font = rf };
        _notesBox = new RichTextBox { Location = new Point(bx, 310), Size = new Size(bw, 80), Font = rf };

        var fileLabel = new Label { Text = "Asset (zip) to upload:", Location = new Point(lx, 400), AutoSize = true, Font = rf };
        _browseButton = new Button { Text = "Browse...", Location = new Point(bx, 422), Size = new Size(100, 30), Font = rf };
        _browseButton.Click += (s, e) => BrowseForFile();
        _filePathLabel = new Label
        {
            Text = "No file selected.",
            Location = new Point(bx + 110, 428),
            Size = new Size(410, 24),
            AutoEllipsis = true,
            Font = rf,
            ForeColor = Color.DimGray
        };

        _createButton = new Button
        {
            Text = "Create Release && Upload",
            Location = new Point(bx, 465),
            Size = new Size(210, 36),
            Font = rf
        };
        _createButton.Click += async (s, e) => await CreateReleaseAsync();

        var progLabel = new Label { Text = "Upload progress:", Location = new Point(lx, 512), AutoSize = true, Font = rf };
        _uploadProgress = new ProgressBar { Location = new Point(bx, 534), Size = new Size(bw, 22) };

        var logLabel = new Label { Text = "Log:", Location = new Point(lx, 565), AutoSize = true, Font = rf };
        _logBox = new RichTextBox
        {
            Location = new Point(bx, 587),
            Size = new Size(bw, 68),
            ReadOnly = true,
            BackColor = Color.WhiteSmoke,
            Font = new Font("Consolas", 8.5F)
        };

        Controls.Add(_logBox);
        Controls.Add(logLabel);
        Controls.Add(_uploadProgress);
        Controls.Add(progLabel);
        Controls.Add(_createButton);
        Controls.Add(_filePathLabel);
        Controls.Add(_browseButton);
        Controls.Add(fileLabel);
        Controls.Add(_notesBox);
        Controls.Add(notesLabel);
        Controls.Add(_nameBox);
        Controls.Add(nameLabel);
        Controls.Add(_tagBox);
        Controls.Add(tagLabel);
        Controls.Add(tokenHint);
        Controls.Add(_rememberTokenCheck);
        Controls.Add(_tokenBox);
        Controls.Add(tokenLabel);
        Controls.Add(_headerPanel);
    }

    private void BrowseForFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select asset to upload",
            Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _selectedFile = dialog.FileName;
            _filePathLabel.Text = _selectedFile;
            _filePathLabel.ForeColor = Color.Black;
        }
    }

    private void Log(string message)
    {
        if (_logBox.InvokeRequired) { _logBox.Invoke(new Action(() => Log(message))); return; }
        _logBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    private async Task CreateReleaseAsync()
    {
        var token = _tokenBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show(this, "Please enter your GitHub token.", "Token Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_tagBox.Text))
        {
            MessageBox.Show(this, "Please enter a tag/version (e.g. v1.0.0).", "Tag Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_selectedFile) || !File.Exists(_selectedFile))
        {
            MessageBox.Show(this, "Please select a valid zip file to upload.", "File Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _createButton.Enabled = false;
        _browseButton.Enabled = false;
        _uploadProgress.Style = ProgressBarStyle.Marquee;
        Cursor = Cursors.WaitCursor;

        try
        {
            var log = new Progress<string>(Log);
            await _gitHubService.CreateRelease(
                AppConstants.GitHubOwner,
                AppConstants.GitHubRepo,
                token,
                _tagBox.Text.Trim(),
                _nameBox.Text.Trim(),
                _notesBox.Text,
                _selectedFile,
                log);

            _uploadProgress.Style = ProgressBarStyle.Blocks;
            _uploadProgress.Value = 100;

            // Save or clear token based on checkbox
            _settings.GitHubToken = _rememberTokenCheck.Checked ? token : "";
            _settingsService.Save(_settings);

            MessageBox.Show(this,
                $"Release '{_tagBox.Text.Trim()}' created and asset uploaded successfully.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
            _uploadProgress.Style = ProgressBarStyle.Blocks;
            _uploadProgress.Value = 0;
            MessageBox.Show(this, $"Could not create the release:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _createButton.Enabled = true;
            _browseButton.Enabled = true;
            Cursor = Cursors.Default;
        }
    }
}
