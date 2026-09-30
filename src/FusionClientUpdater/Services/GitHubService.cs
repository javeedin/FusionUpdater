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
    /// Creates a release (or reuses an existing one with the same tag, so a failed upload can simply be
    /// retried) and uploads the given file as an asset. The upload is streamed straight to GitHub with no
    /// overall timeout; instead it is only aborted if no bytes move for <see cref="UploadStallTimeout"/>.
    /// </summary>
    public async Task CreateRelease(string owner, string repo, string token, string tagName,
        string releaseName, string notes, string filePath, IProgress<string>? log = null,
        IProgress<UploadProgress>? uploadProgress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            throw new ArgumentException("GitHub owner and repo must be configured.");
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("A GitHub token is required to create a release.");
        if (string.IsNullOrWhiteSpace(tagName))
            throw new ArgumentException("A tag/version is required.");
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The selected file was not found.", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length >= MaxAssetBytes)
            throw new InvalidOperationException(
                $"The file is {FormatBytes(fileInfo.Length)}, but GitHub release assets must be smaller than 2 GB.");

        log?.Report($"Validating inputs...");
        log?.Report($"Owner: {owner}, Repo: {repo}");
        log?.Report($"Tag: {tagName}, File: {fileInfo.Name} ({FormatBytes(fileInfo.Length)})");

        var client = CreateClient(token);

        try
        {
            log?.Report($"Validating token by fetching user info...");
            var user = await client.User.Current();
            log?.Report($"✓ Token valid. Authenticated as: {user.Login}");
        }
        catch (Exception ex)
        {
            log?.Report($"✗ Token validation failed: {ex.Message}");
            throw new InvalidOperationException("GitHub token is invalid or has expired.", ex);
        }

        Release release;
        try
        {
            release = await GetOrCreateRelease(client, owner, repo, tagName, releaseName, notes, log);
        }
        catch (Exception ex)
        {
            log?.Report($"✗ Failed to create release: {ex.Message}");
            throw new InvalidOperationException($"Failed to create release: {ex.Message}", ex);
        }

        var assetName = fileInfo.Name;
        Exception? lastError = null;

        for (int attempt = 1; attempt <= MaxUploadAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // A previous failed attempt can leave a half-uploaded ("starter") asset behind, which would
                // make GitHub reject the new upload with 422. Remove any asset with the same name first.
                await DeleteExistingAsset(client, owner, repo, release.Id, assetName, log);

                log?.Report(attempt == 1
                    ? $"Uploading asset '{assetName}' ({FormatBytes(fileInfo.Length)})..."
                    : $"Retrying upload (attempt {attempt}/{MaxUploadAttempts})...");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                await UploadAssetStreaming(release.UploadUrl, assetName, filePath, token, uploadProgress, log, cancellationToken);
                sw.Stop();

                var mbps = fileInfo.Length / (1024.0 * 1024.0) / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
                log?.Report($"✓ Asset uploaded: {assetName}");
                log?.Report($"✓ Upload time: {sw.Elapsed:hh\\:mm\\:ss}, Average speed: {mbps:F2} MB/s");
                log?.Report("✓ Release completed successfully.");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                log?.Report("✗ Upload cancelled by user.");
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                log?.Report($"✗ Upload attempt {attempt} failed: {ex.Message}");
                if (attempt < MaxUploadAttempts)
                {
                    var delay = TimeSpan.FromSeconds(5 * attempt);
                    log?.Report($"Waiting {delay.TotalSeconds:F0}s before retrying...");
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed to upload asset after {MaxUploadAttempts} attempts: {lastError?.Message}\n\n" +
            $"The release '{tagName}' was created. Click Create again with the same tag to retry the upload.",
            lastError);
    }

    private const int MaxUploadAttempts = 3;
    private const long MaxAssetBytes = 2L * 1024 * 1024 * 1024;
    private static readonly TimeSpan UploadStallTimeout = TimeSpan.FromMinutes(2);

    private static async Task<Release> GetOrCreateRelease(GitHubClient client, string owner, string repo,
        string tagName, string releaseName, string notes, IProgress<string>? log)
    {
        try
        {
            var existing = await client.Repository.Release.Get(owner, repo, tagName);
            log?.Report($"✓ Release '{tagName}' already exists (id {existing.Id}); reusing it.");
            return existing;
        }
        catch (NotFoundException)
        {
            // Expected for a new tag.
        }

        log?.Report($"Creating release '{tagName}'...");
        var newRelease = new NewRelease(tagName)
        {
            Name = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName,
            Body = notes ?? "",
            Draft = false,
            Prerelease = false
        };
        var release = await client.Repository.Release.Create(owner, repo, newRelease);
        log?.Report($"✓ Release created (id {release.Id})");
        return release;
    }

    private static async Task DeleteExistingAsset(GitHubClient client, string owner, string repo,
        long releaseId, string assetName, IProgress<string>? log)
    {
        var assets = await client.Repository.Release.GetAllAssets(owner, repo, releaseId);
        foreach (var asset in assets.Where(a => string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase)))
        {
            log?.Report($"Removing existing asset '{asset.Name}' ({asset.State}) before upload...");
            await client.Repository.Release.DeleteAsset(owner, repo, asset.Id);
        }
    }

    private static async Task UploadAssetStreaming(string uploadUrlTemplate, string assetName, string filePath,
        string token, IProgress<UploadProgress>? uploadProgress, IProgress<string>? log, CancellationToken cancellationToken)
    {
        // UploadUrl looks like "https://uploads.github.com/repos/o/r/releases/1/assets{?name,label}".
        var braceIndex = uploadUrlTemplate.IndexOf('{');
        var baseUrl = braceIndex >= 0 ? uploadUrlTemplate[..braceIndex] : uploadUrlTemplate;
        var uploadUrl = $"{baseUrl}?name={Uri.EscapeDataString(assetName)}";

        using var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(30) };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(ProductHeaderName);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var file = new FileStream(filePath, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, true);

        var tracker = new UploadProgressTracker(file.Length, uploadProgress, log);
        using var content = new ProgressStreamContent(file, tracker, stallCts);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? "application/zip" : "application/octet-stream");
        content.Headers.ContentLength = file.Length;

        using var stallTimer = new System.Threading.Timer(_ =>
        {
            if (DateTime.UtcNow - tracker.LastActivityUtc > UploadStallTimeout)
                stallCts.Cancel();
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(uploadUrl, content, stallCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No upload progress for {UploadStallTimeout.TotalMinutes:F0} minutes; the connection appears to have stalled.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
            }
        }
        uploadProgress?.Report(tracker.Snapshot(done: true));
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>Snapshot of an in-flight upload, for driving a progress bar.</summary>
public readonly record struct UploadProgress(long BytesSent, long TotalBytes, double BytesPerSecond, TimeSpan? Eta)
{
    public int Percent => TotalBytes > 0 ? (int)Math.Min(100, BytesSent * 100 / TotalBytes) : 0;
}

internal sealed class UploadProgressTracker
{
    private readonly long _total;
    private readonly IProgress<UploadProgress>? _progress;
    private readonly IProgress<string>? _log;
    private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();
    private long _sent;
    private int _lastPercent = -1;
    private int _lastLoggedTen = -1;
    private long _lastActivityTicks = DateTime.UtcNow.Ticks;

    public UploadProgressTracker(long total, IProgress<UploadProgress>? progress, IProgress<string>? log)
    {
        _total = total;
        _progress = progress;
        _log = log;
    }

    public DateTime LastActivityUtc => new(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);

    public void Add(int bytes)
    {
        _sent += bytes;
        Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

        var snap = Snapshot(done: false);
        if (snap.Percent != _lastPercent)
        {
            _lastPercent = snap.Percent;
            _progress?.Report(snap);
            if (snap.Percent / 10 != _lastLoggedTen)
            {
                _lastLoggedTen = snap.Percent / 10;
                _log?.Report($"  {snap.Percent,3}%  {snap.BytesSent / 1048576.0:F0}/{_total / 1048576.0:F0} MB" +
                             $"  @ {snap.BytesPerSecond / 1048576.0:F2} MB/s" +
                             (snap.Eta is { } eta ? $"  ETA {eta:hh\\:mm\\:ss}" : ""));
            }
        }
    }

    public UploadProgress Snapshot(bool done)
    {
        var seconds = Math.Max(_sw.Elapsed.TotalSeconds, 0.001);
        var rate = _sent / seconds;
        TimeSpan? eta = !done && rate > 0 ? TimeSpan.FromSeconds((_total - _sent) / rate) : null;
        return new UploadProgress(done ? _total : _sent, _total, rate, eta);
    }
}

/// <summary>
/// HttpContent that streams a file in chunks and reports each chunk to a tracker, so the caller can show
/// real progress and detect a stalled connection (instead of relying on a fixed overall timeout).
/// </summary>
internal sealed class ProgressStreamContent : HttpContent
{
    private const int ChunkSize = 256 * 1024;
    private readonly Stream _source;
    private readonly UploadProgressTracker _tracker;
    private readonly CancellationTokenSource _cts;

    public ProgressStreamContent(Stream source, UploadProgressTracker tracker, CancellationTokenSource cts)
    {
        _source = source;
        _tracker = tracker;
        _cts = cts;
    }

    protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        => SerializeToStreamAsync(stream, context, _cts.Token);

    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ChunkSize];
        int read;
        while ((read = await _source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            _tracker.Add(read);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _source.Length;
        return true;
    }
}
