using System;
using System.Linq;
using System.Threading;

namespace K55Kontrol
{
    // Klavye hafızasına sabit bir renk yazar ve klavyeyi kendi moduna geçirir; cihaz cevaplarını yazdırır.
    // Kullanım: HafizaTesti [RRGGBB]
    internal static class HafizaTesti
    {
        private static int Main(string[] args)
        {
            Renk renk;
            if (args.Length == 0 || !Renk.HexOku(args[0], out renk)) renk = new Renk(0, 255, 0);

            using (K55 k = K55.Bul())
            {
                if (k == null) { Console.WriteLine("K55 RGB PRO bulunamadi."); return 1; }
                k.YazilimModunaGec();
                var ayar = new DonanimAydinlatmasi { Efekt = DonanimEfekti.Sabit, Renk1 = renk };
                byte[] veri = ayar.Veri();
                Console.WriteLine("Yazilacak: " + string.Join(" ", veri.Select(b => b.ToString("X2"))));
                k.DonanimAydinlatmasiniYaz(veri);
                k.DonanimModunaGec();
                Console.WriteLine("Klavye kendi moduna gecti; hafizadaki renk gorunmeli.");
                Thread.Sleep(300);
            }
            return 0;
        }
    }
}
