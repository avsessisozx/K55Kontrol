using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace K55Kontrol
{
    // Klavye hafızası sayfası: uygulama kapalıyken klavyenin göstereceği aydınlatmayı ayarlar ve klavyeye yazar.
    public partial class AnaPencere
    {
        private static readonly DonanimEfekti[] DonanimEfektleri =
        {
            DonanimEfekti.Sabit, DonanimEfekti.GokkusagiDalgasi, DonanimEfekti.RenkKaymasi, DonanimEfekti.RenkNabzi,
            DonanimEfekti.RenkDalgasi, DonanimEfekti.Vizor, DonanimEfekti.TusaTepki, DonanimEfekti.Suluboya
        };

        private DonanimAydinlatmasi Hafiza => Ayar.Hafiza;

        private void HafizaSayfasiniKur()
        {
            foreach (DonanimEfekti e in DonanimEfektleri)
                HafizaEfektKutusu.Items.Add(new ComboBoxItem { Content = DonanimAydinlatmasi.Ad(e), Tag = e });

            HafizaOtomatikAnahtari.Click += delegate
            {
                Ayar.HafizaOtomatik = HafizaOtomatikAnahtari.IsChecked == true;
                Ana.Degisti();
                HafizaSayfasiniYukle();
            };
            HafizaEfektKutusu.SelectionChanged += delegate
            {
                if (yukleniyor || !(HafizaEfektKutusu.SelectedItem is ComboBoxItem oge)) return;
                Hafiza.Efekt = (DonanimEfekti)oge.Tag;
                HafizaDegisti();
            };

            HafizaSecimiBagla(HafizaRastgele, () => Hafiza.RastgeleRenkler = true);
            HafizaSecimiBagla(HafizaSecili, () => Hafiza.RastgeleRenkler = false);
            HafizaSecimiBagla(HafizaYavas, () => Hafiza.Hiz = DonanimHizi.Yavas);
            HafizaSecimiBagla(HafizaOrta, () => Hafiza.Hiz = DonanimHizi.Orta);
            HafizaSecimiBagla(HafizaHizli, () => Hafiza.Hiz = DonanimHizi.Hizli);
            HafizaSecimiBagla(HafizaSol, () => Hafiza.Yon = DonanimYonu.Sol);
            HafizaSecimiBagla(HafizaSag, () => Hafiza.Yon = DonanimYonu.Sag);
            HafizaSecimiBagla(HafizaYukari, () => Hafiza.Yon = DonanimYonu.Yukari);
            HafizaSecimiBagla(HafizaAsagi, () => Hafiza.Yon = DonanimYonu.Asagi);
            HafizaRenk1Secimi.Checked += delegate { if (!yukleniyor) HafizaSayfasiniYukle(); };
            HafizaRenk2Secimi.Checked += delegate { if (!yukleniyor) HafizaSayfasiniYukle(); };

            HafizaParlaklikCubugu.ValueChanged += delegate
            {
                HafizaParlaklikDegeri.Text = "%" + (int)HafizaParlaklikCubugu.Value;
                if (yukleniyor) return;
                Hafiza.Parlaklik = (int)Math.Round(HafizaParlaklikCubugu.Value);
                Ana.Degisti();
            };
            HafizaRenkSecici.RenkDegisti += c =>
            {
                if (HafizaRenk2Secimi.IsChecked == true && HafizaRenkHedefi.Visibility == Visibility.Visible) Hafiza.Renk2 = c;
                else Hafiza.Renk1 = c;
                Ana.Degisti();
            };

            HafizaProfildenDugmesi.Click += delegate
            {
                DonanimAydinlatmasi d = DonanimAydinlatmasi.ProfildenOlustur(Ana.EtkinProfil);
                if (d == null)
                {
                    HafizaDurumuYaz("Etkin profil “Klavyenin kendi modu” kullanıyor; doldurulacak bir ayar yok.", "IkincilMetin");
                    return;
                }
                Ayar.Hafiza = d;
                Ana.Degisti();
                HafizaSayfasiniYukle();
            };

            HafizaKaydetDugmesi.Click += delegate
            {
                DonanimAydinlatmasi d = Ayar.HafizaOtomatik ? DonanimAydinlatmasi.ProfildenOlustur(Ayar.SeciliProfil) : Hafiza;
                if (d == null)
                {
                    HafizaDurumuYaz("Seçili profil “Klavyenin kendi modu” kullanıyor; hafıza olduğu gibi bırakıldı.", "IkincilMetin");
                    return;
                }
                HafizaKaydetDugmesi.IsEnabled = false;
                HafizaDurumuYaz("Klavyeye yazılıyor…", "IkincilMetin");
                Ana.HafizayaYaz(d, true);
            };
            HafizaGosterDugmesi.Click += delegate
            {
                Ana.Motor.DonanimOnizle(TimeSpan.FromSeconds(5));
                HafizaDurumuYaz("Klavye 5 saniye boyunca hafızadaki aydınlatmayı gösteriyor.", "IkincilMetin");
            };

            Ana.HafizaYazildi += hata =>
            {
                HafizaKaydetDugmesi.IsEnabled = true;
                if (hata == null) HafizaDurumuYaz("✓ Klavyeye kaydedildi (" + DateTime.Now.ToString("HH:mm") + ").", "Basari");
                else HafizaDurumuYaz("Kaydedilemedi: " + hata, "Tehlike");
                if (SayfaHafiza.IsVisible) HafizaOzetiniYaz();
            };
        }

        private void HafizaSecimiBagla(RadioButton r, Action uygula)
        {
            r.Checked += delegate
            {
                if (yukleniyor) return;
                uygula();
                HafizaDegisti();
            };
        }

        private void HafizaDegisti()
        {
            Ana.Degisti();
            HafizaSayfasiniYukle();
        }

        private void HafizaDurumuYaz(string metin, string renk)
        {
            HafizaDurumu.Text = metin;
            HafizaDurumu.Foreground = (Brush)FindResource(renk);
        }

        private void HafizaOzetiniYaz()
        {
            if (Ayar.HafizaOtomatik)
            {
                Profil p = Ayar.SeciliProfil;
                DonanimAydinlatmasi d = DonanimAydinlatmasi.ProfildenOlustur(p);
                HafizaOtomatikOzet.Text = d == null
                    ? "Seçili profil “" + p.Ad + "” klavyenin kendi modunu kullanıyor; hafıza değiştirilmez."
                    : "Kaydedilecek: “" + p.Ad + "” profili → " + d.Ozet();
            }
            else
            {
                HafizaOtomatikOzet.Text = "Otomatik kayıt kapalı. Kaydedilecek ayar: " + Hafiza.Ozet();
            }
        }

        private void HafizaSayfasiniYukle()
        {
            yukleniyor = true;
            try
            {
                DonanimAydinlatmasi d = Hafiza;
                HafizaOtomatikAnahtari.IsChecked = Ayar.HafizaOtomatik;
                HafizaOzetiniYaz();

                bool elle = !Ayar.HafizaOtomatik;
                HafizaElleKart.Opacity = elle ? 1 : 0.55;
                HafizaElleNotu.Visibility = elle ? Visibility.Collapsed : Visibility.Visible;
                foreach (UIElement e in new UIElement[] { HafizaEfektKutusu, HafizaRenkTuruAlani, HafizaRenkHedefi, HafizaHizAlani,
                                                          HafizaYonAlani, HafizaParlaklikCubugu, HafizaProfildenDugmesi, HafizaRenkAlani })
                    e.IsEnabled = elle;

                HafizaEfektKutusu.SelectedItem = HafizaEfektKutusu.Items.OfType<ComboBoxItem>().First(o => (DonanimEfekti)o.Tag == d.Efekt);

                bool renkSecimli = DonanimAydinlatmasi.RenkSecimliMi(d.Efekt);
                bool tekRenk = d.Efekt == DonanimEfekti.Sabit || d.Efekt == DonanimEfekti.Suluboya;
                bool ikiRenk = renkSecimli && !d.RastgeleRenkler;
                HafizaRenkTuruAlani.Visibility = renkSecimli ? Visibility.Visible : Visibility.Collapsed;
                HafizaRastgele.IsChecked = d.RastgeleRenkler;
                HafizaSecili.IsChecked = !d.RastgeleRenkler;
                HafizaRenkHedefi.Visibility = ikiRenk ? Visibility.Visible : Visibility.Collapsed;
                HafizaRenkAlani.Visibility = tekRenk || ikiRenk ? Visibility.Visible : Visibility.Collapsed;
                bool ikinci = ikiRenk && HafizaRenk2Secimi.IsChecked == true;
                HafizaRenkBasligi.Text = d.Efekt == DonanimEfekti.Suluboya ? "ARKA PLAN RENGİ" : ikiRenk ? (ikinci ? "2. RENK" : "1. RENK") : "RENK";
                HafizaRenkSecici.Renk = ikinci ? d.Renk2 : d.Renk1;

                HafizaHizAlani.Visibility = DonanimAydinlatmasi.HiziVar(d.Efekt) ? Visibility.Visible : Visibility.Collapsed;
                HafizaYavas.IsChecked = d.Hiz == DonanimHizi.Yavas;
                HafizaOrta.IsChecked = d.Hiz == DonanimHizi.Orta;
                HafizaHizli.IsChecked = d.Hiz == DonanimHizi.Hizli;

                HafizaYonAlani.Visibility = DonanimAydinlatmasi.YonuVar(d.Efekt) ? Visibility.Visible : Visibility.Collapsed;
                HafizaSol.IsChecked = d.Yon == DonanimYonu.Sol;
                HafizaSag.IsChecked = d.Yon == DonanimYonu.Sag;
                HafizaYukari.IsChecked = d.Yon == DonanimYonu.Yukari;
                HafizaAsagi.IsChecked = d.Yon == DonanimYonu.Asagi;

                HafizaParlaklikCubugu.Value = d.Parlaklik;
                HafizaParlaklikDegeri.Text = "%" + d.Parlaklik;
            }
            finally { yukleniyor = false; }
        }
    }
}
