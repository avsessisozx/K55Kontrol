using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace K55Kontrol
{
    public partial class AnaPencere : Window
    {
        private sealed class EfektBilgisi
        {
            public Mod Mod;
            public string Ad, Aciklama;
            public Brush Onizleme;
        }

        private static readonly EfektBilgisi[] EfektListesi =
        {
            new EfektBilgisi { Mod = Mod.Sabit, Ad = "Sabit renk", Aciklama = "Tüm klavye tek bir renkte.",
                Onizleme = Duz("#FF3B3B") },
            new EfektBilgisi { Mod = Mod.Bolgeler, Ad = "Bölge renkleri", Aciklama = "Her bölgeye ayrı renk verin.",
                Onizleme = Seritler("#FF3B3B", "#FF9A1F", "#FFE11F", "#2EE66B", "#1FA8FF", "#B03BFF") },
            new EfektBilgisi { Mod = Mod.Nefes, Ad = "Nefes", Aciklama = "Renk yavaşça yanıp söner.",
                Onizleme = Gecis(0, "#2A0C12", "#FF3B5C", "#2A0C12") },
            new EfektBilgisi { Mod = Mod.RenkDongusu, Ad = "Renk döngüsü", Aciklama = "Tüm renkler sırayla gelir.",
                Onizleme = Gecis(0, "#FF3B3B", "#FFE11F", "#2EE66B", "#1FA8FF", "#B03BFF") },
            new EfektBilgisi { Mod = Mod.GokkusagiDalgasi, Ad = "Gökkuşağı dalgası", Aciklama = "Renkler soldan sağa akar.",
                Onizleme = Gecis(1, "#FF3B3B", "#FFE11F", "#2EE66B", "#1FA8FF", "#B03BFF", "#FF3B3B") },
            new EfektBilgisi { Mod = Mod.Muzik, Ad = "Müzik", Aciklama = "Çalan sese göre bölgeler yanar.",
                Onizleme = Seritler("#1A2350", "#3B5BFF", "#7A5CFF", "#C64CFF", "#3B5BFF", "#1A2350") },
            new EfektBilgisi { Mod = Mod.Ekran, Ad = "Ekran rengi", Aciklama = "Ekrandaki renkler klavyeye yansır.",
                Onizleme = Gecis(0, "#1F6FEB", "#2EC4B6", "#F4A261", "#E76F51") },
            new EfektBilgisi { Mod = Mod.TusTepkisi, Ad = "Tuşa tepki", Aciklama = "Basılan tuşun bölgesi parlar.",
                Onizleme = Gecis(0, "#3A0A12", "#3A0A12", "#FFFFFF", "#3A0A12", "#3A0A12") },
            new EfektBilgisi { Mod = Mod.Gosterge, Ad = "Sistem göstergesi", Aciklama = "CPU, RAM ya da sıcaklık çubuğu.",
                Onizleme = Seritler("#00E65A", "#80D52D", "#FFC800", "#FF7D18", "#1A1C22", "#1A1C22") },
            new EfektBilgisi { Mod = Mod.Kapali, Ad = "Kapalı", Aciklama = "Tüm ışıklar söner.",
                Onizleme = Duz("#0B0C10") },
            new EfektBilgisi { Mod = Mod.Donanim, Ad = "Klavyenin kendi modu", Aciklama = "Kontrol klavyenin hafızasına bırakılır.",
                Onizleme = Gecis(1, "#2B303B", "#4A5162", "#2B303B") },
        };

        private readonly RadioButton[] efektDugmeleri = new RadioButton[EfektListesi.Length];
        private readonly Border[] bolgeKutulari = new Border[K55.BolgeSayisi];
        private readonly DispatcherTimer icueZamanlayici = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        private bool yukleniyor;
        private int seciliBolge;
        private Profil duzenlenen;
        private string profilImzasi;

        private volatile Renk[] sonKare;
        private volatile bool kareBekliyor;
        private volatile bool gorunur;

        private static App Ana => App.Ana;
        private static Ayarlar Ayar => App.Ana.Ayarlar;

        public AnaPencere()
        {
            InitializeComponent();

            BaslikCubugunuKur();
            MenuyuKur();
            AydinlatmaSayfasiniKur();
            ProfillerSayfasiniKur();
            AyarlarSayfasiniKur();
            OtomatikSayfasiniKur();
            KisayollarSayfasiniKur();
            MakrolarSayfasiniKur();
            IsiklariAcDugmesi.Click += delegate { Ana.IsiklariAcKapat(false); };

            Ana.DurumDegisti += DurumuYenile;
            Ana.Motor.KareHazir += KareGeldi;
            IsVisibleChanged += delegate { gorunur = IsVisible; if (gorunur) { DurumuYenile(); IcueDenetle(); } };

            icueZamanlayici.Tick += delegate { if (IsVisible) IcueDenetle(); };
            icueZamanlayici.Start();

            duzenlenen = Ayar.SeciliProfil;
            DurumuYenile();
            IcueDenetle();
        }

        // ================= Başlık çubuğu ve gezinme =================

        private void BaslikCubugunuKur()
        {
            KucultDugmesi.Click += delegate { WindowState = WindowState.Minimized; };
            BuyutDugmesi.Click += delegate
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            };
            KapatDugmesi.Click += delegate { Close(); };
            StateChanged += delegate
            {
                // Kenarlıksız pencere ekranı kaplayınca kenarları ekran dışına taşar; payla telafi et.
                bool buyuk = WindowState == WindowState.Maximized;
                Kok.Margin = new Thickness(buyuk ? 7 : 0);
                BuyutDugmesi.Content = buyuk ? "" : "";
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Ayar.TepsiyeKucult)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            Ana.Cik();
        }

        private void MenuyuKur()
        {
            RoutedEventHandler degistir = delegate
            {
                SayfaAydinlatma.Visibility = MenuAydinlatma.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                SayfaProfiller.Visibility = MenuProfiller.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                SayfaOtomatik.Visibility = MenuOtomatik.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                SayfaKisayollar.Visibility = MenuKisayollar.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                SayfaMakrolar.Visibility = MenuMakrolar.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                if (MenuMakrolar.IsChecked == true) MakrolariYukle();
                SayfaAyarlar.Visibility = MenuAyarlar.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                if (MenuProfiller.IsChecked == true) { ProfilListesiniYenile(true); AcikProgramlariYenile(); }
                if (MenuOtomatik.IsChecked == true) OtomatikSayfasiniYukle();
                if (MenuKisayollar.IsChecked == true) KisayollariYukle();
                if (MenuAyarlar.IsChecked == true) AyarlariYukle();
            };
            MenuAydinlatma.Checked += degistir;
            MenuProfiller.Checked += degistir;
            MenuOtomatik.Checked += degistir;
            MenuKisayollar.Checked += degistir;
            MenuMakrolar.Checked += degistir;
            MenuAyarlar.Checked += degistir;
        }

        // ================= Genel durum =================

        private void DurumuYenile()
        {
            bool bagli = Ana.Motor.Bagli;
            DurumNoktasi.Fill = (Brush)FindResource(bagli ? "Basari" : "Tehlike");
            DurumYazisi.Text = bagli ? "Klavye bağlı" : "Klavye bulunamadı";
            Profil etkin = Ana.EtkinProfil;
            EtkinProfilYazisi.Text = Ana.KarartmaNedeni != null ? "Işıklar geçici olarak kapalı"
                : "Profil: " + etkin.Ad + (Ana.OtomatikProfil != null ? " (otomatik)" : "");
            CihazBilgisi.Text = bagli ? "Cihaz: Corsair K55 RGB PRO (USB 1B1C:1BA4) bağlı." : "Cihaz: bağlı değil. Takıldığında otomatik olarak bulunur.";

            AydinlatmayiYukle();
            if (SayfaProfiller.IsVisible) ProfilListesiniYenile(false);
        }

        private void IcueDenetle()
        {
            bool acik = Process.GetProcessesByName("iCUE").Length > 0;
            IcueUyarisi.Visibility = acik ? Visibility.Visible : Visibility.Collapsed;
            IcueDurumu.Text = acik ? "iCUE şu anda çalışıyor." : "iCUE çalışmıyor. Klavye tamamen K55 Kontrol'de.";
            IcueDurumu.Foreground = (Brush)FindResource(acik ? "Uyari" : "Basari");
            IcueKapatDugmesi.IsEnabled = acik;
        }

        private void IcueyuKapat()
        {
            foreach (Process p in Process.GetProcessesByName("iCUE"))
            {
                try { p.Kill(); p.WaitForExit(3000); }
                catch (Exception ex) { MessageBox.Show(this, "iCUE kapatılamadı:\n" + ex.Message, "K55 Kontrol"); }
            }
            IcueDenetle();
            Ana.Motor.Guncelle();
        }

        private void KareGeldi(Renk[] renkler)
        {
            // Motor iş parçacığından gelir; arayüze en fazla bir bekleyen kare gönderilir.
            if (!gorunur) return;
            sonKare = renkler;
            if (kareBekliyor) return;
            kareBekliyor = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
            {
                kareBekliyor = false;
                Renk[] kare = sonKare;
                if (Ana.EtkinProfil.Mod == Mod.Donanim)
                    kare = Enumerable.Repeat(new Renk(70, 74, 84), K55.BolgeSayisi).ToArray();
                Onizleme.Goster(kare);
            }));
        }

        // ================= Aydınlatma sayfası =================

        private void AydinlatmaSayfasiniKur()
        {
            for (int i = 0; i < EfektListesi.Length; i++)
            {
                EfektBilgisi e = EfektListesi[i];
                RadioButton r = new RadioButton
                {
                    Style = (Style)FindResource("EfektKarti"),
                    Content = e.Ad,
                    Tag = e.Onizleme,
                    DataContext = e.Aciklama,
                    GroupName = "efekt"
                };
                r.Checked += delegate
                {
                    if (yukleniyor) return;
                    Ana.EtkinProfil.Mod = e.Mod;
                    Ana.Degisti();
                };
                efektDugmeleri[i] = r;
                EfektKartlari.Children.Add(r);
            }

            for (int i = 0; i < K55.BolgeSayisi; i++)
            {
                int bolge = i;
                Border b = new Border
                {
                    Height = 34, Margin = new Thickness(3), CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(2), Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = (i + 1).ToString(), FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                b.MouseLeftButtonUp += delegate { BolgeSec(bolge); };
                bolgeKutulari[i] = b;
                BolgeDugmeleri.Children.Add(b);
            }
            Onizleme.BolgeTiklandi += BolgeSec;

            ParlaklikCubugu.ValueChanged += delegate
            {
                ParlaklikDegeri.Text = "%" + (int)ParlaklikCubugu.Value;
                if (yukleniyor) return;
                Ana.EtkinProfil.Parlaklik = (int)Math.Round(ParlaklikCubugu.Value);
                Ana.Degisti();
            };
            HizCubugu.ValueChanged += delegate
            {
                HizDegeri.Text = (int)HizCubugu.Value + " / 10";
                if (yukleniyor) return;
                Ana.EtkinProfil.Hiz = (int)Math.Round(HizCubugu.Value);
                Ana.Degisti();
            };
            Secici.RenkDegisti += c =>
            {
                Profil p = Ana.EtkinProfil;
                if (p.Mod == Mod.Bolgeler) p.BolgeRenkleri[seciliBolge] = c;
                else if (p.Mod == Mod.TusTepkisi && TepkiRengiSecimi.IsChecked == true) p.IkinciRenk = c;
                else p.AnaRenk = c;
                Ana.Degisti();
            };
            YeniEfektAyarlariniKur();
        }

        private void BolgeSec(int bolge)
        {
            seciliBolge = bolge;
            AydinlatmayiYukle();
        }

        private void AydinlatmayiYukle()
        {
            yukleniyor = true;
            try
            {
                Profil p = Ana.EtkinProfil;
                ProfilHaplariniYenile(p);

                string neden = Ana.OtomatikNeden;
                OtomatikBandi.Visibility = neden != null ? Visibility.Visible : Visibility.Collapsed;
                if (neden != null)
                    OtomatikYazisi.Text = neden + " Başka bir profil seçerseniz bu durum bitene kadar sizin seçiminiz kullanılır.";

                string karartma = Ana.KarartmaNedeni;
                KarartmaBandi.Visibility = karartma != null ? Visibility.Visible : Visibility.Collapsed;
                KarartmaYazisi.Text = karartma ?? "";
                IsiklariAcDugmesi.Visibility = Ana.ElleKapali ? Visibility.Visible : Visibility.Collapsed;

                for (int i = 0; i < EfektListesi.Length; i++)
                    efektDugmeleri[i].IsChecked = EfektListesi[i].Mod == p.Mod;

                ParlaklikCubugu.Value = p.Parlaklik;
                HizCubugu.Value = p.Hiz;
                ParlaklikDegeri.Text = "%" + p.Parlaklik;
                HizDegeri.Text = p.Hiz + " / 10";

                bool bolgeli = p.Mod == Mod.Bolgeler;
                bool tepki = p.Mod == Mod.TusTepkisi;
                bool renkli = p.Mod == Mod.Sabit || p.Mod == Mod.Nefes || bolgeli || tepki || (p.Mod == Mod.Muzik && !p.MuzikGokkusagi);
                BolgeBolumu.Visibility = bolgeli ? Visibility.Visible : Visibility.Collapsed;
                RenkBolumu.Visibility = renkli ? Visibility.Visible : Visibility.Collapsed;
                MuzikBolumu.Visibility = p.Mod == Mod.Muzik ? Visibility.Visible : Visibility.Collapsed;
                TepkiRenkleri.Visibility = tepki ? Visibility.Visible : Visibility.Collapsed;
                GostergeBolumu.Visibility = p.Mod == Mod.Gosterge ? Visibility.Visible : Visibility.Collapsed;
                GokkusagiAnahtari.IsChecked = p.MuzikGokkusagi;
                if (p.Mod == Mod.Gosterge) GostergeyiYukle(p);
                ParlaklikCubugu.IsEnabled = p.Mod != Mod.Donanim && p.Mod != Mod.Kapali;
                HizCubugu.IsEnabled = Efektler.Animasyonlu(p.Mod) && p.Mod != Mod.Gosterge;
                HizBasligi.Text = p.Mod == Mod.Muzik ? "HASSASİYET" : p.Mod == Mod.Ekran ? "TEPKİ HIZI"
                    : tepki ? "SÖNME HIZI" : "EFEKT HIZI";
                Onizleme.BolgeSecimi = bolgeli;
                Onizleme.SeciliBolge = bolgeli ? seciliBolge : -1;

                if (bolgeli)
                {
                    RenkBasligi.Text = "BÖLGE " + (seciliBolge + 1) + " RENGİ";
                    Secici.Renk = p.BolgeRenkleri[seciliBolge];
                    for (int i = 0; i < K55.BolgeSayisi; i++)
                    {
                        Renk c = p.BolgeRenkleri[i];
                        bolgeKutulari[i].Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
                        bolgeKutulari[i].BorderBrush = i == seciliBolge ? Brushes.White : (Brush)FindResource("Cizgi");
                        ((TextBlock)bolgeKutulari[i].Child).Foreground =
                            (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 150 ? Brushes.Black : Brushes.White;
                    }
                }
                else if (tepki)
                {
                    bool tepkiRengi = TepkiRengiSecimi.IsChecked == true;
                    RenkBasligi.Text = tepkiRengi ? "TEPKİ RENGİ" : "TABAN RENGİ";
                    Secici.Renk = tepkiRengi ? p.IkinciRenk : p.AnaRenk;
                }
                else if (renkli)
                {
                    RenkBasligi.Text = "RENK";
                    Secici.Renk = p.AnaRenk;
                }

                switch (p.Mod)
                {
                    case Mod.RenkDongusu:
                    case Mod.GokkusagiDalgasi:
                        ModBilgisi.Text = "Bu efekt tüm renkleri kendisi kullanır. Hızı yukarıdan ayarlayabilirsiniz.";
                        break;
                    case Mod.Kapali:
                        ModBilgisi.Text = "Klavyenin ışıkları kapalı.";
                        break;
                    case Mod.Donanim:
                        ModBilgisi.Text = "Aydınlatma klavyenin kendi hafızasındaki moda bırakılır. Bu modda renk ve parlaklık klavyenin kendi tuş kombinasyonlarıyla değiştirilir.";
                        break;
                    case Mod.Muzik:
                        ModBilgisi.Text = "Bilgisayardan çıkan sesi dinler; mikrofon kullanılmaz. Soldaki bölgeler basa, sağdakiler tiz seslere tepki verir.";
                        break;
                    case Mod.Ekran:
                        ModBilgisi.Text = "Ana ekran 6 dikey şeride bölünür; her şeridin rengi klavyede aynı konumdaki bölgeye yansır. Ekran görüntüsü kaydedilmez. Tam ekran çalışan bazı oyunlarda siyah kalabilir.";
                        break;
                    case Mod.TusTepkisi:
                        ModBilgisi.Text = "Bastığınız tuşun bulunduğu bölge tepki rengiyle parlayıp taban rengine döner. Tuşlar kaydedilmez; yalnızca hangi bölgede olduğu kullanılır.";
                        break;
                    default:
                        ModBilgisi.Text = "";
                        break;
                }
                ModBilgisi.Visibility = ModBilgisi.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            finally { yukleniyor = false; }
        }

        private void ProfilHaplariniYenile(Profil etkin)
        {
            string imza = string.Join("|", Ayar.Profiller.Select(x => x.Ad)) + "#" + etkin.Ad;
            if (imza == profilImzasi) return;
            profilImzasi = imza;

            ProfilHaplari.Children.Clear();
            foreach (Profil p in Ayar.Profiller)
            {
                Profil secilen = p;
                RadioButton r = new RadioButton
                {
                    Style = (Style)FindResource("ProfilHapi"),
                    Content = p.Ad,
                    GroupName = "profil",
                    IsChecked = p == etkin
                };
                r.Checked += delegate { if (!yukleniyor) Ana.ProfilSec(secilen); };
                ProfilHaplari.Children.Add(r);
            }
        }

        // ================= Profiller sayfası =================

        private void ProfillerSayfasiniKur()
        {
            YeniProfilDugmesi.Click += delegate
            {
                Profil p = new Profil { Ad = Ayar.BosAd("Yeni profil") };
                Ayar.Profiller.Add(p);
                duzenlenen = p;
                Ana.ProfillerDegisti();
                ProfilListesiniYenile(true);
                ProfilAdiKutusu.Focus();
                ProfilAdiKutusu.SelectAll();
            };
            CogaltDugmesi.Click += delegate
            {
                Profil p = duzenlenen.Kopyala(Ayar.BosAd(duzenlenen.Ad + " kopya"));
                Ayar.Profiller.Insert(Ayar.Profiller.IndexOf(duzenlenen) + 1, p);
                duzenlenen = p;
                Ana.ProfillerDegisti();
                ProfilListesiniYenile(true);
            };
            SilDugmesi.Click += delegate
            {
                if (Ayar.Profiller.Count <= 1) return;
                if (MessageBox.Show(this, "“" + duzenlenen.Ad + "” profili silinsin mi?", "Profili sil",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                bool seciliydi = duzenlenen == Ayar.SeciliProfil;
                int sira = Ayar.Profiller.IndexOf(duzenlenen);
                Ayar.Profiller.Remove(duzenlenen);
                duzenlenen = Ayar.Profiller[Math.Min(sira, Ayar.Profiller.Count - 1)];
                if (seciliydi) Ayar.SeciliProfil = Ayar.Profiller[0];
                Ana.ProfillerDegisti();
                ProfilListesiniYenile(true);
            };
            ProfiliKullanDugmesi.Click += delegate { Ana.ProfilSec(duzenlenen); ProfilListesiniYenile(true); };
            DuzenleDugmesi.Click += delegate
            {
                Ana.ProfilSec(duzenlenen);
                MenuAydinlatma.IsChecked = true;
            };

            ProfilAdiKutusu.LostKeyboardFocus += delegate { AdiUygula(); };
            ProfilAdiKutusu.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) AdiUygula();
                if (e.Key == Key.Escape) ProfilAdiKutusu.Text = duzenlenen.Ad;
            };

            UygulamaKutusu.TextChanged += delegate
            {
                UygulamaIpucu.Visibility = UygulamaKutusu.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            };
            UygulamaKutusu.KeyDown += (s, e) => { if (e.Key == Key.Enter) { UygulamaEkle(UygulamaKutusu.Text); UygulamaKutusu.Clear(); } };
            UygulamaEkleDugmesi.Click += delegate { UygulamaEkle(UygulamaKutusu.Text); UygulamaKutusu.Clear(); };
            GozatDugmesi.Click += delegate
            {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = "Programlar (*.exe)|*.exe", Title = "Program seçin" };
                if (d.ShowDialog(this) == true) UygulamaEkle(Path.GetFileName(d.FileName));
            };
            YenileDugmesi.Click += delegate { AcikProgramlariYenile(); };
        }

        private void AdiUygula()
        {
            string ad = ProfilAdiKutusu.Text.Trim();
            if (ad == duzenlenen.Ad) return;
            Profil ayni = Ayar.Bul(ad);
            if (ad.Length == 0 || (ayni != null && ayni != duzenlenen))
            {
                if (ad.Length > 0)
                    MessageBox.Show(this, "“" + ad + "” adında bir profil zaten var.", "K55 Kontrol");
                ProfilAdiKutusu.Text = duzenlenen.Ad;
                return;
            }
            Ayar.ProfilAdiDegisti(duzenlenen.Ad, ad);
            duzenlenen.Ad = ad;
            Ana.ProfillerDegisti();
            ProfilListesiniYenile(true);
        }

        private void UygulamaEkle(string metin)
        {
            string ad = UygulamaIzleyici.ProgramAdi(metin);
            if (ad.Length == 0) return;
            if (!duzenlenen.Uygulamalar.Any(u => UygulamaIzleyici.ProgramAdi(u) == ad))
                duzenlenen.Uygulamalar.Add(ad + ".exe");
            Ana.ProfillerDegisti();
            ProfilListesiniYenile(true);
        }

        private void ProfilListesiniYenile(bool detayiDaYukle)
        {
            if (!Ayar.Profiller.Contains(duzenlenen)) duzenlenen = Ayar.SeciliProfil;
            Profil etkin = Ana.EtkinProfil;

            ProfilListesi.Children.Clear();
            foreach (Profil p in Ayar.Profiller)
            {
                Profil secilen = p;
                string alt = EfektAdi(p.Mod);
                if (p.Uygulamalar.Count > 0) alt += " · " + p.Uygulamalar.Count + " program";

                StackPanel icerik = new StackPanel();
                Grid ust = new Grid();
                ust.Children.Add(new TextBlock { Text = p.Ad, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 60, 0) });
                if (p == etkin)
                {
                    ust.Children.Add(new Border
                    {
                        Background = (Brush)FindResource("Basari"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 1, 8, 2),
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = "Etkin", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black }
                    });
                }
                icerik.Children.Add(ust);
                icerik.Children.Add(new TextBlock { Text = alt, FontSize = 12, Foreground = (Brush)FindResource("SolukMetin"), Margin = new Thickness(0, 3, 0, 0) });

                RadioButton r = new RadioButton
                {
                    Style = (Style)FindResource("ListeKarti"),
                    Content = icerik,
                    GroupName = "profilListesi",
                    IsChecked = p == duzenlenen
                };
                r.Checked += delegate { duzenlenen = secilen; DetayiYukle(); };
                ProfilListesi.Children.Add(r);
            }
            if (detayiDaYukle) DetayiYukle();
        }

        private void DetayiYukle()
        {
            Profil p = duzenlenen;
            if (!ProfilAdiKutusu.IsKeyboardFocused) ProfilAdiKutusu.Text = p.Ad;
            SilDugmesi.IsEnabled = Ayar.Profiller.Count > 1;
            ProfiliKullanDugmesi.IsEnabled = p != Ana.EtkinProfil;

            UygulamaListesi.Children.Clear();
            foreach (string u in p.Uygulamalar.ToList())
            {
                string uygulama = u;
                Button sil = new Button
                {
                    Content = "", FontFamily = (FontFamily)FindResource("Simgeler"), FontSize = 9,
                    Style = (Style)FindResource("BaslikDugmesi"), Width = 22, Height = 22, Margin = new Thickness(6, 0, -4, 0),
                    ToolTip = "Kaldır"
                };
                sil.Click += delegate
                {
                    p.Uygulamalar.Remove(uygulama);
                    Ana.ProfillerDegisti();
                    ProfilListesiniYenile(true);
                };
                UygulamaListesi.Children.Add(Cip(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { new TextBlock { Text = uygulama, VerticalAlignment = VerticalAlignment.Center }, sil }
                }));
            }
            UygulamaYok.Visibility = p.Uygulamalar.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AcikProgramlariYenile()
        {
            AcikProgramlar.Children.Clear();
            foreach (var program in UygulamaIzleyici.PencereliProgramlar())
            {
                string ad = program.Key;
                Border c = Cip(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("Simgeler"), FontSize = 10,
                            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
                            Foreground = (Brush)FindResource("VurguAcik") },
                        new TextBlock { Text = ad + ".exe", VerticalAlignment = VerticalAlignment.Center }
                    }
                });
                c.Cursor = Cursors.Hand;
                c.ToolTip = program.Value;
                c.MouseLeftButtonUp += delegate { UygulamaEkle(ad); };
                c.MouseEnter += delegate { c.BorderBrush = (Brush)FindResource("Vurgu"); };
                c.MouseLeave += delegate { c.BorderBrush = (Brush)FindResource("Cizgi"); };
                AcikProgramlar.Children.Add(c);
            }
        }

        private Border Cip(UIElement icerik) => new Border
        {
            Background = (Brush)FindResource("KartUst"),
            BorderBrush = (Brush)FindResource("Cizgi"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 0, 8, 8),
            Child = icerik
        };

        private static string EfektAdi(Mod m) => EfektListesi.First(e => e.Mod == m).Ad;

        // ================= Ayarlar sayfası =================

        private void AyarlarSayfasiniKur()
        {
            BaslangicAnahtari.Click += delegate
            {
                try { Ayarlar.WindowsIleBaslat = BaslangicAnahtari.IsChecked == true; }
                catch (Exception ex) { MessageBox.Show(this, "Başlangıç ayarı değiştirilemedi:\n" + ex.Message, "K55 Kontrol"); }
                AyarlariYukle();
            };
            TepsiAnahtari.Click += delegate { Ayar.TepsiyeKucult = TepsiAnahtari.IsChecked == true; Ana.Degisti(); };
            OtomatikAnahtari.Click += delegate { Ayar.OtomatikGecis = OtomatikAnahtari.IsChecked == true; Ana.Degisti(); };
            IcueKapatDugmesi.Click += delegate { IcueyuKapat(); };
            YanIcueKapat.Click += delegate { IcueyuKapat(); };
            KlasorDugmesi.Click += delegate
            {
                string klasor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "K55Kontrol");
                Directory.CreateDirectory(klasor);
                Process.Start("explorer.exe", "\"" + klasor + "\"");
            };
        }

        private void AyarlariYukle()
        {
            BaslangicAnahtari.IsChecked = Ayarlar.WindowsIleBaslat;
            TepsiAnahtari.IsChecked = Ayar.TepsiyeKucult;
            OtomatikAnahtari.IsChecked = Ayar.OtomatikGecis;
            IcueDenetle();
        }

        // ================= Efekt önizleme fırçaları =================

        private static Color Hex(string h) => (Color)ColorConverter.ConvertFromString(h);

        private static Brush Duz(string h)
        {
            var b = new SolidColorBrush(Hex(h));
            b.Freeze();
            return b;
        }

        private static Brush Gecis(double aci, params string[] renkler)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = aci > 0 ? new Point(1, 1) : new Point(1, 0) };
            for (int i = 0; i < renkler.Length; i++)
                b.GradientStops.Add(new GradientStop(Hex(renkler[i]), (double)i / (renkler.Length - 1)));
            b.Freeze();
            return b;
        }

        private static Brush Seritler(params string[] renkler)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            for (int i = 0; i < renkler.Length; i++)
            {
                b.GradientStops.Add(new GradientStop(Hex(renkler[i]), (double)i / renkler.Length));
                b.GradientStops.Add(new GradientStop(Hex(renkler[i]), (double)(i + 1) / renkler.Length));
            }
            b.Freeze();
            return b;
        }
    }
}
