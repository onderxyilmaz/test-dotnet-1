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

    /// <summary>Yalnızca belge bilgisi; istek adresleri <see cref="EnumerateManifestUris"/> ile üretilir.</summary>
    internal const string ManifestUrl =
        "https://raw.githubusercontent.com/onderxyilmaz/test-dotnet-1/master/update/latest.json";

    private const string ManifestUrlRaw =
        "https://raw.githubusercontent.com/onderxyilmaz/test-dotnet-1/master/update/latest.json";

    private const string ManifestUrlJsDelivr =
        "https://cdn.jsdelivr.net/gh/onderxyilmaz/test-dotnet-1@master/update/latest.json";

    private static readonly Uri ManifestGitHubApiUri =
        new("https://api.github.com/repos/onderxyilmaz/test-dotnet-1/contents/update/latest.json?ref=master");

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        var v = GetAppVersion();
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"BasitWindowsUygulamasi/{FormatDisplayVersion(v)}");
        c.DefaultRequestHeaders.CacheControl =
            new CacheControlHeaderValue { NoCache = true, NoStore = true, MaxAge = TimeSpan.Zero };
        c.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
        return c;
    }

    /// <summary>CDN ara önbelleğini atlatabilmek için her istek için farklı sorgu dizesi.</summary>
    private static Uri WithCacheBuster(string absoluteUrl)
    {
        var b = new UriBuilder(absoluteUrl)
        {
            Query = "cb=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "_" + Guid.NewGuid().ToString("N")
        };
        return b.Uri;
    }

    internal static IEnumerable<Uri> EnumerateManifestUris()
    {
        yield return WithCacheBuster(ManifestUrlRaw);
        yield return WithCacheBuster(ManifestUrlJsDelivr);
    }

    /// <remarks>Exe üzerindeki ürün/dosya sürümü, bazen assembly adından daha güvenilir (kısmen kopyalanmış dll senaryosu).</remarks>
    internal static Version GetAppVersion()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(exe);
                if (TryParseLooseVersion(fvi.ProductVersion, out var pv))
                    return pv;
                if (TryParseLooseVersion(fvi.FileVersion, out var fv))
                    return fv;
            }
            catch
            {
                /* yoksay */
            }
        }

        var av = Assembly.GetExecutingAssembly().GetName().Version;
        return av ?? new Version(1, 0);
    }

    private static bool TryParseLooseVersion(string? input, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var s = input.Trim();

        var plus = s.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            s = s[..plus].Trim();

        var dash = s.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
            s = s[..dash].Trim();

        if (!Version.TryParse(s, out var parsed))
            return false;

        version = parsed;
        return true;
    }

    internal static string FormatDisplayVersion(Version v) =>
        v.Build is 0 && v.Revision is 0 ? $"{v.Major}.{v.Minor}" : v.ToString();

    internal static async Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken ct = default)
    {
        UpdateManifest? manifest = null;

        // 1) GitHub REST (ham gövde) — ara CDN’nin stale raw.githubusercontent içeriğinden farklı uçtan gelir.
        try
        {
            manifest = await TryDownloadManifestViaGitHubApiAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            /* devam et */
        }

        foreach (var uri in EnumerateManifestUris())
        {
            if (manifest is not null)
                break;

            try
            {
                manifest = await TryDownloadManifestAsync(uri, ct).ConfigureAwait(false);
                if (manifest is not null)
                    break;
            }
            catch
            {
                /* bir sonraki uç */
            }
        }

        if (manifest is null)
            throw new HttpRequestException("Güncelleme manifestosu hiçbir adresten alınamadı.");

        if (string.IsNullOrWhiteSpace(manifest.Version))
            return new(false, "Güncelleme dosyası geçersiz.", null, null);

        var verString = manifest.Version.Trim();
        if (!TryParseLooseVersion(verString, out var remote))
            return new(false, "Uzak sürüm bilgisi okunamadı.", null, null);

        if (remote > current)
            return new(true, $"Yayındaki sürüm: {FormatDisplayVersion(remote)}", manifest.DownloadUrl, remote);

        return new(false, $"En güncel sürümü kullanıyorsunuz ({FormatDisplayVersion(remote)} bildirilen).", null, remote);
    }

    private static async Task<UpdateManifest?> TryDownloadManifestViaGitHubApiAsync(CancellationToken ct)
    {
        var url =
            $"{ManifestGitHubApiUri.AbsoluteUri}&cb={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/vnd.github.raw");

        using var response = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, ct)
            .ConfigureAwait(false);
    }

    private static async Task<UpdateManifest?> TryDownloadManifestAsync(Uri uri, CancellationToken ct)
    {
        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, ct)
            .ConfigureAwait(false);
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
