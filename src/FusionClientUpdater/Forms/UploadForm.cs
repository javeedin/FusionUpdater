using FusionClientUpdater.Models;
using FusionClientUpdater.Services;

namespace FusionClientUpdater.Forms;

public class UploadForm : Form
{
    private readonly AppSettings _settings;
    private readonly GitHubService _gitHubService;

    // Header
    private Panel _headerPanel = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;

    // Inputs
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
        _gitHubService = gitHubService;
        InitializeComponentManual();
    }

    private void InitializeComponentManual()
    {
        Text = "Upload New Release";
        ClientSize = new Size(550, 600);
        StartPosition = FormStartPosition.CenterParent;
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
            Text = "Upload New Release",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 10)
        };
        _subtitleLabel = new Label
        {
            Text = "Create a GitHub release and upload an asset",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            Location = new Point(22, 42)
        };
        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);

        var regularFont = new Font("Segoe UI", 9F, FontStyle.Regular);
        int labelX = 20, boxX = 20, boxW = 510;

        var tagLabel = new Label { Text = "Tag / Version (e.g. v1.2.0):", Location = new Point(labelX, 85), AutoSize = true, Font = regularFont };
        _tagBox = new TextBox { Location = new Point(boxX, 107), Size = new Size(boxW, 24), Font = regularFont };

        var nameLabel = new Label { Text = "Release Name:", Location = new Point(labelX, 140), AutoSize = true, Font = regularFont };
        _nameBox = new TextBox { Location = new Point(boxX, 162), Size = new Size(boxW, 24), Font = regularFont };

        var notesLabel = new Label { Text = "Release Notes:", Location = new Point(labelX, 195), AutoSize = true, Font = regularFont };
        _notesBox = new RichTextBox { Location = new Point(boxX, 217), Size = new Size(boxW, 90), Font = regularFont };

        var fileLabel = new Label { Text = "Asset (zip) to upload:", Location = new Point(labelX, 315), AutoSize = true, Font = regularFont };
        _browseButton = new Button { Text = "Browse...", Location = new Point(boxX, 337), Size = new Size(100, 30), Font = regularFont };
        _browseButton.Click += (s, e) => BrowseForFile();
        _filePathLabel = new Label
        {
            Text = "No file selected.",
            Location = new Point(boxX + 110, 343),
            Size = new Size(400, 24),
            AutoEllipsis = true,
            Font = regularFont,
            ForeColor = Color.DimGray
        };

        _createButton = new Button
        {
            Text = "Create Release && Upload",
            Location = new Point(boxX, 378),
            Size = new Size(200, 36),
            Font = regularFont
        };
        _createButton.Click += async (s, e) => await CreateReleaseAsync();

        var progLabel = new Label { Text = "Upload progress:", Location = new Point(labelX, 425), AutoSize = true, Font = regularFont };
        _uploadProgress = new ProgressBar { Location = new Point(boxX, 447), Size = new Size(boxW, 22) };

        var logLabel = new Label { Text = "Log:", Location = new Point(labelX, 477), AutoSize = true, Font = regularFont };
        _logBox = new RichTextBox
        {
            Location = new Point(boxX, 499),
            Size = new Size(boxW, 85),
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
        if (_logBox.InvokeRequired)
        {
            _logBox.Invoke(new Action(() => Log(message)));
            return;
        }
        _logBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    private async Task CreateReleaseAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.GitHubOwner) || string.IsNullOrWhiteSpace(_settings.GitHubRepo))
        {
            MessageBox.Show(this, "Please configure the GitHub Owner and Repository in Settings first.",
                "Settings Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_settings.GitHubToken))
        {
            MessageBox.Show(this, "A GitHub token is required to create a release. Set it in Settings.",
                "Token Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_tagBox.Text))
        {
            MessageBox.Show(this, "Please enter a tag/version.",
                "Tag Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_selectedFile) || !File.Exists(_selectedFile))
        {
            MessageBox.Show(this, "Please select a valid file to upload.",
                "File Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _settings.GitHubOwner,
                _settings.GitHubRepo,
                _settings.GitHubToken,
                _tagBox.Text.Trim(),
                _nameBox.Text.Trim(),
                _notesBox.Text,
                _selectedFile,
                log);

            _uploadProgress.Style = ProgressBarStyle.Blocks;
            _uploadProgress.Value = 100;

            MessageBox.Show(this,
                $"Release '{_tagBox.Text.Trim()}' was created and the asset was uploaded successfully.",
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
