using System.Windows;

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
        SurumMetni.Text = $"Sürüm {UpdateChecker.FormatDisplayVersion(v)}";
        await RunUpdateCheckAsync(v);
    }

    private async void GuncellemeKontrolButonu_Click(object sender, RoutedEventArgs e)
    {
        GuncellemeKontrolButonu.IsEnabled = false;
        try
        {
            await RunUpdateCheckAsync(UpdateChecker.GetAppVersion());
        }
        finally
        {
            GuncellemeKontrolButonu.IsEnabled = true;
        }
    }

    private async Task RunUpdateCheckAsync(Version current)
    {
        GuncellemeDurumu.Text = "Güncelleme kontrol ediliyor…";
        try
        {
            var result = await UpdateChecker.CheckAsync(current).ConfigureAwait(true);
            GuncellemeDurumu.Text = result.StatusText;
            if (!result.UpdateAvailable)
                return;
            UpdateChecker.OfferOpenDownloadPage(result.DownloadUrl);
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
