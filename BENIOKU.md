# K55 Kontrol

Corsair K55 RGB PRO (USB 1B1C:1BA4) için iCUE'suz, hafif ve Türkçe aydınlatma uygulaması.

## Özellikler
- Koyu temalı arayüz, klavyenin canlı önizlemesi (6 bölge, gerçek klavyeyle eş zamanlı)
- Efektler: Sabit renk, Bölge renkleri, Nefes, Renk döngüsü, Gökkuşağı dalgası, Kapalı, Klavyenin kendi modu
- Renk seçici (renk karesi, ton çubuğu, HEX kodu, hazır renkler), parlaklık ve hız ayarı
- Profiller: oluşturma, adlandırma, çoğaltma, silme
- Programa göre otomatik profil: profile bağlı bir program açılınca o profil devreye girer, kapanınca geri dönülür
- Veriye dayalı efektler (yalnızca seçiliyken çalışır):
  - Müzik: hoparlöre giden ses (WASAPI loopback, mikrofon değil) 6 frekans bandına ayrılır
  - Ekran rengi: ana ekranın 6 dikey şeridinin rengi ilgili bölgeye yansır
  - Tuşa tepki: basılan tuşun bölgesi parlar (yalnızca tuşun konumu kullanılır, kayıt tutulmaz)
  - Sistem göstergesi: CPU/RAM kullanımı, NVIDIA GPU sıcaklığı (NVAPI) veya HWiNFO sıcaklıkları çubuk olarak
- G1–G6 makro tuşları: tuş kombinasyonu, metin yazma, program/web sitesi açma, medya kontrolü,
  K55 Kontrol eylemi (profil değiştirme vb.) ya da zamanlamasıyla kaydedilmiş makro atanabilir
- Zamanlanmış profiller (ör. 22:00–07:00 arası "Gece"); öncelik: açık program > zamanlama > elle seçim
- Işıkları otomatik kapatma: ekran kilitlenince, monitör kapanınca, belirli süre boşta kalınca
- Genel kısayollar (varsayılan Ctrl+Alt+→/←/K/↑/↓/E) ve ekranın altında kısa bildirim; tuşlar değiştirilebilir
- Profil/efekt değişimlerinde ve ışıklar sönerken yumuşak geçiş
- Sistem tepsisi (profil seçimi, ışıkları aç/kapat), Windows ile başlama (`/gizli` parametresiyle tepside açılır)
- Ayarlar: `%APPDATA%\K55Kontrol\ayarlar.json`
- Çıkışta klavye kendi (donanım) moduna döner

## Derleme
Visual Studio 2019/2022 derleme araçları (MSBuild) gerekir; .NET Framework 4.8 hedefleme paketi gerekmez.

    derle.cmd

Çıktılar: `bin\K55Kontrol.exe`, protokol test aracı `bin\ProtokolTesti.exe` ve efekt veri kaynaklarını
(ses, ekran, CPU/RAM/GPU) klavyeye dokunmadan deneyen `bin\KaynakTesti.exe`, klavyenin ham HID raporlarını
ve tuş basışlarını kaydeden `bin\TusGozlemi.exe [saniye] [dosya]`.
Uygulama simgesi `araclar\simge-olustur.ps1` ile üretilir (`assets\K55Kontrol.ico`).

## Klasörler
- `src\` cihaz protokolü, efekt motoru, ayarlar, program izleyici
- `ui\` WPF arayüzü (tema, ana pencere, klavye önizlemesi, renk seçici)
- `test\` protokol testi

## Notlar
- iCUE ile aynı anda çalıştırmayın; ikisi de klavyeye yazar. Uygulama iCUE'yu fark edip kapatma düğmesi gösterir.
- Protokol: arayüz MI_01, usage page 0xFF42, 65 baytlık raporlar. Yazılım modu (`08 01 03 00 02`),
  işlem başlat (`08 0D 00 <uç nokta>`), blok yazma (`08 06 …` / `08 07 …`), düzlemsel R/G/B verisi, işlem bitir (`08 05 01 00`).
  Bu klavyede aydınlatma uç noktası `0x01`'dir (`0x22` desteklenmez).
- G tuşları Windows'a tuş basışı göndermez. Klavyenin MI_02 arayüzünden (usage page 0xFF42, usage 2) gelen
  raporlarda (`rapor[2] = 0x02`) basılı tuş bit haritası bulunur: G1–G5 = `rapor[19]` bitleri 0x08–0x80,
  G6 = `rapor[20]` biti 0x01. `test\TusGozlemi.cs` bu raporları ve normal tuş basışlarını kaydeden gözlem aracıdır.
