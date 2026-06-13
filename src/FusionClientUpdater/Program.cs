using FusionClientUpdater.Forms;

namespace FusionClientUpdater;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
