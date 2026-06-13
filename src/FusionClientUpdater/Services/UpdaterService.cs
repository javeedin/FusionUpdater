using System.Diagnostics;
using System.IO.Compression;

namespace FusionClientUpdater.Services;

public class UpdaterService
{
    /// <summary>
    /// Kills all processes matching processName (without .exe extension) if running.
    /// </summary>
    public void KillProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return;

        var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        var processes = Process.GetProcessesByName(name);
        foreach (var proc in processes)
        {
            try
            {
                proc.Kill();
                proc.WaitForExit(5000);
            }
            catch
            {
                // ignore failures killing individual processes
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    /// <summary>
    /// Extracts the zip to destPath, overwriting existing files, reporting percentage progress.
    /// </summary>
    public void UnzipToPath(string zipPath, string destPath, IProgress<int>? progress)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Downloaded zip file was not found.", zipPath);

        Directory.CreateDirectory(destPath);

        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries;
        int total = entries.Count;
        int done = 0;
        int lastPercent = -1;

        var fullDest = Path.GetFullPath(destPath);

        foreach (var entry in entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(fullDest, entry.FullName));

            if (!targetPath.StartsWith(fullDest, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Zip entry is outside the target directory: {entry.FullName}");

            if (string.IsNullOrEmpty(entry.Name))
            {
                // directory entry
                Directory.CreateDirectory(targetPath);
            }
            else
            {
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                entry.ExtractToFile(targetPath, overwrite: true);
            }

            done++;
            if (total > 0 && progress != null)
            {
                int percent = (int)(done * 100L / total);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report(percent);
                }
            }
        }

        progress?.Report(100);
    }

    /// <summary>
    /// Orchestrates a full install: kill the process, then unzip to the destination.
    /// </summary>
    public void InstallUpdate(string zipPath, string destPath, string processName, IProgress<int>? progress)
    {
        KillProcess(processName);
        UnzipToPath(zipPath, destPath, progress);
    }
}
