using System;
using System.Threading;

namespace K55Kontrol
{
    // Windows tuşu kilidini dener ve açar/kapatır.
    // Kullanım: WinKilitTesti [saniye]   kilidi açar, süre kadar tutar, sonra kaldırır (varsayılan 25)
    //           WinKilitTesti ac         kilidi açar ve çıkar (kilit açık kalır)
    //           WinKilitTesti kapa       kilidi kaldırır ve çıkar
    // Kilit açıkken Windows tuşuna basınca Başlat menüsü açılmamalıdır.
    internal static class WinKilitTesti
    {
        private static int Main(string[] args)
        {
            string komut = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "";

            if (komut == "oku")
            {
                using (HidCihaz h = HidCihaz.Ac(K55.VID, K55.PID, 1, 0xFF42))
                {
                    if (h == null) { Console.WriteLine("K55 RGB PRO bulunamadi."); return 1; }
                    Sor(h, 0x01, 0x03, 0x00, 0x02);
                    Thread.Sleep(150);
                    foreach (byte k in new byte[] { 0x45, 0x4A, 0x03, 0x13, 0x00 })
                    {
                        byte[] c = Sor(h, 0x02, k);
                        Console.WriteLine("02 " + k.ToString("X2") + " -> " + (c == null ? "(cevap yok)" : Hex(c)));
                    }
                }
                return 0;
            }

            if (komut == "kapa2" || komut == "kapa3" || komut == "kapa4")
            {
                using (HidCihaz h = HidCihaz.Ac(K55.VID, K55.PID, 1, 0xFF42))
                {
                    if (h == null) { Console.WriteLine("K55 RGB PRO bulunamadi."); return 1; }
                    Sor(h, 0x01, 0x03, 0x00, 0x02);
                    Thread.Sleep(150);
                    if (komut == "kapa2")
                    {
                        Rapor("4A 00", Sor(h, 0x01, 0x4A, 0x00, 0x00));
                        Thread.Sleep(200);
                        Rapor("45 00", Sor(h, 0x01, 0x45, 0x00, 0x00));
                    }
                    else if (komut == "kapa3")
                    {
                        Rapor("45 01", Sor(h, 0x01, 0x45, 0x00, 0x01));
                        Thread.Sleep(200);
                        Rapor("4A 00", Sor(h, 0x01, 0x4A, 0x00, 0x00));
                    }
                    else
                    {
                        Rapor("45 00", Sor(h, 0x01, 0x45, 0x00, 0x00));
                        Thread.Sleep(300);
                        Rapor("45 00", Sor(h, 0x01, 0x45, 0x00, 0x00));
                        Rapor("4A 00", Sor(h, 0x01, 0x4A, 0x00, 0x00));
                    }
                }
                return 0;
            }

            using (K55 k = K55.Bul())
            {
                if (k == null)
                {
                    Console.WriteLine("K55 RGB PRO bulunamadi.");
                    return 1;
                }

                k.YazilimModunaGec();

                if (komut == "ac-birak")
                {
                    // Kilidi yazılım modunda açıp donanım moduna geçer ve kapatmadan çıkar:
                    // kilit donanım modunda da kalıyor mu diye elle sınamak için.
                    try
                    {
                        k.WindowsTusunuAyarla(true);
                        k.DonanimModunaGec();
                        Console.WriteLine("Kilit acik, donanim moduna gecildi. Simdi Windows tusuna basin.");
                    }
                    catch (Exception ex) { Console.WriteLine("HATA: " + ex.Message); }
                    Environment.Exit(0);
                }

                if (komut == "ac" || komut == "kapa" || komut == "ac-hw" || komut == "kapa-hw")
                {
                    if (komut.EndsWith("-hw")) k.DonanimModunaGec();
                    bool engelle = komut.StartsWith("ac");
                    try
                    {
                        k.WindowsTusunuAyarla(engelle);
                        Console.WriteLine((engelle ? "Kilit acildi." : "Kilit kaldirildi.") + " (" + komut + ")");
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("HATA (" + komut + "): " + ex.Message);
                        return 2;
                    }
                }

                int sure = 25;
                if (komut.Length > 0 && !int.TryParse(komut, out sure)) sure = 25;

                Console.WriteLine("Kilit aciliyor...");
                try
                {
                    k.WindowsTusunuAyarla(true);
                    Console.WriteLine("Klavye kabul etti.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("HATA: " + ex.Message);
                    return 2;
                }

                Console.WriteLine("Simdi " + sure + " saniye boyunca Windows tusuna basin; Baslat menusu ACILMAMALI.");
                Thread.Sleep(sure * 1000);

                Console.WriteLine("Kilit kaldiriliyor...");
                try { k.WindowsTusunuAyarla(false); Console.WriteLine("Kilit kaldirildi."); }
                catch (Exception ex) { Console.WriteLine("Kilit kaldirilamadi: " + ex.Message); }
            }

            Console.WriteLine("Bitti.");
            return 0;
        }

        private static void Rapor(string ad, byte[] cevap)
        {
            if (cevap == null) { Console.WriteLine(ad + " -> cevap yok"); return; }
            Console.WriteLine(ad + " -> durum " + (cevap.Length > 3 ? cevap[3].ToString() : "?") + " (" + Hex(cevap) + ")");
        }

        // Ham okuma komutu: 65 baytlık rapor hazırlar, gönderir ve cevabı döndürür.
        private static byte[] Sor(HidCihaz h, byte komut, params byte[] parametreler)
        {
            byte[] r = new byte[65];
            r[1] = 0x08;
            r[2] = komut;
            Buffer.BlockCopy(parametreler, 0, r, 3, parametreler.Length);
            h.GelenleriTemizle();
            h.Yaz(r);
            return h.Oku(400);
        }

        private static string Hex(byte[] b)
        {
            string s = "";
            for (int i = 0; i < b.Length && i < 12; i++) s += b[i].ToString("X2") + " ";
            return s + "(uzunluk " + b.Length + ")";
        }
    }
}
