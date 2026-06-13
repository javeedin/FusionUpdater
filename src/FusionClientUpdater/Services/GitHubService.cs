using System.Net.Http;
using Octokit;

namespace FusionClientUpdater.Services;

public class ReleaseInfo
{
    public string TagName { get; set; } = "";
    public long ReleaseId { get; set; }
    public string AssetUrl { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public bool HasAsset => !string.IsNullOrEmpty(AssetUrl);
}

public class GitHubService
{
    private const string ProductHeaderName = "FusionClientUpdater";

    private GitHubClient CreateClient(string? token = null)
    {
        var client = new GitHubClient(new ProductHeaderValue(ProductHeaderName));
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.Credentials = new Credentials(token);
        }
        return client;
    }

    /// <summary>
    /// Gets the latest release for the given repo. Looks for an asset matching assetName.
    /// </summary>
    public async Task<ReleaseInfo> GetLatestRelease(string owner, string repo, string assetName, string? token = null)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            throw new ArgumentException("GitHub owner and repo must be configured.");

        var client = CreateClient(token);
        var release = await client.Repository.Release.GetLatest(owner, repo);

        var info = new ReleaseInfo
        {
            TagName = release.TagName ?? "",
            ReleaseId = release.Id,
            ReleaseNotes = release.Body ?? ""
        };

        var asset = release.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase));

        if (asset != null)
        {
            info.AssetUrl = asset.BrowserDownloadUrl;
        }

        return info;
    }

    /// <summary>
    /// Downloads a file from the given URL to destPath, reporting percentage progress.
    /// </summary>
    public async Task DownloadAsset(string url, string destPath, IProgress<int>? progress, string? token = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Download URL is empty. The release may not contain the expected asset.");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(ProductHeaderName);
        if (!string.IsNullOrWhiteSpace(token))
        {
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
        }

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(destPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        int lastPercent = -1;

        while ((read = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (totalBytes > 0 && progress != null)
            {
                int percent = (int)(totalRead * 100 / totalBytes);
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
    /// Creates a new release and uploads the given file as an asset.
    /// </summary>
    public async Task CreateRelease(string owner, string repo, string token, string tagName,
        string releaseName, string notes, string filePath, IProgress<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            throw new ArgumentException("GitHub owner and repo must be configured.");
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("A GitHub token is required to create a release.");
        if (string.IsNullOrWhiteSpace(tagName))
            throw new ArgumentException("A tag/version is required.");
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The selected file was not found.", filePath);

        var client = CreateClient(token);

        log?.Report($"Creating release '{tagName}'...");
        var newRelease = new NewRelease(tagName)
        {
            Name = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName,
            Body = notes ?? "",
            Draft = false,
            Prerelease = false
        };

        var release = await client.Repository.Release.Create(owner, repo, newRelease);
        log?.Report($"Release created (id {release.Id}). Uploading asset...");

        await using var stream = File.OpenRead(filePath);
        var upload = new ReleaseAssetUpload
        {
            FileName = Path.GetFileName(filePath),
            ContentType = "application/zip",
            RawData = stream
        };

        var asset = await client.Repository.Release.UploadAsset(release, upload);
        log?.Report($"Asset uploaded: {asset.Name}");
        log?.Report("Done.");
    }
}
