namespace FusionClientUpdater.Forms;

public class PasswordDialog : Form
{
    private TextBox _passwordBox = null!;
    public string EnteredPassword => _passwordBox.Text;

    public PasswordDialog()
    {
        Text = "Upload Tool — Password Required";
        ClientSize = new Size(360, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        var icon = new Label
        {
            Text = "🔒",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 18F)
        };

        var prompt = new Label
        {
            Text = "Enter password to access the Upload Tool:",
            Location = new Point(55, 26),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F)
        };

        _passwordBox = new TextBox
        {
            Location = new Point(55, 58),
            Size = new Size(280, 24),
            UseSystemPasswordChar = true,
            Font = new Font("Segoe UI", 10F)
        };
        _passwordBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); }
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };

        var okBtn = new Button
        {
            Text = "OK",
            Location = new Point(175, 100),
            Size = new Size(75, 30),
            DialogResult = DialogResult.OK,
            Font = new Font("Segoe UI", 9F)
        };
        var cancelBtn = new Button
        {
            Text = "Cancel",
            Location = new Point(260, 100),
            Size = new Size(75, 30),
            DialogResult = DialogResult.Cancel,
            Font = new Font("Segoe UI", 9F)
        };

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        Controls.Add(icon);
        Controls.Add(prompt);
        Controls.Add(_passwordBox);
        Controls.Add(okBtn);
        Controls.Add(cancelBtn);

        Shown += (s, e) => _passwordBox.Focus();
    }
}
