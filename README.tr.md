[English](README.md) · [Türkçe](README.tr.md)

# Starlit Crosshair

Tıklamaları alttaki pencereye geçiren masaüstü katmanına sahip, yerel bir Windows nişangâh editörü. Tasarımını canlı önizlemede oluştur, profillerini kaydet ve pencereli veya kenarlıksız oyunlarda kullan. Uygulamanın pencere başlığı **Crosshair Studio**, çalıştıracağın dosya **OzelCrosshair.exe** adını taşır.

## İndir ve çalıştır

1. [Son sürüm sayfasını](https://github.com/wiski-i/Starlit-Crosshair/releases/latest) aç.
2. `Starlit-Crosshair-<sürüm>-windows.zip` dosyasını indir ve ZIP'in tamamını yazabildiğin bir klasöre çıkar.
3. `OzelCrosshair.exe` dosyasını aç. Kurulum veya yönetici yetkisi gerekmez.

**Windows 10 veya Windows 11** ve **.NET Framework 4.8 ya da üzeri** gerekir. Windows 11'de ve güncel Windows 10 kurulumlarında bulunur. Tek başına modern .NET SDK, .NET Framework'ün yerini tutmaz. Uygulama dijital olarak imzalanmamıştır; Windows yayıncı uyarısı gösterebilir. İndirmeyi release'teki `SHA256SUMS.txt` ile doğrula:

```powershell
Get-FileHash .\Starlit-Crosshair-1.0.0-windows.zip -Algorithm SHA256
```

Özeti, tam olarak aynı dosya adına ait satırla karşılaştır. Taşınabilir ZIP; EXE, iki README, lisans ve değişiklik listesini içerir. Kişisel ayarlar Windows hesabında ayrı tutulur.

## Nişangâh tasarla

- Renk, opaklık, dış hat, merkez işareti, çizgi uzunluğu, kalınlık, boşluk, ölçek ve konumu düzenle. **Şekil** sekmesinde kare, halka ve elmas merkezler, yuvarlak uçlar ve dört kol için ayrı uzunluklar bulunur; %0 kolu gizler.
- Canlı önizleme, katmanla aynı çizim kodunu kullanır. Tasarımı 1×, 2× veya 3× büyütmede açık, dama ve vizör zeminlerinde gör. **Görünürlük Testi**; gökyüzü, loş oda ve hareketli örnek zeminler ekler.
- Kontrast hazır ayarları ve siyah/beyaz çift dış hat görünürlüğü artırabilir. **Yan Yana Kıyasla** ile iki profili aynı zemin ve büyütmede karşılaştır.
- **Ctrl+Z / Ctrl+Y** veya araç çubuğuyla geri al/yinele. Yakın zamanlı sürekli ayarlar gruplanır; geçmiş yalnız geçerli oturum boyunca tutulur. Paylaşım kodu; içe aktarma, sıfırlama, profil seçimi, geri alma ve yinelemede güncellenir.

## Profiller ve oyun bağları

Geçerli tasarımı açıklayıcı bir adla kaydet. Özel profilleri ad veya etiketle ara, favorileri süz; kartın sağ tık menüsünden kopyala, bilgilerini düzenle veya sil. **Etiket ve Kısayol** ile favorileri **Ctrl+Alt+1 / 2 / 3** tuşlarına ata. Favori kısayolu, başka bir ön plan sürecine geçilene kadar oyunun otomatik profilini geçersiz kılar.

**Oyuna Özel** bölümünde kayıtlı profili oyunun **gerçek `.exe` dosya adına** bağla; örneğin `example-game.exe`. Başlatıcıyı değil, oyunu seç. İlgili süreç ön plandayken katman bağlı tasarımı kullanır; diğer durumda editör tasarımına döner. Bu işlem editörde kayıtlı tasarımı değiştirmez.

## Monitör ve kalibrasyon

Ekran/katman sayfasından hedef monitörü seç. Bağlantısı kesilirse birincil ekrana dönülür. **Konum → Piksel Kalibrasyonu** bölümünde ok tuşlarıyla 1 piksel, Shift+ok ile 10 piksel kaydır. Uygula, düzeltmeyi ilgili monitör için saklar; bu değer etkin profilin konum kaymasına eklenir. Fiziksel monitör bağlantısı ve farklı DPI ölçekleri donanım üzerinde ayrıca kontrol edilmelidir.

## Paylaş ve yedekle

- Bir tasarımı taşımak için Ayarlar'daki **VCRDB paylaşım kodunu** kopyala veya içe aktar.
- Profil Galerisi'nden **`.vcpak` profil paketi** al veya dışa aktar. İçe aktarmadan önce önizle ve istediğin kayıtları seç. Aynı adlı mevcut profiller korunur.
- Paketler **yalnız özel tasarımları** taşır. Uygulama ayarlarını, favori atamalarını, etiketleri, monitör kalibrasyonunu veya oyun bağlarını taşımaz. Paylaşım kodu da yalnız tasarımı içerir.
- **Tam yedek** için uygulamadan tepsi menüsüyle çık ve `config.ini` ile `profiles` dahil `%APPDATA%\VCRDBCrosshair` klasörünün tamamını kopyala. Geri yüklerken uygulama kapalı olsun. Mevcut klasörü değiştirmeden önce onun da yedeğini al.

## Kısayollar, tepsi ve dil

| İşlem | Varsayılan |
| --- | --- |
| Katmanı aç/kapat | Ctrl+Alt+C (değiştirilebilir) |
| Atanmış favoriyi yükle | Ctrl+Alt+1, Ctrl+Alt+2, Ctrl+Alt+3 |
| Tasarımı geri al / yinele | Editörde Ctrl+Z / Ctrl+Y |

Üstteki durum düğmesi görünürlüğü açar/kapatır. **Basılı tutarak göster**, katmanı yalnız ayarladığın tuş birleşimi basılıyken gösterir. **Tepsiye Gizle**, oyun sırasında uygulamayı açık tutar; bildirim simgesine çift tıklayarak editörü geri açabilirsin. Editör gizliyken veya küçültülmüşken katman varsayılan olarak sabittir; animasyonun sürmesi için Ayarlar'dan **Nabzı sürdür** seçeneğini aç.

Vurgu rengini, cam malzemesini ve dili Ayarlar'dan değiştir. **Dil değişikliği uygulamadan çıkıp yeniden açıldığında uygulanır.** İkinci açılışta kayıtlı dil ve temayla uyarı gösterilir; ilk örneği açmak için mevcut tepsi simgesini kullan.

## Kaynaktan derle, test et ve paketle

Windows'ta .NET Framework C# derleyicisi bulunan bir kaynak klasöründe:

```bat
Build.bat
tests\Run.bat test-results
```

Betikler Windows'un .NET Framework derleyicisini kullanır, **C# 5** dil modunu uygular ve derleyici uyarılarını hata sayar. NuGet paketi kurulmaz. `Build.bat`, depo kökünde `OzelCrosshair.exe` üretir.

Tam regresyon paketini **test için ayrılmış etkileşimli Windows masaüstünde** çalıştır. Testler kısa süreli yerel pencereler açar, global kısayol kaydını dener, test penceresini etkinleştirir ve gerçek masaüstü birleştiricisinden piksel okur. Görüntüler isteğe bağlı klasör argümanına yazılır (yukarıda `test-results`). Testler kendi benzersiz geçici veri klasörünü kullanır ve sonunda temizler. Windows odak veya kısayol kaydını engellerse `SKIP` basılır; başarılı çıkış kodunu yorumlarken bu satırları da oku.

Etkin olmayan test masaüstünde `tests\Run.bat --offscreen test-results`, diğer kontrolleri çalıştırıp birleştirici yakalamasını açıkça atlar. **Tam masaüstü paketiyle eşdeğer değildir.** `tests\Run.bat --ui-regression` yalnız iki release hatasının regresyonlarını çalıştırır.

Derleme, tam test, taşınabilir ZIP ve SHA-256 dosyasını yerelde hazırlamak için:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Prepare-Release.ps1
```

Çıktılar `dist/` altına yazılır. Betik derleme/test hatasında durur, kendi geçici paketleme klasörünü temizler; git push veya yayın yapmaz. [Windows CI](.github/workflows/windows.yml), push, pull request ve manuel çalıştırmada aynı işlemi yürütür. Başarılı `v*` etiket derlemeleri etiketi EXE ürün sürümüyle karşılaştırıp ZIP ve özeti yayımlar. Mevcut release'in dosyaları yenilenirken açıklaması korunur. [CHANGELOG.md](CHANGELOG.md) ve [RELEASE_NOTES.md](RELEASE_NOTES.md) dosyalarına bak.

## Sorun giderme ve sınırlar

- **Katman görünmüyor:** açık olduğunu ve hedef monitörü kontrol et; pencereli veya kenarlıksız modu dene. Özel tam ekran masaüstü katmanını gizleyebilir.
- **Kısayol reddedildi:** başka bir program kullanıyor olabilir. Farklı tuş birleşimi seç; başarısız kayıt önceki çalışan ayarı korur.
- **Oyun profili değişmiyor:** ön plandaki gerçek oyun `.exe` adını ve kayıtlı profilin hâlâ bulunduğunu kontrol et.
- **Uygulama zaten açık:** gizli simgeler dahil Windows bildirim alanındaki simgeden geri aç.
- **Derleyici yok / uygulama açılmıyor:** .NET Framework 4.8 veya üzerini etkinleştir ya da onar. Betikler derleyici bulunamazsa açık hata ve sıfırdan farklı çıkış kodu verir.
- **Dil değişmedi:** tepsiden çıkıp yeniden başlat; editörü gizlemek uygulamayı yeniden başlatmaz.

Katman bir Windows masaüstü penceresidir; **oyuna kod enjekte etmez**. Tüm oyunlarla veya anti-cheat sistemleriyle uyumluluk garantisi verilmez. Oyunun kurallarını kontrol et. Otomatik Windows regresyonları; canlı oyun, anti-cheat, fiziksel çoklu monitör veya farklı DPI uyumluluğunu kanıtlamaz. [AUDIT.md](AUDIT.md), eski kontrolleri kendi kapsamlarıyla kaydeder; bu sürümün sertifikası değildir.

## Lisans

[GNU General Public License v3.0](LICENSE).
