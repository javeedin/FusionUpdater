using FusionClientUpdater.Forms;

namespace FusionClientUpdater;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var form = new MainForm();
        if (args.Contains("--minimized"))
            form.WindowState = FormWindowState.Minimized;
        Application.Run(form);
    }
}
