using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WF = System.Windows.Forms;

namespace K55Kontrol
{
    // Uygulamanın merkezi: ayarlar, efekt motoru, otomatik profil kaynakları, ışık kapatma, kısayollar ve sistem tepsisi.
    //
    // Etkin profilin belirlenme sırası:
    //   1. Işıkları kapatma nedeni varsa (kilit, ekran kapalı, boşta, elle) klavye karartılır.
    //   2. Profile bağlı bir program açıksa o profil.
    //   3. Saati gelmiş bir zamanlama kuralı varsa onun profili.
    //   4. Kullanıcının seçtiği profil.
    // Kullanıcı 2 ya da 3 etkinken elle başka profil seçerse, o otomatik kaynak değişene kadar seçimi geçerli olur.
    public partial class App : Application
    {
        public static App Ana => (App)Current;

        public Ayarlar Ayarlar { get; private set; }
        public EfektMotoru Motor { get; private set; }
        public UygulamaIzleyici Izleyici { get; private set; }
        public SistemOlaylari Sistem { get; private set; }
        public EfektKaynaklari Kaynaklar { get; private set; }
        public GTuslari GTuslari { get; private set; }

        // Bir G tuşuna basıldığında (arayüz iş parçacığında) tetiklenir; Makrolar sayfası canlı gösterim için kullanır.
        public event Action<int> GTusuBasildi;

        // Profil, otomatik geçiş, karartma veya bağlantı durumu değişince (arayüz iş parçacığında) tetiklenir.
        public event Action DurumDegisti;

        public HashSet<Eylem> KisayolHatalari { get; private set; } = new HashSet<Eylem>();

        private static readonly Profil KarartmaProfili = new Profil { Ad = "Işıklar kapalı", Mod = Mod.Kapali };

        private Mutex tekKopya;
        private EventWaitHandle gosterSinyali;
        private WF.NotifyIcon tepsi;
        private AnaPencere pencere;
        private EkranBildirimi bildirim;
        private DispatcherTimer kayitZamanlayici;
        private DispatcherTimer saniyeZamanlayici;
        private Profil bastirilanOtomatik;
        private ZamanKurali etkinKural;
        private volatile Profil etkinOnbellek;
        private bool kilitli, ekranKapali, bosta, elleKapali;

        private const string TekKopyaAdi = "K55Kontrol.TekKopya";
        private const string GosterAdi = "K55Kontrol.Goster";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool yeni;
            tekKopya = new Mutex(true, TekKopyaAdi, out yeni);
            if (!yeni)
            {
                // Zaten çalışan kopyaya penceresini göstermesini söyle.
                try { EventWaitHandle.OpenExisting(GosterAdi).Set(); } catch (Exception) { }
                Shutdown();
                return;
            }
            gosterSinyali = new EventWaitHandle(false, EventResetMode.AutoReset, GosterAdi);
            Thread dinleyici = new Thread(delegate ()
            {
                while (gosterSinyali.WaitOne())
                    Dispatcher.BeginInvoke(new Action(PencereyiGoster));
            });
            dinleyici.IsBackground = true;
            dinleyici.Start();

            Ayarlar = Ayarlar.Yukle();
            kayitZamanlayici = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            kayitZamanlayici.Tick += delegate { kayitZamanlayici.Stop(); Ayarlar.Kaydet(); };

            etkinKural = EtkinKuraliBul();
            etkinOnbellek = Ayarlar.SeciliProfil;
            Kaynaklar = new EfektKaynaklari();
            Motor = new EfektMotoru(() => etkinOnbellek, Kaynaklar);
            Motor.BaglantiDegisti += delegate { Dispatcher.BeginInvoke(new Action(DurumuBildir)); };

            Izleyici = new UygulamaIzleyici(() => Ayarlar.Profiller);
            Izleyici.Degisti += delegate { Dispatcher.BeginInvoke(new Action(Yenile)); };

            GTuslari = new GTuslari();
            GTuslari.Basildi += n => Dispatcher.BeginInvoke(new Action(delegate { GTusuIsle(n); }));

            Sistem = new SistemOlaylari();
            Sistem.KilitDegisti += k => { kilitli = k; Yenile(); };
            Sistem.EkranDegisti += acik => { ekranKapali = !acik; Yenile(); };
            Sistem.KisayolBasildi += EylemUygula;
            KisayollariYenile();

