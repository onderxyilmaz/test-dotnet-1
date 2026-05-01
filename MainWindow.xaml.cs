using System.Windows;
using System.Windows.Input;

namespace BasitWindowsUygulamasi;

public partial class MainWindow : Window
{
    private int _sayac;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var v = UpdateChecker.GetAppVersion();
        var label = UpdateChecker.FormatDisplayVersion(v);
        Title = $"Basit Windows Uygulaması — Sürüm {label}";
        SurumKimligiMetni.Text = $"Sürüm {label}";
        await RunUpdateCheckAsync(v, manualInteraction: false);
    }

    private async void GuncellemeKontrolButonu_Click(object sender, RoutedEventArgs e)
    {
        GuncellemeKontrolButonu.IsEnabled = false;
        ArtirButonu.IsEnabled = false;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            await RunUpdateCheckAsync(UpdateChecker.GetAppVersion(), manualInteraction: true);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            ArtirButonu.IsEnabled = true;
            GuncellemeKontrolButonu.IsEnabled = true;
        }
    }

    /// <summary>
    /// Güncelleme indirilirken çıkılır veya doğruca kapanır; böyle bir durumda tekrar etkinleştirmeye gerek yok.
    /// </summary>
    private async Task RunUpdateCheckAsync(Version current, bool manualInteraction)
    {
        GuncellemeDurumu.Text = "Güncelleme kontrol ediliyor…";
        try
        {
            var result = await UpdateChecker.CheckAsync(current).ConfigureAwait(true);
            var currentLabel = UpdateChecker.FormatDisplayVersion(current);

            if (!result.UpdateAvailable)
            {
                GuncellemeDurumu.Text =
                    $"Yeni bir sürüm yok. Çalışan sürüm: {currentLabel}.";

                if (manualInteraction)
                {
                    MessageBox.Show(
                        $"Şu anda yeni bir güncelleme bulunamadı.\n\nÇalışan sürüm: {currentLabel}",
                        "Güncelleme kontrolü",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;
            }

            var remoteLabel =
                UpdateChecker.FormatDisplayVersion(result.RemoteVersion ?? throw new InvalidOperationException());

            GuncellemeDurumu.Text = $"Yayındaki sürüm ({remoteLabel}) mevcut. Onay bekleniyor…";

            var update = MessageBox.Show(
                $"Şu anda {remoteLabel} sürümü çıkmış. Güncelleme yapmak istiyor musunuz?",
                "Güncelleme",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (update != MessageBoxResult.Yes)
            {
                GuncellemeDurumu.Text = "Güncelleme iptal edildi.";
                return;
            }

            if (!UpdateApplier.CanAutoApply(result.DownloadUrl, out var reason))
            {
                GuncellemeDurumu.Text = "Otomatik güncellenemedi.";
                MessageBox.Show(
                    reason ?? "Paket bağlantısı uygun değil.",
                    "Güncelleme",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                var openSite = MessageBox.Show(
                    "İlgili indirme veya yayın sayfasını tarayıcıda açmak ister misiniz?",
                    "Güncelleme",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (openSite == MessageBoxResult.Yes)
                    _ = UpdateChecker.TryOpenUrlInBrowser(result.DownloadUrl);

                return;
            }

            GuncellemeDurumu.Text = "Güncelleme paketi indiriliyor… Uygulama kapanacak.";
            try
            {
                await UpdateApplier.ApplyAsync(result.DownloadUrl!, default).ConfigureAwait(true);
                return;
            }
            catch (Exception exApply)
            {
                GuncellemeDurumu.Text = "Otomatik güncelleme tamamlanamadı.";
                MessageBox.Show(
                    exApply.Message,
                    "Güncelleme",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                var openSite = MessageBox.Show(
                    "Tarayıcıda indirme sayfasını yine de açmak ister misiniz?",
                    "Güncelleme",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (openSite == MessageBoxResult.Yes)
                    _ = UpdateChecker.TryOpenUrlInBrowser(result.DownloadUrl);
                return;
            }
        }
        catch (Exception ex)
        {
            GuncellemeDurumu.Text = "Güncelleme bilgisi alınamadı.";
            MessageBox.Show(
                ex.Message,
                "Bağlantı hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ArtirButonu_Click(object sender, RoutedEventArgs e)
    {
        _sayac++;
        SayacMetni.Text = _sayac.ToString();
    }
}
