using System;
using System.Threading;

namespace K55Kontrol
{
    // Protokolü doğrulamak için konsol testi: bölgeleri tek tek yakar, sonra donanım moduna döner.
    internal static class ProtokolTesti
    {
        private static int Main()
        {
            using (K55 k = K55.Bul())
            {
                if (k == null)
                {
                    Console.WriteLine("K55 RGB PRO bulunamadi.");
                    return 1;
                }
                Console.WriteLine("Acildi: " + k.Yol);
                k.YazilimModunaGec();
                Console.WriteLine("Aydinlatma uc noktasi: 0x" + k.UcNoktasi.ToString("X2"));

                Renk kirmizi = new Renk(255, 0, 0);
                Renk[] renkler = new Renk[K55.BolgeSayisi];
                for (int b = 0; b < K55.BolgeSayisi; b++)
                {
                    for (int i = 0; i < renkler.Length; i++)
                        renkler[i] = i == b ? kirmizi : Renk.Siyah;
                    k.RenkleriYaz(renkler);
                    Console.WriteLine("Bolge " + (b + 1) + " kirmizi");
                    Thread.Sleep(1500);
                }

                foreach (Renk c in new[] { kirmizi, new Renk(0, 255, 0), new Renk(0, 0, 255), new Renk(255, 255, 255) })
                {
                    for (int i = 0; i < renkler.Length; i++) renkler[i] = c;
                    k.RenkleriYaz(renkler);
                    Console.WriteLine("Hepsi: " + c.Hex);
                    Thread.Sleep(1200);
                }
                Console.WriteLine("Donanim moduna donuluyor.");
            }
            return 0;
        }
    }
}
