using System.Collections.Generic;
using System.Runtime.Serialization;

namespace K55Kontrol
{
    // Klavyenin hafızasında saklanabilen (uygulama kapalıyken oynatılan) efektler. Değerler cihazın efekt kodlarıdır.
    public enum DonanimEfekti
    {
        Sabit = 0x207E,
        GokkusagiDalgasi = 0xB94C,
        RenkNabzi = 0xAD4F,
        RenkKaymasi = 0xA5FA,
        RenkDalgasi = 0x7BFF,
        TusaTepki = 0x09A2,
        Vizor = 0x90C0,
        Suluboya = 0x0022
    }

    public enum DonanimHizi { Yavas = 3, Orta = 4, Hizli = 5 }

    public enum DonanimYonu { Asagi = 1, Yukari = 2, Sag = 4, Sol = 5 }

    // Klavye hafızasına yazılacak aydınlatma ayarı.
    //
    // Hafıza dosyası 0x6D61'in biçimi (iCUE'nun kaydı gözlemlenerek çözüldü):
    //   [efekt kodu, 2 bayt LE] [renk türü] [hız] [yön] [00] [renk sayısı] [renkler: parlaklık, B, G, R]
    //   [ışık sayısı] [ışık listesi]
    // Renk türü: 0 = renk yok (gökkuşağı), 1 = seçilen renkler, 2 = rastgele, 3 = arka plan rengi (suluboya).
    [DataContract]
    public sealed class DonanimAydinlatmasi
    {
        [DataMember(Order = 0)] public DonanimEfekti Efekt;
        [DataMember(Order = 1)] public Renk Renk1;
        [DataMember(Order = 2)] public Renk Renk2;
        [DataMember(Order = 3)] public bool RastgeleRenkler;
        [DataMember(Order = 4)] public DonanimHizi Hiz;
        [DataMember(Order = 5)] public DonanimYonu Yon;
        [DataMember(Order = 6)] public int Parlaklik;

        public DonanimAydinlatmasi() => Varsayilanlar();

        [OnDeserializing]
        private void SeriCozmeOncesi(StreamingContext c) => Varsayilanlar();

        private void Varsayilanlar()
        {
            Efekt = DonanimEfekti.Sabit;
            Renk1 = new Renk(255, 0, 0);
            Renk2 = new Renk(0, 80, 255);
            RastgeleRenkler = true;
            Hiz = DonanimHizi.Orta;
            Yon = DonanimYonu.Sag;
            Parlaklik = 100;
        }

        // Uygulamanın bir profilini klavyede saklanabilen en yakın karşılığına çevirir.
        // Klavyenin kendi modu için null döner (hafıza olduğu gibi kalmalı).
        public static DonanimAydinlatmasi ProfildenOlustur(Profil p)
        {
            var d = new DonanimAydinlatmasi
            {
                Parlaklik = p.Parlaklik,
                Hiz = p.Hiz <= 3 ? DonanimHizi.Yavas : p.Hiz <= 7 ? DonanimHizi.Orta : DonanimHizi.Hizli,
                Renk1 = p.AnaRenk
            };
            switch (p.Mod)
            {
                case Mod.Donanim:
                    return null;
                case Mod.Kapali:
                    d.Renk1 = Renk.Siyah;
                    break;
                case Mod.Bolgeler:
                    // Hafızada tek renkli sabit efekt var; tuş bölgelerinin ortalaması alınır.
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int i = 0; i < p.BolgeRenkleri.Length; i++) { r += p.BolgeRenkleri[i].R; g += p.BolgeRenkleri[i].G; b += p.BolgeRenkleri[i].B; n++; }
                    d.Renk1 = new Renk(r / n, g / n, b / n);
                    break;
                case Mod.Nefes:
                    d.Efekt = DonanimEfekti.RenkNabzi;
                    d.RastgeleRenkler = false;
                    d.Renk2 = p.AnaRenk;
                    break;
                case Mod.RenkDongusu:
                    d.Efekt = DonanimEfekti.RenkKaymasi;
                    break;
                case Mod.GokkusagiDalgasi:
                    d.Efekt = DonanimEfekti.GokkusagiDalgasi;
                    break;
                case Mod.Muzik:
                    d.Efekt = DonanimEfekti.RenkDalgasi;
                    break;
                case Mod.TusTepkisi:
                    d.Efekt = DonanimEfekti.TusaTepki;
                    d.RastgeleRenkler = false;
                    d.Renk1 = p.IkinciRenk;
                    d.Renk2 = p.AnaRenk;
                    break;
                case Mod.Gosterge:
                    d.Renk1 = new Renk(0, 230, 90);
                    break;
                // Sabit ve Ekran rengi: profilin ana rengiyle sabit renk.
            }
            return d;
        }