            // Zamanlama ve boşta kalma her saniye denetlenir.
            saniyeZamanlayici = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            saniyeZamanlayici.Tick += delegate { ZamaniDenetle(); };
            saniyeZamanlayici.Start();

            bildirim = new EkranBildirimi();
            TepsiyiKur();
            Yenile();

            pencere = new AnaPencere();
            if (!e.Args.Any(a => a.Equals("/gizli", StringComparison.OrdinalIgnoreCase)))
                pencere.Show();
        }

        // ================= Etkin profil =================

        public Profil EtkinProfil => OtomatikProfil ?? Ayarlar.SeciliProfil;

        // Bastırılmamış otomatik profil (program ya da zamanlama), yoksa null.
        public Profil OtomatikProfil
        {
            get
            {
                Profil aday = OtomatikAday(out string neden);
                return aday == bastirilanOtomatik ? null : aday;
            }
        }

        // Otomatik profilin neden etkin olduğunu anlatan cümle (arayüzde gösterilir).
        public string OtomatikNeden
        {
            get
            {
                Profil aday = OtomatikAday(out string neden);
                return aday == null || aday == bastirilanOtomatik ? null : neden;
            }
        }

        private Profil OtomatikAday(out string neden)
        {
            neden = null;
            if (Izleyici != null && Ayarlar.OtomatikGecis)
            {
                Profil p = Izleyici.EslesenProfil;
                if (p != null && Ayarlar.Profiller.Contains(p))
                {
                    neden = "“" + Izleyici.EslesenUygulama + "” açık olduğu için “" + p.Ad + "” profili otomatik olarak etkin.";
                    return p;
                }
            }
            if (etkinKural != null)
            {
                Profil p = Ayarlar.Bul(etkinKural.ProfilAdi);
                if (p != null)
                {
                    neden = "Zamanlama (" + ZamanKurali.SaatYaz(etkinKural.Baslangic) + "–" + ZamanKurali.SaatYaz(etkinKural.Bitis) +
                            ") nedeniyle “" + p.Ad + "” profili etkin.";
                    return p;
                }
            }
            return null;
        }

        private ZamanKurali EtkinKuraliBul()
        {
            DateTime simdi = DateTime.Now;
            return Ayarlar.Zamanlamalar.FirstOrDefault(k => k.SimdiEtkin(simdi) && Ayarlar.Bul(k.ProfilAdi) != null);
        }

        // Işıkların neden geçici olarak kapalı olduğu; kapalı değilse null.
        public string KarartmaNedeni
        {
            get
            {
                if (elleKapali) return "Işıkları kısayol ya da tepsi menüsüyle kapattınız.";
                if (kilitli && Ayarlar.KilitteKapat) return "Ekran kilitli olduğu için ışıklar kapalı.";
                if (ekranKapali && Ayarlar.EkranKapanincaKapat) return "Monitör kapalı olduğu için ışıklar kapalı.";
                if (bosta && Ayarlar.BostaKapat) return "Bilgisayar " + Ayarlar.BostaDakika + " dakikadır boşta olduğu için ışıklar kapalı.";
                return null;
            }
        }

        public bool ElleKapali => elleKapali;

        public void ProfilSec(Profil p)
        {
            Ayarlar.SeciliProfil = p;
            Profil aday = OtomatikAday(out string neden);
            bastirilanOtomatik = (aday != null && aday != p) ? aday : null;
            Degisti();
        }

        public void IsiklariAcKapat(bool kapat)
        {
            elleKapali = kapat;
            Yenile();
        }

        // Ayar değişikliklerinden sonra çağrılır: klavyeyi günceller, kısa bir gecikmeyle diske kaydeder.
        public void Degisti()
        {
            kayitZamanlayici.Stop();
            kayitZamanlayici.Start();
            etkinKural = EtkinKuraliBul();
            Yenile();
        }

        public void ProfillerDegisti()
        {
            Izleyici.HemenTara();
            Degisti();
        }

        // Efekt motoru profil listesine dokunmaz; etkin profili yalnızca arayüz iş parçacığında hesaplayıp ona veririz.
        private void Yenile()
        {
            Profil aday = OtomatikAday(out string neden);
            if (aday != bastirilanOtomatik) bastirilanOtomatik = null;   // otomatik kaynak değişti: elle seçim bastırması biter
            etkinOnbellek = KarartmaNedeni != null ? KarartmaProfili : EtkinProfil;
            Kaynaklar.ModaGoreAyarla(etkinOnbellek.Mod);
            Motor.Guncelle();
            DurumuBildir();
        }

