using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace K55Kontrol
{
    // Aydınlatma sayfasında dış veriye dayanan efektlerin (müzik, tuşa tepki, sistem göstergesi) ayarları.
    public partial class AnaPencere
    {
        private readonly DispatcherTimer gostergeZamanlayici = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        private bool kaynaklarDolduruldu;

        private void YeniEfektAyarlariniKur()
        {
            GokkusagiAnahtari.Click += delegate
            {
                Ana.EtkinProfil.MuzikGokkusagi = GokkusagiAnahtari.IsChecked == true;
                Ana.Degisti();
            };
            TabanRengiSecimi.Checked += delegate { if (!yukleniyor) AydinlatmayiYukle(); };
            TepkiRengiSecimi.Checked += delegate { if (!yukleniyor) AydinlatmayiYukle(); };

            GostergeKaynakKutusu.DropDownOpened += delegate { KaynaklariDoldur(); };
            GostergeKaynakKutusu.SelectionChanged += delegate
            {
                if (yukleniyor) return;
                var k = (GostergeKaynakKutusu.SelectedItem as ComboBoxItem)?.Tag as SistemGostergesi.Kaynak;
                if (k == null) return;
                Profil p = Ana.EtkinProfil;
                p.GostergeKaynagi = k.Anahtar;
                p.GostergeAlt = (int)k.VarsayilanAlt;
                p.GostergeUst = (int)k.VarsayilanUst;
                Ana.Degisti();
            };
            AralikKutusunuKur(GostergeAltKutusu, true);
            AralikKutusunuKur(GostergeUstKutusu, false);

            gostergeZamanlayici.Tick += delegate { GostergeSimdiyiYaz(); };
            gostergeZamanlayici.Start();
        }

        private void AralikKutusunuKur(TextBox kutu, bool alt)
        {
            Action uygula = delegate
            {
                Profil p = Ana.EtkinProfil;
                int deger;
                if (int.TryParse(kutu.Text.Trim(), out deger))
                {
                    int yeniAlt = alt ? deger : p.GostergeAlt, yeniUst = alt ? p.GostergeUst : deger;
                    if (yeniAlt < yeniUst)
                    {
                        p.GostergeAlt = yeniAlt;
                        p.GostergeUst = yeniUst;
                        Ana.Degisti();
                    }
                }
                kutu.Text = (alt ? p.GostergeAlt : p.GostergeUst).ToString();
            };
            kutu.LostKeyboardFocus += delegate { uygula(); };
            kutu.KeyDown += (s, e) => { if (e.Key == Key.Enter) { uygula(); kutu.SelectAll(); } };
        }

        // Kaynak listesini (CPU, RAM, GPU, HWiNFO sıcaklıkları) yeniden okur; seçimi korur.
        private void KaynaklariDoldur()
        {
            bool onceki = yukleniyor;
            yukleniyor = true;
            try
            {
                GostergeKaynakKutusu.Items.Clear();
                var kaynaklar = SistemGostergesi.Kaynaklar();
                foreach (var k in kaynaklar)
                    GostergeKaynakKutusu.Items.Add(new ComboBoxItem { Content = k.Ad, Tag = k });
                kaynaklarDolduruldu = true;

                bool hwinfo = kaynaklar.Any(k => k.Anahtar.StartsWith("hwinfo:"));
                GostergeNotu.Text = hwinfo ? ""
                    : "İşlemci sıcaklığı için HWiNFO'yu “Shared Memory Support” seçeneği açık olarak çalıştırın; sıcaklıklar bu listeye eklenir.";
                GostergeNotu.Visibility = hwinfo ? Visibility.Collapsed : Visibility.Visible;
                KaynagiSec(Ana.EtkinProfil);
            }
            finally { yukleniyor = onceki; }
        }

        private void KaynagiSec(Profil p)
        {
            var oge = GostergeKaynakKutusu.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => ((SistemGostergesi.Kaynak)i.Tag).Anahtar == p.GostergeKaynagi);
            if (oge == null)
            {
                // Seçili kaynak şu an yok (ör. HWiNFO kapalı): listede belirt ama seçimi değiştirme.
                oge = new ComboBoxItem
                {
                    Content = "Kullanılamıyor (" + p.GostergeKaynagi + ")",
                    Tag = new SistemGostergesi.Kaynak { Anahtar = p.GostergeKaynagi, Ad = "", Birim = "", AralikDuzenlenebilir = true },
                    IsEnabled = false
                };
                GostergeKaynakKutusu.Items.Add(oge);
            }
            GostergeKaynakKutusu.SelectedItem = oge;
        }

        private void GostergeyiYukle(Profil p)
        {
            if (!kaynaklarDolduruldu) KaynaklariDoldur();
            else KaynagiSec(p);

            var k = (GostergeKaynakKutusu.SelectedItem as ComboBoxItem)?.Tag as SistemGostergesi.Kaynak;
            bool duzenlenebilir = k != null && k.AralikDuzenlenebilir;
            GostergeAraligi.IsEnabled = duzenlenebilir;
            GostergeAraligi.Opacity = duzenlenebilir ? 1 : 0.5;
            GostergeBirimi.Text = k?.Birim ?? "";
            if (!GostergeAltKutusu.IsKeyboardFocused) GostergeAltKutusu.Text = p.GostergeAlt.ToString();
            if (!GostergeUstKutusu.IsKeyboardFocused) GostergeUstKutusu.Text = p.GostergeUst.ToString();
            GostergeSimdiyiYaz();
        }

        private void GostergeSimdiyiYaz()
        {
            if (!IsVisible) return;
            Profil p = Ana.EtkinProfil;
            if (p.Mod != Mod.Gosterge) return;
            var k = (GostergeKaynakKutusu.SelectedItem as ComboBoxItem)?.Tag as SistemGostergesi.Kaynak;
            double? v = Ana.Kaynaklar.Gosterge.Deger(p.GostergeKaynagi);
            GostergeSimdi.Text = v.HasValue ? "Şu an: " + Math.Round(v.Value) + " " + (k?.Birim ?? "") : "Veri bekleniyor…";
        }
    }
}
