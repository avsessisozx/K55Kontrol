using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;

namespace K55Kontrol
{
    public enum Mod
    {
        Sabit,
        Bolgeler,
        Nefes,
        RenkDongusu,
        GokkusagiDalgasi,
        Kapali,
        Donanim,
        // Dosyadaki sayısal değerler değişmesin diye yeni modlar hep sona eklenir.
        Muzik,
        Ekran,
        TusTepkisi,
        Gosterge
    }

    // Bir aydınlatma ayarı seti. Uygulamalar listesindeki programlardan biri çalışınca otomatik etkinleşebilir.
    [DataContract]
    public sealed class Profil
    {
        [DataMember(Order = 0)] public string Ad;
        [DataMember(Order = 1)] public Mod Mod;
        [DataMember(Order = 2)] public Renk AnaRenk;
        [DataMember(Order = 3)] public Renk[] BolgeRenkleri;
        [DataMember(Order = 4)] public int Parlaklik;   // 0-100
        [DataMember(Order = 5)] public int Hiz;         // 1-10
        [DataMember(Order = 6)] public List<string> Uygulamalar;
        [DataMember(Order = 7)] public Renk IkinciRenk;          // tuşa tepki rengi
        [DataMember(Order = 8)] public bool MuzikGokkusagi;      // müzik efektinde bölgeler gökkuşağı renginde
        [DataMember(Order = 9)] public string GostergeKaynagi;
        [DataMember(Order = 10)] public int GostergeAlt;
        [DataMember(Order = 11)] public int GostergeUst;

        public Profil()
        {
            Varsayilanlar();
        }

        // Seri çözme kurucuyu çalıştırmaz; dosyada olmayan alanlar bu değerlerle kalır.
        [OnDeserializing]
        private void SeriCozmeOncesi(StreamingContext c) => Varsayilanlar();

        private void Varsayilanlar()
        {
            Ad = "Yeni profil";
            Mod = Mod.Sabit;
            AnaRenk = new Renk(255, 0, 0);
            BolgeRenkleri = new[]
            {
                new Renk(255, 0, 0), new Renk(255, 140, 0), new Renk(255, 230, 0),
                new Renk(0, 220, 80), new Renk(0, 170, 255), new Renk(200, 0, 255)
            };
            Parlaklik = 100;
            Hiz = 5;
            Uygulamalar = new List<string>();
            IkinciRenk = new Renk(255, 255, 255);
            MuzikGokkusagi = true;
            GostergeKaynagi = SistemGostergesi.Cpu;
            GostergeAlt = 0;
            GostergeUst = 100;
        }

        public Profil Kopyala(string yeniAd)
        {
            return new Profil
            {
                Ad = yeniAd,
                Mod = Mod,
                AnaRenk = AnaRenk,
                BolgeRenkleri = (Renk[])BolgeRenkleri.Clone(),
                Parlaklik = Parlaklik,
                Hiz = Hiz,
                IkinciRenk = IkinciRenk,
                MuzikGokkusagi = MuzikGokkusagi,
                GostergeKaynagi = GostergeKaynagi,
                GostergeAlt = GostergeAlt,
                GostergeUst = GostergeUst
            };
        }

        // Dosyadan okunan bozuk değerleri onarır.
        internal void Duzelt()
        {
            if (string.IsNullOrWhiteSpace(Ad)) Ad = "Profil";
            Renk[] varsayilan = new Profil().BolgeRenkleri;
            if (BolgeRenkleri == null || BolgeRenkleri.Length != K55.BolgeSayisi)
            {
                if (BolgeRenkleri != null)
                    Array.Copy(BolgeRenkleri, varsayilan, Math.Min(BolgeRenkleri.Length, varsayilan.Length));
                BolgeRenkleri = varsayilan;
            }
            Parlaklik = Math.Max(0, Math.Min(100, Parlaklik));
            Hiz = Math.Max(1, Math.Min(10, Hiz));
            if (!Enum.IsDefined(typeof(Mod), Mod)) Mod = Mod.Sabit;
            if (Uygulamalar == null) Uygulamalar = new List<string>();
            if (string.IsNullOrEmpty(GostergeKaynagi)) GostergeKaynagi = SistemGostergesi.Cpu;
            if (GostergeUst <= GostergeAlt) { GostergeAlt = 0; GostergeUst = 100; }
        }
    }

    // Belirli saatler arasında bir profili devreye sokan kural. Bitiş başlangıçtan küçükse gece yarısını aşar.
    [DataContract]
    public sealed class ZamanKurali
    {
        [DataMember(Order = 0)] public bool Etkin = true;
        [DataMember(Order = 1)] public int Baslangic = 22 * 60;   // gece yarısından itibaren dakika
        [DataMember(Order = 2)] public int Bitis = 7 * 60;
        [DataMember(Order = 3)] public string ProfilAdi;

