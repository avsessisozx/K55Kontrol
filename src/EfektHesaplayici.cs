using System;

namespace K55Kontrol
{
    // Dış veriye dayanan efektlerin kaynakları. Yalnızca etkin efektin ihtiyaç duyduğu kaynak çalışır
    // (ör. mikrofon/ekran/tuş dinleme gereksiz yere açık kalmaz). ModaGoreAyarla arayüz iş parçacığından çağrılmalıdır.
    public sealed class EfektKaynaklari : IDisposable
    {
        public readonly SesAnalizcisi Ses = new SesAnalizcisi();
        public readonly EkranOrnekleyici Ekran = new EkranOrnekleyici();
        public readonly TusDinleyici Tus = new TusDinleyici();
        public readonly SistemGostergesi Gosterge = new SistemGostergesi();

        public void ModaGoreAyarla(Mod m)
        {
            if (m == Mod.Muzik) Ses.Baslat(); else Ses.Durdur();
            if (m == Mod.Ekran) Ekran.Baslat(); else Ekran.Durdur();
            if (m == Mod.TusTepkisi) Tus.Baslat(); else Tus.Durdur();
            if (m == Mod.Gosterge) Gosterge.Baslat(); else Gosterge.Durdur();
        }

        public void Dispose()
        {
            Ses.Dispose();
            Ekran.Dispose();
            Tus.Dispose();
            Gosterge.Dispose();
        }
    }

    // Efekt motorunun her karede çağırdığı hesaplayıcı. Dış veriye dayanan efektler kareler arasında
    // yumuşatma yaptığı için durum tutar; basit efektler Efektler.Hesapla'ya bırakılır.
    public sealed class EfektHesaplayici
    {
        private readonly EfektKaynaklari kaynaklar;
        private readonly double[] muzikSeviye = new double[K55.BolgeSayisi];
        private readonly double[][] ekranRenk = new double[K55.BolgeSayisi][];
        private double gosterge;
        private double oncekiZaman = -1;

        private static readonly Renk Yesil = new Renk(0, 230, 90), Sari = new Renk(255, 200, 0), Kirmizi = new Renk(255, 30, 30);

        public EfektHesaplayici(EfektKaynaklari kaynaklar)
        {
            this.kaynaklar = kaynaklar;
            for (int i = 0; i < ekranRenk.Length; i++) ekranRenk[i] = new double[3];
        }

        // Göstergenin son okunan (yumuşatılmamış) değeri; arayüzde "şu an" bilgisi için.
        public double? GostergeDegeri(Profil p) => kaynaklar.Gosterge.Deger(p.GostergeKaynagi);

        public Renk[] Hesapla(Profil p, double t)
        {
            double dt = oncekiZaman < 0 ? 0.033 : Math.Max(0, Math.Min(0.2, t - oncekiZaman));
            oncekiZaman = t;

            Renk[] r;
            switch (p.Mod)
            {
                case Mod.Muzik: r = Muzik(p, t, dt); break;
                case Mod.Ekran: r = Ekran(p, dt); break;
                case Mod.TusTepkisi: r = TusTepkisi(p); break;
                case Mod.Gosterge: r = Gosterge(p, dt); break;
                default: return Efektler.Hesapla(p, t);
            }
            double parlaklik = p.Parlaklik / 100.0;
            for (int i = 0; i < r.Length; i++) r[i] = r[i].Olcekle(parlaklik);
            return r;
        }

        private Renk[] Muzik(Profil p, double t, double dt)
        {
            float[] bantlar = kaynaklar.Ses.Bantlar;
            double hassasiyet = 0.55 + p.Hiz * 0.09;
            Renk[] r = new Renk[K55.BolgeSayisi];
            for (int i = 0; i < r.Length; i++)
            {
                double hedef = Math.Min(1, bantlar[i] * hassasiyet);
                // Hızlı yüksel, yavaş sön.
                muzikSeviye[i] = hedef > muzikSeviye[i] ? hedef : Math.Max(hedef, muzikSeviye[i] - dt * 1.8);
                double isik = 0.06 + 0.94 * Math.Pow(muzikSeviye[i], 1.3);
                Renk renk = p.MuzikGokkusagi ? Renk.Hsv((double)i / r.Length + t * 0.02) : p.AnaRenk;
                r[i] = renk.Olcekle(isik);
            }
            return r;
        }

        private Renk[] Ekran(Profil p, double dt)
        {
            Renk[] hedef = kaynaklar.Ekran.Renkler;
            double k = 1 - Math.Exp(-dt * (2 + p.Hiz * 1.6));
            Renk[] r = new Renk[K55.BolgeSayisi];
            for (int i = 0; i < r.Length; i++)
            {
                double[] c = ekranRenk[i];
                c[0] += (hedef[i].R - c[0]) * k;
                c[1] += (hedef[i].G - c[1]) * k;
                c[2] += (hedef[i].B - c[2]) * k;
                r[i] = new Renk((int)c[0], (int)c[1], (int)c[2]);
            }
            return r;
        }

        private Renk[] TusTepkisi(Profil p)
        {
            double[] gecen = kaynaklar.Tus.GecenSureler();
            double sure = 1.6 - p.Hiz * 0.13;   // 1 -> 1.47 sn, 10 -> 0.3 sn
            double[] kendi = new double[gecen.Length];
            for (int i = 0; i < kendi.Length; i++) kendi[i] = Math.Max(0, 1 - gecen[i] / sure);

            Renk[] r = new Renk[K55.BolgeSayisi];
            for (int i = 0; i < r.Length; i++)
            {
                double komsu = Math.Max(i > 0 ? kendi[i - 1] : 0, i < r.Length - 1 ? kendi[i + 1] : 0);
                double n = Math.Max(kendi[i], komsu * 0.35);
                r[i] = Renk.Karistir(p.AnaRenk, p.IkinciRenk, n * n * (3 - 2 * n));
            }
            return r;
        }

        private Renk[] Gosterge(Profil p, double dt)
        {
            double? deger = kaynaklar.Gosterge.Deger(p.GostergeKaynagi);
            double oran = deger.HasValue ? (deger.Value - p.GostergeAlt) / Math.Max(1, p.GostergeUst - p.GostergeAlt) : 0;
            oran = Math.Max(0, Math.Min(1, oran));
            gosterge += (oran - gosterge) * (1 - Math.Exp(-dt * 4));

            Renk[] r = new Renk[K55.BolgeSayisi];
            for (int i = 0; i < r.Length; i++)
            {
                double dolu = Math.Max(0, Math.Min(1, gosterge * r.Length - i));
                double konum = (i + 0.5) / r.Length;
                Renk renk = konum < 0.5 ? Renk.Karistir(Yesil, Sari, konum * 2) : Renk.Karistir(Sari, Kirmizi, (konum - 0.5) * 2);
                r[i] = renk.Olcekle(0.07 + 0.93 * dolu);
            }
            return r;
        }
    }
}
