using System;
using System.Linq;
using System.Threading;

namespace K55Kontrol
{
    // Efekt veri kaynaklarını klavyeye dokunmadan dener: ses bantları, ekran renkleri, sistem göstergeleri.
    internal static class KaynakTesti
    {
        private static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("--- Gösterge kaynakları");
            foreach (var k in SistemGostergesi.Kaynaklar())
                Console.WriteLine("  " + k.Anahtar + " | " + k.Ad + " | " + k.Birim);

            using (var g = new SistemGostergesi())
            {
                g.Baslat();
                Thread.Sleep(2500);
                foreach (string a in new[] { SistemGostergesi.Cpu, SistemGostergesi.Ram, SistemGostergesi.GpuSicaklik })
                    Console.WriteLine("  " + a + " = " + (g.Deger(a)?.ToString("0.0") ?? "yok"));
            }

            Console.WriteLine("--- Ses bantları (3 sn)");
            using (var s = new SesAnalizcisi())
            {
                s.Baslat();
                for (int i = 0; i < 6; i++)
                {
                    Thread.Sleep(500);
                    Console.WriteLine("  " + string.Join(" ", s.Bantlar.Select(b => b.ToString("0.00"))));
                }
                Console.WriteLine("  hata: " + (s.SonHata ?? "yok"));
            }

            Console.WriteLine("--- Ekran şeritleri");
            using (var e = new EkranOrnekleyici())
            {
                e.Baslat();
                Thread.Sleep(600);
                Console.WriteLine("  " + string.Join(" ", e.Renkler.Select(r => r.Hex)));
            }

            Console.WriteLine("--- Tuş haritası (örnek bölgeler)");
            Console.WriteLine("  Esc=" + TusHaritasi.Bolge(0x01, false) + " A=" + TusHaritasi.Bolge(0x1E, false) +
                              " Boşluk=" + TusHaritasi.Bolge(0x39, false) + " Enter=" + TusHaritasi.Bolge(0x1C, false) +
                              " Sağ ok=" + TusHaritasi.Bolge(0x4D, true) + " Num+=" + TusHaritasi.Bolge(0x4E, false));
        }
    }
}