        private void ZamaniDenetle()
        {
            bool yeniBosta = Ayarlar.BostaKapat && SistemOlaylari.BostaSure().TotalMinutes >= Ayarlar.BostaDakika;
            ZamanKurali yeniKural = EtkinKuraliBul();
            if (yeniBosta != bosta || yeniKural != etkinKural)
            {
                bosta = yeniBosta;
                etkinKural = yeniKural;
                Yenile();
            }
        }

        private void DurumuBildir()
        {
            Action a = DurumDegisti;
            if (a != null) a();
            if (tepsi != null)
                tepsi.Text = Kisalt("K55 Kontrol – " + (KarartmaNedeni != null ? "ışıklar kapalı" : EtkinProfil.Ad), 63);
        }

        private static string Kisalt(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        // ================= Kısayollar =================

        public void KisayollariYenile()
        {
            KisayolHatalari = Sistem.KisayollariKaydet(Ayarlar.Kisayollar, Ayarlar.KisayollarEtkin);
        }

        // Kısayol atanırken mevcut kısayollar devre dışı kalır ki basılan tuşlar eylem tetiklemesin.
        public void KisayollariDuraklat() => Sistem.KisayollariBirak();

        public static string EylemAdi(Eylem e)
        {
            switch (e)
            {
                case Eylem.SonrakiProfil: return "Sonraki profil";
                case Eylem.OncekiProfil: return "Önceki profil";
                case Eylem.IsiklariAcKapat: return "Işıkları aç / kapat";
                case Eylem.ParlaklikArtir: return "Parlaklığı artır";
                case Eylem.ParlaklikAzalt: return "Parlaklığı azalt";
                default: return "Sonraki efekt";
            }
        }

        public void EylemUygula(Eylem e)
        {
            List<Profil> liste = Ayarlar.Profiller;
            Profil etkin = EtkinProfil;
            switch (e)
            {
                case Eylem.SonrakiProfil:
                case Eylem.OncekiProfil:
                {
                    int i = liste.IndexOf(etkin);
                    int adim = e == Eylem.SonrakiProfil ? 1 : -1;
                    Profil yeni = liste[((i + adim) % liste.Count + liste.Count) % liste.Count];
                    elleKapali = false;
                    ProfilSec(yeni);
                    bildirim.Goster("", yeni.Ad, Efektler.Ad(yeni.Mod));
                    break;
                }
                case Eylem.IsiklariAcKapat:
                    IsiklariAcKapat(!elleKapali);
                    bildirim.Goster("", elleKapali ? "Işıklar kapatıldı" : "Işıklar açıldı", elleKapali ? null : etkin.Ad);
                    break;
                case Eylem.ParlaklikArtir:
                case Eylem.ParlaklikAzalt:
                    etkin.Parlaklik = Math.Max(0, Math.Min(100, etkin.Parlaklik + (e == Eylem.ParlaklikArtir ? 10 : -10)));
                    elleKapali = false;
                    Degisti();
                    bildirim.Goster("", "Parlaklık %" + etkin.Parlaklik, etkin.Ad);
                    break;
                case Eylem.SonrakiEfekt:
                {
                    Mod[] sira = { Mod.Sabit, Mod.Bolgeler, Mod.Nefes, Mod.RenkDongusu, Mod.GokkusagiDalgasi,
                                   Mod.Muzik, Mod.Ekran, Mod.TusTepkisi, Mod.Gosterge };
                    int i = Array.IndexOf(sira, etkin.Mod);
                    etkin.Mod = sira[(i + 1) % sira.Length];
                    elleKapali = false;
                    Degisti();
                    bildirim.Goster("", Efektler.Ad(etkin.Mod), etkin.Ad);
                    break;
                }
            }
        }

        // ================= G tuşları =================

        // Makrolar sayfası bir G tuşunun kombinasyonunu ya da makrosunu kaydederken atamalar çalıştırılmaz.
        public bool GAtamalariDuraklatildi { get; set; }

        private void GTusuIsle(int n)
        {
            Action<int> olay = GTusuBasildi;
            if (olay != null) olay(n);
            if (GAtamalariDuraklatildi) return;

            GAtama a = Ayarlar.GAtamalari.FirstOrDefault(x => x.Tus == n);
            if (a == null) return;
            try
            {
                switch (a.Tur)
                {
                    case GTuru.TusKombinasyonu:
                        if (a.Vk != 0) MakroCalistirici.TusKombinasyonu(a.Vk, a.Degistirici);
                        break;
                    case GTuru.Metin:
                        MakroCalistirici.MetinYaz(a.Metin);
                        break;
                    case GTuru.Program:
                        MakroCalistirici.ProgramAc(a.Yol);
                        break;
                    case GTuru.Medya:
                        MakroCalistirici.Medya(a.Medya);
                        break;
                    case GTuru.Makro:
                        MakroCalistirici.Oynat(a.Adimlar);
                        break;
                    case GTuru.UygulamaEylemi:
                        UygulamaEyleminiCalistir(a.UygulamaEylemi);
                        break;
                }
            }
            catch (Exception ex)
            {
                bildirim.Goster("", "G" + n + " çalıştırılamadı", ex.Message);
            }
        }

        private void UygulamaEyleminiCalistir(string anahtar)
        {
            if (string.IsNullOrEmpty(anahtar)) return;
            if (anahtar.StartsWith("eylem:"))
            {
                Eylem e;
                if (Enum.TryParse(anahtar.Substring(6), out e)) EylemUygula(e);
            }
            else if (anahtar.StartsWith("profil:"))
            {
                Profil p = Ayarlar.Bul(anahtar.Substring(7));
                if (p == null) return;
                elleKapali = false;
                ProfilSec(p);
                bildirim.Goster("", p.Ad, Efektler.Ad(p.Mod));
            }
        }

        // ================= Pencere ve tepsi =================

        public void PencereyiGoster()
        {
            if (pencere == null) return;
            pencere.Show();
            if (pencere.WindowState == WindowState.Minimized) pencere.WindowState = WindowState.Normal;
            pencere.Activate();
        }

        public void Cik()
        {
            Ayarlar.Kaydet();
            Shutdown();
        }

        private void TepsiyiKur()
        {
            tepsi = new WF.NotifyIcon();
            try
            {
                using (var akis = GetResourceStream(new Uri("pack://application:,,,/assets/K55Kontrol.ico")).Stream)
                    tepsi.Icon = new System.Drawing.Icon(akis, WF.SystemInformation.SmallIconSize);
            }
            catch (Exception) { tepsi.Icon = System.Drawing.SystemIcons.Application; }

            WF.ContextMenuStrip menu = new WF.ContextMenuStrip();
            menu.Opening += delegate
            {
                menu.Items.Clear();
                WF.ToolStripItem ac = menu.Items.Add("K55 Kontrol'ü aç", null, delegate { PencereyiGoster(); });
                ac.Font = new System.Drawing.Font(ac.Font, System.Drawing.FontStyle.Bold);
                menu.Items.Add(new WF.ToolStripSeparator());
                Profil etkin = EtkinProfil;
                foreach (Profil p in Ayarlar.Profiller)
                {
                    Profil secilen = p;
                    WF.ToolStripMenuItem oge = new WF.ToolStripMenuItem(p.Ad, null, delegate { ProfilSec(secilen); });
                    oge.Checked = p == etkin;
                    menu.Items.Add(oge);
                }
                menu.Items.Add(new WF.ToolStripSeparator());
                menu.Items.Add(elleKapali ? "Işıkları aç" : "Işıkları kapat", null, delegate { IsiklariAcKapat(!elleKapali); });
                menu.Items.Add("Çıkış", null, delegate { Cik(); });
            };
            menu.Items.Add("…");   // Opening olayının tetiklenmesi için menü boş olmamalı

            tepsi.ContextMenuStrip = menu;
            tepsi.Text = "K55 Kontrol";
            tepsi.DoubleClick += delegate { PencereyiGoster(); };
            tepsi.MouseClick += (s, a) => { if (a.Button == WF.MouseButtons.Left) PencereyiGoster(); };
            tepsi.Visible = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (tepsi != null) tepsi.Visible = false;
            if (Sistem != null) Sistem.Dispose();
            if (GTuslari != null) GTuslari.Dispose();
            if (Izleyici != null) Izleyici.Dispose();
            if (Motor != null) Motor.Dispose();     // klavyeyi kendi moduna bırakır
            if (Kaynaklar != null) Kaynaklar.Dispose();
            if (Ayarlar != null) Ayarlar.Kaydet();
            base.OnExit(e);
        }
    }
}