        public bool SimdiEtkin(DateTime an)
        {
            if (!Etkin || Baslangic == Bitis) return false;
            int d = an.Hour * 60 + an.Minute;
            return Baslangic < Bitis ? d >= Baslangic && d < Bitis : d >= Baslangic || d < Bitis;
        }

        public static string SaatYaz(int dakika) => string.Format("{0:00}:{1:00}", dakika / 60, dakika % 60);

        public static bool SaatOku(string s, out int dakika)
        {
            dakika = 0;
            s = (s ?? "").Trim().Replace('.', ':');
            if (s.Length > 0 && s.IndexOf(':') < 0) s += ":00";
            string[] p = s.Split(':');
            int sa, dk;
            if (p.Length != 2 || !int.TryParse(p[0], out sa) || !int.TryParse(p[1], out dk)) return false;
            if (sa < 0 || sa > 23 || dk < 0 || dk > 59) return false;
            dakika = sa * 60 + dk;
            return true;
        }
    }

    public enum Eylem
    {
        SonrakiProfil,
        OncekiProfil,
        IsiklariAcKapat,
        ParlaklikArtir,
        ParlaklikAzalt,
        SonrakiEfekt
    }

    // Genel kısayol: Windows sanal tuş kodu + değiştiriciler (Alt=1, Ctrl=2, Shift=4, Win=8). Tus=0 atanmamış demektir.
    [DataContract]
    public sealed class Kisayol
    {
        public const int Alt = 1, Ctrl = 2, Shift = 4, Win = 8;

        [DataMember(Order = 0)] public Eylem Eylem;
        [DataMember(Order = 1)] public int Tus;
        [DataMember(Order = 2)] public int Degistirici;

        public static List<Kisayol> Varsayilanlar() => new List<Kisayol>
        {
            new Kisayol { Eylem = Eylem.SonrakiProfil, Tus = 0x27, Degistirici = Ctrl | Alt },     // →
            new Kisayol { Eylem = Eylem.OncekiProfil, Tus = 0x25, Degistirici = Ctrl | Alt },      // ←
            new Kisayol { Eylem = Eylem.IsiklariAcKapat, Tus = 0x4B, Degistirici = Ctrl | Alt },   // K
            new Kisayol { Eylem = Eylem.ParlaklikArtir, Tus = 0x26, Degistirici = Ctrl | Alt },    // ↑
            new Kisayol { Eylem = Eylem.ParlaklikAzalt, Tus = 0x28, Degistirici = Ctrl | Alt },    // ↓
            new Kisayol { Eylem = Eylem.SonrakiEfekt, Tus = 0x45, Degistirici = Ctrl | Alt },      // E
        };
    }

    // Kullanıcı ayarları; %APPDATA%\K55Kontrol\ayarlar.json dosyasında saklanır.
    [DataContract]
    public sealed class Ayarlar
    {
        [DataMember(Order = 0)] public List<Profil> Profiller;
        [DataMember(Order = 1)] public string SeciliProfilAdi;
        [DataMember(Order = 2)] public bool TepsiyeKucult;
        [DataMember(Order = 3)] public bool OtomatikGecis;
        [DataMember(Order = 4)] public List<ZamanKurali> Zamanlamalar;
        [DataMember(Order = 5)] public bool KilitteKapat;
        [DataMember(Order = 6)] public bool EkranKapanincaKapat;
        [DataMember(Order = 7)] public bool BostaKapat;
        [DataMember(Order = 8)] public int BostaDakika;
        [DataMember(Order = 9)] public bool KisayollarEtkin;
        [DataMember(Order = 10)] public List<Kisayol> Kisayollar;
        [DataMember(Order = 11)] public List<GAtama> GAtamalari;

        private static readonly string Klasor = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "K55Kontrol");
        private static readonly string Dosya = Path.Combine(Klasor, "ayarlar.json");

        private const string CalistirAnahtari = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string CalistirAdi = "K55Kontrol";

        public Ayarlar()
        {
            Varsayilanlar();
        }

        [OnDeserializing]
        private void SeriCozmeOncesi(StreamingContext c) => Varsayilanlar();

        private void Varsayilanlar()
        {
            Profiller = new List<Profil>();
            TepsiyeKucult = true;
            OtomatikGecis = true;
            Zamanlamalar = new List<ZamanKurali>();
            KilitteKapat = true;
            EkranKapanincaKapat = true;
            BostaKapat = false;
            BostaDakika = 10;
            KisayollarEtkin = true;
            Kisayollar = Kisayol.Varsayilanlar();
            GAtamalari = new List<GAtama>();
        }

        // Bir G tuşunun atamasını döndürür; yoksa boş bir atama oluşturur.
        public GAtama GAtamaBul(int tus)
        {
            GAtama a = GAtamalari.FirstOrDefault(x => x.Tus == tus);
            if (a == null)
            {
                a = new GAtama { Tus = tus, Tur = GTuru.Yok };
                GAtamalari.Add(a);
            }
            if (a.Adimlar == null) a.Adimlar = new List<MakroAdimi>();
            return a;
        }

