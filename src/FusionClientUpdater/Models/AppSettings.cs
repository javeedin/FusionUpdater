namespace FusionClientUpdater.Models;

public static class AppConstants
{
    public const string GitHubOwner = "javeedin";
    public const string GitHubRepo = "fusionupdater";
    public const string AssetName = "fusionclientweb.zip";
    public const string DefaultInstallPath = @"C:\fusion";
    public const string DefaultProcessToKill = "GraysWMS";
}

public class AppSettings
{
    public string InstalledVersion { get; set; } = "0.0.0";
    public string InstallPath { get; set; } = AppConstants.DefaultInstallPath;
    public string ProcessToKill { get; set; } = AppConstants.DefaultProcessToKill;
}
