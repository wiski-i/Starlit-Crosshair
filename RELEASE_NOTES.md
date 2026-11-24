# Starlit Crosshair 1.0.0

## English

Native Windows crosshair editor and click-through desktop overlay. This release fixes stale share-code output after design changes and applies the saved theme to the already-running warning. It adds version metadata, bilingual guides, C# 5 build checks, regression coverage and portable Windows packaging. Profiles, favorites, tags, game bindings and monitor calibration were already part of the application.

**Install:** download `Starlit-Crosshair-1.0.0-windows.zip`, compare its SHA-256 with `SHA256SUMS.txt`, extract it, and open **OzelCrosshair.exe**. The window is titled **Crosshair Studio**. Requires **Windows 10/11 and .NET Framework 4.8 or later**. No installer, administrator access or additional packages are required. The executable is unsigned.

Settings remain in `%APPDATA%\VCRDBCrosshair`. Exit from the tray and copy that whole folder for a full backup. `.vcpak` files carry custom designs, not application settings or game bindings. Default toggle: Ctrl+Alt+C; assigned favorites: Ctrl+Alt+1/2/3. Restart after a language change.

**Validation:** local Windows C# 5 build and native UI regressions passed; offscreen tests explicitly skipped compositor capture and foreground activation. The tagged release workflow must also pass the full Windows regression suite before publication. See that run's logs for any desktop-specific SKIP messages and its screenshots artifact for rendered UI evidence. Physical monitors, mixed DPI, actual games and anti-cheat compatibility remain unverified. Exclusive fullscreen may hide the desktop overlay; use windowed or borderless mode. The app does not inject code into games and cannot guarantee compatibility with all games or anti-cheat systems.

[English guide](https://github.com/wiski-i/Starlit-Crosshair/blob/v1.0.0/README.md) · [Changelog](https://github.com/wiski-i/Starlit-Crosshair/blob/v1.0.0/CHANGELOG.md) · [GPL-3.0 license](https://github.com/wiski-i/Starlit-Crosshair/blob/v1.0.0/LICENSE)

## Türkçe

Yerel Windows nişangâh editörü ve tıklamaları geçiren masaüstü katmanı. Bu sürüm, tasarım değişikliklerinden sonra eski kalan paylaşım kodunu ve ikinci açılış uyarısında kayıtlı temanın uygulanmamasını düzeltir. EXE sürüm bilgileri, iki dilli rehber, C# 5 derleme kontrolleri, regresyonlar ve taşınabilir Windows paketleme ekler. Profiller, favoriler, etiketler, oyun bağları ve monitör kalibrasyonu uygulamada zaten vardı.

**Kurulum:** `Starlit-Crosshair-1.0.0-windows.zip` dosyasını indir, SHA-256 değerini `SHA256SUMS.txt` ile karşılaştır, ZIP'i çıkar ve **OzelCrosshair.exe** dosyasını aç. Pencere başlığı **Crosshair Studio**. **Windows 10/11 ve .NET Framework 4.8 veya üzeri** gerekir. Kurulum, yönetici yetkisi veya ek paket gerekmez. EXE dijital olarak imzalanmamıştır.

Ayarlar `%APPDATA%\VCRDBCrosshair` içinde kalır. Tam yedek için tepsiden çıkıp bu klasörün tamamını kopyala. `.vcpak`, özel tasarımları taşır; uygulama ayarlarını ve oyun bağlarını taşımaz. Varsayılan aç/kapat: Ctrl+Alt+C; atanmış favoriler: Ctrl+Alt+1/2/3. Dil değişikliği için yeniden başlat.

**Doğrulama:** yerel Windows C# 5 derlemesi ve yerel arayüz regresyonları geçti; ekran dışı testler birleştirici yakalamayı ve ön plan etkinleştirmesini açıkça atladı. Etiketli release akışı yayın öncesinde tam Windows regresyon paketini de geçmek zorundadır. Masaüstüne bağlı SKIP satırları için ilgili çalıştırmanın loglarına, arayüz kanıtı için görüntü artifact'ine bak. Fiziksel monitör, farklı DPI, gerçek oyun ve anti-cheat uyumluluğu doğrulanmadı. Özel tam ekran katmanı gizleyebilir; pencereli veya kenarlıksız modu kullan. Uygulama oyuna kod enjekte etmez; tüm oyunlar veya anti-cheat sistemleri için uyumluluk garantisi vermez.

[Türkçe rehber](https://github.com/wiski-i/Starlit-Crosshair/blob/v1.0.0/README.tr.md) · [Değişiklikler](https://github.com/wiski-i/Starlit-Crosshair/blob/v1.0.0/CHANGELOG.md) · [GPL-3.0 lisansı](https://github.com/wiski-i/Starlit-Crosshair/blob/v1.0.0/LICENSE)