        public DonanimAydinlatmasi Kopya() => (DonanimAydinlatmasi)MemberwiseClone();

        // Kısa açıklama, ör. "Renk dalgası · rastgele renkler · orta hız".
        public string Ozet()
        {
            var parcalar = new List<string> { Ad(Efekt) };
            if (Efekt == DonanimEfekti.Sabit || Efekt == DonanimEfekti.Suluboya)
                parcalar.Add(Renk1.Hex);
            else if (RenkSecimliMi(Efekt))
                parcalar.Add(RastgeleRenkler ? "rastgele renkler" : Renk1.Hex + " / " + Renk2.Hex);
            if (HiziVar(Efekt))
                parcalar.Add(Hiz == DonanimHizi.Yavas ? "yavaş" : Hiz == DonanimHizi.Hizli ? "hızlı" : "orta hız");
            if (Parlaklik < 100) parcalar.Add("%" + Parlaklik);
            return string.Join(" · ", parcalar);
        }

        // iCUE'nun yazdığı ışık listesi: 0 = Windows kilidi göstergesi, 1–5 = tuş bölgeleri.
        private static readonly byte[] Isiklar = { 4, 3, 1, 2, 0, 5 };

        public static bool YonuVar(DonanimEfekti e) =>
            e == DonanimEfekti.GokkusagiDalgasi || e == DonanimEfekti.RenkDalgasi || e == DonanimEfekti.Vizor;

        public static bool HiziVar(DonanimEfekti e) => e != DonanimEfekti.Sabit;

        // Efekt rastgele renk ya da seçilen iki renkle çalışabiliyor mu?
        public static bool RenkSecimliMi(DonanimEfekti e) =>
            e == DonanimEfekti.RenkNabzi || e == DonanimEfekti.RenkKaymasi || e == DonanimEfekti.RenkDalgasi ||
            e == DonanimEfekti.TusaTepki || e == DonanimEfekti.Vizor;

        public static string Ad(DonanimEfekti e)
        {
            switch (e)
            {
                case DonanimEfekti.GokkusagiDalgasi: return "Gökkuşağı dalgası";
                case DonanimEfekti.RenkNabzi: return "Renk nabzı";
                case DonanimEfekti.RenkKaymasi: return "Renk kayması";
                case DonanimEfekti.RenkDalgasi: return "Renk dalgası";
                case DonanimEfekti.TusaTepki: return "Tuşa tepki";
                case DonanimEfekti.Vizor: return "Vizör";
                case DonanimEfekti.Suluboya: return "Suluboya";
                default: return "Sabit renk";
            }
        }

        public byte[] Veri()
        {
            byte renkTuru;
            List<Renk> renkler = new List<Renk>();
            switch (Efekt)
            {
                case DonanimEfekti.Sabit:
                    renkTuru = 1;
                    renkler.Add(Renk1);
                    break;
                case DonanimEfekti.GokkusagiDalgasi:
                    renkTuru = 0;
                    break;
                case DonanimEfekti.Suluboya:
                    renkTuru = 3;
                    renkler.Add(Renk1);   // arka plan rengi
                    break;
                default:
                    if (RastgeleRenkler) renkTuru = 2;
                    else
                    {
                        renkTuru = 1;
                        renkler.Add(Renk1);
                        renkler.Add(Renk2);
                    }
                    break;
            }

            byte parlaklik = (byte)(System.Math.Max(0, System.Math.Min(100, Parlaklik)) * 255 / 100);
            var v = new List<byte>
            {
                (byte)((int)Efekt & 0xFF), (byte)((int)Efekt >> 8),
                renkTuru,
                HiziVar(Efekt) ? (byte)Hiz : (byte)0,
                YonuVar(Efekt) ? (byte)Yon : (byte)0,
                0,
                (byte)renkler.Count
            };
            foreach (Renk r in renkler) { v.Add(parlaklik); v.Add(r.B); v.Add(r.G); v.Add(r.R); }
            v.Add((byte)Isiklar.Length);
            v.AddRange(Isiklar);
            return v.ToArray();
        }
    }
}
