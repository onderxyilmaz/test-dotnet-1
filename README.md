# Basit Windows Uygulaması

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)

.NET 10 ve WPF kullanan basit bir Windows masaüstü uygulamasıdır. Sayaç, **otomatik güncelleme kontrolü** ve internet üzerinden **paket ile yerinde güncelleme** özelliklerini içerir.

Kaynak kod: [GitHub deposu](https://github.com/onderxyilmaz/test-dotnet-1)

## Gereksinimler

- **Derleme / geliştirme:** [.NET SDK 10.x](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows)
- **Yayın klasöründen çalıştırma (framework-dependent):** aynı makinede **[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)** yüklü olmalıdır.

Self-contained yayın kullanılıyorsa hedef makinede ayrıca runtime kurulması gerekmez (proje şu an varsayılan olarak framework-dependent yayın için yapılandırılmıştır).

## Yerel olarak çalıştırma

Depo kök dizininden:

```bash
dotnet run
```

## Yayın çıktısı üretme (publish)

Örnek (çıktıyı `./publish` klasörüne):

```bash
dotnet publish -c Release -o ./publish
```

Oluşan klasörün tamamını hedef bilgisayara kopyalayın; uygulamayı klasör içindeki `BasitWindowsUygulamasi.exe` ile başlatın.

---

## Güncelleme özellikleri

Uygulama açılışta ve kullanıcı **“Güncellemeleri kontrol et”** düğmesine bastığında uzaktaki bir **manifest** dosyasına bakarak yeni sürüm olup olmadığını anlar.

### Manifest (`latest.json`)

- **Dosya yolu (repoda):** `update/latest.json`
- **Alanlar:**
  - **`version`:** sunucunun “en güncel” kabul ettiği sürüm (örn. `"1.8.4"`)
  - **`downloadUrl`:** indirilecek **zip** paketinin **doğrudan** adresi (`https://...`)

Örnek ham manifest örneği (canlı bağlantı depoyla aynı olabilir):

- `https://raw.githubusercontent.com/onderxyilmaz/test-dotnet-1/master/update/latest.json`

### Manifest’in nasıl okunduğu (istemci sırası)

Eski CDN önbelleklerinden etkilenmemek için uygulama manifest’i **sırasıyla** şu yollarla dener:

1. **GitHub REST API** — `contents/update/latest.json` ve `Accept: application/vnd.github.raw` ile ham JSON (çoğu zaman stale `raw.githubusercontent` sorunundan farklı uçtur).
2. **GitHub Raw** — adresin sonuna her istek için benzersiz `cb=` sorgusu eklenir, `Cache-Control: no-store` kullanılır.
3. **jsDelivr** (`cdn.jsdelivr.net/gh/…`) — yedek kök.

Tüm başarılı okumalar `System.Version` ile **yerel sürümle** karşılaştırılır; uzaktaki sürüm büyükse güncelleme önerilir.

### Güncelleme paketi (zip)

- **Konum:** `update/downloads/` altında `BasitWindowsUygulamasi-<sürüm>.zip` şeklinde tutulur.
- İçeriği **`dotnet publish -c Release`** çıktısıdır (aynı klasör yapısı: `exe`, `dll`, `.deps.json`, `.runtimeconfig.json`, `.pdb` vb.).
- Kullanıcı güncellemeyi onayladığında uygulama zip’i indirir; geçici bir klasöre açar ve arka planda **PowerShell** ile bir süre bekledikten sonra süreci sonlandırıp **`robocopy`** ile yayın klasörünün üzerine yazar ve **aynı exe yolundan** yeniden başlatır.

**Not:** `downloadUrl`, tarayıcıda açılan bir “releases/latest” HTML sayfası değil; **doğrudan zip indiren** HTTPS adresi olmalıdır.

### Yerel sürümün belirlenmesi

Öncelikle çalışan **`BasitWindowsUygulamasi.exe`** üzerindeki **Ürün sürümü / Dosya sürümü** okunur; gerekirse derleme `AssemblyVersion` kullanılır. Böylece kısmen kopyalanmış dll senaryolarında tutarsızlık azaltılır.

### Yeni sürüm yayınlarken yapılacaklar (ön kontrol listesi)

1. **Sürüm numarasını** `BasitWindowsUygulamasi.csproj` içindeki `<Version>` ile yükseltin.
2. `dotnet publish -c Release` ile çıktı alıp klasör içeriğini zip’leyin (`update/downloads/BasitWindowsUygulamasi-<yeni>.zip`).
3. `update/latest.json` içinde **`version`** ve **`downloadUrl`**’i **yeni zip’in doğrudan raw linkine** güncelleyin.
4. Değişiklikleri **`master`** dalına işleyip GitHub’a gönderin.

Eski bir zip dosyası artık kullanılmayacaksa repodan kaldırılıp bağlantılar yalnızca güncel paketi göstersin ki CDN ve kullanıcılar karışmasın.

### İlk kurulumdan güncellenen kullanıcılar

Çok eski istemci sürümleri yalnızca tek bir manifest adresi kullanıyorsa ara ağ/CDN yüzünden eski manifest görebilir. Bu durumda bir kez güncel zip ile el ile kurmak veya daha yeni bir istemci sürümüne geçmek daha güvenilir davranışı geri getirir.

### Windows Defender / SmartScreen

İmzasız yayınların ilk çalıştırmada **tanınmayan uygulama** uyarısı çıkması normaldir. Güvenilir kaynaktaysa kullanıcı “Ek bilgi” → “Yine de çalıştır” ile ilerleyebilir. Yaygın dağıtımda bu uyarıyı azaltmak için **Authenticode kod imzalama** gerekir (ayrı maliyet ve süreç).

---

## Özet klasör yapısı

```
├── BasitWindowsUygulamasi.csproj   # Sürüm: <Version>
├── MainWindow.xaml / .cs           # Ana arayüz ve güncelleme kullanıcı akışı
├── UpdateChecker.cs                # Manifest çekme ve sürüm karşılaştırma
├── UpdateApplier.cs                # Zip indirme ve yerinde güncelleme
├── update/
│   ├── latest.json                 # Güncelleme bildirimi
│   └── downloads/
│       └── *.zip                   # Yayın paketleri
└── README.md
```

İyi çalışmalar.
