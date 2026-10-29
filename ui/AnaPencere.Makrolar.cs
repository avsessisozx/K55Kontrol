using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace K55Kontrol
{
    // Makrolar sayfası: G1–G6 tuşlarına eylem atama, tuş kombinasyonu yakalama ve makro kaydı.
    public partial class AnaPencere
    {
        private int seciliG = 1;
        private readonly RadioButton[] gKartlari = new RadioButton[GTuslari.TusSayisi];
        private readonly TextBlock[] gOzetleri = new TextBlock[GTuslari.TusSayisi];
        private readonly Border[] gRozetleri = new Border[GTuslari.TusSayisi];
        private bool gKombinasyonYakalaniyor;
        private MakroKaydedici kaydedici;
        private Profil gKapsam;   // null: genel atamalar düzenleniyor

        private static readonly GTuru[] GTurleri =
            { GTuru.Yok, GTuru.TusKombinasyonu, GTuru.Metin, GTuru.Program, GTuru.Medya, GTuru.UygulamaEylemi, GTuru.Makro };

        private static string TurAdi(GTuru t)
        {
            switch (t)
            {
                case GTuru.TusKombinasyonu: return "Tuş kombinasyonu gönder";
                case GTuru.Metin: return "Metin yaz";
                case GTuru.Program: return "Program ya da web sitesi aç";
                case GTuru.Medya: return "Medya kontrolü";
                case GTuru.UygulamaEylemi: return "K55 Kontrol eylemi";
                case GTuru.Makro: return "Kaydedilmiş makro";
                default: return "Hiçbir şey yapma";
            }
        }

        // Düzenleyicide gösterilen atama. Profil kapsamında özel atama yoksa genel atama (salt okunur) gösterilir.
        private GAtama SeciliAtama => KapsamAtamasi(seciliG);

        private GAtama KapsamAtamasi(int tus) => gKapsam == null ? Ayar.GAtamaBul(tus) : Ayar.GecerliAtama(gKapsam, tus);

        private string KartOzeti(int tus)
        {
            if (gKapsam == null || Ayarlar.OzelAtama(gKapsam, tus) != null) return Ozet(KapsamAtamasi(tus));
            return "Genel: " + Ozet(Ayar.GAtamaBul(tus));
        }

        private void MakrolarSayfasiniKur()
        {
            for (int i = 0; i < GTuslari.TusSayisi; i++)
            {
                int n = i + 1;
                Border rozet = new Border
                {
                    Width = 38, Height = 38, CornerRadius = new CornerRadius(9), Background = (Brush)FindResource("KartUzeri"),
                    Margin = new Thickness(0, 0, 12, 0),
                    Child = new TextBlock { Text = "G" + n, FontWeight = FontWeights.SemiBold, FontSize = 14,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                TextBlock ozet = new TextBlock { FontSize = 12, Foreground = (Brush)FindResource("SolukMetin"), TextTrimming = TextTrimming.CharacterEllipsis };
                StackPanel metinler = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                metinler.Children.Add(new TextBlock { Text = "G" + n + " tuşu", FontSize = 14, FontWeight = FontWeights.SemiBold });
                metinler.Children.Add(ozet);
                DockPanel icerik = new DockPanel();
                DockPanel.SetDock(rozet, Dock.Left);
                icerik.Children.Add(rozet);
                icerik.Children.Add(metinler);

                RadioButton kart = new RadioButton { Style = (Style)FindResource("ListeKarti"), Content = icerik, GroupName = "gTuslari" };
                kart.Checked += delegate { seciliG = n; GDuzenleyiciyiYukle(); };
                gKartlari[i] = kart;
                gOzetleri[i] = ozet;
                gRozetleri[i] = rozet;
                GListesi.Children.Add(kart);
            }

            foreach (GTuru t in GTurleri) GTurKutusu.Items.Add(new ComboBoxItem { Content = TurAdi(t), Tag = t });
            foreach (MedyaTusu m in Enum.GetValues(typeof(MedyaTusu)))
                GMedyaKutusu.Items.Add(new ComboBoxItem { Content = MakroCalistirici.MedyaAdi(m), Tag = m });

            GTurKutusu.SelectionChanged += delegate
            {
                if (yukleniyor || !(GTurKutusu.SelectedItem is ComboBoxItem oge)) return;
                SeciliAtama.Tur = (GTuru)oge.Tag;
                GDegisti();
            };
            GMedyaKutusu.SelectionChanged += delegate
            {
                if (yukleniyor || !(GMedyaKutusu.SelectedItem is ComboBoxItem oge)) return;
                SeciliAtama.Medya = (MedyaTusu)oge.Tag;
                GDegisti();
            };
            GEylemKutusu.SelectionChanged += delegate
            {
                if (yukleniyor || !(GEylemKutusu.SelectedItem is ComboBoxItem oge)) return;
                SeciliAtama.UygulamaEylemi = (string)oge.Tag;
                GDegisti();
            };
            GMetinKutusu.TextChanged += delegate
            {
                if (yukleniyor) return;
                SeciliAtama.Metin = GMetinKutusu.Text;
                GDegisti(false);
            };
            GYolKutusu.TextChanged += delegate
            {
                if (yukleniyor) return;
                SeciliAtama.Yol = GYolKutusu.Text;
                GDegisti(false);
            };
            GGozatDugmesi.Click += delegate
            {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = "Programlar (*.exe)|*.exe|Tüm dosyalar (*.*)|*.*", Title = "Açılacak programı seçin" };
                if (d.ShowDialog(this) == true) GYolKutusu.Text = d.FileName;
            };

            GKapsamKutusu.SelectionChanged += delegate
            {
                if (yukleniyor || !(GKapsamKutusu.SelectedItem is ComboBoxItem oge)) return;
                gKapsam = oge.Tag as Profil;
                MakrolariYukle();
            };
            GOzelAnahtari.Click += delegate
            {
                if (gKapsam == null) return;
                gKapsam.GAtamalari.RemoveAll(x => x.Tus == seciliG);
                // Özel atama genel atamanın kopyasıyla başlar; oradan değiştirilir.
                if (GOzelAnahtari.IsChecked == true) gKapsam.GAtamalari.Add(Ayar.GAtamaBul(seciliG).Kopya());
                Ana.Degisti();
                MakrolariYukle();
            };

            GKombinasyonDugmesi.Click += delegate { GKombinasyonYakalamayaBasla(); };
            PreviewKeyDown += GKombinasyonYakala;
            Deactivated += delegate { if (gKombinasyonYakalaniyor) GKombinasyonYakalamayiBitir(); };

            GKayitDugmesi.Click += delegate { if (kaydedici == null) KaydaBasla(); else KaydiBitir(); };
            GMakroTemizleDugmesi.Click += delegate
            {
                SeciliAtama.Adimlar.Clear();
                GDegisti();
            };

            Ana.GTusuBasildi += GTusuYaktir;
        }

        private void MakrolariYukle()
        {
            if (gKapsam != null && !Ayar.Profiller.Contains(gKapsam)) gKapsam = null;
            yukleniyor = true;
            try
            {
                GKapsamKutusu.Items.Clear();
                GKapsamKutusu.Items.Add(new ComboBoxItem { Content = "Tüm profiller (genel)" });
                foreach (Profil p in Ayar.Profiller)
                {
                    int ozel = p.GAtamalari.Count;
                    GKapsamKutusu.Items.Add(new ComboBoxItem { Content = "Profil: " + p.Ad + (ozel > 0 ? "  (" + ozel + " özel)" : ""), Tag = p });
                }
                GKapsamKutusu.SelectedItem = GKapsamKutusu.Items.OfType<ComboBoxItem>().First(o => o.Tag == gKapsam);
            }
            finally { yukleniyor = false; }
            GKapsamNotu.Text = gKapsam == null
                ? "Bu atamalar, kendine özel ataması olmayan her profilde geçerlidir."
                : "“" + gKapsam.Ad + "” etkinken (elle seçilince ya da uygulaması açılınca) özel atanan tuşlar genel atamanın yerine geçer.";

            if (gKartlari[seciliG - 1].IsChecked != true) gKartlari[seciliG - 1].IsChecked = true;
            for (int i = 0; i < GTuslari.TusSayisi; i++) GKartiniYaz(i + 1);
            GDuzenleyiciyiYukle();
        }

        private void GKartiniYaz(int tus)
        {
            bool ozel = gKapsam != null && Ayarlar.OzelAtama(gKapsam, tus) != null;
            gOzetleri[tus - 1].Text = KartOzeti(tus);
            gOzetleri[tus - 1].Foreground = (Brush)FindResource(ozel ? "VurguAcik" : "SolukMetin");
        }

        private void GDegisti(bool yenidenYukle = true)
        {
            Ana.Degisti();
            GKartiniYaz(seciliG);
            if (yenidenYukle) GDuzenleyiciyiYukle();
        }

        private void GDuzenleyiciyiYukle()
        {
            GAtama a = SeciliAtama;
            yukleniyor = true;
            try
            {
                GBaslik.Text = "G" + seciliG + " tuşu" + (gKapsam == null ? "" : " · " + gKapsam.Ad);
                bool ozel = gKapsam != null && Ayarlar.OzelAtama(gKapsam, seciliG) != null;
                GOzelAnahtari.Visibility = gKapsam == null ? Visibility.Collapsed : Visibility.Visible;
                GOzelAnahtari.IsChecked = ozel;
                GOzelBasligi.Text = "Bu profilde farklı bir eylem ata";
                GOzelNotu.Text = ozel
                    ? "Kapatırsanız bu profilde de genel atama kullanılır."
                    : "Şu an genel atama kullanılıyor: " + Ozet(Ayar.GAtamaBul(seciliG));
                bool duzenlenebilir = gKapsam == null || ozel;
                GDuzenleyici.IsEnabled = duzenlenebilir;
                GDuzenleyici.Opacity = duzenlenebilir ? 1 : 0.55;
                GTurKutusu.SelectedItem = GTurKutusu.Items.OfType<ComboBoxItem>().First(o => (GTuru)o.Tag == a.Tur);

                GPanelKombinasyon.Visibility = a.Tur == GTuru.TusKombinasyonu ? Visibility.Visible : Visibility.Collapsed;
                GPanelMetin.Visibility = a.Tur == GTuru.Metin ? Visibility.Visible : Visibility.Collapsed;
                GPanelProgram.Visibility = a.Tur == GTuru.Program ? Visibility.Visible : Visibility.Collapsed;
                GPanelMedya.Visibility = a.Tur == GTuru.Medya ? Visibility.Visible : Visibility.Collapsed;
                GPanelEylem.Visibility = a.Tur == GTuru.UygulamaEylemi ? Visibility.Visible : Visibility.Collapsed;
                GPanelMakro.Visibility = a.Tur == GTuru.Makro ? Visibility.Visible : Visibility.Collapsed;

                if (!gKombinasyonYakalaniyor)
                    GKombinasyonDugmesi.Content = a.Vk == 0 ? "Tuş kombinasyonu seçin" : KisayolMetni(new Kisayol { Tus = a.Vk, Degistirici = a.Degistirici });
                if (GMetinKutusu.Text != (a.Metin ?? "")) GMetinKutusu.Text = a.Metin ?? "";
                if (GYolKutusu.Text != (a.Yol ?? "")) GYolKutusu.Text = a.Yol ?? "";
                GMedyaKutusu.SelectedItem = GMedyaKutusu.Items.OfType<ComboBoxItem>().First(o => (MedyaTusu)o.Tag == a.Medya);

                GEylemKutusu.Items.Clear();
                foreach (Eylem e in Enum.GetValues(typeof(Eylem)))
                    GEylemKutusu.Items.Add(new ComboBoxItem { Content = App.EylemAdi(e), Tag = GAtama.EylemAnahtari(e) });
                foreach (Profil p in Ayar.Profiller)
                    GEylemKutusu.Items.Add(new ComboBoxItem { Content = "Profile geç: " + p.Ad, Tag = GAtama.ProfilAnahtari(p.Ad) });
                GEylemKutusu.SelectedItem = GEylemKutusu.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(o => string.Equals((string)o.Tag, a.UygulamaEylemi, StringComparison.CurrentCultureIgnoreCase));

                MakroyuGoster(a.Adimlar, kaydedici != null);
            }
            finally { yukleniyor = false; }
        }

        private string Ozet(GAtama a)
        {
            switch (a.Tur)
            {
                case GTuru.TusKombinasyonu:
                    return a.Vk == 0 ? "Tuş kombinasyonu (seçilmedi)" : KisayolMetni(new Kisayol { Tus = a.Vk, Degistirici = a.Degistirici });
                case GTuru.Metin:
                    string m = (a.Metin ?? "").Replace("\r", "").Replace("\n", " ⏎ ");
                    return m.Length == 0 ? "Metin (boş)" : "Yaz: “" + (m.Length > 28 ? m.Substring(0, 27) + "…" : m) + "”";
                case GTuru.Program:
                    if (string.IsNullOrWhiteSpace(a.Yol)) return "Aç: (seçilmedi)";
                    string y = a.Yol.Trim();
                    return "Aç: " + (y.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? y : Path.GetFileName(y));
                case GTuru.Medya:
                    return MakroCalistirici.MedyaAdi(a.Medya);
                case GTuru.UygulamaEylemi:
                    if (string.IsNullOrEmpty(a.UygulamaEylemi)) return "K55 eylemi (seçilmedi)";
                    if (a.UygulamaEylemi.StartsWith("profil:")) return "Profile geç: " + a.UygulamaEylemi.Substring(7);
                    Eylem e;
                    return Enum.TryParse(a.UygulamaEylemi.Substring(6), out e) ? App.EylemAdi(e) : a.UygulamaEylemi;
                case GTuru.Makro:
                    int basma = a.Adimlar.Count(x => x.Basili);
                    return basma == 0 ? "Makro (kaydedilmedi)" : "Makro: " + basma + " tuş basışı";
                default:
                    return "Atanmamış";
            }
        }

        // G tuşuna basılınca kartındaki rozeti kısa süre vurgula.
        private void GTusuYaktir(int n)
        {
            if (n < 1 || n > GTuslari.TusSayisi) return;
            Border rozet = gRozetleri[n - 1];
            rozet.Background = (Brush)FindResource("Vurgu");
            var sondur = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            sondur.Tick += delegate { sondur.Stop(); rozet.Background = (Brush)FindResource("KartUzeri"); };
            sondur.Start();
        }

        // ---- Tuş kombinasyonu yakalama ----

        private void GKombinasyonYakalamayaBasla()
        {
            gKombinasyonYakalaniyor = true;
            Ana.GAtamalariDuraklatildi = true;
            Ana.KisayollariDuraklat();
            GKombinasyonDugmesi.Content = "Tuşlara basın…";
            GKombinasyonDugmesi.BorderBrush = (Brush)FindResource("Vurgu");
        }

        private void GKombinasyonYakalamayiBitir()
        {
            gKombinasyonYakalaniyor = false;
            Ana.GAtamalariDuraklatildi = false;
            Ana.KisayollariYenile();
            GKombinasyonDugmesi.ClearValue(Button.BorderBrushProperty);
            GDuzenleyiciyiYukle();
        }

        private void GKombinasyonYakala(object sender, KeyEventArgs e)
        {
            if (!gKombinasyonYakalaniyor) return;
            e.Handled = true;
            Key tus = e.Key == Key.System ? e.SystemKey : e.Key;
            int degistirici = Degistiriciler();
            if (tus == Key.LeftCtrl || tus == Key.RightCtrl || tus == Key.LeftAlt || tus == Key.RightAlt ||
                tus == Key.LeftShift || tus == Key.RightShift || tus == Key.LWin || tus == Key.RWin)
            {
                GKombinasyonDugmesi.Content = KisayolMetni(new Kisayol { Degistirici = degistirici }) + " + …";
                return;
            }
            if (tus == Key.Escape && degistirici == 0) { GKombinasyonYakalamayiBitir(); return; }

            GAtama a = SeciliAtama;
            a.Vk = KeyInterop.VirtualKeyFromKey(tus);
            a.Degistirici = degistirici;
            Ana.Degisti();
            GKartiniYaz(seciliG);
            GKombinasyonYakalamayiBitir();
        }

        // ---- Makro kaydı ----

        private void KaydaBasla()
        {
            Ana.GAtamalariDuraklatildi = true;
            Ana.KisayollariDuraklat();
            kaydedici = new MakroKaydedici();
            kaydedici.AdimEklendi += delegate { MakroyuGoster(kaydedici.Adimlar, true); };
            kaydedici.Baslat();
            GKapsamKutusu.IsEnabled = GOzelAnahtari.IsEnabled = false;
            GKayitDugmesi.Content = "● Kaydı durdur";
            GKayitDugmesi.Background = (Brush)FindResource("Tehlike");
            GKayitDugmesi.BorderBrush = (Brush)FindResource("Tehlike");
            GMakroTemizleDugmesi.IsEnabled = false;
            MakroyuGoster(kaydedici.Adimlar, true);
        }

        private void KaydiBitir()
        {
            kaydedici.Durdur();
            List<MakroAdimi> adimlar = kaydedici.Adimlar.ToList();
            kaydedici = null;
            // İlk adımdan önce beklenmez (kaydın başlatılmasından ilk tuşa kadar geçen süre makroya ait değildir).
            if (GGecikmeAnahtari.IsChecked != true)
                for (int i = 0; i < adimlar.Count; i++) adimlar[i].BeklemeMs = i == 0 ? 0 : 15;
            else if (adimlar.Count > 0)
                adimlar[0].BeklemeMs = 0;
            SeciliAtama.Adimlar = adimlar;

            Ana.GAtamalariDuraklatildi = false;
            Ana.KisayollariYenile();
            GKayitDugmesi.Content = "Kaydı başlat";
            GKayitDugmesi.ClearValue(Button.BackgroundProperty);
            GKayitDugmesi.ClearValue(Button.BorderBrushProperty);
            GMakroTemizleDugmesi.IsEnabled = true;
            GKapsamKutusu.IsEnabled = GOzelAnahtari.IsEnabled = true;
            GDegisti();
        }

        private void MakroyuGoster(List<MakroAdimi> adimlar, bool kayitta)
        {
            GMakroAdimlari.Children.Clear();
            int basma = adimlar.Count(x => x.Basili);
            double sure = adimlar.Skip(1).Sum(x => x.BeklemeMs) / 1000.0;
            GMakroOzeti.Text = kayitta
                ? (basma == 0 ? "Kaydediliyor… Şimdi tuşlara basın." : "Kaydediliyor… " + basma + " tuş basışı")
                : (basma == 0 ? "Henüz makro kaydedilmedi." : basma + " tuş basışı, toplam " + sure.ToString("0.0") + " sn");
            GMakroOzeti.Foreground = (Brush)FindResource(kayitta ? "Tehlike" : "Metin");

            int gosterilen = 0;
            foreach (MakroAdimi a in adimlar)
            {
                if (!a.Basili) continue;
                if (++gosterilen > 40)
                {
                    GMakroAdimlari.Children.Add(new TextBlock { Text = "…", Foreground = (Brush)FindResource("SolukMetin"), VerticalAlignment = VerticalAlignment.Center });
                    break;
                }
                if (a.BeklemeMs >= 250 && gosterilen > 1)
                    GMakroAdimlari.Children.Add(new TextBlock
                    {
                        Text = (a.BeklemeMs / 1000.0).ToString("0.0") + " sn", FontSize = 11,
                        Foreground = (Brush)FindResource("SolukMetin"), VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 6)
                    });
                GMakroAdimlari.Children.Add(new Border
                {
                    Background = (Brush)FindResource("KartUzeri"), CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 6),
                    Child = new TextBlock { Text = TusAdi(KeyInterop.KeyFromVirtualKey(a.Vk)), FontSize = 12, FontFamily = new FontFamily("Segoe UI Semibold") }
                });
            }
        }
    }
}
