using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Windows;

namespace BasitWindowsUygulamasi;

internal static class UpdateApplier
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    static UpdateApplier()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("BasitWindowsUygulamasi/1.0");
    }

    internal static bool CanAutoApply(string? downloadUrl, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            reason = "İndirme adresi tanımlı değil.";
            return false;
        }

        var t = downloadUrl.Trim();
        if (!Uri.TryCreate(t, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            reason = "Geçersiz indirme adresi.";
            return false;
        }

        var path = uri.AbsolutePath;
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return true;

        if (t.Contains("github.com", StringComparison.OrdinalIgnoreCase) &&
            t.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase))
            return true;

        reason =
            "Otomatik güncelleme için doğrudan paket bağlantısı gerekir (ör. .zip veya GitHub “releases/download/…/dosya.zip”).";
        return false;
    }

    internal static async Task ApplyAsync(string downloadUrl, CancellationToken ct = default)
    {
        var url = downloadUrl.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Geçersiz adres.");

        var path = uri.AbsolutePath;
        var looksZip =
            path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            || (uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                && path.Contains("/releases/download/", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        var looksExe = path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        if (looksExe && !looksZip)
        {
            var tempExe = Path.Combine(Path.GetTempPath(), "BasitWU-" + Guid.NewGuid().ToString("N") + ".exe");
            await DownloadFileAsync(uri, tempExe, ct).ConfigureAwait(false);
            Process.Start(new ProcessStartInfo { FileName = tempExe, UseShellExecute = true });
            await Application.Current.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
            return;
        }

        var workRoot = Path.Combine(Path.GetTempPath(), "BasitWU-w-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workRoot);
        var zipPath = Path.Combine(workRoot, "package.zip");
        var psPath = Path.Combine(Path.GetTempPath(), "BasitWU-u-" + Guid.NewGuid().ToString("N") + ".ps1");

        try
        {
            await DownloadFileAsync(uri, zipPath, ct).ConfigureAwait(false);

            if (!LooksLikeZipFile(zipPath))
                throw new InvalidOperationException(
                    "İndirilen dosya zip olarak açılamadı (bağlantı bir web sayfası veya yanlış dosya olabilir).");

            var extractDir = Path.Combine(workRoot, "extract");
            Directory.CreateDirectory(extractDir);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var contentRoot = GetExtractedContentRoot(extractDir);
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                exePath = Process.GetCurrentProcess().MainModule?.FileName ??
                    throw new InvalidOperationException("Uygulama yolu alınamadı.");

            var appDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(appDir))
                throw new InvalidOperationException("Uygulama klasörü alınamadı.");

            var pid = Environment.ProcessId;
            var rob = Sq(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "robocopy.exe"));
            var src = Sq(Path.GetFullPath(contentRoot));
            var dst = Sq(Path.GetFullPath(appDir));
            var exe = Sq(Path.GetFullPath(exePath));
            var work = Sq(Path.GetFullPath(workRoot));
            var self = Sq(Path.GetFullPath(psPath));

            var ps =
                $"Start-Sleep -Seconds 4\n" +
                $"try {{ Stop-Process -Id {pid} -Force -ErrorAction SilentlyContinue }} catch {{ }}\n" +
                $"$src = {src}\n" +
                $"$dst = {dst}\n" +
                $"$exe = {exe}\n" +
                $"$rob = {rob}\n" +
                $"$p = Start-Process -FilePath $rob -ArgumentList @($src, $dst, '/E','/R:2','/W:2','/NFL','/NDL','/NJH','/NJS','/IS','/IT') -Wait -PassThru -WindowStyle Hidden\n" +
                $"$rc = if ($null -ne $p.ExitCode) {{ $p.ExitCode }} else {{ -1 }}\n" +
                $"if ($rc -ge 8) {{ Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath {self} -Force -ErrorAction SilentlyContinue; exit 1 }}\n" +
                $"Start-Process -FilePath $exe\n" +
                $"Start-Sleep -Seconds 2\n" +
                $"Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue\n" +
                $"Remove-Item -LiteralPath {self} -Force -ErrorAction SilentlyContinue\n";

            await File.WriteAllTextAsync(psPath, ps, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct)
                .ConfigureAwait(false);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" +
                            psPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            await Task.Delay(600, ct).ConfigureAwait(false);
            await Application.Current.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
        }
        catch
        {
            TryDeleteQuiet(workRoot);
            TryDeleteQuiet(psPath);
            throw;
        }
    }

    /// <summary>PowerShell için tek tırnaklı güvenli dize sabiti.</summary>
    private static string Sq(string path) => "'" + path.Replace("'", "''") + "'";

    private static string GetExtractedContentRoot(string extractDir)
    {
        var entries = Directory.GetFileSystemEntries(extractDir);
        if (entries.Length == 1 && Directory.Exists(entries[0]))
            return entries[0];
        return extractDir;
    }

    private static bool LooksLikeZipFile(string zipPath)
    {
        Span<byte> header = stackalloc byte[4];
        using var fs = File.OpenRead(zipPath);
        return fs.Read(header) >= 4 && header[0] == 0x50 && header[1] == 0x4B;
    }

    private static async Task DownloadFileAsync(Uri uri, string destPath, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fs = File.Create(destPath);
        await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
    }

    private static void TryDeleteQuiet(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            /* yoksay */
        }
    }
}
