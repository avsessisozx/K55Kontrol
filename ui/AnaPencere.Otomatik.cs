using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace K55Kontrol
{
    // Zamanlama (zamanlanmış profiller, ışıkları otomatik kapatma) ve Kısayollar sayfaları.
    public partial class AnaPencere
    {
        // ================= Zamanlama sayfası =================

        private void OtomatikSayfasiniKur()
        {
            KuralEkleDugmesi.Click += delegate
            {
                Profil gece = Ayar.Bul("Gece") ?? Ayar.Profiller[0];
                Ayar.Zamanlamalar.Add(new ZamanKurali { Baslangic = 22 * 60, Bitis = 7 * 60, ProfilAdi = gece.Ad });
                Ana.Degisti();
                KurallariYenile();
            };
            KilitAnahtari.Click += delegate { Ayar.KilitteKapat = KilitAnahtari.IsChecked == true; Ana.Degisti(); };
            EkranAnahtari.Click += delegate { Ayar.EkranKapanincaKapat = EkranAnahtari.IsChecked == true; Ana.Degisti(); };
            BostaAnahtari.Click += delegate
            {
                Ayar.BostaKapat = BostaAnahtari.IsChecked == true;
                BostaSureAlani.IsEnabled = Ayar.BostaKapat;
                Ana.Degisti();
            };
            BostaCubugu.ValueChanged += delegate
            {
                BostaDegeri.Text = (int)BostaCubugu.Value + " dk";
                if (yukleniyor) return;
                Ayar.BostaDakika = (int)BostaCubugu.Value;
                Ana.Degisti();
            };
        }

        private void OtomatikSayfasiniYukle()
        {
            yukleniyor = true;
            KilitAnahtari.IsChecked = Ayar.KilitteKapat;
            EkranAnahtari.IsChecked = Ayar.EkranKapanincaKapat;
            BostaAnahtari.IsChecked = Ayar.BostaKapat;
            BostaSureAlani.IsEnabled = Ayar.BostaKapat;
            BostaCubugu.Value = Ayar.BostaDakika;
            BostaDegeri.Text = Ayar.BostaDakika + " dk";
            yukleniyor = false;
            KurallariYenile();
        }

        private void KurallariYenile()
        {
            KuralListesi.Children.Clear();
            DateTime simdi = DateTime.Now;
            foreach (ZamanKurali kural in Ayar.Zamanlamalar.ToList())
            {
                ZamanKurali k = kural;
                Grid satir = new Grid();
                foreach (GridLength g in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto,
                                                 GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
                    satir.ColumnDefinitions.Add(new ColumnDefinition { Width = g });

                CheckBox etkin = new CheckBox { Style = (Style)FindResource("Anahtar"), IsChecked = k.Etkin, VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "Bu zamanlamayı aç / kapat" };
                etkin.Click += delegate { k.Etkin = etkin.IsChecked == true; Ana.Degisti(); KurallariYenile(); };
                Ekle(satir, etkin, 0);

                Ekle(satir, SaatKutusu(k, true), 1, new Thickness(16, 0, 0, 0));
                Ekle(satir, new TextBlock { Text = "–", Foreground = (Brush)FindResource("IkincilMetin"), VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0) }, 2);
                Ekle(satir, SaatKutusu(k, false), 3);
                Ekle(satir, new TextBlock { Text = "arası", Foreground = (Brush)FindResource("IkincilMetin"), VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 14, 0) }, 4);

                ComboBox profil = new ComboBox { MinWidth = 180, HorizontalAlignment = HorizontalAlignment.Left };
                foreach (Profil p in Ayar.Profiller) profil.Items.Add(p.Ad);
                Profil hedef = Ayar.Bul(k.ProfilAdi);
                if (hedef != null) profil.SelectedItem = hedef.Ad;
                else
                {
                    profil.Items.Insert(0, "(silinmiş profil)");
                    profil.SelectedIndex = 0;
                }
                profil.SelectionChanged += delegate
                {
                    string ad = profil.SelectedItem as string;
                    if (ad == null || Ayar.Bul(ad) == null) return;
                    k.ProfilAdi = ad;
                    Ana.Degisti();
                    KurallariYenile();
                };
                Ekle(satir, profil, 5);

                StackPanel sag = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (k.SimdiEtkin(simdi) && hedef != null)
                    sag.Children.Add(new Border
                    {
                        Background = (Brush)FindResource("Basari"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 1, 8, 2),
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0),
                        Child = new TextBlock { Text = "Şu an etkin", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black }
                    });
                Button sil = new Button { Content = "", Style = (Style)FindResource("BaslikDugmesi"), FontSize = 13, ToolTip = "Zamanlamayı sil",
                    Margin = new Thickness(8, 0, 0, 0) };
                sil.Click += delegate { Ayar.Zamanlamalar.Remove(k); Ana.Degisti(); KurallariYenile(); };
                sag.Children.Add(sil);
                Ekle(satir, sag, 6);

                KuralListesi.Children.Add(new Border
                {
                    Background = (Brush)FindResource("KartUst"), BorderBrush = (Brush)FindResource("Cizgi"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10, 8, 10), Margin = new Thickness(0, 0, 0, 10),
                    Opacity = k.Etkin ? 1 : 0.6, Child = satir
                });
            }
            KuralYok.Visibility = Ayar.Zamanlamalar.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private TextBox SaatKutusu(ZamanKurali k, bool baslangic)
        {
            TextBox t = new TextBox
            {
                Width = 88, Padding = new Thickness(4, 6, 4, 6), FontFamily = new FontFamily("Consolas"), FontSize = 15,
                TextAlignment = TextAlignment.Center,
                Text = ZamanKurali.SaatYaz(baslangic ? k.Baslangic : k.Bitis), MaxLength = 5,
                ToolTip = baslangic ? "Başlangıç saati (SS:DD)" : "Bitiş saati (SS:DD)"
            };
            Action uygula = delegate
            {
                int dakika;
                if (ZamanKurali.SaatOku(t.Text, out dakika))
                {
                    if (baslangic) k.Baslangic = dakika; else k.Bitis = dakika;
                    Ana.Degisti();
                }
                t.Text = ZamanKurali.SaatYaz(baslangic ? k.Baslangic : k.Bitis);
            };
            t.LostKeyboardFocus += delegate { uygula(); };
            t.KeyDown += (s, e) => { if (e.Key == Key.Enter) { uygula(); t.SelectAll(); } };
            t.GotKeyboardFocus += delegate { t.SelectAll(); };
            return t;
        }

        private static void Ekle(Grid g, UIElement e, int sutun, Thickness? kenar = null)
        {
            Grid.SetColumn(e, sutun);
            if (kenar.HasValue && e is FrameworkElement fe) fe.Margin = kenar.Value;
            g.Children.Add(e);
        }

        // ================= Kısayollar sayfası =================

        private Eylem? yakalananEylem;
        private Button yakalananDugme;

        private static readonly Dictionary<Eylem, string> EylemAciklamalari = new Dictionary<Eylem, string>
        {
            { Eylem.SonrakiProfil, "Profil listesinde bir sonrakine geçer." },
            { Eylem.OncekiProfil, "Bir önceki profile geçer." },
            { Eylem.IsiklariAcKapat, "Tüm ışıkları kapatır ya da geri açar." },
            { Eylem.ParlaklikArtir, "Etkin profilin parlaklığını %10 artırır." },
            { Eylem.ParlaklikAzalt, "Etkin profilin parlaklığını %10 azaltır." },
            { Eylem.SonrakiEfekt, "Etkin profilin efektini sırayla değiştirir." },
        };

        private void KisayollarSayfasiniKur()
        {
            KisayolAnahtari.Click += delegate
            {
                Ayar.KisayollarEtkin = KisayolAnahtari.IsChecked == true;
                Ana.KisayollariYenile();
                Ana.Degisti();
                KisayollariYukle();
            };
            KisayolSifirlaDugmesi.Click += delegate
            {
                Ayar.Kisayollar = Kisayol.Varsayilanlar();
                Ana.KisayollariYenile();
                Ana.Degisti();
                KisayollariYukle();
            };
            PreviewKeyDown += KisayolYakala;
            Deactivated += delegate { YakalamayiBitir(); };
        }

        private void KisayollariYukle()
        {
            KisayolAnahtari.IsChecked = Ayar.KisayollarEtkin;
            KisayolListesi.Children.Clear();
            KisayolListesi.IsEnabled = Ayar.KisayollarEtkin;
            KisayolListesi.Opacity = Ayar.KisayollarEtkin ? 1 : 0.5;

            Eylem[] eylemler = (Eylem[])Enum.GetValues(typeof(Eylem));
            for (int i = 0; i < eylemler.Length; i++)
            {
                Eylem e = eylemler[i];
                Kisayol k = Ayar.KisayolBul(e);

                Grid satir = new Grid { Margin = new Thickness(0, i == 0 ? 0 : 12, 0, 0) };
                satir.ColumnDefinitions.Add(new ColumnDefinition());
                satir.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                StackPanel sol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                sol.Children.Add(new TextBlock { Text = App.EylemAdi(e), FontSize = 14 });
                sol.Children.Add(new TextBlock { Text = EylemAciklamalari[e], Style = (Style)FindResource("Aciklama") });
                if (Ayar.KisayollarEtkin && Ana.KisayolHatalari.Contains(e))
                    sol.Children.Add(new TextBlock
                    {
                        Text = "Bu kombinasyonu başka bir program kullanıyor; farklı bir tane seçin.",
                        FontSize = 12, Foreground = (Brush)FindResource("Uyari"), Margin = new Thickness(0, 3, 0, 0)
                    });
                satir.Children.Add(sol);

                Button dugme = new Button
                {
                    Style = (Style)FindResource("TemelDugme"), MinWidth = 200, Padding = new Thickness(14, 8, 14, 8),
                    FontFamily = new FontFamily("Segoe UI Semibold"), Content = KisayolMetni(k),
                    Foreground = k.Tus == 0 ? (Brush)FindResource("SolukMetin") : (Brush)FindResource("Metin")
                };
                dugme.Click += delegate { YakalamayaBasla(e, dugme); };
                Grid.SetColumn(dugme, 1);
                satir.Children.Add(dugme);

                if (i > 0)
                    KisayolListesi.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Cizgi"), Margin = new Thickness(0, 12, 0, 0) });
                KisayolListesi.Children.Add(satir);
            }
        }

        private void YakalamayaBasla(Eylem e, Button dugme)
        {
            YakalamayiBitir();
            yakalananEylem = e;
            yakalananDugme = dugme;
            Ana.KisayollariDuraklat();
            dugme.Content = "Tuşlara basın…";
            dugme.BorderBrush = (Brush)FindResource("Vurgu");
            dugme.Foreground = (Brush)FindResource("VurguAcik");
        }

        private void YakalamayiBitir()
        {
            if (yakalananEylem == null) return;
            yakalananEylem = null;
            yakalananDugme = null;
            Ana.KisayollariYenile();
            KisayollariYukle();
        }

        private void KisayolYakala(object sender, KeyEventArgs e)
        {
            if (yakalananEylem == null) return;
            e.Handled = true;
            Key tus = e.Key == Key.System ? e.SystemKey : e.Key;

            int degistirici = Degistiriciler();
            if (tus == Key.LeftCtrl || tus == Key.RightCtrl || tus == Key.LeftAlt || tus == Key.RightAlt ||
                tus == Key.LeftShift || tus == Key.RightShift || tus == Key.LWin || tus == Key.RWin)
            {
                // Yalnızca değiştirici tuşa basıldı: kombinasyonun o ana kadarki halini göster.
                yakalananDugme.Content = KisayolMetni(new Kisayol { Degistirici = degistirici }) + " + …";
                return;
            }
            if (tus == Key.Escape) { YakalamayiBitir(); return; }

            Kisayol k = Ayar.KisayolBul(yakalananEylem.Value);
            if ((tus == Key.Delete || tus == Key.Back) && degistirici == 0)
            {
                k.Tus = 0;
                k.Degistirici = 0;
            }
            else
            {
                bool fonksiyonTusu = tus >= Key.F1 && tus <= Key.F24;
                if ((degistirici & (Kisayol.Ctrl | Kisayol.Alt | Kisayol.Win)) == 0 && !fonksiyonTusu)
                {
                    yakalananDugme.Content = "Ctrl, Alt veya Win ekleyin";
                    return;
                }
                int vk = KeyInterop.VirtualKeyFromKey(tus);
                // Aynı kombinasyon başka bir eyleme atanmışsa oradan kaldır.
                foreach (Kisayol diger in Ayar.Kisayollar)
                    if (diger != k && diger.Tus == vk && diger.Degistirici == degistirici)
                        diger.Tus = diger.Degistirici = 0;
                k.Tus = vk;
                k.Degistirici = degistirici;
            }
            Ana.Degisti();
            YakalamayiBitir();
        }

        private static int Degistiriciler()
        {
            int d = 0;
            ModifierKeys m = Keyboard.Modifiers;
            if ((m & ModifierKeys.Alt) != 0) d |= Kisayol.Alt;
            if ((m & ModifierKeys.Control) != 0) d |= Kisayol.Ctrl;
            if ((m & ModifierKeys.Shift) != 0) d |= Kisayol.Shift;
            if ((m & ModifierKeys.Windows) != 0 || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) d |= Kisayol.Win;
            return d;
        }

        public static string KisayolMetni(Kisayol k)
        {
            List<string> parcalar = new List<string>();
            if ((k.Degistirici & Kisayol.Ctrl) != 0) parcalar.Add("Ctrl");
            if ((k.Degistirici & Kisayol.Alt) != 0) parcalar.Add("Alt");
            if ((k.Degistirici & Kisayol.Shift) != 0) parcalar.Add("Shift");
            if ((k.Degistirici & Kisayol.Win) != 0) parcalar.Add("Win");
            if (k.Tus != 0) parcalar.Add(TusAdi(KeyInterop.KeyFromVirtualKey(k.Tus)));
            if (parcalar.Count == 0) return "Atanmamış";
            return string.Join(" + ", parcalar);
        }

        private static string TusAdi(Key t)
        {
            switch (t)
            {
                case Key.Left: return "←";
                case Key.Right: return "→";
                case Key.Up: return "↑";
                case Key.Down: return "↓";
                case Key.PageUp: return "Page Up";
                case Key.PageDown: return "Page Down";
                case Key.Space: return "Boşluk";
                case Key.Return: return "Enter";
                case Key.Back: return "Backspace";
                case Key.Add: return "Num +";
                case Key.Subtract: return "Num −";
                case Key.Multiply: return "Num *";
                case Key.Divide: return "Num /";
                case Key.Decimal: return "Num ,";
            }
            if (t >= Key.D0 && t <= Key.D9) return ((int)(t - Key.D0)).ToString();
            if (t >= Key.NumPad0 && t <= Key.NumPad9) return "Num " + (int)(t - Key.NumPad0);
            return t.ToString();
        }
    }
}
