using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace BasitWindowsUygulamasi;

internal static class UpdateChecker
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>Repo manifest adresi; isteklerde <see cref="BuildManifestUri"/> ile önbellek kırıcı eklenir.</summary>
    internal const string ManifestUrl =
        "https://raw.githubusercontent.com/onderxyilmaz/test-dotnet-1/master/update/latest.json";

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var v = GetAppVersion();
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"BasitWindowsUygulamasi/{FormatDisplayVersion(v)}");
        c.DefaultRequestHeaders.CacheControl =
            new CacheControlHeaderValue { NoCache = true, NoStore = true, MaxAge = TimeSpan.Zero };
        c.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
        return c;
    }

    /// <summary>CDN / ara önbelleklerde eski <c>latest.json</c> kalmaması için her çağrıda benzersiz sorgu.</summary>
    internal static Uri BuildManifestUri() =>
        new UriBuilder(ManifestUrl) { Query = "t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }.Uri;

    internal static Version GetAppVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v ?? new Version(1, 0);
    }

    internal static string FormatDisplayVersion(Version v) =>
        v.Build is 0 && v.Revision is 0 ? $"{v.Major}.{v.Minor}" : v.ToString();

    internal static async Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var response = await Http.GetAsync(BuildManifestUri(), HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, ct)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(manifest?.Version))
            return new(false, "Güncelleme dosyası geçersiz.", null, null);

        var verString = manifest.Version.Trim();
        if (!Version.TryParse(verString, out var remote))
            return new(false, "Uzak sürüm bilgisi okunamadı.", null, null);

        if (remote > current)
            return new(true, $"Yayındaki sürüm: {FormatDisplayVersion(remote)}", manifest.DownloadUrl, remote);

        return new(false, $"En güncel sürümü kullanıyorsunuz ({FormatDisplayVersion(remote)} bildirilen).", null, remote);
    }

    internal static bool TryOpenUrlInBrowser(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url.Trim(), UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class UpdateManifest
    {
        public string Version { get; set; } = "";
        public string? DownloadUrl { get; set; }
    }
}

internal readonly record struct UpdateCheckResult(
    bool UpdateAvailable,
    string StatusText,
    string? DownloadUrl,
    Version? RemoteVersion);
