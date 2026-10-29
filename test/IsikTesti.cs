using System;
using System.Threading;

namespace K55Kontrol
{
    // Işıkların klavyedeki konumunu bulmak için: her ışığı ayrı bir renkte yakar ve verilen süre (sn) boyunca tutar.
    // Kullanım: IsikTesti [saniye]
    internal static class IsikTesti
    {
        private static int Main(string[] args)
        {
            int sure;
            if (args.Length == 0 || !int.TryParse(args[0], out sure)) sure = 60;

            Renk[] renkler =
            {
                new Renk(255, 255, 255),   // 0 beyaz
                new Renk(255, 0, 0),       // 1 kırmızı
                new Renk(0, 255, 0),       // 2 yeşil
                new Renk(0, 0, 255),       // 3 mavi
                new Renk(255, 200, 0),     // 4 sarı
                new Renk(255, 0, 255)      // 5 mor
            };

            using (K55 k = K55.Bul())
            {
                if (k == null)
                {
                    Console.WriteLine("K55 RGB PRO bulunamadi.");
                    return 1;
                }
                k.YazilimModunaGec();
                Console.WriteLine("0 beyaz, 1 kirmizi, 2 yesil, 3 mavi, 4 sari, 5 mor - " + sure + " sn");
                for (int i = 0; i < sure; i++)
                {
                    k.RenkleriYaz(renkler);
                    Thread.Sleep(1000);
                }
            }
            return 0;
        }
    }
}
