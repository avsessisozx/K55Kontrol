using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace K55Kontrol
{
    // Arka planda çalışır: etkin profilin efektini hesaplar, klavyeye gönderir ve önizleme için kareleri yayınlar.
    // Klavye bağlı değilken de hesaplamaya devam eder (önizleme çalışsın diye) ve düzenli aralıklarla yeniden bağlanmayı dener.
    public sealed class EfektMotoru : IDisposable
    {
        public event EventHandler BaglantiDegisti;
        public event Action<Renk[]> KareHazir;     // arka plan iş parçacığından çağrılır

        private readonly Func<Profil> etkinProfil;
        private readonly EfektHesaplayici hesaplayici;
        private readonly Thread isParcacigi;
        private readonly AutoResetEvent uyandir = new AutoResetEvent(false);
        private volatile bool calisiyor = true;
        private volatile bool degisti = true;
        private K55 klavye;

        private const int KareSuresiMs = 33;
        private const int YenidenDenemeMs = 2000;
        private const int CanliTutmaMs = 10000;
        private const int GecisSuresiMs = 450;

        public EfektMotoru(Func<Profil> etkinProfil, EfektKaynaklari kaynaklar)
        {
            this.etkinProfil = etkinProfil;
            hesaplayici = new EfektHesaplayici(kaynaklar);
            isParcacigi = new Thread(Dongu);
            isParcacigi.IsBackground = true;
            isParcacigi.Name = "Efekt motoru";
            isParcacigi.Start();
        }

        public bool Bagli { get { K55 k = klavye; return k != null && k.Bagli; } }

        // Ayarlar değiştiğinde çağrılır; sabit modlarda da hemen yeniden gönderim yapılır.
        public void Guncelle()
        {
            degisti = true;
            uyandir.Set();
        }

        // Klavyenin hafızasındaki donanım aydınlatmasını yazar. Hata yoksa null, varsa açıklamasını döndürür.
        // Arayüzü bekletmemek için arka planda çağrılmalıdır; K55 kendi kilidiyle kare gönderimiyle sıraya girer.
        public string HafizayaYaz(byte[] veri)
        {
            K55 k = klavye;
            if (k == null || !k.Bagli) return "Klavye bağlı değil.";
            try
            {
                k.DonanimAydinlatmasiniYaz(veri);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
            finally
            {
                // Yazma klavyeyi yazılım moduna alır; döngü doğru modu yeniden kursun.
                yazilimaGecildi = true;
                Guncelle();
            }
        }

        // Klavyeyi kısa süreliğine kendi moduna alır ki hafızadaki efekt görülebilsin.
        public void DonanimOnizle(TimeSpan sure)
        {
            Interlocked.Exchange(ref onizlemeBitisi, saat.ElapsedMilliseconds + (long)sure.TotalMilliseconds);
            Guncelle();
        }

        public bool Onizlemede => saat.ElapsedMilliseconds < Interlocked.Read(ref onizlemeBitisi);

        private readonly Stopwatch saat = Stopwatch.StartNew();
        private long onizlemeBitisi;
        private volatile bool yazilimaGecildi;

        private void Dongu()
        {
            Renk[] son = null, cikti = null, gecisKaynagi = null;
            long sonGonderim = 0, sonDeneme = -YenidenDenemeMs, gecisBaslangici = 0;
            bool donanimda = false;
            Profil oncekiProfil = null;
            Mod oncekiMod = Mod.Sabit;

            while (calisiyor)
            {
                long simdi = saat.ElapsedMilliseconds;
                if (!Bagli && simdi - sonDeneme >= YenidenDenemeMs)
                {
                    sonDeneme = simdi;
                    bool oncedenVardi = klavye != null;
                    Kapat();
                    try { klavye = K55.Bul(); } catch (Exception) { klavye = null; }
                    if (klavye != null)
                    {
                        son = null;
                        donanimda = true;     // aşağıda yazılım moduna geçilmesini sağlar
                    }
                    if (oncedenVardi || klavye != null) Bildir();
                }

                Profil p = etkinProfil();
                Mod mod = p.Mod;
                Renk[] renkler = hesaplayici.Hesapla(p, saat.Elapsed.TotalSeconds);

                // Profil ya da efekt değişince eski renklerden yenilerine kısa bir geçiş yap.
                if (p != oncekiProfil || mod != oncekiMod)
                {
                    gecisKaynagi = cikti;
                    gecisBaslangici = simdi;
                    oncekiProfil = p;
                    oncekiMod = mod;
                }
                bool geciste = gecisKaynagi != null && simdi - gecisBaslangici < GecisSuresiMs;
                if (geciste)
                {
                    double t = (double)(simdi - gecisBaslangici) / GecisSuresiMs;
                    t = t * t * (3 - 2 * t);
                    for (int i = 0; i < renkler.Length; i++)
                        renkler[i] = Renk.Karistir(gecisKaynagi[i], renkler[i], t);
                }
                cikti = renkler;

                Action<Renk[]> kare = KareHazir;
                if (kare != null) kare(renkler);

                K55 k = klavye;
                if (k != null)
                {
                    try
                    {
                        if (yazilimaGecildi)
                        {
                            yazilimaGecildi = false;
                            donanimda = false;
                            son = null;
                        }
                        if (mod == Mod.Donanim || Onizlemede)
                        {
                            if (!donanimda)
                            {
                                k.DonanimModunaGec();
                                donanimda = true;
                            }
                        }
                        else
                        {
                            if (donanimda)
                            {
                                k.YazilimModunaGec();
                                donanimda = false;
                                son = null;
                            }
                            if (degisti || son == null || !Ayni(son, renkler) || simdi - sonGonderim > CanliTutmaMs)
                            {
                                k.RenkleriYaz(renkler);
                                son = renkler;
                                sonGonderim = simdi;
                            }
                        }
                        degisti = false;
                    }
                    catch (IOException) { Kapat(); Bildir(); }
                    catch (ObjectDisposedException) { Kapat(); Bildir(); }
                }

                uyandir.WaitOne(Efektler.Animasyonlu(mod) || geciste || Onizlemede ? KareSuresiMs : 250);
            }
            Kapat();
        }

        private static bool Ayni(Renk[] a, Renk[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        private void Kapat()
        {
            K55 k = klavye;
            klavye = null;
            if (k != null) k.Dispose();
        }

        private void Bildir()
        {
            EventHandler e = BaglantiDegisti;
            if (e != null) e(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            calisiyor = false;
            uyandir.Set();
            isParcacigi.Join(2000);
        }
    }

    // Efekt hesapları: zaman (saniye) ve profilden her bölgenin rengini üretir.
    public static class Efektler
    {
        public static bool Animasyonlu(Mod m) => m == Mod.Nefes || m == Mod.RenkDongusu || m == Mod.GokkusagiDalgasi
            || m == Mod.Muzik || m == Mod.Ekran || m == Mod.TusTepkisi || m == Mod.Gosterge;

        public static string Ad(Mod m)
        {
            switch (m)
            {
                case Mod.Sabit: return "Sabit renk";
                case Mod.Bolgeler: return "Bölge renkleri";
                case Mod.Nefes: return "Nefes";
                case Mod.RenkDongusu: return "Renk döngüsü";
                case Mod.GokkusagiDalgasi: return "Gökkuşağı dalgası";
                case Mod.Kapali: return "Kapalı";
                case Mod.Muzik: return "Müzik";
                case Mod.Ekran: return "Ekran rengi";
                case Mod.TusTepkisi: return "Tuşa tepki";
                case Mod.Gosterge: return "Sistem göstergesi";
                default: return "Klavyenin kendi modu";
            }
        }

        public static Renk[] Hesapla(Profil p, double t)
        {
            Renk[] r = new Renk[K55.BolgeSayisi];
            double hiz = 0.25 + p.Hiz * 0.15;   // 1 -> 0.4, 10 -> 1.75
            double parlaklik = p.Parlaklik / 100.0;
            for (int i = 0; i < r.Length; i++)
            {
                Renk c;
                switch (p.Mod)
                {
                    case Mod.Sabit:
                        c = p.AnaRenk;
                        break;
                    case Mod.Bolgeler:
                        c = p.BolgeRenkleri[i];
                        break;
                    case Mod.Nefes:
                        c = p.AnaRenk.Olcekle(0.5 - 0.5 * Math.Cos(t * hiz * 2 * Math.PI / 3.0));
                        break;
                    case Mod.RenkDongusu:
                        c = Renk.Hsv(t * hiz * 0.15);
                        break;
                    case Mod.GokkusagiDalgasi:
                        c = Renk.Hsv(t * hiz * 0.15 - (double)i / r.Length);
                        break;
                    default:
                        c = Renk.Siyah;
                        break;
                }
                r[i] = c.Olcekle(parlaklik);
            }
            return r;
        }
    }
}
