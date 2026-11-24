# Changelog / Değişiklikler

## 1.0.0 — 2026-11-24

### English

First versioned Windows portable release; existing editor, profiles, tags, favorites, game bindings and pixel calibration remain available.

- Fixed stale share-code output after importing, resetting or applying a design; covered undo/redo and hidden Settings-page synchronization.
- Fixed the second-instance warning ignoring the saved accent and glass material; it loads the language and theme before opening.
- Added EXE title, product, description and 1.0.0 version metadata.
- Enforced C# 5 and compiler warnings as errors; added an explicit missing-compiler error to the test runner.
- Added native WinForms regressions for both fixes and recursive cleanup limited to the suite's unique temporary folder.
- Added English/Turkish guides, local portable packaging with SHA-256, and Windows build/test/artifact/release automation. Tag and product versions must match; updating release assets preserves an existing description.
- Added an explicitly limited offscreen test mode; the release pipeline runs the full desktop suite.

### Türkçe

İlk sürüm numaralı taşınabilir Windows yayını. Mevcut editör, profiller, etiketler, favoriler, oyun bağları ve piksel kalibrasyonu korunur.

- İçe aktarma, sıfırlama veya tasarım uygulamadan sonra eski kalan paylaşım kodu düzeltildi; geri alma/yineleme ve Ayarlar gizliyken yenileme test edildi.
- İkinci açılış uyarısının kayıtlı vurgu rengini ve cam malzemesini yok sayması düzeltildi; pencere açılmadan dil ve tema yüklenir.
- EXE başlığı, ürün adı, açıklama ve 1.0.0 sürüm bilgileri eklendi.
- C# 5 ve uyarıları hata sayma zorunlu oldu; test betiğine açık derleyici yok hatası eklendi.
- İki düzeltme için gerçek WinForms regresyonları ve yalnız testin benzersiz geçici klasörünü kapsayan temizleme eklendi.
- Türkçe/İngilizce rehberler, SHA-256 ile yerel paketleme ve Windows derleme/test/artifact/release akışı eklendi. Etiket ile ürün sürümü eşleşir; mevcut release açıklaması dosyalar güncellenirken korunur.
- Sınırı açıkça belirtilen ekran dışı test modu eklendi; release hattı tam masaüstü paketini çalıştırır.

### Verification limits / Doğrulama sınırları

Local C# 5 build and both native UI bug regressions passed on Windows. The offscreen suite passed its remaining checks but skipped compositor capture and foreground activation on the non-active desktop. The release pipeline requires the full Windows suite; its logs and `test-screenshots` artifact record the actual run, including any SKIP messages. Physical monitor reconnect, mixed DPI, live games and anti-cheat compatibility are not certified.

Windows'ta yerel C# 5 derlemesi ve iki yerel arayüz hata regresyonu geçti. Ekran dışı paket kalan kontrolleri geçti; etkin olmayan masaüstünde birleştirici yakalama ve ön plan etkinleştirmesi atlandı. Release hattı tam Windows paketini zorunlu tutar; gerçek çalıştırma ve SKIP satırları loglarda, görüntüler `test-screenshots` artifact'inde bulunur. Fiziksel monitör bağlantısı, farklı DPI, canlı oyun ve anti-cheat uyumluluğu onaylanmış değildir.
