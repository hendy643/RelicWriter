using System.Diagnostics;
using System.Reflection;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace RelicWriter.Common.Updates;

public static class AppVersion
{
    // Written into every assembly's metadata by the pre-build version sync from version.json.
    static string? Metadata(string key) =>
        typeof(AppVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;

    /// <summary>The version of the application ("app" in version.json when this build was made).</summary>
    public static SemVer Current { get; } = SemVer.Parse(Metadata("AppVersion")) ?? new SemVer(new Version(0, 0, 0));

    public static string Text => Current.ToString();

    /// <summary>The save version the game database was designed for ("database" in version.json); it is the newest save version the built-in data covers.</summary>
    public static uint DatabaseVersion { get; } = uint.TryParse(Metadata("DatabaseVersion"), out var v) ? v : 0;
}

public sealed record ReleaseAsset(string Name, string Url, long Size, string? Sha256);

/// <param name="Installer">The release's installer for this platform; null when the release has none for it (the update is then only announced).</param>
/// <param name="PageUrl">The release's page on GitHub.</param>
public sealed record UpdateInfo(SemVer Version, ReleaseAsset? Installer, string PageUrl);

/// <summary>
/// Looks at the latest GitHub release: it counts only if it has a <c>version.json</c> among its files (the same file as in the repository root) whose "app"
/// version is newer. The update is that release's installer for this platform. Nothing here is run automatically; the caller decides when to download and install.
/// </summary>
public sealed class UpdateChecker(HttpClient http, string repository = "hendy643/RelicWriter")
{
    public const string VersionFile = "version.json";
    static readonly string[] WindowsInstallers = [".msi", ".exe"];

    // Windows installs by running the installer; there is no Linux installer format yet, so Linux only gets the announcement.
    public static readonly string[] InstallerExtensions = OperatingSystem.IsWindows() ? WindowsInstallers : [];

    public static string DownloadFolder => Path.Combine(Path.GetTempPath(), "RelicWriter-update");

    /// <summary>The newer release, or null when there is none, the release has no version.json, or it is not newer than <paramref name="current"/>.</summary>
    public async Task<UpdateInfo?> CheckAsync(SemVer current, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("RelicWriter", AppVersion.Text));
        using var response = await http.SendAsync(request, cancel);
        if (!response.IsSuccessStatusCode) return null;
        var (page, assets) = ParseRelease(await response.Content.ReadAsStringAsync(cancel));

        if (assets.FirstOrDefault(a => a.Name == VersionFile) is not { } versionFile) return null;
        var published = ParseVersionFile(await http.GetStringAsync(versionFile.Url, cancel));
        return published is null || published <= current ? null : new UpdateInfo(published, PickInstaller(assets, InstallerExtensions), page);
    }

    /// <summary>
    /// Downloads the installer into <see cref="DownloadFolder"/> (a file that is already there and matches is reused) and checks its size and, when GitHub
    /// gives one, its SHA-256. Throws if it doesn't match, so a damaged download is never run.
    /// </summary>
    public async Task<string> DownloadAsync(ReleaseAsset installer, CancellationToken cancel)
    {
        Directory.CreateDirectory(DownloadFolder);
        var path = Path.Combine(DownloadFolder, Path.GetFileName(installer.Name));
        if (File.Exists(path) && Matches(path, installer)) return path;

        var part = path + ".part";
        using (var response = await http.GetAsync(installer.Url, HttpCompletionOption.ResponseHeadersRead, cancel))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            await using var target = File.Create(part);
            await source.CopyToAsync(target, cancel);
        }
        if (!Matches(part, installer))
        {
            File.Delete(part);
            throw new InvalidDataException($"The download of {installer.Name} is damaged.");
        }
        File.Move(part, path, true);
        return path;
    }

    /// <summary>Starts the installer. The caller then closes the app so the installer can replace its files.</summary>
    public static void Launch(string installer)
    {
        var msi = installer.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
        Process.Start(msi ? new ProcessStartInfo("msiexec", $"/i \"{installer}\" /passive") : new ProcessStartInfo(installer) { UseShellExecute = true });
    }

    static bool Matches(string path, ReleaseAsset asset)
    {
        if (new FileInfo(path).Length != asset.Size) return false;
        if (asset.Sha256 is null) return true;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The "app" version of a version.json, or null.</summary>
    internal static SemVer? ParseVersionFile(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("app", out var v) && v.ValueKind == JsonValueKind.String ? SemVer.Parse(v.GetString()) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The release's page and files from the GitHub API's JSON. The "digest" field ("sha256:...") is used when present.</summary>
    internal static (string Page, List<ReleaseAsset> Assets) ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var a in list.EnumerateArray())
            {
                if (!a.TryGetProperty("name", out var name) || !a.TryGetProperty("browser_download_url", out var url)) continue;
                var digest = a.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                assets.Add(new ReleaseAsset(name.GetString()!, url.GetString()!, a.TryGetProperty("size", out var size) ? size.GetInt64() : -1,
                    digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest["sha256:".Length..] : null));
            }
        return (root.TryGetProperty("html_url", out var page) ? page.GetString() ?? "" : "", assets);
    }

    /// <summary>The first asset with one of the extensions, trying the extensions in order; the version file is never an installer.</summary>
    internal static ReleaseAsset? PickInstaller(IEnumerable<ReleaseAsset> assets, string[] extensions) =>
        extensions.Select(e => assets.FirstOrDefault(a => a.Name != VersionFile && a.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(a => a is not null);
}
