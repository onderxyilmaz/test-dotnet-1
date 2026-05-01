using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace BasitWindowsUygulamasi;

internal static class UpdateChecker
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Repodaki <c>update/latest.json</c> (master) ile eşleşmeli URL.
    /// </summary>
    internal const string ManifestUrl =
        "https://raw.githubusercontent.com/onderxyilmaz/test-dotnet-1/master/update/latest.json";

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("BasitWindowsUygulamasi/1.0");
        return c;
    }

    internal static Version GetAppVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v ?? new Version(1, 0);
    }

    internal static string FormatDisplayVersion(Version v) =>
        v.Build is 0 && v.Revision is 0 ? $"{v.Major}.{v.Minor}" : v.ToString();

    internal static async Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var response = await Http.GetAsync(ManifestUrl, HttpCompletionOption.ResponseHeadersRead, ct)
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
            return new(true, $"Yeni sürüm: {FormatDisplayVersion(remote)}", manifest.DownloadUrl, remote);

        return new(false, "En son sürümü kullanıyorsunuz.", null, remote);
    }

    internal static void OfferOpenDownloadPage(string? downloadUrl)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            MessageBox.Show(
                "İndirme adresi tanımlı değil. Lütfen proje sayfasından veya dağıtımınızdan güncellemeyi alın.",
                "Güncelleme",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var open = MessageBox.Show(
            $"İndirme adresini tarayıcıda açmak ister misiniz?\n\n{downloadUrl}",
            "Güncelleme",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (open != MessageBoxResult.Yes)
            return;

        Process.Start(new ProcessStartInfo { FileName = downloadUrl.Trim(), UseShellExecute = true });
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
