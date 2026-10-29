using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace K55Kontrol
{
    // Klavyeye dokunmadan çalışan denetimler: ışık eşlemesi, hafıza verisi (iCUE'nun yazdığı baytlarla),
    // profile özel G tuşu çözümlemesi ve eski ayarların aktarılması.
    internal static class BirimTesti
    {
        private static int hata;

        private static int Main()
        {
            IsikEslemesi();
            HafizaVerisi();
            GTusuCozumlemesi();
            EskiAyarlar();
            Console.WriteLine(hata == 0 ? "TUM TESTLER GECTI" : hata + " TEST BASARISIZ");
            return hata == 0 ? 0 : 1;
        }

        private static void IsikEslemesi()
        {
            Renk[] bolgeler = { new Renk(1, 2, 3), new Renk(4, 5, 6), new Renk(7, 8, 9), new Renk(10, 11, 12), new Renk(13, 14, 15) };
            byte[] v = K55.IsikVerisi(bolgeler, new Renk(255, 255, 255));
            // R'ler: gösterge, bölge 1..5; sonra G'ler; sonra B'ler.
            Esit("isik verisi", "FF 01 04 07 0A 0D FF 02 05 08 0B 0E FF 03 06 09 0C 0F", v);
            Esit("gosterge kapali", "00 01 04 07 0A 0D", K55.IsikVerisi(bolgeler, Renk.Siyah).Take(6).ToArray());
        }

        private static void HafizaVerisi()
        {
            // iCUE'nun "Device Memory Mode" kaydı sırasında yazdığı veriler (Frida ile yakalandı).
            Esit("sabit renk", "7E 20 01 00 00 00 01 FF 41 3D FF 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.Sabit, Renk1 = new Renk(255, 61, 65) }.Veri());
            Esit("gokkusagi dalgasi", "4C B9 00 04 04 00 00 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.GokkusagiDalgasi, Hiz = DonanimHizi.Orta, Yon = DonanimYonu.Sag }.Veri());
            Esit("gokkusagi yavas sol", "4C B9 00 03 05 00 00 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.GokkusagiDalgasi, Hiz = DonanimHizi.Yavas, Yon = DonanimYonu.Sol }.Veri());
            Esit("renk nabzi rastgele", "4F AD 02 04 00 00 00 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.RenkNabzi, RastgeleRenkler = true }.Veri());
            Esit("renk nabzi iki renk", "4F AD 01 04 00 00 02 FF 00 00 FF FF FF 00 00 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.RenkNabzi, RastgeleRenkler = false, Renk1 = new Renk(255, 0, 0), Renk2 = new Renk(0, 0, 255) }.Veri());
            Esit("renk kaymasi", "FA A5 02 04 00 00 00 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.RenkKaymasi }.Veri());
            Esit("renk dalgasi", "FF 7B 02 04 04 00 00 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.RenkDalgasi, Yon = DonanimYonu.Sag }.Veri());
            Esit("tusa tepki", "A2 09 02 04 00 00 00 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.TusaTepki }.Veri());
            Esit("vizor", "C0 90 02 04 04 00 00 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.Vizor, Yon = DonanimYonu.Sag }.Veri());
            Esit("suluboya", "22 00 03 04 00 00 01 FF FF FF FF 06 04 03 01 02 00 05",
                new DonanimAydinlatmasi { Efekt = DonanimEfekti.Suluboya, Renk1 = new Renk(255, 255, 255) }.Veri());

            var bolgeli = new Profil { Mod = Mod.Bolgeler, BolgeRenkleri = new[] { new Renk(100, 0, 0), new Renk(0, 100, 0), new Renk(0, 0, 100), new Renk(0, 0, 0), new Renk(0, 0, 0) } };
            Dogru("bolge ortalamasi", DonanimAydinlatmasi.ProfildenOlustur(bolgeli).Renk1.Equals(new Renk(20, 20, 20)));
            Dogru("kendi modu hafizaya yazilmaz", DonanimAydinlatmasi.ProfildenOlustur(new Profil { Mod = Mod.Donanim }) == null);
        }

        private static void GTusuCozumlemesi()
        {
            var a = new Ayarlar();
            Profil oyun = new Profil { Ad = "Oyun" }, gece = new Profil { Ad = "Gece" };
            a.Profiller.Add(oyun);
            a.Profiller.Add(gece);
            a.GAtamaBul(1).Tur = GTuru.Metin;
            a.GAtamaBul(1).Metin = "genel";
            oyun.GAtamalari.Add(new GAtama { Tus = 1, Tur = GTuru.Medya, Adimlar = new System.Collections.Generic.List<MakroAdimi>() });

            Dogru("ozel atama oncelikli", a.GecerliAtama(oyun, 1).Tur == GTuru.Medya);
            Dogru("ozel yoksa genel", a.GecerliAtama(gece, 1).Metin == "genel");
            Dogru("atanmamis tus", a.GecerliAtama(oyun, 2).Tur == GTuru.Yok);

            oyun.GAtamalari.Add(new GAtama { Tus = 3, Tur = GTuru.UygulamaEylemi, UygulamaEylemi = GAtama.ProfilAnahtari("Gece") });
            a.ProfilAdiDegisti("Gece", "Uyku");
            Dogru("profil adi degisince ozel atama guncellenir", Ayarlar.OzelAtama(oyun, 3).UygulamaEylemi == GAtama.ProfilAnahtari("Uyku"));

            Profil kopya = oyun.Kopyala("Oyun 2");
            kopya.GAtamalari[0].Tur = GTuru.Yok;
            Dogru("kopya bagimsiz", Ayarlar.OzelAtama(oyun, 1).Tur == GTuru.Medya);

            Profil okunan = GidipGel(oyun);
            okunan.Duzelt();
            Dogru("ozel atamalar kaydedilir", okunan.GAtamalari.Count == 2 && Ayarlar.OzelAtama(okunan, 1).Tur == GTuru.Medya);
        }

        private static void EskiAyarlar()
        {
            // Eski sürümün 6 renkli bölge dizisi: ilk renk gösterge ışığına gidiyordu.
            var p = new Profil { BolgeRenkleri = new[] { new Renk(9, 9, 9), new Renk(1, 0, 0), new Renk(2, 0, 0), new Renk(3, 0, 0), new Renk(4, 0, 0), new Renk(5, 0, 0) } };
            p.Duzelt();
            Dogru("6 renk 5 bolgeye aktarilir", p.BolgeRenkleri.Length == 5 && p.BolgeRenkleri[0].R == 1 && p.BolgeRenkleri[4].R == 5);

            Profil eski = GidipGel(new Profil { Ad = "Eski" }, gAtamalariniSil: true);
            eski.Duzelt();
            Dogru("G atamasi olmayan eski profil", eski.GAtamalari != null && eski.GAtamalari.Count == 0);
        }

        private static Profil GidipGel(Profil p, bool gAtamalariniSil = false)
        {
            var s = new DataContractJsonSerializer(typeof(Profil));
            using (var m = new MemoryStream())
            {
                s.WriteObject(m, p);
                string json = System.Text.Encoding.UTF8.GetString(m.ToArray());
                if (gAtamalariniSil) json = System.Text.RegularExpressions.Regex.Replace(json, ",\"GAtamalari\":\\[[^\\]]*\\]", "");
                using (var g = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                    return (Profil)s.ReadObject(g);
            }
        }

        private static void Esit(string ad, string beklenen, byte[] gercek)
        {
            string g = string.Join(" ", gercek.Select(b => b.ToString("X2")));
            if (g == beklenen) Console.WriteLine("  gecti: " + ad);
            else { hata++; Console.WriteLine("BASARISIZ: " + ad + "\n  beklenen " + beklenen + "\n  gercek   " + g); }
        }

        private static void Dogru(string ad, bool kosul)
        {
            if (kosul) Console.WriteLine("  gecti: " + ad);
            else { hata++; Console.WriteLine("BASARISIZ: " + ad); }
        }
    }
}
