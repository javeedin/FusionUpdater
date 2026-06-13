namespace FusionClientUpdater.Models;

public class AppSettings
{
    public string GitHubOwner { get; set; } = "";
    public string GitHubRepo { get; set; } = "";
    public string GitHubToken { get; set; } = "";
    public string InstalledVersion { get; set; } = "0.0.0";
    public string InstallPath { get; set; } = @"C:\fusion";
    public string ProcessToKill { get; set; } = "GraysWMS";
    public string AssetName { get; set; } = "fusionclientweb.zip";
}
