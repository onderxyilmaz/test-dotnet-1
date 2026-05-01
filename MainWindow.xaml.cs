using System.Windows;

namespace BasitWindowsUygulamasi;

public partial class MainWindow : Window
{
    private int _sayac;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ArtirButonu_Click(object sender, RoutedEventArgs e)
    {
        _sayac++;
        SayacMetni.Text = _sayac.ToString();
    }
}
