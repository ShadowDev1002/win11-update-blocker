using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Win11UpdateBlocker.Core.Logging;

namespace Win11UpdateBlocker.Core.Updates;

public sealed class AppUpdateInfo
{
    public required string LatestVersion { get; init; }

    public required string ReleasePageUrl { get; init; }

    public required string DownloadUrl { get; init; }
}

public static class GitHubReleaseUpdateChecker
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var url =
            $"https://api.github.com/repos/{AppMetadata.GitHubOwner}/{AppMetadata.GitHubRepo}/releases/latest";

        using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            FileLogger.Log($"UpdateChecker: GitHub API returned {(int)response.StatusCode}.");
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(stream, JsonOptions, cancellationToken)
                        .ConfigureAwait(false);

        if (release is null)
        {
            return null;
        }

        var latestVersion = AppVersion.Normalize(release.TagName);
        if (!AppVersion.IsNewer(latestVersion, AppMetadata.Version))
        {
            return null;
        }

        var asset = release.Assets.FirstOrDefault(item =>
            string.Equals(item.Name, AppMetadata.ReleaseAssetFileName, StringComparison.OrdinalIgnoreCase));

        if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            FileLogger.Log("UpdateChecker: release asset not found.");
            return null;
        }

        return new AppUpdateInfo
        {
            LatestVersion = latestVersion,
            ReleasePageUrl = release.HtmlUrl,
            DownloadUrl = asset.BrowserDownloadUrl
        };
    }

    public static async Task<string> DownloadInstallerAsync(
        AppUpdateInfo update,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(Path.GetTempPath(), AppMetadata.ConfigFolderName, "updates");
        Directory.CreateDirectory(directory);

        var installerPath = Path.Combine(directory, AppMetadata.ReleaseAssetFileName);
        if (File.Exists(installerPath))
        {
            File.Delete(installerPath);
        }

        using var response = await HttpClient.GetAsync(
            update.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(installerPath);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);

        FileLogger.Log($"UpdateChecker: installer downloaded to {installerPath}.");
        return installerPath;
    }

    public static void LaunchInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
            Verb = "runas"
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
            AppMetadata.DisplayName.Replace(' ', '-'),
            AppMetadata.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