        public Profil SeciliProfil
        {
            get { return Bul(SeciliProfilAdi) ?? Profiller[0]; }
            set { SeciliProfilAdi = value.Ad; }
        }

        public Profil Bul(string ad)
        {
            return Profiller.FirstOrDefault(p => string.Equals(p.Ad, ad, StringComparison.CurrentCultureIgnoreCase));
        }

        // "Profil", "Profil 2", "Profil 3"... şeklinde kullanılmayan bir ad üretir.
        public string BosAd(string temel)
        {
            if (Bul(temel) == null) return temel;
            for (int i = 2; ; i++)
                if (Bul(temel + " " + i) == null) return temel + " " + i;
        }

        // Profil adı değişince ada göre tutulan başvuruları da günceller.
        public void ProfilAdiDegisti(string eski, string yeni)
        {
            if (string.Equals(SeciliProfilAdi, eski, StringComparison.CurrentCultureIgnoreCase))
                SeciliProfilAdi = yeni;
            foreach (ZamanKurali k in Zamanlamalar)
                if (string.Equals(k.ProfilAdi, eski, StringComparison.CurrentCultureIgnoreCase))
                    k.ProfilAdi = yeni;
            foreach (GAtama g in GAtamalari)
                if (string.Equals(g.UygulamaEylemi, GAtama.ProfilAnahtari(eski), StringComparison.CurrentCultureIgnoreCase))
                    g.UygulamaEylemi = GAtama.ProfilAnahtari(yeni);
        }

        public Kisayol KisayolBul(Eylem e)
        {
            Kisayol k = Kisayollar.FirstOrDefault(x => x.Eylem == e);
            if (k == null)
            {
                k = new Kisayol { Eylem = e };
                Kisayollar.Add(k);
            }
            return k;
        }

        public static Ayarlar Yukle()
        {
            Ayarlar a = null;
            try
            {
                if (File.Exists(Dosya))
                    using (FileStream f = File.OpenRead(Dosya))
                        a = (Ayarlar)new DataContractJsonSerializer(typeof(Ayarlar)).ReadObject(f);
            }
            catch (Exception) { a = null; }

            if (a == null) a = Varsayilan();
            if (a.Profiller == null) a.Profiller = new List<Profil>();
            a.Profiller.RemoveAll(p => p == null);
            foreach (Profil p in a.Profiller) p.Duzelt();
            if (a.Profiller.Count == 0) a.Profiller.AddRange(Varsayilan().Profiller);
            if (a.Zamanlamalar == null) a.Zamanlamalar = new List<ZamanKurali>();
            a.Zamanlamalar.RemoveAll(z => z == null);
            if (a.Kisayollar == null) a.Kisayollar = Kisayol.Varsayilanlar();
            a.Kisayollar.RemoveAll(k => k == null);
            if (a.GAtamalari == null) a.GAtamalari = new List<GAtama>();
            a.GAtamalari.RemoveAll(g => g == null || g.Tus < 1 || g.Tus > GTuslari.TusSayisi);
            a.BostaDakika = Math.Max(1, Math.Min(120, a.BostaDakika));
            return a;
        }

        private static Ayarlar Varsayilan()
        {
            Ayarlar a = new Ayarlar();
            a.Profiller.Add(new Profil { Ad = "Varsayılan", Mod = Mod.Sabit, AnaRenk = new Renk(255, 0, 0) });
            a.Profiller.Add(new Profil
            {
                Ad = "Oyun",
                Mod = Mod.GokkusagiDalgasi,
                Hiz = 6,
                Uygulamalar = new List<string> { "cs2" }
            });
            a.Profiller.Add(new Profil { Ad = "Gece", Mod = Mod.Sabit, AnaRenk = new Renk(255, 120, 30), Parlaklik = 25 });
            a.SeciliProfilAdi = "Varsayılan";
            return a;
        }

        public void Kaydet()
        {
            try
            {
                Directory.CreateDirectory(Klasor);
                string gecici = Dosya + ".tmp";
                using (FileStream f = File.Create(gecici))
                using (var yazici = JsonReaderWriterFactory.CreateJsonWriter(f, System.Text.Encoding.UTF8, false, true, "  "))
                    new DataContractJsonSerializer(typeof(Ayarlar)).WriteObject(yazici, this);
                if (File.Exists(Dosya)) File.Replace(gecici, Dosya, null);
                else File.Move(gecici, Dosya);
            }
            catch (Exception) { }
        }

        public static bool WindowsIleBaslat
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(CalistirAnahtari))
                    return k != null && k.GetValue(CalistirAdi) != null;
            }
            set
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(CalistirAnahtari))
                {
                    if (value)
                        k.SetValue(CalistirAdi, "\"" + System.Reflection.Assembly.GetEntryAssembly().Location + "\" /gizli");
                    else
                        k.DeleteValue(CalistirAdi, false);
                }
            }
        }
    }
}
